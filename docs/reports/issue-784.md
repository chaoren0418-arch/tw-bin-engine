# Issue #784 報告：T338 R6-B 自己的名稱位置

階段 code，第 1 次嘗試。分支 `task/784`。

## 做了什麼

- `engine/source/ObjectRendering.cs`（只動 `NameLayout`）：
  - 新增 `SelfOriginX(x,y)`＝`Coordinates.Screen(x,y).X − 24`（P＝`sub_F1A750(obj+16)`＝精靈區塊偏移原點，和 `CrownLayout` 同一約定，FU8§1.1）。
  - 新增 `SelfX(P.x,text)`＝`P.x − 56 − 3·len`（`sub_EFF300` 自己分支 `lea ecx,[edx+eax−38h]`，800×600 下 `dword_154524C`＝0；AN§2.3）。
  - `Hover(...)` 多一個選用參數 `selfOriginX`：有值時名稱、稱號都用 `SelfX`；盟徽仍是名稱 X0 − 20、有血盟時名稱 ＋10，所以三者一起移。Y 不變（仍照外框 T）。
  - `Plan(...)`：滑鼠指到的目標是自己（`NameTarget.Self`）而且有 `WorldSession.Actor` 時，傳入 `SelfOriginX(Actor.X, Actor.Y)`。
  - 沒改：他人（`L + (W − 6·len)/2`）、全名模式（`sub_F01380` 沒查到自己分支）、頭上盟徽、圖示（跟著名稱位置走）。
- 新檔 `engine/source/SelfNameLayoutTests.cs`（`[WorldModuleTest("self-name-layout")]`，自動發現）：
  - 純公式：P.x、ASCII／Big5 的 len、任何長度都以 P.x − 56 為中心；
  - `Hover`：自己的名稱／稱號／盟徽 X 不隨外框變、Y 照外框；他人不變；
  - 合成 S87 走完 `Plan`：自己有稱號＋血盟時三者的位置、換外框後 X 不變、頭上盟徽不變、他人與全名模式不變。

## 怎麼驗證

- `dotnet build engine/source/OfflineClient.csproj -c Release`（本機，restore 來源指向原專案 `engine/vendor`）：0 錯誤。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`status: pass`，errors 0。
- 依規定**沒有在本機執行**測試；`self-name-layout` 與既有單元測試交給 PR 的 GitHub Actions CI。
- 沒改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

## 未驗證什麼

- **走路中的位置**：自己的腳下格取自 `WorldSession.Actor.X/Y`（不內插）。身體在走路時照 `World.DisplayPosition` 內插，所以走一步的過程中，名稱可能比身體早或晚到下一格（最多一格，約 24 px）。要改成內插，必須讓呼叫端（`ObjectRendering.cs` 的 `NameTargets()`／`Plan` 呼叫，在 `NameLayout` 外）傳入 `own.DisplayPosition`，超出本張範圍。原版的相機每步和角色一起移，P 固定在 (456, 288)，所以原版的名稱不會在畫面上移動。
- **相機原點**（FU8§1.1 待驗證）：原版腳下點在 (480, 300)，引擎把腳下點放在畫面中央；絕對位置可能差 80 px 左右，屬相機範圍，要真機截圖（M14／M15、M32）。
- 稱號只在 `0x15442E4` 非 0 時畫（推測）：引擎照舊有稱號就畫，本張沒動。
- 全程沒有真機觀察。

## 產出路徑

- `engine/source/ObjectRendering.cs`（`NameLayout`）
- `engine/source/SelfNameLayoutTests.cs`（新）
- `docs/reports/issue-784.md`（本檔）

## 給下一棒的提示

- 若真機看到走路時自己的名稱跳格：開一張小 code 任務，讓 `NameTargets()` 為自己帶 `own.DisplayPosition`（`NameLayout.Plan` 可加選用參數 `PointF? selfFoot`），`SelfOriginX` 改吃浮點腳下點。
- 頭上條（FU8§1.5，`HpMpHeadBarPanel.Anchor` 的 x＝P.x − 80）可直接用 `NameLayout.SelfOriginX` 取 P.x，兩者就會對齊同一條中線。
- 真機核對：自己的名稱中線＝P.x − 56，頭上條中心＝P.x − 58（376＋22＝398 對 400）。
