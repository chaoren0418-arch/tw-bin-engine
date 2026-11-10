# Issue #760 報告：T322 P-B 參考客戶端 surf 備援

依據 `docs/reference/pak-resources.md`（PAK）§6 的 P-B，以及主控裁定（依 #533 先例，執行期讀本機參考客戶端，優先用 kor25，不複製進 repo）。

## 做了什麼

1. **新檔 `engine/source/NativeUi/RefClientSurfFallback.cs`**
   - 參考客戶端的根目錄跟 P-A 共用：`RefClientSurfFallback.For(RefClientXmlSource)`。
     - 解析順序：`TW_REF_CLIENT` → `native-ui.json` 的 `refClient` → kor25 預設。
     - P-A 關閉時（`refClient:false`、路徑不是絕對路徑、或跟主客戶端是同一個資料夾），P-B 也一起關閉。
   - 讀檔方式（全部唯讀）：
     - 唯讀開啟參考客戶端的 `Sprite.idx`、`Sprite00–15.idx`，在記憶體中解析並算 SHA-256。
     - 從對應的 `.pak` 依 offset 只讀需要的那一筆，算出 payload 的 SHA-256。
     - 參考客戶端的 `.pak` 每個 0.4–1.9 GB，**刻意不整檔 hash**。來源以「idx SHA-256＋payload SHA-256」識別。
     - 讀到的東西只放在記憶體與日誌，不寫任何檔案。
   - 找檔順序與 `SurfCatalog.Candidates` 相同：先 `<F>c.img` 再 `<F>.img`，各自先查 `Sprite{h}` 再查 `Sprite`，`h = 名稱位元組和 % 16`。
   - 只找 `.img`。同號的 `.tbt` 屬於 Icon 群組（T320／#758），不拿來當 surf。
   - 遇到以下情況一律回傳 null，由呼叫端走既有備援（略過元素／WinForms），**不畫替代圖**：
     - idx 裡同名出現兩次
     - 越界或大小不合理
     - 解不開的 `.img`
     - 參考客戶端不存在
2. **既有 surf 載入點只加一個掛點**：`engine/source/NativeUi/SurfCatalog.cs`
   - 新增 `SurfCatalog.Reference` 屬性，預設 null，此時行為與日誌都跟原本一樣。
   - `Get()` 在本機查不到時才呼叫 `Reference.Serve(...)`。查到的圖跟本機圖一樣放進 LRU。
   - 兩邊都查不到時，日誌在原本那一行後面加上「；參考客戶端 X 也沒有」。
   - `UiResources.Open()` 多一行：`res.Surfs.Reference=RefClientSurfFallback.For(res.Xml.Reference)`。
3. **檔名規則（`Serve` 內）**
   - 檔名優先取本機 `SurfList.xml` 的 `file` 屬性。
   - 該列沒有 `file`（id 6310–7246 共 343 列，Login／Select／Create 都在這段），或 SurfList 根本沒有這個 id（9183／9184、8670–8689、8734／8735），就**用 id 當檔名**。
     - 依據：surf 載入函式 `sub_E55DD0` 以 `"%d.img"`（VA 0x11DFA2C，issue-758）組檔名；而 SurfList 有 `file` 的列，全部都是 `file == id`。這一點是**推測**。
   - 檔名取自 id 時，**先查本機**已鎖定的 Sprite 封存（主客戶端優先）。例如 6523／6525、SelectUI 的 6404–6494 本機都有；本機沒有才查參考客戶端。
4. **新檔 `engine/_integration/dev/native-ui-tests/RefClientSurfFallbackTests.cs`**
   - 以 `[CheckGroup("native-ui-ref-surf")]` 註冊，同時加入 CI 已在跑的 `native-ui-resources` 群組（Order 51）與 Default 群組（Order 127）。
   - 主客戶端與參考客戶端的 Tile、Sprite×17、SurfList、.img、.tbt 全部是合成資料，寫在 `scratch/issue-760/synthetic/`。
   - 涵蓋的情況：
     - 沒掛參考時行為不變
     - 本機優先
     - `-c` 優先
     - 沒有 `file` 時用本機 `%d.img`
     - 不在 SurfList 的 id 走參考客戶端
     - 雜湊封存優先於 `Sprite.pak` 誘餌
     - `.tbt` 不當 surf
     - 重名、解不開、參考客戶端不存在
     - 日誌與 `Served` 記錄了 root、payload SHA-256、idx SHA-256
     - 參考檔案沒有被改動

未改 `Program.cs`、`DemoChecks.csproj`、註冊表、`rebuild-lock.json`、`.github/workflows/build.yml`。

## 實際覆蓋（kor25 預設，只讀靜態查證）

- **查法**：
  - PakViewer CLI `pak list` 掃過 4 套客戶端的**所有 .idx**：
    - `0-客戶端` 24 個：`Sprite`、`Sprite00–15`、`Text`、`Tile`、`LiTo\LTicon`、`LTsprite`、`LTsurf`、`LTtext`、`LTtile`
    - kor25、t89、ly 各 19 個：`Sprite`、`Sprite00–15`、`Text`、`Tile`
  - 另用 `py -3 -I` 腳本照引擎的查找順序，直接讀 idx 驗算（腳本與輸出在 `scratch/t760/`，不進 git）。
  - 各窗的 surf 清單取自 kor25 解密後的 XML（`pak_extract.ps1` 輸出到 `scratch/pak-extract/`）。
- **SurfList.xml**：`0-客戶端` 與 kor25 的內容逐 byte 相同（SHA-256 `a596c2529a07…`）。

| 視窗 | surf 數 | 本機引擎讀得到（含 id 檔名） | 要靠參考客戶端 | kor25 補得到 | 仍缺 |
|---|---:|---:|---:|---:|---|
| LoginUI | 44 | 0 | 44 | **44** | — |
| SelectUI | 50 | 35 | 15 | **15** | — |
| CreateUI | 73 | 48 | 25 | **25** | — |
| RenewalMailUI | 12 | 10 | 2（9183、9184） | **2** | — |
| ResetStatUI | 37 | 14 | 23 | **23** | — |

**說明**
- CreateUI 要補的是 25 個，比 PAK 表的 19 個多。ResetStatUI 是 23 個，比 22 個多。原因：PAK 把只在 `LiTo\LTsurf.pak` 的圖算成「本機有」，但引擎的 surf 路徑不讀 LTsurf，所以這幾個要由參考客戶端補。
- **PAK 寫「待找」的 8734／8735，這次在 kor25 找到了**：
  - `Sprite01/8734.img`，38,972 B，`4aab1152…554d28a2`
  - `Sprite02/8735.img`，38,972 B，`1bc2b86e…ccde1c0a`
  - ly 也有同樣的檔。
- **PAK 寫「CreateUI 剩 2 個」，這 2 個是 6523、6525**：本機 `Sprite11/6523.img`、`Sprite13/6525.img`（各 9,608 B）就有，只是 SurfList 該列沒有 `file`，現在依 id 檔名從本機讀。kor25 與 ly 只有同號的 `.tbt`（Icon），不採用。
- 9183／9184：
  - kor25 `Sprite00/9183.img`，768 B，`3c5b830d…dd49bcc6`
  - kor25 `Sprite01/9184.img`，768 B，`b298af6a…217c061c`
  - 與 PAK§5 記錄的 ly 雜湊相同。
- 每個 id 對應的 kor25 封存、檔名、大小與完整 SHA-256，看 `scratch/t760/coverage.txt`（本機重跑 `py -3 -I scratch/t760/coverage.py` 可重產）。
- kor25 的 idx：`Sprite.idx` 跟本機鎖定的 `327c0637…` 相同；`Sprite00.idx` 是 `1d7bdcec2e8fe431…`。
- **在 kor25 這 5 窗沒有任何缺圖**。換別套當參考時：
  - t89 缺 LoginUI 7231，SelectUI 7232–7238，CreateUI 7224、7225、7707、7708，RenewalMailUI 9183、9184，ResetStatUI 8670–8689、8734、8735。
  - ly 跟 kor25 一樣全部補得到。

## 怎麼驗證

- `dotnet build engine/source/OfflineClient.csproj -c Release`：成功，0 錯誤；2 個警告，都不在本次改動的檔案。
- `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：成功。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`status: pass`，0 錯誤。
- **測試執行交給 GitHub Actions CI**。依本機規定，本機沒有執行任何編譯出的程式。新測試掛在 CI 已在跑的 `native-ui-resources` 步驟裡。

## 未驗證什麼

- **CI 結果**：push 後以 PR 的 CI 為準，結果見 PR。
- **實機畫面（待驗證，交給 Codex）**：
  - kor25 的圖放進 TW16 版面後，位置與外觀是否正確。
  - 依據只有「事件名稱相同」與「SurfList 相同」，這部分是**推測**。
- 沒有 `file` 屬性的列，原版 BIN 是否真的用 id 組檔名（**推測**，依 `sub_E55DD0` 的 `"%d.img"`）。
- 參考客戶端的 `.pak` 沒有整檔 hash，只核對 idx 與單筆 payload 的 SHA-256。
- P-D 的各窗面板還沒建立，所以目前實際呼叫到這個備援的，只有已經在用 `SurfCatalog` 的面板。

## 產出路徑

- `engine/source/NativeUi/RefClientSurfFallback.cs`（新）
- `engine/source/NativeUi/SurfCatalog.cs`（新增 `Reference` 屬性、`Get` 的備援掛點、`Open` 多一行）
- `engine/_integration/dev/native-ui-tests/RefClientSurfFallbackTests.cs`（新）
- `docs/reports/issue-760.md`（本檔）

## 給下一棒的提示

- P-D 做 Login／Select／Create／ResetStat／Mail 面板時，`UiResources.Shared.Surfs.Get(id)` 會自動走本機 → 本機 id 檔名 → 參考客戶端，不需要另外接。
- 日誌關鍵字：
  - `surf-ref <id> -> <root>\SpriteNN.pak:<name> sha256 …`：圖來自參考客戶端
  - `依 %d.img 用本機`：用 id 檔名從本機讀到
  - `參考客戶端 … 也沒有`：兩邊都沒有
- `UiSurface.Archive` 以 `ref:` 開頭，表示圖來自參考客戶端。
- 想換成 ly 當參考客戶端：設 `TW_REF_CLIENT=D:\天堂參考用客戶單\雷雨天堂`（這 5 窗也全部補得到）。t89 不建議用，缺圖多。
- PAK§4.1 的 8734／8735「待找」與 CreateUI「剩 2 個」可以更正：kor25 有 8734／8735；6523／6525 本機就有。
