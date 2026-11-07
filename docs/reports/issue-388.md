# Issue #388／T148：中毒、麻痺、詛咒盲目

## 做了什麼

- 新增 `engine/source/Afflictions.cs`：S165 正常／綠色／灰色外觀、S202 異常狀態輸入鎖與排隊請求失效、S47 盲目及解除。
- `WorldSession.cs` 僅新增三個 opcode case；`OnlineInput.cs` 僅新增一個鎖定查詢；`ObjectRendering.cs` 僅將既有像素合成區段接到一個異常狀態合成入口。
- S202 沿用既有傳送模組的鎖集合，保留 S202/7 recovery wrapper；不改傳送模組。切圖保留自身異常狀態，重連／切角 reset 清除。
- 提交合成封包／像素測試，接上 DemoChecks 及 Windows CI；更新 dev lock。未改 `docs/REPORT.md`、release、封存 lock 或遊戲資源。

## 怎麼驗證

已驗證：

- `rebuild.py --update-lock`：185 個來源、194 個鎖定檔核驗通過。
- `rebuild.py --build --configfile scratch/issue-388/nuget.local.config --output scratch/issue-388/build`：產品及 launcher 編譯通過，產品 0 錯誤、2 個既有 `MagicUi.cs:20` nullable 警告。
- `DemoChecks ... afflictions`：138 項檢查通過，涵蓋每個 S165／S202／S47 格式、截斷／補齊／不合法值的原子性、重疊鎖、解除恢復、傳送 recovery、切圖/reset、鍵盤／滑鼠／API 拒絕與排隊請求失效、像素遮蔽／陰影／共用來源保護及解除還原。
- `DemoChecks ... combat-admission`：278 項既有戰鬥檢查通過。
- `DemoChecks ... map-continuity`：358 項既有地圖／傳送／船與公告欄檢查通過。
- `check-no-game-assets.py`：通過，0 錯誤。
- `git -c core.whitespace=cr-at-eol diff --check`：通過（保留既有來源檔的 CRLF）。

產品 DLL SHA-256：`49eb655a891d5f4dd2e3c6f41c08fbc7cc1941cdf34a2be51ac8d302d265f536`。
CI 已加入 `Affliction contracts`；遠端結果以本 PR 的 `build` workflow（Windows／Linux 兩個工作）為準，完成摘要另列於 PR。

初次 NuGet 線上還原因 Windows TLS 認證錯誤失敗；改用原專案 `engine/vendor` 的既有套件唯讀還原，套件快取及暫存均位於本 worktree，未更動原專案。

## 未驗證什麼

- 未啟動遊戲、伺服器或 DB，未登入／送真實遊戲封包。
- 未驗證原生畫面與舊 BIN 調色盤逐像素一致性。綠色、灰色與盲目明度是依伺服器語意實作的呈現政策。
- 未量測真實地圖的盲目合成成本。盲目需要覆蓋完整 renderer Bounds，應由 smoke 觀察大地圖及船背景時的流暢度。

## 產出路徑

- 產品：`engine/source/Afflictions.cs` 與三個授權掛接檔。
- 測試：`engine/_integration/dev/afflictions-tests/AfflictionChecks.cs`。
- 協定：`docs/protocol/afflictions.md`。
- 本機建置證據：`scratch/issue-388/build/`；離線檢查結果：`scratch/issue-388/*.json`（不入 git）。
- 本報告：`docs/reports/issue-388.md`。

## 給下一棒的提示

由 Claude 審查 PR，使用者合併；dev lock 如遇其他任務同步衝突，由調度器重跑產生，不手拼 hash。合併後由 dot 在授權 P24Lab smoke：確認自身與周邊中毒／灰色及解除，六類鎖的移動／攻擊拒絕與恢復，盲目兩模式及切圖／重連，並觀測全圖盲目時的效能。狀態圖示不屬本張。
