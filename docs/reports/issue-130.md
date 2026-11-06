# Issue #130／T073 執行報告

## 做了什麼

- 在 `scratch/wt/task-130`、分支 `task/130`，以 `origin/main` 的 `1cb4d024` 建立獨立工作樹，並同步已合併 #129 的 `e2e1a927`（只需重生 dev lock 解衝突）；未修改主工作樹、`docs/REPORT.md` 或封存 lock。
- 唯讀查閱主伺服器移動、AOI、NPC handlers；結論與 14 個來源 SHA-256 見 [world.md](../protocol/world.md)。一般成功步行不回送自身 S10，所以保留真實回包座標，新增清楚標示的目前預測位置，HUD／F9 同步呈現。
- S87 完整物件更新不再繼承舊死亡狀態，且使舊排隊操作／對話失效；S76 待補外觀清除選取並禁止重選。S120／S206／重連沿用既有清理，新增回歸保護。
- NPC 一般對話／操作距離對齊來源的 5 格；焦點／停止路徑同時清除方向與 F1–F4 held 狀態。沿用左鍵選取、雙擊／Ctrl＋左鍵攻擊、右鍵交談。
- F9 圖示與原始陰影既有實作通過本次離線測試，沒有為此複製或新增遊戲素材。P27 的 12 組覆蓋與真機界線見 [測試說明](../../engine/_integration/dev/world-ui-tests/README.md)。

## 怎麼驗證（已驗證）

- `py -3.12 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock` 與 `--build --output scratch/build-130`：PASS，SDK 10.0.300；產品 0 錯誤、2 個既有 `MagicUi.cs:20` nullable 警告；launcher 0 錯誤／0 警告。
- `powershell -NoProfile -ExecutionPolicy Bypass -File engine/_integration/dev/world-ui-tests/run.ps1`：世界／UI **124** 項、既有 DEMO **437** 項、Web **18** 項全部 PASS，實際 WebView2 合成 PNG 的 HTTP/MIME/解碼/404/cache 亦 PASS。測試不呼叫遊戲入口或連線，使用合成封包及記憶體 sender。
- `check-no-game-assets.py` 以包含新增檔的 git index 再核驗；CI 由 PR 的當前 head build check 確認。CI 跨平台編譯與本機 Windows 離線測試分開解讀。
- 產品 DLL SHA-256：`05e3167521124fd890e241ee62218b3b86e1b6c6a1676a0000f282e607df52b4`。
- 本機證據：本 worktree 的 `scratch/build-130/`、`scratch/issue-130/`；建置檔在既定 `engine/build`、`engine/launcher`、各專案 `bin/obj`，暫存／WebView2 profile 在 `scratch`。不提交產物或合成圖片。

## 未驗證什麼

未啟動遊戲、伺服器或 DB；未執行原生登入／滑鼠／鍵盤／IME／DPI／音效驗收。伺服器目前來源與 P24Lab d287 JAR 是否逐位一致未查證；離線測試不能當作服務端接受操作或原生畫面正確的證據。

## 產出路徑與給下一棒的提示

- 協定：`docs/protocol/world.md`；短報告：本檔；可重跑測試：`engine/_integration/dev/world-ui-tests/`；另擴充 `demo-tests/web-checks.cjs`；產品修改與 dev lock 隨分支提交。
- Claude 審查後由使用者合併；T075 由 dot 一次驗點地終點、雙擊攻擊、右鍵 NPC（含 4／5 格）、AOI 離返與重連、F1–F4 切焦點後可再次使用、聊天 IME、F9 圖示、陰影與 DPI。讀座標時分清「目前預測」和「原服回報」，不能要求每一步都有自身 S10。
- T070／#133 也會動 NPC 服務；合併時保留本張 5 格與 `AppearancePending` 防護，再跑本離線腳本，不用改 DB 製造驗收條件。
- 初期沙箱初始化失敗，改用已審核的本機執行。主目錄 `checkout -B` 被自動審核拒絕後改用新 worktree；junction 前置亦先被拒絕，核實不同的絕對來源／目的路徑後已獲准。首輪測試因未建構 Form 無法存取 Capture 失敗，拆出實際共用按鍵清理路徑後重測；不是遊戲啟動失敗。
