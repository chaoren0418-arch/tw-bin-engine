# P20 原裝備協議與原外觀

固定原 JAR SHA256 `d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf`，ClientLogin2。所有 JVM offset 都是 method bytecode offset；不是 PE RVA/VA。原 Java/class/fresh javap 的 hash 見 `reference/equipment/manifest.json`。實測狀態與失敗鏈見 HANDOFF.md、result.json 及 evidence/finite-ledger.json；來源推導不替代實測。

## 原裝備狀態

- C164：C opcode、D 背包 objectId、零補齊8bytes；單次使用同一原裝備即穿上／卸下，選另一武器由原服換裝。產品只送獲准的一次輸入；不預先切換裝備、不加減預期屬性。有限支援原31長劍、52雙手劍、20096環甲、20222木盾的已鑒定+0正常bless指紋；template ID不在wire，不能將object ID或icon當template ID。
- S119：C119/D objectId/C weapon，零補齊8bytes。原 `S_CharVisualUpdate` int constructor offsets10–22，PC constructor10–28。主角與旁觀同樣使用絕對武器型態。未知object不建立幽靈物件。
- S64/sub65：C64/C65/C count，逐件 D objectId/H slot/H0，尾端補齊。`S_EquipmentWindow(List)` offsets10–75。完整清單取代舊清單。
- S64/sub66：C64/C66/D objectId/C slot/C boolean，恰8bytes。`S_EquipmentWindow(int,int,boolean)` offsets10–33。收到絕對狀態才更新原裝備窗模型；重複不累加。
- S5/S15/S24/S242/sub1/S100背包延續原格式；武器名稱尾綴` ($9)`、本階段防具` ($117)`來自`L1ItemStatus.getNumberedViewName` offsets478–493及562–576。產品保留原名稱、識別資料與完整status/tail，裝備標記是原名稱證據，並另外保留S64裝備窗狀態，不以點擊次數切換。
- 原S8完整OwnStatus是角色屬性絕對來源；S225/S33延續原生命／魔力。AC等不由裝備表數字直接加減。原rebuildAbility另有greater、bless等來源；不可把模板環甲-3／木盾-1當最終變化值。

原`C_ItemUSe.useWeapon/useArmor`與`L1PcInventory.setEquippedSerialized`已提取核對。穿盾後要求雙手回原訊息128；雙手後要求盾回129，保持原裝備。不同武器交換先原卸舊再穿新，中間visual依原changingWeapon規則抑制。產品不假設原服會自動卸盾。保存/載入經原CharItemsTable與原PC inventory load，測試以完整raw與另行SELECT核對。

## 原物品與武器素材

原SQL完整選定行、SQL來源hash與遷移前後語義見`reference/equipment/selected-templates.json`。31：sword/$0/icon2/desc0；52：tohandsword/$3/icon4/desc0；20096：armor/$164/icon86/desc0；20222：shield/$778/icon97/desc0。四件均王族可用、原bless0、無min/max等級限制。圖示由原Sprite.idx/PAK的TBT解碼，沒有替身圖。

固定JAR ItemTable把sword/tohandsword映射4/50，L1EquipmentSlot設置currentWeapon，gfx0王族不經chainsword特殊轉換。原BIN與已驗收資源讀取路徑將gfx0連到sprite3225。`decoded-list.spr`與`inner-list.spz`選定條目逐byte一致；對應walk/attack/damage/breath為4/5/6/7及50/51/52/53，死亡共用8。八方向逐幀與原事件尾資料保留。原spr_action走路640ms，單手攻960ms、雙手攻1120ms；受擊與呼吸用原metadata ticks/24。未支援的武器型態不以徒手替代。

本階段修正承接工具的action搜尋：非逐行regex可能從`8.7:4`誤中死亡action8。新搜尋限定行首，真正死亡96–103切片含新unsigned排序鍵4。第三byte是BIN排序鍵而非色彩mode，參照P04原始byte核對證據；新解碼保留該鍵。P20原/武器共104實體SPR、816幀均經獨立原Lin.Helper.Core.LoadRaw與新建runner逐RGBA/邏輯座標比對。這不改寫P19歷史，不等於原BIN執行期或資源發行版本配對已證。

## 證據區分

`reference/equipment/oracle.tsv`是14組明示合成輸入呼叫原serializer；`evidence/equipment-tests-final.json`及`equipment-gates.json`驗證截短、重複絕對狀態、取消、世代、選取改變、原名稱更新、移除、死亡與忙碌。合成測試不是原互斥動態證明。`equipment-original-poses.png`已實際檢視，是原參考像素圖集，不是遊戲截圖。

真實流程從兩端首seed前開始擷取，所有世代sequence/applied邊界、尾包、原拒絕與保存重登保留。`verify_equipment.py`獨立LE/CP950重建S8完整屬性、S119、S64及原名稱裝備標記，逐快照對照後另核保存SELECT；`verify_rewards.py`獨立核對擊殺、原EXP／金幣與有限藥品。UI用真正WinForms controller/OnKeyDown/WebView2 DOM，不能稱為外部OS鍵鼠。

原內部 bless 與 wire 值不同：`L1Item.toPacketBless` bytecode offsets0–14 將正常0轉成wire1、祝福1轉成wire0。產品指紋使用wire1，不直接套SQL值。這已在首次真實流程前修正，原始建置紀錄保留。實際S8基線AC=-1，環甲後-4，環甲加盾-5；這是原回包觀測，沒有本地套用模板效果。

同樣修正P20狼gfx1110的inline action解析：action標記後必須為字母，避免把frame8.0誤當action8。真正死亡40–47、26ticks（1083ms）經40實體SPR/248幀獨立像素核對。原死亡停止、移除清目標與不可繼續攻擊規則保留；舊工具解析錯誤記錄見evidence/action-token-fix-before.json，不覆寫前階段歷史。
