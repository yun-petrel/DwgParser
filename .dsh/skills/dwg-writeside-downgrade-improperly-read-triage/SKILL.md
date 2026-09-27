---
name: dwg-writeside-downgrade-improperly-read-triage
description: "Write-side cross-version downgrade improperly-read triage for DwgParser. Use when ODA reports 'Object improperly read' on a downgrade leg (AC1018/AC1021 to AC1015, R2010+ to AC1018) though the empty-fixture gate cross matrix misses object-level defects; also when the verdict names NO handle (first blocker can be the AcDb:Classes re-encode, e.g. BS-vs-BL version fields); also the CRASH state (zero output after retry, resolved by three donors). Also for off-gate target picking (chainwalk.read_verdict re-measure, native same-leg decidability), for a rebuilt frame LONGER than its source (xdic-slot four-tuple pins cfDataEnd/cfEnd to source BodyEnd; the knife must self-emit its handle stream), and for re-walking the family reader before declaring a bit unspecified. Covers isolated worktree, red-first body-vs-handles discrimination, the --objects bytes= frame ruler, DWGDBG=1 firing traces, gate-expression drift inside one family, '&&' chains that skip -t:Rebuild, IDVAR ablation, pristine-HEAD diffing."
argument-hint: "Specify the class and legs, e.g. IMAGEDEF 543 AC1018→AC1015 improperly-read"
user-invocable: true
---

# 写侧跨版本降版 improperly-read 缺陷定位与修复验证

## Overview

写侧降版支在通用搬运路上漏补 xdic 槽 `(3,0)` 或数据区形制错误 ⇒ ODA 判 `Object improperly read`。gate 的 cross 矩阵用 `empty_*.dwg`（0 实体）夹具，**结构上测不到任何对象级缺陷** ⇒ 判据只能落在 unit 回归档。本 skill 给出从「隔离取证」到「消融控制」再到「全量回归」的完整流水线；**下刀前先分两层**：判词具名 ⇒ 帧层（body/handles 二分）；不具名 ⇒ 可能连对象流都没读到，首拦路可在节层（Step 1.5）。

> 前置：遵守项目 `AGENTS.md` 硬约束。本文所有行号/文件路径/格数都是当轮快照，引用前现场确认，勿照抄成断言。

## Step 0 隔离工作树（主树必污染）

主树通常被并行 lane 改动数十~百+文件；`WriteObject.cs` 是 §443 热档，随时在动。**绝不在污染树 `--bless`**。

```bash
# 开一棵 detached HEAD 隔离树（前缀与 lane 关联，避免撞名）
git worktree add out/_<nn> HEAD --detach
# 后续所有 build / test / 探针都在这棵树里跑
cd out/_<nn>
python ../tools/winbuild.py dotnet build DwgParser.sln -t:Rebuild
```

## Step 1 签名核验（确认语料档确实是目标版本）

```python
# 一次性 Python 探针，读 DWG 头 bytes 6-22 解版本签名
import struct, pathlib
for f in pathlib.Path('新增图纸').glob('add_*.dwg'):
    sig = f.read_bytes()[6:12].split(b'\x00')[0].decode('ascii', errors='replace')
    print(f.name, sig)  # AC1015 → 'AC1015', AC1018 → 'AC1018' …
```

## Step 1.4 门外选靶（门内已无格可翻时，靶在门外；立项前先做四项否证）

`gate.py` 只覆盖在册语料。门外批量互认走 `tools/oda-compare/dwg_matrix.py <目录…> [--targets AC1018,AC1021]`，
产物与 ODA 输出落 `out/dwg_matrix/`（gitignored，`oda_<tag>` 每格一个目录）。**取哪一格当靶，先否证四件事**：

| 否证 | 做法 | 实测形状 |
|---|---|---|
| ① 门外重扫有没有**具名**判词 | 逐格取 `.err`，按 Step 1.5 分具名 / 不具名 / CRASH | 数百格重扫可以**零判据性 REJECT**；两枚 CRASH 复跑换格即 D46（没跑成 ≠ 红） |
| ② 红的是不是**陈旧夹具** | 同一源用**当前构建**现转目标档再跑一遍 | 172,384 B 夹具四档具名拒收，而现转的 39,616 B 五档 **5/5 OK** ⇒ 红在「读不回自家历史产物」，不是活缺陷 |
| ③ 打印的 OK 是不是**假绿** | 见下面那三条尺子 | 某集 115/115 里有 5 枚 `AC1015→AC1021` = 4,448 B **全零头**桩，同目录躺着 `.err` |
| ④ 这一腿**可判**吗 | 喂 ODA **原生**同腿（源直转目标档）后再归因 | 三枚夹具直转 AC1015 全绿（97,805 / 95,872 / 116,143 B）⇒ 腿可判，红才归我方 |

③ 的尺子**不另立一套** = `chainwalk.read_verdict(outd, ver)` 三条：**有 `.err` ⇒ REJECT**（报错时照样出件）、
**产物头六字节 ≠ 目标魔数 ⇒ REJECT**（零头桩）、**两者皆无 ⇒ NO_OUTPUT = 门没跑成**。`dwg_matrix.py` 与
`inter_bisect.py` 已委托它（§598/§600 立的）；**手搓循环扫 scratch 或读旧日志时必须自己套这三条**，并现场核
该器械此刻是否还在用「目录里有 `.dwg` 即绿」——同一弱判据在门 / `richverify` / `chainwalk` / `dwg_matrix`
四路上各抄过一份，改一处不等于四处都改。

> 把「门外全绿」当结论之前先问第 ③ 条。反过来，**把这种假绿升成判据**（改 `_dir_verdict` 一类口径）属**口径级**、
> 须用户裁定；本类一手只交底、不改判定。

## Step 1.5 判词先分**四态**（具名 / 不具名 / 没跑成 / CRASH）⇒ 再决定用哪张判定表

ODA 的 `.err` 有两种形态，起手必须先分：

```bash
grep -m1 -o 'Object improperly read: <[^>]*> ([0-9A-Fa-f]*)' <outd>/*.err   # 命中 = 具名
```

| 形态 | 含义 | 通路 |
|---|---|---|
| `Object improperly read: <AcDbDataLink> (8E99)` | 病在对象帧，且可枚级走链 | Step 2 判定表 + 逐枚 drop |
| 裸 `Object improperly read`（无 `<类>(句柄)`） | 容器读不下去、**走查落点未定**，首拦路可在 `AcDb:Classes` 节层 | 先做层级分叉，**不得**直接套 Step 2 |

层级分叉的机械规格（`DROPCLS`/`DROPH` 整类不 emit + 节级 `swapsect` 须带「同一器械作用在已翻绿那一腿上仍 OK」的对照轮）
以 `dwg-minimal-fatal-set-bisection` 的 **Step 1.5 写侧消融**为唯一落点，本档不重抄；本 skill 只收「测出是节层写侧形制错」
之后**怎么改码**。

### 已定谱的一型 = R2004 类记录尾两枚 version 发成 `BS`

- **症状**：`R2000ClassTable.BuildR2004ClassesSection` 逐类记录尾发 `[BL nInst][BS dwg_ver][BS maint_ver][BL u1][BL u2]`，
  而原生 AC1018 那两枚按 **`BL`** 落盘 ⇒ 帧长差 16 位/枚 × 类记录数，ODA 判裸不具名。
- **刀**（+2/-2）：`WriteBS((short)c.DwgVer)` / `WriteBS((short)c.MaintVer)` → `WriteBL(c.DwgVer)` / `WriteBL(c.MaintVer)`
  （快照坐标 `R2000ClassTable.cs:329-330`，行号会漂，认函数名）。
- **三条定性腿**（缺一就别落码）：① 同文件姊妹构造器 `WriteR2007ClassStreams` 早发 `BL` ⇒ 改前本支是全文件唯一发 `BS` 的；
  ② 本仓**读侧早已定谱**（`TryParseR2004Classes` 拿 `versionsAsBl` 试两版式 = 读侧已改、写侧漏改）；
  ③ 真值锚取**原生**（`sample_2004`），**不取 oracle** ⇒ ODA 直转的 oracle 在该位已归零，三方在此位**不可判**。
- **正证形制**：装刀后 ODA 判词在**零嫁接**下由不具名变具名 `<AcDbDataLink>(8E99)`。§526 那句「单换一节让不具名变具名
  只是诊断杠杆」仍成立，但**改的是我方写侧自己那一处** ⇒ 杠杆升为正证，不必再靠换节自证。
- **绿腿为何免疫**（= 门为何瞎）：`BS(0)` 与 `BL(0)` 共用短码 `BB=10`、编码逐位相同，而 R13–R2000 源类记录压根没这两枚
  字段（解析得 0）⇒ `AC1015→AC1018` 产物改前改后**逐字节相同**，只有「源 ≥AC1021 ∧ 目标 AC1018」落进分叉
  ⇒ `cross` A/B 与 `recopy` 全绿也判不出来（与 D84/§557 同型：**极简/低版本源语料结构性看不见这一族**）。
- **装刀后必跑三件**：自家读者回读类节仍解出全部类（实测 75）；绿腿产物与基线树**逐字节**对拍；全量 `dotnet test`
  红集合与纯净基线树**逐名相同**（实测均 7 红）⇒ 才判「非口径级改动」，无须用户裁定。

> 本型读数 = 树 `out/_h559` @`72a3157a`、落档 `862c4f5d`（刀以 `.patch` 存档、未落 `src/`）。
> 快照读数，引用前现场重跑。

### CRASH 态：崩不等于「刀写坏了」

ODA 落判时其实是**四态**，起手先分清楚（别把后两态混成一态）：

| 输出目录 | 态 | 读法 |
|---|---|---|
| 有 `.dwg` | OK | 翻格 |
| 只有 `.err` | REJECT | 具名 / 不具名两路（上面那张表） |
| 空目录、**未复现** | NO_OUTPUT = 门没跑成 | D46；同环节跑 `HEAD` 版对照再归因 |
| 空目录、**重试仍空** | CRASH | 本节 |

> **`OK` 那一格也要复量**：判据 = 产物头六字节 **==** 目标魔数 **且** 零 `.err`。ODA 在 `writeFile` 中途抛错时
> 照样落盘一枚**全零头桩**，且 `.err` 与 `.dwg` **并存**（实测 4,448 B 与 164,928 B 两型）⇒ 只看「有产物」会把
> 这类格子读成翻格，链条台账会在第 0 步报「★链闭合（0 步）」、`inter_bisect` 会把二分每一半都判成无罪。
> 尺子 = Step 1.4 ③ 那三条（`chainwalk.read_verdict`）。

崩**多半是外因**（并行 lane 撞 `ODA_SINGLETON` 单实例排斥、看门狗超时 ⇒ 一次挂起被结束成零输出），所以「崩」字面不可信，必须先过五件套才升成判据：

| 判据（五件套） | 实测形状 |
|---|---|
| ① 嫁入帧**无可争议正确** = 三供体互证 | 原生目标件 / oracle / 我方**已判绿产物**同句柄帧，三个互不相同的帧各嫁一次**一律崩** |
| ② 同基座 `bypass all` | OK（容器 / 节层出局） |
| ③ `^h` 只留我方坏帧 | **干净具名 REJECT** ⇒ 崩不由我方那帧字节引发 |
| ④ 单换更大的帧 | 80 KB 位宽表帧不崩 ⇒ 崩与帧大小无关 |
| ⑤ 崩**可复现** | 重试 + 私有 ODA 副本；先排掉单实例排斥 / 超时造的假零输出 |

③ 或 ⑤ 任一缺 ⇒ 当**不可判**，回落 `dwg-minimal-fatal-set-bisection`，别在此落码。

**据此禁止两件事**（都是本型实测推翻的既有结论）：

- **把 REJECT→CRASH 读成「我刀写错」**：改前那格本就 REJECT，嫁入正确帧反而崩 ⇒ 崩点对该帧字节**没有因果性**（= §563「长度对≠内容对」的误判形状；该推论同不被本型支持）。
- **据「drop 任一枚即不崩」给该族定罪**：drop 剪的是**引用图**（条目与引用一并消失），不是那一帧。

**正确出口 = 换尺子，不是改码**：跑 `ProbeTool objsize`（全枚逐行「句柄 / 类号 / 帧字节 / HasHS」，补的正是 `widths` 的 HasHS 盲区）做**四侧逐类枚数对照** —— 四侧 = 目标档原生 / 我方绿产物 / 我方红产物 / 源。判形：

```text
目标原生 0 枚 · 我方绿产物 0 枚 · 红产物照源发 N 枚   ⇒ 「源独有整族」
```

（实测 `AcDbMLeaderObjectContextData` #567 在 AC1018 上 = 0 / 0 / 10。）此时**该族到底该不该出现在目标档**才是问题本身，不是那 N 枚帧的字节怎么改。并记一条供体法的盲区：**供体 / ddmin 对「源独有整族」结构性失明**（供体自身也带着它 ⇒ 嫁接换不掉；ddmin 只能靠抹指向它的字典条目来消崩）⇒「最小致命集里没有它」**绝不等于**它无罪。

- 判「不该进」⇒ 该族不 emit + 同步摘指向它的字典条目 + 重算条目计数（并处理句柄流 code-2 引用与相对位移，§524 同族坑）= **丢对象** ⇒ 口径级、**须用户裁定**，本手只交底不下刀。
- 判「该留」⇒ 回 Step 3 的「形制不合」路线做三侧逐字段重定谱。

> 两把独立尺指向同族 = 互相加固，不是重复劳动：本型由 oracle 白名单二分（收到单枚句柄）与四侧枚数对照（得同族十枚）各自收敛，**共用的判据是「原生目标档该类 0 枚」**；别因别道尺子不同就推翻自己读数。
> 本型读数 = 干净隔离树 `out/_r571` @`ec0cef79`、落档 `56aea999`/`f4c401fb`/`8b3a2a72`（崩象后由容器侧 `c09ce122` 解除）。快照读数，引用前现场重跑。

### 具名之后：链只走「致命句柄」那一列

`.err` 里 **warning 与抛点不同级**：`ODA Warning: Object improperly read` + 下一行 `AcDbLayout(6C)` 是 warning，
末尾 `OdError thrown during readFile … Object improperly read: <AcDbDataLink> (8E99)` 才是致命。判别 = **把 warning
那几枚一并 drop 后看下一个具名句变不变**（实测 `dropDL` 与 `dropDL_lay` 的致命句同为 `<AcDbTableContent>(90EA)`
⇒ `6C/6D` 当时不在致命链上）。逐枚 drop 走链到「REJECT 但不具名」即止，那说明再往前已换出容器可解析面、
剩下的只能真修（drop 单枚会打断 owner 向量 ⇒ 链长**不能**用 drop 数）。

## Step 2 红-先锁：body vs handles 二分判定（核心决策点）

**前提 = `.err` 已给出具名句柄**（不具名先回 Step 1.5，本节两把探针在不具名态下拿不到靶）。

**关键问题**：缺陷在数据区（body）还是句柄流（handles）？修法完全不同。

写两条一次性 xUnit 探针（放同棵隔离树的 tests/ 里即可）：

### 2a. DataBody 逐字节比对

```csharp
// 加载源 → 转目标 → 回读 → 与原生目标同句柄对象的字段体比
// 字段体 = [BodyStart*8+RL, HandleStreamStartBit)
var nat = DataBody(NativeTargetPath, handle);
var ours = DataBody(ConvertedPath, handle);
Assert.Equal(Convert.ToHexString(nat), Convert.ToHexString(ours));
```

### 2b. 句柄流 code 序列比对（复用 DvWalk 解码器模式）

**勿手搓解析器**。用 `tools/DvWalk/Program.cs` 里已验证的 RC 解码逻辑：

```csharp
// 解码伪码（实测复用形）：
// RC → sz = rc & 0xF → 读 sz 字节大端 → 得 (code=rc>>4, val)
// 遇 rc == 0x00 停（空引用 = 句柄流终止符）
var hr = new BitReader(sourceData);
hr.SetPosition((int)(hsBit >> 3), (int)(hsBit & 7));
while (hr.BitPosition + 8 <= endBit)
{
    byte rc = hr.ReadRC(); int sz = rc & 0xF;
    if (hr.BitPosition + sz * 8 > endBit) break;
    ulong v = 0; for (int i = 0; i < sz; i++) v = (v << 8) | hr.ReadRC();
    sb.Append($"{rc >> 4}:{v:X} ");
    if (rc == 0) break;
}
```

### 2c. 三面帧长对照 = 先于逐字段的廉价尺（一把尺先立住，能省掉整轮二分）

写探针之前先只量**帧字节**：`DwgParser.Cli <文件> --objects` 打印 `h= t= bytes= v=`（`bytes = BodyEnd - BodyStart`）。
对同一枚句柄取三面并排：

| 三面 | 取法 |
|---|---|
| **源** | 源文件跑 `--objects` |
| **oracle** | ODA 直转目标档那件跑 `--objects` |
| **我方另一目标绿腿** | 同一张源、同一条路上**已判 OK** 的邻近目标档产物跑 `--objects` |

| 读数形状 | 指向 | 动作 |
|---|---|---|
| 我方帧 **> 源帧**（oracle 同枚只有零头宽） | **装配坐标混用**，不是字段内容错 | 跳过 2a/2b，直接看 Step 4「四元组坐标混用 ⇒ 刀自出句柄流」 |
| 帧比 oracle 短一截、句柄流枚数退回源形 | 同上（走查窗按源位算 ⇒ 恒空） | 同上 |
| 帧宽与 oracle 同量级却仍红 | 病在内容/次序 | 回 2a/2b，走 Step 2 判定表 |

三面必须**同一把尺**：帧长有两种口径（`bitdiff` 的 RL 含 10 位对象类型码，记录面 `HS−Carry` 不含，`--objects`
走的是 `BodyEnd-BodyStart`）⇒ 换口径就换尺，别把两种数混进一张表比。

（本型实测：同枚 `TABLESTYLE`(504) 我方 AC1018 绿腿 235 B、oracle AC1015 253 B，而装通用四元组的 AC1015 产物
= **1,145 B**，比源还长、句柄流从自出 6 枚退成源的 32 枚 ⇒ 三面一排即定到装配层。）

### 判定表

| body | handles | 根因 | 修法 |
|---|---|---|---|
| ✓ 绿 | ✗ 缺 `(3,0)` | 句柄流未补 xdic 硬主槽 | handles-only 支（轻量） |
| ✗ 红 | ✗ | body 形制错误（集中串未回内联 / is_xdic 未剥 / RL 双算） | 整帧接管重算（重量） |
| ✗ 红 | ✓ | body 有错但句柄流恰好通用路已补 | 只修 body |

**实测教训**：IMAGEDEF AC1018→AC1015 最初规格假设「需加支 + 集中串 re-inline」，red-first 证明 body 已正确、只需补句柄流 ⇒ 预期方法与更优实际方法出现偏差，必须听证据。

## Step 3 DWGDBG=1 定「通用路为何没补」

```bash
DWGDBG=1 python ../tools/winbuild.py dotnet test DwgParser.sln --no-build \
  --logger "console;verbosity=detailed" --filter "FullyQualifiedName~<TestClass>"
```

PATCH 行输出字段含义：

| 字段 | 含义 |
|---|---|
| `nr` | num_reactors |
| `xm` | is_xdic_missing |
| `xdicOp` | -1=删 / +1=插 / 0=不动 |
| `dataEnd` | RL 反推的句柄流起点 |
| `hsEnd` | 句柄流终止位 |
| `xbLoc` | LocateHandleRef 定位结果（-1 = 定位失败） |
| `split` | 是否走句柄流拆分 |

**本类常见根因**：pre-R2010 记录的 `dataEnd == hsEnd` ⇒ 扫描窗 `[dataEnd, hsEnd)` 为空 ⇒ 通用路跳过补槽。`hsEnd = BodyEnd*8` 兜底只对 R2010+ 源生效（`IsR2010Plus()` 守卫）。

### firing 痕：先分「刀没跑」与「刀跑了、形制不合」

上面那条「守卫过小 ⇒ 静默回落通用搬运」**不是唯一一型**，反向情形同图实测存在。每把刀自带 firing 痕
（走查类与手术支入口各一行）：

```text
TC531-DOWN h=90EA rl=19368 nreact=0 xdic=True ss=[3265807,3278775) walk=3265807 hs=72
DL18DN h=8E99 t=565 AC1021→AC1018 串5 xm=1 剥串区1843位 body=1113 hs=104
```

| DWGDBG 输出 | 闸条件里的目标档 | 定性 | 动作 |
|---|---|---|---|
| **无**该枚痕 | 不含本腿目标档 | 刀没跑（静默回落通用搬运） | 补那一档支，或按既有坑校过小守卫 |
| **有**痕且 `walk ==` 串区起点（走查自洽闭合） | 已含本腿目标档 | **刀在跑、形制不合** | **禁止放宽闸**；去逐字段对形制/发射次序 |
| **无从谈起**：嫁入无可争议正确帧后 ODA 零输出且重试仍空（CRASH），而 `^h` 仍干净 REJECT | 与本帧无关 | 崩**不在闸也不在本帧**，在下游引用图（常见 = 源独有整族） | 先过 Step 1.5「CRASH 态」五件套 + 四侧 `objsize` 枚数对照；**不查闸、不改码** |

> 实测反例（差点白放宽一刀）：`TC531-DOWN h=90EA rl=19368 … walk=3265807` 已 firing、且
> `CarriesTableContentR2007Down` 的闸本已含 AC1018 ⇒ 病在「哪些格内容进哪一枚」的归属轴，不在闸。
> 该型若照旧坑去放宽闸，只会扩大射程、不翻格。
> 另：若闸本身带 `GATE` 痕（形如 `… okSrc=False okTgt=False`），它打印的是**实际读到的源/目标档**，
> 与文件头签名不符时先查调用侧传参，别改闸常量。

### 自校验「恰差 N 位」收不齐 ⇒ 先把同族**读侧**走查逐行对过去，再谈「未定谱」

写侧刀常自己重走公共头（`own → EED → BL nr → B is_xdic_missing → …`）。这条序列**本仓读侧早已逐行命名在消费**，
例如 `Objects/Records/TableStyleAcR2010Plus.cs` 的 `if (SrcVersion >= AC1027) br.ReadB(); // has_ds_data`。
写侧漏抄其中一读 ⇒ 游标比登记锚短 N 位 ⇒ 自校验弹刀、回落 verbatim ⇒ **看起来像**「该位未定谱」。

**三步序，别跳步**：

1. 按同族 grep 读侧那一段的 `ReadB()` / `ReadBL()` 带注释行，与写侧重走序列**逐行对齐**（两分钟的事）。
2. 用**三档源位差单调性**验：如 `AC1024 闭合 / ≥AC1027 恰差 1 位`（实测 72455/72455、90127/90128、64911/64912）
   ⇒ 单调 = 有一位**成文且在目标档不存在**，不是谱不明。
3. 两条都不符才判未定谱、才开三供体的活。

目标档无该字段 ⇒ **只消费、不重发**（读侧 `br.ReadB()`，写侧不写）。补那一读之后同腿当场 `REJECT → OK`。

> 本仓实测代价：同一把刀曾把 `has_ds_data` 记成「恰差 1 位、按 §156 不猜」，并给下一手开了**三供体逐位定谱**的账。
> 那张账单是假的。**写「未定谱」三个字之前先 grep 读侧**；把已定谱的位记成未定谱 = 给下一手留一张假取证账单。

## Step 4 修法形制

### handles-only（body 已绿时）

在类专属 `XxxDown.cs`（非热档）加：

```csharp
// 窄闸：目标 == AC1015 ∧ 源 < AC1021 ∧ 类码 == NNN
internal static bool CarriesXxxAc1015FromPreR2004(rec, t) => ...

// 手术体：
// 1. 数据区：跳过源内联 RL → dataBegin = start + 32
//    （同 §474 剥法；否则 RL 双算毁帧）
// 2. 句柄流：CopyBits(owner + reactors×nr) + WriteHandleRef(3, 0)
```

`WriteObject.cs`（热档）只加一行 `else-if`：

```csharp
else if (CarriesXxxAc1015FromPreR2004(rec, t)
         && CarryXxxAc1015FromPreR2004(body, rec, src, out var xxxHandles))
    mlsHandles = xxxHandles;
```

### 整帧接管（body 也红时）

需自建 BitWriter、逐字段走查后重写。数据区**必须剥源内联 RL**：`dataBegin = start + 32`（同 §474），否则双 RL 令帧膨胀、回读错位。

### 四元组坐标混用（帧比源还长时）⇒ 刀自出句柄流

**机理**：通用补槽走 `BuildXdicSlotHandleStream(handles, rec, src, t, cfDataEnd, cfXdicBit, cfEnd, cfXdicOp)`，
那四个 `cf*` 全是**源位流的绝对位**（`cfEnd` 甚至直接钉成源侧 `rec.BodyEnd * 8`）；而装配第 3 步取
`long bodyEndBit = splitHandles ? cfDataEnd : start + len` —— 那是**目标帧**的终点。一旦本支走「重建短帧
∧ `splitHandles = true`」，短帧就被按**源**的句柄流长补回去 ⇒ 产物比源还胖、句柄流退回源形（实测同一枚富形
1,145 B、薄形源 1,206 B，而 oracle 只有 253 B；句柄流从自出 6 枚退成源的 32 枚）。
**这不是字段内容错** ⇒ 改形制、放宽闸、逐字段对表都不会翻（Step 2c 三面一排就该定到这里）。

**出口 = 刀自己出整条句柄流**（§474 `IMAGEDEF`(543)→AC1015 同范，见 `DwgObjectStreamWriter.ImageDefDown.cs`）：
手术体自建 `BitWriter`，经 `WriteObject.cs` 那行派发的 `out …Handles` 槽交给装配（形如 `mlsHandles = xxxHandles`），
并把 `cfDataEnd = -1` = 声明「本枚不走通用四元组」。自出形制三段 = ① 源公共段按位搬（`owner + reactors×nr`）
② `HandleStreamHasXdicSlot` 自检后补 `0x30`（pre-R2004 目标恒需 xdic 硬主槽）③ 表记录引用按**源序**重发
（504 = 3 枚 `[5] tsty`，序 `_TITLE←行1 / _HEADER←行2 / _DATA←行0`，与 `HandleStream.cs` 的重建支逐枚同形）。

**三条硬约束**（缺一即扩大射程 / 撞红已绿各格）：

- **分叉依据 = 「源那一枚 xdic 槽在不在」，不是「富形还是薄形」**：只在**源 xdic 缺席**那一支长出（缺席时通用四元组
  本来就插不出东西）；源带 xdic 仍走原路 ⇒ 已绿各格**逐字节不变**。
- **别顺手放宽走查窗**：`while (hp + 8 <= cfEnd)` 那类条件是**源**窗，短帧下恒空。症状 = PATCH 痕报了
  `xdicOp=1 / xm=False` 却没长出那 8 位 ⇒ 正解是自出，不是把窗调大。
- **要删通用四元组那条支先 grep 消费者**：本型删得动，是因为唯一消费者就是这一路，且 `xdicMiss = False` 时它本就
  `return` 零副作用（= 已绿各格走的是那条不存在的支 ⇒ 门读数不动）。消费者不止一个时不得删。

## Step 5 IDVAR 消融控制轮

修复支入口必须挂消融开关：

```csharp
private static int IdVar() =>
    int.TryParse(Environment.GetEnvironmentVariable("IDVAR"), out var v) ? v : 1;
// 手术体首行：if (IdVar() == 0) return false;
```

**控制轮验证**：
```bash
# 支关 → 该腿必须红（否则消融开关没承重 / 探针本身有问题）
IDVAR=0 dotnet test --filter "FullyQualifiedName~<TestClass>"
# 支开 → 该腿必须绿
IDVAR=1 dotnet test --filter "FullyQualifiedName~<TestClass>"
```

任一对不上 ⇒ 控制实验失败，不可继续。

## Step 6 全量回归 + pristine-HEAD 基线 diff

```bash
# 带支全量
python ../tools/winbuild.py dotnet test DwgParser.sln --no-build 2>&1 | grep -E '^  失败 ' | sort > /tmp/fail_with.txt

# 另开一棵纯净 HEAD 基线树（无本次改动）
git worktree add out/_<nn>_base HEAD~1 --detach  # 或 base commit sha
# 跑基线全量
python ../../tools/winbuild.py dotnet test DwgParser.sln 2>&1 | grep -E '^  失败 ' | sort > /tmp/fail_base.txt

# 集合差 = 本次引入的回归（期望为空）
comm -23 /tmp/fail_with.txt /tmp/fail_base.txt
```

非空 ⇒ 逐条归因（语料清退 / 本手引入 / 并行手交叉）。

> **装刀 / 撤刀 / 复原三段各自单独跑 `-t:Rebuild`**，不要和取数串成一条 `&&` 链：`grep` 零命中时退出码 1，
> 会把后面的 Rebuild **整条静默吞掉** ⇒ 拿旧 dll 取判据、造出假 N 红（另两枚同族坑见 Pitfalls 末三行）。

## Step 7 ODA 发现路径（避坑）

```python
# 正确路径：
import sys; sys.path.insert(0, 'tools'); import oda_path; print(oda_path.find_oda())
# ❌ 错误：sys.path.insert(0,'tools/oda-compare'); import oda_path  ← ModuleNotFoundError
```

ODA 二进制位于 `tools/ODAFileConverter/ODAFileConverter.exe`（Windows 仓内唯一合法源）。容器侧走 `bash tools/setup-container-env.sh oda`。

## Pitfalls（均实测踩过）

| 坑 | 症状 | 正解 |
|---|---|---|
| gate cross 用 empty 夹具 | 对象级缺陷永远绿 | 判据只能落 unit 回归档；门内无格可翻时去门外选靶（Step 1.4） |
| 手搓句柄解析器 | desync / 越界 / 解出不存在的 code | 复用 DvWalk 已验证的 RC 解码模式 |
| 不剥源内联 RL | 帧膨胀、回读整体错位 | `dataBegin = start + 32` |
| 在污染主树 `--bless` | 覆盖并行手的基线 ⇒ 口径变更未裁定 | **绝不**；重钉属口径变更须用户裁定 |
| `oda_path.py` 导入路径错 | `ModuleNotFoundError` | `sys.path.insert(0,'tools')` 而非 `tools/oda-compare` |
| body 已绿时仍整帧接管 | 多改多错、回归面扩大 | 先证 body 再决定修法（Step 2 判定表） |
| 降版支里写死的过小守卫（如 `num_reactors > 8`） | 高引用数的合法表记录（DIMSTYLE nr=10）被**静默拒** ⇒ 回落通用搬运 ⇒ ODA improperly-read | 上限**对齐同族兄弟支**（§439/R2010Ac1015 用 `> 1024`）；守卫只为挡走查垃圾，真正确性由后续 `bitPosition != nameStart` 类对齐判据兜 |
| **把上一条当通用 reflex**（反向情形同图实测） | 闸其实已含本腿目标档、刀**已经 firing**，照旧坑去放宽 ⇒ 白一刀、射程还扩大 | 先 `DWGDBG=1` 取 firing 痕分家（Step 3 末表）：有痕 = 病在**形制**，不在闸 |
| 一格里多个对象各自 improperly-read | 修了 A 类（如 IMAGEDEF 783C，ODA 其实容忍）却仍见 ODA REJECT——真红点早挂在 B 类（DIMSTYLE 27） | **单列 `.err` 里全部具名句柄**（正则抓 `<AcDb…>(hh)` 去重），并分 **warning vs 抛点**（`ODA Warning:` 不致命、`OdError … : <类>(句柄)` 才致命，见 Step 1.5 末）；别只看 `_dir_verdict` 的「最具信息量一行」 |
| 降版闸写宽于实证面 | 同刀放宽成 `源 >= AC1021` ⇒ `sample_2010\|2013\|2018` 三腿 ODA **跑崩**（既不出 `.dwg` 也不出 `.err`） | 作用域**只写实证面**（`Hatch.cs` 头注同款纪律）：收到「源恰 AC1021」即恢复；支方法名同步改名（`CarryDataLinkAc1021ToAc1018`）不让名 overclaim |
| 把空目录读成红 | 脚本把「非 OK」一律印 `REJECT` ⇒ 三条 **NO_OUTPUT（门没跑成）**被当判据 | 四态读：有 `.dwg`=OK / 只有 `.err`=REJECT / **空目录=没跑成**（D46）/ **复现过的零输出=CRASH**（Step 1.5 CRASH 态）；且「没跑成」可能是**自己造的** ⇒ 同环节跑 `HEAD` 版对照再归因 |
| 同一处形制差跨源档平移 | AC1021 源该枚**长 111 B**（273 vs 原生 162），R2010+ 源同枚**短 119 B**（43 vs 162）⇒ 方向相反 | 一档一账：R2010+ 那三档另做三侧对表，**不得**按「AC1021 + 多一位」猜 |
| `ProbeTool widths` 的 `HasHS` 盲区 | `widths` 只统计 `HasHS=True` ⇒ 句柄流为空的类实例整个不进账（实测 `h=8E99 HSStartBit=0 HasHS=F` 的 DATALINK 从初见起就不在逐类位宽账上，直到类节修好 ODA 点名才浮出） | 拿 `widths` 做分族账**先报被跳过的枚数与类号**；同族老坑 = §549「`HandleStreamStartBit` 该类不填 ⇒ 拿它当锚恒过」 |
| 改了 Core 不 Rebuild 工具工程 | probe 报的帧长/`HasHS` 是旧 dll 的数（stale-tool gate），据此再下一刀 | 硬约束 9：`dotnet build DwgParser.sln -t:Rebuild` 后**逐个** Rebuild `ProbeTool`/`DvWalk`/… |
| 消融控制轮跳过 | 支无效但探针假绿 | 必须见「支关 → 该腿红」 |
| 把 REJECT→CRASH 读成「我刀写错」 | 嫁入无可争议正确帧后由 REJECT 变崩 ⇒ 去改本帧形制/改闸，而崩点对该帧字节**无因果性**（§563 误判形状） | 先过 Step 1.5 CRASH 五件套（三供体 + `^h` + 大帧 + 可复现）才可落判 |
| 拿「drop 任一枚即不崩」给某族定罪 | drop 剪的是**引用图**（条目与引用一并消失），不是那一帧 ⇒ 定罪定到错的族 | 定罪换尺子 = 四侧 `objsize` 逐类枚数对照（原生 0 / 绿产物 0 / 红产物 N） |
| 把器械性零输出读成真 CRASH | 并行 lane 撞 `ODA_SINGLETON` 单实例排斥、看门狗超时都出零输出（实测一次 ddmin 中途被别道顶掉） | 重试 + 私有 ODA 副本（`ODA_SINGLETON=off`）复现；不可复现即回「没跑成」那一档，别当判据 |
| 同族多处闸各写一套谓词 | 富形在某一档被**外闸**单独挡在手术之外 ⇒ 手术痕**零枚**（= 刀没咬，不是形制不合），而同族三处（内层手术的 `IsCaptured`、句柄流重建支、本腿既有判据档前提行）早已写 `is { Captured: true } or { DownReady: true }` | 动闸前把该记录族的谓词**全族 grep** 一遍（外闸 / 内层 / 重建支 / 判据档前提 = 四处），不一致即对齐多数派；对齐后痕仍零枚再去查过小守卫 |
| 门外矩阵或手搓扫描按「有 `.dwg` 即绿」 | ODA `writeFile` 中途抛错仍落一枚**全零头桩**、`.err` 与产物并存 ⇒ 链条台账第 0 步报「★链闭合（0 步）」，`inter_bisect` 把二分每一半都判成无罪 | 三条一起判（`chainwalk.read_verdict`，Step 1.4 ③）；换靶前先现场核该器械是否已委托它，别各抄一份；陈旧夹具先现转同源复量再立项 |
| `grep … && dotnet build -t:Rebuild` 串成一条 | grep **零命中 = 退出码 1** ⇒ `&&` 后面整条没跑，拿旧 dll 取判据，造出假 N 红 / 假绿（同族：D11 = `dotnet test -t:Rebuild` 直接 `MSB1008`；D3 = `copy2` 保留源 mtime ⇒ 增量跳过重编） | 装刀 / 撤刀 / 复原**三段各自单独** `-t:Rebuild`，别与取数串 `&&`；复原用 `cp + touch`；判据只认**例数**与预期对齐（例数没涨 = 没编进去） |

## 与其他 skill 的分工

| 场景 | 用哪个 |
|---|---|
| 读侧 capture-only 轨道、`captured=0/rejected=0` 缺口 | `dwg-census-gap-root-cause-triage` |
| 读侧字段化收口一手 | `dwg-fieldization-capture-only-hand` |
| **写侧降版 improperly-read、gate 盲点定位** | **本 skill** |
| 位流逐字节差异定谱（帧级 diff） | `dwg-bitstream-diff-forensics` |
| 判词**不具名** / 控制档 `bypass all` 也 REJECT ⇒ 枚级链与**写侧消融**（`DROPCLS`/`DROPH` + 节级 `swapsect` 带对照轮） | `dwg-minimal-fatal-set-bisection`（Step 1.5） |
| 红格选靶尺、绿双胞对照、现造 oracle 与 `minverify`/`gate` 基线 | `dwg-direction-a-cell-forensics` |
| 一把还不存在的降版刀怎么先试一刀（隔离树、分钟级裁定） | `dwg-writeside-knife-first-attempt` |
| golden 打红后的语料重钉 | `dwg-golden-rebless` |
