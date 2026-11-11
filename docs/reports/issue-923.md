# Issue #923 — T406：R10-S 選目標瞄準游標（FU13 D1–D2）

依據：[`docs/reference/followups-13.md`](../reference/followups-13.md) §2、D1–D2（T385／#898）。

## 做了什麼

- **新檔 `engine/source/GameCursorSource.cs`**
  - `GameCursors`：`sub_8EECE0` 的索引常數（3 手形、8 瞄準、15、19、20、23、24）和 BIN 兩張游標 id 表全部 36 筆：單色 `lpCursorName`（VA `0x13720E8`／FO `0xF708E8`）、彩色 `dword_1372180`（VA `0x1372180`／FO `0xF70980`）。
  - `SpellCursorRules.Index(...)`：`sub_F044C0` 選目標分支（`0xF04688` 起）的純函式。不在模式中 → 3；非技能回呼 → 8（alternate 15）；技能回呼：`sub_E23BF0(0)` 為假 → 20（24），在範圍內 → 8（15），不在 → 19（23）。
  - `PeCursorResources.Read(byte[], groupId)`：自己寫的唯讀 PE 資源解析，順序是 `RT_GROUP_CURSOR`(12) → `RT_CURSOR`(1)，回傳熱點和 DIB 的位元組；格式不對就回 null，不丟例外。
  - `GameCursorSource.Get(index)`：執行時唯讀讀取本機原版 BIN，算 SHA-256，用 `CreateIconFromResourceEx` 建出 `Cursor` 並快取。BIN 路徑先看 `TW_ORIGINAL_BIN`，沒設就用 `D:\lineage開發\TW1608262200研究\P01-分析基準\input\TW1608262200.bin`（0-客戶端裡沒有 TW16 BIN，只有 `LiTo.bin`／`swap.bin`）。先試彩色組，讀不到改用單色組；兩組都讀不到時，瞄準游標用 `Cursors.Cross`、手形用 `Cursors.Default`，並設 `EngineCustom=true`（引擎自訂）。SHA 與已知值不同時，`Notice` 會寫明。**游標圖不進 git。**
- **`engine/source/DuelUi.cs`（世界滑鼠游標所在檔，只加游標切換）**
  - `UpdateAttackCursor`（由 `ObjectRendering.cs` 的世界 `MouseMove`／`MouseLeave` 呼叫）：`SpellTargetPicking` 為真時換成瞄準游標，而且優先於攻擊游標。範圍判斷是：滑鼠下有物件時，算角色到物件的 `TileDistance` 是否 ≤ `pick.Range`（15）。
  - 離開模式時還原成 `GameCursorSource.Get(3)`。另外在表單 `timer.Tick` 加了 `SyncSpellCursor()`：選完或取消後即使滑鼠沒動也會還原（對應原版 `sub_F68E60` → `sub_8EECE0(3)`）。
  - 一般非攻擊狀態原本是 `Cursors.Default`，改成 `Get(3)`。有 BIN 時就是原版手形，沒有 BIN 時仍是 `Cursors.Default`。
- **`engine/source/Magic.cs`**：兩處註解從「待真機（M35）、引擎不換游標」改成「已驗證（FU13§2）」。
- **新檔 `engine/source/SpellCursorTests.cs`**：`[WorldModuleTest("spell-cursor")]`，靠自動發現，沒有改任何註冊表。內容：
  - 游標規則的 11 種情況；
  - 兩張 id 表的長度和關鍵索引；
  - 用合成的最小 PE（記憶體內建出）測解析：正常讀出、沒有的群組、空檔、截斷、簽章錯、沒有資源目錄、group type 錯；
  - 本機有原版 BIN 且 SHA 相符時，再核對 208／105／223 的游標 id 和熱點（CI 沒有 BIN，會略過）。

## 已驗證

- 用 pefile 唯讀重讀 BIN（SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`；腳本 `scratch/t923/cur.py`，不進 repo），兩張 id 表和 FU13§2.1 完全一致。各群組的資源如下：

  | 群組 | 用途 | RT_CURSOR | 大小／色深 | 熱點 |
  |---|---|---|---|---|
  | 105 | 單色手形 | 2 | 32×32、1 bit | (1,0) |
  | 136 | 單色瞄準 | 8 | — | (13,6) |
  | 145 | 單色 | 16 | — | (13,6) |
  | 202 | 彩色手形 | 19 | 24 bit | (0,0) |
  | 208 | 彩色瞄準 | 25 | 24 bit | (13,6) |
  | 216 | 彩色 | 33 | — | — |
  | 220 | 單色 19 | 37 | 8 bit | — |
  | 223 | 彩色 19 | 40 | — | — |
  | 227 | 單色 20 | 43 | 8 bit | — |
  | 229 | 彩色 20 | 45 | — | — |

  每組都只有 1 筆，`RT_CURSOR` 共 67 個。
- 本機建置：`dotnet build engine/source/OfflineClient.csproj -c Release`，**0 錯誤**。3 個警告都在既有檔案（`HtmlActionTests.cs`、`MagicUi.cs`），不是本次改的。本機 restore 會遇到 NETSDK1130（同 #607／#675），所以照那兩份報告的做法：`scratch/t923/NuGet.Config` 唯讀指向主專案 `engine/vendor`，`NUGET_PACKAGES` 指到 `scratch/t923/packages`。這和程式變更無關。
- `py -B -X utf8 engine/tools/check-no-game-assets.py` → `status: pass`（4470 個追蹤檔、0 錯誤）。
- 資源查詢：游標是 BIN 內嵌的 `RT_CURSOR`，不在任何 .pak 裡。本任務**沒有宣稱缺資源**。FU13§0 已經用 PakViewer 查過 0-客戶端全部 24 個 idx（`Text`、`Tile`、`Sprite`、`Sprite00`–`Sprite15`、`LiTo\LTtext`、`LTsurf`、`LTsprite`、`LTtile`、`LTicon`），本任務沒有重查。

## 推測（程式中已註明）

- 彩色／單色由 `byte_13A931E` 決定，但預設值沒查。引擎先用彩色組。
- `sub_E23BF0(0)` 的意義沒查，引擎一律當真，所以 UI 實際不會出現 20。規則本身已經測過。
- `byte_139FBAF`／`byte_13CE3F8` 引擎當假，所以不會出現 15／23／24。
- 滑鼠下沒有物件時，視為在範圍內（顯示 8）。原版 `sub_523AC0(x,y,範圍)` 的 x、y 可能是滑鼠所在格，沒查。

## 未驗證／待驗證（交 Codex 或真機）

- **module tests 只在 CI 跑**：依本機限制，本機不執行任何編譯出的程式。`spell-cursor` 組的結果以 PR 的 CI 為準。
- 待真機：
  - 進入選目標模式後移動滑鼠，應該看到 BIN 的瞄準橢圓（熱點 (13,6)）；
  - 對象在 15 格外應該是紅叉（19）；
  - 選完或取消後，應在約一個計時器週期內還原成手形；
  - `CreateIconFromResourceEx` 建出的彩色游標，顯示是否正確。
- 已知小差異（待真機）：表單一開始仍是 `Cursors.Default`。要等第一次游標切換之後，才會變成 BIN 手形。表單的 `Cursor` 也會套用到沒有自設游標的子控制項，這點和既有的攻擊游標相同。

## 產出路徑

- `engine/source/GameCursorSource.cs`（新）
- `engine/source/SpellCursorTests.cs`（新）
- `engine/source/DuelUi.cs`
- `engine/source/Magic.cs`
- `docs/reports/issue-923.md`

分支：`task/923`。

## 給下一棒的提示

- 真機驗收時看上面「待真機」幾項。如果需要彩色／單色切換，再查 `byte_13A931E` 的設定來源。
- 想讓開場游標就是原版手形，可以在表單載入時設 `Cursor=GameCursorSource.Get(GameCursors.Hand)`。這不在本任務範圍，所以沒有做。
- 其他選目標入口（組隊 `/invite` 的 `PartyInviteTargeting`、右鍵傳送點 type 0 → 8／11）還沒接游標。之後可以共用 `SpellCursorRules`／`GameCursorSource`。
