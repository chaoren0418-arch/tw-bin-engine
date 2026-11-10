# Issue #757 T319：P-A 參考客戶端 UI XML 來源

依據：[`docs/reference/pak-resources.md`](../reference/pak-resources.md)（PAK，T318／#752）§6 P-A；主控裁定沿用 #533 先例（`TW_SPELLLIST_CLIENT`），執行期讀本機參考客戶端，**不複製進 repo**。

## 做了什麼

- **新檔 `engine/source/NativeUi/RefClientXmlSource.cs`**
  - 參考客戶端位置的優先順序：環境變數 `TW_REF_CLIENT`（非空）→ `<stage>/runtime/native-ui.json` 的 `"refClient"`（字串路徑；`false` 或 `""` 代表關閉）→ 預設 kor25 `D:\381懶人包kor25(原)`（PAK§3，與 `TW_SPELLLIST_CLIENT` 預設相同）。設定檔壞掉或 `refClient` 型別不對時忽略並記一行 log，跟 `UiOptions.Resolve` 的做法一樣。
  - 不使用參考客戶端的情況：路徑不是絕對路徑、或與主客戶端同一個資料夾。
  - 讀取 `Root\Tile.idx`／`Tile.pak`，用 `FileAccess.Read`／`FileShare.Read` 開檔，不寫任何檔案。idx 格式檢查：筆數×28+4。名稱含不可列印字元的筆數跳過；同名的筆數不使用。
  - 查檔順序跟 FileManager 一樣：先找 `<stem>-c.xml`，再找原名。檔案以 `X?xm` 開頭時，沿用 `UiXmlSource.Decrypt` 在記憶體解密；以 `<` 開頭（可帶 BOM）的明文 XML 直接用；其他內容回傳 null。單筆大小上限沿用 `ClientArchive.MaxEntrySize`（8 MiB），越界或截斷都回傳 null。
  - 每次取到檔案都記錄 SHA-256，分三個：idx、pak 內原檔、解密後內容。log 一行 `ui-xml-ref <name> -> <root>\Tile.pak:<實際檔名> plain sha256 …`，`Served` 屬性列出已提供的檔案。
  - 參考客戶端缺檔或讀不到時，回傳 null 並把原因寫在 `Unavailable`，之後不再重試；查詢結果（包括 null）依檔名快取。
- **掛點（只改原生 UI 資源層，共 2 處）**
  - `UiXmlSource.cs`：新增 `Reference` 屬性，預設 null。只有主客戶端 Tile.pak 的 `-c` 名稱和原名**都找不到**時，才去問參考客戶端。所以主客戶端有的檔案永遠優先。
  - `SurfCatalog.cs` 的 `UiResources.Open()`：產品路徑（`UiResources.Shared`）設定 `Xml.Reference=RefClientXmlSource.Configured(res.Root)`。`new UiResources(...)`（合成測試）不掛參考客戶端，既有測試行為不變。
  - 呼叫端拿不到檔案時，`ReadText`／`Load` 照舊丟 `UiLayoutException`，由原本的備援接手（WinForms 視窗或聊天行）。沒有做任何替代圖或替代版面。
- **新檔 `engine/source/NativeUi/RefClientXmlSourceTests.cs`**：`[WorldModuleTest("ref-client-xml")]`，由 CI 的 `--module-tests` 自動發現。全部用合成的 idx／pak，測試涵蓋：
  - 設定優先順序（env／settings 字串、false、""、型別錯、JSON 壞、沒有設定檔）。
  - `Configured()` 的三種情況：與主客戶端同資料夾、相對路徑、settings 關閉。環境變數只在程序內暫設，測完還原。
  - 讀檔：`-c` 優先、加密檔解密、明文 XML、非 XML、大小 0、重名、越界、壞 idx、不可列印名稱、名稱大小寫、快取、路徑字元。
  - 從磁碟讀：測試輸出目錄下的暫存檔；缺 pak 的情況；確認讀完後檔案沒有被改動。
  - 掛點：主客戶端優先，而且這時根本不去問參考客戶端；主客戶端缺檔時改由參考客戶端提供；兩邊都沒有時回傳 null，`ReadText` 丟例外。
  - 暫存檔只刪測試自己建立的檔案與空資料夾，沒有遞迴刪除。

> 任務說「測試用 `[CheckGroup]`」：`[CheckGroup]` 只用在 `_integration/dev/*-tests`（DemoChecks）。`engine/source` 內的測試一律用 `[WorldModuleTest]` 自動發現，不必改任何註冊表；本任務限定改 `engine/source/NativeUi`，所以用後者。

## 怎麼驗證

- 本機建置：`dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -warnaserror:false -o scratch/t757-build`，0 錯誤。
  - restore 用 `scratch/nuget-757.config`（唯讀指向主專案 `engine/vendor`），`NUGET_PACKAGES=scratch/t757/packages`。
  - 這樣設定是為了避開 #675／#676 記錄的 NETSDK1130（WebView2）本機環境問題，和本次程式變更無關。
  - 剩下的 2 個警告不是新檔產生的。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`（4100 檔，0 錯誤）。
- 依 2026-11-09 本機執行限制，**本機沒有執行任何編出來的程式**。`ref-client-xml` 與既有的單元測試交給 PR 的 GitHub Actions CI。
- `rebuild-lock.json` 未改動。

## 未驗證什麼（待驗證）

- `ref-client-xml` 的實際執行結果：等 CI。
- 真實 kor25 的 `Tile.idx`／`Tile.pak` 能不能用本類別讀到、雜湊是否與 PAK§4.1 一致：本機不能執行程式，所以沒測。用 PakViewer 查到的值見 PAK§4.1，例如 `FishingUI` 解密後 `205b76bb…`。
- 參考 XML 的版面與 TW16 相同：仍是**推測**（PAK§7）。
- 現有面板都還沒有註冊上述缺檔的 XML，所以目前產品畫面不會有變化。實際使用要等 P-D 逐窗面板。

## 產出路徑

- `engine/source/NativeUi/RefClientXmlSource.cs`（新）
- `engine/source/NativeUi/RefClientXmlSourceTests.cs`（新）
- `engine/source/NativeUi/UiXmlSource.cs`、`engine/source/NativeUi/SurfCatalog.cs`（掛點）
- 本報告 `docs/reports/issue-757.md`
- 分支 `task/757`

## 給下一棒的提示

- **P-D 逐窗面板**（例如 `OptionPanel`、`FishingPanel`）：`[UiPanel]` 的 Layout 照常填 XML 基本名稱（例如 `"OptionUI"`），`UiResources.Shared.Xml.Load` 會自動在主客戶端缺檔時改讀參考客戶端。
  - 要注意：`UiRoot.Build` 是全有或全無。參考客戶端不存在時，新面板的 Layout 會讓整個原生 UI 啟動失敗，退回舊 HUD。
  - 所以新面板要嘛在 Build 之外**延後載入**、失敗時只關掉那一窗；要嘛先確認 `UiResources.Shared.Xml.Reference?.TryRead(name)` 不是 null 才註冊。建議前者。
- **P-B surf 備援**可沿用同樣的 env／settings 解析：直接用 `RefClientXmlSource.ResolveRoot`／`Configured`，不要另訂變數。參考客戶端的 Sprite pak 沒有 pin，要像本類別一樣逐檔記錄 SHA-256。
- 使用者可以在 `runtime/native-ui.json` 加 `"refClient":"D:\\天堂參考用客戶單\\89天堂"` 換成 t89，或加 `"refClient":false` 關閉參考客戶端。
