# Issue #767 T329：P-D 原生釣魚窗（參考客戶端 FishingUI.xml）

依據：[`docs/reference/pak-resources.md`](../reference/pak-resources.md)（PAK）§4.2 第 4:FishingUI 列、§6 P-D；P-A（#757，`RefClientXmlSource`）、P-B（#760，`RefClientSurfFallback`）、P-C（#758，結論：surf 不讀 `.tbt`）都已在 main。主控裁定沿用 #533：執行期從參考客戶端（預設 kor25）原路徑讀，**不複製進 repo**。

## 做了什麼

- **新檔 `engine/source/NativeUi/FishingPanel.cs`**：原生釣魚窗 `Fishing_Window`。
  - 註冊：`[UiPanel(181)]` 自動發現（179 已由 #766 狀態窗使用），實作 `IUiClosablePanel`、`IUiMovablePanel`。
  - 載入：跟 #768／#769 一樣，註冊用本機一定有的 `uistatus86` 當宿主版面，`Load` 內再經 `UiResources.Xml` 讀 `FishingUI`。主客戶端沒有這份檔，改讀參考客戶端；SHA-256 由 P-A 記在 `ui-xml-ref` log。
    - 讀不到 XML、版面不符或缺視窗底圖時，標 `Unavailable`，不開窗；其他原生 UI 照常運作。不用替代圖。
  - surf：一律走 `SurfCatalog.Get`，先找本機 Sprite 的 `.img`，找不到再走 P-B 的參考客戶端備援。缺的 surf 不畫，列在 `Missing`，寫一行 log。
  - 版面照 XML：
    - 視窗位置取 Window 的 X/Y（參考檔是 0,0），大小取 BaseSurf 圖的大小；`Moveable="20"` 是可拖曳的上緣高度。
    - 畫：底圖 → `SpeedReel_Image` → `FishingNet_Button`、`QuitFishing_Button`（XML 順序）。按住顯示 Push 圖，滑鼠移上去顯示 HighLight 圖（XML 拼法是 `HighLightSurf`，兩種拼法都認）。
    - `FrameSurf` 與 `BaseSurf` 相同（6102），不重畫；不同時列在 `Skipped`。
  - **不畫的元素**（列在 `Skipped`，報告照列）：
    - `BaitTemp_Image`、`BaitCount_Label`：魚餌數量。背包封包不帶模板編號（`docs/protocol/fishing.md`），沒有資料來源，不推算。
    - `ReelGuage_Image`（`AniGauge`，surf 6108）：伺服器不送收竿進度，原版的動畫時序也沒有反組譯。
    - 4 個 `Tooltip`（字串 3674／3677／3678／3679）：本窗沒有接提示。
  - 操作：
    - `QuitFishing` → `FishingWindow.Quit`，走舊式 HUD 收竿鈕同一條 C62 路徑（`ClientForm.CancelFishingHud`）。送出後按鈕維持 Push 圖、不能重送；等伺服器確認（S119／S71 1163）才關窗。
    - `FishingNet`：沒有已核實的封包，點了只記進 `Unhandled`，寫一行 log，不送任何東西。
- **`engine/source/Fishing.cs`（只加開窗事件）**
  - 新增 `FishingWindowEventArgs`、`FishingWindow.Changed` 事件與 `FishingWindow.Raise`。
  - `ObserveFishing` 改成包一層：原本的內容搬到 `ObserveFishingCore`，邏輯不變；收包後如果釣魚狀態的 revision 有變（開始、通知、結束），就發事件。`FishingCancelSent` 結尾也發一次。
  - 處理器丟例外時，只記一行 log，不影響收包，也不影響後面的處理器。
  - 新增 `ClientForm.QuitFishingFromNativeUi()`：在 UI 執行緒找到原生 UI 開著的 `ClientForm`，用 `ReadFishingHud` 取當下狀態，再呼叫既有的 `CancelFishingHud`。不在釣魚中，或已經在等收竿時，回 false、不送。
  - `--classic-ui`：沒有面板，所以沒人訂閱，舊式 `FishingHud` 鈕不變。
- **新檔 `engine/source/NativeUi/FishingPanelTests.cs`**（釣魚窗測試）：`[WorldModuleTest("fishing-ui")]`，資料全部合成（自寫的同結構小版面、純色 surf），不讀任何客戶端。涵蓋：
  - 版面：位置、大小、Moveable、繪圖順序、`HighLightSurf` 拼法、Missing／Skipped 清單、沒有 X/Y 時算 0、FrameSurf。
  - 壞版面：缺底圖、Window 名稱錯、surf 屬性或旗標壞、沒有 Window、產品 Load 沒有資源。
  - 開關窗：開始時開、通知不會重開已關的窗、結束時關、舊 revision 忽略、排隊依序套用、離開世界時關窗並清除。
  - 像素：底圖、SpeedReel、按鈕 Base／Push、缺 HighLight 時退回 Base、AniGauge 不畫。
  - 收竿：在外面放開不算、被拒可重試、送出一次後擋重送、等待時畫 Push、伺服器端 pending、例外不致命；FishingNet 只記錄。
  - 拖曳、移動、IUiClosablePanel。
  - 事件：沒有面板、正常接手、面板不能用、處理器丟例外。
  - 整合：真的 `WorldSession.Receive` S158（自己／別人）、`FishingCancelSent`、S119、S71 1163，檢查事件內容；再把這串事件交給面板重播。
  - 自動發現。

> 任務說「測試用 `[CheckGroup]`」：`engine/source` 裡的測試一律用 `[WorldModuleTest]` 自動發現，跟 #768／#769 一樣；`[CheckGroup]` 只用在 `_integration/dev`。任務寫的測試檔名「釣魚窗Tests.cs」依指示改用英文類名 `FishingPanelTests.cs`。

## 資源核對（PakViewer CLI，唯讀）

- `pakviewer-cli pak search "D:\381懶人包kor25(原)\Tile.idx" fishing` → `FishingUI.xml`（1,182 B）。解出並解密（只放 `scratch/t767/`，**沒有進 git**）：
  - 原檔 SHA-256 `a8f908cb7cb0bdf032638ce039d2d12a7d7a6f9a11acb4157059248928a5132a`
  - 解密 SHA-256 `205b76bb7c185e692aa465cef5b219f83788a4f48bbf795cf349549cda314bb8`
  - 兩者都與 PAK§4.1 相符。
- 用到的 8 個 surf：6102（底、框）、6106（SpeedReel）、6103／6105／6104（網子鈕 Base／Push／HighLight）、5902／5903（收竿鈕）、6108（AniGauge）。
- 主客戶端 `0-客戶端` 全部 **24 個 idx** 都用 `pak list` 查過（清單在 `scratch/t767/local-idx.txt`）：頂層 `Sprite`、`Sprite00`–`Sprite15`、`Text`、`Tile`，`LiTo\` 下的 `LTicon`、`LTsprite`、`LTsurf`、`LTtext`、`LTtile`，以及其他。查的檔名樣式是 `FishingUI`、`610[2-8](c)?.*`、`590[23](c)?.*`。結果：
  - `FishingUI.xml`：24 個 idx 都沒有。
  - 6102–6106、6108：只有同號的 `.tbt`（Sprite01–05、07），屬 Icon 群組，surf 不讀（P-C／#758）。6102、6103 另有 `LiTo\LTsurf` 的 `.img`，但 `SurfCatalog` 不查 LTsurf（TW16 讀不讀 LTsurf，#758 留給 P-B 決定）。`Tile.idx` 的 `61xx.til` 是地圖圖塊，跟 surf 無關。
  - 5902／5903：本機有 `Sprite14/5902c.img`、`Sprite15/5903c.img`，會先讀到。
  - 本機 `SurfList.xml` 這 8 個 id 都有 `file` 屬性。
- kor25 `Sprite*.idx` 都有這 6 個 surf 的 `.img`：`Sprite04/6102.img`（15,892 B）、`Sprite05/6103.img`、`Sprite06/6104.img`、`Sprite07/6105.img`（各 730 B）、`Sprite08/6106.img`（216 B）、`Sprite10/6108.img`（4,298 B）。執行期由 P-B 讀，log 會記 `surf-ref … sha256 …`。
- 結論：在 kor25 可用時，**沒有缺 surf**。沒有參考客戶端時，6102 缺，整窗不開。不畫的元素是因為沒有資料來源（見上），不是缺圖。

## 怎麼驗證

- `dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o scratch/t767-build`：**0 錯誤**。2 個警告在 `MagicUi.cs`，原本就有。
  - restore 用 `scratch/t767/NuGet.Config`，唯讀指向主專案 `engine/vendor`；`NUGET_PACKAGES=scratch/t767/packages`。
- `py -B -X utf8 engine/_integration/dev/rebuild.py --build` → `PASS dev`（暫時 lock，不寫回）。之後 `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore`：**0 錯誤**，既有的 `FishingChecks` 照樣能編譯。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`（4152 檔，0 錯誤）。
- 依 2026-11-09 本機執行限制，**本機沒有執行任何編出來的程式**。`fishing-ui`、既有的 `fishing` CheckGroup 與其他測試，交給 PR 的 GitHub Actions CI。
- `rebuild-lock.json` 沒有改動。

## 未驗證什麼（待驗證）

- 真實 kor25 的 `FishingUI.xml` 經本面板解析後能不能用（Unavailable 是否為 null）：本機不能跑程式，沒有實測。只用靜態方式比對過，元素結構與合成版面相同。
- 5902c／5903c 本機圖是 180×186（66,968 B），參考客戶端的 5902.img 只有 488 B。依查找順序會讀本機的 c.img，放在 209×38 的窗上可能過大（#758 已提出）。要真機確認。
- 以下規則都是**推測**，綁定函式沒有反組譯：
  - 繪圖順序：照 XML。
  - 開窗時機：開始釣魚時開、結束時關。
  - 收竿後等伺服器確認才關窗。
  - 視窗位置照 XML 的 0,0。
- 換地圖時：`Fishing.cs` 是懶清除（下一次讀或收包時才清），換地圖本身不會發事件。如果伺服器在換地圖時沒送 S119／1163，窗會留到下一個釣魚封包或離開世界。待真機確認。
- 原生 UI 開著、但這窗不能用時（沒有參考客戶端），舊式 HUD 本來就隱藏，所以原生模式下沒有收竿入口。這不是本次造成的，只是先記下來。
- 真機：使用釣竿後，原生釣魚窗是否在左上角出現；按收竿後是否送出 C62、伺服器回 S119 後窗是否關閉；`--classic-ui` 時舊式釣魚鈕是否照舊。

## 產出路徑

- `engine/source/NativeUi/FishingPanel.cs`（新）
- `engine/source/NativeUi/FishingPanelTests.cs`（新）
- `engine/source/Fishing.cs`（只加開窗事件與收竿轉接）
- 本報告 `docs/reports/issue-767.md`
- 分支 `task/767`
- 只在 scratch、沒有進 git：`scratch/t767/`（解出的 XML、SurfList、idx 清單、NuGet 設定）

## 給下一棒的提示

- 真機驗收（Codex／dot，P24Lab 測試角色、地圖 5300 釣魚區、釣竿＋至少 2 個魚餌）：
  1. 原生 UI 開著、kor25 在預設路徑：使用釣竿 → 應該開出原生釣魚窗，log 有 `ui-xml-ref FishingUI.xml` 與 6102–6108 的 `surf-ref`。
  2. 按收竿鈕 → 封包紀錄有一次 C62；伺服器回 S119 後窗關閉。
  3. 加 `--classic-ui`，或在 `native-ui.json` 設 `"refClient":false` → 舊式「釣魚狀態」鈕照舊。
- 要接魚餌數量時：需要先能從背包認出魚餌（模板 41295）。wire 上沒有模板 ID，不要用名稱猜。
- 要接 AniGauge 時，先反組譯原版的 AniGauge 綁定，確認進度來源。
- `FishingWindow.Changed` 只是通知，不改 `Fishing.cs` 的既有語意；舊式 `FishingHud` 不受影響。
