# r31 材料與入口

這是 ENGINE-R31-INTEGRATION-PREP-R1 的準備包，不是 accepted 候選或建置授權。唯一工作區是 `work/engine-r31-integration-r1`。未來的成功路徑須另有主管新派工與精確收據。

`acceptance-materials.schema.json` 定義格式；`acceptance_gate.py` 是跨檔案、路徑、seal、來源覆蓋與授權的實際判定。沒有製造可接受的材料範例。原生格式名稱保留：`engine-candidate-result-v1`、`engine-candidate-seal-v1`、`engine-r29-source-pins-v1`、`engine-independent-review-v1`。其中 r29 是既有欄位格式名稱；候選 ID 必須是 `ENGINE-HASTE-ACTIONS-IMPL-Rn`，n 為正整數。原生材料若有 revision 必須為 r31；若省略，仍須由候選 ID、seal、contract 綁定。

contract 必須是 `engine-r31-input-contract-v1`，revision=r31，含 candidateRoot、preparationSha256、四種材料的專案相對路徑與 SHA256。candidateRoot 是不同的 work 直屬子目錄；獨立 review 須在另一個 work 根。source manifest 的每列是 path／size／sha256／source，涵蓋固定基底全部 177 個 source/audio/web 檔。既有檔可引用精確基底或 sealed candidate；新增只能是候選 seal 覆蓋的 source/*.cs。audio 必須保持基底 pin。

主管 receipt 必須位於主管目錄，schema=`engine-integration-acceptance-v1`、revision=r31、decision=`accepted-for-formal-r31-integration`、authorization=`isolated-r31-build-and-offline-acceptance`，dispatchId 必須是新的 `ENGINE-R31-INTEGRATE-*`。須接受精確 contract、source manifest、candidate seal、candidate result、independent review、product/audio DLL SHA，且 candidateTestsPassed、independentReviewPassed 都為 true、unresolvedFindings=[]。舊 r30 收據不能替代。

contract 與 receipt 同時固定以下三個值，且不得只比對版本字串：

| 欄位 | 必須值 |
|---|---|
| baseRevision | r30 |
| basePinsSha256 | 1329700b9fabf59e7406d62790ea8a32758ee9771963d0dcd49c7cde85708a66 |
| baseReleaseSealSha256 | 199ce6fe81130138e9fedf98e688928ce44b0a45c2aebd1b92fdd9519b8225dc |

入口參數如下；尖括號都是未提供的真實材料，不能照抄執行。省略 `--build` 為唯讀驗證。只有後續新派工才可加入 `--build`。

```powershell
python -B -X utf8 run_next_stage.py --contract <真實contract絕對路徑> --contract-sha256 <精確SHA256> --receipt <主管真實receipt絕對路徑> --receipt-sha256 <精確SHA256> --output work/engine-r31-integration-r1/r31-runs/<新run名稱>
```

入口先完成 gate，才能建立新 r31-runs 直屬子目錄；已存在目錄、reparse 路徑與可寫共享 hardlink 拒絕。stage runner 不可直接執行。未來 baseline 151 通過才生成 rebuild-overlay，仍標示 hasteSuitePassed=false，且無合入、release 或入口切換能力。重建模板保留待接受的 lock／contract／receipt／candidate token；本輪不填入假資料。

未來 stage 的 immutable assets 採 r30 已驗證的 os.link + samefile + link-count 策略，來源只准 canonical releases/r28/assets，包括 13957 本文與 integrity.json。入口依已鎖 integrity 逐檔核 SHA/size，記錄 sourceRoot、來源／目的 device/file ID 與方式。link 不可用時只容許獨立副本並記錄。不得改來源本文或屬性、建立 junction，或讓 build/helper 寫入任何 assets；後續修改須獨立新檔。source/build/tools 與 helper 工作區均獨立複製。本輪未建立任何素材 link 或副本。

Windows 限縮環境的 SYSTEMROOT 查找在 runner 與 rebuild template 中均採 `next(v for k,v in restricted.items() if k.upper()=='SYSTEMROOT')`；不可改回一般 dict 的 `['SystemRoot']`。未來 suite adapter 使用 `assertion_occurrences.py` 的 (name, occurrence ordinal)，同名斷言不去重。這僅支援對帳，不改斷言運算式、不聲稱名稱唯一或依數量證明等價。
