# Issue #860 — T376：R9-P 釣魚窗

依據：`docs/reference/followups-9.md`（FU9）§6.1／§6.2／§14.3／§16.2 R9-P、`gap-matrix.md` §15.11；主控裁定（#818）：釣魚物品表沒查到前不開 FishObtainUI 清單。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/NativeUi/FishObtainPanel.cs`（新） | 原生 FishObtainUI 窗（`[UiPanel(196)]`，實作 `IUiClosablePanel`／`IUiMovablePanel`）。執行期經 `UiResources.Shared.Xml` 讀 `FishObtainUI.xml`（本機沒有 → 參考客戶端 Tile.pak），不複製進 repo。畫底 5194、框 5195、關閉鈕 4429／4430（`CloseFishWindow`）、標題 `"[ %s ]"`＋字串 3674；Esc 關窗（`FishKeyboard`）；Moveable 20 可拖曳。**清單列與捲軸不畫**（只保留 `ListArea` 位置，`Skipped` 寫「等物品表」）。 |
| `engine/source/NativeUi/FishingPanel.cs` | ① `FishingNet` 不送封包，改成開關 FishObtainUI（`sub_E65B00`）；FishObtainUI 不在或不可用時照舊只記錄。② `QuitFishing` 先問 **1516**「你要取消自動釣魚嗎？」（`sub_E659A0`）：按鈕只產生 `PendingQuit`，`AnswerQuit(revision, yes)` 回答；是 → 原本的 C62 路徑。釣魚結束、已送 C62、關窗、離開世界時詢問失效。③ 新 `FishingQuitPromptPanel`（`[UiPanel(197)]`，Float）把 1516 畫在聊天輸入列 `YesNoButtons.Line`，Y＝是、N／Esc＝否（同 R9-H 好友刪除的做法）；伺服器詢問或好友刪除詢問顯示中時讓位；聊天列開著時不收鍵。④ 檔頭 24–26 行與 `:147` 的 AniGauge／魚餌數量註解改成已驗證（FU9 §14.3）。 |
| `engine/source/NativeUi/UserObtainPanel.cs` | `:12` 註解更正：`ItemsListClick` 原版在這個類別沒有處理函式（整個 BIN 只在 ShopUI 引用），列的 CheckButton 沒有回呼 → 已驗證「不做選取」；註明 FishObtainUI 是同一建構子的第二個實例。程式碼未改。 |
| `engine/source/NativeUi/FishingFollowupTests.cs`（新） | `[WorldModuleTest("fishing-followup")]`：FishObtainUI 版面、缺圖、錯誤版面、FishingNet 開關／位置、清單與捲軸不畫（像素）、關閉鈕／Esc／結束釣魚／離開世界關窗、1516 詢問與回答（含過期 revision、重複按、各種失效）、提示面板按鍵、自動發現註冊。只用合成版面與合成 surf。 |
| `engine/source/NativeUi/FishingPanelTests.cs` | **範圍外但必要的小改**：既有測試假設按 QuitFishing 立刻送 C62；行為改成先問 1516 後，在兩處加上 `AnswerQuit(...,true)`，其餘斷言不變。 |

沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。`--classic-ui` 時沒有原生面板，舊式 FishingHud 鈕不變（WinForms 入口保留；1516 確認只加在原生窗）。

## 已驗證／推測／待驗證

- **已驗證（靜態，FU9）**：FishingNet 只開關 FishObtainUI；QuitFishing 先問 1516；FishObtainUI 事件 CloseFishWindow／FishItemListScroll／FishKeyboard(Esc)；`ItemsListClick` 沒有回呼。
- **已驗證（資源，本輪 PakViewer CLI 唯讀）**：
  - `FishObtainUI.xml`：`D:\lineage開發\0-客戶端` 全部 24 個 idx（`Sprite.idx`、`Sprite00`–`Sprite15.idx`、`Text.idx`、`Tile.idx`、`LiTo\LTicon`、`LTsprite`、`LTsurf`、`LTtext`、`LTtile`）`pak list` 都沒有（大小寫皆查）；kor25 `Tile.idx` #4398 有（6,370 B），解密後 SHA-256 `01d94c4464e3d798…`，與 FU9 §6.2 相同。版面與本機 `UserObtainUI.xml`（Tile.idx #5443）相同。
  - surf：5194（Sprite01、Sprite14）、5195（Sprite02、Sprite15）、4429（Sprite14）、4430（Sprite06）本機都有。
  - 字串（`docs/reference/rea-scripts/t267/text_tables.py` 讀本機 `string-c.tbl`，3484 筆，SHA-256 `4ebbc1a7…`）：1516＝「你要取消自動釣魚嗎？」、1163＝「釣魚已經結束了。」；**3674 不在本機表** → 標題不畫（表裡有才畫，不用替代字）。
  - 解出檔只在 `scratch/t860/`，未進 git。
- **推測（真機再確認）**：FishObtainUI 開在釣魚窗右側（放不下放左側）；結束釣魚時關窗（原版確定的是「清空清單」）；1516 用聊天輸入列提示與 Y／N／Esc 回答（原版 Y/N 窗樣式未查）。
- **等物品表（#818 裁定）**：清單內容。原版 `sub_E65050` 用背包物品的 16 位元欄位（`sub_C021B0`）比對表 `unk_1542864`；表內容與欄位意義沒查到，3.81 背包封包也不帶模板編號。所以不列背包全部、不用假清單，清單區與捲軸不顯示。
- **不做**：`AniGauge`（3.81 沒有時長／狀態封包，K1）。

## 怎麼驗證

- 編譯：`dotnet msbuild engine/source/OfflineClient.csproj -t:ResolveReferences;CoreCompile -p:Configuration=Release -p:EnableWindowsTargeting=true -p:RuntimeIdentifier=win-x64 -p:SelfContained=false` → 本次觸及的檔案 0 錯誤；唯一錯誤是 `WebPanel.cs` 的 WebView2（本機 NETSDK1130，與 #848／#854 同一環境問題）。完整建置與測試以 CI 為準。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`（errors 0）。
- 依本機限制，**沒有在本機執行任何測試**；`fishing-ui`、`fishing-followup` 與既有單元測試由 GitHub Actions CI 跑（結果見 PR）。

## 未驗證

- CI 結果（push 後由 PR 檢查）。
- 真機：FishingNet 開窗位置與外觀、1516 提示與回答、收竿後 1163、換地圖／結束釣魚時窗是否殘留（gap-matrix M21）。

## 產出路徑

- `engine/source/NativeUi/FishObtainPanel.cs`、`FishingPanel.cs`、`UserObtainPanel.cs`、`FishingFollowupTests.cs`、`FishingPanelTests.cs`
- 本報告 `docs/reports/issue-860.md`；分支 `task/860`

## 給下一棒的提示

- 物品表查到後：在 `FishObtainPanel` 加清單（可直接借用 `UserObtainPanel` 的列幾何與捲軸寫法，XML 結構相同），資料來源要能拿到模板編號；拿不到就維持不顯示。
- 字串 3674 只在參考客戶端的表裡（kor25 是否為 `dummy` 未查）；本機沒有時標題空白是刻意的。
- 真機 smoke（Codex／dot）：開始釣魚 → 按 FishingNet 開關 FishObtainUI → 按收竿看輸入列 1516 → N 不收竿、Y 收竿並顯示 1163。
