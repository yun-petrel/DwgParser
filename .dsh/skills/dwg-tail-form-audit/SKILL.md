---
name: dwg-tail-form-audit
name_en: DWG Tail Handle-Stream Form Audit
name_zh: DWG尾句柄流形制三侧定谱
description: Three-side "same-form" audit pipeline for DWG object tail handle streams (frame-tail region; AC1015/R2000 origin plus a dedicated R2004/AC1018 lane that tools/r20tail.py cannot reach). R2kCmp ledger anchors plus an anchored code-nibble decode, a window/phase sentinel (bodyEnd excludes the 2-byte object CRC; carryStart+carryLen == bodyEnd*8), the calibrated R2004 read-side handle-stream anchor (registered anchor == CarryStartBit + RL - 10, verified on 4924/4924 native objects; ProbeTool tshs/tshscan/tshdata instead of r20tail.py, semantic fitness = every ref resolves to a real handle and closes at BodyEnd*8), per-class (3,0) presence-rate census as the cheap scope ruler, and relative (code|8) handle folding resolved through the ledger to name the referenced class; comparison splits by scenario (form SET only across renumbered products, per-handle join against the same-drawing same-version native truth), red-first offline criteria, strip/double-insert negative controls in an isolated worktree. Use when deciding how a version's tail stream should be parsed, whether a class must carry a tail ref (xdic / hardowner / counterpart), whether a morehandles block exists, localizing "Object improperly read" to one tail ref, or checking whether tools/r20tail.py can reach the object at hand.
description_en: DWG tail handle-stream form audit — R2kCmp ledger anchors, anchored code-nibble decode with window/phase sentinels, per-class (3,0) presence-rate census, relative-handle folding, scenario-split comparison口径, red-first locked offline criteria, strip/double-insert negative controls in an isolated worktree.
description_zh: DWG 对象尾句柄流（帧尾）「三侧同形」定谱流水线：R2kCmp 台账锚 → 锚定 code-nibble 解尾（含窗口/相位哨：bodyEnd 不含 2 字节对象 CRC、carryStart+carryLen == bodyEnd*8）→ 逐类 `(3,0)` 在位率普查当射程尺 → 相对引用（code|8）链式折算后回台账点名被引用族 → 对照口径**分场景**（跨产出只比逐类形制集合、同图同档原生真本逐句柄 join）→ 红-先锁进离线判据档 → 剥/双插两枚负控在隔离树跑完还原。R2004/AC1018 一侧另有一条专用射程（`r20tail.py` 硬拦 AC1015 魔数进不去）：定谱锚 = 读侧登记锚 ≡ `CarryStartBit + RL − 10`（原生 4924/4924 枚逐枚成立），取数口 = `ProbeTool tshs / tshs TALLY / tshscan / tshdata`，且 `hs := 帧长−16−66−RL` 这类**恒等式余量不得当尾流读数**。用于判定某档（实测原点 AC1015/R2000，另含 AC1018/AC1021）尾句柄流怎么解、某类尾段该不该含某枚引用（xdic/硬主/counterpart）、morehandles 尾块是否存在、把 `Object improperly read` 定位到某一枚尾引用、以及判断 `tools/r20tail.py` 这类尺子当前这枚到底能不能用。
argument-hint: Name the version cell and class, e.g. "尾段定谱 AC1015 512 三侧对照"
argument-hint-en: Name the version cell and class, e.g. "tail form audit AC1015 512"
argument-hint-zh: 指定档与类，如「尾段定谱 AC1015 512 三侧对照」
user-invocable: true
---

# DWG 尾句柄流 / 帧尾形制「三侧同形」定谱（tail form audit）

目标：把「对象帧尾的尾句柄流该怎么解、含哪几枚引用」从假设打成**定谱**，并按**分场景口径**在
**真本 ↔ ODA oracle ↔ 我方产物**三侧锁成离线判据。
实测原点 = AC1015/R2000（§381 续甲六～九轮）；换档沿用同一流程，只换 Step 1 语料与 Step 3 定谱表。

**边界（先选对工具）**：

- 数据区/字段级两帧位流差异（判插入/删除 vs 重排）→ `dwg-bitstream-diff-forensics`（那枚明写「句柄流不是差异所在」，与本技能互补不重叠）。
- 格级「产物 vs oracle」ODA 拒收诊断、firstdiff 逐帧枚举 → `dwg-direction-a-cell-forensics`。
- 语义修复把既有 golden 打红后的重钉 → `dwg-golden-rebless`。
- 本技能只管**尾句柄流自身**：语法、起点锚、终点残位、引用枚数、三侧形制锁。

## Step 0 环境预检（每轮先跑，勿按文档断言）

```bash
command -v dotnet || ls -d ~/.dotnet 2>/dev/null
for p in "D:/ODAFileConverter/ODAFileConverter.exe" "D:/ODAExtract/ODAFileConverter.exe" "D:/Program Files/ODA/ODAFileConverter.exe"; do [ -e "$p" ] && echo "FOUND $p"; done
export ODA_EXE="<实测存在路径>"
```

- 本机（Windows Git Bash）**只有 `python`，没有 `python3`**——旧文档/旧脚本里的 `python3` 现场换掉。
- 任何**驱动 ODA 或打印 `⇒`/中文的 python**都要带 `PYTHONIOENCODING=utf-8`：崩在写盘之前 = 整轮取数白跑（实案 = chainwalk 被 GBK 控制台打死，读数是「没有读数」不是「读数红」）。
- `ODA_EXE` 探不到 ⇒ oracle 侧（Step 1 第二路）不可跑，三侧退成两侧（真本 + 我方），
  汇报里写明「探不到 ODA」而非「门跑红」。

## Step 1 备三侧语料（全部落 `out/<case>/`，新建字面量目录，勿 `rm -rf "$VAR"`）

**① 真本（被删的 `新增图纸/*_2000.dwg` 从历史取回）**——先找删档提交，再从**其父**取 blob：

```bash
git log --oneline --diff-filter=D -1 -- "新增图纸/add_2000.dwg"      # 记 <删档提交>
mkdir -p out/csm15/hist
git show <删档提交>^:"新增图纸/add_2000.dwg" > out/csm15/hist/add_2000.dwg
```

**② ODA 自产 oracle**（高版本真本直转 AC1015，与我方代码无关；版本名 AC1015 = `ACAD2000`）：

```bash
mkdir -p out/<case>/oracle_in out/<case>/oracle_out
cp "最简各版本图纸/2018.dwg" out/<case>/oracle_in/x.dwg
( cd "$(dirname "$ODA_EXE")" && "$ODA_EXE" "<oracle_in 的 Windows 绝对路径>" "<oracle_out 的 Windows 绝对路径>" ACAD2000 DWG 0 1 )
```

- **语料缺席兜底**：`最简各版本图纸/*.dwg` 与 `新增图纸/*.dwg` 都可能已被并行车道在工作副本删掉
  （`git status` 显示 ` D`）——那不是本手缺陷，也不要擅自还原（归因判据见 skill
  `dwg-golden-rebless`）。要跑本流水线就直接从 HEAD 取回：
  `git show HEAD:"最简各版本图纸/2018.dwg" > out/<case>/oracle_in/x.dwg`。

**③ 我方产物**（改过 Core 必须先全 sln 新鲜构建，旧 dll = 假读数）：

```bash
python tools/winbuild.py dotnet build DwgParser.sln -t:Rebuild
src/DwgParser.Cli/bin/Debug/net8.0/DwgParser.Cli.exe "最简各版本图纸/2018.dwg" --convert AC1015 --output out/<case>/ours.dwg
```

## Step 2 R2kCmp 台账（位区间与锚的唯一取数口）

工程**不在 sln** ⇒ 改过 Core 后必须逐个 `-t:Rebuild`（经 winbuild 包装）：

```bash
python tools/winbuild.py dotnet build tools/R2kCmp -t:Rebuild
python tools/winbuild.py dotnet run --project tools/R2kCmp -- <文件.dwg>   # 或直接跑 bin 下 exe，逐对象一行、制表符分隔
```

17 列表头（0-based 下标）：`file0 idx1 hdl2 type3 logical4 cls_name5 kind6 bodyStart7 bodyEnd8 sizeBytes9 frameEnd10 typeEndBit11 carryStart12 carryLen13 hsStart14 hasHs15 [capAnchor16]`

- **列义陷阱（实撞过）**：`bodyStart/bodyEnd` 对 R2000 = **物理文件字节偏移**（无节压缩）；
  而 `carryStart/hsStart/capAnchor` **已经是位位置**——再 ×8 会全表落空（首轮 0 命中的根因）。
- 第 14+15 列 = 读侧锚 `HandleStreamStartBit`/`HasHandleStream`（`hsStart=0` = 读侧无锚）；
  第 17 列 = `CaptureAnchor.TryGet` 只读锚（覆盖 0x2A DICTIONARY / 0x4F XRECORD / 0x31 BLOCK_HEADER /
  VISUALSTYLE / DIMASSOC / SORTENTSTABLE 等读侧未建模类）。

**三条恒等式 = 免费自证「列没数错、CRC 没读进来」**（`add_2000.dwg` 现场重跑，1489 枚全枚成立）：

| 恒等式 | 成立枚数 | 不成立时先怀疑 |
|---|---|---|
| `frameEnd − bodyEnd == 2` | 1489/1489 | 那 2 字节 = **对象 CRC，落在 `bodyEnd` 之后**；解尾**永不**越 `bodyEnd` |
| `carryStart + carryLen == bodyEnd*8` | 1489/1489 | 列下标数错 / 拿 `frameEnd` 当终点（§550 那条假影的来源） |
| `typeEndBit == carryStart` | 1489/1489 | 公共头与句柄流的边界理解错 |

- **AC1015 上 `cls_name`（第 6 列）基本是空的**（1489 枚只 132 枚非空）⇒ 类身份用第 4 列 `type`
  （它与 `logical` 在 1420/1489 枚上相等，差的 69 枚是 `TableControl`），别拿 `cls_name` 当索引。
- `hsStart=0`（读侧无锚）集中在 `Opaque`（315/421）与 `TableControl`（56/69）；`Entity` 999 枚**全有锚**
  ⇒ 「无锚」通常是**这一类**没上公共簿记（§197 契约），不是台账坏了。

## Step 3 锚定 code-nibble 解尾——**先判断 `tools/r20tail.py` 这枚能不能用**

```bash
python tools/r20tail.py --anch 512 <dwg>                  # 逐枚「唯一读法」（锚定），一次一类
python tools/r20tail.py --cls 512 <dwg>                   # 单类普查（四类语法 × 起点穷举 + 计数前缀）
python tools/r20tail.py --rows <dwg>                      # 同上 + 逐枚解集行（**截断** pinrows[:14] / detail[:80]）
python tools/r20tail.py --frames <dwg>                    # 自检：残位分布 + ≥8 位计数，出 ★ 即有未定谱尾块
python tools/r20tail.py --audit <真本.dwg> <我方.dwg>      # 两侧逐类形制差（报差异类数）
python tools/r20tail.py --back <dwg>                      # 帧尾回溯仅估形（默认关，不可自证，勿入判据）
```

**实测射程（本仓 `tools/r20tail.py` 现文件，读码 + 现场跑证；别照旧文档断言它能用）**：

1. **硬拦 AC1015 魔数**（`main()` 与 `anchored_dump()` 各一次）：非 `AC1015` 开头的文件只打一行
   `!! 非 AC1015` 就 `continue` ⇒ **R2004+ 那一侧整侧出射程**（其对象节压缩，物理字节切片必不闭合）。
   比 R2004+ 一侧用 `DvWalk hdl` / `ProbeTool`，或按 Step 3b 自建解码。
2. **默认档（不带 `--rows`/`--anch`）只给逐类聚合**：语法胜出率 + code 直方图 + `c3` 判定 + 计数前缀偏移直方图，
   **没有枚级行**。逐枚行要么 `--rows`（截断 14/80 枚，且它是**穷举解集**=「若干条说得通的解」，不是唯一读法），
   要么 `--anch <类>`（锚定的唯一读法，但一次只一类、且同样只吃 AC1015）。
3. ★ **docstring 里的 `--rlcheck` 准入闸在 argparse 里根本不存在** ⇒ 跑了直接 `error: unrecognized arguments`、
   **rc=2 早退**（实测）。它描述的「头部 RL 正算出的 `hs` 与台账 `hsStart` 逐枚对表」这件事**确实存在**，
   但是在 `anchored()` 内部当**第三级锚**（`src='RL派生'`）用的，不是一道独立闸门 ⇒ 要那道闸就得按 Step 3b
   自己对表（三行 python，别把不存在的旗标写进结论）。文档与实现漂移 = 记账即可，改尺子是另一手。
4. `--anch` 打的 `refs=… c:v->h` 里的**绝对值 `->h` 从第二枚相对引用起是错的**（见 Step 3c）。

**四条已定谱规则（AC1015 实测；他档套用前先各测一遍）**：

1. **语法 = code-nibble**：引用 = `ctrl=(code<<4)|size` + size 字节大端值；`size ≤ 8`、`code ≤ 14`。
   libredwg `bit_read_H` 的 size-only 支只属 `<R_13b1`，别拿去解 R2000。
2. **起点 = 读侧锚 → CaptureAnchor 只读锚 →（仅 `--back`）帧尾回溯**。AC1015 对象头**无 RL**
   （`MS size, BS type, RL bitsize` 只属 R2004+），正向派生不出锚——实测无锚处读到 `0x5A000000`
   一类数据即此故。帧尾回溯回收率低（约 40%）且「残差 <8 位」是**同义反复**，只作估形。
3. **终点 = `bodyEnd*8`（= `carryStart+carryLen`）− 0..7 位字节填充**：对象流按位铺放，帧界可落字节中间；
   「恰收口于帧界」当判据过严会把填充误读成漏读（曾据此错报「尺子只有 11.8% 射程」，实为有锚者 100% 可解）。
   `add_2000.dwg` 实测：1118 枚有锚者**全部**收口在 0..7 位（残位分布 `{0:176, 1:27, 2:133, 3:16, 4:137, 5:547, 6:73, 7:9}`，
   ≥8 位 = 0 枚）。
4. **R2000 对象帧尾没有 morehandles 尾块**：三档语料（真本 ×2 + 我方产物）`≥8 位残差 = 0 例`。
   `--frames` 一旦报 ≥8 位非零 ⇒ 要么新未定谱结构、要么解错，先复现再下结论。

## Step 3b 窗口与相位哨（把「差一枚」和「解错了」分开）

**窗口的两条等价取法**（先跑 Step 2 那三条恒等式确认等价，再任选）：

- `[hsStart, bodyEnd*8)` —— `r20tail.anchored()` 就是这一条；
- `[hsStart, carryStart+carryLen)` —— §554/§555 手工自建解码用的这一条。

**三条硬规矩**：

1. **终点用 `bodyEnd`，不用 `frameEnd`**：`frameEnd − bodyEnd == 2` = 对象 CRC 在 `bodyEnd` 之后。
   反面实案 = §550 §七·补 把 objfields 的 hex 对照读成「少发一段 `C3 02`、末字节 `FF` vs `FE`」，
   §554 已**订正**：那是**半字节相位错**（对照窗从 nibble 中间起算 **且** 把 CRC 末字节读了进来），
   真实差 = **恰少一枚 `0x30`**、两侧 pad 同为 4 位、根本没有 `FF/FE` 之差。
   ⇒ 报「少发/多发了某段字节」之前，先证明窗口起点落在字节边界、终点没越 `bodyEnd`。
2. **两侧 pad 必须同值才可比**：pad = `bodyEnd*8 − 解完最后一枚的位游标`（0..7）。一差 3 一差 4，
   后面的「差一枚」就是假的。§554 §八：富图 `h=11` 那枚「两侧 pad 同为 4 位」是它敢定病的**前置条件**。
3. **位账闭合哨（不过就不许报字段身份）**：`Δ(引用宽度) ∓ Δ(pad) == Δ(尾段总位数)`。
   范例 = §555 §二：我方 tail 125 pad5、原生 tail 136 pad0 ⇒ 两枚各 8→16 位（+16）、pad 5→0（−5）、
   净 +11 == 136 − 125 ✓ ⇒ 判定「不是相位错、不是漏枚，就是中段那两枚槽」。

## Step 3c 相对引用折算 + 回台账点名被引用族（别猜字段）

`code|8` 的两族是**相对**（OFFSETOBJHANDLE）引用：`code 10 = +delta`、`code 12 = −delta`。

- ★ **折算是链式的，不是每枚都减本帧句柄**：游标起于**本帧句柄**，此后每枚 `abs = 前一枚的 abs ± delta`。
  `r20tail.py` 的 `absn` 恒以本帧句柄为基 ⇒ **第二枚相对引用起给出的 `->h` 是错的**，自己走游标。
  实案（§555 §三）：`0x4958 + 13 = 0x4965`，再 `0x4965 + 15 = 0x4974`（若按「本帧 ± 」会得 `0x4958+15`）。
- 拿到绝对句柄后**回同一张台账查它的 `type`** ⇒ 直接点名被引用族，不靠猜：
  `0x4965 / 0x4974` 在原生台账里 type 皆 `0x1B` = **`AcDbDimAssoc`**。再补一步「**我方产物里这两枚在场吗**」
  （§555 据此量到同句柄同 type 两枚**就在自家产物里** ⇒ 病在写侧漏发引用，不是「对象池缺件」也不是「ODA 合成我方不合成」那一墙）。

## Step 3d 逐类 `(3,0)` 在位率普查（最便宜的射程尺 / 落刀自证）

同一条解尾语法（Step 3b 的窗口）跑**两个文件**，按**类**只统计一个比值：`含 (3,0) 的枚数 / 该类可解枚数`。

```bash
# 本技能**不带脚本**（免得与仓内取证器漂移）；现场写约 15 行 python，三步：
#   1) R2kCmp 台账按 \t 切 + rstrip('\r\n')，取 hdl=f[2] / type=f[4] / kind=f[6] / hs=int(f[14]) / hi=int(f[8])*8
#   2) 从 hs 起 code-nibble 吃到 hi：ctrl=(code<<4)|size；size>8 ∨ code>14 ∨ 越 hi ⇒ 该枚记「不可解」，**别当 rate=0**
#   3) 按 (type,kind) 聚「含 (3,0) 的枚数 / 可解枚数」，两档并排打 rate
# 驱动一律带 PYTHONIOENCODING=utf-8（Step 0 那条），否则整轮取数崩在写盘前
```

- **为什么它比逐帧 diff 快两个量级**：它**天然免疫句柄 renumber**（不配对，只比率），一把看出射程——
  §554 补据此量到「全 4824 枚带句柄流的对象里 `(3,0)` 率**只差一枚**」= APPID `h=11`，而那枚正是 ODA 的具名抛点；
  同手在真本 `add_2000.dwg` 上量到 42 类 / 1118 枚可解者率**全为 1.00**（⇒ `rate < 1` 就是我方或该档的病，不是语法假设的病）。
- **落刀后的自证**：同普查再跑一遍 ⇒ 「**零类差异**」（原差那类并入原生同率）+ 产物整字节位移
  （§554：`+18 B` = 17 SCALE + 1 DICTWDFLT 各 8 位；§554 补：`+1 B` 只补那一枚 `0x30`）。
  **两把尺子要同时给**：率闭合 + 字节数 == 枚数 × 位宽。
- **它是射程尺，不是判据**：Step 5 的红-先锁仍钉**具名枚**（§549 教训——全档断言会被语料属性打假红）。

## Step 3e ★ R2004 一侧（AC1018 / AC1021）怎么解尾 —— `r20tail.py` 出射程，改用**自家锚**

`tools/r20tail.py` 硬拦 `AC1015` 魔数 ⇒ R2004+ 整侧出射程（Step 3 第 1 条）。R2004 侧的等价三件套
= `tools/ProbeTool` 的三个只读模式（§567 定谱，全部不落产物字节）：

```bash
ProbeTool tshs    <dwg> <hdl>[,<hdl>]     # 打候选窗 + 逐枚解引用（自家 BitReader.ReadHandle）
ProbeTool tshs    <dwg> TALLY             # ★ 全枚统计「读侧登记锚 − 各候选式」⇒ 一次定谱起点
ProbeTool tshscan <dwg> <hdl>             # 帧尾逐相位穷举，适应度 = **语义**（见下）
ProbeTool tshdata <dwg> <hdl>             # [CarryStart+32, BodyEnd*8) 位串 + BodyStart..BodyEnd hex（两侧对齐用）
```

**定谱读数（原生 `sample_2004`，4924/4924 枚有锚对象逐枚成立）**：

> **本仓读侧登记的 R2004 句柄流起点 ≡ `CarryStartBit + RL − 10`**（= `BodyStart*8 + RL + 6`）。
> 而 `bitdiff --hdr` 用的 `hs := 帧长 − 16 − 66 − RL`（= `CarryStartBit + 32 + RL`）比它**晚 42 位**
> ⇒ 那套 `hs=` 数字**不是测量值、是恒等式的余量**（只反映 RL 与帧长的相位差，不含尾流内容；
> 拿它的差去推「尾流多 N 位零垫」= 同义反复，见「陷阱速查」最后一条）。

- **`tshscan` 的适应度必须是语义的**（不是贪心/re-sync 之类位置启发式 —— 那类在表族帧上已实测不可信）：
  每枚引用可解 + `code ≤ 14`、`size ≤ 3` + 链式折算出的绝对句柄**在本档对象表里真实存在** + 恰好收口于
  `BodyEnd*8`（余 **0..7 位**）+ 引用数 ≤ 6（长串 `(5,0)` 是数据不是尾流）。四条同向才算一解。
- **同一枚尾流常有两可解，相差恰 8 位**（原生 531：`(0,1)0x6 + 3 枚` @79 位 vs `(10,2)0x106 + 3 枚` @87 位；
  oracle 的 DIMASSOC：`@112` 与 `@120`）⇒ **两侧必须同式取窗、比「引用清单」而不是比「窗口位数」**；
  只比位数会把对方的 renumber 宽度差读成自己少发一枚（§566 十二 那条否证的机制解释）。
- 终点仍用 `BodyEnd*8`（`frameEnd − bodyEnd == 2` = 对象 CRC 在后，Step 3b 第 1 条在 R2004 同样成立）。
- 读侧锚缺位（`HasHandleStream=False`，如 Opaque 的 531/532）时 `tshs` 的 `登记锚` 列 = 0 ⇒ 只能靠
  `TALLY` 定出的式子 + `tshscan` 自证，**别退回 `RL` 反推**。

## Step 4 三侧对照口径——**先问「两侧是不是同一张图的同一档」，再决定比什么**

| 场景 | 判据 | 为什么 |
|---|---|---|
| (a) **跨产出 / 换档**（真本 `*_2000` vs `*_2018→AC1015`、产物 vs oracle） | 逐类比较 **`(码序, 残位, c3 枚数)` 形制集合**全等，**禁**句柄配对 | 转换后句柄已 renumber，逐句柄 diff 必假红 |
| (b) **本方产物 vs 同图同档原生真本**（如 `sample_2000` 对 `sample_2007→AC1015`） | **逐句柄 join 才是首选对照**，形制集合反而钝 | 同图同档两侧句柄本就同名；§555 的根因（第 3/4 槽被写成空槽）**只有** join 能取出——形制集合里 `(4,0)` 与 `(12,·)` 都是「5 枚引用」，看不出病 |

- 判定式只有一条：**句柄可不可信 = 两侧是不是同一张图**。不确定就先数两档句柄集合的交集率，别默认。
- **残位勿当常量断言**：真本残位多在 5 位、我方多在 4 位（帧界相位不同而长度合法，ODA 与我方
  回读都接受）⇒ 残位只能当「同类同枚的两侧**相等量**」用（另见 Step 3b 第 2 条）。
- **c3 不恒在索引 2**：真本 TABLESTYLE 的 leading code-4 枚数随类随枚而变（`[4,4,4,3,…]` 前缀），
  拿 512 的形套全族会被真本打脸 ⇒ 拆成「逐类强判据」+「同族共用判据（集合相等 + `c3 ≤ 1` 双插哨）」
  两个断言器。
- **两侧枚数不等 ⇒ `--audit` 会自己判「不比」**（前缀众数可能来自不同子集）：那一行是**射程问题**不是形制问题，
  去补锚或按 Step 3d 数率，别当差异报（也别当「同形」）。

## Step 5 红-先锁（离线判据档）

1. 定谱一旦成立，**先**把判据写成离线测试并让它**红**着，再动写侧/落结论——模板 =
   `tests/DwgParser.Tests/CellStyleMapAc1015TailStreamTests.cs`（16 例：512 专属强判据 +
   504/506/510/511/503/500 同族共用判据 + `c3 ≤ 1` 双插哨），判据语料读 R2kCmp 台账、零 ODA 依赖。
   另一枚模板 = `tests/DwgParser.Tests/Ac1018XdicSlotHeadAnchorAc1015Tests.cs`（6 例：拦路轴 +
   **原生同判据正例哨** + **不位移轴**，引用序列走自家 `BitReader.ReadHandle()` 解，**不依赖取证器的语法假设**）。
2. 需要新锚时**只读、绝不登记**：走 `src/DwgParser.Core/Objects/Records/CaptureAnchor.cs` 式
   **新增文件聚合**（`static long TryGet(record)`）。**禁止**为取证写
   `rec.HandleStreamStartBit / HasHandleStream`——§197 契约：这组公共簿记会被 pre-R2004 写侧
   lane 消费，一登记就改产物字节。`DwgObjectRecord.cs` 顶着 798/800 行硬上限，加字段须先拆档
   （那是另一手，须用户裁定）。
3. 惰性自证：干净 HEAD 隔离树跑相关 filter 全绿 → 同一棵树只加新锚文件再跑，仍全绿 ⇒ 零产物影响。

## Step 6 两枚负控（剥 / 双插）——隔离树跑完还原

目的：证明判据**真能抓变异**，不是恒等式摆设。全程在**隔离树**（= 干净 HEAD，避开主树并行车道
未提交的在途代码；主树大批量红先这样归因，别急着改代码）：

```bash
git worktree add out/_<tag> --detach HEAD
cd out/_<tag> && mkdir -p out                       # 新 worktree 无 out/，脚本会崩
python tools/winbuild.py dotnet build DwgParser.sln -t:Rebuild    # 禁直接 --no-build 判事（陈旧/被打补丁的 dll = 假读数）
```

- **负控① 剥**：从判据输入里人为剥掉一枚尾引用 ⇒ 逐类强判据必须转红。
- **负控② 双插**：塞入多一枚 c3 ⇒ `c3 ≤ 1` 哨必须转红。
- 红 = 判据敏感（通过）；**不红 = 判据失明**，回 Step 4 收紧口径。
- 还原：`git worktree remove --force out/_<tag>`；撞 `.git/index.lock`（并发车道在跑 git）
  **不要去动锁**，等待或先备份现场再改在别处。

## 陷阱速查（本流水线实撞清单）

- ★ **`ProbeTool objfields` 的 `Kind=` 不能当「我方不建模某类」的证据**：它直打印
  `DwgObjectRecord.Kind`，而该字段**默认就是 `Opaque`**（`Objects/DwgObjectRecord.cs:53`），只有读侧解码
  回退处会显式置 `Opaque`（`Objects/Reader/DwgObjectReader.Dispatch.cs:696`、`EntityDispatch.cs:96,287`）
  ⇒ **原生 AutoCAD 真件照样报 Opaque**。判「这一档这类到底建不建模/在不在场」用**台账的 `type` 列**
  （Step 2）或两档 join，别用 `Kind`。本仓据此错归因产出一整条（§546 补九～十一 那句「508 走不建模整段搬运」
  被 §554 否证）。
- **`tools/r20tail.py --rlcheck` 不存在**（docstring 有、argparse 无）⇒ rc=2 早退，别把它写进结论或复现步（Step 3 第 3 条）。
- **`--anch` 的相对引用绝对值不链式**（Step 3c）；**非 AC1015 文件被静默跳过**（只打一行 `!!`，其后读数是「没读数」）。
- **拿 `frameEnd` 当句柄流终点 / 窗从 nibble 中间起算** = 半字节相位错，会把对象 CRC 读成「差一段字节」
  （§550 → §554 订正全过程，Step 3b 第 1 条）。
- **python `open(p,'rb')` + `\n` 检索对 CRLF 源静默 0 命中**：一律 `open(p,'rb').read().decode('utf-8')`
  → `replace('\r\n','\n')` 内存归一再匹配；回写 `open(p,'w',encoding='utf-8',newline='')` +
  `replace('\n','\r\n')`。改补丁脚本用**精确整行替换 + 断言 count==1**，防多行串替换切坏结构
  （实案：吞掉相邻函数数行导致 SyntaxError 且旧读数继续误导）。
- **R2kCmp 台账输出带 `\r\n`**（上面 python 的 `rstrip('\r\n')` 已处理）：**`split('\t')` 前不归一**会让最后一列
  `capAnchor` 变成 `'0\r'` ⇒ 比较全落空，看起来像「只读锚覆盖为零」。
- **GBK 控制台**：dotnet 输出直写文件是 GBK、过管道变 UTF-8——读字节后 `errors='replace'`、
  `grep -a`、锚只用纯 ASCII；用中文锚配 replace 会把解码错掩盖成「0 命中」假清零。
- **Git Bash 把 `//FI` 一类参数判成 UNC 改写**（如 `--filter` 值、`--format=//...`）：加引号或
  `MSYS2_NO_PATHCONV=1`/`MSYS2_ARG_CONV_EXCL="*"` 前缀。
- **`python3` 不存在**（本机只有 `python`）：跑了 `python3` 的补丁会**静默没落**，其后所有「读数」
  都是旧文件行为；补丁后必须 `py_compile.compile(p, doraise=True)` + `git diff --numstat` 双自证。
- **`--no-build` 复用被并车道打补丁的 dll** ⇒ 假红/假绿；测试计数异常先新鲜 build 再判定，
  判「是不是我的」用 `git status --short -- tests/ src/` 看失败类所在文件是否未提交改动。
- **列义错位**：`carryStart/hsStart/capAnchor` 已是位位置；`bodyStart/bodyEnd` 是字节偏移（仅
  R2000 无压缩时二者可直读）。
- **回溯锚冒充读侧锚**：`--back` 的产物标签必须带「不可自证」字样，且**永不**进 Step 5 判据。
- ★ **把恒等式当读数**：`bitdiff --hdr` 打的 `hs=` 是 `帧长 − 16 − 66 − RL` 反推出来的**记账余量**，
  不含任何尾流内容信息 ⇒ 拿它的差去推「尾流多 N 位零垫 / 少一枚引用」= 同义反复
  （实案 = §565 补据此立「须先建字段级走查」当前置，§567 用 Step 3e 的语义穷举否证：两侧尾流一位不差）。
  要判尾流内容只有两条：`tshscan` 逐相位语义解，或两侧**同式取窗后比引用清单**（不是比窗口位数）。

## 验证（收尾自检）

- Step 2 三条恒等式在**当轮那两份文件**上各跑一遍全枚成立（`frameEnd−bodyEnd==2` / `carryStart+carryLen==bodyEnd*8` / `typeEndBit==carryStart`）。
- `--frames`：三侧语料各跑一遍，`≥8 位 = 0`；`--audit`：差异类数 = 0（或如实列出差异并定谱归因），
  且「不比」的类数已单独交代（射程问题 ≠ 形制问题）。
- 报「差 N 枚/N 位」前：Step 3b 的位账闭合哨过 + 两侧 pad 同值，两者任一没做就把结论降级为「假设」。
- 逐类在位率普查给**两把尺子**：落刀前后「零类差异」+ 产物字节位移 == 枚数 × 位宽。
- 判据档：`python tools/winbuild.py dotnet test DwgParser.sln --nologo --filter "<档名>"` 全绿，且消融阶梯（撤刀 N 红 / 装刀全绿）逐枚点名。
- 两枚负控各红一次（判据敏感）；隔离树已 `worktree remove`、主树未残留本手实验。
- 取证结论追加进 `docs/issues/` 对应定谱档（逐轮追加、勿覆盖），引用读数带**格数/枚数 + 提交号 + 是否干净 worktree**。
