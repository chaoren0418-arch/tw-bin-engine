# Issue #568 T243：水中呼吸的狀態圖示（S126／原版 S_BREATH）

## 做了什麼
- 依據 `docs/reference/effects-2.md` 第 3 節（T227／#534）與 `ui-layout.md` 第 4 節：原版 S_BREATH（`sub_931F60`，VA `0x931F60`／FO `0x531360`）只呼叫 `sub_F05A90(秒, 11, 0, -1, 1)`，也就是加上狀態圖示 effect 11 `WATER_BREATH_S`，顯示模式 0，沒有畫面特效。
- `engine/source/TimedBuffs.cs`：
  - `TimedBuffRules.WaterBreathEffect = 11`、`WaterBreathDisplayMode = 0`、`StatusEffect(skill)`：只有 `WaterBreath`（S126 的 buff 鍵 `-3126`）對到 effect 11。其他 buff 沒有在本任務查證過，回傳 `null`。
  - 新 record `StatusEffectIcon(Effect, DisplayMode, Skill, Name, Seconds)`，以及 `WorldSession.StatusEffectIcons`：從 `MagicBuffs` 取出有 effect id 的 buff，剩餘秒數直接用 T153 的計時（`MagicBuff.Remaining`）。秒 = 0 移除、時間到消失、刷新、只收自己的物件 id，全部沿用 T153 的 `ReceiveBlessOfEva`／`ReceiveBuff`，沒有改動。
  - 透過 M7-A 擴充點 `[WorldSnapshot("statusEffects")]` 輸出到 `Snapshot().extensions.statusEffects`。沒有新增封包處理，也沒有改 `WorldSession.cs`。
- 這裡只提供資料來源。effect 11 → `effectlist2.xml` → `552.tbt` 的查表與繪製，由原生 UI 的 ui-buff-icons 模組在執行期從原客戶端讀取。本任務沒有讀取或加入任何圖檔，也沒有做替代圖。
- 新檔 `engine/source/BreathStatusTests.cs`：`[WorldModuleTest("breath-status")]`，自動接上 `--module-tests` 入口（CI 的 World module extension tests 步驟會跑），共 25 項。內容包含：對照表、快照註冊、S126 加入 effect 11、倒數、刷新、秒 0 移除、到期、其他物件、其他 buff 不出現、Reset、截斷封包拒絕。
- `engine/_integration/dev/rebuild-lock.json`：用 `rebuild.py --update-lock` 重新產生（240 個來源、249 個鎖定檔）。

## 怎麼驗證（已驗證）
- 合併 origin/main（落後 27 個 commit，只有 `rebuild-lock.json` 衝突，取 main 版本後以 `--update-lock` 重產：249 個來源、258 個鎖定檔）。
- `rebuild.py --build`：PASS。product DLL SHA-256 `4293854312e6b3cec6e3362a07d21092962fa76dc93f30b299c18d7d4d4a3efc`。
- `P11.ClientEngine.exe --module-tests`（照 CI 方式）：pass，7 組、372 項，0 失敗；breath-status 25 項全過。
- DemoChecks：timed-buffs、afflictions、resurrection、item-status、combat-admission 全部 PASS（同步 main 後重跑）。
- `check-no-game-assets.py`：pass，0 錯誤。
- 第二次同步（2026-11-08，main 又前進 27 個 commit，含 AbilityStatus 與 NativeUi 畫布）：只有 `rebuild-lock.json` 衝突，取 main 版本後 `--update-lock` 重產（260 個來源、269 個鎖定檔）。
  - `rebuild.py --build`：PASS，product DLL SHA-256 `67dc7df70643e3ce1223b4c2ca342b83351bba5871232d4e88a3b4fd93e3c19b`。
  - `--module-tests`：pass，8 組、410 項，0 失敗；breath-status 25 項全過。
  - DemoChecks：timed-buffs（645）、item-status（409）、afflictions（580）、native-ui-canvas（429）全部 PASS。
  - `check-no-game-assets.py`：pass，0 錯誤（3792 個追蹤檔）。
- 第三次同步（2026-11-09，main 前進到 #513／#514／#515／#516）：只有 `rebuild-lock.json` 衝突，取 main 版本後 `--update-lock` 重產（268 個來源、277 個鎖定檔）。
  - `rebuild.py --build`：PASS，product DLL SHA-256 `e4661fa3f3e711073d6dd1df97a18ac6e4a1f43d65532c54f58273a85feb31a6`。
  - `--module-tests`：pass，11 組、596 項，0 失敗；breath-status 25 項全過。
  - DemoChecks：timed-buffs（645）、item-status（409）、afflictions（580）全部 PASS。
  - `check-no-game-assets.py`：pass，0 錯誤（3805 個追蹤檔）。
- 第四次同步（2026-11-09，main 前進到 #546／#566）：只有 `rebuild-lock.json` 衝突，取 main 版本後 `--update-lock` 重產（271 個來源、280 個鎖定檔）。
  - `rebuild.py --build`：PASS，product DLL SHA-256 `de45c4a0a93b8f4eecadad266095a30e1386ae297d092aa2b881f3b9a4232039`。
  - `--module-tests`：pass，14 組、683 項，0 失敗；breath-status 25 項全過。
  - DemoChecks：timed-buffs（645）、item-status（409）、afflictions（580）全部 PASS。
  - `check-no-game-assets.py`：pass，0 錯誤（3812 個追蹤檔）。
- 第五次同步（2026-11-09，main 前進到 #549）：只有 `rebuild-lock.json` 衝突，取 main 版本後 `--update-lock` 重產（273 個來源、282 個鎖定檔）。
  - `rebuild.py --build`：PASS，product DLL SHA-256 `a2d5a9de7b44328c8d7f464159c28a1d8fd7c048b89d41a97538778743b1c445`。
  - `--module-tests`（照 CI 方式）：pass，15 組、775 項，0 失敗；breath-status 25 項全過。
  - DemoChecks：timed-buffs（645）、item-status（409）、afflictions（580）全部 PASS。
  - `check-no-game-assets.py`：pass，0 錯誤（3815 個追蹤檔）。
- 第六次同步（2026-11-09，main 前進到 #559）：只有 `rebuild-lock.json` 衝突，取 main 版本後 `--update-lock` 重產（275 個來源、284 個鎖定檔）。
  - `rebuild.py --build`：PASS，product DLL SHA-256 `c7e2a0b78b5bba8a0e3e7715160fc2e0bcb4c64385f7a633a26b9b7b1c3afe0d`。
  - `--module-tests`（照 CI 方式）：pass，16 組、807 項，0 失敗；breath-status 25 項全過。
  - `check-no-game-assets.py`：pass，0 錯誤（3816 個追蹤檔）。
- 第七次同步（2026-11-09，main 前進到 #556／#557）：PR 上一次 CI 的 dotnet 失敗原因是 `REFUSED: sourceCount 275 != pinned source files 276`（main 自動合併進來的新來源檔沒進 lock）。合併無衝突，`--update-lock` 重產（276 個來源、285 個鎖定檔）。
  - `rebuild.py --build`：PASS，product DLL SHA-256 `2fad93db2e8bd28fce563675657fb410b9c16d334efd392ffb6ab9ac414d2865`。
  - `--module-tests`（照 CI 方式）：pass，17 組、873 項，0 失敗；breath-status 25 項全過。
  - `check-no-game-assets.py`：pass，0 錯誤（3819 個追蹤檔）。

## 未驗證什麼
- DemoChecks 不帶群組的預設入口在這個 worktree 跑不起來：沒有 `engine/assets` junction，讀 `terrain0.bin` 失敗。這是環境限制，和本次改動無關，CI 也不跑這個入口。
- 畫面上的圖示（`552.tbt`、位置、tooltip）還沒做，等 ui-buff-icons。也沒有做真機驗收。
- 原版不檢查物件 id，引擎只收自己的 id（沿用 T153）。3.81 伺服器只把 S126 送給本人，所以看不出差異，本任務沒有改這一點。
- CI：PR 開出後由 GitHub Actions 驗證。

## 產出路徑
- `engine/source/TimedBuffs.cs`、`engine/source/BreathStatusTests.cs`、`engine/_integration/dev/rebuild-lock.json`、本報告。

## 給下一棒的提示
- ui-buff-icons 可以直接讀 `WorldSession.StatusEffectIcons`（或快照 `extensions.statusEffects`）裡的 `effect`，到 `effectlist2.xml` 查 `icon`（effect 11 應該是 `552.tbt`，`Sprite.pak` 與 `Sprite04.pak` 各有一份，見 ui-layout 第 4 節「雙份」的優先序問題）。`mode = 0` 的 tooltip 格式是「字串930: 秒 字串931」。
- 其他 buff 要接 effect id 時，擴充 `TimedBuffRules.StatusEffect`，並照 ui-layout 第 4 節的「舊封包 → effect」表加上測試；顯示模式目前固定為 0，到時要改成每筆各自的值。
- 本機 worktree 建置需要 `engine/vendor/microsoft.web.webview2.1.0.4258.31.nupkg`（本次從主目錄複製一份，未入 git）。
