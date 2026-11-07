# Issue #322：T136 門（第 2 次 code，待範圍裁定）

## 第 2 次執行結論（2026-11-07）

**未完成；本輪只補充唯讀核對與可裁定的修正範圍，未修改產品或測試。** 起始分支為既有 `task/322`，HEAD 為 `bc18900f9573bbebd2dbfb6c6fc9a426d19211db`，工作目錄乾淨；沒有切換分支、移除 worktree 或遞迴刪除。

已讀根目錄 `AGENTS.md`、`docs/REPORT.md`、M6 契約與協定／規則表，並查詢 Issue #322 本文、全部 6 則留言及 PR #350。最新留言只有重新派工；[審查退回](https://github.com/chaoren0418-arch/tw-bin-engine/issues/322#issuecomment-6700514041)要求「請主控／調度器先裁定」且「不要自行越過目前檔案限制」，未見後續擴充裁定。重新排隊不是擴充授權。本輪已在執行對話提出具體範圍請求，尚未收到答覆，故未越界修改。

### 本輪已驗證

- `ObjectRendering.cs` 仍以 gfx 2346 判定門，`animationElapsed=0`；`PlayerPointer.cs` 的 `RawStep` 與 `MoveTo` 內 `PlanStep` 仍把活門納入一般物件佔格。兩項退回原因確實尚在。
- 唯讀檢查原專案 `engine/assets/doors/manifest.json`，其 SHA-256 與 `integrity.json` 相符：`73c7597a7be698bf5b05a5a0650563447c44775c0b2a8ca1381771312110a6c4`。manifest 只有 gfx 2346。
- `doors/2346.json` SHA-256 與 manifest 相符：`8427f26f1826d84d2001115b9de71de6de10c8a489091ac1771ed42743738594`。8 個方向的 open／close 各只有 **1 幀、4 ticks**（open `0:1`，close `0:0`）。因此時間推進本身無法在這份真實素材上產生連續多幀；不得插造幀或以合成測試畫面宣稱原生動畫完成。
- 本輪沒有程式或建置輸入變更，不重建、不更新 dev lock、不重跑前輪測試；以下 251／358 項及 DLL SHA 均明確屬前輪證據，不能當成本輪已修復的證明。
- 本輪 `check-no-game-assets.py` 通過：3294 個追蹤檔、4 個核准 EmbeddedResource 宣告、0 errors；`git diff --check` 通過。PR #350 已確認轉回草稿。

### 給主控的最小裁定方案

1. `Doors.cs`：保存門動作的專用起始時間與物件／世界世代；S87 作為目前狀態，S158 才觸發開關播放。C41 與 S209 不觸發動畫；移除、替換及世界重設使舊播放失效。不要直接沿用會被其他物件更新覆寫的 `UpdatedAt`。
2. **擴充 `ObjectRendering.cs`**：只改門辨識及動作／時間選取，接入上述門播放狀態，沿用 `ObjectAppearance.At` 的原始時序並在結束後停於末幀；一般角色及 NPC 路徑不改。已有 gfx 2346 素材的第一階段不必修改 `ObjectAssets.cs`；只有主控另要求支援新增核驗 manifest 內的 gfx 時，才解除其硬編碼限制。
3. **擴充 `PlayerPointer.cs` 的 `RawStep` 與 `MoveTo/PlanStep`**：已識別門不再進一般物件佔格集合；兩處皆加入 `DoorBlocks`，並保留既有地形、其他物件與斜向穿角檢查。發送前仍沿用 `OnlineMovement` 的 S209 再檢查。原 Issue 沒要求單擊手勢，本輪不申請 `PlayerPointerUi.cs`。
4. `engine/_integration/dev/doors-tests/DoorChecks.cs`：裁定後加入滑鼠開門穿越、關門拒絕、尋路後關門、S158 不取代 S209、開門仍受地形／其他物件限制，以及合成多幀首／中／末幀、反向切換、重複回包與世界／物件失效測試，接既有 `doors` 入口。合成素材只驗播放邏輯，真實 gfx 2346 仍應忠實呈現原始單幀動作。
5. 主控需明確區分「按原始序列播放開關動作」與審查文字的「連續多幀動畫」。若驗收要求可見的多幀門，另派 resource 任務查證確有多幀的原始門素材及核驗 manifest；不修改封存素材，也不以假圖補足。

### 本輪未驗證與交接

動畫與滑鼠尋路修正尚未實作，對應新增測試尚未提交，不宣稱整張任務通過。未啟動遊戲、伺服器、DB 或讀寫機密設定；原專案與封存皆只讀。產出仍為本報告，不改 `docs/REPORT.md`。

**下一棒：Claude／調度器補上上述兩個共用區段的明確授權與原始單幀素材驗收口徑，再交 codex-local 修改、更新 dev lock、建置、補離線測試及確認最新 CI。PR #350 應保持草稿，不合併、不關閉 #322。**

## 前輪成果與驗證（保留交接依據，非本輪新驗證）

已完成限定檔案內的門封包、物件狀態、C41 送出與移動碰撞掛接；**尚未達成整張任務的顯示／操作驗收，PR 應保留草稿**。任務明列的修改範圍不含必要的渲染器修改，已於對話提出最小擴充請求；未取得確認前不改範圍外的產品檔案。

## 做了什麼

- `Doors.cs` 辨識 S87 DoorPack 完整特徵，保存開／關／損傷／毀壞 action；S158 更新門動作，非門仍交原分派。
- S209 保存伺服器回報的方向及通行狀態；寬門逐段更新，開門只撤掉門阻擋層。S87、S158、C41 不自行推算通行狀態。
- `OnlineMovement.cs` 加門查詢，包含實際送出前再次查詢，防止排隊期間關門仍送 C29。
- `PlayerPointer.cs` 的既有操作分支遇門改送 C41（雙擊／Ctrl 左鍵／右鍵）；保留指標可見性、忙碌、死亡、交易及連線／世界／目標實例驗證。只送請求，不預測開關。
- C41 採 **HHD**，與唯讀核對的 `C_Door.start` 及參考文件詳細表一致；任務摘要的 DHH 與實際讀取順序不符。來源 SHA 及原因見 [doors.md](../protocol/doors.md)。
- 測試提交於 `engine/_integration/dev/doors-tests/`，掛接既有 DemoChecks 的 `doors` 群組及 Windows CI job；更新 dev lock，未改封存 lock。

## 怎麼驗證（已驗證）

- .NET SDK 10.0.300：先 `rebuild.py --update-lock`，再 `rebuild.py --build`，產品及 launcher 建置通過。
- 本機 NuGet.org 還原因 TLS 認證失敗；改以 `scratch/issue-322/NuGet.Config` 唯讀引用原專案 `engine/vendor` 完成。套件還原及所有暫存均在此 worktree。
- `DemoChecks ... doors`：**251 項通過**。包括所有 C／S 格式、完整截斷長度、padding、錯誤欄位、同圖 S206、Reset、S120／ID 重用、排隊期間失效、完整毀壞回包清除選取、禁止預測狀態、鍵盤通行及關門競態。
- `DemoChecks ... map-continuity`：**358 項通過**，確認既有地圖、船、旅館、特效及佈告欄回歸。
- 最後產品 DLL SHA-256：`06be563e1a084bc85eef597ffa0fd1f5dc4e9c72d9210a4c2e825bf1fc597a57`。
- `check-no-game-assets.py` 通過（0 errors）；遠端 CI 的 `build`／`npc-offline` 執行與結論以 [PR #350 的 Checks](https://github.com/chaoren0418-arch/tw-bin-engine/pull/350/checks) 對應提交為準，本機測試不代替 CI。

## 未完成／未驗證

1. `ObjectRendering.cs` 目前僅識別 gfx 2346，且 `animationElapsed=0`；因此本次只沿用該既有 open／close 靜態顯示，**沒有完成連續開關動畫**。
2. `ObjectAssets.GetDoor` 硬編碼 gfx 2346；其他 gfx 與損傷／毀壞動作仍缺原始素材及渲染路徑，不能用假素材代替。
3. `PlayerPointer.cs` 的尋路將所有活物件一律視為佔格；開門仍可能擋住滑鼠路徑。鍵盤移動已有 S209 查詢。單次左鍵目前仍僅選取，尚未加單擊開關入口。
4. 未啟動遊戲、伺服器、DB，未進行登入、原生畫面／音效、特殊門權限及真機 C41 驗收。
5. CHARPACK 無明確型別碼，以完整固定特徵辨識仍有未來封包撞型可能。S209 不含 ID，不因 S120 擅自推測門通行狀態；等待新的 S209 或世界重設。

## 產出路徑

- 產品：`engine/source/Doors.cs`，另有 `WorldSession.cs`、`OnlineMovement.cs`、`PlayerPointer.cs` 最小掛接。
- 測試：`engine/_integration/dev/doors-tests/DoorChecks.cs`、`README.md`；DemoChecks 入口與 `.github/workflows/build.yml`。
- 協定：`docs/protocol/doors.md`。
- 本機證據（不提交）：`scratch/issue-322/build/`、`doors.json`、`map-checks.json`。
- 報告：本頁；未改 `docs/REPORT.md`。

## 給下一棒的提示

由 Claude／調度器裁定並擴大最小檔案範圍：`ObjectRendering.cs`（辨識門與動作時間）、`ObjectAssets.cs`（依已核驗 manifest 取門）、`PlayerPointer.cs`（開門佔格與 S209 尋路），單次左鍵還需 `PlayerPointerUi.cs` 或明確允許增加指標入口。取得確認後才能補完整動畫、單擊與滑鼠穿門，並加對應離線渲染／路徑測試。尚缺的真實門素材另派 resource 任務；真機驗收交 dot 的授權 smoke。未補齊前不要因編譯與封包測試通過就關閉 #322。
