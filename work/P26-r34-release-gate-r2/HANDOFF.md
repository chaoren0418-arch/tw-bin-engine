# P26 release gate r2 — RG1

只修復獨立覆核 RG1 的 inner/nested evidence closure，不修改已接受 canonical、原 sealed r1 或產品／suite／expected。r1 完整 20 個 artifacts 加 seal 獨立保存於 historical/r1。原 package controller、asset hardlink/no fallback/no_links、completed/source/runtime/evidence coverage gate 均 byte-identical 保留；新 release authority policy 只重新綁定 SCRIPT_ROOT、前版 seal 及本版依賴 pins。

result_evidence.py 先讀 accepted canonical plan pinned 的 command-recipes 與 nested command plan。逐項驗證 exact inner operation membership/order、rawAttempt/stdout/stderr/probes 的原 size/SHA、成功/完整/產品 identity、argv/cwd、decoder/raw SHA、exact probe set。product 的 sdk/product-build/launcher-build/same-entry-check-only 四個 nested command/raw pins 及 name/exit/argv/cwd，必須與原 product result command rows 一致。

它另讀原 outer marker/probe JSON 內的引用 pins。pickup marker 內保存的原始／副本 PNG/JSON pins 形成歷史錨點，pixel-evidence 的 64 列 PNG 必須吻合這些 pins；不靠重新計算現檔 hash 把漂移變成通過。一般 current report/marker/probe 的 `{path,size,sha256}` 引用逐筆核對，限定 engine/workflow/accepted stage 根並拒絕 reparse 或衝突 pins。歷史 archive snapshots 的 before-state 不當作現在檔案狀態遞迴解讀。

completed_context 在任何 archive/plan write 之前要求 closure 通過；generate 再檢查一次才 archive。inventory 對已有歷史引用的檔案沿用原 pins 並驗證，不替換成新的現檔 hash。新 package plan 會包含 canonicalEvidenceClosure；其他流程仍是先主管接受 exact package plan，才能核發真正 release receipt。PowerShell grandchildren 共用原 parent streams 的已知限制保留，沒有捏造獨立 child receipt。

32 個 private synthetic cases 涵蓋 unchanged positive、各 rawAttempt/stream/probe/PNG 被改或缺檔、缺引用/缺 operation/多 operation、失敗/partial/錯 identity/argv/cwd/decoder/probe set、nested缺失/重複/失敗/錯argv/cwd/raw pin。沒有啟動子程序，沒有建立正式 receipt 或 canonical completion fixture。所有單位資料明標 synthetic，保存在本包 synthetic-unit-only；不當作產品斷言。

真實 canonical 的唯讀驗證同時通過：28 inner、44 outer markers、4 nested、64 pixel rows，共 690 個歷史 reference pins。已完成的 4521 product assertions +93 observations 是先前受派工的 canonical 真執行結果，沒有因 RG1 重跑。詳見 actual-evidence-closure.json 與 verification.json；新版仍待 P26 獨立覆核。

唯讀重跑：

```powershell
python -B -X utf8 'D:\lineage開發\TW1608262200研究\work\P26-r34-release-gate-r2\verify_r2.py'
python -B -X utf8 'D:\lineage開發\TW1608262200研究\work\P26-r34-release-gate-r2\test_result_evidence.py'
```

verify_r2 的「archive/plan 尚不存在」是此準備階段的檢查；未來正式生成後應用 actual generator gate 驗證，不重用準備階段 pending 斷言。source/tool/artifact seal 可獨立逐檔重驗。

後續須主管精確綁定此版工具並交 P26 獨立覆核，才可使用 generate_package_plan.py 的 --write-plan。正式輸出仍限 fresh work/P26-r34-package-plan-rN/package-plan.json，且先把本版 pinned tooling 存入 workflow/evidence/release-tooling。原 r1 generator --write-plan 維持停用。本回合沒有 archive、package plan、releases/r34 或入口變更。

Canonical shared receipt、author/review來源與主管接受資料已保存於 workflow/provenance；主管真實 canonical acceptance/root verification 另在 workflow/evidence/provenance，會由完整 workflow inventory 帶入。額外完整review副本有一個非必要合成fixture存取拒絕，required provenance齊備、partial與錯誤保留，詳見 canonical HANDOFF/result；不改原件權限或歷史 reports。

Goal 實讀 null，未改平台狀態；沒有新聊天／子代理、DB／服務／GUI／socket／native／角色操作。封存後停寫，等待精確新派工。
