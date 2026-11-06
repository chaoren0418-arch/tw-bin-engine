# Issue #140／T071：跨圖連續性

## 做了什麼

- 在既有 `task/140` worktree 修改，未切換或建立分支。先唯讀查閱主伺服器 Java 與靜態 SQL，整理 [maps.md](../protocol/maps.md) 的封包、排程、旅館入口與來源 SHA-256。
- 修正 S206 在完整解碼前退役 6016 的問題：截斷或錯誤 padding 不再破壞目前地圖特效。
- 遠距權威位置超出目前 2×2 cache 時，退役舊 cache／遮擋／效果並重新冷載入；原本只平移一次可能仍不包含角色。一般相鄰移動仍用既有 cache shift。
- 船運以目的圖 S206 加自身 S87 確認到港，接受伺服器修正落點，避免成功到港後永久鎖定；中斷航程後重新登船會清除舊 passage，恢復下一趟。
- 保留既有洞穴 C29→S149→C52→S206→S87、旅館 C34／S39／C125 `room`／`enter` 與通用地圖載入。新增獨立的 `map-continuity` 離線測試群與 Windows CI job；測試資料只在 scratch 動態建立。

## 怎麼驗證（已驗證）

- SDK `10.0.300`；`rebuild.py --update-lock` 核驗 172 個來源／181 個 pins，只更新 dev 滾動 lock。
- `rebuild.py --build --configfile scratch/issue-140/NuGet.Config --write-receipt --output engine/build/issue-140`：產品與 launcher 成功。產品為既有 `MagicUi.cs:20` 兩個 nullable 警告、0 錯誤。
- nuget.org 在此機的 Schannel 還原失敗；改由 scratch 內 NuGet 設定唯讀引用原專案 `engine/vendor` 的 WebView2 nupkg，套件解壓與全部建置輸出留在 worktree。未改專案 NuGet.Config。
- `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-140/map-checks.json map-continuity`：**120 個檢查全部通過**。測試呼叫實際產品 DLL、記憶體 sender、socket guard；不呼叫遊戲入口、不顯示視窗、不連伺服器。
- 覆蓋洞口 C29、ACK 去重、洞穴進出／同圖重建／abort 恢復、排隊舊世界輸入；船運 5→4、6→0、舊自身包、校正落點、票移除、缺票／過期時間、中斷重登船與排程邊界；旅館租房／進入／離開、無預測扣款或鑰匙；過期非同步成功／失敗、A→B→A、遠距同步／非同步冷載入、缺圖、重設；S206 截斷／padding 原子性、效果合成／離圖／回圖／區塊退役。
- `check-no-game-assets.py`：PASS，0 errors；`git -c core.whitespace=cr-at-eol diff --check`：通過。CI 結果以 PR 上同一 commit 的 checks 為準。
- 產品 DLL SHA-256：`ceacc2b919d66c35fa7ca54f11200ef0cf75194094a9f86ae3f3d5f659afc1ef`。

## 未驗證什麼

- 未啟動遊戲、伺服器或 DB，未連正式服務。未驗證登入、原素材實際畫面／音效、船票扣款、鑰匙持久化及 P24Lab 當前 NPC／資料表是否與靜態 SQL 相同。
- 合成 S32、單像素、玩家時序與 NPC 模板只證明解析／生命週期／送包行為，不冒充原始素材或實機通過。6016 真機音效仍由 T074／T075 驗收。
- 本機唯讀 map manifest 列有 0、1、4、5、6、16384 與對應範圍；這僅證明宣告覆蓋，不代表每個原始區塊都已執行解碼或畫面驗收。
- 未改 `docs/REPORT.md`、封存 lock、releases、原專案或伺服器檔案。

## 產出路徑

- `docs/protocol/maps.md`、本報告。
- `engine/source/WorldSession.cs`、`SceneOpacity.cs`、`ShipTravel.cs`、`engine/_integration/dev/rebuild-lock.json`。
- `engine/_integration/dev/demo-tests/MapContinuityChecks.cs` 與 `Program.cs` 掛接、`.github/workflows/build.yml`。
- 未追蹤證據：`scratch/issue-140/map-checks.json`；合成資料：`scratch/issue-140/synthetic-maps/`；建置 log／result：`engine/build/issue-140/`；產品：`engine/build/client-product-dev/`。

## 給下一棒的提示：T075／dot

由 Claude 審查、使用者合併後，T075 的 dot 在授權 P24Lab 測試角色統一驗收。以下座標是本次靜態 `380_db.sql` 路線；若測試服不同，記錄差異交本機調度器處理，不修改正式 DB。

1. **洞穴 R12／N04**：說話之島 map0 `(32477,32851)` 踩格進冒險洞窟1樓，預期 map1 `(32669,32802)`；由 map1 同一入口 `(32669,32802)` 離開，預期 map0 `(32477,32853)`。進出各錄 S149／C52／S206／S87、HUD 原服座標與前後畫面；原站位可能經伺服器校正，以 S87 為準。再檢查返回後沒有洞內 NPC、魔法或選取殘留。
2. **船運 R13／N04**：map0 港口主人 70011，靜態位置 `(32633,32968)`，購買往古魯丁票 40299；遊戲時間 **01:30–02:29:59** 登船（之後每三遊戲小時循環），入口 `(32631,32983)`，預期船圖5 `(32733,32797)`。約 **02:30** 開船、**03:00** 到港，等待引擎自動送一次 C231，預期 map4 古魯丁港附近 `(32564..32566,32723..32730)`。
3. **回程**：map4 港口主人 70022 `(32560,32723)`，購買往說島票 40298；**03:00–03:59:59** 由 `(32543,32728)` 登船，預期船圖6 `(32735,32795)`；約 **04:00** 開船、**04:30** 到港，預期 map0 `(32631..32632,32972..32982)`。班次採 S123 遊戲時間，非 Windows 時鐘；每天 00:00 首班登船窗口只有 6 遊戲分鐘。兩趟都核對船票背包、舊 S87 不觸發假抵達、最終 S87／HUD 座標、下船後可繼續移動及無船景殘留。
4. **旅館 R14**：說話之島瑟琳娜 70012 `(32596,32929)`，對話選租房 `room`；靜態價格 100 金幣、鑰匙 49290。等待背包回包後重新對話選 `enter`，預期 map16384 `(32745,32803)`；也可持鑰匙踩入口 `(32599,32931)`。離房踩 map16384 `(32746,32803)`，預期 map0 `(32599,32931)`。確認門／NPC／對話／效果未跨圖殘留，租房未收到回包時不顯示已入住。
5. **MAP-F9／地圖特效**：於上述往返途中及 map4 港口可見效果處，截取入圖、離圖、回圖畫面與相同世界快照。核對換圖等待畫面不顯示舊特效、回圖重新起算、沒有重複 owner；6016 音效有聲／退役交 T074 的測法一併核對。每個子項分別標示已驗證／未驗證，不能用本次離線結果替代真機結果。
