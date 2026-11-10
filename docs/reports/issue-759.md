# Issue #759 T321：P-E 名稱與稱號字型改用原版

## 結論
名稱與稱號的繪製改成原版的 **GDI** 路徑。字型參數與 `sub_8444A0` 的 `CreateFontA(12,0,0,0,400,0,0,0,136,0,0,0,0,"MingLiU")` 相同：MingLiU、高 12、FW_NORMAL、字元集 136、品質 0。先畫 4 向 1 px 描邊，最後畫本色。MingLiU 不可用時，退回既有的 GDI+ `DrawString`（WinForms 備援），不用替代圖。`Font\*.fnt` 散檔從主客戶端讀取，並記錄 SHA-256。編譯通過，`check-no-game-assets.py` 通過；離線測試交給 CI。字型外觀要對照畫面，列「待驗證」。

## 做了什麼
- `engine/source/ObjectRendering.cs`（只改文字繪製）
  - 新增 `NativeNameFont`：
    - `CreateFontW` 用與原版相同的參數，在記憶體 DIB 上以 1× `TextOutW` 光柵化成墨點遮罩。
    - `Compose` 先蓋 4 向 ±1 px 描邊，再蓋本色。
    - 依鏡頭倍率最近鄰放大。原版也是 1× 畫在 800×600 面上。
    - 依（字串、本色、描邊色）快取，上限 512 筆。
  - 字型檢查：`GetTextFaceW` 確認 GDI 真的選到 MingLiU／細明體。如果字型對應換成別的字，`Unavailable` 會記下原因，呼叫端退回備援。
  - `PaintNames` 先走 `NativeNameFont.Draw`，失敗才走原本的 GDI+ 路徑。備援的 `Font` 也補上字元集 136。
  - 第一次畫名稱時，在背景記一筆 `name-font` 事件，內容包括：
    - GDI 實際選到的字面
    - `%WINDIR%\Fonts\mingliu.ttc` 的大小與 SHA-256
    - 主客戶端（`TW_CLIENT_ROOT`，預設 `D:\lineage開發\0-客戶端`）`Font\*.fnt` 每個檔的大小、SHA-256、字數
  - `.fnt` 只讀不寫，也不遞迴。台版是 CP950＋MingLiU，依 UI§1 `sub_83DC90`，原版不走點陣字路徑，所以這裡只記錄來源，不拿來畫。
- `engine/source/PlayerHudView.cs`（只改文字繪製）
  - `PlayerHudPainter.NameLine`：HUD 標頭的角色名稱改用同一套原版字型，1×、金色、黑描邊。職業接在名稱後面，照舊用面板字型。
  - 名稱放不下、名稱是空的、或 MingLiU 不可用時，整行退回原本的 `TextRenderer`。
- 新檔 `engine/source/NativeFontTests.cs`：`[WorldModuleTest("native-font")]`，由 `--module-tests` 自動發現，只用合成資料。檢查項目：
  - 常數與 `CreateFontA` 參數一致
  - 描邊合成：十字形描邊、本色蓋在描邊上、空遮罩透明
  - `.fnt` 掃描：只看最上層、依名稱排序、SHA-256、字數（eng 95 字、kor 2401 字、small 不算），不改檔
  - GDI 光柵化：
    - 有 MingLiU：核對半形 6×12、全形 12×12、寬度＝6×Big5 位元組數、快取、透明底
    - 沒有 MingLiU：核對回 null 並記下原因
  - HUD 名稱行與 `Draw` 的回傳值
- 依 #757 報告的說明：`engine/source` 內的測試用 `[WorldModuleTest]` 自動發現，`[CheckGroup]` 只用在 `_integration/dev`。所以沒有改任何註冊表、`Program.cs` 或 `rebuild-lock.json`。

## 怎麼驗證
- 本機編譯：`dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -warnaserror:false -o scratch/t759-build`，結果 **0 錯誤**。2 個警告在 `MagicUi.cs`，不是這次改的。
  - restore 用 `scratch/nuget-759.config`（唯讀指向主專案的 `engine/vendor`），並設 `NUGET_PACKAGES=scratch/t759/packages`。
  - 一開始還原壞了，出現 NETSDK1130（WebView2），和 #675／#676 是同一個本機環境問題。照 #676 的做法，**逐檔**刪掉 `engine/source/obj/` 下的 5 個 NuGet 輸出檔（不遞迴）後重新還原，問題就解決了。這和這次的程式改動無關。
- `py -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`（4105 檔，0 錯誤）。
- 依 2026-11-09 的本機執行限制，本機**沒有執行**任何編出來的程式。`native-font` 與既有單元測試交給 PR 的 GitHub Actions CI。
- 沒有缺資源：字型不在 pak 裡，依 PAK§4.2，`mingliu.ttc` 與 `0-客戶端\Font` 的 11 個 `.fnt` 本機都有：
  - `eng.fnt`、`kor.fnt`、`small.fnt`
  - `kor-batang`、`kor-budl`、`kor-buhwal`、`kor-gaul`、`kor-graphic`、`kor-note`、`kor-pilgi`、`Korgulim`

## 未驗證（待驗證，交給 Codex／真機）
- **畫面對照**：原版 GDI 用 `DEFAULT_QUALITY` 畫在 16-bit 面；引擎用 32-bit DIB，並以亮度 ≥128 二值化。MingLiU 12 px 一般走內嵌點陣字，所以應該相同，但要截圖逐像素比對名稱、稱號與描邊。
- 名稱的 TextOut 原點與既有 `NameLayout` 座標的對齊（以前的 GDI+ `GenericTypographic` 會有亞像素偏移，現在是整數像素），要在真機上確認沒有 1 px 的位移。
- 鏡頭倍率不是整數時，最近鄰放大的觀感。
- CI 的 windows-latest 是否裝了 MingLiU，決定 `native-font` 測試走哪一支。兩支都會檢查。
- `small.fnt` 的格式沒有查證，只記錄 SHA-256。

## 產出路徑
- `engine/source/ObjectRendering.cs`、`engine/source/PlayerHudView.cs`、`engine/source/NativeFontTests.cs`
- 本報告：`docs/reports/issue-759.md`
- 分支：`task/759`

## 給下一棒的提示
- 其他畫面文字（地面物品名 `PaintObjectSelection`、聊天、浮動訊息）如果也要用原版字型，可以直接呼叫 `NativeNameFont.Draw(g,text,fill,outline,x,y,zoom)`；不需要描邊時，可以另外加一個不描邊的版本。
- `name-font` 事件會出現在客戶端事件紀錄，真機驗收時可以拿來確認 `faceUsed` 是 MingLiU，以及 `.fnt` 的 SHA-256。
- 本機建置如果遇到 NETSDK1130：逐檔刪掉 `engine/source/obj/` 下的 `project.assets.json`、`project.nuget.cache`、`*.nuget.dgspec.json`、`*.nuget.g.props`、`*.nuget.g.targets`，再用指向主專案 vendor 的 NuGet 設定重新還原。
