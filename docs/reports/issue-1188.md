# Issue #1188：只重測 N01 進世界後切服務巢狀分頁

**結論：通過。** N01 四個子步驟全部 ✅；G01 前置 ✅。沿用項目不計入本輪結果。沒有修改產品或 helper。

2026-11-14 13:20–13:25（台北）完成建置、隔離環境、實機與收尾。實測來源 main `1dbb1c7b0549a228efc79d158153938c6e7274fa`，包含 #1187 修正；提交前同步 main `9b7f6f92dbaed0035fdf668dfea5ea2505ca30a7`，差異僅 docs/REPORT.md 的自動索引，產品來源未變。同步後重建通過、product DLL SHA 相同；已通過實機項目不重跑。

## 已驗證：逐項結果

下列 JSON／log 路徑相對本 worktree 的 `scratch/issue-1188/`，原始內容已封裝在 [Git 證據](issue-1188-evidence.json)；截圖保留於本機 scratch，路徑、尺寸、SHA-256 與沿用的目視結論見下表。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| G01（前置） | ✅ 通過 | p24svc01 到 CharacterList，已解析 3/3 角色，三角色 Gm=0 | 以既有 private 帳密環境變數啟動 real-interactive；讀狀態、登入頁 UIA／GDI | `g01-login-state.json`、`g01-login-tree.json`、`scratch/issue-1188/g01-login.png` |
| N01-1 | ✅ 通過 | CharacterList 的「開發工具服務分頁」Enabled=false，符合對照預期 | 外層服務頁 → Find-UiaControl 精確名稱／Type Tab → 讀 Enabled 一次；未選停用子頁 | `n01-disabled.json`、`n01-disabled-tree.json`、`scratch/issue-1188/n01-disabled.png` |
| N01-2 | ✅ 通過 | 既有資源法師，InWorld、world.ready/coreComplete/entered=true、decodeFailure=null | 登入頁讀回 ComboBox=資源法師 → UIA Invoke「進入世界」一次 → 讀狀態 | `n01-enter.json`、`n01-world-login-state.json`、`n01-world-tree.json`、`scratch/issue-1188/n01-world.png` |
| N01-3 | ✅ 通過 | 巢狀 Tab 與 16 個 TabItem 全為 Enabled=true；名稱、矩形對上標籤，含血盟 | InWorld → 外層服務 → Find-UiaControl $dev -Name 開發工具服務分頁 -Type Tab → 列全樹與 GDI 逐一核對 | `n01-enabled.json`、`n01-nested-tree.json`、`scratch/issue-1188/n01-tabs.png` |
| N01-4a | ✅ 通過 | RequestedName/TabItem.Name=聲音、IsSelected=true、TabSelection=[聲音]；GDI 顯示音量頁 | Select-UiaTab 聲音一次 → 讀回 → GDI | `n01-sound-result.json`、`n01-sound-tree.json`、`scratch/issue-1188/n01-sound.png` |
| N01-4b | ✅ 通過 | RequestedName/TabItem.Name=血盟、IsSelected=true、TabSelection=[血盟]；GDI 顯示血盟頁 | 重新 Find 巢狀 Tab → Select-UiaTab 血盟一次 → 讀回 → GDI | `n01-clan-result.json`、`n01-clan-tree.json`、`scratch/issue-1188/n01-clan.png` |
| 收尾 | ✅ 通過 | 自啟 PID 全退出、兩埠無監聽；17 角色皆 AccessLevel=0／OnlineStatus=0；原持久 DB SHA 不變 | 客戶端 CloseMainWindow → SELECT → 停 Java → mysqladmin shutdown → 核 PID／埠／SHA | `client-final-close.json`、`characters-final.tsv`、`cleanup.json`、`original-db-final.json` |

兩次 Select 的 TabItemError／TabSelectionError 均 null；巢狀、聲音、血盟 UIA 樹均無 Error。選血盟後多列標籤上下換列屬實際視窗行為，選取標籤和頁面一致。只切分頁，未操作頁內任何按鈕。

### 截圖證據索引（#1196 調整引用方式）

本機根目錄：`D:\lineage開發\TW1608262200研究\scratch\wt\task-1188`。下列路徑相對該 worktree；PNG 不追蹤進 git。六張原檔逐 byte 比對原提交 `b25ae7b5` 相同；目視結論沿用本報告原實測，不代表重新執行實機。

| 檔名 | 本機路徑（scratch） | 尺寸 | SHA-256 | 目視結論 |
|---|---|---|---|---|
| `g01-login.png` | `scratch/issue-1188/g01-login.png` | 826×711 | `153d68fdd392213cdb118d971fbfd5e2e186873abf8b59287dd083daa5fb374c` | 沿用 G01：登入／選角畫面與 CharacterList 證據一致。 |
| `n01-disabled.png` | `scratch/issue-1188/n01-disabled.png` | 826×711 | `fd23cc5111171fd28337c17827c407c20be28be01fc845ce7508a22edaa04a32` | 沿用 N01-1：CharacterList 的服務巢狀分頁停用。 |
| `n01-world.png` | `scratch/issue-1188/n01-world.png` | 826×711 | `b1171c98d11303b03a77dce527e422c73e8555d0c76a497f0d1421a4c2ae3d96` | 沿用 N01-2：世界狀態證據；地圖載入提示仍屬原報告額外觀察。 |
| `n01-tabs.png` | `scratch/issue-1188/n01-tabs.png` | 826×711 | `59fc5f764e9e86df9d1ad8db2bfc89dd7ded04911a37514061273538cb519467` | 沿用 N01-3：16 個標籤文字及位置一致，包含血盟。 |
| `n01-sound.png` | `scratch/issue-1188/n01-sound.png` | 826×711 | `a715f2f07e191177abd86d1ac92ea0ddf4cd642b87b7a1ad9a7ee80f2f47e943` | 沿用 N01-4a：選取聲音，GDI 顯示音量頁。 |
| `n01-clan.png` | `scratch/issue-1188/n01-clan.png` | 826×711 | `16bab92ed8d678b8bda1780257d36e6b09a2a426bdb259c83fa47502e0459e06` | 沿用 N01-4b：選取血盟，GDI 顯示血盟頁。 |

### N01-3 全部可見標籤

矩形為螢幕實體像素 `x,y,width,height`；視窗原點 `(490,490)`、826×711、DPI192。各矩形減去視窗原點即可對照未縮放的 `n01-tabs.png`；16 項皆 Offscreen=false。

| TabItem 名稱 | Rectangle | Enabled | GDI 逐項核對 |
|---|---|---|---|
| 背包 | `515,632,78,36` | true | ✅ 標籤文字及位置一致 |
| 地面 | `593,632,78,36` | true | ✅ 標籤文字及位置一致 |
| 魔法 | `671,632,78,36` | true | ✅ 標籤文字及位置一致 |
| 角色 | `749,632,78,36` | true | ✅ 標籤文字及位置一致 |
| 能力點 | `827,632,84,36` | true | ✅ 標籤文字及位置一致 |
| 個人商店 | `911,632,108,36` | true | ✅ 標籤文字及位置一致 |
| NPC | `1019,632,78,36` | true | ✅ 標籤文字及位置一致 |
| 交易 | `1097,632,78,36` | true | ✅ 標籤文字及位置一致 |
| 聲音 | `1175,632,78,36` | true | ✅ 標籤文字及位置一致 |
| 好友／封鎖 | `515,596,132,36` | true | ✅ 標籤文字及位置一致 |
| 決鬥 | `647,596,78,36` | true | ✅ 標籤文字及位置一致 |
| 求婚 | `725,596,78,36` | true | ✅ 標籤文字及位置一致 |
| 寵物 | `803,596,78,36` | true | ✅ 標籤文字及位置一致 |
| 血盟戰爭 | `881,596,108,36` | true | ✅ 標籤文字及位置一致 |
| 稱號／表情 | `989,596,132,36` | true | ✅ 標籤文字及位置一致 |
| 血盟 | `1121,596,78,36` | true | ✅ 標籤文字及位置一致 |

### 額外觀察，非本張地圖驗收

進世界及切頁結束均維持 InWorld／entered=true／decodeFailure=null，但 detail 原文為：

```text
地圖 4 資源無法載入：原始區塊只有尚未支援的 SEG／壓縮格式: 4/80048001。可取消連線或回離線預覽。
```

此現象不影響本張規定的 InWorld 狀態與巢狀 Tab 切換判定，**不宣稱地圖畫面載入成功或完整遊戲可用**。未修程式、未移動或換角色重試；交後續地圖任務判讀。

## 已驗證：建置、備份與環境

- 開始僅記 `query session`：console／CHAOREN／6／Active。lease holder=`chaoren0418-arch/tw-bin-engine#1188`、exp=`1794639004.5348225`（2026-11-14T06:50:04Z）；實機全程在期限內。
- `engine/build-product.ps1 -ConfigFile .github/nuget.ci.config`：product／launcher restore/build 全 0、PASS dev。`DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`；TEMP/TMP 在本張 scratch。未改 committed rebuild-lock.json。
- product DLL SHA-256：`58a017171ccd02a63a5003bc6b1a8fc8d21d47e4dad7f287f062420de9fddd24`；其餘來源 SHA 見 `source-hashes.json`。build log 內提示 inputs 與 committed lock 不同而跳過 receipt 比較；建置使用 temporary lock，不將此比較當通過。
- 啟動前 42061／43324 無監聽。05:22:22Z 冷備份完成：以拒絕其他寫入的 ibdata1 handle 保護原資料，複製完整 `environments/p24/db-data` → `backup-before/db-data` → 執行副本；1,722 檔、279,504,637 bytes，三份逐檔 SHA 一致。manifest SHA=`15cf9077329b82c4d1b57ea73c84455696cf45e145786d447ea966ce899db5f2`。
- D:/BinEngine 為既有專案別名；所有新檔均在本 worktree。原 config／jar／maps 與 engine/assets 以唯讀用途 junction 引用，未修改機密設定、原客戶端或封存，未複製 pak/map/sprite。
- SQL 核對 port=43324、datadir=`D:/BinEngine/scratch/wt/task-1188/scratch/issue-1188/db-data/`。Java cwd／home／tmp／log 均在本張 scratch。
- 客戶端參數：`--assets <worktree>/engine/assets --login-port 42061 --login-demo real-interactive --classic-ui --evidence <worktree>/scratch/issue-1188/client`；`TW_CLIENT_ROOT=D:/lineage開發/0-客戶端`。帳密只由既有 private 檔讀入環境，未輸出、提交。
- 自啟 PID：client4256、Java3524、MySQL父14088／子18888。client CloseMainWindow=true／Exited=true；核對 Java 執行檔及本張路徑後 Stop-Process；MySQL 指定 43324 mysqladmin shutdown=0。最終 RemainingOwned=[]、Listeners=[]。
- 最終 SELECT 17 角色全 GM0／離線，六欄前後逐字相同。05:25:06Z 原持久 DB 1,722 檔 SHA 不變、無新增檔。沒有手動 DML、建角、刪角、補物品；遊戲自動寫入只落隔離副本。
- 六張截圖全為 PrintWindow、826×711、Nonblank=true，已逐張目視；未用 WGC、@oai/sky、座標點擊或鍵盤硬切。

執行插曲：MySQL 啟動後首次 SELECT 太早（尚未 ready）回 ERROR 2003，ready 後成功取得前置 SELECT，未重啟或改設定。世界 Tab 證據保存後，一次性摘要的 PowerShell 陣列管線回 `Where-Object : The input name "Type" cannot be resolved to a property.`；改從已存 JSON 讀出陣列摘要即成功，未重做 UI 動作，UIA 樹本身無 Error。這兩項皆已解除，不是產品失敗。

還原 SQL：本張沒有手動資料修改，無 DML 需反向還原。若需還原隔離副本自動寫入，程序全停後從完整冷備份建立新副本並核 SHA；不覆寫原持久 DB。唯讀核對 SQL：

```sql
SELECT @@port, @@datadir;
SELECT account_name,char_name,objid,Type,AccessLevel,OnlineStatus
FROM p24_accounts.characters ORDER BY objid;
```

## 沿用，不重測

`f1179-activespells` 僅改 MagicStatus.cs 的 S250/20 解析，與 panel.js、UiaGdi.ps1、CharacterList 畫面無關；本輪不附帶回歸。

| 編號 | 結果 | 實際觀察 | 重現步驟／證據 |
|---|---|---|---|
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

- 已驗證：#1179 的進世界協定錯誤於本輪未再發生，N01 全部指定子條件通過。無需再次原樣重跑 N01。
- 推測：地圖提示與 SEG／壓縮格式支援有關；本張只保存原文，未追查其根因或影響範圍。
- 待驗證／未執行：地圖實際顯示、SM08／SM09／SM48、服務子頁功能、移動、戰鬥、交易、對話；均不在本張範圍。
- 下一棒：調度器／Claude 審閱本報告與證據、依流程合併；地圖載入提示交既有地圖任務判讀，不回退本張已通過的分頁結果。
- 產出：本報告、`docs/reports/issue-1188-evidence.json`、六張本機 `scratch/issue-1188/*.png`（不追蹤，見上方 SHA-256 表）。完整 log／一次性腳本／冷備份／隔離副本留本 worktree 的 `scratch/issue-1188/`；未遞迴刪除或移除 worktree。
