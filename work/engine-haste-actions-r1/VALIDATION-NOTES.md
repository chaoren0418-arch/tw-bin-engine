# 驗證紀錄

r30 gap helper 25項通過，證明既有Speed1攻擊／施法仍使用未加速時長，屬本增量缺口。

第一輪新候選suite在`haste shared busy exact attack/item`失敗：helper將ActionClock推進到未來，但r30道具final gate仍讀實際Environment.TickCount64，尚未跨過相同deadline。修正helper將該組起點錨定過去，讓既有實際時鐘閘門先開啟，再單獨驗證action busy −1／exact。未為此修改道具、拾取或其他產品來源。F1補驗也加入真正的舊未知MotionSpeed快照，確保恢復haste後不需新move便可見。

最終通過數與實際載入DLL以evidence/candidate-result.json為準。未封存迭代的執行log由最後一次執行覆寫，不把迭代通過數累加。adapt_tests.py及haste-tests.cs.inc保留測試轉換方式；舊封存helper保持只讀。
