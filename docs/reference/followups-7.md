# 查證補遺第 7 輪：送包欄位、舊式窗分頁、料理窗、舊素材 kind、書籤記憶窗，以及 K6 的 19 列

T315（#742）。查 [`gap-matrix.md`](gap-matrix.md) 第 14.7 節的 **Q5-1 到 Q5-5**，並逐列複核第 14.5 節 **K6 的 19 列**。簡稱：本文 **FU7**，引用寫 `FU7§n`。

每節分「原版怎麼做／3.81 是否支援／引擎差異與去向」。記號：
- **已驗證**＝反編譯、反組譯、原始碼或資源檔直接看到；
- **推測**＝依欄位、命名或旁證判斷；
- **待驗證**＝本輪查不到，寫明卡在哪裡。

## 0. 方法與來源

- **TW16 BIN**：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（本輪重算）。
- **TW16 IDB**：P01 `ida/03-ascii-isolated/TW1608262200.idb` 複製到本 worktree 的 `scratch/rea/ida/tw16.idb`，SHA-256 `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f`（與 FU5、FU6 相同）。本輪沒有在副本上建函式或改名。
- **IDA**：IDA 7.7＋Hex-Rays，唯讀批次 19 輪（q1–q19）。
  - q1–q12 用 `rea-scripts/t690/ida_t690.py`；q13 起用 `rea-scripts/t742/ida_t742.py`（同一支，只多一個 `rangegrep LO HI REGEX`：把範圍內每個函式反編譯、存檔、過濾）。
  - 查詢清單在 `rea-scripts/t742/q*.txt`。
- **subst**：開始時沒有任何代號被占用，本輪一律用 `W:`。
  - 第 2、3 輪因為在指令後接了 `Select -First 1`，管線提早中斷，`run_ida.ps1` 的 `finally` 沒跑到，`W:` 殘留（指向本 worktree 的 `scratch/rea`，沒有 idat 在跑）。我手動 `subst W: /D` 後重跑第 3 輪；之後每輪結束 `subst` 都沒有輸出。
- **送出點掃描**：`rea-scripts/wo_sendsites.py`（`sub_9FBC00` 的 opcode 立即值）、`rea-scripts/t716/t716_callscan.py`（線性掃 `E8 rel32`，能抓到 IDA 沒切成函式的呼叫）。
- **地址換算**（同 FU4§0–FU6§0）：程式碼 FO＝VA − `0x400C00`；`.rdata` FO＝VA − `0x401400`；`.data` FO＝VA − `0x401800`。bss 全域沒有 FO，只寫 VA。
- **本機資源**（只讀，不進 git）：
  - `D:\lineage開發\0-客戶端\Text.pak`：在記憶體解出 `string-c.tbl`（`decoded_sha256` `4ebbc1a7…43e9`，3484 筆；腳本 `rea-scripts/t680/t680_strings.py`）與 `list.spz`（SHA-256 `e8cec038…6242`，12230 個 gfx；腳本 `rea-scripts/t180/listspr_109.py`）。
  - 輸出 JSON 只寫在 `scratch/`。
- **3.81 伺服器**（`D:\lineage開發\1-伺服器\src`，唯讀）：`S_PacketBox.java`。
- **引擎**：本分支 `engine/source`（已同步 main `8dc69b25`），只讀，不建置、不執行。
- **K6 複核**：由 1 個唯讀子代理讀 reference 文件與 `engine/source` 逐列整理（第 6 節）。我另外核對了：
  - 原生 UI 預設已改為開啟（`NativeUi/UiRoot.cs:254` `DefaultOn=true`，#751）；
  - `MapAmbientSound.cs:105` 只處理環境音模式；
  - `ObjectRendering.cs:185` 自己也用 `CenterX`；
  - Q6-3 的指令字串，用本輪的 IDA 結果直接查掉（第 6.2 節）。
- 全程靜態分析。沒有啟動遊戲、伺服器或 DB；沒有改產品程式；沒有任何 BIN、IDB、pak 進 git。

## 1. Q5-1 送包欄位

### 1.1 交易詢問旗標 `byte_136E99D`（已驗證）

**這個旗標是玩家的「接受交易詢問」選項，預設 1。**

| 寫入點 | VA／FO | 作用 |
|---|---|---|
| 預設值 | `sub_998250`／`0x597650` | `byte_136E99D = 1`（同一函式還設了其他選項的預設值） |
| 角色設定封包 | `sub_999B50`／`0x598F50`（由 `sub_998840`／`0x597C40` 呼叫） | `byte_136E99D = (a1 & 0x40) != 0`。`sub_998840` 由 S_EVENT（`sub_9357E0`）在 `0x939314`／`0x538714` 呼叫，解的是伺服器存回來的角色設定 |
| 本機設定檔 | `sub_59DD30`／`0x19D130` 讀、`sub_59E920`／`0x19DD20` 寫回 | 同一組選項的本機存檔 |
| HTML 動作 `HyperActAcceptTrade` | `sub_581790`／`0x180B90`（vtable `0x11A98EC`／FO `0xDA84EC`） | 旗標取反，然後重開選項頁 `option13` |
| 聊天指令 `Commandaccepttrade` | `sub_F54360`／`0xB53760`（vtable `0x11EB7E8`／FO `0xDEA3E8`） | 旗標取反，不送包。指令只註冊英文名 `accepttrade`（`sub_F56320` 裡沒有對應的字串表名稱） |

**S_ASK 252 的處理**（`sub_92EAD0`／FO `0x52DED0`，`case 252`，判斷在 `0x92EE32`）：

- 讀 `s`（對方名稱）。
- 旗標為 1：走通用 Yes/No 框（`LABEL_47` → `sub_4FC710`）。
- **旗標為 0：不問，直接自動回覆「拒絕」**：
  - 有挑戰值時：`sub_9FBC00("chdhcs", 33, 0, 挑戰值, 252, 0, "")`；
  - 挑戰值是 −1 時：`sub_9FBC00("chcs", 33, 252, 0, "")`。
  - 第 5 欄的 **0 就是回覆值（拒絕）**；空字串在 `0x1182D53`／`0x1182D67`，內容為空。

**3.81 是否支援**：伺服器只看 C121 的回覆值。引擎 `TradePackets.Answer(challenge,false)`（`Trade.cs:5`）送 `[121,0,0,挑戰值,252,0,0]`，和 TW16 的 `chdhcs` 去掉最後一個空字串後逐 byte 相同（引擎補 0 到 8 的倍數，所以尾端仍是 0）。

**引擎差異**：
- `TradeAskWindow` 固定為 true（`Trade.cs:41`），沒有任何入口能把它設成 false。
- 沒有 `/accepttrade` 指令，也沒有 `option13` 頁的切換動作。
- 旗標 0 時的自動拒絕沒做。

→ **K5**（R6-E，第 8 節）。

### 1.2 C11 數字框回覆的中間 byte（已驗證）

TW16 的回覆是 op 188 `cddcs`，有 3 個送出點：
- `sub_545920`／FO `0x144D20`：盟屋拍賣數字框；
- `sub_876010`／FO `0x475410`：`HYPER_TEXT_WINDOW` 的 default 分支；
- `sub_A7FE20`／FO `0x67F220`：另一個 HTML 窗的同一份程式。

送出的欄位都是：`188, 窗[+76], 數值, (char)窗[+72], 窗+140 的字串`。

**`+72` 與 `+140` 都是 S136 原樣抄回來的**。`sub_93FDF0`（FO `0x53F1F0`）的 `sub_9340B0("dddddcsssh", …)` 參數順序（由 `0x93FE42`–`0x93FEA8` 的 push 讀出）：

| 格式 | 寫到 | 意義 |
|---|---|---|
| d | `+0x4C`（+76） | 物件 id |
| d | `+0x50` | 第 2 個 D |
| d | 區域變數 → `+0x68` | 目前值 |
| d | 區域變數 → `+0x58` | 下限 |
| d | 區域變數 | 上限 |
| **c** | **`+0x48`（+72）** | **回覆時原樣送回的 byte** |
| s（32） | 區域變數 | — |
| s（256） | `Str1` | HTML id |
| s（256） | `+0x8C`（+140） | 回覆時送回的命令字串 |
| h | 區域變數 | 附加字串筆數 |

3.81 的 S136 是 `D D D D D H(0) S S [H S…]`：
- TW16 的 `c` 讀到 `H(0)` 的低位 byte，也就是 0；
- 下一個 `s(32)` 讀到 `H(0)` 的高位 byte，也是 0，所以是空字串；
- 之後兩個 `s` 剛好對上 3.81 的 HTML id 與命令字串。

所以對 3.81 伺服器而言，**原版回覆的中間 byte 一定是 0**，命令字串是 S136 第二個 S。引擎 `AmountPromptProtocol.Reply`（`AmountPrompt.cs:11`、`:48`）送 `C(11) D(objId) D(amount) C(0) S(cmd)`，結果一致。

→ C_DIALOG(51) 列的這一項**已驗證、沒有差異**。windows-2.md §5.3「後段 `c`／`s` 怎麼對應待驗證」也一併解決。

### 1.3 C_BOOK（126）、C_SAVE（150）的送出點（已驗證：原版從不送）

- 名稱只出現在 opcode 名稱表：`C_BOOK` 字串 `0x1185C24`（引用 `0x952597`），`C_SAVE` 字串 `0x11856A4`（引用 `0x952117`）。
- **所有送包只有兩條路**：
  - 最底層送出函式 `sub_9FB7B0`（FO `0x5FABB0`）只有 2 個呼叫點：`sub_9FBC00` 內的 `0x9FBC75`，以及 `sub_9FBC90` 內的 `0x9FBD7E`。線性掃 `E8 rel32` 也一樣是 2 個（`t716_callscan.py 9FB7B0`）。
  - `sub_9FBC90`（FO `0x5FB090`）由 `sub_9FBBE0`／`sub_9FBBC0` 呼叫，固定寫 opcode **157**（protobuf 通道），再帶子 id。
  - `sub_9FBC00` 的 847 個呼叫點裡沒有 126、150（PG§6，本輪重掃一致）。另外 5 個 opcode 不是立即值的呼叫點，PG§6 也已經排除。
  - `sub_9FB4D0`（FO `0x5FA8D0`，14 個呼叫點）只是把格式寫進緩衝區，不會送包。
- 結論：TW16 **沒有任何程式會送 C_BOOK 或 C_SAVE**，兩者只是名稱表裡的殘留名稱。

→ C_BOOKMARK(84)、C_SAVEIO(128) 兩列的這一項沒有差異。

### 1.4 `/on`、`/off` 的語法（已驗證）

原版**沒有** `/on 頻道` 這種寫法，而是「頻道指令＋參數」。參數接受 `on`／`off`（不分大小寫，用 `stricmp` 比對），或字串 401「開」／402「關」。

指令名稱由 `sub_F56320`（FO `0xB55720`）註冊，順序是「建構子 → 英文名 → 字串表名」；類別名用 vtable 的 RTTI 對：

| 指令 | 類別（執行函式） | 參數處理（VA／FO） | 送出 | 本機旗標 |
|---|---|---|---|---|
| `/whisper`、`/悄悄話`（369） | `Commandwhisper`（`sub_F56230`） | `sub_F4A0A0`／`0xB494A0` | `ccc` 184, **2**, 1／0 | `byte_135C1E6` |
| `/receive`、`/收到`（508） | `Commandreceive`（`sub_F55660`） | `sub_F484A0`／`0xB478A0` | 184, **3**, 1／0 | `byte_135C1E7` |
| `/tradechat`（只有英文名） | `Commandtradechat`（`sub_F56040`） | `sub_F4A7B0`／`0xB49BB0` | 184, **6**, 1／0（`unk_165699C` 不成立時才送） | `unk_1656980` |
| `/accepttrade`（只有英文名） | `Commandaccepttrade`（`sub_F54360`） | 無參數 | 不送 | `byte_136E99D`（1.1） |

**選項 HTML 頁的切換動作**（每個都切換後重開頁面）：

| 動作函式（VA／FO） | 送出 | 重開的頁 |
|---|---|---|
| `sub_5827B0`／`0x181BB0` → `sub_F46770` | 184, **4** | `optiong` |
| `sub_582810`／`0x181C10` → `sub_F47A00` | 184, **7** | `option12` |
| `sub_582870`／`0x181C70` → `sub_F484A0` | 184, 3 | `optiong` |
| `sub_5828D0`／`0x181CD0` → `sub_F4A7B0` | 184, 6 | `option12` |
| `sub_585440`／`0x184840` → `sub_F4A0A0` | 184, 2 | `option12` |

另有選項窗套用函式 `sub_6BBD40`（FO `0x2BB140`）：只有值改變時，才依序送 184 的 0、10、9、6。

**3.81**：C26 只處理 type 255／0／2／6（PG§6）。所以會生效的是：`/whisper`（2）、`/tradechat`（6），以及選項窗的 0、6；3、4、7、9、10 送了也會被忽略。

**引擎差異**：
- `ChatChannelCommands`（`ChatChannelToggle.cs:66–92`）用的是 `/on 頻道`、`/off 頻道`，頻道寫「全體／密語／買賣」，原版沒有這種語法。
- 原版的 `/whisper on`、`/悄悄話 關`、`/tradechat off` 在引擎會被當成一般聊天送出。

→ **K5**（R6-E）。

### 1.5 刪除物品的本地確認（已驗證）

刪除佇列由 `sub_98D2E0`（FO `0x58C6E0`）處理，逐筆走下面的流程：

1. **可堆疊、數量 >1、還沒指定數量**：開數量輸入框 `sub_4FD210`（FO `0xFC610`），提示字串 **4115**（帶名稱與數量）。回呼 `sub_500900`（FO `0xFFD00`）：輸入空白＝全部；有輸入就照輸入的數量（數字字串）；之後重跑佇列。
2. **確認框**：`sub_4FD820`（FO `0xFCC20`）。
   - 數量 1：用字串 **3122**「"%s" 刪除後，即無法再次復原，真的要刪除嗎？ (Y/N)」。
   - 數量 >1：用字串 **4114**。
   - 回呼 `sub_4FF810`（FO `0xFEC10`）：取消時把這筆從佇列移除。
   - 是否要確認由 `sub_996B10(0, 物品)` 決定（物品類別的虛擬判斷）。哪些物品需要確認**沒有逐類追**。
3. 佇列處理完，`sub_9A15D0`（FO `0x5A09D0`）**一次送一包** op 198 `cdb`（批次刪除）。另有單筆路徑 `sub_4FDA00` → `sub_4FF990`（FO `0xFED90`）：`cddd` 198, 1, 物件, 數量。

- 兩種框都是 `sub_AC98E0`，也就是聊天輸入列的提示加 Y/N（FU6§1.1 的「輸入框」、WIN2§5.3 的 `InputWindow`）。**不是彈出式訊息框**。
- 本機 `string-c.tbl` 只有 3484 筆，4114、4115 查不到文字。

**3.81**：C138 一次刪一件（`ItemTransfer.cs` 現有）。TW16 的批次 `cdb` 格式 3.81 不讀，不必照做。

**引擎差異**：用 `MessageBox` YesNo、預設「否」（`ItemTransfer.cs:116`）。
- 原版是聊天輸入列 Y/N，文字是 3122；
- 堆疊物品原版先問數量。

→ **K5**（R6-E）：改用聊天列 Y/N 提示（字串 3122，有表讀表）；堆疊物品先問數量（4115 本機沒有，用空白提示並註明）。

## 2. Q5-2 舊式窗分頁

### 2.1 原版怎麼做（已驗證：TW16 的舊式窗不是 3.81 的版面）

**背包 `InventoryWindow`**（基底建構子 `sub_88B060`／FO `0x48A460`，vtable `0x117DF84`）：
- 雖然讀 `uimagicitem.xml`（失敗時印 `uimagicitem.xml resource is not updated.`），但分頁鈕用的是 **TW16 自己的 surf**：878–881（35×17，分頁 0–3），加上 1600（20×18，分頁 4）。
- 回呼都是 `sub_89B050`（FO `0x49A450`）：播音效 2838；分頁 4 另走 `sub_8969A0`（送 `ccc` 140, 6, 1，或印 2060／4451）。
- 本機 XML 的 2048–2051 分頁圖它不用。

**魔法窗 `SpellWindow`**（建構子 `sub_DFC1E0`／FO `0x9FB5E0`，vtable `0x11DEBA4`）：
- 每個職業有自己的一排鈕，surf 也是 TW16 自己的：6263（0）、6260／6272／6281／6269／6278（1）、6275（2）、5211＋3i／5244＋3i（等級）、5256、5319、5314、9052、9055……
- 鈕數＝`dword_11DEA70[職業]`＋1。表 `0x11DEA70`／FO `0xDDD670`，依職業 0–7 是 5、4、13、11、8、5、6、5。
- 點鈕：`sub_E02BC0`（FO `0xA01FC0`）→ `sub_E02CA0`（FO `0xA020A0`）：
  - **鈕 1＝職業技能頁**：顯示 `dword_11DE910[2×職業]` 到 `[2×職業+1]` 的技能格。表 `0x11DE910`／FO `0xDDD510`，依職業 0–7 是 48–57、8–16、48–99、81–80（空）、16–42、0–15、0–20、8–23。
  - 鈕 ≥3：等級頁 `sub_E01B70(鈕−3)`（FO `0xA00F70`）。每頁格數由 `sub_E00890(職業)` 決定；王族鈕 2、戰士、妖精鈕 10 有特例。
  - 魔法師（職業 3）鈕 ≥3 時，索引要減 1。
  - 格號是 TW16 自己的技能陣列索引，不是 3.81 的技能 id。
- 本機 XML 的 2058–2086 等級鈕它也不用。

**倉庫**：TW16 一律開 XML 版 `RenewalWareHouseUI`（WIN2§2.1 第 5 點）。`WAREHOUSE_WINDOW` 只有設定讀取者 `sub_A842D0`，TW16 沒有消費它的舊式倉庫類別。

### 2.2 結論

- TW16 給了一個**旁證（推測）**：舊式魔法窗把職業技能放在**獨立的一個鈕**（等級鈕之外），而不是混在等級頁裡。
- 但 3.81 `uimagicitem.xml` 的鈕組是：王族 2058–2064 共 7 個、一般 2065–2074 共 10 個、妖精 2075–2085 共 11 個、黑妖 2086 起。它們由 3.81 `LiTo.bin` 消費，TW16 不讀。所以：
  - 「3.81 職業技能（id 97–176）在哪個鈕」
  - 「背包任務分頁、倉庫四個分頁怎麼分類」

  這兩題**都不能從 TW16 讀出**。`LiTo.bin` 有 Themida 殼，不在靜態分析範圍。→ **待驗證**（需要真機看 3.81 客戶端，或脫殼的 `LiTo.bin`）。
- **建議（推測，給 code 任務參考，不改狀態）**：各職業 XML 鈕數比一般 10 級多出來的那幾個鈕（例如妖精 11＝10＋1），就是職業技能頁。王族 7 個、黑妖 5 個比 10 少，可能是「職業能學到的等級＋職業技能」。這要真機確認後才能寫進程式。

## 3. Q5-3 料理窗規則（已驗證）

**類別**：`CCookWindow` vtable `0x1190534`（FO `0xD8F134`），建構子 `sub_A5AAC0`（FO `0x659EC0`）。清單是另一個類別（vtable 在 `0x11904D0`–`0x1190528`），項目類別是 `CookListItem`（`sub_A5B150`）。

| 項目 | 原版（VA／FO） | 引擎現況（推測值） | 判定 |
|---|---|---|---|
| 讀表 | `sub_A5CE60`（`0x65C260`）讀 `recipe.tbl`：每筆「名稱、全名、編號 d、圖示 h、材料數 c、材料×n」。**編號 ≥ 0x30（48）的整筆不算數**（`v26[4] -= v22`），材料也不收；編號 <24 的說明字串 id 取自 `dword_137A0C0[3i+2]`（`.data` FO `0xF788C0`） | 編號 80 起不列（`Cooking.cs:39`） | 結果一樣不列 80–82，但**原因是「≥48 全部排除」**，不是「≥80」 |
| 清單內容 | `sub_A5CDE0`（`0x65C1E0`）只加 **24 筆**（i<24，圖示取 `dword_137A0C0[3i+1]`）；項目上限 48（`sub_A5B7E0` `this[11] <= 48`） | 讀全部 27 筆，篩掉 ≥80 | 本機 recipe.tbl 是 0–23 加 80–82，結果相同 |
| 等級篩選 | `sub_A5CB70`（`0x65BF70`）：先 `sub_A5B980` 把 24 筆全設為不顯示，再把**前 8×L 筆**（L＝`dword_153F284`）設 `+288=1` | 編號 < 8×L（`Cooking.cs:54`） | **一致**（依表內順序；本機表 0–23 已依序排） |
| 版面 | `sub_A5BA50`（`0x65AE50`）：**4 欄的圖示格**，起點在窗內 (+29, +31)，欄距 `this[8]`、列距 `this[9]`，一次最多 24 格；滑過時畫名稱與材料的提示框（寬 142，每行 12 px）。捲動一次 4 格（`sub_A5E6F0` `+40 = 4×n`） | 單欄文字列，每列 15 px（`NativeUi/CookBookPanel.cs:40`） | **不一致**：原版是圖示格＋提示框，不是文字列。欄距／列距數值由建構子傳入，本輪沒取 |
| 等級圖 | `sub_A5C5E0`（`0x65B9E0`）畫第 L−1 張（L＝value＋1，所以是第 value 張，即 2545＋value） | 2545＋value（`Cooking.cs:52`） | **一致** |
| 量表 | 只在 `dword_153F288 > 0` 時畫：寬＝`50 × (剩餘 >> 1) / 總量`、高 8。剩餘由 `sub_A5B920`（`0x65AD20`）每 500 ms 減 1。啟動點：S_MESSAGE_CODE（`sub_942B40`／`0x541F40`）收到訊息 6392（`0x18F8`）、6394（`0x18FA`）等時，設成「食譜 `+0x104` × 2」（`0x9441F0` 起）。**這是製作中的倒數條，不是等級** | 填充＝等級／11（`CookBookPanel.cs:80`） | **不一致**。3.81 本機字串表只有 3484 筆，伺服器不會送 6392 這類 id（推測），所以 3.81 下量表應**不顯示** |
| 製作 | `sub_A5D2A0`（`0x65C6A0`）：沒有料理書（`dword_153F294 == −1`）印 1167「為了製作料理，所以需要料理書。」；倒數中印 1100「正在執行中...」；否則送 `cdcc` 208, 書, 1, 編號 | 選取後再點一下（或雙擊）送 C164 D C(1) C(編號) | 封包一致。觸發方式在清單類別的事件裡，本輪沒逐一追（推測是點格子） |

→ 引擎改動歸 **K5**（R6-F，第 8 節）：
- 清單改 4 欄圖示格＋提示框；
- 量表在 3.81 下不畫（或改成倒數）；
- 篩選規則改寫成「≥48 排除、前 8×L 筆」；
- 1167／1100 提示。

## 4. Q5-4 舊素材的 109 kind、疊層深度、播完時機

### 4.1 舊素材 gfx 的 kind（已驗證，本機 `list.spz`）

`LegacyMagic.Handles` 的 7 個 gfx（`MagicRendering.cs:185`）：

| gfx | 名稱 | 109 kind／param |
|---|---|---|
| 11722 | renewal heal eff sc | 0／−1（沒有 109） |
| 221 | shield | 0／−1 |
| 2165 | holy weapon e | 0／−1 |
| 189 | red potion | 0／−1 |
| 191 | green potion | 0／−1 |
| 197 | water potion | 0／−1 |
| 167 | energy bolt | 4／219 |

- 6 個都是預設類別（kind 0），所以 `UsesNativeClass` 對它們永遠是 false，走舊素材路徑是對的。
- 167 照舊走自己的飛行＋219 落點路徑（也是對的，和 SV§4 一致）。
- 全表 kind 分布：0＝11916、1＝102、2＝8、3＝29、4＝5、5＝129、6＝12、7＝23、8＝6。

### 4.2 10147／10145 疊層深度（已驗證：取值時機與引擎推測相反）

- `dword_13CCBA4` 的寫入只有建立（`sub_81C340` +29）與 kind 7 終點（`sub_81C9D0` 歸 0）。讀取在兩個**每格推進函式**裡：
  - `sub_81C7C0`（FO `0x41BBC0`，EarthRising）在 `0x81C7FA`：gfx 10147 時讀 `dword_13CCBA4`，當場傳給 `sub_A0BA50`（FO `0x60AE50`）；
  - `sub_81C9D0`（FO `0x41BDD0`，Eruption 段）在 `0x81CA25`：gfx 10145 一樣。
- 也就是說，原版**每推進一格就讀一次全域的當下值**，不是建立時記住。
- 引擎 `EarthRisingBuilt`／`SegmentBuilt`（`ProjectileRendering.cs:59–60` 的「推測」註解、`:73–80`）在建立時存下值，**不一致**。
- 實際效果：
  - 同一刻推進的實例會拿到相同深度；
  - kind 7 終點把全域歸 0 之後，所有還活著的 10147／10145 下一格都回到原本的 z。
- → **K5**（併 R6-F 或特效組）：改成每格推進時讀當下的 `earthCounter`。

### 4.3 「播完」的時機（已驗證，TWP§3.2）

影格號繞回 0 時刪除物件，整段只播一次（`sub_81C7C0` 用 `sub_A231C0(該格時間, sub_81D090)` 排下一格）。提亮的「播完減 1」也是同一個時刻（FU5§3）。這兩點文件早已是已驗證；14.3 寫的「推定」，指的是引擎用 list.spr 影格總長換算毫秒。只要 list.spr 讀得到，換算結果和原版逐格推進相同，**沒有已知差異**。

→ 第 5 節「（109 kind」列改歸 **K5**，剩下的差異只有 4.2。

## 5. Q5-5 `BookmarkMemoryWindowUI` 的開窗子碼（已驗證：是 142，不是 141）

- S_EVENT（`sub_9357E0`）的 jumptable `0x935850`：
  - **case 141**（`0x93D5E5`／FO `0x53C9E5`）：讀 `c`，呼叫 `dword_1540828` 的 `sub_BDFB40`（FO `0x7DEF40`），再印字串 **2885**「擴張最大記憶空間10個。」。這才是 `BOOKMARK_EXPAND`（書籤上限 +10）。
  - **case 142**（`0x93D64B`／FO `0x53CA4B`）：讀 `c`，呼叫 **`dword_1540824`（`BookmarkMemoryWindowUI`）** 的 `sub_BD3E40`（FO `0x7D3240`）。
- `sub_BD3E40` 的內容：
  - 讀 `s`（13 字的名稱）；
  - 印 2987「製作者：」＋名稱；
  - 再讀 `c` 筆數 × `sdhh`（書籤名、d、x、y）；
  - 第一個 `c` 是 0 或 1 時，決定是否帶入目前記錄。

  也就是「**顯示別人分享的書籤清單**」的窗，不是擴充書籤上限。
- **3.81**：`S_PacketBox.java:334` 定義了 `BOOKMARK_EXPAND = 141`，但整個 `src` 沒有任何地方使用；142 沒有定義。PG§2 也把 141、142 列在「伺服器不送」。
- → 第 4.1 節 `BookmarkMemoryWindowUI` 列改歸 **K1**（3.81 伺服器不送 142，也不送 141）。WIN2§6「子碼推測為 141」更正為 142。

## 6. K6 的 19 列複核

### 6.1 逐列結果

判定規則照 14.1，只改一點：#751 已把原生 UI 改成預設開啟，所以「只在原生 UI 下」不再算差異。「已有」都還沒有真機看過（原生 UI 預設開啟後的 smoke 是 T201／#475）。

| 列 | 原版（依據） | 引擎現況 | 剩下的差異 | 去向 |
|---|---|---|---|---|
| 1:36 S_TITLE | AN§2.2–2.4：自己的 X0＝腳下格螢幕 X − 3·len − 56；他人 `L+(W−6·len)/2` | 只畫指到的物件、位置與外框照原版（`ObjectRendering.cs:183–188`、`:232–234`），字型 #759；**自己也用 `CenterX`**（`:185`） | 自己名稱／稱號的 X0 沒照原版；稱號只在 `0x15442E4` 非 0 時畫（推測）引擎一律畫 | **K5**（R6-B） |
| 1:86 S_EFFECT | SV§2.5：以物件朝向播放；只有 6985、7179、8150、8152、8981、7834、7836 跟著物件 | `MapEffects.cs:102–111` 方向固定 0；`:190–210` 每格都重讀目標位置 | 方向；跟隨範圍 | **K5**（R6-A） |
| 1:181 S_IDENTIFY_CODE | reference 沒有任何文件查過 `sub_940070` | 畫面下方白字 8 秒（`DisplayNotices.cs:10`、`:74–83`、`:126`），是自訂 | 原版顯示方式整個沒查 | **K2**（Q6-1） |
| 1:29 S_PERSONAL_SHOP_LIST | WIN§2 T102、TWP§1.1–1.6：`TradeWindow` 模式 13／14（BUY 1992／SELL 1998、5 級換色、13 合計 >99999 不送、14 一次 >8 筆不送＋897） | 私商仍是 WinForms（`PrivateShopUi.cs`），一次一列（`PrivateShop.cs:16–20`）；NPC 商店已走原生 `TRADE_WINDOW`（`ShopTradeSource.cs:18–29`） | 私商沒接進 `TRADE_WINDOW` 13／14 | **K5**（R6-C） |
| 2:107 S_VERSION_CHECK | PG§4 | 送 C14、解 S139、語系表、旗標（`ServerFeatures.cs:13–51`、`:74`）；拾取節流讀旗標（`Ground.cs:85–86`） | 無已知差異（狀態非 0 不結束是刻意的，3.81 一律送 0） | **已有** |
| 3:C_VERSION(122) | 同上 | 同上 | 同上 | **已有** |
| 3:C_DUEL(118) | AN§2.6、AN#14 | 決鬥標籤已拿掉，改可直接攻擊＋游標（`DuelUi.cs:38–46`、`ClanWar.cs:117`）；入口只有開發工具分頁 | 攻擊游標編號（`sub_F044C0`）、op 118 的送出點沒查；`Duel.cs:55` 提示文字過時 | **K2**（Q6-2） |
| 3:C_WHO(174)、C_CHECK_PK(9) | WIN§2 T140、SS2§5；**本輪查到指令字串**（6.2） | `/who`、`/rank`、`/check`（`PlayerQuery.cs:25`）；`checkpk` HTML 入口已接（`NpcInteraction.cs:96`） | 原版是 `/checkpk`（只有英文），不是 `/check`；沒有 `/誰`、`/階級` 別名 | **K5**（R6-E） |
| 3:C_ACTION(50) | AN§1.1–1.2 | 編碼、選路、節流、1234 ms 保留、只擋忙碌（`TitleAndEmote.cs:21–52`、`:104–146`，R5-D #736） | 無（入口差異歸 BehaviourUI） | **已有** |
| 4:WidgetSlot | UI§2 V5：TW16 `SlotWidgetUI`，XML 不在本機 | 原生 3.81 世代巨集框（`NativeUi/MacroPanel.cs:4–6`、`:103–116`） | 只差 `SlotWidgetUI` 外觀；XML 本機沒有，T318 沒查參考客戶端 | **K3** |
| 4:BehaviourUI | FU4§1.1 Ctrl+D、AN§1.1 `sub_AD8EF0` | 表情只能從開發工具分頁送（`TitleAndEmote.cs:339–355`）；Ctrl+D 沒接（`NativeUi/MenuButtonsPanel.cs:81`） | `Actions.xml` 的版面與項目沒有文件 | **K2**（Q6-4） |
| 4:PopUpUI | WIN2§6：本機觸發、建議不做 | 無 | 無需處理 | **K1** |
| 5:`sub_87C890` | REA B07：欄位 103、105–107、111–114 的讀取者沒查 | 用到的欄位都有依據 | 不知道還缺什麼 | **K2**（Q6-5） |
| 5:`sub_F0E9B0` | REA B04、EF2§1：`<` 63 個 id／1,751 筆、`[` 10 個 id／61 筆走 `A060F0` 單次音效 | 光源、`]`（#735）、`^` 已做；`MapAmbientSound.cs:105` 只處理環境音 | `<`／`[` 單次音效沒播 | **K5**（R6-A） |
| 5:`sub_A51920` | PG§7 | 整筆取代同 id（`WorldObjects.cs:37–46`） | 只剩「保留同一個 actor」的可見差異，PG§7 判定不另開任務 | **已有**（待真機） |
| 5:`sub_5001C0` | WO§2；UI§2 V5 (b)：F4＝`sub_500640` 鍵盤拾取 | 點擊拾取已做（`Ground.cs`）；**F4 沒有拾取**（`PlayerHudIntegration.cs:50`、`NativeUi/State/MacroState.cs:83–84`） | F4 鍵盤拾取；牆阻擋（bit 0 推測，另列 Q6-7） | **K5**（R6-D） |
| 5:`byte_13CE51E` | WO§4：值≠0 時也擋道具／技能快捷 | M／U／G、滑鼠攻擊、拾取都已擋；**巨集格沒擋**（`PlayerHudModel.cs:38–47`、`NativeUi/State/MacroState.cs:93–98`） | 巨集格；值＝1 的狀態語義未明（Q6-8） | **K5**（R6-D） |
| 5:（表情選單 | AN§1.1–1.2、§3.4 | 順序、重映射、缺圖退回（`TitleAndEmote.cs:13`、`:21–38`、`:225–229`） | 無（選單入口歸 BehaviourUI） | **已有** |
| 5:`sub_8E66D0` | UI§2 V1：自己 x＝P.x＋偏移−80（`sub_F1A750` 沒展開）；閃爍版 `sub_8E4B90`（surf 8393） | `NativeUi/HpMpPanel.cs:217–274`，自己也用 bbox 公式（`:242–243` 註明待驗證） | 自己的 x、閃爍條、灰／綠變體都沒查 | **K2**（Q6-6） |

**去向統計（K6 的 19 列）**：已有 **5**、K2 **5**、K5 **7**、K3 **1**、K1 **1**。

各類的列：
- 已有：2:107、3:C_VERSION、3:C_ACTION、5:`sub_A51920`、5:（表情選單。
- K2：1:181、3:C_DUEL、4:BehaviourUI、5:`sub_87C890`、5:`sub_8E66D0`。
- K5：1:36、1:86、1:29、3:C_WHO、5:`sub_F0E9B0`、5:`sub_5001C0`、5:`byte_13CE51E`。
- K3：4:WidgetSlot。
- K1：4:PopUpUI。

子代理原本把 3:C_WHO 歸 K2（要查指令字串），本輪用 IDA 查掉後改歸 K5。

### 6.2 Q6-3 指令字串（本輪順手查完，已驗證）

`sub_F56320` 的註冊（建構子 → 英文名 → 字串表名）：

| 類別（建構子） | 英文 | 字串表 |
|---|---|---|
| `sub_F53090` | `who` | 360「誰」 |
| `sub_F51890` | `rank` | 779「階級」 |
| `sub_F51770` | `query` | 無 |
| `Commandcheckpk`（vtable `0x11EB784`，`sub_F54A30`，SS2§5） | `checkpk` | 無；在 `sub_F56320` 裡排在 `outparty`／412 之後、`focus` 之前，沒有字串表名 |

註冊表裡沒有 `check`。

## 7. Q5 影響的 K2 列：新去向

| 列 | 原分類 | 新去向 | 依據 |
|---|---|---|---|
| 3:C_ASK_XCHG(5) | K2 | **K5**（R6-E） | FU7§1.1：旗標＝選項，預設 1；0 時自動回覆拒絕 |
| 3:C_DIALOG(51) | K2 | **已有** | FU7§1.2：中間 byte＝S136 的 `c`，3.81 一定是 0；版面差異已歸 op 144（原生 `HyperTextPanel`，現已預設開啟） |
| 3:C_BOOKMARK(84) | K2 | **已有** | FU7§1.3：C_BOOK 原版從不送；新增／刪除已由 R5-B（#734）做進原生書籤窗 |
| 3:C_SAVEIO(128) | K2 | **已有** | FU7§1.3：C_SAVE 原版從不送；C244 存取已有（`CharacterConfig.cs`） |
| 3:C_ONOFF(184) | K2 | **K5**（R6-E） | FU7§1.4 |
| 3:C_DROP(97) | K2 | **K5**（R6-E） | FU7§1.5 |
| 5:（109 kind | K2 | **K5**（R6-F） | FU7§4.2；kind 本身沒有差異（§4.1） |
| 4:BookmarkMemoryWindowUI | K2 | **K1** | FU7§5：開窗是 142，3.81 不送 |

第 4.1 節 Inventory、SpellUI、Warehouse 與第 1 節 op 127 是 K4 列，本輪只更新依據：分頁分類**待驗證**，TW16 讀不出（FU7§2）。第 4.2 節 `CCookWindow` 也是 K4 列，依據改成 FU7§3，程式差異進 R6-F。

## 8. 建議 code 任務（R6 組）

原則同 14.6：
- 同一個既有檔案只分給一組；
- 新測試用新檔、自動發現；
- 不改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`；
- PR 不改 `rebuild-lock.json`。

R5-A–I 都沒有涵蓋這些差異。

| 組 | 目標 | 可改的檔案 | 依據 | 依賴 |
|---|---|---|---|---|
| R6-A 封包特效與地圖單次音效 | ① S55 用物件收包時的朝向；② 只有 6985、7179、8150、8152、8981、7834、7836 每格跟著物件，其他留在收包位置；③ 地圖特效的 `<`／`[` 走單次音效（距離音量、共用冷卻） | `MapEffects.cs`（只限 S55）、`MapAmbientSound.cs`、`ProductSoundBackends.cs`（只加單次音效接線，推測需要）、新檔 `MapEffectFollowupTests.cs` | SV§2.5；EF2§1、§4；REA B04；第 1 節 op 86；第 5 節 `sub_F0E9B0` | 無（R5-C #735、R5-E #737 已合併） |
| R6-B 自己的名稱位置 | `NameLayout` 對自己改用「腳下格螢幕 X − 3·len − 56」，名稱、稱號、盟徽一起移 | `ObjectRendering.cs`（只限 `NameLayout`）、新檔 `SelfNameLayoutTests.cs` | AN§2.3；第 1 節 op 36 | 無；腳下格基準的換算是推測，要和 Q6-6 一起看 |
| R6-C 私商 `TRADE_WINDOW` 13／14 | 照 `ShopTradeSource`，把私商清單接進原生 `TRADE_WINDOW`：買 13、賣 14、單價 5 級換色、兩個上限、多筆每筆送 C161 | `PrivateShop.cs`、`PrivateShopUi.cs`、`WeaponRepairUi.cs`（只加來源）、`NativeUi/TradeWindowPanel.cs`、新檔 `PrivateShopTradeSource.cs`、`PrivateShopTradeTests.cs` | WIN§2 T102；TWP§1.1–1.6；第 1 節 op 29 | 無；多筆 C161 的編法先對 SP 原始碼 |
| R6-D 鍵盤拾取與幽靈快捷 | ① F4 撿 1 格內第一個 type 9：先轉向、整疊、不問數量，和點擊共用節流；② 幽靈中擋巨集格的道具／技能（F5–F12、雙擊格子） | `Ground.cs`、`OnlineInput.cs`、`PlayerHudIntegration.cs`、`PlayerHudModel.cs`、新檔 `PickupGhostFollowupTests.cs` | WO§2、§4；UI§2 V5 (b)；第 5 節 `sub_5001C0`、`byte_13CE51E` | 無 |
| R6-E 聊天指令與本地確認 | ① `/accepttrade`＋HTML 動作 `HyperActAcceptTrade`（`option13` 頁）切換交易詢問旗標，預設 1；旗標 0 時收到 S219/252 直接送 `Answer(challenge,false)`，不開詢問。② 頻道指令改成原版語法：`/whisper`、`/悄悄話` 加 on/off（或 開/關）→ C26 type 2；`/tradechat` → type 6；`/receive`、`/收到` → type 3（3.81 忽略，可只記錄）。舊的 `/on 頻道` 是否保留由審查決定。③ `/checkpk` 送 C51；加 `/誰`、`/階級` 別名；`/check` 不是原版指令（保留與否由審查決定）。④ 刪除物品改用聊天列 Y/N（字串 3122，有表讀表）；堆疊物品先問數量 | `Trade.cs`、`ChatChannelToggle.cs`、`PlayerChat.cs`、`PlayerQuery.cs`、`ItemTransfer.cs`、`NpcInteraction.cs`（只加 HTML 動作）、新檔 `ChatCommandFollowupTests.cs` | FU7§1.1、§1.4、§1.5、§6.2；第 3 節 C_ASK_XCHG、C_ONOFF、C_DROP、C_WHO | 無；旗標要不要跟 C244 角色設定一起存（原版位元 `0x40`），由這組決定 |
| R6-F 料理窗與地裂深度 | ① 料理清單改 4 欄圖示格（一次 24 格、一次捲 4 格），滑過顯示名稱＋材料提示框（每行 12 px）；篩選照「編號 ≥48 排除、前 8×L 筆」。② 量表只在製作倒數中畫（3.81 下不畫）。③ 1167／1100 提示。④ 10147／10145 疊層深度改成每格推進時讀當下的計數 | `NativeUi/CookBookPanel.cs`、`Cooking.cs`、`CookBookFallbackUi.cs`（只改篩選呼叫）、`ProjectileRendering.cs`（只限 `EarthDepth`）、新檔 `CookAndDepthFollowupTests.cs` | FU7§3、§4.2；第 4.2 節 `CCookWindow`；第 5 節 109 kind | 無；欄距／列距的數值要先從清單建構子取（`sub_A5AAC0` 傳給 `sub_8FAB00` 的參數） |

**建議順序**：R6-C、R6-D、R6-E 可以馬上派。R6-A、R6-B、R6-F 範圍小，可以合併成兩組。

**需先查證（Q6）**：以下各項沒有程式依據，建議開 1 張 resource 任務。

| 代號 | 要查的內容 | 起點 | 影響的列 |
|---|---|---|---|
| Q6-1 | S_IDENTIFY_CODE 的顯示方式 | `sub_940070`（FO `0x53F470`） | 1:181 |
| Q6-2 | C_DUEL 的送出點；決鬥游標編號 | `wo_sendsites.py 118`；`sub_F044C0`（FO `0xB038C0`） | 3:C_DUEL(118) |
| Q6-4 | `BehaviourUI` 的版面、項目、回呼 | `Actions.xml`；建構子 VA `0x7C6910`；`sub_AD8EF0` | 4:BehaviourUI |
| Q6-5 | list.spr 未知欄位的讀取者 | `0x13CD86C`、`0x13CD87C`、`sub_8787B0`、`0x13CD854`、`0x13CD858`、`0x13CD860`、`0x13CD870`、`0x13CD874`、`0x13CD878` | 5:`sub_87C890` |
| Q6-6 | 頭上條：自己的 x 基準、閃爍門檻、+86／+87 變體；參考客戶端有沒有 surf 8393 | `sub_F1A750`、`sub_8E4B90` | 5:`sub_8E66D0`（也影響 R6-B） |
| Q6-7 | 拾取牆阻擋的地格旗標 bit 0 | `sub_F12490` | 5:`sub_5001C0` |
| Q6-8 | `byte_13CE51E`＝1 的語義 | `sub_A50100`（FO `0x64F500`）、`sub_5069E0` | 5:`byte_13CE51E` |
| Q6-9 | 刪除確認的物品條件；料理清單的欄距／列距與點擊事件 | `sub_996B10` 的虛擬判斷；清單類別 vtable `0x11904D0` | 3:C_DROP、4.2 `CCookWindow`（細節，不影響分類） |

Q6-3 已在本輪完成（6.2）。另外，`SlotWidgetUI.xml`／`SubSlotWidgetUI.xml` 建議交給 P-A 的參考客戶端 XML 來源（#757）順便查，結果決定 4:WidgetSlot 能不能離開 K3。

## 9. 腳本與查詢

| 檔案 | 用途 |
|---|---|
| `rea-scripts/t742/ida_t742.py` | `ida_t690.py`＋`rangegrep`（唯讀；q13 起使用） |
| `rea-scripts/t742/q1.txt`–`q19.txt` | 本輪 19 輪 IDA 查詢 |
| `rea-scripts/t680/t680_strings.py` | 字串 3122、3049、401、402、1005、3322、368、369、508、399、2987、1167、1100、2885、360、779、956、412；4114、4115、4381 本機沒有 |
| `rea-scripts/t180/listspr_109.py` | 7 個舊素材 gfx 與 10147、10145、7374 的 109 |
| `rea-scripts/wo_sendsites.py` | op 51、126、150、184、33、41、97、188、198、128 的送出點 |
| `rea-scripts/t716/t716_callscan.py` | `sub_9FB7B0`、`sub_9FBC90`、`sub_4FDA00`、`sub_4FF990`、`sub_98D2E0` 的呼叫點 |

## 10. 限制

- 全部是靜態分析，沒有實機觀察。原版以 TW16 為準；3.81 `LiTo.bin` 有殼，不在範圍。所以 Q5-2 的 3.81 分頁分類只能**待驗證**。
- 本機字串表只有 3484 筆，4114、4115（刪除數量相關）、6392、6394（料理倒數的觸發訊息）查不到文字。「3.81 不送 6392」是推測。
- 指令名稱的配對靠「建構子 → 英文 → 字串表」的順序。`accepttrade`、`checkpk` 的建構子沒有落在這個模式裡，所以它們的類別是用 vtable RTTI 對的。
- K6 的表由子代理整理，「原版」欄引用既有文件；我核對了 6.1 開頭列的幾處與 Q6-3，其餘 `檔案:行` 沒有逐一重看。
- 「已有」只表示程式與已查證的表現沒有已知差異；原生 UI 預設開啟後還沒有真機 smoke（T201／#475）。
