# Issue #987 報告：T430 R12-C 類別音量存檔

依據：`docs/reference/gap-matrix.md` 15.14.5「4:OptionUI」列、15.14.7 第 1 點（裁定：照原版做，類別音量要存檔）。

## 做了什麼
- 新檔 `engine/source/AudioVolumeFile.cs`：引擎自己的類別音量檔 `runtime/user/sound-volume.cfg`（位置同 `summon-quick.json`）。
  - 一行一個 `鍵=值`，鍵名照 `lineage.cfg`：`SoundVolume=`、`EnvSoundVolume=`、`VoiceVolume=`、`MusicVolume=`（依類別 0..3，sound.md §4.1）。
  - 值是 0–100 的整數。引擎的類別音量是 0..1、以 0.01 為一格，所以存回讀出逐值相同（測試逐格核過捲軸 0..210）。
  - 讀檔：檔案不存在就什麼都不改（四類維持 1）；某一行壞掉或超界只略過那一類；不認得的行、註解都忽略；鍵名分大小寫；同一鍵以最後一次為準。
  - 寫檔：先寫 `.tmp` 再取代；內容和上次寫的相同就不寫（拖捲軸時只有跨 0.01 才寫一次）。讀寫失敗只寫一行 `UiResourceLog`，不擋遊戲。
- `engine/source/PlayerAudioSettings.cs`：
  - **啟動時讀**：ClientForm 的欄位 `audioSettings=new()` 會呼叫無參數建構子。只有在正常產品入口（entry assembly 是 `P11.ClientEngine`，而且 `ProductEntry.Normal` 判定為正常入口；`--native-ui`／`--classic-ui` 先拿掉）才接上 `AudioVolumeFile.DefaultPath`，把檔案讀進產品唯一的一份 `ClientSoundScene.Volumes`。`--module-tests`、診斷入口、引用產品 DLL 的檢查程式都不讀也不寫。
  - **捲軸改了寫**：`SetBarLevel` 改的是 `ClientSoundScene.Volumes`、而且已經接上檔案時，才呼叫 `Save`。`RingWindows.cs` 的 `OptionWindowHost.SetLevel` 本來就走 `SetBarLevel`，**不用改**。
  - 新增 `PlayerAudioSettings(AudioVolumeFile?)` 建構子與 internal `VolumeFile`／`ProductLaunch()`，給測試用。
- 新檔 `engine/source/AudioVolumeFileTests.cs`：`[WorldModuleTest("audio-volume-file")]`，用自動發現註冊。測試內容：格式、解析容錯、0.01 一格的往返、檔案往返、內容沒變就不重寫、產品路徑（讀進共用那一份、Music／Effect／Env 寫回、Voice 保留原值、拒絕的設定不寫、改別份 `SoundVolumes` 不寫、沒接檔就不寫），以及 `--module-tests` 不會接檔。測試只寫在建置輸出目錄下的 `module-test-audio-volume\`，測完逐檔刪掉（不遞迴），共用狀態也會還原。
- **不讀也不寫正式客戶端的 `lineage.cfg`**（CLAUDE.md 規則 7）。我唯讀看過 `D:\lineage開發\0-客戶端\lineage.cfg`：315 bytes，開頭是 `lineage configuration file` 的二進位檔，不是 `鍵=值` 文字檔。所以引擎的檔案只沿用鍵名，**不是**原版的格式。

## 怎麼驗證
- 已驗證（CI，PR #997，run 42968048078）：dotnet、map-continuity、npc-offline 三個 job 全部 pass；`--module-tests` 裡 `audio-volume-file` 為 pass（29 checks，0 failures），整組 `failed: 0`（既有的 split-volume 等測試也是綠的）。
- 已驗證：`engine/tools/check-no-game-assets.py` 的結果是 `status: pass`，errors 0。
- 已驗證：只改了任務列出的三個檔案（`git diff --stat origin/main...HEAD`）；沒改 `rebuild-lock.json`、`RingWindows.cs`、`Program.cs`、註冊表。
- **本機沒編譯成功**：`dotnet build source/OfflineClient.csproj` 和 `rebuild.py --build --configfile .github/nuget.ci.config` 都卡在 `NETSDK1130: Microsoft.Web.WebView2.Core.winmd 無法參照`。這是本機 SDK／套件環境的問題，在編譯任何 C# 之前就失敗了，跟本次改動無關。所以編譯與測試以 PR 的 CI 結果為準。
- 依規定沒有在本機執行任何編譯出來的程式（包含 `--module-tests`）。

## 未驗證什麼
- 「推測／引擎取捨」：值用 0–100 是推定的（sound.md 第 256 行寫 `MusicVolume / 100`）；原版 `sub_814800`／`sub_814020` 的欄位格式沒有逐 byte 對照。
- 「待驗證（真機）」：正常入口重開客戶端後，三條捲軸和實際音量要回到上次的值，`runtime/user/sound-volume.cfg` 也要真的產生。這部分交給 Codex／dot 用 P24Lab 驗收。
- 存檔只在正常產品入口生效；`--demo`、`--evidence` 這類診斷入口不會存，這是刻意的設計。
- 寫檔的時機是捲軸每次改值（內容有變才寫），不是像原版那樣（推定）在關窗或結束時才寫。

## 產出路徑
- `engine/source/AudioVolumeFile.cs`、`engine/source/AudioVolumeFileTests.cs`、`engine/source/PlayerAudioSettings.cs`
- 本報告 `docs/reports/issue-987.md`
- 分支 `task/987`

## 給下一棒的提示
- 本機 `dotnet build` 會遇到 NETSDK1130（WebView2 winmd），值得開 `needs:orchestrator`／`stage: resource` 查本機 NuGet 快取或 SDK 的差異（CI 在 ubuntu 上建置不受影響）。
- 真機核對清單：① 正常入口開 OptionUI，把 Music 拉到約 30%、Effect 拉到 0；② 關掉客戶端；③ 確認 `engine/build/client-product-dev/../../runtime/user/sound-volume.cfg`（也就是 `engine/runtime/user/`）的內容是 `MusicVolume=30`、`SoundVolume=0`；④ 重開後捲軸位置與音量跟關掉前一樣。
- 如果之後要做 VoiceVolume 捲軸，檔案已經會讀寫 `VoiceVolume=`，只要在 `BarCategory` 加上對應即可。
