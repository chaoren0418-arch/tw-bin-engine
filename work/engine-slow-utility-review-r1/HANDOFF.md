# ENGINE-SLOW-UTILITY-REVIEW-PREP-R1 交付

準備已完成並封存；candidateReviewed=false。唯一寫入目錄為本目錄，未修改產品、releases或其他工作者目錄。沒有讀未封存 engine-slow-utility-source-r1。

最小契約是Speed2/Brave0的exact SupportedHastePotion單次使用；正向依據為固定StatusHaste.execute清除29/76/152後建立mode1。其他道具、裝備、交談、pickup/drop維持證據不足範圍，不一併放行。原生utility scheduler仍unknown；本機100ms與原服delay0分開。詳見CONTRACT.md、class-pins.json與native-byte-audit.json。

實際r30 DLL SHA256 `1abc1eed02917d14fd775be60920fe74efb9b102bc53eea5874fc0128e5247ef`；固定177個release source及其他輸入共195個pin。12個JAR class為新抽取javap，未初始化或執行handler。8個歷史native chunk重新按PE區段映射驗bytes，沒有native執行。

小矩陣90筆：78項契約判斷（60滿足、18個既有差異／未來增量）及12個NPC入口觀察。18不是候選驗收失敗。包括未知Speed/Brave入口、Slow初始拒絕、人工magic/ground/lease final防護、world/lease舊callback。兩方向共10個公開producer互斥案例成立；人工busy注入的可達性限制已標明。ActionClock、MovementClock均為實際Environment.TickCount64，沒有虛擬未來clock誤判item/ground。

主管補充的合法成功對照已加三案：admit後先收S255/S67及S24或S57，再完成sender，均成功並僅一次ItemSent。契約不要求completion重驗speedRevision/selection/item record；只隔離真正session/world/actor/lease失效。r30無法從Speed2初始admit，未來候選需補此起點與獨立actor更替。

建置成功（1個nullable warning，0錯誤），離線probe正常退出，socket EventSource事件0；build.log/probe.log留存。先前第一次helper因反射clock欄位名稱錯誤退出，修正為實際ActionClock後才產生probe；此為helper修正，未改產品。首次SDKfirst-use輸出自動開發憑證步驟已在契約揭露，沒有要求信任或操作既有憑證。

唯讀核驗：`python verify.py` 會驗artifact/source/class及90筆筆數。artifact-manifest.json不包含自身；排除assets唯讀junction、.dotnet/.nuget/tmp與harness/obj快取，保留helper實際binary及全部證據。不可在封存後重跑collect/seal寫入；verify.py唯讀。

本輪Goal觀察null且未變更；未碰DB/服務/SQL、GUI、原遊戲或socket，未跑1707全回歸。工作到此停寫，等待主管新的精確基準及實作／覆核派工。不能用此結果宣稱r31或任一未封存候選通過。
