# Issue #968 — T424：Q8-B① 盟屋拍賣出價步進

依據：`docs/reference/followups-15.md` §2.10、§3（Q8-B①）。本輪只做 ①（出價窗）；② WidgetSlot 模式不在本任務範圍。

## 做了什麼

`engine/source/NativeUi/AgitAuctionPanel.cs`（只動 `AgitBidPanel` 出價窗與檔頭說明）：

| 項目 | 行為 | 依據 |
|---|---|---|
| Minus | 值 ≤ 最小 → 設成最小，否則 −1；再夾在 [最小, 最大] | 已驗證（靜態）：`sub_5457C0`（FO `0x144BC0`）＋`sub_5471F0`（FO `0x1465F0`） |
| Plus | 值 ≥ 最大 → 設成最大，否則 +1；再夾在 [最小, 最大] | 已驗證（靜態）：`sub_545AC0`（FO `0x144EC0`）＋`sub_5471F0` |
| 按住連續 | 按下後第一個 Update 起算，`RepeatDelayMs`＝500 ms 後每 `RepeatIntervalMs`＝50 ms 走一步；每次 Update 最多 `MaxRepeatPerUpdate`＝10 步；指標移出按鈕暫停；放開（514）照舊再走一步 | 513 記方向與 `GetTickCount` 已驗證；**重複速度是推測**（更新函式沒追） |
| Ok／Enter | 低於最小 → 輸入框設成最小、字串 4381 印到聊天欄、不送；高於最大 → 設成最大、不送；範圍內照舊 `host.Bid` | 已驗證（靜態）：`sub_545920`（FO `0x144D20`） |
| 「最小」 | 用 `AmountOffer.Lower`（`max(1, Min)`），與既有送出條件一致 | 既有規則 |
| 空輸入框 | 當成 0（Ok → 最小＋4381；Plus/Minus → 夾到最小） | 推測（原版讀輸入框當數字） |

- 字串 4381：用既有的 `TextResolver`（`RefClientTextSource`，參考客戶端 `string-c.tbl`）取字，不在 repo 內寫死文字。
  - 本機 `0-客戶端` 的 `string-c.tbl`（3,484 行）**沒有** 4381（與 FU7 一致）。
  - 參考客戶端 kor25 `string-c.tbl`（Text.pak record 33458，解碼 SHA-256 `9b165baa…6531b6`，與 `RefClientTextSource.Kor25StringSha256` 相同）4381＝「輸入金額需要比目前競標價高」，意思符合。查法：`py -3 -I docs/reference/rea-scripts/t680/t680_strings.py <客戶端> scratch/t968/<名>.json 4381 4027 4029 2813`（唯讀，輸出在 scratch）。
  - 取不到字（沒有參考客戶端）→ 不印、不編字，記一次 log `4381-text`。
- 聊天欄輸出：新增選用介面 `IAgitAuctionChatHost { void ChatNotice(string text); }`。宿主有實作才印；沒有實作 → 記一次 log `4381-chat`，字留在 `AgitBidPanel.Notice`。
  - **產品宿主 `RingWindows.cs` 的 `AgitAuctionHost` 不在本任務可改範圍，目前沒有實作這個介面**，所以產品裡 4381 還不會出現在聊天欄（只寫 log）。見「給下一棒的提示」。

測試：
- 新檔 `engine/source/BidStepTests.cs`：`[WorldModuleTest("bid-step")]`，自動發現。合成版面（自寫的小 XML）與合成單色圖。涵蓋：±1、夾值的各種邊界、最小值 0→1、無法回答的 S136、接近 `int.MaxValue`、按住連續的時間點與上限、移出暫停、放開再走一步、Minus 按住停在最小、Ok 不連續、Ok 低於／高於／等於界線、宿主不能送時照樣夾值、沒有聊天宿主、沒有字串。
- `engine/source/RingAgitFollowupTests.cs`（**不在列出的可改檔案，但必須改**）：原本有兩個斷言寫死舊行為——「Plus 沒接、值不變」與「Ok 低於最小後輸入框不變，接著退一格再打字」。新行為下這兩處必然失敗，所以只改這兩處：Plus 改成檢查 +1；Ok 低於最小改成檢查輸入框被設成最小，並把後續清空輸入框改成全部刪除再打字。其他斷言沒動。

## 怎麼驗證

- 已驗證：`py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile ../.github/nuget.ci.config` → `PASS dev`（worktree 沒有離線 `vendor`，所以用 CI 的 NuGet 設定；暫時 lock，沒寫回 `rebuild-lock.json`）。
- 已驗證：`py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`，errors 0。
- 待驗證（交給 CI）：`--module-tests` 的 `bid-step` 與既有 `ring-agit-followup`。依本機規定，本機不執行任何編譯出的程式。

## 未驗證什麼

- 按住連續的實際延遲與間隔（500／50 ms 是推測）；原版移出按鈕時是否暫停也是推測。
- 產品裡 4381 印到聊天欄（宿主還沒接，見下）。
- 原版 Ok 送出後會關窗（FU15§2.10）；引擎照舊等 S136 被回答／消失才關，本輪沒改（避免動到 Enter 的既有行為）。
- 全部沒有真機驗收（P24Lab 開窗、按住 Plus、低價按 Ok 看聊天欄）。

## 產出路徑

- `engine/source/NativeUi/AgitAuctionPanel.cs`
- `engine/source/BidStepTests.cs`（新）
- `engine/source/RingAgitFollowupTests.cs`（兩處斷言）
- `docs/reports/issue-968.md`

## 給下一棒的提示

1. **接聊天欄（小任務，改 `RingWindows.cs`）**：讓 `AgitAuctionHost` 實作 `IAgitAuctionChatHost`，例如 `public void ChatNotice(string text)=>form.login.World.AddBoardChatNotice(text,null);`（與看板 189／544、寵物等級訊息同一個聊天列入口；顏色要另查原版 4381 用哪一色）。接上後 `4381-chat` log 不會再出現。
2. 若要追按住速度：反編譯出價窗的更新函式（讀 `+428` 方向與 tick 的那個），把 `RepeatDelayMs`／`RepeatIntervalMs` 換成實值並把註解的「推測」拿掉。
3. Ok 送出後關窗：若要照原版，`Submit` 成功時 `Close()`，並同步調整 `RingAgitFollowupTests` 中「Enter = Ok」那一條（目前它在第一次送出後再按 Enter）。
4. 真機（Codex／dot，P24Lab）：開盟屋拍賣 → 出價窗，按 Plus／Minus、按住 Plus、輸入低於目前價按 Ok，確認輸入框變成最小值；接上 1 之後再確認聊天欄出現 4381。
