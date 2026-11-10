# Issue #780 — T334：R6-D 鍵盤拾取與幽靈快捷

依據：`docs/reference/followups-7.md` 第 8 節 R6-D；WO§2、§4；UI§2 V5 (b)；第 5 節 `sub_5001C0`、`byte_13CE51E`。

## 做了什麼
1. **F4 鍵盤拾取（`sub_500640`）**
   - `Ground.cs`：新增純函式 `GroundRules.LootTarget`（1 格內含斜角、數量 > 0、照 ObjectId 取第一個）；`LoginConnection.LootNearby` 改用它。轉向（`FacePickupTarget`）、整疊、不問數量、不走近、986／600 ms 節流都沿用和點擊相同的 `SendGround`，沒有另開路徑。
   - `PlayerHudIntegration.cs`：`TryPlayerHudKey` 在世界中（舊 HUD 或原生 UI 都一樣）把 F4 交給 `LootKeyFromInput`。原本 F4 在舊 HUD 被吞掉、在原生 UI 被 `Program.cs` 的 F1–F12 規則丟掉，現在不用改 `Program.cs`。
   - `OnlineInput.cs`：新增 `PlayerLootKeyActive`／`LootKeyFromInput`／`LootFromInput`，行為與 P 鍵一致：自動重複不重送（`Input.Held`）、異常狀態擋下、走路中不送、未送出時顯示 `GroundNotice`。
2. **幽靈中擋巨集格（`byte_13CE51E`，WO§4）**
   - `PlayerHudModel.cs`：`PlayerHudActions.Activate` 開頭加 `GhostBlocks`（`GhostLocalBlocked`）。舊 HUD 的 F5–F12、格子雙擊，以及原生 `MacroPanel` 的 F5–F12、雙擊（`MacroBook.TryActivate` → `ActivatePlayerHud`）都經過這裡，所以一處就全擋；不選取、不送封包。
   - `PlayerHudIntegration.cs`：幽靈中未送出時提示改用 `GhostLocalLimits.Notice`。
3. 新測試 `engine/source/PickupGhostFollowupTests.cs`（`[WorldModuleTest("pickup-ghost-followup")]`，自動發現）：LootTarget 規則；F4 不走近、取第一個、整疊、不跳提示、先轉向、腳下不轉向、幽靈不拾取；點擊↔F4 共用 986 ms 節流（雙向）、連按不排隊、S139 旗標 600 ms；幽靈中道具／技能格都不送、不改選取。

沒有改 `WorldSession.cs`（只在既有的 `Ground.cs` partial 內）、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

## 怎麼驗證
- 已驗證：`dotnet build engine/source/OfflineClient.csproj -c Release` 0 錯誤（本機用 scratch 的 NuGet.Config 指向主專案 `engine/vendor`，避開已知的 WebView2 NETSDK1130 本機還原問題，同 #675／#676）。
- 已驗證：`engine/tools/check-no-game-assets.py` → pass。
- 待驗證（CI）：依本機規定不在本機執行測試；既有單元測試與本組 `pickup-ghost-followup` 交給 PR 的 GitHub Actions。

## 未驗證什麼
- 真機：F4 實際拾取、轉向動畫、幽靈中按 F5–F12 的畫面提示（交給 Codex／dot 在 P24Lab）。
- 推測：原版可見物件的掃描順序（目前用 ObjectId）；牆阻擋 bit 0（Q6-7）未做；`byte_13CE51E`＝1 的語義（Q6-8）未處理，只用幽靈旗標 0x80。
- F4 在原生 UI 是否還有其他面板先吃掉（`NativeUiKey` 目前沒有面板處理 F4，靜態閱讀確認）。

## 產出路徑
- `engine/source/Ground.cs`、`OnlineInput.cs`、`PlayerHudIntegration.cs`、`PlayerHudModel.cs`
- `engine/source/PickupGhostFollowupTests.cs`
- 本報告 `docs/reports/issue-780.md`

## 給下一棒的提示
- Q6-7（牆阻擋）查到後，只要在 `GroundRules.LootTarget` 加地格判斷即可，點擊路徑另在 `ApproachRules`。
- Q6-8 若確認值＝1 也要擋，改 `PlayerHudActions.GhostBlocks` 一處。
- `NativeUi/State/MacroState.cs:83` 的註解「F4 is pickup」現在已名符其實，`MacroInventory.Busy` 不需要再加幽靈條件。
