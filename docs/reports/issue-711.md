# Issue #711 T299：R4-H S250 27／30／31 印聊天欄

依據：`docs/reference/gap-matrix.md` 13.5 節 R4-H、`docs/reference/followups-5.md`（FU5）§2。

## 做了什麼

`engine/source/PacketBoxMessages.cs`（M7-A 既有處理器 `ReceivePacketBoxMessage`，另加一個 `[WorldReceive(87,Observe=true)]` 觀察器）：

- **27 階級變更**（FU5§2.1）：有欄位（`C 階級 S 名稱`）且階級在 2–13：
  - 名稱＝自己（`Actor.Name`，ordinal 比對）→ 印 **784**（%s＝階級名），並把自己的 `Actor.Rank` 設成新階級；
  - 別人 → 印 **2125**（名稱、階級名）；
  - 兩種都會更新血盟成員清單（`clanMembers`）裡同名者的階級；
  - 階級名：2、7＝773；3＝1878；4、10＝771；5、8＝1879；6、9＝772；12、13＝3503（`PacketBoxMessageRules.RankNameId`）；
  - 階級 < 2、> 13、兩參數形式（無欄位／全零）→ 不處理；**11** → 只把自己的 `Actor.Rank` 設成 11，不比對名稱、不印字。
- **30 打敗反王**（FU5§2.2）：照 TW16 讀 `dssd`；3.81 只寫 `D S S`，剩不到 4 byte 時城堡視為補零 0。城堡 ≠ 8 印 **782**（血盟、角色），再一律印 **783**（血盟）。`PacketBoxLastavard` 多了 `Castle`（預設 0，舊建構呼叫相容）。
- **31 舒服多了**（FU5§2.3）：印 **77**；TW16 讀 `d`（物件 id），本模組接受有無 id 兩種形式。設旗標（對應 `byte_13D4401`），下次**自己的** S87（物件 id＝`Actor`／`Status` 的 id）出現時，在該封包座標加一個 gfx **831** 的位置特效（走既有 `packetEffects`／`PacketEffectFrames` 路徑，和 S106 同一條），然後清旗標。
- **顏色**：27／30／31 印出的行設 `PlayerChatLine.ColorIndex=24`（palette[24]，淡黃）；`ChatFormat.TextColor` 會直接用它。其他 B1 子碼維持原狀（未改色）。
- 檔頭表格三列改成「聊天欄」；快照多 `lastavard.castle`、`feelGoodTarget`、`feelGoodEffectPending`、`feelGoodEffects`；`[WorldReset]` 一併清旗標。

`docs/reference/protocol-gaps.md`：2.2 節 30 列欄位 `dd` → `dssd`（只改這一列）。

新檔 `engine/source/PacketBoxChatTests.cs`：`[WorldModuleTest("packetbox-chat")]`，合成封包＋合成字串表（假文字）：觀察器登錄、階級範圍與階級名 id、27 自己／別人／不在血盟／12（3503 查不到）／<2／>13／兩參數／11、30 三種城堡、31 的 77＋gfx 831（別人 S87 不觸發、自己 S87 只觸發一次、帶 id 形式）、palette[24]、其他子碼不變色、格式錯誤不改狀態、Reset、無字串表的 fallback 文字。

**超出「可修改檔案」的一處**：`engine/source/PacketBoxMessagesTests.cs`（T229 的舊測試）第 8 段原本斷言「27／30／31 不進聊天欄」、第 9 段斷言 dropped＝1；行為改變後必然失敗，所以只改這兩個斷言（聊天行 +4、dropped 2），其他不動。

## 怎麼驗證

- 編譯：`py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile ../.github/nuget.ci.config` → `PASS dev`（暫時 lock，不寫 `rebuild-lock.json`；worktree 沒有 `engine/vendor`，所以用 CI 的 nuget 設定）。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `status: pass`。
- 錄製 fixture（`engine/fixtures`、`engine/_integration`）裡沒有 S250/27／30／31 的 plainHex，回放測試的聊天行數不受影響。
- 依本機執行限制，**沒有在本機跑** `--module-tests`；本組與既有單元測試交給 PR 的 GitHub Actions CI。

## 未驗證什麼

- CI 的模組測試結果（push 後看 PR）。
- 真機：聊天欄淡黃色的實際觀感；gfx 831 的觸發時機（FU5 推測是傳送後）、本機有沒有 gfx 831 的素材（沒有時既有路徑會顯示「缺少已核定素材；未顯示」並移除）；S87 處理時若同時換了 `WorldEpoch`，特效會被既有 `PrunePacketMedia` 清掉（推測不會，待驗證）。
- 階級 12／13 用的 3503 本機表查不到 → 階級名為空字串；原版在這種情況的實際顯示未查（待驗證）。
- 284「%0%s 對你施法。」（31 帶有效物件 id 時原版先印）**未實作**：3.81 不會送（id 0），且 `%0%s` 的實際格式化方式未查，避免印出錯誤文字。
- 血盟視窗是否即時重繪（原版 `sub_B30240`）：只更新資料，UI 下次取 `ClanMembers` 時看到新值。

## 產出路徑

- `engine/source/PacketBoxMessages.cs`、`engine/source/PacketBoxChatTests.cs`（新）、`engine/source/PacketBoxMessagesTests.cs`（兩個斷言）
- `docs/reference/protocol-gaps.md`（2.2 節 30 列）
- 本報告 `docs/reports/issue-711.md`

## 給下一棒的提示

- `test`（Codex／dot，P24Lab）：用 GM 測試角色觸發 `C_PledgeRecommendation` 的階級變更看 784／2125 是否淡黃；吃補血道具（`S_PacketBoxHpMsg`）看 77，傳送後看是否出現 gfx 831。
- 若要把其他 B1 子碼也改 palette[24]，需另查各 case 的色參數（FU5 只查了 27／30／31）。
- 284 要做的話，先查 `sub_7DC850` 對 `%0%s` 的處理。
