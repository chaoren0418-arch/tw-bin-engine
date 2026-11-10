# Issue #472 報告：原生 UI 模組 書籤視窗 BookmarkUI

## 結論
**本任務已被 main 上的後續任務取代，分支不再帶任何程式變更。** 建議以「已由 #734／#845 完成」結案。

## 做了什麼
- 本分支先前（2026-11-09）提交 `ab3d2d0e`，新增 `NativeUi/BookmarkPanel.cs`、`NativeUi/State/BookmarkState.cs`、`native-ui-tests/BookmarkPanelChecks.cs`，但一直沒有 push。
- 這次同步 `origin/main`（落後 616 個 commit）時，`engine/source/NativeUi/BookmarkPanel.cs` 發生 add/add 衝突，`engine/source/BookmarkUi.cs` 也有內容衝突。查過才知道 main 已經有同一個視窗的完整實作：
  - `0d888fc8` 等 task #734（R5-B 原生書籤窗收尾：新增／刪除＋846 確認、排序鈕、區域篩選、開窗入口）
  - `e9d6b063` task #845（R9-A 原版操作：Delete 多選、Insert 快速移動清單、去色碼排序、878、確認後不關窗、雙擊傳送、隨機傳送鈕）
  - 測試：`engine/source/BookmarkPanelTests.cs`、`BookmarkPanelFollowupTests.cs`、`BookmarkWindowOriginalTests.cs`
- 解衝突時一律採用 main 的版本，並移除本分支舊有的 `BookmarkState.cs` 與 `BookmarkPanelChecks.cs`。這兩個檔是依舊版 panel API 寫的，和 main 不相容，留著也和現有測試重複。合併後的程式樹與 `origin/main` 完全相同，只多了這份報告。

## 驗收條件對照（main 現況，依程式碼閱讀）
| 項目 | main 狀態 |
|---|---|
| 清單捲動與選取 | 已實作：`MemoryPlaceList_Scrollbar` 上下鈕、拉桿、`MaxScroll` clamp、選取與多選 |
| 拖曳限制在畫布內 | 已實作：`BookmarkWindowPanel : IUiMovablePanel`，走共用的可移動面板邏輯 |
| 快速移動清單 | 已實作：Insert 鍵，最多 5 筆，第 6 筆顯示 2828 |
| 區域下拉、排序、隨機傳送 | 已實作 |
| 確認／取消、新增／刪除 | 已實作（T124 邏輯＋846 確認） |
| 13 色調色盤 ColorPalette | **main 刻意不做**，註解寫「待查證」（3.81 是否支援尚未確認） |

## 怎麼驗證
- `git diff origin/main --stat` 只有本報告一個檔案。用 Grep 確認 `engine/` 內已經沒有任何地方引用 `BookmarkPanelChecks` 或 `BookmarkState`。
- 依本機限制，沒有在本機執行任何測試程式。驗證交給 PR 的 CI。

## 未驗證什麼
- 13 色調色盤：如果要做，要先逆向確認 3.81 的 ColorPalette 行為，應該另開任務，不屬於本分支。
- 真機外觀與操作：待 Codex／dot 在 P24Lab 驗收。

## 產出路徑
- `docs/reports/issue-472.md`（本檔）
- 分支 `task/472`

## 給下一棒的提示
- #472 可以直接結案，不要再把舊的 `ab3d2d0e` 內容合回去，會和 #734／#845 重複並衝突。
- 如果要補調色盤，請以 main 的 `engine/source/NativeUi/BookmarkPanel.cs`（`BookmarkWindowPanel`）為基礎另開任務，先做逆向查證。
