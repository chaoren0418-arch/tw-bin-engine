# r32 材料與入口

本包狀態為 preparation-only，不是候選接受或建置授權。原生 schema 名稱保留 `engine-candidate-result-v1`、`engine-candidate-seal-v1`、`engine-r29-source-pins-v1`、`engine-independent-review-v1`；r29 在此只是既有欄位格式名稱。候選族必須為 `ENGINE-SLOW-HASTE-ITEM-IMPL-Rn`，n為正整數；原生材料若有revision必須為r32，省略時仍由candidateId、seal、contract綁定。

未來contract須為 `engine-r32-input-contract-v1`、revision=r32，包含candidateRoot、preparationSha256、sourceManifest／candidateSeal／candidateResult／independentReview四種材料的專案相對路徑與精確SHA。獨立review須位於不同work根，decision=accepted，接受精確candidate seal且unresolvedFindings=[]。

receipt須位於主管目錄，schema=`engine-integration-acceptance-v1`、revision=r32、decision=`accepted-for-formal-r32-integration`、authorization=`isolated-r32-build-and-offline-acceptance`、新的dispatchId=`ENGINE-R32-INTEGRATE-*`，接受精確contract與四材料SHA及product/audio DLL身份；candidateTestsPassed及independentReviewPassed=true、無未解決發現。這些材料尚未提供，本包沒有製造accepted樣本。r30／r31舊收據或候選不能替代。

contract與receipt共同固定：

| 欄位 | 精確值 |
|---|---|
| baseRevision | r31 |
| basePinsSha256 | 00e9d4664024119595faeead50ade5827258763e47dc3d7068ea9b78abf8c813 |
| baseReleaseSealSha256 | 1d5b1c149495cb2b44f50daf2c3361eb15e8000ba27bf133d1029735f10cfd90 |

baseline-lock另鎖final-base-binding SHA `d6946c449f35b1d26804bd3fd4878bf24bdb0f74d66a302765dfd91058010fa4`、原stage baseline SHA `16ab9dbb534e20e03e622777ab530065e1bc97363c08d8b765e2cb880115bb87`與主管r31接受SHA `c61b78bc32afb590675f9a73e1c7e5f171156af2afa9e8d58c4ff03b55c25d1d`。原stage文件不改寫；其額外source來源欄位與final文件不同，按177個path/size/SHA及四身份確認等價。gate必須使用final baseline SHA，拒絕只引用原stage baseline的receipt。

source manifest每列包含path／size／sha256／source，完整覆蓋177個source/audio/web基底。既有檔來自精確基底或sealed candidate，新增僅限真正封存的source/*.cs；audio完全保持。候選與輸出路徑仍禁止跳脫專案根。`evidence_pins.py`僅處理另行固定的唯讀證據：先核絕對路徑檔案SHA/size，再用is_relative_to分類，外部固定BIN不能被跳過，也不取得輸出或候選路徑豁免。

精確基底可引用canonical engine，或原stage baseline每列明定的不可變來源路徑；後者必須額外通過final基底的同一path/size/SHA。不是任意舊stage豁免，也不讓新檔借用stage名義。

唯讀入口格式（尖括號代表尚未提供的真實材料，不可照抄執行）：

```powershell
python -B -X utf8 run_next_stage.py --contract <真實contract絕對路徑> --contract-sha256 <SHA> --receipt <主管receipt絕對路徑> --receipt-sha256 <SHA> --output work/engine-r32-integration-r1/r32-runs/<新run>
```

未來只有新建置派工才可加`--build`。gate先驗全部材料、prep seal、基底與fresh輸出；缺材料時不建立目錄。runner禁止直接執行。基礎151真實通過才生成baseline-only rebuild overlay，hasteItemSuitePassed仍false；utility與r31回歸、shared merge/release/root切換各須後續精確接受。

未來素材接線沿canonical r28 immutable hardlinks，逐筆驗inventory SHA/size、samefile、device/file ID及link count；必要fallback僅獨立copy。無assets junction，不改共享本文或屬性，build/helper不得寫assets；source/build/tools為獨立副本。`prepared/release-layout-r32.json`只定義後續release布局及門檻，沒有部署動作。

SYSTEMROOT從一般dict以大小寫無關方式取得。common.py與rebuild template保留DOTNET_GENERATE_ASPNET_CERTIFICATE=false、私有CLI/NuGet/temp/appdata/cache。本輪沒有啟動SDK。assertion_occurrences.py回傳名稱＋一基出現序號，重名不去重、不改運算式；保留r31漏return及外部BIN路徑修正原件供審查。
