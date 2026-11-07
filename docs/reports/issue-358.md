# Issue #358：T141 求婚與結婚

## 做了什麼

- 在 `Marriage.cs` 實作 C50／0、d287 S219／654 邀請與 C121／654 接受／拒絕，S71／655 成功後顯示伴侶名稱。D 欄位固定為 0，不用 requestId 或 confirmation。
- 加入原生「求婚／伴侶」入口與邀請視窗；等待結果、拒絕、已婚／性別錯誤、重複操作與過期送出皆有獨立處理。
- `Trade.cs` 只增加一個 Yes-No 求婚分支，`WorldSession.cs` 只增加一個 S71 回應 case。測試接上既有 DemoChecks 及 CI 使用的 map-continuity 組，沒有新增平行的測試入口。
- 唯讀核對部署 JAR 與來源，記錄婚禮區域及「此版本無戒指檢查／消耗」的結論。未修改 `docs/REPORT.md`。

## 怎麼驗證（已驗證）

- `rebuild.py --update-lock` 後執行 `--build`：產品、audio、launcher 成功，0 錯誤；產品保留既有 `MagicUi.cs:20` 的 CS8600／CS8602 兩項警告。
- 求婚組 `marriage`：**131 項通過**。覆蓋 C50、C121 Yes／No、S219、S71 所有本模組格式、CP950、截斷與錯誤補零、非零 D 拒絕、訊息參數數量、成功／拒絕／失敗、過期 revision、切圖、重置、死亡、移除、逾時、替代 Yes-No、新邀請、連線世代與待發前面向改變。
- CI 使用的 `map-continuity` 組：**489 項通過**，包括既有地圖／公告板回歸及新增求婚檢查。執行產品 DLL，使用合成封包、記憶體傳輸及 SocketGuard；沒有開啟產品、UI、socket 或遊戲。
- `check-no-game-assets.py`：提交清單檢查通過；沒有新增遊戲資源。
- 保留原有 CRLF，`git -c core.whitespace=cr-at-eol diff --check` 通過。
- CI 接線：`.github/workflows/build.yml` 既有 Windows map-continuity job 執行新增測試；Linux dotnet job 驗證交叉建置、dev lock 與資源政策。遠端結果以本 PR checks 為準。

本機初次 NuGet HTTPS 還原遇到 Schannel 認證錯誤，改用 scratch 內的 NuGet.Config 唯讀指向原專案 `engine/vendor`。所有套件展開、暫存與建置都在本 worktree；沒有複製遊戲資源。測試編譯關閉 shared compilation，避免既有編譯服務的檔案存取限制。

## 未驗證什麼

- 沒有原生畫面、兩角色登入、婚禮區實際操作或伺服器／DB 持久化驗收；未啟停遊戲、伺服器或 DB。
- 本地伴侶名稱只代表此世界 epoch 內收到的 S71／655。切圖／切角／重連後清除，等待權威資料；不宣稱具有登入時伴侶查詢或跨地圖常駐資訊。未知名稱不代表未婚。
- 15 秒逾時與鄰近檢查是本地防護；d287 無求婚取消／requestId 協議，不能聲稱本地逾時已取消伺服器 TempID。
- 原生視窗外觀、縮放、遮擋與鍵鼠實際操作待 smoke；離線測試沒有顯示任何視窗。

## 產出路徑

- `engine/source/Marriage.cs`
- `engine/source/Trade.cs`、`engine/source/WorldSession.cs` 的最小分派掛接
- `engine/_integration/dev/marriage-tests/MarriageChecks.cs`
- `engine/_integration/dev/demo-tests/DemoChecks.csproj`、`Program.cs`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/marriage.md`、本報告
- 本地暫存證據（不提交）：`scratch/issue-358/marriage.json`、`map-continuity.json`、`build/`

## 給下一棒的提示

由 Claude 審查 `task/358` PR 與 CI，再交使用者合併。dot 後續在授權的 P24Lab 測試角色驗證雙方位於 map 4、X=33974..33976、Y=33362..33365 且相鄰面對面時的求婚／拒絕／成功、伴侶名稱、已婚與性別錯誤；不必依其他版本規則預設戒指是 handler 前置條件。若需要跨地圖／重連後伴侶常駐顯示，另行核對伺服器查詢或登入封包契約。
