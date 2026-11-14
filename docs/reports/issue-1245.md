# Issue #1245 f1211-rank-bookmark-window：/rank 排名窗看不到、/bookmark 沒有讀回（#1211 SM31、SM38）

## 結論

- **SM31（/rank）是引擎缺陷，已修。** S39 排名頁有送到原生排名窗，窗也真的開了，但視窗只有 **32×32**，所以看不到。
  - 原因：排名窗的版面 `GameRankingUI.xml` 讀自參考客戶端 kor25，surf 卻先從主客戶端 `0-客戶端` 找。
  - 主客戶端對這 39 個 surf 編號都有同號 `.img`，但全是 32×32 小圖。
  - 視窗範圍等於底圖大小，結果整個窗被裁成 32×32。
  - 修法：參考客戶端的版面只用參考客戶端的 surf。另外加一道保護：底圖放不下視窗自己的元件時，停用原生窗，退回查詢頁。
- **SM38（/bookmark、/qbookmark）程式沒有缺陷，是驗收工具判錯。**
  - C165 的回包是 **S92**（單筆書籤），伺服器兩次都有回。工具卻在等 S54。
  - #1211 現成的截圖已經能看到書籤窗開啟，而且讀回 `SM1211`、`SM1211Q`。
  - 「開窗」那一步（不帶名稱的 `/bookmark`）工具根本沒送出。
  - 這部分只加診斷 log，沒有改行為。

## 證據核對（已驗證：唯讀查看 #1211 證據目錄 `client-A/`）

| 證據 | 看到什麼 | 代表什麼 |
|---|---|---|
| `packet-trace.jsonl` 09:03:30.276／09:04:54.559 | out 125（16 B）→ in 39（56 B），間隔 13–43 ms | /rank 有送出、伺服器有回 |
| `native-ui.log` 17:03:33.319 | `視窗堆疊全部關閉：GameRanking_Window` | 第 1 次 /rank 後，排名窗**在視窗堆疊裡**（UiRoot 只把 `Visible && Bounds 非空` 的窗放進堆疊） |
| `native-ui.log` 17:08:49.038 | `視窗堆疊全部關閉：ITEM_WINDOW、GameRanking_Window` | 第 2 次 /rank 後，排名窗一直開到 17:08:49 |
| `m7-SM31-rank2-empty.png` 對照 `m7-SM31-rank2-after.png`、`m7-SM31-rank-after.png` | /rank 之後 (189,48) 多出一個 32×32 小圖，送出前沒有 | 這就是被裁成 32×32 的排名窗（XML 的 Window X=189 Y=48） |
| `native-ui.log` 開頭 | 有 `ui-xml-ref GameRankingUI.xml -> …kor25…Tile.pak`，沒有 `ranking-ui 缺圖` | 版面取自參考客戶端；surf 全在本機「找到了」，所以不會報缺圖 |
| `m7-SM38-add-send.json`／`quick-send.json` | out 165（8 B／16 B）→ in **92**（24 B）；工具 `ExpectedOpcode` 165、`ResponseOpcodes` **54** | 伺服器有回，回的是 S92。工具等錯 opcode，所以判成「沒有預期回包」 |
| `m7-SM38-add-after.png` | 原生書籤窗開啟，記憶座標清單有 `SM1211`，1/50 | S92 → 開窗 → 讀回正常 |
| `m7-SM38-quick-after.png` | 快速移動清單有 `SM1211Q`；記憶清單有 `SM1211`、`SM1211Q`，2/50 | qbookmark 加進快速移動清單也正常 |
| `m7-SM38-window-send.json` | `Empty-line positive control failed; no command sent` | 不帶名稱的 `/bookmark`（開窗）沒有送出，所以 `m7-SM38-list.png` 沒有書籤窗 |
| `m7-SM34-delayed.png` | 好友名單窗可見；排名窗（應該同時開著）看不到 | 疊窗本身沒問題，只有排名窗的範圍錯 |

### 資源比對（已驗證：PakViewer CLI 唯讀解出，輸出放 `scratch/x/`，不進 git）

- 參考客戶端 `D:\381懶人包kor25(原)\Tile.idx` → `GameRankingUI.xml`：36,858 B，解密後 SHA-256 `a774c2a6…6d610e`，與 #769 相同。
  - `Window X=189 Y=48 BaseSurf=5558;1`；Exit (408,14)；分頁鈕 y=42；翻頁鈕 y=317。
- 兩邊客戶端的 `SurfList.xml` 對 39 個 surf 都是 `id == file`（例：`5558` → `5558`）。
- 39 個 surf 逐一查 Sprite.idx、Sprite00–15.idx，照 HashFileManager 順序：先 `<F>c.img` 再 `<F>.img`，先 hash 分包再 Sprite。

| surf | 0-客戶端（舊行為先用這個） | kor25（版面所屬） |
|---|---|---|
| 5558（底圖） | Sprite02/`5558.img` 2,056 B＝**32×32** | Sprite05/`5558c.img` 305,536 B＝**422×362** |
| 5567（標題） | `5567.img` 2,056 B（32×32） | `5567c.img` 4,124 B |
| 5573–5578（分頁底圖） | 各 2,056 B（32×32） | `557xc.img` 188,376–206,904 B |
| 5579–5596（分頁鈕三態） | 各 2,056 B（32×32） | `55xxc.img` 各 2,952 B |
| 5559–5566、5597–5599（翻頁鈕、名次變動） | 各 2,056 B（32×32） | `.img` 346–398 B |
| 4645／4646（Exit） | `.img` 136 B | `4645c.img`／`4646c.img` 58,488 B |

- `.img` 開頭的 u16 寬、u16 高：本機 5558 是 `20 00 20 00`（32×32），kor25 5558c 是 `A6 01 6A 01`（422×362）。
- 查過的 pak：
  - `0-客戶端`：`Tile.idx`（SurfList.xml）、`Sprite.idx`、`Sprite00.idx`–`Sprite15.idx`。
  - kor25：`Tile.idx`（GameRankingUI.xml、SurfList.xml）、`Sprite.idx`、`Sprite00.idx`–`Sprite15.idx`。
  - Text 類 pak 與本題無關，沒有查。本題不宣稱缺資源：39 個 surf 在 kor25 全部找得到。

## S39 分派表（本引擎）

3.81 的 S39 是 `S_NPCTalkReturn`：`C(39) D(objid) S(htmlid) [H(1) H(n) S×n]`。`WorldSession.ReceiveCore` 依 htmlid 由上往下分派：

| 順序 | 位置 | 條件（htmlid） | 去處 |
|---|---|---|---|
| 1 | `WorldSession.cs:43` → `Buddies.cs:82` | `buddy` | 好友名單窗（SM34 看到的空名單窗） |
| 2 | `WorldSession.cs:62` → `Pets.cs:105–107` | `summonlist` | 召喚選擇 |
| 3 | `PlayerQuery.cs:138` | `y_who`（自己） | 查詢頁 |
| 4 | `PlayerQuery.cs:142` | `pc_show`（自己） | 查詢頁 |
| 5 | `PlayerQuery.cs:145–154` | `y_h_2`／`y_k_2`／`y_k_3`／`y_w_1`／`y_c_1`，參數數量正確（11／10／10／10／20） | 物件 id＝自己：先發 `RankingWindowRequested`，原生排名窗（`GameRankingPanel.Offer`）能用就開窗，否則顯示查詢頁。不是自己：交給 NPC 對話，用解出的行補樣板 |
| 6 | 其他 | 任何 htmlid（含 `agsel` 等） | NPC 對話窗（`HYPER_TEXT`） |

- `/rank` 送 `C125 D(self) S("lv_ranking")`（`PlayerQuery.cs:29`），d287 伺服器回 `y_h_2`（n=11，#10＝職業標題，見 `docs/protocol/player-query.md:25`）。
- #1211 這兩次 /rank 的回包都是 56 B。`y_h_2` 加 10 個空白名次、「全職業」，padding 後長度吻合（推測：trace 只記長度，沒有內容）。
- 回包走第 5 列 → 原生排名窗。之前看不到，是因為上面說的 32×32 範圍。分派本身沒有錯。
- /buddy 能開窗、/rank 不能：兩者分派依據都是 htmlid，不是子類別。差別在排名窗的 surf 來源，不在 S39 分派。

### 與原 BIN 對照（`TW1608262200.bin`，SHA-256 `e12949cf…09641a2`，ImageBase 0x400000）

- pefile 對照：各函式位址的檔案位移處都是 `55 8B EC` 開頭（已驗證）。
- 函式之間的關係沿用 `docs/reference/windows.md:119`（T140），本輪沒有重跑 IDA。

| 項目 | VA | RVA | File Offset | 說明 |
|---|---|---|---|---|
| S_HYPERTEXT 收包（TW16 op 144） | 0x940030 | 0x540030 | 0x53F430 | → 下一列 |
| HTML 顯示 | 0xBBCB00 | 0x7BCB00 | 0x7BBF00 | 顯示在 `HYPER_TEXT_WINDOW`（字串 VA 0x1191DE4／FO 0xD909E4） |
| `CHighLevelRankingUI` 建構子 sub_612AA0 | 0x612AA0 | 0x212AA0 | 0x211EA0 | windows.md：沒有呼叫者 |
| RTTI `.?AVCHighLevelRankingUI@@` | 0x135FF50 | 0xF5FF50 | 0xF5E750 | 全檔掃描沒有以這個位址當立即值的地方（已驗證） |
| 字串 `GameRankingUI.xml` | 0x119CAE8 | 0xD9CAE8 | 0xD9B6E8 | 唯一引用在資料區 VA 0x137B438（推測：UI XML 檔名表）；0-客戶端 Tile.pak 沒有這份檔（PAK§4） |
| 書籤單筆收包（TW16 op 2） | 0x931D60 | 0x531D60 | 0x531160 | 把新書籤寫進書籤窗並開窗（`BookmarkPanel.cs:16`）＝3.81 S92 |

- 原版 /rank 的結果顯示在 HTML 窗（HYPER_TEXT_WINDOW）。
- 原生排名窗是 T331（#769）依 kor25 資源加上的介面，不是 3.81 行為。窗內的 surf 編號只在 kor25 有意義，這就是本次修正的依據。

## 書籤：預期回包與程式路徑

| 步驟 | 程式 | 說明 |
|---|---|---|
| `/bookmark 名稱`、`/qbookmark 名稱` | `PlayerChat.cs:550–568` | 送 C165 S(名稱)，並呼叫 `Bookmarks.Sent(null)`。qbookmark 另外經 `ClientForm.OfferQuickBookmark` 在書籤窗設旗標 |
| 伺服器回包 | `WorldSession.cs:74` → `Bookmarks.ReceiveOne`（`Bookmarks.cs`） | **S92**：`S(名稱) H(地圖) H(x) H(y) D(id)`。書籤由伺服器加入，客戶端不會自己先加 |
| 書籤窗更新 | `BookmarkPanel.cs:325–349` | 同一角色、revision 變了、notice＝`已收到伺服器書籤資料。` → 開窗。qbookmark 旗標在時，把新出現的 id 加進快速移動清單 |
| 登入整份清單 | `WorldSession.cs:71` → `Bookmarks.ReceiveList` | S64 `2A 80 00 02` 開頭 |
| 刪除 | `Bookmarks.cs`（Delete） | C3 S(名稱)，伺服器沒有確認回包 |

`docs/testing/smoke-m7.md` 和程式的對照：

- `:731` 的 `PlayerChat.cs:565`（qbookmark）✓、`AbilityStatus.cs:116`（隨機傳送 id 0）✓。
- `:731` 的 `BookmarkPanel.cs:546` ✗：這一行現在是「點下拉以外就收起」。拖曳的起點在 `:551`，放到快速移動清單在 `:563`。描述的行為和程式一致，只是行號過期。本張範圍不含 smoke 文件，所以沒有改。
- `:805`（19 區域、log `zone-bookmark … 19 組`）✓：#1211 的 `native-ui.log` 16:43:43 有 `…：19 組（1551 個 zone…）`。

## 做了什麼（程式）

- `engine/source/NativeUi/GameRankingPanel.cs`
  - `Load`：主客戶端 Tile.pak 沒有 `GameRankingUI(-c).xml`（版面來自參考客戶端）時，surf 一律從參考客戶端讀（`SurfSource`／`ReferenceSurfs`）。參考客戶端沒有的 surf 列為缺圖、不畫，不改用本機同號圖。版面在主客戶端時照舊用 `SurfCatalog.Get`。
  - 保護：視窗層任何元件的位置落在底圖外，就判 `Unavailable`，這時 /rank 退回查詢頁，不會開一個看不見的窗。真實版面的所有視窗層元件都在 422×362 內（已核對 XML）。
  - 診斷 log：載入時一行 `ranking-ui 版面與 surf 取自…；視窗 X,Y WxH，底圖 <archive>/<file>`；開窗時一行 `ranking-ui 開窗 <html> → <分頁>，N 列；範圍 …`。
- `engine/source/PlayerQuery.cs`：自己的 S39 排名頁加一行 log `s39 rank <html> self 列數 N → 原生排名窗|查詢頁`。行為不變。
- `engine/source/Bookmarks.cs`：加 `bookmark S64 整份清單 n/max 筆`、`bookmark S92 單筆 id=… 名稱=… 地圖 m (x,y)；清單 n 筆`。行為不變，notice 文字不動（書籤窗靠它判斷是不是單筆收包）。
- `engine/source/NativeUi/RankBookmarkFollowupTests.cs`（新）：`[WorldModuleTest("rank-bookmark-1245")]`，全部合成資料：
  - 32×32 底圖：停用、不認領排名頁，`RequestRankingWindow` 退回查詢頁。參考 surf 底圖：Bounds＝(189,48,422,362)。參考客戶端缺的 surf 不拿本機的來補。
  - 合成 `/rank` 回包 S39 `y_h_2` → `DecodeRankHtml` → 事件 → `Update` 後排名窗開啟（Level 分頁、兩列名字、log 有範圍）。空排名也會開窗，頁碼 0 / 0。
  - 合成 S64（空清單）＋兩筆 S92（`SM1211`、`SM1211Q`）→ `Bookmarks.Items` 讀回兩筆，notice 是單筆，log 兩行。
  - 用 `[WorldModuleTest]` 而不是 `[CheckGroup]`：這是產品組件內的離線測試，`docs/design/m6-contract.md:50,64` 建議用這個，CI 的 `--module-tests` 會自動跑全部。沒有改 `Program.cs`、`DemoChecks.csproj`、註冊表。

## 怎麼驗證

- `dotnet build engine/source/OfflineClient.csproj -c Release -r win-x64 -p:SelfContained=false`：0 警告、0 錯誤。輸出 DLL 含新的模組測試名與 log 字串。已設 `DOTNET_CLI_HOME`（scratch）、`DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`。
- 依本機限制沒有執行任何編譯出來的程式。新測試交給 PR 的 CI（`--module-tests`）。
- 證據判讀、PakViewer 資源比對、pefile 位址：都是唯讀，見上方各表。

## 已驗證／推測／待驗證

- **已驗證**
  - #1211 的排名窗有開，但只有 32×32（log＋截圖）。
  - 本機 39 個同號 surf 是小圖，kor25 是真圖。
  - C165 → S92，書籤窗開啟並讀回 SM1211／SM1211Q（截圖）。
  - 工具的書籤預期 opcode 是 54（錯），開窗那一步沒送。
  - BIN 位址對照。
  - 編譯通過。
- **推測**
  - 56 B 的 S39 是空的 `y_h_2`（trace 沒有內容）。
  - VA 0x137B438 是 UI XML 檔名表。
  - 其他「版面取自參考客戶端」的原生窗（`AgitAuctionPanel`、`FishingPanel` 等，註解寫「same as GameRankingPanel」）可能有同樣的 surf 來源問題，本張沒有查證、沒有改。
- **待驗證（交 `f1211-retest`）**
  - SM31：/rank 後在 (189,48) 看到完整 422×362 的排名窗（英雄風雲榜 Level 分頁），伺服器沒有資料時頁碼 0 / 0。
  - SM38：新增讀回、Delete 刪除、19 區域篩選、從記憶清單拖到快速移動清單。

## 給下一棒的提示（f1211-retest）

- 工具的預期 opcode：
  - `/rank`：C125 → **S39**（不變）。
  - `/bookmark 名稱`、`/qbookmark 名稱`：C165 → **S92**（不是 54）。
  - 不帶名稱的 `/bookmark` 只在本機開窗，**不送封包**，不要設預期 opcode，改看畫面或 log。
- 要從 `native-ui.log` 收的行：
  1. 啟動時：`ranking-ui 版面與 surf 取自參考客戶端；視窗 189,48 422x362，底圖 ref:Sprite05/5558c.img`。如果出現 `ranking-ui 停用：…落在底圖外`，表示參考客戶端沒讀到，/rank 會退回查詢頁，也要截圖。
  2. /rank 後：`s39 rank y_h_2 self 列數 N → 原生排名窗`，接著 `ranking-ui 開窗 y_h_2 → Level 分頁，N 列；範圍 189,48 422x362`。
  3. 新增書籤後：`bookmark S92 單筆 id=… 名稱=SM1211 地圖 … ；清單 n 筆`。登入時會有 `bookmark S64 整份清單 n/50 筆`。
- 截圖時機：ESC 或 `CloseAll` 會關掉排名窗。#1211 第 1 次 /rank 3 秒後就被下一步的清空動作關掉了（17:03:33）。請在送出 /rank 後、下一個指令清空聊天列之前截圖。
- `smoke-m7.md:731` 拖曳的行號請改看 `BookmarkPanel.cs:551`（起點）、`:563`（放開）。

## 產出

- 程式：`engine/source/NativeUi/GameRankingPanel.cs`、`engine/source/PlayerQuery.cs`、`engine/source/Bookmarks.cs`。
- 測試：`engine/source/NativeUi/RankBookmarkFollowupTests.cs`（新）。
- 報告：`docs/reports/issue-1245.md`。
- 分支：`task/1245`。沒有改 `rebuild-lock.json`、`docs/REPORT.md`。
