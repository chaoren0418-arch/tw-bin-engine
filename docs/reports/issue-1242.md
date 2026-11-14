# Issue #1242：原生選角／建角畫面背景與按鈕畫成人物卡片（#1211 SM07、SM08 ❌）

結論：**根因是 surf 圖檔的來源錯了，不是版面或 DPI 的問題。** `0-客戶端` 的 6400–6529 這 130 個 id 全部有 `<id>.img`，而且全都是 60×80 的舊版職業頭像；這些 id 都沒有在地化的 `<id>c.img`。`SurfCatalog.Get` 會先在本機找到這些頭像，所以參考客戶端的 fallback 根本不會被問到。結果選角畫面的背景、按鈕、角色欄、資訊框，以及建角畫面的上一頁／下一頁、性別、能力加減、量表，全都畫成紅色或藍色的人物卡片。

修正方式：新增 `CharScreenSurfs`，只給選角與建角兩個面板使用。查圖順序：
1. 先查「美術客戶端」，預設是雷雨天堂（ly）。ly 的 SelectUI／CreateUI 圖號與圖檔是對得上的。
2. 查不到時才查本機，再查 kor25。這兩處 6400–6529 範圍內的頭像 `<id>.img` 一律不用。
3. 都沒有就不畫。

登入畫面與其他畫面的 surf 載入（`SurfCatalog`、`UiResources`）**都沒有改**。

## 根因（已驗證）

### 1. 本機與 kor25 的 64xx／65xx `.img` 是頭像，ly 才是介面圖

用 PakViewer CLI 列出 idx，再用 `docs/reference/rea-scripts/pak/surf_compare.py` 讀 idx／pak，依 HashFileManager 順序找圖（`<id>c.img` 先在 `Sprite{h}`、再在 `Sprite`，然後才找 `<id>.img`）。結果寫在 `scratch/i1242/surf-compare.tsv`，共 239 個 id。

- **本機 `0-客戶端`**：6400–6529 每個 id 都只有 `<id>.img`，大小 9,608 B，也就是 60×80（8 + 60×80×2）。都是人物頭像，沒有任何 `<id>c.img`。
- **kor25**：
  - 141 個 id 有 `<id>c.img`，這些才是真正的介面圖。
  - 下列 30 個 id 只有 `<id>.img`，而且一樣是 60×80（或 60×81）的頭像：6400–6403、6411–6414、6421–6425、6429、6487、6488、6495–6507、6509。
- **ly（`D:\天堂參考用客戶單\雷雨天堂`）**：
  - 上面那 141 個 `<id>c.img`，SHA-256 與 kor25 **逐一相同（141／141）**。
  - 那 30 個 id 在 ly 是真正的介面圖，見下表。
  - SelectUI／CreateUI 引用的 112 個 surf 裡，ly 只缺 6523、6525。這兩個是 `_Multi` 版，引擎本來就不建。
- **t89**：圖號體系不同（例如 6411 是 28×16），不採用。

### 2. 原 BIN 的讀圖方式

位址換算依 #456／#458 的區段規則：`.text` 的 FO = VA − 0x400C00，`.rdata` 的 FO = VA − 0x401400，RVA = VA − 0x400000。IDB 用的是 `P01-分析基準/ida/03-ascii-isolated/TW1608262200.idb` 的副本，SHA-256 `3A0BF206…BC17E82F`，與 #913／#960 相同。只做唯讀查詢。

| 函式／資料 | VA | RVA | File Offset | 內容 |
|---|---|---|---|---|
| `sub_AED660` | 0xAED660 | 0x6ED660 | 0x6ECA60 | Button 屬性 binder：`BaseSurf`、`HighlightSurf`、`PushSurf` 各建一個 `XmlImage`（`sub_AE6A20`）並呼叫 `sub_B0F460(value)` |
| `sub_B0F460` | 0xB0F460 | 0x70F460 | 0x70E860 | 用 `sub_A0CE60` 以 `;` 切開 `"id;N"`：`this[72] = id`、`this[70] = N`；N 是 2、5、11 時，第三段放進 `this[71]` |
| `XmlImage::vftable` | 0x11A0D64 | 0xDA0D64 | 0xD9F964 | 第 8 格（+32）是 `sub_B04440` |
| `sub_B04440` | 0xB04440 | 0x704440 | 0x703840 | 只用 `this[72]`（id）經 `sub_E538D0` 取圖並決定大小，`this[70]` 不改變取哪個檔 |
| `sub_AFA5B0`、`sub_B06AA0` | 0xAFA5B0、0xB06AA0 | 0x6FA5B0、0x706AA0 | 0x6F99B0、0x705EA0 | 整個 vftable 90 格裡，只有這兩處讀 `this[70]`，用途是 N ≥ 6 時的除錯框與數字 |
| `sub_E55DD0` | 0xE55DD0 | 0xA55DD0 | 0xA551D0 | `sprintf("%d.img", id)`，字串在 VA 0x11DFA2C、FO 0xDDE62C，經群組 4（Surf）讀檔。HashFileManager 先找 `c` 版，見 `SurfCatalog.cs` 的說明 |
| `sub_EF13F0`、`sub_EEDD90`、`sub_EF0060` | 0xEF13F0、0xEEDD90、0xEF0060 | 0xAF13F0、0xAEDD90、0xAF0060 | 0xAF07F0、0xAED190、0xAEF460 | 執行期才決定的 surf，維持 R9-O／R10-N／R11-D 的公式（職業卡 6430 + 8×class + 4×sex、上下頁 6411–6414、Create 用 p−2／p−1） |

結論：
- BIN 的取圖規則是「id → `<id>c.img`，沒有才用 `<id>.img`」。圖號本身的對應**沒有錯**，引擎的公式也沒有錯。
- 錯在**圖檔集**：0-客戶端（以及 kor25 的那 30 個 id）的 64xx／65xx `.img` 是另一套舊頭像，不是 SelectUI／CreateUI 要的圖。
- `;N` 不會改變讀哪個檔（已驗證）。N 的實際畫法是推測，見文末「推測」。

### 3. 引擎的錯在哪

- `engine/source/NativeUi/SelectCharPanel.cs:369` 原本是 `Load(layout,surfs.Get)`，`CreateCharPanel.cs:279` 同樣如此，都直接用 `SurfCatalog.Get`。
- `SurfCatalog.Get`（`SurfCatalog.cs:108–122`）先查本機，找到 `<id>.img` 就用。只有本機找不到時，才會問 `RefClientSurfFallback.Serve`（kor25）。
- 6400–6529 在本機一定找得到，所以一定拿到頭像。

## surf 對照（摘錄；完整 239 列見 `scratch/i1242/surf-compare.tsv`，PNG 預覽在 `scratch/i1242/png/`，不進 git）

格式：檔案 尺寸 SHA-256 前 12 碼。

| surf | 元素 | 0-客戶端（修改前） | kor25 | ly（修改後） |
|---|---|---|---|---|
| 6421 | Character_Select_Window BaseSurf（背景） | `6421.img` 60×80 `4e2bc47c0683` | `6421.img` 60×80 `1a5ab3f85875` | `6421.img` **800×600** `2a725adef4ab` |
| 6429 | Slot_Back `;3` | `6429.img` 60×80 | `6429.img` 60×80 | `6429.img` 337×110 `68e3b9091e55`（白邊黑底遮罩） |
| 6430–6485 | Select_Button 職業卡（執行期公式） | 60×80 頭像（例：6430 `752453a3ceb1`） | `<id>c.img` 337×110 | 同 kor25（例：6430c `5abb7e75fb5d`） |
| 6426–6428 | Create_Button | 60×80 | `6426c.img` 337×110 `2b2272c44dd3` | 同 kor25 |
| 6425、6422–6424 | Slot_Lock_Back、Lock_Button | 60×80 | 60×80 | 337×110（6425 `9809ad0422db`、6422 `6078a93440cd`） |
| 6411–6414 | Up／Down_Button（執行期） | 60×80 | 60×80 | 58×50（6411 `c09b9db2d4fb`） |
| 6404–6406 | Enter_Button | 60×80 | `6404c.img` 115×31 `4ba085b8f6b3` | 同 kor25 |
| 6407–6409 | Delete_Button | 60×80 | `c` 58×24 | 同 kor25 |
| 6415–6417 | Back_Button／建角 Prev_Button | 60×80 | `6415c.img` 88×35 `eec25583651b` | 同 kor25 |
| 6418–6420 | 建角 Next_Button | 60×80 | `c` 96×35 | 同 kor25 |
| 6410 | CharacterInfo_Back | 60×80 | `6410c.img` 345×220 `0f3501947a1a` | 同 kor25 |
| 6486 | WaitToDel_Image | 60×80 | `c` 70×14 | 同 kor25 |
| 6488、6487 | Message_Window BaseSurf `;3`、Back_Image | 60×80 | 60×80 | 459×295（6488 `5acadc37624e` 遮罩、6487 `26b4665bfc82` 框圖） |
| 6489–6494 | Ok／Cancel（訊息窗）、建角 OK | 60×80 | `6489c.img` 150×34 `4a96d3618fc3` | 同 kor25 |
| 6495–6501 | 建角 Male／Female_Button、Male／Female_Back | 60×80 | 60×80／60×81 | 68×68（6495 `ce2c5bee162e`、6501 `aa237e36f7c0`） |
| 6502–6507 | 建角 Exit、能力 Down／Up | 60×80 | 60×80／60×81 | 26×25、18×16（6502 `fd1e96360019`、6504 `80463b21331f`） |
| 6509 | 建角 Gauge | 60×80 | 60×80 | 181×17 `c158af82bdcb` |
| 6510–6529 | 建角 Attack／Defense／Mix、Customizing、Reset、Summary、Input_Back | 60×80 | `c` 105×31 至 406×350 | 同 kor25（6523、6525 兩邊都沒有，是 `_Multi`，不建） |
| 7045–7091、7099–7171、7224–7238、7265、7266、7707、7708、8365–8372 | Character_List 版、建角背景與職業按鈕等 | 本機沒有 | 有 | 同 kor25 |

對照截圖（只看，不複製）：
- `m7-SM07-1.png` 左列的 5 張紅卡是 Slot 列的 6430 系列，加上 6426 頭像；中間 (375,218)／(375,271) 的藍卡是 Up／Down 的 6411、6413。
- `m7-SM08-class-0.png` 左下與右下的卡片是 Prev（6415）與 Next（6418）。

## 修改前後

- **新檔 `engine/source/NativeUi/CharScreenSurfs.cs`**：
  - `Get(id)` 依序查三處：
    1. 美術客戶端：`RefClientSurfFallback.TryRead`，先 `<id>c.img` 再 `<id>.img`，記下 idx 與 payload 的 SHA-256。
    2. `SurfCatalog.Get`。
    3. 參考客戶端 kor25。
  - 第 2、3 步若拿到 6400–6529 範圍的 `<id>.img`，視為舊頭像，不用（`Legacy`）。
  - 每個 id 只解碼一次，記錄來源（`Origins`）；不畫的 id 會寫一行 log。
  - 美術客戶端的設定順序：`TW_CHAR_ART_CLIENT` → `native-ui.json` 的 `"charArtClient"` → 預設 ly。設 `false` 或 `""` 就關閉。路徑若與主客戶端相同或不存在，就不用。
  - 共用實例以 `SurfCatalog` 為鍵（`ConditionalWeakTable`），兩個面板共用。
- **`SelectCharPanel.cs`、`CreateCharPanel.cs`**：
  - `Load(UiLayout host, SurfCatalog surfs)` 改成傳 `CharScreenSurfs.For(resources,surfs).Get`。執行期的 `Dynamic()`（職業卡、上下頁、Create、能力畫面背景）也走這個 lookup。
  - 檔頭註解同步更新。
  - 版面、繪製、輸入邏輯都沒有改。
- **沒有改的部分**：
  - `SurfCatalog`、`UiResources`、`RefClientSurfFallback`、`LoginPanel` 等共用載入。**不需要回歸 SM04**。
  - `--classic-ui` 與 `nativeUi:false` 不會進原生面板，WinForms 選角路徑完全沒有改。
  - `rebuild-lock.json`、`Program.cs`、`DemoChecks.csproj`、註冊表都沒有改。
- **行為差異：沒有美術客戶端時**
  - 30 個只有頭像的 id 不畫，所以選角背景、Slot_Back、上下頁鈕、性別鈕等會缺圖。其餘 82 個 id 由 kor25 的 `c` 圖補上。
  - Message_Window 兩張底圖都缺，大小無法決定，選角畫面會變成 `Unavailable`，改走既有的 WinForms 選角。這延續「缺圖不以替代圖冒充」的規則。
  - 使用者電腦上 ly 在預設路徑，PakViewer 已讀過。

## 怎麼驗證

- **已驗證**：
  - `dotnet build engine/source/OfflineClient.csproj -c Release`，0 個錯誤。設了 `DOTNET_CLI_HOME`（scratch）、`DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`。
  - PakViewer 清單、surf 比對、IDA 反編譯，都只讀客戶端與 IDB 副本。
- **新離線檢查**：`engine/source/NativeUi/CharScreenSurfsTests.cs`，`[WorldModuleTest("char-screen-surfs")]`。
  - 為什麼不用 `[CheckGroup]`：`SelectCharPanel.Load(UiLayout,…)` 與 `CharUiBuilder` 是 internal，DemoChecks 看不到。這與 #1206 的 `select-char-geometry` 相同，用的是產品內同樣可自動發現的 attribute，CI 的「World module extension tests」步驟會執行。
  - 合成資料：本機 60×80 頭像、kor25 的 `c` 圖加 30 個頭像、ly 的完整圖。斷言內容：
    1. 查圖順序：有美術客戶端時，每個 id 都從 art 來，尺寸正確。沒有時，`c` 圖由 kor25 補上，30 個頭像 id 回 null。任何情況都不會回傳 60×80 圖。範圍外的 id（7156）照舊。
    2. SelectUI 每個元素拿到預期的 surf：背景 6421 800×600、Slot_Back 6429、Select／WaitDelete 6430、Create 6426、Lock 6425／6422、Up 6411、Down 6413、Enter 6404、Back 6415、Delete 6407、Info 6410、WaitToDel 6486、訊息窗 6488／6489／6492／6487。職業圖 6430–6485 只出現在 Select_Button／WaitDelete_Button。執行期的上下頁與 Create 用的 surf 都是正確尺寸。實際繪製時，背景、Enter、已選列職業卡、空欄 Create 的像素顏色，都等於各自 surf 的顏色。
    3. 沒有美術客戶端時：背景不畫、Enter 用 kor25 的 `c` 圖、選角畫面 Unavailable。
    4. CreateUI 兩頁：Male／Female／Male_Back／Prev／Next、Down／Up／Exit／Gauge／Attack／Summary／Customizing（含 Push）／OK／Prev／Input_Back 都拿到預期的 surf，沒有缺圖、沒有 60×80 圖。
    5. 設定的優先順序。
  - 這些檢查本機**沒有執行**（規定不得執行編譯產物），交給 PR 的 CI。

## 未驗證／推測

- **待驗證**（交給 `f1211-retest`）：
  - SM07、SM08 的原生畫面：背景、按鈕、職業卡要各在其位；建角三頁也要看。
  - SM09（DPI 96）。
  - SM55 classic 選角回歸。
  - SM04 登入畫面：本次沒有改共用載入，**不需要**回歸。
- **推測**：
  - `;3`（6429、6425、6488）在 ly 是白邊黑底的遮罩圖，而且都被同尺寸的圖完全蓋住（Slot 卡、Lock 鈕、6487 框圖），所以照 XML 畫不會露出來。TW16 是否把 type 3 當遮罩或命中區使用，沒有反編譯到繪製端。
  - ly 是否就是「TW16 原本搭配的圖檔集」無法證明。依據只有兩點：141 個 `c` 圖與 kor25 逐 byte 相同，以及 30 個 id 的尺寸與版面吻合（背景 800×600、Slot 337×124 欄內放 337×110 卡、訊息窗 459×295 容得下 394×190 文字區與 Y 237 的按鈕）。
- 沒有啟動遊戲、伺服器或 DB，也沒有執行任何編譯產物。

## 查過的 pak 清單

- **`0-客戶端`**：24 個 idx 都用 PakViewer CLI 列過，清單在 `scratch/pak-index/*.list.txt`、`SUMMARY.txt`。
  - `Sprite.idx`、`Sprite00–15.idx`
  - `Text.idx`（L1）、`Tile.idx`
  - `LiTo\LTicon`、`LTsprite`、`LTsurf`、`LTtext`、`LTtile.idx`
  - `launcher\TW13081901.pak` 沒有 idx，PakViewer 打不開。
  - `LiTo\LTsurf` 有 6448–6474 的 `.img`，但不是 SelectUI 用的圖，引擎也不讀 LTsurf。
- **參考客戶端**（`scratch/pak-index/alt/`）：kor25、ly、t89 各自的 `Tile.idx`、`Text.idx`、`Sprite.idx`、`Sprite00–15.idx`。
- **XML**（PakViewer `pak extract` 加 `xml decrypt`，解到 `scratch/i1242/xml/`）：

| 來源 | 檔案 | SHA-256（解密後） |
|---|---|---|
| kor25 | `SelectUI.xml` | `b4eb1fa7…62bc46b9` |
| kor25 | `CreateUI.xml` | `5277a2af…e1a9d65f` |
| ly | `SelectUI.xml` | `19da0836…ea95cf9f`（與 kor25 只差兩處 `</Control>` 縮排，以及 Name_Edit 的 `LimitText` 16 對 12） |
| 0-客戶端、kor25 | `SurfList.xml` | 兩邊相同 `a596c252…ad5dc051`，**沒有** 64xx 的列 |

## 產出

- 程式：`engine/source/NativeUi/CharScreenSurfs.cs`（新）、`CharScreenSurfsTests.cs`（新）、`SelectCharPanel.cs`、`CreateCharPanel.cs`、`SelectScreenGeometryTests.cs`（只把合成的 `Xml` 改成 internal，給新測試重用）。
- 工具：
  - `docs/reference/rea-scripts/pak/surf_compare.py`：四個客戶端的 surf 對照，含 PNG 預覽。
  - `docs/reference/rea-scripts/t1242/`：`run_ida.ps1`、`ida_surfattr.py`、`ida_vtbl.py`，都是唯讀 IDA 查詢。
- 報告：本檔。分支 `task/1242`。

## 給下一棒的提示

- **retest（SM07／SM08／SM09）**：
  - 看 `ui-resources` log 的 `char-art …` 行。每個 surf 應該來自 `雷雨天堂\Sprite*.pak`，並附 SHA-256。若出現 `char-art surf N 不畫`，表示美術客戶端沒讀到。
  - 預期畫面：背景是石門走廊。左列是 337×110 的職業卡，選中那一列是亮色（c+2）。(375,218)／(375,271) 是三角形的上下頁鈕。右下有資訊框 6410。
- **想換美術客戶端**：設 `TW_CHAR_ART_CLIENT` 或 `native-ui.json` 的 `"charArtClient"`。設 `false` 可以重現「沒有美術客戶端」的行為（選角改走 WinForms）。
- **若之後其他原生畫面也引用 6400–6529 的 surf**：同樣會被本機頭像蓋掉，可以改用 `CharScreenSurfs.For(...)`。不要直接改 `SurfCatalog` 的順序，那樣會影響登入畫面。
