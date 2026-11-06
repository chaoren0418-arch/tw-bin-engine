# Issue #261：T127 寵物與召喚物

## 結論

**部分完成，待調度器／Claude 裁定擴充 S39 分流範圍；不可合併為功能完成。** 本機建置與離線測試已通過。原任務把 C103 寫為控制指令，但來源顯示 C103 是背包查詢，控制走 C125；C104 成功後的 S39 回覆不相容於現有 NPC 解析器。

## 做了什麼

- 新增 `Pets.cs`：S83 項圈清單、S176 寵物背包與有號 AC，保留倉庫分流；C103 背包查詢、C104 依原服索引切換裝備、C125 六種控制指令。資料先完整驗證才發布，限制外部修改背包順序。
- 沿用 S87 世界物件與 S237 HP；不改世界渲染，不把無主人 FollowerPack 當成可控制夥伴。
- 送出時核對 session、世界世代、角色、物件版本與背包版本；失效操作不送出、不預扣物品、不自動重送。裝備操作後須重新查詢背包。
- `PetUi.cs` 透過表單 OnLoad 加入既有玩家服務視窗的「寵物」分頁；包含控制、背包、項圈資訊。`NpcUi.cs` 未修改。
- `WorldSession.cs` 僅新增 S83、S176 各一個分支，置於既有 NPC 處理前以避免 S176 先被倉庫解析消耗。
- 提交模組測試並接入既有 DemoChecks／Windows CI 入口；dev lock 用 `--update-lock` 產生。

## 怎麼驗證（已驗證）

- `rebuild.py --update-lock`：175 個來源檔、184 個鎖定輸入核驗成功。
- `rebuild.py --build --configfile scratch/pets-nuget.config --output scratch/pets-build`：產品、audio、launcher 編譯成功；僅既有 `MagicUi.cs` 兩則 nullable 警告。
- `DemoChecks ... pets`：52 項通過，使用產品 DLL、合成封包與記憶體 sender。
- `DemoChecks ... map-continuity`：180 項通過，含上述寵物測試。
- `check-no-game-assets.py`：通過。
- 本機 NuGet HTTPS 發生 TLS 錯誤，改以 scratch 設定唯讀引用原專案 `engine/vendor`；套件快取、TEMP、CLI HOME 均指定 worktree 內，未複製遊戲資源。
- 本機 `gh` API 回 HTTP 401；改用已連線 GitHub 工具處理 PR 與 CI 查詢。

## 未驗證／未完成

- S39 純解析器已完成，但尚未掛入 `ReceiveCore`。擴充超出明列可改的 S83／S176，已提出範圍確認，尚未取得回覆。
- **C104 成功回覆鏈仍有已知錯誤**：原服會送 `S_PetMenuPacket`，現有 NPC 解析會拒絕其 C旗標格式。寵物詳細資訊也尚無已接通的查詢入口。
- 未啟動遊戲、伺服器、DB，未執行原生 UI／實機驗收。沒有把合成測試當真機證據。
- CI 結果以 PR checks 為準；本段會於提交後查核。

## 產出路徑

- `engine/source/Pets.cs`、`engine/source/PetUi.cs`
- `engine/source/WorldSession.cs`
- `engine/_integration/dev/pets-tests/`、`engine/_integration/dev/demo-tests/`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/pets.md`、本報告
- 不追蹤本機證據：`scratch/pets-build/`、`scratch/pets-checks.json`、`scratch/pets-map-checks.json`

## 給下一棒的提示

調度器／Claude 先裁定允許新增 S39 寵物專用分流，接著由本機執行器掛接已備妥的 `ReceivePetDialog`（非寵物 S39 保留 NPC 路徑），增加查詢資訊入口及真正透過 `Receive` 的 anicom／moncom／一般 NPC／RaiseAttr 回歸，再重跑 lock、建置與 CI。完成前 PR 維持草稿，不關閉 #261。原生驗收集中交給 dot 的 M6 smoke。
