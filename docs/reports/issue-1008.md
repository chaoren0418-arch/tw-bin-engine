# Issue #1008 — T437：Q8-A① 補充 血盟備註不雅字過濾

依據：`docs/reference/followups-17.md` §4.1、§7（T422／#966）。

## 做了什麼
- 新檔 `engine/source/ObsceneFilter.cs`：
  - 執行期從本機 `Text.pak` 唯讀讀 `obscene-<語系>.tbl`（TW＝`obscene-c.tbl`），核 SHA-256 pin `7e90a29e…bc4151`；讀不到（CI 沒有客戶端）或 pin 不符時是 `null`，也就是不過濾、照原文送。表的內容不進 repo。
  - 格式：第一行是筆數，之後一行一個詞（只去掉 `\r`）；空字串跳過，超過實際行數的筆讀成空字串。
  - `Filter`：照 §4.1（`sub_51F5D0`）依序找第一個出現的詞 → 換成「字數」個 `*`（中文一個字算一個）→ 從頭再找，直到找不到為止。以 char 為單位比對，等於 CP950 的 DBCS 字邊界（例如「許」的尾位元組 0x5C 不會被當成 `\`）。
- `NativeUi/PledgePanel.cs`（只改備註送出）：`AnswerMemo`（聊天輸入列 3325 的回答）和 `SaveMemo`（窗內編輯的後備）都改成送 `ObsceneFilter.Apply(文字)`。60 byte 上限／3328 照 FU15，用**輸入的原文**檢查（`sub_B34270` 先做 strlen > 0x3C，之後才過濾）。過濾後的位元組數不會變多。
- `ClanUi.cs`（只改備註送出）：`--classic-ui` 的 WinForms「送出備註」也先過濾再送。
- 新檔 `engine/source/ObsceneFilterTests.cs`（`[WorldModuleTest("obscene-filter")]`，自動發現）：用合成表測解析、替換、重複替換、長詞優先、大小寫、DBCS 邊界、pin 不符／缺表、聊天輸入列送出過濾後的文字、60 byte／3328、沒有表時照原文送。有客戶端時另外核本機表：筆數 124、可用的詞 122 個。

## 本機表查核（PakViewer 同格式的唯讀 python，DES 解碼只在記憶體裡做；腳本放 `scratch/obscene_probe*.py`，不進 git）
- `0-客戶端\Text.idx/.pak` 第 24094 筆 `obscene-c.tbl`：930 B，SHA-256 `7e90a29e72ddae7b982fb1bbd40d833bbf1a8be287c228fc77b4786ad7bc4151`。第一行寫 124，但實際只有 123 行，其中 1 行空白；2 個詞帶尾端空白、2 個詞中間有空白、1 個是純 ASCII 小寫。
- 同一個封存也有 `Obscene-e.tbl`、`obscene-h.tbl`、`obscene-h.html`、`obscene-j.tbl`、`obscene-k.tbl`（其他語系，沒有用到）。

## 已驗證／推測／待驗證
- 已驗證（靜態，FU17§4.1）：替換規則（字數個 `*`、重複到沒有）、表的位置與格式、備註一律過濾後才送。
- 推測：
  1. `sub_51F900` 比對時 ASCII 字母不分大小寫（同一張表的 `sub_93F430` 用 strnicmp）；
  2. `qsort` 的比較函式 `sub_A11230` 沒有追，這裡假設位元組長的詞優先，同長照表的順序；
  3. 詞照原樣保留前後空白（假設和 `string-c.tbl` 用同一個 loader）；
  4. 全是 `*` 的詞會跳過，避免無限循環（本機表沒有這種詞）。
- 待驗證：3.81 是否同樣過濾（快照有 `obscene` 字串，但過濾函式沒有追）；真機上送出後由伺服器回 169 的備註顯示。

## 怎麼驗證
- `dotnet build engine/source/OfflineClient.csproj -c Release`：0 錯誤（3 個警告都是原本就有的，在 `HtmlActionTests.cs`、`MagicUi.cs`）。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass。
- 依本機限制，沒有在本機執行任何測試程式；單元測試和離線測試交給 PR 的 CI。

## 未驗證
- 沒有跑本機測試（規定不能跑），以 CI 結果為準。
- 沒有真機驗證。
- 聊天顯示的過濾（選項 1009）不在這一組。

## 產出路徑
- `engine/source/ObsceneFilter.cs`、`engine/source/ObsceneFilterTests.cs`、`engine/source/NativeUi/PledgePanel.cs`、`engine/source/ClanUi.cs`、本報告。

## 給下一棒的提示
- 如果要確認推測 1、2：在 TW16 IDA 看 `sub_51F900`（大小寫）和 `sub_A11230`（qsort 比較函式）。若順序不同，只要改 `ObsceneFilter.Parse` 的排序。
- 聊天顯示過濾（1009）可以直接用 `ObsceneFilter.Current?.Filter(...)`。
- `sub_B2F130`／`sub_B2F460`（血盟窗的其他文字）等呼叫者若要接，用同一個 `ObsceneFilter.Apply`。
