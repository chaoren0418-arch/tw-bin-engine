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

## 第二次執行的 d287 JAR 核對

2026-11-07 唯讀檢查 `D:/lineage開發/TW1608262200研究/environments/p24/server/Server_Game.jar`，SHA-256 為 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。使用 Microsoft OpenJDK 17.0.17 的 `javap -p -c` 讀取方法位元碼，並在記憶體中掃描 ZIP 內的 class 符號。未執行 JAR、未解壓或複製伺服器程式碼。

本 worktree 與原專案尚無 `docs/reference/source-vs-jar.md`，已透過 GitHub 連接器讀取預設分支上的 T123 文件，並自行核對上述 JAR 的 SHA。以下為本次已驗證的靜態格式，實際往返仍待授權的真機測試。

| 路徑 | d287 核對結果 |
|---|---|
| `C_JoinClan` → `S_Message_YN` | 呼叫 `(Offer, Ticket, int, String)`，kind 為 `CLAN_JOIN`、message 為 97、字串為申請者名稱。S219 格式為 `C(219) H(0) D(0) H(97) S(申請者)`。 |
| `C_Attr.start` | 先讀 H，若為零則讀取並捨棄 D，再讀 H 訊息碼。97 分支讀 C 回答，以 `UiOperationContext.takeCurrent(CLAN_JOIN)` 取得目前邀請。可對應編碼 `C(121) H(0) D(0) H(97) C(接受=1／拒絕=0)`，再沿用傳輸 padding。 |
| `S_Message_YN` | 六個建構子均存在，沒有 `confirmation(...)` 或帶 requestId 的私有建構子。入盟流程只使用上述帶一個字串的格式。 |
| `C_Pledge.start` | 依序送 `S_Pledge(int)`、`S_Pledge(L1Clan)`、`S_PacketBox(171, L1PcInstance[])`。無血盟時送 S166/message=1064。 |
| `S_Pledge(int)` | 頂層 S167，依序為 S 盟名、S 盟主、D emblem ID、D 建立時間、478-byte 公告。 |
| `S_Pledge(L1Clan)` | 頂層 S170，H(1)、C 成員數，逐筆 S 姓名、C 階級、C 等級、62-byte 備註、D 角色 ID、C 職業。 |
| `S_PacketBox(171, L1PcInstance[])` | `C(250) C(171) H(人數)`，逐人為 `S(姓名) C(0)`。每筆字串後另有一個零 byte。此路徑由 `S_PacketBox` 提供。 |
| `C_Clan` | 仍以請求中的 D 比對 `L1Clan.getEmblemId()`，找到後取 clan ID 查圖，回覆 `S_Emblem(請求的 emblem ID, bytes)`。 |
| `L1Clan` | `getEmblemId()` 讀取獨立的 `_emblemId` 欄位，`setEmblemId(int)` 寫入該欄位。整個 JAR 的 class 符號掃描中，兩個符號都只出現在 `L1Clan.class`。 |

盟徽初始化仍待釐清。上述掃描支持「未找到一般直接 setter 呼叫」的結論，尚未排除反射或外部程式碼，也未讀取實際角色、DB 或伺服器設定。S167 若提供有效的非零 emblem ID，可用回包盟名與目前血盟身分建立映射，再送 C72。其他血盟的對應來源與零 ID 的伺服器行為仍須主控／伺服器維護者裁定。

## 第二次執行所需的範圍裁定

已重新讀取 Issue #262 本文及六則留言，包含退回留言與第二次派工。仍限定 WorldSession.cs 的 S72／S118／S200 三個 case，審查明示未授權越界。待裁定的最小產品變更如下。

- `Trade.cs` 的 S219 路徑增加 message=97 轉交，並處理其他確認訊息取代舊邀請時的失效通知，避免回答過期的本地邀請。
- `WorldSession.cs` 增加 S167、S170 各一個血盟接收 case，以及 S250 僅子碼171的轉交。其他子碼沿用既有處理。
- `Clan.cs`／`ClanUi.cs` 補接受、拒絕、邀請失效、成員與上線狀態、有效 emblem ID 的查詢與映射。測試必須經正式 Receive／sender 入口。
- 主控釐清有效 emblem ID 的發布範圍與初始化責任，或在 Issue 明確拆分剩餘功能及歸屬。

本次只補協定證據與報告，產品功能缺口仍列為待完成。

## 第三輪裁定與完成狀態（2026-11-07，取代前述待授權缺口）

主控已授權 Trade.cs 的 message=97 單一轉交，以及 WorldSession.cs 的 S167／S170／S250-sub171 掛接；本輪已實作入盟接受／拒絕、失效處理與成員顯示。前述「尚待擴充分派範圍」及「本次只補證據」為前輪紀錄，不再代表現況。**盟徽查詢及 ID 對應已拆至 T135（#316）**；本輪不依 S167 自動送 C72，不實作 S_Classic*。

- 入盟請求沿用上述 d287 單字串建構子。C121 的零 D 沒有識別用途；本地 `Revision` 只供 UI／排隊失效檢查，從不寫入網路。
- `UiOperationContext` 的 JAR 靜態初始化為 `TimeUnit.MINUTES.toNanos(5)`；本地以單調時鐘設五分鐘有效期。對方物件消失、死亡、血盟變更、WorldEpoch 變更、一般伺服器結果／其他確認或 NPC 操作取代時清除；接收完成事件、UI 快照及送出 gate 均重新驗證。一般恢復訊息不清除邀請。沒有專用取消通知可供推定無聲伺服器失效，最終授權仍以伺服器為準。
- 更正前輪表內無血盟訊息的 opcode：`S_ServerMessage(int, ...)` 為 **S71**，不是 S166。取消／一般結果的合成回歸使用既有 S71 路徑。
- S167 使用固定 478-byte 公告，S170 使用固定 62-byte 備註；Big5 解碼至第一個零，保留滿長度非零字串。S170 只接受 H(1)，S250/sub171 採 C_Pledge 實際呼叫的 L1PcInstance[] 建構子（H count、S name、C(0)），不混用 Object[] 版本。
- S167 開始新查詢快照，僅接受與目前自身血盟相同的盟名。S170 與 sub171 分別更新成員及上線集合；沒有上線快照為未知，收到空集合才是全員離線；離盟或世界切換清除。S200 始終只更新關注血盟。
- S250 既有狀態處理由 ReceiveMagicStatus 提前分派；本輪在 WorldSession 剩餘 switch 加 `case 250 when ... sub171`，既有子碼不變。回歸包含 sub34 與 S219/message252。
- 血盟正式測試 118 項，CI map-continuity 群組 476 項；詳見 `docs/reports/issue-262.md`。
