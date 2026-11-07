# 盟徽查詢與 ID 對應（T135，Issue #316）

依據：唯讀查閱 `D:\lineage開發\1-伺服器\src`，並以 `javap -p -c` 與記憶體內 class 符號掃描核對 d287 JAR（`environments/p24/server/Server_Game.jar`，SHA-256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`，2740 個 class）。沒有執行 JAR、解壓、複製伺服器程式碼，也沒有查詢 DB。D／H 是 little endian，S 是 Big5 NUL 字串，傳輸沿用既有零填補。

本文取代 `docs/protocol/clan.md` 中「C72 未接自動查詢」與「快取依 WorldEpoch 失效」兩處描述（僅限盟徽部分）。

## 結論

1. **clan ID 與 emblem ID 是兩個獨立的數字空間，不可互換。** clan ID 是 `L1Clan._clanId`（DB `clan_data`），emblem ID 是 `L1Clan._emblemId`。
2. **d287 的 emblem ID 恆為 0。** 所以 C72 在 d287 上不會被觸發，**其他血盟真實玩家的盟徽在 d287 上看不到**。這是伺服器的限制，客戶端不繞過。
3. d287 能顯示的盟徽全部走 **S87 clan ID ＋ S118 clan ID 推送**，見下方「發布流程」。
4. **S118 的 D 有兩種語意**，封包本身無法區分，客戶端只能靠「是否有未結案的 C72」判斷。

## 各欄位的 ID 語意

| 封包 | 欄位 | 內容 | 證據 |
|---|---|---|---|
| S87 `S_OwnCharPack`／`S_OtherCharPacks` | 名稱後的 D | **clan ID**（`pc.getClanid()`；隱藏身分時為 0） | src 第 101／94 行 |
| S72 `S_ClanUpdate` | `D(objid) S(盟名) S(封號) D(0) C(階級)` | 第二個 D 兩個建構子都寫死 0，**不是任何 ID**。建構子收到的 `clanid` 參數沒有寫入封包 | src、#132 |
| S167 `S_Pledge(int)` | 盟主名後的 D | **emblem ID**（`clan.getEmblemId()`） | src 第 83 行 |
| S64/0x3c `S_CharReset(int,int)` | `C(64) C(0x3c) D(角色 ID) D(值)`，共 10 bytes | 註解為「給予角色盟徽編號」，但**呼叫端混用**，見下表 | src、JAR `(II)V` |
| C72 `C_Clan` | `C(72) D` | **emblem ID**。以 `clan.getEmblemId()==D` 線性比對 | src 第 117 行 |
| S118 `S_Emblem(L1EmblemIcon)` | D ＋ 圖像 bytes | **clan ID**（`emblemIcon.get_clanid()`） | src 第 34 行 |
| S118 `S_Emblem(int,byte[])` | D ＋ 圖像 bytes | **C72 請求的 emblem ID**（`C_Clan` 原樣帶回） | src 第 129 行 |

`S_PacketBoxPledgeRecommend` 也帶 `getEmblemId()`，但客戶端尚未實作血盟推薦。

### S64/0x3c 的呼叫端（JAR 位元碼已逐一核對）

| 呼叫處 | 角色 ID | 值 | 說明 |
|---|---|---|---|
| `C_Attr` 入盟 | 加入者 | `getClanid()` | **clan ID 被放進 emblem 欄位** |
| `C_Attr` 入盟迴圈 | 加入者 | `getEmblemId()` | 送給所有線上成員。`addMemberName` 在迴圈前，所以加入者自己也會收到，上一列的值隨即被 0 蓋掉 |
| `C_Attr` 入盟迴圈 | 各線上成員 | `getEmblemId()` | 廣播給周圍玩家 |
| `C_LeaveClan` | 成員 | 0 | 解散時清除 |
| `C_BanClan` | **驅逐者 `pc`**（不是被驅逐者 `tempPc`） | 0 | 伺服器看起來是筆誤。客戶端照封包處理：只撤銷該角色的 emblem 發布，不影響 clan ID 路徑 |

`S_CharReset` 只在上述事件發送，**登入與進入視野時不會發送**。

## emblem ID 的初始化

- `L1Clan.setEmblemId` 符號只出現在 `L1Clan.class`，全 JAR 沒有呼叫者。
- `_emblemId` 欄位符號同樣只在 `L1Clan.class`，位元碼只有一個 `getfield`（getter）與一個 `putfield`（setter）。全 JAR 的 `getDeclaredField` 只在 `PacketHcBackpressureProbe.class`，與血盟無關。
- `ClanPersistenceService` 載入血盟時沒有設定它；`ClanEmblemPersistenceService` 只讀 `clan_id`、`emblemicon`、`update`。`emblem_id` 字串只出現在 `CrossMergeSchema` 的欄位清單，沒有讀回 `L1Clan`。

因此 `getEmblemId()` 恆為預設值 0（靜態結論，外部程式碼或執行期代理未排除）。

**C72 絕對不可送 0**：`C_Clan` 以 `getEmblemId()==0` 比對時會命中第一個血盟，回傳與查詢者無關的圖像。`ClanProtocol.Encode` 拒絕 0，S118 解碼也拒絕 ID 0。

## d287 的發布流程

| 時機 | 封包 | 收件者 |
|---|---|---|
| 登入 `C_LoginToServer.war` | S118（clan ID） | 自己，**只送自己血盟，只送一次** |
| 盟主上傳 `C_Emblem` | S118（clan ID） | 線上成員及其視野內玩家 |
| 假人進入視野 `L1DeInstance.onPerceive` | S118（clan ID） | 觀察者 |
| 客戶端查詢 C72 | S118（emblem ID） | 查詢者；**找不到時伺服器完全不回應** |

真實玩家進入視野時沒有推送，所以看不到其他血盟的盟徽，除非當下有人上傳。

## 客戶端實作（`engine/source/ClanEmblem.cs`）

**兩個分開的快取**：`clanKeyedEmblems`（S118 為 clan ID）與 `emblemKeyedEmblems`（S118 為 emblem ID）。相同數字不會互相覆蓋或誤中。

**S118 分類**：D 等於一個回覆期限內（5 秒）的未結案 C72 時才算 emblem ID，並結案；其他一律算 clan ID 推送。

**ID 對應**：來源只有 S167（目前血盟）與 S64/0x3c，記為「角色 → (emblem ID, 當時盟名)」。值為 0 就撤銷。角色不在視野或沒有盟名時不記錄。`FindClanEmblem(clanId)` 先查 clan 快取，再找「S87 ClanId 等於 clanId、盟名與發布時相同」的角色所發布的 emblem ID，兩者都有時取**最新收到**的（上傳後的 clan 推送會蓋過舊的 C72 圖像）。盟名不符的發布自動失效，換盟不需要額外清除。

**查詢時機**：`PumpWorldAsync` 每 250 ms 檢查一次，每次最多送一個 C72。條件是有視野內角色發布了非零 emblem ID、該圖不在快取、本次連線沒查過，而且已進入世界且不在傳送中。不要求活著，因為 `C_Clan` 的死亡與傳送檢查已被註解掉；傳送中不送是客戶端自己的保守選擇。**S87 的 clan ID 不會觸發 C72**，否則就是把 clan ID 當 emblem ID。

**查一次**：送出時才記錄為已查（閘門拒絕的不算，下次重試）。回覆、未回覆、無法解析都不重查，本次連線內有效。C72 排隊期間若 ID 被撤銷、斷線或離開世界，不會送出。

**快取壽命**：到 `WorldSession.Reset` 為止（重新選角、重連），不再隨 WorldEpoch 清除。因為 S118 只在登入送一次，T128 的做法會讓盟徽在第一次傳送後永久消失。

**上限**：圖像各 256 枚、角色發布 256 筆、查詢 256 次，防止伺服器資料無限成長。

**不完整的 S64/0x3c**（少於 10 bytes）維持原本的未處理計數，不中斷連線。完整但角色 ID 為 0 視為協定錯誤，與 S72 一致。

## 已知限制

- **已驗證的伺服器限制**：d287 上其他血盟真實玩家不顯示盟徽（結論 2）。若要顯示，需要伺服器在 `L1Clan` 初始化 emblem ID，或在角色進入視野時推送 S118。這需要伺服器維護者裁定，客戶端不改伺服器。
- **S72 入盟後的空窗**：S72 不帶 clan ID，T128 會把 ClanId 清為 0，直到下一次 S87（通常是切圖）才恢復。這段期間新成員的名稱旁不顯示盟徽。`ClanUi` 的掛載點只傳 clan ID，所以即使 S64/0x3c 已發布也無法在這段期間對應；要改善需要修改任務範圍外的 `ClanUi.cs`。
- **S118 分類的模糊區**：未結案 C72 的 ID 恰好與某 clan ID 相同，且該血盟同時上傳盟徽時，圖像會被歸到 emblem 快取。超過 5 秒才到的 C72 回覆會被歸到 clan 快取。兩者都只會少顯示或顯示在錯的 ID 上，封包無法再細分。
- **重新上傳**：C72 取得的圖像不會被主動更新，但上傳的 clan 推送在同時知道兩個 ID 時會優先顯示。
- 盟徽識別開關 S250/173（`PLEDGE_EMBLEM_STATUS`，`getShowEmblem`）尚未接上，客戶端不依它隱藏盟徽。
- S167 只在使用者按「查詢成員」時才會收到，本任務沒有新增自動 C68。

## 未驗證

- **真實盟徽 bytes 的格式。** T128 的解碼器只接受 16×12 BMP，伺服器對上傳內容不驗證，`clan_emblem` 的實際內容未讀取（DB 不可查）。**推測**：舊版 L1J 系列的盟徽檔常是不含 BMP 檔頭的 384 bytes（16×12×2）。若 P24Lab 的資料是這種格式，S118 會全部落到「無法解析」，名稱保留但不顯示圖像。**這是真機最優先要看的項目。**
- 未在真實連線下驗證封包順序、S118 回覆延遲，以及 `ClanUi` 實際繪製結果。
- `getEmblemId()` 恆為 0 是靜態結論，未在執行中的伺服器觀察。

## 唯讀來源 SHA-256

路徑相對於 `src/com/lineage/server`。已列於 `clan.md` 的（`C_Clan`、`S_Emblem`、`S_ClanUpdate`、`S_Pledge`、`S_OtherCharPacks`、`L1Clan`、`C_BanClan`、`C_LeaveClan`）不重複。

| 路徑 | SHA-256 |
|---|---|
| clientpackets/C_Attr.java | 793a83eae9e44870e4ff80561f78bbeabff453fffc96886f097fd2d991d86d23 |
| clientpackets/C_Emblem.java | 4ea6cd5b26c72c4ac40d9dfb88a772352c0f9bbdc00c1af87930fa59e8c84415 |
| clientpackets/C_LoginToServer.java | c2eedcb6d78d9da7f33af485c596d393d49818a0a05bc5a37cbedb79bcfc9de4 |
| model/Instance/L1DeInstance.java | 1405e5e2686cc2a8a06763ca6f4c8190801be3fcbf69f3ecbf44029423ac0014 |
| model/ClanEmblemPersistenceService.java | 674dbf3727fb1faff770f7bd0115c5377f66e31a87ca0295189763e8a8bd1488 |
| model/ClanPersistenceService.java | 0cf937e843925d9ebddba122c08938db619785e6a97ff7603644a9b8615ab3b6 |
| serverpackets/S_CharReset.java | c1ae26bf59405be0d11ae4b2fae2ce63d28281ead804705f2effb8acbd4e3138 |
| serverpackets/S_OwnCharPack.java | 6925f701e7d0245e2ba41bf0e2892fa9d8a1c786d6ebe6d87363e77004020f26 |
