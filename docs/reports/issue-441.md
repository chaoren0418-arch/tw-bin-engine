# Issue #441 — T169：表情對齊原版（code，第 1 次嘗試）

依據：`docs/reference/actions-and-names.md`（T160／#428）第 1 節、3.4 節與第 6 節差異表 #1–#6。

## 做了什麼

只改 `engine/source/TitleAndEmote.cs`（表情部分與同檔的 `EmoteSprites` 缺圖退回），以及測試 `engine/_integration/dev/title-emote-tests/`。沒有動共用分派點（`WorldSession.ReceiveCore` 的 S158 case 原本就在）。

| # | 項目 | 改動 |
|---|---|---|
| 1 | 順序 | `TitleAndEmotePackets.MenuActions = [68, 69, 67, 66]`（敬禮、歡呼、挑釁、思考）；下拉選單照這個順序。`SendEmote(stamp, item)` 改收項目 1–4，不再收動作值 |
| 2 | 依外型重映射 | `TitleAndEmotePackets.Plan(gfx, item, menu)` 照 1.2 節實作兩張表：動作槽路徑（`sub_ADACB0`…）與選單路徑（`sub_8C14F0`）。群組 G1、G1'、G2（6080／6094）、G3、G4，以及 10413、3750／3775／3858、11635／11650。UI 是選單，所以走選單路徑 |
| 3 | 本地播放與節流 | 立即型：送出被接受後馬上在本地播放，沒有冷卻。節流型：只送封包、不在本地播放，間隔 `ThrottleMs()` = 2500 ms，間隔未到就不送。`ThrottleMs(flag547, speedA, speedB)` 照文件實作 2265 ms 與兩組速度係數，但呼叫端一律傳預設值（見「沒驗什麼」） |
| 4 | 送出條件 | 表情只擋忙碌動作：死亡或 HP 0、攻擊（`CombatBusy`）、施法（`MagicBusy`）、撿物動作（`GroundBusy`）、自己的表情還在播放。**不再擋**移動中（`MovementBusy`／`Walking`）、商店姿勢（`Weapon == 70`）、`Flags & 2`、交易。另外保留連線層的安全條件：stamp 有效、在世界中、`World.Ready`、`!ActionLocked`（傳送／地圖載入／麻痺）、同時只能有一個待送請求。送出前如果外型（gfx）變了就拒絕，避免拿舊外型的重映射送出。稱號（C90）的條件不變 |
| 5 | 缺圖退回 | `EmoteSprites.Load(gfx, action, dir, pose)` 回傳可為 null。動作沒有影格時退回姿勢動作，再退回動作 0（`EmoteSprites.Fallback`，119／120 不退回）；三者都沒有就回傳 null，表情直接結束，不丟例外、不顯示「無法顯示表情原圖」。缺的情況包含：metadata 沒有該列或該動作、sprite 檔不在任何 pinned idx、影格索引超出範圍。`ReadSprite` 缺件改成回傳 null。格式錯誤和 SHA 不符仍然丟例外（不屬於缺圖）。`TryParseSteps` 接受動作 0–120，也支援 `(0 n,…)` 的單方向列 |
| 6 | 時長 | 照舊依 sprite 影格（metadata 的 tick 與 fps）播完後由 renderer 結束；退回的動作也用它自己的影格時長。10 秒的上限只當保護。自身表情播放中算「忙碌」，所以職業外型不能連按插播 |

## 怎麼驗

- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock` → `--build`：全部 0，`PASS dev`。之後跑唯讀核驗，回報 `dev-inputs-verified`。
- 離線單元測試，都用合成封包與記憶體 sender，不需要遊戲資源：
  - `DemoChecks … title-emote`：**PASS 779**。C120 `cc` 格式、C90 `ss`、S158 `dc`、S183 `ds`、兩張重映射表逐列核對、節流邊界（2499 ms 拒絕、2500 ms 接受）、放寬後的條件、送出前換外型會拒絕、退回鏈。
  - CI 跑的群組 `combat-admission`：**PASS 1912**（內含 title-emote）；`polymorph-cadence`：**PASS 309**。
- 本機唯讀原圖核驗（只讀 `D:\lineage開發\0-客戶端`，沒有複製）：
  - `title-emote-assets` **PASS 441**：原本 13 個 gfx 的四個表情；另外核對實際的 6080 列：66 退回姿勢 20、姿勢 4 缺圖時退回 0、68 用自己的影格。
  - `title-emote-compose` **PASS 290**。
- `engine/tools/check-no-game-assets.py`：pass，錯誤 0。
- CI：推上去後看 PR 的檢查結果（本報告寫成時還沒跑完）。

## 沒驗什麼／刻意沒做

- **沒有真機**：沒有啟動遊戲或伺服器。3.81 伺服器在變身中只接受 6080／6094 的表情；其他變身外型照原版仍以節流型送出，預期伺服器會忽略，這點還沒有真機確認。
- **`hold=1` 的 1234 ms 計時器**（`sub_4F94F0`）：文件標「推測」，沒有實作。`EmotePlan.Hold` 只記錄旗標。
- **節流的速度調整**：`ThrottleMs` 的公式已照文件實作並有測試，但「自身 +547」與兩個加密速度狀態（`0x14EBF88`、`0x14EC00C`）和引擎的 Speed／Brave 怎麼對應，文件沒有查證，所以呼叫端固定用 2500 ms。
- **`0x153CE5F`**：文件沒有說明語義，只用引擎既有的忙碌狀態（攻擊、施法、撿物、死亡、表情播放中）近似 `sub_A3EBF0`。這是推測。`ActionLocked` 是引擎自己的連線安全條件，原版沒有，所以保留。
- **3.4 第 3 點**（sprite 檔不存在時改用 gfx 1120）：不在本任務範圍，沒有做。
- 選單路徑中，文件沒有寫到的組合（例如 10413 的第 2、3 項、3750／3775／3858 的第 2–4 項）沿用動作槽路徑的行為，這是推測。
- 退回時，播放的是**姿勢動作**（3.1 節：姿勢本身就是走路動作），沒有另外對到待機動作，這是照文件字面做的。
- 選項的中文名稱（Text.pak）文件標「待驗證」，沿用原本的名稱。

## 產出路徑

- `engine/source/TitleAndEmote.cs`
- `engine/_integration/dev/title-emote-tests/TitleAndEmoteChecks.cs`、`README.md`
- `engine/_integration/dev/rebuild-lock.json`（`--update-lock` 重產）
- `docs/reports/issue-441.md`
- 分支 `task/441`

## 給下一棒的提示

- 真機 smoke 要確認的事：職業外型四項的順序與本地動畫；6080／6094 騎馬頭盔送出的是 68／69／67（選單），第 4 項是節流 66；一般變身按表情時本地不播放，間隔 2.5 秒；移動中按表情可以送出，開始走路會中止本地動畫。
- 要把速度係數接上，需要先查出 `0x14EBF88`、`0x14EC00C` 與引擎 Speed／Brave 的對應，再把值傳進 `ThrottleMs`。
- 如果要做 `hold`，要先反編譯 `sub_4F94F0`。
