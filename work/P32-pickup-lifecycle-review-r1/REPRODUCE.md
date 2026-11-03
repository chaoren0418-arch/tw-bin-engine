# P32 可重現命令

工作目錄：`D:\lineage開發\TW1608262200研究\work\P32-pickup-lifecycle-review-r1`。

唯讀交付驗封：

```powershell
python -B .\seal.py verify
```

已執行的實際命令如下；**不要再次使用既有label**，runner會拒絕覆蓋。完整dotnet argv/cwd/環境/raw在對應 `runs/<label>/command.json`。

```powershell
python -B .\run.py build build-01
python -B .\run.py runtime runtime-01 build-01
python -B .\run.py build build-02
python -B .\run.py runtime runtime-02 build-02 supplement
python -B .\run.py build build-03
python -B .\run.py runtime runtime-03 build-03 actor-reentry
```

既有source snapshot保持每次實際版本。最新版Probe.cs預設suite仍是首輪188，`supplement`僅7，`actor-reentry`僅1。未來獲主管重跑授權後，以新label build並執行需要的子集合；不覆寫任何封存run。不需要、也不允許重建產品或啟動產品exe。

現行run.py固定offline vendor、自己HOME/cache/tmp/packages、PROGRAMFILES(X86)，停用SDK憑證與global-tools PATH首次設定。舊build-01環境偏差不回寫修飾，其原run.py snapshot仍保留。

`finalize.py`是本輪報告產生器，會寫報告，不是唯讀verify。`prepare.py`是本輪初始編製器，會覆寫準備檔，封存後勿執行。單純查驗使用`seal.py verify`。
