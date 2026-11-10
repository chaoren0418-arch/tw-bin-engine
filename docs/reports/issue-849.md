# Issue #849 — T365：R9-E HyperText 小組

依據：`docs/reference/followups-9.md` 第 10 節（Q7-10 HyperText）、第 16.2 節 R9-E；`gap-matrix.md` 第 15.11 節。

## 做了什麼

`engine/source/NativeUi/HyperTextPanel.cs`：

| 項目 | 做法 | 依據 |
|---|---|---|
| ESC 關窗 | 窗開著、名稱框沒有焦點時，ESC（按下、非自動重複）→ `Close()`（藏窗＋`Host.Close`）；按鍵放開不吃，自動重複只吃不關。名稱框有焦點時維持 R7-D 的 `CancelName`。窗沒開時不吃，交給全域 ESC。 | 已驗證：`sub_875F80`（FO `0x475380`）27 → 關窗；HyperText 窗不在 `sub_A57C60` 堆疊裡 |
| 數字框 [1, N] | 0–9 逐位輸入，每位都夾在 [1, N]（例：上限 50 時 4→42→429 變 50；只打 0 變 1）；用 decimal 計算，`N=long.MaxValue` 也不溢位。 | 已驗證：`sub_872BE0` |
| Backspace | 去掉最後一位；刪到空白時保留空白（OK 不送）。 | 已驗證去最後一位；空白不補 1 是推測 |
| 5073 上下鈕 | 數字框右邊（間隔 2）放上鈕、下鈕；放開時 +1／−1，超過上限回下限、低於下限回上限。按上下鈕時框保持焦點。方向鍵 ↑／↓ 同上下鈕。缺 5073 時不畫、不可點，方向鍵照常。 | 已驗證：`sub_872DC0`（5073、+1／−1、循環）；4203／4205＝方向鍵是推測 |
| OK／Cancel | 有 1114／1111 圖時畫成圖鈕（OK 1114 (47,292)、Cancel 1111 (133,292)，按住時 1115／1112），**只在頁內有數字框時畫與可按**；窗的 Bounds 只在那時包含兩鈕。OK 仍要數值在 [max(1,Min), Max] 且宿主說可送才送。Cancel：城堡頁 → `CancelNumber`；S136 數量頁 → 關窗。缺圖時退回原本的「確定」／「取消」文字鈕（排在上下鈕後面）。 | 已驗證：`sub_875B20`（兩鈕可按＝有數字框）、`sub_876010`（範圍內才送）；座標取自參考客戶端 ly `HyperTextUI.xml`（推測，本機 24 個 idx 都沒有此 XML，未複製進專案） |
| 數字框寬度 | `InputWidth` 96 → 72，讓「框＋上下鈕＋兩個文字鈕」放得進 192 寬。 | 引擎自訂 |

新檔 `engine/source/NativeUi/HyperTextFollowupTests.cs`（`[WorldModuleTest("hypertext-followup")]`，自動發現）：ESC 各情況、夾值與 Backspace、方向鍵與 `Step` 循環（含城堡下限 10）、5073 上下鈕位置／點擊／上鈕翻轉的像素、OK／Cancel 圖鈕只在有數字框時出現與按住圖、缺圖時的文字鈕退回。

**範圍外的改動（需審查注意）**：`engine/source/HyperTextPanelTests.cs` 的 `NumberBox` 有一條舊斷言「打 429 > 上限 50 → 不能送」，和已驗證的 [1, N] 夾值直接衝突，只改了這 5 行（改成 429 夾到 50、可送，再刪回 42 送出），其餘不動。

## 資源查詢（PakViewer CLI `pak search`，`D:\lineage開發\0-客戶端` 全部 18 個 .idx）

查詢的 idx：`Sprite.idx`、`Sprite00`–`Sprite15.idx`、`Text.idx`、`Tile.idx`；查詢名：`5073`、`1111`、`1112`、`1114`、`1115`（.img／c.img）。

| 圖 | 所在 | 尺寸（.img 檔頭） |
|---|---|---|
| 5073.img | Sprite10.idx（294 B） | 13×11，只有一張朝下三角 |
| 1111／1111c、1114／1114c | Sprite.idx（1,388 B） | 46×15 |
| 1112c、1115c | Sprite.idx（1,512 B） | 47×16 |

解出的檔只放在 `scratch/t849/`（不進 git）。上鈕用 5073 上下翻轉是推測（本機只有一張圖）。

## 怎麼驗證

- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config`：product／audio／launcher 建置 PASS，沒有 HyperText 相關警告（worktree 沒有離線 `vendor`，所以用 CI 的 NuGet 設定；用暫時 lock，**沒有改** `rebuild-lock.json`）。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`status: pass`、errors 0。
- 離線測試（`hypertext-followup`、既有 `hypertext-panel`、`HtmlActionTests` 的名稱框等）依本機規定**不在本機執行**，交給 PR 的 GitHub Actions CI（`--module-tests`）。

## 未驗證什麼（真機再確認）

- 5073 上下鈕在原版窗裡的位置，以及上鈕是不是翻轉圖（或另有畫法）。
- OK／Cancel 在 uiextra86 版面下的位置：ly 座標 (47,292)／(133,292) 落在 uiextra86 底圖（到 y≈279）下方；沒有數字框時原版是否仍畫停用狀態的鈕（本實作不畫）。
- S136 數量頁按 Cancel 是否就是關窗；OK 送出後原版會關窗（`sub_876010`），目前仍由宿主決定。
- 4203／4205 是否就是方向鍵 ↑／↓。

## 產出路徑

- `engine/source/NativeUi/HyperTextPanel.cs`
- `engine/source/NativeUi/HyperTextFollowupTests.cs`（新）
- `engine/source/HyperTextPanelTests.cs`（5 行斷言，見上）
- `docs/reports/issue-849.md`

## 給下一棒的提示

- R9-F（行內排版器）會大改 `BuildRows`／`Paint`；數字框列的版面集中在 `InputRect`／`SpinPart`／`TextButtonsAt`，OK／Cancel 圖鈕在 `PaintButtons`，可整段搬。
- 真機截圖若確認原版窗用 `HyperTextUI.xml`（ly 版面），之後只要把 1962 關閉鈕、捲軸起點換掉即可；OK／Cancel 已經用 ly 的 surf 與座標。
- `HyperTextPanel.MaxDigits` 已不再使用（夾值取代），保留常數只為不破壞外部參照。
