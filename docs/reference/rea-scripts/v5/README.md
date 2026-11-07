# V5（Issue #460）IDA 唯讀查詢

結果寫在 `docs/reference/ui-layout.md` 的第 2 節「底列行為（V5）」與第 5 節。

## 準備

1. 建立工作目錄：
   - `scratch/rea/scripts/`：放 `ida_exec.py` 與 `run_exec.ps1`
   - `scratch/rea/ida/tw16.idb`：P01 IDB 的副本（SHA 見 `ui-layout.md` 開頭）
2. 準備 TW13：
   - 把 `TW13081901.unpacked.exe`（SHA `653da514…5c62a4`）複製到 `scratch/rea/tw13/tw13.exe`
   - 第一次執行 `tw13_t13a.py` 時，IDA 會自動分析並產生 `tw13.exe.idb`。本機約 3 分鐘，產生 32,999 個函式
3. 把要跑的查詢 `.py` 放到 `scratch/rea/`。

## 執行

```powershell
# TW16
scratch\rea\scripts\run_exec.ps1 -Query tw16_e28.py -Out e28 -Drive Q:

# TW13：第一次用 exe，之後改用 idb
scratch\rea\scripts\run_exec.ps1 -Query tw13_t13a.py -Out t13a -Db 'tw13\tw13.exe' -Drive Q: -TimeoutSec 3600
scratch\rea\scripts\run_exec.ps1 -Query tw13_t13b.py -Out t13b -Db 'tw13\tw13.exe.idb' -Drive Q:
```

- 輸出在 `scratch/rea/ida/<Out>/`：`log.txt` 加上各函式的 `.c` 偽碼。
- `-Drive` 指定的磁碟已被占用時，腳本會直接退出，不會卸載別人的對應。

## 檔案

| 檔案 | 內容 |
|---|---|
| `ida_exec.py` | 查詢的執行環境：提供 `P`、`dec`、`xr`、`find_all`、`dis` 這幾個工具函式 |
| `pe_sections.py` | 印出 PE 區段，用來換算 VA 與 File Offset |
| `tw16_q1.json` | 給舊版 `ui_ida_query.py` 用的字串查詢 |
| `tw16_e01`–`e30` | TW16 查詢：`Status800`、`StatusMiniMap`、widget 工廠、鍵盤、800×470 常數 |
| `tw13_t13a`–`t13j` | TW13 查詢：`Status800` 建立與切換（`sub_7923E0`）、鍵盤（`sub_4DEC00`／`sub_4D3910`）、action 表、小地圖模式 |
