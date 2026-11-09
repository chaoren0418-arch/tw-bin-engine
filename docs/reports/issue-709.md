# Issue #709 — T297：R4-F 狀態圖示與寵物資料接上

階段 code，第 1 次嘗試，分支 `task/709`。依據 `docs/reference/gap-matrix.md` 第 13.5 節 R4-F 列（另見 13.2 表 `PacketBoxStatus.cs:123`、`PetEquipment.cs:67–78` 兩列，13.3 op 25／17 列）。

## 做了什麼

只改任務列出的檔案，另加新測試檔 `engine/source/StatusAndPetFeedTests.cs`。

1. **原生 `BuffState` 讀 `PacketBoxStatusIcons`（S250 75／82／180）**（`NativeUi/State/BuffState.cs`）
   - `BuffState.From` 把 `WorldSession.PacketBoxStatusIcons` 加進 entries：75 → effect 135、82 → effect 175、180 → 封包第一個 D（effect id ≥ 540 在資料層就不列）。
   - `BuffEntry` 加三個選填欄位：`DisplayMode`（sub_F05A90 第 3 參數；0＝秒數、1＝百分比）、`IconOverride`（75 改寫的 .tbt 編號 3230−階段）、`ElapsedMs`（用表上 duration 的圖示，從最近一次加入算起已經過的時間）。
   - `BuffTooltipText` 加 `Percent`（字串 **1551**）。
2. **原生面板 `STATUS_EFFECT_ICONS`**（`NativeUi/BuffIconsPanel.cs`）
   - 圖示：有 `IconOverride` 就畫它（75 畫 3229／3228／3227，不畫表上的 icon）。
   - 百分比 tooltip：顯示模式 1 用 `"%s: %d%%"`，%s 是字串 1551。
   - 無期限：180 傳 −1，tooltip 是 `"%s: -- %s"`。
   - 75 倒數：`ApplyDefaultDuration` 用 effect 135 表上的 duration 減掉 `ElapsedMs/1000`；剩餘 < 0 算過期（有 offIcon 就畫 offIcon，tooltip 用 2854；沒有 offIcon 就顯示 `--`）。duration −1 算無期限。開始時間不知道的（S171 隱身）維持原本行為：不猜秒數。
   - offIcon 只用在顯示模式 0／6（sub_902840）。
3. **資料層**（`PacketBoxStatus.cs`）：75 階段 > 0 時記下收到的時間（`MagicStatusClock`）。原版每收到一次 75 就重新 `sub_F05A90`，所以每次都會重新計時。放在 `PacketBoxStatusIcon.ReceivedAt`；Reset 時清掉。
4. **預設模式（原生 UI 關閉）的水中呼吸 effect 11**（`StatusIcons.cs`）
   - `StatusIconEntry` 加 `Effect`／`IconOverride`。`StatusIconFeed.Read` 給水中呼吸帶 effect 11（`TimedBuffRules.StatusEffect`），75／82／180 帶各自的 effect 和 override。其他計時狀態沒有查證過的對照，維持 null。
   - 新增 `StatusIconArt`：和原生面板用同一個來源，在背景執行緒讀原客戶端 `Tile.pak` 的 effectlist2 和 `Sprite*.pak` 的 `<icon>.tbt`（`BuffEffectTable`／`BuffIconCatalog`，執行期讀取，不複製資源）。只有狀態列上有帶 effect 的項目時才讀。
   - WinForms 狀態列：有圖就畫原版圖示（32×32 整格依 .tbt 位移，等比縮到列高），沒有圖就照舊畫向量符號。表或圖讀不到、或解碼失敗時記一行 log，不畫替代圖。
5. **寵物頁 AC／背包過時**（`PetEquipment.cs`、`PetUi.cs`）
   - `LoginConnection.PetEquipmentSnapshot(pet)`：在 world 鎖裡取 `PetAc`、`PetBagStale` 和最近一次 S250/37 的序號（`PetEquipmentView`）。
   - 寵物頁的背包標題改用 `PetBagRefresh.Line`：AC 取 `PetAc`（S250/37 和 S176 哪份新用哪份），不再固定用背包尾端的 AC；過時的時候加註；背包已清掉、正在等回覆時只顯示 AC。
   - `PetUi.SyncPetBagRefresh`（每個 timer tick 跑一次）：背包過時、這次 S250/37 還沒要過、而且可以送時，**自動送一次既有的 C103 refresh**。同一次 S250/37 只送一次。不經過 `RunPet`，所以不會清掉使用者的輸入。
6. **召喚控制戒指**（`SummonFallbackUi.cs`）：`SummonFallback.ShouldReveal` 原本只認 `server` 來源，現在也認 `local`。召喚控制戒指（S36 type 5＋技能 51，PR #700 的 `TryOpenRingWindow`）開的本機請求，在原生 UI 關閉時也會切到寵物分頁，每個 revision 只切一次。

沒有新增面板（不需要 `[UiPanel]`／`IUiClosablePanel`）；原生 UI 關閉時的入口就是第 4、5、6 點的 WinForms 狀態列和寵物分頁。沒有改 `UiClientBridge.cs`、`rebuild-lock.json`、`Program.cs`、各註冊表。

## 原版依據（靜態分析，已驗證）

用 pefile＋Capstone 反組譯 `TW1608262200.bin`（SHA `e12949cf…641a2`，已核對）的 `sub_9061E0`（status effect tooltip，VA `0x9061E0`／FO `0x5055E0`）。腳本放在 `scratch/t709_tooltip.py`，沒有進 git。

| 顯示模式 | case VA | 格式字串（VA） | 字串 id |
|---|---|---|---|
| 0／6，offIcon | `0x90624A` | `"%s"`（`0x1181100`） | `0xB26`＝2854 |
| 0／6，期限 −1 或已過期 | `0x90628E` | `"%s: -- %s"`（`0x1181104`） | 930、931 |
| 0／6 | `0x9062C2` | `"%s: %d %s"`（`0x1181110`） | 930、剩餘秒、931 |
| **1** | `0x9062FD` | **`"%s: %d%%"`（`0x118111C`）** | **`0x60F`＝1551**、值（`+0x50`） |
| 12 | `0x906644` | `"%s: %d%%"`（`0x1181178`） | `0x12DD`＝4829（引擎沒用到） |

剩餘秒的算法是 `[+0x50] − (now − [+0x4C])/1000`；結果 < 0 時設過期旗標（`0x90620C`）。這和 `ApplyDefaultDuration` 一致。跳表在 `0x9066D0`（13 個 case）。

## 怎麼驗證

- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config`：**PASS**。用的是暫時 lock（`writes: false`，沒有改 `rebuild-lock.json`）。productDll `e901297e…cc54`，產品建置沒有新的錯誤或警告。worktree 沒有 `engine/vendor`，所以照 CI 改用 nuget.ci.config。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：pass，0 個錯誤。
- 離線測試 `StatusAndPetFeedTests.cs`（`[WorldModuleTest]` 自動發現，4 組，交給 CI 的 module-tests 執行）：
  - `status-pet-feed-buffs`：75／82／180 進 BuffState 的欄位；面板圖示（3227 override）、`P: 37%`、`L: -- U`、60→40 秒倒數、過期（有／沒有 offIcon）、duration −1、沒有字串表、`ApplyDefaultDuration` 邊界、移除與 Reset。
  - `status-pet-feed-default`：預設模式水中呼吸 effect 11、75／82／180 的 effect／override、沒有對照的計時狀態保持 null、`StatusIconArt.IconFor` 的 override、超出表、icon 0、圖檔不在時不畫。
  - `status-pet-feed-pets`：`PetEquipmentSnapshot`、`PetBagRefresh.Due`／`Line`，以及 C103 送出後不重送、新背包、下一次 S250/37 會再要一次、離開世界。
  - `status-pet-feed-ring`：戒指本機請求在原生 UI 關閉時切分頁，開啟時不切，同一個 revision 只切一次。
- 依本機執行限制，**沒有在本機執行任何編譯出來的程式**；測試結果以 PR 的 CI 為準。

## 未驗證什麼（待驗證）

- CI 結果（module-tests、DemoChecks 的 `native-ui-buff-icons`）：推送後看 PR。
- 真機：預設模式狀態列的 552.tbt 縮圖實際外觀、原生 UI 下 75／82／180 圖示與 tooltip、寵物穿脫裝備後自動 C103 與 AC 顯示、戴召喚控制戒指施放 51 時切寵物分頁。這些要開遊戲，交給 Codex／dot 在 P24Lab 驗收。
- 推測（沒有查證）：
  - 用 C103 取代原版不會得到回應的 C99（C_CHECK_INVENTORY）來重要寵物背包，是引擎自訂的等價做法。
  - WinForms 狀態列把 32×32 圖示縮到列高，是引擎自訂（原版圖示只在右上角用原尺寸畫）。
  - effect 135 的實際 duration 要看本機 effectlist2；測試用的是合成值。
- 沒做：
  - 180 開啟時原版會把 effect 的 startMsg 印到聊天欄（`sub_F05B10`），不在本任務範圍。
  - 原生 tooltip 的顯示模式 2／4／7／8／12，引擎沒有來源。
  - 82 的 518／574／576 變體旗標，引擎沒有來源。
- 本任務沒有標 Q4-n 的部分。

## 產出路徑

- 程式：`engine/source/NativeUi/State/BuffState.cs`、`engine/source/NativeUi/BuffIconsPanel.cs`、`engine/source/StatusIcons.cs`、`engine/source/PacketBoxStatus.cs`、`engine/source/PetUi.cs`、`engine/source/PetEquipment.cs`、`engine/source/SummonFallbackUi.cs`
- 測試：`engine/source/StatusAndPetFeedTests.cs`
- 報告：`docs/reports/issue-709.md`

## 給下一棒的提示

- gap-matrix 13.3 的 op 25（75／82／180）和 op 17（effect 11）這兩列，可以在下一輪改成「已有（待真機）」；13.2 表的 `PacketBoxStatusIcons`、`PetAc`／`PetBagStale` 兩列已經有產品使用者。
- `StatusIconEntry.Effect` 目前只填查證過的對照。要讓預設模式顯示更多原版圖示，可以把 `BuffEffectRules.Map`（原生的 V3 表）也拿來用，但要注意 classId 和有條件的對照（`Unmapped`）。
- 字串 1551 和 4829（模式 12）的內容沒有在本機核對過，真機看 tooltip 時順便確認。
