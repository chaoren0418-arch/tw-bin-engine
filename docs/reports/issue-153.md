# Issue #153 封存素材缺失唯讀盤點

## 結論

已完成 r28–r33 逐檔盤點。**r28/assets 與 r33/assets 各缺 13,958 檔，共 27,916 個缺失路徑。r29–r32 的對應素材全部存在且 SHA-256 正確。**完整封存清單範圍內，現存檔案 SHA 不符數為 **0**。

歷史調度器程式、junction 建立紀錄、缺檔前後測試、空目錄修改時間與 closed sweep 事件，高度支持 **#130 清理造成 r28 損失，#133 清理造成 r33 損失**。當時逐檔刪除系統呼叫與清理子程序起訖沒有留存，因果屬交叉證據推定。

## 做了什麼與核驗基準

- 讀取根目錄 `AGENTS.md` 與 `docs/REPORT.md`，fetch 後依指示切換 `task/153`。基線 commit 為 `7ebab5696bc7f9d11e60bb284cebf110b0bb59a4`。
- `releases/SHA256SUMS.txt` 在磁碟、`origin/main` 與本機所有 refs 的 Git 歷史均查無，repo 為完整歷史。`.gitignore` 雖有例外文字，但其父目錄 `releases/` 仍被排除。該清單缺失時間與既往內容無從判定。
- 改採**經 Git pins 鎖定的各版本完整 seal**。r28 使用 `_integration/candidate-seal-manifest.json`，SHA 與 `docs/project-consolidation/r28-pins.json` 相符。r29–r33 使用各自 `_integration/rNN/release-seal.json`，SHA 均與 `work/engine-r34-integration-r2/reference-pins.json` 相符。每份 seal 自身亦核驗。
- 素材先驗證 `integrity.json` 的 Git 鎖定 SHA，再逐路徑讀取全檔 SHA，並與各版完整 seal 交叉比對。r29–r32 四份皆實讀雜湊。素材查核時間為 2026-11-06 16:18:51–16:24:11 UTC，非素材另以完整 seal 補核。
- 只新增本報告、兩份文字證據與 scratch 查核輸出。封存與連結全程唯讀。未執行建置或 check-only，未啟動遊戲與服務，未查詢或變更遊戲 DB。

## 受損範圍與完好副本（已驗證）

下表的預期檔數含 seal 自身，素材檔數含 `integrity.json`。

| 版本 | 預期檔數 | 素材 SHA 符合 | 素材缺失 | 非素材 SHA 符合 | SHA 不符 |
|---|---:|---:|---:|---:|---:|
| r28 | 14,365 | 0 | 13,958 | 407 | 0 |
| r29 | 14,287 | 13,958 | 0 | 329 | 0 |
| r30 | 14,297 | 13,958 | 0 | 339 | 0 |
| r31 | 14,330 | 13,958 | 0 | 372 | 0 |
| r32 | 14,343 | 13,958 | 0 | 385 | 0 |
| r33 | 14,654 | 0 | 13,958 | 696 | 0 |

共核對 86,276 個預期路徑，58,360 檔 SHA 符合，27,916 檔缺失。r28 另有 129 個 seal 排除的 `obj`、`.dotnet`、`appdata`、`tmp` 等既存檔，已列出現況 SHA，歷史完整性留待取得相應基準。

**逐檔缺失清單**包含完整路徑、預期 SHA 與實際狀態，並指定四個已核驗副本版本。每筆將路徑中的 r28／r33 換成 r29、r30、r31、r32，即為存在且 SHA 相同的副本。完整清單與 seal SHA、額外檔案、日誌來源、File ID 證據均隨報告提交。[缺失清單](../../work/issue-153/missing-files.json) · [核驗證據](../../work/issue-153/verification.json)

`releases/r28/assets/integrity.json`、`releases/r33/assets/integrity.json` 均缺失，其預期 SHA 為 `07dd19f4407ec3f2bf61da9c00cca827cbbe206866863a87a77f17fce7a18b98`。r29–r32 四份該檔皆為 1,729,333 bytes 且 SHA 相符，內列 13,957 個素材。

NTFS 全量 File ID／link count 核對結果如下。

- **r29** 的 13,958 檔各 `nlink=1`，與 r30–r32 分屬不同 File ID，可作獨立內容副本來源。
- **r30–r32** 的 13,958 個對應路徑共享 File ID。`integrity.json` 的 `nlink=8`，其餘素材各 `nlink=9`。另以 `fsutil hardlink list` 抽核 `integrity.json` 與 `terrain0.bin`，可列出這三個版本的連結。
- r28 與 r33 的 `assets` 現為空的實體目錄。主目錄 `engine/assets` 的 junction 目標為 `releases/r28/assets`。

## 原因與時間（已驗證紀錄／推定）

事故時使用的 `runners.py`，在 commit `78648355201a9763ffa6156028a17814fd344750` 第 87–90 行，直接呼叫以下清理形式。`main.py` 第 478–483 行於呼叫返回後記錄 `closed externally → task:done`。原實作以 `capture_output=True` 接收結果，未記錄退出碼與輸出。下列為依原始碼還原的 argv，**本輪僅查閱**。

```text
git -C "D:\lineage開發\TW1608262200研究" worktree remove --force "D:\lineage開發\TW1608262200研究\scratch\wt\task-N"
```

| N | 清理函式返回後事件時間（2026-11-06 UTC） | 日誌證據與判讀 |
|---|---|---|
| 129 | 13:14:22.056788 | log 顯示工作及建置在主目錄。該路徑是否存在並進入 git 子程序未留存。 |
| 130 | 13:39:08.356297 | log 第 6208–6209 行成功建立自身 assets→r28 與 vendor→主目錄 vendor。第 7668 行世界／UI 測試通過。r28/assets mtime 為 **13:39:07.706511**。 |
| 133 | 13:54:46.369368 | 第二輪 log 第 1164–1207 行證實 r28 integrity 缺失。第 1496 行以非遞迴 `Directory.Delete` 拆自身 junction 後改接 r33，第 1594 行測試通過。r33/assets mtime 為 **13:54:45.940000**。 |
| 140 | 13:58:57.970882 | 有 worktree 執行與清理事件，檢查日誌未找到自身 assets junction 建立指令。 |
| 145 | 14:05:39.808249 | 有 worktree 執行與清理事件。第 11934 行刪除的是自身 receipt。日誌中另有測試腳本連結程式碼，無法據此確認當時建立了指向真素材的 junction。 |

#130 的素材目錄最後變動比清理返回事件早約 0.65 秒，#133 約早 0.43 秒。兩次命中相應 junction 目標，且 #133 在兩次清理之間觀測到 r28 缺失、r33 可用，形成連續證據。調度器亦已回覆其當時清理路徑與修正措施。[調度器說明](https://github.com/chaoren0418-arch/tw-bin-engine/issues/153#issuecomment-6689557574)

本輪讀取 #129、#130、#133 兩輪、#140、#145 共六份執行 log。來源位於 `D:\dev-orchestrator\data\logs\`，檔名與 SHA 全列於核驗證據。時間事件從調度器 `state.sqlite` 以 `mode=ro` 讀取。五個 `scratch/wt/task-N` 及其 `scratch/` 均已不存在，主目錄 `scratch/issue-129` 尚存。被移除 worktree 的原始 scratch 證據無法再取回。

附帶確認主目錄 **`engine/vendor` 亦為空**，mtime 為 13:39:07.793914 UTC，與 #130 清理同時。#130 與 #133 第二輪均有建立 vendor junction 的紀錄。此項需另案核定建置前置修復範圍。

## 未驗證什麼

- 未以破壞性重現驗證特定 Git／Windows 的 junction 遞迴刪除行為，亦無當時逐檔刪除系統呼叫記錄。精確清理開始時間、退出碼及最後一個刪除動作待補證。
- 未核驗 r28 之 129 個 seal 排除檔的歷史正確性。初查用到的 `engine-next` 工作基線與 r28 封存的 `_integration/common.py` 有版本差異，最終以 r28 完整 seal 核對通過。
- 修復、修復後的 check-only 與真機驗收留待後續任務。其他 release、歷史素材及 vendor 的完整損失不在本次逐檔核驗範圍。

## 建議修復方式與給下一棒的提示

1. **主控／使用者裁定後另開修復任務。**先保全證據與 r29 獨立副本，再依逐檔清單從已驗證副本補回 r28、r33 的缺失路徑。既有 hardlink 共用範圍、採獨立檔或重建 hardlink，由主控核定。修復後以相同 seal 全量重驗，再交本機執行器查核建置前置與 check-only，真機交 dot。
2. **調度器接手清理防護。**已確認本機於 16:16:16 UTC 自動更新 c730a94，具先非遞迴拆 junction／symlink 與派工禁止自行清理的文字。調度器回覆已暫停過派工，盤點完成前保留 worktree。[暫停及上線回覆](https://github.com/chaoren0418-arch/tw-bin-engine/issues/153#issuecomment-6689645564)
3. **拆除失敗應立即中止清理。**查閱 c730a94 發現 `lstat` 失敗會 continue，兩次拆除失敗會 pass，隨後仍執行 `git worktree remove --force`。請調度器補齊失敗即中止與清理 argv／退出碼／stderr 留存。本輪已將此項及 vendor 缺口回報 #153，修正驗證由調度器完成。[發現與交接留言](https://github.com/chaoren0418-arch/tw-bin-engine/issues/153#issuecomment-6689776950)
4. **補齊封存清單管理。**主控核定完整 seal 作為正式核驗入口，並修正文檔中 `SHA256SUMS.txt` 的追蹤描述。若要建立該檔，另案處理 `releases/` 父目錄忽略規則與封存寫入授權。

## 產出路徑

- `docs/reports/issue-153.md`
- `work/issue-153/missing-files.json`，27,916 筆逐檔缺失及副本對應規則。
- `work/issue-153/verification.json`，版本 seal／Git pins、統計、129 個 seal 外檔案的現況 SHA、hardlink 身分、事故日誌 SHA 與摘錄。
- 本機查核腳本與完整中間輸出保存在 `scratch/issue-153/`，其中 `audit.py` 核對素材、`seal-audit.py` 補核完整封存、`links.py` 核對 hardlink。Git 提交僅含上述報告與兩份證據。
