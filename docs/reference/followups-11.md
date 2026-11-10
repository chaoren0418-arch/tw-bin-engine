# 查證補遺第 11 輪（FU11）：HyperText 顏色位元組順序與連結色（T381／#886）

依據：[`followups-9.md`](followups-9.md) §10（Q7-10）、[`gap-matrix.md`](gap-matrix.md) 15.11 的 M34、R9-F（#850，已合併）的 `engine/source/NativeUi/HyperTextLayout.cs`／`HyperTextPanel.cs`。

## 0. 來源與方法

- 分析對象：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`。
- IDA 7.7（idat 批次、唯讀查詢）＋P01 IDB `P01-分析基準/ida/03-ascii-isolated/TW1608262200.idb` 複製到 `scratch/t886/ida/tw16.idb`（SHA-256 `3A0BF206…BC17E82F`）。subst 前先確認沒有占用中的磁碟（`subst` 無輸出），只用 `T:`，每次執行後解除。
- File Offset 換算（PE 節區表）：`.text` FO＝VA − `0x400C00`；`.rdata` FO＝VA − `0x401400`；`.data` FO＝VA − `0x401800`。
- 頁面資料：PakViewer CLI（`pakviewer-cli pak extract`）從 `D:\lineage開發\0-客戶端\Text.idx` 解出 `helpcom-c.html`（2,400 B）、`helpcon-c.html`（2,023 B）、`helpcom-h.html` 到 `scratch/t886/pak/`（不進 git）；另以唯讀腳本統計 `releases/r29/assets/dialogs/templates.json`（8,896 頁）的 `fg` 用法。本輪沒有宣稱任何資源「沒有」。
- **更正 FU9／任務描述**：`sub_BBCB00`（VA `0xBBCB00`／FO `0x7BBF00`）是 S39 封包讀取（讀頁名與資料字串、空頁名關窗），不碰顏色。顏色在 HTML 解析器 `sub_8676F0`（FO `0x466AF0`）與繪字函式 `sub_866070`（FO `0x465470`）。

## 1. 結論（給 R9-F 的規則表）

| # | 規則 | 依據 | 狀態 |
|---|---|---|---|
| 1 | `fg`／`bg` 字串由左到右當十六進位累加成一個整數（`"98FB98"`→`0x98FB98`），**不換位元組** | `sub_864530`（VA `0x864530`／FO `0x463930`），`sub_8711F0(...,1)` 呼叫 | 已驗證 |
| 2 | 這個整數**原封不動當 GDI `COLORREF`（`0x00BBGGRR`）**：`SetTextColor(hdc, attr+0x0C)`、`CreateSolidBrush(attr+0x10)`。所以 `fg=RRGGBB` 寫法實際顯示成 **R＝最後兩位、B＝最前兩位**（`00b4ff`→RGB(FF,B4,00) 橘） | `sub_866070`：`0x8667B2`／FO `0x465BB2` 讀 `+0Ch`、`0x8667BA` `SetTextColor`；`sub_86E960`（FO `0x46DD60`）`CreateSolidBrush(this[4])`＋`FillRect` | 已驗證 |
| 3 | 不是 RGB565／ARGB：HyperText 文字全程走 GDI（`SetBkMode(hdc,1)`、`SelectObject` 字型、`ExtTextOutA`） | `sub_866070` | 已驗證 |
| 4 | 預設字色（沒有 `fg`）＝`CTextAttr` 建構子的 **`0x00EEEDEE`**→RGB(EE,ED,EE)；預設 `bg`＝`-1`（不畫底） | `sub_862970`（FO `0x461D70`），`0x8629C6`／FO `0x461DC6` `mov [eax+0Ch], 0xEEEDEE`、`0x8629D0` `+10h = -1`；`<body>` 只算層數不改色 | 已驗證 |
| 5 | 每個 `<a>`（開始標籤）在屬性讀完、push 樣式之後把字色設成 **`0x00B0FFFF`**＝COLORREF→**RGB(FF,FF,B0) 淡黃** | `0x867D3B`／FO `0x46713B` `mov [ebp+var_2980], 0xB0FFFF`（`var_2980`＝目前樣式 `+0Ch`） | 已驗證 |
| 6 | `<a>` 裡面再開的 `<font fg>` 蓋過連結色；`</a>` pop 回 `<a>` 之前的樣式（R9-F 已照做） | push `sub_873990`（FO `0x472D90`）／pop `sub_8738D0`（FO `0x472CD0`），樣式複製 `sub_863F30` | 已驗證 |
| 7 | **滑鼠移上去的色＝`0x0032F00A`**＝COLORREF→**RGB(0A,F0,32) 亮綠**：繪字時若「滑鼠下的連結編號」（doc `+0x88`）≠ −1 且等於這段字的連結編號（attr `+0x18`）就用它，否則用 attr 的 fg | `0x866787`–`0x8667A4`／FO `0x465B87`–`0x465BA4`；doc`+0x88` 由命中測試 `sub_871260`（FO `0x470660`）每次先設 −1（`0x8712B8`）、命中文字連結才寫入（`0x871509`）；`sub_871D60`（FO `0x471160`）對 **WM_MOUSEMOVE（512）**、513 等每個滑鼠訊息都先做命中測試 | 已驗證 |
| 8 | hover 只對**文字連結**；圖片連結（`<img link/action>`）命中時直接 return，不寫 `+0x88`，所以圖不變色 | `sub_871260` 第一段迴圈 | 已驗證 |
| 9 | **沒有「已點過」的色**：`CTextAttr` 欄位 `+4 face／+8 Size／+0C fg／+10 bg／+14 b／+15 i／+16 u／+17 實心框旗標／+18 連結編號` 都已對上，繪字只有「hover 色／attr fg／下列特殊模式」三條路 | `sub_862970`、`sub_863F30`、`sub_866070` | 已驗證 |
| 10 | `bg` 範圍：`FillRect` 的矩形＝**這段樣式相同的字（run）的起點 x 到下一段起點 x** × **整列的上下界**（呼叫端傳入的列矩形 `a4[1]..a4[3]`），先畫底再畫字。不是整行寬、也不只是字形高 | `sub_866070` 第一個迴圈 `SetRect(&rc, start+x0, a4[1], next+x0, a4[3])`→`0x866310` `sub_86E960` | 已驗證（「整列」＝列矩形的高；列高本身由排版決定） |
| 11 | `+17` 旗標為真時改畫 `Rectangle`（白筆框＋bg 刷），只用字高。HyperText 解析器**從不設** `+17`（`sub_8676F0` 沒有寫 `var_2975`），HTML 頁不會走這條 | `0x86624B` `CreateSolidBrush`、`0x86628E` `Rectangle` | 已驗證 |
| 12 | 底線色固定 **`0x00466E6E`**→RGB(6E,6E,46)，不是字色；畫在基線下 2 px，x 最多到左邊界＋198；`R2_COPYPEN` | `0x866B16`／FO `0x465F16`；`SetDCPenColor`、`SetROP2(13)` | 已驗證 |
| 13 | 底線旗標 `+16` 由 `<u>` 設（`0x8678F5`），**也由帶 `link`／`action`／`url` 屬性的 `<a>` 設**（`0x867B72`，jumptable cases 28／29／43）；`</a>` 時清掉（`0x86799E`） | `sub_8676F0` | 已驗證 |
| 14 | 特殊模式：HyperText 狀態 `dword_13CD834+662`＝1 時，每段字跟 `dword_153CF00` 名單比對，命中用 `0xFF00`（綠）並接字串 1061，否則 `0xBEBEBE`（灰），改用 `ExtTextOutA` 不帶字距 | `0x8668F1`、`0x866956` | 已驗證（哪一頁、名單是什麼待查，與 M34 無關） |
| 15 | 解析細節：只收 `0-9a-fA-F`；`#`、`x`（十六進位模式）、其他字元→整個值無效、不改顏色；**空字串＝有效、值 0（黑）**；超過 8 位會 32 位元溢位、不截低 24 位 | `sub_864530` | 已驗證 |

### 1.1 頁面作者的寫法佐證（推測層級，但與 §1 一致）

TW 的 `helpcom-c.html`（Text.pak，Big5）把顏色名稱寫在字上，只有 COLORREF 解讀對得上：

| `fg` | 作者寫的 | COLORREF 解讀 | 若當 RGB |
|---|---|---|---|
| `00b4ff` | 橘色 | RGB(FF,B4,00) 橘 ✔ | 天藍 ✘ |
| `afffff` | 黃色字體 | RGB(FF,FF,AF) 淡黃 ✔ | 淡青 ✘ |
| `ffffaf` | 亮藍色字體 | RGB(AF,FF,FF) 淡青藍 ✔ | 淡黃 ✘ |
| `e7ebbd` | 淡藍色字體 | RGB(BD,EB,E7) 淡青 ✔ | 淡黃綠 ✘ |
| `b5f1ff` | 亮黃色 | RGB(FF,F1,B5) 淡黃 ✔ | 淡藍 ✘ |
| `cecafc` | 粉紅色 | RGB(FC,CA,CE) 粉 ✔ | 淡紫藍 |
| `6ea0ba` | 褐色字體 | RGB(BA,A0,6E) 褐 ✔ | 灰青 ✘ |

`helpcon-c.html` 也寫 `fg=ffffaf bg=ffffff`「藍色」。templates.json 裡 `y_q_c15_1` 的 `0000ff` 用在「紅色斗篷」。R9-F 原本用來推 RGB 的 `98FB98`、`A2A2A2` 是對稱值，兩種順序看起來一樣，不能區分。

## 2. 對照 main 上的 R9-F（`HyperTextLayout.cs`／`HyperTextPanel.cs`）

| # | R9-F 現況 | 原版（§1） | 建議 |
|---|---|---|---|
| D1 | `HyperTextColors.Order` 預設 `Rgb`（`HyperTextLayout.cs:40`），連結＝B0FFFF 淡青 | COLORREF＝`Bgr`；連結＝FFFFB0 淡黃 | 預設改 `Bgr`，`:16–18` 註解改「已驗證（FU11）」；`HyperTextLayoutTests` 的順序測試跟著改 |
| D2 | 沒有 fg 時用 `ChatPalette` White＝FFFFFF（`HyperTextPanel.cs:101`） | `0x00EEEDEE`→RGB(EE,ED,EE) | 富文字排法改用 EEEDEE（舊的一列一個排法可不動） |
| D3 | 沒有 hover 色 | 文字連結在滑鼠下＝RGB(0A,F0,32)；圖片連結不變色 | 面板記錄滑鼠下的連結 id，`PieceColor` 先判斷 hover |
| D4 | 停用的連結畫灰（`DisabledColor`） | 原版繪字沒有停用分支 | 屬引擎自訂，保留但註明「引擎自訂」 |
| D5 | 底線只來自 `<u>`，顏色＝字色（`HyperTextPanel.cs:384`） | 底線色固定 RGB(6E,6E,46)；帶 link／action／url 的 `<a>` 也有底線；x 到左界＋198 為止 | 改底線色與連結底線 |
| D6 | bg 畫 piece 寬 × `LineHeight`（`:379`） | run 起點到下一 run 起點 × 列矩形高 | 大致相同；同列有圖時要用整列高 |
| D7 | `Parse` 空字串→null（不改色）、先 Trim、取低 24 位（`HyperTextLayout.cs:47–51`） | 空字串→0（黑）；不截位 | 空字串改回 0；>6 位屬邊角，維持截位並註明 |
| D8 | `<a>` 內再開 `<font fg>` 蓋過連結色、`</a>` 回復 | 同 | 一致 |
| D9 | 沒有「已點過」色 | 原版也沒有 | 一致 |

以上都沒有改產品程式（本任務禁止），交給 R9-F 後續或另開 code 任務。

## 3. 留給 M34（真機）

靜態已確定：位元組順序（COLORREF）、連結色、hover 色、預設色、bg 範圍、底線色。真機只需確認 GDI 畫到的 DC 最後貼到畫面時沒有再換通道（例如 DIB→16 位元表面），以及 hover 實際觀感。

- **截 `helpcom`（`helpcom-c.html`）**：「橘色」那行應為橘、「亮藍色字體」應為淡青藍、「黃色字體」應為淡黃。三者都對＝COLORREF 結案。
- **截 `helpcon`（`helpcon-c.html`）**：`fg=ffffaf bg=ffffff` 的「藍色」：淡青藍字＋白底，白底寬度應到下一段字起點、高度占整列。
- **任一 NPC 對話頁的 `<a action=…>`**：連結應為淡黃 RGB(FF,FF,B0)、帶 RGB(6E,6E,46) 底線；滑鼠移上去變亮綠 RGB(0A,F0,32)，移開恢復。
- `helpcom`／`helpcon` 的開法（說明選單或伺服器送 S39）待查；開不到就改截 `silrein34`、`helpagi01` 等含 `ffffaf`／`afffff` 標題的頁。

## 4. 未驗證／推測

- 連結編號：doc`+0x88` 存的是 `sub_8707E0` 在連結表（doc`+0x40`）找到的索引，attr`+0x18` 是解析時 `v262++` 的流水號；兩者推測是同一套編號（未逐一核對 `sub_8707E0`）。
- 前一個 `<a>` 沒關又開新的 `<a>` 時，新連結的 attr`+0x18` 留 −1（`sub_8676F0` 第 464 行附近），這種連結不會變 hover 色（推測，邊角）。
- §1 #14 特殊模式用在哪一頁未查。
