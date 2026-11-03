# ENGINE-R34-CANONICAL-PLAN-R1

本交付只建立 canonical 計畫與靜態驗證。沒有新 shared receipt、install、product/helper build、suite execution、release 或入口啟用；不得把計畫內的預期 counts 當成執行結果。exact plan SHA 與此包封存 SHA 見 `result.json` 及最終交付訊息。

新計畫根目錄：`D:\lineage開發\TW1608262200研究\work\engine-r34-canonical-plan-r1`。
唯一未來 canonicalEngine：`D:\lineage開發\TW1608262200研究\engine`。
未來 workflowRoot：`work/engine-r34-integration-r2/r34-shared/accepted-pickup-presentation-r1-01`；本回合不得建立。
原 controller：`work/engine-r34-integration-r2/serial_canonical.py`，保持封存原樣。

## 主管驗收與未來執行

先驗收 `canonical-plan.json` 的實際 SHA。只有主管另發新 `engine-r34-shared-acceptance-v1` receipt 才能執行。此包不建立範例或假 receipt。wrapper 固定讀取 `主管/P24-權威拾取動畫-20261029/r34-shared-acceptance.json`；若主管要用其他位置，須先修訂未執行計畫並重新驗收 SHA，不能執行時臨時改參數。

receipt 必須符合原 sealed `validate_plan`，包括 supervisor、`accepted-for-r34-serial-canonical`、authorization=`install-build-and-offline-suites`、新 `ENGINE-R34-SHARED-INTEGRATE-` dispatch、exact plan SHA、candidate、stage seal、stage acceptance SHA 與四項 identity。先以原 controller 無 `--execute` 驗證。接著每次僅執行一個 step：

`install → product → baseline → ground → utility → haste → versioned-slow → f1 → pickup`

每次 controller 命令為 `python -B -X utf8 <sealed serial_canonical.py> --plan <此包/canonical-plan.json> --plan-sha256 <actual plan SHA> --receipt <新主管 receipt> --receipt-sha256 <actual receipt SHA> --step <step> --execute`。不得略過 prior completed gate。step 內每個 exact argv、scriptPins 與 JSON probe 在 plan；內層 actual argv、cwd、輸出目錄和 probe 在 `command-recipes.json`。

product 的第一個操作將本包被 plan pinned 的所有新 scripts/maps/docs 及 exact plan 以 independent copy 存入 `workflow/evidence/canonical-plan/`，並保存 original→archived SHA 對照。後續 package_release 只需選 engine/workflow/stage 內來源即可帶上計畫證據。不可直接從此新 planning root 作 package source。

## 次數、來源與證據

| step | 預期 assertions | observations | helper builds |
|---|---:|---:|---:|
| baseline | 151 | 0 | 3 |
| ground | 735 | 81 | 1 |
| utility | 431 | 12 | 1 |
| haste | 1707 | 0 | 1 |
| versioned-slow | 1201 | 0 | 1 |
| f1 | 112 | 0 | 1 |
| pickup | 174 + 10 | 0 | 2 |
| 合計 | 4521 | 93 | 10 |

同一產品 DLL `c11678249221475fb749de41713f8ed75473980d8929d72a8cd04f2ca0b49aed`。product 與 launcher 各 build 一次；十個 helper 都在 `engine/build/r34-canonical-*`，`../../assets` 必須解析到 canonical assets。只容許 utility 已繼承的 1 個 CS8602；其餘 helper 與 product/launcher 皆 0 warnings/errors。完整四項 identity 由已接受 stage receipt 取出，置於 context/plan，不宣稱本回合有重新產生。

138 筆來源映射逐檔列明 source/destination/size/SHA/mode。其中 51 個 pickup fixture 複製到 canonical `expected/*` 與 `baseline-pins.json`，均為獨立新檔；既有兩份 metadata vectors 只核對。沒有 asset body copy 或新 junction。原 controller install 負責 180 source 與 rebuild overlay；wrapper 不重做 install。

原 stage 的四個 metadata/HintPath 調整已封存在 stage。此包另有 ground 報告的一個 stage→canonical 字串替換，保留其他 bytes/newlines，附 patch/pins。helper assertion bodies、expected、JSON oracles 不改。native collector 保留原 checks，只移除「結果檔已存在就拒絕」這個 I/O guard；所有 save 均只可新增或核對既有內容相等，以支援部分 collection 恢復。utility/haste/slow 另明確檢查原 probe 的 socket guard row。baseline 保留 29 case rows 的 116 assertions 加 35 DOM checks，沒有捏造 151 個個別 assertion IDs。

pickup 的 helper 以 canonical engine 為 Root，先寫 canonical `_integration/evidence/r34-canonical-pickup-*`，再獨立複製原 probe/PNG 到 workflow。保留原始與副本 pins；main 必須有 64 個 body/composite pixel evidence rows、32 frame pairs、213696 pixels、zero differences。supplement 10 為獨立第二個 helper。

R2 execute_once 原始 attempt 只有 `stdoutDecoder/stderrDecoder`；raw bytes 位於 attemptRoot/stdout、stderr。新相容層從 raw bytes 驗證 `rawSha256`，在不改原始 attempt 的情況下建立含 stdout/stderr `{path,size,sha256}` 的衍生 command.json，供原 stage collector 使用。兩層 argv/cwd/exit/probes 與 raw log 都保存；不以 wrapper exit 取代 native evidence。

`nested-product-command-plan.json` 明列 sealed rebuild 的 sdk/product/launcher/check-only 四個子程序，及 product PowerShell 裡的 source verify 與 dotnet build。sealed PowerShell 兩個內部命令的 streams 由 product-build 原始 stdout/stderr 共同保存，腳本逐一檢查 LASTEXITCODE；原腳本沒有各別 child receipt，這裡不捏造。原 product-build/rebuild 都未改。

## Collect-only 與拒絕重跑

每 step 的 collector 都是最後一個獨立 command。collector 失敗時，`recovery.py --step <step>` 只檢查。`--execute-collector --attempt-name collector-recovery-01` 必須通過真實 receipt gate、所有前置 command 成功紀錄、raw/probe pins，才會在 fresh attempt 執行同一個最後 collector argv；不重跑任何 build/probe/extractor。它輸出選定 attempt manifest，再交給原 controller `--step <step> --collect-only <manifest>`。如果原所有 command 都已成功，recovery 只生成原 attempts 的選擇 manifest。所有修復均保留原失敗紀錄。

實際 build/probe 失敗、inner attempt 不完整、成功外層紀錄遺失、marker 已有而外層未完成等情況會停止，交主管審核；本計畫沒有自動重試。wrapper 內若已存在完整成功 inner attempt，只能讀取/驗證及補 collection bookkeeping，不能再次啟動該 inner process。不得重新執行整個 sealed controller `--execute` step 來恢復，因 helper fresh gate 與成功 attempt 路徑均應拒絕。

原 controller 有一個非 happy-path 限制：product collect 先 exclusive-save build-log-bindings，再 exclusive-save completed/product。如果程序剛好在中間中斷，重用原 collect-only 會因 bindings 已存在而拒絕。本包不修改 sealed controller，也不刪除 bindings 來繞過；遇到才提請主管核准最小修訂「existing bindings 必須完全相等才視為已保存」，並重新封存/綁定 controller。此風險不等同本回合發生產品失敗。

## 靜態驗證與交付邊界

`static_verify.py` 只解析/compile-in-memory Python、驗 JSON/schema/路徑/來源 hashes、比對原 collector checks、確認封存 R2/stage non-assets 與 canonical baseline pins 未變。它不呼叫 execution gate、不建立 shared receipt、不執行任何 suite。完整輸出在 `static-verification.json`，plan/schema 的靜態相容性不代替新 receipt 下原 controller 的完整 gate。

本回合不建立 release、package plan、root activation、DB/services/SQL/JAR/native/GUI/socket 執行；所有 native-neutral/FIFO/variant/runtime/Slow/BIN qualification 維持 false。沒有建立 Goal、subagent 或新聊天。未來 release/入口操作仍需各自新指令。
