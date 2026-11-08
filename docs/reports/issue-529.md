# Issue #529 — T222：M7-V 好友、封鎖、信件封鎖三窗（code）

分支：`task/529`。依據：`docs/reference/windows.md` §1.2（WIN§1.2）、`docs/reference/gap-matrix.md` 第 7 節 M7-V、`docs/design/native-ui.md` §2、§5。

## 做了什麼

1. **新檔 `engine/source/NativeUi/FriendPanel.cs`**：三窗共用的原生面板類別 `FriendPanel`（實作 `IUiMovablePanel`）。
   - `FriendWindowSpec` 定義三窗之間不同的地方：根節點、視窗名稱、標題圖節點、事件名稱。
     - `FriendUI`／`Friend_Window`
     - `ExcludeUI`／`Exclude_Window`
     - `MailExcludeUI`／`Mailexclude_Window`
   - 版面在執行期從原客戶端 `Tile.pak` 的 XML 讀取，程式裡不寫座標。讀取的元素：
     - 底圖、框
     - 關閉鈕 4429／4430
     - 標題圖 4992／4993／4994
     - `Item_List` 的 **14 列** `RadioButton`，選取圖 4995
     - `Info_Label` 的位置
     - `Bestlist_Scrollbar`：底圖 4060、上鈕 4061/4062、捲軸頭 4063/4064、下鈕 4065/4066，以及 `ScrollStart`／`ScrollHeight`
     - `Count_Label`
   - 這三個 XML 用的是大寫屬性（`X`、`BaseSurf = "4990;1"`），和 C1 的 `surfNumber="a~b"` 格式不同，所以另外寫了 `FriendSurf` 解析器。
   - 已做的行為：
     - `Activate=0`，預設關閉；可開關。
     - 選列（以名稱記住選取；名單更新後名字不在了就取消選取）。
     - 捲動：上下鈕、滾輪、點或拖捲軸軌道。捲軸頭位置依 `ScrollStart`＋`ScrollHeight` 換算。
     - 人數標籤。
     - 關閉鈕。
     - 上方 `Moveable` px 可拖曳。
     - 缺 surf 時不畫，不畫替代色塊。
   - 資料走唯讀介面 `IFriendListSource`。面板不碰 `LoginConnection`，也不送封包。
2. **`engine/source/Buddies.cs`**：補上**信件封鎖名單**。
   - `WorldSession.MailExcludedNames`／`IsMailExcluded`／`ToggleMailExclude`。
   - `LoginConnection.ToggleMailExclude(name, view)`，用和好友名單相同的世界存取與版本戳檢查。
   - `BuddyView` 多一個 `MailExcluded` 欄位。
   - 世界重置時清空。
   - 名稱檢查沿用 `BuddyPackets.ValidateName`（改成 `internal`）。
3. **`engine/source/BuddyUi.cs`**：
   - 舊的 `BuddyPanel`（開發工具分頁）多一欄「信件封鎖（僅本機）」，加上「信件封鎖／解除」鈕。
   - 加三個「原生好友窗／原生封鎖窗／原生信件封鎖窗」開關鈕，屬於過渡入口；原生 UI 沒啟用時會顯示提示。
   - 新增轉接器 `LoginFriendListSource`，從 `BuddySnapshot()` 讀三份名單。
   - `ClientForm` 在 `InitBuddyUi` 設定 `FriendPanels.Source`，並提供 `ToggleNativeFriendWindow`。
4. **`engine/source/NativeUi/UiPanelRegistry.cs`**：照 native-ui.md §5.2 只加一行 `..FriendPanels.Registrations`（三窗）。**這個檔不在任務列出的可改清單內**，但 §5.2 規定每個模組在這裡加一行，PR 說明有寫明。
5. **新檔 `engine/source/NativeUi/FriendPanelTests.cs`**：用 `[WorldModuleTest]` 接上既有的 `--module-tests` 入口（CI 會跑）。
   - `friend-panel`：104 項檢查，只用合成 XML 與合成 `.img`。
   - `friend-panel-real`：只有設了 `TW_NATIVE_UI_REAL=1` 才會讀真實客戶端；CI 只記一筆略過。
6. `engine/_integration/dev/rebuild-lock.json`：用 `rebuild.py --update-lock` 重產。

## 怎麼驗證

| 項目 | 結果 |
|---|---|
| `rebuild.py --update-lock` → `rebuild.py --build` | PASS；第三次同步 origin/main（含 #464–#468、#526–#528、#602）後重驗 productDll `ae0c05f0…` |
| `P11.ClientEngine.exe --module-tests` | pass；第三次同步後 23 組、1214 項檢查，`friend-panel` 104 項全過 |
| demo-tests：CI 會跑的 15 組（map-continuity、combat-admission、item-status、native-ui-resources、native-ui-canvas、native-ui-macro、afflictions、resurrection、polymorph-cadence、buddies、item-transfer、doors、pickup、door-approach、hypertext） | 全部 PASS（第三次同步後重跑；native-ui-resources、native-ui-macro 不設 `TW_CLIENT_ROOT`，其餘 13 組把它指到不存在的路徑，見環境備註） |

第三次同步的處理：
- `UiPanelRegistry.cs` 衝突：兩邊的行都保留，`FriendPanels.Registrations` 放在最後一行。
- `rebuild-lock.json`：取 main 版本後用 `--update-lock` 重產。
- **範圍外的一行修改**：`engine/_integration/dev/native-ui-tests/MacroChecks.cs`（#466 的測試）用真實註冊表，但合成的 Tile.pak 只有 `uistatus86`，原本用 Id 黑名單排除別的版面（TRADE_WINDOW）。合併後 FriendUI 三窗也進了註冊表，`native-ui-macro` 因為找不到 `FriendUI.xml` 失敗。改成「只取 Layout 為 `uistatus86` 的註冊，再排除 LEFT／CENTER」，以後其他 M7 面板加進註冊表時不必再改這個測試。測試內容沒有其他改變（403 項全過）。
| `engine/tools/check-no-game-assets.py` | pass，errors 0 |
| 本機選用檢查 `friend-panel-real`（真實 `Tile.pak`／`Sprite*.pak`，唯讀） | pass，45 項 |

本機選用檢查的內容：
- 三個 XML 都能解析。
- 14 列、列／人數／捲軸座標與 WIN§1.2 一致，標題 surf 是 4992–4994。
- 9 個 surf 都找得到。
- 視窗尺寸等於底圖 4990 的尺寸。
- 渲染出的 PNG 只放在 `scratch/issue-529/real/`（不進 git）。目視確認標題「好友名單」「信件黑名單」、選取底、捲軸位置都正確。

合成測試涵蓋：
- 註冊與 id
- surf 解析
- 各元件的邏輯座標
- 缺節點、根節點錯、視窗名錯、列高超過清單時都會拒絕
- 底圖缺時以 XML 範圍當命中矩形
- 不在世界中時名單為空
- 捲動與夾限
- 選取，以及名單更新後取消選取
- 拖曳區與移動
- 關閉
- 繪製：選取底、捲軸頭、缺 surf 不畫
- 信件封鎖的新增、移除、不分大小寫、過期 view 拒絕、名稱檢查、重置清空、不在世界時拒絕、轉接器

**環境備註：**
- 本機有原客戶端時，`map-continuity` 內的 `DisplayNoticeChecks` 會失敗（S245 訊息 999 被本機字串表辨識）。把 `TW_CLIENT_ROOT` 指到不存在的路徑就會通過。和本任務無關；main 在 CI 上是綠的。
- 不分組的 demo-tests 全跑需要 `engine/assets` junction。本 worktree 沒有建立這個 junction，所以只跑 CI 會跑的分組。
- 建置時 `engine/vendor` 以 junction 指向原專案的離線套件，做法同 task-527／528；這個目錄已被 gitignore。

## 已驗證／推測／待驗證

- **已驗證**（XML 與真實資源載入）：
  - 三窗版面相同、14 列
  - 列／Info／捲軸／人數標籤／關閉鈕座標
  - 各 surf 編號
  - 預設關閉
- **推測**（已實作，但標為推測）：
  - `Moveable="20"` 是可拖曳的上方高度。
  - surf `a;n` 的第二個數字沒有使用。
  - 人數標籤只顯示人數。
  - 名稱與人數的位置是左上 +2 px，白字，字型沿用 `UiText` 預設（V4 未定）。真實渲染顯示這個字型比 16 px 的列高還大，等 V4 字型查證後由 `UiText` 統一修正。
- **待驗證**（沒有做成畫面）：
  - `Info_Label` 顯示什麼：不畫，只保留位置與資料（好友的 `Online`）。
  - 開窗的快捷鍵或指令：目前只有開發工具裡的過渡鈕。
  - `FriendName`／`ExcludeName`／`MailExcludeName` 事件的效果：目前只做選取，不送封包。
  - 鍵盤事件：面板不吃鍵，按鍵照常交給遊戲。
  - 名單上限。

## 沒驗證什麼

- **沒有啟動遊戲、伺服器或 DB**；沒有真機開窗驗收。
- **信件封鎖沒有伺服器協定**：`server-protocol.md` 只有 C_Exclude，唯讀搜尋伺服器 `src` 也沒有 mail exclude。所以這份名單：
  - 只存在本機記憶體，重新連線就清空；
  - **尚未用來過濾信件**，因為 `Mail.cs` 不在本任務範圍。
  UI 提示文字有寫明「僅本機」。
- 原生窗不提供新增或刪除好友：原版的操作入口待驗證，目前仍用開發工具分頁的舊按鈕。
- CI 結果以 PR 頁面為準。

## 產出路徑

- `engine/source/NativeUi/FriendPanel.cs`（新）
- `engine/source/NativeUi/FriendPanelTests.cs`（新）
- `engine/source/Buddies.cs`、`engine/source/BuddyUi.cs`
- `engine/source/NativeUi/UiPanelRegistry.cs`（一行）
- `engine/_integration/dev/native-ui-tests/MacroChecks.cs`（一行，同步 main 後修測試過濾條件）
- `engine/_integration/dev/rebuild-lock.json`
- `docs/reports/issue-529.md`

## 給下一棒的提示

- **信件過濾**：負責 `Mail.cs` 的任務可以在收信時呼叫 `World.IsMailExcluded(sender)`。要不要持久化（例如放進 `CharacterConfig`），需要另外裁定。
- **開窗方式**：要等 FriendUI 開窗鍵或指令查證（從 `sub_B17DD0` 的呼叫者與 `KeyboardEvent="FriendWindow"` 往上追）。查到後在 `ClientForm.ToggleNativeFriendWindow` 接上原版入口，並拿掉開發工具裡的過渡鈕。
- **Info 欄**：查證 `Info_Label` 的內容後，在 `FriendPanel.Paint` 補畫。資料已經在 `FriendListRow.Online`。
- **其他 M7 原生窗合併**：會和本任務在 `UiPanelRegistry.cs` 衝突，處理方式是兩邊的行都保留。
