# Issue #230／T116：法師基本技能（依賴阻塞）

## 結論

**未完成，待主控補齊依賴後續派。** 本輪僅提交核對與交接報告；不能作為 T116 技能驗收或關閉 Issue 的依據。

2026-11-07 06:41（台北）核對基底 `d43a6692e3ae538cb46d18b2919b39727ef60ec9`，分支 `task/230`。T100 的 [PR #205](https://github.com/chaoren0418-arch/tw-bin-engine/pull/205) 已合併，其 merge commit `2bdf2dc9da13327905b8289b23b365edd4d3e076` 是目前 HEAD 祖先，`SkillCast.cs` 存在。但任務要求使用的兩個共用介面尚未進入基底：

| 依賴 | GitHub 查核結果 | 本 worktree |
|---|---|---|
| T101／#188，[PR #202](https://github.com/chaoren0418-arch/tw-bin-engine/pull/202) | open，merged=false；head `38ab63849aa64d78dbf6d1f3866093c95745c9a5` | 缺 `engine/source/AttackSkillPackets.cs` |
| T108／#196，[PR #220](https://github.com/chaoren0418-arch/tw-bin-engine/pull/220) | open，merged=false；head `18c57f275b5b4a9fed7ebc50179c57f5eb37fdbf` | 缺 `engine/source/StatusIcons.cs` |

上述狀態為查核當時快照，接手時須重查。

## 做了什麼

- 讀取根目錄 `AGENTS.md`、`docs/REPORT.md`、T100 技能文件及伺服器協定／規則。未改 `docs/REPORT.md`。
- 核對 `SkillCast.cs`、`MagicUi.cs`、`Magic.cs`、`MagicStatus.cs`、`MagicSpells.cs` 與既有離線測試入口。
- 確認現有 S30 魔法分支仍限定 action 18、gfx 167、flight 0；不能據此聲稱冰箭、風刃及進階技能效果完成。現有狀態處理僅涵蓋保護罩、神聖武器等既有路徑，不能代替指定的 T108 介面。
- `SkillCast.cs`、`MagicUi.cs` 均無 `// ClassSkills registrations` 標記，也未找到可直接呼叫的職業註冊介面。標記本身依任務可新增；尚需主控確認共用註冊契約由誰提供，避免七張任務各自建立不相容的入口。
- `MagicUi.cs` 現有目標清單排除死者；書籤、座標選格、召喚選單尚未提供。`AllowsDeadTarget` 只放行死者，並未要求復活目標必須死亡。這些都仍是 T116 的實作工作。

由於任務限制共用檔各只加一行註冊，且效果／HUD 必須使用指定上游介面，本輪未另寫替代解碼器、未合併未核准 PR，也未以無接線的職業檔或測試占位宣稱完成。

## 已核對的技能與後續測試範圍

以下是交接清單，**不是本輪測試通過紀錄**。C6 前綴為 `6,(id-1)>>3,(id-1)&7`；H／D 為 little endian，明文補零至 8-byte 邊界。

| id | 技能／用途 | 尾段 | 接手須實作與測試 |
|---|---|---|---|
| 1 | 初級治癒術 | D | 自己／物件選擇、死亡及消失目標拒絕；效果與 HP 依回包更新 |
| 2 | 日光術 | D | 核對自用規則與 metadata；效果／光照狀態及解除以原服回報為準 |
| 3 | 保護罩 | D | 自用規則、狀態圖示倒數／解除、AC 回報；不預改 AC |
| 4 | 光箭 | DHH | 當前物件 ID／座標、10 格既有規則；實際 S30 效果及 HP 不預扣 |
| 5 | 指定傳送 | HD | 明確 map／bookmark；ID 0 隨機傳送也須明確選擇；取消／換圖使舊選擇失效 |
| 6 | 冰箭 | DHH | 活物件與當前座標；不能沿用固定光箭 gfx 作效果驗收 |
| 7 | 風刃 | DHH | 活物件與當前座標；核對本技能回包效果與呈現 |
| 8 | 神聖武器 | D | 自己／物件規則、武器條件由伺服器裁定；原生／代理圖示更新及解除 |
| 51 | 召喚 | D | 召喚選項而非世界物件 ID；一般與控制戒指路徑、取消選單、召喚物回包 |
| 58 | 座標技能 | HH | 無 DWORD 前綴；明確選格、固定座標不跟隨物件、換圖取消、效果回報 |
| 63 | 座標技能 | HH | 同上，獨立核對本技能效果／狀態，不假設與 58 相同 |
| 61 | 復活 | D | 只選可見屍體；活者、消失及等待送出時復活的物件應拒絕；以回包復原狀態 |
| 75 | 高級復活 | D | 同上，獨立覆蓋目標條件、送出與復活回包 |

共同測試還須覆蓋未學／移除技能、送出期間換技能／目標／地圖／斷線、單次施放、錯誤與截斷回包不得部分更新，以及送出後不預扣 HP／MP、不先套 buff。每個 id 都需獨立斷言送出格式、合法／非法目標及回包效果／狀態，接上可執行入口。

唯讀來源補核：`C_UseSkill.java` 268–290 行確認 58／63 讀 HH；51 在裝備 20284／120284 時將 D 解讀為召喚選項，沒有戒指時只消耗該欄位。`L1SkillUse.java` 1501–1517 行確認復活拒絕活者與塔／門。客戶端若無可靠物件類型資料，不得只憑名稱推斷塔／門，最終資格仍由伺服器判定。以上是 Java 行號，沒有 BIN File Offset／RVA／VA 主張。

## 怎麼驗證

- `rebuild.py` 唯讀核驗通過：`status=dev-inputs-verified`、173 source／182 pinned、`writes=false`。
- `check-no-game-assets.py` 通過：3241 tracked files、4 embedded resource declarations、0 errors（加入本報告前）。
- `git merge-base --is-ancestor 2bdf2dc HEAD` 退出 0，確認 T100 已在基底。
- PATH 沒有 Python，`py -3` 回報未安裝；改用既有 `C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe -B -X utf8` 成功執行上述兩項核驗，未安裝軟體。
- 本輪僅改文件，未更新 dev lock、未執行本機重建；CI 結果以草稿 PR checks 為準，即使全綠也不代表法師功能通過。

## 來源識別（SHA-256）

| 路徑 | SHA-256 |
|---|---|
| `engine/source/SkillCast.cs` | `46c38d4dbdb7c2d4e96ee67abcf7d38ce449a4c681e1fd5f9610fb11fce50676` |
| `engine/source/MagicUi.cs` | `380e595f62533f3ac1ae18cf2152e31a5199e0327abc80fd84e7b96d5d0e4fe5` |
| `engine/source/Magic.cs` | `9b68462c03aef5377a78e1c56a083b674d7f80073c75f3e625178b41aeb9f14f` |
| `engine/source/MagicStatus.cs` | `5b8d0eeec6a7a45457f430ab587d833d8eea387e3ac94d2e3717c2e3f805a8ce` |
| `docs/protocol/skills.md` | `a768e9ff190d4a966a805ec8949daf229bc0d01e1191edf4dcacab98a696ede2` |
| `D:/lineage開發/1-伺服器/src/com/lineage/server/clientpackets/C_UseSkill.java` | `4932c6dd71b330d91b75c4fcbf02c30ffa4712c35d7cb07537626159038e168e` |
| `D:/lineage開發/1-伺服器/src/com/lineage/server/model/skill/L1SkillUse.java` | `2fad1a69076c29704c8ea9a41361aff357f22b0e895223a6bb7b943a4879015d` |

## 未驗證什麼／產出路徑

唯一提交產出為 `docs/reports/issue-230.md`。尚未建立 `WizardSkills.cs`、`class-skills-tests/Wizard.cs` 或測試入口；法師 13 個技能、特殊 UI、效果及狀態整合均未完成驗收。未啟動遊戲、伺服器或 DB；未讀機密設定、未複製 Java 原始碼／遊戲素材、未寫原始專案／封存或移除 worktree。

## 給下一棒的提示

1. 主控／Claude 審查 T101 #202、T108 #220，安排依既有權責合併，再由調度器同步本工作分支；建議為 #230 補上這兩項依賴並暫標 `task:blocked`。本輪未變更 Issue 標籤。
2. 主控確認職業註冊契約、測試入口與共用修改邊界；特別是 UI 每次刷新、場景選格及屍體選取如何掛接。若必須修改超過兩行的共用來源，由主控另派共用基礎任務或明確調整範圍。
3. codex-local 在依賴具備後續作上述 13 個技能與持久化離線測試；以新介面覆蓋實際接收／呈現，不能只測 metadata 或編碼器。
4. 改程式後先 `--update-lock`，再建置、離線測試、素材檢查與 CI；真機由 dot 承接 M6 smoke。
