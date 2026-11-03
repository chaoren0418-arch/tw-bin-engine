# R2 material contract — not an accepted instance

目前candidate與review均封存且相容；仍缺實際主管formal input contract/receipt。此文件只有格式要求，不建立accepted sample，不授權build。

`run_next_stage.py` 預設唯讀，參數仍是 `--contract PATH --contract-sha256 SHA --receipt PATH --receipt-sha256 SHA --output project/relative/path`。只有future新主管收據接受exact R2 contract/preparation seal及fresh `work/engine-r34-integration-r2/r34-runs/...`，再明確提供`--build`，才能建立stage。R1 prep dispatch／review acceptance本身不能代替root receipt。

保留native candidate schemas：source `engine-r29-source-pins-v1`、seal `engine-candidate-seal-v1`、result `engine-candidate-result-v1`。Native source可180，不能限制177；sourceDelta必須等於實際10changed/3added。Audio、r33 base/final binding及所有來源隔離規則不變。

Input contract仍為 `engine-r34-input-contract-v1`，revision r34、candidateId/root、R2 preparationSha256、implementationContractSha256、r33 base bindings，以及材料references `{path,sha256}`：sourceManifest、candidateSeal、candidateResult、independentReview。

R2新增／更正：

- `resourceManifest` 是沿用contract欄位名稱的「原resource material reference」，不是要求author產出新manifest。需 `{path,sha256,format}`；此真候選用 `format: native-projection`，path指向原sealed resource-projection.json。`strict-manifest` 才使用原自訂schema；兩者皆有嚴格語意驗證。
- `independentReviewRoot` 必須是 `work/engine-pickup-presentation-candidate-review-r1`。明確allowlist和exact review hashes在review-root-policy.json，不能只用prefix或任意work根。
- `independentReviewSeal` 為原review artifact-manifest的 `{path,sha256}`。兩份review材料必須位於上述exact root，接受文與seal都要與主管提供hash相符。
- 原review無resourceManifestSha256、counts、unresolvedFindings，不要求加欄位。來源manifest與candidate seal已覆蓋原projection/resource，gate比對實體資料後derive resource證據；原review source/seal核心身份均必須匹配。Native checks/assertions與findings/rows/coverage經完整核驗後才derive normalized counts/unresolvedFindings。

Root receipt仍是獨立主管材料，schema `engine-integration-acceptance-v1`、decision `accepted-for-formal-r34-integration`、authorization `isolated-r34-build-and-offline-acceptance`、新 `ENGINE-R34-INTEGRATE-*` dispatch、主管ID、exact contract/source/seal/result/review hashes、base bindings、product/audio hashes、authorizedOutput與explicit pass flags。`resourceManifestSha256` 不再必需，因exact contract已綁原material SHA且candidate seal覆蓋；若root選擇另給此欄或resourceProjectionSha256，必須精確一致。

R2不產生這份root receipt。Native review所需證據檔名／shape與schema已核5份真sealed文件；gate不將「read-only compatibility pass」當formal acceptance。未知／缺少必需身份、非空repair或blocking findings、row/count/coverage矛盾、來源漂移一律拒絕。

Future rebuild保留 `acceptance/resource-material.json`（原文件exact bytes）、`normalized-resource.json`（derived）、`independent-review.json`、`independent-review-seal.json`、`normalized-review.json`，各被rebuild lock封存。Compiled DLL另核PE embedded resource；package仍包含resource source、csproj、原證據和衍生映射，不漏pin/copy/embed/package。
