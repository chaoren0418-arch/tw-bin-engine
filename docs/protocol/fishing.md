# 釣魚協定（T139／#325）

2026-11-07。唯讀查閱 #132 規則 R24、#226 類別索引、本機 Java 來源，並以 `javap -p -c` 核對 d287 實際位元碼。沒有讀取配置、DB 或啟動伺服器。

## 部署證據

JAR：`D:/lineage開發/TW1608262200研究/environments/p24/server/Server_Game.jar`，SHA-256：`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。

下表路徑均以 `com/lineage/` 為前綴、`.class` 為副檔名，雜湊是 ZIP entry 內容。

| 類別 | SHA-256 |
|---|---|
| `server/clientpackets/C_FishClick` | `f1d9b46f4a34034cb0fb2ba9b308733f47fde780efcb7d677cae3dfd2727f350` |
| `server/clientpackets/C_ItemUSe` | `32dea956650ff787697f465f52309ae4653b083281b279cfdb3dd5dcebf0fc6a` |
| `data/item_etcitem/FishingPole` | `05df30c9c6b56265faac761111aacf4868b631b7a145ff4e9e0f5a4aa7022789` |
| `server/timecontroller/pc/PcFishingTimer` | `caf9317ee99e2bb0cb9d7c7a7437a13d42b91cb76d57a82eba6cf93ed1e03d39` |
| `server/serverpackets/S_Fishing` | `ff9718e6d6c6c4d6593321de39d189beba46561bf90bf551810bb19617155691` |
| `server/serverpackets/S_CharVisualUpdate` | `4991209309da8765159d34d734fe6ba39a1035f10ea54084663b2e29266c528d` |
| `server/serverpackets/S_ServerMessage` | `c4312bebc53f5539fd3c9f04bc28f1e2cf45aafd55d8f6d303ba272629d09d01` |
| `data/cmd/CreateNewItem` | `6f4a2575d574f9e4184f72086b34f9b2115add49d5bd07fa39d9ac34051bd5e3` |

## 實際格式與行為

數字為十進位 opcode；C/H/D 分別為 1/2/4 bytes，H/D 為 little endian，S 是 CP950 NUL 結尾。引擎傳輸沿用補零到 8-byte 邊界。

| 用途 | 封包（含 opcode） | 核對入口 |
|---|---|---|
| 使用釣竿 | `C164 D(背包物件 ID) H(x) H(y)` | `C_ItemUSe` use_type 42 分支 → `FishingPole.execute/startFishing`；沿用 `ItemUseCodec` 與現有座標選擇 UI |
| 取消釣魚 | `C62`，無條件尾段 | `C_FishClick.start` 不讀封包欄位，呼叫 `PcFishingTimer.finishFishing(pc,false)` |
| 開始／等待 | `C158 D(actor) C71 H(x) H(y)` | `FishingPole.startFishing` 發 `S_Fishing`，action 為 71；沒有獨立「等待」封包 |
| 取消後外觀 | `C119 D(actor) C(weapon)` | `finishFishing` 發 `S_CharVisualUpdate`；仍交給既有外觀處理器 |
| 無魚餌／地點拒絕／結束 | `C71 H(message) C0` | message 分別 1137／1138／1163；1163 僅 `finishFishing(pc,true)` 發送 |
| 取得物品通知 | `C71 H403 C1 S(itemLogName)` | `successFishing` → `CreateNewItem.createNewItem` 交易 → `notifyGrantCommitted` |

開始條件由伺服器判定：地圖 5300、落點及四鄰為釣魚區、釣竿距離（41293 為 5 格，41294 為 3 格）、至少 2 個魚餌 41295。背包 wire 不帶模板 ID，不以名稱或 icon 假造模板對應。週期受伺服器設定影響，本輪未讀配置，UI 不顯示推算倒數。

沒有漁獲的週期只消耗魚餌，沒有已核實的失敗結果訊息；取得物品失敗可發結束訊息。403 是通用取得通知，沒有釣魚關聯 ID；只顯示「釣魚期間收到」的原服訊息，不宣稱每個 403 都是漁獲、不預扣魚餌、不預加物品。S119 的 wire 也不帶停止原因；UI 明示依外觀更新結束狀態。

## 產品接收掛接（第 2 次修正）

原任務假設釣魚使用 S250，但已核實的 d287 路徑使用 S158／S119／S71。本次依重新派工所附退回修正要求，在 `WorldSession.ReceiveCore` 增加一處 `ObserveFishing(plain)` 掛接，限這三個 opcode，保留原有處理器。

模組使用獨立 reader；掛接後繼續執行原接收鏈，S71 仍更新伺服器訊息、S119 仍更新外觀、S158 仍更新物件動作。跨地圖／重設以 WorldEpoch 懶清除，未增加共用重設掛接。

離線測試已移除直接 observer 呼叫，全部經 `Feed` → 正常 `WorldSession.Receive`。先以舊產品驗證「S158 開始等待」確實失敗，再建置修正版驗證 39 項通過，包含 C62、C164 DHH、三種 S 格式、訊息 1137／1138／1163／403、其他角色、損壞封包、逾時與排隊失效；另確認 S158 只觀測一次及原有動作／外觀／訊息處理仍生效。合成封包測試不代表真機驗收。
