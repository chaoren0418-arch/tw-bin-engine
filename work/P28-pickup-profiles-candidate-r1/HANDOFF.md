# P28 pickup profiles 候選交接

候選 **ENGINE-PICKUP-OWN-PROFILES-P28-R1** 已完成離線實作、實際 build 與本階段測試，僅存在本工作目錄，待主管交給 P29 獨立覆核。交付不代表接受、合入或正式 r35。

唯一主管：01a185b4-ecf6-705c-90e6-83b9eb1ba02f。唯一共享整合者仍為 01a17a22-17e4-70c0-9792-166ddb86ea42。本次沒有修改入口、共享 engine、封存 release、其他工作目錄或素材；未執行 DB／服務／網路／GUI／原生遊戲操作。

## 基準與實作

- final r34 baseline SHA `ac4254ea339ed35c4257e27e955f0411c617f21698a1da5bb59330a162933024`；release seal `955ea88173cb36cecaefb6d5e9bbce7d76dfd7bf39ccd7830e56a269ab3155c3`。
- 180 個來源逐檔與 engine、sealed r34 核對，共 360 比對，實作前及交付稽核皆吻合。獨立複製 150 個必要 source/audio 檔，新增資源後完整候選 compile source 共 151 檔。另 30 個未複製 web 檔保持基準引用。
- 修改 `PickupAssets.cs`、`ObjectAssets.cs`、`PickupPresentation.cs`、`OfflineClient.csproj`，新增 extension JSON。差異見 `change-manifest.json` 與 `diffs/`。
- loader 驗證兩份 resource 的 exact SHA／大小，依 gfx/sprite 分組，使用原始 exact sequence keys；僅允許 42 個 gfx/weapon pair、own、Speed0/1、Brave0。
- terminal 使用目前 exact profile 已載入的 frame instance，拒絕借用同 gfx 其他 weapon 的 appearance。原 shadow compositor 保持不變：pickup 無 shadow，terminal 恢復 exact-key shadow。
- scheduler、ActiveOwnCombat／WithProjectileHit／Timing.Matches、ActionCadence、double Duration、death、S87 weapon8、900/3000、數量權威與音訊程式均未改寫。PickupPresentation 僅改 pair guard 與傳入目前 weapon 的兩行。

## 產品與資源身份

候選 DLL：`candidate/build/P11.ClientEngine.dll`

SHA-256：`6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3`

原有 resource 保持 **239,766 bytes**，SHA `43b88cec329929484610aceae84566d1a922eb4b8ceba93e17dc44b65922ca90`。

新增 resource 為 **2,473,547 bytes**，SHA `aff2e04d4f9395f7993baf5818eb16218730b7def0a29c7d07e86c314db513ba`，12 組／288 幀。使用已接受 plan.py 的相同 expression 與既有解碼 JSON，在記憶體產生 payload；沒有重解 SPR、掃 PAK 或複製素材。

audio DLL 與 r34 相同：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`。

## 實際執行結果

| 項目 | 實際結果 |
|---|---:|
| 產品 build | 1 次成功，0 warnings／0 errors |
| regression-01 | 357 assertions |
| profiles-02 | 15,875 assertions |
| boundaries-01 | 3,823 assertions |
| negative-01 | 6,751 assertions |
| namespace-01 | 147 assertions |
| 候選 runtime 合計 | **26,953 assertions** |
| 封存 r34 DLL 對照執行 | 225 assertions，另列、不加進候選 |
| 實際 mapping | 672 combinations／**2,688 rows** |
| 獨特 body PNG／RGBA | 416，與獨立 prep fixture 比對 |
| 0/0 candidate ↔ r34 對照 PNG | 32 張逐 byte 相同 |
| native／完整產品驗收 | **0／未完成** |

所有候選成功 suite 都從實際載入 assembly 取得上述 DLL SHA。測試使用離線 helper、明示的空 scene fixture、注入 clock 與 synthetic authoritative packets，實際呼叫 candidate catalog／renderer／scheduler。素材唯讀直取，helper-only AppContext 指向 sealed r34 assets，沒有新增 junction。`coverage.json` 列每個 test family 的範圍與限制；`evidence/*/*.command.json`、stdout/stderr、checks.json 及 PNG 保留原始結果。

原 profiles-01 exit1 完整保留。原因為 helper 將 terminal keys 一起計入碰撞數，得到 all-frame80，而契約為 pickup-only64；沒有改 expected 為實測值。已改為 metadataIndex != 0，重建 helper 並完成 profiles-02；候選產品沒有重建。另 namespace-01 比較封存 namespace fixture 的全部 64 keys 與 gfx/sprite 集合，也比較 sealed body fixture 的 all-frame80 全集合。已通過的其餘 suite 沒有重跑。失敗 attempt 的斷言不加進成功合計，詳見 `attempt-resolution.json`。

## 可交給 P29 的依據

1. 先跑 `verify_delivery.py`，再核 `source-manifest.json`、`change-manifest.json`、`input-pins.json`、`final-input-audit.json` 與 `artifact-manifest.json`。
2. `evidence/profiles-02/executed-mapping.json` 有 2,688 筆真正執行的 slotId、key、sprite、RGBA hash、時間與 serial；並非把 planned rows 改名當結果。
3. `coverage.json`、各 suite checks 與 helper source snapshots 可追溯 case。`REPRODUCE.md` 說明只讀驗封及在另外授權 fresh root 重跑的方式。
4. prep 的 308 input pins 已逐一驗證。唯一歷史 AGENTS pin 依 P26 byte reconstruction proof 在記憶體重建核符；現行 AGENTS／takeover 另綁，未改歷史 seal 或重算舊成功。

尚未執行 P29 的獨立覆核、完整 canonical release suite、正式產品封裝、共享合入、原生／GUI／完整地圖與完整操作流程。7822 planned overlapping slots 不是本階段執行數；r34 歷史 4521 assertions＋93 observations 也不加進本次合計。104 base 與 312 pickup 的來源資格保持 historical-selected-payload-matched／current-idx-and-bounded-slice-only，沒有提升 BIN／native 資格。

正式入口仍依本派工為 r33；入口切換待使用者明示，並非此離線候選的依賴。本階段 Goal 在驗封成功後完成，不代表全案完成。
