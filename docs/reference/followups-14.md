# 查證補遺第 14 輪（FU14）：list.spr 112 `altdir`、111 `stride`、105 `cloth` 的接線時機（T386／#899）

依據：R8-E（#824，PR #892）的 RESULT——資料層 `ListSprFields`／`ListSprLayers` 已完成，但沒有接到繪製；[`followups-8.md`](followups-8.md) §5（FU8§5）；[`gap-matrix.md`](gap-matrix.md) 15.10（M31）、15.12.4／15.12.5 的 5:`sub_87C890`、Q8-2。

記號：**已驗證**＝反編譯、反組譯或資源檔直接看到；**推測**＝依欄位、命名或旁證判斷；**待驗證**＝本輪查不到，寫明卡在哪裡。

**結論**
- 三項 TW16 與 3.81 `LiTo.bin` 的程式**同構**；3.81 的版本也找到了（§0.3）。
- `ClothVerified` **可以打開**（§3.5）。M31 從「阻擋」降為 smoke 時順便看的核對項。
- FU8§5 有兩處要更正：
  - `stride` 不是「站立時輪換」，是**每走一步換一次走路動作**；
  - `altdir` 只作用在「沒有動作表的靜態外型」。

## 0. 來源與方法

### 0.1 TW16

- BIN：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（本輪重算）。
- IDB：P01 `ida/03-ascii-isolated/TW1608262200.idb` 複製到本 worktree 的 `scratch/t899/ida/tw16.idb`，SHA-256 `3a0bf206…7e82f`（與 FU5–FU8 相同）。沒有建函式、沒有改名。
- 地址換算（pefile 重核區段）：
  - `.text` FO＝VA − `0x400C00`；
  - `.data` FO＝VA − `0x401800`（`.data` VA `0x135C000`／raw `0xF5A800`）。
- 腳本：`rea-scripts/t742/ida_t742.py`（未改）。執行器與查詢清單：`rea-scripts/t899/run_tw16.ps1`、`q1`–`q6`。

### 0.2 3.81 `LiTo.bin`

- 用 FU12§0.1 的 OEP 快照 `LiTo.unpacked-oep-current.exe`，SHA-256 `4271d92fb2b9a2fa8d118dde0767f6d8a67a896227bcc24127438fcb5c2ab6e9`。
- IDB：T384 建的 `lito.exe.idb`，複製到 `scratch/t899/lito/ida/`，SHA-256 `c49a30af…8f4ef3`。
- 腳本：`rea-scripts/t896/ida_t896.py`（未改）。執行器與查詢清單：`rea-scripts/t899/run_lito.ps1`、`l1`–`l10`。
- 地址是 VA。快照 FO＝VA − `0x400000`；原檔加密，沒有可用的 FO（同 FU12§0.2）。

### 0.3 對照表（之後查 3.81 物件繪製可以直接用）

| 作用 | TW16（VA／FO） | 3.81 快照（VA） |
|---|---|---|
| list.spr 解析 | `sub_87C890`／`0x47BC90`（case `'i'`＝105 在 `0x87CF56` 一帶） | `sub_43DE10`（case `'i'` `0x43E4BE`；`'o'`＝111 `0x43E6F7`；`'p'`＝112 `0x43E715`） |
| 105 表（計數＋word 陣列，每 gfx 8 bytes） | `dword_13CD87C` | `dword_647E48` |
| 111 表（byte） | `dword_13CD858` | `dword_647E58` |
| 112 表（word，預設 −1） | `dword_13CD860` | `dword_647E54` |
| 104 旗標（附加層的繪製旗標） | `dword_13CD84C` | `dword_647E68` |
| 每幀選圖（換 spr 子檔） | `sub_A3EF40`／`0x63E340` | `sub_4E5C00` |
| cloth 每幀載入 | `sub_A45160`／`0x644560` | `sub_4E6EA0` |
| 放進場景（本體＋附加層＋影子） | `sub_A46620`／`0x645A20`，層：`sub_A463C0`／`0x6457C0` | `sub_4E0200`，層：`sub_4E7000` |
| 從場景移除 | `sub_A42140`／`0x641540`，層：`sub_A44980`／`0x643D80` | `sub_4E01A0`，層：`sub_4E70B0` |
| 物件建立（S_CharPack） | `sub_A51920`／`0x650D20` | `sub_4E7A40` |
| 他人走一步 | `sub_A47D90`／`0x647190`（vtable 方法） | `sub_4E1870` |
| 自己走一步 | `sub_A3DE30`／`0x63D230`（第一步，送 C175）；`sub_A4C270`／`0x64B670`（後續步） | `sub_4E2150` |
| stride 動作表 | `byte_1379D40`／`.data` FO `0xF78540`；`byte_1386BF0`／FO `0xF853F0`（內容相同） | `byte_636938` |
| 職業外型判斷 | `sub_8CA110`／`0x4C9510` | `sub_47BAC0`（另有內嵌版本） |

### 0.4 資源（只讀，不進 git）

- **PakViewer CLI**：`pak_index.ps1 -WithAlt`（輸出 `scratch/t899/pak-index/`）。
  - 本機 24 個 idx：`Sprite`、`Sprite00`–`15`、`Text`、`Tile`、`LiTo\LTicon`／`LTsprite`／`LTsurf`／`LTtext`／`LTtile`；`launcher\TW13081901.pak` 沒有 idx。
  - 參考客戶端 kor25／ly／t89 的 `Sprite*`、`Text`、`Tile`。
- **檔名查詢**：
  - `6255`／`6256`／`6261`–`6264`／`1395`／`1410`／`5940` 的 `*-*.spr`（`scratch/t899/frames.py`）。
  - `list.spr`、`list.spz`、`LTlist.spr`（三處 Text 系 idx 都查）。
- **`list.spr`／`list.spz` 欄位**：新腳本 `rea-scripts/t899/listspr_compare.py`，結果見 §3.4。
  - `list.spr`（3.81 讀）：4,067,417 B，SHA-256 `523c61ad…f127`，11,297 個 gfx。
  - `list.spz`（TW16 讀，引擎目前也讀這份，`MagicRendering.cs:42`）：SHA-256 `e8cec038…6242`，12,230 個 gfx。
  - `LiTo\LTtext.idx` 的 `LTlist.spr`：285 個 gfx，沒有 105／111／112。
- **cloth 圖塊數**：新腳本 `rea-scripts/t899/cloth_tiles.py`，直接唯讀本機 `Sprite*.idx/.pak`（不加密）的 spr 檔頭，檔頭格式同引擎 `MagicRendering.Decode`；不寫檔。
- **3.81 伺服器**（`D:\lineage開發\1-伺服器\src`，唯讀）：`ActionCodes.java:294`、`FishingPole.java:76`、`L1PcInstance.java:1244`。
- **subst**：開始時只有別的任務占用的 `Q:`；本輪只用 `V:`，每輪 `finally` 解除，結束時 `V:` 沒有殘留。
- 全程靜態。沒有啟動遊戲、伺服器或 DB；沒有執行任何編譯出來的程式；沒有改產品程式；BIN、IDB、pak 內容都只在 `scratch/`。

## 1. 112 `altdir`（已驗證）

### 1.1 原版

讀取點只有每幀選圖函式：TW16 `sub_A3EF40` `0xA3F178`–`0xA3F199`（FO `0x63E578`–`0x63E599`）；3.81 `sub_4E5C00` 讀 `dword_647E54`。邏輯：

1. 先用 (gfx, 目前動作) 查動作表（`sub_506210`）。
2. **查得到**，或動作是 119／120：走一般動畫分支，**完全不看 112**。
3. 查不到、但動作 > 0：退回姿勢 +194 再查（AN§3.4）；仍走一般分支。
4. 查不到，而且動作 ≤ 0：才進「靜態外型」分支：
   - `112[gfx] ≥ 0` 而且朝向（TW16 `+8`、3.81 `+17`）＝**6** → 載入 `<altdir>-0.spr`；
   - 否則載入 `<gfx>-0.spr`。
   - 影子照 101 欄位，用的是**換過的那個 gfx 的 101**（`dword_13CD868[ArgList]`）。
5. 不是自己，而且 spr 不存在 → 物件 gfx（+10）**永久**改成 1120、+12＝0、動作 0。

**時機**：
- `sub_A3EF40` 每換一格影格就叫一次（`sub_A43560` +12 前進後、物件建立、轉向、走路）。條件每次重算，**沒有快取**。
- 轉到朝向 6 就換成 altdir；轉離 6 下一次重畫就換回原 gfx。**不是建立時換一次。**
- 原 gfx（+10）不變，換的只是載入的 spr。

### 1.2 本機資源

| gfx | list.spr | spr |
|---|---|---|
| 1395 | `a l chair a`，`102.type(0) 101.shadow(1396) 112.altdir(1410)`，**沒有動作** | `1395-0.spr`（本機與三個參考客戶端都有） |
| 5940 | `1=1395 a l chair a`，`101.shadow(1396) 112.altdir(1410) 107.size(1 2)` | **四個客戶端都沒有任何 `5940-*.spr`** |
| 1410 | `a r chair a`，`101.shadow(1411)` | `1410-0.spr`（四份都有） |

- 「左向椅子」在朝向 6 改畫「右向椅子」。
- 5940：朝向 ≠ 6 時 `5940-0.spr` 不存在 → 原版把物件永久改成 1120，之後轉到朝向 6 也不會再換。只有建立時就是朝向 6，才畫得出 1410。
- **推測**：5940 只是 1395 的別名（`1=1395`），但原版 `sub_A3EF40` 的靜態分支不跟別名，用的是 gfx 本身。

### 1.3 建議接線

- **位置**：`ObjectRendering.cs:33–34`（`ObjectDrawFallback.Appearance(o.Gfx, …)` 之前）。
- **目標**：「該 gfx 沒有任何動作序列（靜態外型）而且 `o.Direction==6`」時，用 `ListSprLayers.AltDirGfx(o.Gfx, o.Direction, fields)` 換載入的 gfx；每幀重算，不改 `o.Gfx`。
- 細節：
  - `AltDirGfx` 現在只看朝向。接線時要多加「靜態外型」條件（1395／5940 本來就沒有動作，結果相同，但條件要照原版寫）。
  - 1410 要有 `ObjectAssets` profile 才畫得出來；沒有就照 R2-I 退回。**待驗證**：assets 有沒有 1410。
  - 5940 的「永久改 1120」：引擎的 `ObjectDrawFallback` 每幀退回、不持久。差在「先轉到別的方向、再轉回 6」時原版仍是 1120。只有一個 gfx，可以記成取捨。

## 2. 111 `stride`（已驗證；更正 FU8§5）

### 2.1 動作表

`byte_1379D40`（TW16 `.data` FO `0xF78540`）、`byte_1386BF0`（FO `0xF853F0`）、3.81 `byte_636938`，三處 bytes 相同：

| 索引 | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|
| 動作 | **0** | **4** | 11 | 20 | 24 | 40 |

- 第 6 byte 起是 `00 00` 與下一個指標，所以表長 6。
- 索引＝計數 +532（3.81 `+0xC1`）＋偏移；**偏移**＝+199（3.81 `+0x23`）＝2 時 2，否則 0。
- +199＝2 由 `sub_A43560`（`0xA43D60`）在欄位 108＝2 的外型播完動作 44 時設，播完 45 設 1（AN§3.5）。3.81 由 `sub_4E7A40` 依 S_CharPack 狀態低 4 bit＝2 設。
  - 本機 `list.spr` 82 個／`list.spz` 86 個 stride gfx **都沒有欄位 108**，所以偏移 2 實際用不到，只會輪 0 和 4。
- 這幾個數字是武器姿勢的走路動作（0 空手、4 劍、11 斧、20 弓、24 矛、40 杖），**推測**原版借用姿勢動作號當「第二套走路」。

### 2.2 時機：每走一步（＝每格）換一次

四個走路函式都是同一段（以 TW16 `sub_A47D90` 為例，`0xA48112`–`0xA481AA`，FO `0x647512`–`0x6475AA`）：

```
物件朝向 = 新方向
if (stride[gfx] > 0) {
    off = (+199 == 2) ? 2 : 0
    if (目前動作 == 8) 播 +194（姿勢）
    else              播 byte_1379D40[+532 + off]
    if (++(+532) > stride[gfx]) +532 = 0         // 計數 0…n，共 n+1 個值
} else 播 +194
```

- **他人**：`sub_A47D90`（每收到一次移動）。
- **自己**：
  - `sub_A3DE30` `0xA3E58B`–`0xA3E62E`（FO `0x63D98B`–`0x63DA2E`），在送 C175 移動封包之前；
  - 後續步 `sub_A4C270` `0xA4D065`–`0xA4D10A`。
- **另一類物件**：`sub_E640D0`（vtable `0x11DFF44`）用 `byte_1386BF0`。
- **3.81**：他人 `sub_4E1870` `0x4E1A13`–`0x4E1A76`；自己 `sub_4E2150` `0x4E2B95`–`0x4E2BEF`。邏輯相同，wrap 條件也是 `> stride`。
- **不是每幀**：步內的影格照該動作自己的序列播。
- **不是站立**：FU8§5 的「站立時輪流換姿勢」是誤讀。
- **推測**：計數在物件建立時是 0（物件記憶體清零；沒追到明確的初始化指令）。

### 2.3 本機資源

- 本機 `list.spr` 82 個 gfx 全是 `stride(1)`，`list.spz` 多 4 個（11137、11184、11200、11277，3.81 沒有這些 gfx）。
- 例：
  - 1062 `drake`：`0.walkfastI`、`4.walkfastII`；
  - 1461 `great dane_ii`：`0.run_one`、`4.run_two`。
  - → 兩套走路（左右腳）交替。
- 統計（`list.spr`，82 個）：
  - 65 個同時有動作 0 與 4；
  - 9 個只有 11／20、沒有 0／4；
  - 1 個只有 0；
  - 7 個沒有自己的動作列。
- 遇到缺的動作：原版照 `sub_A3EF40` 的退回（動作 > 0 → 姿勢 +194）。

### 2.4 建議接線

`StrideNext` 的 0…n 循環與原版相符。

| 對象 | 位置 | 目標 |
|---|---|---|
| 他人 | `ObjectRendering.cs:38–40`（`moving` 時選 `"walk"` 的地方）；計數放在 `WorldObject`，在 `WorldSession` 收到移動、更新 `MotionAt` 時前進一次 | 有 stride 而且 `moving` 時，外型改用 `catalog.Get(drawGfx, StrideActions[計數])`（0→`"walk"` 本身、4→姿勢 4 的 `"walk"`）。一步一個值，不在每幀前進 |
| 自己 | `ObjectRendering.cs:8`（每幀 `catalog.Get(ownActor.Gfx, ownActor.Weapon)`）；計數在 `ClientLogin.cs:53` `MovePlanned`（每送一步）前進 | 自己走路時用 stride 動作取代 `Weapon` 取外型；`:8` 要讀同一個計數，否則每幀會被覆蓋回姿勢 |

- `ListSprLayers` 要補常數 `StrideActions = {0,4,11,20,24,40}`；+199 偏移目前用不到，可以先不做。
- 目前動作＝8 時不換（照原版保留）。
- 改 `ObjectRendering.cs:122` 的註解（「站立時」→「每走一步」）。

## 3. 105 `cloth`（已驗證；3.81 也畫）

### 3.1 每幀載入（TW16 `sub_A45160`、3.81 `sub_4E6EA0`，兩者相同）

- `sub_A3EF40` 選好本體的 spr 子檔號 v89（含朝向偏移）之後，若 `105[gfx]` 計數 > 0 就叫 `sub_A45160(v89)`；否則層數（+324，3.81 `+140`）＝0。
  - TW16 位置：`0xA3F844`／FO `0x63EC44`。
- 對每個 cloth gfx：
  - 載入 **`<cloth>-<v89>.spr`**，也就是和本體**同一個子檔號**；
  - 繪製時用本體同一個影格索引（+52）；
  - 旗標取 `104[cloth]`（+392 起每層 2 bytes；3.81 `+176`）。
- **任何一層的 spr 不存在 → 這一幀層數歸 0**（整組不畫，下一幀重算）。
- 只走一般動畫分支；靜態外型分支（§1）把層數設 0。
- **不看裝備、不看動作號、不看朝向**，只看 gfx 和「這個子檔存不存在」。

### 3.2 放進／移出場景的條件

| 版本 | 函式 | 條件 |
|---|---|---|
| TW16 | `sub_A46620`（放）、`sub_A42140`（移） | 層數 > 0，而且（不是自己 **或** `sub_8CA110(gfx)` 不成立 **或** 目前動作＝**71**） |
| TW16 | `sub_A463C0`（逐層放） | 物件 +40 bit 1（`&2`）成立時不畫；+40 有 `0x100` 而且層旗標沒有 `8`／`0x80` 時加半透明旗標 1；+40 有 `0x40` 時加 `0x40` |
| TW16 | 其他放入點（`sub_A50850`、`sub_A4C270` 自己走路、`sub_A42030`／`sub_A487B0`） | 層數 > 0 就畫，沒有職業例外 |
| **3.81** | `sub_4E0200`（放）、`sub_4E01A0`（移）、`sub_4E7A40` 建立時 | **層數 > 0 就畫，沒有自己／職業例外**（`sub_47BAC0` 的 5 個呼叫點都不在繪製路徑） |
| 3.81 | `sub_4E7000`（逐層放） | 旗標處理與 TW16 `sub_A463C0` 相同（`&2` 不畫、`0x40`、`0x100`→`|1`） |

- `sub_8CA110`：gfx < 2、61、48、138、37、2786、2796、734、1186、6658、6661、6671、6650、12490、12494（職業外型）。
- 3.81 `sub_47BAC0` 是同一張表的前 9 個。
- 繪製順序：本體 → 各層（list.spr 順序）→ 影子（101）。

### 3.3 gfx 0 那一層 6262 是什麼（已驗證）

`list.spr`：

| 職業外型 | cloth | 名稱 |
|---|---|---|
| 0 | 6262 | `prince fishing line`，`104.attr(8)` |
| 1 | 6261 | `princess fishing line` |
| 37 | 6263 | `elf female fishing line` |
| 48 | 6255 | `knight female fishing line` |

61→6256、138→6264、734→6260、1186→6259、2786→6258、2796→6257（10 個職業外型都是 `cloth(1 …)`）。

`cloth_tiles.py` 看圖塊數（本機）：

| cloth | 子檔數 | 全空 | 有圖的子檔（圖塊數） |
|---|---:|---:|---|
| 6262 | 312 | 304 | 304–311（65–100） |
| 6261 | 312 | 304 | 304–311 |
| 6263 | 272 | 264 | 264–271 |
| 6264 | 272 | 264 | 264–271 |
| 6255／6256 | 320 | 312 | 312–319 |

- gfx 0 的 `71.fishing(1 12, 304.11:4 304.0:4 …)`：釣魚動作用子檔 304＋朝向，正好是 6262 唯一有圖的 8 個。
- **所以職業外型的 cloth＝釣魚線，只有動作 71 看得到**；其他子檔都存在但是空格（所以 §3.1 的「缺檔歸 0」不會觸發，空格畫了也看不到）。
- 這也說明 TW16 自己的例外（§3.2）只是省掉空層，**畫面上和 3.81 相同**（**推測**原因；結果是已驗證的資料推得的）。
- 其他 cloth（`list.spr` 1,608 個 gfx 用 400 個 cloth gfx）平常就看得到，例：
  - 175／184／186／188 `necromancer * light`；
  - 201–210 `cw haira／sendal／shoes／shirtsa／skirta…`（NPC 的頭髮、鞋、衣服分層）。

### 3.4 3.81 有沒有觸發的值（已驗證）

- 觸發只看 gfx，伺服器不送任何 cloth 專用值。
- 3.81 伺服器送職業外型 gfx（S_CharPack 的外型），本機 `list.spr` 對這些 gfx 都有 105。
- 釣魚：伺服器在 `FishingPole.java:76` 送 `S_Fishing(objid, ACTION_Fishing=71, x, y)`（`ActionCodes.java:294`）；他人看見時在 `L1PcInstance.java:1244` 重送。所以 3.81 會出現動作 71，釣魚線會畫出來。
- `list.spr` 與 `list.spz` 的差異：
  - 共有 gfx 的 105 只有 112 個不同（都 ≥ 10968）；
  - 111 只差 4 個（≥ 11137）；112 沒有差異；
  - `list.spz` 多出的 210 個 cloth gfx 全部 ≥ 11297（3.81 沒有這些 gfx）。
  - 職業外型、1395／5940、1062 等兩邊相同。
  - 引擎讀 `list.spz` 不影響本節結論；但嚴格照 3.81 應讀 `list.spr`（另案，不擋接線）。

### 3.5 `ClothVerified` 可以打開的依據

1. 3.81 解析 105：`sub_43DE10` case `'i'`（`0x43E4BE`–`0x43E529`）寫 `dword_647E48`。
2. 3.81 每幀依本體子檔號載入：`sub_4E5C00` → `sub_4E6EA0`。
3. 3.81 放進場景不設條件：`sub_4E0200`、`sub_4E7A40` → `sub_4E7000`。
4. 3.81 讀的本機 `list.spr` 有 1,608 個 gfx 的 105，所有子檔在本機 pak 都找得到（6262 等 312／272／320 個子檔齊全）。
5. 伺服器會送觸發外觀的動作 71。

→ 「3.81 讀不讀 cloth」（FU8§5 的待驗證）已靜態確認：**會讀、會畫**。

**M31 改成 smoke 時順便看**（不擋 `ClothVerified`）：釣魚時有沒有釣魚線、NPC 衣物層。

### 3.6 建議接線

| 位置 | 目標 |
|---|---|
| 他人：`ObjectRendering.cs:62–65`（`ShadowDraws` 與本體 `blocks` 之間） | 對 `ListSprLayers.ClothLayers(fields)` 的每個 gfx，取 `<cloth>-<本體 sid>.spr` 中與本體相同 `sid:frame` 的影格；任何一層缺子檔就整組跳過（§3.1）。序號放在本體 1000000＋ 與影子 1100000＋ 之間，維持「本體→層→影子」 |
| 自己：`ObjectRendering.cs:9`（`Actor(own)`）、`:16`（戰鬥影格）、`:22`（拾取影格） | 同上，用自己的當格 `frame.Key`。照 3.81 不做職業例外（§3.2） |
| 旗標 | 隱身／不畫（引擎對應 +40&2 的狀態）時不畫層；半透明狀態下層旗標沒有 8／0x80 才半透明（`104.attr(8)` 的釣魚線不半透明） |
| 取圖 | 可以借 `MagicRendering.cs:73–85` 的 spr 讀取與 `Decode`（依 `Sprite*.idx` 找 `<gfx>-<sid>.spr`）；`MagicRendering.cs:71` 已有 `Fields` |
| 開關 | `ObjectRendering.cs:124` `ClothVerified` 改 `true`，`:118–122` 的註解改成本節的依據 |

**注意**：引擎目前把 S158 動作 71 當成缺圖、改播 `"walk"`（`ObjectRendering.cs:35–36`），所以接上 cloth 後，職業外型的釣魚線**要等動作 71 真的播出來才看得到**；NPC 衣物層接上就看得到。

## 4. 對引擎的總表

| 項目 | 檔案:行 | 去向 |
|---|---|---|
| altdir：只限靜態外型、每幀依朝向重算 | `ObjectRendering.cs:33–34`、`:129` | 新 code 任務（§1.3） |
| stride：每步前進、取 {0,4,11,20,24,40} | `ObjectRendering.cs:8`、`:38–40`、`:131`；`ClientLogin.cs:53`；`WorldSession`（他人計數） | 新 code 任務（§2.4） |
| cloth：開 `ClothVerified`、畫在本體與影子之間 | `ObjectRendering.cs:9`、`:16`、`:22`、`:62–65`、`:124` | 新 code 任務（§3.6） |
| 動作 71 釣魚的播放 | `ObjectRendering.cs:35–36` | 不在本輪；cloth 的釣魚線要靠它才看得到 |
| 動畫表讀 `list.spz` 而 3.81 讀 `list.spr` | `MagicRendering.cs:42`、`Afflictions.cs:278` | 只記錄；本節三項的值在 3.81 會出現的 gfx 上相同 |

## 5. 限制與待驗證

- 全部是靜態分析。畫面效果（釣魚線位置、NPC 衣物疊法）沒有真機看過 → M31（smoke 時順便看）。
- 3.81 快照是 OEP 當下的記憶體映像（FU12§0.1）。本輪用到的函式都在裡面，反編譯沒有斷點問題。
- stride 計數的初始值（推測 0）、5940 是否真的會出現在 3.81 地圖上，沒查。
- 1410 等 gfx 在引擎 `ObjectAssets` 有沒有 profile，沒有查 assets（只看了原客戶端 pak）。
- `sub_A46100`（欄位 106 的姿勢附加層，`unk_13CDAD4`）不在本輪範圍；本機 list.spr 沒有 gfx 用 106（FU8§5）。

## 6. 給下一棒

- 建議開 1 張 code 任務（R8-E 接線），範圍 §4 前三列，檔案 `ObjectRendering.cs`、`ClientLogin.cs`（只限 `MovePlanned` 的計數）、`WorldSession` 的他人計數、`ListSprFieldTests.cs`。
- 測試可以直接用本節的數字：
  - 1395 朝向 6→1410、其他朝向→1395；
  - 1062 連走三步動作 0→4→0；
  - gfx 0 子檔 304 的 6262 有圖、子檔 0 的 6262 是空格；
  - 缺一層子檔時整組不畫。
- gap-matrix：5:`sub_87C890` 由 K2 改 K5，Q8-2 完成，M31 改成非阻擋（見 gap-matrix 15.12 的 FU14 回填）。
