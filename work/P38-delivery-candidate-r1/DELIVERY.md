# P38 開發候選交付界線

此候選包含已接受 P36 的完整181份source/audio/web（含30個web檔）、15份原樣產品輸出、13,957份既有素材與索引，以及新綁定的正常產品manifest與候選launcher。它不代表正式r35啟用，也不修改root入口或project-manifest；正式入口仍為r33。

- r33：目前正式入口使用的封存版本。
- r34：既有封存release與舊launcher，產品DLL為c11678249221475fb749de41713f8ed75473980d8929d72a8cd04f2ca0b49aed；未含新增41組own-pickup profiles。
- P36：已接受canonical來源與產品DLL6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3，額外41組、含control共42，speed0/1、Brave0。
- P38：上述P36產品的隔離交付候選與新launcher綁定；舊r34入口批准不會啟用這份P36成果。

正常產品manifest為build/product-p36-delivery-manifest.json，schema p24-normal-product-v1，保留profile p24-real-jar-d287-client2、port42061、autoLogin=false及唯一參數--normal-product。它與_delivery/evidence/P36-provenance-manifest.json用途不同，不能互相冒充。

候選launcher的本輪資格僅限check-only：驗證46個runtime/web/index檔、13,957個asset檔存在與大小，查詢已安裝.NET10 Core/Desktop及WebView2版本後返回。WebView2 loader只作版本查詢，不啟動browser。完整素材body SHA由獨立final review讀取候選驗一次；check-only本身只驗素材presence/size與索引hash。

素材body是既有immutable實體素材的硬連結，僅允許唯讀消費。禁止原地修改內容、ReadOnly屬性、ACL、mtime或其他共享檔案屬性；也不要以設定唯讀屬性代替保護，因為這會影響原封存的同一file identity。素材索引、source、binary、manifest、launcher與其他可變檔為獨立nlink1副本。候選不得包含junction／symlink。

來源與重建支援：根目錄global.json固定SDK10.0.300，NuGet.Config只含本地vendor，vendor保留已接受WebView2套件；source/OfflineClient.csproj引用audio/P24.Audio.csproj，兩者原樣保留。_delivery/launcher-source保留本候選launcher原碼。_delivery/evidence保存P36真實build命令／raw log／binlog／來源manifest／SDK pins與P36/P37接受證據，足以核對產品source→DLL來源。

封存的P36 build contract包含歷史絕對路徑和權威條件，只作證據，不是可直接套用本候選的重建授權。未來重建須另行明確派工，在fresh work與私有obj/cache/temp中機械重綁路徑、保持來源與deterministic PathMap及exact DLL期待值。不得執行舊r34 build-product.ps1來聲稱重建P36；本候選不攜帶該舊入口。P38本輪禁止再編譯產品。

本輪不證明正常/noargs launch、UI呈現、登入、原生操作、DB／服務或交易、完整regression及網路整合。不得啟動正常branch、繞過手動登入／使用者服務條件，或把歷史相同DLL測試數計為本輪新增。新launcher hash須在唯一一次build後記錄；check-only與獨立review完成前不能宣稱候選資格通過。

候選只有新launcher與新manifest綁定，不提供任何root入口切換、發布或project-manifest更新操作。原入口啟用的自動核准拒絕保持有效，未重試或改道。
