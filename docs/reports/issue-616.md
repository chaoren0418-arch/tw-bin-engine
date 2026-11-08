# Issue #616／T255：R2-G 聊天收尾

依據：`docs/reference/gap-matrix.md` 第 11.5 節 R2-G；`docs/reference/ui-layout.md` §3；`docs/reference/protocol-gaps.md` §6 C_ONOFF；`docs/reference/server-protocol.md` C_ChatParty。

## 做了什麼

| 項目 | 做法 | 檔案 |
|---|---|---|
| `/on`、`/off` 不再當聊天送出 | `LoginConnection.SendPlayerChat` 先用 `ChatChannelCommands.IsCommand` 判斷，是指令就交給 `SendChatChannelCommand`（C26），不送 C136／C40；頻道名稱不明時回報說明文字、什麼都不送。`SendChatChannelCommand` 每次先清空上一次的回報 | `PlayerChat.cs`、`ChatChannelToggle.cs` |
| 喊話改 `[name]` | `line.Text` 保留伺服器字串 `<name> msg`（既有 player-chat-tests 依賴它），顯示時改成 `[name] msg`（`ChatFormat.ShoutText`） | `PlayerChat.cs` |
| `*` 開頭變色 | 一般：第 0 字 `*` 且第 1 字不是 `*` → 黃 13；全體：第 1 字 `*`（如 `[******] msg`）→ 紅 3；另補血盟第 1 字 `(` → 盟主色 47 | `PlayerChat.cs`（`ChatFormat.TextColor`） |
| type 18／25 自帶色號 | S243 type 18（黃金號角）／25（彩色）在字串後多讀 H 色號，1–95 才收，其餘不顯示；歸入全體頁籤 | `PlayerChat.cs` |
| `\f`／`\b` 色碼 | 送出：`\f0`–`\f7`、`\b0`–`\b7` 的數字加 0x22。顯示：`\f`+c 改用調色盤[c−0x30]，`\b`+c 拿掉，`\d0`–`\d7` 拿掉，`\i` 照原樣 | `PlayerChat.cs`（`ChatFormat.Runs`／`EncodeColorCodes`） |
| 血盟第二色 #BDBDBD | `ChatFormat.SecondColor`：血盟 7（#BDBDBD），其他 14。**只提供資料，不畫**（用途待驗證） | `PlayerChat.cs` |
| `$` 前綴 | `$訊息` → 交易，送 C40 type 12 | `PlayerChat.cs` |
| 補送 C199 | `/chatbanish 名稱`（type 0＋S）、`/chatoutparty`（type 1）、`/chatparty`（type 2），走聊天送出閘門 | `PlayerChat.cs`（`ChatPartyCommands`） |
| 完整調色盤 | `ChatPalette`：從 BIN 讀出 96 筆 RGB555；各頻道色改由索引算出（與舊 hex 表逐色相同） | `PlayerHudChat.cs` |
| 兩個畫面都逐段上色 | 舊 HUD 的 RichTextBox 逐段設定顏色；原生聊天窗 `ChatRow.Runs` 在換行時切開顏色段、逐段畫；`Drawn` 每列仍只記一筆（既有測試不變） | `PlayerHudChat.cs`、`NativeUi/State/ChatState.cs`、`NativeUi/ChatPanel.cs` |
| 輸入檢查 | HUD 與原生輸入列都改用 `PlayerChatProtocol.Describe`／`Valid`，指令（/on、/off、C199、/loc）也能判斷有效與否 | `PlayerHudChat.cs`、`NativeUi/State/ChatState.cs` |
| 離線測試 | `[WorldModuleTest("chat-format")]`，93 項 | 新檔 `ChatFormatTests.cs` |

`PlayerChatKeyboardFlow.cs` 不需要改：Enter 開啟／送出流程不變。

## 查證依據（本輪用 capstone 唯讀反組譯 `TW1608262200.bin`，SHA `e12949cf…09641a2`）

地址規則：`.text` FO = VA − `0x400C00`；`.data` 調色盤 FO = VA − `0x401800`。腳本放 `scratch/issue-616/`（不進 git）。

**已驗證**
- S_MESSAGE（op 153，`sub_944F50`／FO `0x544350`）以格式 `cs`（字串 `0x11839F0`）讀 type 與字串；case 18（`0x945460`）、case 25（`0x945514`）再用 `sub_926CA0`（讀 2 byte）取色號，顏色＝`dword_1369D70[色號]`。Printer `sub_6BD720`／`sub_6BD680` 只在 0 < 色號 < 0x60 時輸出。
- 全體（case 3，`0x94525D`）：CP950（`0x3B6`）且字串第 1 字為 `*` → 索引 3，否則 44。血盟（case 4，`0x9452DE`）：第 1 字為 `(` → 索引 47，否則 11。
- S_SAY（op 112，`sub_94B4F0`，`0x94BA36`）：第 0 字 `*` 且第 1 字不是 `*` → 索引 13。
- 輸入轉譯（`sub_7DE200`，`0x7DE516`）：`\f`／`\b` 後的 `0`–`7` 加 0x22；第 3 字在 0x30–0x8F 其他值照抄。
- 血盟 Printer `sub_6BDAA0` 的第二色是 7（ui-layout.md §3）；調色盤 96 筆讀自 VA `0x1369D70`，與 ui-layout.md 表的 12 個頻道色逐一相符。
- 3.81 伺服器：`S_ChatShouting` 送 `<name> msg`；`C_ChatGlobal` 的 type 12 是交易；`C_ChatParty`（C199）type 0＝S 名稱、1、2。伺服器不送 S243 type 18／25（只有合成封包測試）。

**推測**
- 顯示端 `\f`+c → 調色盤[c−0x30]：與 ui-layout.md「數字＋34」一致，但繪字函式本身沒有逐指令讀完。`\b` 的用途（背景或第二色）未查，只拿掉不畫。
- `$`→交易、type 18／25 歸全體頁籤、C199 的指令字（取自伺服器原始碼註解）。
- 本引擎固定 CP950，所以全體 `*` 紅字的 CP950 條件一律成立。

**待驗證**
- 第二色（`sub_7DC850` 第 3 個參數）在畫面上的用途；目前只提供 `ChatFormat.SecondColor`。
- S_MESSAGE 路徑血盟的第二色是 14，Printer 路徑是 7；3.81 封包實際走哪條路徑未實機確認。
- 原版 `/on`、`/off` 的確切語法（沿用 T231 的推定格式）。
- 真機：沒有啟動遊戲，沒有實際看到上色效果。

## 怎麼驗證

- `rebuild.py --update-lock` → `rebuild.py --build --configfile .github/nuget.ci.config`：PASS（新增檔案，所以重產 lock）。
- `P11.ClientEngine.exe --module-tests`（照 CI 方式）：pass，28 組、1463 項，0 失敗；`chat-format` 93 項。
- 既有 `player-chat-tests`：PASS 138 項（行文字、`FormatLine`、頻道色、RichTextBox 行色、Global(18) 無色號被拒都沒變）。
- `demo-tests native-ui-chat-window`：PASS 446 項（真畫面渲染因沒設 `TW_CLIENT_ROOT` 跳過）。
- `check-no-game-assets.py`：pass，0 錯誤。
- 合併 origin/main 後重跑結果見文末「同步」。

## 沒有驗證的

- 沒有啟動遊戲、伺服器或 DB；沒有真機看顏色與指令回應。
- 原生聊天窗的實際渲染（需要客戶端素材）只跑了離線邏輯檢查。

## 產出

- `engine/source/PlayerChat.cs`、`PlayerHudChat.cs`、`ChatChannelToggle.cs`、`NativeUi/ChatPanel.cs`、`NativeUi/State/ChatState.cs`
- 新檔 `engine/source/ChatFormatTests.cs`
- `engine/_integration/dev/rebuild-lock.json`（`--update-lock` 重產）
- 本報告 `docs/reports/issue-616.md`
- 本機證據（不進 git）：`scratch/issue-616/`（反組譯腳本、module-tests.json、chat-checks.json、chat-window.json）

## 給下一棒的提示

- 顯示一律用 `ChatFormat.Runs(line)`／`DisplayText(line)`；`line.Text` 是伺服器原字串，`PlayerChatProtocol.FormatLine` 保持舊行為（座標分享測試也依賴它）。
- `PlayerChatChannel` 不能再加值：既有 player-chat-tests 要求「每個頻道都有調色盤色」且數量等於列舉長度。新類型請像 18／25 一樣用 `ColorIndex` 或其他 init 屬性表達。
- 要畫第二色或 `\b` 背景前，先查 `sub_7DC850`（VA `0x7DC850`／FO `0x3DBC50`）與繪字函式（候選 `sub_C01B70`、`0x7DD0B5` 一帶）。
- `/on`、`/off` 之外的頻道選項視窗（原版 C_ONOFF 選項窗）仍沒做。
- `%`→同盟（C136 type 13）伺服器有處理，但不在本任務範圍，沒有加。
