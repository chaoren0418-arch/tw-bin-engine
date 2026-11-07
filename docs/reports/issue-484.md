# Issue #484 報告：T182 查證原版世界物件類功能（G10c）

## 結論

五個功能都已找到原版來源。封包欄位都與引擎相容，**畫面與操作流程有多處不一致**：合計 13 項不一致、5 項部分一致、7 項一致、5 項無法判定。詳見 `docs/reference/world-objects.md`。

- **T103 拾取：**
  - 原版只在 1 格內送 `C_GET`，距離較遠會先自動走過去。
  - 單個物品或「全部拾取」選項開著時送整疊；否則跳出數量提示，空白就是整疊。
  - 送出前本機先轉向；節流 986 ms（旗標時 600 ms），未過就丟棄。
  - 引擎：3 格內才送、數量預設 1、收到回報才轉向。
- **T136 門：** 原版同樣要先走到 1 格內才送 `C_OPEN`；引擎任何距離都直接送。
- **T143 幽靈：**
  - 原版本機擋拾取、F 鍵瞬移、快捷與自動攻擊，門與 NPC 仍可點。
  - 離開是 HyperText 動作 `exitghost`（推測是 NPC 對話連結），送 `C_EXIT_GHOST` 無欄位。
  - 引擎：HUD 常駐按鈕，本機不擋任何操作。
- **T144 座標分享：**
  - 欄位逐 byte 相同。
  - 原版從地圖視窗 `TransferMenu` 開輸入列提示；第 4 欄是 MapSystem 地區 id（推測），引擎固定送 0。
  - 收到時顯示系統訊息 1784（寄件者＋地區名稱），不是數字座標。
- **T151 復活與死亡：**
  - 原版死亡時顯示 `UserPlayStatsUI`（重新開始／結束／取消＋統計），約 8 分 21 秒後自動 `C_QUIT`。
  - 復活確認 321／322 放在**聊天輸入列**，打 Y／N 加 Enter 回答；回覆格式與 #414 一致。
  - S241 移動鎖定在 TW16 沒有找到對應。
- **新增工具：** TW16 **送出端封包名稱表**（`sub_951F80`），可以把任何送出 opcode 對到 `C_*` 名稱。

## 做了什麼

- **準備分析環境：**
  - 把 P01 IDB 複製到 `scratch/rea/ida/tw16.idb`，SHA `3a0bf206…7e82f`。
  - 核對 BIN，SHA `e12949cf…641a2`。
  - 用 IDA 7.7＋Hex-Rays 唯讀查詢 11 輪。subst 磁碟用 `W:`，每輪執行前確認未被占用；同時段 `Q:`、`R:` 由 task-460 使用，沒有碰。
- **反組譯核對：** 用 pefile／Capstone 核對壓棧順序、距離判定的 `this`、計時器處理，並找 RTTI／vtable、事件表與送出點。
- **讀 UI XML：** 用 `ui_xml_decrypt.py` 把 65 個 UI XML 解到 `scratch/rea/uixml/`，對照了 `UserPlayUI.xml`、`Actions.xml`、`MapSystem.xml`。
- **對照伺服器來源：** 唯讀查看 `S_Message_YN`、`C_Attr` 的 321／322 分支。
- **對照引擎：** 讀了 `Ground*.cs`、`PickupPresentation.cs`、`Doors.cs`、`GhostMode.cs`、`LocationShare.cs`、`Recovery.cs`，以及未合併的 `task/414` 的 `Resurrection.cs`、`docs/protocol/resurrection.md`。
- **文件：**
  - 新增 `docs/reference/world-objects.md`。
  - 更新 `INDEX.md`。
  - 回填 `m6-native-diff.md` 第 1 節的 T103、T136、T143、T144、T151，以及第 2 節的統計。合併 main（含 G10b／#483）後，有差異 26→30、一致 1→0、未查證 14→11。在 T141 列補註：Yes／No 的視覺是輸入列提示。
- **腳本：** 保存 7 支可重跑腳本與查詢清單（`rea-scripts/wo_*`）。

## 怎麼驗證

- **地址：** File Offset 都用腳本依 PE 區段表計算，bss 另外標明。
- **關鍵結論都用反組譯核對過：**
  - `sub_500E40` 的 `this` 是自身座標。
  - 座標分享送出的壓棧順序是 H 地圖、D xy、D 區域。
  - 接收 `shdd` 的落點。
  - 計時器 0x29A 到期時送 `C_QUIT`。
- **送出端名稱表：** 用已知對應交叉檢查：#428 的 `C_ACTION`=50、`C_EMBLEM`=28，以及本輪的 23→`C_GET`、131→`C_OPEN` 都與欄位語義吻合。最初漏了「減 3」偏移時名稱明顯錯亂，修正後才採用。
- **區域 id 的推測：** 依據是 `MapSystem.xml` 的 ID 範圍註解（0–99／100–999）與程式以 100 分界查兩張表。
- **唯讀：** `git status` 只有文件與腳本；沒有遊戲資源、XML 或 IDB 進 git。

## 未驗證什麼

- 字串表 121／321／322／1784／1785／2139 的實際文字。字串表在 `Text.pak`，已加密。
- 幽靈的繪製效果。物件 `+88` 與自身幽靈都沒追到讀取者。
- `exitghost` 連結在哪一頁 HTML。
- 門類別 vtable 如何處理動作 28／29。
- `byte_13CE51E == 1` 的語義。
- `0x10000` 旗標的意義。
- 座標分享 10 筆清單在哪個 UI 顯示、能否點擊。
- S241 在 TW16 的對應。
- 沒有啟動遊戲做真機比對。

## 產出路徑

- `docs/reference/world-objects.md`（新）
- `docs/reference/INDEX.md`、`docs/reference/m6-native-diff.md`
- `docs/reference/rea-scripts/`：`wo_copnames.py`、`wo_sendsites.py`、`wo_callers.py`、`wo_dataref.py`、`wo_immscan.py`、`wo_dis.py`、`wo_xref.py`、`wo_queries.json`
- 本報告 `docs/reports/issue-484.md`
- 未進 git，只在本機 `scratch/rea/`：IDB 副本、IDA 輸出、解密 XML

## 給下一棒的提示

- 依 `world-objects.md` 第 8 節開 code 任務，建議順序如下：
  1. 拾取與門的「自動走近」，兩者共用一套。
  2. 拾取數量與節流。
  3. 幽靈的本機限制。
  4. 座標分享的地區 id 與顯示。
  5. 死亡視窗改成 `UserPlayStatsUI`。
- **#414（T151）合併前**，建議先把復活確認改成聊天輸入列的 Y／N 提示。封包格式不用改。
- 字串 1784／1785／321 的原文需要先解 `Text.pak`，可另開一張 resource 任務。
- 之後查 TW16 送出端時，直接用 `wo_copnames.py` 與 `wo_sendsites.py`。
- **真機驗收時請順便確認：**
  1. 點遠處物品或門時，角色會不會自動走過去。
  2. 撿一疊物品時會不會跳數量提示。
  3. 死亡後出現的是不是「重新開始／結束／取消」視窗。
  4. 被復活時，確認文字是不是出現在聊天輸入列。
