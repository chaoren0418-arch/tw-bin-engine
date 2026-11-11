# Issue #992 — T434：CU-2 classic 功能補洞（G3、G4、G5、G9、G10、G11）

依據：`docs/reference/classic-ui-audit.md` §3、§4（T413／#953）。分支 `task/992`。目標：讓 `--classic-ui` 也能用這六項功能，原生 UI 預設的行為不變。

## 做了什麼

| 項目 | 檔案 | 內容 |
|---|---|---|
| **G3** 個人商店開店 | `PersonalShopIntegration.cs` | 控制器加 classic 模式（`ClassicOpen`／`ToggleClassic`／`SelectClassic`／`SubmitClassic`／`CancelClassic`）。沒有原生 `PersonalShopPanel`、而且主機允許（`IPersonalShopHost.ClassicSetup`，預設 false；產品＝「原生 UI 沒開，或 PersonalShop_Window 不存在或故障」）時，`Open()` 改成切換 WinForms 開店設定，所以 **`/shop` 也能用**（`PlayerChat.cs` 的 `OpenChatWindow` → `shop.Open()`，沒改那個檔）。個人商店分頁多一份 16 列清單（出售 1–8、收購 1–8）＋「送出開店（OK）」「取消（Cancel）」。放入、清除、訊息照用既有按鈕（背包選取＋單價／數量）。OK 走既有 `Setup.BuildOpen` → `SendOpen`（C38 type 0，送前重新核對背包、876／904 檢查）；已開店（S119 動作 70）時 OK 只關、Cancel 送 C38 type 1，與 R10-M 原生規則相同。開啟時自動切到「個人商店」分頁並把視窗叫出來。原生面板接著時這些控制項不出現，流程不變。 |
| **G4** op 175 以物品學魔法 | `NpcUi.cs` | 原生 TRADE_WINDOW 沒有在畫 op 175 清單時（classic、TRADE_WINDOW 故障或沒接上學魔法來源），把 `SpellExchangeTradeWindow()` 的列（已學的不列）做成 NPC 面板的勾選清單；「以物品學習」走既有 `LearnSpellExchange`（C191），「關閉」走 `CloseSpellExchange`（不送封包）。判斷拆成 `ClassicSpellExchange`。原生窗接著時一律由原生窗負責，就算使用者把它關掉也一樣（原生行為不變）。 |
| **G5** 選項入口 | `ObjectRendering.cs`、`ClientEffect6016.cs` | 「名稱顯示」分頁加三個勾選框：地面物品名稱、死亡怪物半透明、妖精記憶座標傳送。「聲音」分頁加三條類別音量：音樂、效果音、環境音。全部透過同一個 `OptionWindowHost`（`RingWindows.cs:197`）讀寫，跟原生選項窗用同一份值、同一個存檔（`option-ui.txt`、`sound-volume.cfg`）。妖精傳送在 Lv < 50 時寫不進去，勾選框會跳回實際值；切到分頁時重新讀一次值。 |
| **G9** 中央訊息 | `PacketBoxScreen.cs` | `WorldSession.CenterMessagesToChat` 預設 false。`AttachPacketBoxScreen` 在原生 `CENTER_MESSAGE` 不能用時（classic、面板故障）把它設成 true，S250/84 type 1／2 就額外印進聊天欄：`$nnn` 用同一個名稱表展開，色碼用 palette[2]（跟中央訊息同一格）。原生面板能用時不印，所以不會重複。 |
| **G10** 盟屋拍賣／地圖 | `RingWindows.cs`（只動 classic 入口 `SyncRingWindows`） | 原生 `AgitAuctionPanel`／`AgitMapPanel` 不能用時，服務分頁多一頁「盟屋拍賣」：S156 清單選一筆，走既有 `SendAgitAuctionSelect`（C125 select）；S187 的「查詢這間盟屋」走既有 `SendAgitSelect`（674 鈕）。新的 S156／S187 進來時自動把這頁叫出來（`ClassicAgitTracker`）。回包 agsel 照舊顯示在 NPC 對話面板；agapply 照舊用 NPC 面板的數字框。原生能用時這頁不加。 |
| **G11** 1813／1516 確認 | `BuddyUi.cs`、`Fishing.cs` | 新增 `ClassicConfirm`（WinForms MessageBox 是／否，預設按鈕是「否」；測試可以換掉）。classic 下好友分頁「刪除好友」會先問 1813（`FriendPanels.DeleteConfirmText`）；HUD 收竿鈕會先問 1516（`FishingQuitText.Confirm`），回答「是」之後再確認一次還在釣魚（`FishingQuitRules.StillCurrent`）才送 C62。原生 UI 開著時不問，原生窗照舊用自己的聊天列 Y/N。 |
| 測試 | 新檔 `ClassicGapTests.cs` | `[WorldModuleTest]` 自動發現 6 組：`classic-personal-shop`、`classic-spell-exchange`、`classic-options`、`classic-center-message`、`classic-agit`、`classic-confirm`。 |

沒改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`；也沒有新增原生面板，所以不需要 `IUiClosablePanel`。

## 已驗證／推測／待驗證

- **已驗證（離線、靜態）**：封包都走既有路徑（C38、C191、C125、C202、C62），沒有新的封包格式。值的存放沿用 `OptionWindowHost`／`PlayerAudioSettings`。
- **推測（引擎的備援設計，不是原版行為）**：
  - 原版只有原生畫面，沒有 WinForms。G9「印進聊天欄」、G11 的 MessageBox、G3／G10 的清單分頁，都是 classic 才有的替代入口。
  - G3 的 `/shop` 在 classic 是「切換」設定（照 sub_F480C0 的切換語意）。
  - G10 的列格式（名稱、面積、截止日、價格）是推測，原生窗的欄位格式本身也是推測。
- **沒有使用遊戲資源**：這次只有 WinForms 控制項，不畫任何圖，也沒有宣稱缺資源，所以不需要用 PakViewer 查。字串 1813／1516 照字串表；表不在時用既有的 Text.pak 原文 fallback。

## 怎麼驗證

- 本機：`dotnet restore`／`dotnet build source/OfflineClient.csproj -c Release -p:EnableWindowsTargeting=true -r win-x64 -p:SelfContained=false`（CI 同樣的參數，nuget.ci.config）。結果 0 個錯誤；3 個警告都在既有檔案（`MagicUi.cs`、`HtmlActionTests.cs`），本次改的檔案沒有新警告。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass（4585 檔，0 錯誤）。
- 依本機規定沒有執行任何測試程式；`--module-tests`（含新的 6 組）與 DemoChecks 交給 CI（結果見 PR）。
- CI（PR #1001，run 43027152430／43027152581）：`dotnet`、`map-continuity`、`npc-offline` 全部 pass。新的 6 組也都通過，沒有失敗：`classic-personal-shop` 20 項、`classic-spell-exchange` 11 項、`classic-options` 10 項、`classic-center-message` 3 項、`classic-agit` 10 項、`classic-confirm` 10 項。

## 未驗證（待驗證，交給 Codex／dot 真機）

- 在 `--classic-ui` 下的實際操作：`/shop` 會不會切到「個人商店」分頁、開店後角色會不會進入擺攤狀態、頭上訊息；op 175 清單在 NPC 面板裡的顯示與送出；選項勾選框和音量捲軸改了之後重開遊戲還在不在；中央訊息印在聊天欄的顏色；盟屋拍賣頁自動跳出、送出 select 後 agsel 頁面；1813／1516 對話框。
- MessageBox 是 WinForms 的模態視窗。它開著的時候，遊戲畫面的計時器還是會跑（推定）；釣魚剛好結束的情況已經用 `StillCurrent` 擋掉送出。
- 個人商店清單把 `personalShopActions` 的高度從 150 拉到約 450（只在 classic）；在小視窗時分頁要捲動才看得到（推定）。

## 產出路徑

- `engine/source/PersonalShopIntegration.cs`、`NpcUi.cs`、`ObjectRendering.cs`、`ClientEffect6016.cs`、`PacketBoxScreen.cs`、`RingWindows.cs`、`BuddyUi.cs`、`Fishing.cs`
- `engine/source/ClassicGapTests.cs`（新）
- `docs/reports/issue-992.md`

## 給下一棒的提示

- classic-ui-audit §3 的 G3、G4、G5、G9、G10、G11 程式端已經做了，下一輪盤點可以把 §1.2 的 PersonalShop_Window、TRADE_WINDOW op 175、OptionUI、AgitMap／Agitmap 拍賣，以及 §1.1 的 R9H／R9P，改成「classic 有替代入口」。
- 還沒做的組別：CU-1（G1、G2、G6、G7、G8）、CU-3（G12–G15，`NpcUi.cs` 跟這張有重疊，合併後再做比較不會衝突）、CU-4（G16–G19）。
- G3 的 classic 清單仍然要「先在背包選取物品」，跟原生的替代做法一樣；如果要在清單裡直接挑背包物品，可以另開任務。
