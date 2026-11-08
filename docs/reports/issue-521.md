# Issue #521 — T215：M7-O 發送座標對齊原版

依據：`docs/reference/gap-matrix.md` 第 7 節 M7-O；`docs/reference/world-objects.md`（WO）第 5 節。

## 做了什麼

產品只改了 `engine/source/LocationShare.cs`：

1. **MapSystem 地區表**（`LocationRegionTable`）
   - 執行時從客戶端 `Tile.pak` 讀 `MapSystem(-c).xml`，沿用既有的 `UiResources.Shared.Xml`，只在記憶體解密，不進 git。
   - 只收 `World_Map`（id 0–99）與 `Village_Map`（id 100–999）底下的 `<Map>`，讀 `id`、`mapDesc`、`mapNum`、`listed`。`Field_Map`／`Dungern_Map` 沒有 id，不算地區。
   - 檢查很嚴：根節點不對、禁止 DTD、id 重複、id 超出區段、數字不合法、`listed` 不是 true/false，一律拒收。
   - `OnShown` 時在背景載入。載入完成後把聊天 revision 加一，讓聊天重畫，畫面文字和可點連結就會一致。載入失敗時只寫 Trace，地區照樣用編號顯示。
2. **送出**：第 4 欄改填目前地圖的 MapSystem 地區 id，不再固定填 0。
   - 選法（**推測**）：先找 `mapNum` 等於目前地圖的 World_Map 項目（取最小 id），沒有就找 Village_Map 項目（取最小 id），都沒有才送 0。
   - 原版從位置物件 `+0xC` 取值，算法沒有追（WO §5），所以這條規則是推測。
3. **接收顯示**：聊天行從 `[座標] 寄件者：地圖 m (x, y)` 改成 `[座標] 寄件者：地區{id}〔字串{mapDesc}〕〔訊息1784〕`。
   - 地區名稱與 1784 的原文都還沒查證，所以用編號占位，沒有自編文字。
   - id ≥ 100 查村莊表，< 100 查世界表；表中沒有的 id 只顯示 `地區{id}`。
   - 地圖與 x、y 仍保留在型別資料裡，既有的 ⊕ 標記與跨圖提示照常運作。
4. **獨立紀錄清單**：`WorldSession.LocationShares` 最多 10 筆，滿了先刪最舊的。
   - 每筆記寄件者、座標、解析出的地區與聊天序號，另有 `LocationShareRevision`。
   - 清單與 100 筆聊天歷史分開；伺服器回覆 S71 1782／1783 不列入。
5. **1785 占位**：收件者不合法時，提示加上 `〔訊息1785〕`。原版的入口是地圖視窗 `TransferMenu`，本組範圍不含入口，`/loc` 照舊保留。

測試：
- 新檔 `engine/_integration/dev/location-share-tests/LocationRegionChecks.cs`，由既有的 `LocationShareChecks()` 呼叫，並在 `DemoChecks.csproj` 加了一行 Compile。
- 舊的 `LocationShareChecks.cs` 改了 4 條斷言：顯示文字從數字座標改成地區占位。
- `rebuild-lock.json` 已用 `--update-lock` 重產。

## 怎麼驗證

| 項目 | 結果 |
|---|---|
| `rebuild.py --update-lock` 後 `build-product.ps1`（`-ConfigFile .github/nuget.ci.config`，因為 worktree 沒有 `engine/vendor`） | PASS dev；product DLL `615f9611…eb68` |
| `DemoChecks … location-share` | PASS 463 |
| `DemoChecks … map-continuity`（CI 跑的組，含 `LocationShareChecks`） | PASS 32989 |
| `PlayerChatChecks` | PASS 138 |
| `check-no-game-assets.py` | pass，errors 0 |
| 選用的本機檢查：用 `LOCATION_REGION_XML` 指向 scratch 中已解密的 `MapSystem-c.xml`（不在 git 內） | 解析出 91 個地區（46 世界＋45 村莊）；地圖 4 → 1，地圖 0 → 0 |

新測試涵蓋：
- 解析與 13 種拒收情況。
- 世界優先、村莊後備、選法固定，查不到時送 0。
- 送出封包的第 4 欄是地區 id。
- 顯示占位（世界、村莊、未知 id）。
- 清單上限 10 筆、先刪最舊；不收 S71；與聊天歷史分開。
- Reset 會清空清單；`RefreshLocationLabels` 會讓聊天重畫。
- 1785 占位。

## 沒驗證什麼

- **字串 1784／1785／2139 與各 `mapDesc` 名稱的原文**：都在加密的 `Text.pak`，未取得，所以只用編號占位。
- **地區 id 就是 MapSystem Map id**：WO §5 標為推測。送出時的選法也是推測，與原版 `+0xC` 的算法可能不同；例如在亞丁大陸的村莊裡，原版可能送村莊 id，本版送世界 id 1。
- **功能物件 `+16` 為 0 時只顯示 2139、不記錄**：條件語義未知，沒有實作。
- **10 筆清單在原版哪個 UI 顯示、能不能點**：WO 標待驗證，所以只做資料，沒做畫面。
- **執行時從 `Tile.pak` 載入**：只驗證了編譯，以及同一份解析程式對解密檔的處理。沒有實際啟動產品，背景載入與重畫沒有實機驗證。
- 沒有啟動遊戲、伺服器或 DB。

## 產出路徑

- `engine/source/LocationShare.cs`
- `engine/_integration/dev/location-share-tests/LocationRegionChecks.cs`（新檔）、`LocationShareChecks.cs`
- `engine/_integration/dev/demo-tests/DemoChecks.csproj`
- `engine/_integration/dev/rebuild-lock.json`
- 本報告

## 給下一棒的提示

- **重設的掛點**：`WorldSession.Reset` 目前沒有擴充點，清單靠「角色 id 變了」或「聊天序號重來」來偵測重設。M7-A 合併後，請改成在 Reset 掛點直接清空 `locationShares`。
- **字串原文**：解出 `Text.pak` 的 1784／1785／2139 與 `mapDesc` 字串後，只需要改 `LocationShareProtocol.RegionLabel`／`Format`；可以另開 resource 任務。
- **協定文件**：`docs/protocol/location-share.md` 還寫「區域 id 送 0」「顯示數字座標」，本組不在可改範圍，請文件維護者依本報告更新。
- **原生入口**：地圖視窗 `TransferMenu` 輸入列（1785）與 10 筆清單的畫面，要等 UI 畫布（T190／T196）完成，並先查證原版的顯示位置。
