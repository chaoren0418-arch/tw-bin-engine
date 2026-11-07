# Issue #450（T176）：快捷列改為 F5–F12 共 8 格，小地圖不再佔用 F5

階段：code，第 1 次嘗試。分支：`task/450`。依據：`docs/reference/ui-layout.md` 第 7 節 #5、#12。

## 做了什麼

- **8 格快捷列（F5–F12）**
  - `PlayerHudModel.cs`：新增 `PlayerHudKeys`（格數 8、F5..F12 對應槽 0..7、標籤、焦點閘門）。`PlayerHudSlots` 改為 8 格；預設前 4 個可用道具放 F5–F8，前 4 個可用技能放 F9–F12。
  - `PlayerHudView.cs`：8 個快捷格，標籤 F5–F12，寬度依格數平均分配（最小 64 px）；提示文字改成 F5–F12；選單「診斷 F9」改為「診斷」。
  - `PlayerHudIntegration.cs`／`Program.cs`：`ProcessCmdKey` 一開始先交給 `ProcessPlayerHudKeyboard`。條件是在世界中、遊戲畫面有焦點、沒有按修飾鍵，這時 F5–F12 執行對應快捷格，按住不連發（看 lParam 的 repeat bit），而且優先於 F9／F10／F11。`HandleDown` 的 `TryPlayerHudKey` 也改成 F5–F12（以 held set 防連發）；舊的 F1–F4 在世界中不做事，也不會落入移動處理。
- **小地圖不再用 F5**：`MinimapUi.cs` 刪掉 `MinimapKeyAllowed`／`ProcessMinimapKeyboard`；`KeyboardNavigation.cs` 移除對應呼叫。選單按鈕改名「小地圖」，提示改成「由選單按鈕開關」。
- **伺服器存取（沿用 T138）**：`CharacterConfig.cs` 的 TWCC v1 改成 count＝8（46 bytes）。讀取仍接受 T138 的 count＝4（26 bytes），還原到 F5–F8；格式不符時照舊保留原 blob、不覆寫。C244／S250／41 的封包格式沒變，分派點沒有新增 case。
- **文件**：`docs/protocol/character-config.md` 附加 T176 段（新布局、相容規則、按鍵分派），並把 protobuf `Lineage.ClientConfig` 記為**待查**。
- **測試（已提交）**：新增 `engine/_integration/dev/hotbar-tests/HotbarChecks.cs`，接到 `demo-tests/DemoChecks.csproj`。新模式 `hotbar`；CI 跑的 `map-continuity` 與預設入口也會執行。另外更新了 `CharacterConfigChecks.cs`（8 格固定位元組、C244 長度、S250／41 兩種尾長、舊 4 格還原、count／長度不符）、`MinimapChecks.cs`（F5 不再開小地圖、選單按鈕）、`world-ui-tests/Program.cs`（技能預設槽位置）。

## 怎麼驗證

| 項目 | 結果 |
|---|---|
| `rebuild.py --update-lock` | `dev-inputs-verified`，219 個來源檔／228 個鎖定檔 |
| `rebuild.py --build --configfile .github/nuget.ci.config` | PASS，product／launcher 都建置成功（productDll `04b97f8b…`） |
| `DemoChecks … hotbar` | PASS 518 checks（快捷列、角色設定、小地圖） |
| `DemoChecks … map-continuity`（CI 步驟） | PASS 23052 checks |
| `check-no-game-assets.py` | pass，errors 0 |
| CI（PR #487） | dotnet、map-continuity、npc-offline 全部 pass |

封包都用合成資料：C244 編碼（8 格與舊 4 格重寫）、S250／41（直接尾長與回送補齊尾長），以及截斷、子碼錯誤、溢位、非零尾段、超限、count 與長度不符。

## 沒驗證什麼

- **沒有真機**：沒有啟動遊戲，也沒有實際按鍵驗證 F5–F12 的 Windows 訊息路由（尤其 F10 是 WM_SYSKEYDOWN）。離線測試只測到 `TryPlayerHudKey`、焦點閘門與 HUD 控制項，沒有測到 `ProcessCmdKey` 的實際訊息。
- 沒有驗證伺服器是否真的存進 46 bytes（伺服器不回 ACK，T138 的限制照舊）。
- `world-ui-tests` 在本 worktree 缺少 `engine/assets`；改用主專案素材唯讀執行時，在 `Lifecycle` 的 S76「pending appearance clears selection」失敗。這段在 HUD 測試之前，與本次修改無關；這組測試不在 CI 內，也沒有在 main 上重跑確認是不是本來就壞。
- DemoChecks 預設入口（不指定模式、需要素材）：用主專案素材唯讀執行，跑了超過 20 分鐘還沒結束，已手動停止，沒有拿到結果。在本 worktree（缺素材）執行時，執行流程已經過 `HotbarChecks`，直到後面的 `Movement123` 讀素材才停下。
- protobuf `Lineage.ClientConfig` 的欄位與 3 個分頁：**待查**，不改協定。
- 巨集框外觀與 3 個分頁屬於 T174，本張不做。

## 行為變更（給審查者）

- 在世界中且遊戲畫面有焦點時，F9 不再開診斷面板（改用選單「診斷」按鈕），F11 不再跳到記事欄，F12 在證據模式下也不再手動截圖；這三鍵都變成快捷格。焦點不在遊戲畫面（聊天、WebView）或還沒進入世界時，維持原有功能。
- 離線 fixture 模式的 F2／F3／F6／F7／F8 不受影響（只有 `PlayerHudActive` 時才生效）。

## 產出路徑

- 程式：`engine/source/{PlayerHudModel,PlayerHudView,PlayerHudIntegration,CharacterConfig,MinimapUi,KeyboardNavigation,Program}.cs`
- 測試：`engine/_integration/dev/hotbar-tests/HotbarChecks.cs`、`character-config-tests/CharacterConfigChecks.cs`、`minimap-tests/MinimapChecks.cs`（及 README）、`world-ui-tests/Program.cs`、`demo-tests/{DemoChecks.csproj,Program.cs}`
- 文件：`docs/protocol/character-config.md`（附加段）、本報告
- Lock：`engine/_integration/dev/rebuild-lock.json`

## 給下一棒的提示

- T174（巨集框外觀／3 分頁）：槽位常數集中在 `PlayerHudKeys.SlotCount`。若要做 3 分頁 × 8 格，TWCC 需要新版本（例如 version 2 加分頁數），讀取端要保留 v1 count 4／8 的相容路徑。
- 真機 smoke 請確認：F5–F12 各執行一次、按住不連發；在聊天輸入框裡按 F9／F10 仍是原功能；選單「小地圖」可以開關。
- 待查：從客戶端 IDB 追 `Lineage.ClientConfig` 的 protobuf 描述（欄位編號、分頁），結論補進 `docs/protocol/character-config.md`。
