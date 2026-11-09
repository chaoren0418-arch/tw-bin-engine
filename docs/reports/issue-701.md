# Issue #701 — T291：gap-matrix 第四輪

## 做了什麼
- 依 main `1786d89a`＋合併佇列 PR #699（#607）、#700（#667）、#702（#690，只有文件）重新核對 `docs/reference/gap-matrix.md`，新增第 13 節（第 1–12 節不改）：
  - 13.1 方法；13.2 本輪新發現與「程式已寫但產品沒接上」表（25 項）；
  - 13.3 狀態變更表（85 列：部分→已有 14 列、缺→部分 1 列，其餘只更新依據，每列寫明補上的任務與 `檔案:行`）；
  - 13.4 統計；13.5 第四批 code 分組 R4-A–K（11 組）；13.6 原生 UI 預設開啟前的缺口（甲乙丙丁四類）；13.7 需先查證 Q4-3–Q4-9；13.8 限制。
- 新增 `docs/reference/rea-scripts/gap_count_r4.py`：依序套用第 11、12、13 節的變更表重算統計；`--list` 印出所有非「已有」列。
- 更新 `docs/reference/INDEX.md` 的 gap-matrix 列。

## 新統計（290 列）
已有 **108**、部分 **138**、缺 **44**（伺服器不支援／不建議 39、可直接補 5、需先查證 0）。第三輪是 94／151／45。

## 怎麼驗證
- 讀 25 個合併 PR 的 `docs/reports/issue-<N>.md`，用 Grep 找產品端呼叫點（排除測試與 `_integration`）；報告閱讀與呼叫點掃描分給 4 個唯讀子代理，關鍵結論（原生 UI 預設關閉、28 個 `[UiPanel]`、S_NEWS 模態框、門的 `]`、隱身血盟條件、`ShowDevToolsPage`）我另外複核。
- 合併佇列 PR 只用 `git show origin/task/<分支>:<路徑>` 讀，沒有 checkout。
- `py -3 -I docs/reference/rea-scripts/gap_count_r4.py docs/reference/gap-matrix.md`：三輪變更 88／87／85 列全部套用，沒有錯誤。

## 未驗證什麼
- 沒有建置、執行，沒有啟動遊戲、伺服器或 DB；沒有開 IDA（本輪不需要新的反組譯）。
- 「產品沒有呼叫」靠 Grep，以反射或委派間接呼叫的可能會漏。
- PR #699、#700 合併時若再改動，13.3 標「（PR #n 合併後）」的列要再核對。PR #702 也改 gap-matrix 檔尾（新增 12.9），兩個 PR 誰後合併誰要同步一次（12.9 的表不會被重算腳本當成變更列）。
- `[UiPanel]` 同序值重複時的排列是否穩定、T201（#475）真機 smoke 結果，本輪都沒有查。

## 產出路徑
- `docs/reference/gap-matrix.md`（第 13 節）
- `docs/reference/rea-scripts/gap_count_r4.py`
- `docs/reference/INDEX.md`
- `docs/reports/issue-701.md`（本檔）

## 給下一棒的提示
- 先派（不等別人）：R4-A（選單鈕、熱鍵、**原生 UI 下信件打不開**）、R4-B（背包／魔法／倉庫原生窗畫內容）、R4-D（料理窗）、R4-E（血盟窗）、R4-I（原生 `HYPER_TEXT_WINDOW`）、R4-K（S22 播放中音量）。PR #702 合併後派 R4-G、R4-H；PR #700 合併後派 R4-J；R4-B 合併後派 R4-C。
- **T290（#698）第 1 項「城主王冠」已在產品畫出**（`ObjectRendering.cs:162`，#618 做的），建議派工前改成「名稱旁隊伍／主人圖示（FU5§1）＋門的 `]`」；T288（#689）的「聊天第二色」R3-G 已處理，可拿掉。
- 原生 UI 是否預設開啟要使用者決定：依據見 gap-matrix 13.6。建議先做完 R4-A、R4-B，再做一次真機 smoke 後決定。
- Q4-3–Q4-9 建議合併成 1 張 resource 任務（IDA 靜態查證，照 #417，subst 磁碟先檢查）。
