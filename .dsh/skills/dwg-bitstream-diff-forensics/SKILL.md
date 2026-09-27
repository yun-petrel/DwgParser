---
name: dwg-bitstream-diff-forensics
name_en: DWG Bitstream Diff Forensics
name_zh: DWG位流差异取证
description: Bitstream-level diff forensics for two DWG object frames of the same handle. Runs tools/oda-compare/bitdiff.py to verify the [MS][BS][RL][data][handle-stream][CRC] header self-consistency, census long common blocks with a greedy-free seed-and-extend pass (which decides insert/delete vs reorder), recurse into an alignment tree plus gap list, and anchor field identities with logical ground truth taken from ODA-generated DXF. Also carries the measured per-version string encoding rule (TV/GBK with BS(n+1) before R2007, TU/UTF-16LE with BS(n+2) after). Use when firstdiff/xcmp already reported which frame differs and how many bytes, and the next step is to pin the field identity, width and position; or when a reorder/insert verdict must be re-verified. When tallying widths across a whole class, measure against the same-content *native* file and require ours-minus-native to be uniform (or |ours-oracle| > |native-oracle|) — ODA's downgrade synthesizes an extra hard-owner/XDIC handle, so an ours-vs-oracle ledger carries synthesis noise and can misrule a real defect as benign.
description_en: Bitstream-level diff forensics for two DWG object frames (header self-check, greedy-free seed-and-extend census, recursive alignment tree, ODA-DXF logical anchors, per-version string encoding rule).
description_zh: 同句柄两个 DWG 对象帧的位流级差异取证：bitdiff.py 验对象头自洽 → 无贪心种子-扩展普查（判插入/删除 vs 重排）→ 递归对齐出对齐树与 gap 清单 → 用 ODA 转 DXF 的逻辑真值锚定字段身份；并附带实测的字符串编码版本规律（R2007 前 TV/GBK 用 BS(n+1)、R2007+ TU/UTF-16LE 用 BS(n+2)）。用于 firstdiff/xcmp 已报「哪帧差、差几字节」后要定谱（同时钉死字段身份/宽度/位置），或复核「重排」类结论时。做**整类宽度账**时尺要用同内容**原生**件，并要求「我方−原生」逐枚恒定（或 `|我方−oracle| > |原生−oracle|`）—— ODA 降版会凭空补一枚硬属主/XDIC 引用，拿「我方 vs oracle」当账会带合成噪声，曾把真缺陷误判成「勿落刀」。
argument-hint: Name the handle and cell to diff, e.g. "位流差异取证 531 1EBAE"
argument-hint-en: Name the handle and cell to diff, e.g. "bitstream diff 531 1EBAE"
argument-hint-zh: 指定句柄与格，如「位流差异取证 531 1EBAE」
user-invocable: true
---

# DWG 位流差异取证（bitstream diff forensics）

目标：把**两个对象帧的位流**差异写成可读的**对齐树 + gap 清单**，一次钉死字段的**身份 / 宽度 / 位置**。
适用：`ProbeTool firstdiff` / `xcmp` 已给出「哪帧差、差几字节」，下一步要**定谱**时。
前置：遵守 `AGENTS.md` 硬约束；代码定位用 `dwg-parser-map`；格级流水线（oracle / firstdiff / gate）用 `dwg-direction-a-cell-forensics`。

## 工具

**`tools/oda-compare/bitdiff.py`**（已入库，纯标准库、无需 build）。

```bash
# 取帧：SsInv --framedump 输出重定向到 txt
tools/SsInv/bin/Debug/net8.0/SsInv.exe --framedump <dwg> <h1,h2,...> > frames.txt

python tools/oda-compare/bitdiff.py ours.txt ref.txt --handle <HEX> --hdr          # 只验对象头
python tools/oda-compare/bitdiff.py ours.txt ref.txt --handle <HEX> --seeds --K 64 # 长公共块普查
python tools/oda-compare/bitdiff.py ours.txt ref.txt --handle <HEX> --align --K 32 --minL 48
```

两侧取**同句柄**的帧（`--framedump` 支持一次传多个句柄，两侧各取一枚）。

## Step 0 先验对象头自洽（别跳过）

`--hdr` 打印 `MS / RL / data=[a,b) / hs=N bits / crc@ / 自洽`。判据式：

```
66 + RL + 句柄流位数 + 16 == 帧长
```

**R2004 与 R2007 布局同构**：`[MS 16][BS 18][RL 32][数据 RL 位][句柄流][CRC 16]`。
（`RS` = 首字节为**低字节**（小端）；`MS` 16 位、最高位置 1 时扩到 32 位。）

自洽不成立 ⇒ 帧不完整（跨页/被截），**先解决这个再谈字段**。
句柄流长度 = `帧长 − 16 − 66 − RL`。实测 `531` h=`1EBAE`：ours 数据 15981 / 句柄流 537、ref 数据 19286 / 句柄流 536 ⇒ **差异全在数据区，句柄流几乎相同**。

> `unknown_bits` **不消费游标**：`decode.c:5924 dwg_decode_unknown_bits` 读位后 `bit_set_position(dat, pos)` 复位 ⇒ 数据区确从 **66** 起。别以为 `HANDLE_UNKNOWN_BITS` 会挪起点。

## Step 1 种子-扩展普查：判「插入/删除」还是「重排」

```bash
python tools/oda-compare/bitdiff.py ours.txt ref.txt --handle <HEX> --seeds --K 64
```

**必须用无贪心法**：贪心最长匹配会把「重复内容块」配到**另一处**，产出**非单调映射**与 `dup=3~44` 的假匹配，从而把「前后移位式插入/删除」**误判成「重排」**。
本工具用 K 位种子做**精确哈希配对**再双侧扩展（K=64 时假阳性期望 ≈ `n·m/2^64 ≈ 2e-11`，可视为零）。

判读：

- 输出 `单调性: inversions=N`。**`N=0` ⇒ 前后移位式插入/删除**（最常见）；
- `N>0` 时**先怀疑工具假匹配**（重复内容块被两个 ours 位置认领），用 `--align` 复核；
  去重必须**两侧都判区间重叠**（只判 ours 侧会留下「同一 ref 位置」的重复块 ⇒ 假逆序）。
- 看 top 块的 `ours−ref` 列：**同号且随 ours 递增 ⇒ 单调插入/删除**。
  实测 `531` h=`1EBAE`：`+8 → +264 → +360 → +608 → +850 → +1034 → +1130 → −3305`
  ⇒ 前段 ours 多、尾部 ref 独占。

## Step 2 递归对齐：把差异分解成 gap 清单

```bash
python tools/oda-compare/bitdiff.py ours.txt ref.txt --handle <HEX> --align --K 32 --minL 48
```

对每个 gap 递归求最长公共块，直到 `< minL` 或找不到。输出每行：

```
ours[i0..i1]=La  ref[j0..j1]=Lb  d=i0-j0→i1-j1  [ONLY-OURS +n | ONLY-REF +n]
```

- **`ONLY-OURS +n`** = 该段**仅 ours 有**（目标版本**废弃**的字段）；
  **`ONLY-REF +n`** = 该段**仅 ref 有**（目标版本**新增**的字段）。
- **`d` 是 gap 起点的偏移差，gap 内部两侧长度不同 ⇒ d 会变**：故同时打 `d_end`。
  **别拿 d 当单调性指标**；单调性看 Step 1 的 blk 映射。
- 末行 `Σ(ours−ref) over gaps` 应等于两侧数据区长度差（实测 `-3305` ✓）——**这是自洽校验**，
  对不上说明 `--minL` 太大、还有未分解的 gap。
- **小 gap 的固定长度差是强信号**：实测 `531` 有 **14 处 `ONLY-OURS +2`**（2 位段）
  与 **6 处 `ours 286 vs ref 20`**（差 266 位）；后者在 6 个单元格上**内容逐位相同**
  ⇒ 是「同一字段的两版形态」，不是 6 个独立差异。

## Step 3 用 ODA 转 DXF 取逻辑真值当锚点

位流给出**位置**，DXF 给出**语义**。两者一配就能定字段身份：

```bash
mkdir -p out/_dxf/in out/_dxf/out
cp "<源图>.dwg" out/_dxf/in/
# ODA 只用仓内 tools/ODAFileConverter/（仓外候选已删）；用 find_oda 现取绝对路径
ODA="$(python tools/oda_path.py)"; "$ODA" "<in 的 Windows 绝对路径>" "<out 的 Windows 绝对路径>" ACAD2018 DXF 0 1 "*.dwg"
```

**关键前提：DXF 的 group code 序列 = 位流字段序列**（已用 `ContentFormat` / `CellStyle` 段逐条核对）。
用法：

1. 从 DXF 取**逻辑值**（如单元格文本、列数、样式字段值）；
2. 把该值在**两侧位流里搜**（构造位串模板 + 全偏移滑窗），命中即得到该字段的**绝对位**；
3. 同一字段两侧命中位之差 = 该字段所在区段的**累计位移**，可直接与 Step 2 的 `d` 序列对上。

> **⚠️ ODA 会重建对象**：实测读 `add_2004.dwg` / `add_2007.dwg` 时**都重建**了 `TABLECONTENT`
> （源 `1EBAE/1EA93/1EA96` → 目标 `1F07E/1F082/1F086` 与 `1F0C2/1F0C6/1F0CA`），
> 但**逻辑值一致**。⇒ DXF 可作**字段清单与逻辑值**的权威路标，**不可**作句柄 / 位级布局的依据。

## Step 4 字符串编码的版本规律（定谱时直接套）

实测（`531` h=`1EBAE`，5 个文本双侧全中）：

```
字符串 = BS(数据字节数 + 终止符字节数) + 数据字节
```

| 版本 | 编码 | 终止符 | 长度字段 |
|---|---|---|---|
| pre-R2007（AC1018 等） | **TV：GBK 字节** | `\0` 1 字节 | `BS(n+1)` |
| R2007+（AC1021 等） | **TU：UTF-16LE 字节** | `\0` 2 字节 | `BS(n+2)` |

实测样本：`版本` 5/6、`{…修订时间}` 32/52、`修订内容` 9/10、`2026-8-6` 9/18、`{…四次修订}` 32/52（左 ours / 右 ref）。

推论（省一轮试错）：

- 同一文本两侧位长差 = `(UTF16字节数 − GBK字节数) × 8` ⇒ **纯 ASCII 文本在 UTF-16 下翻倍**，
  中文持平（GBK 2B/字 = UTF-16 2B/字）；
- **搜串必须三种拼法各搜一遍**（ASCII / UTF-16LE / UTF-16BE）且**位级滑窗**（串常非字节对齐）；
  位流里**先出现的是低字节** ⇒ LE 取 `u >> 8`、BE 取 `u & 0xFF`；
- 读长度字段时**先按「长度 = 数据字节数」扫，再试 ±1 / ±2**（终止符字节数随版本变）。

配套的位流字段差异（同一族，可作旁证）：`TABLE_value_fields` 的 `format_flags`(93)
**只在 R2007+ 存在**，且 `PRE(R_2007a) { data_type &= ~0x200; }`
（`docs-ref/libredwg-master/src/dwg_spec_shared.h:592`）⇒ 薄形的 value **少一个 BL 字段**。

## Step 4a 三种假说快查 + 集中串流判据（最省试错的一步）

（2026-09-25 自 `dwg-direction-a-cell-forensics` Step 4b 归并而来——帧级技法**只在本档维护**。）

`firstdiff`/`xcmp` 只说「哪一帧差、差几字节」；**下一步不是猜字段，而是把差异写成一条精确的位等式**。
把两条位串丢进 Python 试下列假说，**任一成立即定谱**：

| 假说 | 判据式 | 含义 |
|---|---|---|
| 删段（我方多写） | `oracle == ours[:a] + ours[b:]` | 我方多发了 `[a,b)` 这段（典型 = 目标版本已废弃的字段，如 R2018 的 `lt_index BS`） |
| 插段（我方少写） | `oracle == ours[:a] + ours[a:b] + ours[a:]`（且 `len` 差 = `b-a`） | 我方漏发了目标版本新增的位（典型 = `has_ds_data`、`is_xdic_missing`） |
| 尾部追加 | `oracle == ours + tail` | 句柄流尾部少了引用（典型 = 句柄化的向量/映射） |
| 集中串流（串被搬走） | `串流内容尾 + 17 == RL` | 目标把串从 inline 区剥离、集中排到数据区末尾 + 17 位 footer（`RS data_size` + `B has_strings=1`） |

搜索 `a`/`b` 时**先按「字段宽度的整数倍」扫**（BS 18/10/4、BL 32/16/8、句柄 `8+size*8`），命中率极高且能反证字段身份。
**定谱价值**：等式一旦成立，字段身份、宽度、位置三件事同时被钉死 ⇒ 落码一次到位、不必反复试错。

> **集中串流判据：串流内容尾 + 17 == RL**（§196 续三实测；**Opaque 对象首选**）：R2007+ 目标把字符串
> **剥离出几何区**、集中排在数据区末尾，尾随 17 位 footer（`RS data_size` + `B has_strings=1`）。
> 判据式 **`串流内容尾 + 17 == RL = 句柄流起点`** 一旦成立，同时钉死三件事：串被集中化（**不是内联**）、
> footer 确实存在、句柄流起点 = RL。
> - 串流起点 = **首个 UTF-16LE 串起点 − 8**：`[BS 码元数]` 走 modular 短形式（值 17 ⇒ 只占 **1 字节**），
>   别按 18 位/34 位去找前缀。
> - 多串时内容 = Σ `([BS 码元数][UTF-16LE 码元])`，**内容尾取最后一枚串的尾**，不是第一枚。
> - 实证（type `518` `AcDbDimAssoc`，`add_2004→AC1021` 富图）：198EF 串流起点 157、内容尾 437、
>   `437 + 17 = 454 = RL` ✓；198F5 串流起点 286、内容尾 848、`848 + 17 = 865 = RL` ✓。
> - **别把「串落在帧中段」当成内联 TU**：串流排在数据区末尾、句柄流之前，所以串的绝对位**常在帧中部**。
>   判内联还是集中**只看这条等式，不看串的位置**。误判代价 = 按内联 TU 自写一条路径，而仓库里既有的
>   `CarryWithStringsR2007`（几何区搬运 + 串剥离集中写 TU + 17 位 footer）正是这个形态，直接复用即可。

## Step 4a-补 「窗尾多 N 位」= **整段集中串区**，不是尾位（§526 一手实测）

看到「产物比目标真本长 N 位、N 很小」**别直接推断成 `B has_strings` 那一枚位**。R2007+ 源每个对象的
数据区尾是一整段集中串区，形如 `[BS 码元数][TU…][RS data_size][B has_strings]`；**空串区实测恰 19 位**
（`[BS 0]` 2 + `RS` 16 + `B` 1），§123 类「剥尾位 1 枚」的窄刀拿它是拿不掉的（拿掉了也还剩 18 位）。

**三步定谱（全部零 ODA、一次到位）**：

1. **先按自家锚切窗，再逐位比**：`objfields` 取 `CarryStartBit / GeoStartBit / StringStreamStartBit /
   HandleStreamStartBit`，把「几何窗」= `[GeoStart, HSStart)` 单独切出来比。整帧直接对齐会被句柄流
   噪声干扰（`bitdiff --align` 会把 gap 报在句柄流上，看着像「尾段多 42 位」），**按锚切窗后形态会
   干净到一句话**：`ours = native + 尾段 N 位`（前缀逐位全等 ⇒ 只有尾部多余 ⇒ 必是「搬窗过宽」而非字段错）。
2. **拿 `SSStartBit` 对号**：`HSStart − SSStart == N` ⇒ 那 N 位就是集中串区，修法是**收窗**（串由重内联
   那一刀负责，窗里不许再带），不是剥位。§526 实测：`lineanddim` h=344 四档 R2007+ 源同形 `geo=736`
   vs 原生 AC1018 的 `716`，差 19 = `43955 − 43936`，一秒定谱。
3. **钉成绝对锚前先证「同图原生真本确系同内容」**：`同内容多版本对` 不是公理 —— 实测 `textandrect`
   的 2004 本与 R2007+ 本维度正文/点数不同（原生帧 1794 位 vs 我方 1120 位、我方 97.9% 被原生包含
   ⇒ 单 gap、纯「原生多一段」= 语料差）。**判法**：把差值按类型聚成多重集，常量差（全体同一枚数）
   先怀疑结构/语料差，逐枚变差才是写侧差；窗口宽的对表尺仍是**现造 ODA oracle**，不是原生档。

## Step 4b R2000/R2004 位流原语速查（手写解码器 / 假设搜索必读）

位级走查想「手工逐位推」必错（错一位全盘歪）。直接按本仓 `BitReader` 的**实测口径**写脚本
（2026-09-24 →AC1021 旁证手钉死，oracle/原生帧对拍逐条验证）：

| 原语 | 实测口径 |
|---|---|
| RS / RL / MS | **字节级小端**（首字节=低位、字节内 MSB-first）；RL = 4 字节 LE |
| `ReadBD` | code **0→RD(66 位)**、1→1.0、2→0.0（与 libredwg 记忆版 0→0.0 **相反**，TCCOL 列宽对拍钉死） |
| BS / BL | code 0→RS/RL、1→RC、2→0、**3→256（合法值，勿当错误抛）** |
| 句柄引用 | 单字节 `[高4位 code][低4位 counter]` + counter 字节（code 0 也带 counter，勿特判） |
| EED | `[BS size][handle][size 原始字节]` 循环至 size==0（`TrySkipEed` 口径；勿按 item/code 展开） |
| CMC | `[BS idx][BL rgb][RC flag]`，flag<4 时 `&1`→TV 名、`&2`→TV 簿名（TV 走集中流=0 数据位） |
| 帧几何 | `objStart=BodyStart×8`（类型码起点）·`carryStart=objStart+18`（>255 类型码的 RL 起点）·`dataEnd=objStart+RL`（R2000/R2004 两家族同式）·HS=`[dataEnd, BodyEnd×8)` |
| 类型码位宽 | ≤255 走 `01`+RC=10 位；>255 走 `00`+RS=18 位（RL 从**类型码起点**量 ⇒ dataEnd 含这 18 位） |

**假设搜索判据纪律**：候选语法（「该字段是 BL 还是 BS」「payload 有无」「链接体是否带句柄」）用脚本
**穷举**，以「**全帧 dataEnd 精确闭合（delta=0）**」为唯一判据。⚠ **零位会吸收位移**（连续 0 位字段怎么读
都通）⇒ 局部窗口内多解等价，必须「整帧闭合 + oracle 同句柄帧逐字段对拍」**双证**（实测：90EA 行1首格
「ct=5 合并格」假解与真解 `[lk=3][链接体3BL][ncc=1][ct=1]` 在 16 位窗口内位级等价，靠整帧闭合 + "REF"
文本落位才分得出；局部判据会把 79 槽的薄形 HS 当成可逐槽映射）。

## Step 5 收尾

1. 结论落 `docs/direction-a-ledger.md` **新 §**（勿改既有小节），并在 `docs/direction-a-backlog.md`
   对应行补「判定 + 订正」；`src/` 零改动时在 `progress-and-debts` 写明「未跑门」的理由。
2. **推翻既有 § 结论时，在新 § 里显式写「§xxx 已被本 § 推翻」**，并说清旧结论错在哪个算法
   （实测：§207「重排」错在贪心块分解，§208 用无贪心法推翻）。
3. 新驱动脚本**落 `tools/`**，别留 `out/`（`out/` 在 `.gitignore` ⇒ 工作结束即丢，本仓已丢过多个）。

## 陷阱速查

- **贪心块分解会把「插入/删除」误报成「重排」**（§207→§208 实测）。判「重排」前必须用无贪心法复核。
- **逐类/逐枚「我方 vs oracle」的宽度账自带合成噪声 —— 尺要用同内容**原生**件，判据要扣掉两真本互差**（§553 实测）：
  ODA 降版会给对象**凭空补**一枚硬属主/XDIC 引用（本文 Step 5 判 5 的「三件套」），于是同一枚帧
  `oracle` 的句柄流 +24 位、数据区 −24 位，`native` 与 `oracle` 自己就差 8 位。富图 `AC1021→AC1018` 的
  LAYER 因此被误判过「勿落刀」：`ours−oracle=+24` 里有 +8 是真本之间的差，
  **真信号 = `ours−native = +16` 且整类恒定**。正确筛法：`|ours−oracle| > |native−oracle|`
  （或直接看 `ours−native` 是否**逐枚恒定** —— 恒定 ⇒ 结构性字段差，不恒定 ⇒ 文本量等**内容噪声**，
  像 ATTRIB/MTEXT/MULTILEADER 那种大变值差都不是病灶）。oracle 只在「原生件里没有这一类」时当代用品。
- **去重只判一侧区间 ⇒ 假逆序**：同一 ref 位置被两个 ours 位置认领。两侧都要判。
- **别拿 gap 的 `d` 当单调性指标**：d 是 gap 起点偏移差，gap 内部会变。看 `d_end` 或 Step 1 的 blk。
- **`Σ(ours−ref) over gaps` 必须等于数据区长度差**，否则 `--minL` 太大、还有未分解的 gap。
- **`RS`/`MS` 是小端**（首字节低字节）；按大端读会得到荒谬的 `MS`（实测一度得 `39849737`）。
- **位串工具位序必须先对齐**（§174 实测）：`DvWalk tblraw` 旧用 `(by >> bitOff) & 1`（**LSB 优先**）打印，
  而 `BitReader` 是**字节内 MSB 优先**（`_bitPos 0..7`，bit0 = 最高位）⇒ 打印出的位串是真实位序的**镜像**，
  据它做的偏移分析**全错**。**用任何逐位打印前先自证位序**（如 `BD 码 2` 应打印 `10`）。已修：改用 `(by >> (7 - bitOff)) & 1`。
- **位级搜串的 UTF-16 字节序极易取反**（§196 续二实测，与上位序坑同源但方向不同）：位流里**先出现的是低字节**
  ⇒ UTF-16LE 取 `chr = u >> 8`、UTF-16BE 才取 `u & 0xFF`（`u` = 16 位按 MSB-first 拼出的整数）。
  写反 ⇒ 「真值里明明有串却搜不到」⇒ 把**已成立**的结论误判成巧合、推翻重来白绕（§196 首轮就这样把 `17N` 当巧合）。
  两条配套纪律：① 串常**非字节对齐**（实测 BL 前缀在 bit 116、串在 bit 124），字节视图永远看不到，**必须位级滑窗**；
  ② ASCII/UTF-16LE/UTF-16BE 三种拼法**各搜一遍**，只搜一种必漏。搜串一律用「**构造目标串的位串模板再全偏移滑窗**」
  （`''.join(f'{ord(c):08b}' for c in NAME)`，LE 每字符后补 `00000000`、BE 前补），
  **不要**用「滑窗取值再判可打印」的启发式（同样受字节序取反影响，且吐 `1'9708(747:)23` 这类伪匹配）。
- **位级对拍的隐藏前置：先证「待比对的产物确由本手生成器产出」**（§147 的教训、§174 否证）：
  产物帧长 == 源帧长，或 `DebugTrace` 里**没有**生成器 trace 行，或回环自读值未变 ⇒ **手术未触发**，
  此时**先修闸、别比字段**（§147 曾把「verbatim 塞入的源帧」误当「生成器字段布局错」，整手结论作废、白走一手）。
- **句柄流不是差异所在**：先算 `帧长 − 16 − 66 − RL`，两侧句柄流几乎等长时，把注意力放数据区。
  （**注意**：这是**逐帧**观察，不是全局结论——「句柄流含相对引用故须归一」的假设已被 §525 实测否证，
  归一化对 ODA 判词零效果；见 `docs/issues/2026-09-25-relref-…-525.md`。）
- **DXF 的句柄不可用**：ODA 重建对象、重编号；只用它的**字段清单与逻辑值**。
- **`--minL` 与 `--K` 要配对调**：`K=32/minL=48` 能挖出 2 位级 gap；`K=64` 只看长块普查（快）。
- 输出混中文时用 `| tail -N` 截尾（终端 GBK 会花屏，但行序稳定）。
- **手工位算必错**（错一位全盘歪）：字段级走查全脚本化（20 行 BitReader 探针，§178 法），
  判假设用「全帧 dataEnd 精确闭合」，**别用局部闭合**（零位吸收 ⇒ 多解，见 Step 4b）。
- **stderr 落文件后 grep 须 `-a`**：dotnet 输出混控制字符/GBK ⇒ grep 判「二进制文件」⇒ **静默空结果**
  （2026-09-24 实测空转多轮；`grep -c` 都不报数）。
- **`dotnet <apphost>.exe` 报 deps.json 冲突**（"already been found but with a different file extension"）
  ⇒ 跑 `dotnet <同目录>.dll`。
- **sln 增量构建可能不拾取 Core 源编辑**（改了没生效）⇒ `touch` 该文件后重建；`ProbeTool` 不在 sln，
  改 Core 后须单独 `dotnet build tools/ProbeTool -t:Rebuild`（AGENTS §9），否则探针/新命令跑的是旧码。
