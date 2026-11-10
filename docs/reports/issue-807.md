# Issue #807：T344 R7-E 狀態圖示變體

依據：`docs/reference/gap-matrix.md` 第 15.6 節（R7-E）、`docs/reports/issue-738.md`。分支：`task/807`。

## 做了什麼

| 項目 | 內容 | 檔案 |
|---|---|---|
| 82 變體照原版累積 | `sub_736AC0` 每次只對選到的變體呼叫 `sub_F05A90`（新增，或更新該 effect 的值），不移除其他變體；值 ≤ 0 才把 175／518／574／575／576 全部移除。改成保留每個已建立變體最後一次的值。例如先 175 再 518，兩個都在；值 201 → 575（≥ 540 不建立）時 175 留著。收到 82、或 effect 293 新增／移除時重算，兩條路徑都是累積。 | `PacketBoxStatus.cs`（`packetBoxLeavesShown`、`EvaluateLeavesLocked`、`LeavesShownEffects`、快照 `leavesShown`） |
| 82 的按鈕附加處理（`0x736B59`） | 加完圖示後，若 `sub_7362F0` 為真 → `sub_F71FA0(effect,1)`＋`sub_F71800(effect, 字串 4448)`；否則設 `(effect,0)` 與空字串。只記錄資料：`PacketBoxStatusIcon.Boost／BoostText` → `BuffEntry.Boost／BoostText`，快照 `boost／boostText`。判斷式由 `WorldSession.LeavesBoostZone` 注入，預設 false（理由見下方「資源」）。**不畫**：圖示按鈕怎麼用這個旗標和文字（`sub_F799D0`／`sub_F706D0`）沒有追。 | `PacketBoxStatus.cs`、`NativeUi/State/BuffState.cs` |
| effect 470 的 tooltip 尾段（`0x90666D`） | 所有 case（含 5／9／10／11 與 > 12）最後都會走到這裡。effect 470：玩家旗標 +0x328 是 0 → 文字色改成調色盤 3，並用字串 3399 取代原本文字；不是 0 → 清成空字串（不顯示）。tooltip 預設色是調色盤 10（`0x906216`），原本畫成白色，已改正。 | `NativeUi/BuffIconsPanel.cs`（`TooltipText(...,vipFlag)`、`TooltipColorIndex`、`Paint`）；`NativeUi/State/BuffState.cs`（`BuffTooltipText.Vip470`／`FromAll`、`BuffState.VipFlag`） |
| 測試 | 新檔 `StatusIconFollowup2Tests.cs`，4 組 `[WorldModuleTest]`（自動發現）：`status-icon-followup2-leaves`、`-leaves-remove`、`-boost`、`-vip470`。`StatusIconFeedTests.cs` 第 3 段改成：201 之後 175 仍在（值 200）。 | `engine/source/StatusIconFollowup2Tests.cs`、`StatusIconFeedTests.cs` |

**超出「可修改的檔案」的一處（必要）**：`StatusIconFollowupTests.cs`（R5-F 的測試）的 `status-icon-followup-leaves` 有 10 個斷言寫死「只列目前變體」。改成累積後這些斷言一定失敗，所以只改了這段的期望值，並加一行註解說明。其他段沒動。這個檔案不在 15.6 任何一組的範圍內，不會和其他組衝突。

沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`，也沒有新增面板。

## 查證（已驗證，靜態反組譯）

BIN：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf…41a2`，腳本開檔時有核對。`.text` 的 FO = VA − 0x400C00。工具：capstone 5.0.6，加上自寫的 PE 匯入表解析。腳本放在 `scratch/issue-807/`，不進 git。

### `sub_736AC0` 尾段（VA 0x736B36–0x736C3D）

- `0x736B51`：`sub_F05A90(值, effect, 1, −1, 1)`。之後**沒有**任何移除其他變體的呼叫。只有「值 ≤ 0」這個分支（`0x736BE8`）會連續呼叫 5 次 `sub_F059F0`，對象是 0xAF／0x206／0x23E／0x23F／0x240。這就是「累積」的依據。
- `0x736B59`：條件是 `[0x153CE60] ≠ 0`。取 `sub_6B2C30([0x153CE60]+0x10)` 的座標，換算是 x' = ((x − 0x8000) >> 1) + 0x8000，y 不變。再呼叫 `sub_7362F0([0x138BE60], 座標)`：
  - 走訪 EinhasadExManager +0x24 的清單。
  - 每一項先用 `sub_7361D0` 判斷現在是否在期間內，再呼叫該項 vtable[0] 判斷區域。
  - 真 → `sub_521B20()`（圖示管理器 singleton `0x1657088`）→ `sub_F71FA0(effect, 1)`，接著 `sub_F71800(effect, sub_4EAFF0(0x1160))`。
  - 假 → `sub_F71FA0(effect, 0)`，接著 `sub_F71800(effect, "")`（`0x116C80E` 是空字串）。
- 清單的載入函式是 `sub_7363E0`，用到的字串在 `0x116C804–0x116C8DC`：`StartDate`、`EndDate`（`%Y-%m-%d %H:%M`）、`DurationMin`、`IntervalMin`、`BroadcastMessage`、`Zone`、`Map`、`Territory`、**`ExpInfo.xml`**、`BoostEvent`。

### `sub_9061E0` 尾段（VA 0x90666D–0x9066CD）

- 起點 `0x906216`：先把色槽 `[ebp+0xC]` 設成 `dword_1369D70[10]`（0x5F7F）。
- 尾段：`cmp [this+0x20], 0x1D6`，也就是 effect 470。
  - 若 `sub_4F9B90()+0x328 == 0`：色槽改成 `[3]`（0x7C00），再 `sprintf(buf, 字串 0xD47=3399)`。
  - 否則：`strcpy(buf, "")`（`0x118108E` 是空字串）。
- 匯入函式已核對：`[0x114B5C4]` 是 MSVCR110 `sprintf`；`0x103D464` 跳到 `[0x114B5C0]`，是 `strcpy`。
- 13 個 case 結尾都 `jmp 0x90666D`。抽查了 `0x906326`、`0x906364`，跳表外的情形（`ja`）也會到這裡。

### 玩家旗標 +0x328 的來源

- 掃描所有「`call sub_4F9B90` 之後存取 `+0x328`」的位置，共 13 處。**唯一寫入點**是 `0x94EA33`。
- 它所在的函式是 `sub_94E0E0`（TW16 op 43），子碼 72（跳表 `0x94F030`，索引表 `0x94F0B4`）：
  - 讀 3 個 D，分別存到 +0x328／+0x32C／+0x330。
  - +0x328 等於 1／2／3／4／5 時，用 `sub_F05A90(−1, …)` 加 effect 502／503／504／505／608。
- 3.81 伺服器（`1-伺服器/src`，唯讀搜尋）的 S64 送出端是 `S_EquipmentWindow`、`S_CharReset` 等。沒有任何一個送子碼 72（`S_EquipmentWindow` 的 type 是 65–68、48、49）。所以引擎把旗標固定為 0（`BuffState.VipFlag`）。
- **推測**：singleton（`0x13CD620`，大小 0x518）建立時旗標是 0。

## 資源（PakViewer CLI，唯讀，輸出在 `scratch/issue-807/`，不進 git）

- 用 `docs/reference/rea-scripts/pak/pak_index.ps1 -WithAlt` 列出所有 .idx：
  - `0-客戶端` 24 個：`Sprite`、`Sprite00–15`、`Text`、`Tile`、`LiTo\LTicon`／`LTsprite`／`LTsurf`／`LTtext`／`LTtile`。
  - kor25、t89、ly 各自的 `Tile`、`Text`、`Sprite*`。
- **`ExpInfo.xml`**：
  - 本機 `0-客戶端` 所有 pak 都**沒有**。
  - kor25 `Tile.idx` 有（1,995 B），解密後只有註解，沒有任何 `BoostEvent`。
  - ly `Tile.idx` 有 `ExpInfo.xml`、`ExpInfo-c.xml`（各 1,995 B）。
  - t89 `Tile.idx` 有 `expinfo.xml`（3,881 B），內含 `BoostEvent`（2015-02-05～02-17，`BroadcastMessage="4460"`）與多個 `Zone Map=… Territory=…`。
  - 結論：用本機資源時清單是空的，`sub_7362F0` 一律為假。所以 `LeavesBoostZone` 預設 false，與原版相符。
- **字串**（`string-c.tbl`）：

  | 字串 | 本機（3484 筆） | kor25（4703 筆） | ly |
  |---|---|---|---|
  | 3399 | `dummy3401` | 「VIP 尚未開啟」 | 「VIP 尚未開啟」 |
  | 4448 | 沒有 | `dummy4450` | 帶色碼的一行，內容是 `\n…:\nEXP+100%`（推測是附加在 tooltip 的說明） |

- effectlist2（`scratch/task448-uixml/effectlist2.xml`，SHA-256 `FC522B85…88FD`，唯讀）：
  - 470 是 `STR_STATUS_EFFECT_CH_VIP_BUFF`，icon 5646，offIcon 5647，duration −1。
  - 502–505 依序是 `TIMED_EXP_RATE`、`TIMED_ADENA_RATE`、`TIMED_DROP_RATE`、`STR_STATUS_EFFECT_VIP_Z_BUFF`。

## 怎麼驗證

- `dotnet build engine/source/OfflineClient.csproj -c Release`：成功，0 錯誤。
  - restore 用 `scratch/issue-807/nuget.config` 指向原專案的 `engine/vendor`（只讀），`NUGET_PACKAGES` 設成 worktree 的 `engine/.nuget`。
  - 警告都在既有檔案（`HtmlActionTests.cs`、`MagicUi.cs`）。
- `DemoChecks.csproj` 在本機單獨建置時找不到產品組件（`P06` 命名空間），這是本機建置環境的問題，與本次修改無關。改由 CI 編譯與執行。
- `engine/tools/check-no-game-assets.py`：pass（4250 個追蹤檔，0 錯誤）。
- 單元測試（既有測試＋本組 4 組）依規定**不在本機執行**，交給 PR 的 CI。

## 未驗證／推測

- **推測**：玩家 singleton 的 +0x328 起始值是 0。建構子沒有追。
- **推測**：4448 是附加的 tooltip 行。根據是 ly 的字串以 `\n` 開頭；`sub_F706D0` 沒有追。
- **沒做（只做資料）**：
  - Boost 旗標與 4448 的畫法。
  - `sub_7362F0` 的期間／區域判斷：`sub_7361D0` 的時間來源、DurationMin／IntervalMin 的循環、vtable[0] 的 Territory 比對、座標換算的意義。
  - ExpInfo.xml 的讀取。本機沒有這個檔，原版用本機資源時結果固定為假。
- **沒做**：
  - effect 470 在圖示繪製 `sub_9001F0` 的特例。不在本組範圍，ui-layout §4 有記。
  - S64/72 本身的解析：要改 `CharReset.cs`，不在本組範圍，而且 3.81 不送。
- **真機**：effect 470（3.81 的 VIP 經 180 送出）的 tooltip 文字與紅色、82 兩個變體同時存在時的圖示，都要等真機（Codex／dot）。

## 產出路徑

- `engine/source/PacketBoxStatus.cs`、`engine/source/NativeUi/BuffIconsPanel.cs`、`engine/source/NativeUi/State/BuffState.cs`
- `engine/source/StatusIconFeedTests.cs`（第 3 段）、`engine/source/StatusIconFollowupTests.cs`（leaves 段期望值）
- 新檔 `engine/source/StatusIconFollowup2Tests.cs`
- 本報告 `docs/reports/issue-807.md`

## 給下一棒的提示

- 如果要讓 Boost 真的生效：
  1. 實作 ExpInfo.xml（t89 有範例）的 `BoostEvent`／`Zone` 解析。
  2. 把判斷式接到 `WorldSession.LeavesBoostZone`。
  3. 追 `sub_7361D0`（時間）、`Zone` 的 vtable[0]（Territory 是 x1,y1,x2,y2），以及 `sub_F706D0`（文字怎麼顯示）。
- 如果要接 S64/72：在 `CharReset.cs` 讀 3 個 D，存進 `BuffState.VipFlag`。另外 1–5 要加 effect 502–505／608（無期限）。
- tooltip 的字色現在用 `ChatPalette`（`dword_1369D70`）。背景與內距仍是待驗證。
