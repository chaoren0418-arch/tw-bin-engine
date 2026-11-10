# Issue #746 報告：T316 Q4 補做（血盟關注、盟戰與求婚的入口、代碼聊天名稱、盲目職業加成）

依據 [`docs/reference/followups-6.md`](../reference/followups-6.md)（FU6，T304／#716）。分支 `task/746`。

## 做了什麼

| 項目 | FU6 | 改動 |
|---|---|---|
| C129 關注血盟 | §1 | `Clan.cs`：`ClanProtocol.EncodeAttentionAdd/Remove/GetList`（`[129,0,名稱,0]`、`[129,1,名稱,0]`、`[129,2]`，名稱截到 16 個 Big5 byte，不切斷雙位元組字）；`LoginConnection.SendClanAttention`／`AddClanAttention`／`RemoveClanAttention`（刪除時每個名稱各送一包，依序送）。 |
| 血盟窗入口 | §1、§3 | `NativeUi/PledgePanel.cs`：側欄加「新增」（AddFocusPledge，輸入框 3361 → 子碼 0，名稱空白不送）、點清單列切換勾選（CheckFocusPledge，不送包）、「刪除」（DelFocusPledge，確認框 4118 → 每個勾選的血盟各一包子碼 1）、ChangeWarState 三鈕（1 宣戰：先查 478／785 再開 577；2 停戰：579；3 投降：578，後兩者沒有本機檢查）。`IPledgeWindowSource` 加 4 個有預設實作的成員（預設＝沒接上）。 |
| 聊天指令 | §1、§3、§4 | `PlayerChat.cs`：`ClientChatCommands` 解析 `/focus`（`/注視`）、`/war`（`/宣戰`）、`/surrender`（`/投降`）、`/cease`（`/停戰`）、`/propose`（`/求婚`）、`/divorce`（`/離婚`），在 `SendPlayerChat` 內編成封包、走同一個送出閘門，不當聊天送出。`/focus` 不帶參數送子碼 2（只有這裡送）。 |
| C227 本機檢查 | §3.2 | `ClanWar.cs`：`ClanWarProtocol.CheckDeclare`（478 → 785 → 234 → 變身中不送不提示 → 等級門檻 232／HTML 475 → 272 → 名稱空白不送）、`CheckChatSurrenderOrCease`（只查職業）、`HtmlSurrenderOrCease`、`WindowDeclarePrecheck`、`WindowState`（1 宣戰／2 停戰／3 投降）。擋下時把原版的字印在聊天欄。 |
| S84 印聊天欄 | §3.1 | `ClanWar.cs`：type 1／8／2／3／4／6／7 → 字串 226／235／228／227／231／224／225（`%0`＝血盟 1、`%1`＝血盟 2），一律印聊天欄、字色 palette[24]；沒有字串表時用原本 `Describe` 的同義文字。戰爭中旗標與對手清單照舊。WinForms 三鈕盟戰窗保留為備援，標「引擎自訂」。 |
| 求婚／離婚 | §4 | `Marriage.cs`：`MarriagePackets.Divorce()`＝`[50,1]`；`/propose`、`/divorce` 只送包（原版不檢查），送出後 `MarriageCommandSent` 更新提示（面前有角色時照舊記下對象）；註解與過渡頁文字改成「原版入口是聊天指令與 HTML propose／divorce，3.81 範本沒有連結」，過渡頁標「引擎自訂」。 |
| S161 `$數字` | §6 | `PlayerChat.cs`：`PlayerChatProtocol.ExpandDesc`（sub_A11520 規則：行號查 desc-c.tbl、超出範圍 `???`、吃掉數字後的 `;`、有展開時開頭 `+0` 去三字；`leadingOnly` 給職業外型說話者用，3.81 用不到）。表用 `TextArchive.ReadDesc()`（核 pin），座標分享已載入時共用 `LocationShareProtocol.Desc`；文字沒有 `$` 時不載入。 |
| 盲目職業加成 | §2 | `Afflictions.cs`：`WorldSession.OwnClass`＝選中角色的 `LoginCharacter.Class`（`LoginConnection.SelectedCharacterClass`，依 `worldSelection` 找）；`BlindClassBonus`＝職業 2 或 4，取代原本「封包未查到、永遠 false」的推定（setter 只留給離線測試覆寫）。 |
| 測試 | — | 新檔 `engine/source/Q4FollowupTests.cs`：`[WorldModuleTest("q4-followup")]`（`--module-tests` 自動發現；engine/source 的 followup 測試都用這個擴充點）。 |

### 超出指定檔案清單的兩處（請審查時留意）

- `engine/source/OnlineMovement.cs`（1 行）：`LoginConnection` 建構子加 `World.OwnClassSource=()=>SelectedCharacterClass;`。WorldSession 拿不到角色清單，指定的檔案裡也沒有連線建立時一定會執行的地方，這是最小的接法（同一行已經在接 `ActionClock`、`PickupSessionGeneration`）。
- `engine/source/ClanUi.cs`（7 行）：`WorldPledgeSource`（產品端的血盟窗橋接，R4-E）實作新增的 4 個介面成員，轉給 `AddClanAttention`／`RemoveClanAttention`／`ClanWarWindowPrecheck`／`SendClanWarFromWindow`。不改的話產品端的血盟窗新鈕只會顯示「沒有接上」。

## 怎麼驗證

- **編譯（已驗證）**：本機 `dotnet build engine/source/OfflineClient.csproj -c Release`。本機 SDK 10.0.300 對 WebView2 的 `.winmd` 參照報 NETSDK1130（發生在參照解析階段、還沒進到 C# 編譯，與本任務的改動無關；main 上是否相同本輪沒另外試），所以用 scratch 下的暫時 targets（`-p:CustomAfterMicrosoftCommonTargets=scratch/t746-fix.targets`，只移除 `.winmd` 參照、不進 repo）讓編譯進到 C#。結果：只剩 `WebPanel.cs`／`WebLocalResource.cs` 找不到 WebView2 型別的 15 個既有錯誤，本任務的程式與測試**沒有任何編譯錯誤**。正式編譯以 CI（ubuntu 交叉建置）為準。
- **`check-no-game-assets.py`（已驗證）**：`{"status": "pass", "trackedFiles": 4140, "errors": 0}`。
- **離線測試（待 CI）**：依規定本機不執行編譯出的程式；`q4-followup` 與既有單元測試交給 PR 的 GitHub Actions。測試內容：C129 三種編碼與 16 byte 截斷；13 個指令與別名的解析、非指令不誤判；職業 2／4 有加成、其他與未知沒有；S84 七種 type 的字串 id、文字、palette[24]、對手清單增減、沒有字串表的同義文字；`CheckDeclare` 每一步的擋法與順序、HTML 25／475、職業未知不擋；`/war`、`/投降`、`/cease`、`/focus`、`/注視` 實際經 `SendPlayerChat` 送出的位元組，以及等級不足 232、非王族 478、階級 7 785、已交戰 234 印在聊天欄而不送；血盟窗 `WorldPledgeSource` 的宣戰前檢查、投降無檢查、宣戰門檻 15、新增一包、刪除兩包；`/propose`、`/離婚` 的 C50；`$4`、中間的 `$`、`???`、`;`、`+0`、`leadingOnly`、沒有表不改，S161 0／2／3 解碼與 WorldSession 收到的行；原生血盟窗的新增輸入框（3361、退格、16 字上限、Esc）、勾選切換、刪除確認（4118 占位）、清單更新清掉舊勾選、三態輸入框 577／579／578 與送出順序、名稱空白不送、擋下的提示、勾選記號有畫、關窗清狀態。
- **資源查詢（已驗證，PakViewer CLI `pakviewer-cli pak search/extract`，只讀，輸出在 `scratch/t746-pak/`，不進 git）**：`Text.idx` 搜尋 `string` 有 6 筆：`string-c.tbl`（3484 筆，SHA-256 `4EBBC1A7…43E9`，與 FU6 相同）、`string-c.tbl.bak`（3411）、`string-e.tbl`（1054）、`string-h.tbl`（1138）、`string-j.tbl`（1135）、`string-k.tbl`（1191）；`Tile.idx`、`Sprite.idx` 沒有 `string*`。所以字串 **4118 本機所有字串表都沒有**，確認框用「〔字串 4118〕」占位。

## 未驗證什麼

- **CI 結果**：見 PR；本機沒有執行任何測試。
- **實機（待驗證，交給 Codex／dot）**：在 P24Lab 以測試王族角色輸入 `/focus`、`/focus 對方盟`、`/war 對方盟`、`/surrender`、`/cease`、`/propose`、`/divorce`，確認伺服器回應（S200 清單、S84 聊天文字、S219 653／654）；血盟窗側欄的新增／勾選／刪除與三態鈕；NPC 對話裡 `$` 名稱的顯示；妖精／黑暗妖精角色在盲目 2 時畫面的 6/31。
- **推測**：`/war` 傳給 sub_F46290 的等級門檻沒查，比照血盟窗用 15；235 的 `%0` 代入對方血盟；`$` 後不是數字照原樣、表內容的 `%i` 照原樣；英文指令不分大小寫；側欄輸入框的按鍵（Enter／Esc／Backspace）比照公告編輯；比對戰爭清單用同名（sub_A0D4D0 沒追）。
- **沒有做**：HTML 動作 `pledgefocusadd`、`declarewar`（門檻 25、訊息 475，規則已在 `CheckDeclare`）、`surrender`、`cease`（規則 `HtmlSurrenderOrCease`）、`propose`、`divorce` 的接線——分派在 `NpcInteraction.cs`（不在本任務檔案範圍），而且 `pledgefocusadd`／`surrender`／`cease` 需要 NPC 頁上的共用輸入框。`Ambience.cs` 的 `NightVision` 改用 `OwnClass`（不在範圍）。S84 切背景音樂 11／12／13（選配）。`AfflictionFollowupTests.cs:55` 的說明文字仍寫「來源封包未查到」（斷言本身仍成立：沒有連線的 session 沒有職業），不在範圍沒改。

## 產出路徑

- 程式：`engine/source/Clan.cs`、`ClanWar.cs`、`Marriage.cs`、`PlayerChat.cs`、`Afflictions.cs`、`NativeUi/PledgePanel.cs`、`ClanUi.cs`（7 行）、`OnlineMovement.cs`（1 行）。
- 測試：`engine/source/Q4FollowupTests.cs`（`q4-followup`）。
- 報告：本檔。

## 給下一棒的提示

1. **HTML 動作接線**（建議開 code 任務，範圍 `NpcInteraction.cs`）：在 `ClientHyperAction` 加 `declarewar`（取 HTML 參數 1 當血盟名，`ClanWarProtocol.CheckDeclare(self,名稱,SiegeLevel,SiegeLevelMessage)` → `ClanWarProtocol.Encode`）、`surrender`／`cease`（`HtmlSurrenderOrCease` → 需要輸入框 578／579）、`pledgefocusadd`（輸入框 3361 → `AddClanAttention`）、`propose`／`divorce`（直接送 `MarriagePackets.Propose()`／`Divorce()`）。
2. `Ambience.cs` 的 `NightVision` 可以直接用 `WorldSession.OwnClass is 2 or 4`（AM§1(f)），順手改 `AfflictionFollowupTests.cs:55` 的說明。
3. 本機要編譯時，WebView2 的 NETSDK1130 用 `scratch/t746-fix.targets` 的作法繞過（只移除 `.winmd` 參照），只能拿來看 C# 錯誤，不能拿產物執行。
