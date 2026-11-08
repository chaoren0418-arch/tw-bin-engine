# Issue #516 T210：M7-J 通用 Yes／No（聊天輸入列）

依據：`docs/reference/gap-matrix.md` 第 7 節 M7-J；`windows.md` §2.1、§2 T141；`world-objects.md` 第 6 節。

## 做了什麼

- **新檔 `engine/source/YesNoPrompt.cs`**：
  - `YesNoProtocol`：
    - S219 嚴格解碼（自己的 reader：H0、D、H、非空字串、零 padding）。
    - 特例 id 表照 WIN§2.1：102、180、289、321、325、479、512、622、252、217、951，加上 default 分支的 1565、1906。
    - 逾時只給 729（12 秒）、1232／1256／1268（10 秒）。
    - C121 `H0 D H C` 編碼：D 原樣回填，d287 一般是 0，組隊是 challenge；不走 622 的 requestId 路徑。
    - `Accepts`：字首 y／Y／ｙ／Ｙ 就是接受，其他（含空字串）一律拒絕，照 `sub_A58380`。
  - `WorldSession`：**單一詢問槽**。原版 `sub_AC9600` 把提示放進輸入列物件，並在 +788 只記一個請求 id；伺服器每個角色也只有一個 TempID。所以新的 S219 會取代舊的。
    - 換世界（epoch）時失效。
    - 對應模組的邀請失效時，詢問一起失效。
    - 對應模組自己回覆後，詢問清掉。
  - `LoginConnection`：
    - `YesNoSnapshot`、`AnswerYesNo(view, typed)`、`YesNoTick`（四個限時訊息逾時後自動回覆）。
    - 953、630、654 改走各模組既有的送出路徑（`SendParty`／`Duel`／`SendMarriage`）。封包、資格檢查與 generation 防護都不變。
  - `ClientForm`：
    - `KeyPreview` 攔截聊天輸入框的 Enter：有詢問時，輸入框內容視為 Y／N，送出 C121，並清空輸入框，不會送成聊天。
    - 狀態列上方顯示一行詢問文字。
    - 計時器負責逾時處理。
- **`Party.cs`**：`ReceiveParty` 開頭呼叫 `ObserveYesNo`。這裡是每個封包最先進入的模組，`WorldSession.cs` 不用改。953 邀請成立後加入詢問槽；`PartySent` 回覆後清槽。
- **`Duel.cs`**：630 邀請成立後加入詢問槽（在 `duelGate` 外，避免鎖順序反轉）；接受或拒絕後清槽。
- **`Marriage.cs`**：654 邀請成立後加入詢問槽；回覆後清槽。**移除求婚專用 Form `MarriageUi` 與 HUD「求婚／伴侶」按鈕**（T141：原版沒有專用視窗）。
- **測試**：新檔 `engine/_integration/dev/yes-no-tests/YesNoPromptTests.cs`，98 項。
  - 接上既有的 `demo-tests` 入口：`DemoChecks.csproj` 加一行 Compile；`Program.cs` 加 `case "yes-no"`。
  - 也加進 CI 已在跑的 `map-continuity` 群組，以及預設全跑。
- `rebuild-lock.json` 已用 `--update-lock` 重新產生。

## 怎麼驗證

- `py -3.12 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock` → `dev-inputs-verified`。
- `rebuild.py --build --configfile .github/nuget.ci.config` → `PASS dev`，productDll SHA-256 `197b2773…f51665`。
  - worktree 沒有離線 vendor，所以改用 CI 的 NuGet 設定。
- `DemoChecks` 各群組全綠：

  | 群組 | 結果 |
  |---|---|
  | `yes-no` | PASS 425（含 PartyChecks） |
  | `map-continuity` | PASS 33051（含 Marriage、ClanWar、YesNo） |
  | `combat-admission` | PASS 1686（含 DuelChecks） |
  | `duel` | PASS 441 |
  | `resurrection` | PASS 553 |
  | `afflictions` | PASS 580 |
  | `item-status` | PASS 409 |
  | `native-ui-resources` | PASS 539（真客戶端部分因未設 `TW_CLIENT_ROOT` 而 SKIP） |

- 測試涵蓋：
  - 三模組的 C121 逐 byte 相同。
  - Y／N 判定、特例 id、逾時 id。
  - 新詢問取代舊詢問；252 特例清槽並留給交易。
  - epoch 與 generation 失效；畸形封包不動到詢問槽；未就緒時不加入。
  - 四個逾時：提早不送、到時只送一次。
  - 組隊、決鬥、求婚經詢問槽回覆，封包精確；模組按鈕回覆後清槽；跟隨組隊本地逾時；死亡後擋下。
  - `MarriageUi` 類別已不存在。
- `engine/tools/check-no-game-assets.py` → pass。
- **同步 origin/main（2026-11-08，兩輪，含 #509／#518／#535／#524）**：
  - `rebuild-lock.json` 衝突：取 main 版後用 `--update-lock` 重產。
  - `demo-tests/Program.cs` 衝突：取 main 版，再把 `YesNoPromptChecks()` 接在 `MarriageChecks()` 後面。
  - 重建 `PASS dev`，productDll SHA-256 `82a0c258…1b1a65`；`check-no-game-assets.py` pass。
  - 重跑 `yes-no`、`map-continuity`、`combat-admission`、`duel`、`item-status`、`native-ui-resources`、`afflictions`、`resurrection`、`polymorph-cadence`、`buddies`、`item-transfer`、`doors`、`pickup`，13 組全綠。
- **第三輪同步 origin/main（2026-11-08，含 #511／#512／#521／#522／#532–#534／#569）**：
  - 只有 `rebuild-lock.json` 衝突：取 main 版後用 `--update-lock` 重產；`Program.cs` 自動合併，`YesNoPromptChecks()` 仍接在入口上。
  - 重建 `PASS dev`，productDll SHA-256 `d6ddee77…6fa6fa4`；`check-no-game-assets.py` pass。
  - 重跑上面 13 組加 `hypertext`，14 組全綠（`yes-no` 425、`map-continuity` 33117、`combat-admission` 1728）。
- **第四輪同步 origin/main（2026-11-08，含 #519／#523／#531／#548／#551）**：
  - 只有 `rebuild-lock.json` 衝突：取 main 版後用 `--update-lock` 重產；`Program.cs`、`DemoChecks.csproj` 自動合併。
  - 重建 `PASS dev`，productDll SHA-256 `76a6c611…5735311`；`check-no-game-assets.py` pass。
  - 重跑上面 14 組加 `door-approach`，15 組全綠（`yes-no` 425、`map-continuity` 33117、`combat-admission` 1750、`door-approach` 361）。
- **第五輪同步 origin/main（2026-11-08，含 #508／#446 背景音樂）**：
  - `rebuild-lock.json` 衝突：取 main 版後用 `--update-lock` 重產。
  - `demo-tests/Program.cs` 衝突：取 main 版（多了 `BackgroundMusicChecks()`），再把 `YesNoPromptChecks()` 接在 `MarriageChecks()` 後面。
  - 重建 `PASS dev`，productDll SHA-256 `31316c43…c4ee64`；`check-no-game-assets.py` pass。
  - 重跑 15 組加 `background-music`，16 組全綠（`yes-no` 425、`map-continuity` 33224、`combat-admission` 1750、`background-music` 434）。

## 沒驗證什麼（含推測）

- **沒有實機**：沒有啟動遊戲、伺服器或 DB。聊天輸入框的 Enter 攔截（WinForms `KeyPreview`）只有編譯，沒有實際按鍵驗證。
- **畫面是推測**：WO 第 6 節記錄「輸入列物件推測就是聊天輸入列」，原版位置 (192,581) 280×12。這裡只在狀態列上方加一行文字標籤，沒有做原版版面。
- **逾時自動回覆的值是推測**：文件只說「有逾時自動回覆」，沒有記回覆值。這裡送 0（拒絕），是保守的選擇。
- **提示文字是引擎自寫**：沒有載入 string 表（例如「X 邀請你加入隊伍。」「原服詢問 N：…」）。
- 953／954「特定條件下自動回覆」的條件待驗證，所以沒有實作；953 走一般詢問。
- Esc 在原版也走同一條判定路徑，但 Esc 被既有的 `ProcessPlayerChatKeyboard`（`Program.cs` 的 `ProcessCmdKey`）先拿去關閉聊天，所以**Esc 不送拒絕**。要照做需要改 `Program.cs`（M7-A）。

## 偏離與保留

- **97（入盟）、221／222（盟戰）、322（復活）**在原版也是一般詢問，但目前由不在本任務範圍的模組（`ClanUi`／`ClanWar`／`Resurrection`）處理，而且各有自己的 UI。為了避免同一個 TempID 有兩條回覆路徑，這次**不加入詢問槽，只在收到時清槽**。
- 組隊（30 秒）、求婚（15 秒）原本的**本地逾時保護**沒有拿掉：既有的 Party／Marriage 測試依賴它，測試檔也不在可改範圍。原版這些 id 沒有逾時，這一點仍是差異。
- `PartyUi.cs`、`DuelUi.cs` 的接受／拒絕按鈕不在可改範圍，所以保留。按鈕回覆後，詢問槽會跟著清掉，不會重複回覆。
- 移除求婚 Form 之後，**沒有 UI 可以發起求婚**。`SendMarriage("propose")` API 還在；原版是走 NPC HTML 動作 `HyperActPropose`。
- 動到 `demo-tests/Program.cs`、`DemoChecks.csproj` 各一兩行，目的是把新測試接上既有入口（任務要求）。沒有改 `build.yml`。

## 產出路徑

- `engine/source/YesNoPrompt.cs`（新）
- `engine/_integration/dev/yes-no-tests/YesNoPromptTests.cs`（新）
- `engine/source/Party.cs`、`Duel.cs`、`Marriage.cs`
- `engine/_integration/dev/demo-tests/Program.cs`、`DemoChecks.csproj`
- `engine/_integration/dev/rebuild-lock.json`
- 本報告：`docs/reports/issue-516.md`

## 給下一棒的提示

- **M7-K（交易）**：252 是特例（看 `byte_136E99D` 旗標）。旗標為 1 時，可以呼叫 `WorldSession.OfferYesNo(challenge, 252, [peer])`，並在 `AnswerYesNo` 加 252 的分支轉給 `Trade`，再把 252 從 `YesNoProtocol.Special` 的處理改成條件式。
- **血盟、盟戰、復活**要接過來：把 id 從 `delegated` 移出，在模組成立邀請時呼叫 `OfferYesNo`，在 `AnswerYesNo` 加分支，並移除各自的按鈕或面板。
- **M7-A 完成後**：`ObserveYesNo` 可以改成用擴充點註冊，不必掛在 `ReceiveParty`；Esc 也可以在 `ProcessCmdKey` 前先交給詢問槽處理。
- **`PartyUi`／`DuelUi`**：可以拿掉接受／拒絕鈕；兩個模組的本地 30 秒／15 秒逾時需要跟測試一起調整。
- **真機驗收**（dot，P24Lab 測試角色）：讓第二個角色邀請組隊或決鬥 → 按 Enter → 輸入 y → 按 Enter，確認入隊或開始決鬥，而且沒有送出聊天「y」。
