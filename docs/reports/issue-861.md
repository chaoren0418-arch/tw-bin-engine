# Issue #861 T377：R9-Q 選項窗

依據：`docs/reference/followups-9.md`（FU9）§14.4、§16.2 R9-Q 列；`gap-matrix.md` §15.11（4:OptionUI，K5）。
裁定（#818）：原生 UI（預設）下，主選單鈕 6／Ctrl+V 開原生 `OptionUI`；`--classic-ui` 維持 3.81 HTML `option1`。

## 做了什麼
- `NativeUi/OptionPanel.cs`
  - 勾選框依 `MouseEvent` 接上引擎已有的設定，**點一下就生效**（原版每次點擊都馬上經 `sub_9184B0` 寫回全域；OK／Close 只關窗，沒有取消還原）。同一個選項出現在兩頁（Home／Game、Home／Etc）時，兩個勾選框對應同一個值。
  - `ShowCharacterHPMPBtn`、`HPMPBarThickBtn`、`HPMPBarRecoveryBtn`、`HPMPBarPercentBtn`：改 `HpMpPanel.Settings`，透過 `HpMpPanel.Apply` 套用，Thick 會重排。頭上 HP 條讀的是同一份設定。面板經 `UiRoot` 找 HP 窗（註冊時注入）。
  - `ShowNpcNameBtn`、`CheckMute`、`EnableElfTelControlBtn`：由新介面 `IOptionWindowHost`（宿主是 ClientForm）處理。
  - 選項開著的勾選框畫成 PushPng（推測：原版勾選後的樣子沒有追）。
  - 宿主拒絕時（例如聲音關閉）不改值，log 記「這次未套用」，不記成「尚未接上」。
  - `NotWired` 列出 FU9 提到、但引擎沒有開關可改的項目和原因。
- `NativeUi/MenuButtonsPanel.cs`（只接入口）：鈕 6 與 Ctrl+V 在原生選項窗可用時，經 `UiRoot.Toggle` 開關原生窗。不可用時退回 HTML `option1`，跟以前一樣。不可用的情況包括：沒有參考客戶端 OptionUI.xml、面板出錯、沒有 root。新增 `OptionWindow(root)`。`--classic-ui` 不建原生 UI，所以仍走 option1。鈕 5／Ctrl+C（option2）沒改。
- `RingWindows.cs`（只接選項）：新增 `ClientForm.OptionWindowHost : IOptionWindowHost`，在 `SyncRingWindows` 掛上去（和書籤窗、盟屋窗的宿主同一個時機）。
  - `ShowNpcNameBtn` → `showAllNames`（0x138BE7F「全部顯示名稱」），並寫回 `name-display.txt`，跟 WinForms 勾選框同一個值、同一個檔。
  - `CheckMute` → `SetAudioSettings(Gain, muted)`，走既有的套用流程。
  - `EnableElfTelControlBtn` → `WorldSession.ElfBookmarkTeleportOption`（byte_13A931C）。
- 新檔 `NativeUi/OptionWindowTests.cs`（`[WorldModuleTest("option-window")]`）：
  - 對應表；四項 HP／MP 點擊馬上改 `HpMpPanel.Settings`，且畫面勾選狀態正確；
  - 宿主項目：兩頁共用同一個值，宿主拒絕時不改值；
  - 沒接的項目留在 `Unhandled`；
  - 鈕 6／Ctrl+V 開關原生窗，原生窗不可用或沒有 root 時退回 option1，鈕 5 不變；
  - 註冊的面板綁到 root 的 HP 窗；`OptionWindowHost` 的型別；妖精規則；選項預設關閉。
- `PlayerAudioSettings.cs`：**沒有改**，`SetAudioSettings` 已經夠用。
- 未動共用檔：沒有改 `UiClientBridge.cs`、`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`rebuild-lock.json`。

## 沒接的勾選框（引擎沒有對應設定）
| 選項 | 原因 |
|---|---|
| `ShowDropItemNameBtn`（byte_138BE7E） | 地面物品名稱一律畫出（`ObjectRendering.cs:528`），沒有開關；要加開關得改 `ObjectRendering.cs`，不在本組範圍 |
| `TransparentDeadMonsterBtn`（byte_138BFAA） | 引擎沒有透明屍體的畫法 |
| 4 條音量捲軸 | 引擎只有一個主音量；捲軸拖曳本來就沒接 |
| 其他 TW16 選項（EnableTrade、EnableWhisper、ShowTradeChat 等） | FU9 沒列為對得上；其中幾項可能對得上 `ChatChannelSettings`，留給後續判斷 |

## 怎麼驗證
- **本機編譯通過**：0 錯誤，改動的檔案沒有警告。指令：`dotnet restore`，來源只用本機 `engine/vendor`，套件放 `scratch/task-861/packages`；再 `dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o scratch/task-861/build`。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`（4347 個追蹤檔，0 錯誤）。
- MouseEvent 名稱來自先前已解出的 kor25 `OptionUI.xml`（`scratch/wt/task-629/scratch/rea/uixml_alt/kor25/OptionUI.xml`，SHA-256 `fe32d2fd…c8846d`，與 #768 報告相同）。只讀，沒有放進 repo。本組沒有新的圖或資源需求，所以不必另外查 pak。
- 離線測試 `option-window` 和既有的 `option-panel`、`menu-hotkeys` 交給 CI 的 `--module-tests` 跑（依規定，本機不執行編譯出來的程式）。
- **CI（PR #878，run 42887727025／42887727265）**：`dotnet`、`map-continuity`、`npc-offline` 全部 pass。`--module-tests` 整份 `failed: 0`，其中 `option-window` pass（49 項檢查，0 失敗），既有的 `option-panel`、`menu-hotkeys` 也 pass。

## 未驗證什麼（真機再確認）
- 勾選後畫 PushPng 是推測；原版勾選框的繪製沒有追。
- 在原生窗改「全部顯示名稱」後，開發工具裡的 WinForms 勾選框不會跟著更新，要重開才會反映。兩邊共用同一個值和同一個檔。
- `EnableElfTelControl` 只存在記憶體，原版的設定檔存取（`sub_59DD30`／`sub_59E920`）沒做；原版在等級低於 50 時會把它清成 0（`0xEC99EB`），引擎沒做，只在使用時由 `ElfBookmarkTeleportRules` 擋。
- HP／MP 四項設定仍只在當次執行有效（沿用 #465）。
- 真機開窗、點擊、靜音實際效果都待驗證。

## 產出路徑
- `engine/source/NativeUi/OptionPanel.cs`、`engine/source/NativeUi/MenuButtonsPanel.cs`、`engine/source/RingWindows.cs`、`engine/source/NativeUi/OptionWindowTests.cs`
- 本報告 `docs/reports/issue-861.md`；分支 `task/861`

## 給下一棒的提示
- 要接掉落物名稱或透明屍體，先在 `ObjectRendering.cs` 加開關，再把 `OptionPanel.NotWired` 的項目移到 `HostEvents`，並在 `OptionWindowHost` 加 case。
- 要讓 WinForms「全部顯示名稱」勾選框同步，需要改 `ObjectRendering.cs` 的 `HookNameDisplay`（保留勾選框參照）。
- 真機核對清單：
  1. 按鈕 6 和 Ctrl+V 開的是原生選項窗（Home 頁），再按一次會關閉。
  2. Etc 頁四個 HP／MP 勾選框點下去，畫面馬上變化。
  3. Home 頁「NPC 名稱」切換全名顯示。
  4. 「靜音」立即靜音。
  5. 妖精 Lv 50 以上勾選「EnableElfTelControl」後，施放傳送魔法會開書籤窗。
  6. `--classic-ui` 時按鈕 6 仍是 HTML option1。
