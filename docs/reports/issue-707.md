# Issue #707 T295：R4-D 料理窗內容與備援

依據：`docs/reference/gap-matrix.md` 第 13.5 節 R4-D；WIN2§7（W2-F）；第 4.2 節 `CCookWindow`；`docs/reference/packetbox-windows.md` 第 2.3 節；`docs/protocol/item-use.md`。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/Cooking.cs` | ① `CookRecipeTable`：執行期從原客戶端 `Text.pak` 的 `recipe.tbl` 讀食譜表（CP950，只在記憶體解碼，不存檔、不進 repo），嚴格解析（筆數、欄位數、編號不重複且 ≤255）；測試用 `UseTable`。② `CookingProtocol.Listed`：等級 L 列編號 < 8×L（推測）。③ `CookBookState` 加 `BookObjectId`：收到 S250 52 時記下選取中的料理書。④ S250 52 已開且等級相同 → 關窗（PBW§2.3，已驗證；原本推定是「切換」）。⑤ `WorldSession.CookingBookItem`、`LoginConnection.CookRecipe(revision, 編號)`：對料理書送 C_USE_ITEM(164) `D(物件) C(1) C(編號)`，走既有道具使用閘門。⑥ 刪掉只剩測試呼叫的 `CloseCookBook()`（無參數版）。⑦ `WorldCookBookSource` 加清單（名稱經道具名稱表代換）與製作回呼；`AttachNativeCookBook` 每 tick 另外同步 WinForms 備援。 |
| `engine/source/NativeUi/CookBookPanel.cs` | 畫料理清單（白字，選取列黃字）；上鈕／下鈕／滾輪捲動一列；點一下選取，在選取列再點一下或雙擊就製作；量表依數值裁切；實作 `IUiClosablePanel`（4 層堆疊擠出＝關閉 X）。`ICookBookSource.Cook` 是預設介面方法，舊的測試假來源不用改。 |
| `engine/source/CookBookFallbackUi.cs`（新） | 原生 UI 關閉時的 WinForms 暫行清單：編號／料理／材料三欄，「製作」鈕或雙擊送出，「關閉」或標題列 X 只隱藏並關掉這一次的料理窗（不送封包）；同一次關掉後不再自動跳出，下一次 S250 52 才再開。 |
| `engine/source/CookBookFollowupTests.cs`（新） | `[WorldModuleTest("cook-book-followup")]`：解析與 7 種錯誤格式、等級篩選與名稱代換、同等級關窗、開窗料理書的記錄與找不到時不送、C_USE_ITEM 封包 bytes、過期 revision 不送、面板清單區／捲動／選取／製作／量表像素／`IUiClosablePanel`、備援的顯示條件與清單內容。全部是合成資料。 |
| `engine/source/CookingTests.cs` | **超出任務列出的檔案範圍**：改 3 處 `CloseCookBook()` → `CloseCookBook(s.CookBook.Revision)`、改 1 行註解。原因：任務要求刪掉無參數版，它唯一的呼叫者就在這裡，不改就編譯不過。 |

### recipe.tbl（本機 `D:\lineage開發\0-客戶端`，唯讀核對，2026-11-10）
- `Text.idx` 第 29760 筆 `recipe.tbl`，1473 bytes。第一行是筆數 27；每行格式：`"名稱" "全名" 編號 欄4 材料數 ("材料" 數量)×材料數`。
- 編號是 0–23 與 80–82；名稱與材料都是 `$n`（道具名稱表）；欄 4 全部是 3，意義沒查。
- 內容沒有寫進 repo；測試用的是自己編的表。

## 已驗證／推測／待驗證
- **已驗證（文件／本機唯讀）**：S250 52 同等級關窗（PBW§2.3）；製作封包格式（item-use.md）；recipe.tbl 的格式與筆數。
- **推測（程式註解已標明）**：
  - 等級 L 列編號 < 8×L；編號 80–82 的特殊食譜不列（不知道原版何時列）。這三道仍然可以從背包料理書的對話框輸入編號製作。
  - 清單區是底圖左緣＋8 到捲軸左緣−2、上鈕頂到下鈕底，每列 15 px，所以實際版面約 12 列。
  - 製作方式：再點一下或雙擊。原版的操作方式沒查。
  - 量表填充＝等級／11。原版量表的資料來源沒查。
  - 料理書＝收到 S250 52 時選取中的那本；不知道是哪本時，用背包裡唯一的一本；有好幾本又沒記錄時不送。
- **待驗證**：上面每一條推測都要用 IDA 查 `CCookWindow`（`sub_A5AAC0`）的繪製與事件，或是真機比對。另外原生窗沒有畫材料文字（版面沒有位置），拖曳和 Esc 也沒做。

## 怎麼驗證
- `py -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config`：PASS（worktree 裡沒有離線 `vendor`，所以改用 CI 的 NuGet 設定；用暫時 lock，沒有寫入 `rebuild-lock.json`）。
- `py -B -X utf8 engine/tools/check-no-game-assets.py`：pass（errors 0）。
- 依本機限制，**沒有在本機執行任何測試或程式**。`--module-tests`（含新的 `cook-book-followup`，以及既有的 `cooking`、`cook-and-history`）交給 PR 的 CI 跑。

## 沒驗證什麼
- 本機沒有跑過離線測試，結果看 CI。
- 真機：原生 UI 開著和關著兩種情況下，使用料理書 → 開窗 → 列出食譜 → 製作 → 伺服器回應（成品、材料不足訊息）。
- 同一本料理書連用兩次會不會關窗（PBW§2.3 是靜態查的結果）。

## 產出路徑
- 上表 5 個程式檔、本報告 `docs/reports/issue-707.md`；分支 `task/707`。

## 給下一棒的提示
- 真機（Codex／dot，P24Lab 測試角色）：準備料理書與材料；①不加 `--native-ui`：使用料理書選「開啟料理書」→ 應跳出「料理書（等級 n，k 道）」清單 → 選一道按「製作」→ 看伺服器回報；②加 `--native-ui`：同樣流程，在原生窗點兩下食譜；③同一本料理書連開兩次，確認會關窗。
- 開一張 resource 任務，用 IDA 查 `sub_A5AAC0` 和它的繪製、點擊函式：每列高度、清單起點、製作操作、量表數值、80–82 什麼時候列出。查到之後只要改 `CookingProtocol.Listed`、`CookBookPanel` 的常數和 `GaugeFill`。
- gap-matrix 13.6 丙 4／6 裡料理窗的部分（清單沒畫、沒有 `IUiClosablePanel`），以及丁「料理窗只在原生 UI 下有」這幾項，可以標為已處理（推測的部分除外）。
