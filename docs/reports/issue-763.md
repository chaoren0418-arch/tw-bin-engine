# Issue #763 T325：P-D 原生能力值重置窗與能力資訊窗（參考客戶端 ResetStatUI.xml／StatInfoUI.xml）

依據：[`docs/reference/pak-resources.md`](../reference/pak-resources.md)（PAK）§4 第 1:43／4:ResetStatUI／4:StatInfoUI 列、§6 P-D。P-A（#757，`RefClientXmlSource`）與 P-B（#760，`RefClientSurfFallback`）已在 main。Png 圖沿用 #768 的 `OptionPngSource`。

## 做了什麼

- **新檔 `engine/source/NativeUi/ResetStatPanel.cs`**（`PanelId="ResetStatUI"`）
  - 註冊用 `[UiPanel(183)]` 自動發現，實作 `IUiClosablePanel`。宿主版面是 `uistatus86`，跟 Option／RenewalStatus 一樣。`Load` 內才讀 `ResetStatUI`：主 Tile.pak 沒有，就讀參考客戶端（預設 kor25），SHA-256 由 P-A 寫進 `ui-xml-ref` log。
  - surf 走 `SurfCatalog.Get`（先查本機，找不到再走 P-B 參考備援）。png 10933／10935 走 `OptionPngSource`。缺的圖不畫，列在 `Missing`，並寫一行 `reset-stat-ui 缺圖不畫` log。不用替代圖。
  - 圖層用 `UiLayer.Float`：`ResetStat_Win` 是 800×600 全畫面，又是 modal，所以不放進 4 層視窗堆疊，免得被擠掉。視窗開著時，所有滑鼠與按鍵都由它接收。
  - 新的介面 `IResetStatHost` 提供 `Snapshot`、`Send`、`Cancel` 三個動作。資料就是既有的 `CharResetView`。送出一律走既有的 `LoginConnection.SendCharReset`，stamp 與合法性檢查跟 WinForms 對話框相同。`CharReset.cs` 沒有改。
  - **版面照 XML**：
    - `ResetStat_Win` 的大小用宣告的 Width/Height，沒有就用 Back_Image 的大小。
    - `Message_Window` 的大小用 BaseSurf 圖，沒有就用 Back_Image。兩者都沒有時標 Unavailable，因為沒有確認框就不能安全送出。
    - 座標相對於父元素（Window／Control）。
    - 圖號接受 `id;旗標;…` 格式，旗標不使用。
  - **數值**：
    - `Level`、`HitPoint`、`ManaPoint`、`ArmorClass` 顯示 view 的值。
    - 6 個能力 Control 的 `Point` 顯示草稿值，對照是 Strength→STR、Intelligence→INT、Wisdom→WIS、Dexterity→DEX、Constitution→CON、Charisma→CHA（wire 順序）。
    - `Remain_Point` 顯示剩餘點數。
    - `Msg_Label`（MultiLabel，自動換行、垂直置中、LineMargin）顯示階段說明，或最近一次「未送出」的錯誤。
    - `Tip_Label` 的 String 4082 經 `TextResolver` 取字，產品中是 null，所以不畫。
    - 設計時的 Caption 一律不畫。
  - **操作**（規則與 WinForms `CharResetUi` 相同）：
    - initial／elixir 階段：Up／Down 改草稿，範圍是基礎值到 35，剩餘點數不可小於 0。
    - level 階段：
      - 未滿 50 級：`1Level_Button` 送 step 0。
      - 50 級以上：按某項能力的 Up 送 step＝該項 wire index＋1。WinForms 版是「選屬性＋提升 1 級」，這裡改成直接按 Up。
      - `10Level_Button` 在 level＋10 ≤ min(50, max) 時可按，送 step 7。
    - `OK_Button`：
      - 編輯階段剩 0 點，或 level 階段已到最高等級時，先開 `Message_Window` 確認，按 Ok 才送出 `initial`／`elixir`／`finish`。
      - 階段是 ended／idle 時，按 OK 直接關窗並交回遊戲焦點。
    - Esc：
      - 重置進行中：開 `Message_Window`，文字跟 WinForms 相同：「原服沒有取消重置封包。中斷連線並於重新登入後核對？」。按 Ok 會呼叫 `Cancel`（`LoginConnection.Cancel`）並關窗。
      - 重置已結束：直接關窗。
    - 不能按的按鈕照樣畫底圖，但不畫 Highlight 圖，點了也沒有作用。按住時畫 Push 圖，滑鼠移上時畫 Highlight 圖。
  - **開窗與關窗**：關窗條件跟 WinForms `Sync` 相同：登入 generation 改變，或 initial 之後世界 epoch 改變，就關窗。initial 階段會跟隨抵達地圖時的 epoch。
- **新檔 `engine/source/NativeUi/StatInfoPanel.cs`**（`PanelId="StatInfoUI"`）
  - 註冊用 `[UiPanel(182)]` 自動發現，實作 `IUiClosablePanel`、`IUiMovablePanel`，圖層是 Window，宿主版面 `uistatus86`。
  - 底圖是 BasePng 10911（`10911;2;28`），走 `OptionPngSource`。大小取底圖；底圖缺就標 Unavailable。`Moveable=340` 是拖曳區高度。
  - `List Vertical="1"` 的各列依 Height 由上往下排。
    - `*_Label` 的 String id 經 `TextResolver` 取字（產品中是 null）。
    - `*_Value` 經 `ValueProvider` 取值，產品中還沒有提供者，所以留白，Caption「0」不畫。
  - **開窗**：
    - 從 `RenewalStatusPanel`（#766）的 `StatusBonus_Button`（`ShowStatInfoTooltip` CheckButton）開。狀態窗開著且這個鈕被勾起時，資訊窗開在狀態窗右邊，並保持在 800×600 內。
    - 取消勾選或關掉狀態窗時，資訊窗跟著關。
    - Esc（`CloseStatInfoWindow`）可以關窗。
    - 離開世界時關窗。
  - 做法是在 `Update` 裡讀 `UiRoot.Find(RenewalStatusPanel.PanelId)`，所以不必改 `RenewalStatusPanel.cs`。
- **`engine/source/CharResetUi.cs`（只加原生分流）**：
  - `OfferCharReset` 先找可用的原生重置窗，條件是：`NativeUiActive`、已註冊、`Available`、沒有 Faulted。找到就用 `CharResetNativeHost(login)` 呼叫 `Begin` 開窗，不開 WinForms。
  - 找不到時照舊開 WinForms 對話框：`--classic-ui`、沒有參考客戶端、XML 壞、`"refClient":false` 都屬於這種情況。WinForms 類別本身沒有改。
- **新檔 `engine/source/NativeUi/ResetStatPanelTests.cs`**：`[WorldModuleTest("reset-stat-ui")]`、`[WorldModuleTest("stat-info-ui")]`。全部用合成資料：同結構的小版面、純色圖、假 host，不讀任何客戶端。涵蓋：
  - 自動發現與圖層。
  - 版面：大小、巢狀座標、multi 版跳過、Customizing_Win 跳過、Missing 與路徑、圖號旗標。
  - 像素：
    - 背景、16 alpha 黑板、標題。
    - 能力底圖在黑板之上，Up 鈕在能力底圖之上。
    - 進度 On/Off 與箭頭。
    - Msg 文字有畫、Tip 沒有畫。
    - Highlight 與 Push 圖。
    - 確認框蓋在主窗之上。
  - 配點：Up、Down、上限、剩餘點數、確認與取消、送出的 stamp、被拒時的錯誤、elixir。
  - 升級：1 級、10 級、50 級以上用 Up 選屬性、35 上限、finish、CanAct=false、ended 時關窗。
  - Esc：離開提示 → Cancel。
  - generation／epoch 改變時關窗。
  - modal 輸入。
  - 壞版面：根錯、缺窗、缺能力、圖號壞、缺 X、沒有大小、確認框沒有圖。
  - StatInfo：
    - 列堆疊、文字、拖曳、Esc、Toggle、Unavailable。
    - 跟著真的 `RenewalStatusPanel` CheckButton 開關，含右側定位與夾在畫面內。

> 任務說「測試用 `[CheckGroup]`」：`engine/source` 內的測試一律用 `[WorldModuleTest]` 自動發現，與 #757／#761／#766 相同；`[CheckGroup]` 只用在 `_integration/dev`。測試檔依任務指示用英文類名 `ResetStatPanelTests`（兩窗共用一檔）。

## 資源核對（PakViewer CLI，唯讀，輸出只在 worktree 的 `scratch/t763/`，沒有進 git）

- **XML**：從 kor25 `Tile.idx` 用 `pakviewer-cli pak extract` 抽出，再 `xml decrypt`。兩份的雜湊都與 PAK§4.1 相符。

  | 檔案 | 大小 | 原檔 SHA-256 | 解密後 SHA-256 |
  |---|---|---|---|
  | `ResetStatUI.xml` | 15,389 B | `473c7e16…9cf3f244` | `af94ac5257a287369c4c3eddb4c024e62e55e0963799c1b48f0a8e3764c6bbac` |
  | `StatInfoUI.xml` | 7,371 B | `710c4d24…108b09cb` | `6d5699480913221cbaec420678ab9f3df2ae463117cd02aeb4e7e4a6adcace99` |

- **查詢範圍**：用 `docs/reference/rea-scripts/pak/pak_index.ps1 -WithAlt -Out scratch\t763\pak-index` 列出以下清單，再用 `scratch/t763/find_images.py` 逐一比對 `<id>c.img`／`<id>.img`／`.tbt`／`<id>c.png`／`<id>.png`：
  - `0-客戶端` 全部 24 個 idx：`Sprite.idx`、`Sprite00`–`Sprite15.idx`、`Text.idx`、`Tile.idx`、`LiTo\LTicon`／`LTsprite`／`LTsurf`／`LTtext`／`LTtile.idx`。
  - kor25、t89、ly 的 `Tile`／`Text`／`Sprite*.idx`。
- **結果**：

  | 圖 | 本機 `0-客戶端` | kor25 |
  |---|---|---|
  | surf 6487–6494、6502–6507（確認框、能力 Up／Down、Customizing Exit） | 都有（`Sprite00`–`15`；6502–6507 也在 `LTsurf`） | 有 |
  | surf 7156（主窗背景） | 只在 `LiTo\LTsurf.pak`，不在釘選的 17 個 Sprite 檔裡 | `Sprite14` `7156.img`（由 P-B 備援補上） |
  | surf 8670–8689（標題、進度、按鈕） | **全部沒有** | **全有**，例如 `Sprite03` `8670c.img`、`Sprite02` `8672.img`、`Sprite01` `8680.img`、`Sprite13` `8689c.img` |
  | surf 8734、8735（Msg 底圖） | **沒有** | `Sprite01` `8734.img`、`Sprite02` `8735.img` |
  | png 10933、10935（能力底圖、Customizing 底圖） | **沒有** | `Sprite06` `10933c.png`、`Sprite08` `10935c.png` |
  | png 10911（StatInfo 底圖） | **沒有** | `Sprite02` `10911c.png` |

  - **更正 PAK§4.1「kor25 5/22」**：8670–8689 在 kor25 多半是 `c.img`，舊表沒有把 `c` 名稱算進去。實際上 kor25 22 個全都補得到；PAK§7「8734／8735 待找」也已在 kor25 找到。
  - 照 `SurfCatalog.NameHash` 的規則，這些圖放在 kor25 的 Sprite 檔號應該都對得上。這點只做了清單比對，沒有在本機實際解碼，**待驗證**。
  - kor25 在預設路徑時，兩個窗都不應缺圖。參考客戶端不在時，連 XML 都讀不到：重置走 WinForms，資訊窗不開。
- **StatInfoUI 的「surf 數 0」**（PAK§7 待驗證項）：已確認這個版面沒有 `Surf` 屬性，唯一的圖是 `BasePng="10911;2;28"`。

## 怎麼驗證

- 編譯：`dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o scratch/t763/build` 結果 **0 錯誤**。2 個警告在 `MagicUi.cs`，原本就有。
  - 本機 restore 的處理方式同 #761：把主專案 `obj/` 的 `project.assets.json`、`nuget.g.props`、`nuget.g.targets` 讀出來，路徑改成本 worktree 後放進 worktree 的 `obj/`。`engine/audio` 則用一般的 `dotnet restore`。主專案只讀、沒有寫入。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`，0 錯誤。
- 依 2026-11-09 本機執行限制，**本機沒有執行任何編出來的程式**。`reset-stat-ui`、`stat-info-ui` 與既有測試交給 PR 的 GitHub Actions CI。
- `rebuild-lock.json` 沒有改動。

## 未驗證什麼（待驗證）

- 本機不能跑程式，所以沒有實測真實 kor25 的兩份 XML 經面板解析後是否 `Available`。只做了靜態比對：元素結構與合成版面相同，用到的屬性都有處理。
- 以下都是**推測**：
  - 反向繪圖順序。
  - 只畫 `_Screen`／`_screen` 版、不畫 `_Multi`。
  - 進度 On/Off 的階段對照。
  - 50 級以上用 Up 選屬性。
  - 階段結束時按 OK 關窗。
  - Esc 的離開提示。
  - StatInfo 開在狀態窗右側。
  - Message_Window 的 `DarkBack`（變暗背景）沒有畫，因為 XML 沒有顏色或 alpha。
  - 參考客戶端版面是否與 TW16 原版相同（PAK§7）。
- 真機（交給 Codex／dot，P24Lab 測試角色）：
  - 在重置 NPC 觸發 S43 後，是否開出原生重置窗，配點、升級、確認送出、萬能藥點數是否正常。
  - 狀態窗按能力加成鈕後，資訊窗是否在右側開出。
  - `--classic-ui` 下是否仍開 WinForms 重置對話框。

## 沒有做的部分（待做）

- `Customizing_Win` 與 StatInfo 的各項加成值（傷害、命中、爆擊、負重、ER、MR、回復…）：引擎目前沒有這些數值，沒有做，也不自己編算式。之後有數值來源時，接到 `StatInfoPanel.ValueProvider`，並在 `ResetStatPanel` 建出 `Customizing_Win`。
- 原版的出生加點資訊（S43 kind 4，`OriginalBonus`）目前只附在 Msg 文字裡，沒有獨立欄位。
- 字串 3857–3878、4082 是參考客戶端字串表的 id，跟 #768 一樣只經 `TextResolver` 取字，產品中是 null，所以不畫字。
- 若原生面板在重置途中 Faulted，畫面會停用，而且不會自動改開 WinForms 對話框；重新登入可恢復。

## 產出路徑

- `engine/source/NativeUi/ResetStatPanel.cs`（新）
- `engine/source/NativeUi/StatInfoPanel.cs`（新）
- `engine/source/NativeUi/ResetStatPanelTests.cs`（新）
- `engine/source/CharResetUi.cs`（原生分流）
- 本報告 `docs/reports/issue-763.md`
- 分支 `task/763`

## 給下一棒的提示

- 真機驗收步驟：
  1. 原生 UI 開著、kor25 在預設路徑時，找重置 NPC，log 應出現 `ui-xml-ref ResetStatUI.xml`，以及 8670 等圖的參考備援紀錄，並開出全畫面重置窗。
  2. 加 `--classic-ui`，或在 `native-ui.json` 設 `"refClient":false`，應開 WinForms 對話框（原狀）。
- 若真機看到繪圖順序、進度對照或 50 級以上的操作不對：
  - 繪圖順序、進度對照：改 `ResetStatPanel.PaintWindow`／`ShownPiece`。
  - 50 級以上的操作：改 `Fire` 的 `UpEvent` 分支。
- PAK§4.1 的 ResetStatUI 列建議更正為「kor25 22/22」（本任務不改 PAK 文件）。
