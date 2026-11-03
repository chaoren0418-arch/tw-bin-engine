# RG2 — supervisor-authorized exact one-row package plan correction

依新主管精確指示，僅在本fresh package-plan-r2 root修正計畫清單。原r1計畫與完整seal不變，原r2 generator/tooling也不改；workflow維持凍結。

correct_rg2.py讀原exact r1（SHA a831ca6dbbe13dee16bf544b6ff9f1ea3284ff7091915af23685ab8827d0d890），只增加 source=engine/build/product-r34-manifest.json、target=build/product-r34-manifest.json、size=7955、SHA=3b3b5557372f325aeecbb30065ed389216e932478e34b5763a4beda031bf8c9d。依原casefold排序；原2289rows完全等值，其他原fields完全等值，releaseTooling維持原41檔archive及其pins。新增planCorrection metadata記錄RG2原因、原plan pin及correction script pin，方便之後release/package receipt追溯。

新plan為2290 files /1099423 bytes，SHA 939fcbd2176d1360b510153993f43deacf91b34a3b1737fb4d91ad8b30bb0c01。verify_correction唯讀核對全部2290來源pins、180來源、8 completed、4identity正式targets、rebuild/runtime/launcher dependencies、897 stage非資產、808 frozen workflow、690 historical reference pins及41檔tool archive，全通過。真正release receipt未核發，本回合不呼叫完整receipt gate或--package；原package_release仍會以真正新exact receipt完整驗證，不繞過任何gate。

這是主管核准的最小清單補齊，不表示r1生成成功，也不表示r2 generator遺漏已通用修復。原失敗計畫、診斷與現場全保留。沒有重跑generator/build/probe，沒有改workflow、engine、sealed工具、releases或入口/manifest。

唯讀重跑：python -B -X utf8 D:\lineage開發\TW1608262200研究\work\P26-r34-package-plan-r2\verify_correction.py

correct_rg2.py預設只驗輸入；--write只允許fresh不存在輸出，不能用於覆寫本計畫。等主管另核發綁定新plan SHA的release receipt。Goal實讀null未修改；本回合不操作DB/services/GUI/network/native。
