---
name: dwg-golden-rebless
name_en: DWG Golden Rebless & Fixture Triage
name_zh: DWG判据值重钉与语料处置
description: Re-bless hard-coded golden values (byte-exact SVG/point-list/frame fixtures) after a deliberate semantic change, and triage missing-corpus test failures. Covers run-time dumps when xUnit truncates assertions, cross-tree byte diff plus knife ablation for section deltas, hand-recompute, CRLF-safe byte edits, corpus-delete blast radius, evidence-only when another lane's fix is uncommitted, three control-round branches (retire / rebless / producer fork), mass-retire by method bounds with count reconciliation, and per-cell attribution of a --bless that absorbs parallel lanes' unpinned flips. Use when a core fix turns a golden red, dotnet test reports mass FileNotFoundException, a fixture repoints to a byte-identical copy, pruned-reference criteria retire in bulk, or a bless needs live readings not baseline prose. A per-record-multiple skew set by the reference writer is a producer fork, so neither pin it nor retire it, strip per side plus a count lock, else a reverted knife washes into false green.
description_en: Re-bless golden values after a deliberate semantic change (run-time dump because xUnit truncates trx, hand-recompute, CRLF-safe byte edits) and triage missing-corpus failures (byte-identity check, in-flight-lane evidence-only). The control round reads as three branches, retire / rebless / producer fork, and section-level deltas corroborate by cross-tree byte diff plus ablation of the landed knife, not a hand-rolled bitstream walk. The third branch, the one mis-sorted into retirement, is a runtime-computed native frame whose constant skew is a per-record multiple set by that file's writer, so neither pin the constant nor retire the criterion - measure and strip per side plus an explicit count lock, since blunt +8 or strip-without-lock washes a reverted knife into false green and +8 false-reds a later AutoCAD-made fixture. Also covers mass-retire by indent/semicolon bounds with count reconciliation, per-cell attribution of a shared-tree --bless, and the fresh-worktree no-out/ prep.
description_zh: 判据值（逐字节 golden / 点列 / 帧）随体**重钉**的配方 + 语料缺席类失败的**归因与处置**：xUnit 截断 trx 长断言时如何取全值（运行期 dump 而非读 trx）、为何重钉必须**手算复核**而不能盲抄、CRLF 安全的字节级编辑（Edit 会把 CRLF 规范化成 LF）、以及「语料缺席该不该由本手修」的判据（含**修法已在别人 worktree 里未提交**那一型：只出证据、不写第二条修法）、**删语料前先量面**（逐档测试文件数 / 引用处数 + 是否喂恒等门，否则缺产物两侧同缺会报**假 IDENTICAL**）。**钉之前先跑控制轮**（先分「断言的另一侧是谁」；跨族一致偏一个常数 = 换料暴露的真实缺陷 ⇒ 留红记链首，既不钉也不删）、**批量退役的切法**（按同缩进 `}` / 分号取方法尾，裸括号计数会被内插串与表达式体连吃邻居；验收要计数对账，`build 0 错` 不算数）、**换挂替身先验是不是同一枚图**（魔数 + `--census` 实测，名字带 `empty_` 不等于空图）。用于核心语义修复把既有 golden 打红、`dotnet test` 报大量 FileNotFoundException、要把夹具路径改挂到已有的逐字节同内容副本、要成批退役参考图已被清退的判据、或**共享主树里一次门基线 `--bless` 连带吸收并行手「已入库未钉」的位移要做逐格归因**时。 **基线是历史快照不是今日现状**：选靶/报数前先跑活门取今日真值（基线注 `53/60`、活面可能已 `54/60`），bless 前后各与 HEAD 逐格 diff；别道 fix 落库常不动基线，下一道 bless 替其入钉是**接续非抢功**，但须点名格与 sha。★ 控制轮**第三支**：对侧若是**运行期现算**的原生帧、常数差来自该档**写出器** ⇒ producer 分叉，既不钉常数也不退役，正解 = 两侧各自剥 + 枚数锁（钝法或只剥不锁都会把撤刀洗成假绿，消融已实测）；差在整节字节流时改用「两棵树同一转换逐字节对拍 + 对已落库刀做源码级消融」定 Δ 足迹；新隔离树无 `out/`、Windows 用 `python`。
argument-hint: Name the failing test class and the semantic change, e.g. "重钉 DimensionFamilyRendererTests 的 LinearScaled"
argument-hint-en: Name the failing test class and the semantic change, e.g. "rebless LinearScaled in DimensionFamilyRendererTests"
argument-hint-zh: 指定失败档与语义变更，如「重钉 DimensionFamilyRendererTests 的 LinearScaled」
user-invocable: true
---

# 判据值重钉与语料处置（golden rebless & fixture triage）

两条独立链路，别混：

- **A. 语义变更把 golden 打红** ⇒ 重钉，但要**证明**新值对。
- **B. 语料文件缺席把测试打红** ⇒ 先**定性责任**，再决定修不修。

判据：**「值不匹配」才是本手的账；「文件找不到」不是。**

顺序铁律：**开工第一动作 = 抢锁 + 扫当日新档**（A0，先确认没人已在做这批红）→ **再切「断言的另一侧是谁」
→ 再跑控制轮 → 才有资格谈「重钉」**（A6）；钉的是单值，
**门基线整份 `--bless` 之后还有逐格归因**（A7）。跳过前两步的钉，
多半是在把缺陷洗成基线；而批量退役那一刀要按缩进/分号取尾并计数对账（B6），别拿 `build 0 错` 当验收。
基线行话还会滞后于并行手已入库的刀，**选靶/报数前先跑活面取今日真值**（A8）。

## A. 重钉 golden

### A0. 开工前先查：这批红**是不是已经有人在处理**（血泪第一条）

判据面重钉类任务的失败模式不是「钉错值」，而是**做了一份没人要的重复**：同一批红常被多条 lane
同时盯上 —— 它横跨多档、算式又整齐（`+8/+16/+24` 位），看起来「一手可清」，于是最容易被抢。

开工前**四查，缺一不可**：

```bash
python tools/progress_lock.py acquire --task "<方向>" --owner <车道id>  # ① rc=3 ⇒ 有人在动，停手
git log --oneline -15                                                  # ② 同夜刚落/在途的提交
ls -t docs/issues/ | head -20                                          # ③ ★ 当日新档
grep -rln "<失败档类名>" docs/issues/ | head                           # ④ 按类名反查既有档
```

- **`git status` 干净 ≠ 没人做**：别人可能在自己的隔离 worktree 里做（主树看不到），也可能档已入库而
  改动只活在别处（本仓实证：`§576` 档 + patch 已入库、patch 却**未 apply**）。
- **`docs/issues/` 当日新档是最强信号**：本仓同题档会把结论写进**文件名**
  （如 `…-golden-drift-nul-knife-attribution-and-class-pin-575.md`）。不扫这一眼，就会把已完成的归因重做一遍。
- 查到已有 lane 在动 ⇒ **只出证据、不落码**（与 B2「修法已在途」同一条），或改做它交下来的遗留。
- ★ 抢锁尤其重要：**它是开工第一动作，不是收尾动作**。锁是 advisory，抢不到（`rc=3`）就是最便宜的
  「有人在动」信号 —— 比读任何文档都快。

> 实证（2026-09-26 · 使用者的真实翻车）：自主选了「清 12 枚真值对标金标漂移」，控制轮跑得干净
> （撤刀 ⇒ 11 枚全绿、归因钉死 = 实体侧 TV 尾 NUL 刃 + 类节补枚）、全量分片也吻合
> （`4,103 例 / 15 红` = `27 − 12`），但**把这 11 枚直接随迁了** —— 而刀主人的详档写着
> 「属**判据面**改动，**须点名**」。更糟：同夜已有 **三条** lane 覆盖同一批红
> （`§575` 归因档 / `§576` 生产者形普查 + 归一法 patch 已入库 / `§577` 台账随迁在途），
> **直到抢锁 `rc=3` 才暴露**。全程零落库、全部回退，代价是整手白做。

### A1. 先分清是哪一类失败

```bash
python tools/winbuild.py dotnet test DwgParser.sln --no-build \
  --filter "FullyQualifiedName~<失败档>" --logger "trx;LogFileName=x.trx"
```

解析 trx（`UnitTestResult` 节点）按**消息首行**分类：
`FileNotFoundException : DWG file not found.` ⇒ 走 B 链；`Assert.Equal() Failure` ⇒ 走 A 链。
**别只看计数**：trx 的 result 节点数 ≠ VSTest 汇总口径的失败数（本仓实测 875 vs 392，别拿它当读数）。

### A2. 取全值：**别指望 trx**

xUnit 对长字符串断言消息做**截断**，trx 里只留形如
`"99.46806,201.71961 96.23212,212.18054 118"···` 的前缀 ⇒ 拿不到完整实际值。

**唯一可靠办法 = 运行期 dump**：临时把断言改成先把实际值落盘，再断言。

```csharp
var actual = Svg(Fixture(name), name == "LinearScaled" ? Scaled : null);
System.IO.File.WriteAllText(
    System.IO.Path.Combine(@"D:\Project\DwgParser\out\_mtx", "dump_svg_" + name + ".txt"), actual);
Assert.Equal(GoldenSvg[name], actual);
```

跑一次该档，取 dump 文件内容，写回 golden，**然后撤掉 dump 代码**（把表达式体还原回去，把 diff 压到最小）。

### A3. 重钉必须**手算复核**，不许盲 bless

新值要能**解释旧值错在哪**。判据：手算一遍变换链，并指出旧值对应的是哪个错误中间量。

> 实例（`Matrix` 行/列约定修复，2026-09-23）：块内点 `(0,3)` 经 `R(0.3)→S(2,3)→T(100,200)`
> = `(−sin(0.3)·3·2 + 100, cos(0.3)·3·3 + 200)` = `(98.22688, 208.59803)`；
> 旧 golden `97.34032` = `(−sin(0.3)·3·`**3**`+100)` —— 正是把 `sx=2` 误用成 `sy=3` 的指纹。
> 指纹对上了，重钉才算有据。

**另一种取证形**：差不在点列而在**整节字节流**上时，钉值来源 = 两棵树同一产物的逐字节对拍，不是「跑一次拿实得」：

1. 两个 sha 各建 detached worktree（同源图、同目标档 = **同一份转换**，只换码），临时 xunit 件 dump
   **仓内既有解码路**拿到的解压节（如 `(DwgFile.Load(..) as R2004File).DecompressSection("AcDb:Classes")`），跑完即删不入库；
2. 两节 byte-diff ⇒ 列一张「节长 / 节固定开销 / 记录流 / 记录枚数」四列表，再**逐枚指认差异字节**
   （§577 实测：无补枚树 4,197 B/75 枚 vs 有补枚树 4,249 B/76 枚，开销两侧同 46 B，28 枚差异字节 =
   节头 `RL` 1 枚 + 记录流首 `max_num` 1 枚 + 尾 26 枚旧垫位被追加记录占住）
   ⇒ 前 4,151 字节一位不差，**Δ 恰 = 那一枚新记录的 52 B 足迹**；
3. **两重负控**：① **源码级消融已落库那把刀** —— 把它的闸（`R2004SectionReencoder.InfoSections.cs` 的
   `NeedsPreR2007DataTableClass`）临时改 `=> false`、重编译 ⇒ 该档**恰 1 红 / 3 绿**，红就在长度例
   （期望 4,249 实得 4,197）；还原后 `touch` 刷 mtime、复跑 0 红、`git diff -- src/` 空。
   ② 跨树对照 —— 旧树一侧仍读 4,197/75 ⇒ 新值只描述「补枚之后」的形制，**没把旧形洗成正确**。

★ **别自写位流走查**去取节内容：现造走查在真实语料上直接 `IndexOutOfRange` 崩，一手都没拿到分解；
仓内解码路（`DecompressSection` / `BitReader` 既有入口）已经过门，dump 一定拿得全。
★ 只量出「Δ = 一枚足迹」后，**先验该节能不能当对拍锚**：我方类节走共享件 `BitWriter.WriteTV`（不带尾 NUL），
原生带 ⇒ 类节侧与实体侧 NUL **方向相反**，该类节只能自证、不能当「≡ 原生」锚（新判据会当场假红）。

同时**给 golden 注释加一条「例外」**（照既有 `["Dia"]` 例外的写法）：写清触发变更的 issue 档路径、
为何这枚必然变、新值来源 = 实测落盘而非手算。

### A4. 别漏了「同错即绿」的判据

修一个语义 bug 后，往往有**期望值当初就是按错实现取的**判据。它们此前是绿的，修复后才红。
这类判据要**逐条订正**（含它的注释公式、以及被抄进 `docs/` 的同款公式），不是重钉。

判据：如果 golden 的**定义式注释**本身就写错了，那它保护的不是正确语义 ⇒ 订正公式 + 订正期望值。

### A5. 收敛一个约定后，全仓扫「按旧约定写的小助手」

把某类型从约定 X 收敛到 Y 后，**别只测直接消费点**。仓里常散落着按旧约定手写的小助手 ——
它们**不报错、不红门**，只是悄悄算错，且往往只在窄条件下才露馅。

扫法 = 搜**同一物理量**的多种算法，而不是搜类型名：

```bash
grep -rn "Math.Sqrt(m\.M11\|Math.Sqrt(.*M11.*M11" src/ --include=*.cs   # 所有「模长」式
grep -rn "Math.Atan2(m\.\|Atan2(.*M12" src/ --include=*.cs              # 所有「从矩阵取角」式
```

**实证**：`Matrix` 收敛为列向量后，`CurveFamily.AxisScale`（圆/弧半径的尺度估计）仍取
**行**模长 `sqrt(M11²+M12²)` —— 在行向量 `Apply` 时代自洽，收敛后失去几何意义，
且与已改为列模长的 `Matrix.YAxisScale` **口径不一致**。数值只在 `sin(θ)≠0 且 |sx|≠|sy|`
时不同，所以长期无人察觉。

⇒ 判据：**同仓内两个「同一个物理量」的助手若约定不同，必有一个错。** 这类不一致比 golden
变红更危险，因为它不报错。

### A6. 重钉前先跑**控制轮**：先分「断言的另一侧是谁」，再谈钉不钉

「值变了」有**三支**，钉法相反，**看一眼错误消息分不出来**：

| 断言的另一侧 | 换料/改语义后 | 唯一正当处置 |
|---|---|---|
| **另一枚图料**（`产物 == 原生某图的帧`，对侧是**存档金标**） | 对子已不存在，等式不再是同一句话 | **退役**（钉值 = 偷换判据） |
| **同源 oracle / 自洽断言**（`我方 == ODA 对同一源的输出`） | 判据仍成立，只是数值该跟着源走 | 可谈**重钉** |
| **运行期现算的另一枚图料**（对侧 = `ReadFrame(原生档)` 这类**当场算**出来的真值，差在写出器形制） | 等式仍在说同一句话，只是两侧量的不是同一把尺 | **既不钉也不退役** ⇒ 走 A6b（两侧各自剥 + 枚数锁） |

重钉之前必须先把这一刀切出来，再跑控制轮：**同一份编译产物，只把源图换回旧料**（旧料按 blob
取到 `out/` 临时件，不改任何判据），看旧料上是否逐枚 == 已钉值。

- 旧料 == 已钉值 ⇒ 已钉值仍正确描述旧料，差值全部来自新料本身；
- 新料上**一致地偏一个常数** ⇒ 那不是漂移，是新料**暴露出的真实缺陷** ⇒ **既不许钉**（= 造 A3/A4
  明令禁止的「同错即绿」）**也不许删**（= 顺手抹掉该缺陷的唯一信号）：**留红 + 记成链首**。

> 实证（2026-09-23 · `2000.dwg` 换挂 `empty_2000.dwg`）：控制轮里旧料在 LAYER/DIMSTYLE/MLINESTYLE/
> BLOCK_HEADER 上逐枚 == 已钉值 147/988/589/208（全绿）；换 `empty_2000` 后**四族一致变成「我方 = oracle + 8」**
> ⇒ 定案为写作侧余差，27 例原样留红，账落 `direction-a-backlog.md`，**没有 `--bless`、没有钉值**。

⇒ 判据：**重钉的前提是「判据还在说同一句话」。** 控制轮没跑，就还不知道自己是在钉值还是在洗缺陷。

### A6b. 第三支：对侧是**运行期现算**的真值 = **producer 分叉** ⇒ 剥 + 锁（很容易被并进 A6 退役支）

这一支对侧看起来也是「另一枚图料」（`ReadFrame(Rich("*_2004"))` / `DeltaByType(Rich(set,"2004"), outPath)`），
但它是**运行时从那个档当场算出来**的真值，不是过期金标 ⇒ 等式仍成立，**退役就是抹掉该信号的唯一一份副本**。
切这一支靠两问：

1. 差值是否恰为 **枚数 × 同一常数**（pre-R2007 内联 TV 尾终止符 = 8 的整倍），且**跨族一致**？⇒ 形制差，不是漂移。
2. 该常数是**我方**换料前就带的，还是对侧那个档的**写出器**给的？逐族按 producer 判据 v2 普查
   （§623：**只认 `<ProductInformation name=>` 单字段**，值集合里任一条非 AutoCAD 即判 ODA 系，**★ 但本尺单向：见 `AutoCAD` 不能否证 ZWCAD 经手**（另存的 CAD 图纸默认按 AutoCAD 图纸保存 ⇒ 源件那条摘要可原样带走、不触摘要），故只能写「ODA 系经手 / 未见 ODA 声明」，不能写「AutoCAD 亲手、非 ZWCAD」；**射程仅 AC1018+、AC1015 判 unjudgable 且不得回退猜**；旧「档内搜 AutoCAD / ODA / Teigha / ZWCAD 四串」已废止）⇒ §576 实测「带尾终止符」只由写出器决定（AutoCAD 亲手 697 带 / 0 不带，
   ODA·Teigha·ZWCAD 亲手 0 带 / 114 不带，**零交叉**；★ 但其中 AC1015 样本按 v2 属 unjudgable（`sample_2000` 占 AutoCAD 那侧 497/697）⇒ 引它们只能记「按图面系谱」，见 §623 §六）⇒ 定性 **producer 分叉**：既不是我方缺陷，也不是金标过期。

正解 = **两侧各自量枚数、各自剥，再另配一枚枚数锁**（本仓量具
`tests/DwgParser.Tests/NativeTvProducerForm.cs` 的 `UnitsOf` / `FormFreeWidth`）：剥完钉值描述的是
「剥掉终止符后的同形性」，与对侧出自哪家的手无关。

- **不许钉常数**（`truth.RlBits + 8`）：那是把「我方与该原生确实差 8×枚数」这句话改掉 ⇒ **判据面，须用户裁定**。
- **不许只剥不锁**：§576 消融实测（临时给写侧 `WriteTvReinlineNul` 挂一个环境变量闸、跑完还原）
  ⇒ 撤刀后**宽度等式一条都不红**，5 枚红**全部**落在新增枚数锁上。**枚数锁是这套改法能成立的前提，不是装饰。**
- `+8` 钝刀自己的长期代价（它虽撤刀即红）：把「对拍档出自 ODA 之手」焊成常量 ⇒ 日后补一枚 AutoCAD 亲手同档会**假红**；
  归一法换档自动同尺，代价只是多一枚量具档。

⇒ 判据：**三支的正当处置互斥**（退役 / 重钉 / 剥+锁），切错支比钉错值隐蔽得多 —— 钉错会红，切错从此永远绿。

### A7. 共享主树里门基线 `--bless`：位移必逐格归因

`gate.py --bless` 和钉单值不是一回事：它是**全树动作**。基线滞后期间并行手已入库的翻格、签名位移，
会被这一次 bless **一并吸收**——照单全收 = 把别人的战果钉进自己的账。§550 实测：一次 bless 与旧基线
逐格比出 **14 格位移，只有 4 格属本手**（§549/§535补十二 已入库未钉 6 格 + 仅 msg 签名前移 4 格）。

规程四步：

1. **先非 bless 跑一遍门**：除本手预期翻格外零回归（全树零 `CRASH`/`NO_OUTPUT`、五态计数 0、
   `[改进]` 恰 = 本手那几格），再 `--bless`。
2. **bless 前 + 后各跑一次与 HEAD 基线的逐格 diff**：`git show HEAD:tools/oda-compare/gate-baseline.json`
   vs 基线（json 逐格比）——**钉之前**先量出「这一枚底相对 HEAD 已滞后多少位移」，**钉之后**再量一次确认吸收面
   没扩大。按节（`cross` / `cross_producer` / `recopy` / `rich`…）列位移格并**三分类定性**——
   `本手翻格 / 别道已入库未钉 / 仅 msg 签名前移`——逐格写进提交信息与快照行。**「别道已入库未钉」是常态不是异常**
   （见 A8）：既非抢功也非回归，但提交信息里必须**点名是哪几格、来自哪个 sha**，不许只写一个合计位移数。
3. **纯增性自查**：主面 `cross` 与 `recopy` 逐格**零位移（含 `A_msg`）** ⇒ 证明 bless 只吸收了该吸收的，
   没碰别的面。
4. **读数标底**：每个计数写明属**起手底**还是**落库底**；共享主树里同一棵树的读数可能**多次换底**
   （并行手在你落库前又进了几档，本手富图生产者面实测 `19/30 → 23/30 → 25/30` 就是同一棵树换底三次）⇒
   两底不同要在后一笔提交/账档里**显式改口**，不许两个数并存不说明。

> 实证（§550 · 读侧一行判据改「值退出、只核均匀性 + `padBits>7` 仍拒」）：bless 后 14 格位移 =
> 本手 4（`AC1018→AC1021/1024/1027/1032`）+ 别道 6 + 签名前移 4；主面 `cross` A/B 30/30、`recopy` 6/6
> 含 `A_msg` 逐格零位移；起手底 105/105、落库底含并行手新档 112/112，提交信息分笔标明属哪枚底。

> 反证（§556 · 富图读侧 `TrySkipEed` 少吞 198 位，改动合入新底 `ea8fdc8c`）：整门在新底上 `GATE_RC=0`、
> **零位移、零 `[改进]`** ⇒ 该读侧改动不动任何产物字节，**这一手根本不需 bless**（「不在场证明」在 A7 同样成立）。
> 但 §553 的 VISUALSTYLE 刀此前已落库却**没动 `gate-baseline.json`**，下一道 bless 会替它入钉——这正是 A8。

⇒ 判据：**bless 不是抹平。** 位移不归因，下一个审计 backlog 的人就会把别道的翻格记在本手账上。

### A8. 基线行话会滞后：选靶/报数前先取**今日活面真值**；「已翻未钉」由下一道 bless 入钉是常态

门基线 `gate-baseline.json` 与账档行话描述的是**上一次 bless 那一刻**的数，不是今天的数。别的道 fix
落库后通常**不动**基线（§552/§554 皆如此——改完直接进 `main`，基线留给下一道 bless 顺手吸收），于是
冒出两条常见误判：

1. **照基线行话选靶 ⇒ 靶已腐**。基线注着 `rich 53/60`，活面实测可能已经 `54/60`（并行手刚补上一枚却没入钉）。
   选靶/报数**之前**先跑活门取今日真值，别拿基线注释里的旧坐标当现状：
   ```bash
   # 富图/生产者面活红格：先跑活门拿今日真值，再定本手要翻哪几格
   ODA_SINGLETON=off python tools/winbuild.py python tools/oda-compare/gate.py --no-build 2>&1 | grep -E "\[改进\]|REJECT|NO_OUTPUT|CRASH"  # 以活门读数为准，不以基线行话为准
   ```
   账档 / commit 里报的每一枚计数都标「实测于 <sha> <日期>」，而不是「基线写 <某值>」。
2. **把「别道已翻未钉」错当抢功或回归**。你这次 bless 吸收了别人已入库未钉的翻格，**既不是抢功也不是引入回归**——
   那是该吸收的。规矩不是「回避吸收」，而是**点名**：位移表里逐格写清 `本手 / 别道<§xxx·sha> / 仅签名`，
   让下一个审计 backlog 的人一眼能分账（与 A7 步 2/4 同一动作）。

> 实证（§556 手，同一棵共享树）：起手底 → 合入 §553 新底 `ea8fdc8c` → 落库底，富图生产者面三次读数
> `19/30 → 23/30 → 25/30`；三次是同一棵树换底，不是三套判据。§553 的 VISUALSTYLE 刀落库时未动基线，
> 本手 bless 才把它一并钉进——归因为「别道 §553 已入库未钉」，不记在本手账上。

⇒ 判据：**基线是历史快照，不是今日现状。** 选靶前先量活面，报数时先标底；bless 替别道入钉是接续，代价是必须点名。

## B. 语料缺席类失败

### B1. 先定性：是**在途编辑**还是**已落地口径变更**

```bash
git status --porcelain | grep -E "^ ?D"          # 删除项（worktree 未提交 = 在途）
git ls-files <目录>                               # 已在 HEAD 的（含替补文件）
ls -l --time-style=full-iso <目录>                # mtime 落在本手期间 = 别人正在改
```

**硬判据**：某替补文件「已入库但 worktree 里是 `M`」且 mtime 落在本手期间 ⇒ **并行 lane 正在做换正**，
本手**不插手**（插手必然撞车，改完也会被对方提交盖掉）。

> 实例：`最简各版本图纸/empty_2000.dwg` 已入库却显示 `M`、mtime 落在本手期间 ⇒ 判定为并行在途，
> 余下 ~872 个失败**一个没动**。

### B2. 裁决：**不许 `git checkout` 恢复被删夹具**

若用户裁定「不许恢复删除文件」，只能走两条路：**改挂到已有的同内容副本**，或**退役依赖它的判据档**。
判据口径变更须用户裁定，不得自行推进。

**修法已在途时：只出证据、不接手。** 若同一枚红**同时**挂在改动树与基线树（两树总测试数不同只因落在不同 sha），
且 owner lane 的 `M` 测试档 + 配套语料动作**已经在 worktree 里、只是没提交** ⇒ 本手不写第二条修法。
证据取法：把**他们的**测试档 + **他们的**替补夹具拷进隔离树，`dotnet test --filter` 单跑该类到全绿即可
（不改他们一个字节，也就不会被他们的提交盖掉）。

**删语料前先量面**（这条是「恒等门会不会变成假绿」的输入）：

```bash
for f in 2000 2004 2007 2010 2013 2018; do
  echo "$f -> $(grep -rl "最简各版本图纸/$f.dwg" tests/ | wc -l) 个测试文件"; done
grep -n "SRCS\|TGTS" tools/product_fingerprint.py    # 被删档有几枚在恒等门进料表里
```

缺产物的行两侧**同时缺** ⇒ 旧脚本会照样报 IDENTICAL。恒等门现以 `MISSING`/`NO_OUTPUT`/`TIMEOUT` 或行数不满
判**退出码 2 = 判据作废**（不是恒等、也不是红），读输出必须看到 `（66/66 行）` 才算这条判据成立。

### B3. 改挂前先对拍字节

```bash
git hash-object <候选副本>                 # vs
git rev-parse HEAD:<原路径>
```

**同字节 ⇒ 改挂是零语义变化，判据值不用动**；不同字节 ⇒ 等于换语料，断言值需全部重取（非机械改名）。

### B4. 改挂只做**单 token 替换**，别写批量重写脚本

- **反例**：脚本批量把 `Rich("add_…")` 改挂到 `TestData("add_…")`，27 个文件需补插 helper，
  正则匹配到不完整定义、helper 插错位置 ⇒ 42 个 CS 错误，只能全部还原。
- **正例**：先确认目标目录下**文件名同名**，则只需替换**目录常量**这一个 token；
  若目录下还混着**没被删**的文件族，只能**按文件名前缀分派**，绝不可整体换目录常量。

### B5. 换挂「同族另一枚图料」前，先验它**是不是同一枚图**

名字不算证据，两件事必须现场量：

```bash
head -c 6 <候选>                                  # 版本魔数（AC1015…AC1032）
python tools/winbuild.py dotnet <Cli>.dll <候选> --census | grep "^CENSUS file"
```

- 带 `empty_` 的**不一定**是 0 实体空图（本手实测 `empty_2000.dwg` = `records=213 · dimstyles=3 · blockfields=3/3`），
  旧档里「`empty_*` 顶位 = 静默假绿」那句只对它写作当时的形态成立 ⇒ **引用坑档也要重测**（同 C0）。
- 版本对、族数对，**也不代表是同一张图**：`2000.dwg` records=196 vs `empty_2000.dwg` =213 ⇒ 两张图，
  跨图逐字节对拍类判据当场失效（→ 走 A6 的退役支）。
- ⇒ 换挂档（如 `CorpusPaths.cs` 的注释）**必须写下替身是谁、与原图差在哪、哪些判据因此改走退役**；
  不写，下一手就会把「不同图」当「同图」去重钉。

### B6. 批量**退役**判据的切法：按缩进 / 分号取尾，且必须计数对账

- **反例（本手实踩）**：用裸括号计数取方法尾 ⇒ 内插串 `$"…{x}…"` 与**表达式体** `public void X() => …;`
  都会让它错位，一次多吞 14 法、把 5 个文件清成只剩注释。**`dotnet build` 仍然 0 错误**
  （整档只剩注释是合法 C#）⇒ **构建绿不是退役面的验收**。
- **正例**：块体取「与声明**同缩进**的那一行 `}`」为尾；表达式体取「第一个以 `;` 结尾的行」为尾；
  动手前 `cp tests/DwgParser.Tests/*.cs out/_backup_<date>/`，动手后**逐档计数对账**：

```bash
# 预期删除枚数 == 实际删除枚数（对备份数 public void），再谈 build
for cls in <退役档>; do echo $cls $(grep -c "public void" out/_backup_<date>/$cls.cs) $(grep -c "public void" $cls.cs); done
```

- 退役原位要留**可 grep 标记**（本仓用 `RETIRED <日期>`）+ 备份路径 + 详档指针；一行「为什么不是
  重钉能了结的」比删干净更有价值 —— 被删的判据只有靠这条注释才可能被下一手复原。
- 别顺手删**并行手在途的红**：先按「起手是什么红」分箱（语料缺席 / 值不匹配 / 在途），
  只处置本手造成的那一箱（配方：拿改前改后两份 trx 按测试名做差）。

## C. 通用硬约束（两个链路都要守）

### ★ C0. 不许用**记忆**推断既有代码 —— 先取真代码

本 skill 的诞生就带着这条教训：立账档初稿把 `Matrix` 的病根写成「`operator*` 线性部分按行向量、
平移部分按列向量」，**恰好写反**。真相（`git show <rev>:<path>` 实取）是：
`Apply`/`Rotation`/`YAxisScale` 按行向量写，而 `operator*` 的**线性部分**是标准矩阵乘
（`A_lin·B_lin` —— 这就是**列**向量语义、且正确），只有**平移部分**是转置式。

**判据**：凡要在文档 / 注释 / 汇报里**描述**既有代码的行为（尤其「哪一部分按哪套约定」、
「哪个值是谁算出来的」），必须先取真代码：

```bash
git show <提交>^:<路径>        # 改前
sed -n '<起>,<末>p' <路径>     # 改后
```

**读注释不算依据** —— 注释本身可能就是错的。本项目已实证两例：`BlockPlacementMatrixParityTests`
的定义式注释、`DimensionFamilyRendererTests` 的 golden 注释，都曾与**实际实现同错**
（这正是「同错即绿」得以长期隐身的原因）。凭印象写出的「病根」比不写更危险 —— 它会被后人当权威。

### C1. CRLF 保护

`Edit` 工具会把 CRLF **规范化成 LF** ⇒ 动 `src/` 与 CRLF 版文档前**先探行尾**：

```bash
python -c "b=open(P,'rb').read(); print('CRLF',b.count(b'\r\n'),'LF',b.count(b'\n'))"
```

- `CRLF == LF` ⇒ 纯 CRLF，**禁止用 `Edit`**，改走 Python 字节级替换（old/new 都按 CRLF 拼）。
- 纯 LF（`CRLF == 0`）⇒ `Edit` 安全。
- 改完**必须核验** `CRLF 数 == LF 数`，否则会引入裸 LF。

### C2. 多枚替换要**倒序**

按绝对下标逐枚替换时，改前面那枚会让后面那枚的下标失效 ⇒ **从后往前**改。

### C3. 不许绕过 testhost 单例闸

`DWG_TESTHOST_LOCK=off` 是逃生口，**不是许可**。同机多 testhost ⇒ 读数不可复现，
且会与并行 lane 抢资源、污染**双方**读数。撞锁就等（默认超时 2400 s）或如实记为「未跑成」。
注意：Windows 上等待方可能撞 `_open()` 的 `PermissionError` 而非走等待分支 —— 那是工具层现象，
**不要**因此去强杀别人的进程，也不要改闸门脚本。

### C4. 收尾核对

```bash
git diff --stat 593d4ee7 HEAD -- <本手文件>   # 合并是否触碰过本手文件
git merge-base --is-ancestor <本手提交> HEAD  # 修复是否还在祖先链上
```

提交走 `git commit --only -- <本次文件>`（硬约束 7），只落本地 `main`，**推送须当轮点名**。

### C5. 门报 `[REGRESSION]` 时，先做「不在场证明」再决定要不要修

多 lane 并行时，门跑的是**工作区产物**（含别人的在途改动）⇒ 红很可能不是你的。**别立刻去修。**

四步取证：

1. **给失败分类**：`Object improperly read` / 字节差 ⇒ 读侧·写侧；带像素 / `viewBox` / 图元字样 ⇒ 渲染侧。
   先确定是**哪一侧**的红。
2. **查门用的入口是否引用你改的代码** —— 这是最强的一条，**「不可达」比「没影响」硬**：
   ```bash
   grep -rn "CurveFamily\|AxisScale\|ToSvg\|RenderContext" src/DwgParser.Cli/*.cs
   ```
   零命中 ⇒ 你改的东西在门的执行路径上**不可达**，门红与它无关。
3. **查基线原值**，区分「真回归」与「基线过期」：
   ```bash
   python -c "import json; d=json.load(open('tools/oda-compare/gate-baseline.json',encoding='utf-8')); print(d['cross']['AC1015->AC1018'])"
   ```
   基线 `OK` + 现跑 `REJECT` ⇒ 真回归；基线本就是 `REJECT` ⇒ 只是基线未同步。
   **基线过期一族的正解在 A7**：bless 不是抹平，位移要逐格归因（先非 bless 验零回归再钉）。
4. **查工作区在途文件**归因：`git status --porcelain -- src/` —— 失败所属侧的源文件若为 ` M` / `??`
   ⇒ 大概率是并行 lane 在途。

**结论怎么写**：给完整证据链，并**如实标注是「逻辑排除」还是「实测排除」**。
实测隔离（还原自己的改动再跑门）通常要动别人的在途文件或另建干净树 ⇒ 代价高且干扰并行；
逻辑排除已足够时**不要为了好看去做实测**，但**必须写明没做**。

### C6. 隔离树的可跑前置：新树里**没有 `out/`**，命令名是 `python`

```bash
git worktree add --detach "D:/Project/DwgParser/out/_x" <sha>
mkdir -p out/_x                                                   # ★ 重定向 / 临时 dump 前先建
cd out/_x && python tools/winbuild.py dotnet build DwgParser.sln -t:Rebuild
```

- 产物目录 `out/` **不入仓**（`git ls-files out/` 空）⇒ 主树里「顺手 `> out/x.log`」在刚建的隔离树里
  **必失败**；后台跑尤其隐蔽——任务“完成了”，日志一份都没落，只能重跑。
- Windows 侧一律 `python`，**别照搬 unix 习惯写 `python3`**（本仓所有配方即 `python tools/winbuild.py …`）。
