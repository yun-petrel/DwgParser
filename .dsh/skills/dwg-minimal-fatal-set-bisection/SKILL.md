---
name: dwg-minimal-fatal-set-bisection
name_en: DWG Minimal Fatal Set Bisection
name_zh: DWG最小致命对象集半集二分
description: Set-exclusion halving pipeline that pins the minimal fatal object set behind an ODA-REJECTed DWG cell with tools/oda-compare/inter_bisect.py. Control run bypass all must PASS for frame-level bisection, but its REJECT is a conditional reading, not a discard-the-cell verdict - Step 1.5 forks object-stream vs container by write-side ablation plus section-level swapsect validated on an already-green leg, and the control must be re-run after any section-level knife. Spec ^X keeps our frames for half the candidates and swaps the rest to oracle frames - REJECT means the fault is in the kept half, about log2(N) rounds narrow 1300+ frames to one; ^@<handle-list> does the cheaper per-class census first. Use when a rejected rich-drawing cell leaves hundreds of candidate frames, when a control REJECTs and abandoning looks tempting, or after one culprit is fixed and the cell still fails. Run the control per corpus-set and per source leg, not per backlog row: identical ODA wording can fork (one set PASSes and bisects to one frame, its sibling REJECTs naming an oursOnly handle, which no graft can adjudicate - go to Step 1.5), and one leg can hide two rings. Never compare your own bypass-all product against the oracle (circular - that frame already IS the oracle's); compare the ^X product against the all product. Body locks six measured traps plus ^X REJECT only proves X alone is fatal. Step 4.6 adds the sufficiency side: drop the caret so only N frames come from the donor, and the donor may be the same drawing's native AutoCAD file at the target version - this breaks the blind spot where ODA renumbers an object (ours 90EA vs oracle 922E) so it never enters inter and is never tested alone. Nine traps now, including: frame-width reads are polluted by the carried-over preview SBH payload, comp=1 raw pages are a one-shot refutation of the R2004 compressor, and the R2004 tail handle stream sits 16 bits off between objfields and SsInv plus may end with 1-7 all-ones align bits.
description_en: 排除式半集二分定位「最小致命对象集」，控制档 bypass all 先 PASS 钉住前提，再以 ^X 半集保留我方帧过 ODA，约 log2N 轮收到单枚，每轮自证规格生效。
description_zh: 用 tools/oda-compare/inter_bisect.py 做集合排除式半集二分。控制档 bypass all 先 PASS 钉住「病在 inter 内」；规格 ^X = X 保留我方帧、其余换成 oracle 帧 ⇒ REJECT 则病 ∈ X、OK 则病 ∈ 另一半；约 log₂N 轮 ODA 从上千枚收到单枚，每轮自证规格真生效（allowlist/hits 核数 + 产物 sha 比 all/none 两态）；更便宜的粗筛 = `^@<句柄清单>` 逐族穷举（一类一次 ODA；实测 28 族里只有两族单独致命）。**控制档 REJECT 不等于止损走人**：① 它是**条件读数** —— 类节 / 容器自己失步时任何帧级嫁接都救不了 ⇒ 任何节级 / 容器级刀落库后必复跑控制档（§562 类节刀后同一格控制档首次 PASS ⇒ 旧「须合成 242 枚对象、属语义裁定」整条作废）；② 下一格动作 = Step 1.5 写侧消融（整类 / 整句柄根本不 emit ⇒ 容器自建自洽）+ 节级 `swapsect`（正扫 + 反向 + 绿腿对照轮 + 产物字节自证），能把不具名判词变成具名链。附带六条实测陷阱（含 `BASE-COUPLED` 假靶、改过 Core 未重编 ProbeTool ⇒ 整轮 sha 复现上一手）与两条口径：REJECT 不可直接当容器归因；^X 收敛枚只证「单独即致命」，把 oracle 帧 graft 回去不算反证。用于富图格被 ODA 拒、差集与链台账都走到极限、候选仍有数百~上千枚、控制档不 PASS 纠结弃靶还是换层级，或修完一枚而本格仍不翻时。 Step 4.6 = 充分性侧：规格去掉 ^ 只换 N 枚（换掉它就翻），donor 可用**同图同句柄的原生件**，破「oracle 改号 ⇒ 该枚永不进 inter、从没被单独测过」这块盲区（§566 据此把廿六节墙压成一枚帧）。坑增至十条：帧宽账被照搬的预览 SBH 载荷污染、comp=1 原始页一票否证压缩器、R2004 尾句柄流 objfields 与 SsInv 差 16 位且末枚引用后可带 1-7 位全 1 对齐位。
argument-hint: 指名源图与目标档，如「半集二分 tests/DwgParser.Tests/TestData/add_2018.dwg ACAD2000」
argument-hint-en: Name the source drawing and target, e.g. "inter bisection tests/DwgParser.Tests/TestData/add_2018.dwg ACAD2000"
argument-hint-zh: 指定源图与目标档，如「半集二分 tests/DwgParser.Tests/TestData/add_2018.dwg ACAD2000」
user-invocable: true
---

# DWG 最小致命对象集：排除式半集二分（inter bisection）

目标：把「ODA 拒收我方产物，病在哪几枚帧」从**上千枚候选**收到**单枚**，只花约 log₂N 次 ODA。

## 何时才该用这条路（先判前两条是否已死）

- **差集不能用**：`firstdiff` 同档对拍后真差枚数 ≈ 名册总数（实测 1045 枚里 1024 枚真差）⇒ 「与 oracle 不同的那批」= 全集，无信息。
- **链台账到极限**：`chainwalk` 靠「换掉 ODA 点名的那一枚」推进，但被点名的帧逐枚被**同侧 oracle 配对**证无罪 ⇒ 点名推进 ≠ 病灶，继续点只会一直接收落点帧。
- 两条同时成立才起本手（每轮都是一枚真 ODA 格，≈2–3 分钟）。

**弃靶前先量「这一列还剩几格红」**：`python tools/winbuild.py python tools/oda-compare/richverify.py --all --sets all`
= 现存全部图纸集 × 全部源档的一次活矩阵（同目标版本并成一次转换器调用 ⇒ 一趟批量、分钟级，**不是**每格 2–3 分钟）。
实测一轮 = 360 格里 330 通过、28 红**全在** `→AC1018` 一列 ⇒ 「全门只剩这一列的红」不值得弃：弃掉等于把整条方向 A 的
最后一列交出去。秒数随语料与机器变，引用前现跑（该手未落秒数）。

**代理 ≠ 成因**：「某源特征 ⟺ 该列红」只能用来**排序**，不能立说成因果再照它落刀。该列上「源含 `AcDb:AppInfoHistory`」
在 AC1021 源 4/4 同真、看着像成因；据它连试四条改法（drop 该节 / 该节 sectype 17→12 / 该节页容量 `ceil128` + comp 轨改原生形 /
类节整节**不重编码原样搬运**）**全部仍不具名 REJECT**。四条负结果本身是交付物（替下一手省下四轮 ODA 学费），
真翻格的刀在另一节 —— 见 Step 1.5。

**边界**：格级流水线（oracle / firstdiff / gate）= `dwg-direction-a-cell-forensics`；两帧位流定谱 = `dwg-bitstream-diff-forensics`；尾句柄流形制 = `dwg-tail-form-audit`。本技能只管**候选集收窄**这一段。

## Step 0 预检（勿按文档断言）

```bash
command -v dotnet || ls -d ~/.dotnet 2>/dev/null    # 探不到 ⇒ 本手整条不可跑
ls -d tools/ODAFileConverter 2>/dev/null            # Windows 侧只用仓内转换器；ODA_EXE 为可选覆盖
ls -l <要二分的源图>                                 # 源图必须真在盘上（语料正在清退，见下）
```

**源图路径先核再指**（2026-09-24 实测）：`新增图纸/add_*.dwg` 与 `新增图纸/mtext_*.dwg` 正被「语料清退」那手摘除
（工作副本已无、index 里还在 ⇒ 别按旧档照抄路径），**同字节副本** = `tests/DwgParser.Tests/TestData/add_2018.dwg`
（sha1 `9274f56a…`、150,473 B，与 HEAD 的 `新增图纸/add_2018.dwg` 逐字节相同）。

驱动要两枚前置件：① `ProbeTool` 在本树已构建（新 worktree 先 `dotnet restore tools/ProbeTool`；**改过 Core 后必须单独 `-t:Rebuild` 重编它** —— 它不在 `DwgParser.sln` 里，门与构建都不替它编，症状与三件处置见坑「陈旧工具闸」）；② **oracle 落在驱动认的路径**：

```
out/interbisect/<源名>_<ACAD目标名>/oracle/x.dwg     # 缺 ⇒ 驱动 rc=2 并打印该路径
```

先跑 `chainwalk <源.dwg> <ACAD目标名> 6` 造出 oracle，它落在 **`out/chainwalk/<源名>_<ACAD目标名>/oracle/<文件>.dwg`**
（按源名 + 目标档分目录，不在 `out/chainwalk/oracle/` 这个平铺位置），再复制成上面那个 `x.dwg`。
目标名用 `ACAD2000`/`ACAD2018` 这一族（映射表 `AC1015↔ACAD2000` … 在 `chainwalk.DWG_TO_ODA`）。
`chainwalk` 对源图缺失是 `rc=2` 明说「源图不存在」⇒ 造不出 oracle 先查路径，别怀疑 ODA。

**`chainwalk` 只吃具名点**（ODA 判词不具名 ⇒ 起步即 `S={} -> REJECT` 早退），那类格改**手工现造 oracle**：ODA 直转源图到目标档，
再把产物拷成驱动认的 `oracle/x.dwg`（**文件名必须叫 `x.dwg`**，目录按 `<源名>_<ACAD目标名>` 死拼，不认任何别名）。
**勿跑完立刻拷**：`ODAFileConverter` 是 GUI 子系统进程，命令行先返回、工作进程稍后才落盘 ⇒ 第一批极易拷到**空目录**
（2026-09-25 实测：20:27 该目录只有 3.3 KB 的 `.err`，20:30 才出现 234,293 B 的 `x.dwg`）。正解 = 走仓内接缝
`tools/oda-compare/oda_wait.py::run_converter`（已含「等进程真退 + 零输出有界重试 + `CRASH` 独立态」），
或自行轮询到产物**字节数非空**再拷，拷完 `ls -l` 复核。手写 `subprocess.run` + 立即 `listdir` 就是 `NO_OUTPUT` 假红的成因。

## Step 1 控制档：先把二分前提钉住

`inter` = 源图句柄 ∩ oracle 帧（白名单句柄必须取**源图**句柄：我方写侧会重编号，取产物句柄 ⇒ 恒零命中）。驱动首轮自动做两件事：

1. `BYPASS_PRINT_INTER=1` + `spec=none` 取 `inter`，并算 `all`/`none` 两态产物的 sha 与 `hits`；
2. 把 `bypass all` 产物过一次 ODA ⇒ **必须 OK**。

不 OK ⇒ 病不在 `inter` 内（或另有容器病）⇒ 驱动 rc=3 停手，**别改成别的判据硬推**。控制档 verdict 缓存于 `<work>/rd_all/_verdict.txt`，续跑不重花 ODA（**但改过 Core 后这份缓存会留下旧产物的判词**，一并删，见坑「陈旧工具闸」）。

控制档 PASS 的附带战果：`inter` 之外的 `oursOnly`（实测 21 枚）与 `oracleOnly`（157 枚）当场全部脱罪。

**控制档本身是一条层级闸，一跑两得**（2026-09-25 实测，格 `AC1015->AC1018|sample`）：

- **OK** ⇒ 容器与 `inter` 之外无罪、**病在帧内** ⇒ 本格可从枚级 / 位级二分起手。这一条足以**否证**「签名是容器级
  `.dwg file needs recovery` ⇒ 死在容器层」这类立档前提（那份档十条排除全拿它当默认前提）。同一条 writer 路上的
  另一集（`|table`）绿 + 本格两态 sha 相异，是同一实验的免费对照（只说明「病随内容变」，不指字段）。
- **REJECT** ⇒ **枚级 / 位级二分当下不可起手**，但这**不是弃靶票** ⇒ 下一格动作 = **Step 1.5**（写侧消融 + 节级 `swapsect`），
  不是换靶。先核 `hits(all)=len(inter)`、`hits(none)=0`、两态 sha 相异，确认工具活着再认这个 REJECT。
- **★ REJECT 是「条件读数」，不是本格的永久属性**（2026-09-26 实测，代价 = 三手共用的同一条失效前提）：类节 / 容器层自己
  失步时**任何帧级嫁接都救不了** ⇒ 控制档必红，且这一列上枚级与节级的**全部历史读数一起作废**。层一（`AcDb:Classes` 尾两枚
  版本 `WriteBS→WriteBL`）落库后，同一格 `bypass all` **首次 PASS**（`inter=5283`、`hits=5283`、两态 sha 相异、产物 242,464 B
  OK）⇒ 本仓自 §523补七 / §530 / §526 / §547⑤ 记的「四格 `bypass all` 仍 REJECT ⇒ 病不在任何一枚我方帧、须合成 ODA 凭空造的
  242 枚对象、属**语义裁定**」**整条作废**（那些「三种 graft 全无通过根」的读数测的是同一个失效前提）。⇒ 任何**节级 / 容器级**
  改法落库后**必须重跑控制档**，旧 REJECT 不得续用；反过来每落一枚帧级刀也复跑它 —— 它是最便宜的「病仍在帧层」哨。
- **但 REJECT 仍不可当层级归因**（≠「病在容器」）：`oracleOnly` 很大时（实测本格 240 枚）换成 oracle 帧 = 同时引进悬空引用，
  控制档会被**污染**而假红。定性另走 **Step 1.5 写侧消融**（2026-09-26 已把它从「不算判据」升格成判据，见下）。
  ⚠ 旧口径「节级 `swapsect` 双向 ladder 两向都不算判据」**已被否证** —— 它在**没有对照轮**时才成立。

## Step 1·补 控制档要**逐集 × 逐源腿**跑，不按 backlog 行当整体（§565 实测）

同一行记的「六格同签名」在控制档层面可以完全分叉：`富-LAYER-1` 六格判词逐字相同（不具名 `Object improperly read`），
而 `bypass all` 实测 = `textandrect` 三格 **OK**（帧层可二分，二轮收到单枚 ⇒ 翻两格）、
`lineanddim` 三格 **REJECT 且具名 `oursOnly` 的 `<AcDbMLeaderStyle>(228)`** ⇒ **那一集整层 graft 判不动**，
直接转 Step 1.5（具名点在 `oursOnly` = 它不在 inter 宇宙里，别在候选集中找它）。

配套三条（§565 同一手换来）：
- **一条腿常不止一环**：AC1024 腿二分收敛到单枚 `32C`，AC1027 腿（同图同族）收敛到 **2 枚 `{32B, 32C}`**
  ⇒ 修掉第一枚后**必换新 `--state` 再二分**（Step 4.5 那条），别按「一枚治一腿」写收尾判据。
- **循环读数陷阱（本手自否）**：判「我方帧 ≠ oracle 帧」时拿 `bypass all` 产物去对 oracle = **必然全等**
  （那枚在 `all` 里本就是 oracle 帧）。唯一正确配对 = **`^X` 产物 vs `all` 产物**（`^X` 才保留我方那枚）。
- **位锚口径**：`objfields` 打的 `handles[BodyStart..BodyEnd)` 覆盖**整帧**、不是句柄流（`HasHS=False` 时尤甚），
  且 `BodyEnd − BodyStart` 比 `SsInv --framedump` 的 `bytes=` **少 4 字节**（不含帧首尺寸前缀）
  ⇒ 判据档钉的数与帧面实测的数差 4，两侧别混用；`widths` 对 AC1018 目标可整族不点名、`rectypes` 第二参不生效。

## Step 1.5 控制档 REJECT 之后的第二条路 = **写侧消融**（帧级嫁接不可判，但这两件可判）

枚级 / 位级二分的**唯一**前提是「帧级 graft 能造出一个可信的 OK 底座」。造不出来时，**别止损走人**，
先做一次**层级分叉**：这条实验不碰任何外来帧，只让**我方自己的写侧少写东西**，所以容器永远自洽。

1. **整类不 emit ⇒ 对象流 vs 容器的分叉**（本手实测形：给 `DwgObjectStreamWriter` 的 `ordered` 组装处加一次性
   env 探针 `DROPCLS=<逻辑类,>` / `DROPH=<句柄hex,>`，**只活隔离树、勿入 main**）。判据 =
   **同一个目标档、同一个写侧、只差源版本那一腿**，把对象流**清空**（`ProbeTool widths` 报 0 枚自证规格生效）：
   - 绿腿清空后 **仍 OK** ⇒ 消融器械本身不引入红（**这一条控制轮必跑，否则读数全部作废**）；
   - 红腿清空后 **仍 REJECT** ⇒ **病不在任何一枚对象帧上**（比 `bypass all` 强：它不嫁接、不换号、不动句柄空间）。
2. **节级 `swapsect` + 对照轮 = 定位到具体哪一节**：把**原生同档图**的某一节换进我方红产物。
   判据 = **同一器械作用在已翻绿那一腿上仍 OK**（实测：红产物换 `AcDb:Classes` ⇒ 判词从**不具名**变**具名链**；
   绿产物换 `Classes` / 换 `Template` ⇒ **仍 OK** ⇒ 器械无罪、该节确有病）。
   逐节试一遍只花 N 次 ODA，而它买到的是**具名** ⇒ 之后才轮得到 Step 2~4 的枚级链。
   配套取证件 = `secsave <dwg> AcDb:Classes <出.bin>` 导**原始节载荷**直接逐字节对（比类名/计数可靠：
   类名级对表会漏掉位布局，实测「红腿前 75 行逐字段等、只缺末行 `DATATABLE`」这种读数就指不到字段）。
3. **反向 `swapsect` = 对象流整层脱罪（不改码的那条对照，与消融 1 同问）**：
   `swapsect <oracle 产物> <我方产物> <out> AcDb:Handles,AcDb:AcDbObjects` ⇒ **OK** = 我方对象流装进 oracle 容器被接受
   ⇒ 账只剩容器 / 节层。实测它与正扫「12 节里只有 `AcDb:Classes` 出 PRODUCT（1226 B → 1213 B）」**两支同向**，
   才把病灶钉在**我方写出的类节字节**、而非任何一枚对象帧。节级 `swapsect` **不换句柄、不造悬空** ⇒ 不受 `oracleOnly` 污染。

器械硬规（少一条 ⇒ 该轮读数不成立）：
- **自证真换进去了 = 比产物字节数**：只比判词会把**空操作**读成「该节无罪」。实测反向陷阱 = `secdump <f> AcDb:Classes`
  报「无节」而 `swapsect` 换同名却真换到东西 ⇒ 换前先 `secdump` 逐名点存在性，换后认 `swapsect` 自己打的 `SWAP <节>: -> <n>B`。
- **器械自证**：`swapsect <x> <x> none`（零变化重装）须**不引入红**（绿档仍 OK、红档判词不变）⇒ 否则判词不可归属。
- **逐节各一次 ODA、单发跑**：批量喂转换器会崩（实测），崩了的重跑不等于读数。
- **单节全否 ≠ 节级无罪**：最小通过集可以是**多节组合**（判例 = `tools/oda-compare/bisect_obj.py` 的三节
  `TRIO = AcDb:AppInfo,AcDb:Classes,AcDb:SummaryInfo`）。正扫全 REJECT 后再走组合支 = `tools/oda-compare/bisect_sect.py`
  （对「换哪几节」做 ddmin；⚠ `bisect_*` 四枚仍硬编 `ODAExtract` 路径，复用前先改）。
- **射程硬限**：`swapsect` 要求 base 与 donor **同为 R2004+**（`tools/ProbeTool/Program.cs:3911` 直接打印
  「swapsect 需要 R2004+ 文件」后返回）⇒ **AC1015（R2000）目标上这条支根本不存在**，那一列只能回容器重建类的别的取证。

**收口线**：一轮正扫 + 一轮反向 + 上面这些自证全做完仍「无任何通过根」⇒ 本格按**真红留档**收口，别投第三轮嫁接。
⚠ 该收口同样是**条件读数**：别处一旦落了节级 / 容器级刀，按 Step 1 那条复跑控制档，本格会自动回到帧级射程
（§553 那次「枚级与节级两条二分在本格都已判死」正是被 §562 的类节刀整体推翻的）。

三条实测副作用（写结论前必查）：
- **对象流非空时 drop 单枚会打断 owner 向量** ⇒「drop 掉具名枚即翻」**不可判**（实测 `swap_Cls` + `DROPH=具名三枚` 四格
  全 REJECT、判不出链长）。数链长只能走**真修**，不能用 drop。
- **`nInst` 不承重**：原生自己的类表就有 47 处 `nInst != 实际枚数`（我方红 46 / 绿 12），而原生与绿都被 ODA 接受
  ⇒ 别再把它当「与原生余差」的候选根因。
- **「与原生逐行等形」不能当判据**：绿腿类表第 60 行就与原生分叉（`CELLSTYLEMAP` 与 `ACAD_EVALUATION_GRAPH` 次序）
  却 OK；红腿前 75 行逐字段等却红 ⇒ 拿原生当尺只能筛「形状」，**能不能翻格只认 ODA 二值**。


## Step 2 每轮自证规格真生效（不是可选装饰）

一轮 = 一次 `bypass` + 一次 ODA。过 ODA 之前先核四件事：

| 查 | 期望 | 不对意味着 |
|---|---|---|
| `BYPASS allowlist=` | `len(inter) - len(keep)` | 规格被吞 / 句柄取错侧 |
| `BYPASS done hits=` | = `allowlist` 枚数 | 白名单没进写出路 |
| `sha(产物)` | 异于 `sha_none` | 等于 none ⇒ 一个句柄都没换，规格没生效 |
| `sha(产物)` | 见下条陷阱 | 等于 all ⇒ 两种成因，别一律当故障 |

`rc != 0`、`allowlist` 缺行、数不符 ⇒ 驱动 rc=4 停手；`CRASH` 或未判定 ⇒ **该轮不采信**，重跑同一轮（驱动把 `round` 退一格）。

## Step 3 排除式半集二分

```bash
python tools/winbuild.py python tools/oda-compare/inter_bisect.py tests/DwgParser.Tests/TestData/add_2018.dwg ACAD2000 3 --state out/_bisect.json
```

（PowerShell / VS 开发者命令行可去掉 `winbuild.py` 前缀。第三参数 = 本次调用跑几轮；跑满全程必然超时，**靠重复同一行命令续跑**，状态从 `--state` 读回。在隔离 worktree 里跑，状态与产物就落在那棵树。）

**先粗后细：族级穷举通常比逐枚二分更便宜**（2026-09-26 实测）：规格写 `^@<句柄清单文件>`（该文件 = **某一类的全部源句柄**，
一行一枚）⇒ 「只保留该族我方帧、其余 `inter` 全换 oracle 帧」，**一类一次 ODA** 直读 ⇒ 28 族一趟扫完而不用 log₂N 轮。
实测该列扫下来**单独足以致命的只有两族** = `FIELD(568)` 4 枚 + `ACAD_TABLE(555)` 2 枚，其余 26 族全 OK（含 `TABLECONTENT(564)`
/ `TABLEGEOMETRY(572)` / `DATALINK(565)` / `VISUALSTYLE(557)` / `MLINESTYLE(558)` / `DICTIONARYVAR(500)` / LAYER / STYLE / HATCH …）
⇒ 判词里出现的 `<AcDbTableContent> (90EA)` 是**级联症状不是病灶**（我方 FIELD 失步后 ODA 的下一跳）。**充分性另测** =
只换这两族共 6 枚（`allowlist=6 hits=6` 自证生效）⇒ ODA OK ⇒ 「翻格 ≡ 做好这两族」由构造证死（这比逐枚收敛更强的账）。
⚠ 序会记错：本手先记成 `FIELD → TABLECONTENT`、穷举后才订正为 `FIELD → ACAD_TABLE` ⇒ 具名判词的**先后**只当线索，不当链谱。

语义与收窄方向（`tools/ProbeTool/Program.cs` 的 `^` 支 = 从 `inter` 里减去排除集后旁路）：

- `spec = ^keep` ⇒ `keep` 保留我方帧、其余换成 oracle 帧；
- ODA **REJECT** ⇒ 病枚在 `keep` 里（候选 = 前半）；
- ODA **OK** ⇒ 病枚在候选的后半（`keep` 之外）；
- 产物 sha **等于 `all` 态**有两种成因：规格没生效（**必须停**），或保留那几枚我方帧本就逐字节等于 oracle（合法的「该半无罪」，按 OK 收窄并记 `SAME_AS_ALL`）。只有等于 `none` 态才一律是故障。（第三种「相等」不在此列：**与上一手 / 旧 state 的逐轮 sha 相同** = 工具未重编，整轮作废，见坑「陈旧工具闸」。）

**`^X` REJECT 买到什么、没买到什么**（写刀口结论前先背这段）：

- 语义 = 「其余 `inter` 帧全换成 oracle 的底上，**只有 X 保持我方帧**这一件事单独即致命」⇒ 账只能记成
  **「不修 X、本格不会翻」**。它**不**证「修完 X 本格就翻」（同格可还压着第二枚 ⇒ Step 4.5）。方向别写倒：
  不是「单独换回 oracle 即 REJECT」，而是「单独保留我方帧即 REJECT」（`^X`，不是 `X`）。
- **反向 graft（只把 X 换成 oracle 帧、其余仍我方）不能当必要性反证**：2026-09-25 实测 `^1DBC` = REJECT 且具名
  `<AcDbViewport> (1DBC)`，而 graft 同一枚后签名**退成容器级 `.dwg file needs recovery` 且不具名** ⇒ 这条读数只能记
  「没救回来」，**不能**记「病灶外移」。混合底座本身会造耦合假象（坑 **D121**：oracle 自增对象 / 表控登记数不匹配
  ⇒ 换进去的帧无论内容合法与否都红）。
- **可用尺只有两条**：① 三侧同枚 `secsave` 帧逐字节 ≡ oracle（两侧帧头各自闭合时可直接比）；② 整格 / 整门复跑看翻不翻。
  勿拿「ODA 少报一枚」当尺。

## Step 4 收到 ≤2 枚后手工收尾

驱动到 1 枚即停；剩 2 枚时逐枚单判（实测就靠这一步定案）：

```bash
python tools/winbuild.py dotnet run --project tools/ProbeTool -- bypass <源.dwg> <oracle.dwg> out/_k.dwg ^16A95
# keep{16A95} ⇒ REJECT、keep{16A96} ⇒ OK ⇒ 最小致命集 = 单枚 16A95
```

再取该枚身份：`dotnet run --project tools/ProbeTool -- rectypes <源.dwg>` 查类型码（`t=44` = MTEXT）。

**收尾前必查的一步：单枚可能是底座的耦合假象（实测 2026-09-25）** —— 控制档 `bypass all → OK` 的底座是
「全 oracle 帧」的文件，而 **ODA 降版会自增对象**（实测本图 oracle 比我方多 153 枚，其中含表控的成员类层记录）。
若收敛到的那枚是**登记别对象的容器**（`*_CONTROL` / 字典 / xref 之类），换成我方帧就必然出现「有成员未被登记」
⇒ 必红，与帧内容对不对无关。**判据**：比该帧的登记数与非空条目枚数 vs 底座内该成员类的记录数
（实测 73 vs 72 一比即穿），不等 ⇒ 报告里标 `BASE-COUPLED`，此枚**不得**当病灶落刀。
同源反例：把**原生 AutoCAD 的合法同句柄帧**移植进我方产物也照旧 REJECT（其条目指向原生自己的句柄 ⇒ 又造悬空）
⇒ 「单枚移植法」在这类格上**不具判别力**，只能记「没救回来」，不能记「病灶外移」。

## Step 4.5 一格常不止一枚：修完第一枚**换新 state** 再二分

Step 4 只买到「必修」，不买到「翻格」。落完第一把刀后若整格仍 REJECT，**下一手 = 用改后产物重跑整条链**，
而不是回头怀疑第一把刀错。实测链（格 `AC1015->AC1018|sample`，树 `out/_flip549`）：13 轮收到 `1DBC` = `<AcDbViewport>`
⇒ 补上 `SINCE(R_2004a)` 的 `shadeplot_mode` 后五枚视口逐字节 ≡ oracle、**整门 rc=0 零回归而本格仍 REJECT**
⇒ 换新 state 再 12 轮 ⇒ 第二枚 `892B` = `AcDbMaterial`（`kind=Opaque`，`xcmp` ours 98 B / ref 96 B，同族 3/3 枚恒 +2 B）。
两枚病灶互不相干 ⇒ **修了 N 枚不翻不能当作「二分给错」的反证**（签名不具名时尤其如此）。

**为什么必须换全新 `--state`**：`inter` / `cand` / 两态 sha 全取自 state，而三者都随**改后产物**变（帧字节变了、
句柄集也可能变）；沿用旧 state = 拿旧产物的候选集与两态 sha 去裁新产物，Step 2 那四查全部失去意义。新 state 首轮会重花
一次 `all` / `none` 与控制档，但控制档 verdict 按目录缓存 ⇒ 改过 Core 时连它一并处理（坑「陈旧工具闸」）。

## Step 4.6 充分性侧的手 = **白名单只换 N 枚**（在自家底座上），且 **donor 可换同图原生件**（§566 实测）

排除式二分（`^keep`）买到的是「**不修 X 本格不会翻**」（必要性）；它**不**回答「修好 X 就翻」。补这条只有一步：
把规格里的 `^` 去掉 ⇒ `bypass <src> <donor> <out> 90EA` = **只把这一枚换成 donor 帧、其余全保我方**，
ODA 出件即「换掉它就翻」= **充分性由构造证死**（成本 = 一枚 ODA 格）。先核 `BYPASS allowlist=1 hits=1` 再认读数。

★ **donor 不必是 ODA oracle**：改用**同图、同句柄、目标档的原生 AutoCAD 件**（如 `新增图纸/sample_2004.dwg` 之于
`sample_2007 → AC1018`）能破一类盲区——**oracle 会把对象改号**（实测 `TABLECONTENT` 我方 `90EA` → oracle `922E`），
于是那些枚**永远不进 `inter`、在排除式二分里换不掉、也从没被单独测过**；原生件与源图句柄同名在册
（`rectypes` 一比即见 `58F1/8FFF/8F6A/90EA/8CD8…` 全等）⇒ 换得动。§566 就是这一步把一面写了廿六节的墙
从「两族六枚 + 还要过 LAYOUT」压成「**换 90EA 一枚即 OK**」。
两侧对照读法：oracle 与原生件**各自单独换同一枚都 OK** ⇒ 该枚的「可接受形」不依赖某个特定生产者。

**用 donor 时的三条限制**（都实测踩过）：① 原生件与源图**不是同一份字节**（预览/句柄重编号/类表行数都不同），
所以「换 X 即 OK」只证 X 这一枚的形可接受，**不证我方别处无罪**；② 原生件对象集与 oracle 不同
（实测 `inter` 5292 vs 5283、`misses=77`）⇒ 两态 `hits` 不可互相引用；③ 原生件的类表/预览会让 `.err` 里多出
`RegApp "925B" Invalid`、`Invalid Layer` 这类**嫁接噪声警告**，别把它当我方病灶（判读只看致命行）。

## Step 5 判读侧还是写侧（另起一手，别顺手带）

拿到单枚只买到**射程**。分清两侧最省的一次实验：把源图用我方升到**另一档**（ODA 接受的那档）再让 ODA 出 DXF，与源图自己的 DXF 逐值对表 ⇒ 值全等 ⇒ 读侧清白、刀口在目标档写支。**被拒产物拿不到自己的 DXF**，勿硬走那条。

## 十条判据级坑（实测代价换来的）

1. **`INTER_LIST` 打的是 `ulong` 十进制，规格按十六进制解析** ⇒ 直接把十进制串喂进规格，实测 663 枚只中 24 枚、`allowlist` 只减 24 ⇒ **假收窄**。驱动内已改 `f'{int(h,10):X}'`；手写命令时同样要转。
2. **空 allowlist 在 Core 侧语义 = 「不设限」** ⇒ 全旁路、产物逐字节等于 `all`、`rc=0` 零告警。曾据此产生过三次假 PASS（被当成「单枚交 oracle 即 OK」的子集结论）。现护栏：排除集为空或零命中 ⇒ `rc=2` 不出产物（`tools/ProbeTool/Program.cs` 里 `Bypass` 的两处 `BYPASS FAIL` 分支）。**护栏落地前的历史读数一律不采信。**
3. **位级走查先证锚**：同一来源会话里四次判否同源 —— 游标没落在 `record.GeoStartBit`、或把 CRC 段算进了句柄流。二分只给射程，**机理另手取证**，且写档位结论前先按锚复量。
4. **`--state` 的归属**：`inter`/`cand`/两态 sha 取自 state，而 `ver`、工作目录与 oracle 取自 argv ⇒ 指错或沿用别格的 state 会**静默二分另一格的候选集**，逐轮读数看着完全合理、`rc=0` 零告警（实测过这一形态）。现护栏（2026-09-24 落码）：state 内 `src`/`ver` 与命令不符 ⇒ `rc=2` 并打印两侧值（同文件不同分隔符 / 盘符大小写不误伤）。**护栏落地前那几轮凡指错过 state 的读数一律不采信**；**一格一个 state 路径**，续跑前仍可读出当前格位：

    ```bash
    python -c "import json;s=json.load(open('out/_bisect.json',encoding='utf-8'));print(s['src'],s['ver'],'inter',len(s['inter']),'cand',len(s['cand']))"
    ```

5. **二分只保证「换掉这枚就红」，不保证「这枚本身错」** —— 容器类帧（表控/字典）的致命性常来自**底座对象集与我方不等**（见 Step 4 的 `BASE-COUPLED` 判据），此时 12 轮收窄给出的是一枚**假靶**；实测两车道先后据它立过「幽灵引用」根因，后被读侧普查否证（条目枚枚在册、帧与原生同骨）。**收到单枚后先做一次登记数核对再动手**。

6. **陈旧工具闸：改了 Core 只 `dotnet build DwgParser.sln` ⇒ `ProbeTool` 仍是旧 dll**（它不在 sln 里，门与构建都不替它重编）。**判据 = 某轮产物 sha（乃至整串逐轮候选序列）与上一手 / 旧 state 逐枚相同** ⇒ bypass 由旧码产出，**整轮读数作废**（2026-09-25 本手真废过一轮；仓内入库为坑 D124，该编号现存两枚同前缀条目 ⇒ 按标题检索）。三件处置：① `python tools/winbuild.py dotnet build tools/ProbeTool -t:Rebuild`（新 worktree 还须先 `dotnet restore tools/ProbeTool`）；② 换全新 `--state` 重跑（旧 state 的 `inter` / 两态 sha 也是旧产物口径）；③ 删 `<work>/rd_all/_verdict.txt` —— 控制档 verdict 只按「`<源名>_<ACAD目标名>`」目录键缓存（`inter_bisect.py` 里 `work` / `cached` 两行），`all.dwg` / `none.dwg` 会随新 state 重生成、**判词不会** ⇒ 可能拿旧产物的 OK 放行新代码的结论（此条由代码读得，尚未实测）。`richverify` / `chainwalk` / `gate.py` 同判：**都不自带 build**。

7. **帧宽账会被预览（SBH）载荷整体污染**：跨版本降版把源档 preview **逐位照搬** ⇒ 「我方表比原生/Oracle 多约 1 KB」
   这类读数是**预览、不是语义**（§566 实测：我方 `size` 与源档枚枚相等，切段后几何段与原生**逐位全等**，
   据此否证 §563 补四 的「每表多写约 1 KB」）。正解 = 量宽之前先按
   `[RL][own][EED][B preview][RL size][preview][CED][body]` 切段（`ceddump` 给 `preview has/size`；
   几何段起点 = `record.GeoStartBit`；cells 段用 capture 的 `CellStartBit/CellEndBit`）。
   配套一条：**内容罪与长度罪要分两跑**——「写成**等长**全零」与「整枚换成 donor 帧」同时测，
   才能区分「预览内容致命」与「预览长度致命」（§566 两跑都 REJECT ⇒ 两者皆非，靶外移到别枚）。
8. **怀疑容器层时，有两条一票否证比逐枚猜便宜**：① `pagecrc` 全页一致**不构成证据**（实测对 OK 态与 REJECT 态
   产物给同一读数——自洽度量只看自己）；② 直接把 R2004 对象流/句柄流改写成 **`comp=1` 原始页**
   （`R2004Writer.IsRawSection` 加环境开关）再 ODA 直读 ⇒ 仍红即**压缩层无罪**（§566：235 KB 涨到 572 KB 仍
   `REJECT`，一次否掉「我方 RS/LZ77 编码器编出 ODA 解不动的流」这一整类猜测）。
9. **R2004 尾句柄流的坐标与对齐位两条坑**：按 `66 + RL` 反推句柄流起点时，`objfields`（自 `BodyStart` 起）与
   `SsInv --framedump`（自 `MS` 起）**差 16 位**，不减就解出 `counter` 吃进 CRC（本手连着失步两次）；
   且**末枚引用之后可带 1–7 位、原生件实测为逐位全 1** 的对齐位（backlog 行 `富-1015-CTL` 已钉），
   `TrimPreR2004BodyHandlePad` 的「被裁区须逐位全零」护栏对它恒不合 ⇒ 量「我方少几枚 ref」之前先把这两条钉掉。

10. **半集交换对「互引的族」会自造红**（§602 补二 实测）：当 `bypass ^X`（只保留我方一枚 X、其余全换成 oracle 帧）
    报红时，`.err` 点名的句柄**常常不是 X，而是被换成 oracle 帧的兄弟枚** —— 本轮 `^8B99` 报
    `Invalid Layer × AcDbMLeader(8BA2/8BB6/8BCB/8BE0…)`，因为 oracle 的 MLeader 帧里那枚 code-5 指向
    **oracle 自己图**的 `LAYER_CONTROL(9343)`，而该句柄在我方图里根本不存在（我方同类同位的对应物是 `9290`）。
    ⇒ 由「二分收敛到唯一候选」推「病灶就是这一枚」**必须**补两步：① `bypass <那一枚>` 单换它，看签名变不变
    （本轮与 `none` 一字不差 ⇒ 换掉它毫无后果，候选作废）；② 拿两枚**无关**帧做正控（本轮 `^1ED9` / `^8FE0`
    皆 OK ⇒ 器械确有判别力，红只可能来自族内互引）。两条都过，候选才允许写成「这一枚」；否则一律写成「**这一族**」，
    并且紧接着做**族内逐枚对表**（`DvWalk hdl` 比句柄流形状、`DvWalk scanstr` 比串位、`prevalign` 段落差）——
    本轮就是靠这三条把靶从「`h=8B99` 那一枚 + 猜串形制」改成「MULTILEADER 一族、病灶在数据区、
    我方比 oracle 长 336 位、自 public head 后第 1 位分岔」，并顺手否掉了「串集中化那一族」的处方。

> 停手消息一律用 ASCII `->`：本机 python stdout = GBK，`⇒` 直接 `UnicodeEncodeError`，护栏会崩成 `rc=1` 而报不出 `rc=2/3/4`。

## 实测样例（读数属当轮快照，引用须带树 + 提交号）

树 `out/_rich2` @`e35ab112`，格 `add AC1032→AC1015`（当时源图 = `新增图纸/add_2018.dwg`，现由 `tests/DwgParser.Tests/TestData/add_2018.dwg` 同字节顶替）：`inter=1325 / oursOnly=21 / oracleOnly=157`，控制档 `bypass all → OK`。
`1325→662→331→165→83→41→20→10→5→2` 共 9 轮自动 + 手工单判 ⇒ 最小致命集 = **1 枚 `16A95`（`t=44` MTEXT）**，签名 `Invalid group code`；链上被点名的 `D5/B54/B53/1637/1636/18F8/18F7/84D7` 全部实证只是落点。第 10 轮正是「等于 `all` 态 = 该半无罪」那一类（`16A94`）。**射程至今未翻，机理已两度易手** ⇒ §十六·补 的「584 位 = 未再内联正文」被 §十七 判否，现指 EED 换码 / 子项走查错位（§二十四、§二十六）。详档 = `docs/issues/2026-09-23-attdef-tail-ac1015-reinline-flips-two-cells.md` §十二～§二十六。

第二格（同一驱动，量出「控制档当层级闸」与「多靶链」两条口径）：取证树 `out/_flip549` @`3666fafa` + 视口刀（未入库），格
`sample_2000.dwg AC1015->AC1018`（富图，签名 `.dwg file needs recovery`）：`inter=5287 / oursOnly=182 / oracleOnly=240`，
控制档 `bypass all -> OK` 且两态 sha 相异（`821ac91965dcce18` / `c6b8da1eb96d6803`）⇒ 该档旧立档前提「死在容器层」被当场否证。
13 轮 → `1DBC`（`<AcDbViewport>`，全程具名）⇒ 补上 `shadeplot_mode` 后五枚视口逐字节 ≡ oracle、整门 rc=0 零回归
而本格仍 REJECT ⇒ 换新 state 再 12 轮 → `892B`（`AcDbMaterial`，`kind=Opaque`）。**本格未翻格**、两枚均未代落。
同手另跑三格控制档（`AC1021/AC1024->AC1018|sample` 等）均 REJECT 且两态 sha 相异 ⇒ **按当时口径记成了「直接止损」**
（该口径已被下面两段推翻：现读法 = Step 1 的「REJECT 是条件读数」+ Step 1.5）。
详档 = `docs/issues/2026-09-25-ac1015-to-ac1018-recovery-523.md` §一 / 补 / 补七。

**Step 1.5 首次实跑**（2026-09-26，树 `out/_h558` @`feefd0fd`，四格 `AC1021|AC1024|AC1027|AC1032 -> AC1018 | sample`）：
上一段那句「控制档 REJECT ⇒ 止损」被本手升级 —— 同样的四格走写侧消融即出层结论：
对象流清空后 `sample_2000->AC1018`（绿腿，控制轮）**仍 OK**、`sample_2007->AC1018` **仍 REJECT**
⇒ **病不在任何一枚对象帧上**（否证 §553「病回到对象帧内容」）；再逐节 `swapsect` 换原生 `sample_2004` 的节 ⇒
只有 `AcDb:Classes` 让判词**从不具名变具名** `<AcDbDataLink>(8E99)` + `AcDbLayout(6C)/(6D)` + `CMLEADERSTYLE`，
而同一器械作用在绿产物上仍 OK ⇒ 首个拦路 = 类节形制。⚠ 本手**未翻格**、零 src 落主树。
详档 = `docs/issues/2026-09-26-rich-ac1018-four-cells-container-class-section-560.md`。

**这条链随后真翻格**（同列下一手，取证底 `out/_rt560` @`dba02ba3` → 落库底 `70179d42`；`未 bless、未推`）：Step 1.5 把病灶定到类节后，
刀 = `src/DwgParser.Core/Format/R2000ClassTable.cs` 的 `BuildR2004ClassesSection` 尾两枚版本 `WriteBS→WriteBL`（+2/-2，提交 `e86cc8e6`）。
**2026-09-26 现核：该刀仍在 HEAD 血缘、该类记录确发 `WriteBL`** ⇒ 别道 §560 二十一 撤的是它自家并行 patch，不是这笔回退。
机理 = 按 §562 §二那张位宽表（值 ≤255 时 BS/BL 共用短码、值 >255 才分叉 ⇒ 类记录尾字段失步、ODA 顺类表走到尾部抛**不带对象名**的
improper read；真实语料里 `MLEADERSTYLE`=(33,327) 正落进分叉）。⚠ **该表未被本手独立重量，且同族位宽账有主**：§560 二十一 自否过一条
（推 4 字节 / 实测每条记录多 5 字节），并撤回「读侧早已按 BL 定谱 ⇒ 写侧漏改」这条腿（实测两版式都读得回、靠校验择一）
⇒ **落同族刀前自己重数一遍，勿引这段当算术证据**；这段里过硬的只有两条 = 刀在 main、以及下面那行判词与格数的实际变化。读数：`cross_producer` 四枚
`AC1021|1024|1027|1032→AC1018` 由 REJECT→OK（该面 26/30→30/30）+ 新增语料 `v1/v2/v6_autocad AC1021→AC1018` 三枚、
富图面 55/60→56/60（余四枚判词由不具名变具名 `<AcDbLayout>` ⇒ 链前移一层、本格未翻）；判据档
`tests/DwgParser.Tests/ClassRecordVersionWidthAc1018Tests.cs` 4 例（零 ODA 零语料：同夹具只换版本值量产物字节增量）
+ 源码级消融（改回 `WriteBS` ⇒ 恰 2 红 / 2 绿）；整门 `GATE_RC=0` 零回归。⇒ **落刀后复跑控制档**得到的就是上一段那条
「首次 PASS」读数（这正是 Step 1「REJECT 是条件读数」的正面样本）。详档 =
`docs/issues/2026-09-26-classrecord-version-bits-ac1018-562.md` §一~§五 + `docs/issues/2026-09-26-classrecord-third-leg-and-combined-gate-563.md` §补三 / §补四。

## 命令速查

| 目的 | 命令 |
|---|---|
| 列合法句柄集 | 首轮驱动自带（`BYPASS_PRINT_INTER=1` + `spec=none`），打印 `inter=`/`hits=`/两态 sha |
| 跑 / 续跑二分 | 同一行重复：`python tools/winbuild.py python tools/oda-compare/inter_bisect.py <源> <目标> 3 --state out/_bisect.json` |
| 单轮手工旁路 | `dotnet run --project tools/ProbeTool -- bypass <源> <oracle> <out> ^h1,h2`（规格另支持 `@文件`、无 `^` = 白名单式） |
| **族级**穷举（一类一次 ODA） | `dotnet run --project tools/ProbeTool -- bypass <源> <oracle> <out> "^@<句柄清单.txt>"`（清单 = 该类全部**源**句柄 hex 每行一枚；分族先 `rectypes <源.dwg>`；`^@file` 两支可叠 = `Program.cs:3839`） |
| **节级**归因（控制档 REJECT 后） | `dotnet run --project tools/ProbeTool -- swapsect <base> <donor> <out> "AcDb:Classes"`（⚠ base/donor **须同为 R2004+**、AC1015 直接返回；换前 `secdump <f> <名>` 点存在性，换后认 `SWAP <节>: -> <n>B` + 比产物字节自证） |
| 导原始节载荷逐字节对 | `dotnet run --project tools/ProbeTool -- secsave <dwg> AcDb:Classes <出.bin>`（比类名/计数可靠，位布局看得见） |
| 弃靶前量「该列还剩几格红」 | `python tools/winbuild.py python tools/oda-compare/richverify.py --all --sets all`（同目标版本并一次转换器调用 ⇒ 一趟批量、分钟级） |
| 查类型码 | `dotnet run --project tools/ProbeTool -- rectypes <源.dwg>` |
| 改过 Core 后重编取数工具 | `python tools/winbuild.py dotnet build tools/ProbeTool -t:Rebuild`（不在 sln 里 ⇒ 门与构建都不替它编；整轮 sha 复现上一手 = 未重编） |
| 清控制档 verdict 缓存 | 删 `out/interbisect/<源名>_<ACAD目标名>/rd_all/_verdict.txt`（目录键不含代码版本 ⇒ 改码后必删） |
| 手工现造 oracle（不具名格） | ODA 直转源→目标档，**轮询到产物非空再拷**成 `<work>/oracle/x.dwg`（接缝 = `tools/oda-compare/oda_wait.py`） |
| 退出码 | `2` 用法 / 缺 oracle / 取 inter 失败 / **state 归属不符** · `3` 控制档不 PASS · `4` 规格未生效 · `0` 收窄正常或已到单枚 |
