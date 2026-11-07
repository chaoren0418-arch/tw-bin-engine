# 顯示類封包（#415，T152）

來源：`docs/reference/server-protocol.md`，以及唯讀查閱 `1-伺服器` 的 `S_IdentifyDesc`、`S_BlueMessage`、`S_ChangeName`、`S_PinkName`、`S_TrueTarget` 與呼叫端。實作在 `engine/source/DisplayNotices.cs`。

| Opcode | 格式 | 客戶端行為 |
|---|---|---|
| S245 `IDENTIFYDESC` | `H(descId)` `H(messageId)` `C(count)` `S×count` | 畫面下方白字顯示 8 秒 |
| S105 `REDMESSAGE` | `H(id)` `C(count)` `S×count` | 畫面中央紅字顯示 5 秒 |
| S46 `CHANGENAME` | `D(objectId)` `S(name)` | 去掉 `\f` 色碼後改物件名稱 |
| S60 `PINKNAME` | `D(objectId)` `C(seconds)` | 依秒數顯示粉紅名，`0` 立即清除 |
| S11 `TRUETARGET` | `D(targetId)` `D(objectId)` `S(message)`，或 `D(targetId)` `D(targetId)` `S(null)` `H(gfx)` | 目標頭上顯示標記與文字 |

## 查表時要注意的地方

- **S245 的重量字串只有 etcitem 分支才有**。`server-protocol.md` 的「default」標記容易誤讀成所有分支都有；實際上 `writeS(weight)` 在 `S_IdentifyDesc.java` 第 92 行，位於 `case 0` 內，所以武器（134）與防具（135）的字串數就是 `count`。
- **S245 無參數建構子**（測試用，目前沒有呼叫端）在 `count=3` 後多寫一個重量字串。客戶端只對 134 接受這種形式，其他訊息多出字串一律拒絕。
- **S245 非武器、防具、etcitem 的物品**只寫 `descId`，後面全是補零；客戶端不顯示。
- **S11 第二個欄位**在 `S_TrueTarget` 註解寫「施展者」，但 `TRUE_TARGET.java` 第 34 行對血盟成員送的是收件者自己的 id，所以只保存，不當施展者解讀。
- **S11 沒有結束封包**，技能 113 的 `buffDuration` 是 0。標記持續到目標離場、被新標記取代或世界切換；客戶端不自訂時限。
- **S46 對自己的封包不改 `Actor.Name`**。伺服器對玩家送的是真名加色碼或 GM 前綴，而 `Actor.Name` 是選角身分，S87 也靠它比對。

## 文字來源

訊息 134–138、551、552、637 的文字來自伺服器原始碼註解，**尚未與 `Text.pak/string-c.tbl` 逐字核對**。其他 id 顯示為「原服訊息 id：參數」。

## 推測值

紅字 5 秒、鑑定 8 秒、粉紅名的 `HotPink`、標記形狀都不是伺服器資料，也沒有和原客戶端比對，要在真機驗收時確認。
