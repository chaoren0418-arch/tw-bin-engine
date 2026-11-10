# Issue #782 — T336：R6-F 料理窗與地裂深度

依據：`docs/reference/followups-7.md` 第 3 節、第 4.2 節、第 8 節 R6-F（FU7）。分支 `task/782`。

## 做了什麼

### 0. 先補 IDA 查證（唯讀，本輪新做）
TW16 IDB 副本 `scratch/rea/ida/tw16.idb`（SHA-256 `3a0bf206…7e82f`，與 FU7§0 相同），用 `docs/reference/rea-scripts/t742/ida_t742.py` 跑 5 輪唯讀批次（subst `W:`，每輪結束都確認已解除）。查詢內容：

| 輪 | 查詢 |
|---|---|
| q1 | `dec` A5AAC0、8FAB00、A5BA50、A5E6F0、A5D2A0、A5CB70、A5CDE0、A5B7E0、A5C5E0、81C7C0、81C340、81CEB0、81CF30、81C9D0、A0BA50；`vtab 11904D0 24` |
| q2 | `xref` 11904D0、153F258；`dec` A57820、A5CC30、A5CBC0、A5B9C0、A5B920、81D090、A5B150、A5B1E0、A5D3B0、A5E330、8C7C80 |
| q3 | `dec A5AA10` |
| q4 | `dec 88C0E0` |
| q5 | `dec A5CE60`、`dis 9441D0 40`、`dec 920E50` |

結果（已驗證）：
- **欄距／列距**：任務原表說「`sub_A5AAC0` 傳給 `sub_8FAB00`」，查下去 `sub_8FAB00` 是 `LScrollBar` 建構子，不是清單。清單是 `CCookListBox`：`sub_8C7C80` 以 `sub_A5AA10(29,31,165,233)` 建立（存進 `dword_153F258`），`sub_A5AA10` 呼叫 `sub_88C0E0(…, 34, 34, 0)`，`sub_88C0E0` 存 `this[8]＝34`（欄距）、`this[9]＝34`（列距）。之後才把清單傳給 `sub_A5AAC0(…, 19, 清單)`。
- **版面** `sub_A5BA50`（FO `0x65AE50`）：格子起點＝窗 +29、+31；`(i−捲動) % 4 == 3` 換列；最多 24 格。選取格（項目 +8）先畫 surf 24 在 (格−1, 格−1)，再畫圖示。滑過（`this[19]`）時：名稱 x＝格 x（6×byte 超過 796 左移）、y＝格 y＋32（格 y＋44＞600 時改成格 y−12）；**滑過格同時是選取格**且有說明行時才畫提示框：寬 142、高 12＋12×行數＋10、x＝清單左緣，y ≤ 清單頂＋161 時底不超過清單頂＋330，否則上移 40＋12×(行數＋1)、不小於清單頂＋10；邊框上／左用調色盤 7、下／右用 9；字從 +5,+5 起每行 12 px。否則只畫名稱（外框色 0）。
- **說明字串與圖示**：`dword_137A0C0`（`.data` FO `0xF788C0`）每 3 dword 一組 (等級, 圖示, 字串)：圖示 2612…2946，字串 1144–1151／1211–1218／1394–1401（本機 string-c.tbl 1144 起是「材料:…（效果）」）。說明字串由 `sub_89AAE0(Str, …, 23)` 切行。
- **讀表** `sub_A5CE60`：欄 4 存在食譜 `+0x104`（word）。`0x9441F0` 起：S_MESSAGE_CODE 6392／6394 時 `dword_153F288＝欄4×2`、`dword_153F28C＝欄4`，排 `sub_A5B920` 每 500 ms 減 1；剩餘已是 0 的下一拍才把總量歸 0。**欄 4＝製作秒數**（R4-D 記為「意義未查」）。
- **量表** `sub_A5C5E0`：只在 `dword_153F288＞0` 時畫 2256 的 (0,0)–(寬,8)，寬＝`50×(剩餘>>1)/總量`。
- **製作** `sub_A5D2A0`：料理書不明（`dword_153F294＝−1`）→ `sub_7DC850(字串 1167, 色 24, 色 14)`；`dword_153F28C＞0` → 字串 1100；否則送 `cdcc 208`。
- **地裂** `sub_81C7C0`（每格推進）在 `0x81C7FA` 對 gfx 10147 把**當下的** `dword_13CCBA4` 傳給 `sub_A0BA50`；開始函式 `sub_81CEB0`／`sub_81CF30` 直接呼叫每格函式（第一格在開始當下）。kind 7 終點（`sub_81C9D0` 尾端）：`sub_81C340`（+29）→ vtable+24 開始（第一格讀到含 +29 的值）→ `dword_13CCBA4 = 0`。

本機字串查詢（唯讀）：`py -3 -I docs/reference/rea-scripts/t680/t680_strings.py D:\lineage開發\0-客戶端 …`（Text.pak → string-c.tbl，`decoded_sha256 4ebbc1a7…43e9`，3484 筆）：1144、1151、1211、1394、1167「為了製作料理，所以需要料理書。」、1100「正在執行中...」。輸出只在 `scratch/rea/`。

### ① 料理清單 4 欄圖示格＋提示框（`NativeUi/CookBookPanel.cs`）
- 單欄文字列 → 4 欄 34×34 圖示格（窗 +29,+31 起，一頁 24 格，上下鈕／滾輪一次捲 4 格＝一列）。
- 圖示用 `ItemIconSource`（背包圖示同一來源，`<icon>.tbt`）；選取格畫 surf 24。
- 滑過顯示名稱；滑過選取格時畫寬 142、每行 12 px 的提示框（名稱＋說明字串，23 byte 切行）。`Bounds` 包含提示框，`WindowBounds` 才是命中範圍。
- 點一下選取、再點一下（或雙擊）製作（沿用 R4-D）。

### 篩選（`Cooking.cs`；`CookBookFallbackUi.cs` 只改註解）
- `CookingProtocol.Listed(table, level)`：表內順序、編號 ≥48 排除、最多 24 格、前 8×L 筆。舊的「編號 < 8L、依編號排序、80 起不列」移除。
- `CookBookRecipe` 加 `Icon`、`TextId`（編號 ≥24 時 0）、`Seconds`（欄 4），依清單位置取 `RecipeIcons`／`RecipeTexts`。
- 暫行 WinForms 清單仍呼叫 `CookRecipeTable.Rows`，自動用新規則。

### ② 量表只在倒數中畫
- `CookCountdown`（剩餘、Busy、量表寬）＋`WorldSession.BeginCookCountdown`／`CookingCountdown`；`CookBookView.GaugePixels`。沒有倒數時量表不畫（3.81 的實際情況）。
- **沒有接收包**：開始倒數的 S_MESSAGE_CODE 6392／6394 處理不在本任務可改的檔案內，3.81 也不送（推測），所以倒數目前只有模型與測試。

### ③ 1167／1100 提示
- `LoginConnection.CookRecipe`：找不到料理書 → 聊天列印 1167（色 24），倒數中 → 1100；都不送。字串表讀不到時用本機 string-c.tbl 的原文。`ItemNotice` 同步，暫行清單也看得到。

### ④ 10147／10145 疊層深度每格推進時讀當下的計數（`ProjectileRendering.cs` 的 EarthDepth 部分）
- `EarthDepth.Cursor`（顯示序號）；`Renderer.EarthDepthAt` 在序號變動（＝推進一格）時讀當下的 `earthCounter`，同一格內沿用。
- 終點：建立 +29 → 記下第一格的值 → 歸 0；之後各格讀當下的值（通常 0）。
- 「建立時記住」的 `SegmentBuilt`／`EarthDepthOf` 移除。

## 範圍外的改動（需審查確認）
- `MagicRendering.cs:319` **改了 1 行**：S30 kind 6 的深度改呼叫 `EarthDepthAt(…, effect, now−release)`。S30 kind 6 10147 的讀取點只在這裡，不改就做不到 ④。
- 舊測試的斷言寫的是被 FU7 推翻的推測，改成新規則：
  - `CookBookFollowupTests.cs`：篩選順序（表內順序）、`Panel` 段的單欄列測試移到新檔（只留 IUiClosablePanel 與預設 `Cook`）、暫行清單的列索引。
  - `CookAndHistoryTests.cs`：「量表畫出」→「沒有倒數不畫」。
  - `SkillLightTests.cs`：兩個 kind 6 10147「29／58」→「先在收包後 compose 建立，同一刻開始都是 58」。
- 沒改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

## 怎麼驗證
- **編譯**：`dotnet build engine/source/OfflineClient.csproj -c Release`（含全部 `[WorldModuleTest]`）**0 錯誤**；警告只有既有的 NU1701 與 `MagicUi.cs:20` 的 CS8600／CS8602（不是本次改的檔）。
  - 本機環境問題（與 #781 相同）：WebView2 套件在本機解析出 `.winmd` 參考（NETSDK1130）。沒有動共用快取，改用 `scratch/nuget782` 獨立還原（`.github/nuget.ci.config`），並用只放在 `scratch/build782/webview2-fix.targets` 的 `CustomAfterMicrosoftCommonTargets` 拿掉 `.winmd`、補上 net462 DLL 參考。**不是 repo 改動**。
  - `DemoChecks.csproj` 在本機無法單獨編譯（它參考 CI 先建好的 `build/client-product-dev`）；交給 CI。
- **新離線測試** `engine/source/CookAndDepthFollowupTests.cs`（`[WorldModuleTest("cook-and-depth-followup")]`，自動發現）：已驗證常數、篩選（≥48 在中間、超過 24 筆、表內順序、位置取圖示／說明）、格子座標與命中、捲動 4 格與上限、選取／製作／雙擊、提示框（只名稱、選取＋滑過的框位置與高度、下半部上移、右緣 796、下緣 600、23 byte 切行、沒有字串表、說明 id 0）、畫面（圖示、surf 24 選取框、缺圖替代框、量表裁切與不畫）、倒數模型與來源、1167／1100 進聊天列且不送、10147 同格不變／下一格讀新值、終點歸 0 後所有活著的 10147／10145 下一格回原本 z。全部合成資料。
- **依規定不在本機執行測試**，以 PR 的 CI（`map-continuity` 跑全部 `[WorldModuleTest]`）為準。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass。
- **CI（PR #801，run 42790193537／42790193444）**：`dotnet`、`map-continuity`（全部 `[WorldModuleTest]`，failed 0）、`npc-offline` 全部 pass；新群組 `cook-and-depth-followup` pass（87 checks，0 failures）。改過斷言的 `cook-book-followup`、`cook-and-history`、`skill-light` 也都通過。

## 未驗證什麼（待驗證）
- 真機畫面：圖示格、選取框 surf 24、提示框外觀（`sub_9709D0` 底色、調色盤 7／9、字色 `word_153F240[1]`、名稱外框）都沒看過。
- 推測：格子命中範圍 34×34；觸發 `sub_A5B9C0`（製作）的滑鼠事件；最大捲動；畫面偏移 0；送出的編號用食譜編號（原版是清單位置，本機表兩者相同）；renderer 以「compose 時序號改變」代表推進一格（讀值可能晚最多一個 compose tick）。
- 倒數的觸發（S_MESSAGE_CODE 6392／6394）沒接；3.81 不送是推測。
- 拖曳後原版清單命中框會變成 (+21,+42)–(+155,+244)（`sub_A5D3B0`），引擎沒有拖曳，未處理。

## 產出路徑
- `engine/source/NativeUi/CookBookPanel.cs`、`engine/source/Cooking.cs`、`engine/source/CookBookFallbackUi.cs`、`engine/source/ProjectileRendering.cs`、`engine/source/MagicRendering.cs`（1 行）
- 新測試 `engine/source/CookAndDepthFollowupTests.cs`；改舊測試 `CookBookFollowupTests.cs`、`CookAndHistoryTests.cs`、`SkillLightTests.cs`
- 本報告 `docs/reports/issue-782.md`

## 給下一棒的提示
- 要讓量表在別的伺服器動起來：在 S_MESSAGE_CODE 的處理加「6392／6394（以及 `0x9441F0` 附近其他 id）→ `World.BeginCookCountdown(食譜欄4, now)`」，食譜是 `dword_153F290`（上一次送出的清單位置）。要讀 `sub_942B40` `0x944190`–`0x94436E` 確認全部 id。
- `followups-7.md` 第 8 節的「欄距／列距從 `sub_A5AAC0` 傳給 `sub_8FAB00`」要更正：`sub_8FAB00` 是捲軸，數值在 `sub_A5AA10 → sub_88C0E0`（34, 34）。Q6-9 的「料理清單欄距／列距」可以標為已查。
- 真機 smoke（Codex／dot）：開料理書 → 看 4×6 圖示格、捲動一次一列、滑過名稱、選取後滑過出現提示框；沒有料理書時按製作 → 聊天列「為了製作料理，所以需要料理書。」；地裂（S30 kind 6/7）連放時深度不再一個比一個高。
