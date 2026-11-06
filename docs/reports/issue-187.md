# Issue #187：T100 技能施放通用化

## 做了什麼

- 移除技能目錄恰為 1／3／4／8 的限制；1–220 的封包格式改依伺服器 `C_UseSkill`／`validateSkill` 分派。實際送出仍要求原服已學、已選技能及有效動作狀態。
- 新增 `SkillCastTarget` 與共用編碼器，涵蓋 D、DHH、HH、HD、S、DHHS，以及自己、無目標、物件、座標、名字、書籤、召喚參數。拒絕缺漏／錯誤型別、內嵌 NUL 與無法編碼的文字。
- `CastSelectedMagic(target)` 接回既有 sender gate：不預扣 HP／MP，不合成效果；待送期間核對技能、世界、外觀、速度、目標及參數。41／61／75 可明確指定可見屍體。
- 補齊七職業與最低階段技能 id、來源 SHA-256、特殊尾段及下一棒介面。未修改接收端、共用分派點、`docs/REPORT.md` 或封存 lock。

## 怎麼驗證（已驗證）

- `.NET SDK 10.0.300`；先 `rebuild.py --update-lock`，再 `--build --configfile .github/nuget.ci.config --output scratch/issue-187-build`：產品與 launcher 成功。產品 0 error，既有 `MagicUi.cs:20` 的 CS8600／CS8602 共 2 warnings。
- `SkillCastChecks`：**785 項通過**。測試呼叫實際產品 DLL，合成封包與記憶體 sender；涵蓋全部 1–220 的 id／padding、逐位元組 golden vectors、所有特殊格式、CP950、未學／移除、屍體、冷卻、失效取消與座標快照。
- dev 唯讀 lock 核驗：173 source files／182 pinned files 通過。
- `check-no-game-assets.py`：通過，0 errors；最終提交前另以 staged index 重跑，包含本輪新增文件及測試。
- `git -c core.whitespace=cr-at-eol diff --check`：通過（既有來源為 CRLF，保留原換行）。
- 產品 DLL SHA-256：`98b3482ff45e5f937630249f70d4ebc156e0b3e1251bea061338e959160bdb36`。
- 實作提交 `4bb8cb0` 的 [build／dotnet／map-continuity](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42300631596) 與 [npc-offline](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42300631604) 全部通過；[PR #205](https://github.com/chaoren0418-arch/tw-bin-engine/pull/205) 已建立，分支 `task/187` 已推送。

## 未驗證什麼

- 沒有啟動遊戲、伺服器、DB，也沒有網路登入、真機技能效果或消耗驗收；來源與 P24Lab 部署 JAR 的對應未證明。
- 技能特定動畫／冷卻／MP／目標資格仍依外部 metadata 與伺服器。缺 metadata 時可送格式正確的請求，不代表每個技能對任何選定目標都能生效；未知欄位 0 不是實際零成本證據。
- 名字／書籤／召喚／直接點地／屍體的 UI 尚未新增；本次提供可送出的有型別介面，既有 UI 的職業專屬操作由後續任務接上。
- GitHub 既有 CI 的 Linux 工作交叉編譯 Windows 產品，Windows 工作跑既有離線回歸；本模組 785 項由本機 Windows 執行，不冒稱 CI 已自動執行此新增測試專案。

## 產出路徑

- `engine/source/SkillCast.cs`、`MagicSpells.cs`、`Magic.cs`（僅送出端）；`engine/_integration/dev/rebuild-lock.json`。
- `engine/_integration/dev/skill-cast-tests/`：可重跑的測試、專案與 README。
- `docs/protocol/skills.md`：格式、介面、七職業與來源識別。
- `docs/reports/issue-187.md`：本報告。
- 本機建置紀錄：`scratch/issue-187-build/`；產物：`engine/build/client-product-dev/`、`engine/build/skill-cast-tests/`（均不提交）。

## 給下一棒的提示

1. Claude／調度器審查 PR；dev lock 衝突由同步輪重新產生，勿拼接 hash。契約讀自尚未合併的 `task/186`，本任務沒有切換分支或帶入其規劃文件。
2. U8 已確認七職業：王族、騎士、精靈、法師、黑暗妖精、龍騎士、幻術師。主控可各開一張，採 `skills.md` 的基本 id 與介面；只有法師創角自動學到 4，不要將學習清單當已學清單。
3. 各職業任務補外部 metadata、目標／特殊參數 UI、效果與狀態。T101 保有接收端；T105 保有 `AttachedEffect` 擴充點，本次未改這兩區。
4. dot 在 M6 smoke 集中核對部署格式、1／3／4／8 回歸及七職業代表技能。worktree 及其唯讀 assets junction 保留，由調度器管理，未執行任何遞迴刪除。
