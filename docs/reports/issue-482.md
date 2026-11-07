# Issue #482 — T180：查證原版技能與法術表現（G10a）

## 做了什麼
- 以 IDA 7.7＋P01 IDB 的 scratch 副本（SHA `3a0bf206…7e82f`）唯讀分析 `TW1608262200.bin`（SHA `e12949cf…09641a2`），查 T100、T101、T113–T119。
- 產出 [`docs/reference/skills-visual.md`](../reference/skills-visual.md)：
  - 送 C6（TW16 opcode 216）的 10 個施放函式都**不在本地播動作**；冷卻＝自身外型動作 18／19 的 list.spr 影格總長×速度倍率＋技能額外延遲。
  - S30／S42 的特效在施法者 `!` 格才啟動，受擊在特效的 `!` 格；list.spr 欄位 109 的 kind 決定類別（3：30 ms 飛行；4／5：60 ms；7：逐格推進；8：S30 不畫）。
  - S42 預設類別只放一個特效在封包 x,y，而且**只有第一個目標受擊**（反組譯 `0x8AC26E` `imul eax, 0`，原版缺陷）。
  - 箭矢：伺服器預設 66／2349／2989；kind 3 類別不讀 param，所以 2349 原版不畫 219。
  - 引擎差異 14 項（SV#1–SV#14）：❌ 8、⚠️ 2、✅ 3、❓ 2。
- 回填 `m6-native-diff.md`：T100、T101、T113–T119 三列改成已查證，G10(a) 標完成，新增修正組 G12（main 已用 G11 給原生視窗模組）。同步 main 時順手修復 #484 寫壞成「?」的第 2 節統計表，重算為有差異 39／協定 9／未查證 2（T131、T133）。
- 更新 `INDEX.md`；新增腳本 `docs/reference/rea-scripts/t180/`（`run.ps1` 加 `-Drive` 參數，避免與其他查證任務的 subst 互撞）。

## 怎麼驗證
- 每個結論都有反編譯或反組譯的 VA／FO；`queries.txt` 可逐行重跑（輸出在 `scratch/rea/out/`，未進 git）。
- 執行前檢查 `subst`：`W:` 被 task-484 占用，本輪改用 `K:`，每次執行後都解除。
- list.spr 109 分布用 `listspr_109.py` 從客戶端 `Text.pak` 唯讀解出，只寫摘要 JSON 到 scratch。
- 引擎現況對照 main `231f1df1` 的原始碼行號。

## 未驗證什麼
- 全是靜態分析，沒有實機；原版是 TW16，LiTo 3.81 是否相同無法證明。
- 技能資料 `+56`／`+60` 的來源、選目標游標 179、kind 6／7 的細節、各技能實際 gfx（在伺服器 DB，本輪不查）。
- 沒有改產品程式；沒有啟動遊戲、伺服器或 DB；沒有複製遊戲資源。

## 產出路徑
- `docs/reference/skills-visual.md`、`docs/reference/m6-native-diff.md`、`docs/reference/INDEX.md`、`docs/reference/rea-scripts/t180/`、本報告。

## 給下一棒的提示
- G12 可以直接開 code 的部分：SV#5–#8、#10（`MagicRendering.cs`、`AttackSkillPackets.cs`、`ProjectileRendering.cs`）。
- **要使用者裁定**：(1) 拿掉送出時的預測施法動作（SV#1，照原版會多一個往返延遲）；(2) 「S42 只有第一個目標受擊」是否照抄原版缺陷。
- SV#12（type 8 的 +5 提亮）併入 G5；SV#3（選目標游標）併入 #448。
- 跑 IDA 前先 `subst` 看代號，`run.ps1 -Drive` 換一個空的。
