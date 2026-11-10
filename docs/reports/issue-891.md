# Issue #891 報告：T383 R10-B HyperText 顏色修正（FU11 D1–D7）

依據：[`docs/reference/followups-11.md`](../reference/followups-11.md) §1、§2（T381／#886，PR #889）。

## 做了什麼

| # | 修正 | 位置 |
|---|---|---|
| D1 | `HyperTextColors.Order` 預設改 `Bgr`（COLORREF `0x00BBGGRR`）；連結常數 `0x00B0FFFF` 因此顯示成 RGB(FF,FF,B0) 淡黃。檔頭註解從「推測」改成「已驗證（FU11）」；順序測試跟著改 | `HyperTextLayout.cs`、`HyperTextLayoutTests.cs` `Colors` |
| D2 | Rich 列沒有 fg 的字改用 `0x00EEEDEE`＝RGB(EE,ED,EE)（`HyperTextColors.Default`／`HyperTextPanel.RichTextColor`）。舊的「連結一列一個」排法仍用 `TextColor`（白），沒有改 | `HyperTextPanel.PieceColor` |
| D3 | 新增 `HoverLink`：每個滑鼠訊息（Move／Down／Up／Wheel）先做命中測試（照原版 `sub_871260` 的做法，先清成 −1，打中才寫入），只認**文字**連結。同一個選項 id 的所有片段（包括換到下一列的部分）都畫 RGB(0A,F0,32)，蓋過 `<a>` 內的 `<font fg>`。圖片連結不變色，也沒有「已點過」的顏色。換頁、關窗、`Show(null)` 都會清掉 hover | `HyperTextPanel.HoverAt`、`Mouse`、`PieceColor` |
| D4 | 停用連結畫灰的邏輯保留，註明「引擎自訂」；停用連結也不會變成 hover 色 | `HyperTextPanel` 檔頭、`PieceColor` |
| D5 | 底線色固定 `0x00466E6E`＝RGB(6E,6E,46)，不再跟字色；帶 `link`／`action`／`url` 的 `<a>` 也有底線（`</a>` 之後就沒有）；底線 x 最多畫到本文左界＋198 | `HyperTextMarkup.Parse` 的 `Style()`、`PaintRich` |
| D6 | bg 改用整列高 `max(15, line.Height)`，同一列有圖時也一樣 | `PaintRich` |
| D7 | `Parse("")`（`fg=""`）改成回傳 0（黑）。`null`（沒有這個屬性）還是回傳 null，不改顏色。只有空白的值無效；超過 6 位數維持取低 24 位，註明是和原版不同的邊角（原版是 32 位元溢位） | `HyperTextColors.Parse` |
| 附帶 | `docs/reference/INDEX.md` 在 FU10 那列後面加 FU11 一列 | `INDEX.md` |

## 怎麼驗證

- **已驗證（本機編譯）**：`dotnet build engine/source/OfflineClient.csproj -c Release` 結果是 **0 錯誤**。4 個警告都在既有檔案（`HtmlActionTests.cs:108`、`MagicUi.cs:20`），本次改的檔案沒有新警告。
  - 這台機器的 WebView2 套件會觸發 NETSDK1130（#761）。照 #806 的做法，只在 `scratch/t891/no-winmd.targets` 放一個 MSBuild hook（拿掉 `.winmd`、改參考 net462 dll），用 `-p:CustomAfterMicrosoftCommonTargets` 掛上，並從原專案的 `engine/vendor` 唯讀還原到 `engine/source/obj/t891-pkgs`。沒有改 repo 的建置檔。`rebuild.py --build` 在 worktree 裡因為沒有 `vendor` 而失敗（環境問題，和本次改動無關）。
- **已驗證**：`python engine/tools/check-no-game-assets.py` → `pass`（4376 檔，0 錯誤）。
- **離線測試（已提交，交給 CI 執行；依本機限制，本機沒有執行任何編譯出來的程式）**：群組 `hypertext-layout`（`HyperTextLayoutTests.cs`）
  - `Colors`：預設 Bgr；連結＝FFFFB0；`00b4ff`→橘 RGB(FF,B4,00)、`ffffaf`→RGB(AF,FF,FF)（helpcom 的作者標註）；預設、hover、底線三個常數；`""`→0、`null`→null、只有空白→null、`12345678`→`0x345678`；`<font fg="">`→0。
  - `Styles`：`<a action|link|url>` 有底線，沒有屬性的 `<a>` 和 `</a>` 之後沒有。
  - `PanelRich`：連結底線像素＝RGB(6E,6E,46)；bg `0000ff`→紅（COLORREF）；40 px 圖列的 bg 在第 30 px 仍有底色。
  - `PanelHover`（新增）：預設字色 EEEDEE；hover 到文字連結時變色、移開恢復；停用連結不 hover、維持灰；圖片連結不 hover；移出本文區清除；跨列連結兩段一起變色；hover 蓋過 `<a>` 內的 `font fg`；換頁清除。
- CI 結果請看 PR 上的檢查。

## 未驗證什麼

- **待驗證（真機 M34，交給 Codex／dot）**：GDI 畫好的 DC 貼到畫面時有沒有再換通道；hover 的實際觀感。核對步驟見 FU11 §3：
  - `helpcom`：「橘色」應該是橘、「亮藍色字體」淡青藍、「黃色字體」淡黃。
  - `helpcon`：`fg=ffffaf bg=ffffff` 應該是淡青藍字加白底，白底占整列高。
  - NPC 頁的 `<a action>`：淡黃字、RGB(6E,6E,46) 底線，滑鼠移上去變亮綠。
- **推測**：沒有圖、改成文字標籤顯示的可點圖（引擎自訂）仍當作圖片連結，所以不會變 hover 色。
- 原版的特殊模式（FU11 §1 #14，名單比對綠／灰）和 `+17` 實心框模式（#11）沒有做（HyperText 頁不會走到，或用在哪一頁還沒查）。
- 本次沒有宣稱任何資源「沒有」，所以沒有做 PakViewer 查詢。

## 產出路徑

- `engine/source/NativeUi/HyperTextLayout.cs`、`engine/source/NativeUi/HyperTextPanel.cs`、`engine/source/NativeUi/HyperTextLayoutTests.cs`
- `docs/reference/INDEX.md`（加一列）
- `docs/reports/issue-891.md`（本檔）

## 給下一棒的提示

- `HyperTextColors.Order` 還是可以設定；如果 M34 真機發現畫面又換了一次通道，只要改這一個值。底線色、預設色、hover 色也都經過 `Decode`，會一起翻轉。
- hover 的狀態只靠 `HyperTextPanel.Mouse` 收到的事件更新。如果 UiRoot 在游標離開窗後就不再把 Move 送進來，hover 可能留在最後一個連結上，真機要注意看。
- 舊排法（Link 列）的顏色（調色盤黃、白、灰）完全沒有動。
