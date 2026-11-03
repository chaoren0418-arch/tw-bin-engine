# ENGINE-SLOW-REVIEW-R2

**accepted：candidateReviewed=true、candidateAccepted=true、unresolvedFindings=[]。** 此接受僅適用以下原封存 R2；R1 原 rejected 結論及證據不變。

- CandidateId：`ENGINE-SLOW-IMPL-R2`
- 原 candidate seal：`7bed792d86ec1838ce97e0efa935d061420fa56cf6c0435dbaba0f0c919859b9`
- 實際候選 DLL：`8ccfa4e3b5d253206779a5d4b1e59682263ffbddf5dab43fe15b0aa1f86a7397`
- result SHA：`fe8ec487ed8712e43e941f61453d0877d8730ff746ed415031f4cb625d4610b5`
- source-manifest SHA：`8938b63e49c7aff78f1a8baf7b1e7b3765de2bc4da7e16de8408b11db86bda5b`

## F1 已關閉

`candidate/source/NormalHaste.cs:35–36` 僅在遠端當前 Speed／Brave 已合法、舊 MotionSpeed／MotionBrave 無法計時時清除 MotionAt。保留權威終點及未知的原快照欄位，不猜測未知倍率。當前狀態仍不支援時不清除，renderer 繼續拒絕；合法當步快照不清除，動畫時長／幀／插值保持。

相對 R1 的完整 source/audio 實際雜湊比較只有 NormalHaste.cs 一檔不同；相對共享基底仍為原 9 個 delta。ObjectRendering、OnlineMovement、非步行動作閘門未再改動。

## 本覆核者親自執行的證據

| 測試 | 實際結果 | 來源與區分 |
|---|---|---|
| 原獨立 suite | 1,199／1,199 通過 | 從 R1 覆核封存複製 Program.cs／Extra.cs，本文不變；載入精確 R2 DLL。結果 `r2-probe.json`。 |
| 同一最小 F1 helper | 6／6 恢復正常 | 原覆核 repro/Program.cs 本文不變。當前未知期間仍 missing；恢復後無須新 S10 即可靜止顯示。結果 `repro-r2.json`。 |
| R2 F1 專項 | 112／112 通過 | 已逐段讀取、原樣複製實作者專項，在本覆核者 scratch 重新編譯執行。這 112 項是經覆核後親自重跑的實作專項，不冒稱獨立設計。結果 `f1-r2.json`。 |

1,199 項涵蓋原 BIN／JAR 獨立 vectors、0↔2／1↔2／同值、冷卻精確邊界、transient slow、排隊／已准入待完成、舊 login 回呼、非步行動作保持拒絕、本人與遠端合法當步／下一步。112 專項補驗 Speed3＋Brave1 的兩種恢復順序：只解除其中一項仍拒絕；兩項都合法才清除無效舊步行。也驗證立即／晚期靜止呈現、權威終點、下一步速率／幀／插值及合法快照經暫時不支援後仍保留。

三個 helper 的載入 DLL 副本 SHA 均等於上列精確候選；所有 console 命令 exit 0，逐項 assertion 全過。擴充 suite、repro、F1 均沒有觀測 socket events。最小六例另外記錄，不與 1,311 個 assertions 混算。

原 BIN／JAR 逆向不重做；沿用已封存的 36 組原 BIN、108 項 JAR oracle。沒有重跑不相關 1,371 項原實作 suite，也沒有重建完整產品。

## 身份與格式核對

開始與交付時核對 delivery 全 307 檔。source-manifest 的 176 項唯一來源（source 140、audio 6、web 30）均實讀核對 SHA／大小：source/audio 指向本候選，未變 web 指向 engine/web。9 個 delta 與共享 base SHA／候選本文一致。seal、result、source-manifest 的 candidateId 都是 ENGINE-SLOW-IMPL-R2，分別採 engine-candidate-seal-v1、engine-candidate-result-v1、engine-r29-source-pins-v1。未另行包裝候選。

`review-acceptance.json` 採 `engine-independent-review-v1`，綁定上述原 seal，並列本輪真實結果路徑與 SHA，供既有 r29 入口核驗。來源及產物核對見 source-manifest.json、artifact-manifest.json、preservation.json。

## 邊界與接續

本輪只寫 `work/engine-slow-review-r1/candidate-review-r2`，未改 R1 覆核證據、R1／R2 候選、共享 engine 或 r28。測試使用實際 DLL／renderer 與封存原素材，actor／封包／時鐘／sender 為明示合成；沒有 Form、GUI、socket、DB、服務或正常產品執行。真正 get_goal=null，沒有修改 Goal／模型或開代理。

接受範圍是 Speed2／Brave0 步行、轉向及物件顯示。緩速攻擊、施法、道具／裝備、拾取／丟棄及非行走滑鼠動作繼續拒絕，不代表已完成緩速戰鬥、原生完整流程或 P24。後續整合包裝須由指定整合者核對；沒有預先接受未知 seal。

已執行命令的精確 argv 在 r2-commands.json、focused-commands.json。可先執行 `python -B -X utf8 verify.py` 唯讀核驗。封存後不要原地重跑會覆寫輸出的 runner；另開核准 scratch 並轉接根／輸出路徑才重跑。此深度的專案根為 `R.parents[2]`，未使用實作者直接 work 子目錄的根推算。

本輪完成並停寫，交主管接續單一整合者串行合入。
