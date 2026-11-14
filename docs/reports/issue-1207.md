# #1207：頭上 HP／MP 條改用實際角色外框與原生畫布座標

## 結論與做了什麼

已修正產品把自己的 `OwnCombatVisual.Foot − (24,12)` 當成原版特殊 P、再減 80 的定位路徑。自己與他人的頭上條現在都從實際 actor tiles 的外框投影到 WorldRect，使用既有原版外框規則：**外框上緣中心 + (−24,−12) 個 logical pixels**。量表寬度仍為 44、MP 在 HP 下方 4 pixels；鏡頭縮放只影響身體與外框投影，量表大小與偏移維持 UI 畫布單位。

這是引擎座標適配修正，不是改寫原版反編譯結論。明確提供原版 P 的 `HpMpHeadTarget.Origin`／`Anchor` API 及既有 FU8 檢查保留；產品不再用引擎腳下點製造這個 P。自己的外框會隨動作改變，是否有可見的輕微左右跳動要在重測時觀察。

## 已驗證：靜態追查

- 基線：`origin/main`／`c7a655e2`。main 未含 #1186 報告，故讀取 `origin/task/1186` 的 `issue-1186.md` 與 `issue-1186-evidence.json`；分流沿用 main 的 `issue-1201.md`。
- 唯讀查看 `environments/p24/backups/issue-1186-evidence/smoke-A3/normal-mage-ready.png`，SHA-256 `cf8dae9f825641985d0a7a85888634323b3b8183e1a2a7378b53f6b405270aa8`。PNG 未複製或加入 git。
- `Renderer.ComposeObjects` 以最終 `Draw.Px/Py` 的 actor tiles 建立 `ActorBounds`；`NameTargets()` 讀相同外框。站立與走路都應追隨這個顯示影格，而非另以腳下內插點推導特殊原點。
- 身體繪製：`PaintScene` 設定 `Camera.View/ViewOffset = viewport.Size/Location`，用 `Camera.ToView` 投影場景，再依 Zoom 畫 body patch。
- 舊頭上條：Box 也投影，但自己改用 `floor(Camera.ToView(foot−(24,12)).X)−80`。預設 Zoom=1.5、跟隨鏡頭且 viewport=800×600 時，x=`400−36−80=284`，吻合證據量表約 x=285 的位置；算式不含 DPI。Y 原本就是外框上緣−12，不應拿身體中心 Y 與量表 Y 的比例判定縮放錯誤。
- `followups-8.md` §1.1 已記載原版 P=(456,288)、x=376，也明載原版與引擎相機／腳下基準可能差 80 pixels 的待驗證事項。原版自己的 P−80 不能直接視為「引擎腳下投影後再減 104」。本張採用 `ui-layout.md` §2／FU8 的外框定位規則，避免另加目測校正值。
- 狀態收集順序為 `NativeUiGameState → UiRoot.Update → Compose → PaintNativeWorld → PaintScene`。因收集早於 `PaintScene` 設 viewport，新增 `Camera.ToView(point, viewport)` 共用投影公式，頭上條明確傳 `UiRoot.WorldRect`，不依賴舊 client viewport，也不修改鏡頭狀態。
- 畫布最後由 `UiPresenter.Present` 一次縮放；DPI 不參與這段座標計算。量表 surface 透過 `DrawBitmap` 指定像素來源與目的矩形。這與另一張字型 DPI 任務分開處理。

### 推測與適用界線

舊公式與截圖的 x 高度吻合，足以解釋主要水平分離；本輪沒有用修正版啟動遊戲，因此沒有宣稱 SM11 真機通過。新增顯式 viewport 可消除初次切換／截圖／尺寸改變時使用舊鏡頭尺寸的風險；沒有證據把這個時序因素指認為 #1186 當下的主因。名稱原點、鏡頭跟隨、身體繪製與縮放行為本身未調整。

## 怎麼驗證

本機只編譯，未執行任何產出的程式。以下兩項退出碼都是 0：

```powershell
dotnet build engine/source/OfflineClient.csproj -c Release -o engine/build/client-product-dev --configfile .github/nuget.ci.config
dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --configfile .github/nuget.ci.config
```

- 產品：0 errors、4 warnings，位於未修改的 `HtmlActionTests.cs`、`MagicUi.cs`、`MapResources.cs`。
- DemoChecks：0 errors、3 warnings，位於未修改的 `DisplayNoticeChecks.cs`、`MacroChecks.cs`。
- 本機編譯 log：`scratch/build-product.log`、`scratch/build-checks.log`（不進 git）。CLI home、NuGet packages、TEMP/TMP 都指定本 worktree 的 scratch；同時設定 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`。
- `git -c core.whitespace=cr-at-eol diff --check` 通過（保留既有來源 CRLF）。
- 新增 `[CheckGroup("headbar-coordinates")]`，同時掛在既有 CI 會執行的 `native-ui-canvas` 群組；無需修改 workflow、Program.cs、DemoChecks.csproj 或註冊表。CI 結果以本 PR 最新 commit 的 checks／RESULT 為準。

新檢查以合成 actor tiles 與 surf 驗證以下內容，合成畫面只用於回歸，不能代替真機證據：

| 維度 | 檢查內容 |
|---|---|
| 對象／動作 | 自己與他人；站立、步行中、下一格的實際 Draw 外框 |
| 鏡頭 | Zoom 1、1.5、2；非整數 Center；狀態收集時故意保留錯誤 View/ViewOffset |
| 世界範圍 | WorldRect=(0,0,800,600) 與 (37,29,700,520)；確認轉換不改相機 |
| 固定偏移 | 量表原點相對投影後的身體外框上緣中心為 (−24,−12)，步行位移相同 |
| 條件／像素 | HP/MP 位置、他人只有 HP；自己低血閃爍 overlay 原點不動 |
| 呈現 | 實際 Graphics DPI 96／192；800×600、1600×1200、1100×800 非整數 fill，量表與身體通過同一次呈現變換 |

## 未驗證什麼／給下一棒的提示

交 `f1186-retest` 的授權真機執行器驗 SM11：map0 普通法師正常進世界，分別站立與走一步；自己與可見他人的量表應在各自外框上方。再驗低 HP 閃爍、Zoom 1／1.5／2、非整數 fill、DPI 96／192，觀察步行影格是否產生可見抖動。比對時使用角色外框上緣中心，不能拿人物中心 Y 與量表 Y 比例作為尺度證據。

本輪沒有開 GUI、啟動遊戲、伺服器或 DB；沒有執行 DemoChecks、dotnet run／test 或產品程式。沒有宣稱缺少素材，也沒有修改 lock、docs/REPORT.md、正式客戶端、原始專案或封存。

## 產出路徑

- `engine/source/NativeUi/State/HpMpState.cs`：產品統一使用實際外框與明確 WorldRect。
- `engine/source/World.cs`：共用顯式 viewport 投影公式，舊 ToView 行為不變。
- `engine/source/ObjectRendering.cs`：移除頭上條仍共用 SelfHeadOrigin 的過時註解。
- `engine/_integration/dev/native-ui-tests/HeadBarCoordinatesChecks.cs`：自動發現的座標／像素回歸。
- `docs/reports/issue-1207.md`：本報告。

來源 SHA-256（本工作樹 bytes）：`HpMpState.cs`=`4d52d8b6a23d371865900952f14b211c53f55bbbe48a33032e2f0f0b412181a1`；`World.cs`=`a15d9f0c02681136d24f4b906cd266a403e1af247da8bb7e05b5cdf2e4caffbc`；`ObjectRendering.cs`=`753d8117183f70a5d47b95c6674de187a8b5140a159d4f44f4fb993b4b58d6ab`。
