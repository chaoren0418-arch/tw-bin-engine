# Issue #886 T381：HyperText 顏色位元組順序與連結色（resource）

## 做了什麼
- IDA 7.7＋P01 IDB 副本（`scratch/t886/ida/`）唯讀反編譯 `sub_8676F0`（HTML 解析）、`sub_864530`（十六進位解析）、`sub_862970`（`CTextAttr` 預設）、`sub_866070`（GDI 繪字）、`sub_86E960`（bg）、`sub_871260`／`sub_871D60`（滑鼠命中與 hover）。
- PakViewer CLI 從 `0-客戶端/Text.idx` 解出 `helpcom-c.html`、`helpcon-c.html` 到 `scratch/`，比對作者寫的顏色名稱。
- 寫 `docs/reference/followups-11.md`（規則表、R9-F 差異、M34 截圖清單）；gap-matrix 15.11 的 M34 加註靜態結果。

## 結論（已驗證）
- fg／bg 字串照寫的順序轉成整數，直接當 GDI COLORREF（0x00BBGGRR）用：`fg=00b4ff` 顯示成橘色。
- 連結 `0x00B0FFFF`＝淡黃 RGB(FF,FF,B0)；hover `0x32F00A`＝亮綠 RGB(0A,F0,32)；預設字色 `0xEEEDEE`；沒有「已點過」色。
- bg 的範圍是那段字的寬 × 整列高；底線固定色 RGB(6E,6E,46)，帶 link／action／url 的 `<a>` 也有底線。

## 怎麼驗證
- 靜態分析＋頁面作者的顏色名稱（helpcom：「橘色」00b4ff、「黃色字體」afffff、「褐色」6ea0ba 只有 COLORREF 對得上）。沒有執行任何編譯產物，也沒有改產品程式。

## 未驗證
- 真機畫面（M34）：截 `helpcom`／`helpcon` 與一個 NPC 連結頁的 hover，確認最後貼到畫面時沒有再換通道。
- 連結編號兩套是否完全相同、特殊名單模式（`+662`）用在哪一頁。

## 產出路徑
- `docs/reference/followups-11.md`、`docs/reference/gap-matrix.md`（15.11 M34）、本報告。

## 給下一棒的提示
- R9-F 後續 code 任務：`HyperTextColors.Order` 預設改 `Bgr`、富文字預設色 EEEDEE、加 hover 色、底線色與連結底線、空字串 fg＝0（FU11§2 D1–D7）。
- `docs/reference/INDEX.md` 沒有加 FU11 列（避免和 T380 的 FU10 同時改衝突），合併後由調度器或下一輪補。
