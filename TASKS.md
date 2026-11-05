# TASKS — tw-bin-engine

更新：2026-11-06（#76 範圍凍結）。格式：`### ID | 狀態 | 負責 | 範圍`。狀態只用 todo／doing／review／done／blocked。

**計算規則（凍結）**：
- 調度台的進度條**只算 `## M*` 區段**；`## Backlog` 只顯示數量，不算進度。
- 每個里程碑都標了「主線 N 項，封頂」。之後新發現的工作一律先放進 Backlog。要升級成主線，必須同時砍掉一項主線，或經使用者同意。
- 分母只允許因 `PLAN.md`「盤點：已知範圍」中列出的**不確定項**兌現而變動，兌現時要在 PR 說明中引用當初的估算。
- 每個里程碑的「驗收：使用者打開會看到什麼」寫完後就凍結。要改必須開新 Issue 說明理由，並標 `needs:user`。

---

## M0 可重現建置 — 主線 12 項，封頂（✅ 全部完成）

**驗收：使用者打開會看到什麼（凍結）**：
- **前置條件**：Windows 機器上有原客戶端資源目錄（`TW_CLIENT_ROOT`，預設 `D:\lineage開發\0-客戶端`），並且依 `docs/BUILD.md` 接好 `engine/assets` junction，安裝 SDK 10.0.300 與 Python 3.12。
- 在這個前提下，從**新建的 clone 或 worktree** 照 `docs/BUILD.md`／`docs/demo/RUN.md` 執行一條建置指令，會輸出 `PASS dev {…}`；`engine\啟動新版客戶端.cmd --check-only` 輸出的 JSON 是 `revision: dev`、`uiStarted: false`；GitHub Actions 綠燈。
- 證據：#13（在新建的 worktree `scratch/wt/task-13` 接好 junction 後，完整建置加 check-only 都 exit 0）、#38、#70（主專案目錄 dev 建置加 check-only）、CI（#67）。

### T001 | done | Claude | CI：GitHub Actions dotnet build
### T002 | done | Claude | r35 canonical lock（181 檔）
### T003 | done | Claude | 去除硬編路徑（TW_ENGINE_ROOT／TW_CLIENT_ROOT）
### T004 | done | codex-local | `docs/BUILD.md` 乾淨機器建置文件（#6）
### T005 | done | Claude | CLAUDE／AGENTS／cursor 規則與狀態同步
### T013 | done | codex-local | 本機 r35 完整建置（#7）
### T014 | done | codex-local | rebuild.py 允許 assets junction（#12）
### T015 | done | codex-local | 本機 r35 建置加 check-only 通過（#13）
### T016 | done | codex-local | 主專案目錄 r35 建置（#19）
### T040 | done | codex-local | 滾動式 dev lock（#31）
### T041 | done | codex-local | 選用封包追蹤，只記 op／len（#34）
### T050 | done | codex-local | CI 改在 Linux 交叉建置 Windows 目標（#67／PR #72）

---

## M1 打開就看得到的 Demo — 主線 13 項，封頂

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
### T062 | doing | codex-local → dot | DEMO-3（#77）：**一張**修正加補測任務，依 DEMO-2 報告處理：
1. codex-local 修**金幣丟撿未通過**的產品原因，以及 DEMO-2 報告中其他屬於產品的缺陷，附離線回歸測試。
2. dot 補驗 DEMO-2 尚未通過的列：移動座標、近戰 EXP（固定目標）、光箭、非滿血治癒、卸裝截圖、金幣丟撿、聊天、各事件音效峰值。
3. 更新 `docs/demo/README.md`，直到符合本里程碑驗收。

依賴：T061 推送。不確定項 U1 兌現時（見 PLAN），依估算增列修正任務。

---

## M2 完整遊戲循環 — 主線 5 項，封頂

**驗收：使用者打開會看到什麼（凍結）**：`docs/demo/README.md` 新增「M2 遊戲循環」一節。每一項都有真機截圖、前後數值，以及 ✅ 或「❌ 加 Backlog 原因」：
- **NPC**：向 NPC 買一件物品（金幣減少、物品入袋）、倉庫存入再取出、私商買一件、兩個角色直接交易一件。
- **跨圖**：洞穴進出、往返船運各一趟、旅館入住，換圖後畫面與座標正確，回原圖不殘留。
- **戰鬥閉環**：打死怪物，得到自然 EXP 與戰利品並撿起；向 NPC 學一個技能並施放。
- **原生 UI**：滑鼠點地移動、點怪攻擊、點 NPC 交談；HUD、快捷槽、陰影、聊天焦點顯示正確。
- **場景音效**：map4 港口的 6016 場景音效有聲（峰值 > 0），離開後停止。

### T070 | todo | dot（加 codex-local 修正） | NPC 服務：購買、倉庫、私商購買、直接交易（R06、R08、R09、R11、N03）
### T071 | todo | dot（加 codex-local 修正） | 跨圖連續性：洞穴、船運、旅館、地圖特效實機（R12、R13、R14、MAP-F9、N04）
### T072 | todo | dot（加 codex-local 修正） | 戰鬥閉環：自然 EXP、戰利品、技能學習（R03、R04、A1）
### T073 | todo | dot（加 codex-local 修正） | 原生畫面、滑鼠、HUD、陰影、聊天焦點，含 P27 的 12 組計畫（R16、R20、R21、R22、A2、N02）
### T074 | todo | dot（加 codex-local 修正） | 正式世界 6016 場景音效 dispatch／重疊／退役（MAP-F5、N05、S2）

每張都是「真機驗收 → 發現的缺陷在**同一張**任務內交給 codex-local 修正 → 再驗收」。

---

## M3 可攜交付 — 主線 3 項，封頂

**驗收：使用者打開會看到什麼（凍結）**：
- GitHub Release 有一個 zip，**只含**引擎與 launcher 的執行檔、web 頁面、說明文件，**不含任何遊戲資源**（pak／map／sprite／sound，以及由它們衍生的 `engine/assets`）。
- **前置條件**：使用者自己有原客戶端資源目錄。在一台沒有開發環境的 Windows（只裝 .NET 10 Desktop Runtime 與 WebView2）上解壓 zip，照 zip 內的 `README` 指定資源目錄，執行啟動檔，就出現登入畫面。
- `docs/ARCHITECTURE.md` 一頁說明模組與資料流。
- 在兩個不同目錄各建置一次，`productDll` 的 SHA-256 相同。

### T006 | todo | codex-local | 位元可重現建置（Deterministic／PathMap；不同目錄產出相同 SHA）
### T033 | todo | codex-local | 可攜 zip（不含遊戲資源）加 GitHub Release；資源由使用者的客戶端目錄提供
### T080 | todo | codex-local | 交作業版 `docs/ARCHITECTURE.md`

---

## Backlog（不計進度；升級成主線須砍一項主線或經使用者同意）

| ID | 內容 | 來源 | 為什麼不在主線 |
|---|---|---|---|
| B01 | 技能從 1／3／4／8 擴充到各職業基本技能 | T030、R18、S03 | 需要逐一指定 skill／gfx 來源；demo 不依賴 |
| B02 | Slow 速度拾取／丟棄 | T031 | demo 用一般速度 |
| B03 | MAP-F4 map-local effect group fade | T032、S01 | 來源受阻（Themida、缺非零樣本） |
| B04 | 6016 以外的地圖動畫事件 | S02 | 需要個別事件語義來源 |
| B05 | NPC 正向出售、私商正向回購 | R07、R10、C01 | 需要伺服器端有合法報價或買家條件，不屬於產品缺陷 |
| B06 | 終極治癒藥水、自我加速藥水、回家卷軸的地面圖 | #51 | #48 缺 sprite pin，目前停用 |
| B07 | 原生分類全鏈、音效原生選擇語義 | #47、#48 | Themida 執行期初始化，靜態不可得；產品採主控政策 |
| B08 | r34 正式封裝與入口切換 | I01 | 由 M3 的 dev 交付取代 |
| B09 | P27 原始碼審查的最小修正 | I02 | 沒有確認的缺陷；若 T073 重現再升級 |
| B10 | 原 BIN 歷史限制、舊鑰匙刪除時刻 | R15、H01 | 歷史資料不可回補 |
| B11 | 調度器流程改善（runner 權限、收割器、路由） | #21、#25、#62、#74 | 屬於調度器 repo，不屬於本專案進度 |
