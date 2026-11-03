# ENGINE-PICKUP-PRESENTATION-CANDIDATE-REVIEW-R1

獨立結論：accepted。只接受主管明訂的 OWN-BASE-15-001-SUPERVISOR-R1 引擎呈現規則；沒有阻擋缺陷或退修要求，候選與共享產品均未修改。

184個實際獨立斷言全部通過（主測174、補測10），觀察0、失敗0。作者402與prep84未計入。24項prep風險均在coverage.json逐項處置；覆蓋項目數不可再次加總成斷言數。開發中71列／3個helper例外的舊run完整保留但不計入。

64個主像素案例涵蓋8方向×2速度×4影格；合成像素共213696，角色與合成channel差異0。另有1組合成遮擋。真正的candidate S30→ProjectileReport→flight→hit證明：未compose的到期命中阻擋receipt、中途命中取消pickup、同一now重複compose保持圖元／像素／report數一致。這三項沒有注入hit cache。

355候選產物、412候選外部pins、847本覆核pins均核對。finalr33的177來源對應167不變、10修改、3新增，候選共180。Ground sender/completion本文位元組一致；focused ground/item owner completion也通過。未重跑完整4337舊套件。

候選seal：cc7761665593c79b9f82255f5d361bedd6c8ac4563434e7cf7a6e07c5c16cbdc

候選DLL：c11678249221475fb749de41713f8ed75473980d8929d72a8cd04f2ca0b49aed

review-acceptance.json使用engine-independent-review-v1，頂層具有candidateId／candidateSealSha256／sourceManifestSha256／candidateResultSha256、final baseline／contract／checks／observations／product DLL SHA。

重驗：在本目錄執行 `python verify.py`。重跑已封存helper：`powershell -NoProfile -File .\run_probe.ps1 -Suite main -RunId main-recheck-01` 及 `powershell -NoProfile -File .\run_probe.ps1 -Suite supplement -RunId supplement-recheck-01`。輸出僅寫新reruns子目錄，不覆蓋封存證據。helper使用相同候選DLL、fixture和離線方法；不啟動正常產品入口。不得再執行prepare.py或finish_review.py改動封存內容。

資格界線：合成封包、注入時鐘、memory gate、部分reflection、實際candidate ComposeObjects/BitmapOf與接受的原始圖素；不是實機。nativeNeutral／FIFO／variant／nativeTiming／Slow資格／同版BIN仍false。無GUI、socket、nativeBIN、DB／服務執行。缺失resource以靜態路徑與exception seam補足，未宣稱所有parser fault分支動態執行。

本工作Goal讀回null，未建立或改動Goal。僅寫入本派工根；等待唯一主管驗收與下一派工，不自行整合或發布。
