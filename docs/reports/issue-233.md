# Issue #233／T119：幻術師基本技能（第 1 次，阻塞）

## 結論／做了什麼

本輪完成前置與共用介面核查、部分伺服器規則查證；**技能實作尚未完成，不可視為 T119 驗收通過**。沿用 `task/233`，本機基底 `21e83b57a7740e3d347bcc70ea279601a192b353`，未切換或建立分支。

2026-11-07 以本機 Git 歷史及 GitHub API 查核：

| 前置 | 已驗證狀態 |
|---|---|
| T100／#187／[PR #205](https://github.com/chaoren0418-arch/tw-bin-engine/pull/205) | 已合併，`mergedAt=2026-11-06T22:20:17Z`；merge commit `2bdf2dc9da13327905b8289b23b365edd4d3e076` 為 HEAD 祖先，`SkillCast.cs` 存在。 |
| T101／#188／[PR #202](https://github.com/chaoren0418-arch/tw-bin-engine/pull/202) | OPEN、`mergedAt=null`，head `38ab63849aa64d78dbf6d1f3866093c95745c9a5`；指定的 `AttackSkillPackets.cs` 不在本 worktree。 |
| T108／#196／[PR #220](https://github.com/chaoren0418-arch/tw-bin-engine/pull/220) | OPEN、`mergedAt=null`，head `18c57f275b5b4a9fed7ebc50179c57f5eb37fdbf`；指定的 `StatusIcons.cs` 不在本 worktree。 |

另查遠端 main commit `a364b5d6f6d0fed09a001526d4be190aabaaf3fa` 的檔案樹，同樣沒有上述兩檔或 ClassSkills 模組。這不是 T100 未合併，而是任務要求沿用的 T101／T108 尚不可用；不能藉由本任務另造替代接收器或未接線的空殼宣稱完成。

`engine/source/*.cs` 未找到 `ClassSkills registrations` 或職業註冊 API。標記可依任務建立，但現有 `SkillCast.Request` 僅編碼，`TryPrepareSkillCast` 與 `MagicUi` 直接使用既有邏輯；尚無可由各職業「各加一行註冊」接入的共同契約。既有 `MagicSpells.Get` 的 metadata 來自外部 catalog 或通用回退，沒有職業 provider。此缺口也已見 [#228 報告](issue-228.md)。

## 已驗證的來源規則

已讀 `AGENTS.md`、`docs/REPORT.md`、`TASKS.md`、`docs/design/m6-contract.md`、`docs/protocol/skills.md` 與兩份 server reference；伺服器來源僅唯讀，以下是來源規則，不代表部署 JAR 或真機結果。

| 技能 | C6 尾段 | 本輪核查結果／接手注意事項 |
|---|---|---|
| 201 鏡像 | D | `MIRROR_IMAGE.start/stop` 更新施法者閃避並送 `S_PacketBoxIcon1`；不得送出 C6 後自行增加閃避或圖示。 |
| 202 混亂 | DHH | `CONFUSION.start` 對目標設定 `SILENCE`，對 PC 送訊息 1339；不能因技能名稱自行推導另一種混亂狀態。 |
| 203 暴擊 | DHH | 已核對 id 與尾段；精確目標資格、動畫／gfx 與傷害回包仍待完整查證。 |
| 204 幻覺：歐吉 | D | 已核對 id 與尾段；D 不能證明僅限自己，完整 template 目標條件及圖示映射待查。 |
| 205 立方：燃燒 | D | `L1SkillUse.java:2735` 起由伺服器 `spawnEffect` 建立模板 80149，使用技能持續時間與已解析目標座標；572 起以附近同類效果 gfx 6706 檢查重複立方。需要驗證權威物件出現／移除，不能在施法送出後自行產生持續立方。D 不是自由選格的 HH。 |
| 206 專注 | D | 已核對 id 與尾段；精確目標資格、持續時間、HUD 狀態映射仍待查。 |
| 207 心靈破壞 | DHH | `MIND_BREAK.start` 的傷害取決於伺服器對目標 MP 的條件與扣除；客戶端不得固定扣 MP 或以效果封包直接扣 HP。 |
| 208 骷髏毀壞 | DHH | `BONE_BREAK.start` 有機率暈眩分支，建立效果模板 86123；PC 暈眩與解除各送 `S_Paralysis`。有施法／傷害不代表必然暈眩。 |

以上八項 Lv10 清單沿用 U8；仍須原服 S164 已學回包，不自動授予技能。尾段前綴為 `C(6), C((id-1)>>3), C((id-1)&7)`，D/H 為 little endian，整包補零至 8-byte 邊界。`L1SkillUse.java:428` 對 template `target=none` 才將目標與座標改為施法者；未查得個別 template 時不將所有 D 一概當自用。

來源識別（共同根 `D:/lineage開發/1-伺服器/src/com/lineage/server/model/skill/`；上述位置均為來源行號，非 File Offset／RVA／VA）：

| 相對檔案 | SHA-256 |
|---|---|
| `L1SkillId.java` | `5b3dafdac87f504efdd11f70f207999badd7ae8f35b4370b513d4bdcbd678a14` |
| `L1SkillUse.java` | `2fad1a69076c29704c8ea9a41361aff357f22b0e895223a6bb7b943a4879015d` |
| `skillmode/MIRROR_IMAGE.java` | `386967b6999355be3af122077465547c4295a50b66adf561e602ff1ef9fbdb33` |
| `skillmode/CONFUSION.java` | `2ac57fd1358fe4aa9403ca15a81a76ae7ba2a3067f7b28a54f53459f346085c0` |
| `skillmode/MIND_BREAK.java` | `cda9195e773e0144e59dff9f445ec79e1da42f632d4fa79060f2accdec85d175` |
| `skillmode/BONE_BREAK.java` | `e1edb07b4b6a1cdab4760531fdcd1a11a44c07821b5d49fcf16219a6e8ca304e` |

## 怎麼驗證／未驗證什麼

- 已核對 T100 祖先關係、GitHub PR 狀態、本機與遠端 main 的檔案樹及本機註冊符號。
- `check-no-game-assets.py` 通過（`trackedFiles=3244`、`errors=0`、embedded resources=4）；`git diff --check` 通過。後續 CI 以交接 PR 的 checks 為準，綠燈亦不代表幻術師功能完成。
- 純文件變更；未改產品來源、`docs/REPORT.md`、dev lock 或封存 lock，未執行產品建置。沒有新增 `IllusionistSkills.cs`、`class-skills-tests/Illusionist.cs` 或測試入口；八技能的送出／目標／效果／狀態離線驗收皆未完成。
- 未啟動遊戲、伺服器或 DB；未讀機密配置，未複製伺服器原碼或遊戲資源，未做任何刪除。真機留給 M6 smoke。
- 沙箱內 `gh` 回 401、Git HTTPS 回 `SEC_E_NO_CREDENTIALS`；使用沙箱外既有認證已成功完成唯讀 GitHub 核查。

## 產出路徑／給下一棒的提示

產出：`docs/reports/issue-233.md`（阻塞交接，不是功能完工報告）。

請主控／調度器將 #233 列為 `task:blocked`，補上 T101（#188／PR #202）、T108（#196／PR #220）依賴；待合併並同步 worktree 後再派工。同時協調七職業的共用註冊契約，明確提供 metadata、目標規則、效果／狀態及 UI 的接入方式，使兩個共用檔各加一行即可運作。這是上游整合事項，不需要使用者操作遊戲或 DB。

下一輪執行器補齊八技能 template 與回包映射、產品來源及持久化離線測試，測試須涵蓋未學／失效目標、DHH 當前座標、無權威回包時不預扣或授予狀態、效果與解除、立方物件生命週期；先 `--update-lock` 再建置，通過資源檢查及 CI 後才可宣告 code 完成。M6 smoke 由 dot 依既有授權驗證。
