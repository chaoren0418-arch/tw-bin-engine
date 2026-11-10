# Issue #739 T312：R5-G 原生公告板內容（code）

分支：`task/739`。依據：gap-matrix 第 14.6 節 R5-G、WIN§2 T130、#528 報告。本輪另外對 `TW1608262200.bin`（SHA-256 `e12949cf…41a2`）做了 Capstone 靜態反組譯，查出原本「推測／待驗證」的背景、列與文字座標（腳本與輸出只放在 `scratch/issue-739/`，沒有進 git）。

## 做了什麼
- **新檔 `engine/source/NativeUi/BoardPanel.cs`**
  - `BoardContentLayout`：把反組譯查到的版面寫成常數（見下表）。
  - `BoardText`：`sub_83F260` 的字寬規則（雙位元組字 12 px、單位元組 6 px，寬度扣到負數就截斷）、標籤寬 `(strlen+1)·6`、`%04d`，以及字串 540–544（有字串表就用表，沒有就用本機 `string-c.tbl` 的原文）。
  - `BoardListContent`（`BoardListWindow.Content`）：畫背景 656、選取列標記 657 和 8 列文字（編號／作者／日期／第二行標題）。滑鼠規則照 `sub_7CD080`：左鍵按一列就選取，再按同一列取消；左鍵雙擊會選取並閱讀；右鍵雙擊關窗；判斷列時只看 y。
  - `BoardReadContent`（`BoardReadWindow.Content`）：畫背景 670、標籤 542／543／541（調色盤 1）、作者、`%04d - 日期`、標題框和多行內文（調色盤 8）。
  - `BoardWriteContent`（`BoardWriteWindow.Content`）：原生發文窗。畫背景 670、裝飾 672／676、標籤 541，費用不為 0 時畫費用行 540；有標題框（上限 20 byte）和內文框（上限 512 byte），三顆鈕是 686 送出、678 關閉、674 返回。字元經 `IUiTextInput` 輸入（WM_CHAR，含 IME）；Backspace、Enter、Tab、Esc 在 `Key` 處理。標題空白時送出 → 顯示 544「請輸入題目。」，窗不關。
  - 三個面板都實作 `IUiClosablePanel`。
- **`engine/source/BoardUi.cs`**
  - `AttachNativeBoard` 掛上內容面板。清單和閱讀內容加在按鈕窗前面（Bar 層），發文窗加在按鈕窗後面（Window 層，蓋在清單按鈕上）。
  - 原生 UI 啟用時，清單窗的 Write 鈕（662）照原版 `sub_7CDE00` 改成**開原生發文窗**；按發文窗的 686 才送出。送出仍經 `BoardUi` 原本的確認框，以及長度／Big5 檢查。
  - 新增 `SelectedRow`／`SelectRow`：原生清單的選取和 WinForms 清單同步，所以閱讀鈕讀到的就是原生選的那一列。另外新增 `Submit`、`Notice`、`CanWrite`。
  - 原生 UI 關閉時完全沒有改變：WinForms `BoardUi` 照舊是整個公告板的入口。
- **新檔 `engine/source/BoardPanelTests.cs`**：`[WorldModuleTest("board-native-content")]` 離線測試，約 90 項檢查。
- 沒有改 `BoardPanels.cs`、`Board.cs`、`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

## 依據（靜態反組譯，VA；FO＝VA−0x400000）
| 項目 | 函式 | 結果 | 等級 |
|---|---|---|---|
| 清單窗背景 | `sub_7CC7C0`（vtable `0x1172F44`+8） | surf 656（`[+0x44]==1` 時 4732），矩形 (0,0,238,350) | 已驗證 |
| 清單列 | 同上 | 每頁 8 列；第 i 列 y＝50+30i；選取列在 (18,y) 畫 657；`%04d`（id%10000）在 (30,y) 寬 24；作者 (60,y) 寬 72；日期 (135,y) 寬 48；標題 (30,y+12) 寬 160；調色盤 8（`0x2108`） | 已驗證 |
| 板名列 | 同上 | `[+0x28]` 非空時畫在 (30,15)，調色盤 14。3.81 的 S_BOARD 沒有這個欄位 → 不畫 | 已驗證（不適用） |
| 清單窗滑鼠 | `sub_7CD080`（vtable +44） | 只處理 (0,0,238,350) 內；0x201 選取／取消；0x203 選取並 `sub_7CD970`（送閱讀）；0x206 關窗 | 已驗證 |
| 閱讀窗 | `sub_7CCB40`（vtable `0x1172EC4`+8）、建構 `sub_7CBA70` | 背景 670（alt 4733），矩形 (0,0,238,343)；542 在 (20,20)、543 在 (20,35)、541 在 (20,50)，調色盤 1；值畫在標籤後；`"%04d - %s"`（`0x1172E88`）；標題框 (20+標籤寬,50) 180×16；內文框 (20,67) 180×228 | 已驗證 |
| 發文窗 | `sub_7CCD90`（vtable `0x1172F04`+8）、建構 `sub_7CBE90` | 背景 670；672 (56,312)、676 (110,312) 是裝飾；541 在 (20,20)；費用 540 在 (18,269) 寬 160；標題框 (20+標籤寬,20) (180−標籤寬)×18，上限 20；內文框 (20,38) 180×228，上限 512；鈕 686 (159,302)、678 (213,297)、674 (90,299) | 已驗證 |
| Write 鈕／送出 | `sub_7CDE00`、`sub_7CDC70`、`sub_7CDDD0` | 662 開發文窗（金幣不足時改印系統訊息 189）；686：標題空白 → 544，否則送出、關窗、回清單；674：關發文窗 | 已驗證 |
| 字寬 | `sub_83F260` | 座標加上 UI 原點；`IsDBCSLeadByteEx` 為真算 12 px，否則 6 px；寬度扣到負數就停 | 已驗證 |
| UI 原點 | `dword_154524C`／`dword_1545250` | 800×600 下是 0（ui-layout.md V5）→ 原版三窗都在螢幕 (0,0) | 已驗證（沿用 V5） |
| 字串 | `string-c.tbl`（PakViewer CLI 讀 `0-客戶端\Text.idx`，輸出只在 scratch） | 540「費用為金幣 %d。」、541「題目:」、542「作者:」、543「討論號碼:」、544「請輸入題目。」 | 已驗證 |

推定／引擎決定：
- **窗的位置**：跟按鈕窗共用 `BoardWindowLayout.DefaultOrigin` (281,60)，這樣背景、文字和按鈕才對得齊。原版是 (0,0)，但 `BoardPanels.cs` 不在本任務可改的範圍，所以沒有改。
- **圖層**：清單／閱讀內容放在 `UiLayer.Bar`，讓既有按鈕窗（Window 層）畫在背景上面，也不會多占 4 層視窗堆疊的一格。發文窗放在 Window 層。
- **內文框**：行高 12 px，寬 180 px 自動換行。沒有反組譯多行文字元件 `sub_AC3BB0` 來確認（推定）；捲動也沒做。
- **發文窗操作**：輸入焦點、Tab／Enter／Esc 的行為、`_` 游標都是引擎決定。送出前仍跳 `BoardUi` 的確認框；這是引擎規則，原版沒有確認框。
- **待查**：4732／4733 背景的觸發條件沒有查到寫入者；金幣不足的 189 檢查沒有做（交給伺服器）；544 只顯示在 `BoardUi` 的提示列，沒有進聊天欄。

## 怎麼驗證
- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile <指向主專案 engine/vendor 的 scratch nuget.config>` → `PASS dev`（productDll `805629e4…`；lock 只是暫時的，沒有寫入）。
- `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`（引用剛建好的 product）→ 0 錯誤。既有的 `BoardPanelChecks` 沒有改動，型別也還編得過。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`（4112 檔，0 錯誤）。
- **依規定沒有在本機執行任何測試程式**。`board-native-content`、既有的 `board` 等群組以 PR 的 GitHub Actions CI 結果為準。
- 新測試涵蓋：
  - 反組譯常數逐項比對。
  - 截斷、標籤寬、`%04d`、換行。
  - 清單／閱讀／發文的顯示條件。
  - 列文字座標和截斷結果、背景與 657 的像素、選列／取消／雙擊閱讀／右鍵雙擊關窗。
  - 閱讀窗標籤、值與內文座標。
  - 發文窗：開窗條件、版面、20 byte 上限、Backspace／Enter／Tab／點框換焦點、686 送出（CRLF）、空白標題出現 544、678／674／Esc 關窗、在不同鈕上按下和放開不觸發、公告板關閉時發文窗跟著關。
  - 放進 `UiRoot` 後的命中與繪製順序。

## 未驗證什麼
- 沒有啟動遊戲，也沒有真機看過原生公告板（交給 Codex／dot 驗收）。真機要看的項目：
  - 背景、列與文字是否對齊按鈕；
  - 本機字型比 12 px 寬時會不會超出；
  - 發文窗用中文 IME 輸入；
  - 字母鍵會不會被遊戲熱鍵吃掉；
  - 確認框流程。
- `AttachNativeBoard` 和 `BoardPanelHost` 只有編譯過，沒有離線測試（需要真的 `ClientForm`）。
- WinForms `BoardUi` 窗在原生 UI 開啟時仍會一起出現（沿用 M7-U 的過渡做法）。

## 產出路徑
- `engine/source/NativeUi/BoardPanel.cs`（新）
- `engine/source/BoardUi.cs`
- `engine/source/BoardPanelTests.cs`（新）
- `docs/reports/issue-739.md`

## 給下一棒的提示
- **窗位置**：把 `BoardPanels.cs` 的 `BoardWindowLayout.DefaultOrigin` 改成 `(0,0)` 就符合原版；內容面板會跟著移動。改的時候 `BoardPanelChecks.cs` 的座標要一起改。
- **合併面板**：之後若把按鈕窗和內容面板合併成同一個面板，就能改回 Window 層，`BoardPanels.cs` 裡只有資料的 `BoardWriteWindow` 也可以拿掉。
- **原版行為收尾**：
  - 閱讀窗的 X（678）在原版只關閱讀窗；
  - 672／676 在閱讀窗綁的回呼是 `sub_7CD910`、`sub_7CD850`（推測是上一篇／下一篇），可以用同樣方法反組譯確認；
  - 金幣不足的 189 檢查。
- **重現反組譯**：`py -3 -I docs/reference/rea-scripts/disx.py 0x7CC7C0 0x380`（其他 VA 見上表）。
- **本機建置**：worktree 沒有 `engine/vendor`。用 `--configfile` 指向主專案的 `engine/vendor` 即可，不必建 junction。
