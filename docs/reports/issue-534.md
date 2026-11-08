# Issue #534 T227：查證原版——地圖特效、子特效、水中呼吸與座標特效

階段 resource，第 1 次嘗試。分支 `task/534`。

## 做了什麼

- 把 P01 IDB 複製到 `scratch/t227/ida/tw16.idb`（SHA `3a0bf206…e82f`，前後相同），經 subst `Q:`（事前確認沒有占用、事後解除）用 IDA 7.7＋Hex-Rays 反編譯 21 個函式，並對全 `.text` 做立即值掃描。
- 寫了 4 支唯讀腳本（`docs/reference/rea-scripts/t227_*.py`）：光源表重現與 `.s32` 分布、list.spr `]`／`{`／`}` 統計、登入 XML 搜尋、IDA 立即值掃描。
- 產出 [`docs/reference/effects-2.md`](../reference/effects-2.md)；更新 `INDEX.md`；回填 `gap-matrix.md`（第 1 節 op 17／219、第 4 節 LoginUI／SelectUI／CreateUI／QuitWindow／CNoticeWindow、第 5 節 `F0B570`／`]`、第 8 節四列、依據代號 EF2）。

## 結論

1. **`A4E5D0` 第 7 參數＝光源半徑 k**（寫入 `actor+9`，與 S40 同欄位，`F11FA0` 用它選 `source.lmp` 第 k 張）。3442–3446 是 list.spr 的 `dot range five…thirteen`，1 像素的隱形光源點，k=5、7、9、11、13。本機 26,116 筆地圖特效中 60% 是光源。引擎沒有這張表，光照圖也還沒接上畫面。
2. **`]` 子特效**：`A4B060`／`A4BA20`（以及特效自己的 `8ABC10`）在 `]N` 格建立預設 `Effect`（`0x2A0`，`sub_8AB390`），位置與方向取當格擁有者，複製狀態旗標，播完自刪，不看 109 kind。list.spr 有 2,812 處、485 個 sprite。附帶：`{`／`}` 是淡入／淡出。引擎完全沒處理。
3. **S_BREATH**：只加狀態圖示 effect 11（`552.tbt`），沒有畫面特效；3.81 對應 S126。引擎的計時一致，差異只在圖示（屬既有圖示任務）。
4. **S_EFFECT_LOC**：一律建預設 Effect、方向 0、播完自刪；與引擎（T105）一致。2028–2056（爆竹、仙女棒）會設光源 1，但 3.81 伺服器的煙火走 S55，不走 S106。
5. **登入 XML**：掃描本機 2,432 個 `.idx` 與所有鬆散檔名，`LoginUI`／`SelectUI`／`ServerName`／`LoadingScene` 都沒有，列為**缺資源**。`CreateUI.xml` 只在 `2-登入器\經典UI相關實作\…\收藏UI-7142` 找到他版本（89 參考 PAK）的候選，15 個事件名稱都在 TW16 BIN 中，但 surf 7091–7225 不在本機客戶端。

## 怎麼驗證

- 反編譯輸出逐行對照（行號與地址寫在文件裡）；`.data` 表直接從 BIN 讀出。
- 3442–3446 的語義用三個獨立來源交叉確認：`F0B570` 的公式、list.spr 名稱、封裝後的像素內容。
- `t227_light_census.py` 重算的總筆數（26,116）與種類數（521）與 REA B04 相同。
- `CreateUI` 候選的相容性用「事件名稱是否存在於 BIN」核對。

## 未驗證什麼

- TW16 S55 處理（`sub_8676F0`）是否也對 2028–2056 設光源；`Effect+9=1` 是否另外蓋光源圖。
- `{`／`}` 的計時單位；`actor+198` 的用途。
- `CNoticeWindow`、`QuitWindow` 的寫死版面；`LoadingWindowUI` 是否用在 3.81 世代。
- `.idx` 用 28-byte 舊格式解析；若有新格式 idx 可能漏找。
- 全部是靜態分析，沒有實機觀察。

## 產出路徑

- `docs/reference/effects-2.md`（主文件）
- `docs/reference/INDEX.md`、`docs/reference/gap-matrix.md`（回填）
- `docs/reference/rea-scripts/t227_light_census.py`、`t227_token_census.py`、`t227_xml_hunt.py`、`t227_imm_scan.py`
- 本報告 `docs/reports/issue-534.md`
- 中間產物（不進 git）：`scratch/t227/`（IDB 副本、反編譯輸出、統計 JSON）

## 給下一棒的提示

- **可以開 code**：
  - **FX1 地圖特效光源表**（`MapEffects.cs`、`Ambience.cs`）：依 effects-2 §1.1(b) 的表。**要先等光照圖接上畫面**，因為 `AmbienceLights`／`AmbienceLightMap.Stamp` 目前沒有呼叫者。
  - **FX2 `]` 子特效**（`ObjectRendering.cs`、`MagicRendering.cs`、`MapEffects.cs`）：需要先在素材封裝加入 `]` 目標 gfx。FX2b `{`／`}` 可以併入。
- **不用另開**：S_BREATH（圖示差異併入狀態圖示任務）、S_EFFECT_LOC（已一致）。
- **仍查不到**：`LoginUI.xml`、`SelectUI.xml`、`ServerName.xml`、`LoadingScene.xml`。需要使用者提供 TW16 世代的資源，或改以 3.81 實機截圖為準（`needs:user`）。
- 若要查 `CNoticeWindow`／`QuitWindow`：從 `sub_8C7C80` 找它們的建構子再反編譯，不需要 XML。
