# Issue #558 — T236：聊天輸入列的 YES／NO 鈕（W2-E）

## 做了什麼
- **`engine/source/YesNoPrompt.cs`**（M7-J 的檔案）：
  - 新增 `YesNoButtons`（純函式，方便離線測試）：
    - YES 在 (361, 567)、NO 在 (401, 567)，800×600 邏輯座標（依據 `docs/reference/windows-2.md` §5.3：`InputWindow` 1 號，建構子 `sub_A57590`／FO `0x656990`，靜態分析已驗證）。
    - 提供 `Logical`、`Hit`（邏輯座標命中）、`Client`（原生畫布：照 `UiViewport.ToClient` 的倍率與偏移換算）、`Legacy`（舊 HUD 沒有 800×600 畫布：照原版順序與 40 px 間距，放在提示列右端）。
  - `ClientForm` 加兩顆 WinForms 文字鈕「YES」「NO」：
    - 只在 Yes/No 提示顯示時出現；原生 UI 啟用時放在 (361, 567)／(401, 567) 換算到視窗的位置，沒啟用時貼在提示列右端。
    - 按鈕設成不可選取（不搶聊天輸入框或遊戲的焦點）。
    - 點擊時回答**畫面上顯示的那一個**提示（`AnswerYesNo(view, bool)`）：送出的 C121 與 Y／N＋Enter 完全相同；隊伍、決鬥、結婚仍走各模組自己的送出路徑。之後若有新的 S219、世代或世界改變，舊按鈕點了不會送出。
  - 沒有新增封包處理；沒有改 `WorldSession.cs`。
- **新測試 `engine/source/YesNoButtonsTests.cs`**：`[WorldModuleTest("yes-no-buttons")]`，29 項，由 `--module-tests` 執行。內容包括座標、命中邊界、s=1／2、1920×1080 置中、等比填滿往返對映、舊介面位置，以及 C121 accept／decline 位元組。
- `engine/_integration/dev/rebuild-lock.json`：用 `--update-lock` 重產（290 個來源／299 個鎖定檔）。

## 怎麼驗證（已驗證）
- `rebuild.py --update-lock` → `dev-inputs-verified`。
- `rebuild.py --build` → PASS，productDll `cd60f1cd3199ebbeedbf7c1e25f3899b19f372e4fca66ab8b8b466cb7fc7436a`。
- `P11.ClientEngine.exe --module-tests`：結束碼 0，status pass，20 組 1018 項，0 失敗（`yes-no-buttons` 29 項）。
- `DemoChecks … yes-no`（M7-J 既有測試）：`PASS 425 DEMO checks`。
- `engine/tools/check-no-game-assets.py`：pass，3842 個追蹤檔，0 錯誤。
- CI：見 PR。

## 推定／未驗證
- **按鈕大小 32×14 是推定**：文件只給左上角，沒有寬高、字型與底圖。原版的文字鈕外觀（字色、是否有框）待驗證。
- 按鈕是 WinForms 控制項疊在畫面上，**不是** `IUiPanel` 原生面板。原因是改 `UiPanelRegistry` 不在本任務的可改範圍。原生面板化可另開任務。
- 舊 HUD 沒有 800×600 畫布，位置改成「貼在提示列右端」，這是引擎的權宜做法，不是原版位置。
- 沒有做真機驗收：沒有啟動遊戲，也沒有看實際畫面與點擊。

## 產出路徑
- `engine/source/YesNoPrompt.cs`、`engine/source/YesNoButtonsTests.cs`、`engine/_integration/dev/rebuild-lock.json`、本報告。

## 給下一棒的提示
- 真機 smoke 時要確認：提示出現時 YES／NO 跟著出現；點 YES 等同輸入 Y 後按 Enter；點 NO 等同拒絕；按完聊天框的焦點與內容不變。
- 若要改成原生面板（surf／字型照原版）：把 `YesNoButtons.Logical`／`Hit` 搬進一個 `IUiPanel`（Layer=Float），在 `UiPanelRegistry` 註冊，再移除這裡的 WinForms 按鈕。
- 本機 worktree 要建置時，`engine/vendor` 需要是指向原專案 `engine/vendor` 的 junction（本次已建立，沒有進 git）。
