# Future accepted materials

本文件定義形狀，不是授權或 accepted sample。所有 CLI 預設唯讀；本輪不呼叫 `--build`、`--execute`、`--package`。

`run_next_stage.py` 需要 `--contract PATH --contract-sha256 SHA --receipt PATH --receipt-sha256 SHA --output project/relative/path`。未提供 `--build` 時只核驗。輸出必須是本 prep 的 `r34-runs` fresh direct child，收據 `authorizedOutput` 完全相同。

input contract schema `engine-r34-input-contract-v1` 必須有 revision r34、候選 ID、`candidateRoot`（`work/engine-pickup-presentation-rN`）、本 prep seal SHA、implementation contract SHA、exact r33 baseRevision/basePinsSha256/baseReleaseSealSha256。材料欄位 `sourceManifest/resourceManifest/candidateSeal/candidateResult/independentReview` 各為 `{path,sha256}`。path 是 canonical project-relative，不接受 traversal、ADS、reserved names 或 reparse escape。`sourceDelta` 為確切 added/changed path arrays；gate 由內容重算比對，不用「177筆」假定最終來源數。

保留 native schemas：`engine-r29-source-pins-v1`、`engine-candidate-seal-v1`、`engine-candidate-result-v1`、`engine-independent-review-v1`；其 optional revision 若存在必須 r34。source manifest 必須含每個 baseline source/audio/web，以及所有明確接受的新增 C# 和唯一 resource；每筆 path/source/size/sha256。source 只可來自 canonical baseline、精確綁定的原 r33 stage baseline，或 exact sealed candidate 根。audio identity/source 不變。

resource manifest 使用 `engine-r34-resource-manifest-v1`，candidateId 相同，`files` 只允許 schema 中那一筆確切 path/size/hash/logicalName/project/newFrames/reusedFrames。它本身也必須被 candidate seal 覆蓋。不能只提供 DLL hash 而漏掉 resource source。

review 必須 decision accepted、unresolvedFindings 空陣列、counts 包含 positive assertionsPassed、assertionsFailed0、pending0、非負 observations；這些是新 pickup review 的數字，不是既有4337。

Review binding 可選完整 flat 或完整 native nested `candidate`，兩者同時出現時必須都完整且所有值相符：

| Flat field | Nested `candidate` field |
|---|---|
| candidateId | id |
| candidateSealSha256 | deliveryManifestSha256 |
| sourceManifestSha256 | sourceManifestSha256 |
| resourceManifestSha256 | resourceManifestSha256 |
| candidateResultSha256 | resultSha256 |
| productDllSha256 | dllSha256 |
| audioDllSha256 | audioSha256 |

不要求 reviewer 為配合 gate 刪改 native proof；但以上完整 binding 必須在正式交付時具備。若原 review 需補一份 binding appendix，它也要明確封存、被主管接受，不能到 build 時暗補欄位。

主管 isolated receipt 保留 `engine-integration-acceptance-v1`，revision r34、supervisor `01a17e6a-9270-7cc2-a338-0120ed68d0ec`、decision `accepted-for-formal-r34-integration`、authorization `isolated-r34-build-and-offline-acceptance`、新 `ENGINE-R34-INTEGRATE-*` dispatch。收據需 exact input-contract/material hashes、candidate identity、product/audio DLL hashes、base bindings、authorizedOutput，以及 explicit candidateTestsPassed/independentReviewPassed true、unresolvedFindings 空陣列。必須位於主管根；本 prep dispatch 永遠不能代用。

canonical/release 另需要新精確收據，不繼承 isolated 授權。字段和 normalized reports 見 `workflow-contract.json`；不得提供未知新測試的假 accepted 數字。helper output 名稱必須列在 canonical plan 的 `helperNames`，baseline 有三個；工具拒絕直接 dotnet build/run 指向其它 helper 目錄。Script wrappers 的間接輸出也由 source review 和實際 helper DLL pins 驗證。
