# Issue #899（T386）：list.spr altdir／stride／cloth 的接線時機

## 做了什麼
- 用 IDA 7.7 唯讀分析 TW16 BIN 的 IDB 副本，和 3.81 `LiTo.bin` 的 OEP 快照 IDB 副本，查清 112 `altdir`、111 `stride`、105 `cloth` 三項。
- 結果寫成 [`docs/reference/followups-14.md`](../reference/followups-14.md)（FU14）。
- 回填 `gap-matrix.md`：15.10 的 M31、15.12.4 K2 的原因欄、15.12.5 的 5:`sub_87C890`、Q8-2、15.12.7 的 M31、15.12.8。
- 新腳本：`rea-scripts/t899/`（`listspr_compare.py`、`cloth_tiles.py`、`frames.py`、執行器、查詢清單）。

## 結論
- **altdir**：只用在「沒有動作表的靜態外型」，每畫一格依朝向重算（朝向 6 用替代 gfx，轉走就換回）。本機只有 1395／5940 兩張椅子用到。
- **stride**：每走一步（＝每格）換一次走路動作。動作表 `byte_1379D40`＝{0,4,11,20,24,40}，計數 0…n 循環；本機 stride 全是 1，所以是動作 0／4 交替。FU8 寫的「站立時」是誤讀。
- **cloth**：
  - 和本體用同一個 spr 子檔號；任何一層缺子檔，這一格整組不畫。
  - 3.81 會解析、也會畫，而且沒有 TW16 那個「自己＋職業外型」的例外。
  - 職業外型的 6262 等是**釣魚線**，只有釣魚動作（71）的 8 個子檔有圖。
  - 依據見 FU14§3.5：`ClothVerified` 可以打開，M31 不再阻擋。

## 怎麼驗證
- 反編譯／反組譯（地址見 FU14§0.3）。
- 本機與參考客戶端的 pak 清單：PakViewer `pak list`，24 個本機 idx 加 kor25／ly／t89。
- spr 檔頭圖塊數：直接唯讀本機 `Sprite*.pak`。
- 3.81 伺服器原始碼（唯讀）。
- `gap_count_r7.py` 改前改後輸出相同。

## 未驗證什麼
- 沒有真機。釣魚線、NPC 衣物層的實際畫面留給 M31（smoke 時順便看）。
- stride 計數的初始值（推測 0）。
- 引擎 assets 有沒有 1410 的 profile。

## 給下一棒的提示
- 開 1 張 code 任務接線，位置與目標見 FU14§1.3、§2.4、§3.6、§4：
  - `ObjectRendering.cs:8–9`、`:16`、`:22`、`:33–40`、`:62–65`、`:124`；
  - `ClientLogin.cs:53`；
  - `WorldSession` 的他人走路計數。
- 職業外型的釣魚線要等引擎真的播出動作 71 才看得到（`ObjectRendering.cs:35–36` 現在改播 `"walk"`）。
