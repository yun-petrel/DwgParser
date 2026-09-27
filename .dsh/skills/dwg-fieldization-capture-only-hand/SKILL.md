---
name: dwg-fieldization-capture-only-hand
name_en: DWG Fieldization Capture-Only Hand
name_zh: DWG字段化轨道capture-only一手
description: Reproducible manual for one DwgParser fieldization-track capture-only hand — read-side capture and named diagnostics only, Kind/bit anchors/StringFields untouched so product bytes stay byte-identical. Covers live --census picking (lists rot; captured= is any-axis, name per-axis with a ProbeTool <cls>cap subcommand), same-base dual worktrees, probes mirroring production BitReader guards, the remove-load-bearing-bit control run, handoff of an untouched value gate, and the three judgment gates with field-tested substitutes — identity without baseline.txt by knife-on/knife-off product sha dumps in one changed tree (doubling as that control run), pre-existing reds at the tip judged by unchanged red count + empty red-name diff + pass delta equal to new tests, and an occupied ledger doc handed to the next clean hand as a paste-ready batch section in the detail doc. Then two-commit landing and cleanup self-proof. Use for 收口未解析字段、早退具名、恒等门无 baseline、tip 带存量红、账档被别道砍着. Not 方向A翻格取证 nor 判据放宽.
description_en: Reproducible recipe for one capture-only fieldization hand (live --census target, same-base dual worktrees, probes mirroring production BitReader semantics, a control run that must go red, three judgment gates plus their substitutes for a missing baseline.txt, a tip with pre-existing reds and an occupied ledger doc, two-commit landing, cleanup self-proof).
description_zh: 字段化轨道 capture-only 一手的可逐字复跑作业手册，现跑普查选靶（`captured=` 是「任一轴提交」口径，要逐轴点名给 ProbeTool 加 `<cls>cap` 子命令）、同基座隔离双树量读数、探针照抄生产 BitReader 语义落码、撤承重位必见红的控制实验、留手术轴值闸的交接约定（翻它=哪几格什么判据、闸值归号附实测出处或「值不作判据」、定性前先验刀咬没咬）、三条判据（恒等门／全量计数差／gate 无回归）各带一条实测替身（取不到 baseline.txt 就在同一棵改动树做「装刀／撤刀」两态产物 sha 对拍并兼职控制轮、tip 自带存量红就改判红数不变＋逐名 diff 为空＋通过数差等于新增例数、账档被别道砍着就把批次段原文写进详档待粘贴段而本手不提该档）、码与档两笔提交、清理自证。用于收口未解析字段一笔、给早退具名、留闸要交代翻格与闸值出处、恒等门无 baseline、tip 带存量红、账档在途、或落 capture-only 读侧捕获。
argument-hint: 给出类名与 corpus-gap 枚数，如「收 _MLEADEROBJECTCONTEXTDATA_CLASS 30 枚」
argument-hint-en: Name the class and its corpus-gap count, e.g. take TABLECONTENT thin 4 cells
argument-hint-zh: 指定类名与枚数，如「531 薄形 4 枚具名化」
user-invocable: true
---

# 字段化轨道 · capture-only 一手（capture-only fieldization hand）

## 0. 先判该不该走本技能

本车道只干一件事，把某类「未解析字段」做成**只读侧捕获 / 纯诊断面**的一手。硬边界：

- **不改 `Kind`、不设位锚、不写 `StringFields`、不动句柄流面** ⇒ 转换产物**逐字节不变**（这是 capture-only 的立身之本，也是恒等门能过的前提）。
- 判据本身（§213 那类**过严闸**）不动。若收口需要**放宽判据**（如「去锚化定谱」）⇒ **须用户裁定**，不在本技能自主范围；本技能只做到「已具名 / 已判未定谱入 §3」为止。
- 与 `dwg-direction-a-cell-forensics` 的分界，那技能是**方向 A 翻格取证**（会产位锚、动搬运语义）；本技能**零搬运语义改动**。

## 1. 选靶：**必须现跑普查，禁引用旧名单**

多 lane 并行会让名单在几小时内陈旧。起手先现场重跑全语料普查（`--census` 的确切调用形制见 `tools-and-fixtures.md` §0，先探 `dotnet`/ODA 有无再按文档断言）：

- 取 `corpus-gap` 降序里**最大且不需放宽判据**的一笔。
- 逐行核 `captured` / `rejected` / `fails[<reason>]` / `slots` / `opa`。判据（批次 87 已钉），`fails[<none>]` 全域应已归零 ⇒ **再出现 `<none>` 就是本手新引入的缺陷，直接当红看**。

**先分两种病**（批次 85/86 教训，别混）：
- `captured=0 且 rejected=0`，可能是「**有路、路走了、无声退出**」（早退不建袋不盖章）⇒ 可具名化自决；也可能是「**根本没有路**」（`Canonical` 未登记 ⇒ logical 偏到 `type+1000` ⇒ `default:` 整帧 Opaque、`slots` 空）⇒ 那是定谱题，先探外部权威（libredwg 有无成文段 / ODA 直导 DXF 能否解出具名组码）。

**`captured=` 是「任一轴提交」口径**：一类多轴时，只要有一枚轴提交过就计 captured ⇒ 它答不了「本手要的那一枚捕没捕到」。要**逐轴点名**，给 `ProbeTool` 加一枚 `<cls>cap` 子命令（§550 的 `mlscap`：逐枚 507 帧打印各轴捕获位 + pad 值即此形）。工具工程不在 sln 里，照 §2/§5 先 `dotnet restore` + `-t:Rebuild`。没有这枚探针，「翻格病根在哪一轴」极易归错（§550 前手就把它归成了「缺整个 reader」）。

## 2. 隔离双树：读数**只取纯净 worktree**

主树此刻常有并行手大量在途（本会话实测 90+ 测试文件 + 别手 `src/`）。门与全量跑的是**工作区产物** ⇒ 直接读主树数会污染双方。

```bash
git worktree add ../<base 树> <base-sha>     # 钉 base，量基线读数
git worktree add ../<改动树> <base-sha>     # 同 base，落本手改动后量新读数
```

两棵树**同 base**，逐格差值才是本手的账。工具工程（`ProbeTool`/`DvWalk`/…）不在 sln 里，新 worktree 还须先 `dotnet restore` 该工程。

## 3. 落码：探针/走查**照抄生产 BitReader 语义**

一次性取证探针必须逐闸复刻 `DwgObjectReader.<族>.Decode*` 的守卫序与游标语制（如走查跑在**游标复位的第二次读**上）。**读注释不算依据，注释本身可能就错**；要描述既有行为先 `git show <rev>^:<path>` 取真代码。

落诊断面的标准形（批次 85）：
- 建袋提到**射程入口**（原本 `??= new` 写在五道守卫之后 ⇒ 早退既无袋也无归因，普查呈 `captured=0 rejected=0`，与「口径外」长得一模一样）。
- 每处早退写**精确文案**（点位到「声明 N 位内仅 0 枚锚」），`Dispatch` 补一枚 `StampSilentReject` 兜底章（走了多少位 = 结构指纹，同堆 = 同处守卫，语料级可比）。
- 兜底章只写 `FailReason`/`FailBit` 两支**诊断字段**（反射按名取用），不参与任何判据位 ⇒ 产物字节恒等。

**留闸（手术轴值闸）的交接约定**——本手刻意不动的那道判据闸，对后面的方向 A 手是三样东西，缺一即误导下一手：

1. **翻格交接**：一枚值闸在方向 A 手里可能**一格都不翻、也可能一枚翻四格**（例：507 帧尾 7 位字节对齐余量
   旧闸「必须全零」⇒ §550 让 `AC1018→AC1021/1024/1027/1032` 四格齐翻）。留闸时把「翻它 = 哪几格、什么判据」
   写进捕获袋文档与账 §3，别让它以「永久不动」的面目挡在后面翻格手的路上。
2. **闸值归号要附实测出处**：「这个值随哪家写手」这类归号**极易写错**——§550 前手记成「AutoCAD 填 0、
   ZWCAD 填 1」，实测五档 AC1018 语料两值皆有（AutoCAD 亲手空图 `0x7F`／ZWCAD 亲手空图 `0x00`／AutoCAD
   富图 `0x00`／ZWCAD 富图 `0x7F`），既不随写手也不随体量 ⇒ 错误定性直接长成「缺整个 reader + 写侧真变换」
   的一整手量级。定谱表要么**逐档附实测出处**，要么只写可复核判据（如「值不作判据、只核同一位值连跑的
   均匀性 + `padBits > 7` 仍拒」）。
3. **定性前先验「刀咬没咬」**：拿零成本尺子量（`DWGDBG=1` 跑 `--convert` 数具名章计数，或 `<cls>cap`
   逐轴读数，见 §1）。**章 0 枚 = 刀没咬**，别据此立项建 reader——「同档一面绿一面具名红」的第一问永远是
   刀没咬，不是刀错。

## 4. 控制实验：**撤掉承重位必须见红**

绿不算自证。做**负控**，把一处具名退回静默 / 撤掉一枚承重守卫位 ⇒ 该红的必须恰红（批次 85，退回一处 ⇒ 恰 4 红，且红样只剩兜底章文案 ⇒ 具名与兜底章同时承重；批次 83 撤 `has_ds_data` ⇒ 22 例恰 10 红且全落 AC1027/AC1032）。

**复原走「文本重贴 + `touch` + `-t:Rebuild`」，禁止 `.bak` 覆盖复原**，`.bak` 会把 mtime 退回 ⇒ 增量 build 静默跳过 ⇒ 后续全量跑在**错的 dll** 上（批次 83 坑）。控制轮**必见红、计数必变**，否则实验没做成。

**一轮交两条账（§578）**：§5 判据 1 的「装刀 / 撤刀」两态产物对拍天然就是这一把控制轮 —— 两态 sha 逐行相同（恒等）+ 撤刀态判据恰 N 红且红样只落本手靶面（承重）⇒ 不必再单独安排一次全语料跑。实测 210/210 `IDENTICAL` + 撤两行登记 ⇒ 恰 9 红（①×6 + ③×3，邻档 7 枚仍绿）。

## 5. 三条判据（逐条都要；①② 各带一条实测替身）

1. **恒等门（跨树）**，`product_fingerprint` 在**跨 worktree 场景拿不到 `baseline.txt`** ⇒ 只能对**两棵树各自 dump 的 txt 做手工 diff**，要求逐行 `IDENTICAL`（批次 85，66/66）。零 `src/` 改动的手本门天然过，但落码手必查。
   **恒等只在「跑全了」时才算数**：并行 lane 删语料期间，两侧会**同时缺同样的行**，脚本若跳过缺产物行就能报出**假 IDENTICAL**（转换链压根没验）。⇒ 现版脚本已把 `MISSING`/`NO_OUTPUT`/`TIMEOUT` 或行数 ≠ `SRCS×TGTS` 判成**退出码 2 = 判据作废**（既不是恒等、也不是红），并会揪出**基线自身含缺产物行**的情况。读输出要看到 `（66/66 行）` 那一段才算这条判据成立。

   **替身 · 跨树 baseline.txt 根本没有时（§578 实测，比跨树更省一棵树）**：别求第二棵树，在**同一棵改动树**做「装刀 dump / 撤刀 dump」两态对拍，这一把同时就是 §4 的控制轮。
   ```bash
   python s<§>_fp.py on     # 装刀态：全语料 --convert 落 out/fp/，逐格 sha256[:20] → out/fp_on.txt
   # 撤刀 = 文本重贴删掉本手那两行登记 → python tools/winbuild.py dotnet build DwgParser.sln
   python s<§>_fp.py off    # ⇒ out/fp_off.txt
   ```
   脚本形制：复用 `tools/oda-compare/richverify` 的 `set_files()` + `our_convert()`，6 集 × 6 档取源（实测在账 **42 枚**）× 5 目标（跳过与源同档）= **210 行**；**开跑即 assert `rows == SRCS×(TGTS−1)` 并打印 `FULL`**，否则上面那条防假 IDENTICAL 的护栏失效。两态 `sorted` 行 diff 为空 ⇒ 产物逐字节不变（本手实测 210/210 `IDENTICAL`）。

2. **全量测试计数**，基线树 → 改动树通过数差 **== 本手新增判据档数**（批次 85，3818 → 3827 = +9）。
   **「两侧 `失败 0`」只是 tip 干净时的特例**：tip 本身带存量红（§578 基座 `7fb121e8` = **27 红**）时，把这条换成三件同时成立 —— **同基座双树红数不变** + **红集逐名 diff 为空** + **通过数差 == 新增例数**（实测 4,134/27红/5跳 → 4,150/27红/5跳 = 恰 +16 = 本手 16 例）。逐名尺子：
   ```bash
   for f in <基线树>/out/suite_base.log <改动树>/out/suite_change.log; do
     grep -E "^[[:space:]]+失败 DwgParser" "$f" | sed 's/\[.*//; s/^ *失败 //; s/(.*//' | sort -u > "${f%.log}.rednames"
   done
   diff <基线树>/out/suite_base.rednames <改动树>/out/suite_change.rednames   # 空 = 红集逐名相同
   ```
   坑，红**数**按输出行计（27），红**名**经 `sort -u` 去重后枚数不同（15，参数化例共名）⇒ 两个数分开报，别写成「27 枚红名」。
3. **`gate.py`（只读比对，勿随手 `--bless`）**，`A x/x · B x/x · recopy x/x · 富图 …` 与前一手逐格一致 ⇒ **无回归 PASS**。

跑门/构建三条必守，一律 `python tools/winbuild.py …` 包装、`dotnet build` 必带 `DwgParser.sln`、改过 Core 后**逐个** `-t:Rebuild` 不在 sln 里的工具工程。撞 testhost 锁 ⇒ 等（默认 2400s）或如实记「未跑成」，**不强杀别人进程、不改闸**。`NO_OUTPUT`/`CRASH` = 门没跑成，不是门红。

## 6. 两笔提交：**码档 + 账档**（账档在途时第二笔 = 详档）

账在 `docs/fieldization-track.md`（批次段 + §4 现势行 + 必要时 §2/§3 迁移）。**该档当下干净才提它**，被别道砍着则走下面第二条。

```bash
# 主树脏（并行手在途）时按文件级 scoping；-m 必须放在 -- 之前
git commit --only -m "feat(字段化 · 批次 N): …" -- <src改> <tests新>
git commit --only -m "docs(字段化 · 批次 N 立账): …" -- docs/fieldization-track.md
```

- **新建文件先 `git add` 再 `--only`**：`git commit --only -- <未跟踪新文件>` 直接 `error: pathspec … did not match any file(s) known to git`（§578 判据档与详档各撞一次）。
- **账档只在它当下干净时才提第二笔**。`git status --porcelain -- docs/fieldization-track.md` 呈 ` M` ⇒ 别道正砍着它，此时**不要 `--only` 该档**（`--only` 是**文件级** scoping，一提就把别人未竟的重构整文件烘进历史）。改提**本手详档**为第二笔：
  ```bash
  git add docs/issues/<date>-<slug>-<§号>.md
  git commit --only -m "docs(§N 立账 · …): …" -- docs/issues/<date>-<slug>-<§号>.md
  ```
  把**可直接粘贴的 `fieldization-track` 批次段原文**写进详档的「待粘贴」小节（§578 = §五），并在**详档首行与提交信息里都显式声明**「本手不带该档，交下一条干净动它的手带走」（先例链 §568 / §571 / §577 → §578）。别默默略过——不写这句，下一手会以为该批次段没人欠。
- 码档（`src/`+`tests/`）与账档分两笔；只落本地 `main`，**推送须用户当轮点名**。
- 落库前先核「本手文件自 base 起未被并行手动过」（`git diff --stat <base> HEAD -- <本手文件>` 空 ⇒ 可安全同步）。
- 若工具改动（如 `tools/DvWalk` 的选取器 `TypeCode==531`→`LogicalType==531`）已验未落库，另起一笔 `fix(工具 …)`，与本体分开。
- 本手留了手术轴值闸（§3 交接约定）时，批次段给那枚闸带上**闸值归号**：逐档实测出处，或一句「值不作判据」的口径。

## 7. 清理自证（收尾必做）

```bash
git status --porcelain -- <本手文件>            # 主树我的文件净（空 = 与 HEAD 一致）
git worktree remove ../<base 树>                # 清自家隔离树
git worktree remove ../<改动树>                 # 探针树 / out/ 草稿删净
git merge-base --is-ancestor <本手提交> HEAD    # 提交确在 main 祖先链上
```

汇报口径，给**类名 + 双树 base + 三判据读数 + 提交 sha + 边界**（哪些仍未收、卡在判据放宽还是待外部权威），零含糊。`fails[<none>]` 若非空 ⇒ 如实当缺陷登记，别默默略过。读数是**当轮快照**，引用带格数 + sha + 是否干净树。
