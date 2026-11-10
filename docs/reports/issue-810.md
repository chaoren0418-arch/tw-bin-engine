# Issue #810（T347）R7-H 視窗堆疊與 ESC — 報告

依據：`docs/reference/gap-matrix.md` 15.2 第 6 點、15.6 R7-H；FU4§1.1（`docs/reference/followups-4.md`）；`NativeUi/UiRoot.cs` 的 `sub_A57C60` 註解（#677、#733）。

## 做了什麼

1. **全域 ESC＝`sub_A57C60(0)`**
   - `UiRoot.CloseAll()`（`engine/source/NativeUi/UiRoot.cs`）：由上往下關掉堆疊裡的每個窗（最多 4 個），回傳實際關掉的窗。
     - 只有 `IUiClosablePanel` 能關；不能關的窗留在原位。
     - 被擠出（evicted）或被拒絕（refused）的窗不在原版陣列裡，不處理。
     - `Close` 丟例外的面板會停用，其他窗照樣關。
   - `UiInput.GlobalEscape`（`NativeUi/UiInput.cs`）：`UiInput.Key` 的最後一步。
     - 觸發條件：單純按下 ESC（沒有修飾鍵、不是自動重複），而且所有面板都沒吃這個鍵（包括有焦點的面板、由上往下的可用面板、被擠出的窗）。這對應 FU4§1.1：`sub_8CA740` 先讓堆疊窗、聊天列吃鍵，都沒吃才交給 `sub_8C14F0`。
     - 有窗真的被關掉才吃掉這個鍵，並呼叫 `ReturnGameFocus`。
     - 沒有窗開著時 ESC 照舊交給遊戲（YES/NO 拒絕、`ClearInput` 等）。
2. **`WindowEvicted`／`WindowRefused` 有訂閱者了**：`UiInput` 收到這兩個事件時，清掉該窗的焦點、滑鼠捕捉與拖曳（15.2 表列的 R7-H 部分）。`UiRoot.Toggle` 的產品呼叫者歸 R7-I（選單熱鍵），本組不動。
3. **8 個面板實作 `IUiClosablePanel`**：堆疊關窗（第 5 個窗擠出、全域 ESC）都比照該窗的關閉／取消鈕。

   | 面板 | Close 做什麼 |
   |---|---|
   | `QuitWindowPanel`（QUIT_WINDOW） | 等同取消：只關窗（`sub_A59AD0`），呼叫 `Source.Close`；不重新開始、不離開；已關著時不呼叫 |
   | `UserPlayStatsPanel`（UserPlayStatsUI） | 等同取消：呼叫 `Source.Dismiss`（計時照走），一併關掉 UserObtainUI 清單窗。用明確介面實作，因為公開的 `Close` 已經是「關閉鈕」屬性 |
   | `FieldMapPanel`（Field_Map） | 等同結束鈕：`CloseField`。面板仍是 Visible、Bounds 變空，所以 Ctrl+M 照樣能開。**行為改變**：以前被擠出時只是藏起來，現在真的關掉 |
   | `SummonPanel`（SummonMonster_Window） | 等同關閉鈕：記下這次的 revision，呼叫 `Host.Close`；同一次請求只關一次 |
   | `PersonalShopPanel` | 原本的 `Close`（關閉鈕／取消／Esc）直接當介面實作 |
   | `FriendPanel`（FriendUI／ExcludeUI／MailExcludeUI） | `Hide`（本機關窗，不送封包） |
   | `AgitMapPanel`（AgitMap_Window） | 等同關閉鈕 678：記下這次請求，之後有新請求才會再開 |
   | `LocationListPanel`（引擎自己的過渡清單） | 藏起目前這一版清單，有新分享（revision 增加）時再顯示；1785 提示列跟著聊天輸入列走，這裡不動 |

4. **新的離線測試** `engine/source/NativeUi/UiStackEscTests.cs`：`[WorldModuleTest("native-ui-stack-esc")]`，自動發現，不改註冊表。共 13 段：
   - 型別檢查；
   - `CloseAll` 的 4 種情況（全部能關、有不能關的、有被擠出的、Close 丟例外）；
   - ESC 的條件：Shift、自動重複、放開都不算；沒有窗開著時交給遊戲；自己會處理 ESC 的窗先拿到；
   - 被擠出或被拒絕時清掉焦點；
   - 8 個面板各自的 Close；
   - 實際面板放進 `UiRoot`：第 5 個窗把 Field_Map 真的關掉，ESC 一次關掉剩下的。

## 怎麼驗證

- **已驗證（本機）**：`dotnet build engine/source/OfflineClient.csproj -c Release` 的結果是 0 錯誤、3 個警告，3 個警告都在既有檔案（`HtmlActionTests.cs`、`MagicUi.cs`），不是本次改的。
  - 本機的 worktree 沒有 `vendor`，所以 restore 改用 `scratch/t810/nuget.config` 指向原專案的 `engine/vendor`，套件快取放在 `scratch/t810/packages`。
  - 帶 `-r win-x64` 時，本機 SDK 沒有 runtime pack，而且會出現 WebView2 winmd 的 NETSDK1130 錯誤。這是環境問題，CI 用 nuget.org 不受影響。
- **已驗證**：`py -3 engine/tools/check-no-game-assets.py` 的結果是 `status: pass`、0 錯誤。
- **待 CI**：既有單元測試與 `native-ui-stack-esc`。依本機限制，`--module-tests` 不在本機執行。
- 沒有改 `rebuild-lock.json`、`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`。

## 未驗證什麼

- **推測**：
  - 自己會處理 ESC 的窗（QUIT_WINDOW、UserPlayStatsUI、PersonalShop、Trade、Pledge 等）在最上層時，ESC 只關它自己，下一次 ESC 才由全域關掉其餘的窗。這是照 FU4§1.1 的分派順序推的：堆疊窗先吃鍵，全域處理在後。原版這些窗本身吃不吃 ESC，沒有逐一查。
  - 全域 ESC 和 YES/NO 輸入列（`KeyboardNavigation.cs`）同時存在時的先後：原生 UI 會先把 ESC 拿去關窗。原版輸入列屬於聊天列，會比 `sub_8C14F0` 先吃鍵，所以有詢問時 ESC 可能應該先送拒絕。這點待真機確認。
- **沒做**：
  - FU4 提到 ESC 也會關「字型窗」（`FontWindow`，Ctrl+O），引擎沒有這個窗。
  - 公告板按鈕窗的 `IUiClosablePanel` 歸 R7-F，不在本組的可改檔案裡。
  - `CSummonUI` 本身的 ESC 細節（Q7-3）只靠全域 ESC 處理，`SummonPanel.Key` 仍然不吃鍵。
- **待真機**：開滿 4 個窗再開第 5 個、按 ESC，看畫面與焦點是否正確；以及關掉 QUIT_WINDOW／UserPlayStatsUI 之後，死亡計時是否照走。
- 本組只改程式行為，沒有用到任何遊戲資源，也沒有宣稱缺資源，所以沒有做 PakViewer 查詢。

## 產出路徑

- `engine/source/NativeUi/UiRoot.cs`、`UiInput.cs`
- `engine/source/NativeUi/QuitWindowPanel.cs`、`FieldMapPanel.cs`、`SummonPanel.cs`、`PersonalShopPanel.cs`、`LocationListPanel.cs`、`FriendPanel.cs`、`AgitMapPanel.cs`、`UserPlayStatsPanel.cs`
- 新檔 `engine/source/NativeUi/UiStackEscTests.cs`
- 本報告 `docs/reports/issue-810.md`

## 給下一棒的提示

- **R7-I**：選單熱鍵改呼叫 `root.Toggle` 時，Field_Map 已經可以關閉，「再按一次＝關掉」也走得通。
- **R7-F**：公告板按鈕窗實作 `IUiClosablePanel` 之後，ESC 就會一起關掉它，不必另外接線。
- **gap-matrix 下一輪**：15.2 第 6 點的「9 個 Window 層面板沒有實作」只剩 Board 按鈕窗（R7-F）；`UiInput.cs` 已經有全域 ESC；`WindowEvicted`／`WindowRefused` 已經有訂閱者（`UiInput`）。
- **Q7**：有詢問時 ESC 該先送拒絕還是先關窗，要確認 `sub_8CA740` 的吃鍵順序裡輸入列（`sub_4FC710`）排在哪裡。
