# B1–B6 執行結果與回復

本文件已由計畫更新為實際結果；初版文件保存在 physical-migration/phase1-documents.zip。整理不等於遊戲專案完成。

| 批次 | 實際處置與完成門檻 | 結果 |
|---|---|---|
| B1 | 使用者指定唯一整合者；主管及三工作者停寫確認；讀程序與埠、空間、完整檔案基準；14 組 DB 冷備份 | 通過，未停未知程序 |
| B2 | r28 同磁碟搬至 releases/r28；原 delivery 為相容 junction；seal 全本文核對 | 通過，封存產品未改 |
| B3 | 建立 engine 唯一來源；修正整合工具固定祖先層數問題；產品與音訊可重現；正常入口 check-only | 通過，未啟動 UI／網路 |
| B4 | 14 Lab 分別搬至現用 environments/p24 或 history/labs；28 設定檔重綁；14 mysql validate-config | 通過，無 DB 內容修改 |
| B5 | 2 外部研究根搬至 history/research；原本已在根內的階段歷史保留；runs/runtime 與建置快取分開封存 | 通過，來源與證據保留 |
| B6 | 僅白名單不可變檔全 SHA 判重；10,868 原件移入可回復隔離；部署位置硬連結；原件回復／重套抽查 | 通過，永久刪除 0、空間回收 0 |

## 已解決問題

Win32_Process 初次遭拒絕後，使用者補充授權，合規審查允許一次重試讀取並成功。搬移後設定檔繼承原權限，正常寫入被拒後以已授權審查執行成功，未改 ACL。MySQL 中文選項路徑導致 Illegal byte sequence，改用 D:\BinEngine 別名後全數通過。建置 helper 的固定祖先層數已只在 engine 修正。早期零 file ID 證明已正式更正，由全本文核對取代。

## 回復順序

1. 再確認本專案停止寫入；讀 migration journals、before snapshots、config-relocations、dedupe-plan。不可直接重跑搬移腳本。
2. 對指定 hardlink 檔，先將現行連結移入暫存保留，再將 quarantine 原件移回；比 SHA。已有 mysql.exe 的實際往返成功收據。保留回復動作紀錄，沒有必要永久刪除。
3. 需要回舊路徑時，先用 config-before-relocation 的原件還原設定，再按外部搬移 journal 反向逐根移回；每次檢查絕對來源／目的邊界及目的不存在。
4. r28 回復先移開相容 junction（只處理已核對的連結本身），再按 journal 將 releases/r28 移回；runs/runtime/cache 依 runtime-archives 對照移回。不可遞迴刪除連結目標。
5. DB 冷備份解壓與 SHA 已驗證，並抽取 3 個檔案回復比對；未做資料庫邏輯恢復或啟動一致性檢查，若需回復運作須另以受控停寫環境驗收。

[MySQL 官方文件](https://dev.mysql.com/doc/refman/8.0/en/server-configuration-validation.html) 說明 validate-config 不初始化儲存引擎；因此本輪不把配置檢查宣稱成 DB 啟動通過。

## 下一步與獨立子任務（尚未派出）

1. 父對話讀回本次 HANDOFF／result、確認唯一開發主管及新 Goal。原整理者只交付，不成為第二主管。現有開發 Goal 仍暫停。
2. 先安排唯一環境負責人，以新 environments/p24 與 ASCII 別名正常啟動實驗服務、登入／保存／重登煙霧驗證；不變更角色資料製造通過。若環境驗收失敗先修遷移相依，不推進功能。
3. 可平行的唯讀任務 A：來源完整性／缺少154特效與 map-local effect 關聯證據，輸出明確來源 ID、hash、缺口，不改 source/assets。
4. 可平行的隔離任務 B：原生擷取逾時重現與最小修復候選，專屬 work/<派工ID>，不得搶佔遊戲／服務或改共享入口；實機操作由唯一主管串行。
5. 唯一主管安排 map4/6016 播放、離區退役、音量行為與同版完整服務流程驗收。避開已造成死亡的危險港口路線；不要用模組測量替代場景證據。
6. 唯一整合者審查候選後合入 engine，重建新版本、核對資源對帳，通過才產出下一個 releases/<revision>。其他工作者不得發布或直接改封存 r28。

每個子任務需明列基準、專屬輸出、禁止共享區、完成標準與證據；任務真正需要才建 work 子目錄，不預建空資料夾。恢復開發需父對話落實主管／新目標的既有要求；本輪沒有額外必要的使用者批准阻礙。清空隔離或永久刪除不在此次授權內，不自動執行。
