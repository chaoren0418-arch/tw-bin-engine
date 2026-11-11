# Issue #944 — T410：真機 smoke 腳本整合（#475、T112、M1–M40）

階段：resource，第 1 次嘗試；分支 `task/944`。

## 做了什麼
- 新增 `docs/testing/smoke-m7.md`：把 #475（原生 UI smoke）、T112（M6 驗收 11 條，加上 M5 剩下的 M2 列）、gap-matrix 15.12.7 的 M1–M40（扣 M36、M38）合成一份腳本，共 **56 項（SM01–SM56）**。
  - 順序：啟動 → 登入 → 選角／建角 → 世界常駐畫面 → 各視窗 → 戰鬥 → 道具 → 魔法 → 換圖／聲音／聊天指令 → classic-ui 小段與登出。
  - 每一項都有操作步驟、預期（附依據：FU 章節、gap-matrix 列、R10 PR）、要截的圖、`native-ui.log` 關鍵字（或標明是 `window-events.json` 的 events）。
  - 判定：🔴 必須真機判定 **45 項**，🟢 已靜態確認、只需對照 **11 項**；每一段裡 🔴 排前面。
  - 附空白結果表（項目／判定／來源／結果／截圖檔名／備註），還有「三份清單 → SM 編號」對照，確認沒有漏項。
  - 環境只寫 P24Lab（`D:\P24Lab-ClientEngine`、42061、43324），不碰 2000／3306；GM 準備照 #160／#175 例外。
- `檔案:行` 先用 main `d4ba59a3` 逐條核對（兩個唯讀子代理分 M1–M20、M21–M40；啟動相關的部分我自己看）。同步 main `b6ebd74b`（#943 R10-I、#945 召喚窗）後，我又把 `ObjectRendering.cs`、`SummonPanel.cs`、`RingWindows.cs` 的引用重核一次：
  - 約 30 處行號移位（同步後多了 R10-I／#924 造成的幾處），1 處對錯目標（M14 的 180 startMsg 不在 `PacketBoxScreen.cs:24–28`，在 `PacketBoxStatus.cs:323`）。
  - 9 個代號的行為已被 R10 PR 改掉（M3、M7、M23③、M32、M33、M35、M37、M39、M40）。
  - 以上都寫在腳本 §11「已知差異」，腳本內容照新行為寫。

## 重要發現（影響 smoke 跑法）
- **K-1**：launcher 只接受 `--check-only`（`engine/_integration/dev/launcher-source/Program.cs:18`），`啟動引擎.cmd --evidence …` 不能用。
- **K-2**：`--normal-product` 不接受其他參數（`ProductEntry.cs:7`），所以 `--evidence` 只能在診斷入口用。診斷入口原生 UI 預設關，聊天輸入列也打不開（`PlayerChatKeyboardFlow.cs:12–13`）。
- 所以腳本拆成三輪：
  - A 主輪：一般產品，沒有 log。
  - B 證據輪：`P11.ClientEngine.exe --evidence <dir> --native-ui`，只跑啟動、登入、選角和按鈕開窗。
  - C 輪：`--normal-product --classic-ui`。

## 怎麼驗證
- 已驗證（靜態）：腳本引用的 `檔案:行` 都在 main 對過（`d4ba59a3`；同步後改到的三個檔案用 `b6ebd74b` 重核）。啟動相關的部分是我自己讀的：`Program.cs`、`ProductEntry.cs`、`UiRoot.cs`、`UiClientBridge.cs`、`ClientArchive.cs`、launcher。
- 已驗證（靜態）：M1–M40 的 log 關鍵字都走 `UiResourceLog.Write`，會寫進 `native-ui.log`。例外有三種，腳本都已標明：events、`productSound` 快照、執行時才組出的 `surf-ref …` 字串。
- 已驗證：結果表 56 列，🔴 45、🟢 11，和章節裡的 56 個項目標題一致。
- 沒有改產品程式，沒有啟動遊戲、伺服器或 DB，沒有複製遊戲資源。

## 未驗證什麼
- 推測：B 輪診斷入口裡，用按鈕或熱鍵開原生窗是否正常。程式上只確認了聊天、書籤、位置分享這幾個入口會被 `normalProduct` 擋住。
- 子代理核對的 `檔案:行` 我沒有逐條重看。
- 全部 56 項都待真機。

## 產出路徑
- `docs/testing/smoke-m7.md`
- `docs/reports/issue-944.md`（本檔）

## 給下一棒的提示
- **建議 smoke 前先開一張小 code 任務**：讓一般產品入口接受 `--evidence <dir>`（`ProductEntry.Normal`，必要時讓 launcher 轉送 `--evidence`／`--classic-ui`），並加離線測試。合併後 A 輪就能直接收 `native-ui.log`，B 輪可以省略，腳本 §0.3 也要跟著改。
- gap-matrix 15.12.7 已經過時（R10 合併後沒有回填）。之後回填時可以直接抄腳本 §11 的表。
- Codex／dot 跑 smoke 時，照 §12 結果表逐項填；🔴 項的備註要寫觀察值。
