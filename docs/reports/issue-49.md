# Issue #49 — 原服地面物品與背包靜態契約

## 結論

**本輪未完成，等待 P24Lab 既有憑證檔的使用授權。** 已解除前次 MySQL 啟動權限阻塞，但無密碼 root 連線被拒。未取得任何模板列，金幣 ground gfx 7／3247 尚不能裁定；`qualified=false`。本報告取代前次報告，歷史由 Git 保存。

**尚未完成清理：本輪啟動的 P24Lab mysqld PID 23540 正在 127.0.0.1:43324 監聽，未執行 SHUTDOWN。** 不可把未登入或未查模板誤記成資料庫已關閉。Java／遊戲未啟動。

## 做了什麼

- 已讀專案 `AGENTS.md` 與 `docs/REPORT.md`，後者未修改。一般命令工具在程序建立前出現 `helper_unknown_error: setup refresh had errors`；使用正式 `require_escalated` 審查後命令可執行，沒有修改 ACL 或沙箱設定。
- 已 fetch。舊 `task/49` 被 `scratch/wt/task-49` 佔用，確認其工作樹乾淨後 detach，保留原提交 `16fcf643e35ad01be9f69b948d7cac56fe55f62c`。於專案本體執行 `git checkout -B task/49 origin/main`，只帶回本任務四份產出。未動其他任務的未追蹤檔案。
- 啟動前 netstat 確認 43324／42061 無監聽；mysql.ini 僅核對非機密的 basedir／datadir／port／bind-address／tmpdir 等白名單欄位。`D:\BinEngine\environments\p24` 對應專案的 `environments/p24`。
- 使用既有 mysql.ini 與 Hidden 視窗啟動指定 mysqld；日誌顯示 2026-11-05 07:56:06 UTC ready for connections、port 43324、PID 23540。Start-Process 包裝 PID 是 12136。
- 僅嘗試無密碼 root 登入以 SELECT 三項身分；回傳 `ERROR 1045 (28000)`。SQL 未成功執行，不能聲稱 UUID／port／datadir 線上核驗通過。未查角色、帳號或模板表，未讀取機密設定／憑證內容。
- 因任務明示禁止讀取機密設定，已詢問是否允許**僅由 mysql.exe 使用既有 private-root.cnf 登入**，不顯示、複製或讀出帳密。此問題仍待答覆；不能自行視為同意，也未採用其他認證繞過方式。這不是自動批准審查拒絕，而是登入失敗後尚缺明確的憑證使用範圍。

## 已驗證：JAR 與映射補件

本次重新讀取授權 P24Lab `server/Server_Game.jar`，SHA-256：

`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`

與既有歷史來源相符。只定向擷取必要 class 並以 `javap -c -p` 反組譯至 `scratch/issue-49/static`，未執行伺服器類別。保留原有 18 段協定、10 段映射，新增 34 段內嵌摘錄，位於 `contract.json.supplementalJarEvidence`，每段含 class SHA、javap SHA、方法簽名、方法內 bytecode offset、原文與摘錄 SHA。

| 欄位／方法 | 本次核對 |
|---|---|
| grdgfx → set/getGroundGfxId → S87 writeH | getter 直接讀欄位；不可與 invgfx 混用。 |
| invgfx → set/getGfxId → instance.get_gfxid → S15 writeH | 背包 icon 對照成立。 |
| itemdesc_id → getPacketItemDescId | 一般非零值直接返回；80027–80030 及 desc=0 有其他分支，不一概視為直通。 |
| use_type → _useTypes → getPacketUseType | none=-1、normal=0、food=38、arrow=-2；負數取低 8 位，96/97/98 以上另有轉換。 |
| bless → toPacketBless | 0→1、1→0，其餘保留；S15 sealed 另加 128。 |
| ServerBasePacket writeC/H/D/S | 整數為小端序；writeS 使用執行期 charset 與 BAD_TEXT_LIST 過濾，再補 NUL。未讀取相關機密設定。 |
| ClientBasePacket readH/readD/readUnsigned | H=2 bytes、D=4 bytes；請求 count 是 Java signed int。 |
| 背包名稱 | instance 委派 L1ItemStatus；普通數量後綴 bootstrap #9 為 ` (count)`；10 億以上另走 RangeLong，不能照抄地面名稱規則。 |

所有 offset 是 JVM 方法內 bytecode offset，**不是 File Offset／RVA／VA**。新增的模板驗證與 S87／S15 驗證分支尚無真實模板可跑，不能視為已驗證該分支。模板具體值、storageCountMax、完整丟棄／裝備／綁定限制仍未知。

## 推測／靜態推導

- `fixtures.json` 保留 26 個語意案例，均加上 `derivation: static`。
- 新增 2 個 C112 payload：非 L1ItemInstance 拒絕、requested=11 超過 available=10 在 bytecode offset 376 返回。每例包含原始 payloadHex、解碼預期、拒絕預期、來源 pin 與適用前置。
- ObjectId 900001、座標 32768/32768 與 instance 狀態是明示的測試輸入，**不是資料庫資料或實際擷取**。只驗指定局部分支，不宣稱 handler 整體成功／交易正確。
- 數量請求最大正 Java int 2147483647、回覆顯示 clamp 2000000000；均不能替代未知的 storageCountMax。
- 文字 payload 模型只可在 ASCII 相容 charset／無命中過濾詞的前提使用；本輪兩個 C112 fixture 沒有字串。未驗證 transport length、加密或 padding。

## 怎麼驗證

在專案根目錄執行：

```powershell
py -3.12 -B -X utf8 docs/reports/issue-49-replay.py --source-root . --require-qualified
```

實際輸出：

```json
{"status":"partial","evidenceChecks":"pass","scope":"static-template-and-payload-contract; not live-capture or end-to-end","protocolRows":18,"mappingRows":10,"supplementalRows":34,"semanticCases":26,"packetCasesReplayed":2,"sourceHashChecks":77,"adoptedTemplateRows":0,"liveCapturedFixtures":0,"qualified":false,"blockers":["gold-and-meat-template-rows","gold-and-meat-ground-inventory-fixtures"]}
```

退出碼 **2**，表示新的核心驗收未過；不得以 evidenceChecks=pass 冒充完成。77 次來源 hash 核對含舊 42 次與新 JAR 及 34 次 class 核對（有重複 class）。新增摘錄內部另檢查 SHA 與 offset 範圍。

31 個刻意變異均被拒絕：26 個語意 expect、2 個 payload opcode、2 個拒絕結果及 1 個來源摘錄。未更動交付 JSON 執行變異。暫存結果在 `scratch/issue-49/static/mutation-result.json`。本輪無產品建置、無原生驗收。

## 逐條驗收／待驗證

| 新驗收 | 結果 |
|---|---|
| 至少金幣與肉有完整 adopted rows 及 SQL／來源 | 未完成：登入被拒，列數 0。 |
| 七類模板欄位與 unknown 明示 | 七類仍缺模板；未知維持 null 的既有缺件列表，後續 adopted 欄位模型使用 unknown。 |
| 金幣 gfx 7／3247 以 grdgfx 裁定 | 未完成，未採任何值。 |
| 金幣／肉 S87 與背包最小 fixture | 未完成；不可用猜測模板填補。 |
| 非物品與拒絕 fixture 重播 | 已完成 2 個明示 static 的 C112 payload。 |
| 正常 SHUTDOWN 並確認 43324 無監聽 | **未完成，須接續處理本輪已啟動的 MySQL。** |
| 無正式環境／機密／禁止檔案變更 | 未連 3306／2000，未讀 config/server.properties，未修改 docs/REPORT.md、BIN、客戶端、封存 release、assets junction、lock 或硬連結。 |

## 產出路徑／給下一棒的提示

產出只有 `docs/reports/issue-49.md`、`docs/reports/issue-49-replay.py`、`work/issue-49/contract.json`、`work/issue-49/fixtures.json`。日誌與一次性檔案只在 `scratch/issue-49/`，不提交。

由**本機主控／dot**先解決已提出的憑證使用範圍，再接續本次實例；**不要再次啟動另一個 mysqld**。確認 PID／43324 是本輪實例後，以已授權方式登入，先 SELECT 核對 UUID `e3443e40-d090-11f1-8040-4cedfb679789`、port 43324、datadir 在指定 p24 路徑下，三項皆符才可查指定七類模板。只讀模板與必要 schema metadata，不查個人資料，不改 DB。無論補件是否成功，最後對本輪實例執行 SHUTDOWN 並確認 43324 無監聽。

新增驗證器接受 `dbEvidence` 中的精確 SELECT、結果 SHA 與逐列欄位 pin；需以真實查詢資料接入，完成金幣／肉 S87／S15 fixture，再驗證各欄變異及 `--require-qualified` 退出 0。runtime charset/filter、儲存上限及完整政策缺件應如實保留 unknown，不得猜值。
