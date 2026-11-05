# 实体类重构计划（2026-11-03）

> 上位目标见 `docs/project-progress.md`「下一步目标」：文档 = 容器 + 实体集合类；实体只负责
> ①读写自己的字节流 ②暴露属性字段；渲染只经设备层底层接口。本档只写**怎么落地**。

## 0. 不变量（任何车道都不得破）

1. 零第三方依赖进 `DwgParser.Core`（`DwgParser.Core.csproj` 现无 `ProjectReference`，仅 `InternalsVisibleTo`）。
2. 解析失败不抛异常；未表态的实体/渲染分派必须**显式炸或显式标注**，不许静默回落。
3. 写侧产物字节不变（本阶段写侧只做契约，不改行为）。
4. 单文件 ≤ 800 行（仓内无 `SourceFileSizeTests`，该门**目前只是文档承诺**，见 §6 G5）。
5. **字段只增不改名**：并行车道在写侧用 `DwgObjectRecord` 的字段名，改名即跨道冲突。

## 1. 现状事实基线（证据）

### 1.1 实体层

| 事实 | 证据 |
|---|---|
| 根基类是**扁平上帝类**：公共字段与全部几何字段（起点/终点/圆心/Points/Bulges/TextValue…）堆在一处 | `Objects/DwgEntity.cs:19`（443 行） |
| 接口层是纯属性袋（`IText`/`IMText`/`ISpline`/`ILwPolyline`… 共 30+） | `Objects/Entities/IText.cs:3` 等 |
| 族中间抽象类持有解码（`MTextEntity.ReadBody` / `SplineEntity.ReadGeometry` / `LwPolylineEntity`） | `Objects/Entities/MTextEntity.cs:27`、`SplineEntity.cs:43`、`LwPolylineEntity.cs:11` |
| 版本具体类=薄开关（8–9 行，仅 override 4 个 bool） | `MTextAcR2000.cs` / `MTextAcR2004.cs` / `MTextAcR2007.cs` / `MTextAcR2018Plus.cs` |
| 版本分派不在类内，全压工厂 | `EntityFactory.cs:122-131`（562 行） |
| 工厂另有 33 个 `Decode` 重载，每个 `switch(target)` 单 case + `throw NotSupportedException` | `EntityFactory.cs:133+` |
| 读侧实体分派是**三元链 31 项**，`default` 落裸 `DwgEntity` | `Reader/DwgObjectReader.EntityDispatch.cs:16-52` |
| 帧/窗口校验集中在 Dispatch（`BodyStart/BodyEnd/HandleStreamStartBit`） | `Reader/DwgObjectReader.Dispatch.cs:40-90,132` |
| 实体目录混入渲染（表格 SVG 导出、HATCH 几何窗） | `TableEntity.Svg.cs`(144)、`HatchGeometry.cs`(264) |

### 1.2 容器与集合层

| 事实 | 证据 |
|---|---|
| `DwgDocument` 单 sealed 类，暴露 `Sections/Objects/Entities/Layers/ObjectRecords/Blocks/Styles`，无 `Tables` | `Format/DwgDocument.cs:7,22-42` |
| 唯一实体实存储 = `WalkResult.Entities`（List，按对象流顺序 Append，非句柄序） | `Reader/DwgObjectReader.Model.cs:21`、`EntityDispatch.cs:246` |
| 块/空间靠 `OwnerHandle` 现算分组 | `Objects/BlockTable.cs:10,29-36,46,75-77` |
| 图层表与块表结构重复（List + byHandle + byName） | `Objects/Records/LayerTable.cs:13-20` |
| **无统一集合抽象**（`IEntityCollection`/`EntitySet` 全仓 0 命中），同类逻辑 10 处各自实现（BlockTable / LayerTable / DwgStyleCatalog / DwgDocument 索引 / RenderContext / FrameRenderer / BlockOwnership / SyntheticDrawingBuilder / SceneModel / EntityHitTest） | 见 `Render/RenderContext.cs:111`、`Rendering/FrameRenderer.cs:99` |
| SeqEnd 不入 `Entities`，顶点聚合靠 owner 字典或顺序扫至 SeqEnd | `EntityDispatch.cs:246`、`Reader/DwgObjectReader.Primitives.cs:215-257` |
| 写侧块头按 `walk.Entities` 重建 owned 向量并排序 | `Writer/DwgObjectStreamWriter.BlockHeader.cs:237-268` |

### 1.3 渲染/设备层

| 事实 | 证据 |
|---|---|
| **设备层契约已存在**：`IDrawDevice` 10 个基元（Begin/EndPass、Line/Rect/Arc/Circle/Polyline/FillPolygon/DrawPath/Text），3 个实现（PNG/SVG/Recording） | `Core/Render/Draw/IDrawDevice.cs:5-26`、`Rendering/PngDrawDevice.cs:7`、`Core/Render/Draw/SvgDrawDevice.cs:7` |
| **实体→绘制有三条并存通道**：①Core `IEntityRenderer` 注册表（15 kind 有实现、6 kind 返回 null）②Rendering 字典 painter ③`SoftwareRasterizer` 自带 `switch(e.Kind)` | `Core/Render/Entities/EntityRenderRegistry.cs:19-46`、`Rendering/FrameRenderer.Painters.cs:29-63`、`Rendering/FrameRenderer.cs:368-385`、`Rendering/SoftwareRasterizer.cs:74+` |
| 设备反向依赖上层：`PngDrawDevice` 调 `FrameRenderer.MakeTransform` 并直持 `PixelBuffer` | `Rendering/PngDrawDevice.cs:32` |
| 属性进入渲染：颜色/线型在 `RenderContext`；**全库无线宽**；可见性只看图层关闭；字高/字体在设备内重解析 | `RenderContext.cs:246-257`、`FrameRenderer.cs:90,117`、`PngDrawDevice.cs:189,214` |
| 依赖方向单向（Rendering→Core），Core 内 0 处引用 Rendering | `DwgParser.Rendering.csproj:26`、`DwgParser.Core.csproj` |

### 1.4 门禁现状

- 解决方案 4 项目（Core/Cli/Rendering/WinForms），**无测试项目**。
- CLI 已有可用作门的面：`--json`（结构快照）、`--svg`（渲染快照）、`--convert`（字节等价）、`--ops`/`--inspect`（写侧）、`--types`（覆盖率），退出码 0/1/2 有语义（`DwgParser.Cli/Program.cs:16-46`）。
- 仓内**无测试 DWG 文件**（`.workbuddy/memory/2026-09-29.md` 记录），门禁需外部 fixture 目录。

## 2. 在途车道与既有半成品（本计划须避让）

| 在途/半成品 | 证据 | 本计划处置 |
|---|---|---|
| **写侧 HATCH 车道在跑**（未提交） | `git status`：`DwgObjectStreamWriter.Hatch.cs`(+19/−5)、`DwgObjectStreamWriter.Carry.cs`(+1) | 写侧（97 分片）整体划为**围栏**，本阶段只加契约不改行为 |
| 渲染迁移已起步：Core 侧 `IEntityRenderer` + 15 kind | `Core/Render/Entities/*`（11 文件） | L5 从既有注册表继续**收敛**，不另起炉灶 |
| 设备层契约已立（`IDrawDevice` ×3 实现） | 见 §1.3 | 只做依赖方向与状态下放，不重定义接口 |
| 版本薄类模式已成（8–9 行开关类） | `MTextAcR2000.cs` 等 | L1 沿用该模式，不推翻 |

## 3. 目标契约（Wave 0 必须冻结）

> **已冻结 2026-11-04（W0）**。落地形态与本文差异见下「冻结偏差」。

1. `IEntityCollection`（`Core/Objects/`）：`Count` / `IEntity? Find(ulong)` / `IReadOnlyList<IEntity> Items`（稳定序=对象流序）/ `IEnumerable<IEntity> OfOwner(ulong)` / `Add` / `Remove` / `Invalidate()`。
   实现 `EntityCollection`：`List<IEntity>` 保序 + `Dictionary<ulong,IEntity>` 句柄索引 + owner 分组**惰性**构建并随变更置脏。
2. `IEntityCodec`（`Core/Objects/Entities/`）：`bool CanDecode(DwgEntityType, DwgVersion)` / `IEntity Create(ulong handle)` / `void Decode(IEntity, ref BitReader, DwgObjectRecord, EntityWindow)`；注册表 `EntityCodecRegistry` 取代三元链与 33 个 `Decode` switch。
3. `IDrawDevice` 补状态面（不新增基元）：`PushState/PopState`（或 `SetStroke`/`SetFill`/`SetText`），使「属性字段 → 设备状态」显式化；`PngDrawDevice` 改为依赖 `IBitmapCanvas` + `IWorldTransform`，**禁止**再引用 `FrameRenderer`。
4. 决策 D1（写侧）：写侧继续 record 驱动；本阶段只留 `IEntityWriter` 门面空壳与 `EntityRecordMap` 只读映射草案，**不改字节**。是否统一为实体驱动，留待 M5 裁定。
5. 决策 D2（属性模型）：`DwgEntity` 上帝类拆为「公共属性（`EntityBase`）+ 各族属性模型」，接口 `Ixxx` 保留并绑定到族类型；对外 API 只增不改。

### 3.1 冻结偏差与语义裁定（2026-11-04，随实现一并落位）

| 项 | 裁定 |
|---|---|
| `IEntityCodec.Decode` 签名 | 计划写 `ref BitReader`；`BitReader` 实为 sealed class，去掉 `ref`（引用传递已可见变更） |
| `EntityWindow` | 新定义于 `IEntityCodec.cs`：`readonly record struct EntityWindow(int BodyStart, int BodyEnd, uint Bitsize)`；版本不进窗口（codec 实例自带版本族） |
| `Create(ulong)` 版本来源 | 注册表以 `(DwgEntityType, DwgVersion)` 经 `CanDecode` 解析，先注册者优先；`Resolve` 返回 null 即「未表态」，L2 侧必须显式炸 |
| 设备状态面选型 | 取 `PushState()`/`PopState()`（弃 SetStroke/SetFill/SetText 方案）；接口默认体为 no-op（W0 零行为变化），语义=变换/裁剪/默认样式栈，L5 填充 |
| `IEntityCollection` 语义 | `Items` 稳定序=对象流序；`OfOwner` 惰性分组、变更置脏；`Remove` 按句柄返回 bool；`Invalidate()` 清全部派生索引 |
| G1 既有门修复 | `sln -warnaserror` 在 W0 前即红：Rendering/CLI TFM `net8.0`→`net8.0-windows`（CA1416）；`ModuleInitializer`→静态构造（CA2255，字体安装语义不变）；`FrameRenderer.Painters` 补 discard 炸臂（CS8524）。均零行为变化 |
| 基线 fixture | `E:\tmp\mgf2_1015.dwg`(AC1015)、`E:\工程\测试.dwg`(AC1027)、`C:\Users\zou_c\Downloads\kindee测试260508.dwg`(AC1024)；`--json`/`--svg`/同版本 `--convert` 全 rc=0，转换产物与源**逐字节相同**（G4 基线成立）；清单 `out/base/manifest.json`（含 SHA256），`out/` 已入 .gitignore |

### 3.2 W2 冻结偏差与语义裁定（2026-11-05，随实现一并落位）

| 项 | 裁定 |
|---|---|
| 注册表键 | `CanDecode(DwgEntityType, DwgVersion)` → **short typeCode**：3dFace 与 Solid 实体 `Kind` 同为 `DwgEntityType.Solid`（FaceAcR2000Plus 自设），DIMENSION 7 个子类型码共用一个 Kind——DwgEntityType 键无法分辨，short 码才是 DWG 原生身份 |
| `Create` 签名 | `Create(ulong)` → `Create(ulong handle, short typeCode, DwgVersion version)`：版本旗标（Attribute 4 bool / Viewport 3 bool / Hatch 2 bool 等）与 DIMENSION 子类选择、Solid/3dFace 分型都需要 |
| `Decode` 返回值 | `void` → `bool`：Insert 的 hasAttribs 返回值（record.HasAttribs 写侧依赖），其余恒 true |
| `Decode` record 参数 | `DwgObjectRecord` → `DwgObjectRecord?`：dispatch 侧 record 本就可空 |
| `EntityWindow` | 增补 `DwgVersion Version, ulong Handle`：Table/Dimension/MLine/Tolerance/Block 的 ReadBody 需要 version；MLine/Tolerance/Block 需要**原始** handle 参数（非 OwnHandle 覆写后的）；BodyStart=objStart、BodyEnd=objEnd、Bitsize=ced 调整后 |
| Resolve null 语义 | 显式 `NotSupportedException`：Dispatch 外层 switch 已枚举同一实体码集才进 DecodeEntity，三元链 default 臂（裸 DwgEntity）为死路径，无现实行为变化 |
| `IDrawDevice` | 新增基元 `DrawDot(Vec2, int radiusPx, FillStyle)`：Point 标记是**固定 3 像素**半径（无世界半径表达），PNG→FillCircle、SVG→`<circle>`、Recording→log；三实现显式落，无默认体 |
| `RasterTextSpec` | 增补 `SkipVerticalShx = false`：painter 的 Table/MLeader 文字路径无竖排 SHX 分支，W4 切换时保真（Text 既有路径不受影响，默认 false） |
| 渲染注册表拆分 | `For()` 拆 `ForSvg()`（冻结现 22 类 device-SVG 集）+ `For()`（= ForSvg ∪ 5 新 renderer）：SVG 通道 Point/Ellipse/Hatch/Table/MLeader 继续**实体自绘 WriteSvg**（G3 字节不变，实体自绘即显式表态）；光栅通道经 RasterReady（+5 类）进 Core renderer |
| Insert 表态 | 归入结构性 null 组（块内容由走查渲染、自身无位图绘制，光栅管线从未到达 Insert）——注册表内不再有「未表态」null |
| 工厂 | 33 个 `Decode` 重载删除；`Create*` 保留（codec 调用 + FrameRenderer.CreateTable / Geometry.CreateEntityHandleStream 外部在用） |
| L1b 共用字段 | `Rotation`/`Height`/`WidthFactor` 被椭圆/插入/标注兜底多族共用，**不**入单族模型，留 W3 裁定归属 |

**W2 验证记录**：3 fixture JSON/SVG/convert/PNG 四门与基线全等；`OdWriteEx XRef.dwg`（2 Ellipse）
pre/post（git worktree 双构建）PNG/PNG-L/SVG 逐字节等；仓后台架 `E:\tmp\dwgp-ab`（程序集名
`DwgParser.Tests` 吃两边 InternalsVisibleTo，反射调私有 painter）8/8 合成实体像素等价。
**缺口**：Table/MLeader 无真实 fixture，等价性仅由合成实体证明——真实 fixture 到位后、W4 切换前补跑。

## 4. 车道划分（文件占用即所有权）


| 车道 | 独占路径 | 交付物 | 依赖 | 验收 |
|---|---|---|---|---|
| **L0 契约** | `Core/Objects/IEntityCollection.cs`、`Core/Objects/Entities/IEntityCodec.cs`、`Core/Render/Draw/`（仅加状态签名）、`docs/entity-refactor-plan.md` | 3 契约 + D1/D2 记录 + 基线快照 | — | G1 + 基线存档 |
| **L1 实体模型** | `Core/Objects/DwgEntity.cs`、`Core/Objects/Entities/*`（除 `EntityFactory*.cs`、`EntityDispatch`） | `EntityBase` + 各族属性模型；`TableEntity.Svg.cs` 迁出到 `Core/Render/` | L0 | G2/G3 快照不变、G5 |
| **L2 读侧编解码** | `Core/Objects/Entities/EntityFactory*.cs`、`Core/Objects/Reader/DwgObjectReader.EntityDispatch.cs` | 注册表替换三元链与 33 个 switch；未注册类型显式 `NotSupported` | L0 + L1 接口 | G2 不变、`--types` 分布不变 |
| **L3 写侧（冻结）** | `Core/Objects/Writer/**` | 仅 `IEntityWriter` 空壳 + 文档化契约 | L0 | G4 字节不变 |
| **L4 集合层** | `Format/DwgDocument.cs`、`Objects/BlockTable.cs`、`Objects/Records/LayerTable.cs`、`Reader/DwgObjectReader.Model.cs`、`Reader/DwgObjectReader.BlockRegion.cs` | `EntityCollection` 接入；旧属性改适配器（行为不变） | L0 | G2 不变、G6 |
| **L5 渲染收敛** | `Core/Render/Entities/**`、`Rendering/FrameRenderer*.cs`、`SoftwareRasterizer.cs`、`PngDrawDevice.cs` | 三路合一路；补齐 null 的 6 类；解除反向依赖 | L0；L1 的族属性就绪后补 | G3 不变、覆盖率 100% 表态 |
| **L6 门禁** | `tools/gates/*.ps1`（新增） | G1–G6 可执行门 + BLOCKED 语义 | L0 | 自测红/绿各一次 |

> 围栏：`Writer/**`（L3 在途）、`Reader/DwgObjectReader.Dispatch.cs`（帧窗口，另车道）、
> `Compression/**`、`Format/*Writer*.cs` —— 本计划一律不动。

## 5. 波次

| 波 | 并行度 | 内容 | 出口 |
|---|---|---|---|
| W0 | 1 手（串行） | L0 契约冻结 + 基线快照（`--json`/`--svg`/`--convert` 存 `out/base/`） | 3 接口编译通过，基线可比 |
| W1 | 3 手 | L1a（曲线/几何族）+ L4 + L5a（三路合一，只做分派不改绘制） | 集合层可用、单一分派 ✅ 2026-11-05（串行执行：ae77205/6532157/bf853b1） |
| W2 | 3 手 | L1b（文字/表格/标注族）+ L2（注册表）+ L5b（补 6 类 renderer） | 三元链消失、覆盖 100% ✅ 2026-11-06（0416a47/55f2ce9/9c46f2f） |
| W3 | 2 手 | L1c（剩余族 + `DwgEntity` 瘦身）+ L6（门禁铺开） | `DwgEntity` 只剩公共属性 |
| W4 | 1 手 | 死路径删除（旧 painter 表、`SoftwareRasterizer` switch）、`docs/project-progress.md` 收口 | 门全绿 / 或明确 BLOCKED 清单 |

## 6. 验收门（可执行）

- **G1 构建**：`dotnet build DwgParser.sln -warnaserror` → 0 error 0 warning。
- **G2 结构快照**：`dwg <f> --json out/base/<stem>.json`，与基线 diff = 0（允许**新增**键，禁止改值/删键）。
- **G3 渲染快照**：`dwg <f> --svg out/base/<stem>.svg`，与基线 diff = 0；PNG 走 `RenderExtensions.Raster` 哈希比对。
- **G4 字节等价**：`dwg <f> --convert <同版本> --output out/x.dwg`，与原文件/基线产物逐字节相同。
- **G5 行数**：全仓 `*.cs` ≤ 800 行（`SourceFileSizeTests` 不存在，先用脚本门）。
- **G6 依赖方向**：`Core` 内 `grep -r "using DwgParser.Rendering"` = 0；`Rendering` 内不得出现 `FrameRenderer` 反向被设备层引用。
- **BLOCKED 语义**：无 fixture 时 G2/G3/G4 报 `BLOCKED`，**不得**报绿。

## 7. 风险与回退

| 风险 | 触发点 | 缓解 |
|---|---|---|
| 图上静默少画 | L5 收敛三路分派 | 旧 painter 表保留到 W4；未表态 kind 立即 `throw`（沿用现有护栏） |
| JSON 门对字段改名敏感 | L1 拆上帝类 | 字段只增不改名（不变量 5）；先立门再改 |
| 与写侧在途车道冲突 | L1 改 `DwgEntity` 时写侧仍读 record | 本阶段写侧零改动；需要跨道改名时先合并 HATCH 车道 |
| 拆族把文件顶过 800 行 | `TableAcR2010Plus`(336)、`HatchEntity`(443) | 按「编解码 / 属性 / 渲染提交」三段分片 |
| 门禁空转 | 无 DWG fixture | BLOCKED 语义 + W0 先把 fixture 目录写进门脚本参数 |

## 8. 里程碑

- **M0**（W0 末）契约冻结、基线存档 ✅ 2026-11-04（三契约编译通过、G1 绿、基线 3 fixture 逐字节可比）
- **M1**（W1 末）`EntityCollection` 上线，块/空间/图层视图全走它 ✅ 2026-11-05（块/空间走它；图层表元素类型 ILayer≠IEntity 未接入，遗留 W2）
- **M2**（W2 末）读侧注册表替换完成，新增实体类型只碰一处 ✅ 2026-11-06（0416a47）
- **M3**（W2 末）渲染单路 + 全 kind 表态（W1 已完成分派合一：②③ 均经 `EntityKindRegistry.RasterKindOf` 单一决议；W2 ① Core 注册表全 kind 表态达成 55f2ce9——5 类补齐 + Insert 结构性表态；**渲染单路切换**＝RenderEntity 的 painter 回退删除，留 W4，届时旧 painter 表一并删）
- **M4**（W3 末）`DwgEntity` 只剩公共属性，族模型独立（W2 L1b 已落文字/标注/表格三族 9c46f2f；Rotation/Height/WidthFactor 共用字段归属待裁定）
- **M5**（W4 末）写侧策略（D1）裁定 + 文档收口
