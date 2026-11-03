# ENGINE-NEXT-INTEGRATION-PREP-R1

狀態：`integration-prepared-awaiting-accepted-candidate`。本次完成 r29 隔離整合入口的準備與拒絕測試，未完成 r29 功能、建置、正式合入或實機驗收。

本次唯一寫入範圍為 `D:\lineage開發\TW1608262200研究\work\engine-next-integration-r1`。依最新 AGENTS.md 及派工限制，沒有查看兩位 slow 工作者的進度，也沒有寫入 engine、其他候選、releases 或主管目錄。自己的 `get_goal` 實讀為 `null`；未建立、恢復或更新 Goal。證據為 `evidence/goal-read.json`。

## 可覆核交付物

- `baseline-lock.json`：準備當下完整 source/audio/web、vendor、建置設定、既有驗收模板及入口的大小/SHA256 pins，以及資產 junction 目標與既有音訊 DLL 身分。
- `baseline/common.py`、`baseline/run_stage.py`：原檔逐位元保存，僅供比對。
- `delta/_integration/run_stage_r29.py`、`runner-delta.patch`：另立 r29 runner，保留原 151 項檢查與產品／音訊 DLL SHA 比對。原 r28 檔未修改。
- `acceptance_gate.py`：完整材料及路徑核對，驗證本身不寫檔、不啟動程序。
- `run_next_stage.py`：預設唯讀驗證；明確 `--build` 且全部材料通過後，才在本工作區的新子目錄複製獨立檔案與建置。
- `acceptance-materials.schema.json`：六種材料的 JSON Schema 定義，沒有填造任何接受收據實例。
- `preparation-seal.json`：準備程式、schema、文件與 baseline pins 的封存清單。未來 contract 必須 pin 此檔的確切 SHA。
- `test_rejections.py`、`evidence/rejection-tests.json`：拒絕測試及結果。測試替身僅為明示未接受的空 JSON、缺件及本工作區文字／硬連結，不代表功能候選或接受證據。

## 未來接受材料契約

所有 SHA 均須為確切小寫 64 位 SHA256，不接受空白、占位符、最新檔猜測或「檔案存在即合入」。JSON 重複欄位與 Windows 路徑歧義會被拒絕。Schema 定義欄位形狀；程式另強制跨檔身分、SHA、來源及路徑約束。

1. `contract`：schema `engine-r29-input-contract-v1`；revision r29；非空 candidateId；candidateRoot 必須為專案 work 下不同於本準備區的直接子目錄；preparationSha256 必須匹配本次 preparation seal。sourceManifest、candidateSeal、candidateResult、independentReview 均提供專案相對 path 與 sha256。
2. `sourceManifest`：schema `engine-r29-source-pins-v1`，同一 candidateId；每列含目標 path、專案相對 source、size、sha256。必須列出完整組合後 source/audio/web，不能只交差異清單，不能省略任何基準檔。來源僅能是對應 engine 原檔或已封存候選檔；候選來源必須列入 candidateSeal。目標不能含 bin/obj/.git；大小寫碰撞拒絕。本版不支援刪除基準檔，若新功能需要刪檔，須新派工調整並重新覆核契約。
3. `candidateResult`：schema `engine-candidate-result-v1`，同一 candidateId；checksPassed=true、實際 checks>0，確切 productDllSha256/audioDllSha256。須來自真實候選建置與測試。
4. `candidateSeal`：schema `engine-candidate-seal-v1`，同一 candidateId；files 列出候選相對 path/size/sha256，至少覆蓋來源清單、結果與所有採用的候選來源。seal 不包含自己以避免循環雜湊。
5. `independentReview`：schema `engine-independent-review-v1`，同一 candidateId；decision=accepted、candidateSealSha256 精確匹配、unresolvedFindings=[]。檔案須位於與候選及本準備區不同的 work 根下，且由真正獨立覆核產生。
6. `receipt`：schema `engine-integration-acceptance-v1`；主管 `01a17e6a-9270-7cc2-a338-0120ed68d0ec`；revision r29；decision=accepted-for-formal-r29-integration；同一 candidateId；確切 contractSha256、sourceManifestSha256、candidateSealSha256、candidateResultSha256、independentReviewSha256、productDllSha256、audioDllSha256；candidateTestsPassed/independentReviewPassed=true、unresolvedFindings=[]。檔案實體位置必須在主管目錄。dispatchId 必須為新的明確派工，不能沿用本次 PREP；authorization=isolated-r29-build-and-offline-acceptance；authorizedOutput 必須精確匹配命令列。

主管以新派工給出 contract 與 receipt 的檔案路徑和精確 SHA；這是既有工作流程的接受憑據，並非密碼學簽章或對抗可任意修改本機檔案的攻擊者。本次不產生這些接受材料。若候選原生證據格式不同，須由後續明確派工安排具來源連結的契約材料與覆核，不能改標籤冒充通過。

## 路徑及寫入隔離

輸出只允許 `work/engine-next-integration-r1/r29-runs/<全新名稱>`，不能覆寫既有輸出、輸出到 engine/releases、使用 D:/BinEngine 絕對別名、穿越路徑或沿 reparse point 寫入。即使有接受材料，本入口也沒有正式發布能力；共享 engine 合入仍需另外的新派工。

未來 `--build` 使用 copyfile 建立獨立檔案；唯讀來源即使為共享硬連結也不會原地改寫。assets 僅從已 pin 的 `engine/assets -> releases/r28/assets` 讀取、核對資產 pins 後複製到新 stage；不改 junction 或封存素材。stage 中才把 build/launch 腳本的 r28 路徑轉為 r29。common.py 保持原樣，仍從 project-manifest.json 定位唯一根與歷史 reference/development；不重建舊外部根。

建置前再次核對材料、準備 seal、基準及組合來源。內部 r29 runner 不接受直接執行；必須經入口驗證後呼叫。拒絕 Python 最佳化模式，以保留原 runner 的 assertions。未來建置過程中發生失敗時保留隔離輸出作證據，不覆寫重試。

## 本次實際驗證

可重跑命令（只執行離線拒絕測試，新增的測試檔仍在本區）：

```powershell
python -B -X utf8 'D:\lineage開發\TW1608262200研究\work\engine-next-integration-r1\test_rejections.py'
```

17 項測試通過：缺 contract、缺 receipt、r28 收據不得替代、四種材料缺件、來源漂移／缺檔、非法 SHA、Windows 路徑歧義、輸出越界、唯讀路徑規劃、禁止覆寫、硬連結唯讀、assets junction 禁寫、重複 JSON、直接 runner 拒絕、全部基準 pins／舊 resolver 不變、Python 語法。子案例不另灌水計為測試數。

尚未執行成功接受路徑、資產本文完整複製、r29 產品／launcher 建置、151 項回歸或新 slow 功能測試。未啟動正常產品、GUI、COM、遊戲、音訊播放；未查詢／修改／啟停 DB 或服務。只有本次拒絕測試有實際執行證據。

## 後續命令形狀與待合入清單

以下僅為命令參數說明，不能以占位符執行或當作接受憑據：

```text
python -B -X utf8 run_next_stage.py --contract <真實contract路徑> --contract-sha256 <主管pin> --receipt <主管收據路徑> --receipt-sha256 <主管pin> --output work/engine-next-integration-r1/r29-runs/<新派工指定名稱>
```

預設只驗證；收到新派工且材料被主管接受後，才可在同一命令加 `--build`。r29 runner 的成功結果只證明沿用的 151 項回歸及入口核對，不代表新增 slow 功能或完整原生流程已通過。

待辦順序：接受真實 slow 候選與獨立覆核 → 組合完整來源 pins → 主管提供精確接受收據與新派工 → 本隔離入口驗證／建置 → 對同一最終 DLL 完成 151 項回歸及主管指定的新 slow 驗收 → 主管決定是否另派共享 engine 串行合入。依賴伺服器的驗收仍須滿足使用者管理之環境條件；本次不處理該依賴。
