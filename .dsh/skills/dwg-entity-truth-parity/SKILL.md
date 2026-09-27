---
name: dwg-entity-truth-parity
name_en: Entity Teigha Truth Parity
name_zh: 逐实体Teigha真值对标
description: Reproducible parity pipeline for DwgParser per-entity Teigha/ODA ground-truth comparison. Rebuilds Cli only (never re-runs ODA), re-exports out/ours from existing out/teigha-truth, runs entity_truth_gate for a fresh gap report, triages every nodata/diff into truth-side-missing-key vs ODA-derived-fake-gap vs genuine DWG field gap, cross-checks candidate field mappings per handle against disk readings before registering them (ODA-derived keys falsify intuitive mappings), migrates each entity category to the interface + shared base + internal per-version concrete class + EntityFactory pattern, and locks coverage with --gate/--bless. Includes the DvWalk tbl/tblscan/tblbody/tblraw bit-walk forensics for pinning field offsets by geoRel anchors and the compare_field dispatch-shape check. Use when continuing "完善每个类别的 Entity", adding entity slots, or investigating entity_truth_gate nodata/diff rows.
description_zh: DwgParser 实体数据层的 Teigha/ODA 真值对标可复跑流水线：只重建 Cli 不重跑 ODA、用现有 out/teigha-truth 重导 out/ours、entity_truth_gate 出最新缺口报告、把每个 nodata/diff 三分类为「真值侧缺键 / ODA 派生假缺口 / 真实 DWG 字段缺口」、**登记映射前先按句柄把盘上读数与真值逐枚对（派生量会否证直觉映射）**、按「接口+公共基类+internal 版本具体类+EntityFactory」范式逐类别迁移、DvWalk 位走查按 geoRel 钉字段偏移、--gate 守绿 / --bless 固化基线（纯增量不必 bless）、并核 compare_field 分派支与 xy_of 支持面是否一致。用于继续「完善每个类别的 Entity」、补实体槽位、或排查实体字段覆盖率 nodata/diff 时。
description_en: DwgParser 逐实体 Teigha 真值对标流水线（重建 Cli 重导 ours → entity_truth_gate 报告 → nodata 三分类 → 盘上读数×真值逐枚对映射 → 接口+版本具体类+工厂迁移 → geoRel 位取证 → --gate/--bless 守绿固化）。
argument-hint: 点名要处理/排查的实体类别，如「实体对标 TABLE」「排查 AcDbMText nodata」
argument-hint-en: Name the entity class to work on, e.g. "parity TABLE"
argument-hint-zh: 指定实体类别或门报告项，如「TABLE 真值对标」
user-invocable: true
---

# 逐实体 Teigha 真值对标（entity truth parity）

目标：把实体数据模型（读侧解码 + `--json` 暴露 + 门映射）逐类别对齐 ODA/Teigha 真值，并在门上有可度量、可固化的增量。**只动实体层**；跨版本容器/对象流格子取证用 `dwg-direction-a-cell-forensics`，代码定位先加载 `dwg-parser-map`。遵守 AGENTS.md 全部硬约束（零第三方依赖进 Core、800 行上限、docs-ref 只读定向取证、解析失败不抛异常）。

## 资产与语义（先认清四个东西）

| 资产 | 位置 | 性质 |
|---|---|---|
| 真值 | `out/teigha-truth/<stem>.teigha.json` | `tools/TeighaDump`（本机 `Teigha/` ODA SDK）逐对象导出。**通常不重跑**——真值没变就不要碰 step 1 of `tools/oda-compare/teigha_truth_pipeline.py` |
| 我方 | `out/ours/<stem>.dwg.json` | Cli `--json` 产物，每次改完 src 必须重导 |
| 门 | `tools/oda-compare/entity_truth_gate.py` | 按类逐字段比对；`CLASS_SPEC` 登记真值键→我方键映射；无 flag=出报告，`--gate`=对基线判绿（exit 0），`--bless`=把当前覆盖写进 `tools/oda-compare/entity-gate-baseline.json` |
| 报告 | `out/teigha-gap-report.md` / `.json` | md 有「待补槽位排行」（= Entity 类活清单）；json 有每类 `fieldOk/fieldNoData/fieldDiff` 计数，供脚本查询 |

## Step 1 刷新报告（不重跑 ODA）

```bash
dotnet build DwgParser.sln -v q    # 必须连 Cli 一起 build；只 build Core 用旧 dll = 假阴性
CLI="src/DwgParser.Cli/bin/Debug/net8.0/DwgParser.Cli.dll"
for tf in out/teigha-truth/*.teigha.json; do
  stem=$(basename "$tf" .teigha.json)
  for d in "最简各版本图纸" "图纸模版"; do
    cand="$d/$stem.dwg"; [ -f "$cand" ] && dotnet "$CLI" "$cand" --json "out/ours/$stem.dwg.json" >/dev/null 2>&1 && break
  done
done
PYTHONIOENCODING=utf-8 python tools/oda-compare/entity_truth_gate.py --gate   # 先看是否仍对旧基线绿
PYTHONIOENCODING=utf-8 python tools/oda-compare/entity_truth_gate.py          # 无 flag = 重出报告
```

个别 truth stem（历史上如 `maprebuild2`/`tB2`）仓库里没有源 `.dwg`（是往轮跑门的额外夹具）——重导循环找不到就跳过，**不要删它们旧的 ours json**。机理：门的 `load_pair` 对 stem 走 `glob(out/ours/<stem>*.json)` 兜底 join，所以只要旧的 ours json 还留着，门**仍会拿这份过期数据参与比对**——该 stem 的 diff/nodata **不反映本轮改动**（旧到没导出本轮新键即信号）。判无源 stem 的缺口前先确认其 ours 是否陈旧；别为它硬凑数据、也别指望它随本轮转绿。报告口径（parity 分子/分母）以门输出即时数字为准，不信文档里的旧百分比。

## Step 2 nodata/diff 三分类（定性先于动 src）

逐缺口项查 `out/teigha-gap-report.json` 的 `fieldNoData/fieldDiff`，再用 python 直接看真值 json 里该类实体 `specific.*` 的**实际键名**与我方 json 的键，判归三类：

1. **真值侧缺键/键名错位** → TeighaDump getter 命名（`getColumnType`、`horizontalMode`…）≠ CLASS_SPEC 映射键（`columnType`）。改门映射或补导，**不改 src 解码**。
2. **假缺口·ODA 派生字段（勿追、勿伪造）** → 值为 ODA 运行时/由 flag 位派生/方法合成（VPORT `onOff`、编号、MText 参考矩形/行距一族排行项多为 `specific.*` 派生），DWG 里本无 per-entity 字段。
3. **真实缺口** → 目标字段是 DWG 对象流里的真身（TABLE `num_cols/num_rows` 即此类），进 Step 3/4 施工。

**从门报告直接分流（按 `compare_field` 落的 nodata 原因串对号入座，省得二次猜）**：

| 门里的 nodata/排行标签 | 归三类 | 动作 |
|---|---|---|
| `我方无该槽位（待补）` 且真值 json 该 handle 确有值 | ③ 真实缺口 | 进 Step 3/4 |
| 排行段 `unmapped:<真值键>`（真值有该键、CLASS_SPEC 未登记） | ③ 真实缺口（先登记映射再判） | 补 reader+`--json`+CLASS_SPEC 四件套 |
| `真值无该字段` / 真值 json 根本无该键（如 MText `attachment`，列信息实为 `getColumnType` 恒 `kNoColumns`） | ① 真值侧缺键（假缺口） | 勿追，非我方缺陷 |
| `真值角点未取到（工具待补）` | 真值管线工具局限 | 非我方缺陷，勿追（可另立 TeighaDump todo） |
| `非数值` / `真值非法向量` / `点位形状不匹配` / `形状不匹配` / `未知比对方式` | 比对器形状/口径问题 | 回查 CLASS_SPEC 比对方式，非解码缺口 |
| 报告单列 `ghosts`（已 `isErased` 过滤）、`mleaderGap`（链孤儿 MULTILEADER） | 已知管线产物 | 不计缺口，别当 bug 追 |

**两个必查陷阱**：① Cli 的 `Num()` 省略零值——我方已解码但值为 0 会伪装成 nodata（先 grep `--json` 导出清单确认键是否已存在，如 `textAttachment/vportTwist/vportOn/extrusionZ` 早已导出）；② 补槽位顺序 = `DwgEntity` 槽位 → reader 解码 → `--json` 导出 → CLASS_SPEC 登记，四件缺一门上看不到增量。**先分清「没解码」还是「解码了没导出」**（例：TableRows/TableColumns 槽位本已存在，缺的只是 json 键与解码）。

**映射前必做的一步：把盘上读数与真值逐枚对，别拿组码号段猜直觉映射**（2026-09-24 批次 91 实测）：在 reader 里临时打印该实体每枚候选向量的读数，按**句柄**与真值 json 的 `specific.*` 逐枚比。两类结果都有价值：① **一致且值多样**（LEADER 的 `x_direction` ↔ `annotationXDir`：9/9 全等，含 `(1,0,0)` / `(-1,0,0)` / `(1.83697019872103e-16,1,0)` 三类不同值）⇒ 超定成立，登记映射；② **盘上恒零而真值非零**（LEADER 的 `inspt_offset`(212) 盘上 `(0,0,0)` vs 真值 `annotationOffset = (-0.4349, -0.1842, 0)`，语料内 27 枚非默认同形）⇒ 该键是 **ODA 派生量**，归 ②，**只落槽、不登记映射**（否则门会「绿」在错东西上）。**直觉映射被否证这件事要写进门内注释**，否则下一手重新猜一遍。

**门器具体系两条必核**：① `compare_field` 的**分派支**与 `xy_of` 的**支持面**不一致——`xy_of` 早就算得 `xyz`，但 `compare_field` 没有 `"xyz"` 支 ⇒ 登记 `"xyz"` 映射会静默落「未知比对方式」，门既不计 ok 也不报错（批次 91 补了该支；改门前 `grep -n 'comp == "' entity_truth_gate.py` 数一遍有哪些支）；② 三元数组导出用 Cli 现成的 `Pt3(w, name, Vec3)`（`Program.Json.Body.cs`），别自造写手——比较器吃 JSON 数组形状，自造易与 `xy_of` 的形状要求不合而退成 `nodata`。

**抬基线的边界**：`--gate` 语义是「与基线一致或更好」⇒ **纯增覆盖的一手不需要 `--bless`**（让它报通过即可）。只有**判定口径本身**变了（改比较器、改类名映射）才 `--bless`，且按「bless 只单行外科替换」——实体门基线是本 lane 共享的，并行手在改 EDIT/渲染族时随手全量重钉会吞他们的更新。另：一轮里全量测试出现红，**先在基线树（同 sha、不含本改动）跑同名筛选**再判归属（批次 91 的 6 红全部来自并行 lane 的「语料清退」提交）；主树语料可能被别的手从工作副本删掉 ⇒ **取证与判据一律在 `git worktree` 里跑**。

**「真值数组多一维」≠「我方丢了一维」**（2026-09-24 批次 92 差点误判的一例）：Teigha 的 `solidCorners` 是
`[[x,y,z]×4]`，看着像我方 `Vec2` 角点丢了 Z —— 但**先核盘上布局**再下结论：SOLID/TRACE 走
`SolidBodyReader` = `BT thickness · BD elevation · 4×2RD 角点 · BE extrusion`，盘上角点本就是二维、
Z 语义在 `elevation`（且早已捕获），1752 枚真值角点的 Z **全部为 0** 正是这件事的旁证。
⇒ 顺带一条判据体检：`corners` 比较器只比 `tp[0], tp[1]`，若某族**确实**有三维角点（3DFACE 是），
Z 读错也不会在门上红 —— 这类「比较器少一维」的洞要按**盘上布局**逐族判断，不是一律补。

**语料零见证的一手怎么写**（批次 92：全真值集 `3DFACE` **0 枚**）：① 落值照做（模型完备性 + 位落判据），
但**明写「无可观测值效应、价值在结构面」**，不许靠「语料里应该也有吧」蒙混；② 锁只能落**合成帧**，
每枚值取**互不相同**的数 ⇒ 少读一枚即红；共面/不共面两档都要一例（只给一例时「把语义位读反」仍能蒙过）；
③ 把"无见证"本身变成可复跑证据 —— 重导 `out/ours` 后 `grep -c cornerZs` **= 0** 就是机器凭证，别只写一句"没找到"。

## Step 3 位取证（真实缺口找字段偏移）

扩展 `tools/DvWalk`（独立 csproj，`dotnet build tools/DvWalk/DvWalk.csproj`）加走查模式，范式照 tbl 家族：`tbl`（按句柄找记录打几何段）、`tblscan <dwg> <hdl> <fromRel>`（逐位试读打 geoRel 候选位）、`tbltry`（试某字段序消费）、`tblbody`（按权威 spec 序全走查逐字段打 geoRel+值与 ODA 比对）、`tblraw <dwg> <hdl> <from> <to> <at>`（dump 原始位窗 + 指定位解值）。判据：

- **锚点超定**：用 ODA 真值的多个**不同值**样本（跨文件/跨版本，如 num_cols 取 4,1/3,1/6,4/5,5）交叉钉死同一 geoRel（TABLE 实测 cols 落几何段相对位 161 = 两枚连读 BL）。单样本单值不唯一，禁猜。
- **权威字段序先查对地方**：libredwg 参考里 TABLE 布局在 `dwg2.spec` 的 `DWG_ENTITY(TABLE)` pre-R2010 `else` 分支——`dwg.spec` 确无 AcDbTable 块（照 dwg.spec 查会误判"无权威布局"）。只允许定向 `grep`/`sed -n`，禁整读。注意版本门细节：INSERT 前缀里 `num_owned` 对 R2004+ 是**无条件**读（别按 has_attribs 门控）。
- **"文法退化不唯一"先回查共享前缀**：TABLE 表头 21 位多解退化的真身 = 共享 `DecodeInsertGeometry` 的 rotation 位宽读错一位——穷举无解时先怀疑前面的字段宽，不是后面。穷举仍不唯一 → 造 R2013+ 夹具或富真值消歧，宁可回退脚手架保持绿色基线，**不落地猜出来的字段序**。
- C# 小坑：变量名别用 `byte`（关键字，CS1001）；DvWalk build 失败时旧 dll 会把未知模式静默落成默认模式（输出看着像跑通），每改必查 build 成功。

## Step 4 逐版本具体类迁移范式（LINE/TABLE 已验证）

新文件全部放 `src/DwgParser.Core/Objects/Entities/`，四件套：

1. `IXxx.cs` — 版本无关语义接口（类别抽象，注释里写清版本族分岔）。
2. `XxxEntity.cs` — `public abstract class XxxEntity : DwgEntity, IXxx`，公共基类；把既有槽位（如 `TableColumns/TableRows`）当落点；抽象 `ReadBody/ReadGeometry(BitReader)`。**DwgEntity 已为此去 sealed**——消费链（渲染/JSON/句柄流）仍按 DwgEntity 走，零改动。
3. internal 版本具体类 — 如 `LineAcPreR2000`/`LineAcR2000Plus`、`TableAcPreR2010`/`TableAcR2010Plus`；版本差异只体现在各自 `ReadBody`；基类不判版本。
4. `EntityFactory.cs` — `CreateXxx(ulong handle, bool <versionFlag>)` 只选型不读流；`Decode(IXxx target, BitReader br)` **以接口为参数**，内部 `target switch` 到具体类。调用点须显式 cast：`EntityFactory.Decode((ITable)entity, br)`（多接口重载靠 cast 消歧）。

接线两处在 `DwgObjectReader.Dispatch.cs`（热文件，改前查 mtime + 备份）：创建点三元扩展 `type == TypeXxx ? EntityFactory.CreateXxx(handle, flag) : …`；几何 case 从共享分支拆出改走工厂 Decode（flag 用 `version.IsR2010Plus()` 等既有判定，注意变量在创建点之前已算好）。版本选型注意实体存在性下限（如 AcDbTable 自 2000 起，pre-R2010 类天然不覆盖 R13/R14）。每类附一对单测（工厂选型 + 真实文件行为保持），照 `LineEntityModelTests` 模板。

## Step 5 守绿与固化

```bash
dotnet test DwgParser.sln --no-build 2>&1 | grep -aE '失败|通过:'        # 即时计数；并发窗口假红隔时重跑
PYTHONIOENCODING=utf-8 python tools/oda-compare/entity_truth_gate.py --gate   # 覆盖只升不降
python tools/oda-compare/gate.py --bless                                     # 碰了 Dispatch 等共享热文件 → 方向 A 矩阵也要无回归
PYTHONIOENCODING=utf-8 python tools/oda-compare/entity_truth_gate.py --bless  # 确认增量后抬基线
```

热文件（`Dispatch.cs`/`DwgEntity.cs`/`Program.cs`）改前 `ls -l --time-style=+%H:%M:%S` 查 mtime、备份到 `out/_backup_<date>_entity/`、最小化 diff；任何 src 文件越 800 行同轮拆分。回退脚手架时清干净（含 out/ours 里带新键的临时 json），恢复试点前绿色基线后再重放。收口后同步 README 附录 C（三分类结论 + 每类迁移台账 + 取证判据，含"勿追勿伪造"清单），保持可接力。

## 验证（每轮收口必过）

`dotnet test` 全绿（计数以即时为准）+ `entity_truth_gate --gate` exit 0 + 目标类字段在 gap report 中 `fieldOk` 增长/`fieldNoData` 清零 + （碰热文件时）方向 A `gate.py` 无回归。若某类字段判为假缺口，README 记「勿追」名单而非硬凑数据。
