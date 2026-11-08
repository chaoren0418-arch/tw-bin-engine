# Issue #470 — ui-minimap：底列小地圖與 Field_Map 小窗（Ctrl＋M 開關，讓出 F5）

階段：code，第 1 次嘗試；runner：claude-local；分支 `task/470`。
依據：[`docs/design/native-ui.md`](../design/native-ui.md) 第 5.3 節 ui-minimap；[`ui-layout.md`](../reference/ui-layout.md) 第 5 節、V5 (b)(d)(e)（#460）；C2 框架（#463）。

## 做了什麼

### 新檔（命名空間 `P06.NativeUi`）

| 檔案 | 內容 |
|---|---|
| `engine/source/NativeUi/State/MapState.cs` | `FieldMapEntry`：`ratio`／`baseLoc`／`basePixel` 換算，照 TW16 `sub_A67070`（VA `0xA67070`／FO `0x666470`）：X＝(dx＋dy)·f、Y＝f·0.5·(dy−dx)，各自截斷成整數再加 `basePixel`；f＝24／ratio（見「推測」）。`MapSystemData`：解析 `MapSystem-c.xml` 的 `Field_Map`、`Dungern_Map`、`Restricted_Map`、`Common_UI`。`MapViewLayout`：160×120 取景（`sub_A695E0`，FO `0x6689E0`）。`MapPngSource`：png 地圖在執行期從 `Sprite{h}.pak` → `Sprite.pak` 讀取，h＝檔名 byte 和 %16，與 surf 規則相同。`MapState`：兩個面板共用的狀態，一次只快取一張 png。`IUiMapSource`／`UiMapSnapshot`：遊戲狀態轉接；`ClientForm` 以 partial 實作，讀 `WorldSession` 的地圖、位置、朝向與隊伍名單，不解析封包 |
| `engine/source/NativeUi/MiniMapPanel.cs` | `MINI_MAP_WINDOW`：座標 (485,475)，框 3275（158×125），底 3274 在 (+12,+20)（135×101）。屬於 `BAR_485` 槽位，`Visible`＝`group.IsShown`。繪製順序照 `sub_A695E0`（a4＝0）：底 → png → 框 → 隊友點 → 箭頭；取景原點是 `MINI_MAP_BASE`＋(0,5)（V5 (d)）。點擊會被面板吃掉，避免角色走動；不處理任何按鍵 |
| `engine/source/NativeUi/FieldMapPanel.cs` | `Field_Map` 視窗：座標 (5,250)，161×121。框 2816、標題 2815 (96,1)、關閉 2801 (146,6)、縮放 2803 (129,6)。png 取景以玩家為中心，有 8 方向箭頭 2818–2825 與隊友點 2826。地圖不在清單內時改用 `Dungern_Map` 外觀（標題 2888、沒有縮放鈕），也不畫地圖內容。**Ctrl＋M** 開關（V5 (b)）。關閉鈕會關窗。可拖曳。 |
| `engine/_integration/dev/native-ui-tests/MiniMapChecks.cs` | 合成離線測試，75 項，分組 `native-ui-minimap` |

### 既有檔案

- `UiPanelRegistry.cs`：加一行 `new(MiniMapPanel.PanelId,"uistatus86",root=>MiniMapPanel.Create(root))`。`Create` 會同時建立 `Field_Map` 並加進 `UiRoot`，所以這個模組只占註冊表一行。`MiniMapPanel.Load` 會另外載入 `MapSystem-c.xml`，交給 `FieldMapPanel.Load`。
- 測試接線（不在任務列出的檔案內，理由同 C2：沒接線就跑不到）：
  - `demo-tests/DemoChecks.csproj` 加 1 行 Compile，Link 名稱用 `NativeMiniMapChecks.cs`，避開既有的 `MinimapChecks.cs`
  - `demo-tests/Program.cs` 加分組 `native-ui-minimap`，也加進預設全組
- `rebuild-lock.json`：用 `--update-lock` 重產，281 個來源檔、290 個鎖定檔。

### 行為摘要

- **F5**：兩個面板都不處理 F5。原生模式下，F5 由 `UiInput` 退回遊戲，作為巨集鍵。舊 HUD 本來就已經不用 F5 開小地圖（`minimap` 分組既有檢查）。
- **Ctrl＋M**：只在世界中有效；重複觸發（auto-repeat）不會再切換一次；放開鍵時也會被吃掉，避免遊戲收到。
  - 離開世界時自動關閉 `Field_Map`。
  - `Field_Map` 關閉時 `Bounds` 為空，但 `Visible` 仍是 true，所以槽位顯示擴充巨集時，Ctrl＋M 照樣有效。
  - Map 按鈕（action 13）屬於 ui-macro-bar，可以呼叫 `((FieldMapPanel)root.Find("Field_Map")).Map.ToggleField()`。
- **地圖種類**（`MapViewKind`）：
  - 清單內 → png＋箭頭＋隊友點
  - 不在清單 → 只畫框（原版交給地監檢視器，本任務未做）
  - `Restricted_Map` 範圍 → 只畫框（原版模式 4）
  - 清單內但讀不到 png → 只畫框，並記一行 log；面板不會被停用
- **資料異常列**：真實 `MapSystem-c.xml` 有兩列無法使用，本次直接略過並寫 log，不自行修補：
  - mapNum 300：`baseLoc="32896 32523"`，用空白分隔
  - mapNum 1003：屬性名稱拼成 `bbaseLoc`，所以沒有 `baseLoc`
  - 重複的 mapNum 全部丟棄（真實資料沒有重複）

## 怎麼驗證

1. **合成離線測試**：`dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release -- . scratch/issue-470/minimap-checks.json native-ui-minimap`，結果 **PASS 402**。其中 327 項是每次都會跑的基本組，75 項是本分組。fixture 全部由程式產生在 `scratch/issue-470/synthetic/`，包括 Tile、17 個 Sprite、加密 XML、.img 與 png。
   - **座標換算**（驗收）：
     - ratio 24／12／144 下，`baseLoc` 對應到 `basePixel`；x、y 各加 1 時的像素位移正確；負向位移往 0 截斷
     - 隨機取 500 格，與引擎自己的 `Coordinates.Screen`（÷24）完全一致
     - 真實座標（只用數字）：亞丁、古魯丁、歐瑞、海音、奇岩在 ratio 24（3260×1660）與 ratio 144（550×316）兩種比例下都落在 png 內；亞丁城的精確像素也有核對
   - **箭頭選圖**（驗收）：朝向 0–7 對應 surf 2818–2825；8、−1、255 不畫箭頭。合成畫面中，朝向 0、3、7 各自畫出對應顏色的 9×9 箭頭，位置在 (572,556)。
   - **槽位互斥**（驗收）：
     - 加入一個合成的 `EX_MACRO_WINDOW` 後，順序仍是小地圖在前
     - `Cycle` 之後小地圖不可見、命中不到，槽位只畫擴充巨集
     - 切回後小地圖重新可見、可命中；`Reset` 回到小地圖
     - 切換期間 Ctrl＋M 仍然有效
   - **取景**：
     - 玩家固定在原點＋(80,60)
     - 靠近圖片邊緣時，來源範圍會被夾住，但玩家仍在中心
     - 標記範圍：Field_Map 內縮 3；底列內縮 (15,18,15,4)
     - png 裁在取景內縮 3 的範圍
   - **合成畫面逐像素**：
     - 不在世界：只有底與框
     - 在世界：png 位置正確、框蓋在 png 上、隊友點置中、標記範圍外的隊友不畫
     - 移動 1 格時，畫面依換算後的像素移動
     - 不在清單、限制地圖、缺 png：都只畫框，面板不會被停用
   - **輸入**：
     - F5 不被原生 UI 拿走；單按 M 無作用；Ctrl＋M 開在 (5,250)，大小 161×121
     - 關閉鈕會關窗；縮放鈕被吃掉但不做事
     - 拖曳移動 (+100,+20)；點底列小地圖不會傳到世界
     - 關閉的 `Field_Map` 命中不到
     - 不在清單的地圖顯示標題 2888、沒有縮放鈕
   - **MapSystem 解析**：
     - 可用的列與被略過的列（空白分隔、缺 `baseLoc`、ratio 0、重複）
     - `Restricted_Map` 含 `16384~65535`
     - 缺 `Common_UI` 時拒絕整份版面，`UiLayoutException`
2. **開關關閉時既有測試不變**：native-ui-canvas 429、native-ui-resources 539、minimap 398、hotbar 569、item-status 409、pets 基本組 327，**全部 PASS**。其他組見第 5 點。
3. `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock`，再跑 `--build --configfile D:\lineage開發\TW1608262200研究\engine\NuGet.Config`，結果 **`PASS dev`**，productDll `e899d51b…5ebf`。worktree 沒有 `vendor`，所以和 #463 一樣，唯讀引用主專案的離線套件來源。
4. **本機選用檢查**（真實客戶端，唯讀）：程式在 `scratch/issue-470/real-probe/`，輸出在 `scratch/issue-470/real/`，都不進 git。
   - 用真實的 Tile 與 17 個 Sprite 跑 `UiRoot.Build(UiPanelRegistry.Panels)`，**通過，耗時 46 秒**（雜湊所有 pak）。建出 `Field_Map` 與 `MINI_MAP_WINDOW`，bounds 為 (485,475,158,125)，底為 (497,495,135,101)。
   - 真實 `MapSystem-c.xml`：59 筆可用 Field_Map 列、4 個限制範圍，USER_POINT 2818、PARTY_POINT 2826。只略過 300、1003 兩列。
   - 說話之島村（map 0，`10100.png`，1024×512）、奇岩（map 4，`10101.png`，3260×1660）：玩家像素都落在圖內；第一次合成（含解 png）分別 28 ms、65 ms；沒有面板被停用。
   - 目視 `giran-zoom.png`：
     - Field_Map 顯示奇岩村 png、框、縮放與關閉鈕；箭頭在中心，旁邊有隊友點
     - 底列小地圖在 3275 框內顯示同一張圖
   - 不在清單的地圖：只畫框。
5. **較大的回歸組與資產檢查**：
   - combat-admission **PASS 1750**。
   - `engine/tools/check-no-game-assets.py`：**pass，errors 0**。新增的檔案只有 `.cs` 與報告。
   - map-continuity：本機有真實客戶端時，在 `DisplayNoticeChecks` 的「notice S245 unknown message stays labeled」失敗。這是**環境相關、既有的問題，與本任務無關**：#546 改用真實 `string-c.tbl` 後，999 號訊息在真實表裡查得到。單跑 `display-notices` 也一樣：真實客戶端在時失敗；`TW_CLIENT_ROOT` 指向不存在的路徑時 PASS 388。
   - 以 `TW_CLIENT_ROOT=D:\nonexistent-client` 重跑 map-continuity，結果 **PASS 33259**。
   - 建議另開一張小任務：讓這項檢查不依賴本機客戶端。

6. **同步 origin/main**（併入 #465 ui-hpmp 等 13 個提交）：
   - 衝突處理：
     - `UiPanelRegistry.cs`、`DemoChecks.csproj`、`demo-tests/Program.cs`：兩邊都保留
     - `rebuild-lock.json`：改用 main 版本，再以 `--update-lock` 重產（283 個來源檔／292 個鎖定檔）
   - `--build` 結果 **`PASS dev`**，productDll `e99e60fa…d6da`。
   - 本分組的 Build 改成只取小地圖那一行註冊，因為 HpMp 需要自己的 fixture。
   - 重跑：native-ui-minimap 402、native-ui-hpmp 397、native-ui-canvas 429、native-ui-resources 539、minimap 398、item-status 409，**全部 PASS**。
   - 真實資源探針：四個面板（HpMpWindow、MiniHpMpBar、Field_Map、MINI_MAP_WINDOW）一起 Build 通過（46.5 秒），結果與合併前相同。
   - `check-no-game-assets.py`：pass，errors 0。

7. **第二次同步 origin/main**（2026-11-09，PR #597 衝突；併入 #464 #466 #467 #468 #517 #526–#530 #558 #560 #601 #602 等）：
   - 衝突處理：
     - `UiPanelRegistry.cs`：兩邊都保留；小地圖那一行放在 HpMp 之後、`ExMacroPanel`／`MacroPanel`（RIGHT_WINDOW）之前，讓 MACRO_LOCK 照 #466 的設計畫在 BAR_485 上面。`BAR_485` 成員順序由 `UiSlotGroup.OrderOf` 依 V5 決定，跟註冊順序無關；小地圖現在和 main 的真 `ExMacroPanel` 共用同一個 `root.SlotGroup(UiSlotGroup.Bar485)`。
     - `DemoChecks.csproj`、`demo-tests/Program.cs`：兩邊都保留（分組 `native-ui-minimap` 也加進預設全組）
     - `rebuild-lock.json`：改用 main 版本，再以 `--update-lock` 重產（305 個來源檔／314 個鎖定檔）；`docs/REPORT.md` 用 main 版本
   - 合併後的必要小修：
     - `MiniMapChecks.cs` 的 `Rgb555` 與 main 新增的 `MenuButtonsChecks.cs` 同名（同一個 partial class `Checks`，CS0111），本檔改名 `MiniRgb555`。
     - `MacroChecks.cs` 的 `MacroRegistry` 篩選多排除 `MiniMapPanel.PanelId`：它的合成 `uistatus86` 版面沒有 `MINI_MAP_WINDOW` 節點，和既有排除 LEFT／CENTER_WINDOW 的理由相同。
   - `build-product.ps1` 結果 **`PASS dev`**，productDll `0c59f0ea…c66b`；唯讀核驗 `dev-inputs-verified`。
   - 重跑：native-ui-minimap 402、native-ui-macro 403、native-ui-menu-buttons 355、native-ui-hpmp 397、native-ui-canvas 429、native-ui-left-status 370、native-ui-chat-window 446、minimap 398，以及 CI 列出的 combat-admission 1750、item-status 409、native-ui-resources 539、afflictions 580、resurrection 553、polymorph-cadence 380、buddies 496、item-transfer 400、doors 648、pickup 369、door-approach 361、hypertext 357，**全部 PASS**。
   - map-continuity：同第 5 點，本機有真實客戶端時在 DisplayNotice S245 那一項失敗（環境相關，與本任務無關）；交給 PR CI（ubuntu、無客戶端）判定。
   - 不帶分組的預設全組在本 worktree 跑不起來：`engine/assets` junction 不在 worktree（`terrain0.bin` 找不到），屬環境問題。

## 已驗證／推測／待驗證

**已驗證（本輪，離線＋真實資源唯讀）**
- 換算公式的結構（(dx＋dy)·f、0.5·f·(dy−dx)、截斷）與原版偽碼一致；用合成資料逐點驗證。
- 箭頭選圖、槽位互斥、F5 不被占用、Ctrl＋M 開關，都有合成測試。
- png 地圖在 `Sprite{h}.pak`，是一般 PNG（開頭為 `89 50 4E 47`）。最大的 `10101.png` 為 5.6 MB，低於 8 MiB 上限。真實資源可以建出並畫出這兩個面板。

**推測**
- **f＝24／ratio**：BIN 裡 `this[6]` 這個浮點數的來源沒有追到。依據有三點：
  - 和引擎的等角投影一致
  - 真實資料在 ratio 24、144 兩種比例下，主要村莊都落在 png 內
  - 奇岩實際畫面上，箭頭位置在村莊中央
- TW16 對玩家自己的 x 做 `((x−0x8000)>>1)+0x8000`，隊友則不做。推測是 TW16 內部的 x 編碼與封包座標不同，本實作直接使用伺服器座標。
- `Field_Map` 的畫法：先畫框，再把 png 裁在內縮 3 的範圍，最後畫標題與按鈕。底列小地圖的 png 也裁在取景內縮 3 的範圍（同一支繪製函式）。
- 隊友點一律使用 `PARTY_POINT_IMG` 2826。原版 `sub_A6D700` 會依條件選圖，隊長或自己可能用不同的圖。
- `Field_Map` 可以拖曳（設計 §3.1 一般視窗），拖曳範圍是視窗內按鈕以外的區域。

**待驗證／未做**
- **沒有真機驗收**：沒有啟動客戶端，也沒有在 P24Lab 開 `--native-ui`。取景是否和原版逐像素相同，要等 ui-native-smoke（dot）對照原版截圖。
- 小地圖取景：原點 base＋(0,5)、160×120、玩家位於底區 (80,65)，這是照 V5 偽碼推出來的。ui-layout.md §5 已列為待驗證。
- 未做：
  - `Field_Map` 透明度（`transparency="20"`），目前不透明
  - 按鈕按下的狀態圖
  - 隊友名字
  - NPC 標記（`sub_A6D0F0`／`sub_A6CA60`，受選項控制）
  - 視野外隊友的邊緣指示（`sub_A6C430`）
  - 地監檢視器（`Dungern_Map` 的內容）
  - 縮放鈕開啟的大地圖（`World_Map`／`Village_Map`）
  - 原版 Ctrl＋M 在地圖類型 4 時顯示的系統訊息 #1734
- `BAR_485` 的 Alt＋F3 切換沒有放在本模組：小地圖不在槽位上時收不到按鍵。建議由 C2 後續或 ui-macro-bar 放在一個一直可見的面板上。
- `IUiGameState` 沒有地圖欄位，而本任務不改 C2 契約。所以產品端透過 `Application.OpenForms` 找到實作 `IUiMapSource` 的 `ClientForm`（只在 UI 執行緒）；測試則讓 state 物件直接實作 `IUiMapSource`。

## 產出路徑

- `engine/source/NativeUi/MiniMapPanel.cs`、`FieldMapPanel.cs`、`State/MapState.cs`；`UiPanelRegistry.cs`（加一行）
- `engine/_integration/dev/native-ui-tests/MiniMapChecks.cs`；`demo-tests/DemoChecks.csproj`、`demo-tests/Program.cs`（分組 `native-ui-minimap`）
- `engine/_integration/dev/rebuild-lock.json`（重產）
- 本機證據（不進 git）：
  - `scratch/issue-470/minimap-checks.json`、`group-*.json`
  - `scratch/issue-470/real/*.png`（含 `*-zoom.png`）
  - `scratch/issue-470/real-probe/`

## 給下一棒的提示

- **ui-menu-buttons（#467 已合併）**：`MenuButtonsPanel` 的 Map 鈕（action 13）目前沒有接到任何功能（只有 Quit 有對應）。下一張任務可以讓它呼叫下面的 `ToggleField()`。
- **ui-macro-bar**：
  - Map 按鈕（action 13）呼叫 `((FieldMapPanel)root.Find(FieldMapPanel.PanelId)!).Map.ToggleField()`，另播音效 237（V5 (b)）
  - Alt＋F3 → `root.SlotGroup(UiSlotGroup.Bar485).Cycle()`，建議放在一直可見的巨集框面板的 `Key`
- **C2 後續**：
  - 把 `UiMapSnapshot` 併進 `IUiGameState`，由 `UiClientBridge` 提供
  - 之後改掉 `MapState.ClientSource` 的 `Application.OpenForms` 查找
- **smoke（dot）**：
  - 進奇岩或說話之島，看底列小地圖與 Ctrl＋M 的 `Field_Map`：箭頭方向是否和角色朝向一致、隊友點位置是否正確
  - 截取邏輯畫布（`Ctrl+Shift+F12` 的 `-logical.png`），和原版同位置截圖逐像素比對取景
  - 確認 F5 用的是巨集，不是小地圖
- **地圖資料**：mapNum 300、1003 兩列原版怎麼解析還不知道。如果 smoke 發現原版在這兩張圖有顯示地圖，要追 MapSystem 解析函式的 `baseLoc` 讀法，不要自己猜。
