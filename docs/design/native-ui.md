# 原版 800×600 UI 框架設計（T174，Issue #448）

狀態：設計契約，供後續 UI 模組任務平行開發。本文件**不改產品程式、不含任何遊戲資源**。
依據：[`docs/reference/ui-layout.md`](../reference/ui-layout.md)（T161／#429）第 2–7 節，以及本輪在 `scratch/` 的唯讀抽查（見第 8 節）。

每條敘述標註依據等級：
- **已驗證**：T161 已從 BIN／XML 取得，或本輪從本機客戶端唯讀讀出。
- **推測**：有間接證據，但沒有直接查證。
- **待驗證**：必須先開查證任務（第 6 節），查完才能開對應的 code 任務。

---

## 0. 範圍

**涵蓋**：遊戲中（InWorld）畫面的 800×600 框架、資源讀取層、縮放與輸入對映、診斷／網頁面板的去處，以及底列與常用視窗的模組拆分：左狀態框、HP／MP、巨集框與快捷列、選單按鈕、聊天窗、小地圖、隊伍、書籤、狀態圖示、紅字訊息。

**不涵蓋**，各自另開設計：
- 登入／選角畫面。目前在網頁面板，原版是另一套畫面。
- 背包、角色狀態窗（`ReNStatusWinUI-c.xml`）、魔法窗、選項、信件、公告板、NPC 對話、交易、個人商店。框架完成後，這些視窗可以沿用同一套 `UiPanel` 契約另開任務。
- 世界畫面的繪製（`Renderer`）。框架只決定世界畫面畫在哪個矩形。

---

## 1. 架構總覽

```
原客戶端 TW_CLIENT_ROOT（唯讀）
  Tile.idx/pak ──► UiXmlSource ──► UiLayout（XDocument → 具名節點、座標、surf 範圍）
  Sprite*.idx/pak ─┐
  SurfList.xml ────┴► SurfCatalog ──► UiSurface（解碼後 32bpp PArgb，只在記憶體）
                                  │
遊戲狀態（既有模型，唯讀快照）──► IUiGameState
                                  ▼
               UiRoot（800×600 邏輯畫布：世界層 + 視窗層 + 浮動層）
                 ├─ UiPanel：LeftStatus / HpMp / Macro / Menu / Chat / MiniMap / Party / Bookmark / BuffIcons / RedNotice …
                 └─ UiInput（滑鼠／鍵盤 → 邏輯座標 → 命中測試 → IUiCommands）
                                  ▼
               UiPresenter（800×600 → 視窗客戶區，整數倍放大並置中，最近鄰取樣）
```

新程式一律放在 **`engine/source/NativeUi/`**。`OfflineClient.csproj` 使用預設 glob，子目錄會自動編入，不需要改 csproj。每個模組只擁有自己的檔案（見第 5 節），所以可以平行開發；整合點只有 C2 任務留下的註冊表。

### 1.1 功能開關

- **T317（#751，使用者 2026-11-10 #743 決定 1「預設就使用原生的 UI」）起，正常產品入口預設開啟原生 UI。** 舊 WinForms HUD（`PlayerHud*`、`MinimapUi`、`PartyUi`…）仍保留，作為退路與比對用。
- 決定順序（後者蓋過前者），實作在 `NativeUi/UiRoot.cs` 的 `UiOptions.Resolve`：
  1. 預設：正常產品入口（launcher 傳的 `--normal-product`，或不帶參數）為**開啟**；診斷／測試入口（`--demo`、`--evidence`、`--login-demo` 等其他參數）為**關閉**，維持這些工具原本對照的舊 HUD（`ProductEntry.NativeUiDefault`）。
  2. 設定檔 `<stage>/runtime/native-ui.json`：`{"nativeUi":true}` 開、`{"nativeUi":false}` 關；`nativeUiFill` 控制等比填滿（§3.2）。檔案格式錯誤或值不是 JSON 布林時忽略，沿用預設。
  3. `--native-ui`：開啟。保留為相容參數，給了不報錯，效果與預設相同。
  4. `--classic-ui`：關閉，切回舊 WinForms HUD，方便比對與排錯。兩個參數同時給時 `--classic-ui` 優先。
- `--native-ui`／`--classic-ui` 在 `Program.Main` 一開始就從參數中移除（`UiOptions.Strip`），所以 `--normal-product --classic-ui` 仍是正常產品入口。launcher 固定只傳 `--normal-product`；要從 launcher 啟動舊 HUD，請用設定檔 `{"nativeUi":false}`。
- 開關打開時，舊 HUD 控制項不顯示，也不接收輸入。兩套 UI 不能同時接收同一個按鍵。原生 UI 資源核對失敗時，自動退回舊 HUD 並顯示一行紅字（§3）。
- 移除舊 HUD 是另一張任務，不在本契約範圍內。

---

## 2. 執行期資源讀取（契約）

### 2.1 共通規則

| 規則 | 內容 | 依據 |
|---|---|---|
| 來源 | `TW_CLIENT_ROOT`，未設定時為 `D:\lineage開發\0-客戶端`（與 `GroundSpriteReader.ClientRoot` 相同）。只開唯讀 handle（`FileShare.Read`） | 已驗證（既有程式） |
| 不複製 | `.xml`、`.img`、解碼後的像素**不得**寫進 repo、`engine/assets`、建置產物或 zip；不得做成 embedded resource | CLAUDE.md、#109／#102 |
| 快取 | 第一版**只放記憶體**，大小上限 32 MiB，超過時以 LRU 淘汰。量到啟動或開窗過慢時，才另開任務加磁碟快取，位置在 `%LOCALAPPDATA%\tw-bin-engine\ui-cache\<Tile.pak SHA 前 16 碼>\`，鍵值包含來源 pak 的 SHA；這個目錄不進 repo，也不進 zip | 設計決定 |
| pin | `Tile.idx`、`Tile.pak` 與用到的 `Sprite*.idx/pak` 都以 SHA-256 鎖定，做法同 `GroundSpriteReader.archivePins`。不符時**整個原生 UI 停用**：顯示一行錯誤「原客戶端 UI 資源與鎖定版本不符」，並退回舊 HUD。不得改用替代圖或純色假畫面（規則 5） | 設計決定 |
| 邊界 | idx 筆數 × 28＋4 必須等於檔案大小；offset＋size 不可超過 pak 大小；單一 entry 不超過 8 MiB；名稱只能是可列印 ASCII。違反任何一項就拒絕 | 同既有 reader |
| 共用 reader | C1 把 `GroundSpriteReader.Archive` 的 idx/pak 解析抽成 `NativeUi/ClientArchive.cs` 的共用類別。**既有 reader 不改**，避免影響地面物品的 pin 與測試，等之後另開任務再合併 | 設計決定 |

### 2.2 UI XML（`Tile.pak`）

| 項目 | 契約 | 依據 |
|---|---|---|
| 解密 | 第一個 byte 為 `X`，前 4 byte 為 `X?xm` 時要解密：`body=blob[4..]`，AES-128-CBC 解完整的 16 byte 區塊；尾端不足 16 byte 的部分直接與目前 IV 做 XOR；最後輸出 `"<"+blob[1..4]+plain`。金鑰與 IV 照 `docs/reference/rea-scripts/ui_xml_decrypt.py`。C# 用 `Aes.DecryptEcb` 逐塊做 CBC，**不能**直接用 `DecryptCbc`，因為尾段不是 padding | 已驗證（T161 第 1 節） |
| 檔名 | 先找 `<name>-c.xml`（CP950 → `c`，`sub_A14A60`），找不到就用 `<name>.xml`，並在日誌記一行 `ui-xml-fallback`。BIN 缺 `-c` 檔時會不會改讀無後綴檔，**待驗證（V4）**；本輪 65 個檔中，`BookmarkUI.xml`、`MapSystem-c.xml` 等確實是有的有 `-c`、有的沒有 | 部分已驗證 |
| 編碼 | 有 `encoding="…"` 宣告時照宣告（本輪只有 3 個檔宣告 UTF-8）；沒有宣告時以 **CP950** 解碼（`CodePagesEncodingProvider`）。BIN 實際怎麼解碼，**待驗證（V4）** | 推測 |
| 語法 | 屬性的 `=` 兩側可能有空白（`x = "0"`），XDocument 可以直接解析。元素名稱照原文保留，包括拼錯的名稱（如 `FOOD_GUAGE_BAR`），**不要更正** | 已驗證（本輪抽查） |
| 座標 | 視窗元素的 `x`／`y` 是 800×600 的螢幕座標。子元素的 `xRPos`／`yRPos` 相對於所屬視窗；`yPos` 是**底部錨定的絕對 y**（例：`CHAT_WINDOW yPos="600"`） | 已驗證 |
| surf 範圍 | `surfNumber="5726~5728"` 是連續的 surf 範圍。各張圖代表 off／over／down／disabled 的哪一種狀態，**待驗證（V4）**；驗證前先依序存成 `int[]`，模組只用第 0 張 | 已驗證格式；語義待驗證 |
| 介面 | `UiLayout.Load(string baseName)` → `UiNode`（`Name`、`Attr(string)`、`Int(string)`、`Surfs(string)`、`Children`、`Child(string path)`）。缺節點時丟出 `UiLayoutException`，並帶上檔名與路徑；**不**自行補預設座標 | 設計決定 |

### 2.3 UI 圖（`Sprite*.pak` 的 `.img`）

| 項目 | 契約 | 依據 |
|---|---|---|
| surf → 檔名 | 用 `Tile.pak` 的 `SurfList.xml`：`<surf id="N" file="F"/>`，檔名為 `F.img`。6,653 筆裡 343 筆 `id ≠ file`，所以**不可**假設 `id == file` | 已驗證 |
| 封存檔 | UI 的 `.img` 分散在 `Sprite.pak` 和 `Sprite00`–`Sprite15.pak`。本輪抽查：3168→Sprite13、3176→Sprite12、2273→Sprite09、3204→Sprite04、4313→Sprite06、2818→Sprite14。因此要把 17 個 idx 合成一個名稱索引 | 已驗證（本輪） |
| 重名 | 17 個 idx 共 7,543 筆 `.img`，不重複名稱 7,490 個。53 個名稱出現在兩個封存檔，其中 46 個內容不同，而且全部小於 `1500.img`（`Sprite.pak` 對 `SpriteNN.pak`）。BIN 用哪一份，**待驗證（V4）**。在此之前，同名且內容不同時**拒絕載入**該 surf；目前 UI 用到的 surf 全部在 2000–5800，不受影響 | 已驗證計數；優先序待驗證 |
| 格式 | `u16 寬, u16 高, u16 旗標, u16 透明色, 寬×高 個 RGB555`，小端序。必須滿足 `8+寬×高×2 == size`，否則拒絕 | 已驗證（T161） |
| 色彩 | RGB555，R 在高位（`0x7C00`）。每個 5-bit 通道以 `v*255/31` 換成 8-bit。像素等於透明色時，alpha=0 | 已驗證色序；透明規則待驗證 |
| 旗標 | `u16 旗標` 的意義（是否啟用透明色、半透明、加色）**待驗證（V4）**。驗證前一律把透明色當作色鍵，並把旗標值寫進 `UiSurface.Flags` 以便除錯 | 待驗證 |
| 介面 | `SurfCatalog.Get(int surf)` → `UiSurface?`（`Width`、`Height`、`Flags`、`Bitmap`，32bpp PArgb）。surf 不存在時回傳 null 並記一次日誌。模組遇到 null 時**不畫該元件**，不畫替代色塊 | 設計決定 |

---

## 3. 座標系統與縮放策略

### 3.1 邏輯座標

- 唯一的邏輯座標系是 **800×600**，原點在左上，單位是原版像素。XML 座標直接使用，不換算。
- 圖層由下而上：
  1. 世界層
  2. 底列視窗（`LEFT`／`CENTER`／`MINI_MAP`／`EX_MACRO`／`RIGHT`，以及隊伍底板）
  3. 一般視窗（書籤、`Field_Map`…，可拖曳，有 z-order）
  4. 浮動層（HP／MP 浮動窗、狀態圖示、紅字、tooltip、拖曳中的圖示）
- 底列佔 y=470–600。世界畫面是**整個 800×600 被底列蓋住**，還是只畫在 800×470，**待驗證（V5）**。C2 先讓世界層畫滿 800×600、由底列覆蓋，並把這個值做成 `UiRoot.WorldRect` 常數，查證後只改這一處。

### 3.2 呈現（UiPresenter）

- 每一幀先合成到一張 800×600 的離屏 `Bitmap`（32bpp PArgb），再一次 `DrawImage` 到視窗客戶區。
- **預設縮放：整數倍並置中。** `s = max(1, floor(min(W/800, H/600)))`，偏移 `ox=(W-800s)/2`、`oy=(H-600s)/2`。四周填黑，取樣用 `NearestNeighbor` 加 `PixelOffsetMode.Half`（與既有 `MinimapUi` 相同）。
- 視窗客戶區最小為 800×600，開關打開時設定 `MinimumSize`；預設大小為 800×600。小於 800×600 的情況不支援。
- 選用的「等比填滿」：`s = min(W/800, H/600)`，可以是非整數，仍用最近鄰。放在設定裡，預設關閉。
- 世界層也以 800×600 為準，`Camera.View = WorldRect.Size`，不跟著視窗大小改變可見範圍，這點和原版一致。目前 `PlayerSceneBounds` 會隨視窗大小變動，開關打開時改由 `UiRoot` 提供。
- 文字直接畫在 800×600 畫布上，像素跟著縮放，不在高解析度下另外重畫。原版字型與大小（推測為 12 px 細明體點陣）**待驗證（V4）**；在此之前先用 `新細明體 9pt`、`TextRenderingHint.SingleBitPerPixelGridFit`，集中在 `UiText` 一處。
- DPI：維持 `AutoScaleMode.None`；縮放只由 UiPresenter 決定，不受 Windows DPI 影響。

### 3.3 輸入對映

- 滑鼠：`lx = floor((mx-ox)/s)`，`ly = floor((my-oy)/s)`。座標落在 800×600 外（黑邊）時，不送給 UI，也不送給世界。
- 命中測試從浮動層往下找：第一個 `HitTest(lx,ly)` 為真的 panel 取得事件；都沒有命中時交給世界層，也就是現有的點地、點物件、點 NPC 流程（`PlayerPointer`）。
- 拖曳視窗時以邏輯座標移動，並限制視窗整個留在 800×600 內。
- 鍵盤：開關打開時，**F1–F12 保留給遊戲**（F5–F12 是巨集鍵，已驗證；F1–F4 的原版用途待驗證，見 V5）。原本占用 F 鍵的開發快捷鍵依第 4 節搬走。
- 聊天輸入焦點沿用 `PlayerChatKeyboardFlow`；Enter 開始或送出的流程不變，只把輸入框的位置換成 `CHAT_BAR`。

---

## 4. 診斷與網頁面板的處理

現況：
- `webHost`：`Dock=Right`，寬 365，WebView2。
- `normalPanels`：TabControl，寬 470，蓋在場景左側。
- `bar`：`Dock=Bottom` 狀態列。

這三者都佔用遊戲畫面，也都會改變 `PlayerSceneBounds`。

**決定：改成獨立的「開發工具」視窗（`DevToolsForm`），不佔遊戲畫面。**

| 項目 | 契約 |
|---|---|
| 視窗 | `DevToolsForm`：非模態、`Owner=ClientForm`、可縮放，位置與大小存在使用者設定。內容是現有的 `webHost`（WebView2）與 `normalPanels` 分頁，再加一頁「狀態列」放現有 `bar` 的文字 |
| 開關 | 開發工具：`Ctrl+Shift+F9`。回到遊戲焦點：`Ctrl+Shift+F10`。焦點移到網頁輸入：`Ctrl+Shift+F11`。證據截圖：`Ctrl+Shift+F12`。離線 demo 鍵（F2／F3／F6／F7／F8）只在非 `normalProduct` 的診斷模式有效，並改成 `Ctrl+Shift+對應鍵`，使 F1–F12 不再被開發鍵佔用 |
| 主畫面 | 開發工具打開或關閉都**不改變**主視窗的遊戲畫面大小；`PlayerSceneBounds` 不再扣 `webHost.Width` |
| 焦點 | 開發工具取得焦點時，呼叫既有的 `LeaveGameFocus`；回到主視窗時，呼叫 `ReturnGameFocus`。WebView2 的安全設定（禁止導覽、下載、權限、新視窗）原樣保留 |
| 過渡 | 登入、選角、背包、魔法、NPC、交易、個人商店、能力點，在原生視窗完成前**仍然在開發工具裡操作**。這是功能過渡，不是原版行為；驗收報告必須照實寫明 |
| 證據 | `CaptureEvidence` 改成擷取 800×600 的邏輯畫布（縮放前），另存一張視窗截圖。這樣不同縮放倍率的證據可以直接逐像素比較 |

這一項由 C3 任務實作，與原生 UI 開關無關：舊 HUD 模式也一起受益。

---

## 5. 模組契約與任務拆分

### 5.1 共同介面（由 C2 建立，模組只實作）

```csharp
namespace P06.NativeUi;
public interface IUiPanel {
    string Id { get; }                 // 例："LEFT_WINDOW"
    UiLayer Layer { get; }             // World/Bar/Window/Float
    Rectangle Bounds { get; }          // 邏輯座標
    bool Visible { get; }
    void Load(UiLayout layout, SurfCatalog surfs);   // 只讀 XML 與 surf；失敗丟 UiLayoutException
    void Update(IUiGameState state);                  // 每幀或狀態改變時呼叫；不可阻塞、不可送封包
    void Paint(UiCanvas canvas);                      // 只畫在 Bounds 內
    bool Mouse(UiMouseEvent e, IUiCommands commands); // true = 已處理
    bool Key(UiKeyEvent e, IUiCommands commands);
}
```

- `IUiGameState`：唯讀快照，包裝既有模型（`PlayerHudSnapshot`、`PlayerChat`、`Party`、`Minimap`、`StatusIcons`／`MagicStatus`、`CharacterConfig`、`DisplayNotices`…），**不新增封包解析**。模組需要的欄位不存在時，該任務負責在 `NativeUi/State/<Module>State.cs` 加轉接；伺服器沒送的值就顯示空白，**不推算、不補假值**。
- `IUiCommands`：對外動作，例如 `ActivateSlot`、`OpenWindow`、`SendChat`、`ToggleMinimap`、`PartyLeave`…，全部轉呼叫既有的 action，例如 `PlayerHudActions.Activate`。UI 模組不直接碰 `LoginConnection`。
- `UiPanelRegistry`：C2 建立的單一註冊檔。每個模組任務只在這裡加**一行**註冊，衝突時保留兩邊的行。
- 同一格的切換：`MINI_MAP_WINDOW`、`EX_MACRO_WINDOW`、`PARTY_PET_RT_WINDOW` 都位於 (485, 475)，由 C2 提供的 `UiSlotGroup("BAR_485")` 管理，一次只顯示一個。觸發方式待驗證（V5）；在此之前先由選單或按鈕切換。

### 5.2 檔案擁有權與共通驗收

- 每個模組只能新增或修改：
  - `engine/source/NativeUi/<Module>*.cs`
  - `engine/source/NativeUi/State/<Module>State.cs`
  - `engine/_integration/dev/native-ui-tests/<Module>Checks.cs`
  - `DemoChecks.csproj`／`demo-tests/Program.cs` 各加一行
  - `UiPanelRegistry.cs` 加一行
  - `rebuild-lock.json`：用 `rebuild.py --update-lock` 重產
- 要改其他檔案，必須在 PR 說明理由。
- **共通驗收**（每個 code 任務都要做到）：
  1. 離線測試以**程式合成**的 `.img`／XML 驅動（不讀真實客戶端），驗證元件的邏輯座標、命中測試、狀態轉換與 null surf 不畫。
  2. 本機選用檢查：有 `TW_CLIENT_ROOT` 時，以真實 `Tile.pak`／`Sprite*.pak` 渲染 800×600 畫布，PNG **只輸出到 `scratch/`**，在報告中與 `ui_img_render.py` 渲染的底圖目視比對，並列出元件座標表。
  3. `git ls-files` 不得新增任何 `.img`、`.xml`（UI）、`.png` 或二進位遊戲資料。
  4. `rebuild.py --build` 成功，既有 demo-tests 全過。
  5. 報告分成「已驗證／推測／待驗證」。

### 5.3 任務清單

`id` 與 `docs/plans/issue-448.json` 相同。runner 預設為 `codex-local`，查證任務為 `claude-local`。

#### 框架（先做）

| id | 目標 | 可改檔案 | 驗收 | 依賴 |
|---|---|---|---|---|
| **ui-core-resources** (C1) | `ClientArchive`、`UiXmlSource`（解密、`-c` 退回、編碼）、`UiLayout`／`UiNode`、`SurfCatalog`（SurfList、17 檔聯合索引、重名拒絕、`.img` 解碼、記憶體 LRU）、pin 表 | `NativeUi/ClientArchive.cs`、`UiXml*.cs`、`UiLayout.cs`、`SurfCatalog.cs`、`UiSurface.cs`、`native-ui-tests/ResourceChecks.cs` | 合成的加密 XML（含尾段不足 16 byte）解回原文；合成 idx/pak 的邊界、截斷、重名、pin 不符都會拒絕；本機選用檢查：65 個 XML 全部可解析，`3168`／`3176`／`3279` 的尺寸符合 T161 表 | — |
| **ui-dev-window** (C3) | 第 4 節：`DevToolsForm`、開發快捷鍵搬到 `Ctrl+Shift`、場景不再扣面板寬度、證據擷取邏輯畫布 | `WebPanel.cs`、`ProductEntry.cs`、`Program.cs`（快捷鍵與 bar）、新檔 `DevToolsForm.cs`、相關 tests | 開關開發工具時，主視窗場景矩形不變；F1–F12 在 `normalProduct` 不再觸發開發功能；WebView2 安全設定測試照舊通過；既有 web-resource-tests、world-ui-tests 通過 | — |
| **ui-core-canvas** (C2) | `UiRoot`、`UiCanvas`、`UiPresenter`（整數倍置中、選用填滿）、`UiInput`（座標對映、命中、拖曳、焦點）、`IUiPanel`／`IUiGameState`／`IUiCommands`、`UiPanelRegistry`、`UiSlotGroup`、`UiText`、`nativeUi` 開關，並把世界層接到 `WorldRect` | `NativeUi/UiRoot.cs`、`UiCanvas.cs`、`UiPresenter.cs`、`UiInput.cs`、`UiContracts.cs`、`UiPanelRegistry.cs`、`UiText.cs`；`Program.cs`／`PlayerHudIntegration.cs` 只加開關分支 | 視窗 800×600／1600×1200／1920×1080 分別得到 s=1／2／1，偏移正確；黑邊點擊不傳遞；拖曳限制在畫布內；開關關閉時，既有全部測試與舊 HUD 行為不變；開關打開但 pin 不符時，退回舊 HUD 並顯示錯誤 | C1、C3 |

#### 模組（C2 合併後可平行）

| id | 目標 | 可改檔案（`NativeUi/` 下） | 驗收重點 | 依賴 |
|---|---|---|---|---|
| **ui-left-status** | `LEFT_WINDOW` (0, 475)：底圖 3168、等級條 3169 與等級數字、EXP%、AC、MR%、陣營圖示 3170–3172 與陣營值、SP、負重條 3173–3175 與數字、飽食、金幣（座標見 T161 第 2 節）。先盤點 `IUiGameState` 已有哪些欄位，缺的列表 | `LeftStatusPanel.cs`、`State/LeftStatusState.cs` | 每個子元件的邏輯座標等於 XML 座標＋(0, 475)；三種陣營、三段負重的 surf 切換；缺值顯示空白；真實渲染 PNG 放在 scratch | C2 |
| **ui-hpmp** | HP／MP 浮動窗與頭上 HP／MP 條（依 V1 結果） | `HpMpPanel.cs`、`State/HpMpState.cs` | 座標、尺寸、顏色與 V1 報告一致；`showCharacterHPMP` 等設定的行為與 V1 一致；HP／MP 只取伺服器值 | C2、V1 |
| **ui-macro-bar** | `RIGHT_WINDOW` (643, 476) 巨集框：底圖 3176、**F5–F12 共 8 格**（格距依 V5）、3 個分頁（action 10–12）、選取框、鎖定鈕；`EX_MACRO_WINDOW` (485, 475) 擴充巨集。原生模式下 F5–F12 觸發格子，F1–F4 依 V5 | `MacroPanel.cs`、`ExMacroPanel.cs`、`State/MacroState.cs` | 8 格 × 3 分頁的綁定與觸發，用合成 Item／Magic 驗證；存檔沿用 `CharacterConfig`，原版 protobuf 格式**不在本任務**範圍，報告要列出差異；舊 F1–F4 綁定移轉規則寫進報告 | C2、V5 |
| **ui-menu-buttons** | `RIGHT_WINDOW` 選單按鈕列 y=91：Map(2)／Mail(21)／Status(40)／Spell(59)／Item(78)／Pledge(97)／Option(116)／Quit(135)，action 13、1–7。按鈕狀態圖依 V4（V4 完成前只用第 0 張） | `MenuButtonsPanel.cs` | 8 個按鈕的命中矩形；每個 action 對應到 `IUiCommands` 的既有功能（沒有原生視窗的，打開開發工具中對應的分頁，並記錄為過渡）；Quit 走既有登出確認 | C2 |
| **ui-chat-window** | `CENTER_WINDOW` (129, 470)：底圖 3279 底部錨定 y=600、以 36 px 伸縮（130 到 240）、上下鈕 (345, 115)、輸入列 2273 (10, 114)、8 個頻道分頁、5 個過濾圖示、左右分割 3288／3290 | `ChatPanel.cs`、`State/ChatState.cs` | 伸縮的每一階高度與錨定；分頁或過濾切換只影響顯示；輸入焦點沿用 `PlayerChatKeyboardFlow`；捲動與行數上限 | C2 |
| **ui-chat-format** | 聊天行格式與顏色照原版（T161 第 3 節表格）：`name: msg`、`[name]`、`{name}`、`{{name}}`、`(name)`、`-> (target)`；RGB555 調色盤索引色；補齊類型 12／13／14／15／17 的顯示。**只改已驗證的部分**；前綴→頻道對照是推測，不在本任務範圍 | `PlayerChat.cs`、`PlayerHudChat.cs`、`NativeUi/ChatPalette.cs`、player-chat-tests | 每一種類型的格式字串與色值等於 T161 表；`*` 開頭的黃字、全體 CP950 `*` 紅字；舊 HUD 與原生聊天窗共用同一套格式 | —（可立即開工） |
| **ui-minimap** | `MINI_MAP_WINDOW` (485, 475) 框 3275＋底 3274（地圖區 135×101 位於 +12, +20），內容依 V5；`Field_Map` 小窗 (5, 250) 161×121（`MapSystem-c.xml`：png 地圖、8 方向箭頭 2818–2825、隊友點 2826、關閉鈕與縮放鈕）。原生模式下由 Map 按鈕開關，**不再佔用 F5** | `MiniMapPanel.cs`、`FieldMapPanel.cs`、`State/MapState.cs` | `MapSystem` 的 `ratio`／`baseLoc`／`basePixel` 換算以合成資料驗證；8 方向箭頭選圖；與 `BAR_485` 槽位互斥；png 地圖從客戶端執行期讀取 | C2、V5 |
| **ui-party** | `PARTY_INTERFACE`：底板 3204 (0, 493) 蓋在左狀態框上；分頁鈕 y=481；隊員量表 3211／3212；離開、踢除、委任 y=574；`PARTY_PET_RT_WINDOW` 3220 放在 `BAR_485` 槽位 | `PartyPanel.cs`、`PartyRtPanel.cs`、`State/PartyState.cs` | 隊員 HP 比例對應到量表寬度（58 px）；分頁切換；按鈕呼叫既有 Party 動作；與左狀態框的上下層次正確 | C2 |
| **ui-bookmark** | `BookmarkUI.xml` 263×420 可拖曳視窗（版面見 T161 第 6 節）：快速移動清單、記憶座標清單＋捲軸、區域下拉、隨機傳送與排序、13 色調色盤、確認／取消 | `BookmarkPanel.cs`、`State/BookmarkState.cs` | 清單捲動與選取；拖曳限制在畫布內；新增／刪除走 T124 的既有邏輯。**T124 合併前不開工** | C2、T124（#258）合併 |
| **ui-buff-icons** | 狀態圖示，依 V3 的 surf 對照、位置與排序 | `BuffIconsPanel.cs`、`State/BuffState.cs` | 圖示選圖、排序與剩餘時間顯示都和 V3 一致；沒有對照的 skill **不畫**，並記錄下來（不畫替代菱形） | C2、V3 |
| **ui-red-notice** | 紅字（畫面中央系統訊息），位置、字色、時長依 V2 | `RedNoticePanel.cs` | 與 V2 一致；沿用 `DisplayNotices` 的佇列 | C2、V2 |

#### 收尾

| id | 目標 | 依賴 |
|---|---|---|
| **ui-native-smoke** (test，dot) | 在 P24Lab 測試角色開 `nativeUi`，真機跑一輪：底列、聊天、巨集 F5–F12、選單、小地圖、隊伍、書籤、狀態圖示、紅字、縮放 1×／2×；截取邏輯畫布並與原版截圖對照 | 上表全部 code 任務 |

---

## 6. 待查證項目（先開查證，再開 code）

| id | 項目 | 線索 | 阻擋 |
|---|---|---|---|
| **V1** `ui-verify-hpmp` | HP／MP 浮動窗與頭上條的座標、尺寸、surf、顏色、預設開關 | `Lineage.ClientConfig.FloatingHPMP`、`showCharacterHPMP`、`HPMPBarThick`、`HPMPBarRecovery`、`HPMPBarPercent`；解析者 `sub_919F30`（VA `0x919F30`），往繪製端追 | ui-hpmp |
| **V2** `ui-verify-red-notice` | 紅字／畫面中央系統訊息的位置、字型、顏色、時長、最多幾行 | 在 BIN 中找繪製呼叫（從 S_SystemMessage 類處理往下） | ui-red-notice |
| **V3** `ui-verify-buff-icons` | 舊封包（S_SKILLHASTE、S_SKILLBRAVE、`STATUS_EFFECT_*`…）對應的圖示 surf、畫面位置、排列順序、剩餘時間顯示 | `Sprite.pak` 的 `SpellIcon.idx`；除錯字串 VA `0x1183E1C`–`0x1184C34`；d287 不送 `SC_SPELL_BUFF_NOTI` | ui-buff-icons |
| **V4** `ui-verify-runtime` | 框架層細節：(a) `.img` 旗標欄位與透明色規則；(b) 按鈕 surf 範圍 `a~b` 的狀態順序；(c) XML 實際解碼的編碼；(d) 缺 `-c` 檔時的退回行為；(e) Sprite 重名時的封存檔優先序；(f) UI 文字字型與大小 | `sub_B3D390`、`.img` 載入者（從 `SurfList` 字串 xref 追）、按鈕類別的繪製函式 | C1 可以先做（用暫定規則並集中在一處）；ui-menu-buttons 的狀態圖切換 |
| **V5** `ui-verify-bar-behavior` | 底列行為：(a) 巨集 8 格的座標與格距；(b) F1–F4 的原版用途與其他快捷鍵（含 Map）；(c) 世界畫面是否延伸到底列後方；(d) `MINI_MAP_WINDOW` 地圖區是即時縮圖還是 png；(e) `BAR_485` 槽位（小地圖／擴充巨集／隊伍 RT）的切換觸發 | `sub_A9AD80` 之後的巨集框繪製；`MINI_MAP_BASE` 字串（VA `0x119C818`）的 xref；鍵盤處理表 | ui-macro-bar、ui-minimap；(c) 會影響 C2 的 `WorldRect` 常數，但不阻擋 C2 |

查證任務沿用 T161 的方法（IDA 7.7 開 P01 IDB 在 `scratch/` 的副本，唯讀分析未加殼的 `TW1608262200.bin`）。產出併入 `docs/reference/ui-layout.md` 的對應節（新增或更新「已驗證」小節），並附 VA／File Offset。

---

## 7. 開發順序

```
V1 V2 V3 V4 V5  ──────────────────────────────┐（與框架同時進行）
C1 ─┐                                          │
C3 ─┴─► C2 ─► ui-left-status ∥ ui-menu-buttons ∥ ui-chat-window ∥ ui-party
              ui-macro-bar(V5) ∥ ui-minimap(V5) ∥ ui-hpmp(V1) ∥ ui-buff-icons(V3) ∥ ui-red-notice(V2)
              ui-bookmark(T124)
ui-chat-format（無依賴，可立即開工）
全部 ──► ui-native-smoke（dot 真機）──► 主控決定是否把 nativeUi 設為預設、移除舊 HUD
```

C1 與 C3 可以平行。C3 會改 `Program.cs`／`WebPanel.cs`，C2 只在 `Program.cs` 加開關分支，所以排在 C3 之後，避免同時改同一段。

---

## 8. 本輪唯讀抽查紀錄

都在 `scratch/` 執行，沒有任何輸出進 git：

- `ui_xml_decrypt.py` 解出 65 個 XML 到 `scratch/task448-uixml/`。檔名清單證實 `uistatus86-c.xml`、`uiextra86-c.xml`、`MapSystem-c.xml`、`ReNStatusWinUI-c.xml`、`ActionUI-c.xml` 有 `-c` 版，`BookmarkUI.xml` 沒有 `-c` 版。
- `uistatus86-c.xml` 抽查：
  - 屬性 `=` 兩側有空白。
  - `surfNumber` 有 `5726~5728` 範圍寫法。
  - `CHAT_WINDOW` 使用 `yPos="600"` 底部錨定。
  - 元素名稱有原文拼字（`FOOD_GUAGE_BAR`）。
  - 只有 3 個 XML 宣告 `encoding="UTF-8"`。
- 掃描 17 個 `Sprite*.idx`：`.img` 共 7,543 筆、名稱 7,490 個；53 個名稱重複（46 個內容不同），全部小於 1500；第 2.3 節列出的 6 個 UI surf 各只有一份。
