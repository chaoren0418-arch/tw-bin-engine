# Issue #471 — ui-party：原生 UI 隊伍底板 PARTY_INTERFACE 與右側隊伍／寵物窗

階段：code（第 1 次嘗試，runner claude-local）　分支：`task/471`
依據：`docs/design/native-ui.md` §5.3 ui-party、`docs/reference/ui-layout.md` §6 與 V4／V5 (e)、`docs/protocol/party.md`。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/NativeUi/PartyPanel.cs`（新） | `PARTY_INTERFACE`：3 個分頁鈕（角色 3205／隊伍 3208／寵物 3298，y=481）；隊伍頁畫底板 3204 (0,493)、7 個隊員格（名字＋量表 3211＋紅色 3212）、選取框 3219、離開／踢除／委任 (70／90／109, 574)；寵物頁畫 3301；Alt+F3 循環 `BAR_485`。共用的 `PartyLayout`（版面、量表寬度、按鈕點擊）也在這個檔 |
| `engine/source/NativeUi/PartyRtPanel.cs`（新） | `PARTY_PET_RT_WINDOW`：框 3220 (485,475)，加入 `UiSlotGroup("BAR_485")`（V5 順序：小地圖→擴充巨集→隊伍 RT）；隊伍／寵物分頁 3295／3222、7 格隊員、三個按鈕、寵物頁 3221 |
| `engine/source/NativeUi/State/PartyState.cs`（新） | `IUiPartyState`／`UiPartyGameState`（包裝既有 `PartyView`）、`IUiPartyCommands`／`UiPartyCommands`、`PartyStates.StateOf／CommandsOf`（穿透其他 decorator）；產品端 `ClientForm.NativePartyGameState`（`PartyRead()` = `login.PartySnapshot()`）與 `NativePartyCommands`（`PartyAction` → `SendParty`） |
| `engine/source/NativeUi/UiPanelRegistry.cs` | 加 1 行（兩個面板，layout `uiextra86`，排在 `LEFT_WINDOW` 之後） |
| `engine/_integration/dev/native-ui-tests/PartyPanelChecks.cs`（新） | 離線檢查（分組 `native-ui-party`），選用的真實客戶端渲染 |
| `demo-tests/DemoChecks.csproj`、`demo-tests/Program.cs` | 各加 1 行（新分組，並加入預設全組） |
| `engine/_integration/dev/rebuild-lock.json` | `rebuild.py --update-lock` 重產（288 個來源檔／297 個鎖定檔） |

**範圍外的改動（PR 說明理由）：**
- `UiClientBridge.cs` 2 行：命令改成 `NativeMacroCommands(NativePartyCommands(bridge))`；狀態改成 `WithHpMpState(NativeMacroGameState(new UiLeftStatusGameState(NativePartyGameState(basic), …)))`。隊伍放在最內層，因為 `MacroBook.StateOf`（只解開 HpMp）、`LeftStatusState.From`（只解開 HpMp／Macro）、巨集的 `commands is IUiMacroCommands`（只看最外層）都不認得新的包裝層；放最內層，其他模組的程式完全不用改。
- `State/LeftStatusState.cs` 1 行：`UiLeftStatusGameState` 加 `public IUiGameState Inner`，讓 `PartyStates.StateOf` 能往裡解開。
- `MacroChecks.cs` 1 行：`MacroRegistry` 多一個條件 `Layout=="uistatus86"`。原本它用整個註冊表去建一個只放 `uistatus86-c.xml` 的合成 Tile，新增 `uiextra86` 的註冊後會找不到 XML。
- `.github/workflows/build.yml`：加一個 CI 步驟跑 `native-ui-party`（只用合成資料，做法和 #466 相同）。

## 依據等級

### 已驗證
- XML（本機 `scratch/task448-uixml/uiextra86-c.xml` 唯讀）：底板 3204 `xPos=0 yPos=493`；分頁 `xRPos=11/36/65 yRPos=481`；按鈕 `xRPos=70/90/109 yRPos=574`；RT 框 3220 `xPos=485 yPos=475`。測試裡的合成 XML 照抄這份結構，包括重複的 `SURF_IMAGE`。
- 真實客戶端尺寸（`TW_CLIENT_ROOT` 檢查通過）：3204 是 135×107，3220 是 158×125，3211 是 60×10，3212 是 58×8。
- 量表寬度：紅條寬度 = `floor(58·hp/100)`。100→58、50→29、99→57、1→0、0→0；伺服器沒送 HP（`null`，例如只從 S39 名單得知的隊員）時**不畫紅條**，不推算。
- 按鈕只呼叫既有動作：離開 → `"leave"`、踢除 → `"kick"`（帶選取的隊員名，限 `CanKick`）。全部走既有的 `ClientForm.PartyAction` → `LoginConnection.SendParty`，所以 generation／epoch／revision 的防重播檢查照舊。
- 上下層次：在合成檢查中，角色頁的 Bounds 只有分頁列，`LEFT_WINDOW` 照常可點；隊伍頁或寵物頁的底板蓋住 (0,493)–(134,599)，y<493 仍是左狀態框。真實渲染 `party-lt-party.png` 目視確認：分頁鈕剛好落在 3168 頂部的凹槽。

### 推測（集中在常數，查證後只改一處）
- **隊員格版面**：2 欄 × 4 列、每格 64×27（與選取框 3219 同尺寸），依序對應「Party 1…7」，第 8 格放按鈕。LT 原點是底板＋(4,0)、間距 64×26；RT 原點是框＋(15,18)、間距 65×26。這是從真實 3204／3220 圖的格線像素量出來的（`scratch/t471/grid.py`），**沒有追 BIN 的繪製函式**。旁證：三個按鈕剛好落在第 8 格內。
- **格內位置**：名字在格＋(3,2)，白字黑影；量表在格＋(2,15)；紅條在量表＋(1,1)。
- **RT 子元素座標相對於框 (485,475)**：XML 寫 `x=0 y=0`，但算出來的分頁 y=481 與 LT 分頁 y 相同，按鈕也剛好落在 3220 圖的第 8 格，所以這樣取。
- **STAGE3_BTN**：按下時用 a+1，選中的分頁也用 a+1，套用 V4 對 `LImageButton3Stage` 的結論。滑過（hover）圖沒有畫。
- **隊員順序**：照 `PartyView.Members`（`Party.cs` 依名字排序）；原版應是加入順序，但模型裡沒有這個資訊。
- **RT 量表**：RT 自己的 XML 沒有 3211／3212，所以借用 `PARTY_INTERFACE` 的。

### 待驗證／沒做
- **委任（delegate）**：既有程式沒有轉移隊長的動作（C230 type=3，`docs/protocol/party.md` 明寫本專案不提供），所以按鈕只會畫出來、點了不送任何封包。要做的話需另開任務。
- **寵物頁**：只畫底圖 3301／3221，**沒有寵物清單**。`Pets.cs` 的接線不在本任務範圍。RT 的「沒有隊員但有寵物時自動切到寵物頁」（V5 (e) mode 2）也沒有做。
- **灰色量表 3225** 的意義（離線？在其他地圖？）沒有查到，所以從不畫。
- **Alt+F3**：放在 `PartyPanel.Key`，因為進入世界時它一直可見；RT 面板在不是目前顯示的槽位成員時是隱藏的，收不到按鍵。WinForms 會不會把 Alt+F3（WM_SYSKEYDOWN）送進原生按鍵路徑**沒有驗證**，要等 ui-native-smoke。之後若有共用的槽位切換處，應該搬過去，避免重複處理。
- 隊伍通知文字（`PartyView.Notice`）、邀請的接受／拒絕、邀請鈕：原版這兩個視窗沒有對應的元素，所以沒有畫。邀請流程仍走既有的 Yes/No 與開發工具。
- 原版的名字字型、顏色未知（V4 (f)），沿用 `UiText`。
- 真機（P24Lab）沒有測。

## 怎麼驗證

1. `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock` → 288 個來源檔／297 個鎖定檔；`--build --configfile D:\lineage開發\TW1608262200研究\engine\NuGet.Config` → **`PASS dev`**，productDll `dcfc5c00…52c7`（同步 origin/main，含 #464 之後）。
2. `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-471/party-checks.json native-ui-party`
   - 沒設 `TW_CLIENT_ROOT`：**PASS 396**（真實渲染 SKIP）。
   - 設 `TW_CLIENT_ROOT=D:\lineage開發\0-客戶端`：**PASS 398**。
   - 檢查內容：LT／RT 幾何、7 格與按鈕在第 8 格內、量表寬度、HP 未知不畫、分頁切換（角色／隊伍／寵物、RT 隊伍／寵物）、與 `LEFT_WINDOW` 的命中與像素層次、註冊表中 `LEFT_WINDOW` 排在 `PARTY_INTERFACE` 之前、選取與清除、leave／kick 呼叫、委任與無選取／非隊長／忙碌／無隊員時不送、按下後在別處放開不送、`BAR_485` 成員順序與 Alt+F3 循環（不吃連發、不吃單獨 F3）、產品的四層包裝下隊伍／巨集／左狀態都找得到、缺 surf 時不畫、XML 缺節點時拒絕載入。
3. 其他原生 UI 分組（設 `TW_CLIENT_ROOT`）：resources **PASS 550**、canvas **429**、left-status **371**、macro **405**、hpmp **398**。
4. 完整預設全組：在 `Movement123` 中止，原因是 `engine\assets\terrain0.bin` 不存在。worktree 沒有 `engine/assets` junction，與本任務無關；在它之前的原生 UI、小地圖、CharacterConfig、Hotbar、NativeTargetUi 各組都已執行通過。**完整全組待 CI 或主專案目錄確認。**
5. `git ls-files` 沒有新增任何 `.img`、`.xml`、`.png` 或二進位檔。PNG 只在 `scratch/issue-471/real/`。

### 真實渲染座標表（`scratch/issue-471/real/party-coordinates.json`）

| 元件 | 邏輯座標 |
|---|---|
| LT 分頁（3205c／3208c／3298c） | (11,481,27×11)、(36,481,29×11)、(65,481,27×11)（台灣版 `c` 圖比無後綴版寬 1–2 px） |
| LT 底板 3204 | (0,493,135×107) |
| LT 隊員格 0–6 | x=4／68，y=493／519／545／571，64×27 |
| LT 量表 | 格＋(2,15)，60×10；紅條 58×8 |
| LT 按鈕 | (70,574)、(90,574)、(109,574)，20×22 |
| RT 框 3220 | (485,475,158×125) |
| RT 分頁 | (496,481)、(521,481) |
| RT 隊員格 0–6 | x=500／565，y=493／519／545／571 |
| RT 按鈕 | (567,575)、(587,575)、(607,575) |
| RT 寵物底 3221 | (485,493) |

PNG：`party-lt-party.png`（左狀態框＋隊伍頁、RT 隊伍）、`party-lt-char-rt-party.png`（角色頁：左狀態框完整露出）、`party-pet.png`。目視比對：量表、名字、按鈕都在 3204c／3220 的格框內，分頁鈕位於 3168 頂部凹槽。

## 產出路徑
- 程式：`engine/source/NativeUi/PartyPanel.cs`、`PartyRtPanel.cs`、`State/PartyState.cs`
- 測試：`engine/_integration/dev/native-ui-tests/PartyPanelChecks.cs`（分組 `native-ui-party`）
- 報告：`docs/reports/issue-471.md`
- 本機證據（不進 git）：`scratch/issue-471/real/*.png`、`party-coordinates.json`；格線量測 `scratch/t471/`

## 給下一棒的提示
- **ui-native-smoke（dot）**：開 `nativeUi`，用 2 個測試角色組隊。確認以下各項：
  - 隊伍頁出現隊員與 HP 紅條。
  - 被打時紅條縮短（S237）。
  - 離開、踢除（隊長身分）有效。
  - 委任不送任何東西。
  - Alt+F3 能把 (485,475) 切到隊伍 RT。
  - 截圖與原版比對隊員格內名字和量表的位置；若不同，只改 `PartyLayout.GaugeOffset／NameOffset` 與兩個面板的 `CellOrigin／CellPitch`。
- **V4 補查**：`STAGE3_BTN` 的狀態順序（a+1 是按下還是滑過）、3225 灰量表的用途、隊員格的 BIN 繪製位置（`PartyUI` VA `0x9570C0`／`0x5564C0`，見 gap-matrix）。
- **後續任務候選**：
  - 委任（C230 type=3）的送出路徑：需要協定任務。
  - 寵物頁清單接 `Pets.cs`。
  - RT「沒有隊員但有寵物時自動切寵物頁」。
  - 若 ui-minimap 也要處理 Alt+F3，請改成共用一處（目前在 `PartyPanel.Key`）。
- 新模組若再包一層 game-state decorator，請放在**最內層**，並在 `PartyStates.StateOf`、`LeftStatusState.From`、`MacroBook.StateOf` 中檢查能否穿透；不然就照本任務的做法，加 `Inner` 存取子。
