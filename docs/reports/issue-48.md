# Issue #48：S87 地面物品分類與 gfx 靜態資源契約

## 驗收調整

依 [Issue #48](https://github.com/chaoren0418-arch/tw-bin-engine/issues/48) 雲端主控於 **2026-11-05** 的裁定（本輪派工原文）：

> 本輪只收尾，不再補證。LiTo.bin 有 Themida section，缺少的三組 dispatch tables 與分類指標是在執行時才初始化的，而啟動或附加遊戲去 dump 是禁止的。
>
> 本專案是重新實作，不需要複製原生分類表。地面物品改成「伺服器權威加已驗證原圖」：物品是否存在、gfx、數量，以 S87 與 #49 的伺服器模板為準；顯示用本任務已驗證的 sprite pins；兩者都齊全的物品才啟用，不猜、不借圖。
>
> 原生全鏈改成明列的 unknown，不再列為驗收條件。#51 的前置依此調整。

**新的驗收**：報告結論與 pins 清單齊全、unknown 明列、沒有改動任何產品或資源。審查依本節判定，不再要求 S87 → 原生分類表 → factory → 拾取 predicate 全鏈。

日期：2026-11-05。分支：`task/48`。本輪工作目錄：專案本體 `D:\lineage開發\TW1608262200研究`。沿用遠端 commit `dbdf35cbceafd81691986dbda86ef5ee6de29707` 的既有證據，只更新本報告；沒有重跑掃描、解碼或補證。Themida 執行期初始化的說明引用主控裁定，不冒充本輪動態觀測。

以下保留前三次的資源 pins 與歷史查核記錄；其中「本輪／本次」依各次標題解讀。歷史的 `qualified=false / needs:claude`、原生閘門未通過及補件要求均屬舊驗收，已由本節取代，不再構成 #48 收尾或 #51 的原生前置條件。

## 結論

**資源 pins 已核定；原生分類全鏈 unknown（Themida 執行期初始化，靜態不可得）**。

#51 採用「伺服器權威加已驗證原圖」：逐項對接 S87 與 #49 已核定伺服器模板的存在、gfx、數量，以及下列 sprite pins；兩者齊全才啟用。原生分類全鏈不再是驗收條件，保留 unknown；不能由 type9、名稱、圖尺寸或可解碼結果自行授權其他物品。

第 3 次曾補上 S87 gfx getter、count 寫包位址與第二、第三組缺表；第 2 次曾核對 212 個 native 片段與 PE 映射。這些是既有靜態證據，本輪不重跑，也不宣稱已證明原生分類或拾取全鏈。

1. 更正 #42 的缺圖結論：同 SHA 的主索引實際存在 **`3247-0.SPR`**（大寫副檔名）。小寫 byte-exact 查找會漏掉；既有線性 ASCII-fold 查找與 `Archive.find(b'3247-0.spr')` 二分查找獨立一致，均為 record 43801。不是檔案更新、不是借圖。
2. 7、3247、肉 36、箭 66，以及 20/41/43/45 藥水標籤候選的 8 個原始 SPR payload 均可定向讀取並由既有 Python decoder 在記憶體解析，共 10 幀；提供原始 SHA、File Offset、幀與 RGBA pin。未交付或複製 SPR／PAK／PNG，未修改資產。
3. `102.type(9)` **尚不足以授權泛用地面辨識／拾取**：既有證據只核定文字欄位，未證明 S87 → gfx lookup → type table loader → object factory → 滑鼠／C112 准入全鏈；也沒有證明 type9 是原生充分條件或必要條件。新架構以伺服器權威與原圖 pins 決定支援範圍。
4. gfx 7 與 3247 皆有獨立文字定義和原圖，但它們之間沒有本輪可證明的 alias、數量切換門檻、伺服器選用規則。不可因圖大小不同就猜單枚／多枚；金幣真實 gfx／count envelope 留待 #49 的證據及後續核對。

## 第 1 次執行範圍（歷史記錄）

- 先核對 Issue #48：YAML、標籤及調度器留言皆為 `runner:dot`；本文殘留 `runner:codex-local` 不是最新改派。開始時無 RESULT、無 task/48 遠端分支及相關 PR。
- 已讀本機 AGENTS.md 與 TASKS.md，隔離目錄 AGENTS.md SHA 與主目錄相同。保留其他分支、worktree、未追蹤截圖與 runtime 資料。
- 只讀原客戶端明確指定的 TXT、17 個 Sprite IDX，以及 8 個 PAK bounded slices；讀現行四份相關產品原始碼及允許 `work/` 中既存靜態證據。
- 沒有讀正式 BIN、封存 release、assets junction、config/server.properties、docs/REPORT.md；沒有連 DB／服務、操作遊戲程序、build 或修改產品。隔離 worktree 使用 sparse checkout，未實體化 docs/REPORT.md。
- 完全存取模式**未驗證**；本環境是 workspace-write／網路受限。`git fetch origin main` 的自動審查曾因 runner 身份判定拒絕；讀回使用者對 dot 開立本機 Codex、task/N push 與 RESULT 的明確授權後，同一動作成功。未以其他方式繞過拒絕。

## 原始 metadata 身份、alias 與例外

來源 `D:\lineage開發\0-客戶端\TW13081901.txt`，5,990,880 bytes，SHA-256：
`28c4fc600833fb8c609ab195a67ff926e8ffa571100d8d4b389a23be8c2c8db8`。
下表 offset 是原始 TXT 的 **File Offset**，不經換行正規化，不是 BIN RVA／VA；block hash 包含至下一個 gfx header 前的原始換行。

| gfx | header | File Offset | type | 原始 block SHA-256 |
|---|---|---|---|---|
| 7 | `#7	1	adena` | `0x1e04` | 9 | `19a5b6791c3eb188f20e7659fcaeb729c985b1e380e2c770ecf8a98ca78aa740` |
| 20 | `#20   1       potion blue` | `0x2002` | 9 | `65d6a80ab7848dbb8d333467d6fc049cf7dbd5830f4928b099310785a36805ea` |
| 36 | `#36   1       meat` | `0x2ad7` | 9 | `9cbf474b54ab22901a5fc8f7f0fcb8ffbc581531b8e6c5ec1f4039e532e349ad` |
| 41 | `#41   1       potion green` | `0x39aa` | 9 | `9e5b9bddbc9074ff9688c782d767d0211025454c6c250db782b933afbd988042` |
| 43 | `#43   1       potion orange` | `0x3a04` | 9 | `eac5dcc88cd561ed02ce0b428efbd1ca08eabedb63110e03ba2f508fa060ded3` |
| 45 | `#45   1       potion red` | `0x3a5a` | 9 | `eccb0fb1a70307834f8084fa496c4825015c52b490cafa646d325b1ce1524fbe` |
| 66 | `#66   8       arrow` | `0x6d38` | 9 | `7a32919180e112abe38b3c19a6616bfbd83d5c0fbc863736e6b8f088d79d9c52` |
| 3247 | `#3247	1	new adena` | `0x954d2` | 9 | `f90b427dbb3d067233b098dca777d50420696272c4eee089020784c525f937d1` |

這 8 個 header 都沒有 `=sprite` alias。7、3247、20、36、41、43、45 的 block 僅列 type；這不是已核定的 native default frame 選擇語義。66 明列 `0.fly(1 3,0.2:1 0.0:1 0.1:1)` 及 `109.effect(3 -1)`；不可把 fly 動畫直接當地面動畫。

定向 parser 在全份 TXT 找到 1,671 個僅含單一 `102.type(9)` 的 entry，其中 1,655 個未列 action 編號 0..99；有 3 個 header alias：

| gfx | File Offset | 原始 header | 限定意義 |
|---|---|---|---|
| 256 | `0xe40a` | `#256 1=253 ghast L green` | 文字 alias=253；不由英文標籤判物種 |
| 258 | `0xe691` | `#258 1=253 ghast brown` | 文字 alias=253；不證明可拾取 |
| 2884 | `0x83a03` | `#2884 8=122 box` | 另有 28.open／29.close；type9 不等於所有 entry 都只有靜態幀 |

這些是「只按 gfx 拼檔名、只按 type9 套一種顯示規則」的資料層例外，**不是已證明的 native 非物品反例**。未知 type、type 繼承、未載入表、未知 alias、名稱相同、Flags/PC 位、gfx 重用均不能自動通過。所有 type9 的 runtime eligibility 仍為 unknown。

## 3247 缺圖更正與完整索引查核

主索引 SHA 與 #42 完全相同：`327c06372d87f6cf6d5176da51da5ef1f94dbf388d1588b735c5965b96b6a52b`。
record 43801（0-based），IDX File Offset `0x12b6c0`，原始 28 bytes：

```text
36528a1b333234372d302e5350520000000000000000000059000000
```

名稱 bytes 為 `3247-0.SPR`，PAK File Offset `0x1b8a5236`，長度 89；`3247-0.spr` 的區分大小寫相等比較不會命中。本輪全掃 17 個索引，以 ASCII-fold 搜尋 `3247-`，僅此一筆。既有 work reference reader 的同名二分查找亦命中；這是現有靜態工具一致性，未重新證明整個原生 archive runtime。

各索引驗證 `4 + 28*count == 檔案長度`、名稱 NUL 結尾、全部 record 的 offset+size 不超對應 PAK。所有 17 個索引重算 SHA；**未重算 PAK 整檔 SHA**，以下為 current IDX + bounded payload 身份，不冒充整個客戶端與歷史 BIN 同版。

| IDX | records | SHA-256 |
|---|---:|---|
| Sprite.idx | 92356 | `327c06372d87f6cf6d5176da51da5ef1f94dbf388d1588b735c5965b96b6a52b` |
| Sprite00.idx | 10207 | `15b46ae6995b5b3d64a27a509c78847f0329ba55360a200961a7ecb73b13c96c` |
| Sprite01.idx | 10232 | `1e4c819debe37449691142025692ac1ff1d1f82c148830c76338e7fdff8cdd8f` |
| Sprite02.idx | 10320 | `a0208077cfa7e76280641f1a2bf7f7aeacc43b6f9a16e21771ebd732e791ae5c` |
| Sprite03.idx | 10457 | `4deb893637d40e1a9e91844a4a3274f9de25e7af42197e0ac69773241f233bf6` |
| Sprite04.idx | 10591 | `6ea2246d89bfd1dc7ee3d4b70a36f0bf700889d1eb5f4cad9ab633a72bf539f9` |
| Sprite05.idx | 10697 | `f4c6d5d7974ad355b066faf620aea9428104fa64274b5eeb24ce60bf54030158` |
| Sprite06.idx | 10840 | `0248238be94b99e61d2c515d59066b445af9faf5b3430132bbdce706d6812bcf` |
| Sprite07.idx | 10945 | `ce996c526474647be2986c2610afe2e6b193f9d24069033615ffafb3a9136a60` |
| Sprite08.idx | 10968 | `4f96ce84bcb0f733842b28b3dcb39984828985f6af994ce88aae214162782022` |
| Sprite09.idx | 10966 | `37edc3cbfb37ae667aa61a6ff3c141b7a7b351f60b6990d3bcee5c5eb55f8f9a` |
| Sprite10.idx | 10858 | `589c47ecb1a56dc9d0956ad7716e599a00f68290005f401bbc595879e253ecb1` |
| Sprite11.idx | 10712 | `4d560607d2b290992d0cd6beaa7556cd6d03a009346418915636c01e06c20e88` |
| Sprite12.idx | 10519 | `ada81449a16e323f90539cdf0571e59f120c52629855a9fe536e19c8e34481fe` |
| Sprite13.idx | 10355 | `e03cf669b50ee8fb86ac4e3242fb68a211580f78f8929770a9077d4fa776203b` |
| Sprite14.idx | 10254 | `c6d10d9c14ecac20c5b96546f9b4494eb23cc9b28c1ab07a96070eb574e84c9a` |
| Sprite15.idx | 10184 | `874b53afe762c43415e25204f4e8fcf405f36adfaca6b9258bd84c1eb1d4c441` |

## 可供 #51 使用的已驗證 sprite／frame／source pins

以下 **8 個 payload／10 幀** 沿用前三次已驗證記錄，本輪只整理交接，未重新讀取資源。`3247-0.SPR` 是 **0-based record 43801**，必須保留大小寫更正。此清單核定原圖來源；物品啟用仍須逐項具備 S87 與 #49 的伺服器模板證據。

全部 entry 在 `D:\lineage開發\0-客戶端\Sprite.pak`，既有查核尺寸 1,904,012,586 bytes；對應 `Sprite.idx` SHA-256 為 `327c06372d87f6cf6d5176da51da5ef1f94dbf388d1588b735c5965b96b6a52b`。PAK offset 與 IDX offset 皆為 **File Offset**，不是 RVA／VA；下方幀 header File Offset 相對於各 SPR payload 起點。PAK 整檔 SHA 未驗證，身份以 IDX 與 bounded payload SHA 為準。

| entry（保留原大小寫） | IDX record／File Offset | PAK File Offset | bytes | payload SHA-256 |
|---|---|---|---:|---|
| `20-0.spr` | 19937／`0x884a0` | `0x3d0d0fb5` | 176 | `c891f13f73e28b387042408a6c0d4756da0ce21f227ba8f3c5b96d9702ac25d3` |
| `3247-0.SPR` | 43801／`0x12b6c0` | `0x1b8a5236` | 89 | `52420e660d6a0997067a75cdf7855bcfddcf7f2a9b7f9142c304896a1ed7b26c` |
| `36-0.spr` | 48732／`0x14d214` | `0x39e4eb23` | 244 | `1734424f17adb848324f8e6c2aba6ad4bafeb687f990aa59b62426d448899ee8` |
| `41-0.spr` | 55893／`0x17e150` | `0x39d213f9` | 176 | `296b8bf0c6dafd0f438da13823d3df29b074c603ccb9a69c3267631d8f8e7d9b` |
| `43-0.spr` | 59171／`0x1947d8` | `0x39d21299` | 176 | `93e5eebaa451d38b89fa272449bcb984b72336bbe5b0ee2b29035e344082d189` |
| `45-0.spr` | 62818／`0x1ad6bc` | `0x39cb54c6` | 174 | `89ccae52b20c83ea26bdb9906f7c2c0bb1dde411bf3e59fd140fd39a01d69ca9` |
| `66-0.spr` | 85892／`0x24b274` | `0x39648333` | 1056 | `4795b0d2150d7d075d753efc378799b8d471e52d32cdce47eec77fa502bb758c` |
| `7-0.spr` | 86493／`0x24f430` | `0x395ce3ed` | 307 | `65b754770b1915eb070e488ccb010ee2662741a09346f1d1be36fc3faadf2ce2` |

採用現有 `work/engine-pickup-asset-source-r1/decoder.py`（SHA `dc475754e578844493aa34fc2f21a80827dee3c4ce5af852e79c6b5c78895986`），僅呼叫 `decode(payload)`，不走 CLI 寫圖入口。它驗證 palette／direct RGB555、header、RLE 邊界、block offsets、expanded size、完整消耗 bytes、像素 bounds。RGBA 為 RGB555 bit replication，未寫入像素 alpha0，寫入像素 alpha255；未以 native renderer 或第二個 decoder 重驗像素。

| entry／frame index | SPR header File Offset | inclusive bounds [L,T,R,B] | RGBA SHA-256 |
|---|---|---|---|
| `20-0.spr`／0 | `0x21` | `[20, 4, 28, 14]` | `90b1d38e6b1fba68c9705c95549796c82a36db27633efc93330666351599341e` |
| `3247-0.SPR`／0 | `0x1` | `[22, 10, 26, 13]` | `016b589e7f8b09054c8219d16d0a566c7658bb1c92bb28dc69d7007b916181a5` |
| `36-0.spr`／0 | `0x59` | `[18, 5, 29, 14]` | `edcb31e9aca0869a8371a0aceb3fbf38b8dcecd221163a7178253d99587506c9` |
| `41-0.spr`／0 | `0x21` | `[20, 4, 28, 14]` | `ab1667031fefb4fe7ffb6fb0fdeba3a4f1b02eda3841d483478a9f813ae8335f` |
| `43-0.spr`／0 | `0x21` | `[20, 4, 28, 14]` | `940a2a55011cc2544e59ff0e65e8188ba7898b838cc9a1e7e74a5feead8e1ac0` |
| `45-0.spr`／0 | `0x1f` | `[20, 4, 28, 14]` | `157926b43d9e7083c5e446df64a4d248fbf498de87935c97cced7d44bbdefc17` |
| `66-0.spr`／0 | `0xc5` | `[3, -41, 39, 8]` | `9d2fabf6b13d9f0a3b8b062c3edf1e7edba6ba3b1cd023cc8fe7946ff1b6d9c6` |
| `66-0.spr`／1 | `0xf1` | `[7, -22, 39, 13]` | `f3e6c359c9114292161f6324a1c0f577e6b02cf5b6e4e23675311776ee426477` |
| `66-0.spr`／2 | `0x122` | `[10, 3, 39, 18]` | `7f723b2ab663a65160390c122d6d2d5475276b9f81ed86648be387256797c690` |
| `7-0.spr`／0 | `0x81` | `[18, 5, 32, 15]` | `6eb4782f2c6c01d4e5c6b76494bf830e356175f026c64a627b423302e949786a` |

可供下一棒 pin 的候選靜態幀為各 `sid=0/frame=0`；66 另外存在 frame1/2，但目前地面 renderer 固定 `0:0`，不因有三幀就自行播放 fly。3247-0.SPR 為 direct RGB555、1 幀、5×4；7-0.spr 為 indexed、1 幀、15×11。以上是解析結果，**不是金幣數量外觀規則或原生畫面驗收**。實作時須保留 bounds、tile A/B、sort key/source order、RGBA pin、透明與 anchor（-L,-T），不可裁改以迎合預期外觀。

肉／箭的 gfx36/66 對應源於既有 GroundRules `$23`／`$61` envelope，沒有重做真機。20/41/43/45 只核定 TXT 英文標籤與原圖；精確中文道具／背包 template／數量／可丟棄語義須由 #49 提供，不能按顏色猜配對。

## 允許 work 證據中的 native 限界

來源目錄 `work/engine-pickup-renderer-eligibility-source-r1/`。`source-pins.json` 記載歷史完整 BIN 為 16,810,496 bytes、SHA `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`；本輪**沒有開啟或重驗該 BIN**。以下從已允許 work 的 `.bin` slices 重算雜湊並對照 `native-byte-audit.json`（adopted=true），地址映射沿用其歷史記錄，不能套用到 #42 的 LiTo.bin。

| work/native artifact | VA | RVA | 歷史 BIN File Offset | bytes | 本輪 slice SHA-256 |
|---|---|---|---|---:|---|
| `native/00a467f3.bin` | `0xa467f3` | `0x6467f3` | `0x645bf3` | 392 | `117cb47db4be97c7040c18652ab8b0d4b743edfe0f8083d4b867ebd0a7327536` |
| `native/00a44500.bin` | `0xa44500` | `0x644500` | `0x643900` | 683 | `82c8617b2c06e33cb4518fabca2b6afc29fb50b73e6b667f96999c50d7f6015f` |

對照已存的 disassembly：VA `0x00A4453B` 與 `0x00A467FA` 讀取 `0x013CD838` 指標，按 actor+0x0A 的 signed word 索引取 byte，並比較 11；VA `0x00A4694A` 讀 `0x013CD878` 指標後比較 1。這些證據僅支持「既有 action selector 有分類相關查表分支」，**不是 S87 handler、不是 type9 判斷、也不是地面 object factory**。指標所指表的 loader／完整內容未在本輪成立，不把 pointer VA 當原始資料表 File Offset。

原 HANDOFF 與 source-contract 也明列文字 type5 尚未連到 native table bytes。完整 S87 gfx lookup、102 loader、object factory、mouse/C112 predicate、zero count、金幣數量門檻、地面 alias 繼承仍為 **unknown**。本輪不開啟其他原 BIN、不啟動／附加程序來填補缺口。

## 可辨識／可顯示／可拾取的獨立條件

| 階段 | 本輪可核定的現行產品條件 | 本輪未核定的泛用 native 條件 |
|---|---|---|
| 可辨識 | WorldObjects.ReceiveObject 讀 S87；ObjectId 非0、Direction≤7；GroundRules 只接受 gfx36/66 與完整欄位、名稱/count envelope | type9 到 factory 鏈；不可僅由 Name、Flags、IconId 或 DescriptionId 判 species/type |
| 可顯示 | GroundRendering/ GroundUi 僅放行36/66；需要正確幀與既有 scene 範圍、pixel blocks、選取資訊 | 本輪取得其他 gfx 原圖不代表產品已載入；native ground default frame、alias 與數量換圖未知 |
| 可提出拾取 | GroundAvailable、有效選取、count在1..min(地面量,int.MaxValue)、距離≤3、非忙碌／交易／死亡、owner/world/selection lease 有效、900ms guard／3000ms pending；送 C112 | native 自身的完整 input／factory／type／例外 predicate 未核定 |
| 可宣稱完成拾取 | 地面與背包權威回包更新；S87 WireExp=0 在目前受支援 envelope 表示移除 | sender 成功、S158/action15、動畫完結都不能單獨證明財物結算 |

現行 GroundRules 的 WireExp 上限 2,000,000,000；Name 在 WireExp>1 時必須 `$23 (N)`／`$61 (N)`，否則為裸 token。其餘 Action/Direction/Light/Speed/Lawful/Flags/ClanId/Rank/Brave/Level/Unknown=0；Title/Clan/Owner 空字串；HpByte/Tail1/Tail2=255。這是**現有有限產品契約**，不能提升為所有 type9 或金幣的伺服器證據。

數量外觀：兩個 gold block 不含 count 門檻或相互 alias；兩個資源各僅一幀也不排除外部 native/server 邏輯按數量換 gfx。故 count→gfx/frame、7↔3247 的關係一律 unknown；禁止使用「缺圖 fallback 7」或借肉／箭圖。

## 第 1 次驗證記錄（資源 pins 已於第 2 次重驗）

- 一次性唯讀腳本 `scratch/issue-48/inspect_static.py`，輸出 `scratch/issue-48/findings.json`；此報告保留必要 pin，scratch 不是審查依賴。
- 本輪實測結果：8 個 TXT entry、17 個 IDX 完整結構與範圍檢查、8 個 bounded SPR／10 幀 decode、2 個既存 native slice hash 均通過；3247 的線性／二分查找同 record。沒有執行遊戲功能測試。
- 未重驗完整歷史 BIN 身份、PAK 整檔 SHA、native loader/factory/input 全鏈、像素獨立解碼一致性、原生顯示、滑鼠、真機丟撿、數量外觀門檻、伺服器模板／實際封包。

最小可重現更正（Python 3，唯讀，不寫出 sprite）：

```python
from pathlib import Path
import hashlib, struct
root = Path(r'D:\lineage開發\0-客戶端')
idx = (root/'Sprite.idx').read_bytes()
assert hashlib.sha256(idx).hexdigest() == '327c06372d87f6cf6d5176da51da5ef1f94dbf388d1588b735c5965b96b6a52b'
count, = struct.unpack_from('<I', idx)
assert len(idx) == 4 + 28*count
matches = []
for n in range(count):
    off, field, size = struct.unpack_from('<I20sI', idx, 4+28*n)
    name = field.split(b'\0', 1)[0]
    if name.lower() == b'3247-0.spr':
        matches.append((n, off, name, size))
assert matches == [(43801, 0x1b8a5236, b'3247-0.SPR', 89)]
with (root/'Sprite.pak').open('rb') as f:
    f.seek(matches[0][1])
    payload = f.read(matches[0][3])
assert len(payload) == 89
assert hashlib.sha256(payload).hexdigest() == '52420e660d6a0997067a75cdf7855bcfddcf7f2a9b7f9142c304896a1ed7b26c'
print(matches)
```

## 第 1 次相關來源 SHA-256（非全部於第 2 次重讀）

下表為本輪讀檔 pin，包含既存的研究文件與產品碼；不代表它們記載的所有歷史主張都重新驗證。四份產品碼及 #42 設計文件與本輪 origin/main 對照沒有差異。

| 路徑（repo-relative） | bytes | SHA-256 |
|---|---:|---|
| `AGENTS.md` | 6143 | `7c2dc18d8ce8a00a0eef819de940e93772e97b8ff8967915b01fa7a3319a737e` |
| `docs/design/sfx-and-ground-items.md` | 23441 | `9ca8c9807fa2556cafd46a55c1c91466ae2bc96e50b49a321d7c324cb55ed6fc` |
| `engine/source/Ground.cs` | 9418 | `6df52d49787795c432309ed0b02deaacc2a582f4927f98b9b186f89fda06a599` |
| `engine/source/WorldObjects.cs` | 5869 | `d86aa50b81d36a9353b1d0910cccdbf18affca56e555b3080cb8499cfb22e680` |
| `engine/source/GroundRendering.cs` | 1566 | `a0a13e18ec6702cdb8f0deb6011586b20667c598c5010b46016b8844ce141e54` |
| `engine/source/GroundUi.cs` | 3799 | `d1fc05f61dbeef2df739cc01d4b5bddf6e414e07997401f14332c659124f845d` |
| `work/engine-pickup-renderer-eligibility-source-r1/HANDOFF.md` | 9470 | `f2ebf1b62d667dca6940c267e956e803f9bd3213d5a37d995f7ebec6e7b4f962` |
| `work/engine-pickup-renderer-eligibility-source-r1/source-pins.json` | 12018 | `1cabd2f93fee2a6d7ab4b2686ac409dd2591d595140fc93d290ca6d505a60600` |
| `work/engine-pickup-renderer-eligibility-source-r1/native-byte-audit.json` | 19036 | `5a05426afe5b363d0393c86a4e760c8057be5ca5fe500ccdc86ff7837d7dbb71` |
| `work/engine-pickup-renderer-eligibility-source-r1/proof-index.json` | 26634 | `9c0237939231af614f00fce203c0099082924a3f4a8e13ce3ba17830ff0a9a18` |

## 第 2 次補證：做了什麼／怎麼驗證

本次已讀 worktree 根 `AGENTS.md`、`TASKS.md`；因 sparse checkout 沒有實體 `docs/REPORT.md`，改唯讀原專案同路徑的索引。GitHub #48 已核對為 `runner:codex-local`、`attempts:2`，不是前次的 dot 派工。未修改任何規則、索引、產品、封存、lock 或原始資源。

執行環境的 sandbox process 初始化失敗（`helper_unknown_error: setup refresh had errors`），明確限定命令經自動審查放行後才執行。Git ownership 差異只用每次命令的 `-c safe.directory=...` 處理，沒有修改家目錄或全域 Git 設定。這是工具啟動問題，**本次沒有自動審查拒絕造成的證據存取阻塞**。

### 資源重新核驗

- Python 3.12.10，Capstone 5.0.6；檢閱既有 decoder 後，只 import `decode(payload)`，未執行其匯出 CLI。
- 17 個 IDX，共 **261,461 records**：整檔 SHA 與上表一致，長度 `4+28*N`、名稱 NUL 結尾、每筆 offset+size 不超相應 PAK 全部通過。
- 8 個 bounded payload／10 幀：payload 與 RGBA SHA 全與上表一致；8 個 TXT block 的原始 File Offset、block SHA 亦重驗一致。
- `3247-0.SPR` 再次命中主索引 record 43801、File Offset `0x12b6c0`；payload 89 bytes、SHA `52420e660d6a0997067a75cdf7855bcfddcf7f2a9b7f9142c304896a1ed7b26c`。前次「缺圖」原因仍是大小寫精確比較，不是資源換版。本次沒有重跑前次 reference 二分查找；採獨立線性 ASCII-fold 掃描。
- 只保留文字與雜湊，不寫出 SPR、PAK、map、PNG 或其他遊戲資源。

### 原生身份隔離：已驗證

本次唯讀 `D:\lineage開發\0-客戶端\LiTo.bin`：1,941,568 bytes，SHA `ce6ee68ada8fd37e6c096b1a27e46cb6888f1179f85defb5007ac60287360292`，與 #42 一致。PE ImageBase VA=`0x00400000`；entry RVA=`0x0043B014`、VA=`0x0083B014`、File Offset=`0x0009C014`，落於 `Themida ` section。

| section | RVA | VirtualSize | raw File Offset | raw size |
|---|---|---|---|---|
| 第一節（名稱 bytes `2020200020202020`） | `0x1000` | `0x436000` | `0x1000` | `0x98000` |
| `.rsrc` | `0x437000` | `0x28c8` | `0x99000` | `0x2000` |
| `.idata  ` | `0x43a000` | `0x1000` | `0x9b000` | `0x1000` |
| `Themida ` | `0x43b000` | `0x289000` | `0x9c000` | `0x13e000` |

例如歷史 selector 的 RVA `0x6467A0` 在這份 LiTo 會落入 Themida 的虛擬範圍，卻超出該節 raw bytes；不能套用歷史 File Offset `0x645BA0`，更不能以虛擬補零當作函式。歷史表指標 VA `0x013CD838`／`0x013CD878` 的 RVA 也不在上述 sections 內。這些是 **PE 映射與身份不相容的實證**，不是已完成解殼或已證明兩者執行語義不同。

歷史 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（16,810,496 bytes）的身份仍取自允許 work 的 source pins。本次未另讀 `D:\lineage開發\登入器bin資源\TW1608262200.bin拆解\TW1608262200.bin`；該路徑是補證候選，沒有把它默認擴入本輪列明的原客戶端／assets／repo-work 範圍。

### 擴大既存 native 證據查核：已驗證

原專案 `work/` 唯讀；只執行本 worktree `scratch/issue-48/` 的新查核腳本，沒有執行會寫封存檔的既有 collect／finalize 腳本。先搜尋 `engine-*` 頂層與 `native`／`prior` 的文字線索，再針對下列三份 audit 列出的原始 bytes 重新驗 SHA、長度、VA/RVA 關係及重疊一致性。

| `work/` 下來源 | audit bytes | audit SHA-256 | chunk 數 |
|---|---:|---|---:|
| `engine-slow-pickup-source-r1/native-byte-audit.json` | 88552 | `8f7621cbd98c70541140231ee6f6d9df87c641258f12190993cd7a5418afc878` | 101 |
| `engine-pickup-scheduler-source-r1/native-byte-audit.json` | 20323 | `c240da5b9255f4ce7f2ab96ac42df2519cb90c5edd626268cbf2a9d56792e557` | 62 |
| `engine-pickup-renderer-eligibility-source-r1/native-byte-audit.json` | 19036 | `5a05426afe5b363d0393c86a4e760c8057be5ca5fe500ccdc86ff7837d7dbb71` | 49 |

**212 個 archival chunks 的 bytes 核驗通過**，包含重複與不同長度切片；175 個不同 `(VA,end)` 範圍，聯集 **108,221 bytes**，重疊衝突 0。這不是 212 個不同函式，也不是完整 BIN 覆蓋率或 212 項語義通過。其中 `native/00a4371d.bin` 原 audit 明列 `adopted:false`（從指令中間開始），本次只驗其檔案身份，仍不採用其反組譯；另外三個 `excludedExploration` vtable 導航檔沒有列入這 212 個 audit chunks。

新增關鍵 bytes pins（以下 File Offset 都是歷史 audit 記錄，不是 LiTo 的 offset）：

| `work/engine-slow-pickup-source-r1/` artifact | VA | RVA | 歷史 BIN File Offset | bytes | slice SHA-256 |
|---|---|---|---|---:|---|
| `native/0x947530.json` 的第一 chunk | `0x947530` | `0x547530` | `0x546930` | 2957 | `8bd1ff1f11b1761065f5639805527b3a75d7fa48564c323bef59049929a28d4a` |
| `native/0xa467a0.json` 的第一 chunk | `0xa467a0` | `0x6467a0` | `0x645ba0` | 5477 | `0de7dc79cd1fe2c74e88bf26f0309c5195f4ea109d2d0fde7720e257aad79a3a` |
| `native/handler-92cb70.bin` | `0x92cb70` | `0x52cb70` | `0x52bf70` | 2000 | `81d34316824d70fa25b782a344b20c920802c95a8cae3a2a00f7bc680778d4e4` |

對 212 chunks 搜尋兩個指標的 little-endian 直接 operand，去重後只有四處；再由含指令起點的 chunk 以 Capstone 核對：

| 指令 VA | RVA | 歷史 BIN File Offset | bytes | 指令 |
|---|---|---|---|---|
| `0xA4453B` | `0x64453B` | `0x64393B` | `8b1538d83c01` | `mov edx, dword ptr [0x13cd838]` |
| `0xA467FA` | `0x6467FA` | `0x645BFA` | `a138d83c01` | `mov eax, dword ptr [0x13cd838]` |
| `0xA4694A` | `0x64694A` | `0x645D4A` | `8b1578d83c01` | `mov edx, dword ptr [0x13cd878]` |
| `0xA46EE3` | `0x646EE3` | `0x6462E3` | `8b0d38d83c01` | `mov ecx, dword ptr [0x13cd838]` |

新增的 `0xA46EE3` 分支由 actor+`0x0A` signed word 索引讀 byte，存入 `[ebp-0x13]`；後續條件分支在 VA `0xA46F36` 比較 10（`83f80a`），不是 type9 准入。四處皆為讀指標，**本語料沒有直接 operand 形式的指標寫入證據**。這不排除 register-indirect loader、其他函式或未封存區域，也不能用「沒找到寫入」證明不存在 loader。

### S87 入口缺口已定位，未宣稱核定

dispatcher 的原始指令已重驗：

```text
VA 00947616  0fb6828c829400  movzx eax, byte ptr [edx + 0x94828c]
VA 0094761D  ff2485c0809400  jmp dword ptr [eax*4 + 0x9480c0]
```

兩個指令的 RVA／歷史 File Offset 分別為 `0x547616`／`0x546A16`、`0x54761D`／`0x546A1D`。但 **`0x94828C` 起 255 bytes 的 index table，以及 `0x9480C0` 的第一個 4-byte target slot，在本次三組 audit 聯集皆覆蓋 0 bytes**。不能靠已封存 pseudocode 的 case 標籤替代缺失的 table bytes。

前期 `source-contract.json` 與 pseudocode 將 action receiver `0x92CB70` 關聯到內部 opcode226；現行產品 S158 是另一 wire 編號。這屬歷史語義證據，本次重驗了 handler bytes，**未從缺失的 dispatch table 重新證明 opcode226 對應**。同理，直接把 wire S87=87 套入歷史 switch，或把送出 C112=112 當成接收 switch 的 case112，都不成立；需先取得同身份封包翻譯／語義對應。

### 核定表：可辨識、可顯示、可拾取分開

| 待決問題 | 本次裁定 | 實作可用範圍／尚缺證據 |
|---|---|---|
| type9 是充分條件？ | **unknown，不能核定** | 已知文字欄位；缺 loader→table→factory→input 的資料流。不能降格為「type9 即可拾取」 |
| type9 是必要條件？ | **unknown，不能核定** | 尚無完整 factory 分支／alias 繼承／例外集合；不能聲稱所有非9都不是物品 |
| type9 例外 | **資料層已知，native 物種例外 unknown** | 256／258／2884 的 alias 與 66 fly、2884 open/close 保留；名稱不是 species 證據 |
| 可辨識 | **有限產品 envelope；泛用 native unknown** | 原圖存在／能 decode 不能判定物件種類；保留既有36／66完整 envelope 邊界 |
| 可顯示 | **資源可解碼；原生 frame 選擇 unknown** | 7／3247及藥水具獨立原圖；必須綁 source、sid/frame、bounds、透明與 anchor，不能把建議 `0:0` 當 native 已證 default |
| 可提出拾取 | **既有產品 gate；native input unknown** | 類別、物件生命週期、可選取性、位置／數量與輸入限制要分別有證據；圖片不是拾取許可 |
| 可宣稱拾取完成 | **權威回包條件不變** | 以地面／背包更新判定；動畫完成、sender 成功或 S158 單獨均不足 |
| 7↔3247 alias | **unknown** | 兩個文字 block 無相互 alias，只能排除「這兩個 header 明寫 alias」；未排除外部 remap |
| count→gfx/frame | **unknown** | 不能由5×4與15×11推斷金幣堆數；待 #49 server envelope 與同身份 client 分支補證 |

### 最小重現：三組 native audit

此片段只讀允許 `work/`，不執行其中的工具、不啟動 BIN、不讀原完整歷史 BIN；可貼入 worktree `scratch/` 的 Python 腳本，以 `python -B -X utf8` 執行。

```python
from pathlib import Path
import hashlib, json
root = Path(r'D:\lineage開發\TW1608262200研究\work')
folders = ['engine-slow-pickup-source-r1',
           'engine-pickup-scheduler-source-r1',
           'engine-pickup-renderer-eligibility-source-r1']
checks, memory, intervals = 0, {}, set()
for folder in folders:
    base = root / folder
    audit = json.loads((base/'native-byte-audit.json').read_text('utf-8'))
    for row in audit['chunks']:
        path = base / row['artifact']
        va = int(row['VA'], 16)
        if path.suffix == '.json':
            saved = json.loads(path.read_text('utf-8'))
            chunk = next(c for c in saved['chunks'] if int(c['va'],16) == va)
            raw = bytes.fromhex(chunk['bytes'])
        else:
            assert path.suffix == '.bin'
            raw = path.read_bytes()
        assert len(raw) == row['size']
        assert hashlib.sha256(raw).hexdigest() == row['sha256']
        assert va - int(row['RVA'],16) == 0x400000
        intervals.add((va, va + len(raw)))
        for i, byte in enumerate(raw):
            assert memory.get(va+i, byte) == byte
            memory[va+i] = byte
        checks += 1
assert (checks, len(intervals), len(memory)) == (212, 175, 108221)
for va, count in [(0x94828c,255), (0x9480c0,4),
                  (0x13cd838,4), (0x13cd878,4)]:
    assert not any(va+i in memory for i in range(count))
print('212 chunks; 175 ranges; 108221 unique bytes; no overlap conflict')
```

注意：此片段驗證歷史 audit 與現存 bytes 的自洽性，完整歷史 BIN 的 SHA／PE mapping 仍沿用來源 pins；`adopted:false` 仍不能拿來做語義反組譯。重跑時也須先比對上表三份 audit SHA。

## 第 3 次補證與來源阻塞（歷史記錄，舊驗收已取代）

### 做了什麼與查核邊界

本輪先讀 worktree `AGENTS.md`、`TASKS.md`，以及原專案的 `docs/REPORT.md`。worktree 是 sparse checkout，報告索引未實體化，原專案只作唯讀來源。Git ownership 仍以每次命令的 `-c safe.directory=.../task-48` 處理。隔離執行 helper 初始化失敗後，明列命令經自動審查放行執行。

擴查原專案 `work/` 的可讀目錄，盤點 121,655 個檔案名稱與大小。略過 16 個 reparse point，JSON 內容查核限單檔不超過 4,000,000 bytes。找到 20 份 JSON 中 30 個帶 `va/bytes/sha256` 的原始切片，各自 hash 相符。此數字含先前已讀切片及重複範圍。與既有三份 audit 的 212 個切片分別查核，均未提供下表缺失的 table bytes。只有已具歷史 audit 的 dispatcher 被採作新增語義分析來源。

遇到 11 個目錄的 Windows ACL 拒絕，未改 ACL 或進入其內容，故盤點有覆蓋缺口。被拒目錄依 work-relative 路徑列於下方。

```text
P26-authority-rebind-review-r1/synthetic-independent-fgjl3e7f
P26-pickup-integration-review-r1/synthetic-hecff5h6
P26-pickup-integration-review-r1/synthetic-tj11g_rj
P26-pickup-integration-review-r1/synthetic-zhurl291
P26-release-review-r1/synthetic-contract-fmak1edg
P26-release-review-r1/synthetic-new-tools-d4j_sxsa
P26-release-review-r1/synthetic-new-tools-w7ao7rf2
P26-release-review-r1/synthetic-new-tools-_uw75d3y
P26-release-review-r2/synthetic-independent-gifbyfat
P26-release-review-r2/synthetic-independent-q1wqqc0p
P35-release-generator-rg2-review-r1/synthetic-wi9s08by
```

初次盤點遇到 JSON 的 `bytes` 為整數的另一種 schema 而中止，修正型別檢查後重跑成功。曾用 Windows PowerShell 讀取結果，遇到 `.Config`／`.config` 大小寫鍵衝突，後續以 Python 解析。驗證結論取自修正後成功執行的結果。名稱／大小盤點、JSON 切片掃描有上述邊界，其他編碼、未讀大檔與被拒內容仍待驗證。

### S87 入口補查與仍缺的三組 dispatch tables

來源 `work/engine-slow-pickup-source-r1/native/0x947530.json` 為 91,107 bytes，檔案 SHA-256 `133b46d40caff24d5a08680a4325107ec39df66da36452ded2967e2a95635905`。採用其中第一個 raw chunk，VA `0x947530`、RVA `0x547530`、歷史 BIN File Offset `0x546930`、2,957 bytes，slice SHA-256 `8bd1ff1f11b1761065f5639805527b3a75d7fa48564c323bef59049929a28d4a`。完整 BIN 身份沿用上述 e129 pins，LiTo 身份獨立處理。

由已驗 raw bytes 自函式起點重新反組譯，補得下列指令。

| 指令 VA | RVA | 歷史 BIN File Offset | bytes | 意義 |
|---|---|---|---|---|
| `0x947E9F` | `0x547E9F` | `0x54729F` | `83e906` | 第二條路徑先減 6 |
| `0x947EA5` | `0x547EA5` | `0x5472A5` | `817df0ef000000` | 比較無號索引上限 `0xef` |
| `0x947EB5` | `0x547EB5` | `0x5472B5` | `0fb682d4839400` | 讀 index table `0x9483D4` |
| `0x947EBC` | `0x547EBC` | `0x5472BC` | `ff24858c839400` | 經 target table `0x94838C` 跳轉 |
| `0x948056` | `0x548056` | `0x547456` | `2d3b010000` | 第三條路徑先減 `0x13b` |
| `0x94805E` | `0x54805E` | `0x54745E` | `817decea000000` | 比較無號索引上限 `0xea` |
| `0x94806A` | `0x54806A` | `0x54746A` | `0fb691cc849400` | 讀 index table `0x9484CC` |
| `0x948071` | `0x548071` | `0x547471` | `ff2495c4849400` | 經 target table `0x9484C4` 跳轉 |

三組 index table 所需長度由各路徑的上限加一取得。下表的 target 只檢查第一個 slot，完整長度須依取得的 index bytes 最大值決定。

| 缺件 | VA／RVA | 本次檢查長度 | 原始 bytes 覆蓋 | File Offset |
|---|---|---:|---|---|
| 第一組 index | `0x94828C`／`0x54828C` | 255 | 0 | 待完整來源 PE 驗證 |
| 第一組 target 首 slot | `0x9480C0`／`0x5480C0` | 4 | 0 | 待完整來源 PE 驗證 |
| 第二組 index | `0x9483D4`／`0x5483D4` | 240 | 0 | 待完整來源 PE 驗證 |
| 第二組 target 首 slot | `0x94838C`／`0x54838C` | 4 | 0 | 待完整來源 PE 驗證 |
| 第三組 index | `0x9484CC`／`0x5484CC` | 235 | 0 | 待完整來源 PE 驗證 |
| 第三組 target 首 slot | `0x9484C4`／`0x5484C4` | 4 | 0 | 待完整來源 PE 驗證 |
| 分類指標儲存位置 | `0x13CD838`／`0xFCD838` | 4 | 0 | unknown |
| 另一分類指標儲存位置 | `0x13CD878`／`0xFCD878` | 4 | 0 | unknown |

S87 的同身份語義入口仍 unknown。還需 dispatcher 狀態分支、其 caller 的封包轉換／解碼、完整欄位序列與 handler bytes，才能確立 wire87 的落點。新增指令只擴充補件清單。分類指標初始化、102 parser 寫入、所指表、factory、hit-test／pickup 皆仍缺可追溯資料流。

### 與 #49 server envelope 的實際交叉核對

核對 #49 commit `2345fe7c662eb084424172d909cf1646fec33e4f` 的 `work/issue-49/contract.json`，Git blob 原始 bytes 的 SHA-256 為 `f0cda063bcf0b698d0c82ace67b63ce7673cdbf49057a5463d79dda13c317dc4`。`qualified=false`、`adoptedTemplateRows=[]`，七類 `templateGaps` 的 `groundGfx` 均為 null，金幣 7／3247 尚未裁定。#49 於 2026-11-05 07:00:41 UTC 的審查也要求補齊模板與可重播 fixture。[#49 審查留言](https://github.com/chaoren0418-arch/tw-bin-engine/issues/49#issuecomment-6651885173)

本輪另讀允許 work 的 `engine-slow-pickup-source-r1/classes/S_NPCPack_Item.class`，SHA-256 `342a67c96513e6dad7b7254d3b13da8819aa441430efe1a4756ef7284ccc5714`。以 `javap -c -p` 重建，逐行移除首尾空白並略過空白行後，與既存 `S_NPCPack_Item.javap.txt` 完全一致。文字檔 SHA-256 為 `e08834dc4c897530e998b85fc570a45ea4b68ec3cb39a3c2b9b87caebf7f382b`。

解析 class 常數池及 Code attribute，`buildPacket(L1ItemInstance)` 的 code 起始 **class File Offset `0x80C`**，長度 376 bytes，code SHA-256 `43fb59c6d442d9f1c3260ca83a3e252636d9a1e0137f97a362a75a6c823c49f5`。Java 方法使用 bytecode offset，RVA／VA 不適用。

| 已驗寫包步驟 | 方法內 bytecode offset | class File Offset，含該段指令 bytes | javap 行號 |
|---|---|---|---|
| 寫入 opcode87 | 0–5 | `0x80C–0x811` | 21–23 |
| instance `getId()` → `writeD` | 22–29 | `0x822–0x829` | 32–35 |
| `getItem().getGroundGfxId()` → `writeH` | 30–40 | `0x82A–0x834` | 36–40 |
| `getCount()` → `Math.min(count, 2000000000)` → `writeD` | 78–92 | `0x85A–0x868` | 60–66 |
| count 與 1 比較後進入名稱組合分支 | 101–129 | `0x871–0x88D` | 72–85 |

此方法在 offset30–40 直接傳遞 template getter 的 gfx，後面的 count／名稱分支處理另兩個欄位。就這個寫包區段而言，可核定「gfx 取自 getter、數量另寫欄位」，其直接指令沒有 count→gfx 選擇。getter 實作、模板建立／更新、其他 producer、同身份 client 的 remap 仍需獨立證明。因而 7↔3247 alias、金幣 count→gfx/frame 的全鏈結論仍為 unknown。#49 的缺件狀態與本輪觀察一致。

### 驗收逐項裁定與怎麼驗證

| 驗收項目 | 第 3 次結果 | 待補來源 |
|---|---|---|
| 同身份 S87→gfx→102 loader/table→factory→hit-test/pickup | 未完成 | 三組 dispatch tables、封包語義連結、初始化與完整 downstream bytes |
| type9 充分性／必要性／原生例外 | unknown | 上述完整資料流及各分支例外 |
| 原生可辨識／可顯示／可提出拾取 | 各自 unknown | factory 種類、frame resolver／alias、選取與拾取 predicate 分別補證 |
| 7／3247 alias 與 count 外觀 | 部分補證，整體 unknown | 已驗 server 寫包 getter；尚缺模板與同身份 client 分支 |
| 8 個 payload／10 幀 source pins | 重驗通過 | native 地面 frame／alias 選擇仍待驗證 |
| #51 分類與拾取閘門 | 未通過 | 由 Claude／調度器處理來源缺口後再裁定 |

本輪成功執行 `py -3 -B -X utf8 scratch/issue-48/verify_static.py` 與 `repro-1.py`，結果為 17 IDX／261,461 records、8 payload／10 frames、212 native chunks／175 ranges／108,221 unique bytes，重疊衝突 0，LiTo SHA 與 PE sections 符合前次 pin。`attempt3.py` 修正後及 `verify_server.py` 均退出 0。新增 scratch 輸出為 `attempt3.json`，腳本另有 `summarize3.py`。資產以記憶體解碼，產出僅含文字／JSON，必要 pin 已留在本報告。

提交前 `check_attempt3.py` 針對本次新增表格通過 8 處指令 bytes／VA／RVA／File Offset 核對、兩組語料的 8 個缺件範圍檢查與 S87 Code pin 檢查，`git diff --check` 通過。此輪為靜態資源查核，建置、CI 與原生實機測試未執行。

### 第 3 次舊交接（已由驗收調整取代，不再派工補證）

第 3 次曾要求來源補件並讓 #51 等待原生分類及拾取閘門。主控已裁定拆分資源 pins 與原生全鏈驗收，該補件與等待要求取消；現行交接以本文末節為準，原始歷史保留於 Git。

## 本輪收尾：做了什麼／怎麼驗證

- 先讀專案根目錄 `AGENTS.md` 與 `docs/REPORT.md`，fetch 後於專案本體執行 `git checkout -B task/48 origin/main`，再合入 `origin/task/48` 的既有報告歷史；保留主分支索引及其他工作檔。
- 開頭引用主控裁定，更新結論、pins 交接說明與 #51 前置；既有 8 筆 payload 與 10 筆幀表的 SHA、File Offset、bounds 完整保留。
- 僅做文件比對與 `git diff --check`：確認 pins 表與前輪提交相同、總數為 8／10，且相對 `origin/main` 只有 `docs/reports/issue-48.md` 變更。沒有執行報告中的歷史重現腳本，沒有重跑任何資源／BIN 掃描或建置。
- 產品、資源、`docs/REPORT.md`、封存 lock 均未改動；未啟動或附加遊戲，未連 DB／伺服器。本輪沒有功能、原生畫面或拾取實測結果。

## 未驗證什麼

- **原生分類全鏈 unknown（Themida 執行期初始化，靜態不可得）**；依主控裁定不再補證，也不再列為本任務驗收條件。資源 pins 核定不等於原生分類／拾取全鏈已證明。
- 歷史完整 BIN 未重驗；當前 LiTo 只有磁碟身份與 PE mapping 核驗，沒有執行、解殼或附加。
- 102.type loader、table 的完整內容／初始化、S87 同身份入口、object factory、地面 hit-test／mouse／C112 predicate、7/3247數量外觀規則皆 unknown。
- PAK 整檔 SHA、第二個獨立像素 decoder、原生渲染／拾取真機、伺服器模板與實際封包皆未重驗。沒有接觸 DB、伺服器、機密設定、既有遊戲程序、封存 release 或 assets junction；沒有建置。

## 產出與給下一棒的提示

正式產出為 `docs/reports/issue-48.md`；分支 `task/48`。必要 pins 已完整列於本報告，歷史 scratch 不作審查依賴。本輪只更新本報告，沒有建立 plan/design 拆工 JSON。

1. **Claude／調度器**：依開頭「驗收調整」審查結論、8 個 payload／10 幀 pins 與 unknown；同步 #51 前置，不再要求原生全鏈或重派掃描。保留 `3247-0.SPR` record 43801 的缺圖更正。
2. **#49 執行者**：交接已核定 server template→gfx/count/name envelope、金幣 7 或 3247 選用及數量邊界。本報告中 #49 的舊狀態只代表第 3 次核對的提交；本輪不重新裁定 #49 最新成果。
3. **#51 code 執行者**：以 S87 與 #49 伺服器模板決定存在、gfx、數量，以本報告 pins 顯示；兩者齊全才啟用。保持原生全鏈、type9 充分性／必要性及未證 alias／數量外觀規則為 unknown，不猜、不借圖。選取、拾取請求與地面／背包權威回包的結算條件仍須分開處理，不能以送出成功宣稱拾取完成。
4. **使用者**：後續實作完成後執行原生功能與畫面驗收；本次文件收尾不要求啟動或附加遊戲。
