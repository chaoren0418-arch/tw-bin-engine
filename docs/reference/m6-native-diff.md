# 已完成 M6 功能的原版對照修正清單（T164／#432）

整理日期：2026-11-07（Asia/Taipei）。依據使用者 #427：任何原版表現（畫面、動作、版面、音效、時序）都要先有 `docs/reference/` 的查證，才能開 code 任務。本文件把 M6 的 T100–T153 逐項對到這些查證，作為修正任務的依據。

**範圍與方法**
- 引擎基準：main `c751a86b`。查證文件各自以較早的 main 比對（T160：`3b717590`；T161、T163：`9e6d7e8f`）。本輪抽查過文件中的引擎事實，在 `c751a86b` 仍然成立（行號可能有偏移），只有兩處改變：
  - T165（#433）已合併，修好音效差異 1、3，以及 4 的戰鬥路徑。
  - T146（#424）已合併。T160 寫的「未實作」改成實際比對（見 T146 列）。
- 只讀文件與原始碼。沒有改產品程式，沒有啟動遊戲、伺服器或 DB，沒有複製遊戲資源。
- 「未查證」的功能列出還要查什麼。可用線索之一是 #448 在 `scratch/task448-uixml/` 解出的 65 個原版 UI XML，用 `docs/reference/rea-scripts/ui_xml_decrypt.py` 可以重產。本文件只引用檔名與視窗名，**沒有把 XML 放進 git**。

**引用代號**

| 代號 | 文件 | 位置 |
|---|---|---|
| AN#n | [`actions-and-names.md`](actions-and-names.md)（T160） | 第 6 節第 n 列 |
| UI#n | [`ui-layout.md`](ui-layout.md)（T161） | 第 7 節第 n 列 |
| AM#n | [`ambience-and-map-events.md`](ambience-and-map-events.md)（T162） | 第 7 節第 n 列 |
| SD#n | [`sound.md`](sound.md)（T163） | 第 5 節第 n 列 |
| REA | [`rea-inventory.md`](rea-inventory.md)（#417） | — |
| WIN§n | [`windows.md`](windows.md)（T181／#483） | 第 n 節 |
| Rnn | [`server-rules.md`](server-rules.md)（#132） | 伺服器規則 |
| SP | [`server-protocol.md`](server-protocol.md)（#132） | 封包欄位 |

R／SP 是**伺服器原始碼**的查證，只能證明封包與規則，不能證明原版客戶端的畫面。

**「一致？」欄的記號**

| 記號 | 意義 |
|---|---|
| ✅ | 已查證，一致 |
| ⚠️ | 已查證，部分一致 |
| ❌ | 已查證，不一致 |
| ❓ | 原版表現未查證 |
| 協定 | 純協定或規則，沒有可比的畫面；以 R／SP 為準，#132 已對齊 |
| ⏸ | 尚未合併進 main，不算已完成的 M6，只附上查證結果 |

**全域差異（不逐列重複）：**
- UI#1：畫面尺寸不是 800×600。
- UI#2：所有視窗都是 WinForms／GDI 合成，沒有載入原版 `.img`。

凡是有視窗的功能都受這兩項影響，修正已由 #448 的 `docs/design/native-ui.md` 與 `docs/plans/issue-448.json` 排定（C1–C3 框架與各 UI 模組）。下表「一致？」欄只評該功能**自己的**行為差異。

## 1. 逐項對照

| T 編號 | 功能 | 原版來源 | 引擎現況 | 一致？ | 修正建議（檔案、範圍） |
|---|---|---|---|---|---|
| T100 | 技能施放通用化 | 未查證。R17、SP〈C6 技能的條件尾段〉只證明封包尾段 | `SkillCast.cs`、`MagicTargetPreparation.cs`：依技能分送 C6 尾段；施法動作與特效走 `MagicRendering.cs` | 協定一致；表現 ❓ | 待查：TW16 送 C6 前的本地施法動作（動作編號、是否等回包）、目標游標、施法中可否移動。找 C6 的送出函式，往上追 UI 呼叫端 |
| T101 | S42／S30／箭矢回包分派 | R20（伺服器）；影格音效見 REA B07、SD#1、SD#3 | `AttackSkillPackets.cs`、`MagicRendering.cs`、`ProjectileRendering.cs`；影格聲音已在 T165 改為 `<`／`[` 同路徑 | 協定一致；影格音效 ✅（T165 後）；畫面 ❓ | 待查：S42 多目標時特效與受擊的時序、箭矢 gfx 的飛行速度與落點。找 TW16 收包表中 S30／S42 對應的 case（REA 第一組 dispatch table） |
| T102 | NPC 出售與私商回購 | R27（伺服器）；WIN§1.1、WIN§2：NPC 商店與私商購買用 `CNpcShop` XML 窗（本機缺檔），3.81 世代是 `TRADE_WINDOW`；開店設定是 `PersonalShopUI`（出售與收購各 8 格一頁、訊息 2×20 字、3 種外型） | `NpcUi.cs`、`PrivateShopUi.cs`：WinForms 清單與數量框；沒有開店設定窗 | 協定一致；表現 ⚠️ | 新增原生模組 `ui-shop`（`TRADE_WINDOW`，含修理模式 6）與 `ui-personal-shop`（`PersonalShopUI`）。`native-ui.md` §0 目前列為不涵蓋，要先補設計 |
| T103 | 地面物品通用化與三種停用物品的地面圖 | 地面圖：#48／#51 的 sprite 契約；拾取點擊：REA B07（`F112F0` → `5001C0`，距離 1、節流 986 ms／加速 600 ms，**欄位屬推測**） | `Ground*.cs`、`PickupPresentation.cs`：地面圖依釘選 sprite；拾取送 C11 類請求（`Ground.cs`） | 地面圖 ✅（已有 sprite 契約）；拾取時序 ❓ | 待查：把 REA B07 的 opcode 23 欄位對到伺服器拾取格式（REA 建議任務第 6 項），再比較引擎的距離與節流 |
| T104 | 緩速時的道具、地面與交談政策 | R08、R22–R24（伺服器）；`NormalHaste.cs` 註明 JAR 0.755／原版 0.75 | 依 JAR 係數計算動作間隔 | 協定 | 不需修正。0.75／0.755 的差異已在程式註解說明 |
| T105 | S22 音效、S106 座標特效、S55 變體 | SD§3、SD#7、SD#8；S106／S55 畫面未查證 | `SoundPackets.cs`：把第一個 byte 當重複次數，播 `repeat+1` 次，延遲 100 ms；只認 33 個釘選 `.wav` | ⚠️ | `SoundPackets.cs`：第一個 byte 改成模式（0 單次、1 環境循環、2 停止）；同 id 還在播時不重播。`AudioAssets.cs`：查找順序改為先 `.mp3` 再 `.wav`。見 G8 |
| T106 | 多頻道聊天 | UI§3、UI#7–UI#10 | `PlayerChat.cs`、`PlayerHudChat.cs`：`[一般]` 等文字前綴、.NET 具名色、6 種頻道、不處理 `$` | ❌ | 格式與顏色：#448 的 `ui-chat-format`（`PlayerChat.cs`、`PlayerHudChat.cs`）。視窗：`ui-chat-window`。前綴對應頻道仍屬推測（UI§3），要先查 `sub_6C6C60` 之後的呼叫鏈 |
| T107 | 角色建立、刪除、同連線切角 | R03–R06（伺服器）；原版選角／建角畫面未查證 | `CharacterManagementUi.cs`：WinForms 440×590 對話框；切角是 HUD 按鈕 | 協定一致；表現 ⚠️（WIN§2：原版是 XML 全畫面 `CSelectUI`／`CCreateUI`，但 `SelectUI.xml`／`CreateUI.xml` 本機缺檔） | 不寫死在 BIN，推測已推翻。要先取得兩個 XML 或 3.81 實機截圖，再開「登入／選角畫面」設計（`native-ui.md` §0） |
| T108 | buff／狀態圖示 | UI§4、UI#11 | `StatusIcons.cs`、`MagicStatus.cs`：合成菱形／三角形加文字，依 skill id 排序 | ❌（圖示來源待查） | 先做 #448 的 `ui-verify-buff-icons`（V3：`SpellIcon.idx`），再做 `ui-buff-icons` |
| T109 | 通用道具 use_type 尾段 | R24、SP〈C164 物品 use_type 的條件尾段〉 | `ItemUseArguments.cs` | 協定 | 不需修正 |
| T110 | 傳送 recovery wrapper | R33–R35 | `Teleport.cs` | 協定 | 不需修正 |
| T111 | 登入密碼字元規則 | R03 | `LoginProtocol.cs` | 協定 | 不需修正。原版輸入框是否擋字元未查證，影響很小 |
| T113–T119 | 七職業基本技能 | R17–R19（伺服器）；各技能的施法動作、特效 gfx、音效未逐一查證 | `MagicSpells.cs`、`SkillCast.cs`、`MagicRendering.cs`：依伺服器回包的 gfx 與 action 播放 | 協定一致；表現 ❓ | 與 T100 一起查：TW16 是否在本地補施法動作，還是只靠 S30／S42／S55 回包。若只靠回包，引擎已一致 |
| T120 | S242 sub2 受擊反應抑制 | R20 | `HitReactionSuppress.cs` | 協定 | 不需修正 |
| T121 | 物品 status blob、權威裝備槽位 | R25、R26 | `ItemStatus.cs`、`Equipment.cs` | 協定 | 不需修正。說明文字的版面由 T152 處理 |
| T122 | S206 單參數 overload | SP | `MapIdPacket.cs` | 協定 | 不需修正 |
| T123 | 原始碼與 d287 JAR 封包類別對照 | [`source-vs-jar.md`](source-vs-jar.md) | 文件任務 | 協定 | 不需修正 |
| T124 ⏸ | 記憶座標（書籤） | UI§6、UI#14：`BookmarkUI.xml` 263×420 | 未合併；main 只有傳送卷軸的書籤 ID 欄位（`InventoryUi.cs`） | ⏸／❌ | 合併後由 #448 的 `ui-bookmark` 處理版面 |
| T125 | 組隊與隊員 HP 條 | UI§6、UI#13 | `PartyUi.cs`：抽屜內清單，標準 ProgressBar 120×22 | ❌ | #448 的 `ui-party`：`PARTY_INTERFACE` 135×107，量表 60×10、58×8 |
| T126 | 好友與封鎖名單 | WIN§1.2：三個獨立 XML 窗（`FriendUI`／`ExcludeUI`／`MailExcludeUI`），共用版面：14 列＋捲軸＋人數標籤，每列右側有 `Info` 欄 | `Buddies.cs`、`BuddyUi.cs`：單一分頁內兩個 ListBox；沒有信件封鎖 | ⚠️ | 新增原生模組 `ui-friend`（三窗共用一個類別）。名單上限與 `Info` 欄內容待驗證 |
| T127 | 寵物與召喚物控制 | UI§6 的 `PARTY_PET_RT_WINDOW`；WIN§1.3、WIN§1.5：寵物指令窗是舊式 `PetControl`；`PET_QUICK_WINDOW` 有 5 個鈕（攻擊、防禦、散開、警戒、休息），但類別沒有建立（推測）；召喚選擇是 `CSummonUI`（快速 3 格、12 個等級、魅力標籤） | `Pets.cs`、`PetUi.cs`：分頁內 6 個指令鈕；沒有召喚選擇窗 | ⚠️ | 寵物分頁照舊併入 `ui-party`；另新增 `ui-summon`（`SummonUI.xml`＋`summonList.xml`） |
| T128 | 血盟建立、加入、退出、盟徽 | AN§2、AN#9–AN#11；原版有 `PledgeUI.xml`、`PledgeFocusUI.xml` | `Clan.cs`、`ClanUi.cs`：世界畫面畫「名稱 [血盟]」淺青色，盟徽在左方 19 px；盟徽只存記憶體 ≤256 筆，要在 UI 手動查詢 | ❌ | 見 G1（不畫血盟名，盟徽改到名稱右側／基準 −20）與 G4（缺圖自動要求、≤32 待回覆） |
| T129 | 變身 S_ChangeShape | AN§3、AN#16–AN#18 | `Polymorph.cs`、`PlayerProfiles.cs`：外型沒有對應動作時顯示「尚未支援此外觀」，並停止移動與戰鬥；6080／6094 沒有特例 | ❌（#16 推測一致） | 見 G3：依 AN§3.4 逐級退回，不擋操作；補 AN§3.5 的特殊外型 |
| T130 | 公告板 | WIN§2：**專用舊式窗** `BoardListWindow`／`BoardReadWindow`／`BoardWriteWindow`（不是 `HYPER_TEXT_WINDOW`）；按鈕 surf 658–668、672–678 與座標寫死；一頁 8 筆 | `BoardUi.cs`：WinForms 740×680，一頁 8 筆 | ⚠️（分頁一致，視窗不一致） | 新增原生模組 `ui-board`（3 窗），surf 與座標依 WIN§2 |
| T131 | 信件 | UI§6、UI#16；WIN§2：`RenewalMailUI` 由 `S_MAIL_INFO` 填入，XML **本機缺檔**；`sub_B3D390` 缺檔時只跳 `Cannot Found %s`，**沒有退回** | `MailUi.cs`：780×640 對話框 | ❓（資源缺） | 拿不到 `RenewalMailUI.xml` 或 3.81 實機截圖前不改 |
| T132 | 決鬥 | AN§2.6、AN#14 | `DuelUi.cs`：另畫橘紅色「決鬥對手 · 名稱」 | ❌（引擎多畫） | 見 G1：拿掉額外標籤，名稱不變色；保留可直接攻擊與游標的語義 |
| T133 | 刪除與給予物品 | R22（伺服器）；WIN§2.1：`MESSAGE_WINDOW` 只有 OK 鈕，**不是**確認框；伺服器 Yes/No 走 `sub_4FC710`→`sub_AC9560` | `ItemTransfer.cs` | 協定一致；表現 ❓ | 還要查：刪除時的本地確認（背包窗的垃圾桶流程），以及給予的操作方式 |
| T134 | 武器修理與 PK 查詢 | R27（伺服器）；WIN§2：可修清單用 `TradeWindow` 模式 6（`TRADE_WINDOW`），清單為空時開 NPC 對話頁 `touma3`／`gayle5`；PK 查詢是 HTML 動作 `HyperActCheckPK`；**沒有專用視窗** | `WeaponRepair.cs`、`WeaponRepairUi.cs`：獨立 WinForms 580×420，加 HUD 按鈕 | ❌（引擎多出專用窗與 HUD 鈕） | 併入 `ui-shop` 的模式 6；PK 查詢改走 NPC 對話。移除專用窗與 HUD 鈕，要等原生窗完成 |
| T135 ⏸ | 盟徽查詢與 ID 對應 | AN§2.8、AN#11 | 未合併 | ⏸／❌ | 合併後依 G4 調整：自動送 opcode 28、`Emblem%d.dat` 快取 ≤512 筆 |
| T136 | 門 | 未查證 | `Doors.cs`：依 S87／開關封包切換門的動作 | ❓ | 待查：門 sprite 的開關動作編號與點擊判定（REA 的 type 讀取者表沒有門的類別） |
| T137 | 稱號與表情動作 | AN§1、AN§2、AN#1–AN#7、AN#19 | `TitleAndEmote.cs`：所有物件都畫稱號，位置在腳下 −70，LightYellow、JhengHei 10 px、無外框；表情順序 66／67／68／69；不依外型重映射；缺圖就結束 | ❌ | 見 G1（稱號）與 G2（表情） |
| T138 | 角色設定存取（快捷列） | UI#5 | `CharacterConfig.cs`、`PlayerHudView.cs`：F1–F4 共 4 格，26 byte「TWCC」 | ❌ | #448 的 `ui-verify-bar-behavior`（V5）→ `ui-macro-bar`（F5–F12 共 8 格、3 分頁） |
| T139 | 釣魚 | WIN§2：XML 窗 `CFishingUI`（事件 `FishingNet`／`QuitFishing`）＋可釣格提示 `CanFishingTileAreaDisplay`；`FishingUI.xml` 本機缺檔 | `Fishing.cs`：`FishingHud` 按鈕 | ⚠️（版面因資源缺而無法比對） | 新增原生模組 `ui-fishing`，等資源。可釣格提示的繪製規則待查 |
| T140 | 查詢玩家與排名 | WIN§2：指令 `Commandquery`／`Commandrank` 與 HTML 動作 `HyperActWho`／`HyperActRank`；結果是 `S_HYPERTEXT`，在 NPC 對話窗（`HYPER_TEXT_WINDOW`）顯示 | `PlayerQuery.cs`：全部轉成聊天行「[查詢] …」 | ⚠️ | HTML 結果改在原生 NPC 對話窗（新增 `ui-hypertext`）顯示；聊天行只留伺服器原本就送到聊天的訊息 |
| T141 | 求婚與結婚 | WIN§2、WIN§2.1：msg 654 走 `S_ASK` default → 通用 Yes/No 框 `sub_AC9560`，回覆用 TW16 opcode 33；**沒有專用視窗** | `Marriage.cs`：HUD 按鈕＋專用 Form 540×240，收到 S219/654 時自動開 | ❌ | 新增通用原生 `ui-yesno`；求婚、交易、組隊、決鬥、血盟邀請都改走它。視覺（`sub_AC9410` 以下）待查 |
| T142 | 能力值重置 | WIN§1.4、WIN§2：重置介面是 `CResetStatUI`（`ResetStatUI.xml`，本機缺檔），由 TW16 opcode 43 子類型 1–3 開啟，和 S64 子類型對得上。**`ReNStatusWinUI-c.xml` 不是重置介面**，而且 TW16 不讀它 | `CharReset.cs`、`CharResetUi.cs`：WinForms 540×490 | ⚠️（觸發一致；版面因資源缺而無法比對） | 等 `ResetStatUI.xml` 或實機截圖。`ReNStatusWinUI-c.xml` 改歸角色狀態窗設計 |
| T143 | 幽靈模式 | 未查證 | `GhostMode.cs`：刻意不猜 gfx 與轉場（程式註解） | ❓ | 待查：幽靈外觀與退出提示 |
| T144 | 發送座標 | 未查證 | `LocationShare.cs`：自訂顯示 | ❓ | 待查：原版在聊天中的座標格式與點擊行為（聊天 Printer 的類型清單沒有這項） |
| T145 | 傳送門（踩點進入） | R36 | `Portals.cs` | 協定 | 不需修正 |
| T146 | 血盟戰爭 | AN§2.7、AN#15：名稱不變色；可直接攻擊；戰爭模式在頭上畫盟徽（置中、`T−23`） | `ClanWar.cs`：在敵對成員腳下 −48 畫橘紅色「名稱 [血盟]」，有暗底框；沒有頭上盟徽 | ❌ | 見 G1：拿掉橘紅標籤，改成頭上盟徽 |
| T147 | 日夜、天氣與光源 | AM§1–AM§3、AM#1–AM#10 | `Ambience.cs`：本地以 6 倍速推進時鐘，05–06／18–19 漸變，深藍覆蓋 `0x081020`；粒子數 `面積/7000×強度`；先畫明暗再畫天氣；光源原值存入，畫成圓形 | ❌（AM#4 ✅、AM#10 ⚠️） | 見 G5（日夜與光源）、G6（天氣） |
| T148 | 中毒、麻痺、詛咒盲目 | AM§4、AM#11–AM#16 | `Afflictions.cs`：綠以亮度重算、無脈動；麻痺是灰階；灰與綠同時就丟例外；盲目 2 只把色頻除以 4 | ❌（AM#14、AM#15 ⚠️） | 見 G7 |
| T149 | 隱身 | AM§4(d)、AM#19–AM#20 | `Invisibility.cs`：自己約 50% ✅；別人也半透明畫出 | ⚠️ | 見 G7：別人隱身預設不畫，只有 `$431`（或隊伍／血盟）條件成立時才半透明。`$431` 語義待查（AM§8 第 4 項） |
| T150 | 小地圖 | UI§5、UI#12 | `Minimap*.cs`：右上 272×218 地圖快取縮圖，F5 開關 | ❌ | #448 的 `ui-minimap`（讓出 F5、改用 Map 按鈕、png 地圖、8 方向箭頭） |
| T151 ⏸ | 復活確認與移動鎖定 | 未查證（R21 只有伺服器端） | 未合併；main 的 `Recovery.cs` 只有 S85 基本處理 | ⏸／❓ | 合併前先查：原版復活確認框（推測是 `MESSAGE_WINDOW`）與鎖定期間的游標 |
| T152 | 鑑定說明、紅字訊息、名稱狀態 | 紅字：UI#17（位置待查）；名稱色：AN§2.4、AN§2.5、AN#8、AN#12、AN#13 | `DisplayNotices.cs`：紅字黑底在 1/3 高、5 秒；粉紅名另畫 HotPink 文字（頭上 −18），127 當 127 秒；沒有正義值名稱色，沒有一般名稱 | ❌（紅字 ❓） | 名稱：見 G1（正義值色、粉紅名改成 `0x767D` 名稱色、127 表示永久）。紅字：#448 的 V2 → `ui-red-notice` |
| T153 | 暫時能力、酒醉、水中呼吸、刪除技能 | AM§4(c)、AM#17–AM#18 | `TimedBuffs.cs`：酒醉時整個畫面平移（x 2×、y 1×）；型 8 不當酒醉 ✅ | ⚠️ | 見 G7：改成逐掃描線水平正弦（振幅 3.5×醉度、週期 100 行、5 秒一輪），地圖 67／420 至少 1。水中呼吸、刪除技能的表現未查證 |

## 2. 統計

以**編號數**計。T100–T153 共 54 個編號，扣掉 T112（smoke）與未合併的 T124、T135、T151，已合併的 M6 功能是 **50 個**。

| 結果 | 數量 | 功能 |
|---|---:|---|
| 已查證，有差異（❌／⚠️） | **26** | T102、T105、T106、T107、T108、T125、T126、T127、T128、T129、T130、T132、T134、T137、T138、T139、T140、T141、T142、T146、T147、T148、T149、T150、T152、T153 |
| 已查證，一致 | 1 | T103（地面圖；拾取時序仍 ❓） |
| 純協定，#132 已對齊 | 9 | T104、T109、T110、T111、T120、T121、T122、T123、T145 |
| 原版表現未查證 | 14 | T100、T101、T113–T119（7 個）、T131、T133、T136、T143、T144 |
| 合計 | 50 | |

未合併的 3 個裡，T124、T135 已知有差異（書籤版面、盟徽取得），T151 未查證。

26 個有差異的功能怎麼分：
- **5 個只有 UI 版面差異**，已由 #448 排定修正：T106、T108、T125、T138、T150。
- **11 個需要新開 G1–G8**：T105、T128、T129、T132、T137、T146、T147、T148、T149、T152、T153。其中 T152 的紅字部分也歸 #448。
- **10 個由 G10b（#483，[`windows.md`](windows.md)）補查後確認有差異**：T102、T107、T126、T127、T130、T134、T139、T140、T141、T142。這 10 個都屬於 `native-ui.md` §0「不涵蓋」的視窗，修正方式是新增原生視窗模組，見下表 G11。其中 T107、T139、T142 的原版 XML 本機缺檔，要先補資源。

## 3. 建議的修正任務分組

依差異大小排列。每組一行：目標、檔案、驗收。UI 版面不另開任務，已在 #448 計畫中（最後一列）。

| 組 | 目標 | 檔案 | 驗收 |
|---|---|---|---|
| G1 名稱、稱號與頭上盟徽（大） | 依 AN§2：只畫滑鼠指到的物件（或全名選項）；名稱在外框上緣置中，稱號在上一行；名稱色依正義值公式，粉紅名改名稱色 `0x767D`、127 表示永久；1 px 黑外框、MingLiU 12 px；拿掉多畫的血盟名、決鬥對手、敵對血盟橘紅標籤；同盟／戰爭時畫頭上盟徽 | `ObjectRendering.cs`、`TitleAndEmote.cs`、`DisplayNotices.cs`、`ClanUi.cs`、`DuelUi.cs`、`ClanWar.cs` | 離線測試：正義值→顏色向量表、粉紅名 0／127／一般值、位置算式（`T−3`、`T+10`、`T−23`）。T112 截圖：指到與沒指到各一張 |
| G2 表情動作（中） | 依 AN§1：選單順序改為 68／69／67／66；依外型 G1–G4、10413 重映射；職業外型立即本地播放、一般變身 2500 ms 節流；送出條件放寬到原版；缺圖逐級退回，不丟例外 | `TitleAndEmote.cs` | 離線測試：外型×表情的重映射表、節流時序、缺圖退回鏈。T112 用一個職業外型、一個變身外型各做一次表情 |
| G3 變身缺圖退回與特殊外型（中） | 依 AN§3.4／§3.5：外型沒有對應動作時逐級退回（姿勢 → 0），不擋移動與戰鬥；6080／6094 加 6099 特效並切換姿勢；欄位 108／113 規則 | `Polymorph.cs`、`PlayerProfiles.cs`、`ObjectRendering.cs` | 離線測試：缺圖外型的退回結果；移除「尚未支援此外觀」阻擋的回歸測試。T112：變身成缺動作外型後還能移動 |
| G4 盟徽取得與快取（小；等 T135 合併） | 依 AN§2.8：缺圖自動送 opcode 28，最多 32 筆待回覆、不重送；快取上限 512。磁碟快取 `Emblem%d.dat` 要先裁定存放位置（規則 1） | `Clan.cs`、`ClanUi.cs`、T135 的檔案 | 離線測試：待回覆上限與不重送。磁碟快取另開裁定，未裁定前只做記憶體 |
| G5 日夜與光源（大） | 依 AM§1、AM§3：不在本地推進時鐘，只在 S_TIME／S_STATUS 與換圖時重算；曲線 06:00–06:40 變亮、20:00–20:40 變暗，等級 1–15；用每色頻係數表取代深藍覆蓋，含地圖覆寫、職業 +2、雨雪 −1、特效 +5；光源值換算（自己 <5→5 等）；`source.lmp` 16 張光源圖 | `Ambience.cs`、`AmbiencePainter`（同檔或相鄰檔） | 離線測試：時間→等級表、光源換算表、係數表的像素向量。`source.lmp` 內容是 AM§8 第 1 項的待查項，要先查證 |
| G6 天氣（中） | 依 AM§2：L≥10 時減 1；地圖 7701–7710、7771–7780 強制雨 2；gfx 10683／10632／10891 移除時清除；粒子數固定（雨 128／256／512、雪 48／96／144）；雨 4 層、雪的移動規則；先畫雨雪再一起變暗 | `Ambience.cs`、`AmbiencePainter` | 離線測試：解碼與附帶規則向量、粒子數表；繪製順序的像素測試（夜晚雨滴變暗） |
| G7 異常狀態外觀（中） | 依 AM§4：中毒只加 G 8–14、1.2 秒三角波；麻痺 `c/2+(3,3,3)`；灰綠同時以灰優先，不丟例外；綠 ≥8 整包忽略；盲目 1 留自己 k=5 光圈、盲目 2 接近全暗並讓特定物件發光；酒醉改逐掃描線正弦；別人隱身預設不畫 | `Afflictions.cs`、`TimedBuffs.cs`、`Invisibility.cs` | 離線測試：色彩公式的像素向量、封包邊界值不丟例外、酒醉位移表。`$431` 未解前，隱身以「隊伍／血盟可見、其他不畫」實作，並標「推測」 |
| G8 音效封包與播放規則（中；不含 `^`） | 依 SD#4–SD#9：S22 第一個 byte 改成模式 0／1／2，同 id 在播時不重播；200 ms 冷卻擴到所有音效出口（全域每 id 一份）；距離衰減 `max(0,100−14·(nx²+ny²)/20)%`；查找順序先 mp3 再 wav；音量分 4 類並讀寫 `lineage.cfg` 的對應鍵 | `SoundPackets.cs`、`AudioCore.cs`、`AudioAssets.cs`、`PlayerAudioSettings.cs`、`ClientAudioSettings.cs` | 離線測試：模式 0／1／2、冷卻、衰減表、查找順序。`lineage.cfg` 只讀寫專案內的設定副本，不碰正式客戶端 |
| G9 背景音樂與環境音（大；先查證） | 依 SD#10–SD#14：區域音樂、戰鬥音樂（15 秒淡回）、登入音樂、`envsound.def` 環境音、場景音 | 先開 resource：從資源包解出 `music.def`、`envsound.def`（只讀到 `scratch/`），再開 code | resource 驗收：兩個 def 的格式與本機內容摘要寫進 `sound.md`。code 驗收另訂 |
| G10 未查證功能的補查（resource，分 3 張） | (a) 技能與法術表現：T100、T101、T113–T119 的本地施法動作、S42 時序、箭矢 gfx；(b) 視窗類：T102、T107、T126、T127、T130、T131、T133、T134、T139–T142，解讀 #448 已解密的 XML（`PersonalShopUI`、`FriendUI`、`ExcludeUI`、`SummonUI`、`ReNStatusWinUI-c`、`HYPER_TEXT_WINDOW`、`MESSAGE_WINDOW`、`PET_QUICK_WINDOW`）；(c) 世界物件類：T103 拾取時序、T136 門、T143 幽靈、T144 座標格式、T151 復活確認 | 只產出 `docs/reference/*.md` | 每個功能都有「原版來源＋一致？」。有差異的，回填本文件第 1 節 |
| G11 原生視窗模組（design 先行；G10b 結果） | 依 WIN§2：通用 `ui-yesno`（T141 及所有 `S_ASK`）、`ui-hypertext`（NPC 對話，T140、T134 空清單）、`ui-shop`（`TRADE_WINDOW`，含修理模式 6：T102、T134）、`ui-personal-shop`（T102）、`ui-board`（T130）、`ui-friend`（T126）、`ui-summon`（T127）。T107、T131、T139、T142 等資源 | 先開 design，把 `native-ui.md` §0 的「不涵蓋」改成模組表，再開 code | design 驗收：每個模組列出 XML／surf、開窗封包、`IUiCommands` 動作。code 驗收另訂 |
| （已排定）UI 版面 | UI#1–UI#17：T106、T108、T124、T125、T138、T150、T152 紅字、T130／T131 視窗 | 見 `docs/design/native-ui.md`、`docs/plans/issue-448.json` | 依 #448 各任務的驗收；本文件不重複開任務 |

**建議順序：**
1. G1、G7、G8：差異已完全查證，可以直接開 code。
2. G2、G3、G6：已查證，範圍中等。
3. G5：缺 `source.lmp` 內容，可以先做時鐘與曲線，光源圖晚一點補。
4. G4：等 T135 合併。
5. G9、G10：先開 resource（G10b 已由 #483 完成）。
6. G11：先開 design。

T112（M6 smoke）依 #427，要等上述修正合併後才開。

## 4. 限制

- 全部是靜態對照，沒有實機觀察。原版行為以 TW1608262200 為準；LiTo 3.81 是否相同，各查證文件都標為待驗證（例如 AM§8 第 7 項）。
- 引擎現況引用查證文件的描述，本輪只抽查，沒有逐行重讀。G1–G9 開工前，執行者應以當下的 main 重新確認。
- 「協定」類只代表伺服器端已對齊（#132），不代表原版客戶端沒有任何畫面。若之後的查證找到相關畫面，再改成 ❓ 或 ❌。
