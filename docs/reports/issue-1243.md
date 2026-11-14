# Issue #1243 — f1211-player-appearance：自己的外觀沒有 profile 就整個世界停擺（gfx 138、變身 1062）

日期：2026-11-14（台北）。執行者：claude-local，第 1 次嘗試。分支：`task/1243`（從 `origin/main` `6cbe4592` 建立）。

## 結論

- **根因（已驗證，靜態讀碼＋原客戶端資源）**：自己的外觀只能走「玩家規則」（`PlayersRecipe`：姿勢的 7 個動作全部要有，SPR 像素不得落在幀 bounds 外）。規則做不出來時，`PlayerProfiles.Resolve` 回 `null` 且不是 pending，`WorldSession.SupportedAppearance` 變 false，`Renderable` 跟著 false，整個世界停擺。
  - **138**（3.81 男妖精，基本職業）：`list.spz` 七個動作都有，sprite 檔也全在。被拒收的原因是「不透明像素落在幀 bounds 外」（L7，r29 基線 manifest 的 unavailable 原文 `opaque pixel outside frame: 22,-48 versus (57, -3, 121, 25)`）。
  - **1062**（drake）：`list.spz` 沒有動作 18、19，在玩家規則的 `missing complete action 18/spellDir` 被拒。
  - **1461**（great dane）：只有動作 0、3、4，在 `missing complete action 1/attack` 被拒。
  - **1395／1410**（椅子）：沒有任何動作，屬於靜態外型。
  - 這幾個 gfx 都**不缺資源**，PakViewer 查過全部 19 組封存檔，清單見第 3 節。
- **修正**：玩家規則做不出來時，對「自己認領的 gfx」（S87／S76 給的 gfx）改走**寬鬆建置** `RuntimeAppearances.Lenient`。做法是 gfx 有哪些姿勢動作就用哪些（姿勢＋0..3、8、18、19），時間照 R1，不做 bounds 拒收（畫面是逐 tile 畫的，不依 bounds 裁切）。修正後 138、1062、1461 都有 profile，世界不再停擺：可以移動（stepMs＝walk ticks × 40）、畫面照 sprite 表畫、可以卸裝。1062 可以攻擊（攻擊動作存在）；施法時照實提示「沒有施法動作的資料（資料未到）」，不丟例外。1461 沒有攻擊動作，攻擊時同樣照實提示。
- **沒做、要裁定（`needs:claude`）**：連寬鬆建置都做不出來的外型，目前仍維持原本的停擺。這類外型是沒有任何玩家動作的靜態外型（1395／1410／5940），或 SPR 讀不到的 gfx。要把它改成「仍可移動、戰鬥」會和既有設計衝突，衝突點與選項見第 5 節。另外，寬鬆建置讓 138 在 T470 通過前就能在執行期顯示，和 L7「138 放寬等 T470 後另開」的字面有出入，也列在第 5 節。
- 七職男女基本外型裡，除了 138，其他 13 個都已在預建 manifest，行為不變。

## 1. 每個 gfx 的判定（已驗證）

`list.spz` 用 `docs/reference/rea-scripts/t899/listspr_compare.py` 的解法唯讀解出，腳本在 `scratch/issue-1243/listspz_entries.py`，輸出 `scratch/issue-1243/listspz-entries.json`，不進 git。檔數一欄是 PakViewer 列出的 `<sprite>-<n>.spr` 數量，與表頭的檔數相同。預建一欄指 r29 `players/manifest.json`（`engine/assets` → `releases/r29/assets`）。

| gfx | list.spz 表頭 | 有的動作（節錄） | sprite 檔 | 預建 profile | 玩家規則（修正前） | 修正後 |
|---|---|---|---|---|---|---|
| 0 | `#0 312=3225 prince` | 0–8、18–27、40–55… | 3225：312 檔 | 0／4／20／50／46 | 預建 | 不變（預建） |
| 1 | `#1 312=3227 princess` | 同上 | 3227：312 | 0／4／20／50 | 預建 | 不變 |
| 61 | `#61 320=3213 knight` | 同上 | 3213：320 | 0／4／20／50 | 預建 | 不變 |
| 48 | `#48 320=3215 knight female` | 同上 | 3215：320 | 0／4／20／50 | 預建 | 不變 |
| **138** | `#138 272=3217 elf male` | 0–8、11–27、40–49、66–71 | 3217：272 | **無**（0／4／20：`opaque pixel outside frame: 22,-48 versus (57, -3, 121, 25)`；50：缺 50/walk） | 拒收 → 世界停擺 | **寬鬆建置**：0／4／20 各自建出（動作都在） |
| 37 | `#37 272=3474 elf female` | 同 138 | 3474：272 | 0／4／20（50 缺 walk） | 預建；姿勢 50 退回 0 | 不變 |
| 734 | `#734 312=3476 wizard pc male` | — | 3476：312 | 0／4／20 | 預建 | 不變 |
| 1186 | `#1186 312=3221 sorceress` | — | 3221：312 | 0／4／20 | 預建 | 不變 |
| 2786 | `#2786 394 darkelf pc male` | — | 2786：386 | 0／4／20 | 預建 | 不變 |
| 2796 | `#2796 384 darkelf pc female` | — | 2796：376 | 0／4／20 | 預建 | 不變 |
| 6658 | `#6658 288 Dragon knight male` | — | 6658：288 | 0／4／50（20 缺 walk） | 預建 | 不變 |
| 6661 | `#6661 288 Dragon knight female` | — | 6661：288 | 0／4／50 | 預建 | 不變 |
| 6671 | `#6671 248 Illusionist male` | 沒有 4–7 | 6671：248 | 0／20（4、50 缺 walk） | 預建 | 不變 |
| 6650 | `#6650 248 illusionist female` | 沒有 4–7 | 6650：248 | 0／20 | 預建 | 不變 |
| **1062** | `#1062 88 drake` | 0、1、2、3、4、7、8、30、44、45、101.shadow(1063)、108、111.stride(1) | 1062：88（只在 Sprite） | 無 | 缺 18/spellDir → 世界停擺 | **寬鬆**：walk／attack／damage／breath／die；施法照實拒送 |
| **1461** | `#1461 24=1353 great dane_ii` | 0、3、4、101.shadow(1354)、105.clothes(1362)、111.stride(1) | 1353：24（只在 Sprite） | 無 | 缺 1/attack → 停擺 | **寬鬆**：walk／breath（姿勢 4 另有 walk）；攻擊照實拒送 |
| 1395 | `#1395 1 a l chair a` | 沒有動作；`112.altdir(1410)` | 1395：1 | 無 | 缺 0/walk → 停擺 | 寬鬆也不可用（沒有任何動作）→ **仍停擺**，原因寫進 notice 與 native-ui.log；待裁定 |
| 1410 | `#1410 1 a r chair a` | 沒有動作 | 1410：1 | 無 | 同上 | 同上 |

補充：
- 1062、1461 的「玩家規則拒收原因」是照 `SprRecipesB.cs:135–137`（7 個動作依 walk→attack→damage→breath→die→spellDir→spellNoDir 的順序檢查，遇到第一個缺的就拒）對上表的動作清單推得，屬於**靜態推得**。#1211 的 native-ui.log 沒有任何 `runtime-appearances` 行：修正前，被拒的按需項目不會寫 log。這次已補上（見第 2 節），之後重測可以直接看到原文。
- 修正前 `players/manifest.json` 的 15 筆 unavailable 與 L7 相同，這次**沒有改**準備器與 manifest，T470 的比對對象不變。

## 2. 修改前後（行號是修改後的 `task/1243`）

修改前的根因行號（`origin/main` `6cbe4592`）：
- `engine/source/PlayerProfiles.cs:61–77`：`Resolve` 依序找「預建 → 按需玩家規則 → 姿勢 0」。都不可用時 `:70`（`if(weapon==0||!legal)return null;`）與 `:76` 直接回 `null`，沒有其他來源。
- `engine/source/WorldSession.cs:26`：`SupportedAppearance=>AppearanceProfile!=null&&!SelfAppearancePending`；`:28`：`Renderable=>…&&SupportedAppearance`，整個世界停擺。
- `engine/source/AssetPrep/SprRecipesB.cs:137`（缺動作就拒）、`:177–183`（像素在 bounds 外就拒，註解明寫 gfx 138、L7）。
- `engine/source/ActionTiming.cs:63–67`、`engine/source/ActionCadence.cs:35–36`：動作時間只會從 `profile.Path` 讀檔。T478 按需建出的玩家 profile 只有虛擬路徑，攻擊節奏 `ActionCadence.Capture` 會讀不到檔。這一點是讀碼推測，沒有實跑；這次一併改掉。

修改內容：

| 檔案 | 改了什麼 |
|---|---|
| `engine/source/RuntimeAppearances.cs:77、104、176–200、209` | 新增 `LenientKey`、`Lenient(gfx, weapon)`、`BuildLenient`。用 `SprCreature.PlayerActions(weapon)` 裡 entry 有的動作，配上 `SprBody`、`ObjectsRecipe.CreatureModel` 與 `ObjectAppearance.FromCreature`。stepMs＝walk ×40；姿勢 0 沒有 walk 時用 breath（同 creature 規則）。actionMs 照 players 檔（walk ×40，其餘 floor）。不做 bounds 拒收，只存在記憶體，不寫 assets。被拒的按需項目一律寫一行 `runtime-appearances unavailable <key> (<failure>): <reason>` 到 native-ui.log，每個項目只寫一次。 |
| `engine/source/PlayerProfiles.cs:70–96` | `Resolve`：原本回 `null` 的地方，若 gfx 是按需對象（自己認領的或預建職業 gfx），改依序試「寬鬆姿勢 → 寬鬆姿勢 0」。預建與既有退回的順序不變；非法姿勢、未認領的 gfx 不建。新增 `UnavailableReason`、`PlayerProfile.Lenient`；`Runtime` 加 `JsonIgnore`，snapshot 不會序列化外觀。 |
| `engine/source/ActionTiming.cs:63–76` | `ServerMs`／`CastReleaseMs`／`ProjectileReleaseMs` 改走 `StepsOf(profile, action)`：有 `Runtime` 就取外觀裡同一段 ticks／事件，沒有那個動作時和檔案一樣丟 `InvalidDataException`。新增 `Has`。 |
| `engine/source/ActionCadence.cs:34–48、71、192` | `Capture` 新增按需 profile 的分支（不讀檔、不快取）；新增 `Supported(gfx, weapon, action)`；他人／自己的 S158 節奏只在動作存在時才擷取。 |
| `engine/source/Combat.cs:104、117–118`、`engine/source/Magic.cs:128` | 外觀沒有攻擊或施法動作時不送出，提示「沒有…動作的資料（資料未到）」並寫 native-ui.log，不丟例外。 |
| `engine/source/WorldSession.cs:27–30` | 不支援時的提示加上「外觀資料未到（原因）」。`Renderable` 判斷式不變（寬鬆 profile 讓 `SupportedAppearance` 成立）。 |
| `engine/_integration/dev/asset-prep-tests/OwnAppearanceFallbackChecks.cs`（新） | `[CheckGroup("own-appearance-fallback")]`，同時掛在 CI 已在跑的 `polymorph-cadence` 與預設群組。內容見第 4 節。 |

沒改：`Program.cs`、`DemoChecks.csproj`、註冊表、`rebuild-lock.json`、準備器 recipe、`players/manifest.json`、設計文件。

## 3. PakViewer 查詢清單（已驗證，唯讀）

- 工具：`D:\lineage開發\PakViewer-main\src\PakViewer.Cli\bin\Release\net10.0\pakviewer-cli.exe pak list <idx>`。
- 查的封存檔：`D:\lineage開發\0-客戶端` 全部 19 個 `.idx`（38 個 idx／pak 檔）：`Sprite.idx`、`Sprite00.idx`–`Sprite15.idx`、`Text.idx`、`Tile.idx`。輸出在 `scratch/issue-1243/pv/*.txt`，Sprite.idx 有 92,359 列，Text.idx 有 37,501 列。
- 查的檔名：`<sprite>-<n>.spr`，sprite 是 3225、3227、3213、3215、3217、3474、3476、3221、2786、2796、6658、6661、6671、6650、1062、1353、1395、1410。另外查了 `Text.idx` 的 `list.spz`。腳本 `scratch/issue-1243/spr_presence.py`，結果 `scratch/issue-1243/spr-presence.json`。
- 結果：每個 sprite 的檔數都等於 `list.spz` 表頭的檔數（例如 3217 有 272 檔，在 Sprite 與 Sprite01–03、06–14），1062／1353／1395／1410 只在主包 Sprite。**沒有缺資源**；問題全在規則。

## 4. 怎麼驗證

- **已驗證（本機編譯）**：`DOTNET_CLI_HOME` 設在 `scratch/issue-1243/dotnet-home`，同時設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`；NuGet 來源是原專案的 `engine/vendor`（臨時設定在 `scratch/issue-1243/NuGet.Config`）。
  - `dotnet build engine/source/OfflineClient.csproj -c Release -o engine/build/client-product-dev`：0 錯誤，4 個既有警告（與 #1055 記錄的相同）。
  - `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：0 錯誤（新檢查可以編譯）。
  - `OfflineClient.csproj -p:EnableWindowsTargeting=true -r win-x64 -p:SelfContained=false`：0 錯誤。
  - 依本機限制，**沒有執行**任何編譯出來的程式，檢查交給 PR 的 CI。
- **離線檢查內容**（`OwnAppearanceFallbackChecks`，合成 list.spz／17 個 Sprite 容器，寫在 `scratch/issue-1243/run-*`）：
  1. 玩家規則照舊拒收：138（bounds）、1062（缺 18）、1461（缺 1）。L7 不變。
  2. 138 的寬鬆建置：七個動作八方向、stepMs 160、`PlayerProfiles.Check` 通過、幀與 tile 一個不少；姿勢 4 退回寬鬆姿勢 0，而且是同一個實例。
  3. 1062：沒有 18／19；attack 的 R1＝320、walk＝160；缺動作時丟 `InvalidDataException`；放出點取自外觀事件。
  4. 1461：只有 walk／breath，stepMs 80。
  5. 1395（沒有動作）與 SPR 不在容器的 gfx：照實不可用，原因可查，不拿別的 gfx 湊。
  6. 既有順序不變：預建優先；預建職業的缺姿勢退回預建姿勢 0，不走寬鬆；未認領的 gfx 不建；非法姿勢不建；沒有按需來源時只用預建；已完成與不可用的項目都不重建。
  7. `WorldSession`：自己是 138、變身 1062 時 `SupportedAppearance` 為真、提示「角色外觀已載入」、移動節奏取自寬鬆 walk；1062 攻擊節奏可以擷取，施法回報不支援。變身 1395 時提示含「外觀資料未到」，再變回 138 恢復正常。
  - `Renderable` 的其他條件（Ready、地圖）在這組檢查不成立，所以只驗 `SupportedAppearance`，也就是 `Renderable` 裡外觀的那一項。
- **推測（沒有實跑）**：
  - 138 的 SPR 有 bounds 外的像素，畫面照 tile 位置畫出，不會被裁切（`Renderer.Actor`、`ObjectRendering.cs:20` 都是逐 block 畫）。實際好不好看要看真機。
  - 1062／1461 的自己移動、走路 stride、影子（101.shadow）與 cloth 會照既有的按需路徑出現。

## 5. 需要裁定（`needs:claude`）

1. **連寬鬆建置都做不出來的自己外型，仍然停擺**（1395／1410／5940 這類沒有動作的靜態外型，或 SPR 讀不到的 gfx）。
   - 衝突點：
     - `PlayerProfiles.cs:13–15` 的設計註記：「自己沒有 1120 退回，由 AppearanceNotice 告知」。
     - `engine/source/WorldRegression.cs:25` 的回歸檢查：不支援的 gfx 必須 `!Renderable&&!SupportedAppearance`，訊息是「unsupported gfx substituted」。
     - `engine/source/EquipmentTests.cs:26`：未知武器 255 必須讓外觀不支援。
     - 移動節奏（`AppearanceProfile.StepMs`）在沒有任何動作的 gfx 上沒有來源。
     - 另一方面，`docs/reference/m6-native-diff.md:71、126`（G3）主張「不擋移動與戰鬥」。
   - 選項：
     - A. 維持現狀（本 PR 的做法）：提示寫明「外觀資料未到（原因）」並寫 log。
     - B. 自己也照 FU14 §1.1 的靜態分支畫 `<gfx>-0.spr`（朝向 6 換 altdir），移動節奏另定來源（例如沿用變身前外型的 stepMs，或伺服器值）。這需要新的設計，也要改上述兩個回歸檢查。
     - C. 不畫自己，但放行移動與戰鬥（節奏同 B 要另定）。
2. **138 在執行期放寬，與 L7 的字面不同**。`docs/design/asset-recipe.md:379` 寫「gfx 138 放寬等 T470 通過後另開，現在不開」，而 T470（#1055）目前未通過。本 PR **沒有改準備器與 manifest**，T470 的比對不受影響；只是在執行期多了寬鬆建置。
   - 選項：
     - A. 接受，並在 asset-recipe.md L7 補一句「執行期寬鬆建置由 #1243 處理」。
     - B. 不接受：在 `BuildLenient` 恢復 bounds 拒收（只放寬缺動作的情況），138 改等 T470 之後的任務。這樣基本職業妖精會繼續停擺。
3. **變身外型缺攻擊或施法動作時拒送**。1461 不能攻擊，1062 不能施法，送出前就照實提示。原版在這種情況下送不送、節奏怎麼算，**沒有查證**。
   - 選項：
     - A. 維持拒送。
     - B. 照 AN§3.4 的動作退回（姿勢 walk）當節奏來源照送，這要另開 REA 查證。

## 6. 待驗證（交 `f1211-retest`）

- SM20 妖精：登入煙妖甲（gfx 138，objid 1900252003）。世界應正常顯示，可以走、打、開魔法窗。看 native-ui.log 有沒有 `runtime-appearances unavailable players/138-0 (Unavailable): opaque pixel outside frame…`，這一行是預期的玩家規則拒收；之後不應再有 `players-lenient/138-0` 的拒收。
- SM43（妖精盲目光圈）、SM48（七職施放）。
- SM58：
  - `.poly 煙法甲 1062` 後改回 GM0 再登入：應能移動，量 stride（111.stride(1)）。施法應看到「沒有施法動作…（資料未到）」的提示。
  - 1461：應能移動，量 stride；攻擊時應看到提示。
  - 1395→1410 altdir：自己變身成 1395 時預期**仍停擺**，提示含「外觀資料未到」，見第 5 節第 1 點。他人的 altdir 不受本 PR 影響。
- 138 的畫面是否與原版一致（bounds 外的像素是否正確顯示）。

## 產出

- 程式：上表 7 個 `engine/source/*.cs`；檢查：`engine/_integration/dev/asset-prep-tests/OwnAppearanceFallbackChecks.cs`；報告：本檔。
- 本機暫存（不進 git）：`scratch/issue-1243/`（list.spz 解析、PakViewer 清單、建置輸出）。證據圖只看，沒有複製。

## 給下一棒的提示

- 寬鬆建置只對「按需對象」生效（自己認領的 gfx 與預建職業 gfx），offline 測試 `RuntimeAppearances.Active==null` 時行為完全不變。
- 寬鬆項目的 key 是 `players-lenient/<gfx>-<weapon>`，在 native-ui.log 用這個字串就能找到它被拒的原因。
- 若第 5 節第 2 點選 B，只要在 `RuntimeAppearances.BuildLenient` 對每個 `body.File(sid)` 補上 `FirstPixelOutsideBounds` 檢查即可，`OwnAppearanceFallbackChecks` 的第 2 段要跟著改。
