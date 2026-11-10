# Issue #821 — T357：R8-B 鑑定訊息

依據：`docs/reference/followups-8.md` §2（Q6-1）、`gap-matrix.md` 15.10 R8-B。

## 做了什麼
- `engine/source/ServerMessageText.cs`：新增 `ItemDescTable`（`ItemDesc-c.tbl` 解析＋SHA-256 pin）與 `ServerMessageText.ItemDesc`（延遲、唯讀，從客戶端 Text.pak 在記憶體解出；讀不到是 null）、測試用 `UseItemDesc`。
- `engine/source/DisplayNotices.cs`：
  - 新增 `IdentifyDescRules`（照 `sub_940070`）：說明編號 1063–1072 → 訊息改 928；訊息 134＋說明 4139–4142 只印說明；其他先印 `string-c.tbl` 那一筆套參數，再印 `ItemDesc-c.tbl` 說明。
  - `ReceiveIdentifyDesc` 改成把 0–2 行放進聊天欄（`ColorIndex=24`，同 S84／S250 27／30／31 的入口）；**訊息 0 也印說明**；`S_IdentifyDesc()` 的重量字串不接。
  - 拿掉畫面下方 8 秒白字（`PaintDisplayNotices` 不再畫 S245）。
  - 沒有字串表時的範本加 928（`%0：寵物專用牙`）。
  - `LastIdentifyDesc`／`ActiveIdentifyDesc` 只當診斷紀錄保留，內容不變（既有 `DisplayNoticeChecks` 依賴它，該檔不在本任務可改範圍）。
- 新檔 `engine/source/IdentifyDescTests.cs`：`[WorldModuleTest("identify-desc")]`，自動發現。

## 資源查證（PowerShell＋.NET DES 唯讀解 `D:\lineage開發\0-客戶端\Text.idx/.pak`，輸出只在 `scratch/itemdesc/`）
- Text.idx 中 ItemDesc 相關：#13249 `ItemDesc-c.tbl`（108,872 B，解出 SHA `3afae71a…c64d0`）、#13250 `-e`、#13251 `itemdesc-h`、#13252 `-j`、#13253 `-k`；`string-c.tbl` #32559。
- `ItemDesc-c.tbl` 格式（**已驗證**，看檔案內容）：第一行 `14469`（筆數），之後 2048 行「編號 空白 說明」，編號不連續；6 行編號後沒有空白（編號都 ≥ 14469，原版不印）。1063、1072、4139–4142 都有說明。
- `string-c.tbl` 第 0 筆是「王族」，**不是** FU8 推測的空字串。

## 已驗證／推測／待驗證
- **已驗證**（靜態，FU8§2＋本機檔案）：分支規則、兩行都印聊天欄 palette[24]、ItemDesc 檔案格式與筆數。
- **推測**：
  - 訊息 0 照 FU8 的流程印 `string-c.tbl` 第 0 筆，本機會多出一行「王族」。FU8 的反編譯沒寫訊息 0 的防呆，所以照字面做；**真機再確認**（如果原版其實沒有這一行，要在 `IdentifyDescRules.MessageLine` 對訊息 0 回傳 null）。
  - 空字串那一行不印（`sub_7DC850` 收到空字串會不會印空行沒查）。
  - 參數裡的 `$數字` 用 desc-c.tbl 展開（沿用 S161 的 `ExpandDesc`；`sub_933850` 的 0x5417C0 名稱查詢沒細查）。
  - 編號後的空白可有可無、重複編號取後面那行。
- **待驗證（真機，交給 Codex）**：聊天欄實際字色與換行；訊息 0 是否真的印「王族」；1063–1072 寵物牙、4139–4142 精靈武器實際顯示。

## 怎麼驗證
- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/t821/nuget.config`（指向主專案 `engine/vendor`）→ `PASS dev`（暫時 lock，沒寫入；productDll `93b150dd…`）。
- `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release` → 0 錯誤，3 個既有警告。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → pass（4272 檔，0 錯誤）。
- 依本機規定**沒有在本機執行**任何測試程式；`identify-desc` 與既有 `display-notices`／`map-continuity` 由 CI 跑，結果看 PR。

## 未改的東西
- 沒改 `rebuild-lock.json`、`TextArchive.cs`、`DisplayNoticeChecks.cs`、`docs/REPORT.md`。

## 產出路徑
- `engine/source/DisplayNotices.cs`、`engine/source/ServerMessageText.cs`、`engine/source/IdentifyDescTests.cs`、本報告。

## 給下一棒的提示
- 真機驗收時鑑定一個「其他類」物品（3.81 只送說明編號）看聊天欄有沒有「王族」那一行；若沒有，改 `IdentifyDescRules.MessageLine` 對訊息 0 回傳 null，並更新 `IdentifyDescTests` 的「訊息 0」案例。
- `DisplayNoticeChecks.cs` 的 S245 檢查仍測舊的診斷紀錄（含重量字串的 `Text`）；之後若有任務可改那個檔，可以改成檢查聊天欄，並移除 `IdentifyNoticeMs`／`ActiveIdentifyDesc`。
