# r33 接受材料與入口

本包僅準備，沒有 candidate／ground formal review 接受，沒有授權建置。候選族限 `ENGINE-GROUND-LIFECYCLE-IMPL-Rn`，n 為正整數。原生 schema 保留 `engine-candidate-result-v1`、`engine-candidate-seal-v1`、`engine-r29-source-pins-v1`、`engine-independent-review-v1`；r29 僅是既有格式名稱。原生材料如帶 revision，必須為 r33。

未來必備真實材料：

- sealed candidate 的完整 source manifest、candidate result、candidate seal。seal 必須覆蓋 source manifest、result 及所有 candidate 來源本文。作者 result 需 checksPassed=true，但作者自測不取代獨立 suite。
- 不同 work 根的獨立 accepted review，接受精確 candidateId／candidate seal，unresolvedFindings=[]。正式 ground suite、counts、全部 harness/probe/coverage/commands 與 review artifact seal 必須由主管核對後才採用；目前沒有這些接受材料。
- 主管 `engine-r33-input-contract-v1`，revision=r33，包含 candidateRoot、preparationSha256 及 sourceManifest／candidateSeal／candidateResult／independentReview 四材料的路徑與 SHA。
- 主管目錄內 fresh receipt：`engine-integration-acceptance-v1`，revision=r33，decision=`accepted-for-formal-r33-integration`，authorization=`isolated-r33-build-and-offline-acceptance`，dispatchId=`ENGINE-R33-INTEGRATE-*`。接受精確 contract、四材料 SHA、product/audio 身份與尚未使用的 authorizedOutput，candidateTestsPassed=true、independentReviewPassed=true，無未解發現。本準備派工或 r32 收據都不能代替。

contract 和 receipt 必須共同綁定 final r32：

| 欄位 | 精確值 |
|---|---|
| baseRevision | r32 |
| basePinsSha256 | 0c645bf5c3a515fd5da88cf28c834a4b2967aed268946d45fb5a6ebe9f03fcb7 |
| baseReleaseSealSha256 | 46e9249224e768a1ac09fe99c08a9edf387833295022147aa046c206769fa507 |

baseline-lock 另鎖 final-base-binding SHA `26cc55267f04896344333a6eda50d058d432aa4df1d2655875b6e9772eedff5e`，原 stage baseline SHA `015da2542257d3c124346dcdeb3f34528902b56e1913b5878f3bc995597093ff`、原 stage seal `121fd44082ab5f55c9d7c91ca78256276e1303e2734459210bc6c7e672d73734` 及主管 r32 最終接受 SHA `5ecb6d4d9b09ce99febe4ba4a3e80c95fc57d740c01fbe25d3a7159fa35c6de9`。原件不可改；177 個 path/size/SHA 與四身份逐項等價。正式 receipt 只接受 final baseline SHA，拒絕只用原 stage baseline。

source manifest 完整涵蓋 177 個 source/audio/web 基底，現在預期 Ground.cs／WorldEntry.cs 兩檔差異；正式數量與變更集以最終 manifest、獨立 review、主管接受為準，不憑準備預測強制改寫它們。來源可用專案相對路徑，或嚴格位於 canonical 專案內的絕對路徑；只接受 canonical engine、sealed candidate、或原 stage baseline 精確逐檔綁定的 original r32 stage 路徑。後者必須符合 final 基底同一 path/size/SHA；任意舊 stage、舊外部根、跳脫或 reparse 不可替代。新增來源只可為 sealed candidate 的 source/*.cs，audio 保持接受身份。合法外部固定 BIN/JAR 僅屬唯讀 evidence：`evidence_pins.py` 先核 SHA/size 再 contains 分類，不跳過、不授予 candidate/output 豁免。

唯讀入口格式（尖括號為尚未提供的材料，不能照抄執行）：

```powershell
python -B -X utf8 run_next_stage.py --contract <contract> --contract-sha256 <SHA> --receipt <主管receipt> --receipt-sha256 <SHA> --output work/engine-r33-integration-r1/r33-runs/<fresh-run>
```

預設僅驗材料，不寫、不啟程序。只有未來精確新派工才可加 `--build`；本輪未呼叫該旗標。任何缺材料／錯基底／舊接受／重用 output 先拒絕，直接 runner 亦拒絕。未來基礎151通過才生成 baseline-only overlay，groundLifecycleSuitePassed=false；完整 ground suite 與所有 r32 回歸通過並由主管接受前，不准共享合入。

prepared/release-layout-r33.json 為非執行布局計畫；prepared/verify_release_r33.py 只讀完整 release seal／SHA，禁止建置或產品啟動。rebuild template 只有 canonical engine 可 build/verify，sealed release 只能驗來源。未來素材由 canonical r28 建 immutable hardlinks、記 samefile/device/fileID/linkcount，必要才獨立 copy；不改本文或屬性、不建 assets junction。其他 source/tools/build 為獨立副本。

SYSTEMROOT 大小寫無關查找，DOTNET_GENERATE_ASPNET_CERTIFICATE=false，私有 CLI/NuGet/temp/appdata/cache。命令／結果／schema／output 均為 r33；reference 下的 r32 原件保留其原版本，絕不可直接拿原 runner 當 r33 執行。
