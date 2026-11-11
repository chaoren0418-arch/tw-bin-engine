# #1009 T438：Q9-C HTML `<date>`／`<time>`／var 998–999

依據：`docs/reference/followups-17.md` §3.2、§3.4、§7（T422／#966）。

## 做了什麼
- `engine/source/NativeUi/HyperTextLayout.cs`
  - 新增 `HyperTextClock`：
    - `Convert`：照 `sub_A23760` 換算。值 `< 0x80000000` 直接當 `time_t`；否則減 `2019686400`、年份 +64；再用 UTC（`gmtime`，推測）。
    - `Date`：`"%2d%s %2d%s"`＝月＋字串 762＋空白＋日＋字串 763。字串從 `ServerMessageText.Table`（執行時唯讀讀 `string-c.tbl`）取；沒有表（CI）時用 §3.2 已驗證的「月」／「日」；表比 763 短時是空字串（`sub_4EAFF0`）。
    - `Time`：`"%02d:%02d"`；`VarTime`（#999）：`"%2d:%2d"`。
    - 時間來源：最後收到 S123（`WorldSession.ServerTime`）或 S8（`Status.Time`）的那個 session，取最後收到的那一種。都沒有時用 0。
  - `HyperTextMarkup.Parse`：`<date>`、`<time>` 輸出文字；`<var src="#998">`＝玩家名稱、`#999`＝`VarTime`。這兩個是客戶端變數，不讀參數。
- `engine/source/NpcTemplates.cs`
  - `Render` 有一樣的四項，純文字與 `HyperTextMarkup` 一致，原生面板照樣對得上。`<date>`／`<time>` 不再標「部分原始標記尚未支援」。
  - 同檔新增 `partial class WorldSession` 的兩個**觀察型** `[WorldReceive(123/8,Observe=true)]`，用自動發現登錄，只記下 session，不改核心 S123／S8 處理。沒有改 `WorldSession.cs`／`NpcInteraction.cs`。
- 新檔 `engine/source/NativeUi/HyperTextDateTests.cs`（`[WorldModuleTest("hypertext-date")]`）：
  - 換算：0、0x7FFFFFFF、0x80000000→2038-01-19 03:14:08、0xFFFFFFFF→2106-02-06（照原版規則，不是無號 time_t 的 02-07）；
  - 格式補寬；字串表 762／763；
  - NpcTemplates 與 HyperTextMarkup 的純文字、選項一致（含 `<a>` 裡的 `<date>`）；
  - #998／#999 不讀參數，#997 仍是參數；
  - 觀察器有登錄、時間來源有退回。

## 怎麼驗證
- 已驗證：`dotnet build engine/source/OfflineClient.csproj -c Release` 成功，0 警告、0 錯誤。
- 已驗證：`python -B -X utf8 engine/tools/check-no-game-assets.py` → pass。
- 待驗證（CI）：依本機限制沒有在本機執行 `--module-tests`。`hypertext-date` 與既有群組（`hypertext-layout` 等）交給 PR 的 GitHub Actions。
- 換算的期望值用 python `datetime` 另外算過（1970-03-05 09:05、1970-11-23 18:30、0x80000000、0xFFFFFFFF）。

## 未驗證什麼
- 推測：
  - `dword_154334C` 的值是不是 S123／S8 的遊戲時間（§3.2 只確認寫入者有 `sub_94DA90` 等）；
  - `sub_A23F20` 是 `gmtime`；
  - 還沒收到時間時原版顯示 0（1月 1日）。
- 真機再確認：
  - 4 頁「今天是 `<date>`」（`hometown-c`、`hometown1-c`、`othertownh-c`、`secretary1-c`）的實際畫面；
  - 本機 `string-c.tbl` 第 762／763 筆的內容（本任務沒有再用 PakViewer 讀；字串在執行時從客戶端讀，沒有複製進 repo）。
- 沒做：CodePage 1252 的 `strftime("%b %d")` 分支（本客戶端是 CP950）；var N≥1000（屬 Q9-D）。

## 產出路徑
- `engine/source/NativeUi/HyperTextLayout.cs`、`engine/source/NpcTemplates.cs`、`engine/source/NativeUi/HyperTextDateTests.cs`、本報告。
- 分支 `task/1009`。

## 給下一棒的提示
- Q9-D（var N≥1000）可以接在 `HyperTextMarkup.Parse`／`NpcTemplates.Render` 的同一個 `var` 分支：在 `#998／#999` 的判斷後面加，兩邊要一起改，純文字才會一致（`hypertext-layout` 的 Parity 測試會檢查）。
- 如果之後 `NpcInteraction.RenderHyperText` 可以改，可以把時間直接當參數傳進來，取代 `HyperTextClock` 的全域 session 記錄。
- 真機（Codex／dot）：到有「今天是」的村莊 NPC 對話，比對日期與遊戲內時間是否一致。
