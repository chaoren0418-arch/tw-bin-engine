# Issue #1119：R07 七職業 GUI 建角重測（第 2 次）

**結論：通過。R07 本輪完成；合計 ✅ 14／❌ 0／⚠️ 0，其中其餘 13 項僅沿用 #1108。** 七職業各有一張可建立截圖，GUI 實際送出 1 次、原服接受後讀回，再由 GUI 立即刪除本輪角色；p24svc01 回復 3/4 槽。

2026-11-14 04:21–04:44（台北）執行。既有 `task/1119` 合併 main `bdefe653`，測試 HEAD `4193c9e4`；程式來源與 main 相同。未修程式、未改 dev lock／`docs/REPORT.md`；未測 SM08／SM09／SM48。

## 逐項結果

沿用列維持上輪觀察與步驟，**沒有重測或重放準備資料**；其路徑屬於 #1108 的 `scratch/issue-1108/`。來源：[原報告](https://github.com/chaoren0418-arch/tw-bin-engine/blob/f416df309bdcc8b2e44a4462ae14a847fb355ef5/docs/reports/issue-1108.md)。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| B01 備份 | ✅ 沿用 | #1107 備份完整；目前 DB 有 19 檔與其不同，另做 1,722 檔完整冷備份並逐檔比對 SHA-256 | 兩埠空閒，禁止寫入的 ibdata1 handle 保護下複製及比對 | #1108：`snapshot.json`、`backup-before/manifest.json`；Git 證據 `snapshot` |
| R05 雙角色 | ✅ 沿用 | 兩個不同帳號同時在 map 4、距離 1 格；雙方收到對方 S87，SQL 同時 OnlineStatus=1 | 離線準備相鄰座標；各自登入、C137 進世界、C95 時間探測，保持兩連線讀回 | #1108：`dual-clan.json`、`dual-online.tsv`、`dual-*-packets.jsonl`；Git 證據 `dual` |
| R07 七職業 | ✅ 本輪 | 7/7 GUI 配點可建立；晨風騎士經 GUI 建立及立即刪除，3→4→3 槽；解析後狀態紀錄對應 S98=2、S127 | 七職業逐一開框、配點、截圖、取消；再以騎士送出一次，SELECT 核對後僅刪該角色 | [證據 JSON](issue-1119-evidence.json)、[狀態紀錄](issue-1119/decoded-transitions.json)、下表截圖 |
| R08 弓箭 | ✅ 沿用 | 服務驗證持有 172×1、40743×200，原本即成立 | 主 DB 初次與最終 SELECT | #1108：`items-before.tsv`、`final-readback.tsv` |
| R09 肉 | ✅ 沿用 | 40056×19，未補發或消耗 | 同上 | #1108：同上 |
| R10 戒指／卷軸 | ✅ 沿用 | 新增 20288×1、40100×20，持久 DB 最終仍成立 | 跨四個實驗 schema 查 ID 不衝突後重放 INSERT，再讀回 | #1108：`item-id-audit.json`、`applied.sql`、`items-after.tsv` |
| R11 料理書 | ✅ 沿用 | 新增 41255×1，持久 DB 最終仍成立 | 同 R10 | #1108：同上 |
| R12 寵物／召喚 | ✅ 沿用 | 資源法師 LV52、技能 51；項圈叫出資源牧羊，召喚術另生成 gfx 3357；登出後笛 9、寶石 57 | 離線準備測試資料，重開原服載入；GM0 登入後 C164 使用項圈、C6 施放一次召喚 | #1108：`roles-preparation-*.tsv`、`pet-summon.json`、`pet-summon-packets.jsonl`；Git 證據 `pets` |
| R13 血盟／盟倉 | ✅ 沿用 | 資源血盟 10171；服務驗證盟主 rank 4，透明王子 rank 8；盟倉治癒藥水×1 | GM0 C222 建盟；面對面 C194 申請、收到 S219/97 後 C121 接受；向索連正常送存倉請求 | #1108：`clan-warehouse.json`、`clan-members-after.tsv`、`warehouse-*.tsv`；Git 證據 `clan` |
| R14 金幣 | ✅ 沿用 | 建盟扣 30000：100000→70000；離線補回 30000，最終 100000 | 確認指定堆疊／持有人及數量後條件 UPDATE，讀回 | #1108：`gold-before-replenish.tsv`、`gold-after-replenish.tsv`、`final-readback.tsv` |
| R15 GM | ✅ 沿用 | 全部 17 角色 AccessLevel=0、OnlineStatus=0；全程未設 GM | 最終全角色讀回，與冷備份資料對照 | #1108：`final-readback.tsv`；Git 證據 `characters` |
| C01 變更邊界 | ✅ 沿用 | 70 張帳號庫表前後比較：76 新增、8 更新、0 刪除；只有測試角色／帳號及其關聯資料改變；14 個保護檔 hash 相同 | 冷備份另複製到隔離目錄，以同一 43324 順序啟動讀回；與主 DB 最終快照比較 | #1108：`row-diff-summary.json`、`protected-final.json`；Git 資料差異與證據 |
| C02 程序收尾 | ✅ 沿用 | 所有本次 Java、MySQL、客戶端及 WebView 程序均結束；42061／43324 無監聽 | 按自啟 PID 核對後結束；MySQL 用 mysqladmin 正常 shutdown；最後列程序／監聽 | #1108：`shutdown.json`；Git 證據 `shutdown` |
| V01 建置／還原核驗 | ✅ 沿用 | 首次建置 0 錯誤／4 既有警告；同步後增量 build 0 錯誤／0 警告；84 條還原 SQL 的 EXPLAIN 通過，預檢=1 | build、逐條 EXPLAIN、反向資料模型與備份全列比對、git diff --check | #1108：`build.log`、`build-after-sync.log`、`restore-preflight.tsv`；Git 證據 `restoreVerification` |

## R07 本輪紀錄

能力順序為力量／敏捷／體質／精神／魅力／智力；每組總和 75。七張草稿均使用尚未存在的「晨風騎士」，截圖後按 Esc 取消，未送出。GUI 名稱「精靈／黑暗精靈」對應任務所稱妖精／黑暗妖精。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| R07-1 建置／登入 | ✅ | SDK 10.0.300；product 0 錯誤／4 既有警告、launcher 0／0；進 CharacterList，3/4 槽 | build-product.ps1；沿用 #1108 參數 `--login-port 42061 --login-demo real-interactive --classic-ui --evidence`，帳密由本機私有檔提供 | [建置](issue-1119/product-build.log)、[選角](issue-1119/selection.png)、[初始 SELECT](issue-1119/before-create-characters.tsv) |
| R07-2.0 王族 | ✅ | 18／10／13／11／13／10；剩餘 0、顯示「資料符合本機檢查，可以建立」、建立鈕啟用 | 開新框，鍵盤選職業；UIA 配點，切離欄位驗證；截圖後 Esc 取消 | [截圖](issue-1119/royal.png)、[UIA](issue-1119/royal-uia.txt) |
| R07-2.1 騎士 | ✅ | 18／12／16／9／12／8；剩餘 0、顯示「資料符合本機檢查，可以建立」、建立鈕啟用 | 開新框，鍵盤選職業；UIA 配點，切離欄位驗證；截圖後 Esc 取消 | [截圖](issue-1119/knight.png)、[UIA](issue-1119/knight-uia.txt) |
| R07-2.2 精靈（妖精） | ✅ | 18／12／12／12／9／12；剩餘 0、顯示「資料符合本機檢查，可以建立」、建立鈕啟用 | 開新框，鍵盤選職業；UIA 配點，切離欄位驗證；截圖後 Esc 取消 | [截圖](issue-1119/elf.png)、[UIA](issue-1119/elf-uia.txt) |
| R07-2.3 法師 | ✅ | 18／7／18／12／8／12；剩餘 0、顯示「資料符合本機檢查，可以建立」、建立鈕啟用 | 開新框，鍵盤選職業；UIA 配點，切離欄位驗證；截圖後 Esc 取消 | [截圖](issue-1119/mage.png)、[UIA](issue-1119/mage-uia.txt) |
| R07-2.4 黑暗精靈（黑暗妖精） | ✅ | 18／15／12／10／9／11；剩餘 0、顯示「資料符合本機檢查，可以建立」、建立鈕啟用 | 開新框，鍵盤選職業；UIA 配點，切離欄位驗證；截圖後 Esc 取消 | [截圖](issue-1119/darkelf.png)、[UIA](issue-1119/darkelf-uia.txt) |
| R07-2.5 龍騎士 | ✅ | 18／11／15／12／8／11；剩餘 0、顯示「資料符合本機檢查，可以建立」、建立鈕啟用 | 開新框，鍵盤選職業；UIA 配點，切離欄位驗證；截圖後 Esc 取消 | [截圖](issue-1119/dragon.png)、[UIA](issue-1119/dragon-uia.txt) |
| R07-2.6 幻術師 | ✅ | 18／10／15／12／8／12；剩餘 0、顯示「資料符合本機檢查，可以建立」、建立鈕啟用 | 開新框，鍵盤選職業；UIA 配點，切離欄位驗證；截圖後 Esc 取消 | [截圖](issue-1119/illusion.png)、[UIA](issue-1119/illusion-uia.txt) |
| R07-3 真正送出 | ✅ | 騎士晨風騎士，objid=10387、Type=1、Class=61、AccessLevel=0；清單 4/4，取得接受建角及新角色解碼狀態 | 重開騎士框、同合法配點，Return 觸發建立鈕一次；讀回清單與 DB | [清單截圖](issue-1119/created-selection.png)、[SELECT](issue-1119/after-create-characters.tsv)、[解碼狀態](issue-1119/decoded-transitions.json) |
| R07-4 GUI 刪角 | ✅ | 確認框只指向晨風騎士；原服立即刪除，無七日排程，回復 3/4 槽 | 選取新角色→刪除角色→核對名稱→Alt+Y；不以 SQL 清除 | [確認框 UIA](issue-1119/delete-confirm-uia.txt)、[刪後截圖](issue-1119/deleted-selection.png)、[SELECT](issue-1119/after-delete-characters.tsv) |
| R07-5 收尾 | ✅ | 最終 17 角色均 GM0／OnlineStatus0；本輪 14 個已記錄程序均結束，42061／43324 無監聽 | 主視窗 Alt+F4；按自啟 PID 核對關閉 Java、記事本與小畫家；mysqladmin 正常 shutdown | [最終 SELECT](issue-1119/final-characters.tsv)、證據 JSON `cleanup` |

## 已驗證、推導與待驗證界線

- **已驗證**：七張實際 GUI 截圖、一次 GUI 建立、一次 GUI 刪除、清單／DB 前後讀回及程序收尾。沒有以協定腳本代替 GUI、沒有自行呼叫引擎方法或修改程式；原專案的寫入僅限任務指定持久 DB 的正常登入／建角／刪角，執行目錄與所有其他輸出在本 worktree。沒有補物品、改血盟／寵物或設 GM。
- **回包的證據層級**：客戶端 `web-events.json` 實際保留 event 38「伺服器已接受建立，等待新角色資料」、event 39「角色已建立」，以及 event 52「角色已刪除」。[擷取紀錄](issue-1119/decoded-transitions.json)保留來源索引、狀態版本、角色資料；原 log SHA-256 與程式 SHA-256 見證據 JSON。對照本次未修改的 `CharacterManagement.cs:115`、`:117–120`、`:124`，分別是 **S98 reason=2、S127 身分驗證成功、S6 result=5** 分支。這是執行期的解碼後狀態證據，**沒有保存原始封包 bytes，也沒有產生假造的 hex dump**；opcode／reason 的對應由同版來源分支推導。R07 的原服接受判定同時由清單及真 DB 讀回支持。
- **待驗證／未做**：原始回包 bytes 的獨立封包檔、還原 SQL 的實際執行；WGC 逾時根因。R07 未要求七次真正送出，故其餘六職業原服協定接受仍沿用 #1108。

## 可重用 GUI 方法與工具限制

1. 本輪 WGC 仍兩次逾時（`FrameArrived timed out`、`window capture timed out`）；記事本 `set_value` 可成功。改用標準 **Alt+Print → 小畫家 Ctrl+N／Ctrl+V／Ctrl+S → Alt+S** 存 PNG，先以[記事本](issue-1119/notepad-probe.png)確認，再用同法擷取每個對話框；不是合成畫面，也未使用自製 Windows 操作 helper。
2. 元素 `click` 仍可能報 geometry unavailable，且 UIA 焦點資訊有延遲，不能據此盲目重送。改走鍵盤頁籤／Tab／Space，畫面確認在建角按鈕後開框；每個新框重新列窗取得 HWND。
3. 建角框初始焦點是角色名稱；Tab 到職業後用 Down／End 選職業，等下一次 UIA 讀回基礎值再配點。**不要只對職業 ComboBox set_value 文字來當作切換成功。**
4. 能力欄位用 UIA `set_value`；再移到角色名稱欄位（`set_value` 名稱）使 NumericUpDown 提交值，確認剩餘 0 與建立鈕啟用。草稿用 Esc 取消；唯一真正送出用 Return，刪除確認先核對名稱再 Alt+Y。
5. 小畫家存檔欄索引可能失效；本輪在已確認的 `scratch/issue-1119` 資料夾用預設檔名存檔，透過一般檔案移動改成證據檔名。每張都重新擷取並視覺核對，沒有重用舊圖。

## 備份、差異與給下一棒的提示

- 新操作前完整冷備份：`scratch/issue-1119/backup-before/db-data`，1,722 檔、279,501,517 bytes，逐檔 SHA-256 相等；manifest SHA-256 `012b0303b681bf7a559e443a6e8460ccb8360f09ffafa06d4dd07272dc8a4c4e`。備份與原始紀錄須在調度器清理前保存，**不可遞迴刪除 worktree**（server/config、jar、maps 有 junction）。
- 全部 70 張 p24_accounts 表前後比較，500→500 列；角色及其關聯資料已由原服 GUI 刪角清除，唯一剩餘差異為 p24svc01.lastactive：`2026-11-14 02:43:18`→`2026-11-14 04:27:30`。[差異](issue-1119/data-diff.json)與[還原 SQL](issue-1119-restore.sql)只涉及此欄；Python 反向資料模型等於任務前全表快照，沒有執行還原 SQL。物理檔／自增計數不宣稱已逐 byte 還原。
- lease 檢查時 holder 與到期秒值皆符合派工，當時有效；沒有修改鎖或調度器。
- 本輪完成 R07，交 Claude／調度器審查本報告的解碼後回包證據，不要重放 #1108 準備資料或再建晨風騎士。若後續驗收另要求原始封包 bytes，需在新的授權測試前配置封包記錄；本輪不為補 log 多送一次建角。#1121 替代路徑任務可參考本輪鍵盤＋系統截圖方法。
- 產出：本報告、`issue-1119-evidence.json`、`issue-1119-restore.sql`、`issue-1119/` 證據。驗證：截圖逐張檢視、UIA 7/7、狀態／DB 3→4→3、資料差異及還原模型、收尾程序／埠、JSON 解析與 git diff --check。
