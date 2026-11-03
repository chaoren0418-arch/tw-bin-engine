# P25 交接：r33 架構與可驗收責任基線

本 P25 產物已完成，待新主管獨立審閱。這不代表 P24 或整個引擎完成；正式基準仍 r33，native 未驗。P25 不等待 r34 才能完成盤點。

## 交付

- architecture.md：實際資料流、partial 類別耦合、歷史邊界與現行責任。
- module-map.json：11 模組，各有 source path／符號行、owner、資料流、相依、驗證、unknown、下一步、完成標準及承接階段。
- completion-matrix.json：舊32列完整原值，加兩份舊文件各7 remaining＋1 direct fix（16補充 occurrence）；未縮分母、未算完成率。
- finite-backlog.json：12個有限項，分開 integration/review、source-blocked implementation、native、外部條件及歷史限制。
- source-pins.json／source-binding.json：523個來源 pins；r33 accepted177來源逐筆對 engine 與 sealed release 相符。
- historical-evidence-index.json：120筆歷史引用全部解析並 pin；原生版本保留原標籤。
- verification-report.json／result.json／artifact-manifest.json：本階段靜態核驗與封存；counts 只描述盤點範圍。

## 主管可採取的下一步

1. 用下面命令獨立核驗來源及封存，再審閱32列與16補充的 r33Assessment。
2. 接受 P25 架構基線，不把 r27/r28 native 改記為 r33。原生驗收、缺源實作和條件限制維持各自 backlog。
3. P26 提供 exact r34 計畫／主管身分 gate／raw log／回復的覆核後，新主管才向原整合者發精確共享 receipt；P25 不替 P26 宣告通過。
4. P27 的輸入／HUD 覆核獨立進行；只有可重現缺陷可形成候選，缺 native 證據不當 bug。P27 不碰 Ground/WorldEntry 拾取範圍。

產品整合 owner 為 01a17a22-17e4-70c0-9792-166ddb86ea42；新主管 01a185b4-ecf6-705c-90e6-83b9eb1ba02f；本 P25 產物 owner 為 01a185b7-ddd1-79f0-98fe-fa456008cbd3。沒有聯絡／喚醒退役主管、沒有增開代理。

## 重要對帳結論

6016 的舊 pending integration 文字已過時：r28 current6016 與 r33 實際來源顯示 controller/audio/host 已整合。正式世界 native 音訊、其他 event tokens 與原 scheduler 等價仍未驗。MAP-F4 有實作缺口但缺真實非零 group association 契約；不能猜補丁。ground36 是已解歷史封裝缺陷；NPC/交易/倉庫、HUD/chat/pointer/shadows 不能再稱整類未接線。

r34 stage seal：9544f137a10914825b53080d4d390d17902cd40866467cebbf69578290240018。

r34 canonical plan：d25c306fae5abee9209e46c557453f7b6a0a96645e5c052fbda70555f11f15ff。

r34 stage／plan 不能當 canonical build 或 native 成功。41 profiles 僅規劃，需 final base＋新契約；312 pickup影子key缺口仍保留。這些 downstream 依賴沒有阻礙 P25 的文件產出。

## 可重跑命令

```powershell
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P25-architecture-baseline-r1\verify_baseline.py"
```

重新產出同一基線文件的順序（只寫本 root）：

```powershell
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P25-architecture-baseline-r1\build_baseline.py"
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P25-architecture-baseline-r1\finalize_baseline.py"
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P25-architecture-baseline-r1\verify_baseline.py"
```

核驗器只讀 JSON／檔案 hash；不匯入或執行產品程式。finalize 只呼叫自己的文件核驗器並寫交付封存。產品 build=0、產品 test run=0、native=0、DB／服務／socket／GUI=0。不是以驗證項目數宣稱全案完成。

Goal 已實際 get null → create → get active；不冒用文件當平台 Goal。完成封存核驗後才呼叫平台 complete。turn ID 工具未提供，progress.json 明記 null；task ID 來自 get_goal 真實讀回。
