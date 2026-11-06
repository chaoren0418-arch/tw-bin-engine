# Issue #198／T110：傳送 recovery wrapper 相容

## 做了什麼

- 在 `Teleport.cs` 定義 recovery 格式白名單；`LoginReader.Finish` 只增加明確 N+8 判斷，保留原 N-byte 邊界與逐 byte 零 padding 檢查。
- 放寬限 S202/mode 7、S206 的四欄位 bytes、S119 的六欄位 bytes，以及符合已核對 `S_OwnCharPack` 結構的 S87。仍拒絕其他 opcode／mode、非零尾碼、錯誤長度及雙重 wrapper。
- 未修改共用分派、傳送狀態機或通用欄位讀取。依腳本重新產生 dev lock，沒有手動拼接 hash。
- 依 #132 與唯讀伺服器來源補上 [封包契約](../protocol/teleport.md)，含來源 SHA-256、原包／N+8 向量及限制。

## 怎麼驗證（已驗證）

- `.NET SDK 10.0.300`：先 `rebuild.py --update-lock`，再完整 `--build`；產品、audio、launcher 建置成功。產品 0 錯誤，僅既有 `MagicUi.cs:20` CS8600／CS8602 兩項警告；launcher 0 警告、0 錯誤。
- `TeleportChecks`：**4,369 個檢查通過**，直接執行本次產品 DLL 的 `LoginReader` 與 `WorldSession.Receive`；全為合成封包，不啟動遊戲入口、視窗或 socket。
- 正向涵蓋四種格式的原包／N+8 包、S87 全部 8 種 padding 餘數及非空 title／clan、正常恢復流程、無 S149 的提早中止。確認 unlock／map 單獨到達仍等待自身資料，其他人物不冒充自身，最後外觀生效。
- 負向涵蓋各種截短／過長、N+16、原 padding 與新增 8 bytes 逐位置污染、所有無關 opcode、255 種非 recovery mode、錯誤欄位邊界與 S87 不符格式；壞地圖包不改既有世界狀態。
- `check-no-game-assets.py` 通過；dev lock 核驗通過（172 個來源／181 個 pinned 輸入）；`git diff --check` 通過。
- CI 狀態以本 PR 的 GitHub Checks 為準，推送後結果另外回報於 PR。現有 workflow 未自動執行新增的 `TeleportChecks`，本模組測試使用下列本機指令。

本次唯讀使用主專案 `engine/vendor` 的既有 NuGet 套件；暫存設定、cache、建置與測試輸出均留在本 worktree。沒有建立素材 junction 或複製遊戲資源。GitHub 操作需在沙箱外使用既有 Windows 憑證；沙箱內的 Schannel／認證錯誤不是憑證失效。

可重跑命令（先將暫存與 NuGet cache 指向 worktree，Python 可用 3.12 完整路徑）：

```powershell
python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock
python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config --output scratch/issue-198/build
dotnet restore engine/_integration/dev/teleport-tests/TeleportChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/teleport-tests/TeleportChecks.csproj -c Release --no-restore -- scratch/issue-198/teleport-checks.json
python -B -X utf8 engine/tools/check-no-game-assets.py
```

產品 DLL SHA-256：`bd7a856af545048d413a2e8c49af504c7c99638abfd56f8fd75b17c0a71006dd`。來源 SHA-256 見同提交的 dev lock。

## 未驗證什麼

未啟動遊戲、伺服器或 DB；未做真機登入、實際傳送失敗、原生畫面或音效驗收。未改正式環境、封存、硬連結或 `docs/REPORT.md`，也未執行任何刪除。

wrapper 無識別標記，僅能辨識符合來源的 wire 格式，不能證明發送來源；S87 自身身分仍交既有 objectId 邏輯判斷。S250/FF34 徽章仍未支援，沿用原本未支援封包處理，不宣稱徽章功能完成。

## 產出路徑

- 程式：`engine/source/Teleport.cs`、`engine/source/LoginProtocol.cs`。
- 測試：`engine/_integration/dev/teleport-tests/Program.cs`、`TeleportChecks.csproj`。
- dev lock：`engine/_integration/dev/rebuild-lock.json`。
- 契約：`docs/protocol/teleport.md`；本報告：`docs/reports/issue-198.md`。
- 本機證據（不入 git）：`scratch/issue-198/build/`、`scratch/issue-198/teleport-checks.json`；本機重跑腳本：`scratch/issue-198/run.ps1`。

## 給下一棒的提示

Claude／調度器審查白名單與 Finish 的最小差異；dev lock 衝突交同步輪重新生成。使用者合併後，由 dot 在 M6 smoke 補驗真實傳送中止：解鎖後等待地圖與自身資料，恢復位置與外觀，確認無 protocol length 錯誤或提前解鎖。不要為此擴大到其他 opcode、徽章功能或任意尾端 bytes。
