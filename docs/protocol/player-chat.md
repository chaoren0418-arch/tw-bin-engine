# T106 多頻道聊天契約（#193）

依據 #132 的 [封包表](../reference/server-protocol.md) 與 [規則表](../reference/server-rules.md)。本次唯讀核對 `D:\lineage開發\1-伺服器\src\com\lineage\server\clientpackets` 的三個處理器；來源 SHA-256 與 #132 相符，未複製伺服器原始碼。

| 來源檔 | SHA-256 |
|---|---|
| C_Chat.java | `72257d44684061e922f79dcd8f7b5ed4604ccaffd875506d2457a00a1873f4b2` |
| C_ChatGlobal.java | `5c673ed8aeaa1c5ec82b8edeb10ae670d8b99321c1042313da607b8bcd8d8708` |
| C_ChatWhisper.java | `44d67c3dccbdbe6842ec3a59b6c7c3dcb512e98c8182d79ae563a31d5b87f8ff` |

## 欄位與輸入

`C/H/D` 是 1/2/4 bytes，小端序；`S` 是 CP950 字串加 NUL。整個明文封包依既有契約補零至 8 bytes 倍數。

| 頻道 | 輸入方式 | 送出 | 接收 | 本機顏色／前綴 |
|---|---|---|---|---|
| 一般 | 直接輸入；`/say 原文` 可保留開頭的命令符號 | C136 C0 S(text) | C81 C0 D(speaker) S(formatted) | White／`[一般]` |
| 喊話 | `!訊息` | C136 C2 S(text) | C81 C2 D(speaker) S(formatted) H(x) H(y) | Orange／`[喊話]` |
| 全體 | `&訊息` | C40 C3 S(text) | C243 C3 S(formatted) | LightSkyBlue／`[全體]` |
| 密語 | `"對象 訊息`（半形引號起頭，不需結尾引號） | C184 S(receiver) S(text) | C73 S(sender) S(text)；送件者回覆為 C243 C9 S(formatted) | Violet／`[密語]` |
| 血盟 | `@訊息` | C136 C4 S(text) | C243 C4 S(formatted) | LightGreen／`[血盟]` |
| 隊伍 | `#訊息` | C136 C11 S(text) | C243 C11 S(formatted)；另接受 C81 C14 D(speaker) S(formatted) | Yellow／`[隊伍]` |

`C_Chat.start` 的 type 2/4/11 分別呼叫喊話／血盟／隊伍處理；`C_ChatGlobal.start` 的 type 3 呼叫廣播處理。type 14 是另一種聊天隊伍回包，本次接收相容；送出選一般隊伍 type 11。交易、聯盟的獨立送出不是本任務範圍；聯盟回包與血盟共用 type 4，保留伺服器的雙大括號。

三個處理器都以 Java `String.length()` 限制正文不超過 52，對應 C# UTF-16 `Length`；不是 52 個 Big5 bytes。命令前綴與密語對象不計入正文。空白正文、控制字元、無法編成 CP950 的字元由本機拒絕。密語對象另設本機 52 字上限且不得含空白；此上限不是對伺服器角色命名規則的宣稱。UI 輸入上限 106 字容納完整密語語法。

## 分派與狀態邊界

- 沿用 `ReceiveCore` 既有 `ReceivePlayerChat` 掛接；不改共用分派檔案。解析器使用自己的 reader，未知 opcode/type 不改動後續模組的讀取位置。
- 喊話必須讀完 x/y 再核對 padding；無形角色的 speaker=0 仍可顯示。座標只消耗欄位，不作為移動或目標狀態。
- `S243/type9` 同時是 `S_SystemMessage` 和 `S_ChatWhisperTo` 的容器。只有符合來源的 `-> (非空對象) 訊息` 外形才歸入密語，其他訊息繼續交給既有 Inventory 系統訊息處理。協議沒有額外來源標記；若系統刻意產生完全相同外形，無法由封包區分，這是格式限制。
- 所有頻道共用既有 session/world/actor、死亡／場景鎖、傳送中和 1 秒本機防連點檢查。伺服器的禁言、等級、血盟／隊伍資格、全體頻道消耗與間隔仍由伺服器裁定，不預扣資源、不偽造成功回聲。
- 歷史只追加完整有效回包，保留最近 100 筆；保留伺服器格式並加頻道前綴、移除控制字元。密語收件顯示對象與「-> 我」，送件顯示伺服器 `-> (對象)` 回覆。
- 鍵盤開啟 Enter、重複按鍵抑制、Esc 關閉沿用既有流程，未改 `PlayerChatKeyboardFlow.cs`。

## 驗證與待驗

`engine/_integration/dev/player-chat-tests/` 對實際產品 DLL 餵入合成封包，測送出 bytes、接收分派、系統訊息回歸、錯誤封包原子性、狀態拒送、歷史上限和未顯示 WinForms 控制項的顏色／驗證。測試不是實際遊戲驗收。

M6 smoke 由 dot 準備兩個測試角色，逐一檢查六頻道的雙端送收、密語方向／離線對象錯誤、血盟／隊伍資格拒絕、全體條件與消耗、顏色與前綴、不同解析度的輸入提示，以及 Enter／Esc 和中文輸入。來源與實際部署 JAR 的一致性也留待真機確認。
