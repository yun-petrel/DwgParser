---
name: dwg-backlog-liveness-audit
name_en: Direction-A Backlog Liveness Audit
name_zh: 方向A在账档去水审计
description: Use when asked to lay out or re-rank the Direction-A backlog, or to report what is really still open （「把 backlog 列清楚」「还剩哪些真在账」）. Judges each §1 row of docs/direction-a-backlog.md against the machine baseline and a live gate run instead of the row prose, tagging rows live / stale-reading / zombie-fixture / self-declared-closed / broken-table / flipped-but-unblessed / unproven-fatal-list. Read-only; never picks a target. Rows are cost-ranked by fatal-list closure grade（A = chainwalk 闭合到 S={N 枚}->OK 且有充分性另测, B = 控制档绿而判词裸签名, C = 控制档即 REJECT ⇒ 清单未知、禁占起手序顶端）. Any reading of unknown provenance（子代理汇报或别道提交信息里的 sha、§号、坑号、面读数）must first pass the three cheap self-proofs. Readings count as attributable only after the audited commit is proven to build and the baseline proven to hold the time-newest bless; the baseline can also UNDER-report green, so cells the live gate prints as 改进（基线红而现跑绿）are pinned as flipped-awaiting-a-named-bless rather than open work.
description_en: Use when asked to lay out or re-rank the Direction-A backlog, or to report what is really still open （「把 backlog 列清楚」「还剩哪些真在账」）. Judges each §1 row of docs/direction-a-backlog.md against the machine baseline and a live gate run instead of the row prose, tagging rows live / stale-reading / zombie-fixture / self-declared-closed / broken-table / flipped-but-unblessed / unproven-fatal-list. Read-only; never picks a target. Rows are cost-ranked by fatal-list closure grade（A = chainwalk 闭合到 S={N 枚}->OK 且有充分性另测, B = 控制档绿而判词裸签名, C = 控制档即 REJECT ⇒ 清单未知、禁占起手序顶端）. Any reading of unknown provenance（子代理汇报或别道提交信息里的 sha、§号、坑号、面读数）must first pass the three cheap self-proofs. Readings count as attributable only after the audited commit is proven to build and the baseline proven to hold the time-newest bless; the baseline can also UNDER-report green, so cells the live gate prints as 改进（基线红而现跑绿）are pinned as flipped-awaiting-a-named-bless rather than open work.
description_zh: 对 docs/direction-a-backlog.md 的在账行做去水审计；判据取机器基线 + 即时门，不信行内自述。七类水（自我声明已了 / 僵尸夹具 / 读数作废 / 碎表 / 归属错档 / 已翻未钉 / 清单未证实）逐行过，并按致命清单闭合度 A/B/C 排起手成本（C = 链台账走不动，起手须先做层级实验）。基线也会少报绿，外部报来的读数先过三式自证。默认只读、且不在本手跑闭合实验。
argument-hint: 如「列清楚 backlog」
argument-hint-zh: 指定要审的档位或格子族，如「列清楚 backlog」「还剩哪些真在账」
argument-hint-en: Name the backlog section or cell family to audit, e.g. "list what is really still open"
user-invocable: true
---

# 方向 A backlog 去水审计（列清楚 ≠ 照抄）

目标：把「在账」表读成**可信的起手序**。该档 §1 的行数**不等于**真在账的活数——行内自述可能是上一个基线的读数、
也可能那格早已翻绿入库。所以判据只取两件：**机器基线** 与 **即时门**。前置：遵守根 `AGENTS.md` 硬约束与
`dwg-parser-map`（定位）/`dwg-direction-a-cell-forensics`（取数）。

**边界**：本技能**只列不做**。改 §1/§2 归属属热档动作，须 `tools/progress_lock.py acquire` + §0.5 登记 + 用户裁定；
未拍靶前不开任何翻格手。

## Step 0 取三件权威（档内数字一律不算现值）

```bash
git show HEAD:tools/oda-compare/gate-baseline.json > out/_audit_base.json   # 逐格点名红/绿 + 面键数
python tools/winbuild.py python tools/oda-compare/gate.py > out/_audit_gate.txt 2>&1   # 即时真值（勿 --bless）
python tools/progress_lock.py status && git log --oneline -8                            # 谁在动、别接
```

`gate.py` 全门现测 ≈107 s、可一条命令跑完（旧「96 枚超上限跑不动」已作废）；输出是 GBK，落文件再 `iconv` 读。

### Step 0− 取权威前先自证四件：引用自证 / 读数归属哪棵树 / 基线是哪一枚 bless / 基线会不会少报绿

上面那两件权威**同时都可能失真**，未自证就取数 = 把不归属的读数写进在账表。四件里 **④ 最先跑**：它挡的是「这读数根本是别人编的」，比「树不对」「基线不对」更前置，且三式全零成本。

**① 归属：门与测试读的都是「被 build 出来的那棵树」，主树里 = 工作副本，不是 HEAD。**

```bash
python tools/winbuild.py dotnet build DwgParser.sln            # 被审 commit 的树须真跑到 0 Error
git ls-files --others --exclude-standard src | grep '\.cs$'     # 未跟踪源件（必筛 .cs）
```

实测代价：`dd49d8a9`（标题写 `docs(§529 补记)`，实际 **199 files / src 下 93 条路径**，其中 88 枚被写成比其父提交
更旧、**2 枚被删**）落库后，同一时刻主树门读数 **53/60**、该提交态 **21/60**（修复手 `2c95b8b6` 逐 blob 对拍）。
⇒ 判「树干净否」用 Step 0c 那一式；**非零即不得把门/测试读数记在该 commit 名下**，要么去钉住该 commit 的隔离树
重跑（Step 0a），要么把读数显式标成「工作副本态」。`gate.py` 默认就自建（`--no-build` 才跳）且建的是**工作副本**
⇒「门跑绿」本身不证明提交态可用；被提交态删掉却仍在盘上的未跟踪 `.cs` 正是「主树建得过、隔离树建不动」的成因。

**② 基线钉的是不是「时间上最新那枚 bless」：**

```bash
git log --format='%h|%ad %s' --date=format:'%m-%d %H:%M' -- tools/oda-compare/gate-baseline.json | head -6 | cut -c1-70
```

取序之后再跑一次**逐节语义读数**（键集 / 各面 OK 计数 / 单格现值，一式见 Step 0b）。**blob 相异只说明「不同」、
不说明「谁新」**，`ls-files -s` ≡ `HEAD:` ≡ `hash-object` 三方同值也排除不了「HEAD 那枚比我挑的目标更新」——
实测 `45ab5375` 就拿 08:07 那枚 bless 盖掉了当时已在 HEAD 的 10:00 bless（`39253d2a` 自纠；权威叙述在
`pitfalls.md` D120）。`gate.py::shape_mismatch`（`01190993`）现已在 build 之前拒跑**整节回卷 / 面漂移**
（`rc=2` = 门没跑成），但**形状仍是 60 键、个别格读数被退回它抓不住** ⇒ 那一档仍靠人逐格对。

**③ 基线不只「滞后于红」，也会「少报绿」——同树同 HEAD 也可能 现跑 > 基线。**

「基线可能滞后」极易被读成单向。真实形态是双向的：`gate-baseline.json` 被 bless 之后**又有别刀入库而未 bless**
⇒ 基线系统性**偏红**，照它判「在账」就是把已翻绿的格开成靶、并把别人已交的活当待办。

```bash
B=$(git log -1 --format=%H -- tools/oda-compare/gate-baseline.json)   # bless 那一枚
git log --format='%h %s' $B..HEAD -- src/ | cut -c1-60                # 其间的写作侧手
iconv -f gbk -t utf-8 out/_audit_gate.txt | grep -c '\[改进\]'          # 门自报「基线红 → 现绿」的逐格名单（门日志是 GBK ⇒ 直 grep 静默得 0，实测同档 0 vs 2）
```

判据式 = 把两列**逐格对齐**（现跑门的 `[改进]` 行 ∩ 那批手提交信息点名的格名）。命中者一律标「**已翻未钉、待点名 bless**」
：**不算在账红、不进起手序、不得记在本手名下**；钉基线只能 `gate.py --bless` + 用户裁定。
实测两证：① 审 `41357651` 时基线 `cross_producer 19/30 · EDIT 33/40`，同一时刻钉住**该同一 commit** 的隔离树现跑
`21/30 · 37/40`（§549 两格 + §550 四格皆已翻未钉，隔一轮 bless `a27f1540` 才钉掉）；② 现树复跑同式 = 基线钉
`rich 53/60 · producer 25/30`，而 `$B..HEAD -- src/` 已列出 §553/§556/§557/§558 四手 ⇒ 少报当下仍在发生。

**④ 引用先自证（三式，先于 ①②③）：来路不明的读数一律先当假设。**

子代理汇报、别道提交信息里点名的 **sha / § 号 / 坑号 / 面读数** 可能整组凭空存在。本仓提交信息又长又密、机器读数成堆
⇒ **编造的引用与真实的引用在文体上不可分**；照其行事的代价 = 把已绿格当在账、把没测过的收口当事实。

```bash
git cat-file -t <报来的 sha>             # 式一：具名 sha 存不存在（实测某「第 21 手 66937e39」= fatal Not a valid object）
grep -c '<报来的坑号如 D125>' docs/handbook/pitfalls.md # 式二：所指档里 grep 该坑号 / § 号（0 命中 ⇒ 该引用当场作废）
# 式三：门读数只认**本会话自己** `> out/_audit_gate.txt 2>&1` 重定向的那一份
```

式二只可**当场 grep**，不许把「最大坑号 = Dxxx」记成事实：该档随别手增长（同一链上先实测「无 D125」，同一日之内
D125/D126 即已入库）。式三禁 `gate.py … | tail -5` 之后再取 `$?` ⇒ 那拿到的是 `tail` 的退出码，门崩了也报成功。
实测代价：一份伪造汇报的「cross 13/30 · 61 格非绿 · AC1027/32→AC1018 已收口 · 沉坑 D125」与**自己那份重定向的门日志**
（方向A 30/30 · 生产者面 21/30 · 富图 53/60 · EDIT 37/40）逐面矛盾，而其中数字已被复述给用户之后才被三式拆穿。

### Step 0a 门读数只认「钉住被审 commit 的隔离树」

主树并发跑门 = 把别道在途的 `src/` 改动与 dll 重建混进读数，能造出**瞬时 cross REJECT** 这类假回归。
隔离树不只是干净，还**结构性免掉 ODA 单例争抢**：`tools/ODAFileConverter/` 是 git 跟踪目录（实测 56 枚文件）
⇒ 每棵 worktree 自带一份安装目录，而 `tools/oda-compare/oda_wait.py` 的闸口径 = **每安装目录一实例**
（`tools-and-fixtures.md` 单例条目）⇒ 两棵树各跑一进程是合法并发形态，不必为等闸而推迟取数。

### Step 0b 基线逐格只有一式：`st = c['A'] if isinstance(c, dict) else c`

五面单元格形制**不同**（现值实测；面数本身会随并面变 ⇒ 先跑 Step 0−② 再抄本表）：

| 面 | 格数 | 形状 |
|---|---|---|
| `cross` | 30 | dict `{A, A_msg, B}` —— **两维不等价**：`A` = 我方产物被 ODA 收，`B` = ODA 产物被我方读净 |
| `cross_producer` | 30 | 同上形制（`a746cec2` 并入的 AutoCAD 亲手生产者面，键名与 `cross` 同构 ⇒ 子串法在此面同样吞红） |
| `rich` | 60 | dict `{A, msg}` |
| `edit` | 40 | dict `{A, msg}`，`A` 可取 `OK` / `REJECT` / `REFUSED` |
| `recopy` | 6 | **裸字符串** —— 无脑 `c['A']` 在此面直接 `TypeError` |

```bash
python -c "
import json,io,collections
d=json.load(io.open('out/_audit_base.json',encoding='utf-8-sig'))
g=lambda c:c['A'] if isinstance(c,dict) else c
for f,v in d.items(): print(f,len(v),dict(collections.Counter(g(c) for c in v.values())))"
```

**子串法一律禁用**：`'"OK"' in json.dumps(c)` 会把 `cross` 的 `{"A":"REJECT","B":"OK"}` 读成 OK ⇒ 整面 30 格假全绿。
更阴的一层：该形制在 `cross` 面**结构上恒在**（30 格枚枚带 `B`），而当轮 HEAD 又恰好全 `A=OK`（实测：四面 136 格
两法同值）⇒ 子串法**当场看不出错**，只在某格翻红那一手静默吞掉它。判「在账」只取 `A`；`B` 红而 `A` 绿 = 读侧账，另档归口。

**「门没报 shape 错」≠「基线是最新 bless」**：`shape_mismatch` 只核形状，逐节语义读数那一步不可省（射程与同族误卷见 Step 0−②）。

### Step 0c 「树干净否」不许拿 `git diff HEAD --numstat` 判

共享 index 里存在**已无条目**的路径（暂存态 `D ` 而字节仍在盘上）时，numstat 会报一批 `0 N` 的**幽灵删除**
（实测复现：N 恰等于盘上真实行数）⇒ 据此判「工作树背离 HEAD」必错。修法：逐枚对哈希
`git ls-files -s <p>` vs `git rev-parse HEAD:<p>`。另一条实测：`MM`（index 与盘上各改一刀）**不报** `0 N`、
而是报真实行数，但它三方 blob 不同值，同样只能靠哈希对拍。

## Step 1 解析这张表的四个必修坑

- **取字节 + BOM + 行尾**：必须 `io.open(path, encoding='utf-8-sig', newline='')` 再 `.splitlines()`。
  用 `open(p, encoding='utf-8').read().split('\n')` 会得到**一整行**（text-mode 早把 CRLF 折成 LF），
  于是「§1 零行在账」这种假结论当场就出来了；打印 `⇒`/`①` 会崩在写盘前 ⇒ 一律落 UTF-8 文件再读。
- **逐行数「列数」** = `len(line.strip('|').split('|'))`，大于表头列数即**碎表行**：行内裸 `|`（hex 串、
  `{setText,delete}`、`AC1015->AC1018|add`）把 markdown 打碎成 10–18 列 ⇒ 该行的「现势 / 下一手」列**读不出来**，
  只能引原文碎片并标「结构已坏」，勿据此推断语义。
- **往 §1 加行只能按表头 5 列写**：`# | 车道 | 欠什么 | 起手 / 已闭合前置 | 锚`。实测 §1 现存 27 行列数直方 =
  `{3:1, 4:3, 5:14, 6:4, 7:1, 11:1, 13:1, 16:1, 19:1}` ⇒ **旧行不可当范本**（4/6 列那几行本身就是裸竖线破的）；
  新行固定 5 列、单元格内零裸竖线（hex 串、`{a,b}`、`AC1015->AC1018|add` 一律改写成全角或顿号形）。
- **别整读**：该档量级 440 KB（≈11 万 token）。按上面的脚本切片，只把 §1 的行的**列摘要 + 行尾**打出来看。

## Step 2 七类「水」的判据（每行过一遍，逐条给出处）

| 类 | 判据式 | 处置 |
|---|---|---|
| R1 自我声明已了 | 行内含「已收口 / 已判否 / 勿重开 / 收口」却排在 §1 | 移 §2，不算在账 |
| R2 僵尸夹具 | 行内点名的夹具集/验收线已不在仓（`git ls-files -- <夹具目录>` 空、或 `gate.py` 注释/`tools-and-fixtures.md` 记着该验收线退役） | 整行下线 |
| R3 **读数作废** | 把行内数字与 `out/_audit_base.json` **逐键**对（取状态只按 Step 0b 那一式，禁子串法）：面键数（30 vs 60）、`x/30` 分子、具名抛点 `(hdl)` | 标「读数过期」并给现值，禁照抄 |
| R4 碎表 | Step 1 的列数判据 | 单列交代「内容无损、结构不可读」 |
| R5 归属错档 | 工具面/卫生类遗留（`ODA_SINGLETON` 误判、ledger 覆写、一次性脚本丢失）混在翻格行里 | 挪进 §6 |
| R6 **已翻未钉（基线少报绿）** | 现跑门的 `[改进]` 格名 ∩ `git log bless..HEAD` 里写作侧手点名的格名（式见 Step 0−③）⇒ 基线比真值**偏红**的那几格 | 标「已翻未钉、待点名 bless」；**不算在账红、不进起手序、不得记在本手名下** |
| R7 **清单未证实当已知成本** | 行内写「只剩 N 枚 / 两把刀 / 唯一拦路」，但机器侧指不出闭合证据（`out/chainwalk/` 无该格的 `S={…} -> OK`、`inter_bisect.py --state` 那份 json 不存在或不 OK、没有 `allowlist=N hits=N` 那行自证） | 按 Step 2.5 降为 B/C，成本列写「清单未知」，**不得排在 A 级行之前** |

R3 的「核过」定义：差异格再单跑一次；`NO_OUTPUT`/`CRASH` 记「门没跑成」不是红（pitfalls D46）。
引用**全量单测的红**当在账证据时，归属须以消融自证 = **把本手 src 退回 HEAD 重建后红名与红数逐枚相同**
（实测 `a01a1494` 那 7 枚即以此式定的），只说「大概是既存红」不算；且不得据此改代码、不并进门判据面。

**门自报的 `[改进]` 不归属本手**：同一轮里混着别道未钉的战果（实测落库底那一轮 `producer 26/30 · 富图 55/60`，门列的三条 `[改进]` 里只有**一枚**属本手，另两枚 = 别道 §552 与 §554 各一枚）。⇒ 报「本手翻了几格」只认**逐格比对**「本手 delta 那一轮」与「落库底那一轮」两份门日志，两列同格才算；只看一轮必把别人的格记进自己账。

**另有三种「不是我的账」须各自举证，一句「大概是既存红 / 别道在跑」不算**：

- **同常量撞车**：本手验到底的改动与别道**已入库的同一处常量**相同（实测 `nr>64→1024` 两边各写了一次）⇒ 门列的那条
  `[改进]` 归别手、本手撤重复件。撤时最高危一步 = **往主树回拷文件前先核拷的是「消融态」还是「修好态」**
  （消融自证会把闸强制成 `=> false`；把消融态拷回主树 = 静默反向回滚他人刚入库的修复）。
- **`CRASH` / `NO_OUTPUT` 复跑**：先单格复跑（D46），再补一条**零位移自证** = 把本手 src 退回 `HEAD` 重建，同一目标档
  产物哈希与改动前**逐字节相同**，才允许写「该格非本手造成」（实测正是这条把一枚 `→AC1021` 的 `CRASH` 判成与那把
  `nr` 刀无关）。拿不出哈希等式就只能记「未归因」，禁按「目标档走不到我的代码路」这种推理归因。
- **`rc=3`**：这是「门没跑成」的一种，且**不等于**「别道在跑」——`tools/oda-compare/oda_wait.py::preflight` 对取不到进程
  映像路径的情形按**保守命中**处理（宁可少跑一次，也不放过一次争抢）⇒ 报「别道占着、本手不取数」前先核它点名的 pid
  是否自家残留（`progress_lock.py status` 是另一件事，别拿来当门的占用证据）。

凡引用数字须带 **格数 + `git rev-parse --short HEAD` + 树干净否** 三件套。

## Step 2.5 排成本的那把尺 = 致命清单**闭合度**（A/B/C 三态，先评级再排序）

§1 的「起手成本」列是散文，会随别手交账漂移。唯一可机检的成本轴 = **这格的致命清单闭合了没有**：未闭合的格无论行内
写得多轻，起手都得先花 N 轮 ODA 去量清单；闭合的格成本 = 枚数 × 每枚有无对偶刀。

| 级 | 判据（须能在档里指出机器证据） | 处置 |
|---|---|---|
| **A 清单已知** | `chainwalk` 走到 `S={N 枚} -> OK`，**且**做了充分性另测：只换这 N 枚（`allowlist=N hits=N`、`misses` 与 `inter−N` 对得上）⇒ ODA OK | 才可评「已知完整清单」；成本 = N 枚 × 每枚有无对偶刀（见下） |
| **B 控制档绿而判词裸** | `bypass all` = OK，但链首拿不到具名枚（先过下面的「两型不具名」这一关） | 起手 = 层级实验而非落刀，**不得算低成本靶** |
| **C 清单未知** | `bypass all` 即 REJECT（`[ 0] S={} -> REJECT`） | 标「链台账走不动，起手须先做层级实验（节级 `swapsect` / 整类不 emit 消融）」；**禁占起手序顶端** |

```bash
python tools/oda-compare/chainwalk.py 新增图纸/sample_2007.dwg ACAD2004 25          # 逐枚前移；S={…}->OK 即闭合
python tools/oda-compare/inter_bisect.py 新增图纸/sample_2007.dwg ACAD2004 --state out/_bisect.json
```

**默认不在本手跑这两条**（一次 chainwalk = N 轮 ODA，审计要的是评级不是取证）。只有当某行的成本断言**决定起手序**、
而档里又指不出既有证据时，才现测；否则该行标「未核」并按 C 处理。**闭合实验的正确做法与状态续跑口径
在 `dwg-minimal-fatal-set-bisection` / `dwg-direction-a-cell-forensics`，勿在此重述。**

**四件必须先过的口径**（每件都能把一格判成另一级）：

- **「不具名」有两型，先排解析假象**。`gate.py`/`richverify` 取的是 `.err` **末行**，而末行常只是回指前错的套话
  ⇒ 造出**假不具名**。`chainwalk.py` 取 `Object improperly read:` 那一行，且认「同行」与「`ODA Warning` + 次行
  `AcDbXxx(hdl)`」两种具名格式 ⇒ **定 B/C 前先跑 chainwalk**，仍裸才是真不具名（真不具名的处置才是层级实验；
  实测换 `AcDb:Classes` 节才把裸签名具名化成 `<AcDbDataLink>(8E99)`）。
- **`bypass` 的空规格默认是 `all`**（全旁路 ⇒ 假 OK）⇒ 对照态必须显式传 `none`。
- **二分收敛的 N 枚只证必要、不证修好即翻** ⇒ A 级要的是「只换这 N 枚就 OK」那一测；「去掉它就红」不算。
- **A 级 ≠ 必翻**，它只说明清单完整。**级联症状会骗人**：`<AcDbTableContent>(90EA)` 在多手判词里当过病灶，
  逐族穷举后证明它是 FIELD 失步后的下一跳报错点 ⇒ 据此订正掉了一整手的方向结论。

A 级内部再按**每枚有无对偶刀**折成本（同族两枚可差一个量级）：`ls src/DwgParser.Core/Objects/Writer/` 找
`XxxUp.cs` / `XxxDown.cs` 的成对件。**有对偶刀 ≠ 廉价到手**——其头注那张位账表可能只自证过「我方写出的形闭合到 RL」，
拿去逆用真源会失步（实测 `FieldUp.cs` 头注在真实 AC1021 源上走查失步、首刀内部回滚）⇒ 记成本时写成「有配方起点、
须先独立对一次两侧表」。**正形要求新造对象**（把内联内容拆成独立内容对象）= 语义裁定档，不是位宽账，
起手前须先出对象分配设计稿过用户目。

## Step 3 输出（固定三段，末尾不代拍）

1. **水分计数**——§1 N 行中 X 行 R1/R2、Y 行 R3、Z 行 R4、W 条 R5、V 条 R6、U 条 R7（未核的单列「未核」）。
2. **去水后真在账表**——`靶 | 停在哪（可核读数）| 清单闭合度（A/B/C + N 枚）| 起手成本 | 谁在动`；在途靶显式标「别接」；R6 那批格另起一区标「已翻未钉」、**不进起手序**；C 级那一区按 Step 2.5 明写「须先做层级实验」，不得排在 A 级之前。
3. **要议的几件**——是否批准那几行下线/挪档、要不要把某集并入常态门（并门 = 口径变更第四处，见 `dwg-direction-a-cell-forensics` Step 7⑨）、是否点名 `gate.py --bless` 把 R6 那批格钉进基线（不点名则它们继续以「已翻未钉」形态存在，既不红也不该开靶）、要不要为某格的闭合度破例现跑一次 chainwalk（花 N 轮 ODA）、先开哪格。

## 陷阱速查

- 把 §1 的**顺序**当事实——它是「按起手成本排序」，而成本随别手交账漂移；每次重排。
- 把行内「现停 `<AcDbX>(hdl)`」当现值——那可能是上一个基线周期；只认 Step 0 两件。
- 把行内「只剩 N 枚 / 两把刀」当闭合——拿不出 `S={…}->OK` 与 `allowlist=N hits=N` 就是 C 级未知成本（R7、Step 2.5）。
- 拿「判词裸签名」直接定 B/C——`gate`/`richverify` 读 `.err` 末行会造**假不具名**，先跑 chainwalk 再定级（Step 2.5）。
- 把二分收敛出的 N 枚当「修好即翻」——那只证必要；A 级要的是充分测。
- 把 A 级当「必翻」——清单完整 ≠ 每枚都会翻；级联症状（判词里的第二枚）常是下一跳报错点。
- 拿 `rc=3` 当「别道在跑」而放弃取数——`oda_wait.preflight` 对取不到进程目录按保守命中处理，先核自家 pid。
- 回拷共享文件前没核「消融态 vs 修好态」——会把别道刚入库的修复反向回滚掉（Step 2 末段）。
- 拿子串法读基线（`'"OK"' in json.dumps(cell)`）或拿 `git diff HEAD --numstat` 判「树干净否」——
  两件**都不报错**：前者把翻红那格吞成绿（暂存时恰全绿 ⇒ 看不出）、后者报一批 `0 N` 幽灵删除，判据在 Step 0b、0c。
- 在主树跟别道并发跑门拿到的红格当回归报——先查本手树是否钉住单一 commit（Step 0a）。
- 数「在账 = 25 行」就汇报进度——本技能存在的全部理由就是这一条不成立。
- 把「门跑绿」当「提交态可用」——门 build 的是工作副本，同刻主树 53/60、提交态 21/60 实测过（Step 0−①）。
- 拿 blob 相异 / 三方同值体检判基线新旧——两者都只说「不同」、不说「谁新」（Step 0−②）。
- 以为基线只会「滞后于红」——它同样会**少报绿**（bless 之后又有刀入库而未 bless）；同树同 HEAD 基线 `19/30`、现跑 `21/30` 实测过（Step 0−③、判据 R6）。拿这类格当红开靶 = 重做别人已交的活。
- 把门自报的整批 `[改进]` 当本手战果——里面混着别道未钉的战果，逐格比对两轮门日志才谈归属（Step 2 末段）。
- 拿别道提交信息或子代理汇报里的 sha / §号 / 坑号 / 面读数当权威——先过三式自证（Step 0−④）；「最大坑号」这类值只可当场 grep、不可记死，而 `gate.py … | tail` 之后的 `$?` 是 `tail` 的退出码不是门的。
- `git ls-files --others src` 不筛 `.cs` 就当噪声——现测 `src tests` 共 105 枚未跟踪件里 60 是 `.shx`、31 是 `.trx`，
  真 `.cs` 只 10 枚（`src` 单面 2 枚）⇒ 不筛就是拿噪声当账、或拿噪声盖住真漏。
- 为了「让表好看」改判据或 `--bless`——基线只能由用户点名产生。
- 动档前先抢锁：`progress_lock.py acquire` 返 3 = 他道存活持锁 ⇒ 只报判据，写档留下一手。
