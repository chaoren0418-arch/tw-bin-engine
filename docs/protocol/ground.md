# 地面物品契約（T103／#190）

依據 [#132 對照](../reports/issue-132.md) 差異 9、[規則 R22／R23／R26](../reference/server-rules.md)，及 M6 契約 PR #194 的 `6873e75c630c660baafa65b64efb5ba2f51ec73b`。本分支建立時契約尚未進 main；已唯讀讀取該提交，未複製其他任務的變更。

## 封包與權威

- S87 沒有 templateId 或明確物品型別。依 `S_NPCPack_Item` 的固定欄位分類：action／direction／speed／lawful／flags／clanId／rank／brave／level／unknown 為 0，title／clan／owner 為空，HP／兩個 tail 為 255；objectId 非 0，wire count 不超過 2,000,000,000。
- gfx、名稱、亮度由伺服器決定。合法名稱包含充能後綴、照明後綴、未鑑定魔杖的空字串；名稱中的數量可能大於已截上限的 wire count。因此不以名稱反推數量、模板或拾取資格，也不以本機有沒有圖檔決定是否接收。
- S87 取代物品快照；count=0、S120、換圖與斷線移除。C112 仍送地面 x/y、objectId、正整數 count（HHDD，16-byte padding）。距離、世代、數量快照、送出前再次檢查與單次送出機制沿用；不預扣地面或背包。
- `CanDrop`／`Match` 的四筆白名單及限制完全保留。新增的三種地面圖不擴充本地可丟清單，更不代表伺服器允許丟棄。C25 的 DORP_ITEM、白名單、可轉移、裝備／封印、限時等規則仍以伺服器為準。
- 這是目前伺服器封包的分類政策，並非還原舊 BIN 的原生分類鏈。若其他版本將 NPC 編成完全相同的 envelope，線上資料本身不足以辨別；B07／B13 的邊界維持。

## 執行期外觀與核驗

`ground-catalog.json` 逐 byte 不變。`Find` 對任意 ushort gfx 提供外觀描述；catalog 外的描述不偽造 templateId／Source。既有 renderer 與 Web 資源路由沿用這個介面，無須修改共用分派點。

從 `TW_CLIENT_ROOT`（預設 `D:\lineage開發\0-客戶端`）讀取並核驗 `TW13081901.txt`、`Sprite.idx/pak` 與 `Sprite00`–`Sprite15`。程式只保存 SHA-256／大小；不內嵌或輸出原圖。metadata SHA 沿用 #48 的 `28c4fc600833fb8c609ab195a67ff926e8ffa571100d8d4b389a23be8c2c8db8`，執行期解析 header alias，檢查循環後查找 `<sprite>-0.spr`（不分大小寫）。

查表順序為主 Sprite，再依 00–15；主封裝有同名項目時保留現有主封裝外觀，例如 22 在 Sprite04 另有不同版本，未混用。每個 IDX 核 SHA、長度、名稱唯一性與 offset/size 邊界；使用的 PAK 首次串流核對整檔 SHA／長度，保持 FileShare.Read 的唯讀 handle，避免核驗後被替換或寫入。整檔首次核驗有 I/O 成本，後續同根目錄共用 handle 與索引；切換根目錄時釋放舊 handle。

沿用 action 0／frame 0 的靜態地面呈現政策，不宣稱完成任意地面動畫。解碼檢查 palette／RGB555、RLE、tile index、block offsets、expanded length、完整消耗、畫布 bounds，以及大小／展開上限；允許未選中的空幀 sentinel。既有四筆仍另核 payload／bounds／RGBA pin。缺圖、封裝版本不符或解碼拒絕時，不借用其他外觀；物品仍在清單中可選取拾取。

## B06 原圖核對（只有 metadata）

以下三筆皆在主 `Sprite.pak`。offset 是 **PAK File Offset**，不是 RVA／VA；bounds 是 frame 0 的 inclusive [L,T,R,B]。Python 既有 decoder 與本次 C# 結果一致，未輸出圖檔。

| 物品／gfx | IDX record | PAK File Offset | bytes | bounds | payload SHA-256 | RGBA SHA-256 |
|---|---:|---:|---:|---|---|---|
| 回家卷軸／22 | 22705 | 979462404 | 212 | [16,7,31,16] | `2dd7c5dc86081b85c5a2ae381e7fbf3bbb26f5fe47215a1231793ffacef9fb58` | `b2ef7b8643b4e92ef5a2613d1b0544b5de46fbf3b96344f2f246dad0fe48ff08` |
| 自我加速藥水／42 | 57772 | 970068809 | 176 | [20,4,28,14] | `0ff297ed8073cc0af389159845acc77e19174661d34a88dd9872e2af5771d54e` | `8f5069d7cc9a7f3ec9ec63d51ebf3b1d5d1ebbfbe65481c2a2141ea5f7b494d8` |
| 終極治癒藥水／46 | 65648 | 968875454 | 170 | [20,4,28,14] | `f6226c8b2a269c69b169b6e702a18e370d03cf0c76efe9e7826acdafc4206247` | `d9d2efc4a593b83582917a154a707d0e12817133befa09a2d024b9b910003802` |

## 唯讀來源 SHA-256

來源均為 `D:\lineage開發\1-伺服器\src\com\lineage\server\` 下 Java 檔；只讀原始碼，未讀配置或 DB。

| 來源／方法 | SHA-256 |
|---|---|
| `clientpackets/C_DropItem.java`／`start(byte[], ClientExecutor)` | `74e111eca656c740a59d0433c05698e734f699fd9199c006283484de1f553752` |
| `clientpackets/C_PickUpItem.java`／`start(byte[], ClientExecutor)` | `804b4ba715c505f458c63e78f6cfd81f03c65c618ff3a1b506ad7da7a0772254` |
| `serverpackets/S_NPCPack_Item.java`／`buildPacket(L1ItemInstance)` | `80b2fa74a278161a3b30ea1f0807e1aaceae1ceac0de839b9baba3c9528f24ee` |
