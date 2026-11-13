# Issue #1052 — T467 素材準備總流程

## 做了什麼

- 新增 `AssetPrepOrchestrator.Product().Run(AssetPrepSetRequest)`，從 `[AssetRecipe]` 自動發現實作，依契約第 6 節順序執行全部 13 個 recipe 與完整預熱項目；`inventory` 不納入。
- 各 recipe 只完成自己的 staging，依賴讀取也從 staging 取得。整體成功才組成共享 assets 樹，檢查重複路徑、每檔大小／SHA、來源 entry、容器 identity 與錨點，再產生總收據、13 份 recipe 收據及 integrity v2。總收據先寫 `partial` 並驗證，再寫 `ready`、讀回核對；以同磁碟兩次 rename 發布，保留前一棵樹供中斷復原。
- 總收據記錄 17 個 Sprite、Text、Tile 的 idx 大小／SHA 與 pak 大小、實際錨點 SHA、recipe 版本與範圍、輸出統計與收據 SHA、預熱清單 SHA、直讀元件與 L1–L7／D3 限制。既有 `Partial` coverage 表示預熱範圍；所有要求項目完成且 unavailable 已明列時，依契約可為 `ready`。
- 進度提供階段、類別、完成／總數、新做／重用數、估計所需空間與可用空間。預設估計輸出 800 MiB（另計 staging／candidate 雙份與 16 MiB 保留空間），可由呼叫端調整。實際磁碟資訊、時間與客戶端路徑只寫 `runtime/prep-log.json`。
- 磁碟不足、取消、recipe 失敗會保留逐項 checkpoint；重試驗證來源與輸出後重用。全域 writer lock 同時涵蓋單一 recipe 程式庫介面。檔案集合變更也算來源變更（map 目錄、prior output 清冊）；個別項目失效時重建依賴它的 aggregate manifest。工具收據版本升為 `tw-asset-prep/3`。
- `Assets.VerifyIntegrity` 接受 v1／v2；v2 額外驗總收據 SHA 與 `ready`，保留 maps 延遲驗證規則。其餘讀取點的 `files[].path/size/sha256` 格式不變。

## 怎麼驗證

已驗證：

- 本機 `dotnet build engine/source/OfflineClient.csproj -c Release -o engine/build/client-product-dev`：成功；完整編譯 4 個既有警告、0 錯誤。
- 本機 `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：成功；3 個既有警告、0 錯誤。
- 所有本機 dotnet 呼叫均把 CLI home、NuGet cache、暫存放在本 worktree，且設定 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`。僅編譯，沒有執行編譯產物。
- 已同步當時的 `origin/main`；沒有修改 `docs/REPORT.md`、`rebuild-lock.json`、Program.cs、DemoChecks.csproj 或註冊表。

CI 驗證項目（最終結果待最新 commit 完成）：

- 新增 `[CheckGroup("asset-prep-set")]`，亦掛入既有 CI 的 `native-ui-resources` 群組，不修改 workflow。
- 合成兩份獨立客戶端，跑實際全部 13 個 recipe；比較整棵樹、收據與 integrity；用既有 Assets／PlayerProfiles／PlayerShadows／ObjectAssets／MapEffects／OriginalMapStore／Portals 讀取。
- 逐一注入 13 類 recipe 故障，驗 active 不變；取消後續跑、磁碟不足重試、checkpoint 損壞時重建項目與 manifest、兩次 rename 間與之後的中斷復原、重複執行不改 active、全域鎖及 junction 拒絕、v1 相容與 v2 收據拒絕條件。
- 補測最後發布閘門的取消／candidate 竄改、磁碟寫滿錯誤分類、損壞的新 active 回退，以及客戶端新增地圖目錄後會重建對應 terrain。

## 未驗證什麼

- 真實 TW 客戶端完整準備、r29 等價比對、真實耗時／峰值空間與原生畫面驗收，交 T470／T474；合成測試不代表這些驗收完成。
- 本任務提供總流程 API，沒有 launcher 啟動接線；T468 負責。
- 沒有修改或執行原客戶端、既有遊戲程序、DB、封存目錄或原始專案目錄。

## 產出路徑

- `engine/source/AssetPrep/AssetPrepOrchestrator.cs`
- `engine/source/AssetPrep/AssetPrepSetReceipt.cs`
- `engine/source/AssetPrep/AssetPrepCore.cs`、`AssetPrepInput.cs`、`AssetPrepRunner.cs`
- `engine/source/Assets.cs`
- `engine/_integration/dev/asset-prep-tests/AssetPrepSetChecks.cs`
- 本報告；本機建置紀錄在不追蹤的 `scratch/build-product.log`、`scratch/build-checks.log`。

來源 SHA-256（本 worktree 檔案 bytes）：

| 檔案 | SHA-256 |
|---|---|
| `AssetPrepOrchestrator.cs` | `6a55a4edb63c0b3110df8da8246411116ba366fa9cfca88e52eefab984565fc1` |
| `AssetPrepSetReceipt.cs` | `af881f955baf2722f58c320c19ef3e7c4dd2e0c12a67414c55647c6ec9f82cb5` |
| `AssetPrepCore.cs` | `86160283f521f73966f92115e2d14c615ac4459a43f631b51524d7e5e9e37d67` |
| `AssetPrepInput.cs` | `4965c110e15b371ba1b956dbfb3f55f1650853697d1efc2bc94424be3528d59e` |
| `AssetPrepRunner.cs` | `9246b6a2b9365ef8b1f0bb396f3a9995195d03a539d06606794c4a0241c4cfc6` |
| `Assets.cs` | `11a585f9adfc7a326e9f02cc8894c3f15d346c5de160aeb81d9d97b779fc0f17` |
| `AssetPrepSetChecks.cs` | `3a0ac16b2e267374d72fae78d016108b053aa05f91143e9f5231042d80424bfb` |

## 給下一棒的提示

- T468 呼叫 `AssetPrepOrchestrator.Product().Run(new(clientRoot, engineRoot){Cancel=token, Progress=callback})`；進度 callback 同步呼叫，UI 需自行 marshal。預熱清單及集合固定，呼叫端不用組 recipe 項目。
- `AssetPrepSetResult` 回傳 Published／UpToDate、總收據及新做／重用項目數。取消丟 `OperationCanceledException`；空間不足為 `AssetPrepFailure.DiskSpace`；其餘沿用準備器錯誤分類。
- 不要清除 `runtime/assets-staging/<recipe-id>`：它們供續跑與驗證重用；`set.candidate` 可重組，`set.previous` 由下次總流程自動復原。保留 checkpoint 的磁碟成本需由 T470 實測。
- 兩次 rename 之間有極短的 assets 不存在窗口；程序中斷後下一次 Run 先恢復舊樹。這是契約沿用 T451 的可復原發布方式，不宣稱 Windows 具備整個目錄的單一原子交換 syscall。
- 不合併 PR，由審查者確認最新 CI 後交既定合併流程。
