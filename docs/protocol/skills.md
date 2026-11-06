# 技能施放與職業索引（T100／#187）

本節於 2026-11-07 唯讀核對主伺服器來源。共同契約讀自 `task/186` 的 `docs/design/m6-contract.md`（Git blob `51b1c76e1a0512a50c7dd10c2b22af6548ffb58f`）；本任務的 main 基底尚未包含它。承接 [#132 差異 3](../reports/issue-132.md) 與 [伺服器封包表](../reference/server-protocol.md)。以下是來源格式證據，尚未證明與 P24Lab 部署 JAR 同版。

## C6 格式

共同前綴：`C(6), C((skillId-1)>>3), C((skillId-1)&7)`。H 為 16-bit、D 為 32-bit little endian，S 為 CP950 字串加 NUL。下表不含共同前綴；送出時補零到 8-byte 邊界，不含 frame 長度與加密。伺服器 `C_UseSkill.start` 限制技能上限 220，並檢查 template 存在、角色已學、狀態、消耗與目標；客戶端接受 S164 已學清單不代表伺服器必准許施法。

| 技能 id | 尾段 | 意義 |
|---|---|---|
| 116、118 | S | 盟友名字；不能送物件 ID，不能空字串 |
| 113 | DHHS | 物件 ID、X、Y、標記文字（可空，但仍需 NUL） |
| 5、69 | HD | map、bookmark ID；不是物件 ID。明確傳 bookmark 0 才表示無指定書籤 |
| 58、63 | HH | X、Y；沒有 DWORD 前綴 |
| 4、6、7、10、15、16、17、18、22、25、28、30、34、38、45、46、50、65、74、77、108、132、184、187、192、194、202、203、207、208、213 | DHH | 物件 ID、X、Y |
| 其餘 1–220 | D | 物件 ID／自己／無目標；51 的 D 在裝備召喚控制戒指時為召喚選項，沒有戒指時只消耗欄位、不當世界物件 |

依據：`ClientPacketStructure.validateSkill` 180–194、`C_UseSkill.start` 的技能 switch。原生短式只有 row／column（總長 3 或 4）也被伺服器接受；本引擎保留一致的補齊政策，無目標採 D=0，自己採 D=caster。不要先補短式到 8 bytes 再假設它仍是短式：伺服器會依技能進入完整尾段分支。舊 1／3／8 的 16-byte 包收斂為 8-byte，4 的 DHH 仍為 16-byte。

`L1SkillUse.checkUseSkill`（428 起）會將 template `target="none"` 解析為施法者，58／63 則使用座標；`handleCommands` 最後執行技能邏輯。D 只是格式，不足以推斷能否對敵人、盟友或自己生效。41／61／75 可從新入口提供目前可見的屍體目標；一般攻擊仍拒絕死者。治療、傷害、buff 與 MP 一律等伺服器回報。

## 引擎介面與資料邊界

- `SkillCast.Wire(id)` 是上述協議分派，不讀技能目錄的 `wire`。`MagicRules.Request` 委派到同一編碼器，舊 `(id, target, x, y)` overload 保留給一般 D／DHH／HH；名字、書籤必須用有型別的新參數。
- `SkillCastTarget` 的 `Kind` 可為 `None`、`Self`、`Object`、`Location`、`Name`、`Bookmark`、`Summon`。`SkillCast.Request(id, target, caster)`／`MagicRules.Request` 只編碼；**真正送出一定經 `LoginConnection.CastSelectedMagic(target)`**，它核對當前已選且已學技能、角色狀態、冷卻、物件與等待送出期間的失效條件。
- `Object` 的 X／Y 在送出入口由當前世界物件決定，不信任呼叫者舊座標。名字可指遠方盟友，不強求 AOI 物件；座標、書籤與召喚選項不會誤當世界 ID。特殊參數為每次呼叫的 immutable record，不存到下次施法。
- 無參數入口延續既有自己／指定對象選擇：DHH／DHHS 用已選物件，HH 用已選物件所在格，S 用已選對象名字，51 預設召喚選項 0；HD 沒有明確書籤時不送。若等待期間物件移動，取自物件的座標會取消；明確 `Location` 的固定格則不跟著物件移動。
- 外部 `assets/magic/catalog.json` 保留 `p24-magic-casting-v1`，可有任意受支援 id 的 metadata，不再要求恰好 1／3／4／8，也不再使用其 `wire`。repo 沒有新增技能表、SQL、Java 原文或遊戲素材。目錄可缺少：已學技能仍可編碼，名稱回退「原服技能 id」，一般 D 提供自己／物件參數；精確目標資格由伺服器裁定。
- 缺少 metadata 時 MP／effect／reuse 的 0 是未知資料的相容欄位，**不是零消耗、無效果或零伺服器 CD 的證據**；本地只用既有角色施法 cadence（DHH／DHHS 選 action 18，其餘 19）防止連發，不自造技能冷卻表。各職業任務應補外部資料、正確動畫／目標提示與效果，不把這個回退動畫當原生證據。
- 文字拒絕空盟友名、內嵌 NUL、無法編碼 CP950、超過 255 bytes；255 是本地輸入上限，不宣稱是伺服器角色名上限。座標採 ushort，bookmark／summon 採 uint，合法 map／選項及可達範圍由伺服器裁定。

呼叫例（先由原服 S164 學到並選好技能）：

```csharp
await login.CastSelectedMagic(new(SkillTargetKind.Self));
await login.CastSelectedMagic(new(SkillTargetKind.Object, ObjectId: targetId));
await login.CastSelectedMagic(new(SkillTargetKind.Location, X: x, Y: y));
await login.CastSelectedMagic(new(SkillTargetKind.Name, Name: clanMember));
await login.CastSelectedMagic(new(SkillTargetKind.Bookmark, MapId: map, BookmarkId: bookmark));
await login.CastSelectedMagic(new(SkillTargetKind.Summon, SummonId: choice));
```

現有 UI 只有原先的目標模式與物件清單；本任務未改 `MagicUi.cs`、地圖點選、名字／書籤／召喚選單。各職業任務接上上述送出入口，尤其自行選格、遠方盟友、書籤、召喚與屍體選取；不能將編碼器通過解讀為這些 UI 已完成。

## U8：七職業與基本技能

`C_CreateChar` 的 type 順序與 `PcLvSkillList` 的職業方法共同確認 **7 種職業**。下表的「基本」指來源最低職業學習階段與可用來排期的代表技能，並非全部新角色已擁有。`PcLvSkillList` 回傳的是零基 skill index，下表均已換回 **一基 skill id**。

| type／職業 | 最早一般魔法 | 基本專屬技能 id（學習門檻） | U8 任務建議覆蓋 |
|---|---|---|---|
| 0 王族 | Lv10：1–8；Lv20：9–16 | 113 精準目標（Lv15）；116 呼喚盟友（Lv30）；114 激勵士氣（Lv40）；118 援護盟友（Lv45）；117 衝擊士氣（Lv50）；115 鋼鐵士氣（Lv55） | DHHS 標記文字、S 名字、自己光環 |
| 1 騎士 | Lv50：1–8 | Lv50：87 衝擊之暈、88 增幅防禦、90 堅固防護、91 反擊屏障 | 指定對象、盾／雙手武器條件、狀態；89 尖刺盔甲有常數，但此學習方法未列入 |
| 2 精靈／妖精 | Lv8：1–8；後續每 8 級一階，至 Lv48 的 41–48 | Lv10：129 魔法防禦、130 心靈轉換、131 世界樹的呼喚；Lv20：137 淨化精神、138 屬性防禦；Lv30 加 132 三重矢等 | 自己、防禦、HP／MP 回報、三重矢；屬性分支另核對 1地／2火／4水／8風 bit mask |
| 3 法師 | Lv4：1–8；後續每 4 級一階，至 Lv40 的 73–80（24 留空） | 1 初級治癒術、2 日光術、3 保護罩、4 光箭、5 指定傳送、6 冰箭、7 風刃、8 神聖武器 | 自己／物件、DHH、HD；進階 58／63 座標、51 召喚、61／75 復活可在同職業任務界定 |
| 4 黑暗妖精 | Lv12：1–8；Lv24：9–16 | Lv15：97 暗隱術、98 附加劇毒、99 影之防護、100 提煉魔石、109 力量提升 | 自己、材料／物品條件、隱身與狀態；Lv30 101–104＋110，Lv45 105–108＋111 |
| 5 龍騎士 | 此職業方法沒有一般魔法列表 | Lv15：181 龍之護鎧、182 燃燒擊砍、183 護衛毀滅、184 岩漿噴吐 | 自己／攻擊觸發／DHH；Lv30 185–192，Lv45 193–195 |
| 6 幻術師 | 此職業方法沒有一般魔法列表 | Lv10：201 鏡像、202 混亂、203 暴擊、204 幻覺：歐吉、205 立方：燃燒、206 專注、207 心靈破壞、208 骷髏毀壞 | 自己、物件、DHH、立方效果；Lv20 209–212，Lv30 213–216，Lv40 217–220 |

依據：`PcLvSkillList.isCrown` 479、`isKnight` 455、`isElf` 337、`isWizard` 278、`isDarkelf` 241、`isDragonKnight` 217、`isIllusionist` 188；名稱與 id 核對 `L1SkillId`。騎士方法還在 Lv60 重複加入 90，這是來源現況，沒有自行修成另一技能。

創角自動授予：目前 `C_CreateChar` 只對法師呼叫 `spellMastery` 授予 **4**。其餘職業不能因這份表就當作新建角色已學。龍騎士／幻術師另有創角開關；本任務不讀 config、不連 DB，部署是否開放及測試角色實際已學狀態待 M6 smoke 查核。建議主控依 U8 開七張職業任務，以原服已學回包與必要的測試準備作驗收前提。

## 來源識別

共同唯讀根：`D:/lineage開發/1-伺服器/src/com/lineage/`。本表是 Java 來源行號，沒有 BIN 位址或 RVA／VA 主張。

| 相對路徑 | SHA-256 |
|---|---|
| `server/clientpackets/C_UseSkill.java` | `4932c6dd71b330d91b75c4fcbf02c30ffa4712c35d7cb07537626159038e168e` |
| `server/clientpackets/ClientPacketStructure.java` | `ca88c05673188ec1b99a961be0d67561808f9bd42adf8a937b9ebda6767e96a9` |
| `server/model/skill/L1SkillUse.java` | `2fad1a69076c29704c8ea9a41361aff357f22b0e895223a6bb7b943a4879015d` |
| `server/clientpackets/C_CreateChar.java` | `400c2c4d48b63d0c73fb07a736dde7a5c5b3a03159ed063b76aff3cbe09eae72` |
| `server/model/skill/L1SkillId.java` | `5b3dafdac87f504efdd11f70f207999badd7ae8f35b4370b513d4bdcbd678a14` |
| `list/PcLvSkillList.java` | `34a02a1e6d7f0aef0f8ead96bffa521466d336431aeeda808aa157d5346e640d` |
