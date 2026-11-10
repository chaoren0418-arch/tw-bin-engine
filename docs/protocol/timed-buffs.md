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

- S103 type 8（三段加速）：R7-B（T341／#804）依 `sub_9343C0`（`0x934401–0x934425`）處理。自己的 type 8 設 `+547`（`WorldSession.OwnThirdSpeed`），之後**當成 type 0** 處理（酒醉解除；地圖 67／420 為醉度 1）；自己的 type 0 清 `+547`。`+547` 給表情節流用（2265 ms）。不跟 `WorldEpoch` 走（伺服器傳送後不重送 type 8），Reset 才清。其他物件的 type 8 仍計入 `unhandled`（`+547` 影響動畫時間 `sub_A3BC30`，未做）。
- 網頁場景（`PublishSceneWeb`）不搖晃；只有主視窗 `OnPaint` 的場景會搖晃。

## T179（#481）：酒醉改為逐掃描線正弦

取代上方「搖晃波形」與「網頁場景」兩條。依 `docs/reference/ambience-and-map-events.md` §4(c)（TW16 `sub_F01E50`，已驗證）：

- 第 j 行水平位移 `dx = (int)(醉度*3.5*sin((相位+j)*π/50))`，向零截斷；醉度 1–3 的最大位移 3／7／10 像素，垂直週期 100 行。
- 相位自收到 S103 起每 50 ms 加 1，在 0–199 循環，5 秒回到原點（`TimedBuffRules.LiquorPhase`）。
- 掛載：`PaintLiquorWave` 在 `OnPaint` 的 Paint 事件內，先把原本的 `OnPaint` 畫到離屏 32 位元畫面（巢狀 Paint 直接返回），逐行位移後畫到 viewport，再把外層剩餘繪製移出可見範圍。這樣不需改 `Program.cs`。代價是酒醉時每幀畫兩次場景，`paintCount` 也會多加一次。
- 縮放：原版是 800×480、1 倍；引擎依 `Camera.Zoom` 把行號除以縮放、位移乘以縮放。
- 行兩端空出的欄位重複該行邊緣像素（原版留什麼未查證）。

**未實作（推測或文件未定）：** `sub_81B0E0()` 為假時位移減半（意義未知）；地圖 67／420 的最低醉度 1（型 0 時計時器停止、相位凍結的實際畫面未確認）；水中逐欄垂直波（不在範圍）。網頁場景仍不套用。
