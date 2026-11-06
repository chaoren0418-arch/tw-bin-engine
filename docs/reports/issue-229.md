# Issue #229／T115：妖精基本技能（依賴阻塞）

## 結論與做了什麼

2026-11-07 查核：**T115 尚未完成**。本輪僅提交依賴證據、部分伺服器規則核對與交接；不可據此關閉 #229 或宣稱技能可用。

- 工作分支維持 `task/229`，基底 `d43a6692e3ae538cb46d18b2919b39727ef60ec9`；遠端 `main` 查核時亦為此 SHA。未 checkout、建立分支、合併其他任務或移除 worktree。
- 已讀根目錄 `AGENTS.md`、`docs/REPORT.md`、`TASKS.md`、M6 契約、技能索引與伺服器協定／規則表。未修改 `docs/REPORT.md`。
- T100／PR #205 已於 `2026-11-06T22:20:17Z` 合併，merge commit `2bdf2dc9da13327905b8289b23b365edd4d3e076` 是目前 HEAD 的祖先；`SkillCast.cs` 存在。因此阻塞不是 T100。

## 已驗證的依賴缺口

| 任務要求 | 查核結果 | 需要的交接 |
|---|---|---|
| 使用 T101 的 `AttackSkillPackets.cs` | 本 worktree 未含該檔；[PR #202](https://github.com/chaoren0418-arch/tw-bin-engine/pull/202) 為 OPEN，未合併 | 主控完成 #188 審查／合併並同步基底 |
| 使用 T108 的 `StatusIcons.cs` | 本 worktree 未含該檔；[PR #220](https://github.com/chaoren0418-arch/tw-bin-engine/pull/220) 為 OPEN，未合併 | 主控完成 #196 審查／合併並同步基底 |
| 共用檔只加一行註冊 | `SkillCast.cs`、`MagicUi.cs` 尚無 `ClassSkills registrations` 區，也沒有共用註冊 API；T100 目前直接執行通用目標邏輯 | 七職業需先協調可供一行掛接的契約與初始化位置 |
| `class-skills-tests/Elf.cs` 接上入口 | 目前沒有 `class-skills-tests` 入口；已有 `skill-cast-tests` 通用測試 | 依賴到位後建立／共用職業測試入口並接入 Elf 測試 |

註冊區標記及測試入口本可在任務內新增，不把「缺少註解或測試目錄」單獨當作不可實作的理由；實際阻塞是指定沿用的回包／HUD 模組尚未合併。沒有從其他分支複製它們、重做它們的內部，或提交引用不存在型別的半成品。

唯讀查看本機 `origin/task/188`、`origin/task/196` 的介面可知相關程式正在獨立任務中；這些分支快照不是已核准的 main 契約。相鄰職業的草稿 PR #249、#250 亦記錄同一依賴缺口。

## 部分協定核對（已驗證來源，未驗證部署）

以下僅摘要 metadata 與來源行為，沒有複製伺服器程式碼或素材。Java 行號不是 File Offset／RVA／VA。

| 技能 | 送出格式與後續實作注意 |
|---|---|
| 129 魔法防禦 | 通用 C6 尾段 D；`L1SkillUse.java:3552` 的 `RESIST_MAGIC` 分支更新 MR 並送 `S_SPMR`。不可因請求送出便在本地加 MR／buff。 |
| 130 心靈轉換 | C6 尾段 D；`skillmode/BODY_TO_MIND.java` 的玩家 `start` 呼叫 MP 調整 +2。這不代表完整消耗規則，HP／MP 消耗仍經共用技能與模板路徑；HUD 等權威回包，不能硬寫常見版本的轉換數值。 |
| 131 世界樹的呼喚 | C6 尾段 D，不是 5／69 的 HD 書籤格式；常數原文拼作 `TELEPORT_TO_MATHER`（`L1SkillId.java:574`）。`L1SkillUse.java:1213–1220` 將其視為傳送技能並處理解鎖；完整目的地、限制及世界狀態回包仍待核對。 |
| 137 淨化精神 | C6 尾段 D；`L1SkillUse.java:3561` 使用 buff 能力更新及 base MR 重算；不能用目前選技自行授予能力。 |
| 138 屬性防禦 | C6 尾段 D；`L1SkillUse.java:3572` 的 `RESIST_ELEMENTAL` 分支對四抗各 +10，並送 `S_OwnCharAttrDef`。不要誤套另一個 `ELEMENTAL_PROTECTION` 的屬性分支。 |
| 132 三重矢 | C6 尾段 DHH（物件 ID、X、Y），通用補齊後 16 bytes；其餘上述 D 格式補齊後 8 bytes。`skillmode/TRIPLE_ARROW.java` 玩家路徑檢查變身支援後做三次物理攻擊並送自身 `S_SkillSound` gfx 4394；不能合成三次命中、扣箭或扣目標 HP。 |

屬性遮罩：`L1SkillUse.java:1856–1859` 對有屬性的精靈魔法以玩家 mask 與技能 attr 的位元交集判斷，不能以單一 enum 等值比較。`3589–3603` 明載 1 地／2 火／4 水／8 風，可組合；那段四系各 +50 是另一技能 `ELEMENTAL_PROTECTION`，不是本張 138。客戶端取得玩家 mask 的可靠回包來源、技能 target/template metadata 與完整效果映射仍待實作時核對，不從四抗數值反推已選屬性。

三重矢的附著特效還需要確認 T105／PR #214 合併後的 S55 路徑，或由主控明確安排擴充；目前 `MagicSpells.AttachedEffect` 白名單不含 4394。這是待解決的接收缺口，不代表本輪已實作特效。

唯讀來源根：`D:/lineage開發/1-伺服器/src/com/lineage/server/`。

| 相對來源路徑 | SHA-256 |
|---|---|
| `model/skill/L1SkillId.java` | `5b3dafdac87f504efdd11f70f207999badd7ae8f35b4370b513d4bdcbd678a14` |
| `model/skill/L1SkillUse.java` | `2fad1a69076c29704c8ea9a41361aff357f22b0e895223a6bb7b943a4879015d` |
| `model/skill/skillmode/BODY_TO_MIND.java` | `0ea87f1730598a2ede518c460f635363c54b4abfd89de982929e1875335d4476` |
| `model/skill/skillmode/TRIPLE_ARROW.java` | `73e3ce6b0d7c241e8eacfe3f7e796e997991cd6583eccc827240dfe81395a60d` |

## 怎麼驗證

- Git 祖先與檔案清單查核；GitHub 唯讀確認 #205 已合併、#202／#220 未合併，`git ls-remote` 確認 main SHA。
- `python -B -X utf8 engine/_integration/dev/rebuild.py`：`dev-inputs-verified`，173 個來源檔、182 個鎖定檔，`writes: false`。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：通過，`errors=0`。
- `git diff --cached --check`：通過。
- 本機 Python 使用 `C:/Users/CHAOREN/AppData/Local/Programs/Python/Python312/python.exe`。GitHub 沙箱內認證失敗後，以獲准的沙箱外命令完成核對；未讀寫機密設定。

## 未驗證什麼

未新增 `ElfSkills.cs`、`Elf.cs` 或註冊程式；未建置、未更新 dev lock、未執行妖精技能離線測試。CI 僅驗證此文件分支的現有程式，不是 T115 驗收。未啟動遊戲、伺服器或 DB，未做真機測試；未修改原專案、封存、硬連結或遊戲資源，未做任何刪除。

## 產出路徑與給下一棒的提示

本輪唯一追蹤產出：`docs/reports/issue-229.md`；分支 `task/229`，草稿 PR 僅作交接、不關閉 Issue。

請主控將 #229 暫列 `task:blocked`，補上 #188／#196 依賴，確認 S55 4394 路徑與共用註冊契約，合併並同步後再派 codex-local 實作。後續每個技能都須透過正式送出／接收路徑做合成封包測試：自己與選取對象、未學／死亡／失效目標、三重矢 DHH 與權威效果、HP／MP／MR／抗性回報、解除與世界重設、屬性 mask 的 0／單系／多系／不相交，以及送出失敗不改狀態。測試提交到 `engine/_integration/dev/class-skills-tests/Elf.cs` 並接上入口；改程式後先更新 dev lock，再建置、跑測試與 CI。真機交 dot 的 M6 smoke。
