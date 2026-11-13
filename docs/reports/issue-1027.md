# Issue #1027 — B15 升級為主線：拆分實作任務

日期：2026-11-13；執行者：本機 Claude（claude-local）；分支：`task/1027`；階段：design。只改文件，沒有改程式。

## 結論

1. B15 拆成 **32 張任務（T444–T475）**，列為新里程碑 **M8 可攜交付**。規劃檔 `docs/plans/issue-1027.json`，每張有範圍、驗收、預算（1–3 小時）與 runner。
2. **派工文字的前提有一項不成立**：#101（來源查核）與 #103（準備器核心與 TBT 切片）**沒有執行過**。兩張都在 2026-11-06 01:38 UTC 依 #99 的裁定以 not planned 關閉，#101 的執行器在開工一分鐘後被終止。repo 裡沒有這兩張的報告、PR 或程式（`engine/tools/build-assets` 不存在）。
3. 所以 terrain／bounds、skills／cooldown、motion／混色、Text 版本**目前都沒有 pass／blocked 結果**。本張沒有可以沿用的判定，也沒有自己下判定（那需要實際解碼與比對，超出 design 階段）。改成開 7 張來源閉合任務（T444–T450）重新查核，準備器核心（T451）從頭做。
4. 「blocked 項目的明確結論」目前是：**尚未判定**。每張來源閉合任務都寫了 blocked 時的處理：寫明缺什麼欄位、列為已知限制交主控，不用 SQL／JAR／研究輸出／既有 assets 補；對應的準備器任務只做已閉合的部分（T458 在 terrain 判 blocked 時直接不寫程式）。T452 負責把所有 blocked 項收成一張已知限制清單。

## 做了什麼

- 讀 `CLAUDE.md`、`docs/REPORT.md`、#97 設計（`docs/design/portable-delivery.md`）與報告、#102 報告中 32 個 D 檔的逐檔表、#110 報告；用 `gh issue view` 讀 #101、#103 的內文與留言。
- 盤點產品現況（只讀原始碼）：哪些程式讀 `engine/assets`、已有哪些執行期讀原客戶端的元件、launcher 與 CI 拒收檢查的現行規則。
- 寫 `docs/plans/issue-1027.json`（32 項）。
- `TASKS.md`：新增「M8 可攜交付」區段（32 列），Backlog B15 標為已升級。
- `PLAN.md`：現況、不確定項 U10（+32）、主線封頂（378 → 410）、里程碑 M8。

### 任務分組

| 組 | 任務 | 張數 | runner |
|---|---|---:|---|
| 來源閉合（唯讀，Python 原型對本機基線比對） | T444 Text、T445 terrain／bounds、T446 規則表、T447 動作時序與 gfx 清冊、T448 motion／混色、T449 SPR 類、T450 地圖類 | 7 | claude-local |
| 核心與契約 | T451 準備器核心加 TBT 切片、T452 recipe 契約與已知限制清單 | 2 | claude-local |
| 各素材類別的 C# 準備器 | T453 文字、T454–T455 SPR 類、T456 地圖原檔、T457 scene 與特效、T458 terrain、T459 motion／混色、T460 音效 | 8 | claude-local |
| 去伺服器依賴 | T461 動作時序與職業 gfx、T462 skills／magic | 2 | claude-local |
| 移除 9 個已知例外 | T463 pickup（2 檔）、T464 EXP 表與 ground-catalog（2 檔）、T465 測試向量（3 檔）、T466 work/ 歷史證據（2 檔）、T471 CI 收尾 | 5 | claude-local |
| 總流程與接線 | T467 總流程與 integrity、T468 `--prepare-assets` 與 launcher、T469 web 圖片 | 3 | claude-local |
| 封裝與文件 | T472 zip 白名單、T473 README | 2 | claude-local |
| 實跑與真機 | T470 真客戶端全量準備並與 r29 基線比對、T474 乾淨目錄解壓到登入畫面 | 2 | codex-local、dot |
| 結案 | T475 | 1 | claude-local |

依賴：T444–T451 可以同時開工 → T452 → T453–T466 → T467 → T468 → T470 → T472–T475。T466、T469 不必等契約。

### 為什麼是 32 張，不是 #97 估的 20–36 小時

- #97 的估算是靜態估，而且假設來源查核（2–4 小時）一次做完。實際上要查的有 7 個互不相干的主題，每個都要解碼加全量比對，各自 2–3 小時。
- 本機 Claude 不能執行編譯出的程式，所以每張準備器任務只能做到「合成資料的離線測試加 CI」。真客戶端的等價性改成兩道：先在來源閉合階段用 Python 原型對基線比對（Python 解析腳本是允許的），C# 寫完後再由 T470 實跑一次。
- 9 個已知例外的處理方式各不相同，分成 4 張加 1 張收尾。

## 已驗證

- **#101、#103 沒有執行**：`gh issue view 101`、`103` 的留言顯示兩張都以 not planned 關閉；`docs/reports/` 沒有 `issue-101.md`、`issue-103.md`；`docs/REPORT.md` 沒有這兩號的索引；沒有 `task/101`、`task/103` 的 PR 或遠端分支；`engine/tools/` 下只有 `ResourceChecks`、`check-local-resources.py`、`check-no-game-assets.py`、`game-assets-allowlist.json`、`resolve-path.py`。
- **9 個已知例外仍在**：`engine/tools/game-assets-allowlist.json` 的 `b15Exceptions` 有 9 列，與 #110 的清單一致。
- **#97 之後產品已有、可以重用的元件**（讀原始碼開頭的說明確認存在，沒有執行）：
  - `engine/source/TextArchive.cs`：Text.idx／pak 的執行期讀取（#546）。
  - `engine/source/NativeUi/ClientArchive.cs`：idx／pak 共用讀取，含 Sprite、Sprite00–15、Tile 的 pin（#461）。
  - `engine/source/NativeUi/ItemIconSource.cs`：執行期從 Sprite 讀 `<icon>.tbt` 並解碼（#706）。
  - `engine/source/GroundSpriteReader.cs`：SPR 幀解碼與 pin 驗證（#190）。
  - `engine/source/NativeUi/SurfCatalog.cs`、`UiXmlSource.cs`：`.img` 與 UI XML。
- **#97 設計文件有兩處已過時**：`engine/assets` junction 現在指向 `releases/r29/assets`（設計寫 r28）；22 張 web PNG 已由 T035（#109）移出 git，`.gitignore` 有 `/engine/web/icons/`、`/engine/web/ground/`。T452 會順手修訂。
- **仍讀預先產生的 `engine/assets` 的程式**：二十多個檔，主要是 `Assets.cs`、`OriginalMapCache.cs`、`MapEffects.cs`、`ObjectAssets.cs`、`PlayerProfiles.cs`、`PlayerShadows.cs`、`Portals.cs`、`SceneOpacity.cs`、`Renderer.cs`（混色表）、`AudioAssets.cs`；其中 7 個檔（`Assets.cs`、`MapEffects.cs`、`ObjectAssets.cs`、`OriginalMapCache.cs`、`PlayerProfiles.cs`、`PlayerShadows.cs`、`Portals.cs`）用 `integrity.json` 的 SHA 驗檔。
- **launcher 的硬性要求**：`engine/_integration/dev/launcher-source/Program.cs` 第 57–66 行要求 manifest 含 `assets/integrity.json`、schema 為 `p06-assets-integrity-v1`，並逐檔核對存在與大小。T468 要改這一段。
- 規劃檔格式：用 Python 驗過是陣列、32 項、欄位齊全、id 不重複、stage 合法（code 21、resource 7、test 2、design 1、plan 1）、`depends_on` 都存在且沒有循環。

## 推測

- **terrain 有機會只靠客戶端閉合**：S32 有 attribute 層（`docs/protocol/portals.md` 記錄了區段順序），伺服器的 `maps/*.txt` 一般是從這一層轉出來的。這只是合理猜測，T445 要拿 713 份基線逐格比對才算數。
- **Text 解碼查表（Map1–5.bin）的問題可能已經不存在**：產品的 `TextArchive` 已經能解 Text.idx 與 payload。但 html 與 list.spz 內層是否還有另一層編碼沒有確認，交 T444。
- **混色表多半是簡單算式**，T448 以逐 byte 相符為準。
- **EXP 表與 ground-catalog 的模板列最可能判 blocked**：前者原本就來自 SQL，後者來自原服模板。T446 會先查能否改成不需要這兩張表。
- 32 張、每張 1–3 小時，合計約 60–80 小時；T470、T474 實跑後估還要 0–6 張修正（PLAN 的 U10）。

## 待驗證（本張沒做）

- 四個來源缺口與其他類別的 client-only 等價性：全部沒有驗，交 T444–T450。
- 沒有用 PakViewer 查 pak：本張沒有宣稱任何「缺資源」，查 pak 的要求寫進了 T444、T446 的驗收。
- 沒有建置、沒有執行任何程式；沒有讀 SQL／JAR／DB；沒有碰封存與客戶端。
- 本張只改文件。CI 結果以 PR 為準，push 當下尚未取得。
- 調度器能否照 `depends_on` 正確建出 32 張 Issue：格式比照 `docs/plans/issue-448.json`，合併後才知道。

## 需要主控或使用者決定

1. **M8 的驗收文字**（`TASKS.md` M8 區段）是本機 Claude 起草的，請主控與使用者過目後再凍結。
2. **主線分母 378 → 410**：依使用者 2026-11-13 同意升級 B15，記為 U10 兌現 +32。張數比 #97 的估計多，理由見上。若要縮小範圍，最容易砍的是 T465、T466（測試向量與歷史證據，不影響可攜 zip 能不能跑）。
3. **T466**：#110 裁定兩個 `work/` 歷史證據不刪除，B15 要把例外清到 0。任務裡的預設做法是把 `pixels` 陣列換成筆數加 SHA（原檔留在 git 歷史），開工前要主控裁定。
4. **T470 需要 Codex 或 dot**：本機 Claude 不能執行編譯出的程式。PLAN 目前寫「runner 一律 claude-local」「等 Codex 恢復」；Codex 還不能用的話，T470 改派 dot。
5. **是否建立 GitHub Release**：屬於對外發布，T472 只在 CI 驗清單、不上傳；T475 會列成選項。

## 產出路徑

- `docs/plans/issue-1027.json`
- `docs/reports/issue-1027.md`
- `TASKS.md`（M8 區段、B15 列、標頭）
- `PLAN.md`（現況、U10、主線封頂、M8）

## 給下一棒的提示

- 來源閉合 7 張與 T451 互不依賴，可以同時派。每張的 Python 腳本與輸出只放 `scratch/`，結論寫進 `docs/reference/asset-recipes/<主題>.md`，不要提交解出的內容。
- 比對用的基線是主目錄的 `engine/assets`（junction → `releases/r29/assets`），只讀。worktree 裡沒有這個 junction。
- 舊的研究 prepare 腳本（P04／P05／P06／P22／P24）會寫產品目錄、複製研究基線、讀 SQL／JAR，**只能讀來參考算法，不要執行**（#97 的提示仍然有效）。
- scene 的現行 schema 是 `p22-dual-map-v1`，P24 有同名腳本會寫另一個 schema，不要弄混。
- 新模組用 attribute 自動發現（`[AssetRecipe]` 由 T451 定義，測試用 `[CheckGroup]`）；T463 需要改 `OfflineClient.csproj` 拿掉兩個 EmbeddedResource，這是移除例外必要的改動，已寫在任務裡。
- PR 不改 `rebuild-lock.json`；與 main 同步時 lock 取 main 的版本。
