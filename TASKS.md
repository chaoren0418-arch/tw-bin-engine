# TASKS — tw-bin-engine

更新：2026-11-11（#76 範圍凍結；#99、#110 調整 M3；#128 依使用者節流指示改寫驗證方式與 M2；#162 M2 結案、新增 M4；#177 M4 結案、新增 M5；#186 新增 M6 內容擴充；#238 M6 補列 T113–T134；#385 補列 T135–T146；#418 補列 T147–T153；#427 第四層證據：原版表現查證先於開發；#485 補列 T167–T201；#503 新增 M7 重建補缺；#535 M7 補列 T203–T227；#569 M7 補列 T228–T243；#657 M7 補列 T244–T274；#727 M7 補列 T275–T304；#894 M7 補列 T305–T383；#982 M7 補列 T384–T427；#1023 M7 補列 T428–T443 並結案）。格式：`### ID | 狀態 | 負責 | 範圍`。狀態只用 todo／doing／review／done／blocked。

**計算規則（凍結）**：
- 調度台的進度條**只算 `## M*` 區段**；`## Backlog` 只顯示數量，不算進度。
- 每個里程碑都標了「主線 N 項，封頂」。之後新發現的工作一律先放進 Backlog。要升級成主線，必須同時砍掉一項主線，或經使用者同意。
- 分母只允許因 `PLAN.md`「盤點：已知範圍」中列出的**不確定項**兌現而變動，兌現時要在 PR 說明中引用當初的估算。
- 每個里程碑的「驗收：使用者打開會看到什麼」寫完後就凍結。要改必須開新 Issue 說明理由，並標 `needs:user`。

**驗證方式（使用者 2026-11-06 17:39 指示，#76 轉達，#128 寫入）**：
- 每張任務的驗收是「**編譯通過、相關單元測試綠燈、短報告**（做了什麼／怎麼驗／沒驗什麼，各幾行）」。不再逐封包比對或寫千字證據清單。
- **真機只在里程碑結束時做一次 smoke**（dot）；沒過的列直接開 code 任務修，到下一個里程碑的 smoke 一起驗，不再多輪補測。
- 任務切大，一張做完一個看得到的功能；派工文字只寫目標、範圍、驗收三段。
- 逆向協議、伺服器狀態機或資料表欄位時，優先派本機任務**唯讀**查閱 `D:\lineage開發\1-伺服器` 的封包處理器與欄位定義，只把結論與簽章記進 `docs/`，不複製原始碼。


**第四層證據：原版表現（使用者 2026-11-07 16:06／16:18／16:31 指示，#427 寫入）**：
- 任何「原生表現」（畫面、動作、版面、音效、時序）都要先有 `docs/reference/<主題>.md` 的查證，**不准猜**。查證方法照 #417：用 IDA 7.7 與 P01 的 IDB，靜態分析未加殼的 `TW1608262200.bin`，地址標 VA／File Offset。索引見 `docs/reference/INDEX.md`。
- **原生表現沒有查證來源的功能，不得開 code 任務**；只能先開 resource 查證任務。
- 已完成但沒有查證的功能，由對照任務（T164）列出差異後，再開修正任務。
- 里程碑完成的定義加一條：**與原版表現一致**（以 `docs/reference` 的查證為準）。


**暫行政策（使用者 2026-11-08 17:53 指示，#503 寫入）**：
- **只做 TW BIN**；LiTo 兩個專案暫停。
- **審查與驗證暫緩**：不開 test／smoke，不派 dot，不派審查。code 任務的驗收只要「編譯＋既有單元測試綠」，通過閘門與 CI 就合併。
- runner 一律 `claude-local`。Codex 的每週額度在 11/13 前用完，之後會改扣 credits，所以不派 Codex。
- 第四層證據規則仍然有效：補缺的 code 任務都要引用 `docs/reference/` 的查證，包括 `gap-matrix.md`。

---

## M0 可重現建置 — 主線 13 項，封頂（✅ 全部完成）

**驗收：使用者打開會看到什麼（凍結）**：
- **前置條件**：Windows 機器上有原客戶端資源目錄（`TW_CLIENT_ROOT`，預設 `D:\lineage開發\0-客戶端`），並且依 `docs/BUILD.md` 接好 `engine/assets` junction，安裝 SDK 10.0.300 與 Python 3.12。
- 在這個前提下，從**新建的 clone 或 worktree** 照 `docs/BUILD.md`／`docs/demo/RUN.md` 執行一條建置指令，會輸出 `PASS dev {…}`；`engine\啟動新版客戶端.cmd --check-only` 輸出的 JSON 是 `revision: dev`、`uiStarted: false`；GitHub Actions 綠燈。
- **已驗證**（附出處）：
  - 新建 worktree 的 **r35** 建置加 check-only：#13（`scratch/wt/task-13`，`PASS r35`、`revision: r35`，exit 0）。
  - 主專案目錄的 **dev** 建置加 check-only：#38、#70（`revision: dev`、`uiStarted: false`，exit 0）。
  - GitHub Actions：ubuntu-latest 交叉建置 Windows 目標綠燈（#67／PR #72）。CI 只證明可編譯，不算 Windows 原生驗收。
  - 新建 worktree 的 **dev** 建置加 check-only：#88（T017，`scratch/wt/task-88`，來源 `4021caf`，`PASS dev`、`revision: dev`，exit 0）。

### T001 | done | Claude | CI：GitHub Actions dotnet build
### T002 | done | Claude | r35 canonical lock（181 檔）
### T003 | done | Claude | 去除硬編路徑（TW_ENGINE_ROOT／TW_CLIENT_ROOT）
### T004 | done | codex-local | `docs/BUILD.md` 乾淨機器建置文件（#6）
### T005 | done | codex-local | 規則檔與 `docs/BUILD.md` 狀態同步、dot 真機授權寫入（#107／PR #113）
### T013 | done | codex-local | 本機 r35 完整建置（#7）
### T014 | done | codex-local | rebuild.py 允許 assets junction（#12）
### T015 | done | codex-local | 本機 r35 建置加 check-only 通過（#13）
### T016 | done | codex-local | 主專案目錄 r35 建置（#19）
### T040 | done | codex-local | 滾動式 dev lock（#31）
### T041 | done | codex-local | 選用封包追蹤，只記 op／len（#34）
### T050 | done | codex-local | CI 改在 Linux 交叉建置 Windows 目標（#67／PR #72）
### T017 | done | codex-local | 新建 worktree 的 dev 建置加 check-only（#88／PR #90）

---

## M1 打開就看得到的 Demo — 主線 15 項，封頂（✅ 結案，#135）

**驗收：使用者打開會看到什麼（凍結）**：打開 repo 的 `docs/demo/README.md`，看到 N01 流程 15 步的表格：登入、選角、進世界、鍵盤移動、點地移動、近戰、技能 1／3／4／8、喝水、裝備、金幣丟撿、能力值、聊天、登出、重登保存、音效。**每一列**都有一張真機截圖連結與 ✅；或是 ❌／⚠️ 加一句原因，而且原因已列在 Backlog。其中**金幣丟 1 枚再撿回**、**攻擊／施法時音量峰值 > 0、靜音時 = 0** 必須是 ✅（附截圖與數值）。**唯一例外**：如果伺服器 JAR 的證據證明這個伺服器規則上不允許丟金幣，就改用另一個已啟用的可疊加物品（例如治癒藥水）證明丟 1 → 撿回的循環，並把「金幣不可丟」列入 Backlog。頁面頂端寫明 dev 版本 HEAD 與 DLL SHA-256。

### T010 | done | Claude | N01 真機核對表（`docs/native-baseline/CHECKLIST.md`）
### T011 | done | dot | r35 真機基線 N01（#23）
### T012 | done | codex-local | 提交基線證據與 `docs/native-baseline/r35.md`（#27）
### T042 | done | dot | 局部補測加封包追蹤（#37）
### T043 | done | codex-local | 主專案同步加 dev 建置（#38）
### T044 | done | codex-local | 提交補測證據 `dev-retest.md`（#41）
### T045 | done | codex-local | 設計：音效與地面物品原客戶端資料（#42）
### T046 | done | dot／codex-local | 資源契約：音效 WAV pins、地面 sprite pins、伺服器模板（#47、#48、#49）
### T047 | done | codex-local | 攻擊／技能音效依原始幀事件派送（#50／PR #65）
### T048 | done | codex-local | 地面物品 catalog、金幣丟撿與數量輸入（#51／PR #66）
### T060 | done | codex-local | DEMO-1：主專案同步、dev 建置、demo 頁骨架與 `RUN.md`（#70）
### T061 | done | dot | DEMO-2：真機跑完 N01、37 張截圖、填 demo 頁（#71；部分通過，缺口交 T062）
### T062 | done | codex-local → dot | DEMO-3（#77／PR #134）。已通過：卸裝裝回、聊天、數量防呆、原生圖示、初始座標、光箭封包與有聲峰值 0.548、**近戰三階段音量 0.509／0／0.509**（C229＋S30 有效）。未過、移入 M2：普通肉丟撿循環（路線被地形擋住）→ **T070**；光箭靜音、恢復兩階段（沒有 S30）→ **T072**；近戰擊殺與 EXP → **T072**；移動終點與 F9 圖示 → **T073**；非滿血治癒 → **T072**。以上都在 **T075** smoke 一起驗。
### T063 | done | codex-local | DEMO-3 修正：近戰／光箭選目標回饋、數量錯誤文案、權威座標、可丟物品 JAR 查證（#115／PR #118；U1 兌現第 1 張）
### T064 | done | codex-local | DEMO-3 修正：F9 圖示（T035 回歸）、光箭音效尾段、靜音 session 保活、方向鍵拒絕提示（#123／PR #124；U1 兌現第 2 張）

**M1 收尾規則（#128，使用者節流指示）**：第 2e 輪結束後，M1 不論結果都結案。沒過的列（包括上方凍結驗收中兩個「必須 ✅」的列）在 demo 頁標 ⚠️ 加原因，並移到 M2 的對應任務，在 M2 smoke（T075）一起驗。這是對凍結驗收的變更，依據是使用者 17:39 的指示。

---

## M2 完整遊戲循環 — 主線 6 項，封頂（✅ 結案，#162；未過列移入 M4）

**驗收：使用者打開會看到什麼（凍結）**：`docs/demo/README.md` 新增「M2 遊戲循環」一節。每一項都有真機截圖、前後數值，以及 ✅ 或「❌ 加 Backlog 原因」：
- **NPC**：向 NPC 買一件物品（金幣減少、物品入袋）、倉庫存入再取出、私商買一件、兩個角色直接交易一件。
- **跨圖**：洞穴進出、往返船運各一趟、旅館入住，換圖後畫面與座標正確，回原圖不殘留。
- **戰鬥閉環**：打死怪物，得到自然 EXP 與戰利品並撿起；向 NPC 學一個技能並施放；**弓箭**：裝弓單發射擊，看得到箭矢飛行與命中，箭數權威減 1，箭用完時無法射擊，重登後箭數保存；**自身死亡**：角色死亡後畫面鎖定，恢復後可以正常移動。
- **原生 UI**：滑鼠點地移動、點怪攻擊、點 NPC 交談；HUD、快捷槽、陰影、聊天焦點顯示正確；**物件生命週期**：其他玩家或 NPC 離開視野再回來時顯示正確，目標消失時選取跟著清除，斷線重連後不殘留舊物件。
- **場景音效**：map4 港口的 6016 場景音效有聲（峰值 > 0），離開後停止。

### T070 | done | codex-local | NPC 服務：購買、倉庫、私商購買、直接交易（R06、R08、R09、R11、N03），以及 M1 移入的普通肉購買路線與丟撿循環（#133）／PR #142
### T071 | done | codex-local | 跨圖連續性：洞穴、船運、旅館、地圖特效（R12、R13、R14、MAP-F9、N04）（#140）／PR #146
### T072 | done | codex-local | 戰鬥閉環：自然 EXP、戰利品、技能學習、弓箭與彈藥、自身死亡與恢復（R03、R04、A1；#81 U01–U04），以及 M1 移入的近戰擊殺與 EXP、光箭靜音／恢復、非滿血治癒（#129）／PR #138
### T073 | done | codex-local | 原生畫面、滑鼠、HUD、陰影、聊天焦點、物件 AOI 與生命週期，含 P27 的 12 組計畫（R16、R20、R21、R22、A2、N02；#81 U05），以及 M1 移入的移動終點與 F9 圖示（#130）／PR #141
### T074 | done | codex-local | 正式世界 6016 場景音效 dispatch／重疊／退役（MAP-F5、N05、S2）（#145）／PR #149
### T075 | done | codex-local → dot | **M2 smoke**（#151／PR #152 等）：部分通過。✅ 登入、HUD／背包圖示、死亡鎖定與恢復、非滿血治癒與喝藥、點地與重登權威終點、聊天焦點。未過或未到 → M4：F9 空白（產品缺陷）、擊殺 EXP、學技能、弓箭、光箭靜音／恢復、NPC 商店與倉庫、普通肉循環、私商與交易、旅館、洞穴、船運、6016 音效

T070–T074 都是 **code 任務**：先唯讀查閱 `D:\lineage開發\1-伺服器` 的對應封包處理器，比對引擎現有實作，補齊缺漏並修正，以單元或離線測試證明；驗收是「編譯通過、測試綠燈、短報告」。真機只在 T075 做一次。

---

## M4 M2 補驗與修復 — 主線 2 項，封頂（✅ 結案，#177；T091 移入 M5）

**驗收：使用者打開會看到什麼**：`docs/demo/README.md` 的「M2 遊戲循環」一節，T075 中所有 ⚠️ 的列都在 M4 smoke 重新驗過，結果是 ✅ 附截圖，或 ⚠️ 附原因與 Backlog 編號。F9 面板可以載入並操作。

### T090 | done | codex-local | F9 面板：查明是截圖早於 ready，加 WebView2 回歸測試（#161／PR #168；U2 兌現）。M4 smoke 證實 ready 後完整載入
### T092 | done | dot | **M4 smoke**（#164／PR #174）：部分通過。✅ F9 載入與背包圖示、旅館租房入住離房。⚠️ 近戰 52 次未擊殺（疑似產品缺陷 → T093）、光箭有 C6 無 S30、商店／倉庫／肉、學技能、弓箭、私商與交易、洞穴離洞、船運、6016。GM 準備因 dot 執行權限未做（→ T091 移入 M5）

---

## M5 戰鬥修正與 M2 補驗 — 主線 3 項，封頂（✅ 結案，#206；剩餘列併入 M6 smoke T112）

**驗收：使用者打開會看到什麼**：`docs/demo/README.md`「M2 遊戲循環」一節中，M4 smoke 仍是 ⚠️ 的列，在 M5 smoke 重新驗過：✅ 附截圖，或 ⚠️ 附原因與 Backlog 編號。近戰可以擊殺並得到 EXP。

### T091 | done | dot | 測試條件準備：冷備份、GM 0→200、第二個角色「服務交易」、弓箭肉金幣、各地點座標（#175／PR #185；U3 兌現）
### T093 | done | codex-local | 近戰 2 格、光箭 10 格距離檢查，以及目標與傷害 trace（#173／PR #179；U2 兌現）
### T095 | done | dot | **M5 smoke**（#184）：部分通過。✅ 近戰命中（S30 傷害 14，T093 修正生效）、光箭三階段音量（0.2977／0／0.5521；M1 必驗的音量列至此全部 ✅）、GM 還原。⚠️ 擊殺 EXP、肉拾回、商店與倉庫、弓箭、學技能、洞穴、船運、6016、交易 → 併入 M6 smoke T112

---

## M3 可重現交付與文件 — 主線 4 項，封頂（✅ 全部完成）

**驗收：使用者打開會看到什麼（凍結；#99 依使用者「AI 能決定的不要問我」由調度器對話裁定 1-B、2-X 後修訂；#110 依 T034 結果收斂）**：
- 在兩個不同目錄各建置一次，`productDll` 的 SHA-256 相同（#91）。
- `docs/ARCHITECTURE.md` 一頁說明模組與資料流（#94）。
- **repo 的 main 不含遊戲資源檔（已知例外見下）**：發布查核（T034，`docs/reports/issue-102.md`）已列出全部 3,061 個追蹤檔的分類。其中 **25 個有本機等價檔的 D 類資源**（20 張 icons、2 張 ground PNG、2 個 WAV、`6016.json` fixture）都已從 main 移除，改成執行期從使用者本機的 `engine/assets` 或 `TW_CLIENT_ROOT` 讀取，功能不退化（離線測試加 check-only 通過）。CI 有拒收檢查，能擋下改了副檔名的圖片與音訊，以及 pixels JSON；EmbeddedResource 只允許現有的 4 個 logical name。
- **已知例外（#110 裁定，共 9 個）**：暫時留在 main，列入 B15。CI 以**精確路徑加 SHA** 鎖定，不能增加，也不能改內容。
  - 7 個需要生成器才能移出的衍生資料檔：2 個 pickup 像素 JSON、`OriginalExpTable.cs`、`ground-catalog.json`、2 個 metadata vectors、combat-sfx `expected-events.json`。
  - 2 個帶 `pixels` 陣列的歷史審查證據（#102 的 U14）：`work/P31-pickup-composite-review-r1/cases.json`、`work/engine-pickup-presentation-candidate-review-r1/execution-rows.json`。屬於歷史證據，不刪除。
  - T034 報告中其餘 unknown（主要是 `work/`、`docs/` 的歷史工作紀錄）不列入 M3 的移除範圍，但仍受 CI 拒收規則檢查：CI 一旦發現圖像或音訊本文，就交主控裁定，不能自動放行。git 歷史不改寫。`docs/demo/` 的真機截圖是使用者已授權公開的驗收證據（#71），不算遊戲資源檔；只含 ID、SHA、大小等非內容性 metadata 的 JSON 也不算，但要列入分類表。
- 原本的「可攜 zip、使用者本機產生素材」移到 Backlog B15（見 #97 設計）。

### T006 | done | codex-local | 位元可重現建置（#91／PR #92：停用 Source Link 與版本字串的 git revision）
### T080 | done | codex-local | 交作業版 `docs/ARCHITECTURE.md`（#94／PR #95）
### T034 | done | codex-local | 發布查核（#102）：3,061 檔分類，D 32（25 可立即移除、7 需生成器）、證據 85、unknown 2,502
### T035 | done | codex-local | 移除 25 個有本機等價檔的資源、改接本機來源、`.gitignore`、CI 拒收檢查（#109／PR #116）

---

## M6 內容擴充（Backlog 升級，使用者 #186 要求）— 主線 96 項，封頂（#238：U8 兌現 +7、#132 差異補列 +4、協議表未實作模組 +11；#385：+12；#418：+7；#427 原版查證與修正 +7；#485：原版對照修正與原版 UI +35）

**驗收：使用者打開會看到什麼**：`docs/demo/README.md` 新增「M6 內容擴充」一節。在 M6 smoke（T112）中，下列每一項都有真機截圖，或是 ⚠️ 附原因：
- 已學的非 1／3／4／8 技能可以施放；範圍法術與其他箭矢有畫面；
- 可以向 NPC 出售物品、向私商回購；
- 更多物品可以在地面顯示並拾取；
- 緩速時道具、地面與交談仍可依伺服器規則操作；
- 有場景音效與座標特效；
- 有多頻道聊天；
- 可以建立、刪除角色與切換角色；
- 有狀態圖示；
- 可以使用需要指定目標的道具；
- 傳送失敗時能恢復；
- 各職業的基本技能可以施放。

**做法**：依 `docs/design/m6-contract.md`，各任務只動自己模組的檔案，互不依賴，可以平行派工。佇列中隨時保持至少 5 張可派的 code 任務（使用者 #186）。

### T100 | done | codex-local | 技能施放通用化（#132 差異 3；B01 的基礎）
### T101 | done | codex-local | 法術與箭矢回包分派：S42 範圍法術、S30 多種 action、其他箭矢 gfx（#132 差異 4、5）
### T102 | done | codex-local | NPC 出售與私商回購（B05）
### T103 | done | codex-local | 地面物品通用化，以及三種停用物品的地面圖（#132 差異 9；B06）
### T104 | done | codex-local | 緩速時的道具、地面與交談政策（#132 差異 18；B02）
### T105 | done | codex-local | S22 音效、S106 座標特效、S55 變體（#132 差異 13、14；B04 的一部分）
### T106 | done | codex-local | 多頻道聊天：喊話、密語、血盟、隊伍（#132 差異 11）
### T107 | done | codex-local | 角色建立、刪除、同連線切角（#132 差異 10）
### T108 | done | codex-local | buff／狀態圖示（#132 差異 15）
### T109 | done | codex-local | 通用道具 use_type 尾段（#132 差異 8）
### T110 | done | codex-local | 傳送 recovery wrapper（N+8 bytes）相容（#132 差異 1）
### T111 | done | codex-local | 登入密碼字元規則對齊（#132 差異 6）
### T113 | done | codex-local | 王族基本技能（U8；#227）
### T114 | done | codex-local | 騎士基本技能（U8；#228）
### T115 | done | codex-local | 妖精基本技能（U8；#229）
### T116 | done | codex-local | 法師基本技能（U8；#230）
### T117 | done | codex-local | 黑暗妖精基本技能（U8；#231）
### T118 | done | codex-local | 龍騎士基本技能（U8；#232）
### T119 | done | codex-local | 幻術師基本技能（U8；#233）
### T120 | done | codex-local | S242 sub2 受擊反應抑制清單（#132 差異 12；#215）
### T121 | done | codex-local | 物品 status blob 語義化、權威裝備槽位（#132 差異 17；#216）
### T122 | done | codex-local | S206 單參數 overload 變體（#132 差異 16；#217）
### T123 | done | codex-local | 本機伺服器原始碼與 P24Lab d287 JAR 的封包類別對照（#132 差異 19；#226）
### T124 | done | codex-local | 記憶座標（書籤）列表、新增、刪除（#258）
### T125 | done | codex-local | 組隊與隊員 HP 條（#259）
### T126 | done | codex-local | 好友與封鎖名單（#260）
### T127 | done | codex-local | 寵物與召喚物控制（#261）
### T128 | done | codex-local | 血盟建立、加入、退出、盟徽（#262）
### T129 | done | codex-local | 變身 S_ChangeShape（#263）
### T130 | done | codex-local | 公告板（#266）
### T131 | done | codex-local | 信件（#267）
### T132 | done | codex-local | 決鬥（#272）
### T133 | done | codex-local | 刪除物品與給予物品（#273）
### T134 | done | codex-local | 武器修理與 PK 查詢（#274）
### T135 | done | codex-local | 盟徽查詢與 ID 對應（從 T128 拆出；等 T128 合併；#316）
### T136 | done | codex-local | 門（#322）
### T137 | done | codex-local | 稱號與表情動作（#323）
### T138 | done | codex-local | 角色設定存取（快捷列）（#324）
### T139 | done | codex-local | 釣魚（#325）
### T140 | done | codex-local | 查詢玩家與排名（#357）
### T141 | done | codex-local | 求婚與結婚（#358）
### T142 | done | codex-local | 能力值重置（#359）
### T143 | done | codex-local | 幽靈模式（#360）
### T144 | done | codex-local | 發送座標（#361）
### T145 | done | codex-local | 傳送門（踩點進入）（#377）
### T146 | done | codex-local | 血盟戰爭（等 T128 合併；#378）
### T147 | done | codex-local | 日夜、天氣與光源（#387）
### T148 | done | codex-local | 中毒、麻痺、詛咒盲目（#388）
### T149 | done | codex-local | 隱身（#389）
### T150 | done | codex-local | 小地圖（#390）
### T151 | done | codex-local | 復活確認與移動鎖定（#414）
### T152 | done | codex-local | 鑑定說明、紅字訊息、名稱狀態（#415）
### T153 | done | codex-local | 暫時能力、酒醉、水中呼吸、刪除技能（#416）
### T160 | done | claude-local | 查證原版：動作、表情與名稱繪製（#427；#428）
### T161 | done | claude-local | 查證原版：UI 版面與圖示（#427；#429）
### T162 | done | claude-local | 查證原版：光源、天氣與地圖事件（#427；#430）
### T163 | done | claude-local | 查證原版：音效播放策略（#427；#431）
### T164 | done | claude-local | 已完成 M6 功能的原版對照修正清單（等 T160–T163；#432）
### T165 | done | codex-local | 戰鬥音效補播 `<` 模式（依據 #417 `rea-inventory.md`；#433）
### T166 | done | codex-local | 地圖事件 `^` 環境音（B04；等 T162、T163；#434）
### T167 | done | claude-local | 找出光源圖與雨雪圖的原始位置（Tile.pak 加密）（#437）
### T168 | done | codex-local | 名稱、稱號、盟徽與名稱顏色對齊原版（T160 #7–#15、#19；G1、G4）（#440）
### T169 | done | codex-local | 表情對齊原版（G2）（#441）
### T170 | done | codex-local | 變身缺動作時退回，以及特殊外型（G3）（#442）
### T171 | done | codex-local | 解出 music.def 與 envsound.def 的內容（G9 查證）（#445）
### T172 | done | codex-local | 背景音樂、戰鬥音樂、登入音樂與地圖環境音（G9）（#446）
### T173 | done | codex-local | 音效共用層：距離衰減、全域冷卻、S22 模式、音量分類（G8）（#447）
### T174 | done | claude-local | 原版 800×600 UI 框架設計（`docs/design/native-ui.md`）（#448）
### T175 | done | codex-local | 聊天格式與顏色對齊原版（#449）
### T176 | done | codex-local | 快捷列 F5–F12 共 8 格，小地圖不再佔 F5（#450）
### T177 | done | codex-local | 日夜時鐘與明暗曲線對齊原版（G5 第一步）（#479）
### T178 | done | codex-local | 天氣對齊原版（G6）（#480）
### T179 | done | codex-local | 異常狀態外觀對齊原版（G7）（#481）
### T180 | done | claude-local | 查證原版：技能與法術表現（G10a）（#482）
### T181 | done | claude-local | 查證原版：視窗類功能（G10b）（#483）
### T182 | done | claude-local | 查證原版：世界物件類功能（G10c）（#484）
### T183 | done | 依計畫 | V1 查證：原版 HP／MP 浮動窗與頭上條的座標、尺寸、圖與顏色（#456）
### T184 | done | 依計畫 | V2 查證：原版紅字（畫面中央系統訊息）的位置、字色、時長與行數（#457）
### T185 | done | 依計畫 | V3 查證：舊封包狀態圖示的 surf 對照、畫面位置與排序（#458）
### T186 | done | 依計畫 | V4 查證：UI 框架層細節（.img 旗標與透明、按鈕狀態順序、XML 編碼、-c 退回、Sprite 重名優先序、字型）（#459）
### T187 | done | 依計畫 | V5 查證：底列行為（巨集 8 格座標、F1–F4 與 Map 快捷鍵、世界畫面範圍、小地圖內容、485 槽位切換）（#460）
### T188 | done | 依計畫 | C1 原生 UI 資源層：Tile.pak UI XML 解密與 Sprite .img 執行期讀取（#461）
### T189 | done | 依計畫 | C3 開發工具改為獨立視窗：網頁面板、服務分頁、狀態列移出遊戲畫面，開發快捷鍵讓出 F1–F12（#462）
### T190 | done | 依計畫 | C2 原生 UI 畫布：800×600 邏輯畫布、整數倍置中縮放、輸入對映、IUiPanel 契約與 nativeUi 開關（#463）
### T191 | done | 依計畫 | 原生 UI 模組：左狀態框 LEFT_WINDOW（等級／EXP、AC、MR、陣營、SP、負重、飽食、金幣）（#464）
### T192 | done | 依計畫 | 原生 UI 模組：HP／MP 浮動窗與頭上條（#465）
### T193 | done | 依計畫 | 原生 UI 模組：巨集框 F5–F12 共 8 格、3 個分頁、鎖定，以及擴充巨集（#466）
### T194 | done | 依計畫 | 原生 UI 模組：巨集框選單按鈕 Map／Mail／Status／Spell／Item／Pledge／Option／Quit（#467）
### T195 | done | 依計畫 | 原生 UI 模組：聊天窗 CENTER_WINDOW（36 px 伸縮、頻道分頁、過濾、輸入列、左右分割）（#468）
### T196 | done | 依計畫 | 原生 UI 模組：底列小地圖與 Field_Map 小窗（Map 按鈕開關，讓出 F5）（#470）
### T197 | done | 依計畫 | 原生 UI 模組：隊伍底板 PARTY_INTERFACE 與右側隊伍／寵物窗（#471）
### T198 | done | 依計畫 | 原生 UI 模組：書籤視窗 BookmarkUI（263×420 可拖曳）（#472）
### T199 | done | 依計畫 | 原生 UI 模組：狀態圖示（依舊封包對照）（#473）
### T200 | done | 依計畫 | 原生 UI 模組：紅字（畫面中央系統訊息）（#474）
### T201 | blocked | 依計畫 | 原生 UI 真機 smoke：P24Lab 測試角色開 nativeUi 跑一輪（#475）
### T112 | blocked | codex-local → dot | **M6 smoke**：上述合併後，真機跑一次 M6 驗收清單，**連同 M5 剩下的 M2 列**（#206）；T113–T182 也要合併後才開（原版 UI T183–T201 有自己的 smoke T201，不擋 T112）；T124–T182 在 smoke 清單中列為附加列，不改上方凍結的驗收。**另等 T164 的對照清單與其修正任務合併後才開（#427）**

---

## M7 重建補缺（使用者 2026-11-08 17:53，#503）— 主線 239 項，封頂（#535：U9 兌現 +25，原估最多 +40；#569：依 T225–T227 查證補 +16；#657：第二輪與查證補 +29；#727：第三、四輪與查證補 +29；#894：第五至第七輪、參考客戶端 P 組、R6–R10 與查證補 +79；#982：第八、九輪、FU10–FU17 查證、R10–R11 與 Q8 code、smoke 腳本 +44；#1023：R12、CU、Q8／Q9 後續、R13-A、第十輪與結案覆核 +16）

**驗收：使用者打開會看到什麼**：`docs/reference/gap-matrix.md` 列出原版每個功能在引擎中的狀態（已有／部分／缺）；依它開出的 code 任務全部合併後，「缺」與「部分」只剩標明「需先查證」或「伺服器不支援」的列，且每列都附原因。

**做法**：T202 先產出 gap-matrix，主控依它的分組開 code 任務。契約先行、模組平行，每張只動列出的檔案，並引用對應的 reference 文件。佇列隨時保持 ≥4 張可派的任務。審查與驗證暫緩（見上方暫行政策）。

### T202 | done | claude-local | 差距盤點：原版功能對照引擎，產出 `docs/reference/gap-matrix.md`（#504）
### T203 | done | claude-local | M7-A 接收與快照擴充點（#509）
### T204 | done | claude-local | M7-B 能力旗標與 SP／MR（#510）
### T205 | done | claude-local | M7-C 城堡：城主、稅率、金庫（#511）
### T206 | done | claude-local | M7-D 盟屋清單與地圖（#512）
### T207 | done | claude-local | M7-E 數量與文字輸入詢問（#513）
### T208 | done | claude-local | M7-G 以物品學魔法（#514）
### T209 | done | claude-local | M7-H 血盟推薦（#515）
### T210 | done | claude-local | M7-J 通用 Yes／No（聊天輸入列）（#516）
### T211 | done | claude-local | M7-K 交易詢問與交易流程（#517）
### T212 | done | claude-local | M7-L 拾取對齊原版（#518）
### T213 | done | claude-local | M7-M 門：自動走近（#519）
### T214 | done | claude-local | M7-N 幽靈：本機限制（#520）
### T215 | done | claude-local | M7-O 發送座標對齊原版（#521）
### T216 | done | claude-local | M7-P 技能特效時機與飛行（#522）
### T217 | done | claude-local | M7-Q 施法：拿掉本地預測、冷卻改用影格總長（#523）
### T218 | done | claude-local | M7-R HTML 結果改走 NPC 對話窗（#524）
### T219 | done | claude-local | M7-S 商店與修理改用 TRADE_WINDOW（#526）
### T220 | done | claude-local | M7-T 個人商店開店設定窗（#527）
### T221 | done | claude-local | M7-U 公告板原生窗（#528）
### T222 | done | claude-local | M7-V 好友、封鎖、信件封鎖三窗（#529）
### T223 | done | claude-local | M7-W 寵物：召喚選擇與指令目標（#530）
### T224 | done | claude-local | M7-X 死亡統計窗與逾時離線（#531）
### T225 | done | claude-local | 查證原版：協定空白（S_EXTENDED 子碼、訊息字串表、版本檢查、未知封包）（#532）
### T226 | done | claude-local | 查證原版：視窗世代與待查視窗（#533）
### T227 | done | claude-local | 查證原版：地圖特效、子特效、水中呼吸與座標特效（#534）
### T228 | done | claude-local | 伺服器訊息改用完整字串表（#546）
### T229 | done | claude-local | S250 聊天訊息類子碼（#547）
### T230 | done | claude-local | S250 狀態圖示類子碼（#548）
### T231 | done | claude-local | C26 聊天頻道開關（#549）
### T232 | done | claude-local | S187 盟屋地圖窗（AgitMapWindow）（#550）
### T233 | done | claude-local | 送 C14 並解析 S139（#551）
### T234 | done | claude-local | 伺服器主動送的 S39 頁（#556）
### T235 | done | claude-local | 城堡 HTML 頁與城主王冠（#557）
### T236 | done | claude-local | 聊天輸入列的 YES／NO 鈕（#558）
### T237 | done | claude-local | 料理窗資料（#559）
### T238 | done | claude-local | 血盟倉庫紀錄（#560）
### T239 | done | claude-local | 血盟成員窗（#561）
### T240 | done | claude-local | 舊式背包、魔法、倉庫原生窗（#562）
### T241 | done | claude-local | 光照圖接上畫面與地圖特效光源表（#566）
### T242 | done | claude-local | `]` 子特效與淡入淡出（#567）
### T243 | done | claude-local | 水中呼吸的狀態圖示（#568）
### T244 | done | claude-local | gap-matrix 第二輪——依目前的 main 重新盤點，列出下一批任務（#601）
### T245 | done | claude-local | 查證原版——S250 視窗類子碼（B3）與任務窗（#602）
### T246 | done | claude-local | S250 畫面中央訊息與畫面效果（PBW-A）（#607）
- T247（併入，不計）：~~原生「離開遊戲」窗 QUIT_WINDOW（PBW-B）~~ 併入 R2-D（T254），不另計（#608）
### T248 | done | claude-local | 血盟公告與成員備註更新（PBW-C）（#609）
- T249（併入，不計）：~~盟徽顯示旗標（PBW-D）~~ 併入 R2-K（T264），不另計（#610）
### T250 | done | claude-local | 寵物 AC 與裝備欄（PBW-E）（#611）
### T251 | done | claude-local | R2-A 環境表現接上畫面（#612）
### T252 | done | claude-local | R2-B 聲音接上產品（#613）
### T253 | done | claude-local | R2-C 點擊與拾取收尾（#614）
### T254 | done | claude-local | R2-D 幽靈、死亡窗與離開窗（#615）
### T255 | done | claude-local | R2-G 聊天收尾（#616）
### T256 | done | claude-local | R2-H 技能表現收尾（#617）
### T257 | done | claude-local | R2-I 物件繪製收尾（#618）
### T258 | done | claude-local | R2-M 發送座標收尾（#619）
### T259 | done | claude-local | R2-N 個人商店窗接上產品（#620）
### T260 | done | claude-local | R2-Q 登入公告顯示（#621）
### T261 | done | claude-local | R2-F NPC 對話面板收尾（#622）
### T262 | done | claude-local | R2-E 商店改用 `TRADE_WINDOW`（#623）
### T263 | done | claude-local | R2-J Yes/No 收尾（#624）
### T264 | done | claude-local | R2-K 血盟收尾（#625）
### T265 | done | claude-local | R2-L 狀態圖示資料收尾（#626）
### T266 | done | claude-local | 查證原版——TRADE_WINDOW 各模式版面與技能飛行（Q-1＋Q-4）（#627）
### T267 | done | claude-local | 查證原版——圖源與字串（Q-2）（#628）
### T268 | done | claude-local | 查證原版——送出點與欄位（Q-3）（#629）
### T269 | done | claude-local | 技能飛行 kind 6／7 與子特效 ] 的範圍（Q-4 補做）（#643）
### T270 | done | claude-local | 測試與註冊表改為自動發現（減少共用檔案衝突）（#645）
### T271 | done | claude-local | rebuild-lock 改成合併後才重產，PR 不再修改 lock（#646）
### T272 | done | claude-local | Q-2 補做——城主王冠、死亡統計值、座標與地圖字串（#649）
### T273 | done | claude-local | 伺服器踢除時顯示模態訊息框（S_KICK）（#651）
### T274 | done | claude-local | 測試結果隨環境不同——S245 與 attack-skill-resources（#655）
### T275 | done | claude-local | 查證原版——前幾輪留下的未驗證細項（SpellList id、王冠可見、傳送與召喚窗等）（#665）
### T276 | done | claude-local | 控制戒指開窗（傳送選書籤、召喚選單）與盟屋地圖 674 鈕（#667）
### T277 | done | claude-local | gap-matrix 第三輪——依目前的 main 重新盤點，列出第三批任務（#668）
### T278 | done | claude-local | R3-B 寵物：召喚備援與 `PetControl`（#671）
### T279 | done | claude-local | R3-C 料理窗與血盟倉庫紀錄窗（#672）
### T280 | done | claude-local | R3-D 聲音收尾（#673）
### T281 | done | claude-local | R3-E 交易窗 `TRANSACTION_WINDOW`（#674）
### T282 | done | claude-local | R3-G 聊天收尾二（#675）
### T283 | done | claude-local | R3-H 盲目光源與酒醉、隱身細項（#676）
### T284 | done | claude-local | R3-I 視窗堆疊 4 層上限（#677）
### T285 | done | claude-local | R3-A 原生舊式窗接上產品（#678）
### T286 | done | claude-local | R3-F 學魔法窗改用 `TRADE_WINDOW` 模式 7（#679）
### T287 | done | claude-local | 查證原版——第三輪細項（開窗熱鍵、酒醉、光效、隱身例外、倉庫紀錄窗、頭目音樂、聊天第二色）（#680）
### T288 | done | claude-local | Q3 補做——血盟倉庫紀錄原生窗、隱身只限同隊、酒醉註解（#689）
### T289 | done | claude-local | 查證原版——名稱旁圖示、S250 27／30／31、光效 +5、物品欄位 +36（#690）
### T290 | done | claude-local | T272 補做——城主王冠繪製、死亡統計 Reset 與道具窗、地名備援、座標聊天行（#698）
### T291 | done | claude-local | gap-matrix 第四輪——盤點剩餘缺口，列出第四批任務與原生 UI 預設開啟前的缺口（#701）
- T292（併入，不計）：~~S250 子碼 27／30／31 顯示，以及光效亮度 +5 的觸發~~ 併入 R4-G（T298）、R4-H（T299）（#703）
### T293 | done | claude-local | R4-A 選單鈕、熱鍵與信件入口（#705）
### T294 | done | claude-local | R4-B 背包、魔法、倉庫原生窗畫內容（#706）
### T295 | done | claude-local | R4-D 料理窗內容與備援（#707）
### T296 | done | claude-local | R4-E 血盟窗收尾（#708）
### T297 | done | claude-local | R4-F 狀態圖示與寵物資料接上（#709）
### T298 | done | claude-local | R4-G 技能提亮與舊素材 kind 分類（#710）
### T299 | done | claude-local | R4-H S250 27／30／31 印聊天欄（#711）
### T300 | done | claude-local | R4-I 原生 `HYPER_TEXT_WINDOW`（#712）
### T301 | done | claude-local | R4-J 原生書籤窗（#713）
### T302 | done | claude-local | R4-K 聲音：播放中隨距離改音量（#714）
### T303 | done | claude-local | R4-C 交易、商店、修理、學魔法窗收尾（#715）
### T304 | done | claude-local | 查證原版——第四輪細項 Q4-3 到 Q4-9（血盟關注、盟戰、求婚、盟屋窗、代碼聊天名稱、世界地圖）（#716）
### T305 | done | claude-local | gap-matrix 第五輪——剩餘缺口、原生 UI 缺口更新，以及 M7 是否可以結案（#730）
### T306 | done | claude-local | R5-A 原生 UI 開關收尾（#733）
### T307 | done | claude-local | R5-B 原生書籤窗收尾（#734）
### T308 | done | claude-local | R5-C 特效收尾（#735）
### T309 | done | claude-local | R5-D 小協定差異（#736）
### T310 | done | claude-local | R5-E 聲音收尾（#737）
### T311 | done | claude-local | R5-F 狀態圖示收尾（#738）
### T312 | done | claude-local | R5-G 原生公告板內容（#739）
### T313 | done | claude-local | R5-H 地圖窗發送座標（#740）
### T314 | done | claude-local | R5-I `QUIT_WINDOW` 備援（#741）
### T315 | done | claude-local | 查證原版——Q5-1 到 Q5-5，以及複核 K6 的 19 列（#742）
### T316 | done | claude-local | Q4 補做——血盟關注、盟戰與求婚的入口、代碼聊天名稱、盲目職業加成（#746）
### T317 | done | claude-local | 原生 UI 改為預設開啟（使用者 #743 決定 1）（#751）
### T318 | done | claude-local | 用 PakViewer 查遍所有 .pak——K3「缺資源」18 列與先前標「本機沒有」的檔案（#752）
### T319 | done | claude-local | P-A 參考客戶端 UI XML 來源（#757）
### T320 | done | claude-local | P-C `.tbt` surf 解碼（#758）
### T321 | done | claude-local | P-E 名稱與稱號字型改用原版（#759）
### T322 | done | claude-local | P-B 參考客戶端 surf 備援（#760）
### T323 | done | claude-local | P-D 原生盟屋拍賣窗（參考客戶端 XML）（#761）
### T324 | done | claude-local | P-D 原生信件三窗（參考客戶端 XML）（#762）
### T325 | done | claude-local | P-D 原生能力值重置與能力資訊窗（參考客戶端 XML）（#763）
### T326 | done | claude-local | P-D 原生登入畫面（參考客戶端 XML）（#764）
### T327 | done | claude-local | P-D 原生選角與建角畫面（參考客戶端 XML）（#765）
### T328 | done | claude-local | P-D 原生狀態窗（參考客戶端 XML）（#766）
### T329 | done | claude-local | P-D 原生釣魚窗（參考客戶端 XML）（#767）
### T330 | done | claude-local | P-D 原生選項窗（參考客戶端 XML）（#768）
### T331 | done | claude-local | P-D 原生排名窗（參考客戶端 XML）（#769）
### T332 | done | claude-local | R6-A 封包特效與地圖單次音效（#778）
### T333 | done | claude-local | R6-C 私商 `TRADE_WINDOW` 13／14（#779）
### T334 | done | claude-local | R6-D 鍵盤拾取與幽靈快捷（#780）
### T335 | done | claude-local | R6-E 聊天指令與本地確認（#781）
### T336 | done | claude-local | R6-F 料理窗與地裂深度（#782）
### T337 | done | claude-local | 查證原版——Q6-1、Q6-2、Q6-4 到 Q6-9（頭上條基準、決鬥送出點、BehaviourUI 等）（#783）
### T338 | done | claude-local | R6-B 自己的名稱位置（#784）
### T339 | done | claude-local | gap-matrix 第六輪——原生 UI 預設開啟後重新判定、M7 能否結案、真機檢查清單（#800）
### T340 | done | claude-local | R7-A 戰鬥排隊、S42 光照、選目標游標（#803）
### T341 | done | claude-local | R7-B 輸入列 Y/N 與表情保留（#804）
### T342 | done | claude-local | R7-C 戒指與盟屋拍賣（#805）
### T343 | done | claude-local | R7-D HTML 動作（#806）
### T344 | done | claude-local | R7-E 狀態圖示變體（#807）
### T345 | done | claude-local | R7-F 公告板收尾（#808）
### T346 | done | claude-local | R7-G 私商提示（#809）
### T347 | done | claude-local | R7-H 視窗堆疊與 ESC（#810）
### T348 | done | claude-local | R7-I 選單鈕 2／Ctrl+A 與狀態窗（#811）
### T349 | done | claude-local | R7-J 隊伍委任（#812）
### T350 | done | claude-local | R7-K 血盟備註（#813）
### T351 | done | claude-local | R7-L 聲音收尾 3（#814）
### T352 | done | claude-local | R7-M 登入畫面收尾（#815）
### T353 | done | claude-local | R7-O WidgetSlot 外觀（#816）
### T354 | done | claude-local | R7-N 參考客戶端字串表（#817）
### T355 | done | claude-local | 查證原版——Q7-1 到 Q7-14（第六輪需先查證的 36 列）（#818）
### T356 | done | claude-local | R8-A 頭上條（#820）
### T357 | done | claude-local | R8-B 鑑定訊息（#821）
### T358 | done | claude-local | R8-C 決鬥指令（#822）
### T359 | done | claude-local | R8-D 動作窗（#823）
### T360 | done | claude-local | R8-E list.spr 附加層（#824）
### T361 | done | claude-local | R9-A 書籤窗原版操作（#845）
### T362 | done | claude-local | R9-B 地圖窗右鍵選單與存點窗（#846）
### T363 | done | claude-local | R9-C 聊天指令與 S161 文字（#847）
### T364 | done | claude-local | R9-D S55 不跟隨特效（#848）
### T365 | done | claude-local | R9-E HyperText 小組（#849）
### T366 | done | claude-local | R9-F HyperText 排版器（#850）
### T367 | done | claude-local | R9-G 學魔法圖示（#851）
### T368 | done | claude-local | R9-H 好友／封鎖窗事件（#852）
### T369 | done | claude-local | R9-I 私商查詢頁（#853）
### T370 | done | claude-local | R9-J 個人商店流程（#854）
### T371 | done | claude-local | R9-K 交易數量（#855）
### T372 | done | claude-local | R9-L 背包操作（#856）
### T373 | done | claude-local | R9-M 召喚窗收尾（#857）
### T374 | done | claude-local | R9-N 公告板上一篇／下一篇（#858）
### T375 | done | claude-local | R9-O 選角／建角（#859）
### T376 | done | claude-local | R9-P 釣魚窗（#860）
### T377 | done | claude-local | R9-Q 選項窗（#861）
### T378 | done | claude-local | R7 併入項 179 字串提示與狀態窗 Up 確認（#862）
### T379 | done | claude-local | gap-matrix 第七輪與 M7 結案判定（#880）
### T380 | done | claude-local | 查證原版——FishObtainUI 釣魚物品表（#884）
### T381 | done | claude-local | 查證原版——HyperText 顏色位元組順序與連結色（#886）
### T382 | done | claude-local | R10-A FishObtainUI 清單（背包尾段旗標＋desc id 表）（#888）
### T383 | done | claude-local | R10-B HyperText 顏色修正（FU11 D1–D7）（#891）
### T384 | done | claude-local | 查證原版——3.81 倉庫數量輸入（1:127）與 3.81 端未決問題（#896）
### T385 | done | claude-local | 查證原版——真機項目 M35／M37／M39／M40 的靜態部分（#898）
### T386 | done | claude-local | 查證原版——list.spr altdir／stride／cloth 的接線時機（#899）
### T387 | done | claude-local | R10-C 好友／封鎖宿主（#902）
### T388 | done | claude-local | R10-D 組隊邀請點選（#903）
### T389 | done | claude-local | R10-E 動作窗橋接（#904）
### T390 | done | claude-local | R10-F 聊天列確認與提示（#905）
### T391 | done | claude-local | R10-G 表情保留範圍（#906）
### T392 | done | claude-local | R10-H 名稱內插與頭上條（#907）
### T393 | done | claude-local | R10-I 選項窗收尾（#908）
### T394 | done | claude-local | R10-J 書籤窗收尾（#909）
### T395 | done | claude-local | R10-K 拾取通行判斷（#910）
### T396 | done | claude-local | R10-L MIDI 換曲（#911）
### T397 | done | claude-local | R10-M 個人商店收尾（#912）
### T398 | done | claude-local | R10-N 選角／建角收尾（#913）
### T399 | done | claude-local | R10-O 召喚與盟屋拍賣小項（#914）
### T400 | done | claude-local | R10-P 倉庫列內數量（#915）
### T401 | done | claude-local | R10-Q 個人商店頭上訊息兩行輪流（#916）
### T402 | done | claude-local | 查證原版——Q8（StatInfo 數值與第七輪子項）（#917）
### T403 | done | claude-local | R10 小清理（刪除取消、227 開框、InputHelp、過時註解）（#918）
### T404 | done | claude-local | 真機前置——`--evidence` 時把 `[native-ui]` log 寫進 evidence 目錄（#919）
### T405 | done | claude-local | R10-R list.spr 附加層接線（altdir／stride／cloth）（#922）
### T406 | done | claude-local | R10-S 選目標瞄準游標（FU13 D1–D2）（#923）
### T407 | done | claude-local | R10-T 召喚窗停用格色塊（FU13 D6–D10）（#924）
### T408 | done | claude-local | R10-U 登入畫面 Back_Image 動畫（FU13 D11–D12）（#925）
### T409 | done | claude-local | R10-V 好友窗入口與 S39 buddy 頁（FU13 D3–D5）（#926）
### T410 | done | claude-local | 真機 smoke 腳本整合（#475、T112、M1–M40）（#944）
### T411 | done | claude-local | 一般產品入口接受 `--evidence <dir>`（#948）
### T412 | done | claude-local | gap-matrix 第八輪與 M7 結案判定（#952）
### T413 | done | claude-local | `--classic-ui` 備援盤點（#953）
### T414 | done | claude-local | 查證原版——Q9-1 頭上對話框版面（#956）
### T415 | done | claude-local | R11-A 選目標模式共用游標（#957）
### T416 | done | claude-local | R11-B 動作窗收尾（#958）
### T417 | done | claude-local | R11-C 好友 HyperAct（#959）
### T418 | done | claude-local | R11-D 選角空格／建角雙擊（#960）
### T419 | done | claude-local | R11-E 頭上對話框（#961）
### T420 | done | claude-local | R11-F 分開音量（#962）
### T421 | done | claude-local | R11-G 小清理（不對應列）（#963）
### T422 | done | claude-local | 查證原版——Q9-2 到 Q9-5（書籤紀錄、WidgetSlot 元件、HTML 標記、備註 *）（#966）
### T423 | done | claude-local | Q8-A 聊天列提示（血盟備註、委任）（#967）
### T424 | done | claude-local | Q8-B① 盟屋拍賣出價步進（#968）
### T425 | done | claude-local | Q8-B② WidgetSlot 模式切換（#969）
### T426 | done | claude-local | Q8-C 小項（470 圖示、信件提示、公告板等待、ESC 順序）（#970）
### T427 | done | claude-local | gap-matrix 第九輪與 M7 結案判定（#980）
### T428 | done | claude-local | R12-A 自己的動作 71 與層半透明（#985）
### T429 | done | claude-local | R12-B 出價窗收尾（#986）
### T430 | done | claude-local | R12-C 類別音量存檔（#987）
### T431 | done | claude-local | R12-D 信件結果字串（#988）
### T432 | done | claude-local | R12-E 公告板等待收尾（#989）
### T433 | done | claude-local | CU-1 classic 入口可見（G1、G2、G6、G7、G8）（#991）
### T434 | done | claude-local | CU-2 classic 功能補洞（G3、G4、G5、G9、G10、G11）（#992）
### T435 | done | claude-local | CU-3 原生讓位收尾（G12–G15）（#993）
### T436 | done | claude-local | CU-4 故障備援一致（G16–G19）（#994）
### T437 | done | claude-local | Q8-A① 補充 血盟備註不雅字過濾（#1008）
### T438 | done | claude-local | Q9-C HTML `<date>`／`<time>`／var 998–999（#1009）
### T439 | done | claude-local | Q9-D HTML `<var>` 選項值（3.81 範圍）（#1010）
### T440 | done | claude-local | Q9-A 書籤區域群組（參考客戶端 zone3）（#1011）
### T441 | done | claude-local | gap-matrix 第十輪與 M7 結案判定（#1012）
### T442 | done | claude-local | R13-A 本機選項頁可操作（#1021）
### T443 | doing | claude-local | M7 結案覆核——R13-A 合併後重算 gap-matrix（15.16）（#1024）

---

## Backlog（不計進度；升級成主線須砍一項主線或經使用者同意）

| ID | 內容 | 來源 | 為什麼不在主線 |
|---|---|---|---|
| B01 | ~~技能從 1／3／4／8 擴充到各職業基本技能~~ → 升級為 M6（T100、各職業 T113 起，#186） | T030、R18、S03 | 需要逐一指定 skill／gfx 來源；demo 不依賴 |
| B02 | ~~Slow 速度拾取／丟棄~~ → 升級為 M6 T104（#186） | T031 | demo 用一般速度 |
| B03 | MAP-F4 map-local effect group fade | T032、S01 | **結案（#417）**：原生鏈存在，但本機 7,807 個 `.s32` 的地圖特效延伸記錄為 0 筆，目前資料不會觸發 |
| B04 | 6016 以外的地圖動畫事件 | S02 | 主要部分已解開（#417）：`<`、`[`、`^` 語義已從機器碼取得；`^` 環境音升為主線 T166 |
| B05 | ~~NPC 正向出售、私商正向回購~~ → 升級為 M6 T102（#186） | R07、R10、C01 | 需要伺服器端有合法報價或買家條件，不屬於產品缺陷 |
| B06 | ~~終極治癒藥水、自我加速藥水、回家卷軸的地面圖~~ → 升級為 M6 T103（#186） | #51 | #48 缺 sprite pin，目前停用 |
| B07 | 原生分類全鏈、音效原生選擇語義 | #47、#48 | **受阻理由改寫（#417）**：Themida 只限 `LiTo.bin`；歷史地址屬於未加殼的 `TW1608262200.bin`，parser、frame 結構、音效選擇、分類讀取者、dispatch tables 都已靜態取得（`docs/reference/rea-inventory.md`）。剩 S87 同身份入口未定位。音效選擇已升為主線 T165 |
| B08 | r34 正式封裝與入口切換 | I01 | 由 M3 的 dev 交付取代 |
| B09 | P27 原始碼審查的最小修正 | I02 | 沒有確認的缺陷；若 T073 重現再升級 |
| B10 | 原 BIN 歷史限制、舊鑰匙刪除時刻 | R15、H01 | 歷史資料不可回補 |
| B11 | 調度器流程改善（runner 權限、收割器、路由） | #21、#25、#62、#74 | 屬於調度器 repo，不屬於本專案進度 |
| B12 | 舊 EXE 原版 11 步全流程基準 | #81 H02（P10） | 本專案目標是**不依賴舊 BIN** 的自主引擎，舊 EXE 不作為驗收 |
| B13 | TW1608262200 原 BIN 完整協議相容（d287 JAR 以外的伺服器） | #81 H03（P07、P11、P12） | 引擎只對齊目前的 P24Lab d287 JAR 伺服器；擴充到其他伺服器版本另行決策 |
| B14 | 金幣在本伺服器不可丟棄 | #77 | 伺服器規則（`DORP_ITEM`／白名單）；不改 DB 或配置，demo 改用替代物品 |
| B15 | 可攜 zip 加使用者本機產生 `engine/assets`（原 T033），以及移除 9 個已知例外（7 個需生成器的衍生資料檔加 2 個歷史證據，#102、#110） | #97、#99、#102 | 轉換鏈依賴伺服器 SQL／地圖、研究工具，估 20–36 小時，且只靠客戶端能否重建未證實；設計見 `docs/design/portable-delivery.md`，缺口逐檔見 `docs/reports/issue-102.md` |
| B16 | 登入、選角、伺服器名稱、讀取畫面的原版 XML（`LoginUI`、`SelectUI`、`ServerName`、`LoadingScene`） | T227（#534） | **缺資源**：本機所有版本客戶端都沒有；保留現有畫面，取得資源後再開任務 |
