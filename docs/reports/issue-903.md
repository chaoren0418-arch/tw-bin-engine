# Issue #903 報告：T388 R10-D 組隊邀請點選

依據：`docs/reference/gap-matrix.md` 15.12.6（R10-D）與 15.12.5 的 3:C_INVITE_PARTY(82) 列；`docs/reference/followups-9.md` §9.1；`docs/reports/issue-847.md`「給下一棒」第 1 點。

## 做了什麼

| 項目 | 內容 | 檔案 |
|---|---|---|
| `/invite` 點選 | 選目標模式（`World.PartyInvitePicking`）中，左鍵點物件（`Select`）或攻擊路徑（雙擊／Ctrl／直接攻擊，`ActAsync(id,false)`）改呼叫 `LoginConnection.PickPartyInviteTarget(id)`：30 格內的有效玩家才送 **C230 type 0 D id**，否則不送；兩種情況都結束選目標。不改選取目標、不送攻擊。`ActAsync` 這時回 false（UI 不會記成 attack），結果放在 `LastPartyInvitePick`。 | `PlayerPointer.cs` |
| 點地面取消 | 選目標模式中點地面（`MoveTo`）：取消模式、不走路、不送（同 R7-A 施法選目標）。 | `PlayerPointer.cs` |
| 拖人到隊伍窗 | 新增 `WorldObjectDrag`：一般選取選中「看得到、活著、玩家旗標 4、不是自己」的物件時記下來源 id；`PlayerPointerController.Cancel`（每次新的場景點擊或停止都會呼叫）會清掉。`PartyPanel` 收到**不是在本窗按下**的左鍵放開時取走來源，在隊伍分頁、快照 `Available`、`CanInvite` 時呼叫既有 `PartyAction("invite")` → `ClientForm.PartyAction` → `SendParty("invite",…,World.TargetId,0)` → **C230 type 0**。在本窗按下會清掉來源。 | `PlayerPointer.cs`、`NativeUi/PartyPanel.cs` |
| 測試 | `party-invite-pick`（`[WorldModuleTest]` 自動發現）：點人送 C230 00 D(456)＝`E600C80100000000`、太遠／非玩家／自己不送、點地面取消、攻擊路徑也是選對象不攻擊、右鍵交談不走邀請、拖曳來源條件、放開送出、各種不送的情況、SendParty 再擋非玩家。 | `PartyInvitePickTests.cs`（新檔） |

沒改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`；沒有新面板（`--classic-ui` 的 WinForms `PartyUi` 入口不受影響）。這次沒有用到任何圖檔，所以不需要查 pak。

## 怎麼驗證

- 本機編譯：`dotnet build engine/source/OfflineClient.csproj -c Release` 0 錯誤，改動的檔案沒有新警告。本機 SDK 對 WebView2 winmd 會報 NETSDK1130（和這次修改無關，#678／#679／#706 也遇過）：從主專案 `engine/vendor` 唯讀 restore 到 `scratch/nuget-903`，再用只放在 scratch 的 `scratch/task903/nowinmd.targets`（`-p:CustomAfterMicrosoftCommonTargets`）把 winmd 換成 net462 DLL，只為了確認 C# 能編譯。這兩個都沒有入 git。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass（4415 檔，0 錯誤）。
- 依規定本機不執行編譯出來的程式；`--module-tests`（含新的 `party-invite-pick`）與 DemoChecks `native-ui-party` 交給 CI。結果看 PR 的 CI。

## 未驗證什麼（待驗證／推測）

- **真機再確認**：`/invite` 後點人、拖人到隊伍窗，實際送出的封包與伺服器回覆（416／417 等）；拖曳時游標上的人物圖沒畫。
- **推測**：
  - 原版 517 收在哪個窗類別沒有確認；引擎只在原生隊伍窗的**隊伍分頁**收，角色／寵物分頁會吃掉放開但不送。
  - type 1（`byte_13CE519` 為真時）沒接，固定 type 0（同 #847）。
  - 施法選目標與邀請選目標同時開時，先處理施法（原版的先後關係沒查）。
  - 隱身判斷（`sub_A42C30`）沒做，交給 3.81 伺服器。
- 已知限制：如果在場景點了玩家後在場景放開，接著在一個「不處理按下」的面板上按下再拖到隊伍窗放開，舊的拖曳來源還在，會邀請之前選的那個玩家（`SendParty` 仍會檢查對象是玩家、自己是隊長或沒隊伍）。正常操作碰不到。

## 產出路徑

- `engine/source/PlayerPointer.cs`
- `engine/source/NativeUi/PartyPanel.cs`
- `engine/source/PartyInvitePickTests.cs`
- `docs/reports/issue-903.md`

## 給下一棒的提示

- R10-E（動作窗橋接）的 `Action_InviteParty` 可以走同一個入口：沒有名稱時呼叫 `World.BeginPartyInvitePick()`，之後的點選由 `PlayerPointerController` 處理（本任務已接）。
- 拖曳來源 `WorldObjectDrag` 是靜態、只在 UI 執行緒用；之後要做「拖人進好友／封鎖窗」（FU9§9.2 `FriendDrop`／`ExcludeDrop`）可以共用：在面板收到「不是自己按下的放開」時 `WorldObjectDrag.Take()`。
- 真機驗收時看 `[native-ui]` Trace 與送出的 C230：`/invite` → 點 30 格內玩家應送 `E6 00 <id LE>`；拖人到隊伍分頁放開也一樣。
