# 原版 UI 版面與圖示查證（T161，Issue #429）

查證日期：2026-11-07（Asia/Taipei）。全程唯讀，沒有啟動遊戲、伺服器或 DB。方法照 #417：用 IDA 7.7＋Hex-Rays 開 P01 IDB 在 `scratch/` 的副本，靜態分析未加殼的 `TW1608262200.bin`。另外讀取本機客戶端 `D:\lineage開發\0-客戶端` 的 `Tile.pak`／`Sprite*.pak`，只讀出到 `scratch/`，沒有任何遊戲資源進 git。

涵蓋功能：T106、T108、T124、T125、T130、T131、T138、T150、T152。第 4 節於 2026-11-07 由 V3（#458）以同樣方法補完。

## 證據基準與地址規則

| 樣本 | SHA-256 | 用途 |
|---|---|---|
| `TW1608262200.bin` | `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2` | 主要分析對象 |
| P01 `ida/03-ascii-isolated/TW1608262200.idb` | `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f` | 複製到 `scratch/rea/ida/tw16.idb` 後開啟 |
| `TW13081901.unpacked.exe` | `653da514f750d4714fde2c83e5162aeef553c876ec541fe1cfebf6970f5c62a4` | 交叉驗證 |

ImageBase 為 `0x400000`。File Offset 依區段換算：

| 區段 | File Offset |
|---|---|
| `.text` | VA − `0x400C00` |
| `.rdata` | VA − `0x401400` |
| `.data`（有初值部分） | VA − `0x401800` |

偽碼裡的函式名稱都是 IDA 自動命名，不是原始碼名稱。只有 RTTI 類別名（`.?AV…@@`）是編譯器留在檔案裡的原名。

## 結論

1. **UI 版面主要寫在 XML 裡，不在程式碼裡。** 主介面、隊伍、書籤、地圖等的座標都寫在 `Tile.pak` 的 65 個加密 XML 中。加密方式已從 BIN 取得（AES-128-CBC，見下節），本輪已全部解開。台灣版（CodePage 950）會組出 `-c` 後綴的檔名；缺 `-c` 檔時是否改讀無後綴的檔，本輪未確認。
2. **原版是 800×600 固定版面。** 下方是一條 125 px 高的狀態列，由左而右是：
   - 左狀態框：等級、AC、MR、陣營、SP、負重、飽食度
   - 中央聊天窗：可伸縮
   - 小地圖：與擴充巨集共用同一格
   - 右巨集框：**F5–F12 共 8 格**，另有 3 個分頁
3. **原版有小地圖，而且有三層。**
   - 底列的 `MINI_MAP_WINDOW`：158×125
   - `Field_Map` 小地圖窗：161×121，用預先繪好的 png 地圖，畫 8 方向角色箭頭與隊友點
   - 世界／村莊地圖：550×316

   開啟方式是台灣版巨集框上的 **Map 按鈕**（action 13），快捷鍵是 **Ctrl＋M**（V5，#460）。
4. **聊天格式與顏色寫死在 BIN 中。** 共 16 個 `LineageChat::ChatText*Printer` 類別，由聊天類型 0／1／2／3／4／11／12／13／14／15／17／18／25／29 的工廠建立。顏色取自一張靜態 RGB555 調色盤，前 77 筆與 TW13 逐 byte 相同。
5. **與引擎比對 17 項：12 項不一致、2 項部分一致、3 項無法判定**，見最後一節。（#4 HP／MP 由 V1 #456 改判為不一致。）最大的三項：
   - 引擎版面不是 800×600
   - 快捷列只有 F1–F4
   - 小地圖用 F5 開關，與原版巨集鍵 F5 衝突

## 1. UI XML 的儲存與解密（已驗證）

| 項目 | VA | File Offset | 語義 |
|---|---|---|---|
| `sub_B3D390` | `0xB3D390` | `0x73C790` | 載入 UI XML。依 `"%s-%c.xml"`（VA `0x11A5F34`／FO `0xDA4B34`）組出語系檔名；第一個 byte 不是 `X` 時跳出 `XML Encryption Check (%s)` |
| `sub_A14A60` | `0xA14A60` | `0x613E60` | 語系字元。CodePage 950→`c`、932→`j`、936→`h`、949→`k`，其他→`e`。**台灣版取 `-c`** |
| `sub_B3D710` | `0xB3D710` | `0x73CB10` | 呼叫 `decrypt(buf+4, len−4)`，再把 `buf[0]` 改成 `<` |
| `sub_A9FA30` | `0xA9FA30` | `0x69EE30` | 金鑰是堆疊上 32 byte 常數的前 16 byte，IV 是 16 byte 常數。`sub_4D5EB0`（`0x4D5EB0`／FO `0xD52B0`）以 128 bit 建金鑰排程 |
| `sub_4D8FB0` | `0x4D8FB0` | `0xD83B0` | 標準 CBC 解密；尾端不足 16 byte 的部分直接與當前 IV 做 XOR |

`Tile.idx` 是明文索引，每筆 28 byte：offset、20 byte 檔名、size。`Sprite*.idx` 格式相同。UI 圖檔是 `<surf>.img`，格式為：

```
u16 寬, u16 高, u16 旗標, u16 透明色, 寬×高 個 RGB555 像素
```

抽樣的每個檔案都符合 8＋寬×高×2＝檔案大小。`SurfList.xml` 把 surf id 對應到檔案編號，6,653 筆裡 6,310 筆是 `id == file`。

重跑方式：
- 解密 XML：`py -3 -I docs/reference/rea-scripts/ui_xml_decrypt.py "D:\lineage開發\0-客戶端" scratch/rea/uixml`
- 讀圖檔尺寸：`ui_img_dims.py <surf…>`
- 轉成 PNG 供本機目視：`ui_img_render.py scratch/rea/render <surf…>`

**交叉驗證：** TW13081901 也有 `uistatus86.xml`、`BookmarkUI.xml`、`MINI_MAP_WINDOW`、`EX_MACRO_WINDOW`、`%s-%c.xml` 字串，所以這套 XML 版面機制在 2013 年版就已存在。

## 2. 主介面（`uistatus86-c.xml`，解析者 `sub_A9AD80`／FO `0x69A180`）

### 已驗證：底部狀態列

座標都是 800×600 的螢幕座標，子元件寫相對座標 `xRPos`／`yRPos`。尺寸讀自 `.img` 標頭。

| 區塊 | 位置 | 底圖 surf | 尺寸 | 內容 |
|---|---|---|---|---|
| `LEFT_WINDOW` | (0, 475) | 3168 | 135×125 | 見下表 |
| `CENTER_WINDOW`（聊天） | (129, 470)，`stepSize=36` | 3279（底部錨定在 `yPos=600`） | 362×240 | 見第 3 節 |
| `MINI_MAP_WINDOW` | (485, 475) | 框 3275＋底 3274 | 框 158×125；底 135×101，位於 (+12, +20) | 小地圖 |
| `EX_MACRO_WINDOW` | (485, 475) | 3226 | 158×125 | 擴充巨集，與小地圖同位置（推測兩者切換顯示） |
| `RIGHT_WINDOW`（巨集） | (643, 476) | 3176 | 157×124 | 底圖印著 F5–F12，共 8 格 |
| `FLOAT_WINDOW/HPMP_WINDOW` | XML 註解寫「in Binary File」 | — | — | **HP／MP 不在底列 XML 中**，寫在程式碼裡。預設在 (135, 465)，詳見本節最後的 V1 |

`LEFT_WINDOW` 的子元件（相對座標）：

| 元件 | surf | 位置 | 尺寸 |
|---|---|---|---|
| 等級條 | 3169 | (25, 26) | 101×11 |
| 等級數字 | — | (43, 29) | — |
| EXP% | — | (78, 29) | — |
| AC | — | (41, 69) | — |
| MR% | — | (105, 69) | — |
| 陣營圖示 | 3170／3171／3172 | (10, 86) | — |
| 陣營值 | — | (41, 89) | — |
| SP | — | (105, 89) | — |
| 負重條 | 3173／3174／3175 | (24, 107) | — |
| 負重數字 | — | (41, 109) | — |
| 飽食條 | — | (87, 107) | — |
| 飽食數字 | — | (105, 109) | — |
| 金幣 | — | (115, 48) | — |
| 巴洛格／亞希條 | — | (24, 46)／(68, 46) | — |

`RIGHT_WINDOW` 的子元件：

| 元件 | 內容 |
|---|---|
| 選單按鈕 | 8 個，`y=91`：Map(2)、Mail(21)、Status(40)、Spell(59)、Item(78)、Pledge(97)、Option(116)、Quit(135)。對應 action 13、1–7，間距 19 |
| 巨集分頁 | 3 個，action 10–12，位於 x=15／37／60、y=5 |
| 選取框 | (12, 19) |
| 第一格圖示 | (14, 21) |
| 鎖定鈕 | MACRO_LOCK，位於 (−9, 37) |

**語系差異：** 非 `-c` 的 `uistatus86.xml` 沒有 Map 按鈕，其餘按鈕位於 x=27 起、y=94。台灣 `-c` 版多了 Map 按鈕，整排左移。

### 推測

- ~~巨集格距：從 3176 的渲染圖目測約 33×34 px~~ → 已由 V5（#460）查證：4 欄 × 2 列，格距 34×34 px，見下方「底列行為（V5）」。
- ~~HP／MP 浮動窗位置寫死在程式碼中~~ → 已由 V1（#456）查證，見下方「HP／MP 浮動窗與頭上條」。

### 待驗證

- 紅字訊息（畫面中央系統訊息）的位置。

### HP／MP 浮動窗與頭上條（V1，Issue #456）

查證日期 2026-11-07。方法同本文件開頭（IDA 7.7＋Hex-Rays 開 `scratch/rea/ida/tw16.idb` 副本，SHA 已核對；靜態分析 `TW1608262200.bin`，SHA 已核對）。圖檔尺寸讀自本機 `Sprite*.pak` 的 `.img` 標頭；渲染圖只放 `scratch/`，未進 git。

**更正 T161 的推測：** `sub_919F30` 不是 protobuf 解析者，是**選項視窗**（`Lineage::OptionUI`）的 XML 控制項綁定函式。protobuf 是在 `sub_59DD30` 讀進全域變數。

**Hex-Rays 注意事項：** `sub_E538D0`（取 surf，VA `0xE538D0`／FO `0xA52CD0`）的參數被 IDA 標成 `char`，所以偽碼裡直接寫常數的 surf 編號會被截成 1 byte。例如頭上條偽碼寫 `162`，反組譯是 `push 3A2h`（930）。本節所有直接常數的 surf 編號都已用反組譯核對。存在成員變數裡的編號不受影響。

#### 已驗證：設定來源與預設值

`ClientConfig.proto` 的 descriptor 是明文序列化，位於 VA `0x11570D8`／FO `0xD55CD8`，長 3,383 byte。用 `rea-scripts/hpmp_pb_descriptor.py` 解出：

```
message ClientConfig {
  optional FloatingHPMP floatingHPMP = 10;
  optional bool showCharacterHPMP = 11 [default = true];
  message FloatingHPMP {
    optional bool   show         = 1 [default = true];
    optional Status status       = 2 [default = Status2];   // enum Status0=0, Status1=1, Status2=2
    optional int32  shape        = 3 [default = 1];
    optional bool   thickness    = 4 [default = false];
    optional bool   showRecovery = 5 [default = true];
    optional bool   showPercent  = 6 [default = true];
    optional Position position   = 7;                        // Position{int32 x=1; int32 y=2}
  }
}
```

各欄位讀取後存放的位置如下：

- 載入：`sub_59DD30`（VA `0x59DD30`／FO `0x19D130`）
- 存檔：`sub_59E920`（VA `0x59E920`／FO `0x19DD20`）
- 欄位偏移：依 `sub_5AA660`（FO `0x1A9A60`）的 `case 1…7` 核對
- 「全部重設」：`sub_998250`（VA `0x998250`／FO `0x597650`）

| 設定 | 存放處 | descriptor 預設 | 全域初值（.data） | 「全部重設」寫入 | 作用 |
|---|---|---|---|---|---|
| `showCharacterHPMP` | `byte_138BE7C`（FO `0xF8A67C`） | true | 1 | 1 | 自己頭上的 HP／MP 條 |
| `FloatingHPMP.show` | `byte_138BE7D`（FO `0xF8A67D`） | true | 1 | **0** | 浮動窗是否顯示。讀取者見「待驗證」 |
| `FloatingHPMP.status` | `dword_138BFAC`（FO `0xF8A7AC`） | Status2 | 2 | 2 | 浮動窗長度：0 短、1 中、2 長 |
| `FloatingHPMP.shape` | `dword_138B620`（FO `0xF89E20`） | 1 | 1 | 1 | 1 橫排（HP、MP 左右並排）、0 直排（上下疊） |
| `FloatingHPMP.thickness` | `dword_154333C`（.bss） | false | 0 | 0 | 改用另一組外框與條的圖 |
| `FloatingHPMP.showRecovery` | 設定物件 `sub_4F9B90()+670`，同步到浮動窗 `+0x60` | true | — | 1 | 回復光效疊圖，見下文 |
| `FloatingHPMP.showPercent` | `sub_4F9B90()+799`，同步到浮動窗 `+0x61` | true | — | 1 | 百分比文字 |
| `FloatingHPMP.position` | `dword_15451F4`／`dword_15451F8`（.bss） | 無 | — | — | 載入時取 x % 800、y % 550。值為 (−1, −1) 時用 **(135, 465)**，見浮動窗建構子 `sub_ECD2A0`（FO `0xACC6A0`）的 `0xECD375`–`0xECD3BC` |

**改設定的入口：**

- **選項視窗「Etc」頁：** 有 4 個勾選框：`ShowCharacterHPMP`、`HPMPBarThick`、`HPMPBarRecovery`、`HPMPBarPercent`。
  - 綁定在 `sub_919F30`（FO `0x519330`），按鈕註冊在 `sub_919170`（FO `0x518570`）。
  - 4 個處理函式先切換暫存副本（`dword_13D43E4` 的 +812／+814／+818／+823）：
    - `sub_91C540`（FO `0x51B940`）
    - `sub_91B490`（FO `0x51A890`）
    - `sub_91B430`（FO `0x51A830`）
    - `sub_91B3D0`（FO `0x51A7D0`）
  - 再由 `sub_9184B0`（FO `0x5178B0`）寫回全域，`HPMPBarThick` 會觸發浮動窗重排。
- **選項視窗沒有 shape／status 選項。** 這兩項只能用浮動窗上的兩顆小按鈕切換（見下文）。
- **HyperText 指令：** `showhpmp`、`showfloathpmp`、`HPMPBarThicknessSet`、`HPMPPercentage`（註冊在 `sub_BB5180`）。
  - `HyperActShowHPMP`（`sub_584B50`，FO `0x183F50`）切換 `byte_138BE7C`。
  - `HyperActThickHPMPBar`（`sub_585020`）呼叫 `sub_EDB620`（FO `0xADAA20`），切換 thickness 並重排。
  - `HyperActHPMPPercentage`（`sub_582EE0`）切換百分比。
  - `sub_584C40` 切換 showRecovery。

#### 已驗證：浮動窗 `HpMpWindow`

- **物件與建立：**
  - RTTI `.?AUHpMpWindow@@`（VA `0x138B808`／FO `0xF8A008`），vtable `0x11E28D4`。
  - 實例存在 `dword_1543330`。進入遊戲時由 `sub_8C7C80`（FO `0x4C7080`）建立，建構後就是可見狀態（`+0x69 = 1`）。
  - 視窗原點存在子容器的 +20／+24（`this+0x64`），初值 0，加上上表的 position。
- **版面：** 由 `sub_ED9030`（VA `0xED9030`／FO `0xAD8430`）依 (shape, status, thickness) 決定，`sub_ED8F20` 每次切換時重排。
  - 重排後若 x＋外框寬 > 799，x 改為 799 − 外框寬。
  - 拖曳（`sub_ED8D40`，FO `0xAD8140`）時 y 上限是 550。
- **每幀繪製：** `sub_ED0020`（FO `0xACF420`）依序呼叫：
  - 橫排 `sub_ED2680`（FO `0xAD1A80`）或直排 `sub_ED56D0`（FO `0xAD4AD0`）
  - 遊戲時鐘 `sub_ED6330`（FO `0xAD5730`）
  - 兩個只在特殊旗標 `dword_13CE514 & 0x40000000` 時畫的徽章／提示：`sub_ED68C0`、`sub_ED6A80`

座標都是相對視窗原點的 px。最終螢幕座標＝視窗原點＋`dword_154524C`／`dword_1545250`（UI 原點偏移）＋相對座標。這兩個偏移值在 .bss，沒找到直接寫入者；800×600 時推測為 0。

**橫排（shape = 1，預設）**

| status | 外框 surf（一般／thick） | HP 條 surf，位置 (9, 11) | MP 條 surf | 時鐘位置 | 「切換排法」鈕 | 「切換長度」鈕 | 時鐘提示區 | 拖曳區 |
|---|---|---|---|---|---|---|---|---|
| 0 | 2353 265×48／2908 234×34 | 2359 85×20／2914 92×14 | 2356 80×20／2911 92×14，位置 (133, 11) | (108, 3) | (85, 3) | (127, 3) | (108,3)–(128,23) | (102,0)–(132,30) |
| 1 | 2354 320×48／2909 282×34 | 2362 112×20／2917 116×14 | 2357 107×20／2912 116×14，位置 (157, 11) | (132, 3) | (109, 3) | (151, 3) | (132,3)–(152,23) | (126,0)–(156,30) |
| 2（預設） | 2355 378×48／2910 334×34 | 2365 141×20／2920 142×14 | 2358 136×20／2913 142×14，位置 (183, 11) | (158, 3) | (135, 3) | (177, 3) | (158,3)–(178,23) | (152,0)–(182,30) |

- 橫排兩顆鈕：「切換排法」2368（22×7）、「切換長度」2371（22×7）。thick 與否都一樣，位置也一樣。
- **預設外觀：** 橫排、長、一般粗細，位置 (135, 465)。外框 2355 佔 x 135–513、y 465–513，蓋在聊天窗上緣（聊天窗位於 (129, 470)）。

**直排（shape = 0）**

| status | 外框 surf（一般／thick） | HP 條（一般 (30, 9)／thick (30, 9)） | MP 條（一般 (30, 22)／thick (30, 26)） | 時鐘位置（一般／thick） |
|---|---|---|---|---|
| 0 | 2332 197×48／2890 141×46 | 2338 108×15／2896 102×14 | 2335 108×15／2893 102×14 | (7, 9)／(7, 13) |
| 1 | 2333 230×48／2891 167×46 | 2341 142×15／2899 128×14 | 2336 142×15／2894 128×14 | 同上 |
| 2 | 2334 263×48／2892 193×46 | 2344 174×15／2902 154×14 | 2337 174×15／2895 154×14 | 同上 |

- 直排「切換排法」鈕：一般 2347、thick 2905，都是 21×8，位置 (19, 0)。
- 直排「切換長度」鈕：2350（26×8），位置 (36, 0)。
- 時鐘提示區 (7,9)–(27,29)。拖曳區：一般 (0,5)–(33,38)，thick (0,9)–(33,42)。

**HP 條的變體與填充方式**

- **HP 條圖的選擇：** 依 `dword_153CE60`（自己的角色物件）的旗標選用。旗標的語義是推測（顏色讀自渲染圖），見「推測」。
  - 平常用基準圖（紅）。
  - byte +86 為真時用基準＋1（灰）：`sub_A43010`。
  - byte +87 為真時用基準＋2（綠）：`sub_A43030`。
- **填充（`sub_E53BD0`，旗標 `0x8003`）：**
  - 橫排的 HP：來源矩形取右段 `[w − w·HP/MaxHP, w]`，貼在 HPx＋同一偏移處，所以血量**從左往右縮、靠右對齊**。
  - 橫排的 MP：取左段 `[0, w·MP/MaxMP]`，靠左對齊。
  - 直排的 HP、MP 都靠左對齊。
  - w 是當下 HP／MP 條圖的寬。
- **數字文字 `"%d/%d"`：**
  - 每條置中：x＝條x＋條寬/2−字寬/2；y＝條y＋2＋(thick ? 0 : −2)。
  - 用 `sub_841770` 畫，文字色是調色盤[14] 白 #FFFFFF，第二色是調色盤[0] 黑 #000000。
- **百分比 `"%g%%"`：** 只在 showPercent 開啟，而且外框不是 status 0（橫 2353／2908，直 2332／2890）時顯示。
  - 數值是 HP/MaxHP×100。≤ 99 時經 `sub_8611D0` 取整，> 99 時改用 `sub_8611F0`。
  - 位置：
    - 橫排的 HP% 在 HP 條左側 x＝條x＋7。
    - 橫排的 MP% 靠右：x＝條x＋條寬−字寬＋4。
    - 直排兩者都靠右。
    - y＝條y＋(thick ? 0 : −2)＋條高/2−(2·thick＋1)。
  - 用 `sub_841190` 畫，白字黑第二色。
- **回復光效疊圖：** HP 疊圖存在 `+0x30`，MP 疊圖存在 `+0x34`。
  - showRecovery 開啟而且符合以下條件時，用 `sub_AF28D0` 疊在條上，寬度與填充寬一致：
    - (`sub_EE1BC0()` ≥ 50 或 `sub_EE1B80()` < 17)
    - 而且 (`sub_F03F80(56)`、`sub_F03F80(66)`、`sub_F03F80(112)` 三者皆真，或 `sub_EFE870(66／56／112)` 任一為真而且前述值 ≥ 83)
  - 疊圖 surf：
    - 橫排一般：5292＋s（HP）、5295＋s（MP），92／116／142×10
    - 橫排 thick：5304＋s、5307＋s，×14
    - 直排一般：HP 在 s=0 時用 5310，s=1／2 時用 5286＋s；MP 用 5289＋s，×10
    - 直排 thick：5298＋s、5301＋s，×14
  - 疊圖本身是白色雜訊紋理。
- **低血警示框：** 存在 `+0x54`，由 `sub_EDB8E0`（FO `0xADACE0`）控制。
  - HP ≤ 35% 時，計數器 `+0x5C` 設為 6，並以 300 ms 計時器遞減；計數器為奇數的幀畫紅框。
  - `byte_1542AA1 == 7` 時改用 `dword_138B62C`＝0.40 當門檻。
  - 紅框位置：橫排 (+6, +8)，直排 (+27, +6)。
  - 紅框 surf：
    - 橫排：一般 2935＋s（107／134／163×33）、thick 2941＋s
    - 直排：一般 2932＋s（148／182／214×24）、thick 2938＋s
  - 圖是紅色圓角框，透明色是洋紅。
- **時鐘：** 依遊戲時刻取階段＝((時＋23) % 24) / 2，共 12 段，查表取圖。
  - 表的每筆是 {起始 surf, 幀數, dx, dy}。橫排表在 `0x138B630`（FO `0xF89E30`，2405… 與 2381…，33×33）；直排表在 `0x138B6F0`（FO `0xF89EF0`，2772… 與 2748…）。
  - 換段時播放轉場動畫，每 150 ms 一幀，另疊 2416＋t/150、2423＋t/150。
  - 滑鼠停在時鐘提示區時，在 (提示區左−7, 提示區上＋7) 畫 `"%02d:%02d"`，顏色是調色盤[33] #FFE6CE。
- **兩顆按鈕：** 由 `sub_8F9780` 建立，各帶 3 張同尺寸圖（n、n＋1、n＋2）。
  - 「切換排法」：callback `sub_EDC840`（FO `0xADBC40`），翻轉 shape，提示字串 1639。
  - 「切換長度」：callback `sub_EDC620`（FO `0xADBA20`），status＝(status＋1) % 3，提示字串 1640。
- **初始隱藏的元件：** 另有 surf 5868 的圖，位置是時鐘提示區＋(18, −30)；以及按鈕 5866／5867，位置是＋(32, −25)，按下會開啟字串 3409 的網址。這三張圖的檔案本機客戶端沒有。

#### 已驗證：頭上 HP／MP 條（`MiniHpMpBar*`）

- **類別：**
  - `Lineage::MiniHpMpBarManager`：RTTI VA `0x136FFE8`／FO `0xF6E7E8`；建構子 `sub_8E66D0`（FO `0x4E5AD0`），註冊事件 18／26／28／47／48／49／50。
  - `MiniHpMpBarDraw`：RTTI `0x1363F18`／FO `0xF62718`。
  - `MiniHpMpBarBlinkDraw`：閃爍版。
- **每幀走訪：** `sub_8E7290`（VA `0x8E7290`／FO `0x4E6690`）走訪有條的物件。
  - **其他物件：** 一律畫，flag 傳 1。HP%、MP% 取物件 byte +217／+218（有號，值為 −1 時不畫）。
  - **自己：** flag 傳 `byte_138BE7C`（showCharacterHPMP），HP% 由目前 HP×100/MaxHP 算出，經 `sub_8E7110` 夾在 0–100 再取整，MP 同理。
  - **哪些物件有條：** 事件 18 的處理函式 `sub_8E74E0`（FO `0x4E68E0`）加入「+217 ≥ 0 而且 +220 == 0」的物件，另外一律加入自己。
- **位置：** `sub_8E77F0`（VA `0x8E77F0`／FO `0x4E6BF0`），bbox 由 `sub_A3E830` 取得。
  - 其他物件：x＝bbox左＋(bbox右−bbox左)/2−24；y＝bbox上−12。
  - 自己：x＝P.x＋`dword_154524C`−80，其中 P＝`sub_F1A750(sub_F1A080(obj+16))`；y＝bbox上−12。
  - 只在 x ≥ 0、y ≥ 0、x ≤ 偏移＋800 時畫。其他物件另外要求 y ≤ 偏移＋600。
- **繪製：** `sub_8E4E80` 呼叫 HP `sub_8E4D40`（FO `0x4E4140`）與 MP `sub_8E4DE0`（FO `0x4E41E0`）。

  | 層 | surf | 尺寸 | 畫在 | 說明 |
  |---|---|---|---|---|
  | HP 底 | **930**（`0x3A2`） | 44×5 | (x, y) | 整張畫，旗標 `0x8003`。米色框＋白底 |
  | HP 填充 | **931**（`0x3A3`） | 44×5 | (x, y) | 來源 (0, 0, 44·HP%/100, 4)，紅（主色約 RGB 160,48,48） |
  | MP 底 | 930 | 44×5 | (x, y＋4) | 與 HP 底重疊 1 px |
  | MP 填充 | **932**（`0x3A4`） | 44×5 | (x, y＋4) | 來源 (0, 0, 44·MP%/100, 4)，藍（主色約 RGB 48,104,160） |
  | 閃爍（BlinkDraw，`sub_8E4B90`） | `0x20C9`（8393） | — | (x, y) | 只在警示檢查者回真時畫，寬 44·HP%/100。本機 SurfList 沒有 8393 |

### 推測（V1）

- HP 條變體：灰（＋1，角色 byte +86）與綠（＋2，角色 byte +87）。依天堂慣例推測綠是中毒；灰的語義未知。
- 回復光效的條件：`sub_EE1BC0` 推測是等級；56／66／112 推測是技能或狀態編號。語義都未查。
- `byte_1542AA1 == 7` 用 40% 警示門檻：BIN 另有 `HpWarnCheckerWarrior` 類別，推測 7 是戰士。
- `sub_8611D0`／`sub_8611F0`：推測分別向下、向上取整，讓未滿血時不顯示 100%。未展開。
- 頭上條「其他物件」的 +217／+218：推測是伺服器送的 HP%／MP%（例如隊員血條封包）。

### 待驗證（V1）

- **`FloatingHPMP.show`（`byte_138BE7D`）如何控制浮動窗顯示：**
  - 讀取者只有存檔、選項文字和 HyperText 文字，沒有找到它呼叫浮動窗的顯示／隱藏 vtable（`sub_ECF910`／`sub_ECFC30`）。
  - 「全部重設」把它寫成 0，`sub_998840` 在舊設定版本（< 78）時寫成 1。
  - 所以實際預設是否顯示要真機確認。
- **時鐘圖的螢幕繪製點：** `sub_ED6330` 以 `sub_565E50` 組合座標，未展開。
- **自己頭上條的 x：** 公式中 −80 的基準（`sub_F1A750` 回傳的點）未展開。
- **800×600 下的 UI 原點偏移：** `dword_154524C`／`dword_1545250` 是否為 0。
- **缺檔的 surf：** 8393（閃爍）、5866–5868 與徽章 5760–5763、5864／5865 本機缺檔，畫面無法對照。
- **真機比對：** 未做，沒有啟動遊戲。

### 底列行為（V5，Issue #460）

查證日期 2026-11-08。全程唯讀靜態分析，沒有啟動遊戲、伺服器或 DB。

**本節用了兩個樣本：**

| 樣本 | 做法 | File Offset 換算 |
|---|---|---|
| TW16：`TW1608262200.bin` | 照本文件開頭的方法，用 `scratch/rea/ida/tw16.idb` | `.text` VA − `0x400C00`；`.rdata` VA − `0x401400` |
| TW13：`TW13081901.unpacked.exe`（SHA 已核對 `653da514…5c62a4`） | 複製到 `scratch/rea/tw13/`，用 IDA 7.7 自動分析，產生新的 IDB（32,999 個函式） | 只有一個 `.image` 區段，VA − `0x400000` |

**先說明為什麼要看 TW13：** 在 TW16 裡，消費 `uistatus86-c.xml` 底列元素的類別是 `Status800`（vtable VA `0x11E29E4`／FO `0xDE15E4`），但這個類別已經是**死碼**：
- 建構子 `sub_ECD6E0`（FO `0xACCAE0`）沒有任何 xref。整個 `.text` 掃 `E8` 呼叫，也找不到呼叫它的地方。
- 子類別 `StatusPartyPet` 的建構子 `sub_ECED60` 也沒有呼叫者，負責切換的程式碼也不存在。
- TW16 實際用的是新版 widget UI：`MainButtonUI`、`MiniMapUI`、`SlotWidgetUI`、`MainChatUI`，工廠在 `sub_B56A40`（FO `0x755E40`）。在 TW16 裡，F5–F12 送到 `SlotWidgetUI`（`dword_14EC328`，`sub_9FAAD0`／FO `0x5F9ED0`），不送給 `Status800`。
- 這些 widget 的 XML **不在本機 `Tile.pak` 中**。本機 65 個 XML 裡沒有 `MainButtonUI`／`SlotWidgetUI`／`MiniMapUI`，idx 檔名也搜不到。

TW13 有 `Status800`、`StatusMiniMap`、`StatusMacroExtend`、`StatusPartyPet` 的 RTTI，但**沒有**任何新版 widget 字串。它的 `Status800` 由 initGame `sub_4D7E50` 在 `0x4DB4D6`（FO `0xDB4D6`）建立，存進 `dword_C31E64`，是實際在跑的類別。

所以本節的**繪製幾何**兩邊都查了，數值一致；**切換觸發和鍵盤處理**以 TW13 為準。注意：兩個樣本都不是實際遊玩用的 `LiTo.bin`（Themida 加殼，無法靜態分析），見 `rea-inventory.md`。

#### (a) 巨集 8 格座標（已驗證）

元素的 `+4`／`+8` 是**絕對座標**。解析者在 `xRPos`／`yRPos` 加上父視窗的 `x`／`y`（`sub_A9AD80` 的屬性迴圈）。所以：
- `MACRO_ICON`＝(643＋14, 476＋21)＝**(657, 497)**
- `SELECT_MARK`＝(655, 495)

| 用途 | 公式（k＝0…7） | 出處 |
|---|---|---|
| 圖示左上 | (657 ＋ 34·(k % 4), 497 ＋ 34·(k / 4)) | TW16 `sub_ED33F0`（VA `0xED33F0`／FO `0xAD27F0`）的 `for k<8` 迴圈，讀 `dword_153F440+460`（`MACRO_ICON`）；TW13 `sub_783A90`（FO `0x383A90`）相同 |
| 選取框 | (655 ＋ 34·(s % 4), 495 ＋ 34·(s / 4))，s＝目前選取格（TW16 `dword_13CE7FC`） | 同函式，讀 `+456`（`SELECT_MARK`），surf 2331 |
| 數量文字 | 右對齊：x＝655 ＋ 34·(k%4) ＋ 27 − 5·字數，y＝495 ＋ 34·(k/4) ＋ 22；黃字（調色盤 13）、黑影（0） | 同函式 `sprintf("%d")` 一段，上限 99999 |
| 點擊命中 | 格號＝(mx − 655)/34 ＋ 4·((my − 495)/34)，夾在 0…7 | TW13 `sub_782740`（FO `0x382740`）。原點 `this+80/+84` 由 `Status` 建構子從 `SELECT_MARK` 填入（TW13 `sub_77F6B0`；TW16 `sub_ECD9D0`／FO `0xACCDD0`） |

換成具體座標：

| | 第 1 欄 | 第 2 欄 | 第 3 欄 | 第 4 欄 |
|---|---|---|---|---|
| 上列（F5–F8） | (657, 497) | (691, 497) | (725, 497) | (759, 497) |
| 下列（F9–F12） | (657, 531) | (691, 531) | (725, 531) | (759, 531) |

- 分頁：`this[66]`（TW16）是目前分頁 0–2，實際格號＝k ＋ 8·分頁，共 24 格。
- 按鍵對應格號：F5–F12 → `a1 − 116`＝0…7（見 (b)）。F5 是上列第 1 格，F9 是下列第 1 格。

**擴充巨集 `EX_MACRO_WINDOW`（順帶取得）：** 共 12 格，4 欄 × 3 列，格距同為 34。原點是 `EX_MACRO_FRAME` ＋ (10, 20)＝**(495, 495)**，範圍到 (+146, +122)。
- 範圍：TW16 `sub_ECEA70`（FO `0xACDE70`）
- 繪製：`sub_ED4530`（FO `0xAD3930`）的 `for i<12`
- 選取格是 `dword_13CE800`
- `byte_13CE5EA` 決定選取框畫在主巨集還是擴充巨集

**UI 原點偏移：** TW16 的 `dword_154524C`／`dword_1545250`（上面公式都要加上它）只有兩處寫入：`0xEBE06D`、`0xEBE077`，都是 `mov …, 0`。所以在 800×600 下是 0。這也補了 V1「待驗證」的那一項。

#### (b) F1–F4、Map 與其他快捷鍵（已驗證，TW13；TW16 對照）

**鍵碼編碼：** 視窗程序把 `WM_KEYDOWN` 轉成 `vk | 0x1000`，`WM_SYSKEYDOWN`（按住 Alt）轉成 `vk | 0x8000`。`WM_CHAR` 的 32–126 直接送聊天輸入。依據：
- TW16 `sub_8BD990`（FO `0x4BCD90`）的 `off_136EB78(wParam|0x1000, …)`／`(wParam|0x8000, …)`
- TW13 事件處理 `sub_4D3910`（FO `0xD3910`）的 `case 4208…` 與 default 分支

Ctrl 旗標是 `dword_9AB5E4 & 0x4000`（TW16 為 `dword_13CE3E0`）。按鍵前處理是 TW13 `sub_4DEC00`（FO `0xDEC00`）與 `sub_4DEEE0`（FO `0xDEEE0`），TW16 對應 `sub_8C1290`（FO `0x4C0690`）與 `sub_8C21B0`（FO `0x4C15B0`）。

| 按鍵 | 作用 | 出處（TW13） | TW16 |
|---|---|---|---|
| **F1／F2／F3** | 切換巨集分頁 1／2／3（同 `RIGHT_WINDOW` 的 action 10–12 分頁鈕）。只在遊戲中（狀態 3）有效 | `sub_4DEC00` → `Status800` vtable+88＝`sub_783A60`（FO `0x383A60`，`this[69]=頁`）；`sub_4D3910` 的 `case 4208/4209/4210` 也呼叫同一函式 | `sub_9F86C0`：選 `SlotWidgetUI` 的分頁鈕 |
| **F4** | 撿起身邊的地面物品：掃描 type 9 物件，送 opcode 23 `"chhddc"`，節流 986 ms（旗標 `0x10000` 時 600 ms） | `sub_40EE00`（FO `0xEE00`） | `sub_500640`（FO `0xFFA40`）。同一個送出路徑已記在 `rea-inventory.md` B07 |
| **F5–F12** | 使用目前分頁的第 0–7 格巨集 | `sub_4DEC00` → `sub_782480(vk−116, 0)`（FO `0x382480`）；雙擊格子也走這裡（`sub_782740` 的 `case 515`） | 送到 `SlotWidgetUI` `sub_9FAAD0` |
| **Ctrl＋M** | 開關野外／地監地圖窗 `dword_C2F7DC`：先隱藏 `dword_C2F7E0`、`dword_C2F7C4`，再以玩家座標開圖（`sub_5DD7B0`）。地圖類型 4 時改顯示系統訊息 #1734 | `sub_4DEC00` | `sub_8DCC40`／`sub_8DCAC0`，開關 `MapUI` |
| **Map 按鈕（action 13）** | 與 Ctrl＋M 相同，另外播放音效 237 | action 表：TW13 VA `0x8F023C` 起每筆 4 byte，第 13 筆（VA `0x8F0270`／FO `0x4F0270`）＝`sub_77F510`（FO `0x37F510`）；第 8 筆＝`sub_77F4B0`（MACRO_LOCK 切換 `byte_C31EAF`） | — |
| **Alt＋F3** | 循環切換 (485, 475) 那一格，見 (e) | `case 32882`（`0x8072`）→ `sub_7927A0` | 無（Status800 已停用） |
| Alt＋F2 | 視窗大小模式在 4→5→6→7→4 之間循環（`dword_963E54`），不支援時顯示訊息 #1727 | `case 32881` → `sub_4488B0`。對照 TW16 `sub_81AC20`（FO `0x41A020`）的表：4＝400×300、5＝800×600、6＝1200×900、7＝1600×1200（**推測**是輸出縮放，未展開） | — |
| Ctrl＋F1–F12 | 讀工作目錄的 `teleport_F%d.txt`，有檔才動作（開發用傳送清單） | `sub_4DE020`（字串 VA `0x8D1688`） | `sub_8BA150`（FO `0x4B9550`） |
| Ctrl＋J、Ctrl＋N | 開關兩個面板（`sub_42DEE0(dword_980988)`、`sub_402800(dword_BDC7D4)`），面板名稱未查 | `sub_4DEC00` | `sub_7D3400`／`7D3640`；Ctrl＋N 切換 `dword_13AA8D8` |
| Shift＋←／→ | `byte_9A1944` 為真時，在 `dword_9A1948` 的 0–7 之間循環，用途未查 | `sub_4DEC00` | — |
| Alt＋0–4、Alt＋E／W／X | `sub_4D3910` 另有 `case 32816–32820`、`32837`、`32855`、`32856`，多數呼叫 `sub_416AC0(66–69, …)`，用途未查 | `sub_4D3910` | — |

**結論：** F1–F4 在原版**不是巨集鍵**，F1–F3 是分頁、F4 是撿物。Map 的快捷鍵是 **Ctrl＋M**。引擎目前用 F5 開小地圖，與原版巨集鍵衝突（第 7 節 #12），應改成 Ctrl＋M。

#### (c) 世界畫面範圍（待驗證；目前證據傾向「800×600 底列覆蓋」）

- **已驗證：** tooltip 會限制在 800×470 內。超出 y＝470 時改畫到游標上方（減高度再減 34）。出處：TW13 `sub_4CBAC0`（FO `0xCBAC0`），TW16 `sub_8AECE0`（FO `0x4AE0E0`）。所以 UI 層把 y≥470 當成底列。
- **已驗證：** `CENTER_WINDOW` 聊天窗最大 240 高，會往上蓋到 y＝360。也就是說，至少在 360–470 之間，底列元件是疊在世界上面畫的。
- **已驗證：** HP／MP 浮動窗的預設位置是 (135, 465)（V1）。
- **未找到：** 世界層的裁切矩形或攝影機中心常數。在兩個樣本中，同時有 800 與 470 立即數的函式只有 tooltip 與一段聊天相關的 `sub_414AA0(140, …, 470, …)`。沒有出現 (400, 235) 這類「800×470 中心」的常數組合。可以確認的只有：輸出解析度表都是整個 800×600。
- **建議：** C2 維持 `UiRoot.WorldRect = 800×600、由底列覆蓋` 的預設，這和上述證據不衝突。真正定案需要真機目視，或下一輪去追地圖繪製迴圈（從 `sub_4D7E50` 建立的世界視圖物件往下追）。

#### (e) (485, 475) 槽位切換（已驗證，TW13）

`Status800` 的成員：
- `+0x1F4` 模式
- `+0x1F8` 目前子視窗
- `+0x1FC` 隊伍／寵物 `StatusPartyPet`
- `+0x200` 小地圖 `StatusMiniMap`
- `+0x204` 擴充巨集 `StatusMacroExtend`
- `+0x208` `ChatFrameSub`

繪製（`sub_7928D0`，FO `0x3928D0`）只畫「目前子視窗」，所以三者一次只顯示一個。切換函式是 `sub_7923E0`（FO `0x3923E0`）：

| 模式 | 子視窗 | 附帶動作 |
|---|---|---|
| 0 | 小地圖 | 進入時用目前地圖 id 呼叫 `sub_78DA00` 設定小地圖內容（見第 5 節） |
| 1 | 擴充巨集 | 離開模式 1 時清 `byte_9AB625` |
| 2 | 隊伍／寵物 RT | 沒有隊員但有寵物時，自動切到寵物分頁（`sub_78D560(1)`） |
| 3 | `ChatFrameSub` | 進出模式 3 時重建 MACRO_LOCK 鈕：模式 3 用 surf 3293 位於 (643, 523)，其他模式用 surf 3191 位於 (634, 513)。同時互換子視窗 19／20、把聊天相關偏移設為 −48／＋48，並在巨集框左上 −1 處加畫 surf 3292。**語義推測**是「聊天延伸到右側」的版面 |

**觸發來源**（`sub_7923E0` 的所有呼叫者）：

1. **Alt＋F3**：`sub_4D3910`、`sub_4D4510` 的 `case 32882`（另一個跳躍表入口在 `0x4D5321`）都呼叫 `sub_7927A0`（FO `0x3927A0`），循環 0→1→2→3→0。
2. **選項指令 `uitype`**（字串 VA `0x8CB504`／FO `0x4CB504`）：`sub_4116A0` 的同一個循環，屬於 option 指令那一組。
3. **進入遊戲時重設成 0**：`sub_4B5D00`，由 initGame 呼叫。
4. **載入設定**：
   - `sub_4B6530`（FO `0xB6530`）用設定旗標的 bit0（＋1）與 bit1（＋2）組出 0–3。
   - `sub_4BA250` 用設定字串 `abs(atoi(值)) % 3` 得到 0–2，呼叫點在 `0x4BAF02`。

**沒有找到點擊格子本身就切換的路徑：** 三個子視窗的滑鼠處理都不呼叫 `sub_7927A0`／`sub_7923E0`。所以原版是用**快捷鍵或設定**切換，沒有專用按鈕。

TW16 保留了相同的成員配置（`this[123]` 目前、`this[125]` 小地圖、`this[126]` 擴充巨集），但切換函式已移除，`StatusPartyPet` 也不再建立。

#### 推測（V5）

- 模式 3（`ChatFrameSub`）的實際畫面：依 surf 3292／3293 與聊天偏移 ±48 推測是「聊天延伸」版面，未渲染核對。
- Alt＋F2 的 4–7 是視窗輸出縮放倍率：依 TW16 解析度表推測。

#### 待驗證（V5）

- (c) 世界層是否畫到 y＝470 以下（見上）。
- `Status800` 使用的設定旗標從哪裡讀進來（`sub_4B6530` 的呼叫者 `sub_4B8A10`），以及預設值。
- `LiTo.bin`（實際遊玩的 3.81 客戶端）是否與 TW13 行為相同：加殼，無法靜態確認。

## 3. 聊天

### 已驗證：聊天窗版面（`CENTER_WINDOW`）

- **聊天窗：** 底部錨定在 y=600。`CENTER_WINDOW` 的 y=470，所以最小高度 130；最大高度是底圖高 240。上／下鈕位於 (345, 115)，每次伸縮 36 px。
- **輸入列：** surf 2273，尺寸 346×16，位於 (10, 114)。
- **頻道分頁：** 位於 x=8，y=10 到 115、間距 15。由上而下：上級、同盟、頻道、交易、全體、隊伍、血盟、一般。
- **過濾圖示：** 位於 y=5：一般 179、隊伍 198、血盟 221、交易 240、系統 258。
- **左右分割聊天：** `LCHAT_WINDOW` 3288 尺寸 260×240，`RCHAT_WINDOW` 3290 位於 x=260。

### 已驗證：聊天類型、格式與顏色

| 項目 | VA | File Offset | 說明 |
|---|---|---|---|
| 工廠 `sub_6BC550` | `0x6BC550` | `0x2BB950` | 依聊天類型 `switch` 建立 Printer |
| vtable 表 | `0x1165C80`–`0x1165D80` | `0xD64880`–`0xD64980` | 每筆是 [RTTI, 格式化, 上色輸出, …] |
| 格式字串 | `0x1165BFC`–`0x1165C70` | `0xD647FC`–`0xD64870` | 各類型的前綴與括號字串 |
| 輸出 `sub_7DC850` | `0x7DC850` | `0x3DBC50` | 參數為 (文字, **文字色**, 第二色, 類型, −1, …) |
| 調色盤 `dword_1369D70[]` | `0x1369D70` | `0xF68570` | 有初值的 RGB555 陣列。80 處參照全部是讀取，沒有寫入 |

下表顏色欄的 hex 值由 RGB555 依 ×255/31 換算。

| 類型 | RTTI 類別 | 顯示格式 | 文字色（調色盤索引） | 格式化函式 VA／FO | 上色函式 VA／FO |
|---|---|---|---|---|---|
| 0 一般 | `ChatTextNormalPrinter` | `name: msg`；跨服時前面加 `[伺服器] ` | 白 #FFFFFF（14）。訊息以單一 `*` 開頭時改為黃 #FFFF00（13） | `0x6BE250`／`0x2BD650` | `0x6BD820`／`0x2BCC20` |
| 1 收密語 | `ChatTextRecvWhisperPrinter` | `(name) msg` | #FFCECE（32） | `0x6BE8E0`／`0x2BDCE0` | `0x6BDB00`／`0x2BCF00` |
| 送密語 | `ChatTextSendWhisperPrinter` | `-> (target) msg` | #FFF7B5（24） | `0x6BEA50`／`0x2BDE50` | `0x6BDB50`／`0x2BCF50` |
| 2 喊話 | `ChatTextShoutPrinter` | `[name] msg`。依發話者相對方向在前面加 `\d<方向>`，並在頭上顯示對話泡泡 | #FFE6CE（33） | `0x6BE820`／`0x2BDC20` | `0x6BDBA0`／`0x2BCFA0` |
| 3 全體 | `ChatTextWorldPrinter` | `[name] msg` | 預設 #ADEFEF（44）。訊息自帶色號時用調色盤[色號]。CP950 且以 `*` 開頭時為紅 #FF0000（3） | `0x6BED30`／`0x2BE130` | `0x6BDEB0`／`0x2BD2B0` |
| 4 血盟 | `ChatTextPledgePrinter` | `{name} msg` | #C5EFB5（11），第二色 #BDBDBD（7） | `0x6BE760`／`0x2BDB60` | `0x6BDAA0`／`0x2BCEA0` |
| 11 隊伍 | `ChatTextPartyPrinter` | `(name) msg` | #FFB500（53） | `0x6BE470`／`0x2BD870` | `0x6BD9B0`／`0x2BCDB0` |
| 12 交易 | `ChatTextTradePrinter` | `[name] msg`，交易頻道開啟時才顯示 | #BDA56B（34） | `0x6BE820`／`0x2BDC20` | `0x6BDE40`／`0x2BD240` |
| 13 盟主／守護 | `ChatTextPledgePrincePrinter` | `{{name}} msg` | #FF84FF（47） | `0x6BE6A0`／`0x2BDAA0` | `0x6BDA50`／`0x2BCE50` |
| 14 聊天隊伍 | `ChatTextGroupPrinter` | `(name) msg` | #BDEFE6（29） | `0x6BE050`／`0x2BD450` | `0x6BD7D0`／`0x2BCBD0` |
| 15 同盟 | `ChatTextAlliancePrinter` | `[name] msg` | #CED6FF（30） | `0x6BDF90`／`0x2BD390` | `0x6BD780`／`0x2BCB80` |
| 17 血盟公告 | `ChatTextPledgeNoticePrinter` | `{name} msg` | #00BD00（22） | `0x6BE5E0`／`0x2BD9E0` | `0x6BDA00`／`0x2BCE00` |
| 18 黃金號角 | `ChatGoldenHornPrinter` | `[name] msg` | 調色盤[訊息色號 1–95] | `0x6BE820` | `0x6BD720`／`0x2BCB20` |
| 25 彩色 | `ChatColorPrinter` | `[name] msg` | 調色盤[訊息色號] | `0x6BE820` | `0x6BD680`／`0x2BCA80` |
| 29 隊伍（Team） | `ChatTextTeamPrinter` | `[name] msg` | #FF84FF（47） | `0x6BEBC0`／`0x2BDFC0` | `0x6BDDF0`／`0x2BD1F0` |

除血盟外，各類型的第二色都是白（索引 14）。

**輸入前綴（已驗證字元集合）：** `sub_7DE200`（`0x7DE200`／FO `0x3DD600`）與 `sub_502D80`（`0x502D80`／FO `0x102180`）處理輸入：
- `"` 開頭是密語，名稱取到第一個空白為止。
- `!`、`&`、`@`、`#`、`$` 是頻道前綴字元。
- `/` 開頭走指令。
- 洗頻冷卻用的檢查清單是 `" ! # @ % ' $`。
- 文字中的色碼 `\f0`–`\f7`、`\b0`–`\b7` 會轉譯（數字＋34），`\i0`–`\i5` 原樣保留。
- CP950 輸入法狀態下，前綴後會插入 `\c`。

**交叉驗證：** TW13 沒有這組 Printer 類別，也沒有 `{{%s}} %s`、`-> (` 字串，所以聊天 Printer 是 2013 年後才改寫的。但調色盤前 77 筆在 TW13 的 VA `0x95FB38`（FO `0x55FB38`）逐 byte 相同。

### 推測

- **前綴字元→頻道的對應：** 送出鏈 `sub_7DD700` → `sub_7F2320` → `sub_6C6C60` 中尚未找到對照表。依天堂慣例，推測為：

  | 前綴 | 頻道（類型） |
  |---|---|
  | `!` | 喊話（2） |
  | `&` | 全體（3） |
  | `@` | 血盟（4） |
  | `#` | 隊伍（11） |
  | `$` | 交易（12） |

- **調色盤格式：** 依索引 1／2／3 分別為 `0x001F`／`0x03E0`／`0x7C00`，判斷是 R 在高位的 RGB555。若執行時改用 565 模式，可能會再轉換，本輪未查。

## 4. 狀態（buff）圖示

### 新版 protobuf 路徑（T161，已驗證）

- 2016 客戶端有 protobuf 訊息 `LIN.SC_SPELL_BUFF_NOTI`，descriptor 位於 VA 約 `0x11B5D00`。欄位有 `noti_type`（NEW／RESTAT／END）、`spell_id`、`duration`、`duration_show_type`、`on_icon_id`、`off_icon_id`、`icon_priority`、`tooltip_str_id`、`new_str_id`、`end_str_id`、`is_good`、`overlap_buff_icon`、`main_tooltip_str_id`、`buff_icon_priority`。另有 `SC_BUFF_ICON_ON_OFF_NOTI`（`iconid`、`on`）。
- d287 伺服器不送 `SC_SPELL_BUFF_NOTI`，所以這條路在 P24Lab 不會被觸發。以下各小節（V3，#458）查的是**舊封包**走的路徑。
- 除錯字串 VA `0x1183E1C`–`0x1184C34` 的 `STATUS_EFFECT_*`／`qc*` 在 IDB 中**沒有任何 xref**（本輪逐條查證）。它們不是圖示的資料來源，只是殘留字串。

### V3 結論（#458）

1. **效果表是 `Tile.pak` 的 `effectlist2.xml`，不寫在程式碼裡。** 總共 540 筆，id 0–539。每筆有 `icon`、`offIcon`、`kind`、`priority`、`duration`、`group`、`startMsg`／`endMsg`、`graphic` 等欄位。舊封包處理函式只負責把封包換成 **effect id**，再用這個 id 查表取得圖示。
2. **圖示不是 SurfList 的 `.img`，而是 `<icon>.tbt`。** 尺寸 32×32，放在 `Sprite*.pak`。`ui-core-resources`（C1）的 `SurfCatalog` 只處理 `.img`，所以 `ui-buff-icons` 還需要一個 `.tbt` 讀取器（見「給下一棒」）。
3. **位置：畫面右上角，從右往左排，每列 22 格。** 格子 32×32，間距 35 px。第 n 格（從 0 起算）的左上角是 `x = 798 − 35·(n mod 22 + 1)`、`y = 5 + 35·⌊n/22⌋`，所以第一格位於 (763, 5)。增益排在前面；減益（`kind` 為 slow／curse／paralyse）從增益佔用的下一列開始排。
4. **排序：** 先比 `priority`（數字小的在前）。沒有 `priority` 的減益依 kind 排（slow 0、curse 1、paralyse 2）。其餘的依到達順序接在後面。另有一個客戶端選項（旗標 `0x800`）會改成依剩餘時間排序。
5. **剩餘時間不畫在圖示上。** 只在滑鼠移上去時的 tooltip 顯示，舊封包的格式是「`<字串930>: <秒> <字串931>`」。時間用完但伺服器還沒送移除時，圖示改畫成 `offIcon`，tooltip 改成字串 2854。`effectlist2.xml` 沒有任何一筆使用 `gaugePng`，所以本機資源下不會出現量表覆蓋圖。

### 已驗證：資料流與地址

| 項目 | VA | File Offset | 說明 |
|---|---|---|---|
| 載入表 `sub_AA0610` | `0xAA0610` | `0x69FA10` | `dword_153F5A8 = sub_81D2D0(off_137B3A8[36])`；`off_137B3A8+36`（VA `0x137B3CC`／FO `0xF79BCC`）指向字串 `effectlist2.xml`（VA `0x119C8F4`／FO `0xD9B4F4`）。BIN 中沒有任何地方引用 `effectlist.xml`（無 2） |
| XML 解析 `sub_81DF30` | `0x81DF30` | `0x41D330` | `<effect>` 屬性 → 152 byte 紀錄，存在 `dword_13D4304`（bss）。屬性對應的偏移：`icon`+0、`duration`+4、`group`+8、`startMsg`+12、`tooltipMsg`+16、`endMsg`+20、`remainInArea`+24、`remainWhenDie`+25、`graphic`+28、`kind`+32（byte）、`offIcon`+36、`priority`+44、`addChatMsg`+48、`bufDesc`+52、`spellId`+56、`foodId`+60、`overrideId`+64、`gaugePng`+80、`gaugeMin`+84、`gaugeMax`+88、`globalEffectId`+92、`location`+120、`gaugeType=circle`+148。id 與出現順序不一致時，記錄 `Order fault` |
| kind 列舉 | `off_138D8FC` | `0xF8C0FC` | 13 個：0 slow、1 curse、2 paralyse、3 einhasad、4 security、5 dragon、6 magic doll、7 cook、8 speed、9 attack、10 defense、11 neutrality、12 legacy |
| 加入圖示 `sub_F05A90(秒, effect, 顯示模式, …)` | `0xF05A90` | `0xB04E90` | 舊封包處理函式統一呼叫這個函式。「秒 = 0 且該效果已存在」時改為移除。`sub_F6D560`（`0xF6D560`／FO `0xB6C960`）遇到 `effect ≥ 筆數`（`sub_9015E0` 回傳 +44 的 count）時回傳 0，**不建立圖示** |
| 移除圖示 `sub_F059F0(effect)` | `0xF059F0` | `0xB04DF0` | — |
| 圖示類別 | `LStatusEffectButton`（RTTI） | — | 建構子 `sub_8FB4D0`（FO `0x4FA8D0`）。寬高固定 32×32（`sub_8FBC30` 傳入 32, 32） |
| 分組 `sub_8FD8E0` | `0x8FD8E0` | `0x4FCCE0` | `sub_9027F0`：`kind` 為 0／1／2 時進入減益清單（容器 +12，插入函式 `sub_8FD5B0`），其餘進入增益清單（容器 +4，插入函式 `sub_8FD3D0`） |
| 版面 `sub_903470` | `0x903470` | `0x502870` | 先排 +4 清單，再排 +12 清單。座標公式見上；減益的起始列 = ⌈增益數/22⌉（含隱藏的格子）。`+21` 旗標為真的格子不排 |
| 排序鍵 `sub_903EB0` | `0x903EB0` | `0x5032B0` | `priority > 0` → `priority`；`kind` 為 0–2 → `kind`；其他 → 100 |
| 比較器（預設）`sub_8FDE00` | `0x8FDE00` | `0x4FD200` | 任一方的鍵 < 100 時比鍵；兩方都 ≥ 100 時比加入時刻（早的在前）。新圖示插在第一個「比較器為真」的元素前面，否則放在最後 |
| 比較器（選項）`sub_8FDD20` | `0x8FDD20` | `0x4FD120` | 由 `dword_13D4308` 選用，來源是選項旗標 `& 0x800`（`sub_89A0F0`、`sub_999620`）。鍵 ≥ 100 時：無期限（時間 < 0）在前，其餘依剩餘毫秒 `1000·秒 − 已過時間`（`sub_903650`）比較 |
| 繪製 `sub_9001F0` | `0x9001F0` | `0x4FF5F0` | 平常畫 `icon`。`sub_902840` 為真（`offIcon > 0`、模式 0 或 6、已過時間 > 秒數）時改畫 `offIcon`（`sub_97AD60`，參數 19）。`icon == 0` 時改畫 effect id 的數字。effect 470 另有特例 |
| 圖示載入 `sub_87B3A0` | `0x87B3A0` | `0x47A7A0` | 用 `"%d.tbt"`（VA `0x117D924`／FO `0xD7C524`）按編號讀取；失敗時記錄 `Icon Resource Error[%d]`。取用函式 `sub_88CE60`（FO `0x48C260`）。**不經過 SurfList.xml** |
| tooltip `sub_900FC0`＋`sub_9061E0` | `0x900FC0`／`0x9061E0` | `0x5003C0`／`0x5055E0` | 框寬 160，從圖示 (x, y+32) 開始，距右緣不足 5 px 時往左移。模式 0／6 的文字是 `"%s: %d %s"`（字串 930、剩餘秒、字串 931）；期限為 −1 或已過期時顯示 `--`；處於 offIcon 狀態時顯示字串 2854。其他模式：1／12 為百分比，2 為數值＋單位，4 為分鐘，7／8 為日時分，3 不顯示 |
| 量表 `sub_900BE0`＋`sub_8FDAF0` | `0x900BE0`／`0x8FDAF0` | `0x4FFFE0`／`0x4FCEF0` | 只有 `gaugePng != -1` 時才畫：把 png 依已過時間的百分比裁切後蓋上去。**本機 `effectlist2.xml` 沒有任何一筆設定 `gaugePng`** |

### 已驗證：舊封包 → effect id

TW16 的封包編號與 3.81 不同。下表依 `t160_opmap.py` 的分派表找出處理函式（分派在 `sub_947530`，FO `0x546930`）；名稱的對應依格式比對。

| d287 伺服器封包（serializer 格式） | TW16 處理函式（讀取格式） | 條件 → effect |
|---|---|---|
| `S_SkillHaste` 255（D obj、C mode、H 秒） | `S_SPEED` 93 → `sub_94C530`（`dch`，只處理自己） | mode 1 → **0**（並移除 380）；mode 2 → **1**；其他 → 移除 0、1、380 |
| `S_SkillBrave` 67（D obj、C mode、H 秒） | `S_EMOTION` 92 → `sub_935260`（`dch`，只處理自己） | mode 1 → **2**（妖精改為 19）；3、5 → **2**；4 → 依職業：妖精 **24**、法師 **42**、幻術師 **153**、其他 **37**；6 → **110**；8 → **19**；0 → 移除 2、37、110、19 |
| `S_SkillIconShield` 216（H 秒、C type） | `S_MAGE_SHIELD` 69 → `sub_940D80`（`hc`） | type 3 → **5**；5 → **482**；6 → **6**；7 → **7**；10 → **8**；其他 → **4** |
| `S_Strup` 166（H 秒、C str、C 負重、C type） | `S_MAGE_STRENGTH` 4 → `sub_940E70`（`hccc`） | type 3 → **34**；其他 → **9**。秒 = 0 且 type 1 → 移除 34、9 |
| `S_Dexup` 188（H 秒、C dex、C type） | `S_MAGE_DEXTERITY` 196 → `sub_940C80`（`hcc`） | type 3 → **35**；其他 → **10**。秒 = 0 且 type 1 → 移除 35、10 |
| `S_SkillIconBlessOfEva` 126（D obj、H 秒） | `S_BREATH` 17 → `sub_931F60`（`dh`） | → **11** |
| `S_Invis` 171（D obj、C type） | `S_INVISIBLE` 160 → `sub_940330`（`dc`，只處理自己） | 開 → **396**（黑妖且旗標 `byte_13D4403` 時改為 **377**），秒數取該效果的預設 `duration`；關 → 移除 377、396 |
| `S_Paralysis` 202 | `S_PARALYSE` 33 → `sub_945BD0`（`ch`） | 22 → **62**；24 → **61**；28 → 551；26 → 552；30 → 571；2／4 → **395**（帶秒數）；23／25／27／29／31 解除對應的效果。551、552、571 ≥ 540，**本機表中不存在** |
| `S_PacketBox` 250 / 20 `S_Icons` 快照 | `S_EVENT` 108 → `sub_9357E0` → `sub_927320` | 依 byte 位置對應，見下方快照表 |
| 250 / 21（`S_PacketBoxIcon2`：H 秒、C type） | 同上，case 0x15 | TW16 讀 **4 個 byte**，每個 ×16−8 秒。黑妖 → 31、29、30、32；幻術師 → 只有第 4 個，對應 116；其他 → 14、17、15、16。**與伺服器格式不同**，見「待驗證」 |
| 250 / 22 `S_PacketBoxIconAura`（C icon、H 秒） | case 0x16 | icon 113 → **25**；114 → **26**；116 → **27**；119 → **28**；147 → **18**；148 → **21**；154 → **19**；155 → **22**；162 → **20**；165 → **23**；221＋C v → 1：**78**、2：**77**、6：**132**。90／152 在秒 = 0 時移除 71／55 |
| 250 / 34（H 秒、C type） | case 0x22 | type 1 → **38**；type 3 → **481**（地圖 2465 時改為 91） |
| 250 / 35 變身（H 秒） | case 0x23 | 依目前外形：`sub_4FA320` 為真 → 75；`sub_927170` 為真 → 595（≥ 540，不存在）；其他 → 用外形 gfx 查 `graphic`，查不到時用 **39** |
| 250 / 36 禁言（H 秒） | case 0x24 | → **63** |
| 250 / 40 聖結界（H 秒） | case 0x28 | → **41** |
| 250 / 44 風之枷鎖（D obj、H 格數） | case 0x2C（`dc`） | 自己時 → **52**，秒 = 4·C |
| 250 / 57（C 44、H 秒） | case 0x39 | 第一個 byte 為 44 → **3** |
| 250 / 154 技能圖示（H 秒、D gfx 或 H key＋H 0、H flag） | case 0x9A（`H`、`D`、`H`） | 用 gfx 查 `graphic` 欄位（`sub_81DCA0`，FO `0x41D0A0`，取第一筆）。gfx 2165 → **12**、747 → **379**、748 → **394**、2176 → **13**、2951 → **392**。代理 key 518–537 的 `graphic` 正好是 518–537，所以對應 **effect 518–537**（`*_INDEPENDENT`）。flag = 1 時，747／2165／2176 改為 555／562／554（≥ 540，不存在）。秒 ≤ 0 → 移除 |

快照（250/20，`sub_927320`，FO `0x526720`）：資料最多讀 214 byte。下表只列引擎 `MagicStatusCatalog.Slots` 用到的位置。秒數依各格的倍率換算；倍率是 TW16 讀取時用的值。

| byte | TW16 秒數換算 | effect | byte | TW16 秒數換算 | effect |
|---|---|---|---|---|---|
| 0 | 4n−2 | 45 | 21 | 4n−2 | 55 |
| 1、2 | （TW16 不讀） | — | 22 | 4n−2 | 54 |
| 3 | **16n−8** | 47（byte 182 = 240 時為 480） | 23 | 16n−8 | 56（byte 146–147 = 4400 時），否則 66 |
| 4 | 4n−2 | 48 | 24 | 4n−2 | 53 |
| 5 | 4n−2 | 43 | 26 | 4n−2 | 58（職業非 0 時），否則 27 |
| 7 | 4n−2 | 33 | 30／31／32 | 4n−2 | 67／72／69 |
| 8／9 | 4n−2 | 51／50 | 52／53／54 | 16n−8 | 120／128／129 |
| 10＋11 | 4n−2 | byte 11 = 147／148／154／155／162／165 → 18／21／19／22／20／23 | 55／56／57／58／59 | 4n−2 | 113／114／111／123／108 |
| 17 | 4n−2 | 36（byte 29 ×4 = 636 時為 68） | 60 | 16n−8 | 107（byte 193 = 158 時為 7） |
| 18／19／20 | 4n−2 | 40／46（加 byte 169 為高位）／52 | 61 | （TW16 不讀） | — |

### 已驗證：effect → 圖示（本機 `effectlist2.xml`）

數值由 `rea-scripts/buff_icon_table.py` 產生。`icon` 與 `offIcon` 都是 `.tbt` 編號，所列全部都是 32×32（`.tbt` 第 3、4 byte）。「S」表示 `Sprite.pak`，「S07」表示 `Sprite07.pak`；「雙份」表示同時出現在 `Sprite.pak` 與某個 `SpriteNN.pak`，而且內容不同。

| effect | 名稱 | kind | priority | icon | offIcon |
|---|---|---|---|---|---|
| 0 | HASTE_S | speed | 5 | 5397（S00） | 5398（S01） |
| 1 | SLOW_S | slow | — | 480（雙份 S04） | — |
| 2 | SPEED_DOUBLE_S（勇敢） | cook | 6 | 5395（S14） | 5395 |
| 3 | POTION_PRUDENCE_S | cook | — | 484（雙份 S08） | — |
| 4 | SHIELD_S | defense | — | 485（雙份 S09） | 5271（S07） |
| 5 | OLD_SHADOW_ARMOR | defense | — | 1104（雙份 S14） | 5272（S08） |
| 6 | EARTH_SKIN_S | defense | — | 763（雙份 S08） | 5273（S09） |
| 7 | BLESS_OF_EARTH_S | defense | — | 725（雙份 S06） | 5274（S10） |
| 8 | IRON_SKIN_S | defense | — | 783（雙份 S10） | 5275（S11） |
| 9 | ENCHANT_MIGHTY_S | attack | — | 482（雙份 S06） | 5246（S09） |
| 10 | ENCHANT_DEXTERITY_S | attack | — | 483（雙份 S07） | 5247（S10） |
| 11 | WATER_BREATH_S | cook | — | 552（雙份 S04） | — |
| 12 | HOLY_WEAPON_S | attack | — | 777（雙份 S13） | 5248（S11） |
| 13 | BLESS_WEAPON_S | attack | — | 728（雙份 S09） | 5267（S12） |
| 14 | RESIST_MAGIC_S | defense | — | 813（雙份 S04） | 5276（S12） |
| 15 | RESIST_ELEMENT_S | defense | — | 804（雙份 S04） | 5277（S13） |
| 16 | PROTECTION_FROM_ELEMENT_S | defense | — | 807（雙份 S07） | 5278（S14） |
| 17 | CLEAR_MIND_S | defense | — | 745（雙份 S08） | 5279（S15） |
| 18 | FIRE_WEAPON_S | attack | — | 774（S＋S10，相同） | 5249（S12） |
| 19 | BREATH_OF_FIRE_S | attack | — | 735（S＋S07，相同） | 5250（S04） |
| 20 | BURNING_WEAPON_S | attack | — | 738（雙份 S10） | 5251（S05） |
| 21 | WIND_SHOT_S | attack | — | 855（雙份 S10） | 5252（S06） |
| 22 | EYE_OF_STORM_S | attack | — | 771（雙份 S07） | 5253（S07） |
| 23 | STORM_SHOT_S | attack | — | 827（雙份 S09） | 5254（S08） |
| 24 | WIND_WALK_S | speed | 6 | 5241（S04） | 5242（S05） |
| 25 | GLOWING_AURA_S | defense | — | 880（雙份 S08） | 5280（S07） |
| 26 | SHINING_AURA_S | defense | — | 1567（雙份 S11） | 5281（S08） |
| 27 | BRAVE_AURA_S | defense | — | 1557（雙份 S10） | 5282（S09） |
| 28 | NO_USE_DIVINE_SACRIFICE | defense | — | 890（S） | — |
| 29 | ENCHANT_VENOM_S | attack | — | 1101（雙份 S11） | 5255（S09） |
| 30 | BURNING_SPIRITS_S | attack | — | 1113（雙份 S14） | 5256（S10） |
| 31 | DOUBLE_BREAK_S | attack | — | 1122（雙份 S14） | 5257（S11） |
| 32 | ENCANNY_DODGE_S | defense | — | 1125（雙份 S01） | 5283（S10） |
| 33 | VENOM_RESIST_S | defense | — | 1119（雙份 S04） | 5284（S11） |
| 34 | DRESS_MIGHTY_S | attack | — | 1558（雙份 S11） | 5258（S12） |
| 35 | DRESS_DEXTERITY_S | attack | — | 1559（雙份 S12） | 5259（S13） |
| 36 | DRESS_EVASION_S | defense | — | 1608（雙份 S07） | 5285（S12） |
| 37 | MOVING_ACCELERATION_S | speed | 6 | 1110（雙份 S11） | 5243（S06） |
| 38 | POTION_OF_MANA1_S | cook | 8 | 5399（S02） | 5400（S01） |
| 39 | POLYMORPH_S | cook | 4 | 1477（雙份 S11） | — |
| 40 | BERSERKERS_S | neutrality | — | 1556（雙份 S09） | 5320（S02） |
| 41 | IMMUNE_TO_HARM_S | defense | — | 1562（雙份 S06） | 5286（S13） |
| 42 | HOLY_WALK_S | speed | 6 | 1561（雙份 S05） | 5244（S07） |
| 43 | ABSOLUTE_BARRIER_S | defense | — | 1554（雙份 S07） | 5287（S14） |
| 45 | MEDITATION_S | defense | — | 795（雙份 S13） | 5289（S00） |
| 46 | NATURES_TOUCH_S | defense | — | 1609（雙份 S08） | 5290（S08） |
| 47 | DECREASE_WEIGHT_S | defense | — | 757（雙份 S11） | 5291（S09） |
| 48 | DECAY_POTION_S | curse | — | 754（雙份 S08） | — |
| 49 | SILENCE_S | curse | — | 824（雙份 S06） | — |
| 50 | DISEASE_S | curse | — | 760（雙份 S05） | — |
| 51 | WEAKNESS_S | curse | — | 852（雙份 S07） | — |
| 52 | WIND_SHACKLE_S | slow | — | 1623（雙份 S04） | — |
| 53 | ELEMENTAL_FALLDOWN_S | curse | — | 1740（雙份 S04；其中一份 30×30） | — |
| 54 | COUNTER_MIRROR_S | defense | — | 1733（雙份 S06） | 5292（S10） |
| 55 | ERASE_MAGIC_S | curse | — | 1834（雙份 S08） | — |
| 56 | EXOTIC_VITALIZE_S | defense | — | 1799（雙份 S02） | 5293（S11） |
| 58 | ELEMENTAL_FIRE_S | attack | — | 1736（雙份 S09） | 5260（S05） |
| 61 | HOLD_S | paralyse | — | 1620（S） | — |
| 62 | STUN_S | paralyse | — | 1626（雙份 S07） | — |
| 63 | NO_CHAT_S | paralyse | — | 1476（雙份 S10） | — |
| 66 | ADDITIONAL_FIRE_S | defense | — | 2339（S09） | 5298（S00） |
| 67 | STRIKER_GALE_S | curse | — | 2357（S09） | — |
| 68 | AQUA_PROTECT_S | defense | — | 2342（S03） | 5299（S01） |
| 69 | POLLUTE_WATER_S | curse | — | 2348（S09） | — |
| 71 | COUNTER_BARRIER_S | defense | — | 2345（S06） | 5301（S01） |
| 72 | SOUL_OF_FLAME | attack | — | 2354（S06） | 5261（S06） |
| 77／78 | KARMA_BUF | neutrality | — | 2433（S04）／2432（S03） | — |
| 107 | DRAGON_SKIN_S | defense | — | 3080（S03） | 5302（S02） |
| 108 | GUARD_BREAK_S | curse | — | 3187（S11） | — |
| 110 | BLOODLUST_S | speed | 6 | 3075（S07） | 5245（S08） |
| 111 | FEAR_S | curse | — | 3190（S05） | — |
| 113 | MOTAL_BODY_S | defense | — | 3089（S12） | 5303（S03） |
| 114 | HORROR_OF_DEATH_S | curse | — | 3188（S12） | — |
| 116 | MIRROR_IMAGE_S | defense | — | 3123（S01） | 5304（S04） |
| 120 | CONCENTRATION_S | defense | — | 3097（S11） | 5305（S05） |
| 123 | PATIENCE_S | defense | — | 3127（S05） | 5306（S06） |
| 128 | INSIGHT_S | attack | — | 3119（S06） | 5264（S09） |
| 129 | PANIC_S | curse | — | 3125（S03） | — |
| 132 | POTION_OF_DESTRUCT | neutrality | — | 2566（S11） | — |
| 153 | EMOTION_PRESSING_S | speed | 6 | 5395（S14） | — |
| 377 | BLIND_HIDING_S | defense | — | 1098（雙份 S10） | 5310（S01） |
| 379 | ENCHANT_WEAPON_S | attack | — | 5265（S10） | 5266（S11） |
| 380 | ENTANGLE_S | slow | — | 766（雙份 S11） | — |
| 381 | AREA_OF_SILENCE_S | curse | — | 722（雙份 S03） | — |
| 392 | SHADOW_FANG_S | attack | — | 1128（雙份 S04） | 5268（S13） |
| 394 | BLESSED_ARMOR_S | defense | — | 5316（S07） | 5317（S08） |
| 395 | CURSE_PARALYZED_S | paralyse | — | 5270（S06） | — |
| 396 | Invisiblity | neutrality | — | 5401（S02） | — |
| 465／466 | GM_BUFF_BRAVE／GLOWING_AURA_S | defense | — | 1557／880 | 5282／5280 |
| 480 | STR_NEW_JOY_OF_PAIN | defense | — | 5738（**缺 .tbt**） | 5739（**缺 .tbt**） |
| 481 | PC_MANA_POTION | cook | 8 | 5399（S02） | — |
| 482 | SHIELD_S | defense | — | 485 | 5271 |
| 518–537 | `*_INDEPENDENT`（代理 key） | cook | — | 依序 774、855、735、771、738、827、485、1104、763、725、783、483、1559、482、1558、880、1567、777、728、5265 | 依序 5249、5252、5250、5253、5251、5254、5271、5272、5273、5274、5275、5247、5259、5246、5258、5280、5281、5248、5267、5266 |
| 551、552、554、555、562、571、595 | — | — | — | **本機 effectlist2 沒有這些 id**（只有 0–539），客戶端不建立圖示 | — |

注意：代理 effect 518–537 的 kind 都是 `cook`，也沒有 `priority`，所以和一般增益一樣依到達順序排列。

**交叉驗證：**
- 本機 `Tile.pak` 另有舊表 `effectlist.xml`：396 筆，沒有 `offIcon`、`kind`、`priority`；例如 effect 0 的 icon 是 481，新表改成 5397。TW16 的字串 `effectlist.xml`（VA `0x11E480D`）沒有 xref，只有 `effectlist2.xml` 被載入，所以舊表不是 TW16 使用的資料。
- TW13081901（SHA-256 `653da514…`）已有 `effectlist2.xml`（FO `0x4DE774`）、`offIcon`、`priority`、`LStatusEffectButton` 字串，所以這套機制在 2013 年版就已經存在。TW13 中沒有 `%d.tbt` 字串，它怎麼讀圖示本輪未查。

### 推測

- **`SpellIcon.idx`（`Sprite.pak`，336 byte）**：內容是 168 個 u16，可排成 21 列 × 8 欄。數值是 383–1582 的 `.tbt` 編號，像是「技能等級列 × 8 格 → 技能書圖示」（例如第 0 列為 383–387、657、663、775）。TW16 與 `LiTo.bin` 中都**找不到** `SpellIcon` 字串，所以它不是 buff 圖示的來源。這張表是魔法視窗用的，還是已不再使用，本輪未確認。
- **offIcon 的意義**：從程式條件看，它是「時間已到、伺服器還沒送移除」時的灰階版本，不是倒數閃爍。這點還沒在真機上看過。
- **TW16 的秒數換算（4n−2、16n−8）**：可能是為了讓顯示的秒數略為保守。3.81 伺服器的 `S_Icons` 用的是 4n、16n。兩者差 2 到 8 秒，對圖示影響不大，但第 3 格倍率不同（引擎 4、TW16 16），需要真機核對。

### 待驗證

- **雙份 `.tbt` 的優先序**：本表用到的圖示中，有 56 個同時在 `Sprite.pak` 與 `SpriteNN.pak`，而且 56 份 `Sprite.pak` 版本彼此都不同，不是同一張預留圖。BIN 讀哪一份要依 V4 (e) 的封存檔優先序決定。在那之前，ui-buff-icons 遇到雙份應依 C1 契約拒絕載入，或等 V4 結果。
- **`.tbt` 格式**：第 1–4 byte 推測是 (x, y, 寬, 高)，之後是 RLE 列資料。解碼規則本輪未查（`sub_878AE0`／`sub_66E050` 型別 3）。
- **250/21（ICONS2）與 `S_Paralysis` 的格式**：TW16 的讀法與 3.81 伺服器的 serializer 不同。伺服器實際送出的 byte 在客戶端會被解成什麼，要用封包紀錄核對。
- **選項旗標 `0x800`（依剩餘時間排序）的預設值**，以及字串 930／931／2854 的實際文字（`sub_4EAFF0` 字串表）。
- **畫面確認**：實際位置與排序要在真機截圖核對：第一格 (763, 5)，往左每格 35 px。

## 5. 小地圖與地圖（`MapSystem-c.xml`、`uistatus86-c.xml`）

### 已驗證：原版有小地圖

| 視窗 | 位置 | 圖 | 尺寸 | 說明 |
|---|---|---|---|---|
| 底列小地圖 `MINI_MAP_WINDOW` | (485, 475) | 框 3275、底 3274 | 158×125；地圖區 135×101 | 解析在 `sub_A9AD80`，字串 `MINI_MAP_BASE` 位於 VA `0x119C818`／FO `0xD9B418` |
| `Field_Map`（野外地圖小窗） | (5, 250)，透明度 20 | 框 2816、標題 2815 | 161×121 | 有關閉鈕 2801 (146, 6)、縮放鈕 2803 (129, 6) |
| `Dungern_Map`（地監） | (5, 250)，透明度 7 | 標題 2888 | — | — |
| `World_Map`／`Village_Map` | (45, 30)，透明度 10 | 框 2828 | 550×316 | 世界與村莊地圖 |

`MapSystem-c.xml` 的共通元件與資料：
- 上下左右捲動鈕。
- 角色位置圖示：surf 2818–2825，**8 方向**，9×9。
- 隊友點：surf 2826，6×6。
- 透明度捲軸。
- 地圖、子清單、物件清單。
- 153 筆 `Map` 資料：每筆用 `pngNum`（10000 起的 png 地圖圖）、`ratio`、`baseLoc`、`basePixel` 把遊戲座標換成圖上像素。
- 591 筆 NPC 標記：`icon`、`loc`、`color`。
- 44 筆連結。

開啟方式：
- 台灣版巨集框上的 Map 按鈕（action 13）
- 快捷鍵 **Ctrl＋M**（V5 已查證，見第 2 節「底列行為（V5）」(b)）

兩者都是開關 `Field_Map`／`Dungern_Map` 窗，不是底列的 `MINI_MAP_WINDOW`。

### 已驗證：`MINI_MAP_WINDOW` 地圖區的內容（V5，Issue #460）

`MINI_MAP_BASE` 字串（VA `0x119C818`／FO `0xD9B418`）只有一個 xref：解析者 `sub_A9AD80` 的 `0xA9EB6D`（FO `0x69DF6D`）。它把元素存進 `dword_153F440+468`，`MINI_MAP_FRAME` 存進 `+472`。這兩個欄位的讀取者只有：
- 底列小地圖 `StatusMiniMap` 的繪製函式（TW16 `sub_ED0610`／FO `0xACFA10`；TW13 `sub_78D8E0`／FO `0x38D8E0`）
- 它呼叫的兩個地圖繪製函式

依子視窗 `+0x18` 的模式值分成三種畫法：

| 模式 | 畫法 | 出處 |
|---|---|---|
| 2 | 呼叫**野外地圖窗 `Field_Map` 用的同一支繪製函式**：TW16 `sub_A695E0`（FO `0x6689E0`），TW13 `sub_5D6AE0`。畫在 `MINI_MAP_BASE` 的範圍內（x＋0、y＋5） | TW16 `sub_A695E0` 的呼叫者有 `FieldMapWindow` vtable 的 `sub_A6A090`、`MiniMapUI` 的 `sub_707210` 與底列 `sub_ED0610` |
| 1 | 呼叫地監地圖窗用的繪製函式：TW16 `sub_A6B110`（FO `0x66A510`），TW13 `sub_5D3EE0` | 同上，呼叫者含 `sub_ED0610` |
| 其他（建構時為 5；地圖 id 落在特定區間時為 4） | 只畫底圖 3274 與外框 3275，**不畫地圖** | `sub_ED0610` 尾段 |

**`sub_A695E0` 的畫法：**
- 以玩家座標經 `sub_A67070`（FO `0x666470`）換成圖上像素。換算公式用地圖項目的浮點倍率（`this[6]`）與 `+24` 的基準點，對應 `MapSystem` 的 `ratio`／`baseLoc`／`basePixel`。
- 用 `sub_9DCB20`（FO `0x5DBF20`）貼地圖影像。
- 依視窗大小裁切。
- 在影像上依序疊畫：
  - 隊友點與名字
  - NPC 標記
  - 8 方向角色箭頭：surf 基底加 `dword_153CE60+8` 的朝向

**模式值的來源：** 由 TW13 `sub_78DA00`（FO `0x38DA00`）依目前地圖 id 決定：
- 野外地圖窗已在顯示中 → 2
- 地監地圖窗已在顯示中 → 1
- 地圖 id 落在 `dword_C2F864+100` 的區間表內 → 4（不畫地圖）
- `MapSystem` 的 `Map` 清單有這張圖 → 先把圖設給野外地圖窗，再設為 2
- 都不符合 → 交給地監檢視器，設為 1

這個函式在切到小地圖模式（`sub_7923E0` 模式 0）與換地圖（`sub_4EA410`）時呼叫。

**結論：**
- **已驗證：** 底列小地圖**不是即時地形縮圖**。它沒有讀地圖 tile，而是重用 `Field_Map`／`Dungern_Map` 的影像繪製，用 `MapSystem` 的換算把玩家放到影像上。
- **已驗證：** 不在清單內的地圖不畫內容，只留空框。
- **推測（高可信）：** 影像就是 `MapSystem` 的 png 地圖。依據：
  - png 檔名在 `sub_E557D0`（FO `0xA54BD0`，vtable 方法）組出：`%d.png`／`ge%d.png`／`cs%d.png`／`ms%d.png`
  - 讀不到時記錄 `"%d.png cann't find"`（`sub_E53FF0`）
  - 本輪沒有追到「`Field_Map` 的影像成員就是這支載入函式的產物」這一步的寫入點

### 待驗證

- png 影像成員的寫入點（見上面的「推測」）。
- 小地圖模式 2 的取景中心與縮放：`sub_A695E0` 用 (80, 60) 做半寬／半高參數，換算到 135×101 地圖區的實際取景未展開。

## 6. 隊伍、書籤、公告板、信件

### 已驗證：隊伍（`uiextra86-c.xml` 的 `PARTY_INTERFACE`）

- **隊伍底板：** 3204，尺寸 135×107，位於 (0, 493)。也就是**蓋在左狀態框上**。
- **分頁鈕：** 位於 y=481：角色狀態 3205 (11)、隊伍狀態 3208 (36)、寵物 3298 (65)。
- **隊員 HP 量表：** 3211 尺寸 60×10，紅色版 3212 尺寸 58×8。`PARTY_PET_RT_WINDOW` 另有灰色版 3225 尺寸 58×8。
- **按鈕：** 離開／踢除／委任，位於 y=574，x=70／90／109。
- **選取框：** 3219，尺寸 64×27。
- **右側隊伍／寵物窗 `PARTY_PET_RT_WINDOW`：** 3220 尺寸 158×125，位於 (485, 475)，也就是**與小地圖同一格**。

### 已驗證：書籤（`BookmarkUI.xml`，`BookmarkWindowUI Toggle="true"`）

- **視窗框：** `FrameSurf` 4313，尺寸 263×420，可拖曳。底圖 4312 尺寸 251×407，位於 (6, 9)。
- **關閉鈕：** (249, 15)。
- **快速移動清單：** (23, 62)，120×75。
- **記憶座標清單：** (22, 162)，200×204，捲軸位於 x=218。
- **區域篩選下拉：** (129, 141)，105×134。
- **按鈕：** 隨機傳送 (129, 42)、排序 (182, 42)。
- **13 色調色盤：** (19, 368)，每格間距 16。
- **確認／取消：** (67, 395)／(134, 395)。
- **舊版書籤窗：** `uiextra86-c.xml` 的 `BOOK_MARK_WINDOW` 保留舊版，底 2252 尺寸 181×250，框 2253 尺寸 193×262。

### 待驗證：公告板

XML 中沒有公告板專用視窗。通用的 `HYPER_TEXT_WINDOW`（底 1968 尺寸 227×304，框 1969 尺寸 239×317）是否就是公告板，本輪未確認。

### 待驗證：信件

BIN 載入 `RenewalMailUI.xml`（字串 VA `0x119CC48`／FO `0xD9B848`），但**本機 `Tile.pak` 沒有這個檔**，只有 `MailExcludeUI.xml`。因此原版信件版面在本機資源中**無法取得**。缺檔時客戶端會怎麼處理，本輪未查。

## 7. 與目前引擎的差異

引擎狀態依 `engine/source/` 現況（branch `task/429`，基於 main `9e6d7e8f`）。差異只列出，**沒有修改產品程式**。

| # | 項目 | 原版怎麼做 | 引擎怎麼做 | 是否一致 |
|---|---|---|---|---|
| 1 | 畫面尺寸 | 800×600 固定，底列 y=470–600 | 視窗 1180×800 可縮放（`Program.cs:95`），HUD 198 px（`PlayerHudModel.cs:42`），右側 365 px 網頁面板 | 不一致 |
| 2 | UI 美術 | 全部用 `Sprite*.pak` 的 `.img`（surf 編號見上） | WinForms／GDI＋純色合成（`PlayerHudView.cs:16`），沒有載入原版 UI 圖 | 不一致 |
| 3 | 左狀態框 | 等級／EXP、AC、MR、陣營、SP、負重、飽食、金幣 | HP／MP 條＋名稱等級行（`PlayerHudView.cs:26-30`） | 不一致 |
| 4 | HP／MP | 浮動窗 `HpMpWindow` 預設在 (135, 465)：橫排、長，外框 2355（378×48），HP 2365（141×20）位於 (9, 11)，MP 2358（136×20）位於 (183, 11)。另有直排／三種長度／thick 變體與時鐘；頭上條為 930／931／932，44×5（V1，第 2 節） | 底部 HUD 兩條：HP #902D31、MP #2A5B93 | 不一致 |
| 5 | 快捷列（T138） | 右巨集框 8 格 **F5–F12**，3 個分頁；設定存在 protobuf `Lineage.ClientConfig` | 4 格 **F1–F4**（`PlayerHudView.cs:47,83`）；26 byte「TWCC」設定（`CharacterConfig.cs:14-31`） | 不一致 |
| 6 | 選單按鈕 | 巨集框 y=91 一排 8 個：Map／Mail／Status／Spell／Item／Pledge／Option／Quit | 底部一列文字按鈕：背包／裝備／技能／聊天／服務／診斷 F9／登出／小地圖 F5／信件／隊伍（`PlayerHudView.cs:85,111`） | 不一致 |
| 7 | 聊天窗（T106） | 362×240，位於 (129, 600−h)，以 36 px 伸縮；左側有 8 個頻道分頁 | HUD 抽屜內的 RichTextBox，高 236（`PlayerHudChat.cs:4-18`） | 不一致 |
| 8 | 聊天行格式 | `name: msg`、`[name]`、`{name}`、`{{name}}`、`(name)`、`-> (target)` | `[一般] text`、`[喊話]`、`[全體]`、`[密語]`、`[血盟]`、`[隊伍]`；密語為 `{from} -> 我：{msg}`（`PlayerChat.cs:45-48,62`） | 不一致 |
| 9 | 聊天顏色 | 調色盤：一般白、喊話 #FFE6CE、全體 #ADEFEF、收密語 #FFCECE、送密語 #FFF7B5、血盟 #C5EFB5、隊伍 #FFB500 | .NET 具名色：一般 White、喊話 Orange、全體 LightSkyBlue、密語 Violet、血盟 LightGreen、隊伍 Yellow（`PlayerHudChat.cs:39`） | 一般一致，其餘不一致 |
| 10 | 頻道種類與前綴 | 類型 0／1／2／3／4／11／12／13／14／15／17／18／25／29；前綴 `" ! & @ # $` | 6 種，沒有交易（12）、同盟（15）、盟主（13）、聊天隊伍（14）、血盟公告（17）；不處理 `$`（`PlayerChat.cs:3,15-25`） | 部分一致 |
| 11 | 狀態圖示（T108） | 32×32 的 `<icon>.tbt`。依 `effectlist2.xml` 取圖，從右上角 (763, 5) 往左排、每列 22 格；排序依 priority，其次依到達順序；剩餘時間只在 tooltip 顯示（第 4 節，V3） | 合成菱形／三角形＋文字，依 skill id 排序（`StatusIcons.cs:15,41-44`、`MagicStatus.cs:19`） | 不一致 |
| 12 | 小地圖（T150） | 底列 (485, 475) 158×125 格；另有 `Field_Map` 小窗 (5, 250) 161×121，用 png 地圖、8 方向箭頭、隊友點；用 Map 按鈕開啟 | 地圖快取縮圖，右上 272×218，自畫白／青／金色點，F5 開關（`MinimapUi.cs:6-9,50-52,74-76`） | 不一致；F5 與原版巨集鍵衝突 |
| 13 | 隊伍 HP 條（T125） | 隊伍底板 135×107 蓋在左狀態框；量表圖 60×10／58×8（紅、灰）；右側 RT 窗 158×125 | 抽屜內清單，Windows 標準 ProgressBar 120×22（`PartyUi.cs:5-10,36-49`） | 不一致 |
| 14 | 書籤（T124） | `BookmarkUI.xml` 263×420 視窗（版面見第 6 節） | 尚未合併（TASKS 狀態為 review），只有傳送卷軸的書籤 ID 欄位（`InventoryUi.cs:143-145`） | 不一致（未實作） |
| 15 | 公告板（T130） | 未找到專用 XML。#483 已查明：原版是寫死的專用舊式視窗 `BoardListWindow`／`BoardReadWindow`／`BoardWriteWindow`，**不是** `HYPER_TEXT_WINDOW`（見 [`windows.md`](windows.md) 第 2 節） | 獨立 WinForms 視窗 740×680（`BoardUi.cs:11`） | ⚠️（視窗不一致） |
| 16 | 信件（T131） | 版面寫在 `RenewalMailUI.xml`，但本機資源缺檔 | 對話框 780×640，含收件匣／寫信分頁（`MailUi.cs:5,27`） | 無法判定 |
| 17 | 紅字訊息（T152） | 位置待驗證 | 紅字黑底，水平置中，位於視窗高度 1/3 處，顯示 5 秒（`DisplayNotices.cs:93,99,10`） | 無法判定 |

合計：不一致 12 項（#1–8、#11–14），部分一致 2 項（#9、#10），無法判定 3 項（#15、#16、#17）。#4 原為無法判定，V1（#456）查證後改為不一致。

## 給後續任務（T164）的提示

- 本文件的 XML 數值都可以用 `ui_xml_decrypt.py` 重新取得。產品若要還原原版版面，可以在執行期從 `Tile.pak` 解密讀取，不必把 XML 或圖檔複製進專案。
- 本輪仍缺的四項：
  - ~~HP／MP 浮動窗座標~~：V1（#456）已補，見第 2 節
  - 紅字訊息位置
  - ~~舊封包的狀態圖示對照~~：V3（#458）已完成，見第 4 節。來源是 `effectlist2.xml`＋`.tbt`，不是 `SpellIcon.idx`
  - 前綴字元→頻道對照：在 `sub_6C6C60` 之後的鏈中找

## 可重跑腳本

都在 [`rea-scripts/`](rea-scripts/)：

| 腳本 | 用途 |
|---|---|
| `ui_xml_decrypt.py` | 解出 `Tile.pak` 的 65 個 UI XML，輸出到 `scratch/` |
| `ui_img_dims.py` | 讀 `.img` 尺寸 |
| `ui_img_render.py` | 把 `.img` 轉成 PNG，只能輸出到 `scratch/`，不可進 git |
| `ui_ida_query.py`、`run_ui_query.ps1` | IDA 唯讀查詢（字串→xref→反編譯、立即數掃描）。使用時複製到 `scratch/rea/scripts/` 並改名為 `ida_query.py`／`run_query.ps1`；IDB 放 `scratch/rea/ida/tw16.idb`。預設 subst 磁碟為 `K:`，該磁碟已被占用時會直接退出，不會卸載別人的對應 |
| `ui_queries.json` | 本輪使用的全部查詢（q1–q20） |
| `hpmp_ida_query.py`、`hpmp_x_*.py`、`hpmp_queries.json` | V1（#456）的 IDA 查詢。這版 `ida_query.py` 多了 `exec` 鍵，可執行 `hpmp_x_*.py`：RTTI→vtable、區段批次反編譯、反組譯、全域 xref。用法同上：複製到 `scratch/rea/scripts/`，並把 `exec` 檔名的 `hpmp_` 前綴去掉後放在 `scratch/rea/`。執行器是 `run_ui_query.ps1`，要另外設 `REA_ROOT` 為 subst 磁碟根目錄 |
| `hpmp_pb_descriptor.py` | 從 BIN 解出 `ClientConfig.proto` descriptor（欄位編號、型別、預設值） |
| `hpmp_bin_values.py` | 讀 BIN 中的 shape／status／顯示旗標初值、警示門檻、時鐘表、調色盤 |
| `hpmp_img_all.py` | 列出指定編號在所有 `*.idx` 中的檔案（.img／.png／.tbt），以及 `.img` 尺寸 |
| `buff_icon_table.py` | V3：從解密後的 `effectlist2.xml` 與 `Sprite*.pak` 的 `.tbt` 標頭產生第 4 節的 effect→圖示表（只輸出數字，`-o` 寫到 `scratch/`） |
| `buff_handlers.py`、`buff_casegrep.py` | V3：TW16 opcode → 分派 case → 處理函式；在 Hex-Rays 輸出中列出每個 `sub_F05A90`／`sub_F059F0` 呼叫與所在的 case |
| `buff_icon_queries.json` | V3 使用的 IDA 查詢（q1–q9 合併）；用法同 `ui_queries.json`，`ida_query.py` 需支援 `ranges` 與 UTF-8 BOM（本輪已在 `ui_ida_query.py` 加上） |
| `v5/` | V5（#460）：TW16 與 TW13 的 IDA 唯讀查詢（`ida_exec.py` 執行器、`run_exec.ps1` 可用 `-Db` 指定 IDB），用法見 [`v5/README.md`](rea-scripts/v5/README.md) |
