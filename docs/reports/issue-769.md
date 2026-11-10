# Issue #769 T331：P-D 原生排名窗（參考客戶端 GameRankingUI.xml）

依據：[`docs/reference/pak-resources.md`](../reference/pak-resources.md)（PAK）§4 第 4 列、§6 P-D；P-A（#757，`RefClientXmlSource`）與 P-B（#760，`RefClientSurfFallback`）已在 main。

## 做了什麼

- **新檔 `engine/source/NativeUi/GameRankingPanel.cs`**：原生排名窗 `GameRanking_Window`。
  - 註冊：`[UiPanel(177)]` 自動發現，實作 `IUiClosablePanel`、`IUiMovablePanel`。
  - 載入：`UiRoot.Build` 是全有或全無，所以註冊時用本機一定有的 `uistatus86` 當宿主版面。`Load` 內再自己讀 `GameRankingUI`，經 `UiResources.Shared.Xml`：主客戶端沒有這份檔，改讀參考客戶端（預設 kor25），SHA-256 由 P-A 記在 `ui-xml-ref` log。
    - 讀不到 XML、版面不符或缺視窗底圖時，排名窗標 `Unavailable`，不開窗；其他原生 UI 照常運作。
    - 不用替代圖，也不複製任何檔案。
  - surf：一律走 `SurfCatalog.Get`，先找本機 Sprite，再走 P-B 的參考客戶端備援。缺的 surf 不畫，列在 `Missing` 並寫一行 log。
  - 版面照 XML：
    - 視窗位置取 Window 的 X/Y，大小取 BaseSurf 圖的大小；`Moveable` 是可拖曳的標題列高度。
    - 6 個分頁鈕：切到的分頁顯示 Push 圖，滑鼠移上去顯示 Highlight 圖。Exit 關窗。
    - 6 個分頁 Control 各有自己的 Back_Image 和清單：清單列依 Height 往下排，每格文字依 Align 放在 Width 範圍內。
    - Page_Control：頁碼文字和 4 個翻頁鈕。
  - 資料來源（d287 伺服器沒有 3.81 的排名封包，只回 S39 HTML，見 `docs/protocol/player-query.md`）：

    | 伺服器頁面 | 分頁 | 填哪幾欄 |
    |---|---|---|
    | `y_h_2`（/rank、英雄榜） | Level | 名次、名字 |
    | `y_c_1`（血盟榜） | Pledge | 名次；`盟名(數值)` 拆成 Name／Point（**推測**） |
    | `y_w_1`（財富榜） | Money | 左清單的名次、名字 |
    | `y_k_2`／`y_k_3`（殺人／往生榜） | Kill | 名次；`名字,次數` 拆成 Name／Point |

    - Personal、Weapon 兩頁伺服器沒有資料，只畫背景。
    - Level 頁的 Level／Class 欄，伺服器頁面裡沒有，留白，**不推算**。
    - 名字是空白的列不畫。
- **`engine/source/PlayerQuery.cs`（只加開窗事件）**
  - 新增 `RankingWindowEventArgs`、`PlayerQueryPackets.RankingWindowRequested` 事件、`RequestRankingWindow`。
  - 只有在「回給自己的排名頁」而且 `Ready` 時才發事件。有面板接手（`Handled`）就不再開查詢頁；沒人接手時照舊開查詢頁。沒人接手的情況有三種：`--classic-ui`、參考客戶端缺、版面不能用。
  - 處理器丟例外時，記一行 log，照舊開查詢頁，不會中斷收包。
  - 回給 NPC 的排名頁照舊交給 NPC 對話，不受影響。
  - 事件可能從網路執行緒發出：面板只在鎖內排隊，等 UI 執行緒的 `Update` 才開窗。
- **新檔 `engine/source/NativeUi/GameRankingPanelTests.cs`**：`[WorldModuleTest("game-ranking-ui")]`，資料全部合成（自寫的同結構小版面、純色 surf），不讀任何客戶端。涵蓋：
  - 5 種頁面的解碼，以及參數數量錯誤、非排名頁。
  - 版面載入：位置、列堆疊、Missing／Skipped 清單。
  - 壞版面：缺底圖、Window 名稱錯、surf 屬性壞、列沒有 Height、沒有分頁。
  - 開窗與分頁切換、文字靠左／置中／靠右、離開世界時關窗並清資料。
  - 像素層級的繪圖順序：底圖、分頁背景、Push／Highlight 圖、缺 Push 圖時退回 Base 圖、翻頁鈕在分頁背景之上。
  - 拖曳、Exit、IUiClosablePanel。
  - 事件：沒有面板、正常接手、頁面格式錯、面板不能用、處理器丟例外。
  - 自動發現。

> 任務說「測試用 `[CheckGroup]`」：`engine/source` 裡的測試一律用 `[WorldModuleTest]` 自動發現，跟 #757／#768 一樣；`[CheckGroup]` 只用在 `_integration/dev`。

## 資源核對（PakViewer CLI，唯讀）

- `pakviewer-cli pak search "D:\381懶人包kor25(原)\Tile.idx" ranking` 找到 `GameRankingUI.xml`（36,858 B）與 `SihonRanking.xml`。
- 解出並解密 `GameRankingUI.xml`，解密後的 SHA-256 是 `a774c2a6989b859a38286bf5f0bc1570753b95f11919010a0f3ada7f7a6d610e`，與 PAK§4 表相符。
  - 檔案只放在 worktree 的 `scratch/t769/`，**沒有進 git**。
- 主客戶端 `0-客戶端` 的所有 pak 都沒有這份 XML：PAK§4 已用 PakViewer 查過 24 個 idx，本次沒有重複宣稱缺資源。
- XML 用到的 39 個 surf：`5558`（底）、`5567`（標題）、`4645/4646`（Exit）、`5579–5596`（6 分頁×3 態）、`5573–5578`（6 頁背景）、`5597–5599`（名次變動箭頭）、`5559–5566`（翻頁鈕）。PAK§4 記錄本機 Sprite 全部都有。
  - **不畫的**：`5597–5599`（Diff_Image）。原版是在執行期依名次變動填入，伺服器沒有這份資料，所以不畫。這是設計上的選擇，不是缺圖。
  - 另外不畫的：`Update_Label`（「Last Update」只是設計時的佔位字），以及 Personal 頁的 7 個數值欄（沒有資料來源）。

## 怎麼驗證

- `dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o scratch/t769-build`：**0 錯誤**。2 個警告在 `MagicUi.cs`，原本就有。
  - restore 用 `scratch/t769/NuGet.Config`，唯讀指向主專案 `engine/vendor`；`NUGET_PACKAGES=scratch/t769/packages`。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`（4146 檔，0 錯誤）。
- 依 2026-11-09 本機執行限制，**本機沒有執行任何編出來的程式**。`game-ranking-ui` 與既有測試交給 PR 的 GitHub Actions CI。
- `rebuild-lock.json` 沒有改動。

## 未驗證什麼（待驗證）

- `game-ranking-ui` 與既有單元測試的執行結果：等 CI。
- 真實 kor25 的 `GameRankingUI.xml` 經本面板解析後能不能用（Unavailable 是否為 null）：本機不能跑程式，沒有實測。只用靜態方式比對過，元素結構與合成版面相同。
- 以下規則都是**推測**，綁定函式沒有反組譯：
  - 繪圖順序：同一層先畫 Image。
  - 清單列依 Height 堆疊。
  - 文字顏色白色。
  - 開窗時停在該資料的分頁。
- 參考客戶端版面是否與 TW16 原版相同：**推測**（PAK§7）。
- 真機：`/rank` 後原生排名窗是否開在 Level 頁、名字位置與字型；風雲榜 NPC 頁（回給 NPC）仍走 NPC 對話。
- 沒有接上的部分（待做）：主選單的 `Ranking_Button` 開窗入口（`lineage12.xml`）；翻頁鈕；Personal／Weapon 頁的資料來源。

## 產出路徑

- `engine/source/NativeUi/GameRankingPanel.cs`（新）
- `engine/source/NativeUi/GameRankingPanelTests.cs`（新）
- `engine/source/PlayerQuery.cs`（只加開窗事件）
- 本報告 `docs/reports/issue-769.md`
- 分支 `task/769`

## 給下一棒的提示

- 真機驗收（Codex／dot，P24Lab 測試角色）：
  1. 原生 UI 開著、kor25 在預設路徑時，輸入 `/rank`，應該開出原生排名窗的 Level 頁，log 會有 `ui-xml-ref GameRankingUI.xml`。
  2. 加 `--classic-ui`，或在 `native-ui.json` 設 `"refClient":false`，應該照舊開查詢頁。
- 要接主選單的排名鈕時，呼叫 `GameRankingPanel.Open()`／`Toggle()` 即可（沒有資料時停在 Level 頁、內容空白）。
  - 不要改 `PlayerQuery.cs` 的事件語意；`Handled` 只在面板可用時才設定。
- 若之後確認 `y_c_1` 的數值意義，或 `y_h_2` 的列格式含等級／職業，只要改 `GameRankingData.FromServer`。
