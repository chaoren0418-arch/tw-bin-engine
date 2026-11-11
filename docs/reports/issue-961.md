# Issue #961 報告：T419 R11-E 頭上對話框

## 做了什麼
- 新檔 `engine/source/HeadBubble.cs`：照 FU16（`docs/reference/followups-16.md`）§5 的 B1–B19 做 TW16 頭上對話框。
  - `HeadBubbleLayout`：
    - B2 前處理：結尾「空白…`:`」截斷；去掉 `\f?`、`\b?`、`\i0-5`；白色文字時 10 個 Big5 字換成空白；最多 768 bytes。
    - B3–B6 斷行：照 §1.2 代價表；同代價取最後一個；剩下的字 ≤ M 就整段當最後一行；商店上限是 W0＋W0/4。
    - B8 錨點、B9 矩形、B10 夾邊界（48／880／540，左邊超出時平移到 2）。
    - B11–B13 繪製：1 px 線段、4×4 圓角、7×4 尾巴；沒有外框時畫 9×5 黑三角＋7×4 色三角；文字每行 +12。
    - 點陣是 BIN `.data` 0x1368CEC 的 52 bytes，寫成程式常數（不用 surf）。
  - `HeadBubbleBoard`：
    - B1：每個物件最多一個框。
    - B15：到時間直接移除。
    - B16：新框通過夾邊界後，和它相交的舊框改灰 `0x4A52`。
  - `HeadBubbleShops.Sync`：照 `sub_7CFFC0` 每 5000 ms 用「此刻輪到的那一行」重建商店框（5 秒、白色、一律有外框），不受全名模式影響。收攤、看不到、開關關閉時移除（`sub_7CFCB0`）。
  - `HeadBubbleOptions`：
    - `ShopMessages`＝`byte_136E99F`「個人商店名稱」，預設開，存在 `option-ui.txt` 的 `show-shop-message`。
    - `BubbleFrames`＝`byte_13AAF28`「泡泡話框」，預設關、不存檔，跟原版一樣。
  - `WorldSession.Field252Of`／`SetField252`：真正的 +252 欄位，世界重設時清空，用自動發現的 `[WorldReset]` 註冊。
  - `ClientForm.PaintHeadBubbles`：每幀同步並照串列順序畫。文字用名稱同一套 GDI（MingLiU 12，1 px 黑框）。
- `ObjectRendering.cs`（只動商店訊息與 +252）：
  - `NameMarksFor` 的 `Line252` 改讀 `Field252Of`，不再放商店訊息。
  - `PaintObjectSelection` 在名稱之後呼叫 `PaintHeadBubbles`。
  - 「名稱顯示」頁加「個人商店名稱」勾選框。
- `PrivateShop.cs`：只更新 `ShopTitleLine` 的註解。
- 新測試 `engine/source/HeadBubbleTests.cs`（`[WorldModuleTest("head-bubble")]`）涵蓋：
  - 點陣 SHA-256；
  - 前處理、代價表、斷行；
  - 錨點 k＝17／32／12；
  - 矩形與夾邊界的 6 種情況；
  - 外框、尾巴、圓角逐點位置；尾巴朝上、沒有外框兩種畫法；
  - 每物件一個框、變灰、8 秒移除；
  - 商店 5 秒重建、M＝25、開關、收攤、看不到、兩家店重疊；
  - +252 欄位與重設登錄。
- **超出可改範圍的修改（必要）**：既有測試 `ObjectDrawTests.cs`:153 與 `PrivateShopTitleTests.cs`:81–95 原本斷言「商店訊息＝+252 行」。這正是本任務要改掉的行為，所以兩處都改了：
  - 商店訊息改用 `ShopTitleLine`、頭上對話框核對。
  - 全名模式的 +252 版面改用 `SetField252` 測。
  - #916 報告第 72 行已預告要改這兩處。

## 怎麼驗證
- 編譯：用 #675 的做法——NuGet.Config 唯讀指向主專案 `engine/vendor`，`NUGET_PACKAGES` 指到 `scratch/t961/packages`。`dotnet build engine/source/OfflineClient.csproj -c Release` 結果 0 個錯誤，新檔沒有警告。
  - 本機一般 restore 會出現 NETSDK1130（WebView2，和本次修改無關，同 #675／#678）。
  - `rebuild.py --build` 的離線 restore 在這個 worktree 失敗（product-restore exit 1）。
- `check-no-game-assets.py`：pass（errors 0）。
- 點陣 52 bytes 直接從 BIN（`P01-分析基準/input/TW1608262200.bin` FO `0xF674EC`）唯讀讀出，SHA-256 `9d9ffa30…f028`，和程式常數、FU16 相同。
- **單元測試沒有在本機執行**（使用者規定不得執行編譯出來的程式）。測試結果以 PR 的 CI 為準；測試的期望值是逐項手算的。

## 未驗證什麼
- **真機外觀（待驗證，交給 Codex／M4）**：
  - 圓角、尾巴偏 1 px、變灰、文字位置；
  - 5 秒換行時框會重建，原版沒有淡出。
- **座標對應（待驗證，FU16§6 第 4 點）**：引擎把「場景座標 − 視窗左上角的場景點」當成 TW16 畫面座標，直接套 48／880／540。引擎視窗可縮放，所以實際夾邊界的位置要真機對照。
- **推測**：
  - 前處理掃描時跳過 Big5 的第二個 byte，避免把 0x5C 當成 `\`。
  - 盲眼模式 1 不畫他人的框（比照名稱）。
  - 夾邊界每幀重算（原版只在建立與鏡頭平移時算，`sub_7CFF40`），變灰則只在建立時判定。
- **沒有做**：
  - 關鍵字過濾 `sub_4FED10`：引擎沒有，當作「沒設過濾」，框色不變綠、秒數不變。
  - `m`（物件 +608 附加物件高度）：當 0。
  - 聊天圖示 `a7`：PngList.xml 不在任何 pak（FU16§0.4，#956 已用 PakViewer 查過 24 個 idx），本任務沒有重查、也沒有用圖。
  - option12 HyperAct `bubbleon`／`showMargetMsg`：引擎沒有 option12 頁，開關先放在 WinForms「名稱顯示」頁。
- **可選做、沒有做**：一般聊天（8 秒）與喊話的頭上對話框。`HeadBubbleLayout.Create` 已能直接用（`ChatSeconds`、`Shout` 色、`forceFrame`），但沒有接到聊天流程（`PlayerChat.cs` 不在可改範圍）。
- 自己的商店訊息：引擎的自己沒有 `ShopText`，所以不畫（k＝17 的規則已寫好）。
- +252 的語義與來源封包：3.81 沒有已知欄位，正式流程一律空字串，全名模式的 +252 行實際上不會出現。

## 產出路徑
- `engine/source/HeadBubble.cs`、`engine/source/HeadBubbleTests.cs`（新）
- `engine/source/ObjectRendering.cs`、`engine/source/PrivateShop.cs`、`engine/source/ObjectDrawTests.cs`、`engine/source/PrivateShopTitleTests.cs`（改）
- 本報告 `docs/reports/issue-961.md`

## 給下一棒的提示
- 一般聊天頭上對話框（可另開小任務，只要改 `PlayerChat.cs`／S_SAY 處理）：
  - 收到訊息時呼叫 `HeadBubbleLayout.Create(id, 原始 byte, 白或 0x7F99, 8, false, 單一 '*' 開頭, HeadBubbleOptions.BubbleFrames, 錨點, R.Bottom, now)`；
  - 再呼叫 `ClientForm.headBubbles.Add(b, x => HeadBubbleLayout.Place(x, origin))`。
  - `Sync` 只會移除商店框，不會動到聊天框。
- 查到 +252 的來源封包後，呼叫 `WorldSession.SetField252` 寫入即可；全名模式 +252 行與 k＝32 都已接好。
- 真機驗收時核對：商店框在名稱上方 12 px；開全名模式時位置不變；5 秒換第二行；兩家相鄰的店，先開的那家框變灰。
