# Issue #854：T370 R9-J 個人商店流程

依據：`docs/reference/followups-9.md` §9.4、§16.2（R9-J）；`gap-matrix.md` 15.11（Q7-9）。分支 `task/854`。

## 做了什麼

| 項目 | 做法 | 檔案 |
|---|---|---|
| 背包 → 出售／收購清單 | 原版是從背包拖進窗（`sub_98F330`）。引擎沒有跨窗拖曳，**改用替代做法**：先在背包選取物品，再點設定窗的出售／收購空格。點有物品的格子只選取。 | `NativeUi/PersonalShopPanel.cs`（`SlotClicked` 事件）、`PersonalShopIntegration.cs`（`PersonalShopController.BeginPrompt`） |
| 聊天列 898 單價 → 933 數量 | 先問 898；解析失敗印 938，單價 ≥ 10,000,000 印 900，收購單價超過持有金幣印 936。堆疊物品再問 933（`%s`＝934「販售」／935「購買」、`%u`＝上限）：Enter 不輸入＝全部，Esc 取消。輸入錯誤時提示不關，清掉已輸入的字讓使用者重打。 | `PersonalShopIntegration.cs`（`PersonalShopPrompt`、`PersonalShopText`、`PromptEnter`） |
| 提示的輸入與顯示 | 設定窗實作 `IUiTextInput`，提示中的字元、Enter、Backspace、Esc 由設定窗轉給控制器。提示開始時鍵盤離開遊戲（數字鍵不會觸發快捷鍵），結束時還給遊戲。新增 Float 層的 `PersonalShopPromptPanel`（`[UiPanel(116)]` 自動發現），把提示畫在 `ChatPanel` 的 `CHAT_BAR` 位置（字的起點和聊天輸入文字一樣是 +4,+2）。 | `PersonalShopIntegration.cs`、`NativeUi/PersonalShopPanel.cs` |
| 放入檢查（`sub_DDED00`） | 本端做 932（裝備中）和 940（已在清單）。 | `PersonalShopIntegration.cs` |
| 雙擊移除 | 雙擊（Pick 515）有物品的格子就移除；移除後的放開不會在空格上開提示。 | `NativeUi/PersonalShopPanel.cs` |
| OK 檢查（`sub_DE1AD0`） | 本端做 876（地圖 303／70）和 904（出售總額），另外保留原本送出前的背包重核。 | `PersonalShopIntegration.cs`（`SendOpen`） |
| 兩行訊息 | `PersonalShopPackets.Chat`：訊息 1 ＋ `0xFF` ＋ 訊息 2 ＋ `0x00`。每行最多 40 byte，截斷時不會把雙位元組字切成兩半。第 2 行是空的時候 `0xFF` 照樣寫。 | `PrivateShop.cs`（只改訊息組裝） |
| 外型不送 | 本來就沒有送（C_Shop 不讀）；新測試確認選不同外型時 C38 一個 byte 都不變。 | — |
| 訊息欄打字 | 設定窗是 `IUiTextInput` 以後，聚焦的訊息欄可以直接打字（20 字上限和 Big5 檢查都照 `Setup.SetMessage`）。 | `NativeUi/PersonalShopPanel.cs` |
| `--classic-ui` | WinForms 過渡頁（NumericUpDown 單價／數量＋「放入選取格」）保留沒動。 | — |

字串原文：本機 `Text.pak` 的 `string-c.tbl`（用 #852 留在 scratch 的解出檔讀，3484 筆），讀到的編號有 898、900、904、876、932、933、934、935、936、938、940。執行時照表顯示；讀不到表時（例如 CI）改用內建的同一段原文。

**另外改了既有測試 `PersonalShopTests.cs` 三行**（不在可改清單內，但不改會紅燈）：原本的期望值是舊的「兩行直接相接」，改成 `0xFF` 分隔後要跟著更新：hex `41424300`→`4142FF43`、`A4A4A4E500`→`A4A4FFA4E500`、`Take(45)`→`Take(46)`。三行都只是改期望值，測試的邏輯沒動。

## 怎麼驗證

- 編譯：`dotnet msbuild engine/source/OfflineClient.csproj -t:ResolveReferences;CoreCompile -p:Configuration=Release`。這次改的檔案沒有錯誤或警告，唯一的錯誤是 `WebPanel.cs` 的 WebView2（本機 NETSDK1130，和 #848 是同一個環境問題）。`rebuild.py --build --configfile .github/nuget.ci.config` 也卡在同一個 NETSDK1130。完整建置以 CI 為準；沒有改 `rebuild-lock.json`。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：pass（4337 個追蹤檔，0 錯誤）。
- 新的離線測試 `PersonalShopFlowTests.cs`（`[WorldModuleTest("personal-shop-flow")]`）涵蓋：
  - 0xFF／0x00 組裝、40 byte 截斷、外型不影響封包；
  - 898／933／900 的字串；
  - 沒選背包時不開提示；
  - 938、900、936 的錯誤路徑；
  - 堆疊時問 933、Enter＝全部、超過上限重問；
  - 單件物品不問數量；收購數量上限＝金幣 ÷ 單價；
  - 940、932；點有物品的格子只選取；
  - Esc 只取消提示、關窗時連提示一起取消；
  - 雙擊移除；訊息欄打字；
  - OK 時的 876（303／70）、904，通過檢查後送出的 bytes；
  - 提示面板的註冊、跟著控制器的提示行、沒有 ChatPanel 時不畫；
  - 換世代時取消提示並把鍵盤還給遊戲。
- 依本機限制，**沒有在本機執行任何測試**；測試結果看 PR 的 CI。
- PR #872 CI（run 42879369538／42879369584）：`dotnet`、`map-continuity`、`npc-offline` 全綠。
  - `personal-shop-flow`：pass（50 checks），`personal-shop`：pass（91），`personal-shop-integration`：pass（40）；所有模組的 failures 都是空的。
  - 第 1 輪 CI 抓到一個編譯錯誤：`ShopTradeSource.Holding` 應該是 `ShopTrade.Holding`。本機 CoreCompile 被 WebView2 的錯誤擋住，沒報出來。
  - 第 2 輪抓到一項測試錯誤：測試用的物品沒做成裝備類型。兩個都已修正。
- 已同步 origin/main（含 R9-C #847 的 `/shop`，它呼叫 `personalShop.Open()`／`Panel.Close()`，和這次的改動相容）；同步後的 CI 看這個 PR 最新的 commit。

## 未驗證／推測

- **推測**：
  - 「堆疊」用背包數量 > 1 判斷（沒有可靠的堆疊旗標）。所以只拿 1 個的可堆疊物品（例如 1 支箭）放進收購清單時不會問數量。
  - 收購數量上限＝持有金幣 ÷ 單價；不知道金幣時上限是 2147483647。
  - 輸入錯誤後提示留著讓使用者重打。
  - 900 的 `%d` 填 10000000；904 的上限 2,000,000,000（BIN 傳給 `%d` 的值沒查）。
  - 提示的字色是白色，字的位置照聊天輸入文字。
- **待真機**：
  - 0xFF 分隔在 3.81 畫面上怎麼顯示（M38）；
  - 提示在聊天列的實際樣子；
  - 雙擊時機。
- **沒做**：
  - 原版「已開店時 OK 只關窗、Cancel＝關店」：要靠自己的動作 70 才知道真的開店了，引擎沒有這個狀態（`OpenSent` 只代表「已送出」）。
  - 3425、3405、334、927、4393：需要伺服器狀態或沒查清。
  - 1181、1188、941、1497、937：要物品屬性，交給伺服器判斷。
  - 跨窗拖曳。
  - `/shop` 開窗入口：屬於 R9-C。
- 沒有宣稱缺任何資源，所以這次沒有查 pak。提示面板沒有底圖，靠聊天列本身的底圖，也沒畫替代色塊。

## 產出路徑

- `engine/source/PrivateShop.cs`、`engine/source/NativeUi/PersonalShopPanel.cs`、`engine/source/PersonalShopIntegration.cs`
- `engine/source/PersonalShopFlowTests.cs`（新）、`engine/source/PersonalShopTests.cs`（只改 3 行期望值）
- 本報告 `docs/reports/issue-854.md`

## 給下一棒的提示

- 真機（Codex）：
  - 開店設定窗 → 背包選物 → 點空格 → 聊天列應出現「請設定1件物品的價格。」→ 輸入數字 Enter → 堆疊物品出現 933 → Enter → 格子出現圖示；
  - 雙擊格子會移除；
  - OK 後看別的角色怎麼顯示兩行訊息（M38，0xFF）。
- 如果之後要做「已開店」狀態（自己的動作 70 → 902），把 `PersonalShopController.OpenSent` 換成真正的狀態，再補上 OK 只關窗、Cancel＝關店。
- 跨窗拖曳做好以後，在 `PersonalShopController.BeginPrompt` 加一個「拖進來」的入口就行，後面的 898／933 流程直接沿用。
