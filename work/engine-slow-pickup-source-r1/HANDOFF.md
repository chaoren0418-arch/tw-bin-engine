# ENGINE-SLOW-PICKUP-SOURCE-R1

來源研究完成，封存後停寫。本輪沒有產品修改、候選建置、原生／handler執行、GUI/socket、DB/SQL/服務操作；不依賴r32候選，也不代表P24完成。

## 結論

尚不合格直接開放Speed2拾取。已把原缺口縮小：固定server正向鎖序列化提交與S158/action15成立；原BIN以opcode226解析同語義actor/action，在A3EBF0=false及其他所列條件下，經92CB70→vtable+4C→A467A0→A43560→A3EF40→A407D0使用共用逐幀speed分支。A3EBF0=true則可能經+64/A3C4F0排入actor命令佇列，普通base為空實作；queue消費／取消尚未追完，不能聲稱所有狀態立即播放。這不是原BIN輸入限制或完整utility時間已還原。

42個現有合格profile的原entryHash全部相同，13個gfx全有15.get四token，[1,1,4,4]ticks；產品42份皆缺pickup sequence且沒有完整所需幀。metadata/pickup-frames.json列精確sprite/sid/frame缺口，可接續小範圍素材資格工作，不用重解全PAK。沒有生成替代動畫。

時間不可直接取四token總和：native初始化frame0後先++到1，非末幀會排frameTime+1且補償lateness，terminal再取frame0時間交A44500。逐幀Speed2×2的共用算術有來源，但完整utility scheduler窗／輸入起點仍unknown。900ms只是r31 local guard；伺服器鎖序列化不是已證固定時間延後。

## 交付與核對

- CONTRACT.md：完整正向鏈、bytecode offsets、VA條件、未合格理由及下一最小範圍。
- source-contract.json：qualified=false；分開server允許／native條件支援／engine政策／live未驗。
- proof-index.json、method-index.json：逐論點及class方法定位。
- native-byte-audit.json：101個採用byte chunks逐一比固定BIN，列VA/RVA/file offset/size/SHA。探索舊+80假說明確排除，不當正向證據。
- source-pins.json：68個外部來源pin；collection.json含17份class精確entry與javap argv/exitCode。
- metadata：42profile/13gfx、原始對應entry與缺幀清單。只查相關metadata，沒有素材重製。
- validation.json、result.json：純靜態驗證；runtime checks=0，不混入舊36/90或377/3171。
- artifact-manifest.json：封存本文、證據、腳本；不列自身與tmp。

最終r31 baseline SHA `00e9d4664024119595faeead50ade5827258763e47dc3d7068ea9b78abf8c813`；release seal `1d5b1c149495cb2b44f50daf2c3361eb15e8000ba27bf133d1029735f10cfd90`；封存前另核177 release來源仍全部符合。固定BIN `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，JAR `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。

## 可接續工作

下一最小工作先核指定pickup SPR slices／方向／frame bounds／pixels，以及A44500 terminal transition、A228B0剩餘clock路徑和A3C100/A44B60/A4BF00命令佇列消費／取消連結。原BIN輸出pickup caller及輸入禁制仍待定位，不能用JAR112套BIN。若要採僅server-authoritative請求、保留動畫未還原的縮限產品契約，須由主管另行明定；本交付不授權改gate。

r31 Ground有獨立slow gate與pointer gate、900ms/pending3000ms政策、action15只確認朝向而沒有動畫；後續ground的queued busy與world/actor/lease完成歸屬也需另案修補。丟棄、自動撿物、全物品、新NPC/buff不在此輪。

## 重跑

不得原地重跑封存目錄。複製本輪腳本至覆核者獲准fresh work，保持研究根下work子層結構，建立各腳本要求子目錄；依序執行collect.py、metadata.py、native_scan.py、scan_handlers.py、find_action_receiver.py、native_details.py、extra_static.py、finalize.py。Python使用 `C:/Users/CHAOREN/AppData/Local/Programs/Python/Python312/python.exe -B -X utf8`，Java僅javap靜態反組譯。腳本不啟動產品／server，不執行class/native，不解PAK。全部驗證成功後才可在自己的新work以finalize.py seal封存。原件及既有候選全程唯讀。

自己的真Goal實讀blocked，依派工未新建／恢復／修改；沒有新chat/subagent。等待主管交叉覆核。
