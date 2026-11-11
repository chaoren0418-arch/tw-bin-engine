# Issue #1010 報告：T439 Q9-D HTML `<var>` 選項值（3.81 範圍）

依據：`docs/reference/followups-17.md` §3.4、§7（T422／#966）。分支 `task/1010`。

## 做了什麼

- `engine/source/NativeUi/HyperTextLayout.cs`：新增 `HyperTextOptionVars`。`<var src="#N">` 的 N 在 1002–1029、而且引擎已有對應設定時，輸出 §3.4 的字串：
  - 開關：567「開」／568「關」；1019 用 573「可使用」／574「無法使用」。
  - 字串從字串表（`ServerMessageText.Table`）讀；沒有表時用上面這幾個字。表太短時是空字串，和 762／763 的處理一樣。
  - `HyperTextClock` 加了 `Session`（最後收到 S123／S8 的 session），世界相關的值從這裡讀。
- `HyperTextMarkup.Parse` 與 `NpcTemplates.Render`（只改 var N≥1000 那一行）都先問 `HyperTextOptionVars.Value(N)`。有值就輸出；沒有值就照舊（有參數讀參數，否則「缺少參數」）。兩邊的純文字與選項數仍然一致。
- 新的離線測試 `engine/source/NativeUi/HyperTextOptionVarTests.cs`，群組 `hypertext-option-var`，用 `[WorldModuleTest]` 自動發現。

| N | 動作 | 引擎設定 | 原版變數 | 字串 | 依據 |
|---|---|---|---|---|---|
| 1002 | getall | `WorldSession.PickupAll` | `byte_135C1E4` | 567／568 | 已驗證（world-objects.md） |
| 1003 | bubbleon | `HeadBubbleOptions.BubbleFrames` | `byte_13AAF28` | 567／568 | 已驗證（FU16） |
| 1005 | dowhisper | `WorldSession.ChatChannels.Whisper` | `byte_135C1E6` | 567／568 | 已驗證（FU7§1.4） |
| 1019 | accepttrade | `WorldSession.TradeAskWindow` | `byte_136E99D` | 573／574 | 已驗證（FU7§1.1） |
| 1020 | dotradechat | `WorldSession.ChatChannels.Trade` | `unk_1656980` | 567／568 | 已驗證（FU7§1.4） |
| 1023 | showhpmp | `HpMpSettings.ShowCharacterHpMp` | `byte_138BE7C` | 567／568 | 已驗證（ui-layout.md） |
| 1024 | showfloathpmp | `HpMpSettings.Show` | `byte_138BE7D` | 567／568 | 變數對應已驗證；它怎麼控制浮動窗仍是 ui-layout.md 的待驗證 |
| 1027 | showMargetMsg | `HeadBubbleOptions.ShopMessages` | `byte_136E99F` | 567／568 | 已驗證（FU16） |

以下維持「缺少參數」：
- 1004 dochat：`unk_1656948` 沒追到。推測是全體頻道，但沒有證據，所以不接。
- 1009：引擎的語言過濾一律開著，沒有開關。
- 1000／1001／1006–1008／1010–1018／1021／1022／1025／1026／1028／1029：引擎沒有對應設定。
- 1030 以上與 2001 以上是 TW16 專有，不做。

## 怎麼驗證

- `dotnet build engine/source/OfflineClient.csproj -c Release`：建置成功，0 個錯誤。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：`status: pass`。
- 依本機限制，沒有在本機執行任何編譯出來的程式；`--module-tests`（含新群組 `hypertext-option-var` 與既有群組）交給 PR 的 GitHub Actions CI 跑。
- 測試涵蓋：
  - 預設值。
  - 改設定後跟著變（頻道、交易旗標、全部拾取、泡泡話框、商店名稱）。
  - HP/MP 有來源和沒有來源兩種情況。
  - 沒有對應設定的號碼、沒有 session 時，都是缺少參數。
  - 字串表讀取，以及表太短的情況。
  - NpcTemplates 與 HyperTextMarkup 輸出一致，`<a>` 裡的值會成為選項文字。
  - 參數再多也不會蓋過選項值。
- 資源：顯示用的字串在執行期從字串表讀取，repo 裡沒有放任何資源，也沒有宣稱缺資源，所以沒有跑 PakViewer 查詢。

## 未驗證什麼

- **待驗證（重要）**：產品裡 1023／1024 目前會顯示「缺少參數」。HP/MP 設定只存在原生 UI 的 `HpMpPanel` 執行個體裡，可改的檔案範圍內拿不到 UiRoot，`HyperTextOptionVars.HpMp` 還沒有接上。對應與輸出邏輯已經做好，也有測試。
- 推測：世界值取「最後收到 S123／S8 的 session」，NPC 對話使用的 session 應該就是它（單一連線）。
- 真機再確認：在遊戲內打開選項頁（option12／optiong 等），看顯示的開／關與實際設定是否一致。這要開遊戲，交給 Codex。
- 原版 1002–1029 的 3.81 全域變數只靠 case 編號對應（§3.4 自己的限制），各變數的寫入者沒有逐一追。

## 產出路徑

- `engine/source/NativeUi/HyperTextLayout.cs`
- `engine/source/NpcTemplates.cs`
- `engine/source/NativeUi/HyperTextOptionVarTests.cs`
- `docs/reports/issue-1010.md`

## 給下一棒的提示

- 接上 1023／1024 只要一行：在 `NativeUi/UiClientBridge.cs` 的 `ApplyNativeUiStart` 成功分支加 `HyperTextOptionVars.HpMp=()=>OptionPanel.HpMpWindow(root)?.Settings;`，需要另開任務把該檔放進可改範圍。
- 1004 要先追 `unk_1656948`（`sub_F400A0` 的另一個頻道物件），確認是全體頻道後，加到 `Supported` 並對到 `ChatChannels.World`。
- 1025 透明度（893–896）、1028 組隊分配等要等引擎有對應設定，再加進 `HyperTextOptionVars.Flag` 的 switch 與 `Supported`（陣列必須遞增）。
