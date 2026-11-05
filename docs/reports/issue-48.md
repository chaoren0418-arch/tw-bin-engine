# Issue #48：S87 地面物品分類與 gfx 靜態資源契約

日期：2026-11-05。分支：`task/48`。隔離工作目錄：`scratch/wt/task-48`。
基底：`89888870e52cf3c19e046c7b0c040619c117c644`。本輪只有本報告進版控。

## 結論

**部分資源已驗證，完整原生契約仍阻塞；不可把 #51 的 native 分類閘門視為通過。**

1. 更正 #42 的缺圖結論：同 SHA 的主索引實際存在 **`3247-0.SPR`**（大寫副檔名）。小寫 byte-exact 查找會漏掉；本輪線性 ASCII-fold 查找與既有 `Archive.find(b'3247-0.spr')` 二分查找獨立一致，均為 record 43801。不是檔案更新、不是借圖。
2. 7、3247、肉 36、箭 66，以及 20/41/43/45 藥水標籤候選的 8 個原始 SPR payload 均可定向讀取並由既有 Python decoder 在記憶體解析，共 10 幀；提供原始 SHA、File Offset、幀與 RGBA pin。未交付或複製 SPR／PAK／PNG，未修改資產。
3. `102.type(9)` **尚不足以授權泛用地面辨識／拾取**：本輪證明的是文字欄位，未證明 S87 → gfx lookup → type table loader → object factory → 滑鼠／C112 准入全鏈；也沒有證明 type9 是原生充分條件或必要條件。
4. gfx 7 與 3247 皆有獨立文字定義和原圖，但它們之間沒有本輪可證明的 alias、數量切換門檻、伺服器選用規則。不可因圖大小不同就猜單枚／多枚；金幣真實 gfx／count envelope 留待 #49 的證據及後續核對。

## 做了什麼與範圍

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

## sprite／frame／source pins

全部 entry 在 `D:\lineage開發\0-客戶端\Sprite.pak`，本輪尺寸 1,904,012,586 bytes；對應 `Sprite.idx` pin 如上。PAK offset 與 IDX offset 皆為 **File Offset**，不是 RVA／VA。

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

## 怎麼驗證、未驗證什麼

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

## 相關來源 SHA-256

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

## 產出與給下一棒的提示

產出：`docs/reports/issue-48.md`；分支 `task/48`。原始資源沒有進 git，無產品／lock／封存變更。

1. Claude／調度器先審查本報告並更正 #42「3247 無圖」的下游認知；大小寫查找須遵守已核定的索引比較規則。
2. #49 的既有執行者負責 server 模板／gfx／count/name envelope；本輪未接手、修改或干擾 #49。取得後再核對7/3247實際選用，不能只靠本報告的英文標籤。
3. dot／來源研究者需在另行確認可使用的同身份靜態證據中補齊 S87→102.type loader→table→factory→拾取 predicate；本輪安全範圍的既存 slices 不足。禁止把需正式 BIN／新增存取授權的動作包在後續 code 中偷偷執行。
4. #51 可使用這份索引／payload／frame pin 進行受控資料設計，但完整泛用 native 分類與拾取仍不可標通過。未證實欄位須以 unknown／不支援呈現；不得讓缺口變成寬鬆放行。
