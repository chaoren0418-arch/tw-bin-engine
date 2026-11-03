# ENGINE-R32-INTEGRATION-PREP-R1

狀態：`preparation-only`；hasteItemSuitePassed=false，merged/release/rootEntryChanged=false。唯一寫入work/engine-r32-integration-r1，交付封存後停寫，等待主管接受候選、獨立review及精確新build收據。Goal實讀null，未建立／恢復／更改；主管baseline的paused不代表本對話Goal。

已轉接r31的validated入口、gate、原生schema、full-source coverage與rebuild overlay至r32，候選族ENGINE-SLOW-HASTE-ITEM-IMPL-Rn。baseline-lock鎖最終r31：177來源加26支援檔共203。原stage baseline、final baseline、final-base-binding與主管接受均保留原檔及精確SHA；按path/size/SHA和四產品身份確認等價。首次bootstrap曾直接比較含不同來源欄位的整列而在任何寫入前停止，已改為精確身份欄位比較，紀錄在evidence/bootstrap-shape-correction.json，沒有改基底。

| r31 身份 | SHA256 |
|---|---|
| release seal | 1d5b1c149495cb2b44f50daf2c3361eb15e8000ba27bf133d1029735f10cfd90 |
| product DLL | 12ad5bbfac696033ee1f8d807346f401216138f4ce51cb58da9438e0b4ba5ec1 |
| audio DLL | d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4 |
| product manifest | dd56d46466a60cae52e6bbc5b4561e5ad7db331c8db2b04930912cb251cab1d9 |
| launcher DLL | 78b8a832ae8c1c1ff3fa25290d4cc28931fc9d11223acb0737823c9bfd4d3d72 |

MATERIALS.md與acceptance-materials.schema.json定義未來真實材料與入口；VALIDATION-PLAN.md列有限utility範圍、既有測試適配位置及同DLL回歸。既有slow-item fixture是紅水，不是exact haste，不能放寬。prepared/release-layout-r32.json只規劃未來release路徑及驗證門檻，沒有現在發布能力。

11項小型gate／接線檢查通過：舊版本／候選家族、缺材料、錯pin、177完整覆蓋、新sealed source形狀、舊stage baseline拒絕、精確綁定的不可變stage來源、直接runner拒絕、環境大小寫／憑證關閉／私有cache、重名occurrence與外部證據SHA/size。詳細結果與實際命令在evidence。成功建置路徑未執行，沒有假accepted材料、產品候選本文、stage、assets link/copy、完整suite或release。

保留r31的漏return與外部BIN relative_to失敗／修正來源，occurrence helper明確回傳；evidence_pins先嚴格驗檔再分類外部路徑，不擴大candidate/output路徑權限。未來runner仍使用canonical r28 immutable hardlinks，記samefile/device/fileID/linkcount，必要才獨立copy；來源本文／屬性不可寫且無assets junction。

重現小型檢查（封存後如需寫結果須新派工）：

```powershell
Set-Location -LiteralPath 'D:\lineage開發\TW1608262200研究\work\engine-r32-integration-r1'
python -B -X utf8 test_rejections.py
```

preparation-seal固定可執行接線、lock、schema、references、計畫與小型證據；result引用prep SHA，artifact-seal覆蓋完整交付。verify_preparation.py只讀有限pins，不掃14k素材、不執行產品。r31舊prep/stage/shared seals、release seal、根入口／manifest與讀取的封存tests/utility資料核對保持不變。

未讀未封存engine-slow-haste-item-r1或r31-baseline-r1，未輪詢其他工作者。無DB／服務／SQL／GUI／socket／原生或正常產品操作，無新chat/subagent、模型或平台變動。這不是r32功能接受，native/live與P24仍未完成。
