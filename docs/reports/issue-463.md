# Issue #463 — ui-core-canvas（C2）：原生 UI 畫布、縮放、輸入對映、IUiPanel 契約與 nativeUi 開關

階段：code，第 1 次嘗試；runner：claude-local；分支 `task/463`。
依據：[`docs/design/native-ui.md`](../design/native-ui.md) 第 1.1、3、5.1 節；世界範圍採 [`ui-layout.md`](../reference/ui-layout.md) V5 (c) 的建議值，槽位順序採 V5 (e)。

## 做了什麼

### 新檔（`engine/source/NativeUi/`，命名空間 `P06.NativeUi`）

| 檔案 | 內容 |
|---|---|
| `UiContracts.cs` | `UiLayer`（World／Bar／Window／Float）、`UiMouseEvent`、`UiKeyEvent`、`IUiPanel`（與設計 §5.1 相同的 9 個成員）、選用的 `IUiMovablePanel`（可拖曳視窗：`IsDragHandle`、`MoveTo`）、`IUiGameState`（InWorld、角色名、等級、HP／MP，伺服器沒送就是 null）、`IUiCommands`（`ShowDevTools`、`ReturnGameFocus`、`Logout`，都轉呼叫既有動作）、測試用的 `UiStaticGameState`、`UiNullCommands` |
| `UiRoot.cs` | `LogicalWidth/Height`=800×600、`Canvas`、**`WorldRect`＝(0,0,800,600)**（V5 (c) 未定案，只改這一行）、`PinMismatchMessage`。面板註冊、圖層與視窗 z-order、`HitTest`（由浮動層往下）、`BringToFront`、`Update`、`Compose`（黑底 → 世界畫在 WorldRect → 各面板裁切在自己的 Bounds）。模組在 Update／Paint／輸入時丟例外：只停用該面板並記一行，不讓客戶端當掉。`VerifyResources`：Tile 與 17 個 Sprite 的 idx 全部載入，每個 pak 讀一筆（強制核對 pak 大小與 SHA-256），再解析 SurfList。`Build`：驗證 → 依註冊表建立面板 → 載入 XML → 全部成功才回傳。`UiOptions`：開關解析與 `--native-ui` 參數處理。`UiStartState`（Off／Verifying／Active／Failed） |
| `UiCanvas.cs` | 800×600 32bpp PArgb 離屏畫布；`Restrict(rect)` 設裁切；`DrawSurface(null)` 不畫（不畫替代色塊）；最近鄰、1:1 |
| `UiPresenter.cs` | `UiViewport`（倍率、偏移、`ToLogical`／`ToLogicalClamped`／`ToClient`）。整數倍：`s=max(1,floor(min(W/800,H/600)))`、`ox=(W-800s)/2`、`oy=(H-600s)/2`；選用等比填滿：`s=min(W/800,H/600)`。`Present`：黑邊、`NearestNeighbor`＋`PixelOffsetMode.Half`，整張一次 `DrawImage` |
| `UiInput.cs` | client → 邏輯座標 → 命中測試 → 面板，否則交給世界。黑邊輸入丟棄（UI 與世界都收不到）。面板處理了按下事件就捕捉滑鼠直到放開（捕捉期間座標夾在畫布內）。拖曳以邏輯座標移動，並把視窗整個限制在 800×600 內。按鍵：先給焦點面板，再由上往下給其他可見面板，都沒處理就回到遊戲 |
| `UiPanelRegistry.cs` | 唯一的整合點，目前是空表；每個模組任務在這裡加一行 |
| `UiSlotGroup.cs` | `BAR_485`（485, 475）：一次只顯示一個成員；成員依 V5 (e) 模式順序排列（小地圖 → 擴充巨集 → 隊伍／寵物 → ChatFrameSub），與註冊順序無關；`Reset` 回到模式 0、`Cycle` 依 Alt+F3 的順序切換 |
| `UiText.cs` | 唯一的文字入口：`新細明體 9pt`、`SingleBitPerPixelGridFit`，畫在邏輯畫布上，跟著像素縮放 |
| `UiClientBridge.cs` | `ClientForm` 的 partial：開關、背景驗證、繪製與輸入分流、退回時的錯誤行。**不在設計列出的檔案內**，理由：Program.cs／PlayerHudIntegration.cs 只能加分支，所以把實作集中在 NativeUi 目錄下的這個檔案 |

### 既有檔案（只加開關分支）

- `Program.cs`：`Main` 先用 `UiOptions.Strip` 移除 `--native-ui`（正常入口仍然看到「沒有參數」）；建構子最後呼叫 `InitNativeUi()`；`OnPaint`、`MouseDown/Move/Up/Wheel`、`KeyUp`、`ProcessCmdKey` 各加一個分流；`ClearInput` 加 `ResetNativeUiInput()`；`CaptureLogicalScene` 在原生 UI 啟用時改回傳合成後的畫布（世界＋面板，縮放前）。
- `PlayerHudIntegration.cs`：`PlayerHudActive` 加 `&&!NativeUiActive`（舊 HUD、小地圖、隊伍、狀態圖示、F 鍵都依這個值，所以原生啟用時全部隱藏、收不到輸入）；`PlayerSceneBounds` 在原生啟用時回傳 `UiRoot.WorldRect`。
- 測試接線：`DemoChecks.csproj` 加 1 行、`demo-tests/Program.cs` 加分組 `native-ui-canvas`（也加進預設全組）、`.github/workflows/build.yml` 加一步（理由同 C1：讓合成測試在 CI 上跑，這一步不讀客戶端）。
- `rebuild-lock.json`：同步 origin/main 後用 `--update-lock` 重產。

### 開關的行為

- **預設關閉。** 打開的方式：`--native-ui` 參數，或 `<stage>/runtime/native-ui.json` 寫 `{"nativeUi":true}`（同一檔的 `"nativeUiFill":true` 開啟等比填滿）。格式錯誤或值不是 `true` 都當作關閉。
- 打開後：客戶區設為 800×600、`MinimumSize` 為 800×600；在背景執行 `UiRoot.Build`（含全部 pin 驗證）。
  - **驗證中**：舊 HUD 照常運作。
  - **成功**：切到原生畫布。世界用既有的 `PaintScene` 畫在 WorldRect（`Camera.View`＝800×600，不隨視窗大小改變），再疊面板，最後整張縮放呈現。進到世界的滑鼠事件已經換成邏輯座標，原本的點地、點物件、點 NPC 流程不用改。酒醉的逐列扭曲改在世界層做（原生模式不呼叫 `base.OnPaint`，舊的扭曲處理不會執行），所以不會扭到 UI。
  - **失敗**：留在舊 HUD，畫面左上顯示一行紅字「原客戶端 UI 資源與鎖定版本不符」（`UiResourceException`）；其他例外則顯示「原生 UI 無法啟動，已使用舊介面：…」。詳細原因寫進 `UiResourceLog` 與 `window-events.json`。

## 怎麼驗證

1. **合成離線測試** `native-ui-tests/CanvasChecks.cs`（fixture 由程式產生，寫在 `scratch/issue-463/synthetic/`，不讀客戶端）：
   `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-463/canvas-checks.json native-ui-canvas` → **PASS 429**（其中約 100 項是本分組，其餘是每次都跑的 party／pets／polymorph 基本組）。內容：
   - **驗收倍率與偏移**：800×600 → s=1、(0,0)；1600×1200 → s=2、(0,0)；1920×1080 → s=1、(560,240)。另測 1700×1300（2×、(50,50)）、2400×1800（3×）、2560×1440（2×、(480,120)）、1599×1199（1×）、小於 800×600（s=1，不當掉）；填滿 1920×1080 → s=1.8、1440×1080、(240,0)。
   - **座標對映**：四角、`floor`、黑邊回傳 null、2× 與偏移、填滿模式的邊界；捕捉期間夾在畫布內。
   - **呈現像素**：在 800×600／1600×1200／1920×1080／1700×1300 四種大小下，每個邏輯像素都變成剛好 s×s 的同色方塊，鄰點不滲色，四周全黑，沒有殘留舊畫面。
   - **圖層與命中**：註冊順序打亂，仍是 bar < window（z-order）< float；`BringToFront` 同時改變繪製與命中順序；不可見面板不畫、收不到輸入；邊界依 `Rectangle.Contains`。
   - **合成**：世界畫在 WorldRect＝800×600；面板就算想畫滿整張也被裁在 Bounds 內；面板 Paint 丟例外 → 只停用該面板並記錄，整幀照常完成，之後也不再命中。
   - **輸入**：黑邊點擊 UI 與世界都收不到；沒命中 → 世界收到邏輯座標；命中但沒處理 → 世界仍然收不到、不捕捉；處理了 → 捕捉＋焦點＋提到最上層；放開後恢復給世界；按鍵先給焦點面板、再由上往下，都沒處理時回到遊戲（F5 會留給遊戲）；點世界會清掉 UI 焦點。
   - **拖曳**：依邏輯位移移動；往右下、左上拖過頭時分別停在 (600,450)、(0,0)；2× 下 20 client px＝10 邏輯 px；比畫布大的視窗固定在 (0,0)；`Reset`（失焦）取消拖曳與捕捉。
   - **BAR_485**：成員依 V5 順序、`Reset` → 小地圖、`Cycle` 0→1→2→0、只有目前成員會被命中、重複或不存在的成員會被拒絕。
   - **開關**：預設關閉；設定檔開啟、填滿、非布林值或格式錯誤視為關閉；`--native-ui` 優先；`Strip` 之後 `ProductEntry.Normal` 仍為 true。
   - **開關關閉時 ClientForm 的分支不起作用**：滑鼠事件原樣通過（同一個物件、client 座標）、按鍵不被拿走；狀態為 Active 時 `PlayerSceneBounds`＝WorldRect；Failed 不算 Active。
   - **Build 與 pin**：合成的 Tile＋17 個 Sprite → 面板從自己的 XML 取 x/y 與 surf，1:1 畫在 (0,475)；註冊 id 與面板 id 不符、XML 不存在都整個拒絕；任一 Sprite pak SHA 不符、Sprite idx SHA 不符、Tile pak SHA 不符、缺 Sprite idx，`VerifyResources` 都丟 `UiResourceException`（呼叫端據此退回舊 HUD）。
   - **文字**：只畫在裁切區內，像素只有純白／純黑（沒有反鋸齒）。
2. **開關關閉時既有測試不變**（同步 main 之後重跑）：native-ui-resources 539、map-continuity 32953、combat-admission 1686、item-status 409、afflictions 580、resurrection 553、polymorph-cadence 380、buddies 496、item-transfer 400、doors 648、player-chat 138、mail 119、weapon-repair 98、combat-sfx selection 4 cases／46 項、web-resource product，**全部 PASS**。數字與 C1 報告相同（resurrection 是這次 main 新增的分組）。
3. `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock`（240 個來源檔／249 個鎖定檔）與 `--build` → **`PASS dev`**，productDll `185176e5…ca39`。本機 worktree 沒有 `engine/vendor`，所以 `--build` 加了 `--configfile D:\lineage開發\TW1608262200研究\engine\NuGet.Config`（唯讀引用主專案的離線套件來源）。
4. `engine/tools/check-no-game-assets.py` → `pass, errors 0`。新增的只有 `.cs`。
5. **本機選用檢查**（真實客戶端、唯讀；程式與輸出都在 `scratch/issue-463/real-probe/`、`scratch/issue-463/real/`，不進 git）：
   - `UiRoot.VerifyResources` 對真實 `Tile` 與 17 個 `Sprite*` 全部通過，**冷啟動耗時 70.4 秒**（雜湊所有 pak）。
   - 用真實 surf 3168／3279／3176 放在 XML 的 x/y 合成畫布，再以 800×600、1600×1200、1920×1080 呈現。目視 `present-1920x1080.png`：800×600 的畫面置中於 (560,240)，四周全黑，左狀態框、聊天框、巨集框以 1:1 原圖顯示。這只是框架的煙霧測試，不是模組實作（聊天框的底部錨定由 ui-chat-window 負責）。
   - 另外量到：GDI+ 加上 `ImageAttributes`（TileFlipXY）時，1:1 輸出會被混色（255→253、255→248）。所以 `Present` 不用 ImageAttributes，改用單純的最近鄰＋Half，四種大小都逐像素正確（`scratch/issue-463/gdi-probe/`）。

## 已驗證／推測／待驗證

**已驗證（本輪，離線）**
- 驗收要求的三種視窗大小，倍率與偏移都正確；黑邊點擊不會傳遞；拖曳限制在畫布內；pin 不符時 `Build` 拒絕。
- 開關關閉時，既有 15 組測試的數字與合併前相同；ClientForm 的滑鼠與按鍵分支不起作用。
- 真實客戶端資源通過全部 pin；冷啟動驗證約 70 秒。

**推測**
- 世界層畫滿 800×600、由底列蓋住（V5 (c) 待驗證；V5 建議先用這個值）。
- 字型 `新細明體 9pt`（V4 (f) 待驗證）。

**待驗證／未做**
- **沒有任何真機驗收**：沒有啟動客戶端，也沒有在 P24Lab 開 `--native-ui` 看過畫面。開關打開時的實際繪製、滑鼠點地、縮放後的手感，要等 ui-native-smoke（dot）。
- 註冊表目前是空的，所以開關打開並驗證成功後，畫面只有世界（沒有任何底列）。這是預期的，等模組任務加入。**在模組合併之前，不要對使用者開這個開關。**
- 與設計不同之處：設計說「開關打開時舊 HUD 控制項不建立」，本次是**建立但隱藏、收不到輸入**（`PlayerHudActive` 為 false），這樣 pin 不符時可以直接退回，不必重建開發工具的分頁。兩套 UI 不會同時收到同一個按鍵。
- 原生模式下 `Camera.Zoom` 沿用現值（預設 1.5，滾輪可調）。原版是 1:1，要不要鎖在 1.0，交給主控或 smoke 決定。
- 背景驗證期間（約 70 秒）是舊 HUD，驗證完才切到原生畫面，進遊戲途中可能會切換一次。

## 產出路徑

- `engine/source/NativeUi/UiContracts.cs`、`UiRoot.cs`、`UiCanvas.cs`、`UiPresenter.cs`、`UiInput.cs`、`UiPanelRegistry.cs`、`UiSlotGroup.cs`、`UiText.cs`、`UiClientBridge.cs`
- `engine/source/Program.cs`、`engine/source/PlayerHudIntegration.cs`（只加分支）
- `engine/_integration/dev/native-ui-tests/CanvasChecks.cs`；`demo-tests/DemoChecks.csproj`、`demo-tests/Program.cs`（分組 `native-ui-canvas`）
- `.github/workflows/build.yml`（新增一步）、`engine/_integration/dev/rebuild-lock.json`（重產）
- 本機證據（不進 git）：`scratch/issue-463/canvas-checks.json`、`group-*.json`、`real/real-probe.txt`、`real/*.png`、`real-probe/`、`gdi-probe/`

## 給下一棒的提示

- **模組任務（ui-left-status 等）**：
  - 實作 `IUiPanel`，在 `UiPanelRegistry.Panels` 加**一行**：`new("LEFT_WINDOW","uistatus86",root=>new LeftStatusPanel())`。`Layout` 填 XML 主檔名，`-c` 由 UiXmlSource 處理。
  - `Load` 只讀 XML 與 surf；找不到節點就讓 `UiLayoutException` 往外丟（整個原生 UI 會退回舊 HUD），不要自己補座標。
  - `Paint` 只用 `canvas.DrawSurface`／`DrawText`。畫布已經裁在你的 `Bounds`，surf 為 null 就什麼都不畫。
  - BAR_485 的三個模組：建構時 `root.SlotGroup(UiSlotGroup.Bar485).Add(Id)`，`Visible => group.IsShown(Id)`。Alt+F3 切換放在某個模組的 `Key`，呼叫 `Cycle()`。
  - 需要更多狀態時，在 `NativeUi/State/<Module>State.cs` 加轉接。`IUiGameState`／`IUiCommands` 加成員時，要同時改 `UiClientBridge.cs` 的實作和 `UiNullCommands`。
  - 可拖曳視窗實作 `IUiMovablePanel`，限制在畫布內由 UiInput 處理。
- **效能**：冷啟動 pin 驗證約 70 秒。建議另開任務：把「pak 大小＋修改時間 → SHA 已驗證」記在 `%LOCALAPPDATA%\tw-bin-engine\ui-cache\`，或只在第一次讀到某個 pak 時才驗證（C1 本來就是這樣），驗證期間先顯示原生畫面。
- **smoke（dot）**：產品主程式加 `--native-ui` 參數（launcher 是否轉傳參數未查），或在 `<stage>/runtime/native-ui.json` 寫 `{"nativeUi":true}`。等 70 秒後切換，再看 800×600 置中與 2× 縮放；`Ctrl+Shift+F12` 會輸出 `-logical.png`（縮放前的合成畫布）。
- **V5 (c) 定案後**：只改 `UiRoot.WorldRect`。
