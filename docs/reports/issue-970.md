# Issue #970 — T426：Q8-C 小項（470 圖示、信件提示、公告板等待、ESC 順序）

依據：`docs/reference/followups-15.md` §2.1、§2.7、§2.9、§2.11、§3（T402／#917）。分支 `task/970`。

## 做了什麼

| 子項 | 檔案 | 內容 |
|---|---|---|
| ① 470 圖示（§2.9） | `NativeUi/BuffIconsPanel.cs` | 新增 `BuffIconsPanel.IconFor(def, entry, vipFlag)`：effect 470 不看時間和模式（不走 `sub_902840`），`vipFlag≠0` 畫 `icon`，`vipFlag=0` 畫 `offIcon`（表裡沒有 offIcon 時畫 icon，不找替代圖）。其他 effect 的規則不變。`Update` 改用這個函式。接 3.81 時 `vipFlag` 一直是 0，所以 470 會畫 offIcon（FU15 的推測）。 |
| ② 信件結果印到聊天欄（§2.7） | `NativeUi/MailPanel.cs`、`MailUi.cs` | 新增 `MailNoticeRelay`：`MailView.Notice` 變了就印一次。換了身分（stamp）時只記基準、不印。面板自己的請求還在送的時候，那段期間變動的 notice（送出確認、本機檢查訊息）不印，所以印出來的主要是伺服器回包的結果。`IMailPanelHost` 加了預設方法 `ChatNotice`；`NativeMailHost` 用 `AddBoardChatNotice(text, 24)` 印在聊天欄（palette[24]，同 `sub_7DC850`）。窗沒開也照樣印。`--classic-ui`／WinForms `MailDialog` 不變，仍然顯示在窗內。 |
| ③ 公告板等待（§2.11） | `NativeUi/BoardPanels.cs`、`BoardUi.cs` | 新增 `BoardPanelSet.IsWaiting`／`Waiting`，對應 `[this+0x40]`：引擎的 `list-wait`、`read-wait` 狀態。等待期間：清單按鈕窗的 662 Write 不動作；原生 host 的 `SelectRow`（清單點選）和 `Press("read"/"write")` 也不動作。 |
| ④ ESC 先給 Y/N（§2.1，照主控裁定做） | `NativeUi/UiInput.cs` | `Key` 加 `PromptEscape`：ESC 按下時，如果 `YesNoPanel` 可見又可用，就先交給它（拒絕），才輪到有焦點的面板。其他按鍵、或沒有提示時，順序跟以前一樣。 |
| 測試 | 新檔 `engine/source/Q8FollowupTests.cs` | 4 組 `[WorldModuleTest]`（自動發現）：`q8-vip470-icon`、`q8-mail-notice-chat`、`q8-board-waiting`、`q8-esc-yesno-first`。 |

沒改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。沒有新增面板，也沒有新增字串。

## 怎麼驗證

- **已驗證**：`py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config --output scratch/q8-rebuild` → product／launcher restore 和 build 都是 exit 0，`0 個錯誤`。3 個警告都在既有檔案（`MagicUi.cs`、`HtmlActionTests.cs`），不是這次改的。
- **已驗證**：`engine/tools/check-no-game-assets.py` → `status: pass`，errors 0。
- **待 CI**：單元測試（`--module-tests`，包含新的 4 組和既有的 `status-icon-followup2-vip470`、`native-ui-stack-esc`、board 和 mail 相關測試）交給 GitHub Actions 跑。依本機限制，本機沒有執行任何編譯出來的程式。
- 資源：這次不畫新圖，也沒有宣稱任何資源「沒有」，所以不需要查 PakViewer。

## 未驗證什麼／推測

- ② 原版印的是字串 1239／1240／1241–1244／428 等編號。引擎的 `MailView.Notice` 是一整句中文（`Mail.cs` 不在可改範圍），所以照任務說的「用引擎已解析的結果、不新增字串」。用「請求還在送就不印」來分辨送出確認和伺服器結果，這是**推測／引擎決定**：如果伺服器回包剛好在多步驟請求（勾選多封一起刪）的某一步還在送的時候到，那則結果不會印。顏色只用 palette[24]，第二色 palette[14] 沒有接（`AddBoardChatNotice` 只收一個顏色）。
- ③ 清單內容面板「右鍵雙擊關窗」在等待期間仍然有效：它和 658 X 走同一個 `Press("close")`，要分開得改 `NativeUi/BoardPanel.cs`，這個檔案不在可改範圍。原版 `sub_7CD490` 設的 4（op 222／6）在引擎裡沒有對應的等待狀態（寫文／刪文後是 `submitted`），所以不算在等待內。WinForms 公告板（`--classic-ui`）不變：本來就靠 `CanAct`／`busy` 停用按鈕。
- ④ 原版第 2、3 步（`dword_153F748`、舊式堆疊窗的 vtable +40）在聊天列之前，它們吃不吃 ESC 沒有追；引擎沒有對應的窗。
- 真機：470 的 offIcon 實際畫面、聊天欄的信件結果行、公告板等待期間的點擊、Y/N 加上焦點窗時的 ESC，都**待真機驗證**（交給 Codex／dot）。

## 產出路徑

- 程式：`engine/source/NativeUi/{BuffIconsPanel,MailPanel,BoardPanels,UiInput}.cs`、`engine/source/{MailUi,BoardUi}.cs`
- 測試：`engine/source/Q8FollowupTests.cs`
- 報告：`docs/reports/issue-970.md`

## 給下一棒的提示

- 如果 `Mail.cs` 以後改成記錄字串編號（1239 等），`MailNoticeRelay` 可以直接改用編號判斷要不要印，不用再靠「請求還在送」來分辨。
- 要讓公告板清單在等待期間連右鍵雙擊都吃掉，在 `BoardListContent.Mouse` 開頭加 `if(set.Buttons.Waiting)return inside;`（需要開放 `BoardPanel.cs`）。
- 在 worktree 裡直接 `dotnet build source/OfflineClient.csproj` 會報 NETSDK1130（WebView2 的 `Core.winmd`；`Microsoft.Web.WebView2 1.0.4258.31` 的 .NET 版放在 `lib_manual`，原因沒追）。worktree 也沒有 `engine/vendor`，所以預設的 `NuGet.Config` 不能用。用 `rebuild.py --build --configfile .github/nuget.ci.config --output scratch/<dir>` 就能建置成功（它用自己的獨立環境）。
