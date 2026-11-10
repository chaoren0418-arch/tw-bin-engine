# Issue #906 — T391：R10-G 表情保留範圍（gap-matrix 1:92）

## 做了什麼
依 `docs/reference/gap-matrix.md` 15.12 的 1:92 列與 15.12.6 的 R10-G：原版在 0xDF5401–0xDF5416 只在「按住輸入的延續」（`sub_A4C270(0)`）前檢查 byte_153CE69／byte_153CE6A（AN§1），所以表情保留期間只該暫停按住的移動。

- `engine/source/Resurrection.cs`：`MoveLocked` 改成只等於 `ServerMoveLocked`（S241），表情保留（`EmoteHeld`，1234 ms）與 700 ms 收尾（`EmoteSettling`）不再算進去。所以 `MovementAvailable` 不再被表情擋住，所有共用這道閘門的路徑都會恢復：攻擊（`Combat.cs` 的 `CombatAvailable`）、施法（`SkillCast.cs`）、門（`Doors.cs`）、傳送點（`Portals.cs`）。
- `engine/source/OnlineMovement.cs`：
  - 新增 `HeldMovementPaused`（等於 `World.EmoteInputPaused`）。
  - `RequestMovement` 是鍵盤按住移動與 web 指令共用的入口。暫停期間收到「步行」時不送出、不回報失敗，改成等暫停結束再呼叫原本的 `SendMovement`；`SendMovement` 會重新做完整檢查。
  - 等待期間 `OnlineTick` 每個 tick 都會再呼叫一次，這些呼叫共用同一個等待工作，方向取最新一次，所以只會送出一步。
  - 放開按鍵（`CancelMovementInput`，inputVersion 改變）、換連線或換世界時，丟掉這一步。
  - 暫停結束後，如果移動冷卻還沒到（`MovementBusy`），會繼續等，避免冷卻檢查失敗把輸入清掉。
  - 測試用接點 `EmotePauseWait`，預設 `Task.Delay(15)`。
  - 轉向（step=false）、`Move()`、滑鼠路徑（`PointerMove`）不經過這段等待。
- `engine/source/ClientLogin.cs`：**沒有修改**。第 21 行在 `!MovementAvailable` 時呼叫 `ClearInput("unsupported-movement-state")`；`MovementAvailable` 不再含表情之後，這行就不會在表情保留時清掉按住的輸入，所以不用改這個檔。
- 測試：
  - `ProtocolFollowupTests.cs`、`InputLineFollowupTests.cs`：改了原本寫成「保留＝MoveLocked」的斷言（EmoteHold、EmoteSettle、InterruptEmote、HoldScope），改成 `HeldMovementPaused && !MoveLocked`。
  - 新檔 `EmoteHoldScopeTests.cs`（`[WorldModuleTest("emote-hold-scope")]`，自動發現）涵蓋：
    - 保留與收尾都不算 MoveLocked。
    - 保留前後 `MovementAvailable`、`CombatAvailable`、`MovementBlockReason` 不變。
    - 按住的步行會等完整的 1234 ms 才進 `SendMovement`，期間不送封包。
    - 重複輸入共用同一個等待。
    - 放開按鍵、換世界會丟掉這一步。
    - 轉向不等待。
    - S241 仍然鎖住。

## 怎麼驗證
- 本機只編譯，沒有執行任何程式。`dotnet build engine/source/OfflineClient.csproj -c Release`：0 錯誤；3 個警告是原本就有的，在 `HtmlActionTests.cs`、`MagicUi.cs`，和本次修改無關。
  - 本機環境備註（同 #675／#676）：worktree 原本的 restore 會出現 NETSDK1130（WebView2 winmd）。處理方式：
    1. 刪掉 `engine/source/obj/` 下 5 個 NuGet 產生的單檔（不是遞迴刪除）。
    2. 用 `scratch/issue-906/NuGet.Config` 唯讀指向主專案的 `engine/vendor`，`NUGET_PACKAGES` 設成 `scratch/issue-906/packages`，重新 restore。
    3. 為了只檢查能不能編譯，沒有帶 `-r win-x64`。
- `python engine/tools/check-no-game-assets.py`：pass。
- 單元／離線測試（`--module-tests`）依本機限制不在本機執行，交給 PR 的 CI。
- 沒有改 `rebuild-lock.json`、`WorldSession.cs`、`Program.cs`、`DemoChecks.csproj`、各註冊表。

## 未驗證什麼
- **CI 結果**：要看 PR 的 GitHub Actions。新測試的 seed 在 map 4、`CacheEnabled=false`，所以 `Renderable` 是 false，也不會讀遊戲素材；因此「恢復後送出步行封包」只驗證到「有進到 `SendMovement`」（拿到它自己的拒絕原因），沒有驗證到真的送出 C29。
- **真機再確認（待驗證，交給 Codex／dot 在 P24Lab）**：
  - 用變身頭盔（gfx 6080）做保留型表情時一直按住方向鍵，應該約 1.2 秒後自己繼續走，不必重按。
  - 保留期間按 G 攻擊、施法、點門應該照常可以用。
  - 一般立即型表情結束後 700 ms 內按住方向鍵，行為同上。
- 滑鼠按住或點擊的路徑（`PlayerPointer`／`ApproachWalk`）這次不在暫停範圍內，保留期間會照常走。原版 `sub_A4C270(0)` 也包含滑鼠按住延續的可能，這點是**推測**，需要逆向再確認（`PlayerPointer.cs` 不在本任務可改範圍）。
- `TitleAndEmote.cs:117` 的註解還寫著「Resurrection.cs MoveLocked」，這個檔不在可改範圍，留給下一次動到該檔的任務順手修正。

## 產出路徑
- `engine/source/Resurrection.cs`、`engine/source/OnlineMovement.cs`
- `engine/source/ProtocolFollowupTests.cs`、`engine/source/InputLineFollowupTests.cs`
- `engine/source/EmoteHoldScopeTests.cs`（新）
- `docs/reports/issue-906.md`（本檔）

## 給下一棒的提示
- 表情暫停只在 `LoginConnection.RequestMovement` 生效。之後如果要讓滑鼠按住移動也暫停，可以在 `PlayerPointer` 的步行迴圈檢查 `login.HeldMovementPaused`，並且不要取消路徑。
- `OnlineInput.OnlineTick` 在 `MovementBusy` 時會直接 return、不清輸入；在 `RequestMovement` 回 Sent=false 時則會清輸入。所以新增任何「暫停」語意時，都不要讓它回 false。
- gap-matrix 1:92 列可以標成已處理（code）；真機確認項列在上面「未驗證什麼」。
