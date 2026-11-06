# Issue #97 執行報告

任務為 T033 可攜 zip 素材來源設計，第 1 次嘗試。盤點日期為 2026-11-06，基準 `origin/main` 為 `0fab537415a84943322e9f542d8d16f6abe34914`，工作分支 `task/97`。

## 結論

完成素材盤點、A／B／C 比較、建議架構、zip 白名單、README 草稿與三張首批後續任務。建議採 C# 本機準備與原檔 provider 的混合方案。**完整 client-only 素材生成鏈仍受阻，T033 與 U6 尚未完成。**

來源缺口包含伺服器 terrain／SQL 技能與時序、Text 容器版本差異、Text 解碼查表，以及部分生成步驟缺乏可重播工具。現有 repo 還追蹤 22 個 web 遊戲衍生 PNG，後續要改成本機生成並處理追蹤範圍。

完整移植靜態估算為 20–36 小時，超過 U6 原估 +1。plan JSON 遵守三項上限，每項 2 小時，交付來源查核、全 repo 發布資源審查與準備器核心切片。**這是首批拆分，完整轉換／接線／發布的任務尚待 Claude 依查核結果續排**，不能把三項完成當成 M3 完成。

## 做了什麼

- 先讀主專案 `AGENTS.md`、`docs/REPORT.md`，再讀新工作樹規則、TASKS／PLAN、ARCHITECTURE、P01–P24 盤點索引與素材相關歷史交接。
- 使用者開頭要求在主目錄 checkout，任務內另明定主目錄留給 #77。本次採後者的具體隔離要求，執行 `git fetch origin` 後，以 `git worktree add scratch/wt/task-97 -b task/97 origin/main` 建立工作樹。
- 透過主目錄既有 `engine/assets` junction 唯讀列檔、讀 integrity，追查 P04／P05／P06／P22／P24 的工具、主管地圖清冊、版本決策與 work/r29 延續記錄。
- 設計文件逐類列出原檔、產生器、外部依賴與缺口。附錄包含 19 類代表檔、原客戶端實測 pins、歷史容器 pins，以及 69 份工具／來源證據檔指紋。
- 全部任務交付只新增三份文件。一次性統計、附錄產生與文件驗證腳本放在根目錄 `scratch/issue-97-*.ps1`。

## 怎麼驗證

### 已驗證

1. 素材 junction 指向 `releases/r28/assets`。integrity SHA 為 `07dd19f4407ec3f2bf61da9c00cca827cbbe206866863a87a77f17fce7a18b98`，schema 為 `p06-assets-integrity-v1`。
2. 索引共 13,957 筆、19 個頂層分類，無重複 path。分類實體檔數逐類相同，總本文大小 2,222,723,085 bytes。索引本身另計一檔。
3. 每類選一個代表檔重新算 SHA，19／19 與索引吻合。設計表涵蓋全部 19 類，未找到獨立生成步驟者明列「來源不明」。
4. 唯讀重算主 Sprite、Text、Tile 六個容器檔、兩個 map0 S32、兩個 WAV 的 SHA。主 Sprite 與兩 WAV 符合既有 pins。本機 Text.idx 為 `43a342ad3929b541096630165c04794192c48359ecbf116b538eaffc49741f6f`，Text.pak 為 `9516a372c43b269cf1af7adc0ac00abd4163e1235dcd846e34294849616a37ff`，皆異於歷史解碼器鎖定版本。
5. magic/catalog.json 與 `DEV/skill-casting-r1/generated/magic/catalog.json` 本輪 SHA 相同，生成器從 SQL 選技能 1／3／4／8。此項已定位，仍需解除 SQL 依賴。
6. `git ls-files` 確認 `engine/web/icons` 20 PNG、`engine/web/ground` 2 PNG。唯讀腳本顯示圖像來自 TBT／SPR 轉換。
7. 驗證 plan JSON 是陣列，三項均有 id／title／stage／priority／text／depends_on，id 唯一，stage 為 test／code，依賴存在且無環。每項含工作範圍、驗收、2 小時預算與禁止事項。
8. 文件驗證核對設計表與索引分類集合、19 類檔數、附錄 69 pins、UTF-8、`git diff --check` 及提交檔案白名單。`docs/REPORT.md` SHA 保持 `8c4ae7cb0bc98129365ecdfc81f5b1e1d4bacff9acd7661164f128b61f810b99`。

### 推測與設計選擇

- 混合方案可減少現有讀取器的改動，並滿足使用者端僅需 .NET Desktop Runtime／WebView2 的部署方向。可行性以前置來源關卡通過為條件。
- 20–36 小時為靜態估算。來源證實困難、缺少可替代資料或生成器內容限制可能增加工作量。
- 建議新增準備模式、普通 assets 目錄、版本化 recipe、staging 原子替換與 web 動態資源映射。這些都是待審查實作契約。

## 未驗證什麼

- 未重跑歷史轉換器，未做全體 13,957 檔 SHA 或完整位元重建。沒有建立任何素材副本。
- 未解碼目前 Text 版本，尚無 evidence 證明舊 entries 與新版本相同。
- 未證實客戶端能提供伺服器 terrain、bounds、skills、spr_action 的等價資料。未讀伺服器 SQL／JAR 或機密設定。
- names.json、skills.json、audio-manifest 的獨立寫入工具尚未全部定位。Lin.Helper.Core 的五個內嵌解碼查表尚待來源審查。
- 尚未完成全 repo／Git 歷史的遊戲內容分類，只確認 web 22 PNG。移除目前追蹤檔與舊歷史處理需要分別界定。
- 未建置、啟動或附加遊戲，未啟停服務、連 DB、播放音訊或建立 GitHub Release。乾淨 Windows、準備後登入畫面與完整遊戲回歸全部待驗。
- 完整實作任務尚未全部拆出，三張首批任務完成後須續排。

## 產出路徑

- `docs/design/portable-delivery.md`
- `docs/plans/issue-97.json`
- `docs/reports/issue-97.md`

工作樹位於 `D:\lineage開發\TW1608262200研究\scratch\wt\task-97`。分支 `task/97`，提交訊息以 `task #97:` 開頭。

## 給下一棒的提示

1. Claude／調度器先審查來源阻塞與 U6 增量理由，避免將本設計當作可發布承諾。plan 只建立首批三張，完整實作超出三張 2h 預算，需後續續排與主線範圍處理。
2. 本機執行器按兩張 test 任務先核對原資料與 repo 內容，再完成限定圖片切片。舊 prepare 腳本包含產品寫入、研究基線複製、SQL／JAR 與外部輸出路徑，請抽出純轉換邏輯，禁止直接重跑。
3. 保留 P22 scene schema 的來源差異，避免誤用 P24 同名腳本。先解決 Text 指紋差異再使用相關 entry，舊 offset 不可跨版本套用。
4. T006 由 #91 已有獨立交付紀錄，後續只需確認打包採用的版本。正常 launcher、6016 固定 manifest pin 與 web icons 接線都須在完整移植時一起處理。
5. 完整離線準備與發布審查通過後，再交既有授權的 dot／使用者進行乾淨機器原生驗收，最後由原 T033 實作任務建立 Release。
