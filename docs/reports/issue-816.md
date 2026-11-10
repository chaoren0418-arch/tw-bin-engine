# Issue #816 T353：R7-O WidgetSlot 外觀

依據：`docs/reference/gap-matrix.md` 15.2 第 3 點、15.6 R7-O（T339／#800）。

## 做了什麼

- **`NativeUi/MacroPanel.cs`（主格 RIGHT_WINDOW）**
  - 新增 `WidgetSlotLayout.Open`：照 `UiXmlSource` 的順序讀 `WidgetSlot.xml`，先找本機 Tile.pak（`-c` 版、再找原檔名），再找參考客戶端。png 用 `OptionPngSource` 讀，順序是本機 Sprite、再參考客戶端，並記 SHA-256。
  - 新增 `WidgetSlotWindow.Parse`：讀出 `CornerFrame`、`ButtonLock`、`ButtonMode` 三個元素。Window 的 `Name`、`X/Y`、`BaseSurf` 要和 uistatus86 的框一致（`Slotwindow`、(643,476)、3176），不一致就整份不用，維持 uistatus86 外觀，不混用兩份版面。
  - 讀到 `WidgetSlot.xml` 時：
    - 畫 `CornerFrame` surf 3292，位置 Window+(0,0)；
    - 鎖鈕改成 `ButtonLock`：3293＝上鎖、3294＝解鎖，位置 Window+(0,47)＝(643,523)，取代 uistatus86 的 3191／3192 (634,513)；
    - 畫 `ButtonMode`：BasePng 3350／PushPng 3351，位置 Window+(−6,22)＝(637,498)。按下時顯示 Push，放開恢復 Base。
  - 繪製順序照 XML 子元素順序：鎖 → CornerFrame → ButtonMode。
  - 找不到 `WidgetSlot.xml`（合成測試、沒有參考客戶端）時，外觀和行為完全不變。
  - 註冊仍在 `uistatus86`，只把 `root.Resources` 傳進建構子。沒有改 `UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`WorldSession.cs`。
- **`NativeUi/ExMacroPanel.cs`（擴充格 EX_MACRO_WINDOW）**
  - 讀 `WidgetSubSlot.xml`（Window `SubSlotWindow`、(485,475)、3226）。
  - 讀到時畫 `ButtonLock`：3191 上鎖／3192 解鎖，位置 Window+(149,37)＝(634,512)，切換的是同一個 `MacroBook` 鎖（推測）。
  - 也畫 `ButtonMode`：3350／3351，位置 Window+(152,23)＝(637,498)，和主格的 ButtonMode 是同一個點。
- **`NativeUi/WidgetSlotTests.cs`（新檔）**：`[WorldModuleTest("widget-slot")]`，CI 的 `--module-tests` 會自動發現。全部用合成資料，包含：
  - 註冊；
  - 沒有 XML 時維持原外觀；
  - 主格與擴充格的座標、圖號、畫素與點擊；
  - 拒用的情況：位置不同、BaseSurf 不同、Window 名稱不同、元素重複、Surf 格式錯；
  - 缺圖時不畫、不用替代圖；
  - 產品路徑：合成的本機與參考客戶端放在磁碟上，XML 與 png 從參考端讀到，並記 SHA-256。

### 只做資料、沒做畫面或行為（真機再決定，M30）

- **`ButtonMode` 的 `ModeChange`**：按下只記一行 log「尚未接上（M30）」，頁次和鎖都不變。它會切換什麼沒有查證。主格和擴充格的 ButtonMode 在同一點 (637,498)，推測是用來切換主格／擴充格的顯示，沒有實作。
- **鎖圖的對應**：3293 是扣環閉合的鎖，3294 是扣環打開的鎖（看 `0-客戶端` 的 `3293c.img`／`3294c.img` 判斷的），所以定為 Base＝上鎖、Push＝解鎖，和現有 3191／3192 的規則一樣。這是**推測**：BIN 的鎖狀態沒有追。
- **「TW16 依 UI 名稱讀 `WidgetSlot.xml`」仍是推測**：BIN 的 XML 檔名表裡沒有這個檔名。

## 資源查證（PakViewer，唯讀）

- XML 用 #800 已解出的 kor25 `Tile.idx` #4566 `WidgetSlot.xml`、#4567 `WidgetSubSlot.xml`（`scratch/wt/task-800/scratch/issue-800/x/`）。11 套客戶端、296 個 idx 的清單見 15.2 第 3 點。本機 `0-客戶端` 的 24 個 idx 都沒有這兩個檔案。
- 圖檔是查 #800 的 idx 清單（`pakviewer-cli pak list`），再用 `pak extract` 解到 `scratch/issue-816/` 看圖：

  | 圖 | 0-客戶端 | kor25 |
  |---|---|---|
  | surf 3292 | Sprite11 `3292.img`、Sprite14 `3292c.img` | Sprite11 `3292.img`（另有 Sprite03 `3292.png`） |
  | surf 3293 | Sprite12 `3293.img`、Sprite15 `3293c.img` | Sprite12 |
  | surf 3294 | Sprite13 `3294.img`、Sprite00 `3294c.img` | Sprite13 |
  | surf 3191／3192 | Sprite09／10 `.img`、Sprite12／13 `c.img` | Sprite09／10 |
  | png 3350／3351 | **沒有**（所有 Sprite*.idx 都查過） | Sprite14 `3350.png`、Sprite15 `3351.png` |

  所以 3292–3294 從本機讀；3350／3351 只能從參考客戶端讀，這也是 15.2 已經記錄的結論。沒有任何遊戲檔案放進 repo，測試裡的 XML 是只含必要元素的合成版。

## 怎麼驗證

- **編譯（本機，只編譯、不執行）**：`dotnet build engine/source/OfflineClient.csproj -c Release` 的結果是 **0 錯誤**。剩下 3 個警告都在既有檔案（`MagicUi.cs`、`HtmlActionTests.cs`）。
  - 這台機器有 #761 記過的 WebView2 NETSDK1130 問題：全域快取的套件少了 `lib_manual`。
  - 處理方式：把套件 restore 到 worktree 內的 `engine/.nuget/packages`（不追蹤），再用只放在 `scratch/issue-816/no-winmd.targets` 的 hook，透過 `-p:CustomAfterMicrosoftCommonTargets` 拿掉 winmd，並補上 `lib_manual/netcoreapp3.0` 的參考。repo 的建置檔沒有改。
- **`DemoChecks.csproj`**：本機單獨建置時找不到產品組件，和 #807 是同一個環境問題，與本 PR 無關，交給 CI。
  - 既有的 `native-ui-macro`（`MacroChecks.cs`）用的合成 Tile 裡沒有 `WidgetSlot.xml`，`Reference` 是 null，所以走原本的 uistatus86 路徑，斷言應該不受影響。
- **`check-no-game-assets.py`**：pass，errors 0。
- **離線測試 `widget-slot` 和既有測試**：依本機限制，本機沒有執行，以 PR 的 CI 結果為準。
- **CI（PR #839，run 42848125962、42848125886）**：`dotnet`、`map-continuity`、`npc-offline` 三個 job 全綠。
  - `--module-tests` 的結果是 `failed: 0`，其中 `widget-slot` 66 項全部通過。
  - `native-ui-macro`（既有 MacroChecks）也通過。
  - 第一次推送時有 2 項失敗，是測試的預期顏色寫錯：擴充格的鎖本來就是 3191／3192，面板畫的是對的。第二個 commit 修正了測試。

## 未驗證（待驗證）

- **真機外觀（M30）**，在 P24Lab 開遊戲時要看：
  - CornerFrame 3292 是否在 (643,476)，以及是否蓋住第 1 個分頁鈕；
  - 3293／3294 鎖鈕是否在 (643,523)，上鎖和解鎖的圖是否對調；
  - ButtonMode 在 (637,498) 的樣子，按下後實際做什麼；
  - 擴充格的 3191 鎖是否在 (634,512)。
- **產品環境的參考客戶端**：是否設定成 kor25（`RefClientXmlSource.Configured`）。沒有設定的話 `WidgetSlot.xml` 讀不到，會維持 uistatus86 外觀。log 會有 `widget-slot …`／`ui-xml-ref WidgetSlot.xml …` 可以確認。

## 產出路徑

- `engine/source/NativeUi/MacroPanel.cs`、`engine/source/NativeUi/ExMacroPanel.cs`、`engine/source/NativeUi/WidgetSlotTests.cs`
- `docs/reports/issue-816.md`（本檔）
- 不入 git：`scratch/issue-816/`（解出的 img、`imgdump.py`、`no-winmd.targets`、建置輸出）

## 給下一棒的提示

- **WidgetSlot.xml 的格子座標和 uistatus86 差 1–2 px**：
  - SlotF5 的 SelectImg 是 Window+(10+0+1, 18+0+1)＝(654,495)，Icon 是 (655,496)；uistatus86 是 (655,495)／(657,497)。
  - 第 2 欄的 X 是 35，間距不是固定的 34。
  - 本任務沒有改格子（不在 R7-O 範圍）。要改的話，要先在真機確認 TW16 用的是哪一份。
- **XML 的分頁鈕**：Base 3193／Over 3195／Push 3194。這證實「選中的分頁＝a+1」就是 PushSurf，`TabSelectedOffset=1` 可以從推測改成有 XML 依據；Over（滑過）沒有做。
- **主格和擴充格的 ButtonMode 在同一點**：`ModeChange` 很可能是切換 BAR_485 槽位或主格／擴充格。要追 BIN 的 `ModeChange` handler 才能接上。
- **擴充格的鎖和主格共用 `MacroBook.Locked`** 是推測。
