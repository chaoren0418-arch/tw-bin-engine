# P26-RELEASE-GATE-PREP-R1

本包只準備 release tooling，exact package plan pending 真實 canonical 全部完成。本回合不建立 releases/r34、正式 receipt、假 completed/source/evidence，不改 engine/root entry/project-manifest。Goal 實讀 null，沒有建立或修改；沒有新聊天／子代理。

新版 package_release.py 只接受主管 `01a185b4-ecf6-705c-90e6-83b9eb1ba02f` 的 `engine-r34-release-acceptance-v1`，decision=`accepted-for-r34-release`、dispatchId 前綴 `ENGINE-R34-RELEASE-`、exact package-plan SHA、completedSteps 與四 identity，authorizedOutput=`releases/r34`。正式 release receipt 唯一路徑是 `主管/P25-P27-新主管接管-20261029/r34-release-acceptance.json`；shared receipt 不代替 release 批准。

SCRIPT_ROOT 為本包，PREPARATION_ROOT 保持封存 `work/engine-r34-integration-r2`。acceptance_gate.py 與 pe_resource.py 是 sealed authority-rebind 的逐檔 pinned independent copies，不修改原件；歷史 stage 主管身分保留。新版只改 release identity/root binding，並把原 completed/source/evidence 集合判斷抽成同義 pure predicates 供合成負例使用。最小 diff 在 diffs/package_release.py.patch，old→new pins 在 source-rebindings.json；所有 runtime/source 依賴由 tool-source-seal.json 鎖定，完整交付另有 artifact-seal.json。

原 package gate 全保留：completedSteps 全八項及實檔 pins、完整 source/resource/embedding、product runtime 與 launcher runtime、rebuild inputs、stage 與 workflow 證據覆蓋、四 identity、exact fresh release、no_links。封裝仍是獨立 source/runtime/evidence copies，加 13958 個 r28 immutable asset hardlinks；os.link 失敗直接停，無 copy fallback，無 asset body/attribute 寫入。封裝 main 除 verifier 的 PREPARATION_ROOT 路徑語意之外不改，不包含啟動或入口切換。

## 產生器的未來流程

`generate_package_plan.py` 預設只唯讀檢查；canonical 未全完成時回報 pending，不建立 plan。未來另有明確派工後，以實際 canonical plan SHA 與真實 shared receipt SHA 讀取 exact installed/completed/attempt/result/raw/probes/helpers/四 identity。44 操作、10 helper、預期 4521+93 不能代替這些真實紀錄。

八個 completed 真通過後，`--write-plan` 才會把 pinned tooling 獨立保存到 workflow/evidence/release-tooling，然後從真實 engine/workflow/stage 建立逐檔 package inventory。所有 accepted stage non-assets 與 seal、workflow 原 gate 所要求的全證據、canonical build/helper/native pickup 證據、source/rebuild inputs/runtime 都列出；之後新增的 provenance 也必須先放入 workflow 再產生 plan。計畫輸出限 fresh `work/P26-r34-package-plan-rN/package-plan.json`，避免把 plan 放在被自己盤點的 workflow 造成自我循環。

```powershell
python -B -X utf8 'D:\lineage開發\TW1608262200研究\work\P26-r34-release-gate-r1\generate_package_plan.py'
```

未來生成的完整命令格式（本補件回合不執行）：

```powershell
python -B -X utf8 'D:\lineage開發\TW1608262200研究\work\P26-r34-release-gate-r1\generate_package_plan.py' --canonical-plan <ACTUAL_CANONICAL_PLAN> --canonical-plan-sha256 <ACTUAL_PLAN_SHA> --shared-receipt-sha256 <ACTUAL_SHARED_RECEIPT_SHA> --write-plan 'D:\lineage開發\TW1608262200研究\work\P26-r34-package-plan-r1\package-plan.json'
```

生成 plan 不代表可發布。主管另接受 exact package plan、completed record pins、四 identity 後，才可核發 release receipt。使用本包 package_release.py 先不帶 --package 執行完整 readonly gate，核准後同一 exact inputs 才加 --package。任何 partial release 留原樣，不刪除、不自動重跑、不覆寫封存根；回報新主管。

## 驗證與限制

`verify_tools.py` 只做靜態最小 diff、來源 pins、原 engine/root 文件保持不變及 in-memory predicates。14 個合成案例涵蓋新／舊主管、錯 plan、shared receipt 誤用、缺 completed/source/stage/workflow evidence；不建立任何假 canonical record。實際 readonly rerun：

```powershell
python -B -X utf8 'D:\lineage開發\TW1608262200研究\work\P26-r34-release-gate-r1\verify_tools.py'
```

這個 preparation verifier 的 baseline 不變／pending 斷言只適用正式 canonical 開始前；正式整合後用 generator 的真實 evidence 驗證，不把舊基準變更誤判成工具篡改。單純工具封存可逐檔核對 artifact/tool-source seals，不需要重跑產品。

## 入口與回復待後續派工

release 封存後先用原 immutable verifier 核 exact release seal/full SHA；由主管獨立接受後，才能另訂 root entry/project-manifest 的最小切換契約。先保存原 root entry/manifest 獨立副本、size/SHA 與新映射 diff，保留 r33 歷史及所有 workflow/before 原件。切換或回復均須新精確授權與新獨立暫存檔替換，不寫穿硬連結、不修改 sealed release。check-only 不是 native/UI/登入/遊戲驗收。本包只描述此流程，不提供 activation mutation 命令。

DB／服務／socket／GUI／native／角色及產品 build/suite 均未執行。工具準備完成後等待／接續明確派工，不把 release preparation 與 shared canonical 執行權混同。
