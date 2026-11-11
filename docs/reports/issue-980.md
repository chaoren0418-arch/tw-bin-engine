# Issue #980：T427 gap-matrix 第九輪與 M7 結案判定

依據：gap-matrix 15.13（T412／#952）。分支 `task/980`，依 main `80801578`（R11-B #958 在本輪開始時已合併，PR #981）。

## 做了什麼
- `docs/reference/gap-matrix.md` 新增 **15.14**（方法、狀態變更表、統計、分類、K5／K7 逐列說明、R12 分組、M7 判定、真機變動、限制）。
- 新腳本 `docs/reference/rea-scripts/gap_count_r9.py`：依序套 15.3、15.12、15.13、15.14 的變更表，分類只讀 15.14；K7 的列另外印「不算預期」。
- `gap_count_r8.py` 改成讀到 15.14 之前就停（只加 1 行），仍重現第八輪 228／21／41、K1 55、K5 7；`gap_count_r7.py` 仍重現第七輪。
- `docs/testing/smoke-m7.md` 新增 §14「第九輪對齊」（12 項預期更新、5 項補步驟），並在舊的 SM15、SM57、§13.2 SM34／SM35 加指向 §14 的註記。

## 結果
**290 列的去向**：已有 229＋K7 1（4:WidgetSlot，#969，預期已有）＋K1 55＋K5 5＝290；K2 0。含預期 230／19／41，不算預期 229／20／41。
- 部分→已有 6 列：3:C_INVITE_PARTY(82)、3:C_ADD_BUDDY(138)、4:BehaviourUI、4:PersonalShopUI、4:SelectUI、4:CreateUI。
- 已有→部分 4 列（嚴格規則：有依據、程式沒做）：5:`sub_87C890`（自己的動作 71、層旗標半透明例外）、4:AgitAuctionUI（4381 沒接聊天欄、Ok 後不關窗）、1:99（信件印引擎自己的句子，不是字串編號）、4.2 `BoardListWindow`（右鍵雙擊、刪文等待）。
- 4:OptionUI 仍是 K5（類別音量沒存檔）。
- 進行中：#969→4:WidgetSlot（K7）；#966→4:BookmarkWindowUI、1:144、4:PledgeUI、PersonalShopUI 的 option12（維持已有）；#953 不對應任何列。

**M7 判定：還不能結案**。阻擋：K5 5 列（R12-A–E）＋K7 1 列（#966→#969）。結案摘要草稿與需要裁定的 2 點在 15.14.7。

## 怎麼驗證
- `py -3 -I docs/reference/rea-scripts/gap_count_r9.py docs/reference/gap-matrix.md --list`：沒有錯誤，輸出就是 15.14.3／15.14.4 的表。
- `gap_count_r8.py`、`gap_count_r7.py` 重跑，數字和 15.13.3、15.12.3 相同。
- 呼叫點用 Grep 和讀檔抽查（列在 15.14.1）；「沒做」的說法逐項對照任務報告的原句與程式碼（`RingWindows.cs:249`、`AgitAuctionPanel.cs:903–919`、`Mail.cs:115–129`、`BoardPanel.cs:215`、`Invisibility.cs:17`、`sound.md` 第 205 行）。

## 未驗證什麼
- 沒有建置、沒有執行任何程式；沒有開 IDA；沒有啟動遊戲、伺服器或 DB。
- 第八輪前的「已有」列只複核了 6 列（15.14.1），其餘沒有逐列重看。
- 本輪沒有新的「缺資源」宣稱，所以沒有用 PakViewer 新查；R12-D 的字串編號要在任務內查。
- 「已有」全部沒有真機驗收。

## 產出路徑
- `docs/reference/gap-matrix.md`（15.14）
- `docs/reference/rea-scripts/gap_count_r9.py`（新）、`gap_count_r8.py`（1 行）
- `docs/testing/smoke-m7.md`（§14 與 4 處註記）
- `docs/reports/issue-980.md`

## 給下一棒的提示
- R12-A–E（15.14.6）的檔案互不重疊，可以同時派；R12-C 要先裁定（15.14.7 第 1 點）。
- 裁定時要知道：R12-C、R12-E 若裁定為取捨，阻擋列少 2 列；R12-D 建議 classic 的窗內文字一起改。
- #966 查完後要看 Q9-2／4／5 有沒有帶出「有依據、沒做」的子項，有的話轉 K5；#969 合併後把 4:WidgetSlot 從 K7 拿掉。
- 下一輪（第十輪）要寫 `gap_count_r10.py`，並讓 r9 讀到 15.15 之前就停（同本輪對 r8 的做法）。
