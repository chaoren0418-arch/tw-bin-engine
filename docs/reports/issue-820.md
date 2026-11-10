# Issue #820 — T356：R8-A 頭上條

依據：`docs/reference/followups-8.md`（FU8，T337／#783）§1.1、§1.2、§1.4；`gap-matrix.md` §15.10。

## 做了什麼

### 1. 自己的頭上條 x＝P.x − 80（FU8§1.1）
- `NativeUi/State/HpMpState.cs`
  - `HpMpHeadTarget` 加選填欄位 `Origin`（`Point?`，預設 null）：自己的 P（`sub_F1A750` 的精靈區塊原點），和 `Box` 同一套 view 像素。
  - `ClientForm.WithHpMpState`：自己的目標帶 `Origin = Camera.ToView(OwnCombatVisual.Foot − (24, 12))`（沿用 `ObjectRendering.cs:159` 的既有約定）；自己的 visual 還沒建好時給 null。
  - `IUiHpMpState` 加 `OwnClass`（預設介面成員回 null）；`UiHpMpGameState` 多一個選填參數 `ownClass`，產品傳 `login.World.OwnClass`（byte_1542AA1，FU6§2）。
- `NativeUi/HpMpPanel.cs`（只動 `HpMpHeadBarPanel`）
  - 新 `Anchor(HpMpHeadTarget)`：自己且有 P → `(P.x − 80, 外框上 − 12)`；其他物件與「自己但沒有 P」→ 原本的外框規則（中心 − 24）。原本 `:242` 的「own x formula unverified」註解已改掉。
  - `Plan()` 改用新 Anchor。

### 2. 自己的閃爍條（FU8§1.2）
- 新巢狀類別 `HpMpHeadBarPanel.BlinkChecker`＝`HpWarnBlinkChecker`（`sub_740BB0`／`sub_740D20`／`sub_740CE0`）：
  - 職業 0–6 與未知：(35, 5, 0)；職業 7（戰士）：(40, −1, 0)。
  - 間隔＝max(300, 26·HP%) ms；HP% ≤ 門檻時累計超過間隔才切換，每次變亮次數 +1。
  - 次數 5：亮滿 5 次後下一段到就固定不亮，直到 HP% 先回到門檻以上（歸 0、回初始）。
  - 次數 −1：一直閃；HP 回升時若正亮著，等這段時間走完才回初始。
  - 幀時間取 `IUiGameState.Now` 的差（第一幀 0，倒退當 0）。
- 只有自己有檢查器；`showCharacterHPMP` 關閉、沒有自己目標、HP／MaxHP 不明時檢查器清掉。
- `Paint`：先畫一般 HP／MP，亮時在 (x, y) 疊 surf **8393**，來源寬 `44·HP%/100`、高 4 列（`sub_8E4B90`）。`Plan()` 多回傳 `Blink` 欄位。
- `Load(UiLayout, Func<int,UiSurface?>)` 新多載（給離線測試用合成 surf；產品仍走 `SurfCatalog.Get`）。

### 3. surf 8393 走參考客戶端備援（FU8§1.4）
- 不改備援程式：`SurfCatalog.Get(8393)` 本機找不到時，既有的 T322／#760 `RefClientSurfFallback` 會用 id 當檔名查參考客戶端 `Sprite*.idx`。缺圖就回 null，**不畫閃爍**，HP／MP 照常。
- 本機核對（唯讀 idx 解析，`scratch/t820/find_surf.py`，輸出 `scratch/t820/find_surf_8393.txt`）：
  - `D:\lineage開發\0-客戶端`：Sprite.idx、Sprite00–15.idx、Text.idx、Tile.idx（19 個）都沒有 `8393.img`／`8393c.img`。
  - `D:\381懶人包kor25(原)`：同 19 個 idx，只有 **`Sprite02.idx: 8393.img`（584 B）**，和 FU8§1.4 一致；`NameHash("8393.img")＝578 % 16＝2` → Sprite02，正是備援會查的封存檔。
  - 沒有複製任何資源進 repo。

### 4. 測試
- 新檔 `engine/source/HeadBarFollowupTests.cs`，`[WorldModuleTest("headbar-followup")]` 自動發現：
  - Anchor：P＝(456, 288) → x＝376；沒有 P 退回外框規則；其他物件不看 P。
  - BlinkChecker：間隔 910／300／300；(35,5)／(40,−1) 選擇；「＝間隔不切換、超過才切換」；亮滿 5 次後固定不亮；HP 回升歸 0；戰士持續閃與「這段走完才熄」。
  - Panel：合成 surf 的像素檢查（閃爍寬 13、高 4、MP 列不被蓋）；缺 8393 時只畫 HP；戰士 38% 閃、一般 38% 不閃；選項關閉、其他物件、MaxHP 不明、時間倒退。
  - State：`OwnClass` 帶入與預設 null；`Origin` 預設 null。
- 既有 `native-ui-tests/HpMpChecks.cs` 不用改：沒帶 `Origin` 的目標仍走舊規則，`Plan()` 只新增一個具名欄位。

## 怎麼驗證
- 本機編譯：`OfflineClient.csproj`（Release）與 `DemoChecks.csproj` 皆 **0 錯誤**；本次改動沒有新警告。
  - 本機環境備註（與本任務無關，同 #676–#689）：本機 restore 會讓 WebView2 `.winmd` 觸發 NETSDK1130。做法：`NUGET_PACKAGES` 指到 `scratch/t820/packages`，並用只放在 scratch 的 `scratch/t820/nowinmd.targets`（`-p:CustomAfterMicrosoftCommonTargets=…`，把 winmd 換成套件 `lib_manual/netcoreapp3.0` 的 DLL）只為了確認 C# 能編譯；未入 git，產品專案沒改。
- `python engine/tools/check-no-game-assets.py`：pass（errors 0）。
- 依本機限制，**沒有在本機執行任何編譯出來的程式**；單元測試交給 PR #840 的 GitHub Actions CI：run 42850897959（build：dotnet、map-continuity）與 42850898346（npc-offline）全部 success；`headbar-followup` pass，40 checks、0 failures；既有群組（含 native-ui-hpmp）綠。

## 未驗證什麼（待驗證）
- **真機再確認**：自己在畫面上的絕對位置（FU8§1.1 的待驗證：原版 P 固定 (456, 288)，引擎相機把腳下點放畫面中央，可能差約 80 px，屬相機範圍，不是本任務）。建議加進 #475 的 M14／M15 截圖比對。
- 閃爍節奏的實機觀感與 kor25 的 8393.img 實際外觀（只做了資料與合成測試）。
- 推測部分：
  - 一般職業 HP 回升時累計時間歸 0（FU8 只寫「回初始」）。
  - 計時器觸發後累計時間歸 0（不保留超出量）。
  - 職業碼未知時用 kind 1（35／5）。

## 產出路徑
- `engine/source/NativeUi/HpMpPanel.cs`（`HpMpHeadBarPanel`）
- `engine/source/NativeUi/State/HpMpState.cs`
- `engine/source/HeadBarFollowupTests.cs`
- `docs/reports/issue-820.md`（本檔）
- 分支 `task/820`

## 給下一棒的提示
- #784（R6-B）做自己的名稱 X0 時，可直接用同一個 P：`Camera.ToView(OwnCombatVisual.Foot − (24, 12))`（本任務的 `ClientForm.SelfHeadOrigin()`）。
- 4.2 `HpMpWindow` 的 +86／+87 變體與戰士門檻 40（`HpMpPanel.WarnRatio` 註解說「class unknown」）現在可用 `IUiHpMpState.OwnClass` 取得職業碼，但那屬於浮動窗任務，本任務未動。
- 真機（Codex／dot）：HP 壓到 35% 以下看自己的頭上條是否閃 5 次後停；戰士 40% 以下一直閃；log 應出現 `surf-ref 8393 -> …Sprite02.pak:8393.img`。
