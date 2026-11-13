# Issue #1159：分流 #1150 實機驗收的失敗項目

**結論：#1150 共 10 個主項，✅ 5（G01、G02、G03、G05、G06）、❌ 4（G04、G07、F01、P01）、⚠️ 1（#1108 R07）。** 失敗項目歸成三個根因：

- 一個是測試規格錯誤：G04 的證據截圖快捷鍵應該是 Ctrl+Shift+F12，規格卻寫成單鍵 F12。
- 兩個是程式缺陷：建角按鈕在 Click 事件裡同步開 modal，讓 UIA Invoke 卡住；開發工具 TabControl 的 UIA 分頁名稱跟實際分頁錯一格。

依此開 2 張 code 任務、1 張 resource 任務（測試工具），加 1 張只重測失敗項目的 test 任務。沒有項目列為已知限制。本 repo 目前沒有排隊中、`depends_on` 含 #1150 的任務，所以不必改其他任務的依賴。

## 已通過、不重測（✅）

| 編號 | #1150 結果 | 後續處理 |
|---|---|---|
| G01 登入到 CharacterList | ✅ | 重測時只當前置執行並記錄，結果沿用。 |
| G02 主視窗 GDI 截圖 | ✅ | 沿用。修正任務不改主視窗擷取。 |
| G03 主視窗 UIA 樹 | ✅ | 沿用。 |
| G05 開發工具 GDI 截圖 | ✅ | **附帶回歸**，因為 `f1150-devtools-tab-uia` 會改 `DevToolsForm.cs`。 |
| G06 開發工具 UIA 樹 | ✅ | **附帶回歸**，原因同上。 |

#1150 本身沿用的項目照舊沿用，不重測：#1149 S02 系列、#1118 S01、#1126 D01／C01，以及 #1108 B01、R05、R08–R15、C01、C02、V01 和 R07 的協定建角部分。

## 失敗項目 → 任務

| 項目 | #1150 結果 | 分類 | 根因（已驗證／推測） | 對應任務 |
|---|---|---|---|---|
| G04 送 F12 產生證據 | ❌ | (b) 測試規格／工具 | **已驗證**：引擎的證據鍵是 Ctrl+Shift+F12（`DevToolsForm.cs` `IsDevToolsKey`／`ProcessDevToolsKey` → `CaptureEvidence("manual")`）；單鍵 F1–F12 在 `Program.cs` `HandleDown` 直接略過。#1150 已實證 Ctrl+Shift+F12 產生三張新 `manual-*.png`。錯在 #1121／#1136 分流規格與 `Send-UiaF12`（`UiaGdi.ps1:240`），不是引擎缺陷。 | `f1150-uia-helper`（resource）→ `f1150-retest` 重測 G04 |
| G07 SelectionItemPattern 切分頁 | ❌ | (a) 程式缺陷 | **已驗證現象**：「登入／選角」TabItem 執行 Select 後落在「控制」；樹裡只有 3 個 TabItem。**推測根因**：從矩形看，名稱 i 對到原生標籤 i+1（「登入／選角」TabItem 的矩形 433,160,78×36 從分頁列 x=111 算起是第 3 個原生標籤），索引 0、1（網頁面板、服務）不在 Descendants 裡；可能是 .NET 10 WinForms TabControl 的 UIA 導覽／索引問題，或分頁建構順序、巢狀 TabControl 造成。要靜態查證後修正。 | `f1150-devtools-tab-uia`（code，claude-local）→ 重測 G07 |
| F01 建角對話框 | ❌ | (a) 程式缺陷 | **靜態已核對**：`CharacterManagementUi.cs:47` 在 Click 裡 `await OpenCharacterCreationDialog()`，在 :94 同步呼叫 `ShowDialog`。UIA Invoke 在 UI 執行緒同步執行 Click，modal 不返回，所以 Invoke 逾時（0x80131505），後續 UIA 要求都排在它後面（對話框樹要到 Escape 才釋放）。:48–54 刪除角色的 `MessageBox.Show` 是同樣模式。另外對話框的 ComboBox／NumericUpDown 沒有 AccessibleName，UIA 不能用名稱找到。修正後的真機行為待驗證。 | `f1150-modal-invoke`（code，claude-local）→ 重測 F01 |
| P01 README 用法實證 | ❌ | (a)＋(b) 綜合項 | 分頁失敗是 G07、對話框失敗是 F01、單鍵 F12 是 G04。工具本身另有三個問題（已驗證）：`Get-UiaWindow` 找不到 owned 視窗；`Get-UiaTree` 遇到消失元素丟 `ProgrammaticName` 錯；Invoke 沒有逾時保護。 | `f1150-uia-helper`＋上面兩張 code 任務 → 重測 P01 |
| #1108 R07 七職業 GUI 建角 | ⚠️ | 被 F01 阻塞 | 沒有證據顯示職業規則有缺陷；GUI 0/7、沒有送出，原因是對話框的 UIA 可及性。第 6 步 GUI 刪除會遇到同樣的 `MessageBox` 阻塞，一併在 `f1150-modal-invoke` 修。 | `f1150-modal-invoke` → 重測 R07 |

**已知限制（c）**：本輪沒有新增。沿用既有的「此機 WGC 不可用」（#1126 D01），之後一律走 GDI／SendInput。

## 新任務（`docs/plans/issue-1159.json`）

| id | stage／runner | 改動範圍 | depends_on |
|---|---|---|---|
| `f1150-modal-invoke` | code／claude-local | `engine/source/CharacterManagementUi.cs`：建立／刪除角色的 modal 用 `BeginInvoke` 延後開啟並防重入；建角對話框補 AccessibleName | — |
| `f1150-devtools-tab-uia` | code／claude-local | `engine/source/DevToolsForm.cs`：查明並修正 TabItem 名稱／矩形／Select 錯位，以及缺少的 TabItem | — |
| `f1150-uia-helper` | resource／codex-local（runner_lock、real_machine） | `work/f1118-uia-path/`：`Send-UiaEvidenceKey`（Ctrl+Shift+F12）、`Get-UiaWindow -IncludeOwned`、讀樹容錯、Invoke 逾時、`Select-UiaTab`；只用記事本驗證 | — |
| `f1150-retest` | test／codex-local（runner_lock、real_machine、milestone_smoke） | 只重測 G04、G07、F01、P01、R07，附帶回歸 G05、G06；G01 只當前置 | 上面三張 |

兩張 code 任務改的是不同檔案，可以並行。

## 其他排隊任務

查過所有 open Issue（共 9 張）：沒有任何一張的 `depends_on` 含 #1150。#1155 內文只提到「#1139 已由 #1150 取代」，跟本張無關，所以不必關閉或改依賴。#1156（M7 smoke）不依賴 #1150，也不動。

## 做了什麼／怎麼驗證／未驗證什麼

- 做了：
  - 讀 #1150 的本文、留言、RESULT、`task/1150` 分支的報告、證據 JSON 和 README。
  - 對照 `Program.cs`、`DevToolsForm.cs`、`CharacterManagementUi.cs`、`TitleAndEmote.cs`、`UiaGdi.ps1`、`docs/protocol/character-config.md`。
  - 寫分流計畫與本報告。
  - `task/1150` 沒有開 PR，所以把 `docs/reports/issue-1150.md`、`docs/reports/issue-1150-evidence.json`、`work/f1118-uia-path/README.md` 原樣帶進本分支，內容不改，讓後續任務能從 main 讀到。
- 怎麼驗證：用 Python 解析計畫 JSON，並核對 id 與 depends_on 的對應；TabItem 矩形是從證據 JSON 逐筆列出的。
- 未驗證：
  - G07 的確切根因（只有推測，交 code 任務靜態查證）。
  - F01 修正後的真機行為。
  - 這兩項都交給 `f1150-retest`。
- 本張沒有改程式、沒有執行任何程式。

## 給下一棒的提示

1. `f1150-devtools-tab-uia` 先從證據裡的 TabItem 矩形下手：名稱「登入／選角」(433)、「控制」(511)、「狀態列」(595) 都比該有的位置多一格，網頁面板、服務完全沒出現。改之前先確認是 WinForms 本身，還是分頁加入順序或巢狀 TabControl 造成。
2. `f1150-modal-invoke` 只修建立／刪除角色兩顆按鈕；`CharResetUi`、`MailUi`、`ClanUi` 也有同步 modal，但本輪驗收沒用到，只寫進建議。
3. 重測要用 main 重建（引擎有改），並照 #1150 的隔離 DB 副本做法；只有一個空位，送出後要用 GUI 刪除本輪自建角色。
4. #1150 的 PNG、完整 log 與冷備份還在 `scratch/wt/task-1150/scratch/issue-1150/`，調度器清理前如有需要請先保存。

## 第 2 次嘗試說明（2026-11-14）

第 1 次的分流內容已經完整推送（commit 585eb547），但 RESULT 的「結論」行寫成「失敗歸成 3 個根因」。調度器 `_declared_failure` 只看結論行，看到「失敗」就判定這張分流任務失敗。這輪重新核對計畫 JSON：4 張任務的 stage、runner、runner_lock、real_machine、milestone_smoke、depends_on 都符合要求；open Issue 中也沒有 depends_on 含 #1150 的。分流內容沒有改，只補了這一節，並把結論改寫成完成。
