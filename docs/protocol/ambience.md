# T147 環境封包與畫面政策

本次依 `docs/reference/server-protocol.md` 的 S123、S115、S40 表格與 `server-rules.md` R06／R10／R11 實作；不足部分唯讀查閱本機伺服器來源。未執行伺服器、未讀取設定或 DB。

| 封包 | 有序欄位（不含加密） | 行為 |
|---|---|---|
| S123 `S_GameTime` | C(123)、D(time) | 沿用既有 `ServerTime`／船班處理，再通知環境時鐘；不新增 C 封包。 |
| S115 `S_Weather` | C(115)、C(weather) | 0 晴；1–3 雨、17–19 雪，三段強度；其他值保留原碼並不畫粒子。 |
| S40 `S_Light` | C(40)、D(objectId)、C(radius) | 更新自身／已存在物件的 Light；0 熄滅；未知 ID 不緩存。 |

D 是 little-endian 32 位元；沿用引擎無號 `ServerTime`。每包依既有 `LoginReader.Finish()` 驗證 8-byte 對齊與零 padding。S115／S40 在完整驗證後才改狀態。S123 的既有解碼、船班與 ready probe 行為保留。

## 已核對來源

以下路徑皆相對 `D:\lineage開發\1-伺服器\src\com\lineage\server`，僅記錄規則與 SHA-256，不複製來源。

| 來源 | SHA-256 | 結論 |
|---|---|---|
| `model/gametime/L1GameTime.java` | `6f9b22b839c7693d7d8ebe37faa56369a1e2d90de8f1c00a461777566551c749` | `valueOf` 以現實毫秒換成 6 倍遊戲秒，取 3 秒倍數；`isNight` 定義 06:00–17:59 為白天。 |
| `serverpackets/S_GameTime.java` | `14c950e819c2e9cc4a02835e85509929feeb788c56127404e9a2f008d15039ef` | 傳送 `getSeconds()`，不是時分壓縮值。 |
| `serverpackets/S_Weather.java` | `4e1d228c83c39bc5b034e0acff23732ce019530add9cf0b4aa664897e5aa2889` | 類別註解：1–3 雨、17–19 雪。 |
| `command/executor/L1ChangeWeather.java` | `e839e48600e723ea4dd736c5cac4e13aa64160694a88af9a65caee674f947617` | 0 終止氣候；雨雪註解與封包類相反，但實際程式只原值轉送。 |
| `serverpackets/S_Light.java` | `d633c820c10ec2eda64381be17ea3adfbe00e256b0c212cf242414fed2815dd9` | 傳送物件 ID 與光照範圍碼。 |
| `model/L1Character.java` | `efde278d67f29468b2581188c6966a6d6ee9e7f6a87cc2d5650b624700410fa3` | `turnOnOffLight` 取 NPC／照明道具範圍、照明法術設 14；S40 與 S87 共用光照欄位，隱形來源不對他人廣播。 |

## 畫面政策與待驗證

- 時刻為 S123 遊戲秒 `% 86400`，兩個樣本之間以單調時鐘作 6 倍視覺推進；不回寫船班或 `ServerTime`。
- 05:00–06:00 漸亮、18:00–19:00 漸暗；最大遮罩 alpha 140。伺服器沒有提供漸層曲線，這是本引擎視覺政策。
- 光圈半徑暫用 `24 × min(radius, 32) × zoom` 像素；保留完整 wire byte。只使用實際繪製物件的腳點，重疊取較亮值。此比例與柔邊不是舊 BIN 的已核定算法。
- 雨用斜線、雪用圓點；強度增加密度與速度，最多 768 粒子。不載入或複製遊戲素材。
- S87 初始化 Light；S120、S206、Reset 自然清除物件光源。全球天氣與時鐘跨圖保留；登出經既有 `OpacityObserved` 重設通知清除。沒有地圖室內／室外遮罩證據，未推測遮雨區域。
- **雨雪碼有來源註解矛盾**：目前採 S_Weather 類別定義，需由 dot 在授權 P24Lab smoke 觀測核對，不宣稱原生相符。
- 離線像素檢查只證明演算法繪製，不代表遊戲畫面或效能已通過真機驗收。
