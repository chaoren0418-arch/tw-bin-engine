# #1201：分流 #1186（M7 真機 smoke）失敗項目 → 修正任務＋重測

結論：#1186（✅ 7／❌ 10／⚠️ 42）分成 **4 張 code**、**2 張 resource**、**1 張重測**，另有 **2 項已知限制或待裁定**（SM01、SM06），不開任務。本張只改文件與規劃檔，沒有改程式，也沒有執行程式。任務定義在 [`docs/plans/issue-1201.json`](../plans/issue-1201.json)。

## 做了什麼

1. 讀 #1186 最後一則 `=== RESULT ===`（2026-11-14 06:38 UTC）、Issue 內文，以及 `origin/task/1186`（`885181b6`）的 `docs/reports/issue-1186.md`。
2. 唯讀查看 #1186 證據根 `D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-1186-evidence` 的關鍵截圖（`smoke-A3/normal-mage-ready.png`、`smoke-A/m7-SM07-1.png`、`smoke-A/m7-SM16-help-1.png`、`smoke-A/m7-SM57-1.png`、`smoke-A3/m7-SM18-2.png`、`smoke-A3/m7-SM45-1.png`、`smoke-A3/m7-SM31-4.png`、`smoke-A/m7-SM01-1.png`）與 `native-ui.log`。
3. 靜態讀碼，替每個 ❌ 找出可能根因，把同一根因的項目合成一張任務。
4. 查詢本 repo 所有 open issue：除了本張，沒有任何 issue 內文提到 `#1186`／`f1125-retest`，也沒有 `depends_on` 指向 #1186，所以不需要關閉或改其他任務。#1197（f1188-retest，map4 SEG）不依賴 #1186，維持原狀，只在重測任務裡註明兩者的關係。

## 已通過、之後不重測（#1186 ✅）

| 編號 | 沿用內容 | 是否附帶回歸 |
|---|---|---|
| SM02 | 參考 XML（WidgetSlot／LoginUI／OptionUI）載入與 SHA | 否 |
| SM04 | 帳號輸入、錯密碼、正確登入、HKL、Exit | **是，只看登入框文字的大小與位置**：`f1186-uitext-pixel` 會改所有原生 UI 字型 |
| SM05 | 登入背景動畫 | 否 |
| SM15 | 在世界內按 Ctrl+P 會產生截圖檔（`Capture/20261114-135835-970.png`）、Alt+R | 否 |
| SM55 | `--classic-ui`、nativeUi:false、刪除設定後恢復原生介面 | **是，只看 `--classic-ui` 選角仍是 WinForms**：`f1186-native-select` 會改選角畫面的掛載路徑 |
| SM56 | 六次正常 closed | 否 |
| SM59 | classic 頁籤自動選取與焦點 | 否 |

## 每個失敗項目對到哪張任務

### ❌（10 項）

| 編號 | #1186 觀察 | 分類 | 對應 |
|---|---|---|---|
| SM07 | CharacterList 有三個角色，原生畫布卻是黑底「等待真實世界初始化」，沒有選角列 | 程式缺陷 | `f1186-native-select` |
| SM10 | HUD 的 HP／MP 數值、百分比、左側狀態文字重疊 | 程式缺陷（字型 DPI） | `f1186-uitext-pixel` |
| SM12 | 聊天列字高超過列距、底部被裁 | 程式缺陷（字型 DPI） | `f1186-uitext-pixel` |
| SM16 | Ctrl+H 說明頁文字互相覆蓋 | 程式缺陷（字型 DPI） | `f1186-uitext-pixel` |
| SM18 | 背包名稱／數量文字重疊 | 程式缺陷（字型 DPI） | `f1186-uitext-pixel` |
| SM20 | 魔法窗「光箭」超出格子 | 程式缺陷（字型 DPI） | `f1186-uitext-pixel` |
| SM33 | HyperText 文字嚴重重疊 | 程式缺陷（字型 DPI） | `f1186-uitext-pixel` |
| SM11 | 頭上量表離角色身體很遠 | 程式缺陷（座標換算） | `f1186-headbar-offset` |
| SM57 | 常用選項只有勾選框，沒有名稱 | 程式缺陷（執行期才填的 Caption 沒填） | `f1186-option-labels` |
| SM50 | map4 區塊 4/80048001 是 SEG，無法載入 | 程式缺陷，**已由 #1195 修正（已合進 main）** | 不另開 code；由 `f1186-retest` A 組重測（#1197 也會驗同一個區塊） |

### ⚠️（42 項）

| 編號 | 分類 | 對應 |
|---|---|---|
| SM01 | 待裁定：耗時 6565／10137 ms 可否接受；另外「核對中」畫面沒有舊 HUD，與 smoke-m7 的預期不同 | **不開任務**，交主控裁定（見下方「待裁定」） |
| SM06 | 伺服器沒有送 S48 公告（newsPending=false），smoke-m7 規定這時寫「伺服器未送」 | **已知限制**；`f1186-smoke-fixtures` 只唯讀確認公告來源 |
| SM08、SM09 | 被 SM07 擋住；缺空角色欄位 | `f1186-native-select`＋`f1186-smoke-fixtures` → 重測 A 組 |
| SM19 | 被 map4 SEG 擋住 | #1195（已修）→ 重測 A 組 |
| SM03、SM13、SM14、SM17、SM27、SM28、SM29、SM32、SM41、SM42、SM43、SM45、SM46、SM52 | 測試涵蓋不足或沒有觸發（操作或時間不夠） | 重測 B 組 |
| SM25、SM26、SM30、SM36、SM40、SM47、SM48、SM49、SM58 | 缺物品、角色或職業條件（#1186 規定不得補物品） | `f1186-smoke-fixtures` → 重測 B 組 |
| SM21、SM23、SM24、SM37、SM39、SM44、SM54 | 需要 NPC、血盟或死亡等條件（以座標與正常遊戲方式準備） | `f1186-smoke-fixtures`（座標、物品）→ 重測 B 組 |
| SM22、SM31、SM34、SM35、SM38、SM51、SM53 | 測試工具問題：聊天指令會串接、無法點選對方角色、沒有錄音 | `f1186-smoke-tooling` → 重測 B 組 |

## 新任務

| id | stage／runner | 處理項目 | 依賴 |
|---|---|---|---|
| `f1186-uitext-pixel` | code／claude-local | SM10、12、16、18、20、33 | — |
| `f1186-native-select` | code／claude-local | SM07（解除 SM08、09 的阻塞） | — |
| `f1186-headbar-offset` | code／claude-local | SM11 | — |
| `f1186-option-labels` | code／claude-local | SM57 | — |
| `f1186-smoke-fixtures` | resource／codex-local、runner_lock、real_machine | ⚠️ 中「缺條件」的各列 | — |
| `f1186-smoke-tooling` | resource／codex-local、runner_lock、real_machine | SM22、31、34、35、38、51、53 的工具問題 | — |
| `f1186-retest` | test／codex-local、runner_lock、real_machine、milestone_smoke | **只重測**：SM03、SM07–SM14、SM16–SM54、SM57、SM58；回歸 SM04（只看文字）、SM55（只看 classic 選角） | 上面 6 張 |

重測不包含：SM01（待裁定）、SM06（已知限制），以及 SM02、05、15、56、59（✅ 沿用）。

## 已驗證／推測／待驗證

**已驗證（唯讀查看與讀碼）**
- `engine/source/NativeUi/UiText.cs:12` 以 `GraphicsUnit.Point`、9pt 建字型；原版是 `CreateFontA(12,…,136,…,"MingLiU")`，以像素為單位（`ObjectRendering.cs:501`、`NativeFontTests.cs:38`）。物件名稱走 `ObjectRendering.cs` 的像素字型，在同一張截圖裡大小正常（`smoke-A/m7-SM57-1.png`）。
- SM07 的黑底字樣來自 `engine/source/Program.cs:182–183`。選角面板的顯示條件在 `SelectCharPanel.cs:359`，掛載在 `CharacterManagementUi.cs:136–143`。`smoke-A/native-ui.log` 有 SelectUI.xml／CreateUI.xml 載入成功的紀錄，沒有 `select-char-ui … 停用`。
- `OptionPanel.cs:329–333` 遇到 Caption="name0" 這類「執行期才填」的 Label 會直接跳過；`:403–404` 只有在文字解析成功時才畫。
- 沒有其他 open issue 依賴 #1186。

**推測**
- 文字約兩倍大的原因：畫布 Bitmap 的解析度跟著螢幕 DPI 192 走，9pt 換算成 24px，而版面是以 12px 為準。#1186 報告也寫了「尚未證明根因是 DPI」，交給 `f1186-uitext-pixel` 確認。
- SM11 頭上條的位置大約是身體座標×0.7，可能是座標系或縮放換算不一致；不確定是否和 DPI 同源，所以另開一張。
- SM57 勾選框的名稱可能是原版在執行期才填入，和字型 DPI 是不同的根因。

**待驗證**
- 以上所有修正都要在真機確認，由 `f1186-retest` 處理。
- 本張沒有執行任何程式、沒有建置，也沒有連 DB 或伺服器。

## 待裁定（交主控）

- **SM01**：#1186 量到兩次耗時 6565／10137 ms。smoke-m7 要求「核對期間顯示舊 HUD」，但截圖 `smoke-A/m7-SM01-1.png` 只有離線場景、沒有 HUD。請主控裁定：(1) 耗時是否可以接受；(2) 核對中沒有舊 HUD 要算缺陷（若算，另開 code 任務）還是改文件預期。
- **C02**：主專案 `scratch/nuget-packages/microsoft.web.webview2/1.0.4258.31/` 有 3 個檔案的修改時間變了，不知道是哪個工作造成的，請調度器釐清；這不是 SM 項目，所以不開任務。
- nativeUi 是否維持預設、#475／T112 能否結案：照 #1186 的提示，由主控裁定。

## 產出路徑

- `docs/plans/issue-1201.json`（7 張任務）
- `docs/reports/issue-1201.md`（本檔）

## 給下一棒的提示

- 四張 code 任務彼此獨立，可以平行做。`f1186-uitext-pixel` 影響最大（6 項），建議先做。
- `f1186-native-select` 不要改到 `--classic-ui`／nativeUi:false 的 WinForms 選角路徑；SM55 回歸就是在驗這件事。
- SM50／SM19 已由 #1195 修好，**不要再開 SEG 的 code 任務**。#1197 若先跑出 ❌，`f1186-retest` 的 SM19／SM50 先標「受 #1197 阻塞」。
- `f1186-smoke-fixtures` 只能依 #175 授權寫入：設 GM、建立測試角色、發給物品；不得直接改血盟表或寵物表，改之前必須冷備份，並附還原 SQL。
- 重測時伺服器若沒有回應，要先用新工具確認 C 封包已經送出，才能寫「已送出、伺服器未回」；不得直接標「伺服器不支援」。
