# Issue #915 報告：T400 R10-P 倉庫列內數量

依據：[`docs/reference/followups-12.md`](../reference/followups-12.md) §1（3.81 `LiTo.bin` 靜態分析，已驗證）、FU9§12.1。分支 `task/915`。

## 做了什麼

照 FU12§1.5 改寫原生倉庫窗 `WAREHOUSE_WINDOW`（`engine/source/NativeUi/WarehouseWindowPanel.cs`）：

1. 每列兩行：第 1 行是名稱（列 x＋38、y＋3）和持有量（靠右）；第 2 行是「▼ 數量 ▲」。**數量預設 0**；只有數量 > 0 的列算選取（`Selection`）。原本的「點一下＝整疊選取／取消」（`Toggle(id)`）和「把列拖到確定鈕」都拿掉了，3.81 沒有這兩種操作。
2. ▲（`Step(id, +1)`，對應 `sub_4D8870`）：+1，超過持有量就歸 0。▼（`Step(id, −1)`，對應 `sub_4D8830`）：−1，**在 0 時跳到持有量**。按下後要在同一顆鈕上放開才算（514），放到別處不動作。
3. 點列（`Focus`）：選取這一列，並把輸入緩衝 `InputBuffer` 清成 0，數量不動。之後的鍵盤（`Key`，對應 `sub_4D9770`）：
   - `0`–`9`（上排或數字鍵盤）：緩衝＝緩衝×10＋數字，夾到持有量（緩衝也停在上限），再寫回數量。
   - Backspace：緩衝 ÷ 10，再寫回數量。
   - 數字鍵盤 ＋／－＝▲／▼（4203／4205）。
   - Enter：攔下但不做事（送出要按確定鈕）。
   - 以下情況不攔鍵，按鍵照常交給遊戲：沒有選取的列、有修飾鍵、聊天輸入列開著（`find(ChatPanel.WindowName)`，作法同背包窗的 Delete）。
4. 確定鈕只送數量 > 0 的列，照伺服器順序（`sub_4D9930`）。送出後全部歸 0。取出時，TOTAL 右邊**一直**顯示「種類數 × 每種手續費（30）」的總額；`sub_4DA9D0` 對取出類型一定會畫這個數字。存入沒有手續費。
5. 新的清單（revision 改變）時，全部數量、選取列、緩衝都歸 0。同一份清單更新時：數量超過新持有量的列清掉；選取的列不見了，就取消選取。
6. 不做的：雙擊特殊動作、聊天列數量提示（原版都沒有）。
7. 註冊仍用 `[UiPanel(175)]` 自動發現，只多傳 `root.Find`；`IUiClosablePanel` 和 `--classic-ui` 的 WinForms 入口都沒變。

新的測試 `engine/source/WarehouseRowCountTests.cs`（`[WorldModuleTest("native-warehouse-row-count")]`），用合成 XML／surf 驗下列項目：▲▼ 的循環、按下後在別處放開不動作、雙擊沒有特殊動作、逐位輸入與夾值、多位數不溢位、Backspace、點列清緩衝、數字鍵盤 ±、Enter、修飾鍵／放開鍵／沒有選取列時不攔鍵、聊天開著時不攔鍵、確定只送 > 0 的列、手續費、存入沒有手續費、清單更新與換新清單、▲▼ 圖畫在按鈕框中央、缺圖時不畫但鈕照樣能用。

### 超出「可修改檔案」的一處（必要）

`engine/source/LegacyWindowContentTests.cs` 的 `Warehouse()` 一段原本在驗舊行為（點列＝整疊、拖到確定鈕）。新行為照任務要求做之後，這些斷言一定失敗，所以只改這一段：點列只選取、用 ▼▲ 設數量、拖到確定鈕不送、排序後 ▲ 仍對到畫出來的那一列。其他段（背包、魔法、共用）沒動。

## 版面數值與資源（已查，只讀，沒複製進 repo）

| 來源 | 檔案 | SHA-256 | 用途 |
|---|---|---|---|
| kor25 `D:\381懶人包kor25(原)\Tile.idx` | `ReWareHouse.xml`（原檔） | `0e10b345c0fcede67a3b40ef9837f3b6c020eadd8320cac865296b20318963a5` | — |
| 同上，`xml decrypt` 後 | `ReWareHouse.xml` | `f7c9d6c60a73dfea8e5ac3d313900194be2892567f60b7ca0726833bf6b3c8a3`（與 FU9 表的前 16 碼相同） | 對照 TW16 版面 |
| 同上 | `WareHouseCtrl.xml` 原檔／解密 | `b1c8660411eea1493cb167d1ad6a929ae020794f6af607cf5222d3a1c0b3fb02`／`f9a1e6c208176a73db0bb26120b3188f3a165219101eddca5fa3118f987c802b` | 對照 TW16 的捲軸與分頁 |
| 本機 `0-客戶端\Sprite01.idx` | `3003.img`（7×7） | `c46a466ba7074e455b37ef15383ab0effffb8e98825aaaaa96507b511799353c` | 列內 ▲（`SURF_RENEWAL_WAREHOUSE_UP_OFF`） |
| 本機 `Sprite03.idx` | `3005.img`（7×7） | `5b0456cb42c91349eacc600015fde55a07afc25e7b2191be59b18efd44983a46` | 列內 ▼（`SURF_RENEWAL_WAREHOUSE_DOWN_OFF`） |
| 本機 `Sprite02.idx`／`Sprite04.idx` | `3004.img`／`3006.img`（7×7） | `c059c722…50c1`／`689dfa4d…0843` | 推測是按下圖；XML 沒有列出，所以不用 |

- PakViewer CLI（`pakviewer-cli pak search／extract`、`xml decrypt`）查過 kor25 `Tile.idx`（`WareHouse` → `ReAddWareHouse.xml`、`RePetWareHouse.xml`、`ReWareHouse.xml`、`WareHouseCtrl.xml`），本機 `Tile.idx` 的 `SurfList.xml`（3001–3019 都對到同名檔），也查過本機 `Sprite.idx`、`Sprite00`–`Sprite15.idx` 裡的 `300[3-6]`。解出的檔只放在 `scratch/t915/`（不進 git）。
- **更正 FU9§12.1 的推測**：`ReWareHouse.xml` 的 `TradeCountLabel` **不在列裡**。它是窗本身的 Label（`X=27 Y=32`，74×16，`Caption="count"`），兩種模式（Inv／List）各一個。列表模式的列是 `RenewalWareHouseList`：`UnitWidth=208`、`UnitHeight=33`、`UnitGap=1`、7 列；`ReWareHouse.xml` 與 `WareHouseCtrl.xml` 都**沒有每列的 ▲▼ 鈕**。所以 TW16 的數量 UI 在哪裡，XML 看不出來。3.81 已驗證是列內調數量，本次照 3.81 做。
- 3.81 的 ▼▲ 用的是 `dword_834BD4` 圖表 554–557。這幾號對到哪個 surf 檔，FU12 沒查，本輪也沒查。引擎用 uiextra86 `WAREHOUSE_WINDOW` 裡兩顆沒給位置的 `SURF_RENEWAL_WAREHOUSE_UP_OFF`（3003）／`DOWN_OFF`（3005）當 ▲▼，這是**推測**（依名稱判斷，大小 7×7，放在 3.81 的 12×12 鈕框中央）。缺圖或缺元素時不畫圖，鈕照樣能點。

## 已驗證／推測／待驗證

- **已驗證**（FU12 靜態分析，本次照做）：數量從 0 開始、▲▼ 的歸 0 與跳到全部、點列清緩衝、逐位輸入夾到持有量、Backspace ÷10、數字鍵盤 ±、Enter 不送、只送 > 0 的列、取出費用＝種類數 × 30、鈕 12×12、▲ 在 x＋180、名稱在 (x＋38, y＋3)。
- **推測**：▼ 在 x＋120；第 2 行在列底往上 14 px（3.81 列高 34，uiextra86 列高 32）；▲▼ 圖用 3003／3005；Backspace 之後把緩衝寫回數量；選取列畫 1 px 外框；手續費數字畫在 TOTAL 右邊；有選取列時數字鍵與 Enter 由倉庫窗攔下。
- **待驗證（要真機）**：真機上 ▲▼ 的位置與大小、數字鍵會不會和遊戲快捷鍵衝突、Enter 被攔下時聊天能不能開；FU12§1.3 的「清單上限 1000」沒做（標「真機再確認」）；身上的金幣沒有顯示（view 沒有這個資料）。

## 怎麼驗證

- 本機編譯：`dotnet build engine/source/OfflineClient.csproj -c Release`，**0 錯誤**。4 個警告都是既有的（`HtmlActionTests.cs`、`MagicUi.cs`、NU1701），本次改的 3 個檔沒有警告。
  - 本機環境備註（和程式無關，同 #607／#855／#902）：worktree 沒有 `vendor`，`rebuild.py --build` 在 restore 就失敗；本機 `~/.nuget` 的 WebView2 套件只有 xml、沒有 dll，所以會出 NETSDK1130。處理方式：用 `scratch/t915/NuGet.local.config`（唯讀指向主專案 `engine/vendor`），`NUGET_PACKAGES=scratch/t915/packages`，再加 `-p:CustomAfterMicrosoftCommonTargets=scratch/t915/wv2-fix.targets`（拿掉 winmd，改參照 `lib_manual/netcoreapp3.0` 的 DLL）。以上都沒進 git。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：`status: pass`，errors 0。
- 照本機規定，**沒有執行**任何編譯出來的程式（沒有跑 `--module-tests`、DemoChecks、`dotnet test`）。新舊離線測試（`native-warehouse-row-count`、`native-warehouse-window`、`native-legacy-window-content`）綠不綠，**以 PR 的 CI 結果為準**。
- 沒有改 `rebuild-lock.json`、`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`。

## 未驗證什麼

- 離線測試在本機沒跑，交給 CI。
- 沒有真機驗收（不啟動遊戲）：畫面位置、鍵盤與遊戲快捷鍵的互動、送出後伺服器的回應。

## 產出路徑

- `engine/source/NativeUi/WarehouseWindowPanel.cs`（改）
- `engine/source/WarehouseRowCountTests.cs`（新）
- `engine/source/LegacyWindowContentTests.cs`（只改倉庫一段，原因見上）
- `docs/reports/issue-915.md`（本報告）

## 給下一棒的提示

1. 真機（Codex／dot，P24Lab 測試角色）：開倉庫取出和存入，確認以下幾點：
   - 每列 ▼ 數量 ▲ 看得到，▲▼ 的循環正確；
   - 點列後打數字、Backspace、數字鍵盤 ± 都有作用；
   - 確定鈕只送有數量的列；
   - TOTAL 旁的手續費＝種類數 × 30；
   - 選了一列之後按 Enter，聊天列能不能開（目前會被倉庫窗攔下，照 3.81）。
2. 如果要比對 3.81 的 ▲▼ 圖：查 `LiTo.bin` 快照的 `dword_834BD4` 圖表 554–557 對到哪個 surf（FU12 沒查）。如果不是 3003／3005，只要改 `CountUpName`／`CountDownName`，或是改用 surf 編號。
3. FU12§1.3「開過商店再開倉庫，可能被夾到 1000」：真機看到時再加清單上限。
4. 如果要在 TOTAL 旁顯示身上的金幣，`UiWarehouseView` 要多帶這個值；`UiWarehouseView` 在本檔，但資料要從 `WorldSession` 取，屬於另一張任務。
