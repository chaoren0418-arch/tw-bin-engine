# Issue #680 報告：T287 查證原版——第三輪細項

## 做了什麼

- 用 IDA 7.7 唯讀分析 TW16（P01 IDB 的 scratch 副本，10 輪）與 TW13（#460 IDB 的 scratch 副本，16 輪），查 gap-matrix 12.6 的 Q3-1、Q3-3、Q3-4、Q3-5、Q3-6、Q3-8、Q3-9。
- 產出 `docs/reference/followups-4.md`（FU4）。回填 `gap-matrix.md` 第 12 節：12.6 末加結果指引，新增 12.8 結果表與新待查 Q4-1、Q4-2。`docs/reference/INDEX.md` 加一列。腳本與查詢清單放在 `docs/reference/rea-scripts/t680/`。

## 結論摘要

| 代號 | 結論 | 程度 |
|---|---|---|
| Q3-1 | Tab／Ctrl+I 背包、Ctrl+S 魔法、Ctrl+A 狀態、Ctrl+H 說明、Ctrl+V `option1`、Ctrl+C `option2`、Ctrl+Q／Alt+X 統計窗；倉庫沒有熱鍵；選單鈕 13 地圖、1 信件、2 狀態、3 魔法、4 背包、5 `option2`、6 `option1`、7 統計窗（不是登出） | 已驗證（TW13／TW16）；3.81 推測同 TW13 |
| Q3-3 | `sub_81B0E0`＝16 位元色彩；減半只在 32 位元 → 全振幅正確 | 已驗證（意義依呼叫者推定） |
| Q3-4 | 觸發條件是 `Effect` `+9 ≠ 0` 且 `+12 == 1／2`；觸發封包沒找到 | 部分；待驗證（Q4-1） |
| Q3-5 | 例外只有隊伍（沒有血盟）；`$431` 要 S36 類型 3，3.81 不送 | 已驗證 |
| Q3-6 | `HYPER_TEXT_WINDOW` 版面＋寫死 9183／標題列；3 行格式與字串 1880–1889；類型 0 存放、1 領出 | 已驗證（surf 清單對應推定） |
| Q3-8 | 出現→SPAWN、S_ACTION 動作 8→DEATH、SPAWN 播完且頭目在→BATTLE | 已驗證 |
| Q3-9 | 第二色沒有被讀，畫面沒有用途 | 已驗證 |

## 怎麼驗證

- 雜湊：BIN SHA-256 `e12949cf…41a2`；TW16 IDB 複製前後 SHA-256 `3a0bf206…e82f`；TW13 IDB 複製前後 `34af6329…1ad1`，對應 `tw13.exe` `653da514…5c62a4`。
- 每輪執行前後都跑 `subst`，代號 `W:` 沒有被占用，結束後沒有殘留。
- 字串：`string-c.tbl` 1880–1889、`desc-c.tbl` 4／431，以及 HTML `help`／`option1`／`option2`／`option2p`，都從本機 `Text.pak` 在記憶體中讀取。
- 對照 3.81 伺服器原始碼：`S_Ability` 的類型、`S_UseAttackSkill`／`S_UseArrowSkill` 的 byte、`S_PledgeWarehouseHistory`。

## 未驗證什麼

- 沒有實機，也沒有啟動遊戲、伺服器或 DB；沒有建置或執行任何程式；沒有改產品程式。
- 3.81 `LiTo.bin` 有殼，熱鍵與選單以 TW13 為準是推測。
- 其餘未驗證項目：Q3-4 的觸發封包；物品 `+36 == 87`；surf 9183 本機有沒有；`sub_F7D5C0`、`sub_EFE890`；TW16 Ctrl+H／V／C 的目標函式。詳見 FU4§9。

## 產出路徑

- `docs/reference/followups-4.md`
- `docs/reference/gap-matrix.md`（12.6 末、12.8）
- `docs/reference/INDEX.md`
- `docs/reference/rea-scripts/t680/`

## 給下一棒的提示

- R3-A：action 7 現在是 `Logout`，原版開的是 `CUserPlayStatsUI`。倉庫不要加熱鍵。
- R3-H：`InvisiblePeerVisible` 拿掉血盟；`LiquorFullWave` 不用改值；光效 +5 先不接。
- R3-C：`ClanWarehouseHistory.cs` 的「0／1 推定」已確認是 0＝存放、1＝領出。
- R3-D：頭目曲的事件是物件出現與 S158 動作 8，不是進入區域。
- 下一輪查證（Q3-2、Q3-7）時順便查 Q4-1（全程式掃 `Effect` `+12` 的寫入、vtable `0x1150D4C` 的類別）和 Q4-2。
