# 來源閉合：規則表（T446／#1031）

整理日期：2026-11-13。範圍是 B15／M8 的四份規則表：`dialogs/skills.json`、`magic/catalog.json`、`engine/source/OriginalExpTable.cs`、`engine/source/Resources/ground-catalog.json`。這四份目前都來自伺服器 SQL 或原服模板。本文逐份回答：客戶端有沒有同樣的資料、產品能不能不靠這張表、都不行的話缺什麼。

記號：**已驗證**＝這一輪直接從檔案或反組譯看到；**推測**＝由已驗證事實推論；**待驗證**＝這一輪沒查。

## 結論

| 表 | 結論 | 一句話 |
|---|---|---|
| `dialogs/skills.json` | **client-only 來源** | 名稱取 `Text.pak` 的 `spelldesc-c.tbl`；等級不用表，照原版算式 `((id−1)>>3)+1` |
| `magic/catalog.json` | **可改為不需要此表** | 五個欄位各有替代：名稱與 MP 取 `spelldesc-c.tbl`，動作取 `SkillCast.Wire`，對象取 `SpellExDesc-c.tbl`，冷卻用原版預設 10 ms，`effect` 沒有人讀 |
| `OriginalExpTable.cs` | **blocked（已知限制）** | 缺「每級累計 EXP 門檻」。原版把它寫死在執行檔的 `.data`，不在任何 pak；伺服器也不送。而且客戶端的表和伺服器的表本來就不一樣 |
| `ground-catalog.json` 模板列 | **可改為不需要此表**（要主控裁定） | 客戶端沒有物品模板，給不出同樣的匹配鍵；但這 7 列只是引擎自己加的「可丟棄白名單」，拿掉白名單就不需要。不拿掉就是 blocked |

blocked 與需要裁定的項目，整理在最後一節「交主控」。

## 0. 方法與來源（全部唯讀）

- **產品讀取點**：讀 `engine/source` 原始碼（main `04e3fb6b`），沒有建置、沒有執行任何編譯出的程式。
- **客戶端 pak**：`D:\lineage開發\0-客戶端`，用 PakViewer CLI（`pakviewer-cli.exe`，只用 `pak list`／`pak info`／`pak extract`／`xml decrypt`）。
  - 先用 [`rea-scripts/pak/pak_index.ps1`](../rea-scripts/pak/pak_index.ps1) 列出全部 24 個 idx，共 308,323 個檔名（數字與 #752 相同）。
  - 再用 [`rea-scripts/t1031/extract.ps1`](../rea-scripts/t1031/extract.ps1) 解出 398 個「不是圖、不是 HTML」的檔案到 `scratch/t1031/all/`，做內容搜尋。檔名清單在附錄 B。
  - 解出的內容都留在 `scratch/`，沒有進 git。
- **基線**：主目錄 `engine/assets`（junction → `releases/r29/assets`）的 `dialogs/skills.json`、`magic/catalog.json`，只讀，用來比對。
- **原版執行檔**（只做靜態分析，pefile＋Capstone，沒有執行）：
  - `TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`。File Offset（FO）：`.text`＝VA − `0x400C00`；`.rdata`＝VA − `0x401400`；`.data`＝VA − `0x401800`。
  - LiTo 的 OEP 記憶體快照 `LiTo.unpacked-oep-current.exe`，SHA-256 `4271d92fb2b9a2fa8d118dde0767f6d8a67a896227bcc24127438fcb5c2ab6e9`（同 `rea-inventory.md`）。這是 3.81 客戶端脫殼後的映像，FO＝VA − `0x400000`。
  - `0-客戶端\LiTo.bin`，SHA-256 `ce6ee68ada8fd37e6c096b1a27e46cb6888f1179f85defb5007ac60287360292`，有 Themida 殼，只做 byte 搜尋。
- **沒有讀 SQL、JAR、DB**。基線 `magic/catalog.json` 裡本來就嵌著 `sqlRow` 字串，本文只用它的 `id`／`name`／`mp`／`action`／`effect`／`reuseMs`／`target` 七個欄位做比對。
- 腳本都在 [`rea-scripts/t1031/`](../rea-scripts/t1031/)，用 `py -3 -I -X utf8 <腳本> <參數>` 執行，輸出只印到主控台。

## 1. `dialogs/skills.json`

**結論：client-only 來源（名稱）＋不需要表（等級）。**

### 1.1 產品用到哪些欄位（已驗證）

基線檔 SHA-256 `52592c24…6ba5ef8a`，807 筆，每筆只有 `Id`／`Name`／`Level`。

| 讀取點 | 用到的欄位 | 用途 |
|---|---|---|
| `NpcUi.cs:76` | 整檔 | 檔案存在就載入成 `SkillCatalog`；不存在就不載入 |
| `NpcServices.cs:85`（S41 可學清單） | `Name`、`Level` | 列名稱；學費＝基價 × `Level`²（`NpcServices.cs:9`，`Level` 限 1–28 且不是 16） |
| `Magic.cs:40`（已學清單） | `Name` | 蓋掉 `MagicRules.Describe` 的名稱 |
| `SpellExchange.cs:66`（以物品學魔法） | `Name` | 列名稱 |

產品實際碰得到的 id 只有 1–256：S41 的索引加 1，`SkillCast.Supported` 只認 1–220。807 筆裡有 588 筆 id 大於 256（10000 以上的怪物技能），產品讀不到。

### 1.2 客戶端來源（已驗證）

| 項目 | 內容 |
|---|---|
| 容器 | `Text.idx`（SHA-256 `43a342ad…49741f6f`）／`Text.pak`（`9516a372…616a37ff`），L1 加密，PakViewer 自動解開 |
| entry | `spelldesc-c.tbl`，idx 第 32427 筆，Text.pak File Offset `0x159B8EE`，3,925 bytes，解出後 SHA-256 `387c4a9d6ffb3c1bd867127a0161c23d624d75eeb0db999e79691ff2e3d30ab9` |
| 編碼與格式 | CP950 文字。第一行是筆數 `220`。之後每行 `<索引> <名稱> (<MP>/<HP>[/<材料數>])`，共 183 行 |
| 欄位 | 索引＝技能 id − 1；名稱；MP；HP；第三個數字是材料數（例如毒咒 `(10/0/1)`） |
| 例外行 | 索引 107 寫成 `會心一擊 (HP-1/MP-1)`，括號內不是數字；解析名稱時取最後一個 ` (` 之前的文字即可 |

原版怎麼用這張表：TW16 的字串 `SpellDesc` 在 VA `0x11801FC`／FO `0xD7EDFC`，被 `.text` VA `0x8CA279` 引用；LiTo 快照的字串是 `LTSpellDesc`（VA `0x62CDF8`，被 VA `0x47C8E7` 引用）。也就是 3.81 的 LiTo 客戶端讀的是 `LiTo\LTtext.pak` 的 `LTSpellDesc-c.tbl`（**推測**：只看到字串與引用點，沒有逐行反組譯載入流程）。

`LTSpellDesc-c.tbl`（`LiTo\LTtext.idx` 第 282 筆，3,095 bytes，SHA-256 `6c8bba0b…ed6c54dfc`）只有 147 行、最大索引 175，**沒有龍騎士與幻術師（181–220）**，還有一筆亂碼（`擬似魔法武器ア`）。所以建議用 `Text.pak` 的 `spelldesc-c.tbl`。

### 1.3 和基線比對（已驗證，`skills_cmp.py`）

id 1–220 的範圍：

| 比對 | 筆數 | 說明 |
|---|---:|---|
| 兩邊都有、名稱相同 | 177 | |
| 兩邊都有、名稱不同 | 6 | 見下表 |
| 只有基線有 | 36 | 全部是名稱 `none`、等級 0 的佔位列（24、81–86、92–96、121、123–128、135、136、139–144、177–180、196–200） |
| 只有客戶端有 | 0 | |
| 兩邊都沒有 | 1 | 122 |

| id | 基線（SQL） | 客戶端 `spelldesc-c.tbl` |
|---:|---|---|
| 30 | 巖牢 | 岩牢 |
| 111 | 閃避提升 | 迴避提升 |
| 115 | 鋼鐵士氣 | 閃亮之盾 |
| 119 | none | 神聖犧牲 |
| 120 | none | 幻象 |
| 213 | 武器破壞者 | 隱身破壞者 |

這 6 筆是譯名不同或基線沒填。改用客戶端表之後，畫面顯示的會是原版客戶端自己的譯名。

### 1.4 等級不需要表（已驗證）

- 原版的學魔法窗自己算價格：`(索引>>3 + 1)² × 基價`，索引是技能 id − 1。出處是 TW16 的 `sub_931FF0`（FO `0x5313F0`），見 [`trade-window-and-projectiles.md`](../trade-window-and-projectiles.md) 1.1 節。客戶端沒有、也不需要等級表。
- 基線 id 1–220 裡等級不是 0 的 183 筆，**全部**等於 `((id−1)>>3)+1`，0 筆例外。等級是 0 的 36 筆就是上面的 `none` 佔位列。

### 1.5 產品改法（交 T462）

1. `SkillCatalog` 改成從 `TextArchive` 讀 `spelldesc-c.tbl`：`Name`＝該行名稱；`Level`＝`((id−1)>>3)+1`；表裡沒有的 id 不建項目。
2. `NpcUi.cs:76` 不再讀 `dialogs/skills.json`。準備器不需要產生這個檔。
3. 行為差異只有三點：上表 6 個名稱；119、120 從 `none` 變成有名稱；36 個佔位 id 從「`none`、不能買」變成「缺少名稱、不能買」（`NpcServices.cs:85` 既有的備援文字）。價格規則不變。
4. `NpcServices.cs:9` 的「等級 16 不給價」可以留著：121–128 不在客戶端表裡，本來就不會有項目。

## 2. `magic/catalog.json`

**結論：可改為不需要此表。**

### 2.1 產品讀取點（已驗證）

基線檔 SHA-256 `024576e3…54a44ebf`，schema `p24-magic-casting-v1`，只有技能 1、3、4、8 四筆。唯一的載入點是 `MagicSpells.cs:5`；檔案不存在時 `MagicSpells.Get` 已經有備援（`MagicSpells.cs:16–20`，#187 加的）。

| 欄位 | 誰在讀 | 用途 |
|---|---|---|
| `name` | `Magic.cs:26` | 技能名稱；有 `SkillCatalog` 時會被蓋掉（`Magic.cs:40`） |
| `mp` | `Magic.cs:26` → `LearnedMagic.NominalMp` → `PlayerHudIntegration.cs:72`、`NativeUi/SpellWindowPanel.cs:144` | 只用來顯示；不拿來擋施法 |
| `action` | `Magic.cs:30`、`:31`、`:116` | 18（有方向）或 19（無方向），決定施法節奏 |
| `reuseMs` | `Magic.cs:30`、`:31`、`:116` | 和動作時間取大值；`:116` 先問 `SpellListCooldowns` |
| `target` | `Magic.cs:26`、`:44`、`MagicUi.cs:41` | `none`／`buff`／`attack`，決定目標提示與是否對自己施放 |
| `effect` | 沒有 | 讀進 `MagicSpell.Effect` 之後沒有任何程式使用 |
| `targetFlags`、`wire`、`sqlRow` | 沒有 | 載入時就沒讀（`wire` 改由 `SkillCast.Wire` 決定，見 `docs/protocol/skills.md`） |

### 2.2 每個欄位的替代（已驗證，四筆逐欄比對）

| 欄位 | 替代來源 | 1 初級治癒術 | 3 保護罩 | 4 光箭 | 8 神聖武器 |
|---|---|---|---|---|---|
| `name` | `spelldesc-c.tbl`（第 1 節） | 相同 | 相同 | 相同 | 相同 |
| `mp` | `spelldesc-c.tbl` 括號內第一個數字 | 4＝4 | 8＝8 | 3＝3 | 10＝10 |
| `action` | `SkillCast.Wire`：`DHH`／`DHHS` → 18，其他 → 19（`MagicSpells.cs:20` 現有備援） | 19＝19 | 19＝19 | 18＝18 | 19＝19 |
| `reuseMs` | 原版預設 10 ms（`SpellListCooldowns.DefaultDelayMs`，出處 `ActionCadence.cs:67`） | 10＝10 | 10＝10 | 10＝10 | 10＝10 |
| `target` | `SpellExDesc-c.tbl` 的「對象：」欄，見下 | `buff` | `none` | `attack` | `buff` |
| `effect` | 不需要：特效編號由伺服器封包帶 | — | — | — | — |

`target` 的客戶端來源：

| 項目 | 內容 |
|---|---|
| 容器 | `Text.idx`／`Text.pak`（同第 1 節） |
| entry | `SpellExDesc-c.tbl`，idx 第 32434 筆，File Offset `0x15A1355`，39,348 bytes，SHA-256 `2fcb23e0fd7f6278602f9de54e83ffcd3beb416a749eb8d1c901421f8a5feeb2` |
| 格式 | CP950。第一行 `220`，之後每行 `<索引> <說明>`；說明用 `^` 分段、`\fX` 是色碼。183 行 |
| 欄位 | `對象：`後面的文字。183 行裡 177 行有這一欄，共 44 種寫法 |

對照規則：

- 「對象：施法者」→ `none`（只能對自己）。183 行裡有 52 行，技能 3 在內。
- `SkillCast.Wire` 是 `DHH`／`DHHS` → `attack`（技能 4；它的對象欄寫「10格內的…」，和 `Magic.cs:25` 寫死的 10 格一致）。
- 其他 → `buff`（技能 1、8）。

四筆都和基線相同。

### 2.3 產品改法（交 T462）

1. `MagicSpells` 不再讀 `assets/magic/catalog.json`；`Get(id)` 一律走現在的備援路徑，再補三件事：名稱與 MP 取 `spelldesc-c.tbl`；`ReuseMs` 的備援值從 0 改成 `SpellListCooldowns.DefaultDelayMs`；`Target` 在對象欄是「施法者」時給 `none`。
2. 準備器不需要產生這個檔。`MagicSpell.Effect` 可以一併拿掉。
3. 什麼都不補、只刪檔的話，這四個技能會變成：名稱「原服技能 N」（有 `SkillCatalog` 時不受影響）、MP 顯示空白、技能 3 的提示從「自己」變成「自己／指定對象」、`reuseMs` 10 → 0（和動作時間取大值後沒有差別）。封包不變：伺服器會把 `target=none` 的技能解析成施法者（`docs/protocol/skills.md`）。

### 2.4 沒查的

- 3.81 原版客戶端怎麼決定「要不要先選目標」：TW16 讀 `SpellList.xml` 的 `category`，但本機 3.81 客戶端沒有這個檔（#752），LiTo 快照裡的對應邏輯沒查。**待驗證**。用說明文字的「對象：」欄是本文提出的自主規則，四筆對得上，其餘 179 筆沒有基線可比。
- `spelldesc-c.tbl` 和 `SpellExDesc-c.tbl` 的 MP 有 2 筆互相不一致（204：20 對 10；214：4 對 40）。建議以 `spelldesc-c.tbl` 為準，因為技能窗用的是它（**推測**）。

## 3. `engine/source/OriginalExpTable.cs`

**結論：blocked，列為已知限制。**

### 3.1 產品怎麼用（已驗證）

`OriginalExpTable.Thresholds` 是 101 個 `ulong`（索引 0–100），檔頭註明「Generated from fixed original SQL」。唯一的讀取點是 `Rewards.cs:13`：

- `DecodeExperience(level, low32)` 取 `t[level−1]`、`t[level]` 當本級起點與終點。
- 低 32 位落在這個區間內，才填 `Total`、`LevelStart`、`NextLevel`、`Percent`；否則這四個都是 null。
- 下游：`ExperienceText`（`Rewards.cs:42`）、原生左狀態框的 EXP%（`NativeUi/State/LeftStatusState.cs`、`LeftStatusPanel.cs`）、`RewardIntegration.cs:79`、`:90` 的 EXP 差值。

### 3.2 原版怎麼算 EXP 百分比（已驗證）

**TW16（`TW1608262200.bin`）**

- 門檻函式 `sub_EE1260(level)`，VA `0xEE1260`／FO `0xAE0660`：
  - 伺服器功能旗標 `dword_13CE514` 的 bit 30 沒設時：`level < 0` 回 0；`level < 51` 回表 A 的第 `level` 項；否則回 `(level − 50) × 36,065,092 + 91,876,037`。
  - bit 30 有設時：`level < 65` 回表 B 的第 `level` 項；否則回 `(level − 64) × 36,065,092 + 400,640,553`。
  - 兩張表每項 8 bytes，程式只讀低 4 bytes。
- 表 A：`.data` VA `0x138B958`／FO `0xF8A158`，51 項（索引 0–50），內容的 SHA-256（51 個 little-endian qword）`426799bcf4784717b2ac8b79eca3a8ddb0f5698dd6994381c9b111d9b6bea7f5`。
- 表 B：`.data` VA `0x138BAF0`／FO `0xF8A2F0`，65 項（索引 0–64），是較新版本的曲線，第 49 級起和表 A 不同。
- 百分比函式 `sub_EE12E0`，VA `0xEE12E0`／FO `0xAE06E0`：
  `min(99.9999, (目前 EXP − 門檻(level−1)) × 100.0 ÷ (門檻(level) − 門檻(level−1)))`。
  常數 100.0 在 VA `0x1161668`／FO `0xD60268`，99.9999 在 VA `0x11E2D48`／FO `0xDE1948`；取小值的函式是 `sub_963D90`。
- `sub_EE1260` 有 18 個呼叫點、`sub_EE12E0` 有 5 個（含 [`sprites-and-strings.md`](../sprites-and-strings.md) 記過的 `sub_C07330`）。

**3.81（LiTo 快照）**

- 門檻函式在 VA `0x4CDDC0`／FO `0xCDDC0`：`level < 0` 回 0；`level < 51` 回表的第 `level` 項；否則回 `level × 36,065,092 − 1,711,378,563`（代入 50 得 91,876,037，和表尾相接）。
- 表在 VA `0x6360F8`／FO `0x2360F8`，51 項，51 個 little-endian qword 的 SHA-256 `e7fa626a61de5fa7986bd50e4519f4bfb39d5a4827956e88d929872cda60d3ee`。
- 3.81 的百分比函式這一輪沒有定位。**待驗證**；算式**推測**與 TW16 相同。

### 3.3 三張表互相不一樣（已驗證，`qdump.py`）

| 等級 | 產品（SQL） | TW16 表 A | 3.81 LiTo |
|---|---|---|---|
| 1–4 | 125、300、500、750 | 相同 | **20、45、80、625** |
| 5–7、9 | 1296、2401、4096、10000 | 相同 | 相同 |
| 8、10–22 | 客戶端值 **+20** | n⁴ 系列 | 同 TW16 |
| 23–40 | 客戶端值 **+16** | | 同 TW16 |
| 41–50 | 客戶端值 **+17** | | 同 TW16 |
| 51 以上 | 每級 +36,065,092 | 同斜率，起點差 17 | 同 TW16 |

產品的表和 TW16 表 A 有 42 項不同、和 3.81 表有 46 項不同。也就是說，**就算能從客戶端讀出表，也還原不出產品現在這 101 個值**；伺服器的升級門檻和客戶端顯示用的門檻本來就是兩份資料。

### 3.4 為什麼判 blocked

缺的欄位：**每一級的累計 EXP 門檻**（產品目前 101 個值）。

1. **不在任何 pak。** 24 個 idx 的檔名搜過 `exp`、`level`、`lvup`、`lvl`、`rule`：
   - `exp` 只有 `ntexpet.tbl`、`ntexpet-c.tbl`（寵物說明）與 45 個 HTML；
   - `level` 是 `LevelUpHelp.tbl`（升級說明頁對照）、`LevelUp_BS.tbl`；
   - `lvup` 是 34 個 `lvup<職業>-<語系>.tbl`（各等級的說明文字，例如「[12] 可以學習第3級魔法」）；
   - `lvl` 是 `hpasslvlist.xml`。
   398 個解出的檔案內容，搜「至少出現兩個門檻數字」（2401、6561、6581、14641、14661、20736、20756、28561、28581、1185921、1185937），只有 `desc-c.tbl`、`untransfer.tbl` 命中，逐行看過都是物品字串編號（`JP_6561`、`$2401` 之類），不是 EXP 表。
2. **TW16 引用的 `ExpInfo.xml` 不是門檻表。** 字串在 VA `0x116C8D0`，本機 `0-客戶端` 沒有這個檔；kor25 的 `Tile.pak` 有（1,995 bytes），解開後是 EXP 加倍活動的設定（StartDate／EndDate／Map／Ratio），沒有門檻。
3. **原版把表寫死在執行檔。** 使用者的客戶端只有加了 Themida 殼的 `LiTo.bin`；對原檔搜三種表頭（4 bytes 與 8 bytes 兩種寬度）都是 0 筆，靜態讀不到。專案目標也是不再依賴舊 BIN。
4. **伺服器不送。** S113 只有等級、EXP 低 32 位與一個固定的 1（`docs/reference/server-protocol.md` S113；`Rewards.cs:29`）。沒有門檻、沒有百分比。所以「執行期只用伺服器送來的值」只能顯示等級與 EXP 原值，算不出百分比、下一級門檻。
5. **不是算式。** 5–7 級是 n⁴，但 1–4 級、45 級以後與 +16／+17／+20 的偏移都不規則；用算式重寫等於把表藏進程式，不符合本任務的規定。

### 3.5 主控可以選的處理方式

| 選項 | 做法 | 代價 |
|---|---|---|
| A 保留例外 | `OriginalExpTable.cs` 留在 `b15Exceptions`，在已知限制清單寫明它是伺服器規則值、不是客戶端素材 | B15 的例外清不到 0 |
| B 拿掉表 | `DecodeExperience` 不再查表：`Percent`、`LevelStart`、`NextLevel` 一律 null；`Total` 直接用低 32 位；差值用兩次低 32 位相減 | 左狀態框的 EXP% 空白，比原版少一個顯示。CLAUDE.md 規則 5 不允許用假值填，所以只能空白 |
| C 使用者自備 | 準備器接受使用者提供的門檻檔（例如從自己的伺服器設定匯出），沒有就走 B | 多一個選用輸入；可攜 zip 本身不含表 |

本文不替主控選。B 與 C 都不需要客戶端來源。

## 4. `ground-catalog.json` 的 7 筆 template／nameToken

**結論：客戶端給不出同樣的匹配鍵；但產品可以改成不需要這 7 列（要主控裁定）。不改就是 blocked。**

### 4.1 產品怎麼用（已驗證）

檔案是嵌入資源（`OfflineClient.csproj:21`），SHA-256 `ee544685…3c71f4d8`，7 列：治癒藥水、終極治癒藥水、自我加速藥水、肉、傳送回家的卷軸、金幣、箭。

| 用途 | 程式 | 用到的欄位 |
|---|---|---|
| **可丟棄白名單** | `GroundCatalog.Match`／`CanDrop`（`GroundCatalog.cs:26–27`）← `GroundRules.Droppable`（`Ground.cs:18`）← `CanDropGroundItem` | `inventoryDesc`、`inventoryIcon`、`wireUseType`、`nameToken`（比對伺服器送來的名稱）、`inventoryBlesses`、`stackable`，而且 `sprite` 不能是 null |
| 地面圖的 pin | `GroundCatalog.Find` → `GroundSpriteReader`（`GroundRendering.cs:4`、`:11`、`WebPanel.cs:74`） | `groundGfx`、`sprite`（entry、offset、SHA）。表裡沒有的 gfx 已有通用備援（`GroundCatalog.cs:25`，#190） |
| 載入時的完整性檢查 | `GroundCatalog.cs:21` | `templateId > 0`、`source.rowSha256`／`contractSha256` 長度 |

`templateId`、`name`、`templateUseType`、`tradable`、`cantDelete`、`templateBless`、`source.location`（`db:p24_world.etcitem`）、`rowSha256` 來自原服模板（#49）。其中產品真正拿來做判斷的只有白名單那一列的六個欄位；`templateId` 與 `source` 只在載入時檢查格式。

`sprite` 那一段（`45-0.spr` 等 4 筆的 idx 位置、offset、SHA）是從客戶端 `Sprite.pak` 量出來的（#48），不是伺服器資料，準備器可以重新產生。

### 4.2 客戶端有什麼（已驗證）

| 匹配鍵 | 客戶端有沒有 | 說明 |
|---|---|---|
| `nameToken` → 名稱 | **有** | `Text.pak` 的 `desc-c.tbl`（idx 第 4201 筆，File Offset `0x192C49`，249,300 bytes，SHA-256 `f06c94f4…25adb7df`，和 #97 附錄記的歷史值相同）。`$N` 對應第 N+2 行（第一行是筆數 `17001`）。7 個 token 都查得到：`$4` 金幣、`$23` 肉、`$26` 治癒藥水、`$61` 箭、`$234` 綠色藥水、`$238` 白色藥水、`$505` 傳送回家的卷軸 |
| 說明編號 → 說明文字 | 有 | `ItemDesc-c.tbl`，每行 `<編號> <文字>`。只有文字，沒有其他欄位 |
| 背包圖示 ↔ 地面 gfx | **沒有** | 這個對應在伺服器模板。客戶端收到的地面物件封包本來就直接帶 gfx，不需要對照表 |
| `wireUseType`、`stackable`、`bless` | **沒有** | 都是伺服器在封包裡送的值，客戶端沒有按物品列出的表 |
| `templateId`（40010 等） | **沒有** | 398 個檔案搜「至少出現兩個模板編號」，0 筆 |

其中兩筆的客戶端名稱和基線的 `name` 不同：`$234` 客戶端是「綠色藥水」、基線是「自我加速藥水」；`$238` 客戶端是「白色藥水」、基線是「終極治癒藥水」。基線的 `name` 產品沒有讀（`GroundDefinition` 沒有這個欄位），不影響行為。

客戶端另有三份「不可轉移」清單：`untransfer.tbl`（2,055 筆 `$N`）、`untransferble.tbl`、`untransferble2.tbl`（說明編號），以及 `untransaction.tbl`。TW16 會載入它們（字串在 VA `0x117DD30`、`0x117DD0C`、`0x117DD50`、`0x118E578`）；LiTo 快照裡**沒有**這些檔名字串，所以 3.81 客戶端不用它們。它們是黑名單，也不是 `ground-catalog` 那種白名單的替代品。

檔名搜尋：24 個 idx 搜 `drop`、`ground`、`templ`、`catalog`、`rule`，非 HTML 的命中是 0。

### 4.3 為什麼可以不需要

這 7 列是引擎自己加的保守政策（`GroundCatalog.cs:5` 的註解：「the frozen rows remain a conservative drop policy」），不是原版行為的一部分：

- 客戶端沒有物品模板表，丟棄能不能成功由伺服器裁定（[`server-rules.md`](../server-rules.md) R22：白名單、tradable、限時、已裝備、封印等都在伺服器檢查，失敗會回 125／210／1181／623 等訊息）。
- 原版客戶端送出丟棄封包前在本機檢查什麼，這一輪沒有反組譯；[`followups-7.md`](../followups-7.md) 1.5 節只查了刪除確認。**待驗證**。

### 4.4 產品改法（交 T464，先由主控裁定）

1. `CanDrop` 拿掉 `Match(i)` 與 `InventoryBlesses`、`Stackable` 的條件，只留和模板無關的本機條件（有物件 ID、數量大於 0、沒有充能數、沒裝備、不是裝備類、`Bless < 128`、旗標正常）。數量輸入改看持有量是否大於 1。
2. 嵌入資源只留從客戶端量得到的部分（`groundGfx` 與 `sprite` pin），或整個改由準備器從 `Sprite.pak` 產生；`templateId`、`source`、`name` 等伺服器欄位與 `GroundCatalog.cs:21` 的對應檢查一起拿掉。
3. 用到 `GroundCatalog.Rows`、`CountName` 的測試（`ground-tests`、`pickup-tests`、`npc-tests`、`demo-tests`、`PointerFlowTests.cs:34`）要改用合成列。

風險：白名單拿掉後，玩家可以對任何物品送出丟棄封包，伺服器拒絕時要有訊息可看；誤丟貴重物品的機會也變大。這是行為變更，所以要主控裁定。不同意的話，這 7 列維持現狀，列為已知限制（缺的欄位見 4.2 表中「沒有」的三列）。

## 5. 交主控

| # | 項目 | 狀態 | 需要決定的事 |
|---|---|---|---|
| L1 | EXP 門檻表 | **blocked／已知限制** | 3.5 節的 A、B、C 選一個 |
| L2 | `ground-catalog` 模板列 | 可不需要，但屬行為變更 | 同意拿掉可丟棄白名單（4.4），或維持現狀列為已知限制 |
| L3 | 技能名稱改用客戶端譯名 | 不是限制，只是顯示差異 | 知悉即可：6 個名稱會變（1.3） |
| L4 | 技能「對象」規則 | 自主規則，四筆對得上基線 | 知悉即可：3.81 原版的選目標判斷沒查（2.4） |

## 6. 這一輪沒有驗證的

- 沒有建置、沒有執行產品，也沒有真機。上面的「產品改法」都還沒有實作。
- `TextArchive` 能不能直接讀 `spelldesc-c.tbl`、`SpellExDesc-c.tbl`：只確認 PakViewer 解得出來，產品端交 T462 確認。
- 原版載入 `SpellDesc`／`LTSpellDesc` 的流程、3.81 的 EXP 百分比函式、原版送丟棄封包前的本機檢查、`dword_13CE514` bit 30 的來源（該位址在未初始化資料區，檔案裡沒有值）：都只看到引用點，沒有往下追。
- 基線 `skills.json` 裡 id 大於 256 的 588 筆沒有比對，因為產品讀不到。
- 參考客戶端（kor25、t89、ly）只看了 `ExpInfo.xml` 與 `SpellList.xml` 的開頭，沒有做全面比對；它們不是使用者的 `TW_CLIENT_ROOT`，不能當來源。

## 附錄 A：查過的 pak

`D:\lineage開發\0-客戶端`，PakViewer `pak list`，共 24 個 idx、308,323 個檔名。

| idx（→ pak） | 加密 | 檔數 |
|---|---|---:|
| `Text.idx` | L1 | 37,498 |
| `Tile.idx` | None | 5,446 |
| `Sprite.idx` | None | 92,356 |
| `Sprite00.idx`–`Sprite15.idx`（16 個） | None | 10,207／10,232／10,320／10,458／10,593／10,700／10,844／10,949／10,972／10,970／10,862／10,716／10,522／10,357／10,255／10,184 |
| `LiTo\LTtext.idx` | L1 | 353 |
| `LiTo\LTsurf.idx` | None | 1,623 |
| `LiTo\LTsprite.idx` | None | 469 |
| `LiTo\LTtile.idx` | None | 1,210 |
| `LiTo\LTicon.idx` | None | 227 |
| `launcher\TW13081901.pak` | — | 沒有 idx，PakViewer 打不開（同 #752） |

檔名關鍵字（不分大小寫，比對全部 308,323 個檔名）：`skill`、`spell`、`magic`、`exp`、`level`、`lvup`、`lvl`、`item`、`drop`、`ground`、`templ`、`etc`、`rule`、`catalog`。非 HTML 的命中：

- `skill` 0；`drop` 0；`ground` 0；`templ` 0；`rule` 0；`catalog` 0。
- `spell` 12：`spelldesc-c.tbl`、`SpellDesc-e／h／j／k.tbl`、`SpellExDesc-c.tbl`、`spelldur.tbl`（`Text`）；`LTSpellDesc-c／k.tbl`、`LTspelldur.tbl`（`LTtext`）；`SpellDesc-k.tbl`、`SpellIcon.idx`（`Sprite`）。
- `magic` 2：`MagicDoll.tbl`、`uimagicitem.xml`。
- `exp` 2：`ntexpet.tbl`、`ntexpet-c.tbl`。
- `level` 2：`LevelUpHelp.tbl`、`LevelUp_BS.tbl`。`lvl` 1：`hpasslvlist.xml`。`lvup` 34：各職業各語系的 `lvup*.tbl`。
- `item` 67：`ItemDesc-c／e／h／j／k.tbl`、`itemextra.tbl`、`itemimportant.tbl`、`itemquest.tbl`、`alertitems.tbl`（`Text`）；`itemextra.tbl`（`Sprite`）；`uimagicitem.xml`（`Tile`）；`LTItemSet*-*.tbl` 52 個、`LTpitem-c／h／j／k.tbl`（`LTtext`）。
- `etc` 1：`helpetc-j.BAK`。

`Sprite*.idx`、`LTsurf`、`LTsprite`、`LTtile`、`LTicon` 裡不是圖檔（`.spr`／`.img`／`.tbt`／`.png`／`.til`／`.lbt`）的只有：`Sprite` 的 `0.alp`、`crown.raw`、`desc-k.tbl`、`itemextra.tbl`、`list.ico`、`list.lst`、`Source.lmp`、`SpellDesc-k.tbl`、`SpellIcon.idx`、`vssver.scc` 與 4 個 `.sp`；`Sprite00` 的 `vssver.scc`；`Sprite01` 的 `rename_c.py`；`Sprite05` 的 `list.ico`；`Sprite07` 的 `Thumbs.db`；`LTicon` 的 `LTlist.ico`；`LTtile` 的 `default.att`／`.sec`／`.tie`、`MFC523.tmp`、`vssver.scc`。其中 `Sprite` 的 `SpellIcon.idx`（336 bytes）是技能圖示編號的二進位表，沒有名稱或等級。

## 附錄 B：解出並做內容搜尋的 398 個檔案

`extract.ps1` 依副檔名選檔：`Text`／`LTtext` 取 `.tbl`、`.def`、`.txt`；`Tile` 取 `.xml`、`.enx`（XML 另解密成 `.dec.xml`）；`Sprite` 取 `.tbl`、`.lst`、`.idx`、`.ico`、`.lmp`。每個檔的大小與 SHA-256 在 `scratch/t1031/all/HASHES.tsv`（不進 git，重跑腳本可得）。

內容搜尋（`content_scan.py`，CP950 與 UTF-8 各解一次）：

| 搜什麼 | 命中 |
|---|---|
| EXP 門檻數字至少兩個 | `desc-c.tbl`、`untransfer.tbl`（都是字串編號，誤中） |
| 同時有「初級治癒術」與「光箭」 | `spelldesc-c.tbl`、`SpellExDesc-c.tbl`、`desc-c.tbl`、`LTSpellDesc-c.tbl` |
| 伺服器模板編號至少兩個 | 0 |

**`Text.idx`（229 個）**：`ActionDesc-c.tbl`、`ActionExDesc-c.tbl`、`alertitems.tbl`、`blockmsg.tbl`、`bonus.tbl`、`campaignDesc-c.tbl`、`campaignDesc-e.tbl`、`campaignDesc-h.tbl`、`campaignDesc-j.tbl`、`campaignDesc-k.tbl`、`changepass-c.tbl`、`changepass-e.tbl`、`changepass-h.tbl`、`changepass-j.tbl`、`changepass-k.tbl`、`CharAttrType.tbl`、`Charisma-e.tbl`、`charsound.def`、`clause.txt`、`Constitution-e.tbl`、`credits-c.tbl`、`credits-e.tbl`、`credits-h.tbl`、`credits-j.tbl`、`credits-k.tbl`、`CrystalInfo.tbl`、`darkelf-c.tbl`、`darkelf-h.tbl`、`darkelf-j.tbl`、`darkelf-k.tbl`、`desc-c.tbl`、`desc-e.tbl`、`desc-h.tbl`、`desc-j.tbl`、`desc-k.tbl`、`Dexterity-e.tbl`、`dragonKnight-c.tbl`、`elf-c.tbl`、`elf-e.tbl`、`elf-h.tbl`、`elf-j.tbl`、`elf-k.tbl`、`engineer-c.tbl`、`engineer-e.tbl`、`engineer-h.tbl`、`engineer-j.tbl`、`engineer-k.tbl`、`envsound.def`、`equipsound.def`、`eula.txt`、`felf-e.tbl`、`felf-k.tbl`、`fknight-e.tbl`、`fknight-k.tbl`、`flower.tbl`、`help-e.tbl`、`help-k.tbl`、`illusionist-c.tbl`、`Intelligence-e.tbl`、`intro-c.tbl`、`intro-e.tbl`、`intro-h.tbl`、`intro-j.tbl`、`intro-k.tbl`、`ItemDesc-c.tbl`、`ItemDesc-e.tbl`、`itemdesc-h.tbl`、`ItemDesc-j.tbl`、`ItemDesc-k.tbl`、`itemextra.tbl`、`itemimportant.tbl`、`itemquest.tbl`、`knight-c.tbl`、`knight-e.tbl`、`knight-h.tbl`、`knight-j.tbl`、`knight-k.tbl`、`LevelUp_BS.tbl`、`LevelUpHelp.tbl`、`License-c.txt`、`License-e.txt`、`License-h.txt`、`License-j.txt`、`License-k.txt`、`lvupdarkelf-c.tbl`、`lvupdarkelf-e.tbl`、`lvupdarkelf-h.tbl`、`lvupdarkelf-j.tbl`、`lvupdarkelf-k.tbl`、`lvupdknight-c.tbl`、`lvupelf-c.tbl`、`lvupelf-e.tbl`、`lvupelf-h.tbl`、`lvupelf-j.tbl`、`lvupelf-k.tbl`、`lvupillusion-c.tbl`、`lvupknight-c.tbl`、`lvupknight-e.tbl`、`lvupknight-h.tbl`、`lvupknight-j.tbl`、`lvupknight-k.tbl`、`lvupprice-c.tbl`、`lvupprice-h.tbl`、`lvupprince-c.tbl`、`lvupprince-e.tbl`、`lvupprince-h.tbl`、`lvupprince-j.tbl`、`lvupprince-k.tbl`、`lvupwizard-c.tbl`、`lvupwizard-e.tbl`、`lvupwizard-h.tbl`、`lvupwizard-j.tbl`、`lvupwizard-k.tbl`、`MagicDoll.tbl`、`MakeInfo.tbl`、`mapDesc-c.tbl`、`mercenary.tbl`、`mercenary-c.tbl`、`music.def`、`newaccount-c.tbl`、`newaccount-e.tbl`、`newaccount-h.tbl`、`newaccount-j.tbl`、`newaccount-k.tbl`、`newaccountk-c.tbl`、`newaccountk-e.tbl`、`newaccountk-h.tbl`、`newaccountk-j.tbl`、`newaccountk-k.tbl`、`notice.txt`、`notice_BS.tbl`、`npcexname-c.tbl`、`npcinhouse-c.tbl`、`NpcTalkRange-c.tbl`、`ntexpet.tbl`、`ntexpet-c.tbl`、`obscene-c.tbl`、`Obscene-e.tbl`、`obscene-h.tbl`、`obscene-j.tbl`、`obscene-k.tbl`、`otpintro-c.tbl`、`otpintro-e.tbl`、`otpintro-h.tbl`、`otpintro-j.tbl`、`otpintro-k.tbl`、`parameter-c.tbl`、`parameter-e.tbl`、`parameter-h.tbl`、`parameter-j.tbl`、`parameter-k.tbl`、`PledBlockName-c.tbl`、`polymorph.tbl`、`prince-c.tbl`、`prince-e.tbl`、`prince-h.tbl`、`prince-j.tbl`、`prince-k.tbl`、`princess-e.tbl`、`princess-k.tbl`、`PShopPoly.tbl`、`quiz-c.tbl`、`quiz-e.tbl`、`quiz-h.tbl`、`quiz-j.tbl`、`quiz-k.tbl`、`recipe.tbl`、`RegisterSSN-c.tbl`、`RegisterSSN-e.tbl`、`RegisterSSN-h.tbl`、`RegisterSSN-j.tbl`、`RegisterSSN-k.tbl`、`solvent.tbl`、`solvent-c.tbl`、`spelldesc-c.tbl`、`SpellDesc-e.tbl`、`spelldesc-h.tbl`、`SpellDesc-j.tbl`、`SpellDesc-k.tbl`、`spelldur.tbl`、`SpellExDesc-c.tbl`、`strength-e.tbl`、`string-c.tbl`、`string-e.tbl`、`string-h.tbl`、`string-j.tbl`、`string-k.tbl`、`telBook.tbl`、`telBook0.tbl`、`telBook1.tbl`、`telBook2.tbl`、`telBook2-c.tbl`、`telBook4.tbl`、`telBook4-c.tbl`、`theif-c.tbl`、`theif-h.tbl`、`thief-c.tbl`、`thief-e.tbl`、`thief-h.tbl`、`thief-j.tbl`、`thief-k.tbl`、`todaystip-c.tbl`、`todaystip-e.tbl`、`todaystip-h.tbl`、`todaystip-j.tbl`、`todaystip-k.tbl`、`uinform_u14-c.tbl`、`uinformBFR-c.tbl`、`uinform-c.tbl`、`uinformdate-c.tbl`、`untransaction.tbl`、`untransfer.tbl`、`untransferble.tbl`、`untransferble2.tbl`、`Wisdom-e.tbl`、`wizard-c.tbl`、`wizard-e.tbl`、`wizard-h.tbl`、`wizard-j.tbl`、`wizard-k.tbl`、`zone.tbl`、`zone2.tbl`、`zone2-c.tbl`、`zone2-e.tbl`、`zone2-h.tbl`、`zone2-j.tbl`、`zone3-c.tbl`、`zone-c.tbl`、`zone-h.tbl`

**`LiTo\LTtext.idx`（96 個）**：`envsound.def`、`equipsound.def`、`list-lcc.txt`、`LTchannel-c.tbl`、`LTchannel-h.tbl`、`LTchannel-j.tbl`、`LTchannel-k.tbl`、`LTcredits-c.tbl`、`LTcredits-e.tbl`、`LTcredits-h.tbl`、`LTcredits-k.tbl`、`LTdesc-c.tbl`、`LTdesc-k.tbl`、`LTenvsound.def`、`LTequipsound.def`、`LTintro-c.tbl`、`LTintro-h.tbl`、`LTintro-j.tbl`、`LTintro-k.tbl`、`LTItemSet0-0-c.tbl`、`LTItemSet0-0-h.tbl`、`LTItemSet0-0-j.tbl`、`LTItemSet0-0-k.tbl`、`LTItemSet0-1-c.tbl`、`LTItemSet0-1-h.tbl`、`LTItemSet0-1-j.tbl`、`LTItemSet0-1-k.tbl`、`LTItemSet0-2-c.tbl`、`LTItemSet0-2-h.tbl`、`LTItemSet0-2-j.tbl`、`LTItemSet0-2-k.tbl`、`LTItemSet1-0-c.tbl`、`LTItemSet1-0-h.tbl`、`LTItemSet1-0-j.tbl`、`LTItemSet1-0-k.tbl`、`LTItemSet1-1-c.tbl`、`LTItemSet1-1-h.tbl`、`LTItemSet1-1-j.tbl`、`LTItemSet1-1-k.tbl`、`LTItemSet1-2-c.tbl`、`LTItemSet1-2-h.tbl`、`LTItemSet1-2-j.tbl`、`LTItemSet1-2-k.tbl`、`LTItemSet2-0-c.tbl`、`LTItemSet2-0-h.tbl`、`LTItemSet2-0-j.tbl`、`LTItemSet2-0-k.tbl`、`LTItemSet2-1-c.tbl`、`LTItemSet2-1-h.tbl`、`LTItemSet2-1-j.tbl`、`LTItemSet2-1-k.tbl`、`LTItemSet3-0-c.tbl`、`LTItemSet3-0-h.tbl`、`LTItemSet3-0-j.tbl`、`LTItemSet3-0-k.tbl`、`LTItemSet3-1-c.tbl`、`LTItemSet3-1-h.tbl`、`LTItemSet3-1-j.tbl`、`LTItemSet3-1-k.tbl`、`LTItemSet4-0-c.tbl`、`LTItemSet4-0-h.tbl`、`LTItemSet4-0-j.tbl`、`LTItemSet4-0-k.tbl`、`LTItemSet4-1-c.tbl`、`LTItemSet4-1-h.tbl`、`LTItemSet4-1-j.tbl`、`LTItemSet4-1-k.tbl`、`LTItemSet4-2-c.tbl`、`LTItemSet4-2-h.tbl`、`LTItemSet4-2-j.tbl`、`LTItemSet4-2-k.tbl`、`LTmapstr-c.tbl`、`LTmapstr-h.tbl`、`LTmapstr-h.txt`、`LTmapstr-j.tbl`、`LTmapstr-k.tbl`、`LTmusic.def`、`LTpitem-c.tbl`、`LTpitem-h.tbl`、`LTpitem-j.tbl`、`LTpitem-k.tbl`、`LTserver-c.tbl`、`LTserver-h.tbl`、`LTserver-j.tbl`、`LTserver-k.tbl`、`LTSpellDesc-c.tbl`、`LTSpellDesc-k.tbl`、`LTspelldur.tbl`、`LTstring-c.tbl`、`LTstring-h.tbl`、`LTstring-j.tbl`、`LTstring-k.tbl`、`LTzone.tbl`、`LTzone-c.tbl`、`music.def`、`zone2-c.tbl`

**`Tile.idx`（66 個）**：`Actions.xml`、`ActionUI.xml`、`ActionUI-c.xml`、`BookmarkMemUI.xml`、`BookmarkUI.xml`、`CollisionZone.xml`、`CutScene.xml`、`effectlist.xml`、`effectlist2.xml`、`ExcludeUI.xml`、`ExtMapAttr.xml`、`FightPlaceUI.xml`、`FriendUI.xml`、`GlobalEffect.xml`、`GlobalEffect86.xml`、`hpasslvlist.xml`、`lineage1.xml`、`lineage10.xml`、`lineage12.xml`、`lineage13.xml`、`lineage2.xml`、`lineage3.xml`、`lineage4.xml`、`lineage5.xml`、`lineage6.xml`、`lineage7.xml`、`lineage9.xml`、`LinHelperUI.xml`、`list.enx`、`MailExcludeUI.xml`、`map_config.xml`、`MapSystem.xml`、`MapSystem86.xml`、`MapSystem-c.xml`、`MatchMakingUI.xml`、`music.xml`、`PersonalShopUI.xml`、`PledgeFocusUI.xml`、`PledgeUI.xml`、`polymorphList.xml`、`Polymorphlist2.xml`、`PolymorphUI.xml`、`QuestInfo.xml`、`ReNStatusWinUI.xml`、`ReNStatusWinUI-c.xml`、`SmithUI.xml`、`SPolymorphList.xml`、`SPolymorphUI.xml`、`StoryBook.xml`、`StoryBookUI.xml`、`StoryPaperUI.xml`、`summonList.xml`、`SummonUI.xml`、`SurfList.xml`、`uiextra.xml`、`uiextra86.xml`、`uiextra86-c.xml`、`uimagicitem.xml`、`uimenu.xml`、`uishoplev.xml`、`uistatus.xml`、`uistatus86.xml`、`uistatus86-c.xml`、`UIStatusWin.xml`、`UserObtainUI.xml`、`UserPlayUI.xml`

**`Sprite.idx`（7 個）**：`desc-k.tbl`、`itemextra.tbl`、`list.ico`、`list.lst`、`Source.lmp`、`SpellDesc-k.tbl`、`SpellIcon.idx`
