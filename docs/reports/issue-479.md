# Issue #479 報告：T177 日夜時鐘與明暗曲線對齊原版（G5 第一步）

依據：`docs/reference/ambience-and-map-events.md`（以下簡稱 AM）§1、§3；分組見 `docs/reference/m6-native-diff.md` 第 3 節。

## 做了什麼

修改檔案：`engine/source/Ambience.cs`、`engine/source/Renderer.cs`（只改 `PaintAmbience` 的簽章）、`engine/_integration/dev/ambience-tests/AmbienceChecks.cs`、`engine/_integration/dev/rebuild-lock.json`（用 `--update-lock` 重產）。共用分派點（`WorldSession.cs` 的 case 123）沒有改。

1. **不在本地推進時鐘**（AM§1(a)，對應 AM§7 #1）：拿掉原本 6 倍速的本地推進。等級 L 只在三種時機重算：收到 S123（S_TIME）、S_STATUS 的 `Time` 有變化、`MapId` 有變化（換圖）。S_STATUS 與換圖用「觀察 session 欄位變化」的方式得知，所以不用改共用分派。
2. **曲線**（AM§1(c)，`sub_EDB7D0`）：`AmbienceCurve.Level`，以一天中的分鐘數計算：06:00–06:40 變亮、20:00–20:40 變暗，等級 1–15；整數除法；等級 ≥ 10 且下雨或下雪時 −1。秒數取 `% 86400`，`gmtime32` 對 ≥ `0x80000000` 的值所做的修正（減 23376 整天）不影響時分。
3. **每幀等級**（AM§1(e)(f)，`sub_EFE110`／`sub_F11FA0`）：`AmbienceCurve.FrameLevel` 依序處理地圖覆寫（1000→6、2006→2、1931／1932→15、4995–5122→8、≥16384→8）、< 15 時 +1、自身光源旗標 19 +1／20 +5、夾到 0–15、職業 +2（上限 15）。
4. **特效暫時提亮 +5**（AM§1(g)）：`Ambience.BeginLightEffect`／`EndLightEffect` 用計數處理：第一個特效開始時記住 L，並設成 min(15, L+5)；計數歸零時還原；期間曲線不覆寫 L。
5. **係數表取代深藍覆蓋**（AM§1(h)(i)）：內建 3 模式 × 16 等級 × 32 項的表。每個像素取 f = table[mode][FrameLevel][光照值>>3]，每個色頻先降到 5-bit，算 c*f/31 再展開回 8-bit；alpha 保留；f == 31 不動；只有 L < 15 時才套用。天氣改成先畫、再一起壓暗（AM§2(b)，順帶修正 AM§7 #8）。
6. **光源值換算**（AM§3(a)）：S40 收到時先清掉兩個自身旗標。自己：< 5→5、19→10（旗標 19）、20→14（旗標 20）；別人：20→5。光源圖索引 k = min(15, 值)，每幀預算為 k 的總和 ≥ 700 時停止（`WorldSession.AmbienceLights`）。
7. **光源圖介面**（留給 T167）：`AmbienceLightMap`（400×240，半解析度），`Stamp` 依 `sub_97D2F0` 的規則置中，左邊界 `((x−w)>>1)&~3`、上邊界 `(y−h)>>1`，重疊取最大值。**本張不載入、也不畫 `source.lmp`，也沒有用替代圖**：預設光照圖為空，光照值 0（未照亮）。

## 怎麼驗證（已驗證）

- **係數表與 BIN 逐 byte 相同**：用 `docs/reference/rea-scripts/nat.py`（核對 BIN SHA-256 `e12949cf…641a2`）唯讀讀出 VA `0x1374168`／FO `0xF72968` 的 1536 bytes，與 `Ambience.cs` 內的表比對結果為 True（腳本 `scratch/t479_table.py`，未入 git）。
- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock` 後 `--build`：PASS，product DLL SHA-256 `62e57993…ba34`。
- 離線測試：`dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/ambience.json ambience` → `PASS 5597 DEMO checks`。測試已接在既有入口的 `ambience` 與 `map-continuity` 群組。涵蓋：
  - 時間→等級表：25 個錨點（含 06:03=2、06:38=15、20:03=14、20:38=1）、整天 1440 分鐘的範圍、忽略秒數、雨雪 −1、高位秒數。
  - 每幀等級：20 組，涵蓋地圖覆寫、旗標 19／20、職業 +2、+1、夾值。
  - 光源換算表：0–255 全部的自己值、別人值與索引 k。
  - 係數表：3 模式 × 16 等級的未照亮欄、全亮欄、單調性與內部抽樣；`word_14325C0` 的 32×32 全表；8-bit 色頻向量。
  - 像素向量：夜晚 (248,132,66) → (82,41,16)，即 f=10；另有職業、地圖 1000、旗標 20、L13、模式 2，以及光照圖 255／128 的像素。也檢查 viewport／clip 限制、alpha 保留、雨被一起壓暗。
  - 合成封包：S123、S115、S40 的截斷、非零 padding、多出區塊一律拒收且不改狀態；S_STATUS 時間變化、換圖重算；不在本地推進（+1 小時仍同一等級）；S123 仍推進 ready probe 與船班時鐘各一次。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → pass（errors 0）。

## 沒驗證／沒做什麼

- （已驗證）CI：PR #491 的 dotnet、map-continuity、npc-offline 三項全綠。合併 main 後本機重跑：`ambience` PASS 5648、`map-continuity` PASS 28125。
- **真機畫面**：沒有啟動遊戲，沒有實機比對。
- **產品沒有呼叫 `PaintAmbience`**：在 HEAD 中，產品程式沒有任何地方呼叫 `Renderer.PaintAmbience`／`AmbiencePainter.Draw`，只有測試會呼叫，所以實際畫面還看不到明暗。接到場景繪製要另開任務，需要傳入場景 Bitmap。
- 文件標「推測」或「待驗證」的項目，本張沒有實作，維持現狀：
  - 職業夜視 `byte_1542AA1`：`NightVision` 介面已留好，但 session 不會設定它，因為設定這個值的封包還沒查到，「妖精系」的意義也屬推測。
  - `byte_15442BC` 與 `sub_F150B0` 地圖表、`dword_138BE64`（5140／5143）：不套用。
  - 表模式 1／2（`byte_154461E`、盲目 B==2）：session 固定用模式 0，盲目屬另一張任務。
  - `sub_500EA0(1,3)` 關閉明暗的選項、天空進度 990–995。
  - 光源 ±35／±15 距離篩選與動作偏移（欄位意義未確認）。
- 天氣附帶規則（AM§2(a) 第 5 步：每收到一包天氣、L ≥ 10 就減 1；地圖強制下雨等）屬於 AM§7 #5，不在本張範圍，沒有實作。
- 呼叫 `BeginLightEffect`／`EndLightEffect` 的特效物件判定（物件 `+12`／`+9`）還沒接上，只提供 API 與測試。

## 產出路徑

- `engine/source/Ambience.cs`、`engine/source/Renderer.cs`
- `engine/_integration/dev/ambience-tests/AmbienceChecks.cs`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/reports/issue-479.md`
- 分支 `task/479`

## 給下一棒的提示

- **T167（光源圖）**：拿到 `source.lmp` 後，在每幀先 `AmbienceLightMap.Clear()`，再對 `WorldSession.AmbienceLights(...)` 回傳的每個光源呼叫 `Stamp(center.X, center.Y, 第 k 張圖, w, h)`，最後把光照圖傳給 `PaintAmbience(graphics, target, viewport, frame, lightMap)`。座標要先換算成 800×480 的畫面座標。
- **場景接線**：`PaintAmbience` 需要場景 Bitmap（32bppArgb），因為係數會直接改像素。要在 UI／HUD 之前呼叫，並避開 picking／owner 緩衝。
- 職業 +2 要等確認 `byte_1542AA1` 的來源封包，再由 session 設定 `AmbienceFrame.NightVision`。
