# ENGINE-PICKUP-ASSET-REVIEW-R1

判定accepted，僅限列出的metadata與normal RGB555 body pixels來源；無blocking finding。來源封存b9033cb8c0767eccd9eb8c739b0eb6a42d48834f81960404e84e418b7dad8efc與1327 artifacts/185 external pins實際核對。

獨立從final r32 players manifest與原inner-list.spz重新抽取42profiles/13gfx/8directions/4tokens：1344 keys=336reuse+1008missing；unique104 existing+312new，需208 SPR，其中最小新增104 SPR/312frames。逐profile alias/entry hash、32keys、bounds、signed logicalAnchor與所有映射吻合。2796保留sid48..55的frame1/2/3，未改成0/1/2。

重新二分查找17個整檔SHA固定的小IDX，1725個unique records與來源逐byte一致；208筆selected slice共1,256,525bytes，144main、64main absent且shard唯一。沒有全PAK讀取/hash。Sprite04–11大小與歷史不符，初次abort證據仍保留。104 raw payload與歷史副本一致，其中96另匹配player source records；另104只有current IDX/offset/slice SHA。這不證明whole PAK歷史相同或與BIN同發布版；main priority只屬既有產品政策。

未import作者decoder，另寫offset-seek/sparse-plane decoder與parity座標算式，208 SPR＝144indexed+64direct，416frame逐RGBA/PNG、header/tile/block/offset/palette-word資料一致。原RGB555值0保持alpha255，共6752 black opaque；unwritten alpha0，selected frames無重疊／越框。336既有profile frame以final產品block pixels另重建一致。

重新執行P04既有managed SprReader.LoadRaw，416輸出與原封存reference逐byte相同；依共同logical union bounds直接copy rows比對，1,402,968 canvas pixels零差異，未crop/recenter/alpha-mask迎合reference。P04完整tile外框與原SPR header不同，logical alignment正確。managed helper build0警告0錯誤，不是原BIN/native執行。

實際視看本review重新生成的gfx61/48/2796/6650/1186圖板：各8方向×4token，共160幀。抽樣未見裁切／漏條／錯位；沒有聲稱全416人工視看或timing語意通過。視覺僅輔助，數值全416已比對。

主要證據：metadata-audit.json、archive-audit.json、decode-audit.json、reference-comparison.json、visual-review.json、findings.json、review.json/result.json、source-pins.json與artifact-manifest.json。

唯讀核驗：`python -B -X utf8 <this-review-root>/verify.py`，核artifact/pins及208 bounded slices，不全PAK掃描。獨立重算只在新授權的work sibling根複製audit.py、compare_reference.py、reference資料夾、run_reference.ps1、NuGet.Config後依序執行audit.py → run_reference.ps1 → compare_reference.py；不需執行作者腳本。舊封存root所有writer均拒絕再寫。

metadata/pixels資格不涵蓋renderer implementation、native input/queue/scheduler/timing parity、runtime blend/shadow或Slow開放；原pickup source qualified=false仍有效。未讀尚未封存研究，未改產品、archives/attributes、engine/releases或主管；沒有DB/SQL/services/GUI/game/socket。此項完成封存後，才依另派工接續獨立scheduler review。
