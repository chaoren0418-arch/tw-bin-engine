# T073 離線回歸

先依 `docs/BUILD.md` 在本 worktree 建置 dev 並接好唯讀 `engine/assets`、離線 `engine/vendor`。在 repo 根執行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File engine/_integration/dev/world-ui-tests/run.ps1
```

產物在各測試專案 `bin/obj`；結果與 WebView2 合成素材／profile 在 `scratch/issue-130/`。`WorldUiChecks` 載入本次產品 DLL，禁止 socket；不呼叫遊戲入口、ClientForm 建構子、登入或音訊裝置。ClientForm 只執行無視窗 handle 的按鍵清理方法；HUD 使用真實 WinForms controls；renderer 唯讀取本機素材且不保存圖片。WebView2 檢查另用透明離線 Form 與自建 2×2 PNG，攔截所有資源請求。沒有任何測試以替身資料宣稱遊戲功能已真機通過。

## P27 的 12 組計畫對照

原計畫：`work/P27-ui-input-candidate-r1/native-acceptance-plan.json`。它是未執行的驗收計畫，不是 12 個已確認缺陷；既有實作仍保留，這次只修有來源或測試依據的問題。

| P27 ID | 本次離線覆蓋 | 留給 T075 |
|---|---|---|
| N01 同版身分 | canonical build、DLL SHA、dev lock | 啟動版本、PID、geometry |
| N02 登入控制 | 鍵盤焦點隔離；登入 Enter guard 純判定 | 原生 Tab 順序、密碼欄、登入 |
| N03 八方向／停止 | DemoChecks 八方向 C29／阻擋、WorldUiChecks 取消路徑 | 實際按鍵與行走畫面 |
| N04 切焦點 | ClientForm 清理方向及快捷鍵 held；失焦 route 及 pending action 取消 | Alt-Tab／最小化／實際 key-up |
| N05 場景邊界 | HUD、診斷面板邊界與覆蓋控制項拒點；陰影不擁有選取像素 | 真實視窗 hit-test、DPI |
| N06 點地路線 | 有限路徑、C29、取消、新佔位、校正；角落阻擋 | 原生點地終點與伺服器校正 |
| N07 選取／攻擊／交談 | 選取零封包、C229/C34 單次、延遲操作失效、NPC 4/5/6 格 | 物件邊緣雙擊／Ctrl／右鍵 |
| N08 HUD／快捷槽 | 真實 HUD controls、HP/MP、忙碌、缺物品、角色隔離、清槽 | 實際 F1–F4、視覺排列 |
| N09 聊天 | 開啟 Enter 防重送、draft／echo／換角色清除、遊戲按鍵隔離 | IME composition、OS 焦點、伺服器 echo |
| N10 NPC 面板 | C34→S39、目標移除後拒絕晚到頁；場景遮擋 | 原生服務頁操作與可讀性 |
| N11 Web／F9 | 20 個本機圖示 bytes/SHA、合成 WebView2 HTTP/MIME/decode/404、panel.js 目標／數量／座標 | 真實背包圖示、F10/F11、Web 原生焦點 |
| N12 視窗／抽屜 | 兩種 client width × 抽屜狀態的 viewport 邊界 | 實際 DPI、縮放、關閉重開 |

另驗 S87 死亡→存活完整更新、S120→晚到 S10→同 ID 重現、S76→S87、同圖 S206、Cancel→重新建立世界，以及離線 renderer 的移除與重現。合成封包不代表已驗證目前 P24Lab 的實際送包順序。
