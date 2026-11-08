# Issue #437／T167：光源圖與雨雪圖來源

## 做了什麼

- 找到 `Sprite.pak` 的 `Source.lmp` 與 `924.img`～`927.img`，在參考文件第 3.1 節記錄完整 SHA-256、IDX 序號與 File Offset、PAK File Offset、尺寸及讀取方式。
- 沿用 #429 的 AES 解密函式，重新由 TW16 靜態核對，解出 `Tile.pak/SurfList.xml` 並解析 6653 個唯一 Surf ID。明文只放在本 worktree 的 `scratch/issue-437/`。
- 更正 T162：TW16 機器碼使用 32-bit `924..927`；負值 `-100..-97` 是 IDA 誤推 `char` 所致。TW13 與既有 LiTo OEP 快照也讀相同正值 ID。
- 光源為 16 槽／8 組非空像素區，第 15 槽空、第 0 槽非全黑；雪花與雨層 2／3 是原生程序圖樣，不存在待取出的雪圖。補上給引擎的唯讀記憶體載入契約。

## 怎麼驗證

- `t167_resources.py` 實讀 18 個索引，六個目標名稱各一筆；串流計算四個容器完整雜湊，核對六個 payload、所有 LMP 邊界及 IMG 長度。`Sprite` 容器 SHA 與現有 `GroundSpriteReader` pin 相同。
- `t167_static.py` 鎖定 TW16、TW13、LiTo 快照完整 SHA，核對九個指令窗口及 VA／FO；不依賴 IDA 偽碼中的參數型別。
- IDA 7.7／Hex-Rays 對 scratch IDB 副本反編譯，核對 XML 解密、Surf parser、光照印章與雨雪繪製鏈。沙箱內 IDA 因唯讀登錄存取被拒，改用已授權的沙箱外執行；所有輸出與 IDA 使用者環境仍導向 scratch。
- 原始雨圖已在 scratch 解碼檢視，沒有合成圖。重跑 metadata 與第一次逐字相同；`check-no-game-assets.py` 通過，`git diff --check` 通過。
- 本輪僅文件／研究腳本變更，沒有產品來源變更，未建置引擎；`docs/REPORT.md` 未改。

## 未驗證什麼

- 未啟動遊戲、伺服器或 DB，未附加活程序；靜態查證不代表實機畫面驗收。
- 未整合產品載入器；RGB555／其他目標像素格式轉換與透明合成、程序雪花像素操作仍由後續實作與真機比對確認。
- 沒有宣稱 LiTo 快照完整還原；只核對既有快照中的雨圖查詢窗口。

## 產出路徑

- `docs/reference/ambience-and-map-events.md` 第 3.1 節及相應更正。
- `docs/reference/INDEX.md`。
- `docs/reference/rea-scripts/t167_resources.py`、`t167_static.py`、`t167_targets.txt`。
- `docs/reports/issue-437.md`。
- 未追蹤證據：`scratch/issue-437/`（metadata、XML、原圖檢視），`scratch/rea/`（IDB 副本、靜態偽碼）。

## 給下一棒的提示

Claude 審查本 PR 後交使用者合併；T164／G2 實作者可解除缺圖阻塞，直接從 `GroundSpriteReader.ClientRoot` 指向的原客戶端唯讀取 `Source.lmp` 與 `924..927.img`，勿往 `engine/assets` junction 寫入。第 15 槽不可補造光圈；雪花的 2／3 是程序圖樣分支，不是像素直徑。現有 IDB 的 `char` 偽碼有誤，圖號以 `t167_static.py` 驗證的機器碼為準。產品整合後由 dot／使用者依既有授權做實機比對。
