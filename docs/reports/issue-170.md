# Issue #170 封存素材還原的一次性例外

2026-11-07，code 第 1 次嘗試，分支 `task/170`。

## 做了什麼

- 在 `CLAUDE.md`「AI 產出規則」第 7 條後逐字加入任務指定的 #156 裁定 A 一次性例外，適用於 #157 的 r28／r33 素材還原，於 #157 結案後失效。
- 將正文同步到 `AGENTS.md` 與 `.cursor/rules/project.mdc`，保留 Cursor front matter。

## 怎麼驗證（已驗證）

從修改後的 Cursor 檔讀取並移除 front matter，將正文以 UTF-8 寫入 `scratch/issue-170/project-body.md`。實際 diff 輸出如下。

```text
git diff --no-index --exit-code -- CLAUDE.md AGENTS.md
stdout: （空白）
exit: 0

git diff --no-index --exit-code -- CLAUDE.md scratch/issue-170/project-body.md
stdout: （空白）
exit: 0

git diff --stat -- CLAUDE.md AGENTS.md .cursor/rules/project.mdc
 .cursor/rules/project.mdc | 1 +
 AGENTS.md                 | 1 +
 CLAUDE.md                 | 1 +
 3 files changed, 3 insertions(+)

git diff --check
stdout: （空白）
exit: 0
```

三份正文逐字相同，UTF-8 位元組 SHA-256 均為下列值。

```text
B2130A9655DA3D94AA42C6D05FB7F6484E2ACCC051EAACE881AB0FD2DB909AC9
```

- Cursor front matter 與修改前保存的內容比對通過。
- 人工覆核三份規則的完整 diff，每份均只新增指定原文一行，位置緊接第 7 條。
- 追蹤檔案變更範圍為三份規則與本報告。

## 未驗證什麼

- 本輪只執行規則文件編修與文字核對，未建置或啟動遊戲，未操作 DB、封存素材或硬連結。
- #157 的素材還原、seal SHA 核驗與 hardlink 建立留待該任務驗證。
- 授權引文依使用者提供的任務原文照錄，未另行核對本機調度器對話紀錄。

## 產出路徑

- `CLAUDE.md`
- `AGENTS.md`
- `.cursor/rules/project.mdc`
- `docs/reports/issue-170.md`

## 給下一棒的提示

由調度器／Claude 審查指定例外原文及三份正文一致性，再交使用者合併。合併後由調度器續派 #157，執行者依例外逐檔核對 seal SHA，遵守禁止覆寫、刪除及修改 r29–r32 的限制。#157 結案後例外失效。本 worktree 由調度器於任務關閉後處理，禁止遞迴刪除。
