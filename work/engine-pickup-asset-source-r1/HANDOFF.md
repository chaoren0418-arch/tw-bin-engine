# ENGINE-PICKUP-ASSET-SOURCE-R1

source-only資料包完成並封存，停止寫入供主管及reviewer交叉核對。metadataQualified=true、pixelsQualified=true（精確entry／普通RGB555本體範圍）；renderer未實作、timing未認證、Slow仍不合格。productChanged=false、candidateBuilt=false，沒有產品資產或候選修改。

## 結果

42 profiles／13 gfx 的原15.get四token與8方向全部綁定，208個必要SPR共1,256,525 bytes。416個unique frame全可解析；獨立P04 managed decoder對齊1,402,968個畫布像素，RGBA差異0。所選幀無不透明重疊，6,752個原黑色不透明像素保留。13張contact sheet供檢查，人工抽看gfx0/2796/6650全8方向；完整416幀採數值驗證，未冒稱全13張人工看過。

按profile計1344個幀鍵：336可重用既有交付，1008缺交付但原SPR存在。去重後104個existing與312個new frame；最小新增104個SPR的312幀，清單minimal-asset-source-list.json。沒有缺原資料或解析不支援的指定幀。原profile沒有pickup sequence不等於原SPR不存在。

## Archive身份限制

初次抽取在shard大小檢查失敗，未隱藏或放寬成舊hash相符。8組Sprite04/05/06/07/08/09/10/11目前IDX／PAK比歷史pin多資料，詳見gaps.json。保留歷史wholefile SHA來源，重新核對17份小IDX全SHA、1725個必要二分查找record，且只讀指定PAK切片。沒有重算PAK整包hash，也不宣稱目前整包等於歷史版本。

目前208個entry都有確切IDX記錄、PAK offset／size／raw SHA；144取main，64取main不存在且唯一的shard版本。104個entry有歷史原SPR副本逐bytes相同，其中96另有既有player來源record一致；其餘104個entry僅有本輪current IDX＋bounded slice身分。entry-identity-layers.json逐項列明，兩類均不代表歷史整包已驗。這是既有產品main優先政策，不是原客戶端lookup precedence或BIN／資源發行版配對證明。

## 交付

- source-pins.json：185個直接核對來源；archive-identity-history/current與index-probes另列整包歷史／目前索引／slice界線。
- entry-map、frame-map、sequence-map、profile-qualification、pixel-comparison、existing-frame-comparison：精確來源及逐幀映射。
- raw/208個原SPR、png/416原尺寸RGBA、blocks/逐像素原檔offset及RGB555、contacts/13小接觸表。
- minimal-asset-source-list：未來資料增量清單，非產品candidate，不更改manifest。
- SOURCE-CONTRACT、VALIDATION-NOTES、validation、gaps、result與artifact-manifest：資格範圍、限制及重現。

finalr32 seal `46e9249224e768a1ac09fe99c08a9edf387833295022147aa046c206769fa507` 與baseline-final `0c645bf5c3a515fd5da88cf28c834a4b2967aed268946d45fb5a6ebe9f03fcb7`核對；43份選定player manifest/profile另對release seal逐SHA核對，未重掃整版14k素材。action15 source seal `8b1110e77614fbe8f9cb9b68718348c71f75d04110451cbca3c90699114125e1`及獨立review seal `29fb10cb6919a9ea07c13185af3ffc0eab2b8be4944c18093cbf84cb00326095`保持原件不變。

## 後續

可供下一輪獨立來源驗收及另派renderer設計；不可據此開Slow或把四token和當總utility時間。native初始frame1／terminal0、命令佇列／scheduler／input仍沿用原來源的未認證狀態，本輪未擴追。reserved詞語義、外部blend/effects/shadow也未推測。

重現命令見VALIDATION-NOTES；只能在獲授權fresh work執行，封存原件僅可verify.py唯讀核對。Goal實讀blocked未改；無新chat/代理、GUI/socket、正常產品、原BIN/JARhandler、DB/SQL/服務；r33候選保持封存且未重跑其自測。
