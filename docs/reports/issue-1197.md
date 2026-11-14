# Issue #1197：M01 地圖 4 重測與 N01-2 回歸

**結論：部分通過（✅ 3／❌ 0／⚠️ 1）。** G01、N01-2、收尾通過；M01 因既有資源法師進入的是地圖 0，無法判定地圖 4 修正。沿用項目不計入本輪統計。未修改產品、helper 或 DB 資料。

實測時間：2026-11-14 14:42–14:44（台北）。來源為最新 main `e783ff08de9c8b5be20da73077dbf5026029636b`，包含 #1195 與 #1196。工作分支 `task/1197`，沒有 checkout、建分支或移除 worktree。

## 已驗證：逐項結果

以下短檔名皆相對本 worktree 的 `scratch/issue-1197/`；主要原始 JSON／SELECT 已封裝到 [Git 證據](issue-1197-evidence.json)。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| G01（前置） | ✅ 通過 | p24svc01 到 CharacterList，3/3 角色且 Gm=0；既有選取為資源法師 | private 帳密透過環境變數啟動 real-interactive → UIA Select 登入／選角 → 讀樹與 GDI | `g01-login-state.json`、`g01-login-tree.json`、`g01-login.png` |
| M01 | ⚠️ 無法判定 | 資源法師 objid=10334 位於 mapId=0、X=32648、Y=32948；原文「地圖 0 已載入原始區塊。」、mapResourceState=ready，主窗有地面；未進 map 4，未覆蓋 4/80048001 | 讀回 ComboBox=資源法師 → UIA Invoke「進入世界」一次 → 待載入完成讀 login-state → PrintWindow；不換角、不移動、不改 DB | `n01-enter.json`、`n01-world-login-state.json`、`m01-main-capture.json`、`m01-main.png` |
| N01-2（回歸） | ✅ 通過 | 同次進世界：InWorld；world.ready/coreComplete/entered 全 true；decodeFailure=null | 沿用同一次 Invoke，讀完成狀態及結束前狀態，沒有重登 | `n01-world-login-state.json`、`n01-final-login-state.json`、`n01-world.png` |
| 本輪收尾 | ✅ 通過 | 自啟 PID 全退出，42061／43324 無監聽；17 角色 AccessLevel=0、OnlineStatus=0；原持久 DB SHA 不變 | CloseMainWindow → 最終 SELECT → 依身分核對停本張 Java → 43324 mysqladmin shutdown → 核 PID、埠及原檔 SHA | `client-final-close.json`、`characters-final.tsv`、`cleanup.json`、`original-db-final.json` |

M01 沒有出現「SEG／壓縮格式」或「資源無法載入」，但這只證明**地圖 0**載入成功。指定的地圖 4 區塊未被本輪觸發，因此不能把 M01 標成通過，也沒有證據判定 #1195 程式修正失敗。例外訊息：無。指定區塊：`4/80048001`（未載入）；實際 loadedRegion 為 X=32576–32703、Y=32896–33023，mode=`original moving 2x2 cache`。

## GDI 逐張目視

本機根目錄：`D:\lineage開發\TW1608262200研究\scratch\wt\task-1197`。三張皆為 PrintWindow、DPI192、Nonblank=true、PrintWindowError=null；未使用 WGC、@oai/sky、座標點擊或鍵盤硬切。PNG 全留 scratch，不提交。

| 路徑 | 尺寸 | SHA-256 | 本輪目視結論 |
|---|---|---|---|
| `scratch/issue-1197/g01-login.png` | 826×711 | `a00052a640f30ed78bb77b6a378b4ef615e3be919884af223110dd8be59febe9` | 登入／選角頁顯示 CharacterList、已解析角色 3/3、資源法師；帳密欄未顯示密碼。 |
| `scratch/issue-1197/m01-main.png` | 1206×871 | `a4a76ebf660acd55dc9fe7d833b7643652e4b607354b69b7a2fc7243f4cf3777` | 主視窗顯示海水、沙石地面、棕櫚樹、石牆、草地與角色，非空白／純色；login-state 對應地圖 0，不能當地圖 4 證據。 |
| `scratch/issue-1197/n01-world.png` | 826×711 | `f4a0f9b1212056f5d60c06b45b47229405ff8f17bc2fbf85696cb808a773b1a4` | 登入／選角頁明確顯示「InWorld · 地圖 0 已載入原始區塊。」。 |

## 建置、來源與隔離環境

- 開始只記一次 `query session`：console／CHAOREN／6／Active。
- 資源鎖查詢與派工不一致：實際 holder=`chaoren0418-arch/tw-bin-engine#1186`、since=1794635425.8574045、exp=1794640825.8574045，不能宣稱 #1197 lease 有效。本張依「程序疑慮只記錄、不停止」繼續；啟動前 42061／43324 均無監聽，沒有關閉其他程序、修改 lease 或調度器。請調度器釐清派工資訊。
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File engine/build-product.ps1 -ConfigFile <worktree>/.github/nuget.ci.config`：product／launcher restore/build 全 0、PASS dev。設定 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`，TEMP/TMP 在本張 scratch。首次直接呼叫 ps1 被現行執行政策拒絕，改用新子程序的 Bypass，未改系統執行政策。
- product DLL SHA-256：`d42e933fe2e762c815c99fe6288d71e8b6fa879b1500463cbf33e36857aa9223`。建置以 temporary lock 處理 stale committed lock，未修改 `engine/_integration/dev/rebuild-lock.json`；receipt 比較被建置器略過，不把略過列為通過。
- `--assets <worktree>/engine/assets` 唯讀 junction 指向既有 r29 assets；manifest SHA=`0fe3a61444180c90abdca0cc94c1cfb9ea19adc67812a43f624d806b51f96ee7`。`TW_CLIENT_ROOT=D:\lineage開發\0-客戶端`。依 #1195，地圖 4 應走 legacy unsupportedBlocks sources SHA → `SegBlocks.ReadLegacy`，但本輪只實際載入地圖 0，沒有宣稱走過該 SEG 分支。
- 原檔 `map/4/80048001.seg` 唯讀 SHA=`e2dd9928bfcdf46874de18e875543c7378e66170594a1828937a12daf360ba12`，90,398 bytes；原始碼及其他來源 SHA 見 `source-hashes.json`。
- 06:41:58Z 完成冷備份：對原 `environments/p24/db-data/ibdata1` 持有拒絕其他寫入的 handle；完整複製到本張 `backup-before/db-data`，再建 `db-data` 隔離副本，三份逐檔 SHA-256 相同。1,722 檔、279,505,799 bytes；備份 manifest SHA=`5fc8f2ad7a3c909a0f97d626c3adc83d659de898076b626b4309c3f78f45f5fe`。未接觸原持久 DB 服務。
- MySQL SELECT 證實 port=43324，datadir=`D:\BinEngine\scratch\wt\task-1197\scratch\issue-1197\db-data\`。D:/BinEngine 是既有專案別名。config／jar／maps 僅以 junction 唯讀引用；Java home/tmp/log 在本 worktree；沒有修改機密設定或複製 pak/map/sprite。
- 客戶端：`--assets <worktree>/engine/assets --login-port 42061 --login-demo real-interactive --classic-ui --evidence <worktree>/scratch/issue-1197/client`。帳密只從既有 private 檔傳入環境，未輸出或提交。
- 自啟 client=5552、Java=8748、MySQL 父=8016／子=19112。收尾等待程序退出後 RemainingOwned=[]、Listeners=[]；mysqladmin shutdown=0。最終 17 角色全 GM0／離線，六欄 SELECT 前後一致。
- 06:44:16Z 原持久 DB 1,722 檔 SHA 無變動、無新增。無手動 DML、建角、刪角、補物品、GM 設定；登入自動寫入只在隔離副本。

還原 SQL：本張未做手動資料修改，無 DML 需反向還原。若要還原隔離副本的自動寫入，應在程序全停後從本張完整冷備份建立新副本並核 SHA，不覆寫原持久 DB。唯讀核對 SQL：

```sql
SELECT @@port, @@datadir;
SELECT account_name,char_name,objid,Type,AccessLevel,OnlineStatus
FROM p24_accounts.characters ORDER BY objid;
```

## 沿用，不重測

#1195 只改地圖載入／準備器、#1196 只改證據位置，沒有改 UIA 工具、服務巢狀 Tab、panel.js 或 CharacterList；下列依 #1188 原結果沿用，不算本輪重新驗證。#1188 的報告目前不在 main 檔案樹，本張從 `origin/task/1188` 的 `d6221f720be613bf8c777320cd98b9184cdfd09a` 唯讀取得。

| 編號 | 結果 | 實際觀察 | 重現步驟／證據 |
|---|---|---|---|
| #1188 N01-1 | ✅ 沿用 | 不重測；沿用原結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/d6221f720be613bf8c777320cd98b9184cdfd09a/docs/reports/issue-1188.md) |
| #1188 N01-3 | ✅ 沿用 | 不重測；沿用原結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/d6221f720be613bf8c777320cd98b9184cdfd09a/docs/reports/issue-1188.md) |
| #1188 N01-4a | ✅ 沿用 | 不重測；沿用原結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/d6221f720be613bf8c777320cd98b9184cdfd09a/docs/reports/issue-1188.md) |
| #1188 N01-4b | ✅ 沿用 | 不重測；沿用原結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/d6221f720be613bf8c777320cd98b9184cdfd09a/docs/reports/issue-1188.md) |
| #1188 收尾 | ✅ 沿用 | 不重測；沿用原結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/d6221f720be613bf8c777320cd98b9184cdfd09a/docs/reports/issue-1188.md) |
| #1179 G05 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/9a905ce3/docs/reports/issue-1179.md) |
| #1179 G06 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/9a905ce3/docs/reports/issue-1179.md) |
| #1179 G07 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/9a905ce3/docs/reports/issue-1179.md) |
| #1173 P01 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/cf5470ff/docs/reports/issue-1173.md) |
| #1165 G04 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1165 F01 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1108 R07 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1150 G02 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1150 G03 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1149 S02 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1149 S02-GDI | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1149 S02-CLOSE | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1118 S01 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1126 D01 | ✅ 沿用 | WGC 不可用的既有診斷；不重試。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1126 C01 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1108 B01 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1108 R05 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1108 R08 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1108 R09 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1108 R10 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1108 R11 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1108 R12 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1108 R13 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1108 R14 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1108 R15 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1108 C01 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1108 C02 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |
| #1108 V01 | ✅ 沿用 | 不重測；沿用指定結果。 | [原觀察、步驟與證據](https://github.com/chaoren0418-arch/tw-bin-engine/blob/main/docs/reports/issue-1165.md) |

## 推測、待驗證與給下一棒的提示

- 已驗證：既有資源法師本次位置是地圖 0；N01-2 與收尾通過。只有一次進世界，沒有修程式或補測其他項。
- 推測：原持久測試資料自 #1188 後的角色位置已不同；本輪沒有追查是哪張任務改變，不能歸因於 #1195。備份總位元組數與 #1188 不同只證明來源快照不同，不能單憑此確定位置變動原因。
- 待驗證：M01 地圖 4 的 detail／mapResourceState／地面圖塊，尤其 `4/80048001`，仍需重測。不得用本張地圖 0 成功作替代。
- 下一棒：調度器／Claude 先安排明確的**地圖 4 測試前置**（例如核實 #1188 冷備份內既有資源法師位置，指定該備份的隔離副本供重測），再只派 M01；本輪 G01、N01-2、收尾可沿用。若需要 DB 寫入或角色移動，應另立前置任務，不擴大本張限制。
- 未執行：其他 SEG 區塊、SM08／SM09／SM48、服務子頁按鈕、移動、戰鬥、交易、對話、N01-1／N01-3／N01-4；均按任務排除。
- 產出：`docs/reports/issue-1197.md`、`docs/reports/issue-1197-evidence.json`；完整證據／腳本／備份在 `scratch/issue-1197/`。未改 `docs/REPORT.md`，未遞迴刪除任何目錄。
- CI：PR 最新 commit 的結果以 GitHub checks 為準；本機遊戲素材閘門已通過（errors=0）；結果記於 scratch/issue-1197/asset-gate.txt。
