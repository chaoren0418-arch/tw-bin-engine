# Issue #528 T221：M7-U 公告板原生窗（code）

分支：`task/528`。依據：WIN§2 T130（`docs/reference/windows.md`）、gap-matrix 第 7 節、`docs/design/native-ui.md`。

## 做了什麼
- 新檔 `engine/source/NativeUi/BoardPanels.cs`：
  - `BoardWindowLayout`：原樣抄入 WIN§2 T130 的按鈕 surf 與窗內座標。清單窗 658 (209,305)、660 (34,332)、662 (139,304)、664 (171,304)、666 (64,311)、668 (99,311)；閱讀窗 672 (56,312)、676 (110,312)、678 (213,297)、674 (90,299)。
  - `BoardListWindow`、`BoardReadWindow`（實作 `IUiPanel`，`UiLayer.Window`）：**只畫已驗證的按鈕**，位置是窗原點加上 WIN§2 座標，大小取 surf 的實際尺寸。surf 不存在時不畫、也不能點。用左鍵「按下並在同一顆鈕上放開」觸發，轉呼叫既有公告板動作。
  - `BoardWriteWindow`：沒有任何已驗證的 surf 或座標，所以只留資料，永遠不顯示。
  - `BoardPanelSet`：兩個窗共用一個狀態，決定目前顯示「清單／閱讀／無」。閱讀窗的「回清單」只在本機切換畫面，不送封包。
- `engine/source/BoardUi.cs`：
  - 把按鈕動作抽成 `Press(command)`，`Enabled` 規則與發文、刪文的 OKCancel 確認和原本相同。
  - `ClientForm.AttachNativeBoard`：只有原生 UI 啟用時才把三個面板加進 `UiRoot`。原生 UI 關閉（預設）時，行為和以前完全一樣。
  - 公告板三個窗在原版是寫死的舊式視窗，不讀 XML，所以沒有走 `UiPanelRegistry`（它要求 XML 版面），改成在開窗時動態掛上，沒有改到 C2 的檔案。
- `Board.cs` 沒有改：協定與狀態模型已經夠用。
- 測試：新檔 `engine/_integration/dev/native-ui-tests/BoardPanelChecks.cs`，在 `DemoChecks.csproj` 加一行；`demo-tests/Program.cs` 的 `board` 群組、`map-continuity` 群組（CI 會跑）和預設完整測試，各加一個呼叫。

## 依據等級
| 項目 | 等級 | 處理 |
|---|---|---|
| 清單窗 6 鈕、閱讀窗 4 鈕的 surf 與窗內座標 | 已驗證（WIN§2 T130） | 畫出來 |
| SurfList 中 650–689 的 `id == file` | 已驗證（本輪唯讀解密 SurfList.xml，輸出只在 `scratch/`） | — |
| 按鈕用途：658／678 是 X（關閉）、660 Read、662 Write、664 Delete | 推測（本輪目視原圖上的字） | 只用來把點擊導到既有動作 |
| 666／668 是上一頁／下一頁，674 是回清單 | 推測（目視箭頭方向） | 同上 |
| 672／676（閱讀窗左右箭頭） | 待驗證 | **不綁任何動作** |
| 背景 656（238×350，清單）、670（238×343，閱讀） | 推測（色鍵與按鈕相同，尺寸剛好包住所有按鈕） | **不畫**，只記成常數 |
| 發文窗 surf 680／682／684／686（Delete／Write／Done／O.K.） | 推測 | **不畫**，只記成資料 |
| 窗在 800×600 上的位置 `DefaultOrigin=(281,60)` | 待驗證（擺放決定，不是原版資料） | 集中在一個常數 |
| 清單列、文章文字的位置與字型 | 待驗證 | **不畫**；列表、文章、發文仍由既有 `BoardUi` 視窗處理（過渡做法，不是原版行為） |

## 怎麼驗證
- `rebuild.py --update-lock`，然後 `rebuild.py --build` → `PASS dev`；merge origin/main 後重跑 `--update-lock` 與 `--build`，仍是 `PASS dev`（productDll `45fcd359…`）。第二次同步 origin/main（#567）後：解 lock 衝突（用 main 版）→ `--update-lock` → `--build` `PASS dev`（productDll `7006f885…`）；`board` PASS 585、`native-ui-canvas` PASS 429、`native-ui-resources` PASS 539；`check-no-game-assets.py` pass。
- `DemoChecks … board` → `PASS 585`（含新的公告板面板檢查）；`native-ui-canvas` → `PASS 429`；`native-ui-resources` → `PASS 539`。
- 新的檢查涵蓋：
  - WIN§2 資料逐項比對。
  - 按鈕矩形等於原點加偏移；邊界只是已載入按鈕的聯集。
  - surf 缺檔時不畫也不能點。
  - 畫布上：按鈕 1:1 畫出；背景位置和按鈕之間保持黑色。
  - 清單／閱讀／關閉之間的切換；閱讀窗 X 會關閉，「回清單」不送封包，換了新文章會回到閱讀窗。
  - 在不同按鈕上按下與放開、右鍵、按鈕間的空白都不觸發動作；隱藏的窗不會觸發。
  - 經過 `UiInput` 時，點按鈕走面板路徑，點按鈕以外的地方交給世界。
- `engine/tools/check-no-game-assets.py` → pass；沒有新增 `.img`、`.xml` 或 `.png`。

## 未驗證什麼
- 沒有啟動遊戲，也沒有在原生 UI 開啟的情況下真機看過畫面；`AttachNativeBoard` 只經過編譯，沒有離線測試（它需要真實的 `ClientForm`）。
- 本機跑 `map-continuity` 和預設完整測試時，在 `DisplayNoticeChecks`（S245 訊息 999）與 `Movement123` 讀檔失敗。兩者都跟本次改動無關：本機有原客戶端，而且 worktree 沒有 `engine/assets`。main 的 CI 是綠的；以 CI 結果為準。
- 沒有對原版做反組譯；按鈕用途、背景、窗位置都只到「推測／待驗證」。

## 產出路徑
- `engine/source/NativeUi/BoardPanels.cs`（新）、`engine/source/BoardUi.cs`
- `engine/_integration/dev/native-ui-tests/BoardPanelChecks.cs`（新）、`demo-tests/DemoChecks.csproj`、`demo-tests/Program.cs`
- `engine/_integration/dev/rebuild-lock.json`（重產）

## 給下一棒的提示
- 要讓原生公告板完整取代 `BoardUi`，需要先開一張查證任務，反組譯 `sub_7CB710`、`sub_7CBA70`、`sub_7CBE90`，查出：背景 surf 與窗位置、清單列與文章文字的座標和字型、672／676 的用途、發文窗的按鈕座標與輸入框。查完只要改 `BoardWindowLayout` 並加上畫文字的部分。
- 本機在 worktree 建置需要 `engine/vendor` junction，以及 `py -3` 所在的 Python 目錄在 PATH 上。
