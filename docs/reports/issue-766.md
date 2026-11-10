# Issue #766：T328 P-D 原生狀態窗（ReNStatusUIEx.xml，參考客戶端）

依據：[`docs/reference/pak-resources.md`](../reference/pak-resources.md)（PAK，T318／#752）§4.2 的 4:RenewalStatusUI 列。照主控裁定與 #533 的先例：XML 與圖片在**執行期**從參考客戶端（預設 kor25，PAK§3）的原路徑讀取，記錄來源 SHA-256，**不複製進 repo**。P-A（T319／#757）負責讀 XML，P-B（T322／#760）負責補 surf。做法與 #768（OptionPanel）相同。

## 做了什麼

### 1. 新檔 `engine/source/NativeUi/RenewalStatusPanel.cs`

- `[UiPanel(177)]` 自動發現，Id 是 `RenewalStatusUI`，放在 Window 層，實作 `IUiClosablePanel` 和 `IUiMovablePanel`。
- **載入**：註冊在本機一定有的 `uistatus86` 上，`Load` 裡再呼叫 `UiResources.Xml.Load("ReNStatusUIEx")`。查找順序：主客戶端 Tile.pak → 參考客戶端 Tile.pak，兩邊都先找 `-c`，P-A 會記錄 `ui-xml-ref`。如果沒有參考客戶端、讀不到 XML、或 XML 版面不符，就記下 `Unavailable` 並寫進 log。這時視窗不會開，其他原生 UI 照常運作。
- **版面照 XML 建**：
  - 根元素 `RenewalStatusUI` 底下的 `Equip_Window` 和 `Status_Window` 是兩個分頁，位置相同，一次只顯示一頁，開窗時顯示 XML 中的第一頁。
  - `KarmaTooltip_Window` 是提示窗，不建。
  - 元件有 Image、Gauge、Label，以及 Label 底下的 Up／Down 按鈕、Button、CheckButton、RadioButton、`List Horizontal="1"`（由左往右排）。座標相對於父元素。
  - ItemIcon 是裝備欄位，只記下矩形（`Slots`）。
- **顯示的值**（`RenewalStatusView`，來源是 `OwnStatus`）：
  - 等級，HP、MP 用「目前/最大」，六項能力值、善惡值、地水火風四屬性抗性。
  - HP、MP 量條依比例裁切。
  - 不知道的值留白：藥水、魔法等級與魔攻、DG、ER、ME、欄位剩餘時間、PK 次數。
  - XML 裡的設計期 `Caption` 一律不畫。
- **互動**：
  - 分頁鈕切換分頁。
  - 關閉鈕 `CloseWindow` 關窗。
  - 頂端 20 px 可以拖動。
  - 能力點可以分配時才顯示 Up 鈕，點下會呼叫 `StatPointUp(key)`。Down 鈕不顯示。
  - `ShowStatInfoTooltip` 只切換勾選圖，StatInfoUI 沒有做。
  - 其他事件記在 `Unhandled`。
- **缺圖不畫**：缺的圖不畫，也不用替代圖，全部列在 `Missing` 並寫進 log。不把 `.tbt` 當 surf 用（依 #758）。
- Png 共用 `OptionPngSource`（#768）。順序：本機 Sprite → 參考客戶端 Sprite*.idx／.pak，log 記錄 idx 與內容的 SHA-256，前綴是 `option-png`。

### 2. `engine/source/BonusStatsUi.cs`（只加原生分流）

- `SyncBonusStats` 多呼叫一次 `SyncNativeStatus(v)`。原生 UI 開啟、而且狀態窗沒有故障時，才把同一份快照送給原生視窗，並把 Up 鈕接到既有的 `bonusSelect`。這一步只在本機選屬性，不送封包，確認送出仍在 WinForms 面板上。
- `--classic-ui`（原生 UI 關閉）或狀態窗無法使用時，什麼都不改。WinForms 能力點面板一律保留。

### 3. 新檔 `engine/source/NativeUi/RenewalStatusPanelTests.cs`（離線測試）

- 掛 `[WorldModuleTest("renewal-status-panel")]`，由 CI 的 `--module-tests` 自動發現。任務原本寫 `[CheckGroup]`，但 `[CheckGroup]` 只在 `_integration/dev`，`engine/source` 的測試要用 `[WorldModuleTest]`，所以照 #768 的做法。
- XML 和圖片都是在記憶體裡合成的，元素種類仿照 ReNStatusUIEx。
- 測試內容：
  - 註冊
  - 解析：分頁、提示窗不建、List 橫排、Activate=0、相對座標、缺圖清單、不解析 DisableSurf
  - 值的對應：OwnStatus → view，未知值留白，TextResolver
  - 繪製：底圖、欄框、缺圖不畫、量條 25%、Up 鈕顯示條件、文字
  - 輸入：分頁、Up 點擊與按住時中止、勾選、拖動、移動、關窗、離開世界時關窗並清空值、`UiRoot.Toggle`
  - 不可用：兩頁底圖都缺、只缺一頁時借另一頁的大小、未載入

沒有改 `Program.cs`、`DemoChecks.csproj`、各註冊表、`MenuButtonsPanel`、`rebuild-lock.json`。

## 資源盤點（唯讀，PakViewer CLI）

- 用 `pakviewer-cli pak list` 列出下列 idx，清單放在 `scratch/t766/pak/`（不進 git），比對結果在 `scratch/t766/inventory.txt`：
  - `0-客戶端` 24 個 idx：`Sprite`、`Sprite00–15`、`Text`、`Tile`、`LiTo\LTicon`、`LTsprite`、`LTsurf`、`LTtext`、`LTtile`
  - kor25 19 個 idx：`Sprite`、`Sprite00–15`、`Text`、`Tile`
  - 10909 另外查了 t89、ly 的 `Sprite*.idx`
- XML：kor25 的 `ReNStatusUIEx-c.xml` 解密後 SHA-256 是 `6cd348baf079600c4f04203f3074d0c048beb70a48020e1d23722699aecd938b`。loader 先找 `-c`，所以實際讀的是這個檔。`ReNStatusUIEx.xml` 解密後是 `4298f5be…ef8418cc`，與 PAK§4.1 相符。比對用的樣本是 #629 留在 scratch 的抽出檔，內容不進 repo。

| 類別 | 編號 | 本機 0-客戶端 | kor25 | 結果 |
|---|---|---|---|---|
| surf | 2455、5366–5369、5375、5377、5479 | 有（`.img`／`c.img`） | 有 | 本機 |
| surf | 5376 | `Sprite00/5376.img` | 有 | 本機 |
| surf | 5902／5903 | `Sprite14/5902c.img`、`Sprite15/5903c.img` | `Sprite11/5902.img`、`Sprite12/5903.img` | 本機讀 `c.img`（#758）。尺寸是否合理**待驗證** |
| surf | 6295 | 只在 `LiTo\LTsurf` | `Sprite01/6295.img` | 走 P-B 參考客戶端補圖 |
| png | 10898–10908、10910 | **0 個**（24 個 idx 都沒有） | 全有（`Sprite01／03–05／13–15`） | 執行期從 kor25 讀 |
| png | **10909**（`Karma_Button`） | 沒有 | **沒有** | **缺，不畫**。t89 也沒有，只有 ly 的 `Sprite09/10909c.png`（2,387 B） |

**更正**：PAK§4.1 與本任務寫的「2 個 tbt surf」，依 #758 就是 5902／5903。本機有 `c.img`，不需要 P-C。

## 怎麼驗證

- `dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o scratch/t766-build`：**0 個錯誤**。2 個警告都在既有的 `MagicUi.cs`。
  - 本機 restore 遇到 NETSDK1130，是環境問題，同 #675／#676／#768。處理方式：
    - `scratch/t766/NuGet.Config` 指向主專案的 `engine/vendor`。
    - `NUGET_PACKAGES` 改指向 `scratch/t766/packages`。
    - `engine/source/obj` 底下 5 個 NuGet 還原輸出逐一刪除，沒有用遞迴刪除。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`，0 個錯誤。
- 依 2026-11-09 的本機執行限制，**本機沒有執行任何編譯出來的程式**。`renewal-status-panel` 的離線測試交給 PR 的 GitHub Actions CI。DemoChecks 需要 `rebuild.py --build` 的產品 DLL，本機沒有單獨建置它，也交給 CI。
- CI 結果：見 PR。

## 未驗證／待驗證

- **實機畫面（交給 Codex）**：kor25 的 ReNStatusUIEx 版面和 png 放在 TW16 上是否正確；5902c／5903c（180×186）當關閉鈕是否合理。
- **以下規則是推測**，寫在檔頭；`CRenewalStatusUI`（`sub_EDCBE0`）沒有反編譯：
  - 開窗時先顯示第一頁
  - List 橫排時依圖片寬度往右排
  - 缺底圖的分頁借另一頁的大小
  - HP、MP 文字格式「目前/最大」，等級標籤只顯示數字
  - Up 鈕在可以分配能力點時才顯示，Down 鈕不顯示
  - DisableSurf 不用
- **留白的值**：
  - PK 次數：S_OwnCharStatus 最後一個 dword（`OwnStatus.Kills`）是不是 PK 次數，沒有確認，所以不顯示。
  - 藥水、魔法等級與魔攻、DG、ER、ME、欄位剩餘時間：引擎沒有資料。
  - 等級量條：需要經驗表才能算百分比。
- **沒做**：
  - 裝備欄的物品圖示：需要「裝備欄位 → 物品」的資料。
  - 物品 Tooltip、Karma 提示窗、StatInfoUI。
  - Up 鈕之後的確認送出：目前仍走 WinForms。
- **文字**：String 編號（882、883、3233–3235、4163–4166、4227、4228、4500–4504）是參考客戶端字串表的編號，和本機 `string-c.tbl` 不一定對得上（同 #768），所以 `TextResolver` 在產品裡是 null，這些標籤不畫。
- **沒有開窗入口**：選單 2／Ctrl+A 目前仍開 dev-tools 的「角色」頁。`MenuButtonsPanel` 不在本任務可改的範圍，視窗只能用程式呼叫 `UiRoot.Toggle(panel,panel.Open)` 打開。

## 產出路徑

- `engine/source/NativeUi/RenewalStatusPanel.cs`（新）
- `engine/source/NativeUi/RenewalStatusPanelTests.cs`（新）
- `engine/source/BonusStatsUi.cs`（只加原生分流）
- `docs/reports/issue-766.md`（本報告）
- 分支 `task/766`
- 只在 scratch、未進 git：`scratch/t766/`，包含 PakViewer 清單、盤點結果、NuGet 設定

## 給下一棒的提示

1. **開窗入口**：在 `MenuButtonsPanel` 把 action 2（Status）與 `MenuHotkeyCommand.Status`（Ctrl+A）改成：`root.Find(RenewalStatusPanel.PanelId)` 可以用就呼叫 `root.Toggle(panel,panel.Open)`，否則照舊開 dev-tools 的「角色」頁。
2. **確認送出**：決定原版按 `StatPointUP` 時是直接送 C_Attr，還是先暫存、按確認才送，要反編譯 `sub_EDCBE0` 的事件表。決定後，在 `BonusStatsUi.SyncNativeStatus` 把 `bonusSubmit` 接上。
3. **裝備欄**：`Slots` 已經有 21 個欄位名稱和矩形，需要「欄位名稱 → 已裝備物品」的對應（S_EquipmentWindow 或背包的裝備旗標），之後用 `ItemIconSource` 畫圖示。
4. **10909**：kor25 和 t89 都沒有，只有 ly 有，要不要改用 ly 由使用者決定（同 #768 的 10914）。
5. **PK 次數**：確認 S_OwnCharStatus 最後一個 dword 的意義後，在 `RenewalStatusPanel.ViewOf` 填入 `PkCount`。
6. **PAK§4.1** 的「tbt 2」請改成 0（見 #758）。
