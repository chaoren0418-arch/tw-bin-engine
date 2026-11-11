# 查證補遺第 15 輪（FU15）：Q8-1 StatInfo 數值與 Q8-3 子項（T402／#917）

依據：[`gap-matrix.md`](gap-matrix.md) 15.12.6 的 Q8（T379／#880）、15.13.5 的 4:StatInfoUI（K7）。Q8-2（list.spr `altdir`／`stride`）已由 T386／#899（[`followups-14.md`](followups-14.md)）完成，本輪不做。

記號：**已驗證**＝反編譯或反組譯直接看到；**推測**＝依欄位、命名或旁證判斷；**待驗證**＝本輪查不到，寫明卡在哪裡。

**結論**
- **Q8-1**：StatInfoUI 的 22 列數值**不是客戶端算的**。值是伺服器用 protobuf 送來的字串：客戶端送 C157（protobuf 通道）子 id 484 要求，收擴充封包（id 0x79，推測）後填表。3.81 沒有 protobuf（gap-matrix §1 op 19／39／109、§3 C_EXTENDED_PROTOBUF(157)）。`sub_EB85F0` 也不是算數值，是算**顏色**（值和基準不同時換色）。→ 4:StatInfoUI **改 K1**，不開 R11-H。
- **Q8-3**：12 個子項都查到可用的依據（§2）。能轉成 code 的有 6 項，建議分成 3 組（§3）；其餘 6 項不用改，或只要更正文件。

## 0. 來源與方法

- BIN：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（本輪重算）。
- IDB：P01 `ida/03-ascii-isolated/TW1608262200.idb` 複製到本 worktree 的 `scratch/t917/ida/tw16.idb`，SHA-256 `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f`（與 FU5–FU14 相同）。只做查詢，沒有建函式、沒有改名。
- 執行：IDA 7.7 `idat -A`，腳本 `rea-scripts/t742/ida_t742.py`（未改），執行器 `rea-scripts/t899/run_tw16.ps1`（未改，subst `V:`；每次執行前檢查 `V:` 沒被占用，用完 `subst /D`）。查詢清單：`rea-scripts/t917/q1`–`q23.txt`。
- 地址換算（同 FU4§0）：`.text` FO＝VA − `0x400C00`；`.rdata` FO＝VA − `0x401400`；`.data` FO＝VA − `0x401800`。下文寫「VA／FO」。
- 資源（只讀，不進 git）：PakViewer CLI（`pakviewer-cli.exe`）。
  - `pak search <idx> StatInfo`：本機 `0-客戶端` 全部 24 個 idx（`Sprite`、`Sprite00`–`15`、`Text`、`Tile`、`LiTo\LTicon`／`LTsprite`／`LTsurf`／`LTtext`／`LTtile`）都是 0 筆；參考客戶端 kor25 `Tile.idx` 有 `StatInfoUI.xml`（7,371 B）、ly 有 `StatInfoUI-c.xml`／`StatInfoUI.xml`、t89 有 `statinfoui.xml`。結果在 `scratch/t917/statinfo_pak_search.tsv`。這和 T318（PAK§4.2）一致，本輪沒有新的「缺資源」宣稱。
  - `pak extract-all`：本機 `Text.idx`（28,362 檔）、`LiTo\LTtext.idx`（353 檔），kor25、ly、t89 的 `Text.idx`，解到 `scratch/t917/text/<客戶端>/`，給 §2.12 統計用。
- 沒有建置或執行任何程式；沒有啟動遊戲、伺服器或 DB。

## 1. Q8-1 StatInfoUI 的 22 列數值（4:StatInfoUI）

### 1.1 資料表（已驗證）

表在單例 `sub_7A7590`（VA `0x7A7590`／FO `0x3A6990`，回傳 `dword_13AAE0C`，第一次用時由 `sub_7A74C0` 建立）。22 列各 20 byte：

| 位移 | 內容 | 寫入者 |
|---|---|---|
| `+0x84 + 0x14·i` | 基準字串（`char[16]`） | `sub_EBC170`（VA `0xEBC170`／FO `0xABB570`）：先 `+0x94+0x14·i`＝`sub_EB82C0(i)` 的顏色，再 `strcpy` |
| `+0x94 + 0x14·i` | 基準顏色 | 同上；`sub_EBAAE0` 全部設成 `palette[23]` |
| `+0x23C + 0x14·i` | 目前字串（`char[16]`） | `sub_EBC240`（VA `0xEBC240`／FO `0xABB640`）：`strcpy` |
| `+0x24C + 0x14·i` | 顯示顏色 | `sub_EBB050`（VA `0xEBB050`／FO `0xABA450`）＝`sub_EB85F0(i)` |

- `sub_EB85F0`（VA `0xEB85F0`／FO `0xAB79F0`）：`i ≥ 22` → `palette[23]`；目前字串和基準字串相同 → 基準顏色；不同 → `palette[13]`。所以它決定**顏色**，不算數值。
- `sub_EB82C0`（FO `0xAB76C0`）：基準字串空白或和目前相同 → `palette[23]`；否則看 `this+100`（`this[25]`）是否為 1 → `palette[14]`／`palette[23]`。
- `sub_EB7D80`（FO `0xAB7180`）把 22 列目前字串複製成基準。

### 1.2 誰填目前字串（已驗證）

- `sub_EBA270`（VA `0xEBA270`／FO `0xAB9670`）：`sub_F85920(a2,a3)` 解析訊息，之後用一組 `has_…`／`mutable_…` 形式的存取函式（`sub_D68FF0`、`sub_DBAB90` 等）把 5 組子訊息的整數存到 `this+4…+88`，並在 `this+104` 記下有哪幾組（bit 0x1／0x2／0x4／0x8／0x10／0x20）。最後呼叫 `sub_EBADA0`、`sub_EB7CC0`。
- `sub_EBADA0`（FO `0xABA1A0`）：依 `this+104` 各 bit，呼叫物件自己的 vtable `+0`…`+80`（把整數轉字串）寫進第 i 列：

  | bit | 列（XML 順序的 index） | vtable |
  |---|---|---|
  | 0x1 | 0、1、2、3；`this[96]≠31` 時 17 也寫成 +12 的值 | +0、+4、+8、+12 |
  | 0x2 | 9、10、11、12、13 | +16–+32 |
  | 0x4 | 4、5、6、7、8 | +52–+68 |
  | 0x8 | 18、19、20、21 | +40、+36、+44、+48 |
  | 0x10 | 14、15、16；17＝+12；`this[96]≠31` 時 3 也寫成 +12 | +72、+76、+80 |

  index 對 XML 列名（`StatInfoPanel.BonusRows` 的順序）：0–3＝STR 組（DmgBonus、HitBonus、Critical、CarryBonus），4–8＝DEX 組，9–13＝INT 組，14–17＝CON 組（17＝CarryBonus），18–21＝WIS 組。3 與 17 共用同一個值（vtable +12），所以兩列 CarryBonus 是同一個數。這也驗證了 #811 推測的分組（4／5／5／4／4）。
- `sub_EBA270` 只有一個呼叫者：`sub_E44E60`（VA `0xE44E60`／FO `0xA44260`），它沒有直接呼叫者，只在 `0xE41520` 以函式指標登記進一張「id → 處理函式」表（`push 79h` → `sub_E42840` → `push offset sub_E44E60` → `sub_E3E380`；前一格是 `0x78` → `sub_E43FA0`，後一格 `0x1E3` → `sub_E44CB0`）。表的鍵是 protobuf 訊息 id，所以 StatInfo 的值來自 **擴充封包 id 0x79**（鍵和值的配對依程式順序，推測）。
- 要求：`sub_EBB500`（VA `0xEBB500`／FO `0xABA900`）依模式（0／2／1／3／4／5／100）設 `this[96]`，最後 `sub_9FBBE0(484, …)` → `sub_9FBC90(157, 484, msg)`（FO `0x5FB090`）＝**C157 protobuf 通道、子 id 484**（FU7 第 103 行已驗證 `sub_9FBC90` 固定寫 opcode 157）。op 43 的處理函式在關窗後呼叫 `sub_EBB500(1,100)`（#811 報告第 86 行）。

### 1.3 畫面怎麼讀（已驗證）

- `sub_EDFA30`（FO `0xADEE30`）對視窗裡的 22 個 List 子項各呼叫一次 `sub_EE03B0`，順序是 0、1、2、3、9–13、4–8、18–21、17、14、15、16。
- `sub_EE03B0`（FO `0xADF7B0`）：取子項的兩個 `XmlLabel`（列名、數值）和子項 index，先 `sub_EBB050(index)` 算顏色，再用 `sub_7A72D0` 取 20 byte 列資料，`sub_B0BB50(顏色)` 設色、數值 Label 的 vtable +388 設成該字串。
- 另外兩處用同一張表的 22 列繫結：`sub_9D1CB0`（FO `0x5D10B0`）、`sub_EE7280`（FO `0xAE6680`）。`sub_EBA6B0` 依目前 UI 模式（`sub_B556D0(dword_15400E4)` 2／3／4）把資料送給 `dword_1543670`、`dword_14EBF4C`、`dword_15433B4`（StatInfoUI）／`dword_15433B8`。前兩個是哪個窗沒有追（推測是 RenewalStatus 與 ResetStat 的提示）。

### 1.4 判定

- 數值全部來自伺服器的 protobuf 回覆，客戶端只做「轉字串＋比較換色」。3.81 伺服器沒有 C157／protobuf 擴充封包，所以照原版接 3.81 時，這 22 列不會有值。
- 本機 `0-客戶端` 也沒有 `StatInfoUI.xml`（§0），引擎是從參考客戶端讀的。
- → **4:StatInfoUI 改 K1**。`StatInfoPanel` 維持現在的做法（數值空白，不自己編數字，規則 5）。只要更正 `StatInfoPanel.cs:55–58` 的註解（「computed by sub_EB85F0 — not traced」改成本節的結論），可以放進 R11-G。

## 2. Q8-3 子項

### 2.1 ESC 與 Y/N 的吃鍵順序（`sub_8CA740`）

**已驗證**：`sub_8CA740`（VA `0x8CA740`／FO `0x4C9B40`）的順序：
1. ESC 且 `sub_586160()`（`CSceneManager` 單例，FO `0x185560`）`+4` 非 0 → `sub_9DCC70`（FO `0x5DC070`）結束場景，吃掉。
2. `dword_153F748` 的 vtable +40。
3. 視窗堆疊 `dword_153D220[0..3]` 各窗的 vtable +40。
4. **聊天輸入列**：`dword_13AAA68` 存在且 `sub_7037D0`（輸入列在作用中）時，交給 `dword_13CE3F4+172` 的 vtable +32＝`sub_AC7AF0`（FO `0x6C6EF0`）。ESC 時它取輸入框文字，`sub_AC4630(…, 27, 文字)` 呼叫目前的提示回呼。
5. `dword_15400E4` 的 `sub_B557C0`（FO `0x754BC0`）：XML 視窗（StatInfoUI、PledgeUI 等）依序吃鍵，ESC 再走一輪 vtable +36。
6. `dword_153F328`、`dword_153D218`（血盟倉庫紀錄）、`dword_153F2CC`、`sub_8163E0`（聊天列有焦點時再給一次）。
7. 都不吃 → `sub_8C14F0`（全域熱鍵，ESC＝`sub_A57C60(0)` 關全部堆疊窗，FU4§1.1）。

Y/N 回呼 `sub_4FCA30`（FO `0xFBE30`）收到 27 時：照輸入框文字判斷是不是「Y」（`sub_A58380`），然後送 C33 `chdhcs` 回答——空白就是**拒絕**。

**和引擎比**：引擎原生 UI 是「有焦點的面板 → 由上往下的面板（`YesNoPanel` 在 Float 層，排在 Window 層前） → 全域 ESC」（`UiInput.cs:82–89`）。差別只在：有焦點的 XML 窗如果自己吃 ESC，會比 Y/N 先關窗；原版是 Y/N 先（第 4 步在第 5 步之前）。舊式堆疊窗（第 3 步）的 vtable +40 吃不吃 ESC 沒有追。→ 小項，建議 code 分組 Q8-C（§3）。

### 2.2 委任前有沒有 Y/N

**已驗證**：有。`sub_9568C0`（FO `0x555CC0`）取選到的隊員 → `sub_954C90`（FO `0x554090`）：字串 4051 格式化隊員名稱，用 `sub_AC98E0(dword_13CE3F4, …, 回呼 sub_954D90)` 在聊天輸入列問。`sub_954D90`（FO `0x554190`）：Enter 且回答 Y → `sub_9569A0` → `sub_954DC0` → `sub_955D90`（FO `0x555190`）送 TW16 opcode 42 `type 3`（＝3.81 C230 type 3，`Party.cs:15`），然後 `sub_956F60(-1)` 清掉選取。不論回答什麼都 `sub_7DCE00` 收起提示。

**推測**：`sub_9568C0` 是委任鈕的處理函式。它的呼叫者 `sub_957950`（FO `0x556D50`）只以事件指標登記，鈕和函式的對應沒有逐一追。

另外：字串 4051 在本機 `string-c.tbl`（3,484 筆）裡沒有，原版用本機字串表時提示會是空字串（推測）。

**和引擎比**：`PartyPanel.cs:204–213` 直接送，註解寫「No confirmation prompt（推測）」。→ code 分組 Q8-A。

### 2.3 ModifyMemo 的處理函式與 62 byte 上限

**已驗證**：
- `PledgeUI` 建構子 `sub_B2A0C0`（FO `0x7294C0`）登記 `ModifyMemo` → `sub_B2D740`（FO `0x72CB40`）：只在 514（左鍵放開）時 → `sub_B341F0`（FO `0x7335F0`）。
- `sub_B341F0`：字串 3325，用 `sub_AC98E0(dword_13CE3F4, …, 回呼 sub_B34270)` 在**聊天輸入列**問（和 §2.2 同一個機制），不是在血盟窗裡編輯。
- `sub_B34270`（FO `0x733670`）：Enter 且文字不空時——
  - `strlen > 60`（`0x3C`）→ 字串 3328 印在聊天欄（`sub_7DC850`，palette[24]／[14]），不送；
  - 否則 `sub_B28830` ＝ `strncpy_s(buf, 62, 文字, 61)`，再經 `sub_51F850(文字, "*")` 處理後，送 `sub_9FBC00("ccs", 140, 16, 字串)`。TW16 opcode 140 子 16＝3.81 C78 16（`ClanUi.cs:147`）。

所以上限是**輸入 60 byte**；62 是緩衝區大小。

**推測**：`sub_51F850` → `sub_51F5D0` 是「搜尋並替換」的迴圈，作用推測是處理備註裡的 `*`，替換成什麼沒有追。

**和引擎比**：引擎在血盟窗裡直接編輯，上限 `MemoByteLimit`＝62（`PledgePanel.cs` 的 `NotesLength`），超過時自己顯示訊息（`PledgePanel.cs:774–776`）。→ code 分組 Q8-A：改成聊天輸入列提示 3325、上限 60、超過印 3328。`*` 的處理等追完 `sub_51F5D0` 再做。

### 2.4 WidgetSlot `ModeChange`

**已驗證**：
- `SlotWidgetUI`（`sub_9F9AB0`，FO `0x5F8EB0`）與 `SubSlotWidgetUI`（`sub_F6B4A0`／`sub_F6B550`）都把 `ModeChange` 登記到 `sub_9F9C70`（FO `0x5F9070`）：514 時 → `sub_7059E0(dword_13AAA7C)`。
- `sub_7059E0`（FO `0x304DE0`）：模式 `[0]` 在 0／1 之間切換 → `sub_705770`（FO `0x304B70`）。
- `sub_705770`：元件 0、1、3、5 一律顯示；模式 0 → 元件 2 顯示、4 隱藏；模式 1 → 2 隱藏、4 顯示（`sub_705820(idx, 顯示)` → 元件 vtable +68）。
- 同一個切換也綁在熱鍵 **Alt+R**（WM_CHAR `0x8072`，`sub_8C14F0` 的 case 在 `0x8C19A3`／FO `0x4C0DA3`；編輯框路徑 `sub_8C2290`、`sub_8C5380` 也有）。
- 模式會存進設定：`sub_59DD30` 讀設定時呼叫 `sub_705770(dword_13AAA7C, sub_5A3580(…))`。`sub_705910` 建立時預設 0。聊天輸入列（`sub_703200`）也會讀這個模式。

**待驗證**：元件 0–5 各是哪個窗（`sub_7058B0` 從一張 `map<char, 元件>` 取，登記的地方沒有追）。推測 2／4 是主格與擴充格。

**和引擎比**：`MacroPanel.cs:129`、`:278–280` 按了只記 log。→ code 分組 Q8-B（先追元件對應，見 §3）。

### 2.5 A04A20 的單次音效停播

**已驗證**：sample 釋放 `sub_A04A20`（FO `0x603E20`）：
- 循環（`+4` 為 1）的 sample：從槽表移除，播放中就 `AIL_quick_halt`＋`unload`。
- 單次 sample 還在播（`AIL_quick_status==3`）時：
  - 擁有者（`this+8`）自己的 sample 也在播（`sub_A05EA0`）→ 一樣停掉；
  - 否則 → `sub_A066C0` → `sub_A06680`（FO `0x605A80`）：把 handle 交給擁有者的 sample，**繼續播完**。

**和引擎比**：`MapAmbientSound.Release`（`MapAmbientSound.cs:193`）不停單次音效，只是不再追蹤（#814）。和原版主要情況一致。「擁有者正在播時要停」是例外，擁有者的意義是推測。→ 不改。

### 2.6 `0x15442E4` 稱號條件

**已驗證**：`dword_15442E4` **不是稱號文字物件**（AN§2 第 156 行的推測要更正），是「目前指到的物件」的顯示旗標：
- 唯一寫入者是 `sub_A46550`（FO `0x645950`）：設 `dword_15442D8`＝物件、`dword_15442E4`＝旗標。
- 呼叫它的只有 `sub_A46510`（滑鼠指到物件時 `sub_F039B0`；選隊員時 `sub_956F60`），兩處都傳 `0x2000`；以及 `sub_A45DA0`（改稱號時重設，傳目前的值）。

所以第一次指到物件以後，它一直是 `0x2000`。名稱繪製 `sub_EFF300`（FO `0xAFE700`）在 `0xEFF347` 判斷「有稱號（`sub_A41FC0`：`+240` 不空）且 `dword_15442E4≠0`」才畫稱號，實際上等於「有稱號就畫」。

**和引擎比**：引擎有稱號就畫（`ObjectRendering.cs`，#784），一致。→ 不改；更正 `actions-and-names.md` 第 156、309 行。

### 2.7 信件 `MailView.Notice` 的位置

**已驗證**：信件結果的處理函式 `sub_941000`（op 99，FO `0x540400`）把結果訊息印在**聊天欄**：
- 子類 0x20（寄信）：1239、1261＋400、1240；
- 0x30（刪除）：1243／1244；
- 0x40（保管）：1241／1242；
- 0x50：428。

都用 `sub_7DC800`／`sub_7DC850`（palette[24]／[14]）。這和 1:181、1:29 一樣，是聊天欄的系統訊息，不開窗。

**和引擎比**：`MailPanel.cs:36` 寫「MailView.Notice are not drawn」；WinForms `MailUi.cs:146` 放在窗內文字。→ code 分組 Q8-C：原生 UI 下把 `MailView.Notice` 印到聊天欄。訊息編號照引擎現在已解析的結果，不新增字串。

### 2.8 書籤區域表 `sub_F35D90`

**已驗證**：`sub_F35D90`（FO `0xB35190`）**不是載入函式**，是一個空的 `std::map` 建構子（`sub_F35D10` 原樣回傳 → `sub_F359C0` → `sub_F35B10` → `sub_F35AB0` 初始化）。區域下拉 `sub_F36F20`（FO `0xB36320`）的做法：
- 第 0 項是字串 878「全部」。
- 走訪窗內 72 byte 紀錄（`this+16`，紀錄的建構子是 `sub_6A5060`／FO `0x2A4460`，`+66` 是 word）：`+66` 非 0 的，用 `sub_F36470(&群組)` 插入 map，再用 `sub_6A5270` 把該筆紀錄存進去。
- 最後依 map 順序（群組 id 由小到大）加進下拉。

**推測**：同群組後面的紀錄蓋掉前面的；下拉顯示的文字取 map 值（紀錄）`+4` 起的字串。

**待驗證**：`this+16` 的 72 byte 紀錄是誰填的、`+66` 的來源（FU9§1.1 第 4 點推測是地圖表）。所以「區域名稱」還不能接。

**和引擎比**：引擎用地圖編號當區域（`BookmarkPanel.cs:26`、`:148`）。→ 這次不改；留給 Q9（`this+16` 的填表者）。

### 2.9 470 圖示特例（`sub_9001F0`）

**已驗證**：`sub_9001F0`（FO `0x4FF5F0`）對 effect 470 不看 `sub_902840`（時間與模式），直接看玩家 `sub_4F9B90()+0x328`：
- 非 0 → 畫 `icon`；
- 0 → 畫 `offIcon`（`dword_13D4304` 表 `+9` 欄）。

都用 `sub_97C5D0(…, 0, x, y)`，不是 `sub_97AD60(…, 19)`。

`+0x328` 的寫入者有三個：
- op 43 處理函式 `sub_94E0E0` 的 case `0x48`（`0x94EA33`／FO `0x54DE33`，連同 +812、+816 一起讀，值 1–5 時印 502–505、608）；
- `sub_91C600`（切換另一個物件的 +808，不是這個單例）；
- 建構子清 0。

3.81 的 S64（`S_OPCODE_CHARRESET`）在 server-protocol.md 能看到的子類是 1、2、3、4、0x0a、0x0c、0x2a、0x3c 和三個變數型（`S_EquipmentWindow`）；靜態看沒有 0x48（變數型的呼叫者沒有追）。所以接 3.81 時旗標一直是 0，470 一直畫 `offIcon`（推測）。

**和引擎比**：tooltip 已照 `+0x328`（`BuffIconsPanel.cs:253–266`，`vipFlag`），圖示還是走一般的 `offIcon` 規則（`:216`）。→ code 分組 Q8-C：effect 470 改成 `vipFlag≠0 ? icon : offIcon`，不看時間與模式。

### 2.10 `CAgitAuctionUI` 的 agsel 與步進值

**已驗證**：
- 出價窗事件在 `sub_5454A0`（FO `0x1448A0`）登記：`Plus_BidWin`／`Minus_BidWin`（字串 `.rdata` `0x1151968`／`0x1151974`）、`Ok_BidWin` 等。
- **步進值＝1**：
  - `Minus` → `sub_5457C0`（FO `0x144BC0`）：514 時讀輸入框，`≤ 最小值` 設成最小值，否則 −1；
  - `Plus` → `sub_545AC0`（FO `0x144EC0`）：`≥ 最大值` 設成最大值，否則 +1；
  - 513（按下）時記方向（`+428`＝1／2）與 `GetTickCount`，按住會連續（重複速度在更新函式，沒有追）；
  - 兩者都經 `sub_5471F0`（FO `0x1465F0`）夾在 `[最小, 最大]`。
- `Ok` → `sub_545920`（FO `0x144D20`）：
  - 低於最小 → 設最小、字串 4381 印在聊天欄；
  - 高於最大 → 設最大；
  - 否則送 `sub_9FBC00("cddcs", 188, 物件 id, 金額, …)`、關窗。
- 開窗：S_INPUTAMOUNT 處理函式 `sub_93FDF0`（FO `0x53F1F0`）讀 `dddddcsssh`，HTML 名稱是 `agapply` 時 → `sub_546800`（FO `0x145C00`）：取最小／最大／目前值，`PriceTitle_Label`＝`[4027]`、`CurPrice_Label`＝`金額 2813`，窗放在父窗中央往上 10 px。
- `agsel`：`sub_BBC4A0`（FO `0x7BB8A0`）把 `agsel` 和 `moncom`、`crafttable` 等一起登記成 HTML 頁的特殊處理類（`sub_B93F80`）。交給 `CAgitAuctionUI` 資訊窗的部分沒有追（待驗證）。

**和引擎比**：`AgitAuctionPanel.cs:48`、`:855` 寫「加減的步進值沒查」，按了不動。→ code 分組 Q8-B：±1、夾在範圍內、按住連續（速度標推測）；Ok 的 4381 與夾值。

### 2.11 `[this+0x40]` 公告板分支

**已驗證**：`+0x40` 是公告板清單窗的「等伺服器回應」狀態：
- 送出請求時設值：
  - `sub_7CD880` → 1（op 6）；
  - `sub_7CDA70` → 2 或 3（op 6／93）；
  - `sub_7CD970`、`sub_7CDBD0` → 3（op 93）；
  - `sub_7CD490` → 4（op 222／6）。
- 收到回應時清 0：清單 `sub_7CD600`（FO `0x3CCA00`）、讀文 `sub_7CD9F0`（FO `0x3CCDF0`）。
- 非 0 期間：
  - 清單窗滑鼠 `sub_7CD080`（FO `0x3CC480`）全部吃掉、不動作；
  - 662 Write 回呼 `sub_7CDE00` 不開發文窗。

**和引擎比**：`BoardPanel.cs:35` 寫「寫入者沒查（不做）」。→ code 分組 Q8-C（低）：送出清單／讀文請求後到收到回應前，忽略清單點擊與 Write。

### 2.12 R9-F 沒接的 HTML 標記用量

**已驗證**（`rea-scripts/t917/html_tag_census.py`，輸入是 §0 解出的 Text pak；檔數＝含該標記的 html 數，括號是出現次數）：

| 標記 | 本機 `Text`（28,074 html） | 本機 `LTtext`（256） | kor25 | ly | t89 |
|---|---:|---:|---:|---:|---:|
| `<subHtml>` | 45（45） | 0 | 45 | 45 | 90 |
| `<time>` | 0 | 0 | 0 | 0 | 0 |
| `<date>` | 19（19） | 0 | 19 | 19 | 12 |
| `<may>` | 3（22） | 0 | 3 | 3 | 4 |
| `<img src="pN">` | **0** | 0 | 0 | 316（833） | 37 |
| `<var src="#N">` N≥1000 | 32（250） | 4（24） | 37 | 37 | 48 |

- 本機的中文頁：
  - `<subHtml>`：45 頁都是 `-c.html`，是道具說明類，例如 `apaper1-c`、`ev_*-c`、`evnum1`–`14-c`、`lpaper1`–`5-c`、`weirdletter-c`，寫法是 `<subHtml src="s4280">`。
  - `<date>`：4 頁，`hometown-c`、`hometown1-c`、`othertownh-c`、`secretary1-c`。
  - `<may>`：`BsBonus-c`、`monlist-c`、`monlists-c`。
  - `var≥1000`：19 頁，`option11/12/13(a/y)-c`、`optiong-c`、`optionplp-c`、`gameo-c`、`pledge-c`、`pledgeM-c`、`BsBonus-c`、`monlist(s)-c`、`LToptiong(2)-c`；id 1000–1037、2001–2045。
- 結論：
  - `pN` 與 `<time>` 在本機客戶端**用不到**，不必接。
  - `<subHtml>`（45 頁）和 `<date>`（4 頁）有用到，可以接，但語義（`s4280` 的來源、日期格式）還沒查。
  - N≥1000 的客戶端狀態變數用得最多（選項頁），是 R9-F 之後最值得補的一項，但每個 id 對到哪個設定要另查。
  - 都歸 Q9（查證），本輪不開 code。

## 3. 能轉成 code 的建議分組

| 組 | 目標（子項） | 可改的檔案 | 依賴 |
|---|---|---|---|
| Q8-A 聊天列提示（血盟備註、委任） | ① ModifyMemo 改成聊天輸入列提示 3325：Enter 送、上限 60 byte、超過印 3328（§2.3）；② 委任先在聊天輸入列問 4051（帶隊員名稱），Y 才送 C230 3，送完清選取（§2.2） | `NativeUi/PledgePanel.cs`（只限備註編輯）、`ClanUi.cs`（只限上限）、`NativeUi/PartyPanel.cs`（只限 Delegate）、`Party.cs`（只加確認入口）、新檔 `ChatPromptFollowupTests.cs` | 共用 `YesNoPrompt`／`sub_AC98E0` 式的輸入列提示入口（R10-F 已有 1028 的做法）；`*` 的替換等追完 `sub_51F5D0` |
| Q8-B 出價步進與 WidgetSlot 模式 | ① 出價窗 Plus／Minus ±1、夾在 [最小, 最大]、按住連續（速度推測），Ok 低於最小印 4381（§2.10）；② `ModeChange`／Alt+R 切換模式 0／1 並存進設定（§2.4） | `NativeUi/AgitAuctionPanel.cs`（只限出價窗）、`NativeUi/MacroPanel.cs`、`NativeUi/ExMacroPanel.cs`、新檔 `BidStepModeTests.cs` | ② 要先查 `sub_7058B0` 的元件登記（元件 2／4 是哪個窗）；沒查到前只做 ① |
| Q8-C 小項 | ① effect 470 圖示照 `vipFlag` 選 icon／offIcon（§2.9）；② 原生 UI 下 `MailView.Notice` 印到聊天欄（§2.7）；③ 公告板等回應期間忽略點擊與 Write（§2.11）；④ Y/N 提示在作用中時，ESC 先給 Y/N 再給有焦點的 XML 窗（§2.1，低） | `NativeUi/BuffIconsPanel.cs`（只限 470）、`NativeUi/MailPanel.cs`、`MailUi.cs`、`NativeUi/BoardPanels.cs`、`BoardUi.cs`、`NativeUi/UiInput.cs`（只限 ESC 順序）、新檔 `Q8FollowupTests.cs` | 無；④ 可以省略 |

不改的：A04A20（§2.5，已一致）、`0x15442E4`（§2.6，已一致；改文件）、StatInfo 數值（§1，K1；只改註解，放進 R11-G）、區域表（§2.8）、HTML 標記（§2.12，歸 Q9）。

**建議追加的查證 Q9**（併進 15.13.6 的 Q9，不另開）：
- Q9-2：書籤窗 `this+16` 72 byte 紀錄的填表者、`+66` 群組來源。
- Q9-3：`sub_7058B0` 元件 0–5 的登記。
- Q9-4：`<subHtml src="sN">`、`<date>` 的語義，與 `<var>` N≥1000 各 id 對到的設定。
- Q9-5：`sub_51F5D0` 對備註 `*` 的處理。

## 4. 限制

- 只用 TW16 BIN 靜態分析，沒有對 3.81 `LiTo.bin` 快照逐項核對。結論是「TW16 怎麼做」；3.81 有沒有 StatInfoUI 不影響判定，因為 3.81 伺服器不送 protobuf。
- §1.2 的 protobuf id 0x79 是依登記表的程式順序配對，沒有反查 protobuf 描述檔。
- §2.2 委任鈕與 `sub_9568C0` 的對應、§2.8 下拉文字、§2.10 按住連續的速度是推測。
- 3.81 有沒有送 S64 0x48，只看了 server-protocol.md 列出的靜態子類；`S_EquipmentWindow(type, …)` 的呼叫者沒有追。
- 全部沒有真機驗收。
