# Issue #448 報告：T174 原版 800×600 UI 框架設計（契約先行）

階段：design，第 1 次嘗試。分支：`task/448`。

## 做了什麼

- 新增 [`docs/design/native-ui.md`](../design/native-ui.md)，作為後續 UI 模組平行開發的契約，內容包括：
  1. **執行期資源讀取**（第 2 節）：
     - 從 `TW_CLIENT_ROOT` 唯讀讀取：解密 `Tile.pak` 的 UI XML（AES-128-CBC，尾段做 XOR），透過 `SurfList.xml` 對照，從 `Sprite*.pak` 讀 `.img`。
     - 圖檔分散在 17 個封存檔，合成一個聯合索引；同名但內容不同的檔案拒絕載入。
     - 只放記憶體 LRU；用 pin 鎖定來源檔，不符就停用原生 UI。
     - 不複製任何資源進 repo、zip 或 embedded resource。
  2. **座標與縮放**（第 3 節）：
     - 唯一的邏輯座標是 800×600，XML 座標直接用。
     - 預設整數倍放大並置中，最近鄰取樣；另有選用的等比填滿。
     - 滑鼠換算到邏輯座標，黑邊的點擊不傳遞；原生模式下 F1–F12 保留給遊戲。
  3. **開發面板**（第 4 節）：網頁面板、服務分頁、狀態列移到獨立的 `DevToolsForm`，開發快捷鍵改成 `Ctrl+Shift+F9…`，不再佔用遊戲畫面和 F 鍵。
  4. **模組拆分**（第 5 節）：
     - 共同介面 `IUiPanel`／`IUiGameState`／`IUiCommands`，以及檔案擁有權規則與共通驗收。
     - 3 張框架任務、11 張模組任務、1 張真機 smoke。
  5. **待查證**（第 6 節）：V1–V5，標明各自阻擋哪些 code 任務。
- 新增 [`docs/plans/issue-448.json`](../plans/issue-448.json)，共 20 項（resource 5、code 14、test 1），依賴關係已用 JSON 檢查，所有 `depends_on` 都指向存在的 id。

## 怎麼驗證

- 設計的依據是 `docs/reference/ui-layout.md`（T161）第 2–7 節，並對照現行程式：
  - `Program.cs:95`：1180×800 視窗
  - `WebPanel.cs:7`：365 px 網頁面板
  - `ProductEntry.cs:15-25`：服務分頁
  - `Program.cs:113-137`、`ClientNetwork.cs:58-65`：F 鍵占用
  - `GroundSpriteReader.cs`：既有 idx/pak reader 與 pin 做法
  - `OfflineClient.csproj`：預設 glob
  - `engine/_integration/dev/*-tests` 的測試慣例
- 在 `scratch/` 做了唯讀抽查（沒有任何輸出進 git）：
  - `ui_xml_decrypt.py` 解出 65 個 XML。確認哪些檔有 `-c` 版（`BookmarkUI.xml` 沒有）。
  - 在 `uistatus86-c.xml` 確認了四個語法細節：`=` 兩側有空白、`surfNumber="a~b"` 範圍寫法、`yPos` 底部錨定、原文拼錯的元素名。
  - 只有 3 個 XML 宣告編碼。
  - 掃描 17 個 `Sprite*.idx`：`.img` 共 7,543 筆、名稱 7,490 個；53 個名稱重複，其中 46 個內容不同，全部小於 1500。抽查的 6 個 UI surf 分布在 Sprite04／06／09／12／13／14，各只有一份。

## 未驗證什麼

- 本任務**沒有改產品程式**，沒有建置，也沒有啟動遊戲。
- 第 6 節 V1–V5 列出的項目都還沒查證：
  - HP／MP 浮動窗座標
  - 紅字位置
  - 舊封包狀態圖示對照
  - `.img` 旗標與透明規則、按鈕狀態順序、XML 編碼、`-c` 退回、重名優先序、字型
  - 巨集格距、F1–F4 用途、世界畫面範圍、小地圖內容、485 槽位切換
- 設計中的暫定規則（CP950、色鍵透明、按鈕只用第 0 張、世界層畫滿 800×600）都標為推測或待驗證，並且集中在單一位置，查證後只需改一處。
- 書籤模組依賴 T124（#258），該任務目前仍在 review。

## 產出路徑

- `docs/design/native-ui.md`
- `docs/plans/issue-448.json`
- `docs/reports/issue-448.md`（本檔）

## 給下一棒的提示

- **可以立即開工**（沒有依賴）：
  - V1–V5 五張查證（claude-local）
  - `ui-core-resources`（C1）
  - `ui-dev-window`（C3）
  - `ui-chat-format`
- C2（`ui-core-canvas`）要等 C1 與 C3 合併。C3 會改 `Program.cs`／`WebPanel.cs`，C2 只在 `Program.cs` 加開關分支，所以排在後面，避免同時改同一段。
- 模組任務只新增 `engine/source/NativeUi/<Module>*.cs`，共用檔只加一行註冊。`rebuild-lock.json` 衝突時一律用 `rebuild.py --update-lock` 重產。
- `nativeUi` 預設關閉。切換成預設與移除舊 HUD，由主控在 `ui-native-smoke` 通過後另開任務。
- 金鑰與 IV 已經在 `docs/reference/rea-scripts/ui_xml_decrypt.py` 中（取自 BIN 的常數），C1 直接沿用，不要再另存一份解出的 XML。
