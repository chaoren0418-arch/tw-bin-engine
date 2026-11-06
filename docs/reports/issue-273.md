# Issue #273 — T133：刪除物品與給予物品

## 做了什麼

- 新增 `engine/source/ItemTransfer.cs`：C138 整疊刪除確認、C45 給予編碼、原生背包拖放／垃圾桶、確認快照與送出前失效檢查。
- 數量沿用原生背包既有欄位與 `GroundCatalog.TryCount`；給予確認顯示數量／目標，刪除明示整疊並預設取消。
- `InventoryUi.cs` 僅增加一行模組初始化。未修改 `InventoryItem.SupportedUse`、`ReadItem`、接收分派或其他產品模組；沿用 S24／S57／S71 更新及顯示，不預扣背包。
- 提交 `engine/_integration/dev/item-transfer-tests/ItemTransferChecks.cs`，接入 DemoChecks `item-transfer` 群組及既有 Windows CI；dev lock 由 `--update-lock` 產生。

## 怎麼驗證（已驗證）

- Windows .NET SDK 10.0.300：`rebuild.py --update-lock` 後完整 `--build` 成功。產品 0 錯誤，既有 `MagicUi.cs:20` 兩項 nullable 警告；launcher 0 警告／0 錯誤。
- 本機 NuGet 網路還原遇到 TLS 認證錯誤；以 `scratch/issue-273/NuGet.Config` 指向原專案唯讀 `engine/vendor` 完成還原，套件快取及所有建置／暫存輸出均在 worktree。未複製遊戲資源。
- 離線合成封包與記憶體 sender：**73 項通過**。C138／C45 完整 bytes 與 padding、數量界限、確認取消／重複、裝備限制、2 格距離、背包／選取／目標／世界／連線失效、傳輸鎖內重驗、權威回包早於 sender 完成、S24／S57／S71、失敗 token 不可再用、舊連線完成隔離、傳輸失敗不重送。測試入口有 socket guard，未啟動遊戲。
- 測試命令：`dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-273/item-transfer.json item-transfer`。
- `check-no-game-assets.py` 通過：3265 個追蹤檔、0 錯誤、4 個核准 EmbeddedResource。`git diff --cached --check` 通過。
- 產品 DLL SHA-256：`d67b24010d717ed673ac5123a30745e2385bd99127a05b368d30d0b2f49f8c24`。
- GitHub CI 於 PR 建立後檢查；結果另附 PR 留言與最終 RESULT。

## 未驗證什麼

- 未啟動遊戲、伺服器或 DB；未做原生畫面／實際拖放／登入驗收。合成封包測試不代表伺服器已接受轉移。
- 未讀物品白名單或 NPC 模板配置。C45 是否收物由伺服器決定；原處理器只接受可收物的 NPC 類實例，不保證所有世界物件皆可收物。
- 未擴充 Web 背包或 HUD 快捷欄拖放；入口為既有原生「更多 → 背包」。

## 產出路徑

- `engine/source/ItemTransfer.cs`
- `engine/_integration/dev/item-transfer-tests/ItemTransferChecks.cs`
- `docs/protocol/item-transfer.md`
- 本報告；測試入口／CI／dev lock 的必要掛接。
- 未追蹤證據：`scratch/issue-273/build/`、`scratch/issue-273/item-transfer.json`。

## 給下一棒的提示

由 Claude 審查 PR，使用者合併。合併時 dev lock 衝突交調度器重新產生，勿手動拼 hash。dot 於後續 P24Lab smoke 依常設授權驗證：原生背包輸入數量後拖到 2 格內 NPC／寵物、拖空地取消、垃圾桶取消／確認、整疊數量、禁刪／禁轉移訊息、確認期間目標移動或物品變更，以及既有使用／丟棄與行走無回歸。正式服務與正式 DB 不碰。
