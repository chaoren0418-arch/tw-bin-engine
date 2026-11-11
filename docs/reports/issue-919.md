# Issue #919 — T404：`--evidence` 時把 `[native-ui]` log 寫進 evidence 目錄

階段：code，第 1 次嘗試；分支 `task/919`。依據：`docs/reference/gap-matrix.md` 15.12.6「真機前置」、15.12.7。

## 做了什麼
- `engine/source/NativeUi/ClientArchive.cs` 的 `UiResourceLog`（只加輸出，既有 `Recent()`／`Trace` 行為不變）：
  - 第一次 `Write` 時從 `Environment.GetCommandLineArgs()` 找 `--evidence <dir>`（`EvidenceArgument`）；只解析一次。沒有參數、值是空的、或 `--evidence` 在最後一個時，不寫檔（跟原本一樣）。
  - 有參數時，每一行都附加到 `<dir>/native-ui.log`，格式是 `yyyy-MM-ddTHH:mm:ss.fff+08:00 [native-ui] <內容>`（本地時間含時區、UTF-8 無 BOM、`\n` 換行；內容裡的 CR/LF 會換成空白，確保一筆一行）。
  - 用 `FileMode.Append` 加上 `FileShare.ReadWrite|Delete` 開檔，所以 smoke 時可以邊跑邊看（tail／編輯器）；必要時會先 `Directory.CreateDirectory`。
  - 寫檔失敗（任何例外）一律吞掉，只把訊息記在 `UiResourceLog.LastEvidenceError`，不影響遊戲；下一次寫成功就清掉。
  - `EvidenceDirectory` 可以設定（給測試用，設 null 就關閉）。
  - 沒改 `Program.cs`／`ClientForm`：參數直接從程序的命令列讀，跟 `ClientForm` 收到的 `--evidence` 是同一個值（`UiOptions.Strip` 只拿掉 `--native-ui`／`--classic-ui`）。
- 新測試 `engine/_integration/dev/native-ui-tests/NativeUiEvidenceLogTests.cs`（`[CheckGroup]` 自動發現：`native-ui-evidence-log`、`native-ui-canvas` Order=20、Default Order=42）：
  - 參數解析：有值、夾在其他參數中間、沒有、沒有值、空值、相似但不同的參數名（`--evidence-dir`）。
  - 預設值等於本程序命令列的解析結果。
  - 關閉時不產生 `native-ui.log`，`Recent()` 照樣記錄。
  - 開啟時三筆寫成三行，格式是時間戳加 `[native-ui]`，中文正確，多行內容壓成一行，沒有 BOM。
  - 確認是附加不是覆寫；別的程式開著檔案讀的時候仍然寫得進去。
  - 失敗情境：把 evidence 目錄指到一個「檔案」，`Write` 不丟例外、`LastEvidenceError` 有值、`Recent()` 照樣記錄；改回正常目錄後錯誤清掉。
  - 測試目錄是 `<repo>/scratch/issue-919/`；結束時只刪自己建的 `evidence/native-ui.log` 和 `blocker-file`，沒有遞迴刪除，也會還原 `EvidenceDirectory`。
  - CI 現有的「Native UI canvas…」步驟（`native-ui-canvas` 群組）會自動跑到這組測試，不必改 workflow。

## 怎麼驗證
- 已驗證（本機，只編譯、沒執行）：
  - `py -3.12 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config`：product／launcher 的 restore 和 build 都退出 0，`PASS dev`。lock 用暫時版本，沒有寫入（照 #646）。
  - `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：0 錯誤（3 個警告，都不是本次改的檔案）。
  - `py -3.12 -B -X utf8 engine/tools/check-no-game-assets.py`：`status: pass`、0 錯誤。
- 交給 CI：依本機規定不在本機執行 DemoChecks；這組測試的結果看 PR 的 CI（windows `map-continuity` job 的 native-ui-canvas 步驟）。

## 未驗證什麼
- 待驗證（真機）：在 P24Lab 帶 `--evidence <dir>` 啟動時，`native-ui.log` 是否真的出現 15.12.7 列的關鍵字（`ui-xml-ref`、`surf-ref`、`native-ui fallback:`、`視窗堆疊拒絕開啟` 等）。靜態抽查：`ui-xml-ref`（`RefClientXmlSource.cs:100–101`）、`surf-ref`（`RefClientSurfFallback.cs:71–143`）、`native-ui fallback:`（`UiClientBridge.cs:47`）、`視窗堆疊拒絕開啟`（`UiRoot.cs:132`）都是走 `UiResourceLog.Write`，所以會寫進檔案。`engine/source` 另外有 15 個檔案直接呼叫 `Trace.WriteLine`（例如 `Ambience.cs`、`SoundPackets.cs`、`LocationShare.cs`），這些行**不會**寫進 `native-ui.log`；本次沒有逐一對照 M1–M40 的每一個關鍵字。
- 沒有參數時的行為：靜態上只多了一次命令列解析，沒有寫檔；沒有在真機確認。

## 產出路徑
- `engine/source/NativeUi/ClientArchive.cs`
- `engine/_integration/dev/native-ui-tests/NativeUiEvidenceLogTests.cs`
- `docs/reports/issue-919.md`（本檔）

## 給下一棒的提示
- 真機 smoke（M1–M40）：啟動時加 `--evidence <dir>`，結束後在 `<dir>/native-ui.log` 用關鍵字比對 15.12.7，跟同目錄的 `window-events.json` 一起看；不用再開 DebugView。
- 如果 smoke 需要的關鍵字是直接呼叫 `Trace.WriteLine`，而沒經過 `UiResourceLog.Write`（共 15 個檔案，見上），可以另開小任務改成走 `UiResourceLog.Write`，或另外接一個 evidence 輸出。
- 建議 gap-matrix 15.12.6／15.12.7 的說明在合併後改成「看 `<evidence>/native-ui.log`」（本任務不改文件範圍外的檔案）。
