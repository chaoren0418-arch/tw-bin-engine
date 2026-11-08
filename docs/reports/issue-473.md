# Issue #473 ui-buff-icons：原生 UI 狀態圖示（依舊封包對照）

依據：`docs/design/native-ui.md` 第 5.3 節 ui-buff-icons、第 5.2 節共通驗收；資料全部取自 `docs/reference/ui-layout.md` 第 4 節（V3，#458），以及 V4 (d)(e)（#459）的封存檔查找順序。

## 做了什麼

### 新檔
- `engine/source/NativeUi/BuffIconsPanel.cs`
  - `BuffEffectTable`：解析 `Tile.pak` 的 `effectlist2.xml`（走 C1 的 `UiXmlSource`，先找 `-c` 版再找原名；本機沒有 `-c` 版，所以讀原名）。
    - 讀入 `icon`、`offIcon`、`kind`（13 種名稱，順序同 `off_138D8FC`）、`priority`、`duration`。
    - 遇到未知 kind 或重複 id 時拒絕載入。id 與出現順序不一致時，只記錄 `Order fault`（同 `sub_81DF30`）。
    - 筆數之外的 effect 一律不建立圖示（同 `sub_F6D560`）。
  - `TbtImage`：`.tbt` 解碼。沿用專案裡既有的 `LoadL1Image` 移植規則（`InventoryTests.cs`）：
    - 標頭依序是 x、y、寬、高。
    - 每列：段數，接著每段是「位移 byte÷2、像素數、RGB555 像素」。
    - 沒畫到的像素與值為 0 的像素都是透明。
    - 截斷、奇數位移、超出範圍、結尾有多餘資料，全部拒絕。
  - `BuffIconCatalog`：`<n>.tbt` 查找，依 V4 (d)(e) 的 Icon 封存群規則（`HashFileManager`＋無 `-` 格式器），順序是：
    1. `<n>c.tbt`，在 `Sprite{h}`
    2. `<n>c.tbt`，在 `Sprite.pak`
    3. `<n>.tbt`，在 `Sprite{h}`
    4. `<n>.tbt`，在 `Sprite.pak`

    V3 提出的「雙份 `.tbt`」問題就依這個順序解決：`SpriteNN` 優先。找不到的圖示每個只記一行，**不畫**。
  - `BuffIconOrder`：原版的兩條清單（增益 +4、減益 +12），採插入語義：
    - 排序鍵（`sub_903EB0`）：`priority > 0` 用 priority；kind 0–2 用 kind；其他為 100。
    - 比較器（`sub_8FDE00`）：任一方的鍵 < 100 時比鍵；兩方都 ≥ 100 時比到達順序。
    - 新圖示插在第一個「比較器判定新圖示在前」的元素前面，否則放最後；已經在清單上的圖示不會移動。
  - `BuffIconsPanel`（`STATUS_EFFECT_ICONS`，Bar 層）：
    - 位置：第 n 格在 (798 − 35·(n mod 22 + 1), 5 + 35·⌊n/22⌋)。減益從第 ⌈增益數/22⌉ 列開始排。
    - 繪製：平常畫 `icon`。時間已過、伺服器還沒送移除、而且 `offIcon > 0` 時，改畫 `offIcon`。`icon == 0` 時畫 effect 編號。
    - 剩餘時間**不畫在圖示上**，只在滑鼠移上去時顯示 tooltip：框寬 160，位置 (x, y+32)，距右緣不足 5 px 時往左移。
      - 文字是 `"<字串930>: <秒> <字串931>"`；期限 −1 時，秒數位置顯示 `--`。
      - offIcon 狀態時顯示字串 2854。
      - 字串一律取自原版 `string-c.tbl`（`ServerMessageText.Table`）。讀不到這個表就不顯示文字，**不自己編**。
- `engine/source/NativeUi/State/BuffState.cs`
  - `BuffEffectRules`：把 V3「舊封包 → effect id」表套到引擎既有模型上（`MagicBuffs` 的 skill 鍵＋`Source` 標籤，以及 `OwnActor.Speed／Brave／Flags`）。**沒有新增任何封包解析。**
    - 只要 V3 表需要的值引擎沒有保留，就不對照，記為「未對照」並附上原因（見下表）。這些效果不畫，也不畫替代菱形。
  - `BuffState.From(WorldSession, 職業, tooltip 字串)`。`UiBuffGameState` 裝飾器放在 bridge 鏈的最內層（聊天狀態的內側），由 `BuffState.Of` 依序穿過 HpMp → Macro → LeftStatus → Chat 找到它。
  - `partial WorldSession`：唯讀公開加速計時（`NormalHaste.cs` 的 `speedDisplayUntil`）。
  - `partial ClientForm.WithBuffState`：組出上述快照。職業取自 `login.Characters`。
- `engine/_integration/dev/native-ui-tests/BuffIconsChecks.cs`：離線檢查（合成資料），以及選用的本機真實客戶端檢查。

### 改到的既有檔案（理由）
| 檔案 | 改動 | 理由 |
|---|---|---|
| `NativeUi/UiPanelRegistry.cs` | 加一行註冊 | 第 5.2 節允許 |
| `NativeUi/UiClientBridge.cs` | `new UiChatGameState(WithBuffState(basic),…)`：一個呼叫，加上註解 | 不改這行，模組就拿不到狀態。#464／#465／#466／#468 都用同樣方式接上 |
| `native-ui-tests/MacroChecks.cs` | `MacroRegistry` 排除 `STATUS_EFFECT_ICONS` | 它用真實註冊表加合成 Tile 來 Build，而合成 Tile 裡沒有 `effectlist2.xml`。#464、#468 也是用同樣方式排除自己的面板 |
| `demo-tests/DemoChecks.csproj`、`Program.cs` | 各加一行；預設清單加上 `NativeUiBuffIconsChecks` | 第 5.2 節允許 |
| `.github/workflows/build.yml` | 新增 `native-ui-buff-icons` 步驟（合成資料，不需要遊戲資源） | 讓 CI 也跑這組檢查。macro 等模組也有自己的步驟 |
| `rebuild-lock.json` | 用 `rebuild.py --update-lock` 重產 | 第 5.2 節允許 |

另外：同步了 origin/main（#468 聊天窗，有 3 個衝突）。`Program.cs` 兩邊的行都保留；`LeftStatusState.cs` 用 main 版本，main 已經加了同樣的 `Inner`；bridge 用 main 版本，再把 buff 包裝放進聊天狀態的內側。

### 對照狀態（V3 舊封包 → effect）
| 來源（引擎） | 已對照 | 未對照（不畫）與原因 |
|---|---|---|
| S255 加速（`Actor.Speed`） | mode 1 → 0、mode 2 → 1；秒數取引擎的加速計時，到期但仍在加速時畫 offIcon | — |
| S67 勇敢（`Actor.Brave`） | 3、5 → 2；6 → 110；8 → 19；1 → 妖精 19，其他 2；4 → 妖精 24、法師 42、幻術師 153、其他 37 | 職業未知時，mode 1／4 不對照。引擎丟掉了秒數，所以這幾個圖示沒有 tooltip |
| S171 隱身（`Flags & 2`） | 非黑妖 → 396，期限取表中的 `duration`（−1 → 顯示 `--`） | 黑妖：377 或 396 要看旗標 `byte_13D4403`，引擎沒有這個值；職業未知時也不對照 |
| S126 水中呼吸 | → 11 | — |
| S216 防護圖示 | type 3/5/6/7/10/其他 → 5/482/6/7/8/4 | — |
| S166／S188 | 引擎鍵 109／110（type 2）→ 9／10 | 引擎鍵 42／26：type 可能是 3（→ 34／35），也可能是其他值（→ 9／10），引擎沒有保留 type |
| 250/20 快照 | byte 0、4、5、7、8、9、18–22、24、30–32、52–59、10＋11（屬性武器）依 V3 快照表；byte 26 依職業（58／27） | byte 1、61：TW16 不讀。byte 3（47／480）、17（36／68）、23（56／66）、60（107／7）：要看其他 byte，引擎沒有保留 |
| 250/22 光環 | icon 113/114/116/119/147/148/154/155/162/165 → V3 表；221＋v 1/2/6 → 78/77/132 | 其他 icon／variant |
| 250/36、40、44、57 | → 63、41、52、3 | — |
| 250/154 | 原生武器 gfx 2165/747/748/2176/2951 → 12/379/394/13/392；代理 key 518–537 → effect 518–537 | 其他 key |
| 250/34 | — | type 1 → 38、type 3 → 481（地圖 2465 → 91），引擎沒有保留 type |
| 250/35 變身 | — | 要依目前外形查 `graphic`，本模組沒有實作外形查表 |
| 250/21（`icon21`）、料理、魔法娃娃、三段加速、地圖限時 | — | 250/21 的 TW16 格式和伺服器不同（V3 待驗證）；其他幾項不在 V3 表 |
| S202 麻痺 | — | 不在 `MagicBuffs`，而且 V3 標示格式差異待驗證；本任務沒有接 |

未對照的項目在執行期會寫入 `UiResourceLog`，每種只記一行：`buff-icons 未對照（不畫）<來源>：<原因>`。

## 怎麼驗證（已驗證）
- **同步 main：** 合併 origin/main（落後 10 個 commit，含 #468），解完衝突後用 `rebuild.py --update-lock` 重產 lock（289 個來源、298 個鎖定檔）。
- **建置：** `rebuild.py --build` PASS，product DLL SHA-256 `6f3642b6b28b8c80ec17477a1847796d86c0876ede15f3a507112ce75c0baf40`。
- **DemoChecks `native-ui-buff-icons`：** PASS（381 項）。全部用合成的 `effectlist2.xml`、Tile／17 個 Sprite idx/pak、`.tbt`，涵蓋：
  - 格子座標（第 0、1、21、22、45 格）與 tooltip 位置
  - effectlist2 解析：筆數之外不建立、未知 kind、重複 id、Order fault
  - 排序鍵與插入語義：priority 5／6 在前，無鍵依到達順序，已存在的圖示不移動，減益依 slow／curse／paralyse 排並從下一列開始，23 個增益時減益從第 2 列開始
  - `.tbt` 解碼與各種拒絕情況；查找順序 `<n>c.tbt` 在 `SpriteNN` 優先
  - V3 每一類對照，以及每一類「未對照」都有原因
  - 用真的 `WorldSession` 餵封包（S255、S126、S216、S166、S67、S171）：加速到期時畫 offIcon，S166 type 5 列為未對照
  - bridge 鏈能找到 buff 狀態，其他模組也照樣找得到自己的狀態
  - 用註冊表那一行就能 Build 出面板
  - 像素檢查：icon、offIcon、`SpriteNN` 的 c 版、缺 `.tbt` 時不畫、`icon 0` 時畫數字
  - 滑鼠移動、tooltip 文字（930／931／2854／`--`）、沒有字串表就不顯示文字、離開世界後不顯示
- **本機選用檢查**（`TW_CLIENT_ROOT=D:\lineage開發\0-客戶端`）：PASS（386 項）。
  - 真實 `effectlist2.xml`：540 筆，id 順序正確。
  - 63 個 V3 effect 的 `icon`／`offIcon` 與 V3 表一致。
  - 這些 effect 用到的每個 `.tbt` 都解得出 32×32，偏移 (0, 0)，絕大多數來自 `SpriteNN`。
  - **完整的 `UiPanelRegistry` 用 pin 驗證過的真實客戶端 Build 成功，沒有任何面板失效。**
  - 樣本 PNG：`scratch/issue-473/real/buff-icons-sample.png`。我目視檢查過放大圖 `buff-icons-crop3x.png`：是原版圖示，由右往左排；過期的 OLD_SHADOW_ARMOR 是灰色 offIcon；第二列是減益。
- **既有 DemoChecks**（合併 main 之後）：全部 PASS。

  | 群組 | 項數 |
  |---|---|
  | native-ui-resources | 539 |
  | native-ui-canvas | 429 |
  | native-ui-left-status | 370 |
  | native-ui-macro | 403 |
  | native-ui-hpmp | 397 |
  | native-ui-chat-window | 446 |
  | timed-buffs | 645 |
  | item-status | 409 |
  | afflictions | 580 |
  | resurrection | 553 |
  | combat-admission | 1750 |
- **`P11.ClientEngine.exe --module-tests`：** pass，18 組、910 項，0 失敗。
- **`check-no-game-assets.py`：** pass，0 錯誤（3842 個追蹤檔）。新增的檔案只有 `.cs`／`.md`，沒有 `.img`、`.tbt`、`.xml`、`.png`。

### 元件座標（真實客戶端樣本渲染，計時是合成的，不是伺服器資料）
| effect | 名稱 | 圖示 | 左上角 |
|---|---|---|---|
| 0 | HASTE_S | 5397 | (763, 5) |
| 110 | BLOODLUST_S | 3075 | (728, 5) |
| 4 | SHIELD_S | 485 | (693, 5) |
| 11 | WATER_BREATH_S | 552 | (658, 5) |
| 45 | MEDITATION_S | 795 | (623, 5) |
| 518 | FIRE_WEAPON_S_INDEPENDENT | 774 | (588, 5) |
| 5 | OLD_SHADOW_ARMOR（過期） | offIcon 5272 | (553, 5) |
| 1 | SLOW_S | 480 | (763, 40) |
| 52 | WIND_SHACKLE_S | 1623 | (728, 40) |
| 55 | ERASE_MAGIC_S | 1834 | (693, 40) |
| 63 | NO_CHAT_S | 1476 | (658, 40) |

## 推測
- **職業編號：** 用 3.81 伺服器的編號（2 妖精、3 法師、4 黑妖、6 幻術師、0 王族）對應 V3 的「依職業」條件。TW16 內部怎麼判斷職業，沒有查證。
- **顯示模式：** 舊封包除了 S126（已驗證是模式 0）以外，一律當作模式 0。這會影響 offIcon 與 tooltip 格式。
- **`.tbt`：** 標頭 (x, y) 當作繪製偏移；值 0 的像素當作透明。buff 圖示的偏移全部是 (0, 0)，所以目前不影響畫面。
- **effect 缺 `kind`：** 本機表中有 5 筆沒有 kind，這裡當作增益處理（原版這個 byte 的初始值未查）。目前的對照都不會用到這 5 筆。

## 待驗證／未做
- **真機畫面：** 位置、排序與原版截圖的對照。由 ui-native-smoke 處理。
- **tooltip 外觀：** 底色、邊框、內距、字色都沒有查證。目前是半透明黑底、白字，只有位置、寬度與文字依 V3。
- **tooltip 不會自動消失：** 滑鼠從圖示直接移到面板範圍以外時，面板收不到移動事件，tooltip 會留到下一次滑鼠移進面板。框架層沒有「滑鼠離開」事件。
- **未實作的原版機制：** 選項旗標 `0x800`（依剩餘時間排序，預設值待驗證）、`+21` 隱藏格旗標、effect 470 特例、`group` 互斥、模式 0 以外的 tooltip 格式。
- **和原版的已知差異**（都在既有模型，本任務沒有改）：
  - `MagicBuffs` 時間一到就把 buff 移除，所以除了加速以外，都不會出現「時間到、等伺服器移除」的 offIcon 狀態。
  - 快照秒數：引擎用 4n／16n，TW16 用 4n−2／16n−8；byte 3 的倍率也不同（引擎 4、TW16 16）。
  - S67 勇敢的秒數被丟掉，所以這些圖示沒有 tooltip。

## 產出路徑
- `engine/source/NativeUi/BuffIconsPanel.cs`
- `engine/source/NativeUi/State/BuffState.cs`
- `engine/_integration/dev/native-ui-tests/BuffIconsChecks.cs`
- 改動的既有檔案：`UiPanelRegistry.cs`、`UiClientBridge.cs`、`MacroChecks.cs`、`DemoChecks.csproj`、`demo-tests/Program.cs`、`.github/workflows/build.yml`、`rebuild-lock.json`
- 本報告。本機證據只放在 `scratch/issue-473/`，不入 git。

## 給下一棒的提示
- **要補對照：**
  - 優先處理 S166／S188 type 與 250/34 type：在 `MagicStatus.cs`／`TimedBuffs.cs` 保留 type，再到 `BuffEffectRules.Map` 加上對應的行。
  - 快照的條件 byte（3、17、23、60）也是同樣做法：讓引擎保留需要的 byte。
  - S67 勇敢：在 `NormalHaste.cs` 保留秒數，tooltip 就會出現。
- **變身（250/35）：** 用目前外形 gfx 查 `effectlist2.xml` 的 `graphic` 欄位（`sub_81DCA0`，取第一筆），可以在 `BuffEffectTable` 加 `graphic` 欄位來做。
- **ui-native-smoke 核對清單：** 第一格 (763, 5)，往左每格 35 px；priority 5（加速）在最右；減益在下一列；滑鼠移上去時 tooltip 在圖示下方 (x, y+32)、寬 160；記錄 tooltip 的底色與字型。
- **worktree 建置：** 需要 `engine/vendor/microsoft.web.webview2.1.0.4258.31.nupkg`。本次從主目錄複製一份，沒有進 git。
