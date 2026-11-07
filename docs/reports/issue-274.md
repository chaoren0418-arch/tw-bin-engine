# Issue #274／T134：武器修理與 PK 查詢

分支：`task/274`。工作檔只在指定 worktree 寫入，主專案僅讀取現有 NuGet 套件；Git 同步及提交另會更新 worktree 共用的 Git 中繼資料。沒有啟動遊戲、伺服器或 DB，沒有複製遊戲資源、伺服器原始碼或改封存。

## 做了什麼

- 實作 C118 查詢、S83 D200 清單、C20 單件修理、C51 PK 查詢與 S71／562 回應顯示。修理窗顯示持有物品名稱、損壞度及每點 200 金幣的預估費用。
- `NpcUi.cs` 只增加一次修理服務初始化；`WorldSession.cs` 只增加一個受 D200 條件限制的 S83 case。D70 寵物清單不由本模組消耗；此基底尚未包含 T127。
- 防止重複提交，送出前再驗證世代、目標、距離及庫存；換圖、離線、換目標、死亡、背包變動與 30 秒逾時使舊清單失效。傳送錯誤斷線，不重送。
- T121 `ItemStatus` 尚未在基底合併，依任務規定只更新清單 UI。S464／189 回報後清單失效，再明確查詢；不預扣金幣、不自行把損壞度改為 0。原有 S24／S242 照常更新庫存。
- 離線測試提交於 `engine/_integration/dev/weapon-repair-tests/`，已接上既有 `.github/workflows/build.yml` Windows 測試工作。dev lock 由 `--update-lock` 產生。

## 怎麼驗證

- 已驗證：SDK 10.0.300，`rebuild.py --update-lock` 後執行 `rebuild.py --build --configfile scratch/issue-274/NuGet.Config --output scratch/issue-274/build`；產品與 launcher 編譯成功。產品有既有 `MagicUi.cs:20` 的 CS8600／CS8602 警告，本模組無新增警告。
- 離線測試直接載入產品 DLL，使用合成封包、記憶體 sender 與拒絕 socket 的 guard；驗證每個 C／S 格式、空／單／多列、損壞度邊界、重複／零 ID、截斷／過長／非零 padding、寵物與未知子型別隔離、庫存權威更新、重複提交、逾時、傳送失敗與排隊期間失效。
- 本機 NuGet 網路還原遇到沙箱 TLS 認證錯誤，改用 scratch 下的 NuGet 設定唯讀引用主專案 `engine/vendor`。套件展開、暫存、編譯輸出均留在 worktree。CI 仍用既有 nuget.org 設定。
- 已驗證：98 項本模組離線檢查全部通過；`check-no-game-assets.py` 回報 pass、0 errors；`git diff --check` 通過。
- 前次 T105 同步（`43ab7202e49d1f8998673b4a49f836125cec4dc1`）：保留主線音效接線、重新產生 dev lock（177 來源／186 鎖定檔），重建成功並重跑 98 項測試全數通過。這是前次結果，不代表本次退回要求已完成。
- CI：各次 head 的結果分開記錄於 [PR #297](https://github.com/chaoren0418-arch/tw-bin-engine/pull/297)，本次推送後另補精確 head、無衝突狀態與 CI 執行連結，不沿用前次綠燈。

## 第 2 次嘗試：主線同步補交（2026-11-07）

- 本次僅執行審查要求的同步，從既有 `43ab720` 合併 `origin/main` 的 `ccaa3e02caf1c0cc2c0696683e243c1c7577df4e`，不擴充功能。`WeaponRepair.cs`、`WeaponRepairUi.cs` 與 98 項測試內容均未修改。
- `WorldSession.cs` 衝突保留 main 的公告板 S68／S148 與原有音效接線，再保留本模組唯一的 S83／D200 分支；CI 衝突保留 main 的物品轉移測試與本模組測試。
- `docs/REPORT.md` 完全採 main，差異為零；dev lock 衝突以 `rebuild.py --update-lock` 重新產生，181 個來源／190 個鎖定檔全部核驗通過，沒有手工拼接 hash。
- 同步後重新執行既有 canonical build：產品與 launcher 成功、0 錯誤；產品仍只有既有 `MagicUi.cs:20` 的 CS8600／CS8602，launcher 0 警告。產品 DLL SHA-256：`a9f7cebad8292f825ff1ce16987bc2b2c8a47aafa464dacb6ed9bd6ab2c65687`。
- 同步後重新執行 `dotnet run --project engine/_integration/dev/weapon-repair-tests/WeaponRepairChecks.csproj -c Release --no-restore`：98 項全數通過；`check-no-game-assets.py`：pass、3296 個追蹤檔、0 errors。既有證據路徑已更新為這次結果。
- `git -c core.whitespace=cr-at-eol diff --cached --check` 通過（main 帶入的 C# 檔使用 CRLF，依原始位元組保留）；未解決衝突清單及 `git ls-files -ci --exclude-standard` 均為空。相對此次 main 仍只涉及原先核准的 10 個檔案。

## 審查後最新主線同步（2026-11-07）

- 前次審查的 head `b430d55ed2ed19c2db90b2f87e82b90e55e01e59` 已通過，但 PR #297 隨後呈現 `CONFLICTING`。本次從該 head 合併最新 `origin/main`：`5fece0032aef4ed8b772671f4a425c0f1d4044e8`（含 T109／#197），不重做已審查功能。
- 唯一衝突是 dev lock，執行 `rebuild.py --update-lock` 重新產生後，182 個來源／191 個鎖定檔核驗通過。`WeaponRepair.cs`、`WeaponRepairUi.cs`、本模組測試及兩個共用接線相對前次 head 均無差異；`docs/REPORT.md` 完整沿用 main，未加入本任務索引。
- 重新執行上述 canonical build 及本模組離線測試：產品／launcher 編譯成功，98 項檢查全部通過。產品仍為既有 CS8600／CS8602 兩個警告，launcher 0 警告；產品 DLL SHA-256：`49b6a7394ae7ad111e10c31ce46ad745e851befe4a0936ac2c2ad35dc3ba67ba`。既有 `scratch/issue-274/build/`、`tests.txt` 已更新為本次證據。
- 本次 `check-no-game-assets.py`：pass、3297 個追蹤檔、0 errors；`git diff --check` 通過，`git ls-files -ci --exclude-standard` 為空。相對最新 main 仍只有原核准的 10 個檔案。
- 推送後的新 head 與 CI 結果另記於 PR #297 的本次驗證留言；先前 head 的成功紀錄不當作本次 CI 證據。調度器合併前仍須核對即時 main 與可合併狀態。

## CI 退回同步修正（2026-11-07）

- 依本輪「只做同步」要求，先 `git fetch origin`，將本機分支快轉到遠端 `ce4493adfc587803127f4ab33cc396fb42214fba`，再執行 `git merge origin/main`（已包含 `0380ae6062804b976fe32e5ac2a20b0b611e42ac`，回報 Already up to date）。
- 已驗證 CI 失敗原因：[build 42325417265](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42325417265) 的 canonical build 回報 `sourceCount 182 != pinned source files 184`。執行既有 `rebuild.py --update-lock` 後只改來源數 182 → 184，193 個鎖定檔全部通過；未改產品程式、測試、CI 或封包文件，`docs/REPORT.md` 與 main 相同。
- 以 Python 3.12 明確路徑執行既有 canonical build，參數為 `--build --configfile scratch/issue-274/NuGet.Config --output scratch/issue-274/build`；唯讀使用主專案 vendor，套件及暫存均留在 worktree。產品與 launcher 成功，產品仍只有既有 `MagicUi.cs:20` 的 CS8600／CS8602；launcher 0 警告，兩者 0 錯誤。
- 重跑 `dotnet run --project engine/_integration/dev/weapon-repair-tests/WeaponRepairChecks.csproj -c Release --no-restore`：98 項全部通過。`check-no-game-assets.py`：pass、3301 個追蹤檔、0 errors；`git diff --check` 通過，未解決衝突及已追蹤但遭忽略的檔案清單均為空。
- 本輪產品 DLL SHA-256：`403988cc2b2008b80ecc922aced2e61b195ccd226bc840c4ccd339b49dddc214`。本機證據沿用 `scratch/issue-274/build/`、`scratch/issue-274/tests.txt`；推送後精確 head 與新 CI 結果另留於 PR #297，不沿用舊 head 的結果。
- 下一棒：Claude／調度器核對本輪 head 的 CI 與可合併狀態，依既定流程交由使用者合併；原生修理與 PK 驗收仍由 dot 的 M6 smoke 接手。

## 未驗證什麼

- 未驗證原生視窗排版、實際 NPC 修理扣款／耐久變更、PK 真實次數與正式客戶端操作；合成封包測試不代表實機驗收。
- S83／S71 沒有 request ID；跨逾時重查的舊回應無法精確關聯，限制與處理方式見封包文件。

## 產出路徑

- `engine/source/WeaponRepair.cs`、`engine/source/WeaponRepairUi.cs`
- `engine/source/WorldSession.cs`、`engine/source/NpcUi.cs` 最小接線
- `engine/_integration/dev/weapon-repair-tests/`、`.github/workflows/build.yml`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/weapon-repair.md`、本報告
- 本機可重建證據：`scratch/issue-274/build/`、`scratch/issue-274/tests.txt`（不提交）

## 給下一棒的提示

Claude 審查並由使用者合併；調度器處理多任務 dev lock 合併時重新產生，不手工拼 hash。T127 合併時保留修理 D200 與寵物 D70 各自分支；T121 合併後可接其狀態介面，不把訊息當成耐久權威。dot 在授權的 P24Lab 做 M6 smoke：選取 3 格內修理 NPC →「修理」→ 查詢 → 選一件損壞武器 → 提交一次 → 核對金額與訊息 → 更新清單；另測金幣不足、換目標失效、零 PK 與非零 PK。不要碰正式 2000／3306。
