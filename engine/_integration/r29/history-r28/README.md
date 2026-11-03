# 唯一可寫引擎來源

本目錄由已接受的 r28 派生，`source/`、`audio/`、`web/` 是下一次開發的唯一來源。整理期間暫停功能修改。`releases/r28` 是不可變基準，不直接重建或修改。

`assets` 是指向 `../releases/r28/assets` 的基準素材連結，唯讀使用；要修改素材時先建立新版本素材輸入，不可透過此連結覆寫 r28。`vendor` 為工作建置所需的套件副本。

本次已修正 `_integration/common.py` 原先依賴固定父目錄層數的定位；產品與音訊 DLL 重建仍與 r28 相同。`_integration` 中複製的舊 seal／收據是來源歷史，不代表此工作目錄已封存。最新重建收據位於 `_integration/evidence/migration-rebuild-20261028/result.json`。

基準重建驗證（在新的 evidence 目錄，避免覆寫舊收據）：

```powershell
python -B -X utf8 .\_integration\run_stage.py --output .\_integration\evidence\<新的批次名稱>
```

這個基準驗證器固定比對接受的 r28 DLL。未來開發改版需由唯一整合者先獨立審查新候選並建立新版本契約，不應直接改驗證器來放過未知差異。

正常產品入口為專案根的 `啟動引擎.cmd`；本目錄的啟動器僅供工作副本驗證。資料庫與伺服器位於 `../environments/p24`，ASCII 相容入口為 `D:\BinEngine\environments\p24`。整理不會自動啟動服務或遊戲。
