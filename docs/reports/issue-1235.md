# Issue #1235：持久 map4 fixture 的 F07／M01 重測

**結論：通過（本輪 ✅ 4／❌ 0／⚠️ 0；含 G01 前置與本輪收尾，沿用列不計數）。** 資源法師在地圖 4（33071,32800）進世界，快取明確載入 `80048001`，地圖資源 ready，PrintWindow 可見地面。沒有修改產品程式或 helper，沒有手動寫 DB 或改角色位置；登入自動寫入只在隔離副本。

2026-11-14 18:32–18:38（台北）執行；分支 `task/1235`，建置來源 main `8514a9f1`。已閱讀 AGENTS.md、docs/REPORT.md、UIA README 與指定來源；main 沒有 #1197／#1215 報告及 fixture.json，從 `origin/task/1197`／`origin/task/1215` 唯讀取得，實際 fixture 只用 main 的 `fixture-persist.json` 指定持久路徑。開始記一次 `query session`：console／CHAOREN／6／Active。

## 已驗證：逐項結果

以下短檔名相對本 worktree 的 `scratch/issue-1235/`；關鍵原文、SQL、快取、雜湊與完整 main commit 差異已收入 [Git 證據](issue-1235-evidence.json)。PNG 只在 scratch，沒有提交。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| F07 | ✅ 通過 | 持久 fixture 存在；manifest SHA 正確，DB 1,722 檔／279,503,832 bytes 全相符；隔離副本 SELECT 10334=資源法師／p24svc01／map4(33071,32800)／GM0／離線 | 依 fixture-persist.json 讀來源，逐檔核對 SHA／大小／完整集合，以 exclusive-create 複製至本張 scratch，再逐檔核對；只開副本 43324，唯讀 SELECT | `preparation.json`、`copy-manifest.json`、`identity-before.tsv`、`select-before.json`、`characters-before.tsv` |
| G01（前置） | ✅ 通過 | p24svc01 到 CharacterList，角色 3/3 均 Gm=0；既有選取資源法師 | real-interactive 登入；UIA Select「登入／選角」，讀樹、login-state、GDI | `g01-login-state.json`、`g01-login-tree.json`、`g01-login.png` |
| M01 | ✅ 通過 | mapId=4、actor=(33071,32800)；cache slot 2=`80048001` 且 SHA 與原 SEG 相同；detail「地圖 4 已載入原始區塊。」、mapResourceState=`ready`；圖有土石路及草地 | UIA 讀回資源法師，Invoke「進入世界」一次；讀穩定 login-state，PrintWindow 主窗；不換角、不移動 | `n01-enter.json`、`m01-login-state.json`、`m01-final-login-state.json`、`m01-main-capture.json`、`m01-main.png`、`n01-world.png` |
| 本輪收尾 | ✅ 通過 | 自啟 PID 全退出，42061／43324 無監聽；17 角色全 GM0／離線且位置不變；fixture 全目錄與持久 DB SHA 均不變 | CloseMainWindow；最終 SELECT；核對 PID／啟動時間／exe 停本張 Java；43324 mysqladmin shutdown；核埠與來源全檔 SHA | `client-final-close.json`、`characters-final.tsv`、`select-final.json`、`cleanup.json`、`final-verification.json` |
| #1215 F01 | ✅ 沿用 | 資源鎖／起始埠原結果沿用 | 不重測；本輪鎖另記下節 | #1215 報告／#1228 分流 |
| #1215 F02 | ✅ 沿用 | fixture 來源與複製原結果沿用 | 不重測原來源選擇 | #1215 報告／#1228 分流 |
| #1215 F03 | ✅ 沿用 | 角色與帳號原結果沿用；本輪 SELECT 是 F07 前置 | 不另計重測項 | #1215 fixture.json／本輪 `select-before.json` |
| #1215 F04 | ✅ 沿用 | SEG 原 SHA 核驗沿用 | 不另計重測項 | #1215 報告／本輪 `preparation.json` |
| #1215 F05 | ✅ 沿用 | fixture 未啟動且收尾相符原結果沿用 | 不重測上一輪收尾 | #1215 報告／#1228 分流 |
| #1215 F06 | ✅ 沿用 | 持久 DB 不變原結果沿用 | 不重測上一輪收尾 | #1215 報告／#1228 分流 |
| #1197 G01 | ✅ 沿用 | 上輪結果沿用，本輪前置另列 | 不另計回歸項 | #1197 報告 |
| #1197 N01-2 | ✅ 沿用 | 本輪同次進世界順手記錄 InWorld；world.ready/coreComplete/entered=true；decodeFailure=null，未見異常 | 不另跑、不另計重測項 | #1197 報告；本輪 `m01-login-state.json` |
| #1197 收尾 | ✅ 沿用 | 上輪收尾原結果沿用 | 不重測上一輪 | #1197 報告 |
| #1188 N01-1 | ✅ 沿用 | 原結果沿用 | 不重測 | #1188／#1197 報告 |
| #1188 N01-3 | ✅ 沿用 | 原結果沿用 | 不重測 | #1188／#1197 報告 |
| #1188 N01-4a | ✅ 沿用 | 原結果沿用 | 不重測 | #1188／#1197 報告 |
| #1188 N01-4b | ✅ 沿用 | 原結果沿用 | 不重測 | #1188／#1197 報告 |
| #1188 收尾 | ✅ 沿用 | 原結果沿用 | 不重測 | #1188／#1197 報告 |
| #1179 G05 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1179 G06 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1179 G07 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1173 P01 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1165 G04 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1165 F01 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1108 R07 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1150 G02 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1150 G03 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1149 S02 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1149 S02-GDI | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1149 S02-CLOSE | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1118 S01 | ✅ 沿用 | 開始只記一行 session，未重跑 | 不重測 | 本報告前言／#1197 沿用表 |
| #1126 D01 | ✅ 沿用 | 沿用「WGC 不可用」診斷，不宣稱 WGC 可用 | 不再試 | #1197 沿用表 |
| #1126 C01 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1108 B01 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1108 R05 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1108 R08 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1108 R09 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1108 R10 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1108 R11 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1108 R12 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1108 R13 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1108 R14 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1108 R15 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1108 C01 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1108 C02 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |
| #1108 V01 | ✅ 沿用 | 原結果沿用 | 不重測 | #1197 沿用表 |

沿用來源：[#1197 報告](https://github.com/chaoren0418-arch/tw-bin-engine/blob/b4538e05/docs/reports/issue-1197.md)、[#1215 報告](https://github.com/chaoren0418-arch/tw-bin-engine/blob/49fa09e2/docs/reports/issue-1215.md)、[#1204](issue-1204.md)、[#1228](issue-1228.md)。沿用不表示本張重新驗證這些功能。

## M01 原文與 GDI 目視

- `state=InWorld`；`detail=地圖 4 已載入原始區塊。`；`mapResourceState=ready`；`mapId=4`；`actor.ObjectId=10334`，`actor.X=33071`、`actor.Y=32800`。
- `loadedRegion={minX:33024,minY:32768,maxX:33151,maxY:32895,mode:"original moving 2x2 cache"}`。`world.cache.slots[2].block=80048001`、SHA=`e2dd9928bfcdf46874de18e875543c7378e66170594a1828937a12daf360ba12`，另有相同區塊的 `load` 事件。這是指定 SEG 的直接載入證據，不以別張地圖替代。區塊座標範圍 X=33024–33087、Y=32832–32895，包含在 loadedRegion 內。
- `world.ready/coreComplete/entered` 均 true；`decodeFailure=null`、`entryFailure=null`；detail 不含「SEG／壓縮格式」或「資源無法載入」。結束前仍在同一座標，沒有第二次進入世界。
- `TW_CLIENT_ROOT=D:\lineage開發\0-客戶端`；`map/4/80048001.seg` 為 90,398 bytes，SHA 同上。未出現 `Original cache source hash` 或 `InvalidDataException`。

三張皆 PrintWindow、DPI192、PrintWindowError=null；已逐張目視，沒有使用 WGC、@oai/sky、computer-use、座標點擊或鍵盤硬切。

| 圖檔（本 worktree 相對路徑） | 尺寸 | SHA-256 | 目視結論 |
|---|---|---|---|
| `scratch/issue-1235/g01-login.png` | 826×711 | `a00052a640f30ed78bb77b6a378b4ef615e3be919884af223110dd8be59febe9` | 登入／選角頁顯示 CharacterList、已解析角色 3/3、資源法師，密碼未顯示。 |
| `scratch/issue-1235/m01-main.png` | 1206×871 | `39671eb31c59d49a2320722de348e1450814c1e3f36b8007faaba62dfba8b217` | 土石道路、草地、木屋、樹木、木材、推車、牛及角色清楚可見，地面有紋理，非空白／純色。 |
| `scratch/issue-1235/n01-world.png` | 826×711 | `b6eefd9902e0913a954954b9deacde1988a65fb9a4e06716233d42e76d6c96fd` | 登入／選角頁明確顯示「InWorld · 地圖 4 已載入原始區塊。」。 |

## 建置與 main 差異

執行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File engine/build-product.ps1 -ConfigFile <worktree>/.github/nuget.ci.config` 一次；設定 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`，TEMP/TMP 指本張 scratch。product／launcher restore、build 全為 0，最後 PASS dev；stale lock 使用建置器 temporary lock，未改 committed lock；receipt comparison 因輸入不同被略過，不宣稱核過舊 receipt。

product DLL：`engine/build/client-product-dev/P11.ClientEngine.dll`，SHA-256 **`3c95a5c708f72bbbae4c600e8f43bbcd5cfe5ed3b64a166c90afd0321c586f4c`**。與 #1197 `d42e933fe2e762c815c99fe6288d71e8b6fa879b1500463cbf33e36857aa9223` 不同；#1197 main=`e783ff08`，本輪 main=`8514a9f1`。期間產品來源新增六個非 merge commit：

| Commit | 變更 |
|---|---|
| `1138840d` | #1205 原生 UI 文字改像素字高，修 DPI192 |
| `f0601dab` | #1207 頭上 HP／MP 條對齊角色與 viewport |
| `68cfdccf` | #1208 選項名稱由第二客戶端 desc-c.tbl 補入 |
| `6d234ace` | #1206 原生選角／訊息框範圍與等待字樣 |
| `acf93457` | #1244 龍騎士／幻術士魔法窗一般等級組 |
| `086a33e3` | #1242 選角／建角美術資源來源 |

全部 37 個非 merge commit（含文件、fixture、helper 與索引）列於 Git 證據的 `build.nonMergeCommitsSincePreviousMain`。#1204／#1228 本身只有分流，沒有產品修正；本張不擴大重測其他功能。

`engine/assets` 新建唯讀用途 junction 指向原專案 `releases/r29/assets`；`maps/manifest.json` SHA=`0fe3a61444180c90abdca0cc94c1cfb9ea19adc67812a43f624d806b51f96ee7`。依 #1195 §3，r29 Packaged manifest 的 legacy SEG sources SHA 路徑讀 `TW_CLIENT_ROOT`；本次 cache 記錄的原 SEG SHA 與它相符。未複製 pak／map／sprite，也未修改 assets 目標。

## fixture、隔離與收尾

- 權威：`work/f1197-map4/fixture-persist.json`。來源固定為 `D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-1215-map4-fixture\db-data`，未使用 task-1215 內的 DB 或持久 `environments/p24/db-data` 開服。
- manifest SHA：`996a39971570b7a2d8ab2547fea02e737a24c898f02b5f7f740135e6b3caf06b`。fixture 全目錄包含 DB、manifest、原證據共 1,736 檔；本輪前後清單 SHA 均 `5081244dceccdb5cbc1c68ccf5f36d2303a05494a9d9e885c06c10f86cec3293`。每個 DB 檔案、大小、SHA 與 manifest 一致；隔離副本開服前亦完全一致。
- 原持久 DB 1,722 檔／279,511,437 bytes；前後清單 SHA 均 `f94bf98347b9789f368dc9863a28ab09132ad613cb86373f87bd7283d0f19668`。清單是依 POSIX 相對路徑排序的 path／bytes／sha256，UTF-8、indent=2、CRLF、結尾換行。
- 18:34:50 的 API 證據確認 holder=`chaoren0418-arch/tw-bin-engine#1235`、exp=`1794663171.302706`（2026-11-14 13:32:51.302706Z），與派工一致且有效。第一次 PowerShell 查詢未顯示 leases；後續 Python 取得完整有效記錄，未修改調度器。
- 只開本張 `scratch/issue-1235/db-data`；SQL 證實 `@@port=43324`，datadir=`D:\BinEngine\scratch\wt\task-1235\scratch\issue-1235\db-data\`。D:\BinEngine 是既有專案別名。config／jar／maps 以 junction 引用；Java home／tmp／log 與 WebView2 user data 都指定本 worktree。沒有更改機密設定。
- 客戶端參數：`--assets <worktree>/engine/assets --login-port 42061 --login-demo real-interactive --classic-ui --evidence <worktree>/scratch/issue-1235/client`。帳密從既有 private 檔讀到程序環境，不輸出或提交。
- client PID=8876；Java=19076；MySQL 父=4172／子=3436。18:38:31 核對全部退出、Listeners=[]、mysqladmin shutdown=0。最終 17 角色 SELECT 的九欄與開服前相同。
- 一次性 SELECT 腳本初名 `select.py` 遮蔽 Python 標準庫；改名 `check-db.py` 後正常，沒有重開 MySQL。Java 收尾的初次時間比較受 PowerShell 時區轉換影響而拒絕停止；核對 exe／PID／UTC 後改用 DateTimeOffset 比較並收尾。這兩項是本張暫存腳本問題，沒有修產品或重新跑遊戲項目。

唯讀 SQL：

```sql
SELECT @@port,@@datadir;
SELECT account_name,char_name,objid,Type,AccessLevel,OnlineStatus,MapID,LocX,LocY
FROM p24_accounts.characters ORDER BY objid;
```

還原 SQL：不適用；沒有手動 DML、設 GM、建角、刪角、補物品或位置修改。登入／伺服器的自動寫入只發生在隔離副本；需要重置時應由持久 fixture 建立新副本並核 SHA，不覆寫原持久 DB。未操作正式 2000／3306、未改原 BIN／客戶端／封存、未遞迴刪除或移除 worktree。

## 推測、待驗證與給下一棒的提示

- 已驗證：F07、G01 前置、M01 與本輪收尾均通過；`80048001` 有 cache slot／load event／原檔 SHA 的直接證據。
- 推測：沒有需要用推測支撐的通過項。其他 SEG 區塊與完整地圖巡航不由本次結果推廣。
- 待驗證：本張範圍內無待補項；SM 系列、服務子頁功能、移動、戰鬥、交易、對話及其他已通過列均未重測。N01-2 只記同次佐證。
- 交付檢查：證據 JSON 解析與條件斷言、圖片 SHA 核對、`git diff --check`、遊戲素材閘門；PR CI 由最新提交的 GitHub checks 驗證，與原生結果分開。
- 產出：本報告、`docs/reports/issue-1235-evidence.json`；完整 login-state、GDI、log、逐檔清單、隔離副本與一次性腳本在本 worktree `scratch/issue-1235/`。未改 docs/REPORT.md 或 rebuild-lock.json。
- 下一棒：Claude／調度器審查 PR 與 CI 後可結束 F07／M01 重測鏈；本張沒有失敗項需再派修正或重測。截圖仍在 scratch，調度器清理 worktree 前若要長期保留，需自行納入其證據保存流程。
