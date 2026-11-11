# Issue #908 — T393：R10-I 選項窗收尾（code，第 1 次）

依據：`docs/reference/gap-matrix.md` 15.12.6 R10-I 列、15.12.5 的 4:OptionUI 列；FU9§14.4（`followups-9.md`）。

## 做了什麼

| 項目 | 做法 | 檔案 |
|---|---|---|
| ① `EnableElfTelControl` 存檔 | 新增 `OptionSettingsFile`（引擎使用者資料夾 `option-ui.txt`，一行一個 `key=0/1`，和 `name-display.txt` 放同一個資料夾）。`ClientForm.SyncElfTeleportOption`（書籤計時器每次呼叫）第一次呼叫時載入 `elf-teleport-control`；選項窗勾選時存檔 | `NativeUi/OptionPanel.cs`、`RingWindows.cs`、`ObjectRendering.cs`（只加設定檔路徑） |
| ① Lv<50 清成 0 | `WorldSession.ClearElfTeleportOptionBelowLevel()`：收到自己的狀態、等級 < 50 就清成 0，不看職業（0xEC99EB／0xEED7B9）。計時器每次檢查，清掉就存檔；Lv<50 時勾選會立刻被清回 0，回報「未套用」 | `RingWindows.cs` |
| ② `ShowDropItemNameBtn` | `ClientForm.ShowDropItemNames`（預設開，也就是引擎原本的行為）。關掉時地面物品只畫選取中那一個的名稱（`GroundNameShown`）。存 `show-drop-item-names` | `ObjectRendering.cs`、`RingWindows.cs` |
| ② `TransparentDeadMonsterBtn` | `Renderer.TransparentDeadMonsters`（預設關）。開啟時，死亡怪物的身體和影子用 alpha 16/31 畫：同一組 commands 拿掉屍體再組一次，兩張用既有的 `FadeMix` 混合（和淡入淡出同一種做法）。哪些算怪物：他人、不是門（gfx 2346）、名稱以 `$` 開頭（伺服器 NPC 封包的 nameid）。存 `transparent-dead-monsters` | `ObjectRendering.cs`、`RingWindows.cs` |
| ③ 音量捲軸 | 水平 `ScrollBar` 加上 `Move_Button` 會建成新的 `Slider` 元件。拇指位置＝`ScrollStart`＋值×(`ScrollWidth`−拇指寬)；按下時就把值設在指標位置，拖曳時（拖出捲軸外也算）跟著指標走。Music／Effect／Env 三條共用引擎唯一的主音量（`SetAudioSettings`，聲音關閉時不套用，會記一行「未套用」）。值以 0.01 為一格（`PlayerAudioSettings.SliderGain`／`SliderPosition`，和 WinForms TrackBar 一樣）。Voice（3.81 沒有語音聊天）、WindowAlpha 沒接，維持 Unhandled | `NativeUi/OptionPanel.cs`、`PlayerAudioSettings.cs`、`RingWindows.cs` |
| 離線測試 | 新檔 `OptionFollowupTests.cs`（`[WorldModuleTest("option-followup")]`，自動發現）：設定檔格式、Lv49／Lv50 規則、兩個顯示開關、捲軸數學與拖曳、屍體判斷與 FadeMix、產品型別 | `OptionFollowupTests.cs` |

`IOptionWindowHost` 新增兩個預設方法 `Level`／`SetLevel`（預設回 null／false），所以既有實作不必改。

**範圍外的修改（必要）**：`NativeUi/OptionWindowTests.cs` 改了 2 行。原本的斷言要求 `ShowDropItemNameBtn`／`TransparentDeadMonsterBtn` 列在 `OptionPanel.NotWired`；這兩項本任務已經接上，舊斷言一定會失敗，所以改成「列在 `DisplayEvents`」。`NotWired` 現在只剩 `VoiceVolumeScrollBar`。

沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。`--classic-ui` 時原生選項窗不會建立，但妖精選項仍會從設定檔載入並套用等級規則；WinForms 入口沒有改。

## 怎麼驗證

- **已驗證**：本機 `dotnet build engine/source/OfflineClient.csproj -c Release` 成功，0 錯誤；3 個警告都是既有的（`HtmlActionTests.cs:108`、`MagicUi.cs:20`），新程式沒有警告。本機 restore 會遇到 WebView2 NETSDK1130 環境問題（同 #607／#675／#676），處理方式：只刪 `engine/source/obj/` 下 5 個 NuGet 輸出檔（逐檔刪，沒有遞迴），用 `scratch/t908/NuGet.Config` 以唯讀方式指向主專案的 `engine/vendor`，並設 `NUGET_PACKAGES=scratch/t908/packages`。這些都沒有進 git。
- **已驗證**：`engine/tools/check-no-game-assets.py` 結果是 `pass`（4480 檔，0 錯誤）。
- **待 CI**：`--module-tests` 裡的 `option-followup`、`option-window` 和其餘既有測試。依本機限制，本機沒有執行任何編譯產物，結果以 PR 的 CI 為準。
- 資源：本任務沒有新增圖或文字的使用，所以沒有「缺資源」的宣稱。捲軸圖 10834／10835 照舊經 `OptionPngSource` 取得（本機 Sprite 找不到時用參考客戶端，T330／#768 已查）。版面元素名稱對照的是 #768 留下的 kor25 `OptionUI.xml`（`scratch/wt/task-768/scratch/t768/OptionUI.xml`，沒有進 repo）。

## 未驗證／推測（真機再確認）

- 地面物品名稱開關關閉時，原版怎麼顯示沒有追（例如是否滑鼠移上去才顯示）；引擎目前是只顯示選取中的那一個。
- 透明屍體的 alpha（16/31）、原版判斷「怪物」的方法（引擎用 `$` 開頭的名稱判斷）都是推測；畫面效果要在真機確認。另外，開啟這個選項時，畫面上有屍體就會多組一次畫面，效能也要在真機觀察。
- 音量捲軸：`LScrollBar` 用在這幾條的範圍沒有追，引擎假設是線性 0–1。三條捲軸對到同一個主音量，是引擎的簡化做法（引擎沒有分開的音樂、效果、環境音量）。
- 原版設定檔（`sub_59DD30`／`sub_59E920`）的格式和存放位置沒有重現，引擎用自己的 `option-ui.txt`；是否依帳號或角色分開存也沒有追，引擎是全域一份。
- 勾選框的「已勾」外觀沿用 R9-Q 的推測（畫 PushPng）。

## 產出路徑

- `engine/source/NativeUi/OptionPanel.cs`、`engine/source/RingWindows.cs`、`engine/source/ObjectRendering.cs`、`engine/source/PlayerAudioSettings.cs`、`engine/source/OptionFollowupTests.cs`（新檔）、`engine/source/NativeUi/OptionWindowTests.cs`（2 行）
- 本報告：`docs/reports/issue-908.md`
- 分支：`task/908`

## 給下一棒的提示

- R10-J（書籤窗收尾）排在本張之後：本張在 `RingWindows.cs` 只動了選項相關的部分（`OptionWindowHost`、`SyncElfTeleportOption`、`ClearElfTeleportOptionBelowLevel`），`AcceptsRandomTeleport` 還沒碰。
- gap-matrix 15.12.5 的 4:OptionUI 列，①②③ 可以改成「已做（資料／code），待真機」；1:122 的「妖精選項存檔」也已完成。
- 真機驗收（交給 Codex／dot）建議步驟：
  1. 開選項窗（Ctrl+V），在 Application 頁拖音樂捲軸，確認 WinForms 主音量跟著變、實際音量也有變化。
  2. 勾掉「掉落物名稱」，確認地上的物品名稱消失、選取的那一個仍然顯示。
  3. 勾選「透明屍體」，打死一隻怪，確認屍體變成半透明。
  4. 用 Lv<50 的角色勾妖精選項，確認勾不起來；用 Lv≥50 的妖精勾選後重開客戶端，確認選項仍在。
- 如果之後要把音量拆成「音樂／效果」兩路，接點在 `ClientForm.OptionWindowHost.Level`／`SetLevel`（`RingWindows.cs`）。
