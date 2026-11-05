---
name: dwg-direction-a-cell-forensics
name_en: Direction-A Cell ODA Forensics
name_zh: 方向A单元格ODA互认取证
description: Reproducible forensics pipeline for DwgParser Direction-A cross-version DWG cells. Sets ODA_EXE and build/test/gate baselines, regenerates the oracle and our frame by CLI --convert, enumerates divergent frames with ProbeTool firstdiff, tells benign renumbering from real blockers, drills per-handle with objfields. Governs landing verified hands in the shared main tree and the shortest flip path (widen a single-target gate). Use when continuing Direction-A cell flipping, locating ODA "Out of range" / "improperly read" / "needs recovery" signatures, choosing which rich cell is still open, or diffing our output vs the oracle. Carries container-vs-frame triage (section ladder with size self-proof, bypass-all sentinel), Step 6d per-family fatal map, dual-graft sufficiency, instruments that lie, and two selection rulers - the on-demand 12-set rich census (360 cells, isolated tree only) and Step 6e green-twin donor fatal set (objsize join + fatalset.py ddmin, four-side COUNT check first). Plus a verdict self-check for the forensic tools themselves (chainwalk / inter_bisect must reject a known-BAD output dir before any chain or bisection reading counts) and the "plan never bit" why= read recipe. Also Step 6g bit-account triage for a bare "Invalid input" after the fatal set is narrowed to one family (trace-to-rule-out-the-unlanded-knife, same-reader semantic diff vs the ODA-resaved native, per-section bit-anchor delta instead of seed-extend alignment, and the native-target-corpus criterion - our own variable-length BL/BS/BD make a wrong shape look like a shorter frame, never a failed decode). Plus Step 6h for the class of debt the gate cannot see at all (ODA back-compatibles an un-upgraded cross-version shape, so every cell stays OK while our own reader falls back to Kind-only): a three-face self-check - native same-content version pair for shape, per-handle semantics vs the source, and a measured bit account where only the target version varies, with knife-ablation as the control.
description_en: Reproducible forensics pipeline for DwgParser Direction-A cross-version DWG cells (ODA oracle regeneration, CLI convert, firstdiff enumeration, benign-vs-real classification, objfields drill-down, control-run-as-sentinel, per-family fatal map, necessary-vs-sufficient dual test, single-shot container section ladder). Adds Step 6g bit-account triage for a bare "Invalid input" once the fatal set is one family: trace first to rule out an unlanded knife (a closed source-side walk proves nothing about the target shape), same-reader semantic diff against the ODA-resaved native, per-section bit-anchor deltas instead of seed-extend alignment, and a native-target-corpus criterion because our own variable-length BL/BS/BD turn a wrong shape into a shorter frame rather than a failed decode.
description_zh: DwgParser 方向 A 跨版本单元格（AC10xx→AC10yy）的可逐字复跑取证流水线：设 ODA_EXE、建立 build/test/minverify/gate 基线、ODA 直转源图重生成 oracle、CLI --convert 产我方帧、ProbeTool firstdiff 全帧枚举并区分良性句柄 renumber 与真结构拦路、objfields 逐句柄锁位。用于继续方向 A 翻格、定位 ODA Out of range / improperly read / needs recovery 签名、或产物与 oracle 逐帧对拍时；另含「翻一格最短通路」= 放开只写死单一目标档的写侧闸 + pre-R2004 两句换形（不发 is_xdic_missing、句柄流 owner 后补 (3,0) 硬主槽）。红格起手先跑两把尺（绿双胞对照 + `bypass all` 控制档），再决定去容器层还是帧层；Step 6d 钉「控制档 = 每落一把刀必复跑的哨，REJECT 非永久判决」、逐族致命图（`^@该族句柄文件`）、必要≠充分的对偶白名单测，以及三件会造假结论的器械（规格只命中 inter、widths 漏收无句柄流类、ODA 具名不稳定）；Step 6b 另含容器节级 ladder 正证法（逐枚单发 + `SWAP … -> NB` 大小自证）。选靶前可跑按需全 census（12 集 × 30 = 360 格，只能在隔离树跑）；Step 6e = 绿双胞供体最小致命集（`objsize` 两侧 join + `fatalset.py` ddmin，起手先做四侧逐类枚数对照）；另加 Step 6g 位账定谱（ODA 具名警告消失、只剩裸 `Invalid input` 且致命集已收到一族时）：先用 trace 排「刀没落下」（★ 源侧走查闭合不证目标形制）、同一把读者对表「我方产物 vs ODA 原生另存件」的语义字段、用**分段位锚**做 Δ 差分而不用位串 seed-extend 对齐（CED 夹 preview + 取值差混在位差里）、判据钉**原生目标档语料**同形而非自家往返（本仓 BL/BS/BD 变长 ⇒ 形制错只表现为帧变短，自家恒绿）。
argument-hint: Name the cell to investigate, e.g. "方向A互认取证 AC1024→AC1021"
argument-hint-en: Name the cell to investigate, e.g. "Direction-A forensics AC1024->AC1021"
argument-hint-zh: 指定要取证的格子，如「取证 AC1024→AC1021」
user-invocable: true
---

# 方向 A 跨版本单元格 ODA 互认取证

目标：让任何人逐字复跑一整个格（SRC→TGT）的"产物 vs oracle"取证，产出**分类差异台账**（真拦路 / 已知良性 / 良性 renumber / oracle 污染不可追），指出下一步修复点。前置：遵守项目 AGENTS.md 硬约束与 `dwg-parser-map` 技能（定位代码用）。

## Step 0 环境预检（每轮先跑，勿凭文档记忆路径，**也勿凭文档断言有没有**）

宿主可换 ⇒ `dotnet` 与 `ODAFileConverter` 的有无**必须现场测**；历史记录里两种情形都出现过
（「Linux + 无 dotnet/无 ODA」与「Windows + ODA/`Teigha/` 齐备」）。探不到不是项目缺陷，但也**不许**把「探不到」升级成「本仓不支持」。

```bash
# ① dotnet 在不在（不在 ⇒ 本 skill 全流程不可跑，退 offline 判据）
command -v dotnet || ls -d ~/.dotnet 2>/dev/null

# ② ODAFileConverter：Windows 侧**只用仓内** `tools/ODAFileConverter/`（仓外候选表已删，2026-09-23 裁定）
#    权威发现器 = `tools/oda_path.py::find_oda()`（脚本带 `__main__`，实测打印仓内 exe 绝对路径、exit 0）
python tools/oda_path.py                       # 打印仓内 exe 路径；容器/Linux 走 `tools/setup-container-env.sh oda`
export ODA_EXE="$(python tools/oda_path.py)"   # 一切 minverify/gate/ODA 直转的前置；禁按历史文档断言，须现场取

# ③ 让门自己确认（非 None = 就绪；本仓另有 Teigha/ 为本体）
python3 -c "import sys;sys.path.insert(0,'tools/oda-compare');import gate;print(gate.ODA)"
```

- 两者齐备 ⇒ 按本 skill 全程走（`gate`/`minverify`/`richverify` 是真值门）。
- `ODA_EXE` 探不到 ⇒ 本 skill 的 Step 2 起（oracle 重生成、`gate`、`minverify`）**不可跑**，
  改用离线代理判据（`tests/DwgParser.Tests/OfflineByteCriteriaTests.cs`、
  `ProbeTool firstdiff <我方产物> 最简各版本图纸/<同内容目标版本>.dwg 0`），
  并在汇报里**写明是「探不到 ODA」而非「门跑红」**（`docs/handbook/pitfalls.md` C22）。

## Step 1 基线三连（计数以即时结果为准，不信文档）

```bash
dotnet build DwgParser.sln -v q                          # 必须 0 错误；改了 Core 必须连 Cli 一起 build（旧 dll = 假阴性）
dotnet test DwgParser.sln --no-build 2>&1 | grep -aE '失败|通过:'   # OracleBypassTests 为已知红，与本案无关
python tools/oda-compare/minverify.py --src <SRC> 2>&1 | grep -aE "<SRC>|通过"
```

minverify 输出的目标格签名即起点判据：`PASS` / `Out of range` / `Object improperly read: <AcDbXxx>(hdl)` / `.dwg file needs recovery`。签名→根因方向查 AGENTS.md「失败签名-对象流层根因」表。

## Step 1b 绿双胞对照（零成本容器常量反证，**先于任何层级归因**）

同一条写侧路上换一份图集就换判词 ⇒ **病灶随内容变、容器常量无罪**。这条只花一次 `richverify`，比节级 ladder 便宜一个数量级：

```bash
python tools/oda-compare/richverify.py --cells <SRC>-><TGT> --sets sample,table
```

- `--cells` = 逗号分隔 `SRC->TGT`（**默认只取基线里 A==OK 的翻格** ⇒ 要测红格必须显式传）；`--sets` = 逗号分隔图纸集（默认 `add` + `sample`），`--sets all` = 盘上现存全部集（慢）。
- 实证（2026-09-25）：`AC1015->AC1018` 的 `|table` = **OK 70,432 B**，`|sample` = **REJECT 207,424 B**，同走一条 R2004 写侧路 ⇒ 起手即可用；配 Step 6b 控制档一并否证了 §523 的「死在容器层」前提。
- 只在**同一目标档、同一写侧分派支**上两集对照才有意义（跨档/跨路的对照本就两条刀路，读出 PASS/REJECT 归不到内容因）。

## Step 1c 按需富图全 census（选靶前先量「面上还剩几格」，默认门只覆盖 60/360）

常态门只跑 `gate.RICH_SETS`（现 = `sample` + `table`）⇒「富图还剩几格」≠「按需面还剩几格」。全量 = **12 集 × 30 格 = 360**，选靶前一次性量完（驱动现搭，勿入仓）：

```bash
python - <<'PY'
import sys; sys.path.insert(0,'tools/oda-compare')
import gate, richverify as rv
gate.OUT = rv.OUT = r'out/<私有>/census'          # 私有 scratch，躲开并发 gate 轮的目录轮转
_s, _t, cells = gate.plan_cells(False)            # 30 枚翻格 (SV, TV)
for name in 'sample,table,add,mtext,fill,v1_autocad,v1_zwcad,v2_autocad,v6_autocad,v6_zwcad,lineanddim,textandrect'.split(','):
    gate.rich_section([f'{a}->{b}' for a, b in cells], [name])
PY
```

- **只能在隔离树跑**：主树工作区的语料会被并行手从盘上删（HEAD 仍跟踪）⇒ `rv.set_files(集名, 源档)` 返空 ⇒ 那一集**静默缺源**、整面看着像全绿（2026-09-26 实测主树 `add`/`mtext` 已空）。跑前先逐集数一遍命中枚数。
- 只问「有没有别的靶」时按集**逐步扩**（先 `sample`，出红再上其余），别一上来 360 格。

## Step 2 重生成 oracle（ODA 直转源图，与我方代码无关）

```bash
mkdir -p out/<cell>/oracle_in out/<cell>/oracle_out        # 新建字面量目录；Bash 守卫会拒 rm -rf "$VAR"
cp "最简各版本图纸/<SRC年>.dwg" out/<cell>/oracle_in/x.dwg
( cd "$(dirname "$ODA_EXE")" && "$ODA_EXE" "<oracle_in 的 Windows 绝对路径>" "<oracle_out 的 Windows 绝对路径>" <ACAD_TGT> DWG 0 1 )
ls -la out/<cell>/oracle_out/
```

| 魔数 | 最简图文件 | ODA 目标格式名 |
|---|---|---|
| AC1015 | 2000.dwg | ACAD2000 |
| AC1018 | 2004.dwg | ACAD2004 |
| AC1021 | 2007.dwg | ACAD2007 |
| AC1024 | 2010.dwg | ACAD2010 |
| AC1027 | 2013.dwg | ACAD2013 |
| AC1032 | 2018.dwg | ACAD2018 |

oracle 只随 ODA 版本变，不随我方代码变；发现既有 oracle 夹具可能过期就重跑本步（文件名以目录实际为准）。

## Step 3 产我方帧

```bash
src/DwgParser.Cli/bin/Debug/net8.0/DwgParser.Cli.exe "最简各版本图纸/<SRC年>.dwg" --convert <TGT> --output out/<cell>/ours.dwg
```

## Step 4 firstdiff 全帧枚举（本技能核心命令）

```bash
dotnet build tools/ProbeTool -v q   # 改过 ProbeTool 才需要
dotnet run --project tools/ProbeTool -- firstdiff out/<cell>/ours.dwg out/<cell>/oracle_out/x.dwg 0
```

- 第 4 参 `maxPrint≤0` = 不在首帧 break，**枚举全部差异帧**（>0 只打印前 N 帧）。该枚举模式若在工作树缺失（`FirstDiffByOrder` 于 maxPrint≤0 时仍首帧即停），按「size 或 content  diverge 即记录、不 break」补一个分支即可，属工具改动零生产码风险。
- firstdiff 按物理序（BodyStart）走、按 (handle,type) join；每帧报 `尺寸差`（字节）或同尺寸内容 diff 偏移 + 28 字节窗口。
- 输出混 build 噪声时过滤：`2>&1 | grep -avE 'warning|Determining|Restored|  ->|已用'`。

## Step 4b 从「哪帧差」到「字段身份」（本节的帧级技法已外包，只留格级专属）

> **本节原内嵌约 60 行帧级位流技法（三种假说 / 集中串流判据 / 位序与 UTF-16 字节序坑）已归并到独立技能
> `dwg-bitstream-diff-forensics`（2026-09-25 整理）——那里是定谱的**唯一落点**，勿在本档平行维护。**
> 凡要「定字段身份 / 宽度 / 位置」、要判「插入/删除 vs 重排」、要用 DXF 逻辑值锚字段、要查字符串编码版本规律
> ⇒ **加载 `dwg-bitstream-diff-forensics`**（`tools/oda-compare/bitdiff.py`：对象头自洽 `66+RL+HS+16==帧长` ·
> **无贪心**种子-扩展普查 · 递归对齐 gap 清单 · ODA-DXF 真值锚 · 串编码规律 · 位流原语速查表）。
> **贪心块分解会把「前后移位式插入/删除」误报成「重排」**（§207 的「重排」判定即被 §208 用无贪心法推翻）。

格级**专属**部分留在这里（帧级技能不含）：

- **帧级工具命令**：`tools/ProbeTool`（在 sln 外，Core 改过后须单独 rebuild）——
  `ProbeTool xcmp <ours.dwg> <oracle.dwg> <句柄hex> [条数]`（按句柄对齐：尺寸差 + 首差异位 + 两侧位串）、
  `ProbeTool firstdiff <ours.dwg> <oracle.dwg> [帧数]`（按物理序找首个差异帧）。
  （旧文引的 `tools/MlFrame` **当前树中不存在**、从未入库，已丢，勿再照抄。）
- **`bitdiff.py` 取帧入口（仅订正入口，机制权威落点仍在 `dwg-bitstream-diff-forensics`）**：它**不吃 `.dwg`**，只解析
  `FRAME h= t= data=<hex>` 行 ⇒ 喂帧器 = `tools/SsInv/bin/Debug/net8.0/SsInv.exe --framedump <dwg> <h1,h2,...> > frames.txt`
  （同样在 sln 外、改过 Core 须 `-t:Rebuild`）。SsInv 会**静默跳过** `BodyStart<2` 或前一字节最高位为 1（跨页）的帧 ⇒
  该枚取不到时才退到 `ProbeTool secsave <file> AcDb:AcDbObjects <bin>` 取解压对象流、按签名定位帧、现拼两份位表。
  跑它必带 `PYTHONIOENCODING=utf-8`（实测：末行汇总含 U+2212 减号，重定向到文件时本机 GBK stdout 直接
  `UnicodeEncodeError` 崩在打印前 ⇒ **gap 清单在此之前已全部吐出、读数仍可用**，但加 `PYTHONIOENCODING=utf-8` 后 rc=0）。
- **`xcmp` 的「同尺寸但 @+N」读数**：`@+2` 这类**极小偏移差**常是**对象尺寸/句柄前缀编码**而非字段差
  （§174：原生 AC1024 vs AC1027 同枚 TABLE 帧 = 同尺寸 5665B、仅 `@+2` 异 ⇒ 无版本增量）。别当字段级差异追。
- **pre-R2010 源（`HSStartBit=0`）**：按句柄取位串的旁路工具不会自反推句柄流起点，会报负长度。
  句柄流起点用 `DWGDBG=1` 跑 CLI 看 `LTYPE_HS_DERIVED`/`HSStart`，或 `ProbeTool objfields`。
- **位级对拍的隐藏前置：先证「待比对的产物确由本手生成器产出」**（§147 的教训、§174 否证）：
  产物帧长 == 源帧长，或 `DebugTrace` 里**没有**生成器 trace 行，或回环自读值未变 ⇒ **手术未触发**，
  此时**先修闸、别比字段**（§147 曾把「verbatim 塞入的源帧」误当「生成器字段布局错」，整手结论作废）。

## Step 4c 四侧逐类 `RL` join（说「我方缺整条计划」之前先跑，零位流假设）

「某族在我方**根本没有**降形路」是**归因级**判词，写错 ⇒ 下一手刀口整个错。先用一把零假设的尺：对**同一类型码逐枚** join 四侧帧长 `RL`。

```bash
for f in "<源.dwg>" out/<cell>/ours.dwg "新增图纸/sample_<TGT年>.dwg" out/<cell>/oracle_out/x.dwg; do
  echo "== $f"; dotnet tools/DvWalk/bin/Debug/net8.0/DvWalk.dll ls "$f" <类型码十进制>   # 非数字参数按类名子串过
done
```

- 四侧 = 源 / 我方 / **同内容 AutoCAD 亲手写的目标版本档**（`新增图纸/<集>_<TGT年>.dwg`；该集无同内容档 ⇒ 这一侧缺席并退三方，且要在结论里写明）/ ODA 亲手 oracle。
- 实证（`AC1018→AC1015|sample` 的四枚 DIMSTYLE `t=69`，2026-09-25）：`1D` 源 996→我方 **869 ≡ 原生 869**、`75` 988→**861 ≡ 861**、`44B7` 980→**877 ≡ 877**，**唯 `3CDE` 1060→1059 而原生 957** ⇒ 结论从「缺 `DimStyleCarry*ToAcR2000` 计划」**订正**为「降形路本来就在、某道守卫对这一枚单独放过」。该读数属那一格，引用须带格名。
- **形制尺钉「≡ 原生同柄」，不钉 oracle**：上例 oracle 对四枚一律 = 原生 −8 位（861/853/869/949）= ODA 私有归一 ⇒ 拿 oracle 当尺会把三枚**已正确**的枚打成红（Step 5 判据 4 的定量版）。
- 反手提醒：结论若是「放宽某道守卫」，同轮补**反向复测射程**（`AC1024/1027/1032→AC1015` 三格现绿同走此路 ⇒ 放宽须证那三格不漂）。

## Step 5 分类判据（良性 vs 真拦路 vs 不可追）

1. **真结构拦路**：`尺寸差 ≠ 0` 且不在已知良性台账；或同尺寸但内容差异出现在帧**深部**。
2. **已知良性台账**（AC1024→AC1021 实测；他格有同款请追加）：h=1 type48 BLOCK_CONTROL +1B、h=5 type56 +1B —— num_owned=2 忠实搬运，与原生合法 R2007 同形（§38.3），oracle 的 num_owned=1 是 ODA 私有归一、非目标。
3. **良性句柄 renumber**：data 区逐字节相同、仅句柄区**末字节**差异，且同类型全体帧为**同一常量偏移**（实测 509 VISUALSTYLE 尾参考 oracle−ours=+0x3C、逐对象 +2 递增）→ 句柄重编号嫌疑，非拦路（以 ODA 签名不再点名该帧为准）。
4. **oracle 污染（不可追平）**：ODA 图增广——DICTIONARY 条目多于源、EED(Annotative) 多挂等。判据：三方对齐 `最简各版本图纸/<TGT年>.dwg`（原生真值）；若我方与原生同形而 oracle 独异，**勿追 oracle 逐字节**。
5. **降版向的「ODA 合成三件套」专判（§51 实测）**：源侧公共头是**空 EED(2 位) + is_xdic_missing=1**，而 oracle/原生目标多一枚 60 位 XDATA EED（`[BS size][H app-name][payload][BS 0]`，ODA DXF 尾 `1001 <APP名>` + `1070 n` 定名）+ `xd=0` + 句柄流多一枚 app-name 引用——**三件套一体、属 ODA 降版合成**（追平要凭空造 APPID/DICTIONARY）。判法 = 先只做「剥源侧多出的版本增量字段」这一半，让产物成为**合法但更短**的 R2007 形，再看 ODA 直读是否无 `.err`：AC1024→AC1021 的 507 即如此（我方帧比 oracle 短 11B，**ODA 接受 ⇒ 格翻**）。只有 ODA 仍点名该对象时才回头补合成。
6. **四方一次到位法（新类型起步首选，别猜字段序）**：同一 handle 的四份解压 `.bin`（源 / 我方 / oracle / **原生目标图**）各跑一遍逐字段走查器，并按假说做**分段位比对**（脚本形如 `out/ml507/mls507verify.py`，只报「第几段首个差异位」）。四方一致 + 分段全等 ⇒ 配方就是**纯位加/减法**：reader 只需**动态测量**被增删块的位长（勿硬编码，同 §47），writer 只做 `CopyBits` 分段拼接、不发串、不动句柄流。
7. **oursOnly 盲区 + `drop=` 链判别（§136）**：`bypass`/`firstdiff`/留一法**只覆盖 inter 句柄** ⇒ 我方池独有对象（§136 那格 66 枚 DICTIONARY/XRECORD/CELLSTYLEMAP 的 xdic 链）对整个 diff 框架不可见，坏了只能经「引用入口帧」间接暴露 ⇒ **`.err` 点名的帧可能只是链入口，不是病灶**（`h=8`/`h=93` 当时就被误判成帧缺陷）。Teigha 按需解析 ⇒ `bypass all` OK ≠ 它们健康。判别 = `bypass` 第 5 参 `drop=<链上句柄>`：`^h drop=X` → OK ⇒ 真凶在 X 不在 h；ODA **容忍悬空 (3,·) 引用**（故 `drop` 掉引用不会自己造红）。
8. **一格常藏数族 ⇒ 先起「逐步具名前移」链台账再下刀（§140）**：ODA 在第一个硬错处早停，`.err` 只剩一个签名不代表只剩一族（富图 `AC1018→AC1021` 实测 = 代理 ×2 + 视图样式 ×4 + LAYOUT ×1 + 块头 ×22）。台账 = 我方体内**逐枚**把句柄换成 oracle 帧：集合从 `∅` 起，每步把 `.err` 点名的句柄加入并复跑，直到 OK（= 该集合恰为全部拦路面）或触顶；REJECT 而**不具名**（裸 `Out of range`）即停手转 Step 6b 层级二分（§140 那枚裸签名真身 = 两枚不具名即越界的 SECTIONVIEWSTYLE）。成对跑反向实验（Step 6b/判据见 `pitfalls.md` C14/C19），驱动范式与 `bypass`/`donor` 规格语义见 `docs/handbook/tools-and-fixtures.md` §6。

尾字节快查（对同类型多帧验证常量偏移）：

```bash
for h in <hdl1> <hdl2> ...; do
  o=$(dotnet run --project tools/ProbeTool -- objfields out/<cell>/ours.dwg $h 2>/dev/null | grep -aoE 'handles.*= [0-9A-F]+$' | grep -aoE '[0-9A-F]{2}$')
  r=$(dotnet run --project tools/ProbeTool -- objfields out/<cell>/oracle_out/x.dwg $h 2>/dev/null | grep -aoE 'handles.*= [0-9A-F]+$' | grep -aoE '[0-9A-F]{2}$')
  echo "h=$h ours_tail=$o oracle_tail=$r"
done
```

## Step 6 objfields 逐句柄锁位

```bash
dotnet run --project tools/ProbeTool -- objfields <dwg> <hdl-hex> 2>&1 | grep -avE 'warning|Determining|Restored|  ->|已用' | head -8
```

对**源 / 我方 / oracle** 三份同 handle 输出比对：`data[...]`、`handles[...]` hex、`BodyEnd`、`CarryStartBit`、`HSStartBit`。data 同、handles 异 → 回 Step 5 判 3；data 本体异 → 进字段级施工（reader/writer 分改分验，遵守热文件 mtime 规则）。

## Step 6b 层级定位：ODA 只给裸签名时**先怀疑**非对象流，但「怀疑」不是判词

`Out of range` / `Unexpected end of file` / `.dwg file needs recovery` **不带 `<AcDbXxx>(hdl)`** ⇒ 疑在对象流之前或首个对象即越界。**不得直接当成「病在容器」起手**（2026-09-25 一手据此将 §523 立档前提判废）——下节级 ladder 前先跑两把尺：

1. **绿双胞对照**（Step 1b，零 ODA 增量）：同一条写侧路上另一图集 PASS ⇒ 容器常量无罪。
2. **控制档 `bypass all`**（`ProbeTool bypass <源> <oracle> <出> all` → ODA 直读）：
   - **OK = 正证「病在 inter 帧内」**（inter 之外的 `oursOnly` / `oracleOnly` 当场脱罪）⇒ **转 `dwg-minimal-fatal-set-bisection`，别再走本节 ladder**；需两态产物 sha 相异（`all` vs `none`）且 `inter=` 具数，才算规格真生效。
   - **REJECT ≠ 反证**：该控制档会被 `oracleOnly` 引用污染（换进去的帧引用着没换进来的对象），与本档陷阱速查 §526① 同因 ⇒ 先补「只裁不换」对照，仍不据以判「病在容器」。**例外**：`bypass` 打印的 `OURS_ONLY = 0` ⇒ 该对照是空操作，当场即可判对象集/容器级（见陷阱速查 §540 条）。
   - **同一列方向可相反，且 REJECT 只是「当下这一棵树」的读数、不是永久判决**（实测 `→AC1018`）：某日 `AC1015` 格控制档 **OK**（`inter=5287`、两态 sha 相异）⇒ 病在帧内，`AC1021`/`AC1024` 格 **REJECT** ⇒ 当时据此把那堵墙判成「ODA 降版按表合成字典对象、我方不合成」（属语义裁定：合成手 or 按真红入基线）。**该判词已被否证**：层一 §562（R2004 类记录 `DwgVer/MaintVer` 该发 BL 而发了 BS）落库后，同一列控制档**首次 PASS** ⇒ 帧级二分当场重新可用。⇒ 控制档要按 **Step 6d ①** 当「每落一把刀必复跑的哨」用；任何据旧 REJECT 立下的「须合成 N 枚对象 / 非帧级」判词，在同树发生节级、类表或容器修复后**一律作废重测**。

语义订正一次：枚级二分收敛到某枚的含义是「该枚**单独保持我方帧即 REJECT**」（`^X`），不是「单独换回 oracle 即 REJECT」。

两把尺均指向容器层 ⇒ **先跑仓内现成的定性件**（勿手抄）：

```bash
export ODA_EXE="$(python tools/oda_path.py)"   # 硬约束 9：现场探，勿按文档断言
python tools/oda-compare/graft_scope.py "<源.dwg>" <ACAD目标名>   # 4 次 ODA 判定即出「容器 vs 对象流」
```

| 跑 | 内容 | 读法 |
|---|---|---|
| `c_ours` / `c_oracle` | 我方 / ODA 亲手 oracle 各 `recopy` | 前者须**复现该格红**、后者**必须 OK** ⇒ 任一不符弃本轮（门没跑成 ≠ 判词红） |
| `e1` | `swapsect base=我方 donor=oracle swap=AcDb:Handles,AcDb:AcDbObjects` | **OK ⇒ 病在对象流**；**REJECT ⇒ 病在我方容器（头 / 类表 / 非对象节）** |
| `e2` | 反向 `base=oracle donor=我方`（同两枚节） | **OK ⇒ 我方对象流脱罪**；与 `e1` **同向**才落层判词 |
| `b_all` | `bypass <源> <oracle> <出> all` | OK ⇒ 可起枚级二分（转 `dwg-minimal-fatal-set-bisection`）；REJECT ⇒ **当下这棵树**勿起，且它是「每落一把刀必复跑」的**哨**、不是一次性判决（Step 6d ①）|

实证（`v1_autocad AC1021→AC1018`，树 `out/_rt560` @`dba02ba3`，该轮快照）= `c_oracle` OK · `c_ours` REJECT · **`e1` REJECT** · **`e2` OK** · `b_all` REJECT ⇒ 两支同向钉「病在我方某枚**非对象节**」，且该格当时枚级二分的前提不成立（别去起 `inter_bisect`）；**但该 REJECT 是状态读数** —— 同一档在层一 §562 类节那刀入库后首次 PASS ⇒ 控制档按 Step 6d ① 当哨复跑，勿据旧读数永久判死该格。

再往下钉「哪一枚节」用 §26 的两件工具做**层级二分**：

```bash
# ① 造「全旁路基」：我方对象帧全部换成 oracle 帧（句柄映射照常重建）
dotnet run --project tools/ProbeTool -- bypass "最简各版本图纸/<SRC年>.dwg" <oracle.dwg> <出.dwg> all
# ② 对该基换节：把 oracle 的指定节装进我方产物，看 ODA 是否接受（最小通过集 = 真身所在节）
dotnet run --project tools/ProbeTool -- swapsect <base.dwg> <oracle.dwg> <出.dwg> AcDb:Classes
```

**容器节级 ladder 正证法**（钉「病在哪一枚节」——旧文引的驱动 `out/dim21/sectest.py` 在 `out/` 里、随回合即丢，**照下面五步现搭**）：

1. **射程** = `ProbeTool` 的 `StandardSections()` 名册（`Program.cs:3897`，16 枚）去掉 `AcDb:AcDbObjects`，且只有 base 里真存在的节会被换。实测那手扫的 12 枚清单 = `AcDb:Header` · `AuxHeader` · `Classes` · `Handles` · `SummaryInfo` · `AppInfo` · `AppInfoHistory` · `RevHistory` · `Preview` · `Template` · `ObjFreeSpace` · `FileDepList`。⚠ 该清单里 **`AcDb:AppInfoHistory` 不在 `StandardSections()`** ⇒ `swapsect` 对它是**空操作**（不打印 `SWAP <节>: -> NB`）⇒ 它那一枚的「判词未变」不是阴性读数。
2. **逐枚单发**：`ProbeTool swapsect <我方产物> <oracle> <出.dwg> AcDb:<Tag>` → 对该枚跑 ODA 同档直读（`oda_wait.run_converter`，见陷阱速查「多枚小文件别用 `run_converter_batch`」），判据 = 出 `.dwg` 无 `.err` 还是仍裸 `Object improperly read`。工作目录带时间戳、**不做任何删除**。
3. ★ **自证真换进去了** = 把 `swapsect` 打的 `SWAP <节名>: -> NB` 与 base 自身同节大小对拍（`ProbeTool secdump <我方产物> <一枚不存在的节名> <出.bin>` 会列出全部节的 `decomp=`）。**判词未变 且 大小未变 ⇒ 该枚本轮无读数**，不得写进「已排除」（§551 十六 就是漏了这步，才把「大半没测到」当成了结论）。
4. **补反向对照**（`base=oracle donor=我方` 换同一枚 / 同一组节）⇒ 两支同向才落「病在某枚节、不在对象帧」。节级交换不动句柄 ⇒ 不造 §526① 那形 `oracleOnly` 悬空引用污染，这正是它比 `bypass` 阴性档强的地方。
5. 实证（同上树同提交号）：12 枚里**只有 `AcDb:Classes`**（1226 B → 1213 B）出 PRODUCT、其余判词一字未变，配 `e2` OK ⇒ 病灶钉死在我方写出的类节字节 ⇒ 刀 = `R2000ClassTable.BuildR2004ClassesSection` 尾两枚版本 `WriteBS`→`WriteBL`（值 > 255 时 BS 18 位 vs BL 34 位 ⇒ 逐枚类记录错位、ODA 顺类表走即失步并抛**不具名** improper read）⇒ `cross_producer` 四枚 `→AC1018` 真翻（该面 26/30 → 30/30），富图 `→AC1018|sample` 四枚只由不具名变具名 `<AcDbLayout>`（**链前移一层、本格未翻**）。引用须带格名 + `e86cc8e6`（该轮快照）。

另一格同法读数（勿与上例混用）：AC1024→AC1021 的 `bypass all` **仍** 裸 `Out of range`，而**只换 `AcDb:Classes` 即 OK** ⇒ 真身在节层（`ReencodeClasses` 对 R2010+ 源→AC1021 目标只拔 hsize 一枚 RL、留 R2010 位布局）；修后 ODA 直读我方产物即前移为具名 `Object improperly read: <AcDbTableStyle>(93)`（方向文档 §48）。**注意各格答案不同**：§26 的 AC1018→AC1024 最小集是三节 `{AppInfo,Classes,SummaryInfo}`，勿照抄。

## Step 6c 翻格前的**必要 / 充分**双测（选靶成本尺，先于任何下刀）

枚级二分（`dwg-minimal-fatal-set-bisection`）收敛到 N 枚只证**必要** —— 语义是「这 N 枚单独保持我方帧即 REJECT」（`^X`），**不含**「修好这 N 枚即翻格」。补一次**正向 graft** 才分得开：

```bash
dotnet tools/ProbeTool/bin/Debug/net8.0/ProbeTool.dll bypass "<源.dwg>" <oracle.dwg> out/<cell>/graft.dwg "<句柄表,逗号分隔>"
# 裸句柄表 = allow 列表：只有列出的句柄换成 oracle 帧、其余仍我方（Program.cs:3851）
# 再对 graft.dwg 跑 ODA 同档直读（Step 2 的调用形、输入换成它），判据 = 无 .err
```

- **OK ⇒ 修这一枚 = 翻这一格**（便宜靶）；**REJECT ⇒ 该枚只是必要之一**，本格还有别族，按「逐族各一根」估工。
- 实证对照（同为 `→AC1015|sample`，2026-09-25）：`AC1021` 格的 `4958` 正向 graft **即翻**；`AC1018` 格的 `{11,3CDE}` 清完**仍不翻** ⇒ 这一对实验只能靠双测分开，起手选靶差一个量级。
- 两条读数守卫：① 列出的句柄**不在 inter** ⇒ `bypass` 打 `BYPASS FAIL: 规格未命中任何 inter 句柄`，别把那次空操作当结论（`Program.cs:3859`）；② graft 产物 sha 须与 `all`/`none` 两态**都相异**，相等即刀未跑（同 D124 一族）。
- **反向 graft（`^X`）不能当必要性反证**：实测 `^1DBC` = REJECT 且具名，graft 同枚后签名退成容器级 `.dwg file needs recovery` 且不具名。bisect 的机械规格以 `dwg-minimal-fatal-set-bisection` 为唯一落点，本档只留这把格级选靶尺。**收敛集的穷举版（逐族致命图 + 对偶白名单证充分）与 `^@file` 那条「零命中静默退化成 `all`」的坑，唯一落点在 Step 6d ②/③。**

## Step 6d 帧级致命集定位（控制档 = **哨** · 逐族致命图 · 器械会骗人）

### ① `bypass all` 是每落一把刀都要复跑的哨，**不是**一次性判决

Step 6b 那句「控制档 REJECT ⇒ 病不在任何一枚我方帧上」**是状态依赖的读数**：`AC1021→AC1018|sample` 四格长期 REJECT、
并据此立过「须合成 oracle 独有的 242 枚对象 = 语义裁定」的框架；层一 §562（类记录 `DwgVer/MaintVer` 该发 BL）落库后
同一档**首次 PASS**（`inter=5283`、`hits=5283`、与 `none` 态 sha 相异）⇒ 帧级二分当场重新可用，那套框架整条作废。

- 根因形状：**容器 / 类节自己失步时，任何帧级嫁接都救不了** ⇒ 历次「枚级 graft / 节级 swapsect / 反向嫁接全无通过根」测的是**同一个失效前提**，不是三个独立结论。
- 铁律：同树任何一次**节级 / 类表 / 容器**修复后，控制档必须**重跑**才继续有效；PASS 侧仍须三件齐（`inter=` 具数 + `all`/`none` 两态 sha 相异 + ODA 直读无 `.err`）。
- **`oracleOnly > 0` 不自动作废 PASS**（实测 `oursOnly=84 oracleOnly=242` 仍 PASS）：Step 6b 那句「控制档被 `oracleOnly` 引用污染」**只在 REJECT 侧**才是威胁，PASS 侧是正证。

### ② 逐族致命图（穷举「哪些族单独足以致命」，确定性、不等整门）

枚级二分只给你**第一枚**，族级致命图给**全集**；一族一次 ODA 直读，28 族一晚跑得完。

```bash
dotnet tools/ProbeTool/bin/Debug/net8.0/ProbeTool.dll rectypes "<源.dwg>" > out/fam/types.txt   # handle<TAB>typeCode
# 按 typeCode 分组 => 每族一枚句柄清单 out/fam/h_<码>.txt（逗号 / 换行皆可，ParseHandleList 通吃）
for t in $(cut -f2 out/fam/types.txt | sort -u); do
  ProbeTool bypass "<源.dwg>" <oracle.dwg> out/fam/o_$t.dwg "^@out/fam/h_$t.txt"   # 只留该族我方帧，其余 inter 换 oracle
  # 再对 o_$t.dwg 跑 ODA 同档直读（Step 2 的调用形、输入换成它）=> 该族单独致命与否
done
```

- **句柄表必须取自源图**（`rectypes <源.dwg>`，不是我方产物）：`bypass` 的规格与 `inter` 都按**源图句柄**取（`Program.cs:3815-3819`），而我方产物已重编号。
- ★ **`^@file` 有一条把红读成绿的静默退化**：排除式规格只在「排除集解析出 **0** 枚句柄」时报 `rc=2`（`Program.cs:3840`）；
  若文件里的句柄**全不在 inter** ⇒ `allowlist == inter` ⇒ 产物与 `all` **逐字节同一**、而 `all` 是 **PASS** 的 ⇒ 该族判据会报「刀做对了」。
  守卫 = **自己核 `BYPASS allowlist=` 等不等于 `inter − 该族枚数`**（白名单式核 `= 该族枚数`），只读 OK/REJECT 不够。
  `drop=`（第 5 参）更严 —— **完全无护栏**（`Program.cs:3810`），裁非 inter 句柄 = 静默零命中、产物 ≡ `none`。★ §572 补一形更阴：**`spec=none` 分支会调 `OracleFrameBypass.Reset()`，而 `Reset()` 连 `DropHandles` 一起清** ⇒ `spec=none` + `drop=` 同时给出即静默空操作（**即使句柄全在 inter**，症状 = 裁与不裁两份产物 sha 相同，极易被读成「oursOnly 已脱罪」的真结论）。规避 = **同规格双态对照**：`spec=<一枚实换帧>` 跑「不裁 / 裁」两遍、比 sha 才认读数。
- 读数（`AC1021→AC1018|sample` 快照 2026-09-26，引用须现跑）：28 族跑完**单独足以致命的只有两族** = `FIELD(568)`×4 + `ACAD_TABLE(555)`×2；
  `TABLECONTENT(564)` / `TABLEGEOMETRY` / `DATALINK` / `VISUALSTYLE` / `MLINESTYLE` / `MULTILEADER` / `DICTIONARYVAR` / LAYER / HATCH … 全 OK
  ⇒ `.err` 点名的 `90EA` 是**级联症状**（我方 FIELD 失步后 ODA 的下一跳报错点），据它起手会整手错靶。
- 族级判据可长期复用（比整门便宜两个数量级）：`bypass … "^@out/fam/h_<码>.txt"` 这一格今日 REJECT ⇒ 该族降版刀做对了它应翻 **OK** ⇒ **不等整门**即可判成没成。

### ③ 必要 ≠ 充分 —— 收敛集要用**对偶白名单**另测（Step 6c 那把尺的穷举版）

枚级二分收敛到 N 枚只证「这 N 枚保持我方帧即 REJECT」。把**同一份清单换成裸列表**（白名单：只把列出的换成 oracle、其余仍我方）跑一次
⇒ **ODA OK 才算「翻这格 ≡ 做好这 N 枚」证死**。实证：`allowlist=6 hits=6 => OK 241,708 B`（`8CD8,8CD9,8CDB,8CDC` + `58F1,8FFF`）；
而只换二分收敛的那 1 枚（`allowlist=1 hits=1`）判词**前移**成 `90EA` 而非翻格 ⇒ 「修好它 = 翻格」当场否证。墙从「未知深度」压成「两把刀」就是这一读的产物。

### ④ 会造出假结论的三件器械

- **ODA 具名不稳定**：`sha` 相同的一份字节，三次运行分别点 `<AcDbLayout>(6C)` / `<AcDbTableContent>(90EA)` / `<AcDbDataLink>(8E99)`
  ⇒ **「具名链前移到 X」量的是 ODA 报错位置、不是进度**，只当症状。证一把修复可信的形态 = **只切一枚闸的 A/B**（看 REJECT/CRASH 是否消失），或 ② 的族级判据翻 OK。
- **`chainwalk.py` 在 ODA 改号的格上第 1 步即死**（`[0] S={} -> 90EA` → `[1] bypass 失败 rc=2`：具名点是 oracle 号、不在 `inter`）
  ⇒ 这类格**只有集合排除可用**（②/③ 或 `dwg-minimal-fatal-set-bisection`），别拿链台账当前进尺。
- **`ProbeTool widths` 按类对位宽账会静默漏族**：只收 `HasHandleStream ∧ HandleStreamStartBit>0 ∧ CarryStartBit>0`（`Program.cs:269`）
  ⇒ 句柄流为空的类实例（实测 `565 DATALINK`）整个不进账、该格 5367 枚对象**少收 446 枚** ⇒ 拿 `widths` 做分族账**必须先报被它跳过的枚数与类号**，
  否则「候选族已收齐」是假结论；族全集用 `rectypes` 数。★ §572 同族第二形：**类表 `nInst` ≠ 对象在场** —— `classes sample_2004` 打 `#567 ACDB_MLEADEROBJECTCONTEXTDATA nInst=10`，而对象走查 `rectypes` 实测该类 **0 枚实例**（AutoCAD 往 R2004 存盘时把该族连字典条目一起丢掉、只留类声明）⇒ 凡「这一族在不在真本里」的判据**必须数对象**，数类表会把「声明保留但已清空」读成「在场」。
- 反手一条：我方自有确定性器械 `ProbeTool frameverify <源.dwg> <TGT档> [前N枚]` 能逐枚校帧账（实测报「全 5367 枚帧账自洽 + 产物字节网格自洽」却**仍被 ODA 拒**）
  ⇒ 自洽审计只当诊断，**不能当放行证**（同 Step 5「产物自洽 ≠ ODA 可读」）。

## Step 6e **绿双胞供体**的最小致命帧集（`objsize` join + `fatalset.py`）

Step 6d 的族级致命图以 **oracle 当供体**；该格一旦**改号**（或把 `TABLECONTENT` 换成另一个号）⇒ 按句柄 join 的集合排除必出假读数（§559）。换一枚**零改号**的供体即可整链复活：

- **供体优先级** = ① 我方**同写侧 · 同目标档 · 已判绿的另一腿**产物（同内容版本对 ⇒ 句柄与对象数与本格逐枚对齐）→ ② 同内容**原生目标档图** `新增图纸/<集>_<TGT年>.dwg`（**有内容漂移**，只当旁证）→ ③ ODA 亲手 oracle（会改号，最后选）。
- **量具 = `ProbeTool objsize <dwg>`**：全枚「句柄 / 类号 / 帧字节 / HasHS」，补上 `widths` 只收 `HasHS=True` 的那个盲区（§560）⇒ 两侧 join 即**分歧枚集**，再 `bypass <源> <供体> <出> @<全集>` 自证 **OK** = 容器/节层出局、拦路面 ⊆ 该集。
- **一条命令跑完整链**：`python tools/oda-compare/fatalset.py <源.dwg> <绿双胞.dwg> <ACAD目标名> [--ours <我方产物>]` = 控制轮（不红即弃本轮）→ objsize 分歧 → 全集 graft 自证 → 逐类组必要性 → ddmin 收敛；探针逐枚落盘**可续跑**（被 ODA 单例闸打断不必重来）。`rc=1` = 全集都不 OK（供体不对路 / 另有容器病 ⇒ 回 `graft_scope`）；`rc=2` = 环境不齐或控制轮没跑成，**不是判据**（D46）。

★ **起手先跑四侧逐类「枚数」对照**（源 / 原生目标档 / 绿双胞 / 我方，全用 `objsize` 数）——**枚数不等的族优先于帧长不等的族**：一整族在目标档本不该存在时，逐帧 diff 永远看不见它（绿双胞没那枚 ⇒ 它压根不进分歧候选）。实证 = `AC1024/27/32→AC1018|sample` 的段错误真身 `AcDbMLeaderObjectContextData`(#567)：我方红产物照源发 **10 枚**，而真 AC1018 原生图该类 **0 枚**、我方绿双胞亦 0 枚。

- 两条必做的否证：① ddmin 收来的「必要」含**内容耦合**（供体那枚恰好条目更少 ⇒ 「换绿形即 OK」有一部分是把条目换没而非把形改对）⇒ 定罪须回 `objfields` 三侧**对内容**，别只看帧长差；② 修好一环**判据面不动** ⇒ 先判该环是否被**更前面的雷**掩盖（ODA 在首个硬错处早停 ⇒ 具名点前移 ≠ 进度，即 Step 6d ④ 第一条），此时该环的验收**不拿门当尺**，改写成三件：自家产物位账（如字典 `numitems` 由 1→0）、组测表那一档由红翻 OK、全量 `dotnet test` 红集与纯净基线树逐名相同。

## Step 6f 「翻格」的**终判尺** = ODA 另存之后那一枚还在不在（「有 `.dwg` 即 OK」会造假绿）

`gate.py::_dir_verdict` 的 OK = 格目录里**有** `.dwg` —— 这对**慢写/半写**的 ODA 腿不成立。实测（§579 交底）：
同一批里 `sample_2000 → AC1021` 的四格（**含长期钉绿的对照格 `none`**）产物同为 162,880 B、**头 3,200 字节全零**、
自家 reader 读成 `version Unknown / 0 对象`，而 `.err` 正文是 128 条 `Invalid ResBuf` + 尾行
`OdError thrown during writeFile … Invalid input` ⇒ **「有 .dwg」在 ODA 报错时照样成立**。修该判据 = 口径变更须点名，
修它之前**凡 `→AC1021` 的读数一律视为未证**。

⇒ 凡「新增或改写某一枚对象」的翻格（`addMText` / 帧 graft / 薄形与中形拆并…），终判走三件套，**不要只看退出码**：

```bash
# 1) 我方产物 与 ODA 重存件 的首 6 字节都须 == 目标魔数；「全零头」= 该轮判据作废，不是绿
head -c 6 out/ours.dwg; head -c 6 out/oda_roundtrip/x.dwg
# 2) 三方计数（源 / 我方 / ODA 重存）+ 我方那一枚的正文能对上 —— 缺这一件就分不清「翻格」与「假绿」
Cli <文件> --inspect out.json --kind MText --limit 3000     # 逐文件数 candidates，再比 ours vs recopy
# 3) ODA 腿不得留 .err（留了就按 REJECT 读，即便它同时写出了 x.dwg）
ls out/oda_roundtrip/*.err
```

★ **第 ④ 件（§618 补，专治 EDIT 面）**：凡「**新增**某一枚实体」的格，还要对 **ODA 重存件**跑同一张
计数与文本命中 —— `Cli <重存件> --inspect x.json --kind MText --limit 4000` 数 `candidates`，
再看我方那枚的正文在不在里面；**并跑一条对照腿**证明这把尺子不恒假。实测：摘掉 `addMText` 的 AC1015
窄闸后门直接报 `EDIT 40/40`、该格 `REFUSED -> OK`，而 ODA 另存件里 MTEXT **245 → 244、命中 1 → 0**
= 那枚被静默丢弃（头合法、零 `.err`、自家读者还解得回来）。⇒ 「容器被收」与「那一枚还在」是两件事；
三层读数（判词 / `Applied` + 指纹 / 自家回读）里没有一层的口径是「严格读者数得着」，就得手补第 ④ 件。
按字节搜串在这条上不成立（AC1021 的 MTEXT 正文是集中流 TU 形，两侧 utf-16-le / gbk 命中都是 0）。

三件套齐 = 翻格。**只有 1、3 齐而 2 少一枚 = 假绿** = 「帧在、无人认领」那一形（R2004+ 看块头 owned 向量、
R2000 看块头 `[4]first_entity` 链首，见 §575 与 `Objects/BlockHeaderOwnedSync.cs`），它比不翻更坏 ——
会把没通的那一支钉进基线。另附一条同族坑：**具名预判拒的拒语里不要写箭头符号** —— 门按 GBK 控制台打印 msg，
一个非 GBK 字符能把整轮门崩掉、`GATE_RC=1`，看着像门红其实门没跑成（D46 的近亲）。

## Step 6g 「ODA 只给裸 `Invalid input`、致命集已收到一族」时的**位账定谱**（分段位锚 + 原生语料同形判据）

具名警告消失后剩裸签名 ⇒ **错误文本里读不出形制**（实测：`Cannot set CMLEADERSTYLE` 被 §602 摘掉之后，
那五枚的 `.err` 只剩 109 B `OdError thrown during writeFile … : Invalid input`）。走四步，每步都落读数：

1. **先排「刀没落下」**：`DWGDBG=1` 跑 `ProbeTool trace <源> <目标版本>` 再 `grep -a` 该器具名前缀
   （本例 `MLEADER-UP` / `MLEADER-MISS`；★ 日志含 NUL ⇒ 必须 `grep -a` / `tr -d ' '`）。
   ★ 关键一条：**`stop == end` 的闭合判据只闭合「源」的走查，不证「目标」的形制** —— 十枚全闭合仍能让 ODA 整帧拒。
2. **同一把读者对表两侧语义字段**：把「我方产物」与「ODA 自己另存同一张图的原生目标档」同句柄各反射转储一遍
   （临时工具，`tools/` 下自建即可，不入库），逐行 diff ⇒ 差异会缩到个位数行（实测 193 行差 9 行、且全是同一族字段）。
   ★ 动手前先找**姊妹件**：同族字段本仓多半已有正确实现（实测那次映射早钉在
   `Records/VisualStyleCarryAcR2000ToAc1021.UpgradedRgb`，AC1018 侧一直在用，AC1021 实体面自写一式才漏）。
3. **把「帧短 N 位」分到具体字段 = 分段位锚插桩**：在读侧各段末打临时绝对位锚（`Console.Error` + 按句柄门控），
   两侧同一枚各跑一次，逐段做 Δ 差分 ⇒ Δ 会**恰好**落在某几枚字段上（实测 Δ176 = 5 枚内嵌色 × 32 + 16 位取值差）。
   ★ 别用位串 seed-extend 对齐干这件事：CED 里夹 preview 区 + **取值差混在位差里**，块对齐会输出一堆假 gap。
4. **落判据**：写侧自造跨版本字段时，判据钉**仓库自带的原生目标档语料**里同一字段的解码值，
   **不钉**自家产物 round-trip —— 本仓 `ReadBL/ReadBS/ReadBD` 与 libredwg 同口径是**变长**的
   （`00→RL32 / 01→RC8 / 10→0 / 11→256`）⇒ **取值决定位宽**，形制错在这套读者身上不表现为「解不出」
   而只表现为「帧变短」，自家往返恒绿。另：一把刀**同改**「取值 + 位宽」时，ODA 收了只证「这一组合被接受」
   ⇒ 单变量分离要么当场一版临时刀跑掉（≈1 分钟），要么在详档显式留账。

## Step 6h **门全盲的那一类账**：跨版本形制没升级 ⇒ 三面自家判据（形制 / 语义 / 位账）

`AC1021 源 → R2010+ 目标` 三格富图长期 **OK**（ODA 向后兼容「旧形塞新容器」），而我方产物里那十枚
MULTILEADER 拿**自家读者**解全部落 Kind-only（`ClassVersion=1 / LeaderCount=0`）。⇒ **门绿不等于形制对**：
写侧逐位搬、读侧按目标档字段表走查，两侧不对称时三层读数（ODA 判词 / 接口自证 / 自家回读）都不覆盖它。
落这类账配三面判据（一次配齐，别只留一面）：

1. **形制** —— 钉仓库自带的**同内容原生版本对**（`新增图纸/sample_2007.dwg` vs `sample_2010.dwg`）里
   该族的逐枚解码值，**同一套断言两侧复用**（「与原生同形」不能有两把尺子）；语料恒等要用
   「十枚去掉句柄后 `sort -u` 只剩一行」这种形制证，不要抽样三枚就说「恒等」。
2. **语义** —— 逐枚引线数 / 折点 XY / 正文与**源**全等。专防一类假绿：走查错位后**恰好闭合**、
   值却串了位（只校形制会放过它）。
3. **位账** —— 同一张图源、**只变目标档**，Δ 必须等于逐笔构成之和；且这条数**由盘上量出来**。
   实测教训：配方档推的是 146 位，落码量出来 144 —— 差在升版时**剥掉** pre-R2010 的
   `num_arrowheads` 一节，连它那枚 2 位 BL 前缀一起走（推的账在档里一律标「推」）。

撤刀负控形制：闸里加 `&& false` ⇒ 三面承重例应当**全回红**、原生真值锁照绿（照绿才算对照组没坏）。
相关：坑 **D131/D132/D133/D134**、`docs/issues/2026-09-27-mleader-r2007-up-to-r2010-recipe-622.md`（配方）
与 `docs/issues/2026-09-27-mleader-r2010-up-knife-625.md`（落码与三面判据）。

## Step 7 收尾（判据落仓 + 回归）

0. **落库前撞车复查（强制）**：提交 src/ 前再扫一遍 `progress-and-debts` §1 快照 + `git log --all --oneline --since="8 hours ago"`
   ——并行道可能已把你的靶格翻绿入库（2026-09-24 实证：→AC1021 两格取证到一半被 §517 翻掉）。
   落库晚于翻转 ⇒ 改判「旁证+工具档」，且 **src 改动逐条重验「是否仍必要」**（详见陷阱速查「同靶撞车双检」）。
   0a. **基线是否已被别道 bless 覆盖本手格子**：`git show HEAD:tools/oda-compare/gate-baseline.json` 逐枚点名目标格现值
     ——本手「等点名 bless」的欠账可能已被别道一并钉掉（实证：`3666fafa` 翻的四格 EDIT `sample_2007→AC1021`
     由 `a27f1540` 入钉为 OK）。欠账已闭却还在档里挂着 ⇒ 下一手会去赎一笔已不存在的账。
   0b. **刀所在热档是否已有别道同函数在途**：`git status --porcelain -- <刀所在文件>`（主树工作副本）——命中即
     **交刀不覆盖**（本手半成品与红-先判据留取证树等合流，勿 `--only` 顶掉他道在途文件，参见本档 Step 7 第 6 条）；
     且**判据例须等该 src 进 HEAD 后另笔补**，否则把 HEAD 打成红（`dd49d8a9` 形态）。
1. 台账 + 新线索追加进 `docs/direction-a-ac1032-downgrade.md` 末：**先** `grep -nE '^## [0-9]' docs/...` + `wc -l` 查最新小节，**只追加「独立附证」小节，勿覆盖既有小节内容**（同一文档可能被并发扩写）。
2. AGENTS.md 只更新 frontier 一句话。
3. 落码修复后回归门：`dotnet test`（即时基线）+ **全门 `python tools/oda-compare/gate.py` 必跑**（2026-09-25 实测整门 = cross A 30 + B 30 + recopy 6 + 富图 60（`RICH_SETS=['sample','table']`）+ EDIT 40 ≈ **107 s**、`GATE_RC=0`；旧记「≈96 枚超单命令上限、一手跑不完」已作废，别拿它当不跑门的理由）+ 重跑 Step 1/4 确认 ODA 签名前移或翻格。**基线只认 `[改进]` 以外的位移**：翻格数只算逐格点名的`REFUSED/REJECT → OK`，抖动格（`CRASH`/`NO_OUTPUT`）那遍不作正式数，重跑到全树零抖动为止。
4. 翻格是 all-or-nothing（ODA 模型门）：单类型修复的合格判据是**签名前移/firstdiff 中该类型帧消失**，不期待立刻翻格。
5. **落码前先查「假岔路」**：刀与既有能力面冲突时，先问冲突是否按**另一维度**分档（目标档 / 写路径 / 源版本族）；
   若是，给本手那枚刀加一道**窄到该维度**的闸即可两条并存，**不必把整手挂成「须用户裁定」**。
   判据 = 收窄后把冲突类单独 `dotnet test --filter FullyQualifiedName~<冲突类>`：绿 ⇒ 岔路是假的、可落；
   仍红 ⇒ 才是真岔路，且**只把那枚刀**交裁定，其余刀照常落（勿因一枚挂整手）。
   同轮须把该窄闸写成**双向判据**（`[InlineData(档A, 不写)]` + `[InlineData(档B, 仍写)]`），否则日后被放宽即测不出。
6. **`git commit --only -- <路径>` 会覆盖本手点名路径的他人暂存 blob**（其余路径无损）：共享树里若某道把内容
   **只放在 index**（工作树 ≡ HEAD、`git status` 显 `MM`），本手一次 `--only` 就把那份**唯一副本**顶掉。三段式：
   提交前 `git ls-files -s > out/_backup_<x>/index_before.txt`；对 `MM` 的本手文件
   `git cat-file -p $(git ls-files -s -- <f> | awk '{print $2}') > <f>.INDEXBLOB` 存一份，并用标记词
   `grep -c` 判归属（判为本手 ⇒ 覆盖无妨）；提交后 `diff index_before.txt <(git ls-files -s)`，
   凡覆盖掉的**他人** blob 逐枚 `git update-index --cacheinfo 100644,<旧hash>,<path>` 还原
   （还原后须回到接刀前的 `MM` 形态 = 交还原样）。
7. **取证树 ≠ 落码树 ⇒ 先证「同一份内容」再宣称读数可迁移**：① 提交后立刻
   `git -C <取证树> diff <新提交sha> -- src` —— **输出为空 = 逐 blob 相同**，比在共享树重跑门快两个数量级
   且零并发风险（共享树里另有在途 src 时，在那儿 build 出来的**不是**你验过的那棵树）；
   ② 判据读的夹具须在两树都在场且同字节（`git hash-object` 逐对比）。主树工作副本可能被别道 scrub
   从盘上抹掉在途语料（HEAD 仍跟踪）⇒ 此时夹具须走 `CorpusPaths.Relative` 迁址支
   （`tests/DwgParser.Tests/CorpusPaths.cs` 的 `Moved` 表两侧同字节 ⇒ 换路不改判据，但要**当场数一遍字节相同**）。
8. **多批复测要数并集**：报「N 枚翻格」前，把各批**逐格点名**的读数取并集、与目标集逐枚对齐。
   汇总数（`富图 11/12`）**不指名 ⇒ 不可当点名读数入并集**，须回到逐格行；漏的那枚既不算红也不算绿，
   要么补跑、要么把结论改口成「已点名 M 枚」。
9. **重钉基线（`--bless`）四条硬规矩**（2026-09-25 一手全踩过）：
   ① **分节重钉靠门内建合并，别自造并行**：`--bless --no-edit` 写 cross/recopy/rich 三节、`edit` 节原样留着；
     `--edit-only --bless` 只并 `edit` 节（打印「已并入基线（仅 edit 节）」）⇒ 两遍**串行**跑完即可，
     **不许**为省时间开后台/并行门（四面同写一枚 `gate-baseline.json`，且用户禁后台进程）。
     ⚠ 合并表只含 `rich`/`edit`：`--no-rich` 那一遍若 bless，`cross` 会被本次读数整体覆盖 ⇒ 「只跑一节的 bless」保不住其它节。
   ② **扩语料面 = 改门代码，不是传参**：默认面写在 `gate.py::RICH_SETS`。只在 bless 时传 `--rich-sets a,b` 而不同轮换常量
     ⇒ 基线多出 N 集键、下次默认跑产不出那些键 ⇒ 比对当场报一排「新格未钉」（判败）。所以扩面是口径变更的**第四处**：
     `gate-baseline.json` + 快照 + backlog + **`RICH_SETS`**，缺一处即该手未完成。
   ③ **bless 前先查这枚文件自己的 index 态**：`git ls-files -s -- <基线路径>` ≠ `git rev-parse HEAD:<同路径>`
     （`git status` 显 `MM`）⇒ 别道把一版**只活在暂存态**的基线留在 index，本手 `--only` 会把它顶掉（实证：那版把 14 枚
     `→AC1024/27/32` 记成 REJECT = 其自身在途半成品）。先 `git cat-file -p <index-blob> > 备份`，提交后
     `git update-index --cacheinfo` 原样还原，并在提交信息里点名**该道下次 bless 须以新 HEAD 为底**。
   ④ **钉完当场两件套自证**：同一棵干净树 `gate.py --no-build` 复跑应报 `无回归 [PASS]`；再
     `git -C <取证树> diff <bless 提交> -- tools src` **必须为空** = 基线钉的源码 ≡ 落库源码。复跑里单格冒 `NO_OUTPUT`
     ⇒ **不改码、不回退基线**，单跑那一面复证（`--edit-only --no-build` ≈35 s）；持续不消才按「门没跑成」查环境，别当判据红。
10. **翻一格的最短通路往往是「把只开给某一目标档的写侧闸放开」，但放开 ≠ 形制即对**（§528 实证
   `AC1021→AC1015|table` 真翻格 · `3f03597f`）—— 本条与第 5 条「假岔路按目标档收窄」互为对偶：
   ① **起手一把 grep 定位硬闸**（别通读分片）：
     `grep -rnE 't\.Version *== *DwgVersion\.AC10(15|18)' src/DwgParser.Core/Objects/Writer/*.cs`。
     `Carries*Down` 这类搬运支的目标档判据常年写死单一档（§429 中→薄刀 `TableR2007Down.cs:31`、
     §430 的 531 降版器 `TableContentDown.cs:52` 皆 `== AC1018`）。刀体若与目标档无关（收窗靠
     `StringStreamStartBit`、正文按 pre-AC1021 内联 TV 回插）⇒ **只改闸**。
   ② **放开后仍红是常态**：缺的常是 R2004 界两句 —— 公共头 `if (t.Version != AC1015)` 才发
     `is_xdic_missing`（该位 `SINCE (R_2004a)`），且句柄流在 **owner 之后补 xdic 硬主槽 `(3,0)`**
     （源 `is_xdic_missing=1` ⇒ 该槽在源流里缺席，而 pre-R2004 读侧无条件解析它）；**owner 位长必现读
     `[RC][counter×值]`，写死 8 位会把 owner 值字节推到槽后**。任一守卫不合 ⇒ 返 false 回落通用搬运（保守零写）。
   ③ **形制尺取三边同句柄**（我方 / 同图 ODA 亲手 oracle / **原生同版本图集**）逐枚对 `RL` 与句柄流；
     **勿追 oracle**（§528 实测原生薄形比 oracle 大 24K 位 = ODA 私有归一，即 Step 5 判据 4）。
   ④ **判据档里句柄流起点须自算**：pre-R2010 目标产物 `rec.HandleStreamStartBit` 恒 0（读侧未派生，
     D44 同族）⇒ 写 `new BitReader(sd, rec.BodyStart)` → `ReadBS()`（类型码，531 走 18 位）→
     `hsStart = BodyStart*8 + ReadRL()`（同 `DvWalk hdl` 口径）。否则拿「起点未建立」把反控判成红、并误报红-first 计数。
   ⑤ **红-first 要如实标出「哪一枚判据在 HEAD 假绿」**：§528 七例在 HEAD 恰 **2 红/5 绿**，其中「句柄流首两枚形」
     那枚假绿 = 源流本就带 `(3,0)`，它真正钉的是「刀已落但未补槽」那一中间态（实测 `RL` 86,185、流首 `(4,·)(5,0)`、
     ODA 仍 REJECT）。不写这句，下一手会把它当冗余判据撤掉。
   ⑥ **射程自证可用枚数普查替代整门重跑**：`ProbeTool rectypes <file> | grep -P '\t(53[0-4])$'` 对各语料集与恒等门
     SRCS 逐档数该族枚数 ⇒ 全 0 即 cross/recopy/EDIT/产品恒等面**结构上不可达**（§528 六集 + 11 枚 SRCS 皆 0
     ⇒ 新增面恰一格）；R2010+ 源那侧另须查上游是否已摊平该族（`DropsTableContentOnUpgrade` ⇒ 本闸结构上不可达）。

## 陷阱速查

- **新增实体的「认领」判据（§620）**：落盘后 `DwgDocument.Load(产物)` ⇒ 断言**存在某块头** 的
  `BlockOwnedHandles.Contains(新句柄)`（`0x31` = BLOCK_HEADER），并跑一条**源为 R2004+** 的同目标档对照例
  防恒假；消融 = 把登记条件置 `&& false`，红的应当只有那几支机制例。★ 别只靠「自家读者把文本读回来了」——
  写侧合成帧只进 `Records` 时自读恒绿，而目标侧 owned 向量由 `StampBlockHeaderOwnedVectors` 按
  **`walk.Entities` 物理序**现算（R2000 源那条腿），漏登记 = 严格读者静默丢帧（坑 **D133**）。
- 忘设 `ODA_EXE` → 脚本回退硬编码旧路径 `D:\ODAExtract` 直接找不到 ODA。
- 只 build Core 不 build Cli → CLI 用旧 dll，产帧假阴性/假阳性。
- **D124 ⇒ 不在 sln 里的工具不会随 `dotnet build DwgParser.sln` 重建，症状是「整轮读数逐枚相同」**：
  改 Core 后只 build sln ⇒ `ProbeTool` 仍是旧 dll，`bypass` 由旧生成器产出 ⇒ 二分第一轮的产物 sha
  与上一手**逐枚相同** ⇒ 该轮所有读数作废（买到的代价是一次完整 13 轮二分）。**判据** = 改码后第一轮
  sha 与上一手相等 ⇒ 即判工具未重建，先 `dotnet build tools/ProbeTool -t:Rebuild` **再换新 state** 重跑（旧 state 里的
  缓存 verdict 不重花 ODA、也就不会变）。`richverify`/`chainwalk` 同判（都不自带 build）。
- 后台 `dotnet test | tail -N` 丢失败详情；用 `grep -aE '失败|通过:'` 截失败行。
- 同版本直通 = 假阳性（产物与源同魔数同大小 ⇒ 转换未发生；verify.py 已内置检测，勿手工绕过）。
- `rm -rf "$VAR"` 会被 Bash 守卫拒绝 → 用新建字面量目录（如 `out/ac1021c1/`）。
- grep 中文/混合输出行加 `-a`（dotnet run 输出会混构建噪声）。
- firstdiff 默认语义首帧即停，全帧枚举务必显式传 `0`。
- **R2013 公共头 `has_ds_data(B)` 是降版走查器最常见的「静默不触发」根因**（§47/§52 两次同因）：按 AC1024 写的 capture-only reader 少读这一位 ⇒ 位流偏 1 位 ⇒ 闭合判据不过 ⇒ 回退原样搬运、**产物尺寸看着只差几字节且无报错**。凡目标族含 AC1027/AC1032 源，必写 `if (version >= DwgVersion.AC1027) br.ReadB();`。
- **`CopyDataRegionCentralizing` 首行 `mleSkip>0 || mleInsert>0` 会抢走整方法**（§52）：MLEADERSTYLE 跨 R2013 界的 `text_extended` 插/剥由 `WriteObject` 设 `mleSkip` ⇒ 后面**所有按类专用 restructure 门永不执行**。判据 = DWGDBG 里**读侧行有（`MLDN`）、写侧行无（`MLS21DN`）** ⇒ 别再查读侧/字段序，先去看该方法首行；新写降版门时若该类已有 `mleSkip`，需在首行按「类型+源版本+目标版本+已捕获」窄豁免。
- **「按文档字段表逐字段读」在某版本族恒失败时，改试「从句柄流自洽解析」**（§169）：`dwg.spec` 的「`num_entries` 住在数据区」对 **pre-R2010 容器不成立** —— `2000.dwg` h=5（LTYPE_CONTROL）数据区 `[RL 32][own 16]` 后仅剩 **14 位**，放不下一个 BL `num_entries`（旧走查读得 0 ⇒ `ByLayer==0` ⇒ 表判 null ⇒ 计划**静默永不触发**）。句柄流则是自描述的（`code`+`size`）且以**帧尾对齐**为强判据：自 `hs=185104` 逐枚解析 6 枚恰止于 185176（`owner / xdic(null) / vec[0x16] / vec(空) / byblock / bylayer`）。⇒ 新增读侧走查时，**先看该容器有没有句柄流可用**，别默认文档字段表通用。
- **`gate.py` 撞安全删除守卫**（`[safe-delete] SAFE_DELETE_BULK_CONFIRM_REQUIRED`）：它要 `shutil.rmtree(out/gate)`，而 `out/gate` 每次跑攒上百项。**关键更正（§172 实测）：`count` 是「本回合累计删除量」**（JSON 里 `"scope":"turn"`），**不是单次目录大小** —— 所以「先 `mv out/gate out/_gate_prev_$(date +%H%M%S)`」只在**本回合删除量还小**时有效；**同一回合内跑第二次 gate 必然耗尽额度**（`oda_wait` 每次 ODA 调用删 2 个目录，富图段几十次调用 ⇒ 累积超 50）。规避：① **每回合只跑一次完整 gate**；② 加 `--no-rich` 跳过富图段（大幅减少 ODA 调用 ⇒ 减少删除量）；③ 只要 cross 读数就用 `minverify.py --src <SRC>`（单源矩阵，删除量小）；④ `dangerouslyDisableSandbox` **对 hook 级守卫无效**。**§172 已把 `out/gate` 的清理改为「改名移开」内建进 `gate.py`**（`os.rename` 不触发删除守卫），但子目录（`oda_wait` 的 `_in`/出目录）仍走 `rmtree` ⇒ 上限仍在。
- **并发也在跑 `gate.py` 时，本轮的 scratch 会被并发任务轮转掉 ⇒ 造出「假回归」**（§174 实测）：`gate.py` 每次启动都把 `out/gate` **改名移开**（`gate_prev_<ts>`），并发任务跑一轮就会把你正在跑的 `ours_*.dwg` 删掉 ⇒ 症状 = ① `FileNotFoundError: [WinError 2]` 直接崩掉整轮（`oda_wait.run_converter` 的 `shutil.copy2(inp,…)` 找不到我方产物）；② 方向B 假 `MISREAD`（产物被删 ⇒ `our_read_stats` 拿不到文件 ⇒ 正则不匹配 ⇒ 返回 None）；③ 方向A 假 `CONV_FAIL`。**这三者看着都像真回归**——先按「隔离复现」证伪（ODA 重转源图 → 我方 CLI 读数 `实体=N>0`），别急着改代码。**隔离法（推荐，互不干扰）**：把 `OUT` 改到**私有 scratch** 再跑 —— `python -c "import sys;sys.path.insert(0,'tools/oda-compare');import gate;gate.OUT=r'out/__x/gatepriv';sys.argv=['gate.py','--no-build'];sys.exit(gate.main())"`（`OUT` 是模块常量，`gate.main()` 内按全局读 ⇒ 赋值即生效）。判「本轮读数是否可信」还须看 `out/gate_prev_*` 的**时间戳**是否落在你的运行窗口内。
- **改动密集期 `--no-build` 会给出假红**：本手先得 1439/**6**/1，`dotnet build DwgParser.sln` 后降到 1442/**1**/1 —— 多出的 5 红是**陈旧 `DwgParser.Tests.dll` 与新写的测试源码不匹配**。凡测试计数异常，**先新鲜 build 再判定**；判「是不是我的」用 `git status --short -- tests/ src/` 看失败类所在文件是否未提交。
- 并发竞争：`dotnet test` 会在构建被占用时报**假失败**（dll 中途被换）。判法 = `--filter` 单测重跑，若单独通过即为竞争假象，别去改代码。
- **ODAFileConverter 间歇 `0xC0000005` 崩、崩时零输出**（§111 实测 ≈1/22 次调用，同一输入二次跑即 OK）：旧 `minverify.run_oda` 在 `subprocess.run` 返回后**立刻** `listdir` ⇒ 崩时返回 `NO_OUTPUT`。**留一法绝不可写 `st != 'OK' ⇒ 必要族`**（把崩溃计成致命、造出系统性假必要）。**已落码（§111 补）**：共享件 `tools/oda-compare/oda_wait.py::run_converter(oda, inp, outd, ver)` = `tasklist` 等进程真退出（+0.7s 句柄余量）→ 每次重试重建 `_in`/出目录并重拷输入 → 零输出有界重试，返回 False 即 **`CRASH`**；`minverify`/`richverify`/`gate.py` 的 `run_oda` 均已接（状态 `OK/REJECT/CRASH`，`richverify` 尾部标 `UNDECIDABLE` 不标 `FAIL`）——**§172 更正：`gate.py` 早已接入（§111 补那轮就接了），旧记的「刻意未接」已作废**。**自写 ddmin/留一法驱动直接 `import oda_wait`，别手搓等待。**
- **`.err` 是三层，具名抛点在**第 2 行**，末行恒是套话**（§172 实测）：`richverify`/`gate` 曾取 `ln[-1]` ⇒ **所有富图格看着「不具名」**（末行 `Previous error: Object improperly read`，第 2 行才是 `<AcDbSun>(1A9F3)`）。`minverify` 早有 `err_signature()`；**§172 已把同一语义补进 `gate.py`**（只改诊断文本，不碰 OK/REJECT 判定 ⇒ 回归判定只比 A/B 判定、不比 `A_msg`，**无需 `--bless`**）。自查具名点：`grep -m1 'Object improperly read:' <outd>/*.err`。
- **链台账工具已固化：`tools/oda-compare/chainwalk.py`**（§172 新建，替代过去散落在 `out/` 下的一次性 `chain.py`；旧 §178 那句「当前树中不存在」已作废）。
  **签名只有三段 = `<源.dwg> <ODA目标版本名 ACAD2007> [最大步数]`**（`chainwalk.py:17`）：**没有 oracle 位置参**，它自己现造 oracle 到
  `out/chainwalk/<源名>_<ACAD名>/oracle/`（路径经 `tools/oda_path.py` 发现，`ODA_EXE` 只是可选覆盖）。把 `<oracle.dwg>` 当第 2 参传进去
  ⇒ 拿到的只是 `argument maxsteps: invalid int value: 'ACAD2004'`（形参错位，不是它跑不动）。两个内置口径修正：**基线显式传 `none`**
  （`bypass` 空规格默认 `all` ⇒ 会假 OK）、**具名点取 `.err` 里 `Object improperly read:` 那一行**。`out/` 在 `.gitignore` ⇒ 一次性脚本会丢，新驱动请直接落 `tools/`。
  **两条失效条件 ⇒ 转集合排除（Step 6d）**：① ODA 判词**不具名**时链走不动（实测 `sample AC1024->AC1018` 起步即 `S={} -> REJECT 不具名` 早退）；
  ② 具名点是 **oracle 改号后的句柄**（不在 `inter`）时第 1 步 `bypass` 即 `rc=2`（实测 `[0] S={} -> 90EA` → `[1] bypass 失败 rc=2`）⇒ 这类格只能做集合排除。

- **`chainwalk.py` 自己现造 oracle，且每轮 `shutil.rmtree` 工作目录**（`out/chainwalk/<源名>_<ACAD目标名>/oracle/`，`chainwalk.py:130-133`）⇒ **不需要**预先摆档。需要预放的只有 `inter_bisect.py`（认死 `out/interbisect/<源名>_<ACAD目标名>/oracle/x.dwg`，缺 ⇒ `rc=2` 空跑一轮，规格以 `dwg-minimal-fatal-set-bisection` Step 0 为唯一落点）。**复用现成 oracle = `cp` 过去并改名 `x.dwg`，别重造**（一格 ODA 直转≈2-3 min）。
- **`DvWalk` 各模式列名不统一**：`ls` / `bits` 打 `t=` 与 `RL=`（`Program.cs:48`），`hrefs` 一类才打 `type=` ⇒ 拿 `type=` 去 grep `ls` 输出会**静默零命中**（混中文输出还需 `-a`）。
- **Git Bash 里 `dotnet <路径>X.exe` 不可用**：实测 rc=**140**、host 报 `An assembly specified in the application dependencies manifest … has already been found but with a different file extension`（**不是 127**）⇒ 一律 `dotnet X.dll` 或 `./X.exe`；改过 Core 后工具不在 sln 里，仍按硬约束 9 逐个 `-t:Rebuild`。
- **`ProbeTool objfields` 的 `data[…]` / `handles[…]` 窗按字节切**（`hsb = HandleStreamStartBit / 8` 截断，`Program.cs:1723`）⇒ 句柄流起点非字节对齐时，从这两行手解引用**整段错位**。实证：§555 据此报过「少发一段 `C3 02`、末字节 `FF` vs `FE`」，真身 = **恰少一枚 `0x30`**、两侧帧尾余量同为 4 位、无 `FF`/`FE` 之差。逐枚引用用 `DvWalk hdl`。

- **「帧位串三方对齐」定谱法（§178 实测，尾段/增量块首选）**：`firstdiff`/`xcmp` 只说「哪帧差、差几字节」，要**定字段**就做位串：
  1. 写一枚 20 行的探针（C# 直用 `BitReader`，`out/` 下不入库），按 `ReadBS()` + `ReadRL()` 解出**源 / 我方 / 原生同句柄**三份帧的 bit 串（`bit = (data[(start*8+p)>>3] >> (7-((start*8+p)&7))) & 1`）；
  2. **先用源帧自证走查**：源帧的 `TV`（name/desc）位置已被既有 reader 钉死 ⇒ 逐字段走查源帧应**恰闭合到源 `RL`**（§178 实测 `add_2004` h=ED 闭合在 rel653 = RL）；闭合即说明字段序假设成立；
  3. 再把「目标帧的 inline 区」与「源字段区」逐位比：**目标形 = 源形减去被集中化的串**（若目标把某串移入集中流，则 inline 区 = 源字段区删掉那几段位）；用 `del spans` / `insert spans` 两种假说扫，命中即同时钉死字段身份、宽度、位置；
  4. 尾段用 `A == B[:a] + X + B[a:]` 之类**等式**收口，别用「窗口容差/同余」。
  实证收益：§178 一次定位两处独立根因（5 枚 map filename 未集中化 = 10 位；尾段 `channel_flags` 派生值 1 位），且给出**可直接落码的位序**（`BD BD BD BL BL BL`）。

- **`product_fingerprint.py` / `minverify.py` 的 ROOT = `abspath(__file__)/..`**：从 worktree 里用相对路径 `../../tools/xxx.py` 调用 ⇒ ROOT 规范化回**主仓** ⇒ A/B 两侧其实跑同一支 dll ⇒ 报**假 IDENTICAL**。worktree 侧必须传**该树自己的绝对路径**（`python .../out/_<wt>/tools/product_fingerprint.py baseline|after`），并逐行核 `wrote D:\...\out\<wt>\out\fingerprint\...` 的回显路径。
- **同靶撞车双检（开工前 + 落刀前）**（2026-09-24 第二次实证）：深手开工前与提交前各复扫
  `progress-and-debts` §1 快照 + `git log --all --oneline --since="8 hours ago"`——并行道可能已把你的靶格
  翻绿入库（本手 →AC1021 两格取证到一半被 §517 翻掉、两格翻格判据恰好也是本手独立找到的那枚）。
  撞车后处置：① src 改动**逐条重验必要性**（本手「Val case 0 薄占位不透传」刀收口时发现被 §520-补否证，
  撤手免于把绿格打回红）；② 取证产物降级为「旁证+工具档」入库，标题显式标「已由 §NNN 收口」。
- **「读侧不读 ≠ 写侧不该发」**（§520-补 / D119）：读/写对称性推断在 **ODA 硬要求字段**上不成立——
  kUnknown 的 +2 位载荷：读侧 `case 0` 不读、写侧**必须发**（缺即拒，三形实测）。
  这类字段只认 **ODA 三形实测 + red-first**，「自家 reader 读得回」不作数；别按「对称应然」落刀。
- **「产物自洽 ≠ ODA 可读」双向都别当尺**（§12 / §519-补五 / 2026-09-24 再现）：自家走查 Δ=0 闭合
  仍被 ODA 拒 ⇒ ODA 有额外硬要求（查 `.err` 具名点第 2 行、做三形实测）；反过来自家 reader **读不回**
  也不必然是写侧错（§519-补五：基线判绿格的表帧同样 `rec.Table==null`）⇒ **成败只以 ODA 判词为准**，
  自洽/读回只当中间诊断。
- 解析 `ProbeTool`/`DvWalk` 的**重定向文件**要先试 GBK（直出到文件是 GBK、过管道变 UTF-8）；且**只用纯 ASCII 锚**（如 `h=\w+ type=\d+ kind=`）——用中文锚（`物理第…帧差异`）配 `errors='replace'` 会把解码错掩盖成「0 分歧帧」假清零（§92续 与本手各踩一次）。
- 句柄流/帧尾一类的差，判「数据 vs 引用」用 `DvWalk hdl <file> <hdl>` 的逐枚 `code/size/val + hsBits` 三方对拍（源/我方/oracle），比看 hex 快且不会因位偏移 1 位而全错；`hrefs`/`objfields` 在 `HSStartBit=0`（锚未派生）的对象上整段吐垃圾引用，那类帧只认 `firstdiff` 帧长 + 真 BitReader 位表。
- **位级搜串 / 集中串流 / 位序** 三条帧级技法**已归并到 `dwg-bitstream-diff-forensics`**
  （其 Step 4a「三种假说 + 集中串流判据」、Step 4「字符串编码版本规律」、陷阱速查「位序 / UTF-16 字节序 / 生成器产出前置」）：
  - UTF-16 字节序：位流先出现低字节 ⇒ LE 取 `u >> 8`、BE 取 `u & 0xFF`；三种拼法各搜一遍。
  - Opaque 帧判「内联 vs 集中串流」只看等式 `串流内容尾 + 17 == RL`，不看串的位置。
  **本节勿平行维护这三条——有更新改帧级技能那一份。**
- **消融 / 负控 golden 也会随上游刀移动**：负控只关掉**本类**那枚刀，若同轮另一枚刀作用在**同一帧**上
  （实证：pre-R2007 目标不再搬 R2007+ 集中串 17 位页脚），消融态同样变短 ⇒ 重钉时须写出**算式**
  （`542 − 17 = 525`）并注明「消融 ≠ 回到改刀前的旧帧长」，否则下一手会把这枚判据当「刀没生效」的证据而误撤。

- **「不具名 improper read」的两条假线索（§526 一手实撞，别再付一次学费）**：
  ① **`bypass all`（我方对象帧全换 oracle）仍红 ⇒ 不能判「病在容器」** —— 该控制档被 `oracleOnly` 引用污染
  （实测 `sample AC1024->AC1018`：oracle 独有 **232 枚**、我方独有 2 枚，换进去的帧引用着没换进来的对象）。
  先补「只裁不换」对照（把 oursOnly 摘掉再跑），或直接用逐格 `firstdiff` 的**枚数**判断差集是否有信息
  （实测 2937/5372 帧差 ⇒ 差集 = 全集，无信息，走 `dwg-minimal-fatal-set-bisection`；**起手前先按该技能 Step 0
  把亲手 oracle 摆到 `out/interbisect/<源名>_<ACAD目标名>/oracle/x.dwg`** —— 驱动只认这个路径，缺 ⇒ `rc=2` 空跑一轮）。
  ⚠ 该阴性读数本身要先过下一条「类表改号幅度」的闸，别拿它当「病在容器」的判词。
  ② **旧口径订正（2026-09-26 复核）：「`swapsect` 节级 ladder 两向都不可当判据」已收窄** —— `CRC does not match` 那形假信号只属于**就地改节字节而不重算 CRC**；
  `ProbeTool swapsect` 走 `R2004Writer.Build` 把 base 全部节**重装整枚容器**（节 CRC / 页账 / 加密头现算，AC1021 基座另走
  `R2007Writer.TryBuildCrossVersion`）⇒ 不冒该假信号。**带反向对照轮 + 逐枚大小自证的节级 ladder 是成立判据**（Step 6b 五步）。
  仍然成立的那半 = 单换 `AcDb:Classes` 让不具名变**具名**（`<AcDbLayout>(6C)`）本身只是「ODA 能不能命名这枚」的**诊断杠杆**、
  不是成因（那一格换完照旧 REJECT；成因判据 = 换进去后 ODA 是否出 PRODUCT），且当时我方类表与原生只差末行 `AcDbDataTable`
  一枚、逐枚 positional diff 其余 54 行全等 ⇒ 类表内容无罪并不等于类**节**无罪（错位可以出在字段位宽而非名册）。
- **多枚小文件别用 `oda_wait.run_converter_batch`（节级 ladder / 逐格判词这类「枚少、判词快」的批量尤其）**：它的最坏预算 = `attempts`（默认 4）× `timeout`（默认 600 s）+ 收尾逐枚回落 ⇒ **结构性超过单命令上限**。实测两次（3 枚 / 12 枚）分别在 15 min / 25 min 上限内不返回被杀，`r1`/`r2` 子目录建出而**零 verdict** ⇒ 同一批改逐枚单发 `run_converter` 后 12 枚一条命令跑完。配套两条可观测性：**勿 `log=lambda s: None`**、**驱动勿 `| tail -N`**（stdout 缓冲 ⇒ 被杀后连「跑到第几枚」的证据都不留下）。
- **不具名 `Object improperly read` 别在 `audit` / DXF 上找命名**：同一枚被拒产物走 `ACAD2004 DXF 0 1` 与 `… 0 2` 两支，都只落一枚 **135 B** `x.dxf.err`、判词仍是裸 `Object improperly read` ⇒ 调 `audit=2`、换输出 DXF **不产生任何新指针**，这一轮直接省掉；要指针就回 Step 6b 层级判据 + 先 `cat` `.err` 全文（见下面那条「不具名格必读 `.err` 全文」）。
- **凡 `bypass` 的「阴性」读数（`all` / `^X` / 只裁不换），用它判「病在容器 / 病在帧」之前先量两侧类表改号幅度**
  （2026-09-25 一手险把 `h=2` 当唯一致命枚）：ODA 降版会**整表重排 per-file 类号**，而 `h=2` 正是承载这批类号的
  **类表控制对象** ⇒ 「换成 oracle 的 `h=2` 就不再 REJECT」可能只说明**ODA 自家改了号**，不说明我方写坏。
  判别式（零 ODA、秒级）= 拿**原生同版本图**当第三列：`dotnet run --project tools/ProbeTool -- classes <file>`
  比类号名册（`classes` 模式在 `tools/ProbeTool/Program.cs`），并直接比三侧 `h=2` 帧字节。若**我方 ≡ 原生
  而 oracle 离原生很远** ⇒ **收敛到表控帧即假收敛**，该枚不作独立靶（真要用它先做「正向只换该枚 + 与原生对表」两件事）。
  类号按档漂移本仓已两度实测：`#507 MLEADERSTYLE`/`#508 MATERIAL`（`empty_2004`）vs `#505 MATERIAL`/`#508 MLEADERSTYLE`（`empty_autocad_2004`）。
  ⚠ 同一套对照**逐格方向可相反**（同一道另格上同法判为真拦路）⇒ 它是**否决尺**，不是「类表族一律无罪」的通行证。
  该轮快照读数（树 `out/_h9v` @`1c7c0735`，**未随迁复测**，引用须带树 + 提交号）：`sample AC1021→AC1015` 枚级二分
  收到 2 枚 = `h=2` + `h=8F38`，其中 `h=2` 我方帧 ≡ 原生 **215/217 B**、oracle 同帧与原生**公共前缀仅 8 B**
  ⇒ 按上式判为伴随项；两枚合换才 OK、单换各自 REJECT（`only2` 的签名还会**变形成 `Invalid group code`**）。
- **`gate.py`/`richverify` 连撞 `rc=3` ⇒ 正解是「换一份私有安装目录」，`ODA_SINGLETON=off` 只静音核对**：单例闸的
  **争抢域 = 安装目录**（`tools/oda-compare/oda_wait.py::preflight()` 只在「同目录有他道进程」或「进程目录取不到
  ⇒ 保守命中」时 `SystemExit(3)`；**异目录正是合法的并发形态**）。先手测：`python -c "import sys;sys.path.insert(0,'tools/oda-compare');
  import oda_wait,gate;print(oda_wait.preflight(gate.ODA))"` —— **返非空** = 真有人同目录在跑（等）；
  **返空却 rc=3** = 保守命中（自家 `out/gate/oda_inst_N` 兄弟实例、或他道报不出目录），可复跑并在详档写明。
  两者口径不同 ⇒ 别把 `off` 当解法：`off` 之后同目录两个转换器**照样互撞**，症状就是 `NO_OUTPUT`/`CRASH`（2026-09-25
  实测链：`GATE_RC=3` → 探得 `[]` → `rc=1` 且 `AC1015->AC1021 OK -> NO_OUTPUT`、富图 51/58 → 复跑 `RC=0`、53/60）。
  **解法 = 把仓内 `tools/ODAFileConverter/` 整份复制到本树私有目录**（同一二进制、许可不变；实测两车道各用
  `out/_h9u/out/tr/oda/`、`out/_h9v/out/dd/oda/`），再 `export ODA_EXE=<私有 exe 绝对路径>` ⇒ `rc=3` 结构性消失。
  铁律：凡该遍读数里出现过 `NO_OUTPUT`/`CRASH` ⇒ **不作数、绝不 `--bless`**，复跑到全树零抖动（`pitfalls.md` D46）。
- **撤闸类手的判据迁移**：删掉一道 `return false` / `XxxBail(...)` 时，**同时迁走三样** —— 钉旧边界的
  `[InlineData]` 换成 `[MemberData]` 新档、契约例（「拒得具名且不留产物」）的**载体换一枚仍在场外的档**
  （否则该例恒真）、以及**别把不同射程的档并回一张表**（§527：`setText/setAttrib` 六档落地，
  `addMText` 仍只覆盖 R2004+ ⇒ 拆两表）。做法与红-first 见技能 `dwg-red-first-ablation-verification` Step 7b。
- **写侧类表字段的判据不许拿 build→parse 回读当锚**（§562 实测）：`R2000ClassTable.TryParseR2004Classes` 对 AC1018 源把 **BS / BL 两种版式都试**、靠「类号自 500 连续递增」择一 ⇒ 单枚夹具两支都闭合 ⇒ 回读判据**恒真**（同「射程条款先在夹具上数一遍」那条坑；`ProbeTool classesrt` 同形，只证自洽）。正解 = **位宽锚**：同一枚夹具只改该字段值、量**产物字节增量**（`tests/DwgParser.Tests/ClassRecordVersionWidthAc1018Tests.cs` 4 例 = 109 / 114 / 113），并**写明免疫腿**：值 ≤ 255 时 BS/BL 共用短码 `BB=10` ⇒ 全零夹具两版逐字节相同 ⇒ 极简源该位恒 0——这正是 `cross`/`recopy` 全绿却看不见该列的原因（别拿门绿反证写侧对）。消融 = 两行改回 `WriteBS` 重编 ⇒ 恰 2 红 / 2 绿（免疫腿两支皆绿属预期，不是判据坏）。
- **打印 `Writer/` 分片前先查内嵌夹具**：`grep -l FromBase64String <分片>` ⇒ 命中就勿整段 `sed -n 1,110p`
  （`DwgObjectStreamWriter.TableContent.cs` 第 101/105/109/117 行各 3,260–7,544 字符 base64 帧夹具、
  合计 ≈ 19 KB ⇒ 一行就灌满上下文）。看闸口一律定点取行（`grep -nE 'Version *(==|>=)' <分片>`）。
- **不具名格必读 `.err` 全文，且先数 `OURS_ONLY` 再决定要不要做「只裁不换」（§540 实测，两条各省一手）**：
  ① `gate.py` 只取 `.err` **第 2 行**当签名 ⇒ **头部的 `ODA Warning: Cannot set <FIELD> to that value.` 全被丢掉**，
     而警告里直接写着字段名（实测 `CMLEADERSTYLE` / `CANNOSCALE`）。查不具名格先 `cat <outd>/*.err`，
     别只看门的 `msg` 就判「无指针」。② `ProbeTool bypass <src> <oracle> <出> all` 会打
     `OURS_ONLY …` / `ORACLE_ONLY count=N`：**`OURS_ONLY = 0` ⇒ 我方对象集是 oracle 的真子集**，
     「把我方多余对象裁掉」这一对照**结构上是空操作**，且此时 `bypass all` 仍 REJECT 就等于判了
     **对象集/容器级**（缺的是 ODA 降版合成的那 ~240 枚对象）⇒ 直接下「非帧级」判词，别再逐帧找单点
     （`sample AC1024/27/32→AC1018` 即此形；`AC1021→AC1018` 只多几枚 XRECORD 同墙）。
- **`cross` 面的绿 ≠ 跨写入者的绿**：同一张空图，**AutoCAD 亲手导出 → 方向A 19/30（11 红）**、
  **ZWCAD 导出 → 30/30**，而仓里 bless 的极简语料是**第三次导出的更薄版**（对象数比两套都少）
  ⇒ 门对「写入者」维度零覆盖。据 bless 语料的绿宣称「任意来源可转」前，先拿 `empty_autocad_*` 一类
  **异写入者同内容**语料跑一遍 `gate.py --no-rich --no-edit`（换语料 = 只覆盖**该隔离树自己**的
  `最简各版本图纸/empty_*.dwg`，勿动主树）。

- **取证器械自身的 verdict 也要先自证**（§602 实测）：`tools/oda-compare/chainwalk.py::read_verdict` 旧形 =
  「输出目录里有 `.dwg` ⇒ OK」，而 ODA 在 `writeFile` 中途抛错时**照样落盘一枚首六字节全零的桩** ⇒
  链台账在第 0 步就报「★链闭合（0 步）」，而它自己的 `read/x.dwg.err` 就躺在同目录；
  `inter_bisect.py` 复用同一枚函数 ⇒ 未修前二分的每一半都被判「无罪」（= 病灶永远找不出来）。
  口径：**用任何「件 + err」型判定器之前，先拿一枚已知 BAD 的输出目录喂它，要求它回 REJECT**；
  回 OK 就是器械瞎了，本轮所有链/二分读数一律作废。§602 已补三条判据（有 `.err` ⇒ REJECT ·
  产物头 ≠ 目标魔数 ⇒ `REJECT STUB_HEAD` · 否则 OK；`ver` 缺省时只判第一条，向后兼容）。
- **「某把刀从未咬合」类缺口：先读 `DWGDBG` 的 `why=`，再判那枚闸是结构约束还是拟态启发**（§602 判例）：
  507 升版计划里写 `numReactors is < 0 or > 8 ⇒ 拒`，而富图真本那枚带 **11** 枚反应器 ⇒
  该计划自入库起在这张图上一次没跑过，整帧按源形塞进目标容器，而 ODA 只报一句
  `Cannot set CMLEADERSTYLE to that value` —— **具名点在别的对象上（样式值），不在病灶对象上**，
  所以链台账对它无效（`.err` 里没有句柄）。判法两问：① 找**读侧同字段孪生闸**比对（读侧写 64，并注明
  「只当早退闸、不消费它」）；② 从帧尾句柄流真数出有几枚引用（实测 owner + 11 枚 `(5,·)`）。
  两侧口径不一致 **且** 该计划不消费这个值 ⇒ 是写死的启发式，不是格式约束。
- **放宽「被多处共用的走查函数」上的闸，射程会顺着所有消费者铺开**，别按类名推断落点：
  `MLeaderStyleCarryAcR2000ToAc1021.TryLocateShortCmcBits` 与整帧接管共用同一枚 `TryReadSource`，
  而该静态被 `Writer/DwgObjectStreamWriter.PreR2004Cmc.cs` 消费 ⇒ 一刀下去 `AC1015→AC1018` 的产物也变了
  +32 B（三枚短 CMC 现在被升宽）。口径 = 改后**逐产物 `cmp` 新旧两态**（整门每次把 `out/gate` 改名留档成
  `out/gate_prev_<ts>`，直接拿两目录同名文件对），再对**变了的那几格**单独查「头 == 目标魔数、有无 `.err`」，
  对本手目标格之外的变化要么证明 ODA 接受、要么收回闸。
- **帧内搜串：TV 与 TU 两形都落在位上、不在字节上**（§602 实测数）：同一枚 description 在 AC1015 源帧 =
  8 位 TV 形落 **bit 370**、未刀产物落 **bit 371**、升版后 = UTF-16LE TU 形落 **bit 815** ⇒
  `b'Standard' in frame` 与 `'Standard'.encode('utf-16-le') in frame` **双双 False**。据此写判据必假红，
  写「产物里没有那枚串」的结论必假阴性 —— 位级滑窗（把帧展开成 0/1 串再 `find`）才是唯一读数形。
- `oda_wait.run_converter(oda_exe, inp, outd, ver)` 的 **`inp` 是文件不是目录**，且输入暂存目录固定 = `<outd>_in`；
  把目录当 `inp` 传 ⇒ `PermissionError [Errno 13]` 落在 `shutil.copy2`。另：`oda_path.py` 在 **`tools/`**、
  不在 `tools/oda-compare/` ⇒ `sys.path` 得两条（`tools` + `tools/oda-compare`）。
- 「走查闭合 / 自家读者解得回来」被当成「目标形制对」的证据（§610 实测：五枚色各少写 32 位、门内五枚格挂
  `Invalid input` 数轮）⇒ 写侧判据的对照组必须是**原生目标档语料**，坑档 **D131**、详档
  `docs/issues/2026-09-27-mleader-cmc-method-in-rgb-610.md`。
