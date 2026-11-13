> **r35（2026-11-04）**：canonical 來源 181 檔，lock 在 `_integration/r35/rebuild-lock.json`；建置 `engine/build-product.ps1`（→ r35）；CI 於 windows-latest 以 `rebuild.py --build --write-receipt` 產生 `receipt.json`。正式入口仍為 r33，待 M1 真機基線後切換。

# r33 引擎來源與操作

共享 engine 合入 accepted ENGINE-GROUND-LIFECYCLE-IMPL-R1。177來源相對r32僅Ground.cs與WorldEntry.cs改變（r35 canonical 已更新為 181 檔：P36 新增 source/Resources/pickup-own-base15-profile-extension.json 等，見 _integration/r35）：正常Speed0/1肉／箭單次pickup及共用既有手動drop sender修正session/world/actor/lease admission與晚完成歸屬。world／actor換置釋放舊busy/deadline/pending，舊callback不能改新owner。保留r32 item防護、900ms／3000ms政策、Slow gates、原封包／入口／動畫，不新增Slowpickup或drop能力。

正式根「啟動引擎.cmd」在release完整SHA通過後切至releases/r33；`--check-only`僅核環境及runtime，不開GUI、登入或socket，不啟停服務。資料庫與服務由使用者管理。

唯讀來源核驗（現行）：`python -B -X utf8 engine/_integration/r35/rebuild.py`；歷史 r33：`engine/_integration/r33/rebuild.py`。必要時加`--verify`或`--build`，只能在精確canonical engine產生新的evidence；release不可建置。本輪canonical產品一次重建四身份與stage完全相符，另依派工對同版執行4337 assertions及93 observations分列。

本輪canonical helper與suite本文／expected保持stage exact副本；utility保留原CS8602警告1，產品／launcher／ground及其餘helpers零警告。ground24個newowner證據仍為pass:null觀察，原checker驗其data；200formal owner及真900／3000時鐘保留。接線失敗、未完成ground baseline的修復attempt與成功結果完整保留，不把它們當產品功能缺陷或假稱所有attempt一次。

> **`engine/assets` 現況（2026-11-14，T473）**：開發機的 junction 已改指 `releases/r29/assets`（#155），見 `docs/BUILD.md`「資源目錄約定」。**可攜模式**（M8）不使用 junction：`engine/assets` 是使用者本機產生的普通目錄。zip 根目錄的 `啟動引擎.cmd`（來源 `engine/tools/portable/`）設定 `TW_CLIENT_ROOT`，launcher 第一次啟動時以 `P11.ClientEngine.exe --prepare-assets` 從原客戶端產生（v2 `integrity.json`＋`prep-receipt.json`）。準備器拒絕寫入 junction，開發機的 junction 不受影響。使用者步驟見 `engine/tools/portable/README.md`。下一段是 r33 當時的紀錄。

engine/assets維持原canonical r28唯讀junction。新release素材以immutable hardlink重用r28，無新junction，不能透過任一路徑改本文或屬性。新素材修改須獨立新檔。r28～r32封存保持。engine根兩個metadata vectors為exact離線helper建置輸入，engine/build/r33-canonical-*-checks為本輪helpers；產品來源仍177檔。

ground harness本身關閉cache，但PlayerProfiles等產品方法仍透過AppContext.BaseDirectory間接讀../../assets；helpers位於engine/build下使用原素材，不建立額外14k素材集。native utility scheduler、frame初始化／+1scheduler／絕對release起點仍unknown；相對marker與額外guard屬引擎policy，保留flight30/60ms、effect、F1。離線接受不代表native/live或整個P24完成。

交接：work/engine-r33-integration-r1/shared-integration-r1/HANDOFF-r33.md。readonly release verifier位於releases/r33/_integration/r33/verify_release.py，精確seal見project-manifest.json。
