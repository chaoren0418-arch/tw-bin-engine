# Issue #925 — T408：R10-U 登入畫面 Back_Image 動畫（FU13 D11–D12）

依據：`docs/reference/followups-13.md` §5（M40）、§5.3 D11–D12。

## 做了什麼

- `engine/source/NativeUi/LoginPanel.cs`
  - **D11**：`Login_Window` 裡沒有 `Surf` 的 `Back_Image` 記成 `BackImage`（視窗內 X/Y）。它仍不是一般元件（`Skipped` 照舊列為「執行期才畫」），改由背景動畫畫出：
    - 登入窗從隱藏變成顯示時記下 `BackStart = Clock()`（對應 `sub_EEAC50` 進場景 4 時把時間、幀號歸 0）。
    - 目前圖號 `BackPngAt(經過毫秒) = 1825 + ((經過 ÷ 200 + 1) mod 18)`，順序 1826、1827…1842、1825，18 張一輪 3.6 秒。
    - 圖用 `OptionPngSource`（同 OptionPanel／CreateCharPanel：本機 `Sprite*.pak` 先找，再找參考客戶端，唯讀，SHA-256 記在 `option-png` 日誌），**畫到才讀**，每張讀一次後快取；不複製任何資源。
    - 畫在 `Login_Window` 原點 + Back_Image X/Y、所有元件之下；訊息窗／公告窗照舊畫在上面。有畫背景時 `Bounds` 併入背景範圍（800×600 → 整個畫布），否則 `Bounds` 不變。
    - 讀不到（或解碼失敗）的那一格不畫、不補替代圖，`Missing` 加一筆 `png <id> (<path>/@Png，FU13§5 只在參考客戶端)`，只記一次；其他格照常畫。沒有 png 來源時完全不畫、也不算缺圖。
  - **D12**：檔頭 `:43` 舊註解「Back_Image (no Surf) is still not drawn (Q7-14)」刪除，改為 T408 段落（Png 1825–1842 動畫，FU13§5，只在參考客戶端）；`:22` 同步更正。
- 新檔 `engine/source/NativeUi/LoginBackImageTests.cs`（`[WorldModuleTest("login-back-image")]`，自動發現）：只用合成版面、合成單色圖與可控時鐘。涵蓋：圖號順序與一輪 18 張、長時間回繞；Back_Image 記錄與 `Skipped`／Message_Window 的 Back_Image 不受影響；沒有 png 來源時不畫且 `Bounds` 不變；動畫逐格換圖、元件與訊息窗在背景之上、每張只讀一次；背景位置＝視窗原點＋X/Y、`Bounds` 聯集；缺一格只缺那格並列一次 `Missing`、全部缺時登入窗仍可用、解碼例外不畫；隱藏後再顯示從 1826 重新開始；沒有 Back_Image 或 Back_Image 有 Surf 時不啟動動畫。

沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`；`--classic-ui` 行為不變（LoginPanel 原本的入口邏輯沒動）。

## 怎麼驗證

- 已驗證：`py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build`（`--configfile` 指向 scratch 內引用原專案 `engine/vendor` 的離線 NuGet 設定，因為 worktree 沒有 vendor；暫時 lock，未改已提交的 lock）→ product／launcher 建置 PASS，新增檔案沒有編譯警告。
- 已驗證：`engine/tools/check-no-game-assets.py` → pass（4472 tracked files，0 errors）。
- 依本機限制**沒有在本機執行任何測試程式**；`login-back-image` 與既有 `login-ui`／login 系列測試交給 PR 的 GitHub Actions CI。
- 資源：本任務沒有宣稱新缺資源。1825–1842.png 的位置沿用 FU13§5.2 的 PakViewer 查核（本機 0-客戶端 24 個 idx 皆無 png；kor25／ly 18 張都有，idx 與 SHA-256 見該表；清單在原專案 `scratch/t898/pak-index/`）。

## 未驗證什麼（待驗證，交給 Codex／真機）

- 真機：登入畫面背景是否為這組黃昏城鎮動畫、換圖速度約 200 ms/張（FU13 M40 的對照項）。
- 推測、真機再確認：原版第一次換圖前（≤ 200 ms）沒有圖號、不畫；這裡一顯示就畫 1826。原版是「距上次換圖 > 200 ms」才換，這裡用顯示起算的固定 200 ms 格線（累積誤差上原版會略慢）。
- 實際執行時參考客戶端路徑的 png 讀取與 SHA-256 日誌（`option-png 18xx -> ref …`）只能在有參考客戶端的機器上看。
- 18 張 800×600 解碼後約 34 MB 常駐記憶體（每張讀一次快取，與 CreateCharPanel 相同做法）。

## 產出路徑

- `engine/source/NativeUi/LoginPanel.cs`
- `engine/source/NativeUi/LoginBackImageTests.cs`
- `docs/reports/issue-925.md`

## 給下一棒的提示

- 背景的 png 來源在 `LoginPanel.Load(UiLayout,SurfCatalog)` 末段設定（`PngLookup`），測試直接注入 `PngLookup`／`Clock`。
- 若之後要讓原版「前 200 ms 不畫」或「> 200 ms 相對上次換圖」完全一致，只需改 `BackPngAt`／`BackPng`，測試的 `Sequence`／`Animation` 要一起調。
- 建角畫面的同類 18 張循環在 `CreateCharPanel`（R10-N），兩邊可在將來合併成共用的 png 動畫小工具（本任務範圍限制，未做）。
