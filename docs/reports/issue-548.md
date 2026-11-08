# Issue #548 T230：S250 狀態圖示類子碼（protocol-gaps B2）

依據：`docs/reference/protocol-gaps.md` 第 2.2、2.3、8 節；欄位寬度依 `docs/reference/server-protocol.md` 的 3.81 寫法（文件第 2.2 節注意事項：TW16 讀取寬度與 3.81 寫法不同時，以 3.81 為準）。

## 做了什麼
- 新檔 `engine/source/PacketBoxStatus.cs`：以 M7-A 擴充點在一個方法 `ReceivePacketBoxStatus` 上疊 13 個 `[WorldReceive(250,Subtype=n)]`，另有 `[WorldReset]`、`[WorldSnapshot("packetBoxStatus")]`。**沒改 `WorldSession.cs`**。每包先完整解析、驗證（含 8 byte padding），通過後才改狀態；格式錯誤丟 `ProtocolException`，不留半套狀態。

| 子碼 | 3.81 來源 | 解析欄位 | 結果 |
|---|---|---|---|
| 53 | `S_PacketBoxCooking` | C×6 能力、H 食物、C 類型、C 圖示種類（7→0x24、54→0x2A、其他→0x26）、H 秒、C 0 | `MagicBuffs` 鍵 `-53000-類型`「料理 n」；最近一筆能力／食物值存進快照 |
| 56 | `S_PacketBox` DOLL | H 秒 | `MagicBuffs` `-3056`「魔法娃娃」 |
| 59 | `S_PacketBoxWaterLife`（DEL_ICON） | H(0) | 移除技能 170「水之元氣」的計時圖示（推定，見下） |
| 60 | `S_PacketBoxThirdSpeed` | C(秒>>2)、C(8) | `MagicBuffs` `-3060`「三段加速」，×4 秒、標為約略 |
| 75 | `S_PacketBoxDk` | C 階段 | 數值 `PacketBoxDkLevel` |
| 82 | `S_PacketBoxExp` LEAVES | C 值、C 0×3 | 數值 `LeavesValue` |
| 87 | `S_Karma` | D（有號） | 數值 `Karma` |
| 114 | `S_PacketBoxProtection` | D 種類 0–6、D 開關 | 集合 `Protections`（JusticeL1–3、EvilL1–3、Encounter）；未知種類只計數 |
| 132 | `S_PacketBox` EVASION_UPDATE | C | 數值 `EvasionRate` |
| 153 | `S_PacketBox` MAP_LIMIT_TIMER | D 秒 | `MagicBuffs` `-3153`「地圖限時」 |
| 159 | `S_PacketBoxMapTimer` | D 筆數、{D 順序、S 地圖名、D 分鐘} | 清單 `MapTimes`（新清單取代舊清單） |
| 160 | `S_WeaponRange` | C（-1 寫成 15） | 數值 `WeaponRange` |
| 180 | `S_PacketBox` NONE_TIME_ICON | C 開關、D 圖示、D 字串 id、H 0 | 無時間圖示集合 `NoneTimeIcons`（以圖示 id 為鍵；字串 id 只存數字，文字留給字串表任務 A） |

- 有倒數的 5 種（53、56、60、153，以及 59 的移除）走現有 `ReceiveBuff`，所以狀態列 `StatusIcons` 與之後原生 UI 的 buff-icons 模組不必改就讀得到；沒有時間的數值與圖示放在新屬性、`PacketBoxStatusChanged(子碼)` 事件和快照 `extensions.packetBoxStatus`，**本任務不畫圖示**。
- `engine/source/MagicStatus.cs`：`MagicStatusCatalog.Name` 補 170「水之元氣」與 `-3056`／`-3060`／`-3153` 的名稱。
- 新檔 `engine/source/PacketBoxStatusTests.cs`：`[WorldModuleTest("packetbox-status")]` 共 86 項，由現有 `--module-tests` 入口與 CI 步驟自動執行。
- `engine/_integration/dev/rebuild-lock.json`：`--update-lock` 重產（241 個來源、250 個鎖定檔）。

## 怎麼驗證（本機 Windows）
- `rebuild.py --update-lock` → `--build --configfile scratch/issue-548/NuGet.Config`（指到原專案 `engine/vendor`，唯讀）：PASS，productDll SHA-256 `53381c274a8e7ee3600c0e076b968ab6f01018c70cf3b96715e4fb74d56d892e`。
- `P11.ClientEngine.exe --module-tests`：結束碼 0，5 組 303 項全過（`packetbox-status` 86、`agit` 44、`castle` 62、`recovery-death` 72、`world-extensions` 39），Default 登錄表沒有錯誤。
- `packetbox-status` 涵蓋：13 個註冊項目、各子碼依 3.81 寫法合成的封包、料理三種圖示種類、倒數與 4 秒單位、開關集合、CP950 地圖名、空清單取代、全部被消化（沒有留在 `unhandledSubtypes`）、既有 S250/34 仍由 `MagicStatus` 處理、13 種格式錯誤一律丟例外且狀態與 buff 不變、Reset 清空。
- 既有回歸：`StatusIconChecks` 626 項 PASS；DemoChecks `item-status`、`afflictions`、`buddies`、`polymorph-cadence`、`hypertext` 都是 exit 0。
- `engine/tools/check-no-game-assets.py`：pass，0 錯誤。

## 沒驗什麼／推定
- 沒有真實伺服器封包，只用依 3.81 欄位簽章做的合成封包；沒啟動遊戲、伺服器或 DB。遠端 CI 結果以 PR 頁面為準。
- **59 DEL_ICON 要刪哪個圖示是推定**：3.81 只在水之元氣（170）結束時送，TW16 由「另函式讀」，本輪沒展開。引擎目前沒有任何封包會加入 170 的計時圖示，所以實際上只會計數。
- 82 LEAVES 的值意義（伺服器送「經驗÷LeavesSet.EXP」）、75 的階段意義、180 圖示 id 對應的圖檔，都只存數值，沒有畫面與文字。
- TW16 讀取寬度與 3.81 不同的子碼（56 `hhd`、82 `ddd`、160 `ccc`）照 3.81 寫法解析；原版在 3.81 下怎麼顯示待驗證。
- 3.81 `L1PacketProbe`（GM 測試指令）另有 `S_PacketBox(LEAVES,msg,value)` 的 `S H` 變體，正式流程不送，本任務不支援（會丟格式錯誤）。

## 產出路徑
- `engine/source/PacketBoxStatus.cs`、`engine/source/PacketBoxStatusTests.cs`、`engine/source/MagicStatus.cs`、`engine/_integration/dev/rebuild-lock.json`、本報告；分支 `task/548`。

## 給下一棒的提示
- **原生 UI buff-icons**：計時狀態直接讀 `WorldSession.MagicBuffs`（鍵見上表）；無時間圖示讀 `NoneTimeIcons`、`Protections`，訂閱 `PacketBoxStatusChanged` 或看 `MagicRevision` 變化即可重畫。180 的 `StringId` 等字串表任務 A 完成後查文字。
- 數值類（`Karma`、`EvasionRate`、`WeaponRange`、`PacketBoxDkLevel`、`LeavesValue`、`MapTimes`）可給角色視窗／地圖限時視窗使用。
- 本機建置照 #512 的作法：`py -3.12`、worktree 另寫 NuGet.Config 指到原專案 `engine/vendor`。
