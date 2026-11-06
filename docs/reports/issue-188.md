# Issue #188／T101：法術與箭矢接收端

## 做了什麼

- 新增 `AttackSkillPackets.cs`，依完整 S30 布局／flight 分法術與箭矢，不再以 action18、gfx167、action1、gfx66 或單獨 sequence338 判斷。保留不同動作、gfx、無施法者／受擊抑制的零 ID 與座標。
- `ReceiveMagic` 加入一個 S42 分派，完整解析目標數、0／32 受擊旗標，以及逐非零目標的 `D(0xD14A6E3F),D(damage)` 延伸；先驗完整包，再更新面向、施法動作、可見目標特效與受擊狀態。`LastRangeSkill` 保留所有目標及顯示傷害，跨圖／重設後失效。
- S42 分別驗證標準與延伸布局，避免將下一筆 targetId 恰等於延伸 magic 時誤吞。長度、heading、type、reserved、padding、負顯示傷害及不完整延伸均驗證。
- HP 維持 S225 絕對數值、S237 比例等權威回包；S30 damageWord、S42 hitFlag／顯示傷害不自行扣血，重複回包不重扣。
- 保留 24-byte 近戰與現有箭矢66排程。不同箭矢 gfx 保留 `LastProjectile`／`LastAttack`，暫不加入 renderer 的箭矢66佇列，避免使用錯誤圖像冒充支援。
- 產品來源僅修改指定三檔；未改送出端、共用 `WorldSession.cs`、`docs/REPORT.md`、封存 lock 或資源。另更新 dev 滾動 lock。

## 怎麼驗證（已驗證）

- 先執行 `rebuild.py --update-lock`，173 來源／182 pins；再執行 `rebuild.py --build --configfile .github/nuget.ci.config --write-receipt --output scratch/issue-188/build`，產品與 launcher 成功。產品只有既有 `MagicUi.cs` CS8600／CS8602 兩警告，launcher 零警告。
- `scratch/issue-188/AttackSkillChecks.csproj` 參照實際產品 DLL，以合成封包執行 decoder 及 `WorldSession.Receive`：**695 項通過**。覆蓋 5 種 action × 6 種 gfx × 2 種 flight、序號338碰撞、零／未知物件、多目標、type0／8、有／無延伸、延伸 magic 作為 targetId、空集合、截斷、壞欄位／padding、原子拒絕、近戰回歸、權威 HP 回包與跨圖／重設。未建立 LoginConnection 或開啟視窗／網路。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：通過，0 errors。
- `git -c core.whitespace=cr-at-eol diff --check` 通過；保留原本已追蹤的 CRLF。
- 產品 DLL SHA-256：`21eb918f7a8d92a5570eb93d128077577ade501bbfb169f4b801a3249850ff85`。
- GitHub CI 狀態以本 PR checks 為準；發布時將另留驗證結果。

## 未完成／未驗證

- **尚未達成任意 gfx 的完整繪製。** 指定修改範圍之外的 `MagicRendering.cs` 僅處理既有少數 gfx；`ProjectileRendering.cs` 固定使用箭矢66與其移動契約。其他 gfx 已解碼，但未實作原始 sprite／飛行語義。S42 的方向型特效、多目標呈現時序、空目標特效、caster0 特效及新 gfx 音效亦待 renderer 與真機驗證，不能由本次狀態測試宣稱畫面完成。
- 已向使用者提出將上述兩個繪製檔及本模組持久化離線測試目錄納入範圍的問題；尚未收到回覆，因此目前以草稿 PR 供審查，不關閉 Issue。
- `docs/design/m6-contract.md` 在本 worktree、原專案目錄及 GitHub main 均不存在（GitHub contents API 404）。依 #188 明示範圍與 #132 對照表執行；待主控提供契約再重核，未自行建立替代契約。
- 未啟動遊戲、伺服器、DB；未驗證畫面、登入、音效、實際 HP 或部署 JAR 與本機 Java 的一致性。真機留給 M6 smoke。

## 產出路徑與來源

- 產品：`engine/source/AttackSkillPackets.cs`、`Magic.cs:ReceiveMagic`、`Combat.cs:ReceiveCombat`；lock：`engine/_integration/dev/rebuild-lock.json`；本報告。
- 本機未追蹤證據／測試：`scratch/issue-188/Program.cs`、`AttackSkillChecks.csproj`、`run.ps1`、`checks.json`、`build/`。重跑：`powershell -NoProfile -ExecutionPolicy Bypass -File scratch/issue-188/run.ps1`（首次先 restore 該 csproj；產物／合成空 profile fixture 都在 scratch）。測試尚未納入 CI／git，調度器清理 worktree 前須保留或授權移入正式測試目錄。
- 唯讀核對 `D:\lineage開發\1-伺服器\src\com\lineage\server\serverpackets\`，沒有複製伺服器原始碼。SHA-256 與 #132 表一致：
  - `S_UseAttackSkill.java`：`83e00ecf1da363510643ae259d8da3c43d99508fef9edb36fa9dacf74403dcfe`
  - `S_UseArrowSkill.java`：`870d1757e281e346a635c52e6f3b4cf8b5dc22a06959ff5c88a1029bc6a0a769`
  - `S_RangeSkill.java`：`2e6cce916e7b43835793ae55f346284a94e078a5699e3779542a86d96b8d635f`

## 給下一棒的提示

主控／Claude 補齊 M6 契約並確認繪製及測試檔案範圍後，由本機 code 執行器完成 renderer 與持久化回歸。不要把 `LastRangeSkill.Targets[].HitFlag` 當 HP 差值，或把 `DisplayDamage` 截成 ushort；不要把任意新箭矢交給固定66 renderer。新 gfx 的資源來源／飛行語義需先核實，不能只換 gfx 數字套既有圖像。完成後再交 dot 依既有授權做 M6 smoke；此草稿不可當成 T101 全部驗收完成。
