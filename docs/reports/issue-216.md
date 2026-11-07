# Issue #216：T121 物品 status blob 語義化

## 做了什麼

- `ItemStatus.cs` 解碼武器／防具／一般物品、強化、損傷度、能力、元素與狀態抗性、Big5 說明；未知或截斷 tag 保留原始 hex、未解析尾段與錯誤位置。可交易／刪除／封印狀態使用背包旗標。
- S5／S15／S24／S242 更新同步解析屬性；`InventoryItem.Equipped` 改讀 S64 權威裝備槽位。背包 tooltip 顯示解析結果及槽位／待同步狀態。
- 依 2026-11-07 主控裁定，`ItemTransferChecks.cs` 只改裝備前置：S15 名稱改成一般「劍」，再送 `S64/66 D(itemId) C(slot=9) C(equipped=1)`。裝備中不可刪除／給予兩個斷言不變；未改 `ItemTransfer.cs`、`SupportedUse`。這項修改在先前的提交已完成，本輪確認後沒有再動。
- 本輪把 `task/216` 同步到最新 `origin/main`。main 在這段期間持續前進，共合併多次。衝突只有 dev lock、`build.yml`（各自新增的測試步驟，全部保留）與 `demo-tests/Program.cs` 的預設入口（全部保留）。lock 每次都用 `--update-lock` 重產，未手工拼接 hash；自動合併成功的那一次也重產，並確認文字合併的 lock 確實過期。
- 沿用 [PR #242](https://github.com/chaoren0418-arch/tw-bin-engine/pull/242)。本模組測試已提交並掛入 DemoChecks 與 Windows CI。

## 怎麼驗證（已驗證）

以下結果都在程式 head `6e26e36`（已含 main `a54971c`）取得。之後只再併入 main 的 `docs/REPORT.md` 索引 commit 與本報告，沒有程式變更。

- `rebuild.py --update-lock` 與唯讀核驗：203 個來源／212 個 pin 通過。
- `rebuild.py --build`：產品與 launcher 成功；產品 0 錯誤，2 個既有 `MagicUi.cs:20` nullable 警告。產品 DLL SHA-256：`cc433d5183e204553575124dd8e7138aa48310a25e7fa9be7cff22d4d790d2bc`。
- DemoChecks 各群組全部通過：item-status 275、item-transfer 266、map-continuity 21859、combat-admission 1326、polymorph-cadence 226、buddies 362、character-config 286、player-query 894、location-share 292、attack-skills 934、portals 293、doors 514、pets 193、ghost-mode 552、fishing 232、ambience 549、invisibility 263、duel 307、board 423、slow-actions 557、combat-loop 257、hit-reaction-suppress 259。每個群組的計數都含 DemoChecks 共用檢查，不能相加，也不是該模組的獨立案例數。
- 獨立測試專案全部通過：NPC 165、packet-trace 3468、login 396589、skill-cast 785、teleport 4369、status-icons 626、player-chat 107、effect6016 25、weapon-repair 98、item-use 426、character-management 218、ground 596、sound-packets 148、combat-sfx 8794，以及 ResourceChecks、WebView2 產品頁與 legacy 頁。ground、combat-sfx 唯讀載入本機資源，沒有真機操作。
- `check-no-game-assets.py`：3379 個追蹤檔、0 errors。
- 掃描 main 新合入的程式與測試：除本任務的負向測試和下述兩個舊 CLI 自測外，沒有其他地方用 `($9)`／`($117)` 名稱後綴模擬裝備。
- 新 head 的 GitHub Actions 結果於推送後確認，精確 head、run URL 與結論記在 PR #242 的 RESULT 留言，避免把歷史綠燈當成本輪證據。

## 未驗證什麼／已知問題

1. **`demo-all`（DemoChecks 無群組的預設入口）在 main 上會永久卡死，提交狀態下無法跑完。** 本分支未修，因為那是共用入口，不在本輪授權內。
   - 卡點：`PlayerQueryChecks`。`NativeTargetUi()` 只在預設入口執行，會讓主執行緒裝上 `WindowsFormsSynchronizationContext`；測試程式沒有訊息迴圈，所以 #357 加入的第一個真實 `await Task.Delay` 不會再恢復。
   - 已驗證：實測當時的 context 就是這個類型。暫時在該步驟前 `SetSynchronizationContext(null)` 後，`demo-all` 通過 2725 項。此修改未提交，只是診斷，所以這個數字**不是**提交狀態的結果。
   - 推測：main 本身同樣會卡，因為機制不涉及本分支的檔案；但沒有在乾淨的 main 上實跑。CI 只跑個別群組，所以綠燈看不出這個問題。
   - 建議修法：在 `NativeTargetUi` 結束時還原 context，或在預設入口的 await 前清除；並給 `demo-all` 設時限。
2. **`world-ui` 歷史測試失敗，不在 CI 內，與本分支無關。** 目前停在 `Program.cs:44` 的 `pending appearance clears selection`：#263 的 `ReceivePolymorph` 刻意保留目標、只提升 `TargetRevision`（`Polymorph.cs:40-41`），舊斷言已過時。測試遇到第一個失敗就中止，後面的斷言這次沒有執行，包含上一輪記錄的第 138 行聊天斷言（要求 `history.Text == "server echo"`，現行 `FormatLine` 會加頻道前綴），因此不能宣稱所有歷史測試全綠。
3. **`EquipmentTests.cs`、`EquipmentGateTests.cs` 的斷言已被本任務弄成過時。** 兩者只能由 `--equipment-tests`／`--equipment-gates` 進入，不在 CI 內。靜態判讀：它們用名稱後綴建立「已裝備」（`EquipmentTests.cs:32`）；或在沒有 S64 時要求 `Equipped==false`（`EquipmentTests.cs:31`、`EquipmentGateTests.cs:13`），現在會是 `null`。所需的 `fixtures/combat-initialization.json` 在 repo 與主專案都不存在，本輪和 main 都跑不起來，所以沒有實跑。兩個檔案不在本任務可改範圍，未修改。
4. **S64 完整清單到達前的空窗。** 這段時間 `Equipped` 為 `null`，`Equipped!=true` 的呼叫端（交易、存倉、給予、刪除）會放行裝備中的物品，只能靠伺服器拒絕。舊的名稱後綴邏輯在這段時間能擋下；這是改用權威槽位的取捨，尚未在真機確認空窗的實際長度。請 M6 smoke 留意。
5. 未啟動遊戲、登入、伺服器或 DB；真實 tooltip 排版、即時原服回包與音訊裝置仍待 M6 smoke。
6. 空 status 不推定零能力，未知 tag 不嘗試重新對齊。沒有獨立 tag 的元素強化種類／階級不從名稱推測，自由文字不冒充數值解碼。
7. 封存／check-template 舊測試、需真機或未追蹤 fixture 的歷史測試入口不作本次驗收證據；未執行會遞迴清理的測試腳本。

## 產出路徑

- 協定及唯讀來源 SHA：`docs/protocol/item-status.md`。
- 實作：`engine/source/{ItemStatus,Inventory,Equipment,InventoryUi}.cs`。
- 測試：`engine/_integration/dev/demo-tests/ItemStatusChecks.cs`、`Program.cs`；`engine/_integration/dev/item-transfer-tests/ItemTransferChecks.cs`；CI：`.github/workflows/build.yml`。
- 本報告：`docs/reports/issue-216.md`。
- 本機證據（未追蹤）：`scratch/issue-216/run-merge.ps1`、`merge-regressions.json`、`rebuild.log`、`demo-all-diag.out`／`.err`、各測試 JSON／log。

## 給下一棒的提示

- Claude：依 PR #242 的 RESULT 留言所列 head 與 CI 結果審查，再交使用者合併。main 更新很快，合併前若又落後，同步後只需重產 lock。
- 調度器：建議另開三張維護任務：修 `demo-all` 卡死（上述第 1 項，影響所有之後的完整回歸）；更新 `world-ui` 過時斷言（第 2 項）；更新或退役 `EquipmentTests`／`EquipmentGateTests`（第 3 項）。
- dot：M6 smoke 核對 tooltip、穿卸、重新登入的槽位同步，以及登入初期 S64 到達前的空窗。
- `Equipped` 是即時權威查詢，需要歷史快照的呼叫端應保存 bool? 值。worktree 與 assets junction 保留交調度器清理，禁止遞迴刪除。
