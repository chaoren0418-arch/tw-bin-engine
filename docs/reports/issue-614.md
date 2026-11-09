# Issue #614 — T253：R2-C 點擊與拾取收尾（code，第 1 次嘗試）

依據：`docs/reference/gap-matrix.md` 11.5 節 R2-C；WO（`world-objects.md`）第 1、2、4 節；PG§4（`protocol-gaps.md`）。

## 做了什麼

| 項目 | 改動 | 檔案 |
|---|---|---|
| 雙擊地上物品改走 `PickupGroundClicked` | 新增 `PlayerPointerController.ClickGroundAsync(id, pickup)`，場景雙擊改呼叫它並傳入 `GroundUi.PickupGroundClicked`（→ `LoginConnection.PickupGround`）：1 格內單個或「全部拾取」開時送整疊；多個時開數量提示；遠處先用 `ApproachWalk` 走近。滑鼠提示改讀 `World.GroundNotice`；HUD 送出文字改成「整疊或指定數量」，不再寫「1 件」。走近中 pointer 算 `Active`（Esc／鍵盤可停），但不占用 `sending`，新點擊透過 inputVersion 取消舊的走近，不會被擋 | `PlayerPointer.cs`、`PlayerPointerUi.cs`、`PlayerPointerHud.cs`、`GroundUi.cs`（只改註解） |
| 死亡阻擋套到 NPC 與攻擊點擊 | 新增 `Refusal(id, talk)`：自身死亡（`ObjectClicksBlocked`，WO §1 `byte_13CE3FD`）時 NPC 交談、攻擊都不送，提示「未送出：死亡中不能操作物件」。個人商店的右鍵入口也套用同一個判斷 | `PlayerPointer.cs`、`PlayerPointerUi.cs`、`PlayerPointerHud.cs` |
| 幽靈中擋滑鼠攻擊 | `GhostLocalBlocked` 時滑鼠攻擊（雙擊、Ctrl＋左鍵、直接攻擊目標）同步返回，提示沿用 `GhostLocalLimits.Notice`；NPC 交談與門照 WO §4 不擋 | `PlayerPointer.cs` |
| 拾取節流改讀 S139 功能旗標 | `PickupFastFlag` 改成 `ServerFeatures?.FastPickup==true`（`dword_13CE514 & 0x10000`）。沒收到 S139 或狀態非 0 時是 986 ms（原版 bss 預設 0）；自身加速不再算 | `Ground.cs` |
| 門提示改讀 `DoorNotice` | 點門後滑鼠提示直接用 `LoginConnection.DoorNotice`（死亡、走近、距離、送出等實際理由）。門的可用性改由 `ToggleDoor` 自己檢查，pointer 只先擋焦點、可見與重入；`walking` 也傳進 `ToggleDoor` | `PlayerPointer.cs` |
| 離線測試 | 新檔 `PointerFlowTests.cs`，`[WorldModuleTest("pointer-flow")]`，40 項檢查，接到 `--module-tests` | `PointerFlowTests.cs` |

**範圍外改動（1 處，只改測試）**：`engine/_integration/dev/pickup-tests/PickupChecks.cs` 第 55–57 行原本用「自身 Speed=1」測 600 ms 節流；節流改讀 S139 後這項會失敗，所以改成注入 S139 旗標，其他沒動。CI 的 `pickup` 步驟會跑到這個檔案。

沒有動 `WorldSession.cs`、`UiPanelRegistry.cs`、`ApproachWalk.cs`；沒有新增封包。

## 怎麼驗證（已驗證）

- `rebuild.py --update-lock` 後 `rebuild.py --build --configfile .github/nuget.ci.config`：**PASS dev**（product DLL SHA-256 `e833bc67…5d75a1`）。worktree 沒有 `engine/vendor`，所以 restore 用 CI 的 nuget.org 設定。
- `P11.ClientEngine.exe --module-tests`：**25 組、1301 項全部通過**，`pointer-flow` 40 項通過。
- DemoChecks 相關組（都通過）：`pickup` 369、`door-approach` 361、`doors` 648、`combat-admission` 1750、`ghost-limits` 362、`slow-actions` 691、`afflictions` 580、`resurrection` 553、`ghost-mode` 721。
- `check-no-game-assets.py`：pass（3865 個追蹤檔，0 錯誤）。
- `rebuild.py`（唯讀核驗）：`dev-inputs-verified`，298 個來源檔。

`pointer-flow` 涵蓋：1 格內單個雙擊送整疊；多個開提示、空白＝整疊；全部拾取開時整疊；遠處走 3 步後送一次並轉向；UI 傳入的委派收到被點的 id；不可見或不存在的物品不送；沒有 S139／旗標沒有 0x10000／狀態非 0 都是 986 ms，有 0x10000 是 600 ms（登入期或進世界後才收到的 S139 都算）；死亡中 NPC 交談、攻擊、雙擊拾取都不送；幽靈中攻擊同步擋下、交談與門不擋；死亡中點門時 pointer 提示等於 `DoorNotice`。

### 同步 main 後重驗（2026-11-09）

- `git merge origin/main`：只有 `rebuild-lock.json` 衝突，取 main 版本後 `rebuild.py --update-lock` 重產（321 個來源檔）。
- `rebuild.py --build --configfile .github/nuget.ci.config`：**PASS dev**（product DLL SHA-256 `032f0ef5…542a71`）。
- `--module-tests`：**35 組、1738 項全部通過**，`pointer-flow` 40 項通過。
- `check-no-game-assets.py`：pass（3936 個追蹤檔，0 錯誤）。

## 未驗證什麼

- **CI**：push 後才會跑，結果看 PR。
- **真機**：沒有啟動遊戲（本任務禁止）。雙擊拾取、走近、數量提示、門提示的實際畫面都還沒在 3.81 測過。
- DemoChecks `busy` 這組在 worktree 失敗，原因是需要 `engine/assets/integrity.json`（worktree 沒有素材 junction），與本次改動無關，CI 也沒有跑這組。`world-ui-tests` 一樣需要素材，沒有跑。
- 推測：600 ms 會在 3.81 上一律生效。依據是 PG§4 指出 3.81 S139 旗標 `0x77d82` 含 0x10000，而引擎已經會送 C14；還沒用真機量過。

## 產出路徑

- 程式：`engine/source/PlayerPointer.cs`、`PlayerPointerUi.cs`、`PlayerPointerHud.cs`、`Ground.cs`、`GroundUi.cs`、新檔 `PointerFlowTests.cs`
- 測試調整：`engine/_integration/dev/pickup-tests/PickupChecks.cs`
- lock：`engine/_integration/dev/rebuild-lock.json`
- 報告：`docs/reports/issue-614.md`
- 分支：`task/614`

## 給下一棒的提示

- `ServerFeatures.cs` 第 7 行的註解還寫著「拾取節流改跟旗標走需另開任務」，現在已經做了；這個檔案不在本組範圍，下一個碰它的任務可以順手更新。
- `PlayerPointerController.PickupAsync(id, count)`（指定數量、不走近）還保留給既有整合測試（`SlowActionChecks`）使用，產品的雙擊已經不走它。之後如果整合測試改用 `ClickGroundAsync`，就可以刪掉它和 `LoginConnection.PointerPickup`。
- 真機驗收建議（P24Lab 測試角色）：雙擊 1 格內單個金幣以外的物品；雙擊遠處的一疊箭，確認先走近再跳數量提示；死亡時右鍵 NPC、Ctrl＋左鍵怪物都沒有送出；幽靈中雙擊怪物出現幽靈提示；點遠處的門時 HUD 顯示「走近門中…」。
