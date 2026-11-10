# Issue #768 T330：P-D 原生選項窗（參考客戶端 OptionUI.xml）

依據：[`docs/reference/pak-resources.md`](../reference/pak-resources.md)（PAK，T318／#752）§4.2 第 4:OptionUI 列。主控裁定沿用 #533 的先例：執行期從參考客戶端（預設 kor25）的原路徑讀檔，**不複製進 repo**。P-A（T319／#757）提供 XML，P-B（T322／#760）提供 surf。

## 做了什麼

### 1. 新檔 `engine/source/NativeUi/OptionPanel.cs`

**`OptionPanel`**
- 用 `[UiPanel(176)]` 自動發現，Id 是 `OptionUI`，圖層是 Window。實作 `IUiClosablePanel` 和 `IUiMovablePanel`。
- **載入方式**：`UiRoot.Build` 是全有或全無（#757 的提示）。所以註冊時用的是宿主版面 `uistatus86`（本機一定有，先例是 `LocationListPanel`），`Load` 裡再自己讀 `UiResources.Xml.Load("OptionUI")`。
  - 讀檔順序：主客戶端 Tile.pak → 參考客戶端 Tile.pak。來源 SHA-256 由 P-A 記成 `ui-xml-ref`。
  - 讀不到 XML、或 XML 版面不符時，只把 `Unavailable` 記下來寫一行 log。這一窗永遠不開，**其他原生 UI 照常啟動**。
- **版面照 XML**：
  - 視窗位置取 `Window` 的 `X`／`Y`。
  - 視窗大小取 BasePng 圖（kor25 是 550×316）。這張圖缺時，改用最大的頁面 `Control` 宣告的 `Width`／`Height`。兩個都沒有就不開。
  - 頁面是頂層的 6 個 `Control`：Home、Application、UI、Game、Etc、Shortcut。
  - 元件依 XML 順序畫：`Image`、`BoardImage`、`Label`，以及 `Button`、`CheckButton`、`RadioButton`。巢狀的 `Control`、`ScrollBar`、`ComboBox` 會把座標累加上去。
- **操作**：
  - 開窗時顯示 Home 頁。
  - 點分頁鈕 `<頁名>Btn` 切到該頁。目前頁的分頁鈕畫 PushPng。
  - 按住按鈕時畫 Push 圖。
  - 點關閉鈕（`MouseEvent="CloseBtn"`，surf 6125）關窗。
  - 拖曳範圍是上方 `Moveable="30"` px。
  - 離開世界時自動關窗。
  - `UiRoot.Toggle` 再開一次會關窗；4 層堆疊滿時，被擠出就關窗。
- **沒接的操作**：勾選框、單選、捲軸、下拉、Help、Init、改倉庫密碼。點了只記進 `Unhandled`，寫一行 log，不改任何設定值。

**`OptionPngSource`**
- 這份 XML 主要用 `Png` 類屬性：`BasePng`、`PushPng`、`Png`、`FramePng`，值的格式是 `id;flag`。
- 找檔順序：先 `<id>c.png`，再 `<id>.png`；每個名稱先找 `Sprite{h}`，再找 `Sprite`。
- 先查本機已鎖定的 Sprite 封存，找不到才查參考客戶端的 `Sprite*.idx`／`.pak`。
- 參考客戶端只用唯讀方式開檔，不寫任何檔案。idx 和 payload 的 SHA-256 寫進 log，格式是 `option-png <id> -> ref <pak>:<name> sha256 … (idx …)`。
- PNG 在記憶體中解成 32bpp PArgb。不是 PNG、解不開、或尺寸超過 4096 時丟 `UiResourceException`，該圖不畫。

**缺圖不畫**：沒有替代圖，缺的圖都列在 `Missing`，啟動時寫一行 log。

### 2. 新檔 `engine/source/NativeUi/OptionPanelTests.cs`（選項窗測試）

`[WorldModuleTest("option-panel")]`，CI 的 `--module-tests` 會自動發現。

- 全部用合成資料：自己寫的小型 XML，元件種類照 OptionUI 但不是原檔；合成的 PNG、surf、idx、pak。
- 涵蓋的內容：
  - 註冊（宿主版面）
  - `id;flag` 解析與錯誤格式
  - png 候選順序（與 kor25 實際落點相同：`10814.png`→Sprite01、`10816c.png`→Sprite06）
  - 頁面與元件、巢狀座標、沒有 Y 時算 0
  - 缺圖清單、略過清單
  - 逐像素檢查畫面：底圖、框、分頁鈕 Push 圖、缺圖不畫、BoardImage
  - 文字解析器的呼叫
  - 點分頁、關閉鈕（在外面放開不算）、拖曳範圍、移動、離開世界、`UiRoot.Toggle`
  - 不可用的三種情況，以及底圖缺時退回宣告大小
  - PNG 解碼（預乘透明、非 PNG、截斷）
- 產品路徑：合成 `UiResources`，主客戶端沒有 OptionUI，參考客戶端的 Tile／Sprite 寫在測試輸出目錄。驗證以下幾點：
  - 沒有參考客戶端時不可用
  - XML 由參考客戶端提供
  - 底圖取自參考客戶端的 `c.png`
  - 同名圖本機優先
  - log 有 payload 與 idx 的 SHA-256
  - 參考客戶端的檔案沒有被改動
  - 暫存檔只刪測試自己建立的，不做遞迴刪除

> 任務寫「測試用 `[CheckGroup]`」，但 `[CheckGroup]` 只用在 `_integration/dev`（DemoChecks）。`engine/source` 內一律用 `[WorldModuleTest]` 自動發現，比照 #757，不改任何註冊表。

沒有改：`Program.cs`、`DemoChecks.csproj`、各註冊表、`MenuButtonsPanel`、HTML `option*-c.html`、`rebuild-lock.json`、`.github/workflows`。

## 資源盤點（只讀靜態查證）

- **查了哪些 pak**：用 PakViewer CLI `pak list` 列出所有 idx，清單放在 `scratch/t768/pak/`，未入 git。
  - `0-客戶端` 24 個：`Sprite`、`Sprite00–15`、`Text`、`Tile`、`LiTo\LTicon`、`LTsprite`、`LTsurf`、`LTtext`、`LTtile`
  - kor25 19 個：`Sprite`、`Sprite00–15`、`Text`、`Tile`
  - 10914 另外查了 t89、ly 的 `Sprite*.idx`
- **比對方式**：用 `scratch/t768/inventory.py`（`py -3 -I`）比對 XML 裡所有 `…Png`／`…Surf` 編號，結果在 `scratch/t768/inventory.txt`。
- **XML**：kor25 `Tile.pak:OptionUI.xml`，解密後 SHA-256 `fe32d2fd9d7bff00c80443b9ea9158bb9bf42e3312ff4d8fc3a670cb5b8c846d`（與 PAK§4.1 相同）。內容只在 `scratch/`。

| 類別 | 數量 | 本機 0-客戶端 | kor25 | 結論 |
|---|---:|---|---|---|
| Png（10814–10913、10854） | 43 | **0 個**（24 個 idx 都沒有） | 43 個都在 `Sprite00–15`，名稱依雜湊落點 | 執行期從 kor25 讀 |
| Png 10914（Home 頁上方圖） | 1 | 沒有 | **沒有** | **缺，不畫**。t89 也沒有；只有 ly 有 `Sprite05/10914c.png`（12,217 B） |
| Surf 2040–2045（捷徑頁捲軸） | 6 | `Sprite01–09` 有 `.img`／`c.img` | 有 | 本機 |
| Surf 6125／6126（關閉鈕） | 2 | 只有 `LiTo\LTsurf`（引擎的 surf 路徑不讀它） | `Sprite09/6125.img`、`Sprite10/6126.img` 各 488 B | 走 P-B 參考 surf 備援 |

**更正 PAK§4.1 的 OptionUI 列。** 原表寫「surf 8 個、本機全有」，實際是：

- surf 有 2 個要靠參考客戶端補，就是 6125／6126。
- 視窗主要的圖其實是 44 個 **png**。PAK 只數了 `Surf…` 屬性，所以沒算到 png。這 44 個裡，本機一個都沒有，kor25 有 43 個，10914 缺。
- `pak-resources.md` 不在本任務可改的範圍，交給下一棒。

## 怎麼驗證

- 建置：`dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o scratch/t768-build` 成功，**0 錯誤**。2 個警告都在 `MagicUi.cs`，是原本就有的。
  - restore 照 #675／#676 的本機環境繞法：用 `scratch/t768/NuGet.Config` 指向主專案的 `engine/vendor`，`NUGET_PACKAGES` 指到 `scratch/t768/packages`。
  - 另外逐檔刪掉 `engine/source/obj` 下 5 個 NuGet 還原輸出，沒有遞迴刪除。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`，0 錯誤。
- 依 2026-11-09 本機執行限制，**本機沒有執行任何編出來的程式**。`option-panel` 與既有單元測試交給 PR 的 GitHub Actions CI。
- 沒有改 `rebuild-lock.json`。
- **CI（PR #788）全綠**：`dotnet`、`map-continuity`（含 `--module-tests`）、`npc-offline` 都通過。第一輪 `option-panel` 的 82 項檢查都過，但測試本身用空的 Sprite 清單建 `SurfCatalog` 而丟例外；修正測試後通過。

## 未驗證什麼（待驗證）

- **實機畫面（交給 Codex）**：kor25 的 OptionUI 版面與 png 放到 TW16 是否正確。依據只有「元件名稱與 TW16 綁定函式 `sub_919F30`／`sub_919170` 相符」（ui-layout.md），這點是**推測**。
- **推測項目**（綁定函式本輪沒有反編譯），寫在 `OptionPanel.cs` 檔頭：
  - 座標相對父元素，沒寫 X／Y 時算 0
  - FramePng 畫在原點
  - 開窗先顯示 Home 頁
  - 分頁鈕用 Push 圖表示目前頁
  - `Moveable` 是可拖曳的高度
  - ComboBox 預設收合
  - `id;flag` 的 flag 意義不明，目前不使用
  - png 不套色鍵；kor25 的圖多是 RGB 不透明
  - png 也走 `c` 優先＋雜湊的順序（kor25 實際落點全部相符）
- **文字不畫**：Label 用的是 kor25 字串表的 `Desc`（18792–21391）和 `String`（613、893–896、1994–1997、4042–4045、4408）。本機 `string-c.tbl` 只有 3484 筆，編號也對不上；硬套會顯示不相干的字（同 PAK§5 的 4118）。所以產品的 `TextResolver` 是 null，文字一律不畫。
- **設定值沒接**：勾選框、音量捲軸、視窗模式等都還沒接到遊戲設定（`PlayerAudioSettings` 等），點了不會改任何東西。
- **沒有開窗入口**：原版 OptionUI 是本機觸發，觸發點還沒追（WIN2§6）。選單鈕 6 和 Ctrl+V 照現狀開 HTML `option1` 頁，本任務也不能改 `MenuButtonsPanel`。所以**目前產品裡不會出現這一窗**，只能由程式呼叫 `Open()`／`UiRoot.Toggle(panel,panel.Open)` 開啟。
- 捷徑頁的清單列（`Caption="name0"`）是執行期才填的內容，原版的資料來源沒查，所以沒有建也不畫。

## 產出路徑

- `engine/source/NativeUi/OptionPanel.cs`（新）
- `engine/source/NativeUi/OptionPanelTests.cs`（新）
- `docs/reports/issue-768.md`（本報告）
- 分支 `task/768`
- 只在 scratch、沒有進 git：`scratch/t768/`（PakViewer 清單、盤點腳本與結果、NuGet 設定）

## 給下一棒的提示

1. **開窗入口**：用 IDA 追 `dword_13D43E4`（OptionUI）的建立與開啟呼叫點（WIN2§6「本機觸發」）。追到之後在 `MenuButtonsPanel` 或熱鍵加上 `root.Toggle(optionPanel,optionPanel.Open)`。HTML `option1`／`option2` 頁保持不動。
2. **設定值**：照 ui-layout.md 的 `sub_919F30` 綁定與 `sub_9184B0` 寫回流程，把 Etc 頁 4 個 HPMP 勾選框、Application 頁音量接到既有設定。`Unhandled` 裡記了已經點過的 MouseEvent 名稱。
3. **文字**：要決定用哪一份字串表。可以在執行期讀 kor25 的 `Text.pak` `desc`／`string` 表（比照本類別，記錄 SHA-256），或另找 TW16 對應的編號。決定後設定 `OptionPanel.TextResolver` 即可。
4. **10914**：kor25 沒有。如果要補，只有 ly 有 `Sprite05/10914c.png`，要先做資源來源決策（windows-2 §8）。
5. **PAK§4.1** 的 OptionUI 列請照上面的「資源盤點」更正：要算 png，6125／6126 不是本機有。
