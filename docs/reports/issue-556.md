# Issue #556 — T234：伺服器主動送的 S39 頁（W2-A）

階段 code，第 1 次嘗試。分支 `task/556`。依據 `docs/reference/windows-2.md` 第 9 節（W2-A）與第 4 節。

## 做了什麼

**問題**：S39（S_HYPERTEXT）原本只在 `npc == InteractionNpc` 時接受。3.81 `S_ShowPolyList` 送的物件 id 是玩家自己，不是對話中的 NPC，所以 `monlist`、`pledge` 這類頁面會被丟掉。

**改法**（`engine/source/NpcInteraction.cs`）：
- `ReceiveInteraction`：S39 的物件 id 等於自己（`Status.ObjectId`），且世界已就緒、又不是目前 NPC 對話的回包時，交給 `ReceiveServerPage`，不再丟掉。
  - `y_who`、`pc_show`、排名頁、`RaiseAttr` 照舊由原模組先攔，不受影響。
- **伺服器頁**放在既有的查詢頁欄位（`QueryPage`），與 NPC 對話共用同一個面板。新增 `QueryActionable=true` 表示選項可點。
  - 不取代進行中的 NPC 對話、商店或已送出的交易。
  - `SelectTarget` 會關掉 NPC 對話，但不會關掉伺服器頁。
  - 世界切換（`WorldEpoch`）時伺服器頁失效。
- **選項**：
  - `link`：本地換頁，沿用同一組 S39 參數，不送封包。
  - `action`：送 C125 `C_NPCAction`，物件 id＝自己，與 NPC 頁走同一個封包格式（`NpcPackets.Action`）。
  - `checkpk`：照 HyperAct 規則送 C51，與 NPC 頁一致。
- **送出**：走 `LoginConnection.QueryOption`，經 `sendMovementGate` 閘門，帶 revision、epoch、session 檢查。成功送出一次後關頁，不自動重送。
- **關頁**：S39 的 htmlid 為空字串時關閉伺服器頁；玩家查詢結果不受影響。
- **快照**：`InteractionSnapshot().query` 多了 `actionable` 與 `options`。
- **`monlist` 的特別模式**：id 剛好是 `monlist`（3.81 會送的就是這個）時，原版 `sub_874A10` 走一般 HTML，本實作也照一般模板顯示。長度大於 7 的 `monlist*` 特別模式（`sub_867530`）3.81 不會送，沒有做。

**畫面**（`engine/source/NpcUi.cs`，WinForms NPC 面板）：
- 伺服器頁標題顯示「伺服器頁」，選項按鈕可點，點了經 `RunQueryOption` → `login.QueryOption`；關閉鈕文字改成「關閉伺服器頁」。
- 玩家查詢結果維持唯讀，行為不變。
- 網頁命令契約新增 `npcQueryOption`（欄位同 `npcOption`，`revision` 填 `query.revision`），對應指令 `query-option`。`engine/web/interaction.js` 不在可改範圍，網頁按鈕還沒接上。

**測試**：新檔 `engine/source/NpcServerPageTests.cs`，`[WorldModuleTest("npc-server-page")]`，接 `--module-tests` 入口，共 28 項檢查。內容全是合成封包、合成模板與記憶體送出器。
- 這個檔案不在「可修改的檔案」清單內，依共同規則「測試提交進 repo，接 `--module-tests` 入口」在自己的新檔新增。
- 涵蓋：
  - 自己的 `monlist` 頁能顯示，模板參數正確代入，選項可用。
  - 不影響 NPC 對話狀態。
  - action 送出的 C125 bytes 正確，送一次後關頁，不重送。
  - link 本地換頁，舊 revision 被拒絕。
  - checkpk 送 C51；送出閘門拒絕時頁面保留。
  - NPC 等待回包時兩者並存。
  - 空 htmlid 關頁；缺模板時仍顯示。
  - 別人的 S39 照舊丟掉；世界未就緒時不開頁。
  - 世界切換後頁面失效。
  - 網頁命令契約；畸形封包被拒絕。

未改 `WorldSession.cs`，不需要新的封包註冊：S39 原本就由 `NpcInteraction.cs` 的 `ReceiveInteraction` 接收。

## 怎麼驗證（已驗證）

| 項目 | 指令 | 結果 |
|---|---|---|
| lock | `rebuild.py --update-lock` | `dev-inputs-verified`，240 個來源／249 個鎖定檔 |
| 建置 | `rebuild.py --build --configfile .github/nuget.ci.config` | `PASS dev`，product DLL `c5bf8f5c…44536` |
| 模組測試 | `P11.ClientEngine.exe --module-tests` | pass：5 組、245 項（`npc-server-page` 28 項），0 失敗 |
| 既有測試 | DemoChecks `player-query`（含 hypertext） | PASS 1058 |
| 既有測試 | DemoChecks `hypertext` | PASS 357 |
| 既有測試 | DemoChecks `map-continuity` | PASS 33019 |
| 既有測試 | DemoChecks `pets` | PASS 327 |
| 既有測試 | DemoChecks `polymorph` | PASS 327 |
| 既有測試 | DemoChecks `polymorph-cadence` | PASS 380 |
| 既有測試 | `npc-tests/NpcChecks` | PASS 165 |
| 資產檢查 | `check-no-game-assets.py` | pass，errors 0 |

**同步 origin/main 後重驗（2026-11-08）**：main 前進 27 個 commit，`rebuild-lock.json` 衝突改取 main 版本後以 `--update-lock` 重產（249 個來源／258 個鎖定檔）。
- 建置 `PASS dev`，product DLL `573a196a…96ed40`。
- `--module-tests`：pass，7 組、375 項，0 失敗。
- DemoChecks `hypertext` PASS 357、`player-query` PASS 1058、`polymorph` PASS 327。
- `check-no-game-assets.py`：pass，errors 0。

**第二次同步 origin/main 後重驗（2026-11-08）**：main 又前進 27 個 commit（含 NativeUi、AbilityStatus 等），同樣只有 `rebuild-lock.json` 衝突，改取 main 版本後以 `--update-lock` 重產（260 個來源／269 個鎖定檔）。
- 建置 `PASS dev`，product DLL `f2dbc061…2078eb`。
- `--module-tests`：pass，8 組、413 項，0 失敗（`npc-server-page` 28 項）。
- DemoChecks `hypertext` PASS 357、`player-query` PASS 1058、`polymorph` PASS 327。
- `check-no-game-assets.py`：pass，errors 0。

- worktree 沒有 `engine/vendor`，本機離線 NuGet 設定無法 restore，所以建置改用 CI 的 `.github/nuget.ci.config`。
- DemoChecks `world` 讀 `engine/assets/terrain0.bin`，worktree 沒有 assets junction，所以失敗。這與本改動無關，CI 也不跑這組。
- CI：PR 開出後由 GitHub Actions 驗證。

## 未驗證什麼

- **真機**：沒有啟動遊戲或伺服器。3.81 伺服器收到「物件 id＝自己」的 C125（例如 `monlist` 選變身）後怎麼處理，未實測。依 windows-2.md 第 4 節的建議走同一路徑。
- **關窗時機（推測）**：送出 action 後關頁，是為了避免重送而做的推測。原版點 action 後何時關窗未查證。
- **畫面**：r29 `templates.json` 裡實際的 `monlist` 模板在本頁的呈現沒有看過，測試只用合成模板。模板內的 `<input>`／`subHtml` 等動態結構會依既有規則停用選項。
- **原生面板**：`HYPER_TEXT_WINDOW` 的原生版面仍等 T190。目前是 WinForms 面板，沒有新增原生面板。
- `CPolymorphUI`、`CSpecialPolymorphUI`、S_ASK 180／289 依文件不做。

## 產出路徑

- `engine/source/NpcInteraction.cs`、`engine/source/NpcUi.cs`（修改）
- `engine/source/NpcServerPageTests.cs`（新增，模組測試）
- `engine/_integration/dev/rebuild-lock.json`（`--update-lock` 重產）
- `docs/reports/issue-556.md`（本報告）

## 給下一棒的提示

- 網頁介面要接伺服器頁按鈕時：
  - `engine/web/interaction.js` 送 `npcQueryOption`，`revision` 填 `state.model.query.revision`，`option` 填 `query.options[i].id`。
  - 只在 `query.actionable` 為 true 時啟用按鈕。
- 真機 smoke（dot／P24Lab）：用變身卷軸或 GM 指令觸發 `S_ShowPolyList`，確認：
  - 面板顯示「伺服器頁」與 `monlist` 選項；
  - 點選後角色變身；
  - 封包追蹤看得到 C125 的物件 id 是自己。
- W2-D（S136 數量輸入）也可以沿用 `ShowServerPage`／`QueryActionable` 的路徑，但需要加數字框。
