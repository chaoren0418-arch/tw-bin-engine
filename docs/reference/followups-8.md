# 查證補遺第 8 輪：頭上條基準、鑑定訊息、決鬥送出點、BehaviourUI、list.spr 欄位、拾取牆、幽靈旗標 1、刪除確認與料理清單

T337（#783）。查 [`followups-7.md`](followups-7.md) 第 8 節的 **Q6-1、Q6-2、Q6-4 到 Q6-9**（Q6-3 已在 FU7§6.2 完成），並順便核對 `SlotWidgetUI.xml`／`SubSlotWidgetUI.xml`。簡稱：本文 **FU8**，引用寫 `FU8§n`。

每節分「原版怎麼做／3.81 是否支援／對應要改哪個檔案」。記號：
- **已驗證**＝反編譯、反組譯、原始碼或資源檔直接看到；
- **推測**＝依欄位、命名或旁證判斷；
- **待驗證**＝本輪查不到，寫明卡在哪裡。

## 0. 方法與來源

- **TW16 BIN**：`P01-分析基準/input/TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（本輪重算）。
- **TW16 IDB**：P01 `ida/03-ascii-isolated/TW1608262200.idb` 複製到本 worktree 的 `scratch/rea/ida/tw16.idb`，SHA-256 `3a0bf2065993989e44ec9f6cd764449a2b2ec36f8b499384d53fda16bc17e82f`（與 FU5–FU7 相同）。沒有在副本上建函式或改名。
- **IDA**：IDA 7.7＋Hex-Rays，唯讀批次 51 輪（q1–q51），腳本沿用 `rea-scripts/t742/ida_t742.py`，查詢清單放在 `rea-scripts/t783/q*.txt`。
- **subst**：開始時沒有任何代號被占用；每輪用 `W:`，輸出一律接 `| Out-Null`（FU7 的提醒），每輪結束 `subst` 都沒有輸出。
- **送出點掃描**：`rea-scripts/wo_sendsites.py 118`（`sub_9FBC00` 的 opcode 立即值）。
- **地址換算**（同 FU4§0–FU7§0）：程式碼 FO＝VA − `0x400C00`；`.rdata` FO＝VA − `0x401400`；`.data` FO＝VA − `0x401800`。bss 全域只寫 VA。
- **本機資源**（只讀，不進 git；輸出都在 `scratch/`）：
  - PakViewer CLI（`pak list`／`pak extract`／`xml decrypt`）。用 `rea-scripts/pak/pak_index.ps1 -WithAlt` 重列 `0-客戶端` 全部 **24 個 idx**（`Text`、`Tile`、`Sprite`、`Sprite00`–`Sprite15`、`LiTo\LTtext`、`LTsurf`、`LTsprite`、`LTtile`、`LTicon`），另列 kor25／t89／ly 的 `Tile`、`Text`、`Sprite*.idx`。檔名搜尋結果見 §3.3、§4、§9。
  - 解出的檔案與 SHA-256（前 16 碼；「解密」＝`xml decrypt` 後）：

    | 來源 | 檔案 | 原檔 | 解密 |
    |---|---|---|---|
    | 本機 `Tile.idx` | `Actions.xml`（3,330 B） | `8a2e289a0be7d01c` | `20c180afe54e6e10` |
    | 本機 `Tile.idx` | `ActionUI-c.xml`（1,900 B） | `dad69f9392bc4889` | `ae467982a6880161` |
    | 本機 `Tile.idx` | `ActionUI.xml`（2,181 B） | `932085dfe4303481` | `3387e7a16e417d69` |
    | 本機 `Tile.idx` | `SurfList.xml` | `754c27542f0b803b` | `a596c2529a0721e0` |
    | 本機 `Text.idx` | `alertitems.tbl`（1,382 B，明文） | `a39418ec37a4d62d` | — |
    | kor25 `Tile.idx` | `Actions.xml` | 與本機相同 | 與本機相同 |
    | kor25 `Tile.idx` | `BehaviourUI.xml`（3,652 B） | `4d5d0d8e2c2d362a` | `000fc3f65941f95c` |
    | kor25 `Tile.idx` | `WidgetSlot.xml`（7,139 B） | `62653515a4e0b0ab` | `504ca99eb16d7127` |
    | kor25 `Tile.idx` | `WidgetSubSlot.xml`（9,144 B） | `e0dafa1b9df4b0cb` | `368c0ea67ec12bb1` |
    | kor25／ly `Sprite02.idx` | `8393.img`（584 B，兩版相同） | `45c8f6df12c01942` | — |

  - `string-c.tbl`：`rea-scripts/t680/t680_strings.py`（`decoded_sha256` `4ebbc1a7…43e9`，3484 筆）。
  - `list.spz`：新腳本 `rea-scripts/t783/listspr_fields.py`（SHA-256 `e8cec038…6242`，12230 個 gfx），統計欄位 100–119 的使用數。
- **3.81 伺服器**（`D:\lineage開發\1-伺服器\src`，唯讀）：`S_IdentifyDesc.java`、`S_CharPacks.java`、`S_Door.java`、`ClientPacketStructure.java`。
- **引擎**：本分支 `engine/source`（main `acf1e58b`），只讀，不建置、不執行。
- 全程靜態分析。沒有啟動遊戲、伺服器或 DB；沒有改產品程式；沒有任何 BIN、IDB、pak 內容進 git。

## 1. Q6-6 頭上條（優先；擋 R6-B／#784）

### 1.1 自己的 x 基準（已驗證）

`sub_8E77F0`（VA `0x8E77F0`／FO `0x4E6BF0`）算頭上條位置：

| 物件 | x | y |
|---|---|---|
| 自己（`obj+4 == dword_15442C8`） | **P.x ＋ `dword_154524C` − 80** | bbox 上 − 12 |
| 其他 | bbox 左 ＋ 寬/2 − 24 | bbox 上 − 12 |

**P 是什麼**：`P = sub_F1A750(obj+16)`（VA `0xF1A750`／FO `0xB19B50`）。

- `obj+16` 是物件座標。X 是**加倍的**世界 x：送 C23／C131 時寫 `((X − 0x8000) >> 1) + 0x8000`（`sub_5001C0`、`sub_500640`），所以 X＝2·(wx − 0x8000)＋0x8000。
- `sub_F1A750`：`P.x = 12·((X − cx + 1) & ~1) + 24·(Y − cy) − 24`；`P.y = 12·(Y − cy) − 6·((X − cx) & ~1)`。(cx, cy)＝相機格 `dword_154520C`／`dword_1545210`。換成世界格就是 `P.x = 24·(Δwx + Δwy) − 24`、`P.y = 12·(Δwy − Δwx)`，和引擎 `Coordinates.Screen` 同一個菱形公式（差一個常數）。
- 物件的外框也是用同一個 P：`sub_A3E850` → `sub_7CE1C0(frame, P)` 把影格矩形加上 P（`sub_565E50` 只做點加法，沒有額外位移）。所以 **P＝精靈區塊偏移的原點**。引擎已有同一個約定：`ObjectRendering.cs:159`「`sub_F1A750` 的人物畫面點＝`Coordinates.Screen(x,y) −(24,12)`」。
- 名稱用同一個 P：`sub_EFF300` 的自己分支（`0xEFF3AD`–`0xEFF3C8`、`0xEFF4E8`–`0xEFF503`）是 `X0 = P.x − 3·len + dword_154524C − 56`（`lea ecx,[edx+eax−38h]`）。AN§2.3「腳下格的螢幕 X」就是這個 P.x。
- `dword_154524C`／`dword_1545250`：UI 原點偏移。`sub_EBE050` 在 `0xEBE06D` 設成 0；它被 300 多處 UI 程式當成「800×600 的左上角」讀。800×600 下是 0（**已驗證**寫入點只有這一處 mov；其他解析度的值沒查）。

**相機與 P 的實際值**：
- `sub_A49BC0`（FO `0x648FC0`）與 `sub_F09C70`（FO `0xB09070`）設相機＝`(角色 X + 4, 角色 Y − 22)`（`sub_F09C70` 再把 `(a2 − 4, a3 + 22)` 傳回 `sub_A6DEE0`）；走一步時 `sub_A4C270`（`0xA4D949`）把相機和角色一起移一格。
- 所以自己永遠是 ΔX＝−4、ΔY＝22：**P＝(456, 288)**。
  - 自己的頭上條 x＝456 − 80＝**376**，寬 44 → 中心 398。
  - 自己的名稱 X0＝456 − 56 − 3·len＝**400 − 3·len**，正好以 x＝400 置中。
- **推測**：原版刻意把自己的名稱與頭上條固定在 800 寬畫面的中線，不跟著動作影格的外框跳動。

**對引擎（R6-B／#784）**：
- 用相對公式，和相機無關：`P = Coordinates.Screen(self) − (24, 12)`（`ObjectRendering.cs:159` 的既有約定），再經 `Camera.ToView`。
  - 名稱、稱號：`X0 = P.x − 56 − 3·len`（盟徽 X0 − 20、血盟 ＋10 照 AN§2.3）。
  - 頭上條：`x = P.x − 80`，y 仍是外框上 − 12。
- **待驗證**：原版 P 固定在 (456, 288)，腳下點 P＋(24, 12)＝(480, 300)；引擎相機是把 `DisplayPosition`（腳下點）放在畫面中央（`Program.cs:160`）。若兩者的相機原點不同，自己在畫面上的絕對位置會差 80 px 左右，這屬於相機，不是 R6-B 的範圍。要真機截圖比對自己的身體中線與名稱中線（加進 #475 的 M14／M15）。

### 1.2 閃爍條與門檻（已驗證）

頭上條的繪製物件由 `sub_8E7030`（FO `0x4E6430`）依 `sub_74D750(obj, byte_1542AA1)`（FO `0x34CB50`）選：

| kind | 條件 | 建立（`sub_74D5D0`／FO `0x34C9D0`） | 畫什麼 |
|---|---|---|---|
| 0 | 其他物件 | `off_116BCC4` 類別（`sub_706EF0`） | **只畫 HP**（`sub_706FE0`／FO `0x3063E0`）；MP 一律不畫 |
| 1 | 自己，職業碼 0–6 | `MiniHpMpBarBlinkDraw`＋`HpWarnBlinkChecker(35, 5, 0)` | HP＋MP，再疊閃爍 |
| 2 | 自己，職業碼 7（戰士，FU6§2） | `MiniHpMpBarBlinkDraw`＋`HpWarnBlinkChecker(40, −1, 0)` | 同上 |

- `MiniHpMpBarBlinkDraw` vtable `0x1180DD0`（FO `0xD7F9D0`），+12＝`sub_8E4C00`（FO `0x4E4000`）：先畫一般 HP／MP（`sub_8E4E80`），再問檢查器；回真時 `sub_8E4B90`（FO `0x4E3F90`）在 (x, y) 畫 surf 索引 201（UI§2 V1：8393），來源寬 `44·HP%/100`、高 4。`showCharacterHPMP`（`byte_138BE7C`）關閉時整組都不畫。
- `HpWarnBlinkChecker`（vtable `0x116EB2C`／FO `0xD6D72C`，建構子 `sub_740BB0`）欄位：+4 門檻、+8 次數、+12 初始狀態、+16 累計毫秒、+20 已亮次數、+24 目前狀態。
- 每幀 `sub_740D20`（vtable +12，FO `0x340120`）：
  - **HP% > 門檻**：已亮次數歸 0，狀態回初始（不亮）。次數 −1（戰士）時，等目前這段時間走完才回初始。
  - **HP% ≤ 門檻**：累計時間超過間隔就切換亮／滅，每次變亮 +1。
  - 次數 5（一般職業）：亮滿 5 次之後，下一段時間到就固定回「不亮」，直到 HP 先回到門檻以上。
  - 次數 −1（戰士）：一直閃。
- **間隔**＝`sub_740CE0`（vtable +20，FO `0x3400E0`）＝**max(300, 26 × HP%) ms**。例：HP 35% → 910 ms；10% → 300 ms。累計時間取自幀時間（`sub_F67FA0` 讀 `+40`）。
- `sub_741080`（FO `0x340480`）另有 case 1（35, 5, 1）、case 2（`HpWarnCheckerAlways`，門檻 40）兩種，只被 `sub_741350`（`HpWarnCheckerWarrior` 系，HP 窗用）建立，頭上條不用。

### 1.3 +86／+87 變體（已驗證：頭上條沒有）

- 頭上條的 HP 底／填充、MP 填充固定用 surf 索引 162／163／164（`sub_8E4D40`、`sub_8E4DE0`），**不看** +86／+87。
- +86／+87 只在 HP 窗（`HpMpWindow`）被讀：`sub_A43010`（`this[86]`）、`sub_A43030`（`this[87]`），呼叫者有 `sub_ED2680`、`sub_ED56D0`（HP 窗）與 `sub_945BD0`、`sub_A51920`、`sub_85FB50`。
  - `S_PARALYSE`（`sub_945BD0`／FO `0x544FD0`）type 2／4、10、12、22／28 呼叫 `sub_A458F0(1)`；解除時呼叫 `sub_A458F0(0)`。**推測** `sub_A458F0` 寫 +86，所以 +86＝麻痺／凍結類（灰條）。
  - +87 由 `sub_A45A60` 寫；**推測**是中毒（綠條，UI§2 V1 的推測不變）。
- → 引擎 `HpMpHeadBarPanel` 不必做變體；變體只屬於 4.2 `HpMpWindow`（`NativeUi/HpMpPanel.cs` 的浮動窗）。

### 1.4 surf 8393（已驗證：本機沒有、參考客戶端有）

- 本機 24 個 idx 的清單都沒有 `8393.*`；本機 `SurfList.xml` 也沒有 id 8393（930 有）。
- **kor25 與 ly 的 `Sprite02.idx` 有 `8393.img`**（584 B，兩版 SHA-256 相同 `45c8f6df…`）；t89 沒有。
- → 閃爍條可以用 P-B 的參考客戶端 surf 備援（#760）補圖；本機沒有就不畫閃爍，其餘照常。

### 1.5 引擎對應

| 項目 | 檔案 | 去向 |
|---|---|---|
| 自己的名稱 X0 | `ObjectRendering.cs`（只限 `NameLayout`） | #784（R6-B）照 §1.1 |
| 自己的頭上條 x | `NativeUi/HpMpPanel.cs`（`HpMpHeadBarPanel.Anchor`，`:242–243` 的「待驗證」註解改掉）；`NativeUi/State/HpMpState.cs`（自己要多帶 P） | 新 K5 |
| 自己的閃爍條 | `NativeUi/HpMpPanel.cs`（門檻 35／40、次數 5／無限、間隔 max(300, 26·HP%)，surf 8393 走參考客戶端備援） | 新 K5 |
| 其他物件只畫 HP | 引擎本來就沒有他人的 MP（`HpMpState.cs:81`） | 無差異 |
| +86／+87 | 只屬於 `HpMpWindow`（浮動窗） | 併入 4.2 `HpMpWindow` |

## 2. Q6-1 S_IDENTIFY_CODE 的顯示方式（已驗證）

處理函式 `sub_940070`（VA `0x940070`／FO `0x53F470`），由封包分派 `sub_947530`（`0x9479AD`）呼叫。讀 `hhc`（字串 `0x1183288`）：說明編號、訊息 id、參數個數，參數字串在後。

1. 說明編號 1063–1072 → 訊息 id 一律改成 **928**「\f1%0：寵物專用牙」。
2. 訊息 134（武器）而且說明編號是 **4139–4142**（`sub_88E7C0`，表 `dword_136DD94`＝`0x102B`–`0x102E`，`.data` FO `0xF6C594`）→ **只印物品說明**，不印打擊值那一行。
3. 其他情況：
   - 先用 `sub_933850` 把字串表第「訊息 id」筆套進參數，印一行；
   - 再印**物品說明**一行。
4. **物品說明**＝`dword_13CD880[說明編號]`，也就是開機時 `sub_87BA80("ItemDesc")` 讀的 `ItemDesc-c.tbl`（`StartAddress`／`sub_8CA220`，FO `0x4C9620`）。編號 ≥ 筆數（`dword_13CDAA8`）或該筆空白時不印。
5. 兩行都用 `sub_7DC850(文字, palette[24], palette[14], −1, −1, 0)` 印**在聊天欄**，淡黃色（FU5§2 同一條路）。**沒有視窗，也沒有畫面中央的字。**

**3.81**：`S_IdentifyDesc.java` 有兩種寫法。
- 一般形式：`H(說明編號) H(訊息) C(n) S×n`，訊息是 134／135／136／137／138。
- 只寫說明編號的物品（其餘是補 0）：原版讀到訊息 0、參數 0 個，第 3 步先印第 0 筆（**推測**是空字串），再印物品說明。所以這類物品原版仍會在聊天欄顯示 `ItemDesc` 的說明。
- 無參數建構子 `S_IdentifyDesc()` 在 3 個計數字串後多寫一個重量字串；原版只讀計數內的 3 個，**重量不會顯示**。
- 本機有 `ItemDesc-c.tbl`（`Text.idx` #13249，108,872 B）。

**引擎現況**：`DisplayNotices.cs:74–89` 存成畫面下方白字 8 秒（`:126`）；訊息 0 時直接 return（`:86`）；重量字串會接在後面（`:88`）。

**差異 → K5**（改 `DisplayNotices.cs`，必要時 `ServerMessageText.cs` 加 `ItemDesc-c.tbl` 讀取）：
- 改印聊天欄（淡黃，同 S250 的入口）；
- 加第二行 `ItemDesc` 說明；
- 928 與 4139–4142 兩個特例；
- 訊息 0 也要印說明；不接重量字串。

## 3. Q6-2 C_DUEL 的送出點與決鬥游標（已驗證）

### 3.1 送出點

- `wo_sendsites.py 118` 只找到 **1 個**送出點：`sub_F434F0`（`0xF434FA`／FO `0xB428F0`）送 `sub_9FBC00("c", 118)`，**沒有欄位**。
- 唯一呼叫者是 `sub_F54C60`（FO `0xB54060`）。它是聊天指令類別 **`Commandduel`** 的執行函式：vtable `0x11EB7C4` 的前一格是 `??_R4Commandduel@Lineage@@`（`0x11EB7C0`／FO `0xDEA3C0`）。
- 指令註冊 `sub_F56320`（FO `0xB55720`）的順序是「建構子 `sub_F50390` → 英文名 **`duel`** → 字串表 **644「決鬥」**」（建構子對應是依 FU7§6.2 的排列規則，**推測**；英文名與字串號已驗證）。
- 所以原版的入口是聊天指令 **`/duel`、`/決鬥`**。指令不檢查面對誰；判定交給伺服器（3.81 的 `C_Fight` 用 FaceToFace）。
- **3.81**：TW16 op 118 對到 3.81 的 C5 `C_OPCODE_FIGHT`（gap-matrix 第 3 節），無欄位；引擎 `DuelPackets.Invite()` 已是 `[5]`（`Duel.cs:5`）。

### 3.2 游標

`sub_F044C0`（FO `0xB038C0`）的物件類別 5／11（玩家）分支：

- 目標是「+197＝2（敵對血盟）、粉紅名（vtable+124 > 0）、**決鬥對手 `dword_15442DC`**、`sub_EFACE0`、`sub_707890`」其中之一，而且 `sub_A3FF10` 成立，就走**攻擊游標**：
  - `byte_13CE3F9` → 7；
  - `byte_13CE3FA` 為 0 → 依武器狀態 21／17／18／6／22（`sub_A42A60`、`sub_A429E0`、`sub_990980`、`+604`）；
  - 否則依距離 8／19／20（`byte_139FBAF` 時 15／23／24）。
- 決鬥對手和粉紅名、敵對血盟**完全同一組游標**，沒有專用游標。
- 游標編號 → PE 游標資源：`sub_8EEBF0`（FO `0x4EDFF0`）用 `LoadCursorA(hInstance, lpCursorName[i])` 載入 1–36 號，表在 `0x13720E8`（`.data` FO `0xF708E8`）。

  | 編號 | 3 | 6 | 7 | 8 | 15 | 17 | 18 | 19 | 20 | 21 | 22 | 23 | 24 |
  |---|---|---|---|---|---|---|---|---|---|---|---|---|---|
  | 資源 id | 105 | 133 | 144 | 136 | 145 | 224 | 219 | 220 | 227 | 225 | 226 | 233 | 234 |

  `sub_8EECE0`（FO `0x4EE0E0`）在 `dword_15442CC` 的特殊狀態下把 6／8／17／3／14 換成 31／32／33／34／35。

### 3.3 引擎對應

- `DuelUi.cs:41–46` 用 `Cursors.Cross`，條件 `CanAttackDirectly`（與原版同一組目標）。差在游標圖。
- 入口只有開發工具的「決鬥」分頁（`DuelUi.cs:15–26`）；**沒有 `/duel`、`/決鬥`**。
- → **K5**：
  - 加聊天指令 `/duel`、`/決鬥` → `DuelPackets.Invite()`（檔案：`Duel.cs`、`DuelUi.cs`；聊天分派照 `PlayerQuery.cs:38` 的做法，不動 `PlayerChat.cs` 的其他指令）；
  - `Duel.cs:55` 的過時提示一起改（15.6 小清理）。
  - 游標圖要從 BIN 的 PE 游標資源取，屬另一件事（游標資源是否可在執行期唯讀載入，由審查決定；不擋 `/duel`）。

## 4. Q6-4 `BehaviourUI` 的版面、項目、回呼（已驗證，版面檔是推測）

### 4.1 類別與版面檔

- 建構子 `sub_7C6910`（VA `0x7C6910`／FO `0x3C5D10`）：`XmlUI("BehaviourUI")`（`sub_B4E720`），另建 `CActionDb("Actions.xml")`（`sub_AD8E60`／FO `0x6D8260`）。由 `sub_B56A40`（`0xB57C15`）建立。
- **版面檔**：XmlUI 依名稱讀 `BehaviourUI.xml`（**推測**，同 15.2 第 3 點 WidgetSlot 的情況：BIN 的 XML 檔名表 `0x137B39C` 72 筆裡沒有 `BehaviourUI.xml`）。
  - **本機沒有** `BehaviourUI.xml`（24 個 idx 都查過）。
  - **參考客戶端有**：kor25 `Tile.idx` #4369（3,652 B）、ly #5632–5633（含 `-c`）、t89 #4679（小寫）。
  - kor25 版的事件名（`CloseWindow`、`TabButton`、`GridEvent`、`QuickEmotion`、`EmotionGridEvent`、`KeyEvent`）和 TW16 建構子註冊的名稱一致，佐證 TW16 用的就是這份。
- **本機有另一個較舊的動作窗**：`ActionUI.xml`／`ActionUI-c.xml`（`Tile.idx` #5372–5373）。
  - 它的事件名是 `OpenButtonPressed`、`KeyboardEvent`、`GridEvent`、`ExitButtonPressed`、`TabButtonPressed`。
  - TW16 沒有 `ActionUI` 類別（RTTI 找不到；`ActionUI.xml` 只留在檔名表 `0x137B41C`）。
  - **推測**：3.81 客戶端（`LiTo.bin`，有殼）用的是本機的 `ActionUI-c.xml`＋`Actions.xml`。

### 4.2 版面

| 來源 | 視窗 | 格子 | 分頁 | 其他 |
|---|---|---|---|---|
| kor25 `BehaviourUI.xml`（TW16） | `Behaviour_Window`，底 png 10643、框 10633，可拖曳 30 | 動作格 (27, 53)，6 列 × 4 欄，32×32，間距 2，可拖；選取框 png 10290；捲軸 (141, 6) | `Tab_Control` (27, 26)：`Action_Button`（png 10640–10642）、`Emotion_Button` x＋68（10634–10636） | 表情頁：快捷表情 4 格（surf 8170–8173，x 27、y 69，橫排）、表情格 4×4 於 (27, 120)、`WheelControl`；關閉鈕 (168, 9) surf 6125／6126 |
| 本機 `ActionUI-c.xml`（推測 3.81） | `Action_Window`，底 surf 5141、框 5142，可拖曳 20 | 動作格 (21, 45)，4 列 × 6 欄，32×32，間距 1，可拖；選取框 surf 5333；捲軸 (202, 8) | (−2, 20) 起 4 顆：`Total`（5144–5146）x 23、`Community`（5147–5149）x 73、`Action`（5150–5152）x 123、`Management`（5153–5155）x 173 | 常駐開窗鈕 `ActionUI_Button` (774, 470) surf 5156／5157，提示字串 3118；關閉鈕 (228, 11) surf 1962／1963 |

`Actions.xml`（本機與 kor25 相同）列 25 個動作：`category`＝action／community／management，`control`＝action（直接執行）／toggle（開關）／target（要指定對象，`target` 四旗標＝地圖物件、聊天窗、隊伍窗、寵物隊伍窗；`domain`＝global／view／map），各帶一個 surf 圖示（5347–5391、5558、5580、5582）。

### 4.3 回呼（`sub_B4FA80` 註冊；訊息碼 513／514／515＝WM_LBUTTONDOWN／UP／DBLCLK）

| 事件 | 函式（VA／FO） | 行為 |
|---|---|---|
| `CloseWindow` | `sub_7C7160`／`0x3C6560` | 514（放開）→ 關窗（`dword_13AAF08` vtable+68(0)） |
| `TabButton` | `sub_7C8720`／`0x3C7B20` | 513（按下）→ `this[109]`＝分頁，`sub_7C8800` 重排 |
| `GridEvent` | `sub_7C7670`／`0x3C6A70` | **515（雙擊）** → `sub_7C7CB0`：`sub_ADC200` 執行該格的動作，成功後 `sub_ADCB10(0)` |
| `QuickEmotion` | `sub_7C8100` → `sub_7C7940`／`0x3C6D40` | 513 開始拖曳、514 放下（換格）、515 播表情 |
| `EmotionGridEvent` | `sub_7C7380` → `sub_7C77C0`／`0x3C6BC0` | 表情格的同一組操作 |
| `OpenButtonPressed` | `sub_7C80D0`／`0x3C74D0` | 514／515 → 開關窗（vtable+76） |
| `KeyEvent` | `sub_7C7C80`／`0x3C7080` | 鍵碼 27（Esc）→ 關窗 |

動作表 `sub_AD8EF0`（FO `0x6D82F0`）註冊 37 個名稱與處理函式（XML 用其中 25 個）。和引擎有關的幾個：

| 動作 | 處理（VA） | 做什麼 |
|---|---|---|
| `Action_Loot` | `sub_ADA840` | 不是幽靈、`dword_13CE3D8 == 3`、`!byte_13CE3FD`、`!sub_8CB3F0(24)` 時呼叫 **`sub_500640`**（F4 鍵盤拾取，WO§2） |
| `Action_Trade` | `sub_ADB030` | `sub_F43510`（對面前的人交易） |
| `Action_SocialAction1`–`4` | `sub_ADACB0`、`sub_ADADE0`、`sub_ADAED0`、`sub_ADAFC0` | C_ACTION 68／69／67／66，依外型重映射（AN§1.1 已記錄） |
| `Action_InviteChatParty` | `sub_ADA670` | `sub_9FBC00(off_119F834, 120, 3, 名稱)` |
| `Action_Report` | `sub_ADABC0` | `sub_9FBC00(off_119F96C, 55, 42, 名稱)` |
| `Action_OpenShop` | `sub_ADA960` | `sub_4FFCD0("shop", 1)` |
| `Action_ViewDropItemName`／`LootAll`／`CorpseTransparent`／`DungeonTimeLimit` | `sub_ADB040`／`sub_ADA880`／`sub_ADA540`／`sub_ADA5A0` | 切換 `byte_138BE7E`／`byte_135C1E4`／`byte_138BFAA`／`byte_1379424` |
| `Action_HPMPRecoveryEffect` | `sub_ADA630` | 切換 `ShowHPMPRecovery` 選項 |

### 4.4 3.81 與引擎

- 引擎：表情只能從開發工具分頁送（`TitleAndEmote.cs:339–355`）；Ctrl+D 沒接（`NativeUi/MenuButtonsPanel.cs:81`）；沒有動作窗。
- → **K5**（新原生面板，自動發現）：
  - 依本機 `ActionUI-c.xml`＋`Actions.xml` 建原生動作窗（4 分頁、6×4 格、雙擊執行）。3.81 用這份是推測，所以版面要在真機確認。
  - TW16 的 `BehaviourUI.xml` 要用參考客戶端，屬 P-A／P-B 的來源（kor25）。
  - 動作接既有功能：SocialAction→`TitleAndEmote`、Loot→F4 拾取（R6-D）、Trade→`Trade.cs`、各 toggle→既有選項。
  - 檔案：新檔 `NativeUi/ActionWindowPanel.cs`、`NativeUi/MenuButtonsPanel.cs`（只接 Ctrl+D）、新檔測試。
  - 待驗證：3.81 的開窗鍵（TW16 是 Ctrl+D，FU4§1.1）與 `ActionUI_Button` (774, 470) 是否常駐。

## 5. Q6-5 list.spr 欄位的讀取者（已驗證）

本機 `list.spz` 的使用數用 `listspr_fields.py` 數「`NNN.名稱(…)`」形式，避開影格參照：

| 欄位 | 名稱（本機） | 本機 gfx 數 | 寫入（REA B07） | 讀取者（VA） | 原版做什麼 |
|---|---|---:|---|---|---|
| 103 | — | **0** | `0x13CD86C` byte，預設 1 | `sub_A4A620`（`0xA4A8BD`） | 當成最後一個參數傳給自己位置相關的呼叫（未細追）。本機沒有任何 gfx 設定，等於一律預設 1 |
| 105 | `cloth(n gfx…)` | **1,930** | `0x13CD87C`（計數＋word 陣列） | `sub_A3EF40`（含 `0xA3F844`）、`sub_A45160`（FO `0x644560`）、`sub_7A25E0` | **附加精靈層**：物件建立時依清單為每個 gfx 建子精靈（`+164` 起每個 16 bytes，`+324` 計數，旗標取欄位 104）；`sub_7A25E0` 預載。例：gfx 0 → 6262、1 → 6261、37 → 6263 |
| 106 | — | **0** | `sub_8787B0`（往容器追加 word＋dword） | 未追（本機沒有 gfx 用） | — |
| 107 | `size(a b)` | 84 | `0x13CD854` byte＝`a \| b<<4` | `sub_A51920`、`sub_E4D300` | 物件 `+13`＝值，`+200`＝`sub_A4E470(obj, 值)`（**推測**：多格佔位，影響點選與碰撞） |
| 111 | `stride(n)` | 86 | `0x13CD858` byte | `sub_A3DE30`、`sub_A47D90`、`sub_A4C270`、`sub_E640D0`、`sub_A43560`、`sub_A51920`、`0xA43D84` | ①站立時輪流換姿勢：`+532` 計數 0…n，取 `byte_1379D40[+532 (+2)]`；②欄位 108＝2 的外型在動作 44 後改姿勢 11（否則 4），AN§3.5 已記錄 |
| 112 | `altdir(gfx)` | 2（1395、5940） | `0x13CD860` word | `sub_A3EF40` | 物件朝向 `+8 == 6` 時改用替代 gfx |
| 113 | `MorphEffect(gfx)` | 45 | `0x13CD870` word | `sub_946CF0` | 附加特效（AN§3.5 已記錄） |
| 114 | `FurnitureSize(n)` | 32 | `0x13CD874` byte | `sub_A51920` | `+200`＝`sub_A3C190(obj, n)`（**推測**：家具佔位） |
| 118 | `switching(1)` | 2（10369、10557） | `0x13CD878` byte | `sub_A467A0`、`sub_A51920` | 值 1 時依 `sub_ED8BD0()` 把姿勢設 0 或另一個值 |
| 116／117 | `dir(n)`／`lev(n)` | 15／13 | 讀後丟棄 | — | 原版不用 |

**3.81 與引擎**：
- 引擎完全沒有解析 105／107／111／112／114／118（`engine/source` 裡沒有 `cloth`、`stride`、`altdir` 等字樣）。
- **最大的一項是 105 `cloth`**：1,930 個 gfx（含 0、1、37、48、61、138 等職業外型）有附加層。引擎只畫本體，可能少畫一層。
- **待驗證**：
  - 3.81（`LiTo.bin`）讀不讀 `cloth`。本機 `list.spz` 是 0-客戶端（3.81 資源）的檔案，但 TW16 才有這段 parser；舊版客戶端可能直接略過不認得的欄位。要真機看 gfx 0 有沒有 6262 這一層。
  - 107／114 的佔位效果。
- → 5:`sub_87C890` 改 **K5**（`cloth` 附加層、`stride` 站立輪換、`altdir`），檔案 `ObjectRendering.cs`（#784 合併後）、`MagicRendering.cs` 的 sprite 目錄（只加欄位解析）、新檔測試；`cloth` 要先真機確認（新增 #475 項目）。

## 6. Q6-7 拾取牆阻擋的地格旗標 bit 0（已驗證）

- `sub_F12490(x, y, dir)`（FO `0xB11890`）讀格子陣列 `dword_15442C0`：每格 20 bytes，`+4` 是 word 旗標；索引 `sub_6E9B10`＝`X + ((Y − 原點Y) << 8) − 原點X`，**一列 256 格、X 是加倍的半格座標**。
  - 偶數方向：查 `表[dir]` 偏移的那一格 bit 0（偏移表 `dword_138D87C`＝0、0、1、1、256、256、−1、−1，`.data` FO `0xF8C07C`）。
  - 奇數方向（斜走）：兩組相鄰格（`dword_138D87C`／`89C`／`8BC`／`8DC`）任一組都是 bit 0 就擋。
- **bit 0 的寫入者**：
  - 地圖載入（`sub_F10CE0` 等）；
  - **`sub_F07190`**（FO `0xB06590`）在執行期改寫 bit 0。它的呼叫者是封包處理 `sub_932470`（FO `0x531870`，讀 `hhcc`：x、y、方向、旗標，格 X＝2x − 0x8000＋方向）與 `sub_741B40`（矩形範圍）。這就是 3.81 的 `S_Door`（`S_OPCODE_ATTRIBUTE`：`H(x) H(y) C(方向 0／1) C(passable)`）。
  - 所以 **bit 0＝「不可通行」**：地圖屬性加上門的開關。
- **移動用同一個判斷**：`sub_F06CC0`（FO `0xB060C0`）第一步就是 `if (sub_F12490(...)) return 0`。拾取（`sub_5001C0`、`sub_500640`）只在「物件與自己不同格」時查「自己往物件方向（`sub_A40EC0`）那一步能不能走」。
- **3.81**：同一份地圖屬性與 S_Door，伺服器支援。
- **引擎對應**：引擎已有同一個判斷 `MovementRules.Passable`／`MapCache.Passable`，加上門的 `doorBlockedTiles`（`Doors.cs:81–89`），統一入口在 `ApproachWalk.cs:49`。R6-D 的 F4 拾取與點擊拾取在「不同格」時，用這個入口檢查自己往物件方向的一步即可（`Ground.cs`）。WO§2 的「推測是牆」改為已驗證。

## 7. Q6-8 `byte_13CE51E`＝1 的語義（已驗證）

`sub_A50100`（FO `0x64F500`）由自身物件封包 `sub_A51920`（`0xA51A42`）以 `+0x64` 呼叫：

- 參數非 0 → **2**（幽靈，WO§4）。
- 否則 **`dword_14EBFE8 == 1` 而且等級 ≤ 50**（`sub_5008D0`，等級；`sub_F09C70` 用同一個函式判斷「1 級＋地圖 7783 播序章」）→ **1**；其他 → 0。

**`dword_14EBFE8` 是 GM 旗標**（加密整數，`sub_9E3190` 建構、`sub_9E3260` 寫入、`sub_5069E0` 比較）：
- 只有兩組寫入點：選角進入世界的 `sub_EC9510`（`0xEC993C` 寫 0、`0xEC996B` 寫 1）與 XML 選角窗的 `sub_EED2B0`（`0xEED709`／`0xEED738`）。
- 寫 1 的條件是角色紀錄 `+0xAC` 非 0。`+0xAC`＝`+0x78`（職業，FU6§2）往後第 13 個欄位，也就是 S_CHARACTER_INFO `sscchhhcccccccccdc` 的第 14 欄。3.81 `S_CharPacks.java:96–97` 註明這欄「大於 0 為 GM 權限」，**一律寫 0**。
- 旁證：`sub_506D30` 在這個旗標＝1 時才印除錯 log；`sub_5218B0` 在旗標＝1 時把魔法編號附進 log（`"Magic\t%s"`）。

**所以 1＝「GM 角色、等級 ≤ 50」**。效果和幽靈相同的那一串本機限制（WO§4），只是 `sub_5001C0` 在 1 時仍可點 NPC（4）與類別 14。

**3.81**：伺服器一律送 0，`byte_13CE51E` 不會是 1。→ 引擎不必實作值 1，R6-D 只處理 2（幽靈）。5:`byte_13CE51E` 的 Q6-8 結案，主分類維持 K5（巨集格沒擋，R6-D）。

## 8. Q6-9 刪除確認的物品條件、料理清單的欄距／列距與點擊（已驗證）

### 8.1 刪除確認

`sub_98D2E0`（FO `0x58C6E0`，`RenewalInventoryHelper`）逐一看要刪的物品：
1. 堆疊數量 > 1 而且還沒選數量 → `sub_4FD210`：用字串 **4115** 問數量（`sprintf`），回來後再走一次。
2. 數量 1 → 直接標記已選（`sub_4FAF40`）。
3. **`ItemDeleteAlertChecker`**（vtable `0x1187614`／FO `0xD86214`）判斷為真 → `sub_4FD820`：單件用 **3122**，多件用 **4114**，在輸入列問 Y/N，然後結束（等回覆）。
4. 都不需要確認 → `sub_9A15D0` 直接送刪除。

`ItemDeleteAlertChecker` 的條件（`sub_996AA0`）：物品資料 `sub_C021B0`（`this[19]`，16-bit 編號；**推測**是 ItemDesc／名稱編號）在 **`alertitems.tbl`** 的清單內。清單由 `sub_996C40` 讀，每行一個整數。

- `alertitems.tbl` **本機有**（`Text.idx` #806，1,382 B，**232 筆**，例如 25、26、64、65、78…13952–13954）；kor25／ly／t89 同大小。
- 也就是說：**只有清單內的物品才問 Y/N**，其他單件物品直接刪。

**引擎**：R6-E 的「刪除改聊天列 Y/N」要再加這個條件。
- 檔案：`ItemTransfer.cs`、讀表放在既有的 Text.pak 讀取共用處。
- 待驗證：編號用的是哪個欄位。3.81 的背包封包沒有 TW16 的 `this[19]`；要先對 SP 原始碼的 `item_descid`／`nameid`，這一步可以放在 R6-E 開工前。
- 字串 4114、4115 本機沒有（FU7§10），沿用 FU6 的處理。

### 8.2 料理清單

- 清單類別是 **`CCookListBox`**（建構子 `sub_A5AA10`／FO `0x659E10`，vtable `0x11904D0`／FO `0xD8F0D0`）。由 `0x8C7E64` 建立：
  - 矩形 `sub_4F8410(29, 31, 165, 233)`；
  - 基底 `sub_88C0E0(…, 34, 34, 0)` → **欄距 34、列距 34**（32 px 圖示＋2 px 間距），也就是 `sub_A5BA50` 的 `this[8]`／`this[9]`。寬 136＝4 欄 × 34；高 202，可放 6 列（24 格）。
- **點擊／執行**：vtable +88 `sub_A5B9C0`（FO `0x65ADC0`）：
  - 不是幽靈、`!byte_13CE3FD`、`!sub_8CB430()` 時，找第一個「已選取（`+8`）」的項目；
  - 項目可見（`+288`）就呼叫 `sub_A5D2A0`（製作，FU7§3）。
  - 這格 vtable 由哪個滑鼠事件觸發，在基底 ListBox（`sub_88C0E0`）的分派裡，本輪沒追（**推測**是雙擊，與 BehaviourUI 的 `GridEvent` 一致）。
- **CCookWindow 本身**（`sub_A5AAC0`）的控制：關閉鈕 `sub_A5E330`、標題列 (1, 1, 193, 20) `sub_A5D3B0`、捲軸 `sub_8FAB00`（`LScrollBar`，回呼 `sub_A5E6F0`，最大 299）。視窗貼右邊：x＝800 − 寬。
- → R6-F（PR #801）的 4 欄圖示格用 34×34；只是細節，不影響分類。

## 9. `SlotWidgetUI.xml`／`SubSlotWidgetUI.xml`（已驗證：與 15.2 第 3 點一致）

- 所有清單（本機 24 個 idx＋kor25／t89／ly）都沒有 `SlotWidgetUI*.xml`、`SubSlotWidgetUI*.xml`。這兩個是類別名稱，不是檔名。
- 實際檔名 `WidgetSlot.xml`／`WidgetSubSlot.xml`：
  - 本機沒有；
  - kor25 #4566／#4567（7,139／9,144 B）、t89（小寫，同大小）、ly（另有 `-c`；沒有 `-c` 的 `WidgetSlot.xml` 是 8,019 B）都有。
- 和 #800 的結論相同：4:WidgetSlot 已離開 K3、歸 K5（R7-O）。本輪不再改分類。

## 10. 分類去向（給 gap-matrix 15.10）

| 列 | 原分類（15.5） | 新去向 | 依據 | 建議檔案 |
|---|---|---|---|---|
| 1:181 S_IDENTIFY_CODE | K2 | **K5** | FU8§2 | `DisplayNotices.cs`、`ServerMessageText.cs`（ItemDesc 表） |
| 3:C_DUEL(118) | K2 | **K5** | FU8§3 | `Duel.cs`、`DuelUi.cs` |
| 4:BehaviourUI | K2 | **K5** | FU8§4 | 新檔 `NativeUi/ActionWindowPanel.cs`、`NativeUi/MenuButtonsPanel.cs`（只接 Ctrl+D） |
| 5:`sub_87C890` | K2 | **K5** | FU8§5（`cloth` 先真機確認） | `ObjectRendering.cs`（#784 後）、`MagicRendering.cs` 的 sprite 目錄 |
| 5:`sub_8E66D0` | K2 | **K5** | FU8§1 | `NativeUi/HpMpPanel.cs`（頭上條）、`NativeUi/State/HpMpState.cs` |
| 4.2 `HpMpWindow` | K2 | **K5** | FU8§1.3（+86／+87＝麻痺／中毒條，推測） | `NativeUi/HpMpPanel.cs`（浮動窗） |
| 5:`sub_5001C0` | K2 | **K5** | FU8§6（bit 0＝不可通行，與移動同判斷） | `Ground.cs`（R6-D） |
| 5:`byte_13CE51E` | K2 | **K5** | FU8§7（1＝GM 且 ≤50 級，3.81 不會出現） | `PlayerHudModel.cs`、`NativeUi/State/MacroState.cs`（R6-D） |

K2 由 36 列減為 28 列（只剩 Q7）；K5 由 47 列增為 55 列。

## 11. 腳本與查詢

| 檔案 | 用途 |
|---|---|
| `rea-scripts/t742/ida_t742.py` | IDA 唯讀查詢（本輪沿用，未改） |
| `rea-scripts/t783/q1.txt`–`q51.txt` | 本輪 51 輪 IDA 查詢 |
| `rea-scripts/t783/listspr_fields.py` | `list.spz` 欄位 100–119 的使用數與範例（唯讀，輸出 JSON 到 `scratch/`） |
| `rea-scripts/pak/pak_index.ps1 -WithAlt` | 本機 24 個 idx＋參考客戶端清單（本輪重跑，輸出在 `scratch/pak-index/`） |
| `rea-scripts/wo_sendsites.py 118` | C_DUEL 送出點 |
| `rea-scripts/t680/t680_strings.py` | 字串 644、928、134、1738、3122、360 |

## 12. 限制

- 全部是靜態分析，沒有實機觀察。原版以 TW16 為準；3.81 `LiTo.bin` 有殼，不在範圍。所以這幾項只能**待驗證**：
  - 3.81 動作窗用 `ActionUI-c.xml`；
  - 3.81 是否畫 `cloth` 附加層；
  - 原版與引擎相機的絕對位置差；
  - 料理清單的觸發事件。
- 「BehaviourUI／WidgetSlot 依 UI 名稱讀 XML」是推測：BIN 的 XML 檔名表沒有這兩個檔名。
- 游標編號對到的是 PE 資源 id，游標圖本身本輪沒有解出。
- `alertitems.tbl` 的編號欄位對到 3.81 哪個值，要先對 SP 原始碼。
- 字串 4114、4115 本機沒有（同 FU7）。
