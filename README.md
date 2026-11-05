# DwgParser

DWG（AutoCAD AC1015-AC1032）解析 / 跨版本转换 / 软件渲染库，附 CLI（`dwginfo`）与 WinForms 查看器。
零第三方运行时依赖；解析失败不抛异常，未表态的分派显式炸。

## 构建

```
dotnet build DwgParser.sln
```

构建门须全量重建（增量构建会假绿）：`dotnet build DwgParser.sln -warnaserror --no-incremental`。

## 门禁

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gates/gates.ps1            # G1-G12 全量
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gates/gates.ps1 -Gate G7   # 单门
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gates/gates.ps1 -Rebaseline [-Stem <名>]   # 基线重生成（棘轮名单不触碰）
```

| 门 | 内容 |
|---|---|
| G1 | 构建 0 error 0 warning（`--no-incremental`） |
| G2/G3/G4 | 基线快照：`--json` / `--svg` / 同版本 `--convert` 与 `out/base/` 逐字节比对（fixture 清单含路径与 SHA256，见 `out/base/manifest.json`，**不入库**属本地门禁状态） |
| G5 | 全仓 `*.cs` ≤ 800 行 |
| G6 | 依赖方向单向（Core 不依赖 Rendering；设备层不反引 FrameRenderer） |
| G7 | 实体层不变量（`tests/DwgParser.Tests`：codec typeCode×版本全量、渲染全 kind 表态、集合双模式语义、族默认值、编辑三判据、光栅冒烟墨量、拾取几何） |
| G8 | 跨版本转换×自消费全矩阵（7 fixture × 6 版本 convert→自读回）+ 值级哨兵（ltscale/celtscale）+ 已知坏棘轮（`manifest.g8KnownBad`，修后不摘即红） |
| G9 | 编辑回路端到端（inspect→组单→--ops→回读验证；setText/setAttrib/addMText/delete/edit-attr 五腿 + 用法负例 rc=2） |
| G10 | 只读诊断模式冒烟（strings/classes/eedaudit/fieldize × 7 fixture + acis/preview） |
| G11 | 分层渲染容差门（`--pngL` 哈希→漂移则像素差分 ≤2000px 容差，`tools/gates/pngdiff.py`） |
| G12 | 拾取场景端到端（`DwgScene.Load`→Pick 命中/确定性/图幅外/巨容差） |

fixture 缺失时 G2-G4 报 `BLOCKED`，不报绿。PNG 字节门仅同会话有效（GDI 文本反锯齿跨会话漂移），
跨会话用 JSON/SVG/convert。

## CLI 速查（DwgParser.Cli → dwginfo）

```
dwginfo <文件.dwg> --types                     # 对象类型分布（诊断解码覆盖率）
dwginfo <文件.dwg> --json out.json             # 结构化快照（真值比对用）
dwginfo <文件.dwg> --svg out.svg               # 渲染 SVG
dwginfo <文件.dwg> --png out.png               # 渲染 PNG（900×900）
dwginfo <文件.dwg> --convert AC1024 --output out.dwg   # 跨版本转换（同版本=字节等价）
dwginfo <文件.dwg> --inspect cand.json         # 图纸修改候选（机读面）
dwginfo <文件.dwg> --ops ops.json --output o.dwg --report r.json   # 批量值级改写
```

完整选项 `dwginfo` 无参输出；`--inspect/--ops` 契约见 `docs/design-cli-edit-interface.md`。

## 架构

| 项目 | 职责 |
|---|---|
| `DwgParser.Core` | 全部解析/写侧/渲染核心（net8.0，零第三方依赖；`InternalsVisibleTo` 仅 Rendering/Tests） |
| `DwgParser.Rendering` | PNG 光栅化、GDI 文本后端、分层渲染器 |
| `DwgParser.Cli` | `dwginfo`（JSON/SVG/PNG/convert/edit 机读面） |
| `DwgParser.WinForms` | 查看器 |
| `tests/DwgParser.Tests` | 零依赖不变量运行器（G7） |

Core 内部地图：

- `Objects/Reader/` — 容器与对象流走查；`Entities/Codecs/` — 33 实体编解码器注册表
  （新增实体类型只碰 Codecs/ 一处 + Register 一行）
- `Objects/Entities/` — `EntityBase` 公共字段 + 10 族属性模型（`*Geometry.cs`）+ 版本薄类
- `Objects/DwgEntity.cs` — 162 行纯转发（族模型实存储，对外 API 只增不改）
- `Render/Draw/` — `IDrawDevice`（PNG/SVG/Recording 三实现）+ 实体渲染注册表（`For` 光栅 / `ForSvg` 矢量）
- `Format/` — 文档容器（`DwgDocument`）、编辑器（`DwgEditor`）、集合层（`EntityCollection` 活视图）
- `Objects/Writer/` — record 驱动写侧（97 分片；裁定见计划档 M5）

## 关键约定

- **字节不变**：同版本 `--convert` 产物与源逐字节相同（G4 常绿是所有重构的前提）。
- **字段只增不改名**：写侧按 record 字段名驱动，改名即跨车道冲突。
- **显式炸**：未注册实体类型、未表态渲染 kind、附着集合变异——一律抛出，不静默回落。
- **无代码注释**：位级与设计知识一律进 `docs/`（本 README 与 plan/progress/design 三档）。

## 文档

- `docs/entity-refactor-plan.md` — 实体类重构计划（W0-W4 已完成，冻结存档；含契约偏差全表）
- `docs/project-progress.md` — 项目进度台账（含 HATCH 渐变块修复史与门教训）
- `docs/design-cli-edit-interface.md` — `--inspect/--ops` 机读契约
