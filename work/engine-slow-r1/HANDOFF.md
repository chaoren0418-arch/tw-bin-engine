# ENGINE-SLOW-IMPL-R1

候選已完成，等待獨立覆核與指定整合者串行合入。共享 `engine` 未合入；沒有啟動正常產品、遊戲、伺服器或資料庫。這不是 r28／P24 原生實機驗收通過。

## 基準與所有權

- 唯一寫入工作區：`D:\lineage開發\TW1608262200研究\work\engine-slow-r1`。
- 基準：搬移後 `engine/source`、`engine/audio` 共 146 檔，先固定 SHA-256，再逐檔獨立複製；不是硬連結。`base-manifest.json` 記錄所有原始檔；完成時全部重驗不變。
- 唯一整合者：`01a17a22-17e4-70c0-9792-166ddb86ea42`。本工作者不寫其工作區，不自行合入或部署。
- 既有 worker Goal 實讀為 blocked，未重建、恢復或宣稱完成該舊 Goal。此交付是新派工的獨立候選。
- `assets` 是本工作區內指向封存 r28 素材的唯讀使用 junction；不得將建置或輸出寫入此 junction。沒有修改原 `engine/assets`。

| 固定來源 | SHA-256 |
|---|---|
| 原 BIN | `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2` |
| 固定 Server_Game.jar | `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf` |
| r28 seal | `83770091f8051466d5dccb420210cd0567ffef5a1158e8f7ebf5c7667127bfcd` |
| r28 product DLL | `7762951fba9a2d37d08e2d40a34dfa7f2a6f2219b93689a031a566a8fd8f8cb8` |
| 候選 product DLL | `a595559878d3e24b447d8be3a5c1e8ce595ff3a47dceb6877b9256ba00f12472` |

完整原路徑見 `source-pins.json`。r28 seal 的核驗是 manifest 本身的 SHA；未重新宣稱整份 r28 所有本文全量核驗。另有 95 個素材／metadata 本文對照 r28 integrity 驗證，見 `evidence/asset-pins.json`。

## 實作與限制

共 9 個來源檔變更，精確 before/after SHA 見 `changed-files.json`；可檢視 `changes.diff`，整合本文放在 `delta/source`。完整獨立來源與建置分別在 `candidate/source`、`candidate/audio`、`candidate/build`。

1. `NormalHaste.cs`：Speed2／Brave0 步行動畫與送出間隔為原基礎時間兩倍；640ms profile 為 1280ms。Speed0 保持 640ms，Speed1 動畫 480ms、送出 483ms。Speed3／17／18 與任何非零 Brave 不開放。狀態正確顯示「緩速」，期限只供顯示，不擅自恢復速度。
2. `OnlineMovement.cs`：每次權威速度回報立即保留較長冷卻；0→2→0 在 UI 尚未輪詢時仍保留 1280ms 邊界。拒絕排隊請求及已准入送出的非同步完成不覆寫較長間隔。既有 generation、world、位置校正、外觀、SpeedRevision、取消、交易、死亡與送出前閘門保留。
3. `WorldObjects.cs`、`ObjectRendering.cs`：S10 記錄當步 MotionSpeed／MotionBrave，之後的速度回報不改寫這一步的時長與幀進度；下一步採新速度。本人已有 NetworkStep 快照機制，沿用並測試。
4. `Combat.cs`、`Magic.cs`、`Inventory.cs`、`Ground.cs`：緩速時攻擊、施法、道具／裝備使用、拾取及丟棄明確拒絕。排隊動作新增 SpeedRevision 重驗，避免 0→2→0 又放行。Speed0／1 既有動作間隔保留；本候選不宣稱它們原有的攻擊／施法節奏已完整對照原生。
5. `PlayerPointer.cs`：緩速滑鼠行走可用；滑鼠攻擊、拾取與交談保持關閉並顯示原因。路徑原有 SpeedRevision stamp 保留，同值回報也取消舊路徑。

固定 JAR 的 ATTACK／SPELL 也有 slowdown 倍率，但本候選選擇派工允許的「明確未支援閘門」，沒有部分開放緩速戰鬥。遠端攻擊／施法的動畫節奏未在本次認證。非滑鼠 NPC／交易／商店等既有非動畫流程未擴張；仍消費 MovementBusy 與各自狀態閘門。所有共用消費端決策見 `consumer-audit.json`。沒有新增補給、商店、交易或其他功能。

## 驗證證據

| 層次 | 結果與邊界 |
|---|---|
| 原 BIN 位元組 | `native-byte-audit.json` 將複製的研究函式 chunks 逐位元組對照固定 BIN；VA／RVA／File Offset 均列出，ImageBase `0x400000`。 |
| 原 x86 函式 | `native-speed-oracle.json`：57 組 0／1／2 計時。函式 VA `0xA407D0`，RVA `0x6407D0`；Unicorn 執行原指令，metadata 與中性狀態 callee 是明示替身，不是原生客戶端執行。 |
| 固定 JAR | `evidence/server/result.json`：171 組 MOVE／ATTACK／SPELL 計算與 21 個 S_SkillHaste 序列化封包。直接載入固定 JAR，SpeedState／輸入值為合成；SecurityManager 拒絕 connect/listen，沒有呼叫 DB／遊戲啟動。class 本文與依賴 pins 均保留。 |
| r28 缺口重現 | `evidence/baseline-result.json`：4 checks，確認 Speed2 移動被擋、遠端不出圖、沒有緩速文字及無 socket 事件。載入的 DLL SHA 等於指定 r28 DLL。 |
| 候選實作 | `evidence/candidate-result.json`：1,371 checks，載入實際候選 DLL。涵蓋 0↔2、1↔2、0↔1、同值回報、所有未知 speed bytes／非零 Brave、本人與遠端當步／下一步、精確冷卻邊界、transient slow、送出准入與完成之間回報、排隊請求、地圖／重登／死亡／HP／交易／取消／外觀／校正閘門，以及非移動／滑鼠動作。 |
| 建置 | Release 成功，0 warnings、0 errors。只執行 console checks DLL，沒有執行正常產品 EXE，也沒有建立 Form／WebView。 |
| 保全 | `evidence/preservation.json`：146 個共享來源、4 個固定 pins 不變；95 個素材本文對照 sealed integrity。 |

Managed tests 使用合成 in-memory actor／packet／clock／sender seam，但跑實際候選方法與 renderer。`evidence/slow-walk.png` 是原素材與合成 actor 的離線 composition debug 圖，保留原場景遮擋；幀與時長以測試 assertions 為證，圖片不是 native screenshot。未做伺服器登入、socket、資料庫、角色或服務操作。

測試初版曾一次以 320ms 呼叫 World.Advance，觸發原有每次最多 250ms 的 clamp；已把 fixture 改成 40ms ticks，沒有修改引擎 clamp。最終數字只指最後全數通過的結果。

## 重跑與合入

封存後不要在本目錄重跑會覆寫輸出的腳本。覆核者將 `candidate`（略過 obj/bin）、`checks`、`vendor`、`native`、`NuGet.Config`、`native_speed_oracle.py`、`SlowServerOracle.java`、`run_oracle.py`、`run_checks.py` 複製到自己核准的 `work/<review-scratch>`，在該 scratch 建立 `evidence`、`tmp`、`cli-home`、`nuget`，並建立指向 `releases/r28/assets` 的唯讀使用 `assets` junction。腳本依所在 work 子目錄求得專案根，不使用歷史外部根的建置位置。

於該 scratch 依序執行以下命令（此處 `$scratch` 必須指向覆核者自己的可寫副本）：

```powershell
$python = 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe'
& $python -B -X utf8 (Join-Path $scratch 'native_speed_oracle.py')
& $python -B -X utf8 (Join-Path $scratch 'run_oracle.py')
& $python -B -X utf8 (Join-Path $scratch 'run_checks.py')
```

需求：現有 Python pefile／unicorn、.NET 10.0.300、JDK 17.0.17；NuGet 僅使用隨附 vendor。建置 env 將 CLI home／NuGet／TEMP／TMP 指向 scratch，停用 telemetry、workload notify 與 ASP.NET dev certificate generation。初次 SDK 啟動曾輸出開發憑證設定訊息；未執行任何憑證管理命令，也未檢查／修改系統憑證存放區。後續 runner 已明確停用自動產生。

`bootstrap.py`、`implement.py` 是建置過程紀錄；候選已包含後續具名 patch，不要對交付再執行這兩個腳本。重建以已交付完整候選來源為準。不要在封存目錄執行 `prepare_delivery.py` 或 `seal.py`。

覆核接受後，由指定整合者核對 `changed-files.json` 的 base SHA 才套用 9 個 delta；若共享來源已變動，先解決差異，不覆蓋。原生連線驗收留待主管另行安排；DB 交由使用者處理，本候選沒有 DB 前置工作。交付後本工作區停寫。
