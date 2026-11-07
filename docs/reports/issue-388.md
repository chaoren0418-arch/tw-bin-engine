# Issue #388／T148：中毒、麻痺、詛咒盲目

## 做了什麼

- 新增 `engine/source/Afflictions.cs`：S165 正常／綠色／灰色外觀、S202 異常狀態輸入鎖與排隊請求失效、S47 盲目及解除。
- `WorldSession.cs` 僅新增三個 opcode case；`OnlineInput.cs` 僅新增一個鎖定查詢；`ObjectRendering.cs` 僅將既有像素合成區段接到一個異常狀態合成入口。
- S202 沿用既有傳送模組的鎖集合，保留 S202/7 recovery wrapper；不改傳送模組。切圖保留自身異常狀態，重連／切角 reset 清除。
- 提交合成封包／像素測試，接上 DemoChecks 及 Windows CI；更新 dev lock。未改 `docs/REPORT.md`、release、封存 lock 或遊戲資源。

## 怎麼驗證

已驗證：

- `rebuild.py --update-lock`：185 個來源、194 個鎖定檔核驗通過。
- `rebuild.py --build --configfile scratch/issue-388/nuget.local.config --output scratch/issue-388/build`：產品及 launcher 編譯通過，產品 0 錯誤、2 個既有 `MagicUi.cs:20` nullable 警告。
- `DemoChecks ... afflictions`：138 項檢查通過，涵蓋每個 S165／S202／S47 格式、截斷／補齊／不合法值的原子性、重疊鎖、解除恢復、傳送 recovery、切圖/reset、鍵盤／滑鼠／API 拒絕與排隊請求失效、像素遮蔽／陰影／共用來源保護及解除還原。
- `DemoChecks ... combat-admission`：278 項既有戰鬥檢查通過。
- `DemoChecks ... map-continuity`：358 項既有地圖／傳送／船與公告欄檢查通過。
- `check-no-game-assets.py`：通過，0 錯誤。
- `git -c core.whitespace=cr-at-eol diff --check`：通過（保留既有來源檔的 CRLF）。

產品 DLL SHA-256：`49eb655a891d5f4dd2e3c6f41c08fbc7cc1941cdf34a2be51ac8d302d265f536`。
CI 已加入 `Affliction contracts`；遠端結果以本 PR 的 `build` workflow（Windows／Linux 兩個工作）為準，完成摘要另列於 PR。

初次 NuGet 線上還原因 Windows TLS 認證錯誤失敗；改用原專案 `engine/vendor` 的既有套件唯讀還原，套件快取及暫存均位於本 worktree，未更動原專案。

## 同步輪（2026-11-07，claude-local）

調度器退回同步：PR #401 落後 main 105 個 commit，5 個檔案衝突。上方的檢查數、DLL SHA-256 與 lock 數量是第一輪數值，已由本段取代。

- 合併 `origin/main`：`build.yml`、`DemoChecks.csproj`、`Program.cs`、`WorldSession.cs` 兩邊保留；`rebuild-lock.json` 由 `--update-lock` 重產。
- **超出「只做同步」的一項修正（獨立 commit，可單獨回退）**：第一輪的 `case 202` 提早返回，略過 `BasicSupplyReply`。原服拒絕吃肉／回家卷軸時 S202/7 是唯一解除路徑，所以 `BasicSupplyPending` 不會清除，`ActionLocked` 一直為真，只能重連。這不是合併造成的，第一輪測試也沒有涵蓋。已在 `ReceiveParalysis` 補呼叫，並加入走真實 `UseSelectedItem` 路徑的迴歸測試；修正前該測試失敗（`S202/7 releases pending supply False`），修正後通過。
- S165／S47 的 case 同樣略過 `BasicSupplyReply`，但這兩種封包不是補給品回包，只少了一次提示刷新與世界／死亡檢查，下一個封包就會補上，故未修改。

同步輪最終驗證（合併 main 並修正後）：

- 工作期間 main 又前進 9 個 commit（#361），已第二次同步：`WorldSession.cs` 兩邊保留，`Program.cs` 的 `map-continuity` 採 main 版本（含 `LocationShareChecks`）。
- `--update-lock`：191 個來源、200 個鎖定檔；`--build`：產品及 launcher 通過，產品 DLL SHA-256 `0e4fc33aced6d65f44781391fd4aba829e9e31801eb2953555ae29c13d5113ba`。
- DemoChecks：afflictions 238、combat-admission 1109、map-continuity 20832、polymorph-cadence 123、buddies 259、item-transfer 163、location-share 189，全部通過。main 合入後每組都會先跑 `PolymorphChecks()`，所以總數比第一輪高。
- `check-no-game-assets.py`：通過，0 錯誤。

## 第三次同步（2026-11-07，claude-local）

調度器再次退回同步：分支落後 main 70 個 commit，6 個檔案衝突。工作期間 main 又兩度前進（門 #322、幽靈模式 #360），各再合併一次：`DemoChecks.csproj`、`Program.cs` 兩邊保留，lock 重產。本段的數值取代上方所有數值。

- 文字衝突：`WorldSession.cs`、`DemoChecks.csproj` 兩邊保留；`build.yml` 兩邊保留，並補回 Affliction 步驟結尾的 exit code 檢查（該行原本是兩邊共用）；`Program.cs` 的 `map-continuity` 採 main 版本，保留 `afflictions`；`rebuild-lock.json` 由 `--update-lock` 重產。
- **文字合併解決不了、必須改程式的兩處**（都只改 `Afflictions.cs`）：
  1. main 的隱身（#389）在舊合成迴圈套用 `InvisibilityOpacity`，而這個迴圈已移到 `ComposeAfflictionPatch`。只取 HEAD 版會讓隱身靜默失效，且編譯與 `InvisibilityChecks` 都不會報錯，因為後者直接測 `Blit`。已把呼叫搬進 `ComposeAfflictionPatch`。
  2. main 的 `Ambience.cs` 也定義了 `public WorldSession()`，與本模組重複，無法編譯。改用 `WeaponRepair.cs`／`SoundPackets.cs` 既有的寫法，第一次收到 S165／S47 才訂閱事件，不必動 `Ambience.cs` 或 `Reset()`。
- 新增 4 項檢查（`AfflictionChecks.cs`）：隱身經過合成路徑仍半透明、解除後還原、S47／S165 作為第一個異常封包時 reset 能清除。

驗證：

- `--update-lock`：202 個來源、211 個鎖定檔；`--build`：產品及 launcher 通過，產品 DLL SHA-256 `0df66dce77f4b81fe98a0d49344da97d14e72feefb33d5b84141988dd49f5102`。已確認測試連結的 DLL 就是這個雜湊。
- DemoChecks 14 組全部通過：afflictions 345、ghost-mode 552、doors 514、invisibility 263、ambience 549、fishing 232、pets 193、duel 307、combat-admission 1326、map-continuity 21759、polymorph-cadence 226、buddies 362、item-transfer 266、location-share 292。每組都會先跑共用檢查，所以數字比前幾輪高。
- 門的滑鼠路徑 `ToggleDoor` 有 `MovementAvailable` 閘門，麻痺時不會送出（讀程式確認，未另寫測試）。
- 突變測試（第一次合併後、重建產品後執行）：拿掉隱身呼叫、S47 掛接、S165 掛接，`afflictions` 都失敗，失敗的是對應檢查；還原後 lock 與 DLL 雜湊回到原值。
- `check-no-game-assets.py`：通過，0 錯誤。
- `weapon-repair-tests` 本機未跑，以 CI 結果為準。

## 第四次同步（2026-11-07，claude-local）

調度器再次退回同步：分支落後 main 46 個 commit（合併基準 `5813dd2`，含血盟 #262、信件 #267、傳送門 #377）。本段的數值取代上方所有數值。本輪沒有改 `engine/source` 的本模組程式。

- 文字衝突：`DemoChecks.csproj`、`Program.cs`、`build.yml` 兩邊保留，並補回 Affliction 步驟結尾的 exit code 檢查；`rebuild-lock.json` 由 `--update-lock` 重產。
- 語意檢查（讀程式）：main 新增的 opcode 分派與 S165／S202／S47 沒有重疊。傳送門 `EnterPortalAtArrival`、血盟 `ClanAccess`、信件送出都有 `MovementAvailable` 或 `ActionLocked` 閘門，麻痺時不會送出。main 對 `ObjectRendering.cs` 只多了 `PaintClanLabels` 一行，不影響合成，隱身呼叫仍在 `ComposeAfflictionPatch`。

驗證：

- `--update-lock`：207 個來源、216 個鎖定檔；`--build`：產品及 launcher 通過，產品 DLL SHA-256 `20a3542b7e20beaff27f8d2cf8c37daa79ae1965efe5b503e4e24e8a6a6c5c41`，已確認測試連結的 DLL 就是這個雜湊。
- DemoChecks 29 組中 24 組通過：afflictions 345、clan 312、portals 293、doors 514、pets 193、map-continuity 21978、ghost-mode 552、fishing 232、location-share 292、player-query 894、character-config 286、polymorph 193、polymorph-cadence 226、buddies 362、buddies-codec 308、ambience 549、invisibility 263、board 423、item-transfer 266、combat-admission 1326、duel 307、attack-skills 934、hit-reaction-suppress 259、slow-actions 557。
- **另外 5 組本機沒有跑成**：`combat-loop`、`keyboard`、`busy`、`world` 因 worktree 沒有 `engine/assets`（`integrity.json`／`terrain0.bin` 讀取失敗）中止；`attack-skill-resources` 因少傳參數中止。這些都是環境或呼叫方式問題，不是斷言失敗，但也代表這 5 組沒有驗到本分支。CI 也沒有跑這 5 組。
- `MailChecks` 119、`WeaponRepairChecks` 98：通過。
- `check-no-game-assets.py`：通過，0 錯誤。

## 未驗證什麼

- `keyboard`、`busy`、`world`、`combat-loop`、`attack-skill-resources` 五組 DemoChecks 需要真實素材或額外參數，本機與 CI 都沒跑（見第四次同步）。其中 `keyboard`、`busy` 會經過 `OnlineDown`；本模組新增的鎖定查詢由 `afflictions` 組直接測到。
- 未啟動遊戲、伺服器或 DB，未登入／送真實遊戲封包。
- 未驗證原生畫面與舊 BIN 調色盤逐像素一致性。綠色、灰色與盲目明度是依伺服器語意實作的呈現政策。
- 未量測真實地圖的盲目合成成本。盲目需要覆蓋完整 renderer Bounds，應由 smoke 觀察大地圖及船背景時的流暢度。
- 未處理盲目與環境光（#387）的疊加：`Renderer.PaintAmbience` 目前沒有呼叫端，環境光還不會畫到畫面上。之後接上時，要確認盲目 1 的黑畫面不會被光暈蓋過。

## 產出路徑

- 產品：`engine/source/Afflictions.cs` 與三個授權掛接檔。
- 測試：`engine/_integration/dev/afflictions-tests/AfflictionChecks.cs`。
- 協定：`docs/protocol/afflictions.md`。
- 本機建置證據：`scratch/issue-388/build/`；離線檢查結果：`scratch/issue-388/*.json`（不入 git）。
- 本報告：`docs/reports/issue-388.md`。

## 給下一棒的提示

本機跑 DemoChecks 時注意：它連結 `engine/build/client-product-dev/` 的 DLL，改了 `engine/source` 要先 `--update-lock`、`--build`，否則測到的是舊程式。本機 PATH 沒有 `python`，用調度器的 `$env:PY`。

由 Claude 審查 PR，使用者合併；dev lock 如遇其他任務同步衝突，由調度器重跑產生，不手拼 hash。合併後由 dot 在授權 P24Lab smoke：確認自身與周邊中毒／灰色及解除，六類鎖的移動／攻擊拒絕與恢復，盲目兩模式及切圖／重連，並觀測全圖盲目時的效能。狀態圖示不屬本張。
