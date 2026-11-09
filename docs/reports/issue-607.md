# Issue #607 T246（R2-O，PBW-A）：S250 畫面中央訊息與畫面效果

## 做了什麼

新檔 `engine/source/PacketBoxScreen.cs`、`engine/source/PacketBoxScreenTests.cs`。沒有改 `Renderer.cs`（R2-A 留下的 `ApplyScreenShake` 與 `ScreenShake` 掛點已存在，直接使用）、`Program.cs`、`WorldSession.cs`、`UiClientBridge.cs`、`UiPanelRegistry.cs`、`rebuild-lock.json`。

| 項目 | 實作 | 依據 |
|---|---|---|
| S250/84 類型 1／2 | `[WorldReceive(250,Subtype=84)]` 讀 `C(type) S(text)`。放進中央佇列：7000 ms、字色調色盤[2]（綠）、外框[0]（黑）、旗標＝(type≠1)。已有 14 則時先丟最舊的，新的放最前面 | PBW§2.6、ui-layout.md V2 (B) |
| 84 類型 3／4 | 不處理（回 false，記為未處理子碼） | 用途待驗證（宿主 RoundScoreUI，FU3§5.4） |
| 中央訊息原生面板 | `CenterMessagePanel`（`CENTER_MESSAGE`，`[UiPanel(155)]`，World 層，不接收輸入）。最新一則 y＝121，較舊的往上每行 15 px，y≤0 不畫（最多 9 行），x＝400 − 字寬/2（字寬去掉 `\f` 色碼）。字色與外框的畫法沿用紅字面板（`RedNoticePanel.Runs`、±1 px 外框） | V2 (B) `sub_F188A0`／`sub_F19680` |
| `$` 代換 | 畫面層呼叫引擎既有的 `$nnn` 名稱表（`ClientForm.ResolveNpcText`，和紅字面板相同）；沒有名稱表時原樣顯示 | `sub_A11520` 的規則待驗證（見下） |
| S250/83 | `[WorldReceive(250,Subtype=83)]`。剩餘 ≥4 byte 讀 D，否則讀 C，之後只允許補 0 | PBW§2.5 |
| 83 = 2 | 晃動開關。依原版 12 步偏移表平移世界畫面，透過 `Renderer.ScreenShake` 掛點 | 本輪反組譯（下表） |
| 83 = 3 | 煙火：自己＋可見物件最多 5 個，用 `rand()%3` 從 gfx 2042／2030／2029 選一個，每個間隔 10 ms，以定點特效加入既有的 `MagicEffects`（由 MagicRendering 播放） | 本輪反組譯 |
| 83 = 1 | 只記錄計數器（資料），**不畫** | 本輪反組譯：其實是疊一張 surf，不是震動（下表） |
| 83 其他值 | 只計數（原版只是移除通知項目，沒有畫面效果） | PBW§2.5 |
| 接線 | `ClientForm.OnInvalidated`（沒有其他模組用這個覆寫）：第一次進來時設定 `renderer.ScreenShake`，原生 UI 開著時把面板接上 `WorldCenterMessageSource` | m6-contract「自動發現」 |
| 快照／重設 | `[WorldSnapshot("packetBoxScreen")]`、`[WorldReset]` | M7-A |

### 本輪補讀的原版細節（已驗證，Capstone 唯讀反組譯）

來源：`TW1608262200.bin`，SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`（已核對）。換算：`.text` FO＝VA − `0x400C00`；`.data` FO＝VA − `0x401800`。

| 項目 | VA | File Offset | 內容 |
|---|---|---|---|
| 83 case 1 | `0x93B91E`–`0x93B937` | `0x53AD1E` | `word_154461C`＝15；`sub_A231C0(100, sub_EFDFD0)` |
| `sub_EFDFD0` | `0xEFDFD0` | `0xAFD3D0` | 計數器非 0 時減 1，再排 30 ms（`push 1Eh`）。從 15 到 0 約 520 ms |
| 83 case 1 的繪製 | `0xF0F737`–`0xF0F76F` | `0xB0EB37` | 計數器非 0 時，取 surf `0x5C1`（**1473**），在 UI 原點、第 4 參數＝計數器，呼叫 `sub_7D7740`（再經函式指標 `[0x14D7DCC]` 畫出）。**畫面不會平移**。所以 PBW 寫的「短震動」並不正確：這是一張由強到弱（15→1）疊上的圖 |
| 83 case 2 | `0x93B93C`–`0x93B961` | `0x53AD3C` | `byte_13CE5DE` 為 0 時設成 1，並 `sub_A231C0(50, sub_F04260)`；為 1 時設成 0 |
| `sub_F04260` | `0xF04260` | `0xB03660` | 旗標開著時，依 `dword_1544644 % 12` 查表（跳躍表 `0xF04418`）寫入 (`dword_154463C`, `dword_1544640`)。計數器 < `dword_138BE6C`×3 時加 1，否則清旗標、計數器歸 0。之後每 17 ms（`push 11h`）再排一次。旗標關掉時，偏移與計數器歸 0，不再排 |
| 12 步表 | `0xF04297`–`0xF043A5` | `0xB03697` | (−1,−1) (−2,−2) (−1,−1) (1,1) (2,2) (1,2) (−1,2) (−2,2) (−1,1) (1,−1) (2,−2) (1,−1) |
| `dword_138BE6C` | `0x138BE6C` | `0xF8A66C` | 初值 8，所以 24 步後會自己停（共約 50＋25×17≈475 ms） |
| 晃動的繪製 | `0xF0FD80`–`0xF0FEB9` | `0xB0F180` | 世界畫面的目的矩形各邊都減去 (dx,dy)，露出的邊用調色盤[0]（黑）補上。也就是世界平移 (−dx,−dy) |
| 83 case 3 | `0x93B96D`–`0x93BAE4` | `0x53AD6D` | gfx 表 {`0x7FA`,`0x7EE`,`0x7ED`}＝{2042,2030,2029}。走物件清單 `0x153CEB0`，`+0xC4` 為真的物件取 `+0x10`／`+0x14` 座標，用 `rand()%3` 選 gfx 建立特效。每個之後 `Sleep(10)`，最多 5 個 |

反組譯腳本只放在 `scratch/t246/`（不進 repo）。

## 怎麼驗證

- **編譯（已驗證）**：`py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/t246/NuGet.Config` 結果 `PASS dev`。用的是暫時 lock，`rebuild-lock.json` 沒有寫入。productDll SHA-256 `2dd7dcd324e733395761c67e02cffee0b96aaaf6e01884b03570c6f83eb031b4`（第 2 次 commit 後）。新檔沒有編譯警告。
  - 本機環境備註（和程式無關，同 #675）：直接 restore 時 WebView2 的 `build/*.targets` 沒有接上，會出現 NETSDK1130。改成 `NuGet.Config` 唯讀指向主專案 `engine/vendor`，`NUGET_PACKAGES` 指到 `scratch/t246/packages`，在沙箱外 restore 就正常。
- **`check-no-game-assets.py`（已驗證）**：`pass`（4021 個檔案，0 個錯誤）。
- **離線測試**：`[WorldModuleTest("packetbox-screen")]`，CI 的 `--module-tests` 會自動執行。依 2026-11-09 的本機執行限制，本機沒有執行任何編譯出來的程式，結果以 PR CI 為準。測試內容：
  1. 註冊：83／84 處理器、重設、快照、面板（只註冊一次）、ClientForm 接線。
  2. 規則：D／C 兩種寬度、補位不是 0 時拒收；12 步表、50／17 ms 時間軸、24 步後自己停、關閉後下一個 tick 歸 0；83/1 計數器 15→0（520 ms）；y＝121／106…1、最多 9 行、x 置中。
  3. 84：類型 2／1 入佇列、7 秒後過期、最多 14 則、最新在前；3／4 不入佇列且記為未處理；格式錯誤會拒收；重設會清空。
  4. 83：1 不平移；2 開→晃動→自己停，再開→中途關→下一個 tick 歸 0；其他值只計數，不加特效。
  5. 83/3：最多 5 個、gfx 依序是 `rand()%3` 的結果、間隔 10 ms、第一個在自己的座標、其餘依物件 id。
     - CI 第一次跑時，這段在「有自己角色」的 session 上做完整快照，會去讀素材（CI 沒有 `engine/assets`），結果丟出例外。已改成直接讀 `FireworkBursts`。
  6. 面板：不在世界時隱藏、`$` 代換、綠字黑框、不接收滑鼠、9 行的 y 值、過期後隱藏、像素檢查（字在 y＝121 那一行且水平置中）、`\f3` 換成紅色、字寬不計色碼。

## 未驗證什麼

- **待驗證：83/1 的 surf 1473。** 第 4 參數（15→1）是 alpha 還是畫格沒有追，所以沒有畫，只把計數器放進快照（`flash.level`，`drawn=false`）。下一棒可以追 `[0x14D7DCC]` 指到的函式。
- **待驗證：83/3 物件 `+0xC4` 旗標的意義。** 目前用「自己＋可見物件（依 id）」當候選，特效放在收到封包時的座標，不跟著物件移動。
- **推定：`dword_138BE6C`（初值 8）執行期沒有被改。** 它的其他引用者（`0x8ABD1C` 等）沒有追。
- **待驗證：84 的 `$` 代換（`sub_A11520`）。** 3.81 的 `"$7647"` 超過 `string-c.tbl` 的 3484 筆，所以目前用引擎的 `$nnn` 名稱表。`"$ 5秒後將會被傳送!"` 這種 `$` 後面接空格的，原樣顯示。
- **待驗證：** 84 第 5 參數（`+268`）的作用、中央佇列畫字函式是否和紅字一樣有 1 px 外框。
- **與原版的差異：** 晃動時露出的邊原版補黑，引擎只平移（露出的地方是畫面底色）。83/2 在 17 ms 內快速開關時，原版會產生兩個計時器，本模組沒有模擬。舊版 WinForms 路徑（原生 UI 關閉時）不畫中央訊息，晃動仍然有效。
- **真機：** 沒有啟動遊戲、伺服器或 DB。畫面位置、晃動與煙火的實際樣子，交給真機 smoke（P24Lab 可以用 3.81 `MobTeleport`／`Chapter01R`／`GreenBroadcast` 觸發）。

## 產出路徑

- `engine/source/PacketBoxScreen.cs`
- `engine/source/PacketBoxScreenTests.cs`
- `docs/reports/issue-607.md`（本檔）
- 分支 `task/607`

## 給下一棒的提示

- 補畫 83/1：追 `sub_7D7740` 的函式指標 `[0x14D7DCC]`，確認第 4 參數的意義，再用 `CenterMessagePanel` 同樣的 World 層面板畫 surf 1473（計數器從 `WorldSession.ScreenFlashLevel` 讀）。
- 煙火候選：追 `0x153CEB0` 清單與物件 `+0xC4` 的寫入者，換掉 `WorldSession.Fireworks` 的候選條件。
- `docs/reference/packetbox-windows.md` §2.5 的「1＝短震動」建議更正為「疊 surf 1473（15→0，約 520 ms）」；「2＝持續晃動開關」補上「24 步（約 0.48 秒）後自己停」。本任務只能改列出的檔案，所以沒有改文件。
- 其他封包（V2 (B) 列的 `S_MESSAGE` 26／28、`S_MESSAGE_CODE` 3599 等）要進中央佇列時，可以直接放進 `WorldSession.centerMessages`（記得自帶毫秒與字色索引），面板不用改。
