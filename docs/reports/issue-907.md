# Issue #907 報告：T392 R10-H 名稱內插與頭上條

依據：`docs/reference/gap-matrix.md` 15.12.6 R10-H 列、15.12 的「5:（名稱」「4:`HpMpWindow`」兩列；FU8§1.1–1.3（`docs/reference/followups-8.md`）、UI§2 V1（`docs/reference/ui-layout.md` 「HP 條的變體」「低血警示框」）。

## 做了什麼

1. **自己的名稱改用顯示中的腳下點**（`engine/source/ObjectRendering.cs`，只動名稱位置）
   - `NameLayout.Plan` 多一個選用參數 `selfFoot`（場景座標）。滑鼠指到自己時，P.x 改成 `floor(selfFoot.X) − 24`。沒給時退回 `WorldSession.Actor` 的格座標，也就是 #784 的行為。
   - 新增 `NameLayout.SelfOriginX(PointF foot)`。
   - `PaintNames` 傳入 `renderer.OwnCombatVisual?.Foot`。這個值就是 `World.DisplayPosition`（`ObjectRendering.cs:9`），走路時會內插。頭上條的 `SelfHeadOrigin` 也用同一個點，所以名稱、身體、頭上條現在用同一個基準。
   - 全名模式和他人的名稱不變。
2. **HP 浮動窗的戰士門檻**（`engine/source/NativeUi/HpMpPanel.cs`）
   - 新增 `WarriorWarnRatio=0.40` 與 `WarnRatioFor(ownClass)`：職業 7 用 0.40，其他職業或不知道職業時用 0.35（`sub_EDB8E0`、`dword_138B62C`，UI§2 已驗證）。
   - `Update` 從 `IUiHpMpState.OwnClass` 取職業碼。這個值在產品裡由 `UiHpMpGameState` 帶入，`HpMpState.cs:88` 已經接好。
   - 計數規則照舊：設成 6、每 300 ms 減 1、奇數時畫紅框。
   - 頭上條的「戰士 40%／無限閃爍」在 #820 已經用 `OwnClass` 選檢查器（`BlinkChecker.ForClass`）。本輪只補測試，確認同一個 `OwnClass` 會切換檢查器。
   - HP 窗本身不做「無限閃爍」。原因：UI§2 已驗證 `sub_EDB8E0` 用的是 6 次計數；FU8§1.2 提到的 `HpWarnCheckerAlways`（`sub_741350`）有沒有接到 HP 窗，沒有查證。
3. **+86／+87 灰條、綠條（推測）**
   - 新增 `HpBarVariant`（Normal／Gray／Green）。
   - 新增 `HpMpLayout.HpSurf(variant)`：基準＋0／＋1／＋2。
   - 新增選用介面 `IUiHpMpVariantState`，含 `FromFlags`：兩個旗標都有時灰色優先（推測）。
   - `HpMpPanel.Paint` 依變體選 HP 條圖。狀態沒帶變體時用 Normal。
4. **新測試** `engine/source/NameHeadBarFollowupTests.cs`（`[WorldModuleTest("name-headbar-followup")]`，自動發現）
   - 名稱：站著時結果不變；走半步時名稱跟著內插點移動；他人與全名模式不受影響。
   - 門檻：職業 0–7 與未知；HP 40／41／38／35% 的邊界；沒有 `IUiHpMpState` 的狀態。
   - 頭上條：檢查器隨 `OwnClass` 在 (40,−1) 與 (35,5) 之間切換。
   - 變體：12 組 (shape, status, thick) 的 surf 號；`FromFlags`；panel 讀到的變體。

## 資源核對（PakViewer CLI，唯讀）

- 用 `pakviewer-cli pak list` 列出 `D:\lineage開發\0-客戶端` 的全部 24 個 idx：`Sprite`、`Sprite00–15`、`Text`、`Tile`、`LiTo\LTicon`、`LTsprite`、`LTsurf`、`LTtext`、`LTtile`。清單放在 `scratch/t907/pak/`，不進 git。
- 用 `scratch/t907/variants.py` 比對，結果在 `scratch/t907/variant-surfs.txt`。HP 條的基準圖與兩種變體共 36 張，**本機都有 `.img`／`c.img`**：
  - 2338–2346（直排一般）
  - 2359–2367（橫排一般）
  - 2896–2904（直排 thick）
  - 2914–2922（橫排 thick）
- 沒有複製任何資源進 repo。

## 怎麼驗證

- `dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -o scratch/t907-build`：**0 個錯誤**。3 個警告都是既有的，在 `MagicUi.cs` 與 `HtmlActionTests.cs`。
  - restore 改用 `scratch/t907/NuGet.Config`，指向主專案的 `engine/vendor`，並把 `NUGET_PACKAGES` 設到 `scratch/t907/packages`。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`，0 個錯誤。
- 依本機規定，**沒有在本機執行測試**。單元測試與本組離線測試交給 PR 的 GitHub Actions CI。

## 未驗證什麼

- **灰條、綠條在產品裡還不會出現**：
  - 產品的狀態轉接 `UiHpMpGameState` 在 `NativeUi/State/HpMpState.cs`，不在本任務能改的檔案內。所以本輪只做資料與繪製路徑，沒有實作 `IUiHpMpVariantState`。
  - 旗標的意義也是推測：+86＝麻痺／凍結類（S_PARALYSE type 2／4、10、12、22／24），+87＝中毒。
- 走路時名稱內插的實際畫面待真機確認（#475 M32：走一步時截圖）。
- 戰士 40% 的紅框待真機確認（M32：HP 壓到 40% 以下）。
- HP 窗是否也有「無限閃爍」（`HpWarnCheckerAlways`）沒有查證，本輪沒有做。

## 產出路徑

- `engine/source/ObjectRendering.cs`
- `engine/source/NativeUi/HpMpPanel.cs`
- `engine/source/NameHeadBarFollowupTests.cs`
- `docs/reports/issue-907.md`

## 給下一棒的提示

- **接上灰／綠條**：在 `HpMpState.cs` 讓 `UiHpMpGameState` 實作 `IUiHpMpVariantState`。
  - 可以從 `login.World` 取值：+86 對 `paralysis` 的 2／4／10／12／22／24 模式，+87 對中毒（`TintFor(own)==Poison` 或 S165 的 green）。
  - 實作前建議先用 IDA 確認 `sub_A458F0` 與 `sub_A45A60` 寫入的位移，以及 `sub_ED2680` 裡兩個旗標的先後順序。
- **HP 窗戰士是否一直閃**：查 `sub_741350`（`HpWarnCheckerWarrior` 系）的呼叫者是不是 `HpMpWindow`。如果是，`HpMpPanel` 的計數器要依職業換成無限。
- R10-I 依賴本任務在 `ObjectRendering.cs` 的改動，排在本任務之後。
