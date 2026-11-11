# 查證補遺第 17 輪（FU17）：Q9-2～Q9-5 與 S64 0x48（T422／#966）

依據：[`followups-15.md`](followups-15.md) §3「建議追加的查證 Q9」（T402／#917，PR #964）。Q9-1 已由 #956（[`followups-16.md`](followups-16.md)）處理，本輪不重複。

記號：**已驗證**＝反編譯、反組譯、資源檔或伺服器原始碼直接看到；**推測**＝依欄位、命名或旁證判斷；**待驗證**＝本輪查不到，寫明卡在哪裡。

**結論**
- **Q9-2**：書籤窗的區域表**不是書籤窗自己的資料**。它是全域區域表 `dword_16568A8`，從 **`zone3.xml`** 讀進來；`+66` 是 `<zone>` 底下的 **`<bookmark id="N"/>`**，`+0` 是區域名稱。本機 3.81 客戶端**沒有** `zone3.xml`，也沒有 `zone3desc-c.tbl`（24 個 idx 都查過），所以 3.81 不支援。引擎維持用地圖編號（§1）。
- **Q9-3**：元件 0–5 是 **0＝MainCharInfoUI、1＝MainChatUI、2＝MiniMapUI、3＝WidgetSlot、4＝WidgetSubSlot、5＝MainButtonUI**（`sub_705180`）。所以 `ModeChange` 是「**小地圖 ↔ 擴充快捷欄**」二選一：模式 0 顯示小地圖、藏 WidgetSubSlot；模式 1 相反。其他 4 個元件一律顯示。**Q8-B ② 可以開工**（§2）。
- **Q9-4**：
  - `<subHtml src="sN">` 是在附屬檢視裡載入另一頁 `sN`。`sN-c.html` 只有一張 `<img src="#N">`，也就是道具說明的插圖。
  - `<date>` 印伺服器遊戲時間的「月／日」（字串 762／763）。
  - `<may>…<may>` 是條件區塊：前一個 `<var>` 的值是 0，區塊就不輸出。
  - `<var>` N≥998 各 id 對到的全域變數、顯示字串、選項頁標籤都列在 §3.4。
  - 3.81 的標記表**有** `date`／`time`／`var`，**沒有** `may`／`subHtml`；3.81 的 `<var>` 只有 998、999、1002–1029。
- **Q9-5**：`sub_51F5D0` 是**不雅字過濾**。它用 `obscene` 表（本機 `Text.pak` 的 `obscene-c.tbl`），把每個找到的詞換成「字數個 `*`」。血盟備註**一律**過濾後才送出（§4）。
- **S64 0x48**：3.81 伺服器**不送**。8 個寫 `S_OPCODE_CHARRESET` 的封包類別都看過了，子類只有 0x01–0x04、0x0a、0x0c、0x2a、0x3c、0x30／0x31、0x41–0x44（§5）。所以 effect 470 在 3.81 一直畫 `offIcon`，FU15§2.9 的推測改成已驗證。

## 0. 來源與方法

- TW16 BIN：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（本輪重算）。區段：`.text` FO＝VA−`0x400C00`、`.rdata` FO＝VA−`0x401400`、`.data` FO＝VA−`0x401800`（本輪從 PE 區段表重讀）。下文寫「VA／FO」。
- TW16 IDB：P01 `ida/03-ascii-isolated/TW1608262200.idb` 複製到本 worktree 的 `scratch/t966/ida/tw16.idb`，SHA-256 `3a0bf206…7e82f`（與 FU5–FU16 相同）。只做查詢，沒有建函式、沒有改名。
- 3.81：FU12 的 OEP 快照 `lito_oep.exe`，SHA-256 `4271d92f…ab6e9`；IDB 用 #896 建好的 `lito.exe.idb`，複製到 `scratch/t966/lito/ida/`。**快照 FO＝VA−`0x400000`**（同 FU12§0.2）；原檔加密，沒有可用的 FO。
- 執行：IDA 7.7 `idat -A`。TW16 用 `rea-scripts/t742/ida_t742.py`＋`rea-scripts/t899/run_tw16.ps1`（subst `V:`），3.81 用 `rea-scripts/t896/ida_t896.py`＋`run_t896.ps1`（subst `Q:`），兩組都沒改。每次執行前檢查磁碟沒被占用（開始時 `subst` 無輸出），在 `finally` 解除。查詢清單在 [`rea-scripts/t966/`](rea-scripts/t966/)（`q1`–`q8.txt`、`lito_l1`–`l4.txt`）。
- `<var>` 對照表：[`rea-scripts/t966/varmap.py`](rea-scripts/t966/varmap.py) 解析 `sub_8676F0` 的反編譯結果，並掃描本機 `Text`／`LTtext` 的 `*-c.html`。輸出在 [`varmap.tsv`](rea-scripts/t966/varmap.tsv)。
- 資源（只讀，不進 git）：PakViewer CLI（`pakviewer-cli.exe`）。
  - `pak search`，本機 `0-客戶端` 全部 24 個 idx（`Sprite`、`Sprite00`–`15`、`Text`、`Tile`、`LiTo\LTicon`／`LTsprite`／`LTsurf`／`LTtext`／`LTtile`），關鍵字 `obscene`、`hometown`、`secretary`、`zone`：
    - `Text.idx` 有 `obscene-c.tbl`（930 B）、`zone-c.tbl`、`zone2-c.tbl`、`zone3-c.tbl`（62,600 B，是地圖顏色／alias 的 tbl，不是 xml）；
    - `Tile.idx` 只有 `CollisionZone.xml`；
    - `LTtext.idx` 有 `LTzone-c.tbl`、`LTzone.tbl`、`zone2-c.tbl`；
    - **沒有 `zone3.xml`、沒有 `zone3desc-c.tbl`**。
    - 結果在 `scratch/t966/pak_search.tsv`。
  - 參考客戶端 5 套（kor25、pmc、c760、t89、ly）共 95 個 idx（各自的 `Tile`、`Text`、`Sprite*`），關鍵字 `zone3`、`obscene`、`WidgetSubSlot`：
    - 5 套都有 `zone3.xml`（kor25 另有 `zone3-c.xml` 251,915 B）；
    - kor25、pmc、ly、c760 有 `zone3desc-c.tbl`；
    - 5 套都有 `WidgetSubSlot.xml`。
    - 結果在 `scratch/t966/pak_search_ref.tsv`。kor25 `zone3-c.xml` 用 `xml decrypt` 解開後才讀。
  - `pak extract-all`：本機 `Text.idx`（37,498 檔）、`LiTo\LTtext.idx`（353 檔），解到 `scratch/t966/text/`。
- 3.81 伺服器：`D:\lineage開發\1-伺服器\src` 只讀 grep。
- 沒有建置或執行任何程式；沒有啟動遊戲、伺服器或 DB。

## 1. Q9-2 書籤窗區域表

### 1.1 表是誰填的（已驗證）

- FU15§2.8 的「窗內 `this+16`」要更正。區域下拉 `sub_F36F20`（VA `0xF36F20`／FO `0xB36320`）只有一個呼叫者 `sub_F2FA40`（FO `0xB2EE40`），內容是 `sub_F36F20(this+16)`。`sub_F2FA40` 的 3 個呼叫者（`sub_6A58C0`、`sub_BD4970`／FO `0x7D3D70`、`sub_BDD830`）傳的都是**全域 `dword_16568A8`**。
- `dword_16568A8` 在 `sub_F2FE10`（VA `0xF2FE10`／FO `0xB2F210`）建立：`new(0x48)` → 建構子 `sub_F2F5B0`，之後 `sub_F2FEB0(off_138E0C4[0])`。`off_138E0C4`（`.data` FO `0xF8C8C4`）指向字串 **`"zone3.xml"`**（VA `0x11E8B6C`／FO `0xDE776C`）。
- 讀檔順序：`sub_F374C0`（FO `0xB368C0`）解析 XML → `sub_F37580`（FO `0xB36980`）走訪子節點：`effecttreelist`、`colorlist`、`alias`、`zone`。每個 `<zone>` 交給 `sub_F385F0`（VA `0xF385F0`／FO `0xB379F0`）建一筆 72 byte 紀錄（建構子 `sub_6A5060`），存進 `this+16` 的 vector。

### 1.2 72 byte 紀錄（已驗證：`sub_F385F0` 的元素名稱與區域變數位置）

| 位移 | 來源（`<zone>` 的子元素） |
|---|---|
| `+0`（字串） | `<name>`；有 `<zone3desc>N</zone3desc>` 時改用 `sub_A6D780(N)` 的字串（推測是 `zone3desc-c.tbl` 第 N 筆） |
| `+24`（word） | `<color id>` |
| `+28` | `<map num>`（地圖編號） |
| `+36`–`+51` | `<map left top right bottom>` 矩形 |
| `+32` | `<nameimage png>`（預設 −1） |
| `+52`／`+56`／`+60` | `<parentmap num>`、`loc` 的兩個值 |
| `+64`（word） | `<background id>` |
| **`+66`（word）** | **`<bookmark id>`** |
| `+68`／`+69` | `<level min max>` |

參考客戶端 kor25 `zone3-c.xml`：
- 1,551 個 `<zone>`，其中 44 個有 `<bookmark>`，id 1–19。
- 例：id 1＝`zone3desc` 0「說話之島」；id 14＝desc 3「甘特之家」。
- `zone3desc-c.tbl` 是 Big5 純文字，第一行是筆數 802。

### 1.3 下拉與篩選（已驗證／推測）

- **已驗證**：
  - `sub_F36F20`：第 0 項是字串 878。
  - 走訪全部區域，`+66≠0` 的用 `sub_F36470` 取 map 的值，再用 `sub_6A5270` 整筆複製進去。所以**同一個 bookmark id 由最後一筆蓋掉**（FU15 的推測改成已驗證）。
  - 依 id 由小到大加入下拉，文字是 map 值的 `+0`（區域名稱）。
- **推測**：篩選 `sub_BDC920`（FO `0x7DBD20`）用 `sub_F2F9D0` 取出選到的群組。之後每筆書籤：
  - 區域沒有矩形 → 比地圖編號（`sub_BD67D0`）；
  - 有矩形 → 再用 `sub_500F80` 判斷書籤座標（`sub_BD67F0`／`sub_BD6810`）在不在矩形內。
  - `sub_F2F9D0`、`sub_579BD0` 沒有反編譯。

### 1.4 3.81 與引擎

- 3.81 快照找不到 `zone3`、`BookmarkMemory`、`ZoneFilter` 字串（§0、§6）。本機客戶端也沒有 `zone3.xml`、`zone3desc-c.tbl`。→ **3.81 不支援區域群組**。
- 引擎 `BookmarkPanel.cs:26`、`:148`（`ZoneItems`＝書籤出現過的地圖編號）可以維持。只要把註解「表來源 `sub_F35D90` 待驗證」改成本節的結論。
- 若要照 TW16：從參考客戶端讀 `zone3(-c).xml`＋`zone3desc-c.tbl`（同 `RefClientXmlSource` 的做法）。這不是 3.81 行為，放在 §7 的 Q9-A，優先度低。

## 2. Q9-3 WidgetSlot 元件 0–5（`sub_7058B0`）

### 2.1 登記（已驗證）

`sub_705910`（VA `0x705910`／FO `0x304D10`，由 `sub_8C7C80` 呼叫）建 `dword_13AAA7C`：建構子 `sub_705180`（FO `0x304580`）用 `sub_7059A0(idx, 視窗)` 插入 `map<char, 視窗>`，然後 `sub_705770(…, 0)`。

| idx | 全域變數 | 在 `sub_B56A40`（FO `0x755E40`）的建立處 | XML 名稱／類別建構子 |
|---|---|---|---|
| 0 | `dword_13AAA54` | `0xB5971A`／FO `0x758B1A` | `MainCharInfoUI`（`sub_6FE180`，0x1A4 byte） |
| 1 | `dword_13AAA68` | `0xB58213`／FO `0x757613` | `MainChatUI`（`sub_7021B0`） |
| 2 | `dword_13AAA94` | `0xB58362`／FO `0x757762` | **`MiniMapUI`**（`sub_707020`） |
| 3 | `dword_14EC328` | `0xB58667`／FO `0x757A67` | `WidgetSlot`（`SlotWidgetUI`，`sub_9F82E0`） |
| 4 | `dword_1657078` | `0xB58552`／FO `0x757952` | **`WidgetSubSlot`**（`SubSlotUI1`，`sub_F6B4A0`） |
| 5 | `dword_13AAA50` | `0xB582BA`／FO `0x7576BA` | `MainButtonUI`（`sub_6FBA30`） |

每處都是 `strcmp(名稱, "…")` → `new` → 建構子 → 寫進全域變數。這 6 個全域變數各只有一個寫入點。

### 2.2 模式（已驗證）

- `sub_705770`（FO `0x304B70`）：
  - 0、1、3、5 一律顯示；
  - 模式 0：2（小地圖）顯示、4（WidgetSubSlot）隱藏；
  - 模式 1：2 隱藏、4 顯示。
  - 顯示／隱藏都經 `sub_705820` → 視窗 vtable +68。
- 切換：`sub_7059E0`（`ModeChange` 514、Alt+R，FU15§2.4）。
- 讀取：`sub_705900`（FO `0x304D00`）＝`*this`。
- 存檔：
  - `sub_997660`（FO `0x596A60`）組選項位元時，把模式放在 **bit 0**；
  - `sub_59E920` 讀設定時，值不同就 `sub_5A3190(模式)` 寫回；
  - `sub_59DD30` 載入時 `sub_705770(…, 值)`。
- **推測**：`sub_9F8EF0`（FO `0x5F82F0`）在模式 0 回傳 (3191, 3192, 8780)，模式 1 回傳 −1。這和 WidgetSubSlot 的 Lock 圖號 3191／3192 相同，推測是模式相關的鎖定鈕圖；沒追呼叫者。

### 2.3 和引擎比

- 引擎已有 `UiSlotGroup` 的 `BAR_485`（`UiSlotGroup.cs:5–13`）：小地圖 `MINI_MAP_WINDOW` → 擴充快捷欄 `EX_MACRO_WINDOW` → 隊伍 RT。這是 TW13／3.81 的 Alt+F3（V5 (e)）。
- TW16 的 WidgetSlot 模式就是其中前兩個的二選一：MiniMapUI＝`MiniMapPanel`，WidgetSubSlot＝`ExMacroPanel`。
- → **Q8-B ②**（§7）：`ModeChange`（`MacroPanel.cs:129`、`ExMacroPanel.cs:92`）改成在 `BAR_485` 切換 `MINI_MAP_WINDOW`／`EX_MACRO_WINDOW`。
  - 目前是隊伍 RT 時要怎麼處理，原版沒有這個狀態（TW16 的隊伍 RT 不在這個群組），推測當成模式 0。
- 3.81 沒有 WidgetSlot（快照沒有字串），所以這個按鈕只有在引擎用參考客戶端 `WidgetSlot.xml` 時才會出現（R7-O）。

## 3. Q9-4 HTML 標記

### 3.1 標記表（已驗證）

`.data` `0x136CE00`–（FO `0xF6B600`–）每筆 12 byte（字串、旗標、id）。字串在 `.rdata` `0x117D0B8`（FO `0xD7BCB8`）起：`scatter`＝0xa、`br`＝0xb、`font`＝0xc、`img`＝0xd、`var`＝0xe、`name`＝0xf、`username`＝0x10、`time`＝0x11、`date`＝0x12、`may`＝0x13、`subHtml`＝0x14（旗標 0x101）、`u`＝0x15。`src`＝0x1c。

解析器 `sub_8676F0`（VA `0x8676F0`／FO `0x466AF0`）的 switch 用 **id−1**（`name`→case 14、`u`→case 20、`src`→27，用三個不同的 case 交叉核對過）。

### 3.2 `<date>`、`<time>`（已驗證）

- `<date>`（case 17）：
  - `tm = sub_A23760(dword_154334C)`；
  - CodePage 1252 時 `strftime("%b %d")`；
  - 否則 `"%2d%s %2d%s"`＝月＋字串 762「月」＋日＋字串 763「日」。
- `<time>`（case 16）：`"%02d:%02d"`（時:分）。
- `<var src="#999">`：`"%2d:%2d"`；`#998`：玩家名稱。
- `sub_A23760`（FO `0x622B60`）：
  - 值 `< 0x80000000` 直接當 `time_t`；
  - 否則減 `2019686400`，年份 +64。
  - 結果交給 `sub_A23F20`（推測是 `gmtime`）。
- `dword_154334C` 的寫入者有 `sub_94DA90` 等，推測是 S_TIME 的遊戲時間（引擎已有 `Ambience.ServerTime`，`Ambience.cs:134`）。
- 本機用到 `<date>` 的 4 頁：`hometown-c`、`hometown1-c`、`othertownh-c`、`secretary1-c`（「今天是 `<date>`」）。
- **3.81**：快照 `sub_435640`（VA `0x435640`／快照 FO `0x35640`）case 17 一樣：`%b %d`，或用字串 762／763 組 `%2d%s %2d%s`，時間來源是全域 `0x8348AC`。→ **3.81 支援**。

### 3.3 `<may>`、`<subHtml>`（已驗證；3.81 不支援）

- `<may>`（case 18）：
  - 第一次記下目前輸出位置（`v315[10]`／`[11]`）；
  - 第二次時，如果「前一個 `<var>` 值是 0」（`v308`；2001–2016 是 `v297==0`），就把輸出退回記下的位置，也就是整段不顯示。
  - 用到的頁：`BsBonus-c`、`monlist-c`、`monlists-c`。
- `<subHtml>`（case 19）：
  - 讀屬性直到標記結束，`src` 有值而且有附屬檢視（`v315[9]`）時，呼叫 `sub_B0D8D0(檢視, 值, 1)`（FO `0x70CCD0`）；
  - 它清掉該檢視，再 `sub_867530(值)` 載入名為 `值` 的 html。
  - 本機 `s4275`–`s4302-c.html`（25 頁）每頁只有 `<p align=left><img src="#N">`。所以 `apaper1-c` 等 45 頁是「本文＋另一格顯示圖 N」。附屬檢視是哪一格沒有追（推測是道具說明窗的插圖區）。
- **3.81**：快照的標記字串（VA `0x5082E4` 一帶）有 `body a b i p left center right justify scatter br font img var name username time date`，**沒有 `may`、`subHtml`、`u`**。
  - 3.81 讀這 45 頁時會略過 `<subHtml>`（推測：未知標記不處理）；
  - 引擎現在「不輸出」（`HyperTextLayout.cs:215`）和 3.81 一致。
  - 但 `NpcTemplates.cs:43` 把含 `subHtml` 的頁當成 dynamic、不給選項，這和 3.81 不同。推測影響很小（這些頁沒有 `<a action>`）。

### 3.4 `<var>` N≥998（已驗證：TW16 的 case／全域變數；標籤取自本機選項頁）

- 顯示字串：567「開」／568「關」；573「可使用」／574「無法使用」；569「3-SET」／570「2-SET」；893–896「1–4 階段」。
- 3.81 欄：「有」＝快照 `sub_435640` 也有同一個 case。
- 完整表在 [`varmap.tsv`](rea-scripts/t966/varmap.tsv)。

| id | TW16 條件（全域變數） | 選項頁動作（標籤） | 3.81 |
|---|---|---|---|
| 998 | 玩家名稱 | — | 有 |
| 999 | 遊戲時間 `%2d:%2d` | 「目前的參考時間為」 | 有 |
| 1000／1001 | 本函式沒有輸出 | `musicon`／`soundon`（gameo） | 待驗證 |
| 1002 | `byte_135C1E4` | `getall` 撿取全部 | 有（`byte_502278`） |
| 1003 | `byte_13AAF28` | `bubbleon` 泡泡話框（FU16） | 有 |
| 1004 | `sub_F400A0(&unk_1656948)` | `dochat` 聊天頻道 | 有 |
| 1005 | `byte_135C1E6` | `dowhisper` 全體密語 | 有 |
| 1006 | `byte_135C1E7`（573／574） | — | 有 |
| 1007 | 本函式沒有輸出 | `chatstate` | 有（3.81 是 0–3 子 switch） |
| 1008 | CodePage 949 才輸出 569／570 | `keyboard` 鍵盤版面 | 有 |
| 1009 | `byte_135C1E5` | `obscene` 語言過濾（§4） | 有 |
| 1010 | `byte_13A930B` | `violence` 暴力過濾 | 有 |
| 1011–1013 | `byte_135C1E9`／`136A5FB`／`135C1EA` | — | 有 |
| 1014 | `byte_137946C` | `tooltip` 遊戲提示 | 有 |
| 1015 | `byte_13A931C` | `telcon` 瞬間移動定位 | 有 |
| 1016 | `byte_13A931D` | `infracon` 夜視 | 有 |
| 1018 | `byte_13CE518` | — | 有 |
| 1019 | `byte_136E99D`（573／574） | `accepttrade` 交易 | 有 |
| 1020 | `sub_F400A0(&unk_1656980)` | `dotradechat` 買賣頻道 | 有 |
| 1021 | 玩家 `+535` 血盟階級 2–10 → 773／1878／771／1879／772 | 「血盟階級：」 | 有 |
| 1022 | `byte_135C1E8`（573／574） | `domail` 郵件接受 | 有 |
| 1023 | `byte_138BE7C` | `showhpmp` 顯示 HP/MP 條 | 有 |
| 1024 | `byte_138BE7D` | `showfloathpmp` | 有 |
| 1025 | `dword_13CEFDC` 0–3 → 893–896 | `alpha` 視窗透明度 | 有 |
| 1026 | `byte_136E99E` | `shortcutscroll` | 有 |
| 1027 | `byte_136E99F` | `showMargetMsg` 個人商店名稱 | 有 |
| 1028 | `byte_13CE519` | `randomParty` | 有 |
| 1029 | `byte_13CE51A` | `cPartyReject` | 有 |
| 1030 | `byte_13CE51B` | `combatMusic` | 無 |
| 1031 | `dword_136EB94==1` | — | 無 |
| 1032 | `dword_154333C`（893／894） | `HPMPBarThicknessSet` | 無 |
| 1033 | `sub_8DC000(dword_13CF590)` | `mapLocationReceive` | 無 |
| 1034–1036 | `sub_6C6C40()` 陣營（1616–1618）／階段／數值 | BsBonus | 無 |
| 1037 | `byte_13A931E`（893／894） | `mouseCursorChange` | 無 |
| 1038 | `byte_135C1EB` | — | 無 |
| 2001–2016 | `byte_1543358`…`byte_1543368` 的數字（`itoa`，2007 是 word） | BsBonus 的 16 個加成 | 無 |
| 2021–2023 | 圖示（`sub_8750D0(N−2021)`） | — | 無 |
| 2026／2027 | `byte_138BE7E`／`byte_138BE7F` | `dropItemView`／`npcNameView` | 無 |
| 2028 | 本函式沒有輸出 | `uitype` 主要 UI | 無 |
| 2030 | 圖 3035／1475／1476 | — | 無 |
| 2037–2044 | `byte_138BFAA`、`byte_13CEFD1`、`dword_1543330+96`、`sub_F71CF0`、`byte_1379424`、`byte_13CEFD2`、玩家 `+800` | `deadTrans`…`broadcasttopledge` | 無 |
| 2045、2047–2064 | 其他（2054–2056 固定 `"-"`） | `mypledgefocus` 等 | 無 |

- 2001–2016 的值寫入者沒有追。推測是伺服器送的加成（3.81 沒有）。
- 1000、1001、1007、1017、2028 在本函式沒有輸出。可能另外處理，沒有追（待驗證）。
- 3.81 的全域變數和 TW16 不同（例：1002＝`byte_502278`、1009＝`byte_502279`）。對應只靠 case 編號，各變數的寫入者沒有逐一追。

## 4. Q9-5 備註 `*`（`sub_51F5D0`）

### 4.1 做法（已驗證）

- `sub_51F850(out, 文字, "*")`（FO `0x11EC50`）→ `sub_51F5D0`（FO `0x11E9D0`）的迴圈：
  1. `sub_51F740`（FO `0x11EB40`）：依序取 **`dword_153C9F0` 表**的每個詞（`sub_4EAFF0(i)`，跳過空字串），在文字裡找第一個位置（`sub_51F900` 比對，逐字前進時處理 DBCS 前導位元組）；找到就把詞複製出來、回傳位置，找不到回傳 −1。
  2. `sub_51F510`（FO `0x11E910`）：組出 **`mbslen(詞)` 個 `*`**，也就是「字數」，中文一個字算一個。
  3. `sub_51F6D0` → `sub_51F950(位置, 長度, 替換字串)`：取代。
  4. 回到 1，直到找不到為止。
- `dword_153C9F0` 是 **`obscene`** 表：`sub_A14550`（FO `0x613950`）`sub_A11F50("obscene")`，然後 `qsort(dword_153C9F4, dword_153C9F0, 4, sub_A11230)`（排序規則沒追，推測長的優先）。同一個函式也載 `todaystip`、`solvent`、`ItemImportant.tbl`、`nidtohtml.tbl` 等。
- 本機 `Text.pak` 的 `obscene-c.tbl`（930 B，Big5 純文字）：第一行是筆數，之後一行一個詞。

### 4.2 誰會呼叫（已驗證）

- **血盟備註** `sub_B34270`（FO `0x733670`）：一律過濾後才送 opcode 140 子 16。`sub_B2F130`、`sub_B2F460`（血盟窗其他文字）、`sub_94AF00`、`sub_94B4F0`、`sub_DE1AD0` 也是一律過濾。
- **聊天顯示** `sub_7F2DB0`（FO `0x3F21B0`）：只有 `byte_135C1E5`（選項 1009「語言過濾」）打開時才過濾。
- 另外兩個用同一張表的函式：
  - `sub_93F430`（FO `0x53E830`）：就地替換，`strnicmp` 不分大小寫，換成 `off_13727D4` 的字；
  - `sub_B28310`（FO `0x727710`）：只檢查有沒有不雅字。
  - 呼叫者沒有逐一追。
- **3.81**：快照有 `obscene` 字串（VA `0x502670`），選項 1009 也在（§3.4），推測一樣會載入並過濾；過濾函式沒有追（待驗證）。

### 4.3 和引擎比

- 引擎沒有不雅字過濾（`engine/source` 搜不到 `obscene`）。
- → 併進 Q8-A ①（§7）：備註送出前照 §4.1 過濾。表從本機 `Text.pak` 讀 `obscene-c.tbl`。
- 聊天顯示的過濾要等引擎有 1009 選項才接，不在本組。

## 5. S64 0x48（已驗證：3.81 伺服器原始碼）

- 寫 `S_OPCODE_CHARRESET` 的檔案只有 8 個：`S_BookmarkList`、`S_EquipmentWindow`、`S_CharReset`、`S_Unknown_B`、`S_PetCtrlMenu`、`S_InitialAbilityGrowth`、`S_CharResetInfo`、`OpcodesServer`。
- 固定子類：0x01、0x02、0x03、0x04、0x0a、0x0c、0x2a、0x3c。
- 變數型 `S_EquipmentWindow` 的呼叫者：
  - `C_LoginToServer.java:1153–1157` 送 `TYPE_3`（67／0x43）、`TYPE_4`（68／0x44）；
  - `L1EquipmentSlot.java:270`／`:280`、`L1PcInventory.java:3359`／`:3379` 送 `TYPE_2`（66／0x42）；
  - `InventorySorter`、`C_LoginToServer.java:1161`／`:1393` 送 `TYPE_1`（65／0x41）；
  - `Npc_Refine.java:43–45`、`C_NPCAction.java:548–552` 送 `TYPE_FIRE_REFINE`／`TYPE_FIRE_CRAFT`（48／49＝0x30／0x31）。
- **沒有 0x48**。所以 TW16 `+0x328`（effect 470 的 `vipFlag`）在 3.81 一直是 0，470 一直畫 `offIcon`。FU15§2.9 的推測改成已驗證，Q8-C ① 不受影響。
- 補充（已驗證）：3.81 客戶端內部封包編號 0x40 的處理函式是 `sub_4AE320`（分派 case 64，`0x4B2C8F`），它不是子類 switch。客戶端內部編號和伺服器編號不同（FU12§0、第 155 行），所以不能拿客戶端 0x40 對伺服器 S64。

## 6. 3.81 快照字串檢查（已驗證）

`lito_oep.exe` 原始位元組搜尋：
- `zone3`、`subHtml`、`WidgetSlot`、`WidgetSubSlot`、`BookmarkMemory`、`ZoneFilter`、`MainChatUI`、`MiniMapUI`、`nidtohtml`：0 筆；
- `obscene`：1 筆（`0x502670`）；`username`：1 筆（`0x508344`，標記表）。

## 7. 能轉成 code 的建議分組

| 組 | 目標 | 可改的檔案 | 依賴 |
|---|---|---|---|
| **Q8-B ②**（解鎖） | `ModeChange` 鈕（主欄、副欄）與 Alt+R：在 `BAR_485` 切換 `MINI_MAP_WINDOW`↔`EX_MACRO_WINDOW`（模式 0／1，§2.2）；模式存進本機設定，下次登入還原；目前是隊伍 RT 時當成模式 0（推測） | `NativeUi/MacroPanel.cs`（只限 `ModeChangeNotWired` 與 Mode 鈕）、`NativeUi/ExMacroPanel.cs`（只限 Mode 鈕）、`NativeUi/UiSlotGroup.cs`（加二選一切換，不改 Alt+F3 循環）、`NativeUi/WidgetSlotTests.cs:148–149`（改成驗證切換）、新檔 `WidgetSlotModeTests.cs` | 無。①（出價步進）照 FU15 |
| **Q8-A ①** 補充 | 血盟備註送出前做不雅字過濾：`obscene-c.tbl` 每個詞 → 字數個 `*`，重複到沒有為止（§4.1）；上限 60、提示 3325／3328 照 FU15 | `NativeUi/PledgePanel.cs`（只限備註送出）、`ClanUi.cs`（只限備註送出）、新檔 `ObsceneFilter.cs`（讀 Text pak 的 `obscene-<語系>.tbl`）、新檔 `ObsceneFilterTests.cs` | 比對大小寫（`sub_51F900`）與 qsort 順序是推測；測試用合成表 |
| **Q9-C** HTML `<date>` | `<date>`＝伺服器時間的「M月 D日」（`%2d%s %2d%s`，762／763）；`<var #999>`＝`%2d:%2d`；`<var #998>`＝玩家名稱；`<time>` 照 `%02d:%02d`（本機 0 頁，可一起做） | `NativeUi/HyperTextLayout.cs`（只限 date／time／var 998–999）、`NpcTemplates.cs`（同）、新檔 `HyperTextDateTests.cs` | 伺服器時間來源 `Ambience.ServerTime`；`sub_A23760` 的 0x80000000 規則照 §3.2 |
| **Q9-D** HTML `<var>` 選項值（3.81 範圍） | 1002–1029 裡引擎已有對應設定的項目照 §3.4 輸出 567／568（或 573／574、893–896）：至少 1019 accepttrade、1023 showhpmp、1005 dowhisper、1020 dotradechat、1003 bubbleon；沒有對應設定的維持「缺少參數」 | `NativeUi/HyperTextLayout.cs`、`NpcTemplates.cs`（只限 var N≥1000）、新檔 `HyperTextOptionVarTests.cs` | 各引擎設定的名稱要對照（例：交易詢問旗標 `World.TradeAskWindow`，`ChatCommandFollowupTests.cs:128`）；1030 以上與 2001 以上是 TW16 專有，不做 |
| Q9-A（低，可省略） | 書籤區域群組：從參考客戶端讀 `zone3(-c).xml`＋`zone3desc-c.tbl`，下拉列 bookmark id 1–19 的名稱，篩選照 §1.3 | `NativeUi/BookmarkPanel.cs`（只限區域下拉）、新檔 `ZoneBookmarkGroups.cs`、新檔 `ZoneBookmarkGroupTests.cs` | 不是 3.81 行為；`sub_F2F9D0` 篩選細節待驗證 |

不改的：
- `<may>`／`<subHtml>`：3.81 沒有這兩個標記，引擎「不輸出」已經一致（§3.3）；
- S64 0x48：3.81 不送（§5）；
- 書籤區域：預設維持地圖編號，只改 `BookmarkPanel.cs` 的註解。

## 8. 限制

- §1.2 `zone3desc` → `sub_A6D780` 的表名、§1.3 篩選、§2.2 `sub_9F8EF0` 的圖號用途、§4.1 qsort 規則與大小寫，都是推測。
- §3.4：
  - 3.81 欄只核對了 case 有沒有存在，沒有逐一核對 3.81 的顯示字串與變數寫入者；
  - TW16 各全域變數的寫入者（選項動作）也沒有逐一追，對應靠選項頁上 `<a action>` 與 `<var>` 的相鄰位置。
- §3.3 附屬檢視（`v315[9]`）是哪個窗沒有追。
- 全部沒有真機驗收。
