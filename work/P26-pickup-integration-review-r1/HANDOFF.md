# P26 拾取整合獨立審核

原封存計畫的獨立審核已完成。結論：**必須最小新 authority/controller/plan 版本修正，才能發新 shared 授權**。本交付不是正式整合、產品成功或 P24／全案完成。

## 可據以派工的結論

- F1：acceptance_gate.SUPERVISOR 固定退役主管；serial_canonical.validate_plan 拒絕合法新主管。common.controller 仍載入舊 controller，context.futureReceiptPath 固定舊路徑。主管已派唯一整合者在 `work/P26-r34-authority-rebind-r1` 修訂；本review沒有重複修補。新版需另提供exact seal/plan/controller SHA再覆核，不能改舊sealed件或偽裝舊主管。
- F2：product collect 保存build-log-bindings後、completed/product前中斷，原exclusive save使collect-only不能恢復。保留失敗，不刪bindings、不重跑build；契約應明示停下交主管，或新版本只對相等bindings作冪等保存。
- F3：nonzero失敗保留raw/attempt且拒絕重跑；但timeout partial stdout/stderr會遺失，只留failed attempt/error。這是合成例外測試，不是產品逾時；若要求完整例外raw logs，另封存最小診斷保存修訂。
- F4：review prep完整verifier因AGENTS政策pin漂移仍FAIL；451 artifacts與其餘307來源pins相符。記憶體精確移除新增1683-byte接管段落後，舊10058-byte AGENTS SHA完全還原。fixture-only獨立核驗通過。新契約應分開綁現行政策和歷史fixture封存，不覆寫舊prep、不冒稱原full verifier通過。

## 已驗證與證據界線

plan SHA `d25c306fae5abee9209e46c557453f7b6a0a96645e5c052fbda70555f11f15ff`，stage seal `9544f137a10914825b53080d4d390d17902cd40866467cebbf69578290240018` 相符。核對180來源（144 source、6 audio、30 web）與20 overlay；138 mappings（136 independent copy、2 verify-existing）、51 pickup fixtures；44操作和10 helper build規劃。205 canonical baseline file pins相符；assets junction解析到releases/r28/assets且integrity相符；不重hash全部封存asset payload。

raw-log草稿問題已在sealed plan修復：原attempt不改，先驗raw SHA再衍生collector stdout/stderr specs。nested 4個rebuild child argv/cwd/log paths與2個PowerShell child指令靜態對核通過；兩個PowerShell child沒有各別receipt，只由parent保留streams並逐一檢查exit。

原stage的4521 assertions／93 observations是引用的歷史接受證據；本輪產品build、runtime、native均0。8項synthetic/negative檢查不是產品成功。41 profiles source plan verifier通過；review prep的416 PNG、2688 planned mappings、294 duration rows、672 scenarios等fixture-only檢查通過，不是候選執行。

## 後續契約

`followup-contract-inputs.json` 包含r33 canonical、已接受r34 stage四項identity、source manifest、source plan/prep seals、現行policy/takeover及資源pins。41-profile擴展必須等真正canonical r34接受與final base精確綁定；不能把stage當final base。既有239766-byte resource SHA保持不變；新增extension預期2473547 bytes、288幀、12組，SHA `aff2e04d4f9395f7993baf5818eb16218730b7def0a29c7d07e86c314db513ba`，本輪沒有寫payload。

exact-pair allowlist、gfx/sprite namespace、2796 exact keys、shadow misses、double timing、owner切換、projectile selector、S158/15與terminal584/900/3000等限制詳列於contract inputs；所有native/Slow/remote/BIN資格不升格。

## 重跑

在任意工作目錄執行（Python標準庫，`-B`禁止cache）：

```powershell
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P26-pickup-integration-review-r1\run_review.py" --run-verifiers
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P26-pickup-integration-review-r1\finalize_review.py"
```

第一個命令只跑三個指定read-only verifier、hash/AST檢查、專屬root synthetic fixtures；預期profiles-prep原verifier exit 1（AGENTS pin），審核會確認唯一漂移及byte還原。任何其他漂移應失敗，不可自行更改來源。第二個命令只重建本review報告。synthetic子目錄保留以供查核。

`review.json`逐項結論、`findings.json`精確位置與退修條件、`source-sha256.json`來源pins、`verification-commands.json`命令/log pins、`synthetic-tests.json`合成檢查、`policy-drift-proof.json`政策byte證明為主證據。此報告沒有shared receipt或產品寫入權。
