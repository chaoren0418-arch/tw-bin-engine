# ENGINE-R30-INTEGRATION-PREP-R1

狀態：`prepared-awaiting-accepted-actions-candidate`。r30 版本接線與少量拒絕驗證已完成；沒有接受、建置或合入未封存候選。下一步由主管取得真實候選／獨立接受後，以精確 SHA 收據另派正式整合。

唯一寫入：`D:\lineage開發\TW1608262200研究\work\engine-r30-integration-r1`。沒有寫入 engine、根入口／manifest、releases、主管或舊 work；沒有查詢新實作者／覆核者的進度。沒有 DB／服務、正常產品、GUI、網路或音訊操作，沒有新 chat／代理／模型／平台資料變更。自己的 get_goal 實讀 `null`，未建立或更新；主管 baseline 中的 paused 不是本對話 Goal。

## 固定基底

- 基底：已接受 r29；release seal `d3ac187e7fc44745864640e4bcc72fefbaeeaa0b0e9d63de7c0c3cfa8e46273d`。
- DLL：`8ccfa4e3b5d253206779a5d4b1e59682263ffbddf5dab43fe15b0aa1f86a7397`。
- 音訊：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`，本契約不接受音訊來源或 DLL 改動。
- 素材索引：`07dd19f4407ec3f2bf61da9c00cca827cbbe206866863a87a77f17fce7a18b98`。engine/assets 仍唯讀解析至 releases/r28/assets。
- 主管 `主管/P24-緩速攻擊施法接續-20261029/baseline.json` SHA：`2a6db2201916014b44b1281aaf7b244e4ebe0ac49b0bba4c432e9c964125b104`。
- 176 項 base source/audio/web 全部實讀核對；加上支援工具／設定共 203 項，保存於 `baseline-lock.json`。沒有複製素材本文或重跑全素材 hash。

## 交付與轉接

- `acceptance_gate.py`：沿用已驗證的 r29 fail-closed gate，新增明確 r30 revision、actions candidate family 與 r29 base 身分綁定。原路徑、review、candidate seal、來源完整性、SHA 與 preparation seal 條件保留。
- `run_next_stage.py`：預設唯讀驗證；只有完整真實接受材料及新派工才可 `--build`，輸出限本區 `r30-runs/<新名稱>`。不含合入、發布或修改根入口功能。
- `prepared/run_stage_r30.py`：轉接 r29 的正式 151 checks，保留全部檢查與 DLL／音訊 SHA，調整版本、路徑及 r29 launcher→r30 的替換。直接執行仍拒絕。成功只報 `pass-r30-baseline-151-only`。
- `prepared/build-product.ps1`、`prepared/launch-product.ps1`：隔離 stage 使用的 r30 腳本。建置本文從 r29 的實際腳本轉接；只移除不適用隔離 stage 的 canonical-engine 包裝，外層完整接受 gate 仍在任何複製／建置前執行。
- `prepare_rebuild.py`、`prepared/rebuild_template_r30.py`：未來真實 stage 的 151 checks 通過後，才產生 `rebuild-overlay`。該 overlay 固定實際 contract／receipt SHA、candidateId、最終 DLL／音訊／manifest／launcher 及完整採用來源，保留以 canonical engine 為唯一建置根的限制。來源數量由真實完整 manifest 決定，不固定 176，因此可容納新 .cs。合入後重建核對已接受 r30 來源，不要求退回 r29。此輪未產生 overlay，也未安裝至 engine。
- `reference/`、`reference-pins.json`、`r29-to-r30-preparation.patch`：原腳本逐位元副本、來源 pins 及可覆核差異。原 r28/r29/舊 work 沒有修改。
- `acceptance-materials.schema.json`、`validation-plan.json`：材料欄位契約與後續測試版本適配位置；沒有接受收據實例。

## 真實材料要求

主管 contract schema 為 `engine-r30-input-contract-v1`；receipt schema 仍為 `engine-integration-acceptance-v1`。兩者都必須 revision=r30，並綁定以下三個相同欄位：

| 欄位 | 值 |
|---|---|
| baseRevision | r29 |
| baseReleaseSealSha256 | 上列已接受 r29 seal |
| basePinsSha256 | 上列主管 baseline.json SHA |

candidateId 為 `ENGINE-SLOW-ACTIONS-IMPL-R1` 或實際退修後的同 family R2/Rn，必須在原始 candidate result、seal、完整 source manifest、獨立 review、contract、receipt 完全一致。

**保留原生格式**：`engine-candidate-result-v1`、`engine-candidate-seal-v1`、`engine-r29-source-pins-v1`、`engine-independent-review-v1`。source schema 名稱中的 r29 不代表本輪版本。contract／receipt 明確指定 r30；原生材料若帶 revision，必須為 r30。沒有 revision 欄位的原生材料由精確 candidateId、原 seal SHA 與主管 contract 綁定，不要求為改欄位重新包裝或覆核。

完整來源必須覆蓋目前 176 項 base，不允許省略基準檔。可新增來源；新增檔必須來自已接受候選且列入原 candidate seal，不能以 engine 中臨時多出的檔案冒充。所有採用候選本文、source manifest、candidate result 都必須受原 seal 覆蓋；音訊來源保持原 pins。素材沒有 candidate 寫入入口。

contract 必須包含本次 preparation seal 的精確 SHA，以及 sourceManifest／candidateSeal／candidateResult／independentReview 的專案相對路徑與 SHA。receipt 的 decision 為 `accepted-for-formal-r30-integration`，authorization 為 `isolated-r30-build-and-offline-acceptance`，dispatchId 必須以 `ENGINE-R30-INTEGRATE-` 開頭；本次 PREP 不構成 build 授權。receipt 必須位於主管目錄，指定確切 authorizedOutput、contract SHA、四項材料 SHA、product/audio DLL SHA、真實 candidate 與 independent review 通過、無未解 findings。review 必須位於獨立 work 根，接受同一 candidate seal。

不能將已接受 r29 收據替代 r30；不能以檔案存在、未封存 work 內容、占位 SHA 或手填 passed 冒充接受。規則細節以 schema 及 gate 為準。新動作最終 DLL suite 通過後，overlay 的安裝、共享合入與 release 建立仍需新主管正式派工，這個準備包不授權那些動作。

## 舊 1199 suite 的適用範圍

只讀定位原封存 `work/engine-slow-review-r1/candidate-review-r2/harness`，原本文保持不變。詳細 source SHA 與位置記於 `validation-plan.json`：

- `Extra.cs:51`：`independent-slow-action-strictly-rejected-{kind}` 原屬 r29。attack／spell 只有在真實 r30 證據及獨立接受支援的 profile 範圍，才可由新正向／負向案例取代；item／pickup／drop 的拒絕仍適用。
- `Extra.cs:57`：排隊中的 slow／transient／same 回包取消條件，不因新增攻擊／施法就整段刪掉。任何替換須有具體行為契約與原 assertion→新 assertion 的對照。
- `Program.cs:36,44`：safe-or-blocked 是安全下限，容許永遠拒絕，不能證明 r30 新功能成功。需新 suite 對符合條件的動作明確要求成功、正確時序，以及不支援狀態仍拒絕。

後續正式驗收須針對同一最終 DLL：保留 151 基準、新獨立接受的 actions suite、仍適用的移動／呈現／F1 回歸。保留每個被取代的原 assertion ID、原 SHA、原版本／適用範圍、證據與替代案例。不可刪斷言後仍宣稱原 1199 全過。本次只列位置，沒有改測試預期、推測新 worker 的 helper 路徑或執行候選。

## 本輪實測與後續命令

實跑 6 項小型拒絕／契約驗證，全部通過：版本與 candidate family、精確 base／source pin、缺 review／candidate 材料、錯誤 review／candidate SHA、完整來源 coverage 與可新增形狀、缺件時 `--build` 及直接 runner 均不得產生輸出。語法檢查也包含 prepared 模板。測試沒有製造 accepted r30 收據或候選本文。

```powershell
python -B -X utf8 'D:\lineage開發\TW1608262200研究\work\engine-r30-integration-r1\test_rejections.py'
```

未來命令形狀（占位符不能當成接受材料執行）：
```text
python -B -X utf8 run_next_stage.py --contract <真實主管contract> --contract-sha256 <確切SHA> --receipt <真實主管receipt> --receipt-sha256 <確切SHA> --output work/engine-r30-integration-r1/r30-runs/<新派工名稱>
```

預設唯讀；只有新派工與真實接受材料通過後才加 `--build`。這輪建置、完整 suite、素材複製、overlay 生成、共享合入、release 建立均為 **0**。成功接受／建置路徑與新 actions suite 尚未實跑，不能把準備完成當 r30 功能或 native/live 驗收完成。
