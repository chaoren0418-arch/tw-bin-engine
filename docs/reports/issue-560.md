# Issue #560 報告：T238 血盟倉庫紀錄（W2-G）

## 做了什麼
- 新檔 `engine/source/ClanWarehouseHistory.cs`：
  - 用 M7-A 擴充點 `[WorldReceive(250, Subtype=117)]` 接 S250 子碼 117。沒有改 `WorldSession.cs`。
  - `ClanWarehouseHistoryPackets.Parse` 解析 `C(250) C(117) D(n) {S 角色名, C type, S 物品名, D 數量, D 距今分鐘}×n`，並檢查 8 byte 對齊。
    - 欄位依 `docs/reference/windows-2.md` 第 7 節。該節記錄原版 `scsdd`，與 3.81 `S_PledgeWarehouseHistory` 完全一致。
    - 筆數超過封包剩餘長度能放下的上限時，直接丟 `ProtocolException`，不會先配置大陣列。
  - 收到非空清單：存起來，並觸發 `ClanWarehouseHistoryReceived` 事件，讓之後的原生面板開窗。
  - 收到 0 筆：不觸發開窗事件，改顯示訊息 1889。
    - 文字用 `ServerMessageText.Decode(1889)`。T228 字串表還沒合併，目前顯示編號占位「原服訊息 1889」。
    - 顯示走畫面中央提示，和 S_RedMessage 同一個通道。不改 `LastServerMessage`，以免打斷進行中的血盟或交易流程。
  - 快照 `extensions.clanWarehouseHistory` 包含 packets、records、notice、ui 四項；另外註冊了 `[WorldReset]` 清除。
- 新檔 `engine/source/ClanWarehouseHistoryTests.cs`：`[WorldModuleTest("clan-warehouse-history")]`，由 `--module-tests` 執行，共 47 項檢查：
  - 註冊項目。
  - 兩筆清單：含 CP950 中文、`$` 名稱代碼、有號數量與分鐘。
  - 0 筆時出現 1889，且不動 `LastServerMessage`。
  - 收到非空清單後清除 1889 訊息。
  - 格式錯誤：筆數過大、padding 非零、多一塊、子碼錯。
  - 其他 S250 子碼不會被這個處理器吃掉。
  - Reset。
- `engine/_integration/dev/rebuild-lock.json`：以 `--update-lock` 重產。

## 怎麼驗證（已驗證）
- `rebuild.py --update-lock` → `dev-inputs-verified`，共 241 個來源檔。
- `rebuild.py --build --configfile .github/nuget.ci.config` → `PASS dev`。worktree 沒有離線 `vendor`，所以改用 CI 的 NuGet 設定。
- `P11.ClientEngine.exe --module-tests` → `pass`，5 組、264 項，失敗 0；本組 47 項全過。輸出在 `scratch/issue-560/module-tests.json`，不進 git。
- `check-no-game-assets.py` → `pass`。
- **同步 origin/main 後重驗（2026-11-08）**：merge origin/main（含 #446／#463／#510／#519／#548／#551），`rebuild-lock.json` 取 main 版後以 `--update-lock` 重產（261 個來源檔）；`--build` → `PASS dev`（product DLL SHA-256 `8031a02f…71a3`）；`--module-tests` → `pass`，8 組、432 項，失敗 0，本組 `clan-warehouse-history` 47 項全過；`check-no-game-assets.py` → `pass`（3791 檔，0 錯誤）。S250 子碼 117 與 main 新增的 `PacketBoxStatus.cs` 註冊的子碼沒有重疊。
- **第二次同步 origin/main 後重驗（2026-11-09）**：merge origin/main（含 #514／#515／#516／#520），`rebuild-lock.json` 取 main 版後以 `--update-lock` 重產（267 個來源檔）；`--build` → `PASS dev`（product DLL SHA-256 `910c8fce…f96f`）；`--module-tests` → `pass`，10 組、548 項，失敗 0，本組 `clan-warehouse-history` 47 項全過；`check-no-game-assets.py` → `pass`（3803 檔，0 錯誤）。S250 子碼 117 仍只由本檔註冊。

## 未驗證／推定
- **type 欄位的意思**：0／1 是存入還是領出沒有查證，目前保留原值，由畫面任務查證後再定文字。
- **物品名**：可能是 `$編號` 名稱代碼，目前保留原文，交給畫面層解析。
- **1889 的顯示通道**：原版用中央提示還是聊天系統訊息屬於推定。實際文字等 T228。
- **視窗**：`sub_A559D0`、surf 9183 這個視窗還沒做，要等原生 UI（T190）。
- **真機**：沒有啟動遊戲或伺服器；CI 結果以 PR 為準。

## 產出路徑
- `engine/source/ClanWarehouseHistory.cs`
- `engine/source/ClanWarehouseHistoryTests.cs`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/reports/issue-560.md`

## 給下一棒的提示
- 畫面任務訂閱 `WorldSession.ClanWarehouseHistoryReceived` 開窗，資料從 `WorldSession.ClanWarehouseHistory.Records` 取。版面依 `sub_A559D0`。
- T228 合併後，在字串表加上 1889，`DisplayNoticeText.Format` 或 `ServerMessageText.Decode` 就會帶出實際文字。測試第 4 段的占位字串要一起改。
- 本機 worktree 建置需加 `--configfile .github/nuget.ci.config`，因為 worktree 沒有 `engine/vendor`。
