# ENGINE-R29-INTEGRATE-R1 完成交接

**r29 已完成離線整合、共享 engine 重建、封存與入口切換。** 本次已授權開發動作全部完成；整合者停寫 r29，交主管獨立核驗與後續 native/live 驗收。P24 尚未全部完成。

## 精確身份

- Candidate：ENGINE-SLOW-IMPL-R2；原 seal `7bed792d86ec1838ce97e0efa935d061420fa56cf6c0435dbaba0f0c919859b9`。
- r29 release：`D:\lineage開發\TW1608262200研究\releases\r29`，封存 14286 檔；seal：`d3ac187e7fc44745864640e4bcc72fefbaeeaa0b0e9d63de7c0c3cfa8e46273d`。
- 產品 DLL：`8ccfa4e3b5d253206779a5d4b1e59682263ffbddf5dab43fe15b0aa1f86a7397`。
- 音訊 DLL：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`（保持既有接受身分）。
- product manifest：`01e5dfd1f8a310749c0e6daa60b7e6aa2df69b7e4c4009c3902ff6abd6861bb7`。
- launcher DLL：`b1ce1a5f6d11a5f99c566c4d4eecbabc0ca6c946b72371aefb37e5e6d928bdf3`。
- stage、共享 engine 及 releases/r29 三處產品／音訊／manifest／launcher SHA 完全相同。

## 已實際完成

1. 原準備 gate 核對主管 contract／receipt、候選、獨立接受與 preparation seal 後，於唯一指定 fresh stage 建置。沒有改動或放寬準備 gate。
2. 隔離最終 DLL 完成原 151 項回歸及 launcher／restricted-PATH CMD check-only，全部通過。
3. 原樣複製覆核者測試本文到新 scratch，重新編譯並載入同一最終 DLL：1199 獨立向量、112 F1 專項及另外 6 個最小案例通過。1462 assertions = 151+1199+112；六例另計，未混算。每個 helper 的實際載入 DLL SHA 已核對。
4. 9 個 delta 在寫入前再次逐檔核對 base SHA，透過獨立暫存檔替換合入。176 項完整來源全部匹配接受清單；其他 source/audio/web 保持原 pins。
5. 在 engine 安裝新 r29 接受來源／收據／工具 pins 驗證入口，保留原 r28 runner/common。從 engine 完成一次必要重建，0 warnings／0 errors，身份與隔離 stage 完全一致；沒有再跑一遍全部 suite。
6. releases/r29 只建立一次，包含完整來源、必要建置輸入、正式產品／launcher、13957 項獨立素材與來源鏈／證據；排除 NuGet、tmp、obj、測試 helper 二進位等中間輸出。封存後完整逐檔 SHA 核驗通過，未再改 release。
7. 根「啟動引擎.cmd」改指 r29，project-manifest.json 更新 current release 與 revision；歷史 migration、mappings、path_aliases 與 r28 證據保持原值。更新 engine/README 與操作說明。正式根入口只跑 --check-only，通過且沒有啟動視窗、連線或服務。

## 保全與工作邊界

r28 原 seal 未變，全部 14364 檔含素材本文重新做完整 SHA 通過。R1/R2 候選及獨立覆核封存保持原值；R1 rejected 不變。engine/assets 仍指向 r28 的唯讀 junction；未原地改寫共享硬連結，未重建舊外部根。準備 seal 仍為 `f987628e668cfadc77f92b0245f1df19e4fedfb68e71ef2624f4a51ccd2ba21b`。

只寫本次授權 work、engine、新 releases/r29 與根入口／manifest；沒有寫主管目錄。沒有 DB／服務查詢、診斷、啟停或修改，沒有正常產品、GUI、遊戲、音訊播放或網路測試。自己的 Goal 實讀 `null`，沒有新建／恢復／修改 Goal、平台儲存、模型、chat 或代理。

## 驗證與重建入口

唯讀核對已接受 r29 來源：
```powershell
python -B -X utf8 'D:\lineage開發\TW1608262200研究\engine\_integration\r29\rebuild.py'
```

既有 engine 產品核對與 check-only：上述命令加 `--verify`。需要重建已接受的同版 engine 時加 `--build`；會建立全新 evidence 子目錄，驗證 r29 pins／收據及精確 DLL／音訊／manifest／launcher，不要求 engine 回到 r28。不可在封存 releases 內建置。

完整 release 唯讀驗證：
```powershell
python -B -X utf8 'D:\lineage開發\TW1608262200研究\releases\r29\_integration\r29\verify_release.py' --seal-sha256 d3ac187e7fc44745864640e4bcc72fefbaeeaa0b0e9d63de7c0c3cfa8e46273d
```

根入口驗證：
```powershell
& 'D:\lineage開發\TW1608262200研究\啟動引擎.cmd' --check-only
```

本輪已執行命令的 argv、退出碼與輸出在 `integration-r29/actual-commands.json`、stage 的 `_integration/evidence/build01`、`r29-final-slow-checks-01` 與 engine 的 `_integration/evidence/r29-accepted-rebuild-01`。不要原地重跑已封存的一次性 merge/package runner。所有實際變更的前後 pins 與獨立原件備份在 `integration-r29/before-after-pins.json`、`integration-r29/before`。

## 限制與下一步

離線接受範圍為 Speed2/Brave0 步行、轉向與物件呈現；緩速攻擊／施法／道具／裝備／地面與非步行滑鼠動作保持拒絕。測試使用真實 DLL／renderer 與明示合成 actor／封包／時鐘，不代表 native/live 流程通過。現在只剩主管獨立驗證與相應實機驗收；未將其冒稱為整個 P24 完成，也未因等待人工驗收留下本輪開發動作未做。
