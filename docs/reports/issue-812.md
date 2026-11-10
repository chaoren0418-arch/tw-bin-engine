# Issue #812 — T349：R7-J 隊伍委任

依據：`docs/reference/gap-matrix.md` 15.6 節 R7-J；協定 `docs/protocol/party.md:21`。

## 做了什麼
- **C230 type 3 封包**（`engine/source/Party.cs`）：新增 `PartyPackets.Delegate(memberId)`，格式 `C230 C(3) D(memberObjectId)`，補零到 8 bytes；ID 為 0 時拒絕。格式依據：伺服器 `clientpackets/C_CreateParty.java`（SHA-256 `cd0f739d…9b9c`，見 party.md 表）的 case 3 讀 `D(objid)`。只讀過伺服器來源，沒有複製進 repo。
- **資格判定** `PartyPackets.DelegateTarget(shown,name)`：必須同時符合下面四點，才回傳隊員的物件 ID，否則回 0（不送）：
  - 自己是隊長（`CanKick`）；
  - 名字在畫面快照的隊員名單裡；
  - 物件 ID 已知（S39 只有名字的列 ID 是 0）；
  - 不是自己。

  同圖、19 格內這兩個限制交給伺服器判斷（伺服器回 1695／1697），客戶端不自己加。
- **送出路徑**：`LoginConnection.SendParty` 新增 `"delegate"` 命令，沿用既有的 generation／revision／PartyAccess 閘門，送出前還會再檢查一次。送出後只更新提示（「已送出委任隊長請求」），**不在本地改隊長**，等伺服器的 S250/106 或重組隊伍封包。
- **原生 UI**（`engine/source/NativeUi/PartyPanel.cs`）：委任鈕（tag 2）在選了符合資格的隊員時呼叫 `PartyAction("delegate",party,name)`。LT 與 RT 兩個面板共用 `PartyLayout.Interaction`，所以都會生效。資料走既有的 `UiClientBridge.PartyAction` → `ClientForm.PartyAction` → `SendParty`，不用改橋接層。
- **新離線測試** `engine/source/PartyDelegateTests.cs`（`[WorldModuleTest("party-delegate")]`，自動發現）。涵蓋：
  - 線上格式，以及 ID 0 被拒絕；
  - 資格判定：非隊長、ID 未知、自己、名字不存在都不送；
  - 經 `World.Receive` 餵 S250/105、104 後委任送出 `E6 03 <id>`；
  - 送出後隊長不變；舊快照不能重送；
  - 收到 S250/106 換隊長後不能再委任；
  - S39 只有名字的隊員不送；HP 0 時不送；
  - 面板委任鈕的各種情況；leave／ban 照舊。
- **範圍外的一行調整**（必要）：`engine/_integration/dev/native-ui-tests/PartyPanelChecks.cs:161` 原本斷言「委任鈕什麼都不送」，這正是本任務要改的行為。改法是先清掉選取再按委任（斷言沒選人時不送），然後重選 Bravo，讓後面的計數斷言維持原狀。送包行為改由新測試檔驗證。

## 怎麼驗證
- **本機只編譯，沒有執行任何程式**：
  - `dotnet build engine/source/OfflineClient.csproj -c Release`：0 錯誤；
  - `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：0 錯誤；
  - 本次三個檔案沒有新警告。
- 本機建置環境問題（和本 PR 無關，同 #761／#805／#806）：這台機器的 WebView2 1.0.4258.31 套件會解析出 `.winmd`，觸發 NETSDK1130。處理方式：
  - 以 `NUGET_PACKAGES=scratch/issue-812/nuget-packages` 從 nuget.org 還原到 worktree 的 scratch，沒有寫入家目錄；
  - 用只放在 `scratch/issue-812/no-winmd.targets` 的 hook（`-p:CustomAfterMicrosoftCommonTargets`）拿掉 winmd，改參考套件的 `lib_manual/netcoreapp3.0` DLL；
  - 沒有改 repo 的建置檔。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`，errors 0。
- **CI（PR #835，run 42837194807／42837195145）**：`dotnet`、`map-continuity`、`npc-offline` 三個 job 都通過。
  - `--module-tests` 全部 `failed: 0`，其中 `party-delegate` 32 項全部通過；
  - DemoChecks 的 `native-ui-party` 也通過，含改過的第 161 行。
  - 依本機限制，沒有在本機執行任何編譯出來的程式。

## 未驗證什麼
- **真機**：
  - 原版按委任鈕時有沒有確認框（Y/N）沒查過 BIN，現在直接送，屬推測；
  - 伺服器 1695 那條同圖檢查會送 `S_Message_YN(1695)` 而不是 S71，客戶端怎麼顯示待真機確認；
  - 委任後伺服器 `breakup` 再重組，會依序送 418 與 104／105／106；原生面板看起來對不對，待真機。以上列入 15.7 真機清單。
- `--classic-ui` 的 WinForms 隊伍抽屜（`PartyUi.cs`）不在本組可改範圍，沒有加委任鈕。原生 UI 關閉時仍保留既有 WinForms 入口（離隊／踢除／邀請），行為不變。
- 邀請入口歸 Q7-9，本組沒碰。

## 產出路徑
- `engine/source/Party.cs`、`engine/source/NativeUi/PartyPanel.cs`、`engine/source/PartyDelegateTests.cs`（新）
- `engine/_integration/dev/native-ui-tests/PartyPanelChecks.cs`（一行，理由見上）
- 本報告 `docs/reports/issue-812.md`
- 分支 `task/812`

## 給下一棒的提示
- 若要在 classic UI 也提供委任：在 `PartyUi.cs` 加按鈕，呼叫 `owner.PartyAction("delegate",stamp,name,0)` 即可，資格判定已集中在 `PartyPackets.DelegateTarget`。
- 真機驗收（P24Lab 兩個測試角色組隊）：
  1. 隊長選隊員按委任鈕，應送 `E6 03 <id>`；
  2. 伺服器解散後重組，S250/106 帶新隊長名，原隊長的 ban／委任鈕應失效；
  3. 隊員不在同圖時，應看到 1695。
- `IUiPartyCommands` 的註解（`NativeUi/State/PartyState.cs:14`）仍只寫 "leave"、"kick"，下次動到該檔時順手補上 "delegate"。
