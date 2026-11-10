# Issue #817：T354／R7-N 參考客戶端字串表

依據：`docs/reference/gap-matrix.md` 15.6 節 R7-N；#763、#768、#766 報告；PAK§3、§5。

## 1. 字串表用哪一份（15.6「要先裁定」）

**決定：用參考客戶端自己的 `Text.pak`，也就是 `RefClientXmlSource` 讀 XML 的同一套客戶端（預設 kor25）。**

理由（PakViewer CLI `pak list`／`pak extract`，只讀，解出檔只放 `scratch/t817/`，沒進 git）：

| 表 | 本機 `0-客戶端` | kor25 `D:\381懶人包kor25(原)` |
|---|---|---|
| `string-c.tbl` | 第 32559 筆，105,931 B，解出 SHA-256 `4ebbc1a7…a22d43e9`，表頭 **3484** 筆 | 第 33458 筆，127,474 B，`9b165baa…6a6531b6`，表頭 **4703** 筆 |
| `desc-c.tbl` | 第 4201 筆，249,300 B，`f06c94f4…28adb7df`（與 `TextArchive.DescSha256` 相同），表頭 17001 筆 | 第 4415 筆，340,551 B，`67242c39…3568d354`，表頭 22617 筆 |

- P-D 面板 XML 用到的 String 編號最大到 4500（ResetStatUI 4082、AgitAuctionUI 4391、ReNStatusUIEx 4500…），Desc 最大到 21391。本機兩張表都不夠長，查不到。
- 兩張表都有的部分大多一樣：string 前 3484 筆有 3304 筆相同；desc 前 16945 筆有 15974 筆相同。
- 抽查的編號，kor25 的文字和視窗內容對得上：
  - 4082「Tip. 可以使用空白鍵提升每一等級」（重置窗的 Tip_Label）
  - 4391「競標目錄」（盟屋拍賣的 List_Button）
  - 3233–3235「陣營／階段／數值」（狀態窗）
- kor25 有 843 筆 string、5733 筆 desc 是佔位字 `dummyNNNN`（NNNN＝檔案行號＝編號＋2）。例如 3857–3878、4071–4073、4118、4163、4227、4408、4500、Desc 18792 等。這些不畫，不當成文字顯示。
- 其他參考客戶端（PakViewer 也列過）：
  - ly `雷雨天堂`：string-c.tbl 156,604 B
  - t89 `89天堂`：string-c.tbl 129,276 B

  都沒採用。因為面板 XML 預設來自 kor25，編號以 kor25 為準。換了參考客戶端（`TW_REF_CLIENT`／`native-ui.json`）時，字串表也跟著換。

**推測**：TW16 自己的 String／Desc 編號和 kor25 一致。原因是這些視窗的 XML 本機沒有，無法直接比對。

## 2. 做了什麼

- **新檔 `engine/source/NativeUi/RefClientTextSource.cs`**
  - 在執行期讀參考客戶端 `<root>\Text.idx`／`Text.pak` 的 `string-c.tbl`（`String`）與 `desc-c.tbl`（`Desc`）。用現有的 `TextArchive` 在記憶體內解碼，不寫檔。
  - 每張表在 UI log 記一行：`ui-text-ref <表> -> <root>\Text.pak:#<筆> plain sha256 <64 碼> (count, size, 來源, 是否與 kor25 相同)`。
  - 不設 pin，因為參考客戶端可以由使用者設定。kor25 的兩個 SHA-256 只拿來比對、寫進 log。
  - `Resolve(kind, value)` 統一當作 `TextResolver` 用：
    - `"4391;1"` 取第一欄。
    - 編號超出筆數、空字串、`dummy*` 都回 null。
    - 去掉色碼 `\fN`／`\FN`／`\<`，並修掉尾端空白與 tab。這兩項是推測，只影響顯示。
    - 結果有快取。
  - `RefClientTextSource.Resolver(UiResources?)` 是一行接線用的方法，會先在背景載入兩張表，避免第一次畫面要等。
  - 沒有參考客戶端時回 null，行為和現在一樣（字不畫）。
- **`NativeUi/SurfCatalog.cs`（只改 `UiResources`）**：新增 `Text` 屬性。`Open()` 依 `Xml.Reference` 建立，所以和 XML 用同一個 root。
- **各 P-D 面板，註冊行各加一行接線**：`{TextResolver=RefClientTextSource.Resolver(root.Resources)}`。涵蓋：
  - `OptionPanel`、`CreateCharPanel`、`SelectCharPanel`、`MailPanel`
  - `ResetStatPanel`、`StatInfoPanel`、`RenewalStatusPanel`
  - `AgitAuctionPanel` 與兩個子窗（AuctionInfo、Bid）
- **重置窗 Faulted 時改開 WinForms（`CharResetUi.cs`）**：
  - 原生重置窗開啟後，每 150 ms 檢查一次。間隔和 WinForms 對話框的計時器相同。
  - 如果重置還在進行，而面板進了 `UiRoot.Faulted`（Paint／Update／輸入時拋例外），或原生 UI 已關閉／換了 root：關掉原生窗，改開 `CharResetUi` WinForms 對話框。對話框用同一個 snapshot，也走同一條 `SendCharReset`。
  - 重置結束（idle／ended）、換了登入、或窗本身已關閉時，停止檢查。
  - 判斷寫在 `CharResetFallback.Decide`，可以離線測試。
  - `--classic-ui` 本來就不走原生窗，不受影響。
- `ResetStatPanel.cs` 檔頭的註解改成新的行為。
- **新檔 `engine/source/NativeUi/RefClientTextTests.cs`**（`[WorldModuleTest("ref-client-text")]`，自動發現）。測試內容：
  - 編號解析；`dummy`、空白、超出筆數、色碼、尾端 tab 的處理。
  - 用合成的 Text 封存測 String／Desc 查詢、快取，以及 SHA-256 與筆號紀錄。
  - 缺表、表名重複、表頭錯誤、資料夾沒有 Text.idx 的情況。
  - `For(null)`、`Resolver(null)` 回 null；字串表與 XML 用同一個 root。
  - `CharResetFallback` 的 9 種情況。
  - 本機有 kor25 時，多核對一次真表的 SHA-256、筆數與 4082／4391／4118。只在記憶體讀，不印遊戲文字。CI 沒有 kor25，會略過。

沒改：`WorldSession.cs`、`UiPanelRegistry.cs`、`UiRoot.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`、`docs/REPORT.md`。

## 3. 怎麼驗證

- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`（4280 檔，0 錯誤）。
- **本機編譯沒有成功，原因是環境問題，和本次改動無關**：
  - 用主專案的 `engine/vendor`（套件快取放 `scratch/t817/packages*`）restore 之後，NuGet 的檔案清單漏掉 WebView2 套件的 `.dll`／`.targets`。結果選到 `lib/*.winmd`，出現 `NETSDK1130`。
  - 這個錯誤在 C# 編譯之前就發生，還沒編到本次改動的檔案。
  - `rebuild.py --build` 失敗在同一步。
  - 編譯與測試改由 CI 驗證。
- **CI（PR #863，run 42864237178／42864237083）**：`dotnet`、`map-continuity`、`npc-offline` 全部 pass。
  - `--module-tests` 整份 `failed: 0`。
  - `ref-client-text`（`P06.NativeUi.RefClientTextTests.Run`）46 項檢查全部通過；CI 沒有 kor25，真表核對照設計略過。
- 依本機執行限制，沒有執行任何編譯出來的程式；`--module-tests` 交給 CI。

## 4. 已驗證／推測／待驗證

- **已驗證**（PakViewer，只讀）：
  - 4 套客戶端 Text.idx 裡的字串表名稱與大小（第 1 節）。
  - 本機與 kor25 兩張表的 SHA-256、筆數、相同列數。
  - 第 1 節列出的編號，對應的 kor25 文字。
- **推測**：
  - TW16 的編號與 kor25 一致。
  - 去色碼、修尾端空白屬於顯示細節。
  - `dummy*` 不畫（原版可能直接畫出 `dummyNNNN`；這要真機確認）。
- **待驗證（真機，交給 Codex／#475）**：
  - 重置窗的 Tip、拍賣窗的「競標目錄」、狀態窗的標籤等文字，在畫面上的位置與字型。
  - 原生重置窗 Faulted 時，WinForms 是否確實接手（真機很難觸發 Faulted，可以只看 log 那一行）。

## 5. 產出路徑

- `engine/source/NativeUi/RefClientTextSource.cs`（新）
- `engine/source/NativeUi/RefClientTextTests.cs`（新）
- `engine/source/NativeUi/SurfCatalog.cs`、`engine/source/CharResetUi.cs`、`engine/source/NativeUi/ResetStatPanel.cs`
- 一行接線：`AgitAuctionPanel.cs`、`CreateCharPanel.cs`、`MailPanel.cs`、`OptionPanel.cs`、`RenewalStatusPanel.cs`、`SelectCharPanel.cs`、`StatInfoPanel.cs`
- 本報告 `docs/reports/issue-817.md`
- 分支 `task/817`

## 6. 給下一棒的提示

- 有幾個面板檔頭的註解還寫「TextResolver 在產品裡是 null」，現在已經過時。本張只允許每個面板加一行接線，所以沒改。涉及的檔案：
  - `OptionPanel.cs:32`
  - `AgitAuctionPanel.cs:32–33`
  - `RenewalStatusPanel.cs:30`
  - `StatInfoPanel.cs:45`

  可以併進 15.6 的小清理。
- PAK§5 的 P-F（血盟刪除確認字串 4118）：kor25 的 4118 是 `dummy4120`，用這個來源也拿不到。ly 有文字，但 ly 的表頭和實際行數對不上（PAK§5）。所以 P-F 維持現狀。
- 要讓別的面板（例如 `FishingPanel`、`GameRankingPanel`）也取字串時，同樣在註冊行加 `{TextResolver=RefClientTextSource.Resolver(root.Resources)}` 即可。前提是面板先有 `TextResolver` 屬性。
- 本機建置的 NETSDK1130 問題建議另開 `needs:local` 查：其他 worktree 怎麼 restore 才能讓 NuGet 看到 WebView2 的 `buildTransitive` targets。
