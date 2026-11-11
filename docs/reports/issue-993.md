# Issue #993 — T435：CU-3 原生讓位收尾（G12–G15）

依據：`docs/reference/classic-ui-audit.md` §3 C 組、§4 CU-3（T413／#953）。分支 `task/993`。

## 做了什麼

| 缺口 | 檔案 | 原生 UI 開著時 | `--classic-ui`（或原生面板故障） |
|---|---|---|---|
| **G12** 座標分享 ⊕ 疊在畫布上 | `LocationShare.cs` | WinForms Label（⊕、提示列）一律隱藏；新面板 `LocationMarkerPanel`（`CU3_LocationShareMarker`，`[UiPanel(171)]`，World 層，不做命中測試）畫在 800×600 畫布上，跟著畫布一起縮放，所以視窗不是 800×600 也對得準。只畫字（青字＋黑影），不畫底圖或色塊。堆疊關窗（Close）＝取消選取 | 照舊用 Label |
| **G13** 公告板確認跳 MessageBox | `BoardUi.cs` | 原生公告板接手時，發文（686）與刪文的確認改成聊天輸入列上的 Y／N 提示：新面板 `BoardConfirmPromptPanel`（`CU3_BoardConfirmPrompt`，`[UiPanel(198)]`，Float 層，畫在 `YesNoButtons.Line`），做法同 R9-H 1813、R9-P 1516。Y＝確定，N／Esc＝取消；伺服器詢問（YesNoPanel）或聊天列提示（`ChatPanel.Prompt`）顯示中時讓位；聊天輸入列開著時不收鍵。答「是」之後再檢查一次按鈕規則，刪文還要求閱讀中的仍是同一篇，才送出一次 | 照舊 MessageBox（問句文字相同，`BoardLineConfirm.Question`） |
| **G14** NPC 商店買賣 0／1 讓位沒做完 | `NpcUi.cs` | `nativeOwnsAll` 改成 `NpcPanelYield.OwnsAll`：商店清單由原生 TRADE_WINDOW 畫著（`ShopTradeWindow()!=null`）時，WinForms NPC 面板整個不顯示。查詢頁需要 WinForms 顯示時（HyperText 不可用）不讓位；空清單（TRADE_WINDOW 不開）照舊留提示文字 | 不變（`ShopNativeWindow=false`） |
| **G15** agsel 同時進 HYPER_TEXT_WINDOW 與 AuctionInfo_Window | `NpcUi.cs` | `AgselPageTracker` 在 `CaptureWorld` 裡找出「這一頁是 agsel 頁」；原生 AuctionInfo_Window 可用、Agitmap_Window 開著同一個看板時，HyperText 不放這頁，這頁在本機關掉（不送封包，同既有「另一頁被取代」的做法），對話不會卡在看不到的頁上 | 不變（HyperText 路徑不進來） |

新的離線測試 `engine/source/NativeYieldTests.cs`（`[WorldModuleTest("native-yield")]`，自動發現）：兩個新面板註冊一次、層級、IUiClosablePanel；G12 標記計算（同地圖、不同地圖、視野外夾邊）、Label 顯示規則、面板 Update／Paint／Close；G13 問句、Proceed 規則、提示面板 Y／N／Esc／重複鍵／修飾鍵、讓位給伺服器詢問、Close；G14 判斷表＋合成 S70（classic、原生有列、空清單）；G15 合成 S39 板頁＋agsel、追蹤器、別的看板、重置。

沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`；新面板都用 `[UiPanel]` 自動發現。

## 怎麼驗證

- 本機只編譯：`dotnet build engine/source/OfflineClient.csproj -c Release -p:EnableWindowsTargeting=true -r win-x64 -p:SelfContained=false --configfile .github/nuget.ci.config`（`DOTNET_CLI_HOME` 在 scratch，並設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`）→ 建置成功，0 錯誤；3 個警告都是既有檔案（`HtmlActionTests.cs`、`MagicUi.cs`）。
- `python -B -X utf8 -I engine/tools/check-no-game-assets.py` → `pass`（4606 個追蹤檔，0 錯誤）。
- 依本機執行限制，沒有在本機跑 `--module-tests`；既有單元測試與 `native-yield` 交給 PR 的 GitHub Actions CI。

## 未驗證什麼（待驗證／推測）

- **真機再確認**（交給 Codex／dot）：
  - G12：原生畫布上的 ⊕ 位置與提示列是否對準（尤其非 800×600 視窗、`nativeUiFill`）；標記每 100 ms 更新一次（同原本 Label），走路時可能落後一格。
  - G13：原版公告板發文／刪文**有沒有確認、用哪個字串都沒有查到**；確認本身是引擎沿用 WinForms 公告板的規則（推測），提示句與「（Y＝確定，N／Esc＝取消）」是引擎自製文字。
  - G15：原版把 agsel 交給 CAgitAuctionUI 的路徑沒追到（`AgitAuctionPanel.cs` 檔頭「推測」）；比照 agapply（`HyperTextOmitsNumber`），使用者關掉 AuctionInfo_Window 後也不改回 HyperText。agsel 頁本機關掉後，原版 HyperText 是否仍保留前一頁（看板首頁）未知。
- 沒有新增或引用任何圖檔，⊕ 是文字，所以沒有「缺資源」的宣稱，也就不需要 PakViewer 查詢清單。

## 產出路徑

- `engine/source/LocationShare.cs`、`engine/source/BoardUi.cs`、`engine/source/NpcUi.cs`
- `engine/source/NativeYieldTests.cs`（新）
- `docs/reports/issue-993.md`（本檔）

## 給下一棒的提示

- CU-3 做完後，classic-ui-audit §3 C 組（G12–G15）全部有對應；`classic-ui-audit.md` 的 §3／§5 狀態可以在下一輪文件整理時標成「已做、待真機」（本張沒有改 audit 文件）。
- 真機 smoke 核對清單：① 原生下點聊天座標連結或座標清單一列 → ⊕ 在畫布上，沒有 WinForms 方塊；`--classic-ui` 仍是 Label。② 原生公告板按發文 686／刪文 → 聊天輸入列出現問句，Y 送出一次、N／Esc 不送；`--classic-ui` 仍跳 MessageBox。③ NPC 商店買／賣 → 只有 TRADE_WINDOW，開發工具 NPC 分頁的面板不出現。④ 盟屋拍賣窗選一間 → 只有 AuctionInfo_Window，沒有同內容的 HyperText 頁；之後 Bid → Bid_Window 正常。
- CU-4（G16–G19）還沒做，會動 `UiClientBridge.cs`、`CharacterManagementUi.cs` 等，和本張沒有重疊。
