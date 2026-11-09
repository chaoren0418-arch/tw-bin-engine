# Issue #665（T275）：查證原版——前幾輪留下的未驗證細項

分支 `task/665`。完整內容見 [docs/reference/followups-3.md](../reference/followups-3.md)（FU3）。

## 結論

1. ★ **`SpellList.xml`**（T269／#643）：
   - 查表鍵就是 XML `id`，等於 TW16 技能索引；**3.81 技能編號＝`id`＋1**（核對 20 筆）。`pos` 只是顯示位置。
   - 沒寫屬性時：**delay 10 ms**、direction 0（動作 19）、savelastspell 1、align 1。SS2 推測的「delay 0」不對。
2. ★ **王冠的他人條件 `sub_A42C30`**（T272／#649）：
   - ＝物件 `+85` 隱身旗標。來源是 S_PUT_OBJECT 狀態 bit 0x02 與 S_INVISIBLE（3.81 `STATUS_INVISIBLE`、S171）。
   - 引擎用 `(Flags & 2) == 0 && y > 0`；同隊、同盟看得見的隱身者也不畫王冠。
3. **S36 type 1／5**：
   - type 1：施放傳送 5／69 時開書籤窗 `CBookmarkWindowRenewal`（`BookmarkWindowUI`）選目的地。TW16 送 `H 地圖 H x H y`，3.81 要 `H 地圖 D 書籤 id`，引擎維持 `HD`。
   - type 5：施放 51 時開 `SummonUI`，送 `summonList.xml` 的 `action`；等級不足時顯示 743。3.81 兩者都支援。
4. **盟屋地圖 674 鈕**：送 C125 `D S("select") S(編號)`，3.81 回 S39 `agsel` 拍賣資訊頁。
   - **新物品清單**：S5 建基準；S15 數量高於基準時記差額；S24 回到基準以下時移除；S57 移除。
5. **`QuitWindow`**：只在死亡逾時開，ESC 與選單都不開。
   - S250／192＝角色選項旗標（bit 0 顯示 VIP 標記）。
   - 190 的 512–514＝魔法娃娃狀態。
   - 84 類型 3／4 的宿主＝`RoundScoreUI`。
   - 這三項 3.81 都不送原版格式。
6. **賣出確認清單**：只由 S250 子碼 162 填寫（寵物項圈類，字串 3133）。3.81 不送 162，所以不跳確認。修理上限＝背包物品數量（通常 1）。
7. **`dword_153FCC4`**：運算元、索引陣列、資料指標全掃，沒有間接寫入。TW16 確定丟棄 op 133。

## 做了什麼

1. 把 P01 IDB 複製到 `scratch/rea/ida/tw16.idb`，SHA-256 不變（`3a0bf206…e82f`）。
2. 用 IDA 7.7 唯讀查詢 19 輪，subst 用 `W:`，每輪前後都跑 `subst` 確認。
   - 第 2 輪輸出被截斷，`W:` 殘留，當場用 `subst W: /D` 解除。
3. 新寫 `ida_t665.py`（加上全程式 store／運算元掃描、對呼叫者反編譯再 grep），以及 RTTI、XML 檔名表的小腳本。
4. 用 `wo_sendsites.py` 找 C_QUIT／C_USE_SPELL／書籤／C_HACTION 的送出點。
5. 讀 kor25／ly 的 `SpellList.xml`（T268 已解在 `scratch/`）與本機 `effectlist2.xml`；用 `t225_strings.py` 查字串 846、3133、1143、2847、743、276、278。
6. 對照 3.81 原始碼：`L1SkillId`、`C_UseSkill`、`TELEPORT`、`C_NPCAction`、`Npc_AuctionBoard`、`S_AuctionBoardRead`、`S_PacketBox`。
7. 產出 FU3。回填 SS2、SS、PBW、TWP 的待驗證處，gap-matrix 11.6 與新的 11.10，`INDEX.md` 加一列，新增 `rea-scripts/t665/`。

## 怎麼驗證

- 換算規則逐筆對照 `L1SkillId.java`。
- 王冠條件從 setter 反查到兩個封包來源，再核對 3.81 的狀態常數。
- 書籤／召喚的送包格式與 `C_UseSkill.java:257–288` 逐欄比對；674 鈕與 `C_NPCAction.java:514–558`、`Npc_AuctionBoard.java:63` 比對。
- 新物品清單的兩份 vector 用反組譯的 `add ecx,154h`／`160h` 區分。
- `gap_count_r2.py` 修改後仍是 67／175／48。
- 沒有改產品程式；只改 `docs/`。

## 未驗證什麼

- 全程靜態分析，沒有實機。
- 3.81 `LiTo.bin` 是否讀 `SpellList.xml`（卡在加殼）。
- `byte_13A931C`、技能索引 130 被排除的原因、`sub_A50850` 外型 7138 的模式值都沒追。
- `TradeItem+314` 是 descId，依參數順序推定。
- `BookmarkWindowUI`↔`BookmarkUI.xml` 是推定。
- 新物品清單「名稱＋第 6 參數」分支的參數意義。
- S36 type 3、`sub_A11520` 的 `$` 代換規則、`sub_88EA30` 的格式（不在本任務範圍）。

## 產出路徑

- `docs/reference/followups-3.md`（新）
- `docs/reference/gap-matrix.md`、`INDEX.md`、`send-sites-2.md`、`sprites-and-strings.md`、`packetbox-windows.md`、`trade-window-and-projectiles.md`（回填）
- `docs/reference/rea-scripts/t665/`（新）
- `docs/reports/issue-665.md`
- `scratch/rea/`（IDB 副本、偽碼、XML 副本；不進 git）

## 給下一棒的提示（可以開 code 的項目，FU3§8）

- **T269（#643）／R2-H**：`SpellList.xml` 以 `id+1` 對技能 id，預設 delay 10、動作 19；沒有檔時維持 `ReuseMs`。
- **T272（#649）**：
  - 王冠他人條件 `(Flags & 2) == 0 && y > 0`，自己一律畫。
  - 「獲得道具」照 FU3§4.2。
- **R2-H**：
  - 傳送控制戒指：`AbilityEquipped(1)` 時，施放 5／69 開書籤窗選目的地，送 `HD`；限制地圖顯示 276。
  - 召喚控制戒指：`AbilityEquipped(5)` 時，施放 51 改開既有 `OpenLocalSummon()`。
- **#550（T232）**：674 鈕送 C125 `select`。
- **R2-E**：拿掉賣出確認；修理上限用物品數量。
- **不開**：`QuitWindow` ESC 入口、S250 192／190／84 類型 3／4、血盟推薦 UI。
- **重跑 IDA**：不要把 `run_ida.ps1` 的輸出接 `Select -First`，否則 `finally` 不會解除 subst；`grepfuncs`／`grepcallers` 的 regex 不能含空白。
