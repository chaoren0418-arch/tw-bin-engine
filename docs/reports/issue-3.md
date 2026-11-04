# Issue #3 — T020 本機核驗 r35 來源鎖定

階段：test；第 2 次嘗試。執行時間：2026-11-04T13:06:15.927819+08:00。

## 做了什麼

- 已讀取專案根目錄 AGENTS.md；原本不存在 docs/REPORT.md，本輪新增該檔及要求的兩節。
- 本機原先停在初始化提交 1e47e415，缺少 r35 腳本；fetch 後從 origin/main 的 39de4e9b13f2efdc6eab62652189a405597bcf31 建立 task/3，核驗該版本的本機來源。
- 執行指定預設唯讀命令，未加 --build、--verify 或 --write-receipt。沒有修改 lock 或引擎程式碼。
- 本輪只新增 docs/reports/issue-3.md 與 docs/REPORT.md。

## 怎麼驗證／已驗證

工作目錄：D:\lineage開發\TW1608262200研究。

```text
python -B -X utf8 engine/_integration/r35/rebuild.py
```

結論：**通過**，退出碼 **0**。181 個來源檔全部命中；含建置輸入共 190 個 pinned files 全部通過檔案存在、大小及 SHA-256 核驗。SHA 不符檔名：**無**。

腳本逐一檢查 lock 中的檔案，通過後才輸出成功 JSON；sourceCount 亦核對為 181。

### 完整標準輸出（stdout）

```text
{"status": "r35-inputs-verified", "sourceFiles": 181, "pinnedFiles": 190, "writes": false}
```

### 完整標準錯誤（stderr）

空（0 bytes）。

### 來源識別

- engine/_integration/r35/rebuild-lock.json SHA-256：`36b3e6991a69d5b06b8978022af61a8593dc5da41de84ecc50b52a1249e6794d`
- engine/_integration/r35/rebuild.py SHA-256：`f73130508b48a19758745efc50470c236a50c96b877df9b7ca6c677c375fd95f`
- 核驗基準提交：`39de4e9b13f2efdc6eab62652189a405597bcf31`。

### 執行環境處理

隔離命令程序初始化失敗，改用獲准的直接命令執行。Git 擁有者檢查以單次 `-c safe.directory=...` 處理，未修改全域 Git 設定。原 PATH 找不到 python；透過既有 py launcher 查到 Python 3.12，僅在本次程序 PATH 加入既有安裝目錄後，以 subprocess 完整擷取指定命令的 stdout、stderr 及退出碼。未安裝任何軟體。前置命令與引號處理錯誤不屬於核驗結果。

## 未驗證什麼／推測／待驗證

- 未執行建置、產物核驗或原生實機驗收；來源鎖定通過不代表完整遊戲流程通過。
- 未啟動遊戲、未連線正式服務或資料庫、未讀取機密設定、未操作 releases/、未複製 pak/map/sprite、未修改硬連結。
- 推測：無；本結論只涵蓋本次來源鎖定檢查。

## 產出路徑

- docs/reports/issue-3.md（本報告，含完整核驗輸出）。
- docs/REPORT.md（本輪變更與下一步各一條 Issue #3 紀錄）。
- scratch/issue-3/verification.json 為本機暫存原始輸出，不納入提交。

## 給下一棒的提示

請調度器／Claude 審查 task/3 的兩份文件與來源核驗結果，確認 Issue #3 驗收。TASKS.md 既有 T020 指 NPC 服務，與本次 Issue #3 標題重名；本輪依 Issue #3 指定範圍執行，不改 TASKS.md。後續建置可另派任務；原生實機測試由使用者執行。
