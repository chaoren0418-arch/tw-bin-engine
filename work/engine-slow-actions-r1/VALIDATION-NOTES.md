# 驗證修正紀錄

- 第一輪候選 helper 791 checks 通過。主管隨後指出 sender 已准入、同session換世界後完成的 race；原完成點只有generation判定，候選加入生命週期／lease guard，並涵蓋成功及例外完成、target/input失效與較新lease不被finally清除。
- 新增逐幀 renderer 邊界時，helper 首次以 sequence[0] 為有時長第一幀，導致 `slow native first frame end -1 0` 失敗。固定gfx0素材的sequence[0]實際 ticks=0；原逐幀路徑會跳過它。helper改為依實際攻擊方向選序列、略過零時長列，再驗第一個有時長幀的−1／exact邊界。沒有為迎合此斷言修改產品動畫。
- profile-action-timings產物最初的legacy欄位缺milliseconds時未正確累計；已以原ticks×1000/24整數規則重新計算126列的native總和及marker前綴。此步只讀metadata，沒有重跑x86／JAR。最終helper另直接讀固定profile獨立核對。
- 最終實際DLL與helper結果以evidence/candidate-result.json、checks.command.json及stdout/stderr為準。前期迭代log在本未封存工作區被最終成功執行覆寫，不把早期通過數累加到result.checks。
