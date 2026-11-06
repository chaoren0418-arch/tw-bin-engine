# Issue #173／T093 戰鬥准入修正

## 做了什麼

- 已確認 C229／光箭 C6 編碼符合伺服器 DHH／CCDHH 契約。產品缺少距離准入，會向過遠目標送攻擊。補上近戰 2 格、光箭 10 格檢查與走近提示，並補近戰排隊時自身座標重核。
- PacketTrace 增加選用 `TW_PACKET_TRACE_COMBAT=1`，只為白名單封包加入 target 與 S30 damage。文字型 C6、其他封包及關閉時維持原五欄。
- 唯讀核對伺服器處理器、技能模板、目標 fence、timeline 與 S30，契約／靜默條件／SHA 已補入 `docs/protocol/combat.md`。更新 dev 滾動來源 lock。

## 已驗證與根因界線

- 客戶端可重現的缺口是距離未檢查。伺服器 `L1AttackPc.calcHit` 在距離大於 weaponRange+1 時判 miss，空手／普通近戰 range=1。`L1SkillUse.makeTargetList` 對光箭距離大於10或 LOS 失敗留下空集合，PC 仍可能扣 MP，`sendGrfx` 只送 S158，沒有 S30。
- #164 原 trace 核得近戰 C229×52、S30×18、S158×19，光箭量測窗 C6×1、S158×1、S33×1、S30×0。當時沒有目標／傷害欄位與准入診斷，無法將18筆 S30 對應到牛，亦無法唯一證明52次的拒絕原因。超距離／LOS 與光箭現象相符，歷史歸因仍待驗證。

## 怎麼驗證

- SDK 10.0.300 正規 `rebuild.py --build --configfile .github/nuget.ci.config --write-receipt` 通過，172來源／181 pins。產品0錯誤，既有 MagicUi.cs 的 CS8600／CS8602 共2警告，launcher 0警告／0錯誤。
- `DemoChecks ... combat-admission` 通過212項，含正常 Attack／滑鼠、近戰與光箭邊界、獨立封包解碼及伺服器幾何條件對照、排隊競態、冷卻、EXP／掉落／學技／箭數合成回歸。
- `PacketTraceChecks` 通過3,468項，覆蓋 opt-in、完整白名單與秘密假字串、截斷、unsigned 數字、併發、結束 flush 與 junction 路徑拒絕。
- `check-no-game-assets.py` 通過，0錯誤。CI 已加入 Windows 戰鬥回歸，PR 最終檢查結果以 GitHub checks 為準。
- NuGet／Git 在沙箱內遇 TLS 認證限制，建置已透過核准的沙箱外命令完成。測試資料均為明示合成資料，測試沒有啟動遊戲、伺服器或 DB。

## 未驗證什麼

實際牛目標身分、每筆歷史 C229 的准入結果、LOS、實際擊殺／EXP／掉落、光箭音效，以及本機 Java 來源與當時部署 JAR 是否一致，均待真機。現有 wire 未提供武器模板射程，本次近戰採目前支援姿勢的普通2格門檻，客製加長射程武器須另核對。

## 產出路徑

- 產品 `engine/source/Combat.cs`、`Magic.cs`、`PacketTrace.cs`。
- 測試 `engine/_integration/dev/demo-tests/CombatAdmissionChecks.cs`、`packet-trace-tests/Program.cs`，及 `.github/workflows/build.yml`。
- 文件 `docs/protocol/combat.md`、本報告。
- 本機證據 `scratch/issue-173/verify.log`、`combat-checks.json`、`source-hashes.json`、`build/`，trace 測試證據在 `scratch/packet-trace-checks/`。不提交合成資源與本機 receipt。

## 給下一棒的提示

Claude 審查 PR、CI 綠後交使用者合併。dot 在授權的 P24Lab 用測試角色，開 `TW_PACKET_TRACE` 指向新 scratch 檔並設 `TW_PACKET_TRACE_COMBAT=1`，選定單一活怪、近戰保持2格內、光箭10格內且視線暢通，逐次操作並核對 target／damage、EXP及死亡。若仍零傷害或無 S30，請由伺服器維護者對照既有 AttackAdmissionDiagnostics／PcPhysicalAttackFlow 的位置同步、timeline 與 LOS 原因。原始 payload 與額外身分資訊不納入本次 trace。
