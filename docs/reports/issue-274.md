# Issue #274／T134：武器修理與 PK 查詢

分支：`task/274`。本輪只在指定 worktree 寫檔，主專案僅讀取現有 NuGet 套件；沒有啟動遊戲、伺服器或 DB，沒有複製遊戲資源、伺服器原始碼或改封存。

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
- CI：待 PR 建立後檢查，結果補記於 PR。

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
