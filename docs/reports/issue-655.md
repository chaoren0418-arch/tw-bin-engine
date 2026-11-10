# Issue #655 T274：測試結果隨環境不同——S245 與 attack-skill-resources

分支：`task/655`（從 origin/main `10a44ad8` 建立）

## 結論
- **原因（已驗證）**：`notice S245 unknown message stays labeled` 送的是非模板訊息 999。`DisplayNoticeText.Format` 遇到非模板 id 時會改用 `ServerMessageText.Decode`，而 `Decode` 會**延遲讀取執行期的 T228 字串表**（`TW_CLIENT_ROOT` 或預設的本機客戶端裡的 `Text.pak` → `string-c.tbl`）。本機讀得到字串表時，999 會變成字串表第 999 筆的實際文字；CI 讀不到，才是「原服訊息 999：%1」占位。測試只寫死了占位文字，所以本機紅、CI 綠。同一檔案的 `notice S105 null info count zero`（訊息 700）也有一樣的問題：S245 一失敗就中止，所以先前沒被看到。
- **修正**：只改測試，產品碼 `DisplayNotices.cs` 不必動。新的 helper `NoticeUnknownMessage` 讓每則非模板訊息都在三種字串表狀態下各跑一次，每次都有明確預期值：
  1. `UseTable(null)` 不給字串表 → 預期編號占位（`原服訊息 999：%1`／`原服訊息 700`），**兩種環境都會跑**；
  2. 自建合成字串表（第 N 筆為 `\f1鑑定%0說明`）→ 預期 `鑑定%1說明`／`鑑定說明`，用來驗證字串表路徑（格式化、顏色碼移除、引數照字面），**兩種環境都會跑**；
  3. 本機讀得到客戶端 `string-c.tbl` 時 → 預期值從該筆記錄本身算出（`DisplayText(Format(table[id],args))`），且不可以是占位文字；讀不到時印 `INFO … not applicable`。實際文字不寫進 repo，也不印出來（只印字數）。
  - `UseTable` 是 internal，用反射呼叫，和本檔其他反射寫法一致。沒有刪除或跳過任何檢查。
- **attack-skill-resources**：改成 `string repo,string[] args` 繫結，第 3、4 個引數變成選用。沒給時自動帶入預設值：客戶端用 `GroundSpriteReader.ClientRoot`，素材用 `<repo>/engine/assets`。預設路徑不存在時印出 `SKIP` 與用法，不算失敗；明確給了路徑就一定照常檢查，路徑錯就失敗。

## 改了哪些檔案
- `engine/_integration/dev/display-notices-tests/DisplayNoticeChecks.cs`：S245 999 與 S105 700 改用 `NoticeUnknownMessage`。S245 測試其實放在這個資料夾，不在 `demo-tests/`，但仍屬於任務指定的「S245 相關測試檔」。
- `engine/_integration/dev/demo-tests/AttackSkillRenderingChecks.cs`：`NativeResources` 引數改成選用。

## 怎麼驗證（本機 Windows，DemoChecks Release；「CI 模擬」是把 `TW_CLIENT_ROOT` 設成 `scratch/issue-655/empty-client` 空目錄）
| 項目 | 本機（有字串表 `4ebbc1a7a002…`，3484 筆） | CI 模擬（無字串表） |
|---|---|---|
| display-notices | **PASS 392**（main 會失敗） | **PASS 390**（main 是 388；新增合成表檢查 +2） |
| map-continuity | **PASS 33318**（main 會失敗） | **PASS 33316** |
| attack-skill-resources（不給引數，repo=worktree，沒有 assets） | PASS 327，印 SKIP＋用法 | PASS 327，印 SKIP＋用法 |
| attack-skill-resources（repo=原專案根目錄，自動帶入預設值） | **PASS 1256**（實際比對） | — |
| attack-skill-resources（明確給客戶端與素材路徑） | **PASS 1256** | — |
| 預設全套（repo=原專案根目錄，唯讀） | **PASS 15258**（main 會在 S245 失敗） | 見下方「未驗證」 |

- `--module-tests`：pass，32 個測試／1577 項檢查，0 失敗。
- `engine/tools/check-no-game-assets.py`：pass（3927 個追蹤檔，0 錯誤）。
- `OfflineClient.csproj` 與 `DemoChecks.csproj` 建置：0 錯誤。產品原始碼沒有改。

## 未驗證什麼
- CI（GitHub Actions）結果要等 PR 跑完才知道。
- 「預設全套 + 無客戶端」沒辦法在本機完整模擬：只要設了 `TW_CLIENT_ROOT`，其他選用的真實客戶端檢查（例如 `NativeUiMacroRealClient`）就會被啟用，然後因為空目錄而失敗。這是模擬方式造成的，和本任務無關；CI 不設 `TW_CLIENT_ROOT`，也不會跑預設全套。
- 在 worktree 以 `.` 當 repo 跑預設全套時，會因為 worktree 沒有 `engine/assets` junction（找不到 `terrain0.bin`）而失敗，這是環境問題；改用原專案根目錄當 repo 就全部通過。

## 產出路徑
- 程式：上列 2 個測試檔。報告：`docs/reports/issue-655.md`。
- 本機證據（不進 git）：`scratch/issue-655/*.json`。

## 給下一棒的提示
- 新測試只要經過 `ServerMessageText.Decode`（非模板訊息 id），就要明確指定字串表狀態（`UseTable(null)`／合成表／真實表），不要依賴執行環境；可以直接沿用 `NoticeUnknownMessage` 的寫法。
- 「只要設了 `TW_CLIENT_ROOT` 就跑真實客戶端檢查」的那些選用檢查（native-ui 系列），環境變數指向無效目錄時會失敗而不是 SKIP。若要讓預設全套在任何環境都穩定，可以另開任務，改成依「檔案是否存在」來判斷。
