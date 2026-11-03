# P24 主管安全交接

狀態：兩個有界工作及主管覆核均已完成，所有既有worker均idle並停止寫入，已到安全主管切換點。後任尚未指定；由父監督指定單一後任承接。此文件不代表正式產品完成。

## 切換指示與 Goal

使用者要求清除舊主管 Goal，待目前有界回合結束後，由父監督另建新主管與新 Goal；子任務維持獨立。舊主管不得自行建立新主管或 Goal，也不得再派下一輪研究、擴充或整合。

舊主管 `01a1748a-d237-71a0-9323-60ad8462794c` 自 **2026-10-27 21:34:25 Asia/Taipei** 停止新派工。其後訊息僅用於既有任務必要收束、覆核與交接修正，沒有新整合回合。

實際 `get_goal` 回傳 `goal: null`；讀取原文見本目錄 `cutoff-and-goal-read.json`。沒有重建 Goal。較早一次即時讀取仍為 blocked，嘗試 create_goal 因未完成目標而失敗，原文另保存在 `主管/P24-持續開發與同版驗收-20261027/r27-effect6016-minimal-contract-review/current-goal-tool-receipt.json`。兩者是先後不同時點，不得用舊 blocked 覆蓋現在已清除的結果。

## 原目標與完成定義

原整體方向是依同版原始來源完成可運行、可重建的自主 BIN 客戶端及本機網頁引擎；P24 延續原 2x2 地圖快取與跨區載入，補齊正常登入／選角／進入世界、移動／戰鬥／技能、物品／裝備／拾取、跨圖與內容、技能學習／商店／交易／倉庫，以及連續遊戲後保存重登。主管獨立覆核與真實操作，不能把單模組或固定腳本通過當成整體完成。

完成需要來源與產品行為對得上、固定版本完整串接、可重建交付及明示限制。原生完整畫面／滑鼠未驗、僅四種技能與有限內容、事件及來源缺項仍須各自記錄。舊 r20–r22 實測不得改標為 r27；研究契約、離線替身或播放指令成功也不等於真實遊戲／可聽聲音驗收。

使用者原則：有可自主完成的開發時，不可因等待人工驗收而全部停工；只有全部既定實作完成且確實只剩人工時才可等待。正常登入到遊戲操作應走完整流程，不能只跑固定測試腳本；發現問題須修正後驗證。不得用增加對話數或重跑已通過項目製造進度。

## 當前正常產品

- 正常版本：r27，根目錄 `D:/lineage開發/TW1608262200研究/P24-原2x2地圖快取與跨區載入/reference/development/release-integration-r11/delivery`。
- 正常入口：上述目錄的 `啟動新版客戶端.cmd`。其內 `source/`、`web/`、`assets/` 與 `_integration/launcher-source/` 保留可重建材料。
- `_integration/candidate-seal-manifest.json` SHA：`a333cc86191f23bc72590f5b2d6f2722339f8a90e769b64c1a2315d3b8e66f07`，15941 檔。
- 產品 DLL SHA：`1fdba73b5d52e6fb6b58e4fa1c9f18bfd8175cb31ac671c08ee819ce7062f4b1`。
- manifest SHA：`e0addbfd014cd4952356019a50fedad2c8bd8438428eac4f60d2a9ee68b3eda3`；資產 integrity SHA：`5e9afd70a579b0ae39e8fcf88103b5d35f89c423a834406300ea67555450e9df`。
- 根主管已獨立核對完整交付檔與132個compiler inputs，獨立重建產品／launcher；見 `主管/P24-持續開發與同版驗收-20261027/r27-package-root-review.json`、`r27-launcher-root-review.json`。
- r26 的原使用者指定 CMD 曾修復 PowerShell 找不到問題；修復保留在 `release-integration-r10/delivery`，不要還原舊 CMD。其原 seal 已因兩檔修改及四檔新增而被修復收據取代，不能宣稱舊 seal 仍覆蓋所有現檔。當前以 r27 為準。

已驗：r27 正常入口、原生鍵盤登入／選角／進世界、聊天開關、草稿保留與單次原服回傳、輸入隔離、Escape／F10 焦點、移動、正常取消與保存重登；真實場景 PNG 已查看。證據在 `主管/P24-持續開發與同版驗收-20261027/r27-targeted-live/current-status.json`。

未驗：同一 r27 的完整連續戰鬥／技能／物品／服務／跨圖閉環、完整原生視窗／滑鼠／HUD互動。先前原生擷取恢復已失敗，不能重複相同失效程序、猜座標、把場景PNG當完整視窗或要求已確認開螢幕／未鎖定的使用者再證明一次。

權威進度為 `主管/完整引擎完成矩陣-20261026.md`、`主管/完整引擎未完成項目.json`、`主管/P24-持續開發與同版驗收-20261027/r27-completion-matrix.json` 及 state/checkpoint 的 currentVersionSummary；歷史 P22/r16–r22 命名欄位保持歷史。全 P24 未完成，不能將封存套件誤稱正式全案 freeze。

## 6016 來源工作與音訊候選

本輪研究的 input SHA 為 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，ImageBase `0x400000`。P01 原 IDB 為 `D:/lineage開發/TW1608262200研究/P01-分析基準/ida/03-ascii-isolated/TW1608262200.idb`，255188143 bytes，SHA `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f`。只能將原 BIN／IDB 視為唯讀。

已有來源工作：`r27-effect6016-event-source-r1`、`r27-effect6016-existing-source-locator-r1`、`r27-effect6016-targeted-export-r1`；目前收束於 `r27-effect6016-minimal-contract-r1`，均位於 P24 `reference/development/`。前輪四函式及本輪六批17函式／877 heads已獨立核對。新函式證明候選是音訊資源，先載入 `sound/<ID>.mp3`，失敗再試同ID `.wav`，透過 mss32midi quick-copy／quick-play。不是同號 child SPR。

6016 序列是 `[11,0,1,...,10]`，每步5 tick；事件位於 sequence index1，候選槽 `[3332,1340,1340]` 只選一槽，不去重、不全播，不截斷成byte。mode0精確呼叫點為 A3FCB9；A3FBFD屬mode1。一般初始化使用 A3EF40(0)，一般換幀／循環使用 A3EF40(1)。已定位 UserObject 建立／註冊與slot清理分派，但原6016實際periodic updater、highlight表成員與底層A053E0 loader仍未恢復。

主管允許以現有24Hz自有時鐘及明示generation／跳幀政策形成最小契約，不要求恢復原整套排程或資源生命週期；不能將此稱為原生完全等價。可實作與限制以最終 `contract.json` 及主管覆核為準。

音訊候選在 `reference/development/r27-6016-audio-module-r1`，含真實3332.wav／1340.wav與Windows winmm/MCI播放模組。兩檔均為單聲道22050Hz、16-bit PCM，來源與封裝bytes已由主管獨立核對；SHA分別為 `973e0a3598edd23c2a24f6900643e43a9155fe36906b939d2d8e24374abeff2a`、`30b4ce04d5ca914cdbfd80c7ee9b9cdbe3bf79487e609b2a9d32f6546a90f9c3`。本輪不實際播放、不開音訊裝置。主管獨立重建DLL與候選一致，19組離線替身案例通過；native播放／裝置仍未驗。

以上均為**未整合候選或來源證據**，沒有修改正常r27入口、source、assets或seal。未進行新native音訊／遊戲驗收。下一主管可在覆核接受後安排事件實作及單一整合者串行整合，再由唯一實機操作者驗證；舊主管本輪不啟動該下一階段。

## 工作者與唯一寫入權

| 身分 | 對話／目前或最近回合 | 產物與當前狀態 |
|---|---|---|
| 舊根主管 | 01a1748a-d237-71a0-9323-60ad8462794c | 唯一主管目錄寫入及實機操作者；本輪只收束／覆核／交接 |
| 來源工作者 | 01a1749a-7e8d-7200-9fc8-8618ed2559ac；回合01a17d7c-0967-7073-bbb2-4811abd43e2d | r27-effect6016-minimal-contract-r1，已覆核／idle／停寫；獨占D:/P24Lab-6016-Export-r1隔離IDB及其minimal-contract-r1批次目錄 |
| 音訊工作者 | 01a17a21-8dec-7410-8a1b-80f7fa84d0a1；回合01a17d87-2e14-7381-b1d1-aca4865a36db | r27-6016-audio-module-r1，已覆核／idle／停寫 |
| 唯一整合者 | 01a17a22-17e4-70c0-9792-166ddb86ea42；最近完成回合01a17cff-3845-7dd2-b8e1-16c38c2bb539 | r27已整合完成，當前idle，沒有新整合派工 |
| 父監督 | 01a17953-d39f-7771-a4e7-7d84b1c0b038 | 安全換主管協調；後任ID尚未指定，舊主管不自行建立 |

目前 research/AGENTS.md 仍指舊根主管為唯一協調者。接管需父監督確認舊主管與worker停寫，再以使用者授權明確指定新主管並更新協調身分；不得兩個主管同時改state/checkpoint、派同一worker或操作實機。worker只寫自己的候選根；正式shared source/web/assets/build與正常版本由新主管安排唯一整合者串行修改。

## 服務、角色與操作限制

唯讀服務觀測見本目錄service-observation.json：java PID88012、遊戲port42061；mysqld PID59052、127.0.0.1:43324仍在。保留原實驗服務，不重啟／停止。遊戲長存exec session42878不要終止；原JAR SHA `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。

最新已驗證實機收尾為兩角色離線、客戶端正常關閉；本輪來源／音訊候選工作沒有再次登入。服務驗證1900249001與透明觀察乙1900240006已存在，使用者新增角色授權已執行，不能重建／重配物資。保留既有角色、金幣、裝備、歷史死亡與鑰匙自然過期；不得改DB／授物製造通過。憑證僅在D:/P24Lab-ClientEngine/private-tests.json與private-service-test-role.json等既有私有檔，禁止打印密碼。

保留未完成範圍：F4真實extended L8 group關聯缺證；其他事件token與未封裝內容未完成；NPC正向收購目前原服價格全停用，私商正向回購缺匹配資源；歷史鑰匙精確刪除時刻不可補造；封存原BIN漏洞／完整callback可達性研究不得重啟。不重掃已完成24760筆F4清單，不因相同未知處重跑已通過套件。

本輪與後續都不得修改原BIN／原IDB、下載安裝／接受新工具授權／上傳資料／改安全設定／干擾現有應用；遇實際拒絕保存精確錯誤，不繞過。所有實機、遊戲、角色、服務操作只可由一位主管負責。必要開發可自主完成，但此安全換主管安排明確要求舊主管在本輪交接後停止新派工。

## 最終接管收據

已核：三位worker皆idle；兩位本輪worker已明確停止寫入，專屬候選目錄可由後任安排接管。根主管只完成本輪必要覆核，沒有派新研究或整合。IDB task-owned程序數為0，Goal最後實讀仍為null。

安全切換收據時間：2026-10-27T21:45:12.017948+08:00。source root-review核對128個artifact pins、六批IDB hash鏈及原IDB不變；audio root-review核對48個pins、兩個原WAV與獨立重建同DLL、19組離線測試。正式r27 seal檔SHA仍符合原pin，未整合候選。

- 6016 contract SHA：f4fdfa8f342f58afed926142a5763d75b286644e2be7d3d628ddb1f98efd70aa
- 6016 result SHA：756bcf4337354fefe7c21bfa9ad3a6231ef0bd5314b0bd31c195b88b5535d5eb
- 音訊 result SHA：50e19c866542392eb5fb3b005e6af9532aa9ec854108a9f6c55820d0f0981da3
- 音訊候選manifest SHA：21661fa5d7940069f19fda18a300274bf0c06b3f0e97936689556b4ce38cb94a
- 根主管來源覆核：D:\lineage開發\TW1608262200研究\主管\P24-持續開發與同版驗收-20261027\r27-effect6016-minimal-contract-review\root-review.json
- 根主管音訊覆核：D:\lineage開發\TW1608262200研究\主管\P24-持續開發與同版驗收-20261027\r27-6016-audio-module-review\root-review.json

`handoff.json` 保存所有worker回合／cursor、產物pin、下一個可交付成果與接管規則。`final-worker-and-goal-snapshot.json` 保存實際工具回應。後任指定前，無人應推定已獲共享寫入權；舊主管在本輪交接發布後不再派工或寫入。
