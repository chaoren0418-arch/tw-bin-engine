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


## 第 2b 輪：新直接授權後續測（2026-11-06）

**結論：部分通過，DEMO-3／M1 T062 未完成。** 本節取代前節「未啟動／聊天 blocked」作為目前狀態；保留前節是為了記錄先前拒絕，不是本次又遭拒絕。新授權後已登入、操作並取證，沒有新的自動安全審查拒絕。

### 授權、去重與環境

- 已透過使用者訊息歷史核對 2026-11-06 02:09:01 UTC 本人對 P24Lab 原生啟動、登入、操作、截圖、音量與測試聊天的直接授權；未修改權限或專案規則。先前確定未執行的啟動在新證據下重試一次成功。
- 啟動前核對最新 Issue／RESULT、既有 task/77、scratch、對話與程序，沒有另一份新授權續測結果或執行中的 #77。收尾再查 28 則留言，最新派工為 [6675144919](https://github.com/chaoren0418-arch/tw-bin-engine/issues/77#issuecomment-6675144919)，無新授權後同輪 RESULT，故只交付一次。本輪不重做第 1／2a 輪。
- 起始 `task/77@50d09756722961827411d2cca5d7900a1cf75f3b`；`0b747de4e230c72906c830be821de322894acbf2` 祖先檢查退出 0，`--check-only` 為 `revision: dev`，退出 0。
- DLL SHA-256：`44eb5843f25833f2da1cdf7081eabeb7b8cd6e27562423e2d06b150ac787d477`；manifest：`2963b5224dbe9f3b95966778839e3f572767e8bd8a326632fdae0a2b751e9cc3`。收尾 DLL hash 仍相同；沒有 fetch、重建、改產品或 lock。
- 核實 D:\BinEngine junction 與既有搬遷紀錄；MySQL UUID `e3443e40-d090-11f1-8040-4cedfb679789`、port `43324`、datadir `D:\BinEngine\environments\p24\db-data\` 全符。冷備份 `environments/p24/backups/issue-77-resumed2b-before-demo/db-data` 的 893 檔逐一 SHA 相符。只對指定測試角色做必要唯讀查詢；未修改 DB／配置、讀取機密設定內容或輸出帳密。
- 本輪客戶端 PID20908（10:14:02 +08 啟動）、Java PID18784、mysqld 父／子 PID19284／19696。每次輸入均由既有 native-ui 核對 PID、路徑、標題及前景；未用 CUA。一次前景不符被工具 guard 擋住、未輸入，後續前景核對恢復才繼續，未繞過 guard。
- 啟動前已設 `TW_PACKET_TRACE`；本機 trace 為 `scratch/packet-trace/demo3-01.jsonl`。不提交完整 trace、帳密、DB、私有設定或遊戲資源。

### 實際結果

| 項目 | 結果與證據 | 尚缺條件／追蹤 |
|---|---|---|
| 進世界 | ⚠️ [新畫面](../demo/demo3-03-world.png)顯示地圖、測試角色及 HUD；初始 DB map0、32562/32956、HP686/686、MP117/117 | 未命名物件仍有占位；沒有玩家 UI 精確座標與 DB 的獨立核對，不能以物件座標冒充玩家座標。T062／T073 |
| 卸裝裝回 | ✅ 等權威更新後分別截[未裝備](../demo/demo3-09-unequipped.png)與[已裝備](../demo/demo3-09-equipped.png)，登出後匕首 is_equipped=1 | 此列通過 |
| 聊天 | ✅ 只送一次指定字串，取得[「服務驗證: Issue77 r35 測試」回顯](../demo/demo3-12-chat.png) | 此列通過；先前授權阻塞解除 |
| 金幣 | ❌ 本服規則不可丟 → **B14**；沿用本報告第 1 輪 JAR 證據，本次不重送 | 不改 DB／配置繞過 |
| 藥水丟 1／10 | ❌ [前41](../demo/demo3-10-potion-before.png)→[丟1被拒](../demo/demo3-10-potion-drop1.png)→[丟10被拒](../demo/demo3-10-potion-drop10.png)，均「你不能夠放棄此樣物品。」、背包41 | 兩次都有 C25，證實拒絕後可再次送出；無地面物品／拾取，不等於完整循環。T062（B14 替代驗收） |
| 數量 0／超額42 | ⚠️ [0](../demo/demo3-10-zero.png)、[42](../demo/demo3-10-over.png)未增加 C25，背包仍41 | 截圖仍是舊125訊息，沒有清楚的獨立數量錯誤文案；不宣稱訊息驗收通過。T062 |
| 肉替代 | ❌ 丟1後背包肉消失，[地上有肉](../demo/demo3-10-meat-ground.png)；切地面頁時已[原服移除、清單空](../demo/demo3-10-meat-ground-list.png)，未送拾取 | **肉由1→0，未撿回**；未吃肉、Food仍60，未補物品或改DB。消失原因 uncertain，不能斷言清理或怪物拾取。只有1塊，未做10循環。T062 |
| 八方向 | ⚠️ 八方向各2鍵，共16筆 C29，逐鍵各1筆；[新截圖](../demo/demo3-04-eight-directions.png) | 玩家精確終點沒有獨立證據，不能只憑16筆宣告全列通過。T062／T073 |
| 點地 | ⚠️ 點3–5格外，動作窗至下一步前5筆 C29；[停下畫面](../demo/demo3-05-click-arrived.png) | 線上 DB 座標尚未保存；之後選怪未命中又造成移動，最終登出座標不能獨立驗證此步。T062／T073 |
| 近戰 | ⚠️ 兩次重新選取附近移動牛並按G；[第一次結果](../demo/demo3-06-attack1.png) | 0筆 C229／C123，沒有有效近戰、減血、死亡；EXP未增。未持續盲打至30次。T062／T072 |
| 技能4光箭 | ⚠️ [技能清單](../demo/demo3-07-skills.png)可見；選技能後仍未建立有效敵方目標 | 未施放，沒有 MP 減少或效果證據。T062 |
| 非滿血治癒／藥水 | ⚠️ 未執行；角色始終HP686/686，藥水41 | 未取得弱怪傷害，不以滿血施放冒充回血驗收。T062 |
| 攻擊／施法各三階段音量 | ⚠️ 未執行，沒有本輪峰值 | 缺有效攻擊／光箭事件，不能用背景聲或舊輪量值代替。T062；B07 是既有語義來源限制，不免除本列 |

藥水 C25 時間為 02:24:28.442Z（1）、02:25:07.401Z（10）；肉 C25 為 02:27:32.787Z（1）。八方向輸入從 02:29:52.981Z 開始，依序上、下、左、右、Home、PgDn、PgUp、End 各兩次，16個動作窗各有1筆C29。trace 僅記錄時間／方向／opcode／長度／序號，不含解密 payload，不能由它倒推出精確座標、目標或傷害。

### 保存差異、執行限制與清理

登出後唯讀核對：位置由 map0 32562/32956 → map0 32570/32956；HP686、MP117、EXP412855581、Str22、BonusStatus4、Food60 均未變。藥水41、金幣33400，匕首已裝備；肉1→0。其他正常服務物品變動不作為本案驗收證據。沒有刷怪、加點、吃肉、改NPC、補DB或使用主要角色。

收尾發現主目錄已為 `task/102@a084ea262547e792cb3c9ccf0b28f61c9cd8410b`。reflog 顯示本輪切回 task/77 後，10:12:03 +08 又被切到 task/102，10:26:55 有另一任務提交。故不能宣稱整段測試工作目錄一直是 task/77。產品 DLL 前後相同，但未對全部 runtime 檔做持續監控；此限制必須保留。發現後不追加遊戲操作，正常收尾，未切回或重設他人主目錄。文件在新隔離 worktree `scratch/wt/task-77-resumed2b` 的既有 task/77 上提交，不重建遊戲、不另起重複驗收。

本輪客戶端先按登出，再 CloseMainWindow 回報 requested=true、exited=true；Java 核對擁有權與獨立 console 後正常退出（02:40:30.327Z）；MySQL 再核對三項身分後 SHUTDOWN 退出0。**02:40:45.367Z 確認 42061／43324 無 LISTENING**；後續查 PID20908／18784／19284／19696 全不存在。不碰正式2000／3306、其他遊戲、另案3307或 task/83。

本機證據留在 `scratch/issue-77/resumed2b/`：backup、mysql-identity、client/server-start、03-before／10-after／99-after-logout 指定角色快照、04-inputs／summary、05-inputs、06-attempt1/2、09-off/on-ui、10-*、12-chat-ui、server-close、mysql-close。這些不是公開附件；公開只含整理後報告與以下15張真實PNG。所有圖逐張檢視、未合成／補繪，無帳密或無關桌面；每張小於2 MB。較早37張DEMO-2圖未改。

### 給下一棒的提示

1. 主控先處理共享主目錄的 runner 互斥，續測前再次確認產品與工作目錄身分，避免再次切分支競爭；不要重跑第1／2a輪。
2. T062仍缺替代物品完整丟撿、數量錯誤文案、近戰＋EXP、光箭、非滿血回血、各事件三階段音量及精確座標。肉已不在背包；先判斷合法可用的既有測試物品，不擅自還原備份／補DB／改白名單。
3. M2對照採本機 `task/102@a084ea2:TASKS.md` 唯讀版本：T070 NPC、T071跨圖、T072戰鬥閉環、T073原生畫面／滑鼠／HUD／AOI、T074場景6016音訊；B01–B11保持原定義，金幣是新增既有B14。未把M1缺口硬塞進無關Backlog，亦未修改TASKS.md。
4. 本次沒有待處理的安全拒絕（denied）；未完成驗收為 pending，肉消失原因及獨立移動終點為 uncertain。不要再以聊天缺授權標 needs:user；也不能以本次部分通過關閉#77或宣稱M1完成。

### 本輪公開截圖校驗

| 檔案 | bytes | SHA-256 |
|---|---:|---|
| [demo3-03-world.png](../demo/demo3-03-world.png) | 697372 | `26dd54b70f341f9729cadca606ffb26848e9b1c3d64f04c0f2744f7479f2ad40` |
| [demo3-04-eight-directions.png](../demo/demo3-04-eight-directions.png) | 697827 | `5af312f8ea7e3be0651dff9e77bb182a542fdb46f2e05c525527a6bd3d19e5d9` |
| [demo3-05-click-arrived.png](../demo/demo3-05-click-arrived.png) | 699958 | `0897ac4a2beed23bce819dd0a2e36b5cf7e33bf473c94c1e0e9859393ee97a99` |
| [demo3-06-attack1.png](../demo/demo3-06-attack1.png) | 697199 | `eb96ea7ac14390c9275ef22498c6742402842d611d1492121d9e38bd89097337` |
| [demo3-07-skills.png](../demo/demo3-07-skills.png) | 433483 | `a97b647695d039d87cf3869b7bd3ce5d599dfa372ea3fb15bfab8c72d6ded670` |
| [demo3-09-unequipped.png](../demo/demo3-09-unequipped.png) | 441290 | `ee4d9a72528ddfdcf20422df9c02ff0f8ca86152cb917357dfdc4b58c20b80ad` |
| [demo3-09-equipped.png](../demo/demo3-09-equipped.png) | 442261 | `1cae315e31861dcc9bc4f5c985adde86a18792d7115d210d783009111422d392` |
| [demo3-10-potion-before.png](../demo/demo3-10-potion-before.png) | 464960 | `d98bdb8d96aa0527c595f92fabe58ab8282a1acfce1cac263e8d4ad27563eec5` |
| [demo3-10-potion-drop1.png](../demo/demo3-10-potion-drop1.png) | 464329 | `56d069e16870ba0d8ca0c9fa5b051008ff178a1dee6cc86ad022b8962b841624` |
| [demo3-10-potion-drop10.png](../demo/demo3-10-potion-drop10.png) | 464054 | `8e4ab10842ac479b71b2a4e21e18465372dab75aa64d8f63785976c31457e256` |
| [demo3-10-zero.png](../demo/demo3-10-zero.png) | 464265 | `e59e7fffcb9bdf35c0d5a9cc3038b15905edb16fec4de10dd90144cee3733345` |
| [demo3-10-over.png](../demo/demo3-10-over.png) | 464100 | `9c85c49d468e16a5881267c9f134cf137e45fd7af13dacdb27bb15a7adda74e3` |
| [demo3-10-meat-ground.png](../demo/demo3-10-meat-ground.png) | 451253 | `ec52ef8f05020f20663988c1b5c824a8f45d52fa7cccd464255e1bdee3c90b6d` |
| [demo3-10-meat-ground-list.png](../demo/demo3-10-meat-ground-list.png) | 443733 | `0eb5353cafce43d48d1e1a7001dcfa0da9944f0181028d336a3790f4a9e840bc` |
| [demo3-12-chat.png](../demo/demo3-12-chat.png) | 439994 | `67ea1d6b7144c74857f9d0b89d684d94bb4e904058d8e2a99e29c3f76e52ef39` |

## 第 2c-a 輪：主專案同步與 dev 建置（2026-11-06）

**已驗證：本輪完成，可交主控重排 dot 第 2c-b 輪；M1 真機驗收仍未完成。** 在主專案 `D:\lineage開發\TW1608262200研究` 同步含 T063／T035 的 main，完整建置 dev、寫入 receipt，入口只執行 `--check-only`。本輪只新增本節；前輪 demo 頁、截圖及報告原樣保留。

### 來源與分支交接

- 初始分支 `task/102`；`git status --porcelain --untracked-files=no` 退出 0、無輸出。
- `git fetch origin` → `git checkout main` → `git merge --ff-only origin/main` 均退出 0。建置 HEAD：`4b8ee9f86285c4797c8638287051b02c2236483d`。
- HEAD 包含 PR #118：`f54113430bd8f10b11d421463044838a8744f6c7`；PR #116：`08f32708bc58146f32a47592ce13a67fd6e53c11`。合併後以 `git merge-base --is-ancestor <commit> HEAD` 核對，兩項均退出 0。
- 依本輪詳細要求保留 `task/77@2a76acc870d0c5f066faeed207e205c8639a4c31`，沒有使用通用前言的 `checkout -B` 重設前輪歷史。
- 該分支原掛在 `scratch/wt/task-77-resumed2b`；確認追蹤檔乾淨後，執行 `git -C scratch/wt/task-77-resumed2b checkout --detach HEAD`（0），留在原 commit，未刪除 worktree。
- 首次 `git checkout task/77` 退出 1：主目錄有 15 張同名未追蹤 `demo3-*.png`。逐張比對 `git hash-object` 與 `task/77:<path>`，全部相同；將原檔移存 `scratch/issue-77/preexisting-demo/`，SHA-256／blob 清單保存在該目錄 `evidence.json`。沒有刪檔、reset 或 stash。
- 再次 `git checkout task/77`、`git merge --no-edit origin/main` 均退出 0，無衝突；merge commit：`ac06bfdcfa350088c83e26b88f02a2a572d8416c`。為避免分支切換覆蓋報告，在 merge 完成後才追加本節。
- `git diff --exit-code origin/main -- engine docs/REPORT.md` 退出 0。分支相對 main 只保留 #77 的報告、demo 頁與前輪截圖，產品與 lock 完全相同；未自行修改 `docs/REPORT.md`。

### 指令、退出碼與產物

一般命令執行環境最初初始化失敗，改用已授權的本機執行環境。首次呼叫 `python` 時 PATH 找不到命令，Python 尚未執行，不能把該次 shell 的 0 當成建置成功。以 `py -0p`（0）找到既有 Python 3.12，僅調整本次程序 PATH；沒有安裝工具或更改全域設定。

```powershell
$ErrorActionPreference = 'Stop'
$env:PATH = 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312;' + $env:PATH
$env:NUGET_PACKAGES = Join-Path (Get-Location) 'engine/.nuget'
python -B -X utf8 engine/_integration/dev/rebuild.py --build --write-receipt --output scratch/issue-77/rebuild-2c
& '.\engine\啟動新版客戶端.cmd' --check-only
# 分支 merge 後再核對來源、manifest 與 receipt：
python -B -X utf8 engine/_integration/dev/rebuild.py --verify --output scratch/issue-77/rebuild-2c/verify-after-merge
```

| 檢查 | 退出碼／結果 |
|---|---|
| dev 建置命令 | 0；169 個來源／178 個 pins，writes=false，PASS dev |
| sdk／product-restore／product-build／launcher-restore／launcher-build | 各 0；SDK 10.0.300 |
| 主程式建置 | 2 警告、0 錯誤；`MagicUi.cs(20,62)` 的 CS8600、CS8602；本輪未修產品程式 |
| launcher 建置 | 0 警告、0 錯誤 |
| engine 入口 --check-only | 0；revision=dev，uiStarted/networkStarted/servicesStarted=false |
| merge 後 --verify | 0；PASS dev，DLL 身分不變 |
| `Test-Path -LiteralPath engine/assets/inventory -PathType Container` | True；T035 的本機圖示目錄存在，尚不代表實際圖示顯示已驗收 |

產物身分（SHA-256）：

| 路徑 | SHA-256 |
|---|---|
| `engine/build/client-product-dev/P11.ClientEngine.dll` | `37cd8201f6ad631f70915a8314d19d0ec4ca0cb28dbd524169937d5d23930174` |
| `engine/build/client-product-dev/P24.Audio.dll` | `c64a38a8165f5651a331c19cf580e64d0e59541d060aeebb69c71941bb04b52d` |
| `engine/build/product-dev-manifest.json` | `44164613584da833a0e53e721ce3a1957315701b94616a6d43067afc46d78a3a` |
| `engine/launcher/P24.Launcher.dll` | `222e8a0d03c657a980c2d1c0ab24e46c2026e41b1ea17866b73029cf7452e63d` |

以上與 #115 第 3 次嘗試記錄的產品 DLL／manifest 相同。Receipt 寫入 `engine/_integration/dev/receipt.json`，builtAt 為 `2026-11-06T03:54:41.818026+00:00`。主程式留既定 `engine/build/client-product-dev/`，launcher 留既定 `engine/launcher/`，中間產物留各專案 `obj/`／`bin/`。命令紀錄、stdout／stderr、result.json、退出碼及環境暫存留 `scratch/issue-77/rebuild-2c/`，未搬移遊戲資源。

入口完整 JSON（原始輸出保存在 `scratch/issue-77/rebuild-2c/check-only.json`）：

```json
{
  "status": "checked-only",
  "revision": "dev",
  "executable": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\engine\\build\\client-product-dev\\P11.ClientEngine.exe",
  "workingDirectory": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\engine",
  "arguments": [
    "--normal-product"
  ],
  "manifest": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\engine\\build\\product-dev-manifest.json",
  "manifestSha256": "44164613584da833a0e53e721ce3a1957315701b94616a6d43067afc46d78a3a",
  "hashedRuntimeAndWebFiles": 24,
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

建置結果 JSON：

```json
{
  "status": "pass-dev-build",
  "sourceFiles": 169,
  "GUI": false,
  "network": false,
  "database": false,
  "productDllSha256": "37cd8201f6ad631f70915a8314d19d0ec4ca0cb28dbd524169937d5d23930174",
  "audioDllSha256": "c64a38a8165f5651a331c19cf580e64d0e59541d060aeebb69c71941bb04b52d",
  "productManifestSha256": "44164613584da833a0e53e721ce3a1957315701b94616a6d43067afc46d78a3a",
  "launcherDllSha256": "222e8a0d03c657a980c2d1c0ab24e46c2026e41b1ea17866b73029cf7452e63d"
}
```

### 未驗證與給下一棒的提示

- 本輪沒有執行原生遊戲、P24Lab、MySQL、伺服器或任何 DB 操作；未連正式服務，未操作機密設定、封存、素材 junction 或原 BIN。沒有新增真機截圖、音量或遊戲功能通過結論。
- 兩個 nullable 警告是本次實際編譯輸出，需如實保留；建置成功與 check-only 不等於 M1 完成。本輪不重跑 #115 已交付的離線功能斷言。
- **下一棒為主控／調度器：**審查本報告後，將 #77 runner 改為 dot，重排第 2c-b 輪；繼續維持主目錄 checkout／commit／建置互斥。主目錄交付停在 `task/77`，使用本節的建置 HEAD 與 DLL SHA 辨識產物。
- **dot：**只補尚未通過列，依 [RUN.md](../demo/RUN.md) 及 [#115「給 dot 第2c輪的操作步驟」](issue-115.md)。普通肉 40056 須向 NPC 合法購足至少 11 個；買不到就記取得受阻，不補 DB、不改白名單、不吃肉。拾取數量錯誤改正後直接送出，不重新選取；舊錯誤文案殘留時依 trace 判定是否送出。
- 補齊近戰 EXP、光箭 MP／效果、非滿血治癒與藥水、數量錯誤、1／10 丟撿循環、原服權威座標、攻擊與施法音量、T035 圖示／地面／音訊。demo 頁的 HEAD／DLL SHA 及成功標記留待 dot 依真機證據更新；原有第 2b 輪證據不能當成此 DLL 的真機結果。
- 本節為可追蹤交付報告；scratch 是本機原始紀錄，必要的 JSON、退出碼與 SHA 已內嵌本節，交付結論不依賴 scratch 永久保留。


## 第 2c-b 輪：跨專案排他未確認，blocked（2026-11-06）

**結論：blocked／待主控裁定，不是M1完成，也不是產品驗收失敗。** 本輪只完成安全前置與文件交付，沒有啟動P24Lab／客戶端、登入、操作、量音效或新增截圖。直接使用者授權已核實且適用，不缺原生／聊天授權；未重做第2c-a或第2b已通過列。

### 派工、去重與讀取範圍

- 對應正式派工 [6676517566](https://github.com/chaoren0418-arch/tw-bin-engine/issues/77#issuecomment-6676517566)；查核#77為open／runner:dot／task:running，42則留言無本輪實測RESULT。04:06的舊審查RESULT針對第2c-a，不是第2c-b產出；04:07主控已清除review:fail。
- 沿用既有驗收會話 `01a1ad9e-a818-7549-8ee2-00fb5aa5e038`，它是清單中唯一active的本機#77；#83、launcher與modern既有會話idle，沒有新建重複會話。本機無demo3c截圖／trace。
- 已讀適用AGENTS.md、CLAUDE.md、docs/demo/RUN.md、docs/reports/issue-115.md及本任務最新派工；**本輪未讀改docs/REPORT.md**。專案與父層未找到相關.agents/skills。
- 主目錄一開始即 `task/77@9a685a04fa40065ce8c16ee474c76e270faa3d66`，追蹤檔乾淨；lito-launcher-server與lito-modern主目錄均main。未fetch、checkout、reset、stash、重建或改產品／lock。

### 候選前置（已驗證）

| 檢查 | 結果 |
|---|---|
| `git merge-base --is-ancestor 9a685a0 HEAD` | exit0 |
| `engine/啟動新版客戶端.cmd --check-only` | exit0；status=checked-only、revision=dev、uiStarted/networkStarted/servicesStarted=false |
| 產品DLL SHA-256 | `37cd8201f6ad631f70915a8314d19d0ec4ca0cb28dbd524169937d5d23930174`，符合第2c-a |
| manifest SHA-256（check-only） | `44164613584da833a0e53e721ce3a1957315701b94616a6d43067afc46d78a3a` |
| 入口核對 | 24個runtime/web檔、13,957個素材存在／大小；不是素材實機顯示證明 |
| T035前置 | engine/assets/inventory存在；未複製、生成或改動素材 |
| Lab靜態身分 | D:/BinEngine是本專案junction；auto.cnf UUID=e3443e40-d090-11f1-8040-4cedfb679789；mysql.ini僅篩選port43324、datadir=D:/BinEngine/environments/p24/db-data、bind=127.0.0.1及basedir |

沒有啟動DB，所以本輪沒有live SELECT身分確認；靜態相符不冒充已建立連線的實例驗證。沒有讀config/server.properties、private設定或#83秘密內容。

### 跨專案排他：具體缺口

04:18–04:20 UTC查核三repo GitHub與本機調度面板：tw-bin只有#77為dot/running；launcher #225仍dot/queued，#138是code/queued；modern無dot/running，另有code佇列。既有#83會話已完成文件修正、idle，本輪未接手或重跑setup。

1. `D:/dev-orchestrator/orchestrator/main.py:492` 已有同repo、workdir:project、其他running任務即等待的判斷。因此不再沿用第2b「主目錄互斥尚未上線」說法；但該判斷明確限制同repo，不能排除另一repo啟停共用lab。
2. `runners.py:177` 的autonomous human-started分支只貼派工／狀態，不取得lab資源租約。`router.py` 的max_parallel依running_count；本機state.sqlite以mode=ro查到running空、沒有lease／lock／p24／owner相關kv鍵；`http://127.0.0.1:8787/api/state`亦為running=[]，tasks仍顯示#77 running、#225 queued。故不能把runner槽位當本輪跨專案鎖。
3. 調度器原始碼與既有文件未找到P24Lab跨repo租約／持有機制；P24Lab根目錄亦未找到lock／lease／owner紀錄。本輪沒有自行新增不被其他runner遵守的假鎖、改調度器配置或搶占程序。
4. #225本文要求tw-bin正在使用時暫停，但queued狀態、會話idle、目前埠閒置只是當時觀測，沒有本輪明確持有者及阻止並行派工的可核實紀錄。既有巡查會話亦回報同一缺口。

**未執行的動作／目標：啟動P24Lab MySQL43324、server42061與P24客戶端。理由：本輪明確要求先核定跨專案排他，現有證據不足。狀態pending；沒有工具denied或執行結果uncertain，也沒有setup refresh錯誤。** 已完成去重、候選、靜態身分、程序／埠與調度機制查核；不反覆要求使用者重授已涵蓋的權限。

### 本輪每列結果

| 列 | 狀態 | 尚缺證據／對照 |
|---|---|---|
| 近戰 | 未驗證、未執行 | C229、傷害／死亡＋EXP；T062／T072 |
| 光箭 | 未驗證、未執行 | 有效敵方清單、C6、效果＋MP；T062 |
| 非滿血治癒／藥水 | 未驗證、未執行 | HP差與藥水−1；T062 |
| 數量0／超額 | 未驗證、未執行 | 本機明確文案、C25不增、舊125消失；T062 |
| 普通肉購買及1／10丟撿 | 未驗證、未執行 | NPC實際供應與既有遊戲幣購足11、數量循環；不宣稱商店有貨／缺貨，不改DB；T062／B14替代 |
| 拾取錯誤後更正 | 未驗證、未執行 | 0／超額無C112，同一選取改1後單筆C112與權威更新；T062 |
| 進世界、八方向、點地權威座標 | 未驗證、未執行 | 各段獨立S87／S10權威值；T062／T073 |
| 攻擊／光箭事件音效 | 未驗證、未執行 | 各三階段六組峰值；本輪無數值；T062 |
| T035圖示／地面／音訊 | 前置目錄存在；實機未驗證 | 無新圖或聲音證據；T035／B15 |
| 第2b卸裝裝回／聊天 | 歷史通過、本輪不重做 | 不作本候選DLL新證據 |

各未執行列共同前置對應B11調度協調缺口，不以B11豁免驗收。README已直接依目前TASKS.md列出B01–B15：B01技能擴充、B02慢速丟撿、B03–B04地圖效果、B05交易前提、B06地面圖pin、B07原生音效語義、B08封裝、B09 P27、B10歷史來源、B11調度器、B12舊EXE基準、B13其他伺服器協議、B14金幣規則、B15可攜assets與9個例外。沒有編造新Backlog或修改TASKS。

### 服務狀態、產出與下一棒

04:20:06 UTC查核42061／43324無LISTENING；沒有P11.ClientEngine、P24.Launcher或Java，本輪未啟動任何程序，故沒有本輪待清理client/server/MySQL。觀察到既有mysqld PID4188，不屬本輪，未連線、未停止或變更。正式2000／3306、另案3307、#83資料與秘密均未操作。

交付只更新docs/demo/README.md與本報告；沒有新截圖。保留六張未追蹤第2b圖及native-baseline副本原狀，不stage、不刪。README已更新候選HEAD／DLL、增加T035待驗列、B01–B15對照，刪除舊task/102編號來源與「主目錄互斥尚待建立」描述，清楚區分新候選與歷史圖。

**下一棒：專案主控／調度器（needs:orchestrator），先提供跨repo P24Lab排他持有紀錄／有效租約與釋放條件，並核定#225等待本輪釋放；再讓同一#77會話續作。** 不是needs:user授權缺口，不需重做第2c-a、#83或已通過第2b列；不要以舊第2c-a審查退回反覆重測。交付前重新核對標籤，要求本輪全新審查；不合併、不關Issue。


## 第 2c-b 輪：租約核實後續測（2026-11-06）

**本次實測及清理完成，M1 T062未完成。** 本節對應重新派工 [6678372228](https://github.com/chaoren0418-arch/tw-bin-engine/issues/77#issuecomment-6678372228)，取代上一節blocked作為目前狀態，但保留歷史事實。沿用同一本機會話與task/77；去重確認沒有本次RESULT或其他驗收接手，未重做第1輪、第2c-a及第2b卸裝／聊天。所有時間均UTC。

### 前置、權限與產品身分

- 已讀適用AGENTS.md、CLAUDE.md、RUN.md與issue-115；沒有讀改docs/REPORT.md，未找到相關.agents/skills。直接原生授權及公開推送授權已涵蓋本輪，沒有透過Issue留言擴權。
- 有效會話限制為workspace-write／auto_review；無法宣稱never／danger-full-access。需要的own-PID原生操作與正常關閉經現有工具安全審查允許，未改安全設定、未使用CUA。
- 先前全系統Win32_Process查詢因Windows 0x80041003遭拒，本輪沒有重試或換API繞過。既有非監聽程序狀態維持unknown；採唯一lease、空埠、前輪正常清理紀錄與新啟動衝突／身分核對。前輪清理時間分開：第一次第2b為11月5日21:35，直接授權後第2b為11月6日02:40，本次為07:18–07:19。
- 啟動前API與SQLite mode=ro核實唯一lease:p24lab：holder=`chaoren0418-arch/tw-bin-engine#77`，06:09:11.234735取得、07:39:11.234735到期；#225等待。三repo與既有會話查核無其他本輪驗收，主目錄無其他checkout／commit／建置，追蹤檔乾淨。操作前持續核lease與HEAD，沒有自行續租。
- 實測HEAD `b416e8e0677738ad098d3c684ba8eed3aa1ff8d4`；`git merge-base --is-ancestor 9a685a0 HEAD` exit0。保留先前blocked報告，不reset／stash／fetch／切別支。未改產品／lock、未同步或重建。
- 入口check-only exit0、revision=dev，uiStarted/networkStarted/servicesStarted=false，24個runtime/web檔、13,957個素材存在／大小核對；inventory目錄存在。
- DLL SHA-256 `37cd8201f6ad631f70915a8314d19d0ec4ca0cb28dbd524169937d5d23930174`；清理後相同。manifest SHA-256 `44164613584da833a0e53e721ce3a1957315701b94616a6d43067afc46d78a3a`，均符合第2c-a。
- D:/BinEngine為專案junction。啟動前空埠；MySQL啟動後06:23:02 live核對UUID `e3443e40-d090-11f1-8040-4cedfb679789`、port43324、datadir `D:/BinEngine/environments/p24/db-data/`一致；Java成功綁42061。未讀秘密config/server.properties或#83設定。
- DB啟動前冷備份1407檔逐SHA相符，保存在lab自己的 `environments/p24/backups/issue-77-resumed2c-before-demo`，備份紀錄06:22:52。沒有資料修改SQL、配置或白名單變更；僅身分確認與正常SHUTDOWN，遊戲正常行為由原服存檔。
- 本輪只啟動一次客戶端，預設trace `scratch/packet-trace/demo3c-01.jsonl`；MySQL parent20308／listener20328、Java14300、client24768。每次輸入核對own PID、路徑、標題／前景及租約；僅測試角色。未碰正式2000／3306、另案3307、原BIN、正式client、封存或遊戲資源。

### 每列結論與證據

| 驗收列 | 結論 | 實際觀測／限制 |
|---|---|---|
| 初始進世界 | 通過此子項 | [世界與HUD](../demo/demo3c-30-world-focus.png)，原服map0 x32570 y32956 S87#1；HP686/686、MP117/117、Lv59。未命名物件／部分外觀仍占位，不稱整個場景完成 |
| 數量0／超額 | 通過此子項 | 紅藥持有41；原生[0](../demo/demo3c-13-zero-client.png)／[42](../demo/demo3c-14-over-client.png)及F9 [0](../demo/demo3c-59-web-zero.png)／[42](../demo/demo3c-60-web-over.png)各按丟棄一次，顯示「未送出：數量不合法，請輸入1到持有量（最多2147483647）的整數」。全輪C25=0、C112=0，藥水41不變。未先製造125，因此舊125清除路徑未驗證 |
| 光箭 | 部分已驗證，整列未通過 | 技能4、[有效牛目標](../demo/demo3c-20-arrow-selected.png) #2000036217，3筆C6均有S30、MP117→115。傷害欄13／15／15；[首次](../demo/demo3c-21-arrow-first.png)、[靜音](../demo/demo3c-24-arrow-muted-1.png)、[恢復](../demo/demo3c-27-arrow-unmuted-2.png)可見施法／回報。未清楚辨識飛行光箭，不能冒稱效果完整；本地模板MP3，實際扣2依原服回包 |
| 近戰／EXP | 驗收失敗，根因未確定 | 共3次G，2筆C229；首次[傷害欄0](../demo/demo3c-61-web-target.png)、target HP未提供。EXP [前](../demo/demo3c-41-exp-before.png)／[後](../demo/demo3c-76-exp-after.png)均412855581，無死亡／EXP增加證據 |
| 非滿血治癒／喝水 | 未執行／未驗證 | HP全程686，未建立弱怪自然受傷前提；未施放治癒、未喝藥，紅藥仍41。不能推論功能失效 |
| 普通肉取得、丟1／撿1、丟10／撿10 | 未執行／未驗證 | 未建立一般NPC可購普通肉40056前提；未取得肉、未丟撿、未吃肉。金幣33400未購買，未改DB／NPC／白名單 |
| 地面拾取錯誤改正後直接送出 | 未執行／未驗證 | 缺合法地面肉；沒有C112，不以背包防呆通過代替拾取循環 |
| 八方向與點地權威終點 | 未驗證 | 各停點仍為S87#1初始值，提示移動後尚無位置回包；下表分列C29與終點，不以預測或其他物件封包代替 |
| 事件音效 | 六組驗收未通過 | 首次近戰事件後峰值0.45367342；其他組有零、缺session或缺事件，詳下表；null不是0 |
| T035背包圖示 | 原生通過；F9失敗 | 原生數量圖有紅藥、匕首、盾、袋、寶石等實際圖示；F9數量圖為broken image及「原道具」替代字，文字／數量正常 |
| T035地面物品圖／音訊 | 未驗證／音訊未通過 | 無合法肉地面圖；音訊不能以assets目錄存在或背景聲替代 |

近戰操作限制：首次單擊移動牛身體實際落在地面，沒有有效選取，所以當時沒有按G。後來改由可見敵方清單選牛再F10／G，**未完整符合派工要求的「走近、單擊身體、已選取提示」流程**；不能據此判定身體點選已通過。第二次先兩步Left接近、停下再G，有C229／S30但沒有獨立HP或EXP變化證據；[畫面](../demo/demo3c-73-attack-muted.png)僅預測／待回報，不能擅稱第二次傷害也是0。第三次[目標失效、本機未送出](../demo/demo3c-75-attack-unmuted.png)，沒有第三筆C229。未無限重試、未達30次上限，更未刷怪。

普通肉前提限制：[NPC頁](../demo/demo3c-70-npc-stable.png)只完成可見目標觀測，包含動物與私商；沒有取得一般NPC商品清單，也沒有交易／購買請求。不能寫「所有商店沒有賣肉」或以私商面板冒充一般NPC供貨查證。此列是本次測試前提尚未建立，交主控安排可重現合法供貨路徑；B05不豁免B14替代驗收。在線禮包29→30→31為原服計時更新，沒有主動開啟，不能當EXP或戰利品。

### 封包時間與權威座標

Trace無payload，opcode／長度僅搭配UI判讀，不單憑opcode推論目標減血或自身座標。SHA-256：`98bbf2e95ccccbe0a7a91ff562cf0459c3e9dd1c1018b5fd54f9620d56ac9f94`。out計數：C119=1、C137=1、C95=164、C6=3、C29=17、C229=2；C25=0、C112=0。

| 事件 | C請求UTC | S30回報UTC／seq／len |
|---|---|---|
| 光箭首次 | 06:47:04.386 | 06:47:04.650／2400／40 |
| 光箭靜音 | 06:50:07.259 | 06:50:07.314／3074／40 |
| 光箭恢復 | 06:57:30.880 | 06:57:30.909／4705／40 |
| 近戰首次 | 06:58:36.180 | 06:58:36.264／4941／24 |
| 近戰靜音 | 07:15:15.830 | 07:15:15.845／8576／24 |

每方向兩次單步輸入，以下停止畫面皆仍為原服map0／32570／32956、S87#1及「移動後尚無位置回包」，**不代表與預期終點相符**。

| 方向 | 停止證據UTC | C29筆數 | 原始截圖 |
|---|---|---|---|
| Home | 06:52:15.978 | 2 | [Home](../demo/demo3c-31-home2.png) |
| Up | 06:52:47.415 | 0 | [Up](../demo/demo3c-32-up2.png) |
| PageUp | 06:52:51.160 | 2 | [PageUp](../demo/demo3c-32-pageup2.png) |
| Right | 06:52:56.449 | 2 | [Right](../demo/demo3c-32-right2.png) |
| PageDown | 06:53:00.511 | 2 | [PageDown](../demo/demo3c-32-pagedown2.png) |
| Down | 06:53:05.045 | 2 | [Down](../demo/demo3c-32-down2.png) |
| End | 06:53:07.943 | 0 | [End](../demo/demo3c-32-end2.png) |
| Left | 06:53:10.996 | 0 | [Left](../demo/demo3c-32-left2.png) |

八方向共10筆C29，Up／End／Left各0；是否碰撞或其他狀態限制沒有唯一根因證據。有限點地4筆C29，[停下圖](../demo/demo3c-34-point-ground.png)仍無新權威終點。其餘1筆為點牛落空、2筆為近戰前接近，總計17。沒有各段重登取獨立S87，不能用單一末端位置回填前面各段。[F9權威／預測分列](../demo/demo3c-63-web-npc-area.png)可見原服32570／32956與預測32571／32958不同；此圖檔名沿用拍攝當時名稱，內容是座標證據。其他物件的S10／S87不作自身ACK。

### 六組事件音訊（僅本輪PID24768）

使用既有default-render endpoint程序session peak量測，每100ms取樣、每組300筆（實際約32秒）。匹配session缺失時記null，不能轉成靜音0；不查其他程序音訊，不宣稱覆蓋所有裝置／音軌。有聲／靜音／恢復控制圖：[光箭有聲](../demo/demo3c-22-sound-before.png)、[靜音](../demo/demo3c-23-muted.png)、[恢復](../demo/demo3c-25-unmuted.png)；[近戰靜音](../demo/demo3c-71-attack-muted-setting.png)、[恢復](../demo/demo3c-74-attack-unmuted-setting.png)。

| 事件／階段 | 量測起訖UTC | session匹配／300 | 整窗peak | C事件後2秒匹配／樣本；peak |
|---|---|---|---|---|
| 光箭／有聲 | 06:46:56.749–06:47:29.511 | 218/300 | 0.0 | 7/19；0.0 |
| 光箭／靜音 | 06:49:58.528–06:50:31.078 | 66/300 | 0.0 | 0/18；null |
| 光箭／恢復 | 06:57:21.431–06:57:53.999 | 0/300 | null | 0/19；null |
| 近戰／有聲 | 06:58:27.863–06:59:00.548 | 218/300 | 0.45367342233657837 | 13/18；0.45367342 |
| 近戰／靜音 | 07:15:03.295–07:15:35.835 | 0/300 | null | 0/18；null |
| 近戰／恢復 | 07:16:21.127–07:16:53.680 | 0/300 | null | 沒有C229；事件前提不成立 |

光箭靜音整窗雖是0，事件後2秒卻完全無匹配session，不能算有效靜音事件證據。近戰恢復階段目標失效，既沒有C229也沒有session，更不能宣稱恢復無聲是產品故障。首次近戰事件窗有正峰值，但單組不滿足攻擊／施法各三階段驗收，且不是獨立音軌語義鑑定。

### 清理、工具限制與交付

客戶端先登出、正常關窗，`client-close.json`為requested=true／exited=true。Java14300於07:18:10.529正常退出（IdentityVerified／GracefulSignal／Exited=true）；MySQL身分核對後07:18:54.792正常SHUTDOWN，ShutdownExit=0。07:19:18.463再次核對如下，早於07:29停止補測界線及07:39:11租約到期；未續租、未停別人的程序，之後僅做文件交付。

```json
{"OwnedPids":[24768,14300,20308,20328],"OwnedProcessesRemaining":0,"LabListeners":0,"Utc":"2026-11-06T07:19:18.4633590Z","NonListeningPreexistingProcesses":"unknown; denied query not retried"}
```

本機原始紀錄在 `scratch/issue-77/resumed2c/`：preflight、backup、mysql-identity、各項UI JSON、六組samples／summary、analysis、client/server/mysql-close與cleanup；trace在 `scratch/packet-trace/demo3c-01.jsonl`。關鍵數值、UTC、SHA與清理結果已內嵌本報告，不依賴scratch永久保留。有效權限沒有改變，沒有新的auto-review拒絕或setup refresh失敗。

觀測限制：一般shell初期看不到own-window title，經現有原生工具才取得；不把這段觀測盲區說成產品啟動耗時。WebView UIA兩次ElementNotAvailableException後用可見原生截圖／捲動繼續；一次前景guard發現ChatGPT而拒送輸入，重新核對own window後才聚焦續作。截圖helper曾有DrawingCommon編譯錯誤，僅修scratch helper；沒有產品改動或繞過安全拒絕。

公開交付31張已逐張檢視的真實客戶區PNG，全部≤2,000,000 bytes；無帳密、私人聊天、無關桌面或合成畫面。測試場景公開店名不另行抄錄。最初含窗框／桌面或大於限制的試拍、未採用連拍留本機不stage；六張第2b舊未追蹤圖與native-baseline副本完全不stage。提交範圍僅README、本報告及下列新圖，不提交trace／工具／遊戲資源。

| 新圖 | bytes | SHA-256 |
|---|---:|---|
| [demo3c-13-zero-client.png](../demo/demo3c-13-zero-client.png) | 470597 | `c208fdc8dca5f9b3cc6738fe5d42785248abd629f438bbb5437eb29a93a2f777` |
| [demo3c-14-over-client.png](../demo/demo3c-14-over-client.png) | 467050 | `1ba3be952a88cdca89e5c08547e010997953ff8e9cb36189e27a0d1fb0999431` |
| [demo3c-20-arrow-selected.png](../demo/demo3c-20-arrow-selected.png) | 457388 | `f2698463246442583a8fbdf5b69c14c486f6dace75e82eb53c27adea451f1ba4` |
| [demo3c-21-arrow-first.png](../demo/demo3c-21-arrow-first.png) | 458474 | `667eb5bdcb2a9a94b534b3595521cc4d8f227ec4f01d7464b26655c4f59809f9` |
| [demo3c-22-sound-before.png](../demo/demo3c-22-sound-before.png) | 449109 | `948e19188454353f0fee23f6e8553abd12a986df036ca79865b3f822c6e9d67a` |
| [demo3c-23-muted.png](../demo/demo3c-23-muted.png) | 449227 | `ccc23713ec400fc94081f296797e1b2d8e26b71017f4c827a530ade51269d945` |
| [demo3c-24-arrow-muted-1.png](../demo/demo3c-24-arrow-muted-1.png) | 460083 | `72e7f7ef1e615e06a43799044111fcd5f0d47587ad31a3089cd92b4a183a9253` |
| [demo3c-25-unmuted.png](../demo/demo3c-25-unmuted.png) | 449209 | `a6beec9d847db7fb150c436a6f72b338ae5ddc921610a5142a9c52ad607d401b` |
| [demo3c-27-arrow-unmuted-2.png](../demo/demo3c-27-arrow-unmuted-2.png) | 470520 | `87181634750e42f42f689367e3a1644084fdf793901cf5013dffb0daec0f74ff` |
| [demo3c-30-world-focus.png](../demo/demo3c-30-world-focus.png) | 714476 | `838b7c007148323e42a4078c8a89a21f2749ca8bf3749e1b4af8ce6c8dc482a9` |
| [demo3c-31-home2.png](../demo/demo3c-31-home2.png) | 715475 | `4b46a239073013a290aca8af4537083358eaee51b197996bb85d9e47d549e484` |
| [demo3c-32-up2.png](../demo/demo3c-32-up2.png) | 716489 | `e6d079d7de1ff6e910176c4a0781b0756a177f8353ef95bb59fe80aa8421c7c1` |
| [demo3c-32-pageup2.png](../demo/demo3c-32-pageup2.png) | 712577 | `305971b35aefbbd936d5677bc3bd220ac1d3cf2bded255cc2c5cf0e7004a355d` |
| [demo3c-32-right2.png](../demo/demo3c-32-right2.png) | 712147 | `5f34aec48857a6c96a4ab4c2db8ff122e145b3708bd508847fdabda25b4ad330` |
| [demo3c-32-pagedown2.png](../demo/demo3c-32-pagedown2.png) | 713239 | `240a856aa77de8ed5d3b19eeff76ee4459c1000efaa90589c4c23ac033c5ff4e` |
| [demo3c-32-down2.png](../demo/demo3c-32-down2.png) | 717324 | `441ccabde1f5b6f459f3c93484c92b5a1c739cafdc8c1e925878cb62529b927b` |
| [demo3c-32-end2.png](../demo/demo3c-32-end2.png) | 717770 | `7527c6cbfa8bc428249bd2ef1c452be59d784e0d55e29e357ab1f1ff95dbca70` |
| [demo3c-32-left2.png](../demo/demo3c-32-left2.png) | 716809 | `ac6d1241de0faa946fcecf8d9ab410f2bae4c92db7a8ac63bb2b5a7b4b14fb00` |
| [demo3c-34-point-ground.png](../demo/demo3c-34-point-ground.png) | 704677 | `f4234c1e970bfb987446e546bdb256f1a5c969aae9768d84762028cd180214af` |
| [demo3c-41-exp-before.png](../demo/demo3c-41-exp-before.png) | 462064 | `5ad2a7d7f6468b98f1e3dac1b84f6eafd60a698fe4273cac8c2c7da5d73312e9` |
| [demo3c-42-attack-first.png](../demo/demo3c-42-attack-first.png) | 712821 | `29283c22cc35f727f2f606c3fbfbac66359625b4eab72c0540320574753e3355` |
| [demo3c-59-web-zero.png](../demo/demo3c-59-web-zero.png) | 552828 | `2fe6ce99fd934a9c2ae88e5b29a4820cb3d32889611798677bd6085f238e10cf` |
| [demo3c-60-web-over.png](../demo/demo3c-60-web-over.png) | 553507 | `f6ca86232d89f9d8292c9f0092f7e7a8a75c5e8ed02f70a4ba833a1dc962a311` |
| [demo3c-61-web-target.png](../demo/demo3c-61-web-target.png) | 559019 | `07f887d7f2f3154382fa51a7a2f9e5f7941bf5f005c156aee974c0da8f9645fd` |
| [demo3c-63-web-npc-area.png](../demo/demo3c-63-web-npc-area.png) | 560397 | `48b2aa77e1c6f7c5d6d8e7360b66ca1d7c005e56d93b85cd86ec794cac54db69` |
| [demo3c-70-npc-stable.png](../demo/demo3c-70-npc-stable.png) | 292203 | `6497415887617400ed034c39b71a4da846f6beb1d98a58d0e99ae5ff6db5fc6b` |
| [demo3c-71-attack-muted-setting.png](../demo/demo3c-71-attack-muted-setting.png) | 460646 | `f7065f5363f02b982695f9224b7c2a88fcd8c619fcbe503f6f2b220110f0d6bf` |
| [demo3c-73-attack-muted.png](../demo/demo3c-73-attack-muted.png) | 721573 | `ee30e13d6bf61428cb3d601a38f5e0c25c92ad299962831c6fb0b0e7c92d0f03` |
| [demo3c-74-attack-unmuted-setting.png](../demo/demo3c-74-attack-unmuted-setting.png) | 447117 | `8a38bdb795a09e68e6cc82ae8d7d314c690df4a258e5028a82da82ffb53da2e9` |
| [demo3c-75-attack-unmuted.png](../demo/demo3c-75-attack-unmuted.png) | 722604 | `dfeb1bfb5e1547e490aa89c07930a213437b57a4a42df40b1f4a3a10523bf612` |
| [demo3c-76-exp-after.png](../demo/demo3c-76-exp-after.png) | 453425 | `84ed792f54da516e4bbcaf71e52e3e2193dc53a409d22cefd04d48bd87b631ac` |

### Backlog對照與下一棒

README保留當前TASKS的完整B01–B15對照：B01技能擴充、B02慢速丟撿、B03–B04地圖效果、B05交易前提、B06地面pin、B07原生音效語義、B08封裝、B09 P27、B10歷史來源、B11調度器、B12舊EXE基準、B13其他伺服器、B14金幣規則、B15可攜assets及9個例外。本次B11排他前置已解決；B14金幣拒絕仍屬Backlog，不重丟金幣、不改DB繞過。其他Backlog不能豁免T062近戰、技能4、普通肉循環、座標或T035已有圖示／音訊。

請專案主控對本次實測做**全新審查**，不要套用第2c-a舊review。優先裁定F9圖示破圖、移動後自身權威回包缺失，以及可重現的有效近戰選取／傷害／EXP流程；安排合法一般NPC購肉、非滿血情境與穩定own-session音訊量測後再派未通過列。此次操作偏差與前提缺失不直接等同產品根因。M1不可標完成；不合併、不關Issue，不再啟動本輪服務。

## 第 2d-a 輪：主專案同步與 dev 建置（2026-11-06，codex-local）

### 做了什麼／已驗證

本輪完成第 2d-b 輪的建置前置，未進行原生實機驗收，M1 尚未宣告完成。工作目錄為 `D:\lineage開發\TW1608262200研究`；先讀根目錄 `AGENTS.md` 與 `docs/REPORT.md`。起始分支 `task/77`、HEAD `0ab8f93a1ff8bb5f029932b9d799f13ba027c8ce`，`git status --porcelain --untracked-files=no` 無輸出。保留全部既有未追蹤檔，未 reset、stash 或刪檔。

2026-11-06T09:33:23Z 以 `curl.exe --noproxy "*" --max-time 15 http://127.0.0.1:8787/api/state` 唯讀核實：唯一 `p24lab` lease 的 holder 為 `chaoren0418-arch/tw-bin-engine#77`，exp `1793962928.734436`（2026-11-06T11:02:08.734Z），與派工指定的秒級到期時間一致且未到期。

依本輪具體步驟執行 `git fetch origin` → `git checkout main` → `git merge --ff-only origin/main`，均退出 0。建置 HEAD 為 `5708965656e33f12a16b05698e3f7e0552a8e59f`，包含 PR #124 合併 commit `38e2e50dbacaadddb9ab58fe434f00db7c15fda6`；`git merge-base --is-ancestor <PR124合併SHA> HEAD` 退出 0。

建置與 check-only 後追蹤檔仍乾淨。為保留 task/77 尚未進 main 的第 2a～2c-b 輪報告，先 `git checkout task/77`、`git merge origin/main -m 'task #77: merge origin/main for round 2d-a'`（均退出 0），再追加本節；merge commit 為 `c7e8cda9aab4fd3d21149e3cd9120897df6d6637`，未 rebase、未重設分支。`git diff origin/main -- engine` 無差異，因此分支切換後產品來源與本次建置完全相同。

### 指令與退出碼

| 指令／檢查 | 退出碼／結果 |
|---|---|
| `python -B -X utf8 engine/_integration/dev/rebuild.py --build --write-receipt --output scratch/issue-77/rebuild-2d` | 0；`pass-dev-build` |
| dev 來源核驗 | 171 個 source、180 個 pins；`writes: false`，未更新 lock |
| SDK／product restore／product build／launcher restore／launcher build | 全部 0；SDK 10.0.300 |
| product build | 2 警告、0 錯誤：`MagicUi.cs(20,62)` 的 CS8600、CS8602；本輪未修改產品來源 |
| launcher build | 0 警告、0 錯誤 |
| `engine\啟動新版客戶端.cmd --check-only` | 0；`revision: dev`、`uiStarted: false` |

執行器的一般沙箱啟動最初出現 `setup refresh had errors`，改用核准的非沙箱執行後正常。第一次建置呼叫因 PATH 未含 Python 而未啟動（PowerShell 命令解析失敗，外層退出 1）；以 `py -0p` 找到既有 Python 3.12，只在本次程序 PATH 加入 `C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312` 後原指令成功。未安裝工具或修改系統 PATH。

為符合專案寫入限制，程序 `NUGET_PACKAGES` 指向 `engine/.nuget`，`TEMP`／`TMP` 指向 `scratch/issue-77/rebuild-2d/tmp`；rebuild 自行將 `DOTNET_CLI_HOME`、NuGet scratch 放入本輪 scratch。輸出沿用既定 `engine/build/client-product-dev`、`engine/launcher` 與專案 `obj`，未複製遊戲資源。

### check-only 完整 JSON

```json
{
  "status": "checked-only",
  "revision": "dev",
  "executable": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\engine\\build\\client-product-dev\\P11.ClientEngine.exe",
  "workingDirectory": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\engine",
  "arguments": [
    "--normal-product"
  ],
  "manifest": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\engine\\build\\product-dev-manifest.json",
  "manifestSha256": "162c9b5b42dac2e44a9591dd7a560a76be870d8f0e5c88e974cc2d8ec5cae5d9",
  "hashedRuntimeAndWebFiles": 24,
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

### 產物身分與產出路徑

Receipt 建置時間為 `2026-11-06T09:35:08.504575+00:00`；下列 DLL 均以 `Get-FileHash -Algorithm SHA256` 再次核對，與 `result.json`／receipt 一致。

| 產物 | SHA-256 |
|---|---|
| `engine/build/client-product-dev/P11.ClientEngine.dll` | `5747ffb6f5b31f0686645b8ed15e2b3f43be1317cb7fced55d1936ccbc86a9e9` |
| `engine/build/client-product-dev/P24.Audio.dll` | `f135b5080fb5acab27905d243e3a03fa06bfb7fc941bcf313f8839535abcd63d` |
| `engine/build/product-dev-manifest.json` | `162c9b5b42dac2e44a9591dd7a560a76be870d8f0e5c88e974cc2d8ec5cae5d9` |
| `engine/launcher/P24.Launcher.dll` | `222e8a0d03c657a980c2d1c0ab24e46c2026e41b1ea17866b73029cf7452e63d` |

本輪交付僅追加本報告 `docs/reports/issue-77.md`。本機證據保存在 `scratch/issue-77/rebuild-2d/`：`result.json`、`build-console.log`、`build-exit.txt`、各建置步驟 `.command.json`／`.stdout`／`.stderr`、`check-only.json`、`check-only-exit.txt`；lease 回應在 `scratch/issue-77/lease-state-2d.json`。Receipt 為 `engine/_integration/dev/receipt.json`（既定忽略檔，不提交）。關鍵 JSON、SHA 與結果已內嵌本節，不依賴 scratch 永久保存。

### 未驗證什麼／給下一棒的提示

- 未啟動遊戲 UI、P24Lab、MySQL；未登入、查詢 DB、連正式服務或操作既有遊戲程序。未修改產品程式、lock、機密配置、封存內容或硬連結；`docs/REPORT.md` 僅隨 main 同步取得既有索引，沒有本輪編輯。
- 本輪只證明指定來源可建置且入口唯讀檢查通過；不把 PR #124 的離線結果或本輪 check-only 當作 F9、光箭尾音、靜音 session、移動提示已通過真機驗收。
- 交由主控審查本節後改 runner 為 dot，重排第 2d-b 輪。主目錄停在 `task/77`。dot 先確認 HEAD 含本節的報告 commit、check-only 為 dev 且兩個 DLL SHA 與上表一致；本輪後不得另行同步或重建再沿用這份身分。
- dot 依第 2d-b 派工重新核實有效唯一 lease、兩埠未監聽與服務身分。M1 必須完成普通肉 40056 的 1／10 丟撿循環，以及近戰／光箭各自有聲、靜音、恢復的有效事件音量；靜音 session 必須存在、null 不算 0，量測窗至少 4 秒。其他列依主控裁定標示原因與 Backlog／M2；本輪不關 Issue、不宣告 M1 完成。

## 第 2d-b 輪：部分實測與正常收尾（2026-11-06）

**結論：部分實測有新證據，兩個M1必驗列尚未完成。** 本節對應派工 [6683465548](https://github.com/chaoren0418-arch/tw-bin-engine/issues/77#issuecomment-6683465548)。沿用原會話，保留第2b／2c-b已完成子項，不重做第2d-a建置。收到停止驗收、僅做身分核實與收尾的指示後，已停止新增驗收並正常清理；本次文件交付沒有重啟、重新連線或補測。

### 候選與已完成前置

- 候選為 `task/77@3f802bedff42530d918a40444ea49bae6c529b24`，含 PR #124；追蹤檔起始乾淨，未fetch、切別支、重建、修改產品或lock。
- 本輪入口檢查exit0、revision=dev，未由check-only啟動UI或服務。兩個DLL實際雜湊符合第2d-a：主程式 `5747ffb6f5b31f0686645b8ed15e2b3f43be1317cb7fced55d1936ccbc86a9e9`，音訊 `f135b5080fb5acab27905d243e3a03fa06bfb7fc941bcf313f8839535abcd63d`。
- 啟動前已核對本輪排他使用條件與前輪清理證據；只使用指定實驗環境及測試角色。未改DB、配置或權限，未操作正式環境。程序身分疑點核對期間曾暫停新增互動；本次啟動證據支持服務歸屬，但未直接列舉OS父子程序鏈，未重試先前被拒的程序查詢。

### 本輪結果與解讀界線

| 項目 | 狀態 | 實際證據／不足 |
|---|---|---|
| 登入與進世界 | 已完成前置 | 成功登入指定測試角色並進世界；不將重複前置計為新增M1通過列 |
| 光箭有聲階段 | 部分已驗證 | 一次C6及對應S30、MP117→115；畫面回報傷害欄17，但不是獨立目標HP差證明。完整效果、距離對照及三階段音量尚未完成 |
| 近戰有聲階段 | 未建立有效事件，驗收未通過 | 僅一次G，畫面明確「未選取有效目標」，全輪C229=0。沒有連打、擊殺或EXP增加證據，不能判近戰產品根因 |
| 攻擊／光箭靜音與恢復 | 未測 | 沒有這四組有效事件、靜音零峰值或恢復正峰值證據；不能以有聲階段的session存在代替 |
| 普通肉取得及1／10丟撿 | 未執行循環 | 供貨唯讀查找初期因來源不唯一停止；後續按本輪服務實際連線與商店來源交集限定後，取得販售NPC及位置線索。尚未在遊戲內抵達、購買或丟撿，不能稱「沒有NPC販售」或循環通過 |
| 拾取錯誤直接改正 | 未測 | 未取得合法地面普通肉；全輪C25=0、C112=0，不是已完成拾取防呆或數量循環的證據 |
| 非滿血治癒／喝水 | 未測 | HP一直滿血，未建立受傷前提、未使用藥水或治癒；不能判功能失效 |
| 八方向／點地權威終點 | 未測 | 本輪沒有移動驗收或重登終點核對；不能以初始座標或前輪資料補足 |
| F9背包圖示／地面圖 | 未測 | 新候選尚未在本輪完成F9對照，不能把第2c-b破圖直接判為本候選仍失敗，也不能只憑修正合併判通過 |

供貨查找中另遇一次唯讀距離排序的無號整數運算錯誤，修正查詢運算後取得位置線索；未改資料或配置。來源篩選與查找結果的原始內容保留本機，不公開內部名稱、結構或位置細節。這段進展僅解除部分查找疑義，並未完成遊戲內購買前提。

### 已取得的音訊數據

量測只針對本次客戶端的音訊session，沒有拿其他程序聲音充數。每組設定20秒、每100ms取樣，實際含排程開銷約23秒；原始instance ID保留本機，公開僅記連續性與數值。

| 測量 | 樣本／session匹配 | 峰值 | 能證明什麼 |
|---|---|---|---|
| 光箭有聲整窗 | 200／200；同一instance | 0.5480536222457886 | 本次整窗有正峰值，不能單獨等同六組驗收完成 |
| 光箭C6前 | 已有匹配session | 0 | 提供本次事件前背景對照 |
| 光箭C6後6秒 | 61／61 | 0.5480536 | 有效光箭事件窗內測得正峰值，窗長超過4秒；不作完整音效語義或所有距離場景證明 |
| 近戰有聲嘗試整窗 | 200／200；同一instance | 0 | 因沒有C229，這只是無有效近戰事件的量測，不能計為近戰通過或產品無聲故障 |

同一次光箭的C6、S30、MP差與音訊樣本可以互相核對。靜音、恢復及有效近戰的必要對照仍不存在；null不替換為0，也不以常駐靜音stream取代事件音效證據。原始截圖與trace只留本機，本次不新增公開圖片或原始JSON／log，不使用舊圖冒充本候選成果。

### 正常收尾與交付範圍

已核實本輪自有客戶端先登出並正常退出，伺服器正常退出，實驗資料庫正常關閉；最終核對本輪自有程序全數退出、實驗監聽均已關閉。沒有停止未確認歸屬的程序，沒有延長使用時間或改設定。前述歸屬判斷、原始清理紀錄及命令輸出已由上層另行唯讀核對一致，詳細資料留本機。

本次公開交付只更新 `docs/demo/README.md` 與本報告，保留歷史已完成項目。沒有新增公開內部部署、連線或程序身分細節，沒有上傳帳密、原始log／JSON或未檢查截圖，也不涉及其他任務的受限內容。文件整理期間未恢復實測，未修改 `docs/REPORT.md`。

### 待專案主控裁定

1. M1兩個必驗列仍未完成：普通肉1／10丟撿及拾取更正；攻擊與光箭各自有聲／靜音／恢復的有效事件音量。請決定下一輪剩餘驗收優先及對應修復，不以本次部分正面數值宣告M1完成。
2. 普通肉已有販售線索，但尚缺遊戲內購買與循環；近戰需先建立穩定有效目標再測音訊。本次無有效近戰事件，不能直接歸責產品故障。
3. 其餘未測列由主控依既有B01–B15／M2範圍裁定。B14金幣限制不豁免替代物品必驗列，B07也不豁免音量對照。
4. 本輪須全新審查，不沿用第2d-a舊review；本次不合併、不關Issue、不改標籤。

## 第 2e 輪：限定清單實測（2026-11-06）

依 [本次派工](https://github.com/chaoren0418-arch/tw-bin-engine/issues/77#issuecomment-6684247112)，本次只處理普通肉循環、光箭靜音／恢復與近戰三階段音量；清單已在同一次執行內結束，沒有另拆續測或重做前輪通過項。**近戰事件音量對照通過，其餘兩項仍為 ⚠️；M1 不宣稱全部通過。**

### 做了什麼／怎麼驗

- 實測 HEAD `54e0e463c2653395d4af7f33e0a8a2d5a608e258`，含候選 `3f802bedff42530d918a40444ea49bae6c529b24`／PR #124。check-only exit0、revision=dev；主程式 SHA-256 `5747ffb6f5b31f0686645b8ed15e2b3f43be1317cb7fced55d1936ccbc86a9e9`，音訊 SHA-256 `f135b5080fb5acab27905d243e3a03fa06bfb7fc941bcf313f8839535abcd63d`，與前輪相同；未同步、重建或改產品／lock。
- 僅用已核實的實驗環境與測試角色，沿用原生操作；先核實本次啟動歸屬。未修改權限或重試先前被拒查詢。一次截圖因身分保護未產出；收起選單後既有檢查正常、後續實拍成功，未繞過保護。
- 普通肉：依既有販售線索步行，途中出現「靜態地形／已載入範圍邊界阻擋」。未完成到店購買，轉做獨立音效項並預留清理時間。未購買／丟1／撿1／丟10／撿10／拾取更正；全程C25=0、C112=0，不能推論NPC不販售。未吃肉或修改資料、配置。
- 光箭：遊戲內主音量50%，確認[靜音勾選](../demo/demo3e-sound-muted.png)及[恢復未勾選](../demo/demo3e-sound-unmuted.png)，各對同一可見史萊姆送一次C6。靜音後畫面MP117→115；恢復嘗試畫面為117，期間有自然回復，不據此斷言第二次扣魔成功。兩次無S30、未取得可確認的光箭效果，故事件音量對照不能判通過。
- 近戰：靠近牛後從可見清單選取，早先一次選取找不到項目，尚未進入攻擊／量測。重新觀察後同一牛完成三次單次G，各有C229及緊接S30；只有這三次計入事件。未連打、刷怪；未取得逐擊目標HP、死亡或EXP證據，不擴張成完整戰鬥閉環通過。

### 真實量測與判定

每組設定20秒、200筆；含排程實際約21.7–21.8秒。每組200／200筆都有同一個本次客戶端session，五組instance一致，沒有缺session或把null當0；事件前峰值均0。下表時間均為UTC，事件窗是送出後6秒，超過至少4秒要求。

| 項目 | 送出／S30時間 | 整窗峰值 | 事件後6秒：匹配樣本／峰值 | 結果與截圖 |
|---|---|---|---|---|
| 光箭靜音 | C6 12:19:29.186；無S30 | 0 | 56／56；0 | ⚠️ 缺有效效果回包；[實拍](../demo/demo3e-arrow-muted-4.png) |
| 光箭恢復 | C6 12:20:23.326；無S30 | 0 | 55／55；0 | ⚠️ 未證明恢復發聲；[實拍](../demo/demo3e-arrow-unmuted-4.png) |
| 近戰有聲 | C229 12:27:24.910；S30 12:27:25.002 | 0.5086371302604675 | 55／55；0.50863713 | ✅ 事件發聲；[實拍](../demo/demo3e-attack-audible.png) |
| 近戰靜音 | C229 12:28:13.543；S30 12:28:13.560 | 0 | 55／55；0 | ✅ 有效事件靜音；[實拍](../demo/demo3e-attack-muted.png) |
| 近戰恢復 | C229 12:28:53.212；S30 12:28:53.222 | 0.5086371302604675 | 55／55；0.50863713 | ✅ 恢復發聲；[實拍](../demo/demo3e-attack-unmuted.png) |

本輪全程C6=2、C229=3，三筆S30均對應近戰時間窗。光箭零峰值的原因不能由現有證據唯一判定，不能寫成「音訊模組故障」或「靜音成功」。前輪光箭有聲正峰值保留為歷史，不替代本次缺少的有效靜音／恢復事件。

### 清理、產出與未驗證項

測完恢復遊戲音量，正常登出並關閉本次客戶端，接著正常關閉伺服器及實驗資料庫。**12:30:20 UTC 已核實本次自有程序全數退出、實驗監聽均關閉**，在既定使用期限內完成；沒有強制關閉不明程序、續租或重新啟動。原始身分、清理與量測資料只留本機。

公開產出為本報告、`docs/demo/README.md` 及上方7張真實客戶區PNG；每張逐一檢視，均小於2 MB，未合成或後製，無帳密、私人玩家資料或無關桌面。其餘導航、原始JSON／log與內部部署細節不提交；既有未追蹤舊圖保持不動，未改 `docs/REPORT.md`。

普通肉循環／拾取更正與光箭有效事件音量仍待驗證；其他列依派工本次不測，前輪通過證據保留。請專案主控依 #128／M1 收尾規則安排剩餘缺口的M2追蹤與對應修復，審查本次新證據；不把近戰音量子項通過擴張成M1全通過。本次只交付，不合併、不關閉Issue、不改標籤。
