# Issue #806 T343：R7-D HTML 動作

依據 `docs/reference/gap-matrix.md` 第 15.6 節 R7-D，以及 `docs/reference/followups-6.md`（FU6）§1.1、§3.2、§4.1。

## 做了什麼

**HTML 動作分派**（`engine/source/NpcInteraction.cs`）

NPC 頁走 `ClientHyperAction`，伺服器主動頁走 `QueryOption`。兩條路都先問新的 `HtmlClientAction`：是本組的動作就由客戶端自己處理，不再當 C125 送出。

| 動作 | 行為（FU6） | 送出 |
|---|---|---|
| `declarewar` | 名稱取 HTML 參數 1（r29 城門守衛頁的 `#1`，也就是城盟名）。檢查用 `sub_F46290` 的規則，門檻 25：非王族印 478；階級不是 0／4／10 印 785；已在交戰印 234；變身中不送也不提示；等級不足印 **475**；沒有血盟印 272；名稱空白不送 | C227 type 0 |
| `surrender`／`cease` | 職業碼 ≠ 0 時什麼都不做；階級不是 4／10 印 785；其他情況開名稱框 **578**／**579**，名稱非空才送 | C227 type 2／3 |
| `pledgefocusadd` | 開名稱框 **3361**，沒有本機檢查；名稱非空才送（呼叫既有的 `AddClanAttention`） | C129 子碼 0 |
| `propose`／`divorce` | 只送包，不檢查；送出後的提示與 `/propose`、`/divorce` 相同（`MarriageCommandSent`） | C50 0／1 |

- 動作名稱比對時去掉前後空白、不分大小寫，與 `checkpk`／`accepttrade` 的做法一樣。`war`、`pledgefocus` 不屬於本組，照舊送 C125。
- 名稱框的狀態放在 `WorldSession.HtmlNameInput`，並綁定開框當下的頁版本。換頁、關對話、關伺服器頁或世界改變時，框自動失效。開框與關框時頁版本會 +1。
- `SubmitHtmlName` 先關框再送：名稱空白時不送。`CancelHtmlName` 只關框。
- 送出後頁面不關（原版的關窗時機沒有查）。

**名稱框**（`engine/source/NativeUi/HyperTextPanel.cs`）

- `HyperTextView` 新增選填欄位 `Name`（`HyperTextNameBox`）。頁內依序畫：空行、提示文字、名稱列（輸入框 108＋「確定」＋「取消」，共 192 寬）。
- 新框出現時清空並取得焦點。收一般字，不收空白與控制字元；上限 16 個 Big5 位元組，遊戲字集無法表示的字不收。
- 按鍵：Backspace 刪一字、Enter 確定、Esc 取消；點窗內其他地方會放開焦點。
- `IHyperTextHost` 新增 `SubmitName`／`CancelName` 兩個方法，預設不做事。既有的宿主與測試不用改。

**宿主接線**（`engine/source/NpcUi.cs`，**超出任務列出的檔案範圍，說明如下**）

名稱框的資料要由宿主放進 `HyperTextView`，按下確定或取消也要由宿主轉給 `LoginConnection`。宿主就是 `NpcUi.cs` 的 `NativeHyperTextHost`，不改它，名稱框只會是測試才呼叫得到的程式（gap-matrix「只有測試呼叫」表列的那類問題）。所以只做了下面這些接線：

- `NpcHyperTextView`／`QueryHyperTextView` 傳入 `HtmlNameBox(...)`。
- `NativeHyperTextHost` 實作 `SubmitName`／`CancelName`。
- 原生 UI 關閉（`--classic-ui`）時，WinForms 頁下方顯示「提示＋文字框＋確定＋取消」（`AddHtmlNamePrompt`），保留 WinForms 入口。

第 15.6 節沒有其他組分到 `NpcUi.cs`，目前也沒有開著的 PR 改它。

**測試**（新檔 `engine/source/HtmlActionTests.cs`，`[WorldModuleTest("html-actions")]` 自動發現）

- 名稱表與正規化、提示字串 id、門檻 25 與 475。
- declarewar：成功送出、等級 24 印 475、印 478／785、變身中不送、沒有參數 1、沒有血盟印 272。
- surrender／cease：開框、版本不符、送出 type 2／3、空白不送、取消、換頁或關對話後框失效、印 785、非王族不動作。
- pledgefocusadd：在伺服器主動頁開框，送出 C129 子碼 0。
- propose／divorce：送出 C50 0／1。
- 其他動作照舊送 C125。
- 面板：名稱列、焦點、收字規則、16 byte 上限、Enter／Esc／點擊、不能送時的狀態、與數字框並存、舊宿主走預設方法。
- 接線：用反射確認 NpcUi 的宿主與 WinForms 備援都存在。

## 怎麼驗證

- **已驗證（本機）**：`dotnet build engine/source/OfflineClient.csproj -c Release` 編譯成功，本組三個檔案沒有新警告。
  - 這台機器的 WebView2 套件會觸發 NETSDK1130（`rebuild.py --build` 也一樣，與本 PR 無關）。編譯檢查時改用只放在 `scratch/no-winmd.targets` 的 MSBuild hook，透過 `-p:CustomAfterMicrosoftCommonTargets` 掛上，拿掉 `.winmd` 參考。沒有改 repo 的建置檔。
- **已驗證（本機）**：`py -3 engine/tools/check-no-game-assets.py` → `status: pass`。
- **已驗證（CI，PR #828，run 42814123259）**：`dotnet`、`map-continuity`、`npc-offline` 三個 job 全綠。`--module-tests` 全部 `failed: 0`，其中 `html-actions` 共 80 項全部通過。依本機限制，沒有在本機執行任何編譯出來的程式。
- 沒有改 `rebuild-lock.json`、`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`。

## 未驗證什麼

- **推測**：
  - 名稱框畫在 HYPER_TEXT_WINDOW 頁內是引擎自訂。原版是共用的 InputWindow（`sub_AC98E0`），外觀與位置沒有查證，所以不畫替代圖，只用文字鈕。
  - 名稱上限 16 byte 依 C129 的緩衝（0x11）推定；C227 用同一個框也是推測。
  - 送出後頁面不關。
- **待驗證（真機）**：
  - 城門守衛頁的宣戰。
  - 血盟選項頁 `optionplp` 的投降、停戰、新增關注（伺服器實際送哪一種頁，以及頁上的參數）。
  - 輸入法輸入中文名稱到名稱框。
  - 名稱框的 Esc 與全域 ESC（R7-H）之間誰先處理。
- **沒做**：web 面板（`InteractionWeb`）沒有名稱框；`pledgefocus`／`mypledgefocus` 沒追。
- **資源**：本組沒有宣稱任何資源缺少，所以沒有跑 PakViewer。提示文字用 `string-c.tbl` 的 578／579／3361，沒有字串表時用 FU6 記下的同義文字（`PledgePanel.PromptText`）。

## 產出路徑

- `engine/source/NpcInteraction.cs`：HyperAct 分派與 R7-D 區段
- `engine/source/NativeUi/HyperTextPanel.cs`：名稱框
- `engine/source/NpcUi.cs`：宿主接線與 WinForms 備援（範圍外，說明見上）
- `engine/source/HtmlActionTests.cs`：離線測試
- `docs/reports/issue-806.md`：本報告

## 給下一棒的提示

- 真機時 Codex／dot 請在 P24Lab 用王族測試角色（等級 25 以上、血盟君主）核對以下幾點：
  1. 城門守衛頁「向 X 血盟宣戰」會送出 C227 type 0，伺服器回 S84。
  2. 等級 24 時聊天欄印 475。
  3. 血盟選項頁的投降／停戰開出 578／579 名稱框，輸入後送出 C227 type 2／3。
  4. 「新增關注」開出 3361 名稱框，送出後收到 S200 type 2。
- R7-H（全域 ESC）若要攔 Esc，請注意名稱框有焦點時，`HyperTextPanel.Key` 會先把 Esc 吃掉當取消。
- gap-matrix「只有測試呼叫」表中（R7-D 那一列）的 `ClanWarProtocol.HtmlSurrenderOrCease` 與 `CheckDeclare` 的 HTML 門檻，現在已經接上產品路徑。
