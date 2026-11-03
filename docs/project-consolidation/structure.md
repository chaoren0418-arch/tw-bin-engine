# 唯一專案結構與入口

唯一實體專案根：`D:\lineage開發\TW1608262200研究`。

| 根內位置 | 用途／寫入責任 |
|---|---|
| 啟動引擎.cmd | 正常 r28 客戶端入口；加 --check-only 可檢查但不啟動遊戲 |
| engine/source、audio、web | 後續唯一有效引擎原始碼；由唯一整合者串行合入 |
| engine/vendor、launcher、_integration | 建置相依、啟動器原始碼與驗證工具 |
| engine/build | 可重建產物；本輪重建與 r28 產品／音訊 DLL 相同 |
| engine/assets | 指向 releases/r28/assets 的 junction；封存基準，不得直接改寫 |
| releases/r28 | 正式封存交付；產品 seal 未改 |
| environments/p24 | 現用實驗伺服器／DB／設定；目前停寫，未開機測試 |
| history/labs | 13 個舊 Lab 的狀態與研究歷史 |
| history/research | 6016 匯出、ASCII 研究 |
| evidence/r28-runtime-before-consolidation | 遷移前 runs、runtime 證據 |
| recovery | 冷備份、設定原件、隔離重複原件、舊快取及回復抽查 |
| docs/project-consolidation | 本次報告、索引、基準與執行收據 |
| 既有 P01～P24、主管 | 凍結的階段歷史及權威驗收證據，保持根內原位置 |

既有根內歷史約 112.36 GiB 的原始盤點沒有整批複製或重新搬家；它們已在唯一根內，保留以免破壞歷史相對路徑。此輪沒有宣稱完成所有歷史內容的去重。

## 相容與路徑解析

只保留三個用途明確的相容連結：`D:\BinEngine` 指向本根（MySQL 選項路徑需要 ASCII）、舊 r28 delivery 指向 releases/r28、engine/assets 指向封存素材。原 16 個 D 槽根資料夾沒有留下相容副本。

14 個 mysql.ini 與 14 個 server.policy 已重綁到新根的 ASCII 別名；憑證、埠號及 policy 權限語意未變。沒有改 ACL、OS 安全設定或 DB 內容。舊報告與歷史腳本保留原文，不代表仍可直接重跑；先以 manifest 對照轉換輸入路徑。正式接續只使用 engine 與 environments/p24。

舊路徑解析範例（僅輸出路徑，不執行歷史程式）：

```powershell
python -X utf8 .\engine\tools\resolve-path.py 'D:\P24Lab-ClientEngine\mysql.ini'
```

## 可重跑的必要檢查

在本根執行 `./啟動引擎.cmd --check-only`。建置重現命令為 `python -B -X utf8 engine/_integration/run_stage.py --output engine/_integration/evidence/migration-rebuild-20261028`，既有結果已通過，不需為閱讀報告重跑。該腳本是 r28 基準驗證；未來修改產品時應更新版本與驗收規格，不能沿用舊 seal 宣稱新版本通過。

[manifest](../../project-manifest.json) · [原始碼說明](../../engine/README.md) · [重建結果](../../engine/_integration/evidence/migration-rebuild-20261028/result.json)
