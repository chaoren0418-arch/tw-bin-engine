# Issue #809 T346：R7-G 私商提示（code）

分支：`task/809`。依據：gap-matrix 第 15.6 節 R7-G、FU7§6.1、#779 報告。

## 重要更正：1028 不是「拒絕」，是「高價確認 Y/N」
用 PakViewer CLI（唯讀）從本機 `D:\lineage開發\0-客戶端\Text.idx` 抽出 `string-c.tbl` 到 `scratch/t809/`（不進 git），讀到的原文如下：

| 編號 | 原文 |
|---|---|
| 897 | 不能一次販賣8樣以上的物品。（**沒有 `%s`**） |
| 934 | 販售 |
| 1028 | 這是高價物品。請再次進行確認，確定要購買嗎？(Y/N) |

接著用 `docs/reference/rea-scripts/disx.py`（Capstone，唯讀）反組譯 `TW1608262200.bin`：

| 項目 | 位置（VA） | 結果 | 等級 |
|---|---|---|---|
| 897 的顏色 | `sub_A2BB90` 0xA2BC1C–0xA2BC5E | 呼叫 `sub_7DC850(字, dword_1369D70[24], [14], 7, 897, 0)`，印在聊天列，色 24 | 已驗證 |
| 897 是否一定印 | `sub_A11280` 0xA11280 | 函式本體是 `xor al,al; ret`，永遠回 0，所以一定印 | 已驗證 |
| 897 的 sprintf | 0xA2BBEC–0xA2BC13 | 格式是 897、參數是 934。897 沒有 `%s`，所以結果就是 897 原文 | 已驗證 |
| 1028 的框 | `sub_4FD770` 0x4FD79B–0x4FD802 | 呼叫 `sub_AC98E0(dword_13CE3F4, 字, 1, 回呼 0x4FE4A0, 1, 0)`，是聊天輸入列的提示（同 FU7§1.5 的刪除確認），不是彈出框 | 已驗證 |
| 1028 的回呼 | `sub_4FE4A0` 0x4FE4A0–0x4FE4CA | 答覆經 `sub_A58380` 判定是 Y 時，呼叫 `sub_4F9C20()`（＝0x1541D0C）的 `sub_DE8E90`；之後一律呼叫 `sub_7DCE00` | 已驗證 |
| 答 Y 之後會送出 | `sub_DE8E90` | 裡面有同樣的 14／>8／897 檢查，之後 `push 0x59`，再呼叫 `sub_9FB4D0`／`sub_9FBDB0`（組包並送出） | 推定（沒有逐條追封包內容） |

#779 把 1028 當成「合計超過就不送」。實際上原版是「先問 Y/N，答 Y 就送」。

## 做了什麼
- **`engine/source/PrivateShopTradeSource.cs`**
  - **897**（模式 14 一次超過 8 筆）：改用既有的聊天列寫入入口 `WorldSession.AddBoardChatNotice`，色 24。照舊不送、不關窗、不動版本。`PlayerChat.cs` 沒有改。
  - **1028**（模式 13 合計 >99999）：這一包先不送。
    - 提示寫進聊天輸入列 `playerChatNotice`，和 R6-E 刪除確認用的是同一個入口。
    - 這一包記成 `PrivateShopConfirmCurrent`，其中包含列、版本、世代、世界、目標、店主。
    - 以下任一情況會讓這筆確認失效：清單版本、世代、世界、目標或店主改變；清單關閉；原生窗沒有接上；直接送出了另一包。
  - **`AnswerPrivateShopConfirm(typed)`**：
    - 答覆是 Y 開頭（`YesNoProtocol.Accepts`，全形也算）時，用原來那一包再走一次 `SendPrivateShopBatch`。所有驗證照舊，只跳過 1028 這一關。
    - 其他答覆一律不送，並收掉提示。
  - `PrivateShopTrade` 新增以下成員，`Refusal` 保留給舊的呼叫端：
    - `NeedsConfirm`、`TooManyRows`；
    - `RowsColor`＝24、`RowsSecondColor`＝14；
    - 原文備援 `TotalFallback`／`RowsFallback`：沒有字串表時用本機表的原文，取代原本的引擎占位文字。
  - 新增 `PrivateShopLegacyYields`：原生模式 13／14 的窗開著時為 true。
- **`engine/source/PrivateShopUi.cs`**
  - 原生私商窗開著時，WinForms 私商的控制項全部隱藏：清單、提示、數量、合計、送出、查詢／關閉鈕。
  - 個人商店設定 `personalShopActions` 不受影響。
  - `--classic-ui`、或原生 UI 沒有接上時，`PrivateShopNativeWindow` 是 false，WinForms 面板照舊顯示。
- **新檔 `engine/source/PrivateShopFollowupTests.cs`**：`[WorldModuleTest("private-shop-followup")]`，自動發現。涵蓋：
  - 897／1028 的字：沒有表、表裡沒有 `%s`、表裡有 `%s`、`\f1` 色碼去掉；
  - 897 進聊天列、色 24、沒有說話者、不是輸入列提示、不送、窗不關；
  - 1028 進輸入列提示、不進聊天紀錄、記下這一包；
  - 答 N 不送、答 Y 送出同一包（逐 byte 比對）、重複答 Y 不重送；
  - 合計剛好 99999 時直接送出、不問；
  - 確認在以下情況失效：新清單、關閉清單、直接送出另一包、原生窗沒接上；
  - 讓位判斷：idle、classic、模式 13、模式 14、關閉、空清單。
- 沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`PlayerChat.cs`、`rebuild-lock.json`。
- 既有測試 `private-shop-trade` 仍比對 `PrivateShopNotice==TotalText()／RowsText()`，所以私商提示照舊也寫一份，那組測試不必改。

## 怎麼驗證
- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/t809/nuget.config`（指向主專案的 `engine/vendor`）→ `PASS dev`。用的是暫時 lock，沒有寫入；productDll `2b183095…`。
- `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release` → 成功。警告都是既有的（`DisplayNoticeChecks.cs`、`MacroChecks.cs`）。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`（4256 檔，0 錯誤）。
- 依規定，本機沒有執行任何測試程式。`private-shop-followup` 與 `private-shop-trade` 以 PR 的 GitHub Actions CI（map-continuity 的 `--module-tests`）為準。
- **CI（PR #833，run 42830679959／42830680151）**：dotnet、map-continuity、npc-offline 全部 pass。
  - `private-shop-followup`：58 項 pass；
  - `private-shop-trade`：82 項 pass；
  - 0 失敗。

## 未驗證／限制
- **答 Y 的入口還沒接上（需要下一張任務）**：聊天輸入列的答覆分派在 `PlayerChat.SendPlayerChat`（現在只轉給刪除確認 `AnswerItemDelete`），本任務規定不能動這個檔案。所以目前：
  - 1028 會顯示，但玩家打 Y 時，這一列會被當成聊天送出，這一包不會送；
  - 合計 >99999 的私商購買仍然買不成，和 #779 的行為相同，沒有變差。
  - 接上的方法只要一行：在 `SendPlayerChat` 開頭比照 `ItemDeletePromptActive` 加上 `if(PrivateShopConfirmCurrent!=null){await AnswerPrivateShopConfirm(text);return true;}`（這一列當作答覆、不當聊天送出；回傳語意由接手者核對）。
  - 原生 YES／NO 鈕（`YesNoPanel`）目前只接伺服器詢問，要不要也接 1028 一併決定。
- **原生 `ChatPanel` 不畫 `playerChatNotice`**，刪除確認（#781）也有同樣的問題。原生模式下，1028 只會出現在狀態列（`Set(State, …)`，引擎做法）與私商提示。要畫在輸入列 (192,581)，需要動 ChatPanel 或 YesNoPrompt，不在本任務範圍。
- `sub_7DCE00`（回呼最後一定會呼叫）沒有查。答 Y 之後原版是否關窗也沒有查；引擎照 #779，送出後清單進入 submitted、窗關閉。
- WinForms 讓位（`SyncPrivateShop`）需要真的 `ClientForm`，只有編譯過；判斷函式 `PrivateShopLegacyYields` 有離線測試。
- **待真機（交給 Codex／dot，P24Lab 測試角色）**：
  - 原生私商窗開著時，WinForms 私商分頁看不到清單與按鈕；
  - 一次賣 9 筆時，897 以色 24 出現在聊天列；
  - 合計 >99999 時出現 1028（目前在狀態列）。
- 本任務沒有宣稱缺少資源。查過的 pak 只有 `Text.idx`（`string-c.tbl`）。

## 產出路徑
- `engine/source/PrivateShopTradeSource.cs`
- `engine/source/PrivateShopUi.cs`
- `engine/source/PrivateShopFollowupTests.cs`（新）
- `docs/reports/issue-809.md`

## 給下一棒的提示
- 開一張小任務，可改的檔案是 `PlayerChat.cs` 與 `NativeUi/ChatPanel.cs`（或 `YesNoPrompt.cs`），內容兩項：
  1. `SendPlayerChat` 把答覆轉給 `AnswerPrivateShopConfirm`；
  2. 原生輸入列畫出 `playerChatNotice`。刪除確認（R6-E）也需要這一項。
- 接上之後，`PrivateShopFollowupTests.BuyConfirm` 已經涵蓋 Y／N 的協定部分，只要再加一段「從 `SendPlayerChat` 打 y」的測試。
- 要查 1028 回呼的後續，可以用 `py -3 -I docs/reference/rea-scripts/disx.py 0x7DCE00 0x80`。
