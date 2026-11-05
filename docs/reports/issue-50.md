# Issue #50 技能與攻擊音效幀事件派送

2026-11-05，code 第 1 次嘗試更新。分支 `task/50`，基線 `origin/main@df7b5f4`。依新版裁定採用 #47 `task/47@7cffcd2` 的靜態事件與 33 個 WAV pins，更新先前的 blocked 報告。

## 結論

實作、本機 dev 建置與離線回歸完成。多候選隨機擇一，以及忽略 `<`、`!` 等非 `[` 尾綴，均標示為「**產品政策，推測，待 #52 真機聽音核對**」。原生派送語義仍為 unknown，來源背景見 #47 報告。[#47 證據報告](https://github.com/chaoren0418-arch/tw-bin-engine/blob/7cffcd2/docs/reports/issue-47.md)

## 做了什麼

- `AnimationStep` 保留原快取 `events`。`SoundSchedule` 依 sequence index 與既有幀時長建立進度，`SoundCursor` 在首次進入幀時消費候選，循環每輪各消費一次。亂數可注入 `Random`，產品使用 `Random.Shared`。
- 技能 1／3／4／8 分別派送 109／127／97／804。純效果沿用 index1 起始時序。光箭沿用 `MagicMotion` 的釋放時間、60ms 飛行步進及飛行／impact 分段。gfx219 的資料零個音效事件。
- 近戰以權威 `CombatAnimation.Instance` 識別，同一預測動作的確認保留實例 ID。攻擊與受擊分別讀取攻擊者、目標的 gfx／weapon appearance。事件排程使用 `ActionCadence` 的 normal／haste／slow 幀時長。
- combat 重用 `AudioPlayer`／`AudioCore`，以獨立 owner 管理實例。重用 slot 時提高 generation。換圖、死亡、斷線、外觀失效、動作過期及關閉都取消派送，延遲 register 完成後再次檢查 fence。
- 音效由主計時更新與權威封包套用後推進。接入 `PlayerAudioSettings` 的 gain／mute，關閉流程等待有時限的音訊清理。
- embedded resource 收錄 33 個 WAV 的檔名、大小及 SHA-256。從 `TW_CLIENT_ROOT` 指定的原客戶端根目錄讀取，預設為 `D:\lineage開發\0-客戶端`。每次開啟 WAV 核對大小與 SHA，持有唯讀 deny-write/delete lease 直到 voice 關閉。
- 既有 WebEvidence 目錄下的 `audio/combat-sfx.jsonl` 記錄 controls、register、play、retire、maintenance、shutdown，包含 ID、幀 index、cycle、狀態與 remaining voices。缺檔為 `FileMissing`，雜湊失敗為 `IntegrityFailure`，未知 ID 為 `IdNotAllowed`。

本次選擇為 combat 建立獨立的 32-voice transport，沿用相同 AudioCore 與設定值。這是產品生命週期設計，實際混音與聲音重疊交由 #52 核對。

## 已驗證（離線）

| 項目 | 結果 |
|---|---|
| dev lock | 187 個來源檔、196 個 pin 通過 |
| dev 主程式與 launcher | 建置成功，各 0 警告、0 錯誤 |
| dev `--verify` | 通過，receipt 與 manifest 身分吻合 |
| 新增 combat 音效回歸 | 7 組、8,780 項檢查通過 |
| Effect6016 host | 18 組、56 項檢查通過 |
| 既有客戶端音訊生命週期 | 4 組、21 項檢查通過 |
| 既有音量／靜音設定 | 7 組、39 項檢查通過 |
| Web 音量控制 | 35 項檢查通過 |
| 封包追蹤 | 7 組、36 項檢查通過 |
| dev／r35 路徑回歸 | 7／4 個測試通過 |

新增回歸覆蓋以下範圍。

- #47 的 31 組外觀／武器、62 條 attack／damage action，全八方向的來源尾綴、幀邊界、單候選消費及重繪去重。基準附來源 commit、原始 evidence SHA 與 TXT SHA。整理時修正了先前漏掉帶武器名稱標籤的篩選，最終包含完整 62 條列並有數量防漏檢查。
- 技能 1／3／4／8 的 ID、index1 起始、光箭釋放前抑制、每 240ms 飛行循環再次進入、silent219 impact，以及純效果聲音幀與 renderer 一致。
- seed 50 的單候選序列、normal／haste／slow 邊界、實際 S30 parser 的攻擊者 weapon4／目標 gfx1 分流、預測確認共用實例 ID。
- S158 死亡、S206 換圖、`LoginConnection.Cancel()` 的實際離線入口，以及過期外觀、target death、延遲 register、過期 action、AudioPlayer in-flight owner／mute fence。
- 33 個原始 WAV 唯讀大小／SHA 核驗。以明示 fake driver 驗證 gain 0／0.3、靜音停音、缺檔、hash mismatch、退役 owner 拒播、自然完成清理、close failure 保留 lease 與重試釋放。
- AudioCore 既有 MP3 開啟失敗後 WAV fallback，以及 Effect6016／UI 設定回歸。

既有 `check-template` 在 scratch 產生測試副本。HostChecks 的舊 DLL 路徑及固定身分改指向本次 dev 產物，原音訊 manifest 固定 SHA 仍核對。Client method excerpt 從目前來源重產。音量設定 excerpt 的 `PumpCombatSfx` 使用明示空接點隔離，combat 另有獨立回歸。調整與各測試結果保存於 evidence。

### 建置身分

- `P11.ClientEngine.dll` SHA-256 為 `d6c2884c83e7d5a9f08a74e658391e3c1654492d9de059905432f224689e7121`。
- `P24.Audio.dll` SHA-256 為 `ca64c3b8638330fcf50be43dd0318f8607691584af5fb1ee5179b9e9b09ae309`。
- `P24.Launcher.dll` SHA-256 為 `34c6aa28bcfa0206f9464ae88cb649288552a07f9e3d7f1310f24f860f3479c2`。

### 重現主要驗證

在專案根目錄執行。此機 `python` 未在原 PATH，實際使用既存 Python312 執行檔。

```powershell
$env:PATH='C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312;'+$env:PATH
$env:NUGET_PACKAGES=Join-Path $PWD 'engine/.nuget'
$env:DOTNET_CLI_HOME=Join-Path $PWD 'scratch/issue-50/build/.dotnet'
$env:TEMP=Join-Path $PWD 'scratch/issue-50/build/tmp'
$env:TMP=$env:TEMP
New-Item -ItemType Directory -Force $env:TEMP,$env:DOTNET_CLI_HOME | Out-Null
python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock
python -B -X utf8 engine/_integration/dev/rebuild.py --build --write-receipt --output scratch/issue-50/build
dotnet build engine/_integration/dev/combat-sfx-tests/CombatSfxChecks.csproj -c Release -o engine/build/combat-sfx-tests --configfile engine/NuGet.Config -p:NuGetAudit=false -p:UseSharedCompilation=false
dotnet engine/build/combat-sfx-tests/CombatSfxChecks.dll $PWD 'D:\lineage開發\0-客戶端' scratch/issue-50/combat-sfx-result.json
python -B -X utf8 engine/_integration/dev/rebuild.py --verify --output scratch/issue-50/verify
```

## 待真機聽音

- 由 #52 核對技能 1／3／4／8、不同武器 attack、不同目標 damage、光箭飛行與 impact 的實際可聽結果及時序。
- 核對多候選隨機擇一、忽略非 `[` 尾綴這兩項產品政策，以及連續攻擊、聲音重疊、換圖、死亡、斷線、音量和靜音的體感。
- 實際 DirectShow 裝置、音效輸出與原生 parity 待驗。本輪執行純離線程式與 fake driver，遊戲／服務／DB 執行次數為 0，實際音訊播放次數為 0。
- 跨機器乾淨建置待驗。PR #65 的 GitHub CI 已確認受 #62 帳務／額度問題阻塞，詳見下節。

## GitHub CI 阻塞

PR #65 已建立。2026-11-05 的 run `42016590980`、job `127208539949` 在 commit `9bb181547b67c3b6681129ec86b95d8fafd3d62a` 回報 failure。GitHub API 顯示 `runner_id=0`、`runner_name` 為空、`steps=[]`。Annotation 明確指出近期帳款失敗或 spending limit 需要提高，須檢查 Billing & plans。這與既有 #62 的 runner 啟動阻塞一致，交調度器／Claude 接續處理。[失敗工作](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42016590980/job/127208539949)

## 產出路徑

- `engine/source/CombatSfx.cs`、`CombatSfxScene.cs`、`ClientCombatSfx.cs` 與既有來源的必要接線。
- `engine/audio/IAudioPlayback.cs`、`Resources/combat-sfx-pins.json` 與既有音訊元件接線。
- `engine/_integration/dev/combat-sfx-tests/`、`engine/_integration/dev/rebuild-lock.json`。
- `work/issue-50/evidence.json` 保存本輪最新版機器可讀結果。
- `docs/reports/issue-50.md` 為本報告。
- `scratch/issue-50/` 保存暫存、測試副本與建置紀錄。產品輸出為 `engine/build/client-product-dev`、`engine/launcher`，測試輸出位於 `engine/build` 各測試子目錄。

## 給下一棒的提示

1. Claude 審查事件消費、owner fence、33 個 pins 與離線證據。調度器依 PR 的 CI 結果處理已知 #62 建置工作。
2. 使用者合併後，由 #52 安排真機聽音驗收。保留 `audio/combat-sfx.jsonl`，將 skill／gfx／weapon、幀 ID、cycle 與聽到的聲音一起記錄。
3. 若出現缺檔或雜湊失敗，先核對原客戶端資源根與 #47 pin。新增 ID 應先補來源與 SHA，再擴充 catalog。

本次先解除乾淨舊 worktree 的 `task/50` 占用，在指定主目錄從 origin/main 建立分支，再合入舊報告 commit 作為歷史父節點，供正常 fast-forward push。
