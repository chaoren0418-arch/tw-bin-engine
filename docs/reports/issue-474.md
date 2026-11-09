# Issue #474 ui-red-notice：原生 UI 模組「紅字（畫面中央系統訊息）」

階段 code，runner claude-local，第 1 次嘗試，分支 `task/474`。日期 2026-11-09。

## 結論

依 V2（#457，`docs/reference/ui-layout.md` 第 2 節「紅字與畫面中央系統訊息（V2）」(A) 紅字區）新增原生面板 `RED_NOTICE`。資料來源沿用 `DisplayNotices` 的佇列（`WorldSession.redNotice`，每則 S105 覆蓋前一則），不新增封包解析。離線檢查與本機真實字串表渲染都通過；真機尚未驗收。

| 項目 | 實作（全部取自 V2） |
|---|---|
| 位置 | 5 格，第 k 格 y＝144＋12k（字的上緣）；x＝400 − CP950 byte 數×6/2（跳脫碼也算 byte） |
| 分格 | 1829 → 第 0 格；554 → 第 1 格；553 → 第 2 格；其他編號 → 第 4 格（y＝192）；第 3 格永遠空著 |
| 時長 | 第 4 格從該則的到達時刻起 **15 秒**後清除；第 0–2 格沒有計時器；世界重設（`WorldEpoch` 改變）時 5 格全部清空 |
| 行數 | 一般紅字只有 1 行，新的直接覆蓋舊的；一則訊息永遠畫成一行，不折行 |
| 字色 | 調色盤[3] #FF0000；1 px 黑外框（調色盤[0]，上下左右 ±1 px）；**沒有底色** |
| 字中換色 | `\f<c>` 換成調色盤[c−'0']（只有已讀出的 0–3）；`\<` 回到這一行的起始色 |
| 文字 | 有 `string-c.tbl` 時照 `sub_94FB00`＋`sub_933850`：1006 改成 1017；編號 ≥ 表的筆數時不處理；`%n` 用 `ServerMessageText.Format`（既有的 `sub_933850` 移植）。沒有字串表時改用 `DisplayNotices` 的文字（引擎替代文字，不是原文）。`$nnn` 名稱用既有的 `ResolveNpcText` 換掉，換完才計算寬度 |

## 做了什麼

| 檔案 | 內容 | 依據 |
|---|---|---|
| `engine/source/NativeUi/RedNoticePanel.cs`（新） | 面板本體，以及帶 `[UiPanel(150)]` 的註冊行 | 第 5.2 節的模組檔；註冊表自 T270（#645）起改為在面板旁宣告 `[UiPanel]`，`UiPanelRegistry.cs` 不再加行 |
| `engine/source/NativeUi/State/RedNoticeState.cs`（新） | `RedNoticeState`／`UiRedNoticeGameState` 轉接；`WorldSession.LatestRedNotice`（不套舊 HUD 的 5 秒窗）；`ClientForm.WithRedNoticeState`、`NativeRedNoticeActive` | 第 5.2 節的 `State/<Module>State.cs` |
| `engine/_integration/dev/native-ui-tests/RedNoticeChecks.cs`（新） | 離線檢查組 `native-ui-red-notice`（也排進預設組 Order 126）；選用的真實客戶端渲染 | 第 5.2 節 |
| `engine/source/NativeUi/UiClientBridge.cs`（改 1 行） | 把轉接放在 bridge 鏈最內層：`WithBuffState(WithRedNoticeState(basic))` | **超出第 5.2 節，理由：** 面板拿到狀態的唯一入口就是這條鏈，buff（#473）也是這樣接上的。放在最內層，其他模組的 `Of()` 都會先找到自己的那一層，不受影響 |
| `engine/source/DisplayNotices.cs`（改 1 行） | 原生面板啟用、而且沒有被停用時，不畫舊的黑底紅字 | **超出第 5.2 節，理由：** `PaintDisplayNotices` 是在 `PaintScene` 裡呼叫的，原生模式下也會執行。不加這行的話，同一則 S105 會畫兩次：一次黑底、位於 1/3 高，一次是原生版本。舊 HUD 模式（開關關閉）行為不變 |
| `engine/_integration/dev/rebuild-lock.json` | 用 `rebuild.py --update-lock` 重產（322 個來源、331 個鎖定檔） | 第 5.2 節 |

### 設計決定

- **圖層用 `World`，不用 `Float`**（與第 3.1 節的圖層表不同）。`UiInput` 只要 `Bounds` 命中就會把滑鼠交給面板（`Route=Panel`），即使 `Mouse()` 回傳 false。紅字區橫跨畫面中央，用 `Float` 會擋住點擊地面、怪物與 NPC。原版是直接畫在主畫面上（`sub_F0F0C0`），不接收輸入。`World` 層面板畫在世界之上、所有底列與視窗之下，而且永遠不參與命中測試。紅字和視窗重疊時，原版誰在上面**待驗證**。
- **三個專用格（1829／554／553）有做**（#457 交給 Claude 決定）。這是 V2 已驗證的行為，而且不需要新的資料：面板每一幀看 `DisplayNotices` 的最新一則，每則只處理一次（以參考比對），然後放進對應的格子。
- **只看最新一則：** `DisplayNotices` 只存一則。兩則 S105 落在同一幀之間時，只有後一則會進格子（例如 1829 緊接著一般紅字時，1829 不會出現）。實際上很少發生，已記錄。
- 中央綠字佇列（V2 (B)）**不在本任務**，見「給下一棒的提示」。

## 怎麼驗證（已驗證）

- **建置：** worktree 沒有 `engine/vendor`，依先例（#13、#115）建了唯讀 junction 指向主專案的 `engine/vendor`；`engine/assets` 也一樣建了 junction。沒有複製任何素材。`rebuild.py --build` PASS，product DLL SHA-256 `9e8bec918c2777d22ba421ede52a7d4a2fd3958ce62bb51fbe1b75db2611b58d`。
- **DemoChecks `native-ui-red-notice`：** PASS（361 項，包含共用的前置組）。全部用程式合成的 `RedNotice`、字串表與狀態：
  - 座標：第 0／1／2／4 格的原點；CP950 一個中文字 2 byte；`\f3` 算 3 byte
  - 分格對照；調色盤 0–3 的色值
  - 換色分段：`\f2`、`\<`、`\f1`、未讀出的調色盤項目、結尾的反斜線
  - 文字：`%n` 加上 `\<`、1006→1017、編號 ≥ 筆數時忽略、沒有字串表時的替代文字、`$nnn` 先換再量寬度
  - 狀態轉換：第 14.999 秒仍顯示、第 15 秒清除；同一則不重複出現；第 0–2 格不消失；新的覆蓋舊的；15 秒從最新一則起算；換世界時全部清空；不在世界中、沒有轉接時都不畫；buff 包裝之下也找得到轉接
  - 不接收滑鼠
  - 像素：畫面上只有 #FF0000 與 #000000 兩種顏色、沒有底色方塊、字在該行的範圍內
- **本機選用檢查**（`TW_CLIENT_ROOT=D:\lineage開發\0-客戶端`，真實 `string-c.tbl`：SHA `4ebbc1a7…2d43e9`、3484 筆）：PNG 與 JSON 只輸出到 `scratch/issue-474/real/`。

  | 格 | 編號 | 原文（真實字串表） | byte | x | y |
  |---|---|---|---|---|---|
  | 0 | 1829 | 跟AuthD伺服器的連接斷了，請迅速處理。 | 37 | 289 | 144 |
  | 2 | 553 | `\f3`NPC伺服器暫時關閉。 請等候重新啟動。 | 39 | 283 | 168 |
  | 4 | 551（5，100） | 你已經殺了 5`\<` 人。 如果你PK的次數達到 100`\<`， 你將會被打入地獄。 | 65 | 205 | 192 |

  目視結果：紅字、1 px 黑外框、沒有底色，三行的位置與上表相同，`\f3`／`\<` 沒有被畫成字元。
- **其他組（確認沒有影響）：** `auto-discovery`、`native-ui-canvas`、`native-ui-buff-icons`、`native-ui-macro`、`native-ui-party`、`native-ui-chat-window`、`native-ui-hpmp`、`native-ui-left-status`、`native-ui-menu-buttons`、`native-ui-minimap` 全部 PASS。`display-notices` 在沒有客戶端（`TW_CLIENT_ROOT` 指向不存在的目錄）時 PASS（388 項）。
- **預設全組：** 一路通過到 `CombatLoop`（含 Movement123 與全部原生 UI 組），停在 `DisplayNoticeChecks` 第 30 行「notice S245 unknown message stays labeled」。
  - 原因：本機 `GroundSpriteReader.ClientRoot` 預設會讀真實客戶端，因此載入了真實的 `string-c.tbl`，S245 編號 999 的文字跟測試預期的替代文字不同。
  - 這是 S245，與本任務無關，main 上也一樣；沒有客戶端時這一組會通過。**完整全組以 CI 為準。**
- `git status`：沒有新增任何 `.img`、`.xml`、`.png` 或二進位遊戲資料；scratch 與兩個 junction 都被 `.gitignore` 排除。

### 2026-11-09 與 main 同步

- PR #662 與 main 衝突（落後 86 個 commit），只有 `rebuild-lock.json` 衝突：取 main 版本後用 `rebuild.py --update-lock` 重產（341 個來源、350 個鎖定檔），沒有手動拼 hash。
- 同步後 `rebuild.py --build` PASS，product DLL SHA-256 `760d9722a4ccd47b8933fcffbcc1c68934ead6e12876321e75275d56878006a0`。
- 依 2026-11-09 本機限制，同步後**沒有在本機執行** DemoChecks 或任何建置出來的程式；同步後的檢查結果以 PR 的 CI 為準。

## 未驗證什麼（待驗證／推測）

- **待驗證：**
  - 真機：在 P24Lab 送一則 S105，核對 y＝192、15 秒、黑外框（ui-native-smoke）
  - 實際遊玩的 `LiTo.bin`（加殼）是否跟 TW16 相同
  - 紅字與視窗、底列重疊時誰在上面
  - 字型：沿用 `UiText` 的暫定字型（新細明體 9pt ≈ 12 px，V4 (f)）。實測 "WARNING 12"（10 byte）畫出來大約 70 px 寬，原版用 60 px 估寬；置中用的是原版的估寬公式，所以只會影響字的右緣
  - 調色盤第 4 項以後的值沒有讀出；遇到 `\f4`–`\f9` 時維持目前顏色，並記一行 `UiResourceLog`
  - `\F<c>`：本面板當成一般字元畫出（V2 沒有記載 `sub_841770` 怎麼處理它）
- **推測，沒有實作：**
  - 前一則的 15 秒計時器提早清掉後一則（V2 推測 `sub_A22830` 不去重）
  - 超過 131 byte 時溢位到下一格
  - `sub_F067B0`／`sub_F12610` 進入世界時重新排一次清除
- **過渡：** 沒有字串表時用的是引擎的 `DisplayNotices` 文字，不是原文。例如 551 會顯示「你的PK次數已達…」，不是表中的原句。

## 產出路徑

- `engine/source/NativeUi/RedNoticePanel.cs`
- `engine/source/NativeUi/State/RedNoticeState.cs`
- `engine/_integration/dev/native-ui-tests/RedNoticeChecks.cs`
- `engine/source/NativeUi/UiClientBridge.cs`、`engine/source/DisplayNotices.cs`（各 1 行）
- `engine/_integration/dev/rebuild-lock.json`
- `.github/workflows/build.yml`（加一個 CI 步驟）
- `docs/reports/issue-474.md`（本檔）

## 給下一棒的提示

- **ui-native-smoke（dot）：**
  - 開 `nativeUi` 後在 P24Lab 讓伺服器送 S105，例如 PK 次數訊息 551／552，或關進地獄時的 637
  - 截取邏輯畫布，核對第 4 格 y＝192、x＝400−byte×3
  - 確認 15 秒後消失，而且舊的黑底紅字不會同時出現
  - 確認點擊紅字下方的地面仍然會移動
- **中央綠字佇列（V2 (B)）** 引擎還沒有：`S_MESSAGE` 類型 26／28、`S_MESSAGE_CODE` 3599／4433／3736／565／4509／3902／4207／3015 等，最多 14 筆，最新一筆在 y＝121、每行 15 px。建議另開一張 code 任務（例如 `ui-center-queue`），不要塞進本面板。
- 這個面板是 `World` 層。之後如果框架加入「`Float` 層但不參與命中」的選項，可以改回第 3.1 節的圖層規劃。
- CI 不跑預設全組，所以本任務在 `.github/workflows/build.yml` 加了一個 `native-ui-red-notice` 步驟，與 #473 的 buff 步驟相同；這是第 5.2 節以外的第 3 個檔案。CI 沒有客戶端，選用的真實渲染會自動 SKIP。
