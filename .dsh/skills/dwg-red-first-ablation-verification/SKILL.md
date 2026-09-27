---
name: dwg-red-first-ablation-verification
name_en: Red-First Source-Level Ablation Verification
name_zh: 落码一手的红-first消融验刀
description: Minimal verification ladder for a code-landing hand in DwgParser - prove a red-first criterion by SOURCE-level ablation (back up the knife file, force its guard to `=> false`, rebuild, run the criterion class, record exactly N red / M green, restore, rebuild - refresh mtime on restore, else the incremental build silently reads the ablated dll), because an env kill-switch prefixed to `dotnet test` does not reach the test process (measured 7/7 false green) while the same env works for directly-run CLI/fingerprint processes and for in-test Environment.SetEnvironmentVariable. Strict same-tree dotnet serialization: a second build/test while a test runs (even the implicit build of a `--no-build` test) yields MSB3021/MSB3027 testhost dll locks plus mid-run dll swaps that spray batch false reds indistinguishable from real regressions, and a timed-out wrapper can leave a detached testhost still writing dlls after reporting "completed". Then the zero-ODA substitutes when the full gate cannot run - same-tree `product_fingerprint` OFF-to-ON 66-row A/B, name-initial sharded full suite (A-H / I-Z, named trx, --list-tests reconciliation), and an explicit written statement of whether `gate.py` ran and why not. Use when landing a new write-side knife or gate, when a criterion might be vacuously true, or when asked to prove the knife actually changes product bytes. A criterion that stays green under ablation is usually anchored on a field the reader never fills for that class (§549: `HandleStreamStartBit` is 0 whenever `HasHandleStream=false`) - validate the anchor, and pin one "native ground truth satisfies the same criterion" case, before concluding the knife is irrelevant. Write frame-level criteria as one `InlineData` per handle so the ablation reading names the *same* handle ODA names (§552: 1 red = `0x1DBC` = the `.err` culprit) - that proves knife and disease are the same object, which an aggregate assertion cannot; add neighbouring legs as do-not-move controls. Anchor the criterion on the same dimension the knife moves (frame-level `RL`, not a sub-segment bit length) and re-run the whole ablation ladder after re-anchoring - in §553 a segment-level anchor briefly overturned a correct frame-level conclusion. Also covers the two ways a green gate lies: changing a SHARED encoder primitive (ODA accepts both byte forms while 20+ native-equality goldens go red), and using "byte-equal to the true reference" before proving the true references agree with each other.
description_en: Red-first proof for a landed knife via source-level ablation (guard forced to `=> false`, two builds, exact red/green split), env kill-switch射程, same-tree dotnet serialization (no concurrent build/test - MSB3021/3027 dll locks and mid-run dll swaps spray batch false reds; detached-testhost "false completion"; gate.py --no-build needs rebuilt exes first), zero-ODA fingerprint A/B, sharded full suite, gate-not-run disclosure.
description_zh: 落码类一手的最小验证阶梯，红-first 只认「撤 src 改动」的源码级消融（备份刀件 → 闸改 `=> false` → build → 跑判据档 → 记「恰 N 红 / M 绿」→ 还原 → 再 build）；shell 前缀喂 `dotnet test` 的 env 消融实测不生效（7/7 假绿），env 闸只可用于直跑 CLI 进程与测试自身进程内设值。**同一棵取证树内严禁并发 dotnet 作业**：test 跑着再起 build/test ⇒ MSB3021/3027 dll 锁 + 中途换 dll 吐成片假红；超时包装层可能留 detached testhost 假完成；gate.py --no-build 跑门前须 Rebuild 到位。还原刀件必须顶新鲜 mtime（`cp -p`/`copy2` 带旧戳 ⇒ 增量 build 判「未变」跳过 ⇒ 其后判据档与门读数全挂在消融态 dll 上，且 build 照样打「0 个错误」）。跑不动全门时用同树零 ODA 的 `product_fingerprint` OFF↔ON 恒等面 + 按类名首字母分片的全量套件替代，并在详档显式写明跑没跑 `gate.py` 与理由。消融仍全绿先怀疑判据的锚：锚位点若在该类根本不被填（§549 `HasHandleStream=false` ⇒ `HandleStreamStartBit` 恒 0）判据就是恒过的，另钉一枚「原生真本同判据成立」的正例再谈落刀。帧级判据按**句柄逐枚拆条**（一条 `InlineData` 一枚），消融读数才说得出「红的那枚 = ODA 具名那枚」（§552：1 红 = `0x1DBC`），聚合成一条证不到「刀在治那一枚」；并把邻档同判据钉成不动量控。判据锚要与刀所改的量同量纲（整帧 `RL` 而非某子段位数），换锚后撤刀/装刀两端必须重跑（§553 曾拿段级量具推翻过正确的帧级结论）。用于落新写侧刀/闸、判据可能恒真、同树 dotnet 撞锁/成片假红、或要证明「这把刀真在改字节」的场合；另含两条「门绿仍判错」的实测：改共享编码件的字节形只有全量套件能判、以及「≡ 真本」动手前须先证真本之间一致。
argument-hint: 指本手那把刀的落点文件与判据档名，如「验刀 AttrTailAc1015Down / AttDefTailDowngradeToAc1015Tests」
argument-hint-en: Name the knife file and criterion class, e.g. "verify AttrTailAc1015Down vs AttDefTailDowngradeToAc1015Tests"
argument-hint-zh: 指定刀件与判据档名
user-invocable: true
---

# 落码一手的红-first 消融验刀（red-first ablation verification）

## Overview

任何「新窄刀 / 放闸 / 写侧能力」落库前，必须给出**撤掉本手改动后判据恰转红 N 枚**的读数——
否则判据可能恒真（闸未触发、射程为空、断言被短路都能全绿）。本技能定这条阶梯的合法通路、
env 消融的射程边界、以及全门跑不动时的零 ODA 等价证据。

实例锚（2026-09-23 §502 属性刀 `afe6ab39`，全程干净隔离树 `out/_rich1` @`143d267f`）：
判据档 7 例 ⇒ 源码级撤刀 **恰 3 红 / 4 绿**、还原后 7/7；恒等面 66 行 OFF↔ON **diff 0**；
全量分片 A–H `1367/0/1` + I–Z `2465/0/3`（求和 3836，`--list-tests` 3812），单片 ≈4.4 min。

## 边界（先选对工具）

- 判据值随体重钉 / 语料缺席处置 → `dwg-golden-rebless`。
- 数据区两帧位流定谱 → `dwg-bitstream-diff-forensics`；尾句柄流形制三侧对照 → `dwg-tail-form-audit`。
- 「哪几枚帧致命」的最小集二分 → `dwg-minimal-fatal-set-bisection`。
- 格级 ODA 拒收诊断 → `dwg-direction-a-cell-forensics`。
- **本技能只管一件事**：证明落下去的代码真在改产物字节、且判据不是恒真。

## Step 0 预检（每轮现场跑，勿按文档断言）

```bash
command -v dotnet || ls -d ~/.dotnet 2>/dev/null
tasklist //FI "IMAGENAME eq testhost.exe"      # 有在飞 testhost ⇒ 计数不可归因，先等或转隔离树
```

- `testhost.exe` 为零 **不等于**树空闲：detached 的 `dotnet test` 父进程可能先退、testhost 后起，
  只数 testhost 会漏孤儿 ⇒ 连 `dotnet.exe` 一起按 CommandLine 点名归属树（取法见 Step 2b）。

- 共享主树有并行 lane 在途时，整条阶梯在**钉 commit 的隔离树**里跑：
  `GIT_LFS_SKIP_SMUDGE=1 git worktree add --detach out/_<tag> <sha>`（cwd 留在该树内，它自带 docs/tools）。
- 消融中途 `src/` 处于改过状态 ⇒ 备份件放 `out/_abl/`（仓内忽略），**不要**用 `git checkout <rev> -- .` 还原。
- ★ **补丁脚本先归一换行再改、写回时还原原形态**（§583 自犯）：本仓 `.cs` 是 CRLF。若脚本读原文后直接做
  「给每个 LF 前补一个 CR」式写回而不先把 CRLF 归一成 LF，就会造出 CR CR LF 双断行（多跑一遍再多一个 CR）。
  症状极坑：.NET 的 `File.ReadAllLines` 把每个 CR 都当断点 ⇒ **行数三倍化** ⇒ `SourceFileSizeTests`（800 行硬门）
  报「某刀文件 1068 行（上限 800）」，看着像「谁把文件写大了 / 本手撞了硬约束 5」，而 `wc -l` 只数 LF 仍报 356
  ⇒ 极易误归因、误撤刀。自查 = 一次数三量（LF 数、CRLF 数、孤立 CR 数 = CR 总数减 CRLF 数），孤立 CR 大于 0 即中招；
  处方 = 先归一再改、按原形态写回（做一个行尾安全的 `out/repl.py` 包装，逐处 assert 命中再落盘）。

## Step 1 红-first 的定义（三件缺一即未收口）

1. **档级计数**：撤刀态下「恰 N 红 / M 绿」，且 N 的每一枚能逐例归因（哪条断言因撤刀而红）。
2. **不是负控测试的替身**：判据档自带的消融负控只证「闸能翻一个数」，不证「整档在没有刀时红」——
   它自己就是被验对象的一部分，不能自证。
3. **还原后同档全绿**：同一段命令在还原态必须全绿，否则红因不在本手改动上。

## Step 2 源码级消融（唯一合法红-first 证据）

```bash
KNIFE=src/DwgParser.Core/Objects/Writer/<本手刀件>.cs      # 点名本手那把刀的落点文件
CLS=<判据档类名>                                            # 如 AttDefTailDowngradeToAc1015Tests
mkdir -p out/_abl && cp "$KNIFE" out/_abl/knife.bak
python - <<'PY'
p = r'<与上面同一个 .cs>'
s = open(p, 'rb').read().decode('utf-8')
crlf = '\r\n' in s
s = s.replace('\r\n', '\n')
old = "        => IsAttributeFamily(rec)\n"        # 刀自己的闸首行，改成你那一行
new = "        => false\n           && IsAttributeFamily(rec)\n"
assert s.count(old) == 1, s.count(old)              # 精确整行 + 断言唯一命中，防多行串替换
out = s.replace(old, new, 1)
open(p, 'wb').write((out.replace('\n', '\r\n') if crlf else out).encode('utf-8'))
PY
python tools/winbuild.py dotnet build DwgParser.sln -v q
python tools/winbuild.py dotnet test DwgParser.sln --no-build --filter "FullyQualifiedName~$CLS" 2>&1 | grep -aE "失败 |通过!|失败!" | head -8   # 记 N 红 / M 绿
cp out/_abl/knife.bak "$KNIFE"
python tools/winbuild.py dotnet build DwgParser.sln -v q
python tools/winbuild.py dotnet test DwgParser.sln --no-build --filter "FullyQualifiedName~$CLS" 2>&1 | grep -aE "通过!|失败!" | head -3        # 必须全绿
git diff --numstat -- "$KNIFE"                      # 还原自证，必须为空行
```

- **改 .cs 走 python 且先归一再写回**：本仓部分源文件存 `\r\n`（甚至 `\r\r\n`），通用写口整文件重编码
  会把 13 行小改写成 417 行假 diff；落地前用 `git diff --numstat --ignore-all-space` 对一眼。
- 成本固定 = 2 次 build + 2 次跑档（≈10 min），且 build 与 test **串行独占该树**（铁律见 Step 2b：并发毁的是两副读数）。
- 闸首行不是单一表达式时（多条件 `&&` 串），优先在**调用侧**摘 `else if` 分支——那是本手真正的「无刀世界」。
- **还原后必重建再取一次读数**：只 `cp` 回源码而不 build ⇒ 下一段命令跑的是消融态旧 dll（本手实撞，
  读出「3 例全红」归错因）。
- **还原用的 `cp -p` / `shutil.copy2` 连 mtime 一起搬回来 ⇒ 增量 build 判「输入未变」直接跳过**，
  它照样打「0 个错误」而 dll 仍是上一形态（本手实撞：判据档 5/5 绿被读成「1 红」、
  `gate.py` 把已翻的格读成「具名点未前移 + 富图未翻」，两处都指向假结论「刀没生效」）。
  正解 = 还原后 `touch <刀件>` 顶新鲜，或消融轮次一律 `-t:Rebuild`。
  **自查哨 = `build` 耗时 ≈2 s 即红旗**（真重编十几秒起）；另一枚不可能读数 = 同一份源码两次
  跑出不同产物字节（本手 = 同一枚 531 帧读成 11,143 / 11,144 B），出现即先疑 dll 形态，别去改码。

## Step 2b 同一棵取证树内严禁并发 dotnet 作业（串行独占铁律）

`dotnet test` 正在跑时再起任何 `dotnet build` / `dotnet test` —— **含 `--no-build` 那次 test 自己触发的
隐式 build** —— 后果两层：

1. `MSB3021/MSB3027`「dll 被 testhost 锁定」，起锁那手直接构建失败；
2. 更毁的是**已开跑的那轮会在中途换 dll、吐出成片假红**，事后无法与真回归区分，整轮作废重跑
   （§550 实测一轮从 8 红涨到 1400+ 行栈、`DimStyleValueAxisTests` 整族假红）。

规矩：

- **验刀全流程**（改闸 → `-t:Rebuild` → 跑判据档 → 还原 → 再 `-t:Rebuild`）、**全量/分片 test**、
  **`gate.py`** 三类作业在同一棵树内串行独占；要并行就**另起一棵钉 sha 的 worktree**，别共享 bin/obj。
  **消融与取门读数不可同树叠跑**：`gate.py`/`richverify` 用 `--no-build` 吃当下 dll，而消融轮次正在
  换 dll ⇒ 门读数挂在某个中间形态上。口径 = 一条命令里串完「build → 判据 → 门」，中间不插第二件事。
- **假完成**：前台命令超时被 detach ≠ dotnet 已停——包装层报「已完成」而子 testhost 仍在写 dll 是常态。
  下一步 build/test 前点名归属：
  ```powershell
  Get-CimInstance Win32_Process -Filter "Name='testhost.exe' or Name='dotnet.exe'" |
    Select-Object ProcessId,CreationDate,CommandLine
  ```
  自家树的孤儿 `WaitForExit` 等它退，确认是脱离残骸才按 PID 杀；**别道树的一律不动**。
- `gate.py --no-build` 吃的是**预建 exe**：共享树（或刚被并发污染过的树）跑门前必须先把 exe `-t:Rebuild`
  到位，否则门测的是旧程序集——吐出来的是假位移/假回归，事后还要按 `dwg-golden-rebless` A7 花一整轮
  bless 逐格归因去擦。

## Step 3 消融自证生效（否则读数作废）

任何「关掉某段代码再测」的手法，先证它真改了产物：

- 同一档 OFF/ON 两态各存一份产物，比 `sha256` 或帧长：**同哈希 = 消融没生效**，其后所有「绿/红」都是旧行为。
- 旁路/留一法同型判据：`sha(子集旁路) == sha(全旁路)` 一比即穿；规格零命中必须 rc≠0（fail-loud）。
- 上刀痕迹可直接数：写侧 trace 行（`DWGDBG=1` + `DebugTrace`）里本手刀痕的行数应 ≥ 射程对象数——
  「放宽闸零效应」那一族老坑的解药（§502 实例，`ss <= tailStart` 写错 ⇒ 11 枚一枚不上刀、判词一字不动，
  症状与「闸未触发」完全同形）。同一枚坑在 §398乙 重现——枚数上限 `nr <= 8` 把真 `nr=10` 的富图表记录
  整枚挡在闸外，而**走查已闭合**才是真哨；症状同样是「只有大图那几枚红」。
- **消融全绿先怀疑判据的锚，别先信「刀无关」**（§549 实撞）：判据取位点若在该类**根本不被填**，走查会被
  守卫整枚跳过 ⇒ **无刀也全绿 = 恒过判据**。实例：帧级判据拿 `rec.HandleStreamStartBit` 起走，而
  `LAYER_CONTROL` 这类 `HasHandleStream=false` 的记录该字段恒 0 ⇒ 第一版判据 6/6 绿、消融也 6/6 绿，
  改成写侧同式 `BodyStart*8 + RL` 后才取出「无刀 3 红 / 有刀 7 绿」。守法：**每条帧级判据都要在消融轮
  见到 N 红**，并在判据档里另钉一枚「原生真本同判据成立」的正例——原生也红 = 判据本身不合，不是病灶。
  同源第二枚坑：同一段走查在原生件上 2,658 枚落空 ⇒「全档无违例」不是产品不变量，判据只能钉具名枚。
- **消融轮的红「落在哪一枚」本身是判据的一部分**（§552 正用）：帧级判据写成**逐枚句柄一条 `InlineData`**
  而不是「整档一条聚合断言」，则消融读数不止是「N 红/M 绿」，还能说出**红的那枚 = ODA 具名点的那枚**
  （§552 实测：无刀 1 红 = `handle: 7612` = `0x1DBC` = `.err` 里点名的 `AcDbViewport (1DBC)`）。
  这一比 = 「刀与病同枚」最便宜的自证：聚合成一条就只能证「刀有关」，证不了「刀在治那一枚」。
  反手同用：把**邻档**（同图同类的其它目标版本）也逐枚钉进判据档当**不动量控**，落刀后它们仍绿才叫射程干净。
- **判据锚必须与「刀所改的那个量」同量纲，且换锚后整条阶梯要重跑**（§553 实撞，两版结论互相打过）：
  同一枚帧上「帧级量具」（`RL` / 数据区宽度）与「段级量具」（某子段位数）**不必同起同落** ——
  刀把总长修对、内部两处各差 ±8 位恰好对消时，段级锚会报「未修好」、帧级锚报「已同形」，
  于是出现一条**基于错锚的自我订正**（本手先信了段级，把对的结论推翻了一轮）。
  守法三条：① 落刀前先问「这把刀改的是哪一段的宽度」，判据就锚在那一段的**上级可测量**（整帧/整区），
  别拿子段位数当唯一尺；② 任何两把量具读数矛盾时**先对齐坐标系与锚点**（同句柄、同起点、同含头规则），
  再判谁对 —— 本手的正解是「三件器械同枚一次跑齐」，不是推理；③ **换锚后必须重跑撤刀/装刀两端**
  （旧锚下 26 红≠新锚下会红；本手换锚后重跑得 = 撤刀 26 红 / 装刀 27 绿，与旧锚结论方向相反的一刻要写进档）。

## Step 4 env kill-switch 的射程（实测三态，勿一概用）

| 谁设这个变量 | 读者 | 实测结果 |
|---|---|---|
| shell 前缀喂 `ATTDN15_OFF=1 … dotnet test` | 判据档（vstest/testhost） | **刀照旧发火 ⇒ 7/7 全绿 = 假绿**。现象可复现；**成因未定谱**（当初「证明包装层吞变量」那条探针漏了前缀，是无效对照，别照抄因果） |
| 测试自己 `Environment.SetEnvironmentVariable(...)` + `finally` 清值 | 同进程内的 Core | **有效**（①负控「撤刀 ⇒ 542 位」就是这么钉的） |
| shell 前缀喂 CLI / `product_fingerprint.py`（python 再 fork CLI） | 直跑子进程 | **有效** ⇒ 才有 Step 5 的同树 A/B |

结论：**消融开关照留**（直跑面便宜、且是恒等面 A/B 的唯一入口），但**红-first 证据只认 Step 2**。
判「变量到底抵达没」的那条探针，必须与被验命令**同一条前缀**。

## Step 5 零 ODA 恒等面 A/B（同树 ≈2 min，替代 cross A/B + recopy 面）

```bash
ATTDN15_OFF=1 python tools/winbuild.py python tools/product_fingerprint.py baseline   # OFF 态（换成本手刀名）
python tools/winbuild.py python tools/product_fingerprint.py after                    # ON 态
python - <<'PY'
a = open('out/fingerprint/baseline.txt', encoding='utf-8', errors='replace').read().splitlines()
b = open('out/fingerprint/after.txt', encoding='utf-8', errors='replace').read().splitlines()
d = [(i, x, y) for i, (x, y) in enumerate(zip(a, b)) if x != y]
print('rows', len(a), len(b), 'diff rows =', len(d))
for i, x, y in d[:12]: print(' @', i, ' OFF:', x[:150], ' ON :', y[:150])
PY
```

- 判读：本手只碰某族时，**极简面 diff 0 = 该面零位移**（极简语料 0 实体 ⇒ 结构上不可达）；
  差异行必须**全部落在本手射程**；出现射程外行 = 有连带改动，先定因再落。
- 位移行也要定因：先在**该图上跑一次 ODA 两态对照**再写结论（实例，`图纸模版/MgF2` 魔数 AC1018 ⇒ 在射程内，
  而 OFF/ON 判词逐字相同 ⇒ 破损不是本手，只是摸到了一笔未收口账）。
- 若没有 env 开关可造 OFF 态，就走两棵钉**同一 commit** 的树做跳树 A/B（`worktree add … HEAD` 会随并行推进漂移）。
- 坑：`product_fingerprint.py --help` 不是帮助参数，会写出 `out/fingerprint/--help.txt (66 rows)`；
  模式位只有 `baseline` / `after` / `<名字>`。控制台中文行是 GBK，按 `errors='replace'` 读。

## Step 6 分片全量套件（避 10 min 上限，禁后台跑）

前提：`grep -rhn "^namespace" tests/DwgParser.Tests/*.cs | sed 's/.*namespace //' | sort -u` **只有一个命名空间**
⇒ 按类名首字母分片即覆盖全类；多命名空间时此法失效，须改按类名清单分片。

```bash
S=""; for L in A B C D E F G H; do S="$S|FullyQualifiedName~DwgParser.Tests.$L"; done; S="${S#|}"
python tools/winbuild.py dotnet test DwgParser.sln --no-build --filter "$S" --logger "trx;LogFileName=s1.trx" --results-directory out/_abl
S=""; for L in I J K L M N O P Q R S T U V W X Y Z; do S="$S|FullyQualifiedName~DwgParser.Tests.$L"; done; S="${S#|}"
python tools/winbuild.py dotnet test DwgParser.sln --no-build --filter "$S" --logger "trx;LogFileName=s2.trx" --results-directory out/_abl
python tools/winbuild.py dotnet test DwgParser.sln --no-build --list-tests 2>&1 | grep -acE "^\s+DwgParser"
```

- **trx 必须显式命名并按名读**（默认名 + mtime 取最新会读到别道那次运行）；两片**串行**，跑前数 testhost。
  后台起的 test 以「**日志不再增长 + 进程消失**」双条件判完成，包装层的「已完成」不算数（Step 2b 假完成坑）。
- 分片求和比 `--list-tests` 多约 30 枚属正常（运行期才展开的数据行），两个数都要给。
- 片里出红先做**不在场证明**，同树退回无本手改动的基线重跑那一枚，再判归因。

## Step 7 门：全门是**必跑项**（旧「一手跑不完」口径已作废）

- **实测耗时**：`gate.py --no-build` 整门（cross A/B 60 枚 + recopy 6 + **富图 60 枚**（`RICH_SETS =
  ['sample','table']`，2026-09-25 用户裁定并入 table）+ EDIT 40 枚 ≈ 106 枚 ODA 格）**= 107 s、
  `GATE_RC=0`**（2026-09-25 @`9b937806` 干净树实测；`--bless` 同量级 ≈77 s、`--edit-only` ≈43 s）。
  ⇒ 旧记「≈96 枚超单命令上限、一手跑不完是常态」**不再成立**，别再拿它当不跑门的理由。
- **`rc=3` 先自测再决定绕**：整门连撞 `rc=3`（单例闸）时，先 `python -c "... import oda_wait,gate;
  print(oda_wait.preflight(gate.ODA))"` 手测一次 —— 实测该闸会把**自家** `out/gate/oda_inst_N` 兄弟实例
  按「目录取不到 ⇒ 保守命中」误判成他人（`_ours()` 的已知缺陷，别道已挂遗留）。`preflight` 返空 =
  假忙 ⇒ `ODA_SINGLETON=off` 取数**并在详档写明是假忙**；返非空 = 真有人在跑 ⇒ 等，别绕。
- **抖动格不得入正式读数**：某遍带 `CRASH`/`NO_OUTPUT`（实测 `AC1015->AC1024` 会连撞两遍）⇒ 该遍只作
  过程记录，重跑一遍取到「`GATE_RC=0` 且全树零 `CRASH`/`NO_OUTPUT`」那遍当正式数（D46 同口径）。
- **三证（Step 5 + Step 6 + 逐格 `richverify`）降级为省时近似**，只在「确实跑不动门」时用，且必须
  同时写：为什么没跑成（真忙 / 超时）+ 三证各给了什么 + 给下一手的并入路径。**不 `--bless`**
  （`--bless` 属口径变更，须用户点名）。
- 注意**面与集**：`add`/`lineanddim`/`textandrect`/`fill` 等仍是按需集，逐格 `richverify` 得出的翻格
  **不会自动进基线**；换集不换判据（`sample` 同键仍红是常态）。

## Step 7b 撤闸 / 放宽类手（「删一行」也算落码手）

「把一道 `return false` / `return XxxBail(...)` 删掉」类手，配方与「加窄刀」有四处不同，漏一处即造出**恒真判据**：

1. **红-first = 源码级装回那道闸**（不是把别的条件改 `false`）：`git show HEAD:<刀件> > <刀件>` ⇒ 重建
   ⇒ 跑判据 ⇒ 记「恰 N 红 / M 绿」，且**每一枚红都要能归因到本档新入的 face**（实测 §527 = 5 红：
   `AttributeEditPersistence` 2 + `DrawingEditApi` 3，全部落在 `add_2000.dwg` × AC1015）。
2. **钉旧边界的判据要逐枚随迁，不许一表并档**：把该档从「场外」挪进「场内」表后，**别的算子可能仍在场外**
   —— §527 里 `setText/setAttrib` 六档能落、`addMText` 仍只覆盖 R2004+ ⇒ 拆成两张表
   （`SameVersionSixFaces` vs `SameVersionR2004PlusFaces`），并回一张即假绿。
3. **契约的载体必须迁到仍在场外的档**：旧例 `..._Is_Refused_Without_Product` 钉的不是 AC1015 本身，
   而是「拒得具名 + 不留产物」这条契约。闸一开，若直接删例 = 契约失守；若留着例 = 恒真。
   正解 = 换一枚**仍然被拒**的载体（§527 换成 AC1021→AC1027 同形跨档）继续钉住契约。
4. **零回归可用机理证明替代抽样 A/B**：被删那行**下游**的一切都要先过一句
   `if (edits.Count == 0) return false;` ⇒ 不带改写的产物**结构上不可能变字节** ⇒ `cross`/`recopy`/`rich`
   三面一枚 `--ops` 都不带。把这条写进详档，比跑一遍指纹强（指纹只抽样，机理覆盖全语料）。

## Step 8 落地与「已测树 ≡ 提交」的核对（共享主树专用，零破坏）

- 只 `git apply` **本手那几个路径**（逐枚 `--check` 后 apply），**绝不**整文件 `cp` 覆盖主树。
- 起手先 `git status --short -- <本手路径>`：命中 `MM` = 别道把一版改动**只活在暂存态** ⇒ 先
  `git ls-files -s` + `git cat-file blob :<path> > out/_staged_x.blob` 存下，提交后按原 blob
  `git update-index --cacheinfo 100644,<sha>,<path>` **还原**，别让对方的暂存态被本手 `--only` 吞掉。
- 提交后核对只用只读哈希（见 [[shared-tree-verification-no-checkout]]）：
  `git rev-parse HEAD:<path>` 与 `git -C <已测树> hash-object <path>` 逐枚相等。
- `README.md` 这类带别道在途 reorg 的 `MM` 文件：宁可不改，把那一行欠账写进 backlog 现势行，
  交 README 的 owner 或下一手 —— 别为改一行把人家半成品一并提交。

## 验证（收尾自检）

- [ ] 撤刀态 = 恰 N 红 / M 绿；还原并重建后 = 同档全绿；`git diff --numstat -- <刀件>` 为空
- [ ] 动过**共享件**的字节形 ⇒ 已跑全量套件 + `firstdiff` 位移枚数（只跑门不算验过）；拿「≡ 真本」当尺 ⇒ 已把≥两本真本同枚互校。
- [ ] 改过的 `src/` 件行数已 `wc -l` 核过（≤800），越线是先拆 partial sibling + 复验产物 sha 不变，不是压注释。。
- [ ] 消融生效已自证（OFF/ON 产物 sha 或刀痕行数不同），不是同哈希的重复测量。
- [ ] 恒等面 diff rows 数已记；若有位移行，已跑该图两态 ODA 对照定因。
- [ ] 两片计数与 `--list-tests` 差值已解释；红/绿读数带**档名 + 格数 + 提交号 + 是否干净树**。
- [ ] 全门已跑（`GATE_RC=0` 且全树零 `CRASH`/`NO_OUTPUT`）；没跑成则写清「真忙 / 假忙（`preflight` 手测返空却 rc=3）/ 超时」三选一 + 三证各给了什么。基线未动。
- [ ] `out/_abl/` 备份件与本手临时件已清。

## 陷阱速查

- **判据锚字段在该类根本不被填 ⇒ 判据恒过**（消融也全绿）：帧级判据落刀前先证锚有效
  （§549：`HasHandleStream=false` ⇒ `HandleStreamStartBit` 恒 0，走查被守卫整枚跳过）。
- **`--no-build` 复用被别道或本手上一步打补丁的 dll** ⇒ 假红/假绿；改过 Core 后不在 sln 里的工具工程须逐个 `-t:Rebuild`。
- **构建报 `MSB3021/3027 文件被 testhost 锁定`** 不是源码错：**首选等**（同树还有在跑的 test / 超时后
  detached 的自家 testhost 在写 dll）。杀进程是最后手段，且先用 `Get-CimInstance Win32_Process` 看
  CommandLine 确认是**自家树**的脱离残骸才按 PID 杀；别道树的进程一律不动。
- **同一棵树里起第二个 dotnet 作业，毁的是两副读数**——报锁那手只是明错，暗账是**已在跑那轮中途换 dll、
  吐成片假红**（§550 实测 8 红 → 1400+ 行栈、`DimStyleValueAxisTests` 整族假红），事后无法与真回归区分、
  整轮作废重跑十余分钟。验刀 / 全量 test / `gate.py` 同树串行独占，要并行另起 worktree（Step 2b）。
- **打印 `⇒`/`①` 的 python 会崩在写盘前**（GBK 控制台）：结论先落 UTF-8 文件再用 Read 看。
- **「探针没读到变量」≠「变量没传进去」**：探针与被验命令必须同一条前缀，否则是无效对照（本手实撞）。
- **消融还原用 `cp` 备份件 + 重建**，不用 `git checkout -- .` / `stash`（共享树里会抹别道在途）。
- **门耗时按文档断言 ⇒ 白等**：整门实测 107 s（≈106 枚 ODA 格），旧记「一手跑不完」已作废；
  先跑门再说跑不动。
- **长手落地前先查在途提交**，`git log --oneline <起手 sha>..HEAD -- <目标路径>`；别手可能已把同一行入库，
  此时把手改判成「补机器锁 + 记帐」，不重贴 `src/`。
- **改「共享件」的字节形，门是瞎的**（§557 实测）：`TryCarryEedPreR2007` 那类被 20+ 把降版刀复用的编码口径，
  改后**整门照样 `无回归 [PASS]`、格照样翻**（ODA 只看 EED 载荷自洽、两形都吃），而**全量套件一次吐红 20+ 枚**
  「产物 ≡ 原生 AC1018」金标 + 单格产物位移 178/5368 枚 ⇒ 凡动共享件，阶梯里 **Step 6 全量不是可选项**、
  且必须跑 `firstdiff <改前产物> <改后产物> 0` 数位移枚数；只跑门就落 = 把口径债推给下一手。
- **拿「≡ 真本」当尺之前，先证真本之间彼此一致**：同一枚 EED code-0 "DSTYLE" 串，原生 `add_2000/add_2004`
  写 **7 B 含**尾 NUL、原生 `sample_2000/sample_2004` 写 **6 B 不含**，而两者源的 TU 都是 6 枚码元无尾 NUL
  ⇒ 该位**不是位级不变量**，判据须降到「文本/字段级逐枚等值 + ODA 接受」并把分叉写进判据档注释与锚档；
  要统一口径 = 独立一手 + 重钉那批金标 + 用户裁定（坑 **D126**）。
- **几行注释也能顶破 800 行硬上限**（`SourceFileSizeTests` 是机器门）：本手三行中文注释把
  `DwgObjectStreamWriter.Dictionary.cs` 从 797 推到 802 ⇒ 全量套件当场红。处方 = 按职责把**该手自己那一支**
  逐区间原样搬进新 partial sibling（类加 `partial`、可见性不动、零行为改动），并**同轮复验产物 sha256 与搬家前
  逐字节相同** + 邻域过滤集复跑；搬家前先 `git diff --name-only <base>..HEAD -- <那几件>` 确认别手没在同文件在途。
