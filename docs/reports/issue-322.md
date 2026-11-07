# Issue #322：T136 門（第 1 次 code）

## 結論

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
