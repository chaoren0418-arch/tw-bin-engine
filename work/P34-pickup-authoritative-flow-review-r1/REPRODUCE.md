# P34 重現

工作目錄：`D:\lineage開發\TW1608262200研究\work\P34-pickup-authoritative-flow-review-r1`。

唯讀核驗，不重跑產品：

```powershell
python -B .\verify.py
```

已執行命令（原label不可覆寫）：

```powershell
python -B .\prepare.py
python -B .\run.py build build-01
python -B .\run.py build build-02
python -B .\run.py runtime runtime-01 build-02
python -B .\run.py build build-03
python -B .\run.py runtime runtime-02 build-03 guard-only
python -B .\verify.py --write
```

prepare為一次性，已存在source-manifest時拒絕。第一次build未執行runtime，原因見pre-runtime-review.json。每run的command.json是該次精確argv/cwd/env與snapshots；不手工拼deps，不重建產品。

主管另授權重現時，使用本work新的fresh label；目前helper無mode時只跑原10鏈，guard-only只跑第11鏈：

```powershell
python -B .\run.py build reproduce-build-01
python -B .\run.py runtime reproduce-main-01 reproduce-build-01
python -B .\run.py runtime reproduce-guard-01 reproduce-build-01 guard-only
```

runner對既有label一律拒絕；會核驗已封存使用檔與固定DLL。`verify.py`預設核原runtime-01/02；新重現結果應在獨立validator複本改輸入，不覆寫現有verification或封存attempt。若只驗本次交付，使用第一個唯讀命令即可。

seams：合成S8/S87/S158/S24/S15/S120/S255/S149/S206/S123，in-memory transport callbacks，程序內AppContext asset root，明示空Assets／animation clocks。ground900/3000使用真Environment.TickCount64；不設private deadline，不連socket。
