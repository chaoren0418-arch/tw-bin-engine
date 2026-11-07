# Issue #316 報告：T135 盟徽查詢與 ID 對應

2026-11-07，分支 `task/316`。協定結論見 `docs/protocol/clan-emblem.md`。

## 重要結論

- **d287 的 emblem ID 恆為 0**（靜態結論：`setEmblemId` 全 JAR 無呼叫者，`_emblemId` 只有 setter 內一處寫入）。所以自動 C72 在 d287 上不會觸發，**其他血盟真實玩家的盟徽看不到**。這是伺服器限制，要改善需要伺服器維護者裁定。
- d287 上實際可顯示的是：自己的血盟（登入 S118）、上傳當下的推送、假人。全部走 S87 clan ID ＋ S118 clan ID。
- C72 的查詢邏輯已完成並有測試，但在 d287 上屬於待命路徑，要等伺服器發布非零 emblem ID 才會實際運作。

## 做了什麼

- 新增 `engine/source/ClanEmblem.cs`：解析 S64/0x3c；clan ID 與 emblem ID 分開快取；S118 依「是否有未結案的 C72」分類；角色 → emblem ID 對應（以盟名防止換盟後誤用）；未知盟徽查一次，結果快取。
- `Clan.cs`：圖像快取移到新檔，S167 接上 emblem ID 來源。`FindClanEmblem(clanId)` 簽章不變，所以 T128 的 `PaintClanLabels` 掛載點不用改，名稱旁顯示直接沿用。
- **修正 T128 的隱性缺陷**：圖像快取原本隨 WorldEpoch 清除，但伺服器只在登入送一次 S118，盟徽會在第一次傳送後永久消失。現在保留到 `Reset`。
- 測試 `engine/_integration/dev/clan-emblem-tests/`，已接上 `DemoChecks.csproj`、`clan-emblem` 群組、`map-continuity` 與預設入口。
- `docs/protocol/clan-emblem.md`。

## 超出「可修改檔案」清單的改動（請審查）

都是單行掛接，理由如下：

| 檔案 | 改動 | 理由 |
|---|---|---|
| `WorldSession.cs` | S64/0x3c 一個 case | 合約允許的分派掛接；0x3c 是 emblem ID 的來源 |
| `WorldSession.cs` | `Reset()` 加 `ResetClanEmblems()` | 快取不再隨 WorldEpoch 清除，需要連線層級的清除點 |
| `WorldEntry.cs` | `PumpWorldAsync` 加 `PumpClanEmblemQuery` | 自動查詢需要定期的送出時機。合約的送出掛接寫 `LoginConnection.cs`，但 pump 在同一個 partial class 的這個檔案 |

`ObjectRendering.cs` 與 `ClanUi.cs` 沒有修改。`docs/protocol/clan.md` 與 `clan-tests/README.md` 因不在範圍內沒有改，其中「未接自動查詢」「快取依 WorldEpoch 失效」的描述已由 `clan-emblem.md` 註明取代。

## 怎麼驗證

- `rebuild.py --update-lock`、`--build`、`--verify`：通過，product／audio／launcher 建置成功。
- `clan-emblem` 群組 323 項通過（含共用檢查）；`map-continuity` 22476 項通過，既有血盟與角色重置測試都包含在內。
- `check-no-game-assets.py` 通過。
- 2026-11-07 再同步 origin/main（併入 #416 暫時能力），`Program.cs` 兩邊測試入口都保留，重產 dev lock 後重建：`clan-emblem` 323 項、`map-continuity` 22818 項通過，資源檢查 0 錯誤。
- 封包全走正式入口：`World.Receive`，查詢經真實的 `PumpWorldAsync` 與 `sendMovementGate`。S64/0x3c 用伺服器註解裡的樣本 bytes 核對。
- **變異測試**：手動破壞 8 處關鍵邏輯（命名空間分類、pump 掛接、盟名防護、切圖保留、排隊失效、重複查詢、最新優先、回覆期限），全部被測試擋下。其中「最新優先」第一次存活，已補測試後確認擋下。切圖保留的變異是在較早的檢查就失敗，不是由切圖專用的檢查擋下。
- 過程中 `CharResetChecks` 抓到我的回歸：不完整的 S64/0x3c 會中斷連線。已改成需要完整 10 bytes 才分派，短封包維持原本的未處理計數；既有測試沒有改。

## 未驗證什麼

- **真實盟徽的資料格式（最大風險）**：T128 解碼器只接受 16×12 BMP，`clan_emblem` 實際內容未讀取。推測舊版系列可能是無檔頭的 384 bytes；若如此，P24Lab 上所有 S118 都會顯示「無法解析」，只有名稱沒有圖像。
- 沒有真機測試：未啟動遊戲、伺服器或 DB。`PaintClanLabels` 的實際繪製沒有執行過。
- `getEmblemId()` 恆為 0 未在執行中的伺服器觀察。

## 已知限制

- 入盟後到下一次 S87 之前，新成員不顯示盟徽（S72 不帶 clan ID，且 `ClanUi` 只傳 clan ID）。
- S250/173 盟徽識別開關尚未接上。
- S118 分類的模糊區見協定文件。

## 產出路徑

- `engine/source/ClanEmblem.cs`
- `docs/protocol/clan-emblem.md`
- `engine/_integration/dev/clan-emblem-tests/`
- `docs/reports/issue-316.md`

## 給下一棒的提示

- dot 真機驗收時，**先看有盟徽的血盟角色登入後是顯示圖像，還是提示「收到無法解析的盟徽」**。若是後者，請記錄 S118 長度與開頭 bytes，再開任務擴充解碼器。
- 真機要驗：登入後自己的盟徽顯示、傳送後仍在、盟主上傳後周圍玩家立即看到、全程不應該送出 C72（封包追蹤可確認）。
- 是否讓其他血盟的盟徽可見，需要主控或伺服器維護者裁定。不要在客戶端用 clan ID 送 C72，也不要送 0：`C_Clan` 比對 0 會命中第一個血盟，回傳不相干的圖像。
