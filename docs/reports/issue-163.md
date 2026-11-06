# Issue #163：P24Lab 測試角色暫時 GM 授權規則同步

日期：2026-11-07；階段：code，第 1 次嘗試；分支：`task/163`。

## 做了什麼

- 在 `CLAUDE.md` 的資料庫 dot 例外清單最後，逐字加入任務提供的 #160 裁定 A，未改寫或擴張授權。
- 在既有 2026-11-06 授權引文後加註「（原文『…』之後的條款未取得）」。
- 恢復「工作方式」第一條：歷史治理文件（主管／工作者對話 ID、Goal、pins、seal）只供參考，不再是流程的一部分。
- 將正文逐字同步至 `AGENTS.md` 與 `.cursor/rules/project.mdc`；保留 Cursor front matter。

## 怎麼驗證（已驗證）

從修改後的 Cursor 檔實際讀取並移除 front matter，將正文寫入 `scratch/issue-163/project-body.md`，執行以下比對：

```text
git diff --no-index --exit-code -- CLAUDE.md AGENTS.md
stdout: （空白）
exit: 0

git diff --no-index --exit-code -- CLAUDE.md scratch/issue-163/project-body.md
stdout: （空白）
exit: 0
```

三份正文的 UTF-8 檔案 SHA-256 相同：

```text
661BE615B4FB098E9EAE685C7364E186F36183D10EF3E295ADC2D1934C2F6A12
```

- Cursor front matter 與修改前保存內容逐字相同。
- `git diff --check` 通過；逐項覆核規則檔 diff，僅有指定三處變更。每份規則檔的 `git diff --numstat` 均為新增 3 行、刪除 1 行。
- 修改範圍僅三份規則檔及本報告；`docs/REPORT.md` 未改動。

## 未驗證什麼

- 本輪為文件變更，未建置、啟動遊戲、操作 DB 或服務；未驗證 smoke、GM 操作及還原。
- 授權文字依本次任務提供的原文照錄，未取得 dot 對話完整原文，也未推測省略條款。
- GitHub 連接工具不可用，`gh` 既有登入 token 回報 HTTP 401，無法重新讀取線上 #160／#163；本輪依使用者提供的完整任務與既有 `docs/reports/issue-107.md` 核對。

## 產出路徑

- `CLAUDE.md`
- `AGENTS.md`
- `.cursor/rules/project.mdc`
- `docs/reports/issue-163.md`

## 給下一棒的提示

調度器／Claude 審查三處指定文字與正文一致性，通過後交使用者合併。若 GitHub 發布仍受認證阻擋，由本機調度器修復認證後續接推送及 PR；PR 內容預備於 worktree 的 `scratch/issue-163/pr-body.md`。本 worktree 保留，由調度器於任務關閉後清理；不可遞迴刪除。後續真機工作由 dot 依核定授權範圍接手，本任務沒有執行該授權。
