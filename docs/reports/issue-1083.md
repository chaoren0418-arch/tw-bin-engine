# Issue #1083 — T479 執行期封存驗證：整包 pin 改成結構檢查＋錨點＋逐 entry（D1）

依 `docs/design/asset-recipe.md` 1.1 第 3 點、1.2、第 8 節 D1（T476／#1072 定案）。分支 `task/1083`。

## 結論

- 產品執行期不再用整包 pin（idx SHA、pak SHA、pak 大小）拒絕 Sprite／Tile 容器。`UiResourcePins` 的 17 個 Sprite 容器只剩名稱；`GroundSpriteReader` 自己那份重複的 pin 表與私有讀取器整個拿掉，改用同一個 `ClientArchive`。
- 三層驗證由一個共用元件提供（`engine/source/NativeUi/ClientArchive.cs`），準備器也改用它，全專案只剩一份規則：
  1. **結構**（`ClientArchiveIndex.Parse`，拒絕）：idx 大小 = 筆數 × 28 ＋ 4；每筆 offset＋size 不超出 pak；同包不重名（不分大小寫）；名稱非空、可列印；單筆不超過 2 GiB。
  2. **錨點**（`ClientArchivePin.Anchors`，拒絕並指出容器與 entry）：第一次讀取該容器時讀出錨點 entry 比對 SHA-256。
  3. **逐 entry**：已有 per-entry 核對的地方照舊（ground-catalog 的 `GroundSprite` pin：主包、record、offset、size、payload SHA、header、bounds、RGBA）。
- 主包優先與替代包唯一性（`sprites.md` §3.3）抽成 `SpriteContainerRule.Choose`，準備器的 `SprSource.TryRead` 與執行期 `GroundSpriteReader` 共用。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/NativeUi/ClientArchive.cs` | 新增 `ClientArchiveAnchor`、`ClientArchiveFormatException`（帶 `Container`／`Entry`／`Detail`，是 `UiResourceException`）、`ClientArchiveIndex`（`ReadIndex`／`Parse`／`Sha256`）、`SpriteContainerRule`。`ClientArchivePin` 改成 `(Name, Anchors)`，沒有整檔欄位。`ClientArchive` 第一次查找時開 pak（FileShare.Read，保持開啟、不能被換掉）並以 pak 實際大小做結構檢查；第一次讀取時核對錨點。`UiResourcePins.Tile` 的錨點 = 契約 1.2 的 Tile 三筆（`Water.lac`、`Pier1.lac`、`Pier5.lac`，值同 `ShipBackdrop.cs`）；`UiResourcePins.Sprites` 17 個只有名稱（1.2 在 Sprite 容器內沒有錨點，SPR 版本錨點是 Text 的 `list.spz`，不屬於這兩個讀取器） |
| `engine/source/GroundSpriteReader.cs` | 刪掉私有 `ArchivePin`／`Archive` 與 17 組 pin；改用 `UiResourcePins.Sprites`＋`ClientArchive`。新增公開 `ReadPayload(sprites, name, pin)`：有 pin 的列只收主包、且 record／offset／size／名稱與 catalog 相同；沒有 pin 的列照 §3.3（主包優先；替代包內容不同就拒絕，不挑一份）。容器錯誤包成 `InvalidDataException`（訊息含容器與 entry），`GroundRendering` 的「原圖缺件或 pin 不符」路徑不變 |
| `engine/source/AssetPrep/AssetPrepInput.cs` | `PrepArchive.Open` 改呼叫 `ClientArchiveIndex`（行為與訊息不變，只有「單筆 > 2 GiB」改成在結構檢查時就拒絕） |
| `engine/source/AssetPrep/SprShared.cs` | `SprSource.TryRead` 改呼叫 `SpriteContainerRule.Choose`（訊息不變） |
| `engine/source/MapRawSource.cs` | `PlainArchive.Open` 改呼叫 `ClientArchiveIndex`，仍丟 `InvalidDataException`（越界／重名訊息文字變長一點） |
| `engine/source/NativeUi/UiRoot.cs` | `VerifyResources` 註解改成新規則（行為：每包讀一筆 → 觸發錨點核對；不再雜湊整個 pak，啟動時少讀數 GB） |
| `engine/_integration/dev/native-ui-tests/ArchiveVerifyChecks.cs`（新） | `[CheckGroup("native-ui-resources",Order=3)]` 驗收測試，全部合成資料，見下 |
| `ResourceChecks.cs`、`CanvasChecks.cs`、`AssetPrepChecks.cs` | 依賴 pin 表形狀的測試改成驗新規則：整包 pin 不符的案例改成錨點不符／缺錨點／pak 比 idx 短／idx 有 pak 沒有；`ResourceChecks.cs:230` 改成驗 17 個 Sprite 只有名稱、Tile 錨點 = 1.2 三筆 |
| `OptionPanelTests.cs`、`WidgetSlotTests.cs`、`RefClientXmlSourceTests.cs`（產品內測試） | 只改 `ClientArchivePin` 建構方式 |
| `engine/_integration/dev/ground-tests/GenericGroundChecks.cs` | 原本用反射建私有 `ArchivePin`／`Archive`，改用公開 `ClientArchive`；「pak hash／idx hash／pak size」三個案例隨整包 pin 一起移除（此專案不在 CI，只確認能編譯） |

`ArchiveVerifyChecks` 涵蓋：

- (a) 合成 `Sprite03`：pak 尾端附加 2 個無關 `.img`、idx 多 2 筆（與 2026-11-09 的附加方式相同）→ 接受；三個原 entry 的 bytes 與 offset 不變；帶錨點也接受。同樣情形經 `UiResources`（surf 從附加過的容器取出、解碼像素相同；Tile 附加一筆＋帶錨點仍可讀 XML）與 `GroundSpriteReader.ReadPayload` 驗一次。
- (b) idx 少 1 byte、多 4 byte、筆數大於列數、最後一筆越界、offset 遠超 pak、同包重名（大小寫不同）、附加列與舊列重名 → `ClientArchive`、`PlainArchive`、`ClientArchiveIndex` 三邊都拒絕且訊息含 `Sprite07`（有 entry 時含 entry 名）；`ground reader` 遇到壞主包拒絕並寫出 `Sprite`。
- (c) 錨點 entry 改 1 bit（大小不變）→ 只查找不讀錨點、任何讀取都拒絕並寫出 `Sprite11` 與 `anchor.img`；缺錨點 entry 拒絕；Tile 錨點不符使 `UiRoot.VerifyResources` 拒絕（寫出 `Tile`、`Water.lac`）；ground reader 也拒絕並寫出 entry。
- §3.3：主包有就用主包（替代包內容不同也不看）；多個替代包內容相同 → 接受；替代包內容不同 → 拒絕並寫出兩個容器名；都沒有 → null；有 pin 的列在主包 → 讀到、record／offset 不同 → 拒絕、只在替代包 → 拒絕。

## 怎麼驗證

- 本機只編譯（依 2026-11-09 規定不執行任何編出來的程式）：
  - `dotnet build engine/source/OfflineClient.csproj -c Release -o engine/build/client-product-dev`：0 錯誤。
  - `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：0 錯誤。
  - `dotnet build engine/_integration/dev/ground-tests/GroundChecks.csproj -c Release`：0 錯誤、0 警告。
- 唯讀核對本機客戶端（python，只讀 `D:\lineage開發\0-客戶端\Tile.idx／Tile.pak`，腳本在 `scratch/tile_anchor.py`）：`Tile.idx` 152,492 bytes = 5,446 × 28 ＋ 4；`water.lac`（record 5445）、`pier1.lac`（5409）、`pier5.lac`（5413）的 SHA-256 與 `ShipBackdrop.cs`／新 Tile 錨點完全相同，所以本機 Tile 會通過錨點。
- CI（PR #1088，run 43445840433）：`dotnet`、`map-continuity`、`npc-offline` 全部 pass；「Native UI resource layer」步驟 `PASS 1411 DEMO checks`（`native-ui-resources` 群組，含新的 `RuntimeArchiveVerifyChecks`、既有 `NativeUiResourceChecks`、`AssetPrepChecks`、REF-SURF 等），`native-ui-canvas` 等其他群組也過。
- PR 不改 `engine/_integration/dev/rebuild-lock.json`。

## 未驗證什麼（待驗證）

- **本機客戶端下的實際畫面**：原生 UI 能否啟動（之前照原始碼會因 Sprite03–14 的 pin 被拒而退回舊介面）、surf 圖、地面物品圖。交 **T474 真機驗收**。
- 本機客戶端的 17 個 Sprite 容器是否都通過新的結構檢查：照 `sprites.md` §8.1，附加的 36 個 `.img` 都在舊檔尾之後、不重名，推測會通過；本張沒有在本機執行產品驗證。

## 範圍外、仍有整檔 pin 的地方（沒改，給後續參考）

- `Ambience.cs`（`AmbienceLightImages.IndexSha256`／`PakSize`）、`ObjectRendering.cs`（crown.raw）、`WeatherSprite.cs` 仍 pin **`Sprite.idx` 整檔 SHA 與 `Sprite.pak` 大小**。主包 `Sprite` 在本機沒有被改，所以目前不受影響；但可攜版遇到「主包多了無關 entry」的客戶端會被擋。不在本張範圍（本張只列 `UiResourcePins` 與 `GroundSpriteReader`），建議另開任務改用 `ClientArchive`。
- `GroundSpriteReader` 的 gfx → sprite 對應仍讀 `TW13081901.txt`（整檔 SHA pin），契約 1.1 第 5 點要求改用 `list.spz`；本張不動。
- ground-catalog 的 `GroundSprite` pin 仍比 record 與 offset（「照現有方式核對」）；契約 1.1 第 2 點說找 entry 只用檔名。主包沒變所以本機不受影響；若之後要讓別版主包也能用，可只留名稱、大小與 payload SHA。

## 產出路徑

- 程式：`engine/source/NativeUi/ClientArchive.cs`、`engine/source/GroundSpriteReader.cs`、`engine/source/AssetPrep/AssetPrepInput.cs`、`engine/source/AssetPrep/SprShared.cs`、`engine/source/MapRawSource.cs`、`engine/source/NativeUi/UiRoot.cs`
- 測試：`engine/_integration/dev/native-ui-tests/ArchiveVerifyChecks.cs`（新）及上表所列
- 報告：`docs/reports/issue-1083.md`

## 給下一棒的提示

- 新的容器讀取一律用 `ClientArchive`（或至少 `ClientArchiveIndex.Parse`），不要再寫第三份 idx 解析；要「這是支援版本」的保證就加 `ClientArchiveAnchor`（只放雜湊）。
- `ClientArchive` 現在在**第一次查找**就開 pak（以前是第一次讀取）：idx 在、pak 不在會在 `Find`／`Entries` 時就丟 `UiResourceException("…: 缺少 pak")`。
- T474 真機驗收時請特別看：原生 UI 是否不再退回舊介面（`UiRoot.PinMismatchMessage`）、背包／buff 圖示、地面物品；若被拒，`native-ui.log`（`--evidence`）會寫出容器與 entry 名稱。
- 上面「範圍外」三項建議由規劃另開 code 任務。
