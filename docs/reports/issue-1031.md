# Issue #1031 — T446 來源閉合：規則表

日期：2026-11-13；執行者：本機 Claude（claude-local）；分支：`task/1031`；階段：resource。只新增文件與查詢腳本，沒有改產品程式。

## 結論

詳細內容在 [`docs/reference/asset-recipes/rule-tables.md`](../reference/asset-recipes/rule-tables.md)，每份表一節。

| 表 | 結論 | 重點 |
|---|---|---|
| `dialogs/skills.json` | **client-only 來源** | 名稱取 `Text.pak` 的 `spelldesc-c.tbl`（索引＝id − 1）。等級不用表：原版自己算 `((id−1)>>3)+1`，基線 183 筆全部符合 |
| `magic/catalog.json` | **可改為不需要此表** | 名稱與 MP 取 `spelldesc-c.tbl`；動作取 `SkillCast.Wire`；對象取 `SpellExDesc-c.tbl` 的「對象：」欄；冷卻用原版預設 10 ms；`effect` 沒有程式在讀。技能 1／3／4／8 四筆逐欄和基線相同 |
| `OriginalExpTable.cs` | **blocked（已知限制）** | 缺每級累計 EXP 門檻。原版寫死在執行檔 `.data`，不在任何 pak；伺服器 S113 只送等級與 EXP 低 32 位。而且客戶端的表和產品的表本來就不同 |
| `ground-catalog.json` 模板列 | **可改為不需要此表，但要主控裁定** | 客戶端只有 `desc-c.tbl`（`$N` → 名稱），沒有圖示、地面 gfx、use type、可疊加的對照。這 7 列其實是引擎自己加的可丟棄白名單；拿掉白名單就不需要。不同意拿掉就是 blocked |

## 做了什麼

- 讀 `CLAUDE.md`、`docs/REPORT.md`、#1027 報告、#97 設計的前置 2、#102 報告「其餘 10 個 D 檔」。
- 逐表列出產品讀取點與實際用到的欄位（只讀 `engine/source`）。
- 用 PakViewer CLI 列出 `0-客戶端` 全部 24 個 idx（308,323 個檔名），用 14 個關鍵字搜檔名；解出 398 個非圖、非 HTML 的檔案，搜 EXP 門檻數字、技能名稱、伺服器模板編號。
- 用 Python 把客戶端表和主目錄 `engine/assets` 的基線比對（`skills.json` 807 筆、`catalog.json` 4 筆）。
- 靜態分析 `TW1608262200.bin` 與 LiTo 的 OEP 快照（pefile＋Capstone）：找到 EXP 門檻表、門檻函式與百分比函式，標 VA／File Offset。
- 寫 `rule-tables.md`，腳本放 `docs/reference/rea-scripts/t1031/`。

## 已驗證

- **技能名稱**：`spelldesc-c.tbl`（`Text.idx` 第 32427 筆，File Offset `0x159B8EE`，SHA-256 `387c4a9d…e2e3d30ab9`）183 行。id 1–220 和基線比：177 筆名稱相同、6 筆不同（30、111、115、119、120、213）、36 筆只有基線有而且全是 `none` 佔位列。
- **技能等級**：基線 id 1–220 等級不是 0 的 183 筆全部等於 `((id−1)>>3)+1`。原版算式的出處是既有的 `trade-window-and-projectiles.md` 1.1 節（`sub_931FF0`）。
- **`magic/catalog.json`**：產品只讀 `name`、`mp`、`action`、`reuseMs`、`target`；`effect` 讀進來後沒有人用。四筆的這五個欄位都能由替代來源得到相同的值。
- **EXP**：
  - TW16 門檻函式 `sub_EE1260`（VA `0xEE1260`／FO `0xAE0660`）、表 A（VA `0x138B958`／FO `0xF8A158`，51 項）、表 B（VA `0x138BAF0`／FO `0xF8A2F0`，65 項）、百分比函式 `sub_EE12E0`（VA `0xEE12E0`／FO `0xAE06E0`）：`min(99.9999, (EXP − 本級門檻) × 100 ÷ 級距)`，51 級以上每級加 36,065,092。
  - LiTo 快照的門檻函式在 VA `0x4CDDC0`，表在 VA `0x6360F8`（51 項）。
  - 產品的表和 TW16 表 A 有 42 項不同（8 級起多 16–20）、和 3.81 表有 46 項不同（1–4 級是 20、45、80、625）。
  - 對 `LiTo.bin` 原檔搜三種表頭都是 0 筆（有 Themida 殼）。
- **ground-catalog**：7 個 `nameToken` 在 `desc-c.tbl` 都查得到；伺服器模板編號在 398 個檔案裡 0 筆。產品只在 `GroundCatalog.CanDrop` 用模板列做判斷。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：`status: pass`，errors 0。

## 推測

- 3.81 的 LiTo 客戶端讀的是 `LTtext.pak` 的 `LTSpellDesc-c.tbl`（只看到字串與引用點）。這份只有 147 行、沒有龍騎士與幻術師，所以建議用 `Text.pak` 那一份。
- 3.81 的 EXP 百分比算式與 TW16 相同（3.81 只定位到門檻函式）。
- `spelldesc-c.tbl` 與 `SpellExDesc-c.tbl` 的 MP 有 2 筆不一致（204、214），以前者為準。

## 待驗證（這一輪沒做）

- 沒有建置、沒有執行產品、沒有真機。文件裡的「產品改法」都還沒實作，交 T462、T464。
- `TextArchive` 是否能直接讀 `spelldesc-c.tbl`／`SpellExDesc-c.tbl`：只確認 PakViewer 解得出來。
- 3.81 原版怎麼決定施法前要不要選目標：沒查。「對象：施法者 → 只對自己」是本文提出的自主規則，只有四筆有基線可比。
- 原版送丟棄封包前的本機檢查：沒有反組譯。
- `dword_13CE514` bit 30（選 EXP 表 A 或 B）由誰設定：沒追。
- 基線 `skills.json` 裡 id 大於 256 的 588 筆沒有比對，產品讀不到它們。
- 背景執行的一個檔案搜尋指令（在 `D:\lineage開發` 遞迴找 LiTo 快照）跑到時間上限被停掉；快照路徑後來從 `followups-12.md` 取得，不影響結果。
- CI 結果以 PR 為準，push 當下尚未取得。

## 需要主控決定

1. **EXP 門檻表（blocked）**：`rule-tables.md` 3.5 節的三個選項——A 保留例外、B 拿掉表（EXP% 空白）、C 接受使用者自備的門檻檔。T464 的範圍取決於這個決定。
2. **ground-catalog 的可丟棄白名單**：同意拿掉（4.4 節），或維持 7 列並列為已知限制。拿掉是行為變更：任何物品都能送出丟棄封包，由伺服器裁定。
3. 知悉即可：技能名稱改用客戶端表後，6 個名稱會和現在不同（1.3 節）。

## 產出路徑

- `docs/reference/asset-recipes/rule-tables.md`
- `docs/reference/rea-scripts/t1031/`：`extract.ps1`、`skills_cmp.py`、`spellex.py`、`content_scan.py`、`exp_tables.py`、`qdump.py`、`xref_dis.py`、`callers.py`、`strref.py`、`peek.py`
- `docs/reports/issue-1031.md`
- `scratch/pak-index/`、`scratch/t1031/`：解出的客戶端檔案與清單，不進 git

## 給下一棒的提示

- **T462（skills／magic）**：兩張表都不用準備器產生。`spelldesc-c.tbl` 每行 `<索引> <名稱> (<MP>/<HP>[/<材料數>])`，索引 107 的括號內是 `HP-1/MP-1`，解析名稱時取最後一個 ` (` 之前。`SpellExDesc-c.tbl` 用 `^` 分段、`\fX` 是色碼，「對象：」後面可能直接接色碼。`MagicSpells.Get` 已經有缺檔備援，改動集中在 `MagicSpells.cs` 與 `NpcUi.cs:76`。
- **T464（EXP 表與 ground-catalog）**：先等主控裁定上面兩件事再動工。ground-catalog 的 `sprite` pin 是客戶端量出來的，可以留或交給準備器；要拿掉的是 `templateId`、`source`、`name` 等伺服器欄位與 `GroundCatalog.cs:21` 的對應檢查。用到 `GroundCatalog.Rows` 的測試有 `ground-tests`、`pickup-tests`、`npc-tests`、`demo-tests`、`PointerFlowTests.cs:34`。
- **T452（已知限制清單）**：收 `rule-tables.md` 第 5 節的 L1、L2。
- 重跑方法（在 repo 根目錄）：`pak_index.ps1` → `t1031/extract.ps1` → `py -3 -I -X utf8 docs/reference/rea-scripts/t1031/<腳本> <參數>`，各腳本開頭有用法。`.ps1` 要存成帶 BOM 的 UTF-8，否則 Windows PowerShell 5.1 讀中文路徑會解析失敗。
- 沒有改 `docs/reference/INDEX.md`：T444–T450 同時在跑，避免衝突；請 T452 一次補上 `asset-recipes/` 的索引。
- 本文沒有讀 SQL／JAR／DB。基線 `magic/catalog.json` 本身嵌著 `sqlRow` 字串，只用了旁邊七個已解析的欄位。
