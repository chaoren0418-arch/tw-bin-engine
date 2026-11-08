# Issue #615：T254 R2-D 幽靈、死亡窗與離開窗（code，第 1 次）

依據：`docs/reference/gap-matrix.md` 第 11.5 節 R2-D；WO 第 4、6 節；PBW§4；gap-matrix 第 4.1 節 `UserPlayStatsUI`、第 4.2 節 `QuitWindow`。
分支：`task/615`（worktree `scratch/wt/task-615`）。

## 做了什麼

| 項目 | 檔案 | 內容 |
|---|---|---|
| `exitghost` 動作 | `GhostMode.cs` | `GhostModePackets.ExitAction="exitghost"`／`IsExitAction`；`LoginConnection.TryHyperAction(action,out sending)`：是 exitghost 就回 true（呼叫端不要再送 C_DIALOG）。只在幽靈中送 C173，不是幽靈時不送。沿用既有 `ExitGhost`，包括防重送、世代檢查和傳送失敗處理 |
| HTML 入口給 R2-F | `OnlineInput.cs` | `ClientForm.HyperAction(string)`：轉呼叫 `TryHyperAction`，送出後把結果寫進 HUD 提示。**沒有接到任何 HTML**，由 R2-F 接（`WebPanel.cs` 的指令白名單不在本組範圍） |
| S85 接 `GhostSkipsSelfAppearance` | `Recovery.cs` | 自己在幽靈中收到 S85：照樣清除死亡狀態，但不換 gfx、不加 `AppearanceRevision`（WO§4 `sub_A43B50`）。另加 `OwnResurrections` 計數，用來關掉 QUIT_WINDOW |
| 死亡統計窗改成 `IUiPanel` | 新檔 `NativeUi/UserPlayStatsPanel.cs`、`Recovery.cs` | Id `UserPlayStatsUI`，版面 `UserPlayUI.xml`：底圖 BaseSurf（5322）＋外框 FrameSurf（**5323**）；重新開始、結束、取消、關閉四個鈕，各有 Base／Highlight／Push 狀態圖；四列統計（圖示、名稱、數值）；Moveable 拖曳條；Esc 等於取消。`UserPlayStatsButton` 補讀 `HighlightSurf`／`PushSurf`，`UserPlayStatsLayout` 補讀視窗 X／Y 與 `Moveable`。原生 UI 啟動後，`SyncRecovery` 接上資料來源並隱藏 WinForms 面板；原生 UI 關閉或面板故障時，照舊用 WinForms |
| 字串 3123–3126 改讀字串表 | `Recovery.cs` `UserPlayStatsText` | 執行期讀 `string-c.tbl`（`ServerMessageText.Table`）；字串表不存在、id 超出筆數或內容空白時，退回 WO§6 的名稱。WinForms 與原生面板用同一套 |
| **PBW-B** QUIT_WINDOW | 新檔 `NativeUi/QuitWindowPanel.cs`、`Recovery.cs` | Id `QUIT_WINDOW`，版面 `uiextra86`：底 1951、關閉 X 1962、重新開始 1953、離開遊戲 1956、取消 1959（STAGE3 狀態圖）；位置是 (400 − 寬/2, 150)。規則在 `QuitWindowRules`：重新開始在死亡時走 C177，存活時走 C7（`SwitchCharacter`），戰鬥中顯示訊息 130；離開遊戲走 C122（新的 `LoginConnection.QuitGame`），送出後關閉程式（原版 `exit(0)`），戰鬥中顯示訊息 131；取消、X、Esc 只關窗 |
| 死亡逾時改開此窗＋訊息 47 | `Recovery.cs` `CheckDeathTimeout` | 逾時後照舊送一次 C122、關閉連線，**再開 QUIT_WINDOW**；狀態列文字以訊息 47 開頭（有字串表就讀表，沒有就用 PBW§4 記錄的「斷線。」）。QUIT_WINDOW 的狀態記在連線物件上，所以斷線後仍然存在；開新連線（世代改變）或自己收到 S85 時會關掉 |
| 註冊 | `NativeUi/UiPanelRegistry.cs` | 加兩行（UserPlayStatsUI／UserPlayUI、QUIT_WINDOW／uiextra86） |
| 測試 | 新檔 `UserPlayStatsTests.cs` | `[WorldModuleTest("user-play-stats")]`，111 項（見下） |

## 怎麼驗證（全部離線，沒有啟動遊戲、伺服器或 DB）

- `rebuild.py --update-lock` 之後跑 `--build`：`PASS dev`。productDll SHA-256 `951a768e973de8fce3bb9c242427a354e7caf800cc6466c5e1a6d99305fc80c1`。worktree 沒有 `engine/vendor`，所以用 `--configfile scratch/issue-615/NuGet.Config` 指向主專案的 vendor（唯讀）。本組新檔沒有編譯警告。
- `P11.ClientEngine.exe --module-tests <絕對路徑>`：`pass`，28 組、1481 項、失敗 0。其中 user-play-stats 111 項、recovery-death 72 項（既有、未改）、trade-window 79 項、friend-panel 104 項、summon-panel 56 項。
- DemoChecks 全部 `pass`：ghost-mode 721、ghost-limits 362、resurrection 553、pickup 369、native-ui-canvas 429、native-ui-macro 403、native-ui-resources 539、native-ui-hpmp 397、native-ui-left-status 370、native-ui-chat-window 446。
- `engine/tools/check-no-game-assets.py`：`pass`，errors 0。沒有新增 `.img`／`.xml`／`.png`；測試用的 XML 與圖是程式合成的，數字刻意和原檔不同。
- user-play-stats 測了這些：
  - 註冊各一筆。
  - exitghost：名稱比對；非幽靈不送；幽靈送 C173 而且只送一次；等待回覆時不重送。
  - S85：非幽靈會換 gfx；幽靈不換 gfx，但仍清除死亡狀態。
  - `QuitWindowRules`：15 秒戰鬥規則與各種分支；訊息 47／130／131 讀表與退回；3123–3126 讀表、空白、超出筆數與退回。
  - QUIT_WINDOW 狀態：逾時才開，死亡時不開；`InWorld=false`、訊息 47；取消後關閉；新世代隱藏；自己的 S85 關窗；`QuitGame` 只送一次 C122 並斷線。
  - QUIT_WINDOW 面板：原點與 Bounds；STAGE3 狀態圖；在鈕外放開不觸發；重新開始、離開、取消、X、Esc；同一 revision 關了不再開；缺元件或缺座標時丟例外；缺圖退回底圖；畫素位置正確，外框不畫。
  - UserPlayStats 面板：置中；Bounds；列名讀表；列的矩形；四個鈕的狀態圖與觸發；重新開始不可用時不觸發；Esc；拖曳條；缺節點時丟例外；畫素位置正確。

## 已驗證／推測／待驗證

**已驗證**（有文件或 XML 根據，引擎照做）：
- 送出：C173 無欄位、只在幽靈時送（WO§4）。
- S85 在幽靈中略過自身外觀（WO§4）。
- QUIT_WINDOW：元件、座標、(400 − 寬/2, 150)、三個鈕的封包與訊息 130／131、逾時送 C_QUIT 後開窗與訊息 47、S_RESURRECT 關窗（PBW§4）。
- UserPlayUI.xml 的元件與座標（本機唯讀抽查 `scratch/task448-uixml/UserPlayUI.xml`、`uiextra86-c.xml` 的結構，沒有複製進 repo）。

**推測**（程式註解與檔頭都有標明）：
- 按鈕狀態圖的用法：UserPlayUI 是 Base＝平常、Highlight＝停留、Push＝按下。QUIT_WINDOW 的 STAGE3 用 n／n+1（按下）／n+2（停留），根據是 UserPlayUI 對同一組 surf 1953–1955 的用法。觸發時機是按下後在鈕上放開。
- QUIT_WINDOW 的外框 1952 在 XML 沒有座標，照 TRADE_WINDOW 的做法**不畫**；「寬」取已畫元件的右緣。
- UserPlayStatsUI 的畫面位置：XML 是 0,0，沿用 QUIT_WINDOW 的置中規則、y＝150；外框疊在底圖上、畫在同一原點；`Moveable="20"` 當作頂端 20 px 可拖曳；Esc 當作取消。
- 引擎沒有原版的「上次戰鬥時間」，15 秒戰鬥限制在引擎一律放行，交給伺服器判定。只有死亡逾時會開 QUIT_WINDOW，那時一定是死亡狀態，所以實際不會走到存活的分支。
- 逾時後連線已經關了：QUIT_WINDOW 的「重新開始」送不了 C177，引擎改走既有的登出、回登入畫面（**引擎過渡**，不是原版行為）。「離開遊戲」直接關閉程式。

**待驗證／未做**：
- **統計數值（Q-2）**：`sub_C0AB50` 沒查，數值一律顯示「—」，不顯示合成的 0。3123–3126 是不是就在 `string-c.tbl`，也要等 Q-2 確認；目前是有讀到就用。
- 沒有接 HTML：`exitghost` 由 R2-F 接到 NPC 對話頁。
- 沒有開窗入口：QUIT_WINDOW 由 ESC 或選單開啟（PBW§4 待驗證），所以沒接。
- 沒做的元件：QUIT_WINDOW 的拖曳；UserPlayStatsUI 的 NewItemList 鈕、Reset 鈕、地監時間清單；統計數值的字型與對齊細節。
- 原生 UI 預設是關閉的。關閉時，逾時後只有狀態列顯示訊息 47，沒有 QUIT_WINDOW（舊 HUD 沒有對應的窗）。
- 斷線後原生畫布是否仍然顯示 QUIT_WINDOW、真實 surf 渲染與座標目視比對：都沒有真機或真實資源驗證。設計 §5.2 的本機選用渲染檢查本輪沒做。

## 產出路徑

- `engine/source/GhostMode.cs`、`engine/source/Recovery.cs`、`engine/source/OnlineInput.cs`
- `engine/source/NativeUi/UserPlayStatsPanel.cs`（新）、`engine/source/NativeUi/QuitWindowPanel.cs`（新）、`engine/source/NativeUi/UiPanelRegistry.cs`（加兩行）
- `engine/source/UserPlayStatsTests.cs`（新）
- `engine/_integration/dev/rebuild-lock.json`（`--update-lock` 重產）
- 本報告 `docs/reports/issue-615.md`

## 給下一棒的提示

- **R2-F**：NPC 對話頁的 HyperText 動作先呼叫 `ClientForm.HyperAction(action)`（或 `LoginConnection.TryHyperAction`）。回 true 就不要再送 C_DIALOG／C125。目前只處理 `exitghost`，名稱照原文比對、區分大小寫。
- **R2-I**：S76／變身的自身外觀，比照 S85 呼叫 `GhostSkipsSelfAppearance`。
- **Q-2** 查到 `sub_C0AB50` 的統計來源後，在 `UserPlayStatsView` 加數值欄位，並改 `UserPlayStatsPanel.Paint` 的「—」。如果查出 3123–3126 不在 `string-c.tbl`，就改 `UserPlayStatsText.Label`。
- 如果查到 QUIT_WINDOW 由 ESC 或選單開啟：呼叫 `LoginConnection.OpenQuitWindow(message,true)`。存活分支的 C7 與 15 秒規則已經在 `QuitWindowRules`；要做到原版行為，還要補一個「上次戰鬥時間」的來源。
- 真機 smoke（dot）時要看：原生 UI 開著，死亡 → UserPlayStatsUI 的外框與按鈕狀態圖；等 8 分 21 秒逾時 → QUIT_WINDOW 有沒有在斷線後出現。
