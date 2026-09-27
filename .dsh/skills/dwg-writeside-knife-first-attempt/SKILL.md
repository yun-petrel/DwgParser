---
name: dwg-writeside-knife-first-attempt
name_en: Write-Side Knife First Attempt
name_zh: 写侧新刀首次尝试的可判化流程
description: "First-attempt protocol for a DwgParser write-side cross-version transform knife that does not exist yet (only its opposite upgrade/downgrade sibling is in the tree). The trial stays in an isolated worktree with zero main-tree src changes; the outcome is decided in minutes: prove the gate opens before debugging the body (DWGDBG print of class, normalized class, target, source, kind, matched count == expected), wrap every silent return-false with Bail so a rollback is never read as a wrong form, walk the source frame with real C# bit primitives instead of a Python re-decode, and judge by a per-family bypass criterion (keep only that family of our frames, swap the rest for oracle frames) plus a bypass-all control re-run, not the 12-minute gate. Use when mirroring a sibling knife into its inverse, when a new knife compiles but frames stay at source length, or before writing a 150-line converter. Not for locating the fatal set, reviving a knife that silently falls back, or ablation-verifying a landed knife."
description_en: "新刀不存在（仓内只有对向刀）时第一刀的可判化试手：隔离树零主树 src、先证闸开再证体、逐守卫挂 Bail、真原语走游标、族级旁路判据分钟级定成败、失败即撤只交轨迹。"
description_zh: "写侧「新刀首次尝试」：仓内只有对向刀（升版刀在、降版刀不存在，或反之）时怎么试第一刀。目的不是翻格，而是在分钟级判出「成 / 不成 + 不成在哪一位序」，且可整体撤回、主树 `src/` 零改动。顺序死写 = ① 控制档 `bypass all` 当场 PASS 才开刀；② 先证**闸开**（`DWGDBG=1` + 临时打印 `cls/norm/t/src/kind`，枚数 == 预期）再查体，否则「刀没跑」与「形制错」读数同形；③ 每个静默 `return false` 前挂 `Bail()`；④ 游标走查一律用 C# 真原语，硬判据 = 游标终点 == 数据区末 且 集中流串槽数 == 期望式，**禁止**用 Python 重解句柄/RL（实测假读数）；⑤ 成败用**族级旁路** `bypass <源> <oracle> <出> ^@<该族句柄文件>` + ODA 直读（REJECT→OK 即刀成，分钟级）而不是等整门，每刀复跑 `bypass all` 当「病仍在帧层」哨；⑥ 失败即撤（`git checkout --` 只撤派发件、删刀件、`git status` 证主树无本手在途），只把轨迹 + 判读入档。★ 头号警戒：兄弟刀头注那张字段序若当初验证是「我方写出的目标形闭合到 RL」= 自证循环，真源文件上可能不闭合 ⇒ 动刀前先做一次只读两侧对表。用于照逆兄弟刀、新刀编译过而帧长仍 == 源、或落 150 行变换器之前。不用于定位最小致命集、救已存在却静默回落的刀、或验收要落库的刀（那三件各有专档）。"
argument-hint: 指名类号与两档，如「FIELD 568 AC1021→AC1018 首次试刀（照 FieldUp 取逆）」
argument-hint-zh: 指定类号与两档，如「FIELD 568 AC1021→AC1018 首次试刀」
user-invocable: true
---

# 写侧新刀首次尝试（knife first attempt）

## Overview

TESTMARKER

仓内只有**对向**刀（例如只有 `FieldUp.cs` / `TableContentUp.cs`，缺其逆）时，第一刀的目标不是翻格，而是**在分钟级判出「成 / 不成 + 不成在哪一道守卫、哪一位序」**，且失败能整体撤回、主树 `src/` 零改动。判成之后再交 `dwg-red-first-ablation-verification` 走落库阶梯。

## 边界（先选对工具）

| 你要判的事 | 该用的档 |
|---|---|
| 哪几枚对象才致命、把上千候选收到单枚 | `dwg-minimal-fatal-set-bisection` |
| 已存在的刀**静默回落**（典型 = 漏句柄槽）并要修好落库 | `dwg-writeside-downgrade-improperly-read-triage` |
| 一把要落库的刀：红-first 源码级消融 + 全门 | `dwg-red-first-ablation-verification` |
| **新刀根本不存在，要照逆兄弟刀试第一手** | 本档 |

## Step 0 开刀前置（缺一即不动码）

1. **控制档当场 PASS**：`bypass all`（inter 全换 oracle 帧）= ODA OK，才证明病仍在帧层、帧级刀有意义。不 PASS ⇒ 回二分档的写侧消融那一步，别开刀。
2. **隔离树**（主树必被并行 lane 污染，派发件 `WriteObject.cs` 常驻在动）：
   ```bash
   git worktree add out/_<lane> HEAD --detach
   cd out/_<lane>
   python tools/winbuild.py dotnet build DwgParser.sln -t:Rebuild
   python tools/winbuild.py dotnet build tools/ProbeTool/ProbeTool.csproj -t:Rebuild   # 改过 Core 必逐个 Rebuild
   ```
3. **现场探测**（禁按文档断言）：`dotnet --version`、`python tools/oda_path.py`（打印仓内转换器绝对路径）。
4. **抢锁**：`python tools/progress_lock.py acquire --task "<类> <源>→<目标> 首次试刀" --owner <车道id>`。返回 `BUSY` ⇒ **只交测量、不开刀**（把已量到的读数交给持锁车道，不撞车）。

## Step 1 先证「闸开」，再证体

为什么这个顺序：闸没开与体回退**读数完全相同**（产物帧长仍逐位 == 源）。把「刀没跑」读成「形制错」会去追一整张错的形制表。

- 派发件里加一行临时打印（试完即撤），闸的每个合取子句都要能被看出来：
  ```csharp
  if (DebugTrace) System.Console.Error.WriteLine(
      $"FLDBG cls='{rec.ClassName}' norm={ClassTypeCodes.Normalize(rec.ClassName)} " +
      $"t={t.Version} src={rec.SourceVersion} kind={rec.Kind}");
  ```
- `DebugTrace` 由环境变量 `DWGDBG=1` 赋值 ⇒ 不设 env 就永远静默（该字段历史上曾被声明却从不赋值）。
- 判据 = **命中行数 == 预期枚数**（不是「看见几行」）。
- 判类走 `ClassTypeCodes.Normalize(rec.ClassName)`，**不用 `TypeCode`**：per-file 类号与规范码不同轴 ⇒ 换图必漏判/误判。
- 闸开之后才看帧长：`objfields` 逐枚量我方 / 源 / 原生 / oracle 四侧，我方 == 源即「落了回落」。回落 ≠ 闸关。

## Step 2 每个静默 `return false` 前挂 `Bail()`

- 刀件骨架惯例 = `entry = w.BitPosition` + `try` + 任一处不合即 `w.TrimLastBits(w.BitPosition - entry); return false;`（零外溢，也正是「帧长仍 == 源」的成因）。**每一道**这样的守卫都要留一行，否则只能看见「回退了」看不见「在哪一位序退的」。
  ```csharp
  static void Bail(string why) { if (DebugTrace) System.Console.Error.WriteLine($"FLDBAIL {why}"); }
  ```
- 里程碑也各打一行（游标值本身就是判据）：`own` / `eed start->end` / `nreact` / `xdic` / `posAfterHead` / `childs,objects` / `preScalars` / 每次进联合体的 `@pos` / `afterOuter`。
- 实测两坑：① `Bail` 先定义后用，否则 `CS0103` 整件编不过（在隔离树里也一样白跑一轮）；② 别用 bash heredoc 里套 python `s.replace(..., "...\\n...")` 注点 —— `SyntaxError: unexpected character after line continuation character`，直接 `Edit` 刀件。
- 终止签名要会读：`EX EndOfStreamException ... value type 1342586887` = 游标**撞进 BD 载荷中段**（把载荷中间字节当 BL 类型码），不是「该值类型不支持」。

## Step 3 游标走查用真原语，硬判据 = 闭合

- **不要**用 Python 重解句柄 / RL / TV。同一段字节，自写解码实测产出 `numReactors 4241063942`、`RL=978`、页脚 `data_size=51714 位` 这类假读数并一度把结论带偏；走查一律在 C# 里用真原语：`BitReader`（`ReadBS/ReadRS/ReadRL/ReadBL/ReadB/ReadHandle`）、`CentralStrings.TryDataBits`、`CentralStrings.TryMeasure`、`DwgObjectReader.TrySkipEed`。
- 每枚必见的两条硬判据：① **游标终点 == 数据区末（= 句柄流起点）**；② 集中流串**槽数 == 期望式**（FIELD 例 = `6 + 3·ncv`）。任一不合 ⇒ 位序尚未钉死，**先别写目标形**。
- 帧尾 `[RS data_size][B has_strings]` 的取法照 `CentralStrings.TryMeasure`，别手撕字节序（LE/BE 反就读出 51714 这种数）。
- 探针输出：GBK 控制台里 `print("⇒")` 直接 `UnicodeEncodeError` ⇒ 探针文本走 ASCII 或设 `PYTHONIOENCODING=utf-8`。
- 非 ASCII 串**不放宽**：`EncodeText` 的 GBK 往返自校失败就整枚回滚（薄形码页只有单/双字节语义，UTF-8 回退每 CJK 多 1 字节）⇒ 宁可不合成也不造假形。

## Step 4 判成败：族级旁路，不等整门

整门 12 分钟级；族级判据分钟级，且**结论更强**（把「别的族也红」排除掉了）。

```bash
# 1) 该族全部句柄落文件（一枚一行；ParseHandleList 认 逗号 / 分号 / 空格 / 换行，按 16 进制）
# 2) 只留该族我方帧、其余 inter 换成 oracle 帧
<probe> bypass <源.dwg> <oracle.dwg> out/fam/x.dwg ^@out/fam/h_<类号>.txt
# 3) 产物 ODA 同档直读判词（复用 inter_bisect.oda_verdict / gate._dir_verdict）
```

- **每轮自证规格真生效**（缺一即读数作废）：`BYPASS allowlist=` 与 `hits=` 核 == 预期枚数；产物 sha **必异于** `all` / `none` 两态；空 allowlist 会被护栏拒成 rc=2（历史上它静默退化成 `all`，造出过三次假 PASS）。
- 白名单句柄必须取**源图**句柄（我方产物已重编号）；不确定就先 `BYPASS_PRINT_INTER=1` 列合法集。`chainwalk.py` 在改号图上第一步即 rc=2 ⇒ 此类格只有集合排除可用。
- 判据：该族 **REJECT → OK** ⇔ 这把刀做对了。逐族跑一遍可把「还有谁致命」一并钉死（实测 28 族里只有两族 REJECT，其余 26 族 OK ⇒ 先前追的第三族其实是**级联症状**）。
- ★ `^X` REJECT 只证「X **单独**即致命」，**不证**「修好 X 即翻格」；充分性另做构造测试 = 把已钉的全部致命族一次换成 oracle 帧，应 OK。
- 每落一刀**复跑一次 `bypass all`** —— 最便宜的「病仍在帧层」哨。
- ODA 具名在容器失步时会**漂移**（同一份字节两次分别点名 `AcDbLayout(6C)` 与 `AcDbTableContent(90EA)`）⇒ 归因改用确定性器械（`frameverify` 全帧账自洽、`objfields` 逐句柄四侧尺），判词只当线索。

## Step 5 失败即撤 + 只交轨迹

- 撤：`git checkout -- <派发件>`（**只**列本手碰过的那一个文件），删掉新刀件与其 `using` 行。
- 撤后必查归属，别把别道在途件卷进来：
  ```bash
  git status --porcelain src/ | head
  git diff --stat -- <本手文件>     # 期望为空
  ```
- 入档只写四样：闸开没开 / 回退在哪道守卫 / 逐位轨迹原文 / 两条判读 + 给下一手的起手（含现成的族级判据路径）。**负读数就是有效交付物**，不必先有正读数才配入库。
- 未过落库四件（红-first 判据、源码级消融、每刀复跑控制档、整门逐格位移归因）的 src **一律不留共享树**；深夜尤甚。
- 若环一做完仍**不翻格**（多族共病是常态），预先声明「本手只把具名点前移、不翻格」，不许把前移当翻格报。

## Step 6 照逆兄弟刀之前：先拆掉自证循环

`FieldUp.cs` 头注那张「中形 = 薄形序 − 串槽 − format」在**真实** AC1021 文件上不闭合 —— 它当初的验证是「我方写出的中形闭合到本帧 RL」，即自证循环（真源由 AutoCAD/ODA 写出，序必须独立对一次表）。所以：

1. 先做**只读**两侧对表：拿原生同图该类逐枚走源形与目标形，要求 Step 3 两条硬判据成立；
2. 不闭合的那枚先把位序钉死，再谈动刀；
3. 族级判据当场可用 ⇒ 该族句柄文件先落盘，刀的成没成才有一把不等整门的尺。

## 验证（本档自检）

- 命令面均可在仓内静态核对：`tools/ProbeTool/Program.cs`（`Bypass` 的 `^@` / `@` / 裸句柄三支、`BYPASS FAIL` 护栏、`ParseHandleList`、`BYPASS_PRINT_INTER`）、`tools/oda-compare/inter_bisect.py`（`oda_verdict`）、`tools/oda-compare/gate.py::_dir_verdict`、`tools/oda-compare/oda_wait.py::run_converter`、`tools/oda_path.py`、`tools/progress_lock.py`、`src/DwgParser.Core/Objects/Writer/DwgObjectStreamWriter.cs::DebugTrace`、`src/DwgParser.Core/Objects/Records/CentralValueAcRecords.cs::CentralStrings`。
- 正文出现的类号 / 枚数 / 字节数（`568`、`555`、4 枚、2 枚、28 族、130/277 B）全是**当轮快照**，引用须现场重跑并带树 + 提交号，勿当常数。
