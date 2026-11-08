# Issue #531 — T224：M7-X 死亡統計窗與逾時離線（code）

依據：`docs/reference/world-objects.md` 第 6 節（WO§6）、`gap-matrix.md` 第 4.1 節 `UserPlayStatsUI` 與第 7 節 M7-X。

## 做了什麼
- `engine/source/Recovery.cs`（本組唯一可改的既有檔）
  - `DeathTimeoutRules`：逾時 **501,264 ms**；地圖 1017–1023 為 **1,801,234 ms**；地圖 2600 視窗延後 **5000 ms**（WO§6 已驗證值）。逾時封包是 **C122**，無欄位，補到 8 byte。C122 是 3.81 伺服器的 `QUITGAME`／`C_Disconnect`，對應 TW16 的 `C_QUIT`(68)，見 gap-matrix 第 2 節與 SP C122。
  - `DeathWatch`：純狀態機，時間由呼叫端傳入。
    - 同一次死亡只記一個起點。
    - 取消只關窗，計時繼續跑。
    - 可以重開視窗。
    - 離線封包只送一次。
    - 角色恢復（不再死亡）時清除。
  - `WorldSession`：
    - 用 M7-A 擴充點 `[WorldReceive(158,Observe=true)]` 在 S158 自身 action 8 當下記下死亡時間，不受 UI 輪詢延遲影響。漏接時以第一次輪詢看到 `OwnDead` 為起點。
    - S85 復活或 `Reset()` 時清除計時。
    - `[WorldSnapshot("death")]` 發布 `extensions.death`（phase／map／diedAt／windowAt／quitAt／remainingMs／windowOpen／dismissed／quitSent）。
  - `LoginConnection.CheckDeathTimeout()`：到期且仍死亡時，經既有送出閘門送一次 C122。閘門會再檢查一次，送出前若已復活就不送。送出後關閉連線，狀態改為 `Disconnected`。「重新開始」（C177）送出時照原版關閉視窗（`sub_C0BB20`）。
  - `UserPlayStatsLayout`：執行期從原客戶端 Tile.pak 讀 `UserPlayUI.xml`，解析以下內容：
    - 根節點必須是 `UserPlayStatsUI`。
    - `Restart_Button`／`Quit_Button`／`Cancel_Button`／`CloseWindow_Button` 的座標與 BaseSurf（`"5322;1"` 只取第一個數字）。
    - `UserPlayStats_List` 各列的字串 id、寬度與 FontColor。
    - 缺節點或屬性一律丟 `UiLayoutException`，不補預設座標。
  - `ClientForm`：
    - 死亡面板改照上面的版面：底圖 surf、三顆按鈕的 BaseSurf 與座標、關閉鈕、四列統計。
    - 每 250 ms 檢查一次逾時。
    - 讀不到客戶端資源時，退回引擎面板（重新開始／結束／取消），並在面板上標明。
- `engine/source/RecoveryTests.cs`（新檔）：`[WorldModuleTest("recovery-death")]`，72 項。由 `--module-tests` 自動執行，CI 也會跑。
- `engine/_integration/dev/rebuild-lock.json`：以 `--update-lock` 重產。

## 怎麼驗證（本機 Windows，worktree）
- `rebuild.py --update-lock` → `--build`：PASS。productDll SHA-256 `deeaa1dea43c91acacbff008fa4922b05c47b6cf898b378048bccf4c730f0c4f`。NuGet 用 `scratch/issue-531/NuGet.Config`，指向原專案的 vendor（唯讀）。
- 同步 origin/main（落後 39 個 commit，只有 `rebuild-lock.json` 衝突，取 main 版後以 `--update-lock` 重產）後重驗：`--build` PASS，productDll `cf80f8748669338934909da9d1d07dec4e45acfb307296dcc4a02fc8031f05d5`；`--module-tests` 結束碼 0，4 組 217 項全過（agit 44、castle 62、recovery-death 72、world-extensions 39）；`check-no-game-assets.py` pass。
- `P11.ClientEngine.exe --module-tests`（同步前）：結束碼 0；`recovery-death` 72 項、`world-extensions` 39 項全過。涵蓋：
  - 逾時規則的邊界（1016／1017／1023／1024、2600）與 C122 位元組。
  - 死亡起點取 S158 封包時刻，不取之後的輪詢時刻；他人死亡不會啟動計時。
  - 提早 1 ms 不送、到期才送；地圖 1019 套用 30 分鐘規則。
  - 取消後計時繼續；可以重開視窗。
  - 重新開始照送 C177，並關閉視窗。
  - 閘門拒絕時不送、仍保持連線。
  - 到期只送一次 C122，送出後狀態為 `Disconnected`。
  - 到期前復活不送；復活與送出同時發生時，閘門重檢會擋下。
  - `Reset` 會清除計時。
  - 快照 hook 與 S158 觀察者都有註冊。
  - 合成 XML 的版面解析，以及 4 種錯誤版面會被拒絕。
- 既有回歸（DemoChecks）全部 exit 0、status=pass：`combat-admission`、`resurrection`（含 `DeathAndRecovery`／`RecoveryRaces`）、`map-continuity`、`item-status`、`native-ui-resources`、`afflictions`、`pickup`、`doors`、`polymorph-cadence`、`buddies`、`item-transfer`。
- `check-no-game-assets.py`：pass，0 錯誤。測試 XML 是程式內合成的，數值刻意與原檔不同，沒有提交任何 `.xml`／`.img`。
- 版面欄位名稱：已人工對照本機先前解出的 `UserPlayUI.xml`（`scratch/task448-uixml/`，未入 git），節點與屬性名稱一致。

## 已驗證／推測／待驗證
- **已驗證**（WO§6）：逾時值、地圖例外、2600 延後 5 秒、到期仍死亡才送離線並關窗、重新開始關窗並送無欄位封包、S85 關窗、版面元件與座標（由 XML 執行期讀取）。
- **推測**（已實作，需要時再改）：
  - 「結束」（`MouseEvent="Quit"`）原版處理函式沒查。目前走既有的 HUD 登出路徑：關閉連線，不送封包。
  - 視窗在畫面上的位置：XML 的 X=0、Y=0 是視窗內座標，目前水平置中、y=160，沿用舊面板。
  - 統計列的水平排列規則（圖示 → 名稱 → 數值，間距取 `Gap`）。
  - 「死亡選單」重開鈕是引擎過渡：原版以 `Toggle` 重開視窗，觸發方式未查。
- **待驗證／未做**（依規則只做資料，不做畫面）：
  - 統計數值（經驗％、擊殺數、金幣、取得物品）的資料來源沒查。目前顯示「—」，不顯示假的 0。
  - 字串 3123–3126 的原文沒從 Text.pak 解出。目前用 WO§6 的中文名稱代替（經驗／擊殺數／金幣／取得物品）。
  - 地監時間清單、`Reset_Button`、`NewItemList_Button`、按鈕的 Highlight／Push 圖都沒做（按鈕狀態圖依 V4，V4 完成前只用 BaseSurf）。
  - 外框 surf 5323 有解析，但沒畫。
  - `sub_EFE730`（全域 id 5153 時不顯示）語義不明，沒做。
  - 活著時的「重新開始／結束」選單（C_RESTART 194、15 秒戰鬥限制、訊息 130）不在本組範圍。
  - 原生 UI 畫布（C2、`IUiPanel`）還沒合併，所以本面板仍是 WinForms 控制項加原版 surf 點陣圖，還不是 IUiPanel 模組。

## 沒驗什麼
- 沒啟動遊戲、伺服器或 DB，沒做真機驗收。沒有以真實 `TW_CLIENT_ROOT` 渲染面板截圖；真實 XML 只人工對照過欄位名稱。
- 伺服器收到 C122 後的實際行為（立即斷線與否）沒實測。引擎送出後會自己關閉連線。
- 遠端 CI 結果以 PR 頁面為準。Linux job、WebView2、sound／chat 等群組沒在本機跑，本次也沒碰那些模組。

## 產出路徑
- `engine/source/Recovery.cs`、`engine/source/RecoveryTests.cs`、`engine/_integration/dev/rebuild-lock.json`、本報告。
- 分支 `task/531`。

## 給下一棒的提示
- C2（`IUiPanel`）合併後，可以把 `UserPlayStatsLayout` 直接接進 `IUiPanel.Load`。`LoginConnection.DeathSnapshot()／DismissDeathWindow()／ReopenDeathWindow()／RestartAfterDeath()` 就是 `IUiCommands` 需要的動作。逾時檢查 `CheckDeathTimeout()` 不依賴 UI，也可以改由連線層的計時器呼叫。
- 統計數值要先查 `CUserPlayStatsUI`（`sub_C0AB50`）的更新來源，再接 `IUiGameState`；伺服器沒送的值請維持空白。
- 字串 3123–3126、3131 要從 Text.pak 解出後，替換 `DeathStatNames`。
- 單元測試要用 `DeathSnapshot()`，不要用完整的 `Snapshot()`：有 Actor 時，`Snapshot()` 會讀外觀素材（`engine/assets/integrity.json`），在沒有素材的環境會失敗。
