# 客戶端 .pak 資源盤點（T318／#752）

**依據**：使用者 2026-11-10 07:58 裁定（#743 決定 2）不同意「缺資源」算終態，要求用 PakViewer 查遍 `D:\lineage開發\0-客戶端` 的所有 .pak。先前只用自寫腳本搜過頂層 `Sprite*.idx`／`Tile.idx`（[`windows-2.md`](windows-2.md) 第 8 節、`idx_find.py`、`t226_surfcheck.py`、`t690_surfcheck.py`），**沒有搜 `LiTo\LT*.pak`，也沒解開 `Text.pak` 的加密檔名**。本輪全部改用 PakViewer CLI 重查。簡稱：本文 **PAK**，引用寫 `PAK§n`。

**結論（先講）**
- **K3 的 18 列裡，在 `0-客戶端` 找到 1 列**（5:`sub_8444A0` 字型）。字型不在 pak 裡：GDI 用 Windows 系統字型 `MingLiU`，點陣字是散檔 `Font\*.fnt`，兩者本機都有。這一列不是缺資源，是程式差異，改歸 K5。
- **其餘 17 列要的 XML 都不在 `0-客戶端`**。24 個 idx 都用 PakViewer 查過，查法有三種：檔名完全相同、加上 `-c`／`-h` 變體、關鍵字。另外把 Tile.pak 的 65 份 XML 全部解密，搜視窗名稱，也沒有結果。
- **這 17 列的 XML 在本機 3 套參考客戶端全部都找得到**：kor25、t89、ly，都放在各自的 `Tile.pak`。先前文件寫「他版也沒有」的 `AgitAuctionUI.xml`，其實 3 套都有，該更正。
- 先前標成「本機沒有」的 7 項：
  - 這次在 `0-客戶端` 找到 3 項：`monlist-c.html`、`summonList.xml`、`recipe.tbl`。
  - `string-c.tbl` 找得到，但只有 3484 筆，沒有第 4118 筆。
  - 仍找不到 3 項：surf 9183／9184、`SpellList.xml`。這 3 項在參考客戶端都有。

## 1. 工具與方法

- **工具**：`D:\lineage開發\PakViewer-main\src\PakViewer.Cli\bin\Release\net10.0\pakviewer-cli.exe`，原始碼在 `src\PakViewer.Cli`。
  - 只用了這幾個命令：`pak list`、`pak info`、`pak extract`、`xml decrypt`。
  - 這些命令只讀 pak：`PakFile` 以 `FileAccess.Read` 開檔，見 `Lin.Helper.Core\Pak\PakFile.cs:149`。`Save()`／`Create()` 都沒呼叫。
  - GUI 版 `PakViewer.exe -cli` 是 WinExe，主控台沒有輸出，所以不用。
  - 加密：Text.idx、LTtext.idx 由 `LoadIndexAuto` 自動辨識成 **L1** 並解開檔名，其他 idx 都是 **None**。
- **腳本**（都在 [`rea-scripts/pak/`](rea-scripts/pak/)，從 repo 根目錄執行）：
  - `pak_index.ps1 [-WithAlt]`：列出每個 idx 的清單，寫到 `scratch/pak-index/<idx>.list.txt`，摘要寫到 `SUMMARY.txt`。加 `-WithAlt` 會另列參考客戶端的 `Tile`／`Text`／`Sprite*.idx`，寫到 `scratch/pak-index/alt/`。
  - `pak_extract.ps1`：解出本文用到的檔案，寫到 `scratch/pak-extract/<來源>/`。加密的 XML 另存 `.dec.xml`。SHA-256 寫在 `HASHES.tsv`。
  - `pak_find.py`：讀上面的清單與解出檔，輸出檢索結果。指令是 `py -3 -I docs/reference/rea-scripts/pak/pak_find.py > scratch/pak-index/FIND.txt`。內容包括：
    - 完全相同的檔名，以及 `-c`／`-h` 變體。
    - 關鍵字掃描。
    - 參考客戶端 XML 引用的 surf，在本機全部 pak 裡的覆蓋率。
    - `string-c.tbl` 的第 4118 筆。
- **只解到 `scratch/`，不進 git**。參考客戶端的 XML 與 surf 也一樣。

## 2. 搜尋過的 pak（`0-客戶端`，共 24 個 idx、308,323 個檔名）

| idx（→ pak） | 加密 | 檔數 | 主要副檔名 |
|---|---|---:|---|
| `Text.idx` | L1 | 37,498 | .html 30,866、.h 2,622、.ht 2,106、.htm 1,324、.tbl 217、.bak 119、.def／.txt |
| `Tile.idx` | None | 5,446 | .til 5,364、**.xml 65**、.lac 8、.spr 8 |
| `Sprite.idx` | None | 92,356 | .spr 87,344、.tbt 2,334、.img 1,862、.png 803、.tbl 3 |
| `Sprite00.idx`–`Sprite15.idx`（16 個） | None | 10,184–10,972 | .spr、.img 296–423、.tbt 132–262、.png 73–113 |
| `LiTo\LTtext.idx` | L1 | 353 | .html 256、.tbl 88、.def 6 |
| `LiTo\LTsurf.idx` | None | 1,623 | .img 1,613、.spr 10 |
| `LiTo\LTsprite.idx` | None | 469 | .spr |
| `LiTo\LTtile.idx` | None | 1,210 | .til |
| `LiTo\LTicon.idx` | None | 227 | .lbt |
| `launcher\TW13081901.pak` | — | — | **沒有 .idx**，PakViewer 打不開（開頭不是已知格式）。這是 launcher 的 pak，與遊戲 UI 無關（推測） |

**散檔資料夾**：`Sprite`、`Surf`、`text`、`Tile`、`icon` 都是空的；`Font` 有 11 個 `.fnt`；沒有任何散的 `.xml`。

**關鍵字**（不分大小寫，比對所有檔名）：`login`、`selectui`、`createui`、`servername`、`loading`、`mail`、`resetstat`、`statinfo`、`renstatus`、`agitauction`、`fishing`、`optionui`、`ranking`、`spelllist`、`font`、`9183`、`9184`。

只有 4 個檔名命中，都不是目標檔：
- `mail` → `helpmail-c.html`、`MailExcludeUI.xml`
- `renstatus` → `ReNStatusWinUI(-c).xml`
- `font` → `helpfont-c.html`

**內容搜尋**：把 Tile.pak 的 65 份 XML 全部解密，搜視窗名稱。命中的只有別的視窗裡的按鈕，例如 `lineage12.xml` 的 `Mail_Button`／`Ranking_Button`，以及 `SurfList.xml` 的 surf 說明（`6995 Login_Off` 等）。**沒有任何一份是目標視窗的版面**。

## 3. 參考客戶端（只讀，不進 git）

| 代號 | 路徑 | 用途 |
|---|---|---|
| kor25 | `D:\381懶人包kor25(原)` | 主要來源。windows-2 §8 已用過，事件表與 TW16 相同（推測） |
| t89 | `D:\天堂參考用客戶單\89天堂` | 小寫檔名，有 `-c`／`-h` 變體 |
| ly | `D:\天堂參考用客戶單\雷雨天堂` | 有 `-c` 變體 |

kor25 的 XML 解密後，SHA-256 前 12 碼和 windows-2 §8、followups-3 記錄的一致：
- `RenewalMailUI` `e6a0e037ca15`
- `ResetStatUI` `af94ac5257a2`
- `FishingUI` `205b76bb7c18`
- `SpellList` `5d21a9ba95f2…`

所以 PakViewer 解出的內容與先前的自寫腳本相同。

## 4. K3 的 18 列

**表欄說明**
- 「本機」＝`0-客戶端` 全部 24 個 idx 的結果。
- 參考檔的大小與 SHA-256 取 **kor25 `Tile.pak`**：「原檔」是 pak 內的加密檔，「解密」是 PakViewer `xml decrypt` 的輸出。
- surf 覆蓋率的分類：
  - **本機 img**：在 `Sprite*.idx` 找得到。
  - **LT**：只在 `LiTo\LTsurf.pak` 找得到。
  - **tbt**：只有 `.tbt` 檔。
  - **缺**：哪裡都沒有。

### 4.1 需要的 XML 與 surf

| XML | 原檔大小 | 原檔 SHA-256 | 解密 SHA-256 | surf 數 | 本機 img | LT | tbt | 缺（id） | 參考客戶端 Sprite 補得到 |
|---|---:|---|---|---:|---:|---:|---:|---|---|
| `LoginUI.xml` | 4,899 | `c79d636c…3c821a61` | `dfefdd0d…b2c8d58f` | 44 | 0 | 0 | 0 | 44（6993–7031 中 35 個、7091–7098、7231） | t89 44/44、kor25 42/44、ly 38/44 |
| `SelectUI.xml` | 11,532 | `24661d17…a414af40` | `b4eb1fa7…62bc46b9` | 50 | 35 | 0 | 0 | 15（6988、6989、7033–7038、7232–7238） | t89 15/15、kor25 10/15 |
| `CreateUI.xml` | 19,722 | `c7591720…a2d69f04` | `5277a2af…e1a9d65f` | 73 | 48 | 6 | 0 | 19（7099、7128–7143 中 14 個、7170、7171、7707、7708） | t89 17/19、kor25 14/19、ly 14/19 |
| `RenewalMailUI.xml` | 20,327 | `372c8f8e…92aa0988` | `e6a0e037…fd5622b9` | 12 | 10 | 0 | 0 | 2（9183、9184） | kor25 2/2、ly 2/2 |
| `ResetStatUI.xml` | 15,389 | `473c7e16…9cf3f244` | `af94ac52…64c6bbac` | 37 | 14 | 1 | 0 | 22（8670–8689、8734、8735） | ly 20/22、kor25 5/22 |
| `StatInfoUI.xml` | 7,371 | `710c4d24…108b09cb` | `6d569948…adcace99` | 0 | — | — | — | — | — |
| `ReNStatusUIEx.xml` | 14,902 | `799dd129…199383d8` | `4298f5be…ef8418cc` | 12 | 9 | 1 | 2 | 0 | — |
| `AgitAuctionUI.xml` | 28,580 | `2a3cd825…0208e737` | `3869aeac…284d1e6d` | 16 | 5 | 7 | 0 | 4（3961、6239、6247、6248） | kor25 4/4、ly 4/4 |
| `FishingUI.xml` | 1,182 | `a8f908cb…28a5132a` | `205b76bb…da314bb8` | 8 | 0 | 2 | 6 | 0 | — |
| `OptionUI.xml` | 23,378 | `b6e2ac59…4ced047f` | `fe32d2fd…5b8c846d` | 8 | 6 | 2 | 0 | 0 | — |
| `GameRankingUI.xml` | 36,858 | `6e872dcc…56d9c3f7` | `a774c2a6…7a6d610e` | 39 | 39 | 0 | 0 | 0 | — |
完整 64 碼雜湊看 `scratch/pak-extract/HASHES.tsv`（`pak_extract.ps1` 會重產）。

上表的「surf 數」是用 `Surf…="數字"` 屬性數出來的。`StatInfoUI` 是 0，可能是只有文字的版面，或用了別種屬性名稱（待驗證）。

### 4.2 逐列結論

| 列 | 需要的資源 | 本機（0-客戶端）結果 | 結論 | 建議的 code 任務（目標／檔案） |
|---|---|---|---|---|
| 1:0 S_AGIT_LIST | `AgitAuctionUI.xml` | XML 找不到。有相關 HTML：`agit-c.html`（586 B，`5fcd7039…86ed4083`）、`auction1-c.html`（388 B，`3c5fa82a…efaebcbf`），以及 `auction2`／`auction3`、`y_auction_*` | **全部 pak 都查過，仍找不到 XML**。參考客戶端 3 套都有（更正 windows-2 §8「他版沒有」）。缺 4 個 surf，參考客戶端補得齊 | P-A＋P-D：`AgitAuctionUI.xml` 載入成盟屋拍賣窗，接 S156。新檔 `NativeUi/AgitAuctionPanel.cs`＋測試 |
| 1:155 S_READ_MAIL、1:99 S_MAIL_INFO、3:C_MAIL(95)、4:RenewalMailUI | `RenewalMailUI.xml`、surf 9183／9184 | XML、9183、9184 都找不到。有相關的 `MailExcludeUI.xml`（5,075 B）、`helpmail-c.html` | **全部 pak 都查過，仍找不到**。參考客戶端有 XML；9183／9184 在 kor25 與 ly 的 `Sprite00`／`Sprite01` 有（PAK§5） | P-A＋P-B：信件三窗原生面板。`MailUi.cs`→新檔 `NativeUi/MailPanel.cs`，關閉鈕讀參考 surf |
| 1:43 S_VOICE_CHAT（重置）、3:C_VOICE_CHAT(232)、4:ResetStatUI、4:StatInfoUI | `ResetStatUI.xml`、`StatInfoUI.xml`、22 個 surf | 找不到 | **全部 pak 都查過，仍找不到**。參考客戶端有 XML。22 個 surf 用 ly 能補 20 個，8734／8735 待找 | P-A＋P-B：`CharResetUi.cs`→新檔 `NativeUi/ResetStatPanel.cs`、`StatInfoPanel.cs` |
| 4:LoginUI | `LoginUI.xml`＋44 個 surf | 找不到。surf 44 個本機全缺，`SurfList.xml` 只有說明文字 | **全部 pak 都查過，仍找不到**。t89 能補齊 44 個 surf | P-A＋P-B：登入畫面原生化。`LoginControl.cs`→新檔 `NativeUi/LoginPanel.cs` |
| 4:SelectUI | `SelectUI.xml`＋15 個 surf | 找不到 | 同上；t89 能補齊 15 個 | P-A＋P-B：`CharacterManagementUi.cs`→`NativeUi/SelectCharPanel.cs` |
| 4:CreateUI | `CreateUI.xml`＋19 個 surf | 找不到 | 同上；t89 能補 17 個，還缺 2 個 | P-A＋P-B：`NativeUi/CreateCharPanel.cs` |
| 4:RenewalStatusUI | `ReNStatusUIEx.xml` | 找不到。本機的 `ReNStatusWinUI(-c).xml` TW16 不讀（WIN§1.4） | **全部 pak 都查過，仍找不到**。參考客戶端有，surf 本機全有（含 2 個 tbt） | P-A：`BonusStatsUi.cs`→新檔 `NativeUi/RenewalStatusPanel.cs` |
| 4:AgitAuctionUI | 同 1:0 | 同 1:0 | 同 1:0 | 同 1:0 |
| 4:FishingUI | `FishingUI.xml` | 找不到 | **全部 pak 都查過，仍找不到**。參考客戶端有。8 個 surf 本機都有，但 6 個只有 `.tbt`、2 個在 LTsurf | P-A＋P-C：`Fishing.cs`→新檔 `NativeUi/FishingPanel.cs` |
| 4:OptionUI | `OptionUI.xml` | XML 找不到。HTML 頁本機有：`option*-c.html`、`optiong-c.html`（1,277 B，`274dcf03…429c9d55`） | **全部 pak 都查過，仍找不到 XML**。參考客戶端有，surf 本機全有 | P-A：新檔 `NativeUi/OptionPanel.cs`。HTML 頁維持現狀 |
| 4:GameRankingUI | `GameRankingUI.xml` | 找不到 | **全部 pak 都查過，仍找不到**。參考客戶端有，39 個 surf 本機全有 | P-A：`PlayerQuery.cs`→新檔 `NativeUi/GameRankingPanel.cs` |
| 5:`sub_8444A0` 字型 | GDI `MingLiU` 12 px＋`font\kor.fnt`／`eng.fnt`／`small.fnt`（UI§1 V4） | **找到**，但不在 pak 裡：<br>• `C:\Windows\Fonts\mingliu.ttc`（27,446,360 B，`af10c7d6…2c45c5be`）<br>• `0-客戶端\Font\eng.fnt`（1,140 B，`6fad7163…041b2bd3`）<br>• `kor.fnt`（57,624 B，`497b8dff…985f1ae5`）<br>• `small.fnt`（184 B，`36530a91…046be804`） | **已找到（系統字型＋`Font\*.fnt` 散檔）**。這列是程式差異，改歸 K5 | `ObjectRendering.cs`、`PlayerHudView.cs`：名稱與稱號改用 MingLiU 12 px、FW_NORMAL、字元集 136，畫 4 向 1 px 描邊。要對畫面，列「待驗證」 |

**18 列合計**：已找到 1 列（字型）；17 列的 XML 在 `0-客戶端` 全部 pak 都查過，仍找不到，但 17 列在本機參考客戶端都找得到。

## 5. Backlog B16 與先前標「本機沒有」的項目

| 項目 | 本機（0-客戶端） | 參考客戶端 | 結論 |
|---|---|---|---|
| `ServerName.xml` | 找不到 | kor25 3,290 B（原檔 `512c6e4a…4f85cc98`，解密 `b4b2f43c…33b8a064`）；ly、t89 也有 | **全部 pak 都查過，仍找不到**。沒有 `Surf` 屬性 |
| `LoadingScene.xml` | 找不到 | kor25 548 B（原檔 `5d7cdb6a…acc5933c`，解密 `98dcb77c…579c0b1c`）；surf 7229／7230 本機缺，3 套參考客戶端都有 | **全部 pak 都查過，仍找不到**。gap-matrix 11.8 判定 3.81 沒有讀取畫面，不建議補 |
| surf 9183／9184 | 24 個 idx 都沒有 `.img`／`.tbt`／`.png`。`918x` 全段都沒有 | kor25 `Sprite00` 有 `9183.img`、`Sprite01` 有 `9184.img`，各 768 B。ly 的相同（9183 `3c5b830d…dd49bcc6`、9184 `b298af6a…217c061c`），ly 另有 `Sprite14` 的 `9184.tbt` | **全部 pak 都查過，仍找不到**；參考客戶端有 |
| 字串 4118 | `Text.pak` `string-c.tbl` 105,931 B，`4ebbc1a7…a22d43e9`，表頭 3484 筆。`string-c.tbl.bak` 3411 筆。`LiTo\LTtext.pak` `LTstring-c.tbl` 296 筆。**都沒有第 4118 筆** | ly `string-c.tbl` 第 4118 筆＝「確認選擇的項目。是否刪除? (Y/N)」（31 B，`259b86ac…e0c2d6b2`）。kor25 第 4118 筆是 `dummy4120` | **全部 pak 都查過，仍找不到**。ly 的文字與血盟窗刪除確認吻合，但 ly 的表頭 5860 和實際行數 5438 對不上，對齊是**推測** |
| `Text.pak` 的 `monlist-c.html` 等 | **找到**：`Text.pak` `monlist-c.html`（689 B，`3037e119…83c60894`），另有 `monlist0`–`9`、`a`–`j`、`s` 等 30 頁 `-c`，以及 `archmonlist-c.html`。`LTtext.pak` 也有 `LTmonlist-c.html`（380 B，`1911752b…d5cd9eae`）等 | — | **已找到**。先前「`Text.pak` 檔名加密沒確認」的疑點解除：PakViewer 以 L1 解開了 37,498 個檔名 |
| `SpellList.xml` | 找不到。Tile.pak 65 份 XML 都不是 | kor25 35,756 B（原檔 `6430b2a1…e2271d37`，解密 `5d21a9ba…2f1a0cc2`）；ly 有 `SpellList(-c／-h).xml` | **全部 pak 都查過，仍找不到**。引擎現在讀 kor25 路徑（`TW_SPELLLIST_CLIENT`），維持 |
| `summonList.xml` | **找到**：`Tile.pak`，2,982 B，原檔 `becf334b…5b2674ef`，解密 `c1e6ce79…8c046c96`。另有 `summonlist-c.html`（1,393 B，`5ab348d7…f11d20eb`） | — | **已找到**（FU3 已記錄「本機都有」，本輪確認） |
| `recipe.tbl` | **找到**：`Text.pak`，1,473 B，`a50efa19…529e9a2c` | — | **已找到**（`Cooking.cs` 已在用） |

## 6. 建議的 code 任務

參考客戶端的資源**不得複製進 git**（CLAUDE.md）。所以做法是：在執行期讀使用者指定的參考客戶端，沿用 `TW_SPELLLIST_CLIENT` 的前例；沒有參考客戶端時，維持現在的 WinForms 或聊天行備援。

採用哪一套參考客戶端，是**資源來源決策**（windows-2 §8 已列為使用者決定）。下表的各項任務都要等這個決定。

| 代號 | 目標 | 檔案 | 依賴 |
|---|---|---|---|
| P-A 參考客戶端 UI XML 來源 | 讀 `TW_REF_CLIENT`（或 `native-ui.json` 的設定）指定的客戶端 `Tile.idx`，找出 XML 並解密，提供給原生 UI 解析器。找不到時回傳 null，由呼叫端走備援 | 新檔 `engine/source/NativeUi/RefClientXmlSource.cs`、`RefClientXmlSourceTests.cs`（`[CheckGroup]` 自動發現）。不改 `UiPanelRegistry.cs` | 使用者決定來源 |
| P-B 參考客戶端 surf 備援 | 本機 `Sprite*.idx` 找不到某個 surf 時，改查參考客戶端的 `Sprite*.idx`（9183／9184、Login／Select／Create／ResetStat 的缺圖） | 新檔 `engine/source/RefClientSurfFallback.cs`＋測試；接到現有 surf 載入點（實作時先查是哪個檔） | P-A |
| P-C `.tbt` 解碼 | FishingUI 有 6 個 surf 只有 `.tbt`，ReNStatusUIEx 有 2 個 | 現有 surf 解碼器（實作時先查有沒有 tbt 路徑） | — |
| P-D 逐窗原生面板 | 依 4.2 表逐窗做，一窗一張 PR，用 `[UiPanel]` 自動發現 | 4.2 表「建議」欄的新檔 | P-A（＋P-B） |
| P-E 字型 | 名稱與稱號改用 GDI MingLiU 12 px＋4 向描邊 | `ObjectRendering.cs`、`PlayerHudView.cs` | — |
| P-F 字串 4118 | 血盟刪除確認的文字。本機表沒有，先用通用的刪除字串；如果使用者同意，再從參考客戶端讀 | `NativeUi/PledgePanel.cs` | P-A（選用） |

## 7. 已驗證／推測／待驗證

- **已驗證**（PakViewer CLI，只讀）：
  - 第 2 節 24 個 idx 的完整清單。
  - 4.1、第 5 節的有無、大小、SHA-256。
  - kor25 解密後的雜湊與先前文件一致。
  - 9183／9184 在 kor25 與 ly 的位置與內容相同。
- **推測**：
  - 參考客戶端 XML 與 TW16 版面相同。依據只有事件名相同（windows-2 §8），沒有版本號可比。
  - ly 第 4118 筆的對齊。
  - `launcher\TW13081901.pak` 與遊戲 UI 無關。
- **待驗證**：
  - `StatInfoUI`／`ServerName` 的 surf 屬性名稱。
  - `CreateUI` 剩下 2 個、`ResetStatUI` 剩下 2 個 surf 的來源。
  - `.tbt` 的顯示方式。
  - 字型改動後的畫面要真機比對（交給 Codex）。
