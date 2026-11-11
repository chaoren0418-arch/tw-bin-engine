# Issue #1024 — T443：M7 結案覆核（gap-matrix 15.16）

**結論：M7 可以結案。** R13-A（#1021）的 ①②③ 都做到，1:144 改「已有」。實算：已有 **235**、部分 14、缺 41；K1 **55**、K2 **0**、K5 **0**、K7 **0**（290 列）。

## 做了什麼
- 讀 #1021 報告與合併後的產品碼（main `41b0fe2c`），逐項核對 15.15.5 的 ①②③，`檔案:行` 記在 gap-matrix 15.16.1：
  - ① 本機頁換頁：`NativeUi/UiClientBridge.cs:252–254`、`LocalOptionPage.cs:86–90`、`NpcInteraction.cs:106`、`:109`；點擊路徑 `NpcUi.cs:84` → `QueryOption`。
  - ② 本機動作：`NpcInteraction.cs:158`、`LocalOptionPage.cs:28–31`、`:47–59`、`:105–128`（accepttrade／getall／bubbleon／showMargetMsg，另加 showhpmp／showfloathpmp／dowhisper／dotradechat）。
  - ③ HP/MP：`NativeUi/UiClientBridge.cs:44` → `LocalOptionPage.cs:67–70`（`HyperTextOptionVars.HpMp`）。
- `docs/reference/gap-matrix.md` 新增 15.16（核對表、狀態變更表 1 列、統計、分類、去向、M7 判定、結案摘要最終版、限制）。
- 新增 `docs/reference/rea-scripts/gap_count_r11.py`（`--round 9|10|11`，預設 11）；`gap_count_r10.py` 改成讀到 15.16 前就停（一行）。
- `docs/testing/smoke-m7.md` §15.3 改成可測（SM16 附加項展開成各頁步驟與判定），§15.4 記一行。

## 怎麼驗證
- `py -3 -I docs/reference/rea-scripts/gap_count_r11.py docs/reference/gap-matrix.md` → 235／14／41，K1 55（無 K5、K7）。
- `--round 10`（r11 與 r10 兩支）→ 234／15／41，K1 55、K5 1，和 15.15.3 相同。
- `gap_count_r9.py` → 含預期 230／19／41、不算預期 229／20／41，K1 55、K5 5、K7 1，和 15.14 相同。

## 未驗證什麼
- 沒有建置或執行程式；PR #1022 的 CI 結果引用 #1021 報告，沒有重跑。
- 「已有」只是靜態核對，全部沒有真機驗收；SM16 附加項（§15.3）待 P24Lab 測試角色執行。
- 沒有開 IDA／PakViewer（本輪沒有「缺資源」宣稱）；沒有啟動遊戲、伺服器或 DB。

## 產出路徑
- `docs/reference/gap-matrix.md`（15.16）
- `docs/reference/rea-scripts/gap_count_r11.py`（新）、`gap_count_r10.py`（一行）
- `docs/testing/smoke-m7.md`（§15.3、§15.4）
- `docs/reports/issue-1024.md`（本檔）

## 給下一棒的提示
- #1023（主控結案）：結案摘要直接用 gap-matrix 15.16.5 的引用區塊，數字已是實算。
- 真機 smoke（59 項，含 §15.3）建議和 M7 結案一起排給 P24Lab。
- 之後若再盤點，新增 15.17 時記得讓 `gap_count_r11.py` 讀到 15.17 前就停（同本次對 r10 的處理）。
