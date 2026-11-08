# Issue #557 — T235：城堡 HTML 頁與城主王冠（W2-B）

依據：`docs/reference/windows-2.md`（T226／#533）第 5.1 節、第 9 節 W2-B。

## 做了什麼

只改 `engine/source/Castle.cs`、`engine/source/CastleTests.cs`（加上 `--update-lock` 重產的 `rebuild-lock.json`）。沒有改 `WorldSession.cs`、`NpcInteraction.cs`、`NpcUi.cs`。

- **S185／S141／S4 開 HTML 頁**（已驗證：頁名來自 §5.1 的 TW16 靜態分析；模板已存在於 r29 `dialogs/templates.json`）
  - S185 → `tax`，S141 → `withdraw`，S4 → `deposit`。
  - 顯示方式：沿用 NPC 對話面板裡「伺服器主動送的頁」那一格（`WorldSession.ShowQueryPage`，WIN§2.1 同一個 HYPER_TEXT_WINDOW）。這樣不會覆蓋進行中的 NPC 對話或商店，也不必改 `NpcUi.cs`。
  - 參數：
    - `#0`＝目前對話 NPC 的名稱（原始 `$n`，由畫面依 names.json 解析）。查不到就留空，不用自己的名字代替。
    - `withdraw` 的 `#1`＝`"$457 : {金額} $4"`（TW16 格式「`%s : %d %s`」，字串 457＝寶庫、4＝金幣）。
    - `deposit` 的 `#1`＝`"{持有金幣} $4"`。持有金額來自背包裡的金幣；「數字＋金幣」這個排法是**推定**。
  - 新增 `WorldSession.CastlePage`（`CastlePage(Kind, Html, Args)`）、`CastleView.Page`、snapshot `castle.page`。
  - 送出（`CastleSent`）或取消（`CancelCastleOffer`）時，會關掉這一頁。條件是面板上顯示的仍是這一頁；如果面板已被其他查詢頁換掉，就不動它。
  - 使用者自己關掉面板時，頁會失效，但詢問（原服一次性票）還在，回覆照樣可以組。
  - S141 金額 0 時照樣開 `withdraw` 頁並顯示 0，但不開詢問。
- **S69 城主表與王冠資料**（城主表由 M7-C 完成，本張補動畫資料）
  - `CastleProtocol.CrownFrame`：12 格，每格 500 ms。
  - `CrownAnchor`：x＋17；y − 高度（40，特定外型 50）− 12。
  - 每座城各自計時：從該格被寫入時開始算，時間來源是可注入的 `CastleClock`。
  - `WorldSession.CastleCrownFrames(objectId)` 回傳該人物頭上各城的王冠格。第 2 格不畫，物件 0 不畫，Reset 會清空。
  - **不畫**：王冠圖源 `dword_15451B8` 的載入點待驗證；依外型、方向、動作的偏移表內容也未取得。

## 怎麼驗證

- 改程式之前先跑 `rebuild.py --update-lock`。之後 `rebuild.py --build`：PASS。
  - productDll SHA-256：`2b071b189e294b4e31f477326e7f15105e2dcbb0890f8e29986e377f64d3762d`
  - NuGet 用 `scratch/issue-557/NuGet.Config`，指向原專案 vendor（唯讀）。
- `P11.ClientEngine.exe --module-tests <out>`：結束碼 0，4 組共 255 項全過。
  - castle：62 → 100 項。新增 `Pages`（頁名、參數、關頁條件、金額 0、持有金幣）與 `Crowns`（格數循環、錨點、各城計時、第 2 格、Reset）。
  - agit 44、recovery-death 72、world-extensions 39。
- `DemoChecks hypertext`（T218 查詢頁契約，與本張共用 QueryPage）：PASS 357。
- `check-no-game-assets.py`：pass。
- 測試只用合成封包，沒有讀取遊戲資源。

## 未驗證什麼

- 沒有啟動遊戲或伺服器，也沒有實機看過頁面。
- `deposit` 頁 `#1` 的確切排法、TW16 S185 多讀的那一個 C 的意義，都待驗證。
- 頁內的數字輸入框與送出鈕還沒有畫面：目前面板只顯示頁面文字，回覆仍只能走 `LoginConnection.SendCastleReply`。原生 HYPER_TEXT_WINDOW 等 T190。
- 王冠沒有畫：圖源與偏移表待查；`ObjectRendering.cs` 的掛點等 T168。

## 產出路徑

- `engine/source/Castle.cs`、`engine/source/CastleTests.cs`、`engine/_integration/dev/rebuild-lock.json`
- 本報告 `docs/reports/issue-557.md`

## 給下一棒的提示

- 畫王冠時，在 `ObjectRendering.cs` 掛點呼叫 `World.CastleCrownFrames(obj.ObjectId)` 與 `CastleProtocol.CrownAnchor(x, y, tall)`。`tall` 對應 `sub_501260`，還需要查外型清單。
- 頁內數字框（T190 或 W2-D 一起做）：用 `CastleView.Offer` 的 Min／Max，送出走 `SendCastleReply`；`CastleView.Page` 可判斷目前顯示的是哪一頁。
- `CastlePage` 只在 `QueryRevision` 沒變時有效；其他模組呼叫 `ShowQueryPage` 會自然讓它失效。
