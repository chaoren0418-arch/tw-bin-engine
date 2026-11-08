# Issue #602（T245）：S250 視窗類子碼（B3）與任務窗、離開窗、通知窗查證

分支 `task/602`。完整內容見 [docs/reference/packetbox-windows.md](../reference/packetbox-windows.md)。

## 結論

- **B3 的 14 個子碼中，真正開窗的只有 52（料理）、117（血盟倉庫紀錄）、146（師徒 HTML）三個。**
  - 168／169／173／37 是寫入既有視窗的欄位。
  - 83 是畫面震動與煙火，84 是畫面中央訊息。
  - 86、190 是狀態圖示，45 是 GM 工具，66 是記分板覆蓋層，192 是純資料。
- **3.81 會送的：** 37、45（只限 GM）、52、83、84、117、168、169、173。
- **3.81 不會送的：** 66、146 沒有呼叫點；86 只有測試指令會送；190／192 是伺服器自訂格式（只在 `ClientLogin=2` 時送），與 TW16 不同。
- **`QuestUI` 不走 S250：** 熱鍵 N 切換，資料走 protobuf `SC_QUEST_*`。3.81 的 `S_ClassicQuest` 是子碼 255 的伺服器自訂擴充，TW16 沒有這個 case。
- **`QuitWindow`：** 死亡逾時（約 8 分 21 秒）送出 C_QUIT 後開的「重新開始／離開遊戲／取消」窗，版面讀 `uiextra86.xml` 的 `QUIT_WINDOW`。
- **`CNoticeWindow`：** 是韓版遺留的「拍賣物品通知」圖示（`NOTICE_WINDOW`），不是登入公告。S_NEWS 實際用的是通用模態訊息框，gap-matrix 原本的對應已更正。

## 做了什麼

1. 把 P01 的 IDB 複製到 `scratch/rea/ida/tw16.idb`，複製後 SHA-256 相同（`3a0bf206…e82f`）。用 IDA 7.7 唯讀批次查詢 7 輪，subst 用 `W:`。
2. 讀 `sub_9357E0` 的 14 個 case，以及它們呼叫的函式（`B2EF40`、`B2EE50`、`C15540`、`B60A60`、`EFDFD0`、`F04260`、`A7C3F0`、`F18F40`、`9DA2B0` 等）。
3. 從 RTTI type descriptor（字串位址 −8）找出 `QuitWindow`／`CNoticeWindow` 的 vtable、建構子與全域，再追全域的引用點。新腳本：`ida_t245_rtti.py`。
4. 在 3.81 伺服器原始碼逐一搜尋每個子碼的建構子呼叫點，判定會不會送。
5. 對照 `uiextra86.xml` 的段落順序（第 0 段 `QUIT_WINDOW`、第 25 段 `NOTICE_WINDOW`）。`t226_surfcheck.py` 確認 7 個 surf 本機都有。
6. 查字串表的 47、130、131、1889、3383；BIN 內嵌的韓文提示字串用 CP949 解碼。
7. 產出 `packetbox-windows.md`，並更新：
   - `INDEX.md`：新增一列，並更新 windows-2 列。
   - `gap-matrix.md`：op 108／179 列、`QuestUI`／`QuitWindow`／`CNoticeWindow` 列、依據代號 PBW、統計重算、第 8 節兩列。
   - `protocol-gaps.md` 第 8 節：加上指向本文的連結。
   - 新增 `rea-scripts/t245/`。

## 怎麼驗證

- 欄位寬度與 3.81 寫法逐一對照：
  - 37 `cdcc`、45 `dss`、146 `scc`、169 `sb`(62)、117 `scsdd` 一致。
  - 83：`S_GlobalEffect` 只寫 1 byte，TW16 讀 D，不一致；已寫入建議。
- `gap_count.py` 重算統計：已有 58、部分 164、缺 67，需先查證 0。
- 版面段索引有兩處交叉吻合：`QuitWindow` 用第 0 段，`CNoticeWindow` 反組譯 `0x9090F0` 用第 25 段。
- 每輪 IDA 結束後 `subst` 都無輸出。
- 沒有改產品程式；只改 `docs/`。

## 未驗證什麼

- 全程靜態，沒有實機。83 的「震動／晃動」是依計數器與偏移表推定。
- `QuitWindow` 是否也由 ESC 或選單開啟，只看了第一層引用。
- 84 類型 3／4 的宿主物件、`$` 代換規則、190 圖示 512–514 的意義、192 的讀取者，都還沒查。
- `dword_153F598` 的寫入者沒有追；段索引對應 `uiextra86.xml` 是推定。
- P24Lab 的 `ClientLogin` 設定值沒讀（設定檔不可碰），所以不知道會不會收到 190／192／255。

## 產出路徑

- `docs/reference/packetbox-windows.md`（新）
- `docs/reference/INDEX.md`、`docs/reference/gap-matrix.md`、`docs/reference/protocol-gaps.md`（更新）
- `docs/reference/rea-scripts/t245/`（`ida_t245_rtti.py`、`queries.txt`、`strs.py`）
- `docs/reports/issue-602.md`
- `scratch/rea/`（IDB 副本、偽碼，不進 git）

## 給下一棒的提示

- **可開的 code 任務**（`packetbox-windows.md` 第 6 節）：

  | 任務 | 內容 | 優先／依賴 |
  |---|---|---|
  | PBW-A | 83／84 畫面效果與中央訊息 | 中 |
  | PBW-B | 原生離開窗 | 中，依賴 T190 |
  | PBW-C | 168／169 | 併入 W2-H |
  | PBW-D | 173 盟徽旗標 | 低 |
  | PBW-E | 37 寵物 AC | 低 |

- PBW-A 的 `$` 代換依賴 PG 任務 A（整張字串表）；沒有 A 時可先顯示原文。
- `gap-matrix.md` 第 4 節 `CCookWindow` 列的引擎欄仍寫 `InventoryUi.cs`，但 #559 已加 `Cooking.cs`，可順手更正。本輪沒改，因為不在範圍內。
- PG§2.2 把 66 算進「伺服器會送」，以呼叫點判定應排除；本輪只在 PBW 註明，沒有改 PG 的表。
- 重跑 IDA 時：
  - 用 `rea-scripts/t245/queries.txt` 的分段清單。
  - RTTI 要給 TD 位址（字串位址 −8）。
  - 執行前先跑 `subst` 確認代號沒被占用。
