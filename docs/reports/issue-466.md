# Issue #466 報告：ui-macro-bar（原生巨集框 F5–F12、3 個分頁、鎖定、擴充巨集）

日期：2026-11-09。runner：claude-local。分支：`task/466`。
依據：`docs/design/native-ui.md` §5.3 ui-macro-bar；`docs/reference/ui-layout.md` §2（RIGHT_WINDOW 子元件）、V4（按鈕圖）、V5 (a)(b)(e)（格距、F 鍵、BAR_485）。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/NativeUi/MacroPanel.cs`（新） | `RIGHT_WINDOW` 巨集框：底圖 3176、8 格（4 欄 × 2 列、格距 34）、3 個分頁鈕（`TAB_BUTTON` action 10–12）、選取框 2331、`MACRO_LOCK`。F1–F3 切分頁，F5–F12 使用目前分頁的第 0–7 格；單擊選格，雙擊使用；點鎖定鈕切換鎖定。所有座標都從 XML 讀取（`x`/`y`＋`xRPos`/`yRPos`），只有格距 34 與 4 欄是 V5 查到的 BIN 常數 |
| `engine/source/NativeUi/ExMacroPanel.cs`（新） | `EX_MACRO_WINDOW`：底圖 3226、12 格（4 × 3），圖示原點為 `EX_MACRO_FRAME`＋(10, 20)（V5 BIN 常數）。加入 `UiSlotGroup("BAR_485")`，只在它是目前成員時顯示 |
| `engine/source/NativeUi/State/MacroState.cs`（新） | `MacroBook`（每個角色 3 頁 × 8 格＋擴充 12 格、目前分頁、選取格、選取框在哪個框、鎖定）；`IUiMacroState`／`IUiMacroCommands` 轉接介面；`UiMacroGameState`／`UiMacroCommands` 裝飾器；產品端 `ClientForm` 部分類別（建快照、第 1 頁走既有 `CharacterConfigHud` 存檔、啟動走既有 `ActivatePlayerHud` → `PlayerHudActions.Activate`） |
| `engine/source/NativeUi/UiPanelRegistry.cs` | 加一行（同一行內兩筆註冊：`EX_MACRO_WINDOW` 在前、`RIGHT_WINDOW` 在後，原因見下） |
| `engine/source/NativeUi/UiClientBridge.cs` | **任務清單外的檔案，改 2 處、各 1 行**：`NativeUiGameState()` 的回傳包上 `NativeMacroGameState(...)`；`new UiInput(...)` 的 commands 包上 `NativeMacroCommands(...)`。理由：C2 的 `IUiGameState`／`IUiCommands` 沒有道具、魔法、快捷綁定與 `ActivateSlot`，`NativeUiCommands` 是 private sealed 類別，模組檔案無法接上。這是讓「原生模式下 F5–F12 觸發格子」能在產品裡生效的最小改動；開關關閉時這兩行不會執行 |
| `engine/_integration/dev/native-ui-tests/MacroChecks.cs`（新） | 離線檢查（合成 Tile／Sprite／XML／`.img`、合成道具與魔法）；另有選用的真實客戶端渲染 |
| `demo-tests/DemoChecks.csproj`、`demo-tests/Program.cs` | 各加 1 行（分組 `native-ui-macro`，也加進預設全組） |
| `.github/workflows/build.yml` | 加一個 CI 步驟跑 `native-ui-macro`（合成資料，不讀客戶端；做法同 C1／C2） |
| `engine/_integration/dev/rebuild-lock.json` | `rebuild.py --update-lock` 重產 |

### 疊放順序（registry 順序的理由）
`MACRO_LOCK` 位於 `xRPos=-9`，也就是 (634, 513)，蓋在 `BAR_485` 槽位（485–642）最右邊 9 欄上。所以 `RIGHT_WINDOW` 的 `Bounds` 是「底框 ∪ 鎖定鈕」＝(634, 476, 166, 124)，而且必須疊在 BAR_485 成員上面。同一層內「後註冊＝在上」，所以 registry 中 `EX_MACRO_WINDOW` 放前面。**之後的 `MINI_MAP_WINDOW`、`PARTY_PET_RT_WINDOW` 也要註冊在 `RIGHT_WINDOW` 那一行之前**，否則鎖定鈕會被蓋住、點不到。

## 舊 F1–F4 綁定的移轉規則

| 來源 | 原生巨集框 |
|---|---|
| TWCC v1 count 8（T176 之後，F5–F12） | 第 1 頁 F5–F12，逐格對應 |
| TWCC v1 count 4（T138 時期，當時顯示為 F1–F4） | 第 1 頁 **F5–F8**；F9–F12 空白。下次在第 1 頁改動時，以 8 格重寫（沿用 `CharacterConfig` 既有規則） |
| 格式不明（非 TWCC、count 不是 4／8、長度不符） | 不移轉、不覆寫，伺服器上的原 blob 保留（沿用 `CharacterConfigHud`） |
| 第 2、3 頁與擴充巨集 | 永遠不從存檔填入（TWCC v1 沒有位置） |
| F1–F4 鍵本身 | 原版不是巨集鍵（V5 (b)）：原生模式下 F1／F2／F3 改為切換分頁 1／2／3；F4（撿物）不由本模組處理 |

`MacroBook.MigrateStored` 寫明這套規則（實作就是 `CharacterConfig.Slots`，count 4 → 前 4 格）。產品端由既有 `CharacterConfigHud` 在登入時還原到第 1 頁、變動時存回；本任務沒有改 `CharacterConfig.cs` 或封包。

## 原版 protobuf 格式與 TWCC 的差異（不在本任務實作）

| 項目 | 原版（`Lineage.ClientConfig`，protobuf） | 引擎（TWCC v1） |
|---|---|---|
| 格式 | protobuf message；欄位定義**未查證** | 自訂二進位：`"TWCC"`、版本 1、格數、每格 `kind(1)+id(u32)` |
| 容量 | 3 個分頁 × 8 格＝24 格（V5 (a)：實際格號＝k＋8·頁）；擴充巨集 12 格是否也存在同一份，**未查證** | 只有 8 格（第 1 頁）。**第 2、3 頁與擴充巨集只存在記憶體（每個角色各一份），重新登入或重開客戶端就消失** |
| 其他設定 | 同一個 message 可能還存其他客戶端設定（BAR_485 模式的設定旗標、鎖定狀態等，V5「待驗證」） | 沒有 |
| 綁定內容 | 未查證（可能是物品／魔法以外的類型，例如動作、表情） | 只有物品 objectId 與魔法 id |
| 伺服器 | C244／S250／41 不解讀 blob | 相同 |

要把 24 格＋擴充 12 格存起來，需要另開任務：擴充 TWCC（例如 count 24／36 或 v2）並改 `CharacterConfig.cs`；若要相容原版，先查 `Lineage.ClientConfig` 的欄位。

## 怎麼驗證

1. `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock`（275 個來源檔／284 個鎖定檔），`--build --configfile D:\lineage開發\TW1608262200研究\engine\NuGet.Config` → **`PASS dev`**，productDll `72007ab8…e4a8`。（worktree 沒有 `engine/vendor`，所以唯讀引用主專案的離線套件來源。）
2. `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-466/macro-checks.json native-ui-macro` → **PASS 402**（本分組約 70 項，其餘是每次都跑的基本組）。內容：
   - 用**真的 registry** 從合成 `uistatus86-c.xml` 建出兩個面板；底框 (643,476) 157×124、鎖定鈕 (634,513)、Bounds＝聯集、3 個分頁鈕 (658/680/703, 481)、`MACRO_ICON` (657,497)、`SELECT_MARK` (655,495)。
   - 8 格圖示座標與 V5 表逐格相同；點擊換算 `(mx−655)/34＋4·((my−495)/34)`，格外不命中。擴充 12 格從 (495,495) 起、格距 34。
   - 合成道具（可用／不可用／已不在背包）與合成魔法（可用／未支援）：F5→道具、F6→魔法（帶出畫面上的 stamp）；不可用、不在背包、未支援、空格都不送；自動重複與放開只吞鍵不觸發；Ctrl＋F5、Alt＋F3、F4 不接；F2／F3／F1 切頁後 F5／F12 用到第 2／3 頁的格子；沒有遊戲鍵盤焦點、忙碌、別的角色的背包都不送；世界外不顯示也不接鍵。
   - 滑鼠：點分頁換頁、單擊選格不觸發、雙擊觸發、鎖定後無法改綁定但仍可使用、解鎖可改；擴充格單擊移動選取框、雙擊觸發。
   - 繪製（逐像素）：底框 1:1、目前分頁用 a+1、道具圖示、魔法與無圖示道具不畫任何東西、數量 5 以調色盤[13] 黃字畫在格內、選取框色鍵中空、鎖定開／關兩張圖、選取框移到擴充框、世界外不畫。
   - 每個角色分開；回到原角色 3 頁＋擴充都還在；第 1 頁經 TWCC 8 格往返相同；count 4 舊 blob → F5/F6…、F9–F12 空、第 2、3 頁不填；格式不明不移轉；移轉後的 F1 綁定可由 F5 觸發。
   - 缺 surf：缺鎖定圖時沒有鎖定命中區、缺 3226 時擴充框隱藏，兩者都不畫替代圖。
   - 版面錯誤（分頁 action 超出 10–12、缺 `MACRO_LOCK`、缺 `MACRO_FRAME`）一律丟 `UiLayoutException`。
3. 本機選用檢查：`TW_CLIENT_ROOT=D:\lineage開發\0-客戶端` 重跑 → **PASS 404**（pin 全部相符，真實 3176／3226 尺寸與文件一致）。渲染圖只在 `scratch/issue-466/real/`（`macro-page1/2/3.png`、`macro-locked-select6.png`、`macro-coordinates.json`）。目視：選取框正好落在 3176 底圖印好的格線上；3226 的 4×3 格與 (495,495)／格距 34 吻合；選中分頁 a+1 是金色高亮。
4. 回歸：native-ui-canvas 429、native-ui-resources 539、character-config 455、hotbar 569、item-status 409，**全部 PASS**。
5. `engine/tools/check-no-game-assets.py` → pass、errors 0；`git diff` 沒有新增任何 `.img`／`.xml`／`.png` 或二進位遊戲資料。
6. 不帶分組的預設全組：在 `Movement123` 因讀不到 `engine/assets/terrain0.bin` 而中止（這個 worktree 沒有 `engine/assets` junction），與本任務無關；它之前的各組（含 native-ui、minimap、character-config、hotbar）都已通過。

## 已驗證／推測／待驗證

**已驗證**（V5 或 XML，加上本輪真實渲染核對）：底框、分頁鈕、選取框、鎖定鈕的 XML 座標；8 格與擴充 12 格的座標與格距；點擊換算公式；F1–F3＝分頁、F5–F12＝目前分頁第 0–7 格、雙擊＝使用；數量文字位置與顏色（黃字、黑影、上限 99999）；擴充巨集位於 BAR_485。

**推測**（集中在常數，查證後只改一處）：
- 鎖定鈕圖：真實 3191 是**上鎖**的鎖頭、3192 是**打開**的鎖頭，所以「鎖定＝a、未鎖＝a+1」是依圖面內容判斷（`MacroPanel.LockOpenOffset`）。預設未鎖。BIN 的 `byte_C31EAF` 預設值與是否存檔沒有追。
- 鎖定的作用：本引擎是「鎖定時不能改綁定，但仍可使用」。原版鎖住的到底是什麼（拖曳、刪除？）沒有追。
- 選中分頁用 a+1（`TabSelectedOffset`）：V4 沒有追 `TAB_BUTTON` 的 3 張圖語義；真實圖的 a+1 是金色高亮，看起來合理。
- 按 F5–F12 也會移動選取框（`sub_782480` 第二個參數未追）。
- 擴充巨集：選取框沿用主框的 2331、相對圖示 (−2,−2)；單擊選格、雙擊使用。追到的程式碼中沒有任何按鍵綁到擴充格。
- 數量只在 >1 時顯示（原版顯示門檻未追）。

**待驗證**：
- 魔法格的圖示來源（原版用哪一組 surf／icon）未查證，所以魔法格**目前不畫圖示**（不畫替代圖）；道具圖示沿用舊 HUD 的 `assets/inventory/<IconId>.png`，原版是否用同一份圖未核對。
- 從背包／魔法窗拖曳到格子（綁定入口）：原生背包與魔法窗尚未實作。目前只有 `MacroBook.Assign` API；第 1 頁可以先在舊 HUD（`nativeUi` 關閉）指派並存到伺服器，開啟 `nativeUi` 後會還原到原生第 1 頁。**原生模式下第 1 頁不會自動填預設**（舊 HUD 的「前 4 個道具＋前 4 個技能」是引擎的便利功能，不是原版行為）。
- 存檔失敗或成功的提示（`NativeMacroNotice`）目前沒有地方顯示，等紅字或聊天模組接上。
- 真機：原生模式下實際按 F5–F12、F1–F3，以及畫面比對，都沒有做（需 ui-native-smoke）。

## 產出路徑
- 程式：`engine/source/NativeUi/MacroPanel.cs`、`ExMacroPanel.cs`、`State/MacroState.cs`；`UiPanelRegistry.cs`（1 行）、`UiClientBridge.cs`（2 行）
- 測試：`engine/_integration/dev/native-ui-tests/MacroChecks.cs`；分組 `native-ui-macro`
- 本機證據（不進 git）：`scratch/issue-466/macro-checks.json`、`scratch/issue-466/real/*`

## 給下一棒的提示
1. **ui-minimap／ui-party**：在 `UiPanelRegistry` 中，註冊行要放在 `ui-macro-bar #466` 那一行**之前**（理由見「疊放順序」）。BAR_485 的切換（Alt＋F3）目前沒有人處理；`UiSlotGroup.Cycle()` 已經有，建議由 ui-minimap 接 Alt＋F3。
2. **ui-menu-buttons**：同一個 `RIGHT_WINDOW` 的選單按鈕列 (y=91) 在本面板的 Bounds 內。本面板在按鈕列不處理點擊，但同層命中測試不會往下傳，所以選單按鈕面板要**註冊在本行之後**（疊在上面），並使用不同的 Id（例如 `RIGHT_WINDOW/BUTTON_2STAGE_LIST`）。
3. **存檔擴充**（建議另開 code 任務）：TWCC 擴到 24＋12 格，或查證後改用原版 `Lineage.ClientConfig`。`MacroBook` 已經把第 2、3 頁與擴充格分開存放，只要在 `MacroState.cs` 的產品轉接處加讀寫即可。
4. **原生背包／魔法窗**：拖曳到格子時呼叫 `MacroBook.Assign(page, slot, binding)`／`AssignExtended`；鎖定中會回傳 false。
5. **查證建議**：`TAB_BUTTON` 3 張圖語義、`MACRO_LOCK` 的 `byte_C31EAF` 預設值與作用、魔法格圖示來源（`sub_ED33F0` 畫圖示的分支）。
6. **ui-native-smoke（dot）**：先在舊 HUD 指派第 1 頁格子讓它存到伺服器，再開 `nativeUi`；檢查 F5–F12、F1–F3、分頁鈕、鎖定鈕、雙擊、擴充框，並與 `scratch/issue-466/real/*.png` 及原版截圖對照。
