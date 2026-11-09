# 查證補遺第 6 輪：血盟關注、盟戰、求婚、盟屋窗、代碼聊天名稱、世界地圖

T304（#716）。查 [`gap-matrix.md`](gap-matrix.md) 第 13.7 節的 **Q4-3 到 Q4-9**。簡稱：本文 **FU6**，引用寫 `FU6§n`。

每節分「原版怎麼做／3.81 是否支援／對應要改哪個檔案」。記號：**已驗證**＝反編譯、反組譯、原始碼或資源檔直接看到；**推測**＝依欄位、命名或旁證判斷；**待驗證**＝本輪查不到，寫明卡在哪裡。

## 0. 方法與來源

- **TW16 BIN**：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（本輪重算）。
- **TW16 IDB**：P01 `ida/03-ascii-isolated/TW1608262200.idb` 複製到本 worktree 的 `scratch/rea/ida/tw16.idb`，SHA-256 `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f`（與 FU5 相同）。本輪沒有在副本上建函式或改名。
- IDA 7.7＋Hex-Rays，唯讀批次 10 輪（q1–q10），腳本沿用 `rea-scripts/t690/ida_t690.py`，查詢清單在 `rea-scripts/t716/queries.txt`（第 8 節）。
- **subst**：每輪前後都跑 `subst`，開始時沒有任何代號被占用；本輪一律用 `W:`，每輪結束後 `subst` 都沒有輸出（沒有殘留）。
- **送出點**：`rea-scripts/wo_sendsites.py 201 197 21 33`（掃 `sub_9FBC00` 呼叫點的 opcode）；**沒有呼叫者的函式**另用 `rea-scripts/t716/t716_callscan.py` 線性掃 `.text` 的 `E8 rel32` 與 32 位元立即值（能抓到 IDA 沒切成函式的程式）。
- **地址換算**：程式碼 FO＝VA − `0x400C00`；`.rdata` FO＝VA − `0x401400`；`.data` FO＝VA − `0x401800`（同 FU4§0、FU5§0）。bss 全域沒有 FO。
- **本機資源（只讀，不進 git）**：`D:\lineage開發\0-客戶端` 的 `Text.pak`（在記憶體解出 `string-c.tbl`，`decoded_sha256` `4ebbc1a7…43e9`，3484 筆）；`releases/r29/assets/objects/names.json`（16945 筆，第 4 筆「金幣」、第 431 筆「沙哈的戒指」，與 FU4 的 `desc-c.tbl` 相同）與 `releases/r29/assets/dialogs/templates.json`（只讀）。
- **3.81 伺服器**（`D:\lineage開發\1-伺服器\src`，唯讀；SHA-256 前 16 碼）：`C_ClanAttention.java` `29bc590d3fb56172`、`C_War.java` `733980808f998f81`、`C_Propose.java` `81d8dd895063670d`、`S_War.java` `a9eea0aa678c0211`、`S_NpcChat*.java`。
- **引擎**：本分支 `engine/source`，只讀，不建置、不執行。
- 全程靜態分析。沒有啟動遊戲、伺服器或 DB；沒有改產品程式；沒有任何 BIN、IDB、pak 進 git。

## 1. Q4-3 C129 `C_PLEDGE_WATCH` 的送出點

### 1.1 原版怎麼做（已驗證）

TW16 op 201 有三個送出函式，各是一個封包類別的 `+8` 方法（vtable 的 RTTI 名稱直接寫出用途）：

| 子碼 | 格式 | 送出函式 VA／FO | 類別（vtable VA／FO） | 意義 |
|---|---|---|---|---|
| 0 | `ccs`：201, 0, 血盟名（最多 16 字＋NUL，`0x11` byte 緩衝） | `sub_C15C10`／`0x815010` | `CS_PLEDGE_WATCH_ADD_REQ`（`0x11AD034`／`0xDABC34`） | 新增關注 |
| 1 | `ccs`：201, 1, 血盟名 | `sub_C15C40`／`0x815040` | `CS_PLEDGE_WATCH_DEL_REQ`（`0x11AD04C`／`0xDABC4C`） | 刪除關注 |
| 2 | `cc`：201, 2 | `sub_C15C70`／`0x815070` | `CS_PLEDGE_WATCH_GET_LIST_REQ`（`0x11AD064`／`0xDABC64`） | 取清單 |

格式字串在 `.rdata` `0x11ACEE0`／`0x11ACEE4`／`0x11ACEE8`（FO `0xDABAE0` 起）。三個類別的下一格就是對應的回應類別 `SC_PLEDGE_WATCH_*_ACK`，處理函式 `sub_C14680`／`sub_C146B0`／`sub_C146E0`，也就是 SS2§3 的 S200 type 0／1／2。

入口（`PledgeUI` 建構子 `sub_B2A0C0`／FO `0x7294C0` 用 `sub_B4FA80` 註冊 XML 事件）：

| 入口 | 事件／名稱 | 處理 | 送出 |
|---|---|---|---|
| 血盟窗「新增」鈕 | `AddFocusPledge` → `sub_B2B040`（FO `0x72A440`） | 放開左鍵（514）時開輸入框 `sub_AC98E0`，提示字串 **3361**「請輸入想要查看的血盟名。」；確定後 `sub_B2AF90`（FO `0x72A390`）：名稱非空才送子碼 0，之後 `sub_B30D20` 重畫血盟窗 | 子碼 0 |
| 血盟窗清單勾選 | `CheckFocusPledge` → `sub_B2B910`（FO `0x72AD10`） | 只切換該列的勾選狀態（`sub_B2B8D0`），不送包 | — |
| 血盟窗「刪除」鈕 | `DelFocusPledge` → `sub_B2C4D0`（FO `0x72B8D0`） | 開確認框，字串 **4118**（本機 `string-c.tbl` 只有 3484 筆，查不到文字）；確定後 `sub_B2C5A0` → `sub_B2C2F0`（FO `0x72B6F0`）：逐列檢查勾選（`sub_B2D270`），**每個勾選的血盟各送一包**子碼 1＋名稱，並從清單移除 | 子碼 1（可多包） |
| HTML 動作 `pledgefocusadd` | `HyperActPledgeFoucsAdd`（拼字照 RTTI；執行 `sub_583860`／FO `0x182C60`） | 與「新增」鈕相同：輸入框 3361 → `sub_B2AF90` | 子碼 0 |
| 聊天指令 `/focus`、`/注視`（字串 3322） | `Commandfocus`（執行 `sub_F54EC0`／FO `0xB542C0` → `sub_F46F70`／FO `0xB46370`） | 有參數：子碼 0＋參數；**沒有參數：子碼 2（取清單）** | 子碼 0／2 |

- 指令名稱的註冊在 `sub_F56320`（FO `0xB55720`）：每個指令依序是「建構子 → 英文名 → 字串表名」，例如 `focus` 與 `string[3322]`「注視」。
- 子碼 2 **只有** `/focus` 不帶參數會送；血盟窗本身打開時不送（`PledgeUI` 的事件裡沒有呼叫 `sub_C15C70`）。
- 本機 r29 `templates.json` 的血盟選項頁 `optionplp` 有 `pledgefocusadd`、`pledgefocus`、`mypledgefocus` 三個連結（`pledgefocus`／`mypledgefocus` 的執行函式本輪沒追）。

### 1.2 3.81 是否支援（已驗證）

`C_ClanAttention`（C129）：`readC(type)`。type 0 讀 `S` 血盟名並加入；type 1 讀 `S` 並刪除（另有「GS 標記＋列號」的變體，TW16 不會送）；type 2 回清單。三種成功時都回 `S_ClanAttention(clan, 2)`，也就是 S200 type 2 的整份清單。檢查：要有血盟而且是君主（`isClanPrince`），否則不處理或回訊息 1064。

欄位與 TW16 完全一致：`C(129) C(type) [S(name)]`。

### 1.3 對應要改的檔案

- `engine/source/Clan.cs`：`ClanProtocol` 加 `AttentionAdd(name)`＝`[129, 0, name…, 0]`、`AttentionRemove(name)`＝`[129, 1, name…, 0]`、`AttentionGetList()`＝`[129, 2]`；名稱截到 16 byte。送出走既有的 `sendMovementGate`。
- `engine/source/NativeUi/PledgePanel.cs`（`:198` 的註解處）：「新增」鈕開共用輸入框（字串 3361）→ 送子碼 0；清單列可勾選；「刪除」鈕確認後**每個勾選列各送一包**子碼 1。確認框文字 4118 本機沒有，先用空白或沿用「刪除」的通用字串並註明。伺服器回 S200 type 2 後重畫（現有的 `ScrollAttention` 路徑）。
- `engine/source/PlayerChat.cs`：`/focus`（與「/注視」）指令：有參數送子碼 0、沒參數送子碼 2。（T290 已由 PR #744 合併，`PlayerChat.cs` 可以直接改。）
- HTML 動作 `pledgefocusadd` 走 `NpcInteraction.cs` 的 HTML 動作分派（若有）；沒有就列為選配。

## 2. Q4-4 `byte_1542AA1`（職業碼）的來源

### 2.1 原版怎麼做（已驗證）

`byte_1542AA1` 是**自己的職業碼**，不是遊戲中封包寫的：

1. 選角清單 S_CHARACTER_INFO（op 31／204，`sub_933040`／FO `0x532440`）讀 `sscchhhcccccccccdc`（名稱、血盟、**職業**、性別、正義值、HP、MP、AC、等級、六項能力、存取等級、生日、檢查碼）。第 3 個欄位（職業）在 `0x93323F`／FO `0x53263F` 寫進角色紀錄的 `+0x78`。
2. 玩家在選角畫面按進入時（`sub_EBEA20`／FO `0xABDE20` 或 XML 選角窗 `sub_EED8A0`／FO `0xAECCA0`），`sub_EC9510`（FO `0xAC8910`）在 `0xEC9717`／FO `0xAC8B17` 執行 `byte_1542AA1 = 紀錄[+0x78]`；另一份 `sub_EED2B0`（FO `0xAEC6B0`）在 `0xEED4B0`／FO `0xAEC8B0` 做同樣的事。`gstores` 全程式只有這兩個寫入點。
3. 同一個函式也依職業切換 UI 資源指標（2、4 等有自己的表；1 與 5／6 有不同的 surf），證明這是職業碼。

職業碼的意義（依 3.81 的 `type`，以及本輪看到的用法）：0＝王族（`sub_F46290`、`sub_B34930`、HyperAct 盟戰都要求 `byte_1542AA1 == 0`，否則印 478「只有王子和公主才能宣戰」）、2／4＝妖精／黑暗妖精（AM§1(f) 的夜視加成與 AM§4 盲目的 6/31）、7＝戰士（UI 的 40% 血量警示）。

### 2.2 3.81 是否支援（已驗證）

3.81 `S_CharPacks`（S93）與新角色 `S_NewCharPacket`（S127）的第 3 個欄位就是 `type`（職業）。引擎已經在 `LoginProtocol.cs` 的 `LoginCharacter.Class` 讀到這個值。

### 2.3 對應要改的檔案

- 進入世界時（選角送 C_ENTER_WORLD 的地方，`CharacterManagement.cs`／`LoginConnection.cs`）把選中角色的 `LoginCharacter.Class` 交給 `WorldSession`。
- `engine/source/Afflictions.cs:13`：`BlindClassBonus` 改成「自己的職業碼是 2 或 4」，拿掉「封包沒查到」的註解，改寫「`byte_1542AA1`＝選角清單的職業，進入世界時鎖定（FU6§2）」。
- `engine/source/Ambience.cs` 的 `NightVision` 用同一個條件（AM§1(f)：職業 2 或 4）。
- `AfflictionFollowupTests.cs:55` 的「產品不設職業條件」要改成依職業。

## 3. Q4-5 盟戰訊息與宣戰入口

### 3.1 S84 War 的顯示（已驗證）

處理函式 `sub_94F190`（VA `0x94F190`／FO `0x54E590`）讀 `css`（格式字串 `.rdata` `0x11851FC`／FO `0xD83DFC`）：type、血盟 1、血盟 2。文字一律**印在聊天欄**：`sub_7DC850(文字, palette[24], palette[14], …)`，與 FU5§2 的 S250 是同一條路（淡黃）。**沒有視窗、沒有畫面中央的字。**

| type | 字串 | 文字（`%0`＝血盟 1、`%1`＝血盟 2） | 另外做的事 |
|---|---|---|---|
| 1 | 226 | 「%0 血盟向 %1血盟宣戰。」 | 若自己的血盟是其中一方：把畫面上對方血盟的物件設 `+197=2`、`+196=1`，自己血盟的設 `+197=1`（AN§3）；記下對手；`byte_153CE76=1`（戰爭中）；`sub_8F3B90(11, 0)` |
| 8 | 235 | 「\f1目前你的血盟與 %0 血盟交戰當中。」 | 同 1（不印 226） |
| 2 | 228 | 「%0 血盟向 %1 血盟投降了。」 | 自己的血盟是其中一方：移除對手（`sub_949050`）；戰爭清單空了才清 `byte_153CE76`，並 `sub_8F2FB0(13, -1)` |
| 3 | 227 | 「%0 血盟與 %1血盟之間的戰爭結束了。」 | 同上，改 `sub_8F1C30(-1)` |
| 4 | 231 | 「%0 血盟贏了對 %1 血盟的戰爭。」 | 同上，改 `sub_8F2FB0(12, -1)` |
| 6 | 224 | 「%0 血盟與 %1血盟成為同盟關係。」 | 只印字 |
| 7 | 225 | 「毀掉%0 血盟與 %1血盟之間的同盟。」 | 只印字 |

- type 1 與 8 的 `sub_7DC850` 第 4、5 個參數是 (7, 1)／(7, 235)，其他是 (−1, −1)；這兩個參數交給 `sub_7DCC50`，用途本輪沒追（推測：聊天分類或去重複）。
- type 1、8 另外呼叫 `sub_92B250(1)`（把血盟窗物件的 `+1201` 設 1，`PledgeUI` 建構子有初始化這一格，推測是「戰爭中」旗標）；2／3／4 設 0。最後若血盟窗開著（`sub_B2D340`）就重畫（`sub_B30D20`）。
- `sub_8F3B90`／`sub_8F2FB0`／`sub_8F1C30` 是背景音樂函式（看 `byte_13D3F88` 等音樂旗標，`sub_8F2FB0` 依 `dword_13D3F90[n]` 切曲）。所以宣戰時切到第 11 首、勝利 12、投降 13、結束回原曲（**推測**：11–13 對應 `music.def` 的哪一行本輪沒對）。

### 3.2 C_WAR 的原版入口（已驗證）

TW16 op 197（3.81 C227）的格式都是 `ccs`：197, 類型, 對手血盟名。

| 類型 | 入口 | 檢查與提示 | 送出函式 VA／FO |
|---|---|---|---|
| 0 宣戰 | 聊天指令 `/war`（英文名在 `0x11EB238`）或「/宣戰」（字串 380）＋血盟名；類別 `Commandwar`（`sub_F561E0`／FO `0xB555E0`） | `sub_F46290`（FO `0xB45690`）：沒有角色或職業碼 ≠ 0 → 478；自己的血盟階級（`+535`）不是 0、4、10 → 785「你不再是君主了」；戰爭中（`byte_153CE76`）時逐一比對戰爭清單，比對成立就印 234（比對函式 `sub_A0D4D0` 的語意沒追）；自己外型 gfx（`+10`）必須 ≤ 1（王子／公主本體，變身中不送也不提示）；等級 < 門檻 → 232；`sub_A41F80` 為假（推測：沒有血盟）→ 272；名稱非空才送 | `sub_F46290` |
| 0 宣戰 | 血盟窗 `ChangeWarState` 的值 1（`sub_B2B830` → `sub_B2B730` → `sub_B34930`／FO `0x733D30`）：先查 478／785，再開輸入框 **577**「請輸入要宣戰的對象血盟名稱。」→ `sub_F464E0` → `sub_F46290(名稱, 15)` | 同上（等級門檻 15） | 同上 |
| 0 宣戰 | HTML 動作 `declarewar`（`HyperActDeclareWar`，執行 `sub_582280`／FO `0x181680`）：取 HTML 參數 1（頁面上的城盟名），`sub_F46290(名稱, 25)` | **等級門檻 25**，不足時印 475「若要攻城，君主的等級得２５以上。」；r29 `templates.json` 只有城門守衛頁（`adguard6`、`ktguard9` 等 15 頁）有這個動作 | 同上 |
| 2 投降 | 聊天指令 `/surrender`、「/投降」（381）＋血盟名；`Commandsurrender`（`sub_F55E90`／FO `0xB55290` → `sub_F49970`／FO `0xB48D70`） | 只檢查職業碼 == 0（不是王族就不送，也不提示） | `sub_F49970` |
| 2 投降 | 血盟窗 `ChangeWarState` 的值 3：直接開輸入框 **578**「請輸入要投降的血盟名稱。」，**沒有本地檢查**；HTML 動作 `surrender`（`HyperActSurrender`，`sub_584E50`／FO `0x184250`）：職業碼 ≠ 0 時什麼都不做，階級不是 4／10 時印 785，否則開同一個輸入框 | 名稱非空才送 | `sub_B35350`（FO `0x734750`，血盟窗）、`sub_504030`（FO `0x103430`，HTML） |
| 3 停戰 | 聊天指令 `/cease`、「/停戰」（382）＋血盟名；`Commandcease`（`sub_F54830`／FO `0xB53C30` → `sub_F44FB0`／FO `0xB443B0`） | 只檢查職業碼 == 0（同上） | `sub_F44FB0` |
| 3 停戰 | 血盟窗 `ChangeWarState` 的值 2：直接開輸入框 **579**「請輸入要停戰的血盟名稱。」；HTML 動作 `cease`（`HyperActCease`，`sub_581D40`／FO `0x181140`）：檢查同投降的 HTML 動作 | 名稱非空才送 | `sub_B34490`（FO `0x733890`，血盟窗）、`sub_4FE550`（FO `0xFD950`，HTML） |

- 血盟窗的 `ChangeWarState` 是一個值 1／2／3 的控制項（`vtable +324` 取值）：**1＝宣戰、2＝停戰、3＝投降**（注意 2、3 與封包類型的 2、3 相反）。
- 投降與停戰的 HTML 動作在 r29 `templates.json` 的血盟說明頁（`optionplp` 一類）出現；說明文字裡也寫了 `/surrender [對方血盟名稱]`、`/cease [對方血盟名稱]`。同一頁有 `action="war"`，但 TW16 的 HyperAct 表（145 個名稱）**沒有 `war`**，點了不會有反應（推測：r29 範本來自別的版本）。
- 原版**沒有**引擎那種同時有三個鈕的獨立盟戰窗。

### 3.3 3.81 是否支援（已驗證）

- `S_War`：`C(type) S(clan1) S(clan2)`，和 TW16 讀法一致。
- `C_War`：`readC(type)`、`readS(對手)`；type 0 宣戰、2 投降、3 停戰。伺服器自己也查 478、272、234、232（等級 25 以下的君主對**非城盟**宣戰）、474 等。所以客戶端的檢查只是提早擋，結果以伺服器為準。對城盟宣戰時伺服器不看等級 15／25 的差別（客戶端 HyperAct 用 25）。

### 3.4 對應要改的檔案

- `engine/source/ClanWar.cs`：
  - S84 的 `Describe` 改成 226／235／228／227／231／224／225 的字串表文字，**印到聊天欄**（`PlayerChat` 的系統行，palette[24]），不要只寫進 WinForms 盟戰窗。type 1／8 更新戰爭中旗標與對手清單，2／3／4 在清單空了時清旗標（頭上盟徽已由 `ObjectRendering.cs:236` 處理）。
  - C227 的入口改成：聊天指令 `/war`（「/宣戰」）、`/surrender`（「/投降」）、`/cease`（「/停戰」）＋血盟名；血盟窗的三態控制項＋輸入框（577／578／579）。拿掉引擎自訂的三鈕 WinForms 窗（或只在原生 UI 關閉時保留為備援，並標「引擎自訂」）。
  - 本地檢查照 3.2（478、785、272、232、234）。
- `engine/source/PlayerChat.cs`：加三個指令。
- `engine/source/NativeUi/PledgePanel.cs`：`ChangeWarState` 控制項。
- 背景音樂 11／12／13：交給 `BackgroundMusic.cs`／`ProductSoundBackends.cs`（選配，先對 `music.def`）。

## 4. Q4-6 求婚入口

### 4.1 原版怎麼做（已驗證）

TW16 op 21（3.81 C50）只有兩個送出函式，格式都是 `cc`（**沒有目標欄位**）：

| 子碼 | 送出函式 VA／FO | 入口 |
|---|---|---|
| 0 求婚 | `sub_F48380`／`0xB47780`：`cc` 21, 0 | HTML 動作 **`propose`**（`HyperActPropose`，vtable `0x11A9884`／FO `0xDA8484`，執行 `sub_5838F0`／FO `0x182CF0`）；聊天指令 `/propose`、「/求婚」（字串 788）（`Commandpropose`，`sub_F55510`／FO `0xB54910`） |
| 1 離婚 | `sub_F46690`／`0xB45A90`：`cc` 21, 1 | HTML 動作 **`divorce`**（`HyperActDivorce`，vtable `0x11A9890`／FO `0xDA8490`，執行 `sub_582730`／FO `0x181B30`）；聊天指令 `/divorce`、「/離婚」（789）（`Commanddivorce`，`sub_F54C40`／FO `0xB54040`） |

- HyperAct 名稱在工廠 `sub_BB5180`（FO `0x7B4580`）註冊（`"propose"`、`"divorce"`；建構子 `sub_B72CD0`、`sub_B711F0`），執行函式只做送包，不開窗、不檢查狀態。
- 對象由伺服器決定（面前的角色），客戶端不送 id。
- 本機 r29 `templates.json` **沒有**任何頁面用 `propose` 或 `divorce`（搜尋 0 次）；所以 3.81 世代實際能用的入口是聊天指令。
- 對方的確認是 S_ASK（YES/NO）654，回覆走共用詢問槽（WIN T141，引擎已有）。

### 4.2 3.81 是否支援（已驗證）

`C_Propose`（C50）：`readC(c)`；c＝0 求婚（檢查幽靈、已婚 657／658、同性 661、在禮堂座標內，對方收到 `S_Message_YN(654)`），c＝1 離婚（未婚 662，否則回 `S_Message_YN(653)`）。欄位一致。

### 4.3 對應要改的檔案

- `engine/source/Marriage.cs`：`Propose()` 的 `[50, 0]` 已經正確；補 `Divorce()`＝`[50, 1]`（653 的確認走共用詢問槽）。`:122` 的註解改成「原版入口是 HTML `propose`／`divorce` 與 `/propose`、`/divorce`（「/求婚」「/離婚」）指令，3.81 的 HTML 範本沒有這兩個連結（FU6§4）」。
- `engine/source/PlayerChat.cs`：`/propose`、`/divorce`（與中文別名）指令。
- 引擎自訂的過渡頁入口（`Marriage.cs:138`）可以拿掉或標「引擎自訂」；不必等 HTML。

## 5. Q4-7 `AgitWindow` 的觸發

### 5.1 原版怎麼做（已驗證）

- `AgitWindow`（建構子 `sub_541B80`／FO `0x140F80`，vtable `0x1151734`／FO `0xD50334`，全域 `dword_13A96EC`）在 initGame（`sub_8C7C80`）的 `0x8C8C8E` 建立，**之後沒有任何程式把它打開**：
  - `dword_13A96EC` 的引用只有 7 個（IDA xref 與 `t716_callscan.py` 的立即值掃描一致）：initGame 建立、`sub_8C32B0` 拆除、`sub_8CB760` 換解析度時重排、`sub_542450` 讀取。沒有 `sub_A59AB0(dword_13A96EC)` 或 vtable 的顯示呼叫。
  - `sub_542450`（FO `0x141850`）是 `AgitListBox`（vtable `0x11516C4`／FO `0xD502C4`）的 `+100` 方法：對 `AgitWindow` 呼叫 `sub_5430D0`（FO `0x1424D0`）＝把目前選取列的編號送 TW16 op 227 `cdss`（物件 id, `"select"`, 編號），也就是 3.81 C125 NPC 動作，和 `AgitMapWindow` 的 674 鈕一樣。
- 版面（已驗證，但用不到）：底圖 **surf 144**（`sub_5428A0`），點擊區 238×350；鈕 658（關閉，(209, 305)）、660（送出 `select`，(34, 332)）、666／668（上下捲動，(64, 311)／(99, 311)）；清單列（`AgitItem`，`sub_542910`）畫名稱、「%s %d/%d」、價格（千分位）與另一個數字，用的字串表不是 `string-c.tbl`（id 1381／1389／1390 在本機表是無關訊息，待驗證）。
- 推測：這是舊版的盟屋拍賣清單。TW16 的 S156 HouseList 改由 XML 版 `CAgitAuctionUI` 顯示（WIN2§5.2），舊窗只剩建立與拆除。

**與 `AgitMapWindow` 的關係**：兩個不同的類別（`AgitMapWindow` 建構子 `sub_5419E0`／FO `0x140DE0`，全域 `dword_13A96F0`），彼此不互相開啟。`AgitMapWindow` 只由 S187 HouseMap（`sub_92E710`／FO `0x52DB10`，區域 < 9 時）打開；兩者共用的只有 `select` 送包格式。

### 5.2 3.81 是否支援

3.81 的盟屋清單是 S156（`S_AuctionBoard`），TW16 交給 `CAgitAuctionUI`（本機缺 XML，WIN2§8）。`AgitWindow` 在 TW16 本身就不會出現，**不做**。

### 5.3 對應要改的檔案

- 不新增 `AgitWindow` 面板。gap-matrix 第 4 節 `AgitWindow` 列改成「原版不顯示（死窗）」。
- `NativeUi/AgitMapPanel.cs`（PR #700）維持只接 S187。

## 6. Q4-8 S161 代碼聊天的 `$數字` 名稱

### 6.1 原版怎麼做（已驗證）

`sub_94AF00`（VA `0x94AF00`／FO `0x54A300`）：

1. 目前地圖 509 時整包丟掉。讀 `cdshh`：type、物件 id、文字（後兩個 `h` 3.81 不送，讀到補零）。
2. 語系旗標 `byte_13CE3FE == 1` 或選項 `byte_135C1E5` 時先跑髒話過濾（`*` 代換）。
3. 若說話者是種類 5 的物件、`+196 == 0`、`+219 == 1`，而且文字含字串 786（本機表是「伊娃之祝福」）：整段不印（推測：特定 NPC 的祝福廣播另外處理）。
4. **`$` 展開**：用 `dword_153C9C4` 的表（`0x94B178`／FO `0x54A578` 與 `0x94B23F`／FO `0x54A63F` 都是 `mov ecx, offset dword_153C9C4`），也就是 **`desc-c.tbl`**（`sub_8CA220` 以 `"desc"` 載入，見 [`sprites-and-strings.md`](sprites-and-strings.md) §4）。
   - 說話者是種類 5 而且外型屬於職業外型（`sub_8CA110`／`sub_501280`）：只有文字**開頭**是 `$` 才展開。
   - 其他（一般 NPC）：一律展開。
   - 展開規則（`sub_A11520`／FO `0x610920`）：`$` 後面的十進位數字＝表的行號；超出範圍換成 `???`；數字後面若緊接 `;` 一併吃掉；表的內容含 `%i` 時代入額外參數；結果開頭是 `+0` 時去掉前三個字元。
5. 展開後若含 `^` 或 `!` 另外處理（`sub_941E40`、`sub_941DE0(33)`，推測：特效／音效代碼，本輪沒追）；type 20 而且文字含 `#` 時改走 `sub_A06FD0` 的特殊指令，不印字。
6. 顯示：type ≠ 21 時印到聊天欄（type 2 用 palette[33]，其他 palette[14]）；**一律**在說話者頭上畫泡泡（`sub_7CE710`，type 21 只有泡泡）。

### 6.2 3.81 是否支援（已驗證）

3.81 `S_NpcChat`／`S_NpcChatShouting`／`S_NpcChatGlobal` 寫 `C(type 0／2／3) D(id) S(...)`，文字是 `npc.getNameId() + ": " + chat`、`"<" + nameId + "> " + chat`、`"[" + nameId + "] " + chat`。`getNameId()` 是 `$1234` 這種 `desc-c.tbl` 行號。所以 TW16 會把 `$1234: 你好` 展開成「NPC 名稱: 你好」。

### 6.3 對應要改的檔案

- `engine/source/PlayerChat.cs:91–98`（S161 解碼）：文字依 6.1 的規則展開 `$n`。查表用 T290 新增的 `TextArchive.ReadDesc()`（`TextArchive.cs:94`，讀 `desc-c.tbl` 並核 pin），或 `assets/objects/names.json`（r29 版本有 16945 筆，第 4／431 筆與 `desc-c.tbl` 相同，是同一張表）；超出範圍顯示 `???`、吃掉數字後的 `;`。可以重用 `ObjectAssets.Name`／`ItemText.Resolve` 的規則，但要補 `???` 與 `;`。
- 頭上泡泡（type 0／2／3 都畫）若引擎還沒有，列為選配。
- （T290 已由 PR #744 合併，`PlayerChat.cs` 可以直接改。）

## 7. Q4-9 世界地圖與村莊地圖

### 7.1 原版怎麼做（已驗證）

- 類別都在：`WorldMapWindow`（建構子 `sub_A64460`／FO `0x663860`，vtable `0x1190E0C`／FO `0xD8FA0C`）、`VillageMapWindow`（建構子 `sub_A63A30`／FO `0x662E30`，vtable `0x1190E5C`／FO `0xD8FA5C`），共同基底 `sub_A622F0`（`ImageBasedMapSystemWindow` 系）。版面資料來自 `MapSystem` XML 解析 `sub_A730A0`（FO `0x6724A0`）讀到的 `World_Map`（字串 `0x1190628`／FO `0xD8F228`）、`Village_Map`（`0x1190640`／FO `0xD8F240`）元素，存在 `dword_153F45C` 的版面資料（`WorldMapWindow` 的框取 `*(dword_153F45C+164)`，鈕取該紀錄的 `+24`、`+28`、`+36`，以及 `*(dword_153F45C+184)`）。UI§5 已查的版面：位置 (45, 30)、透明度 10、框 **2828**、550×316。
- 打開與關閉都經 `sub_A77D80(視窗)`（FO `0x677180`）：已顯示就呼叫 vtable `+36` 關閉，否則 `+32` 打開。`WorldMapWindow` 的 `+32`（`sub_A66B70`／FO `0x665F70`）設目前地圖窗、`sub_815CA0(4)`、`dword_153F2E0 = 0`；`VillageMapWindow` 的 `+32`（`sub_A66AA0`／FO `0x665EA0`）用 `sub_815CA0(5)`、`dword_153F2E0 = 3`。
- 呼叫端：野外地圖窗的連結清單（`sub_A683A0`／FO `0x6677A0`、`sub_A684E0`／FO `0x6678E0`）依連結的地圖編號 < 100 開世界地圖、≥ 100 開村莊地圖；WM_CHAR 處理 `sub_8C14F0` 在 `0x8C16C1`／FO `0x4C0AC1` 只做「已開著就關掉」。
- **但這兩個視窗從來沒有被建立**：
  - initGame（`sub_8C7C80`）只建 `dword_153F2D0`＝`SmallBGMapWindow`（`sub_A63210`，`0x8C7FAA`）與 `dword_153F2D4`＝`FieldMapWindow`（`sub_A61DE0`，`0x8C7FFF`）。
  - 世界／村莊的全域 `dword_153F2D8`／`dword_153F2DC` 唯一的寫入是拆除函式 `sub_8C32B0` 寫 0（`0x8C3F35`、`0x8C3F82`）。
  - 兩個建構子在 IDA 沒有呼叫者；`t716_callscan.py` 線性掃 `.text` 也找不到 `call` 或立即值引用；兩個 vtable 也只有各自的建構子與解構子引用。
  - 所以 `sub_A77D80` 收到 0 直接返回；熱鍵那段永遠不成立；連結清單的路徑會對 0 指標呼叫 vtable `+64`（推測：實際上點不到，或點了會當掉；沒有實機確認）。

### 7.2 3.81 是否支援

與伺服器無關（純客戶端視窗）。TW16 本身不顯示世界／村莊地圖，玩家能看到的只有 `Field_Map`／`Dungern_Map`（UI§5）。

### 7.3 對應要改的檔案

- **不做**世界／村莊地圖窗。`NativeUi/FieldMapPanel.cs` 的連結清單若有「跳到世界／村莊地圖」，改成不動作（不要照原版去碰空指標）。
- gap-matrix 第 4.2 節 `MapSystemWindow`、MapUI 的「世界／村莊」部分改為「原版不建立（死窗）」；R5-H 的依賴「世界／村莊地圖等 Q4-9」解除。

## 8. 重跑

腳本在 `rea-scripts/t716/`：

| 腳本 | 用途 |
|---|---|
| `queries.txt` | q1–q10 查詢清單（用 `rea-scripts/t690/ida_t690.py` 執行） |
| `t716_callscan.py` | 給 VA，線性掃 TW16 `.text` 的 `E8 rel32` 呼叫與 32 位元立即值（用 `rea-scripts/nat.py`） |

其他：送出點用 `rea-scripts/wo_sendsites.py 201 197 21 33`；HyperAct 名稱用 `rea-scripts/t268/t268_hyper.py`（輸入 `sub_BB5180` 的反編譯）；字串用 `rea-scripts/t680/t680_strings.py`。執行方式同 FU5§6：把 `ida_t690.py`、`run_ida.ps1` 放進 `scratch/rea/scripts/`，IDB 副本放 `scratch/rea/ida/tw16.idb`，先跑 `subst` 確認代號沒有被占用，再執行 `run_ida.ps1 -Script ida_t690.py -List qN.txt -Out oN -Drive W:`。

## 9. 未驗證與限制

- 全程靜態分析，沒有實機驗證。3.81 `LiTo.bin` 有殼，行為以 TW16 為準（推測同代）。
- Q4-3：刪除確認字串 4118 本機表沒有；`pledgefocus`／`mypledgefocus` 的執行函式沒追。
- Q4-5：S84 的背景音樂 11／12／13 對應哪一首、`sub_7DC850` 第 4／5 參數的用途沒追；r29 範本的 `action="war"` 在 TW16 沒有對應動作，是推測。
- Q4-7：「沒有打開 `AgitWindow` 的程式」是依全部引用判斷，不排除經由視窗管理器的通用迭代間接顯示（`sub_8CB760` 只重排位置，看起來不會顯示）。清單列的字串表沒對。
- Q4-8：`^`／`!` 的處理、字串 786 那個分支的用途沒追。
- Q4-9：連結清單路徑是否真的點得到（會不會當掉）沒有實機確認。
- 本輪沒有改產品程式；第 1.3、2.3、3.4、4.3、5.3、6.3、7.3 節是給 code 任務的建議。
