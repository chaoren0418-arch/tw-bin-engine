# 查證補遺第 13 輪（FU13）：真機項目 M35／M37／M39／M40 的靜態部分（T385／#898）

依據：[`gap-matrix.md`](gap-matrix.md) 15.11 的 M35、M37、M39、M40；[`followups-9.md`](followups-9.md) §3.1、§8、§9.2、§14.6、§16.3。

記號同 FU9：**已驗證**＝反編譯／反組譯／資源檔直接看到；**推測**＝依欄位、命名或旁證判斷；**待驗證**＝本輪查不到，寫明卡在哪裡。

## 0. 來源與方法

- 分析對象：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（本輪重算）。
- IDA 7.7（idat 批次、唯讀查詢）＋P01 IDB `P01-分析基準/ida/03-ascii-isolated/TW1608262200.idb`，複製到 `scratch/t898/ida/tw16.idb`（SHA-256 `3A0BF206…BC17E82F`，與 FU5–FU11 相同）。沒有在副本上建函式或改名。腳本是 `rea-scripts/t742/ida_t742.py` 加一個 `uses OFF LO HI`（找某位移的所有讀寫），放在 [`rea-scripts/t898/`](rea-scripts/t898/)，31 輪查詢清單也在那裡。
- subst：開始前 `subst` 沒有輸出。只用 `Q:`，每輪結束都解除。第 3、12 輪各有一次 `Q:` 殘留（第 3 輪是管線提早結束、第 12 輪是腳本 BOM 讓 IDA 讀檔失敗），都已用 `subst Q: /D` 解除再重跑。其他任務（task-899）同時占用 `V:`，沒有碰。
- 地址換算（PE 節區表，本輪用 pefile 重算）：`.text` FO＝VA − `0x400C00`；`.rdata` FO＝VA − `0x401400`；`.data` FO＝VA − `0x401800`。bss 全域只寫 VA。
- 游標資源：用 pefile 從 BIN 的 `RT_GROUP_CURSOR`／`RT_CURSOR` 唯讀讀出，轉成圖片放在 `scratch/t898/cursors.png`（不進 git）。
- PakViewer CLI：用 `rea-scripts/pak/pak_index.ps1 -WithAlt` 重列 **0-客戶端全部 24 個 idx**（`Text`、`Tile`、`Sprite`、`Sprite00`–`Sprite15`、`LiTo\LTtext`、`LTsurf`、`LTsprite`、`LTtile`、`LTicon`；`launcher\TW13081901.pak` 沒有 idx），以及參考客戶端 kor25、t89、ly 的 `Tile`、`Text`、`Sprite`、`Sprite00`–`Sprite15`（每個 18 個 idx）。清單在 `scratch/t898/pak-index/`。用 `pak extract` 把 kor25／ly／t89 的 `1825.png`–`1842.png` 和本機 `Text.idx` 的 `buddy-c.html` 解到 `scratch/t898/pak-extract/` 算 SHA-256。**沒有複製任何資源進 git**。
- XML：沿用 #448 解出的 `scratch/task448-uixml/`（`FriendUI.xml`、`ExcludeUI.xml`、`SummonUI.xml`、`uimenu.xml`、`Actions.xml` 等 64 個），登入版面沿用 #629 解出的參考 `t89/loginui-c.xml`（本機沒有 `LoginUI.xml`，PAK§4）。

## 1. 結論一覽

| 項目 | 靜態結論 | 真機還要看什麼 |
|---|---|---|
| **M35** | 進入選目標模式本身不換游標；但**下一次滑鼠在世界上移動**時，世界滑鼠處理會把游標換成索引 **8**（瞄準橢圓）。技能類回呼（type 4）另外依射程換 8／19／20。選完或取消時還原為 3（手形） | 只需對照：進模式後移動滑鼠應看到瞄準游標，選完恢復手形 |
| **M37** | TW16 **沒有任何快捷鍵或選單會單獨打開 `FriendUI`**：`FriendUI` 指標的 21 個讀寫點都不呼叫顯示；`Friend_Window` 這個名字不在 BIN 字串裡。好友名單的入口是 `/buddy`、HyperAct `buddy`（都送 `c` 56），回覆畫在 TW16 的社群窗。`/exclude` **不讀名稱**：`Commandexclude` 固定傳空字串（再次確認） | 3.81 客戶端收到 S39 `buddy` 的畫法（推測是 `buddy-c.html` 的 HTML 頁）；`/exclude 名稱` 在 3.81 的行為 |
| **M39** | `DisableOverSurf="13"` **不是 surf 13**：它是一層 `XmlBoardImage` 半透明色塊（色＝palette[0]、強度 13），大小跟格子一樣。停用格**每一幀都畫**這層，跟滑鼠無關；停用格**不收一般點擊**（按下＋放開都不觸發事件），滑鼠移上去仍顯示提示 | 只需對照：停用格常駐變暗、點了沒反應、提示仍會出現；色塊顏色與濃淡 |
| **M40** | `Back_Image` 是**PNG 動畫**：進登入場景時把 png 模式設 0，之後每超過 200 ms 換下一張，png 編號 **1826→1842→1825** 循環（18 張，約 3.6 秒一輪）。圖在 kor25／ly 的 `Sprite*.pak`（800×600），**本機 0-客戶端 24 個 idx 都沒有** | 只需對照：登入背景是否為這組動畫、換圖速度 |

## 2. M35：選目標模式的游標

### 2.1 原版怎麼做（已驗證）

1. **換游標的函式**：`sub_8EECE0(n)`（VA `0x8EECE0`／FO `0x4EE0E0`）。`n` 和目前的 `dword_13D3EE4` 不同時才 `SetCursor`；`byte_13A931E` 為真用彩色組 `hCursor[n]`，否則用單色組 `dword_13D3E50[n]`。在 `dword_15442CC` 的特殊狀態下把 6／8／17／3／14 換成 31–35（FU8 已記）。
2. **游標表**：`sub_8EEBF0`（FO `0x4EDFF0`）用 `LoadCursorA(hInstance, MAKEINTRESOURCE(id))` 載入 1–36 號；單色組 id 表 `lpCursorName`（VA `0x13720E8`／FO `0xF708E8`），彩色組 id 表 `dword_1372180`（VA `0x1372180`／FO `0xF70980`）。

   | 索引 | 單色資源 id | 彩色資源 id | 外觀（`scratch/t898/cursors.png`） | 熱點 |
   |---|---|---|---|---|
   | 3 | 105（0x69） | 202（0xCA） | 手形（平常的游標） | (1,0)／(0,0) |
   | 8 | 136（0x88） | 208（0xD0） | 橢圓瞄準框（彩色版中間有紅十字） | (13,6) |
   | 15 | 145（0x91） | 216（0xD8） | 雙圈橢圓瞄準框 | (13,6) |
   | 19 | 220（0xDC） | 223（0xDF） | 瞄準框打紅叉 | (13,6) |
   | 20 | 227（0xE3） | 229（0xE5） | 青色瞄準框打紅叉 | (13,6) |
   | 23／24 | 233／234 | 231／232 | 紅／橘菱形 | (13,6)／(15,9)、(15,8) |

3. **進入模式**：`sub_F691C0`（FO `0xB685C0`）只做三件事：`sub_F68F30` 清舊回呼、存新回呼（`this[1]`）與範圍（`sub_F69200`→`this[0]`）、`sub_F690D0` 印字串（179 等）。**沒有呼叫 `sub_8EECE0`**（FU9§8.1 已記，本輪重看一致）。
4. **滑鼠移動時換游標**：世界的滑鼠處理 `sub_F02BA0`（FO `0xB01FA0`，vtable `0x11E4A48`）在 WM_MOUSEMOVE（512）且沒按左鍵時，若有指到物件、拖曳中或**選目標模式中**（`sub_F68FE0(dword_1657038)`＝回呼不為空），走 `sub_F039B0`（FO `0xB02DB0`）找滑鼠下的物件，再進 `sub_F044C0`（FO `0xB038C0`）決定游標。`sub_F044C0` 先處理幾個更優先的狀態（自己死亡 16、`sub_AA7DE0` 25、特定視窗狀態 13／5、`byte_13CE3FD` 11、拖曳物品 13／5），接著（`0xF04688` 起／FO `0xB03A88`）：
   - 選目標模式中，`byte_139FBAF` 或 `byte_13CE3F8` 為真 → 預設 **15**；否則預設 **8**。
   - 回呼類型是 4（`LineageSpell::CSpellCallback`，`sub_DF94E0` 設 type 4）時再看射程：`sub_E23BF0(0)` 為假 → 20（或 24）；為真時，對象在範圍內（`sub_523AC0(x, y, 範圍)`）→ 8（或 15），不在 → 19（或 23）。
   - 回呼類型 0（`CEnterPortalCallback`，右鍵傳送點）由 `sub_F02BA0` 自己處理：依地圖格旗標換 8 或 11。
   - 其他類型（`CWandTargetCallback` type 1、`CClickCallBackFunc` 等）→ 8。
5. **離開模式**：確認 `sub_F68E60`（FO `0xB68260`）最後 `sub_8EECE0(3)`；`sub_E08D90`（FO `0xA08190`）也是先換 8／15、處理完換回 3。

### 2.2 呼叫 `sub_F691C0(…, 15, 179)` 的入口（已驗證，`grepcallers`）

`sub_4FFF80`、`sub_505280`、`sub_741A90`、`sub_DF9CD0`、`sub_DFA600`、`sub_E216A0`、`sub_E255C0`、`sub_E2AB60`、`sub_E26350`（另一個是 282）、`sub_E26D00`（範圍＝技能資料）、`sub_504F10`（範圍 2）。右鍵傳送點是 `(…, 3, 625)`。全部共用 4 的換游標規則。

### 2.3 3.81 與引擎

- 純客戶端行為，3.81 沒有封包相關。3.81 `LiTo.bin` 的游標表沒查（有殼，見 FU12§0.1）；兩版共用同一套 `.cur` 資源編號是**推測**。
- main 現況：`engine/source/Magic.cs:10–16` 寫「進入模式後游標是否改變待真機（M35），引擎不換游標」；`Magic.cs:54` 同。引擎全程沒有自訂遊戲游標（找不到 `LoadCursor`、`.cur` 或 `new Cursor(`），對戰用 `Cursors.Cross`（`DuelUi.cs:41`）。

| # | main 現況 | 原版 | 建議修正 |
|---|---|---|---|
| D1 | 選目標模式不換游標（`Magic.cs:10–16`、`:54`） | 模式中滑鼠在世界上移動 → 索引 8（技能類依射程 8／19／20），結束 → 3 | `Magic.cs`／世界滑鼠處理：`SpellTargetPicking` 為真時把世界區的游標換成瞄準游標，選完或取消還原；註解改「已驗證（FU13§2）」 |
| D2 | 沒有遊戲游標資源 | 游標是 BIN 內嵌的 `RT_CURSOR`（上表） | 另開 resource 任務：從原版 BIN 唯讀讀出游標（或用系統游標 `Cursors.Cross` 暫代並標「引擎自訂」），不複製進 git |

## 3. M37：好友窗的入口與 `/exclude`

### 3.1 `FriendUI` 怎麼打開（已驗證：找不到單獨打開的入口）

1. **建立**：`FriendUI.xml` 在 XML 檔名表 `0x137B388` 起的第 34 項（`0x137B410`→字串 `0x119CA20`）。通用 UI 工廠 `sub_B56A40`（FO `0x755E40`）看到根節點 `FriendUI` 時建 `sub_B17DD0`（FO `0x7171D0`），存到 `dword_153FCB4`，登記到第 4 類清單（`sub_B54C40(…, 4)`）。視窗 `Friend_Window` 在 XML 寫 `Activate="0"`（建好不顯示）、根節點 `Toggle="true"`（`sub_B4FBE0` 存到 `this[133]`）。
2. **`dword_153FCB4` 的 21 個讀寫點**（`xref`）全部看過，沒有一個呼叫「顯示」（vtable +140 帶 1）：
   - `sub_8C7C80`（重設時清名單）、`sub_9872A0`（FO `0x5866A0`，TW16 擴充好友封包：社群窗開著時把名單填進 `FriendUI` 的資料，再由 `sub_BECED0`／`sub_BEA680` 畫到**社群窗**的好友頁）、`sub_ADA220`→`sub_B18300`（加／刪好友送 `cs` 138／124＋`c` 56）、`sub_B18880`（`CloseFriendWindow`，514 關窗）、`sub_B18F10`／`sub_B19030`／`sub_B191A0`（列的 Delete、雙擊、捲軸）、`sub_B191F0`（Esc 27 關窗）、`sub_B1B670`（1813 Y/N 回呼）、`sub_B56A40`（建立）。
3. **名稱查找**：`Friend_Window`、`Exclude_Window`、`MailExclude_Window` 這些字串**不在 BIN 裡**（`findstr` 沒有結果），所以程式沒辦法用 `sub_B502B0(名稱)` 找到並打開它。XML 裡的 `KeyboardEvent="FriendWindow"` 是視窗**自己收鍵**的事件名（`sub_B19270`→`sub_B191F0`＝Esc 關窗），不是開窗熱鍵。
4. **選單與熱鍵**：64 個解出的 XML 只有 `Actions.xml` 的 `Action_AddFriend`／`Action_Exclude`（對目標加好友／封鎖，不是開窗）提到好友；`uimenu.xml` 沒有好友項目。
5. **好友名單的實際入口**：
   - `/buddy`（指令類別）與 HyperText 動作 `buddy`（`HyperActBuddy`，vtable `0x11A9878`，執行 `sub_581BF0`）都只送 **`c` 56**（查名單）。
   - 伺服器回覆後：TW16 擴充封包走 `sub_9872A0`，畫在社群窗（`dword_154084C`）或舊社群窗。
   - `Toggle` 旗標 `this[133]` 的讀取者**沒追**（只確認寫入點），所以「有某個通用的切換鍵依 `Toggle` 開窗」不能完全排除（**待驗證**，但找不到窗名字串，可能性低）。
6. **3.81 的回覆**：3.81 伺服器回 **S39 `buddy`**（`docs/protocol/buddies.md`），是 SHOWHTML。本機 `Text.idx` 有 **`buddy-c.html`**（110 B，SHA-256 `8b7b624f…5b11d08d`）：

   ```
   好友名單(全部<var src="#0">人／在線<var src="#1">人)：<br><p><var src="#2">
   ```

   所以 3.81 世代很可能是「`/buddy` → S39 → 用 `buddy-c.html` 開 HTML 頁」，不是開 `FriendUI`（**推測**：3.81 客戶端的 S39 分派沒看；而且 S39 只帶 2 個字串，頁面有 3 個變數，人數怎麼算待查）。

### 3.2 `/exclude 名稱`（已驗證，補 FU9§9.2）

- 指令註冊 `sub_F56320`：`0xF5C435`（FO `0xB5B835`）推入 `"exclude"`、字串 366（「斷絕」）；物件 `sub_F50510`（FO `0xB4F910`）＝`Lineage::Commandexclude`，vtable `0x11EB5D8`。
- 執行函式（vtable +4，`0x11EB5DC`／FO `0xDEA1DC`）＝`sub_F54D40`（FO `0xB54140`）：**`sub_F40FC0("")`**，參數 `a1` 完全不用。→ `sub_987750("", 1)`（FO `0x586B50`）：空字串且有社群窗 → 切到封鎖頁（`sub_7F6D90(…, 1)`）；否則送 TW16 擴充封包 543。
- 對照：會讀名稱的是另一個類別 `CommandAddExclude`（vtable `0x11EB880`，執行 `sub_F53870`→`sub_F40FC0(Source)`）和 `CommandDeleteExclude`（`sub_F53A00`→`sub_F41FC0(Source)`）。它們綁的聊天字沒追（**待驗證**）。HyperText 動作 `exclude`（`HyperActExclude`，vtable `0x11A97F4`，`sub_582C00`）是另一條路。
- 所以「TW16 的 `/exclude` 不讀名稱」**確定**；3.81 怎麼做只能看 3.81 客戶端（有殼）或真機。

### 3.3 對照 main

| # | main 現況 | 原版 | 建議修正 |
|---|---|---|---|
| D3 | 原生好友窗只能從 WinForms 過渡鈕開（`BuddyUi.cs:32–33`、`:103`；`FriendPanel.cs:13`「開窗方式待驗證（M37）」） | TW16 沒有開 `FriendUI` 的熱鍵或選單；名單入口是 `/buddy`、HyperAct `buddy`（送查詢） | `FriendPanel.cs:13` 註解改「已驗證：TW16 無開窗鍵（FU13§3.1）」；原生入口改成 `/buddy` 收到 S39 時打開（或開 `buddy-c.html` 頁，等 3.81 對照再定）；過渡鈕保留到那時 |
| D4 | S39 `buddy` 只更新名單模型，不顯示頁面（`Buddies.cs:74–77`、`WorldSession.cs:39`） | 3.81 推測顯示 `buddy-c.html` | 待 3.81 對照後由 code 任務決定：收到 S39 `buddy` 時用現有 HyperText 面板開 `buddy-c`，`#0`／`#1` 填人數、`#2` 填名單 |
| D5 | `/exclude 名稱` 送 C171（`Buddies.cs:145`、`:160–164`，註明「推測，M37」） | TW16 不讀名稱；3.81 只有 C171 這條路 | 維持現狀（3.81 伺服器只認 C171），註解把「原版固定傳空字串」改為「已驗證（FU13§3.2）」；不帶名稱時原版會切到封鎖頁，引擎可改成打開封鎖窗而不是只提示 |

## 4. M39：召喚窗停用格

### 4.1 `DisableOverSurf` 是什麼（已驗證）

`XmlListItem` 屬性解析 `sub_AED660`（FO `0x6ECA60`，vtable `0x119B94C` 的 +44）把各屬性存成子元件：

| 屬性 | 位移 | 子元件 |
|---|---|---|
| `BaseSurf`／`BasePng` | +280（`this[70]`） | `XmlImage` |
| `HighlightSurf`／`HighlightPng` | +284（`this[71]`） | `XmlImage` |
| `PushSurf`／`PushPng` | +288（`this[72]`） | `XmlImage` |
| `DisableSurf`／`DisablePng` | +292（`this[73]`） | `XmlImage` |
| `OverSurf`／`OverPng` | +296（`this[74]`） | `XmlImage` |
| **`DisableOverSurf`** | **+300（`this[75]`）** | **`XmlBoardImage`**（`sub_B40C80`，FO `0x740080`） |
| `Disable` | +304（byte） | `sub_B0B6F0` 設停用旗標 |

`DisableOverSurf` 那一段（`0xAEDA7E`／FO `0x6ECE7E` 起）：建 `XmlBoardImage`，色＝`dword_1369D70[0]`（palette[0]，`sub_ADC8D0` 存 `+396`），**`atoi("13")` 存到 byte `+393`**（`sub_545E20`），大小取 BaseSurf（或 Highlight）的寬高，然後直接設成顯示。**13 不是 surf 編號**。

`XmlBoardImage` 繪製 `sub_B44890`（FO `0x743C90`，vtable `0x11A7A6C` +24）：`+392`＝0 且 `+393` 介於 1–30 → `sub_970B80(…, 寬, 高, 色, 13)`，以 palette[0] 色畫一個**半透明矩形**；`+393` ≥ 31 → `sub_97B5F0` 畫實心色塊；`+393`＝0 → 當一般圖畫。強度刻度推測是 0–31（13 ≈ 40%，**推測**）；palette[0] 是執行期色表第 0 格，推測是黑色（**推測**）。

### 4.2 什麼時候畫（已驗證）

`XmlListItem` 繪製（vtable +24＝`sub_B45320`，FO `0x744720`）：
- **停用**（`+304`，`sub_B06610`）：畫 Base（`this[70]`）→ DisableSurf（`this[73]`）→ **DisableOverSurf 色塊（`this[75]`）**，**每一幀都畫，不看滑鼠**；然後畫子元件（`sub_AF59C0`）。
- **可用**：Base → Highlight（滑鼠在上且沒按著）→ Over（滑鼠在上）→ Push（按著）→ 文字；再畫子元件。滑鼠旗標 `+312`／`+313` 由 `sub_B0BEA0`（vtable +380）一起設，同時播 `OverSound`。

召喚窗的怪物格（`sub_C00BF0`，FO `0x7FFFF0`）把**怪物圖設成這一格的 BaseSurf**，徽章（4944＋type）和等級字是子元件。所以停用格的樣子是「怪物圖 → 半透明暗色塊 → 徽章與等級字在最上面」。

### 4.3 停用格會不會吃點擊（已驗證）

`XmlListItem` 滑鼠處理（vtable +12＝`sub_B47010`，FO `0x746410`）在停用時：
- 512（移動）在格內：只顯示提示（`desc`），不設 Over、不播聲音。
- 513（按下）：只把「按著」旗標設 1（`sub_B0A360`），**不呼叫 `MouseEvent`**。
- 514（放開）：只有「按著」旗標是 0 時（也就是在別處按下、拖到這格才放開）才呼叫 `MouseEvent`（`sub_B03620`＝呼叫 `+172` 的事件函式），然後清旗標。
- 回傳 0（不吃掉事件）。

所以**直接點停用格不會召喚，也不會開始拖曳**（`SummonButton` 的 513 拖曳起點在 `sub_C003F0`，FO `0x7FF7F0`，停用時不會被叫到）。邊角：從窗外按下、拖到停用的怪物格放開，事件會送到 `sub_C003F0`；這時召喚窗的拖曳旗標 `+352`／`+353` 都是 0，會走 `sub_C00F50` 召喚（裡面只再檢查等級、不查魅力）。這是原版的漏洞，引擎不必照做。

### 4.4 對照 main（`engine/source/NativeUi/SummonPanel.cs`）

| # | main 現況 | 原版 | 建議修正 |
|---|---|---|---|
| D6 | `DisableOverSurf` 當 surf 編號讀（`SummonPanel.cs:207–208` `SurfId(item,"DisableOverSurf")`→ surf 13） | 13 是半透明色塊的強度，不是圖 | `Slot.DisableOver` 改成「色塊強度」；畫法＝格子大小、palette[0]（先用黑色）、透明度 13/32 |
| D7 | 停用格只在滑鼠移上去時畫 DisableOver（`:338`） | 停用格**常駐**畫色塊，與滑鼠無關 | `DrawSlot`：停用時一律畫色塊，而且畫在怪物圖**之上**、徽章與等級字**之下**；可用時才看 hover 畫 Over |
| D8 | 停用格不在可點清單（`:275–281`），提示仍顯示（`:314–323`） | 一致：停用格不觸發事件、提示仍顯示 | 註解「推測，M39」改「已驗證（FU13§4.3）」 |
| D9 | `:18–19` 註解寫「OverSurf／DisableOverSurf 是滑鼠移上去時畫」 | Over 是滑鼠移上去；DisableOverSurf 是停用常駐色塊 | 改註解 |
| D10 | Highlight（`HighlightSurf`）與 Over 的先後沒分 | 滑鼠在上且沒按著畫 Highlight，滑鼠在上畫 Over，按著畫 Push | SummonUI 的格子沒有 Highlight，影響小；通用 ListItem 若有再照這個順序 |

PolymorphUI、SPolymorphUI 的格子也是 `DisableOverSurf="13"`（各 9 格），同一套規則。

## 5. M40：登入畫面 `Back_Image`

### 5.1 原版怎麼做（已驗證）

1. 登入場景類別（vtable 在 `0x11E3B00` 附近）的切換函式 `sub_EEAC50`（FO `0xAEA050`）：進場景 4（`Login_Window`）時開 BGM（`sub_8F2E40`），顯示 `Login_Window`，`0xEEAD6F`（FO `0xAEA16F`）用 `"Back_Image"` 找到 `XmlImage` 存到 `this[86]`，有找到就 **`sub_B0D100(0)`**，再把 `this[88]`（上次換圖時間）、`this[89]`（幀號）設 0。
2. `XmlImage` 的欄位（`sub_B0F460`＝`Surf` 解析、`sub_B07F90`＝`Png` 解析）：`this[72]` surf 編號、`this[70]` surf 的 `;n`；**`this[83]` png 編號、`this[81]` png 的 `;n`**。`sub_B0D100` 設 `this[81]`，`sub_B0D0C0` 設 `this[83]`。所以 `Back_Image` 走的是 **Png**，不是 Surf。
3. 每幀更新 `sub_EE8D40`（FO `0xAE8140`，同一個 vtable 的 +8；「每幀」是依位置推測）：場景是 4 且 `this[86]` 存在時，若 `sub_A228B0()`（毫秒，`GetTickCount` 或 QPC）−`this[88]` **> 200**，就 `this[88]`＝現在、`this[89]`＋1（到 18 歸 0），**`sub_B0D0C0(this[89] + 1825)`**。
   - 換圖順序：1826、1827、…、1842、1825、1826…（18 張一輪，每張 > 200 ms，約 3.6 秒）。
   - 第一次換圖前（≤ 200 ms）`Back_Image` 沒有圖號，不畫（推測）。
4. 其他場景的 `Back_Image`（`sub_EE6DD0` 建角的 7156＋2×職業＋性別、`sub_9CF430`、`sub_EF0490`）與登入無關，不在本項。

### 5.2 圖在哪裡（PakViewer，已驗證）

- **本機 0-客戶端 24 個 idx**：`1825`–`1842` 只有 `Sprite.idx` 的 **`.tbt`**（地圖格貼圖，跟這裡無關）和 `Sprite08` 的 `1834.tbt`；**沒有任何 `1825.png`–`1842.png`**。
- **參考客戶端**（每個都查了 `Tile`、`Text`、`Sprite`、`Sprite00`–`Sprite15`）：kor25 與 ly **18 張都有、內容逐 byte 相同**（800×600）；t89 也有同名檔，但是 200×120、內容不同（不是這組背景）。

| png | kor25／ly 的 idx | 大小（B） | 尺寸 | SHA-256（kor25＝ly） |
|---|---|---:|---|---|
| 1825 | `Sprite03` | 590,385 | 800×600 | `a80f4b08fa8f2f2fbc0136c5d86e64e8e698e23141e670d7147b34a28ebcf046` |
| 1826 | `Sprite04` | 599,711 | 800×600 | `d81e3cf5f23a8ff4332222daafe5109877d64fe63c6b04c0e0bb83cbf33bba27` |
| 1827 | `Sprite05` | 596,310 | 800×600 | `94fc6d818b24a35408f02aff9afb1e31e798fa8489b134253955244d003be52f` |
| 1828 | `Sprite06` | 613,711 | 800×600 | `f1ebd622bcd94f8cdc22bc7f4123894b4e603e9e9231a765e423d845ef2fb006` |
| 1829 | `Sprite07` | 614,769 | 800×600 | `f998f5302cb48cf8d66e30b65670538afacb57035602152244d8130ee51394d9` |
| 1830 | `Sprite15` | 639,165 | 800×600 | `691264ed00df78331bb195902e74aa54295037856f5725a8fb8ecfb33d48a266` |
| 1831 | `Sprite00` | 647,804 | 800×600 | `717606e980e477f50892d1ae8d7ac9d5854db20fb25d570105a97966a129a123` |
| 1832 | `Sprite01` | 652,617 | 800×600 | `b508a0b976639cff28e80be87d5afec9a96c8e71f437258945735d9843bc3b7e` |
| 1833 | `Sprite02` | 653,117 | 800×600 | `b7aed1cd2427e7d4a538d3597df957d8dc22ea56d01f05c1c203e490cb0af712` |
| 1834 | `Sprite03` | 651,271 | 800×600 | `1d9ce2bfeff1422fda8300b1f5f19a383cd0448c98758245c059c384210073e1` |
| 1835 | `Sprite04` | 655,958 | 800×600 | `32cc847eec642affe3dc25e4664c6f7a2ff0642dc6246f468ed32393dcadcd22` |
| 1836 | `Sprite05` | 637,995 | 800×600 | `034a6a7083041fb2659a195bcc3fb71d667dedf97b8262dd708a89356f320349` |
| 1837 | `Sprite06` | 647,110 | 800×600 | `2d953c559192d1812b5a0ab8e0cf02a4b226577e9d05737a606789a271a22a7d` |
| 1838 | `Sprite07` | 625,960 | 800×600 | `020801d9f98c3bc8b0debf2a724d700f38c637fd897fa7df9a5190507aac129a` |
| 1839 | `Sprite08` | 632,584 | 800×600 | `25e225d067283e107cc61f13abe6dbee984f71d69ec538c3e2728ce10eb90db7` |
| 1840 | `Sprite00` | 612,543 | 800×600 | `aae2a533a034ab2e25462d7fe24e78cac8393259491900847607ff88e5ac5888` |
| 1841 | `Sprite01` | 615,591 | 800×600 | `be4ddd5e043d1ea540fcdc6d1ec29b7e4d8826d68d5c1aede26058be64075700` |
| 1842 | `Sprite02` | 599,088 | 800×600 | `22698f5db6d22e56d169f0eca9118f12629eff8fbe589bd6b60d779c67619b92` |

`1825.png` 的畫面是黃昏城鎮遠景（左邊塔樓與旗幟），符合登入背景。參考客戶端的 idx 編號就是 `SurfCatalog.NameHash` 的分桶（同 `OptionPanel.cs:23` 的找法）。

### 5.3 對照 main（`engine/source/NativeUi/LoginPanel.cs`）

| # | main 現況 | 原版 | 建議修正 |
|---|---|---|---|
| D11 | `Back_Image` 沒有 Surf → 跳過、不畫（`LoginPanel.cs:22`、`:43`、`:222`） | Png 動畫 1825–1842，每 > 200 ms 換一張，順序 1826 起、1825 收尾 | `LoginPanel.cs`：顯示登入窗時記下開始時間，依「(經過時間 ÷ 200 ms) mod 18」選 png（1825＋((n＋1) mod 18)），用 `OptionPanel` 的 png 找法（本機→參考 `Sprite*.pak`，記 SHA-256）畫在 `Login_Window` 最底層；找不到圖就照舊不畫並列進 Missing |
| D12 | `:43` 註解「Back_Image（no Surf）is still not drawn（Q7-14）」 | 已查清 | 改成「Png 1825–1842 動畫（FU13§5），只在參考客戶端」 |

## 6. 真機縮小後的核對清單

- **M35**：施放一個沒有選好對象時會進入選目標模式的魔法（R7-A／SV#3 的入口），看到聊天欄「請選擇對象。」後，移動滑鼠到世界上：游標應變成橢圓瞄準框（彩色游標開著時中間有紅十字）；移到超出射程的位置可能變成打紅叉的框；選完或取消後恢復手形。
- **M37**：在 P24Lab 3.81 打 `/buddy`：看是否跳出 HTML 頁「好友名單(全部 n 人／在線 m 人)」＋名字（`buddy-c.html`）；有沒有任何快捷鍵會開好友窗（TW16 沒有）。打 `/exclude 測試角色名`：記下 3.81 客戶端是否送出封包（伺服器回 S250/18 或沒反應）。
- **M39**：開召喚窗、選一個高等級頁：魅力或等級不夠的格子應**常駐變暗**（半透明黑），滑鼠移上去不變亮但會出提示；點擊、按住拖曳都沒反應。
- **M40**：登入畫面背景應是會動的 800×600 圖（城鎮遠景，18 張約 3.6 秒一輪）。

## 7. 未驗證／推測

- M35：`byte_139FBAF`、`byte_13CE3F8`（換成 15 號游標的條件）與 `sub_E23BF0(0)` 的意義沒查。3.81 的游標表沒看。
- M37：`FriendUI` 根節點 `Toggle`（`this[133]`）的讀取者沒追；`CommandAddExclude`／`CommandDeleteExclude` 綁的聊天字沒追；3.81 收 S39 `buddy` 的畫法沒看（3.81 有殼；FU12 用的 OEP 快照也許能查 S39 分派，建議下一輪）。
- M39：palette[0] 的實際顏色、`sub_970B80` 的強度刻度（推測 0–31）沒展開。
- M40：`sub_EE8D40` 是每幀呼叫是依 vtable 位置推測；第一次換圖前是否有圖沒確認。登入 BGM 不在本項。
- 「沒有呼叫者」都是 IDA 交叉參照與字串搜尋；經由虛擬函式或資料表間接呼叫的地方可能漏掉（§3.1 已列出補強的理由：窗名字串不存在）。
