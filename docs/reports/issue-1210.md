# #1210：f1186-smoke-tooling 測試工具補強

結論：部分通過（✅ 9／❌ 0／⚠️ 2）。只修改 `work/f1118-uia-path/` 與本報告，未改產品、rebuild-lock 或 docs/REPORT.md；不做任何 SM 判定。

## 做了什麼

- 新增 `SmokeTools.ps1`：聊天草稿清除與 GDI 正向驗證、指定 C／S opcode 觀察、objid 投影與 targetId 讀回、共享讀取／保存 JSON、等待 closed 再確認程序退出。
- 新增 `SmokeLoopback.cs`：WASAPI endpoint loopback，輸出 WAV、RMS／Peak、格式／樣本數；自家播放 session 與其他播放程序檢查，不改音訊裝置或系統設定。
- `Test-NativeClient.ps1` 改用共享讀檔與關窗等待；存活程序才做逾時清理。`UiaGdi.ps1` 修正日誌複製遇到字典 `Keys` 欄位遮蔽集合屬性，保留 Keys／VirtualKey／Modifiers。
- 新增 `Test-SmokeTools.ps1`（8 個離線契約檢查），README 的 **f1186-retest** 段落提供每個新函式的用法與限制。

## 逐項自測

證據根：`D:\lineage開發\TW1608262200研究\scratch\wt\task-1210\scratch\issue-1210`。下表皆相對此根；圖片、log、錄音、DB 副本不進 git。

| 編號 | 結果 | 實際觀察 | 重現步驟 | 證據路徑 |
|---|---|---|---|---|
| R01 | ✅ | 本機 product／launcher 建置成功；工具 interop 編譯與 8 個契約檢查通過；同步 main 後重建亦通過。 | `engine/build-product.ps1 -ConfigFile .github/nuget.ci.config`；`Test-SmokeTools.ps1`。 | `build.log`、`build-after-sync.log`、`contracts/results.json` |
| R02 | ✅ | 先留 `old /rank /buddy` 草稿；106 Backspace 後白字像素 0，W 探針 24，刪除後 0；送出的輸入截圖只剩 `/buddy`。 | 點輸入列中央→輸入草稿→`Clear-SmokeChat`；矩形 (143,586,338,12)。 | `client-A/dirty-draft-calibrated.png`、`buddy-calibrated-{empty,probe,verified-empty,typed}.png`、`buddy-calibrated-clear.json` |
| R03 | ✅ | `/buddy` 觀察到 C4→S39；`/rank` 觀察到 C125→S39。只證明送出與符合 opcode 的回包，不判好友／排名功能。 | `Send-SmokeCommand` 分別指定 C4／C125、S39；一次只送一項。 | `client-A/buddy-calibrated-send.json`、`rank-send.json`、`packet-trace.jsonl` |
| R04 | ✅ | `/loc` 缺收件人，完整觀察窗無 C254，回 `send-not-observed`；未誤報成伺服器未回。 | 清空→送 `/loc`，指定 C254／S71、等待 4 秒。 | `client-A/loc-missing-argument-send.json`、`loc-missing-argument-typed.png` |
| R05 | ✅ | 指定 NPC objid 2000039466，從 (32645,32951) 相對角色 (32648,32948) 算出 (400,372)，新狀態 targetId=2000039466。 | 關聊天列→`Select-SmokeObject`，預設 Zoom=1.5、BodyOffset=(0,-24)。 | `client-A/select-pandora-select.json`、`select-pandora-before.png`、`select-pandora-after.png` |
| R06 | ⚠️ | 雙角色同時進世界，但法師 (32648,32948)、王子 (32580,32974) 不相鄰；王子普通步行至 (32591,32970) 受牆阻擋，未取得 PC 點選讀回。 | 測試帳號 p24svc01／p241db6e；普通畫布地面點選準備，不設 GM、不瞬移。 | `positions.tsv`、`client-B/world.png`、`approach.json`、`approach-final.png` |
| R07 | ⚠️ | 同一預設輸出裝置已有 Chrome PID 8928 active render session；loopback 在建立 WAV 前拒錄，Frames=0、HasWaveform=false。沒有客戶端波形可判。 | 僅 A 客戶端播放時呼叫 `Record-SmokeLoopback -Seconds 3`；不停止其他人的程序。 | `client-A/loopback.wav.json`（無 WAV） |
| R08 | ✅ | 真客戶端連續讀 100 次，revision 674→680，維持 InWorld；獨立壓力測試 250 次截斷寫入／60 次解析，sharing failure=0。 | `Read-SmokeJson`／`Copy-SmokeState`，全部採 ReadWrite\|Delete；另做半列及追蹤改寫拒絕。 | `client-A/live-read.json`、`contracts/results.json` |
| R09 | ✅ | A PID 1076、B PID 3372 都有 closed、正常退出，KeyError=null；沒有 forced.json。 | `Close-SmokeClient` 等 closed＋退出，再結束各包裝腳本。 | `client-A/close.json`、`client-B/close.json`、各自 `window-events.json` |
| R10 | ✅ | 自啟 Java／MySQL 已退出，mysqladmin=0；42061／43324 無監聽，24 個角色 GM=0／OnlineStatus=0；原 DB 1722 檔 SHA 未變。 | 客戶端先關→自啟 Java 依 PID 結束→查角色→mysqladmin 正常停庫；比對原始冷備份。 | `runtime-shutdown.json`、`characters-final.tsv`、`source-readonly.json`、`final-check.json` |
| R11 | ✅ | PR #1224 工具提交 `15f3b0cb` 的 dotnet、map-continuity、npc-offline 全通過。 | 推送 task/1210 後以 `gh pr checks 1224` 核對提交與檢查。 | [建置與地圖 CI](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/43574773399)、[NPC CI](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/43574773417) |

## 證據識別

完整 80 檔清單：`scratch/issue-1210/evidence-manifest.json`，SHA-256：`b5e7c83ea4706699172807031b155f4c4372b17cdeb9eb615aed7eb6f589308f`。其中包含所有主要 PNG、日誌與工具來源 SHA；未收錄秘密設定內容。重要結果如下：

| 檔案（client-A/） | SHA-256 |
|---|---|
| `buddy-calibrated-clear.json` | `83c0295c645cd9e56fc29cab713b6a328a74a5b2565417b565e7e2267f61009d` |
| `buddy-calibrated-send.json` | `1991456011cfb6741d1eee3532b8689759b817634486775dd838736c5ed68411` |
| `rank-send.json` | `5f1eaba4550521143fab2d12701bb8acaabd809d854ba6a39bdb9dd1201482ad` |
| `loc-missing-argument-send.json` | `5c9502227876da91cdd4c98e8ae9df89809602286ed93373845aed3313adf8e8` |
| `select-pandora-select.json` | `2d5b572867e93a717674341ebd8f523cc9c28e9b35a0d550388fa5373f6a4b92` |
| `loopback.wav.json` | `26c90f8de1f23a87318bd652010b667ab862349bcf520e5790e8c8b051654009` |
| `live-read.json` | `ceddba354ca7bb6e690cf19722f413f27f8a4575c3e2400b65297e1f4b56b3a4` |
| `close.json` | `f952c249a5302e5d8eb21b1847c4079fc34deaa7c7d38afaa65eba6c06ae3b67` |

後續同步與收尾證據（相對證據根，不在上述原始 80 檔清單）：

| 檔案 | SHA-256 |
|---|---|
| `build-after-sync.log` | `ccd87abec3853d2e678a309d85a87954fc76e06978b612a2c883d55cd056f2b2` |
| `final-check.json` | `0ae90ac217921e966a51df7eff3806da1e756367839e5ba533c1c9370176c6ff` |

## 環境、備份與收尾

- 實機來源：main `b3888c8e01fc9039bc0916a7b1d782369ce0b7da`；Windows 10 build 19045、DPI 192、800×600 工作區。product DLL SHA=`2a96bc10e5ec58d959b45d870c656fd5faf2fce020c6419115a4ad89f6055182`；audio=`4c4aa97837e7c4e610d3e0b75eb2b619faf41508096435e37c635c2cec456473`；launcher=`c700cfaafbce6a912944766b9d9f0469a610a5363472b4bd4811d64938dcdcd4`。
- 收尾同步 main `51c11852`，合併提交 `15f3b0cb`；重建 product DLL SHA=`e643bb88dac9f9e45f30fb22c126cd64707645a39304e8a8de46a40b9981a21f`，audio／launcher 同上。此重建僅核驗同步後可建置，未重跑已通過的實機列；實機證據仍對應前列來源與 DLL。PR 相對 main 僅 7 檔，沒有產品／lock 修改。
- 開始時已確認 p24lab lease holder=#1210、到期 2026-11-14T09:10:24Z。只使用 42061／43324；未連正式 2000／3306，未使用 LiTo 環境。
- 為遵守原專案唯讀，在本 worktree `scratch/issue-1210/cold-backup/db-data` 完整冷備份並逐檔核對，再複製到 `scratch/issue-1210/lab/db-data` 執行。1722 檔；manifest、檔案數／bytes 見 `preparation.json`。MySQL 的 datadir、tmpdir、secure-file-priv、log-error、pid-file 全指向副本；沿用既有 ASCII junction `D:/BinEngine` 的 worktree 路徑解決舊執行檔的中文路徑問題，沒有新增外部目錄。
- JAR／依賴／設定原樣複製到副本，未修改秘密設定；server maps 與 engine/assets 透過 junction 唯讀使用既有資源，未複製原客戶端 pak/map/sprite。最終原 DB 1722 檔與冷備份 SHA 全相符，server 設定副本／JAR 與原件一致。
- **沒有執行資料準備 UPDATE／INSERT／DELETE，也沒有設 GM**，因此沒有 GM 還原 SQL。遊戲登入／登出與普通步行只影響實驗副本；角色位置的實際觀察見 R06。沒有主張副本 DB 逐 byte 還原。
- Java 依本輪自啟 PID 12624 結束；MySQL listener PID 19252 以 mysqladmin 正常關閉；A／B 客戶端與包裝程序均退出，查無本輪 WebView 子程序殘留。未移除 worktree、未遞迴刪除任何目錄、未更動登錄檔／服務／權限。

## 已驗證、推測與未驗證

**已驗證**：上表 ✅ 的範圍。最初工具點輸入區左端會落在重疊的頻道按鈕，GDI 的 W 正向檢查因此拒絕送出；已把本工具改成點中央，保留失敗取證 `client-A/buddy-send.json`，產品未改。這也證明「SendInput 回傳成功」不能當聊天已開啟。共享讀取未讓本輪客戶端進 Faulted。

**推測／限制**：指定物件的預設身體偏移不是所有外型的通用命中點；相鄰 PC、移動目標、遮擋與模式消費點擊仍須依實際畫面驗證。封包追蹤只有 metadata，C／S opcode 與時間相符不證明 payload／因果。GDI 空白檢查依目前白字與版面。loopback 是 endpoint 混音，每約 20ms 檢查 session，並非程序隔離錄音；不能保證兩次輪詢間沒有其他聲音。

**未驗證**：R06 PC 點選、R07 實際 WAV／RMS／Peak 波形、指定秒數完整收音、靜音／音效 6016，以及全部 SM 功能；不以合成音訊或僅有 audio ready 快照冒充通過。

## 給下一棒的提示

1. 調度器／本機測試執行器只補 R06、R07；R01–R05、R08–R10 不必原樣重測。R06 承接 `f1186-smoke-fixtures` 的相鄰角色；本次唯讀使用的原環境座標不是 #1186 圖上的相鄰狀態。
2. R07 需要自家客戶端在指定預設 endpoint 的獨佔播放時段；若仍有其他人的 active session，記錄跳過，勿關他人程序或改系統設定。錄製成功後檢查 WAV／JSON 的實際 frames、CapturedSeconds、RMS／Peak，再判「工具取得波形」。
3. `f1186-retest` 直接依 README 新段落操作；SM22／35 的邀請選對象模式可能不更新 targetId，應另看該模式 C 封包及對方畫面，不能把普通選取契約套成 SM 通過。
4. 本 worktree 的 scratch 含冷備份、執行副本及 junction，交調度器管理；**不要遞迴刪除**。本 PR 不修改 docs/REPORT.md，由調度器合併後補索引。
