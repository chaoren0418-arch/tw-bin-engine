# #232／T118：龍騎士基本技能（前置阻塞）

日期：2026-11-07；分支：`task/232`；檢查基底：`89f5adf04111f2385191369d76108e991f14bc57`。

## 結論與做了什麼

**本輪未完成技能實作，不能結案。** T100 已合併，但任務指定必須使用的 T101、T108 尚未合併，職業註冊契約也尚未提供。僅提交本報告，供調度器補齊前置後重新派工；草稿 PR 不使用關閉 Issue 的關鍵字。

已讀 `AGENTS.md`、`docs/REPORT.md`、`TASKS.md`、`docs/design/m6-contract.md`、`docs/protocol/skills.md` 與 `docs/reference/server-protocol.md`、`server-rules.md` 的技能相關條目。唯讀檢查本地／遠端前置、現有施法與狀態流程，以及伺服器技能常數；未改 `docs/REPORT.md`、引擎來源或任何 lock。

## 已驗證的阻塞證據

| 項目 | 實際結果 | 影響 |
|---|---|---|
| T100／#187／PR #205 | 已於 2026-11-06 22:20:17 UTC 合併；merge `2bdf2dc9da13327905b8289b23b365edd4d3e076` 是本分支祖先，`git merge-base --is-ancestor` 退出 0 | 明列的 T100 前置已滿足 |
| T101／#188／PR #202 | 查詢時 open、未合併；head `38ab63849aa64d78dbf6d1f3866093c95745c9a5`；本分支及遠端 main 缺 `engine/source/AttackSkillPackets.cs` | 不能接入指定的多種 action／gfx 與 S42 效果介面 |
| T108／#196／PR #220 | 查詢時 open、未合併；head `18c57f275b5b4a9fed7ebc50179c57f5eb37fdbf`；本分支及遠端 main 缺 `engine/source/StatusIcons.cs` | 不能接入指定的狀態圖示與 HUD 介面 |
| 職業註冊 | `SkillCast.cs`、`MagicUi.cs` 均無 `ClassSkills registrations` 標記，也無職業模組的註冊 API；repo 無既有 `ClassSkills` 模組／測試入口 | 任務允許建立標記，但標記本身不能提供註冊契約；需共用基礎先實作或主控明確分配給某張任務 |

`SkillCast.cs:60` 的準備流程目前直接使用通用 Wire 與 `World.MagicTargetsOwn`；`MagicUi.cs:25` 的初始化直接繫結通用送出入口。`Magic.cs` 的 S30 法術只接受 action 18、gfx 167、flight 0，S55 限定既有 gfx；`MagicStatus.cs` 的 S216／S250 主要只處理技能 3、8。只在兩個檔案各加一行註冊，無法代替缺少的接收模組。

未把其他任務未合併的來源複製或 cherry-pick 進本分支，也未建立同名替身介面、以 reflection 繞過缺少的依賴，或以合成效果冒充完成。

來源 SHA-256（本 worktree 實際位元組）：

| 路徑 | SHA-256 |
|---|---|
| `engine/source/SkillCast.cs` | `46c38d4dbdb7c2d4e96ee67abcf7d38ce449a4c681e1fd5f9610fb11fce50676` |
| `engine/source/MagicUi.cs` | `380e595f62533f3ac1ae18cf2152e31a5199e0327abc80fd84e7b96d5d0e4fe5` |
| `D:/lineage開發/1-伺服器/src/com/lineage/server/model/skill/L1SkillId.java` | `5b3dafdac87f504efdd11f70f207999badd7ae8f35b4370b513d4bdcbd678a14` |

## 已整理的實作與測試依據

以下是既有協定表／來源證據，不是新功能驗收：

| ID／技能 | C6 row／column | 尾段 | 後續必測 |
|---|---|---|---|
| 181 龍之護鎧 | 22／4 | D | 自己目標、權威圖示新增／刷新／解除／到期 |
| 182 燃燒擊砍 | 22／5 | D | 核對攻擊觸發方式與消耗時機，不能因送出 C6 就本地授予命中或傷害 |
| 183 護衛毀滅 | 22／6 | D | 核對指定對象條件、成功／失敗回報與狀態；D 格式不能用來推論可對自己施放 |
| 184 岩漿噴吐 | 22／7 | DHH | 選取物件並使用送出當下 X/Y；等待期間移動、死亡、離開 AOI 時取消；驗證效果回包 |

共同前綴是 `06,row,column`，H/D 為 little endian，完整請求補零至 8-byte 邊界；前三者 8 bytes，184 為 16 bytes。技能名稱與 ID 已由 `L1SkillId.java:771–788` 核對；183 常數拼字為 `GUARD_BRAKE`。學習門檻 Lv15 依 `docs/protocol/skills.md` U8，不表示新角色已學。目標資格、gfx、持續時間仍需逐技核對 handler／模板與實際權威回包，不能由本表推定。

真正送出須走 `LoginConnection.CastSelectedMagic`，保留已學、角色狀態、cadence 與等待期間失效檢查。HP／MP、命中、傷害與 buff 依原服回包；遵守 server-rules R17–R20，不能自行扣血、扣魔或授予狀態。

## 怎麼驗證／未驗證什麼

- 已驗證：本地 T100 ancestry、所需檔案／註冊入口缺少，以及 GitHub 前置 PR 狀態。GitHub CLI 的環境 token 回傳 401；僅在子程序移除 `GH_TOKEN`／`GITHUB_TOKEN` 覆寫後，既有 gh 登入可正常讀取 repo，未改任何持久憑證設定。
- 本輪為文件變更，不需 `--update-lock`；未以重新建置無修改的產品宣稱 T118 完成。
- 使用本機 Python 3.12 絕對路徑執行 `-B -X utf8 engine/_integration/dev/rebuild.py`：173 source／182 pinned 通過，`writes: false`，退出 0。
- `-B -X utf8 engine/tools/check-no-game-assets.py`：3244 個追蹤檔、4 個 embedded resource 宣告、0 errors，退出 0；已包含暫存的本報告。`git diff --cached --check` 通過。
- 待驗證：四技能產品實作、指定 `DragonKnight.cs` 離線測試與入口、Windows 建置、T118 效果／狀態驗收。沒有建立必定失敗或不接產品的占位測試。
- 未啟動遊戲、伺服器或 DB，未連正式服務；未複製遊戲資源、Java 原文，未修改原專案、封存、硬連結，未執行任何刪除。真機驗收仍交 M6 smoke。

## 產出路徑

- `docs/reports/issue-232.md`：本輪唯一追蹤變更。
- `scratch/issue-232/`：本機檢查輸出與 PR／留言 UTF-8 內容；不提交。

## 給下一棒的提示

1. **主控／調度器**：建議將 #232 標為 `task:blocked`，追加 #188（PR #202）、#196（PR #220）及職業註冊契約為前置。本輪未自行改 Issue 狀態。
2. **共用介面負責人**：明定目標解析／資格驗證、metadata 顯示、UI 更新與測試入口的註冊方式，使七張職業任務確實可各加一行接入。若改由本任務建立基礎，需由主控調整可修改範圍及其他職業依賴。
3. **codex-local**：上述合併並由調度器同步本 worktree 後，實作 `engine/source/ClassSkills/DragonKnightSkills.cs`、`engine/_integration/dev/class-skills-tests/DragonKnight.cs`，接上真實入口；先更新 dev lock，再建置、測試、資源拒收檢查與 CI。不要僅因 T100 已合併就視為效果／狀態前置齊備。
4. **Claude／使用者**：草稿 PR 僅供審查阻塞報告，不能據此結案 #232；功能完成後才交 dot 做 M6 smoke。
