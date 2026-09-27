---
name: dwg-census-gap-root-cause-triage
name_en: "Census Gap Root-Cause Triage"
name_zh: "普查缺口三分类根因分诊"
description: "Decide what a --census row showing captured=0 AND rejected=0 with empty slots really is in the DwgParser fieldization (capture-only) track, choosing between out-of-scope, a silent early-return before the bag is built (dumb row), and a genuine no-path gap. Greps the family reader shard's `??= new` bag line and counts returns above it, reconciles with the tools/DvWalk probe while avoiding the TypeCode-vs-LogicalType ruler-blindness false negative, splits disposition between ruling-out (libredwg dwg.spec vs dwg2.spec volume ownership) and staying in-ledger, and lists bit-level self-deception traps (writer-feature anchor, zero-value repeat, prefix-form BD, derived coords). Use when a census line is neither captured nor rejected, when tc531-style probes print nothing, or before deciding whether to relax an anchor criterion."
description_zh: "判定 DwgParser 字段化(capture-only)轨道 --census 里某行 captured=0 且 rejected=0 且 slots 空 到底是「口径外」「走查在建袋前无声早退(哑账)」还是「真缺口」。grep 该族 reader 分片的 `??= new` 建袋行、数其上方 return，用 tools/DvWalk 复现同时避开 TypeCode-vs-LogicalType 的尺子失明假阴性，按 libredwg dwg.spec/dwg2.spec 卷归属做判否/留账拆半，并登记位级自骗面(写手特征锚、零值重复命中、前缀式 BD、派生坐标)。用于普查行既未捕又未拒、tc531 类探针无输出、或决定是否放宽锚判据之前。"
argument-hint: "Name the census row, e.g. triage TABLECONTENT 531@AC1015/AC1018"
argument-hint-zh: "指定要分诊的普查行，如「分诊 _MLEADEROBJECTCONTEXTDATA 30 枚」"
user-invocable: true
---

# 普查缺口三分类根因分诊

## Overview

字段化(capture-only)轨道每手起手跑 `--census`，得到一张「`captured<total` 且 `ent<total`」的对象类名单。名单里最阴的一行是 **`captured=0 且 rejected=0 且 slots 空`**：它同时兼容三种截然不同的真相，普查本身**无法**区分。本 skill 给出一套 grep 驱动的分诊流程把它落到唯一一类，并给拆半处置与外部权威取法。

> 前置：遵守项目 `AGENTS.md` 硬约束；本 skill 只判「缺口是哪一类 + 拆半去向」，**深段位逐枚定谱走 `dwg-bitstream-diff-forensics`**，判据落档分层走本车道记忆与 `docs/fieldization-track.md`。
> 本文所有数字（逻辑码↔per-file 类表号、批次号、门格数、锚步长）都是**当轮快照**，引用前现场重跑，勿照抄成断言。

## Step 0 现跑普查 + 核既有判词（勿信陈旧名单）

并行手两小时内就能让名单陈旧。起手先现跑一次全语料普查，再对照台账：

```bash
# 现跑 --census（字段化轨道口径），取当前 captured<total 名单
# 调用形制以 docs/handbook/tools-and-fixtures.md 实测为准，勿按文档断言
# 名单里某行是否已在账上被处理过？三处必查
grep -nE '<CLASS>' docs/fieldization-track.md docs/direction-a-ledger.md docs/direction-a-backlog.md
```

台账 §3 = 已判否（勿重开）、§2 = 射程口径（本就不捕）、§4 = 现势在账。三者都命不中 ⇒ 这是需要本 skill 分诊的**新缺口行**。

## Step 1 三分类判定（核心：grep 建袋行、数上方 return）

`captured=0 且 rejected=0` 有三种成因，**判据 = 读该族 reader 分片**，不是看普查行本身：

```bash
# 该族的 reader 分片里，建袋行 record.X ??= new ... 在哪、它上方有几道 return？
grep -rn '??= new' src/DwgParser.Core/Objects/Reader/
```

| 现象 | 归类 | 证据 | 处置 |
|---|---|---|---|
| 该类**不挂 `DwgObjectRecord`**（如 HEADER 节）或已落 §3/§2 | **口径外** | 普查面上本就没有它的行位 | 不动，登记去向 |
| 袋字段反射可见（`public Records.XxxCapture? Xxx`）+ 版本在射程内，但**早退的 return 写在 `??= new` 建袋行之前** | **哑账 / 无声早退** | 走通路径存在、走查也进了、只是在建袋前 `return` ⇒ 既不捕也不盖章 ⇒ 普查只能记 `0/0` | Step 4 具名化 **且** Step 5 拆半去向（两者正交） |
| `Canonical` **未登记** ⇒ logical = base+1000 ⇒ 落 `Dispatch` 的 `default:` ⇒ 整帧 Opaque、`slots` 空、`opa>0` | **真缺口 / 根本没路** | 连走查路径都没有，与 531 薄形**不是一种病** | 见 Step 5「外部权威」 |

**关键区分（判据 = `Dispatch` 有无 `case` + reader 分片有无 `??= new` 建袋行，不是看 `opa`）**：
- 该类在 `Dispatch` 有**专用 `case`**、且其 reader 分片有 `??= new` 建袋行，只是 return 在上方 ⇒ **哑账**；
- 该类落 `default:`（`Canonical` 未登记）⇒ **真缺口**。

> ⚠️ **别拿 `opa>0` 当「真缺口」的票**：哑账样本也可能**整帧 Opaque 搬运**（`opa>0`、`slots` 空、袋 `null`）—— 531 薄形正是此类（对象级走 Opaque 整体搬运，但 `case TypeTableContent:` 会调 `DecodeTableContentStructure` 建薄形袋）。`opa` 只说「对象本体未被字段化」，**不说「有没有一条会建袋的走查路径」** ⇒ 单靠 `opa` 会把 531 误读成真缺口。只有当 `Dispatch` **无** `case`、直接落 `default:` 时才是真缺口。

（`rejected` 非 0 = 走查进过且建了袋、判据未过 ⇒ 正常拒收，不属本 skill；本 skill 只吃 **`captured=0 且 rejected=0 且 slots 空`**。）

## Step 2 探针复现 —— 先证尺子没瞎（TypeCode vs LogicalType）

要用 `tools/DvWalk`（如 `tc531` 子命令）独立复现「这几枚到底停在哪一闸」，但**探针自身会静默失明**：

```bash
# 失明写法：用【规范逻辑码】去比【per-file 类表号】
grep -n 'TypeCode == ' tools/DvWalk/*.cs   # 命中 ⇒ 高危：ZWCAD 件会静默零输出
```

> **本 skill 的招牌教训**：`531` 是规范逻辑码，而 per-file 类表号会漂移 —— ZWCAD 件实测把该类编成 `564`。凡 `Where(r => r.TypeCode == 531)` 的选取器，对 `sample_2000`/`sample_2004` **返回空**，看起来像「本图没有 531」，实为**假阴性**。修法 = `r.LogicalType == 531` + 行内打印 `t=<per-file 码>`，并留一条 WHY 注释。改法属工具改动、零 `src/` 风险，但**是否落库须用户点名**（那是别的 lane 的工具）。

**验收尺子没瞎** = 逐档 `census 枚数 == 探针行数` 全等（含此前失明的件）：

```bash
# 对每份语料：把 --census 的该类枚数与 DvWalk 的行数对齐
dotnet run --project tools/DvWalk -- tc531 "<file.dwg>" [hdl...]   # 改后须 dotnet build tools/DvWalk -t:Rebuild
```

任一档 census>0 而探针行=0 ⇒ 尺子仍瞎（先修选取器，别据此下「本图无该类」的结论）。

## Step 3 读消费面证「零行为」再动手

判据放宽/具名化前，先确认捕获袋有没有写侧消费者（决定能不能安全改）：

```bash
grep -rn '\.TableContent\b\|\.PurgePreventer\b' src/DwgParser.Core/Objects/Writer/
# 若消费者都是 `rec.X is { Captured: true }` 模式匹配 ⇒ 空袋与无袋同形 ⇒ 建袋/具名属零行为改动
```

写侧只吃 `Captured: true` ⇒ 把建袋提前、给早退具名，**不产位锚、不改 Kind、产物逐字节恒等**，恒等门可兜底验证。

## Step 4 哑账的处置 = 具名化（不是放宽判据）

若 Step 1 归到「无声早退」：把 `??= new` 建袋提到**射程入口**、每处早退各写精确 `FailReason` 文案（点位到「声明 N 位内仅 0 枚锚」）、并在 `Dispatch` 补一枚 `StampSilentReject` 兜底章。**控制实验**：把一处具名退回静默 ⇒ 必须见对应红的控制例（否则具名/兜底章没承重）。

收口信号：`fails[<none>]`（有袋无字）桶应全域归零；今后再出 `<none>` = 新引入缺陷、可直接当红看。

> 具名只是把「哑账」变成「有名拒收」，**不等于该捕** —— 这些枚到底该判否还是留账，仍要走 Step 5 拆半（批次 84 定去向 → 批次 85 才具名，两事正交、勿合并）。

## Step 5 拆半去向 + 外部权威（对**哑账与真缺口都适用**，不是真缺口专属）

**判完类不等于判完去向。**无论 Step 1 归到「哑账」还是「真缺口」，只要走查 bail（不捕），都要用 **libredwg 类表卷归属** 逐枚定去向：判否入 §3 / 留账待去锚化 / 待外部权威。**哑账同样要走这一步**——典型：531 薄形既已判为哑账（Step 4 具名），又要对本批样本拆半：AC1015 两枚判否、AC1018 两枚留账（`docs-ref/` 只准定向 grep，禁整读）：

```bash
# 该类在哪个版本族在册？
grep -c '<CLASS>' docs-ref/libredwg/src/dwg.spec     # R13–R2000 卷
grep -c '<CLASS>' docs-ref/libredwg/src/dwg2.spec    # R2004+ 卷
```

| 卷归属 | 判语 | 去向 |
|---|---|---|
| `dwg.spec` **零条目**、`dwg2.spec` 在册 | 低版本容器里该类是降级工具塞入的**非合规构造**（同批次 52 的 510/511@AC1015 判据） | **判否入 §3**，正解归方向 A 降级路由 |
| 两卷皆在册、但同类高版本样本能闭合、此样本零锚 | 判据实质是**一枚写手特征串**（覆盖 ODA/AutoCAD 写的单元，不覆盖 ZWCAD 写的）⇒ 收它须**去锚化** = 判据放宽 | **留在账**，**须用户点名**才动；先找同图高版本档的已闭合读数当对齐靶 |
| libredwg 只到 `// ??`（无字段表） | 借不了字段序；ODA 直导 DXF 能给出具名组码（**外部权威存在但无成文布局**） | 判「**未定谱·待外部权威**」，**一位不落**，写三条死路进账防重跑 |

## Step 6 位级自骗面清单（量谱前逐条对照，均实测踩过）

- **写手特征锚 ≠ 形制不变量**：§213 的等距锚 `FE FF FF FF` 只覆盖特定写手；ZWCAD 件**零锚 ≠ 形制缺席**，别据此判「无此类」。
- **零值命中不算证据**：`0.0` 按 RD 位形会**重复命中同一段零位串** ⇒ 真值搜位时零值命中一律作废。
- **前缀式 BD 无位形可搜**：`0.0/1.0` 盘上是前缀式 BD（实测标量段 `41/140/145` 间距恒 **66 位 = 64 + 2 位前缀**），裸 `BD` 连读首枚中、第二枚即失步。
- **派生坐标不是对齐靶**：`10/12/22/110/120` 等大坐标值是 ODA 由块位置 + 相对偏移**算出**的派生量，整帧内 double 与 float 皆不存在，不能当逐枚靶。
- **密文字节勿当 TV/TU 形制**：同一句柄跨 AC1015/AC1018 逐字节异、呈 XOR 规律是解密位形，非串形制；判 TV(ASCII)/TU(UTF-16) 前先看 `UnderlyingData()` 交回的是否已解密。

## Step 7 收尾（防并行手 + 唯一前缀）

- **并行手会删工作副本**（实测：对照件 `add_2004.dwg` 被别的 lane 从主树删掉）⇒ 换**同形对照件**复测，并把「这一枚未复测」写进账，**勿静默略过**。
- **清 scratch 用唯一前缀**：`rm -rf out/_<name>*` 会连带删掉并行手撞前缀的临时件（实测误删过）⇒ 用 `<mode>-<pid>` 之类唯一名，或只删自己列出的字面路径。
- 立账：新缺口行按 §3/§2/§4/待外部权威 四类写去向表；台账更新后跑恒等门 + `gate.py`（零 `src/` 改动的手，门读数应与 base 逐格一致 ⇒ 无回归）。

## 三分类速判（一句话版）

- 不在普查面上 / 已落 §3§2 ⇒ **口径外**。
- 有反射可见的袋 + 在射程 + 早退在建袋行之前 ⇒ **哑账**，走具名化。
- `Canonical` 未登记 → `default:` → 整帧 Opaque、`slots` 空、`opa>0` ⇒ **真缺口**，走 libredwg 卷归属拆半。
- 用 DvWalk 复现前先确认选取器用 `LogicalType` 不是 `TypeCode`，并逐档对齐 census 枚数。