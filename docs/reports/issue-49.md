# Issue #49 — 原服地面物品與背包靜態契約

第 3 次嘗試，2026-11-05；resource；分支 `task/49`；承接受審提交 `2345fe7c662eb084424172d909cf1646fec33e4f`。

**部分補件，整體未通過：`status=needs:local`、`qualified=false`。** 七類物品的完整採用模板列仍為 0；packet bytes fixture 與實際擷取 fixture 都為 0。此次增加 10 段模板載入／icon getter 映射，以及 26 個會執行 `expect` 比對的靜態分支案例。這些案例不能算成原服封包解碼驗收，也不能解除 catalog 來源閘門。

## 做了什麼

沿用指定 worktree，沒有 checkout／建立分支；已讀根目錄 `AGENTS.md`、`docs/REPORT.md`，後者未修改。只修改本報告、重播工具與兩個任務 JSON；暫存腳本只寫在此 worktree 的 `scratch/issue-49/`。

保留原有 12 份 class 的 18 段協定摘錄；另外從主專案 `work/timed-item-lifecycle-r1/` 找到 `ItemTable`、`L1ItemInstance` 的既有反組譯。兩份文字 SHA 與該目錄的 `class-evidence.json` 相符；`initial-source-pins.json` 記錄的歷史 JAR SHA 也與原交付一致：`d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`。

新來源只有歷史 javap／索引核對，**本輪未重新讀取或核 hash JAR／新增來源的 class，也未重新 javap**，不宣稱等同目前執行中的服務。來源索引提及原伺服器 source 路徑，但本輪未循該路徑讀取。

| 新來源 | 文字 SHA-256 |
|---|---|
| `work/timed-item-lifecycle-r1/datatables.ItemTable.javap.txt` | `4319d84178159307827e07b20cc996ebc701f578969705fa5354712303e456de` |
| `work/timed-item-lifecycle-r1/model.Instance.L1ItemInstance.javap.txt` | `ea13fda5cca763610b7c7f90643ce4d4ead65333a6c833224ea9c98b234b66de` |

索引 SHA、歷史 class SHA、來源方法、精確文字行號、方法內 bytecode offset 與原文摘錄均保存於 `contract.json.mappingSources`／`adoptedMappingRows`。**bytecode offset 不是 File Offset／RVA／VA。** 映射列不是實際物品模板列，後者仍放在空的 `adoptedTemplateRows`。

## 已驗證：模板欄位載入與使用端

下列 offset 除最後一列外，皆屬 `ItemTable.allEtcItem()`；欄名是反組譯中的 ResultSet 欄名，沒有執行查詢。

| 列 ID | 文字行／bytecode offset | 可採用的靜態結論 |
|---|---|---|
| etc-template-id | 194–204／45–69 | `item_id` 經 getInt／setItemId 載入模板鍵；不是 wire instance ObjectId。 |
| etc-name-token | 217–221／104–115 | `name_id` 經 getString／setNameId 載入；S87 的使用端呼叫 getNameId。 |
| etc-use-type | 231–239／146–171 | `use_type` 字串經 `_useTypes` map 轉 int 才 setUseType；不可直接當作 packet useType。 |
| etc-inventory-icon | 257–261／222–233 | `invgfx` 經 getInt／setGfxId 載入。 |
| etc-ground-gfx | 262–266／236–247 | `grdgfx` 經 getInt／setGroundGfxId 載入；S87 使用 getGroundGfxId。 |
| etc-item-desc | 267–271／250–261 | `itemdesc_id` 經 getInt／setItemDescId 載入；S15 使用 getPacketItemDescId，轉換本體仍缺。 |
| etc-trade | 287–295／306–325 | `trade == 0` 才 setTradable(true)；不等於 droppable=true。 |
| etc-cant-delete | 296–305／328–348 | `cant_delete == 1` 才 setCantDelete(true)。 |
| etc-stackable | 306–315／351–371 | `stackable == 1` 才 set_stackable(true)；非零不一概為 true。 |
| instance-icon | 3409–3414／get_gfxid 0–9 | instance.get_gfxid 直接回傳 template.getGfxId；S15 將該值傳給 writeH。 |

上述是載入端與使用端的呼叫證據。`L1Item`／`L1EtcItem` setter/getter 本體、`getPacketItemDescId`、`getPacketUseType`、`toPacketBless` 等仍未補齊，因此不能聲稱已驗證全部欄位到 wire 的轉換。`trade`、`cant_delete`、`stackable` 的 boolean 計算則直接包含在本次摘錄。

## 已驗證：原有協定摘錄

以下保留原交付的靜態結論；C/H/D/S 是方法呼叫欄位記法，尚未認證底層 byte 編碼、加密與傳輸 framing。

| 項目／contract 列 ID | 方法內 offset | 結論 |
|---|---|---|
| C25-fields／ownership | C_DropItem.start 0–56、76–107 | Hx Hy D背包 ObjectId D有號 count；非正數返回；物品必須在角色背包且現存量正數。 |
| C25-template-policy／equipped-sealed | 108–233、372–434 | 非 GM 受 DORP_ITEM、ItemDropTable、tradable、限時狀態限制；裝備／sealed 拒絕。234–371 另有寵物／娃娃前置分支，不能省略後宣稱成功。 |
| C112-fields／nonitem／source-owner | C_PickUpItem.start 147–415 | Hx Hy D地面 ObjectId D有號 count；readD 轉 long；非正數、非 L1ItemInstance、座標／距離／地面 inventory 引用不符、超量、其他 owner 等會拒絕。 |
| S87-header／name／tail | S_NPCPack_Item.buildPacket 0–127、296–372 | C87 Hx Hy DinstanceId HgroundGfx C0 C0 Clight C0 Dmin(count,20億) H0；普通單件用 nameId，多件由 concat bootstrap #140 組成 `nameId + " (" + actualCount + ")"`。名稱的 long count 不受 D 顯示量 clamp 影響。特殊 charge／照明分支不能套用普通單件公式。 |
| S87-tail | 309–372 | 名稱後 Snull C0 D0 Snull Snull C0 C255 C0 C0 C0 C255 C255；null 的 bytes 編碼仍缺。 |
| S15-header／flags／tail | S_AddItem constructor 9–363 | C15 DinstanceId HpacketItemDescId CpacketUseType Ccharge Hinstance.get_gfxid Cbless/sealed Dmin(count,20億)，再寫 flags、viewName、短 status／長路徑的 0，尾 C10 H0 D0 D0。 |
| S24 | S_ItemAmount.buildPacket 0–102 | C24 DinstanceId SviewName Dmin(count,20億) CstatusLength + bytes。 |
| S242-gate／S242 | shouldUseLongStatus 0–41；writeLongStatus 0–75 | RO client 且已辨識、status 長度 256..65535 才選 long；C242 C1 DinstanceId SviewName Dcount HstatusLength + bytes。配置值未知。 |
| S120／S57 | 各 constructor 9–20 | 地面 C120 DobjectId；背包 C57 DinstanceId。舊摘要的地面 C9 與 class 不符，不採用。 |

**身分與上限：** template ID、ground gfx、inventory icon、packet desc、wire useType 是不同欄位。C25 的背包 ObjectId、C112 的地面 ObjectId 是 instance 身分；合併／部分移轉不能要求兩者相同。請求最大正 Java int 為 2147483647，回覆顯示 clamp 為 2000000000；兩者均不能當作 storageCountMax。本輪未認證完整交易、成功回覆順序或所有背包封包（例如 S_InvList）。

## 推導案例與仍缺的封包 fixture

`fixtures.json` 將人工輸入明示為 `static-semantic-vector`，使用邊界整數、布林值與虛構 instance ID，沒有假稱是原服資料或去識別擷取。每例包含來源列 ID、摘錄 SHA、適用的前置條件、input 與結構化 expect；工具實際執行 Python 條件模型並比對 expect。

26 例：C25/C112 各 4 個 count 邊界，物品／非物品 2 例，空量／超量／不同 owner／無 owner／本人 owner 5 例，裝備／sealed／皆否 3 例，顯示量 3 例，地面／背包移除呼叫參數 2 例，模板 boolean 轉換 3 例。`continue` 只表示通過該局部分支，**不是 handler 接受或實際拾取／丟棄成功**。這些是靜態來源推導，不是 Java handler 執行。

`packetFixtures=[]`、`liveCapturedFixtures=[]`、`byteEncoding=null`。目前沒有 ClientBasePacket／ServerBasePacket 的 read/write 編碼來源，不能猜 endian、文字編碼、null terminator 或 framing 後當成原服 bytes。審查要求的「封包輸入／解碼結果／拒絕預期」因此仍未完整交付；本輪只修正 expect 未執行的缺陷。

工具不再斷言 qualified 必為 false、模板／實際 fixture 必為空；改為回報缺件，並對尚無驗證器的新增模板／packet／capture 資料保持阻擋，避免只塞資料就自動合格。後續補件者須一併加入欄位來源核對與實際解碼器。

## 怎麼驗證

在此工作樹根目錄執行：

```powershell
py -3.12 -B -X utf8 docs/reports/issue-49-replay.py
py -3.12 -B -X utf8 docs/reports/issue-49-replay.py --source-root 'D:\lineage開發\TW1608262200研究'
py -3.12 -B -X utf8 docs/reports/issue-49-replay.py --require-qualified
```

本次實際結果：前兩條 Python exit 0，僅代表來源／語意檢查通過。帶 source-root 的輸出如下；不帶時 sourceHashChecks=0，其餘相同。

```json
{"status":"partial","evidenceChecks":"pass","scope":"static-excerpts-and-semantic-vectors-only","protocolRows":18,"mappingRows":10,"semanticCases":26,"packetCasesReplayed":0,"sourceHashChecks":42,"adoptedTemplateRows":0,"liveCapturedFixtures":0,"qualified":false,"blockers":["template-rows-and-getter-conversions","packet-bytes-and-encoding-source"]}
```

42 次 SHA 檢查包含原有 36 次與新增的 2 份文字、2 次 class 索引、2 次 JAR pin 索引（後兩項含重複檔案）；並比對 28 段原行文字。新來源的 class SHA 僅比對歷史索引，不是重讀 class。第三條的 **Python exit 2** 為預期的整體驗收阻擋；PowerShell 包裝時需 `exit $LASTEXITCODE` 才能保留退出碼。

在 scratch 的一次性檢查另將 26 例 expect 各別改錯，全部被拒絕；摘錄、案例 pin、qualified=true、超出 Java int 範圍共 4 個變異也全部被拒絕。測試未改動交付 JSON。靜態驗證器無網路、無 DB，不載入 handler。`git diff --check` 通過；四個交付路徑的 `git check-ignore --no-index` 無匹配（exit 1），未強制加入忽略檔。

## 未驗證什麼／逐條驗收

| 驗收項 | 第 3 次結果 |
|---|---|
| 每個採用協定／映射列有 SHA 與精確位置 | 已核對：18 協定＋10 映射；映射來源層級另行揭露。 |
| 七類完整模板列與每欄證據 | 未達成：金幣、肉、箭、治癒藥水、綠藥水、白藥水、回家卷均缺合法完整列，adoptedTemplateRows=0。 |
| ground gfx、name token、desc/icon/useType、stack/drop/equip/bind、storageCountMax | 各類實值仍為 null；增加載入端映射，不用 getter 名稱冒充模板值。 |
| 金幣 7／3247 裁定 | 未達成；兩者只作定位線索，均未採為金幣 ground gfx。 |
| template ID／wire ObjectId 區分 | 已有靜態證據；未驗實際合併／移轉交易。 |
| 最小封包 fixture，含非物品／拒絕 | 未達成：26 個語意案例的 expect 已執行，但 packet bytes／編碼依據仍缺。 |
| 誠實輸出整體狀態 | 已修正：status=partial；require-qualified 退出 2，不再以 status=pass 掩蓋缺件。 |

資源查找範圍：`D:\P24Lab-ClientEngine` 不存在；主專案 work 的定向檔名盤點找到上述歷史反組譯，未找到完整物品模板或 BasePacket 證據。部分既有 synthetic 目錄共 11 處拒絕存取，沒有重試／改 ACL；另跳過 junction、assets、tmp、native、evidence、build/obj/bin 與 engine-next-integration-r1。這是有限查找，不能宣稱全機無資料。沒有開啟盤點中的 SQL 或 DB 快照。

本輪一般命令工具在程序建立前失敗，改用核准的擴權命令處理同一工作樹；Git 擁有者差異以單次 `-c safe.directory=...` 處理，沒有修改全域設定。沒有 DB 操作、服務啟停、遊戲程序操作、正式 BIN／客戶端／封存／硬連結內容／assets junction／lock／docs/REPORT.md 修改，沒有讀取 config/server.properties；沒有產品建置或原生驗收。

## 產出路徑／給下一棒的提示

產出為 `docs/reports/issue-49.md`、`docs/reports/issue-49-replay.py`、`work/issue-49/contract.json`、`work/issue-49/fixtures.json`。臨時採集／變異測試腳本只留此 worktree 的 `scratch/issue-49/`，不提交。歷史交付請由 Git 查閱。

交由**本機資源整理者／Claude 主控**接手 `needs:local`，不要在沒有新來源的情況下當作完整驗收重新排同一任務：

1. 指明可合法讀取的既有去機密模板匯出及版本；精確區分七類物品的普通／祝福／受詛咒／自訂 variant。保留欄名、原始行號、來源 SHA，至少包括 item_id、name_id、grdgfx、invgfx、itemdesc_id、use_type、stackable、trade、cant_delete 及裝備／綁定／儲存上限依據。不要從俗名猜 ID。
2. 提供同一 JAR 版本的 L1Item／L1EtcItem setter/getter、packet desc/useType/bless 轉換與非機密丟棄政策依據；以逐欄 evidence 填入 adoptedTemplateRows，另處理金幣 gfx 裁定。
3. 提供 ClientBasePacket read/readH/readD、ServerBasePacket writeC/H/D/S 的既有靜態證據及 SHA／方法位置；據此加入有 bytes、解碼欄位、拒絕預期與來源 pin 的最小 fixture。可以是明示推導的 fixture；不必假稱實際擷取。若使用既有實際擷取，另附去識別與來源說明。
4. 擴充 replay 的模板逐欄／packet 解碼驗證後重跑 `--source-root` 與 `--require-qualified`。在缺件解除前維持 qualified=false；本輪不申請查 DB、開服或讀機密設定的例外。
