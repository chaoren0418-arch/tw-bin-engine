# Issue #804 報告：T341 R7-B 輸入列 Y/N 與表情保留

依據：`docs/reference/gap-matrix.md` 第 15.6 節 R7-B；WO §6、WIN2 §5.3、AN §1、#736 報告。分支 `task/804`。

## 做了什麼

我先用 pefile＋Capstone 靜態讀原版 `TW1608262200.bin`（`P01-分析基準/input`）核對，才動程式。腳本放在 `docs/reference/rea-scripts/t804/`：`dis.py` 反組譯，`xref.py` 找絕對位址引用。

| 項目 | 原版（已驗證，VA） | 引擎改動 |
|---|---|---|
| 321 改走共用輸入列（op 197） | WO §6：321 直接進 `sub_4FC710` 的輸入列，322 先讀一個 `s` 再進同一列 | `YesNoPrompt.cs`：`ModuleOwned` 加 321；`ObserveYesNo`、`YesNoModuleLive`、`AnswerYesNo` 改成 321／322 都走同一條路（打 Y／N＋Enter 或按 YES／NO → C121 H0 D0 H321 C）。`Resurrection.cs`：拿掉 WinForms 復活面板，提示文字改成「請在聊天輸入列回覆 Y／N」；`ResurrectionUsesPanel` 一律回 false。`InitResurrection` 保留成空方法，因為 `Recovery.cs` 還在呼叫它（該檔不在本組範圍） |
| YES／NO 提示畫在輸入列 (192,581) | `sub_7026C0` 用 `sub_AC3A40(192,581,280,12)` 建立輸入列物件，`sub_AC9600` 把提示放進去（WO §6） | `YesNoButtons.Line`；`YesNoPanel` 的 Bounds（也就是繪圖裁切範圍）加上這一列，文字裁在 280×12 內；滑鼠只攔兩顆鈕，點輸入列會穿透。面板顯示的文字拿掉引擎自加的「輸入 Y 接受…」提示（`YesNoProtocol.LineText`） |
| 原生開啟時藏 WinForms Label | — | `SyncYesNo`：原生 `YesNoPanel` 可用時不顯示 `yesNoLine`（`YesNoButtons.WinFormsLineVisible`）。`--classic-ui` 或面板 Faulted 時照舊顯示 Label＋WinForms 鈕 |
| 立即型表情 700 ms（`byte_153CE69`） | `sub_4FBA70` 在本地播放後一律設 `153CE69=1`（`0x4FBABC`）；hold=1 時清回 0、改設 `153CE6A`（`0x4FBADA`）。自己的動作 66–69 結束時（`0xA4B573`）先取消舊的 `sub_A400C0` 計時器（`sub_A23B30`），再排 700 ms（`0x2BC`）；`sub_A400C0`（`0xA400C3`）只做 `153CE69=0`。三個讀取點都在呼叫 `sub_A4C270(0)` 前檢查 busy、`153CE5F`、`153CE69`、`153CE6A`（`0xDF5401／0xDF540C`、`0xDF554F／0xDF555A`、`0xF0789F／0xF078AA`）。`0x8C47CD` 兩個旗標一起清 | `TitleAndEmote.cs`：`EmoteSettling`（旗標）與清除計時器分開存，和原版一樣：計時器只負責清旗標，第二個表情可能被第一個計時器提早清掉。`EndEmote`／10 秒 prune 視為「動作結束」。新增 `EmoteInputPaused`＝`EmoteHeld ‖ (EmoteSettling 且自己的表情已不在播)`，並併進 `MoveLocked`（`Resurrection.cs`） |
| S103 type 8 保留給 `flag547` | `sub_9343C0`（`0x934401–0x934425`）：type 0 → 物件 `+547(0x223)=0`；type 8 → `+547=1`，之後把 type 當 0 處理 | `TimedBuffs.cs`：`OwnThirdSpeed`。自己的 type 8 設旗標並當 type 0 處理（酒醉解除，地圖 67／420 醉度 1）；自己的 type 0 清旗標；`[WorldReset]` 清除。不跟 `WorldEpoch` 走，因為伺服器只在吃蛋糕與登入（`CharBuffTable`）時送 type 8，傳送後不重送。`TitleAndEmote.cs` 的 `EmoteThrottleMs` 改傳 `OwnThirdSpeed`（2265 ms） |
| 縮小保留期間阻擋的範圍（op 92） | 原版保留期間只略過 `sub_A4C270(0)`，也就是按住的輸入不再繼續走或動作 | **沒做到**，原因見「未驗證什麼」第 1 點 |

**範圍外但必要的改動**（舊斷言和本組目標直接衝突，比照 #736 的做法）：
- `engine/_integration/dev/resurrection-tests/ResurrectionChecks.cs` 的 `ResurrectionPanel`：原本用反射測 WinForms 復活面板，改成確認面板已移除、321 進共用輸入列、S85 會清掉。
- `engine/_integration/dev/timed-buffs-tests/TimedBuffChecks.cs` 第 94–98 行：原本斷言「自己的 type 8 不影響酒醉、計入 unhandled」，改成依 `sub_9343C0`：設 `+547`、當 type 0 處理、不計 unhandled；其他物件的 type 8 仍計入 unhandled。
- `engine/source/YesNoFollowupTests.cs`：「321 保留復活面板」改成「321 走共用輸入列」；面板 Bounds 的期望值加上輸入列。
- `docs/protocol/resurrection.md`、`docs/protocol/timed-buffs.md`：各改一段說明，對齊新行為。

## 怎麼驗證

- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile <絕對路徑>/.github/nuget.ci.config`：sdk、product、launcher 的 restore 與 build 都回 0，結果 `PASS dev`。這次用暫時 lock，沒有寫入 `rebuild-lock.json`。
- `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：0 個錯誤。3 個警告都是既有檔案（`DisplayNoticeChecks.cs`、`MacroChecks.cs`），不是本 PR 造成的。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`，0 錯誤。
- 新增離線測試 `engine/source/InputLineFollowupTests.cs`（`[WorldModuleTest("input-line-followup")]`，自動發現），涵蓋：
  - 321 的 Y／N、按鈕兩種回覆、只能回一次、活著時不排入、S85 會清掉、被 322 或其他 S219 取代、模組自己回覆也會清輸入列；
  - 輸入列座標、WinForms Label 的顯示條件、提示文字拿掉引擎提示；
  - 面板：文字畫在輸入列內、輸入列上方不會畫到、空字串時只畫鈕、點輸入列會穿透；
  - 700 ms：表情播放中不擋，結束後擋到 699 ms、700 ms 解除；計時器只負責清旗標；prune 算結束；走路打斷會丟掉旗標；沒有本地表情時立刻計時；hold 會清 `153CE69`；節流型不設；Reset 會清；
  - `+547`：2265 ms 與 jbe 邊界（2265 不送、2266 送）、加速時 1698 ms、換地圖保留、其他物件 type 8 計入 unhandled、type 0 會清、Reset 會清、地圖 67 醉度 1、會解除酒醉；
  - 記錄目前 hold 仍經過 `MoveLocked`。
- **依使用者規定，本機沒有執行任何測試**（沒跑 `--module-tests`／DemoChecks），測試結果以 PR 的 CI 為準。

## 未驗證什麼

1. **「縮小保留期間阻擋的範圍」沒做到**。保留旗標只能從 `WorldSession.MoveLocked` 送出去，而它唯一的讀取點是 `OnlineMovement.cs:56` 的 `MovementAvailable`。攻擊（`Combat.cs`）、施法（`SkillCast.cs`）、門（`Doors.cs`）、傳送點（`Portals.cs`）都以 `MovementAvailable` 為前提，所以保留期間這些也會停。另外 `ClientLogin.cs:21` 一看到不能移動就清掉按住的輸入；原版是暫停，旗標解除後會繼續走。要照原版只擋「按住的輸入繼續走」，必須改 `OnlineMovement.cs`、`ClientLogin.cs`、`Program.cs`（`ClearHeldInput`）或 `PlayerPointer.cs`（R7-A）。這些都不在本組可改的檔案，`Program.cs` 還是共同禁改檔。本 PR 先把三個旗標整理成單一的 `WorldSession.EmoteInputPaused`，下一棒只要在按住輸入的路徑讀它、把它從 `MoveLocked` 拿掉就行。
2. 因為第 1 點，本 PR 新增的 700 ms 也會跟著擋攻擊、施法、門與傳送點，並清掉按住的輸入（推定可接受，每次最多 700 ms）。表情播放期間不擋，引擎原本的規則是走路就打斷表情；原版此時靠 busy 檢查 `sub_A3EBF0` 擋住。
3. 沒有本地表情可以結束時（例如 `StartEmote` 被拒），引擎改成立刻開始計 700 ms，避免旗標一直卡著。這是推定。
4. 輸入列文字的畫法是推定：白字黑影、不加底色，超過 280 寬就裁掉（原版會不會捲動沒查）。原生輸入列和聊天窗輸入框是不是同一列、會不會疊字，也還沒確認，都要真機看。
5. 原生模式下，「未送出：詢問已失效…」的失敗訊息原本寫在被藏起來的 WinForms Label 上，現在看不到。提示仍在時面板繼續顯示原題目。
6. 其他物件的 `+547`（動畫時間 `sub_A3BC30`）沒保留，type 8 仍計入 unhandled。
7. 沒有宣稱任何資源「沒有」，所以這次沒有跑 PakViewer 查詢。字串表 321 的原文由 `ServerMessageText` 在執行期讀參考客戶端，CI 用引擎自己的文字。
8. 沒開遊戲、伺服器或 DB。待真機確認：死亡後一般復活卷軸的 321 出現在輸入列，打 Y＋Enter 或按 YES 會復活；原生 UI 下提示列不重複出現；職業外型做表情後約 0.7 秒才能移動；吃三段加速蛋糕後，變身表情的間隔是 2.27 秒。

## 產出路徑

- `engine/source/Resurrection.cs`、`YesNoPrompt.cs`、`NativeUi/YesNoPanel.cs`、`TitleAndEmote.cs`、`TimedBuffs.cs`
- 新檔 `engine/source/InputLineFollowupTests.cs`
- 範圍外但必要：`engine/_integration/dev/resurrection-tests/ResurrectionChecks.cs`、`engine/_integration/dev/timed-buffs-tests/TimedBuffChecks.cs`、`engine/source/YesNoFollowupTests.cs`
- `docs/protocol/resurrection.md`、`docs/protocol/timed-buffs.md`、`docs/reference/rea-scripts/t804/*.py`、本報告

## 給下一棒的提示

- **縮小保留範圍**（建議開一張小任務，可改 `OnlineMovement.cs`、`ClientLogin.cs`，必要時加 `PlayerPointer.cs`）：
  1. `Resurrection.cs` 的 `MoveLocked` 改回只看 `ServerMoveLocked`；
  2. 在按住輸入繼續移動或攻擊的地方讀 `World.EmoteInputPaused`，旗標為真時「暫停、不清除」；
  3. `ClientLogin.cs:21` 不因 `EmoteInputPaused` 清輸入；
  4. 同時改 `ProtocolFollowupTests.cs:88` 與 `InputLineFollowupTests.HoldScope` 的斷言。
- 原生模式的失敗訊息可以加進 `YesNoPanelView`（例如 `Notice`），由面板畫在輸入列。
- 其他物件的 `+547`，可以和 `sub_A3BC30` 的動畫時間一起做。
- gap-matrix 下一輪：op 197（321）、`InputWindow` 文字、op 92 的 `153CE69`、S103 type 8 可以改判「已做」。保留範圍那項仍是「部分」。
- 真機（Codex）：照「未驗證什麼」第 8 點的四項，在 P24Lab 用測試角色確認。
