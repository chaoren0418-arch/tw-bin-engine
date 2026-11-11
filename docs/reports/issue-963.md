# Issue #963 報告：T421 R11-G 小清理（過時註解）

依據：`docs/reference/gap-matrix.md` 15.13.5、15.13.6（R11-G）；主控 #917 追加（FU15§1）。不對應 gap-matrix 的列。

## 做了什麼（只改註解，程式行為不變）

| 檔案 | 原註解 | 更正為 | 依據 |
|---|---|---|---|
| `engine/source/PrivateShopTradeSource.cs` 檔頭（原 `:22–25`） | 答 Y 的分派「還沒接上」；原生 ChatPanel 不畫 playerChatNotice | R10-F（#905）起 `PlayerChat.SendPlayerChat` 在刪除確認之後分派到 `AnswerPrivateShopConfirm`；原生 ChatPanel 經 `TradeUi.cs` 的 `ChatLinePrompts.Of` 畫 1028 提示（Key `shop1028`，Esc＝`AnswerPrivateShopConfirm("")` 不送）。「推測／未做」只留：1028 另外也寫到狀態列（`Set(State,text)`），可考慮拿掉 | `PlayerChat.cs:457–459`、`TradeUi.cs:67`、#905 報告 |
| 同檔 `AnswerPrivateShopConfirm` 的 summary（原 `:132`） | 輸入列的分派還沒接到這裡 | 列出兩個呼叫者：`PlayerChat.SendPlayerChat`（R10-F）、原生聊天列提示的 Esc（`TradeUi.cs`） | 同上 |
| `engine/source/TitleAndEmote.cs:117` | 表情保留期間移動輸入由 `Resurrection.cs MoveLocked` 擋住 | 只暫停按住的移動續走（`EmoteInputPaused`，0xDF5401–0xDF5416）；R10-G（#906）起不算進 `MoveLocked`（只看 S241），攻擊、施法、門、傳送點不擋 | `Resurrection.cs:23–28`、#906 報告 |
| `engine/source/NativeUi/StatInfoPanel.cs:55–58` | 22 列值「computed by sub_EB85F0 — not traced」、待查 | FU15（T402／#917）§1：值是伺服器 protobuf 回覆的字串（C157 子 id 484），客戶端只轉字串與換色；`sub_EB85F0`（經 `sub_EBB050`，+0x24C）決定顏色、不算數值；+0x23C 是目前字串（`sub_EBC240` 複製）。3.81 沒有 protobuf → K1，數值刻意留白（規則 5） | `docs/reference/followups-15.md` §1.1、§1 結論 |
| 同檔 `:48`（附帶） | 數值空白「(待做)」 | 改成「(K1, see the R7-I values note below)」，與上面一致 | 同上 |

## 怎麼驗證

- **已驗證（本機）**：
  - `dotnet build engine/source/OfflineClient.csproj -c Release` 成功（0 錯誤；3 個既有警告都在未改的檔案）。worktree 沒有 `engine/vendor`，所以建置時用 `-p:RestoreSources=<原專案 engine\vendor>`（唯讀讀取），`NUGET_PACKAGES` 指到 worktree 內 `engine/.nuget`。
  - `py -3 engine/tools/check-no-game-assets.py`：`pass`，errors 0。
  - `git diff` 只有註解行變動（3 個檔案，+16／−9）。
- **交給 CI**：依本機執行限制不執行編譯出來的程式；既有單元測試與 `--module-tests` 以 PR 的 GitHub Actions 結果為準。本任務只改註解，沒有新增測試（沒有可測的行為變更）。
- **資源**：沒有用到圖或字串表，也沒有「缺資源」的宣稱，所以不必查 PakViewer。

## 未驗證什麼

- 無程式行為變更，不需要真機核對。

## 產出路徑

- `engine/source/PrivateShopTradeSource.cs`、`engine/source/TitleAndEmote.cs`、`engine/source/NativeUi/StatInfoPanel.cs`（只改註解）
- 報告：`docs/reports/issue-963.md`
- 分支：`task/963`

## 給下一棒的提示

- `PrivateShopTradeSource.cs:108` 的 `Set(State,text)`（1028 同時寫狀態列）現在可以考慮拿掉（#905 報告也提過）；這是行為變更，要另開任務並加測試。
- `docs/protocol/resurrection.md:47` 仍寫「表情保留…也併進 `MoveLocked`（`EmoteInputPaused`）」，R10-G 後已過時；不在本任務範圍，下一張碰到該文件的任務請更正。
- gap-matrix 15.13.6 的 R11-G 可標為已完成。
