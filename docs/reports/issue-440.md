# #440 T168：名稱、稱號、盟徽與名稱顏色對齊原版（code，第 1 次嘗試）

依據：`docs/reference/actions-and-names.md`（T160／#428）第 2 節，第 6 節差異表 #7–#15、#19。分支 `task/440`。

## 做了什麼

| 差異 # | 內容 | 檔案 |
|---|---|---|
| 7、8、19 | 平常**只畫滑鼠指到的物件**的稱號與名稱；稱號在名稱上一行（稱號 `T−3`、名稱 `T+10`，無稱號時名稱 `T−3`）；X0＝L＋(W−6·Big5 bytes)/2；隊伍成員且非自己 −9。字型 MingLiU 12 px、上下左右 ±1 px 黑外框、稱號與名稱同色，座標隨相機縮放。新增「全部顯示名稱」選項（分頁「名稱顯示」；全名模式 `T−16`、不畫稱號、戰爭中的 +196 角色不畫）。移除舊的 LightYellow「稱號＼n名稱」場景疊字與目標「名稱 #id」金字 | `ObjectRendering.cs`、`TitleAndEmote.cs`（只剩表情合成；新增 `DisplayTitle` 稱號資料） |
| 9、14 | **移除**「名稱 [血盟名]」淺青標籤、敵盟紅底標籤、「決鬥對手 · 名稱」標籤 | `ClanUi.cs`、`ClanWar.cs`、`DuelUi.cs` |
| 10 | 盟徽 16×12 畫在名稱基準 −20、`T−4`（有稱號 +13），有血盟 id 時名稱右移 10。頭上盟徽置中、`T−23`：自己有血盟時畫在同盟成員頭上（`sub_F09070`），戰爭中另畫在敵盟成員頭上（`sub_F08C30`） | `ObjectRendering.cs` |
| 11 | 缺盟徽自動送 C72 `D(id)`：同一 id 不重送、最多 32 筆待回覆；記憶體 512 筆 LRU；磁碟快取 `engine/runtime/user/emblems.bin`（不入 git，不寫原客戶端目錄）；快取不再隨換圖清除。S118 另支援 TW16 384-byte RGB555（0 透明） | 新檔 `ClanEmblem.cs`、`Clan.cs` |
| 12 | 名稱顏色依正義值公式（2.4）；新增 S34 `S_Lawful` 接收（`WorldSession.cs` 只加 `case 34` 一行）；gfx 2715 非戰爭時紅名 | `DisplayNotices.cs`、`WorldSession.cs` |
| 13 | 粉紅名改成名稱色 `0x767D`（不再另畫 HotPink 字），**127＝永久**，0＝立即清除 | `DisplayNotices.cs` |
| 14、15 | 決鬥對手、敵對血盟（+197＝2）、粉紅名**不改名稱顏色**，只影響「可直接攻擊」：滑鼠指到時游標改十字，左鍵單擊即攻擊 | `ClanWar.cs`（`ClanWarFlag`、`CanAttackDirectly`）、`DuelUi.cs`（游標）、`PlayerPointerUi.cs` |

**超出任務列出檔案的最小掛接（請審查確認）**：
- `ClanUi.cs`：刪除 `PaintClanLabels`（「[血盟名]」標籤的實作所在，主控裁定要移除）。
- `PlayerPointerUi.cs`：左鍵攻擊條件加一個 `||DirectAttackTarget(target)`，讓「可直接攻擊」判定實際生效。
- `WorldSession.cs`：只加 `case 34:ReceiveLawful(r);break;`。

## 怎麼驗證

- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock`（220 來源、229 鎖定檔）→ `--build` 通過（product、audio、launcher）。產品 DLL SHA-256：`95218e9f9d62796554b5d247f04431c2b7384021ea428c39600b58c79b4e7f62`。
- 新測試 `engine/_integration/dev/name-display-tests/NameDisplayChecks.cs`（README 同目錄），接上 `DemoChecks` 的 `name-display` 群組、`map-continuity`（CI 會跑）與完整回歸入口。本群組 87 項全過（群組共回報 363 項，含 276 項每次都跑的共用檢查），涵蓋 S34／S60／S87／S118（BMP 與 384-byte）／S84／S250-sub5 與 C72 每個格式，細目見 README。
- 既有群組重跑全過：`display-notices` 337（新增 127 永久、`0x767D` 3 項）、`title-emote` 427（改寫舊的稱號疊字測試為「不再改動場景 patch」與稱號資料提供）、`clan` 395、`clan-war` 479、`duel` 390、`map-continuity` 23064、`combat-admission` 1560、`afflictions` 428、`item-status` 358、`buddies` 445、`item-transfer` 349、`doors` 597。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：pass，3511 個追蹤檔、0 錯誤。
- 本機還原用 `scratch/issue-440/nuget.config` 唯讀指向原專案 `engine/vendor`。
- **2026-11-08 同步 main（落後 42 個 commit）**：`demo-tests/Program.cs` 衝突取聯集（保留 `NameDisplayChecks` 與 main 新增的 `MapAmbientChecks`），`rebuild-lock.json` 取 main 版後 `--update-lock` 重產（221 來源、230 鎖定檔）；`--build` 通過，產品 DLL SHA-256 `e0bb83198541661eb926edbfb0bedd004fcdaeb3adf09f18b9057e7dde1871f7`。重跑 `name-display`、`map-continuity`、`display-notices`、`title-emote`、`clan`、`clan-war`、`duel`、`combat-admission`、`item-status`、`afflictions`、`buddies`、`item-transfer`、`doors`、`map-ambient` 共 14 群組全為 pass；`check-no-game-assets.py` pass（3546 追蹤檔、0 錯誤）。

## 沒驗證什麼

- **沒有真機**：字型實際外觀（MingLiU 是否存在、外框粗細）、與原版逐像素位置、十字游標、左鍵單擊攻擊的手感、實際伺服器盟徽格式與 emblem id 是否等於 clan id，都待 M6 smoke 由 dot 驗收。
- 不帶群組的完整回歸在本 worktree 跑不完：`Movement123` 讀 `engine/assets/terrain0.bin`，worktree 沒有 `engine/assets` junction（與本次改動無關；CI 不跑這個入口）。
- 同步前 PR #454 的 CI（dotnet、map-continuity、npc-offline）全綠；同步後的 CI 結果待 push 後確認。

## 推測或未做（照任務規則不做或維持現狀）

- 自己的名稱 X0：原版是「腳下格螢幕 X − 3·len − 56」，引擎座標系不同，改用與他人相同的外框置中公式。
- R（`sub_A3ED50`）用引擎最終 actor 圖塊（24 px 格）外接矩形近似；自己的滑鼠指到判定用自己的外接矩形（自身像素沒有物件 id）。
- 全名模式的盟徽位置（`sub_F02870` 未反編譯）沿用滑鼠指到時相對名稱的偏移，標為推測。
- 同盟名單（文件標推測）、+181 圖示、隊伍／主人圖示、+252 行、物件種類 2／3／9／10 過濾、怪物等級差外框、頭上盟徽在自己／隊伍成員時的 −12：未做。
- 原版攻擊游標圖未載入，用系統十字游標代替。
- 「全部顯示名稱」：專案沒有既有的本機設定檔，選項存在 `engine/runtime/user/name-display.txt`。

## 產出路徑

- 程式：`engine/source/{ObjectRendering,DisplayNotices,TitleAndEmote,ClanEmblem,Clan,ClanUi,ClanWar,DuelUi,PlayerPointerUi,WorldSession}.cs`
- 測試：`engine/_integration/dev/name-display-tests/`；修改 `display-notices-tests`、`title-emote-tests`、`demo-tests/{DemoChecks.csproj,Program.cs}`
- 文件：`docs/protocol/name-display.md`（新）、`docs/protocol/clan.md`（附加段落）、本報告
- lock：`engine/_integration/dev/rebuild-lock.json`（`--update-lock` 重產）

## 給下一棒的提示

- **T135（#316）會碰 `Clan.cs`**：本任務把盟徽快取移到 `ClanEmblem.cs`（`WorldSession.Emblems`），`clanEmblems` 字典已刪除；合併衝突時以 `Emblems.Store/Reject/Find` 為準。若 T135 建立 emblem id 映射，`ObjectRendering` 的 `NameLayout.Plan` 目前用 S87 clan id 當盟徽 id，可在 `WorldSession.ClanIdentity` 換成映射後的 id。
- M6 smoke 核對清單：滑鼠指到他人／自己／NPC 時名稱與稱號位置；全名選項；粉紅名（PK 後）顏色、127 永久；決鬥中對手名稱不變色但游標變十字、單擊即攻擊；戰爭中敵盟頭上盟徽；`engine/runtime/user/emblems.bin` 有產生且重開後盟徽直接出現。
