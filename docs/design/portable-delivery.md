# T033 可攜交付與使用者本機素材產生設計

Issue #97，2026-11-06。來源基準為 `0fab537415a84943322e9f542d8d16f6abe34914`。本文件完成唯讀盤點與方案設計，實作與原生驗收待後續任務。

## 2026-11-13 修訂（T452／#1037）

本文件是 2026-11-06 的盤點與方案設計，以下幾處已經過時。實作契約以 [`asset-recipe.md`](asset-recipe.md) 為準；兩份文件不一致時看那一份。本節以下的原文保留作為當時的紀錄，過時處在原位加了〔修訂〕註記。

- **`engine/assets` junction 現在指向 `releases/r29/assets`**（#155、#181），不是 r28。r28 的素材在 #153 受損、#157 還原；比對用的基線是 r29（同樣是 13,957 檔、19 類）。
- **22 張 web PNG 已經移出 git**（T035／#109）：`engine/web/icons/`、`engine/web/ground/` 在 `.gitignore`，`engine/web/` 只剩 8 個文字頁面檔。F9 面板的圖示改由執行期即時解碼（T469／#1054），ground 圖由 `GroundSpriteReader` 即時產生（#190）。所以「22 張 PNG 交由本機準備功能生成」不再需要，準備器不產生 web 圖片。
- **全 repo 的追蹤檔與 embedded resources 已經分類**（#102），拒收檢查在 CI（`engine/tools/check-no-game-assets.py`）。已知例外從 9 列降到 7 列（T466／#1051）。
- **六項前置的結論**（T444–T450，2026-11-13）：

| 前置 | 原本的疑問 | 結論 | 出處 |
|---|---|---|---|
| 1 | terrain 與 bounds 能否只靠客戶端 | bounds 與通行位元全部可以；8 張地圖 13,567 格的區域位元不行（產品不讀） | `asset-recipe.md` L3、`terrain.md` |
| 2 | skills、`serverActionMs`、職業 gfx | 技能名稱有客戶端來源、等級是算式；`serverActionMs` = 40 ms × `list.spz` 的 tick 總和，基線全部相符；產品不需要職業 gfx 表 | `rule-tables.md`、`action-timing.md` |
| 3 | motion 與混色表 | 三張表是整數算式，逐 byte 相同；motion 每欄都有來源。不需要 BIN、IDB、Unicorn | `motion-blend.md` |
| 4 | Text 版本差異 | 已查清：關鍵三個 entry 沒變，舊 offset 不能用，176 個模板只差圖片編號 | `text.md` |
| 5 | 22 張 web PNG | 已移出（見上） | #109、#1054 |
| 6 | `Map1.bin`–`Map5.bin` | 就是標準 DES 的五張表，產品的 `TextArchive` 已經涵蓋，不需要這五個檔 | `text.md` 第 6 節 |

- **新出現的 blocked 項**不在原本的六項前置裡：EXP 門檻表與 `ground-catalog.json` 的模板列（`asset-recipe.md` L1、L2）。
- **「本機生成與啟動」的調整**：staging 與收據的實作見 T451（#1036）；輸入驗證不再用整包 SHA（`asset-recipe.md` 1.1）；地圖原檔、TIL、LAC、Sound 採 provider 直讀；`audio-manifest.json` 改成程式內的 pin；gfx 採「遇到再準備」，首次只預熱。
- **工作量**：原估 20–36 小時、首批三張。實際拆成 M8 的 32 張（T444–T475，#1027），本次再加 3 張（T476–T478）。「首批三張」表格裡的 `portable-source-closure`、`portable-preparer-core` 當時沒有執行（#101、#103 以 not planned 關閉），由 T444–T451 取代。

## 結論與必要前置

建議採 **C 混合方案**，將可重用的解碼算法整理成引擎內的 C# 本機素材準備功能。SPR、TBT、文字與需要預處理的資料在使用者電腦產生，地圖容器與聲音可由 provider 唯讀存取使用者指定的 `TW_CLIENT_ROOT`。發布包只納入程式、web 原始頁面與說明。使用者端的所有派生資料都在解壓目錄下產生。

〔修訂 2026-11-13：下面六項前置都已有結論，見上一節的表。〕

**U6 仍有來源缺口，現階段不能承諾只給原客戶端就能重建完整 r28 素材。** 前置查核找到下列實際依賴。

1. `cache/original-map0.txt`、`terrain0.bin`、`maps/terrain/*.bin` 使用 P24Lab 伺服器 `server/maps/*.txt`。地圖 bounds 使用研究清冊中的靜態 SQL metadata。從客戶端 S32／SEG 產生等價移動地形與 bounds 的方法待證實。
2. `dialogs/skills.json` 的 807 筆名稱／等級來自 `380_db.sql`。玩家與部分 NPC 的 `serverActionMs` 使用同檔的 `spr_action`，職業 gfx 集合曾由 JAR 常數導出。客戶端 metadata 的動畫 ticks 與伺服器冷卻分別被產品使用，等價替換待證實。
3. `motion.json` 與三張混色查表的研究生成鏈使用外部 BIN／靜態匯出／Unicorn。必須移植可核對的算法與產品規則，補齊數值等價測試，才能解除研究執行環境依賴。
4. 歷史文字輸入與本機現有 `Text.idx`／`Text.pak` 的全檔 SHA 不同，見附錄。尚未解碼比較相關 entries，因此目前既不能認定內容相同，也不能直接沿用舊 offset。A 方案的全檔一致要求會先遭輸入 pin 拒絕。
5. Git 目前追蹤 `engine/web/icons/*.png` 20 檔與 `engine/web/ground/*.png` 2 檔。研究腳本明示由 TBT／SPR 產生並複製到 web。這 22 檔須改成本機生成並移出追蹤後，才可宣稱該部分符合零遊戲資源交付。還須審查全 repo 的其他圖檔、JSON、測試 fixture 與 assembly embedded resources。
6. 歷史 Text 解碼器 `Lin.Helper.Core.Pak.PakTools` 讀取組件內的 `Resources.Map1.bin` 至 `Map5.bin`。這五份查表的產生與可重用依據尚未查明，純 C# 移植仍須釐清它們。直接夾帶研究 DLL 無法通過來源閉合與內容審查。

這些前置皆不可透過附送 SQL、JAR、BIN、研究輸出或現有 assets 解決。無法從指定原客戶端與自主程式規則閉合時，T033 應回報來源受阻，交 Claude 裁定後續工作。TASKS 的 M3 前置條件與完整遊戲功能須繼續滿足。

## 盤點方法與範圍

- 本機素材入口為主工作樹 `engine/assets`，junction 實際指向 `releases/r28/assets`〔修訂 2026-11-13：現在指向 `releases/r29/assets`，#155〕。本輪透過唯讀入口列檔、讀 manifest 與計算選定檔案 SHA，沒有建立資源副本或執行歷史腳本。
- `integrity.json` schema 為 `p06-assets-integrity-v1`，SHA-256 為 `07dd19f4407ec3f2bf61da9c00cca827cbbe206866863a87a77f17fce7a18b98`。
- 索引有 **13,957 筆、19 類、2,222,723,085 bytes**，無重複路徑。19 類實體檔數逐類與索引相同。另有索引本身 1 檔、1,729,333 bytes，合計 13,958 檔。本文未對全部素材重新做逐檔 SHA 核驗。
- 查閱 `docs/inventory/p01-p24.md`、`docs/ARCHITECTURE.md`、P04／P05／P06／P22／P24 的素材工具、P24 development 交接、`主管/` 的地圖清冊與外觀版本決策，以及 `work/engine-next-integration-r1/HANDOFF-r29.md` 的 13,957 檔延續證據。只檢查與素材相關的程式與紀錄。
- 下文 `P04`、`P05`、`P06`、`P22`、`P24` 分別代表根目錄 `P04-角色動畫`、`P05-單地圖`、`P06-離線客戶端`、`P22-魔法清單與單次施法`、`P24-原2x2地圖快取與跨區載入`。`DEV` 代表 `P24/reference/development`。這些本機研究路徑未隨 Git clone 提供。
- 表中的「已定位」表示靜態讀到工具及輸入路徑。重建成功、全檔一致與任意原客戶端相容性都待驗證。「來源不明」具體指出仍缺哪段生成鏈。

## 素材分類與來源

產生器與證據檔的完整路徑及 SHA 列於附錄。原檔以 `TW_CLIENT_ROOT` 為相對根，Text、Sprite、Tile 的容器 pin 與研究衍生中間檔 pin 也列於附錄。

| integrity 頂層類別 | 檔數 | 內容與生成來源 | 原始輸入／依賴與判定 |
|---|---:|---|---|
| `cache` | 378 | `P24/cache_assets.py` 產生 20 個 map0 S32、353 個 TIL、manifest 與 original-map0.txt。`cache_pixel_modes.py` 產生 3 個 channel.bin。 | `map/0/*.s32`、`Tile.idx/pak`。地形取 P24Lab `server/maps/0.txt`。混色取外部 BIN 模擬結果。工具已定位，客戶端單一來源尚未閉合。 |
| `character` | 103 | `P04/scripts/extract_sources.py` → `P04/build.py`、`sprite.py` → `P06/scripts/prepare.py`、`derive.py`。96 PNG 幀、animation／blocks、角色 entry、canvas checks、contact sheet、2 GIF。 | Text `list.spr/list.spz` 的 gfx0→sprite3225，主 Sprite `3225-0.spr` 至 `3225-15.spr`。依賴 Reference.dll／Lin.Helper.Core、Python Pillow、AES 解碼。已定位。 |
| `dialogs` | 2 | `DEV/npc-commerce-r1/decoder/Program.cs` 生成 templates.json。skills-source.json 與 HANDOFF 記錄 skills.json 的 SQL 欄位及輸出 SHA。 | Text `*-c.html/htm` 經 PakTools.Decode 與 Big5 產生 8,896 模板。skills 取 SQL `skills(skill_id,name,skill_level)` 807 筆。**skills.json 的獨立產生腳本來源不明**，資料來源已定位。 |
| `doors` | 2 | `DEV/inn-door-r1/prepare.py` 生成 2346.json 與 manifest。 | Text gfx2346 metadata、Sprite `2346-0.spr`，open28／close29。工具已定位，整支腳本還攜帶研究基線與 JAR 驗證，須抽出純轉換。 |
| `encountered-monsters.json` | 1 | `DEV/shop-appearance-fix-r1/build_monsters.py`、`encountered-monster-1108-r1/build_assets.py`、`encountered-map01-r1/build_assets.py` 及其安裝增量。 | Text 動作 metadata、Sprite 原幀、研究選定 gfx 清冊。清冊選取曾依 SQL spawn。已定位生成鏈，所有增量的精確重播順序待驗。 |
| `ground` | 4 | `P24/ground_assets.py`、`arrow_ground_asset.py`，各產生 JSON／PNG。 | Sprite `36-0.spr` 與 `66-0.spr`、Text metadata。圖像與背包 TBT 分開。已定位。 |
| `inventory` | 20 | `P24/inventory_assets.py`、equipment／ranged icons，後續 `DEV/basic-potion-haste-r1/prepare_visuals.py`、旅館鑰匙、船票、ruby／sapphire／online-gift 等 prepare.py 增量。 | 主 Sprite 中對應 `<icon>.tbt` → RGB555／RGBA → PNG，部分輸出同步 web/icons。已定位工具族，20 檔逐項版本重播待驗。 |
| `magic` | 1 | `DEV/skill-casting-r1/prepare_sources.py` 第 24–29 行從 SQL 選技能 1／3／4／8 並寫 generated/magic/catalog.json，apply_assets_and_ui.py 安裝。 | SQL 技能規則。歷史 generated 檔與現行 catalog 的 SHA 同為 `024576e3c96bedc33c63b889d77366f4204b99ceb40c78f3c743ff6e54a44ebf`，本輪已實測相符。原客戶端等價來源待查。 |
| `maps` | 13,304 | `DEV/maps-r1/build_maps.py`，map-effects-r1/build_effects.py，ship-route-implementation-r1/package_seg.py 與 package_lac.py。 | 7,198 map 原檔、5,052 TIL、713 terrain、337 effects 檔、3 LAC、1 manifest。客戶端 `map/**/*.s32`、`map/6/7fff8000.seg`、Tile 的 TIL／Water.lac／Pier1.lac／Pier5.lac、Sprite 特效、Text metadata。terrain 與 bounds 依伺服器及主管清冊。已定位，單一來源未閉合。 |
| `motion.json` | 1 | `P06/scripts/derive.py` 從 constructor push immediate 導出八方向，連同固定 166／664ms、spawn 寫檔。 | 歷史 `TW1608262200.bin`，VA `0x01146030`，image base `0x00400000`，RVA `0x00D46030`。工具已定位，使用者端須以已驗算法／產品規則取代研究匯出。 |
| `objects` | 54 | `P24/assets_objects.py`、combat／equipment／arrow／magic_assets.py，及 DEV NPC、怪物、施法、遭遇外觀增量。包含一般／武器／projectile／magic／world JSON、manifest、names。 | Sprite 各 gfx 對應 SPR 與 Text `list.spr/list.spz`。names 由 Text `desc-c.tbl` 經 Reference.dll 解碼。部分時序依 SQL。**names.json 最後序列化步驟來源不明**，解碼來源與 packed／decoded SHA 已定位。 |
| `players` | 39 | `DEV/player-appearance-r1/build_profiles_r3.py` 與 player_sprite_r2.py，後補 dagger 的 `encountered-dagger-appearance-r1/prepare.py`。 | 38 profile JSON＋manifest。主 Sprite 優先，主包缺少時要求替代包 bytes 唯一。Text 動作、SQL spr_action、JAR 職業常數。已定位，移除 SQL 依賴的等價性待驗。 |
| `scene` | 13 | `P05/build.py` 產生初始圖塊／圖集／圖像，`P22/dual_block_assets.py` 更新雙區命令／tile-pixels／manifest，`P24/opacity_sources.py` 產生 opacity。 | `map/0/7ffd8002.s32`、`7ffd8003.s32`、Tile.idx/pak。保留的初始預覽與雙區執行資料混存。現行 schema 是 **p22-dual-map-v1**，P24 同名腳本會寫 p24 schema，重播不可誤選。已定位，逐檔最終版本待驗。 |
| `shops` | 12 | `DEV/shop-appearance-fix-r1/build_assets.py` 及後續增量，生成 11 個 profile＋manifest。 | Text action70、Sprite 原幀，沿用固定主包／唯一替代政策。已定位，與 NPC 商業資料的區別由執行期 catalog 決定。 |
| `terrain0.bin` | 1 | `P22/dual_block_assets.py`，P24 有繼承版。 | P24Lab `server/maps/0.txt` 擷取 x32576–32639、y32896–33023 的 8,192 bytes。原客戶端生成方法**來源不明**。 |
| `world-appearances.json` | 1 | `DEV/encountered-map01-r1/build_assets.py`、preserve_existing_cow.py，rabbit／gludin-merchant 等增量。 | Text metadata、Sprite 原幀，gfx234 包含 body200 與六個衣飾。已定位，完整增量順序待驗。 |
| `shadows` | 18 | `DEV/player-shadow-implementation-r1/prepare.py`、player-shadow-coverage-r2/prepare.py。17 個 gfx JSON＋manifest。 | Text `101.shadow`、已生成 body profile 的 frame keys、Sprite 主包及唯一替代。已定位，需先產生 body。 |
| `audio-manifest.json` | 1 | `DEV/r27-6016-audio-module-r1/HANDOFF.md`、verify-assets.py，經 controls-r3 與 r28 host import 延續。 | WAV 的路徑／格式／hash 與 MP3 unavailable 記錄可重算。**現行 manifest 初次寫入工具來源不明**，現有檔含絕對研究路徑及 native 證據。 |
| `sound` | 2 | 同音訊 HANDOFF 與 verify-assets.py 證明直接複製，host import 接續納入素材。 | `Sound/1340.wav`、`Sound/3332.wav`，本輪實測 SHA 與 integrity 相同。可在新 provider 唯讀使用原檔，保留既有 fallback、pin 與生命週期政策。 |

### 素材外的發布污染面

`P24/inventory_assets.py`、`ground_assets.py` 與後續 icons 腳本會同時寫 assets 和 web，因此排除 `engine/assets` 尚不足以完成發布白名單。`engine/web` 的 8 個文字頁面檔可納入 zip，22 PNG 交由本機準備功能生成〔修訂 2026-11-13：22 張 PNG 已由 T035 移出 git，圖片改由執行期即時解碼（T469），準備器不產生 web 圖片〕。`OfflineClient.csproj` 與 `P24.Audio.csproj` 另嵌入 pickup、ground、combat-sfx JSON，須逐欄確認只有自主規則、來源定位與雜湊，禁止把原始圖像、文字表、時序表或素材 bytes 轉成 JSON／Base64 後藏進 assembly。

repo 零遊戲資源的驗收須明確覆蓋交付 commit 的所有追蹤檔。本次已確認 22 張 web PNG 的問題，其他追蹤檔仍待完整分類〔修訂 2026-11-13：已由 #102 完成分類，CI 有拒收檢查〕。既有 Git 歷史與研究證據頁的保留範圍須交 Claude／使用者確認，任何歷史重寫都需另外授權，本設計不把刪除目前檔案等同於清除歷史。

## 方案比較

下列為靜態盤點後的工程估算，包含來源閉合、移植與測試，缺件調查可能增加時間。

| 方案 | 作法與優點 | 缺點／工作量 | 原客戶端內容進 repo 或 zip | repo 外研究工具依賴 |
|---|---|---|---|---|
| A 全量精確重建 | 整理 `engine/tools/build-assets`，產生現行 13,957 檔，逐檔匹配舊 integrity SHA。可直接沿用既有讀取器與本機基線。 | 約 24–40 小時。Python、Pillow、NumPy、PyCryptodome、Unicorn／pefile、Lin.Helper.Core 與實驗資料需剝離。單純交付 .py 無法滿足僅 Runtime 的機器。PNG encoder、JSON 排序／換行／浮點與絕對路徑都影響 byte SHA。SQL 地形與 Text 版本已構成阻塞。 | **否**。只能提交純程式與非內容性 pins，所有原檔和派生輸出留在使用者機器。 | 原樣使用為「是」。正式採用前必須移植成 Runtime 可執行的 C# 或引擎組件，並移除研究／SQL／BIN／預建 Reference.dll 依賴。 |
| B 全部執行期直讀 | 新增 Text／SPR／TBT／S32／SEG／TIL／LAC provider，隨需解碼，降低預生成容量。GroundSpriteReader 與現有地圖解碼可重用。 | 約 40–64 小時以上。要改多個 catalog、渲染、動作時序、web 與 launcher 契約，需廣泛回歸。伺服器 terrain／skills／cooldown 仍須另解。 | **否**。唯讀使用者容器，快取只在本機。 | 最終應為「否」。目前產品 reader 覆蓋有限，其他格式仍須從研究移植並重驗。 |
| C 本機準備＋原檔 provider（建議） | 自主 C# 解碼與 catalog 準備保留既有模型，Sound／大型 map/TIL 可直讀。工具編進引擎，以 launcher 呼叫準備模式。使用者僅需兩種 Runtime。 | 約 20–36 小時，先做 1–2 小時來源關卡再校正。仍須處理 SQL 地形、時序、Text 版本。採版本化新 manifest，與舊 SHA 不一致的 metadata 必須有明確原因及語義比對。 | **否**。轉換工具本身只含算法。禁止把未知資料改以內嵌常數／壓縮表方式附送。 | 發布時必須「否」。研究腳本只供開發者唯讀參考，最終準備過程只讀 zip 程式與 TW_CLIENT_ROOT。 |

### A 的 SHA 驗收邊界

A 嚴格要求現有 integrity 的每個 path／size／sha256 全部一致，連 GIF、預覽 PNG 與歷史 metadata 都包括。相同輸入可重算的像素相同，仍可能因 encoder 或 JSON 格式使 bytes 不同。audio-manifest、cache／maps provenance 等含本機絕對路徑，也與換目錄運行的需求衝突。不能改舊 lock 或更新舊 SHA 來宣稱 A 通過。

C 應使用新的素材 schema／recipe 版本。對未改格式的 raw slices 繼續逐 byte 驗證。對格式調整的 JSON 比對 frames、RGB555、bounds、排序、動作 ticks、events 與有效集合。新 PNG 需驗 decoded RGBA、offset 與透明資訊。地形與 cooldown 必須有原資料或實機權威對照，不能靠自產 hash 自證正確。舊 integrity 保持本機基線角色，新 recipe pins 只保存必要路徑、大小與雜湊，禁止攜帶內容。

## 建議方案的實作契約

### 資源輸入與閉合關卡

1. 從使用者提供的絕對 `TW_CLIENT_ROOT` 尋找 Text／Tile／Sprite 容器、map 與 Sound，先驗格式、檔案身份及所需 entry。版本差異需回報具體容器／entry，禁止偷偷回退到開發機、release 或研究目錄。
2. 定位 Text.idx 需要先解碼索引。以檔名查找並驗證 entry 的 range 與 SHA，禁止將舊 File Offset 套到另一版 Text.pak。兩種文字 metadata 的相等性、主 Sprite 版本政策與替代包衝突處理均保留。
3. 地形關卡須證明 client S32／SEG 中的資訊足以建立產品所需 bounds、通行方向、靜態格資料與 cache 行為，對照既有本機基線。若不足，記錄明確缺欄並阻擋正式移植，不能把所有格子設為可走。
4. skills／spr_action 的關卡逐一列出讀取點與欄位，證明客戶端來源或自主計算可提供等價結果。已有 JAR 身份與 SQL SHA 僅作歷史證據，使用者端不得需要伺服器安裝、SQL、DB 連線或查詢帳密。
5. 混色與 motion 由純程式實作，與既有本機值全量比對。Byte-exact table 不符即停止採用，研究 BIN／IDB／Unicorn 都不得成為使用者前置。

### 本機生成與啟動

- 建議新增引擎 `--prepare-assets` 模式，由 launcher 在正常驗證前呼叫。此模式只處理檔案，結束後由 launcher 再驗證並啟動正常 UI。完成版本才可提示 ready。
- `engine/assets` 為使用者本機生成的普通目錄。寫入先進 `engine/runtime/assets-staging/<recipe-id>`，記錄輸入 SHA、工具版本、完成項目與輸出 SHA。全驗通過後在同磁碟交換成 active 目錄。若目標或祖先是 junction／symlink，拒絕寫入。
- 只允許同時一個 writer。失敗、取消、磁碟不足或中斷時保留可辨識的不完整狀態，再次執行先驗來源及已完成項目。已發布的 active 目錄須等新目錄完整通過後才替換。原客戶端一律唯讀。
- Web 虛擬站台保留 index.html 等程式頁，icons／ground 指向本機產生的資源目錄，並限制 URI 路徑〔修訂 2026-11-13：icons／ground 改由執行期即時解碼，不落地成檔，T469〕。發行 manifest 只認證程式與 recipe，生成收據另記內容。現有 launcher 強制要求 assets/integrity 與全檔存在，必須修改此接線後才能使用新流程。
- C 的聲音 provider 可參考 combat SFX 的原檔 pin 驗證。6016 目前有固定 audio-manifest SHA，需同時調整其版本契約並做 6016 回歸。Sound 缺失維持明確失敗／原既有 fallback 規則。
- 啟動所得登入畫面須使用正常產品入口。準備過程不得自動登入、啟動服務或連 DB。後續完整遊戲回歸持續使用實際素材，不能僅靠延後載入隱藏缺件。

## zip 內容白名單草案

以下為目標結構，確切 dependency 檔名由 release publish 結果與 manifest 鎖定。所有建置及封裝中間產物只進 `engine/build/`、對應專案 `bin/obj/`。採 explicit allowlist 收集，禁止遞迴複製整個 engine 或跟隨 junction。

| zip 路徑 | 允許內容 |
|---|---|
| `README.md` | 下節操作步驟、支援的輸入版本、失敗訊息與驗收範圍 |
| `啟動引擎.cmd` | 設定本次資源路徑並啟動 launcher 的短入口 |
| `engine/launcher/` | launcher 的 exe、dll、deps.json、runtimeconfig.json 與確實需要的程式相依檔 |
| `engine/build/client-product-dev/` | P11.ClientEngine.exe／dll／deps.json／runtimeconfig.json、P24.Audio.dll、WebView2 Core／WinForms 組件、已審查的轉換程式相依檔 |
| `engine/build/client-product-dev/runtimes/win-x64/native/WebView2Loader.dll` | 應用程式所需的 loader，WebView2 Runtime 由使用者事先安裝 |
| `engine/build/product-dev-manifest.json` | 實作後的程式檔案白名單與 hashes，配合新資源 recipe 契約 |
| `engine/web/` | 僅 index.html、panel.css、panel.js、login.js、interaction.js、bonus-stats.js、private-shop.js、trade.js |
| `docs/` | 必要授權說明與文字版使用／排錯文件，經內容審查選入 |

zip 不列入 assets、icons／ground 圖片、Sound、map、Sprite、Tile、Text、研究快照、PDB／source、vendor、SDK、Python、pip、SQL、JAR、BIN、IDB、測試 capture 或本機 runtime。所有原始與派生遊戲資料均由使用者電腦的準備步驟提供。recipe 的位置可為程式內非內容性 metadata 或程式 manifest 附表，最終打包須檢查內容。

`engine/runtime`、`engine/assets` 與 web 生成圖片〔修訂 2026-11-13：web 圖片不再產生成檔〕在初次運行後才出現，排除於 Git 及發布 staging。zip 存在與缺失測試須同時檢查檔名、magic、hash 與嵌入資源，改副檔名不構成合格。

## README 步驟草稿

此段描述實作完成後的介面，本輪尚無可供執行的新 launcher。

1. 準備 Windows x64、.NET 10 Desktop Runtime x64、WebView2 Runtime，以及自己的受支援原客戶端目錄。將引擎 zip 解壓到有寫入權限的目錄，例如 `D:\Games\TWEngine`。
2. 用文字編輯器打開 `啟動引擎.cmd`，將唯一的資源目錄設定改為自己的原客戶端路徑，包含引號。例如 `set "TW_CLIENT_ROOT=D:\Games\MyOriginalClient"`。入口另以自身位置設定 `TW_ENGINE_ROOT`。
3. 執行 `啟動引擎.cmd`。首次會檢查原檔版本並產生本機資源，視窗顯示進度與所需空間。原客戶端保持唯讀。結束後正常產品登入畫面出現。
4. 若顯示版本不符、必要檔缺失或準備失敗，依訊息核對所指的原客戶端目錄。修正後重跑入口。錯誤時不進入遊戲，也不以開發機路徑補檔。
5. `啟動引擎.cmd --check-only` 只檢查已準備完成的程式與資源，輸出版本與資源收據，保持 `uiStarted=false`。乾淨解壓尚未準備時回報明確的未準備狀態。
6. 登入畫面之後的連線需要另有相容且獲准的伺服器。此發行版不附伺服器或帳密，登入畫面驗收不需要建立網路連線。

實際必要磁碟空間與耗時由轉換實測後填入 README。目前僅知完整舊素材約 2.22 GB，原子替換還需 staging 空間，不能直接將此數當新版本需求。

## 工作量、任務拆分與交接關卡

U6 原估 +1 張，現已證明包含多種 decoder、研究工具移植、伺服器資料依賴、文字版本差異及 repo 已有資源清理。完整 C 估計 20–36 小時，超出三張各 1–2 小時可誠實承諾的範圍。估算拆為來源查核 2–4h、decoder／生成器 8–14h、去伺服器依賴 4–8h、launcher／web／打包 4–6h、驗證 2–4h。查核失敗的研究時間另計。

依本次 plan JSON 上限，只派 **首批三張有獨立驗收條件的任務**，每張 2h。這三張不涵蓋完整轉換器實作或 GitHub Release，不能結清 T033。Claude 須先審查超出 U6 +1 的原因、去重既有 T006／T033，再按關卡結果提出剩餘 2h 任務並依 PLAN 規則處理主線分母。本次不修改 PLAN／TASKS 或凍結驗收。

| id | stage／估算 | 交付與前置 |
|---|---|---|
| `portable-source-closure` | test／2h | 唯讀檢驗 Text 版本差異、terrain／bounds／skills／cooldown 的 client-only 對應，並補未知生成來源。交付逐項 pass／blocked 清單與 SHA。無依賴，允許以具體缺欄作受阻結論。 |
| `portable-resource-audit` | test／2h | 全追蹤檔與 embedded resources 的內容分類、發行白名單與拒收檢查規格，追溯 22 web PNG。無依賴，不刪歷史、不發布。 |
| `portable-preparer-core` | code／2h | 前兩項審查後新增 C# 準備器核心與一個已閉合的 TBT→PNG 垂直切片，驗輸入、staging、原子發布及重試。結果標 partial，完整分類未通過前禁止接正常啟動／標 ready。 |

若來源查核對地形或時序得到 blocked，核心試作仍只可處理已閉合的圖片切片，完整遊戲接線持續受阻。這讓後續有可測試程式，同時保留未解問題的可見性。

### 完整實作後必須補驗

- 在無研究目錄、無 releases junction、無 SDK／Python／SQL／JAR 的 Windows 上，由 zip 與自己的客戶端完成準備與正常登入畫面。原生步驟交既有授權的 dot／使用者執行，codex-local 負責離線前置與問題修正。
- 兩個含不同路徑／空格／中文字的解壓目錄，生成結果在相同 recipe／輸入下可重現。路徑性 provenance 與內容 hash 分開。T006 沿用 #91 的獨立交付與驗收紀錄，不重複開單。
- 故障覆蓋缺檔、錯版本、截斷、索引越界、同名衝突、取消、磁碟不足、中斷恢復、重入、既有 junction 與路徑穿越。檢查原客戶端前後 hash，只有允許的本機輸出有變更。
- 素材內容比較與遊戲回歸須涵蓋角色、場景、跨圖、NPC、背包、陰影、施法、地面物品與 6016。明列舊完整 SHA、原像素／欄位等價及原生驗收三種證據。
- 封裝白名單、repo 內容分類、zip 實際清單及 embedded resource 審查全過後，才交原 T033 實作任務建立 GitHub Release。

## 證據附錄

下列附錄僅保存路徑、數量、大小與 SHA-256。研究工具沒有被複製或執行。外部伺服器的身份只引用已存的非機密研究紀錄，本輪未讀 SQL／JAR、未連 DB 或任何服務。

### 附錄 A 索引代表檔與本輪核對

每類選取一個索引代表檔重新計算 SHA，19／19 與索引相同。其餘素材只核對分類檔數，沒有宣稱全部本文通過 SHA。

| 類別 | 索引／實體數 | bytes | 代表路徑 | SHA-256 |
|---|---:|---:|---|---|
| `cache` | 378／378 | 37687310 | `cache/half-channel.bin` | `52f925e71edb30a18d4321ee28baba7e537862278822d9bde166dcdfeac5e5cb` |
| `character` | 103／103 | 7028561 | `character/animation.json` | `5924258d20c26696951e66a6f4631a3b0e2811603a8991bce7dd75d026620541` |
| `dialogs` | 2／2 | 9183942 | `dialogs/skills.json` | `52592c24503ad57f5bc01c53de22c9d38abdc7e069f85e6c046c336d6ba5ef8a` |
| `doors` | 2／2 | 424972 | `doors/2346.json` | `8427f26f1826d84d2001115b9de71de6de10c8a489091ac1771ed42743738594` |
| `encountered-monsters.json` | 1／1 | 8433 | `encountered-monsters.json` | `5d95cc99743f432b89dc3ac19bf37d3d26018083b194d9dbdfc64b80a501594e` |
| `ground` | 4／4 | 14410 | `ground/36.json` | `b87a2162c90194d469eb9469e66bb6594e9bbec6d9d46dc9006ebc3099005837` |
| `inventory` | 20／20 | 17018 | `inventory/124.png` | `7a989ea2e4f676a42d82e612ca9ff6a1d25e27005acf0cc312ffd270cef62919` |
| `magic` | 1／1 | 1626 | `magic/catalog.json` | `024576e3c96bedc33c63b889d77366f4204b99ceb40c78f3c743ff6e54a44ebf` |
| `maps` | 13304／13304 | 1594410357 | `maps/effects/1036.json` | `bbd039bc8d247021bb4668feeebd5411524200cfbaf709bf6288d3472eb9f137` |
| `motion.json` | 1／1 | 2250 | `motion.json` | `050410a887a1a825a82ca5bd596a7218216fceab25ac6d0c53d01a7d570c7c0b` |
| `objects` | 54／54 | 253124370 | `objects/0-weapon-20.json` | `6b9aaddeaf4b913ff0aa44237349e314ca57a69d99620f9408553d006f35183a` |
| `players` | 39／39 | 192141888 | `players/1-0.json` | `2f4f0b0f80b38634a0063b4232c89458f6f7cf77aff1b48274c94748af438f15` |
| `scene` | 13／13 | 65145865 | `scene/atlas.png` | `c42ad755b2c1577940e682e94c27b8f5412b9d521325ce66eac7bea006c75caf` |
| `shops` | 12／12 | 29666183 | `shops/0.json` | `a789bc1a62794fa8f9301eb549b9e88638181eb91aaf503019a96467a2afcba6` |
| `terrain0.bin` | 1／1 | 8192 | `terrain0.bin` | `b78a673314e254ea23914c6edc585c34bdd102edee7174b7568e13012c182848` |
| `world-appearances.json` | 1／1 | 2679 | `world-appearances.json` | `18c29638f97b0f80bb04928c04aac42679dd96cb998929e8fc8b3c238f42bd6f` |
| `shadows` | 18／18 | 33812613 | `shadows/0.json` | `5e4c902992ffa00a1f7df529e767f5c89dfe1646b468945e4cbf279ca4013dd1` |
| `audio-manifest.json` | 1／1 | 3346 | `audio-manifest.json` | `87c6cd6867e0d81bdc627c1cac0a922891d650018211b58525de9226926ad9fe` |
| `sound` | 2／2 | 39070 | `sound/1340.wav` | `30b4ce04d5ca914cdbfd80c7ee9b9cdbe3bf79487e609b2a9d32f6546a90f9c3` |

### 附錄 B 本輪原客戶端實測指紋

所有路徑相對於 `D:\lineage開發\0-客戶端`。只讀並計算雜湊，沒有解碼、複製或修改容器。

| 原檔 | SHA-256 |
|---|---|
| `Sprite.idx` | `327c06372d87f6cf6d5176da51da5ef1f94dbf388d1588b735c5965b96b6a52b` |
| `Sprite.pak` | `4dd4e5348fbad2e69bae9cc388be23d7becebe8423ad9f79f393c1457a69ee62` |
| `Text.idx` | `43a342ad3929b541096630165c04794192c48359ecbf116b538eaffc49741f6f` |
| `Text.pak` | `9516a372c43b269cf1af7adc0ac00abd4163e1235dcd846e34294849616a37ff` |
| `Tile.idx` | `9291fd800ac8cae6dc355c1d8bbb965880561de169fbc7ea22d174fff9ad24db` |
| `Tile.pak` | `82e52f47317f9a55ef775bbfce3c5f26f42b31a98748ffaa84ce5e2c3ed60d4a` |
| `map/0/7ffd8002.s32` | `a7e3e584202ddb9221fe6cd5042190486651f4a71db7e056b5e161bf31b30239` |
| `map/0/7ffd8003.s32` | `23ad0e1ef9001c6e331a3425e5a1faa410d3cf4a8e5d9425f38daa85c22e7e3a` |
| `Sound/1340.wav` | `30b4ce04d5ca914cdbfd80c7ee9b9cdbe3bf79487e609b2a9d32f6546a90f9c3` |
| `Sound/3332.wav` | `973e0a3598edd23c2a24f6900643e43a9155fe36906b939d2d8e24374abeff2a` |

### 附錄 C 歷史輸入指紋與來源紀錄

以下從已存的研究 JSON 讀取，不表示本輪重新驗證了全部容器。主 Sprite 的本輪實測相符，Text 的本輪實測不同。Sprite 替代包與 Text 各 entry 須在後續查核。

| 歷史原檔 | SHA-256 | 證據 |
|---|---|---|
| `Sprite.idx` | `327c06372d87f6cf6d5176da51da5ef1f94dbf388d1588b735c5965b96b6a52b` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite.pak` | `4dd4e5348fbad2e69bae9cc388be23d7becebe8423ad9f79f393c1457a69ee62` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Text.idx` | `b85faf2685cab65357800368228ed3a5a96108e512cf883fa63ad2f74a44a91b` | `P04/evidence/scout.json` |
| `Text.pak` | `19bfe9d44c55f697d55ca23292509aea56dbbc1f534427afc740522081b7448c` | `P04/evidence/scout.json` |
| `Sprite13.idx` | `803960def65a3b1713515c9350c9c60a90cb03888dd18dfa8f61c2272d6f9251` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite13.pak` | `d75f4e194be9a132b34957f15095120d593af7df1d9541ac72c1f0f2a5d0b53d` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite03.idx` | `dcb4da9f78507f580f753a420ec7b666131b1f3806f7ca6bc5928c8ba23f151e` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite03.pak` | `9160c0b91dfa874ba8cc769dcc2adc623ee9e250d7e41a32b158becc82fc5189` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite04.idx` | `8c8e7d220ddbde1f2feec6800a16afb5900d3b90014c11450ddddec8eca0c667` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite04.pak` | `a6f5cf1f65ba801c5bfca9d8975ba8812f2af476e9f2770abc15daacdb41ab1e` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite02.idx` | `bcae6bc833e2a761554d660848496342afe6b512716984710c52ceaf5ce0c7ce` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite02.pak` | `7246a9383951817f47665f9bd1bd7104f6011231a938f0ca454fce07c6339fb3` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite11.idx` | `c470f356d5c0b09166962f5b302317c667973b9d206cde73bcc5598de0463aeb` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite11.pak` | `14908465e793157c3a079742c612722374f061c9ccf7df09fa22a7201296d9fd` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite12.idx` | `f529da765cf96b6354fe5a8cb927d3a1610fac265865c6b7245e97eeee6d96a0` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite12.pak` | `067cf305691f0a37c2ed6d7c2177deecafb6ee86d1c88a9de11c7adb7d35d06c` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite14.idx` | `d5d6a491280ce37a30790aa113768a4e0adafe6dd0533fbb3536031fb048187f` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite14.pak` | `1a82d38efa5b889274e287d73cf09a84472eb66b1fdd197fd0cb37898d516b28` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite15.idx` | `e4ce732730c62c820b6cabfcd818193cead90723823527996bc4f77e021725d0` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite15.pak` | `cf6dd522df5b4fa2014347eb6a35b052dd6780d19ec4f2f9fbffb869997d396b` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite00.idx` | `ab8dcf18c2a63dde97abbfb23c7e9e4f35efd0f1e2ab5acf9f53cded6f5f9817` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite00.pak` | `b9a2ce16accc08c5254b77d76129ee58aec13c66bc01f7953180acbf0039af56` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite01.idx` | `465274c542a7373af97b1b4fc7c906827293fa000d9f0ea09546bb527e693d54` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite01.pak` | `80e274887954a2f27e1eeef203beda862d20091120bb7fc30757893c032710a6` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite05.idx` | `ddcbdf73a075eb73dc87a01267f196002d2cc319ae00264961ae6efb02698ceb` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite05.pak` | `00747e5d858ce0ec1e77a5b1e8515788d2f9f21996b1fb77e765f4df3742d2a4` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite06.idx` | `f102d2b343d1e471714853d1c1391a4bd610a3fa1b066340cdb80c882c31c1e2` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite06.pak` | `545e393563f825ace51de890209387b201bc6427ecb3572d44e4651520f8848e` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite07.idx` | `79f844a872761014cfc74109fbced128f29232941ccf23857d2ac01793a23ab6` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite07.pak` | `c51e126f22997dad05dc95b3e330a21e8afdcc6821ddd1179932b533ff045a50` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite08.idx` | `e5867da3438190a77b283ec45a427819bee1526d1071046ffd6bb8e375ebf961` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite08.pak` | `380243fe13193d6ee758c753b98c795704b04e7bb0325c27ed0db231167bbd68` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite09.idx` | `0c446e0a839911b02eb4adeccce3bcdb2bceb39a0e39f9a40ec5914803ce73db` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite09.pak` | `863e0b00b6729898ea5d9e27cf8b034bf16c887877d607792d6f5ff6a0f35acd` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite10.idx` | `90e10dcd775a475fccb5c989be842a3e38dede810333649ebccbaaadeca55c57` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |
| `Sprite10.pak` | `fc39a62ad8884fcfeb9f47c88ce56ba75399c4e6256d5c502b9313ba2b77eb91` | `P04 scout／DEV/player-appearance-r1/source-records-r3.json` |

外部 SQL 身份為 `D:/P24Lab-ClientEngine/sql/380_db.sql`，SHA `0875f7cff78d7d97a7380cc5d24f47b2c66b630d452377e5f8bfa6f3d29fd95e`，引自 skills-source.json。JAR 身份為 `D:/P24Lab-ClientEngine/server/Server_Game.jar`，SHA `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`，引自 player source-records。兩者本輪未開啟。

map0 terrain 原始身份由現行 maps/manifest.json 記錄為 `D:\P24Lab-ClientEngine\server\maps\0.txt`，SHA `ece432f3af3cc20bcbf2722dea31a379a9e6ee2b05d7db8940f35ca52ec6bc44`。其他 712 份 terrain 來源由同 manifest 的 maps[].terrain.sourcePath/sourceSha256 指定，本輪未讀伺服器目錄。客戶端推導方法尚未證實。

歷史 BIN 身份由 P06/scripts/derive.py 鎖定為 `D:/lineage開發/登入器bin資源/TW1608262200.bin拆解/TW1608262200.bin`，SHA `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`。本輪未開啟 BIN。混色生成的 VA 區間見 P24/cache_pixel_modes.py，screen 為 `0x0097B766..0x0097B823`，multiply／half 為 `0x0097B9B3..0x0097BA67`。

desc-c.tbl 的研究記錄位於 P24/reference/object-assets/names-source.json，記錄號 4201、Text.pak File Offset 1649737、長度 249300，packed SHA `0570773730a7df2cabdf7f3d283d706c77f8b48159e1dc40b845911847371343`，decoded SHA `f06c94f47271fcb01f1d5af63725dfa9a230b5f6be3c0dd1d53cd37628adb7df`。目前 Text 容器身份不同，此 offset 只描述歷史輸入。

### 附錄 D 本輪讀取工具與證據檔 SHA-256

路徑以專案根為基準。表內列出工具及各類的來源紀錄，完整路徑可供本機下一棒定位。這些指紋識別本次盤點版本，沒有表示腳本可直接安全重跑。

| 路徑 | SHA-256 |
|---|---|
| `P04-角色動畫/scripts/extract_sources.py` | `60492c1339ee909221f3971096cfa8bf1c19f227fc4f65f67b408d8355b819a3` |
| `P04-角色動畫/build.py` | `62e3197dc2a19f594a8e8f382e464216cd6c2b2ba9100cf252b5f4b0286dc239` |
| `P04-角色動畫/sprite.py` | `c6319da13db52c7c2f804fa4855bcce71f6eeb7ecb1d3bcf4b471a31c72b3058` |
| `P04-角色動畫/reader.py` | `ce0f64f267e528c2f3aacdc8c7a8472fb97b23e8ac4053507af1aa744d15cb2a` |
| `P04-角色動畫/evidence/scout.json` | `73fd9008e94bc8f146ac059c169ceba711a414116f834244a99aa6076a33ea92` |
| `P05-單地圖/build.py` | `47dd96f1734e0088e300b809b5c26881193649f91e9644a66757f55c52972cb1` |
| `P05-單地圖/archive.py` | `1ba7e83773b9abed49208b81e29fba0589c4fc8242e36c083f471e91d844242f` |
| `P05-單地圖/mapdecode.py` | `7db9f2426cdd6b7e6a4b3f0ff063dd7f4edc67bd22d01244645143552ab20e94` |
| `P06-離線客戶端/scripts/prepare.py` | `ceea427763b088b31a264a0219d1ca992a57454b9bdbbd79045510fbfdae7039` |
| `P06-離線客戶端/scripts/derive.py` | `d69d01f68ec88825daae6d1bf9d8a38b6e6ae438b680f0a27b8b578631ec7aaa` |
| `P22-魔法清單與單次施法/dual_block_assets.py` | `bc8017c7767191b067e3e9f6f477cbb59f22ef9031ac76600c660c909fa6c32d` |
| `P24-原2x2地圖快取與跨區載入/dual_block_assets.py` | `6104cba8c268009e8d6488f56f7006d2253a20f57bdbc3f782d0ef982fd8d685` |
| `P24-原2x2地圖快取與跨區載入/cache_assets.py` | `181c611697a4fc71688368afeab735c1f094329ca85a9dbe7ae2b0a2acf16f06` |
| `P24-原2x2地圖快取與跨區載入/cache_pixel_modes.py` | `8b8f69f0404c37e639f9b4cea4506b4ef9eca3d1c112e21ed1848440f03d740d` |
| `P24-原2x2地圖快取與跨區載入/opacity_sources.py` | `78397aded7c610051bdb4bbf7705432609d0841fce9d5829a804205ff54633db` |
| `P24-原2x2地圖快取與跨區載入/assets_objects.py` | `68bafa3504f718a556ce23294a99fbddb6c691ab0be28dbfca2d249b0d25432c` |
| `P24-原2x2地圖快取與跨區載入/combat_assets.py` | `54ca0f28abf225d6d79b296b945aa2609b2e6466affcffb1baa80ae792ad02ae` |
| `P24-原2x2地圖快取與跨區載入/equipment_assets.py` | `59a2d6c4b4dc46fb6266c300006cea6aa98156b337aa6519448a0b1157a199c8` |
| `P24-原2x2地圖快取與跨區載入/arrow_assets.py` | `123eb0db69b2e878eb0be600e8376d5a2ca98c87baff6f3ef3a168592edfa4af` |
| `P24-原2x2地圖快取與跨區載入/magic_assets.py` | `905ab97cab3a9070846107250e328484451301944ebf6e0b879d29999ef52ab4` |
| `P24-原2x2地圖快取與跨區載入/ground_assets.py` | `3448d21b59ffd2a3d5b83728cb352ccd05d199231996703c74ff887bd1e7365b` |
| `P24-原2x2地圖快取與跨區載入/arrow_ground_asset.py` | `f96682c62caf7ad77ad3fcb98004770cfb8e9a7c24416572efec4eac4cb2a4ce` |
| `P24-原2x2地圖快取與跨區載入/inventory_assets.py` | `3b8fad12ceb0d7f7d006c621d981bf185ba3b9677a9002fac4f324170abc77f7` |
| `P24-原2x2地圖快取與跨區載入/ranged_icons.py` | `fd64b5d344976debedb5c73e14e38e45f5c55c9e68033b85b72f5d2fe327000b` |
| `P24-原2x2地圖快取與跨區載入/reference/object-assets/names-source.json` | `26f2f1552375f0c7fedf40c97bf32bd6c6c48cbb7ac9e1f649dcad6868158f9d` |
| `P24-原2x2地圖快取與跨區載入/reference/object-assets/decoded-list.spr` | `523c61ad5cc362a56a36f58b262c84efa8b4435f017e7074d35b5834e8b9f127` |
| `P24-原2x2地圖快取與跨區載入/reference/object-assets/inner-list.spz` | `e8cec0388d6e9fdf518493fba1674953141c023765e02c210c05a23dfbbe6242` |
| `P24-原2x2地圖快取與跨區載入/reference/object-assets/text-reference/Program.cs` | `298dccb2a3b6a474016e4727a8baf3cb944b8b197c47b143203270be971eafb0` |
| `P24-原2x2地圖快取與跨區載入/reference/object-assets/text-reference/source/Pak/PakTools.cs` | `b4bd734b42fafb751000a7e944790bdd794dfe6f07cdbc3e9a4c9dc7f731874a` |
| `P24-原2x2地圖快取與跨區載入/reference/development/npc-commerce-r1/decoder/Program.cs` | `7deb11a416200b86fdcd348b6223b728cd1d3de86b6ed50e09a08b660b3cfa36` |
| `P24-原2x2地圖快取與跨區載入/reference/development/npc-commerce-r1/decoder/Decoder.csproj` | `a808da1c257eeffe7ef3c62d5db2b471e261dd9aeb2f078a22de80504a685143` |
| `P24-原2x2地圖快取與跨區載入/reference/development/npc-commerce-r1/text-provenance.json` | `d33b64979b919771358a56af2c9c88b3349cd7ba6b9f3cc7b3a234b75b49ed67` |
| `P24-原2x2地圖快取與跨區載入/reference/development/npc-commerce-r1/build_npc_assets.py` | `d9cdd17fa15c12866adf3469acdc0ae99c1317457f623da53a1aacd7129ee17d` |
| `P24-原2x2地圖快取與跨區載入/reference/development/skill-warehouse-r1/skills-source.json` | `6960ed0cbeed8741136c387b9bb34c02a7d9a6762bc54992d9421b56644a54ed` |
| `P24-原2x2地圖快取與跨區載入/reference/development/inn-door-r1/prepare.py` | `992bead7067d7b840abe3335580a2eb0cc731109dedd9059ffe991c4e96328b5` |
| `P24-原2x2地圖快取與跨區載入/reference/development/maps-r1/build_maps.py` | `dd197f1eb175b40d632c21255e3da90b4ff31ca6e3dae553e0372f4f5e34f4c2` |
| `P24-原2x2地圖快取與跨區載入/reference/development/map-effects-r1/build_effects.py` | `10de3742136bad5ef49e157da83ee1533090986265edbd986b8b0b775a4ed5a4` |
| `P24-原2x2地圖快取與跨區載入/reference/development/ship-route-implementation-r1/package_seg.py` | `fd59c7c3cb164d11bd45a5c0ff4c30e51dc31b6e1070d04d4ec7939ea32897ae` |
| `P24-原2x2地圖快取與跨區載入/reference/development/ship-route-implementation-r1/package_lac.py` | `2a4289519ecc2c9fa0a7a6e0b8a2c682919dc873aa637f7994d8f47e9c66f600` |
| `P24-原2x2地圖快取與跨區載入/reference/development/player-appearance-r1/build_profiles_r3.py` | `27624625de92a523d95702a849b9107ea4cbd90c8d548218726a8fbd31a32de3` |
| `P24-原2x2地圖快取與跨區載入/reference/development/player-appearance-r1/player_sprite_r2.py` | `dc475754e578844493aa34fc2f21a80827dee3c4ce5af852e79c6b5c78895986` |
| `P24-原2x2地圖快取與跨區載入/reference/development/player-appearance-r1/source-records-r3.json` | `eb5d00f2bf76bc8edcc5d34a71400298c99154612ed0e989163d619d954a01ba` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-dagger-appearance-r1/prepare.py` | `f0c38afffd00f230feb607684a802073d7eea36e0067c42d7792acf87c353143` |
| `P24-原2x2地圖快取與跨區載入/reference/development/shop-appearance-fix-r1/build_assets.py` | `0376e6a675a2df188632684af70a5e45b75b6f7a18a3459c5d1fb3076dc5555e` |
| `P24-原2x2地圖快取與跨區載入/reference/development/shop-appearance-fix-r1/build_monsters.py` | `93c9553b9ecc15600fbe37c3a2f54457df4f61886b1aed2af4a164b6be3bdbcb` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-monster-1108-r1/build_assets.py` | `dc74f53b304b182615519c08a2c32688498f97d0541fa8305f72ddcb8ccfa0ab` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-map01-r1/build_assets.py` | `f957c35d95e3fac00780b82f065e733140b2ade0c6edb9caccf82033ccbf6c37` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-map01-r1/preserve_existing_cow.py` | `f68eb2846031ff1d1d10e84d2c9a329affed9d503e0936f98a7aa1c3cdb00527` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-rabbit-1002-r1/prepare.py` | `4af783ff723b79eaed7fe49936e0829cf15b2ee379928aa687d460ac2814fb62` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-gludin-merchant-r1/prepare.py` | `9d18f8855ed4fee045dbbc938d3bc14697eca190376b212833232b1c3cb8bda2` |
| `P24-原2x2地圖快取與跨區載入/reference/development/skill-casting-r1/prepare_sources.py` | `7ef3ecc0a209d5b39100bb2f9174674460e7a6994bef439cdc19ddd79cbdbce8` |
| `P24-原2x2地圖快取與跨區載入/reference/development/basic-potion-haste-r1/prepare_visuals.py` | `161cfab265fe1f92b4c4ff7fb75b8fedc30e04cef15e8d16c8d131ccf890196f` |
| `P24-原2x2地圖快取與跨區載入/reference/development/inn-dialogue-r1/prepare_key_icon.py` | `1a0104ccf0bedc7064c42b7fbbc39448802557892b3702d7ecd3711c4253213f` |
| `P24-原2x2地圖快取與跨區載入/reference/development/ship-ticket-icon-r1/prepare.py` | `5e23b7e362fceb8aad92746cb8c2b6e193c67898abefd6185c0baa64232d9400` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-ruby-icon-r1/prepare.py` | `13f1566287b94b588e1163a3c1dc7731136d0d7decef00365bb6d3a71add0f53` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-sapphire-icon-r1/prepare.py` | `92c2c40fcb209f08f67a10cc8e4bf715e34a6e95a400c1a2e2e13422e42d8541` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-online-gift-icon-r1/prepare.py` | `20b5263b9943dc176ec764793eee767c4fd31fa6b3a09d62be084a53228f3af1` |
| `P24-原2x2地圖快取與跨區載入/reference/development/player-shadow-implementation-r1/prepare.py` | `d3874f8200e5b162a8947ccabd098fd68498eeac175510a0fd7351cc34d92621` |
| `P24-原2x2地圖快取與跨區載入/reference/development/player-shadow-coverage-r2/prepare.py` | `7b8b124b1f4dff5960ed03104fb16ccea4b7578247d739e09ddebc3b5d319f5a` |
| `P24-原2x2地圖快取與跨區載入/reference/development/r27-6016-audio-module-r1/HANDOFF.md` | `1812a8e356ac8f49d70e58b814deee252fff77d656c4d143b42cbb5328245b68` |
| `P24-原2x2地圖快取與跨區載入/reference/development/r27-6016-audio-module-r1/verify-assets.py` | `83f735dc7350539371e89e0815644a20e61283726a67086e3059c8385c55a203` |
| `P24-原2x2地圖快取與跨區載入/reference/development/r28-6016-host-adapter-r1/import_r3.py` | `de4580d5b230dc504d833466e03386f244e98d9576d26ff6985e5bf561d8bb6d` |
| `主管/P24-完整地圖來源清冊-20261026/map-metadata.json` | `1282d74c2977635106f32bdbcacec48f309569b1a77be2d4a203d5086738b8f1` |
| `主管/P24-完整地圖來源清冊-20261026/blocks.jsonl` | `079c9f43de434744d58193d03a50e1a333a54695f91c5211d5262542a14aec70` |
| `主管/P24-完整地圖來源清冊-20261026/parser-limit-classification.json` | `fc365448ec5310a6af070e4f1db014ddcffb06b58395f010e0ca944f3dbee157` |
| `主管/P24-完整地圖來源清冊-20261026/effect-source-candidates-all-archives.json` | `fd9920be2b4e33f93b935a4cd78fc88de660220de1ea1724defee9addaed3337` |
| `主管/P24-外觀來源版本選擇-20261026.md` | `5ca8b2ece319d3c7f0c196b6930729f1560e449b5473301ec355a3185d674b06` |
| `work/engine-next-integration-r1/HANDOFF-r29.md` | `9fc67df31fbc96456b71ea8eeba81371519514942f00571b871c14aa6f557510` |
| `P24-原2x2地圖快取與跨區載入/reference/development/skill-casting-r1/apply_assets_and_ui.py` | `f80c8db6cb73305651a324c6228bb477e135205b56f028a0d22d22bf1d6fe34a` |
