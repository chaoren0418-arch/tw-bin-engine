# ENGINE-PICKUP-RENDERER-ELIGIBILITY-SOURCE-R1

本輪完成來源／實作契約，沒有產品或候選實作。`runtimeChecks=0`、`productChanged=false`、`candidateBuilt=false`。基準只用 final r32，不使用尚未共享的 r33 候選。既有 Goal 實讀 blocked、未修改；只寫本 work，prior roots、engine、assets junction、releases 與原件均不寫。未執行／emulate 原 BIN/JAR，未操作 GUI/socket/DB/服務；沒有子代理或新 chat。

**可立即派往下一個實作增量的是明確標示為 nominal 的有限 presentation 政策。** 目前沒有任何一列能證明「完整 native 中性」，但這不阻擋以下有限政策的實作與離線驗收，也不需要先無限追完私有旗標。

## 交付與判定

`eligibility-table.json` 有42 profiles × own/remote＝84列；每列列出23個 native 條件、對應引擎欄位、證據狀態、缺口與採用/fallback。`condition-catalog.json` 說明逐條邊界。strict native eligible=0，第一增量 policy adopted=1列：自身 gfx0/weapon0，Speed0/1，8方向。其他41個 own profile 與全部 remote 是「本增量未採用」，不是素材不合格。

12項來源主張在 `proof-index.json`，native bytes 有 VA/RVA/File Offset/hash，engine snapshot 有檔名與行號 anchor。`metadata-evidence.json` 保存13個限定 entry、42個 base15/idle 配對；這是文字／結構證據，沒有重新解碼 SPR 或重跑像素比較。

## 本輪關閉與保留的來源條件

- variant 虛擬目標已關閉：`505880→5057D0→5055B0` 的 manager vtable `114F8F4`，+4=`5162C0` 建立 `7BD7F0` backer，vtable=`1172574`。`516290/+3C→7BE780` 查存在；`5161B0/+44→7BE630` 取第一筆匹配的 variant ID；`516180/+40→7BE570` 取最後一筆。`5058F0` 在 action 改變時把 helper variant 設為-1，`505920` 賦值；末幀 `66D3B0→5058A0` 會遞增並在超出正的最後 ID 時回到1。這比「virtual target 未知」更具體，但沒有證明每個 gfx 的 runtime map 內容，也不能假設 ID 必然連續。
- 13份 metadata 都有唯一 base `15.get`、唯一對應 weapon+3 idle、`102.type(5)`。全部42對 weapon 為0/4/20/46/50，排除28..39、weapon32、weapon+3==15 與 weapon+2==15。**文字 type5 尚未連結成 native `13CD838`/`13CD878` table 的確切 bytes**；class11 在 selector 開頭會直接以 terminal callback1 拒絕 action15，另一 table 還可改 weapon0/11，故不能省略此缺口。
- `501260` 只對 gfx6094/6080 為真，13個目標均排除；`8CA110` 則涵蓋全部13個目標。+70 已解為 actor+0x60 getter；普通收到 action15 的 immediate path 會先清此值，因此當下不受 action70 override。但 queued selector 執行前可有新狀態，不能推成所有排隊命令永遠排除。
- `A4ADD0(action15)` 為 false。額外時長條件仍包括 fixed/FPS flags +0x258..0x25B、+0x220、+0x223、+0x268 的 secondary bits、+0x26C；這些都沒有已證明的完整 engine 對應。Brave0 **不代表**全部中性。+0x218 的 weapon+2 條件對42個 base weapon 不適用；+0x21C 的 action集合不含15，可以分別排除，無須把每個未知欄位都列成一律相關。
- own 可避開 native nonself 的 queue-depth 時長因子與 remote1000ms deferral，但 self encrypted status/gfx、terminal self guards、+0x5C、variant、native FIFO 仍未完整對應。remote 的 `WorldObject.Action` 是 base profile lookup 使用的原始欄位，不能拿 `ReceivedAction=15` 當 weapon15；`Flags` 的PC位也不能證明 native class。

前輪 scheduler seal `aecf48c342d082c17520351f992a387b880dc980ab4c500143036ce35f86b07a` 僅已通過來源完整性核驗，獨立語意覆核尚待；本輪不把它提升成 native timing acceptance。native map 的 loader/全程序寫入者、完整 private flag 封包生命週期在此停止，不擴大到 C112 native input 全鏈。

## 第一個有限增量 OWN-BASE-15-001

完整可實作規格是 `presentation-policy.json`。在每筆 authoritative S158/action15 的 `ReceivePickupAction` 處，先保留既有 heading/receipt 處理，再觸發新的 per-receipt presentation hook。不能只輪詢 `PickupActions/LastPickupActor`，因同一畫面更新前可能有多筆不同 actor 回報。

只接受 current own id、gfx0/weapon0、Speed0或1、Brave0、方向0..7、有效資源且 Ready/Renderable/alive/無 pending appearance；第一增量保守要求 `World.Walking=false`、`OwnCombat==null`、`!ActionLocked`。`OwnCombat` 可能在視覺期限後仍非null，因此這個保守政策會略過部分可顯示場景；這是已知的功能限制，不是假裝判斷了 native idle。不能用 `NormalSpeed.Supported` 直接代替 Speed guard，因該方法還接受 Speed2。

capture session/world/map/actor/appearance/correction/speed revision、方向位置及新 instance serial。依 accepted base 素材呈現 index1→2→3→0；採前輪中性條件下的逐幀 +1／遲到扣除算式作 **nominal** 排程。terminal frame0 保留584ms，然後只移除自己的 instance，還原當時現有呈現。584 是選定普通空 queue 分支的有限呈現政策，不是 native 每次實際等待、輸入鎖或伺服器冷卻。

新的自身 action15 取代舊 instance，這是明示的 latest-receipt 政策，**不是 native FIFO 相符**。新攻擊／受擊／施法／死亡、移動、方向位置校正、速度或外觀變更、owner/world/session 失效、資源失敗及 guard 消失都取消舊 instance；延遲 callback 必須比對 owner+serial，不能清掉新 instance。未知 actor／remote／其他 profile 維持原呈現，仍照常處理權威回報。

本政策相符的是已接受的 base body pixels/frame keys、條件式 index順序與算式；不宣稱 private flags 中性、variant/FIFO/native開始時間/畫面耗時相等。不執行 `<206` 新音效或特效。C112900ms、pending3000ms、inventory/ground 權威數量與完成判斷均不改，renderer finish 不能完成請求。

`test-matrix.json` 有36項**尚未執行**的測試案例，包括16個 Speed×direction 正例、burst/unknown/owner/remote/asset/late callback/terminal/collision/gates 等負例與一項獨立 live 驗收。此矩陣可直接供下一輪隔離候選與獨立覆核採用；本輪沒有拿它冒充測試通過。

## 最小素材交付，不改 assets junction

採用 source `Resources/pickup-own-base15-gfx0-weapon0.json` 作新的 `EmbeddedResource`，logical name `P06.Resources.PickupOwnBase15Gfx0Weapon0.json`。只需24個新 body frame；另8個 terminal frame0 body 直接引用目前已載入且 pinned 的 `objects/0.json`，不複製29MB整份 profile，不改 players manifest，也不複製整包素材。

| metadata index | 每方向使用的 frame key | 數量 | 交付 |
|---|---|---:|---|
| 0 | `d:0`，d=0..7 | 8 | 重用既有 objects/0.json |
| 1 | `(88+d):0` | 8 | 新 resource |
| 2 | `(88+d):1` | 8 | 新 resource |
| 3 | `(88+d):2` | 8 | 新 resource |

`minimal-frame-inventory.json` 逐32frame列 key、bounds、anchor、source blocks、原entry offset/size/hash、identity tier及精確 canonical frame bytes/hash。新24frame全部是 **current-idx-and-bounded-slice-only**；重用8frame是 **historical-selected-payload-matched**。它們都在已接受的 bounded body pixels 範圍，但不得因 pixel accepted 就把新24frame升格為歷史 archive/BIN 配對。

以 `asset_plan.py` 的明確 JSON 投影／序列化規則計算，預期 runtime resource 為 **239,766 bytes**，SHA-256 `43b88cec329929484610aceae84566d1a922eb4b8ceba93e17dc44b65922ca90`。此輪只在記憶體計算清單與預期大小/hash，**沒有寫出或打包 pixel payload，也沒有再次解碼**。完整 csproj item、來源8個block JSON pins、runtime schema/hash/key檢查與未來 source/resource/DLL pins 見 `asset-delivery-plan.json`。

只沿用現有 shadow exact-key lookup；缺該key的已資格 shadow 時依既有程式保持無影，不借用舊 idle shadow，不再追加shadow素材研究。overlay 時需移除舊 body/idle shadow，再按新key查詢；本次資格明確是 body pixels。

素材引用新 accepted independent review：seal `b0f5dd9e1e0469c9c6ef3e0067d8b41e1fa9f2961ea86be25b46c2247d66805e`，result `80613924513ab0815b70ec40dcc09634fd11e1d35d54e3c5a01c8bc717e44a08`。它的416frames／1,402,968pixels零差異與160frames視看是**覆核包既有結果**，本輪沒有重跑。8個 archive shard 差異及104historical/104current-only限制全部保留。

## 重跑及封存

```powershell
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 'D:\lineage開發\TW1608262200研究\work\engine-pickup-renderer-eligibility-source-r1\verify.py'
```

verify 只讀，核對 pins／native chunks／表格與來源引用／metadata entry／最小frame清單與預期resource序列化／seal。這些是靜態完整性檢查，不是產品 runtime tests。collector/report/asset-plan/seal scripts 會寫本工作產物，封存後不得原地重跑。`native/00a4371d.*` 是一次起點落在指令內的 navigation slice，已明確 `adopted=false`，不可用作反組譯語意證據；採用的是 aligned `00a43750`，失敗紀錄保留。

後續請單獨派工實作上述 nominal policy，依當時已接受的共享版本重新對基準；本輪不合併、不改資產junction、不建立候選、不宣稱整套拾取或native精確等價已完成。
