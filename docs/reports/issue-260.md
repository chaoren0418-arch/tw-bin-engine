# Issue #260／T126：好友與封鎖名單

## 結論

**第 3 次嘗試仍未完成驗收；PR #275 不可合併。** 2026-11-07 透過 GitHub 連接器核對 Issue 本文與全部 9 則留言，仍未找到主控對兩處必要掛接的範圍修訂；本次對話亦已提出確認，尚未取得答覆。維持原有「WorldSession.cs 只加 S39 分支」限制，不擅自增加 S250／S71 路由或 Reset 掛接。

前次審查的三項問題仍成立：S250／S71 無法到達名單處理器、Reset 未清空名單與等待、完整 buddies 測試失敗。115 項 codec 通過及無 CI run 都不代表驗收成功。

## 做了什麼

既有實作包含 `Buddies.cs` 的 C4／207／202／171 編碼、S39 buddy 與 S250/17–19 解碼、名單模型與記憶體可測 sender；`BuddyUi.cs` 的好友／封鎖服務頁；`PlayerChat.cs` 單一封鎖查詢點。好友變更不樂觀更新；封鎖切換等待伺服器回覆。詳細格式與唯讀來源 SHA-256 見 `docs/protocol/buddies.md`。

本輪只補強已提交的 `buddies-tests/BuddyChecks.cs`：

- 在完整測試前段增加四個斷言，確認其他 S39 NPC 對話、S250 飽食度、S71 訊息參數保留原分派，且不修改好友狀態。
- 增加直接 Reset 與 Cancel 兩種清理情境：先建立非空清單、待確認好友及已送出 C171 的等待，再驗證清單、等待、notice 與 UI revision 清理。這些後段斷言尚未執行到，不能宣稱通過。
- 保留原先失敗斷言及完整測試入口，沒有略過或改弱；原 Windows CI 仍執行完整 `buddies`。

## 怎麼驗證

本輪使用 SDK 10.0.300、Python 3.12.10，所有快取、暫存、建置輸出均在此 worktree；NuGet 唯讀引用原專案 vendor。未寫原專案工作檔。

| 檢查 | 本輪結果 |
|---|---|
| `rebuild.py --update-lock` | 通過，175 source files／184 pinned files，dev lock 無差異 |
| `rebuild.py --build --configfile scratch/issue-260/NuGet.Config --write-receipt --output scratch/issue-260/build` | product、audio、launcher 成功；本次增量建置輸出 0 警告、0 錯誤 |
| DemoChecks `buddies-codec` | 115 項通過，涵蓋全部 C 格式及 S39／S250 合成格式與錯誤封包 |
| DemoChecks `buddies` | 退出 1；新增前段四項與原 S39 路由／owner 斷言已通過，仍停在 `S250/17 snapshot and case-insensitive lookup` |
| `check-no-game-assets.py` | pass；3251 tracked files、0 errors、4 EmbeddedResource declarations |
| `git diff --check` | 通過 |

未重跑前輪已通過且此次未改產品程式的地圖／戰鬥回歸。建置 DLL SHA-256：`fe048e0cc7278489733a7a2137fcc11e95991b2f0c5a92a9bede563c77098447`，與前輪相同。

GitHub：已確認 PR #275 開啟中且 `mergeable=false`；原 head `6a8fc20a9d18ab5413c7b700f19a82c89735f808` 的 pull-request workflow runs 為空。CLI `gh` 回 401，讀取改用已連接的 GitHub 帳號。**CI 綠燈未達成**；沒有把空 checks 當成成功。

## 未驗證什麼

- 兩處必要掛接尚未取得範圍修訂，封鎖功能不能交付，完整模組測試尚未通過。
- 最新 head CI 綠燈尚未達成；PR 另有合併衝突狀態，修復時需檢查最新 main。
- 原生 UI／登入、好友持久化、在線變化、雙角色封鎖聊天；本任務禁止啟動遊戲，集中交獲授權的 dot smoke。
- S250/17 有伺服器建構子依據，但不宣稱每次登入必定收到完整快照。

沒有啟動遊戲、伺服器或 DB；沒有複製遊戲資源或伺服器原始碼；沒有修改封存、硬連結、`docs/REPORT.md`；沒有遞迴刪除。

## 產出路徑

- 本輪測試：`engine/_integration/dev/buddies-tests/BuddyChecks.cs`，既有 DemoChecks／Windows CI 入口。
- 程式：`engine/source/Buddies.cs`、`BuddyUi.cs`、`WorldSession.cs`、`PlayerChat.cs`（本輪未改）。
- 協定：`docs/protocol/buddies.md`；本報告：`docs/reports/issue-260.md`。
- 本機證據（不提交）：`scratch/issue-260/build/`、`buddies-run.log`、`buddies-codec.json`、`receipt.json`。

## 給下一棒的提示

主控先在 Issue #260 明確允許下列兩處最小修改，避免帶著相同限制再派一次：

1. `WorldSession.Reset()` 加 `ResetBuddies()`。
2. 把 `if(opcode==39&&ReceiveBuddies(opcode,plain))return;` 改成 `if(ReceiveBuddies(opcode,plain))return;`。模組只消費 S39 buddy 與 S250/17–19；S71/109、472 只觀察並回 false，保留原訊息分派。

取得修訂後，由 codex-local 套入、更新 dev lock、建置並完成完整 buddies 測試；處理與最新 main 的實際衝突，再確認最新 head CI 綠燈。由 Claude 審查、使用者合併；本次不能以「已提交」視為 T126 完成。
