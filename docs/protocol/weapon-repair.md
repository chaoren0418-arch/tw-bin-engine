# T134 武器修理與 PK 查詢

核對 `docs/reference/server-protocol.md`、`server-rules.md`，並於本機唯讀查閱下列處理器。未複製伺服器程式碼。C/H/D 分別為 1/2/4 bytes，H/D little endian；下列長度未含加密前補至 8 的倍數之零 padding。

| 封包 | 欄位與語義 |
|---|---|
| C118 | 只有 opcode，長度 1。`C_FixWeaponList.start` 未讀 payload，沒有條件尾段；從持有武器挑損壞度大於 0 者回清單。 |
| S83 修理 | C83、D200、H筆數、每列 D物品實例 ID＋C損壞度，長度 `7+5n`。D200 同時是每點價格與本 profile 的分派辨識值。三個建構路徑（list/player/single）布局相同，允許空清單。 |
| S83 寵物 | D70，留給 T127；本模組不消耗其 reader、不解析或更新寵物資料。 |
| C20 | C20、D物品實例 ID、DNPC 實例 ID，長度 9。NPC 不得為 0（0 是寵物分支）。查詢 C118 不帶 NPC，提交時使用查詢綁定的目前目標。 |
| C51 | 只有 opcode，長度 1，沒有條件尾段。 |
| S71／562 | C71、H562、C1、S十進位 PK 次數；沿用現有 S71 解碼，本模組從解碼後的訊息顯示繁體中文。 |
| S71／464、189 | 分別為修理結果與金幣不足；只失效清單並提示重新查詢，不以訊息替背包扣款／歸零損壞。 |

`C_SelectList.start` 核對目標是同圖 NPC、各座標距離不超過 3 格、物品在背包且損壞度大於 0，費用為損壞度 × 200。成功扣款後清零並送訊息 464 與庫存更新，金幣不足送 189。客戶端只提交一件，送前再次驗證 session/world/target/list/inventory 世代、距離、死亡、交易及動作鎖。角色／NPC 的最終資格由原服決定；S87 flags 並不能可靠證明 NPC 種類。

目前分支尚無 T121 `ItemStatus`，因此依任務降級為更新清單 UI；既有 S24／S242 庫存路徑維持權威，沒有自行解析或修改耐久 blob。修理回報後按「查詢／更新清單」取得新 S83。

清單與訊息都沒有 request ID。只在相符的等待階段接收，拒絕非等待期間及逾時回應；已提交清單不因重複 S83 重開。無法從線上格式嚴格識別「逾時後再次查詢」收到的是哪次回應，因此不宣稱具備交易級相關性，也不自動重送 C20。原服再次檢查實際損壞及金額。訊息觀察使用既有每包處理後事件，避免 UI 輪詢漏掉被後續訊息覆蓋的 S71，沒有新增共用 S71 分派。

來源均位於 `D:\lineage開發\1-伺服器\src\com\lineage\server`，SHA-256 與參考表相同：

| 相對來源 | SHA-256 |
|---|---|
| `clientpackets/C_FixWeaponList.java` | `cdd18397b303b5b1b103969a2bd8c02d5cabc6ae6a49844dd96aa420d99b3ad6` |
| `clientpackets/C_SelectList.java` | `49532fe36d8bb990cee9fb230845f33cfd813085e8e7f33e1881a411799f28c4` |
| `clientpackets/C_CheckPK.java` | `11ffa8ad46337a48bc3c0d0edaf97f18f9baf200aa79c483d172f986a37371be` |
| `serverpackets/S_FixWeaponList.java` | `ef59e1f5f87c538be34a5c55b72d72b81aaa89709559ef158e282ba38641ca66` |

S83 寵物分派、S71 通用格式與 serializer SHA 另見既有 `server-protocol.md` 的 `S_PetList`／`S_ServerMessage`。
