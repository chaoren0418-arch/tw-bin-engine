# ENGINE-R31-INTEGRATION-PREP-R1

狀態：`prepared-awaiting-accepted-haste-candidate`。本輪準備已完成，交付後停止寫入。唯一寫入目錄 `work/engine-r31-integration-r1`；自己的 Goal 實讀為 null，未建立或更改。主管 baseline 內的 paused 是主管狀態，不冒充本對話 Goal。

已從封存 r30 gate／runner／rebuild 模板最小轉接 r31；接受基底為 r30、候選族為 ENGINE-HASTE-ACTIONS-IMPL-Rn。baseline-lock.json 鎖177個 source/audio/web，另加26個建置支援／入口／素材index，共203檔；不因先前 r29 到 r30 新增一個來源而假設支援檔數相同（r30 runner 改從已封存 stage 取用，canonical engine 沒有該檔）。reviewable diff 是 r30-to-r31-wiring.patch，reference 保留原文與來源 pins。

基底四項身份：

| 物件 | SHA256 |
|---|---|
| r30 release seal | 199ce6fe81130138e9fedf98e688928ce44b0a45c2aebd1b92fdd9519b8225dc |
| product DLL | 1abc1eed02917d14fd775be60920fe74efb9b102bc53eea5874fc0128e5247ef |
| audio DLL | d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4 |
| product manifest | 4b0029286463050a4ed64de5a7c8485defd673a6e3d1bba185a9f9d4f157de69 |
| launcher DLL | f0145345af54961b59943b981e392ae3e268c42ef957a8eacdda64967641c050 |

MATERIALS.md 提供原生 schema、contract／receipt 與唯讀入口；VALIDATION-PLAN.md 列出 Speed1 舊斷言的精確來源位置與待驗收邊界。8項小型檢查通過，結果在 evidence/rejection-tests.json，包含大小寫 SYSTEMROOT 與 assertion occurrence 教訓。r30完整151／976／1201／112本輪都未執行，沒有製造 accepted r31 receipt、候選本文或成功建置證據。

重跑小型檢查（輸出只在本工作區；封存後若要改寫須新派工）：

```powershell
Set-Location -LiteralPath 'D:\lineage開發\TW1608262200研究\work\engine-r31-integration-r1'
python -B -X utf8 test_rejections.py
```

未來 stage 素材接線重用 r30 release 已驗證的 hardlink／samefile 方法，reference/r30/release.py 與其 shared integration seal 已對 pin。新 run 會記錄來源與file ID，fallback只能是獨立副本；素材皆不可寫，不建立 junction。本輪 asset link、asset copy、stage、build、merge、release、入口切換皆為零；沒有 DB、服務、正常產品、GUI、socket 或原生研究操作。

preparation-seal.json 固定可執行接線、基底、格式、reference與計畫；result.json 引用其精確SHA；artifact-seal.json再覆蓋交付結果與證據。evidence/final-verification.json 記錄有限範圍唯讀核對；未做新的14296檔release／13958檔assets全掃描。已核對來源177、支援26、基底四身份、封存舊測試來源、舊prep/stage/shared seals與本輪讀取的證據pins。

尚待主管接受 haste 候選與獨立覆核、提供真實收據及新建置派工。這份交付不授權成功路徑、完整suite、canonical合入或release；成功路徑本輪未測。整體 P24 與 native/live 驗收仍未完成。
