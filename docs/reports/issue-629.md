# Issue #629（T268）：查證原版——送出點與欄位（Q-3）

分支 `task/629`。完整內容見 [docs/reference/send-sites-2.md](../reference/send-sites-2.md)（SS2）。

## 結論

- **以物品學魔法**：
  - C125 唯一的送出點是 HyperAct `xchgspell`，只在 NPC 外型 834 時送 `cd`＋NPC id（3.81 C245）。
  - `buyspell` 在外型 ≠ 834 時送 C24。
  - op 175／44 都把清單放進 `TRADE_WINDOW` 模式 7；空清單時改開 HTML 頁 `HorunEv3`／`GerengEv3`／`Sedia1`／`Siriss1`。
- **血盟推薦**：
  - TW16 收到 op 133 時，要 `dword_153FCC4` 不為 0 才處理，但這個全域沒有寫入點，所以**封包直接丟棄**。
  - C221 的 7 個送出點都是死碼，欄位與 3.81 C76／S0 一致。
  - 建議 R2-K **不做**推薦 UI。
- **op 78**：`PledgeUI` 內的 7 列「關注血盟」清單。
- **`PledgeNoticeUI`**：只由 S_MESSAGE type 17 開，3.81 不送 type 17，**不做**。
- **`checkpk`**：只送 opcode（3.81 C51）。引擎已照做。
- **技能 `+56`／`+60`**：分別是 `SpellList.xml` 的 `delay`／`direction`。本機客戶端沒有這個檔，kor25／ly 有。
- **S36**：戒指能力旗標。type 1 傳送控制 → 開目的地窗；type 5 召喚控制 → 施放召喚術時開 `CSummonUI`。不是 SP／MR。
- **S185 第 3 個 C**：數字框的初始值，3.81 不送；範圍 10–50。
- **S_KICK**：通用模態框加 `PostQuitMessage`。3.81 一律顯示字串 64「連線中斷。」。
- **C53**：只有子碼 16（`timeleft` 指令），繁中 CodePage 950 永遠不送。

## 做了什麼

1. 把 P01 IDB 複製到 `scratch/rea/ida/tw16.idb`，SHA-256 不變（`3a0bf206…e82f`）。用 IDA 7.7 唯讀查詢 14 輪，subst 用 `W:`，執行前先確認沒有被占用。
2. 用 `wo_sendsites.py` 找 125、221、9、53、157、24 的送出點。
3. 從 `sub_BB5180` 反編譯抽出 145 個 HyperAct 名稱與建構子的對照，再用 vtable 的 RTTI 名稱反查。
4. 讀各處理函式（`93F200`、`931FF0`、`B243A0`、`B2E1D0`、`932300`、`94D8E0`、`940960`、`944F50`、`E246E0`、`E177C0`、`F4A150`）。
5. 對照 3.81 伺服器原始碼與 r29 `templates.json`，並用 `t225_strings.py` 查字串表。
6. 解密 kor25／ly／t89 的 XML 到 `scratch/`，取得 `SpellList.xml`。
7. 產出 `send-sites-2.md`，更新 `INDEX.md`、`gap-matrix.md`（11.6 的 Q-3 列，新增 11.8 回填表），新增 `rea-scripts/t268/`。

## 怎麼驗證

- 欄位逐一對照 3.81 原始碼：S0 的 type 0–6、C76 的 data 0–6、S13、S41、S200、S185、S227、S36、C245、C51（SS2 各節表格）。
- S185 第 3 個 C 的意義，是拿 S136 `sub_93FDF0` 寫入同一組 NPC 對話窗欄位（`+0x58`／`+0x60`／`+0x68`）交叉比對得出的。
- `gap_count_r2.py` 修改前後都是：已有 67、部分 175、缺 48。新增的 11.8 表不影響統計。
- 每輪 IDA 結束後都解除 `W:`。
- 沒有改產品程式，只改 `docs/`。

## 未驗證什麼

- 全程靜態分析，沒有實機。
- `dword_153FCC4` 只排除了直接寫入，間接寫入沒有排除。
- `dword_1540828`（傳送目的地窗）的類別、S36 type 3 的作用，都沒有追。
- `SpellList.xml` 未寫屬性時的預設值、XML id 與 3.81 技能編號的換算，都待驗證。3.81（`LiTo.bin`）是否也讀這個檔，也待驗證。
- `tax` 頁怎麼讀 `+0x68`，沒有直接追到。
- S_KICK 原因 28、229–231 的字串，本機字串表裡沒有。
- `C_BUY_SPELL`／`C_EXCHANGE_SPELL` 的確定鈕送出點，以及用暫存器傳 opcode 的送出點，都沒有掃（併入 Q-1）。

## 產出路徑

- `docs/reference/send-sites-2.md`（新）
- `docs/reference/INDEX.md`、`docs/reference/gap-matrix.md`（更新）
- `docs/reference/rea-scripts/t268/`（新）
- `docs/reports/issue-629.md`
- `scratch/rea/`（IDB 副本、偽碼、解密的 XML；不進 git）

## 給下一棒的提示

- **R2-F**：
  - `xchgspell` 接 `RequestSpellExchangeList()`，只在外型 834 時送；`buyspell` 加上外型 ≠ 834 的條件。
  - 空清單的頁名照 SS2§1.2。
  - 稅率框範圍 10–50，初始值要決定（建議用 10）。
- **R2-H**：
  - 冷卻與 18／19 改從執行期的 `SpellList.xml` 讀；沒有檔時維持 `ReuseMs`。用哪個客戶端的檔當基準，需要決定（建議 kor25）。
  - S36 type 1／5 的施法分支（type 5 依賴 #530 的召喚窗）。
- **R2-K**：
  - 拿掉「血盟推薦 UI」與 `PledgeNoticeUI`。
  - 改做血盟窗內的關注清單（7 列）。
- **R2-L**：拿掉「S36 接左狀態框（SP／MR）」這一項。
- **另開小任務**：S_KICK（op 245／S227）改用模態框顯示字串 64，不要丟例外。
- **下一輪 gap-matrix 盤點**：op 133、C_MATCH_MAKING、C_EXTENDED、`PledgeNoticeUI` 建議改列「不建議／伺服器不支援」，op 122 的依據欄要更正。本輪只寫在 11.8，沒有改狀態與統計。
- **重跑 IDA 時**：
  - 腳本必須是無 BOM 的 UTF-8。
  - `run_ida.ps1` 以腳本所在目錄的上一層當根目錄，所以腳本要和它一起放在 `scratch/rea/scripts/`。
  - 執行前先跑 `subst` 確認代號沒被占用（本輪 `T:` 被 task-627 使用中）。
