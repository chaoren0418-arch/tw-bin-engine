# C164 道具使用參數

## T109／#197 核對結果

2026-11-07 依 #132 對照表與本機伺服器來源唯讀核對。來源根目錄為 `D:\lineage開發\1-伺服器\src\com\lineage`，下列內容記錄欄位與行為結論。

封包開頭為 `C(164), D(ownedItemObjectId)`。所有整數皆 little-endian，`S` 為 Big5／CP950 加一個 NUL。依既有 `LoginPackets.Pad` 補零到 8-byte 倍數。只有伺服器背包回報的物件可以成為來源，使用者輸入提供目標與參數，編碼器依來源物品的 `UseType` 決定格式。

| 背包 use_type | 尾段 | 本次輸入介面 |
|---|---|---|
| 248（伺服器 -8） | C(操作), C(食譜編號) | 開啟料理書／製作料理，0-based 食譜編號 |
| 5、17、39 | D(目標物件), H(x), H(y) | 點選場景物件後確認 |
| 6、29 | H(所在 map), D(書籤 ID) | 自動帶入當前 map，手動填書籤 ID，0 表示隨機 |
| 7、14、26、27、46、55 | D(目標背包物件) | 獨立下拉選單選擇持有物品 |
| 8、30 | D(目標物件) | 點選場景物件，8 可選屍體 |
| 16 | S(變身名稱) | 文字輸入 |
| 28 | C(0-based 魔法編號) | UI 輸入 1-based 編號，送出時減一 |
| 12、31、33、35 | H(信紙代碼), S(收件人), UTF-16LE 主旨／本文 | 每個 UTF-16LE 欄位各加兩個零 bytes 作結尾 |
| 42 | H(x), H(y) | 點選地面座標後確認 |

### 格式以外的語義

- `C_ItemUSe.useItemRequest` 先核實持有道具，再讀尾段並呼叫 executor。參數正確仍須由伺服器判定職業、距離、物品類別、冷卻、材料及作用結果。
- `L1Item.getPacketUseType` 將負值轉成 byte，將 96／98 以上映射為 26，97 映射為 27。客戶端只能使用實際收到的背包類型與實例 ID。
- `Move_Reel.execute` 使用書籤 ID 找角色的記憶點，找不到時可消耗卷軸並改走隨機傳送。UI 明示這項行為。目前書籤由使用者填入 ID，書籤名稱列表未納入本模組。
- `Cooking_Book.execute` 的操作 0 開啟料理書，操作 1 製作指定料理。料理選單回包的呈現由後續模組接續，本次可直接填入食譜編號。
- `Blank_MagicReel.execute` 以 0-based 編號處理，模板查詢使用編號加一。材料與職業檢查由原服執行。
- `LetterDeliveryService.parseContent` 逐兩 bytes 找兩個結尾，分成主旨與本文。preflight 的 `H,S` 最低長度檢查無法單獨證明信件本文有效，因此本次產生完整的兩個 UTF-16LE 欄位。

### 客戶端狀態與限制

參數視窗綁定 session generation、WorldEpoch、map、角色、來源物品、背包內容版本與選取版本。確認時及送出鎖內都重新驗證，移動、換圖、死亡、斷線、背包異動、取消會拒絕舊請求。等待送出期間，世界目標資料被替換或更新也會拒絕。資料錯誤與等待傳送失效皆不自動重試。

UI 的背包、U、HUD 快捷與 Web 使用按鈕均通過原有 `UseSelectedItem`。有尾段的道具先提出選取要求，明示確認才送出。點目標時在 `OnMouseDown` 攔截場景事件，右鍵／Esc 可取消。成功送出後清除選取要求，數量、HP、裝備及作用結果由伺服器回報。

本次保留既有無尾段補給品／裝備的支援條件。其餘未知 use_type 會拒絕。名稱限制 200 個 Big5 bytes，信件主旨／本文分別限制 200／2000 個 UTF-16 code units，均為本地輸入上限。名稱拒絕空白、控制字元與 Big5 無法編碼的字元，信件拒絕內嵌 NUL 與不完整 surrogate。

## 來源 SHA-256

| 相對來源路徑 | SHA-256 |
|---|---|
| `server/clientpackets/C_ItemUSe.java` | `9cba57e80f4e62f76ccfc461f12294ebc4e8428d74472765f3aba19a9bfd7959` |
| `server/clientpackets/ClientPacketStructure.java` | `ca88c05673188ec1b99a961be0d67561808f9bd42adf8a937b9ebda6767e96a9` |
| `server/templates/L1Item.java` | `48497f7b9666aeed96ebe477c46a9fef8b5a1e52481c98c0009ef5aa65b53bfb` |
| `data/item_etcitem/Cooking_Book.java` | `a7927430d4cad2fc108f56b442bebef2024ed6e640b4965eb2bc05fa5aa9a4bd` |
| `data/item_etcitem/Blank_MagicReel.java` | `a61fa38e08931359688c6c5b1a5e21c0b98961183c612b637ea36db46a212e1e` |
| `data/item_etcitem/Letter.java` | `994f4a1f4bbc54673ccb4dd6c9a325f7216ee01aceb51076b79a8773fa1a28e0` |
| `server/model/mail/LetterDeliveryService.java` | `5aaed2dbfade15a0bca810b3475d09ce2cd2da7d8d3ea75d34668bbe284fd120` |
| `data/item_etcitem/teleport/Move_Reel.java` | `7b033662df02f20c8d9e7d818206bf6646c59c9c1584489621bed7a3b12fb17f` |
