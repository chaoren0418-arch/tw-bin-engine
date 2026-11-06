# Issue #181 規則檔同步

2026-11-07，code 第 1 次嘗試，分支 `task/181`。

## 做了什麼

- 在 `CLAUDE.md` 資料庫例外清單最後，逐字加入任務提供的 #175 使用者裁定 A，涵蓋 `codex-local` 對 P24Lab 的測試準備寫入與備份、前後對照、還原 SQL 等條件。
- 將素材行的「指向 `releases/r28`」替換為「指向 `releases/r29/assets`（#155）」。
- 將正文逐字同步到 `AGENTS.md` 與 `.cursor/rules/project.mdc`。

## 怎麼驗證（已驗證）

從修改後的 Cursor 檔移除 front matter，將正文以 UTF-8 寫入 `scratch/issue-181/project-body.md`。實際 diff 輸出如下。

```text
git diff --no-index --exit-code -- CLAUDE.md AGENTS.md
stdout: （空白）
exit: 0

git diff --no-index --exit-code -- CLAUDE.md scratch/issue-181/project-body.md
stdout: （空白）
exit: 0

git diff --stat -- CLAUDE.md AGENTS.md .cursor/rules/project.mdc
 .cursor/rules/project.mdc | 3 ++-
 AGENTS.md                 | 3 ++-
 CLAUDE.md                 | 3 ++-
 3 files changed, 6 insertions(+), 3 deletions(-)

git diff --check
stdout: （空白）
exit: 0
```

三份正文的 UTF-8 位元組 SHA-256 相同。

```text
4A2C023AEC5EC088297E7873BD64D365D88FE87ED7C6D80E0FA3CDB77B052604
```

- Cursor front matter 與修改前保存內容逐字比對通過。
- 覆核規則 diff，每份均只有指定的素材行替換及一條例外新增。
- 交付範圍共四份檔案，包含三份規則及本報告。

## 未驗證什麼

- 本輪驗證涵蓋文件文字，未執行建置、遊戲或資料庫操作。
- 素材路徑依本任務指定內容記載，未重新核驗 junction 目標或封存內容。
- 授權引文依使用者提供的任務原文照錄，未另行核對本機調度器對話紀錄。

## 產出路徑

- `CLAUDE.md`
- `AGENTS.md`
- `.cursor/rules/project.mdc`
- `docs/reports/issue-181.md`

## 給下一棒的提示

由調度器／Claude 審查兩處修改及正文一致性，再交使用者合併。後續 P24Lab 測試準備任務應依新增例外完成完整備份、前後對照及還原 SQL，並遵守例外所列用途與環境範圍。本 worktree 留待調度器於任務關閉後清理。
