# Issue #192／T105：音效與座標特效封包

## 結論

**code 階段已完成接線與本機離線驗證。** S22、S106、S55 8／16-byte 變體由正式接收入口處理，音訊沿用既有派送，圖像依 WorldEpoch 退役。PR #214 的最新 CI 結果見 PR checks；真機留給 M6 smoke。

未啟動遊戲、伺服器、DB、視窗或音訊裝置；未讀取機密配置、複製遊戲資源、更動封存或移除 worktree。原專案僅使用 worktree 必需的共用 Git 中繼資料，未修改原專案工作檔。

## 做了什麼

- `SoundPackets.cs` 解碼 S22 與 S55／S106，保留 unsigned ID、repeat、H(time)，嚴格驗證長度／padding。S22 時長取既有核定 PCM WAV，沿用音訊核心白名單。
- `MapEffects.cs` 管理座標／附著圖像、逐幀顯示、跟隨目標、期限、缺資源診斷與換圖退役。`MagicSpells.AttachedEffect` 增加 appearance 動作選擇。佇列各最多 256 筆。
- 依 [主控核准](https://github.com/chaoren0418-arch/tw-bin-engine/issues/192#issuecomment-6697166058)，`WorldSession.ReceiveCore` 在舊接收鏈前增加三個只呼叫模組的 case；`ClientCombatSfx.cs` 只在既有 Pump 引數增加一個 `WithPacketSounds` 呼叫，未更動戰鬥音效邏輯或 `engine/audio`。
- 六種已支援短版 S55 委派既有魔法路徑一次；其他及 timed 變體只進新圖像佇列，保留道具／補給通知。gfxId 不作 soundId，未重複加入兩條顯示或播音路徑。
- 已在 `task/192` 合併 `origin/main`，dev lock 使用 `--update-lock` 重新產生；沒有手動拼 hash。`docs/REPORT.md` 僅隨 main 合併帶入既有索引，本任務沒有編輯它。

## 去重規則與限制

S22 首播延遲 100 ms；同一 WorldEpoch 中，若既有動作／sprite 幀事件的唯一聲音候選與 soundId 相同、幀時間距 S22 收包時間不超過 100 ms，就保留幀事件並略過 S22 首播。每個幀事件最多配對一個 S22；repeat 後續次數、獨立 S22、原戰鬥排程都保留。配對決定不因重複 pump 改變，換圖清空；幀歷史只留最近 200 ms、最多 1024 筆。

**推測／待驗證**：S22 沒有角色或事件編號，時間窗無法證明事件同源。同時發生的獨立同 ID 聲音可能誤配；超過時間窗、多候選音效不配對，可能仍重複。repeat=額外次數、time=秒同樣是待 smoke 核對的產品政策。

## 怎麼驗證（已驗證）

- Python 3.12／.NET SDK 10.0.300：`rebuild.py --update-lock` 後，`rebuild.py --build --configfile .github/nuget.ci.config --output scratch/issue-192/build` 通過。產品 DLL SHA-256：`3e8f4002e533c59abda5c49dc9acc6264f4afab57a9d0acb24ad5545f344f3d0`。產品有既有 `MagicUi.cs` 兩項 nullable 警告，以及 NU1900 弱點資料端點無法連線警告；0 編譯錯誤。
- **148 項模組檢查通過**：解析邊界／截斷／padding、S55 8／16 bytes、time=0、逐幀／期限／跟隨／缺資源、佇列上限、Reset／S149／S206、repeat、不重播、延遲 registration fence。
- 本輪 Feed 全改走 `WorldSession.Receive`，新增六種舊 S55 的單次顯示與道具通知、timed S55 的分派優先序、事件通知次數、malformed 封包不變更狀態，以及直接呼叫 `ClientForm.PumpCombatSfx` 的接線／退役測試。宿主跳過 Form 建構子，音訊使用記憶體 transport。
- 去重檢查包含兩種收包順序、repeat 保留、不同 ID／多候選不配對、一對一配對、換圖清空與時間窗外保留。
- **128 項 map-continuity 回歸通過**；`check-no-game-assets.py`：3232 個追蹤檔案，0 errors。`git -c core.whitespace=cr-at-eol diff --check` 通過，保留既有 CRLF。
- 模組測試重現：先 restore `engine/_integration/dev/sound-packets-tests/SoundPacketChecks.csproj`（指定 `.github/nuget.ci.config`），再 `dotnet build engine/_integration/dev/sound-packets-tests/SoundPacketChecks.csproj -c Release --no-restore -o scratch/issue-192/tests/bin`；執行 `dotnet scratch/issue-192/tests/bin/SoundPacketChecks.dll scratch/issue-192/checks.json`。先完成產品建置；測試合成 catalog 只寫 scratch。

## 未驗證什麼

- 沒有原生登入、圖像混色、實際聽音與時間語義驗收。
- 音訊仍只支援既有 33 個核定 WAV，圖像只支援既有 catalog；其他 ID 明示未播放／未顯示。新圖像佇列未擴充 sprite 幀事件音訊，既有短版 S55 維持原路徑。
- 本模組 148 項測試於 Windows 本機執行；既有 CI 執行其原有建置與回歸工作，未擴改共用 workflow。CI 編譯成功不等於真機通過。

## 產出路徑

- `engine/source/SoundPackets.cs`、`MapEffects.cs`、`MagicSpells.cs`
- `engine/source/WorldSession.cs`、`ClientCombatSfx.cs`（核准的最小掛接）
- `engine/_integration/dev/sound-packets-tests/`、`engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/sound-effects.md`、`docs/reports/issue-192.md`
- 未追蹤本機證據：`scratch/issue-192/checks.json`、`map-checks.json`、`build/`
- 分支 `task/192`；[PR #214](https://github.com/chaoren0418-arch/tw-bin-engine/pull/214)。

## 給下一棒的提示

Claude 審查 PR #214 的最小掛接與時間窗政策，合併由使用者處理。dot 在 M6 smoke 使用核定素材核對 repeat 0／1、time 0／1／2、S106 座標與混色、timed S55 跟隨、換圖／斷線／靜音退役，以及 S22 與動作幀事件的同源／獨立同 ID 情境。未核定 ID 交資源任務補來源，不得用替代素材冒充支援。前輪 `scratch/issue-192/pending-wiring.patch` 已被正式實作取代，不要再套用。
