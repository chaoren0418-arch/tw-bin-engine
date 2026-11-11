# Issue #958：T416 R11-B 動作窗收尾（4:BehaviourUI）

依據：`docs/reference/gap-matrix.md` 15.13.5（4:BehaviourUI 列）、15.13.6（R11-B）；裁定 #952 第 1 點（④ 提示 3118 照原版做）。
前置 R11-A（#957，`LoginConnection.BeginObjectPick`）已在 main。

## 做了什麼

| 項 | 改動 | 檔案 |
|---|---|---|
| ① | `Action_ViewDropItemName`、`Action_CorpseTransparent` 從 None 改成 Toggle，Argument 是選項窗的 MouseEvent（`ShowDropItemNameBtn`／`TransparentDeadMonsterBtn`）。FU8§4.3 寫兩個處理函式只切 `byte_138BE7E`／`byte_138BFAA`，跟 R10-I 選項窗是同一個值。產品端由 `NativeUiCommands` 呼叫 `ClientForm.OptionWindowHost` 切換（同一個值、同一個存檔 `option-ui.txt`，不送封包）。面板的後備是經 `OptionPanel.Host` 切換；沒有宿主時回 Unhandled | `ActionWindowPanel.cs`、`UiClientBridge.cs` |
| ② | `Action_InviteParty`（sub_ADA6B0，FU9 第 341 行）：自己是隊長而且有隊伍（`PartyView.CanKick`）時，走 R11-A 的選對象入口。點到已在隊上的人送 C255 踢人（`SendParty("kick")`，等同 `/banish`）；點到別人照 R10-D 送 C230 type 0。不是隊長時照舊走 `/invite` 的組隊選對象 | 同上；邏輯在 `ActionTargets.InviteOrKick`／`PartyKick` |
| ③ | `Action_AddFriend`、`Action_Exclude`、`Action_InviteChatParty`：產品端進入 R11-A 選對象模式（瞄準游標 8）。點到合格玩家後，等同在聊天列輸入「前綴＋名稱」送出（`SendPlayerChat`，閘門與斜線指令都跟 ChatCommand 路線相同）。沒有橋接（離線、不在世界中）時，面板照舊把前綴放進聊天列當退路 | 同上；邏輯在 `ActionTargets.SendToTarget`／`PlayerName` |
| ④ | 常駐鈕 `ActionUI_Button` 滑鼠停留時畫 Tooltip String 3118。文字取自字串表（`ServerMessageText.Table`），去掉色碼；讀不到字串表時只有 3118 用 PakViewer 查到的原文「行動視窗」。框的寬高、字色、字的起點沿用 SummonPanel（sub_B0DAF0、sub_C00BF0）。新增 `ButtonRect` 當點擊範圍；`Bounds` 是鈕加上提示框，提示框本身不收點擊 | `ActionWindowPanel.cs` |
| 註解 | 更正檔頭（提示不畫、聊天列前綴是過渡）、`:125`（`sub_9FBC00` 是送封包函式，不是輸入框）、`:136`（檢舉直接送 op 55 type 42）、`:137–138`（「引擎沒有開關」→ 已接 R10-I）、`ActionOpenButtonPanel` 文件註解、`NativeUiCommands` 開頭註解 | 同上 |

### ③ 送什麼（任務內核對）
- **FU8 第 16 行**：`sub_9FBC00` 是送封包函式，第 2 個參數是 opcode（`wo_sendsites.py 118` 掃的就是這個）。所以 FU8§4.3 的 `sub_9FBC00(off_119F834, 120, 3, 名稱)` 是**直接送** TW16 op 120（C_CHAT_PARTY_CONTROL，gap-matrix 第 182 行）type 3 加名稱，不是開輸入框。3.81 的 C199（C_ChatParty，server-protocol.md）只有 type 0–2，所以改送 3.81 的聊天隊伍邀請 `/chatinvite 名稱`（C230 type 2，Party.cs）。**這個對應是推測**。
- **FU13 第 70 行**：`sub_ADA220 → sub_B18300` 送 `cs` 138（加好友＋名稱）＋`c` 56（查名單），等同引擎 `/addbuddy 名稱` → C207＋C4（Buddies.cs:217）。**已驗證（文件）**。
- `Action_Exclude`：處理函式沒有單獨追。FU13§3.2 說，會讀名稱的封鎖是 `CommandAddExclude → sub_F40FC0(名稱)`，3.81 只有 C171 這條路 → `/exclude 名稱`。**推測**。
- 對象規則（推測）：玩家旗標（Flags & 4，同 sub_954FC0）、外觀已到、不是自己、名稱合法；距離不限（domain="global"）；死亡的玩家也算對象。

### 資源查核（PakViewer CLI，唯讀；輸出在 worktree 的 `scratch/t958/`，未進 git）
- 工具：`D:\lineage開發\PakViewer-main\src\PakViewer.Cli\bin\Release\net10.0\pakviewer-cli.exe`，指令 `pak extract`、`xml decrypt`。
- `string-c.tbl`（`Text.idx`）第 3118 筆：

  | 客戶端 | 筆數 | 3118 |
  |---|---:|---|
  | 本機 `D:\lineage開發\0-客戶端` | 3,484 | 行動視窗 |
  | kor25 `D:\381懶人包kor25(原)` | 4,703 | 行動視窗 |
  | t89 `D:\天堂參考用客戶單\89天堂` | 4,552 | 行動視窗 Ctrl+D |

- 本機 `Tile.idx` 的 `ActionUI-c.xml` 解密後，SHA-256 前 16 碼是 `ae467982a6880161`（跟 FU8§0 相同）。`ActionUI_Button` 底下是 `<Tooltip Name="Desc_Tooltip" String="3118"/>`，**沒有 X／Y／BackColor**，所以提示框位置與底色是推測。
- 本任務沒有新增任何圖；勾選圖沒有依據，照舊不畫。

## 怎麼驗證
- **編譯**：`dotnet build engine/source/OfflineClient.csproj -c Release` 成功，0 錯誤；改動的檔案沒有新警告。本機遇到 WebView2 `.winmd` 的 NETSDK1130（環境問題，跟 #922／#967 相同），用 `scratch/t958/nowinmd.targets`（`-p:CustomAfterMicrosoftCommonTargets=…`）繞過，**沒有改 csproj**。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`（4565 個檔，0 錯誤）。
- 新增離線測試 `engine/source/NativeUi/ActionWindowFollowupTests.cs`（`[WorldModuleTest("action-window-followup")]`，自動發現）：
  - 路線表與更正後的說明；
  - ① 經 OptionPanel 宿主切換、宿主拒絕或沒有宿主時是 Unhandled；
  - ③ 對象判斷；送出文字能解析成既有指令；用假連線檢查實際封包（加好友 C207＋C4、封鎖 C171、聊天邀請 C230 type 2）；經 PlayerPointer 點人就送、點 NPC 不送、點地面取消；
  - ② `PartyKick` 規則；隊長點隊員 → C255，點別人 → C230 type 0，點 NPC 不送；
  - ④ 提示框文字、尺寸、位置、繪製、點擊不收、`^` 換行、沒有字串表時的後備、其他編號不畫。
- 既有 `ActionBridgeTests.cs`（action-bridge）有兩處斷言把 ① 的舊行為寫死（路線計數 Toggle 2／None 11、Toggle 一律 Toggled），**只改這兩處**，改成新計數 4／9，地面名稱與透明屍體在沒有宿主時是 Unhandled；Unhandled 總數仍是 12。這個檔不在「可修改的檔案」清單內，但不改的話既有測試一定紅，所以把改動限在最小。
- **依本機執行限制，沒有執行任何編譯出來的程式**；單元測試與 `--module-tests` 以 PR 的 GitHub Actions 結果為準。

## 未驗證什麼（待驗證，交真機／Codex）
- 3.81 的 `Action_InviteChatParty` 是否真的對應 C230 type 2；`Action_Exclude` 的處理函式沒有單獨追。
- 選對象的對象規則（死亡玩家算不算、有沒有距離限制）。
- 提示框位置（鈕正下方置中、放不下時放上方）、黑底不透明、字色沿用 SummonPanel 的 0xBDF7，都是推測；畫面要真機比對（M33）。
- 原版切換兩個顯示開關時有沒有印訊息：FU8 只寫切換 byte，引擎不印。
- 隊長踢人時，原版有沒有確認框：FU9 沒寫，引擎直接送。

## 產出路徑
- `engine/source/NativeUi/ActionWindowPanel.cs`（`ActionRoutes`、新增 `ActionTargets`、面板後備、`ActionOpenButtonPanel` 提示）
- `engine/source/NativeUi/UiClientBridge.cs`（只動 `NativeUiCommands`）
- `engine/source/NativeUi/ActionWindowFollowupTests.cs`（新）
- `engine/source/NativeUi/ActionBridgeTests.cs`（兩處斷言，理由見上）
- `docs/reports/issue-958.md`（本報告）

## 給下一棒的提示
- gap-matrix 4:BehaviourUI 列的 ①–④ 都已有程式。剩下的只有選做的「拖到巨集格」和沒有依據的勾選圖，下一輪盤點可以考慮改「已有」（標明推測項）。
- 其他對象型動作（`Action_NumberingMarker` 等）如果之後查到處理函式，可以照 `ActionTargets.SendToTarget` 的寫法接上 `BeginObjectPick`。
- 本機編譯遇到 NETSDK1130 時，可以照本報告的 scratch hook 只做編譯檢查。
