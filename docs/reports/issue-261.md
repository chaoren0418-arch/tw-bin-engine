# Issue #261：T127 寵物與召喚物（第二次執行）

## 結論

**阻塞，未完成驗收，不可合併。** 本次已準備 C34 詳細資訊查詢與真正的接收流程回歸，建置成功，但新測試重現 S39 被 NPC 解析器拒絕的既有缺陷。原任務仍明列 `WorldSession.cs` 僅允許 S83／S176；Issue 全部留言與 PR 留言中未找到調度器／Claude 的擴充裁定，已在本次對話提出單一 S39 分流確認，尚未取得答覆。未把重新排入佇列當作擴充範圍同意。

## 做了什麼

- 保留前次 S83 項圈、S176 寵物背包與倉庫分流、C103 背包查詢、C104 裝備索引、C125 控制，以及 S87／S237 世界物件與 HP 共用實作。
- 唯讀核對原服 C34 → 寵物／召喚物 `onTalkAction` → S39 詳細資訊的路徑，來源 SHA-256 補入 `docs/protocol/pets.md`。
- `Pets.cs` 新增 `Information` 編碼及 `SendPet(...,"info")`，沿用送出前的 session／世界／物件／背包版本檢查。查詢或裝備操作清除舊詳細資訊，不預填數值。
- `PetUi.cs` 增加明確的寵物／召喚物資訊查詢按鈕。回覆分派未接通前，這仍不是可驗收功能。
- repo 測試路徑新增 C34 byte 格式、查詢入口、S39 真正 Receive、無效回覆原子性、C104 成功回覆、NPC／RaiseAttr 相容案例；保留既有測試入口，不以純解析測試替代接收驗證。
- 本輪沒有修改 `WorldSession.cs`、`NpcUi.cs`、`docs/REPORT.md`、機密設定、封存、原專案或遊戲資源。

## 怎麼驗證

已驗證：

- `rebuild.py --update-lock`：175 個來源檔、184 個鎖定輸入通過。
- `rebuild.py --build --configfile scratch/pets-nuget.config --output scratch/pets-build`：產品、audio、launcher 建置成功；產品僅既有 `MagicUi.cs` 兩則 nullable 警告。
- DemoChecks 建置成功；執行 `pets` 時，在首個 anicom 真實接收路徑拋出 `P06.ProtocolException: dialog arguments`（`NpcInteraction.cs:56`，由 `WorldSession.ReceiveCore` 呼叫）。退出碼 1，**不是通過**。
- `check-no-game-assets.py`：pass，3252 個追蹤檔案，0 錯誤。
- 候選一行分流 patch 已通過 `git apply --ignore-space-change --check`，未套用。
- 建置使用 scratch NuGet 設定唯讀引用原專案 `engine/vendor`；快取、TEMP 與 CLI HOME 均在 worktree。

待驗證：

- S39 分流套用後的全套 pets／map-continuity 回歸；目前測試在已知缺口中斷，後續案例沒有宣稱通過。
- 本次 head 的 CI；前次 head `7cde33a` 的 CI 成功不能代表本次變更通過。新增回歸未修復前，Windows 測試預期失敗。
- 未啟動遊戲、伺服器、DB，未驗證原生 UI、部署 JAR 與實機。

## 產出路徑

- `engine/source/Pets.cs`、`engine/source/PetUi.cs`
- `engine/_integration/dev/pets-tests/PetChecks.cs`、`README.md`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/pets.md`、本報告
- 不追蹤本機證據：`scratch/pets-build/`、`scratch/pets-checks.json`（本次 blocked，已覆蓋舊 pass 狀態）
- 不追蹤候選差異：`scratch/pets-s39-proposed.patch`

## 給下一棒的提示

調度器／Claude 必須先明確裁定允許 `WorldSession.ReceiveCore` 加 `case 39:if(ReceivePetDialog(plain))return;break;`。這只攔截 anicom／moncom；其他模板回傳 false 並保留原 reader 與 NPC／RaiseAttr 路徑，`NpcUi.cs` 不動。取得裁定後由本機執行器套用、重新更新 dev lock、建置、跑完新增回歸、更新報告與 PR／CI。不要跳過紅燈測試，也不要再次以「部分完成」結案。PR #276 應保持草稿並阻擋合併，#261 保持開啟；實機集中交 dot 的 M6 smoke。
