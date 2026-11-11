# Issue #986 — T429：R12-B 出價窗收尾

依據：`docs/reference/gap-matrix.md` 15.14.5／15.14.6 R12-B（4:AgitAuctionUI），FU15§2.10。

## 做了什麼

| 項目 | 改動 | 依據 |
|---|---|---|
| 4381 印聊天欄 | `RingWindows.cs` 的 `ClientForm.AgitAuctionHost` 改成同時實作 `IAgitAuctionChatHost`：`ChatNotice(text)` 在世界內時用 `World.AddBoardChatNotice(text, BidChatColor)` 印到聊天欄（看板／信件也走這個入口）。`BidChatColor`＝24 | 已驗證（靜態）：見下方「顏色查證」 |
| Ok 送出後關窗 | `NativeUi/AgitAuctionPanel.cs` 的 `AgitBidPanel.Submit`：`host.Bid(o,v)` 之後馬上 `Close()`（會記下這筆 S136 的 revision，同一筆不重開；下一筆 agapply 照常開窗）。低於最小、高於最大、宿主不能送、不能回答的 S136 都**不關窗** | 已驗證（靜態）：`sub_545920` VA `0x545A7B` 呼叫 `sub_9FBC00("cddcs",0xBC,…)`，接著 VA `0x545A83–0x545AA9` 呼叫 `[this+0x154]` 的 vtable `+0x8C(0,1,1)`（關窗），然後回傳 |
| 測試 | 新檔 `BidCloseTests.cs`（`[WorldModuleTest("bid-close")]`，自動發現）；`RingAgitFollowupTests.cs` 的「Enter＝Ok」改成用下一筆 S136 重開再按，並檢查送出後關窗；`BidStepTests.cs` 的 Ok 段補兩次「下一筆 S136 重開」（見「範圍外的改動」） | — |

### 顏色查證（`sub_545920`，FO `0x144D20`）

BIN：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（本輪重算，與 FU15 相同）。本機找不到 IDA 主程式，所以改用 Capstone 唯讀反組譯（`scratch/t986/dis.py`，輸出 `scratch/t986/sub_545920.asm`，不進 git）。

```
005459A3 push 0 / push -1 / push -1
005459A9 mov edx,4 ; imul edx,edx,0xE ; push [edx+0x1369D70]   ; palette[14]
005459B8 mov ecx,4 ; imul ecx,ecx,0x18 ; push [ecx+0x1369D70]  ; palette[24]
005459C7 push 0x111D                                            ; 4381
005459CC mov ecx,0x153C998 ; call 0x4EAFF0                      ; 取字串
005459D6 push eax ; call 0x7DC850                               ; 印聊天欄
```

也就是 `sub_7DC850(字串4381, palette[24], palette[14], -1, -1, 0)`，參數排法跟信件結果一樣（FU15§2.7，`MailChatColor`＝24）。引擎的 `AddBoardChatNotice` 只收一個顏色，所以用第一色 24；第二色 palette[14] 的用途引擎沒有對應（跟信件一樣）。

## 怎麼驗證

- 已驗證：`py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile <worktree>/.github/nuget.ci.config` → `PASS dev`（0 錯誤；3 個警告都在既有的 `MagicUi.cs`、`HtmlActionTests.cs`，與本任務無關）。用暫時 lock，沒有寫回 `rebuild-lock.json`。
- 已驗證：`py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`，errors 0。
- 待驗證（交給 CI）：`--module-tests` 的 `bid-close`（新增）、`ring-agit-followup`、`bid-step`。依本機規定，本機沒有執行任何編譯出來的程式。

## 範圍外的改動（必要，請審查）

- `BidStepTests.cs`（#968 的測試，不在本任務列出的檔案內）：`Ok` 段原本在同一筆 S136 上連送三次。改成送出後就關窗以後，關著的窗 `Key(Back)` 回 false、`Input` 不會變短，測試裡的 `Type()` 會**無窮迴圈**卡住 CI。所以只在兩次送出之後各補一行「`host.BidOffer` 換下一個 revision＋`Update`」重開窗，斷言沒改；另外更新一行過時的註解（「產品宿主沒有聊天欄」）。
- `IAgitAuctionChatHost` 的 XML 註解（`AgitAuctionPanel.cs:559–563`）還寫著「產品宿主沒有實作」，不在 `Submit` 範圍內，所以沒改，留給下一棒順手改。

## 未驗證什麼

- 真機：Ok 送出後窗馬上關；低於最小價按 Ok，聊天欄出現 4381（參考客戶端字串「輸入金額需要比目前競標價高」），顏色是 palette[24]。palette[24] 實際是什麼顏色，取決於引擎聊天欄的色表對應（與信件結果同一格）。
- 關窗後焦點：`Submit` 拿不到 `IUiCommands`，所以 Ok／Enter 關窗時沒有呼叫 `ReturnGameFocus()`（Cancel／Esc 有）。產品宿主的 `Bid` 會先 `form.ClearInput(...)`；關著的窗 `Key` 回 false，按鍵會往下傳。真機確認一下關窗後鍵盤有沒有回到遊戲。
- 產品端非同步送出：`Bid` 是非同步送出，送出套用（`AmountSent` 清掉 S136）之前有極短的空檔；這段時間原生窗已經關了，`SyncAmountPrompt` 理論上可能讓 WinForms 數字列閃一下（只在 NPC 面板開著時）。靜態推測，沒有實測。
- 原版關窗呼叫 vtable `+0x8C(0,1,1)` 的三個參數意義沒追；引擎用一般的 `Close()`。

## 產出路徑

- `engine/source/RingWindows.cs`（只改 `AgitAuctionHost`）
- `engine/source/NativeUi/AgitAuctionPanel.cs`（只改 `Submit` 與它的註解）
- `engine/source/RingAgitFollowupTests.cs`（只改 Enter＝Ok）
- `engine/source/BidStepTests.cs`（見「範圍外的改動」）
- `engine/source/BidCloseTests.cs`（新檔）
- `docs/reports/issue-986.md`（本檔）

## 給下一棒的提示

1. gap-matrix 下一輪：4:AgitAuctionUI 的兩個 K5 子項（4381 聊天欄、Ok 後關窗）合併後可以改回「已有」；按住的速度仍是推測（不擋）。
2. `smoke-m7.md` SM29：「4381 不會出現在聊天欄」「要等伺服器回應才關」這兩點已知差異，合併後要改成正向檢查：送出後窗馬上關、低於最小價聊天欄出現 4381。
3. 順手修 `IAgitAuctionChatHost` 過時的註解（見上）。
4. 真機（Codex／dot，P24Lab 測試角色）：盟屋拍賣 → 出價窗，輸入低於目前價按 Ok（聊天欄 4381、窗不關）→ 輸入合法價按 Ok（窗馬上關、伺服器回應照常）→ 確認關窗後鍵盤回到遊戲。
