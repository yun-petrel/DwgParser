---
name: dwg-q10-ff-push
name_en: DWG Q10 Fast-Forward Push
name_zh: DWG按Q10快进推送远端
description: Verbatim Q10 fast-forward push recipe for the DwgParser Windows main tree. Use when the user names Q10 and asks to push (按Q10推送远端 / push 远端 / 推上去); naming Q10 already carries step-4 push authorization, so no second confirmation. Probe shape read-only first - fetch, ls-remote for the live remote tip, rev-list --left-right for ahead/behind, merge-base --is-ancestor for ff. behind=0 plus ff means Q10 steps 1/3/5/6 (backup, detach paths plus independent-index merge, 3way re-apply, index cross-check) are all no-ops and only push plus three post-push checks run. A dirty worktree is never a push blocker (push depends only on the committed tree), so never stash or reset to look clean. Non-fast-forward cells exit this skill and expand the full faq.md Q10 sequence.
description_en: Verbatim fetch-prove-fast-forward-push-crosscheck pipeline for the shared DwgParser remote; decides mechanically whether Q10 steps 1/3/5/6 collapse to no-ops, and records worktree-unchanged proof around the single write action.
description_zh: DwgParser Windows 主树「按 Q10 推送远端」的逐字复跑配方。先只读探测形态（fetch → ls-remote 取远端真值 → rev-list --left-right 数领先/落后 → merge-base --is-ancestor 判快进），behind=0 且判快进 ⇒ Q10 的 ①备份 / ③摘路径+独立索引合并 / ⑤3way 回填 / ⑥索引对拍**全部 no-op**，只做「存证 → 推送 → 推后三件对拍」。工作区再脏也不是推送阻塞项（push 只依赖已提交的树），严禁为「清干净」而 stash/reset。非快进形态退出本技能、展开 faq.md Q10 全序。用户点名 Q10 即含第④步推送授权，不再二次请示。
argument-hint: 无需参数；触发语如「按q10推送远端」
argument-hint-en: No argument; trigger phrases like "push remote per Q10"
argument-hint-zh: 无需参数；触发语如「按q10推送远端」
user-invocable: true
---

# 按 Q10 推送远端（纯快进形态）

## 授权边界（先钉这一条）

- **用户当轮点名 Q10 或「推送」= 第④步推送授权已给**（2026-09-23 用户裁定「直接执行 Q10，不要确认」）。不再回头二次请示，也不要把「合并到主树」「提交到本地 main」当授权 —— 那两句只授权到提交为止。
- **本技能只处理一种形态** = `behind == 0` 且快进成立。另一形态（远端有本地没有的手、或被判 `force-needed`）**立刻退出本技能**，走 `docs/handbook/faq.md` Q10 ①–⑥ 全序（含备份、摘路径、独立索引合并、3way 回填、索引对拍）；强推一律须用户裁定。
- **禁止脚本化推送**（根 `AGENTS.md` §1 铁律 ⑦）：本技能**故意不带 scripts/**，Step 2 与 Step 3 之间必须留人工判点，探测与推送不得被合成一条命令。
- **只有主树能推**（铁律 ①）。起手先确认落点：

```bash
git worktree list | head -1     # 首行须是主树 D:/Project/DwgParser 且带 [main]
```

## Step 0 只读探测（一条命令，无写副作用）

```bash
git fetch origin main 2>&1 | tail -2
echo "=== local main ==="; git rev-parse main
echo "=== remote tip (live truth) ==="; git ls-remote origin main
echo "=== ahead/behind main...origin/main ==="; git rev-list --left-right --count main...origin/main
echo "=== remote-only ==="; git log --oneline main..origin/main
echo "=== local-only (this push would land) ==="; git log --oneline origin/main..main | head -20
```

两个口径注意：

- 数的是**本地 `main`**，不是 `HEAD`。主树通常 `HEAD == main`，但别道 worktree 多处于 detached 态 —— 被推的那枚 ref 是 `main`，探针也钉 `main`。
- ⚠ **文档、记忆、上一手报告里出现过的 `origin/main` sha 一律不可照抄**（可能是上一小时的）。本轮基线只认这次 `fetch` 之后的读数 + `ls-remote` 的远端实测。

## Step 1 形态门（决定走哪条，只有本技能这边才继续）

```bash
git merge-base --is-ancestor origin/main main && echo ff || echo force-needed
grep -c DWGPARSER_PUSH_AUTHORIZED .git/hooks/pre-push || true
```

- `behind == 0` **且** `ff` ⇒ 纯快进。判定写进结论：**① 备份 / ③ 摘路径+独立索引合并 / ⑤ 3way 回填 / ⑥ 索引对拍 全部 no-op**（合并不写任何路径），只跑 Step 2–4。
- `behind > 0` 或 `force-needed` ⇒ 停手，转 faq.md Q10 全序；不要为了套用本配方而先 `reset` 或强推。
- 第二行是**现场探测而非断言**（2026-09-24 实测 = `0`）：当前安装的 `.git/hooks/pre-push` 只校验 ref 形状（两档都只放 `refs/heads/main` → `refs/heads/main`、拒删远端 ref），**不查授权标记**；而 `tools/install-git-hooks.py:163` 的模板查。⇒ 标记照设（换机 / 重装钩子后即拦），但**不得**把「设了标记且没报错」当成机器证明了授权 —— 授权只来自用户那句话。

## Step 2 推前存证（工作区指纹）

```bash
git status --porcelain | sort | md5sum
git status --porcelain | cut -c1-2 | sort | uniq -c
```

记下这两行读数（md5 + 逐状态计数）。主树长期挂着别道在途 WIP（实测量级 82 M / 19 D / 72 ??），**脏不是阻塞项**：push 只依赖已提交的那棵树。⇒ 任何「先把工作区弄干净再推」的念头都属违规动作（禁 `stash`、禁 `reset --hard`、禁 `checkout -- .`）。

## Step 3 推送（本技能唯一的写远端动作）

```bash
DWGPARSER_PUSH_AUTHORIZED=1 git push origin main
```

- 只 `main` → `main`。**禁** `--force` / `--force-with-lease` / `--no-verify`；禁新建分支、禁推非 `main`。
- 一次性人工动作，推完即止。禁定时器 / CI / 钩子 / 「顺手补一刀」。

## Step 4 推后三件对拍（缺一即不算收口）

```bash
git ls-remote origin main; git rev-parse main
git rev-list --left-right --count main...origin/main
git status --porcelain | sort | md5sum
```

三条判据，逐条给读数：

1. **远端 tip == 本地 `main`**（用 `ls-remote` 实测，不用 fetch 缓存）。
2. **计数 == `0  0`**。
3. **工作区 md5 == Step 2 所记**（未变）。

⚠ md5 **变了**通常不是本手所为 —— 别道并行写了同一路径。处置 = 查因（比对逐状态计数、看哪枚路径新出现），**不回滚别人的 WIP**，也不因此重推。若推送后又落了新提交 ⇒ 第二推**先回 Step 0 重取** `origin/main`，别拿旧基线硬推。

## Step 5 结论输出（固定三段，与本仓既有手数同形）

- **形态**：`N` 领先 / `0` 落后、远端 tip `<sha>`、纯快进 ⇒ ①③⑤⑥ 全 no-op。
- **推送**：`<old>..<new>  main -> main`；`ls-remote` 实测远端 = `<new>` = 本地 `main`；计数已 `0/0`。
- **工作区**：md5 推前后同为 `<hash>`（`M/D/??` 计数），他道在途 WIP 未被触及；本手零改动 ⇒ 无第二推。

## 失败分档（照读法处置，别即兴）

| 症状 | 归因 | 动作 |
|---|---|---|
| `Everything up-to-date` 或 Step 0 已 `0 0` | 无可推 | 报「远端 == 本地 main（`<sha>`），无可推」并停手，不算失败 |
| push 被拒 `non-fast-forward`（Step 1 曾判 `ff`） | fetch 之后远端又动了一手 | **回 Step 0 重跑**整条探测；别拿旧引用硬推、别在解好的冲突上叠回填 |
| `401` | 未认证 | 走凭据通路（本机 https 直推即可，GCM 有凭据，正常不弹交互） |
| `404` | 令牌对私有仓无权限 | 换有权限的令牌（`git credential approve` 落盘），别改 remote URL |
| pre-push 拒绝 | ref 形状不合铁律 | 停手报告；**绝不** `--no-verify` 绕过，先查为什么推的不是 `main → main` |

## 边界（本技能不管什么）

- 提交怎么切（`git commit --only -- <本次文件>`、文件级 scoping、不裹别道 WIP）= faq.md Q7 / Q11。
- 撞冲突的合并全序 = faq.md Q10（本技能只判「该不该展开」）。
- 跑门、重钉基线、取证 = `dwg-golden-rebless`、`dwg-direction-a-cell-forensics`、`dwg-minimal-fatal-set-bisection`。

判据权威只有两处 = `tools/oda-compare/gate-baseline.json` 与即时 `gate.py`；本文任何 sha / 计数 / 行号都只是写作当下的读数（写于 `cecb7c04`，实测主树 `1` 领先 / `0` 落后），引用前现场重跑。