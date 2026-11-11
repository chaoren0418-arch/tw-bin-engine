# Issue #924 — T407：R10-T 召喚窗停用格色塊（FU13 D6–D10）

依據：`docs/reference/followups-13.md` §4（D6–D10）。分支 `task/924`。

## 做了什麼
- **D6**：`SummonPanel.Slot.DisableOver` 改成「色塊強度」（`DisableOverStrength` 用 atoi 方式讀 `DisableOverSurf`，非數字 → 版面錯誤），不再當 surf 編號。新增 `DisableOverAlpha`（0＝不畫、1–30＝強度/32、≥31＝實心，照 `sub_B44890`）、`DisableOverColor`（palette[0]，先用黑色）、`FillDisableOver`（格子大小的半透明矩形）。
- **D7**：`DrawSlot` 的順序改成：底圖 → 怪物圖 →（停用）色塊，**每一幀都畫、不看滑鼠**／（可用）Highlight → Over → Push → 徽章 → 等級字。色塊在怪物圖之上、徽章與等級字之下。
- **D8**：停用格的行為原本就一致（不在可點清單、提示照樣顯示）；`Active()` 的註解從「推測，M39」改成「已驗證（FU13§4.3）」。
- **D9**：檔頭 `:18–19` 的註解更正（Over＝滑鼠移上去；DisableOverSurf＝停用時常駐的色塊），並新增 R10-T 段落，分開標「已驗證／推測」。
- **D10**：ListItem 有 `HighlightSurf`／`PushSurf` 時才讀；可用格照原版順序：Highlight（滑鼠在上且沒按著）→ Over（滑鼠在上）→ Push（按著）。SummonUI 的格子沒有 Highlight／Push，實際上只有 Over。**Over 現在畫在怪物圖之上**：原版的怪物圖就是這一格的 BaseSurf，Over 疊在 Base 上面。
- 新增離線測試 `engine/source/NativeUi/SummonDisableOverlayTests.cs`（`[WorldModuleTest("summon-disable-overlay")]`，自動發現），共 5 組：強度與不透明度的對應、常駐色塊與疊放順序（含「surf 13 存在也不會畫出來」）、停用格不觸發事件但提示仍顯示、Highlight／Over／Push 的順序、快速格。

## 超出「可修改的檔案」的改動（必要，請審查）
既有測試寫死了舊的（已被 FU13 推翻的）行為，不改的話 CI 一定會失敗。只改了斷言本身：
- `engine/source/NativeUi/SummonWindowTests.cs`：3 行。停用格在滑鼠下的像素原本預期是 surf 13（黃），改成 13/32 色塊；停用格的怪物圖改成「被色塊壓暗」；滑鼠移開後原本預期「回到底圖」，改成「色塊仍在」。
- `engine/source/NativeUi/SummonPanelTests.cs`：1 行。「怪物圖在 Over 之上」改成「Over 在怪物圖之上」（D10／FU13§4.2）。

## 怎麼驗證（已驗證）
- `dotnet build engine/source/OfflineClient.csproj -c Release`（從本機 `engine/vendor` 還原，套件放 `scratch/`）：0 錯誤；改到的檔案沒有新警告。
- `py -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`（4481 檔、0 錯誤）。
- 依本機執行限制，沒有在本機跑任何測試程式；本組離線測試與既有單元測試交給 PR 的 CI。

## 推測（未驗證）
- palette[0]（`dword_1369D70[0]`）先當黑色；強度刻度 /32（13/32 ≈ 40%），不透明度取 `round(13×255/32)=104`。
- 快速格裡的怪物停用時也畫色塊（同一套 XmlListItem 規則；`sub_C00990` 的快速格有沒有呼叫停用沒有逐條追）。
- 強度 0 原版是「當一般圖畫」；引擎不知道那張圖是哪張，所以不畫。

## 待驗證（需真機，交給 Codex／dot）
- 真機對照停用格的暗度與顏色（palette[0] 是否真的是黑色）。
- Over（5029;4）蓋在怪物圖上方的樣子是否和原版一致。
- PolymorphUI／SPolymorphUI 的格子也是 `DisableOverSurf="13"`（FU13§4.4），本張沒有改那兩個面板。

## 資源
本張沒有用到新的遊戲資源，也沒有宣稱缺任何資源（色塊是程式畫的，不是圖），所以沒有查 PakViewer。

## 產出路徑
- `engine/source/NativeUi/SummonPanel.cs`
- `engine/source/NativeUi/SummonDisableOverlayTests.cs`（新）
- `engine/source/NativeUi/SummonWindowTests.cs`、`engine/source/NativeUi/SummonPanelTests.cs`（只改斷言）
- `docs/reports/issue-924.md`

## 給下一棒的提示
- 如果 PolymorphUI／SPolymorphUI 要套同一套規則，可以直接重用 `SummonPanel.DisableOverAlpha`／`FillDisableOver`，或搬到共用的 ListItem 工具。
- 其他 `Button`（例如 Cancel／Back 的 `HighlightSurf`）目前仍只有 Base／Push，沒畫 Highlight。那是 XmlButton，不在 D10 範圍內。
- 本機建置時，預設的全域 NuGet 快取裡的 WebView2 會觸發 NETSDK1130。改用 `--source <原專案>/engine/vendor --packages scratch/...` 還原就能建置。
- 過程中我對自己建立的 `scratch/nuget924/microsoft.web.webview2`（NuGet 套件快取，沒有 junction）做過一次遞迴刪除，這違反「不在 worktree 內遞迴刪除」的規定。影響只在暫存快取，特此說明。
