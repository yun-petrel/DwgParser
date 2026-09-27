---
name: dwg-skill-mirror-sync
name_en: Skill Mirror Sync (multi-copy, .dsh as base)
name_zh: 技能多副本同步（`.dsh` 为基准 + 双向合并）
description: Keep this repo's four skill copies in sync, treating the in-project .dsh/skills as the base. Covers the four roots (.dsh/skills, .workbuddy-ai/skills, .workbuddy/skills, ~/.qoder-cn/skills) and the easy-to-miss one that lacks the -ai suffix, why editing only .workbuddy-ai silently skips the base, how to decide which copy is newer (mtime plus line count plus diff-hunk shape, not file size alone), the bidirectional union merge for genuinely forked skills (take the superset body, union the description fields), per-copy line-ending policy (never mass-normalize), and verifying with a normalized-md5 multi-way comparison over all skills. Use when a skill was edited and must be propagated, when the copies disagree, when asked to sync skill copies, or before trusting a skill edit that was only made in one place. Real surface = 7 roots (3 user dirs + 4 in-repo mirrors); a sync lane may pre-align them, so compare first and verify-only; ABSENT != inconsistent; probe EOL per file; only .dsh is tracked.
description_en: Keep this repo's four skill copies in sync with the in-project .dsh/skills as the base. Covers the four roots and the easy-to-miss one lacking the -ai suffix, why editing only .workbuddy-ai skips the base, deciding which copy is newer (mtime + line count + diff-hunk shape), the bidirectional union merge for forked skills (superset body + unioned description), per-copy line endings (never mass-normalize), and a normalized-md5 multi-way verification over 7 roots (3 product user dirs + 4 in-repo mirrors), with the sync lane that can pre-empt any cp, ABSENT vs inconsistent reporting, per-file EOL probing, and the git-side HEAD-only-line check.
description_zh: 本仓技能有**四处副本**（`.dsh/skills` · `.workbuddy-ai/skills` · `.workbuddy/skills` · `~/.qoder-cn/skills`），**基准 = 项目内 `.dsh/skills`**。覆盖：为何只改 `.workbuddy-ai` 会漏掉基准、**不带 `-ai` 后缀的 `.workbuddy/skills/` 是最易漏的第四处**、怎么判「哪一版更新」（mtime + 行数 + diff hunk 形状，不能只看体积）、**真分叉时的双向并集合并**（取正文超集 + description 各取并集）、行尾口径（**禁止批量归一**，会重写全仓技能字节）、以及用「归一化 md5 多方对比」对全部技能做终检。 ★ 真面对齐 = **七棵**（3 棵产品用户目录 + 4 棵仓内镜像），只比四棵会假收口；环境里的**同步道**可能已自行对齐 ⇒ **先比后写**，已全等就只验证别再 `cp`；**ABSENT（该根没这枚）与「不一致」分两类报**；行尾**逐枚**探；七棵只有 `.dsh` 被 git 跟踪 ⇒ 终检还要核 HEAD 独有行是否逐名可解释。用于：改完技能要传播、副本之间不一致、被要求「同步技能副本」、或想信任一处单点改动之前。
argument-hint: 无参：跑全量多方对比 + 同步；或指明要处理的技能名
argument-hint-zh: 无参跑全量对比，或点名技能（如「同步 dwg-direction-a-cell-forensics」）
user-invocable: true
---

# 技能多副本同步（`.dsh` 为基准 + 双向合并）

## 0. 四处根（先记住谁是基准）

| 代号 | 路径 | 角色 |
|---|---|---|
| **A** | `.dsh/skills/`（项目内） | **★ 基准**，一切以它为准 |
| B | `.workbuddy-ai/skills/`（项目内） | 副本 |
| **D** | `.workbuddy/skills/`（项目内） | 副本 · **最易漏**：它**不带 `-ai` 后缀**，与 `.workbuddy-ai/` 是**两个不同目录**，名字近到一眼扫过去就跳过 |
| C | `~/.qoder-cn/skills/`（用户目录） | 副本（同目录下另有 `_sync-backup-*/` 历史备份，**不是技能**，别统计别同步） |

★ **本表的「四处根」只是仓内视角，真面对齐 = 七棵**（2026-09-27 实测）：3 棵产品用户目录
`~/.qoder-cn` / `~/.qoder` / `~/.qwenworkcn` + 4 棵仓内镜像 `.dsh` / `.zcode` / `.workbuddy` / `.workbuddy-ai`。
⇒ §1 与 §5 的对比脚本必须**先枚举七棵**再比 md5；只比四棵会「全等收口」而另外三棵其实落后。
⇒ **新建的技能常只落在部分棵**（本支自己实测只在 4 棵，缺 `~/.qoder` / `.zcode` / `~/.qwenworkcn`）
⇒ 报告分两类写：**内容不一致** 与 **该根根本没有这枚（ABSENT）**；ABSENT ≠ 一致，也不等于该建（建 = 扩面，须用户裁定）。

**铁律**：改技能**先改 A**，再 `cp` 到 B / D / C。
只改 B（或只改 C）是最常见的错 —— 基准 A 会静默落后，下一次同步就把你的改动冲掉。

★ **已知例外（2026-09-27 实测）：环境里有一条同步道会自己把七棵对齐**，别把功劳记成 `skill_manage` 的。
本轮对 `dwg-golden-rebless` 做完 9 次 `skill_manage patch` 后收尾发现：**七棵** mtime 全 = `09:17:26`、
归一化 md5 全等 `31f2bece`，且**各保原有行尾**（`~/.qoder` / `.dsh` / `~/.qwenworkcn` = CRLF，其余 = LF）。
⇒ ① **「先比后写」不是礼节而是必需** —— 写入前跑一次 §1 就多半发现根本不需 `cp`；
② 已全等时**只做验证、不要再 `cp`**，`cp` 反而可能把别道在途的副本盖回去；
③ 因果**未隔离**（别道同秒跑同步也有同样读数）⇒ 每轮按现场对比处理，别按「谁写谁对齐」推断。

## 1. 多方对比（动手前必跑）

归一化行尾后比 md5；行尾差异**不算内容差异**，但要在报告里分开说。

```python
import os, hashlib
roots = {'A .dsh': r'.dsh/skills',
         'B .workbuddy-ai': r'.workbuddy-ai/skills',
         'D .workbuddy': r'.workbuddy/skills',
         'C ~/.qoder-cn': os.path.expanduser('~/.qoder-cn/skills')}
norm = lambda p: open(p, 'rb').read().replace(b'\r\n', b'\n')
for n in sorted(os.listdir(roots['A .dsh'])):
    hs = [hashlib.md5(norm(os.path.join(r, n, 'SKILL.md'))).hexdigest()[:8]
          if os.path.exists(os.path.join(r, n, 'SKILL.md')) else 'MISSING'
          for r in roots.values()]
    if len(set(hs)) > 1:
        print('DIFF', n, hs)
```

## 2. 判「谁更新」：mtime + 行数 + **diff hunk 形状**

**不能只看文件体积**（一方可能精简改写、另一方可能追加章节，体积谁大都可能）。

```bash
for n in <技能>; do
  ls -l --time-style=full-iso ".dsh/skills/$n/SKILL.md" "$HOME/.qoder-cn/skills/$n/SKILL.md" | awk '{print $6,$7,$NF}'
  diff -u <(sed 's/\r$//' ".dsh/skills/$n/SKILL.md") <(sed 's/\r$//' "$HOME/.qoder-cn/skills/$n/SKILL.md") | grep "^@@"
done
```

四种典型结论：

0. **七棵归一化 md5 全等**（最常见，尤其改的是产品棵时 —— 见 §0 的同步道）⇒ **无事可做**，
   直接跳 §5 终检 + 报「本轮没有一棵落后」，别为了「执行同步」去 `cp`。
1. **纯行尾差** ⇒ 内容已一致，**不用动**（报告即可）。
2. **一侧是另一侧的超集**（diff 只有 `+` 新增 hunk，或新增 hunk + 个别行改写）⇒ 用**超集那一版**当基准。
3. **双向真分叉**（两侧各有对方没有的 hunk / 各自独有章节）⇒ 走 §3 的**并集合并**。

⚠ **mtime 只能当线索**：并行 lane 可能正在改某一副本（本仓实测：C 的 mtime 比本手动作早 44 s ⇒ 疑似别道在途）。
mtime 新 + 内容独有 ⇒ 先判「是不是别人正在写」，宁可只报告不合并。

## 3. 双向并集合并（真分叉时）

原则：**正文取超集，description 类字段取并集**。

1. 先用 §2 的 hunk 形状确认哪一侧的**正文**是超集（例：C 正文 = A 正文 + 2 个新增章节）。
2. 以**超集那侧的全文**为底。
3. 把**另一侧独有的 description / description_zh 短语**补回去（description 是触发用的，两侧措辞各有价值，不要二选一）。
4. **自证**：合并版必须满足
   - 「C 独有行未并入 = 0」且「A 独有行未并入 = 0」（除被并集取代的 description 行本身）
   - 用 `set(merged.split('\n'))` 做行级包含检查，逐条打印未并入的行，**不许只看行数**。

```python
merged = set(out.split('\n'))
missA = [l for l in a_lines if l not in merged and not l.startswith('description: ')]
missC = [l for l in c_lines if l not in merged and not l.startswith('description: ')]
print('A 未并入', len(missA), 'C 未并入', len(missC))   # 期望：仅被取代的 description 行
```

5. 合并结果**写回 A**（基准），再 `cp` 到 B / D / C。

## 4. 行尾口径

各副本行尾**本就不统一，而且「哪棵 = 哪种行尾」这个断言本身不成立** ⇒ 逐枚数 `CRLF/LF` 再决定写法。
（2026-09-27 实测同一枚 `dwg-golden-rebless`：`~/.qoder-cn` 纯 LF，而 `~/.qoder` / `.dsh` / `~/.qwenworkcn` 纯 CRLF；
`.dsh/skills/` 内部也混住 —— `golden-rebless` 420 / `bitstream-diff` 260 / `minimal-fatal-set` 340 枚 CRLF，
而 `census-gap-triage` / `direction-a-cell-forensics` / `fieldization-capture-only-hand` = 纯 LF。）

- **同步 = 内容同步**；写入时**保持目标文件原有行尾**，或至少**别把「统一行尾」当成同步的一部分**。
- 用 `Edit` 工具会**把 CRLF 规范化成 LF** ⇒ 改 CRLF 文件必须走**字节级替换**（Python `rb`/`wb`），并在写完后核验 `CRLF 数 == LF 数`（纯 CRLF 文件）。
- 批量归一全仓技能行尾 = 重写每一个技能的字节 ⇒ 除非用户明确要求，**不要做**。
- 实践：若 A 是 CRLF 而目标副本是 LF，**别直接 `cp`**（会把 CRLF 带过去）；按目标原行尾重写。

## 5. 终检（收尾必跑）

§1 的脚本必须报 **不一致数 = 0**；有 `MISSING` 也要单列出来（副本缺文件 ≠ 内容不一致）。

```bash
# 快速版：全技能多方 md5
python - <<'PY'
import os, hashlib
roots = [r'.dsh/skills', r'.workbuddy-ai/skills', r'.workbuddy/skills',
         os.path.expanduser('~/.qoder-cn/skills')]
bad = tot = 0
for n in sorted(os.listdir(roots[0])):
    tot += 1
    hs = [hashlib.md5(open(os.path.join(r, n, 'SKILL.md'), 'rb').read()
                      .replace(b'\r\n', b'\n')).hexdigest()[:8] for r in roots]
    if len(set(hs)) > 1: bad += 1; print('NO ', n, hs)
print(f'技能总数 {tot} · 不一致 {bad}')
PY
```

### 5b. git 面终检（七棵里只有 `.dsh` 被跟踪，必做）

`git ls-files` 实测 —— `.dsh/skills/**` 在仓内跟踪，`.workbuddy/skills/`、`.workbuddy-ai/skills/`、`.zcode/skills/`
整目录 `??`，新建的技能（如本支自己）同样未跟踪 ⇒ **内容同步对了 ≠ 账面没丢东西**，还要核 HEAD：

- `git status --porcelain -- .dsh/skills` —— 本手那枚应显示 ` M`；同目录常有别道在途枚
  （2026-09-27 同刻另有 `dwg-fieldization-capture-only-hand`（已在 index）、`dwg-parser-map`、
  `dwg-red-first-ablation-verification`）⇒ **勿 `git add .dsh/skills`**。
- 行级包含检查：取 `git show HEAD:.dsh/skills/<档>/SKILL.md`（去 CR）的行集，数「HEAD 独有行」，
  **每行都必须逐名可解释为「被本手有意取代的那几句」**；本手实测 = 6 = 三支旧 description
  + 被三支表取代的 2 行 + 1 行旧「顺序铁律」。出现别的句子 ⇒ 同步把别人的内容带走了，立刻回查。
- 提交一律 `git commit --only -- .dsh/skills/<档>/SKILL.md`（单文件），推送须当轮点名。

## 6. 坑（都实测过）

- **漏基准**：只改 `.workbuddy-ai/skills/` 而不改 `.dsh/skills/` ⇒ 下一次同步把你的改动冲掉。
- **漏第四副本**：`.workbuddy/skills/`（无 `-ai` 后缀）与 `.workbuddy-ai/skills/` 是两个目录，极易只看到一个。
- **盲目覆盖**：拿 A 直接盖 C 前**必先 diff** —— C 可能是**更新的**那一侧（本仓实测 C 独有 2 个章节）。
- **把行尾差当内容差**：不归一化就比 md5，会把「内容已一致」误报成不一致。
- **`Edit` 工具静默改行尾**：CRLF 文件必须字节级编辑（见 §4）。
- **别道在途**：副本是**共享状态**，别的 lane 也在写；mtime 很新 + 内容独有 ⇒ 先报告、别合并。
- **`~/.qoder-cn/skills/_sync-backup-*/`** 是历史备份目录，不是技能，别统计、别同步。
