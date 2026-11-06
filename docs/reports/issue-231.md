# Issue #231／T117：黑暗妖精基本技能（第 1 次）

## 結論

**阻塞，功能尚未實作完成。** T100 已合併，但任務指定沿用的 T101／T108 仍未合併；目前也沒有七職業共用註冊契約。本輪只提交唯讀查核與交接，草稿 PR 不應關閉 #231，也不能作為技能驗收。

## 做了什麼／已驗證

- 已讀根目錄 AGENTS.md、docs/REPORT.md、TASKS.md、M6 契約、skills.md、server-protocol.md 與 server-rules.md。
- 工作分支維持 `task/231`，基底 `89f5adf04111f2385191369d76108e991f14bc57`；`2bdf2dc`（PR #205 合併）是其祖先，`engine/source/SkillCast.cs` 存在。
- 2026-11-07 查 GitHub：T101／#188 的 [PR #202](https://github.com/chaoren0418-arch/tw-bin-engine/pull/202) 為 open、merged=false（head `38ab63849aa64d78dbf6d1f3866093c95745c9a5`）；T108／#196 的 [PR #220](https://github.com/chaoren0418-arch/tw-bin-engine/pull/220) 亦為 open、merged=false（head `18c57f275b5b4a9fed7ebc50179c57f5eb37fdbf`）。
- 本 worktree 缺少 `engine/source/AttackSkillPackets.cs`、`StatusIcons.cs`。查核時遠端 main 為 `21e83b57a7740e3d347bcc70ea279601a192b353`，以該 SHA 讀取這兩個路徑均回傳 404；單純同步 main 尚不能解除依賴。
- `SkillCast.cs` 與 `MagicUi.cs` 尚無 `// ClassSkills registrations`、註冊 API；T100 的 `SkillTargetKind` 沒有背包物品種類，`TryPrepareSkillCast` 的 Object 分支只查自身或 `World.Objects`，不能把背包物品 ID 當成已支援目標。
- 現有 `MagicStatus.cs` 只處理有限圖示；`MagicSpells.AttachedEffect` 與 S55 接收仍有既有 gfx 白名單。不能只新增五個名稱或編碼器，就宣稱效果與 HUD 狀態已接通。

## 部分伺服器規則查核

以下為唯讀來源結論，未複製 Java、SQL 或遊戲資源；技能名稱採任務／U8。不是部署 JAR 或實機證據。

| 技能 | 已確認／下一輪必測 |
|---|---|
| 97 暗隱術 | `L1SkillUse.applyInvisibilityEffect` 先建立技能狀態，再對本人送 `S_Invis(objectId,1)`，對周圍送移除物件。S_Invis 格式為 C(171)、D(objectId)、C(type)，type 0／1 為取消／隱形。重施解除條件另受隱形裝備影響；不能在按下施法時自行切換隱身。需確認狀態回包與場景呈現的掛接範圍。 |
| 98 附加劇毒 | `L1SkillId.ENCHANT_VENOM=98` 已核對；實際材料、武器條件、效果／圖示來源本輪尚未完成追查。不能自行假設消耗、武器限制或預加 buff。 |
| 99 影之防護 | `L1SkillUse` 對 PC 呼叫 `updateBuffStatus` 與 `IndependentSkillIcon.sendStart`。AC 與圖示等權威回報，不能本地直接減 AC；覆蓋群組還受伺服器共存政策影響。 |
| 100 提煉魔石 | C6 尾段是 D，但語意為持有的物品實例 ID；`checkUseSkill` 保存至 `_itemobjid`，效果從玩家背包取該實例。支援模板轉換 40320→40321→40322→40323→40324（最後一種不能再煉）。`bringStone` 先移除一個來源；機率失敗送訊息 280，成功入庫送 403，入庫失敗補償並送 79。不可本地扣材料或保證產出。需測不在 AOI 的合法背包 ID、已移除／數量變更、非法物品、失敗消耗與權威庫存更新。 |
| 109 力量提升 | `DRESS_MIGHTY=109`；效果對 PC 呼叫 `updateBuffStatus` 與 `IndependentSkillIcon.sendStart`，不在客戶端預加 STR；須測新增、刷新、移除與角色狀態回報。 |

五招在 T100 協定表均為 C6 的 D 格式；這不代表五招可以共用世界物件資格。精確目標／材料與 gfx metadata 仍須在實作時逐一查核。

來源根：`D:/lineage開發/1-伺服器/src/com/lineage/server/`。以下為 Java 行號，沒有 BIN File Offset／RVA／VA 主張。

| 來源 | 使用區段 | SHA-256 |
|---|---|---|
| `model/skill/L1SkillId.java` | 448–508，五個技能常數 | `5b3dafdac87f504efdd11f70f207999badd7ae8f35b4370b513d4bdcbd678a14` |
| `model/skill/L1SkillUse.java` | 502–508 物品目標；1052–1071 隱身重施；3268–3300 提煉；3422–3453 防護／力量；3967–3974 隱身；4208 起提煉提交 | `2fad1a69076c29704c8ea9a41361aff357f22b0e895223a6bb7b943a4879015d` |
| `serverpackets/S_Invis.java` | 17–26，物件隱形序列化 | `891515fe8213d326999ceb8537f9543fe37100da96de23e16b59560f587f24c1` |

## 怎麼驗證

- 使用本機 Python 3.12 完整路徑執行 `-B -X utf8 engine/_integration/dev/rebuild.py`：通過，173 source／182 pinned，唯讀。
- `-B -X utf8 engine/tools/check-no-game-assets.py`：通過，3243 tracked files，errors=0。
- 本輪沒有程式變更，因此沒有更新 dev lock、重建產品或執行技能測試。文件 PR 的 CI 結果另見 PR checks；即使綠燈，也不代表 T117 完成。

## 未驗證什麼／產出路徑

- 本輪產出僅 `docs/reports/issue-231.md`。
- 尚未新增 `engine/source/ClassSkills/DarkElfSkills.cs`、`engine/_integration/dev/class-skills-tests/DarkElf.cs` 及測試入口；五技能的送出、目標條件、效果與狀態驗收均未完成。
- 未啟動遊戲、伺服器、DB；未修改原專案、設定、封存、硬連結、docs/REPORT.md 或 lock；未刪除任何目錄或拆除 worktree。

## 給下一棒的提示

1. 主控／調度器將 #231 暫列 `task:blocked`，補入 #188／#196 依賴；待 PR #202／#220 審查合併後同步再派工。
2. 主控先協調七職業共用註冊 API：目標解析、送出前及 sender 等待後的背包重驗、UI 物品選取，以及效果／狀態掛接。若需要改 S171 接收、隱身呈現或 S55 白名單，明列允許修改的檔案；目前「兩個共用檔各一行註冊」不能取代尚不存在的掛接契約。
3. codex-local 接手完成五招規則查核與實作，將每招的合成封包測試提交至指定 DarkElf.cs，接入測試入口／CI；先更新 dev lock，再建置、離線測試與資源拒收檢查。測試包括未學技能、待送期間物品失效、拒絕時不偽造效果、隱身進出及狀態刷新／清除。
4. 真機仍交 dot 的 M6 smoke，確認實際已學技能、材料消耗、隱身與原生效果；本報告不能替代該驗收。
