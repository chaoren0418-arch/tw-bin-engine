# V4（Issue #459）查證腳本

結果寫在 `docs/reference/ui-layout.md` 第 1 節「框架層細節（V4）」。

## IDA 唯讀查詢

執行器沿用 V5 的 `ida_exec.py`／`run_exec.ps1`（見 [`../v5/README.md`](../v5/README.md)）。

1. 準備目錄：
   - 把 `../v5/ida_exec.py`、`../v5/run_exec.ps1` 複製到 `scratch/rea/scripts/`
   - 把 P01 IDB 複製成 `scratch/rea/ida/tw16.idb`（SHA-256 `3a0bf206…7e82f`）
   - TW13 IDB 放在 `scratch/rea/tw13/tw13.exe.idb`，產生方式見 V5 README
2. 把要跑的 `.py` 複製到 `scratch/rea/`，執行：

```powershell
powershell -ExecutionPolicy Bypass -File scratch\rea\scripts\run_exec.ps1 -Query tw16_q07.py -Out q07 -Drive R:
powershell -ExecutionPolicy Bypass -File scratch\rea\scripts\run_exec.ps1 -Query tw13_t13h.py -Out t13h -Db 'tw13\tw13.exe.idb' -Drive R:
```

3. 輸出在 `scratch/rea/ida/<Out>/`：`log.txt` 加上各函式的 `.c` 偽碼。

| 檔案 | 查的是 |
|---|---|
| `tw16_q01`–`q04` | `.img`／`SurfList`／字型／XML 相關字串、`sub_B3D390` 與呼叫者、UI XML 檔名表 |
| `tw16_q05`–`q07` | 封存檔管理：`FileManager`、`HashFileManager`、`LocalFileFormatter`／`LocalFileNonDashFormatter` 的 vtable 與 `sub_52D8A0` 反組譯 |
| `tw16_q08`–`q14` | `.img` 旗標與透明色：`Surf` 類別、`GetSurf`（`sub_E538D0`）、`Surf::Draw`（`sub_E539E0`）、blit 函式 |
| `tw16_q15`–`q16` | `surfNumber` 解析（`atoi`） |
| `tw16_q17`–`q18` | 字型：`CreateFontA` 呼叫者與文字繪製 |
| `tw16_q19` | UI XML 第二條載入路徑（`sub_A97450`→expat） |
| `tw13_t13a`–`t13j` | TW13 按鈕類別 `LImageButton`／`LImageButton3Stage` 與 `Status800` 建構（`sub_77F6B0`） |

`tw13_t13h.py` 會在 IDB 副本上呼叫 `add_func(0x50CA20)`，因為自動分析沒有把那段程式碼建成函式。這只會改 `scratch/` 裡的副本，不會改原始 IDB。

## 資料核對（不需要 IDA）

```powershell
py -3 -I docs/reference/rea-scripts/v4/img_check.py "D:\lineage開發\0-客戶端" 5726 3168
py -3 -I docs/reference/rea-scripts/v4/img_csuffix.py "D:\lineage開發\0-客戶端" 3168 2273
```

- `img_check.py` 檢查四件事：
  - `SpriteNN` 的雜湊規則
  - 同名 `.img` 會先讀到哪一份
  - 旗標值的分布
  - 圖中有沒有和透明色相同的像素
- `img_csuffix.py` 統計 `<n>c.img` 在地化圖：共幾張、落在哪些編號、和 `<n>.img` 有沒有不同。

兩支都只讀 `Sprite*.idx/pak`，不寫任何檔案。
