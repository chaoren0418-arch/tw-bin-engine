# #272／T132 決鬥：正式接收整合與同步驗證

## 做了什麼

- `Duel.cs` 實作 C5 邀請、C121 接受／拒絕，以及 S219/630 邀請、S250/5 開始／結束、S71 決鬥回覆。C5 經唯讀核對 `C_Fight` 無條件尾段；Yes-No 的 D 欄位固定零，不依賴 requestId／raw confirmation，也未實作 `S_Classic*`。
- `DuelUi.cs` 提供「服務 → 決鬥」頁面與邀請／接受／拒絕操作；只有 S250/5 確認後顯示對手標記，`ObjectRendering.cs` 僅增加一個繪製查詢呼叫。未修改攻擊准入。
- 依第二次重派修正要求，在 `WorldSession.ReceiveCore` 讀取 opcode 後新增單行 `if(ReceiveDuel(opcode,plain))return;`。解析仍集中在決鬥模組；使用獨立 reader，非決鬥 S219／S250 保留原分派，S71 更新決鬥狀態後仍交由既有訊息處理器顯示。
- 本輪依調度器「只做同步」要求，將 `origin/main` 合入 `task/272`（開始時落後 122 個 commit，等 CI 期間 main 又前進，最後合到 `d604050`／PR #400）。文字衝突有三處：`DemoChecks.csproj` 保留雙方的測試檔；`demo-tests/Program.cs` 的 `combat-admission` 同時保留 main 的 `AttackSkillChecks`、受擊抑制與決鬥測試，`duel`、`attack-skills`、`attack-skill-resources` 三個入口都保留；dev lock 先取 main 版再以 `--update-lock` 重產，沒有手動拼接 hash。`docs/REPORT.md` 採 main 版本。
- **本輪唯一的非同步修改**：文字合併沒有衝突，但編譯失敗（CS0111）。main 的 `LocationShare.cs` 也 override 了 `ClientForm.OnShown`，與 `DuelUi.cs` 重複；`OnLoad` 已由 `BuddyUi.cs` 使用。任務範圍不含這兩個檔案與建構子，所以只改 `DuelUi.cs`，將初始化移到 `OnVisibleChanged`，並加 `duelUiReady` 防止重複觸發。決鬥邏輯與封包未變動。
- 測試提交在 `engine/_integration/dev/duel-tests/DuelChecks.cs`，由正式 `demo-tests` 的 `duel` 與 CI 既有 `combat-admission` 群組執行。

## 怎麼驗證（已驗證）

| 驗證 | 結果 |
|---|---|
| `rebuild.py --update-lock`、`rebuild.py --build` | 產品、audio、launcher 成功；194 個來源、203 個 lock 項目 |
| 正式 `demo-tests`：`duel` | 204 項通過 |
| 正式 `demo-tests`：`combat-admission` | 1223 項通過，包含決鬥測試、main 的 `AttackSkillChecks` 與受擊抑制測試 |
| 正式 `demo-tests`：`map-continuity` | 21258 項通過，驗證合入 main 後的接收鏈 |
| 正式 `demo-tests`：`polymorph-cadence`／`buddies`／`item-transfer`／`ambience` | 123／259／163／446 項通過 |
| `engine/tools/check-no-game-assets.py` | pass，0 errors，追蹤 3349 個檔案 |

上述測試經 `WorldSession.Receive` 正式入口驅動本次建出的產品 DLL；沒有使用前次 scratch 的 `ReceiveDuel` reflection 掛接替代品。覆蓋每個 C／S 格式、CP950 名稱、接受／拒絕、權威開始／結束、錯誤 ID、截斷／非零填充／多餘區塊、session／epoch／revision 失效、送出失敗、交易與 buff 子型分派、S71 原訊息，以及決鬥標記不得跳過既有攻擊距離准入。

本機重現（Python 可使用安裝路徑，dotnet 測試暫存／快取皆指向 worktree）：

```powershell
python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock
python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/issue-272/NuGet.Config --output scratch/issue-272/build
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile scratch/issue-272/NuGet.Config
dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false
# <群組> 依序為 duel、combat-admission、map-continuity、polymorph-cadence、buddies、item-transfer、ambience
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-build -- . scratch/issue-272/<群組>-checks.json <群組>
python -B -X utf8 engine/tools/check-no-game-assets.py
```

本機 NuGet.Config 僅在 scratch，唯讀引用原專案 `engine/vendor`；未複製遊戲資源。產品保留既有 `MagicUi.cs:20` 的 CS8600／CS8602 警告；launcher 與測試專案沒有新增編譯警告。產品 DLL SHA-256：`02d888c390505ad4f596b5b493b6c4e4e2a1ec551575fdbe3cbe00a281e356cf`。

本輪同步提交的 CI 結果於推送後核對並回報至 [PR #293](https://github.com/chaoren0418-arch/tw-bin-engine/pull/293)，前輪綠燈不代替本輪驗證。

## 未驗證什麼

未啟動遊戲、伺服器或 DB，未做登入、兩角色互動、畫面／音效與實網驗收。合成封包測試只證明離線接收、狀態與送出契約；不宣稱原生 smoke 通過。

`DuelUi.cs` 的初始化時機改動**沒有自動測試覆蓋**：`DuelChecks` 不建立 `ClientForm`，上述測試只證明可編譯與封包邏輯未壞，「決鬥」分頁是否實際出現要到真機確認。

## 產出路徑

- 程式：`engine/source/Duel.cs`、`DuelUi.cs`、`WorldSession.cs`、`ObjectRendering.cs`。
- 提交的測試：`engine/_integration/dev/duel-tests/DuelChecks.cs`；入口：`engine/_integration/dev/demo-tests/`。
- 契約與來源 SHA-256：`docs/protocol/duel.md`；dev lock：`engine/_integration/dev/rebuild-lock.json`；本報告：`docs/reports/issue-272.md`。
- 本機證據（不提交）：`scratch/issue-272/build/`、`duel-checks.json`、`combat-checks.json`、`map-checks.json`。

## 給下一棒的提示

1. 調度器核對 PR #293 最新 head 的 CI 與可合併狀態：本輪同步 main、解決測試入口與 lock 衝突，並因編譯失敗改了 `DuelUi.cs` 的初始化掛點。
2. dot 於集中 smoke 依既有授權驗證「決鬥」分頁有出現，以及兩角色邀請、接受、拒絕與決鬥結束標記；本任務沒有執行真機。
   `ClientForm` 的 `OnLoad`、`OnShown`、`OnVisibleChanged` 現已各被一個模組佔用；之後新增 UI 模組請改用建構子內的 `Shown+=`，或把這些 override 收斂成共用掛點（需另開任務）。
3. 協議沒有邀請取消指令；世界重建時本機保守清除標記，沒有新的 S250/5 就不自行恢復。不修改伺服器攻擊判定。
