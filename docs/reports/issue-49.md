# Issue #49 — 原服地面物品與背包靜態契約

2026-11-05；resource；分支 `task/49`；基線 `89888870e52cf3c19e046c7b0c040619c117c644`。

## 第 2 次嘗試：修正合併前閘門

本次沿用既有 `task/49` worktree，未切換或建立分支；已讀工作樹根目錄 `AGENTS.md` 與 `docs/REPORT.md`，後者未修改。以下靜態採集與缺件判定沿用第 1 次交付；本次只修正重播工具的追蹤位置與文件，未重新反組譯或搜尋模板。

將被 `.gitignore` 排除的 `work/issue-49/replay.py` 搬至 `docs/reports/issue-49-replay.py`；工具依自身位置定位根目錄的 `work/issue-49/contract.json` 與 `fixtures.json`，不依賴啟動時的工作目錄。保留原 ignore 規則，不再強制加入被忽略檔。重播指令與產出清單同步更新。

## 結論

**部分完成，整體 `qualified=false`／`needs:local`。** 已核對 12 份既有 class、採用 18 項靜態封包／限制契約，附 6 個靜態來源重播案例。七類物品的完整模板採用列為 **0**；實際封包擷取 fixture 為 **0**。不可用本交付解除一般物品 catalog 的來源閘門，也不能據此決定金幣使用 gfx 7 或 3247。

## 第 1 次交付做了什麼與證據範圍

- 開工核對 Issue 最新 body、全部留言：runner 為 dot、`task:running`，僅有派工留言，沒有 RESULT；本機及遠端都沒有 `task/49`。讀取 AGENTS.md、TASKS.md；父任務明確禁止接觸 `docs/REPORT.md`，因此未讀該檔，優先於派工留言的通用讀取要求。
- 在 `scratch/wt/task-49` 從最新 origin/main 建立隔離工作樹；主工作樹 `task/42` 與使用者未追蹤圖檔未改動。
- 只用 `work/engine-slow-pickup-source-r1`、`work/engine-slow-utility-source-r1` 已保存的 class、javap、collection 與 source-pins。歷史 JAR SHA 為 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`；本輪未重新讀取／核 hash JAR，不能宣稱與當前服務使用的 JAR 相同。
- 12 份 class 的 SHA 與各自 collection.json 相符；重新執行 `javap -c -p`（除 OpcodesClient 外加 `-constants`）與既有文字忽略空白行／行首尾空白後相同。另用 `javap -v -p` 讀 S_NPCPack_Item 的 concat bootstrap。javap 只反組譯，不載入或執行伺服器 class。
- 每個採用協定列在 `work/issue-49/contract.json` 都有來源文字 SHA-256、class SHA-256、collection SHA、方法簽章、**方法內 bytecode offset**、精確文字行號與原文摘錄。bytecode offset 不是檔案 File Offset／RVA／VA。

## 已驗證的靜態契約

下表的列 ID 對應 `contract.json.adoptedProtocolRows`；C/H/D/S 是既有 write/read 方法的欄位記法。本輪未核 ServerBasePacket 的編碼／加密／傳輸 framing，不把下列欄位描述冒充完整 socket bytes。

| 項目 | 方法內 offset／列 ID | 採用結論 |
|---|---|---|
| C25 丟棄 | C_DropItem.start 0–56、76–107；C25-fields／ownership | H x、H y、D **背包 instance ObjectId**、D 有號 count；count <= 0 拒絕，物件必須在角色背包且現存量 > 0。 |
| C25 丟棄限制 | start 108–233、372–434；C25-template-policy／equipped-sealed | 非 GM 受 DORP_ITEM、ItemDropTable、tradable 與 `_time` 限制；已裝備／sealed 拒絕。234–371 另有使用中寵物／娃娃分支，本表不是完整允許條件。未知配置不能寫成每類物品 droppable=true。 |
| C112 拾取 | C_PickUpItem.start 147–205；C112-fields | H x、H y、D **地面 instance ObjectId**、D 有號 count；readD 轉 long，非正數返回。最大可表示正請求為 2147483647，並非物品儲存上限。 |
| 非物品／超量／其他 owner | start 206–415；C112-nonitem／source-owner | World.findObject 必須是 L1ItemInstance；座標、距離、同一地面 inventory 引用必須成立；超量或不允許的 owner 拒絕。這是實際 class 分支證據，未實際送出拒絕請求。 |
| S87 地物 | S_NPCPack_Item.buildPacket 0–97；S87-header | C87 Hx Hy D instanceId H **template.getGroundGfxId** C0 C0 Clight C0 D min(count,2000000000) H0。ground gfx 與背包 icon 是不同 getter。 |
| S87 名稱／數量 | buildPacket 98–127、296–306；S87-name／tail；constant pool #140 | 普通單件取 nameId；count > 1 為 `nameId + " (" + actualCount + ")"`。名稱用實際 long count，D 顯示量則 clamp 20 億，兩者可能不同。特殊 charge／照明分支不能套用普通單件公式。 |
| S87 尾欄 | buildPacket 309–372；S87-tail | S null、C0、D0、S null、S null、C0、C255、C0、C0、C0、C255、C255；此處 null 如何編碼仍由 ServerBasePacket 決定。 |
| S15 新背包物品 | S_AddItem constructor 9–120；S15-header | C15 D **instanceId** H packetItemDescId C packetUseType C charge H **instance.get_gfxid** C bless/sealed D min(count,20億)。template ID 沒有在此直接作 ObjectId 傳送。 |
| S15 flags／名稱 | constructor 123–263；S15-flags | identified、tradable、cantDelete、safeenchant/useType、sealed 決定 flags；名稱取 instance.getViewName，不能拿地面 nameId 直接替代。 |
| S15 status／尾 | constructor 266–363；S15-tail | C 短 status 長度與 bytes；long status 路徑在此寫 0；尾 C10 H0 D0 D0。 |
| S24 數量 | S_ItemAmount.buildPacket 0–102；S24 | C24 D instanceId S viewName D min(count,20億) C statusLength + status bytes。 |
| S242 長狀態 | S_ItemStatus.shouldUseLongStatus 0–41、writeLongStatus 0–75；S242-gate／S242 | RO client 且已辨識，status bytes 長度 256..65535 才選 long；C242 C1 D instanceId S viewName D count H statusLength + bytes。未讀配置，不能判定目前服務是否走此分支。 |
| 移除 | S_RemoveObject constructor 9–20；S120；S_DeleteInventoryItem constructor 9–20；S57 | 地面 **C120 D objectId**，背包 **C57 D instanceId**。舊 `work/engine-slow-pickup-source-r1/CONTRACT.md` 的 C9 與原 class 不符；本輪以 class 及重現 javap 為準，不改歷史文件。 |

C25/C112 的 opcode 常數可由已 pin 的 OpcodesClient.class 以 `javap -constants` 重現；ClientPacketStructure.javap 的 25 與 112 都跳至 offset 1072 的 `hhdd`。來源 hash 同列於 sources。

**身分與數量邊界：** template ID 是資料模板鍵，ground gfx、inventory icon、packet desc、wire UseType 是各自欄位；不得將同數字視為同命名空間。C112 使用地面 ObjectId，C25 使用背包 ObjectId。拾取後的背包合併／部分移轉不能要求 ObjectId 恆等；數量必須依權威地面與背包回覆各自處理。本輪未認證完整移轉交易、所有成功回覆順序或儲存 count 上限。

## 七類模板缺口與推測

`contract.json.templateGaps` 逐列列出：金幣、肉、箭、治癒藥水、綠藥水、白藥水、回家卷。各列的 templateId、groundGfx、nameToken、inventoryDesc、inventoryIcon、wireUseType、stackable、droppable、equippable、bindingRestrictions、storageCountMax 均為 null，`qualified=false`。

既有設計文件中的 gfx7／3247，以及肉、箭、藥水候選值，只是後續定位線索，**本輪沒有採為原服模板列**。藥水俗名也不能自行等同普通／祝福／受詛咒或自訂變體。來源 getter 證明欄位如何使用，無法代替實際模板欄位值。

已查的兩組 source bundle 沒有完整模板匯出；utility 的 source-pins 將舊用品依據指向 P24 歷史 HANDOFF，本輪沒有進入封存／研究原件追讀。`D:\P24Lab-ClientEngine` 在本次環境不可見；專案內 environments/p24/server 的 JAR 標成 HardLink，未讀取其內容。對 work 的檔名盤點遇到 `engine-next-integration-r1/evidence/rejection-fixtures-46dcwnvx` 與 `rejection-fixtures-mwcyqa27` 存取拒絕後停止廣泛掃描，未重試／改 ACL；改用已明確存在的來源檔。不能宣稱已窮盡全機資源或完全存取。

## 怎麼驗證與重播

在 task/49 工作樹根目錄：

```powershell
py -3.12 -B -X utf8 docs/reports/issue-49-replay.py
py -3.12 -B -X utf8 docs/reports/issue-49-replay.py --source-root 'D:\lineage開發\TW1608262200研究'
```

第一條驗證交付的摘錄完整性與 6 個來源分支案例；第二條另外核對既有 12 份來源文字、12 份 class、12 次 collection pin（共 36 次 hash 檢查，含重複 collection）及 18 段原行文字。腳本唯讀、無網路、不載入 handler、不寫測試檔。

第 1 次交付的兩種重播均 exit 0：18 段協定摘錄、6 個靜態案例通過；帶來源根目錄時 36 次 hash 檢查通過。該次曾強制加入被忽略的腳本，導致合併前閘門退回；第 2 次已搬移至正常追蹤路徑，驗證結果見下方。

第 2 次實際執行上述兩條新路徑命令，皆 exit 0：`protocolRows=18`、`replayedCases=6`，來源核對模式 `sourceHashChecks=36`；兩者仍為 `adoptedTemplateRows=0`、`liveCapturedFixtures=0`、`qualified=false`。`git diff --check` 通過；以 `git check-ignore --no-index` 檢查本報告、新腳本與兩個 JSON，無匹配規則（exit 1），確認四個交付路徑皆可正常追蹤。`contract.json` 與 `fixtures.json` 內容未修改。

`fixtures.json` 明示類型為靜態來源摘錄重播；含非物品、非正數、超量／owner、裝備／sealed 拒絕與回覆限制。**這些不是原服封包錄製、不是 UI fixture、不是實際 handler 測試。** 原任務要求的去識別實際最小封包 fixture 仍未交付，沒有用合成 bytes 充數。

本輪 Python launcher 在一般沙箱找不到安裝；經核准的擴權 shell 才能啟動已安裝 Python 3.12。Git 一般沙箱無法連線；只讀 refs 與 fetch 在核准擴權 shell 成功。這不等於全檔案存取權驗證。javap 舊文字部分為 CP950，依原 bytes 計 SHA，解碼後比較；沒有改寫原證據。

## 未驗證什麼／給下一棒的提示

需由本機資源整理者／Claude 主控提供或指明**既有合法、去機密的模板匯出**及對應來源版本：七類物品的精確 variant、欄名、列號、SHA，包含模板與 packet getter 轉換依據、stack/drop/bind 規則所需的非機密設定摘要；另提供去識別的成功／非物品／拒絕封包樣本及擷取來源。不能為了補件查 DB、開服或讀 server.properties；若沒有既有匯出，保持 needs:local，由主控另定授權範圍。

兩次交付均沒有 DB 操作、服務啟停、遊戲程序操作、正式 BIN／客戶端／封存／assets junction／lock 修改；未讀 config/server.properties。第 1 次未讀 docs/REPORT.md，第 2 次依本次指示讀取但未修改。沒有產品程式碼修改、建置或原生驗收。

產出：本報告、`work/issue-49/contract.json`、`work/issue-49/fixtures.json`、`docs/reports/issue-49-replay.py`。第 1 次的一次性採集腳本及 verbose javap 留在根專案 `scratch/issue-49/`，不提交；第 2 次未在根專案寫檔。所有原資料保留。Claude 審查可先採用有 pin 的歷史協定結論；模板與實際 fixture 補齊前不得將 Issue #49 視為完整通過，仍需 `needs:local` 補件。
