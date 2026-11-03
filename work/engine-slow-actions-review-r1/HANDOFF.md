# 慢速攻擊／施法：獨立準備檢查點

**狀態：preparation-ready；candidate-review-pending。這不是本任務或全案完成，也不是候選接受／拒絕。** 主管收到精確封存候選後，在同一覆核對話續派；不無限輪詢未封存實作。

基底為已接受 r29：seal `d3ac187e7fc44745864640e4bcc72fefbaeeaa0b0e9d63de7c0c3cfa8e46273d`，DLL `8ccfa4e3b5d253206779a5d4b1e59682263ffbddf5dab43fe15b0aa1f86a7397`。共享176來源逐檔核對主管 baseline。原 BIN／JAR、素材 integrity 與42 profile SHA均核對；只寫本 work，沒有碰 DB／服務／正常產品／GUI／socket或共享引擎。

## 已交付的獨立證據

- `CONTRACT.md`：action1／18／19原呼叫鏈、native動畫／server cadence／reuse／32ms及100ms本機餘裕／release／30ms與60ms飛行的界線，以及候選生命週期驗收政策。
- `native-byte-audit.json`、`native/`：10 個原BIN函式精確bytes、VA/RVA/FileOffset及偽碼副本。不是啟動或附加遊戲。
- `native-action-oracle.json`：270 組原x86 frame計時向量，action1／18／19、frame0／1／7、10種基底、speed0／1／2。metadata及中性狀態callee是明示替身。
- `action-metadata-vectors.json`：42 profile、126 action rows；server base與動畫逐幀值分欄。`server-action-vectors.json` 提供630組既有profile／技能／速度預期及3組reuse辨識向量；這是來源推導，不冒充再執行JAR。
- `harness/`、`run_probe.py`、`r29-probe.json`：實際r29 DLL／renderer，**210項：160成立、50重現本增量缺口**。25項慢速真實請求被拒、5項已准入後transient slow未保留延長、18項慢速動畫／當步／結束邊界、2項release仍用基準等待。50個false不是候選判決。
- 正常／加速舊送出、45項排隊狀態變化、動作與移動互斥、正常renderer、獨立30／60ms飛行及無socket事件均有具名結果。尚未以合成結果宣稱原生實機通過。
- `race-vectors.json`：現有實跑與候選後計畫分開，特別列server ack、雙向切速、同值、未准入／已准入待完成、各種stale callback及跨動作互斥，不把尚未跑的計畫算已通過。

## 給實作與主管的具體判準

施法是 `max(2×serverActionBase, reuse)+100`；弓的32ms另加；不能把總delay全部乘2。actor marker前等待放慢，箭30ms／光箭60ms飛行與附著特效各自保持。

三個既有gfx0近戰動畫profile（weapon0／4／50）仍以800／960／1120正規化，原ticks逐幀整數總長832／997／1163。此差異已明列，不要求偷偷改Speed0／1；候選須明示Speed2動畫策略，必要時適配preparation helper，不能以helper預期冒充native來源。

release相對倍率與飛行獨立有原byte鏈支持；本輪沒有完整重播原actor scheduler／parser，因此不額外認證r29的791／916絕對marker起算值（frame先遞增與+1排程）完全等同native。若候選宣稱新的絕對native release，最低補證與supplier列在CONTRACT；不足只限制具體投射子項，不能阻擋有證據的近戰／既有非飛行魔法。

## 環境與重跑

所有helper均console，合成actor／clock／packet／sender，讀原素材，沒有Form。DOTNET/NuGet/TEMP輸出全部本work；三個小metadata為獨立副本，候選／基底DLL不原地寫。最初release seal位置假設不符，已按r29 HANDOFF解析到`_integration/r29/release-seal.json`；初版遠端StepMs斷言改為真實幀／終點，沒有改產品。

實際命令與exit code見r29-commands.json。可先執行 `python -B -X utf8 verify.py` 唯讀核驗。準備封存後不原地重跑產生覆寫；後續候選使用新子目錄副本並調整專案根解析。

```powershell
python -B -X utf8 prepare_evidence.py
python -B -X utf8 run_probe.py
```

真get_goal=null，未重建或修改平台，未開代理／chat或改模型。本輪沒有讀未封存候選內容；最後一次僅檢查delivery-manifest是否存在，尚未有可讀封存manifest。收到精確seal後續做明確accept/reject，採engine-independent-review-v1收據。
