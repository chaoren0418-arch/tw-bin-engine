# Issue #852 — T368：R9-H 好友／封鎖窗事件

依據：`docs/reference/followups-9.md` §9.2（XML 窗事件表）、§16.2 R9-H；`gap-matrix.md` §15.11（Q7-9）。

## 做了什麼

只改 `engine/source/NativeUi/FriendPanel.cs`，新增 `engine/source/NativeUi/FriendWindowTests.cs`。

| 事件（FU9 §9.2，已驗證，靜態） | 引擎做法 |
|---|---|
| FriendUI `FriendName` 4142（Delete，`sub_B18F10`）→ Y/N 1813 → 是 → `cs` 124（＝3.81 C202） | `FriendPanel.Delete()` 開 `PendingDelete`（1813 Y/N）；`AnswerDelete(rev, yes)` 是 → `IFriendWindowHost.Remove(Friend, 名稱)`，否 → 不送 |
| ExcludeUI `ExcludeName` 4142（`sub_B18D30`）→ 直接 `cs` 7（＝3.81 C171，切換） | 不確認，直接 `Remove(Exclude, 名稱)` |
| MailExcludeUI 4142（只在旗標 0x2000 送 `csc` 7；3.81 不支援） | 不送，鍵不攔 |
| FriendUI `FriendName` 515（雙擊，`sub_B19030`）→ `"名稱 ` 放進聊天輸入列 | `FriendPanel.Whisper()`：同 `MapSendEntry` 作法打開原生聊天輸入列、取代草稿、填 `"名稱 `；不送封包，Enter 走既有 ChatPanel → SendChat → 密語 |
| FriendUI 27（Esc，`sub_B191F0`）→ 關窗 | `Key(Esc)` → `Hide()`（三窗都這樣；未開時不攔，交給 R7-H 全域 ESC） |

- **1813 原文**：用 `docs/reference/rea-scripts/t267/text_tables.py`（唯讀解密 `D:\lineage開發\0-客戶端\Text.pak` 的 `string-c.tbl`，sha256 `4ebbc1a7…2d43e9`，3484 列）讀出：`1813 = '要刪除你所選擇的好友嗎?'`（沒有 `%s`）。執行期照 `ServerMessageText.Table`，表不在時用同一句（`FriendPanels.DeleteConfirmFallback`）。
- **1813 提示面板** `FriendDeletePromptPanel`（同檔，`[UiPanel(195)]` 自動發現，Float 層）：畫在聊天輸入列 `YesNoButtons.Line`（192,581,280,12；InputWindow 放提示的位置），白字黑影；`Y`＝是、`N`／`Esc`＝否；伺服器 Y/N（`YesNoPanel`）顯示中時讓位；聊天輸入列開著時不收鍵；不攔滑鼠。
- **送封包介面** `IFriendWindowHost.Remove(kind, name)` 與靜態 `FriendPanels.Host`（同 `FriendPanels.Source` 的接法）。面板不碰 `LoginConnection`。
- 註冊工廠改成帶 `root.Find`（雙擊找聊天面板、提示面板找好友窗）；`FriendPanel` 原建構子簽名保留（新參數皆可省略），`IUiClosablePanel` 不變；`--classic-ui` 不受影響（WinForms `BuddyPanel` 入口照舊）。
- 沒有新增或使用任何圖（只畫文字），因此沒有「缺資源」宣稱。

## 怎麼驗證

- **本機編譯：通過（0 錯誤；本次兩個檔案沒有警告）**。這台機器 restore WebView2 有已知的 NETSDK1130 問題（#817／#846），只為編譯在 `scratch/b852/fix.targets` 用 `-p:CustomAfterMicrosoftCommonTargets` 拿掉 `.winmd`、改參考 net462 DLL；repo 檔案沒動。指令（在 `engine/`）：
  `dotnet build source/OfflineClient.csproj -c Release --no-restore -p:CustomAfterMicrosoftCommonTargets=<scratch>\fix.targets -o ..\scratch\b852\client`
  （restore 由 `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config` 完成，暫時 lock，**沒有改** `rebuild-lock.json`。）
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`（4337 個追蹤檔，0 錯誤）。
- 離線測試 `friend-window`（`[WorldModuleTest]`，由 CI 的 `--module-tests` 執行；本機依規定不執行）：註冊與自動發現、1813 原文與 fallback、密語前綴可被 `PlayerChatProtocol` 解析；Delete → 1813 → N／Esc 不送、Y 送一次 `Remove(Friend,…)`；過期 revision、重複回答、名單變更、關窗、離開世界、沒有宿主、聊天列開著、伺服器 Y/N 顯示中；ExcludeUI 直接 `Remove(Exclude,…)`、MailExcludeUI 不送；三窗 Esc；雙擊（同列才算、不同列只選取、取代草稿、不能發話／不讓開聊天列／沒有 root 時不做）、Enter 經既有 SendChat 送 `"丙 嗨`。
- CI：見 PR 的 CI 結果。

## 未驗證／推測（真機再確認）

- **送封包尚未接到 LoginConnection**：`FriendPanels.Host` 要在 `BuddyUi.cs`（不在本任務可改範圍）設定，在此之前原生窗的 Delete **不詢問、不送**（雙擊、Esc 照常）。
- 1813 Y/N 的確切畫法與回答方式（是否同 InputWindow 輸入列、是否要 Enter、有無 YES／NO 鈕）待真機確認；目前 Y／N 單鍵回答。
- 關窗時未回答的 1813 一併取消（原版行為沒查）。
- ExcludeUI／MailExcludeUI 的 Esc 關窗是依 XML 同構推測（表內只列 FriendUI 27）。
- 舊式 `ExcludeWindow`（vtable `0x117B6A0`，每個選取列送 `cs` 7）在引擎沒有對應面板；本任務只做 XML 的 ExcludeUI。
- 原版 3.81 刪除好友後是否本地移除（TW16 本地移除、不送 56）：`SendBuddy(Delete)` 的本地名單處理不在本任務範圍。
- 開窗鍵（M37）仍待驗證，沿用 `BuddyUi.cs` 過渡入口。

## 產出路徑

- `engine/source/NativeUi/FriendPanel.cs`（修改）
- `engine/source/NativeUi/FriendWindowTests.cs`（新增，測試名稱 `friend-window`）
- `docs/reports/issue-852.md`（本報告）
- 分支 `task/852`

## 給下一棒的提示

1. **接線（code，可改 `BuddyUi.cs`）**：讓 `LoginFriendListSource` 也實作 `IFriendWindowHost`（或另寫轉接），在 `InitBuddyUi()` 設 `FriendPanels.Host=...`。`Remove`：取 `connection.BuddySnapshot()`，Friend 需 `CanSend`、Exclude 需 `CanToggle`，先用 `BuddyPackets.Encode` 驗名稱（`ArgumentException` → false），再 `_ = connection.SendBuddy(BuddyAction.Delete／ToggleExclude, name, view)` 並觀察例外。
2. 真機（Codex／dot）：開好友窗 → 選人 → Delete → 看輸入列 1813 → Y 後伺服器名單更新；封鎖窗 Delete 解除封鎖；雙擊好友 → 聊天列出現 `"名稱 `；Esc 關窗。
3. 本機 WebView2 NETSDK1130 的根因（restore 只解出 net462／winmd）仍需 `needs:local` 處理。
