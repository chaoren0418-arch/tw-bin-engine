# Issue #417：盤點本機逆向資源並二度分析

分支 `task/417`。完整內容見 [docs/reference/rea-inventory.md](../reference/rea-inventory.md)。

## 結論

- **B07：** 「Themida 靜態不可得」的受阻理由只適用 `LiTo.bin`。歷史地址屬於未加殼的 `TW1608262200.bin`，parser、frame 結構、音效選擇、分類讀取者、dispatch tables 都已讀到。S87 同身份入口仍未定位。
- **B04：** `<`、`[`、`^` 語義已取得。`^` 占地圖事件大宗（111 個 id、8,003 筆），產品未實作。
- **B03：** 原生鏈完整，但本機全部 `.s32` 的延伸記錄為 0 筆，建議結案。
- **產品差異：** `CombatSfx.cs` 略過 `<`，腳步聲等無聲。

## 與任務指示的偏差

**沒有使用 rea-main。** 實測跑不起來：未建置、Node v24.20.0 不符 `^24.22.1`（`engine-strict=true`）、沒有可用的 native provider。工具目錄禁止改動，所以沒有建置。改用 pefile、Capstone，以及 P01 IDB 副本的 Hex-Rays。

**讀取範圍超出兩個來源資料夾，全部唯讀：** P01 的 IDB、`0-客戶端/map`、`0-客戶端/TW13081901.txt`、`lito登入器/舊版分析與驗收/runtime/LiTo.unpacked-oep-current.exe`、`lito脫殼資源/`。

**任務未要求而新增：** `work/issue-417/`。`scratch/` 會隨 worktree 清除，所以把腳本與小型結果放這裡，供重跑核對。內容不含 BIN、dump 或大段反組譯。

## 做了什麼

1. 盤點兩個資料夾，對照 repo 的 `docs/`、`work/`、`主管/`、根目錄 P01–P24 與 `lito脫殼資源`。
2. PE 分診五個樣本。
3. 讀 `TW1608262200.bin` 的 dispatch tables，分類寫入者與讀取者。
4. 用 IDA 7.7 反編譯約 50 個函式，輸出在 `scratch/rea/ida/`。
5. 全量掃描 7,807 個 `.s32` 與 330 個 `.seg` 的特效尾段。
6. 跨版本比對 parser。

## 怎麼驗證

- 地址換算用 #47／#48 的三筆歷史 File Offset 核對，全部相符。
- `TW13081901.unpacked.exe` 的 parser 常數完全一致（`0x1951`、`0x2B`、`0x1812`、模式 0/1/2、旗標 1/2/4/8/0x10）。
- `LiTo.unpacked-oep-current.exe` 在 File Offset `0x3E85C` 一帶有相同的 xor 與符號比較。
- `#6016` 的 token `<3332<1340<1340` 與 #145 的 ID 相符，佐證 `<` 是聲音。
- 結束時重算 `TW1608262200.bin` SHA 相同；兩個來源資料夾與 `rea-main` 最新修改時間都早於本輪。

過程中修正的錯誤：第一版 xref 分類把 `8b 0d` 切開解成假指令，已改用 opcode 判斷；第一版 frame 掃描被資料區截斷，已改 skipdata 重掃；coverage 搜尋兩度回空不可信（大括號 glob 失效；Grep 遵守 `.gitignore`，跳過未追蹤的 `P*/`），改用 `Select-String` 重查後多找到 P10 一筆引用。之後要查 `P*/`，不要用 Grep 工具。

## 未驗證什麼

- 全程靜態，沒有聽音或實機。`^` 為循環音是依 Miles API 慣例推測。
- LiTo 只驗證 parser 窗口，消費端未確認。
- `!`、`]` 的語義是推測；`>`、`{`、`}` 未找到消費者，但掃描樣式有限。
- 地圖 actor 走 `A43560 → A3EF40` 是推測。
- `A4E5D0` 第 7 參數語義未解。
- `.zip` 未解壓比對；`Lin.bin`、`LinR` 只做分診與指紋。

## 產出路徑

- `docs/reference/rea-inventory.md`
- `docs/reports/issue-417.md`
- `work/issue-417/`（腳本與小型結果）
- `scratch/rea/`（偽碼與 IDB 副本，不進 git，隨 worktree 清除）

## 給下一棒的提示

- 主控可依 `rea-inventory.md` 的「對各專案的建議任務」開任務。優先順序建議：`CombatSfx` 的 `<`、裁定 B03、`^` 環境音。
- 重跑反編譯：把 P01 IDB 複製成 `scratch/rea/ida/tw16.idb`，建立 `scratch/rea/ida/env/{user,appdata,local,temp}`，用 `docs/reference/rea-scripts/run_ida.ps1`。腳本要放在 `scratch/rea/scripts/`。IDA 在中文路徑會失敗，腳本用 `subst R:` 映射。**不要用 `Select-Object -First` 截斷它的輸出**，否則 `finally` 跑不到，`R:` 會殘留。
- `TASKS.md`、`PLAN.md`、`docs/demo/README.md` 的 B03／B07 受阻理由本輪沒改，由主控決定。
- `hook_capture.py`、`gree_path_probe.py`、`scan_broadcast.py` 會碰活程序，不得執行。
- 要用 REA 需使用者決定：升級 Node、安裝 Ghidra 12.x＋JDK 21、同意建置。
