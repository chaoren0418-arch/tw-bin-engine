# Issue #513 T207：M7-E 數量輸入詢問（S136 → NPC 對話面板 HTML＋數字框 → C11）

## 做了什麼
依主控 2026-11-08 的改寫（`windows-2.md` §5.3、W2-D）：S136 **不是** `InputWindow`，改成在 NPC 對話面板顯示 HTML 頁加數字框，回 C11。**沒有另做 WinForms Form，也沒有建 `AmountPromptUi.cs`**。

- 新檔 `engine/source/AmountPrompt.cs`
  - **協定**（以 3.81 伺服器為準，SP `S_HowManyMake`／`S_ItemCount`／`S_ApplyAuction`／`S_SellHouse`／`C_Amount`）：
    - S136：`C(136) D(objId) D(0) D(初始值) D(下限) D(上限) H(0) S(html) S(cmd) [H(n) S×n]`。附加清單只有在剩餘位元組比補零長度多時才讀，`n` 必須在 1–256 之間；最後用 `Finish()` 驗證補零。
    - C11：`C(11) D(objId) D(amount) C(0) S(cmd)`。`cmd` 就是 S136 的第二個字串，例如 `agapply 3`，也就是原服 `UiOperationContext.take` 比對用的命令。
  - **接收**：透過 M7-A 的 `[WorldReceive(136)]` 掛上，沒改 `WorldSession.cs`／`Program.cs`。
    - 物件 id 等於目前對話中的 NPC 時，直接沿用該對話。
    - 沒有對話、但物件 id 是目前選取且在附近的目標時（例如公告板的 `agapply`），先開一個對話脈絡。
    - 其他物件送來的 S136 一律忽略並計數。
    - html 是空字串時關閉對話（照原版）。
    - 其他情況：以既有的 `ShowPage` 顯示該 HTML 頁（模式 0，附加字串當作 `#0`、`#1`… 參數），並記下這一份數量詢問。
  - **有效範圍**：詢問綁定 `InteractionRevision`。換頁、關閉、目標離開或世界換代都會讓它失效。原服不收 ≤ 0 的數量，所以可送出的下限是 `max(1, 下限)`，初始值會被夾進範圍內。上限 < 1 的詢問照樣顯示，但不能送出。
  - **送出**：`LoginConnection.SendAmountReply(view, value)`。
    - 只在快照仍然有效、數值在範圍內時才送一次 C11。
    - 送出後清掉詢問，NPC 對話回到 `waiting` 等原服回包，沿用既有的 15 秒逾時提示，不會自動重送。
    - 傳送例外時斷線，與其他模組相同。
  - **快照與重設**：快照放在 `extensions.amountPrompt`，內容有收到／忽略／送出的次數、最後一份解碼結果、目前的詢問。`[WorldReset]` 會清空。
  - **畫面**：在既有 NPC 對話面板（`NpcUi.cs` 的 `npcPanel`）底部加一列，內容是範圍文字、`NumericUpDown`、「確定」鈕，按 Enter 也可以送出。
    - 這一列位在頁面內容下方、提示文字上方。HTML 頁文字照舊由 `InteractionPage` 顯示。
    - 掛法比照 `ClanWar.cs`／`Marriage.cs`：在 partial `ClientForm` 覆寫還沒被其他模組使用的 `OnSizeChanged`，在 `Shown` 時建立這一列，同步則掛在既有的 16 ms `timer.Tick`。**沒改 `NpcUi.cs`／`NpcInteraction.cs`。**
- 新檔 `engine/source/AmountPromptTests.cs`：`[WorldModuleTest("amount-prompt")]` 共 70 項，只用合成封包與記憶體內的假傳送端。CI 既有的「World module extension tests」步驟會自動跑到，沒改 workflow。
- `engine/_integration/dev/rebuild-lock.json`：以 `--update-lock` 重產，共 244 個來源、253 個鎖定檔。

## 怎麼驗證（本機 Windows）
- `rebuild.py --update-lock` → `--build`：PASS。worktree 沒有 `engine/vendor`，所以用 `--configfile scratch/issue-513/NuGet.Config` 指向原專案的 vendor（唯讀）。productDll SHA-256 `45ca1acff7987adec2d1c313c1cd7f2c8039f53ae0506e472ddb3f73ca312d0f（與 main 43ec1e97 同步後）`。
- `P11.ClientEngine.exe --module-tests`：結束碼 0。7 組共 417 項全部通過（含 `amount-prompt` 70 項、`agit` 44 項、`castle` 62 項、`packetbox-status` 86 項），Default 登錄表沒有錯誤。
- `amount-prompt` 測試涵蓋：
  - 註冊項目。
  - C11 逐 byte 編碼；拒絕數量 0、空命令、控制字元、過長命令。
  - S136 三種伺服器建構法的解碼，含附加字串與 CP950 中文、8 種補零長度。
  - 格式錯誤會丟 `ProtocolException`：opcode 錯、截斷、補零非零、多一個零區塊、筆數比字串多。
  - 對話中收到 S136 時開出 dialog 頁並帶範圍，且不計入 unhandled。
  - 範圍外不送；範圍內只送一次，內容正確；送出後詢問清掉並回到 waiting，舊快照不能重送。
  - 新的 S136 會取代舊詢問；關閉對話後不能送。
  - 非目標的 S136 被忽略；目標的 `agapply` 會開脈絡，範圍 100,000～2,000,000,000，界外不送。
  - 上限 0 的詢問不能送；空 html 會關窗；Reset 會清空。
- 既有回歸全部 exit 0／pass：
  - DemoChecks：`hypertext`、`item-transfer`、`doors`、`buddies`、`pickup`、`combat-admission`、`item-status`、`polymorph-cadence`、`afflictions`、`resurrection`、`map-continuity`、`native-ui-resources`。
  - 另有 `MailChecks`（119 項）、`WeaponRepairChecks`（98 項）。
- `check-no-game-assets.py`：pass，0 錯誤。

## 沒驗什麼
- **畫面沒有實際看過**：數字框列只有編譯通過，沒有開視窗、沒有截圖，也沒做真機驗收（沒啟動遊戲、伺服器或 DB）。要等 M7 smoke 由 dot 在 P24Lab 實測，例如找製作 NPC 的「想要製作多少個呢？」頁。
- **WebView2 面板**（`engine/web/interaction.js`）會照舊顯示 HTML 頁文字，但**沒有數字框**。web 檔不在本任務範圍。
- 原版數字框在 HYPER_TEXT_WINDOW 裡的位置與外觀**待查證**，要等 T190 原生版面；目前是 WinForms 面板底部的一列。
- 推測／待驗證：
  - S136 第 2 個 D 與 H(0) 的意義。3.81 一律送 0，本引擎只保存、不使用。
  - C11 中間那個 byte 原版送什麼。原服忽略，本引擎送 0。
  - TW16 `dddddcsssh` 後段與 3.81 的對應。依規定以 3.81 為準。
  - `agapply` 原版交給 `CAgitAuctionUI`，但 XML 缺；本引擎照一般 HTML 頁加數字框處理。
- 遠端 CI 結果以 PR 頁面為準；本機沒跑 Linux job、sound／combat-sfx／player-chat／web-resource 群組。

## 產出路徑
- `engine/source/AmountPrompt.cs`、`engine/source/AmountPromptTests.cs`、`engine/_integration/dev/rebuild-lock.json`、本報告。分支 `task/513`。

## 給下一棒的提示
- **M7 smoke（dot）**：
  1. 在製作 NPC 點製作選項，應出現 `request` 頁「想要製作多少個呢？」，面板底部有數字框。
  2. 輸入 1 按「確定」，封包追蹤應看到一個 C11（opcode 11）。
  3. 拍賣公告板的 `agapply` 也走同一條路。
- **web 面板**：要在 `interaction.js` 加數字框時，資料可以取 `LoginConnection.AmountPromptSnapshot()`（`Prompt.Lower`／`Max`／`Suggested`／`Answerable`、`CanSend`），送出呼叫 `SendAmountReply(view, value)`。
- **T190／原生 HYPER_TEXT_WINDOW**：做原生版面時，把 `AmountPrompt.cs` 底部的 `ClientForm` 區段改畫進原生窗即可，協定與狀態層不用動。
- `ClientForm` 已被各模組使用的生命週期覆寫：`OnLoad`、`OnShown`、`OnVisibleChanged`、`OnCreateControl`、`OnHandleCreated`、`OnLayout`、`OnActivated`、`OnResize`，本任務再用掉 `OnSizeChanged`。後續自我掛載的模組要另外挑一個。
- 本機建置照 #512 的提示：用 `py -3.12`；worktree 要自備指向原專案 vendor 的 NuGet.Config；DemoChecks 先單獨 restore，再 `dotnet run --no-restore`。
