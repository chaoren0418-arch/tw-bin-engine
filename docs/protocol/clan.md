# T128 血盟封包補充（Issue #262）

本次以 `docs/reference/server-protocol.md`、`server-rules.md` 為基礎，唯讀核對 `D:\lineage開發\1-伺服器\src\com\lineage\server`。沒有複製伺服器程式碼、讀取設定或連接服務。以下均為明文 opcode；D/H 是 little endian，S 是 Big5 NUL 字串。傳輸沿用既有 8-byte 零填補。

## 已實作的格式

| 方向 | 格式與語義 |
|---|---|
| C222 | S 血盟名稱；客戶端限制最多 16 字、32 Big5 bytes。職業、重名及建立資格由伺服器決定。 |
| C194 | 無欄位。伺服器依 FaceToFace 找招募者；不是物件 ID 封包。客戶端要求選取相鄰且面對面的角色，排隊期間重新核對雙方位置。 |
| C61 | 無欄位。一般成員退出；盟主可能觸發解散，UI 有確認。 |
| C69 | S 成員名稱。UI 有確認；權限與實際移除由伺服器決定。 |
| C72 | D **emblem ID**。提供編碼器與送出 API；目前沒有可靠自動查詢來源，未接自動查詢。 |
| C68 | 無欄位。可送出查詢，成員資料回包仍待擴充分派範圍。 |
| S72 | D 角色 ID、S 血盟名稱、S 封號、D reserved、C 階級。兩個建構子皆把 reserved 寫成零；不是 clan ID。空名稱表示清除血盟。 |
| S118 | D 識別碼、其餘為圖像 bytes。沒有額外長度欄位；BMP 的 file size 可區分圖檔與傳輸 padding。 |
| S200 | H type；type=2 時為 D count、count 個 S；最後 H(0)。其他 type 沒有 count。此名單是「關注血盟」，不是血盟成員。 |

S72 直接更新既有 Actor／WorldObject，換盟或退出時清除舊 ClanId，避免顯示舊盟徽；不從 reserved 推導 ID。快取依 WorldEpoch 失效，包含切圖、重設和重連。送出不預先修改本地血盟。

S118 解碼只讀封包記憶體，接受 16×12、BITMAPINFOHEADER=40、BI_RGB、1/4/8/16/24/32-bit BMP，含上／下方向與調色盤索引檢查；16-bit 依 BI_RGB 使用 RGB555。其他格式顯示未支援提示並移除該識別碼舊圖像，不假造圖、不讀圖檔、不讓圖像錯誤中斷連線。**伺服器只保存／轉送上傳 bytes，未驗證實際既有盟徽的檔案格式；BMP 支援僅由合成封包驗證。** 快取上限 256 枚。

## 範圍缺口與伺服器差異（未完成）

1. `C_JoinClan` 對招募者送 S219／message=97 的確認 challenge，需 C121 回答；現有 `Trade.ReceiveTrade` 消耗所有 S219，非 252 一律標未處理。僅增加 S72/S118/S200 無法完成雙方入盟。需要 Trade.cs 最小轉交點與血盟 challenge 狀態／接受拒絕 UI。
2. `C_Pledge` 現行路徑送頂層 S167 公告、S170 成員、S250/171 上線成員；舊 S39 `pledge`／`pledgeM` 路徑已註解。S167 帶 S 盟名、S 盟主、D emblem ID、D 建立時間、478-byte 公告；S170 帶 H(1)、C count，逐筆 S 姓名、C 階級、C 等級、62-byte 備註、D 角色 ID、C 職業。必須增加對應接收掛接，不能把 S200 名單冒充成員。
3. S87 的 D 是 **clan ID**；C72 卻用 **emblem ID** 比對；C72 回覆 S118 時鍵為查詢的 emblem ID，而上傳廣播 `S_Emblem(L1EmblemIcon)` 使用 clan ID。現有 S118 可以依廣播 clan ID 掛到角色；不能把所有回覆一律用 ClanId 對應。
4. `L1Clan._emblemId` 是獨立欄位；本次唯讀搜尋 src 未找到 `setEmblemId` 的有效呼叫，不能假定等於 clan ID。即使擴充 S167，仍需核對實際伺服器是否給非零、可用的 emblem ID，或由伺服器維護者修正查詢契約。客戶端不猜 ID、不修改伺服器。

本輪已向使用者詢問是否允許超出明列範圍的最小掛接，尚未取得答覆前維持原範圍。新增掛接、加入確認、成員顯示與自動盟徽查詢不列為完成。

## 唯讀來源 SHA-256

下列路徑相對於上述伺服器 `src/com/lineage/server`；只有摘要，無原始碼副本。

| 路徑 | SHA-256 |
|---|---|
| clientpackets/C_CreateClan.java | 511c19e9ac3175c7d6554b99844704f09045f0cba000ea199651ef059d8556d6 |
| clientpackets/C_JoinClan.java | 6a132cc69fae05b7a338f2772469689cdca119a326e75a2ebb9bf935348701af |
| clientpackets/C_LeaveClan.java | 6e4da42ff3cd31ae98cfa5558a5b273cfc3d95af1fa3390c1a20bfeb128848b6 |
| clientpackets/C_BanClan.java | aad634b1ae485cc7a85c756d7dca162beb7a5c4fdcc52c031cee0af314829ce9 |
| clientpackets/C_Clan.java | 56d98e8cbdc9ff2a72e518050f3ec21699a0be0bf3348f0f75b8590258244d04 |
| clientpackets/C_Pledge.java | aeacb3730a64876991c059f160899830d2bc3d71b430aa47f17296b000f2e04a |
| serverpackets/S_ClanUpdate.java | 73518ee411eeea3e780892bbae64d39942bedea677e57d529d8586d4a5c9499a |
| serverpackets/S_Emblem.java | 3cc2416ec03c685ca7ce5bb4509b390a2f16553016edb443d3b7ff348a494f72 |
| serverpackets/S_ClanAttention.java | e8322f46f48943591cd5060d1f51ca868abf680e606b1596b15284515439f35e |
| serverpackets/S_Pledge.java | 5fbe220f68ee9f8923452ab5e1dbb01a038c77ad001a0c31087b86c89d1c2e4a |
| serverpackets/S_OtherCharPacks.java | 95ee7228e1c9d14d1ae42ec35dbac5fd37166ab2a5ee06f94406960babf99ce5 |
| model/L1Clan.java | 136a4f6b9130882bb98a0d4d5647e9b73d08bac6b0becaa220e5facee6b4156b |
