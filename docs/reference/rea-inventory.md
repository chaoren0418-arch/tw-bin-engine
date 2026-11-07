# 本機逆向資源盤點與二度分析（Issue #417）

盤點日期：2026-11-07（Asia/Taipei）。來源全部唯讀；沒有啟動遊戲、伺服器或 DB。可重跑腳本與小型結果在 [`work/issue-417/`](../../work/issue-417/)。

## 結論

1. **B07 的受阻理由要改寫。**「Themida 執行期初始化，靜態不可得」只適用 `LiTo.bin`。歷史地址其實屬於**未加殼**的 `TW1608262200.bin`，#47／#48 列的缺件大多能靜態讀到，本輪已補上 parser、frame 結構、音效選擇、分類讀取者與 dispatch tables；S87 同身份入口仍未定位。
2. **B04 解開了主要部分。** 地圖事件 token 與角色動作用同一個 parser，`<`、`[`、`^` 的語義已從機器碼取得，並經三個版本交叉確認。`^` 是地圖事件的大宗，產品尚未實作。
3. **B03 建議結案。** 原生鏈完整存在，但全量掃描本機 7,807 個 `.s32`，26,116 筆地圖特效**延伸記錄 0 筆**。這個功能在目前客戶端資料上不會被觸發。
4. **產品有一項已確認的差異：** `CombatSfx.cs` 只取 `[`，略過 `<`；但 `<` 是聲音，連 `#0` 的腳步聲都是 `<`。

### 與任務指示的偏差：沒有用 rea-main

任務要求用 `rea-main` 拆解，本輪**實際上跑不起來**，改用 pefile／Capstone 與 P01 既有的 IDA 資料庫（Hex-Rays）。

| 阻礙 | 實測 |
|---|---|
| 未建置 | `node scripts/rea.mjs --version` 回 `REA's compiled runtime is missing`；工具目錄沒有 `node_modules`、`dist`。建置要在工具目錄跑 `npm ci`，違反「不改動工具目錄」 |
| Node 版本 | 本機 v24.20.0，`package.json` 要求 `^24.22.1 \|\| ^22.23.4 \|\| >=26.12.1`，且 `.npmrc` 設 `engine-strict=true` |
| 沒有 native provider | 32-bit PE 需要 Ghidra、IDA、Hopper 或 Kuna。查過 PATH 與常見安裝位置（非全磁碟）沒有 Ghidra／Hopper／Kuna；REA 的 Windows Ghidra 路徑要 Ghidra ≥12.1 與 JDK 21（本機 `JAVA_HOME` 是 JDK 17）。IDA 7.7 存在，但 REA 要透過 ida-pro-mcp，文件未列 7.7 為已驗證版本 |

要真正用上 REA，需要使用者決定：升級 Node、安裝 Ghidra 12.x＋JDK 21，並同意在工具目錄或其副本建置。**本輪結論不依賴 REA，少了它不影響下面的地址與語義。**

## 證據基準

| 樣本 | SHA-256 | 狀態 | 用途 |
|---|---|---|---|
| `TW1608262200.bin` | `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2` | 未加殼，時間戳 2016-08-26 | 主要分析對象，與 P01 鎖定同檔 |
| `TW13081901.unpacked.exe` | `653da514f750d4714fde2c83e5162aeef553c876ec541fe1cfebf6970f5c62a4` | 已重建，時間戳 2013-08-19 | 交叉驗證；packed 原檔在 `0-客戶端` |
| `LiTo.unpacked-oep-current.exe` | `4271d92fb2b9a2fa8d118dde0767f6d8a67a896227bcc24127438fcb5c2ab6e9` | LiTo 的 OEP 快照 | 交叉驗證（僅 parser 窗口） |
| `LiTo.bin` | `ce6ee68ada8fd37e6c096b1a27e46cb6888f1179f85defb5007ac60287360292` | Themida，時間戳 2007-09-04 | 不能靜態分析 |

反編譯使用 P01 的 `ida/03-ascii-isolated/TW1608262200.idb`（SHA `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f`）在 `scratch/` 的副本，IDA 7.7＋Hex-Rays，104,289 個函式。地址預設為 TW1608262200 的 VA，ImageBase `0x400000`。`.text` 的 File Offset = VA − `0x400C00`，已用 #47／#48 的三筆歷史值核對相符。偽碼函式名是 IDA 自動命名，型別為推導結果，不等於原始碼。

## B07：分類全鏈與音效選擇語義

### 已驗證：`list.spr` parser（`sub_87C890`，File Offset `0x47BC90`，字串標籤 `loadSprites`）

**#48 的「102 parser 寫入」與「分類指標初始化」就在這裡。** `0x13CD838`、`0x13CD878` 落在 `.data` 的未初始化區，所以磁碟上沒有內容；但它們是這個函式用一般程式碼配置並填入的，與 Themida 無關。陣列長度＝sprite 總數，以 gfx 編號索引。

| 欄位 | 寫入位置 | 預設 | 備註 |
|---|---|---|---|
| 100 | `0x13CD83C` | 0 | 計數＋每筆 4 個 dword |
| 101 | `0x13CD868` | -1 | `A3EF40` 以它載入另一張圖，推測是陰影 |
| **102 type** | **`0x13CD838`** byte | **1** | 分類 |
| 103 | `0x13CD86C` byte | 1 | |
| 104 | `0x13CD84C` dword | 0 | |
| 105 | `0x13CD87C` | 0 | 計數＋word 陣列 |
| 106 | `sub_8787B0` | — | 5 個 word |
| 107 | `0x13CD854` byte | 0 | `a \| b<<4` |
| 108 | `0x13CD850` byte | 0 | 地圖 actor 建立時會讀 |
| 109 | `0x13CD85C` byte＋`0x13CD864` word | 0／-1 | 對應 #47 的 `109.effect(4 219)` |
| 110 | 區域變數 | 24 | 影格率，用於換算毫秒 |
| 111～114 | `0x13CD858`、`0x13CD860`、`0x13CD870`、`0x13CD874` | 0／-1／0／0 | |
| 115～117 | 讀後丟棄 | — | |
| **118** | **`0x13CD878`** byte | 0 | 值 1 時有動作分支 |
| 119 | 讀後丟棄 | — | |

動作以外的 `=` 表示 alias：複製來源 gfx 的 36-byte frame，並把來源 gfx 記到 `+16`。

### 已驗證：36-byte frame 結構

| 偏移 | 內容 |
|---|---|
| `+0` | 影格鍵 `^ 0x1951` |
| `+2..+10` | 候選，最多 5 個 word，**第 6 個起讀掉不存** |
| `+12` | 候選數 |
| `+14` | 毫秒 `1000*ticks/影格率`，再 `^ 0x1812` |
| `+16` | alias 來源 gfx，預設 -1 |
| `+18` | `]`、`{`、`}` 的值，預設 -1 |
| `+20` | **模式：`<`=0、`[`=1、`^`=2** |
| `+21` | 第二個數字 `^ 0x2B` |
| `+22` | **旗標：`!`=1、`>`=2、`]`=4、`{`=8、`}`=0x10** |
| `+32` | ticks |

符號解析是互斥的 `switch`，所以一格只會有一種 `<`／`[`／`^`。

### 已驗證：音效選擇（`sub_A3EF40`，`A3FB..` 段）

1. 需要參數 `a2` 非零，且自身 `0x153CE60` 存在。
2. 候選數 0 或 >5 略過（parser 已限 5）；1 取第 0 項；2～5 用 `rand() % n`。
3. 選中的值為 0 就略過，**不重抽**。
4. 模式 2 走 `A06210(id, 距離²)`，**其餘（`<` 與 `[`）都走 `A060F0(id)`**。全 `.text` 掃描 `+0x14` 的讀取只有 `A3FBB8`、`A3FC2E`，都在這裡，`<` 和 `[` 在此路徑沒有差別。
5. `A06020` 內有同 sample 200ms 冷卻（`now+200`），並呼叫 `AIL_quick_play(handle, 次數)`。`A060F0` 傳 1，`A06210` 傳 0。

`A06210` 維護每個 id 一個 52-byte 槽，最多 5 個音源，依距離排序，滿了只換入較近者（`+48` 是目前最遠距離）。

**推測：** Miles 的 `loop_count=0` 是無限循環，所以 `^` 應是循環環境音；此語義來自 API 慣例，未實測。`127 - 7*round(sqrt(距離²))` 算出的值看起來是音量，但 Hex-Rays 的傳參不完整，尚未確認。

### 已驗證：分類的讀取者

| 函式 | 判斷 |
|---|---|
| `A42C60` | `type == 9`，僅此一句 |
| `A45060` | type ∈ {5, 6, 10, 12} |
| `A400D0` | 同上集合，再加 `A3FF10` |
| `948F70` | type ∈ {6, 11, 12} 時清 `+196` |
| `A44500`、`A467A0`、`A51920` | type 11 的動作分支 |

**點擊流程（`F112F0`）：** `A41D10` 通過 → 與自身距離 1 內 → `type==9` 才進 `5001C0`，否則設 `0x13CE400=3`。`5001C0` 的 default 分支送 `"chhddc"`、opcode 23（x、y、物件 id、`+208`、另一值），節流 986ms（旗標 `0x10000` 時 600ms）。opcode 131（`chhd`）用於類別 7／8，opcode 121 用於 14。

**推測：** 欄位形狀符合拾取，`+208` 應為數量，但未對照伺服器。`A41D10` 與 `5001C0` 用的是 vtable `+108` 的回傳值，未證明等於 102 表。

### 已驗證：dispatch tables（#48 列為缺件）

| 組 | index VA／File Offset | target VA／File Offset | 長度／最大索引 |
|---|---|---|---|
| 第一組 | `0x94828C`／`0x54768C` | `0x9480C0`／`0x5474C0` | 255／114 |
| 第二組 | `0x9483D4`／`0x5477D4` | `0x94838C`／`0x54778C` | 240／17 |
| 第三組 | `0x9484CC`／`0x5478CC` | `0x9484C4`／`0x5478C4` | 235／1 |

第一組只在 `[0x13CE3D8]==3` 時使用。完整 case 對應可用 `docs/reference/rea-scripts/b07_tables.py` 產生。

值 226 落在 `0x9477AB`，呼叫 `0x92CB70`，補上 #48 未能重新證明的 opcode226 對應。

**負面結果，很重要：** 值 8、11、87 都落在 `0x947DF4`，這是越界時也會跳去的預設區塊，共 139 個值共用。所以 **TW16 的接收編號與 3.81 伺服器的 S87 不同**，#48 的警告成立，S87 的同身份入口仍未定位。線索是物件建立函式 `A51920` 的 6 個呼叫點（`A4C043`、`A4E846`、`A50D83`、`A50E23`、`A5104B`、`A51171`）。

### 仍待驗證

- `!`：`81C7C0`、`8ABC10` 在此旗標下處理 `actor+316` 的待處理動作，推測是攻擊或投射物的釋放時機。
- `]`：`A4B060`、`A4BA20` 會 `new` 一個 `0x2A0` 物件，推測是以 `+18` 為 gfx 的子特效。
- `>`、`{`、`}` 本輪沒有找到消費者，但掃描只涵蓋 `imul 0x24` 樣式，不代表不存在。
- 每種動作第一次進幀的 caller 尚未逐一核對。

## B04：地圖動畫事件

**已驗證：**
- `F0E9B0` 用與其他物件相同的 `A4E5D0` 建立地圖 actor，gfx 就是 s32 記錄的 id。
- `TW13081901.txt` 的 `#6016` 是 `0.0:5<3332<1340<1340`，與產品 `Random.Shared.Next(3)`、200ms 同 ID 冷卻一致。這也證實 **`<` 是聲音 token**。

**推測：** 地圖 actor 的每幀更新走同一條 `A43560 → A3EF40`，尚未逐呼叫鏈確認。

全量 token 分佈（521 個地圖特效 id，184 個帶 token；同一 id 可重複計）：

| 符號 | id 數 | 地圖記錄數 | 原生路徑 | 產品狀態 |
|---|---:|---:|---|---|
| `^` | 111 | 8,003 | `A06210` 循環候選 | **未實作** |
| `<` | 63 | 1,751 | `A060F0` 單次 | 僅 6016 |
| `[` | 10 | 61 | `A060F0` 單次 | 戰鬥路徑有；地圖特效未接 |

**新發現：id→類別表。** `F0B570`（約 `0xF0C700` 起）依 id 指定一個值，傳給 `A4E5D0` 的第 7 參數。

| id | 值 |
|---|---|
| `F112A0(id)` 為真 | 8 |
| `F11250(id)` 為真 | 11 |
| 1884、1885、2209、1901～1987 | 5 |
| 1892～1897 | 7 |
| 1886、2196 | 14 |
| 2195、2197～2202 | 9 |
| 149、1522 | 13 |
| 3442～3446 | `2*id-6879`，即 5、7、9、11、13 |
| 其他 | 0 |

3442～3446 共 8,282 筆，占全部地圖特效 31.7%；`engine/source` 查不到這些常數。此參數的語義本輪未解。

## B03：MAP-F4 區域特效淡出

**已驗證原生鏈。P36 triage 列的三項缺件中，「最終消費者」已補上：**

`F0B570`（高位元 `0x8000` 為延伸旗標；每筆 `u16 id, u16 x, u16 y, [u32 group]`）→ 陣列 `0x1545150` → `F0E9B0` 寫 `actor+612` → **`85E090` 遍歷 actor，`actor+612 == group` 時改 `+40`（mode）與 `+560`（alpha）**，mode 2 時 alpha 為 0。同函式族 `85E600`、`85EA90`、`85EE80` 也讀此欄（掃描所得，未逐一反編譯）。`A3A530` 把 `+612` 預設為 -1。

**另外兩個缺口是資料不存在，而不是找不到：**

| 掃描 | 結果 |
|---|---|
| `.s32` | 7,807 檔，解析失敗 0；有特效 1,949 檔，共 26,116 筆；**延伸 0 筆** |
| `.seg` | 330 檔，全部沒有特效尾段 |
| 無尾段 `.s32` | 474 檔 |
| 尾端多出資料 | 512 檔，全在 map `12004`，8 或 12 bytes，前 4 bytes 像時間戳；原生讀完特效後不再讀，且這些檔也是 0 筆延伸 |

範圍僅限 `D:\lineage開發\0-客戶端\map`，其他資料包不在內。

**建議：** 把 B03 改為「目前客戶端無資料觸發」並結案，保留 `OriginalMapCache.cs` 的 `NotSupportedException`，不要實作沒有資料可驗證的功能。

## 產品差異清單

| 差異 | 原生 | 產品 | 影響 |
|---|---|---|---|
| `<` | 與 `[` 同為單次聲音 | `SoundSchedule.Candidates` 只取 `[`（`CombatSfx.cs:9-11`） | 腳步聲等全部無聲，例如 `#0` 的 `<479`、`<478` |
| `^` | 循環候選，最多 5 個近距音源 | 沒有 | 111 個地圖特效 id 無環境音 |
| 候選上限 | 只存前 5 個 | 全取 | 超過 5 個時機率不同 |
| 候選為 0 | 抽中就略過 | 先過濾再抽 | 含 0 時機率不同 |
| 同 sample 200ms 冷卻 | 所有經 `A06020` 的音效 | 6016 有；`CombatSfx.cs` 沒有 | 連續動作可能比原生密集 |

`CombatSfx.cs` 的註解已標示「推測，待 #52 真機聽音核對」，這些差異是給該項的新依據，不代表要立刻全改。

## 盤點：`D:\lineage開發\登入器bin資源`

涵蓋欄：「P01」等為已被該研究使用；「無」表示以 `LinR`、`.unpacked.exe`、`rea-investigation`、`TW13081901bin拆解` 搜尋都沒有引用。搜尋範圍：`docs/`（不含 `project-consolidation/` 路徑清單）、`work/`、`主管/`（P25 之後的研究在這裡）；根目錄 24 個 `P*/` 的前兩層 Markdown（79 檔）；`lito脫殼資源` 根層的 Markdown 與 76 個 JSON。更深層未查。同類檔案合併一列，列內已逐一點名。

| 名稱 | 大小 | 類型 | 用途 | 專案 | 涵蓋 |
|---|---:|---|---|---|---|
| `TW1608262200.bin拆解/TW1608262200.bin` | 16,810,496 | 未加殼 PE32 | TW BIN 的原 BIN | TW BIN | P01 起全程使用；本輪補讀 |
| `TW1608262200.zip` | 5,090,049 | ZIP | 上檔壓縮副本（內含檔大小相同，未解壓比對） | TW BIN | 重複 |
| `TW13081901bin拆解/` | 1.44 GB／425 檔 | 脫殼成果 | 見下表 | 三者 | 部分：`rea-investigation` 用過 exe；P10 把 exe 列入實驗環境佈局，未做靜態分析 |
| `LinR.bin拆解/LinR.bin` | 235,703,808 | 重建 PE32，WinLicense 殘留 | 2022 新版客戶端（cocos2d） | 重建版 | 無 |
| `LinR.bin拆解/LinR_static_analysis_20260805/` | 2.05 GB／53 檔 | IDA＋報告 | 91,635 函式、RTTI、字串 | 重建版 | 無 |
| `LinR.zip` | 22,878,507 | ZIP | LinR.bin 副本（同上，未比對） | 重建版 | 重複 |
| `Lin.bin` | 11,365,984 | VMProtect（`.vmp0`），2015-05-26 | 舊版客戶端 | — | 無；靜態價值低 |
| `V3_Master_Report_20260604_100659.md` | 261,165 | AI 報告 | gpt-4o 審查 128 個 AOB 目標 | 登入器 | 無；未驗證，僅供線索 |
| `server.py` | 36,797 | MCP | reverse-32-mcp 副本 | 工具 | `tools/reverse-32-mcp` |
| `hook_capture.py` | 10,681 | 活程序 hook | 在 `0x4E94B0` 裝 hook | 登入器 | **不得執行（規則 7）** |
| `gree_path_probe.py` | 13,234 | 活程序探測 | 查 `0x539394` 的 opcode | 登入器 | **不得執行** |
| `scan_broadcast.py` | 8,862 | 活程序掃描 | 診斷 `0x4E94B0` | 登入器 | **不得執行** |
| `verify_gree_aob.py` | 7,185 | 靜態＋執行中驗證 | AOB 錨點 | 登入器 | 依賴的 `Lin380脫殼.bin` 已不存在 |
| `requirements-mcp.txt` | 15 | 文字 | `mcp`、`capstone` | 工具 | — |
| `__pycache__/` | 54,164 | 快取 | `server.py` 編譯檔 | — | 可忽略 |

### `TW13081901bin拆解/` 子項

| 名稱 | 大小 | 用途 | 價值 |
|---|---:|---|---|
| `TW13081901.unpacked.exe` | 11,624,448 | 完整重建 PE | **高**，同家族可讀樣本 |
| `TW13081901.unpacked.no-time-limit.exe` | 11,624,448 | 去時間限制版 | 低；分析用不到 |
| `analysis-final/` | 226.9 MB | IDA 資料庫、32,999 函式、79,271 呼叫邊、字串、`full-disassembly.asm` | **高**，可直接查 |
| `FINAL-REPORT.zh-TW.md`、`README.md` | 3,959／3,881 | 報告 | 入口；Hex-Rays 當時失敗，骨架非偽碼 |
| `pe-rebuild-final-report.json`、`runtime-validation.json`、`time-limit-patch-report.json`、`LATEST.json`、`LATEST.txt` | 1.98 MB 等 | 驗證紀錄 | 中 |
| `tools/`（`full_pe_unpack.py`、`build_runnable_pe.py` 等） | 435 KB | 脫殼與 IAT 重建流程 | **高**，LiTo 尚缺此能力 |
| `full-unpack/` | 813 MB／175 檔 | 擷取映像與符號 | 中；重建來源 |
| `sessions/` | 314 MB／49 檔 | 擷取場次 | 低 |
| `runtime-validation/`、`third-party/`、`vendor/` | 62 MB | pe-sieve、unicorn 等 | 低 |
| `build-*.ps1`、`run.ps1`、`launch-unpacked.ps1`、`capture-live.ps1`、`open-ida.ps1`、`query-address.ps1` | 數 KB | 腳本 | 含啟動遊戲者**不得執行** |

## 盤點：`D:\lineage開發\tools\rea-investigation`

這批是 2026-10-31～11-01 針對 `TW13081901`＋舊登入器與 `1-伺服器` 的事故分析，與 TW BIN 引擎沒有直接關係；對 lito 只能類比，能否套用要先確認是否共用程式碼。

| 名稱 | 大小 | 類型 | 用途 | 專案 | 涵蓋 |
|---|---:|---|---|---|---|
| `README.md` | 16,293 | 報告 | 地圖初始化、UI、callback 總結 | 登入器 | 無 |
| `crash-20261031-1720/` | 目錄 | 事故審核 | 初始化順序與就緒判定 | 登入器 | 無 |
| `crash-20261101-1037/` | 目錄 | 事故審核 | 進場順序、接收重入 | 登入器＋伺服器 | 無 |
| `crash-20261101-1607/` | 目錄 | 事故＋28 份函式 asm | 格子引用與物件生命週期；含 `005A9EE0.asm`（依字串與結構推定對應 `A3EF40`）、`004E75F0.asm` | 登入器、TW BIN 對照 | 無 |
| `packet-trace-review-20261101/` | 目錄 | 審核 | 指定連線逐包取證 | 伺服器 | 無 |
| `server-review-20261101/` | 目錄 | 審核＋probe | 初始化順序方案、LoginSuspension 模型 | 伺服器 | 無 |
| `trace-capacity-review-20261101/` | 目錄 | 審核 | 取證容量；含 `Server_Game.jar` 7.18 MB | 伺服器 | 無 |
| `source-snapshot/` | 3,272 | 來源 | `classic_bag_quantity.inc` 凍結版 | 登入器 | 無 |
| `points.json`、`writers.json`、`writers.csv` | 39,193／25,843／13,776 | 結構化 | 關鍵全域與寫入者 | 登入器 | 無 |
| `paths.asm`、`blocks.json`、`initial-blocks.asm`、`initial-blocks.json` | 1.26 MB／1.50 MB／115 KB／130 KB | 反組譯 | 本輪範圍路徑 | 登入器 | 無 |
| `instruction-evidence.json` | 7,264,677 | 證據 | 逐指令 bytes | 登入器 | 無 |
| `xrefs.json`、`direct-edges.json` | 414,223／241,901 | 交叉引用 | 呼叫邊 | 登入器 | 無 |
| `vtables.json` | 14,030 | 結構 | 物件 vtable | 登入器 | 無 |
| `callback-config-callers.asm`、`wrapper-callers.asm` | 12,452／126,452 | 反組譯 | callback 呼叫者 | 登入器 | 無 |
| `address-constant-candidates.json`、`offset-2fc-candidates.json`、`initial-call-candidates.json` | 42,848／8,128／1,303 | 候選 | 掃描結果 | 登入器 | 無 |
| `crash-evidence.json`、`replay-results.json`、`source-harness-results.json`、`verification.json`、`manifest.json` | 19,219／22,517／1,800／5,207／7,804 | 驗證 | Unicorn 33 案、C++ 8 案 | 登入器 | 無 |
| `inspect_native.py`、`scan_paths.py`、`replay_paths.py`、`build_catalog.py`、`verify_artifacts.py`、`build_source_harness.py`、`build-source-harness.cmd` | 數 KB～20 KB | 腳本 | 可重跑流程 | 工具 | 無 |
| `source-harness.cpp`、`.exe`、`.obj`、`source-harness-build.log` | 2,521／135,680／5,366／461 | 測試 | 自製離線 fixture | 登入器 | 無 |

## 對各專案的建議任務

每項一行：專案／用途／來源路徑。

| 專案 | 用途 | 來源路徑 |
|---|---|---|
| TW BIN | `CombatSfx` 納入 `<`，按原生規則處理候選上限與 0 值（配合 #52 聽音） | 本文「產品差異清單」；`sub_A3EF40` |
| TW BIN | 實作 `^` 循環環境音（最多 5 個近距音源） | `sub_A06210`、`sub_A06020` |
| TW BIN | 主控裁定 B03 結案 | 本文 B03；`work/issue-417/s32-effect-census.json` |
| TW BIN | 解 `A4E5D0` 第 7 參數，評估 3442～3446 等 id 的行為 | `sub_F0B570`、`sub_A4E5D0` |
| TW BIN | 找 TW16 物件建立封包，補 S87 同身份入口 | `A51920` 的 6 個呼叫點；第一組 dispatch table |
| TW BIN | 用 `A42C60`、`F112F0`、`5001C0` 更新 #48 的 unknown 表，對照伺服器拾取格式 | 本文 B07 |
| TW BIN | 查清 `!`、`]` 的消費語義 | `81C7C0`、`8ABC10`、`A4B060`、`A4BA20` |
| TW BIN | 修正 `TASKS.md`、`PLAN.md`、`docs/demo/README.md` 的 B03／B07 受阻理由 | 本文結論 1 |
| LiTo 登入器＋伺服器 | 確認 lito 是否共用 `1-伺服器` 的 8→206→87 順序問題 | `rea-investigation/server-review-20261101/` |
| LiTo 登入器＋伺服器 | 確認 lito 登入器有無 callback 清零同型缺陷 | `rea-investigation/README.md` 第三節 |
| LiTo 登入器＋伺服器 | 評估沿用指定連線逐包取證 | `rea-investigation/packet-trace-review-20261101/` |
| LiTo 重建版 | 用同一 parser 規格解 sprite 資料，取代逐項猜測 | 本文 B07；LiTo 快照 File Offset `0x3E85C` 一帶 |
| LiTo 重建版 | 把同一批函式在 LiTo 快照上定位，做版本差異表 | `LiTo.unpacked-oep-current.exe`；`docs/reference/rea-scripts/xver.py` |
| LiTo 重建版 | 評估用 TW13081901 的 IAT 重建流程補 LiTo 的 import 缺口（`pe-audit.json` 記載尚未重建；重擷取需另行授權） | `TW13081901bin拆解/tools/` |
| LiTo 重建版 | 以 LinR 的 RTTI 與封包處理類別名稱作為現代架構參考 | `LinR_static_analysis_20260805/05_ida/rtti_vftable_names.csv`、`network_packet_candidates.md` |

## 限制

- `<`、`[`、`^` 的語義在 TW1608262200 是函式層級驗證。TW13081901 比對的是 parser 全部常數與 selector 的結構特徵。LiTo 快照只驗證 parser 窗口（三個 xor 與符號比較都在），**消費端未確認**，也未對應到 `LiTo.bin` 本身。
- 全程靜態，沒有聽音或實機觀察；所有行為都要由 dot／使用者真機確認。
- 掃描 `.text` 用的是位元組樣式與 `imul 0x24` 啟發式，「沒找到」不等於不存在。
- `.zip` 只比對內含檔大小，未解壓雜湊；`Lin.bin` 與 `LinR` 只做分診與指紋，沒有深入分析。
