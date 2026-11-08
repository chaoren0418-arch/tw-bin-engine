# Issue #511 — T205：M7-C 城堡：城主、稅率、金庫（code，第 1 次嘗試）

分支：`task/511`。日期：2026-11-08。

## 做了什麼
- 新檔 `engine/source/Castle.cs`，透過 M7-A 擴充點（`[WorldReceive]`／`[WorldReset]`／`[WorldSnapshot("castle")]`）掛上，**沒有改** `WorldSession.cs`、`Program.cs`、`LoginConnection.cs` 或其他模組的檔案。
  - 接收（格式照 SP；原版行為照 `windows-2.md` §5.1／T226）：
    - **S69 CastleMaster**：`C(type) D(objid)` 寫進城主表 `table[城堡]=物件 id`（城堡編號 < 9 才寫入）。照 WIN2 §5.1 的原版行為：第 1、3–8 格會畫王冠，第 2 格不畫；type 0 只寫第 0 格，不會清掉其他格。
    - **S185 TaxRate**：`D(objid) C(min) C(max)` 開「稅率」詢問。範圍取封包裡的值（原服送 10／50）。
    - **S4 Deposit**：`D(objid)` 開「存入」詢問，範圍 1–int.MaxValue（原服 offer 的範圍）。
    - **S141 Drawal**：`D(objid) D(count)` 開「提領」詢問，上限取 count；count 是 0 時只發通知，不開詢問。
    - 每種封包都會在聊天區加一行 `[城堡] …` 文字通知（城主變更、失城、詢問、送出）。這是暫代，原版沒有這些文字。
  - 送出：`LoginConnection.SendCastleReply(CastleView, value)`，依目前的詢問送 **C19** `D(objid) C(rate)`、**C56** `D(objid) D(count)` 或 **C44** `D(objid) D(count)`。另有 `CastleSnapshot()`、`CancelCastleOffer()`。
    - 送出前檢查：objid 必須是自己角色 id（原服只接受這個）；稅率限 10–50，金額限詢問的範圍，超出就不送。
    - 原服每次詢問只發一張一次性票（`UiOperationContext.takeCurrent`），所以送出後就清掉詢問，同一個詢問不會送第二次。新的詢問會取代舊的（以 revision 判斷）。
  - 快照 `extensions.castle`：revision、notice、目前的詢問、城主表第 1–8 格（含 `crowned`）、第 0 格的值。
- 新檔 `engine/source/CastleTests.cs`：`[WorldModuleTest("castle")]` 共 62 項檢查，由既有的 `--module-tests` 入口自動執行（CI 的 "World module extension tests" 步驟）。
  - 涵蓋：註冊、C19／C44／C56 的位元組、S69 城主表語義（第 2 格、type 0、type ≥ 9、同值重送、leader 0）、各詢問的範圍與邊界、身份不符或舊 revision 時拒送、單次回覆、取消、壞封包丟 `ProtocolException`、Reset。
- `engine/_integration/dev/rebuild-lock.json`：用 `rebuild.py --update-lock` 重新產生，沒有手動改 hash。

## 怎麼驗證（本機，2026-11-08）
- `rebuild.py --update-lock` → `dev-inputs-verified`，236 個來源檔。`engine/build-product.ps1` → `PASS dev`，product DLL SHA-256 `3f1165868d4e1ec7320e47c78cd65049156e31ecab3e2e480b5abba9c93dbc66`。新檔沒有新增警告（警告只有既有的 `MagicUi.cs(20,62)`）。
- `P11.ClientEngine.exe --module-tests` → `pass`，共 2 組、101 項：castle 62、world-extensions 39（其中「default registry has no invalid registrations」也通過）。
- 既有 CI 離線測試全部退出碼 0：DemoChecks 的 map-continuity、combat-admission、item-status、afflictions、resurrection、polymorph-cadence、buddies、item-transfer、doors、pickup、hypertext，以及 PlayerChatChecks。
- `check-no-game-assets.py` → `pass`（3738 個追蹤檔，0 錯誤）。
- 建置前置：worktree 沒有離線的 `engine/vendor`，所以照其他 worktree 的做法建了一個 junction，指到主專案的 `engine/vendor`（唯讀使用，已被 gitignore，沒有 commit）。
- CI 結果以 PR 為準。

## 沒驗證什麼
- **沒有做真機驗收**：沒有啟動遊戲、伺服器或 DB，也沒有在原服上實際收發過封包。
- **畫面沒做**：
  - 王冠：12 格動畫、500 ms 一格、位置偏移已查證，但圖源 `dword_15451B8` 待查；而且要在 `ObjectRendering.cs`（保留給 T168）加掛點。
  - S185／S141／S4 在原版是開 NPC 對話窗的 `tax`／`withdraw`／`deposit` HTML 頁，要改 `NpcInteraction.cs`／`NpcUi.cs`，不在本張範圍。
  - 目前沒有任何 UI 會呼叫 `SendCastleReply`；只有 API 與離線測試。
- **S185 的第三個 C**：TW16 讀 `dccc`，原服只寫兩個 C，所以第三個落在補零上，意義待驗證；本引擎只讀兩個。
- **第 8 格的名稱**：原服把妖魔城（2）的城主改送 8，所以文字通知裡的「狄亞得要塞」也可能其實是妖魔城（推測，依原服 `L1CrownInstance`）。
- **type 0（失城）**：原版不會移除既有王冠，本引擎照做，只加一行文字通知。實際上王冠會不會一直留到下一次 S69，待真機確認。

## 產出路徑
- `engine/source/Castle.cs`、`engine/source/CastleTests.cs`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/reports/issue-511.md`（本檔）

## 給下一棒的提示
- 補畫面時建議開兩張任務：
  - (a) NPC 對話面板接 `tax`／`withdraw`／`deposit` 頁（`templates.json` 已有這三頁），送出時呼叫 `LoginConnection.SendCastleReply(CastleSnapshot(), 值)`。
  - (b) T168 合併後，在 `ObjectRendering.cs` 加王冠掛點，用 `WorldSession.HasCastleCrown(objectId)` 判斷要不要畫；王冠圖號要先查。
- 聊天區的 `[城堡]` 文字是暫代，原版沒有。接上 HTML 頁之後，應該拿掉詢問類的文字（`CastleLine` 裡的 Prompt／Sent）。
- 本機建置要用 `py -3` 找到的 Python（PATH 裡沒有 `python`），而且 worktree 要有 `engine/vendor` junction。
