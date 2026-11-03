# ENGINE-PICKUP-PRESENTATION-IMPL-R1 — r34 isolated candidate

已完成有限 own 拾取動畫候選。封存後停止寫入，交主管安排獨立覆核與唯一整合者；未正式合入／發布 r34，未完成正常產品或原生驗收。

## 行為

依 `OWN-BASE-15-001-SUPERVISOR-R1`，僅 own gfx0/weapon0、Speed0/1、Brave0、八方向。真 LoginConnection.ApplyWorldPacket 帶 session generation 進 ReceiveOwned；真 S158/action15 在原 heading／pending receipt 處理後逐筆呼叫新 hook。最後合格回包替換獨立 presentation instance，remote／unknown action15 不取代 own。未綁定 renderer／session 的回包保守跳過，沒有佇列補播。

捕捉 session/world/map/actor、appearance/correction/speed revision、gfx/weapon/direction/X/Y 與單調 instance serial。收包結束與每次呈現重驗；衝突動作、移動／轉向、校正、外觀／速度、死亡／HP0、shop、鎖定、移除／reset 等取消。舊 serial 回呼先拒絕，不能清新 instance。取消完整性是引擎政策，不宣稱 native action8 queue clear 一致（來源存在 null-entry skip 限制）。

frame metadata index `1→2→3→0`。首期限 `N+D1+1`；後續一次 due callback 只前進一幀，`due=now+max(1,D−lateness)+1`、`baseline+=D+1`。terminal frame0 從實際該次 callback 時點 hold584ms，之後只移除自身 instance。Speed0 理想時點0/42/209/376、結束960；Speed1為0/31/156/281、結束865。這些是可注入 monotonic64 clock 的名義政策，未認證原生輸入時點、pump cadence、32-bit wrap、variant/FIFO/private-neutral。

OwnCombat 與 WithProjectileHit 的既有 active-window 判定抽成同一 selector，renderer 與 admission 共用。保留 double duration、原 combat report、timing/profile match 與死亡優先。damage583.3333333333334：583仍顯示damage、584才到期。已過期的非null history不再永久擋住拾取。walking與active attack/hit/spell/death優先；新的衝突動作直接取消拾取。

## 產品與資產

177 個原始來源均為獨立副本；10個原檔修改、167個不變，新增2個C#及1個resource，完整 source manifest **180檔**。Ground.cs 的完整 LoginConnection send/completion 本文與r33逐字一致，保留 C112 bytes、900ms click guard、3000ms pending、權威數量及r33 owner lifecycle。r32 inventory／haste 實作不變。

24個新增body frames嵌入239,766 bytes resource，SHA `43b88cec329929484610aceae84566d1a922eb4b8ceba93e17dc44b65922ca90`；重用8個已pin `objects/0.json` frame0，沒有重複嵌入。`project_resource.py` 從accepted JSON重投影，未重新decode SPR。`resource-projection.json`保存32幀來源範圍與身份層：24 current-idx-and-bounded-slice-only、8 historical-selected-payload-matched；不是整包archive/BIN版本等價。

body採原block anchors／Z／source order。覆蓋前移除自己的舊body及idle shadow，再用既有exact-key shadow lookup；沒有合格shadow時不借用其他姿勢。既有 Blit、depth comparator、PlayerShadowCatalog、players manifest全不變。`<206`等事件仍在authenticated metadata，沒有新增音效／effect dispatch。

實際Release產品DLL：`c11678249221475fb749de41713f8ed75473980d8929d72a8cd04f2ca0b49aed`。

Audio DLL：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`，與r33相同。產品與helper build均零警告／錯誤。沒有啟動正常product entrypoint或建立ClientForm。

## 證據與限制

- 候選 **339+63=402** 次成功檢查，401個不同ID（兩套各有no-socket-events），400功能斷言＋2 socket guards。原r33精確DLL另有18列（17既有戰鬥邊界斷言＋1 guard），不累加候選數。獨立review-prep的84列、歷史4337等均不算本候選檢查。
- 真產品DLL、真ComposeObjects／Blit／BitmapOf、真Assets建構與完整唯讀場景。64個speed/direction/frame組合共 **213,696像素** RGB555/mask/owner/Bitmap 比對零差；原body/shadow指令逐項比對，32張PNG留存。含2,650次來源不透明黑像素出現；未以黑色當透明。
- clock、初始登入狀態、封包、延遲sender及部分防禦性revision／actor置換均為離線注入，不是實服可達性或牆鐘認證。projectile cache injection明列，另有尚未compose的實際ProjectileReport路徑；不是原生飛行測量。
- 其餘41個已知profile、Speed2/17/18/255、Brave、各種失效、burst、late/min2/terminal、remote回包、r33早回包與跨world舊error、r32緩速使用加速道具皆有針對性測試。
- renderer讀immutable stage assets；其建構驗證653個non-map檔，共628,312,728 bytes。AppContext ../../assets另有6個最小獨立副本，約30MB，詳細見runtime-dependencies.json。沒有整包copy、新junction、硬連結修改、素材或manifest改寫。
- 無GUI/game/native BIN/JAR/socket/DB/SQL/services執行。沒有音訊device測試；無新dispatch為source/data-flow與audio DLL不變證據。未知私有旗標、native timing/FIFO、全部remote狀態排列、正常產品與同版全流程均未認證。

`coverage.json`逐列對應36個原提案，COMBAT依主管active-window override修正，LIVE-SEPARATE仍pending。實際gfx0 base frame都非空；未假造透明base profile。最後一輪僅補上未涵蓋的針對性case，未重跑全部歷史suite。

## 基準與封存

原immutable r33 stage baseline SHA `38956c96dfa7e6c7360fade71f8c095323990c2d5d24cb48395073457459b1ad`保留。final baseline SHA `2a946e965f1aed1f9a0358ceb560f52575ea5a3bf217fc9ccf36e9ce5823c40f`、binding `28d0d6f3386575c175aa06b7b9076da961cfd1db62e2563cf7cd65c7c276ea95`、release seal `846302448dc4a1421a2689b96632978353b6e39b1c9f8a9331bdd23b7760a417`已實際逐項核對177來源及四身份相等（370 pins）。因此只追加binding，沒有重建或重跑已通過測試。主管全14653檔audit只引用，不冒充本worker重掃。

source-manifest／changed-files／changes.diff／delta提供完整來源與精確差異；input-pins、resource-projection、final-base-binding-pins固定證據；delivery-manifest封存交付。`verify_delivery.py --seal SHA`可唯讀核對；重現步驟見VALIDATION-NOTES.md，禁止原地覆寫封存候選。

真Goal於本次派工與封存前均讀為blocked，未新建／恢復／修改。無新chat／subagent／模型變更。唯一寫入範圍為本work root，shared engine／releases／主管／其他work保持唯讀。

獨立review-prep只作原基準參照：seal `db2267a456ff5d802ec639b2819251c151407670ad26de5c7a7f85cfefe24229`、其final binding seal `f0bbf30212a1aecf6e677b55effe95dce31bda1d629799c917ed61bc489dddde`另行pin；不是本候選accepted，不把其84列納入402。
