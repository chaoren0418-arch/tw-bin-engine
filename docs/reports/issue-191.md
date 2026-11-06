# Issue #191／T104：緩速操作政策

## 做了什麼

- 移除 `OnlineMovement.cs` 的 `SlowActionsBlocked`／`SlowActionNotice`，以及 `Ground.cs`、`PlayerPointer.cs` 的緩速專用拒絕分支。
- 移除 `Inventory.cs` 的「緩速只准加速藥水」條件；已支援的補血藥、加速藥、肉、回家卷軸與裝備沿用既有 C164 編碼及各自准入檢查。
- 保留移動邏輯、速度變更取消舊請求、道具／地面冷卻、數量／距離、死亡／麻痺／交易／世界世代等保護。請求送出不代表成功，物品與屬性仍等待伺服器回報。
- 沿用 S71、S161、S243 的伺服器訊息解碼與顯示模型；已知訊息顯示既有譯文，未知 S71 保留訊息編號及參數，文字封包保留原文。本次沒有新增接收 opcode 或重排共用分派。

依據：#132 差異 18、`docs/reference/server-rules.md` R22–R27，以及 `docs/reference/server-protocol.md` 的 C164／C25／C112／C34。worktree 基準 `69cd94a` 尚無 M6 契約，已唯讀查閱 [task/186 的契約](https://github.com/chaoren0418-arch/tw-bin-engine/blob/task/186/docs/design/m6-contract.md)；不另複製或改動主控文件。

## 怎麼驗證（已驗證）

- 先執行 `rebuild.py --update-lock`，再完整 dev 建置；product、audio、launcher 成功。主程式 0 錯誤，既有 `MagicUi.cs:20` 的 CS8600／CS8602 共 2 個警告；launcher 0 警告、0 錯誤。
- `DemoChecks` 的 `slow-actions` 群組：**364 個斷言通過**。直接執行本次產品 DLL 的接收／送出路徑，使用合成封包、記憶體 sender 與自行編寫的時序 metadata；不執行遊戲入口或開 socket，不依賴原遊戲資源。
- 覆蓋三種速度、六種已支援道具、直接與滑鼠拾取／交談、丟棄；核對完整 C164／C25／C112／C34 bytes。S71／S161／S243 在 sender 完成前後到達都保留理由、不預扣物品、不自動重送。
- 負向案例覆蓋死亡、麻痺、交易、移動冷卻、數量錯誤、送出前選取／世界／速度變更。確認緩速移動間隔仍為正常兩倍。
- `check-no-game-assets.py` 通過；dev 唯讀 lock 核驗通過（172 個來源、181 個 pinned 輸入）；`git -c core.whitespace=cr-at-eol diff --check` 通過。原有 CRLF 檔案保留換行格式。
- CI 結果以本 PR 的 GitHub Checks 為準，推送後確認；不能把本機建置當作 CI 或真機通過。

本機網路還原首次遭 NU1301／Windows TLS 憑證錯誤；重跑時唯讀使用主專案 `engine/NuGet.Config` 及 `engine/vendor` 的既有套件，NuGet cache、暫存與輸出均放本 worktree。未修改原專案。

重跑命令（Python 3.12 可用完整路徑呼叫）：

```powershell
python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock
python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config --output scratch/issue-191/build
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-191/slow-actions.json slow-actions
python -B -X utf8 engine/tools/check-no-game-assets.py
```

本次產品 DLL SHA-256：`ab13f7d58744d5d2e3ecd80c0fa9d3ea7aa90caa8764fde18feda6726d2cce25`。來源逐檔 SHA-256 見同提交的 `engine/_integration/dev/rebuild-lock.json`。

## 未驗證什麼

未啟動遊戲、伺服器或 DB，未驗證 P24Lab 實際接受／拒絕結果、原生畫面、緩速拾取動畫或音效。合成封包只能證明客戶端政策與資料流，不證明伺服器已執行操作；伺服器無訊息的靜默拒絕仍不能推定原因。未擴充未知道具格式、未知速度或其他既有支援範圍。

## 產出路徑

- 政策：`engine/source/OnlineMovement.cs`、`Ground.cs`、`Inventory.cs`、`PlayerPointer.cs`。
- 測試：`engine/_integration/dev/demo-tests/SlowActionChecks.cs`，入口 `Program.cs` 的 `slow-actions` case。
- dev lock：`engine/_integration/dev/rebuild-lock.json`。
- 本報告：`docs/reports/issue-191.md`。
- 本機證據（不入 git）：`scratch/issue-191/slow-actions.json`、`scratch/issue-191/build-offline/`。

## 給下一棒的提示

Claude／調度器審查政策與 lock 差異；與 T103 都可能觸及 `Ground.cs`，只需保留移除緩速條件的兩處小改，lock 衝突由同步輪重新生成。使用者合併後交 dot 的 M6 smoke：緩速狀態以一般玩家操作道具、丟撿及右鍵交談，核對 C164／C25／C112／C34、伺服器數量與拒絕文案，並補驗動畫。既有 CI 並未新增此群組執行步驟，重跑本模組請使用上述 `slow-actions` 指令。
