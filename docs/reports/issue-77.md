# Issue #77／T062 第 1 輪：丟棄拒絕後恢復操作

本輪完成客戶端根因 B／C 修正；根因 A 裁定為原服拒絕條件，不改 wire、不繞過規則。DEMO-2 原 DLL 的選取失效已離線重現，修正後原有地面 722 項與 combat 8,780 項全數通過，新增 86 項，共 **9,588 項**。**第 2 輪與 M1 尚未完成**；本輪不更新 demo 成功標記。

分支 `task/77`，起點 `bb27a49c9987958d8af20d60bda59478cf7275d3`。全程在指定 worktree；主專案、原客戶端與資源僅唯讀。已讀 AGENTS.md、docs/REPORT.md、TASKS.md、#49／#51／#71 報告。此起點的 TASKS.md 尚無 T062／新版 Backlog 編號，依 Issue #77 本輪明列範圍執行，未自行改里程碑。

## 已驗證（離線）

### A：C25 格式正確，125 是原服的丟棄拒絕

唯讀分析 `D:\lineage開發\TW1608262200研究\environments\p24\server\Server_Game.jar`，SHA-256：

`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`

與 #49 完全相同。`C_DropItem.class` SHA-256 為 `5b097f52ad2f3b7f6836ceb4124208adc8d9f4c8ddf172c658456f5629225319`，亦與 #49 相同。使用 JDK 17 `javap -J-Dfile.encoding=UTF-8 -c -p -classpath <jar> <class>`，未載入或執行伺服器 handler。完整採用摘錄、各 class 與 javap SHA 見 [static-evidence.json](../../work/issue-77/static-evidence.json)。以下 offset 均是 **JVM 方法內 bytecode offset**，不是 File Offset／RVA／VA。

| 方法／offset | 已核實行為 |
|---|---|
| `PacketHandler.handlePacket` 1137–1158 | opcode 25 建立 `C_DropItem` 並呼叫 `start` |
| `ClientBasePacket.read` 20–25 | 跳過 opcode，從 payload File Offset 1 讀取 |
| `C_DropItem.start` 5–26 | 依序 `readH x`、`readH y`、`readD objectId`、`readD count` |
| `ClientBasePacket.readH/readD` 0–10；`readUnsigned` 25–35 | 2／4 bytes，小端序；每 byte `&255` 後左移 `8*i` |
| `C_DropItem.start` 76–86 | `pc.getInventory().getItem(id)`；id 是背包實例 ObjectId，不是模板 ID |
| `C_DropItem.start` 28–56 | count 是 Java signed int；非正值提前返回 |
| `C_DropItem.lambda$start$0` 10–23 | `World.getInventory(x,y,pc.getMapId())` 作為落地容器，再 `tradeItem`；x/y 是落地格座標 |

`GroundRules.Request` 現行 C25：File Offset 0=25、1–2=x、3–4=y、5–8=背包 ObjectId、9–12=count、13–15=零 padding；`SendGround` 丟棄時傳角色當前 x/y 作落地格。與原服讀法相符，沒有把畫面上的金幣數量 33400 或模板 40308 當成 ObjectId。沒有修改任何 wire 欄位。

`C_DropItem.start` 的兩個 125 分支：

```text
108: ... pc.isGm()
113: ifne 234
116: invokestatic ItemDropTable.get
121: invokevirtual L1ItemInstance.getItemId
124: invokevirtual ItemDropTable.isDroppable
129: getstatic ConfigAlt.DORP_ITEM
132: ifne 159
135: iload 9
137: ifne 159
146: bipush 125
148: invokespecial S_ServerMessage.<init>(int)
151: invokevirtual L1PcInstance.sendPackets
158: return

374: invokevirtual L1ItemInstance.isEquipped
377: ifeq 399
386: bipush 125
388: invokespecial S_ServerMessage.<init>(int)
391: invokevirtual L1PcInstance.sendPackets
398: return
```

因此 125 的充分條件為：**非 GM，且 `DORP_ITEM=false`，且模板不在 `ItemDropTable` 白名單**；或 **實例已裝備**。白名單判斷 `isDroppable` offset 0–13 是 `Set.contains(templateId)`；`load` offset 25 的 SQL 是 `SELECT itemid FROM 道具_物品強制丟棄`，offset 94–108 加入白名單。這裡只讀 JAR 字串，**未執行 SQL、未讀伺服器機密設定，也未更改白名單**。

125 在 transfer 前返回，不會移轉物品；不可交易／限時／封印等分支另回 210，移轉失敗回 623。DEMO-2 的「收到 125、背包仍 33400、沒有地面金幣」與此拒絕路徑相符。#49 七筆模板的 tradable=true **不等於**伺服器允許丟棄。

**裁定與證據界線：**本次 DEMO-2 的金幣丟棄已由原服拒絕；金幣不是裝備，已知資料支持開關／白名單分支。JAR 證明兩個條件與欄位意義，但不包含當時 DB 白名單及配置實值，因此不宣稱已離線讀出 `DORP_ITEM=false` 或所有環境永遠禁止金幣。依本 Issue 的替代驗收規則，第 2 輪改用已在客戶端 catalog 啟用的 **治癒藥水 40010（gfx45／icon80）**；能否被該實驗服接受仍須真機確認，不在本輪冒稱可丟。

### 125 的原客戶端文字

唯讀解碼目前客戶端的 `Text.idx`，以 `string-c.tbl` 名稱找唯一 record，從 `Text.pak` 讀 bounded slice，再依既有 `PakTools.Decode`、CP950／CRLF 解碼。完整 payload 都留記憶體，未複製 PAK 或完整表進專案。

- Text.pak SHA-256：`9516a372c43b269cf1af7adc0ac00abd4163e1235dcd846e34294849616a37ff`。
- Text.idx SHA-256：`43a342ad3929b541096630165c04794192c48359ecbf116b538eaffc49741f6f`。
- record 32559，索引 File Offset 911656；PAK File Offset **22783426**，長度 **105931**。
- 表頭 `3484`，其後採 zero-based ID；ID 125 在解碼後表的 File Offset **3210**。
- 原文字：`\f1你不能夠放棄此樣物品。`；`\f1` 是格式前綴，顯示文字為「你不能夠放棄此樣物品。」。
- 解碼後表 SHA-256：`4ebbc1a7a002a4c1dcdfa73dbf056b0e00648e0789763410c40e5079a22d43e9`。

來源摘要見 [text-source.json](../../work/issue-77/text-source.json)，解碼器 DLL／來源 SHA 見 static-evidence。歷史 P04 解碼索引的整檔 SHA 與本次即時解碼結果不相同，故沒有把歷史索引當本次定位依據；採用的是目前原始 Text.idx 直接解碼的 record。產品只新增無參數 125 的已知文字；帶非預期參數仍保持 unknown。

### B：第二次未送出是快照過期，不是丟棄 pending 卡住

1. WinForms／Web 選取時，保存 `GroundQuantitySelection(Epoch,Id,Selection,Revision,Available)`。
2. 原碼 `ReceiveInventory(71)` 在訊息解碼後，無條件 `InventoryRevision++`；S71/125 沒有修改物品，但使該快照的 Revision 過期。
3. 原 `SyncQuantitySelection`／Web `rememberQuantity` 只在 epoch／物件／選取版本變更時刷新快照，刻意不跟隨背景版本更新。因此輸入 10 後在 `SendGroundQuantity` 即被擋，不會進入傳送 gate。
4. 丟棄沒有 `pendingPickup`；`groundSending` 的 owner／lease 在完成後正常釋放，900ms guard 也正常到期。不能以清除拾取 pending 或取消節流來修這個問題。

離線重現使用 DEMO-2 同一份產品 DLL（#70 SHA `4b1e5ac8841edaf7963f45154a97bec2611a678bbffc66b5192c635f1176d0e5`），唯讀引用到本 worktree 的測試宿主，沒有啟動產品入口。新增案例在「`rejection preserves unchanged quantity snapshot`」斷言如預期失敗；見 [baseline-result.json](../../work/issue-77/baseline-result.json)。

修正新增 `InventoryContentRevision`，只由 S5／15／24／242／57／100／240 物品封包增加。地面數量快照與丟棄送出前 guard 改用此版本；原 `InventoryRevision` 繼續驅動訊息／UI 刷新，其他交易及裝備流程的版本語義不變。Web 額外傳 `quantityRevision`，原狀態 `revision` 保留。真正的物品更新即使數量回到原值（ABA），仍需重新選取；world epoch、選取、裝備、死亡、距離、lease、900ms 與拾取 3000ms 保護均保留。

沒有將 125 當成成功確認，沒有預扣／補回數量，沒有自動重送。使用者在原有節流結束後可用未變動的選取再手動操作。

### C：不同拒絕原因分開顯示

- 尚未選物品：「未送出：請先選取物品」。
- 數量格式／範圍不合法：「未送出：數量不合法，請輸入 1 到持有量（最多 2147483647）的整數」。
- 數量合法但快照已變：「未送出：選取已過期，請重新選取物品」。

同時不合法時先報數量，修正輸入後才驗選取，不再於同一句混用兩個原因。WinForms 與 Web 宿主共用驗證；Web 本地拒絕也採同一組數量／未選取提示。

### 驗證與產出

| 檢查 | 結果 |
|---|---:|
| dev `--update-lock`、完整 build、`--verify` | 190 source／199 pins；通過 |
| 主程式／launcher | 各 0 警告、0 錯誤 |
| GroundChecks | **361**（原 309＋新增 52） |
| 舊肉／箭 lifecycle candidate、extra | **252＋87** |
| 實際 panel.js＋DOM 替身 | **72**（原 38＋新增 34） |
| PacketTraceChecks | **36** |
| 地面小計 | **808**（保留原 722） |
| CombatSfxChecks | **8,780／7 組** |
| 合計 | **9,588** |
| #49 嚴格重播 | 6 fixtures／26 semantic cases／81 來源 hash checks 通過 |
| catalog 來源交叉驗證 | 7 模板／4 sprite pins／3 禁用列／原 fixture bytes 通過 |
| `git -c core.whitespace=cr-at-eol diff --check` | 通過 |

新增回歸沿用 #49 原 `gold-s15`，另由 JAR `S_ServerMessage(int)` offset 9–22 推導 S71/125 `47 7D 00 00`（測試 padding 為 `47 7D 00 00 00 00 00 00`）。涵蓋拒絕於傳送完成前／後、送出前 gate、恢復後手動送 10、900ms 仍拒連點、沒有財物預扣、訊息刷新、真實物品 ABA 仍擋、重新選取後可送、0／超額錯誤文案與 125 文字。這些是明示合成回包與記憶體 sender，**不是 Java handler 執行、實機擷取或端到端證明**。

最終產品 DLL SHA-256：`0a34f0bb136bd530f5f603c3a60a0b1e8965882d9986ef31d126ff570aa4635e`。完整建置與測試身分見 [validation.json](../../work/issue-77/validation.json)、[ground-result.json](../../work/issue-77/ground-result.json)、[web-result.json](../../work/issue-77/web-result.json)、[combat-result.json](../../work/issue-77/combat-result.json)。沒有建立 receipt；`--verify` 核對來源與建置 manifest，不冒稱 receipt 核驗。

只更新 dev lock。未改封存 lock／release、assets junction、正式 BIN／客戶端、docs/REPORT.md 或機密設定；未啟動、附加或停止遊戲／Java／DB，未連正式服務。

### 重現方式

使用 SDK 10.0.300、Python 3.12、Node；先將 `DOTNET_CLI_HOME`、`NUGET_PACKAGES`、`TEMP`、`TMP` 指向 worktree 內。`scratch/issue-77/NuGet.Config` 僅指向原專案既有離線 `engine/vendor`，不複製套件到原專案。可連 NuGet 的環境可換 `.github/nuget.ci.config`。

```powershell
python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock
python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/issue-77/NuGet.Config --output scratch/issue-77/build
python -B -X utf8 engine/_integration/dev/rebuild.py --verify
dotnet build engine/_integration/dev/ground-tests/GroundChecks.csproj -c Release -o engine/build/issue-77-tests --configfile scratch/issue-77/NuGet.Config
dotnet engine/build/issue-77-tests/GroundChecks.dll . work/issue-77/ground-result.json 'D:\lineage開發\0-客戶端' 'D:\lineage開發\TW1608262200研究\engine'
node engine/_integration/dev/ground-tests/web-checks.cjs . work/issue-77/web-result.json
python -B -X utf8 engine/_integration/dev/ground-tests/verify-sources.py
python -B -X utf8 docs/reports/issue-49-replay.py --source-root 'D:\lineage開發\TW1608262200研究' --require-qualified
python -B -X utf8 engine/_integration/dev/ground-tests/legacy.py --original 'D:\lineage開發\TW1608262200研究'
dotnet run --project engine/_integration/dev/packet-trace-tests/PacketTraceChecks.csproj -c Release
```

combat 沿用 #51 的測試宿主調整：只將 `assets` 讀取指向原專案 `engine/assets`，AppContext base 指向原專案 `engine/build/offline-checks`；測試副本位於本 worktree scratch，AssemblyName 保持 `CombatSfxChecks` 供原有 friend assembly 使用，引用本次產品及音訊 DLL。所有斷言原樣執行；原始／副本 SHA 見 [host-adaptation.json](../../work/issue-77/host-adaptation.json)。測試沒有播放聲音。一次性來源收集、文字 probe、建置與宿主調整腳本留 `scratch/issue-77/`，交付結論與必要證據不依賴 scratch 保留。

## 待第 2 輪真機

**給下一棒的提示：**Claude／雲端主控先審查本 PR，由使用者合併；合併後仍使用 Issue #77，主控改 runner 為 dot 並重排第 2 輪。本 PR 不關閉 Issue、不宣稱 M1 完成。

1. 重新建置合併後 dev，再驗金幣拒絕後可繼續操作；送出仍由原服裁定。將「本次原服金幣不可丟」列入 demo Backlog，原因連回本報告 JAR 證據；**不要修改 DB／配置來讓金幣通過**。
2. 依 Issue 替代條款，以 **治癒藥水 40010** 補做 1→撿回、10→撿回、0／超額拒絕。此物品已有完整 catalog／圖像與離線丟撿支援，但本輪未證明原服允許；若仍被拒，保留回應與 Backlog，不標 ✅。每次成功後背包／地面有權威更新，下一次先重新選取，避免把設計中的快照過期當成回歸。
3. 補齊 DEMO-2 所有 ⚠️／❌：近戰與光箭使用有效弱怪（目標走遠就重選，最多 30 次），截減血／死亡＋EXP；受傷後治癒及藥水 HP 差；卸裝欄更新後截圖；空曠處八方向 16 筆 C29；聊天由調度器先放行；攻擊與施法各自峰值 >0、靜音 =0。
4. 更新 `docs/demo/README.md` 的 15 步與截圖；每列 ✅，或 ❌／⚠️ 附 Backlog 原因。依本 Issue 金幣替代條款，替代物品完整丟撿與事件音量仍必須真機 ✅；不得用本輪 9,588 項離線通過代替。新版 TASKS 編號由主控提供後對齊，不沿用舊頁錯配編號。

未驗證：修正後 WinForms／Web 實際點擊與畫面、伺服器當時白名單／配置值、治癒藥水的真機丟撿、所有第 2 輪補測及 M1 驗收。未進行本輪真機截圖或遊戲連線。

## 第 2a 輪：主專案同步與 dev 建置（2026-11-06）

### 做了什麼／已驗證

本輪在主專案 `D:\lineage開發\TW1608262200研究` 完成同步、dev 完整建置與 check-only，提供 dot 第 2b 輪使用。建置與入口檢查均退出 **0**；主程式與 launcher 各 **0 警告、0 錯誤**。check-only 的 `revision: dev`、`uiStarted: false`、`networkStarted: false`、`servicesStarted: false` 均已斷言確認。**第 2a 輪完成不代表 M1 或第 2b 輪完成。**

已讀根目錄 AGENTS.md、docs/REPORT.md、既有 #77 報告與入口實作。初始分支為 `task/81`，`git status --porcelain --untracked-files=no` 無輸出；fetch 後將本機 main 從 `71ebe7eddf2613f20b86b1f009745ccf7c27ea8a` 快轉到下列建置來源：

- HEAD、main 與本輪 fetch 的 origin/main：`0b747de4e230c72906c830be821de322894acbf2`，即 PR #80 合併提交。
- `git merge-base --is-ancestor 0b747de HEAD` 退出 0。
- `engine/source/Inventory.cs:32` 宣告 `InventoryContentRevision`；第 99 行僅在指定物品更新 opcode 遞增，已用 Select-String 確認符號存在。
- dev 原有 lock 核驗 **190 source／199 pins** 通過；未執行 `--update-lock`。
- receipt 建置時間：`2026-11-05T21:12:21.590569+00:00`（UTC），SDK `10.0.300`。
- DLL 實檔 SHA-256 與本輪 result／receipt 完全一致；manifest 實檔 SHA-256 與 check-only／receipt 一致。

依本輪具體步驟沿用分支，未使用通用前言的 `checkout -B` 重設分支。原 `task/77` 位於 `scratch/wt/task-77`；先確認該 worktree 追蹤檔乾淨、`task/77` 是 origin/main 祖先，再於該 worktree 執行 `git checkout --detach`，保留原 HEAD `f6d70b649087a79a2f0f84331d691b37340d878d` 與全部檔案。主目錄接著 `git checkout task/77`、`git merge --ff-only origin/main`，均成功。最終主目錄留在 `task/77`，本輪唯一追蹤檔變更是本報告；產品輸入等於上述 main。

### 指令、退出碼與環境處理

| 指令／檢查 | 退出碼／結果 |
|---|---|
| `git status --porcelain --untracked-files=no` | 0；同步前與建置後皆無輸出 |
| `git fetch origin` | 0 |
| `git checkout main` | 0 |
| `git merge --ff-only origin/main` | 0；快轉至 PR #80 |
| `git merge-base --is-ancestor 0b747de HEAD` | 0 |
| `Select-String -LiteralPath engine/source/Inventory.cs -Pattern InventoryContentRevision` | 命中第 32、99 行 |
| `python -B -X utf8 engine/_integration/dev/rebuild.py --build --write-receipt --output scratch/issue-77/rebuild-2a` | 0 |
| sdk／product-restore／product-build／launcher-restore／launcher-build | 各 0 |
| `engine\啟動新版客戶端.cmd --check-only`（經 `cmd.exe /d /c`） | 0 |
| check-only 欄位與 receipt／result／實檔雜湊比對 | PASS |
| `git merge-base --is-ancestor task/77 origin/main` | 0 |
| `git -C scratch/wt/task-77 checkout --detach` | 0；保留原提交與檔案 |
| 主目錄 `git checkout task/77`、`git merge --ff-only origin/main` | 各 0 |

一般命令工具在程序建立前發生 `helper_unknown_error: setup refresh had errors`，改用執行器核准的本機模式後成功。`rg` 不在 PATH，改用 `git ls-files` 與 PowerShell 查找。第一次呼叫 `python` 時 PATH 未包含它，建置尚未執行；以 `py -0p` 找到既有 Python 3.12，僅補入本次程序 PATH 後執行指定指令，未安裝工具或更改系統設定。

`NUGET_PACKAGES` 限 `engine/.nuget`；暫存限 `scratch/issue-77`，rebuild 自行將 TEMP／TMP／DOTNET_CLI_HOME／NUGET_SCRATCH 限於 `rebuild-2a` 證據樹。建置前檢查既定產品目錄、launcher、manifest、receipt，沒有 symlink／junction 或多重硬連結；assets 沿用既有唯讀 junction。沒有改產品程式、lock、機密設定或 docs/REPORT.md；沒有 reset、stash、刪檔、啟動遊戲、P24Lab、DB 或正式服務。

### 本輪建置結果 JSON

```json
{
  "status": "pass-dev-build",
  "sourceFiles": 190,
  "GUI": false,
  "network": false,
  "database": false,
  "productDllSha256": "44eb5843f25833f2da1cdf7081eabeb7b8cd6e27562423e2d06b150ac787d477",
  "audioDllSha256": "2cdc6e2fc4df4f276ff7cbda04098ee1eea5e0fe2b9307aba5e8315076aa54c5",
  "productManifestSha256": "2963b5224dbe9f3b95966778839e3f572767e8bd8a326632fdae0a2b751e9cc3",
  "launcherDllSha256": "d4f30315da7046a0f11a386ff066608baaf63ea17f156da0e5633964bb8725eb"
}
```

### 本輪 check-only JSON

以下為實際 JSON 解析後以 UTF-8 重新排版；欄位與值不變。`arguments` 描述產品入口參數，不表示本輪有啟動產品。

```json
{
  "status": "checked-only",
  "revision": "dev",
  "executable": "D:\\lineage開發\\TW1608262200研究\\engine\\build\\client-product-dev\\P11.ClientEngine.exe",
  "workingDirectory": "D:\\lineage開發\\TW1608262200研究\\engine",
  "arguments": [
    "--normal-product"
  ],
  "manifest": "D:\\lineage開發\\TW1608262200研究\\engine\\build\\product-dev-manifest.json",
  "manifestSha256": "2963b5224dbe9f3b95966778839e3f572767e8bd8a326632fdae0a2b751e9cc3",
  "hashedRuntimeAndWebFiles": 46,
  "assetFilesCheckedForPresenceAndSize": 13957,
  "assetContentVerification": "Pinned integrity manifest; body hashes remain verified by the product when loaded, matching prior entry contract.",
  "runtime": {
    "architecture": "x64",
    "loadedCoreVersion": "10.0.8",
    "loadedCoreDirectory": "C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\10.0.8\\",
    "desktopVersion": "10.0.8",
    "desktopDirectory": "C:\\Program Files\\dotnet\\shared\\Microsoft.WindowsDesktop.App\\10.0.8"
  },
  "webView2Version": "156.0.4314.58",
  "powerShellRequired": false,
  "pathLookupUsed": false,
  "autoLogin": false,
  "uiStarted": false,
  "networkStarted": false,
  "servicesStarted": false
}
```

### 本輪檔案身分（SHA-256）

```json
[
  {
    "path": "engine/build/client-product-dev/P11.ClientEngine.dll",
    "size": 4994560,
    "sha256": "44eb5843f25833f2da1cdf7081eabeb7b8cd6e27562423e2d06b150ac787d477"
  },
  {
    "path": "engine/_integration/dev/receipt.json",
    "size": 474,
    "sha256": "5265090d505c86b4716db2dff8e1bfb67dfc62902126d63131fed170d5b50302"
  },
  {
    "path": "engine/_integration/dev/rebuild-lock.json",
    "size": 28757,
    "sha256": "855863f1fc073b1bbc9a9adcc5ddde342e27bedbd04dd9d2715e71f985e9f802"
  },
  {
    "path": "engine/build/product-dev-manifest.json",
    "size": 7953,
    "sha256": "2963b5224dbe9f3b95966778839e3f572767e8bd8a326632fdae0a2b751e9cc3"
  },
  {
    "path": "engine/launcher/P24.Launcher.dll",
    "size": 24064,
    "sha256": "d4f30315da7046a0f11a386ff066608baaf63ea17f156da0e5633964bb8725eb"
  }
]
```

### 產出路徑

- 唯一提交文件：`docs/reports/issue-77.md`，保留第 1 輪證據並新增本節。
- 主程式：`engine/build/client-product-dev/P11.ClientEngine.dll`；manifest：`engine/build/product-dev-manifest.json`。
- 既定啟動器：`engine/launcher/`；receipt：`engine/_integration/dev/receipt.json`。
- 本機證據：`scratch/issue-77/build-2a.stdout`、`build-2a.stderr`、`build-2a.command.json`；`rebuild-2a/` 下的子命令、stdout／stderr、`result.json`。
- 入口證據：`scratch/issue-77/check-only-2a.json`、`check-only-2a.stderr`、`check-only-2a.command.json`；雜湊：`identities-2a.json`。
- 一次性腳本：`scratch/issue-77/build-2a.py`、`check-2a.py`、`report-2a.py`；scratch 與建置產物均不提交，主要證據已內嵌本報告。

### 未驗證什麼／推測界線

未做第 2b 輪任何登入、遊戲畫面、丟撿、近戰／光箭、HP／EXP、卸裝、八方向、聊天或音效真機測試，未新增截圖，也未修改 demo 頁。check-only 只核驗入口環境、46 筆 runtime／web／索引雜湊，以及 13,957 筆素材存在與大小；未逐一核對所有素材內容 hash。未重跑第 1 輪離線測試，未宣稱其結果為本輪新增驗證。無推測性的遊戲功能通過結論。

### 給下一棒的提示（以本輪派工為準）

1. 雲端主控／Claude 審查本報告並完成既定合併流程，再將同一 Issue #77 改 runner 為 **dot**、重排第 2b 輪；本輪不關閉 Issue 或宣告 M1 完成。
2. dot 使用主專案既有 dev 產物，先比對本節 DLL／receipt 身分；本輪文件提交不改產品輸入，無須為文件重新建置。若另行建置，需記錄新的 receipt 與雜湊。
3. 金幣丟棄被 125 拒絕列 Backlog，引用本報告第 1 輪 JAR 證據，不改 DB／配置。依派工先試治癒藥水 40010：丟 1／10 後各撿回，0／超額應拒；若同被 125 拒絕，改試肉丟 1 後撿回。每次權威更新後先重新選取。
4. 依第 2b 輪要求補齊有效目標近戰／光箭（最多 30 次、減血／死亡＋EXP）、受傷後治癒／藥水 HP 差、卸裝、八方向 16 筆 C29、獲放行後聊天，以及攻擊／施法峰值與靜音 = 0。
5. 更新 `docs/demo/README.md` 各列為 ✅ 或 ❌／⚠️ 加 Backlog 原因。本次 main 的 TASKS.md 查無 T062，故未自行改任務表；第 2b 輪由主控對齊新版 M1 T062、M2 T070–T074、Backlog B01–B11。真機結果由 dot 記錄，不用本輪建置通過代替。

## 第 2b 輪：前置查核完成，原生補測 blocked（2026-11-06）

### 結論與驗證範圍

本輪狀態為 **blocked／未完成**，不是驗收通過。最新派工是 [issuecomment-6669870333](https://github.com/chaoren0418-arch/tw-bin-engine/issues/77#issuecomment-6669870333)。已確認前置版本、P24Lab 身分、啟停與清理；啟動客戶端遭自動安全審查拒絕，未執行原生補測，未登入、未新增截圖或聊天訊息。第 1 輪與第 2a 輪沒有重做。

### 去重與版本查核（已驗證）

- 已讀根目錄 AGENTS.md、CLAUDE.md、docs/REPORT.md（唯讀）、#77 Issue 與全部 11 則留言、第 1／2a 輪報告、#71 報告及既有 native-ui／啟停腳本。查找專案及父層 `.agents/skills`，未找到相關目錄。
- 查核當時最新 RESULT 仍是第 2a 輪 `e21241d`。本機 #77 調度 log 僅第 1／2a 輪，scratch/issue-77 僅 2a 產物；Codex 對話清單未發現另一個正在執行的第 2b 輪。未發現已完成或執行中的同輪工作。
- 主目錄最初為 `task/81@289b454`；reflog 顯示 2a 完成後被 #81 切換。#81 `.last.md` 已回報完成及推送，追蹤檔乾淨，故切回既有 task/77，未重設分支或覆蓋檔案。原 worktree `scratch/wt/task-77` 仍 detached 於 `f6d70b6`，沒有改動。
- 本輪起始 HEAD：`e21241d7b69a43c01ad74c6cd07a5ca0b6b69ce2`；`0b747de4e230c72906c830be821de322894acbf2` 為祖先，核驗退出 0。
- `engine/啟動新版客戶端.cmd --check-only` 成功，回傳 `status=checked-only`、`revision=dev`、`uiStarted=false`、`networkStarted=false`、`servicesStarted=false`。核對 46 個 runtime/web 檔與 13,957 個素材存在／大小；未把它當成真機成功。
- DLL SHA-256：`44eb5843f25833f2da1cdf7081eabeb7b8cd6e27562423e2d06b150ac787d477`；manifest SHA-256：`2963b5224dbe9f3b95966778839e3f572767e8bd8a326632fdae0a2b751e9cc3`，與第 2a 輪相符。未 fetch、重建、修改產品或 lock。

### P24Lab 身分及啟停（已驗證）

舊 `D:\P24Lab-ClientEngine` 路徑不存在。`D:\BinEngine` 是指向本專案的 junction；#71 報告有搬遷與相同實例的既有證據。啟動前讀取 auto.cnf 的 UUID，並只篩選 mysql.ini 的 basedir/datadir/port/bind-address；未讀 private-root.cnf 內容或 config/server.properties，未輸出密碼。

啟動前在既定 `environments/p24/backups/issue-77-round2b-before-demo/db-data` 建立冷備份，893 個檔案逐一 SHA-256 比對相符。最初一般模式建立目錄遭 Windows Access denied，未啟動 DB；確認沒有部分啟動並核對使用者常設授權後，經 require_escalated 的安全核准完成同一路徑操作，未換路徑繞過。

MySQL 啟動後唯讀身分查詢符合三項：UUID `e3443e40-d090-11f1-8040-4cedfb679789`、port `43324`、datadir `D:\BinEngine\environments\p24\db-data\`。本輪 mysqld 父／子 PID 10736／16488。JAR 與隔離 policy SHA 符合 #71 既有腳本，啟動 Java PID14908，僅本輪實驗 server 42061。啟動 log 有 component_reference_cache／ICU 資料缺失警告，但 MySQL 已 ready、身分查詢成功，server 亦啟動完成；未修配置或資料來繞過警告。

沒有執行資料修改 SQL、測試角色查詢或遊戲操作。只有身分 SELECT 與正常 SHUTDOWN；伺服器啟動自身可能產生正常實驗資料寫入，本報告不宣稱實驗 DB 位元組完全未變。

### 具體阻塞／安全審查

動作：執行 `scratch/issue-77/round2b/launch.ps1`，以既有 launcher 啟動 `engine/build/client-product-dev/P11.ClientEngine.exe`，預定只連 P24Lab 42061 並記錄 demo3 trace。

狀態：**denied，確定未執行**。自動安全審查原文：

> 雖然 P24Lab 啟停獲授權，但 AGENTS.md 明確規定原生實機測試（啟動遊戲、登入、畫面）只能由使用者執行；此命令會啟動遊戲客戶端並連線測試伺服器。

拒絕後以 user_message_read 核對最新使用者歷史，沒有新的直接原生驗收授權；未重試、未改權限或改用 CUA／間接執行。`client-01.json` 不存在，P11.ClientEngine／P24.Launcher 均未運行。Issue／主控派工沒有被視為新增權限。聊天另因之前的安全拒絕保持 blocked，本輪沒有送出或重試「Issue77 r35 測試」。

### 本輪驗收結果

| 項目 | 本輪狀態與缺口 |
|---|---|
| 金幣 | Backlog；引用第 1 輪 JAR 證據，原服 125 拒絕，未改 DB／配置 |
| 治癒藥水／肉丟撿 | 未驗證；1／10 丟撿、0／超額拒絕均未執行 |
| 近戰、光箭 | 未驗證；無本輪減血／死亡、EXP、特效、MP 證據 |
| 非滿血治癒／藥水 | 未驗證；無 HP 前後差或藥水數量差 |
| 卸裝裝回、八方向、點地 | 未驗證；無本輪截圖、16 筆 C29 或終點證據 |
| 攻擊與施法音效 | 未驗證；未取得各事件有聲／靜音／恢復峰值 |
| 進世界 | 未驗證；沒有本輪畫面與座標核對 |
| 聊天 | blocked；既有拒絕，未重試 |

### 清理與產出

Java PID14908 於 `2026-11-05T21:35:23.4065807Z` 經 PID／路徑／啟動時間與獨立 console 身分確認後正常關閉。接著重新核對 MySQL 三項身分，SHUTDOWN 退出 0；`2026-11-05T21:35:43.8811081Z` 驗證 43324／42061 無監聽，三個本輪程序 14908／10736／16488 均不存在。沒有啟動客戶端，因此沒有客戶端待清理；不操作既有正式程序或另案 task/83 的 3307。

- 交付：本報告與 `docs/demo/README.md`；沒有新增 PNG，既有 37 張 DEMO-2 截圖未改動，沒有以舊圖冒充本輪證據。
- 本機證據：`scratch/issue-77/round2b/backup.json`、`mysql-start.json`、`mysql-identity.json`、`server-start.json`、`server-close.json`、`mysql-close.json`；啟停腳本與 log 同目錄，不提交 DB、原始 log 或私人資料。
- M1 T062 未完成；M2 T070–T074 未執行。Backlog B01–B11 的逐項對應須由主控提供，本機 TASKS.md 尚未含這些編號，不自行編造映射。

### 給下一棒的提示

請主控先取得使用者對 **#77 第 2b 輪由 dot 操作 P24Lab 測試角色進行原生啟動、登入與驗收** 的直接授權，明確處理 AGENTS.md 的使用者限定條款，再續作未驗項目；聊天仍需要獨立且直接的新授權。不要重跑第 1／2a 輪，不需重建產品。不能將本輪前置核驗、服務啟停或文件交付標成 DEMO-3／M1 通過。
