# #1045 T460 音效：Sound 改由 TW_CLIENT_ROOT 直讀，audio-manifest 不再是檔案

依 `docs/design/asset-recipe.md` 3.6「音效」與 `docs/design/portable-delivery.md`「本機生成與啟動」第 5 點。採**直讀加程式 pin**（asset-recipe 第 396 行的定案），不由準備器產生 audio-manifest。

## 做了什麼

- `engine/audio/AudioAssets.cs`
  - 拿掉 `Load(root, manifestSha256)`（讀 `assets/audio-manifest.json`、驗整檔 SHA、schema `p24-audio-assets-v2`）。
  - 新增程式內 pin `Effect6016Pins`：3332、1340 各一列 mp3（不可用，size／sha 為 null）與一列 wav（大小與 SHA-256 和舊 manifest 逐字相同：3332 = 24748 bytes／`973e0a35…eff2a`，1340 = 14322 bytes／`30b4ce04…f9c3`）。路徑一律是相對的 `Sound/<id>.<ext>`。
  - 新增 `LoadEffect6016(clientRoot)` 與 `FromClientRoot(clientRoot, pins)`：根目錄不存在或空字串→建立失敗；pin 路徑不是 `Sound/<id>.<ext>`、SHA 格式錯、某個 ID 缺 mp3 或 wav 宣告→拒絕。
  - `Acquire` 和 `AudioCore.Play` 都沒有改，所以這些既有行為不變：載入順序 mp3 → wav、MP3 不可用記 `SourceUnavailable` 後 WAV fallback、MP3 缺檔／讀不到時 fallback、缺檔回 `FileMissing`、pin 不符回 `IntegrityFailure` 且不 fallback。
  - 新增契約版本字串 `Effect6016Contract = "p24-audio-assets-v3-client-sound"`。
- `engine/audio/AudioPlayer.cs`：`TryCreate(clientRoot, contract, capacity, …)` 的第二個參數從 manifest SHA 改成版本字串，和 `Effect6016Contract` 不同就拒絕建立。新增 `Effect6016Contract` 公開常數，以及測試用的 `NativeForTests(assets, capacity)`（DirectShow 鏈相同，catalog 由測試提供）。
- `engine/source/AudioApiTransport.cs`：`ManifestPin`（舊 manifest 的 SHA `87c6cd68…`）改成 `AudioContract`（版本字串）；`Create` 收的參數改成客戶端根目錄。
- `engine/source/ClientEffect6016.cs`：6016 transport 工廠收的參數從 `World.Assets.Root` 改成 `TW_CLIENT_ROOT`（未設定時用 `ProductSound.DefaultClientRoot`，和 SoundPackets／combat SFX 相同）。`maps/effects/6016.json` 仍從 assets 讀，這不在本張範圍。
- `engine/audio/P24.Audio.csproj`：加上 `InternalsVisibleTo` DemoChecks 和 Effect6016Checks（測試要用合成 pin 和假驅動）。
- 新的 `[CheckGroup("audio-provider")]`：`engine/_integration/dev/audio-provider-tests/AudioProviderChecks.cs`（自動發現，沒有改 DemoChecks.csproj 或 Program.cs）。CI 的 `build.yml` map-continuity job 加了一步執行這個群組。
- `engine/_integration/dev/effect6016-tests/Program.cs`、`README.md`：Native 模式改成產生合成客戶端根目錄 `Sound/<id>.wav`，不再產生 audio-manifest；會先檢查產品工廠的契約和根目錄（舊版本字串要被拒），再用合成 pin 走同一條 DirectShow 鏈播放。

## 怎麼驗證

- 本機只做編譯（依本機規定，不執行測試）：`OfflineClient.csproj`、`DemoChecks`、`Effect6016Checks`、`CombatSfxChecks`、`SoundRulesChecks`、`SoundPacketChecks` 都是 Release 建置成功，0 個錯誤。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass（tracked 4682 檔，0 個錯誤）。沒有提交 WAV，合成 WAV 只在執行時寫到 `scratch/issue-1045/run-<時間>/`。
- 讀取比對（唯讀）：原客戶端 `D:\lineage開發\0-客戶端\Sound\1340.wav`、`3332.wav` 的 SHA-256 和大小，與主目錄 `engine/assets/audio-manifest.json`（SHA `87C6CD68…`）的 pin 相同，所以改成程式 pin 後內容沒有變。原客戶端 Sound 目錄沒有 1340.mp3、3332.mp3。
- `audio-provider` 群組由 CI 執行，涵蓋以下情境，全部用合成 WAV 和假驅動，不開裝置：
  1. 程式 pin 的契約：兩個 ID、MP3 不可用、WAV pin 與舊 manifest 相同、沒有絕對路徑、host 和 audio 的版本字串一致。
  2. 正常：MP3 不可用後 WAV fallback，開啟 `<root>/Sound/<id>.wav`，狀態為 `StartedWavFallback`。
  3. 缺檔：回 `FileMissing`，不開驅動。
  4. pin 不符：WAV 不符回 `IntegrityFailure`；MP3 不符也不 fallback 到 WAV。
  5. fallback：MP3 有 pin 但缺檔時退到 WAV；MP3 存在且相符時優先播 MP3。
  6. 產品 pin 套到合成根目錄：非原檔的 3332 被拒、缺少的 1340 回 `FileMissing`、其他 ID 回 `IdNotAllowed`。
  7. 拒絕情境：根目錄不存在或為空、路徑大小寫不對或是絕對路徑、缺 mp3 宣告、SHA 格式錯、舊版本字串 `p24-audio-assets-v2`。工廠能以正確契約建立播放器並正常釋放。整個流程不需要也不會產生 audio-manifest.json。
  8. 選用：有設 `TW_CLIENT_ROOT` 時，唯讀核對原檔與程式 pin（CI 沒有原客戶端，會略過並在 labels 註明）。

## 未驗證什麼

- **CI 結果**：推送後以 PR 的 CI 為準，本機沒有執行任何測試。
- 既有測試（combat-sfx `--selection`、sound-packets、sound-rules、module-tests 裡的 SoundFollowup2／SoundVolumeUpdate）本機只確認能編譯，結果待 CI。它們沒有用到被移除的 `AudioAssets.Load`。
- **6016 Native 回歸**（`effect6016-tests/run.ps1 -Native`，會開音訊裝置）：CI 只編譯（ubuntu），需要在 Windows 實機執行，交給 Codex。
- 真機聽感：進入有 6016 的地圖（例如 map4 港口），1340／3332 能從 `TW_CLIENT_ROOT/Sound` 正常播放，且 `web-evidence/audio/host.jsonl` 沒有 `create Unavailable`。待驗證。

## 產出路徑

- 程式：`engine/audio/AudioAssets.cs`、`engine/audio/AudioPlayer.cs`、`engine/audio/P24.Audio.csproj`、`engine/source/AudioApiTransport.cs`、`engine/source/ClientEffect6016.cs`
- 測試：`engine/_integration/dev/audio-provider-tests/AudioProviderChecks.cs`、`engine/_integration/dev/effect6016-tests/Program.cs`、`README.md`
- CI：`.github/workflows/build.yml`（新增 audio-provider 步驟）
- 報告：`docs/reports/issue-1045.md`
- 沒有改 `engine/_integration/dev/rebuild-lock.json`。

## 給下一棒的提示

- 產品已經不讀 `assets/audio-manifest.json`。等 T45x 的準備器／integrity 清單整理時，可以把它從 assets 的必要檔或 integrity 清單拿掉（本張沒有動 integrity）。
- 之後只要改 pin 或載入規則，一定要同時改 `AudioAssets.Effect6016Contract` 和 `AudioApiTransport.AudioContract`，兩者不同時 6016 會記 `create Unavailable`，不會出聲。
- 6016 的 metadata `maps/effects/6016.json` 仍從 assets 讀（`ClientEffect6016.cs` 第 20 行），屬於另一張 recipe。
