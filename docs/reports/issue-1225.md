# #1225：#1210 實機驗收失敗項目分流

結論：#1210（f1186-smoke-tooling，測試工具補強）結果 ✅9／❌0／⚠️2。兩個 ⚠️ 一個是**測試條件**（R06：兩角色不相鄰），一個是**工具判定過嚴**（R07：只要其他程式有 active 播放 session 就拒錄）。開一張 resource（`f1210-adjacent-pc-fixture`）、一張 code（`f1210-loopback-silent-session`），再加一張只重測 R06、R07 的 test（`f1210-tooling-retest`）。SM22、SM35、SM51 直接依賴這兩項工具，從 #1211 移到這張重測。

## 讀了什麼
- #1210 最新 `=== RESULT ===`（調度器留言，failed，`codex-local`）與 `docs/reports/issue-1210.md`（`task/1210` `22e33ad4`，PR #1224 **尚未合併**）。
- `task/1210` 的 `work/f1118-uia-path/SmokeLoopback.cs`：`CheckSessions` 碰到其他 PID 的 Active session（或跨程序 session）就丟例外。
- `docs/reports/issue-1201.md`、`docs/reports/issue-1220.md`／`docs/plans/issue-1220.json`（前例）、#1186 報告的 SM22／SM35／SM51 列（`task/1186`）、`docs/testing/smoke-m7.md` 的 SM22／SM35／SM51、#1209 的 `work/f1186-smoke/fixtures.json`（`task/1209`，PR #1219 未合併；裡面沒有相鄰角色的 fixture）。
- 所有 Issue 內文中提到 `1210`／`f1186` 的：#1211、#1222、#1223、#1216、#1215。

## 已通過（✅，之後不重測）
R01 建置＋8 項契約、R02 聊天草稿清空、R03 指令 C 封包送出確認、R04 未送出辨識、R05 NPC 點選＋targetId 讀回、R08 共享讀檔、R09 兩個客戶端正常關閉、R10 收尾、R11 CI。

**附帶回歸**：只有 R01 的契約檢查部分（`Test-SmokeTools.ps1`）。原因：`f1210-loopback-silent-session` 會改 `SmokeLoopback.cs` 與 `Test-SmokeTools.ps1`，而 CI 不編譯這段 PowerShell `Add-Type` interop，只能在實機跑契約檢查才知道能不能編譯。R02–R05、R08、R09 用到的聊天、點選、讀檔、關窗程式兩張修正都不碰，不回歸。

## 失敗項目對照
| #1210 項目 | 結果 | 分類 | 處理 |
|---|---|---|---|
| R06 指定 PC 點選：法師 (32648,32948)、王子 (32580,32974) 不相鄰，王子步行到 (32591,32970) 被牆擋住，沒有 PC 目標可點 | ⚠️ | (b) 測試環境／資料 | `f1210-adjacent-pc-fixture` → `f1210-tooling-retest` R06 |
| R07 loopback 波形：Chrome PID 8928 有 active render session，工具在建立 WAV 前就拒錄（Frames=0） | ⚠️ | (a) 工具缺陷（判定過嚴）；環境部分列已知限制 | `f1210-loopback-silent-session` → `f1210-tooling-retest` R07 |

### R06 為什麼是資源任務
- 工具本身已在 R05 用 NPC 證明「投影換算座標 → 點選 → targetId 讀回」可行；R06 缺的只是一個能點的相鄰 PC。
- #1210 是在 worktree 裡的**副本 DB**（`scratch/issue-1210/lab/db-data`）跑的，角色移動沒有留下來；#1186 時兩角色曾在 map0 相鄰。所以要在**持久 DB**（`environments/p24/db-data`）把位置準備好並保存，給重測直接用。
- 方法：先用一般玩家步行；15 分鐘內不成立才依 #160 暫設 GM 瞬移、立刻改回 GM0。直接 UPDATE 座標不在 #175 授權清單內，明文禁止。產出 `work/f1186-smoke/adjacent-pcs.json`（另開檔，避免跟還沒合併的 PR #1219 的 `fixtures.json` 衝突）。

### R07 為什麼是程式任務
- WASAPI session 的 Active 只表示「該程序開著輸出串流」，不代表有聲音混進 endpoint；瀏覽器分頁常保持靜音的 active 串流。執行器依規定不能關他人程式、不能改系統設定，照現在的判定，只要使用者開著瀏覽器，R07 與 SM51 就永遠無法完成——重跑不會有不同結果。
- 修正：每個 session 取 `IAudioMeterInformation` 的 peak。他人 active 但全程 peak ≤ 門檻就繼續錄並記錄；有聲就停、不留 WAV；取不到 meter 照舊拒錄。判定抽成純函式並加離線契約；保留 `-RequireExclusive` 的舊嚴格模式。
- 只改 `work/f1118-uia-path/`，不改產品程式。claude-local 不得執行該工具，契約與實錄交重測的 R01／R07。

## 已知限制（不開任務）
- **程序隔離 loopback 不可用**：測試機是 Windows 10 build 19045；`AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK` 需要 build 20348 以上。工具只能錄 endpoint 混音，所以「他人程式真的在出聲」時只能等安靜時段，重測遇到時記 ⚠️（環境）跳過，不關他人程式。#1210 報告也寫明了 20 ms 輪詢之間無法保證沒有其他聲音，這點照實保留在 JSON 的 `Scope`。

## 其他排隊任務的處理
- **#1211（f1186-retest）**：範圍遠大於本張（SM03–SM58），**不關閉**。已用 `gh issue edit` 改內文（修改前後存於 `scratch/1211-before.md`、`scratch/1211-after.md`，不進 git）：
  - `depends_on` 從 `[1205, 1206, 1207, 1208, 1209, 1210, 1225]` 改成 `[1205, 1206, 1207, 1208, 1209, 1210]`（拿掉 1225；#1210 已結案，保留無妨）。
  - B 組刪除 SM22、SM35、SM51，加一段「分流 #1225 調整」說明這三列改由 `f1210-tooling-retest` 測；SM31、SM34、SM38、SM53 的指令送出只需要 #1210 已通過的 R02–R04，留在 #1211。
  - 理由：新任務的 Issue 編號要等本張合併後才產生，不能寫進 #1211 的 `depends_on`；與其讓整個大重測等兩項工具，不如把真正依賴它們的三列移出（同 #1220 前例）。#1211 的前置「上列 6 張依賴都已合進 main」仍要求 PR #1224 已合併。
  - 已讀回確認：`depends_on` 正確、B 組不再含 SM22／SM35／SM51。
- **#1222／#1223**（公告板／拍賣板）：不依賴 #1210；#1223 等 #1211 結案，不受影響，不改。
- **#1215／#1216**（map4 fixture／重測）：與 #1210 無關，不改。`f1210-tooling-retest` 的 SM51「map4 港口 6016」子項寫明要 #1216 M01 ✅ 才做，否則記「受 #1216 阻塞」。

## 產出
- `docs/plans/issue-1225.json`（3 項）：
  - `f1210-adjacent-pc-fixture`：resource、codex-local、runner_lock、real_machine，P1。
  - `f1210-loopback-silent-session`：code、claude-local，P1。
  - `f1210-tooling-retest`：test、codex-local、runner_lock、real_machine、milestone_smoke，P1，`depends_on` 上面兩張；只重測 R06、R07，回歸 R01 契約；通過後補 SM22、SM35、SM51。
- 本報告。

## 怎麼驗證／未驗證
- 已驗證（只讀）：#1210 RESULT 與報告原文；`SmokeLoopback.cs` 的拒錄條件（`CheckSessions` 第 38–44 行）；PR #1224、#1219 狀態都是 OPEN；#1209 fixtures.json 沒有相鄰角色；#1211 修改後內文讀回正確；計畫 JSON 可被 `json.load` 解析（3 項）。
- 推測：Chrome 那個 session 當時是靜音的 active 串流（#1210 沒記它的 peak，`f1210-tooling-retest` 的 `otherSessions` 會留下證據）。如果它其實在出聲，R07 仍會是 ⚠️（環境），由已知限制處理。
- 待驗證：相鄰角色能否步行到位（resource）；meter 判定是否可編譯並正確分類（retest R01／R07）；SM22、SM35、SM51 的遊戲行為。
- 本輪未執行任何程式、未碰 DB、未啟動伺服器或客戶端。

## 給下一棒的提示
- 調度器：PR #1224 要先合進 main，`f1210-loopback-silent-session` 才有基礎（內文有未合併時的備案）；#1211 也需要它。
- `f1210-tooling-retest` 會用持久 DB 的相鄰位置；若 #1211 先跑而移動了這兩個角色，重測內文已要求先讀回座標、依 `adjacent-pcs.json` 的步行方法歸位。
- SM35 的邀請選對象模式可能不更新 `targetId`（#1210 提示 3），重測已寫明改看 C 封包與 B 畫面。
- #1210 worktree 的 `scratch/issue-1210` 裡有冷備份、DB 副本與 junction，**不要遞迴刪除**；新任務會唯讀引用其證據路徑，清理前請先確認三張任務都已讀完。
