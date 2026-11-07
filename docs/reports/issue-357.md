# Issue #357／T140 執行報告（第 2 次）

## 結論

查詢玩家、查看資訊、排名三個目標都已用 d287 實際存在的協定實作，離線測試全綠。**任務原文的 C99、C63 刻意不送**，因為兩者都不是查詢。審查提出的兩條路裡，這次走「提供正確協定後補齊實作」，不是限縮範圍。這項替換尚未有主控裁定，已在 Issue 請主控確認，並另列於下方。

## 回應前次審查退回

| 退回項目 | 處理 |
|---|---|
| `/check`、`/rank` 只拋不支援訊息 | 兩者都已實際送出查詢並顯示結果。 |
| 協定落差需裁定 | 第一次的「沒有排名協定」結論不成立，當時只看了 C99／C63。重新搜尋後，來源與 d287 JAR 都找到真實入口，見 [協定](../protocol/player-query.md)。 |
| 不可硬送防外掛／血盟階級封包 | 未送；另有測試斷言三個指令都不會送出 C99／C63。 |
| 補上 C／S 格式與結果顯示測試 | 已補，零送出測試已換成功能測試。 |

## 做了什麼

- `/rank`：C125 D(self) S("lv_ranking") → S39 `y_h_2`，全職業等級榜顯示於聊天區，不需要 NPC。
- `/check`：先選取目標 → C125 D(self) S("pc_list") → 等伺服器提示 → C223 D(self) C0 D(target) → S39 `pc_show`，29 欄顯示於聊天區。含 3 秒本機逾時，失敗回覆也會顯示。
- 風雲榜 NPC：`y_h_2`、`y_k_2`、`y_k_3`、`y_w_1`、`y_c_1` 回覆同步顯示到聊天區，既有對話視窗不受影響。`y_c_1` 的宣戰選項不顯示、不送出。
- **修正第一版的 bug**：S71／166 是伺服器泛用訊息（來源 352 處），第一版全部攔下並標成「玩家資訊」，等於吃掉既有訊息處理；參數數量不是 1 時還會拋例外。現在只攔帶 `($1501–3)` 善惡代碼的 `S_WhoCharinfo`。
- 共用掛接：PlayerChat 相對 main 只改 `SendPlayerChat` 一行；WorldSession 仍是 S39／S71 各一個分支。
- 併入 origin/main，解決 WorldSession、DemoChecks、dev lock 衝突（`case 71 when` 須排在 `case 71:` 之前），重產 lock。

## 怎麼驗證（已驗證）

- .NET SDK 10.0.300；`rebuild.py --update-lock`、`--build` 通過，187 個來源檔、196 個鎖定檔（併入 main 至 89e80f9 後）。
- 產品 DLL SHA-256：`6d6f8d7e619fa26e9cb66be102d2a634b829dba1011aac9789064ce0be67606b`。變異測試在併入最新 main 之前做，當時還原後重建的 SHA 與變異前相同。
- `player-query` 791 項通過（含 main 新增、每個群組都會跑的共用檢查）：每個 C／S 位元組向量、截斷、padding、實際分派、選取提示時序、逾時、目標變更、session 重設、節流、六種失效守衛×三個指令。
- CI 群組：`map-continuity` 20640、`combat-admission` 368、`buddies` 259、`item-transfer` 163，全部通過。`player-chat` 107 項通過。
- 變異測試 4 項全部被抓到：166 全部當玩家資訊、移除 `/check` 的逾時判斷、宣戰選項當名次、`/rank` 改送 C63。第一輪全部假性通過，原因是 `dotnet run --no-build` 沒有換 DLL；修正後重跑並以 DLL hash 確認。
- `check-no-game-assets.py` 通過（3316 個追蹤檔，0 錯誤）。
- 協定以 `javap` 與 class 常數比對 d287 JAR，SHA-256 見協定文件。
- CI 狀態以 PR checks 為準。

環境處理與上次相同：NuGet 用 scratch 內的臨時設定唯讀引用原專案 `engine/vendor`；未改產品設定或 CI。

## 未驗證什麼

- 未啟動遊戲、伺服器、DB；未讀機密設定、未查詢或修改 DB。所有封包測試都是合成資料，不代表真機通過。
- `lv_ranking`、`pc_list` 由自身 objid 送出的實際回應未經真機確認，目前是靜態分析結論。
- **風雲榜 NPC 可能不在世界中**：`380_db.sql` 沒有 80026–80029 的生成資料列，P24Lab 實際狀態未知。`/rank` 不受影響，NPC 鏡射路徑可能走不到。
- `y_c_1` 的數值欄位意義未核對，原樣顯示。
- `/check` 中斷時伺服器可能留在選取模式，下一個寵物指定目標的 C223 會被吃掉一次（伺服器行為）。

## 需要主控確認

T140 原文指定 C99、C63，實作改用 C125／C223。請主控確認此替換，並修正任務敘述。這不影響程式，但我不能自行認定已獲核准。

## 產出路徑

- `engine/source/PlayerQuery.cs`、PlayerChat／WorldSession 最小掛接。
- `engine/_integration/dev/player-query-tests/`、demo-tests 入口、dev `rebuild-lock.json`。
- `docs/protocol/player-query.md`、`docs/reports/issue-357.md`。
- 本機暫存證據：`scratch/issue-357/*.json`（不入 git）。

## 給下一棒的提示

- 主控：確認上述協定替換。
- dot（P24Lab smoke）：
  1. `/who` 空名、在線名字、查無名字（查無名字回的是線上資訊，不是離線通知）。
  2. `/rank` 有無回 `y_h_2`、聊天區名次是否正確。
  3. 選取另一個測試角色後 `/check`，核對 HP、六圍與裝備；再選 NPC 測「只能對玩家查詢狀態。」。
  4. 確認一般 S71／166 訊息（例如自動狩獵提示）仍正常顯示，不再帶「玩家資訊」。
  5. 若世界中有風雲榜 NPC，點選確認對話與聊天鏡射。
- 合併由使用者處理。
