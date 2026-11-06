# #272／T132 決鬥：草稿，接收掛接待範圍裁定

## 做了什麼

- 新增 `engine/source/Duel.cs`：C5 邀請、C121 接受／拒絕、S219/630、S250/5、S71 決鬥訊息解碼及狀態模型；送出閘門核對 session、world epoch、revision、面向與選取對象。沒有改變攻擊准入。
- 新增 `engine/source/DuelUi.cs`：服務視窗決鬥頁、邀請／接受／拒絕按鈕、伺服器確認後的對手標記；`ObjectRendering.cs` 僅一個繪製查詢呼叫。
- 新增 `engine/_integration/dev/duel-tests/DuelChecks.cs`，接上既有 `DemoChecks.csproj`、`duel` 測試群組與 CI 已執行的 `combat-admission` 群組。
- 封包與來源 SHA-256 整理於 `docs/protocol/duel.md`；只更新 dev lock，未改封存或 `docs/REPORT.md`。

## 阻塞與目前可用性

**本草稿尚不可視為功能完成，也不應合併。** Issue 指定「WorldSession.cs 只在 Yes-No 既有分派加一個決鬥子型別分支」，但這份 main 的 S219 分派在 `Trade.cs/ReceiveTrade`；S250/5 與 S71 又是不同 opcode。現有指定位置不存在。

建議的具體最小調整：在 `WorldSession.ReceiveCore` 建立 reader／讀取 opcode 後、既有接收鏈之前，加一行 `if(ReceiveDuel(opcode,plain))return;`。所有解碼仍在新檔，非決鬥封包保留原 reader cursor 與分派。這個調整已向使用者提出範圍確認，尚未收到答覆，因此 **WorldSession.cs 尚未修改**。

現況 C5 可送出，但決鬥邀請／開始／結束尚未進入新模組；UI 不會假裝收到成功。完整回歸正確暴露此缺口，沒有刪除測試或降級斷言以取得綠燈。

## 怎麼驗證

已驗證：

- `rebuild.py --update-lock` 後執行 `--build`：產品、audio、launcher 編譯成功。產品有既有 `MagicUi.cs:20` 的 CS8600／CS8602 警告；未修改該檔。
- NuGet 網路來源遭本機 TLS 錯誤拒絕；改以 `scratch/issue-272/NuGet.Config` 唯讀引用原專案 `engine/vendor`，快取與建置輸出均留本 worktree。
- 測試專案編譯：0 警告、0 錯誤。
- **模組直接呼叫測試**：scratch 臨時 harness 以 reflection 呼叫產品 DLL 的 `ReceiveDuel`，再讓未消費封包進原有分派；84 項通過。與既有戰鬥准入測試一起執行為 296 項通過。這只是擬議接收掛接的模擬，不能替代已提交入口的整合驗證。
- `check-no-game-assets.py` 通過；`git -c core.whitespace=cr-at-eol diff --check` 通過。保留既有 CRLF 檔案位元格式。

未通過／待驗證：

- 已提交的正式離線入口 `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release -- . scratch/issue-272/duel-checks.json duel` 於 `S71/631 matches invited peer` 失敗，原因是尚未新增接收掛接；不是封包解析測試通過的宣稱。
- CI 綠燈未達成；草稿 PR 的 Windows 測試預期會重現上述整合缺口。
- 未啟動遊戲、伺服器或 DB；未做畫面、網路或真機驗收。

## 產出路徑

- 程式：`engine/source/Duel.cs`、`DuelUi.cs`、`ObjectRendering.cs`。
- 提交的測試：`engine/_integration/dev/duel-tests/DuelChecks.cs`，既有入口 `engine/_integration/dev/demo-tests/`。
- 契約：`docs/protocol/duel.md`；本報告：`docs/reports/issue-272.md`。
- 本機暫存證據（不提交）：`scratch/issue-272/build/`、`module-checks.json`、`module-combat-checks.json`、`module-tests/`。

## 給下一棒的提示

1. 調度器／Claude 先裁定是否允許上述單行接收鏈掛接；不必擴增 `Trade.cs` 的修改範圍。使用者的 async 確認仍待回覆。
2. 掛接後重新 `--update-lock`、完整建置並執行正式 `duel` 與 `combat-admission` 群組；不要把 scratch harness 的結果當成完成。
3. CI 綠燈後再解除草稿；原生 smoke 由 dot 依既有授權驗證兩角色的接受、拒絕與結束標記。
4. 協議沒有邀請取消指令，且世界重建時本機保守清除標記；若沒有新的 S250/5，不自行恢復狀態。
