# #361／T144：座標分享

## 結論與做了什麼

**程式及本機檢查完成；已同步 main 並解除合併衝突，CI 綠燈（見下方「同步輪」）。** 已完成 C254 座標發送、S250 接收、聊天座標點擊與畫面標示。新檔透過 `ClientForm.OnShown` 及公開控制項集合掛接 UI，產品修改維持原定三個檔案，不需要擴充 `PlayerHudChat.cs` 的修改範圍。已提交並推送 `task/361`，PR：[#394](https://github.com/chaoren0418-arch/tw-bin-engine/pull/394)。

- 唯讀查證 `C_Windows`、結構檢查及回包 serializer；正確格式是 C254/sub11，傳給指定角色，並非公開頻道廣播。完整欄位、來源 SHA-256、zone 的證據限制見 [協定](../protocol/location-share.md)。
- 聊天框輸入 `/loc 角色名稱`，透過 `SendLocation` 發送目前角色 map/x/y；不接受任意座標。角色移動後的目前位置可能是既有引擎的本地預測，並非新取得的伺服器 ACK。
- 接收 S250/sub111，保存發件者、map/x/y/zone；以 `[座標] 名稱：地圖 … (x, y)` 顯示。S71/1783 與 1782 分別顯示成功及離線失敗；不產生虛構的本機座標回聲。
- 聊天座標加底線並可點擊：同圖標記實際投影位置，視野外顯示邊緣指示，跨圖只顯示目的地文字。換圖／換角／reset 清除標記。點擊不送出封包或改變角色／鏡頭；普通文字無法偽造連結。
- 共用聊天 sender gate、傳送中鎖及節流；排隊期間換圖、換角、死亡、動作鎖或位置改變皆拒送。其他 S250 與 S71 保持原分派。
- 測試提交至 repo，接入既有 `DemoChecks` 的 `location-share`、預設組及 CI 已執行的 `map-continuity` 組；未修改 workflow。

## 同步輪（2026-11-07，只做同步）

合併 `origin/main`（commit `beae9f0`），沒有改任何座標功能。對 main 的產品差異維持原範圍：`LocationShare.cs` 新檔、`PlayerChat.cs`、`WorldSession.cs` 兩個分派 case。

- `WorldSession.cs`：保留雙方分支。座標的 `case 250 when sub111`、`case 71 when 1782/1783` 排在 main 不帶條件的 `case 71:` 之前，所以 1782／1783 不會落到 `ReceivePlayerQueryMessage`。
- `demo-tests/Program.cs`：`map-continuity` 與預設組取聯集，並保留 main 新增的測試入口及 `location-share`。
- `rebuild-lock.json`：取 main 版後以 `--update-lock` 重產（189 個來源檔／198 個鎖定輸入），未手動拼 hash。

| 同步後檢查 | 結果 |
|---|---|
| `rebuild.py --build` | 通過 |
| `location-share-tests/run.ps1` | **189 項通過**＝座標 99 項＋main 帶入、在 switch 前固定執行的 `PolymorphChecks` 90 項 |
| `player-chat-tests/run.ps1` | 107 項通過 |
| `DemoChecks` 的 CI 組 | `map-continuity` 20832、`combat-admission` 368、`polymorph-cadence` 123、`buddies` 259、`item-transfer` 163 項通過 |
| `DemoChecks` 的共用分派組 | `player-query` 791、`character-config` 183 項通過 |
| `check-no-game-assets.py` | 通過，0 errors |
| `DemoChecks` 無參數預設組 | **失敗，未跑完**：`Movement123` 讀不到 `engine/assets/terrain0.bin`，因為本 worktree 沒有 `engine/assets` junction。失敗點在 `LocationShareChecks` 之前，此組的後續項目未執行；CI 不跑此組，座標項目已由上面的分組涵蓋 |
| CI（`beae9f0`） | **綠燈**：`dotnet`、`map-continuity`、`npc-offline` 全部通過，PR 為 MERGEABLE／CLEAN |

下方「已驗證」表是首輪（同步前）的紀錄，數字以本段為準。

## 已驗證

| 檢查 | 結果 |
|---|---|
| dev `rebuild.py --update-lock` | 183 個來源檔／192 個鎖定輸入通過；僅更新 dev lock |
| dev `rebuild.py --build` | product／audio／launcher 成功；產品 0 errors，既有 `MagicUi.cs:20` 兩項 nullable warnings |
| `location-share-tests/run.ps1` | **99 項通過**；真實產品 DLL、合成封包、記憶體 sender、未顯示的 UI 控制項，無遊戲入口／網路連線 |
| 既有 `player-chat-tests` | **107 項通過**，包含未顯示的聊天控制項回歸 |
| 本機 `DemoChecks ... map-continuity` | **457 項通過**，確認既有 CI 測試入口可執行本模組；不等同遠端 CI |
| `check-no-game-assets.py` | 通過，0 errors |
| CI | **未啟動／未驗證**。GitHub 回報 PR `mergeable=false`，本次 head 的 workflow runs 為空；無綠燈主張 |

離線測試涵蓋 C254 精確位元組與 CP950、S250 的全部欄位及整數邊界、S71 兩種回覆、每個封包長度的截斷、參數數量及 padding 拒絕、錯誤包不更新歷史、其他 subtype 分派不變、100 筆上限、reset、共用節流、舊世代／移動／死亡／麻痺拒送、無逐步 ACK 時使用目前位置。UI 測試在 STA 執行緒上使用真實 RichTextBox 滑鼠事件、底線與標記座標，涵蓋同圖、跨圖、視野外、普通文字防偽、過期顯示、世代清理及 dispose。

重現順序：

```powershell
python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock
python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config --output scratch/issue-361/build
powershell -NoProfile -ExecutionPolicy Bypass -File engine/_integration/dev/location-share-tests/run.ps1
python -B -X utf8 engine/tools/check-no-game-assets.py
```

本機 `python` 不在 PATH，使用已安裝的 Python 3.12 完整路徑。NuGet 線上還原遇到 TLS 認證錯誤，改用 `scratch/issue-361/nuget.config` 唯讀指向原專案 `engine/vendor`；套件快取、暫存、建置輸出全部留在 worktree。檢查 CRLF 來源的空白使用 `git -c core.whitespace=cr-at-eol diff --check`，保留原檔換行格式。

## 未驗證／未完成

- `DemoChecks` 無參數預設組在本 worktree 未跑完（缺 `engine/assets`，見「同步輪」）。
- 未啟動客戶端、遊戲、伺服器或 DB，未連任何遊戲服務。未驗證跨客戶端實際投遞與原生 UI 操作。
- zone 僅證明為透明轉送欄位；送出 0 的原客戶端顯示效果待 smoke，不能推斷為地圖分流或安全區。

## 產出路徑

- `engine/source/LocationShare.cs`、`PlayerChat.cs`、`WorldSession.cs`
- `engine/_integration/dev/location-share-tests/LocationShareChecks.cs`、`run.ps1`
- `engine/_integration/dev/demo-tests/DemoChecks.csproj`、`Program.cs` 測試掛接
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/location-share.md`、本報告
- 本機證據（不入 git）：`scratch/issue-361/location-share.json`、`player-chat.json`、`build/result.json` 與建置紀錄

## 給下一棒的提示

1. 之後若再同步 main：`WorldSession.cs` 的座標 `when` 分支必須留在不帶條件的 `case 71:` 之前；dev lock 一律用 `rebuild.py --update-lock` 重產。
2. 好友模組使用 `OnLoad`、本模組使用 `OnShown`，生命週期擴充可共存。
3. 原生雙角色投遞與 zone 互通由 dot 依既有 P24Lab 授權於後續 smoke 驗證。
