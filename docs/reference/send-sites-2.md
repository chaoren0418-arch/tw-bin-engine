# 送出點與欄位 第 2 輪（gap-matrix Q-3）

查證任務：T268（#629）。起點：[`gap-matrix.md`](gap-matrix.md) 第 11.6 節 Q-3。影響 R2-F、R2-H、R2-K（另有 R2-L 一項）。
簡稱：本文 **SS2**。引用寫法：`SS2§n`。

## 0. 方法與來源

- **BIN**：未加殼的 `TW1608262200.bin`，SHA-256 `e12949cf…41a2`（`nat.py` 開檔時檢查）。
- **IDB**：P01 `ida/03-ascii-isolated/TW1608262200.idb` 複製到 `scratch/rea/ida/tw16.idb`，複製前後 SHA-256 都是 `3a0bf206…e82f`。
  - 用 IDA 7.7 唯讀批次查詢 14 輪，subst 用 `W:`。
  - 執行前用 `subst` 確認 `W:` 沒有被占用（`T:` 被 task-627 占用，沒有碰）。每輪結束後都解除 `W:`。
- **送出點**：照 T182 的作法，用 `wo_sendsites.py` 線性掃描 `sub_9FBC00`（VA `0x9FBC00`／FO `0x5FB000`，送包函式：格式字串、opcode、欄位）的呼叫點。
- **HyperAct 名稱**：從工廠 `sub_BB5180`（FO `0x7B4580`）的反編譯，抽出「建構子 → 註冊名稱」對照，共 145 個（`t268/t268_hyper.py`）。再用 vtable 的 RTTI 名稱反查。
- **資料**：
  - 字串表 `string-c.tbl` 用 `t225_strings.py` 在記憶體中讀取（`decoded_sha256` `4ebbc1a7…43e9`，3484 行）。
  - XML 用 `ui_xml_decrypt.py` 解到 `scratch/rea/uixml*/`。
  - **XML、pak 都沒有進 git。**
- **3.81 伺服器**：`D:\lineage開發\1-伺服器\src`，唯讀。用到的檔案與 SHA-256 前 16 碼：

  | 檔案 | SHA-256 前 16 碼 |
  |---|---|
  | `S_Disconnect.java` | `752eb3a4ee130495` |
  | `S_Ability.java` | `1d7aeeb271e5f288` |
  | `S_TaxRate.java` | `2973e2e0b4c8914b` |
  | `S_ClanAttention.java` | `e8322f46f4894359` |
  | `S_SkillBuyItem.java` | `2652dfc43a61b96d` |
  | `S_SkillBuy.java` | `09628d128beb0187` |
  | `C_PledgeRecommendation.java` | `3b70b9fa2b3dba3d` |
  | `S_PacketBoxPledgeRecommend.java` | `6492db82484df8e7` |
  | `C_SkillBuyItem.java` | `d54fa4c4cb1dcd62` |
  | `C_CheckPK.java` | `11ffa8ad46337a48` |

- **地址換算**：程式碼 FO＝VA − `0x400C00`；`.rdata` FO＝VA − `0x401400`；`.data` FO＝VA − `0x401800`。這三個差值都和既有文件的對照一致（例如 `0x93F200`／`0x53E600`、`0x1183F78`／`0xD82B78`、`0x137B3A8`／`0xF79BA8`）。bss 沒有 FO。
- **範圍**：全程靜態分析。沒有建置、沒有執行，也沒有啟動遊戲、伺服器或 DB。沒有改產品程式。

## 1. 以物品學魔法（C_EXCHANGEABLE_SPELL 125）、op 175 與 op 44 學魔法窗

### 1.1 送出點（已驗證）

`C_EXCHANGEABLE_SPELL`(125) 只有一個送出點：HyperAct **`xchgspell`**。

- 類別：`HyperActXchgSpell`。
  - vtable `0x11A9428`／FO `0xDA8028`。
  - 建構子 `sub_B74AD0`，在 `sub_BB5180` 的 `0xBB590F` 註冊。
- 執行函式：`sub_585520`（FO `0x184920`）。
  - 取目前對話 NPC（`dword_13A9340`），查物件 `+10`（外型 gfx）。
  - **只在 gfx＝834 時送** `sub_9FBC00("cd", 125, npcObjId)`。格式字串在 VA `0x115645C`／FO `0xD5505C`。
- 對照的 HyperAct **`buyspell`**：
  - 類別 `sub_B70390`；執行函式 `sub_581C10`（FO `0x181010`）。
  - **gfx ≠ 834 時**送 `C_BUYABLE_SPELL`(24) `"cd"`＋npcObjId（格式字串 VA `0x1156458`）。
- 兩個動作用 NPC 外型 834 分流：
  - 834 的 NPC 只能用物品學。
  - 其他 NPC 只能用金幣學。
- **3.81 對照：**
  - TW16 125 對應 3.81 **C245 `C_SkillBuyItem`**：`readD(objid)`，5 格內，依 npcId 回 `S_SkillBuyItem`。欄位一致。
  - 伺服器**不處理** `"xchgspell"`、`"buyspell"` 這兩個字串（全原始碼搜不到）。所以兩個動作都必須由客戶端自己轉成封包。
  - r29 `templates.json`：
    - `xchgspell` 只在 `horunev1` 頁出現。
    - `buyspell` 出現在 `gerengev1`、`gerengT1`、`sedia`、`siriss`、`y_skill_01`。

### 1.2 收包與視窗（已驗證）

- **op 175 `sub_93F200`（FO `0x53E600`）**
  1. 清空 `dword_153CE10` 的清單，`+153`＝0（標記「用物品學」）。
  2. 讀 `h` 筆數，每筆 `d` 技能索引；本機已學過的跳過（`sub_475C90`）。
  3. 每筆用 `sub_A27B70(索引, word_136D548[索引], 價格 0, 名稱)` 加入清單。
  4. 最後 **`sub_A59780(7)`**：以模式 7 開 `TRADE_WINDOW`（`dword_153CE14`）。
  - 筆數為 0 時：改開 NPC 對話窗的 HTML 頁 **`HorunEv3`**，然後 `sub_8763E0` 收起舊窗。
- **op 44 `sub_931FF0`（FO `0x5313F0`）**
  - 同一個清單與視窗，但 `+153`＝1（標記「用金幣學」）。
  - 格式是 `"dh"`＋逐筆 `d`。價格＝`D × ((索引>>3)+1)²`，也就是基礎價乘上「魔法等級²」。
  - 同樣以 `sub_A59780(7)` 開窗。
  - 筆數為 0 時，再讀一個 `d`（NPC 物件 id），依 gfx 開頁：237→`GerengEv3`；2820→`Sedia1`；其他→`Siriss1`。
- **`sub_A59780` 模式 7**（FO `0x658B80`）：
  - 呼叫 `sub_7D7650(5)`，再把 `sub_7D7630(9)` 的值寫進子控制項 `+36`（用途沒追）。
  - `dword_153CE14+8` 的 `+148`＝1。
  - 模式寫進 `dword_153CE10+116`。
  - 版面（列、按鈕）屬於 Q-1（`TRADE_WINDOW`），本輪沒有查。
- **3.81 對照：**
  - `S_SkillBuyItem`（S13）＝`H(n)`＋`n×D`，和 op 175 一致。空清單時只送 `H(0)`。
  - `S_SkillBuy`（S41）＝`D(6000) H(n) n×D`，和 op 44 一致。空清單時只送 `H(0)`。
    - TW16 會先讀 `d`，再讀 `h`，然後讀 NPC id，全部越過封包尾端。所以 3.81 的空清單在 TW16 會落到預設頁 `Siriss1`（**推測**；值取決於封包補齊的位元組）。
  - 四個 HTML 頁在 r29 `templates.json` 都有。
- **確定鈕送出**（`C_BUY_SPELL` 123／`C_EXCHANGE_SPELL` 210）：用立即值 opcode 掃描找不到送出點。T266（#627）已查到：兩者都由 `TRADE_WINDOW` 模式 7 的 `sub_A2BD10` 組包（見 [`trade-window-and-projectiles.md`](trade-window-and-projectiles.md) §1.6），本輪未重新核對。

## 2. 血盟推薦：S_MATCH_MAKING（op 133，`sub_B243A0`）與 C_MATCH_MAKING（221）

### 2.1 TW16 實際上不處理 op 133（已驗證，有一點保留）

dispatch `0x947D67`：

```
case 133: if (dword_153FCC4) sub_B243A0(dword_153FCC4, pkt)
```

- IDA 列出的 `dword_153FCC4` 引用全部是**讀取**（`cmp`／`mov ecx`），包括 `0x8C81D3`、`0x94683B`、`0x947D67` 和 `0xB23xxx`–`0xB28xxx` 的按鈕處理。**沒有任何寫入。**
- gap-matrix 第 4.1 節已驗證：`sub_B56A40` 的 `MatchMakingUI` 分支不建立物件。
- 兩者相符，所以這個全域一直是 0。**TW16 收到 S0 會直接丟掉**，推薦窗的按鈕也是死碼。
- 保留：用指標間接寫入的可能性沒有排除。
  - **T275（FU3§7）已補查**：全程式運算元與資料指標掃描，沒有任何寫入點碰得到 `0x153FCC4`（附近帶索引的陣列最遠只到 `0x153F574`）。TW16 確定丟棄 op 133。

### 2.2 欄位（已驗證；死碼，但格式可以當參考）

| type | TW16 讀法 | 3.81 `S_PacketBoxPledgeRecommend` | 一致？ |
|---|---|---|---|
| 0／1 | `C(結果)`；0 時 `+356`＝1／0，訊息 3261「成功登錄」／3262「登錄取消」 | `C(0/1) C(0)` | ✅ |
| 2 | `C(結果)`；0 時讀 `C(n)`＋n×`dssdcccssd` → `MatchPledgeInfo`（`sub_B22ED0`）；141 時用訊息 1925 | `C(0) C(n)`＋n×`D S S D C C C C(0) S D` | ✅（3.81 的 `C(0)` 被 TW16 當成空字串讀，位元組相同） |
| 3 | 同上，每筆多 `d c` 前綴（`dcdssdcccssd`） | `D C(0) D S S D C C C C(0) S D` | ✅ |
| 4 | `C(結果)`：0 → `cs`（類型、說明）＋`C(n)`＋n×`dccschcc` → `MatchUserInfo`（`sub_B22F70`）；130／138 清空；140 訊息 3305 | `C(0x82)` 或 `C(0) C(type) S(msg) C(n)…` | ✅（逐筆欄位只核對了數量） |
| 5 | `C(動作) D(id)`；0 → 3257「提出加入申請了」；138 → 3280；141 → 1925；其他 → 3258 | `C(5) C(動作) D(id) C(accept)` | ✅（TW16 不讀最後的 C） |
| 6 | `C(動作) D(id) C(accept)`；依動作與 accept 顯示 3259／3260／3266–3269／3275／3286；動作 1 或 0x87 且 accept＝1 時自動再送 `cc 221,4` | 同 5 | ✅ |

### 2.3 C_MATCH_MAKING（TW16 221 → 3.81 C76 `C_PledgeRecommendation`）送出點（已驗證，全在死窗內）

| 送出點 VA／FO | 格式與內容 | 3.81 `data` | 觸發 |
|---|---|---|---|
| `0xB253C3`／`0x7247C3`（`0xB25190`） | `cccs` 221, 0, 類型, 說明 | 0 登錄 | 登錄鈕（說明空白時用 `...`；說明檢查不過時顯示訊息 3309） |
| `0xB2342F`／`0x72282F`（`0xB233C0`） | `cc` 221, 1 | 1 取消登錄 | 自己 `+0x217` 是 3／6／9 時改顯示訊息 3308「血盟王族才能進行登錄和修正」 |
| `0xB25138`／`0xB2515C`／`0xB25172`（`0xB250F0`） | `cc` 221, 2／3／4（依目前頁籤 `+0x154`＝0／1／2） | 2／3／4 | 重新整理 |
| `0xB26834`（`0xB267F0`） | `cc` 221, 3 | 3 | 切到頁籤 1 |
| `0xB2582E`（`0xB257D0`） | `ccd` 221, 5, 血盟 id | 5 申請 | 申請鈕 |
| `0xB23340`（`0xB232D0`）、`0xB25490`（`0xB25420`）、`0xB234B0`（`0xB23450`） | `ccdc` 221, 6, 物件 id, 1／2／3 | 6，accept 1／2／3 | 接受／拒絕／取消 |
| `0xB24F12`、`0xB24F6E`（`sub_B243A0` 內） | `cc` 221, 4 | 4 | 6 號結果之後自動重整 |

- 每個按鈕處理都檢查 `0x202`（WM_LBUTTONUP）和忙碌旗標 `+0x39D`，送出後把旗標設為 1。
- 3.81 伺服器的 `data` 0–6 分支和上表一一對應（`C_PledgeRecommendation.java`）。

### 2.4 結論

- **原版**：TW16 有完整協定與按鈕，但窗不建立、收包被丟掉，玩家看不到血盟推薦。
- **3.81**：伺服器完整支援 C76／S0。
- **對 R2-K**：
  - 原版沒有畫面可以照抄，**不建議做推薦 UI**。
  - `ClanMatching.cs` 的資料層保留。
  - 是否另外做自訂畫面，屬於使用者決策。
  - gap-matrix 第 1 節 op 133、第 3 節 C_MATCH_MAKING 建議改列「不建議」。

## 3. S_PLEDGE_WATCH（op 78，`sub_B2E1D0`）的 UI（已驗證）

- **收包對象**：dispatch `0x947D88`，`this`＝`dword_153FCC8`。T226 已驗證這是 `PledgeUI`，所以這是**血盟窗內的「關注血盟」清單**，不是獨立視窗。
- **格式**：`C(type)`，之後用 `sub_C13B20`（讀 1 byte）、`sub_C13B90`（讀 4 byte）、`sub_C13BF0`（讀以 NUL 結尾的字串）。
  - **type 0**（`sub_C14680`）：`C(結果)`。131 或 129 → 聊天訊息 3349「對象血盟的王族為離線狀態」；138 → 3324「對象血盟於戰爭狀態」。
  - **type 1**（`sub_C146B0`）：`C(結果)` → `sub_B30040(0)` 重畫清單。
  - **type 2**（`sub_C146E0`）：`C(結果) D(n)`＋n×`S`（每個名稱最多 16 字元）。
    - 結果為 0 時：清空清單（`sub_B2BB40`），逐筆加入（`sub_B2AEC0`），再 `sub_B30040(0)`。
    - `sub_B30040` 把清單從第 `a2` 筆開始畫進 `PledgeUI` 子控制項（3, 7）的列，每列一個圖示（`sub_B2D270`）＋名稱（`sub_B2CCF0`）；捲動上限＝(筆數 − 7) / 2。
- **3.81 對照**：
  - `S_ClanAttention` 送 `H(type)`，也就是 `C(type) C(0)`，正好對上 TW16 的 type＋結果。之後的 `D(n) S…` 一致，最後的 `H(0)` 會被忽略。
  - 伺服器只送 type 2。
- **對 R2-K**：
  - `Clan.cs` 已存清單。
  - 要補的是血盟窗內的 7 列關注清單（圖示＋名稱，可捲動）。
  - 訊息 3349／3324 只在 type 0 出現，3.81 不送，可以不做。

## 4. `PledgeNoticeUI` 的開窗（已驗證）

- **建立**：`sub_B56A40` 在 `0xB586D3`／FO `0x757AD3` 以 `"PledgeNoticeUI"` 呼叫建構子 `sub_95D8F0`（vtable `off_118680C`），存進 `dword_13DC544`，再交給視窗管理（`sub_B54C40(…, 4)`）。
- **開窗**：唯一的使用點是 **op 153 S_MESSAGE `sub_944F50`（FO `0x544350`）的 type 17**（`0x9453A0`／FO `0x5447A0`）：
  1. 有視窗時，用它的標籤顏色（`sub_95DAC0`）；沒有視窗時用預設色 `0x3E0`。
  2. 把文字印到聊天區。
  3. 呼叫視窗 vtable `+0x44(1)` 顯示。
  4. `sub_95DB20` 把文字寫進 `XmlLabel`。
- **3.81 對照**：
  - 伺服器的 `S_OPCODE_GLOBALCHAT` 只送 type 3、4、9、11、12（`S_ChatGlobal`、`S_ChatClan`、`S_ChatClanUnion`、`S_SystemMessage`、`S_ChatWhisperTo`、`S_ChatParty`、`S_ChatTransaction`）。**沒有 type 17**，所以 3.81 不會開這個窗。
  - `PledgeNoticeUI.xml` 本機與 kor25／ly／t89 都沒有。
- **對 R2-K**：**不做**。gap-matrix 第 4.1 節 `PledgeNoticeUI` 建議改列「伺服器不支援」。血盟公告維持現有的 S167 資料。

## 5. `checkpk` → C51（已驗證）

- **HyperAct `checkpk`**：
  - 類別 `HyperActCheckPK`：vtable `0x11A9860`／FO `0xDA8460`，建構子 `sub_B709D0`。
  - 在 `sub_BB5180` 的 `0xBBA884` 註冊。
  - 執行函式 `sub_582080`（FO `0x181480`）：`sub_9FBC00("c", 9)`，**只有 opcode、沒有欄位**。
- **其他兩個送出點**（同樣是 `"c", 9`）：
  - 聊天指令類別 `Lineage::Commandcheckpk`（vtable `0x11EB784`／FO `0xDEA384`）的 `sub_F54A30`。
  - `sub_4FAFA0`（FO `0xFA3A0`）。它被 S250 dispatch（`sub_9357E0`）與 `sub_942B40` 呼叫，參數為 0 時先把計數 `dword_13A9374` 加 1。
- **3.81 對照**：
  - TW16 C9 對應 3.81 **C51 `C_CheckPK`**：不讀欄位，回 `S_ServerMessage(562, 次數)`「你的PK次數為%0次。」。
  - 引擎已經照做：`WeaponRepair.cs:10` 的 `CheckPk()` 送 `[51]`；`NpcInteraction.cs:87–96` 攔截 `checkpk`。
- **差異**：
  - 聊天指令入口沒做。
  - 原版的 HyperAct 不檢查死亡等狀態，引擎有加送出閘門（無害）。

## 6. 技能資料 `+56`／`+60` 的來源（已驗證）

- **資料表**：`dword_153F59C`。
  - 由 `sub_AA0610` 在 `0xAA0E5E`／FO `0x6A025E` 建立：`new(0xB4)` → `sub_E14160(off_137B3A8[0x2B])`。
  - 第 0x2B 筆檔名是 **`SpellList.xml`**（指標在 VA `0x137B454`／FO `0xF79C54`，字串在 VA `0x119CB64`／FO `0xD9B764`）。
- **解析**：`sub_E177C0`（FO `0xA16BC0`，vtable `0x11DF004` 第 1 格）。每個 `<spell>`／`<pspell>` 的屬性對應：

  | 屬性 | 寫入 | 偏移 |
  |---|---|---|
  | `delay`（毫秒，`atoi`） | `sub_E18980` | **`+56`** |
  | `dir`／`direction`（`true`→1，`false`→0） | `sub_E189C0` | **`+60`** |
  | `duration` | — | `+16` |
  | `icon` | — | `+48` |
  | `type`（`attack`） | — | `+24` |
  | `savelastspell` | — | `+62` |
  | `id` | — | `+44` |

- **使用**：`sub_E246E0`（FO `0xA23AE0`）。
  - `+60` 為真 → 動作 18（有方向施法），否則動作 19。
  - 冷卻＝`sub_A3BC30(動作)`＋`+56`（`sub_E16D10`）。
- **資料內容**：
  - 本機 `D:\lineage開發\0-客戶端` 的 `Tile.pak` **沒有** `SpellList.xml`。
  - 其他客戶端有：
    - kor25：`SpellList.xml`，186 筆。
    - ly：`SpellList.xml`／`-h`／`-c`，181 筆。
  - kor25 的 `delay`：10 有 45 筆、500 有 21 筆、100／1000 各 12 筆、3000 有 11 筆…；沒寫的 39 筆。
  - `direction="true"` 有 66 筆；沒寫的 120 筆。
  - 未寫屬性時的預設值（建構子初值）**待驗證**，推測是 0，也就是 delay 0、動作 19。
    - **T275（FU3§1）已查**：建構子 `sub_E14030` 的初值是 **delay 10 ms**、direction 0（動作 19）、savelastspell 1、align 1；不是 0。
- **3.81 是否支援**：
  - 3.81 世代（`LiTo.bin`＋本機資源）沒有這個檔，冷卻與 18／19 是否同樣來自客戶端資料，**待驗證**（`LiTo.bin` 有殼）。
  - 伺服器的動作毫秒表仍是判定加速的依據。
- **對 R2-H**：
  - 可以在執行期從 `SpellList.xml` 讀 `delay`／`direction`（找不到檔案時退回現在的 `ReuseMs`／`Action`），**不得把檔案複製進 git**。
  - 引擎 `MagicSpells` 的 `Action` 18／19 和 `ReuseMs` 可以和 kor25 的值逐筆比對。用哪個客戶端的檔當基準，需要決定（建議 kor25，因為它是 3.81 懶人包）。
  - XML 的 `id` 從 0 起算；與 3.81 技能編號的換算（推測是 +1）要先核對。
    - **T275（FU3§1）已查**：查表鍵就是 XML `id`（`pos` 只是顯示位置），等於 TW16 技能索引；**3.81 技能編號＝`id`＋1**（核對 20 筆）。

## 7. S36（op 122 S_CHANGE_ABILITY）各 type（已驗證）

- **`sub_932300`（FO `0x531700`）**：`"cc"`＝`type`、`值`。
  - `type < 6` 時，`sub_9E3260(&dword_14EBFA0[3*type], 值)`：存進 6 格「加密整數」（12 byte，XOR `0xC0017921` 混淆）。
  - 讀取一律用 `sub_500EA0(1, 3)`，意思是「值在 1–3 之間」。
  - 登出或重置時，`sub_8C32B0` 把 6 格清 0。
- **讀取點**（`calls 500EA0`）：

  | type | 讀取者 | 作用 | 3.81 會送？ |
  |---|---|---|---|
  | 1 | `sub_5041B0`、`sub_E08A20`（FO `0xA07E20`）、`sub_E2B6C0`（FO `0xA2AAC0`） | 施放傳送類魔法時，若成立（或王族 Lv≥50 且 `byte_13A931C`）且不在限制地圖，開 `dword_1540828` 的目的地選擇窗（vtable `+68`），不直接送 C_USE_SPELL | ✅ 傳送控制戒指 20288 |
  | 2 | （沒有讀取者） | — | ✅ 變形控制戒指 20281，客戶端不用 |
  | 3 | `sub_F03F20`、`sub_F05E50`、`sub_F07F00` | 某種顯示判定（含 `$431`），細節沒追 | ❌ 伺服器不送 |
  | 5 | `sub_DF8A50`（FO `0x9F7E50`）、`sub_E030B0` | 施放技能索引 50（召喚術）時，若成立就開 **`CSummonUI`**（`sub_503FF0`，見 WIN）、不送封包；否則送 `cccd` 216 加上依等級算的 7–13 | ✅ 召喚控制戒指 20284 |
  | 0、4 | 沒有讀取者 | — | ❌ |

- **3.81**：`S_Ability(type, equipped)`＝`C(type) C(1/0)`，只送 1、2、5。
- **對 R2-L／R2-H**：
  - S36 不是 SP／MR，它是「戒指能力旗標」。`AbilityStatus.cs` 已存快照，不需要圖示。
  - 真正要做的是兩個施法分支：
    - type 1：傳送開目的地窗。`dword_1540828` 的類別**待驗證**，推測是書籤／傳送清單窗。
      - **T275（FU3§3.1）已查**：是書籤窗 `CBookmarkWindowRenewal`（XML 名稱 `BookmarkWindowUI`）。TW16 選定後送 `H 地圖 H x H y`，3.81 要的是 `H 地圖 D 書籤 id`，引擎維持 `HD`。
    - type 5：召喚術開 `CSummonUI`（依賴 #530）。
  - 這兩項建議歸 R2-H（施法流程）。gap-matrix op 122 的「SP／MR 沒有顯示」要更正。

## 8. S185（op 95 S_TAX）第 3 個 C（已驗證）

- **`sub_94D8E0`（FO `0x54CCE0`）**：`"dccc"`。
  - 依 `push` 順序：`d`→`+0x4C` 物件 id；第 1 個 C→`+0x58`；第 2 個 C→`+0x60`（64 位元）；**第 3 個 C→`+0x68`（64 位元）**。
  - 模式 `+0x54`＝1，開 HTML 頁 `tax`。
- **欄位意義**：拿 S136 `sub_93FDF0`（FO `0x53F1F0`）的同一組欄位對照。
  - S136 的 `dddddcsssh` 把第 3 個 D（3.81 註解「數量初始值」）寫到 `+0x68`，第 4 個 D（「最低可換數量」）寫到 `+0x58`，第 5 個 D（「最高可換數量」）寫到 `+0x60`。
  - 所以 **S185 是 `C(最小)＝10`、`C(最大)＝50`、`C(初始值)`**。第 3 個 C 是數字框的初始稅率，推定是目前稅率。
- **3.81**：`S_TaxRate` 只送 `D C(10) C(50)`，沒有第 3 個 C。TW16 會讀到封包補齊的位元組（推測是 0）。
- **對 R2-F**：
  - 稅率數字框的範圍是 10–50。
  - 初始值 3.81 不送。建議用最小值 10，或「目前稅率」——如果有其他來源（例如城堡資料）就用那個。屬於畫面決策。
  - `Castle.cs:43` 的 `DecodeTaxRate` 只解兩個 C，這是對的。

## 9. S_KICK（op 245）的提示窗（已驗證）

- **`sub_940960`（FO `0x53FD60`）**：
  1. 讀 `C(原因)`，依原因取字串。
  2. `dword_136E998`＝2，`sub_A23DE0`、`sub_8C2D10` 收尾。
  3. 把 `+521` 設為 1。
  4. **`sub_8E4670(文字, 1, 1)`**：通用模態訊息框（`dword_13D3624` vtable `+4`，和 S_NEWS 同一個框，見 PBW§5.3）。
  5. **`PostQuitMessage(0)`**：結束程式，不回登入畫面。

| 原因（byte） | 字串 | 內容 |
|---|---|---|
| 22 | 3018／3017（依 `+520`） | 「（嘗試）重複登入，如果懷疑有其他人登入…」 |
| 28 | 4366 | （本機字串表只有 3484 筆，查不到） |
| 31 | 356 | 「別的人以同樣的NetsGo id來登入。」 |
| 38 | 1035 | 「若透過虛擬IP等方式…可能會被限制連接。」 |
| 44 | 359 | 「連線中斷。」 |
| 45 | 833 | 「網頁上的密碼有變更，因此強制結束。」 |
| 83 | 內嵌字串 | GameGuard 未執行或被竄改（`byte_13CE3FE` 為真時是英文；否則是 VA `0x1182D70` 的韓文 CP949「게임가드가 실행 중이 아니거나 변조되었습니다.」） |
| 127 | 1498 | 「與伺服器的連線中斷。」 |
| 180 | 2005 | 「因青少年保護法…」 |
| 222 | 3079 | 「為了預防過度沉迷遊戲…」 |
| 229／230／231 | 3452／3825／3826 | 本機表是 `dummy3454`／查不到 |
| 其他 | **64** | 「連線中斷。」 |

- **3.81**：`S_Disconnect`（S227）＝`H(500) D(0)`，第一個 byte 是 `0xF4`（244），不在表內，所以**一律顯示字串 64「連線中斷。」並結束程式**。
- **引擎**：`WorldSession.cs:112` 收到 227 時丟出 `ProtocolException`，顯示錯誤。
- **建議**（併入 R2-Q，或另開小任務；不屬於 R2-F／H／K）：改成通用模態框顯示字串 64，按確定後結束或回登入畫面。回登入畫面是引擎的選擇，原版是直接結束。

## 10. C_EXTENDED（53）子碼（已驗證）

- 用立即值掃描，只找到 **1 個送出點**：`sub_F4A150`（FO `0xB49550`）的 `sub_9FBC00("ch", 53, 16)`（格式字串 VA `0x11E967C`／FO `0xDE827C`），也就是子碼 **16**。
- **觸發**：聊天指令類別 `Lineage::Commandtimeleft`（vtable `0x11EB69C`／FO `0xDEA29C`，執行函式 `sub_F55FC0`）。
  - 函式一開始：`CodePage` 不是 949（韓）也不是 936（簡中）就直接返回。**繁中 950 永遠不送。**
  - 會送時，旗標 `byte_13CE80F` 為真才送 C53，之後把剩餘時間（字串 1196／1247）印到聊天。
- **相關**：`C_EXTENDED_PROTOBUF`(157) 也只有 1 個立即值送出點：`sub_56FBC0` 的 `"ch", 157, 460`。
- **3.81**：伺服器的 opcode 表沒有對應的擴充送包。
- **結論**：TW 版不會送 C53，**不做**。gap-matrix 第 3 節 C_EXTENDED 列可以把「子碼未列」改成「僅子碼 16，TW 不送」。
- 保留：opcode 用暫存器傳的送出點沒有掃。

## 11. 對應的 R2 組要改什麼

| 組 | 項目 | 依據 |
|---|---|---|
| R2-F | NPC 頁動作 `xchgspell` → `RequestSpellExchangeList()`（3.81 C245 `D(npcObjId)`）；只在 NPC 外型 834 時送，否則不送。`buyspell` 已送 C145，加上「外型 ≠ 834 才送」 | SS2§1.1 |
| R2-F | op 175／44 空清單改開 `HorunEv3`（175）、`GerengEv3`／`Sedia1`／`Siriss1`（44，依外型） | SS2§1.2 |
| R2-F | 稅率數字框：範圍 10–50，初始值由畫面決定（3.81 不送） | SS2§8 |
| R2-H | 冷卻與 18／19 改讀執行期 `SpellList.xml` 的 `delay`／`direction`（有檔時），沒有檔時維持 `ReuseMs`／`Action` | SS2§6 |
| R2-H | S36 type 1：傳送開目的地選擇窗；type 5：召喚術開 `CSummonUI`（依賴 #530） | SS2§7 |
| R2-K | **不做**血盟推薦 UI（原版丟棄 op 133）、**不做** `PledgeNoticeUI`（3.81 沒有 type 17）；改做血盟窗內 7 列關注清單 | SS2§2、§3、§4 |
| R2-L | S36 不是 SP／MR，拿掉「接到左狀態框」這一項 | SS2§7 |
| （另開）| S_KICK：模態框顯示字串 64 | SS2§9 |
| 不做 | C_EXTENDED 53（TW 不送） | SS2§10 |
| R2-D | 死亡統計窗顯示時送 3.81 **C63 `C(9)`**，伺服器回 S250／159 填地監時間清單 | SS2§14 |

## 12. 未驗證與限制

- 全程靜態分析，沒有實機驗證。
- `dword_153FCC4` 是否完全沒有寫入，只檢查了 IDA 的直接引用。（T275 已補：FU3§7，沒有間接寫入。）
- `dword_1540828`（傳送目的地窗）的類別、S36 type 3 的作用，都沒有追。（T275 已補類別：FU3§3.1；type 3 仍沒追。）
- `SpellList.xml` 未寫屬性時的預設值、XML `id` 與 3.81 技能編號的換算，都待驗證。3.81 世代是否同樣讀這個檔也待驗證（`LiTo.bin` 有殼）。（T275 已補預設值與換算：FU3§1；3.81 是否讀檔仍待驗證。）
- S185 第 3 個 C 是「初始值」，依據是和 S136 共用同一組欄位推定；`tax` 頁怎麼讀 `+0x68`，沒有直接追到。
- S_KICK 原因 28、229–231 的字串，在本機 3484 筆的字串表裡沒有。
- `C_BUY_SPELL`／`C_EXCHANGE_SPELL` 的確定鈕送出點，以及暫存器傳 opcode 的送出點，都沒有掃。

## 13. 重跑

- 腳本在 `rea-scripts/t268/`：

  | 腳本 | 用途 |
  |---|---|
  | `ida_t268.py` | IDA 查詢：`dec`／`vt`／`xref`／`calls`／`vtdump`／`dis` |
  | `t268_hyper.py` | HyperAct 名稱 → 建構子 |
  | `t268_str.py` | 讀 VA 上的字串 |
  | `t268_xmltab.py` | 讀 XML 檔名表 |
  | `t268_sites.py` | 列出指定範圍內的送出點 |
  | `t268_spelllist.py` | `SpellList.xml` 統計 |
  | `queries.txt` | 14 輪查詢清單 |

- 執行方式：
  1. 把腳本複製到 `scratch/rea/scripts/`，與 `run_ida.ps1` 放在一起（`run_ida.ps1` 以上一層目錄當根目錄）。
  2. 先跑 `subst`，確認代號沒有被占用。
  3. 執行 `run_ida.ps1 -Script ida_t268.py -List qN.txt -Out oN -Drive W:`。
- 腳本必須是**無 BOM** 的 UTF-8，否則 IDAPython 會出現語法錯誤。

## 14. 補查：死亡統計窗送的 TW16 op 10 子碼 9（T267 轉入 Q-3）

T267（#628）在 [`sprites-and-strings.md`](sprites-and-strings.md) §3.2 把這一項併入 Q-3；同步 main 後補查。

- **TW16 端**（引用 SS§3.1，本輪沒有重開 IDA）：`UserPlayStatsUI` 顯示時（vtable `sub_C0AED0`）呼叫 `sub_C0A110`（VA `0xC0A110`／FO `0x809510`），送格式 `"cc"`＝ op **10**、子碼 **9**。
- **opcode 對應**：TW16 op 10＝`C_RANK_CONTROL`（`sub_951F80` 名稱表，gap-matrix 第 3 節血盟列），對 3.81 **C63**。3.81 C63 的 `RESTARTMENU` 與 `RANK` 同值，實際由 `C_Rank.start` 處理（server-protocol.md）。
- **3.81 處理**（已驗證，讀原始碼）：`1-伺服器/src/com/lineage/server/clientpackets/C_Rank.java`（SHA-256 `0e284028348f160ed06a9241e0ec36ec5e4b0127f3f42dc2368773be76e17e14`）
  - `start` 先讀 `C(data)`，只有 `data == 1` 才再讀 `C(rank) S(name)`。
  - `case 9`（第 273 行）：`pc.sendPackets(new S_PacketBoxMapTimer(pc))`，也就是回 **S250 子碼 159**（`D(筆數)`，每筆 `D(order) S(地圖名) D(分鐘)`）。
  - 同一個 switch 的 `case 8`（第 248 行）是用系統訊息 2535 逐行列出地監剩餘時間；`case 5`／`6` 是生存吶喊；其他子碼記警告後忽略。
- **意義**：子碼 9＝「要求地監時間清單」。統計窗一打開就送，伺服器回 S250／159，正好填 SS§3.1 的 `DungeonTime_List`。
- **3.81 是否支援**：支援，格式一致（只有 opcode＋1 byte 子碼）。
- **引擎現況**：已收 S250／159（`PacketBoxStatus.cs:141`），但沒有送 C63 `C(9)`，所以統計窗打開時清單不會更新。
- **R2-D 要改什麼**：死亡統計窗顯示時送 C63 `C(9)`（2 byte：opcode 63、9）；收到的 S250／159 寫入地監時間清單（照 SS§3.3）。op 55／41、55／44 仍不送。
- **未驗證**：TW16 端的 `"cc"` 與子碼 9 沿用 T267 的讀法，本輪沒有重新反組譯核對；沒有實機抓包。
