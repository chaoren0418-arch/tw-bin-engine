# Issue #51：地面物品 catalog 與金幣數量輸入

分支：`task/51`。工作目錄：`scratch/wt/task-51`。依本次更新規格採「伺服器權威加已驗證原圖」，沒有等待或猜測原生分類表。

## 已驗證（離線）

### 做了什麼

新增可寫、內嵌的 `engine/source/Resources/ground-catalog.json`。每筆保留 #49 模板 ID、ground gfx、inventory icon、description、wire use type、name token、stackable、unknown 欄位、來源位置及列 SHA；有 #48 payload／frame／RGBA pin 才啟用。

| 物品 | template ID | ground gfx／inventory icon | 結果 |
|---|---:|---|---|
| 金幣 | 40308 | 7／5 | 啟用，`7-0.spr` frame 0 |
| 肉 | 40056 | 36／124 | 保留並改由 catalog 推導 |
| 箭 | 40743 | 66／7 | 保留，固定 frame 0，不播放 fly |
| 治癒藥水 | 40010 | 45／80 | 模板及 pin 齊全，啟用 |
| 終極治癒藥水 | 40012 | 46／81 | 未啟用：#48 無 gfx 46 pin |
| 自我加速藥水 | 40013 | 42／77 | 未啟用：#48 無 gfx 42 pin，不能借 gfx 41 |
| 傳送回家的卷軸 | 40079 | 22／474 | 未啟用：#48 無 gfx 22 pin |

模板來源為 `task/49@ddc2267` 的 `work/issue-49/contract.json`，原始 blob SHA-256：`dcdd5a3e6883e4405a6b1b297fa5f6efab25596321605b87cf14a700cad3bfdd`。先依指示 fetch，再以 Git blob 讀取，沒有查 DB。#48 來源為 `docs/reports/issue-48.md` 的八筆 payload／十幀表；catalog 的 offset 明標 PAK File Offset、SPR payload File Offset，非 RVA／VA。

`GroundRules.Droppable`、`IsGround`、繪圖及 Web 圖片允許清單共用 catalog。地面選取只接受由完整 S87 envelope 收入的物件；繪圖產生的 opaque pixel owner 接回既有 hit-test，沒有保留 gfx 36／66 白名單。背包識別同時核對 description／use type／icon／精確名稱與數量，不能只憑同名或同 gfx 放行。

`GroundSpriteReader` 由 #48 所用 decoder 的格式移植，唯讀 `TW_CLIENT_ROOT`（預設 `D:\lineage開發\0-客戶端`）下的 `Sprite.idx`、PAK bounded slice。驗證完整 IDX SHA、record／offset／長度、payload SHA、frame header／bounds 及 RGBA SHA，保留 tile A/B、sort key、source order、透明洞與 anchor。只在記憶體解碼／產生 Web PNG；沒有複製 PAK、SPR、map 或 PNG 進 repo。缺件／pin 不符時不借圖，renderer 留明確通知、Web 回 404。

WinForms 改用文字數量欄，避免 `NumericUpDown` 把負數、小數或超額值自動修成可送值。WinForms 按鈕、P 快捷鍵與 Web 都經 `SendGroundQuantity`：只接受 ASCII 十進位正整數，範圍為 `1..min(持有量,int.MaxValue)`。Web 傳原始字串，不先轉成浮點數或捨去小數。

UI 保留當時的世界／物件／選取 revision／資料 revision／持有量快照；背景更新不會偷偷換成新選取。過期後需再次點選物品。送出前重新驗證快照與資料 revision，連數量變更後又恢復原值的 ABA 情況也拒絕。C25／C112 仍呼叫 `GroundRules.Request`，沒有預扣；S158 只代表動作，財物仍以 S87／背包回包為準。

保留 900ms、3000ms、距離、交易／死亡、速度與 owner lease 防護。已裝備（含 S64 裝備欄）、封印／綁定指示或未知 flags 一律拒絕丟棄。#49 的 droppable、equippable、bindingRestrictions、storageCountMax 仍為 **unknown**；這些是本次保守政策，不宣稱完整重建伺服器限制。普通列僅接受 flags 0／1、charges 0 與核定 blessing；肉另保留既有 bless 0 fixture，相容值記在 catalog。伺服器仍可能因未公開配置拒絕請求。

### 怎麼驗證

| 檢查 | 最終結果 |
|---|---|
| dev lock | 185 個 source 檔、194 個 pins 通過；只更新 dev lock |
| dev 完整建置 | SDK 10.0.300；主程式與 launcher 各 0 警告、0 錯誤 |
| 新增 GroundChecks | 309 斷言／9 組通過 |
| 既有肉／箭生命週期 | candidate 252、extra 87 項通過 |
| 實際 `panel.js` | 38 項通過，使用明示 DOM 替身 |
| T041 PacketTraceChecks | 36 項通過；追蹤實作未修改，不記 payload |
| 來源交叉比對 | 7 個模板列、4 筆 pins、3 筆禁用理由及 #49 fixture 原始 bytes 全符合 |
| 差異檢查 | `git -c core.whitespace=cr-at-eol diff --check` 通過，保留既有 CRLF |

GroundChecks 使用真正建出的產品 DLL、#49 原 fixture、明示合成角色狀態與記憶體 sender；沒有啟動產品入口或建立網路連線。涵蓋金幣／肉 S87、S15、非物品與超量拒絕 fixture；四種物品的 1／N C25／C112；零、負、小數、溢位、超額、過期選取與送出前變更；S158 不結算；裝備與身份反例；真實 3000ms pending；四張原圖 pin；實際 `ComposeGround → Blit → VisibleObjectIds → SelectGround`。肉／箭逐 block、逐像素與既有地面 JSON 相同。

既有生命週期測試來源 SHA：`f3ac34a5b40a9d50ecc55cca6bc57163b2e21f7d32e3ba591f97a19b631beb84`，位於原專案 `work/engine-ground-lifecycle-r1/checks/Program.cs`。`legacy.py` 只調整測試宿主的 AppContext 基準路徑，使既有 `../../assets` 讀取原專案資源；測試邏輯原樣執行，複本與建置輸出只在本 worktree。新測試也使用相同唯讀資源基準。沒有建立或改動 `engine/assets` junction。

#49 fixture 是條件式靜態推導，**不是實機擷取**；`pickup-nonitem` 驗證未納入 ground 的 ID 不能選取，並不證明伺服器 Java 分類可由 S87 全部還原。S87 沒有 template/type discriminator；若另一種物件逐欄完全冒充已核定 envelope，現有 wire 本身無法區分。原生分類全鏈、7↔3247 alias、數量換圖門檻仍為 unknown。

初次網路還原遇到 NuGet TLS 錯誤，改以 scratch 內 NuGet.Config 指向原專案既有離線 `engine/vendor`，套件快取留在本 worktree。測試曾發現 JSON `byte[]` 被當成 Base64 的型別錯誤，已改為整數陣列並重跑上述最終建置及兩套地面回歸。未把早期失敗或中間版本結果列為通過。

主要重現命令（先將 Python 3.12 加入 PATH；設定本 worktree 的 DOTNET_CLI_HOME、NUGET_PACKAGES、TEMP／TMP）：

```powershell
python -B -X utf8 engine/_integration/dev/ground-tests/verify-sources.py
python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock
python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/issue-51/NuGet.Config --output scratch/issue-51/build
dotnet build engine/_integration/dev/ground-tests/GroundChecks.csproj -c Release -o engine/build/issue-51-tests --configfile scratch/issue-51/NuGet.Config
dotnet engine/build/issue-51-tests/GroundChecks.dll . work/issue-51/result.json 'D:\lineage開發\0-客戶端' 'D:\lineage開發\TW1608262200研究\engine'
python -B -X utf8 engine/_integration/dev/ground-tests/legacy.py --original 'D:\lineage開發\TW1608262200研究'
node engine/_integration/dev/ground-tests/web-checks.cjs . work/issue-51/web-result.json
dotnet run --project engine/_integration/dev/packet-trace-tests/PacketTraceChecks.csproj -c Release
```

可連 NuGet 的乾淨環境可將 configfile 換成 `.github/nuget.ci.config`。`verify-sources.py` 需要先 fetch `task/49`。本機輔助 Python 位於 `C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe`，僅執行，不寫入該目錄。

產出：catalog、`GroundCatalog.cs`、`GroundSpriteReader.cs`、相關 Ground／Inventory UI／Web 接線、`engine/_integration/dev/ground-tests/`、dev lock，以及 `work/issue-51/fixtures-49.json`、`result.json`、`web-result.json`、`validation.json`。可重建產品位於 `engine/build/client-product-dev`。完整本輪命令紀錄在 `scratch/issue-51/build`，正式摘要不依賴 scratch 保留。

## 待真機（#52）

沒有啟動／附加既有遊戲、啟動服務或 DB，沒有連正式服務；沒有修改主專案 #50、正式 BIN／客戶端、封存 release／lock、機密設定或 `docs/REPORT.md`。WinForms／WebView2 的實際畫面、滑鼠體感與真實伺服器接受情況尚未驗證。

給下一棒的提示：Claude／調度器先審查 catalog 交集、保守政策與離線結果，合併由使用者裁定。#52 由使用者執行真機驗收：

1. 以此 dev 建置與同 pin 客戶端資源，核對金幣 gfx 7 原圖、透明洞與點選；分別在 WinForms、Web 輸入 1／持有量 N 丟棄及拾取。確認背包／地面量只隨權威回包更新。
2. 測零、負、小數、超額；選取後等待數量更新再送出應拒絕，再點選後可重新操作。核對裝備／綁定拒絕、距離／交易／死亡、連點 900ms 與超過 3000ms 未確認情形。
3. 回歸肉、箭與治癒藥水，核對地面重疊時的可見像素選取。gfx 46／42／22 維持未啟用；若要擴充，另由資源任務核定對應 pin，不借圖。
4. T041 只保留 opcode／長度／時間；不得為真機驗收開啟 payload 記錄。原生分類與 alias unknown 不另派禁止的執行期 dump。
