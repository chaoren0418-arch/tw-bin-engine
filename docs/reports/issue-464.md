# Issue #464 ui-left-status：原生 UI 左狀態框 LEFT_WINDOW

階段：code（claude-local，第 1 次嘗試）。分支：`task/464`。依據：`docs/design/native-ui.md` §5.1–5.3、`docs/reference/ui-layout.md` §2。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/NativeUi/LeftStatusPanel.cs`（新） | `IUiPanel`，Id `LEFT_WINDOW`，圖層 Bar。執行期從 `uistatus86`（先找 `-c`）讀 `LEFT_WINDOW x/y`，再讀 20 個子元件的 `xRPos/yRPos`、`surfNumber`；元件的邏輯座標＝視窗座標＋相對座標，程式裡**沒有寫死任何座標**。繪製內容：底圖 3168、等級條 3169（依 EXP% 從左裁切）、等級、EXP%、AC、MR%、陣營圖示 3170／3171／3172 擇一、陣營值、SP、負重條 3173／3174／3175 擇一（依負重裁切）、負重%、飽食條（低／一般／滿三擇一）、飽食%、金幣。缺節點時丟 `UiLayoutException`（整個原生 UI 拒絕啟用，不補預設座標）；surf 不存在的元件不畫，也不畫替代圖。`Drawn` 會記錄最後一次繪製的每個元件（元素、surf、矩形、文字），供測試與證據使用 |
| `engine/source/NativeUi/State/LeftStatusState.cs`（新） | 轉接層：`LeftStatusState` 唯讀快照、`ILeftStatusSource`、`UiLeftStatusGameState` 裝飾器（包住共用的 `IUiGameState`）、陣營／負重／飽食的分段規則、文字格式、金幣辨識。分段門檻集中成常數，查證後只需改這裡 |
| `engine/_integration/dev/native-ui-tests/LeftStatusChecks.cs`（新） | 用程式合成的 XML 與 `.img` 做離線檢查；另有選用的真實客戶端渲染（需要 `TW_CLIENT_ROOT`，輸出只寫到 `scratch/issue-464/real/`） |
| `UiPanelRegistry.cs` | 把範例註解換成正式註冊行 `new("LEFT_WINDOW","uistatus86",root=>new LeftStatusPanel())` |
| `DemoChecks.csproj`／`demo-tests/Program.cs` | 加 1 行 Compile；新增分組 `native-ui-left-status`，並加進預設全組 |
| `UiClientBridge.cs`（**範圍外，理由見下**） | `NativeUiGameState()` 改成回傳 `new UiLeftStatusGameState(basic, LeftStatusState.From(login.World))`，共改 2 行 |
| `rebuild-lock.json` | 以 `rebuild.py --update-lock` 重產（262 個來源檔／271 個鎖定檔） |

**為什麼改 `UiClientBridge.cs`：** C2 的 `IUiGameState` 只有 Level／HP／MP。設計 §5.1 要求模組在 `State/<Module>State.cs` 加轉接，但真正把遊戲狀態交給 `UiRoot.Update` 的地方只有 bridge 的 `NativeUiGameState()`。不改這裡，產品執行時左框只拿得到等級。改法是用裝飾器包住原本的 `UiStaticGameState`，不改 `IUiGameState` 介面，也不影響其他模組。

### 欄位盤點（既有模型，沒有新增任何封包解析）

| 元件 | 來源 | 狀態 |
|---|---|---|
| 等級 | `OwnStatus.Level`（S_OwnCharStatus／S113） | 已有 |
| EXP% | `WorldSession.Experience.Percent`（`OriginalExpTable`）；32 位元值無法對應級距時為 null，畫面留空 | 已有 |
| AC | `OwnStatus.Ac` | 已有 |
| MR%、SP | `WorldSession.SpMr`（S37）；沒收到 S37 就留空 | 已有 |
| 陣營值 | `OwnStatus.Lawful` | 已有 |
| 負重 | `OwnStatus.Weight`（原服 0–240 刻度） | 已有 |
| 飽食 | `OwnStatus.Food` | 已有 |
| 金幣 | 背包中 `$4`（DescriptionId 0、IconId 5）的數量加總；背包還沒載入就留空，載入後沒有金幣顯示 0 | 已有 |
| 巴洛格／亞希（KARMA）、LEVEL_BTN、負重／飽食警告鈕、Shout 鈕 | — | 不在本任務範圍，未讀取也未繪製 |

沒有任何欄位是推算出來的。伺服器沒送的值一律顯示空白（null）。

## 怎麼驗證

建置：`rebuild.py --update-lock` 後執行 `rebuild.py --build --configfile .github/nuget.ci.config`，結果 `PASS dev`，productDll `da6e768b…c802`。worktree 沒有 `engine/vendor`，所以改用 CI 的 NuGet 設定。

| 指令（`dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . <json> <分組>`） | 結果 |
|---|---|
| `native-ui-left-status`（無 `TW_CLIENT_ROOT`，純離線） | **PASS 368** |
| `native-ui-left-status`（有 `TW_CLIENT_ROOT`，加跑真實渲染） | **PASS 369** |
| `native-ui-canvas`（註冊表現在不是空的） | PASS 429 |
| `native-ui-resources`（有 `TW_CLIENT_ROOT`） | PASS 550 |
| `item-status` | PASS 409 |
| `engine/tools/check-no-game-assets.py` | pass，errors 0 |

離線檢查（全部用合成資源）涵蓋：
- 20 個元件的邏輯座標都等於 XML 相對座標＋(0, 475)。另一份 XML 把原點改成 (30, 100) 時，所有元件跟著移動，證明沒有寫死座標。
- 不在遊戲世界時不顯示。在世界中但沒有任何值時，只畫底圖。只給共用的 `IUiGameState` 時，只顯示等級。
- 陣營切換：600／500 → LAWFUL 3171；499／0 → NEUTRAL 3172；-1／-32768 → CHAOTIC 3170。每種都核對畫出的元素、矩形與像素顏色。
- 負重三段：1（裁成 0 px，不畫）、6、119 → LOW 3173；120、239 → MID 3174；240 → HIGH 3175。核對裁切寬度與邊界像素。
- 飽食：分段規則；112 → 一般條 19 px；224／225／255 的切換；2 → 0 px 不畫，數字顯示 0%。
- null surf：缺 3169 圖檔、3171 沒有 file 屬性、缺 3174 圖檔時，這三個元件不畫，底圖露出，其他元件照常畫。缺底圖時，Bounds 改用現有元件 surf 的聯集，不畫替代背景。
- 缺 `NEUTRAL_ICON` 節點時丟 `UiLayoutException`。
- 文字格式：EXP% 截斷到小數 2 位、不進位（99.999 → 99.99%）；空值一律空字串；金幣辨識規則。
- 點擊左框時命中這個面板，事件不會傳到世界層。

### 同步 origin/main（2026-11-09，PR #586 衝突）

- `git merge origin/main`（帶入 #514 SpellExchange），唯一衝突是 `rebuild-lock.json`：先取 main 版，再用 `rebuild.py --update-lock` 重產（265 個來源檔／274 個鎖定檔），沒有手動拼 hash。`docs/REPORT.md` 沒有衝突，維持 main 版。
- 重新建置：`PASS dev`，productDll `46dcea7b…1910`。
- 重新測試：`native-ui-left-status` PASS 368（離線）／PASS 369（有 `TW_CLIENT_ROOT`）；`native-ui-canvas` PASS 429；`item-status` PASS 409；`check-no-game-assets.py` pass，errors 0。

### 第二次同步 origin/main（2026-11-09，PR #586 CI 失敗）

- CI 失敗原因：`rebuild.py --build` 回報 `REFUSED: sourceCount 265 != pinned source files 267`。CI 建的是 PR 和 main 的合併結果，而 main 在上次同步後又合入了 #556／#557／#559／#568 等任務，來源檔變多，分支上的 lock 跟著過期。這不是本任務程式碼的問題。
- `git merge origin/main`：唯一衝突是 `rebuild-lock.json`，先取 main 版，再用 `rebuild.py --update-lock` 重產（278 個來源檔／287 個鎖定檔）；唯讀核驗 `dev-inputs-verified`。`docs/REPORT.md` 沒有衝突，維持 main 版。
- 重新建置：`PASS dev`，productDll `5ba7e8d9…902b`。
- 重新測試：`native-ui-left-status` PASS 368（離線）／PASS 369（有 `TW_CLIENT_ROOT`）；`native-ui-canvas` PASS 429；`item-status` PASS 409；`check-no-game-assets.py` pass（trackedFiles 3823），errors 0。

### 真實客戶端渲染（只在 scratch，未進 git）

輸出在 `scratch/issue-464/real/`：4 種狀態的 `canvas-*.png`（800×600）、`left-x4-*.png`（左框放大 4 倍）、`surf-3168…3175.png`，以及 `left-status.json`（元件座標、surf 尺寸、每種狀態的繪製清單）。

與 `docs/reference/rea-scripts/ui_img_render.py` 的參考圖（`scratch/issue-464/ref/`）逐像素比對，色彩都換回 5-bit 再比：

| surf | 尺寸 | 比對結果 |
|---|---|---|
| 3168c 底圖 | 135×125，flags 1，key 0x324C | 16,605 點相同；270 點不同，全部是色鍵像素（flags≠0 時變透明，畫布上是黑色）；其他差異 0 |
| 3171c／3170c 陣營圖示 | 13×14 | 182/182 相同 |
| 3175c 負重 HIGH | 39×10 | 310 相同；80 點不同，全部落在 "100%" 文字矩形內；其他差異 0 |

真實 XML 讀出的元件座標（邏輯座標）：

| 元件 | 座標 | surf（真實尺寸） |
|---|---|---|
| MAIN_FRAME | (0, 475) | 3168（135×125） |
| LEVEL_BAR | (25, 501) | 3169（**101×10**） |
| LEVEL_NUMBER／EXP_PERCENT_A | (43, 504)／(78, 504) | — |
| ADENA_VALUE | (115, 523)，文字右緣對齊 | — |
| AC_NUMBER／MR_PERCENTAGE | (41, 544)／(105, 544) | — |
| CHAOTIC／LAWFUL／NEUTRAL_ICON | (10, 561) | 3170／3171／3172（13×14） |
| ALIGN_NUMBER／SP_NUMBER | (41, 564)／(105, 564) | — |
| LOW／MID／HIGH_GAUGE_BAR | (24, 582) | 3173／3174／3175（39×10） |
| FOOD_LOW／FOOD_GUAGE／FOOD_MAX_BAR | (87, 582) | 3175／3173／3174 |
| CARRY_GAUGE_NUMBER／FOOD_NUMBER | (41, 584)／(105, 584) | — |

目視檢查：空狀態下底圖的陣營格是空的；圖示、等級條、負重條與飽食條都正好落在底圖的槽位內。金幣列的金幣圖示本來就印在底圖 3168 上。

## 已驗證／推測／待驗證

- **已驗證**
  - 元件座標＝XML＋視窗原點，以真實 `uistatus86-c.xml` 讀出，結果如上表。
  - surf 尺寸（3169 是 101×10，T161 表寫 101×11，以 c 版實測為準）。
  - 渲染像素與參考腳本一致。
  - 負重 0–240 刻度（伺服器 `calcWeight240`）。
  - 顏色：3173 橘、3174 紅、3175 亮紅（實測）。
- **推測**
  - 陣營門檻沿用名字顏色規則 `sub_A406C0`：≥500 正義、<0 邪惡，其餘中立。
  - 負重分段：120（50％，伺服器停止回復的門檻）／240。
  - 飽食刻度 225（P16 滿飽食角色的實際值）；分段 <3 低、≥225 滿。
  - 條狀圖從左依比例裁切（無條件捨去）。
  - 數字格式：EXP 小數 2 位、負重與飽食顯示百分比、MR 加 `%`。
  - 文字白色加 1 px 黑影；金幣數字右對齊到 x=115；`EXP_PERCENT_B` (79, 29) 沒有使用（看起來像陰影或小數位）。
- **待驗證**
  - 上述各項門檻與格式在 BIN 中的實際規則（建議加進 V4 或另開查證）。
  - 文字的垂直位置：目前以 XML 點作為文字左上角，渲染圖中數字下緣比條框低約 2 px；這與 V4 (f) 字型問題相關。
  - 飽食 LOW 的實際門檻：照目前規則，<3 時條寬是 0 px，所以 LOW 圖永遠看不到，門檻很可能不對。
  - 金幣與巴洛格共用 (8, 42)，切換方式未查。
  - 真機上的顯示（需要 dot 的 ui-native-smoke）。

## 未驗證什麼

- 沒有啟動遊戲、沒有登入，也沒有在真機打開 `nativeUi`。`From(WorldSession)` 只做了編譯與型別層級的接線，沒有用真實封包跑過。
- 預設全組 demo-tests 跑到 `Movement123` 時，因為 worktree 沒有 `engine/assets` junction（找不到 `terrain0.bin`）而中斷。在那之前的 native-ui 三組已經通過；我沒有自己建立 junction。
- CI（`build.yml`）沒有加 `native-ui-left-status` 這一步，因為它不在本任務可改的檔案範圍。目前預設全組會呼叫它，但 CI 只跑指定分組。

## 產出路徑

- 程式：`engine/source/NativeUi/LeftStatusPanel.cs`、`engine/source/NativeUi/State/LeftStatusState.cs`
- 測試：`engine/_integration/dev/native-ui-tests/LeftStatusChecks.cs`
- 本報告：`docs/reports/issue-464.md`
- 本機證據（不進 git）：`scratch/issue-464/real/`、`scratch/issue-464/ref/`、`scratch/issue-464/*.json`

## 給下一棒的提示

1. 其他模組需要更多狀態時，可以照 `UiLeftStatusGameState` 的做法再包一層。如果多個模組同時改 bridge 的同一行，建議 C2 改成用一個 `UiClientGameState` 類別一次實作所有 `I…Source` 介面，避免一層包一層。
2. 分段門檻與文字格式都集中在 `LeftStatusState` 的常數與 `*Text` 屬性。V4 或後續查證確定後，只需改這一個檔。
3. 建議 CI 加一步 `native-ui-left-status`（不讀客戶端，可以在 ubuntu 執行），由調度器另開小任務處理。
4. 真機 smoke 時，請對照原版截圖核對：數字位置與格式、陣營圖示的門檻、負重顏色切換的時機。
