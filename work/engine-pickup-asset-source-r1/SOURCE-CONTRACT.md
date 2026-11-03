# Action15 定向素材來源資格

本包的 `metadataQualified=true`、`pixelsQualified=true` 只對固定42 profiles／13 gfx的已列entry與普通RGB555本體像素成立。`rendererImplemented=false`、`timingCertified=false`、`slowQualified=false`。沒有變更任何產品assets、manifest或程式，`productChanged=false`、`candidateBuilt=false`。

## 身分層次

finalr32 seal/baseline及先前action15來源seal、review seal皆核對固定SHA；42 profile加players/manifest共43檔對finalr32 release seal核SHA。本包未重掃14k產品素材。資料與BIN的發行版配對仍未確立。

archive-identity-history.json引用既有player-appearance-r1/source-records-r3.json的整檔pin；該歷史證據本身有source pin。17份目前小IDX重新全hash，用既有plain28格式與ASCII-fold比較進行1725個定向二分查找record讀取，未枚舉解包全部PAK。

Sprite04/05/06/07/08/09/10/11的目前IDX及PAK大小與歷史不同。首次extract因此中止，程式與失敗收據保留。後續沒有把歷史SHA套成目前身份：`archive-identity.json`逐項列current IDX SHA/current sizes/historical SHA及不相同狀態；`gaps.json`保存8組差異。**所有17個PAK目前整檔SHA均未重算；即使size相同，也不等於整檔hash已核。**

`entry-identity-layers.json`逐entry列兩個層次：

- historical-selected-payload-matched：目前slice與指定歷史原SPR實際bytes相同，或與已pin歷史來源record的raw SHA／offset／28byte記錄吻合；列具體副本路徑及證據，不由檔名推斷。
- current-idx-and-bounded-slice-only：只有本輪固定IDX SHA、record、PAK offset/size與slice SHA，再加獨立解碼一致；沒有歷史整包或同一original發行版本已確證的含義。

兩類都能作精確bounded資料來源證據，但不能混稱歷史整包身分已驗。像素一致、metadata alias、missing keys、archive identity與native timing是分開的結論。

取材遵守既有產品版本政策：main Sprite.idx/pak有entry時取main；不存在才查16個shard，必須unique bytes。本輪144個main entry與64個唯一shard entry，後者各只有一個匹配variant。此政策並非原客戶端runtime archive優先順序證明。

## Metadata與幀鍵

從既有解出的inner-list.spz（SHA e8cec0388d6e9fdf518493fba1674953141c023765e02c210c05a23dfbbe6242）按原來源工具同樣的CRCRLF正規化提取13個entry；每份entry與42profile.entryHash吻合。header `=sprite` alias逐一核對；無alias時sprite=gfx。只使用原`15.get`四token、directional=1、原sid+方向0..7與原frame index，不鏡像、不借其他weapon/性別/attack/spell圖。

sequence-map保存metadata token0..3、ticks[1,1,4,4]與事件`<206`，不執行事件。gfx2796明確從pickup sid48..55的frame1/2/3取圖，未擅改為0/1/2。其他四token依原entry。相同gfx的多weapon profile共用同一來源action15，不是視覺相似推導alias。

每profile32必要幀鍵，1344個profile鍵中336已有交付且bounds及RGBA重建相同，1008未交付但本輪定向SPR可取得。去重為104可重用、312需新增的frame；本輪208個SPR包含現有幀來源核對與缺幀來源，minimal-asset-source-list指定最小新增104 SPR/312幀。本輪沒有任何指定幀屬「原資料缺失／不支援」，也不宣稱已查盡archive其他幀。

## SPR、像素、透明與原點

原樣沿用已存在player_sprite_r2.py decoder，支援所選FF indexed與direct RGB555兩種格式。208個SPR中144 indexed、64 direct；plain28只讀opaque payload，不加外層解壓。entry stored/raw size相同；expandedRleSize是RLE轉為16-bit後的工作資料量，**不是PAK壓縮長度或raw檔案長度**。

每幀保留原header inclusive signed bounds、兩個reserved u16、tile引用／source offset／block index／sort key；PNG原尺寸為r-l+1、b-t+1，logicalAnchor=[-left,-top]，不是推測腳底或額外pivot。reserved欄位語義未知，不用作anchor。

blocks逐像素保留[x,y,rgb555,pixelByteOffsetInSPR,paletteWordByteOffsetInSPR]；direct像素無palette offset。從tile signed A/B依原公式x=24*(B+A-floor(A/2)), y=12*(B-floor(A/2))映射到header邏輯座標。原RLE未寫入位置alpha0，寫入位置alpha255；RGB555黑色0仍是不透明黑，共6752個黑色像素未被當透明鍵。

RGB555以(v<<3)|(v>>2)展開RGBA，不冒稱另一555→565顯示模式或原遊戲實機色彩。tile第三byte是sort key，不是blend mode；所選幀只有18/22/26/30/34/38。所選416幀沒有不透明重疊，因此磁碟序與升序key的像素相同。外部runtime的blend/effects/shadow與角色特例不在本包資格內。

獨立P04既有Lin.Helper.Core SprReader.LoadRaw用offset seek及另一個負座標算式重建。其frame外框涵蓋整個tile，本包保留原header；以共同邏輯原點對齊比較416幀、1,402,968畫布像素RGBA完全相同。未裁圖迎合reference。PNG/hash/bounds/tile map及pixel comparison皆可重做；13張contact只作視覺輔助，2x nearest預覽不改source PNG。

## 未完成項目

本包資料不接入renderer，不改pending heading或回包流程，不跑r33測試。native初始化先frame1、terminal回frame0，另有queue消費／取消與scheduler/input未知，不能拿四token總和當utility冷卻或因此開Speed2。完整action15 renderer及正常產品／live需後續另派；先前原生來源qualified=false仍有效。
