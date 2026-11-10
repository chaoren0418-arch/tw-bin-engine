# Issue #862 — T378：R7 併入項 179 字串提示與狀態窗 Up 確認

依據：`docs/reference/followups-9.md`（FU9）§8、§14.5、§14.6。分支 `task/862`。

## 做了什麼

### 1. 選目標模式的字串 179（FU9§8）
- `engine/source/Magic.cs`
  - `SpellTargeting.Cursor` 的註解改成「字串 179，進入模式時印到聊天欄」，值改成引用 `ChatFollowupCommands.SelectTarget`（同為 179，跟組隊 `/invite` 用同一筆）。名稱沿用 `Cursor`，因為既有測試和 `SpellTargetPick` 欄位都用這個名字。
  - `PickNotice` 原本是引擎自訂的「請點選施法目標（15 格內）；點地面取消」，改成字串 179：有字串表就照表去色碼，沒有表就用本機原文「請選擇對象。」（`ChatFollowupCommands.SelectTargetText`）。
  - `BeginSpellTargetPick` 進入模式時，把這行以 palette[24] 印到聊天欄（系統行，沒有說話者），聊天欄最多保留 100 行，`MagicNotice` 也是同一行字。
  - 游標不換（M35 待真機）。原版 `sub_A11280(179)` 為真時不印，這個條件沒查，引擎一律印（推測）。
- `SkillCast.cs:79` 的「請點選可見的施法目標；」後面接的字也跟著變成字串 179（這個檔沒改）。

### 2. 狀態窗按 Up（FU9§14.5）
- `engine/source/NativeUi/RenewalStatusPanel.cs`
  - 按原生 Up（`StatPointUP`）只在本機預覽：那一項數值顯示 +1（`Pending`），其他 Up 全部隱藏，選中那一項改顯示 Down_Button。不送封包，也不改引擎狀態。
  - 視窗下方出現 Y/N 條（樣式是引擎自訂：暗底、YES／NO 文字鈕 32×14，因為 `sub_EDE380` 的窗沒讀）。提示文字用**本機 string-c.tbl 第 929 行**「請再次確認是否要提升\f2%s\f>？(Y/N)」，`%s` 填能力名（力量…），色碼去掉。TW16 原本用的字串 4471 在本機表裡不存在（本機表只有 3484 筆），1131–1136 在本機是別的內容，所以沒用。929 是照字意找的對應（推測）；沒有字串表時用同一句原文。
  - 「是」（按 YES、Y、Enter）會呼叫 `StatPointConfirm(key)`；「否」（按 NO、N、Esc、Down_Button）只會取消預覽、還原數值。另外，在不能再加點（`Show` 收到 CanRaise=false）、關窗或離開世界時，預覽也會清掉。
  - 能力特效 `sub_EBB500(16,k)` 沒有播：特效的 sprite 和表沒追到（**待驗證**），所以 Y/N 是按下 Up 就馬上出現，不等特效播完。
- `engine/source/BonusStatsUi.cs`：`StatPointUp`（舊行為：按 Up 直接 bonusSelect）換成 `StatPointConfirm`，「是」的時候走 `ConfirmNativeBonus`：如果還沒選過，先 `bonusSelect`，再用更新後的 view 送 `bonusSubmit`（C_Attr 479＋能力名，跟 WinForms 按鈕過同一套閘門）。「否」只動面板、不碰引擎。WinForms 面板與 `--classic-ui` 都沒變。

### 3. §14.6 登入 `Back_Image`
FU9 結論是維持現狀（`LoginPanel.cs` 不畫），這張的可改檔案也不包含它，所以沒動。

## 怎麼驗證（已驗證）
- `dotnet build engine/source/OfflineClient.csproj -c Release -r win-x64 -p:SelfContained=false`（restore 來源指向主專案的 `engine/vendor`）：**成功，0 錯誤**。3 個警告都是既有的（HtmlActionTests、MagicUi），跟這次的改動無關。
- `py -3 engine/tools/check-no-game-assets.py`：`{"status": "pass", "errors": 0}`。
- 字串來源：本機 string-c.tbl 的解出檔（SHA-256 `4ebbc1a7a002a4c1…`，跟 repo `ServerMessageTextTests.cs` 釘住的本機表一致），只讀，放在 scratch，沒有進 repo。內容：第 179 行＝`\f1請選擇對象。`；第 929 行＝`請再次確認是否要提升\f2%s\f>？(Y/N)`；4471 超出範圍（共 3484 筆）；1131–1136 是活動 server、頭盔、時間到、不可丟棄、已擁有、釣魚失敗等別的訊息。
- 離線測試（依規定本機不執行，交給 CI 的 `--module-tests`）：
  - 新增 `engine/source/SpellTargetNoticeTests.cs`（`spell-target-notice`）：檢查 179／色 24／範圍 15、有表和沒有表時的字、進入模式時印一行（文字、色、沒有說話者、版本前進）、取消時不印、最多 100 行。
  - `RenewalStatusPanelTests.cs`（`renewal-status-panel`）新增 `Confirm`：預覽 +1、Up 隱藏／Down 顯示、不送東西、Y/N 條的位置、929 文字（有表、沒有表、各能力名）、YES／NO／Down／Esc／N／Y／Enter、其他按鍵與按住重複會略過、點到條上空白處也會被吃掉、CanRaise 結束／關窗／離開世界都會清掉預覽。`Input` 改成按 Up 只預覽。
  - 既有的 `CombatQueueFollowupTests`（`Cursor==179`、`PickNotice==MagicNotice`）依然成立。

- CI（PR #879）：第一次跑，`renewal-status-panel` 因為測試引用了合成版面裡沒有的 `Int_Label` 而失敗；改用 `HpGauge_Label` 後重跑，`dotnet`、`map-continuity`（所有 `[WorldModuleTest]`）、`npc-offline` **全部通過**。

## 未驗證什麼
- 單元測試本機沒跑（依規定），結果以 CI 為準（已通過）。
- 真機：聊天欄 179 的顏色和位置、進入模式後的游標（M35）、狀態窗 Y/N 的樣子（原版 `sub_EDE380` 的窗）、能力特效 `sub_EBB500(16,k)`、按「是」之後伺服器的回應，都要交給真機（Codex／dot）確認。
- 原生 UI 的鍵盤事件會不會送到 Window 層的這個面板，沒有實機確認；就算送不到，滑鼠的 YES／NO／Down 也能用。
- 929 是不是原版這個 Y/N 實際用的字（TW16 用 4471），只是推測的對應。

## 產出路徑
- `engine/source/Magic.cs`、`engine/source/BonusStatsUi.cs`、`engine/source/NativeUi/RenewalStatusPanel.cs`、`engine/source/NativeUi/RenewalStatusPanelTests.cs`、`engine/source/SpellTargetNoticeTests.cs`（新檔）
- 本報告 `docs/reports/issue-862.md`

## 給下一棒的提示
- 真機核對清單：(1) 選技能、沒有目標時按施法，聊天欄應該出現「請選擇對象。」，顏色同料理書提示（palette 24）；(2) 等級 51 以上、有能力點時開狀態窗按 Up，數值 +1、其他 Up 消失、出現 Down 和 Y/N 條；按 NO 會還原，按 YES 會送 C_Attr 479，等 S155 更新數值。
- 如果要補能力特效，要先追 `sub_EBB500(16,k)` 用的 sprite 和表；Y/N 的原版樣式要讀 `sub_EDE380`。
- 「否」只清面板的預覽，不會清 WinForms 那邊的選擇；原生的流程本來就不先 select，所以兩邊不會互相影響。
