# Issue #948 報告：T411 一般產品入口接受 `--evidence <dir>`

依據：`docs/testing/smoke-m7.md` §0、§11 K-1／K-2（T410／#944，PR #946）；#919。

## 做了什麼
1. **`engine/source/ProductEntry.cs`**（產品參數接受條件）
   - `Normal(args)` 在有 `--normal-product` 時，除了它本身只另外接受**一組** `--evidence <dir>`（前後順序都可以）。其他情況一律丟出原本的 `ArgumentException("正常入口不接受診斷或自動登入參數。")`，訊息不變（抽成常數 `RejectMessage`）。被拒絕的情況：其他診斷或自動登入參數、`--evidence` 沒有值、值是空白、值以 `--` 開頭、兩組 `--evidence`、`--normal-product` 出現兩次。
   - 新增 `Normal(args, projectRoot)` 給測試用合成參數；`Normal(args)` 用 `DefaultProjectRoot()`，也就是產品目錄 `<專案>/engine/build/client-product-dev/` 往上三層。這跟 `Program.Main` 推算 `../../assets` 用的是同一套目錄結構。
   - 新增 `CheckEvidenceDirectory(dir, projectRoot)`：證據目錄必須在專案資料夾**裡面**（等於專案資料夾本身、或只是開頭名稱相同的兄弟目錄都不算），而且不能在 `releases\` 底下，路徑上也不能經過 junction 或符號連結（例如 `engine\assets` 會連到 `releases\r29\assets`）。不符合時會跳「證據目錄…：請改用專案資料夾內的目錄，例如 `<專案>\scratch\smoke-m7\<日期>`。」，產品拒絕啟動，提示框沿用既有的 `P24 啟動錯誤`。相對路徑以目前目錄為準，跟寫證據的程式一樣。
   - 帶 `--evidence` 時 `normalProduct` 仍然是 true，所以原生 UI 預設、聊天輸入列、書籤、位置分享等一般產品行為都不變。證據檔照原本程式寫出：`UiResourceLog` 寫 `native-ui.log`，`ClientForm` 寫 `window-events.json`、`login-state.json`。
   - 沒有帶 `--normal-product` 的診斷入口完全不變，包括 `--evidence` 不受專案資料夾限制。
2. **`engine/_integration/dev/launcher-source/Program.cs`**（只加轉發）：除了 `--check-only` 系列，也接受 `--evidence <dir>`。我先用 launcher 的目前目錄把路徑轉成完整路徑，再以 `--normal-product --evidence <完整路徑>` 啟動產品。原因：產品的工作目錄是產品根目錄（`engine\`），如果原樣傳相對路徑，指到的位置會跟使用者打的不一樣。launcher 拒絕其他參數時的訊息多寫了 `--evidence`，產品的拒絕訊息沒有變。
3. **新測試 `engine/_integration/dev/native-ui-tests/NormalEvidenceArgsTests.cs`**：測試群組 `normal-evidence-args`，也加進 `native-ui-canvas`（Order 30，CI 會跑）和 Default（Order 43），用 `[CheckGroup]` 自動發現。內容全是合成 argv，不啟動遊戲：
   - 接受：7 組，包含順序對調、與 `--native-ui`／`--classic-ui` 混用、結尾帶分隔符號、目錄還不存在、相對路徑。也確認接受後原生 UI 預設仍然開、`UiResourceLog.EvidenceArgument` 讀得到同一個目錄。
   - 拒絕：17 組，都要回原本的訊息。
   - 專案外的路徑：6 組，包括上層目錄、名稱開頭相同的兄弟目錄、專案資料夾本身、`..` 跳出、`releases\`、大寫的 `RELEASES`，都要回「證據目錄…」並附 scratch 提示。找不到專案資料夾時拒絕 `--evidence`。
   - 符號連結：能建立連結時才測，Windows 沒有權限就跳過；測完用不遞迴的 `Directory.Delete` 只刪連結本身。
   - 診斷入口不變。
4. **`docs/testing/smoke-m7.md`**：§0.3 從三輪改成一輪（A 輪用 `.\啟動新版客戶端.cmd --evidence <證據目錄>`），B 輪取消，C 小段只留給 §10。§11 的 K-1、K-2 標「已修（#948）」。表格裡各項的「輪」欄沒有逐列改，§0.3 寫明原本標「B」或「A＋B」的一律在 A 輪做（任務只允許改 §0 與 K-1／K-2）。

`engine/source/Program.cs`、`DemoChecks.csproj`、各註冊表、`rebuild-lock.json` 都沒有改。

## 怎麼驗證（只編譯，沒有執行任何編出來的程式）
- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config --write-receipt --lock-out scratch/issue-948/candidate-lock/rebuild-lock.json --output scratch/issue-948/rebuild` → product-build 0、launcher-build 0、`PASS dev`。這是不改 lock 的暫時 lock 模式，產生的 `receipt.json` 已刪除，沒有 commit。
  - 註：worktree 裡沒有 `engine/vendor`，所以改用 CI 的 NuGet 設定。直接用全域 `dotnet build` 會遇到既有的 NETSDK1130（WebView2 winmd）環境問題，跟這次修改無關。
- `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release` → 0 錯誤。3 個警告都是原本就有的（`DisplayNoticeChecks.cs`、`MacroChecks.cs`）。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `status: pass`，errors 0。
- 單元測試是否全綠（包括本組 `native-ui-canvas`）：本機規定不能執行測試程式，**交給 PR 的 CI 判定**。

## 未驗證什麼（待驗證）
- 真機：用 `.\啟動新版客戶端.cmd --evidence <scratch 內目錄>` 啟動後，畫面是一般產品（原生 UI、聊天輸入列、書籤），而且證據目錄裡有 `native-ui.log`、`window-events.json`。另外要看用專案外目錄時，跳出的提示框文字對不對。這些要交給 smoke（dot／Codex，P24Lab）。
- 符號連結那一項在沒有建立權限時會跳過。CI 的 `native-ui-canvas` 步驟在 Windows runner 上跑（PR #951：`PASS 547 DEMO checks`），log 看不出這一項有沒有實際跑到，所以列為待確認。
- 根目錄 `啟動引擎.cmd` 指向 r33 封存 launcher，封存不能改，所以它**不**接受 `--evidence`。smoke 請用 `engine\啟動新版客戶端.cmd`。

## 產出路徑
- `engine/source/ProductEntry.cs`
- `engine/_integration/dev/launcher-source/Program.cs`
- `engine/_integration/dev/native-ui-tests/NormalEvidenceArgsTests.cs`
- `docs/testing/smoke-m7.md`（§0.3、§11 K-1／K-2）
- `docs/reports/issue-948.md`（本檔）

## 給下一棒的提示
- 合併後由調度器重產 `rebuild-lock.json`（launcher 與產品來源都有變動）。
- smoke-m7 各段表格的「輪」欄和「log（B 輪）」字樣還留著，建議跑 smoke 的任務順手統一改成 A，或另開一張純文件小任務。
- 如果之後想讓 C（classic）也經過 launcher，需要另開任務讓 launcher 轉發 `--classic-ui`；本任務沒有做。
