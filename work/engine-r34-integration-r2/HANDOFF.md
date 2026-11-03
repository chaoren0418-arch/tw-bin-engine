# ENGINE-R34-INTEGRATION-PREP-COMPAT-R2

R2已修正r1的真實格式接線問題，原r1與candidate/review均保持immutable。本輪只寫本r2根，沒有產品build、SDK、stage、asset links/copies、正式suite、shared writes、入口啟用、DB/GUI/socket/native/JAR/services、Goal修改或新chat/subagent。

21項有限Python相容性測試通過，raw stdout/stderr與argv在 `evidence/compat-tests`；JSON Schema Draft202012驗證5份實際sealed native文件通過。`evidence/native-compatibility.json` 是唯讀相容性核驗，不是formal acceptance或stage結果；實際主管input contract／receipt尚未提供，formal gate仍關閉。

## 已核的精確交付

- Candidate `work/engine-pickup-presentation-r1`：delivery `cc7761665593c79b9f82255f5d361bedd6c8ac4563434e7cf7a6e07c5c16cbdc`；355 artifacts、412 source/input pins。
- Source manifest `337afaa3607e49e0c50f2f388ab8cec8c42f7816e33ed2ee98df6bfb0a48b533`；180＝177基底＋3新增，167未變、10修改。完整差異路徑在audit `sourceDelta`。
- Candidate result `05983d2858d22a9b8420fdcbe17a03415445ff9315d131be740297e1c1cc5c88`。作者402不加入獨立184或既有4337。
- Native resource projection `d71b951e7117bfd05847694266f28a5b209ff54d722cd1ed8dff462efceb6064`。
- DLL `c11678249221475fb749de41713f8ed75473980d8929d72a8cd04f2ca0b49aed`；本輪PE/CLR解析實際resource stream，沒有Assembly.Load／產品執行。logical name `P06.Resources.PickupOwnBase15Gfx0Weapon0.json`，239766bytes，SHA `43b88cec329929484610aceae84566d1a922eb4b8ceba93e17dc44b65922ca90`。
- Review exact root `work/engine-pickup-presentation-candidate-review-r1`：artifact `715c95654af8a1c59853f15e41c3511d5b15c1b0749bf35c47e39fe918b99196`；acceptance `5ba17f25a978b146a282f14fce8f477a60307303451ded1b9bca916b56411d4d`；result `81481f6fc05d19baea5af17a1338638c5d038708182961db47ef4864d063cb0e`。414 artifacts、847 source pins核驗。

## 修正方式

`compatibility.py` 的 native projection分支不要求author另造 `engine-r34-resource-manifest-v1`。核projection在candidate seal內，resource path與source manifest一致，bytes/hash一致，32個direction/index唯一且與已資格inventory逐項相同；實際resource JSON的8directions／4steps、24新frame canonical hashes／8 reuse、source身份、base profile、csproj無條件exact LogicalName均相符。嚴格自訂manifest保留為另一個明確format，不能以任意JSON代用。

gate只在記憶體產生 `normalizedResource`，包含原projection path/hash、candidate seal、source manifest、csproj SHA、32 mappings與 `derived:true`。本輪audit可在r2 evidence記錄這份映射；future authorized stage/rebuild才將原文件與normalized descriptor分開封裝。沒有寫回candidate，也不冒稱原review自帶resourceManifestSha256。

Review root由input contract的 `independentReviewRoot` 精確指定，並比對本包 `review-root-policy.json` 的主管授權exact sibling、seal與acceptance hash。拒絕舊prep root、其它work結果、prefix相似root和candidate自身；保留路徑與reparse隔離。

Native review分支驗頂層checks/assertions、observations/failedChecks、blockingFindings、repairRequired，並驗 `findings.json` 實際blockingFindings[]／repairRequests[]。由兩個sealed accepted probes重組184行，逐一對execution aggregate的ordinal/ID/run/localOccurrence，核實際helper receipts與hash、24risk coverage/disposition、source pins、candidate和final r33身份。不是只信摘要數字。正規化的counts/unresolvedFindings都標明derive來源，不要求review重封。

`acceptance-materials.schema.json`／`MATERIALS.md`更新這兩個明確native分支。`run_next_stage.py`、`prepare_rebuild.py`及rebuild template會保留原resource material、原review seal／acceptance及衍生normalized records；package透過完整rebuild lock/source/runtime/evidence規則保留全部。Self-root均由r2 `__file__`決定，r34-runs/r34-shared在r2，不能回寫r1。r1原件只作pinned reference，差異在 `correction-index.json`／`corrections`。

## 未來184 suite的實際來源

`pickup-suite-snapshots.json` 綁兩組實際執行source snapshots：

| group | assertions | actual helper SHA |
|---|---:|---|
| execution-builds/main-174 | 174 | 5859a7b0eb8ed4846ca4a4c7854e9ec6fc93f4c633fe01f3ae2ae6b459e8cbba |
| execution-builds/supplement-10 | 10 | b232da5d874cecd4c6a8916c30d1efb759607a1a8ba3769c839df4264188df83 |

本包只獨立複製其cs/csproj/receipt與必要metadata，沒有複製DLL、PNG或asset payload。不得用最後harness替換main早期body；不得直接用 `run_probe.ps1` 的原candidate hash gate作canonical runner。保留174＋10順序、184 ordinal/IDs、functional assertions；只允許必要csproj HintPath、輸出路徑及產品hash metadata資料接線，保存逐檔diff。

`pickup-fixture-plan.json`列51個sealed fixture metadata pins（3030545bytes，現在copy0）：future Root綁當次產品根，Root/assets與helper AppContext ../../assets指向同一所屬素材，expected/*／baseline-pins以fresh或exact既存pins處理。Stage helpers在stage/build/r34-stage-*；canonical在engine/build/r34-canonical-*。不建新素材junction、不複製整套14k素材。

## 保留的gate及限制

既有4337＋93不變；accepted新review為184＋0。因此future同DLL驗證計畫是4521 assertions＋93 observations，但本輪executed0。舊Weapon46／OwnCombat-only兩個ground測試原body/expected不改；P01/P02已由這份封存review具體覆蓋。Native neutral/FIFO/variant/runtime timing/Slow/BIN同版資格均仍false。

沿用r1的exact source/resource pins、fresh out、canonical guard、stage/assets與engine/assets helper配置、actual product/launcher log binding、可見UTF8 replacement策略、failed/partial attempt保存與collect-only不重跑語意。Baseline collector／stage/canonical/release runtime尚未執行；後續仍需精確主管receipt及正常stage/full/canonical/release授權鏈。

R1 186prep/189artifact、r33 177＋28、130refs/18immutable/1837old/696r33/44qualified pins保全。本輪讀Goal為null，未新增／修改Goal。封存交付後停止寫入，等待主管下一個精確派工。
