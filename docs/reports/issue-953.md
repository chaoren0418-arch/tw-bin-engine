# Issue #953 — T413：`--classic-ui` 備援盤點（resource，第 1 次）

## 做了什麼
- 靜態盤點全部 `[UiPanel]` 自動發現的面板，共 58 個成員、60 個註冊，另有 FieldMap 由 MiniMap 加入，合計 61 個；公告板動態面板另列。每個面板都記錄：
  - 有沒有實作 `IUiClosablePanel`；
  - `--classic-ui` 下的 WinForms 入口（`檔:行`）與怎麼點到；
  - 入口是否可達；
  - 原生 UI 開著時是否和 WinForms 重複顯示。
- 列出只有原生的面板（18 個註冊）和 19 項缺口（G1–G19），每項附建議修正（檔案＋目標），並分成 4 組 code 任務（CU-1–CU-4）。
- 在 `docs/testing/smoke-m7.md` §10：
  - 新增 SM57「classic 備援入口逐一點開」；
  - SM54 補看 G1（死亡窗被搬進開發工具）；
  - §12 結果表加一列。
- `docs/reference/INDEX.md` 加一列。

## 怎麼驗證
- 只讀程式碼（grep、讀檔、`git log -S`／`-G`），以 main `3528e1c2` 為準。
- 盤點時分三組平行閱讀，主要結論再抽查原始碼，都對得上：
  - G1：`Program.cs:115` 的初始化順序，加上 `DevToolsForm.cs:84` 會搬走主視窗控制項，再加上 `Recovery.cs:425`；
  - G3：`PersonalShopIntegration.cs:441,448`；
  - G13：`BoardUi.cs:41–42`；
  - G14：`NpcUi.cs:68`；
  - G12：`LocationShare.cs:343–352`；
  - G6：`MenuButtonsPanel.cs:17–18` 的註解；
  - G16：`SummonFallbackUi.cs:67`；
  - G4：`SpellExchangeUi.cs:10`。
- 沒有建置，沒有執行任何程式、遊戲、伺服器或 DB；沒有改產品程式。

## 未驗證什麼
- G1、G7、G12、G15 是從程式碼推出來的，要真機確認（SM54、SM57）。
- AuctionInfo 的 Bid 在 classic 是否有 HTML 連結、動作窗的隊長踢人與選對象送出在 classic 有沒有替代，都沒有逐項確認。

## 產出路徑
- `docs/reference/classic-ui-audit.md`（主文件）
- `docs/testing/smoke-m7.md`（§10 SM54 補看、SM57；§12 一列）
- `docs/reference/INDEX.md`（一列）
- 分支 `task/953`

## 給下一棒的提示
- 建議依 §4 開 4 張 code 任務：
  - CU-1 優先，G1 會讓 SM54 的 classic 死亡窗看不到；
  - CU-2 可再拆；
  - CU-3、CU-4 優先度低。
- 這些任務都不必改 `Program.cs`、註冊表或 lock 檔。
- §2.1 標「不補」的只有原生項目，請下一輪 gap-matrix 註記「classic 不提供」。
- 真機：C 輪照 SM57 逐一點開；A 輪補看 G12、G13。
