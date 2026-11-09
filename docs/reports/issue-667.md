# Issue #667 — T276：控制戒指開窗（傳送選書籤、召喚選單）與盟屋地圖 674 鈕

依據：`docs/reference/followups-3.md` 第 3、4 節（T275／#665）。分支 `task/667`。

## 做了什麼

1. **S36 type 1（傳送控制戒指）**：`AbilityStatus.cs` 新增 `WorldSession.TryOpenRingWindow()`。施放技能 5／69（不帶參數的 `CastSelectedMagic`），而且 `AbilityEquipped(1)==true` 時：
   - **不送 C6**，改開「選傳送目的地」請求 `TeleportDestinationRequest`。窗已開就不重開。
   - 目前地圖是 5、6、83、84 或 ≥ 0x4000 時，顯示字串 276、不開窗。
   - 選定書籤後由 `RingWindows.cs` 的 `SendTeleportDestination` 走既有的 `CastSelectedMagic`，目標是 `SkillTargetKind.Bookmark`，送出 **3.81 `HD`（書籤地圖、書籤 id）**，不照抄 TW16 的 `hhh`。送出成功才關窗；取消鈕或 ESC 只關窗、不送。
   - 畫面：既有的書籤分頁 `BookmarkPanel`（`BookmarkUi.cs`）加上「選目的地」模式：橫幅、「傳送到此書籤」、「取消傳送」、ESC。請求開啟時會自動切到書籤分頁。
2. **S36 type 5（召喚控制戒指）**：施放技能 51，而且 `AbilityEquipped(5)==true` 時，改呼叫既有的 `OpenLocalSummon()`。原生 `SummonPanel`（M7-W）照常顯示，不送封包；窗已開就不重開。
   - 送出前的等級檢查是 `SummonCatalog.RingLevelAllows`（`NativeUi/SummonPanel.cs`）：`(等級−28)/4+7 ≥ action%256`。不成立時顯示 743、不送；窗、選擇與 revision 都保留。
   - 檢查掛在 `Pets.cs` `SendSummonChoice` 的 local 分支（加一行）。
3. **盟屋地圖 674 鈕**：`NativeUi/AgitMapPanel.cs` 新增 `IAgitMapHost`。按下與放開都在 674 鈕內時，呼叫宿主的 `Select(shown)`。宿主（`RingWindows.cs`）送 `C125 D(S187 物件 id) S("select") S(十進位盟屋編號)`，封包由 `AgitSelectPackets.Select` 組。條件不成立時不送：S187 已被新的取代、編號超出原版範圍、物件 id 為 0，或互動正在送出、動作被鎖定。伺服器回的 S39 `agsel` 交給既有的 NPC 對話處理（`NpcInteraction.cs`），沒有改那裡。
4. **測試** `engine/source/RingWindowsTests.cs`，共 4 組 `[WorldModuleTest]`：`ring-windows`、`ring-windows-teleport`、`ring-windows-summon`、`ring-windows-agit`。只用合成封包、假傳送端和合成單色 surf。

### 超出任務列出檔案範圍的改動（接線必要，各只有一兩行）

- `Magic.cs`：`CastSelectedMagic(requested)` 開頭加一行。`requested==null` 而且 `TryOpenRingWindow()` 為真時，回 false、不送。所有施放入口（按鈕、Web、熱鍵、HUD）都經過這裡，這是唯一的攔截點；目前沒有施放用的擴充 attribute。
- `Pets.cs`：`SendSummonChoice` 的 local 分支加 743 檢查（同上第 2 點）。
- 新檔 `RingWindows.cs`：送出端（LoginConnection）、674 封包，以及 ClientForm 轉接（674 宿主、書籤分頁同步）。由 `BookmarkUi.cs` 原本的計時器呼叫。
- 任務寫的 `NativeUi/BookmarkPanel.cs` **不存在**。repo 裡的 `BookmarkPanel` 是 `BookmarkUi.cs` 的 WinForms 控制項，原生的 `BookmarkWindowUI` 窗還沒做，所以「選目的地模式」加在 `BookmarkUi.cs`。

## 怎麼驗證

- 已驗證：`py -3.12 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config` 結果是 `PASS dev`，0 錯誤。只有兩個既有警告：`MagicUi.cs(20)` 的 CS8600／CS8602，不是本次改動造成的。本機沒有 `vendor`，所以用 CI 的 NuGet 設定；lock 沒有寫入，用的是暫時 lock。
- 已驗證：`check-no-game-assets.py` 結果 `status: pass`，errors 0。
- 已驗證（CI，PR #700）：dotnet、map-continuity（含全部 `[WorldModuleTest]`）、npc-offline 三項全綠。
  - 前兩輪各修了一個測試本身的錯：等級公式的測試邊界寫錯；測試走到需要素材的施放閘門／完整快照，改為直接讀能力快照、只測攔截。產品程式碼沒有改。
  - 本機依規定不執行任何編譯出來的程式。

## 未驗證什麼／推測

- **沒做（照任務指示）**：
  - 王族 Lv ≥ 50 且 `byte_13A931C` 的開窗分支，旗標的意義沒查。
  - 「技能索引 ≠ 130」的排除：技能 5／69 的索引是 4／68，碰不到，所以沒寫。
- **推測**：
  - 書籤窗的版面不是原版，用的是 WinForms 分頁。
  - 674 送出後地圖窗不關。
  - 276／743 顯示在技能提示（`MagicNotice`）與召喚提示（`SummonNotice`）；原版的顯示位置（系統訊息欄）沒有逐行追。
- **沒做**：書籤「沒有座標」時送目前地圖、0、0 的分支（原版 `sub_BE0F10`）。3.81 對應的是書籤 id 0，UI 沒有提供這個選項。
- **待驗證（實機）**：
  - 3.81 伺服器收到 `HD` 後確實傳送到該書籤。
  - 戴召喚控制戒指時，`D(action)` 召喚出指定怪物。
  - 674 的回包 `agsel` 要在「和拍賣看板的對話仍有效」時才會顯示：既有 S39 只接受 `InteractionNpc` 相符的頁。對話已關閉時，回包會被忽略。

## 產出路徑

- `engine/source/AbilityStatus.cs`、`RingWindows.cs`（新）、`RingWindowsTests.cs`（新）、`BookmarkUi.cs`、`Magic.cs`、`Pets.cs`、`NativeUi/SummonPanel.cs`、`NativeUi/AgitMapPanel.cs`
- 本報告：`docs/reports/issue-667.md`

## 給下一棒的提示

- CI 綠燈後由 Codex／dot 在 P24Lab 實機驗證，要準備戒指 20288、20284 和至少一個書籤：
  1. 施放 5／69 → 書籤分頁進入選目的地 → 選書籤 → 角色傳送。
  2. 限制地圖顯示 276。
  3. 施放 51 → 召喚窗 → 等級不足時顯示 743。
  4. 拍賣看板「地圖」→ 按 674 → 顯示拍賣資訊頁。
- 674 若因對話已關閉而看不到 `agsel`，可以在 `SendAgitSelect` 送出後讓互動進入等待狀態（`AwaitInteraction`）；這要改 `NpcInteraction.cs`，請另開任務。
- 原生 `BookmarkWindowUI`（`BookmarkUI.xml`，`Bookmark_Window`）做好之後，把 `BookmarkUi.cs` 的選目的地模式搬過去，`TeleportDestinationRequest` 和送出端可以照用。
