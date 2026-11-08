# Issue #527 — T220：M7-T 個人商店開店設定窗（code，第 1 次嘗試）

分支：`task/527`。日期：2026-11-09。依據：WIN§1.1（`docs/reference/windows.md`）、SP `C_Shop`（`server-protocol.md`）、`docs/design/native-ui.md`。

## 做了什麼
- **`engine/source/PrivateShop.cs`**：在檔尾、`LoginConnection` 之前加上開店設定的資料與協定。既有的私商查詢與購買程式**沒有改**。
  - `PersonalShopPackets`：C38 `C_Shop`。
    - `Close()`＝`C(38) C(1)`。
    - `Open(sell, buy, chat)`＝`C(38) C(0) H(n) {D 物件, D 單價, D 數量}ⁿ H(m) {…}ᵐ Byte(chat)`。
    - 欄位意義已用伺服器 `C_Shop.java` 核對（唯讀，SHA-256 `cd94e740…8b9bff`，與 SP 記載相同）：兩個 D 是單價與數量；兩份清單**各最多 8 筆**；單價或數量 ≤0 時伺服器會直接**踢線**；收購單價上限 2,000,000,000；伺服器一律用 `polyIndex=0` 開店。
    - 本端送出前先擋：清單是空的、超過 8 筆、單價或數量是 0、超過 `int.MaxValue`、收購單價超過 2e9。
  - `PersonalShopSetup`：開店設定窗的本機狀態。
    - 出售、收購各 8 格。一頁 8 格，伺服器上限也是 8，所以頁數只會是 0 或 1；頁碼顯示「0/0」或「1/1」。
    - 兩行訊息，各最多 20 字。不接受控制字元，也不接受無法用 Big5 編碼的字。
    - 3 種外型（0–2），只存在本機。
    - 同一份清單裡不能放兩次同一個物件。
    - `BuildOpen()` 驗證後產生 C38。
  - `WorldSession.PersonalShopCandidate(...)`：從背包取一筆。
    - 出售：要持有、未裝備、數量不超過持有量。
    - 收購：背包要有同一個物件當樣本、未裝備。
    - 其他條件（能不能交易、封印、寵物項鍊、娃娃、安全區、貨幣）由伺服器判斷，本端不推算。
- **新檔 `engine/source/NativeUi/PersonalShopPanel.cs`**：實作 `IUiMovablePanel`（Window 圖層），Id 為 `PersonalShop_Window`。
  - 版面在執行期從 `PersonalShopUI.xml` 讀出，**沒有寫死座標或 surf**：底圖與框、關閉鈕、出售與收購各 2×4 格、兩個訊息欄（`LimitText` 必須是 20）、3 張外型圖與 CheckButton、4 個翻頁鈕與頁碼、確定與取消。
  - 缺節點或屬性時丟出 `UiLayoutException`；surf 是 null 時不畫，也不畫替代色塊。
  - 操作：
    - 點格子：選取該格。
    - 點外型：選取該外型。
    - 點訊息欄：取得焦點；Backspace 刪字，Tab／Enter 換欄。
    - 翻頁。
    - 確定：驗證通過時觸發 `Confirmed(byte[] C38)`。**面板自己不送封包**，送出交給整合端決定。
    - 取消、關閉鈕、Esc、離開世界：關窗並觸發 `Closed`。
    - 視窗上緣 18 px 可拖曳。
  - 物品圖示由 `IconSource` 提供；沒有提供時不畫。
  - `PersonalShopPanel.Registration` 已備好一行註冊。**沒有改 `UiPanelRegistry.cs`**，因為它不在本任務的可改檔案內。
- **新檔 `engine/source/PersonalShopTests.cs`**：`[WorldModuleTest("personal-shop")]`，共 91 項，由既有的 `--module-tests` 入口自動執行。
  - 只用合成資料：XML 是依 WIN§1.1 的元素名稱與座標自己組出來的（不是原檔）；surf 是單色合成圖（尺寸照原圖）。
  - 涵蓋：C38 的位元組、各種拒絕條件、設定模型、背包候選、由 XML 讀出的元件座標、版面錯誤、點擊與按鍵、拖曳、繪製（底圖、圖示、勾選、滑過與按下的圖、null surf 不畫）。
- `PrivateShopUi.cs` **沒有改**：開店設定窗屬於原生 UI；既有的 WinForms 私商分頁維持原樣。
- `engine/_integration/dev/rebuild-lock.json`：用 `rebuild.py --update-lock` 重產（276 個來源檔）。

## 重驗（本機，2026-11-09，第四次同步 origin/main `8264d7f3`，含 #464–#468、#526、#558）
- 合併時 `rebuild-lock.json` 衝突：取 main 版本後用 `rebuild.py --update-lock` 重產（`dev-inputs-verified`，292 個來源檔）。其他檔案自動合併，沒有衝突。
- `engine/build-product.ps1`：`PASS dev`，product DLL `ea015c516ab780ad1ef99cd5751f3f70cf91a7ee37635662ac95de979d0100ad`。
- `--module-tests`：`pass`，21 組共 1109 項，0 失敗（含 personal-shop）。
- `check-no-game-assets.py`：`pass`，3849 個檔案，0 錯誤。

## 重驗（本機，2026-11-09，第三次同步 origin/main `0b8dea8d`，含 #568）
- 合併時 `rebuild-lock.json` 衝突：取 main 版本後用 `rebuild.py --update-lock` 重產（`dev-inputs-verified`，278 個來源檔）。
- `engine/build-product.ps1`：`PASS dev`，product DLL `1f5eca75fbcc9d27f922bfb815ac5467f56fa223c9694d13680b41077311b775`。
- `--module-tests`：`pass`，18 組共 964 項，0 失敗。
- `check-no-game-assets.py`：`pass`，3824 個檔案，0 錯誤。

## 重驗（本機，2026-11-09，再同步 origin/main `b2d67c19`）
- 合併時 `rebuild-lock.json` 衝突：先取 main 版本，再用 `rebuild.py --update-lock` 重產（`dev-inputs-verified`，277 個來源檔）。
- `engine/build-product.ps1`：`PASS dev`，product DLL `7db4a4faede9802fb469ce24e0ef823c92e26f5704412a4512bc1c5d9bcea638`。
- `--module-tests`：`pass`，17 組共 939 項，0 失敗（personal-shop 91）。
- `check-no-game-assets.py`：`pass`，3820 個檔案，0 錯誤。

## 怎麼驗證（本機，2026-11-09，已同步 origin/main `4c4e76c1`）
- `rebuild.py --update-lock`：`dev-inputs-verified`，276 個來源檔。
- `engine/build-product.ps1`：`PASS dev`，product DLL `306ceb7befe46fe68f04c8de2613af16d94ba7a430bd6eb954b1d15e1ea6b002`。
- `P11.ClientEngine.exe --module-tests`（照 CI 的參數）：`pass`，16 組共 873 項，0 失敗（personal-shop 91）。
- DemoChecks（CI 的各組），退出碼都是 0：combat-admission、item-status、native-ui-resources、native-ui-canvas、afflictions、resurrection、polymorph-cadence、buddies、item-transfer、doors、pickup、door-approach、hypertext。PlayerChatChecks 也是 0。
- `map-continuity`：本機有原客戶端時，`notice S245 unknown message stays labeled` 失敗。原因是本機的訊息字串表認得 999 這個 id，和本次變更無關。把 `TW_CLIENT_ROOT` 指到不存在的目錄（等同 CI）後 `PASS 33259`。
- `check-no-game-assets.py`：`pass`，3814 個檔案，0 錯誤。
- CI 結果以 PR 為準。

## 沒驗證什麼
- **沒有做真機驗收**：沒有啟動遊戲、伺服器或 DB。
- **沒有接進產品流程**：
  - 面板沒有註冊。
  - 沒有開窗的觸發點。
  - 確定後不會真的送 C38。
- **推測**（照規則不當成已驗證）：
  - `5734;1`、`5736;4` 分號後數字的意義。目前只取第一個數字當 surf。
  - CheckButton 只有 `PushSurf`：選中時疊在外型圖上，沒選中時不畫。
  - 按鈕 Highlight＝滑鼠滑過、Push＝按住。
  - 訊息文字畫在欄位內 (2, 2)，白色。
  - `Moveable=18` 解讀為上緣 18 px 可拖曳。
  - Esc 觸發視窗的 `KeyboardEvent=ClosePersonalShop`。
  - 頁碼格式「目前頁/總頁數」。
  - 兩行訊息合成 chat 的方式：直接相接，最後補 00。原版有沒有分隔字元未查證。
- **待驗證**：
  - `LimitText=20` 算的是字元還是 CP950 byte。目前以 UTF-16 字元計。
  - OK／取消在 y=334、高 17–18，但底圖只有 332 高，窗外那一段原版怎麼顯示。
  - 視窗初始位置。目前照 XML 的 (0, 0)。
- **未實作**：
  - 物品放進格子的拖放（`ItemIcon MouseEvent="Pick"`），以及單價與數量的輸入窗。原版用哪個輸入窗未查證。
  - 文字輸入：原生 UI 契約（`UiKeyEvent`）沒有字元或 IME 事件，目前只能透過 `Setup.SetMessage` 設定。
  - Tooltip（XML 裡的文字是 `test`）。

## 產出路徑
- `engine/source/PrivateShop.cs`（修改）
- `engine/source/NativeUi/PersonalShopPanel.cs`（新檔）
- `engine/source/PersonalShopTests.cs`（新檔）
- `engine/_integration/dev/rebuild-lock.json`（重產）
- `docs/reports/issue-527.md`（本檔）

## 給下一棒的提示
- **整合**：
  - 在 `UiPanelRegistry.Panels` 加一行 `PersonalShopPanel.Registration`。
  - 把背包圖示接到 `IconSource`。
  - 訂閱 `Confirmed`，經 `LoginConnection` 送出。送出前要確認角色在世界中、沒有交易或互動進行中，並用 `PersonalShopPackets.Close()` 處理關店。
  - 自己開店後，伺服器會廣播 S158 動作 70（`S_DoActionShop`）；`WorldObjects` 已經會解析成 `ShopActive`／`ShopText`，可以用來確認開店成功。
- **開窗觸發點**：TW16 由 `sub_B56A40` 依名稱建立窗，指標存在 `dword_1541CF8`。誰呼叫開窗（選單、指令或其他），要先查 BIN。
- **原生 UI 契約**：要能輸入訊息，需要在 C2 加字元／IME 事件。這不在本任務範圍。
- **本機建置**：worktree 要有 `engine/vendor` junction（已被 gitignore），PATH 要有 `py -3` 的 Python。本機跑 `map-continuity` 時，請把 `TW_CLIENT_ROOT` 指到不存在的目錄，避開原客戶端字串表的影響。
