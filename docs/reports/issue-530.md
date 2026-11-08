# Issue #530 T223：M7-W 寵物：召喚選擇與指令目標

分支：`task/530`。依據：`docs/reference/gap-matrix.md` 第 7 節 M7-W、`windows.md` §1.3／§1.5、`server-protocol.md` S236／C223／S_ShowSummonList、`docs/design/native-ui.md` §5。

## 做了什麼

### 1. 寵物指定攻擊目標（op 228 → 3.81 S236／C223）：新檔 `engine/source/PetTarget.cs`
- 協定：S236 `C(236) D(id) C0 C0 C2` 解碼（尾段、補零、id≠0 都檢查）；C223 `C(223) D(pet) C0 D(target)`，與 `/check` 用的格式相同。
- 用 M7-A 擴充點 `[WorldReceive(236)]` 接收，不改 `WorldSession.cs`／`Program.cs`：
  - S236 D(自己)：是玩家查詢／掉落查詢的選取模式，**不消化**，維持原本的未處理計數。
  - S236 D(自己的寵物)：進入選目標模式，記下當時的 `TargetRevision`。
  - 其他物件：忽略並計數（快照 `extensions.petTarget.ignored`）。
- 選目標模式下，玩家**之後**再選一次目標（`TargetRevision` 改變；目標不能是寵物本身、自己或死亡物件），就送一次 C223，送完清除。S236 之前就選好的目標不算。
- 地圖／世界更新、寵物消失、`Reset` 都會取消；「取消指定目標」只清本機狀態。
- 送出前後都核對世代、世界 epoch、請求 revision 與目標，並沿用寵物指令的存取條件；`sendMovementGate` 失敗會斷線，不會重送。
- 觸發方式：`Pets.cs` 加 `PetPackets.TargetCommand="attackchr"`（C125）。原服 `L1ActionPet` 只對寵物回 S236，召喚物不處理。

### 2. 召喚怪物選擇（op 52 → 3.81 S39 `summonlist`）：`Pets.cs`
- `ReceivePetDialog` 接收 S39 `C(39) D(objid) S("summonlist")`（後面不可以有多餘欄位）。只有 objid 是自己時才開請求（`Source="server"`）。
- 送出：
  - `server` 請求：C125 `D(自己) S(action)`。原服 `L1ActionPc` 在 `isSummonMonster` 時，把純數字 action 當召喚編號。
  - `local` 請求：選技能 51，再以 `SkillTargetKind.Summon` 施放，C6 尾段帶 `D(action)`。原服 `C_UseSkill` 會發布 summonId，`SUMMON_MONSTER` 在裝備召喚控制戒指時採用。
- action 只接受 1–6 位數字。送一次後關閉請求；地圖切換與 `Reset` 也會關閉。
- 快照：`extensions.summon`。

### 3. 原生 `CSummonUI`：新檔 `engine/source/NativeUi/SummonPanel.cs`
- 實作 `IUiMovablePanel`，Id 是 `SummonMonster_Window`。`UiPanelRegistry.cs` 依 native-ui §5.2 加一行註冊。
- 執行期從 Tile.pak 讀 `SummonUI.xml` 與 `summonList.xml`，不複製任何遊戲資源。
- 版面：
  - 元件位置一律取 XML 座標（相對視窗）。
  - List 依 `Horizontal`／`Vertical`、`Gap`、子 List 的 `Width`／`Height` 與元件底圖尺寸排列。
  - XML 結構缺漏時丟 `UiLayoutException`，不自己補座標。
  - 缺少 surf 時該元件不畫；缺少視窗底圖或排版需要的底圖時，只停用這個面板並記一行日誌。
- 內容與操作：
  - 快速 3 格、12 個等級鈕、每級最多 6 隻（2×3）、魅力標籤（S8 Charisma，沒送就留白）。
  - 確定／取消，以及選了等級後出現的返回鈕；可以拖曳（上方 `Moveable` 高度的區域）。
  - 等級不到的怪物不能選。
- 狀態與動作透過面板自己的 `ISummonUiHost` 轉接，由 `PetUi.cs` 提供，面板不直接碰 `LoginConnection`。

### 4. `PetUi.cs`（舊寵物分頁）
- 新增「指定攻擊目標」按鈕、選目標狀態，以及「取消指定目標」。
- 新增召喚選擇的狀態與關閉鈕。原生 UI 啟動時，另有「開啟召喚怪物選擇」（本機入口，要求已學技能 51）。
- 計時器每格做兩件事：替原生面板掛上宿主；選目標模式下有新目標時送 C223。

### 5. 測試（新檔，由既有 `--module-tests` 入口自動執行，CI「World module extension tests」步驟會跑）
- `engine/source/PetTargetTests.cs`
  - `pet-target`：36 項，涵蓋註冊、C223／C125 位元組、S236 解碼與壞封包、自己／他人／自己的寵物三種分流、只送一次、舊快照拒送、取消、地圖切換、Reset。
  - `summon-request`：17 項，涵蓋 S39 summonlist 開請求、他人 id 忽略、多餘欄位拒收、C125 回覆位元組、送一次、關閉、本機入口未學 51 不送、離線不能開。
- `engine/source/NativeUi/SummonPanelTests.cs` 的 `summon-panel`：離線 56 項，全部用程式合成的 XML 與 `.img`。涵蓋 summonList 解析與錯誤、每個元件的邏輯座標（含拖曳後）、可見性、等級→怪物→確定／返回／取消／關閉流程、按下與放開不在同一個元件時不觸發、宿主不能送出時確定鈕無效、1:1 繪製、缺 surf 不畫、缺視窗底圖時只停用本面板。
  - 只有設定環境變數 `TW_NATIVE_UI_SUMMON_PNG` 時，才會額外用真實客戶端渲染 PNG（另 3 項檢查）。

## 怎麼驗證（全部本機，2026-11-09；已合併最新 origin/main）
- `rebuild.py --update-lock` → 280 個來源檔。
- `rebuild.py --build --configfile .github/nuget.ci.config` → `PASS dev`，productDll `9c7cca6e…cdf818`。worktree 沒有離線 `vendor`，所以照 BUILD.md 改用 CI 的 NuGet 設定。
- `P11.ClientEngine.exe --module-tests` → `pass`，20 組、982 項，失敗 0；其中 pet-target 36、summon-request 17、summon-panel 56。
- DemoChecks：pets `PASS 327`、native-ui-canvas `PASS 429`、native-ui-resources `PASS 539`、player-query（含 hypertext）`PASS 1058`。
- `engine/tools/check-no-game-assets.py` → `pass`，errors 0。
- 真實客戶端渲染（唯讀 `D:\lineage開發\0-客戶端`，PNG 只放在 `scratch/issue-530/`，沒有進 git）：
  - 所有 surf 都解得到，summonList 共 31 隻；等級 28 的第一隻 action 是 `7`，與伺服器 `summonstr_list` 一致。
  - 元件矩形：視窗 239×364，位於 (0,0)。關閉鈕 (225,15)，確定 (60,340)，取消 (127,340)。快速格 (25,67)、(85,67)、(145,67)，每格 60×80。等級鈕從 (25,169) 開始，每顆 86×21，兩欄分別在 x=25、x=119。怪物格從 (25,166) 開始，每格 60×80。
  - **修正一個原本的假設**：等級鈕的底圖本身寫著「等級 72 以上」「等級 68 以上」…「等級 28 以上」，順序是**列優先、由高到低**。程式已改成第 0 鈕＝72、第 11 鈕＝28，測試也一併更新。

## 已驗證／推測／待驗證
- **已驗證**
  - S236、C223、S39 summonlist、C125 的欄位（伺服器原始碼、SP 文件）。
  - 原服 attackchr 只對寵物；ring 路徑走 C6 51 D(summonId)；summonList 的 action 與伺服器表一致。
  - XML 版面座標；等級鈕的順序（看真實底圖文字確認）。
- **推測**（已實作，標在程式註解）
  - 怪物圖示畫在格子左上。
  - 選取框（`OverSurf 5029;4`）畫在怪物圖下面。實際渲染看起來和未選取的格子差不多，`;4` 可能是混色模式。
  - 返回鈕取代取消鈕。
  - 文字顏色用白色。
  - 視窗初始位置用 XML 的 (0,0)。
  - 「下一次選取目標」就送 C223。
- **待驗證（沒有做）**
  - 快速 3 格的內容：只畫 XML 預設的「?」，不能點。
  - Desc tooltip（要字串表 13683–13714）。
  - `DisableOverSurf` 何時使用。
  - ESC 行為。
  - 選目標時的游標外觀。
  - 原版點選目標時是否同時觸發攻擊或移動（引擎點怪物可能照舊攻擊；C223 會在動作空檔才送）。
  - `CSummonUI` 的原版開窗時機（WIN§3）：本伺服器把 S_ShowSummonList 註解掉了，所以 S39 路徑實際上不會觸發。本機入口是過渡用的按鈕，**不是原版行為**。
  - 魅力不足時本機不擋，交給原服判斷（伺服器的消耗表與 XML 的 `minCha` 不同）。

## 沒驗證什麼
- 沒有啟動遊戲、伺服器或 DB；沒有真機驗收召喚、寵物攻擊或原生 UI 的點擊。
- 沒有測到裝備戒指時本機入口的完整 C6 51 施放（離線只測到「未學 51 不送」）。

## 產出路徑
- `engine/source/PetTarget.cs`、`engine/source/PetTargetTests.cs`
- `engine/source/NativeUi/SummonPanel.cs`、`engine/source/NativeUi/SummonPanelTests.cs`
- `engine/source/Pets.cs`、`engine/source/PetUi.cs`
- `engine/source/NativeUi/UiPanelRegistry.cs`（加一行）
- `engine/_integration/dev/rebuild-lock.json`（以 `--update-lock` 重產）
- 本報告

## 給下一棒的提示
- 真機 smoke（dot／P24Lab 測試角色）建議：
  - 寵物指令「指定攻擊目標」→ 點怪物 → 看寵物是否攻擊。
  - 法師裝備召喚控制戒指，用 `--native-ui` 從寵物分頁開召喚窗，選等級 28 的怪 → 確定。
  - 兩項都要抓封包（C125 attackchr、S236、C223、C6 51）。
- `UiPanelRegistry` 目前只有本面板。原生 UI 是全有全無：之後 SummonUI 版面若改壞，會讓整個原生 UI 退回舊 HUD。`summon-panel` 測試與真實渲染（`TW_NATIVE_UI_SUMMON_PNG=<scratch 路徑>`）可以當回歸。
- `OverSurf`／`DisableOverSurf` 的 `;n` 後綴與混色方式，`UiLayout` 目前不支援；其他 XML 面板也會遇到，建議由框架統一處理。
