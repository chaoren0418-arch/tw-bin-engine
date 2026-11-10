# Issue #855 — T371：R9-K 交易數量

依據：`docs/reference/followups-9.md` 第 12.1／12.2 節（Q7-12 數量輸入）與第 16.2 節 R9-K、`gap-matrix.md` 第 15.11 節（4:ExchangeUI）；主控裁定（#818）：金幣框照原版**移除**。

## 做了什麼

- `engine/source/NativeUi/TransactionPanel.cs`（TRANSACTION_WINDOW）
  - **移除金幣輸入框**（R4-C 引擎自加：`AdenaBox`、`AdenaInput`／`AdenaFocused`、`SubmitAdena`、`TransactionRules.AdenaAmount`，以及 `TransactionView.AdenaHeld／AdenaId`）。
  - 加物品（原生背包選取＋點自己的清單）改成照原版 `sub_82DF30`：
    - **堆疊（數量 >1，金幣也是）先問字串 140**「要給幾個？ (1-%u， Enter=全部，Esc=取消)」，`%u`＝持有量；
      Enter 空白＝全部、輸入數字＝1…持有量（`TransactionRules.Amount`，另限 int.MaxValue＝C37 的 D 欄），合法才送 C37；不合法不送、文字框寫原因。
      Esc＝只取消提示（不送 C86）；Backspace 刪一位；數字走 WM_CHAR（`IUiTextInput.Char`）。
    - **單件直接送** C37（數量 1）。
    - **自己已放 16 件以上**：送出步驟不送、文字框印字串 255「你一次不能交換１６個以上的物品。」（`sub_82E870`）。
    - 提示在換一筆交易（generation）、不能加（加入中／已確定／取消中）、交易結束、`IUiClosablePanel.Close()` 時作廢；對方加物品（revision 變）不作廢，送出時用當下 revision。
  - 新增 `TradeAmountText`（140／255；字串表讀得到就照表，讀不到用同樣原文）、`TransactionAmountPrompt`。
- `engine/source/TradeUi.cs`：註解更新；`--classic-ui`／備援的 WinForms 交易面板保留，數量框改成預設整疊（對應 140 的「Enter=全部」）。
- 新檔 `engine/source/TradeAmountTests.cs`（`[WorldModuleTest("trade-amount")]`，自動發現）。
- **範圍外的必要修改**：`engine/source/TradeFollowupTests.cs`。它原本斷言已被裁定移除的金幣框（`AdenaBox` 測試、`AdenaAmount` 規則、`AdenaHeld／AdenaId`），不改就無法編譯；只做了最小調整：刪掉 `AdenaBox` 測試與金幣框規則斷言，`AddFromInventory` 改成「點清單 → 140 提示 → 空白 Enter → 整疊 C37」。其他測試內容不動。

## 已驗證／推測／待驗證

- 已驗證（文件＋本機資源）：流程與字串依 FU9§12.1（TW16 `sub_82DF30`／`sub_82DB50`／`sub_82E560`／`sub_82E870`）。本機 `string-c.tbl` 用 PakViewer CLI 唯讀解出（`D:\lineage開發\0-客戶端\Text.idx` → `string-c.tbl`，輸出在 `scratch/task855/`，未進 repo）核對：140＝「要給幾個？ (1-%u， Enter=全部，Esc=取消)」、255＝「\f1你一次不能交換１６個以上的物品。」、941＝「此為無法交易的道具。」、1181＝「這個魔法娃娃目前正在使用中。」、1188＝「這個寵物項鍊不能交易或丟在地上。」。
- 引擎做法（標明）：原版 140 提示在**聊天輸入列**（`sub_AC98E0`）。聊天輸入列答覆的分派在 `PlayerChat.SendPlayerChat`（本任務不可改），所以提示畫在交易窗文字框（2008），數字由交易窗自己接。
- 推測：「16 件以上」用 `>=16`（自己這一側已有的 S35 列數）；255 在送出步驟檢查（所以堆疊會先問數量再被擋，照 FU9 對 `sub_82E870` 的描述）。
- 未做：941／1181／1188 的本機檢查（引擎背包沒有不可交易、娃娃使用中、寵物項鍊的旗標；伺服器仍會擋）。
- 待驗證（真機）：3.81 `LiTo.bin` 是否同 TW16；提示文字在文字框的實際可讀寬度。

## 怎麼驗證

- 本機編譯：`dotnet build engine/source/OfflineClient.csproj -c Release`，0 錯誤；觸及的檔案沒有新警告。
  註：本機 NuGet 還原把 WebView2 解成 net462／winmd（NU1701 → NETSDK1130，與本變更無關，main 同樣會遇到），本機只為了編譯檢查用 scratch 內的 MSBuild hook（`-p:CustomAfterMicrosoftCommonTargets=scratch/task855/wv2-fix.targets`，把 winmd 換成 `lib_manual/netcoreapp3.0` DLL）；沒改 repo 的專案檔。正式以 CI 為準。
- `python engine/tools/check-no-game-assets.py`：pass（4339 個追蹤檔、0 錯誤）。
- 依本機執行限制，**沒有在本機執行測試**；`trade-amount`、`trade-followup`、`transaction-window` 等 WorldModuleTest 交給 GitHub Actions CI。

## 產出路徑

- `engine/source/NativeUi/TransactionPanel.cs`、`engine/source/TradeUi.cs`、`engine/source/TradeAmountTests.cs`、`engine/source/TradeFollowupTests.cs`
- `docs/reports/issue-855.md`（本檔）
- 分支 `task/855`

## 給下一棒的提示

1. 後續任務（需要改 `PlayerChat.cs`）：把 140 提示搬到聊天輸入列——在 `SendPlayerChat` 開頭加一個分派（同 `ItemDeletePromptActive`），把答覆交給 `TransactionPanel.SubmitAmount` 的邏輯（可抽成 `TransactionRules.Amount`＋`ITransactionSource.Add`）。私商 1028 確認（R7-G）也在等同一個分派點，可以一起做。
2. 941／1181／1188：等背包有「不可交易」等旗標（或 3.81 的對應資料）後再在 `AddSelected` 前檢查。
3. 真機核對：拖一疊金幣進交易窗應出現 140；空白 Enter 整疊、輸入超過持有量不送；放滿 16 件後再加應出現 255。
