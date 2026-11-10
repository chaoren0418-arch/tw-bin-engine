# Issue #762：T324 P-D 原生信件三窗（RenewalMailUI.xml，參考客戶端）

依據：[`docs/reference/pak-resources.md`](../reference/pak-resources.md)（PAK，T318／#752）§4.2 的「1:155 S_READ_MAIL、1:99 S_MAIL_INFO、3:C_MAIL(95)、4:RenewalMailUI」列。照主控裁定與 #533 的先例：XML 與圖片在**執行期**從參考客戶端（預設 kor25，PAK§3）的原路徑讀取，記錄來源 SHA-256，**不複製進 repo**。P-A（T319／#757）負責讀 XML，P-B（T322／#760）負責補 surf；做法與 #766（狀態窗）、#768（選項窗）相同。

## 做了什麼

### 1. 新檔 `engine/source/NativeUi/MailPanel.cs`

- `[UiPanel(188)]` 自動發現，Id 是 `RenewalMailUI`，放在 Window 層，實作 `IUiClosablePanel`、`IUiMovablePanel`、`IUiTextInput`。
  - 186／187 已被 main 上的 SelectCharPanel 等使用，所以取 188。
- **載入**：註冊在本機一定有的 `uistatus86` 上，`Load` 裡再呼叫 `UiResources.Xml.Load("RenewalMailUI")`。
  - 查找順序：主客戶端 Tile.pak → 參考客戶端 Tile.pak，兩邊都先找 `-c`。P-A 會記錄 `ui-xml-ref`。
  - 沒有參考客戶端、讀不到 XML、或 XML 版面不符時，記下 `Unavailable` 並寫進 log（前綴 `mail-ui`）。這時面板不會開，信件改走既有的 WinForms 視窗。
- **版面照 XML 建**：
  - 三個 Window：`MailList_Window`、`ReadMail_Window`、`WriteMail_Window`。三窗位置相同，一次只顯示一個；開窗時顯示清單。
  - 視窗大小用 XML 的 Width/Height（285×413），沒有時才用底圖大小。`Moveable` 是頂端可拖動的高度。
  - 元件有：Image、Label、Button／CheckButton、Edit／MultiEdit、ComboBox（含 UpDownList）、List（Horizontal／Vertical，含 Gap）、ScrollBar。座標相對於父元素。
  - `MailList` 的 19 個 `MailInfoNN` CheckButton 是信件列，列內有寄件人、標題、日期三個 Label。
  - `Include Xml="CommunityTab.xml"`（社群分頁列）不建，列在 `Skipped`。
- **圖片**：
  - surf 走 `SurfCatalog.Get`，本機查不到時由 P-B 改查參考客戶端。包括：
    - 關閉鈕 9183／9184（本機沒有，由參考客戶端補）
    - 捲軸 4444–4449、上一封／下一封 5429–5433（本機都有）
  - png 走 `OptionPngSource`：先查本機 Sprite，再查參考客戶端的 `Sprite*.idx`／`.pak`，log 記錄 idx 與內容的 SHA-256，前綴是 `option-png`。
  - 缺的圖不畫，也不用替代圖，全部列在 `Missing` 並寫進 log。
  - HighlightPng（滑過）與 DisablePng 不使用。
- **顯示的值**：資料來自 `WorldSession.MailSnapshot()`，跟 WinForms 視窗同一份。
  - 信件列：寄件人、標題、日期（`yy/mm/dd`，伺服器沒給月份時留白）。沒有信的列不畫。
  - `Count_Label`：信件數。
  - `Cost_Label`：郵資，一般信 50、血盟信 1000，跟 WinForms 視窗同值。
  - 信匣下拉選單：先用 TextResolver 解 String 4071–4073；解不到時用「一般／血盟／保管箱」。
  - XML 裡的設計期 Caption（`IU`、`item00`）一律不畫。
- **互動**：
  - 清單窗：
    - 勾選：單封、全選，再按一次全選會全部取消。
    - 排序：寄件人、標題、日期；同一顆再按一次會反過來排。
    - 切換信匣：送出 list。
    - 捲軸：上下鈕、滾輪。
    - 點信件列：送出 read 並切到讀信窗。
    - 寫信、存入保管箱、刪除：對勾選的信依序送出。
  - 讀信窗：
    - 寄件人、標題、本文用唯讀欄位顯示；本文會自動換行，可以捲動。
    - 上一封、下一封、回信（收件人預填寄件人）。
    - 存入保管箱、刪除：刪除後回到清單。
  - 寫信窗：
    - 欄位：收件人、標題、本文，各自有 `LimitText` 上限（280／20／256，以 UTF-16 單位計算）。
    - Tab 或 Enter 切到下一個欄位（ToNextFocus）。本文欄的 Enter 是換行。
    - 血盟信勾選。
    - 寄出：先用 `MailPackets.Write` 驗證；送出成功就回到清單，失敗則留在草稿。
    - 送出中不能打字，也不能再送一次。
  - 關閉事件：`CloseMailUI` 關閉面板；`CloseReadMailUI`／`CloseWriteMailUI` 回到清單。Esc 送出目前視窗的 `KeyboardEvent`。
  - 世界改變（MailStamp 不同）或離開世界時，自動關窗。
- **按鍵規則**：可列印的鍵不在 `Key` 處理，留給 WM_CHAR 進 `Char()`。理由是 `ProcessCmdKey` 回傳 true 會讓 WM_CHAR 不產生。編輯中的 Back／Tab／Enter／Delete／方向鍵／Home／End 由面板處理，不會傳到遊戲。
- **沒接上的**：`ExcludeMail`（MailExcludeUI）會記在 `Unhandled`。

### 2. `engine/source/MailUi.cs`（只加原生分流）

- `RequestMail()` 在原本的判斷（在世界中、已有視窗 handle、信件視窗沒開也沒排隊）之後，多一步檢查：原生 UI 開啟、面板已載入（`Available`）、而且沒有故障時，用 `UiRoot.Toggle(panel, panel.Open)` 開關原生信件窗，並在第一次使用時接上 `NativeMailHost`。
- `NativeMailHost` 只轉呼叫既有的 `MailSnapshot()`／`LoginConnection.Mail(...)`，傳入同一個 stamp 與 viewCurrent 檢查，跟 WinForms 視窗相同。
- 以下情況完全不變，照舊開 WinForms `MailDialog`：
  - `--classic-ui`（原生 UI 關閉）
  - 沒有參考客戶端或 XML
  - 面板故障

### 3. 新檔 `engine/source/NativeUi/MailPanelTests.cs`（離線測試）

- 掛 `[WorldModuleTest("mail-panel")]`，由 CI 的 `--module-tests` 自動發現。
  - 任務原本寫 `[CheckGroup]`，但 `[CheckGroup]` 只在 `_integration/dev`，`engine/source` 的測試要用 `[WorldModuleTest]`，所以照 #766／#768 的做法。
- XML、圖片、信件都是在記憶體裡合成的，元素種類仿照 RenewalMailUI.xml。
- 測試內容：
  - 註冊：含 `ClientForm.NativeMailPanel` 這個分流存在。
  - 解析：三窗、相對座標、橫排加 Gap、直排信件列、combo 背景不推進、捲軸參數、唯讀與 LimitText、缺圖路徑、Include 不建；錯誤根元素、重複窗、缺窗時拋出例外。
  - 值的對應：寄件人、標題、日期、無日期留白、信件數、信匣名稱、TextResolver、郵資、Caption 不畫。
  - 繪製：底圖、關閉 surf、勾選圖、捲軸滑塊位置、空列不畫、下拉清單、缺圖不畫。
  - 清單互動：勾選、存入與刪除依序送出、全選、三種排序、切換信匣、捲動、ExcludeMail 記錄。
  - 讀信互動：讀信、本文、上一封／下一封、唯讀欄位不取焦點、刪除、回信、關閉鈕、Esc。
  - 寫信互動：焦點、Tab／Enter、LimitText、Back、血盟信、送出成功、失敗、驗證錯誤、送出中鎖定、Esc。
  - 生命週期：拖動、移動、stamp 變更、離開世界、`UiRoot.Toggle`、沒有 host 時的離線開窗。
  - 不可用：沒有大小也沒有底圖時拋出例外；沒有 Width/Height 時借用底圖大小；缺底圖但有宣告大小時仍可用；從沒載入時不能開。

沒有改 `Program.cs`、`DemoChecks.csproj`、各註冊表、`MenuButtonsPanel`、`rebuild-lock.json`。

## 資源盤點（唯讀，PakViewer CLI）

- 用 `pakviewer-cli pak list` 列出下列 43 個 idx，清單放在 `scratch/t762/pak/`（不進 git）：
  - `0-客戶端` 24 個：`Sprite`、`Sprite00–15`、`Text`、`Tile`、`LiTo\LTicon`、`LTsprite`、`LTsurf`、`LTtext`、`LTtile`
  - kor25（`D:\381懶人包kor25(原)`）19 個：`Sprite`、`Sprite00–15`、`Text`、`Tile`
- 比對腳本是 `scratch/t762/inventory.py`（`py -3 -I` 執行），結果在 `scratch/t762/inventory.txt`。
- 11180–11182 另外用 `pak list --filter 1118` 查過 ly（雷雨天堂，17 個 `Sprite*.idx`）與 t89（89天堂），**都沒有**。
- **XML**：
  - kor25 `Tile` 只有 `RenewalMailUI.xml`（20,327 B，沒有 `-c` 版）。解密後的 SHA-256 是 `e6a0e037ca1567d3b0f1f1463fc66d5f8603ca07406adfca2aaed7adfd5622b9`，與 PAK§4.1 相符。
  - 主客戶端 Tile 沒有這個檔，只有 `MailExcludeUI.xml`。
  - `CommunityTab.xml` 只在 kor25，本面板不使用。

| 類別 | 編號 | 本機 0-客戶端 | kor25 | 結果 |
|---|---|---|---|---|
| surf | 9183／9184（關閉鈕） | 沒有 | `Sprite00/9183.img`、`Sprite01/9184.img` | P-B 由參考客戶端補 |
| surf | 4444–4449（捲軸） | 有（`.img`／`c.img`） | 有 | 本機 |
| surf | 5429、5430、5432、5433（上一封／下一封） | 有（`.img`／`c.img`） | 有 | 本機（同號 `.tbt` 不用，#758） |
| png | 10782、10810、10811（三窗底圖） | **0 個** | `Sprite08/10782c.png`、`Sprite00/10810c.png`、`Sprite01/10811c.png` | 執行期從 kor25 讀 |
| png | 10746–10751、10783–10808、10729、10892–10894 | **0 個**（24 個 idx 都沒有） | 全有 | 執行期從 kor25 讀 |
| png | **11180–11182**（讀信窗 `OK_Button`） | 沒有 | **沒有** | **缺，不畫**。ly、t89 也沒有。這顆鈕沒有圖就沒有點擊範圍；同一個事件 `CloseReadMailUI` 仍可以用關閉鈕或 Esc 觸發 |

## 怎麼驗證

- `dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o scratch/t762-build`：**0 個錯誤**。2 個警告都在既有的 `MagicUi.cs`。
  - 本機 restore 遇到 NETSDK1130，是環境問題，同 #766／#768。處理方式：
    - `scratch/t762/NuGet.Config` 指向主專案的 `engine/vendor`。
    - `NUGET_PACKAGES` 改指向 `scratch/t762/packages`。
    - `engine/source/obj` 底下 5 個 NuGet 還原輸出逐一刪除，沒有用遞迴刪除。
- 已合併 `origin/main` 後重新建置，結果相同。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`，0 個錯誤。
- 依 2026-11-09 的本機執行限制，**本機沒有執行任何編譯出來的程式**。`mail-panel` 的離線測試交給 PR 的 GitHub Actions CI（`--module-tests`）。
- CI 結果：以 PR 上最新 commit 的 CI 為準。

## 未驗證／待驗證

- **實機畫面（交給 Codex）**：
  - kor25 的 RenewalMailUI 版面和 png 放在 TW16 上是否正確。
  - 按選單 1 是否開出原生信件窗。
  - 中文輸入法（IME）組字後，能不能經由 WM_CHAR 進入各欄位。
  - 寄、讀、刪、存入保管箱在 P24Lab 實際往返是否正常。
- **以下規則是推測**，寫在檔頭；CRenewalMailUI 沒有反編譯：
  - 三窗同位置、一次一窗；Read／Write 關閉後回到清單。
  - Esc 對應 `KeyboardEvent`。
  - List 依宣告寬高推進；UpDownList 的 Image 是不推進的背景。
  - 沒有信的列不畫。
  - `Count_Label` 只顯示信件數（原版可能是「n/上限」）。
  - 日期格式 `yy/mm/dd`。
  - 郵資文字「郵資 N 金幣」。
  - `LimitText` 以 UTF-16 單位計算。
  - 回信時標題留白。
- **刪除不會先確認**：WinForms 視窗會先問「確定刪除？」，原生窗按下就送出。原版有沒有確認窗待查。
- **沒做**：
  - ExcludeMail（MailExcludeUI.xml）
  - 拖動捲軸滑塊
  - 滑過時的圖（Highlight）
  - CommunityTab 分頁列
  - 伺服器訊息（`MailView.Notice`）的顯示：原版可能顯示在聊天欄，目前只留在快照裡
- **文字**：String 4071–4073 是參考客戶端字串表的編號，跟本機 `string-c.tbl` 不一定對得上（同 #768），所以產品裡 `TextResolver` 是 null，改用內建名稱。

## 產出路徑

- `engine/source/NativeUi/MailPanel.cs`（新）
- `engine/source/NativeUi/MailPanelTests.cs`（新）
- `engine/source/MailUi.cs`（只加原生分流）
- `docs/reports/issue-762.md`（本報告）
- 分支 `task/762`
- 只在 scratch、未進 git：`scratch/t762/`，包含 PakViewer 清單、`inventory.py`、`inventory.txt`、NuGet 設定

## 給下一棒的提示

1. **真機驗收（Codex）**：在 P24Lab 用測試角色按選單 1 開信件窗，依序測試：
   - 切換信匣
   - 讀信、上一封／下一封
   - 寫信：中文 IME 輸入、血盟信勾選
   - 刪除、存入保管箱
   - Esc
   - log 要看到 `ui-xml-ref RenewalMailUI`、`surf-ref 9183`／`9184`、`option-png 107xx -> ref …` 等行，`mail-ui 缺圖不畫` 只應列出 11180–11182。
2. **OK_Button（11180–11182）**：四套客戶端都沒有。要不要另找來源，由使用者決定；目前不畫、不可點。
3. **刪除確認**：如果原版有確認窗，可以接 `YesNoPanel`（#180 順序）。
4. **ExcludeMail**：要另做 `MailExcludeUI.xml` 面板。主客戶端 Tile 就有這個檔（5,075 B）。
5. **伺服器訊息**：確認原版是否把 S186 結果顯示在聊天欄；若是，把 `MailView.Notice` 接到 ChatPanel 的系統訊息。
