# 查證補遺第 4 輪：第三輪盤點留下的 Q3 細項

T287（#680）。查 [`gap-matrix.md`](gap-matrix.md) 第 12.6 節的 Q3-1、Q3-3、Q3-4、Q3-5、Q3-6、Q3-8、Q3-9（Q3-2、Q3-7 依 12.6 排到下一輪）。簡稱：本文 **FU4**，引用寫 `FU4§n`。

每節分「原版怎麼做／3.81 是否支援／對應的 R3 組要改什麼」。記號：**已驗證**＝反編譯、反組譯、原始碼或資源檔直接看到；**推測**＝依欄位、命名或旁證判斷；**待驗證**＝本輪查不到，寫明卡在哪裡。

## 0. 方法與來源

- **TW16 BIN**：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（本輪重算）。
- **TW16 IDB**：P01 `ida/03-ascii-isolated/TW1608262200.idb` 複製到本 worktree 的 `scratch/rea/ida/tw16.idb`，複製前後 SHA-256 都是 `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f`。
- **TW13 IDB**（只用在 Q3-1）：TW16 的舊底列 `Status800` 是死碼（[`ui-layout.md`](ui-layout.md)「底列行為（V5）」），選單鈕的動作只能從 TW13 看。來源是 #460 在 `scratch/wt/task-460/scratch/rea/tw13/` 建的 IDB，複製到本 worktree 的 `scratch/rea/ida13/tw13.idb`，前後 SHA-256 都是 `34af632902f07b20960053aec292481e6bbe402a0de02b5820a18593dd5d1ad1`；對應的 `tw13.exe` SHA-256 `653da514f750d4714fde2c83e5162aeef553c876ec541fe1cfebf6970f5c62a4`（與 V5 記錄相同）。
- IDA 7.7＋Hex-Rays，唯讀批次：TW16 10 輪（q1–q10）、TW13 16 輪（t13a–t13p）。查詢清單與腳本在 `rea-scripts/t680/`（見第 9 節）。
- **subst**：每輪執行前後都跑 `subst`，開始時沒有被占用的代號；本輪一律用 `W:`，每輪結束後 `subst` 都沒有輸出（沒有殘留）。
- **地址換算**：TW16 程式碼 FO＝VA − `0x400C00`；`.rdata` FO＝VA − `0x401400`；`.data` FO＝VA − `0x401800`（同 SS2§0）。TW13 只有一個 `.image` 區段，FO＝VA − `0x400000`。
- **本機資源（只讀，不進 git）**：`D:\lineage開發\0-客戶端\Text.pak`（SHA-256 前 16 碼 `9516a372c43b269c`）。在記憶體中解出 `string-c.tbl`（`decoded_sha256` `4ebbc1a7…43e9`）、`desc-c.tbl`（`f06c94f4…b7df`，16945 行）、`help-c.html`、`option1-c.html`、`option2-c.html`、`option2p-c.html`、`option11-c.html`。`uiextra86.xml`（SHA-256 `7836f7a5…83cd`）、`uistatus86.xml` 是 #448 解到 `scratch/task448-uixml/` 的結果，本輪只複製到本 worktree 的 `scratch/` 讀。
- **3.81 伺服器**（`D:\lineage開發\1-伺服器`，唯讀）：`S_UseAttackSkill.java`（SHA-256 前 16 碼 `83e00ecf1da36351`）、`S_EffectLocation.java`（`0816db3e29cc8b89`）、`S_UseArrowSkill.java`、`S_Ability.java`、`L1EquipmentSlot.java`、`db/380_db.sql`。
- **引擎**：本分支 `engine/source`，只讀，不建置、不執行。
- 全程靜態分析。沒有啟動遊戲、伺服器或 DB；沒有改產品程式；沒有任何 BIN、IDB、pak、XML 進 git。

## 1. Q3-1 開窗熱鍵與選單 8 鈕（R3-A）

### 1.1 原版怎麼做

**鍵碼格式（已驗證）**：鍵盤分派的參數是 WM_CHAR 值。Ctrl＋字母送 1–26（Ctrl+A＝1…Ctrl+Z＝26），Tab 也是 9、ESC 是 27；Alt＋鍵是 `0x8000|字元`（例如 Alt+X＝`0x8058`）；方向鍵、Delete、F1–F3 等是 `0x1000|VK`。證據：TW16 編輯框子類別 `sub_AC22D0`（VA `0xAC22D0`／FO `0x6C16D0`）在 `WM_CHAR`（`0x102`）時直接以 `wParam` 呼叫 `sub_8C2290`。

**TW16 的分派（已驗證）**：`sub_8CA740`（VA `0x8CA740`／FO `0x4C9B40`）先讓視窗堆疊、聊天列、血盟倉庫紀錄窗（`dword_153D218`）等吃鍵，都不吃才交給 `sub_8C14F0`（VA `0x8C14F0`／FO `0x4C08F0`）。焦點在編輯框時改走 `sub_8C2290`（FO `0x4C1690`），內容幾乎相同。視窗全域與名稱由 XML 工廠 `sub_B56A40`（FO `0x755E40`）的 `strcmp` 對出。

**TW13 的分派（已驗證）**：`sub_4D3910`（VA `0x4D3910`／FO `0xD3910`，WM_CHAR）與 `sub_4D4510`（FO `0xD4510`，編輯框焦點）。視窗類別由建構子的 vtable 名稱確認：`InventoryWindow`（`sub_4BD5A0`）、`SpellWindow`（`sub_741080`）、`NewMailWindow`（`sub_518580`）、`HyperTextWindow`（`sub_4A1F40`）、`CRenewalStatusUI`（`sub_793600`）、`CUserPlayStatsUI`（`sub_6AE590`）。

| 鍵 | TW13（舊式視窗） | TW16（XML 視窗） |
|---|---|---|
| **Tab／Ctrl+I**（9） | 切換 `InventoryWindow`（`dword_9A924C`） | 切換 `Inventory`／`RenewalInventoryUI`（`dword_14EBEC0`，vtable `+76`） |
| **Ctrl+S**（19） | 切換 `SpellWindow`（`dword_C31320`） | 切換 `SpellUI`（`dword_1542440`） |
| Ctrl+A（1） | 切換 `CRenewalStatusUI`（`dword_C31EE4`） | 切換 `RenewalStatusUI`（`dword_15433B8`） |
| Ctrl+H（8，需按著 Ctrl，Backspace 不算） | HTML `help`（說明，內文寫「同按 Ctrl+H 鍵可關閉說明視窗」） | `sub_8CC330(0)` |
| Ctrl+V（22） | HTML `option1`（功能選項） | `sub_8CC400(0)` |
| Ctrl+C（3） | HTML `option2`／王族 `option2p`（社群選項） | `sub_7F6D90`→`sub_7F7240` |
| Ctrl+Q（17）、Alt+X | 切換 `CUserPlayStatsUI`（`dword_C3094C`） | 切換 `UserPlayStatsUI`（`dword_1540880`） |
| Ctrl+P（16） | 截圖 | 截圖（「Saved current screen to %s」） |
| Ctrl+O（15） | 字型窗 `FontWindow` | 同左（`sub_83D7A0`） |
| Ctrl+B（2）、Ctrl+D（4）、Ctrl+E（5） | —、`dword_C2FA10`、`Status800` | `MonsterDexUI`、`BehaviourUI`、`sub_ECC680` |
| ESC（27） | 關掉堆疊中的視窗（最多 4 個）與字型窗 | 同左 |
| Ctrl+M | WM_CHAR 分派沒有（13 是 Enter）；地圖只有選單鈕 13（單鍵 `M` 那段沒有呼叫者，見下）。WM_KEYDOWN 端沒查 | `sub_8C1290`（WM_KEYDOWN，`'M'`＋Ctrl 旗標 `0x4000`）→ `sub_8DCC40` |
| F1–F3（`0x1070`–`0x1072`） | `Status800` vtable `+88`(0/1/2)（巨集頁） | `sub_9FA0D0(0–2)` |

- **倉庫沒有熱鍵（已驗證）**：兩版的分派都不碰倉庫全域（TW16 `RenewalWareHouseUI`＝`dword_14EBF48`、TW13 的倉庫窗），倉庫只由 S_RETRIEVE_LIST（op 127）開（WIN2§2.1 第 5 點）。
- TW13 另有一段「單鍵」分派，起點 `0x4D4E20`（FO `0xD4E20`）：`V`＝背包、`S`＝魔法、`C`＝狀態、`X`＝`option1`、`O`＝`option2`、`H`＝說明、`M`＝地圖。這段**沒有任何呼叫者**（xref 與立即值掃描都是 0），推測是沒啟用的單鍵模式，不採用。

**選單 8 鈕（TW13 已驗證）**：`Status800` 建構子 `sub_77F6B0`（VA `0x77F6B0`／FO `0x37F6B0`）把每個 `IMAGE_BUTTON` 的 `action` 當索引，從函式表 `off_8F023C`（FO `0x4F023C`）取點擊回呼。`uistatus86.xml` 的 `title` 與回呼：

| action（title） | 回呼 | 開什麼 |
|---|---|---|
| 13（Map） | `sub_77F510` | 地圖窗（`dword_C2F7DC`，世界／村莊等四種外觀擇一）；沒有角色或 `dword_C2F7D8==4` 時改顯示訊息 1734 |
| 1（Mail） | `sub_77F400`→`sub_4D7990` | `NewMailWindow`（`dword_AC4CE0`；已開的信件窗 `dword_9AAB0C`／`dword_9AAB08` 優先關閉） |
| 2（Status） | `sub_77F410` | 切換 `CRenewalStatusUI`（同 Ctrl+A） |
| 3（Spell） | `sub_77F430` | `SpellWindow`（同 Ctrl+S） |
| 4（Item） | `sub_77F450` | `InventoryWindow`（同 Tab） |
| 5（Pledge） | `sub_77F470`→`sub_4D34F0` | HTML `option2`（「對話／血盟／同盟／組隊／朋友選項」）；王族看 `option2p`（第一項改成「命令選項」）。再按一次關閉（同 Ctrl+C） |
| 6（Option） | `sub_77F480`→`sub_4D3460` | HTML `option1`（「功能選項：環境設定／社群設定／指令設定／幫助」）（同 Ctrl+V） |
| 7（Quit） | `sub_77F490` | 切換 `CUserPlayStatsUI`（遊玩統計窗，同 Ctrl+Q）。**不是直接登出** |

另外：action 0（Help，XML 裡被註解掉）＝HTML `help`；8＝巨集鎖；10–12＝巨集頁 0–2；14（Shout 鈕 3165）＝送 `cc` 63,5。

### 1.2 3.81 是否支援

- 3.81 用的是本機 `uimagicitem.xml`／`uiextra86.xml` 的舊式視窗（WIN2§2.2），和 TW13 同一代，所以**熱鍵與選單鈕以 TW13 為準**。實際玩的 `LiTo.bin` 有殼，沒有直接驗證（推測）。
- 熱鍵與選單都是客戶端行為，不需要伺服器支援。HTML 頁 `help`／`option1`／`option2`／`option2p` 本機 `Text.pak` 都有。

### 1.3 R3-A 要改什麼

- `ItemWindowPanel.Key`：Tab 與 Ctrl+I 切換 `ITEM_WINDOW`；`SpellWindowPanel`：Ctrl+S 切換 `SPELL_WINDOW`。**倉庫不加熱鍵**。
- `MenuButtonsPanel` 的 action 對應：4→`ITEM_WINDOW`、3→`SPELL_WINDOW`、13→`Field_Map`（同 Ctrl+M）、1→信件窗、6→HTML `option1`、5→HTML `option2`（王族 `option2p`），用既有 NPC／HTML 頁顯示；2（狀態）、7（統計窗）對不到已註冊的原生窗時維持 DevTools 並標待驗證。**action 7 不應直接登出**：原版開的是統計窗（引擎已有 `UserPlayStatsPanel`），現在的 `Logout` 要改。
- 其餘熱鍵（Ctrl+A／H／V／C／Q／P）屬於選配，R3-A 可以只做背包、魔法兩個，其餘列待辦。

## 2. Q3-3 酒醉減半條件 `sub_81B0E0`（R3-H）

### 2.1 原版怎麼做（已驗證）

- `sub_81B0E0`（VA `0x81B0E0`／FO `0x41A4E0`）＝取全域繪圖物件 `dword_168AF18`（`sub_1039BE0`，FO `0xC38FE0`；由 `sub_1039BF0` 設定）後呼叫它的 vtable `+8`。
- 從其他呼叫者看它的意思：
  - `sub_97B5F0`（FO `0x57A9F0`）：為真時直接畫 RGB565 色；為假時先把 565 轉成 `0x00RRGGBB` 再交給另一個表面 `dword_13CCB5C`。
  - `sub_9E8E50`（FO `0x5E8250`）：掃描線跨距 1856 bytes（928 px × 2），為假時乘 2（928 px × 4）。
- 所以 `sub_81B0E0()` 為真＝**畫面是 16 位元色彩**，為假＝32 位元。酒醉波 `sub_F01E50`（FO `0xB01250`）在 32 位元路徑才把位移與來源 x 除以 2。
- 32 位元時為什麼要除 2（是補償還是錯誤）沒有追到 vtable 的實作類別（**待驗證**）。但除 2 只是像素格式的處理，跟遊戲狀態無關。

### 2.2 3.81 是否支援

和伺服器無關。3.81 客戶端（`litoddraw.dll`）是 16 位元 DirectDraw 世代（推測），走全振幅分支。

### 2.3 R3-H 要改什麼

- `TimedBuffRules.LiquorFullWave` 固定回 true 就是原版 16 位元的行為，保留即可。建議把註解改成「`sub_81B0E0`＝16 位元色彩模式；引擎照 16 位元路徑全振幅」，減半分支可以刪掉或只留測試。

## 3. Q3-4 光效亮度 +5（R3-H）

### 3.1 原版怎麼做

**提亮與還原（已驗證）**：計數 `dword_13CE258` 只在下列地方讀寫：特效物件的兩個方法、`initGame` 清 0、明暗曲線 `sub_EDC880`（計數 > 0 時不覆寫 L）、`sub_F09C70`。

- `sub_8ABC10`（VA `0x8ABC10`／FO `0x4AB010`）是 `Effect` 類別 vtable `+24`（開始播放時呼叫，vtable 位置 `0x117ECE4`／FO `0xD7D8E4`）：物件 `+9 ≠ 0`，而且 `+12 == 1` 時，計數加 1；計數從 0 變 1 時記住 L，並設 L＝min(15, L+5)（`sub_9E3260`）。第二次呼叫（`+664` 已設）而且 `+12 ≠ 1` 時減 1，歸 0 時還原 L。
- `sub_8ACB20`（FO `0x4ABF20`，計時重複呼叫的特效）規則一樣，只是條件是 `+12 == 2`。

**哪些物件會符合（部分已驗證）**

- `Effect` 建構子 `sub_8AB390`（FO `0x4AA790`）：`+9`＝第 5 參數，`+12` 一律設 0。
- 本輪在 37 個建構呼叫者裡找 `+12` 的寫入，只在彈道類（建構子 `sub_7C36C0`、移動 `sub_7C3AB0`）找到：倒數到 1 設 1、到 0 設 2。但彈道類的 vtable 是 `0x11727FC`，`+24` 是 `sub_7C3950`，**不經過** `sub_8ABC10`，也不碰計數。
- `+9 ≠ 0` 的 `Effect` 來源：
  - S_EFFECT_LOC（op 219＝3.81 S106，`sub_934530`／FO `0x533930`）在 gfx 2028–2056 時第 5 參數是 1。但 `+12` 仍是 0，依上面的規則**不會提亮**。
  - S_ATTACK（op 5）把封包裡 gfx 後的 1 byte 當第 5 參數，傳給彈道建構子。3.81 `S_UseAttackSkill` 這個 byte 固定寫 0；`S_UseArrowSkill` 寫 `0x7F`。
- 結論：觸發條件已確定，但**哪個封包會建出 `+12 = 1／2` 的 `Effect`，本輪沒有找到**（待驗證）。下一步要全程式掃描 `Effect` 物件 `+12` 的寫入，以及 vtable `0x1150D4C` 那個也用 `sub_8ABC10` 的類別。

### 3.2 3.81 是否支援

依目前找到的路徑，3.81 會送的 S106 只建出 `+12 = 0` 的特效，不會提亮（推測）。

### 3.3 R3-H 要改什麼

- **先不接** `BeginLightEffect`／`EndLightEffect` 的封包觸發，維持 12.5「沒查到前只做盲目」。在 `Ambience.cs` 的註解補上觸發條件（`+9 ≠ 0` 且 `+12 == 1／2` 的 `Effect`，FU4§3）。另開查證項（建議代號 Q4-1）。

## 4. Q3-5 隱身可見例外 `sub_956D30`、`$431`（R3-H）

### 4.1 原版怎麼做（已驗證）

**`dword_13DC4E4 && sub_956D30(id)` ＝「對方在自己的隊伍名單裡」**

- 在 `0x94046C`，S_INVISIBLE（`sub_940330`）呼叫前執行 `mov ecx, dword_13DC4E4`。
- `sub_956D30` → `sub_953C00` → `sub_953950`（FO `0x552D50`）在 `dword_13DC4E4` 的名單裡逐筆比 `*(entry) == id`（`sub_956120` 回傳項目 `+0`）。
- `dword_13DC4E4` 是隊伍名單：S_HIT_RATIO（`sub_93FAD0`）用它更新隊伍 HP（PG§5），隊伍相關函式群 `0x954D90`–`0x959710` 都用它。
- 路徑上**沒有血盟判斷**。同盟或血盟成員隱身時，走一般人的 `0x40` 分支。

**`0x40` 分支的可見條件 `sub_F03F20`**（VA `0xF03F20`／FO `0xB03320`），三個條件都要成立：

1. `sub_500EA0(1,3)` 檢查的是加密數值表 `unk_14EBFA0` 第 3 格（`ecx = 0xC*3 + 0x14EBFA0`），要在 1–3 之間。這張表唯一的封包寫入點是 S_CHANGE_ABILITY（op 122，`sub_932300`／FO `0x531700`）：讀 `cc`（類型、值），類型 < 6 時寫進第「類型」格。所以這一條＝**S36 類型 3 的值在 1–3**。FU3§3 的類型 1（傳送控制戒指）、類型 5（召喚控制戒指）是同一張表。
2. `byte_1540830 == 0`。
3. `sub_990E10("$431")`（FO `0x590210`）：在背包管理器（`sub_4F9FF0` 回傳的 `dword_13A9618`；`sub_990610` 依 id 查物品也用它）裡找一件物品，條件是 `+36`（word）== 87，而且名稱（`+43`）含有 `$431` 的展開文字。展開用 `dword_153C9C4` 的 `$n` 表（`sub_A11520`，FO `0x610920`），和血盟倉庫紀錄判斷 `$4`（金幣）是同一張表。

### 4.2 3.81 是否支援

- 3.81 `S_Ability` 只送類型 1、2、5（`L1EquipmentSlot.java:411–418`，傳送控制、變形控制、召喚控制戒指），**不送類型 3**。所以第 1 條永遠不成立，`0x40` 的隱身者在 3.81 **永遠不畫**。
- 本機 `desc-c.tbl` 第 431 行是「沙哈的戒指」（第 4 行「金幣」可以用來對照行號）。3.81 的 `380_db.sql` 沒有 `name_id = '$431'` 的物品，也沒有這個名字（推測：TW16 的新道具）。`+36 == 87` 的欄位意義沒有追到（待驗證）。
- 隊伍名單：3.81 會送隊伍封包，引擎已經有 `partyMembers`。

### 4.3 R3-H 要改什麼

- `Invisibility.InvisiblePeerVisible` **拿掉血盟條件**，只留「對方在隊伍名單」。註解改成「已驗證（FU4§4）」。
- `$431` 分支在 3.81 不會成立，不要實作；其他隱身者一律不畫（維持現狀）。
- 原版是在收到 S171 時決定旗標，引擎在繪製時判斷。入隊、退隊時的差異列為待驗證，不必改。

## 5. Q3-6 血盟倉庫紀錄窗 `sub_A559D0` 版面（R3-C）

### 5.1 原版怎麼做

**建立（已驗證）**：`sub_A559D0`（VA `0xA559D0`／FO `0x654DD0`，vtable `off_11901C4`／FO `0xD8EDC4`）。`initGame` 建立後存進 `dword_153D218`；S250 子碼 117（`0x93CEC0`／FO `0x53C2C0`）填資料後開窗。版面借用 `uiextra86.xml` 第 3 段 **`HYPER_TEXT_WINDOW`**：`sub_7D7650(dword_153F598+0x30, 3)`；段序由 `uiextra86` 讀取者 `sub_A842D0`（FO `0x6836D0`）的 `v145[11] = 3` 確認。

- `uiextra86` 每一段把 `SURF_IMAGE` 和 `IMAGE`／`IMAGE_BTN`／`STAGE3_BTN` 分成兩張清單（`sub_A842D0` 第 525、544 行）。`HYPER_TEXT_WINDOW` 的內容：底圖 1968 (6, 9)、框 1969、`IMAGE_BTN` 1962 (225, 15)、捲軸 1970 (217, 51)、下 2040 (217, 275)、上 2042 (217, 37)、鈕 2044。

| 元件 | 來源 | 值 |
|---|---|---|
| 視窗大小 | 段內 surf 清單的寬高（`sub_571DF0`／`sub_571E20`） | 推定是框 1969 的寬高（依繪製方式對應；兩張 surf 清單各自是哪個指標沒有逐欄核對） |
| 底圖 | 透明度設定 `dword_1372264` 為 31 時用 `sub_6ED600` 畫，其他值用 `sub_7D7740` 半透明畫 | 1968，位置＝視窗原點＋(6, 9) |
| 框 | `sub_542370` 畫在視窗原點 | 1969 |
| 關閉鈕 | **寫死**：`LImageButton` surf **9183** 在 (215, 9)，回呼 `sub_A56AE0`＝切換本窗 | 不用 XML 的 1962 |
| 標題列（拖曳） | **寫死**：`LTitleBar` 矩形 (1, 1, 239, 20)，回呼 `sub_A569C0` 移動視窗 | — |
| 捲軸 | `LScrollBar`（`sub_8FAB00`／FO `0x4F9F00`）：捲軸 1970 (217, 51)、上鈕 2042 (217, 37)、下鈕 2040 (217, 275)、鈕 2044；範圍 0–299，步長 1 | 依 XML |

**繪製（`sub_A55FE0`／FO `0x6553E0`，已驗證）**

- 沒有紀錄：在 (21, 73) 用白色（調色盤 14）畫字串 1889「目前沒有血盟倉庫使用的記錄。」。
- 有紀錄：每筆占 4 行（3 行字＋1 行空白）。畫面一次顯示 16 行：第 r 行在 (21, 44 + 15·r)，白色，捲軸位置（`+88`）是第一行的行號。
  - 第 1 行：`string[1880]`「%s將道具 %s」或 `string[1881]`「%s將金幣 %s」（金幣＝物品名稱等於 `$4`）。參數是角色名稱，以及 `string[1883]`「存放至血盟倉庫中。」（類型 0）或 `string[1882]`「從血盟倉庫領出。」（類型 1）。
  - 第 2 行：`string[1884]`「[%s]x(%d)」（物品名稱展開後、數量），或金幣用 `string[1885]`「[%d] 金幣」。
  - 第 3 行：`string[1886]`「(動作時間已過：%d分)」；分鐘 ≥ 60 時改用 1887（小時）；小時 ≥ 24 時改用 1888（日）。
- 所有座標都是視窗原點＋偏移（加上 `dword_154524C`／`dword_1545250` 的 UI 原點）。
- 按鍵：`sub_8CA740` 讓本窗在一般熱鍵之前吃鍵（vtable `+40`）。

### 5.2 3.81 是否支援

支援：`S_PledgeWarehouseHistory` 的 `D(n)`＋逐筆 `S C S D D`，和 TW16 的 `scsdd` 一致（WIN2§7）。HYPER_TEXT 的 surf 本機都有（同 `HYPER_TEXT_WINDOW`）。9183 在本機 `Sprite*.idx` 有沒有，本輪沒有核對（待驗證，缺圖時不畫）。

### 5.3 R3-C 要改什麼

- `ClanWarehouseHistory.cs` 的註解「0／1 對應存入／領出是推定」改成已驗證：**0＝存放、1＝領出**。
- WinForms 暫行清單可以直接用上面的三行格式與字串 1880–1889（從 `string-c.tbl` 讀，讀不到才用內建文字）。
- 原生版面（`ClanWarehouseHistoryUi` 或之後的原生面板）照 5.1 的表：`HYPER_TEXT_WINDOW` 的底圖、框、捲軸；關閉鈕 9183 (215, 9)；標題列 (1, 1, 239, 20)；16 行、行高 15、從 (21, 44) 起。

## 6. Q3-8 頭目音樂觸發（R3-D）

### 6.1 原版怎麼做（已驗證）

- **登記**：`sub_8F3990(sprite, 模式)`（VA `0x8F3990`／FO `0x4F2D90`）把 `dword_137223C` 設成這個 sprite。背景音樂開啟時（`byte_13D3F8A` 而且 `sub_F7D5C0(3,0)`，後者沒展開，推測是音樂開關），用 `sub_8F16B0(sprite, 模式)` 查曲目。查到而且沒在播時，停掉目前的曲子並播放這首（單次）。
- **查曲目** `sub_8F16B0`（FO `0x4F0AB0`）：在目前地圖、玩家在矩形內的區域裡，找 `SPRITE`（`+28`）等於 sprite 的那筆。
  - 模式 0（出現）：回傳 `SPAWN`（`+32`），並設「已觸發」（`+44`）；已觸發過就不再回傳。
  - 模式 2（死亡）：回傳 `DEATH`（`+40`）。
  - 雙頭目（`TWIN_SPRITE1/2`，`+48/+52`）：任一隻出現時回傳 `SPAWN`（全域只觸發一次 `byte_13D42BE`）；兩隻都死（`byte_13D42BF`、`byte_13D42C8`）才回傳 `DEATH`。
  - 模式 −1／−2 只用來問「這個 sprite 是不是頭目」。
- **呼叫點**：
  - 物件出現 `sub_A50F20`（FO `0x650320`，由物件建立流程呼叫）：sprite 是頭目時呼叫模式 0。`sub_EFE890()` 為真時只有物件不在死亡狀態（描述 `+20 != 8`）才呼叫。
  - S_ACTION（op 226，`sub_92CB70`／FO `0x52BF70`）：動作 **8（死亡）** 而且 gfx 是頭目時，呼叫模式 2。
  - `sub_92C9F0` 是同樣內容的另一份，沒有任何引用。
- **戰鬥曲** `BATTLE`（`+36`）由區域選曲 `sub_8F31F0`（FO `0x4F25F0`）決定：
  - 登記的 sprite 對到區域，而且 `SPAWN`／`BATTLE`／`DEATH` 有一首正在播，就讓它播完。
  - `SPAWN` 播完後，頭目物件還在而且沒死（`sub_8F15B0`：依 sprite 找物件，動作 ≠ 8；雙頭目兩隻都要在）時，改回傳 `BATTLE`。`BATTLE` 播完會再選到 `BATTLE`。
  - 頭目不在了就回到一般 `MUSIC` 清單。

### 6.2 3.81 是否支援

支援：物件出現（S11 CharPack）與死亡動作（S158 DoActionGFX，動作 8＝`ACTION_Die`）3.81 都會送；`music.def` 的 `SPRITE`／`SPAWN`／`BATTLE`／`DEATH`／`TWIN_SPRITE` 欄位引擎已經解析（SD 背景音樂節）。

### 6.3 R3-D 要改什麼

- `BackgroundMusic.RegisterBoss` 改成照原版的兩個事件：
  - **物件出現**（gfx 是區域的頭目 sprite，而且不是死亡狀態）→ 播 `SPAWN`，每個區域只觸發一次；
  - **S158 動作 8** → 播 `DEATH`（雙頭目要兩隻都死）。
- 選曲：`SPAWN` 播完、頭目物件還活著 → `BATTLE`，重複播；頭目離開視野或死亡 → 回到一般曲。原版只記「最後一個登記的 sprite」，引擎現在的 `bossSprites` 集合可以保留，但判斷要以目前區域為準。
- 掛點在 `SoundPackets.cs`／`BackgroundMusic.cs`。物件出現與 S158 的事件要用既有的訂閱，不改 `WorldObjects.cs`。

## 7. Q3-9 聊天第二色 `#BDBDBD`（R3-G）

### 7.1 原版怎麼做（已驗證）

- 血盟 Printer `sub_6BDAA0`（FO `0x2BCEA0`）呼叫 `sub_7DC850(文字, 調色盤[11], 調色盤[7], 4, −1, …)`。第 3 個參數就是第二色 #BDBDBD。
- `sub_7DC850`（FO `0x3DBC50`）把第 3 個參數原樣傳給 `sub_7DCC50`（另外只把文字與第 2 個參數交給 `sub_7DEF20`）。
- `sub_7DCC50`（VA `0x7DCC50`／FO `0x3DC050`）的反組譯只讀 `arg_0`、`arg_4`、`arg_C`、`arg_10`、`arg_14`，**從來不讀 `arg_8`**。它把文字、文字色、類型、旗標交給聊天紀錄 `sub_7F2020`。
- 所以第二色在 TW16 **沒有任何畫面用途**，是殘留參數。一般 Printer `sub_6BD820` 的對話泡泡（`sub_7CE710`）也只用文字色。

### 7.2 3.81 是否支援

和伺服器無關。

### 7.3 R3-G 要改什麼

- `ChatFormat.SecondColor` 不要接到畫面；建議刪掉，或把註解改成「原版 `sub_7DCC50` 不讀第二色（FU4§7），只保留資料」。

## 8. 可以開 code 的項目

| 項目 | 依據 | 對應組 | 檔案（建議） |
|---|---|---|---|
| Tab／Ctrl+I 背包、Ctrl+S 魔法；倉庫沒有熱鍵 | FU4§1 | R3-A | `NativeUi/ItemWindowPanel.cs`、`NativeUi/SpellWindowPanel.cs` |
| 選單 action 4／3／13／1／6／5 的對應；action 7 開統計窗，不要直接登出 | FU4§1 | R3-A | `NativeUi/MenuButtonsPanel.cs`、`NativeUi/UiClientBridge.cs` |
| 酒醉一律全振幅（註解改正） | FU4§2 | R3-H | `TimedBuffs.cs` |
| 隱身可見只看隊伍，拿掉血盟 | FU4§4 | R3-H | `Invisibility.cs` |
| 倉庫紀錄：0＝存放、1＝領出；三行格式；原生版面 | FU4§5 | R3-C | `ClanWarehouseHistory.cs`、`ClanWarehouseHistoryUi.cs` |
| 頭目曲：出現→SPAWN、S158 動作 8→DEATH、SPAWN 播完且頭目在→BATTLE | FU4§6 | R3-D | `BackgroundMusic.cs`、`SoundPackets.cs` |
| 第二色不畫（刪掉或改註解） | FU4§7 | R3-G | `PlayerChat.cs` |

**不開**：光效 +5 的封包觸發（FU4§3，觸發物件沒找到）、`$431` 隱身分支（3.81 不送 S36 類型 3）。

## 9. 未驗證與限制

- 全程靜態分析，沒有實機驗證。3.81 `LiTo.bin` 有殼；熱鍵與選單以 TW13 為準是推測（同一代舊式視窗）。
- Q3-4：`+12 = 1／2` 的 `Effect` 是誰建立的沒有找到；vtable `0x1150D4C` 那個類別沒有追。
- Q3-3：繪圖物件 `dword_168AF18` 的實作類別沒有追（16／32 位元是依呼叫者推定）。
- Q3-5：物品 `+36 == 87` 的意義；TW16 的 `desc` 表是否與本機相同（第 431 行）。
- Q3-6：兩張 surf 清單和底圖／框的對應是依繪製方式推定；9183 本機有沒有沒核對；捲軸範圍 299 是不是隨筆數調整沒追。
- Q3-8：`sub_F7D5C0(3,0)`、`sub_EFE890()` 沒展開。
- TW16 的 Ctrl+H／V／C 接的函式（`sub_8CC330`、`sub_8CC400`、`sub_7F7240`）沒有展開。

## 10. 重跑

腳本在 `rea-scripts/t680/`：

| 腳本 | 用途 |
|---|---|
| `ida_t680.py` | `ida_t665.py` 加上 `findstr TEXT`（在 `.rdata`／`.data`／`.image` 找字串與引用）、`gstores VA`（對全域的 `mov` 寫入點，附前 6 行） |
| `queries.txt` | TW16 q1–q10、TW13 t13a–t13p 的查詢清單 |
| `run_ida13.ps1` | `run_ida.ps1` 的 TW13 版（IDB 放在 `scratch/rea/ida13/tw13.idb`） |
| `t680_strings.py` | `t225_strings.py` 加上環境變數 `TBL`（例如 `TBL=desc-c.tbl`） |
| `t680_tbls.py` | 列出 `Text.pak` 裡的 `.tbl` |
| `t680_html.py` | 讀 `Text.pak` 的 HTML，去掉標籤後寫到 `scratch/rea/html1.txt` |

執行方式同 FU3§10：把腳本與 `run_ida.ps1`、`nat.py` 放進 `scratch/rea/scripts/`，先跑 `subst` 確認代號沒有被占用，再執行 `run_ida.ps1 -Script ida_t680.py -List qN.txt -Out oN -Drive W:`。**不要把輸出接 `Select -First`**。腳本必須是無 BOM 的 UTF-8。
