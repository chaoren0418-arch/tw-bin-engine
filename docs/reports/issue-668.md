# Issue #668 T277：gap-matrix 第三輪盤點

分支：`task/668`（從 origin/main `77baa88d` 建立）。階段：resource。

## 結論
- `docs/reference/gap-matrix.md` 新增第 12 節「第三輪」，第 1–11 節原文沒動。
- 新統計（290 列）：**已有 94、部分 151、缺 45**（第二輪是 67／175／48）。其中「部分→已有」26 列、「缺→已有」1 列、「缺→部分」3 列、「部分→缺（伺服器不支援）」1 列。「需先查證」0 列，「缺·可直接補」6 列。
- 建議第三批 code 任務 **9 組**（R3-A–I），另建議 1 張 resource 任務查 Q3-1–Q3-9。

## 主要發現
- 第二輪發現「只有測試在呼叫」的 10 項，有 7 項已經接上產品：日夜明暗、光照圖、雨雪、背景音樂與環境音、王冠、城堡回覆、學魔法入口、聊天指令、幽靈外觀。
- **程式已寫但產品沒接上**（12.2 有完整表）：
  - 5 個原生面板寫好了卻沒註冊：背包、魔法、倉庫、血盟、個人商店。就算開 `--native-ui` 也看不到。
  - 選單鈕除「離開」外都開 DevTools。
  - 開背包、魔法窗的熱鍵沒有接。
  - 血盟倉庫紀錄與料理沒有視窗。
  - 登入音樂與頭目音樂沒有產品呼叫。
- 原生 UI 仍預設關閉，所以第 4 節（UI 視窗）0 列「已有」。召喚選擇窗沒有 WinForms 備援，預設狀態下玩家選不了召喚怪物。
- S250 子碼現在處理 62 個中的 51 個。
- 依 T268／T275 的查證改列：op 133、C221、C53 改成「已有」（原版也沒有可見行為），`PledgeNoticeUI` 改成「缺（伺服器不支援）」。

## 怎麼驗證
- 逐張讀 `6d07fe0d` 之後合併的約 20 張任務報告，用 Grep 在 `engine/source` 找產品端的呼叫點（排除測試檔與 `engine/_integration`）。
- 報告閱讀與呼叫點掃描分給 4 個唯讀子代理平行做。重點的接線點我另外用 Grep 複核過：`ComposeAmbienceLights`、`ClientSoundScene.Configure`、`CastleCrownFrames`、`SendCastleReply`、`RequestSpellExchangeList`、`IsCommand`，以及全部 `[UiPanel]` 註冊。
- 新增 `rea-scripts/gap_count_r3.py`，依序套用第 11、12 節的變更表。第 11 節套用 88 列、第 12 節套用 87 列，沒有錯誤。
- 用 `gap_count.py`、`gap_count_r2.py` 重跑，結果仍是 58／164／67 與 67／175／48，表示前幾輪的資料沒有被改動。
- 分組列出的既有檔案都確認存在，彼此之間不重複，也不和審查中的 PR、已開任務的檔案重疊。

## 沒有驗證的部分
- 沒有建置、執行或真機測試；沒有啟動遊戲、伺服器或 DB。
- 「產品沒有呼叫」是用 Grep 判斷的，透過反射、委派或事件間接呼叫的地方可能會漏掉。
- 本輪沒有新的反組譯，所以沒有開 IDB、沒有用 subst。
- 審查中的 PR（#637–#640、#647、#653–#656、#662–#664）沒有比對。

## 產出
- `docs/reference/gap-matrix.md`（第 12 節）
- `docs/reference/rea-scripts/gap_count_r3.py`
- `docs/reference/INDEX.md`
- `docs/reports/issue-668.md`

## 給下一棒的提示
- 本任務是 resource 階段，沒有寫 `docs/plans/issue-668.json`。請主控依 12.5 開 code 任務，依 12.6 開 1 張 resource 任務。
- 可以直接派的組：R3-B（召喚備援與 `PetControl`）、R3-C（料理與血盟倉庫紀錄）、R3-E（`TRANSACTION_WINDOW`）、R3-G（聊天收尾）、R3-I（視窗 4 層上限）。
- 要等其他 PR 合併的組：
  - R3-A 等 #662、#654；它是本批唯一可以改 `UiClientBridge.cs` 的組。
  - R3-F 等 #623。
  - R3-D 的登入畫面那一處等 #638。
- 原生 UI 要不要改成預設開啟，請使用者決定（建議等 #475 真機 smoke 之後）。第 4 節的 48 列「部分」大多卡在這一點。
- `ServerFeatures.cs:7` 的註解已過時，`Renderer.PaintAmbience` 是死碼；由 R3-H 順手處理。
