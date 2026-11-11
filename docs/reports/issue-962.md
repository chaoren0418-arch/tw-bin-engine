# Issue #962 報告：T420 R11-F 分開音量

依據：`docs/reference/gap-matrix.md` 15.13.5（4:OptionUI ③）、15.13.6（R11-F）；裁定：#952 第 1 點「照原版做，三條音量分開；刻度無法確定就照線性並標推定」。

## 做了什麼

- **OptionUI 三條音量捲軸各自獨立**（`engine/source/PlayerAudioSettings.cs`、`engine/source/RingWindows.cs` 的 `OptionWindowHost.Level`／`SetLevel`）：
  - 新增 `PlayerAudioSettings.BarCategory`／`BarLevel`／`SetBarLevel`，把捲軸對到 `lineage.cfg` 的類別音量（sound.md 4.1）：

    | 捲軸 | 類別音量 | 影響的聲音 |
    |---|---|---|
    | `MusicVolumeScrollBar` | `MusicVolume`（類別 3） | 背景音樂、登入／選角音樂（`BackgroundMusic`） |
    | `EffectVolumeScrollBar` | `SoundVolume`（類別 0） | 影格音效、S22 模式 0、`<`／`[` 單次音效、6016 特效音（`SoundGate`、`VoiceLevel`、`OneShotScale`） |
    | `EnvVolumeScrollBar` | `EnvSoundVolume`（類別 1） | `envsound.def` 環境音、`^` 循環、S22 模式 1（`MapEnvSound`、`MapAmbientSound.Scale`） |
    | `VoiceVolumeScrollBar` | 不接（3.81 沒有語音） | 照舊 Unhandled |

  - `OptionWindowHost` 改寫產品唯一一份類別音量 `form.SoundVolumes`（＝`ClientSoundScene.Volumes`）。改之前三條都寫主音量 `SetAudioSettings`。
  - 主音量（WinForms TrackBar、網頁面板）與 `CheckMute` 不變，還是另一層：最終音量＝類別音量 × 主音量。
  - 類別音量是設定值，跟聲音裝置有沒有開無關。所以聲音關閉時捲軸也會記下新值（以前會拒絕並記「未套用」）。
- **在播的聲音會跟著變**：各消費端每次更新都會重新讀類別音量，不用改後端。`BackgroundMusic.Update` 每次呼叫 `SetVolume`；`MapEnvSound.Update` 套淡入淡出音量；`MapAmbientSound` 每幀呼叫 `SetScale`；戰鬥音效每輪呼叫 `ApplyLevels`。所以 `ProductSoundBackends.cs`、`BackgroundMusic.cs` 不用改，也沒有改。
- **刻度**：照線性 0–1、0.01 一格（沿用 `SliderGain`／`SliderPosition`），標「推定」（`LScrollBar` 的範圍沒追）。
- **新測試** `engine/source/SplitVolumeTests.cs`（`[WorldModuleTest("split-volume")]`，自動發現）：
  - 對應表：三條捲軸對到三個不同類別，Voice 和其他捲軸不接。
  - 獨立性：只改一類，其餘不動；超界、NaN、Voice 一律拒絕、不改值。
  - 合成 OptionUI 版面：拖 Effect、點 Env 只改自己那一類；三個拇指各自畫在自己的值。
  - 產品宿主 `ClientForm.OptionWindowHost`：用 `GetUninitializedObject` 建立，不跑建構子；改的是 `ClientSoundScene.Volumes`，測完還原。
  - 消費端：登入音樂音量＝MusicVolume × 主音量，Music 捲軸改了下一次更新就生效；Effect／Env 不影響音樂。`SoundGate` 的效果音只看 SoundVolume，環境音只看 EnvSoundVolume。

## 怎麼驗證

- 編譯：`dotnet build engine/source/OfflineClient.csproj -c Release` 成功，0 錯誤。剩下的警告是 CS8600／CS8602，都不在本次改的檔案。
  - 本機環境備註（和程式無關，同 #675／#676）：直接 restore 會出現 NETSDK1130。做法是 `scratch/t962/NuGet.Config` 唯讀指向主專案 `engine/vendor`，`NUGET_PACKAGES` 指到 `scratch/t962/packages`；只刪了 `engine/source/obj` 下 5 個 NuGet 輸出檔（逐檔刪，沒有遞迴刪除）。
- `py -3 engine/tools/check-no-game-assets.py` → `pass`（errors 0）。
- 單元測試與本組離線測試：依本機限制不在本機執行，交給 PR 的 GitHub Actions CI（`--module-tests` 會自動發現 `split-volume`）。結果以 PR 的 CI 為準。

## 未驗證什麼

- **推定**：捲軸刻度是線性。另外「Effect＝SoundVolume、Env＝EnvSoundVolume」這個對應是依控件名和設定鍵名推定的，`sub_919F30` 的寫入目標沒在 IDA 追。
- **沒做（範圍外）**：類別音量存檔。原版寫回 `lineage.cfg`（`814020`），引擎目前的 `OptionSettingsFile` 只存 bool，主音量也沒存檔，所以重開客戶端後三類會回到 1。
- `OptionPanel.MasterVolumeBars` 這個名稱已經不貼切（現在是「三條類別音量捲軸」），但 `NativeUi/OptionPanel.cs` 不在可改範圍，沒有改名；`OptionFollowupTests` 的替身宿主仍用舊規則，跟產品無關，也沒改。
- `RingWindows.cs` 除了 `Level`／`SetLevel`，只更新了 `OptionWindowHost` 類別說明註解的一行（讓註解跟行為一致），沒有改任何程式碼。
- **待真機驗證（交給 Codex／dot）**：進世界後開選項窗（Ctrl+V）→ Application 頁，分別拖三條捲軸：
  1. 拖音樂捲軸只有背景音樂變大小；
  2. 拖效果捲軸只有打擊／魔法音效變大小；
  3. 拖環境捲軸只有環境音、`^` 循環音變大小；
  4. WinForms 主音量和靜音仍對三類都有效。

## 產出路徑

- `engine/source/PlayerAudioSettings.cs`、`engine/source/RingWindows.cs`（只有 `OptionWindowHost`）、`engine/source/SplitVolumeTests.cs`（新）
- `docs/reports/issue-962.md`（本報告）
- 分支：`task/962`

## 給下一棒的提示

- 如果要做類別音量存檔：`OptionSettingsFile` 只支援 bool，要另外加數值鍵；或者新增引擎版的 `lineage.cfg` 讀寫（`SoundVolume=`、`EnvSoundVolume=`、`VoiceVolume=`、`MusicVolume=`，sound.md 4.1）。接點在 `PlayerAudioSettings.SetBarLevel`。
- 如果之後要改名 `OptionPanel.MasterVolumeBars`，記得同步改 `OptionFollowupTests.cs` 第 127 行的斷言字串。
- 要確定刻度：去 IDA 追 `sub_919F30` 綁定 `MusicVolumeScrollBar` 等控件的回呼，以及 `LScrollBar` 的 min／max。
