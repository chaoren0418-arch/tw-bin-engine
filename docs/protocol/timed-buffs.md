# 暫時能力、酒醉、水中呼吸、刪除技能（#416）

實作：`engine/source/TimedBuffs.cs`。格式依 `docs/reference/server-protocol.md`；語義唯讀查閱 `D:\lineage開發\1-伺服器`。

## 封包格式（已驗證：與伺服器來源一致）

| opcode | 名稱 | 欄位 | 客戶端處理 |
|---|---|---|---|
| S166 | `STRUP` | `H(time)` `C(str)` `C(weight240)` `C(type)` | 更新 `Status.Strength`、`Status.Weight`，並計時 |
| S188 | `DEXUP` | `H(time)` `C(dex)` `C(type)` | 更新 `Status.Dexterity`，並計時 |
| S103 | `LIQUOR` | `D(objectId)` `C(type)` | 自身 type 1～3 開始搖晃，type 0 解除 |
| S126 | `BLESSOFEVA` | `D(objectId)` `H(time)` | 水中呼吸計時，time 0 解除 |
| S160 | `DELSKILL` | `C(tier)` 加 28 或 32 組 mask | 從已學技能清單移除 |

## 伺服器語義（已驗證：來自發送點）

- `IndependentSkillIcon`：`type` 是增加值。`PHYSICAL_ENCHANT_STR`／`DEX`（技能 42／26）送設定值（預設 5），停止時送 1；`DRESS_MIGHTY`／`DRESS_DEXTERITY`（技能 109／110）開始與停止都送 2。
- 設定值為 2 的 42／26 開始時無法與 109／110 區分，所以停止封包在對應欄位沒有計時時，改清另一欄。
- 同一來源也可能改走 S250/154 圖示封包（`getGraphicKey` 不為 0）；兩種路徑以 `MagicBuff.Source` 區分。
- S103 發送點：`Liquor` 道具送 type 1、`sendVisualEffectAtTeleport` 重送 type 1 或 2、三段加速蛋糕送 type 8（可對 NPC）、其結束送 type 0。`S_Liquor` 註解寫 `0:無 1:2:3:效果強度`。目前 `setCHAOTIC` 沒有呼叫者，type 2 實際不會出現，type 3 沒有發送點。
- `S_MapID` 會讓客戶端清空畫面，伺服器隨後才重送 S103，所以酒醉狀態綁 `WorldEpoch`。
- 解除酒醉的 `setDrink(false)`（`StatusHaste`、`Food2`、`HASTE`、`L1SkillUse`）**不會送 S103**。
- S126 只用 `sendPackets` 送給本人；道具 300／1800／2100 秒，疊加上限 3600 秒，結束時 `L1SkillStop` 送 time 0。

## 推測（待 M6 smoke 或原 BIN 校準）

- **搖晃波形**：振幅 `level*2` 像素、週期 1.2 秒與 1.7 秒的正弦都是本任務自訂，不是從原客戶端取得。
- **自身加速解除酒醉**：伺服器不通知，客戶端以自身 S255 mode 1 比照 `setDrink(false)`。若加速裝備也送 mode 1，可能多清一次，下一次傳送時伺服器會重送。
- **type 0 不分來源**：三段加速結束的 type 0 也會清掉酒醉，比照 `S_Liquor` 的單一欄位語義。

## 尚未實作

- S103 type 8（三段加速）不屬於本任務，仍計入 `unhandled`，讓缺口看得到。
- 網頁場景（`PublishSceneWeb`）不搖晃；只有主視窗 `OnPaint` 的場景會搖晃。
