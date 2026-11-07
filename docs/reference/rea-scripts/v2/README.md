# V2（Issue #457）紅字與中央訊息佇列：唯讀查詢

結果寫在 `docs/reference/ui-layout.md` 第 2 節「紅字與畫面中央系統訊息（V2）」。

## 準備（全部在 `scratch/`，不進 git）

1. `scratch/rea/scripts/`：放本目錄的 `ida_exec.py`、`run_exec.ps1`（`ida_exec.py` 以 `utf-8-sig` 讀查詢檔，PowerShell 寫出的 BOM 不會出錯）。
2. `scratch/rea/ida/tw16.idb`：P01 `ida/03-ascii-isolated/TW1608262200.idb` 的副本（SHA `3a0bf206…bc17e82f`）。
3. `scratch/rea/bin/tw16.bin`：`P01-分析基準/input/TW1608262200.bin` 的副本（SHA `e12949cf…09641a2`，`fo.py` 用）。
4. 把要跑的 `r0N.py` 放到 `scratch/rea/`。

## 執行

```powershell
powershell -NoProfile -File scratch\rea\scripts\run_exec.ps1 -Query r01.py -Out r01 -Drive Q:
# 輸出：scratch\rea\ida\r01\log.txt 與各函式 .c
py -3 -I scratch\rea\fo.py                     # 區段、File Offset、調色盤 0–3
py -3 -I docs\reference\rea-scripts\v2\tbl.py scratch\rea\tbl.txt 553 554 1829 1006 1017
```

`-Drive` 指定的磁碟已被占用時，腳本直接退出，不會卸載別人的對應。

## 檔案

| 檔案 | 內容 |
|---|---|
| `r01.py` | 反編譯 `S_WARNING_CODE`／`S_MESSAGE_CODE`／`S_MESSAGE` 處理函式（`sub_94FB00`、`sub_942B40`、`sub_944F50`） |
| `r02.py` | `sub_F04180`（中央佇列入口）、`sub_F044A0`、`sub_A231C0`；`sub_F04180` 全部呼叫點與參數；`byte_1544388` 各格的 xref |
| `r03.py` | 繪製端：`sub_F18F40`、`sub_F0F0C0`（主畫面）、`sub_F067B0`、`sub_F12610`、`sub_F0A580`、`sub_94F100` 與反組譯 |
| `r04.py` | 文字函式 `sub_841770`、佇列 `sub_F188A0`／`sub_F191A0` 等、訊息組字 `sub_933850`、原點偏移 xref |
| `r05.py` | 佇列每筆繪製 `sub_F19680`、字寬 `sub_F18AB0` |
| `r06.py` | `\f` 色表基底、串列插入／刪除方向、計時器插入 `sub_A22830` |
| `r07.py` | 紅字迴圈與 `sub_94FB00` 內關鍵指令的精確地址、區段 |
| `fo.py` | 以 `nat.py` 的 `Img` 換算 File Offset 並讀調色盤（讀 `scratch/rea/bin/tw16.bin`，SHA 由 `Img` 核對；從專案根目錄執行） |
| `tbl.py` | 讀本機 `Text.pak` 的 `string-c.tbl`，輸出指定編號的原文到 `scratch/` |
