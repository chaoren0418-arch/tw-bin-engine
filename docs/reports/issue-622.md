# Issue #622 T261：R2-F NPC 對話面板收尾

依據：`docs/reference/gap-matrix.md` 11.5 節 R2-F、11.9 節（Q-3 回填）；`docs/reference/send-sites-2.md`（SS2）§1.1、§1.2、§8、§11；WIN2 §5.1、§5.3。

## 做了什麼

1. **城堡 `tax`／`withdraw`／`deposit` 頁的數字框與送出鈕**（`Castle.cs`）
   - 新增 `WorldSession.CastlePageOffer`：城堡頁還顯示在面板上、而且頁的種類和詢問相同時，才有數字框可用。
   - 新增 `LoginConnection.SendCastlePageReply(epoch, page, offer, value)`：世界、查詢頁版本、詢問版本都要對，最後交給既有的 `SendCastleReply` 送 C19／C56／C44。
   - 新增 `CancelCastlePage`：關掉城堡頁，不送封包。
   - WinForms NPC 面板底部加一列：範圍文字、數字框、「確定」、「取消」。排法和 S136 的數量列相同。
   - 稅率範圍 10–50。初始值依 SS2§8，3.81 不送，由畫面決定：一律用下限（稅率 10、金額 1）。
   - **拿掉暫代聊天行「[城堡]」**：S69／S185／S4／S141 與送出後的通知都不再寫進聊天區（原版沒有這些文字，見 issue-511）。通知只留在 `castle` 快照的 `notice`（新增 `CastleNotice`），面板上也會顯示。
2. **S136 數字框補到 web 面板**
   - `AmountPrompt.cs`：新增 `SendAmountPromptReply(epoch, page, prompt, value)`，檢查世界、對話頁、詢問三個版本；新增 `AmountPromptWeb()` 提供 web 狀態。
   - `NpcUi.cs`：新增 web 指令 `npcAmount`、`npcCastle`、`npcCastleCancel`，欄位多 `prompt`、`value`，嚴格檢查欄位與範圍。`InteractionWeb()` 多帶 `query`（伺服器頁／城堡頁的選項）、`amount`、`castle`。
   - `engine/web/interaction.js`：在 `#npcNotice` 前動態建立 `#npcPrompt`，內容包括：
     - 伺服器頁／查詢結果的文字與選項（`npcQueryOption`）；
     - S136 數字框加「確定」；
     - 城堡頁數字框加「確定」「取消」。
   - `index.html` 沒有改。
3. **`exitghost` 接到 HTML 動作**（`NpcInteraction.cs`）
   - NPC 對話頁的 action 選項：`Interact("option")` 先經 `ClientHyperAction`。動作是 `exitghost` 時交給 R2-D 的 `TryHyperAction`（幽靈中才送 C173），**不送 C125**。
   - 伺服器主動頁（`QueryOption`）也一樣；送出後照其他動作關頁，沒送出就保留頁面。
4. **以物品學魔法的入口**（`NpcInteraction.cs`、`SpellExchange.cs`、`SpellExchangeUi.cs`，依 SS2§1.1／§1.2）
   - HyperAct `xchgspell`：NPC 外型＝834 才呼叫 `RequestSpellExchangeList(openWindow:false)` 送 C245；其他外型什麼都不送。
   - `buyspell`：外型＝834 時不送；外型 ≠ 834 照舊送金幣學魔法清單請求。
   - S13 空清單：如果要清單的那個 NPC 對話仍有效，就把對話頁換成 `HorunEv3`。非空清單會由 `SyncSpellExchangeWindow` 開學魔法窗（目前仍是暫行 WinForms），同一份清單只開一次。
5. **測試**：新檔 `engine/source/NpcPageTests.cs`，`[WorldModuleTest("npc-page")]`，73 項，自動接上 `--module-tests`。
   - `CastleTests.cs`（不在可改清單內，但必須改）：原本斷言「[城堡]」聊天行的 7 處，改成讀 `castle` 快照的 notice，另加「聊天區沒有城堡行」一項。這是本任務要求拿掉的行為，不改的話既有測試會失敗。

## 怎麼驗證

- `rebuild.py --update-lock`：重產 lock，328 個來源檔、337 個鎖定檔。
- `rebuild.py --build --configfile scratch/issue-622/NuGet.Config`：`PASS dev`。worktree 沒有 `engine/vendor`，所以這份設定以唯讀方式指向主專案的 vendor。productDll SHA-256 `d963edfec60c0c0507996b317151ff9c4362c1068f33275b2fb106aa83f01a67`。
- `P11.ClientEngine.exe --module-tests`：`pass`，40 組、1963 項、失敗 0。其中：

  | 組 | 項數 | 說明 |
  |---|---|---|
  | npc-page | 73 | 新增 |
  | castle | 101 | |
  | amount-prompt | 70 | |
  | spell-exchange | 54 | |
  | user-play-stats | 111 | |
  | npc-server-page | 28 | |

- DemoChecks 的 `ghost-mode` 群組：PASS 721。
- DemoChecks 的 `player-query` 群組（含 HyperTextChecks）：PASS 1058。
- `web-checks.cjs`：PASS 18。
- `node --check engine/web/interaction.js`：通過。
- `engine/tools/check-no-game-assets.py`：pass，errors 0。

## 未驗證什麼

- DemoChecks 的 `combat-loop`、`map-continuity` 與預設完整入口：worktree 沒有 `engine/assets` junction（找不到 `terrain0.bin`、`integrity.json`），本機無法執行，交給 CI 與後續有素材的環境。
- 沒有真機驗收。下列項目都要在實機上核對：
  - 城堡管家的 tax／deposit／withdraw 頁實際的 HTML 模板；
  - 數字框在原版 HYPER_TEXT_WINDOW 裡的位置與外觀（待 T190）；
  - 伺服器收到 C19／C56／C44 後的回應；
  - web 面板的實際操作。
- 推測（畫面決定）：
  - 數字框初始值用下限。
  - `exitghost` 送出後 NPC 對話頁不關。原版 HyperActExitGhost 送出後會不會關窗沒有查。
- 沒做：
  - op 44（S41 金幣學魔法）空清單依外型開 `GerengEv3`／`Sedia1`／`Siriss1`。處理點在 `NpcServices.cs`，不在本組可改範圍。
  - 學魔法窗改成 `TRADE_WINDOW` 模式 7，歸 R2-E／Q-1。

## 產出路徑

- 程式：`engine/source/Castle.cs`、`AmountPrompt.cs`、`NpcInteraction.cs`、`NpcUi.cs`、`SpellExchange.cs`、`SpellExchangeUi.cs`、`engine/web/interaction.js`
- 測試：`engine/source/NpcPageTests.cs`（新）、`engine/source/CastleTests.cs`（斷言更新）
- lock：`engine/_integration/dev/rebuild-lock.json`
- 報告：`docs/reports/issue-622.md`

## 給下一棒的提示

- **R2-E（接著派）**：
  - `NpcInteraction.cs` 新增了 `ClientHyperAction`、`InteractionNpcGfx`、`QueryActionValue`，`ShopRows` 呼叫點沒動。
  - 學魔法窗改 `TRADE_WINDOW` 模式 7 時，把 `SpellExchangeUi.cs` 的 `SyncSpellExchangeWindow` 改成開原生面板即可，開窗時機（非空 S13）已經接好。
  - op 44 空清單換頁可以照 `SpellExchange.cs` 的 S13 空清單寫法，放在 `NpcServices.cs`。
- **R2-I（王冠）**：「[城堡]」聊天行已拿掉；城主變更只剩 `castle` 快照的 notice 與 `CastleCrownFrames` 資料。
- **web 指令**：`npcAmount`／`npcCastle`／`npcCastleCancel` 的 `revision` 是頁版本，不是詢問版本。
  - `npcAmount` 的 `revision`＝InteractionRevision；`npcCastle*` 的 `revision`＝QueryRevision。
  - 詢問版本放在 `prompt`。
- **真機 smoke（dot）**：在 P24Lab 找城堡管家開 tax 頁，輸入 10–50 送出一次，確認 C19 有被接受。另外核對幽靈狀態下 NPC 頁的 `exitghost` 選項（如果模板有）。
