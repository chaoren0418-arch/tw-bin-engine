# 原版表現查證索引（第四層證據）

使用者 #427 規定：任何「原生表現」（畫面、動作、版面、音效、時序）都要先有本目錄的查證，才能開 code 任務。查證方法照 #417：用 IDA 7.7 與 P01 的 IDB，靜態分析未加殼的 `TW1608262200.bin`，地址標 VA／File Offset。

| 主題 | 文件 | 查證任務 | 涵蓋功能 | 狀態 |
|---|---|---|---|---|
| 逆向資源盤點；B03／B04／B07（list.spr parser、frame 結構、音效選擇、分類讀取者、dispatch tables） | [`rea-inventory.md`](rea-inventory.md) | #417 | B03、B04、B07、T165、T166 | ✅ 已合併 |
| 封包格式（伺服器端） | [`server-protocol.md`](server-protocol.md)、[`server-rules.md`](server-rules.md) | #132 | 全部 | ✅ 已合併 |
| 伺服器原始碼與 d287 JAR 對照 | [`source-vs-jar.md`](source-vs-jar.md) | T123（#226） | 全部 | ✅ 已合併 |
| 動作、表情與名稱繪製（表情 66–69 與外型重映射、名稱／稱號／盟徽位置與顏色、正義值色與粉紅名、決鬥／敵對血盟、變身姿勢與缺圖退回；引擎差異 19 項） | [`actions-and-names.md`](actions-and-names.md) | T160（#428） | T128、T129、T132、T135、T137、T146、T152 | ✅ 已合併 |
| UI 版面與圖示（UI XML 解密、800×600 主介面、聊天 Printer／調色盤、小地圖、隊伍、書籤） | [`ui-layout.md`](ui-layout.md) | T161（#429） | T106、T108、T124、T125、T130、T131、T138、T150、T152 | ✅ 已查（HP/MP 浮動窗與頭上條已由 V1 #456 補查，見第 2 節；舊封包狀態圖示見 V3 #458 第 4 節；紅字位置／字色／時長／行數見 V2 #457 第 2 節） |
| 光源、天氣與地圖事件（日夜曲線、天氣粒子、光源圖、中毒／麻痺／盲目／酒醉／隱身外觀、`^` 循環音規則；T167 補齊原圖來源與 XML 解密，修正 IDA 負 ID 誤判） | [`ambience-and-map-events.md`](ambience-and-map-events.md) | T162（#430）、T167（#437） | T147、T148、T149、T153、T166、T164／G2 | ✅ T162 已合併；T167 已靜態核驗（第 3.1 節，待 PR 合併與產品整合） |
| 音效播放策略（`<`／`[`／`^` 路徑、距離衰減、`^` 循環與 5 槽、200ms 冷卻、S22 模式、背景音樂／戰鬥音樂／環境音；引擎差異 14 項） | [`sound.md`](sound.md) | T163（#431） | T105、T047 系列、T165、T166 | ✅ 已合併 |
| 技能與法術表現（送 C6 不在本地播動作、冷卻由動作 18／19 算出；S30／S42 特效在施法者 `!` 格啟動、受擊在特效 `!` 格；list.spr 109 kind 0–10 的飛行與落點；S42 只有第一目標受擊的原版缺陷；箭矢 66／2349／2989；引擎差異 14 項） | [`skills-visual.md`](skills-visual.md) | T180（#482） | T100、T101、T113–T119 | ✅ 已查（PR 待合併） |
| 世界物件類功能（物件點擊共通流程與自動走近、拾取數量／節流／轉向、門、幽靈本機限制與 `exitghost`、座標分享格式與地區 id、死亡視窗 `UserPlayStatsUI`、復活確認走聊天輸入列；TW16 送出端名稱表；引擎差異 13 項不一致、5 項部分） | [`world-objects.md`](world-objects.md) | T182（#484） | T103、T136、T143、T144、T151 | ✅ 已查（PR 待合併；字串表文字、幽靈繪製效果、S241 對應仍待驗證） |
| 視窗類功能（兩套視窗系統、34 個缺檔 XML、個人商店／好友／封鎖／召喚／新版能力窗 XML、`MESSAGE_WINDOW`／`HYPER_TEXT_WINDOW`／`PET_QUICK_WINDOW`、公告板／修理／求婚／重置的原版視窗；對應 `native-ui.md` 模組） | [`windows.md`](windows.md) | G10b T181（#483） | T102、T107、T126、T127、T130、T131、T133、T134、T139–T142 | ✅ 已查（T131、T133 仍 ❓；多個 XML 本機缺檔，版面待資源） |
| 已完成 M6 功能的原版對照（50 個已合併功能：39 個有差異、9 個純協定、2 個未查證；修正分組 G1–G12） | [`m6-native-diff.md`](m6-native-diff.md) | T164（#432） | T100–T153 | ✅ 已完成（PR 待合併） |
| 原版功能對照引擎的差距盤點（gap-matrix：三組收包 dispatch 的全部 case、139 個送包名稱、80 個 XML 視窗與舊式視窗類別、物件／技能／地圖事件處理函式；T225 回填後 289 列：已有 58、部分 165、缺 66；M7 code 任務分組 M7-A–X 共 22 組；需先查證清單） | [`gap-matrix.md`](gap-matrix.md) | T202（#504）、T244（#601） | M7 全部 | ✅ 已完成；T244 第二輪（第 11 節，main `6d07fe0d`）：290 列，已有 67、部分 175、缺 48；下一批 code 分組 R2-A–Q 共 17 組（含 T245 的 PBW-A／B／D／E）；需先查證 Q-1–Q-4，建議 3 張 resource 任務 |
| 協定空白（S_EXTENDED 是 protobuf 通道、S250 的對應是 S_EVENT op 108 與 62 個子碼對照；S_MESSAGE_CODE 取 `Text.pak` 的 `string-c.tbl`；3.81 不要求 C14／S139；S_BLINK／S_HIT_RATIO／S_SHOW_MAP／S_CLONE／S_EVENT／S_NOTICE；13 個未知送包的送出點與欄位；S87 同身份入口 `A51920`） | [`protocol-gaps.md`](protocol-gaps.md) | T225（#532） | M7（gap-matrix 第 8 節六列） | ✅ 已查（PR 待合併；C_ALT_ATTACK／C_BOOK／C_SAVE 送出點仍查不到） |
| 視窗世代與待查視窗（TW16 背包／魔法／倉庫走 XML 版、3.81 世代用 `uimagicitem.xml`／`uiextra86.xml` 舊式設定；血盟窗讀 `RePledgeUI.xml`、S250 167–171；變身走 S39 `monlist` HTML；城堡 `tax`／`withdraw`／`deposit` 頁與王冠；盟屋地圖窗；S136 走 HTML 數字框、`InputWindow` 是聊天列 YES／NO；料理 52、血盟倉庫紀錄 117；信件／釣魚／重置 XML 在本機其他客戶端找到） | [`windows-2.md`](windows-2.md) | T226（#533） | M7-C、D、E、J、R；gap-matrix 第 8 節七列 | ✅ 已查（PR 待合併；王冠圖源仍查不到；`QuestUI` 的 S250 格式、`QuitWindow` 使用點已由 T245 [`packetbox-windows.md`](packetbox-windows.md) 補查） |
| 地圖特效光源、`]` 子特效、水中呼吸、座標特效與登入流程 XML（`A4E5D0` 第 7 參數＝光源半徑 k、3442–3446＝`dot range` 隱形光源點 k=5–13、六成地圖特效是光源；`]` 建立預設 Effect、`{`／`}` 淡入淡出；S_BREATH 只加圖示 effect 11；S_EFFECT_LOC 一律預設 Effect、2028–2056 光源 1；登入 XML 只找到他版本 `CreateUI.xml`） | [`effects-2.md`](effects-2.md) | T227（#534） | gap-matrix §8 四列；T105、T153 | ✅ 已查（PR 待合併；可開 FX1、FX2） |
| S250 視窗類子碼（B3：37、45、52、66、83、84、86、117、146、168、169、173、190、192 逐一查原版行為與 3.81 呼叫點；真正開窗只有 52／117／146；83 震動／煙火、84 畫面中央訊息；190／192 是伺服器自訂格式）；`QuestUI` 走 protobuf 不走 S250（3.81 `S_ClassicQuest` 是 255 自訂擴充）；`QuitWindow`＝死亡逾時後的重新開始／離開窗（`QUIT_WINDOW`）；`CNoticeWindow`＝拍賣通知圖示（不是登入公告） | [`packetbox-windows.md`](packetbox-windows.md) | T245（#602） | PG§2.3 B3；WIN2§6–7、EF2§5 待查三項；可開 PBW-A、PBW-B | ✅ 已查（PR 待合併；`QuitWindow` 的 ESC 入口、192 讀取者仍待驗證） |
| 圖源與字串（Q-2：城主王冠 `crown.raw`＝`Sprite.pak` 內 12 格 16×12 RGB555、位置與共用走路內插表；盟屋地圖 surf 表 `dword_135DA34`（`aAvadentelui` 是誤命名）與 350 筆標記位置；死亡統計值是客戶端以基準快照相減、殺怪數只來自 S250／61（3.81 不送）、地監時間走 S250／159；1784／1785／2139／3123–3127／3131 在 `string-c.tbl`，**mapDesc 在 `MapDesc-c.tbl`**） | [`sprites-and-strings.md`](sprites-and-strings.md) | T267（#628） | R2-D、R2-I、R2-M；#550（T232） | ✅ 已查（PR 待合併；`sub_A42C30`、674 鈕、新物品清單計入規則、TW16 op 10／9 待驗證） |

各查證任務合併時，把自己那一列的「狀態」改成 ✅，並補上文件連結。
