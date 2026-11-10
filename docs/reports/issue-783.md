# Issue #783 報告：T337 查證原版 Q6-1、Q6-2、Q6-4 到 Q6-9

階段 resource，第 1 次嘗試。分支 `task/783`。

## 做了什麼

- 用 IDA 7.7 唯讀批次查 `TW1608262200.bin`，共 51 輪（q1–q51）。
  - IDB 是 P01 IDB 在 `scratch/rea/ida/` 的副本，SHA-256 `3a0bf206…7e82f`；BIN 的 SHA-256 是 `e12949cf…41a2`。
  - 腳本沿用 `rea-scripts/t742/ida_t742.py`，沒有建函式或改名。
- 用 PakViewer CLI 重列 `0-客戶端` 全部 24 個 idx，以及 kor25／t89／ly 的 `Tile`、`Text`、`Sprite*.idx`（`pak_index.ps1 -WithAlt`）。解出 `Actions.xml`、`ActionUI(-c).xml`、`BehaviourUI.xml`、`WidgetSlot.xml`、`WidgetSubSlot.xml`、`8393.img`、`alertitems.tbl`、`SurfList.xml`，都只放在 `scratch/`。
- 結論寫在 [`docs/reference/followups-8.md`](../reference/followups-8.md)（FU8）：
  - **Q6-6（優先，擋 #784）**：
    - 自己的 P＝`sub_F1A750(obj+16)`，就是精靈區塊偏移原點，和引擎 `ObjectRendering.cs:159` 的約定一樣（`Screen − (24,12)`）。
    - 頭上條 x＝P.x − 80；名稱 X0＝P.x − 56 − 3·len。
    - 原版相機讓自己的 P 固定在 (456, 288)，所以名稱與頭上條都以 x＝400 置中。
    - 閃爍只畫自己：一般職業 ≤35% 閃 5 次，戰士 ≤40% 一直閃，間隔 max(300, 26·HP%) ms。
    - 其他物件只畫 HP。+86／+87 變體只在 HP 窗。
    - surf 8393 本機沒有，kor25／ly 有。
  - **Q6-1**：鑑定訊息印在聊天欄（淡黃），先印訊息再印 `ItemDesc-c.tbl` 的說明；928 與 4139–4142 兩個特例。
  - **Q6-2**：C_DUEL 唯一送出點是 `/duel`、`/決鬥`（`Commandduel`），無欄位。決鬥對手用一般攻擊游標。
  - **Q6-4**：TW16 是 `BehaviourUI.xml`（本機沒有、kor25 有）＋`Actions.xml`（本機有）。7 個回呼都查到了。本機另有較舊的 `ActionUI-c.xml`，推測是 3.81 的動作窗。
  - **Q6-5**：105 `cloth`（本機 1,930 個 gfx）、107、111、112、114、118 的讀取者都找到。引擎都沒解析。
  - **Q6-7**：bit 0＝不可通行（地圖屬性加 S_Door），拾取和移動用同一個判斷。
  - **Q6-8**：值 1＝GM 角色而且 ≤50 級；3.81 一律送 0，不會出現。
  - **Q6-9**：刪除時只有 `alertitems.tbl`（本機 232 筆）內的物品問 Y/N。料理清單 34×34。
  - **WidgetSlot**：`SlotWidgetUI*.xml` 在所有清單都沒有；實際檔名是 `WidgetSlot.xml`／`WidgetSubSlot.xml`，與 #800 的 15.2 第 3 點一致。
- `gap-matrix.md` 新增 **15.10 節**：
  - Q6 的 8 列從 K2 改歸 K5，K2 剩 28 列；
  - 建議 R8-A–E 五組 code 任務；
  - 新增真機項目 M31–M33。
  - 15.5 的表沒有改，所以 `gap_count_r6.py` 的計數不變。
- `INDEX.md` 加 FU8 一列。腳本與查詢放在 `docs/reference/rea-scripts/t783/`。

## 怎麼驗證

- 每個結論都附 VA／FO，以及反編譯或反組譯的出處。
- 字串用本機 `string-c.tbl`（3484 筆）在記憶體解碼。list.spr 欄位用新腳本 `listspr_fields.py` 數本機 `list.spz`。3.81 端讀伺服器原始碼：`S_IdentifyDesc.java`、`S_CharPacks.java`、`S_Door.java`。
- 宣稱「本機沒有」的檔案（`BehaviourUI.xml`、`8393.*`、`SlotWidgetUI*.xml`）都在 24 個 idx 的 PakViewer 清單搜過，另搜了 3 套參考客戶端。
- subst：開始時沒有代號被占用；每輪用 `W:`、輸出接 `| Out-Null`；每輪結束 `subst` 都沒有輸出。
- `py -3 -I docs/reference/rea-scripts/gap_count_r6.py docs/reference/gap-matrix.md`：計數不變（K1／K2／K5＝52／36／47），沒有錯誤。
- 只改文件與唯讀腳本，沒有改產品程式。CI 看 PR 結果。

## 未驗證什麼

- 3.81（`LiTo.bin` 有殼）的表現都只能推測：
  - 動作窗用 `ActionUI-c.xml`；
  - 會不會畫 `cloth` 附加層。
- 原版相機把腳下點放在 (480, 300)，引擎放在畫面中央。自己在畫面上的絕對位置可能差 80 px 左右，要真機看（M32）。
- 「BehaviourUI 依 UI 名稱讀 XML」是推測：BIN 的 XML 檔名表沒有這個檔名。
- `alertitems.tbl` 比對的編號（`this[19]`）對到 3.81 的哪個欄位，要先對 SP 原始碼。
- 料理清單的 vtable +88 由哪個滑鼠事件觸發，沒追（推測是雙擊）。
- 游標編號只對到 PE 資源 id，游標圖本身沒解。
- 字串 4114、4115 本機沒有（同 FU7）。
- 全程沒有真機觀察。

## 產出路徑

- `docs/reference/followups-8.md`（新）
- `docs/reference/gap-matrix.md`（新增 15.10 節）
- `docs/reference/INDEX.md`（加 1 列）
- `docs/reference/rea-scripts/t783/`：`q1.txt`–`q51.txt`、`listspr_fields.py`
- `docs/reports/issue-783.md`（本檔）

## 給下一棒的提示

- **#784（R6-B）**：照 FU8§1.1 用相對公式 `P = Screen − (24,12)`。名稱 X0＝P.x − 56 − 3·len，不必等真機。
- **新 code 任務**（gap-matrix 15.10）：
  - 可以馬上派：R8-A 頭上條、R8-B 鑑定訊息、R8-C 決鬥指令。
  - R8-D 動作窗：排在 R7-I 之後（共用 `MenuButtonsPanel.cs`）。
  - R8-E list.spr 附加層：等 #784、#801 合併，`cloth` 先真機確認（M31）。
- **R6-D**：不同格的拾取檢查用 `ApproachWalk.cs:49` 的通行判斷；`byte_13CE51E` 只要處理 2。
- **R6-E**：刪除的 Y/N 只問 `alertitems.tbl` 內的物品。
- **下一輪盤點**：把 15.10 的分類變更套到 15.5，K2 只剩 Q7 的 28 列。
- **IDA 小提醒**：`grepfuncs` 的正規式不能有空白（查詢行用空白切欄位）；要配空白請改用 `rangegrep`，或用 `\s`。
