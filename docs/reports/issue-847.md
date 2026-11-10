# Issue #847 T363：R9-C 聊天指令與 S161 文字

依據：`docs/reference/followups-9.md`（FU9）§1.1、§4、§9，第 16.2 節 R9-C；`gap-matrix.md` §15.11（1:61、3:C_INVITE_PARTY、3:C_ADD_BUDDY）。

## 做了什麼

### 聊天指令（`PlayerChat.cs` 只加分派表與分派；內容放在各模組）

| 指令（中文別名＝字串號） | 行為 | 檔案 |
|---|---|---|
| `/invite`（410 邀請隊員） | 不帶名稱：進入選目標模式（印 179「請選擇對象。」），點中 30 格內的玩家後送 **C230 type 0 D id**（`PickPartyInviteTarget`）。帶名稱：3.81 沒有 type 4／5，**不送**，提示 | `Party.cs` |
| `/chatinvite 名稱`（957 邀請聊天） | **C230 type 2 S 名稱**（新增 `PartyPackets.ChatInvite`）；不帶名稱印 960 並提示格式 | `Party.cs` |
| `/banish 名稱`（411 驅逐隊員） | C255 S 名稱 | `Party.cs` |
| `/outparty`（412 離開隊伍） | C33 | `Party.cs` |
| `/buddy`（947 好友名單） | C4 | `Buddies.cs` |
| `/addbuddy 名稱`（948 新增好友） | 名稱檢查失敗印 **53**；成功送 C207＋C4（原版 138＋56） | `Buddies.cs` |
| `/delbuddy 名稱`（949 刪除好友） | C202＋C4 | `Buddies.cs` |
| `/exclude 名稱`（366 斷絕） | C171 S 名稱（**3.81 推測，M37**：原版固定傳空字串）；已在封鎖名單時不送（3.81 C171 是切換，會變成解除），提示改用 `/include`；等待回覆時不再送 | `Buddies.cs` |
| `/include 名稱`（470 包含） | 原版 C217，3.81 沒有；改用 C171 切換，只在名稱已在伺服器回覆的封鎖名單時送 | `Buddies.cs` |
| `/trade`（399 交換） | 面前一格是擺攤玩家 → 印 **989**、不送；否則送 C2。面前有面向自己的玩家時本機交易狀態進入「邀請中」（同 WinForms 鈕）；沒有時照原版送出、狀態不動。已有交易流程時不送（引擎自加） | `Trade.cs` |
| `/shop`（907 商店） | 自己擺攤中（S119 動作 70）→ C38 type 1 並印 **903**；否則切換既有的個人商店設定窗 | `Trade.cs` |
| `/bookmark 名稱`、`/記憶 名稱`（365） | C165（既有 `BookmarkPackets.Add`）；名稱 > 30 byte 印 **204**；不帶名稱＝切換既有的原生書籤窗（`ToggleNativeBookmarkWindow`） | `PlayerChat.cs` |
| `/qbookmark 名稱` | 同樣送 C165；原版另把新書籤加進快速移動清單——那份清單在 `BookmarkPanel.cs`，不在本任務範圍，**沒做** | `PlayerChat.cs` |

- 指令表 `ChatFollowupCommands`：英文不分大小寫（推測，同既有指令）、以第一個空白切開、參數去頭尾空白；`Describe`／`InputHelp` 一併列出。
- 送出走聊天列同一個閘門（戳記、1 秒防連點、lease）；`/addbuddy`、`/delbuddy` 第一包放行後才送第二包 C4。
- 開窗：`ClientForm.OfferChatWindow`（寫在 `PlayerChat.cs` 的 `ClientForm` partial）用 `Application.OpenForms` 找擁有這個 session 的視窗（同 `KickNotice.cs` 的做法），只呼叫既有入口，沒有改其他檔案。原生 UI 沒啟動（含 `--classic-ui`）時回 false，聊天列提示「未開啟」。
- 本機印的字（53、179、204、903、960、989）：執行期從 `string-c.tbl` 讀並去色碼；讀不到時用本機 Text.pak `string-c.tbl` 查到的原文（`ChatFollowupCommands.Text`）。

### S161 文字（列 1:61）

- `PlayerChatProtocol.CodeText`：`$` 展開之後**換第一個 `^`、第一個 `!` 為空白**，開頭是 `\f` 且第 3 字元 ≥ `'0'` 時**拿掉這 3 字**；中間的 `\f` 不動。對解碼後的字元操作，等於 DBCS 安全的換法（原版的位元組 `strchr` 遇到 Big5 第二位元組 0x5E 會換錯，推測是原版缺陷，不仿）。
- `PlayerChat.cs` case 161 後處理套用；`Inventory.cs` 的 type 0＋id 0 系統文字也套用（含 `$` 展開）。公告分類（`SystemAnnouncements`）仍比對原字串，所以 `\fR…` 公告照舊。type 2＋id 0 3.81 沒有呼叫點，照舊不收；786 分支 3.81 不會出現，不做。

### 其他小改

- `Trade.cs`：`FacingTradeTarget` 改成呼叫新的 `FacingTradePeer(候選)`（判斷相同），另加 `PlayerAhead`（面前一格唯一玩家，不需選取）與 `BeginTradeInvite(WorldObject)` overload。
- `Party.cs`：`PartyInvitePick` 狀態（同世界、同角色才有效）、`PartyInviteCandidate`（玩家旗標、活著、非自己、30 格內）。

## 怎麼驗證

- **已驗證（本機）**：`dotnet build engine/source/OfflineClient.csproj -c Release` 0 個錯誤；本次改到的檔案沒有新警告。本機 SDK 對 WebView2 winmd 會報 NETSDK1130（既有環境問題，#676–#706 都遇過，與本次修改無關），所以編譯時暫時加了 `scratch/t847/nowinmd.targets`（未入 git）並把 NuGet 套件還原到 `scratch/t847/nuget2`。
- **已驗證**：`python engine/tools/check-no-game-assets.py` → `pass`（4330 檔，0 錯誤）。
- **待 CI**：新的產品內離線測試 `[WorldModuleTest("chat-command-r9c")]`（追加在既有的 `ChatCommandFollowupTests.cs`，類別 `ChatCommandR9cTests`），依規定本機不執行，交給 GitHub Actions 的 `--module-tests`。內容：
  1. 指令字與別名、非指令、`Describe`、不與 `/chatparty`、`/duel`、`/who` 等重疊；
  2. `CodeText` 各情況與 S161 type 0／2 解碼；
  3. id 0 系統文字去 `\fY`、換 `!`／`^`，公告分類不變；
  4. `/invite 名稱` 不送、`/chatinvite` → C230 2、`/banish` → C255、`/outparty` → C33；
  5. `/invite` 選目標：31 格、非玩家、自己都不送，30 格內玩家送 C230 0，世界重置失效；
  6. `/buddy`、`/addbuddy`（53）、`/delbuddy` 的封包序列；
  7. `/exclude`、`/include` 的 C171 與名單條件；
  8. `/trade`：擺攤 989、面對面進入邀請中、沒面向自己照送 C2；
  9. `/shop`：擺攤中 C38 1＋903，否則只開窗；
  10. `/bookmark`／`/記憶`／`/qbookmark` 的 C165、204、開窗；
  11. 有字串表時照表並去色碼。
- 字串來源（唯讀解出，沒有寫進 repo）：`docs/reference/rea-scripts/t680/t680_strings.py` 讀 `D:\lineage開發\0-客戶端\Text.idx／Text.pak` 的 `string-c.tbl`（3484 筆，decoded SHA-256 `4ebbc1a7a002a4c1dcdfa73dbf056b0e00648e0789763410c40e5079a22d43e9`）：53、179、204、365、366、399、410、411、412、470、582、588、589、903、907、947、948、949、957、960、989、2120。本任務沒有宣稱缺任何資源，所以沒有做 PakViewer 全 pak 搜尋。

## 未驗證什麼

- **真機再確認**：所有指令在 P24Lab 3.81 伺服器的實際回應（特別是 `/exclude 名稱` 的 C171 推測、`/include` 用 C171 切換、`/trade` 沒有面向自己時伺服器的回覆）。
- **`/invite` 的點選接線沒做**：選目標模式只有狀態與 `LoginConnection.PickPartyInviteTarget(id)`；畫面點擊要由 `PlayerPointer.cs` 在 `World.PartyInvitePicking` 時呼叫它（`PlayerPointer.cs` 不在本任務可改檔案）。游標 179 也沒換（同 SpellTargeting，推測）。**所以目前產品裡 `/invite` 只會進入選目標模式，點人還不會送。**
- `/shop`、`/bookmark` 開窗只在原生 UI 時有效；`--classic-ui` 沒有對應入口（沒有切到 WinForms 分頁）。
- 沒做：`/shop` 釣魚中印 2120（引擎沒有釣魚狀態）；`/qbookmark` 加入快速移動清單；書籤限制地圖的本機 214 檢查（部分地圖只限區域，區域表待查；3.81 伺服器同樣回 214）；`/chatinvite`、`/addbuddy` 不帶名稱時原版的 960／588 輸入框（只印提示）；`/exclude` 不帶名稱時原版開封鎖名單頁；隱身判斷（sub_A42C30）。

## 產出路徑

- 程式：`engine/source/PlayerChat.cs`、`Party.cs`、`Buddies.cs`、`Trade.cs`、`Inventory.cs`
- 測試：`engine/source/ChatCommandFollowupTests.cs`（追加 `ChatCommandR9cTests`，模組名 `chat-command-r9c`；檔名任務單寫「新檔」，但 #781 已有同名檔，所以追加類別、沒有覆寫）
- 報告：`docs/reports/issue-847.md`
- 分支：`task/847`

## 給下一棒的提示

1. **`PlayerPointer.cs`**：`Select(id)`／`ActAsync` 開頭比照 `TryPickSpellTarget`，在 `login.World.PartyInvitePicking` 時改呼叫 `login.PickPartyInviteTarget(id)`；點地面時 `World.CancelPartyInvitePick()`。這一步做完 `/invite` 才能完整使用。
2. `BookmarkPanel.cs`：`/qbookmark` 的「新書籤也加進快速移動清單」（原版 `this+486`＝1，收到 S92 時加入）。
3. 動作窗（R8-D）可以直接重用：`Action_InviteParty` → `/invite` 流程、`Action_Trade` → `TradeChatCommand`、`Action_OpenShop` → 聊天指令 `shop`。
4. 真機驗收時請記錄 `/exclude 名稱`、`/include 名稱`、`/trade`（沒有面對面）在 3.81 的回覆，確認 M37 推測。
