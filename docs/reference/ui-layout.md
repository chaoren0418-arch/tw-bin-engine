# 原版 UI 版面與圖示查證（T161，Issue #429）

查證日期：2026-11-07（Asia/Taipei）。全程唯讀，沒有啟動遊戲、伺服器或 DB。方法照 #417：用 IDA 7.7＋Hex-Rays 開 P01 IDB 在 `scratch/` 的副本，靜態分析未加殼的 `TW1608262200.bin`。另外讀取本機客戶端 `D:\lineage開發\0-客戶端` 的 `Tile.pak`／`Sprite*.pak`，只讀出到 `scratch/`，沒有任何遊戲資源進 git。

涵蓋功能：T106、T108、T124、T125、T130、T131、T138、T150、T152。

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

   開啟方式是台灣版巨集框上的 **Map 按鈕**（action 13）。快捷鍵本輪未取得。
4. **聊天格式與顏色寫死在 BIN 中。** 共 16 個 `LineageChat::ChatText*Printer` 類別，由聊天類型 0／1／2／3／4／11／12／13／14／15／17／18／25／29 的工廠建立。顏色取自一張靜態 RGB555 調色盤，前 77 筆與 TW13 逐 byte 相同。
5. **與引擎比對 17 項：11 項不一致、2 項部分一致、4 項無法判定**，見最後一節。最大的三項：
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
| `FLOAT_WINDOW/HPMP_WINDOW` | XML 註解寫「in Binary File」 | — | — | **HP／MP 不在底列 XML 中** |

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

- **巨集格距：** 從 3176 的渲染圖目測，8 格排成 2 列 × 4 欄，格距約 33×34 px。XML 只給第一格位置，其餘格的座標在程式碼裡，本輪未取。
- **HP／MP 浮動窗：** 位置寫死在程式碼中。BIN 有 protobuf 設定 `Lineage.ClientConfig.FloatingHPMP`，以及 `showCharacterHPMP`、`HPMPBarThick`、`HPMPBarRecovery`、`HPMPBarPercent` 等選項，解析者在 `sub_919F30`（`0x919F30`）。由此推測有「浮動 HP/MP 窗」和「頭上 HP/MP 條」兩種顯示。

### 待驗證

- HP／MP 條的座標、尺寸、顏色。
- 紅字訊息（畫面中央系統訊息）的位置。

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

### 已驗證

- 2016 客戶端有 protobuf 訊息 `LIN.SC_SPELL_BUFF_NOTI`，descriptor 位於 VA 約 `0x11B5D00`。欄位有 `noti_type`（NEW／RESTAT／END）、`spell_id`、`duration`、`duration_show_type`、`on_icon_id`、`off_icon_id`、`icon_priority`、`tooltip_str_id`、`new_str_id`、`end_str_id`、`is_good`、`overlap_buff_icon`、`main_tooltip_str_id`、`buff_icon_priority`。
- 另有 `SC_BUFF_ICON_ON_OFF_NOTI`（`iconid`、`on`）。
- 結論：新版的圖示編號、排序優先序和持續時間顯示方式，**都由伺服器逐筆指定**。
- 舊封包另有 `STATUS_EFFECT_*` 處理，除錯字串在 VA `0x1183E1C`–`0x1184C34`，例如料理、龍之血、魔眼。

### 待驗證

- 舊封包（3.81／d287 伺服器實際送出的 S_SKILLHASTE、S_SKILLBRAVE 等）對應的圖示 surf 編號、畫面位置、排列順序。本輪未定位繪製函式。
- `Sprite.pak` 內有 `SpellIcon.idx`，推測是技能→圖示對照，未解析。

**注意：** d287 伺服器不會送 `SC_SPELL_BUFF_NOTI`，所以「圖示由伺服器指定」這條路在 P24Lab 不會被觸發。

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

開啟方式：台灣版巨集框上的 Map 按鈕（action 13）。

### 待驗證

- 快捷鍵。
- `MINI_MAP_WINDOW` 地圖區畫的內容：是即時縮圖，還是 png 地圖。

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
| 4 | HP／MP | 浮動窗，位置寫死在程式碼中（座標待驗證） | 底部 HUD 兩條：HP #902D31、MP #2A5B93 | 無法判定 |
| 5 | 快捷列（T138） | 右巨集框 8 格 **F5–F12**，3 個分頁；設定存在 protobuf `Lineage.ClientConfig` | 4 格 **F1–F4**（`PlayerHudView.cs:47,83`）；26 byte「TWCC」設定（`CharacterConfig.cs:14-31`） | 不一致 |
| 6 | 選單按鈕 | 巨集框 y=91 一排 8 個：Map／Mail／Status／Spell／Item／Pledge／Option／Quit | 底部一列文字按鈕：背包／裝備／技能／聊天／服務／診斷 F9／登出／小地圖 F5／信件／隊伍（`PlayerHudView.cs:85,111`） | 不一致 |
| 7 | 聊天窗（T106） | 362×240，位於 (129, 600−h)，以 36 px 伸縮；左側有 8 個頻道分頁 | HUD 抽屜內的 RichTextBox，高 236（`PlayerHudChat.cs:4-18`） | 不一致 |
| 8 | 聊天行格式 | `name: msg`、`[name]`、`{name}`、`{{name}}`、`(name)`、`-> (target)` | `[一般] text`、`[喊話]`、`[全體]`、`[密語]`、`[血盟]`、`[隊伍]`；密語為 `{from} -> 我：{msg}`（`PlayerChat.cs:45-48,62`） | 不一致 |
| 9 | 聊天顏色 | 調色盤：一般白、喊話 #FFE6CE、全體 #ADEFEF、收密語 #FFCECE、送密語 #FFF7B5、血盟 #C5EFB5、隊伍 #FFB500 | .NET 具名色：一般 White、喊話 Orange、全體 LightSkyBlue、密語 Violet、血盟 LightGreen、隊伍 Yellow（`PlayerHudChat.cs:39`） | 一般一致，其餘不一致 |
| 10 | 頻道種類與前綴 | 類型 0／1／2／3／4／11／12／13／14／15／17／18／25／29；前綴 `" ! & @ # $` | 6 種，沒有交易（12）、同盟（15）、盟主（13）、聊天隊伍（14）、血盟公告（17）；不處理 `$`（`PlayerChat.cs:3,15-25`） | 部分一致 |
| 11 | 狀態圖示（T108） | 原版圖示（新版由伺服器指定 icon id 與優先序；舊封包對應待驗證） | 合成菱形／三角形＋文字，依 skill id 排序（`StatusIcons.cs:15,41-44`、`MagicStatus.cs:19`） | 不一致（圖示來源待驗證） |
| 12 | 小地圖（T150） | 底列 (485, 475) 158×125 格；另有 `Field_Map` 小窗 (5, 250) 161×121，用 png 地圖、8 方向箭頭、隊友點；用 Map 按鈕開啟 | 地圖快取縮圖，右上 272×218，自畫白／青／金色點，F5 開關（`MinimapUi.cs:6-9,50-52,74-76`） | 不一致；F5 與原版巨集鍵衝突 |
| 13 | 隊伍 HP 條（T125） | 隊伍底板 135×107 蓋在左狀態框；量表圖 60×10／58×8（紅、灰）；右側 RT 窗 158×125 | 抽屜內清單，Windows 標準 ProgressBar 120×22（`PartyUi.cs:5-10,36-49`） | 不一致 |
| 14 | 書籤（T124） | `BookmarkUI.xml` 263×420 視窗（版面見第 6 節） | 尚未合併（TASKS 狀態為 review），只有傳送卷軸的書籤 ID 欄位（`InventoryUi.cs:143-145`） | 不一致（未實作） |
| 15 | 公告板（T130） | 未找到專用 XML；可能用 `HYPER_TEXT_WINDOW` 239×317 | 獨立 WinForms 視窗 740×680（`BoardUi.cs:11`） | 無法判定 |
| 16 | 信件（T131） | 版面寫在 `RenewalMailUI.xml`，但本機資源缺檔 | 對話框 780×640，含收件匣／寫信分頁（`MailUi.cs:5,27`） | 無法判定 |
| 17 | 紅字訊息（T152） | 位置待驗證 | 紅字黑底，水平置中，位於視窗高度 1/3 處，顯示 5 秒（`DisplayNotices.cs:93,99,10`） | 無法判定 |

合計：不一致 11 項（#1–3、#5–8、#11–14），部分一致 2 項（#9、#10），無法判定 4 項（#4、#15、#16、#17）。

## 給後續任務（T164）的提示

- 本文件的 XML 數值都可以用 `ui_xml_decrypt.py` 重新取得。產品若要還原原版版面，可以在執行期從 `Tile.pak` 解密讀取，不必把 XML 或圖檔複製進專案。
- 本輪仍缺的四項：
  - HP／MP 浮動窗座標：從 `sub_919F30` 與 `FloatingHPMP` 往繪製端追
  - 紅字訊息位置
  - 舊封包的狀態圖示對照：可試 `SpellIcon.idx`
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
