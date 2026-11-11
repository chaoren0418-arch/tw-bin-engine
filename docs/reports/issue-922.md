# Issue #922 T405：R10-R list.spr 附加層接線（altdir／stride／cloth）

依據：[`followups-14.md`](../reference/followups-14.md)（FU14）§1.3、§2.4、§3.6；R8-E（#824）的資料層 `ListSprFields`／`ListSprLayers`。

## 做了什麼

### 1. altdir（112，FU14§1.3）
- `ObjectRendering.cs` 他人迴圈：先判斷**靜態外型**，條件照 `sub_A3EF40`／3.81 `sub_4E5C00`：
  - 動作 ≤ 0；
  - 這個 gfx 自己那一列沒有該動作（`ListSprLayers.StaticShape`／`HasAction`，不跟別名）。
- 靜態外型而且朝向 6 → 用 `AltDirGfx` 換載入的 gfx。每幀重算、不改 `o.Gfx`，轉離 6 就換回。
- 有動作表的外型完全不看 112。靜態外型分支沒有 cloth 層（FU14§3.1）。
- 讀不到 list.spr 時不算靜態，行為照舊。

### 2. stride（111，FU14§2.2、§2.4）
- `ListSprLayers.StrideActions = {0,4,11,20,24,40}`；`StrideAdvance`（這一步用前進前的值，然後 ++，> n 歸 0）；`StridePose`（目前動作＝8 時不換）。
- **他人**：
  - 新檔 `ListSprStride.cs`：`[WorldReceive(10, Observe=true)]` 自動發現的 S_MOVE 觀察器，前進 `WorldObject.StrideStep`／`StrideCounter`。
  - 條件同主分派：信封不合、死亡、自己都不動；沒有 stride 的 gfx 不動計數。
  - 主分派的 `o with{…}` 會保留這兩個欄位；新的 S87 重建物件時歸 0。
  - 沒改 `WorldSession.cs`。
- **他人繪製**：`moving` 時改用 `catalog.Get(drawGfx, StrideActions[StrideStep])`；沒有圖就照原姿勢（AN§3.4）；換成 1120 的不套用。
- **自己**：
  - `ClientLogin.cs:53` `MovePlanned` 每送一步呼叫 `login.World.AdvanceOwnStride(gfx)`；
  - 外型統一用 `Renderer.OwnStrideAppearance`，`ComposeObjects`（原 :8）、`MovePlanned`、`SyncWorld`（:18）三處用同一個。
- `WorldObjects.cs` 只加了 `StrideCounter`、`StrideStep` 兩個欄位。
- 快照 `extensions.stride`。
- **計數初值 0 是推測**（FU14§2.2：物件記憶體清零，沒追到明確的初始化）。

### 3. cloth（105，FU14§3.1、§3.5、§3.6）
- `ClothVerified = true`。依據是 FU14§3.5 的五點：3.81 會解析、每幀載入、放進場景時不設條件。
- `Renderer.ClothDraws`：每一層取 `<cloth>-<本體 sid>.spr` 中與本體相同的 `sid:frame`。
  - 任一層缺子檔，或沒有那一格 → 這一格整組不畫；
  - 不看裝備、動作、朝向；沒有「自己＋職業外型」的例外（照 3.81）。
- 接在以下位置：他人（含門、商店）、自己（`Actor(own)`）、自己的戰鬥影格、拾取影格。
- 序號在本體 `1000000+` 和影子 `1100000+` 之間（`1004096 + 層×4096 + order`），維持「本體 → 各層（list.spr 順序）→ 影子」。
- 圖層名用 `"actor"`，所以隱身、淡入淡出、屍體半透明、點選都和本體一起處理。
- 混色取 cloth 自己的 `104.attr`，對應方式同 `EffectSprite.Parse`（8 → screen）。
- **全空子檔**（例如本機 `6262-0.spr`，65 B，4 格，0 個 block）：
  - `EffectSpriteCatalog.Decode` 要求 block ≥ 1，會拒絕這種檔；
  - 新增 `ListSprLayers.EmptySprite` 把它讀成空格，不當缺檔。否則同一 gfx 的其他層會被整組跳過。
- 唯讀來源 `ListSprRuntime`（`IListSprSource`）：
  - 從原客戶端讀 `list.spz`（解密方式同 `EffectSpriteCatalog.LoadMetadata`），以及 `Sprite`、`Sprite00`–`15` 的 plain28 索引；
  - 不複製任何資源；讀不到時全部回 null（不畫層、不換外型），不丟例外；執行緒安全。

### 4. 動作 71（釣魚）
- 不再一律改播 `"walk"`。改用 `ListSprAction.Parse(gfx, row, 71)` 播 list.spr 的釣魚動作。
- 本體子檔走標頭的 `=K` 別名：本機 gfx 0 是 `#0 312=3225`，所以本體檔是 `3225-<304+朝向>.spr`。
- 釣魚線（gfx 0 的 cloth 6262）用同一個子檔號，自然就畫出來。
- 動作或子檔讀不到時，才照 AN§3.4 退回姿勢動作（舊行為）。
- 釣魚動作會循環到下一個動作或移動。**循環是推測**，原版播完後怎麼處理沒有查。

## 怎麼驗證
- **編譯**：`dotnet build engine/source/OfflineClient.csproj -c Release` 結果 0 錯誤；改到的檔案沒有新警告（4 個警告都在 `HtmlActionTests.cs`、`MagicUi.cs`，本來就有）。
  - 本機 SDK 對 WebView2 winmd 會報 NETSDK1130（同 #902／#903／#916）。處理方式：從主專案 `engine/vendor` 唯讀 restore 到 `scratch/t922/packages`，再用只放在 scratch 的 `scratch/t922/nowinmd.targets` 把 winmd 換成 netcoreapp3.0 DLL。都沒有入 git。
- **`check-no-game-assets.py`**：pass（tracked 4493 個檔，0 錯誤）。
- **離線測試**（新檔 `ListSprWiringTests.cs`，`--module-tests` 名稱 `listspr-wiring`）：只用合成資料。依本機規定不在本機執行，交給 CI。涵蓋：
  - altdir：1395 朝向 6 → 1410、其他朝向 → 1395、6→2→6 每次重算、5940、有動作的外型不換、名稱有空白的動作也認得；
  - stride：1062 連走三步 0→4→0（純函式、他人 S_MOVE、自己三種情況都測）、stride(5) 走完整張表、動作 8、沒有 stride、轉向、不合的封包、自己的 S_MOVE、S87 歸 0、死亡、快照、Reset；
  - cloth：子檔 304 有圖、子檔 0 是空格、缺一層整組不畫、影格超出範圍、序號區間與順序、混色、全空子檔；
  - 釣魚：動作 71 解析、子檔＝304＋朝向、別名、讀不到時回 null；
  - 讀不到原客戶端時不丟例外。
- `ListSprFieldTests.cs`（R8-E 的既有測試）有兩行寫死「`ClothVerified` 是 false」，已改成符合本任務的值（見下面「範圍說明」）。
- **CI**：見 PR。

### 資源查證（PakViewer CLI，唯讀，輸出在 `scratch/t922/pak-index/`）
- 範圍：`pak_index.ps1 -WithAlt` 列出的本機 24 個 idx（`Sprite`、`Sprite00`–`15`、`Text`、`Tile`、`LiTo\LTicon`／`LTsprite`／`LTsurf`／`LTtext`／`LTtile`；`launcher\TW13081901.pak` 沒有 idx），加上參考客戶端 kor25／ly／t89 的 `Sprite*`、`Text`、`Tile`。
- **`0-304.spr`～`0-311.spr` 四個客戶端都沒有**，gfx 0 只有 `0-0`～`0-207`。原因是 gfx 0 的標頭 `#0 312=3225`：本體實際用 `3225-N.spr`。
  - `3225-304`～`311` 都在本機：`Sprite03`–`08`、`Sprite00`、`Sprite01`；
  - `6262-0.spr`（`Sprite00`，65 B，全空）、`6262-304.spr`（`Sprite07`）、`6262-311.spr`（`Sprite05`）也都在。
- 本機 `list.spr`（PakViewer 從 `Text.idx` 解出，只放在 scratch）：
  - gfx 0 有 `71.fishing(1 12,304.11:4 304.0:4 …)`；
  - `#6262 320 prince fishing line` 有 `104.attr(8)`；
  - 1395／5940／1410／1062 的列與 FU14 相同（1062 另有 `108.flying type(1)`，不是 2，所以 +199 偏移照樣用不到）。
- 引擎 assets（`releases/r29/assets`，唯讀）：1395、1410、5940、1062、1461 **都沒有** `objects`／`world-appearances`／`encountered-monsters` 的 profile。所以 altdir／stride 在現有 assets 上換到的 gfx 或姿勢會照 R2-I／AN§3.4 退回，畫面不會有變化。資料和接線已經完成，要等 assets 補 profile 才看得到（見待驗證）。

## 範圍說明（超出「可修改的檔案」字面的地方）
- `ClientLogin.cs:18`（`SyncWorld`）：和 :53 是同一個外型對應，也改用 `OwnStrideAppearance`。
  - 原因：`World.SetOnlineAppearance` 換外型會呼叫 `Stop()`。如果 :18 還用 `(gfx, Weapon)`，自己變身成有 stride 的外型走路時，每次 `Changed` 都會把走路停掉。
- `ListSprFieldTests.cs`：只改了寫死「`ClothVerified` 是 false」的兩行，以及一行說明。不改的話，打開 cloth 之後既有測試必定失敗。
- 執行期的讀取程式（`ListSprRuntime`、`ListSprAction`、`IListSprSource`）放在 `ObjectRendering.cs`。沒有改 `MagicRendering.cs`（`EffectSpriteCatalog` 的 metadata 與索引都是 private），所以 list.spz 解密有一份複本，前例是 `Afflictions.cs` 的 `SpriteTypeTable`。
- 沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。沒有新面板。

## 未驗證什麼（待驗證，交給 Codex／真機 M31 smoke）
- 真機畫面：他人釣魚時有沒有釣魚線、NPC 衣物層（例如 201–210 `cw haira／sendal…`）的疊法與位置。
- **自己的釣魚**：自己的 S158 71 在 `WorldSession` 不會記錄（`UpdateObject` 找不到自己，只會累加 `UnknownObjectUpdates`），所以自己釣魚目前看不到釣魚線。要補的話得新增「自己的動作狀態」，不在本張範圍。
- 隱身時「層旗標有 8／0x80 就不半透明」（FU14§3.6）：自己隱身用的 `Invisibility.cs` 會對所有 `"actor"` 圖塊加 `|1`，沒有區分層旗標。不在可改檔案內，沒有做。
- 加上 cloth 後，`ActorBounds`（名稱位置、點選範圍）會包含附加層。原版 `sub_A3ED50` 算不算附加層沒有查（推測）。
- 釣魚動作的循環方式、`UpdatedAt` 被其他更新刷新時動畫會重頭播（推測）。
- 5940 的「永久改成 1120」沒有做（FU14§1.3，記成取捨）。
- 第一次畫面會從原客戶端讀 list.spz 和 17 個 Sprite 索引（在 UI 執行緒上，只讀一次）。實機的停頓時間沒有量。

## 產出路徑
- 程式：
  - `engine/source/ObjectRendering.cs`
  - `engine/source/ClientLogin.cs`（:18、:53）
  - `engine/source/WorldObjects.cs`
  - 新檔 `engine/source/ListSprStride.cs`
- 測試：新檔 `engine/source/ListSprWiringTests.cs`；`engine/source/ListSprFieldTests.cs`（兩行）
- 報告：`docs/reports/issue-922.md`
- 不進 git：`scratch/t922/`（PakViewer 索引、解出的 list.spr／6262-0.spr、查詢腳本、建置輸出、restore 套件）

## 給下一棒的提示
- 真機 M31 smoke 的核對項：
  - 他人釣魚有釣魚線（`3225-30x` 本體＋`6262-30x`）；
  - NPC 衣物層有畫，而且順序是本體→層→影子；
  - stride 外型走路左右腳交替（要等 assets 有 1062 之類的 profile）。
- 要讓 altdir／stride 真的看得到，`ObjectAssets` 需要 1410、1062（以及姿勢 4 的走路）的 profile，可以另開 resource 任務。
- 自己釣魚（S158 71 打在自己身上）需要一個自動發現的觀察器記錄自己的動作，然後在 `ComposeObjects` 的自己分支用 `FishingFrame`。
- 嚴格照 3.81 應該讀 `list.spr` 而不是 `list.spz`（FU14§3.4，另案）。`ListSprRuntime` 和 `EffectSpriteCatalog` 是同一個優先順序，要改時兩處一起改。
