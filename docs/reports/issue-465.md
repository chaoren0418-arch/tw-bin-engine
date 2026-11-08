# Issue #465 報告：ui-hpmp 原生 UI 模組（HP／MP 浮動窗與頭上條）

## 結論

- 依 `docs/design/native-ui.md` 第 5.3 節 ui-hpmp、`docs/reference/ui-layout.md` 第 2 節 V1（#456）完成兩個原生 UI 面板：
  - **`HpMpWindow`（浮動窗，Float 層）**：橫排／直排 × 短中長 × 一般／thick，共 12 種版面，座標與 surf 全部照 V1 兩張表；預設橫排、長、一般，位置 (135, 465)。兩顆小按鈕：「切換排法」翻轉 shape、「切換長度」status＝(status＋1) % 3。可拖曳（只在拖曳區），y 上限 550，x＋外框寬不超過 799。HP 條靠右對齊（由左往右縮），MP 與直排靠左；`%d/%d` 置中；百分比在 status 0 或 showPercent 關閉時不顯示；低血紅框（HP ≤ 35%，計數 6、每 300 ms 遞減、奇數幀才畫）。
  - **`MiniHpMpBar`（頭上條，World 層）**：surf 930 底、931 HP、932 MP，各 44×5，填充畫 4 列，MP 在 y＋4。位置＝bbox 中心 −24、上緣 −12；畫面範圍規則照 V1（其他物件另加 y ≤ 600）。自己受 `showCharacterHPMP` 控制。World 層不參與命中測試，所以**不會吃掉點地／點物件**。
- **HP／MP 只用伺服器值**：自己的值來自 `IUiGameState.Hp/MaxHp/Mp/MaxMp`（`World.Status`）；其他物件只用伺服器送的 HP 百分比（`WorldObject.HpByte` ≤ 100，即 opcode 237 等）。沒送的值一律留白，不推算。其他物件的 MP 百分比模型裡沒有 → 不畫 MP 條。
- **本輪新發現（已驗證，本機客戶端）**：台灣版客戶端的 **thick 那一組（2890–2920）會解析到 `<n>c.img`（V4 (e)），尺寸和一般版相同**（例如 2910c.img 是 378×48，V1 量的純 2910.img 是 334×34）。所以浮動窗的外框大小改成**依實際繪出的 surf 決定**（原版夾 799 也是用執行期外框寬；V1 表尺寸只當後備），填充寬也一律用條圖實際寬度。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/NativeUi/HpMpPanel.cs`（新） | `HpMpLayout`（V1 12 種版面表）、`HpMpPanel`（浮動窗，`IUiMovablePanel`）、`HpMpHeadBarPanel`（頭上條） |
| `engine/source/NativeUi/State/HpMpState.cs`（新） | `HpMpSettings`（ClientConfig #10／#11 欄位、descriptor 預設值、載入規則 (−1,−1)→(135,465)、x%800、y%550）、`HpMpHeadTarget`、`IUiHpMpState`、`UiHpMpGameState`（轉接，其他欄位原樣轉發）、`ClientForm.WithHpMpState`（從名稱顯示用的同一組目標取 bbox，經世界相機換成畫布座標） |
| `engine/_integration/dev/native-ui-tests/HpMpChecks.cs`（新） | 離線合成測試 70 項；本機選用的真實客戶端檢查 |
| `UiPanelRegistry.cs` | 加一行（同一行註冊兩個面板：`HpMpWindow`、`MiniHpMpBar`；Layout 填 `uistatus86` 只因註冊格式需要，兩個面板都不讀 XML） |
| `DemoChecks.csproj`／`demo-tests/Program.cs` | 各加一行：分組 `native-ui-hpmp`，也加進預設全組 |
| `rebuild-lock.json` | `--update-lock` 重產（262 個來源檔／271 個鎖定檔） |

**範圍外修改（理由）：** `UiClientBridge.cs` 改 1 行，把 `NativeUiGameState()` 的回傳值包進 `WithHpMpState(...)`。頭上條需要每個物件的螢幕 bbox 和伺服器 HP%，C2 的 `IUiGameState` 沒有這些欄位，而 `UiStaticGameState` 在 bridge 裡建立，模組沒有其他接點能把轉接資料送進 `Update`。轉接邏輯本身全部放在 `State/HpMpState.cs`。

## 怎麼驗證

1. `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock` → `--build`（`--configfile` 指向主專案 `engine/NuGet.Config`，因 worktree 沒有 `engine/vendor`）→ **`PASS dev`**，productDll `673a0d20…3f97`。
2. `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-465/hpmp-checks.json native-ui-hpmp` → **PASS 397**（本分組 70 項，其餘 327 項是每次都跑的基本組）。涵蓋：
   - V1 表：預設版面、6 個代表組合逐值比對；12 種版面的條與鈕都在外框內，橫排 HP／MP 不重疊（直排一般 HP y9 h15 與 MP y22 依 V1 表就是重疊 2 列，照實保留並列為檢查項）。
   - 設定：descriptor 預設、(−1,−1)→(135,465)、(900,600)→(100,50)、show=false 隱藏、status 0 不顯示百分比。
   - 互動：按鈕命中、按下圖 n＋1、放開在鈕外不觸發、長度循環 2→0→1→2、排法切換、thick 切換、重排 x 夾 799、拖曳 y≤550；經 `UiRoot`／`UiInput` 實際拖曳 (−100,−200)、捕捉後按鈕生效；頭上條面板不擋世界點擊。
   - 數值：填充寬、無伺服器值時不填、`%d/%d`、百分比 ≤99 取下／>99 取上。
   - 低血紅框：35% 觸發、偶數不畫／奇數畫、300 ms 遞減、同值不重觸發、新低值重觸發、36% 不觸發、無 HP 不觸發。
   - 合成 `.img` 逐像素：外框 1:1、HP 50% 右段、MP 25% 左段、按鈕位置、白字、缺 surf 不畫（不補色塊）、外框尺寸跟隨實際 surf。
   - 頭上條：錨點、自己 HP% 由伺服器 HP/MaxHP 算、其他物件只有 HP、`showCharacterHPMP` 只藏自己、畫面範圍規則、無轉接狀態不畫、無 MaxHP 不畫、逐像素 22 px／14 px 與 MP 疊 1 列。
   - 註冊表兩個 id；以合成、鎖定的資源 `UiRoot.Build` 成功；頭上條跟隨浮動窗的設定；轉接物件轉發全部欄位。
3. **本機選用檢查**（`TW_CLIENT_ROOT=D:\lineage開發\0-客戶端`，唯讀）→ **PASS 398**：
   - 63 列 surf（12 種版面的外框／條／鈕＋930–932）全部存在，**純 `.img` 尺寸與 V1 表全部相同**；實際繪出的 `c.img` 有 18 列和 V1 不同（全部是 thick 組，見結論）。
   - 渲染 5 張 800×600 畫布（預設、直排、thick、短、thick 直排；數值用合成的 HP 123/456、MP 78/90，**不是伺服器資料**），只輸出到 `scratch/issue-465/real/`，連同 `hpmp-real.json`（含尺寸表與 c 版差異）。目視：外框、條、按鈕、文字位置合理；HP 由左往右縮向中央徽章。
4. 既有分組重跑：native-ui-canvas 429、native-ui-resources 539、item-status 409、combat-admission 1750，**全部 PASS**。
5. `engine/tools/check-no-game-assets.py` → `pass, errors 0`；新增的只有 `.cs`，沒有 `.img`／`.xml`／`.png` 進 git。

### 元件座標表（預設：橫排、長、一般，視窗原點 (135, 465)）

| 元件 | surf | 相對座標 | 畫布座標 | 尺寸 |
|---|---|---|---|---|
| 外框 | 2355 | (0, 0) | (135, 465) | 378×48 |
| HP 條 | 2365 | (9, 11) | (144, 476) | 141×20 |
| MP 條 | 2358 | (183, 11) | (318, 476) | 136×20 |
| 切換排法 | 2368／按下 2369 | (135, 3) | (270, 468) | 22×7 |
| 切換長度 | 2371／按下 2372 | (177, 3) | (312, 468) | 22×7 |
| 拖曳區 | — | (152,0)–(182,30) | (287,465)–(317,495) | 30×30 |
| 低血紅框 | 2937 | (6, 8) | (141, 473) | 163×33 |
| 頭上條 | 930／931／932 | bbox 中心 −24，上緣 −12；MP ＋4 | — | 44×5（畫 4 列） |

## 已驗證／推測／待驗證

**已驗證**（V1 或本輪）：版面表、按鈕行為、拖曳限制、填充方向、百分比顯示條件、低血門檻 35% 與 6／300 ms、頭上條 surf／尺寸／錨點／畫面範圍、設定預設值；本輪：純 `.img` 尺寸＝V1、thick 組的 `c.img` 尺寸＝一般版。

**推測**（本模組的實作選擇）：
- 低血計數「何時重設為 6」：實作成「收到新的 HP 值且 ≤ 35% 時」。V1 只說 HP ≤ 35% 時設為 6。
- 百分比取整方向（≤99 取下、>99 取上）沿用 V1 推測。
- 浮動窗按鈕狀態圖：照 V4 (b) 的 3 張圖規則（n 一般、n＋1 按下）；**滑過圖 n＋2 沒有做**，因為面板收不到「滑鼠離開」，做了會卡在滑過狀態。
- 自己頭上條的 x：V1 的「P.x − 80」基準未展開，暫用其他物件的公式（bbox 中心 −24）。
- 其他物件的頭上條：名稱顯示的可見物件中，伺服器 HP 位元組 0–100 且未死亡者（對應 V1「+217 ≥ 0」）；`+220 == 0` 條件的語義未知，未套用。
- 文字第二色（黑）用 `UiText` 的 1 px 陰影表示；原版 `sub_841770` 是描邊還是陰影未查。字型仍是 C2 的 `UiText`（V4 (f) 已查到 12 px 細明體，屬框架任務）。

**未實作／待驗證**（都**沒有畫替代物**）：
- 遊戲時鐘（`sub_ED6330`）：螢幕繪製點與時鐘表全文未展開，未畫；外框中央的時鐘位置是空的。
- 回復光效疊圖：條件中的等級與 56／66／112 語義未知，未畫。
- HP 條灰／綠變體（角色 byte +86／+87）：語義未知、模型無對應狀態，一律用基準圖。
- 戰士 40% 門檻（`byte_1542AA1 == 7`）：未套用。
- 閃爍頭上條 8393、5866–5868：本機缺檔，未做。
- 設定只存在記憶體（每次登入回到預設）；原版 protobuf 存檔、選項視窗「Etc」4 個勾選框、HyperText 指令都不在本任務範圍。`FloatingHPMP.show` 實際預設（V1 待驗證）先用 descriptor 的 true。
- thick 在台灣版會用一般尺寸的 `c.img`，但條的位置仍是 thick 的常數（例如直排 MP y＝26），畫出來 MP 條略低。原版台灣客戶端是否也是如此，要真機看。
- 開關打開時與舊 HUD 同畫面的實際觀感、縮放 1×／2×、真實伺服器 HP 變化：**未做真機**（ui-native-smoke 負責）。
- CI：`.github/workflows/build.yml` 不在本任務可改範圍，沒有加 `native-ui-hpmp` 這一步；預設全組已包含本分組。

## 產出路徑

- `engine/source/NativeUi/HpMpPanel.cs`、`engine/source/NativeUi/State/HpMpState.cs`
- `engine/_integration/dev/native-ui-tests/HpMpChecks.cs`
- `engine/source/NativeUi/UiPanelRegistry.cs`、`engine/source/NativeUi/UiClientBridge.cs`（各 1 行）
- `engine/_integration/dev/demo-tests/DemoChecks.csproj`、`demo-tests/Program.cs`、`engine/_integration/dev/rebuild-lock.json`
- 本報告 `docs/reports/issue-465.md`
- 未進 git：`scratch/issue-465/`（合成封存檔、測試 JSON、真實客戶端渲染 PNG 與 `hpmp-real.json`）

## 給下一棒的提示

- **其他模組**：沒有 XML 的面板可以照本模組做法——Layout 填 `uistatus86`、`Load` 不讀節點。需要世界物件 bbox 的模組可以直接用 `IUiHpMpState.HeadTargets`，或比照 `WithHpMpState` 在 bridge 那一行外再包一層（衝突時保留兩邊）。
- **台灣版 `c.img` 尺寸可能和 V1／T161 量的純 `.img` 不同**：其他模組若把 surf 尺寸寫成常數，建議改用實際 surf 尺寸，並在本機檢查同時比對純檔與 c 檔（`HpMpChecks.NativeUiHpMpRealClientChecks` 有現成寫法）。
- **CI**：建議主控在 `build.yml` 加一步 `native-ui-hpmp`（同 native-ui-canvas 的寫法，不讀客戶端）。
- **ui-native-smoke 真機請確認**：新角色進入遊戲時浮動窗是否預設顯示；thick 外觀在台灣版的實際樣子；低血紅框的閃法；頭上條是否出現在隊員／怪物上（伺服器何時送 HP%）；自己頭上條的水平位置。
- **後續可開的小任務**：時鐘（先查 `sub_ED6330` 與兩張時鐘表）、設定存檔與選項視窗 Etc 頁、回復光效條件查證。

## 同步 main 紀錄（2026-11-09，第 1 次同步）
- PR #587 與 main 衝突：`git merge origin/main`（含 #514／#515），唯一衝突是 `engine/_integration/dev/rebuild-lock.json`，取 main 版本後以 `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock` 重產（267 個來源、276 個鎖定檔）；`docs/REPORT.md` 用 main 版本（自動合併、未手改）。
- 重驗：`rebuild.py --build` → **PASS dev**（productDll `a8bac311…9168`）；`DemoChecks … native-ui-hpmp` → **PASS 397**（未設 `TW_CLIENT_ROOT`）、設 `TW_CLIENT_ROOT=D:\lineage開發\0-客戶端` 後 **PASS 398**（含本機客戶端檢查）。
- 本模組程式碼未改動。

## 同步 main 紀錄（2026-11-09，第 2 次同步）
- `git merge origin/main`（含 #513／#546／#566），唯一衝突仍是 `rebuild-lock.json`，取 main 版本後 `--update-lock` 重產（272 個來源、281 個鎖定檔）；`docs/REPORT.md` 用 main 版本（自動合併）。
- 重驗：`rebuild.py --build` → **PASS dev**（productDll `901a25bf…8042`）；`native-ui-hpmp` → **PASS 397**（未設 `TW_CLIENT_ROOT`）／**PASS 398**（設本機客戶端）；`check-no-game-assets.py` → pass、errors 0。
- 本模組程式碼未改動。

## 同步 main 紀錄（2026-11-09，第 3 次同步）
- `git merge origin/main`（含 #549 聊天頻道開關、#559 料理窗），唯一衝突仍是 `rebuild-lock.json`，取 main 版本後 `--update-lock` 重產（276 個來源、285 個鎖定檔）；`docs/REPORT.md` 用 main 版本。
- 重驗：`rebuild.py --build` → **PASS dev**（productDll `1b7dffdf…0650`）；`native-ui-hpmp` → **PASS 397**（未設 `TW_CLIENT_ROOT`）／**PASS 398**（設本機客戶端）；`check-no-game-assets.py` → pass、errors 0。
- 本模組程式碼未改動。
## 同步 main 紀錄（2026-11-09，第 4 次同步）
- `git merge origin/main`（含 #556、#557），唯一衝突仍是 `rebuild-lock.json`，取 main 版本後 `--update-lock` 重產（277 個來源、286 個鎖定檔）；`docs/REPORT.md` 用 main 版本。
- 重驗：`rebuild.py --build` → **PASS dev**（productDll `5422c095…5c06`）；`native-ui-hpmp` → **PASS 397**（未設 `TW_CLIENT_ROOT`）／**PASS 398**（設本機客戶端）；`check-no-game-assets.py` → pass、errors 0。
- 本模組程式碼未改動。