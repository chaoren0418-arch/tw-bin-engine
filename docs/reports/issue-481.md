# #481 T179：異常狀態外觀對齊原版（G7）

依據：`docs/reference/ambience-and-map-events.md` §4（AM，T162／#430）、`docs/reference/m6-native-diff.md` G7。所有「原版」指 TW16 客戶端；3.81 是否相同屬**推測**，要等 M6 smoke 實機比對。

## 做了什麼

產品程式只改 `engine/source/Afflictions.cs`、`TimedBuffs.cs`、`Invisibility.cs`，共用分派點沒有改動（S165／S47／S103／S171 的 case 原本就有）。

| 項目 | 改動前 | 改動後（已依 AM 實作） |
|---|---|---|
| 中毒 | 以亮度重算 `(L/3,L,L/3)`，無脈動 | 只有 G 加 `k*8/16`（8–14），`k=16+|12-(t%24)|`，t 每 50 ms 加 1，週期 1.2 秒三角波；R、B 不變 |
| 麻痺 | 灰階 `(L,L,L)` | 每色頻 `3 + c/2` |
| S165 綠＋灰 | 丟 ProtocolException | 灰優先，不丟例外 |
| S165 綠 ≥ 8 | 綠 > 1 丟例外 | 整包忽略，狀態不變 |
| S165 ID 0、S47 值 > 2 | 丟例外 | 忽略，不丟例外（截斷、補齊非 0、多餘區塊仍拒收） |
| 盲目 1 | 自己以外全黑 | 每像素乘 `f/31`，f = 自身 k=5 光圈的光照索引；未照亮處全黑；光圈內的別人也會被照亮 |
| 盲目 2 | 全畫面色頻 /4 | 未照亮處 4/31，光圈內升到 31 |
| 酒醉 | 整個畫面平移（x 2×、y 1×） | 逐掃描線水平位移 `(int)(醉度*3.5*sin((相位+j)*π/50))`，相位每 50 ms 加 1、200 循環（5 秒） |
| 別人隱身 | 半透明畫出 | 預設不畫（連影子），沒有像素、深度、點選；自己仍 50% 混合 |

酒醉的掛載方式：`Program.cs` 不在範圍內，所以 `PaintLiquorWave` 在 Paint 事件裡把原本的 `OnPaint` 畫到離屏畫面、逐行位移後畫上，再把外層剩餘繪製移出可見範圍。酒醉時每幀畫兩次場景。

協定文件以附加段落更新：`docs/protocol/afflictions.md`、`invisibility.md`、`timed-buffs.md`。

## 推測項目：未實作，維持現狀

- **別人隱身的可見例外**（`$431`、`sub_956D30` 隊伍／血盟）：意義未知，**不做例外**，所有別人的隱身一律不畫。`m6-native-diff.md` G7 建議的「隊伍／血盟可見」也沒有做，以 Issue 的指示為準。
- **盲目 2 讓特定種類物件發光**（102 表 {5,6,10,12}）：**不做**。
- 盲目 2 的職業加成（6/31）：**不做**，一律 4/31。
- 光圈形狀：`source.lmp` 未解讀，沿用引擎既有光源半徑（`AmbiencePainter.Radius(5)`＝120 場景像素）與平滑邊緣；盲目 2 光圈內的係數用線性內插。
- 酒醉：`sub_81B0E0()` 為假時位移減半、地圖 67／420 最低醉度 1：都沒做；行兩端空出的欄位重複邊緣像素。
- 中毒脈動的起始相位：原版計數器起點未知，只對齊週期與振幅。

## 怎麼驗證

1. `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock`，再 `--build`：PASS（合併 main 後 product DLL `06098f57…11fd`）。
2. 離線測試（產品 DLL、合成封包、手寫 RGB555 像素；不連網、不開遊戲）：
   - `afflictions`：PASS 580。包含中毒 24 個相位的綠色加量表、峰谷與 1.2 秒週期、G 飽和；灰的白／黑／紅向量；`c*f/31` 向量；盲目係數與光圈衰減；S165 綠＋灰、綠 7、灰 255、綠 8／9／255 整包忽略、ID 0、S47 值 3／255／256／65535 都不丟例外；合成時盲目 1／2 的未照亮、光圈、照到別人，深度與點選不變；別人隱身在合成裡沒有像素也不能點選。
   - `invisibility`：PASS 397（自己半透明、別人隱藏、影子跟隨擁有者、轉場中的別人也隱藏）。
   - `timed-buffs`：PASS 645（相位 50 ms／200 循環、18 筆位移表、振幅 3／7／10、垂直週期 100 行、相位 200 回原點、逐行移位與邊界、`WarpLiquorFrame` 對 Bitmap 的實際移位）。
   - 合併 main 後，CI 的全部群組都跑過：`map-continuity` 23272、`combat-admission` 1686、`item-status` 409、`polymorph-cadence` 380、`buddies` 496、`item-transfer` 400、`doors` 648，另跑 `title-emote` 553，全部 PASS。
3. `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：pass，errors 0。
4. CI：見 PR。

## 沒驗證什麼

- 沒有啟動遊戲，沒有實機畫面比對。中毒閃爍速度、盲目光圈大小、酒醉波形、別人隱身消失，都要在 M6 smoke 用肉眼確認。
- 酒醉的離屏重畫只在 Bitmap 層級測過，`PaintLiquorWave` 本身沒有在真視窗裡跑過；效能和閃爍要實機看。
- 網頁場景（`PublishSceneWeb`）不套用酒醉波形。
- 3.81（LiTo）是否和 TW16 相同。

## 產出路徑

- 程式：`engine/source/Afflictions.cs`、`engine/source/TimedBuffs.cs`、`engine/source/Invisibility.cs`、`engine/_integration/dev/rebuild-lock.json`
- 測試：`engine/_integration/dev/afflictions-tests/AfflictionChecks.cs`、`invisibility-tests/InvisibilityChecks.cs`、`timed-buffs-tests/TimedBuffChecks.cs`
- 文件：`docs/protocol/afflictions.md`、`invisibility.md`、`timed-buffs.md`（附加段落）、本報告
- 分支：`task/481`

## 給下一棒的提示

- M6 smoke 核對清單：(1) 中毒角色綠色約每 1.2 秒閃一次，不會變成整片綠；(2) 麻痺角色變暗偏灰，不是純灰階；(3) 盲目 1 只看得到自己周圍一圈；盲目 2 整片很暗，但仍隱約可見；(4) 喝酒後畫面呈水平波浪，約 5 秒一輪；(5) 別人隱身後完全看不到，包括影子與名字。
- 想要「隊伍／血盟看得見隱身」或「盲目 2 讓怪物發光」，要先在 IDB 查清 `$431`、`sub_956D30` 與 102 表種類，再開新任務；都只動 `Invisibility.cs` 或 `Afflictions.cs` 的 `ApplyBlindLight`。
- `source.lmp` 解讀出來後，換掉 `AfflictionColors.BlindLight`／`BlindLightRadius` 即可；明暗表完整列印出來後，換掉 `BlindCoefficient` 的線性內插。
- `Renderer.ApplyScreenShake` 已經沒有產品呼叫者（在 `Renderer.cs`，不在本任務範圍），可以另外清掉。
