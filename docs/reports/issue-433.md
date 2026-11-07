# #433 T165：戰鬥音效補播 `<` 模式

依據：`docs/reference/rea-inventory.md`「B07：分類全鏈與音效選擇語義」（#417）。

## 做了什麼

- `engine/source/CombatSfx.cs`
  - `SoundSchedule.Candidates`：改成依序解析 `<`、`[`、`^` 三種聲音 token。候選最多存 5 個，第 6 個起丟掉；模式取最後一個 token。模式 0（`<`）與模式 1（`[`）回傳候選；模式 2（`^`）回傳空集合，屬 T166。值 0 保留在候選中，超過 word 範圍的值當成 0。
  - 新增 `SoundSchedule.Select`（對應 `sub_A3EF40`）：0 個或超過 5 個候選就略過；1 個取第 0 項、不呼叫亂數；2～5 個用 `random.Next(n)`；選中 0 就略過，不重抽。`SoundCursor` 改呼叫它。
  - `CombatSfxController`：加上同一 sample 200ms 冷卻（對應 `A06020` 的 `now+200`）。冷卻中的事件直接略過、不補播，並記一筆 `Cooldown` 觀測。冷卻表跨地圖 epoch 保留。
- `ClientCombatSfx.cs` 不用改，模式判斷集中在 `CombatSfx.cs`。
- 測試（`engine/_integration/dev/combat-sfx-tests/`）
  - 新增 `SelectionChecks.cs`，用 `--selection` 模式執行。全部用合成候選，不讀素材、WAV，也不開音訊裝置。4 個 case、46 項檢查：parser 模式與 5 個上限、`rand()%n` 的每個選項、選中 0 不重抽、cursor 進幀只播一次、200ms 冷卻（同 tick、199ms、200ms、不補播）。
  - `Program.cs`：原本的「`<` 尾綴被排除」斷言改成 `<206!<478<479 → 206,478,479`。controller 測試的第二、三段時間往後移，避開新的冷卻，驗證的取消語義不變。
  - 新增 `run.ps1` 作本機入口；`.github/workflows/build.yml` 的 Windows job（`map-continuity`）加了一步「Combat sound selection contracts」。
- `engine/_integration/dev/rebuild-lock.json`：用 `rebuild.py --update-lock` 重新產生。

## 怎麼驗證

| 項目 | 結果 |
|---|---|
| `rebuild.py --update-lock` → `rebuild.py --build` | PASS。合併 main 前 productDll 是 `e8546eb7…ecd4`；合併 main 後重產 lock（沒有差異）再建置，productDll 是 `4e3ef52b…44a7`，audioDll `f135b508…d63d` |
| `--selection` 離線檢查（`run.ps1`） | PASS 4 cases / 46 checks（合併 main 前後各跑一次） |
| 既有 combat-sfx 完整套件（唯讀讀取 r29 assets 與 `0-客戶端` WAV） | PASS 8 cases / 8987 checks（只在合併 main 前跑過） |
| `check-no-game-assets.py` | pass，errors 0 |

完整套件需要 `engine/assets`，但 worktree 沒有這個 junction。所以暫時用 `mklink /J` 建一個指向 `releases/r29/assets` 的 junction，跑完已經用 `cmd /c rmdir`（不加 /s）拆掉，目標目錄還在。

## 沒驗證什麼

- **沒有真機聽音**，沒啟動遊戲、伺服器或 DB。
- **重要：`<` 的 ID 目前大多會被音訊層拒絕。** 掃描 `engine/assets/objects` 共有 24 個不同的 `<` ID（例如 478、479、34、1715、704、1340），**沒有一個在** `engine/audio/Resources/combat-sfx-pins.json` 的 33 個 WAV 白名單裡。所以這次改完，控制器會派送這些事件，但 `AudioCore` 會回 `IdNotAllowed`（會記在 `combat-sfx.jsonl`），實際聽不到。要真的出聲，得擴充 pin 清單（`engine/audio`），不在本張範圍。
- 200ms 冷卻只在戰鬥控制器內共用，沒有和 6016 場景音（`Effect6016Controller` 有自己的冷卻）或其他音源共用。原版 `A06020` 是全域的。
- S22 伺服器音效也走同一個控制器，所以也會套用 200ms 冷卻。重複間隔（WAV 長度）小於 200ms 的 S22 會被略過，這部分沒有實測。
- 推測的部分：`^` 與 `<`／`[` 混在同一格時，假設 parser 把所有 token 的數字都累積進同一個候選陣列、模式取最後一個。B07 只寫了「互斥 switch」，沒有逐 token 驗證累積方式。
- CI 綠燈要等 PR 跑完才知道。

## 產出路徑

- `engine/source/CombatSfx.cs`
- `engine/_integration/dev/combat-sfx-tests/SelectionChecks.cs`、`Program.cs`、`run.ps1`
- `.github/workflows/build.yml`
- `engine/_integration/dev/rebuild-lock.json`
- 本報告

## 給下一棒的提示

1. **擴充戰鬥 WAV pin**：把 `<` 用到的 ID（至少 gfx 0 腳步聲與常見攻擊幀）加進 `combat-sfx-pins.json`，並逐一核對 size／SHA。沒做這一步，T165 在真機上聽不出差異。建議開一張 resource／code 任務。
2. T166（`^`）：`Candidates` 已經會辨識模式 2，目前回傳空集合。T166 可以改回傳模式，接 `A06210` 的距離槽（每 id 最多 5 個音源）。
3. 如果要讓冷卻和 6016、S22 共用全域表，要動 `engine/audio` 或抽出共用服務。
4. 真機（dot）：近戰時看 `combat-sfx.jsonl`，應該出現 `<` ID 的 `play` 紀錄（目前是 `IdNotAllowed`），並確認同 ID 200ms 內只出現一次 `StartedWav`。
