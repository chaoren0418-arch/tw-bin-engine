# Issue #415 — T152 鑑定說明、紅字訊息、名稱狀態（code，第 1 次）

## 做了什麼

新增 `engine/source/DisplayNotices.cs`，處理五種顯示類封包。格式與查證結果在 `docs/protocol/display-notices.md`。

- **S245 鑑定說明**：涵蓋武器、防具、魔杖、食物、光源、其他，以及只有 descId 的形式。文字在畫面下方顯示 8 秒。
- **S105 紅字訊息**：畫面中央顯示 5 秒。551、552、637 有文字，其他 id 顯示「原服訊息 id」。
- **S46 改名**：去掉 `\f` 色碼後更新物件名稱。
- **S60 粉紅名**：依封包秒數顯示，`0` 立即清除，換世界時失效。名稱畫在頭上。
- **S11 精準目標**：支援文字和 gfx 兩種形式，在目標頭上畫標記與文字。

共用檔案的改動：

- `WorldSession.cs`：加 5 個 case。
- `ObjectRendering.cs`：加 1 行 `PaintDisplayNotices(g)`。名稱顏色查詢 `NameColor` 在這一行呼叫的方法裡。
- `ServerStatusMessages.cs`：加 `CenterNotice` 掛載點。

測試在 `engine/_integration/dev/display-notices-tests/DisplayNoticeChecks.cs`，接上 demo-tests 的 `display-notices` 入口，也併入 CI 會跑的 `map-continuity` 組和無參數的完整回歸。

## 設計決定（請審查）

1. **S245、S105 不寫 `LastServerMessage`**。`Clan.cs:117` 把任何非 informational 的伺服器訊息視為入盟申請被取代，寫進去會讓鑑定物品取消待處理的入盟申請。所以改由 `PaintDisplayNotices` 直接畫在場景上，不進背包與 HUD 的訊息列。
2. **S46 不改自己的 `Actor.Name`**。那是選角身分，GM 前綴不該取代它。自己的名稱目前也沒有畫在畫面上。
3. **T121（#216）尚未合併**，S245 沒有和 `ItemStatus` 合併顯示。
4. **`descId` 只保存**，沒有查 `ItemDesc-c.tbl` 的長說明。

## 怎麼驗證

- 已合併 `origin/main`（落後 14 個提交；只有 `rebuild-lock.json` 衝突，取 main 版後以 `--update-lock` 重產）。以下結果都在合併後的內容上重跑。
- `rebuild.py --update-lock` 後 `--build` 通過，213 個來源／222 個鎖定檔。產品 DLL SHA-256：`a6cd79cedb24cade5c0825c3add9ed25a430e18e91393c8b2f95869a4d060254`。
- demo-tests 各組皆退出 0：`display-notices` **334**（共同 276 ＋ 本模組 58）、`map-continuity` **22416**、`combat-admission` **1409**、`doors` **597**、`buddies` **445**、`clan` **395**、`item-transfer` **349**、`polymorph-cadence` **309**。各組都含共同檢查，數字不可加總。
- 合成封包涵蓋上述全部 S 格式，以及截斷、非零補位、缺字串、多字串的拒絕與原子性。本任務沒有新增 C 封包。
- `check-no-game-assets.py` 通過，dev lock 唯讀核驗通過。
- CI 結果以 PR 最新 head 為準。

## 未驗證什麼

- **畫面沒有看過**。`PaintDisplayNotices` 的繪製程式只有編譯，沒有離線測試，也沒有啟動遊戲。位置、重疊、可讀性都待 M6 smoke 確認。
- **訊息文字來自伺服器註解**，沒有和 `string-c.tbl` 逐字核對。
- **推測值**：紅字 5 秒、鑑定 8 秒、`HotPink`、標記形狀，都沒有和原客戶端比對。
- **S11 的 gfx 只保存，沒有播放特效**。伺服器目前沒有呼叫這個建構子。
- **本機沒跑** MailChecks、NpcChecks 等獨立測試專案，以 CI 為準。

## 已知限制

- 選中的粉紅名目標，腳下的選取標籤仍是金色，粉紅名畫在頭上。任務限定 `ObjectRendering.cs` 只加一個點，所以沒有改第 69 行。
- `WorldSession.Reset` 不在可改範圍，所以紅字與鑑定文字靠 `Entered` 和時限失效。若在 5～8 秒內重新登入，舊文字可能短暫出現一次。粉紅名與精準目標靠 `WorldEpoch`，沒有這個問題。
- 世界切換或 `!Renderable` 期間文字不繪製，但時限照算，傳送途中收到的 552 可能看不到。

## 產出路徑

- `engine/source/DisplayNotices.cs`
- `engine/_integration/dev/display-notices-tests/DisplayNoticeChecks.cs`
- `docs/protocol/display-notices.md`
- `docs/reports/issue-415.md`

## 給下一棒的提示

- M6 smoke（dot）：用鑑定卷軸看 S245，兩個測試角色互打看粉紅名，王族施放精準目標看標記。確認顯示時間與顏色。
- 若想讓鑑定文字進訊息列，要先讓 `Clan.cs` 的判定認得 S245，建議另開任務。
- T121 合併後，可把 `LastIdentifyDesc` 接進物品 tooltip。
- 建議另開小任務：在 `Reset` 加清除呼叫、選中目標的名稱改用 `NameColor`、用 `string-c.tbl` 核對文字。
