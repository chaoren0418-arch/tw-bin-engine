# Issue #227／T113：王族基本技能（第 1 次，依賴阻塞）

## 結論與做了什麼

**本輪未完成技能實作；此報告與草稿 PR 不可作為 T113 驗收通過或關閉 Issue 的依據。**

在既有 `task/227` worktree 核對 AGENTS.md、docs/REPORT.md、M6 契約、T100 程式與協議，以及唯讀伺服器來源。未切換分支、未修改引擎、未修改 docs/REPORT.md、未啟動遊戲／伺服器／DB，未複製遊戲資源或伺服器原始碼。

## 已驗證：依賴現況

本輪基底：`f8cca46f9c0702057d9c029c45ee4d25388b32ef`。2026-11-07 透過 GitHub 查核：

| 前置 | 查核結果 | 對 T113 的影響 |
|---|---|---|
| T100／PR #205 | 已合併；本地包含 merge `2bdf2dc` 與 `engine/source/SkillCast.cs` | 通用送出可用 |
| T101／[PR #202](https://github.com/chaoren0418-arch/tw-bin-engine/pull/202) | open、merged=false；head `38ab63849aa64d78dbf6d1f3866093c95745c9a5` | 本地沒有 `AttackSkillPackets.cs`，不能完成指定介面的效果整合與測試 |
| T108／[PR #220](https://github.com/chaoren0418-arch/tw-bin-engine/pull/220) | open、merged=false；head `18c57f275b5b4a9fed7ebc50179c57f5eb37fdbf` | 本地沒有 `StatusIcons.cs`；現有 MagicStatus 僅處理技能 3／8，無王族光環 |

本地 `SkillCast.cs`、`MagicUi.cs` 尚無 `ClassSkills registrations` 標記或職業註冊介面。標記可依任務建立，但必須先確定共用介面，不能把未合併依賴自行複製進本任務，或重做它們的封包處理來冒充整合完成。

## 已驗證：技能格式與範圍差異

| 技能 | C6 尾段／目標 | 回報與接續注意事項 |
|---|---|---|
| 113 精準目標 | DHHS：物件 ID、X、Y、CP950 標記文字與 NUL | `TRUE_TARGET.start` 發 `S_TrueTarget`，尾段為 D(target)、D(object)、S(message)。目前引擎沒有此接收處理；必須另確認接收掛接與標記呈現範圍。不能用本地輸入文字冒充伺服器回報 |
| 116 呼喚盟友 | S：盟友名字 | `CALL_CLAN.start` 對被呼喚者發 Y/N 訊息 729；不能以送出成功宣稱盟友已傳送 |
| 118 援護盟友 | **S：盟友名字，並非自己光環** | `RUN_CLAN.start` 依伺服器地圖條件傳送，或回訊息 1192／647 與傳送解鎖；不能改送 D(caster) |
| 114 激勵士氣、115 鋼鐵士氣、117 衝擊士氣 | D；職業 UI 應固定自己 | 依伺服器回報更新圖示與時間，不能在送出時預設成功或自行改能力值 |

116／118 的格式同時受到 `docs/protocol/skills.md` 與 `docs/reference/server-protocol.md` 支持。`C_UseSkill.java` 第 232–248 行確認名字與標記欄位。`TRUE_TARGET.java` 對附近盟友送出時，第二個 D 可為接收盟友 ID，不能一律當成原施法者。伺服器 `S_TrueTarget` 另有附 H(gfx) 的 overload，接收設計須區分，不能套用 S30／S55 格式。

上述是來源核驗；與 P24Lab 部署 JAR 是否完全同版、真機行為均待驗證。

## 怎麼驗證

- `rebuild.py` 唯讀核驗：`dev-inputs-verified`，173 個來源檔、182 個鎖定檔、writes=false。
- `check-no-game-assets.py`：pass，errors=0（提交報告前 3241 個追蹤檔）。
- 僅文件變更，未執行 `--update-lock` 或重建產品，避免產生無關 lock 變動。
- 尚未新增／執行 `Crown.cs`；六技能送出、目標、效果、狀態的離線驗收未通過。草稿 PR 的 CI 只代表此文件分支檢查，不代表 T113 功能完成。

## 唯讀來源 SHA-256

根目錄：`D:/lineage開發/1-伺服器/src/com/lineage/server/`。均為檔案識別，沒有 BIN File Offset／RVA／VA 主張。

| 相對路徑 | SHA-256 |
|---|---|
| clientpackets/C_UseSkill.java | `4932c6dd71b330d91b75c4fcbf02c30ffa4712c35d7cb07537626159038e168e` |
| model/skill/skillmode/TRUE_TARGET.java | `515b9e53d1fa255fbc525d321dce92f95b1fdc5a0c37cc43af85315363b77f54` |
| model/skill/skillmode/CALL_CLAN.java | `e1e4313dc60c4b1926df260f8abd480b2d0a9769f058f621fef06c34412f1a41` |
| model/skill/skillmode/RUN_CLAN.java | `93c9896dd3330fceab799e747ac0fe2732810ef5daec7460ea85381829c66f17` |
| serverpackets/S_TrueTarget.java | `8814433e64d0eee71d844d9f3fa48a935b62daeda5f540c0ebc2c31ec0a9c7a5` |

## 產出路徑／給下一棒的提示

產出僅 `docs/reports/issue-227.md`；分支 `task/227`，草稿 PR 不使用 Closes。

1. 主控／調度器先處理 #188／#196 的審查與合併，補列為 #227 的依賴，等待期間建議標 `task:blocked`；本輪未自行修改任務標籤。
2. 主控將 118 修正為名字型援護盟友；確認 `S_TrueTarget` 接收與標記畫面是否允許新增共用掛接（目前只允許 SkillCast／MagicUi 各一行）。若已有其他模組承接，提供其介面。
3. 依賴合併並同步基底後，由 codex-local 繼續同一任務：新增 CrownSkills、名字／標記 UI、自身光環、`class-skills-tests/Crown.cs` 與測試入口。涵蓋 6 個技能、CP950／NUL／長度、無效／消失目標、伺服器效果回包、光環更新／解除／到期及失效送出取消。
4. 程式完成後再 update-lock、canonical build、離線測試、素材檢查與 CI。Claude 審查，使用者合併；原生驗收留給 dot 的 M6 smoke。
