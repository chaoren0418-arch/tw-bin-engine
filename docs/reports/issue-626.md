# Issue #626 T265：R2-L 狀態圖示資料收尾

依據 `docs/reference/gap-matrix.md` 第 11.5 節的 R2-L 列。分支是 `task/626`。

## 做了什麼

### 1. 原版對照（已驗證，用 capstone 靜態反組譯）

BIN 是 `TW1608262200.bin`，SHA-256 為 `e12949cf…09641a2`，`nat.py` 會先核對這個值。TW16 S_EVENT（op 108，`sub_9357E0`）的子碼跳表在 VA `0x93EC14`，涵蓋子碼 0–0xCC。各子碼查到的結果如下：

| 子碼 | case VA | 原版行為 | 引擎原本 | 本次 |
|---|---|---|---|---|
| 59 | `0x93AB77` | 不讀任何欄位。effect 57 `WATER_LIFE_S` 存在時呼叫 `sub_F059F0(57,0,1)` 移除它 | 移除技能 170（標為推定） | 確認 effect 57 就是技能 170「水之元氣」（3.81 `L1SkillId.WATER_LIFE`），所以仍然只刪這個鍵、不分來源。註解與常數改成已驗證：`WaterLifeEffect=57` |
| 75 | `0x93B2E2` | 讀 C（階段）。>0：把 effect 135 的 icon 改成 `0xC9B+3−階段`（3229／3228／3227），再呼叫 `sub_F05A90(effect 135 的 duration, 135, 0, −1, 1)`。≤0：移除 135 | 只存數值 | 補上圖示 effect 135、icon 改寫值和預設秒數旗標。3.81 呼叫端是龍騎士「弱點曝光」（`L1AttackPc`） |
| 82 | `0x93B802` → `sub_735F90` → `sub_736AC0` | 讀 3 個 D，組成 `EinhasadExpEvent`（事件 0x18）交給 `EinhasadExManager`。值 >0 時加 effect **175**（`STR_STATUS_EFFECT_BLESS_OF_EINHASAD`），顯示模式 **1（百分比）**。值 >200 時改用 575（旗標開時 576），這兩個 id ≥540，原版不建立圖示。值 ≤0 時移除 175、518、574、575、576 | 只存數值 | 補上 effect 175 與模式 1。值 >200 時不列。3.81 只寫 `C(值) C(0)×3`，所以 TW16 讀到的第一個 D 正好就是這個值 |
| 180 | `0x938857`（解析在 `sub_C14ED0`） | C（開關）、D → `+8`、D → `+0xC`。開：呼叫 `sub_F05B10(−1, +8, 0, −1, 1)`，**第一個 D 是 effect id**，無期限，並把 effect 的 startMsg 印到聊天。關：呼叫 `sub_F059F0(+8,1,1)`。封包裡的字串 id 原版沒用到 | 用「圖示 id」當鍵 | 改成以 effect id 解讀（`PacketBoxNoneTimeIcon.Effect`），無期限（−1）、模式 0。≥540 不列。3.81 的呼叫端（`ItemVIPTable`、`TimedRateController`、`MapRecoveryIconState`）傳的也是 effect id |
| S36（TW16 op 122） | `sub_932300` | 用 `cc` 讀 type 和值。type（有號）<6 時呼叫 `sub_9E3260(值)`，寫進一張 6 格的表 | 只存原始 type | 新增 `AbilityRules`：只保留 type 0–5；名稱 1＝傳送控制戒指、2＝變形控制戒指、5＝道具 20284（名稱待驗證），取自 3.81 `L1EquipmentSlot`。**原版不加圖示，也不進左狀態框** |

effect 名稱是在本機 `effectlist2.xml`（scratch 解密檔）查的，只記編號與名稱，沒有任何素材進 repo。

### 2. 程式

- `PacketBoxStatus.cs`
  - 檔頭寫入上表的原版地址。
  - `PacketBoxStatusRules` 加入常數：effect 57／135／175／575、`DkIcon()`、`LeavesEffectFor()`、`LeavesRemoved`、`EffectTableCount=540`。
  - 新 record `PacketBoxStatusIcon(Effect, DisplayMode, Value, DefaultDuration, IconOverride, Origin, Name, ValueText)`。
  - 新屬性 `WorldSession.PacketBoxStatusIcons`：依 75、82、180 的順序列出。
  - 快照 `packetBoxStatus.statusIcons`。
  - 原有的解析和拒絕規則都沒改。
- `StatusIcons.cs`：新增 `StatusIconFeed.Read(WorldSession)`。WinForms 狀態列除了倒數狀態，也會列出 75／82／180 的文字：「弱點曝光 · 第 n 階」「殷海薩的祝福 · n%」「狀態 e · 無期限」。這些項目的 Id 是 −(1,000,000＋effect)，不會和技能鍵相撞。
- `AbilityStatus.cs`：新增 `AbilityRules`。快照的 flags 加上 `name`、`keptByOriginal` 兩欄。
- 新檔 `StatusIconFeedTests.cs`：`[WorldModuleTest("status-icon-feed")]`，共 62 項，自動接上 `--module-tests`。
- `MagicStatus.cs` 不需要改。水中呼吸和 S37 已經由 #473／#598 接好，本次只用測試確認：
  - S126 經 `BuffState.From` 對到 effect 11，秒數和移除都正確。
  - S37 經 `LeftStatusState.From` 填入 SP／MR（`MrText` 是 "45%"）。
  - S36 不影響左狀態框，也不增加圖示。

## 怎麼驗證

- `rebuild.py --build --configfile .github/nuget.ci.config`：PASS，用暫時 lock，**沒有改 `rebuild-lock.json`**（#646 政策）。product DLL SHA-256 `ed06674ad11c308e0c0b8136e6a7da8f752eadd6f7c65287c16c7bf3cd4b685c`。worktree 沒有離線 `vendor`，所以和 CI 一樣改用 nuget.org 設定。
- `--module-tests`：pass，29 組、1514 項，0 失敗。其中 status-icon-feed 62 項、packetbox-status 86 項、ability-status 38 項、breath-status 25 項。
- DemoChecks：native-ui-buff-icons 381、native-ui-canvas 429、native-ui-party 396、item-status 409，全部 PASS。
- `check-no-game-assets.py`：pass，errors 0。
- 重現反組譯只需要 repo 內已追蹤的腳本，例如 `py -3 -I docs/reference/rea-scripts/disx.py 93AB77 20`、`… 93B2E2 80`、`… 938857 60`、`… 736AC0 140`、`… 932300 44`。

## 未驗證什麼

- **畫面**：`BuffState.cs`（#600 的檔案）不在本組可改範圍，所以 75／82／180 的 effect **還沒進 ui-buff-icons 面板**。目前只有資料（`PacketBoxStatusIcons`）、快照和 WinForms 狀態列文字。面板還缺三件事：
  - 支援模式 1（百分比 tooltip）。
  - 支援 icon 改寫（75）。
  - 支援無期限（Seconds −1）。
- 82 的管理器旗標 `+0x10` 由事件 0x15／0x16（id 0x125）切換，全域 `0x138BE64 == 0x1900` 也是一個條件。兩者在 3.81 都找不到來源，所以一律當成關（effect 175）。是否正確待驗證。
- 82 的 tooltip 另外用到字串 4448（`0x1160`）的條件分支，沒有展開。
- 180 開啟時，原版會把 effect 的 startMsg 印到聊天；75／82 移除時可能印 endMsg。這兩項都沒做，要讀 effectlist2 的字串欄位，屬於畫面或聊天工作。
- 75 的秒數取 effect 135 的預設 duration（本機表是 16）。引擎沒有讀這張表，所以狀態列不顯示秒數。
- S36 type 5（道具 20284）的名稱、`sub_9E3260` 的用途都沒查。
- 沒有真機測試。

## 產出路徑

- `engine/source/PacketBoxStatus.cs`、`StatusIcons.cs`、`AbilityStatus.cs`
- `engine/source/StatusIconFeedTests.cs`（新）
- `docs/reports/issue-626.md`

## 給下一棒的提示

- 動 `NativeUi/State/BuffState.cs` 的任務，在 `BuffState.From` 的迴圈後面讀 `w.PacketBoxStatusIcons`：
  - `BuffEntry.Effect` 取 `i.Effect`。
  - 模式 0：`Seconds = i.Value`（−1＝無期限），`DefaultDuration = i.DefaultDuration`。
  - 模式 1：`BuffEntry` 需要新增 DisplayMode 欄位，tooltip 改顯示百分比（ui-layout 第 4 節 `sub_900FC0`：模式 1／12 是百分比）。
  - `IconOverride` 要讓 `BuffIconsPanel` 用它取代 effectlist2 的 icon（只有 75 用到，`.tbt` 編號 3227–3229）。
- 狀態變化會觸發 `PacketBoxStatusChanged`，同時 `MagicRevision` 會遞增，面板可以靠這兩個重畫。
- 180 的 `PacketBoxNoneTimeIcon.IconId` 名稱沿用舊的，意義是 effect id，請改用 `.Effect` 讀。
