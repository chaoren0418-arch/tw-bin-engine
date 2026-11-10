# 查證補遺第 9 輪：書籤窗、S55、召喚窗、S161、op 132、取得清單、地圖窗、選目標、送包入口、HyperText、學魔法圖示、數量輸入、背包操作、P-D 窗

T355（#818）。查 [`gap-matrix.md`](gap-matrix.md) 15.8 節的 **Q7-1 到 Q7-14**，影響 K2 的 28 列（另 8 列 Q6 已在 FU8 查完）。簡稱：本文 **FU9**，引用寫 `FU9§n`。

每節分「原版怎麼做／3.81 是否支援／對應要改哪個檔案／去向」。記號同 FU8：
- **已驗證**＝反編譯、反組譯、原始碼或資源檔直接看到；
- **推測**＝依欄位、命名或旁證判斷；
- **待驗證**＝本輪查不到，寫明卡在哪裡。

## 0. 方法與來源

- **TW16 BIN**：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（本輪重算）。
- **TW16 IDB**：P01 `ida/03-ascii-isolated/TW1608262200.idb` 複製到本 worktree `scratch/`，SHA-256 `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f`（與 FU5–FU8 相同）。分成 4 組平行查，每組一份獨立副本（`scratch/rea_a`–`rea_d`），各用一個 subst 代號（X:、Y:、V:、T:）。沒有在副本上建函式或改名（沒用 `mkfunc`）。
- **IDA**：IDA 7.7＋Hex-Rays，唯讀批次。腳本沿用 `rea-scripts/t742/ida_t742.py`。4 組合計 39 輪：A 組 11 輪（q1–q11）、B 組 9 輪（q1–q9）、C 組 11 輪（q1–q3、q5–q12；q3 中途當掉沒有結果，內容拆進 q5–q7 重跑，沒有 q4）、D 組 8 輪（q1–q8）。第一次嘗試在 A q4、B q6、C q3、D q5 之後中斷，第二次接著編號，沒有重跑已有結果。查詢清單放在 `rea-scripts/t818/<組>/q*.txt`（a＝Q7-1／5／7、b＝Q7-2／4／8／10／11、c＝Q7-9／12／13、d＝Q7-3／6／14）。
- **subst**：開始時沒有代號被占用。C 組第一次嘗試中斷時 V: 殘留，已用 `subst V: /D` 解除，並把該組的 IDB 換回乾淨副本；A 組 q6 結束時 X: 殘留一次，已手動解除。其餘各輪結束時都已解除，最後檢查 `subst` 沒有輸出。
- **送出點掃描**：`rea-scripts/wo_sendsites.py`（230、82、42、119、65、108、138、124、56、7、217、86、98、5、247、111、195、120、33、71、137、208、89）。
- **地址換算**（同 FU4§0–FU8§0）：程式碼 FO＝VA − `0x400C00`；`.rdata` FO＝VA − `0x401400`；`.data` FO＝VA − `0x401800`。bss 全域只寫 VA。
- **鍵碼與滑鼠訊息**（FU4§1.1）：鍵盤事件參數是 WM_CHAR 值；方向鍵、Delete 等是 `0x1000|VK`。所以 4142＝Delete、4141＝Insert。滑鼠：512 移動、513／514 左鍵按下／放開、515 左鍵雙擊、516／517 右鍵按下／放開、518 右鍵雙擊、522 滾輪、675 離開。
- **字串表**：本機 `string-c.tbl`（`t680_strings.py`，解碼後 `4ebbc1a7…43e9`，3484 筆）。超出本機表的 4158、4216、4217、4397、3674、3931、4050、4393、4451 取自 kor25 表（4703 筆），或寫明查不到。`desc-c.tbl` 解碼後 `f06c94f47271fcb0`。
- **PakViewer**（`D:\lineage開發\PakViewer-main` 的 CLI：`pak list`／`pak extract`／`xml decrypt`，全部唯讀，輸出只放 `scratch/`）：
  - 用 `rea-scripts/pak/pak_index.ps1 -WithAlt` 重列 `0-客戶端` 全部 **24 個 idx**：`Text`、`Tile`、`Sprite`、`Sprite00`–`Sprite15`、`LiTo\LTtext`、`LTsurf`、`LTsprite`、`LTtile`、`LTicon`。`launcher\TW13081901.pak` 沒有 idx。
  - 參考客戶端：kor25（`D:\381懶人包kor25(原)`）、t89（`D:\天堂參考用客戶單\89天堂`）、ly（`D:\天堂參考用客戶單\雷雨天堂`）的 `Tile`、`Text`、`Sprite*.idx`；pmc（381 泡麵陳）、c760（超人 760）的 `Tile.idx`。
  - 查過的檔名與解出檔的 SHA-256 見 §15。
- **3.81 伺服器**（`D:\lineage開發\1-伺服器\src`，唯讀）：`MapR.java`、`OpcodesServer.java`、`C_Windows.java`、`C_AddBookmark.java`、`C_DeleteBookmark.java`、`S_Bookmarks.java`、`S_BookmarkList.java`、`TELEPORT.java`、`S_SkillSound.java`、`Firework.java`、`S_NpcChat*.java`、`S_SystemMessage.java`、`S_ToGmMessage.java`、`S_HelpMessage.java`、`S_SkillBuy*.java`、`C_CreateParty.java`、`C_BanParty.java`、`C_AddBuddy.java`、`C_DelBuddy.java`、`C_Buddy.java`、`C_Exclude.java`、`C_ShopList.java`、`S_PrivateShop.java`、`C_NPCTalk.java`、`C_Shop.java`、`C_Trade.java`、`C_TradeAddItem.java`、`C_CreateChar.java`、`C_Attr.java`、`S_CharPacks.java`、`C_FishClick.java`、`ClientPacketStructure.java`。
- **引擎**：本分支 `engine/source`（main `4e8423de`），只讀，不建置、不執行。
- 全程靜態分析。沒有啟動遊戲、伺服器或 DB；沒有改產品程式；沒有任何 BIN、IDB、pak 內容進 git。

## 1. Q7-1 書籤窗（`CBookmarkWindowRenewal`，`dword_1540828`）

### 1.1 原版怎麼做

**事件表**（已驗證）：`sub_BDD060`（VA `0xBDD060`／FO `0x7DC460`）登記：

| 事件 | 函式 | 事件 | 函式 |
|---|---|---|---|
| `MemoryPlaceListMouse` | `sub_BDE250` | `Confirm` | `sub_BDBCE0` |
| `QuickList` | `sub_BDEE90` | `Cancel` | `sub_BDAB90` |
| `QuickMoveList` | `sub_BDF0B0` | `CloseWindow` | `sub_BDBBE0` |
| `ZoneFilterComboExpandButton` | `sub_BE1D70` | `MyBookmarkTab`／`CloningBookmarkTab` | `sub_BDED50`／`sub_BDBAF0` |
| `ZoneFilterComboListItem` | `sub_BE1B80` | `Sorting` | `sub_BE0EC0` |
| `ColorPalette` | `sub_BDBC80` | `RandomTeleport` | `sub_BDF110` |
| `AddIcon` | `sub_BDA730` | 鍵盤 `BookmarkKeyBoard` | `sub_BDAA10` |

**兩種模式**（已驗證，**更正 FU3§3.1**）：`this+460` 才是模式旗標（1＝一般／管理，0＝選目的地）；`this+462` 是「窗可以接收輸入」（跳 846／2831 對話時設 0）。一般模式由 `sub_4FE1D0("")`（VA `0x4FE1D0`／FO `0xFD5D0`）設 462＝1、460＝1 後開窗；選目的地由 `sub_E08A20` 設 460＝0。`RandomTeleport_Button` 只在選目的地模式顯示（`sub_BDC800`）。

1. **刪除只用 Delete 鍵**（已驗證）：`sub_BDC1F0` 唯一呼叫者是 846 對話的「是」回呼 `sub_503EE0`（FO `0x1032E0`）；846 由 `sub_4FCD20` 開，它唯一呼叫者 `sub_BDA930`，而 `sub_BDA930` 只由鍵盤 `sub_BDAA10` 的 **4142** 呼叫。原版**沒有右鍵刪除**（`sub_BDE250` 只處理 512–515）。
   - 記憶清單：可多選（Shift 範圍、Ctrl 切換，最多 12 筆；修飾鍵的對應是推測），由大到小每筆送 TW16 op 106 `csd`。
   - 快速移動清單：只移出清單，不送封包。
2. **新增時名稱從哪裡輸入**（已驗證）：窗內**不能**新增。`sub_4FE1D0`（op 84 `cs`）的來源有：
   - 聊天指令 `bookmark`（別名字串 365「記憶」）：`Commandbookmark` `sub_F54680` → `sub_F43C30` → `sub_4FE1D0(參數)`；參數空白＝開一般模式窗。
   - 聊天指令 `qbookmark`：`sub_F55580` → `sub_F48450` → `sub_502720`，先設 `this+486`＝1，收到新書籤時也加進快速移動清單。
   - HTML 連結 `HyperActBookmark`（`sub_581B70` → `sub_4FE420`）：在聊天輸入列提示字串 582「請輸入要標記(bookmark)的位置名稱。」，Enter 後送出。
   - 動作 `Action_BookmarkHere`（提示 4216）：**推測不可達**，本機 `Actions.xml` 沒有這個動作，4216／4217 也超出本機字串表。
   - 本地檢查：限制地圖（2、4、5、6、8–15、18、20、21、83、84、446、447，部分再看區域）顯示 214、不送；名稱 > 30 byte 顯示 204。
   - `AddIcon`／`ColorPalette` 是改選取那筆的名稱前綴（`\d0`–`\dd` 圖示碼、`\f?`／`\a?` 色碼），屬於改名。
3. **排序**（已驗證）：`Sorting` → `sub_BE0E00` 讓 `this+624` 在 0／1 間切換。1＝依名稱 `strcmp`，比較前**先去掉色碼**（`sub_BE0C50` → `sub_BDF3A0`）；0＝照伺服器存的順序表（`sub_BDF210`）。關窗時歸 0。
4. **區域下拉**（878 已驗證，表來源推測）：`sub_BDD830` → `sub_F36F20`（FO `0xB36320`）：第 0 項＝字串 878「全部」、id −1；其餘來自每筆 72 byte 的地圖表（`+66` 區域群組 id 非 0 才列）。篩選時再用 `sub_500F80(x,y)` 判斷區域範圍。表從哪個檔載入（`sub_F35D90`）**待驗證**。
5. **QuickList**（已驗證）：`this+340`，最多 5 筆（第 6 筆顯示 2828）。加入方式：**Insert 鍵**（4141 → `sub_BDDE20`，把記憶清單選取那筆加進去）、從記憶清單拖過去、`qbookmark`。雙擊快速移動清單的列：在選目的地模式＝選它傳送。
   - 保存在 128 byte 的順序表（`this+487`）：第 0 byte＝模式（固定寫 2），之後每筆書籤 1 byte 順序值，再 5 byte 快速移動清單索引。
   - 載入：TW16 op 43 子碼 42（`sub_94E0E0` case 42，`0x94EC98`）。
6. **2847 與存檔**（已驗證）：2847「座標記憶結束」在 ESC、關閉鈕、**確認鈕（存檔但不關窗）**、選目的地雙擊時出現；條件是 dirty（`this+618`）且篩選是「全部」（否則 3106）。存檔送 TW16 op 55 子碼 39（改名的書籤）、34（順序表），另有子碼 40（推測給複製頁）。
   - 改名：一般模式雙擊記憶清單 → 提示 2831 → 只改本地並設 dirty，關窗時才送。
7. **入口**（已驗證）：動作 `Action_OpenBookmarkWin`（`sub_AD8EF0` 在 `0xAD930B` 登記 → `sub_4FBE50`：開著就按關閉鈕，沒開就 `sub_4FE1D0("")`）。本機 `Actions.xml` 有這個動作（icon 5349），是放在動作窗或快捷格用。**沒有固定熱鍵**；按鍵分派沒有直接呼叫 `sub_4FBE50`。另兩個入口：`bookmark` 空參數、op 2 收包。
8. **`sub_931CC0`（op 238）不開窗**（已驗證）：VA `0x931CC0`／FO `0x5310C0`，讀 `c`、`c 筆數`、每筆 `s`（≤13）＋`d`，`d` 交給 `sub_A23760` 時間換算後丟掉。不碰 `dword_1540828`，內容也不是書籤。
   - **更正對應**：3.81 書籤清單 S64（`S_BookmarkList`：`C(0x2a) H(0x80) C(0x02) 127×C(0xff) H H {D S H H H}`）對到的是 **TW16 op 43 子碼 42**，格式逐欄相符。這條路徑也不開窗，只填清單、更新 `BookmarkCount_Label`（`%d/%d`）。
9. **op 2（`sub_931D60`）**（已驗證）：讀 `shhhdc`。最後的 `c`＝0：附加並以一般模式開窗；＝1：插入指定位置、存檔、開窗。3.81 S92 `S_Bookmarks` 是 `S H H H D`，沒有最後的 `c`（讀到 0）。

### 1.2 3.81 是否支援

- 支援：新增 C165 `C_AddBookmark`（`S`，不可標記回 214）；刪除 C3 `C_DeleteBookmark`（讀**名稱**）；S92、S64；隨機傳送（`TELEPORT.java:36–58`：書籤 id 查不到時 `randomLocation(200)`。原版用名稱 487、座標 0、id 0 的假書籤施放）。
- **不支援**：改名、顏色、圖示、排序、快速移動清單的保存（op 55 子碼 34／39／40）。3.81 op 55＝C254 `C_Windows.java`，`switch` 只有 0x00、0x0B、0x2C、0x2E、0x5B、0x5F、0x60–0x67，沒有 default。重新登入後會回到伺服器順序，快速移動清單是空的。

### 1.3 對應要改哪個檔案

`engine/source/NativeUi/BookmarkPanel.cs`、`BookmarkUi.cs`：

| 項目 | 原版 | 引擎 | 要改 |
|---|---|---|---|
| 刪除 | 只有 Delete，可多選 | Delete（`:464`）＋**右鍵**（`:399–402`） | 拿掉右鍵刪除；加多選（3.81 一筆一個 C3） |
| Insert | 加進快速移動清單 | 開始命名新書籤（`:465`、`:275–285`） | 改成快速移動清單（本次登入有效） |
| 新增入口 | `bookmark`／「記憶」、`qbookmark`、HTML 582 提示 | 窗內 Insert＋自訂提示列（`:368–370`） | 移到聊天指令（R9-C） |
| 排序 | 名稱去色碼 ↔ 伺服器順序 | 含色碼的 `Ordinal`（`:246`） | 先去掉 `\f?`／`\a?`／`\d?` |
| 區域下拉 | 878「全部」＋區域表 | 「ALL」＋地圖編號（`:118–119`、`:245`） | 第 0 項改 878；區域表待補查 |
| 確認鈕 | 存檔、不關窗 | 關窗（`:438`） | 不關窗（3.81 沒有東西可存） |
| 雙擊 | 一般＝改名；選目的地＝傳送 | 沒有 | 補選目的地雙擊；改名 K1 |
| 隨機傳送 | 選目的地模式顯示，送 id 0 | 不畫（`:32–34`） | 補；宿主 `RingWindows.cs` 要接受 id 0 |
| 開窗入口 | `Action_OpenBookmarkWin`、`bookmark` 空參數 | 開發工具「記憶座標」分頁（`BookmarkUi.cs:128–133`） | 接聊天指令；動作窗做好後接動作（R8-D） |
| op 238／S64／op 2 | 都照上面 | 一致（`BookmarkPanel.cs:229–231`） | 不用改 |

### 1.4 去向

| 列 | 去向 | 理由 |
|---|---|---|
| 1:2 | **已有** | `c`＝0 分支＝附加並開一般模式窗；3.81 S92 沒有 `c`；`BookmarkPanel.cs:231` 已照做 |
| 1:238 | **已有**（更正對應） | `sub_931CC0` 不開窗、也不是書籤資料；S64 對到 op 43／42，同樣不開窗，引擎一致 |
| 4:BookmarkWindowUI | **K5**（子項 K1） | 上表各項有依據。K1 子項：改名、顏色／圖示、順序與快速移動清單的保存、2847 |

## 2. Q7-2 S55 光源（列 1:86）

### 2.1 原版怎麼做

1. **`sub_8676F0` 不是 S55 的處理**（已驗證）：它（VA `0x8676F0`／FO `0x466AF0`）是 HyperText 的 HTML 解析與排版函式（§10）。EF2§4.2 看到的 `cmp 7EBh`／`7ECh`／`808h`／`809h` 是 `<var src="#N">` 的客戶端狀態變數分支：2026／2027／2037–2052 印 567「開」／568「關」，2028 不印，2053 印 `"test"`，2054–2056 印 `"-"`。和光源無關，EF2§4.2 的「待驗證」結案。
2. **S55 不設光源**（已驗證）：TW16 op 86 的處理是 `sub_934690`（VA `0x934690`／FO `0x533A90`），讀 `dh`。4 個 `sub_8AB390`（Effect 建構子，FO `0x4AA790`）呼叫的第 5 參數（存進 `Effect+9`＝光源旗標）**都是常數 0**。所以 S55 的 2028–2056 煙火也**不會**變亮。只有 S106 `sub_934530` 有 `light = (2028 ≤ gfx ≤ 2056)`（EF2§4.1）。
3. **不跟隨的 S55 在對象消失後留在原地播完**（已驗證）：
   - Effect 建構時只複製座標，不存對象指標；只有跟隨組 6985／7179／8150／8152／8981／7834／7836 把對象 id 寫進 `Effect+0x290`。
   - `+0x290` 的讀取者只有每格函式 `sub_8ABC10`（FO `0x4AB010`）與 `sub_8AE620`，用途都是「找得到對象才接下一段特效」，找不到就不接，**本身不刪除**。
   - 特效結束只看動畫播完（`sub_8ABC10` 第 79–80 行 → `this[664]`＝1 → 刪除）。
4. **跟隨組的跟法**（推測，強）：Effect vtable `0x117EC54`（.rdata FO `0xD7D854`）只覆寫 `+0`、`+24`、`+144`、`+148`，畫圖用基底的 `+16`／`+20` 座標，沒找到每格讀 `+0x290` 改座標的程式。也就是「每段播完時在對象目前位置接下一段」，**不是**每格重畫在對象身上。
5. 附帶（已驗證）：`sub_8AE440`（FO `0x4AD840`）在畫面更新率低時丟棄新特效，除非玩家自己在 2 格內。引擎沒有，屬細節，不另列。

### 2.2 3.81 是否支援

S55 `S_SkillSound`（`C D H [H]`）；煙火 `Firework.java` 用 S55 送 2028–2055、3198。依 §2.1 第 2 點，3.81 下放煙火原版**不會**變亮。

### 2.3 對應要改哪個檔案

- 光源：不用改（`MapEffects.cs` 沒有 S55 光源，與原版一致）。
- 對象消失：`engine/source/SoundPackets.cs:174` `PrunePacketMedia` 在對象不在時刪除；`MapEffects.cs:248–251` `PacketEffectFrames` 在 `PacketEffectPosition(target)` 為 null 時刪除，連已有 `Origin` 的不跟隨特效也一起刪。改成不跟隨的只看 `Expires`／動畫長度、位置用 `Origin`。
- 跟隨組（推測）：段內位置固定，段間跳到對象位置；對象消失時這段在原地播完、不接下一段。
- `MapEffects.cs:220–221` 收包當下對象不在就略過：與原版一致（`sub_934690` 開頭）。

### 2.4 去向

| 列 | 去向 | 理由 |
|---|---|---|
| 1:86 | **K5**（光源子項 K1） | 光源：原版 S55 本來就不設；剩下「不跟隨特效在對象消失後留在原地」有依據，要改 `SoundPackets.cs:174`、`MapEffects.cs:249–251` |

## 3. Q7-3 召喚窗（`CSummonUI`）與公告板 672／676

### 3.1 召喚窗（已驗證，除另註）

- 建構子 `sub_BFFB60`（VA `0xBFFB60`／FO `0x7FEF60`），vtable `0x11AC79C`，實例 `dword_1540858`（`sub_B56A40` 在 `0xB570AE` 建）。滑鼠解析 `sub_C00130`（vtable +144），鍵盤 `sub_C000F0`（+148）。

| 事件 | 處理 | 行為 |
|---|---|---|
| `OkButton` | `sub_C006E0` | 514 → **只關窗**，不送封包 |
| `CancelButton`／`CloseWindow` | `sub_BFFCF0`／`sub_BFFEA0` | 514 → 關窗 |
| `BackButton` | `sub_BFFCC0` → `sub_BFFDA0` | 標題換回 surf 4893，回到等級鈕頁 |
| `SummonLevelButton` | `sub_C00EE0` → `sub_C00710` | 由（列, 欄）查等級（(0,0)=72、(0,1)=68 … (5,1)=28），標題換 4943，填怪物格 |
| `SummonButton`（怪物格） | `sub_C01090` → `sub_C003F0` | 513 開始拖曳；**514 放開時沒拖曳就直接召喚並關窗** |
| `QuickSummonButton`（快速格） | `sub_C00990` → `sub_C00310` | **513 按下＝直接召喚該格**；514 放開時若從怪物格拖來，就放進這一格（或交換） |
| `KeyBoard` | `sub_C01060` | 27（ESC）→ 關窗 |

- **送出**（`sub_C00F50` FO `0x800350`、`sub_C00890` FO `0x7FFC90`）：`(等級 − 28)/4 + 7 < action % 256` → 訊息 743，不送；否則送 TW16 op 216 `cccd`(6, 2, action)，最後一律關窗。送出時**只看等級**。
- **怪物格**（`sub_C00BF0` FO `0x7FFFF0`；記錄 7 個 dword：level、desc、surf、action、id、type、minCha，由 `summonList.xml` 解析器 `sub_BFDC50` 確認）：
  - 格圖＝怪物 surf；徽章 `LevelLabel_Image`＝surf 4944＋type（本機 `summonList.xml` 沒有 `type` → 一律 4944）；`Level_Label`＝level；提示＝`desc-c.tbl` 第 desc 行，字色 0xBDF7。
  - **停用**：等級條件不成立**或**魅力 < minCha → `sub_B0B6F0(1)`。
- **快速 3 格**（`sub_C00A80`、`sub_C01150`）：有內容時同怪物格；空格底圖 4958、提示 desc 13714「你可以標示一個怪物」。3 格存在本機設定檔：寫入函式 `sub_C009F0` 的呼叫者是設定載入 `sub_59DD30`（`0x59E444`）與 `sub_998840`（`0x998F38`）；存檔格式**待驗證**。
- **tooltip 與 `^`**（`sub_B0DAF0` FO `0x70CEF0`）：`^` 是**強制換行**（最多 64 行）；每行寬＝6 px × 位元組數，框寬＝最寬一行 +20 後取 4 的倍數，高＝12 ×（行數＋1）；雙位元組字整字搬；沒有 `^` 時依框寬自動換行。本機 desc 例：13683「哈柏哥布林^(魅力 8/1, 10/2, …)」→ 兩行。
- **DisableOverSurf**：由通用 ListItem 解析 `sub_AED660` 讀入（已驗證）。`SummonUI.xml` 全部格子 `DisableOverSurf="13"`、`OverSurf="5029;4"`（本機都有）。停用時滑鼠移上去畫 13、可用時畫 5029（推測，繪製函式沒展開）。停用格是否還收點擊**待驗證**（`+304` 的讀取者沒追）。
- 魅力 `this[87]` 與重填頁的呼叫者是 `sub_92C820`、`sub_94C950`（已驗證呼叫點；它們是 S39 `summonlist` 與 S36 戒指入口，推測）。

### 3.2 公告板 672／676（已驗證，不影響分類）

閱讀窗 `sub_7CBA70` 建立：
- **672＝上一篇**（(56,312)，`sub_7CDEE0` → `sub_7CD910` → `sub_7CDBD0`）：忙碌時不動；索引 ≤ 0 時關閱讀窗；否則索引 −1，送 TW16 op 93 `cdd`(板 id, 文章 id)。
- **676＝下一篇**（`sub_7CD470` → `sub_7CD850` → `sub_7CDA70`）：下一篇已載入就送 op 93；否則若還有下一頁（`this[7]`≠0，推測是伺服器游標）先送 op 6 `cdd`(板 id, 游標) 載下一頁；都沒有就關閱讀窗。
- 3.81：op 93＝`C_BoardRead`、op 6＝`C_BoardBack`，支援。

### 3.3 對應要改哪個檔案

- `engine/source/NativeUi/SummonPanel.cs:9`：待驗證清單改成已驗證。
- `:173` `Enabled(m)=>Level>=m.Level` → 改成等級公式（`SummonCatalog.RingLevelAllows` 已有，`:41–42`）**且**魅力 ≥ minCha。
- `:246–249`：引擎是「點怪物＝選取，再按 OK 送出」→ 改成點怪物（沒拖曳）立即送出並關窗；OK＝關窗。
- `:200–207`、`:213`：快速格要畫已存的怪物、空格提示 13714、按下即召喚、可從怪物格拖進來（本機設定存 3 格）。
- `:203`：OverSurf 是滑鼠移上去，不是選取；徽章換 4944＋type。
- `:251` `Key`：吃 27 關窗。
- 公告板：`NativeUi/BoardPanels.cs:35–36` 的「meaning unverified → no action」改接上一篇／下一篇；`BoardUi.cs` 要能讀第 i±1 篇、跨頁先載下一頁。

### 3.4 去向

| 列 | 去向 | 理由 |
|---|---|---|
| 1:52 | **K5** | 立即召喚、停用條件、快速 3 格、desc 提示與 `^` 換行、ESC 都已驗證；3.81 不需新封包。DisableOverSurf 時機是推測、停用格是否吃點擊待驗證，照推測做、真機確認（M39） |

## 4. Q7-4 S161 名稱展開（列 1:61）

### 4.1 原版怎麼做（`sub_94AF00`，VA `0x94AF00`／FO `0x54A300`；補 FU6§6.1）

1. **`^`／`!` 只是換成空白**（已驗證）：`sub_941E40`（FO `0x541240`）＝`strchr(Str,'^')` 換空白；`sub_941DE0(33, Str)`（FO `0x5411E0`）＝`strchr(Str,'!')` 換空白。觸發前用 DBCS 安全的 `sub_A0D430` 確認有這兩個字元，但替換用位元組 `strchr`。**只換第一個**。
   - 推測：Big5 第二位元組含 0x5E，`^` 前若有這種字，原版會換錯位元組（原版缺陷）。建議引擎用 DBCS 安全的方式換第一個並註明。
   - 兩條路徑都做：職業外型說話者（文字開頭 `$` 才展開）在展開後做；一般那條在 type 20 `#` 檢查之後做。職業外型而開頭不是 `$` 時直接複製，不換。
2. **開頭的 `\fX` 色碼整段拿掉**（已驗證，FU6 沒寫）：展開後若開頭是 `\f` 且第 3 字元 ≥ `'0'`，從第 4 字元開始顯示。顏色仍由 type 決定（type 2 用 palette[33]，其他 palette[14]）。中間的 `\f` 不處理，交給 Printer。
3. **字串 786 分支**（已驗證）：說話者在物件表、`+196`＝0、種類＝5、`+219`＝1，而且文字含 786「伊娃之祝福」時，整包不做（不印、不畫泡泡）。推測是擋玩家冒充。3.81 原始碼沒有「伊娃之祝福」字串，這個分支實際上不會觸發。
4. **type 2＋id 0**（已驗證）：`sub_A4A230(0)` 回 0，走一般那條：照樣展開、換字元、去色碼，**以 palette[33] 印到聊天欄**；泡泡找不到物件直接 return（`sub_7CE710` 第 44–46 行）。type 0＋id 0 同理（palette[14]）。
5. 其他（已驗證）：`$` 展開的第 4 參數是空字串（`unk_1182E6B`／`unk_1182E6F`），`%i` 拿到空參數時保留還是換成空**待驗證**（影響很小）。`byte_13CE518` 為真而文字含 0x80 以上位元組時不畫泡泡（推測是語系旗標）。

### 4.2 3.81 是否支援

S161 的送出者：`S_NpcChat`（type 0）、`S_NpcChatShouting`（type 2）、`S_NpcChatGlobal`（type 3）、`S_ServerMessage(String)`／`S_BoxMessage`／`S_HelpMessage`／`S_KillMessage`／`S_ToGmMessage`（type 0、id 0）、`S_SystemMessage(String, boolean)`（**type 2、id 0**）。最後這個 overload 在 3.81 原始碼沒有呼叫點，所以 **type 2＋id 0 實際上不會送**。`S_ToGmMessage` 送 `"\\fY"+info`，原版會去掉開頭 `\fY`。

### 4.3 對應要改哪個檔案

- `engine/source/PlayerChat.cs:104`：`ExpandDesc` 之後補「換第一個 `^`、第一個 `!`」與「去掉開頭 `\fX`」。現在兩者都沒做（`Display` 只去控制字元，`:133`）。
- `Inventory.cs:86–88`（type 0＋id 0 系統文字）：原版也做同樣的展開與後處理，引擎沒做。
- `PlayerChat.cs:99` 把 id 0 交給 `Inventory.cs:82–88`，而 `Inventory.cs:85` 只收 type 0，type 2＋id 0 兩邊都不收。3.81 不送，可不改（或順手用喊話色收下）。
- 786 分支：3.81 不送，只在註解記一筆。

### 4.4 去向

| 列 | 去向 | 理由 |
|---|---|---|
| 1:61 | **K5** | `^`／`!` 換空白、開頭 `\fX` 去掉都有依據；786 分支與 type 2＋id 0 在 3.81 不會出現，附註即可 |

## 5. Q7-5 op 132（列 1:132）

### 5.1 原版怎麼做（已驗證）

- 收包 `sub_94BFC0`（VA `0x94BFC0`／FO `0x54B3C0`），唯一呼叫者是主分派 `0x947C5B`。讀 `dc`：D＝背包物品 objid，C＝頁。`sub_990D90`（FO `0x590190`）在背包找到這個物品才繼續：`dword_1656770+12`＝C−1，`sub_A59AB0(dword_1656770)` 推上視窗堆疊。
- **`dword_1656770` 是舊式窗 `MapWindow`**：唯一建立點在 initGame `sub_8C7C80` 的 `0x8C7F47`，`new(0x18)` → `sub_F1A8B0(0x13)`（VA `0xF1A8B0`／FO `0xB19CB0`），vtable `MapWindow::vftable`，視窗編號 19。建構子載入 `0.alp`，另建一個 54,45,200,125 的子區塊，可拖動（`sub_F1B2A0`）。推測是「地圖」類道具的看圖窗；`.alp` 的檔名規則、頁怎麼對到檔案沒追（不影響分類）。
- 和 S187（盟屋地圖 `D D`，對 TW16 op 18）不同。

### 5.2 3.81 是否支援

**不支援**（已驗證）：3.81 沒有 `S_UseMap` 類別。地圖道具 `com/lineage/data/item_etcitem/MapR.java:35–36` 只送 `S_ServerMessage(74, 物品名)`（「%0 無法使用。」），`S_UseMap` 那一行被註解掉。`OpcodesServer.java` 和地圖有關的只有 187（盟屋）與 206（換地圖）。

### 5.3 對應要改哪個檔案

引擎沒有 op 132 的處理，**不用寫程式**。只更正註解：`engine/source/NativeUi/FieldMapPanel.cs:10` 寫「op 132 … maps to 3.81 S187 HouseMap」，應改為「op 132 開舊式 `MapWindow`，3.81 不送（`MapR.java` 註解掉 `S_UseMap`）」。文件：`protocol-gaps.md:26`、`:172` 的「S187（推定）」改「3.81 不送」，`:228` 任務 D 作廢。

### 5.4 去向

| 列 | 去向 | 理由 |
|---|---|---|
| 1:132 | **K1** | 3.81 沒有 `S_UseMap`，地圖道具只回訊息 74，`MapWindow` 不會開 |

## 6. Q7-6 取得清單（`CUserObtainUI`）

### 6.1 原版怎麼做（已驗證）

- 建構子 `sub_C03330`（VA `0xC03330`／FO `0x802730`），vtable `0x11ACA64`。同一建構子建兩個實例：`dword_154085C`（`UserObtainUI`，`0xB573EA`）與 `dword_1540860`（`FishObtainUI`，`0xB5791B`）。
- 登記的事件：`CloseFishWindow`、`FishItemListScroll`、鍵盤 `Keyboard`／`FishKeyboard`（ESC 關窗）；滑鼠解析 `sub_C04300` 只認 `CloseWindow`、`ItemListScroll`，其餘交給基底表。
- **`ItemsListClick` 在這個類別沒有處理函式**：整個 BIN 只有一處引用這個字串（`0x11DE030`），在 `ShopUI` 建構子 `sub_DEDC50`（`0xDEDDBD`）。所以取得清單的列上 CheckButton **沒有任何回呼**。XML 的 CheckButton 都是 `Activate="0"`，推測勾選圖也不會切換。
- 捲軸 `sub_C045B0`：522／514／516／512 → 重排。
- `FishObtainUI` 的清單內容：`sub_E65050`（FO `0xA64450`）走訪背包，物品的某個 16 位元欄位（`sub_C021B0`）在表 `unk_1542864` 裡才列入；自己的背包變動時重填，結束釣魚時清空。表內容與欄位意義**待驗證**；3.81 背包封包不帶模板編號（`docs/protocol/fishing.md:35`），查到也可能無法比對。

### 6.2 資源

- `UserObtainUI.xml`：本機 `Tile.idx` #5443 有（原檔 `9f233a189ccd904e`、解密 `8b2fd2acb300dfcb`，與 kor25 #4561 相同）。
- `FishObtainUI.xml`：**本機 24 個 idx 都沒有**（含大小寫）。參考客戶端 **5 套都有**，內容相同（原檔 `b66b93d28c830603`、解密 `01d94c4464e3d798`）：kor25 `Tile.idx` #4398、pmc #5757、c760 #4705、t89 #4713（小寫 `fishobtainui.xml`）、ly #5697／#5698（`-c` 與無 `-c`）。版面與 `UserObtainUI.xml` 相同，只有事件名與標題字串 3674（本機表沒有）不同。

### 6.3 去向

| 列 | 去向 | 理由 |
|---|---|---|
| 4:UserObtainUI | **已有** | 原生清單窗已在（`NativeUi/UserObtainPanel.cs:16`）；`ItemsListClick` 原版沒有回呼。只需把 `:12` 的註解改成已驗證。`FishObtainUI` 歸 4:FishingUI（§14.3） |

## 7. Q7-7 地圖窗事件表與存點／發送座標窗

### 7.1 MapUI（`sub_8DE770`，`dword_13CF594`）

- **本機 24 個 idx 都沒有 `MapUI.xml`**。參考客戶端有：kor25 `Tile.idx` #4423（13,232 B）、t89 #4758 `mapui.xml`、ly #5757 `MapUI-c.xml`／#5758 `MapUI.xml`。kor25、t89、ly `-c` 三份解密後相同（`71A4900DB6656364`）；ly 無 `-c` 版（`6A02F0509151E980`）多一顆 Quest 鈕。事件名和 `sub_8DE770` 登記的 21＋2 個名稱逐一相符。
- 綁定（已驗證；控制項以 kor25 版為準）：

| 事件 | 控制項 | 行為 |
|---|---|---|
| `TransferMenu` | `MapCommunicationMenu_PopUpWindow` 的 `TransferLocation_Button` (0,0)，8908／8909 | `sub_8E06D0` → `sub_8E0620`：聊天輸入列提示 1785，Enter 送 op 55／11 |
| `SaveMenu` | 同選單 `SaveLocation_Button` (0,20)，8910／8911 | `sub_8E0200`：把 `MapMemory_Window` 移到 (312,199)、焦點給輸入框 |
| `MoveToLocMenu` | 同選單 `MoveToLocation_Button` (0,20)，8912／8913 | `sub_8DFB80` → `sub_8DB930`：用瞬移卷軸傳到存點（op 208），沒有卷軸顯示 4158 |
| `SelectColor`／`SaveLocation` | `MapMemory_Window`（底 8914）的調色盤 (11,43)、`Confirm_Button` (124,104)、`Memo_Edit` 12 字 | 格子 19×17、每列 8 格；存進本地清單，上限 30 筆（1786） |
| `MapName` | `MapName_Button` (2,270)，8891／8892 | 開關地圖清單 `MapList_Window` |
| `QuestToggle` | kor25／t89 **沒有**；ly 版 `Quest_Button` (2,237) png 3497／3498 | 開關任務窗 `dword_13AA8D8` |
| `ShowBookmark` | `ShowBookmark_Button`（CheckButton）(530,294)，png 10748／10749，tooltip 4397 | 地圖上顯示存點 |
| `DeleteLocation` | `DeleteLocation_Button` (100,0)，2972–2974 | 刪掉游標下的存點 |
| 選單叫出 | `Map_Image`（`DragScroll`） | `sub_8DF520`：516 → 在游標處顯示選單；518 在 GM 旗標下送 op 226 |

- 存點是**純本地資料**（已驗證）：`sub_8D40C0` 讀 `<角色名>.loc`（或 `_map.loc`），每筆 44 byte、XOR 校驗，沒有封包。
- **推測：TW16 在本機資源下不會出現 MapUI**：XML 檔名表 `0x137B39C` 有 `BookmarkUI.xml`，但整個 BIN 找不到 `MapUI.xml` 字串，本機也沒有這個檔；它的鈕圖 8908–8918 連參考客戶端都沒有 `.img`。工廠 `sub_B56A40` 的呼叫名單沒追（待驗證，不影響分類）。

### 7.2 本機實際用的 `Field_Map`（舊式 `FieldMapWindow`）（已驗證）

- **`MapCommunicationSelectWindow`**（RTTI `0x137AFA8`，vtable `0x1190EAC`）：initGame `0x8C8028` 用 `sub_A62870`（FO `0x661C70`）建立，存在 `dword_153F2C0`。
  - 版面寫死：鈕 0 surf 2944（按下 2946）在 (0,0)；鈕 1 surf 2947（按下 2949）在 (0, surf 128 的高)。回呼 `sub_A762A0`（0／1）。
  - **叫出**：`Field_Map` 滑鼠處理 `sub_A6F0B0`（vtable `0x1190E88`）：**右鍵按下（516）只記旗標，右鍵放開（517）時**算出游標下的地圖座標（`sub_A66F50`）並顯示選單。選單開著時，左鍵或右鍵按下先收起選單。518 在 GM 旗標下送 op 226，否則關地圖窗。選單開著時另畫 surf 150 在 (x−4, y−4)（推測是點選標記）。
  - 鈕 0＝發送座標：聊天輸入列提示 **1785**「請輸入要接受的角色名稱。」（`sub_AC98E0`）。
  - 鈕 1＝存點：開 `MapLocationSaveWindow`。
- **`MapLocationSaveWindow`**（RTTI `0x137AFD4`，vtable `0x1190EEC`）：`sub_A62C40`（VA `0xA62C40`／FO `0x662040`）建立，存在 `dword_153F2C4`。
  - 底 surf 134，置中在 ((800−w)/2, (530−h)/2)，半透明（`sub_A6AA80`）。
  - 調色盤：起點 (11,43)，每格 19×19、每列 8 格，選取框 surf 138（`sub_A6E710`）。
  - 輸入框 (12,108) 110×16，上限 12 字；確認鈕 2951（按下 2953）在 (124,104)，回呼 `sub_A77080`；Enter＝確認、ESC＝關閉（`sub_A6E6A0`）。
  - `sub_A77080` 讀出輸入後關窗；舊式路徑存到哪裡**待驗證**（MapUI 路徑是本地 `.loc`）。
- **1785 怎麼畫**：`sub_AC98E0` → `sub_AC9600`（聊天輸入列物件 `dword_13CE3F4`）：存提示與回呼，設 `byte_13ABC63`＝1，在聊天列用 `sub_7025D0(字串, 14, 14)` 畫，焦點移到輸入框。**畫在聊天輸入列本身**，和書籤 582、2831 同一個函式。
- 圖本機都有：128、134、138、150、155、2944、2946、2947、2949、2951、2953。

### 7.3 3.81 是否支援

發送座標：C254／11 支援，引擎已有（`LocationShare.cs`）。存點：純客戶端。op 226 是 GM 功能，不做。`MoveToLocMenu` 只在 MapUI 版，本機用不到。

### 7.4 對應要改哪個檔案

| 檔案 | 現況 | 要改 |
|---|---|---|
| `NativeUi/FieldMapPanel.cs:59–61` | 右鍵**按下**直接進 `TransferMenu`（自訂） | 右鍵**放開**時在游標處顯示兩顆鈕的選單（2944／2947）；選單開著時按下先收起；`:67–71` 的「推測」改已驗證 |
| `NativeUi/FieldMapPanel.cs:10` | op 132 註解錯 | 見 §5.3 |
| `NativeUi/LocationListPanel.cs:27–40`、`:96–101` | 清單面板另畫 1785 提示列 | 鈕 0 沿用 `MapSendEntry`；1785 改成畫在聊天輸入列 |
| 新檔（例如 `NativeUi/MapLocationSavePanel.cs`、`MapCommunicationPanel.cs`） | 沒有 | 上述版面；存到本地（每角色、上限 30、1786）。在 `Field_Map` 上怎麼畫存點，開工前先補查舊式路徑，查不到就只做到存進本地清單 |

### 7.5 去向

| 列 | 去向 | 理由 |
|---|---|---|
| 4:MapUI | **K1**（不建議：本機資源與 BIN 都用不到） | 事件綁定已查清；本機沒有 `MapUI.xml`、BIN 檔名表沒有、鈕圖參考客戶端也缺。本機實際用 `Field_Map`（已有） |
| 4:`MapLocationSaveWindow` | **K5** | 版面寫死在 BIN，圖全在本機，純客戶端 |
| 5:（`TransferMenu` | **K5** | 入口已查清：右鍵放開 → 選單鈕 0 → 聊天列 1785；引擎只差選單與提示位置 |

## 8. Q7-8 選目標游標

### 8.1 原版怎麼做

1. **179 是字串編號，不是游標**（已驗證）：`sub_F691C0`（VA `0xF691C0`／FO `0xB685C0`）清舊回呼、存回呼與範圍（15），再 `sub_F690D0(179)` → `sub_7DC800`（FO `0x3DBC00`）把 `string-c.tbl` 第 179 行「`\f1`請選擇對象。」以 palette[24] 印到聊天欄（`sub_A11280(179)` 為真時不印）。同型呼叫：`sub_741A90`（範圍 15）、`sub_ADCB10`（`sub_F69110(…, 30, 179)`，範圍 30）、組隊 `/invite` 不帶名稱（§9.1）。
2. **游標**（待驗證）：換游標的是 `sub_8EECE0(n)`（FO `0x4EE0E0`，`SetCursor(hCursor[n])`）。進入模式的 `sub_F691C0`／`sub_F69110`／`sub_F690D0` 都沒有呼叫它；選完時 `sub_F68E60`（FO `0xB68260`）呼叫 `sub_8EECE0(3)` 還原。進入模式後游標是否改變，要追滑鼠移動路徑（`sub_8EECE0` 有 80 個呼叫點，沒逐一看）。
3. 模式狀態（已驗證）：`dword_1657038` `[0]`＝範圍、`[1]`＝回呼；確認時檢查對象在範圍內才呼叫回呼。附帶：清單窗 `sub_759D40`（FO `0x359140`）在模式中點一列＝選該角色當目標（細節）。

### 8.2 3.81 與檔案

純客戶端。`engine/source/Magic.cs:5–12` 的 `SpellTargeting.Cursor=179` 註解改成「字串 179，進入模式時印到聊天欄」；`PickNotice`（`:12`，引擎自訂文字）改用字串 179；`BeginSpellTargetPick`（`:48`）進入時印這行。游標維持不換。

### 8.3 去向

（細節，無列）：**K5（小）**，併入 R7-A；進入模式後的游標索引仍待驗證（M35）。

## 9. Q7-9 送包入口

### 9.0 共用：聊天指令與動作（已驗證）

指令註冊在 `sub_F56320`（FO `0xB55720`），每個指令類別的 vtable 在 `0x11EB500`–`0x11EB830`，+4 是執行函式；建構子都用 RTTI 核對過類別名。

| 指令（字串號＝中文別名） | 執行函式 | 做什麼 |
|---|---|---|
| `invite`（410「邀請隊員」） | `sub_F55110` → `sub_F474A0` | §9.1 |
| `banish`（411「驅逐隊員」） | `sub_F54620` → `sub_F43AE0` | 以名稱踢人（C65） |
| `outparty`（412「離開隊伍」） | `sub_F55430` | `c` 119 離隊 |
| `chatinvite`（957「邀請聊天」） | `sub_F54910` → `sub_F455A0` | §9.1 |
| `buddy`（947）／`addbuddy`（948）／`delbuddy`（949） | `sub_F54780`／`sub_F544C0`／`sub_F54C20` | §9.2 |
| `exclude`（366「斷絕」）／`include`（470「包含」） | `sub_F54D40`／`sub_F550F0` | §9.2 |
| `trade`（399「交換」） | `sub_F56020` → `sub_F43510` | §9.5 |
| `accepttrade`／`autotrade` | `sub_F54360`／`sub_F54580` | 只切換旗標，不送包 |
| `shop`（907「商店」） | `sub_F55D00` → `sub_F480C0` | §9.4 |
| `bookmark`（365「記憶」）／`qbookmark` | 見 §1.1 | 新增書籤 |

動作窗（`Actions.xml`，`sub_AD8EF0`）的對應：`Action_InviteParty`（`sub_ADA6B0`：隊長而對象已在隊上＝踢人，否則以名稱邀請）、`Action_InviteChatParty`、`Action_AddFriend`（`sub_ADA220` → `sub_B18300`）、`Action_Exclude`、`Action_Trade`、`Action_OpenShop`（執行聊天指令 `shop`）。

### 9.1 組隊

- **TW16 op 82 沒有送出點**（`wo_sendsites.py 82` 0 筆）。實際用 **op 42**：type 0／1 `ccd`（物件 id，`sub_955CD0` FO `0x5550D0`）、type 2 `ccs`（聊天隊伍）、type 4／5 `ccs`（以名稱，`sub_955D20`、`sub_EDC4A0`）。1／5 是 `byte_13CE519` 為真時。
- 入口（已驗證）：
  - `/invite 名稱` → `sub_956C20` → `sub_954EC0` → type 4／5。
  - **`/invite` 不帶名稱** → `sub_9550C0` → 進入選目標模式（字串 179，範圍 30），點到人後 `sub_954FC0`：對象是有效玩家、且 `sub_A42C30`（推測＝隱身）不成立，才送 type 0／1。
  - **拖人到隊伍窗**：`sub_ED8AC0`（FO `0xAD7EC0`）收到 517、拖的是別人的 `userObject` → type 0／1（窗的類別是推測）。
  - 動作窗、社群窗 `SendPartyRequest`（以名稱）、`/chatinvite 名稱` → type 2（不帶名稱時開 960 輸入框）。
- 3.81：`C_CreateParty.java`（C230）只有 type 0／1（`D` 目標）、2（`S` 名稱）、3（轉移隊長），**沒有 type 4／5**。踢人 `C_BanParty.java`（C255 `S`）。
- 引擎：`Party.cs:10` 只有 type 0／1；`PartyUi.cs:24` 只在開發工具；`PlayerChat.cs:342–370` 沒有 `/invite`、`/chatinvite`、`/banish`、`/outparty`。

### 9.2 好友／封鎖

- 聊天指令（已驗證）：`/buddy` → `c` 56；`/addbuddy 名稱`（`sub_F43160`）名稱檢查失敗印 53，成功送 `cs` 138＋`c` 56；`/delbuddy 名稱` → `cs` 124＋56；**`/exclude` 固定傳空字串**（不讀參數）→ `sub_987750("",1)`：有社群窗就切到封鎖頁，否則送 TW16 專用擴充包；`/include 名稱` → `cs` 217。
- HyperAct：`buddy`／`refreshbuddy` → 56；`addbuddy` → 輸入框 588；`delbuddy` → 589。舊式封鎖輸入框 584（推測是 HyperAct `exclude`）→ `sub_F46820`：名稱 > 12 字印 435，否則送 `cs` 7；空名稱開 `ExcludeUI`。
- XML 窗事件（已驗證）：

| 窗 | 事件 | 函式 | 行為 |
|---|---|---|---|
| `FriendUI` | `FriendName` 4142（Delete） | `sub_B18F10` | Y/N 1813 → 是 → `cs` 124，本地移除（不送 56） |
| 同上 | `FriendName` 515 | `sub_B19030` | `"名稱 `（密語前綴）放進聊天輸入列 |
| 同上 | 27、`CloseFriendWindow` | `sub_B191F0`、`sub_B18880` | 關窗 |
| `ExcludeUI` | `ExcludeName` 4142 | `sub_B18D30` | **直接**送 `cs` 7，不確認 |
| 舊式 `ExcludeWindow`（vtable `0x117B6A0`） | 4142 | `sub_830E50` | 每個選取的列送 `cs` 7 |
| `MailExcludeUI` | 4142 | `sub_B19520` | 只在旗標 0x2000 時送 `csc` 7 名稱 1 |
| 社群窗 | `FriendDrop`／`ExcludeDrop`／`AddMember` | `sub_986F10`／`sub_986E80`／`sub_986230` | 拖人進窗加好友／封鎖；開 588 輸入框 |

- 好友窗由哪個鍵或鈕開**待驗證**（只找到空名稱封鎖開 `ExcludeUI` 這一處）。
- 3.81：`C_AddBuddy`（C207）、`C_DelBuddy`（C202）、`C_Buddy`（C4）一一對應；`C_Exclude`（C171）只讀 `S`，空名稱直接 return、已在名單就移除（**切換**）；沒有 217；`csc` 的第 3 byte 不讀，信件封鎖不支援。
- 引擎：`Buddies.cs:12–17` 封包已對；入口只有開發工具（`BuddyUi.cs:96–107`）；`PlayerChat.cs` 沒有這些指令；`NativeUi/FriendPanel.cs:13`、`:236–245` 只做選取，`Key()` 回 false。

### 9.3 私商查詢（已驗證）

- 點店主：點擊處理 `sub_5001C0` 的 `LABEL_46`，物件是個人商店時設對話目標＝店主 id，以 NPC 對話窗開**本機 HTML `usershop`**（字串 VA `0x114F31C`），**不送封包**。
- 頁面：本機 `Text.idx` #36115 `usershop-c.html`（248 B，`df84333d81b970c5`）：`<var src="#0">的個人商店`、「歡迎光臨～」、兩顆圖片鈕 `<img src="#602" action="userShop0">`、`<img src="#603" action="userShop1">`。
- 按鈕：HyperAct `userShop`（`sub_BA7610`，vtable `0x11A9914`）執行 `sub_585270`（FO `0x184670`）：取動作字串第 8 字元（0／1），送 `ccd` 86, type, 店主 id。
- 3.81：`C_ShopList.java`（C47 `C D`）回 `S_PrivateShop`（0＝店主賣出、1＝收購），欄位一致；`C_NPCTalk.java` 只處理 NPC，所以這頁一定要客戶端自己開。
- 引擎：右鍵店主 → `RunInteraction("talk")`（`PlayerPointerUi.cs:22`）→ `PrivateShopUi.cs:30` 切到 WinForms 分頁。封包 `PrivateShop.cs:13` 已對。差在入口：改成原生 HyperText 開 `usershop-c.html`，攔 `userShop0／1` 送 C47。

### 9.4 開個人商店（已驗證）

- 入口：`/shop`、`/商店`（`sub_F480C0`）或動作 `Action_OpenShop`。擺攤中（推測 `& 0x400`）時：釣魚中印 2120，否則送 `cc` 98, 1（關店）並印 903；不在擺攤就切換 `PersonalShopUI` 窗。沒有開店鈕或右鍵選單。
- 窗事件（建構子 `sub_DDC740`，開機讀 `PShopPoly.tbl`）：`ClosePersonalShop` 關窗；`Pick` 515 雙擊＝從清單移除、513＝開始拖曳；`SelectPoly` 選外型；`PageControl` 翻頁；`OK`（已開店＝只關窗，否則送出）；`Cancel`（已開店＝關店）。
- **放物品＝從背包拖進窗**（`sub_98F330`）：出售／收購清單分開。檢查 `sub_DDED00`：1181、1188、941、1497、937（多種）、932、940。
- **輸入都在聊天輸入列**：先問單價 898，解析失敗 938、≥ 10,000,000 印 900、收購金幣不足 936；堆疊再問數量 933（`%s`＝934「販售」／935「購買」）。
- 送出 `sub_DE1AD0`（FO `0x9E0ED0`）：先檢查 3425、3405、876（地圖 303／70）、334、908、3434、927、904、4393；然後送
  `98, 0, H(出售數), {D 物件, D 單價, D 數量}×n, H(收購數), {…}×m, 訊息1, [0xFF], 訊息2, [0x00], 外型名稱\0`
  （每行 ≤ 40 byte，先過濾）。自己的動作變成 70 時印 902。
- 3.81：`C_Shop.java`（C38）type 0 讀出售／收購 `(D D D)`，剩下 byte 整段當擺攤文字；type 1 關店；**不讀外型**。
- 引擎：`PersonalShopIntegration.cs:7–12`、`:159–170` 是 WinForms 過渡頁（先在背包選取、NumericUpDown 填價格，沒有拖放）；`PrivateShop.cs:116–119` 兩行訊息直接相接（原版 0xFF 分隔、0x00 結尾）；`PlayerChat.cs` 沒有 `/shop`；`NativeUi/PersonalShopPanel.cs` 沒有雙擊移除。

### 9.5 交易邀請（已驗證）

- `/trade`、`/交換`、`Action_Trade`、HyperAct `trade`（`sub_5851D0`）都進 `sub_F43510`（FO `0xB42910`）：面前一格是個人商店玩家時印 989，否則送 **`c` 5**（無欄位）。只有英文地區碼（`byte_13CE3FE`＝1）才用選目標並送 `cd` 5 id；台版是 3，面對面。
- 把物品拖到玩家身上（`sub_98DB60`）在台版是**給予**（op 137，堆疊先問 140），不是交易。
- 3.81：`C_Trade.java`（C2）用 `FaceToFace` 找對象，一致。
- 引擎：`Trade.cs:4` 封包已對、`:29–33` 有面對面檢查；入口只有 WinForms 鈕（`TradeUi.cs:9`、`:23`）；沒有 `/trade` 與 989。

### 9.6 去向

| 列 | 去向 | 理由 |
|---|---|---|
| 3:C_INVITE_PARTY(82) | **K5** | 入口已查清（`/invite`、選目標、拖人到隊伍窗、動作窗）；3.81 C230 0／1 支援；以名稱邀請 3.81 不支援，不送 |
| 3:C_ADD_BUDDY(138) | **K5** | `/addbuddy`、`/delbuddy`、`/buddy` 與 HyperAct 輸入框已驗證，3.81 欄位一致 |
| 3:C_QUERY_PERSONAL_SHOP(86) | **K5** | 點店主 → 本機 `usershop-c.html` → `userShop0／1` → C47，只差入口 |
| 4:FriendUI | **K5** | Delete → 1813 → C202；雙擊 → 密語前綴；Esc。開窗鍵待驗證（M37），先沿用現有入口 |
| 4:`ExcludeWindow` | **K5** | Delete → C171（3.81 是切換）；空名稱查清單、C217、信件封鎖 3.81 不支援 |
| 4:PersonalShopUI | **K5** | `/shop`、拖放、898／933、雙擊移除、OK 檢查、C38、0xFF 分隔都已驗證；外型 3.81 不讀 |

## 10. Q7-10 HyperText

### 10.1 原版怎麼做

- **視窗**（已驗證）：S39（TW16 op 144）`sub_940030` → `sub_BBCB00`（VA `0xBBCB00`／FO `0x7BBF00`）讀頁名；空字串關窗；否則載入 `"%s-%c.html"`（`sub_867530`）、`sub_8676F0` 解析、`sub_8763E0` 開窗。窗物件 `dword_13CD830`＝`CHyperTextUI`（建構子 `sub_875810`，vtable `0x117D4DC`），XML 名稱 `HyperTextUI`。子元件 `HyperTextUI`、`Icon`、`Scrollbar`、`OKBtn`、`CancelBtn`。
- **TW16 沒有 `HyperTextWindow` 類別**（`nameea` 回 BADADDR）；那是 TW13 的類別。
- **ESC**（已驗證）：`sub_875F80`（FO `0x475380`）：27 → 關窗。
- **OK／Cancel**（已驗證）：`sub_875B20` 每次畫圖前把兩鈕的可按狀態設成「頁內有數字框」（`dword_13CD834+396`）。OK → `sub_876010`（FO `0x475410`）：數值在範圍內時依頁的種類送 op 41／59／36／253／169／91／188，然後關窗。
- **數字框**（已驗證）：`CNumberInputFrame`（建構子 `sub_862250`，vtable `0x117D28C`），由 `<a … NumberInput="N">` 建立：下限 1、上限 N、初值 1。
  - 滑鼠（`sub_872DC0`）：上下鈕是 **surf 5073**（本機 `Sprite10.idx`、`LTsurf.idx` 都有），放開時 +1／−1，**超過上限回下限、低於下限回上限（循環）**。
  - 鍵盤（`sub_872BE0`）：0–9 逐位輸入並夾在 [1, N]；Backspace 去掉最後一位；Enter 結束；4203／4205 等同上下鈕（推測是方向鍵）。
- **HTML 標籤**（已驗證，`sub_8676F0`）：標籤表 `unk_136CDE0`（.data FO `0xF6B5E0`，21 個）`body a b i p left center right justify scatter br font img var name username time date may subHtml u`；屬性表 `unk_136CF00`（FO `0xF6B700`，26 個）。
  - **顏色**：`<font fg bg face Size>`；`fg`／`bg` 是十六進位數字（不收 `0x`／`#`）。位元組順序（RGB 或 BGR）**待驗證**。
  - **連結色**：每個 `<a>` 開始時文字色設常數 `0x00B0FFFF`（實際顏色依位元組順序，待驗證）。
  - **行內連結**：`<a link | action | url>`、`accesskey`，排在文字流裡，**不是**一列一個。
  - **圖片**：`<img src="#N"|"pN" align valign link|action|url tooltip highlight>`；`#N`＝SurfList（312→1109、314→1107、331→601 換號），`pN`＝PngList；有 link／action／url 的圖可點。
  - **`<var src="#N" textwidth img>`**：N < 900＝S39 帶來的第 N 個資料字串，先 `$` 展開、再換第一個 `^`；N ≥ 1000＝客戶端狀態變數（§2.1）。
  - 另有 `<b>`、`<i>`、`<u>`、`<p align lgap agap>`、`<br>`、對齊、`<subHtml>`。
- **不在 `sub_A57C60` 的堆疊裡**（已驗證）：4 格堆疊是 `dword_153D220[0..3]`；`dword_13CD830` 的 52 個引用沒有一個交給 `sub_A57C60`／`sub_A59AB0`。開關走 XML 窗自己的 vtable +68。開窗時另關掉 `dword_1541D38` 那個窗（是哪個窗待驗證）。`sub_A58980` 在個人商店相關情況下關掉頁名含 `usershop` 的 HyperText。
- **版面**：`HyperTextUI.xml` 本機 24 個 idx **都沒有**；參考客戶端只有 ly `Tile.idx` 有（1,320 B，原檔 SHA-256 `00b3effd4570361c`）：關閉鈕 9183 (215,9)、捲軸 (217,38)、OK 1114／1115 (47,292)、Cancel 1111／1112 (133,292)（兩鈕預設 `Activate="0"`）。9183 只在 kor25／ly，11527 只在 ly。引擎現在讀 uiextra86 第 3 段（關閉 1962 (225,15)），差在關閉鈕、捲軸起點與 OK／Cancel。

### 10.2 3.81 與檔案

- S39 送頁名＋資料字串，頁面 HTML 在本機 `Text.pak`。數字框回應的 3.81 封包引擎已有一部分（`HyperTextNumberBox`，WIN2§5.3）。
- `engine/source/NativeUi/HyperTextPanel.cs:19`：ESC、顏色／圖片標籤、數字框三項改為已驗證。
- `:348–361` `Key`：沒有焦點時 ESC 不處理 → 補 Esc 關窗（名稱框有焦點時維持 `CancelName`）。
- `:364–373`、`:16–17`、`:79`：數字框改成 [1, N] 逐位夾值、Backspace、5073 上下鈕循環；OK／Cancel 只在有數字框時可按（1114／1111 本機有，座標依 ly XML 是推測）。
- `:16`（連結各占一列）與 `NpcInteraction.cs` 的選項拆分：改成行內排版器是大工程，另開一組。
- `:9–10` 版面：維持 uiextra86，只在註解記下 XML 差異（不得複製資源進專案）。

### 10.3 去向

| 列 | 去向 | 理由 |
|---|---|---|
| 1:144 | **K5** | ESC、數字框、OK／Cancel、行內連結、`<font>`、`<img>`、`<var>` 都有依據；拆成小組與排版器大組 |
| 4:HyperTextUI | **K5** | 同上；XML 本機缺，維持 uiextra86 並註明差異 |

## 11. Q7-11 學魔法圖示

### 11.1 原版怎麼做（已驗證）

- **`word_136D548` 是物品圖示編號表，不是 surf**：S13（op 175，`sub_93F200` FO `0x53E600`）與 S41（op 44，`sub_931FF0` FO `0x5313F0`）都呼叫 `sub_A27B70(索引, word_136D548[索引], 0, 名稱)` → `TradeItem` 建構子 `sub_A26C50` → `Item` 建構子 `sub_88BAC0`，存在 `Item+290`。
- 列圖示由 `Item` vtable +8＝**`sub_891DF0`**（FO `0x4911F0`）畫：讀 `Item+290`，再 `sub_88CE60` 取圖畫 32×32，**和背包、商店的物品圖示同一條路**（引擎 `ItemIconSource`）。
- 表在 `.data` VA `0x136D548`／FO `0xF6BD48`，索引＝技能編號 − 1。前段：`0:383 1:384 2:385 3:386 4:387 5:657 6:663 7:775 8:388 … 23:0 … 79:5737 80–85:0 86:1624 … 96:1097 … 111:4471 …`（完整 240 個 word 見 `rea-scripts/t818/b/q3.txt` 的 `dwords 136D548 120`，結果可從 IDA 輸出重算）。
- 資源：圖示是 `<編號>.tbt`。240 個中非 0 的 192 個，本機缺 5 個：5737、5734、5731、6458、6501（kor25／ly／t89 都有）。缺的照引擎規則不畫。

### 11.2 3.81 與檔案

- S41 `S_SkillBuy`（`C D H n×D`）、S13 `S_SkillBuyItem`（`H n×D`），伺服器只送索引，圖示由客戶端表決定。支援。
- `engine/source/SpellTradeSource.cs:50`（op 44）與 `:60`（op 175）：`TradeListItem` 的圖示從 0 改成 `SpellIcons[index]`（新增 240 個 word 的常數表，標 VA／FO）。
- `SpellExchangeUi.cs:8`、`:18–19`：拿掉「模式 7 不畫圖示」的包裝；`SpellTradeSource.cs:15` 的註解更正。`TradeWindowPanel.cs` 不用改。

### 11.3 去向

| 列 | 去向 | 理由 |
|---|---|---|
| 1:44 | **K5** | 圖示來源已確定，改 `SpellTradeSource.cs:50` 與 `SpellExchangeUi.cs:18–19` |
| 1:175 | **K5** | 同上，改 `SpellTradeSource.cs:60` |
| 4:`TradeWindow` | **K5**（只限模式 7 圖示） | 其餘模式不變 |

## 12. Q7-12 數量輸入

### 12.1 原版怎麼做

- **交易**（`ExchangeUI`，已驗證）：從背包拖進交易窗 → `sub_82DF30`（FO `0x42D330`）：1181／1188、不可交易 941；**堆疊在聊天輸入列先問 140**「要給幾個？ (1-%u， Enter=全部，Esc=取消)」（`sub_82DB50`，回呼 `sub_82E560`：空白＝全部，須 ≤ 持有量）；單件直接送。送出 `sub_82E870`：`cdd` 247, 物件, 數量；已放 16 件以上印 255。**金幣也是背包裡的一疊，走同一個 140 提示，沒有獨立的金幣輸入框**。版面 `ExchangeUI.xml` 本機沒有，kor25 #4392 有（解密 `cdf53b56faade616`：各 3×4 的 33 px 格＋OK／Cancel）。
- **`ADENA_EXCHANGE_WINDOW`**（已驗證）：設定讀取 `sub_A842D0`（FO `0x6836D0`）把它存成第 8 段、`TRANSACTION_WINDOW` 是第 6 段；49 個 `sub_7D7650(n)` 呼叫者用到的段號沒有 6 和 8，**TW16 不讀這兩段**。本機 `uiextra86-c.xml` 第 113 行的內容是底 4388、框 4389、兩條捲軸、OK 1996、Cancel 1994——「兩張清單＋確定／取消」，**不是金幣輸入框**。#715 報告的「金幣交易窗」推測沒有依據。3.81 誰開它要看 `LiTo.bin`（有殼），待驗證。
- **倉庫**：TW16 `RenewalWareHouseUI`（`sub_9CB850`）走 TW16 專用 `ccdb` 140, 10；`rangegrep 9BF000–9C2000` 這段沒有聊天列數量提示；kor25 `ReWareHouse.xml`（`f7c9d6c60a73dfea`）每列有 `TradeCountLabel`，推測數量在列內調。本機字串表的數量提示只有 119、121、140、933、3054，沒有存倉專用的。**3.81 的倉庫數量輸入待驗證**（`WAREHOUSE_WINDOW` 由 `LiTo.bin` 消費）。

### 12.2 3.81 與檔案

- `C_TradeAddItem.java`（C37 `D D`）與 TW16 C247 一致；字串 140 本機有。
- `NativeUi/TransactionPanel.cs:24–27`、`AddSelected`（`:293–296`）一律加整疊 → 堆疊時先在聊天輸入列問 140，Enter 空白＝全部（做法照 `ItemTransfer.cs:101–129`）。
- 金幣框（`:26–27`、`SubmitAdena` `:298–302`）是引擎自加的：建議改走 140；保留的話標「引擎自訂」。
- 倉庫 `NativeUi/WarehouseWindowPanel.cs:153–168` 一律整疊，維持，等真機（M36）。

### 12.3 去向

| 列 | 去向 | 理由 |
|---|---|---|
| 1:127 | **仍 K2** | TW16 倉庫是 C140 10＋列內數量，和 3.81 不同；3.81 怎麼輸入數量只能看真機或脫殼的 `LiTo.bin`（M36） |
| 4:ExchangeUI | **K5** | 拖進交易窗時堆疊先問 140 → C37；`ADENA_EXCHANGE_WINDOW` 不是金幣框 |

## 13. Q7-13 背包操作（Inventory 的附帶項）

TW16 `RenewalInventoryUI`（建構子 `sub_9A40D0`，FO `0x5A34D0`；版面 `ReInventory.xml` 本機沒有，kor25 #4458 有，解密 `52ca2676edd38d24`）：

1. **雙擊使用**：`sub_9A6FB0`（FO `0x5A63B0`）擋幽靈等狀態，`sub_9930B0` 依使用類型分派、送 C208（已驗證）。觸發事件是雙擊屬推測（同 FU8§4.3 的 `GridEvent`）。
2. **垃圾桶與 Delete 鍵**（已驗證）：kor25 XML `InvDelButton`（152,258），surf 2052／2053，事件 `DeleteItem` → `sub_9A54D0`（514 且有選取）→ `sub_9A52C0`（FO `0x5A46C0`）；鍵盤 `sub_9A6E20` 收到 4142 也進同一個函式，27 關窗。
   - 單件非堆疊 → Y/N 668「真的要把 "%s" 刪除嗎？」→ 是 → FU8§8.1 的流程（`alertitems.tbl` 內的再問 3122，送 C198）；
   - 單件堆疊 → 直接進數量提示；多件 → 用 3049「%d 次」再確認。
   - 本機字串表沒有 4115，但 **3054「要刪除幾個呢？(1-%u, Enter=全部, Esc=取消)」** 文字相同，可代用（推測是舊編號）。
3. **寶箱**：TW16 XML 窗沒有寶箱事件；最接近的 `CashWareHouse`（`sub_9A6EA0`）送 `ccc` 140, 6, 1（N 幣倉庫），3.81 沒有對應。本機 `uimagicitem.xml` 的 `SURF_RENEWAL_BTN_ITEM_CHEST_OFF` 在 3.81 做什麼**待驗證**。建議不做。

引擎：`NativeUi/ItemWindowPanel.cs:311–312` 垃圾桶與寶箱只畫圖，`:324–329` `Click` 只處理分頁與選取；U 鍵是引擎自訂（`OnlineInput.cs:10` → `Inventory.cs:126–144`）。要改：格子雙擊使用（U 鍵可保留）；垃圾桶或 Delete → 既有刪除流程，單件先問 668。

去向：Inventory 主分類仍是 K5（R7-H），本項併入 R9-M。

## 14. Q7-14 P-D 窗的原版規則

### 14.1 選角 `CSelectUI`

- 建構子 `sub_EEBB10`（FO `0xAEAF10`），vtable `0x11E42CC`；`Character_Ani` 存 `this+444`、`DeleteDelay_Image` 存 `+448`（已驗證）。
- 選角 `sub_EF0060`（FO `0xAEF460`，已驗證）：
  - 每列 `Class_Image`＝職業 0–6 用 **7049＋6×職業＋3×性別**（`this[90]`≠0 時改 6430＋8×職業＋4×性別；職業 7 用 8365／8368），**選中的那列再 +2**。
  - `Select_Button` 三態改成 7045／7046／7047；選中那列三態都畫 7047。
  - 待刪除中（`+176`，推測）：顯示 `DeleteDelay_Image`（模式 4，圖號依職業性別，例如王族 800／712）；否則播 `Character_Ani`，效果編號＝**4×職業＋41＋2×性別**（職業 7：72／74）。
- `CharacterInfo_List`（`sub_EECB10`，已驗證）：名稱、等級、職業名、血盟、正邪、**生日排成 `YYYY.MM.DD`**。
- **待驗證**：效果 41–74 由哪個表解成動畫（本機 `effectlist2.xml`、`GlobalEffect86.xml` 的這些編號都不像選角動畫）；`DeleteDelay_Image` 圖號在模式 4 的意義；3.81 `S_CharPacks` 的 `time` 是否為 `YYYYMMDD`。
- 資源：7045–7047、7049–7090、8365／8368 本機沒有，kor25 有；6430 本機有。
- 引擎：`NativeUi/SelectCharPanel.cs:38` 不建 `Character_Ani`／`DeleteDelay_Image`；`:33` 選中列用 XML PushSurf；`:34`／`:387` 生日顯示原值。

### 14.2 建角 `CCreateUI`（已驗證，`sub_EE4110` FO `0xAE3510`）

- 讀 `CharAttrType.tbl`（職業、型態、六項）與 `StatGuage.tbl`（職業、量表型態、能力 7–20 的百分比）。
- **Attack／Defense／Mix**：型態 1／2／3，同一型態再按＝重設；否則把六項**直接設成表上的值**。標題／說明字串 3607／3628＋3×職業＋型態。
- **±1 鈕**：總和 ±1 後在 1–75 之間，且在 TW16 的上下限表內（`byte_138B248`，.data FO `0xF89A48`）。**這是 TW16 規則，和 3.81 不同**：3.81 `C_CreateChar.java`（第 97–104、214–229 行）用 `ORIGINAL_*` 下限、總和須 75。引擎用 3.81 的值（`CharacterManagement.cs:11`），**不要換**。
- **`Gauge_Group`**（`sub_EE5430` FO `0xAE4830`）：每條寬＝圖寬 × `StatGuage[職業][型態][能力−7]` ÷ 100（能力不在 7–20 時 0）。5 條：0←STR、1←DEX、4←INT、3←CON、5←WIS；6 條：0←STR、1←DEX、2←DEX、3←CON、4←INT、5←WIS。
- **職業背景**：`Back_Image`＝surf **7156＋2×職業＋性別**（職業 7：7265／7266），換職業時以模式 2、31 步切換（推測是淡入淡出）。
- 資源：本機 `CharAttrType.tbl`（`Text.idx` #2773，`64da36ed6d718821`，21 列）用 3.81 的上下限與總和 75 逐列核對，**21 列都合格**。`StatGuage.tbl` **本機 24 個 idx 都沒有**；kor25 #33374、ly #33799（同 `2cf977ee45e67622`）、t89 `statguage.tbl` #20599（`ba8c58fc9226939c`）有。背景 7156–7169 本機 Sprite 沒有（7156、7157 只在 `LTsurf`），kor25 全有。
- 引擎：`NativeUi/CreateCharPanel.cs:31–33`、`:114–115`、`:197` 停用這三項。

### 14.3 釣魚 `CFishingUI`（已驗證）

- 建構子 `sub_E65660`（FO `0xA64A60`），量表預設 5000 ms。
- **`FishingNet` 不送封包**：`sub_E65B00` 開關 `FishObtainUI`（標題 `"[ %s ]"`＋字串 3674）。
- **`QuitFishing`**：`sub_E659A0` 先開 Y/N **1516**「你要取消自動釣魚嗎？」（本機有），是 → 送 TW16 op 146 `cc`(1)、顯示 1163。
- 開窗位置 (400 − 寬/2, 300 − 高)；刷新魚餌數（物品 14467）。
- **`AniGauge`**：`sub_E65510` 依伺服器訊息物件設時長（1000 × 欄位 ms，或 1000 ms），另一欄控制 `SpeedReel_Image`（6106、提示 3676／3677）。訊息物件推測是 TW16 新式自動釣魚封包，opcode 沒追。
- 3.81：只有 C62／C164／S158／S119／S71，**沒有時長或狀態欄位**，`AniGauge` 無法照原版驅動（K1 子項）。收竿 C62 引擎已接（`FishingPanel.cs:18`）。
- 引擎：`NativeUi/FishingPanel.cs:24–26`、`:147` 的註解改已驗證；`QuitFishing` 送 C62 前加 1516 確認。

### 14.4 選項窗 `OptionUI`（已驗證，除另註）

- 實例 `dword_13D43E4`（`sub_B56A40` `0xB58CA5` → 建構子 `sub_919170` FO `0x518570`）。開關函式 `sub_8CC400`（開在第 0 頁）的呼叫者：
  - 鍵盤分派 `sub_8C14F0`（`0x8C1934`，**Ctrl+V**＝22）與 `sub_8C2290`（編輯框焦點時）；
  - `sub_8C5380` 的 case 88／120（`X`／`x`，推測是另一情境的單鍵分派）；
  - `sub_6FC390` case 6（推測是主選單 8 鈕的第 6 顆）。
- 其他開法：HyperText 動作 `OptionUI`（`sub_583610`，帶頁號）、聊天指令處理 `sub_F47C00`／`sub_F49850`（開第 1 頁）。7 頁：Home、Application、UI、Game、Etc、Shortcut、ExpertKey。
- `OkBtn`（`sub_91BEA0`）與 `CloseBtn`（`sub_91BD10`）**都只關窗**，沒有「取消還原」。
- **設定值寫入**：31 個勾選框都是切換暫存副本 `dword_13D43E4+732+k` → **馬上** `sub_9184B0(this+732)`（FO `0x5178B0`）逐欄比對、不同才寫全域。**點一下就生效**。節錄：`ShowCharacterHPMPBtn`→`byte_138BE7C`、`ShowDropItemNameBtn`→`byte_138BE7E`、`ShowNpcNameBtn`→`byte_138BE7F`、`TransparentDeadMonsterBtn`→`byte_138BFAA`、`AutoLootPartyBtn`→`byte_13CE519`、`EnableTradeBtn`→`byte_136E99D`、`TopmostWindowBtn`→`byte_13CEFD2`、`CheckMute`、4 條音量捲軸。
- **R7-C 附帶項結案**：`byte_13A931C`＝選項 **`EnableElfTelControl`**（Home 頁與 Game 頁各一），bss 初值 0；設定檔載入 `sub_59DD30` 讀、`sub_59E920` 存、全部重設 `sub_998250` 設 0。讀取者 `sub_5041B0`：職業＝2（妖精）且等級 ≥ 50 且旗標為真（與 FU6§2 一致）。
- 3.81：選項窗是純客戶端；3.81 的 Ctrl+V／鈕 6 是 HTML `option1`（FU4§1.1 TW13 欄）。TW16 選項大多是 3.81 沒有的功能；對得上引擎既有功能的只有 HP／MP 顯示 4 項、掉落物名稱、NPC 名稱、透明屍體、音量／靜音、`EnableElfTelControl`。
- 引擎：`NativeUi/OptionPanel.cs:208–217` 有 `Open`／`Toggle` 但沒有呼叫者，勾選框只記 `Unhandled`；`NativeUi/MenuButtonsPanel.cs:68`、`:90`、`:251` 的鈕 6／Ctrl+V 開 HTML `option1`；`RingWindows.cs:40` `ElfBookmarkTeleportOption` 可接 `EnableElfTelControl`。**鈕 6／Ctrl+V 要不要從 HTML `option1` 改成原生窗需要裁定**（TW16 與 3.81 各有依據）。

### 14.5 狀態窗按 Up 之後（已驗證；屬 4:RenewalStatusUI、R7-I）

- `StatPointUP`（`sub_EE09D0`）：只在本機把該能力 **+1 預覽**，播能力特效 `sub_EBB500(16, k)`；其他 Up 隱藏、被選的改顯示 Down。**不送封包**。
- 特效播完：`sub_EBA6B0` 呼叫 `sub_EDE380` 開 Y/N（字串 4471 帶能力名與數值）。
- 按「是」：送 TW16 op 33 `chdhcs`(0, 確認編號, 479, 1, `"str"`／`"int"`／`"dex"`／`"wis"`／`"con"`／`"cha"`)；按「否」或取消：還原。
- 3.81 `C_Attr.java` case 479 讀 `C(1)＋S(屬性名)`，與 TW16 一致；引擎 `BonusStats.cs:10` 已送同格式。TW16 字串 4471、1131–1136 在本機表是別的內容，不能直接用。
- 引擎：`BonusStatsUi.cs:42`、`NativeUi/RenewalStatusPanel.cs:34`、`:348` 原生 Up 只做 `bonusSelect`，送出仍在 WinForms。改成 Up＝預覽＋確認，是＝`bonusSubmit`，否＝還原。

### 14.6 登入 `Back_Image`

- TW16 `CLoginUI`（建構子 `sub_EE8260`，vtable `0x11E3AD4`）的窗比 kor25 `LoginUI.xml` 多（`InfoAgreeWin`、`SettingWin`、`SecureWin` 等），推測 kor25 版較舊（已驗證事件、推測新舊）。
- 進 Login_Window（`sub_EEAC50` 場景 4）時取 `Back_Image` 設模式 0，XML 沒給圖號；同時從播放清單隨機挑一首**背景音樂**（`sub_8F2E40` → `sub_8F3B90`，Miles 串流）（已驗證）。
- `Back_Image` 實際畫什麼**待驗證**（讀模式欄位的繪製函式沒追到）。引擎 `NativeUi/LoginPanel.cs:22` 不畫，維持。登入 BGM 歸音效組。

### 14.7 去向

| 列 | 去向 | 理由 |
|---|---|---|
| 4:SelectUI | **K5** | 職業圖公式、選取鈕 7045–7047、生日格式已驗證，圖在 kor25（P-B 參考 surf）；`Character_Ani` 動畫表待驗證，這一小項留在 `Skipped` |
| 4:CreateUI | **K5** | Attack／Defense／Mix 用本機 `CharAttrType.tbl`；`Gauge_Group` 公式已驗證、`StatGuage.tbl` 走參考客戶端；背景 7156＋2c＋s；上下限維持 3.81 |
| 4:FishingUI | **K5**（`AniGauge` 子項 K1） | `FishingNet`＝開關 FishObtainUI（XML 走參考客戶端）、收竿前 1516；物品過濾表待驗證，code 任務要先裁定清單內容 |
| 4:OptionUI | **K5** | 開窗點與「點一下就生效」已驗證；只接引擎已有的設定；入口要不要改需要裁定 |

## 15. 資源查詢紀錄

本機 `0-客戶端` 24 個 idx 與參考客戶端的清單在各組 `scratch/rea_*/pak*`（不進 git）。查過的檔名：

| 檔名 | 本機 | 參考客戶端 |
|---|---|---|
| `MapUI.xml`／`MapUI-c.xml`／`mapui.xml` | 無 | kor25 #4423、t89 #4758、ly #5757／#5758 |
| `BookmarkUI.xml`、`BookmarkMemUI.xml`、`MapSystem*.xml` | 有 | — |
| 8891–8919 `.img`／`.tbt`、png 10748／10749 | 無 | 8908–8918 `.img` 參考客戶端也沒有；8919、10748／10749 只有 kor25／ly |
| 128、134、138、150、155、2944、2946、2947、2949、2951、2953、2972–2974 | 有 | — |
| `HyperTextUI.xml`、`HyperTextSubUI.xml` | 無 | 只有 ly |
| 1962、1968–1970、2040–2045、1111、1112、1114、1115、5073 | 有 | — |
| 9183 `.img`、11527 `.png` | 無 | 9183 在 kor25／ly；11527 只在 ly |
| `*.cur` | 0 筆 | — |
| 學魔法圖示 192 個 `.tbt` | 缺 5737、5734、5731、6458、6501 | 這 5 個 kor25／ly／t89 都有 |
| `usershop-c.html`、`PShopPoly.tbl`、`PersonalShopUI.xml`、`FriendUI.xml`、`ExcludeUI.xml`、`MailExcludeUI.xml`、`uiextra86-c.xml` | 有 | — |
| `ExchangeUI.xml`、`ReInventory.xml`、`ReWareHouse.xml` | 無 | kor25 #4392／#4458／#4517，ly、t89 也有 |
| `SummonUI.xml`、`summonList.xml`、`UserObtainUI.xml`、`effectlist2.xml`、`GlobalEffect86.xml`、`CharAttrType.tbl`、13、5029 | 有 | — |
| `FishObtainUI.xml` | 無 | kor25、pmc、c760、t89、ly 都有（內容相同） |
| `StatGuage.tbl` | 無 | kor25、ly、t89 |
| 7045–7047、7049–7090、8365／8368、7156–7169 | 無（6430 有；7156、7157 只在 `LTsurf`） | kor25 有 |

解出的檔（SHA-256 前 16 碼；「解密」＝`xml decrypt` 後）：

| 來源 | 檔案 | 原檔 | 解密 |
|---|---|---|---|
| 本機 `Text.idx` #36115 | `usershop-c.html` | `df84333d81b970c5` | — |
| 本機 `Tile.idx` | `PersonalShopUI.xml` | `407eeb38…` | `63a4def5…` |
| 本機 `Tile.idx` | `FriendUI.xml` | `7a89cdb3…` | `f840432a…` |
| 本機 `Tile.idx` | `ExcludeUI.xml` | `95da38cf…` | `e277020a…` |
| 本機 `Tile.idx` | `uiextra86-c.xml` | `2bac61f3…` | `7836f7a5aacc6cd0` |
| 本機 `Tile.idx` | `SummonUI.xml` | `cc73d7c27df866c1` | `ceb6b70c560a5821` |
| 本機 `Tile.idx` | `summonList.xml` | `becf334b15088ace` | `c1e6ce79b940dc76` |
| 本機 `Tile.idx` #5443 | `UserObtainUI.xml` | `9f233a189ccd904e` | `8b2fd2acb300dfcb` |
| 本機 `Tile.idx` | `effectlist2.xml` | `0dab4e39b2cc53a0` | `fc522b85beab8a23` |
| 本機 `Tile.idx` | `GlobalEffect86.xml` | `8daf2a66cf6254f7` | `652aebeccfdb006b` |
| 本機 `Text.idx` #2773 | `CharAttrType.tbl` | `64da36ed6d718821` | — |
| kor25 `Tile.idx` #4423 | `MapUI.xml` | — | `71A4900DB6656364` |
| ly `Tile.idx` #5758 | `MapUI.xml` | — | `6A02F0509151E980` |
| ly `Tile.idx` | `HyperTextUI.xml` | `00b3effd4570361c` | — |
| kor25 `Tile.idx` #4392 | `ExchangeUI.xml` | `a7abb4f8…` | `cdf53b56faade616` |
| kor25 `Tile.idx` #4458 | `ReInventory.xml` | `b44e60ce…` | `52ca2676edd38d24` |
| kor25 `Tile.idx` #4517 | `ReWareHouse.xml` | `0e10b345…` | `f7c9d6c60a73dfea` |
| kor25 等 5 套 | `FishObtainUI.xml` | `b66b93d28c830603` | `01d94c4464e3d798` |
| kor25 `Tile.idx` | `SelectUI.xml`／`CreateUI.xml`／`FishingUI.xml`／`LoginUI.xml` | `24661d17a8429adf`／`c759172004f8c9b6`／`a8f908cb7cb0bdf0`／`c79d636c56750fc5` | `b4eb1fa7409611aa`／`5277a2af96a41812`／`205b76bb7c185e69`／`dfefdd0dac096ff9` |
| kor25 `Text.idx` #2925 | `CharAttrType.tbl` | `17791ef622f9943f` | — |
| kor25 #33374／ly #33799 | `StatGuage.tbl` | `2cf977ee45e67622` | — |
| t89 `Text.idx` #20599 | `statguage.tbl` | `ba8c58fc9226939c` | — |

（「…」是該組只記了前 8 碼。）

## 16. 去向統計與建議 code 分組

### 16.1 K2 36 列的去向

| 去向 | 列數 | 列 |
|---|---:|---|
| 已有 | 3 | 1:2；1:238；4:UserObtainUI |
| K1 | 2 | 1:132；4:MapUI |
| K5 | 30 | Q7：1:61；1:52；1:86；1:144；1:44；1:175；3:C_QUERY_PERSONAL_SHOP(86)；3:C_INVITE_PARTY(82)；3:C_ADD_BUDDY(138)；4:BookmarkWindowUI；4:`MapLocationSaveWindow`；4:HyperTextUI；4:ExchangeUI；4:PersonalShopUI；4:FriendUI；4:`TradeWindow`；4:`ExcludeWindow`；4:SelectUI；4:CreateUI；4:FishingUI；4:OptionUI；5:（`TransferMenu`（22 列）。Q6（FU8，見 gap-matrix 15.10）：1:181；3:C_DUEL(118)；4:BehaviourUI；4:`HpMpWindow`；5:`sub_87C890`；5:`sub_8E66D0`；5:`sub_5001C0`；5:`byte_13CE51E`（8 列） |
| 仍 K2 | 1 | 1:127（3.81 倉庫數量輸入，只能真機或脫殼 `LiTo.bin` 確認） |
| **合計** | **36** | |

### 16.2 建議 code 分組（R9-A–R）

原則同 gap-matrix 15.6：同一個既有檔案只分給一組；新面板、新測試用自動發現；不改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`；PR 不改 `rebuild-lock.json`；新面板實作 `IUiClosablePanel`。R7／R8 已用的檔案寫在「依賴」。

| 組 | 目標（列） | 可改的檔案 | 依賴 |
|---|---|---|---|
| R9-A 書籤窗原版操作 | 拿掉右鍵刪除、Delete 多選；Insert＝加進快速移動清單（本次登入有效）；名稱排序去色碼；區域第 0 項 878；確認鈕不關窗；選目的地雙擊；隨機傳送鈕（id 0）（4:BookmarkWindowUI） | `NativeUi/BookmarkPanel.cs`、`BookmarkUi.cs`、新檔 `BookmarkWindowOriginalTests.cs` | 隨機傳送要 `RingWindows.cs` 接受 id 0 → 排在 R7-C 之後；區域表來源（`sub_F35D90`）未查前維持地圖編號 |
| R9-B 地圖窗右鍵選單與存點窗 | 右鍵放開出 `MapCommunicationSelectWindow`（2944／2947）；鈕 0 走 `MapSendEntry`、1785 畫在聊天列；鈕 1 開新的 `MapLocationSaveWindow`（本地存檔）；更正 `FieldMapPanel.cs:10`（4:`MapLocationSaveWindow`、5:（`TransferMenu`） | `NativeUi/FieldMapPanel.cs`、`NativeUi/LocationListPanel.cs`、新檔 `NativeUi/MapCommunicationPanel.cs`、`NativeUi/MapLocationSavePanel.cs`、`MapPopupTests.cs` | 兩個既有檔也在 R7-H → 排在 R7-H 之後；存點在地圖上的畫法先補查 |
| R9-C 聊天指令與 S161 文字 | `/invite`（不帶名稱＝選目標 179 → C230 0／1；帶名稱提示 3.81 不支援）、`/chatinvite` → C230 2、`/banish` → C255、`/outparty`、`/buddy`、`/addbuddy`（53）、`/delbuddy`、`/exclude 名稱`、`/include`、`/trade`（店主 989）、`/shop`、`/bookmark`／`/記憶`／`/qbookmark` 與中文別名；S161 換第一個 `^`／`!`、去開頭 `\fX`，id 0 系統文字也套用（3:C_INVITE_PARTY、3:C_ADD_BUDDY、1:61） | `PlayerChat.cs`（只加分派與 case 161 後處理）、`Party.cs`（加 type 2）、`Buddies.cs`、`Trade.cs`、`Inventory.cs`（只限 id 0 文字）、新檔 `ChatCommandFollowupTests.cs` | `Party.cs` 也在 R7-J → 排在其後；R8-C 若也動 `PlayerChat.cs`，兩組串行；`/shop`、`/bookmark` 只呼叫既有開窗入口；`/exclude 名稱` 是 3.81 推測（M37） |
| R9-D S55 不跟隨特效 | 不跟隨特效在對象消失後留在 `Origin` 播完；跟隨組改成段內固定、段間跳位置（推測）（1:86） | `SoundPackets.cs`（只限 `PrunePacketMedia`）、`MapEffects.cs`（只限 `PacketEffectFrames`）、`MapEffectFollowupTests.cs` | 無 |
| R9-E HyperText 小組 | ESC 關窗；數字框 [1, N]、Backspace、5073 上下鈕循環；OK／Cancel 只在有數字框時可按（1:144、4:HyperTextUI 的一部分） | `NativeUi/HyperTextPanel.cs`、新檔 `HyperTextFollowupTests.cs` | 同檔在 R7-D → 排在其後 |
| R9-F HyperText 排版器 | 行內連結、`<font fg bg face Size>`、`<img src>`、`<var textwidth img>`、`<b>/<i>/<u>/<p>/<br>`、對齊（1:144、4:HyperTextUI） | `NativeUi/HyperTextPanel.cs`、`NpcInteraction.cs`、新的排版檔與測試 | R9-E、R7-D 之後；顏色位元組順序與連結色先真機對色（M34） |
| R9-G 學魔法圖示 | 模式 7 列畫 `word_136D548` 物品圖示（1:44、1:175、4:`TradeWindow`） | `SpellTradeSource.cs`、`SpellExchangeUi.cs`、`SpellTradeTests.cs` | 無 |
| R9-H 好友／封鎖窗事件 | 好友窗 Delete → 1813 → C202；封鎖窗 Delete → C171；好友雙擊 → 聊天列 `"名稱 `；Esc（4:FriendUI、4:`ExcludeWindow`） | `NativeUi/FriendPanel.cs`、新檔 `FriendWindowTests.cs` | `FriendPanel.cs` 也在 R7-H → 排在其後；開窗鍵待驗證（M37） |
| R9-I 私商查詢頁 | 點店主開原生 HyperText `usershop-c.html`（`#0`＝店主名）；`userShop0／1` → C47；WinForms 分頁降為備援（3:C_QUERY_PERSONAL_SHOP） | `PrivateShopUi.cs`、`PlayerPointerUi.cs`、新檔 `UserShopPageTests.cs` | `PrivateShopUi.cs` 在 R7-G、HyperText 的 `img action` 在 R9-F → 排在兩者之後（或先用 R9-E 的面板做簡化版） |
| R9-J 個人商店流程 | 背包 → 出售／收購清單（沒有跨窗拖曳時用「背包選取＋點清單」並註明）；聊天列 898 單價 → 933 數量；雙擊移除；OK 檢查；兩行訊息 0xFF 分隔、0x00 結尾；不送外型（4:PersonalShopUI） | `PersonalShopIntegration.cs`、`NativeUi/PersonalShopPanel.cs`、`PrivateShop.cs`（只改訊息組裝）、新檔 `PersonalShopFlowTests.cs` | `PersonalShopPanel.cs` 在 R7-H → 排在其後；0xFF 的顯示待真機（M38） |
| R9-K 交易數量 | 交易窗加物品：堆疊先問 140（Enter＝全部）再送 C37；金幣走同一提示，金幣框標「引擎自訂」或移除（4:ExchangeUI） | `NativeUi/TransactionPanel.cs`、`TradeUi.cs`、新檔 `TradeAmountTests.cs` | 無 |
| R9-L 背包操作 | 格子雙擊使用；垃圾桶與 Delete → 刪除流程（單件 668、堆疊先數量 3054、`alertitems.tbl` 再 3122）；寶箱不做（Inventory 附帶） | `NativeUi/ItemWindowPanel.cs`、`ItemTransfer.cs`、新檔 `InventoryActionTests.cs` | R6-E 刪除流程（已在） |
| R9-M 召喚窗收尾 | 點怪物立即召喚並關窗、OK＝關；停用＝等級公式或魅力；快速 3 格（本機保存、按下即召喚、拖進來）；desc tooltip 與 `^` 換行；徽章 4944＋type；ESC（1:52） | `NativeUi/SummonPanel.cs`、新檔 `SummonWindowTests.cs` | `SummonPanel.cs` 在 R7-H → 排在其後；停用格是否吃點擊待真機（M39） |
| R9-N 公告板上一篇／下一篇 | 閱讀窗 672／676 照 §3.2 | `NativeUi/BoardPanels.cs`、`BoardUi.cs`、`Board.cs`、新檔 `BoardNavigateTests.cs` | R7-F 之後 |
| R9-O 選角／建角 | 選角 `Class_Image` 公式、`Select_Button` 7045–7047、生日 `YYYY.MM.DD`；建角 Attack／Defense／Mix（本機 `CharAttrType.tbl`）、`Gauge_Group`（參考客戶端 `StatGuage.tbl`，記 SHA-256）、背景 7156＋2c＋s；上下限維持 3.81（4:SelectUI、4:CreateUI） | `NativeUi/SelectCharPanel.cs`、`NativeUi/CreateCharPanel.cs`、新檔 `CharScreenFollowupTests.cs` | R7-N 也會在 P-D 面板加一行接線 → 兩組串行；`Character_Ani` 不做 |
| R9-P 釣魚窗 | `FishingNet` 開關 FishObtainUI（參考客戶端 XML）；收竿前 1516 確認；更正 `UserObtainPanel.cs:12` 註解（4:FishingUI、4:UserObtainUI 註解） | `NativeUi/FishingPanel.cs`、`NativeUi/UserObtainPanel.cs`（或新檔 `FishObtainPanel.cs`）、新檔 `FishingFollowupTests.cs` | FishObtainUI 清單內容要先裁定（物品表待驗證）；`AniGauge` 不做 |
| R9-Q 選項窗 | 勾選框接引擎已有設定、點一下就生效；`EnableElfTelControl` 接 `ElfBookmarkTeleportOption`；入口依裁定（4:OptionUI） | `NativeUi/OptionPanel.cs`、`NativeUi/MenuButtonsPanel.cs`（只接入口）、`RingWindows.cs`（只接選項）、`PlayerAudioSettings.cs`、新檔 `OptionWindowTests.cs` | 使用者裁定鈕 6／Ctrl+V；`MenuButtonsPanel.cs` 在 R7-I、R8-D，`RingWindows.cs` 在 R7-C → 排在三者之後 |

**併入既有組**
- **R7-A**：`Magic.cs` 的 179 改成字串提示（§8）。
- **R7-C**：`byte_13A931C`＝`EnableElfTelControl`，bss 初值 0（§14.4），R7-C 的附帶小項結案；UI 接在 R9-Q。
- **R7-I**：狀態窗 Up＝本機預覽＋特效＋Y/N，「是」才送 `bonusSubmit`，「否」還原（§14.5）。
- **R7-F**：672／676 的回呼已查清（§3.2），實作在 R9-N。

**不分組**：1:132（K1，只改 `FieldMapPanel.cs:10` 註解，併 R9-B）；4:MapUI（K1）；1:2、1:238、4:UserObtainUI（已有）。

### 16.3 新增真機項目

- **M34**：HyperText `<font fg>` 的位元組順序與連結色 `0x00B0FFFF`（截一張有 `<font fg>` 的頁對色）。
- **M35**：選目標模式下游標有沒有改變。
- **M36**：3.81 倉庫的堆疊物品怎麼輸入數量（1:127）。
- **M37**：好友窗的開窗鍵；`/exclude 名稱` 在 3.81 是否讀名稱。
- **M38**：個人商店兩行訊息的 0xFF 分隔在 3.81 怎麼顯示；寶箱鈕的作用。
- **M39**：召喚窗停用格的滑鼠外觀（`DisableOverSurf` 13）與能不能點。
- **M40**：登入畫面 `Back_Image` 畫什麼。

## 17. 限制

- 全部是靜態分析；3.81 `LiTo.bin` 有殼，凡是「3.81 客戶端怎麼做」的問題（倉庫數量、`ADENA_EXCHANGE_WINDOW` 誰開、寶箱鈕、0xFF 顯示）只能真機確認。
- 4 組分析由子代理執行，主控讀過每組草稿並抽查了：`MapR.java:35–36`、`C_CreateParty.java` 的 type、`SpellTradeSource.cs:15`／`:50`／`:60`、`BookmarkPanel.cs:399–402`／`:464–465`、`FieldMapPanel.cs:10`／`:59–61`、`TransactionPanel.cs:293`／`:298`。其餘 VA／FO 與 `檔案:行` 依各組的 IDA 輸出與讀檔結果，沒有逐一重看。
- 「沒有呼叫者」「沒有送出點」是依 IDA 交叉參照與立即值掃描；經由虛擬函式表、事件名稱表間接呼叫的地方可能漏掉（例如 `sub_8EECE0` 的 80 個呼叫點沒逐一看）。
- 主選單 8 鈕的對應（`sub_6FC390` case 6＝選項）、Delete 鍵碼 4142、多選的修飾鍵，都是依並列判斷的推測。
- 每組 IDA 結果、解出的資源與字串表 JSON 都在 `scratch/`，不進 git；查詢清單複製到 `rea-scripts/t818/`。
