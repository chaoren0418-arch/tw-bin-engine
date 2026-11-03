# P35 RG2 generator 隔離候選交接

本交付修正 `package_inventory` 遺漏 runtime product manifest 的 RG2 問題。只寫本 P35 目錄，先獨立複製原 generator，再修改 candidate；未合入或重新授權原 P26 工具。完成後封存停寫，等候獨立覆核。

唯一主管：01a185b4-ecf6-705c-90e6-83b9eb1ba02f。執行者：01a17a22-17e4-70c0-9792-166ddb86ea42。實際 Goal 讀值 null，未建立或修改 Goal，未新增代理或聊天。

## 變更與證據

- `candidate/generate_package_plan.py` SHA-256：`4fbf32a4d1a5faa12e34631ad2c0e9ee02647ff51947128c69e1894690f13fa2`。
- 原 generator SHA-256：`32e5cefcc739c7e432dfa9cf00488072c330445695a4b3761d0e43c8ae962578`。原件及 `reference` 副本一致。
- `generator.patch` 和 AST 比較確認只有 `package_inventory` 改動：透過既有 `add` 加入 manifest，再要求四項 identity 具備精確 target/source、canonical SHA-256，並重新核對實際大小／雜湊。
- 原 source/evidence closure、reserved path、casefold collision、`no_links`、authority、pin 邏輯全部保留。沒有自動補齊其他缺失 identity，避免掩蓋不完整清單。
- 原驗收工具的 `completed_context` 真實唯讀驗證成功：180 source files、690 historical evidence pins；沿用原模組與 `SCRIPT_ROOT`，沒有修改 authority、receipt 或接受 synthetic context。
- 原 inventory 2,289 列完整保留，既有列變動 0、刪除 0；候選 2,290 列與不可變 PP2 的 `files` 完全相同。
- 唯一新增 `build/product-r34-manifest.json`，7,955 bytes，SHA-256 `3b3b5557372f325aeecbb30065ed389216e932478e34b5763a4beda031bf8c9d`。來源為原 engine 同名檔案。
- 14 個有限 synthetic 案例通過：manifest 正向納入及原列保存 1、四種錯誤 identity 4、四種缺失 identity 4、product/audio inventory 缺項 2、manifest/launcher 實體缺檔 2、既有大小寫 collision 拒絕 1。13 個案例預期拒絕。測試資料明示 SYNTHETIC ONLY，沒有偽造驗收收據。
- 新增唯讀重驗模式後，以既有 fixture 重驗相同 14 案例，未擴張測試範圍。

## 命令、原始輸出與可重驗方式

工作根為 `D:\lineage開發\TW1608262200研究\work\P35-release-generator-rg2-fix-r1`。實際執行 `python -B <根>\run_check.py prepare`、`synthetic`、`real_compare`、`synthetic_verify`、`finalize`；每個子命令 argv、cwd、真實 exit code 與原始 stdout/stderr 位元組雜湊，保存在 `commands/<名稱>/command.json`、`stdout`、`stderr`。這些是一次性寫入證據命令，不應在封存目錄重新執行。封存由 recorder 在 finalize 子程序完成且實際 exit/log 落盤後執行，輸出封存雜湊至工具紀錄。

下列命令可直接重驗，無寫入、不建立 pycache、不執行產品：

```powershell
python -B 'D:\lineage開發\TW1608262200研究\work\P35-release-generator-rg2-fix-r1\verify_delivery.py'
python -B 'D:\lineage開發\TW1608262200研究\work\P35-release-generator-rg2-fix-r1\synthetic.py' --verify-only
python -B 'D:\lineage開發\TW1608262200研究\work\P35-release-generator-rg2-fix-r1\verify_delivery.py' --inventory
```

第三條會再次使用原 `completed_context` 及原／候選 inventory 比較。如當前 material 漂移，命令應拒絕；不得重算舊驗收 pins 以消除失敗。`source-seal.json` 封存候選、副本、patch 與檢查程式；`artifact-manifest.json` 包含其餘所有交付檔，僅排除它自己和 `artifact-seal.json`。`artifact-seal.json` 綁定 manifest，最後兩個外層雜湊由回報固定，沒有自循環 seal。

## 資格限制與交接條件

這是候選程式與有限 inventory 證據，尚待獨立覆核；不代表原 release tooling 已更新、正式 package plan 已重生或 release 再驗收。候選的完整 CLI 仍保留原權威邊界，未將候選目錄重新綁定為受接受 tooling。沒有呼叫 `generate`、`archive_tools`、`package_release`，沒有正式 plan／release／receipt 寫入或入口啟用。engine、releases、原工具、其他 work、主管目錄皆唯讀。正式入口仍 r33，前後入口與 project-manifest 雜湊一致，見 input-pins 及 final-input-verification。

未執行 RG1 56、canonical 4521 或遊戲套件；未 build、GUI/native/game/socket/network、DB／服務、環境或憑證變更；未刪除檔案。獨立覆核者需檢查 diff、四項 fail-closed 檢查及封存，後續任何合入／authority rebind／正式生成均需要新的明確派工。

先前 ENGINE-R34-ENTRY-ACTIVATE-P26-R1 的自動核准拒絕保留且未重試：

> 此派工會持久修改正式入口與 project-manifest，將 r34 提升為現行產品版本，屬外部／正式承諾；使用者只授權引擎開發，未明確授權這次入口啟用，且政策不以先前助手派工替代該核准。

本輪未請求入口啟用許可，也未另行繞道啟用。
