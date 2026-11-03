# ENGINE-R34-INTEGRATION-PREP-R1

本包只完成 r34 整合準備。候選 family `ENGINE-PICKUP-PRESENTATION-IMPL-Rn`、source/resource manifest、正式 accepted independent review 與新主管收據均待交；沒有讀取未封存 author/reviewer 根，沒有製造可用的 accepted delivery。不要把本包視為 r34 build 成功或 P24 完成。

唯一寫入根是本目錄。未建產品、未呼叫 SDK、未建 stage、未連結／複製素材、未執行回歸 suite、未改 shared engine/releases/入口/主管；未執行 GUI/game/socket/native/JAR/DB/services；沒有新 chat、subagent 或 Goal。既有 Goal 讀取結果為 null。

本輪最終 31 項小型 unittest 通過（初版30項；controller調整後新增1項拒絕測試），涵蓋 gate/path/schema、EmbeddedResource、來源新增、review binding、helper 相對路徑、collector 拒絕 partial attempt 等。這個數字不屬於產品 assertions。未實跑候選產品或新 PE resource；PE 檢查器與 future controller 僅完成靜態語法核驗，實際 r34 編譯與封裝驗證仍須下一個精確派工。

## 基底與資源

- final r33 supervisor acceptance：`09b35736898f5a5c269ddcaab7ecd375ce7f6c0f5b04bcc22776d331905d176a`。
- r33 release seal：`846302448dc4a1421a2689b96632978353b6e39b1c9f8a9331bdd23b7760a417`。
- r33 shared integration seal：`d3d92c65baf6175852914fd98890c8abcf099f9a695e39566a8e0e5a508f6aee`。
- r34 implementation contract：`8c60b1061c637bcb7f992d7f92c74fc80c0ad945c7f05d1c3a2d74507cffc6f2`。
- final baseline：`2a946e965f1aed1f9a0358ceb560f52575ea5a3bf217fc9ccf36e9ce5823c40f`；binding：`28d0d6f3386575c175aa06b7b9076da961cfd1db62e2563cf7cd65c7c276ea95`。
- 177 baseline source pins 與原 stage 相同，四身份相同。`baseline-lock.json` 另鎖 28 support pins（含兩份 metadata vectors）。候選總數按 baseline177＋接受的 declared additions 計算，沒有177上限。
- 唯一可新增 JSON resource：`source/Resources/pickup-own-base15-gfx0-weapon0.json`，239766 bytes，SHA `43b88cec329929484610aceae84566d1a922eb4b8ceba93e17dc44b65922ca90`；logical name `P06.Resources.PickupOwnBase15Gfx0Weapon0.json`；24 新 body frames、8 既有 frame0 references。本輪沒有產生或複製 pixel payload。
- csproj 必須明確、無條件嵌入上述 resource。除了這個 additive item，基底 project 結構保持相同；如候選確實需要其他 MSBuild 變更，先取得新 qualification，不擴寬任意 Include/Import/glob。
- 新 resource 同時出現在完整 source manifest 和 resource manifest，受 candidate seal 覆蓋。stage copy、rebuild lock、release package 都要求其存在；future stage/rebuild 直接解析 PE ManifestResource 驗 size/hash，不載入或執行產品 DLL。

## 準備內容

`acceptance_gate.py`、`acceptance-materials.schema.json`：native candidate schema 相容，完整 flat 或 native nested review binding，拒絕部分或衝突 binding。`MATERIALS.md` 定義新資料欄位；`pending-materials.json` 只有 pending/null，不是假 accepted instance。

`run_next_stage.py`：預設唯讀。只有 exact contract＋receipt SHA 通過 gate 後，future `--build` 才能建立本目錄 `r34-runs` 下收據授權的 fresh child。`prepared/run_stage_r34.py` 拒絕直接執行，baseline151 後仍不宣稱全套通過。`prepare_rebuild.py` 產生完整 source/resource lock 及 exact canonical engine guard。

`serial_canonical.py`、`execution_support.py`：新 stage acceptance 及新 shared receipt 接受 exact plan 後才可 install／逐步 execute；預設不寫。順序 product→baseline→ground→utility→haste→versioned-slow→F1→pickup，每一步須收集完成才可下一步。任何已完成 step 不可 replay。

`workflow-contract.json` 明列 future command plan 與 normalized reports 的完整需求。候選獨立測試尚未封存，因此本包不填造其 commands、assertions 或結果；後續 exact plan 必須接上被接受的 helper／collector及其 script pins。舊 suite body/expected 從 sealed references 複製；若只需 native report key 映射，保留原報告和逐行 occurrence 對帳。不能用 normalized totals 取代原始執行證據。

`package_release.py`：只接受另一份精確 release receipt。要求 source/resource/rebuild/runtime dependencies 與 canonical evidence 完整，fresh `releases/r34`，13958 immutable asset hardlinks；無整包 copy fallback，無新 junction。全 SHA inventory 封存後仍待主管驗證；沒有 root entry activation 能力。

`adaptation.py` 提供 ground checker 的單一 exact DLL hash 替換與 unified diff；新 hash 僅能來自 future accepted receipt，本輪未套用。`test-adaptation-inventory.json`、`VALIDATION-PLAN.md` 保存所有接線及 coverage 限制。

## 回歸與 r33 經驗

既有 4337＝151＋735 ground＋431 utility＋1707 haste＋1201 slow＋112 F1。93 observations＝81 ground＋12 NPC，另列。新 pickup assertions/observations 只採正式 accepted independent review，目前未知，不把 author 數字或 source proposed tests 加進總數。

主管已明確指示：兩個「無動畫」舊 ground case 的 Weapon46 在本增量外，原 body/expected 保留；它們只檢查 OwnCombat，名稱不構成 blocker。gfx0/weapon0 的權威 S158 正例及本地 C112 不得啟動 presentation，交正式 review P01/P02 補完整。詳見 adaptation inventory 的實際觸發與行號。

stage helper 必須位於 `stage/build/r34-*`，canonical helper 位於 `engine/build/r34-canonical-*`，使產品 DLL 的 `AppContext.BaseDirectory/../../assets` 讀到其所屬 assets。canonical 三個 baseline helpers 也採同樣位置。兩份 metadata vectors 保持在產品根。不能只看 harness 沒直接讀 assets 就忽略 DLL 間接依賴。

canonical baseline 在收集前，先用實際 canonical product/launcher command exit0 與原始 stdout 的 size/SHA 建立 log binding。統一 `utf-8-sig, errors=replace`、保留原始 bytes，記錄 replacement count。failed attempts／partial probes 保留；collect-only 必須逐一選用成功、完整、同 DLL、同 argv/cwd 的 attempt，且不執行 subprocess。不硬編 `recovery/command-*` fallback。

r33 的四個 canonical 失敗階段仍是真實歷史：baseline 最後收集缺 build logs；第一次收集的 strict UTF8 decode 失敗；ground 因 baseline 未收集而在 build 前拒絕；ground-ready 的 helper 位置使 baseline mode 找不到間接 assets。其成功 build／helper 不曾機械重跑，ground failed baseline 有第二 attempt，其餘 modes 成功一次。原 sealed 根未改；本包 `reference/r33-canonical` 保留 pinned scripts、phase command receipts、once/reconciliation 等。stage 的 source copy 路徑假設及 observation 分類修正也保留在原 r33 seal 與本包 reference 記錄。

## 保全與封存

`verify_preparation.py` 是唯讀核驗：177 source＋28 support、130 reference copies、18 fixed immutable pins、1837 older-release pins、696 r33 non-asset/index pins、44 qualified-source pins（其中1個外部絕對路徑）。外部 pin 先核 SHA/size，再依 containment 分類；不先做 `relative_to`。僅資格清單要求的兩個舊 asset 文件被核 hash，沒有掃13957素材全體。

`evidence/preparation-tests.json` 如實記錄首次30項 unittest 的 tool transcript 結果；該次 raw stdout/stderr 沒另存檔。Controller調整及新增拒絕case後，最終31項全部通過，actual argv、stdout、stderr、source/log hashes 存在 `evidence/final-small-tests/`。兩次均不是產品suite。`result.json`、`preparation-seal.json`、`artifact-seal.json` 為最終交付入口。preparation seal 不含後產生的 result/seal/final verification，artifact seal 包含它們並排除自身，避免循環。

封存後停止寫入，等待主管下一個精確派工。
