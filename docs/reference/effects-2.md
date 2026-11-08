# 地圖特效光源、`]` 子特效、水中呼吸、座標特效與登入流程 XML（T227／Issue #534）

整理日期：2026-11-08（Asia/Taipei）。M7 查證任務，對應 [`gap-matrix.md`](gap-matrix.md) 第 8 節的「地圖特效 id→類別」「`]` 子特效」「水中呼吸、座標特效」「登入流程畫面」四列。

## 0. 方法與來源（全部唯讀）

- 原版：`TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（與 REA 相同）。`.text` 的 File Offset = VA − `0x400C00`；`.data` 的 File Offset = VA − `0x401800`。
- IDA 7.7＋Hex-Rays：P01 `ida/03-ascii-isolated/TW1608262200.idb`（SHA `3a0bf206…e82f`）複製到本任務 worktree 的 `scratch/t227/ida/tw16.idb`，複製前後雜湊相同。subst 用 `Q:`（執行前 `subst` 無任何對映，結束時解除）。批次反編譯 21 個函式，另以立即值掃描整個 `.text`。
- 本機資源：`D:\lineage開發\0-客戶端` 的 `TW13081901.txt`（list.spr 文字版）、`map\**\*.s32`（7,807 檔）、`releases/r29/assets/maps/effects/*.json`（產品已封裝的地圖特效）。
- 伺服器：`D:\lineage開發\1-伺服器\src`（只讀原始碼，比對 `S_EffectLocation` 的呼叫者）。
- 引擎：本分支（origin/main `a0b59824`）的 `engine/source`。只讀程式碼，不建置、不執行。
- 沒有啟動遊戲、伺服器或 DB；沒有把 BIN、pak、IMG 或解出的 XML 放進 git。腳本見第 7 節。

記號沿用 gap-matrix：**已驗證**＝反編譯或資料直接可見；**推測**＝由已驗證事實推論；**待驗證**＝本輪沒查到。

---

## 1. 地圖特效 id→類別：`A4E5D0` 第 7 參數是「光源半徑」

### 1.1 原版怎麼做（已驗證）

**(a) 參數傳遞鏈。**

| 步驟 | 函式 VA／FO | 內容 |
|---|---|---|
| 讀 `.s32` 特效尾段 | `sub_F0B570`／`0xB0A970`（表在 `0xF0C700` 起；`cmp 0D72h`／`0D76h` 在 `0xF0C925`／FO `0xB0BD25`） | 每筆 `u16 id, u16 x, u16 y [, u32 group]`；依 id 查表，把結果寫入 `dword_154514C[區塊][i]` |
| 建 actor | `sub_F0E9B0`／`0xB0DDB0` | `sub_A4E5D0(x, y, 物件 id, gfx=id, 0, 0, dword_154514C[i], 0, 1, 0, "", "", 0, 0, "", "", …)`，**第 7 參數就是上表的值** |
| 組物件描述 | `sub_A4E5D0`／`0x64D9D0` → `sub_9262D0`／`0x5256D0` | 參數依序寫入描述結構：x `+8`、y `+10`、id `+12`、gfx `+16`、第 5 參數 `+20`、第 6 參數 `+24`、**第 7 參數 `+28`**、…、第 13 參數（狀態旗標）`+88` |
| 建物件 | `sub_A51920`／`0x650D20` | `actor+9 = (byte)描述+28`；`actor+8 = 描述+24`（方向）；`actor+195 = 描述+88`（狀態旗標）。只有 `描述+12 == 自己的物件 id`（`dword_15442C8`）時才套用「< 5 改 5、19 改 10、旗標 20 改 14」 |

**`actor+9` 就是 [`ambience-and-map-events.md`](ambience-and-map-events.md) 第 3 節的光源值**（S40 `S_CHANGE_LIGHT` 也寫這個欄位）。光源繪製 `sub_F11FA0`（`0xF11FA0`／`0xB113A0`）用 `k = min(15, actor+9)` 從 `source.lmp` 取第 k 張光源圖。所以第 7 參數的語義是：**這個地圖特效當作光源時的半徑索引 k**（0 = 不發光）。S87 物件封包同一位置的欄位也是光源（3.81 `S_PutObject` 的 `C(light)`）；物件建立對兩種來源共用同一條路。

**(b) id → k 表（`.s32` 路徑）。** `F112A0`（`0xF112A0`／`0xB106A0`）與 `F11250`（`0xF11250`／`0xB10650`）各查一張 `.data` 表：

| 條件 | k | 表或範圍 |
|---|---:|---|
| `word_138BE2C[12]`（FO `0xF8A62C`） | 8 | 3552、3737、3601、3604、3607、3610、3613、3616、3619、3622、3625、3628 |
| `word_138BE44[14]`（FO `0xF8A644`） | 11 | 1992、1993、2088、2094、2097、2214、2215、2217、2219、2221、2223、3550、3580、3736 |
| 1884、1885、1901–1987、2209 | 5 | |
| 1892–1897 | 7 | |
| 2195、2197–2202 | 9 | |
| 149、1522 | 13 | |
| 1886、2196 | 14 | |
| **3442–3446** | `2·id − 6879` = **5、7、9、11、13** | |
| 其他 | 0 | |

`.seg` 路徑（同函式 `0xF0C9xx` 之後）另有一張較小的表（1884／1885 → 4、1992／1993 → 11、1886 → 14、1892–1897 → 7、1901–1987 → 5）。本機 330 個 `.seg` 都沒有特效尾段（REA B03），**這張表在本機資料下不會被用到**。

**(c) 3442–3446 是什麼。** `TW13081901.txt` 的名稱是 `dot range five`、`dot range seven`、`dot range nine`、`dot range eleven`、`dot range thirteen`，數字正好等於 k。產品已封裝的 `maps/effects/3442.json`–`3446.json` 都是 **1 格、1 個像素**（RGB555 `13707`，深灰點），沒有 token。全 `.text` 的立即值掃描中，`0xD72`–`0xD76` 只出現在 `F0B570` 的這個判斷（`0x8344B0` 的 `add 0D72h` 是無關的算術），**沒有任何繪製端的特例**。

結論：**3442–3446 是地圖編輯者放的「隱形光源點」**，本身只畫一個像素，作用是在夜晚／地城照亮半徑 5–13 的區域。

**(d) 本機分布**（`t227_light_census.py`，7,807 個 `.s32`、26,116 筆、521 種 id）：

| k | 筆數 | 占比 | 主要來源 |
|---:|---:|---:|---|
| 0 | 10,442 | 40.0% | 不發光 |
| 5 | 7,479 | 28.6% | 火花／蒸氣／熔岩／火山（1901–1987）、`dot range five` 1,097 |
| 7 | 2,588 | 9.9% | 火堆 1892–1897、`dot range seven` 2,021 |
| 8 | 165 | 0.6% | 黑妖地城火 3737 等 |
| 9 | 1,819 | 7.0% | 冰洞菇 2197–2202、`dot range nine` 1,698 |
| 11 | 1,854 | 7.1% | 藍火、火盆、`dot range eleven` 1,727 |
| 13 | 1,739 | 6.7% | 全部是 `dot range thirteen` |
| 14 | 30 | 0.1% | `brazier fire` 1886 |

**六成的地圖特效是光源**；3442–3446 共 8,282 筆（31.7%）。

### 1.2 3.81 伺服器是否支援

純客戶端行為，不經伺服器。`.s32` 由客戶端讀。

### 1.3 引擎現況

- `MapEffects.cs` 只把 3442–3446 當普通地圖特效畫出那一個像素，沒有光源。`OriginalMapCache.cs`／`MapEffects.cs` 都沒有 id→k 表（搜尋不到這些常數）。
- `Ambience.cs:345` 的 `AmbienceLights` 只收集**自己與 S87 物件**的 `Light`，而且**沒有任何呼叫者**；`AmbienceLightMap.Stamp` 也沒有被呼叫，光照圖目前沒有接上畫面（gap-matrix S_CHANGE_LIGHT 列：「光源畫成圓形、未用 `source.lmp`」）。
- 結果：夜晚與地城中，原版靠這些光源點照亮的區域，引擎全部是暗的（或全亮，取決於明暗是否啟用）。

### 1.4 建議的 code 任務

**FX1 地圖特效光源表**（依賴光照圖接上畫面，見 AM 第 3 節／T177 系列）
- 目標：把 1.1 (b) 的 `.s32` 表做成資料表（id → k），地圖特效載入時記下 k；`AmbienceLights` 把 k > 0 的地圖特效也加入（中心點用特效的畫面座標，套同樣的 700 預算與距離濾除）。
- 檔案：`MapEffects.cs`（記 k）、`Ambience.cs`（`AmbienceLights` 加地圖特效來源）、新增單元測試（3442→5、3446→13、3737→8、2088→11、未列 id→0）。
- 不要做：不要替 3442–3446 另畫光圈圖形；光的形狀一律來自 `source.lmp`。

---

## 2. `]` 子特效（`0x2A0` 物件）

### 2.1 原版怎麼做（已驗證）

list.spr 影格的 36 byte 紀錄中，`+22` 是旗標（`!`=1、`>`=2、`]`=4、`{`=8、`}`=0x10，REA B07），`+18` 是 token 帶的數字。

**(a) 誰處理。** 角色／怪物的影格回呼有兩個，兩者的 `]` 分支程式碼相同：

| 函式 VA／FO | 用途 | 呼叫方式 |
|---|---|---|
| `sub_A4B060`／`0x64A460` | 每格推進（重新排程自己：`sub_A23260(…, sub_A4B060, actor)`） | 函式指標（10 個資料參照） |
| `sub_A4BA20`／`0x64AE20` | 另一組進格回呼（在 `0xA46A45`、`0xA48298`、`0xE64520`、`0xE6453A` 以函式指標傳入） | 函式指標（4 個資料參照） |

特效物件自己的每格處理 `sub_8ABC10`（FO `0x4AB010`）也有 `]` 分支（[`skills-visual.md`](skills-visual.md) 第 2.2 節），所以**特效可以再生特效**。

**(b) `]` 分支做的事**（`A4B060` 第 123–147 行、`A4BA20` 第 91–115 行）：

```
if (frame->flags & 4) {                         // ']'
    e = new Effect (0x2A0 bytes);               // sub_8AB390 / FO 0x4AA790
    Effect(e, 物件id=0, gfx=frame->+18, 方向=actor+8, 旗標=0, 位置=(actor+16, actor+20), 0, 0);
    e+195 = actor+195;                          // 狀態旗標（隱身等），描述結構 +88
    e+198 = actor+198;
    e->vtbl[+24](e);                            // 加入世界、開始播放
}
```

- `0x2A0` 物件就是 RTTI `Effect`，即 [`skills-visual.md`](skills-visual.md) 表 2.2 的「kind 0／1（預設）」類別：放在指定位置、**播完自刪**，每格由 `sub_8ABC10` 處理 `!`／`]`／音效。
- **不看 list.spr 欄位 109 的 kind**：`]` 一律建預設類別，即使該 gfx 的 109 是飛行類。
- 位置與方向跟著**產生它的那一格當下**的擁有者，之後不再跟隨（Effect 沒有擁有者指標，`this[1]` 物件 id 為 0）。

**(c) 附帶發現：`{`、`}` 是淡入／淡出**（同一函式第 148–167 行）：

| token | 動作 | 計時回呼 |
|---|---|---|
| `{N` | `actor+552 = N`、步長 `32/N`，alpha `+560` 從 0 開始，每 10 個單位時間 +1 步，到 31 為止；`actor+40` 設 `0x40000` | `sub_A4F160`／`0x64E560` |
| `}N` | alpha 從 31 往下，N 步到 0；`actor+40` 設 `0x80000` | `sub_A4F2B0`／`0x64E6B0` |

本機 list.spr 只有 5 個 `{`（4 個 sprite）、3 個 `}`（3 個 sprite），用量很小。`sub_A231C0(10, …)` 的 10 是否為毫秒屬**推測**。

**(d) 本機用量**（`t227_token_census.py`）：`]` 在 list.spr 出現 **2,812 次、485 個 sprite**。例：necromancer（173、183、185、187 → 212–215、180）、ent（847、851 → 849）、cockatrice 1052 → 1054、drake 1062 → 1064、darkelf 1125 → 1127。最常被生出的 gfx 是 189、194、197（各 77 次）。這些多半是怪物攻擊或施法時附帶的光效、刀光、投射起點特效。

### 2.2 3.81 伺服器是否支援

純客戶端行為（由 list.spr 與動作影格觸發），不需要封包。

### 2.3 引擎現況

- `ObjectAssets.cs` 會保留影格的 `events` 字串，但引擎沒有任何地方消費 `]`、`{`、`}`（`ActionCadence.cs` 只看 `!`，戰鬥音效只看 `<`／`[`）。
- 產品素材：`releases/r29/assets` 的 97 個物件／角色／魔法 JSON 中，只有 `objects/6325.json` 帶 `]6327`。也就是說，**`]` 生出的 gfx 大多還沒封裝**。

### 2.4 建議的 code 任務

**FX2 `]` 子特效**（依賴素材封裝 `]` 目標 gfx）
- 目標：角色、怪物、特效的動作播到 `]N` 那格時，在擁有者當下位置以當下方向建立 gfx N 的一次性特效（播完即移除），不看 109 kind；擁有者隱身時子特效也不畫（複製狀態旗標）。特效自己的 `]` 也要處理（可遞迴）。
- 檔案：`ObjectRendering.cs`／`MagicRendering.cs` 的影格推進處、`MapEffects.cs` 的 `packetEffects`（可重用一次性特效清單）；素材端需在封裝流程加入 `]` 目標 gfx 清單（建議先限定已封裝怪物的 `]` 目標）。
- 測試：6325 的 `]6327` 在對應格建立 6327、位置／方向取當格擁有者、播完即刪。

**FX2b `{`／`}` 淡入淡出**（低優先，可併入 FX2）：只有 7 個 sprite 用到，等 FX2 完成後再決定。

---

## 3. S_BREATH（TW16 op 17）水中呼吸

### 3.1 原版怎麼做（已驗證）

`sub_931F60`（`0x931F60`／FO `0x531360`）：

```
read "dh"  → (物件 id, 秒)
sub_F05A90(秒, 11, 0, -1, 1);
```

- 只加狀態圖示 **effect 11 `WATER_BREATH_S`**（`effectlist2.xml`：kind cook、icon `552.tbt`），沒有任何畫面特效、音效或角色外觀變化。圖示的排序、tooltip、移除規則見 [`ui-layout.md`](ui-layout.md) 第 4 節（`sub_F05A90`：秒 = 0 且已存在時改為移除）。
- **讀了物件 id 但沒有使用**：不檢查是不是自己。
- 全 BIN 沒有其他「水中」計量條或倒數畫面與此封包相關（本輪只追這個處理函式，未全面搜尋）。

### 3.2 3.81 伺服器是否支援

支援。對應 **S126 `S_SkillIconBlessOfEva`**（`D 物件 id, H 秒`，[`server-protocol.md`](server-protocol.md)）。gap-matrix 第 1 節原寫「S250 WaterLife」，依 UI 第 4 節更正為 S126。

### 3.3 引擎現況（對照 T153）

`WorldSession.cs:84` 收 S126 → `TimedBuffs.cs:56` `ReceiveBlessOfEva`：物件 id 不是自己就忽略，否則加入 `WaterBreath` buff（名稱「水中呼吸」）。

| 項目 | 原版 | 引擎 | 一致？ |
|---|---|---|---|
| 計時與移除 | effect 11，秒數倒數；秒 = 0 移除 | buff 計時 | ✅ |
| 畫面特效 | 無 | 無 | ✅ |
| 物件 id 檢查 | 不檢查 | 只收自己的 | ⚠️ 實務上 3.81 只送給自己，無可見差異 |
| 圖示 | `552.tbt`，依 `effectlist2.xml` 排列 | 合成菱形／三角形圖示（ui-layout 第 11 項，T108 不一致） | ❌，屬既有的狀態圖示任務 |

### 3.4 建議的 code 任務

不另開。圖示差異併入既有的狀態圖示任務（ui-layout 第 4 節，`effectlist2.xml` → `.tbt`）；那個任務要確認 effect 11 對到 `552.tbt`。

---

## 4. S_EFFECT_LOC（TW16 op 219）座標特效

### 4.1 原版怎麼做（已驗證）

`sub_934530`（`0x934530`／FO `0x533930`）：

```
read "hhh" → (x, y, gfx)
pos = sub_4F84A0(2·x − 0x8000, y);          // 與 .s32 特效相同的座標轉換
light = (2028 ≤ gfx ≤ 2056) ? 1 : 0;        // cmp 7EBh／809h 在 0x934592／0x93459E（FO 0x533992／0x53399E）
e = new Effect(0x2A0);  Effect(e, 0, gfx, 方向 0, light, pos, 0, 0);   // sub_8AB390
e->vtbl[+24](e);
```

- **一律用預設 Effect 類別**（不看 list.spr 109 kind），方向固定 0，播完自刪；每格的 `!`／`]`／音效照 `sub_8ABC10` 處理。
- 2028–2056 在 list.spr 是 `cracker type aa`…`fa`（爆竹）與 `fire stick type a`…`d s`（仙女棒）。這些特效的 `Effect+9`（光源值）= 1。依 AM 第 1 節 (g)，「光源特效物件存在時（`+9` 非 0）」整體明暗等級暫時 +5（上限 15），所以**原版放煙火時畫面會暫時變亮**。
- x 的 `2·x − 0x8000` 轉換與 `.s32` 特效相同，所以封包的 x、y 就是一般世界座標，與引擎的用法一致。

### 4.2 3.81 伺服器是否支援

支援。S106 `S_EffectLocation`：`C 106, H x, H y, H gfx`，欄位順序與 TW16 相同。伺服器的送出點：陷阱（`L1Trap`）、閃電魔杖、`L1Chaser`（gfx 7025）、GM 指令（5524）、NPC 動作。**3.81 的煙火道具（`Firework.java`，gfx 2028–2055、3198）改用 S55 `S_SkillSound` 掛在玩家身上送出，不走 S106**，所以 4.1 的「煙火變亮」在 3.81 伺服器下不會從 S106 觸發。TW16 的 S55 對應處理是否也有 2028–2056 的光源分支：立即值掃描在 `sub_8676F0`（FO `0x466AF0`）看到 `cmp 7EBh／7ECh／808h／809h`，**未反編譯，待驗證**。

### 4.3 引擎現況（對照 T105）

`WorldSession.cs:64` → `MapEffects.cs:108` `ReceiveEffectPacket`：

| 項目 | 原版 | 引擎 | 一致？ |
|---|---|---|---|
| 欄位 | x、y、gfx | `H x, H y, H gfx`（`SoundPackets.cs:31`） | ✅ |
| 位置 | 封包座標，不跟隨物件 | 封包座標 | ✅ |
| 存活 | 播完一次自刪 | 無 time 時用一次動畫週期（`PacketEffectFrames`） | ✅ |
| 外觀來源 | 預設 Effect，播 action 0 | 先找 `objects/magic-{gfx}.json` 的 `AttachedEffect` 動作，否則找地圖特效；**素材沒封裝就不畫**（只留通知） | ⚠️ 依素材覆蓋率 |
| 方向 | 固定 0 | 方向 0（`appearance.At(action,0,…)`） | ✅ |
| 影格 `]`／`!`／音效 | 照 `sub_8ABC10` 處理 | 不處理 `]`；影格音效**待驗證** | ⚠️（併入 FX2） |
| 2028–2056 光源 | `+9 = 1`，畫面暫時 +5 | 無 | ❌，但 3.81 伺服器不從 S106 送煙火 |

### 4.4 建議的 code 任務

- 不另開。S106 的協定與存活規則已一致。`]` 併入 FX2。
- 「光源特效使畫面暫時變亮」（AM 第 1 節 (g)）若要做，應以 S55 煙火為主（先查 `sub_8676F0`），與 FX1 同屬光照圖之後的工作。

---

## 5. 登入流程畫面的 XML

### 5.1 TW16 要的檔名（已驗證）

BIN 的 ASCII 字串中，登入流程相關的 XML 檔名有：`LoginUI.xml`、`SelectUI.xml`、`CreateUI.xml`、`ServerName.xml`、`LoadingScene.xml`。`CNoticeWindow`（RTTI `0x1372600`）與 `QuitWindow`（RTTI `0x13767B0`）是**舊式寫死視窗**（`sub_8C7C80` 建立，[`windows.md`](windows.md) 第 0.1 節），不讀 XML，版面在建構子裡；本輪未反編譯它們的建構子。

### 5.2 本機搜尋結果

`t227_xml_hunt.py` 掃描 `D:\lineage開發` 下全部（略過 `scratch`、`.git`、`bin`、`obj`）：2,432 個 `.idx`（491 個目錄）的條目名稱，加上所有鬆散檔名。

| 檔案 | 結果 |
|---|---|
| `LoginUI.xml` | **找不到**（任何 `.idx` 與鬆散檔都沒有） |
| `SelectUI.xml` | **找不到** |
| `ServerName.xml` | **找不到** |
| `LoadingScene.xml` | **找不到**。`介面UI\經典版` 有 `LoadingScene.csb`／`.json`，是 Cocos Studio 格式的經典服介面，不是 TW16 的 XML 格式，不能直接用 |
| `CreateUI.xml` | **有候選**：`2-登入器\經典UI相關實作\待實作\收藏UI-7142\XML解碼\createui.xml`（明文 SHA `823A0FF3…`，19,771 bytes）與 `createui-c.xml`、`createui-h.xml`（同一批另有 `XML原始` 密文）。來源是該資料夾 README 所稱的「89 參考 PAK」，**不是 TW16、也不是 3.81 的資源** |

`CreateUI.xml` 候選的相容性：

- 全部 15 個 `MouseEvent`（`Class_ClassSelectScene`、`Male_ClassSelectScene`、`Auto_StatSettingScene`、`Ok_MessageWin` 等）都以字串出現在 TW16 BIN，事件名稱相容。
- 版面是 800×600：職業按鈕 7 個（王族、騎士、妖精、法師、黑妖、龍騎士、幻術師）在 y=486，男女鈕在 (148, 326)／(220, 326)，上一步 (14, 535)、下一步 (690, 535)，訊息窗在 (176, 160)。
- 使用的 surf 7091–7225 **不在** `0-客戶端` 的任何 `.idx`（只找到 `6488.img`），圖檔只存在於該參考資料夾抽出的部分 IMG。所以只有版面可參考，圖不完整。
- 3.81 伺服器開放哪些職業（候選版面有 7 個職業按鈕），本輪未查，需對照 `server-rules.md`。

限制：`.idx` 條目以 28 byte 舊格式解析；若有其他格式的 `.idx`（例如新版加長名稱），可能漏掉。

### 5.3 結論

| 畫面 | 狀態 |
|---|---|
| `LoginUI`、`SelectUI`、`ServerName`、`LoadingScene` | **缺資源**：本機任何版本都沒有。只能等使用者提供 TW16 世代的 `Tile.pak`／XML，或改以 3.81 實機截圖為準 |
| `CreateUI` | **部分**：有他版本的 XML 可參考版面（事件名稱相容），但圖檔不在本機客戶端；不建議直接套用 |
| `CNoticeWindow`、`QuitWindow` | 不需要 XML；版面要反編譯舊式建構子（另開查證） |
| `LoadingWindowUI` | 仍待驗證（3.81 世代是否有讀取畫面，以及它讀哪個 XML） |

---

## 6. 總表：可以開 code 的項目與仍查不到的項目

**可以開 code（依賴寫在括號）**

| 代號 | 內容 | 主要檔案 | 依賴 |
|---|---|---|---|
| FX1 | 地圖特效光源表：`.s32` id → k（含 3442–3446 = 5–13），加入光源收集 | `MapEffects.cs`、`Ambience.cs` | 光照圖接上畫面（`AmbienceLights`／`Stamp` 目前沒有呼叫者） |
| FX2 | `]` 子特效：角色、怪物、特效的 `]N` 格建立一次性預設特效（位置／方向取當格擁有者，複製隱身旗標，可遞迴） | `ObjectRendering.cs`、`MagicRendering.cs`、`MapEffects.cs`；素材封裝 | 素材封裝 `]` 目標 gfx |
| FX2b | `{`／`}` 淡入淡出（7 個 sprite） | 同 FX2 | FX2 |

**不需另開**：S_BREATH（只差狀態圖示，併入既有圖示任務）、S_EFFECT_LOC（協定與存活一致；`]` 併入 FX2）。

**仍查不到／待驗證**

1. `LoginUI.xml`、`SelectUI.xml`、`ServerName.xml`、`LoadingScene.xml`：本機缺資源。
2. TW16 S55 處理函式在 `sub_8676F0` 對 2028–2056 的判斷是否也設光源（未反編譯）。
3. `Effect+9 = 1` 是否也會在 `F11FA0` 蓋第 1 張光源圖（目前只確認 AM (g) 的「整體 +5」）。
4. `{`／`}` 計時單位（`sub_A231C0` 的 10）。
5. `actor+198` 的用途（`]` 會複製）。
6. `CNoticeWindow`、`QuitWindow` 的寫死版面；`LoadingWindowUI` 是否用於 3.81 世代。

## 7. 可重跑腳本

腳本放在 [`rea-scripts/`](rea-scripts/)；IDA 部分沿用 `run_ida.ps1`＋`ida_dec.py`（把 `$PSScriptRoot` 指到放 `ida/tw16.idb` 與 `scripts/` 的 scratch 目錄）。

| 腳本 | 用途 | 執行 |
|---|---|---|
| `ida_dec.py` | 反編譯：`F0B570 A4E5D0 A4B060 A4BA20 931F60 934530 F112A0 F11250 F0E9B0`、`9262D0 A51920 8AB390 F05A90 4F84A0 7C3EBA DF55F2 A4F160 A4F2B0 A7C380` | `run_ida.ps1 -Script ida_dec.py -List <清單> -Out <目錄> -Drive Q:` |
| `t227_imm_scan.py` | IDAPython：全 `.text` 找立即值（`7E9 7EB 7EC 808 809 D71 D72 D76 D77`） | `run_ida.ps1 -Script t227_imm_scan.py …` |
| `t227_light_census.py` | 從 BIN 讀兩張 k 表、重現 `F0B570` 的判斷、統計本機 `.s32` 的 k 分布、附 list.spr 名稱 | `py -3 -I t227_light_census.py <TW1608262200.bin> <客戶端根目錄> <out.json>` |
| `t227_token_census.py` | 統計 list.spr 的 `]`／`{`／`}` | `py -3 -I t227_token_census.py <TW13081901.txt> <out.json>` |
| `t227_xml_hunt.py` | 在所有本機 `.idx` 條目與鬆散檔名中找登入流程 XML | `py -3 -I t227_xml_hunt.py <根目錄> <out.json>` |
