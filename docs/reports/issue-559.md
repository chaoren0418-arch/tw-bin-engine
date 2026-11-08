# Issue #559 — T237：料理窗資料（W2-F）（code，第 1 次嘗試）

分支：`task/559`。日期：2026-11-08。

## 做了什麼
- 新檔 `engine/source/Cooking.cs`：用 M7-A 擴充點（`[WorldReceive(250,Subtype=52)]`、`[WorldReset]`、`[WorldSnapshot("cooking")]`）註冊。**沒有改** `WorldSession.cs` 或其他檔案。
  - 接收 **S250 子碼 52**（`S_PacketBoxCooking(value)`）：`C(52) D(0x02DF31DB) C(1) C(value)`。這和 TW16 原版讀的 `d`＋`c`＋`c` 完全一致（`windows-2.md` 第 7 節）。
  - 第二欄是 1：開窗，等級 = value＋1。已經開著時再收到，就切到新等級。
  - 第二欄不是 1：不開窗，等級不變，只記錄（推定）。
  - 長度不對或補零不是 0：丟 `ProtocolException`。
  - 資料模型 `CookBookState(Open, Value, Level, Magic, Revision)`：
    - 由 `WorldSession.CookBook` 讀取。
    - `CloseCookBook()` 在本機關窗，不送封包。
    - Reset 時清空。
  - `CookingProtocol` 的常數與輔助函式：
    - 料理書道具編號 = 41255＋value（依伺服器 `Cooking_Book`：操作 0 送 `itemId − 41255`）。
    - `COOK_BOOK_WINDOW` 的版面常數：底 2254、捲軸 2039、等級圖 2545–2555 在 (116, 257)、量表 2256 在 (35, 251)。這些只是數字，留給 T190 用，不讀任何素材。
  - 快照 `extensions.cooking`：open、value、level、bookItemId、levelSurf、magic、magicMatches381、revision。
- 新檔 `engine/source/CookingTests.cs`：`[WorldModuleTest("cooking")]`，共 32 項檢查，由既有的 `--module-tests` 入口自動執行。涵蓋：
  - 註冊與快照
  - 3.81 封包的位元組
  - 開窗、切換等級、旗標 0、非 3.81 的 magic
  - 本機關窗
  - 長度錯誤
  - Reset
- `engine/_integration/dev/rebuild-lock.json`：用 `rebuild.py --update-lock` 重新產生（241 個來源檔），沒有手動改 hash。

## 怎麼驗證（本機，2026-11-08）
- `rebuild.py --update-lock`：`dev-inputs-verified`。
- `engine/build-product.ps1`：`PASS dev`，product DLL SHA-256 為 `a3c2ce5d63b75934bf3bf30226bf9b19b7a59fdc1052a21e844f5a14d2b7f56d`。
- `P11.ClientEngine.exe --module-tests`：`pass`，5 組共 249 項（agit 44、castle 62、cooking 32、recovery-death 72、world-extensions 39）。
- 其他既有的 CI 離線測試，退出碼都是 0：
  - DemoChecks：map-continuity、combat-admission、item-status、native-ui-resources、afflictions、resurrection、polymorph-cadence、buddies、item-transfer、doors、pickup、hypertext
  - PlayerChatChecks
- `check-no-game-assets.py`：`pass`（3757 個檔案，0 錯誤）。
- CI 結果以 PR 為準。
- **同步 origin/main 後重驗（2026-11-08）**：`rebuild-lock.json` 衝突取 main 版本後用 `--update-lock` 重產（259 個來源檔，`dev-inputs-verified`）；`build-product.ps1` `PASS dev`（product DLL `1124d2d1d49d73cb90c08b8af04931c3ddb3adab1ef604097770c78ff02f1aa1`）；`--module-tests` `pass`，7 組共 379 項，0 失敗（cooking 32）；`check-no-game-assets.py` `pass`（3790 個檔案，0 錯誤）。

## 沒驗證什麼
- **沒有做真機驗收**：沒有啟動遊戲、伺服器或 DB。
- **畫面沒做**：要等 T190。目前沒有任何 UI 讀 `CookBook`。
- 以下是推定或待驗證：
  - 第二欄不是 1 時，原版不做任何事。
  - 已經開著時再收到 1，原版是「切到新等級」，不是「關窗」。原文是「開／切換」，兩種讀法都說得通。
  - 第一欄 D 原版不檢查。本模組照收，並在快照標出是否符合 3.81 常數。
  - 等級圖用第 value 張。
- 料理的食譜清單、能不能做、需要的材料，3.81 都沒有用封包送，原版可能是本機資料表，本模組沒做。製作時送的 C 封包已有 `ItemUseArguments.Cooking`。

## 產出路徑
- `engine/source/Cooking.cs`、`engine/source/CookingTests.cs`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/reports/issue-559.md`（本檔）

## 給下一棒的提示
- T190 做料理窗畫面時：
  - 讀 `WorldSession.CookBook`（或快照 `extensions.cooking`）；`Revision` 有變就重畫。
  - 版面常數用 `CookingProtocol` 裡的。
  - 關窗呼叫 `CloseCookBook()`。
  - 在窗裡選食譜製作時，用既有的 `ItemUseArguments.Cooking(1, 食譜編號)` 對料理書送 C_UseItem。
- 「開／切換」到底是哪一種，需要真機用同一本料理書連用兩次來確認。
- 本機建置要先把 `py -3` 的 Python 加進 PATH，worktree 也要有 `engine/vendor` junction（已被 gitignore）。
