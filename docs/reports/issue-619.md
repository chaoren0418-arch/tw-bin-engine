# Issue #619 — T258：R2-M 發送座標收尾

依據：`docs/reference/gap-matrix.md` 11.5 節 R2-M；`docs/reference/world-objects.md` 第 5 節（WO §5）。
分支：`task/619`。

## 做了什麼

| 項目 | 內容 | 檔案 |
|---|---|---|
| 10 筆清單改用 `[WorldReset]` 清空 | 拿掉舊的推測式清空（比對 actor 與聊天序號），改成 M7-A 擴充點 `[WorldReset] ResetLocationShares()`。`WorldSession.Reset()` 在斷線、取消、以及每次進入角色（`WorldSession.cs:131`）都會呼叫，所以舊 session／舊角色的清單不會留下。沒改 `WorldSession.cs` | `LocationShare.cs` |
| `+16`＝0 分支 | `sub_8D43B0` 第一個分支：功能物件 `+16` 為 0 時只顯示系統訊息 **2139**、不記錄。新增 `WorldSession.LocationListObjectPresent`（預設 true）；為 false 時，收到的 S250/111 改成一行 `〔訊息2139〕`，沒有座標連結、不進清單、清單 revision 不變；壞封包照樣拒絕 | `LocationShare.cs` |
| 清單快照 | `LoginConnection.LocationShareSnapshot()` 在 world capture lock 下複製清單（generation、world epoch、revision、紀錄） | `LocationShare.cs` |
| 清單畫面 | 新增原生面板 `LocationListPanel`（`IUiPanel`，Window 層）。只在原生 UI 開啟、在世界中、`Field_Map` 視窗開著、而且清單不是空的時候出現，貼在 `Field_Map` 右側；每列「寄件者：地區」，最多 10 列；只畫文字（白字、黑色 1 px 陰影，選取列青色），**不畫任何底圖或色塊** | 新檔 `NativeUi/LocationListPanel.cs` |
| 點列的行為 | `LocationShareUi` 自己掛成面板的宿主（`ILocationListHost`）。點一列只放既有的 ⊕ 標記或「不同地圖」提示，和點聊天連結一樣；不移動角色、不送封包。清單換版（revision）或 session 重置後，舊畫面的點擊一律拒絕 | `LocationShare.cs` |
| 註冊 | `UiPanelRegistry.cs` 只加一行 | `NativeUi/UiPanelRegistry.cs` |
| 離線測試 | `[WorldModuleTest("location-share-followup")]`，62 項檢查，由 `--module-tests` 自動執行 | 新檔 `LocationShareFollowupTests.cs` |
| lock | `rebuild.py --update-lock` 重產 | `engine/_integration/dev/rebuild-lock.json` |

## 字串（本輪不改，等 Q-2）

1784／1785／2139 與 mapDesc **維持編號占位**（`〔訊息1784〕`、`〔訊息1785〕`、`〔訊息2139〕`、`〔字串N〕`）。理由：gap-matrix 11.5 規定 R2-M 的字串要等 Q-2 確認編號，Q-2（11.6）還沒做。

給 Q-2 的線索（**推測**，未驗證）：
- `protocol-gaps.md` §3 已經在 `string-c.tbl` 的 1784／1785 讀到語意吻合的文字（1784＝「%s從 %s地區發送了座標位置情報。」）。
- 但 1784 用的是 `%s`。原版 `sub_933850` 在 CP950 路徑會把 `%s` 吃掉（不輸出），所以 `sub_7DC850` 應該不是走 `sub_933850`，而是另一套格式化（可能是 sprintf 類）。Q-2 要一起確認 `sub_7DC850` 讀哪張表、怎麼代入參數，再決定用 `ServerMessageText.Format` 還是另寫。
- mapDesc 是 MapSystem XML 的字串 id，要確認是同一張 `string-c.tbl` 還是別的表。

## 怎麼驗證

| 驗證 | 結果 |
|---|---|
| `rebuild.py --update-lock` → `rebuild.py --build` | PASS（product DLL `b151ec13…1010`） |
| `P11.ClientEngine.exe --module-tests` | pass：28 組、0 失敗、1432 項；`location-share-followup` 62 項全過 |
| demo-tests `location-share` | pass，462 項（既有的座標分享與地區測試，包含舊 UI 點擊流程） |
| demo-tests `map-continuity`（CI 也跑這組，內含 LocationShareChecks） | 沒有客戶端時 pass，33314 項。**注意**：本機如果找得到真實客戶端，S245 的一項檢查（`notice S245 unknown message stays labeled`）會失敗，原因是字串表從真實客戶端載入，跟本任務無關；CI 沒有客戶端，不受影響 |
| demo-tests 其他 CI 組（native-ui-resources／canvas／macro／menu-buttons／minimap／hpmp／chat-window／left-status、combat-admission、item-status、afflictions、resurrection、polymorph-cadence、buddies、item-transfer、doors、pickup、door-approach、hypertext） | 全部 pass |
| `check-no-game-assets.py` | pass（errors 0） |

新測試涵蓋：`[WorldReset]` 註冊與清空、最多 10 筆而且先刪最舊的、只換 actor 不會清空、快照是獨立複本；`+16`＝0 只顯示 2139、不記錄、壞封包照樣拒絕、旗標不隨 reset 改變；面板顯示條件、位置（限制在 800×600 內）、命中測試、按下和放開不在同一列不算、清單換版不算、右鍵不算、不發任何指令；在合成畫布上確認只有文字筆畫、沒有底色、沒畫出 Bounds；宿主（STA 執行緒上真的 WinForms 控制項）：清單鏡像、過期 revision 或未知序號拒絕、同地圖放 ⊕、不同地圖只提示、不送封包、reset 後清掉選取和標記。

## 已驗證／推測／待驗證

- **已驗證**（WO §5 反組譯）：清單最多 10 筆、先刪最舊；`+16`＝0 時只顯示 2139、不記錄。
- **推測**：
  - 面板放在 `Field_Map` 右側、只畫文字、每列「寄件者：地區」，都是引擎自己的過渡畫面，**不是原版**。原版清單在哪個 UI、能不能點，WO §5 還是「待驗證」。
  - 點列放 ⊕ 標記是沿用聊天連結的行為，原版點擊行為未追到。
- **待驗證／沒做**：
  - `+16` 在原版到底是什麼條件沒追到，產品從不把 `LocationListObjectPresent` 設成 false（分支只在測試觸發）。
  - 1784／1785／2139／mapDesc 的文字（等 Q-2）。
  - 原生 UI 模式下，⊕ 標記（WinForms Label）會不會疊在原生畫布上正常顯示，沒有實機看過；面板本身也沒用真實客戶端渲染比對。
  - `TransferMenu` 送出入口（地圖視窗 → 輸入列提示 1785）不在本組範圍，`/loc` 照舊保留。
  - 沒有啟動遊戲、伺服器或 DB；沒有真機驗收。

## 產出路徑

- `engine/source/LocationShare.cs`
- `engine/source/NativeUi/LocationListPanel.cs`（新）
- `engine/source/LocationShareFollowupTests.cs`（新）
- `engine/source/NativeUi/UiPanelRegistry.cs`（一行）
- `engine/_integration/dev/rebuild-lock.json`（重產）
- `docs/reports/issue-619.md`（本檔）

## 給下一棒的提示

1. **Q-2 查完後**（另一張任務）：只改 `LocationShareProtocol` 的 `Format`／`RegionLabel`／`ListUnavailableText` 和 `Encode` 的錯誤提示，改從 `ServerMessageText.Table` 取文字；沒有表的時候要保留編號占位（CI 沒有客戶端）。`demo-tests` 的 `LocationShareChecks.cs:24`、`:106` 寫死了占位格式，到時要一起改。
2. 查到原版清單 UI 之後：只要換掉 `LocationListPanel` 的 `Bounds`／`Paint`（改讀 XML 和 surf），宿主介面 `ILocationListHost` 不用動。
3. 查到 `+16` 的條件之後：在條件成立的地方設定 `World.LocationListObjectPresent`；這個值不是 session 資料，reset 不會改它。
4. 本 worktree 沒有 `engine/assets`，demo-tests 全套（不指定組）會因為 `terrain0.bin` 中斷；CI 的各組可以照常跑。`engine/vendor` 是指向原專案的 junction（跟其他 worktree 一樣），已被 git 忽略。
