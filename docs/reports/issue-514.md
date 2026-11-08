# Issue #514 — T208：M7-G 以物品學魔法（code，第 1 次嘗試）

分支：`task/514`。日期：2026-11-08。

## 做了什麼
- 新檔 `engine/source/SpellExchange.cs`，透過 M7-A 擴充點（`[WorldReceive(13)]`／`[WorldReset]`／`[WorldSnapshot("spellExchange")]`）掛上，**沒有改** `WorldSession.cs`、`Program.cs`、`NpcInteraction.cs` 或其他模組的檔案。
  - 格式照 SP（#132）。原服行為照 `C_SkillBuyItem`／`C_SkillBuyItemOK`／`S_SkillBuyItem` 原始碼（只讀，SHA-256 見 SP）：
    - **C245** `C(245) D(npcObjId)`：要求清單。原服只接受「目前有對話窗的 NPC」且距離 5 格內，所以本機只在 NPC 對話頁開著（`InteractionValid && InteractionKind=="dialog"`）時送。
    - **S13** `C(13) H(count) count×D(skillIndex)`：skillIndex＝skillId−1，原服已濾掉已學的技能；空清單只有 `H(0)`。重複 index、截斷、多出資料、index ≥ int.MaxValue 都丟 `ProtocolException`。
    - **C191** `C(191) H(count) count×D(skillIndex)`：原服逐筆 +1 換回 skillId。本機先檢查：至少 1 項、不重複、都在清單內、還沒學會。
  - 原服在 S13 發的是一次性授權票（`UiOperationContext.takeSkills`），所以一份清單只能送一次 C191。送出後清單作廢，新清單會取代舊清單（用 revision 判斷）。
  - 鬼魂、死亡、傳送中、交易中、動作鎖定時不送（原服也會擋鬼魂／死亡／傳送／個人商店）。
  - 不自己授予技能，也不扣本機背包：學會與否只看原服的 S164（`LearnedMagic`）。快照與提示顯示「本次選取已學會 n/m 項」。
  - API：`LoginConnection.RequestSpellExchangeList()`、`LearnSpellExchange(view, indices)`、`SpellExchangeSnapshot()`、`CloseSpellExchange(view)`。
  - 快照 `extensions.spellExchange`：revision、phase（closed／waiting／list／empty／submitted）、notice、目前清單（NPC id、每列 index／skillId／名稱／是否已學）、已送出的 skillId 與已學數量。
- 新檔 `engine/source/SpellExchangeUi.cs`：**暫行** WinForms 視窗 `SpellExchangeUi`（自有 Form，作法同 `BoardUi`）。內容是勾選清單、「學習勾選的魔法」（先確認，送一次）、「重新要求清單」、提示列。標題註明「原版版面待查證」。C245 成功送出後，由 `LoginConnection` 找到擁有這條連線的 `ClientForm` 開窗。
- 新檔 `engine/source/SpellExchangeTests.cs`：`[WorldModuleTest("spell-exchange")]` 共 54 項，由既有的 `--module-tests` 入口自動執行（CI 的「World module extension tests」步驟）。涵蓋：註冊、C245／C191 位元組、S13 解碼與壞封包、要求→清單→送出→S164 回報流程、各種拒送條件（空選擇、不在清單、重複、已學、舊 revision、送第二次）、空清單、沒要求過的清單、清單取代、關閉、Reset，以及離線連線不送任何封包。
- `engine/_integration/dev/rebuild-lock.json`：用 `rebuild.py --update-lock` 重產，沒有手動改 hash。

## 怎麼驗證（本機，2026-11-08）
- `rebuild.py --update-lock` → `dev-inputs-verified`（第二次合併 origin/main〔含 #531〕後 242 個來源檔）。`rebuild.py --build` → `PASS dev`，product DLL SHA-256 `a36d9d97315f1e8b5d967c2af4eb1b6639e849087ca37cc689f8e3d95341cf37`。
- `dotnet build --no-incremental`：新檔沒有警告。原本有 CS0114（`Refresh` 蓋掉 `Control.Refresh`），已改名修掉；剩下的只有既有的 `MagicUi.cs` 警告。
- `P11.ClientEngine.exe --module-tests` → `pass`（5 組、271 項）：agit 44、castle 62、recovery-death 72、spell-exchange 54、world-extensions 39（其中「default registry valid」也通過）。
- 既有 CI 離線測試退出碼全部是 0：DemoChecks 的 map-continuity、combat-admission、item-status、afflictions、resurrection、polymorph-cadence、buddies、item-transfer、doors、pickup、hypertext，以及 PlayerChatChecks。
- `check-no-game-assets.py` → `pass`（3761 個追蹤檔，0 錯誤）。第二次同步後另跑 DemoChecks map-continuity → `PASS 33019`。
- 建置前置：worktree 用 junction 指到主專案的 `engine/vendor`（唯讀，已被 gitignore，沒有 commit）。
- 第三次同步 origin/main（含 #519、#523、#548、#551）：合併無衝突；`--update-lock` → 245 個來源檔；`--build` → `PASS dev`，product DLL SHA-256 `fde9cb39b7d9776acdba14b17ee7a0c2d75820ef9cfd5bb054fe311137a7ceeb`；`--module-tests` → `pass`（7 組、401 項，spell-exchange 54 項全過）；DemoChecks map-continuity `PASS 33019`、door-approach `PASS 361`、doors `PASS 648`；`check-no-game-assets.py` → `pass`（3768 檔，0 錯誤）。
- 第四次同步 origin/main（含 #446 背景音樂）：`rebuild-lock.json` 衝突改用 main 版後以 `--update-lock` 重產（251 個來源檔）；`--build` → `PASS dev`，product DLL SHA-256 `f9ae10b9187a851f10a4194cff974a975574454a591975cda34023be7ebd430d`；`--module-tests` → `pass`（7 組、401 項，spell-exchange 54 項全過）；DemoChecks map-continuity `PASS 33126`、doors `PASS 648`、hypertext `PASS 357`、pickup `PASS 369`；`check-no-game-assets.py` → `pass`（3778 檔，0 錯誤）。
- 第五次同步 origin/main（含 #510 AbilityStatus、#463 原生 UI 畫布）：`rebuild-lock.json` 衝突改用 main 版後以 `--update-lock` 重產（262 個來源檔）；`--build` → `PASS dev`，product DLL SHA-256 `51ef81cb493031fddb8f7bc2c62735bc5dff31860c453272e20dead9eb1e904e`；`--module-tests` → `pass`（8 組、439 項，spell-exchange 54 項全過）；`check-no-game-assets.py` → `pass`（3792 檔，0 錯誤）。
- 第六次同步 origin/main（2026-11-09，含 #520 幽靈限制、#516 Yes/No 詢問槽）：先快轉到遠端 `task/514`，再合併 main；`rebuild-lock.json` 衝突改用 main 版後以 `--update-lock` 重產（263 個來源檔）；`--build` → `PASS dev`，product DLL SHA-256 `7daf51d3ec86d2b2f7fae91dda0e0809fa5df69c4cae4ab951906905575ab3b8`；`--module-tests` → `pass`（8 組、439 項，spell-exchange 54 項全過）；DemoChecks yes-no `PASS 425`、map-continuity `PASS 33259`；`engine/tools/check-no-game-assets.py` → `pass`（3799 檔，0 錯誤）。
- CI 結果以 PR 為準。

## 沒驗證什麼
- **沒有做真機驗收**：沒有啟動遊戲、伺服器或 DB，也沒有在原服上實際收發過封包。
- **C245 的入口還沒接（待查證）**：TW16 的 NPC 對話頁用哪個 HTML 動作觸發 C_EXCHANGEABLE_SPELL（op 125），reference 裡查不到（PG§6 只查了 13 個未知送包，不含這個）。所以目前**沒有產品入口會呼叫** `RequestSpellExchangeList()`，視窗在產品裡實際上打不開；只有 API、視窗類別和離線測試。這不能算功能完成。
- **原生視窗版面待查證**：op 175（`sub_93F200`／`0x53E600`）的原版視窗沒有查過，所以暫行視窗不照原版畫，也不冒充原版。
- **材料不足的回包**：原服回 `S_ItemError(skillId-1)`，但原始碼裡 `writeC(S_OPCODE_ITEMERROR)` 被註解掉，送出去的只有一個 D，沒有 opcode。客戶端收到會怎麼解讀不明，本模組不處理。未開放的技能，原服回 S71 訊息 939，由既有的訊息路徑顯示。
- 學會時的 S55 特效（gfx 224）沿用既有路徑，沒有另外測試。
- 傳送後原服的授權票還算不算數不明；本模組只在 Reset（重進世界）、新清單或送出後讓清單作廢。

## 產出路徑
- `engine/source/SpellExchange.cs`、`engine/source/SpellExchangeUi.cs`、`engine/source/SpellExchangeTests.cs`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/reports/issue-514.md`（本檔）

## 給下一棒的提示
- 先開一張查證任務：在 TW16 IDB 找 C_EXCHANGEABLE_SPELL（op 125）的 `sub_9FBC00` 送出點（可用 `wo_sendsites.py`），看是哪個 HTML 動作字串或 UI 觸發；順便查 op 175 處理函式 `sub_93F200` 開的視窗版面。
- 查到動作字串後，在 `NpcInteraction.cs` 的 `InteractCore`（`"buyspell"→C145` 那一行旁邊）加一個分支，呼叫 `RequestSpellExchangeList()`，不要送 C125。這要改 `NpcInteraction.cs`，需另開任務。
- 原生版面查到之後，用原生 UI 面板取代 `SpellExchangeUi`；`SpellExchangeView` 已有清單、能否送出、提示，面板可直接用。
- 本機建置要用 `py -3` 找到的 Python，而且 worktree 要有 `engine/vendor` junction。
