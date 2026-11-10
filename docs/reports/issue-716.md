# Issue #716 報告：T304 查證 Q4-3 到 Q4-9

## 做了什麼

- 用 IDA 7.7＋P01 IDB 副本（`scratch/rea/ida/tw16.idb`，SHA `3a0bf206…e82f`）唯讀批次查了 10 輪（subst `W:`，前後檢查都沒有占用與殘留），另用 `wo_sendsites.py` 與新寫的 `t716_callscan.py` 做送出點與線性掃描。
- 產出 [`docs/reference/followups-6.md`](../reference/followups-6.md)（FU6），每項寫「原版怎麼做／3.81 是否支援／要改哪個檔案」，地址都附 VA／FO。
- 回填 `gap-matrix.md` 第 13 節：13.7 下加回填說明，新增 **13.9** 結果表；`docs/reference/INDEX.md` 加 FU6 一列。
- 腳本與查詢清單：`docs/reference/rea-scripts/t716/`（`queries.txt`、`t716_callscan.py`）。

## 結論摘要

| 代號 | 結論 |
|---|---|
| Q4-3 | op 201：0＝新增（血盟窗 `AddFocusPledge`＋輸入框 3361、HTML `pledgefocusadd`、`/focus 名稱`），1＝刪除（`DelFocusPledge`，每個勾選列各一包），2＝取清單（只有 `/focus` 不帶參數）；和 3.81 `C_ClanAttention` 一致 |
| Q4-4 | `byte_1542AA1`＝選角清單第 3 欄（職業），進入世界時寫入；遊戲中沒有封包寫它 |
| Q4-5 | S84 印在聊天欄（7 種字串）；C_WAR 入口＝`/war`、`/surrender`、`/cease`（含中文別名）、血盟窗 `ChangeWarState`、HTML `declarewar`／`surrender`／`cease`；原版沒有獨立盟戰窗 |
| Q4-6 | HTML `propose`／`divorce` 與 `/propose`、`/divorce` 送 `cc` 0／1（沒有目標欄位）；3.81 範本沒有這兩個連結 |
| Q4-7 | `AgitWindow` 建立後從未打開（死窗），與 `AgitMapWindow` 互不相關 |
| Q4-8 | `$n` 查 `desc-c.tbl`（引擎的 `names.json` 是同一張表） |
| Q4-9 | `WorldMapWindow`／`VillageMapWindow` 從未建立，原版看不到世界／村莊地圖 |

## 怎麼驗證

- 反編譯與反組譯（Hex-Rays），交叉引用與全程式寫入掃描（`gstores`），送出點掃描，`.text` 線性掃描（`t716_callscan.py` 對 `dword_13A96EC` 的結果 7 筆，與 IDA xref 一致）。
- 3.81 伺服器原始碼對照（`C_ClanAttention`、`C_War`、`C_Propose`、`S_War`、`S_NpcChat*`）。
- 字串用 `t680_strings.py` 從 `Text.pak` 在記憶體解出 `string-c.tbl`。
- `gap_count_r5.py` 在改動前後輸出相同（新增的 13.9 不影響第 14 節統計）。

## 未驗證什麼

- 全部是靜態分析，沒有實機驗證。
- 刪除關注的確認字串 4118 本機表沒有；S84 切的背景音樂 11／12／13 是哪一首；`sub_7DC850` 第 4、5 參數；S161 的 `^`／`!` 處理；野外地圖連結清單跳世界／村莊地圖時會不會當掉。

## 產出路徑

- `docs/reference/followups-6.md`
- `docs/reference/gap-matrix.md`（13.7 回填說明、13.9）
- `docs/reference/INDEX.md`
- `docs/reference/rea-scripts/t716/`
- 分支 `task/716`

## 給下一棒的提示

- 可以直接開 code 任務的：Q4-4（`BlindClassBonus`／`NightVision` 用 `LoginCharacter.Class` 2／4）、Q4-8（S161 展開 `$n`）、Q4-3（R4-E 補 C129 三個送出）、Q4-6（補離婚、指令入口）。
- Q4-5 的指令、Q4-3 的 `/focus`、Q4-6 的 `/propose`／`/divorce` 都要改 `PlayerChat.cs`；T290 已合併（PR #744），可以一起排成一張 code 任務。Q4-8 可直接用 T290 新增的 `TextArchive.ReadDesc()` 查 `desc-c.tbl`。
- Q4-7、Q4-9 的結論是「原版不顯示」，下一輪盤點把 `AgitWindow`、世界／村莊地圖改成「不建議」，R5-H 的 Q4-9 依賴可以解除。
