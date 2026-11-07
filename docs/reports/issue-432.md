# #432 T164：已完成 M6 功能的原版對照修正清單

## 做了什麼

- 新增 [`docs/reference/m6-native-diff.md`](../reference/m6-native-diff.md)：
  - **第 1 節：** T100–T153 一功能一列，欄位是「T 編號／功能／原版來源／引擎現況／一致？／修正建議」。
  - **第 2 節：** 統計。
  - **第 3 節：** 修正任務分組 G1–G10，每組一行，寫目標、檔案、驗收。
- `docs/reference/INDEX.md`：T164 列補上連結與狀態；T160、T162、T163 三列從「PR 待合併」改成「已合併」。

## 結果

已合併的 M6 功能共 50 個（T100–T153，扣掉 T112、T124、T135、T151）：

| 結果 | 數量 |
|---|---:|
| 已查證，有差異 | **16** |
| 已查證，一致 | 1（T103 地面圖） |
| 純協定，#132 已對齊 | 9 |
| 原版表現未查證 | 24 |

16 個有差異的功能：T105、T106、T108、T125、T128、T129、T132、T137、T138、T146、T147、T148、T149、T150、T152、T153。
- 其中 5 個只有 UI 版面差異（T106、T108、T125、T138、T150），已由 #448 排定修正。
- 其餘 11 個需要新開 G1–G8。

## 怎麼驗證

- 讀過 `INDEX.md`、`rea-inventory.md`、`actions-and-names.md`、`ui-layout.md`、`ambience-and-map-events.md`、`sound.md`、`server-rules.md` 的差異表，以及 #448 的 `native-ui.md` 計畫清單。
- 在 main `c751a86b` 抽查文件中的引擎事實，大多仍成立（行號可能有偏移）：
  - `Ambience.cs` 的 `0x081020` 與粒子上限 768
  - `PlayerHudChat.cs` 的具名色
  - `TitleAndEmote.cs` 的 LightYellow／JhengHei
  - `SoundPackets.cs` 的 repeat
  - `MinimapUi.cs` 的 F5
  - `DuelUi.cs` 的「決鬥對手」
- 兩處已和查證文件不同：
  - T165（#443）已合併，修好音效差異 1、3，以及 4 的戰鬥路徑。
  - T146（#424）已合併。實際比對是 `ClanWar.cs` 畫橘紅色「名稱 [血盟]」，原版不畫。
- 用 `git log` 確認 T124（#258）、T135（#316）、T151（#414）還沒有合併進 main。
- 「未查證」的視窗線索，是列出 #448 在 `scratch/task448-uixml/` 解出的 XML 檔名與 `uiextra86-c.xml` 的視窗標籤。這一步唯讀，沒有把 XML 放進 git。

## 未驗證什麼

- 沒有實機觀察；原版行為以 TW16 靜態分析為準，LiTo 3.81 是否相同沒有確認。
- 引擎現況主要沿用查證文件的描述，只抽查，沒有逐行重讀。G1–G9 開工前，執行者要以當下的 main 重新確認。
- 24 個未查證的功能，只寫了還需要查什麼，沒有查。
- 沒有改產品程式，沒有建置、啟動遊戲、伺服器或 DB。

## 產出路徑

- `docs/reference/m6-native-diff.md`
- `docs/reference/INDEX.md`
- `docs/reports/issue-432.md`
- 分支：`task/432`

## 給下一棒的提示

- 主控可以依 `m6-native-diff.md` 第 3 節開任務。建議順序：
  1. G1（名稱與稱號）、G7（異常狀態外觀）、G8（音效封包與播放規則）：差異已完全查證，可以直接開 code。
  2. G2、G3、G6。
  3. G5：缺 `source.lmp` 內容，可以先做時鐘與曲線。
  4. G4：等 T135 合併。
  5. G9、G10：先開 resource。G10 分 3 張：技能表現、視窗類、世界物件類。
- UI 版面差異不要重開任務，直接沿用 #448 的 `ui-*` 任務。
- G4 的 `Emblem%d.dat` 磁碟快取寫在哪裡，要先裁定（AGENTS 規則 1）。
- `TASKS.md` 的狀態落後：T165 已合併但仍標 todo；T160–T163 已合併但仍標 todo；T146 已合併但標 doing。這不在本任務範圍，沒有改。
- T112（M6 smoke）依 #427，要等 G1–G8 合併後才開。
