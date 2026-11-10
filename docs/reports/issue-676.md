# Issue #676：T283（R3-H）盲目光源與酒醉、隱身細項（第 1 次）

依據：`docs/reference/gap-matrix.md` 第 12.5 節 R3-H；AM＝`docs/reference/ambience-and-map-events.md` §1(f)(h)、§3(c)、§4(b)。

## 結論

依前置說明，本輪**只做盲目**：

- 盲目 2 讓 list.spr 欄位 102（102 表）為 {5,6,10,12} 的物件也有 k=5 光源，已接上。
- 職業條件 6/31 的規則已接上，但產品還沒有輸入來源，所以預設不會生效。

其餘項目照規定跳過：

- 刪除 `Renderer.PaintAmbience`：要等 #607（R2-O），目前 #607 還沒關，本輪沒刪。
- `LiquorFullWave`（Q3-3）、隱身例外（Q3-5）、光效 +5 的觸發（Q3-4）：都要等 T287（#680）查證，#680 還沒關，本輪沒動。
- 因此 `Invisibility.cs`、`TimedBuffs.cs`、`Ambience.cs` 都沒改。

## 做了什麼

只改 `engine/source/Afflictions.cs`，並新增 `engine/source/AfflictionFollowupTests.cs`。

1. **`SpriteTypeTable`**（新類別，放在 `Afflictions.cs`）
   - 對應 TW16 的表 `0x13CD838`，也就是 list.spr 欄位 102（`102.type(N)`），由 `sub_87C890` 載入，預設值 1（REA B07）。
   - 執行時從原客戶端 `Text.idx`／`Text.pak` 唯讀讀取：有 `list.spz` 時先用 AES 解密（做法同 `EffectSpriteCatalog.LoadMetadata`），否則讀 `list.spr`。不複製任何資源。
   - alias（`#N M=K`）只認它自己寫的欄位 102。alias 會不會繼承來源的值，沒有查證過。
2. **`AfflictionColors`**
   - 新增 `BlindTwoGlows`：種類為 {5,6,10,12} 時發光（AM§3(c)）。
   - `BlindCoefficient` 新增 `classBonus` 參數。為真時改用表 mode 2 的第 3 級（AM§1(f)(h)、§4(b)），未照亮處是 6/31；不帶參數時行為不變。
3. **`WorldSession.BlindClassBonus`**
   - 對應 TW16 的 `byte_1542AA1 ∈ {2,4}`。
   - 寫入這個 byte 的封包還沒查到（和 `AmbienceFrame.NightVision` 是同一個缺口），所以 session 不會設它，產品一律是 4/31。
4. **`Renderer.ApplyBlindLight`／`BlindLightCenters`**
   - 盲目 2 時，光源清單是：先放自己，再加這次合成中有畫出來的其他物件裡、102 種類會發光的那些，每個 k=5。
   - 光源預算照 TW16：k 總和到 700 就停。
   - 有 `Source.lmp` 時，全部蓋進一張以自己腳下為中心的 400×240 光照圖，重疊處取最大值（`sub_97D2F0`）。
   - 沒有 `Source.lmp` 時，每個光源用原本的替代圓形，取最大值。
   - list.spr 在第一次合成盲目 2 時讀一次。讀不到時記在 `BlindSpriteTypeNotice`，只留自己的光（CI 就是這種情況）。
   - 測試可用 `SpriteTypesOverride` 換掉種類表。
   - 只有自己一個光源時，結果和 #612 的 k=5 蓋章逐像素相同（測試有比對）。

## 怎麼驗證（已驗證）

- `rebuild.py --build --configfile .github/nuget.ci.config`：**PASS dev**。用的是暫時 lock，**沒有改 `rebuild-lock.json`**。productDll SHA-256 `86d264c7831cd07e18f12cf068d6b62683e761eca73224cac44bdee8ab959199`。
- `DemoChecks.csproj` 只編譯，0 錯誤（`afflictions-tests` 仍呼叫 `BlindCoefficient(mode,light)`，相容）。
- `check-no-game-assets.py`：pass，0 錯誤。
- 新測試組 `[WorldModuleTest("affliction-followup")]`（自動發現），內容：
  - 發光種類集合
  - 職業條件下的係數列（6/31～31）
  - list.spr 欄位 102 解析：CRLF、同一行多個欄位、alias 用預設值、超過 byte 的值不採用、缺客戶端時丟錯
  - 光源中心：只有盲目 2、只有畫出來的物件、種類 9 不發光、預算 140 個
  - 像素結果：
    - 怪物腳下全亮、遠處 4/31
    - 盲目 1 時怪物不亮
    - 讀不到 Source.lmp 時用替代圓形
    - 職業條件下未照亮處是 6/31
    - 只有自己時與舊結果逐像素相同
- 依本機限制，**沒有在本機執行測試**，結果以 PR 的 CI（windows `--module-tests`）為準。

## 推測（已寫在程式註解）

- 其他物件的光源中心取「目前格子的腳下」。走路中原版可能會跟著步伐位移。
- 原版的 ±35／±15 欄位距離過濾沒做，光照圖只在 800×480 視窗邊界裁掉（同 `AmbienceLights`）。
- 「有畫出來」才發光：原版是遍歷所有物件，隱身（不畫）的物件在原版可能也會發光。本輪選擇不發光，避免洩漏位置。

## 未驗證／未做

- 實機：盲目 2 時怪物或玩家周圍是否真的有光圈、亮度是否與原版一致（交給 Codex／dot 的真機 smoke）。
- 真實 list.spr 的欄位 102 分布：有沒有 alias 繼承、角色 gfx 0 是否為 5（設計文件提過 `102.type(5)`）。
- 職業條件的來源封包（`byte_1542AA1`）。
- 刪除 `Renderer.PaintAmbience`（等 #607）。
- Q3-3／Q3-4／Q3-5（等 #680）。

## 產出路徑

- `engine/source/Afflictions.cs`
- `engine/source/AfflictionFollowupTests.cs`
- `docs/reports/issue-676.md`

## 給下一棒的提示

- **#607 合併後**：刪除 `Renderer.PaintAmbience`（死碼，在 `Renderer.cs`）。
- **#680（T287）查完後**：
  - Q3-3：改 `TimedBuffRules.LiquorFullWave`（`TimedBuffs.cs:23`）。
  - Q3-5：改 `InvisiblePeerVisible`（`Invisibility.cs`）。
  - Q3-4：找到觸發 `Ambience.BeginLightEffect`／`EndLightEffect` 的封包後接上。
- **查到 `byte_1542AA1` 的封包後**：同時設 `WorldSession.BlindClassBonus` 與 `AmbienceFrame.NightVision`。
- **文件**：`docs/protocol/afflictions.md` 第 71–72 行還寫著「不做」，不在本任務可改範圍，建議下一張文件任務改成「已接（種類表執行時讀取）／職業條件規則已接、來源未查」。
- **本機建置**：本 worktree 一開始用一般的 `dotnet build`（沒帶 CI 設定）還原，留下壞的 `obj/project.assets.json`（WebView2 的 dll／targets 不在檔案清單裡，結果是 NETSDK1130）。之後就算加 `--force` 重新還原也沒有修好，要先刪掉 `engine/source/obj/` 下的 `project.assets.json` 等 NuGet 輸出（只刪單一檔案，不要遞迴刪除），再重新還原才恢復。
