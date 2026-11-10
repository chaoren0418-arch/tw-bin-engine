# Issue #848 — T364：R9-D S55 不跟隨特效

依據：`docs/reference/followups-9.md` §2（Q7-2）、§16.2 R9-D；`gap-matrix.md` §15.11（列 1:86）。

## 做了什麼
1. `engine/source/SoundPackets.cs` `PrunePacketMedia`：特效只在 `Expires` 到期時刪除，**不再因對象消失而刪除**（FU9§2.1 第 3 點，已驗證：原版 Effect 不存對象指標，只看動畫播完）。
2. `engine/source/MapEffects.cs` `PacketEffectFrames`：
   - 不跟隨的 S55：位置固定在收包時的 `Origin`，對象消失也在原地播完；結束只看 `time` 期限或一輪動畫長度（已驗證）。
   - 跟隨組（6985／7179／7834／7836／8150／8152／8981）：**段內位置固定、段間跳到對象目前位置**（推測，FU9§2.1 第 4 點）。段＝一輪動畫長度，第 0 段用收包位置；換段時找不到對象就不接下一段，特效結束。
   - 為了記住每段的位置，在 `PacketEffectFrames` 正上方加了一個私有欄位 `packetEffectSegments`（特效編號 → 段號與位置），每次呼叫結束時清掉已不存在的特效。只給這個方法用。
   - 沒有 `Origin` 的舊資料（測試直接塞的）照舊：找得到對象就畫在對象位置，找不到就刪。
3. `engine/source/MapEffectFollowupTests.cs`（`map-effect-followup`）：把舊的「每格跟隨」「對象消失就刪」斷言改成新行為，新增：段內固定、換段跳位置、對象消失後不跟隨的特效在 Origin 播完到期限、不帶 time 的一輪特效對象消失後播完、跟隨組在段內失去對象時播完本段且不接下一段。
4. **超出可改範圍的一處**：`engine/_integration/dev/sound-packets-tests/Program.cs`（CI 的 SoundPacketChecks）有兩條斷言寫的是舊行為（「S55 每格跟著目前位置」「對象消失就刪特效」），新行為一定會讓它們失敗。只改了這兩條的期望值與說明（第 121、125–126 行），沒有動其他測試。理由：驗收要求 CI 綠。

## 怎麼驗證
- 編譯：`dotnet msbuild engine/source/OfflineClient.csproj -t:ResolveReferences;CoreCompile -p:Configuration=Release`：本次改的檔案沒有錯誤或警告。唯一的錯誤是 `WebPanel.cs` 的 WebView2 型別（本機 NETSDK1130 環境問題，與本任務無關，#676／#689 也遇過）。完整建置以 CI 為準。
- `check-no-game-assets.py`：pass（4328 個追蹤檔案，0 個錯誤）。
- 依本機規定，**沒有在本機執行任何測試**；`map-effect-followup` 與 SoundPacketChecks 交給 PR 的 CI。

## 未驗證什麼
- 跟隨組「段內固定、段間跳位置」是推測（FU9§2.1 第 4 點：vtable 沒找到每格讀 `+0x290` 改座標的程式），**真機再確認**：例如 8981 長時間特效在角色移動時是否一段一段跳。
- 「段」用一輪動畫長度近似；原版是「每段播完時接下一段特效」，若某些 gfx 的下一段不是同一個 gfx（由 `]` 或 `sub_8AE620` 接續），本次沒有模擬。
- `sub_8AE440` 低更新率時丟棄新特效（FU9§2.1 第 5 點）未做，屬細節。
- 本任務不涉及新資源，沒有查 .pak。

## 產出路徑
- `engine/source/SoundPackets.cs`、`engine/source/MapEffects.cs`、`engine/source/MapEffectFollowupTests.cs`、`engine/_integration/dev/sound-packets-tests/Program.cs`
- 本報告 `docs/reports/issue-848.md`；分支 `task/848`

## 給下一棒的提示
- 真機（P24Lab，Codex／dot）：讓目標在 S55 特效中途走出畫面或登出，確認不跟隨特效（例如 1036）留在原地播完；對 8981 等跟隨 gfx 觀察是否段內不動、換段才跳。
- 如果真機看到跟隨組是每格跟著，只要把 `PacketEffectFrames` 跟隨分支改回每格讀 `PacketEffectPosition`，並把 `map-effect-followup` 對應三條斷言改回即可。
