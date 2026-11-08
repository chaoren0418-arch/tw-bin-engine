# S250 視窗類子碼（B3）與任務窗、離開窗、通知窗查證（T245／Issue #602）

整理日期：2026-11-09（Asia/Taipei）。範圍：[`protocol-gaps.md`](protocol-gaps.md)（PG）第 2.3 節的 **B3 組 14 個子碼**（37、45、52、66、83、84、86、117、146、168、169、173、190、192），以及 [`windows-2.md`](windows-2.md)（WIN2）、[`effects-2.md`](effects-2.md)（EF2）列為仍查不到的三項：`QuestUI` 的 S250 格式、`QuitWindow` 與 `CNoticeWindow` 的使用點。

每個子碼寫：原版怎麼做／3.81 是否會送／引擎現況／建議的 code 任務。

記號：**已驗證**＝反編譯、反組譯或原始碼直接讀到；**推定**＝依名稱、欄位、行為推論；**待驗證**＝本輪查不到。

## 0. 方法與身分

- **原版：** `TW1608262200.bin`（P01 `input/`），SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`。`.text` FO＝VA − `0x400C00`；`.rdata` FO＝VA − `0x401400`；`.data` FO＝VA − `0x401800`（同 PG§0）。
- **IDB：** P01 `ida/03-ascii-isolated/TW1608262200.idb`（SHA-256 `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f`），複製到本 worktree 的 `scratch/rea/ida/tw16.idb`，複製後雜湊相同。IDA 7.7＋Hex-Rays，唯讀批次 7 輪（q1–q7）。
- **subst：** 執行前 `subst` 無輸出（沒有被占用的代號）。本輪用 `W:`，每輪由 `run_ida.ps1` 的 `finally` 解除；每輪結束後 `subst` 都無輸出。
- **3.81 伺服器：** `D:\lineage開發\1-伺服器\src`，只讀原始碼（`S_PacketBox.java`、`S_PetEquipment`、`S_PacketBoxGm`、`S_PacketBoxGame`、`S_GlobalEffect`、`S_PacketBoxGree`、`S_Pledge`、`S_ItemBoard`、`S_ShowDrop`、`S_ClassicQuest` 與各呼叫點）。「會不會送」以**全原始碼搜尋 `new <類別>(` 或子碼常數的呼叫點**判定。
- **UI 設定：** 本機 `D:\lineage開發\0-客戶端` 的 `uiextra86.xml`（#448 的解密結果，只在 `scratch/` 讀）。`QUIT_WINDOW`、`NOTICE_WINDOW` 用到的 7 個 surf 在本機 `Sprite*.idx` 全部有 `.img`（`t226_surfcheck.py`）。
- **字串表：** 本機 `Text.pak` 的 `string-c.tbl`（3484 筆，作法同 PG§3），只在記憶體解出，本文只引用幾條文字。
- **引擎：** 本分支（`origin/main` `8264d7f3`）的 `engine/source`，只讀，不建置、不執行。
- 沒有啟動遊戲、伺服器或 DB；沒有任何 BIN、pak、XML 或圖檔進 git。

S_EVENT（TW16 op 108＝3.81 S250）的處理函式是 `sub_9357E0`（VA `0x9357E0`／FO `0x534BE0`），讀取函式代號同 PG§2.2（`926C70`／`926B10`＝C、`926BF0`／`926CA0`＝H、`926B80`＝D、`926B40`＝Q、`926C40`＝S）。`sub_9340B0(src, fmt, …)` 是依格式字串一次讀多欄的共用函式。

## 1. 結論摘要

**B3 的 14 個子碼裡，真正「開視窗」的只有 52（料理）、117（血盟倉庫紀錄）、146（師徒，NPC 對話窗 HTML）三個。** 其他是寫入既有視窗的欄位（168、169、173、37）、畫面效果與中央訊息（83、84）、狀態圖示（86、190）、GM 工具（45）、記分板覆蓋層（66）或純資料（192）。

| 子碼 | 原版（TW16）實際做法 | 3.81 會送？ | 引擎 | 建議 |
|---|---|---|---|---|
| 37 寵物裝備 | 讀 `cdcc`，記下寵物 id，送 C_CHECK_INVENTORY（160）要寵物背包；不開窗 | ✅ 穿脫寵物裝備時（`C_UsePetItem`） | 無 | 低：PBW-E（只記 AC，不回送） |
| 45 GM 呼叫 | GM 工具：使用者清單寫進 `bot_list.txt`，並在 `TelTablesWindow` 列出 | ✅ 但只有 GM 指令 | 無 | **不建議** |
| 52 料理窗 | WIN2§7：第二欄 1 開／切換 `CCookWindow`；**第二欄 0 時回送 C_USE_ITEM（208）** | ✅ 料理書 | `Cooking.cs`（W2-F，#559） | 已處理；補「第二欄 0」的差異（第 2.3 節） |
| 66 遊戲資訊 | 小遊戲記分板覆蓋層（HUD 每格繪製），不是視窗 | ❌ `S_PacketBoxGame` 沒有任何呼叫點 | 無 | **不建議** |
| 83 全畫面效果 | `d`：1＝短震動、2＝持續震動開關、3＝煙火、其他＝移除通知項 | ✅ 多處（傳送前、任務、合成等） | 無（`Renderer.ApplyScreenShake` 只有掛點） | **中：PBW-A** |
| 84 綠字訊息 | `cs`：類型 1／2＝畫面中央訊息（7 秒，最多 14 則）；3／4＝另一個數字欄 | ✅ 只送類型 2 | 無 | **中：PBW-A** |
| 86 經驗回報 | 實為**狀態圖示**分派（子類 0x70＝龍之綠寶石圖示 291／292，帶秒數） | ⚠️ 只有測試指令 `L1PacketProbe` 會送 | 無 | **不建議**（屬 B2，平時不送） |
| 117 血盟倉庫紀錄 | WIN2§7：`D(n)`＋`scsdd`×n，0 筆顯示訊息 1889，否則開窗 | ✅ NPC 動作、`L1ActionPc` | 無 | 已有建議 W2-G |
| 146 師徒清單 | NPC 對話窗 HTML 頁 `mentor`（弟子 `scc`×n＋師父 `scc`） | ❌ 建構子沒有呼叫點 | 無 | **不建議** |
| 168 血盟公告 | 寫入 `PledgeUI` 公告欄（`b` 固定 212 bytes） | ✅ 寫公告後回送（`C_PledgeContent` 15） | 無 | 併 W2-H（PBW-C） |
| 169 成員備註 | 更新 `PledgeUI` 成員清單中該成員的備註 | ✅ 寫備註後回送（`C_PledgeContent` 16） | 無 | 併 W2-H（PBW-C） |
| 173 盟徽狀態 | `cc`：類型 1＝設定 `PledgeUI` 的「顯示盟徽」旗標；130＝訊息 3383 | ✅ 登入、建立／加入血盟、`C_Windows` | 無 | 低：PBW-D |
| 190 物品看板 | TW16：`cc`，狀態圖示 512／513／514 開關 | ⚠️ 3.81 是**自訂格式** `H S`，只在 `ClientLogin=2` 送 | 無 | **不建議** |
| 192 掉落顯示 | TW16：`q`，存進角色物件 `+832` | ⚠️ 3.81 是**自訂格式** `C D`，只在 `ClientLogin=2` 送 | 無 | **不建議** |

| 待查項 | 結論 |
|---|---|
| `QuestUI` 的 S250 格式 | **TW16 的 `QuestUI` 不走 S250。** 熱鍵 N 切換顯示，資料來自 protobuf（S_EXTENDED）的 `SC_QUEST_*` 與 `contents\quest-common.bin`。3.81 的 `S_ClassicQuest` 是 S250 子碼 **255**＋`0x36`＋`CQS1` 的**伺服器自訂擴充**，TW16 沒有 case 255。**不建議做原生任務窗**（第 3 節） |
| `QuitWindow` 使用點 | 已找到：`initGame` 建立（`dword_14EBE24`），版面讀 `uiextra86.xml` 第 0 段 `QUIT_WINDOW`；開啟點是**死亡逾時**（WM_TIMER `0x29A`），S_RESURRECT 會關掉它。三鈕：重新開始／離開遊戲／取消（第 4 節）。**可開 PBW-B** |
| `CNoticeWindow` 使用點 | 已找到：`initGame` 建立（`dword_13D43BC`），版面讀 `uiextra86.xml` 第 25 段 `NOTICE_WINDOW`（`SURF_AUCTION_NOTICE_OFF` 3716）；**是「拍賣物品通知」角落圖示，不是登入公告**。沒有找到讓它顯示內容的程式，3.81 也沒有拍賣通知。**不建議做**（第 5 節） |

另外更正 gap-matrix 第 2 節 op 179 列：**S_NEWS（登入公告）不是 `CNoticeWindow`**，而是通用模態訊息框（第 5.3 節）。

## 2. 14 個子碼逐一說明

### 2.1 子碼 37（0x25）GUI_PET_EQUIPMENT

- **原版（已驗證）：** `sub_9357E0` case 0x25：`sub_9340B0(src, "cdcc")`，把其中的 D 存進 `dword_1656934`，再 `sub_9FBC00(fmt, 160, d)` 送 **C_CHECK_INVENTORY（TW16 op 160）**。不開任何視窗。
  - `dword_1656934` 的其他引用者：`sub_949930`（S_RETRIEVE_LIST，FO `0x548D30`）、`sub_98DB60`、`sub_995E50`、`sub_899EC0`、`sub_F3AC80` 等，**推定**是寵物背包（`PetInventoryWindow`）的「目前寵物」。
  - 也就是說，原版把 37 當成「寵物裝備變了，重新要一次寵物背包」，背包內容靠 S_RETRIEVE_LIST 類型 11 回來（WIN2§7）。
- **3.81：** `S_PetEquipment`：`C(250) C(37) C(data) D(petId) C(listNo) C(petAc)`，欄位寬度與 TW16 的 `cdcc` 完全一致。唯一呼叫點 `C_UsePetItem.java:220`（寵物穿脫裝備後）。
  - TW16 會回送的 C_CHECK_INVENTORY 對應 3.81 **C99 `C_Check`**，伺服器只讀 `D` 與剩餘位元組並記 debug log，**不回任何封包**；3.81 也不送 S_RETRIEVE_LIST 類型 11。所以原版這條路在 3.81 下沒有效果。
- **引擎：** 沒接 37。`Pets.cs` 有寵物資料層。
- **建議：** PBW-E（低）：接 S250/37，更新該寵物的 AC 與裝備欄位（`listNo`），**不回送 C99**。檔案：`Pets.cs`（或新檔 `PetEquipment.cs`）＋測試。

### 2.2 子碼 45（0x2D）CALL_SOMETHING

- **原版（已驗證）：** case 0x2D：先清 `TelTablesWindow`（`dword_153CD8C`），讀 `C(n)`，n>0 時以附加模式開 **`bot_list.txt`**，寫入時間與筆數；逐筆以格式 `off_1183B28`＝`"dss"`（VA `0x1183B28`／FO `0xD82728`）讀入，整理後以「`%-10s [%s] %s`」同時寫檔並顯示在 `TelTablesWindow`；最後切換三個 `TelTablesWindow`（`dword_153CD88`／`98`／`90`）。這是 GM／反外掛用的使用者清單。
- **3.81：** `S_PacketBoxGm`：`C(n)`＋n×`D(最後登入秒) S("模式:名稱") S(等級)`，與 TW16 `dss` 一致；只在 GM 指令（`L1Kick`、`L1Recall`、`L1ToPC` 等 9 個）送，並且只在 `srcpc.isGm()` 時寫清單。
- **引擎：** 無。
- **建議：** **不建議補**。GM 工具，原版還會在玩家電腦寫檔；一般驗收用不到。

### 2.3 子碼 52（0x34）COOK_WINDOW

- **原版（已驗證，補 WIN2§7）：** case 0x34 讀 `D(v1) C(v2) C(v3)`，等級＝v3＋1：
  - v2＝1：料理窗 `CCookWindow`（`dword_153F25C`）已開且等級相同 → 關；否則（必要時先關）設等級後開。
  - **v2＝0：不開窗，改送 `sub_9FBC00("cdc", 208, v1, 1)`，即 C_USE_ITEM（TW16 op 208）`D(v1) C(1)`。** WIN2 只寫了開窗分支。
- **3.81：** `S_PacketBoxCooking(value)`：`C(52) C(0xdb) C(0x31) C(0xdf) C(0x02) C(1) C(value)`，前 4 byte 被 TW16 讀成 D，第二欄固定 1，所以 3.81 只會走開窗分支。呼叫點 `Cooking_Book.java:49`。
- **引擎：** `Cooking.cs`（W2-F，#559）已接 S250/52。
- **建議：** 不另開任務。若日後遇到第二欄 0 的封包，依原版應送 C_USE_ITEM（3.81 C164）而不是開窗；3.81 不會送，可只在註解註明。

### 2.4 子碼 66（0x42）GAMEINFO

- **原版（已驗證）：** case 0x42：清空 `unk_15401B8` 的字串清單（`sub_B617C0`），讀 `H(n) H`，`sub_B61A10(0)`，再讀 n 個 `s` 逐筆加入（`sub_B60A60`，FO `0x75FE60`）。同組 case 0x43／0x44／0x45 是倒數與勝者（`"sd"`，勝者是自己時顯示訊息 4619）。
  - 這份清單由每格繪製函式 `sub_F0F0C0`（FO `0xB0E4C0`）在 `byte_15401B5` 為真時以 `sub_B60B50` 畫出，是**小遊戲記分板覆蓋層**，不是視窗。`QuitWindow` 的兩個按鈕在離開前也會清掉它（第 4 節）。
- **3.81：** `S_PacketBoxGame` 寫 `C(n) C(0) C(0) C(0)`＋n 個 `S`（前 2 byte 被 TW16 讀成 `H(n)`，相容）。**但全原始碼沒有任何 `S_PacketBoxGame` 呼叫點，伺服器不會送。**（PG§2.2 的 62 個子碼清單把它算進去，是因為類別有參數化子碼；以呼叫點判定應排除。）
- **引擎：** 無。
- **建議：** **不建議補**。

### 2.5 子碼 83（0x53）GLOBAL_EFFECT／SECRETSTORY_GFX

- **原版（已驗證）：** case 0x53 讀 **`D`**：
  - **1：** `word_154461C = 15`，以 `sub_A231C0(100, sub_EFDFD0)` 起算，之後每 30 ms 減 1（`sub_EFDFD0`，FO `0xAFD3D0`），共約 0.1＋15×0.03＝0.55 秒。**推定**是短暫畫面震動（3.81 註解「01 畫面震動」）。
  - **2：** 切換 `byte_13CE5DE`；開啟時以 50 ms 起動 `sub_F04260`（FO `0xB03660`），依 `dword_1544644 % 12` 查 12 步偏移表寫 `dword_154463C`／`dword_1544640`（−2…+2 像素）。**推定**是持續晃動，再送一次 2 才停。3.81 註解寫「粉紅特效」，與 TW16 不符，以 TW16 為準。
  - **3：** 對畫面上最多 5 個 `+196` 旗標為真的物件，各放一個隨機特效 gfx **2042／2030／2029**（間隔 10 ms）。3.81 註解「煙火」。
  - **其他值：** `sub_A7C3F0(v)`（FO `0x67B7F0`）：在 `dword_153F574` 清單裡找 id＝v 的項目並移除，沒有畫面效果。
- **3.81：** 兩種寫法：
  - `S_PacketBoxGree(int type)`：`C(83) D(type) C0 C0 C0`，寬度與 TW16 一致。呼叫點：`MobTeleport`（傳送前 1）、`Chapter01R`（任務 1、2）。
  - `S_GlobalEffect(trigger)`：`C(83) C(trigger)`，**只有 1 byte**，TW16 讀 D 會吃到填充位元組。呼叫點：`AutoHuntControl`、`WearingArrows`（值 10 等）、`CombindTable`、`WenyangSystem`、`ClassicBagCombineService`、`TimedSpawnController`、GM 指令。這些值多半不是 1–3，在原版只會走「移除通知項」，等於沒效果。
- **引擎：** 沒接。`Renderer.ApplyScreenShake(Graphics, Point)` 已有平移掛點，但沒有呼叫者。
- **建議：** PBW-A（中，與 84 同一任務）：剩餘長度 ≥4 讀 D、否則讀 C；1＝震動約 0.55 秒、2＝持續晃動開關、3＝隨機煙火 gfx、其他忽略。

### 2.6 子碼 84（0x54）GREEN_MESSAGE

- **原版（已驗證）：** case 0x54 讀 `C(type) S(text)`：
  - **1／2：** 文字先經 `sub_91E160`→`sub_A11520`（FO `0x610920`，**推定**是 `$` 字串表代換，與 86 子類 0xCB 用同一函式），再交 `sub_F04180`→`sub_F18F40`（FO `0xB18340`）加入畫面中央訊息佇列：**顯示 7000 ms**，顏色 `dword_1369D70[2]`、`[0]`，第 5 參數＝（type≠1）；佇列滿 14 則時先丟最舊的。
  - **3：** `sub_9DA410(0)`（隱藏某個三元件欄位）；**4：** `sub_9DA2B0(text)`：取前 10 字、`atoi` 後交 `sub_9D97F0` 顯示數字。3／4 的宿主物件待驗證。
- **3.81：** `S_PacketBoxGree(String)`：`C(84) C(2) S(msg)`，**只送類型 2**。呼叫點：`MobTeleport`（「$ 5秒後將會被傳送!」倒數）、`Chapter01R`（任務對白，例如 `"$7647"`）、`GreenBroadcast`（全服廣播）。
- **引擎：** 無。
- **建議：** PBW-A（中）：S250/84 類型 2 → 畫面中央訊息佇列（7 秒、最多 14 則），`$` 開頭的字串依字串表代換（依賴 PG 任務 A 的整張 `string-c.tbl`）；類型 1 同處理，3／4 先忽略。

### 2.7 子碼 86（0x56）BLUE_EXPREPLY

- **原版（已驗證）：** case 0x56 是**狀態圖示分派**：讀 `C(子類) C(開關)`，再依子類讀不同欄位，呼叫 `sub_F05A90(秒或值, 圖示 id, …)` 加圖示或 `sub_F059F0(圖示 id)` 移除。例：子類 0x3E（屬性武器等，圖示 208、103、173、166、101…）、0x70（`C(種類) H(秒)`，種類 2→圖示 291，否則 292）、0x74、0x81、0x8E、0xCB（帶字串，圖示 567）等。PG§2.2 表上的長格式 `cchcccc…` 是所有子類讀取欄位的串接，不是一個封包的格式。
- **3.81：** `BLUE_EXPREPLY`：`C(86) C(0x70) C(1) C(type) H(time)`，正好落在 TW16 子類 0x70，開關＝1，type 2→圖示 291、其他→292。**唯一呼叫點是測試指令 `L1PacketProbe`**，遊戲流程不送。
- **引擎：** 無；這是圖示，應屬 B2（`PacketBoxStatus.cs`），不是 B3。
- **建議：** **不建議補**（平時不送）。若之後要做，加進 `PacketBoxStatus.cs` 的子碼表即可。

### 2.8 子碼 117（0x75）HTML_CLAN_WARHOUSE_RECORD

- **原版（已驗證，同 WIN2§7）：** case 0x75：清空 `dword_153D218+24` 的清單，讀 `D(n)`，逐筆 `"scsdd"`（名稱為 `$4` 時記旗標，名稱經 `sub_A11470` 轉換）；n≤0 顯示訊息 1889「目前沒有血盟倉庫使用的記錄。」，否則 `sub_A59AB0` 開窗。視窗由 `sub_A559D0` 建立（WIN2§7）。
- **3.81：** `S_PledgeWarehouseHistory`：`D(n)`＋`S C S D D`，完全一致。呼叫點 `C_NPCAction.java:1284`、`L1ActionPc.java:400`。
- **引擎：** 沒接（沒有 `ClanWarehouseHistory.cs`）。
- **建議：** 沿用 WIN2 的 **W2-G**，不另開。

### 2.9 子碼 146（0x92）APPRENTICE_HTML

- **原版（已驗證）：** case 0x92：讀 `C(n)`，逐筆以 `off_1184F98`＝`"scc"`（VA `0x1184F98`／FO `0xD83B98`）讀名稱、等級、職業，組成「`%s(Lv:%d)[%s]`」；再讀師父 `s c c`。把師父名稱、人數、弟子字串寫進 NPC 對話窗（`dword_13CD834`）並以 HTML 頁 **`mentor`** 開啟。
- **3.81：** `S_PacketBox(master, totalList)`：`C(146) C(n)`＋n×`S C C`＋`S C C`，完全一致。**但全原始碼沒有呼叫這個建構子**（`APPRENTICE` 只在 `S_PacketBox.java` 與 `C_Title.java` 的註解出現），伺服器不會送。
- **引擎：** 無。
- **建議：** **不建議補**。

### 2.10 子碼 168（0xA8）HTML_PLEDGE_REALEASE_ANNOUNCE

- **原版（已驗證）：** `sub_B2EF40`（VA `0xB2EF40`／FO `0x72E340`，log 名「RecvPledgeNoti」）：`sub_9340B0(src, "b", 212)` 讀**固定 212 bytes**，寫進 `PledgeUI` 第 5 頁第 0 個元件的文字（`this+382`）並設為可見。不開窗，只更新已存在的血盟窗公告欄。
- **3.81：** `C(168) S(announce)`（NUL 結尾，長度可變；公告上限 160 字）。呼叫點 `C_PledgeContent.java:163`（類型 15 寫公告成功後回給本人）。TW16 讀 212 bytes 會多讀封包尾端，但文字以 NUL 結束，顯示結果相同（**推定**）。
- **引擎：** 無。
- **建議：** 併入 **W2-H 血盟成員窗**（PBW-C）：S250/168 → 更新公告文字，以 3.81 的 `S` 讀。

### 2.11 子碼 169（0xA9）HTML_PLEDGE_WRITE_NOTES

- **原版（已驗證）：** `sub_B2EE50`（FO `0x72E250`，「RecvPledgeMemberMemo」）：讀 `"sb"`（名稱最長 13＋備註 62 bytes），在 `PledgeUI` 成員清單（`this+229`）找同名成員，用 `sub_B28830` 更新備註，再 `sub_B30240` 重畫。
- **3.81：** `S_Pledge(name, notes)`：`C(169) S(name)`＋**固定 62 bytes**（Big5），一致。呼叫點 `C_PledgeContent.java:176`（類型 16）。
- **引擎：** 無。
- **建議：** 併入 W2-H（PBW-C），WIN2§3 已列。

### 2.12 子碼 173（0xAD）PLEDGE_EMBLEM_STATUS

- **原版（已驗證）：** case 0xAD：`sub_C15540`（FO `0x814940`）讀兩個 byte，第一個是類型、第二個是值：
  - 類型 1：`sub_B30990(值≠0)`（FO `0x72FD90`），寫 `PledgeUI+1220`，**推定**是血盟窗的「顯示盟徽」勾選狀態。
  - 類型 130：顯示訊息 3383「血盟注視：戰爭狀態中無法使用」。
- **3.81：** `C(173) C(1) C(showEmblem?1:0) D(0)`。呼叫點：`C_LoginToServer.java:693`（登入）、`C_CreateClan.java:116`、`C_Attr.java:390`（加入血盟）、`C_Windows.java:185`（切換）。
- **引擎：** 無（`ClanEmblem.cs` 只處理盟徽圖）。
- **建議：** PBW-D（低）：存「顯示盟徽」旗標，供血盟窗勾選框與名字旁盟徽顯示使用；類型 130 顯示訊息 3383。檔案：`Clan.cs` 或 `ClanEmblem.cs`＋測試。

### 2.13 子碼 190（0xBE）與 192（0xC0）

- **原版（已驗證）：**
  - 190：讀 `C(種類) C(開關)`，種類 0x20／0x40／0x60 → 狀態圖示 513／512／514 加上或移除（`sub_F05A90(-1, …)`／`sub_F059F0`）。**是圖示，不是物品看板。**
  - 192：讀 `Q`（8 bytes），存到角色物件 `sub_4F9B90()+832`；讀取者本輪沒追（待驗證）。
- **3.81：** 這兩個是**伺服器自訂**：`ClientLootNotifier` 的註解寫明「PACKETBOX 190/192 不是公版協定」，只在 `ClientLogin=2`（`ConfigClient.isROClient()`）時送：190＝`S_ItemBoard` `H(gfx) S(物品名)`（撿到物品），192＝`S_ShowDrop` `C(0 經驗／1 金幣) D(數量)`。格式與 TW16 完全不同。
- **引擎：** 無。
- **建議：** **不建議以原版名義實作**。它們不是 TW16 的行為；P24Lab 的 `ClientLogin` 設定值本輪沒查（設定檔屬禁止讀改範圍），是否會收到**待驗證**。若收到，引擎應安靜消費、不要誤解成圖示。

## 3. `QuestUI`：不走 S250

**原版（已驗證）：**
- 全域 `dword_13AA8D8`（`CQuestUI`，建構子 `0x698130`／FO `0x297530`，gap-matrix 第 4 節）的引用者共 100 多處，集中在 `0x621760`–`0x69B754` 的任務系統程式區、`sub_B56A40`（XML 工廠）、`sub_8C1290`、`sub_8DCAC0`、`sub_A67360`、`sub_A6D0F0`。**沒有一處在 `sub_9357E0`（S_EVENT）裡。**
- **熱鍵：** `sub_8C1290`（VA `0x8C1290`／FO `0x4C0690`）在旗標 `dword_13CE3E0 & 0x4000` 時，字元 `N`／`n`（78／110）切換 `QuestUI` 顯示（vtable `+72` 讀狀態、`+68` 設定）。
- **資料來源：** BIN 內嵌 protobuf 描述有 `CS_QUEST_PROGRESS_REQ`／`SC_QUEST_PROGRESS_ACK`／`SC_QUEST_PROGRESS_UPDATE_NOTI`／`SC_QUEST_START_ACK`／`SC_QUEST_FINISH_ACK`／`SC_QUEST_FINISH_NOTI`／`SC_QUEST_TELEPORT_ACK`／`SC_QUEST_SHOW_IN_WINDOW_ACK` 等（FO `0xDBBE8A`–`0xDBC39F`），任務定義檔 `contents\quest-common.bin`（FO `0xD632A8`）與 `.LIN.QuestT…` 結構。也就是 PG§2.1 的 S_EXTENDED（op 19／39／109）protobuf 通道。

**3.81（已驗證）：**
- `S_ClassicQuest`：`C(250) C(255) C(0x36) D(0x31535143 'CQS1') C(2) C(command)`＋nonce、玩家、token、NPC、頁、總數、選取、狀態、權限、最多 12 列任務（`D C 文字`）、獎勵（`D H D 文字`）、說明行；文字是 `H(長度)`＋UTF-8。
- 客戶端請求是 C254 `0x63 0x02`＋6 個 D（`ClassicQuestProtocol.decode`）。
- 只在資料庫開關啟用且 `Config.CLIENT_LOGIN_TYPE == 2` 時送（`ClassicQuestService.java:50`）。
- TW16 的 S_EVENT 沒有 case 255（PG§2.2），收到會丟棄。

**結論：** 這是 3.81 伺服器專為自訂客戶端加的協定，跟原版 `QuestUI` 的資料流（protobuf）、版面（`QuestInfo.xml`）都無關。引擎沒有任何任務窗程式。**不建議做原生任務窗**；若要支援 `S_ClassicQuest`，屬自訂功能，需使用者決定。

## 4. `QuitWindow`：死亡逾時後的「重新開始／離開遊戲」窗

**建立與版面（已驗證）：**
- RTTI type descriptor `0x13767A8`（名稱字串在 `0x13767B0`）→ COL `0x122CEC4` → vtable **`0x118702C`**（FO `0xD85C2C`）。WIN2§7 寫「vtable 沒有程式引用」，是因為當時拿字串位址當 TD；改用 TD 後，vtable 的引用者是建構子 `sub_980580` 與解構子 `sub_980B20`。
- `initGame`（`sub_8C7C80`）在 `0x8C8561`（FO `0x4C7961`）呼叫 `sub_980580(22)`（FO `0x57F980`），結果存 **`dword_14EBE24`**。
- 版面來自 `dword_153F598+48` 的第 0 段（`sub_980C80` 繪製時同樣取第 0 段）。`uiextra86.xml` 的段落順序第 0 段正是 `QUIT_WINDOW`（第 25 段是 `NOTICE_WINDOW`，與第 5 節吻合），所以是 **`QUIT_WINDOW`**（推定，依段落順序兩處吻合）：
  - 底 1951 (4, 8)、框 1952、關閉 1962 (147, 15)
  - 重新開始 1953 (31, 41)、離開遊戲 1956 (31, 62)、取消 1959 (31, 83)
  - 7 個 surf 本機都有 `.img`
- 視窗位置：建構子最後以 `(400 − 寬/2, 150)` 平移，即 800×600 畫面水平置中、y＝150。滑鼠判定區 130×113（`sub_980E90`）；拖曳條（surf 160 的寬度）由 `sub_980F70` 移動視窗。

**按鈕（已驗證）：**

| 元件 | 處理函式 | 行為 |
|---|---|---|
| 重新開始 | `sub_981490`（FO `0x580890`） | 有攔截條件時顯示訊息 4449（≥3484，本機字串表沒有）並返回。死亡狀態（`byte_13CE3FD`）：關死亡窗 `dword_1540880`、送 **C_DEAD_RESTART（58）**。存活：死亡、安全區旗標或離上次戰鬥 ≥15 秒才允許，否則訊息 130「戰鬥中，無法重新開始。」；允許時清除各狀態、送 **C_RESTART（194）**，回選角 |
| 離開遊戲 | `sub_981150`（FO `0x580550`） | 同樣的 15 秒戰鬥檢查（否則訊息 131「戰鬥中無法離開遊戲。」）；送 **C_QUIT（68）**，清理後 `exit(0)`（`byte_136EB72` 為 0 時） |
| 取消、關閉 | `sub_A59AD0` | 關窗 |

**開啟點（已驗證）：**
- 全域 `dword_14EBE24` 的引用者只有：`sub_8BD990`（主視窗 WndProc）、`sub_8C32B0`（離開世界清理）、`sub_8C4EE0`、`sub_8C7C80`、`sub_8CB760`（視窗清單）、`sub_9496B0`、`sub_980F70`、`sub_A57C60`（視窗堆疊）。
- **唯一的開窗呼叫**在 WndProc 的 WM_TIMER，計時器 id `0x29A`（`0x8BF027`–`0x8BF052`）：仍是死亡狀態時送 **C_QUIT（68）**、關掉死亡窗與 `UserObtainUI`、顯示訊息 47「斷線。」，再開 `QuitWindow`。
- 計時器由死亡處理 `sub_8C4EE0`（FO `0x4C42E0`，呼叫點 `0xA4B5C3`）設定：**501,264 ms**；地圖 1017–1023 是 **1,801,234 ms**。這就是 WO 第 6 節的「約 8 分 21 秒逾時 C_QUIT」。同一函式在 `QuitWindow` 已開時不再開死亡窗。
- S_RESURRECT（`sub_9496B0`／FO `0x548AB0`）：自己復活時，若 `QuitWindow` 開著就關掉。
- ESC 或選單是否也開它：第一層引用沒有找到（待驗證；可能經視窗清單間接呼叫）。

**3.81 對應：** C_RESTART（194）＝C7、C_DEAD_RESTART（58）＝C177、C_QUIT（68）＝C122（gap-matrix 第 3 節）。

**引擎現況：** `Recovery.cs` 已依 WO§6 在逾時後送 C122 並關閉連線（`DeathTimeoutRules`）；`CharacterManagement.cs` 有 C7 回選角。沒有「離開遊戲」窗，逾時後直接斷線。

**建議 PBW-B（中）：原生 `QUIT_WINDOW`。**
- 版面：`uiextra86.xml` `QUIT_WINDOW`，置中 y＝150。
- 按鈕：重新開始（死亡→C177；存活→C7，戰鬥 15 秒內顯示訊息 130）、離開遊戲（C122，戰鬥中顯示訊息 131）、取消。
- 死亡逾時後改為開此窗並顯示訊息 47，不直接關閉程式。
- 檔案：新檔 `NativeUi/QuitWindowPanel.cs`（依賴 T190），規則放 `Recovery.cs`／`CharacterManagement.cs` 的既有入口（只呼叫，不改協定）。

## 5. `CNoticeWindow`：拍賣物品通知圖示（不是登入公告）

**建立與版面（已驗證）：**
- RTTI TD `0x13725F8`（字串 `0x1372600`）→ COL `0x122B830` → vtable **`0x1181F7C`**（FO `0xD80B7C`）。
- `initGame` 在 `0x8C92CB`（FO `0x4C86CB`）呼叫建構子 `sub_909050`（FO `0x508450`），結果存 **`dword_13D43BC`**。
- 版面：第 25 段（反組譯 `0x9090F0`：`ecx = dword_153F598 + 0x30`，`sub_7D7650`）＝`uiextra86.xml` **`NOTICE_WINDOW`**：`SURF_AUCTION_NOTICE_OFF` 3716 (618, 442)。
- 另有兩個寫死的元件：surf **4621** 在 (580, 441)（按鈕，回呼 `sub_909520` 是空函式）、surf **4862** 在 (580, 447)（圖片）。兩者建構時都設為隱藏。
- 繪製 `sub_909550`（vtable +8，FO `0x508950`）：滑鼠停在元件上時畫提示框，文字是 BIN 內嵌的**韓文** CP949 字串「경매 물품 알림(%d초 경과)」／「(%d분 경과)」（VA `0x1181F4C`／`0x1181F30`，FO `0xD80B4C`／`0xD80B30`），意思是「拍賣物品通知（經過 %d 秒／分）」。

**使用點（已驗證）：**
- `dword_13D43BC` 的引用者只有：`sub_8C7C80`（建立）、`sub_8C32B0`（清理）、`sub_8CAB40`（滑鼠事件，vtable +44）、`sub_8CB760`（視窗清單）、`sub_F0F0C0`（每格繪製，vtable +8）。
- 沒有任何程式把兩個元件設為可見，也沒有收包函式寫入它的時間欄位（`this+52`）。在 TW16 本身就**推定**是沒有作用的韓版遺留。

**3.81：** 沒有拍賣通知封包。**引擎：** 無。**建議：** **不建議做。**

### 5.3 S_NEWS（op 179）的實際顯示（更正 gap-matrix 第 2 節）

- `sub_ECAA10`（FO `0xAC9E10`）：
  - XML UI 可用時（`sub_B556D0`），以通用模態訊息框 `sub_8E1E30(公告文字, 65, sub_ECA950)` 顯示；否則交 `sub_EEB810`。
  - 不論哪條路，都會把公告另存成 `<程式目錄>Capture\NewsNNNN.txt`。
  - **與 `CNoticeWindow` 無關。** 確認鈕回呼 `sub_ECA950` **推定**送 C_READ_NEWS。
- 引擎收到 S48 後自動回 C16、不顯示。若要補，應做「模態訊息框＋確定」，不必存檔。

## 6. 建議的 code 任務

| 代號 | 目標 | 檔案 | 依賴 | 優先 |
|---|---|---|---|---|
| PBW-A | S250/84 類型 2 → 畫面中央訊息佇列（7 秒、最多 14 則、`$` 字串表代換）；S250/83 → 1 短震動、2 持續晃動開關、3 隨機煙火 gfx 2042／2030／2029（D 或 C 寬度都接受） | 新檔 `PacketBoxScreen.cs`、`PacketBoxScreenTests.cs`；畫面掛 `Renderer.ApplyScreenShake` 與既有特效播放 | PG 任務 A（字串表，只影響 `$` 代換） | 中 |
| PBW-B | 原生「離開遊戲」窗 `QUIT_WINDOW`：重新開始（C177／C7）、離開（C122）、取消；死亡逾時改開此窗＋訊息 47 | 新檔 `NativeUi/QuitWindowPanel.cs`；呼叫 `Recovery.cs`、`CharacterManagement.cs` 既有入口 | T190 | 中 |
| PBW-C | 併入 W2-H：S250/168 更新公告、169 更新成員備註 | `NativeUi/PledgePanel.cs`（W2-H） | W2-H | 中（隨 W2-H） |
| PBW-D | S250/173 存「顯示盟徽」旗標；類型 130 顯示訊息 3383 | `Clan.cs` 或 `ClanEmblem.cs`＋測試 | 無 | 低 |
| PBW-E | S250/37 更新寵物 AC 與裝備欄，不回送 C99 | `Pets.cs`（或新檔 `PetEquipment.cs`）＋測試 | 無 | 低 |

已有、不另開：52（`Cooking.cs`，W2-F）、117（W2-G）。

**不建議：** 45（GM 工具）、66（伺服器不送）、86（只有測試指令送；屬 B2 圖示）、146（伺服器不送）、190／192（伺服器自訂格式，非原版）、`QuestUI`／`S_ClassicQuest`（原版走 protobuf；3.81 是自訂擴充）、`CNoticeWindow`（拍賣通知，3.81 無）。

## 7. 未驗證與限制

- 全部是靜態分析，沒有實機觀察。83 的「震動／晃動」是依計數器與偏移表推定，沒看到畫面。
- 第 4 節版面對應依「`dword_153F598+48` 的段索引＝`uiextra86.xml` 段落順序」推定（第 0 段、第 25 段兩處吻合）；`dword_153F598` 的寫入者本輪沒追。
- `QuitWindow` 是否也由 ESC 或選單開啟，待驗證（只看了第一層引用）。
- 84 類型 3／4 的宿主物件、`sub_A11520` 的 `$` 代換規則、192 的讀取者、190 圖示 512–514 的意義，待驗證。
- 「3.81 會不會送」以原始碼呼叫點判定；以反射或設定組出的呼叫可能漏掉。`ClientLogin` 的實際設定值沒有讀（設定檔不可碰）。
- 字串 4449、4619 的 id ≥3484，本機字串表沒有，依 PG§3 原版不顯示。

## 8. 可重跑腳本

| 腳本 | 用途 | 執行 |
|---|---|---|
| `rea-scripts/t245/ida_t245_rtti.py` | IDAPython：RTTI TD → COL → vtable → 引用函式與其呼叫者，並反編譯（唯讀） | `run_ida.ps1 -Script ida_t245_rtti.py -List lists/t245_rtti.txt -Out q3 -Drive <未占用代號>`，清單內容 `13767A8`、`13725F8` |
| `rea-scripts/t245/queries.txt` | 本輪 `ida_dis.py` 的 q1–q7 查詢清單（`dec`／`xdec`／`dis`） | 每段存成一個清單檔，`-Script ida_dis.py` |
| `rea-scripts/t245/strs.py` | 在 BIN 中搜尋 ASCII 字串（例如 `quest`、`SC_QUEST`） | `py -3 -I strs.py <TW1608262200.bin> "(?i)quest"` |

既有腳本沿用：`run_ida.ps1`、`ida_dis.py`、`t225_strings.py`、`t226/t226_surfcheck.py`。
