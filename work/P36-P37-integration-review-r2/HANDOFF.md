# P36/P37 r2 有限差異獨立覆核

結論：accepted-r2-plan-P37-F1-closed。plan-review.json 為 p36-p37-plan-review-v1，decision=accepted-for-p36-serial-source-build。這是精確準備計畫接受；共享執行仍須主管另發 execution receipt，真正 P36 build／compiler provenance 與 P37 正向 inventory 仍待執行後覆核。

精確綁定：

- plan：f67fa56815fbc133c1710c450efe1305d58da7c75566e02a9d909a24ca6059bd
- contract：f439915614397edb8a478196375706aec1af68b670bd06c310d1ba3e251e8c30
- author artifact seal：d72f2c75fc792df44a5e035651a82e5cb2244d944d42564e2231c7e15a1a30c7
- author artifact manifest：b106754c71c98a5df611a8b3aeed7f2afefb85159887ae146c2ee577f746667d
- tool source seal：ea73adff65e8887e1b3369147a8c8c22a980f32fc5a6f8cba5868e9b860ad172
- bound_inventory：3bb2bc7a22c486dbfe81b0c8d08c04e18b80c5cda5473dc46b17e319af8e61a1

862 個成員與完整檔案集合均核對，含原 r1/reference 歷史證據，未當成新增執行。P37唯一runtime改動精確等於將 bundle/protected/source_state 一行換為 validate(plan_sha,contract_sha,initial=False,dependencies=False)。其餘12個runtime/reference bytes與r1相同。獨立推導七份JSON的允許轉換，僅r2根、工具版本及指向新副本的size/SHA重綁；不是只相信作者delta摘要。五個target、fresh EXEC/build/manifest、expectedDLL、single build、receipt path、禁止區和failure policy不變。

新增兩項限定查核：

1. 本review自行複製protected-pins，刪除project-manifest一列，保留原契約expected size/SHA。執行從未改gate擷取的原inputDocuments迴圈，只把路徑別名指向本review副本，確認pin drift立即拒絕。真metadata未改；沒有造成功主管receipt。
2. AST確認P37呼叫post-install validate，inputDocuments pin loop早於bundle/protected/source_state。直接呼叫真gate.validate(initial=False,dependencies=False)，確認驗完清單後到達after181檢查，並以exact canonical source set after拒絕目前before180。這是預期拒絕，不冒稱實際安裝成功或繞過P37 receipt prefix。

P37-F1因此關閉。P36未改部分沿用r1已封存的copy/allowlist/rollback/private build/entry保護審查及七項唯讀查核；不重做347 stage基準盤點、舊16案例、遊戲套件或4350 SDK全面重驗。副本／封存成員核對是證據閉合，不是產品測試。

實際命令與原始輸出/exit見commands/delta-review-01、commands/finalize-01。run_recorded.py由r1獨立複製，原件未改。delta-review-01 exit0；兩項新查核預期結果均符合。最初探索讀取delta.json曾在對話輸出遭截斷，沒有以截斷輸出作結論；正式腳本完整載入JSON並獨立比對。

封存後可唯讀重驗：

```powershell
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P36-P37-integration-review-r2\verify_delivery.py"
```

一次性review_delta/finalize不可在封存根重跑寫入。source/artifact seals於命令stdout/stderr及exit落盤後建立，只排除外層manifest/seal以避免循環。本r2只寫自己的review根，所有r1封存保持原bytes。

下一步由主管核發綁定本plan-review receipt及r2 plan/contract/tool/artifact pins的P36 execution receipt，再由唯一整合者串行安裝與單次build。不能因此先跑P37：仍須真實P36成功、獨立compiler/source provenance覆核、主管P36 final acceptance與獨立P37 inventory-only receipt。

沒有共享寫入、build、rollback、產品測試、DB/services/native/憑證或入口變更。正式入口r33；Goal實讀歷史blocked且未修改。

原入口拒絕保留且未重試/改道：ENGINE-R34-ENTRY-ACTIVATE-P26-R1 send_message_to_thread automatic approval review denied。原文：「此派工會持久修改正式入口與 project-manifest，將 r34 提升為現行產品版本，屬外部／正式承諾；使用者只授權引擎開發，未明確授權這次入口啟用，且政策不以先前助手派工替代該核准。」
