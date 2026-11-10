# Issue #859 — T375：R9-O 選角／建角（code）

依據：`docs/reference/followups-9.md` §14.1–14.2、§16.2（R9-O 列）；`gap-matrix.md` §15.11（4:SelectUI、4:CreateUI）。
分支：`task/859`。前置 R7-N（#817）已合併（`4aa757db`）。

## 1. 做了什麼

### 1.1 建角（`NativeUi/CreateCharPanel.cs`）

- **Attack／Defense／Mix**（`Auto_StatSettingScene` → `sub_EE2F70` → `sub_EE6760`）：
  - 型態 1／2／3（原版是「按鈕在職業 Control 裡的序號＋1」；引擎用按鈕名稱對應，順序與 CreateUI.xml 相同）。
  - 再按同一個型態＝重設（`sub_EE5D60`：型態 0、職業基礎值）；否則把六項直接設成 `CharAttrType.tbl` 的（職業, 型態）那一列。
  - 選中的型態鈕三態都畫 PushSurf。
  - **進入能力場景時先把型態歸 0，再自動套用 Attack**（`sub_EE6DD0` 場景 2 → `sub_EE6760(1)`）。換職業、Reset 都歸 0。
  - 表的來源是本機 Text.pak 的 `CharAttrType.tbl`（新類別 `CharAttrTypeTable`）。每一列都要先通過 **3.81 規則**（`CharacterManagementPackets`：總和 75，基礎值 ≤ 值 ≤ 基礎值＋可分配點數）才會套用。TW16 的上下限表 `byte_138B248` 不用，3.81 的上下限不變。讀不到表或沒有合格的列時，按鈕停用（跟以前一樣）。
- **`Gauge_Group`**（`sub_EE5430`）：
  - 每條的寬度＝圖寬 × `StatGuage[職業][量表型態][能力−7]` ÷ 100，用 float 計算後截斷。能力不在 7–20，或表裡沒有這一列時，寬度是 0，不畫。
  - 對應關係（IDA 反組譯確認子項序號）：
    - 5 條：子項 0–4 依序是量表型態 0、1、4、3、5，對應 STR、DEX、INT、CON、WIS。
    - 6 條：型態 0–5，對應 STR、DEX、DEX、CON、INT、WIS。
    - kor25 XML 是 5 條。
  - `StatGuage.tbl`（新類別 `StatGaugeTable`）先找本機 Text.pak，沒有再找參考客戶端（`UiResources.Text.Root`）的 Text.pak。SHA-256 寫進 UI 紀錄，並和 kor25 比對。讀不到時全部是 0，不畫。
- **能力場景背景**：`Back_Image`＝surf **7156＋2×職業＋性別**（`sub_EE4030`；職業 7 用 7265／7266），由 `sub_EE6DD0` 進場景 2 時設定。缺圖就不畫。

### 1.2 選角（`NativeUi/SelectCharPanel.cs`）

- **IDA 發現與任務文字不同的地方**：`sub_EF0060` 先找 `Character_List`，但 kor25 SelectUI.xml 只有 `BigSlot_List`，沒有 `Class_Image`。`this+360` 是 `BigSlot_List`（`sub_EEC090` 0xEEC355），所以 TW16 讀這份版面時走的是 **BigSlot 分支 `sub_EF13F0`**。引擎照這個分支做：
  - 職業圖 c＝`sub_EEDD90`＝**6430＋8×職業＋4×性別**（有 BigSlot_List 時；沒有時是 7049＋6×職業＋3×性別；職業 7 用 8365／8368）。
  - `Select_Button`：一般 c、亮 c+1、按下 c+2；**選中列的一般與亮都是 c+2**。
  - 待刪除（`WaitDelete_Button`）三態都是 **c+3**（`sub_EEDE30`；職業 7 用 8371／8372）。
  - 欄位的等級標籤是 **`Lv.%d`**。
- **`Character_List` 版面的規則**（`Class_Image` 選中列＋2、`Select_Button` 7045／7046／7047、選中列三態都是 7047）：目前手上的 SelectUI.xml 都沒有這種版面，所以只寫成 `CharacterListSurfs` 這組資料並加上測試，沒有接到畫面。
- **生日**（`sub_EECB10`）：`sprintf("%d")` 之後依「第 0–3 字 `.` 第 4–5 字 `.` 第 6–7 字」排成 **`YYYY.MM.DD`**。比 8 位數短的值，照 C 字串在結尾處截斷。
- 上／下捲動沒有改：TW16 用 `this+352` 整頁翻並換 6411–6414 的圖，引擎維持一次捲一列。這不在本張範圍。

### 1.3 其他

- 新檔 `NativeUi/CharScreenFollowupTests.cs`（群組 `char-screen-followup`，`[WorldModuleTest]` 自動發現）。內容包含：兩張表的解析規則（略過的行號、重複列、atoi、15 個值的列、短列）、3.81 把關、表的載入順序（本機 → 參考客戶端，使用記憶體內的 Text.idx／Text.pak）、型態鈕的套用／切換／重設、進場自動套 Attack、送出的能力值、5 條與 6 條量表的寬度與畫素、背景圖號與缺圖、選角公式、待刪除列、hover 與按下、`Lv.`、生日。本機有 0-客戶端或 kor25 時，會另外核對真表的 SHA-256 與 3.81 檢查（CI 沒有這些客戶端，會略過）。
- **`NativeUi/SelectCharPanelTests.cs` 有改（超出任務列的檔案範圍，原因如下）**：生日格式、`Lv.`、職業圖鈕、`Gauge_Group` 改成會建立、背景改成 7156＋，這幾項都是本張刻意改變的行為，舊斷言只能跟著改。共 7 處：
  - 合成圖加入 6430–6432、6441、7156；
  - 「52」改成「Lv.52」；
  - 「20261110」改成「2026.11.10」；
  - 待刪除列 hover 不變亮；
  - `Gauge_Group` 由「不建」改成「已建」。

  其他斷言不動。
- 沒改：`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`、`docs/REPORT.md`。`--classic-ui` 不經過這兩個面板，WinForms 入口不變。

## 2. 依據（IDA 7.7 唯讀；IDB 是 P01 IDB 的 scratch 副本 `3a0bf206…e82f`，subst Q:，結束後已解除）

| 函式 | FO／VA | 用到的事實 |
|---|---|---|
| `sub_EE4110` | VA 0xEE4110 | CharAttrType 從第 2 行讀 8 個 atoi；StatGuage 從第 3 行讀 class、type＋14 個值（`sub_879EC0` 依序讀） |
| `sub_EE3D30`／`sub_EE3F80` | 0xEE3D30／0xEE3F80 | 取第一個符合的列；能力−7 不在 0–13 時回 0 |
| `sub_EE6760` | 0xEE6760 | 同型態＝`sub_EE5D60` 重設；套列的順序 STR=b2、DEX=b3、CON=b4、INT=b5、WIS=b6、CHA=b7（全域 030／03C／048／06C／054／060，以 `sub_EE34D0` C_CreateChar 的送出順序對出） |
| `sub_EE2F70` | 0xEE2F70 | 型態＝按鈕序號＋1 |
| `sub_EE5430` | 0xEE5430（反組譯 0xEE5476–0xEE5918） | 5 條／6 條各子項的型態與能力 |
| `sub_EE6DD0` | 0xEE6DD0 | 場景 2：型態歸 0 → Back_Image＝`sub_EE4030` → `sub_EE6760(1)` |
| `sub_EE4030` | 0xEE4030 | 7156＋2c＋s；職業 7 用 7265／7266 |
| `sub_EE5F50`／`sub_EE6450` | — | 職業頁的 Front_Image 是動畫（`sub_EE3F40`＝1573＋36c＋18s，模式 2、31 步），**不是 surf**，引擎不播 |
| `sub_EF0060`／`sub_EF13F0` | 0xEF0060／0xEF13F0 | Character_List 分支與 BigSlot 分支（見 §1.2） |
| `sub_EEDD90`／`sub_EEDE30` | 0xEEDD90／0xEEDE30 | 職業圖、待刪除圖 |
| `sub_EEC090` | 0xEEC355 | `this+0x168`（360）＝`BigSlot_List` |
| `sub_EECB10` | 0xEECB10 | 生日的字元重排 |

反編譯輸出只放在 `scratch/rea/ida/o1…o9`（不進 git）。

## 3. 資源查詢（PakViewer CLI，唯讀，輸出在 `scratch/t859/`）

| 檔案 | 來源 | 結果（SHA-256，解出內容） |
|---|---|---|
| `CharAttrType.tbl` | 0-客戶端 `Text.idx` No.2774，584 B | `64DA36ED6D718821A4B4A0B491ECFAAE72CF064B16894DE65C90B500D841575D`（＝FU9 的 `64da36ed6d718821`）；21 列都合 3.81 規則（python 離線核對） |
| `CharAttrType.tbl` | kor25 `Text.idx` 655 B | `17791EF622F9943F…3632`；多出職業 7 的 3 列。另外黑暗精靈型態 2／3 兩列的內容與本機對調 |
| `StatGuage.tbl` | kor25 `Text.idx` No.33375，2182 B | `2CF977EE45E6762271BAFE629A9894952701FA30019FC656FD0F493571D80C10`（＝FU9 `2cf977ee45e67622`）；職業 5 型態 5 那列有 15 個值，原版只讀前 14 個 |
| `StatGuage.tbl` | 0-客戶端 `Text.idx` | 沒有（`list` 查不到） |
| `CreateUI.xml`／`SelectUI.xml` | kor25 `Tile.idx`（解密） | `5277A2AF…D65F`／`B4EB1FA7…46B9`（＝FU9）；Gauge_Group 5 個 Image（6509），SelectUI 只有 BigSlot_List |

Sprite surf（查了 0-客戶端與 kor25 的 `Sprite.idx`、`Sprite00`–`Sprite15.idx`、`Tile.idx`，清單在 `scratch/t859/surf-query.txt`）：
- 6430–6489：0-客戶端 Sprite00–15 都有，所以選角職業圖鈕全部用本機圖。
- 6509（量表）：0-客戶端 Sprite15 有。
- 7156–7169：0-客戶端 Sprite 沒有（FU9：7156、7157 只在 LTsurf）；kor25 Sprite00–15 全有，執行期經 `SurfCatalog` 的參考備援（T322）讀取。
- 7045–7047、7049–7090、8365、8368：只在 kor25（只用在 Character_List 版面，目前不會查）。

## 4. 怎麼驗證

- 編譯：`dotnet msbuild engine/source/OfflineClient.csproj -t:ResolveReferences;CoreCompile -p:Configuration=Release -p:EnableWindowsTargeting=true`（restore 用離線 vendor，套件放 worktree 的 scratch）→ **0 錯誤**，本次檔案沒有警告。
- `python -B -X utf8 engine/tools/check-no-game-assets.py` → `{"status": "pass", "trackedFiles": 4353, "errors": 0}`。
- 依本機限制，單元測試不在本機執行，以 CI 為準。**CI（PR #881，run 42894667601／42894667643）**：`dotnet`、`map-continuity`、`npc-offline` 都 pass。World module 測試全部群組 failed 0，其中 `char-screen-followup` pass（99 項檢查）、`create-char-ui` pass（69 項）、`select-char-ui` pass（89 項）。CI 沒有客戶端，所以真表核對（LocalTables）依設計略過。

## 5. 未驗證什麼（待驗證）

- 真機畫面：職業圖鈕、量表長度、背景、自動套 Attack 的實際外觀與時機（需要開遊戲，交給 Codex／dot）。
- 3.81 `S_CharPacks` 的 `time` 是否為 `YYYYMMDD`（FU9 待驗證）。如果伺服器送的是別的數字，生日會照 TW16 的規則排成怪字串。
- `sub_EE2F70` 的 vfunc 324 是「在父項中的序號」：屬推測，但與 XML 順序（Attack、Defense、Mix）一致。
- `Character_Ani`、`DeleteDelay_Image`、職業頁 Front_Image 動畫、`StatType_Title`／`Desc`（kor25 XML 已註解掉）都沒做。

## 6. 產出路徑

- `engine/source/NativeUi/CreateCharPanel.cs`、`engine/source/NativeUi/SelectCharPanel.cs`、`engine/source/NativeUi/CharScreenFollowupTests.cs`（新）、`engine/source/NativeUi/SelectCharPanelTests.cs`（調整舊斷言）
- `docs/reports/issue-859.md`（本檔）

## 7. 給下一棒的提示

- 真機時看 UI 紀錄的 `create-char-ui CharAttrType.tbl …`／`StatGuage.tbl -> 參考客戶端 …` 兩行：裡面有 SHA-256 與「不合 3.81 規則」的列清單（本機表應該是「無」）。
- 選角的上／下鈕：TW16 是整頁翻（`this+352`，每次一頁＝4 列）並換 6411–6414 的圖。如果要照原版做，可以另開一張小任務，範圍是 `SelectCharPanel.cs`。
- 如果之後找到有 `Character_List` 的 SelectUI.xml（例如 TW16 自己的版本），可以直接用 `SelectCharPanel.CharacterListSurfs` 接線。
