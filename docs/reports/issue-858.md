# Issue #858 — T374：R9-N 公告板上一篇／下一篇

依據：`docs/reference/followups-9.md` §3.2、§3.3（T355／#818），`gap-matrix.md` §15.11。

## 做了什麼

- `engine/source/Board.cs`：新增 `BoardArticleStep` 與 `BoardNavigation.Article(view, next)`，照 §3.2（已驗證）決定 672／676 的動作：
  - **672＝上一篇**（`sub_7CDEE0` → `sub_7CD910` → `sub_7CDBD0`）：忙碌（`CanAct=false`）不動；目前文章在本頁的索引 ≤ 0 → 關閱讀窗；否則讀索引 −1 的文章。
  - **676＝下一篇**（`sub_7CD470` → `sub_7CD850` → `sub_7CDA70`）：本頁還有下一列 → 讀它；否則有下一頁（`CanNext`，對應 `this[7]`≠0）→ 先載下一頁；都沒有 → 關閱讀窗。
  - 封包沿用既有路徑：讀文 = `C_BoardRead`（3.81 opcode 114，TW16 op 93 `cdd`(板 id, 文章 id)），載下一頁 = `C_BoardBack`（3.81 opcode 23，TW16 op 6 `cdd`(板 id, 游標)，游標＝本頁最後一列 id）。
  - 文章不在目前頁（沒有索引）時當作「沒有可走的」→ 關閱讀窗。
- `engine/source/NativeUi/BoardPanels.cs`：拿掉「meaning unverified → no action」，閱讀窗 672／676 接到 `Navigate`：關閱讀窗在面板本地處理（同 678，不送包）；讀鄰篇／載下一頁交給 host（`"article-previous"`／`"article-next"`）。
- `engine/source/BoardUi.cs`：`Press` 接受 `"article-previous"`／`"article-next"`，用自己的快照與 `busy` 再判斷一次；讀鄰篇時把清單選取移到該列，再走原本的讀文動作（`Run("read", topic)`）；跨頁走原本的 `"next"`（同一套啟用條件）。`--classic-ui` 的 WinForms 公告板沒有這兩顆鈕，行為不變。
- 新檔 `engine/source/BoardNavigateTests.cs`（`[WorldModuleTest("board-navigate")]`，自動發現）：16 項決策表（中間、頭、尾、單列、有／無下一頁、忙碌、等待中、沒有文章、不在本頁）＋按鈕接線（672 (56,312)、676 (110,312) 的角色與位置、本地關窗不送包、忙碌不動、678 不受影響）。圖面全為合成尺寸。

- **第 2 次嘗試（CI 修正）**：第 1 次 PR #876 的 `map-continuity` 失敗在 `engine/_integration/dev/native-ui-tests/BoardPanelChecks.cs:76`，那條舊檢查斷言「672／676 意義未驗證 → 不動作」，正好是本任務要取代的行為。改成檢查新行為（中間列 → `article-previous`／`article-next`；第一列按 672、最後一列且無下一頁按 676 → 本地關閱讀窗、不送包；最後一列有下一頁按 676 → `article-next`；忙碌 → 不動）。此檔不在原列「可改的檔案」內，但不改就無法讓 CI 綠，只動這一段斷言。同時已同步 `origin/main`。

## 怎麼驗證

- 第 2 次：同步 main 後本機編譯 `OfflineClient.csproj`（輸出 `engine/build/client-product-dev`）與 `DemoChecks.csproj` 皆 **0 錯誤**（只有既有警告）；檢查本身依本機限制不執行，交給 CI。

- 本機編譯：`dotnet build engine/source/OfflineClient.csproj -c Release --no-restore` → **0 錯誤**（4 個既有警告，在 `MagicUi.cs`、`HtmlActionTests.cs`，與本次無關）。
  - 本機環境繞法（沒進 repo、沒改建置設定）：worktree 沒有 `engine/vendor`，暫時用 junction 指向原專案 vendor 還原（已用 `cmd /c rmdir` 拆掉）；全域 NuGet 快取的 WebView2 1.0.4258.31 缺 DLL（同 #851 報告），改用 `NUGET_PACKAGES=scratch/task858/packages` 隔離還原，並以 `scratch/task858/nowinmd.targets` 濾掉 winmd 參照。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`（4345 檔，0 錯誤）。
- 單元測試與本組離線測試 `board-navigate`：依本機限制不在本機執行，交給 PR 的 GitHub Actions CI。

## 未驗證什麼

- **待驗證（真機）**：676 跨頁載入下一頁後，原版是否自動讀新頁第一篇。§3.2 只寫「先送 op 6 載下一頁」，本次只載頁（清單窗顯示新頁），不自動讀文。
- **推測**：「索引」＝文章在目前頁清單的列號（伺服器送來的順序，新到舊）；`this[7]` 是伺服器游標（§3.2 原文即標推測），引擎以既有 `CanNext` 代表。
- 實機畫面與封包互動（P24Lab）未做，留給 Codex／dot。
- 沒有新增或宣稱缺少任何資源（672／676 圖既有）。

## 產出路徑

- `engine/source/Board.cs`、`engine/source/BoardUi.cs`、`engine/source/NativeUi/BoardPanels.cs`、`engine/source/BoardNavigateTests.cs`
- 本報告 `docs/reports/issue-858.md`
- 分支 `task/858`

## 給下一棒的提示

- 若真機確認跨頁後原版會自動讀第一篇：在 `WorldSession.ReceiveBoardList`（`Board.cs`）加「待讀第一篇」旗標即可，`BoardNavigation` 不用改。
- `BoardChecks.cs`（`engine/_integration/dev/board-tests`）已有 `read`／`next` 封包向量；672／676 沒有新封包，只重用這兩條。
- 本機若要編譯，照上方繞法（或 #851 報告）；不要改 repo 的 NuGet 設定。
