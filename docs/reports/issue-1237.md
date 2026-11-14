# Issue #1237：#1211 實機驗收失敗項目分流

結論：#1211（✅ 2／❌ 4／⚠️ 41，共 47 列）的 45 列非 ✅ 項目全部有去處：**4 張程式修正（code）、2 張資源準備（resource）、1 張重測（test）**，另列已知限制／交其他任務的項目。計畫檔 `docs/plans/issue-1237.json`（7 項），調度器合併後自動建成 Issue。

## 做了什麼

1. 讀 #1211 Issue 本文與 RESULT、`origin/task/1211`（`93440384`，PR #1236 未合）的 `docs/reports/issue-1211.md`、`issue-1211-evidence.json`。
2. 唯讀查看證據目錄 `scratch/wt/task-1211/environments/p24/backups/issue-1211-evidence/` 的 `m7-SM07-1.png`、SM20／SM58 的 state JSON，確認缺陷細節（見下表「分流時新增的觀察」）。
3. 靜態讀碼找線索（不改程式）：`SpellWindowPanel.cs:54`、`WorldSession.cs:23–27`、`PlayerProfiles.cs:55–61`、`SelectCharPanel.cs:14–73`、`docs/testing/smoke-m7.md:731,761,767,805`。
4. 查 `origin/task/1209` 的 `work/f1186-smoke/fixtures.json`（PR #1219 **仍未合進 main**，main 上沒有這個檔）。
5. 查本 repo 開放中的 Issue：沒有任何一張 `depends_on` 含 #1211（13 張開放 Issue；內文提到 #1211 的只有 #1231、#1223，它們分別測 SM22／35／51 與 SM27／29，與本次重測不重疊、也不受本次修正影響），因此**沒有改動或關閉其他 Issue**。

## 已通過、不重測

- **SM04**（登入框文字）、**SM55**（classic 選角）：✅。
  - SM55 在重測附帶回歸：`f1211-select-art` 會改原生選角／建角面板，要確認 classic 路徑沒被動到。
  - SM04 只在 `f1211-select-art` 改到共用 surf／UiResources 載入時才回歸（重測任務 D 組寫明條件）。
- 沿用 #1186 ✅：SM02、SM05、SM15、SM56、SM59。

## 分流時新增的觀察（已驗證：讀證據檔）

- SM07：`m7-SM07-1.png` 黑底、**沒有背景圖**，按鈕位置畫成紅／藍職業人物卡片；DPI 96 圖同樣錯置 → 不是 DPI 問題，是 surf 對應問題。
- SM20 妖精列：`m7-SM20-class-2-state.json` 煙妖甲 **gfx 138（基本職業男妖精）** `appearanceProfile=null`、`renderable=false`，與 SM58 的 1062 同一個「沒有 profile 就停擺世界」機制 → 合併成 `f1211-player-appearance`。龍騎 6658 正常載入（`players/6658-0.json`）。
- SM20 龍騎／幻術：state 的 `magic.learned` 有 181／182、201／202，而 `SpellWindowPanel.ClassOf` 只對應 0–4，檢查碼還斷言 `ClassOf(5)==null`。

## 逐項對應

| 編號 | #1211 | 分類 | 去處 |
|---|---|---|---|
| SM07 | ❌ | (a) 程式：選角 surf 錯位 | `f1211-select-art` → 重測 A |
| SM08 | ❌ | (a) 程式：同上（建角按鈕）；IME 屬工具 | `f1211-select-art`、`f1211-smoke-tooling` → 重測 A |
| SM20 | ❌ | (a) 程式：class 5／6 魔法窗空白；妖精 gfx 138 外型停擺；(b) map509 落點 | `f1211-spell-class56`、`f1211-player-appearance`、`f1211-retest-fixture` → 重測 A |
| SM58 | ❌ | (a) 程式：外型無 profile 停擺世界；(b) 1461／1395 變身方法核對 | `f1211-player-appearance`、`f1211-retest-fixture` → 重測 A |
| SM43 | ⚠️ 未執行 | 被妖精外型停擺阻塞 | `f1211-player-appearance` → 重測 A |
| SM48 | ⚠️ 未執行 | 被妖精外型、class 5／6 阻塞 | `f1211-player-appearance`、`f1211-spell-class56` → 重測 A |
| SM31 | ⚠️ | (a) 待定位：C125／S39 都有卻無排名窗，兩輪沒結論 | `f1211-rank-bookmark-window` → 重測 A |
| SM38 | ⚠️ | (a) 待定位：C165 已送，無書籤讀回 | `f1211-rank-bookmark-window` → 重測 A |
| SM09 | ⚠️ | (b) DPI 96 證據方法 | `f1211-smoke-tooling` → 重測 A |
| SM10、SM11、SM18、SM19、SM57 | ⚠️ | (b) DPI 96 方法＋未完成子步驟 | `f1211-smoke-tooling` → 重測 C |
| SM12 | ⚠️ | (b) IME、DPI 96＋各頻道 | `f1211-smoke-tooling` → 重測 C |
| SM13 | ⚠️ | (b) 6400／82／470 圖示條件未建立 | `f1211-retest-fixture` → 重測 C |
| SM21 | ⚠️ | (b) 無可出售物、修理／學法未備 | `f1211-retest-fixture` → 重測 C |
| SM24、SM53 | ⚠️ | (b) 聊天列清空失敗 | `f1211-smoke-tooling` → 重測 C |
| SM40 | ⚠️ 未執行 | (b) 確認第二種箭 | `f1211-retest-fixture` → 重測 B |
| SM03、SM14、SM23、SM25、SM28、SM36、SM37、SM39、SM41、SM42、SM46、SM47、SM49、SM52 | ⚠️ 未執行（時間） | 時間盒不足，無功能判定 | 重測 B（時間盒建議 120 分鐘、A→B→C→D 順序） |
| SM16 | ⚠️ | 未完成子步驟；巨集拖入未綁定再現時判引擎 ❌ | 重測 C |
| SM17 | ⚠️ | 箭頭未核；隊友點受 #1229／#1231 相鄰 PC 條件影響 | 重測 C（隊友點視 #1231） |
| SM26、SM30、SM32、SM33、SM34、SM44、SM45、SM50、SM54 | ⚠️ | 未完成子步驟（功能主體已取得） | 重測 C |

重測任務 `f1211-retest` 共 45 列＋SM55 回歸，`depends_on` 指向上面 6 張。

## 已知限制／交其他任務（不在本次開任務）

- SM22、SM35、SM51：#1225 → #1229／#1230 → #1231 處理。
- SM27、SM29：#1220 → #1222 → #1223 處理。
- SM57 實際聽感：依 #1225 分流（聲音工具），本次重測不做。
- SM01：待主控裁定耗時；SM06：P24Lab 未送 S48 公告（已知限制）。
- DPI 96 有效截圖、真正 IME：若 `f1211-smoke-tooling` 證明不改系統設定做不到，由該張標 `needs:user` 請使用者裁定是否列已知限制（改顯示縮放是系統設定，AI 不可做）。

## 怎麼驗證

- `python` 讀回 `docs/plans/issue-1237.json`：7 項，所有 `depends_on` 都指向本檔內 id；重測列數 45 = #1211 的 47 列 − 2 列 ✅。
- 開放 Issue 的 `depends_on` 以 `gh issue list --json body` 全文比對。

## 未驗證／推測

- 各缺陷的根因（surf 錯位來源、138／1062 為何無 profile、S39 分派）都只是靜態線索，待各 code 任務確認。
- 未執行任何程式、未開遊戲、未碰 DB。

## 產出

- `docs/plans/issue-1237.json`、`docs/reports/issue-1237.md`；分支 `task/1237`。

## 給下一棒的提示

- **PR #1219（fixtures.json）必須先合進 main**，否則重測又要從分支 git show；重測任務已寫成「未合就停下回報」。
- 新的 fixture 寫到 `work/f1211-smoke/fixtures.json`（新檔），避免與 #1219 衝突。
- `f1211-player-appearance` 若需要改「外觀缺 profile 就停擺世界」的既有設計，該張會標 `needs:claude`，請雲端裁定。
- #1211 證據目錄目前在 `scratch/wt/task-1211/` 下，調度器清 worktree 前要先保全（#1211 報告已提醒）；各任務本文已寫「保全後以新位置為準」。
