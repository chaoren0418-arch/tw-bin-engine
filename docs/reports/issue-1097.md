# Issue #1097 f475-ui-fail-message：原生 UI 退回舊介面的紅字分類

階段 code，runner claude-local，分支 `task/1097`。來源：#475 SM01、分流 #1094；與 #1093（SPELL_WINDOW 幾何）並行，本張不碰 `SpellWindowPanel.cs`。

## 做了什麼

根因：`UiLayoutException` 繼承 `UiResourceException`，而 `UiClientBridge.ApplyNativeUiStart` 只判斷 `error is UiResourceException`，所以面板版面錯誤也顯示成「原客戶端 UI 資源與鎖定版本不符」。

- `engine/source/NativeUi/UiRoot.cs`
  - 新增 `UiRoot.DescribeStartFailure(Exception?) → UiStartFailure(Category, Message)`（純函式，可離線測試），分三類：
    - `UiLayoutException` → `layout`：「原生 UI 版面載入失敗（<檔名>:<視窗>）：<原因>，已使用舊介面」。節點路徑和視窗 Id 不同（例如 `Root/Window/Items_List`）時，原因前面會加上節點路徑。
    - 其他 `UiResourceException`（pin／錨點／idx／容器驗證）→ `pin`：維持 `PinMismatchMessage`。
    - 其他例外（含 null）→ `other`：維持原通用訊息（抽成常數 `GenericFailurePrefix`）。
  - `UiRoot.Build`：面板 `Load` 拋出 `UiLayoutException` 時補上 `PanelId=reg.Id` 後**原樣重拋**（`throw;`），全有或全無的行為不變。
- `engine/source/NativeUi/UiLayout.cs`：`UiLayoutException` 加上 `Reason`（不含「檔名:路徑:」前綴的原因）與 `PanelId` 兩個屬性；Message 格式不變。
- `engine/source/NativeUi/UiClientBridge.cs`：紅字改用 `DescribeStartFailure`；`window-events.json` 的 failed 事件加上 `category`（pin／layout／other）；`native-ui.log` 那一行加上分類：`native-ui fallback (<category>): ...`。
- 新測試 `engine/_integration/dev/native-ui-tests/StartFailureChecks.cs`（`[CheckGroup("native-ui-start-failure")]`，也掛進 CI 已經在跑的 `native-ui-canvas` 群組（Order=50）和 Default（Order=44））：
  - (a) 用合成的 Tile／Sprite 封包和 `uimagicitem-c.xml`，讓仿 SM01 的面板在 `Load` 時拋出「魔法清單沒有空間」，經過 `UiRoot.Build` → 分類 `layout`，訊息完全等於「原生 UI 版面載入失敗（uimagicitem-c.xml:SPELL_WINDOW）：魔法清單沒有空間，已使用舊介面」，而且不含「鎖定版本」；另外測了 XML 解析錯誤（只有檔名）和深層節點路徑兩種情況。
  - (b) Sprite 錨點不符，經過 `UiRoot.Build` 拋出一般的 `UiResourceException` → 分類 `pin`，訊息等於原本的 pin 訊息；容器缺 idx 的情況也歸 `pin`。
  - (c) `InvalidOperationException` → 分類 `other`，訊息是通用訊息；null 也歸 `other`。

沒改 `Program.cs`、註冊表、`DemoChecks.csproj`、`rebuild-lock.json`、`SpellWindowPanel.cs`。#1093 在本張開工時尚未改到這三個檔（`origin/main` 與本分支起點相同）。

## 怎麼驗證

- 已驗證（本機只編譯）：`dotnet build source/OfflineClient.csproj -c Release -o build/client-product-dev` 與 `dotnet build DemoChecks.csproj -c Release` 都是 0 個錯誤。依本機限制，沒有在本機執行測試。
- 測試結果以 PR 的 GitHub Actions CI 為準（`native-ui-canvas` 群組步驟）。

## 未驗證什麼

- **待驗證（交 f475-retest，SM01）**：真機畫面紅字。#1093 修好之前，用 `engine\啟動新版客戶端.cmd --evidence <證據目錄>` 重現時，紅字應該是「原生 UI 版面載入失敗（uimagicitem…xml:SPELL_WINDOW）：魔法清單沒有空間，已使用舊介面」，`window-events.json` 的 failed 事件應該有 `"category":"layout"`。#1093 合併之後就不會再走到這條退回路徑，屆時只能靠離線測試涵蓋。
- 真實 XML 的檔名是 `uimagicitem-c.xml` 還是 `uimagicitem.xml`，取決於 Tile.pak 實際讀到哪一個；訊息會照實際的檔名顯示。

## 產出路徑

- `engine/source/NativeUi/UiRoot.cs`、`UiLayout.cs`、`UiClientBridge.cs`
- `engine/_integration/dev/native-ui-tests/StartFailureChecks.cs`
- 本報告 `docs/reports/issue-1097.md`

## 給下一棒的提示

- 新的退回原因請在 `UiRoot.DescribeStartFailure` 加分類，不要在 bridge 裡直接比對例外型別。
- 已知限制（不在本張範圍）：Build 仍然是全有或全無，任何一個面板版面出錯都會讓整個原生 UI 退回舊介面（`docs/design/native-ui.md`，見 #1094 報告）。
- 驗收者看到 `category=layout` 時，請去查對應面板的排版（例如 #1093），不是去查資源 pin。
