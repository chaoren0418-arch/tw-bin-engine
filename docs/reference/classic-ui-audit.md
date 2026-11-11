# `--classic-ui` 備援盤點（T413／#953）

基準：`main` `3528e1c2`（2026-11-11）。**只看程式碼**：沒有建置、沒有執行、沒有開遊戲。行號都對照過原始檔，路徑相對於 `engine/source/`。用途：給真機 smoke（`docs/testing/smoke-m7.md` §10）和下一輪 gap-matrix 參考。

## 0. 結論

- **面板總數**：
  - `[UiPanel]` 成員 **58 個**，展開後是 **60 個註冊**（`FriendPanels.Registrations` 一次註冊 3 個窗，`FriendPanel.cs:58,67`；PersonalShop 4 個各算一個）。
  - `MiniMapPanel.Create` 另外用 `root.Add` 加進 `FieldMapPanel`（`MiniMapPanel.cs:31`），所以 UiRoot 裡共 **61 個面板**。
  - 公告板原生窗不經 `[UiPanel]`，是 `BoardUi.cs:154–178` 動態加入的，另列在 §1.4。
- **IUiClosablePanel**：
  - 60 個註冊中有 **44 個**實作（另有 FieldMap）。
  - **16 個**沒實作，全部在 Bar／Float 層（HUD 列、提示列），不在視窗堆疊裡，符合共同規則。
  - 規則要求的「新視窗型面板實作 `IUiClosablePanel`」**沒有違反的**。
- **`--classic-ui` 的機制**：`UiOptions.Resolve` 回 `Enabled=false`（`NativeUi/UiRoot.cs:289`）→ `InitNativeUi` 直接 return（`NativeUi/UiClientBridge.cs:25`）→ `NativeUiActive` 永遠 false（`:21`）→ 所有原生守衛都走 WinForms。
  - WinForms 服務頁大多在開發工具窗裡：「服務」分頁（`ProductEntry.cs:62`、`DevToolsForm.cs:79`），由舊 HUD「服務」鈕打開（`PlayerHudView.cs:100` → `PlayerHudIntegration.cs:25` → `ProductEntry.cs:71`），或用 Ctrl+Shift+F9（`DevToolsForm.cs:55`）。
- **缺口共 19 項**（§3），另有 **18 個註冊只有原生、沒有任何 WinForms 對應**（§2.1）。
  - 最嚴重的是 G1：classic 的死亡窗被搬進開發工具，場景上看不到。
  - 其次是 G3：classic 不能開個人商店。
- **R5–R10（#733–#930）期間沒有 WinForms 入口被刪掉或改名**。
  - 查法：用 `git log -S`／`-G` 查 `ShowDialog`、`Show(this)`、`new Form`、`new TabPage`、`OpenMail`、`ShowBoardUi`、`playerLoginStrip`、`charactersBox`、`OpenCharacterCreationDialog`、`MinimapUi.Attach` 等。
  - 這段期間只有新增：#741 離開窗、#765 選角、#808 公告板、#817 重置窗故障時改開 WinForms。
  - 唯一被拿掉的是 op 175 的暫行清單，發生在更早的 R3-F（#679）。

判定標記：✅ 可達（追過呼叫鏈）／⚠️ 部分或要手動找／❌ 不可達／— 只有原生／❓ 不確定（推測）。

## 1. 面板清單

### 1.1 HUD 與常駐列（Bar／Float）

| 面板 Id | 類別（註冊檔:行） | Closable | classic 的 WinForms 入口（檔:行，怎麼點） | classic | native 時重複？ |
|---|---|---|---|---|---|
| LEFT_WINDOW | LeftStatusPanel（NativeUi/LeftStatusPanel.cs:14） | 無 | HUD 頂列 `PlayerHudView.cs:24–31`（內容在 `PlayerHudIntegration.cs:73–75`）；開發工具「角色」分頁 `Rewards.cs:47` | ⚠️ MR／正義值／SP／負重沒有逐項對應 | 否（HUD 只在 `!NativeUiActive` 時顯示，`PlayerHudIntegration.cs:10`） |
| CENTER_WINDOW | ChatPanel（ChatPanel.cs:26） | 無 | HUD 聊天抽屜 `PlayerHudChat.cs:3–60`；「聊天」鈕 `PlayerHudView.cs:99`；Enter `PlayerChatKeyboardFlow.cs:25–31` | ✅ | 否 |
| HpMp | HpMpPanel（HpMpPanel.cs:82） | 無 | HUD HP／MP 條 `PlayerHudView.cs:24–28,53–58` | ✅（外形與長度切換只有原生） | 否 |
| MiniHpMpBar | HpMpHeadBarPanel（HpMpPanel.cs:252） | 無 | 無 | — | — |
| 小地圖 | MiniMapPanel（MiniMapPanel.cs:11） | 無 | HUD「小地圖」鈕 `MinimapUi.cs:21`（掛在 `PlayerHudView.cs:103`） | ✅ | 否（`MinimapUi.cs:41–43`） |
| Field_Map | FieldMapPanel（由 MiniMapPanel.cs:31 加入） | 有 | 無（沒有 Ctrl+M；classic 下 M 鍵是施法） | — | — |
| EX_MACRO_WINDOW | ExMacroPanel（ExMacroPanel.cs:14） | 無 | 無（HUD 只有 8 格） | — | — |
| RIGHT_WINDOW | MacroPanel（MacroPanel.cs:15） | 無 | HUD 快捷列 `PlayerHudView.cs:98`；F5–F12 `PlayerHudIntegration.cs:53–62` | ✅（F1–F3 分頁在 classic 刻意不作用） | 否 |
| PARTY | PartyPanel（PartyPanel.cs:11） | 無 | HUD「隊伍」鈕 `PartyUi.cs:15–21`（`PlayerHudView.cs:107`） | ✅ | 否（`PartyUi.cs:36`） |
| PARTY_PET_RT | PartyRtPanel（PartyRtPanel.cs:11） | 無 | `PartyUi`＋開發工具「寵物」分頁 | ⚠️ 分在兩處 | 否 |
| BUTTON_2STAGE_LIST | MenuButtonsPanel（MenuButtonsPanel.cs:50） | 無 | 各鈕分散：地圖 `MinimapUi.cs:21`、信件 `PlayerHudView.cs:104`、技能／背包 `:100`、狀態在「角色」分頁 | ⚠️ 血盟、選項 option1、說明頁、統計窗、Ctrl+P、Tab／Ctrl+I／Ctrl+S 不可達（G6、G8） | 原生窗不能用時改開開發工具（`UiClientBridge.cs:205–212,237`），這是設計好的退路 |
| STATUS_EFFECT_ICONS | BuffIconsPanel（BuffIconsPanel.cs:171） | 無 | HUD 狀態圖示 `PlayerHudView.cs:102,127,135`（資料 `StatusIcons.cs:77`） | ✅ | 否 |
| RED_NOTICE | RedNoticePanel（RedNoticePanel.cs:27） | 無 | 場景紅字 `DisplayNotices.cs:171`（S105 自動顯示） | ✅ | 否（`NativeUi/State/RedNoticeState.cs:53`，含故障檢查） |
| CENTER_MESSAGE | CenterMessagePanel（PacketBoxScreen.cs:265） | 無 | 無（S250/84 只進佇列，`PacketBoxScreen.cs:186–201`） | — | — |
| YES/NO | YesNoPanel（YesNoPanel.cs:25） | 無 | 提示列＋YES／NO 鈕 `YesNoPrompt.cs:251–286`；Enter `:256–264` | ✅ | 否（`:274,276`） |
| ActionUI_Button | ActionOpenButtonPanel（NativeUi/ActionWindowPanel.cs:534） | 有 | 無 | — | — |
| R9B_MapSendPrompt | MapSendPromptPanel（MapCommunicationPanel.cs:149） | 有 | 無（classic 只有聊天列「指令 · 發送座標」提示，`PlayerChat.cs:46`） | — | — |
| R9H_FriendDeletePrompt | FriendDeletePromptPanel（FriendPanel.cs:91） | 無 | 無；classic「刪除好友」不問 1813，直接送出（`BuddyUi.cs:38`） | —（G11） | — |
| R9P_FishingQuitPrompt | FishingQuitPromptPanel（FishingPanel.cs:297） | 無 | 無；classic 收竿不問 1516，直接送 C62（`Fishing.cs:126`） | —（G11） | — |
| PersonalShop_Prompt | PersonalShopPromptPanel（PersonalShopIntegration.cs:36） | 有 | 無 | — | — |
| PersonalShop OK／Cancel 守衛 | PersonalShopOpenGuardPanel（PersonalShopIntegration.cs:38、:39） | 有 | 無 | — | — |

### 1.2 視窗（Window 層，在視窗堆疊與 ESC 處理範圍內）

| 面板 Id | 類別（註冊檔:行） | classic 的 WinForms 入口（檔:行，怎麼點） | classic | native 時重複？ |
|---|---|---|---|---|
| SummonMonster_Window | SummonPanel（SummonPanel.cs:136） | 「寵物」分頁暫行清單 `SummonFallbackUi.cs:75–88`、`PetUi.cs:133`；收到召喚請求時自動跳分頁（`SummonFallbackUi.cs:66–73` → `PlayerHudServices.cs:17–24`） | ✅ | 否；但只看 `NativeUiActive`，面板故障時沒有備援（G16） |
| PetControl | PetControlPanel（PetControlPanel.cs:60） | 「寵物」分頁按鈕 `PetUi.cs:123–126,135` | ⚠️ 要手動開 | 否 |
| AgitMap_Window | AgitMapPanel（AgitMapPanel.cs:66） | 無（`AgitHouseMapReceived` 沒有訂閱者，`Agit.cs:81`） | —（G10） | — |
| UserPlayStatsUI | UserPlayStatsPanel（UserPlayStatsPanel.cs:33） | 死亡窗 `recoveryPanel`（`Recovery.cs:412–439,550`）、「死亡選單」鈕（`:551`） | ❌ 推測：被搬進開發工具（G1） | 否（`:550` 有 `!native`；`:551` 沒有守衛，但按鈕在開發工具裡） |
| UserObtainUI | UserObtainPanel（UserObtainPanel.cs:19） | 無 | — | — |
| QUIT_WINDOW | QuitWindowPanel（QuitWindowPanel.cs:19） | `QuitFallbackUi.cs:33–49`（`Form.Show`）；死亡逾時後自動開（`Recovery.cs:438,549`） | ✅ | 否（`QuitFallbackUi.cs:30`，含故障檢查） |
| 座標清單 | LocationListPanel（LocationListPanel.cs:61） | `/loc`（`PlayerChat.cs:461`）；聊天連結 `LocationShare.cs:292–296` | ⚠️ 能發送、能點連結；10 筆清單本身不可達 | **WinForms Label 疊在原生畫布上**（G12） |
| COOK_BOOK_WINDOW | CookBookPanel（CookBookPanel.cs:56） | `CookBookFallbackUi.cs:27–45`（`Form.Show`，S250 52 由 `Cooking.cs:307–311` 自動開） | ✅ | 否（`CookBookFallbackUi.cs:30`）；不看故障（G16） |
| 血盟倉庫紀錄 | ClanWarehouseHistoryPanel（ClanWarehouseHistoryPanel.cs:23） | `ClanWarehouseHistoryUi.cs:41–44`（`Form.Show`，S250/117 自動開） | ✅ | 否（`:35–39`）；不看故障（G16） |
| TRADE_WINDOW | TradeWindowPanel（TradeWindowPanel.cs:50） | 買賣 0／1：NPC 分頁數量框 `NpcUi.cs:75`；修理 6：`WeaponRepair.cs:143–148`；學魔法 7（op 44）：NPC 分頁清單；私商 13／14：個人商店分頁 `PrivateShopUi.cs:47–57` | ⚠️ op 175 沒有清單（G4），其他可達 | 買賣 0／1 讓位沒做完（G14）；其餘已讓位（`WeaponRepair.cs:143`、`SpellExchangeUi.cs:24`、`PrivateShopUi.cs:51`） |
| PersonalShop_Window | PersonalShopPanel（PersonalShopIntegration.cs:34） | 個人商店分頁「開店設定窗」鈕，但只在原生面板接上時才啟用（`:441`），提示寫「原生 UI 未啟動…無法開啟」（`:448`）；`/shop`（`PlayerChat.cs:313–315`）也走不通 | ❌（只剩關店鈕，G3） | 否 |
| Friend／Exclude／Mailexclude_Window | FriendPanel（FriendPanel.cs:67） | 好友／封鎖分頁 `BuddyUi.cs:119–120` | ✅ | 否（原生窗本來就只能用這顆 WinForms 鈕打開，`BuddyUi.cs:33,126`；原版沒有開窗鍵） |
| HYPER_TEXT_WINDOW | HyperTextPanel（HyperTextPanel.cs:95） | NPC 對話頁 `NpcUi.cs:73`；S39 查詢頁 `NpcUi.cs:51–60,72`；數字框 `AmountPrompt.cs:169` | ⚠️ NPC 頁可達；S39 不會自動跳出（G7）；本機 HTML 頁不可達（G6） | 否（`NpcUi.cs:67–69`） |
| USERSHOP_HYPER_TEXT | UserShopPagePanel（PrivateShopUi.cs:158） | 個人商店分頁 `PrivateShopUi.cs:32`（換成店主為目標時自動跳） | ✅ | 否（`PrivateShopUi.cs:95`） |
| Agitmap_Window（拍賣） | AgitAuctionPanel（AgitAuctionPanel.cs:158） | 無（S156 只存資料，`RingWindows.cs:235–236`） | —（G10） | — |
| AuctionInfo_Window | AgitAuctionInfoPanel（AgitAuctionPanel.cs:754） | agsel 頁出現在 NPC 分頁 | ❓ Bid（C125 apply）連結沒查到 | 原生之間重複：agsel 同時進 HYPER_TEXT_WINDOW 和本窗（`AgitAuctionPanel.cs:41–42`，G15） |
| Bid_Window | AgitBidPanel（AgitAuctionPanel.cs:817） | NPC 分頁數字框 `AmountPrompt.cs:179–180` | ✅ | 否（`NpcUi.cs:125`、`RingWindows.cs:246–247`） |
| ITEM_WINDOW | ItemWindowPanel（ItemWindowPanel.cs:274） | HUD「背包」抽屜 `PlayerHudView.cs:100`、背包分頁 | ✅（Tab／Ctrl+I 只有原生） | 否 |
| SPELL_WINDOW | SpellWindowPanel（SpellWindowPanel.cs:42） | HUD「技能」抽屜、魔法分頁 | ✅（Ctrl+S 只有原生） | 否 |
| WAREHOUSE_WINDOW | WarehouseWindowPanel（WarehouseWindowPanel.cs:40） | NPC 分頁存入／取出清單 `NpcUi.cs:75`、`NpcServices.cs:62` | ✅ | 否（`UiClientBridge.cs:201–202`）；只看是否故障，不看窗能不能用（G17） |
| Pledge 窗 | PledgePanel（PledgePanel.cs:219） | 血盟分頁鈕 `ClanUi.cs:13` → `OpenClanWindow`（`:22`）→ `Form.Show`（`:108`） | ⚠️ 只能手動開 | 否（`ClanUi.cs:131`） |
| OptionUI | OptionPanel（OptionPanel.cs:143） | 名稱顯示分頁 `ObjectRendering.cs:687`；聲音分頁 `ClientEffect6016.cs:31–41` | ⚠️ 妖精傳送、掉落物名稱、死怪透明、各類別音量沒有入口（G5） | 否 |
| GameRanking_Window | GameRankingPanel（GameRankingPanel.cs:93） | S39 查詢頁進 NPC 分頁（`PlayerQuery.cs:105–116`） | ❓ 不會自動跳出（G7） | 否（`GameRankingPanel.cs:139`） |
| BookmarkWindowPanel | BookmarkWindowPanel（BookmarkPanel.cs:79） | 記憶座標分頁 `BookmarkUi.cs:109`；選目的地時自動叫出（`RingWindows.cs:150,157–162`） | ✅ | 否（`RingWindows.cs:147–150`） |
| ReNStatusUIEx | RenewalStatusPanel（RenewalStatusPanel.cs:73） | 能力點分頁 `BonusStatsUi.cs:13–40` | ✅ | 否（`BonusStatsUi.cs:45`） |
| StatInfoUI | StatInfoPanel（StatInfoPanel.cs:67） | 無 | —（數值本來就刻意空白，K1） | — |
| FishingUI | FishingPanel（FishingPanel.cs:58） | HUD「釣魚中／收竿」鈕 `Fishing.cs:120–137`（`PlayerHudView.cs:146`） | ✅ | 否 |
| FishObtainUI | FishObtainPanel（FishObtainPanel.cs:29） | 無 | —（3.81 清單本來就是空的，FU10） | — |
| ResetStatUI | ResetStatPanel（ResetStatPanel.cs:67） | S64 → `CharReset.cs:69` → `CharResetUi.cs:68–78`（`ShowDialog`） | ✅ | 否；故障時自動改開 WinForms（`CharResetUi.cs:89–104`） |
| TRANSACTION_WINDOW | TransactionPanel（TransactionPanel.cs:134） | 交易分頁 `TradeUi.cs:9–28,81–86`（自動跳） | ✅ | 否（`TradeUi.cs:82`） |
| Action_Window | ActionWindowPanel（ActionWindowPanel.cs:216） | 沒有動作窗。替代：表情在「稱號／表情」分頁（`TitleAndEmote.cs:381`）、拾取 F4（`PlayerHudIntegration.cs:49`）、斜線指令自己打 | ⚠️ 顯示開關見 G5；隊長踢人與選對象送出沒有入口 | 否 |
| MapCommunicationSelectWindow | MapCommunicationPanel（MapCommunicationPanel.cs:42） | 沒有右鍵選單（`MinimapUi.cs` 不處理點擊）；替代 `/loc` | —（檔頭 `:19` 寫明是刻意的） | — |
| MapLocationSaveWindow | MapLocationSavePanel（MapLocationSavePanel.cs:48） | 無（檔頭 `:21`：舊 HUD 沒有存點） | — | — |
| RenewalMailUI | MailPanel（MailPanel.cs:94） | HUD「信件」`PlayerHudView.cs:104` → `MailUi.cs:7` → `OpenMail`（`:14–23`，`ShowDialog`） | ✅ | 否（`MailUi.cs:31–42`） |

### 1.3 登入、選角、建角（Float 層／登入畫面）

| 面板 Id | 類別（註冊檔:行） | Closable | classic 的 WinForms 入口 | classic | native 時重複？ |
|---|---|---|---|---|---|
| LoginUI | LoginPanel（LoginPanel.cs:96） | 有 | 登入列 `ClientLogin.cs:43–52`，被搬進開發工具「登入／選角」分頁（`DevToolsForm.cs:80`）；Ctrl+Shift+F9 打開 | ⚠️ 可達，但主視窗沒有任何提示（G2） | 否；S48 兩種模式都用 MessageBox（`UiClientBridge.cs:130–134`） |
| SelectUI | SelectCharPanel（SelectCharPanel.cs:264） | 有 | 同一個分頁：角色清單＋「進入世界」（`ClientLogin.cs:51`）、「刪除角色」（`CharacterManagementUi.cs:48–54`，MessageBox） | ⚠️ 同上 | 否（`CharacterManagementUi.cs:72–80`） |
| CreateUI | CreateCharPanel（CreateCharPanel.cs:180） | 有 | 「建立角色」→ `CharacterCreationDialog.ShowDialog`（`CharacterManagementUi.cs:47,62–66`） | ⚠️ 同上 | 否；面板不可用時才開 WinForms（`SelectCharPanel.cs:651–655`），是刻意的備援 |

### 1.4 公告板（不是 `[UiPanel]`）

- 原生窗：`BoardListWindow`／`BoardReadWindow`（`NativeUi/BoardPanels.cs`，繼承 `BoardButtonWindow:IUiClosablePanel`，`:94`）、`BoardList/Read/WriteContent`（`NativeUi/BoardPanel.cs:166,224,269`，都有 Closable）。由 `BoardUi.cs:154–178` 的 `AttachNativeBoard` 用 `root.Add` 加入。
- classic 入口：在世界中點公告板 → `NpcInteraction.cs:194` → `OpenBoardUi`（`BoardUi.cs:124`）→ `ShowBoardUi`（`:138–145`）。判定 ✅。
- native 時：WinForms 窗會隱藏（`:144`），但發文、刪文的確認仍跳 WinForms MessageBox（`BoardUi.cs:41–42`，G13）。

## 2. 只有原生、沒有 WinForms 備援

### 2.1 完全沒有 WinForms 對應（18 個註冊）

| 面板 | 處理建議 |
|---|---|
| CenterMessage（S250/84）、AgitMap（S187）、Agitmap 拍賣（S156） | 補 classic 顯示：G9、G10 |
| FriendDeletePrompt（1813）、FishingQuitPrompt（1516） | classic 補確認：G11 |
| PersonalShop_Prompt、OK 守衛、Cancel 守衛 | 跟 G3 一起做 |
| ActionWindow、ActionUI_Button | 功能分散，見 G5；動作窗本身寫進 gap-matrix 註明「classic 不提供」 |
| MapCommunication、MapSendPrompt、MapLocationSave | 檔頭已寫明刻意不做（替代為 `/loc`）；寫進 gap-matrix，不補 |
| ExMacro、HpMpHeadBar、UserObtain、StatInfo（K1 空白）、FishObtain（3.81 恆空） | 寫進 gap-matrix，不補 |

另外 Field_Map（MiniMap 附帶加入）也只有原生。

### 2.2 有對應但 classic 實際到不了

PersonalShop_Window（G3）、UserPlayStats 死亡窗（G1）、TRADE_WINDOW op 175（G4）、本機 HTML 頁（G6）、統計窗手動開與 Ctrl+P（G8）、座標 10 筆清單（§1.2）。

## 3. 缺口與建議修正（19 項）

**A. classic 入口看不到或被藏起來**

- **G1 死亡窗看不到（推測，需真機確認，SM54）**：`InitRecovery` 在 `InitDevTools` 之前執行（`Program.cs:115`）。`DevToolsForm.cs:84` 會把當時主視窗上除 `playerHud` 以外的控制項全部搬進開發工具分頁，`recoveryPanel` 和 `deathReopenButton` 都是在 `Recovery.cs:425` 加進主視窗的，所以也被搬走。結果是 classic 死亡時，場景上沒有重新開始／死亡選單。
  - 建議：在 `DevToolsForm.cs:84` 排除這兩個控制項（或讓 `Recovery.cs` 在 dev tools 建好後才加入）；`Recovery.cs:551` 的可見條件比照 `:550` 補上 `!native`。
- **G2 登入列沒有可見入口**：登入列和狀態列（`Program.cs:102`、`DevToolsForm.cs:80–82`）都在隱藏的開發工具裡。從 #462 起就是這樣，不是 R5–R10 造成的。
  - 建議：`ClientLogin.cs`／`DevToolsForm.cs` 在「非原生、而且不在世界中」時，自動打開「登入／選角」分頁，或在主視窗留一行提示。
- **G6 本機 HTML 頁在 classic 沒有入口**：說明頁、option1、option2（選單 5／6、Ctrl+H／V／C）只能經 `NativeUiCommands` 打開（`UiClientBridge.cs:353`）；`MenuButtonsPanel.cs:17–18` 的註解說「classic 下 option1 仍是 3.81 入口」，與現況不符。
  - 建議：在開發工具 NPC 分頁加三顆鈕呼叫 `ToggleMenuLocalPage`（它本身不依賴原生，`UiClientBridge.cs:226`），並更正註解。
- **G7 classic 下 S39 查詢頁不會自動跳出（推測）**：排名、/buddy 頁、伺服器頁都受影響，因為追蹤器只看 NPC 對話與交易狀態（`PlayerHudServices.cs:5–11,28`）。
  - 建議：追蹤器也看 `QueryVisible`／`QueryRevision`。
- **G8 統計窗手動開（選單 7、Ctrl+Q、Alt+X）與 Ctrl+P 截圖只有原生**（`UiClientBridge.cs:247,268`）。
  - 建議：在 ClientForm 的按鍵處理加 classic 分支，或把「classic 不提供」寫進 TASKS.md。

**B. classic 功能補洞**

- **G3 個人商店不能開店**：見 `PersonalShopIntegration.cs:441,448`，`/shop` 也走不通。
  - 建議：在個人商店分頁加一份不依賴 `PersonalShopPanel` 的 WinForms 設定清單，沿用 `PersonalShopController.Setup`／`SendOpen`。
- **G4 op 175（用物品學魔法）沒有清單**：見 `SpellExchangeUi.cs:10`，是 R3-F（#679）拿掉的。
  - 建議：在 `NpcServices.cs`／`NpcUi.cs:75` 把 `SpellTradeSource` 的列做成 NPC 分頁的數量清單。
- **G5 選項只有一半有 WinForms 入口**：
  - 建議：在名稱顯示分頁（`ObjectRendering.cs:687`）加掉落物名稱、死怪透明、妖精傳送三個勾選框；在聲音分頁（`ClientEffect6016.cs:31–41`）加類別音量。全部經由 `OptionWindowHost`（`RingWindows.cs:197`），保持同一份值、同一個存檔。
- **G9 CenterMessage（S250/84）在 classic 不顯示**。
  - 建議：`PacketBoxScreen.cs:187` 在非原生時把訊息印進聊天欄。
- **G10 盟屋地圖 S187 與拍賣清單 S156 在 classic 沒有入口**（`Agit.cs:81`、`RingWindows.cs:235–244`）。
  - 建議：做一份 WinForms 清單（NPC 分頁或新分頁），選了就走既有的 `SendAgitAuctionSelect`。
- **G11 classic 少了 1813（刪好友）與 1516（收竿）的確認**（`BuddyUi.cs:38`、`Fishing.cs:126`）。
  - 建議：各加一個 MessageBox 確認，和原生行為一致。

**C. 原生模式下的 WinForms 混用或重複（R7-G 讓位）**

- **G12 座標分享**：⊕ 標記和提示用的是 WinForms Label（`LocationShare.cs:343–352`），沒有 `NativeUiActive` 守衛，疊在原生畫布上；座標用的是邏輯尺寸 `PlayerSceneBounds`（`PlayerHudIntegration.cs:13`），視窗不是 800×600 時推測會對不準。
  - 建議：原生時把 Label 隱藏，⊕ 改由 FieldMap 或原生 Float 面板畫；或至少套用 `NativeUiViewport` 換算。
- **G13 公告板發文、刪文確認跳 WinForms MessageBox**（`BoardUi.cs:41–42`）。
  - 建議：原生時改用 YesNoPanel 或聊天列 Y/N。
- **G14 NPC 商店買賣 0／1 的讓位沒做完**：`NpcUi.cs:68` 的 `nativeOwnsAll` 在 `InteractionPage==null` 時是 false，WinForms NPC 面板仍然 Visible（清單是空的、提交鈕停用）。只在開發工具裡，不會自動彈出。
  - 建議：`nativeOwnsAll` 加上 `w.ShopNativeActive`，比照 `SpellExchangeUi.cs:24`。
- **G15 agsel 同時出現在 HYPER_TEXT_WINDOW 和 AuctionInfo_Window**（`AgitAuctionPanel.cs:41–42` 自註「推測」；原生和原生之間的重複）。
  - 建議：`NpcUi.cs` 的 `SyncNativeHyperText`（`:98–110`）在 AuctionInfo 顯示同一個板子時不送進 HyperText，比照 `HyperTextOmitsNumber`（`:124`）。

R7-G 式讓位**已經做到**的：私商（`PrivateShopUi.cs:51,95`）、學魔法 op 44（`SpellExchangeUi.cs:24`）、修理（`WeaponRepair.cs:143`）、倉庫、HyperText、交易（`TradeUi.cs:82`）、盟屋出價、書籤、信件、重置、QUIT、料理、血盟倉庫紀錄、召喚、YES/NO、紅字、選角、建角。**沒做完的**只有 G14。

**D. 原生故障時的備援不一致**

- **G16 只看 `NativeUiActive`、不看面板是否故障**：`SummonFallbackUi.cs:67,70`、`CookBookFallbackUi.cs:30`、`ClanWarehouseHistoryUi.cs:35`。原生面板故障時，兩邊都不顯示。
  - 建議：改成「已註冊而且沒故障」，比照 `QuitFallbackUi.cs:30`。
- **G17 `NativeWarehouseActive`（`UiClientBridge.cs:201–202`）只看是否故障，不看窗能不能用或是否被關掉**。
  - 建議：加上可用性或 `Dismissed` 判斷，比照 `TradeUi.cs:53`。
- **G18 原生 Active 但 LoginUI 或 SelectUI 不可用（沒有參考客戶端 XML）時沒有出路**：這時 HUD 隱藏、登入列在開發工具裡，畫面上也沒有指引。
  - 建議：`CharacterManagementUi.cs`（或 `UiClientBridge.cs` 的 `ApplyNativeUiStart`）偵測到這種情況就自動開「登入／選角」分頁，或畫一行提示。
- **G19 classic 的 HUD「信件」直接呼叫 `OpenMail`**（`MailUi.cs:7`），沒經過 `RequestMail` 的排隊機制。目前功能正常。
  - 建議：統一改走 `RequestMail()`。

## 4. 可合成的 code 任務（建議分組）

| 組 | 內容 | 主要檔案 | 優先 |
|---|---|---|---|
| CU-1 classic 入口可見 | G1、G2、G6、G7、G8 | `Recovery.cs`、`DevToolsForm.cs`、`ClientLogin.cs`、`PlayerHudServices.cs`、`NativeUi/MenuButtonsPanel.cs`（註解） | 高（G1 會影響 SM54） |
| CU-2 classic 功能補洞 | G3、G4、G5、G9、G10、G11 | `PersonalShopIntegration.cs`、`NpcUi.cs`／`NpcServices.cs`、`ObjectRendering.cs`、`ClientEffect6016.cs`、`PacketBoxScreen.cs`、`RingWindows.cs`、`BuddyUi.cs`、`Fishing.cs` | 中（可再拆成兩張） |
| CU-3 原生讓位收尾 | G12、G13、G14、G15 | `LocationShare.cs`、`BoardUi.cs`、`NpcUi.cs` | 中 |
| CU-4 故障備援一致 | G16、G17、G18、G19 | `SummonFallbackUi.cs`、`CookBookFallbackUi.cs`、`ClanWarehouseHistoryUi.cs`、`NativeUi/UiClientBridge.cs`、`CharacterManagementUi.cs`、`MailUi.cs` | 低 |

CU-1 與 CU-3 都會改 `NpcUi.cs`／`DevToolsForm.cs` 一帶，建議依序做，避免衝突。各組都不需要改 `Program.cs`、註冊表或 lock 檔。§2.1 標「不補」的項目，交給下一輪 gap-matrix 註記「classic 不提供」。

## 5. 未驗證

- G1、G7、G12、G15 都是從程式碼推出來的，需要真機確認（smoke-m7 §10）。
- AuctionInfo 的 Bid 在 classic HTML 頁上有沒有對應連結（❓）沒有查到。
- Action_Window 的隊長踢人、選對象送出在 classic 是否完全沒有替代，沒有逐項確認。
