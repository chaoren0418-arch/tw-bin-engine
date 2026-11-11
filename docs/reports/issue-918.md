# Issue #918 — T403：R10 小清理（刪除取消、227 開框、InputHelp、過時註解）

依據：`docs/reference/gap-matrix.md` 15.12.6「小清理」（T379／#880）。前置 R10-F（#905）、R10-J（#909）已合併。
分支：`task/918`。選做的兩項（信件捲軸滑塊拖曳、`ItemIconSource` 參考客戶端備援）也做了。

## 做了什麼

| 項目 | 檔案 | 內容 |
|---|---|---|
| Esc 取消刪除提示 | `KeyboardNavigation.cs` | 新增 `ProcessItemDeleteEscape`，放在 `ProcessLoginKeyboardEnter` 裡、伺服器 YES/NO 的 Esc 之後。條件：在世界中、聊天列有刪除提示、焦點在本窗／聊天列／聊天鈕／WinForms 背包清單／垃圾桶鈕、不是重複按鍵（判斷式 `ItemDeleteEscapeCancels`）。成立時呼叫 `CancelItemDelete`（不送封包），聊天列開著就一起關。原生畫面上，`ItemWindowPanel` 本身不吃 Esc，所以通常先由 R10-F 的原生 `ChatPanel` 取消（呼叫的是同一個 `CancelItemDelete`）；原生面板都沒吃 Esc 時，才輪到這裡。 |
| classic 刪除也照 alertitems.tbl | `ItemTransfer.cs` | 新增 `LoginConnection.BeginClassicItemDelete`；`DispatchItemTransfer` 原本的 else 分支（WinForms 垃圾桶、拖到垃圾桶）改走這裡。單件：物品在 `alertitems.tbl` 內才問 3122，不在就直接送 C138（不問 668，因為 668 是原生背包窗的步驟）。堆疊：先問數量（4115／3054），物品在清單內才再問 4114。讀不到表時一律問（同 R9-L）。直接送出時不開輸入列，結果寫在聊天列提示。`BeginItemDelete`（R6-E 一律問）保留給既有測試。相關註解一併更新。 |
| 角色管理途中收到 227 要開框 | `CharacterManagement.cs` | `ReceiveCharacterManagement` 收到 227 時，先交給自動發現的觀察器（新增 `WorldSession.ObserveCharacterManagementPacket`，寫在同一個檔案的 partial 裡，內部呼叫 `ObserveExtensions`），讓 `KickNotice` 記下通知、交給 `ClientForm` 開「連線中斷。」（64）框，之後照舊丟 `ProtocolException` 結束連線。沒改 `WorldSession.cs`／`KickNotice.cs`。 |
| `InputHelp` 補 `/duel` | `PlayerChat.cs`（只改 `InputHelp`） | 加上 `/duel（/決鬥）：決鬥`。 |
| 過時註解 | `NativeUi/UiClientBridge.cs`、`NativeUi/StatInfoPanel.cs`、`AbilityStatus.cs`、`NativeUi/AgitMapPanel.cs` | UiClientBridge：開關現在預設開（`UiOptions.DefaultOn`），`--classic-ui`、`native-ui.json` 的 `"nativeUi": false`、診斷入口才關。StatInfoPanel：`TextResolver` 從 R7-N（#817）起在產品裡有設定（參考客戶端字串表）；op 43 那段改寫成「15.6 已更正」。AbilityStatus:87：選項窗的 `EnableElfTelControlBtn` 已由 R10-I（#908）接上。AgitMapPanel:17：Esc 從 R7-H 起走全域 ESC → `Close()`，從待驗證清單移出。四個檔案都只改註解。 |
| gap-matrix 15.6 R7-I | `docs/reference/gap-matrix.md` | 「op 43 開 StatInfo」改成「op 43（S64 kind 1／2）關 StatInfo」，註明原本寫錯、出處 `StatInfoPanel.cs:3–6`。只改這一句。 |
| 選做：信件捲軸滑塊拖曳 | `NativeUi/MailPanel.cs`（只改捲軸拖曳） | `Move_Button` 依畫出來的位置（`ThumbOffset`）接受點擊。按住後滑塊頂端跟著指標沿軌道移動（軌道長度＝`ScrollHeight − 滑塊高`），換算成最接近的列／行，抓住的點保持在指標下。按滑塊不會觸發點擊；沒有可捲的內容時滑塊不動；關窗或換窗時中止拖曳。拖曳規則是推測（原版沒追）。檔頭的「待做」更新。 |
| 選做：`ItemIconSource` 參考客戶端備援 | `NativeUi/ItemIconSource.cs` | 新增 `RefClientIconFallback`。只在本機 Sprite*.pak 沒有 `<icon>.tbt` 時，才到 `RefClientSurfFallback` 用的同一個參考客戶端（`TW_REF_CLIENT` → `native-ui.json` 的 `refClient` → kor25）讀，查找順序同 `BuffIconCatalog.Candidates`。idx 在記憶體裡讀並算雜湊，只用 offset 讀那一筆，記下 SHA-256（log 關鍵字 `icon-ref`）。不複製檔案、不寫檔，也不對整個 pak 算雜湊。找不到、重名、越界或解不開時回 null，不畫。`FromRoot` 會自動接上；`FromSprites` 多一個選用參數。5 張 kor25 的 SHA-256 寫成常數，只用來比對和記 log。 |
| 測試 | 新檔 `engine/source/R10CleanupTests.cs` | `[WorldModuleTest("r10-cleanup")]`，靠自動發現執行。涵蓋：classic 刪除的 6 種情況、Esc 判斷式與聊天列取消、角色管理三種狀態下的 227（記下通知＋仍斷線）、`InputHelp`、信件滑塊拖曳（合成版面）、圖示備援（合成 idx／pak：本機優先、雜湊檔 → Sprite 順序、重名、缺圖、SHA-256、快取）。 |

## 資源查核（PakViewer CLI，唯讀；輸出在 `scratch/task918/`，未進 git）

- 工具：`D:\lineage開發\PakViewer-main\src\PakViewer.Cli\bin\Release\net10.0\pakviewer-cli.exe`，用 `pak list` 和 `pak extract`。
- **本機 `D:\lineage開發\0-客戶端` 的 24 個 idx 全部查過**：`Sprite.idx`、`Sprite00`–`Sprite15.idx`、`Text.idx`、`Tile.idx`、`LiTo\LTicon`、`LTsprite`、`LTsurf`、`LTtext`、`LTtile`。`5731`／`5734`／`5737`／`6458`／`6501`（含 `c` 版）`.tbt` **都沒有**，和 FU9§11.1、#851 一致。`launcher\TW13081901.pak` 沒有 idx，PakViewer 打不開。
- **參考客戶端 kor25 `D:\381懶人包kor25(原)` 的 19 個 idx 全部查過**：`Sprite.idx`、`Sprite00`–`15.idx`、`Text.idx`、`Tile.idx`。5 張各出現一次，位置和 NameHash 規則一致，都是 2,146 B 的 32×32 `.tbt`（用 scratch 裡的 python 腳本唯讀解過，格式正確）。`launcher.Pak`、`launcher-c.Pak`、`TW13081901.pak` 沒有 idx。

| 圖示 | kor25 檔案 | 記錄 | payload SHA-256 |
|---|---|---|---|
| 5731 | Sprite08 | #12171 | `135efe10efc6b2c854cd9d06b55e4d9289232c5a43526d067b66137206efa515` |
| 5734 | Sprite11 | #12194 | `1092e5f538af490218f5fd9609919f0b069327dffb1493425e36a17a089c6586` |
| 5737 | Sprite14 | #11996 | `0dbe7c260395caddf880e6e4fde6495c90f18c2dd1d5a8bd2307cf69830ad2df` |
| 6458 | Sprite15 | #12809 | `3a14434aa57f03a41dc8c2af4ba35a2a70e32f836426c859dfbff98184a1d0b0` |
| 6501 | Sprite04 | #12738 | `e79d1d7d72c9f0ab8c7084d563d6f33f65bed149dd80d06b3db1497f0f628277` |

kor25 idx 的 SHA-256（只記錄，不鎖定）：Sprite04 `b0855489…719f68`、Sprite08 `01c5147a…68f944`、Sprite11 `c598d585…6da6be0`、Sprite14 `d7d34397…a129dd`、Sprite15 `6ff91315…d5136d`。

## 怎麼驗證

- **已驗證（本機，只編譯，沒有執行）**
  - `dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -p:EnableWindowsTargeting=true -r win-x64 -p:SelfContained=false`：0 錯誤。3 個警告都是既有的（`MagicUi.cs:20`、`HtmlActionTests.cs:108`），和 #909 相同，不在本次改的檔案裡。
    - 本機的 NETSDK1130：worktree 的 restore 產生的 `obj/*.nuget.g.targets` 沒有 import WebView2 套件的 targets。本機建置時另外用 `-p:CustomAfterMicrosoftCommonTargets=scratch/task918/nowinmd.targets` 補上：拿掉 winmd，改參考 `lib_manual\netcoreapp3.0` 的兩個 dll。這個檔案沒進 git。restore 用 `.github/nuget.ci.config`，`NUGET_PACKAGES` 指到 worktree 的 `engine/.nuget`（已 gitignore）。
  - `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`，0 錯誤。
  - 沒有改 `rebuild-lock.json`、`WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`。
- **交給 CI**：依本機執行限制，本機不執行編譯出來的程式。既有單元測試、`--module-tests`（含新的 `r10-cleanup`）都以 PR 的 GitHub Actions 結果為準。

## 未驗證什麼（待驗證，交給 Codex／真機）

- 真機上的 Esc：WinForms 聊天列與背包、原生背包窗開著時，Esc 是否取消刪除提示並關掉聊天列；原生窗疊在上面時，吃鍵順序是否合理（原版順序屬 Q8-3，沒追）。
- classic 垃圾桶對清單外的單件直接刪除：「物品編號對到 3.81 `DescriptionId`」沿用 R9-L 的推測（FU8§8.1），要在真機確認。
- 角色管理途中被踢（例如建角或刪角時伺服器送 S227）：是否開出「連線中斷。」框、按確定後關閉程式。離線測試只驗證了通知有記下。
- 信件滑塊拖曳的手感：原版的換算與捨入規則沒追（推測）。
- 學魔法清單 5 張圖示在真機上的顯示（要有 kor25 參考客戶端；log 會出現 `icon-ref <id> -> …Sprite08.pak:5731.tbt sha256 …`）。

## 產出路徑

- 程式：`engine/source/ItemTransfer.cs`、`KeyboardNavigation.cs`、`CharacterManagement.cs`、`PlayerChat.cs`、`AbilityStatus.cs`、`NativeUi/UiClientBridge.cs`、`NativeUi/StatInfoPanel.cs`、`NativeUi/AgitMapPanel.cs`、`NativeUi/MailPanel.cs`、`NativeUi/ItemIconSource.cs`
- 測試：`engine/source/R10CleanupTests.cs`
- 文件：`docs/reference/gap-matrix.md`（只改 15.6 R7-I 那一句）、本報告

## 給下一棒的提示

- 其他想用參考客戶端圖示的地方：`ItemIconSource.FromRoot(root)` 已自動接上。如果直接呼叫 `FromSprites`，要傳 `RefClientIconFallback.For(resources.Surfs.Reference)`。注意共用快取：同一份 archive 清單只有第一次呼叫的設定會生效。
- `ItemDeleteAlertList` 的 classic 與原生兩條路徑現在規則一致，差別只在原生單件刪除多問一次 668。
- 本機 NETSDK1130 的根本原因：worktree restore 後 `nuget.g.targets` 是空的，沒有 import WebView2 的 targets。可以另開 `needs:local` 查是哪個設定讓 NuGet 不寫 import，例如 `ExcludeRestorePackageImports` 或 vendor 套件的 metadata。
- gap-matrix 15.12.6「小清理」的各項，本張都已完成。
