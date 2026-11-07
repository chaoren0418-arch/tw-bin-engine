# Issue #316 報告：T135 盟徽查詢與 ID 對應

2026-11-07 初版，2026-11-08 與 #440 整合後改寫。分支 `task/316`。協定結論見 `docs/protocol/clan-emblem.md`。

## 重要結論

- **clan ID 與 emblem ID 是兩個獨立的數字空間。** S87 帶 clan ID；S167 與 S64/0x3c 帶 emblem ID；S118 推送時是 clan ID、C72 回覆時是 emblem ID；C72 要的是 emblem ID。
- **d287 的 emblem ID 恆為 0**（靜態結論：`setEmblemId` 全 JAR 無呼叫者）。所以其他血盟真實玩家的盟徽在 d287 上看不到，這是伺服器限制，要改善需要伺服器維護者裁定。
- d287 上實際能顯示的是：自己的血盟（登入 S118）、上傳當下的推送、假人，全部走 S87 clan ID ＋ S118 clan ID。

## 與 #440 的整合（2026-11-08）

本分支開發期間，#440（T168）已合併自己的 `ClanEmblem.cs`：512 筆 LRU 快取、跨連線磁碟快取、繪製時缺圖就自動送 C72（依原版用 S87 clan ID）。兩邊設計衝突，整合方式：

- **採用 #440 的快取與送出路徑**（main 已合併、依原版反組譯）。移除本分支原本的雙快取、S118 分類（5 秒回覆期限）與 `PumpWorldAsync` 掛接，所以 `WorldEntry.cs` 不再有改動。
- **保留本任務的 emblem ID 對應**：S167 與 S64/0x3c → 「角色 → (emblem ID, 盟名)」。
- 繪製掛載點（`ObjectRendering.cs` 原本那一行）改呼叫 `LoginConnection.ClanEmblemFor(objectId, clanId)`：有發布 emblem ID 就查並送 C72(emblem ID)，回覆前先顯示 clan 推送圖；沒有發布就維持 #440 的 C72(clan ID) 行為。
- **和本分支先前結論不同的地方**：先前寫「不要用 clan ID 送 C72」。#440 依原版做了後備查詢；在 d287 上它只會送出伺服器不回應的封包，無害，所以保留。風險寫在協定文件「客戶端實作」一節，要不要移除後備需主控裁定。

## 做了什麼

- `engine/source/ClanEmblem.cs`：#440 內容不動，另加 S64/0x3c 解碼、emblem ID 對應（盟名防止換盟後誤用、上限 256、`Reset` 清除）、`PublishedClanEmblemId`、`ClanEmblemFor`。
- `Clan.cs`：S167 一行掛接 `ObserveClanEmblemId`。
- `ObjectRendering.cs`：既有盟徽掛載那一行改呼叫 `ClanEmblemFor`，沒有新增掛載點。
- `WorldSession.cs`（範圍外，單行掛接）：S64/0x3c 一個 case；`Reset()` 加 `ResetClanEmblems()` 清除對應。
- 測試 `engine/_integration/dev/clan-emblem-tests/`（依整合後設計改寫），接在 `DemoChecks.csproj`、`clan-emblem` 群組、`map-continuity` 與預設入口。
- `docs/protocol/clan-emblem.md`：客戶端實作、已知限制、未驗證三節依整合結果改寫。

## 怎麼驗證

- 同步 origin/main（含 #440、#449），`rebuild.py --update-lock` 重產 dev lock，`--build` PASS，唯讀核驗 `dev-inputs-verified`。
- 離線測試（合成封包，走 `World.Receive` 與真實繪製掛載點）：`clan-emblem` 363、`name-display` 414、`clan` 446、`clan-war` 530、`map-continuity` 23196 項全部通過（計數包含共用檢查）。
- `check-no-game-assets.py` 通過（0 錯誤）。
- 整合過程中有一項檢查失敗，原因是測試本身漏了觸發 C72 的步驟；修正測試後通過，產品程式沒有改。

## 未驗證什麼

- 沒有真機測試：未啟動遊戲、伺服器或 DB；`ObjectRendering` 的實際繪製沒有執行過。
- P24Lab `clan_emblem` 的實際格式（#440 已支援 384-byte RGB555 與 BMP）。
- `getEmblemId()` 恆為 0 未在執行中的伺服器觀察。
- 整合後的版本**沒有重做**初版的變異測試。

## 已知限制

- 單一快取以 S118 的 D 為鍵，clan ID 與 emblem ID 數字相同時共用同一格（d287 上不會發生）。
- 入盟後到下一次 S87 前，新成員不顯示盟徽（S72 不帶 clan ID）。
- S250/173 盟徽識別開關尚未接上。

## 產出路徑

- `engine/source/ClanEmblem.cs`
- `docs/protocol/clan-emblem.md`
- `engine/_integration/dev/clan-emblem-tests/`
- `docs/reports/issue-316.md`

## 給下一棒的提示

- dot 真機驗收：有盟徽的血盟角色登入後，看名稱旁是圖像還是提示「收到無法解析的盟徽」；若是後者，記錄 S118 長度與開頭 bytes。
- 真機要驗：登入後自己的盟徽顯示、傳送後仍在、盟主上傳後周圍玩家立即看到。封包追蹤預期會看到 #440 的 C72(clan ID)，而且伺服器不回應；不應該出現 C72(0)。
- 是否移除 C72(clan ID) 後備、是否讓其他血盟的盟徽可見（伺服器端），需要主控或伺服器維護者裁定。
