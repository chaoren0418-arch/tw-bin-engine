# Issue #1054 — T469 web 圖片：F9 面板的 icons／ground 一律由本機即時產生（b15-web-local-images）

2026-11-13，runner：claude-local，分支 `task/1054`。依 `docs/design/portable-delivery.md`「本機生成與啟動」第 4 點。

## 結論

F9 面板的道具圖示不再限於研究時挑的 20 個：`assets/inventory` 沒有該圖示的檔案時，由本機用 `ItemIconSource` 的 TBT 解碼即時產生 PNG（只放記憶體，不寫檔）。ground 原本就由 `GroundSpriteReader` 即時產生，本次沒有改。`engine/web/` 只有 8 個文字頁面檔，`.gitignore` 的 icons／ground 規則還在。本機編譯通過；測試只在 CI 執行（本機依規定不執行編譯出來的程式）。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/WebIconResource.cs` | `Read(assetsRoot, request, generate = null)`：檔案優先；只有「assets 根／inventory 目錄／該檔不存在」時才呼叫 `generate`。URI 規則（必須逐字等於 `https://p08.example/icons/<0..65535>.png`）與 reparse point 規則（inventory 目錄或檔案是 junction → 403）不變，403 不會被即時產生的圖取代。既有檔案讀不到或不是 PNG 仍回 404。產生器回的內容不是 PNG 也回 404。這個檔案仍然不依賴 GDI+，`LocalResourceChecks`（net10.0）照樣單獨編譯它。 |
| `engine/source/WebIconGenerator.cs`（新） | `Decoded(source, icon)`：向 `ItemIconSource` 取解碼後的點陣圖，讀取或解碼失敗回 null 並記一行 log。`Png(source, icon)`：把點陣圖用 `PngWriter` 編成 PNG；每張點陣圖一份，放在記憶體（`ConditionalWeakTable`，跟著圖示來源的快取存活）。 |
| `engine/source/WebLocalResource.cs` | `Read(stageRoot, request, icons = null)`，把產生器往下傳。 |
| `engine/source/WebPanel.cs` | `ResourceRequested` 傳入產生器：`ItemIconSource.FromRoot(nativeUiRoot)`，和原生視窗（ITEM_WINDOW 等）用同一個來源與同一份解碼快取。 |
| `engine/source/InventoryUi.cs` | 新增 `ItemIconOrGenerated`；F9 狀態裡的 `icon` 網址，以及舊 WinForms 背包清單的圖示，在沒有檔案時改問同一個來源。原本沒有檔案就是 `null`（頁面顯示「缺圖」）。 |
| `engine/_integration/dev/web-resource-tests/GeneratedIconChecks.cs`（新）、`Program.cs` | 合成 TBT 的測試，見下。 |

### 行為細節

- **輸出內容**：解碼後的圖原樣輸出，不套 .tbt 的繪製位移。這和 `inventory` recipe（#1036）對同一張 .tbt 寫出的 PNG 逐 byte 相同，也和現有 20 張歷史 PNG 的版面一致（已用唯讀腳本看過主專案 `engine/assets/inventory` 的 20 張尺寸：10×30 到 32×32 不等，都是 .tbt 原尺寸，沒有補成 32×32）。
- **來源**：`ItemIconSource.FromRoot`，也就是本機 pin 住的 Sprite*.pak，本機沒有時才問參考客戶端（#918 的 5 個圖示）。原生 UI 資源還沒載入時來源是空的，這時沒有檔案的圖示回 404；資源載入後下一次狀態更新就會有網址。
- **不寫檔**：沒有寫進 `engine/runtime/`。任務允許兩種做法，選記憶體快取是因為解碼結果本來就在 `ItemIconSource` 的快取裡，寫檔只多出一份要清理、要驗證的東西。
- **圖示編號**：F9 與舊背包清單沿用原本的做法，直接用 `IconId`（沒有套 `ItemIconSource.IconOf` 的 487／1617 祝福規則）。這是既有行為，本次沒有改。

## 怎麼驗證

### 已驗證（本機）

- `python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/issue-1054/nuget.config --output scratch/issue-1054/build` → `PASS dev`（temporary lock，沒有寫 lock；NuGet 來源指向主專案的 `engine/vendor`）。
- `dotnet build`（只編譯）`web-resource-tests/WebResourceChecks.csproj` 與 `local-resource-tests/LocalResourceChecks.csproj`：0 警告、0 錯誤。
- `git ls-files engine/web`：`bonus-stats.js`、`index.html`、`interaction.js`、`login.js`、`panel.css`、`panel.js`、`private-shop.js`、`trade.js`，共 8 個。`.gitignore` 第 90–91 行 `/engine/web/icons/`、`/engine/web/ground/` 保留。
- PR 沒有 PNG，沒有改 `rebuild-lock.json`、`Program.cs`、`build.yml`。

### 交給 CI 驗證

`build.yml` 既有步驟「Real WebView2 product page and local resource regression」會執行 `WebResourceChecks`（legacy 模式），新增的檢查在裡面：

| 案例 | 檢查內容 |
|---|---|
| 有檔 | `42.png` 有檔案，合成來源也有 42（內容不同）→ 回檔案內容，產生器沒有被呼叫 |
| 無檔即時產生 | `77.png` 沒有檔案 → 200、`image/png`；位元組等於 `PngWriter.Encode(合成 .tbt 解碼結果)`；解回來的像素（含透明）等於 .tbt 解碼結果；第二次回同一份記憶體；`assets/inventory` 沒有多出檔案、沒有 `runtime` 目錄；不給產生器時仍是 404 |
| 不存在的 icon | 來源沒有（999）、來源解碼失敗（500）、id 0、產生器回非 PNG → 404，沒有代換 |
| 路徑穿越 | 38 個網址（`../`、`..%2F`、反斜線、`%2e%2e`、多一層路徑、query／fragment、前導 0、大寫副檔名、其他 host／scheme／port／userinfo 等）→ icons 前綴下 404、其餘 403；產生器呼叫次數 0，旁邊放的 `assets/secret.png`、`web/77.png` 讀不到 |
| junction 規則 | assets 根是 junction → 有檔與即時產生都正常；`inventory` 是 junction → 403，也不以產生的圖代替 |
| assets 根不存在 | 有產生器 → 200；沒有 → 404 |
| 真 WebView2 | 頁面 `fetch('/icons/77.png')` 得 200、`image/png`、位元組相同；`<img>` 解碼後尺寸與 canvas 讀回的 RGBA 等於 .tbt 解碼結果 |

既有的 F9 相關測試（同一步驟的 `--product-page`、原本的 42／65536／999 檢查、`world-ui-tests` 的 `Icons`、`local-resource-tests`）沒有改，呼叫端不傳產生器時行為與原本相同。

CI 結果見 PR；本報告提交時 CI 尚未跑完，結果以 PR 最新 commit 為準。

## 未驗證什麼

- **真機**：沒有開遊戲看 F9 面板。待驗證：登入後背包裡不在那 20 張之內的道具，F9 面板顯示圖示而不是「缺圖」；舊 WinForms 背包清單同樣有圖。
- **真實 Sprite*.pak**：測試全部用合成 .tbt；真實客戶端圖示經 `ItemIconSource` 解碼已由 #706／#1036 的測試涵蓋，本次沒有另外對真實檔案跑。
- **canvas 讀回像素**：新增的 WebView2 檢查假設不透明像素經 Chromium 解碼再由 canvas 讀回時數值不變。若 CI 上出現色彩管理造成的 ±1 誤差，該項要放寬成只比位元組與尺寸。
- **ground**：沒有改，也沒有新增測試。現況：`/ground/<gfx>.png` 只對 `GroundCatalog` 有登錄的 gfx 由 `GroundSpriteReader` 即時產生。

## 產出路徑

- `engine/source/WebIconResource.cs`、`WebIconGenerator.cs`、`WebLocalResource.cs`、`WebPanel.cs`、`InventoryUi.cs`
- `engine/_integration/dev/web-resource-tests/GeneratedIconChecks.cs`、`Program.cs`
- `docs/reports/issue-1054.md`

## 給下一棒的提示

- 現在 `engine/assets/inventory` 對 F9 面板已經不是必要的：整個目錄不存在時圖示照樣出得來。可攜版要不要還讓準備程式產生 `assets/inventory`，可以在 b15 的後續任務決定；兩條路輸出的 PNG 位元組相同。
- `PersonalShopIntegration.cs` 的 `Icon` 與 `WeaponRepairUi.cs` 的後備還是只讀 `assets/inventory` 的檔案（`ItemIcon`）。它們有各自的原生圖示來源，本次沒有動；之後要統一的話改呼叫 `ItemIconOrGenerated` 即可。
- F9 與舊背包清單沒有套 487／1617 的祝福圖示規則（`ItemIconSource.IconOf`），原生 ITEM_WINDOW 有套。兩邊要一致的話，F9 狀態的 `icon` 網址改用 `IconOf(i.IconId, i.Bless)`。
- launcher 目前仍強制要求 `assets/integrity` 與全檔存在（設計文件同一點的後半），不在本任務範圍。
