# #969 T425：Q8-B② WidgetSlot 模式切換

## 做了什麼
- 依 FU15§2.4 與 FU17§2.1–2.2（#966 Q9-3）：元件 2＝MiniMapUI → `MINI_MAP_WINDOW`，元件 4＝WidgetSubSlot → `EX_MACRO_WINDOW`。模式 0／1＝在 `BAR_485` 槽位群組顯示其中一個。
- `NativeUi/MacroPanel.cs`：
  - 新增 `WidgetSlotMode`（`MemberOf`／`Of`／`Apply`／`Toggle`／`Restore`）與 `IWidgetSlotModeStore`。原本的 `WidgetSlotLayout.ModeChangeNotWired` 移除。
  - `MacroPanel` 註冊時多帶 `root.SlotGroup(BAR_485)`：
    - 主欄 ButtonMode 在**放開**時切換（514＝WM_LBUTTONUP）；在別處放開只結束按下狀態。
    - **Alt+R** 切換：key-up 與自動重複都吞掉；不要求 `InputAllowed`，因為原版編輯框路徑也有這個鍵。
    - 進世界時（InWorld 由 false 變 true）還原存檔的模式（sub_59DD30）。
  - 以上都只在有 `WidgetSlot.xml` 時生效。沒有這個檔（uistatus86／3.81 外觀）時行為不變，Alt+R 也交還給遊戲。
- `NativeUi/ExMacroPanel.cs`：副欄 ButtonMode 放開時一樣切換（在模式 1 才看得到，按了回到模式 0）。
- 存檔：用引擎自己的 `option-ui.txt`（`OptionSettingsFile`），key 是 `widget-slot-mode=0|1`。原版是選項位元的 bit 0（sub_997660），原版設定檔格式沒有重現，做法同 R10-I。
- 目前顯示的是隊伍 RT 時當成模式 0，所以下一次切換會到模式 1（推測，FU17§2.3）。Alt+F3 循環不變。群組裡沒有目標成員時不切換、不存檔，只記一行 log。
- 新檔 `NativeUi/WidgetSlotModeTests.cs`（`[WorldModuleTest("widget-slot-mode")]`，自動發現）：
  - 涵蓋對應、切換、寫入失敗、缺成員、還原、主欄／副欄按鈕（按下不切、放開才切）、Alt+R（重複／放開／其他修飾鍵）、無 WidgetSlot.xml、無群組、註冊。
  - 全部用合成 XML／圖與記憶體存放處，不寫檔。

## 怎麼驗證
- `dotnet build engine/source/OfflineClient.csproj`：建置成功，0 錯誤；只有 3 個原本就有的警告，不在本次改的檔案。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass。
- 離線測試：依本機限制**沒有在本機執行**，交給 PR 的 GitHub Actions CI（`--module-tests`）。
- 既有的 `WidgetSlotTests.cs:148–149` 沒改，照設計應該仍然通過：
  - 那裡的 MacroPanel 沒有群組，按下時記的 log 含 `ModeChange` 與 `M30`；
  - 頁面與鎖定都不變。
  - SubWidget 的群組只有 EX，切換找不到小地圖，所以不切換，原有斷言不受影響。

## 未驗證什麼（待驗證，交給 Codex 真機）
- 真機上 Alt+R 在聊天輸入框有焦點時能不能到達 MacroPanel：要看焦點面板會不會先吃掉這個鍵。
- 切換後的實際外觀（M30）、主欄與副欄的 ButtonMode 在同一個位置 (637,498) 重疊時由哪一個收到點擊（兩個結果相同）。
- 隊伍 RT 當成模式 0 是推測。
- 原版選項檔的格式沒有重現。
- 沒有新的資源宣稱（沒有說「缺圖」），所以這次不需要 PakViewer 查詢。

## 範圍說明
- FU17§7 建議的範圍還包括 `UiSlotGroup.cs` 與修改 `WidgetSlotTests.cs:148–149`。本 Issue 只開放 MacroPanel／ExMacroPanel／新測試檔，所以那兩個檔都沒動，二選一的邏輯放在 `WidgetSlotMode`（MacroPanel.cs）。
- 沒改 `rebuild-lock.json`、`Program.cs`、`DemoChecks.csproj`、各註冊表。

## 產出路徑
- `engine/source/NativeUi/MacroPanel.cs`、`engine/source/NativeUi/ExMacroPanel.cs`、`engine/source/NativeUi/WidgetSlotModeTests.cs`
- 本報告 `docs/reports/issue-969.md`

## 給下一棒的提示
- `WidgetSlotTests.cs:148–149` 的標籤仍寫「ModeChange not wired」，實際上現在驗的是「沒有群組時不切換」。之後有權限改那個檔時，可以把標籤改掉，或把該段移到 `WidgetSlotModeTests`。
- 真機驗收步驟（需要參考客戶端提供 WidgetSlot.xml）：
  1. 進世界，按主欄 ButtonMode，確認小地圖換成擴充快捷欄；
  2. 按副欄 ButtonMode 回到小地圖；
  3. Alt+R 一樣能切換；
  4. 重新登入後還原上次的模式；
  5. 檢查 `option-ui.txt` 有 `widget-slot-mode`。
