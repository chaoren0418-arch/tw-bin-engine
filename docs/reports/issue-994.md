# Issue #994 T436：CU-4 故障備援一致（G16–G19）

依據：`docs/reference/classic-ui-audit.md` §3 D 組（G16–G19）與 §4 CU-4。階段 code，第 1 次嘗試。分支 `task/994`。

## 做了什麼

| 項目 | 檔案 | 改動 |
|---|---|---|
| 共用規則 | `NativeUi/UiClientBridge.cs` | 新增 `NativePanelUsable(nativeActive, root, id)`（以及實例版 `NativePanelActive(id)`）：原生 UI 開著、面板已註冊、而且沒有故障，才由原生窗接手；其他情況一律交給 WinForms 備援。條件和 `QuitFallbackUi.NativeQuitWindowActive` 一樣。 |
| G16 召喚 | `SummonFallbackUi.cs`、`PetUi.cs`（1 行，見下方「範圍外」） | 新增 `NativeSummonActive`。`SyncSummonFallback` 和寵物分頁的 `native` 都改用它。原生 UI 開著、但 SummonMonster_Window 故障時，暫行清單會出現；因為這時 HUD 是隱藏的，自動跳頁改從開發工具開「寵物」分頁（`SummonFallback.RevealInDevTools`）。classic 仍照舊走 `ShowPlayerService`。 |
| G16 料理書 | `CookBookFallbackUi.cs` | `Wanted` 的第一個參數改傳 `NativeCookBookActive`（面板已註冊且沒故障）。 |
| G16 血盟倉庫紀錄 | `ClanWarehouseHistoryUi.cs` | 交給原生窗前多查一次「面板沒有故障」；故障時改開 WinForms 暫行清單。 |
| G17 倉庫 | `NativeUi/UiClientBridge.cs` | `NativeWarehouseActive` 拆成可以測試的靜態版本。除了已註冊、沒故障，還要求這一次的清單沒被 UiRoot 關掉（`WarehouseDismissed`：面板已顯示過目前的 interaction revision，但現在是關著的）。這種情況發生在 4 窗堆疊擠掉最舊的窗、地圖拒絕開窗、全部關閉時：交易流程還開著，畫面上卻什麼都沒有，所以要退回 WinForms 清單。做法比照 `TradeUi.cs:53` 的 `!Dismissed`。新清單在面板還沒 Update 之前仍算原生，預設行為不變。使用者按取消或 X 時本來就會送 `CancelWarehouse`，interaction 跟著關閉，也不受影響。 |
| G18 登入／選角 | `CharacterManagementUi.cs` | 新增 `NativeLoginScreenMissing(state, loginUsable, selectUsable)`，依登入狀態判斷需要哪一個畫面：角色清單之前要 LoginUI；角色清單、角色操作、換角要 SelectUI；進入世界中或在世界中都不需要。另新增 `ShouldRevealNativeLoginFallback`，偵測到的瞬間觸發一次。原生 UI 開著、但當下需要的畫面不能用（不存在、`Available=false` 或故障）時，會自動打開開發工具的「登入／選角」分頁一次，並記一筆 event `native-login-fallback`。只在正常入口、非自動化時啟用（沿用 G2 的 `classicLoginEntryEnabled`／`classicLoginEntryArmed`）。`--classic-ui` 仍由 G2 處理。 |
| G19 信件 | `MailUi.cs` | classic HUD 的「信件」鈕改呼叫 `RequestMail()`，和原生選單鈕 1 走同一條排隊入口。 |
| 測試 | 新檔 `FallbackConsistencyTests.cs` | `[WorldModuleTest("fallback-consistency")]`，自動發現。內容：G16 三個面板 × 五種情況（正常、classic、沒有 root、沒註冊、故障）；召喚和料理書的備援條件；G17 的 `WarehouseDismissed` 四種情況與 `NativeWarehouseActive` 五種情況；G18 各個登入狀態與觸發條件；G19 的 `RequestMail` 入口。只用合成面板或未載入的面板，不讀客戶端。 |

### 範圍外改動（需審查者確認）
- `engine/source/PetUi.cs:109`：`bool native=NativeUiActive` 改成 `NativeSummonActive`，只改這 1 行。原因：寵物分頁靠這個值決定要不要畫暫行召喚清單（`:133 if(!native)AddSummonFallback`）。不改的話，G16 的召喚部分沒辦法做到。audit §3 G16 只列了 `SummonFallbackUi.cs:67,70`，漏了這個呼叫點。原生 UI 正常時，`NativeSummonActive` 和 `NativeUiActive` 的結果相同（面板已註冊且沒故障），所以預設行為不變。

沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

## 怎麼驗證
- 已驗證：`dotnet build engine/source/OfflineClient.csproj -c Release` 成功，0 個錯誤；3 個警告都在既有檔案 `HtmlActionTests.cs`、`MagicUi.cs`，與本次改動無關。worktree 沒有 `engine/vendor`，所以 restore 用 `-p:RestoreSources=<原專案>\engine\vendor`（唯讀）。
- 已驗證：`python -B -X utf8 engine/tools/check-no-game-assets.py` 回報 `status: pass`，errors 0。
- 待 CI：依本機限制，不在本機執行 `--module-tests`。既有單元測試和 `fallback-consistency` 由 PR 的 GitHub Actions 驗證。
- 資源：本任務沒有宣稱任何資源「沒有」，也沒有新增圖或字串，所以沒有用 PakViewer 查詢。

## 未驗證什麼（待驗證，交給 Codex／dot 真機）
1. G16：原生 UI 開著時，讓 SummonMonster_Window、COOK_BOOK_WINDOW、血盟倉庫紀錄面板故障（例如記錄裡出現 `panel … 失敗，已停用`）。之後收到召喚請求、S250 52、S250/117 時，WinForms 備援要出現；召喚會打開開發工具「服務 › 寵物」。
2. G17：倉庫清單開著時，連開 4 個以上的窗，讓 WAREHOUSE_WINDOW 被擠掉。NPC 分頁的 WinForms 存入／取出清單要出現，並且能完成交易或取消。
3. G18：在沒有參考客戶端 LoginUI／SelectUI XML 的環境下用原生 UI 啟動，開發工具「登入／選角」分頁要自動打開一次；使用者關掉後，同一段狀態不再打開。
4. G19：`--classic-ui` 時按 HUD「信件」，MailDialog 照常開啟，連點不會開出兩個。
5. 一般原生 UI（面板都正常）時，上面四項的畫面都和合併前一樣。

## 產出路徑
- 程式：`engine/source/{SummonFallbackUi,CookBookFallbackUi,ClanWarehouseHistoryUi,CharacterManagementUi,MailUi,PetUi}.cs`、`engine/source/NativeUi/UiClientBridge.cs`
- 測試：`engine/source/FallbackConsistencyTests.cs`
- 報告：`docs/reports/issue-994.md`

## 給下一棒的提示
- 之後新增「原生面板加 WinForms 備援」的模組時，請直接用 `ClientForm.NativePanelActive(id)`（或靜態的 `NativePanelUsable`），不要再只看 `NativeUiActive`。
- 面板若有自己的「讀不到 XML」狀態（`Available`／`Unavailable`），要和 `NativePanelActive` 一起判斷，像 `MailUi.NativeMailPanel` 與 G18 的寫法。
- G18 目前的出路是自動開開發工具的分頁，沒有在原生畫布上畫提示。若之後要畫，需要先查 Text.pak 有沒有合適的字串；本次沒有新增任何字串。
- audit §3 G16 的檔案清單漏了 `PetUi.cs:109`。下一輪 gap-matrix 或 audit 更新時可以順手補上。
