---
name: dwg-parser-map
name_en: DwgParser Project Map
name_zh: DwgParser项目结构地图
description: DwgParser project structure map - module responsibilities, key files, the parse data flow, entry APIs and conventions. Load before locating or modifying code in the repo. Also carries the mandatory build/test/gate command wrappers and the "no third-party deps" / "no throw on parse failure" project rules. Also carries the gate exit-code ladder (rc 1 = judged red, rc 2/3/4 = the gate did not run at all), the ODA singleton scope = one instance per install dir (so a linked worktree is the legal second lane), progress_lock LIVE/STALE judged by ts/ttl rather than its transient pid, plus two command-shape traps (`dotnet <X>.exe` => rc 140; probe .cs line endings before byte-level patching).
description_en: DwgParser structure map (module duties, key files, parse data flow, entry APIs, conventions); load before locating or editing code. Also carries the gate exit-code tiers, the ODA per-install-dir singleton concurrency rule, and build/gate command-shape traps.
description_zh: DwgParser 项目结构地图——模块职责、关键文件、解析数据流、入口 API 与约定；在仓库中定位或修改代码前先加载。另含门退出码档位（`1` = 判红，`2/3/4` = 门根本没跑成）、ODA 单例闸口径 = 每安装目录一实例（linked worktree 自带第二份安装目录即合法并发形态）、progress_lock 只按 ts/ttl 判 LIVE/STALE（其 pid 属瞬时进程），以及两条命令形态坑（`dotnet <X>.exe` ⇒ rc=140、字节级补丁前先探 .cs 行尾）。
argument-hint: 无需参数；在仓库中定位或改代码前用
argument-hint-en: No argument; use before locating or editing code in the repo
argument-hint-zh: 无需参数；定位代码或改代码前加载
user-invocable: true
---

# DwgParser 项目结构地图

> 目标：让模型用最小成本定位代码。
> **本技能的规模读数会随体重增长而腐化——引用前一律现场 `find`/`wc` 复核，或以下述指针为准。**
> **权威读数落点**：构建/测试/门的合法通路与即时读数 = `AGENTS.md` §1（硬约束 9）+ 根 `README.md`
> + `docs/handbook/progress-and-fixtures.md`；本技能只给**结构导航**，不给「当前进度」。
> 现场实测（2026-09-25，仅供参考，会变）：`Core` **571 个 .cs / 86,209 行** ·
> `DwgParser.Rendering` 19 / 5,598 · `Cli` 13 / 2,380；测试 `tests/DwgParser.Tests/` **331 个 .cs**。
> 类族按「类型 × 版本」双轴铺展：`Objects/Records` 226 文件、`Objects/Entities` 108 文件，
> 其中版本具体类 `*Ac<档>*` 51 枚、跨版本迁移边 `*Carry*` 48 枚。
> 两个枢纽类靠 `partial` 拼装：`Objects/Writer/DwgObjectStreamWriter` **91 分片 / ~23,548 行**、
> `Objects/Reader/DwgObjectReader` **37 分片 / ~6,287 行**——**改它们 = 撞最大热文件面
> （并发冲突高发），动前先查占用（`tools/progress_lock.py status` / `ls -l` 查 mtime）**。
> 其余 `bin/`、`obj/`、`docs-ref/`（只读参考）以及 `out/` 下整树备份（git 已忽略、
> 但会污染文件级 `find`/glob）——搜索时一律排除。

## 1. 项目本质

从零实现的 C# DWG 二进制解析库（.NET 8，**零第三方源码**——不含也不衍生自任何第三方代码；`Core` 只用 BCL，仅 `DwgParser.Rendering` 受控引入 `System.Drawing.Common` + `SixLabors.Fonts` 两个托管包，测试用 xUnit）。
命名空间根 `DwgParser.Core.*`。中文注释与输出为主。格式知识来自公开文档与逆向标定，
参考 `docs-ref/`（只读）；自有格式笔记在根 `README.md` 附录 A。

## 2. 目录地图

| 路径 | 内容 | 性质 |
|---|---|---|
| `src/DwgParser.Core/` | 全部解析逻辑 | 核心（改这里） |
| `src/DwgParser.Cli/Program.cs` | 命令行工具 `dwginfo` | 入口 |
| `tests/DwgParser.Tests/` | xUnit 测试，真实样本在 `TestData/` | 测试 |
| `tools/ProbeTool/` | 调试用探测工具（引用 Core；不在 sln 中） | 工具 |
| `tools/RenderAll/` | 批量渲染 `图纸模版/*.dwg` → `渲染对比/*.png`（不在 sln 中）。`tools/RenderDemo/` 已改成单图 dimension dump，**非**批量渲染器 | 工具 |
| `tools/oda-compare/` | 闭源 ODAFileConverter 外部有效性对照（`verify.py` 为主入口；`entity_truth_gate.py`+`teigha_truth_pipeline.py` 为 Teigha 逐对象真值比对门） | 工具 |
| `tools/R2007Dump/`、`tools/VsProbe/`、`tools/DimProbe/` | AC1021 完整性值复现 / VISUALSTYLE 位级解剖 / DIMENSION 取证（均不在 sln） | 工具 |
| `tools/TeighaDump/` | 用本机 `Teigha/`(ODA 4.02.11 x64 SWIG 绑定) 逐对象导出字段真值 JSON → `out/teigha-truth/`（不在 sln，Core 零依赖不破） | 工具 |
| `Teigha/` | ODA 4.02.11 x64 SDK 本体（`TD_*.dll/.tx`、`TD_Swig*Mgd.dll`）；调用范式参考 `D:\Project\DrawLib\TeighaHelper.cs` | 只读依赖 |
| `README.md` | 项目现状与内容权威（附录 A 格式硬事实 / B ODA 对照 / C Teigha 真值对标链）；导航与硬约束见根 `AGENTS.md`，详档在 `docs/`（`docs/README.md` 为总目录） | 文档 |
| `AGENTS.md` | **根文档**：硬约束 10 条 + 路由表（想知道什么看哪一档） | 文档（权威） |
| `docs/` | 四档 handbook（progress-and-debts / tools-and-fixtures / pitfalls / faq）+ 台账 + `docs/issues/` + `_archive/` | 文档 |
| `docs-ref/` | libredwg 规格 / ACadSharp 参考源码 | **只读参考，勿改勿读入上下文** |
| `最简各版本图纸/` | 同一极简图（单条直线）的 2000/2004/2007/2010/2013/2018 六版本基准（原 `r14.dwg` 随 AC1014 于 2026-09-17 移出支持删除） | 样本数据 |
| `图纸模版/`、根目录 `*.dwg` | 真实样本图纸（AC1015/AC1018 等） | 样本数据 |
| `out/`、`bin/`、`obj/` | 输出与构建产物 | 忽略 |

## 3. 解析数据流（一条主链）

```
DWG 文件字节
  → FileHeader（0x80 字节文件头：版本签名/维护版本/代码页/预览地址/系统区指针）
  → 按版本分派（DwgFile.Load 工厂 → 各容器自身的 DiscoverSections）：
      ├─ R2000（AC1015，受支持版本下限）→ R2000File：节定位记录 + CLASSES 位编码类表 + HANDLES→(句柄,绝对偏移) 映射
      ├─ R2004（AC1018）→ R2004File：解密 0x80 处 120 字节 XOR 流密码头
      │                     → 节页目录(0x41630E3B) + 节描述符(0x4163003B) → RS 解压各命名节
      │                     （底层页装配在 R2004Container）
      ├─ R2007（AC1021）→ R2007File + R2007Container：独立容器——0x80 处 RS(255,239) 列交织头
      │                     + LZ77-AC21 解压的 0x110 节头部；数据页自 0x480 起、无页头
      │                     （复用 R2004 对象读取 + 独立字符串流 AttachStringStream）
      └─ R2010/2013/2018（AC1024/27/32）→ R2010File/R2013File/R2018File（继承 R2004File）：
                            对象头改为 MS+UMC+BOT、末尾分离句柄流；R2013 增 has_ds_data
  → 节数据（AcDb:Header / Classes / Handles / AcDbObjects…）存入 DwgSection
  → 按需 GetObjects()：
      R2000: DwgObjectReader.ReadObjectsR2000(绝对偏移)
      R2004+: DecompressSection("AcDb:Handles"|"AcDb:AcDbObjects") → ReadObjects
  → WalkResult{ObjectsSeen, ObjectsDecoded, Entities: List<DwgEntity>, Records,
               BlockRegions, LayerColors/LayerNames, ModelSpaceHandle, PaperSpaceHandle}
  → SvgRenderer.Render(objs)（INSERT 递归展开 ≤16 层、矩阵变换、Y 翻转、ACI 颜色）
```

## 4. 模块明细（src/DwgParser.Core）

### 根
- `DwgVersion.cs` — `enum DwgVersion`（AC1006…AC1032；AC1006–AC1014 只用于识别签名）+ 扩展：
  `MinimumSupportedVersion` = AC1015 与 `IsSupportedVersion()`（**受支持版本下限的唯一判据落点**，
  2026-09-17 移除 AC1012/AC1014 时引入：低于此档在 `DwgFile` 落 `ScannedDwgFile` 结构扫描、
  `DwgConverter.Save` 对目标与源双向抛 `ArgumentException`）、`IsR2000()`(==AC1015，原
  `IsR13ThroughR2000()` 已删)、`IsR2004Plus()`(≥AC1018)、`IsR2007Plus()`、
  `IsR2010Plus()`(≥AC1024，BOT 编码/UMC 句柄流)、`GetVersionName()`。
- `Crc.cs` — `Crc.Compute32`（IEEE CRC-32，校验 "123456789"=0xCBF43926）、`Compute8`、
  `Compute16` / `Compute16Dwg`（CRC-16 反射 0x8408 / 0xA001，种子 0xC0C1；DWG 头部/页/对象 CRC 用 0xA001）。

### `Format/`（容器与文件结构 + 写出）
- `FileHeader.cs` — `FileHeader(byte[])`；`Version`/`VersionString`/`MaintenanceRelease`/`CodepageGuess`/`SystemAreaAddress`/`PreviewAddress`/`LocatorMarker`。
- `DwgSection.cs` — 节数据模型（Name/DataOffset/CompressedSize/DecompressedSize/IsCompressed/Data）。
- `IDwgFile.cs` / `BaseDwgFile.cs` / `DwgFile.cs` — 文件抽象接口、公共基类、`DwgFile.Load` 版本分派工厂。
- `R2000File.cs` — `TryLoad(raw, out R2000File)`；`Records`（节定位）、`Classes`、`Handles`(List<ulong>)、`ObjectOffsets`(List<int>)、`FindSection(name)`、`ResolveTypeName(type)`、`Diagnostics`、`BuildSecondHeader`（R13–R2000 文件尾 2ndheader）。
- `R2004Container.cs` — 底层容器：`DecryptHeaderBlock`、`ParseSecondHeader`、`TryLoad(...)`、`TryGetPageAddress`、`TryReadSection`；类型 `R2004Page`/`R2004Section`/`MapSection`/`SecondHeader`。
- `R2004File.cs` — 高层容器（**DwgDocument 实际使用**，R2007–R2018File 继承之）：`TryLoad`、`DecompressSection(name)`、`ParseHandlesSection`；跨版本转换构造 `BuildFromR2000/BuildFromR2004`（含 `ReencodeHeaderSection` 经 `HeaderVarsCodec` 真重编码，替代旧 no-op `PatchHeaderVersion`）。
- `R2007Container.cs` / `R2007File.cs` / `R2007Writer.cs` — **AC1021 独立容器**（0x80 RS(255,239) 列交织头 → 32B 校验块 → `Lz77Ac21` 解压 → 0x110 节头部；数据页自 0x480 起、无页头）。`R2007Writer.TryRebuildFromIndex` 支持**同版本**容器重建（页索引驱动搬运式，recopy/convert 同版本 ODA 可读）；跨版本写出仍复用 R2004 家族结构且已判定不可行（无 LZ77-AC21/CRC64/RS 交织编码器，见 README §9 条 4）。
- `R2010File.cs` / `R2013File.cs` / `R2018File.cs` — AC1024/27/32，继承 R2004File，仅版本签名/对象头差异。
- `HeaderVarsCodec.cs` / `HvFieldTable.generated.cs` — R2004+ 头部节变量 Decode→Encode→Decode 重编码（C# 移植 hdrvars.py）；字段表生成码。
- `R2000Writer.cs` / `R2004Writer.cs` — 容器级写出：`R2000Writer.Build`（含真哨兵/定位记录/2ndheader）、`R2004Writer.Prepare/Build`（页化、加密头、SEED/CRC，动态重算 PageCapacity/节偏移）。
- `DwgConverter.cs` — 跨版本转换入口：`Load(path)` → `Save(f, targetVersion, outPath)`；同版本原样写回。
- `DwgDocument.cs` — **总入口**，见 §5。
- `DwgStyleCatalog.cs` — 样式聚合视图（九族，全按句柄索引）：`Text`/`Dimension`/`Linetype` 直接取 `WalkResult` 的三本字典；`Visual`/`TableStyle`/`MLeaderStyle`/`MLineStyle`/`ViewStyle`/`ViewStyleTemplate` 按 `record.Xxx` 收拢**存活捕获袋**（范式同 `LayerTable`：捕获袋就地实现 `IXxx`，不造影子模型）；`All` = 九族并集（按句柄升序去重）。由 `DwgDocument.Styles` 惰性构建，只读投影、不做解码、不影响产物字节。**只反映「读取器已具体化」的样式**——具体化由 `ObjectFactory.CreateXxx` 按源版本门控，故有已知空洞（AC1015 源的 507、R2010+ 源的 510/511、pre-2000 源的五族），判存在性一律以记录 + `ClassTypeCodes` 为准。
- `BlockTable.cs` — 块表图级视图（句柄/名字双向查 + `FindAll` 取重名匿名块 + `ModelSpace`/`PaperSpace` + `EntitiesOf(块头句柄)` = 块内容），由 `WalkResult.Blocks`（惰性、缓存一份）与 `DwgDocument.Blocks` 取得；只读投影 `BlockRegions` + 实体的 `OwnerHandle` 分组（与渲染层 `FrameRenderer.BuildBlockContents` 同一口径，但**不含**「owner 不指向块头时归模型空间」那类渲染策略兜底）。`WalkResult.Blocks` 的声明落在 `Objects/Reader/DwgObjectReader.BlockRegion.cs` 分片（`WalkResult` 已改 `partial`，因主分片贴近 800 行上限）。

### `Compression/`（R2004+ RS 解压族）
- `RsDecoder.cs` — `TryDecompress`（严格：精确填满，用于节页目录）、`TryDecompressSection`（停 0x11 终止符，用于节数据）、`DecodeLenient`（探测用）。
- `RsEncoder.cs` — 写出侧 `Store`（不压缩 store 格式，压缩率≈1）。
- `Lz77Ac21.cs` — AC1021 的 LZ77 变体解压器（逐 token 移植 decode_r2007.c）。
- `R2004Checksum.cs` — R2004+ 校验和辅助。
- `LiteralScanner.cs` — `Scan(data)` 在压缩流中挖字面量名。`ChunkLocator.cs` — `FindSystemChunks` 暴力探测压缩块。

### `IO/`（位/字节读取）
- `ByteReader.cs` — 小端流式读取：UInt16/32/64、Double、CString、LengthPrefixedString（**长度含 NUL 终止符**）、UnicodeString、Skip/Seek。
- `BitReader.cs` — MSB 位流：`ReadBit/ReadBits/AlignByte` 及 DWG 类型 B/BB/RC/RS/RS_BE/RL/RD/BS/BOT/BL/BD/DD/BT/MC/UMC/MS/TV/HANDLE；`ReadTV()` 严格 UTF-8→GBK→Latin-1 回退；R2007+ `UnicodeStrings`（TU，BS 长度按字符数计）+ 独立 `StringStream` 挂接；`DwgHandleReference(code,value)`+`Resolve(ownerHandle)`。
- `BitWriter.cs` — `BitReader` 的**逐方法真逆**（MSB-first、raw 类型按位写、不做字节对齐）；新增位类型必须两侧同步 + `BitWriterTests` 往返验证。

### `Objects/`（对象与实体解码 + 跨版本重写）

> **子目录口径（2026-09-17 起）**：`Reader/` = `DwgObjectReader*`（2026-09-25 实测 37 片）· `Writer/` =
> `DwgObjectStreamWriter*`（实测 91 片）· `Entities/` · `Records/`；根只留跨版本模型与工厂。
> **分片数会随体重增长——动手前现场 `find … -name 'DwgObject*Writer*.cs' | wc -l` 复核。**
> 移动 = 纯路径搬运（零内容行），但 `PerFileTypeCodeGateTests` 的 Allowed 键与
> `docs/direction-a-*.md` 的路径锚按相对路径索引，搬家须同轮改锚。

- `Reader/DwgObjectReader.cs` — `ReadObjects`（R2004+）、`ReadObjectsR2000`（R2000）、`ReadObjectsWithPairs`；`WalkResult`（ObjectsSeen/ObjectsDecoded/Errors/Entities/Records/BlockRegions/LayerColors/LayerNames/ModelSpaceHandle/PaperSpaceHandle/SourceData）；内部 `DecodeObject`/`DecodeEntity`（版本感知公共实体数据；pre2000 分支随两档移出支持删除）、`DecodeLayer`（R2004+ 按 libredwg common_object_handle_data + COMMON_TABLE_FLAGS 精确解码，R2007+ 挂字符串流）、`AttachStringStream`。
- `DwgEntity.cs` — `enum DwgEntityType`、`Vec2`、`DwgEntity`（Handle/ColorIndex/EntMode/OwnerHandle/LayerHandle/几何/TextValue/Points/Bulges/BlockHeaderHandle/Corners）。
- `IEntity.cs` — 版本无关实体视图接口。
- `Objects/Entities/` — **实体侧逐版本类族**（范式首例）：`ILine`/`ITable`/`ILwPolyline`/`IText`/`IMText`(`: IEntity`
  语义接口) + `LineEntity`/`TableEntity`/`LwPolylineEntity`/`TextEntity`/`MTextEntity`(`: DwgEntity, IXxx` 公共基类，
  `abstract ReadGeometry/ReadBody`；MTEXT 变体 = 共享骨架 + 四枚形态钩子、五档版本具体类)
  + `internal sealed XxxAc<版本档>`（版本差异只在这里）
  + `EntityFactory.CreateXxx/Decode(IXxx, …)`。消费链（渲染/JSON/句柄流）仍按 `DwgEntity` 走 ⇒ 零改动。
- `Objects/Records/` + `Objects/ObjectFactory.cs` — **对象侧逐版本类族**（DIMSTYLE 首例、VISUALSTYLE 并轨、
  STYLE 第三族：`IStyle` + `StyleRecord`（帧式/xdic/ds/xref 形/提锚六钩子）+ 7 档 `StyleAc*`
  + `StyleCapture`（`rec.Style`，取代 9 枚散落 `Style*`）+ `StyleCarryPlan`/`StyleCarryPreR2007Up`、
  LTYPE 第四族：`ILtype`+`LtypeRecord`+两档 `LtypeAc*`（余档工厂出 null）+ `LtypeCapture`
  （`rec.LType`）+ `LtypeCarryPlan`/`LtypeCarryPreR2007Up`，§24 裁垫门同读 `rec.LType`）：
  `IRecord`(Handle/TypeCode/SourceVersion) → 逐类别 `IXxx : IRecord` → public 基类 `XxxRecord`
  （含 `Family` 诊断档名）→ `internal XxxAc*` 版本具体类。**对象侧唯一工厂 = `Objects/ObjectFactory.cs`**
  （勿另立同名工厂）：读侧 `CreateXxx + Decode(IXxx, br)`；写侧按「**源档→目标档**」出计划类
  （DIMSTYLE 四格：`DimStyleCarryAc1018{ToAc1021,ToR2010}` / `…Ac1021Up` / `…R2010ToAc1021`，
  选型条件必须与迁移前窄门**逐格等价**，多一格就可能打断已绿格子）。
  跨阶段存活的捕获态收在类别自带模型（`DimStyleCapture` 挂在 `DwgObjectRecord.DimStyle`，
  取代原先 13 枚散落 `DimStyle*` 字段），读侧版本具体类按停点强判据填、写侧计划类消费。
- `ObjectScanner.cs` — `ScanClasses(data)` → `ClassOccurrence`（AcDb*/Acad* 频次统计）。
- `DwgObjectRecord.cs` — 版本无关对象记录：类型码、own handle、位区间（几何/搬运/句柄流/EED/preview/CMC/字符串流起点）、**P0-2 起捕获全部公共字段与句柄槽位**（NumReactors/NoLinks/IsXdicMissing/HasDsData/LtypeFlags/PlotStyleFlags/MaterialFlags/ShadowFlags/OwnerRef/ReactorRefs/XdicRef/Prev·NextRef/LayerRef/LtypeRef/…）。
- `DwgObjectStreamWriter.cs` — `Build(walk, targetVersion)` → 新对象流字节 + (句柄,偏移) 映射；`WriteCommonEntityData`/`WriteEntityHandleStream` 走捕获值（`WriteCapturedHandle`）；`CanReencode` 门控（跨 R2000b 边界整体搬运、pre2000 重编码未实现、type=4 跨 R2007 降级退回搬运）；类实例专项：`TryRewriteStringStream`（降级串流内联）、`TryCarryVisualStyleR2013b`（VISUALSTYLE 跨 R_2013b 块插/剥）、`ReencodeVisualStyle`（R2000→R2004 CMC 升级）、MLINESTYLE 跨 R2018 变换、伪实体（BLOCK/ENDBLK/SEQEND）实体感知搬运。
  **三条改写出器必知的硬规则（2026-09-05 晚实证）**：① 任何**新的专用搬运分支**（`TryCarry*`）
  从 `CarryStartBit` 拷公共头时必须经 `CopyCommonHead`，否则跨 R_2013 界的 `has_ds_data` 位不同步
  （R2018 源→AC1024 目标实测多 1 位 → ODA `Object improperly read: <AcDbBlockTable> (1)`）；
  ② **R2004 家族源（`PrefixHasRl`＝AC1015–AC1021）→ R2010+ 目标**若 `!rec.HasHandleStream`
  （未解码 Opaque）必须 `hs = BodyStart*8 + 源RL` 反推「数据区/句柄流」分界并把它写进 UMC，
  否则 UMC=0（实测 181/191 对象如此 → `Unexpected end of file`）；
  ③ 写**对象集中字符串流**（R2007+ 目标）必须用 `WriteCentralTu`（`BS 字符数 + len×16`，**无 NUL**），
  勿用 `WriteTV(v, unicode:true)`（会补 `0x0000` 尾单元、每串多 16 位；真本实测 219 位/data_size 202/
  码元数 12）。门：`R2018DowngradeToR2010Tests`、`R2004ToR2010HandleStreamSplitTests`；
  取证：`docs/direction-a-ac1032-downgrade.md` §19–21、硬事实 README §A.7、
  探针 `tools/BhProbe --ss <hdl>`（串区口径）与 `tools/DirAVal --capstr <dwg>`（内联串捕获缺口盘点）。
  ④ R2004 家族源→R2007+ 的内联 TV 集中化走 `CentralizeInlineStrings` + `CopyDataRegionCentralizing`
  （搬运分支两处数据区末段拷贝）；capture-only 解码统一用「本地列表＋停点校验 `BodyStart*8+RL` 后提交」
  模式（DICTIONARY 已此形，猜错零副作用）。

### `Strings/`
- `StringExtractor.cs` — `Extract(data, baseOffset, fromDecompressed)`、`ClassifyValue(v)` → `StringKind`（ClassName/FontRef/Path/SymbolTable/Text）。

### `Core/Render/`（矢量与纯数据，只用 BCL）
- `SvgRenderer.cs` — `Render(WalkResult)` → SVG 字符串；`RenderContext`（含 `StyleFonts`/`ResolveFontFamily(styleHandle, text)`）/`Matrix`。
- `FontResolver.cs` — `Resolve(TextStyleInfo?, text)` → 字体族（TTF 名/别名匹配、SHX→TTF 映射 gbcbig/hzfs→仿宋、hztxt→宋体、latin shx→Arial；CJK 文本强制 CJK 字体）；`ResolveRunFamily(fontSpec, fallback)` 处理 MTEXT 内嵌字体名。**本文件不再引用 System.Drawing**。（`TextStyleInfo` 自 2026-09-17 起实现 `IStyle`，可当对象侧样式接口直接消费；其 `Handle`/`SourceVersion` 由 `StyleRecord` 构造时回填。）
- `FontCatalog.cs` — 已装字体族清单接缝（`Register(Func<IReadOnlyList<string>>)`/`Families`/`IsInstalled`）：Core 侧唯一让 `FontResolver` 判「装没装」的入口，未登记 = 空清单 = 拆分前无 GDI 的回退形。提供者由渲染工程 `RenderingRuntime.EnsureSystemFonts()` 登记（模块初始化器自动 + Cli 启动显式调用）。
- `MTextParser.cs` — `Parse(raw)` → List<Line>（行→Run 列表）；Run 含 FontSpec/Bold/Italic/下划线/上划线/删除线/HeightScale(相对 x)/AbsoluteHeight/WidthFactor/Oblique/AciColor；支持 `\f \H \C \W \Q \S \L \O \K {}`、`\U+XXXX;`、`%%d %%p %%c`。
- `DimensionRenderer.cs` — DIMENSION 合成渲染（PNG/SVG 共用）：从定义点+实测值重建尺寸线/界线/实心箭头/文字。**块 *D 是陈旧缓存不可用**；dim_rotation 字段实测不可靠（NaN/垃圾角）→ 测量方向由 |dot(x1-x2,dir)|=实测值 反解（尺寸线平行于测量方向）；`<>` 替换实测值（0.###），R/Ø 前缀。
- `AciPalette.cs` / `BulgeSampler.cs`（internal，经 `InternalsVisibleTo DwgParser.Rendering` 供位图侧复用）/ `RenderExtensions.cs`（**只剩 SVG 半**：`RenderToSvg`/`RenderToSvgFile`）。
- **矢量为什么留在 Core**：`IEntity.ToSvg(RenderContext, Matrix)` 由约 20 个实体类各自实现，搬走等于重写实体层。

### `src/DwgParser.Rendering/`（位图栈，唯一 `System.Drawing.Common` 消费者，命名空间 `DwgParser.Rendering`）
- `FrameRenderer.cs` + `SoftwareRasterizer.cs` + `PixelBuffer.cs` + `PngEncoder.cs` + `RenderExtensions.Raster.cs`（`RenderToPng`/`RenderToPngBytes`/`RenderToPngByFrame`/`RenderToPngByFrameBytes`/`DetectFrame`）— PNG 光栅化管线。FrameRenderer 持 `_walk`（WalkResult），文字 case 做样式句柄→字体解析、白底白字翻黑、MTEXT attachment 对齐、内嵌格式检测分流。
- `GdiTextRenderer.cs` — public static：`DrawText` 全参重载（fontFamily/widthFactor/hAlign/vAlign/lineGapFactor，3-on-5 行距）；`DrawMTextRuns` 分段排版渲染（每段字体/字号/颜色/粗斜体，Shear 倾斜，`\H` 绝对字高经 `WorldToPxScale`）。GDI LockBits 直写绕过 SetPixel → 用绘制前后缓冲差分标记 PixelBuffer.Coverage。
- `LayeredFrameRenderer.cs` — 按图层分层渲染+逐层缓存：每层独立 PixelBuffer+Coverage 掩码；实体内容指纹（FNV-1a 64）比对，**修改后仅重绘变化图层**；`Invalidate(handle)`/`Composite(visible)` 图层显隐；视图变化全量失效。
- `RenderingRuntime.cs` — `EnsureSystemFonts()`（幂等，`Interlocked` 守住只登记一次）+ `[ModuleInitializer]`：把 GDI 已装字体清单登记进 Core 的 `FontCatalog`。还原清单请直接 `FontCatalog.Register(...)`（覆盖式）。
- 消费方（Cli/Tests/tools/RenderAll）须 `ProjectReference` 本工程 + `using DwgParser.Rendering;`。回归门 `RenderingSplitTests`（Core 无 System.Drawing 两面 + 接缝驱动 + 切缝不漂回）。
- 批量渲染工具：`tools/RenderAll/`（默认走 LayeredFrameRenderer，二次渲染全缓存命中；`--single` 整体渲染、`--layers` 导出逐层 PNG。tools/RenderDemo 已改为单图 dimension dump，勿混淆）。

## 5. 入口 API（DwgDocument）

```csharp
var doc = DwgDocument.Load(path);            // 或 Load(path, byte[])
doc.Version / doc.VersionName               // 版本判定
doc.Header                                   // FileHeader
doc.Sections                                 // 发现的节（含诊断性伪节）
doc.R2000 / doc.R2004                        // 对应容器（可空）
doc.Diagnostics                              // 加载诊断，永不抛
doc.GetObjects()                             // 惰性对象流遍历 → WalkResult
doc.ExtractStrings(maxLength=200)            // 字符串挖掘（原始流+解码节+字面量扫描，去重）
doc.ScanObjectClasses()                      // AcDb/Acad 类别统计
doc.HasPreviewBitmap() / doc.TryExtractPreview()  // 内嵌预览位图
doc.Save(path)                             // 同版本原样写回（经 IDwgFile.Save）

// 版本无关公共模型（四类内容视图，全部惰性派生自同一份 GetObjects()，零二次解码）
doc.Entities                                 // IReadOnlyList<IEntity>（不含 BLOCK/ENDBLK/SEQEND 伪实体）
doc.UnmappedEntities                         // 补扫产物：无记录、不可写回的实体（映射外对象）
                                             //   FindRecord 取不到、另存不写出；FindEntity 仍可按句柄寻址
                                             //   R13–R2000 源不走补扫 ⇒ 恒空
doc.Layers                                   // LayerTable（句柄/名字双向查 + 状态判定）
doc.Blocks                                   // BlockTable（句柄/名字双向查 + FindAll 取重名匿名块
                                             //   + ModelSpace/PaperSpace + EntitiesOf(块头句柄)=块内容）；
                                             //   = walk.Blocks，缓存一份
doc.ObjectRecords                            // IReadOnlyList<DwgObjectRecord>
doc.Styles                                   // DwgStyleCatalog：九族样式全按句柄索引
                                             //   Text/Dimension/Linetype + Visual/TableStyle/MLeaderStyle
                                             //   /MLineStyle/ViewStyle/ViewStyleTemplate + All（并集）
                                             //   ⚠ 只含「读取器已具体化」的样式（按源版本门控，有已知空洞）
                                             //     判存在性用 ObjectRecords + ClassTypeCodes
doc.FindRecord(handle) / doc.FindEntity(handle)   // 句柄索引（DWG 的寻址方式：Owner/Layer/块头/样式
                                                  //   引用全是句柄；惰性建、Save 后随视图一起重建）
doc.Save(path, DwgVersion.AC1021)            // 另存为目标版本；SaveAs(ver, path) 同义
// ⚠ 任何 Save 之后内容视图一律失效（Objects/Styles/句柄索引归零、AcDs 挂接标志复位）⇒ 再次 GetObjects() 重建。
//   跨版本重写会在**源记录**上落目标侧标记（如 StampBlockHeaderOwnedVectors 写 rec.BlockOwnedHandles），
//   故源侧视图同样必须重建——别把保存前的 WalkResult 继续当有效视图用。

// 跨版本转换（独立入口，不经 DwgDocument）
var f = DwgConverter.Load(path);             // 或 Load(path, bytes)（与 DwgFile/DwgDocument 对称）
DwgConverter.Save(f, DwgVersion.AC1015, outPath);
// ⚠ f 的静态类型 IDwgFile **已暴露 Version / VersionName**（2026-09-17 起）⇒ 问「这是哪一版」
//   不必再向下转型到 R2000File/R2004File；接口只藏版本特有的帧式/字段，不藏版本号本身。
```

## 6. CLI（src/DwgParser.Cli/Program.cs）

```
dwginfo <文件.dwg> [--strings] [--classes] [--types] [--entities] [--svg 输出.svg] [--preview 输出.bmp]
dwginfo <文件.dwg> --convert AC1015 --output old.dwg    # 跨版本转换（目标仅 AC1015、AC1018、AC1021、AC1024、AC1027、AC1032）
```
`--svg` 隐含 `--entities`。输出含版本/维护版本/代码页/定位标记/节清单/实体列表（hdl/kind/color/owner/几何）。

## 7. 测试（tests/DwgParser.Tests/）

- **测试方法静态计数（2026-09-25 实测，仅供参考）：`[Fact]` 1102 + `[Theory]` 675；331 个 .cs。**
  **计数一律以即时 `python tools/winbuild.py dotnet test DwgParser.sln` 输出为准**——静态 `[Fact]` 数
  不等于实际执行的测试数（`[Theory]` 每例展开、跳过的语料缺席例不计入通过），且会随并行手增删。
  **勿**把本技能里任何数字当「当前全绿」的证据（详见 `docs/handbook/progress-and-debts.md` 快照）。
- 覆盖面（按类名索引，清单会变）：实体解码/渲染、`BitWriterTests`(位流互逆)、
  `ObjectStreamRewriteTests`(同版本字节恒等/跨版本几何保持)、
  `R2018DowngradeToR2010Tests`(专用搬运分支 has_ds_data 同步)、
  `R2004ToR2010HandleStreamSplitTests`(R2004 家族→R2010+ 句柄流分界)、
  `OdaFixtureTests`(ODA 夹具)、`ClassesDowngradeTests`(CLASSES 降级)、
  `HeaderDowngradeTests` / `HeaderVarsCodecTests`、`R2007ContainerTests` / `R2007DictionaryDecodeTests`、
  `EntityMirroringTests`、`DimensionAlignmentTests`(DIMENSION 尾段对齐回归门)、
  `VisualStyleR2013bTests`、`R2007TrueWriterTests`、`RenderingSplitTests`(渲染切分回归门)、
  `SourceFileSizeTests`(800 行上限)、`PerFileTypeCodeGateTests`(类型码门)。
- **Teigha 真值对标门**（低版本实体保真）：`python tools/oda-compare/teigha_truth_pipeline.py`；
  `entity_truth_gate.py --bless/--gate` 与 `tools/oda-compare/entity-gate-baseline.json` 比对；
  报告 `out/teigha-gap-report.md`。我方侧结构化导出用 `dwginfo <dwg> --json out.json`。
  **覆盖率数字以即时门输出为准**（勿引用旧值）。
  两个坑：ODA `db.version()` 对旧文件返回"默认另存版本"，真实版本用 `originalFileVersion()`；
  R2004/R2000 句柄映射的增量**按页重置**（每页首条为绝对值），改成跨页累计会让对象数骤降。
- 真实样本：`TestData/`（`OdWriteEx XRef.dwg`(AC1018)、`透镜图框.dwg`/`透镜毛胚半成品.dwg`(AC1015)、
  `AC1027.dwg`/`AC1032.dwg`、`libredwg_2010_Arc/2013_Arc/2013_circle/2018_Arc.dwg`、
  `Oda/透镜图框_AC1021_ODA.dwg` 等）。**具体清单以 `ls tests/DwgParser.Tests/TestData/` 为准**。
- csproj 用 `None Include="TestData\**" CopyToOutputDirectory=PreserveNewest` 拷贝样本；
  **新增样本必须放 TestData/ 并保持该规则**。
- 构建/测试/门的**合法通路**（硬约束 9）：一律 `python tools/winbuild.py …` 包装，
  `dotnet build` 必带 `DwgParser.sln`，改过 Core 后逐个 `-t:Rebuild` 不在 sln 里的工具工程。
  三条命令：
  ```bash
  python tools/winbuild.py dotnet build DwgParser.sln -t:Rebuild
  python tools/winbuild.py dotnet test  DwgParser.sln --no-build     # 计数以此刻输出为准
  python tools/winbuild.py python tools/oda-compare/gate.py         # 只读比对，勿随手 --bless
  ```

### 7.1 跑门 / 取数：退出码档位与 ODA 并发（先看这张表，别再互相冒充）

**退出码分档**（权威 = `tools/oda-compare/gate.py` 模块 docstring 自带那段；`dwg_matrix` 同口径；`minverify` / `richverify` 语义同源但**不区分 2/3**——单例闸那一档它俩 `return 2`）：

| rc | 含义 | 处置 |
|---|---|---|
| `0` | 与基线一致 | — |
| `1` | **判红**（回归；或 `--strict-sig` 下的签名变化） | 唯一值得取证的那一档 |
| `2` | **没跑成**：无 ODA / 缺 CLI / 缺基线 / 基线形状与代码侧不符（`shape_mismatch`，自检排在 build 与任何 ODA 调用之前）/ 目标安装目录被他人 ODA 占用 | 先修环境或形状，勿当判据红（`pitfalls.md` D46） |
| `3` | 起跑后被 **ODA 单例闸**拦下（`oda_wait.BUSY_EXIT`）= 同样没跑成 | 换一份安装目录，见下 |
| `4` | `cli_pool` 撞车（两 job 产物路径不唯一）= 拒跑 | 改产物路径，别读判定 |

⇒ 读到 `2/3/4` 时**那一轮日志里没有格读数**：回去查环境，不要报「门红」，也不要拿它跟基线逐格比。

**单例闸口径 = 每个安装目录一实例**（判据全文在 `tools/oda-compare/oda_wait.py` 文件头「单例核对」节：2026-09-22 起**不用锁**，起跑前核对命中即 `SystemExit(3)`，且**永不**杀他人进程）。
⇒ **并发的合法形态 = 另开一份安装目录**，实操即 `git worktree add out/_<tag> <现 tip 的 sha>`：`tools/ODAFileConverter/` 随仓跟踪（现场核：`git ls-files tools/ODAFileConverter | wc -l`）且 `tools/oda_path.py::find_oda()` 的仓内候选按**该文件自身位置**解析 ⇒ 每棵 linked worktree 自带一份 ODA，两棵树各起一进程互不拦。全量门只在**最终 base** 那棵树跑一次；主树与隔离树的读数不混进同一张表（隔离树须显式钉同一 commit）。

**`progress_lock` 的 `pid` 不是存活判据**：`acquire` 写进 holder 的 `pid = os.getpid()` 属于那条**瞬时 python 进程**（命令一返回就消失）⇒ 查它存活必然「已死」。LIVE / STALE 只由 `ts` 与 `ttl` 决定（`age > ttl`，默认 1800 s），`status` 打的 `STALE(可接管)` 就是该式的结果——**age 逼近 ttl 不等于弃管**，接管前先确认那档真没人写。

**两条命令形态坑**：
- `dotnet <X>.exe` ⇒ **rc=140** + “An assembly … has already been found but with a different file extension”（2026-09-27 实测）。合法形态只有两种：**直接跑 apphost** `<X>.exe …`，或 `dotnet <X>.dll …`；脚本侧一律经 `tools/oda-compare/dotnet_bin.py` 的 `cli()` / `probe()` 挑（它在 `.exe` / POSIX 无后缀 apphost / `dotnet X.dll` 里取第一个存在的）。
- `DWG_GATE_PAR`（`cli_pool.py`）并发的是**我方 CLI** 子进程（各写各的 `--output` 才安全），**不是**转换器；置 `1` = 完全串行，这一档就是 A/B 对读开关——并行前后 `report.md` 必须逐字相同。

## 8. 约定与易错点

- **零第三方依赖**：只允许 BCL；测试仅 xUnit/coverlet。不要引入 NuGet 运行时包。
- **解析失败不抛异常**：进 `Diagnostics` 列表继续（DwgDocument 的加载/发现路径）。
- **R2010+（AC1024/1027/1032）已可用**：BOT 类型码 / UMC 句柄流长度 / R2013 has_ds_data 均实现。
- **`BitWriter` 必须是 `BitReader` 的逐方法真逆**（MSB-first、raw 类型按位写、内部不做字节对齐）；改任一侧都要跑 `BitWriterTests`。
- **跨版本保存/转换**：`DwgObjectRecord`（遍历期捕获）→ `DwgObjectStreamWriter`（按目标版本重写对象头+公共字段+句柄流，几何位流原样搬运；`TryRewriteStringStream` 降级串流内联、`TryCarryVisualStyleR2013b` 跨 R_2013b 块插/剥、伪实体实体感知搬运）→ `R2000Writer`/`R2004Writer`/`R2007Writer`。对象入口取自句柄映射，故单个对象编码不完美不会污染其他对象。**外部有效性现状（2026-09-05）**：方向 B 36/36、同版本 recopy 全 OK；方向 A **2/36**（`AC1027→AC1024` 与 `AC1027→AC1032` OK），剩余拦路点已钉死两个——ODA 降级 51 对象结构归一化、AC1024→AC1027 容器 `needs recovery`（详见 README §9 条 2 / AGENTS.md）。
- **AC1021 (R2007) 写出（2026-09-05 已完成同版本 + 跨版本容器层）**：`R2007Writer.TryBuildFromSource`
  （LZ77-AC21 重压缩 `Lz77Ac21Compress` + RS 重交织 `ReedSolomonAc21` + 全部 CRC64 重算 `R2007Crc64`
  + `CrcRandomEncoder`）同版本真写出 **ODA 8/8 接受**；跨版本→AC1021 经 `R2007Template` +
  `TryBuildCrossVersion` 接线（合法 AC1021 容器，ODA 签名前移到对象流/Header 层）。
  **写侧布局规格见 `docs/r2007-writer-layout.md`**；防再踩要点：页物理形态只由节记录 `Encoded` 决定
  （`==4` 才 LZ77+RS 交织），读侧页长度取节目录 `CompSize`。剩余 = Stage G-②（对象流/Header 按
  AC1021 语义重编码）；逐字段复现工具 `tools/R2007Dump`。（历史调查档 `docs/phase2-r2007-writer.md`
  已不在仓内，结论散入 README 附录 A.5/A.8 与 `docs/_archive/`。）
- **libredwg 宏边界易错（实证更正）**：`UNTIL(R_2004)` 是**严格 `< R_2004`（不含 AC1018）**。表记录
  `COMMON_TABLE_FLAGS` 的 xref 三字段 `is_xref_ref(B)/is_xref_resolved(BS)/is_xref_dep(B)` 仅适用 ≤AC1015
  （R13b1/R14/R2000）；**AC1018 起（R_2004 的 `LATER_VERSIONS`）均为单 `is_xref_resolved(BS)`**（ref/dep
  由读取器赋值；xref 句柄本体在句柄流，不在几何流）。实证：`MgF2波片毛坯半成品.dwg`(AC1018,11 层) 用单 BS
  → 11 个真实图层名全解出，用 B/BS/B → 一个都解不出；**单图层 "0" 的极简样本是假阳性**（name 在 xref 之前读，
  切法不影响它），须拿多图层真实文件复验。`R2004PlusLayerNameTests` 守护。（另：R2004+ 图层色为 ByLayer/真彩
  占位、非具体 ACI，`DecodeLayer` 对 R2004+ 只发名字、色由 `InferLayerColors` 兜底。）
- **表记录 preamble**：名字 T/TU 之前必须先读完 `common_object_handle_data`（`num_reactors(BL)` + R2004a+ `is_xdic_missing(B)` + R2013+ `has_ds_data(B)`），否则位流错位（LAYER 修复的根因之一）。
- **R2007+ 表记录也要 `AttachStringStream`**：集中字符串流（TU）不挂接则名字从主数据流乱读（AC1021 LAYER 名字乱码根因）。
- **R2000 vs R2004 两套容器/两套对象读取**：改版本相关代码必须同时考虑两条路径与分派点 `DwgFile.Load` 工厂（R2004File/R2007File 各自 override `DiscoverSections`）。
- **句柄引用**：`DwgHandleReference.Resolve(ownerHandle)` 相对引用以 owner 为基准；INSERT 的 `BlockHeaderHandle` 须在 `BlockRegions` 中。
  **相对引用归一 ≠ 翻格解药（§525 实测否证）**：R2010+ 源的句柄流确含相对码（`code` 6/8/10/12/14，相对本对象自身句柄解算），
  `DwgObjectStreamWriter.HandleStreamRelRef.cs` 有归一为绝对码（code 4）的刀 + `RELREF_OFF=1` 消融闸；
  **但实测对 ODA 判词零效果**（刀开 23/30 = 刀关 23/30，差异格 0）。⇒ 相对引用不是这些格 REJECT 的真身，
  **别再把「句柄流编码」当拦路假设**；判词真身在数据区/节结构/类表。详见 `docs/issues/2026-09-25-relref-r2010-to-prer2007-handle-normalization-525.md`。
- **文本编码**：中文 CAD 文本常为 GBK；文本提取与 CLI 已有 GBK 回退逻辑（UTF-8 失败 → GBK）。新代码保持同样回退。
- **R2004 压缩**：`TryDecompress`（严格）、`TryDecompressSection`（停 0x11）、`DecodeLenient`（探测）用途不同，别混用。
- **解决方案**：`DwgParser.sln`（含 src/ 两项目 + tests/；tools/ProbeTool 与 tools/RenderDemo 不在其中，需单独 `dotnet run --project`）。
- **git 现状（2026-09-25 实测更正）**：本仓**有 `.git`**，且**多车道并行写同一主树**（工作区常有别道
  未提交 WIP，如 200+ staged 文件）。提交一律 `git commit --only -- <本次文件>`（**禁 `git add -A`**、
  禁代码树 `reset --hard` / 裸 `stash`），详情见 `AGENTS.md` 硬约束 7 + `faq.md` Q11。
  **禁止在共享主树跑仓库维护**（gc / `repack --prune`），见硬约束 10 / `pitfalls.md` D68。
  `.dsh/skills/` 本身**已被 git 跟踪**（勿当临时目录误删）。
- **给 `src/` 的 `.cs` 打补丁前先探行尾**：本仓 .cs **以 CRLF 为主**（现场数：`python -c "import glob;cs=glob.glob('src/**/*.cs',recursive=True);print(sum('\r\n' in open(p,'rb').read() for p in cs), len(cs))"`），而 LF 锚串去套 CRLF 档 ⇒ **assert 直接失败**（症状像「没匹配上」，实为行尾）；`Edit` 还会把整档重编码成 LF。字节级替换配方的**唯一落点 = `dwg-golden-rebless` §C1**，此处不重写。
- 大文件与二进制：`.dwg`、`docs-ref/`、`bin/`、`obj/` 不要整读进上下文。

## 9. 技能选择总表（`.dsh/skills/`，2026-09-25 整理）

本仓 10 个 DWG 技能的分工与**互认边界**。开工前按下表**选一个**；每技能内部还会互引，但**同类技法
只有一个落点**（标注见「唯一落点」列）。载体为操作手册，硬约束一律以根 `AGENTS.md` 为准。

| 技能 | 干什么 | 触发语（含 argument-hint） | 不适用 / 唯一落点 |
|---|---|---|---|
| **`dwg-parser-map`** | 代码定位、模块职责、数据流、入口 API、构建/测试/门通路 | 定位代码、改代码前 | **本项目结构地图的唯一落点**（本档） |
| `dwg-direction-a-cell-forensics` | 格级（SRC→TGT）ODA 互认取证流水线：oracle 重生成 → 我方帧 → firstdiff 全帧枚举 → 分类台账 | 「取证 AC1024→AC1021」 | 不是帧级定谱（→ bitstream-diff）；不是最小致命集（→ bisection） |
| `dwg-bitstream-diff-forensics` | **帧级**两帧位流定谱：对象头自洽、无贪心种子普查、递归对齐 gap、DXF 真值锚、三种假说、集中串流判据、串编码规律、位流原语速查 | 「位流差异取证 531 1EBAE」 | **帧级位流技法的唯一落点**（含位序 / UTF-16 字节序坑） |
| `dwg-tail-form-audit` | 尾句柄流（帧尾）三侧同形定谱：R2kCmp + r20tail.py + 形制集合 | 「尾段定谱 AC1015 512 三侧对照」 | 只管**尾句柄流自身**；数据区帧级差异 → bitstream-diff |
| `dwg-minimal-fatal-set-bisection` | 富图格被拒、候选上千时，用 `inter_bisect.py` 半集二分收窄到单枚 | 「半集二分 add_2018.dwg ACAD2000」 | 只管**候选集收窄**；前两条路（差集 / 链台账）已死才用 |
| `dwg-red-first-ablation-verification` | 证明落下去的刀真改产物字节、判据不恒真：源码级消融（闸 `=> false`）+ 零 ODA 替代 | 「验刀 AttrTailAc1015Down vs …Tests」 | 只管**验刀**；钉值/语料 → golden-rebless |
| `dwg-golden-rebless` | 判据值重钉（手算复核、CRLF 安全）+ 语料缺席归因处置（责任定性、换挂替身先验同一枚图） | 「重钉 DimensionFamilyRendererTests 的 …」 | 只管**判据值/语料**；不是验刀 |
| `dwg-entity-truth-parity` | 实体层 Teigha 真值对标（**不跑 ODA**）：重导 ours → 缺口报告 → nodata 三分类 → 版本具体类迁移 → 守绿 | 「实体对标 TABLE」 | 只管**实体数据层**；跨版本容器/对象流格 → direction-a |
| `dwg-fieldization-capture-only-hand` | 字段化 capture-only 一手：只读侧捕获、零搬运语义改动 ⇒ 产物逐字节不变 | 「收 _MLEADEROBJECTCONTEXTDATA_CLASS 30 枚」 | **零搬运语义改动**；判据放宽须用户裁定 |
| `dwg-q10-ff-push` | Q10 快进推送远端（仅 behind=0 且快进形态） | 「按q10推送远端」 | 只处理**快进**形态；非快进 → `faq.md` Q10 全序 |

**常见岔口速判**：
- 「先定帧还是先定字段？」→ 已有 firstdiff/xcmp 报「哪帧差」⇒ `bitstream-diff-forensics`。
- 「尾段差还是数据区差？」→ 尾句柄流形制 ⇒ `tail-form-audit`；数据区字段 ⇒ `bitstream-diff-forensics`。
- 「刀真生效没？」→ `red-first-ablation-verification`；「值对不对？」→ `golden-rebless`。
- 「整格被拒，病在谁？」→ 差集/链台账已死 ⇒ `minimal-fatal-set-bisection`。

**编辑技能的硬约束**（`.dsh/skills/` 卫生）：
- `description` 有**加载器 1024 字符硬上限**（超限该技能加载失败；`1045856e` 曾修 `dwg-golden-rebless`
  1632→985）。**改任何 `description*` 后必须数字符数**（含 `description_en` / `description_zh` 各自可能另受同一上限）。
- **10 个技能必须保持新式 frontmatter 一致**：`name` + `name_en` + `name_zh` + `description` +
  `description_en` + `description_zh` + `argument-hint` + `argument-hint_en/zh` + `user-invocable`。
- **同类技法只有一个落点**（见上表「唯一落点」列）；别处只留指针，勿平行维护。
- 技能**不写会腐化的读数**（测试计数 / 文件行数 / 进度）：写进技能即逼每手改它。要数字写**指针 + 现场复核法**。
