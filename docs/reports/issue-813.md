# Issue #813 — T350：R7-K 血盟備註

依據：`docs/reference/gap-matrix.md` 15.6 節 R7-K（T339／#800）；WIN2§3（`docs/reference/windows-2.md` 第 3 節：PledgeUI 建構子註冊的事件含 `ModifyMemo`）；`ClanMatching.cs:127`（`ClanMatchingProtocol.Notes` → C78 16）。

## 做了什麼
- **原生血盟窗 ModifyMemo**（`engine/source/NativeUi/PledgePanel.cs`）：
  - 版面：讀 `ModifyMemo_Button`（本機 `PledgeUI.xml` 兩個窗都有，(217,24)，BaseSurf 4452，`MouseEvent="ModifyMemo"`）；XML 沒這個節點就沒有入口（選用節點，不補座標）。
  - 第一次按：開始編輯自己那一列的備註，草稿＝目前收到的 169 備註；自己那列不在畫面上時捲過去。自己不在上線名單時仍可編輯，草稿畫在側欄輸入框。不知道自己的角色名（`IUiGameState.CharacterName`）或沒接上來源就不進編輯。
  - 再按一次或 Enter：送 C78 16；Esc 取消；Backspace 刪字。送出後**不先改本機備註**，等伺服器回送 S250/169。
  - 上限：`MemoByteLimit = 62 byte`（= S250/169 的固定欄寬，`PledgeProtocol.NotesLength`）。3.81 的 C_PledgeContent 16 本身沒有長度檢查，但超過 62 byte 就沒辦法透過 169 回送，所以用這個上限（推定）。字元規則沿用 `ClanMatchingProtocol.Notes`：控制字元、代理字元、不能用 Big5 編碼的字都擋。判斷集中在 `PledgePanel.MemoError`。
  - 互斥：備註編輯、公告編輯、側欄輸入框同時只能有一個；換窗或關窗都放棄草稿。
  - `IPledgeWindowSource` 新增 `SendMemo(text)`，預設實作是「沒接上」（回 false）。
  - **更正過時註解**：原 `:227` 寫「只做資料、不畫」，改成「畫在引擎自訂側欄（R4-E／T316 起）」；類別說明的「寫備註（ModifyMemo）不做」改成本組的行為說明。
- **產品端與 classic UI**（`engine/source/ClanUi.cs`）：
  - `WorldPledgeSource.SendMemo` 經既有的 `LoginConnection.SendClanContent(v, ContentNotes, text)` 送出。條件：有血盟、可送出、通過 `MemoError`。
  - WinForms 血盟窗（`--classic-ui`）加「自己的成員備註」輸入框與「送出備註」鈕，走同一條送出路徑。
- **新離線測試** `engine/source/PledgeMemoTests.cs`（`[WorldModuleTest("pledge-memo")]`，自動發現；沒有改 `Program.cs`、`DemoChecks.csproj` 或任何註冊表）。涵蓋：
  - 線上格式 `4E 10 S(備註) 00` 加補齊，以及空備註；
  - 62 byte 邊界：31 個中文字可以，32 個不行；63 個 ASCII 不行；控制字元不行；62 byte 的備註能經 169 往返；
  - 面板：按鈕座標；沒有來源不編輯；點鈕開始編輯並帶入目前備註；輸入與刪字；草稿以編輯色畫在自己那列；Enter 和再按一次都會送出；來源拒絕時保持編輯；Esc 取消、不關窗；超過上限的字不收；和側欄輸入框互斥；換窗、關窗放棄草稿；自動捲到自己那列；不在名單也能編輯；沒有角色名或沒有按鈕就沒有入口；收到 169 後草稿從新備註開始；
  - 產品端：記憶體內假傳送端，`WorldPledgeSource.SendMemo` 送出的位元組等於 `Notes(text)`；超過 62 byte 不送；沒有血盟不送；公告路徑（C78 15）不受影響；介面預設 `SendMemo` 回 false。

## 怎麼驗證
- **本機只編譯，沒有執行任何編譯出來的程式**：
  - `dotnet build engine/source/OfflineClient.csproj -c Release`：0 錯誤，本組三個檔案沒有警告；
  - `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：0 錯誤。
- 本機建置環境：WebView2 的 NETSDK1130 問題和 #812 相同，處理方式也相同：
  - 以 `NUGET_PACKAGES=scratch/issue-813/nuget-packages` 從 nuget.org 還原；
  - 用只放在 `scratch/issue-813/no-winmd.targets` 的 hook 拿掉 winmd；
  - 沒有改 repo 的建置檔。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`，errors 0。
- 沒有改 `rebuild-lock.json`、`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`。
- **CI（PR #837，run 42842682653／42842682544）**：`dotnet`、`map-continuity`、`npc-offline` 三個 job 都通過。
  - `--module-tests` 全部 `failed: 0`；
  - `pledge-memo`（`P06.PledgeMemoTests.Run`）55 項全部通過；
  - 既有的 `pledge-panel` 也通過。

## 未驗證什麼（推定／待真機）
- 原版 ModifyMemo 的編輯框位置、按鍵、輸入上限沒查 BIN。現在的做法是：畫在自己那列的 Memo_Label，Enter 送出、Esc 取消，上限 62 byte，都是推測。
- 公告窗的 `Noticebutton_Control` 在 (183,24)、寬 66，`ModifyMemo_Button` 在 (217,24)，兩者在 XML 上重疊。點擊判定先看公告鈕、再看備註鈕。4488／4452 實際有多寬、兩顆鈕會不會蓋到彼此，待真機看畫面。
- 伺服器收到 C78 16 後回送 169、原生窗更新備註，這段待 P24Lab 真機驗證。
- 階級 8／7／9／3 的對應（M12）本組沒動，仍是推定。
- 沒有宣稱缺少任何資源。使用的 surf 4452 是本機 `PledgeUI.xml` 寫的，執行期從 Tile.pak 讀取，沒有放進 repo。

## 產出路徑
- `engine/source/NativeUi/PledgePanel.cs`、`engine/source/ClanUi.cs`、`engine/source/PledgeMemoTests.cs`（新）
- 本報告 `docs/reports/issue-813.md`
- 分支 `task/813`

## 給下一棒的提示
- 真機驗收（P24Lab 有血盟的測試角色）：
  1. 開血盟窗（S250/171），按備註鈕，輸入後 Enter，應送 `4E 10 <Big5> 00`；
  2. 伺服器回 169 後，自己那列的備註應更新；
  3. 輸入 31 個中文字應能送出，第 32 個字應收不進去。
- 若查到原版 ModifyMemo 的處理函式（PledgeUI 建構子 `sub_B2A0C0` 註冊的事件），只要改 `PledgePanel.BeginMemoEdit`／`SaveMemo` 和 `MemoByteLimit`。
- 送出路徑只有 `WorldPledgeSource.SendMemo` 一個入口；classic UI 也走 `SendClanContent`。
