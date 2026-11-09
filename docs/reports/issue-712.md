# Issue #712 T300：R4-I 原生 `HYPER_TEXT_WINDOW`

階段 code，第 1 次嘗試。分支 `task/712`。依據：`docs/reference/gap-matrix.md` 13.5 節 R4-I。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| 新檔 `engine/source/NativeUi/HyperTextPanel.cs` | 原生 `HYPER_TEXT_WINDOW` 面板（`[UiPanel(173)]`，layout `uiextra86`，id `HYPER_TEXT_WINDOW`）。實作 `IUiMovablePanel`、`IUiClosablePanel`、`IUiTextInput`。版面從執行期 XML 讀：底 1968 (6,9)、框 1969（窗原點）、捲軸 1970 (217,51)、上 2042／下 2040、關閉 X 1962 (225,15)。內容：本文（逐字換行）、連結列（可點／停用）、提示文字、頁內數字框（S136 數量、城堡 tax／withdraw／deposit，城堡頁多一個「取消」）、捲動（滾輪、上下鈕）、標題列拖曳、關閉鈕。另含 `HyperTextRouter`（單窗取代規則）與 `IHyperTextHost`（面板不碰 `LoginConnection`）。 |
| `engine/source/NpcUi.cs`（只限頁面分流） | `SyncInteraction` 在原生 UI 開啟、面板沒有故障時，把 NPC 頁／查詢頁／伺服器頁＋數字框交給原生窗（`SyncNativeHyperText`），WinForms 面板只留不是「頁」的東西（商店／服務清單、等待提示）。原生 UI 關閉或面板故障時，WinForms 行為完全不變。宿主 `NativeHyperTextHost` 的連結、送出、取消、關閉都走既有的 `RunInteraction`／`RunNpcPrompt`（版本過期就不送）。 |
| 新檔 `engine/source/HyperTextPanelTests.cs` | `[WorldModuleTest("hypertext-panel")]` 離線測試（自動發現）：註冊、XML 版面與丟例外、繪製位置、換行、連結點擊、捲動、數字框（輸入、Backspace、Enter 送出、範圍、版本重設、城堡取消）、關閉與 4 層堆疊擠出、單窗取代規則、ClientForm 接線。只用合成 XML／圖、固定字寬。 |

**單窗取代（op 144 的差異）**：原生 UI 開啟時，NPC 頁與查詢頁共用一個窗，最新出現的那頁在前，被蓋掉的那頁在本機關掉（`CloseInteraction`／`CloseQueryPage`，不送封包），不會再浮出。關閉鈕關掉窗內所有頁，但**不會**關掉商店／服務清單。原生 UI 關閉時 WinForms 仍是原本的並列顯示（備援入口不變）。

沒有新封包；沒改 `UiClientBridge.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

## 依據分級

- **已驗證（引用）**：同一個 `HYPER_TEXT_WINDOW` 顯示 NPC HTML、查詢／排名結果、修理空清單頁（WIN§2.1）；城堡 tax／withdraw／deposit 是此窗的頁（WIN2§5.1）；S136 的數字框在 HTML 頁內、不是 `InputWindow`（WIN2§5.3）；版面元件與座標（FU4§5、WIN§1.5）。
- **推測（程式內註明）**：文字起點 (21,44)、行高 15、16 行（借 `sub_A559D0`，FU4§5）；可用寬 192；關閉鈕用 XML 的 1962（`sub_A559D0` 寫死 9183 是那個窗自己的做法）；標題列 (1,1,239,20)；顏色（本文白 14、連結黃 13、停用／提示灰 7）；連結各占一列（引擎模板把連結拆成選項）；數字框是引擎自訂的框＋「確定」「取消」文字鈕（原版外觀沒查證，沒有 surf，不畫替代圖）；兩頁同一格出現時查詢頁在前。
- **待驗證（沒做）**：ESC 是否關窗；連結滑鼠移入效果；HTML 顏色／圖片標籤；數字框上下調整鈕；HyperTextUI（`sub_875810`／`sub_BBCB00`）本身的繪製函式沒有逐行追。

## 怎麼驗證

- 產品編譯 `engine/source/OfflineClient.csproj`（Release，SDK 10.0.300）：**0 錯誤**，2 個警告都在既有的 `MagicUi.cs:20`。
  - 本機備註：worktree 直接 restore 出現 NETSDK1130（WebView2 targets 沒寫進 assets，同 #705 的本機環境問題），`rebuild.py --build` 也因此在 product-build 失敗。改用 `scratch/issue-712/NuGet.Config`（指向原專案離線 `vendor`）＋獨立 packages 目錄＋`UseArtifactsOutput`（輸出在 `scratch/issue-712/artifacts`）才編過。與程式碼無關；CI 不受影響。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass（4059 個追蹤檔，0 錯誤）。
- 模組測試（含新的 `hypertext-panel`）：依規定**本機不執行**編譯產物，交給 PR 的 CI（`--module-tests`）。CI 結果見 PR。

## 未驗證什麼

- 沒有啟動遊戲、伺服器、DB；原生窗沒有真機畫面驗收（字型、換行寬度、連結點擊範圍、數字框輸入、IME）。
- 真實 `uiextra86-c.xml` 是否能讓本面板 Load 成功：元素與已上線的 `ClanWarehouseHistoryPanel` 相同（同一段、同名元件），推定可以，但本機沒有執行。

## 產出路徑

- `engine/source/NativeUi/HyperTextPanel.cs`
- `engine/source/HyperTextPanelTests.cs`
- `engine/source/NpcUi.cs`
- `docs/reports/issue-712.md`

## 給下一棒的提示

1. **R4-A 收尾**：`UiClientBridge.cs` 的 `ToggleMenuLocalPage`（選單 5／6、Ctrl+H／V／C）還會呼叫 `ShowDevToolsPage("NPC")` 開開發工具。R4-I 合併後，原生 UI 開著時這一頁已經在原生窗顯示，該行應改成只在原生窗不可用（`NativeHyperText()==null`）時才開開發工具。檔案屬 R4-A，本任務沒改。
2. 原生 UI 開著時，選單本機頁、伺服器主動頁會依單窗規則**蓋掉**正在顯示的 NPC 對話頁（本機關掉 NPC 對話，不送封包）；商店／倉庫清單不受影響。真機 smoke 時請確認這與原版一致。
3. 真機驗收清單（交給 dot／Codex）：原生 UI 開啟 → 和 NPC 對話（頁面、連結換頁、action 送出）→ 製作類 NPC 的 S136 數量（輸入、Enter、確定）→ 城堡稅率／金庫頁（確定、取消）→ `/who` 查詢結果蓋掉 NPC 頁 → 關閉鈕 → 開滿 5 個窗看是否擠掉 → 原生 UI 關閉時 WinForms NPC 分頁照舊。
4. 之後若查到 HyperTextUI 真正的文字起點、行高、連結樣式或數字框位置，只需改 `HyperTextPanel.cs` 檔頭列出的常數。
