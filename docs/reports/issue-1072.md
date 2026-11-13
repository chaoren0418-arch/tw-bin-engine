# Issue #1072 — T476 主控裁定：B15 已知限制 L1–L7 與決定 D1–D8

日期：2026-11-13。執行者：本機 Claude（claude-local，代寫文件）。只改文件，沒有建置、沒有執行任何程式。

## 裁定出處

本張的裁定不是本機執行者自己下的，而是照 #1072 上的兩則留言寫回文件：

- 調度器（雲端主導，代主控）裁定 L3–L7、D1–D8 與分母。
- **使用者裁定（2026-11-13）**：L1＝A、L2＝X。這兩項會改變或保留玩家看得到的行為，照專案規則交使用者決定，已經決定，所以本張**不用再標 `needs:user`**。

## 結論

| 項目 | 定案 |
|---|---|
| L1 EXP 門檻表 | **A**（使用者）：`OriginalExpTable.cs` 保留，是 B15 例外裡唯一剩下的一列；EXP% 顯示維持現狀 |
| L2 ground-catalog | **X**（使用者）：拿掉可丟棄白名單，由伺服器裁定；保留丟棄前確認視窗；伺服器模板值不得搬進程式 |
| L3 terrain 區域位元 | T470 標準：bounds、bit0–3 713 張全同；bit4–5 不符只能在 8 張內且不超過各自格數；bit6–7 全同 |
| L4 `maps[].name` | 拿掉 |
| L5 `spr_action` | 照 R1；不在客戶端加表 |
| L6 154 個特效 | 知悉，照樣重現 |
| L7 15 筆 profile | 照樣重現；gfx 138 放寬等 T470 通過後由 T475 開，現在不開 |
| D1 執行期 Sprite pin | 改三層驗證，新增 **T479**；不更新 pin、不還原客戶端 |
| D2 | (a) 照本機來源，T474 截圖；(b) 不疊加 LTtext，不開反組譯 |
| D3–D6 | 照 T452 的暫行做法定案 |
| D7 | 知悉 |
| D8 | **做**：T477（#1073）、T478（#1074）保留，不關閉 |
| 分母 | T476–T479 計入 M8：32 → 36，總計 410 → 414（U10） |

## 做了什麼

- `docs/design/asset-recipe.md`：第 7 節加總表，L1–L7 的「暫行做法」改成「定案」；第 8 節表格改成「定案與實作」欄；第 9 節 T456、T461、T464、T470、T471、T473、T474、T475 照裁定改，新增 T479 列；第 10 節加 T479 與分母；開頭讀法、3.3 的 Tile pin 說明同步。
- `TASKS.md`：M8 標題 36 項、驗收文字改成「只剩 `OriginalExpTable.cs` 一個」、加裁定摘要；T464、T471 標題改範圍；T476 改 done；新增 T479。
- `PLAN.md`：現況、M3 例外說明、U10（+36）、主線封頂（414）、M8 目標與裁定摘要；拿掉已完成的 T466 待決事項。
- `docs/plans/issue-1072.json`：一張 code 任務 T479（`b15-runtime-archive-verify`）。

## 怎麼驗證

- 三份文件用 grep 確認：`asset-recipe.md` 不再有未定案的「暫行」或 `needs:claude`；L1＝A、L2＝X、T479、414 在三份文件說法一致。
- 確認 T477（#1073）、T478（#1074）是 open，D8＝做，所以不關。
- 確認 `engine/tools/game-assets-allowlist.json` 現在 `b15Exceptions` 有 7 列（含 `OriginalExpTable.cs`、`ground-catalog.json`）。
- 確認 `engine/source/AssetPrep/` 已有 Sprite 的結構與錨點驗證程式（T454），T479 可以重用。

## 未驗證什麼

- D1：本機客戶端下產品執行期開 `Sprite03`–`Sprite14` 實際壞了哪些畫面，沒有驗證（不能跑程式）；交 T474。
- L2 定案後原版在送丟棄封包前的本機檢查沒有反組譯過（`rule-tables.md` 4.3），仍是待驗證。
- `TASKS.md` 裡 T444–T475 的 todo／done 狀態沒有逐張更新（不在本張範圍，T475 結案時同步）。

## 產出路徑

- `docs/design/asset-recipe.md`
- `TASKS.md`、`PLAN.md`
- `docs/plans/issue-1072.json`
- `docs/reports/issue-1072.md`（本檔）

分支：`task/1072`。

## 給下一棒的提示

- **T464（#1049）** 可以開工：只做 L2（ground-catalog），L1 只在 allowlist 寫理由，`OriginalExpTable.cs` 內容與 SHA 不動。
- **T471** 的目標是 `b15Exceptions` 只剩 1 列，例外機制要保留，不是移除。
- **T479** 由調度器依規劃檔建立；先讀 `AssetPrepInput.cs`、`SprShared.cs` 的驗證，抽成執行期也能用的共用元件，不要寫第二份。
- **T475** 結案時：依 T470 結果決定要不要開 gfx 138 放寬任務（L7）。
