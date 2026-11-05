# Issue #34 — T041 選用的客戶端封包追蹤

## 做了什麼

- 新增 `engine/source/PacketTrace.cs`。僅在程序首次使用追蹤時讀到非空的 `TW_PACKET_TRACE` 才建立追蹤器；未設定時無檔案、背景寫入工作或退出事件。相對路徑以產品執行檔向上找到的 repo 根解析，不受 launcher 工作目錄為 `engine/` 影響。
- 背景解析路徑，僅允許 repo 的 `scratch/` 子路徑，拒絕越界、同名前綴、junction／symlink、NTFS alternate data stream。只用 `CreateNew` 建檔，既有檔案與硬連結不開啟、不覆寫；每次補測須用新檔名。
- JSONL 固定只有 `utc`、`dir`、`op`、`len`、`seq` 五欄。UTC 精確到毫秒；opcode 是首 byte 的十進位；長度是加密前／解密後長度（含既有協定 padding）；出站序號為 null，入站保留既有 sequence。空封包沒有 opcode，因此不產生紀錄。
- 容量 4096 的佇列只存時間、方向、opcode、長度、序號，不保留封包陣列或任何 payload。呼叫端只用 `TryWrite`，不等待 I/O；背景分批寫入及 flush，佇列滿或寫入失敗會靜默停用。正常 `ProcessExit` 排空並 flush，等待上限 2 秒，避免故障裝置阻塞退出。
- `LoginConnection.cs` 只增添四個出站、兩個入站掛接；公告確認改用同一份局部明文陣列供追蹤與原本的 Transform。既有 `PacketReceived`／`WorldPacketSent` 事件、封包內容、加解密及狀態機維持原樣，沒有將既有事件接入新追蹤檔。
- 依既有 integration console 斷言方式新增無第三方套件的 `packet-trace-tests`，連結實際 `PacketTrace.cs`，加入 CI。測試程序不載入遊戲、不開 socket。
- 更新 dev lock：182 個來源／191 個 pinned 輸入，僅更新 `LoginConnection.cs` pin 並新增 `PacketTrace.cs` pin。
- 修正 CI artifact 為 `dev-receipt`，來源為 dev receipt 與 `ci-dev/**`；BUILD 文件補上 M1 tree `20dedf6`（#19 建置、#23 真機使用）；CHECKLIST 補上 T042 追蹤設定與新檔名要求。

## 怎麼驗證（已驗證）

本機 Windows、.NET SDK 10.0.300；`python` 別名不可用，使用同一 Python 3.12 安裝的 `py -3.12 -B -X utf8`。所有本機測試與建置寫入本 worktree。

| 檢查 | 結果 |
|---|---|
| `dotnet run --project engine/_integration/dev/packet-trace-tests/PacketTraceChecks.csproj -c Release -p:UseSharedCompilation=false` | exit 0；7 組、36 個斷言全過 |
| 未設變數 | 無檔案、無子目錄，靜態 out／in 呼叫不產生輸出 |
| out／in 與假密碼 | 每筆剛好五欄、UTC 毫秒、opcode／len／seq 正確；無假密碼字串、完整 hex 或 base64；原封包未被修改 |
| 路徑與寫入失敗 | scratch 外、`..` 越界、同名前綴、非法字元、ADS、junction 均無越界檔；既有檔與非目錄父路徑保持原樣；失敗後大量呼叫不拋例外 |
| 並行與退出 | 4 個 producer 共 400 筆無遺失／重複；獨立 metadata-only 子程序從 `engine/` 工作目錄立即退出，實際 ProcessExit 仍 flush 兩筆 |
| `engine/_integration/dev/rebuild.py --update-lock` | exit 0；182 來源／191 輸入 |
| `engine/_integration/dev/rebuild.py` | exit 0；唯讀核驗成功 |
| `engine/_integration/dev/test_rebuild_paths.py` | exit 0；既有 7 項測試全過 |
| `engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config --write-receipt --output scratch/issue-34-build` | exit 0；產品與 launcher 各 0 警告、0 錯誤 |
| `engine/_integration/dev/rebuild.py --verify --output scratch/issue-34-verify` | exit 0；DLL／manifest 與 receipt 一致 |
| `scratch/issue-34-audit.py` | 移除六個新增呼叫並還原公告局部變數後，LoginConnection 與修改前完全相同；lock 只含預期兩檔差異 |
| 禁止修改範圍、`git diff --check` | 通過；r35、releases、正式入口、project-manifest 與 docs/REPORT.md 無差異 |

SHA-256：

- `PacketTrace.cs`：`f5b8b0f0af0a455bd75db265cb3995dd2508a095c8581f0a6449f5fbd68f19dd`
- `LoginConnection.cs`：`2239e0918b0c2ae94a29debcd0ca5428a44d1f95d128f469c9c1053616dd9cfe`
- dev lock：`e533ff6178d0a10e68101afa2f7a77f5b65e4d8cd4595f329ac34a891a665d4a`
- 本機產品 DLL：`913fcbb39ef9eea4feeea4071a3a445f5ed379128c30d880209611b03ce2c72a`

CI 與 artifact：待推送並建立 PR 後補上實際結果。

## 未驗證什麼

- 未啟動遊戲、伺服器或 DB；未連正式服務，未執行真機登入／移動／戰鬥／技能／拾取。單元測試的假封包只驗追蹤器，不是遊戲功能證據。
- 未執行 launcher check-only 或原生畫面；本 worktree 未建立素材 junction。r35 與正式入口未改動。
- 強制終止、斷電或 I/O 卡住超過退出等待上限時，尾端追蹤不保證完整；佇列滿或 I/O 失敗停用後不自動重啟。無追蹤紀錄本身不能證明遊戲沒有送包。
- 出站紀錄位於 Transform／WriteAsync 前，表示嘗試送出，不能單憑該列證明伺服器收到或接受；須配合同時間的入站與畫面判讀。

## 產出路徑

- `engine/source/PacketTrace.cs`、`engine/source/LoginConnection.cs`
- `engine/_integration/dev/packet-trace-tests/PacketTraceChecks.csproj`、`Program.cs`
- `engine/_integration/dev/rebuild-lock.json`
- `.github/workflows/build.yml`
- `docs/BUILD.md`、`docs/native-baseline/CHECKLIST.md`、`docs/reports/issue-34.md`
- 本機不入 git 的證據：`scratch/issue-34-build/`（含 receipt）、`scratch/issue-34-verify/`、`scratch/issue-34-audit.json`。單元測試暫存位於本 repo 的 `scratch/packet-trace-checks/`，測完清除。

## 給下一棒的提示

- 分支 `task/34`；Claude 審查後由使用者合併。調度器安排 T042 局部真機補測 04／05、06、07 技能 4、10 拾取。
- 使用包含 T041 的 dev 建置；啟動前設定 `$env:TW_PACKET_TRACE='scratch\packet-trace\04.jsonl'`，每次另取尚不存在的檔名。相對路徑解析到執行產品所在 repo；凍結 r35 不會讀此變數。
- 正常退出後收集 JSONL，配合時間與畫面核對；步驟可分次啟動，各保留獨立檔案。補測結束用 `Remove-Item Env:TW_PACKET_TRACE` 清除設定。
- r35 核驗仍須在 main `20dedf6` 對應 worktree 執行，不要求現行 dev tree 符合 r35 lock。
