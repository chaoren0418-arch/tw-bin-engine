# Issue #808 T345：R7-F 公告板收尾（code）

分支：`task/808`。依據：gap-matrix 第 15.6 節 R7-F、#739 報告。本輪用 `docs/reference/rea-scripts/disx.py`（Capstone，唯讀）對 `TW1608262200.bin`（SHA-256 `e12949cf…41a2`）補反組譯 `sub_7CDE00` 與 `sub_7CBA70` 的 678 綁定。

## 做了什麼
- **`engine/source/NativeUi/BoardPanels.cs`**
  - `BoardWindowLayout.DefaultOrigin` 從 (281,60) 改成 **(0,0)**（UI 原點在 800×600 為 0，ui-layout.md V5）。內容面板跟著移動。
  - `BoardButtonWindow` 改成實作 **`IUiClosablePanel`**。`Close()` 和窗的 X 鈕做同一件事，窗沒顯示時不動作：
    - 清單窗：658 → 關整個公告板（`Press("close")`，同前）；
    - 閱讀窗：**678 只關閱讀窗**（`ShowList()`，本機切換，不送包）。
  - 加上 `internal Load(Func<int,UiSurface?>)`／`BoardPanelSet.LoadSurfaces(Func)`，讓離線測試能用合成圖（同 `BoardPanel.cs` 的作法）。
- **`engine/source/NativeUi/BoardPanel.cs`**
  - `BoardContentSet.OpenWrite()`（662）照 `sub_7CDE00` 加上金幣檢查：費用≠0 且金幣＜費用 → 聊天列印 **189「金幣不足。」**（色 24），不開發文窗。
    - 費用 0 時不看金幣。
    - 金幣不明（背包還沒載入）時不擋，交給伺服器。
  - `BoardText.StringNoGold=189`、`NoGoldText`：走 `ServerMessageText`，和伺服器訊息用同一張表，並去掉 `\f1`。沒有表時用已查證的原文「金幣不足。」。
  - `IBoardContentHost` 新增兩個有預設實作的成員，所以既有的 host 不必改：
    - `Gold`：預設 null；
    - `ChatNotice`：預設退回 `Notice`。
- **`engine/source/BoardUi.cs`**
  - **原生公告板掛上時，不再 `Show` WinForms `BoardUi`**。`BoardUi` 改用 `RunHidden(owner)`：設 Owner，自己啟動同步 timer，留在背景負責動作、確認框與長度／Big5 檢查。
  - `AttachNativeBoard` 改成回傳 bool。以下情況回到原本的 WinForms 窗（含 `LeaveGameFocus`）：
    - `--classic-ui`；
    - 原生 UI 不可用；
    - 清單／閱讀面板 faulted。
  - 隱藏的 Form 沒有 handle，`Close()` 只會 Dispose、不會觸發 FormClosed。所以 `CloseBoard` 改在 `FormClosed` 與 `Disposed` 都會跑，只執行一次。
  - 隱藏時，確認框的 owner 改成 `ClientForm`。
  - 隱藏時，544「請輸入題目。」改印到聊天列（原本只寫在看不到的提示列）。
  - 新增 `Gold`（`ShopTrade.Holding`，背包金幣）、`ChatNotice`。另在本檔加了 `WorldSession` partial 的 `AddBoardChatNotice`（不改 `WorldSession.cs`）。
- **`engine/_integration/dev/native-ui-tests/BoardPanelChecks.cs`**：只改一行（第 77 行）。舊檢查寫的是「閱讀窗 X → close」，和本任務目標（678 只關閱讀窗）正好相反，所以改成「不送包、清單窗留著、閱讀窗消失」。其他座標檢查都用相對原點，不必改。
  - **注意**：這超出「只改座標」的限制，但不改這行，這組既有測試一定失敗。
- **新檔 `engine/source/BoardFollowupTests.cs`**：`[WorldModuleTest("board-followup")]`，38 項檢查：
  - (0,0) 座標；
  - `IUiClosablePanel`；
  - 678／658 的行為；
  - `ShortOfGold` 的邊界：0、等於、null、long.MaxValue；
  - 189 的文字，有表、沒表兩種；
  - 介面預設成員；
  - `AddBoardChatNotice` 的行數、顏色、上限 100；
  - `UiRoot` 視窗堆疊與命中。
- 沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

## 依據（靜態反組譯，VA；FO＝VA−0x400000）
| 項目 | 位置 | 結果 | 等級 |
|---|---|---|---|
| 662 回呼 | `sub_7CDE00`（0x7CDE00–0x7CDED1） | `[this+0x40]≠0` → 不動作。`word [this+0x20]`（費用）≠0 時，比較 `sub_992F80`（`sub_4F9FF0` 單例 +0x54 容器，回傳 64 位元）與費用：較小 → `sub_4EAFF0(0xBD=189)` 交給 `sub_7DC850(字, palette[24], palette[14])`，不開窗。否則抄 `[+0xc]`、費用進發文窗並 `sub_A59AB0` 開窗 | 已驗證 |
| `sub_992F80` 的值是背包金幣 | 同上 | 引擎用 `ShopTrade.Holding`（背包金幣） | 推定 |
| 閱讀窗 678 | `sub_7CBA70` 0x7CBDAF–0x7CBDDC | `push this; push 0xA59AB0; …; push 0x2A6` → 回呼是 `sub_A59AB0(閱讀窗)`，只關閱讀窗 | 已驗證 |
| 189 原文 | `ServerMessageText.cs` 既有的查證（`work/issue-77/text-source.json`） | `\f1金幣不足。` | 已驗證（沿用） |
| 窗位置 | UI 原點 `dword_154524C`／`dword_1545250` | 800×600 時是 0 → (0,0) | 已驗證（沿用 #739／V5） |

本任務沒有宣稱缺少任何資源，所以沒有另外跑 PakViewer。

## 怎麼驗證
- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/issue-808/nuget.config`（指向主專案 `engine/vendor`）→ `PASS dev`。productDll `441a983d…`；用的是暫時 lock，沒有寫入。
- `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release` → 0 錯誤。3 個警告是既有的，不在本任務的檔案。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`（4252 檔，0 錯誤）。
- **依規定沒有在本機執行任何測試程式**。`board-followup`、`board-native-content`、`board` 等群組以 PR 的 GitHub Actions CI 為準。
- **CI（PR #831，run 42824799628／42824799597）**：dotnet、map-continuity、npc-offline 全部 pass。`board-followup` 38 項 pass，`board-native-content` 87 項 pass，0 失敗。

## 未驗證什麼
- 沒有開遊戲。要交給 Codex／dot 在真機確認（M17）：
  - WinForms 公告板窗不再出現；
  - 原生三窗在 (0,0)，背景、列與按鈕對齊；
  - 678 回到清單；
  - 金幣不足時 189 出現在聊天列（色 24）；
  - 確認框以遊戲窗為 owner 正常跳出；
  - 544 進聊天列。
- `ShowBoardUi`／`RunHidden`／`AttachNativeBoard` 需要真的 `ClientForm`，只有編譯過，沒有離線測試。
- 隱藏 Form 的 `Close()` → Dispose → `CloseBoard`，是依 WinForms 對「沒有 handle 的 Form」的處理推定的，待真機驗證。
- 原生模式下，`BoardUi` 提示列的其他訊息看不到，包括「未送出…」與伺服器訊息（只有 544 改進聊天列）。
- 544 在聊天列的字色沒查（用預設色）。
- `[this+0x40]` 的寫入者沒查，所以「不動作」這個分支沒做。
- 672／676 的回呼等 Q7-3（不擋，本任務不做）。

## 產出路徑
- `engine/source/NativeUi/BoardPanels.cs`
- `engine/source/NativeUi/BoardPanel.cs`
- `engine/source/BoardUi.cs`
- `engine/_integration/dev/native-ui-tests/BoardPanelChecks.cs`
- `engine/source/BoardFollowupTests.cs`（新）
- `docs/reports/issue-808.md`

## 給下一棒的提示
- 真機 M17 照上面的清單看。如果原生公告板有任何一窗 faulted，`AttachNativeBoard` 會退回 WinForms 窗，看 `UiResourceLog` 的 `board native panels skipped`。
- 672／676（閱讀窗）的回呼是 `sub_7CD910`／`sub_7CD850`，可以用 `py -3 -I docs/reference/rea-scripts/disx.py 0x7CD910 0x40` 查（Q7-3）。查清楚後，在 `BoardButtonWindow.Activate` 接上 `ArticlePrevious`／`ArticleNext`。
- 想把「未送出」等提示也放進原生畫面，可以在 `BoardUi.Run` 的 `!sent` 分支，隱藏時呼叫 `AddBoardChatNotice`。原版沒有這段文字，屬於引擎決定。
