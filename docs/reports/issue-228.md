# Issue #228／T114：騎士基本技能（第 1 次，阻塞）

## 結論與做了什麼

本輪完成前置與介面核查，**未完成技能實作，不可作為功能驗收通過**。工作分支維持 `task/228`，檢查基底為 `f8cca46f9c0702057d9c029c45ee4d25388b32ef`；未切換分支、未改產品來源、dev lock 或 `docs/REPORT.md`。

T100 已合併：本機歷史含 PR #205 的 merge commit `2bdf2dc`，且 `git merge-base --is-ancestor 2bdf2dc HEAD` 退出 0，`engine/source/SkillCast.cs` 存在。

實際阻塞是任務要求使用的另外兩個模組尚未合併。2026-11-07 查核 GitHub 時：

| 必要模組 | 上游狀態與證據 |
|---|---|
| T101／`AttackSkillPackets.cs` | [PR #202](https://github.com/chaoren0418-arch/tw-bin-engine/pull/202) 為 open、merged=false；head `38ab63849aa64d78dbf6d1f3866093c95745c9a5` |
| T108／`StatusIcons.cs` | [PR #220](https://github.com/chaoren0418-arch/tw-bin-engine/pull/220) 為 open、merged=false；head `18c57f275b5b4a9fed7ebc50179c57f5eb37fdbf` |

兩個檔案在本 worktree、本機原專案來源、`origin/main` 樹均不存在；GitHub connector 讀取遠端 `main` 的相應路徑亦均回 404。GitHub Issue #228 本文仍要求沿用這兩個介面，不能改它們內部。未擅自合併尚未合併的 PR，亦未用替代接收器或空殼介面宣稱完成。

另查 `engine/source/*.cs` 沒有 `ClassSkills registrations`、職業註冊 API 或對應呼叫。標記本身可依任務新增，但共用技能 metadata、送出規則與 UI 如何共用同一註冊契約尚需明確化。既有 `SkillCast.Request` 是編碼器，並不提供職業註冊；`MagicSpells.Get` 由外部 catalog 或一般回退產生 metadata。

## 已驗證的規則與接手注意事項

- 已讀 `AGENTS.md`、`docs/REPORT.md`、`TASKS.md`、`docs/design/m6-contract.md`、`docs/protocol/skills.md`，並檢索 `docs/reference/server-protocol.md` 與 `server-rules.md`。
- U8 的騎士 Lv50 清單為 87、88、90、91；89 尖刺盔甲有常數，但未列於該學習方法，不能自動授予或宣稱為基本已學技能。
- 87、88、90、91 由 T100 `Wire` 歸入 D 尾段。共同前綴為 C6、零基技能 row、column；D 為 little endian，補齊到 8 bytes。格式通過不等於目標合法或施法成功。
- 唯讀核對伺服器 `L1SkillUse.java:2291` 起：87 使用近戰揮擊呈現；不能沿用一般 D 回退的 action19 當作已核定動畫。`SHOCK_STUN.java` 的成功效果建立由伺服器處理，PC 暈眩／解除使用 `S_Paralysis`；客戶端不得因 C6 已送出就預設暈眩成功。
- `SOLID_CARRIAGE.java:24` 起：90 可用條件包含盾，且依伺服器 `SOLID_CARRIAGE_MODE` 可能接受臂甲。未讀部署配置；UI 不應憑猜測硬拒絕全部臂甲。
- 雙手武器的精確可用條件、88／91 的狀態與特效 metadata 尚未完整核對，列為待驗證，不能將一般遊戲常識作為本服規則。

上述伺服器檔案只讀；未複製 Java、SQL、圖像或音效。來源識別如下（根目錄 `D:/lineage開發/1-伺服器/src/com/lineage/server/model/skill/`；均為來源行號，非 File Offset／RVA／VA）：

| 檔案 | SHA-256 |
|---|---|
| `L1SkillUse.java` | `2fad1a69076c29704c8ea9a41361aff357f22b0e895223a6bb7b943a4879015d` |
| `skillmode/SHOCK_STUN.java` | `a75f93a2b7ce36d8e6e5ccb0b08ef71485b854bb3251510d35866803344b1ebd` |
| `skillmode/SOLID_CARRIAGE.java` | `56675b9adaa7e3f72df8df9331d353551b5b98c433dfc46fea820cb30bbd5d89` |

## 怎麼驗證／未驗證什麼

已核對本機 Git 祖先、檔案與註冊符號，以及遠端 PR 狀態。純文件變更，未執行產品建置或更新 lock。未建立 `KnightSkills.cs`、`class-skills-tests/Knight.cs` 或測試入口，四技能送出／目標／效果／狀態測試皆未完成。未啟動遊戲、伺服器或 DB，未做真機驗收。

`check-no-game-assets.py` 通過（trackedFiles=3241、errors=0），`git diff --check` 通過。沙箱內 `gh` 曾回 401、Git HTTPS 曾回 `SEC_E_NO_CREDENTIALS`；使用本機既有認證於沙箱外讀取 PR #205 已成功，確認 mergedAt=`2026-11-06T22:20:17Z`，不是上游未合併問題。後續提交檢查以 PR checks 為準，CI 通過亦不能代表本職業功能已完成。

## 產出路徑／給下一棒的提示

產出：`docs/reports/issue-228.md`。此文件是阻塞交接，不是 T114 完工報告。

請主控／調度器將 #228 暫列 `task:blocked`，補上 T101（#188／PR #202）與 T108（#196／PR #220）依賴；兩者合併並同步此 worktree 後再派工。請同時協調七職業共用的註冊契約，確保每職業僅需在 `SkillCast.cs`、`MagicUi.cs` 各加一行即可接入 metadata、目標規則及 UI。這是主控的整合作業，不需使用者進行真機或 DB 操作。

後續執行器完成本職業來源與持久化離線測試、接上測試入口，先 `--update-lock` 再建置，通過資源檢查與 CI 後才可宣告 code 完成；M6 smoke 再由 dot 依既有授權驗證。
