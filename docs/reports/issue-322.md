# Issue #322：T136 門

## 結論

依主控第 3 輪裁定完成。門的封包、C41、S209 碰撞、開關播放與滑鼠穿門都已實作，離線測試全部通過。CI 結果以 PR #350 最新 head 的 Checks 為準。真機尚未驗收。

## 做了什麼

**前兩輪（保留）**
- `Doors.cs`：解析 S87 DoorPack、S158 門動作、S209 通行狀態。C41 採 HHD，任務摘要的 DHH 與 `C_Door.start` 的讀取順序不符，來源見 [doors.md](../protocol/doors.md)。
- `OnlineMovement.cs`：鍵盤移動查 `DoorBlocks`，實際送出前再查一次。
- `PlayerPointer.cs`：既有的雙擊、Ctrl＋左鍵、右鍵操作遇到門改送 C41，不預測開關。

**第 3 輪（裁定第 1–4 點）**
1. `Doors.cs`：保存播放起始時間與世代。S87 是目前狀態、不播放；S158 28／29 且狀態改變時才開始播放；C41、S209 不觸發。移除、替換、世界重設後舊播放失效。
2. `ObjectRendering.cs`：只改門的辨識與時間選取。播放中用 `ObjectAppearance.At` 的原始時序，沒有播放時停在末幀。一般角色與 NPC 路徑未改，`ObjectAssets.cs` 未改。
3. `PlayerPointer.cs`：`RawStep` 與 `MoveTo`／`PlanStep` 不再把已識別的門算成佔格，改查 `DoorBlocks`。地形、其他物件、斜向穿角檢查保留。`PlayerPointerUi.cs` 未改。
4. `DoorChecks.cs`：補上報告第 4 點的測試，接既有 `doors` 入口。

**裁定以外的一個必要修改（都在 `Doors.cs` 內）**：門身分原本比對 `WorldObject` 實例，HP 這類差量更新會讓門被當成消失，播放也跟著中斷。現在改以完整封包世代識別，同 ID 改送一般 S87 時才解除身分；格式錯誤的封包不影響現有的門。排隊中的 C41 仍比對實例，維持原本較嚴格的失效條件。

**同步時的共用分派修正**：#325 釣魚也在 `WorldSession` 的同一個 `switch` 加了 `case 158`，文字合併沒有衝突，但編譯失敗（CS0152）。已合成一個 case：先 `ObserveFishing`，再 `ReceiveDoorAction`。釣魚用獨立 reader，只有 action 71 才繼續讀，不影響門；`DoorChecks` 補了釣魚與門共用 S158 的回歸測試。

**唯讀核對**：`L1DoorInstance.open()／close()` 只在狀態改變時廣播 S158 再送 S209，與裁定的播放觸發條件一致。

## 怎麼驗證（已驗證）

已合併最新 `origin/main`（含 #325 釣魚），`rebuild-lock.json` 取 main 版本後用 `--update-lock` 重新產生。

- `rebuild.py --update-lock`、`--build` 通過，產品 DLL SHA-256 `1d6eaaa352e7d5519fb63d34c02baa77a25be0be9ddabf19877a50961ab75da4`。
- `DemoChecks` 全部通過。每次都會先跑 193 項基礎檢查（polymorph、pets 等），下表列總數與扣除後的項數：

| 群組 | 總數 | 扣除基礎 193 項 |
|---|---|---|
| `doors` | 514 | 321 |
| `fishing` | 232 | 39 |
| `map-continuity` | 21400 | 21207 |
| `combat-admission` | 1326 | 1133 |
| `polymorph-cadence` | 226 | 33 |
| `buddies` | 362 | 169 |
| `item-transfer` | 266 | 73 |
| `duel` | 307 | 114 |

- `check-no-game-assets.py` 通過，0 errors。

`doors` 新增涵蓋：
- **滑鼠**：開門穿越、S209 未到時可穿越、關門拒絕與繞路、規劃後與排隊中關門、S158 不取代 S209（兩個方向）、開門仍受地形與其他物件限制、斜向穿角。
- **播放**：經由真正的 `Renderer.ComposeObjects`，用合成 3 幀與 1 幀素材驗證首／中／末幀、S87 停末幀、重複回包、反向切換、S209 與 HP 更新不影響、移除／替換／ID 重用／世界重設失效、損傷與毀壞不播放、未核驗 gfx 不繪製。

本機 NuGet.org 還原有 TLS 問題，改用 `scratch/issue-322/NuGet.Config` 唯讀引用原專案 `engine/vendor`。

## 沒驗什麼

- 未啟動遊戲、伺服器或 DB，沒有真機的開關門畫面、音效或 C41 來回。
- 測試用的是合成素材，真實 gfx 2346 素材沒有在本輪載入渲染。前一輪已唯讀核對它的開、關各只有 1 幀，依驗收口徑顯示這 1 幀並在 S158 時切換。

## 已知限制

- 單次左鍵仍只選取門，開關要用雙擊、Ctrl＋左鍵或右鍵。本輪裁定不做單擊入口。
- 其他 gfx 及損傷、毀壞動作沒有已核驗素材，列為缺素材、不繪製。要支援需另派 resource 任務，也要解除 `ObjectAssets.GetDoor` 的 gfx 2346 限制。
- CHARPACK 沒有型別碼，門靠完整固定特徵辨識；沒有這個特徵的 gfx 2346 物件仍算一般佔格，也不會播放。
- S209 不含物件 ID，S120 不清除通行狀態，要等下一個 S209 或世界重設。

## 產出路徑

- 產品：`engine/source/Doors.cs`、`ObjectRendering.cs`、`PlayerPointer.cs`、`OnlineMovement.cs`、`WorldSession.cs`
- 測試：`engine/_integration/dev/doors-tests/`
- 協定：`docs/protocol/doors.md`
- 本報告；未改 `docs/REPORT.md`

## 給下一棒的提示

- 真機項目交 dot 的 M6 smoke：在 P24Lab 開關門、確認 gfx 2346 在 S158 時切換、開門後滑鼠能穿過、關門後被擋。
- 若驗收要看到多幀的門，另派 resource 任務查證原始素材，不要插造幀或改封存素材。
- `DemoChecks` 的 `Main` 每次都先跑基礎檢查，看測試數字時要扣掉這 193 項（數字會隨 main 增加）。
- 合併後要重新建置再看測試：`--no-build` 建置失敗時仍會跑舊二進位並顯示 PASS。
