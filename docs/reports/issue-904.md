# Issue #904 報告：T389 R10-E 動作窗橋接

依據：`docs/reference/gap-matrix.md` 15.12.5（4:BehaviourUI、4:LoginUI）、15.12.6 R10-E 列；裁定 #880 第 2 點（S48 照 TW16 用模態訊息框）。
R10-D（#903，`/invite` → `World.BeginPartyInvitePick`）已合併，本張直接用它的入口。

## 做了什麼

改動只在可改的三個檔案：`engine/source/NativeUi/ActionWindowPanel.cs`、`engine/source/NativeUi/UiClientBridge.cs`，以及新檔 `engine/source/NativeUi/ActionBridgeTests.cs`。沒有改 `rebuild-lock.json`、`Program.cs`、`UiPanelRegistry.cs`、`DemoChecks.csproj`、`WorldSession.cs`。

1. **路線表 `ActionRoutes`**（`ActionWindowPanel.cs`）：本機 `Actions.xml` 的 25 個動作各有一列（`ActionRoute`：種類＋參數＋說明）。

   | 種類 | 動作 | 接到哪裡 |
   |---|---|---|
   | Emote（4） | `Action_SocialAction1`–`4` | `LoginConnection.SendEmote(stamp, 1–4, menu:false)`（動作槽路徑，AN§1.1 的重映射） |
   | Loot（1） | `Action_Loot` | 同 F4：`LootFromInput`（取消點選模式、異常狀態擋下、走路中不送） |
   | ChatCommand（3） | `Action_Trade`、`Action_OpenShop`、`Action_InviteParty` | 當作在聊天列輸入 `/trade`、`/shop`、`/invite` 送出（`SendPlayerChat` → `ChatFollowupCommands`）。`/invite` 不帶名稱就是 R10-D 的入口（`BeginPartyInvitePick`，之後點玩家送 C230） |
   | ChatLine（3） | `Action_InviteChatParty`、`Action_AddFriend`、`Action_Exclude` | 把 `/chatinvite `、`/addbuddy `、`/exclude ` 放進原生聊天輸入列，等使用者打名稱後 Enter（過渡做法） |
   | Toggle（2） | `Action_LootAll`、`Action_HPMPRecoveryEffect` | `WorldSession.TogglePickupAll()`；`HpMpPanel` 的 `ShowRecovery` |
   | Window（1） | `Action_OpenBookmarkWin` | 原生書籤窗（沿用 R8-D） |
   | None（11） | `ShoutOfSurvival`、`OpenMacroWin`、`OpenPetManagementWin`、`Report`、`ViewDropItemName`、`CorpseTransparent`、`ShowDisableStatusIcon`、`DungeonTimeLimit`、`BrodcastToPledge`、`PotionHelper`、`NumberingMarker` | 引擎沒有對應的功能，或處理函式沒查。不送封包，記一次 log，log 裡附原因（`Note`） |

2. **產品橋接**（`UiClientBridge.cs`）：`NativeUiCommands` 改成 `internal`，並實作 `IUiActionCommands.RunAction`。
   - 接 Emote、Loot、ChatCommand、LootAll 四種。其他回 `false`，交給面板處理。
   - 不在世界中時回 `false`。
   - 在世界中但暫時做不到時，印原因並回 `true`：
     - 聊天輸入列正在等答覆（刪除確認、私商 1028；R10-F 的 `ChatLinePrompts`）時先擋下，不讓 `SendPlayerChat` 把 `/trade` 當成答覆；
     - 聊天列在冷卻中。
   - 每次執行都寫一筆 `native-ui-action` 事件。
3. **面板退路**（`ActionWindowPanel.Run`）：順序是橋接 → 書籤窗 → 聊天列前綴 → HP／MP 回復顯示 → 表情退到開發工具（沒有橋接時）→ Unhandled。
   - 聊天列前綴和 `FriendPanel.Whisper` 的做法相同，會取代原本的草稿。
   - 以下情況不碰聊天列：輸入列送出中、輸入列正在等提示的答覆、聊天列不能開、沒有聊天窗。
   - `ActionRunResult` 新增 `ChatLine`、`Toggled` 兩個值。
4. **常駐開窗鈕 `ActionOpenButtonPanel`**：用 `[UiPanel(192)]` 自動發現，放在 Bar 層，實作 `IUiClosablePanel`。
   - 圖與座標由 `ActionWindowPanel` 從同一份 XML 讀：`OpenButtonBase`、`OpenButtonHighlight`。缺圖的另記在 `OpenButtonMissing`，不進 `Missing`，所以既有測試不受影響。
   - 只在世界中顯示，滑鼠停留時畫 HighlightSurf。
   - 514（放開）與 515（雙擊）都經 `UiRoot.Toggle` 開關動作窗，和 Ctrl+D 同一條路（FU8§4.3 `OpenButtonPressed`）。
   - 全域 ESC 不關這顆鈕；`Close()` 只清掉按住／停留的狀態。
   - 動作窗不能用或缺底圖時，不畫也不收點擊（不用替代圖）。
5. **S48 登入公告**（照裁定）：新增 `ClientForm.LoginNewsRoute(modalBoxAvailable, noticeWindowUsable)`。
   - 產品的 `LoginNewsModalBoxAvailable=true`，所以 S48 一律走既有的模態訊息框 `ShowLoginNewsBox`，對應 TW16 `sub_8E1E30`。
   - kor25 的 `Notice_Window` 只在沒有模態框時才當備援；程式碼保留，產品預設走不到。
   - `--classic-ui` 本來就沒有原生 UI，照舊用訊息框。
6. **離線測試 `ActionBridgeTests.cs`**（`[WorldModuleTest("action-bridge")]`，自動發現）：只用合成版面、合成的 25 項清單與合成 surf。涵蓋：
   - 路線表的數量與各列對應；聊天指令和前綴都能被 `ChatFollowupCommands.Parse` 解析；
   - 面板對 25 個動作各走哪條路；
   - 聊天列前綴的各種擋下條件；
   - HP／MP 切換；
   - 橋接優先；
   - 開窗鈕的註冊、畫法、點擊、雙擊（3 次切換）、放開在外面、右鍵、ESC、離開世界、缺圖；
   - `LoginNewsRoute` 的四種組合與產品預設；
   - `NativeUiCommands` 實作 `IUiActionCommands`。

## 怎麼驗證

- 本機編譯：`py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config` → `PASS dev`，product／launcher 的 restore 和 build 都是 0，本次改的檔案沒有新警告。lock 用暫時的版本，`writes:false`，沒有寫入。
  - 用預設的 `engine/NuGet.Config` 會失敗，因為 worktree 沒有 `vendor`，所以改用 CI 的 NuGet 設定。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `{"status":"pass","errors":0}`。
- 依本機執行限制，**沒有在本機跑任何測試或編出來的程式**。既有單元測試和本組 `action-bridge` 交給 PR 的 GitHub Actions CI。
- 既有測試 `ActionWindowTests` 的斷言都保留，我逐條對過：
  - `OpenButtonSpec` 的值相等、`Missing` 只列 901；
  - Loot 在沒有橋接時是 Unhandled，而且不呼叫任何指令；
  - 沒有書籤窗時書籤動作是 Unhandled；
  - 表情在沒有橋接時退到開發工具。
- 資源查證（PakViewer CLI，唯讀；輸出在 `scratch/issue-904/surf-5156.tsv`，不進 git）：用 `pak list` 查了 `0-客戶端` 全部 17 個 Sprite idx（`Sprite.idx`、`Sprite00`–`Sprite15.idx`）。
  - `5156.img` 在 Sprite12，`5156c.img` 在 Sprite15，`5157.img` 在 Sprite13，`5157c.img` 在 Sprite00，各 1,100 B。
  - 開窗鈕的圖存在，本張沒有宣稱缺任何資源。

## 未驗證什麼（待驗證／推測）

- **真機（M33，交給 Codex／dot）**：
  - 開窗鈕 (774,470) 在 3.81 是否常駐，以及會不會擋到右下角其他 HUD；
  - Ctrl+D 開窗鍵；
  - 動作窗的版面；
  - 雙擊各動作的實際效果：表情播放、F4 拾取、`/trade`、`/shop`、`/invite` 點選、全部拾取的提示。
- **推測**：
  - `Action_HPMPRecoveryEffect` 的 `ShowHPMPRecovery` 對到 `HpMpSettings.ShowRecovery`（OptionUI `HPMPBarRecoveryBtn` 同一個值）。這個值目前在引擎裡沒有畫面效果。
  - ChatLine 類的動作，原版是 control="target"：點選地圖物件、聊天窗或隊伍窗的名稱，或開 `sub_9FBC00` 輸入框。引擎改成放指令前綴到聊天列，是過渡做法。
- **沒做**：
  - 拖到巨集格（選做）；
  - toggle 動作的勾選圖；
  - 開窗鈕提示字串 3118；
  - `Action_InviteParty` 的踢人分支（隊長點已在隊上的人）；
  - 11 個 None 動作（原因寫在 `ActionRoutes` 的 `Note`）；
  - S48 改回 WinForms 訊息框：原版 `sub_8E1E30` 是遊戲內自畫的模態框，引擎用的是系統 MessageBox，外觀不同。

## 產出路徑

- `engine/source/NativeUi/ActionWindowPanel.cs`：`ActionRoutes`、`ActionRouteKind`、`ActionRoute`、面板退路、`ActionOpenButtonPanel`
- `engine/source/NativeUi/UiClientBridge.cs`：`NativeUiCommands : IUiActionCommands`、`LoginNewsRoute`
- `engine/source/NativeUi/ActionBridgeTests.cs`：離線測試 `action-bridge`
- `docs/reports/issue-904.md`：本報告
- 分支：`task/904`

## 給下一棒的提示

- R10-I（選項窗）如果在 `ObjectRendering.cs` 加上地面名稱或透明屍體的開關，在 `ActionRoutes` 把 `Action_ViewDropItemName`、`Action_CorpseTransparent` 改成 `Toggle`，再到 `NativeUiCommands.RunAction` 接上即可。
- 要做 control="target" 的點選對象：走 `PlayerPointer` 的選目標狀態（R10-D 的 `PartyInvitePick` 可以當樣板），點到人之後把名稱帶進對應的 `/chatinvite 名稱` 等指令。
- 如果之後要做遊戲內自畫的模態訊息框（`sub_8E1E30`），在 `LoginNewsRoute` 加一種顯示方式即可，`ClientLogin.cs` 不用動。
- 真機 smoke 時可以看事件：`native-ui-action`（route＝emote／loot／chat-command／loot-all），以及 `native-ui-login-news` 的 `display="message-box"`。
