# Issue #824 — T360：R8-E list.spr 附加層（105 cloth／111 stride／112 altdir）

依據：`docs/reference/followups-8.md` §5（FU8§5）、`gap-matrix.md` §15.10。

## 做了什麼
- `engine/source/MagicRendering.cs`（只動 sprite 目錄解析）
  - 新增 `ListSprFields(int[] Cloth, int Stride, int? AltDir)` 與 `ListSprFields.Parse(entry)`：
    - 105 `cloth(n gfx…)`：n 必須等於後面 gfx 的個數，n ≤ 10（物件 `+164` 起每層 16 bytes、`+324` 計數；本機最多 6），gfx 範圍是 word；
    - 111 `stride(n)`：一個 byte；
    - 112 `altdir(gfx)`：一個 word；
    - 只看欄位編號、不看名稱。本機 list.spz 的 105 有 `cloth`／`clothes`／`clothed`／`cloths`／`clothe`／`shadow` 六種寫法。lookbehind 和 `listspr_fields.py` 一樣是 `(?<![\d.:\[])`，避免把影格參照（`24.105:4`）或更長的編號（`1105.`）當成欄位；
    - 格式不對時丟 `InvalidDataException`，沒有任何欄位時回 `ListSprFields.Empty`。
  - `EffectSpriteCatalog.Fields(gfx)`：讀這個 gfx 自己那一列（不跟 alias；原版每個 gfx 各自讀）。動畫表讀不到或沒有這個 gfx 時回 null，不丟例外。
- `engine/source/ObjectRendering.cs`：新增純函式 `ListSprLayers`
  - `ClothLayers(fields, verified=ClothVerified)`：`ClothVerified=false`，在真機確認前一律回空，也就是不多畫附加層；
  - `AltDirGfx(gfx, direction, fields)`：朝向 6 而且有 altdir 時回替代 gfx；
  - `StrideNext(counter, fields)`：站立輪換計數在 0…n 之間循環。
  - **這些函式都還沒接進 `ComposeObjects`**，本次只做資料層，原因見「未驗證」。
- 新檔 `engine/source/ListSprFieldTests.cs`：`[WorldModuleTest("listspr-fields")]`，用自動發現註冊，共 4 組約 30 個檢查：解析、拒絕錯誤格式、catalog、物件端規則。只用合成的 list.spr 條目，不讀客戶端資源。

## 本機 list.spz 實測（唯讀）
用 `py -3 -I docs/reference/rea-scripts/t783/listspr_fields.py "D:\lineage開發\0-客戶端" scratch/t824/fields.json` 讀取 0-客戶端 的 Text.pak 中的 list.spz（sha256 `e8cec038…6242`，共 12,230 個 gfx）：
- 105：1,930 個 gfx，**每一列的個數都和後面的 gfx 數相符**，最多 6 層。例：gfx 0 → 6262、1 → 6261、37 → 6263。
- 111：86 個 gfx，值全都是 `stride(1)`。
- 112：只有 1395、5940 兩個，都是 → 1410。

本次沒有宣稱缺少任何資源，所以沒有做 PakViewer 的全 pak 查詢。

## 怎麼驗證
- 本機編譯：`dotnet build engine/source/OfflineClient.csproj -c Release`（輸出到 `engine/build/t824`）結果是 0 個錯誤，改到的三個檔案沒有警告。本機有一個和程式無關的既有問題：WebView2 的 winmd 會觸發 NETSDK1130，#676～#689 也遇過。所以本機編譯時暫時加了 `-p:CustomAfterMicrosoftCommonTargets=scratch/t824/nowinmd.targets`（這個檔案沒有進 git），restore 改從主專案 `engine/vendor` 唯讀取得套件。
- `engine/tools/check-no-game-assets.py` 通過（4374 個追蹤檔，0 個錯誤）。
- 依本機限制，沒有在本機執行測試；`listspr-fields` 模組測試與既有單元測試交給 PR 的 CI。
- 沒有改 `rebuild-lock.json`。

## 未驗證／待驗證
- **105 cloth（要真機確認）**：3.81（`LiTo.bin`）到底畫不畫附加層。要在真機看 gfx 0（王子）有沒有 6262 這一層。確認會畫之後，把 `ListSprLayers.ClothVerified` 改成 true，並在 `ComposeObjects` 中依順序疊畫子精靈。疊畫時的 Z 順序與旗標（來自欄位 104）還沒有細追。
- **112 altdir**：還沒確定 `sub_A3EF40` 只在建立物件時換 gfx，還是每次轉向都換，所以沒有接線。
- **111 stride**：姿勢表 `byte_1379D40` 的內容還沒取出，所以只做了計數；何時遞增（站立時的觸發點）也還沒接。

## 產出路徑
- `engine/source/MagicRendering.cs`、`engine/source/ObjectRendering.cs`、`engine/source/ListSprFieldTests.cs`
- `docs/reports/issue-824.md`
- 分支 `task/824`

## 給下一棒的提示
- 真機項目（M31／#475）：比較 gfx 0、1、37 角色在 3.81 是否多一層（6262／6261／6263）。截圖時請對照 `ListSprFields.Parse` 的結果。
- 接線點：`ObjectRendering.cs` 的 `ComposeObjects` 迴圈中 `drawGfx` 決定之後。Renderer 已有 `effectSprites`（`EffectSpriteCatalog`），可以直接呼叫 `Fields(drawGfx)`。
- stride 要先從 BIN 取出 `byte_1379D40`（.data），再查 `sub_A43560`／`0xA43D84` 何時把 `+532` 加一。
