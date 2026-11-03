# 重跑與驗封

現有交付目錄封存後唯讀。可直接重跑只讀 verifier：

```powershell
python -B -X utf8 'D:/lineage開發/TW1608262200研究/work/P28-pickup-profiles-candidate-r1/verify_delivery.py'
```

執行環境為 Windows、已安裝 .NET 10 SDK、Python；NuGet.Config 僅指向 engine/vendor 的已存在離線 WebView2 package，無網路來源。run.py 將 dotnet home、NuGet cache、TEMP/TMP、APPDATA/LOCALAPPDATA 全部置於自己的工作 root。原始 command.json 已列精確 argv/cwd/environment/exit code，stdout/stderr 另外保存。

P29 若獲授權重建，先在其**另行指定的 fresh work root** 建立獨立 byte copies；不要直接在此封存目錄執行 build，也不要複製 assets 或建立 junction。假設 fresh root 直接位於本專案 `work/` 之下，需複製：

- `source-manifest.json` 所列的全部 151 檔至相同 `candidate/source`、`candidate/audio` 相對路徑。
- `checks/` 下四個 source/project 檔、`run.py`、`NuGet.Config`。
- 不複製 artifact-manifest.json、obj/bin、素材、舊 evidence 或 caches。

從 fresh root 執行下列命令；每個 tag 必須尚不存在：

```powershell
python -B -X utf8 run.py product reviewer-product-01
python -B -X utf8 run.py regression reviewer-regression-01
python -B -X utf8 run.py profiles reviewer-profiles-01 reviewer-regression-01
python -B -X utf8 run.py boundaries reviewer-boundaries-01 reviewer-regression-01
python -B -X utf8 run.py negative reviewer-negative-01 reviewer-regression-01
python -B -X utf8 run.py namespace reviewer-namespace-01 reviewer-regression-01
python -B -X utf8 run.py baseline-control reviewer-baseline-control-01
```

最後參數為可重用的相同 helper build tag；runner 先核對 helper source pins 及實際引擎 DLL SHA，才重用。baseline-control 明確使用 sealed r34 DLL，其斷言須另列。candidate/assembly hash 預期為 `6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3`；若新環境重建 hash 不符，停止依賴步驟並調查，不能把原成功結果轉綁新 DLL。

`prepare.py`、`implement.py`、`prepare_checks.py` 是本次初始製作過程的保留腳本，不是封存上的更新命令。payload 精確 recipe 在 prepare.py：只改 AST return 取回已接受 plan.py build 的 in-memory payload，原 expression 保持相同。交付的 current helper source 包含後續 namespace 修復與完整集合 audit；以 current checks source 或對應 attempt snapshot 為準。

如只需重現原失敗 profiles-01，可在另一授權 fresh root 使用 `evidence/regression-01/helper-source/` 的原 helper snapshot，將輸出 tag 另外命名。這會再次得到 all-frame80 vs pickup-only64 的 helper assertion failure；不是產品失敗驗收，也不應為例行覆核重跑。
