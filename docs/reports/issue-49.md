# Issue #49 — 原服物品模板與靜態封包契約

2026-11-05；承接 09:39 的 dot 專屬派工與 `11e62ee`。本報告取代舊輪報告，歷史由 Git 保留。

## 結論

**本輪資源驗收已達成：7 筆指定模板、金幣與肉各一個 S87／S15 靜態 fixture、非物品及超量拒絕案例，嚴格離線重播通過。** `qualified=true` 僅限本報告的靜態契約；不是實際封包擷取、Java handler 執行、圖像或端到端驗收。

金幣模板 `item_id=40308` 的 `grdgfx=7`，背包 `invgfx=5`。本次有來源地採用 **ground gfx 7**；不採用3247，也不將箭的背包 icon 7 混為金幣 ground gfx。P24Lab MySQL 已正常 SHUTDOWN；最後確認兩個實驗埠無監聽、本次程序已結束。

## 做了什麼／前次啟動阻塞

- 開工核對 `runner:dot`、`runner_lock:true`；本輪派工後沒有其他 RESULT，舊執行者已有清理回報。隔離工作樹切至 `11e62ee`，保留其他人的靜態工具改進；沒有重設共用 task/49。
- 已讀 AGENTS.md；未讀 config/server.properties、憑證內容或 docs/REPORT.md。僅讓 mysql.exe 使用指定 P24Lab 憑證檔，沒有顯示、複製或記錄帳密。
- 前次錯誤為 `The innodb_system data file 'ibdata1' must be writable`。本輪唯讀檢查顯示 ibdata1 未設 ReadOnly、ACL 沒有明列拒絕；一般工具以沙箱帳號執行。經正式 `require_escalated` 審查後，同一既有設定成功啟動，支持先前失敗與沙箱執行限制有關；尚未隔離 token 與 junction 路徑限制的個別影響。未修改 ACL、屬性或安全設定。
- 每次啟動前確認實驗埠空閒；每次連線先 SELECT 指定的 UUID／port／datadir，三者相符才讀模板。身份、查詢與關閉證據在 contract.json 的 dbEvidence／dbLifecycle。
- 第一次僅 SELECT information_schema 中 etcitem／weapon／armor 的欄位 metadata；第二次僅 SELECT etcitem 的七個明列主鍵。未查其他資料列、帳號或角色；沒有 INSERT、UPDATE、DELETE、DDL，也未啟動 Java／客戶端或操作正式環境。
- 兩次都在 finally 正常 SHUTDOWN；最後日誌於09:48:40 UTC 記錄 Shutdown complete，最終再次核對程序及兩埠。mysqld 正常啟停需要寫實驗資料目錄，屬已授權維護；模板查詢本身是唯讀。

## 已驗證：七筆模板

來源 `db:p24_world.etcitem`。主鍵、查詢 SQL 原文、欄名、回傳列與 canonical JSON SHA-256 內嵌於 dbEvidence.queries 及每筆 adoptedTemplateRows.source。候選主鍵以既有研究／引擎線索定位，再逐一核對實際回傳名稱；不從俗名外推其他變體。SQL 以 HEX 包裝 UTF-8 JSON，避免主控台編碼破壞名稱。

| 類別／回傳名稱 | template ID | name token | ground gfx | inventory icon | raw desc → wire desc | raw use_type → wire UseType |
|---|---:|---|---:|---:|---|---|
| 金幣 | 40308 | `$4` | 7 | 5 | 0 → 0 | none → 255 |
| 肉 | 40056 | `$23` | 36 | 124 | 10 → 10 | food → 38 |
| 箭 | 40743 | `$61` | 66 | 7 | 0 → 0 | arrow → 254 |
| 治癒藥水 | 40010 | `$26` | 45 | 80 | 1342 → 1342 | hpr → 249 |
| 綠藥水／自我加速藥水 | 40013 | `$234` | 42 | 77 | 17 → 17 | power → 246 |
| 白藥水／終極治癒藥水 | 40012 | `$238` | 46 | 81 | 130 → 130 | hpr → 249 |
| 回家卷／傳送回家的卷軸 | 40079 | `$505` | 22 | 474 | 1338 → 1338 | home → 9 |

七筆 raw stackable=1、trade=0、cant_delete=0、bless=0；載入／getter 證據推得 stackable=true、tradable=true、cantDelete=false、packetBless=1。只適用明列主鍵，不外推祝福／受詛咒／自訂變體。

每筆 fieldEvidence 連到具摘錄 SHA 的載入、setter/getter、writer。保留18段協定、10段載入／icon映射；supplemental JAR 摘錄由34增至38，補上 hpr=-7、power=-10、home=9、getMagicCatalystType。重算 JAR 與定向 class SHA，未修改來源。

`itemdesc_id=0` 不是猜值：getPacketItemDescId offset 95–99 呼叫 getMagicCatalystType，金幣／箭皆走 default 回0；其他五筆非零 desc 走 offset 89–94。負數 UseType 取低8位。另修正舊索引將 switch case 值誤當最大 offset 的問題；所有 offset 都是 JVM 方法內指令位置，不是 File Offset／RVA／VA。

## 推導 fixture 與邊界

6個 packetFixtures 全標 `derivation: static`，附 payloadHex、來源 pin、解碼與預期結果：

- gold-s87／gold-s15：金幣模板，count=250；核對 gfx7／icon5、desc0、UseType255。
- meat-s87／meat-s15：肉模板，count=3；核對 gfx36／icon124、desc10、UseType38。
- pickup-nonitem：C112 解碼後驗非 L1ItemInstance 拒絕。
- pickup-over-available：請求11、現存量10，驗 offset 376 返回；沒有把10當儲存上限。

ObjectId、座標、count、owner/actor 與 instance flags 是明示的測試輸入，不是 DB 角色資料或實際擷取。C112 用地面 ObjectId，C25 用背包 ObjectId；template ID、gfx、icon、desc 各自獨立。

整數小端序與 C/H/D/S 編碼有 BasePacket 摘錄支持。fixture 只含解密後 payload，不含 framing、加密或 padding。字串僅在 ASCII 相容 charset、未命中文字過濾的前提推導；未讀執行期 charset／BAD_TEXT_LIST 配置。S15 只覆蓋未辨識／未封印／未裝備／零charge等明列狀態，count小於10億以避開 RangeLong 分支。這些前提不能省略後宣稱實際線上等價。

請求 count 是 signed int，最大正值2147483647；回包顯示量 clamp 到2000000000。地面名稱可用實際 long count，背包名稱另有分支；兩者都不是已證實的 storageCountMax。

## 怎麼驗證

在本次隔離工作樹執行：

```powershell
py -3.12 -B -X utf8 docs/reports/issue-49-replay.py --require-qualified
py -3.12 -B -X utf8 docs/reports/issue-49-replay.py --source-root 'D:\lineage開發\TW1608262200研究' --require-qualified
```

帶來源根目錄的實際輸出（exit 0）：

```json
{"status":"qualified","evidenceChecks":"pass","scope":"static-template-and-payload-contract; not live-capture or end-to-end","protocolRows":18,"mappingRows":10,"supplementalRows":38,"semanticCases":26,"packetCasesReplayed":6,"sourceHashChecks":81,"adoptedTemplateRows":7,"liveCapturedFixtures":0,"qualified":true,"blockers":[]}
```

另做29個變異：七筆模板各改錯gfx／來源hash、六個packet各改錯opcode／expect，以及DB身分、關閉標記、缺少金幣列，全部被拒絕。交付JSON未被變異測試修改。81次hash含重複pin，不是81個獨立檔案。重播與變異工具都不連DB、不執行handler。

## 未驗證什麼／下一棒

- 每列 droppable、equippable、bindingRestrictions、storageCountMax 保留 `unknown`。tradable=true 不代表可丟；C25還受 ItemDropTable／配置、限時、裝備、sealed、寵物／娃娃等狀態影響。沒有擴查其他表或正式設定來補欄位。
- 未驗當前Java服務載入、完整交易／合併、原圖、音效、原生點擊或真機移轉。實際擷取fixture為0，符合本次允許靜態推導的新規格。
- ground-items 後續可採明列模板／writer對照，保留unknown及伺服器權威結果，不以靜態qualified推定所有物品可丟棄。

僅更新本報告、docs/reports/issue-49-replay.py、work/issue-49/contract.json、fixtures.json。一次性查詢／整理／變異腳本與關閉證據留在主專案 scratch/issue-49，不提交憑證、原始設定或服務日誌。共用工作樹保留原狀；提交／推送的實際結果由RESULT另報，不預先宣稱已上傳。
