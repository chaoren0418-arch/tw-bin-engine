# Issue #1132：#1119 R07 證據重新封裝

已完成 13 個 PNG／build log 的 off-git 保存與逐 byte／SHA-256 核對；#1119 的 R07 ✅、合計 ✅ 14／❌ 0／⚠️ 0 維持原判定。本次未重跑實機。

## 做了什麼

- 來源 commit：`79d30d6e8e14214e350aa95ec461579b1ebc3bf7`（PR #1128、`task/1119`）。13 個原始檔均仍在 `scratch/wt/task-1119/docs/reports/issue-1119/`。
- 指定持久位置：`D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-1119-evidence\`。只新增，未覆寫或刪除既有檔案；原 worktree 未清理。
- Git 僅帶入原報告、證據 JSON、還原 SQL，以及 4 TSV、11 TXT、2 JSON。原報告只替換 12 個 PNG／log 連結並追加重新封裝說明；證據 JSON 僅新增 `offGitEvidence`（13 筆，含 path、bytes、sha256、originalGitPath、sourceCommit）。原 `classes[].screenshot` 與 `artifactSha256` 保留歷史紀錄，可用 `originalGitPath` 對照新位置。
- 未修改引擎、`docs/REPORT.md`、dev lock、資源閘門或 allowlist。

## 13 檔清單與比對結果

以下檔案均位於上述持久目錄；每筆 SHA-256 同時對應來源 worktree、指定 commit 的原 git blob 及目的檔。

| 檔名 | bytes | SHA-256 | 比對 |
|---|---:|---|---|
| `created-selection.png` | 23294 | `349b4c44abc44888d81af4b1fa15abd2374f1014bb6377163067a88814bc149c` | 相同 |
| `darkelf.png` | 18961 | `89cb5050c078c1b7a13641138fa8ef15e02cf9429442b40c5eacd9a78a49b1d1` | 相同 |
| `deleted-selection.png` | 23197 | `5a06bf34e7b024ae4867bc44d702d8bba86d39e0104064a0e69000d4dfb2dcf9` | 相同 |
| `dragon.png` | 18661 | `9a1ec00d0034ebb324b34f607fc7cfcce1313112eec90b26e1b3fbb4397030f3` | 相同 |
| `elf.png` | 18818 | `ffd6b05f7a19585ea3d1c25d65ed9cc260d8e215bb800fc746ea20c84ccf7f3f` | 相同 |
| `illusion.png` | 18951 | `1ddb30eef6d6cc19432ddcc39c53e09a7a2d69b12f6459e6d96d73d07b32d953` | 相同 |
| `knight.png` | 18710 | `b2962a7559e60b46bf48cc1b5b1b93126908e2d3acd1236246298cb32c37d899` | 相同 |
| `launcher-build.log` | 179 | `50226f53bfef73e4c1d707efd3fed04009fe5f36db98e4988d50dc3f9bdfe496` | 相同 |
| `mage.png` | 18729 | `a7821fa009b9cf1b614f535a7a75845830e38da2dc00ac535ce61f5af9f9e512` | 相同 |
| `notepad-probe.png` | 61925 | `c8eef4fb5d7a5cc400499cce8dbc894c79711c2d4ebd15fef59ff4442a0f0b85` | 相同 |
| `product-build.log` | 2455 | `c388774a1afc2e092168d970398a8897335e4ba90658588ffb94138708d013fb` | 相同 |
| `royal.png` | 18636 | `e5ce7699d1ac9e8901f3700ec5b936710824af5f46064b5969486485d350f7e1` | 相同 |
| `selection.png` | 50905 | `526b3839b791795c3d139428fc7c8ac3c3db73f8b8c7b33c639b613213b4758b` | 相同 |

## 怎麼驗證

- 以二進位 `git show <commit>:<path>` 取得原 blob，與原檔及保存後目的檔比對；13/13 相同。
- 17 個文字證據與還原 SQL 的 git index blob 與來源 commit 相同；JSON 除新增陣列外，其餘資料完整相等；報告判定及其他文字保留。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：`status: pass`、4,785 tracked files、0 errors；保留既有 L1 已裁定限制，未放寬 allowlist。
- `git check-ignore --no-index --stdin -z` 檢查全部追蹤路徑：無命中（exit 1）；`git ls-files -ci --exclude-standard` 無輸出，沒有被忽略的檔案進 git。
- `git diff --check` 與 `git diff --cached --check` 通過；本次 3 個 JSON 全部解析成功；變更範圍為指定的 21 個文件檔，沒有 PNG／log。

## CI 結果

待推送本分支並建立 PR 後，確認 build／dotnet、map-continuity、npc-offline 均為 SUCCESS；全綠後才關閉被取代的 PR #1128。

## 未驗證什麼

- 未啟動遊戲、伺服器、MySQL 或任何編譯產物；未建角、刪角、寫 DB、執行還原 SQL 或重新截圖。實機通過結論沿用 #1119，並非本次重測。
- 原始封包 bytes、WGC 逾時根因仍依 #1119／#1129 的已知限制；不因封裝改變判定。

## 產出路徑

- `docs/reports/issue-1132.md`（本報告）。
- `docs/reports/issue-1119.md`、`docs/reports/issue-1119-evidence.json`、`docs/reports/issue-1119-restore.sql`。
- `docs/reports/issue-1119/` 下 17 個 TSV／TXT／JSON；13 個 off-git 檔在上述持久目錄。

## 給下一棒的提示

- 由 Claude／調度器完成合併前閘門審查，再由使用者／既有合併流程將本 PR 合併到 main；本機執行器不自行合併。本報告進 main 才完成該項驗收。
- PR #1128 由本分支取代；保留 `task/1119` 分支。
- 冷備份 `scratch/wt/task-1119/scratch/issue-1119/backup-before` 不在這次 13 檔搬移範圍，仍須由調度器／dot 決定持久保存；在備份妥善保存前不要清理該 worktree，任何時候都不可遞迴刪除含 junction 的目錄。
- 不重做 R07；本機 PNG／log 保存位置不能由 GitHub 線上直接開啟，請依清單 SHA-256 核對。
