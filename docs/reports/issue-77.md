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
