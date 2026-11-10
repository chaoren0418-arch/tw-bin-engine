# Issue #738：T311 R5-F 狀態圖示收尾

依據：`docs/reference/gap-matrix.md` 第 14.6 節（R5-F）、`docs/reference/ui-layout.md` 第 4 節（V3）。
分支：`task/738`。

## 做了什麼

| 項目 | 內容 | 檔案 |
|---|---|---|
| 180 的 startMsg | 180「開」時，依 effectlist2 的 `startMsg`（string-c.tbl id）印一行到聊天欄，色 24。每次「開」都印，包括圖示已存在的情況。startMsg 是 0、effect ≥ 540、effect 不在表中、表讀不到、字串 id 超出字串表時都不印，不猜文字。封包裡的字串 id（第二個 D）原版不用，這裡也不用。 | `PacketBoxStatus.cs`（`PrintNoneTimeStartMessage`、`NoneTimeStartMessages`、快照 `startMessages`）；`NativeUi/BuffIconsPanel.cs`（`BuffEffect.StartMsg` 解析 `startMsg`）；`StatusIcons.cs`（`StatusIconArt.SharedEffectTable`：同步讀一次 Tile.pak 的 effectlist2） |
| tooltip 模式 | `sub_9061E0` 的 13 個 case 全部對照：2、4、7、8、12 已實作；3、5、9、10、11 不顯示 | `NativeUi/BuffIconsPanel.cs`（`TooltipText` 改成靜態可測）；`NativeUi/State/BuffState.cs`（`BuffTooltipText` 加 978／1587／763／1630／4829 與 `From`） |
| 82 變體旗標 | 依 `sub_736AC0` 選 175／518／574／575／576。基底地圖是 0x1900 → 574；effect 293 顯示中 → 518（值 > 200 → 576）。只在收到 82，或 effect 293 新增／移除時重算，與原版相同。 | `PacketBoxStatus.cs`（`LeavesEffectFor(value,mapBase6400,pcCafe)`、`BaseMap`、`LeavesPcCafeFlag`） |
| 字串 1551 核對 | 本機 string-c.tbl 1551 是「剩餘的祝福指數」。它是 82 百分比 tooltip 的標籤，與 effect 175（`STR_STATUS_EFFECT_BLESS_OF_EINHASAD`，startMsg 1550「受到殷海薩的祝福…」）相符。 | `NativeUi/State/BuffState.cs` 註解 |
| 測試 | 新檔 `StatusIconFollowupTests.cs`，4 組 `[WorldModuleTest]`（自動發現）：`status-icon-followup-strings`、`-tooltip`、`-leaves`、`-startmsg` | `engine/source/StatusIconFollowupTests.cs` |

沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`，也沒有新面板。

## 查證（已驗證，靜態反組譯）

BIN：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf…41a2`（腳本開檔時核對）。`.text` 的 FO = VA − 0x400C00。用 capstone 反組譯；腳本在 `scratch/issue-738/`（不進 git）。

### tooltip `sub_9061E0`（VA 0x9061E0／FO 0x5055E0）

- 用 byte +0x260（顯示模式）分派，跳表在 VA 0x9066D0：`[0x90624A, 0x9062FD, 0x90632B, 0x906369, 0x90637D, 0x90666D, 0x90624A, 0x9063CE, 0x906485, 0x90666D×3, 0x906644]`。
- left = [+0x50] − (now − [+0x4C]) / 1000；left < 0 時視為過期。

| 模式 | case VA | 格式與字串 |
|---|---|---|
| 0、6 | 0x90624A | offIcon 狀態 → 2854；−1 或過期 → `"%s: -- %s"`（930、931）；其他 → `"%s: %d %s"`（930、left、931） |
| 1 | 0x9062FD | `"%s: %d%%"`（1551、[+0x50]） |
| 2 | 0x90632B | `"%s: %d%s"`（978、[+0x50]、1587） |
| 3 | 0x906369 | 寫空字串 |
| 4 | 0x90637D | `"%s: %d%s"`（978、left/60+1、1587） |
| 7 | 0x9063CE | `"%s: %d%s %d%s %d%s"`（978、日、763、時、1630、分+1、1587） |
| 8 | 0x906485 | `"%s:"`（978），再依序取日 763／時 1630／分 1587（不 +1）／秒 931 中非 0 的，最多兩段，每段 `" %d%s"` |
| 12 | 0x906644 | `"%s: %d%%"`（4829、[+0x50]） |
| 5、9、10、11 | 0x90666D | 不寫 |

- 結尾 VA 0x90666D 有 effect 470 的特例：玩家旗標 +0x328 是 0 時改成字串 3399，否則接一段字尾。**本輪沒做**。

### 180 startMsg：`sub_F05B10`（VA 0xF05B10）

- 先呼叫 `sub_F05A90` 加圖示。第 5 參數不是 0，且 effect 表 +0xC（startMsg）不是 0 時，呼叫 `sub_7DC800(startMsg, 色[24], 色[14])`。
- `sub_7DC800`（VA 0x7DC800）：`sub_A11280(id)` 回假，且 id < 字串表筆數時，才把字串印到聊天欄。`sub_A11280` 的過濾條件沒有展開。

### 82 變體：`sub_736AC0`（VA 0x736AC0／FO 0x335EC0）

- 值 [+4] ≤ 0 → 移除 175／518／574／575／576。
- `[0x138BE64] == 0x1900` → 574。
- 值 > 200 → 旗標 [+0x10] 開時 576（0x240），關時 575（0x23F）。
- 其他 → 旗標開時 518（0x206），關時 175（0xAF）。
- 加圖示用 `sub_F05A90(值, effect, 1, −1, 1)`，模式 1。
- `0x138BE64` 只在 `sub_F09D74` 寫入，值是 `sub_F027F0(地圖)`，也就是基底地圖：`sub_F14F90` 先查一張 std::map 對照表；查不到時，地圖 < 0x4000 取原值，否則取 `& 0xFE00`。
- 旗標 [+0x10]：
  - VA 0x736070 的 handler：事件 payload +8 是 0x125（293）時設 1，然後重跑 `sub_736AC0`。
  - VA 0x7360C0 的 handler：條件相同時清 0。
  - 事件 0x15 由圖示新增路徑 `sub_F716D0 → sub_F6CB50 → sub_F6D120`（`push 0x15`，VA 0xF6D163）在「新建圖示」時送出，payload 是 effect id。
  - 本機 effectlist2 的 293 是 `STR_STATUS_EFFECT_PC_CAFE_CHRISTER`（kind neutrality，startMsg 2156）。

### 字串（本機資源，只讀）

- `D:\lineage開發\0-客戶端\Text.pak`，在記憶體解出：`string-c.tbl`（record 32559，decoded SHA-256 `4ebbc1a7…43e9`，3484 筆）。內容：
  - 930「剩餘時間」、931「秒」、2854「魔法效果結束」、1551「剩餘的祝福指數」
  - 978「剩餘時間」、1587「分」、763「日」、1630「時間」
  - 3399「dummy3401」、1550「受到殷海薩的祝福，增加了些許的狩獵經驗值。」
- **4829 不在本機任何字串表**。查過 Text.pak 全部 6 個 `string*`：
  - `string-c.tbl`（3484 筆）
  - `string-c.tbl.bak`（3411 筆，1551／978／1587 與 c 版相同）
  - `string-e.tbl`（1054）、`string-h.tbl`（1138）、`string-j.tbl`（1135）、`string-k.tbl`（1191）
  - 所以模式 12 在本機沒有文字，程式回 null，不自己編。
- effectlist2 屬性：取自先前 #448 從 Tile.pak 解出的 `scratch/task448-uixml/effectlist2.xml`（SHA-256 `FC522B85…88FD`，只讀）。135／175／293／470／518 的 `startMsg` 都是 string-c id。

## 怎麼驗證

- `python -B -X utf8 engine/_integration/dev/rebuild.py --build`：PASS。
  - worktree 沒有 `engine/vendor`，所以用 `--configfile scratch/issue-738/nuget.config` 指向原專案的 `engine/vendor`，只讀。
  - lock 是暫時的，沒有寫入。
- `DemoChecks.csproj`、`StatusIconChecks.csproj`：`dotnet build` 成功（只編譯，沒有執行）。
- `engine/tools/check-no-game-assets.py`：pass（4112 個追蹤檔，0 錯誤）。
- 單元測試（既有＋本組 4 組）依規定**不在本機執行**，交給 PR 的 CI（`--module-tests`）。

## 未驗證／推測

- **推測**：事件 0x16 是「圖示移除」。只看到 handler 會清旗標，送出端沒有追。
- **推測**：effect 293 只會由 180 送來（引擎裡沒有其他來源）。
- **待驗證**：`sub_F14F90` 的地圖對照表（std::map）來源沒有展開。本模組只用「查不到時」的規則，所以被對照表改了基底的地圖不會判成 6400。
- **與原版不同（刻意）**：原版換變體時不會移除舊變體。例如先收到 175 再收到值 201，175 會留著。本模組只列目前的變體，因為既有測試 `StatusIconFeedTests.cs`（不在本任務可改範圍）規定「201 → 不列」。要完全照原版，需要開一張允許改那個測試的任務。
- **沒做**：82 加圖示後的按鈕附加處理（VA 0x736B59：`sub_7362F0` 為真時設旗標，並設字串 4448，本機表沒有這個字串）；effect 470 的 tooltip 特例；`sub_A11280` 的訊息過濾。
- **真機**：180 的 startMsg 在聊天欄的位置與顏色、82 在 6400 地圖與 PC 咖啡效果下的圖示，都要等真機（Codex／dot）。
- **本機副作用**：直接 `dotnet build` 那兩個檢查專案時沒有設 `NUGET_PACKAGES`，restore 可能用到使用者家目錄預設的 NuGet 快取（`~/.nuget/packages`）。下一棒請改用 rebuild.py，它會設成 `engine/.nuget`。

## 產出路徑

- `engine/source/PacketBoxStatus.cs`、`engine/source/StatusIcons.cs`、`engine/source/NativeUi/BuffIconsPanel.cs`、`engine/source/NativeUi/State/BuffState.cs`
- 新檔 `engine/source/StatusIconFollowupTests.cs`
- 本報告 `docs/reports/issue-738.md`

## 給下一棒的提示

- 如果要完全照原版的「變體累積」：改 `PacketBoxStatusIcons` 的 82 段，改成保留每個變體最後的值，並同步改 `StatusIconFeedTests.cs` 第 3 段（201 那一行）。
- 參考客戶端（T319 `RefClientXmlSource`）如果有更大的 string-c.tbl，可以再查 4829 與 4448。
- 180 的 startMsg 用的 effect 表從 `StatusIconArt.SharedEffectTable()` 來（同步、只讀一次）。測試用 `WorldSession.StartMessageEffects` 換成合成表。
