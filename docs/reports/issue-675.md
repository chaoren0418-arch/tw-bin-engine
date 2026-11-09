# #675 T282：R3-G 聊天收尾二

依據：`docs/reference/gap-matrix.md` §12.5（R3-G）、`docs/reference/ui-layout.md` §3、`docs/reference/server-protocol.md`（S_NpcChat／S_NpcChatShouting／S_NpcChatGlobal、S_ServerMessage）。

## 做了什麼

| 項目 | 內容 | 檔案 |
|---|---|---|
| op 61 S_SAY_CODE（3.81 S161） | `PlayerChatProtocol.TryDecode` 新增 S161 `C type D id S text`：type 0 → 一般 Printer（`name: msg`）、type 2 → 喊話 Printer（伺服器送 `<name> msg`，顯示 `[name] msg`）、type 3 → 全體 Printer（`[name] msg`）。字串照伺服器原樣，不加本機前綴。**id 0**（S_ServerMessage／S_BoxMessage／S_SystemMessage 等系統文字）與其他 type 不當聊天，仍留給 `Inventory.cs` 的伺服器訊息處理。 | `PlayerChat.cs` |
| 代碼聊天顏色 | `PlayerChatLine.Code` 標記 op 61 的行；顏色用同類型 Printer 的頻道色（白 14／喊話 33／全體 44）。`*` 黃字（S_SAY `0x94BA36`）與全體 `*` 紅字（S_MESSAGE `0x94525D`）是處理函式裡的判斷，不在 Printer，所以代碼聊天不套用。 | `PlayerChat.cs` |
| op 104 S_TELL（3.81 S73） | 格式在 #449 已改成原版 `(name) msg`（#FFCECE，32）；送密語回覆 S243/9 `-> (target) msg`（#FFF7B5，24）。本任務沒有改程式，補上離線測試鎖定格式、顏色、第二色與 C184 送出格式。 | `ChatFollowupTests.cs` |
| 原生聊天窗高度切換 | `ChatPanel.Load` 改呼叫 `ChatViewState.SetHeights`（原本每次 new 一個新的 view）：重新載入版面時保留分頁、過濾、分割，目前高度夾到新的高度階，捲動歸零。新增 `ChatPanel.SetHeight(h)`（走 `ChatViewState.SetHeight`）給宿主或設定還原使用。另外加 `Load(layout, Func<int,UiSurface?>)` 多載，讓離線測試可用記憶體內的 surf；`Load(layout, SurfCatalog)` 行為不變。 | `NativeUi/ChatPanel.cs` |
| 第二色（Q3-9） | **只做資料，不畫。** `ChatRow.SecondColor` 帶 `ChatFormat.SecondColor(line)`（血盟 7 #BDBDBD，其他 14），換行後每一列都相同。畫面上的用途等 Q3-9 查證。 | `NativeUi/State/ChatState.cs`、`PlayerChat.cs`（註解） |

沒有改 `PlayerHudChat.cs`：舊 HUD 已經透過 `ChatFormat.Runs` 顯示，S161 的行自動套用相同格式與顏色。沒有改 `UiPanelRegistry.cs`、`WorldSession.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

## 怎麼驗證

- `rebuild.py --build`：**PASS dev**（暫時 lock，`rebuild-lock.json` 未修改），productDll SHA-256 `7e2dcab9ce88fc03798be440c5a4bb305a6e3556f9a713c7e3d4591afe770520`。
- `--module-tests`：47 組、2362 項、0 失敗；新組 `chat-followup` 49 項通過；既有 `chat-format` 93 項、`chat-channel-toggle` 92 項通過。
- DemoChecks `native-ui-chat-window`（既有原生聊天窗測試，因為改了 `ChatPanel.Load`）：PASS 446。
- `check-no-game-assets.py`：pass，0 錯誤。
- `chat-followup` 涵蓋：S161 type 0／2／3 的頻道、speaker、顯示文字、顏色；`*` 規則只在 S81／S243 生效；S161 type 1／4 與 id 0 不當聊天；經 `WorldSession.Receive` 實際分派，NPC 代碼聊天進聊天歷史，id 0 仍是 `LastServerMessage`；S73／S243-9 密語格式、顏色；C184 送出；`ChatRow.SecondColor` 跨換行；原生聊天窗兩組 XML（(129,470)/36 與 (30,400)/20）重新載入後高度階、高度夾值、分頁／分割保留、`SetHeight` 夾值、上下鈕循環。

本機建置環境備註：使用者 NuGet 快取與沙箱下的 restore 會漏掉 WebView2 的 `build/*.targets`，出現 NETSDK1130。做法是 `scratch/issue-675/NuGet.Config` 唯讀指向主專案 `engine/vendor`，`NUGET_PACKAGES` 指到 `scratch/issue-675/packages`，在沙箱外執行。這和程式變更無關。

## 未驗證什麼

- **S_SAY_CODE（`sub_94AF00`／FO `0x54A300`）本身沒有逐指令讀**：「type → Printer」的對照與「代碼聊天不套用 `*` 規則」是依 UI§3 工廠 `sub_6BC550` 與 `*` 判斷所在位置推得的（推測）。
- NPC 名稱可能是 `$數字`（`npc.getNameId()`）：目前照原字串顯示，沒有查 NPC 名稱表（待驗證；名稱表屬別的任務）。
- S161 type 2 + id 0（`S_SystemMessage(msg, nameid)`）仍然沒有任何接收端，跟本任務之前相同。
- 第二色的畫面用途（Q3-9）：只帶資料，不畫。
- 沒有真機測試（不啟動遊戲／伺服器）；CI 結果以 PR 頁為準。

## 產出路徑

- `engine/source/PlayerChat.cs`、`engine/source/NativeUi/ChatPanel.cs`、`engine/source/NativeUi/State/ChatState.cs`
- 新檔 `engine/source/ChatFollowupTests.cs`（`[WorldModuleTest("chat-followup")]`）
- 本報告 `docs/reports/issue-675.md`
- 本機證據（不進 git）：`scratch/issue-675/module-tests.json`、`scratch/issue-675/chat-window.json`

## 給下一棒的提示

- Q3-9 查完後：把 `ChatRow.SecondColor` 接到 `ChatPanel.Rows` 的繪製（例如描邊或陰影色），舊 HUD 視需要比照。
- 若查證 S_SAY_CODE 也有 `*` 判斷，只要拿掉 `ChatFormat.TextColor` 裡 `if(line.Code)` 那一行。
- NPC `$` 名稱表接上後，S161 的 `name` 段可在 `ChatFormat.Runs` 前替換（`line.Text` 要保留伺服器原字串）。
- 若要保存聊天窗高度（設定檔），用 `ChatPanel.SetHeight` 還原；`Load` 已經不會把高度重設。
- dot 真機 smoke：找會喊話的 NPC（S161 type 2）與一般對話 NPC（type 0），確認聊天窗顯示 `[名稱] 訊息`／`名稱: 訊息` 與顏色；GM 公告（S161 id 0）仍走伺服器訊息。
