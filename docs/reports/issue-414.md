# Issue #414／T151：復活確認與移動鎖定

## 做了什麼

- 新增 `engine/source/Resurrection.cs`：S219/321、322 復活確認、C121 回覆（d287 格式，D 固定 0，不走 requestId）、S241 移動鎖定、確認面板。
- `OnlineMovement.cs`：`MovementAvailable` 加一個 `MoveLocked` 查詢點。
- `Recovery.cs`：`InitRecovery` 加一個 `InitResurrection()` 掛載點，既有行為未改。
- `WorldSession.cs`：加兩個 case（見下方差異）。
- 新增 `docs/protocol/resurrection.md` 與 `engine/_integration/dev/resurrection-tests/ResurrectionChecks.cs`，接上 DemoChecks 的 `resurrection` 入口與 CI。

## 與任務指定不同的地方

1. **`WorldSession.cs` 的 case 是 S219 與 S241，不是 S85 與 S241。** Issue 假設 S85 含確認，但原服的確認走 S219，S85 只是完成後的動作，來源與 d287 JAR 都一致。S85 已由 `ReceiveRecovery` 解析，所以沒有加。總數仍是兩個 case，但 **S219 這個 case 不在 Issue 列出的範圍內**，請審查者確認。
2. **`build.yml`、`DemoChecks.csproj`、demo-tests 的 `Program.cs` 也有改**，這是「接上既有測試入口」所需，不在 Issue 的檔案清單內。
3. **S241 沒有解除封包**，Issue 寫的「解除」由引擎判定：收到自身 S87、世界重建、Reset，或 5 秒安全上限。5 秒是引擎政策，不是原服數值。

## 怎麼驗證

已驗證：

- `rebuild.py --update-lock`：213 個來源、222 個鎖定檔。`--build`：產品與 launcher 通過，產品 DLL SHA-256 `d8c599b6e2cb9293b2290b11479e074e00ed3ce0c669eb8fb6751dfe8a408272`。
- `DemoChecks ... resurrection`：502 項通過（每組都會先跑 Party、Pets、Polymorph 的檢查，所以數字含這些）。涵蓋 S219／C121／S241／S85 的 hex 向量、截斷、非零補齊、非零 token、提示失效的各種路徑、排隊請求競態、傳送失敗、面板顯示、鎖定期間 C29／C225 為零、解除後可重新移動。
- 同一組也跑既有的 `DeathAndRecovery`、`RecoveryRaces`、`KeyboardBusy123`，使用自製全通行地形，不需遊戲資源。這幾項原本 CI 沒跑，現在會跑。
- 變異測試：分別拿掉移動閘門、排隊失效、S219 觀察、重新開始後失效，四項都讓測試失敗；還原後 DLL SHA 與變異前相同。
- 既有測試組全過：afflictions 428、combat-admission 1409、map-continuity 22358、duel 390、marriage 412、clan 395、slow-actions 640、ghost-mode 635、doors 597。
- `check-no-game-assets.py`：通過，0 錯誤。
- d287 JAR 以 `javap -c` 唯讀核對：`C_Attr` 的 H／D／H 順序與 321、322 分支、`S_Lock` 寫入 241 與 0、`C_UnLock` 的 127 分支。

- 同步 origin/main（25 個提交，只有 `rebuild-lock.json` 衝突，取 main 版後 `--update-lock` 重產：215 個來源、224 個鎖定檔）。同步後 `--build` 通過，產品 DLL SHA-256 `6af85308f4a7d3c6550705dcadf3f6170e4216e82675a61b11f149360f5959de`；`resurrection` 502 項重跑通過；`check-no-game-assets.py` 0 錯誤。

CI 結果見 PR。

## 沒驗證什麼

- **真機未驗**：沒有啟動遊戲、伺服器或 DB，全部是合成封包。集中在 M6 smoke 驗。
- `combat-loop` 與 `keyboard` 測試組本機無法完整跑：worktree 沒有 `engine/assets`，兩組都因讀不到真實資源而中止，不是斷言失敗。CI 也沒有跑這兩組。其中與本任務相關的項目已併入 `resurrection`；`keyboard` 的方向移動項目未跑。
- 面板只測了顯示與關閉，按鈕點擊與版面位置未實際看過畫面。
- 原客戶端的 S241 行為與 321／322 畫面文字未核對。

## 產出路徑

- `engine/source/Resurrection.cs`
- `engine/_integration/dev/resurrection-tests/ResurrectionChecks.cs`
- `docs/protocol/resurrection.md`
- `docs/reports/issue-414.md`

## 給下一棒的提示

- **最重要**：原客戶端收到 S241 後很可能會自動回 `C246`。原服 `Locerr2Timer` 倒數結束只送 S241，位置修正要靠 `C246` 非 127 分支。本客戶端不送 C246，所以「修正人物錯位」選單目前只有 5 秒鎖定，不會修正位置。要補的話，先從原 BIN 的 0xF1 處理器或真機封包追蹤確認 type 值。
- 鎖定期間攻擊與施法也會停止（都以 `MovementAvailable` 為前提），最長 5 秒。若 smoke 覺得不合理，要把查詢點移到 `SendMovement`，但那需要不只一個查詢點。
- 鎖定中的拒絕提示是通用的「目前角色狀態不能移動」，因為只允許一個查詢點。
- 接受復活後到 S85 之前，`死亡後重新開始` 仍可按。這是為了避免原服不回包時卡死；原服 `C_Restart` 本身沒有檢查死亡，所以這個小窗口內連按可能讓剛復活的角色被送回重生點。
- `docs/protocol/combat.md` 第 33 行「沒有實作 Y/N 提示回覆」已過時，該檔不在本任務範圍內。
