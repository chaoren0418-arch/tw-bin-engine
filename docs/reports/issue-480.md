# #480 T178：天氣對齊原版（G6）

依據：`docs/reference/ambience-and-map-events.md`（AM）§2；分組見 `docs/reference/m6-native-diff.md` §3 G6。

G6 改的是與 G5（#479／PR #491）同一個 `Ambience.cs`，「雨雪 −1」與「先畫天氣再一起變暗」都要用到 G5 的明暗等級 L 與係數表。**PR #491 已合併進 main；2026-11-08 本分支已與 `origin/main`（`8815978c`）同步**，現在 PR 差異只剩 G6。

## 做了什麼

| AM§2 項目 | 實作（`engine/source/Ambience.cs`） | 狀態 |
|---|---|---|
| (a) 解碼：先清 0；`n=v&0x0F`，n>3 視為 0；高半位 0x00 雨、0x10 雪、其他晴 | `AmbienceWeather.Decode`；`AmbienceFrame` 新增 `Rain`／`Snow`（0–3），`Kind`／`Intensity` 由兩者推出，雨優先 | 已驗證（全部 256 碼） |
| (a)5 n>0 且 L≥10 時，**每包** L 減 1 | `Ambience.WeatherReceived`（不重算曲線；曲線本身的 −1 由 G5 在重算時套用） | 已驗證 |
| (a)6 地圖 7701–7710、7771–7780 雨量強制 2 | `AmbienceWeather.ForcedRain`，只在收到 S115 時套用（與原版相同，換圖本身不觸發） | 已驗證（含 7700／7711／7770／7781 邊界） |
| 移除物件或動作 8：gfx 10683／10632 清雪、10891 清雨 | `WorldSession.ObserveWeatherObject`：在物件處理前讀出 gfx（處理後物件已經被移除）；共用分派點只在既有的「預先觀察」switch 加一組 `case 120: case 158:` | 已驗證 |
| (c)(d) 粒子數：雨 128／256／512、雪 48／96／144，取前 n 顆 | `AmbienceWeather.RainCount`／`SnowCount`、`AmbiencePainter.ParticleCount(frame)`（不再依畫面面積計算） | 已驗證 |
| (c) 雨：512 顆的池；每 256 顆分 4 層；24 ms 一個 tick；層 3／2／1／0 分別每 4／3／2／1 個 tick 移 1 px；落點；`sin(t·π/144)·8` 擺動；x 模 800、y 模 485；鏡頭補償 | `WeatherParticles`（`Step`／`Active`／`Reset`），亂數用 MSVC `rand()` LCG，固定 seed 可重現；每幀時間差上限 500 ms | 已驗證 |
| (d) 雪：192 顆的池；大小與週期表；每幀 `x -= rand%(3n)`（x≤0 回到 800）、`y += 5n`；落地後 y 歸 0，新落點 `(4−大小)*64 + (505−(4−大小)*64)*rand/0x7FFF` | `WeatherParticles.Step` | 已驗證 |
| (b) 先畫雨雪，再一起變暗 | `AmbiencePainter.Draw`：先 `DrawWeather`，再用 G5 的係數表變暗 | 已驗證（用合成 sprite 測試） |
| 粒子圖 | **不畫**。舊的程序直線雨與圓點雪（替代圖）已移除；新增 `WeatherSprite` 委派，`Draw(..., particles, sprite)` 只有在有 sprite 來源時才畫 | 等 T167 |

### 對文件的解讀（文件沒寫死的細節，請下一棒留意）

- **雨的落地與重生**：文件寫「到達落點後 y 停住、階段值增加」，又寫「`y − 5*(4−層) ≥ 落點` 時重新出生」。我的實作：落地後 y 固定在落點，每次移動階段值 +1，`階段值 ≥ 5*(4−層)` 時重生（層 3 走 5 步、層 2 走 10 步）。兩種寫法在數學上一致。
- **重生與初始 y**：重生時 y=0、x 隨機；初始 y 是 `rand%485`（超過落點時設為 0）。文件沒有寫，屬推測。
- **擺動**：當成顯示時的位移，不累加到 x，否則 x 會一直漂移。
- **雪公式裡的 n**：解讀為強度（與 AM§2(a) 的 n 相同），不是雪花大小。這點**待驗證**。
- **鏡頭補償**：`Step(frame, ms, shiftX)` 把 shiftX 直接加到雨的 x。正負號由呼叫端決定，因為原版的正負號沒有寫出來。
- 雨層 1／0 的落點是 485，y 模 485 不會到達落點，所以這兩層只是一直循環的雨絲。

### 沒做的項目（依「推測／待驗證不實作」）

- 地圖設定 `sub_926DA0()->[4]` 停用天氣：欄位意義待驗證。
- 地圖腳本的 `Rain`／`Snow` 指令：引擎沒有地圖腳本。
- 程序圖樣（雨層 2／3 的十字與 4×4 圖樣、`sub_9755E0` 雪花）與 Surf −100..−97：依任務規定，沒有原圖就不畫，也不用替代圖。`WeatherParticle` 已提供 `Layer`、`Stage`、`Landing`，sprite 實作要的資料都有（例如 alpha `16/階段`）。
- 畫面掛接：`Renderer.PaintAmbience` 目前沒有呼叫端（T147 起就是如此）。逐幀的 `Ambience.Particles.Step(...)` 要在畫面掛接時一起加上。

## 怎麼驗證

（以下為與 main 同步後重跑的結果，2026-11-08）
- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock`，再加 `--build`：PASS（product DLL `5c10255c…4603`）。
- `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/ambience.json ambience`：**PASS 9881**。
- 同一個指令改跑 `map-continuity` 群組（CI 會跑，裡面包含 AmbienceChecks）：**PASS 32436**。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：pass（3667 files，0 errors）。
- 測試在 `engine/_integration/dev/ambience-tests/AmbienceChecks.cs`（新增 `WeatherParticleChecks`），已經接在既有的 `ambience`、`map-continuity` 與預設群組。涵蓋：
  - S115 全部 256 碼。
  - 每包減 L 的時機：中午、黃昏 L=13、夜晚不減、清除與 n>3 不減。
  - 強制雨地圖的邊界，以及「換圖本身不觸發」。
  - gfx 清除表與 S120 完整分派；S158 動作 8 直接呼叫觀察點；格式錯誤的 S120／S158 不改天氣。
  - 粒子數表、層與速度、tick 與 500 ms 上限、擺動振幅、鏡頭位移、長時間跑的範圍與重生、雪的移動與落點範圍、固定 seed 可重現。
  - 沒有 sprite 時不改任何像素；夜晚用合成白色 sprite 測試，雨點變暗成 `白×10/31`。
- 沒有新增 C 封包。S 格式只用到 S115 `c`、S120 `d`、S158 `d c`（已對照 `server-protocol.md`）。

## 沒驗證什麼

- 同步前 CI 已綠（run 42406401685／42406401669）；同步後的 CI 結果見 PR #494。
- S158 動作 8 的**完整分派路徑**：後面會進入戰鬥動畫，需要素材目錄，離線環境沒有素材，所以測試直接呼叫觀察點。
- 3.81 伺服器實際會送哪些天氣值，以及這些 gfx 物件是否真的會出現。
- 實機畫面：沒有圖，所以也沒有畫面。
- LiTo 3.81 與 TW16 是否相同（AM§8 第 7 項）。

## 產出路徑

- `engine/source/Ambience.cs`（`AmbienceFrame.Rain`／`Snow`、`AmbienceWeather`、`WeatherParticle`、`WeatherSprite`、`WeatherParticles`、`Ambience.WeatherReceived`／`WeatherObjectGone`、`AmbiencePainter.Draw`／`ParticleCount`）
- `engine/source/WorldSession.cs`（預先觀察 switch 加一組 case）
- `engine/_integration/dev/ambience-tests/AmbienceChecks.cs`、`README.md`
- `engine/_integration/dev/rebuild-lock.json`
- 分支 `task/480`

## 給下一棒的提示

1. #491（G5）已合併，本 PR 可直接審。之後與 main 同步時，`rebuild-lock.json` 一律用 `rebuild.py --update-lock` 重產。
2. T167 找到雨雪的圖以後：實作一個 `WeatherSprite`，依 `particle.Kind`／`Layer`／`Stage` 選 Surf −100..−97 或程序圖樣，傳給 `AmbiencePainter.Draw(..., Ambience.Particles, sprite)`。
3. 畫面掛接時：每幀先呼叫 `Ambience.Particles.Step(frame, 這幀的毫秒數, 鏡頭 x 位移換算成 800 寬的像素)`，再呼叫 Draw。
4. 待查：雪公式的 n 是強度還是大小；鏡頭補償的正負號；`sub_926DA0()->[4]`。
