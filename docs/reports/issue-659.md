# #659 規則檔加入 lock 與自動發現規則

**做了什麼**：在 `CLAUDE.md`、`AGENTS.md`、`.cursor/rules/project.mdc` 的「技術棧與建置」加兩條規則：
- PR 不改 `rebuild-lock.json`，由合併後重產；同步遇到 lock 衝突時取 main 的版本（T271）。
- 新模組用 attribute 自動發現，不改 `Program.cs`、csproj、註冊表（T270）。

**怎麼驗**：三份檔案的新增行相同（`git diff` 各 +2 行）。

**沒驗什麼**：調度器端的「合併後重產 lock」由 #644 處理。
