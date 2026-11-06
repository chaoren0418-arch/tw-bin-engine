# Issue #129／T072：戰鬥閉環

## 做了什麼

- 唯讀核對本機伺服器攻擊、死亡、EXP、掉落、學技、彈藥與恢復流程；封包布局、方法與 24 份來源 SHA-256 見 [combat.md](../protocol/combat.md)。未讀取機密設定，未啟動遊戲、伺服器或 DB。
- 弓箭無箭時不送 C123，排隊送出前重新檢查；箭數只依背包回包更新。死亡清除選取與舊動作，新增原生「死亡後重新開始」按鈕／C177，接收 S85 復活並等待 HP 與外觀權威資料。
- 保留既有 EXP、戰利品拾取、NPC 學技與施法流程；新增各子功能的合成封包閉環與競態回歸。更新舊弓箭真機測試的耗盡預期，但未執行真機測試。
- 依派工開頭 `workdir: project` 在專案本體工作，fetch 後從 `origin/main` 建立 `task/129`；保留既有未追蹤證據，未改 `docs/REPORT.md` 或封存 lock。

## 怎麼驗證（已驗證）

- `py -3.12 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock`：172 個來源／181 個 pins 通過；只更新 dev lock。
- `py -3.12 -B -X utf8 engine/_integration/dev/rebuild.py --build --write-receipt --output engine/build/issue-129`：`PASS dev`，產品／launcher 編譯成功。產品有既存 `MagicUi.cs:20` 的兩個 nullable 警告、0 錯誤。舊 dev receipt 已先備份至 `scratch/issue-129/previous-dev-receipt.json`。
- `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release -- <repo> <repo>/scratch/issue-129/demo-checks.json`：**437 個檢查通過**，其中本次戰鬥閉環 64 個；實際執行產品 DLL，記憶體 sender，socket guard，沒有遊戲視窗或伺服器連線。
- 覆蓋：三次明確近戰 → 死亡 → EXP 回包；自然掉落形狀的 S87 → C112 → 入袋；C145/S41/C39/S164 → C6；箭矢回報、2→1→0、重複／延遲回包、耗盡與排隊取消、重登清單基線；死亡鎖定、C177 去重、S85 截斷原子性、恢復後 C29、斷線／死亡恢復競態。
- `py -3.12 -B -X utf8 engine/tools/check-no-game-assets.py`：通過；`git -c core.whitespace=cr-at-eol diff --check`：通過。GitHub CI 狀態以本分支 PR checks 為準。
- 本次產品 DLL SHA-256：`88906ef00e2eb005059eb524367d9eaff64db88c8f7a13b87f645ac734c61504`。

## 未驗證什麼

- 真機擊殺／自然掉落、學費扣除、箭矢飛行與音效、死亡面板可見性、恢復後實際步行，以及真正重新登入後的伺服器箭數保存。合成背包基線不是 DB 持久化證據。
- 本機來源與正在使用的 JAR 是否完全一致。其他箭種、魔法弓、未核定地面圖像及復活 Y/N 對話不在此次擴充範圍；死亡恢復使用 C177 入口。

## 產出路徑

- `docs/protocol/combat.md`、本報告。
- `engine/source/Recovery.cs` 與戰鬥／彈藥／死亡處理掛接、`engine/_integration/dev/rebuild-lock.json`。
- `engine/_integration/dev/demo-tests/CombatLoopChecks.cs`；可用相同測試指令加第三參數 `combat-loop` 單跑。
- 未追蹤證據：`scratch/issue-129/demo-checks.json`；建置結果與 log：`engine/build/issue-129/`；既定產品輸出：`engine/build/client-product-dev/`、`engine/launcher/`。

## 給下一棒的提示

Claude 審查 PR、使用者合併後，由 T075 的 dot 在授權 P24Lab 測試角色驗收：選有可識別自然掉落的怪物，記錄擊殺前後 EXP／入袋；向 NPC 學既有支援技能（例如 4）並施放；普通箭逐發觀察權威數量、耗盡拒送、重登數量；死亡時確認操作鎖定，點「死亡後重新開始」，待 HP／地圖／自身包後測八方向移動。每項記前後數值與封包／畫面，不以離線結果代替真機證據。
