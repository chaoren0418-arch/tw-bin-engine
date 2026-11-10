# Issue #758：T320 P-C `.tbt` surf 解碼

依據：[`docs/reference/pak-resources.md`](../reference/pak-resources.md)（T318／#752）的 P-C 列。方法照 #417：唯讀靜態分析未加殼的 `TW1608262200.bin`（SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，本輪重算過），再用 PakViewer CLI 列出各 pak 的內容。沒有啟動遊戲、伺服器或 DB，也沒有執行任何編譯出來的程式。

## 結論

**TW16 的 surf 從來不讀 `.tbt`，所以不做「把 .tbt 解成 surf」。** 這兩個 XML 引用的 surf 編號雖然有同號的 `.tbt`，但那些檔案是 Icon 群組的圖示，不是 surf。拿來當 surf 畫，畫出來的是不相干的圖示，等於用替代圖，違反本任務「讀不到就走既有備援、不用替代圖」的規定。

本輪改做三件事：

1. 把這條規則和 TW16 的依據寫進 `SurfCatalog`。
2. surf 缺圖、但有同號 `.tbt` 時，日誌會指出那個 Icon 檔，方便判斷。
3. 用合成資料寫防呆測試。

`.tbt` 解碼器其實早就有了：`TbtImage`，在 `NativeUi/BuffIconsPanel.cs`。狀態圖示、物品圖示走的都是這個解碼器，屬於 Icon 路徑，本輪沒有改它。

## 已驗證

### 1. TW16 靜態：surf 只組 `.img`，`.tbt` 只給 Icon 群組

把格式字串掃出來，再找哪段程式碼引用它們。工具：`pefile`＋Capstone，腳本放在 `scratch/t758/strscan.py`、`xref.py`、`dis.py`，未入 git。反編譯片段沿用 #458、#459 留在 scratch 的 P01 IDB 輸出。

| 字串 | VA／File Offset | 引用處 | 封存群組 | 作用 |
|---|---|---|---|---|
| `%d.img` | `0x11DFA2C`／`0xDDE62C` | `sub_E55DD0`（push 在 `0xE55E41`） | **4（Surf）**：`sub_66E050(4,…)` | 依 surf id 讀圖；檔案太小時記錄 `Surf file size is too small` |
| `%s.img` | `0x11DFA6C`／`0xDDE66C` | `sub_E55C50`（`0xE55C9E`） | **4（Surf）** | 依 SurfList 的 file 讀圖 |
| `%d.img cann't find` | `0x11DFA18`／`0xDDE618` | `sub_E540D0` | — | surf 缺圖時的錯誤訊息 |
| `%s.tbt` | `0x117D800`／`0xD7C400` | `sub_87A720`（`0x87A9A9`，函式起點 FO `0x479B20`） | **3（Icon）**：`0x87A9F7` 的 `push 3` 之後呼叫 `0x66E050` | 依名稱讀圖示 |
| `%d.tbt` | `0x117D924`／`0xD7C524` | `sub_87B3A0`（`0x87B3E9`） | **3（Icon）**：`sub_66E050(3,…)` | 依編號讀圖示；失敗時記錄 `Icon Resource Error[%d]` |

整個 BIN 只有這兩個含 `.tbt` 的格式字串，兩個都走群組 3。群組的定義見 [`ui-layout.md`](../reference/ui-layout.md) V4 (d)：索引 3＝Icon、4＝Surf。

### 2. 資源比對：同號 `.tbt` 是圖示，與參考客戶端的 surf 不同

工具是 `pakviewer-cli pak list`，用 `docs/reference/rea-scripts/pak/pak_index.ps1 -WithAlt` 執行，清單輸出到 `scratch/pak-index/`。查過的 pak 如下：

- `0-客戶端`：24 個 idx。
  - 頂層：`Sprite.idx`、`Sprite00`–`Sprite15.idx`、`Text.idx`、`Tile.idx`
  - `LiTo\`：`LTicon`、`LTsprite`、`LTsurf`、`LTtext`、`LTtile`
  - `launcher\TW13081901.pak` 沒有 idx，PakViewer 打不開，跟 UI 無關
- 參考客戶端：kor25、t89、ly 三套的 `Tile`、`Text`、`Sprite*.idx`

抽出的檔案只放在 `scratch/t758/`。表中尺寸取自檔頭：`.img` 是 u16 寬、u16 高；`.tbt` 是 x、y、寬、高各 1 byte。

| surf | 本機 0-客戶端 | kor25（ly、t89 相同位置） | 判讀 |
|---|---|---|---|
| 6102（FishingUI 底圖） | `Sprite01/6102.tbt` 2,148 B，32×32；`LTsurf/6102.img` 66,888 B，209×160 | `Sprite04/6102.img` 15,892 B，**209×38**；另有 `Sprite01/6102.tbt` 1,344 B | 參考客戶端的 surf 是 `.img`，`.tbt` 是另一張圖示 |
| 6103（FishingUI 網子鈕） | `Sprite02/6103.tbt` 2,148 B；`LTsurf/6103.img`、`6103c.img` 各 614,408 B | `Sprite05/6103.img` 730 B，19×19 | 同上 |
| 6104 | **只有** `Sprite03/6104.tbt` 1,090 B，23×32 | `Sprite06/6104.img` 730 B，**19×19**；`Sprite03/6104.tbt` 1,338 B | 本機缺 surf |
| 6105 | 只有 `Sprite04/6105.tbt` 1,240 B，27×28 | `Sprite07/6105.img` 730 B，19×19 | 本機缺 surf |
| 6106 | 只有 `Sprite05/6106.tbt` 1,240 B，27×28 | `Sprite08/6106.img` 216 B，13×8 | 本機缺 surf |
| 6108（釣竿量表） | 只有 `Sprite07/6108.tbt` 1,240 B，27×28 | `Sprite10/6108.img` 4,298 B，165×13 | 本機缺 surf |
| 5902／5903（兩窗的關閉鈕） | `Sprite08/5902.tbt`、`Sprite09/5903.tbt`；**`Sprite14/5902c.img`、`Sprite15/5903c.img`** 各 66,968 B | `Sprite11/5902.img`、`Sprite12/5903.img` 各 488 B | 本機有 `c.img`，現行查找順序會讀到 |

本機 6105、6106、6108 三個 `.tbt` 的前 12 byte 完全相同（`02 02 1b 1c 01 0a 06 ce 49 51 4e b1`），長得像同一組圖示，跟 surf 的尺寸也對不上。參考客戶端裡同號的 `.img` 和 `.tbt` 同時存在，可見兩者是不同群組的不同圖。

**更正 PAK§4.1 的「tbt」欄。** 這個任務原本寫「FishingUI 6 個、ReNStatusUIEx 2 個 surf 只有 `.tbt`」，實際是：

- **FishingUI**：真正缺 surf 的是 6104、6105、6106、6108 四個；本機只有同號的圖示 `.tbt`。6102、6103 在 `LTsurf`；5902、5903 有 `c.img`。
- **ReNStatusUIEx**：12 個 surf 是 2455、5366–5369、5375–5377、5479、5902、5903、6295。那 2 個「tbt」就是 5902、5903，本機都有 `c.img`，現行 `SurfCatalog` 先查 `Fc.img` 就讀得到，**沒有缺**。6295 只在 `LTsurf`。
- 原表把這些算成 tbt，推測是因為統計時沒有把 `<n>c.img` 算進去。`pak-resources.md` 不在本任務可改的範圍，交給下一棒修正。

### 3. 程式變更（`engine/source/NativeUi/SurfCatalog.cs`）

- 新增常數 `SurfExtension = ".img"` 與 `IconExtension = ".tbt"`。`Candidates` 改用 `SurfExtension`，查找順序不變：`Fc.img` 在 Sprite{h}、`Fc.img` 在 Sprite、`F.img` 在 Sprite{h}、`F.img` 在 Sprite。
- 新增 `IconOnlyFile(surf)`：用同樣的雜湊順序找同號的 `Fc.tbt`／`F.tbt`，**只用來產生診斷訊息**，`Get`／`Resolve` 照樣回傳 null。
- surf 缺圖、但有同號 `.tbt` 時，日誌多一段：`；只有 Icon 群組的 SpriteNN/F.tbt，原版 surf 不讀 .tbt，不畫`。其他缺圖情況的日誌內容不變。
- 類別說明補上上面第 1 節的 TW16 地址。

### 4. 測試（新檔 `engine/_integration/dev/native-ui-tests/TbtDecoderTests.cs`）

`[CheckGroup("native-ui-tbt")]`，同時掛在 CI 已經在跑的 `native-ui-resources`（Order 50）和預設群組（Order 126）。封存、`SurfList.xml`、`.img`、`.tbt` 全部在 `scratch/issue-758/synthetic/` 下合成，不讀任何客戶端。測試內容：

- `TbtImage`：兩段資料、位移、值為 0 時透明、x／y 位移、轉成 Bitmap 的 alpha；寬為 0、缺列、超出寬度時要丟例外；`.tbt` 的 bytes 交給 `UiImage.Decode` 會失敗（兩種格式不通用）。
- `SurfCatalog`：
  - 4 個 file 的候選檔名都是 `.img`。
  - 只有 `6104.tbt` 時，`Resolve`／`Get` 回傳 null；`IconOnlyFile` 指出所在的 pak；日誌只記一次，第二次缺圖不再記。
  - 5902 讀到 `5902c.img` 而不是 `.tbt`；6102 讀到 `.img` 的 20×4，而不是 32×32 的 `.tbt`。
  - 什麼都沒有的 7000：日誌和改動前逐字相同。
  - 不在 SurfList 的 id：回傳 null。
- Icon 路徑（`BuffIconCatalog`）照樣讀得到同一個 `6104.tbt`。

### 5. 建置與檢查

- `py -B -X utf8 engine/_integration/dev/rebuild.py --build` → **`PASS dev`**。用的是暫時 lock，不寫回；**沒有改 `rebuild-lock.json`**。
- `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release` → **0 錯誤**。3 個警告都在既有的檔案，新檔沒有警告。
- `py -B -X utf8 engine/tools/check-no-game-assets.py` → `{"status": "pass", "trackedFiles": 4102, "errors": 0}`。
- 本機建置的環境問題，與程式改動無關，同 #675／#676：使用者的 NuGet 快取還原時接不上 WebView2 的 targets，會出現 NETSDK1130。處理方式：
  - `scratch/t758/NuGet.Config` 唯讀指向主專案的 `engine/vendor`。
  - `NUGET_PACKAGES` 改指向 `scratch/t758/packages`。
  - 把 `engine/source/obj/` 底下 5 個 NuGet 還原輸出逐一刪掉（單檔刪除，沒有遞迴）。

## 未驗證／待驗證

- **離線測試還沒在本機執行**。依規定不能在本機跑 DemoChecks，要等 PR 的 CI（`native-ui-resources` 那一步）結果。
- 本機 `5902c.img`／`5903c.img` 尺寸是 180×186（66,968 B），參考客戶端的 `5902.img` 只有 488 B，大約 16×15。依 V4 (e) 的查找順序，TW16 會先讀 `c.img`，但這兩張放在 ReNStatusUIEx／FishingUI 的關閉鈕位置是否合理，**待驗證**：要等 P-A 面板做出來，或真機確認。
- TW16 碰到 surf 缺圖時會怎麼處理（跳錯誤後結束程式，FU5§1），本輪沒有重查。引擎維持「缺圖不畫」。

## 產出路徑

- `engine/source/NativeUi/SurfCatalog.cs`（修改）
- `engine/_integration/dev/native-ui-tests/TbtDecoderTests.cs`（新增）
- `docs/reports/issue-758.md`（本報告）
- 只在 scratch、未入 git：`scratch/pak-index/`（PakViewer 清單）、`scratch/t758/`（抽出的樣本、分析腳本、NuGet 設定）

## 給下一棒的提示

- **FishingUI 要原生化（P-A）時，缺的是 4 個 `.img` surf：6104、6105、6106、6108**，要走 P-B 的參考客戶端 surf 備援（`RefClientSurfFallback`）。kor25 的位置：`Sprite06/6104.img`、`Sprite07/6105.img`、`Sprite08/6106.img`、`Sprite10/6108.img`，執行期從原路徑讀，並記錄來源 SHA-256。**不要**改用本機同號的 `.tbt`。
- 6102、6103 本機只有 `LiTo\LTsurf` 的版本，尺寸跟 kor25 不同（6102：209×160 對 209×38）。TW16 讀不讀 `LTsurf`、要不要改讀 kor25 的 `Sprite04/6102.img`、`Sprite05/6103.img`，要在 P-B 一起決定。
- ReNStatusUIEx（P-A `RenewalStatusPanel`）不需要 P-C，只有 6295 在 `LTsurf`。
- `pak-resources.md` §4.1 的「tbt」欄與 P-C 列，請照上面第 2 節更正：FishingUI 是 4 個真缺，ReNStatusUIEx 是 0 個。
- `SurfCatalog.IconOnlyFile` 可以拿來做資源盤點，但不要拿它的結果去畫 surf。
