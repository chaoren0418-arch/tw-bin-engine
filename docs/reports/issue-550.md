# Issue #550 T232：S187 盟屋地圖窗（AgitMapWindow，W2-C）

## 做了什麼

- **新檔 `engine/source/NativeUi/AgitMapPanel.cs`**：舊式 `AgitMapWindow` 的原生面板 `AgitMap_Window`（`[UiPanel(150)]` 自動註冊；沒有 XML，照 HpMpPanel 的做法掛在 `uistatus86`，版面不讀 XML）。
  - 版面依 `docs/reference/windows-2.md` 第 5.2 節與 `docs/reference/sprites-and-strings.md` 第 2 節（T267／#628）的 BIN 數值：
    - 地圖 surf（`dword_135DA34`，VA `0x135DA34`／FO `0xF5C234`）：區域 4＝689、5＝741、6＝741、7＝801、8＝1482，區域 1 同 8；區域 0、2、3、≥ 9 不開窗。
    - 標記位置表（靜態初始化 `sub_113F600`，VA `0x113F600`）寫成程式內常數 `AgitMapLayout`：區域 4／5／6／7／8 各 45／11／0／67／6 筆，區域 1 用區域 8 的表；超出有資料筆數的編號畫在 (0,0)（照原版）；編號不在 1–70 不畫標記。
    - 標記 surf 176，區域 7 用 34；閃爍亮 800 ms／暗 200 ms。
    - surf 160 (312, 56)、164 (312, 110)；按鈕 678 (213, 297)＝關窗、674 (90, 299)。
    - 視窗原點 (0, 0)（UI 原點偏移 `dword_154524C／dword_1545250` 在 800×600 下為 0，ui-layout.md V5）。
  - 缺任一 surf → 面板停用並記一行日誌（規則 5，不畫替代圖）。
  - 678 關窗後呼叫 `ReturnGameFocus`；同一次 S187 關掉後不重開，下一個 S187 才重開；離開世界或 `WorldSession.Reset` 都會關窗。
- **`engine/source/Agit.cs` 只加開窗事件**：新增 `AgitMapOpenRequest`（單調 Revision＋`AgitHouseMap`）與靜態 `AgitMapWindow`（`Request`／`Clear`／`Current`）。`ReceiveAgitHouseMap` 收到 S187 時呼叫 `AgitMapWindow.Request`，`ResetAgit` 呼叫 `Clear`。快照 `agit` 多一欄 `mapWindowRevision`，`ui` 字串改寫。
  - 為什麼是靜態：面板由 `UiRoot` 建立，拿不到 `WorldSession`；本任務不可改 `ClientForm`／`WorldSession.cs`。面板在 Update 時輪詢 `Current`（不訂閱事件，不會洩漏）；測試可注入自己的來源。
- **新檔 `engine/source/NativeUi/AgitMapPanelTests.cs`**：`[WorldModuleTest("agit-map-panel")]`，51 項，接 `--module-tests` 入口（CI「World module extension tests」會跑）。全部用合成單色 surf。

## 怎麼驗證（已驗證）

- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build` → `PASS dev`（productDll `cd57609e…d6ca`）。依 #646 政策 PR 不改 `rebuild-lock.json`，建置以暫時 lock 進行（新增 2 檔、改 1 檔）。
- `P11.ClientEngine.exe --module-tests <out>` → `pass`，39 組、1829 項、失敗 0（agit 44、agit-map-panel 51、auto-discovery 28）。
- `DemoChecks native-ui-canvas` → `PASS 429`。
- `engine/tools/check-no-game-assets.py` → `pass`（3940 檔、0 錯誤）。
- CI：PR 開出後由 GitHub Actions 驗證。
- worktree 原本沒有 `engine/vendor`：從主目錄複製離線 `microsoft.web.webview2.1.0.4258.31.nupkg` 到 worktree 的 `engine/vendor/`（不追蹤），沒有改 NuGet 來源。

## 推測（已在程式註解標明）

- 閃爍相位：以開窗那一格為 0，每 1000 ms 前 800 ms 亮（原版開窗時排 1000 ms 計時，初始旗標值未查）。
- Bounds＝地圖與四張圖的聯集；按鈕須「按下與放開都在同一鈕」才作用；視窗不可拖曳。

## 未驗證／沒做

- 674 鈕（`sub_543810`）的作用沒查：畫出、可點，但不接動作（待驗證）。
- 按鈕的按下圖、ESC 行為：未查，不做。
- 3.81 伺服器實際送的盟屋編號（DB `house` 表）：依規定不查 DB，待驗證。
- 真機畫面（真實 surf 渲染、與原版對照）：本任務不啟動遊戲，未做。

## 產出路徑

- `engine/source/NativeUi/AgitMapPanel.cs`、`engine/source/NativeUi/AgitMapPanelTests.cs`、`engine/source/Agit.cs`
- 本報告 `docs/reports/issue-550.md`；分支 `task/550`

## 給下一棒的提示

- 真機 smoke（dot，P24Lab）：開 `--native-ui`，對盟屋拍賣 NPC 觸發 S187，核對地圖 surf、標記位置與閃爍、右側兩張圖、678 關窗。
- 若要接 674 鈕：先反編譯 `sub_543810`（VA `0x543810`），再在 `AgitMapPanel.Mouse` 的 `"action"` 分支接動作。
- 若之後 `ClientForm` 有正式的 UI 轉接層，可把 `AgitMapWindow` 靜態來源換成注入（建構子已接受 `Func<AgitMapOpenRequest?>`）。
