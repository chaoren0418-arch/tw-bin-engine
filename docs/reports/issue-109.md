# Issue #109／T035：25 個本機等價資源接線與 CI 拒收

日期：2026-11-06；code，第 1 次嘗試；分支 `task/109`。

## 結論

**部分實作已驗證；整張任務 BLOCKED，不得合併或宣告 CI 綠燈。** 指定 25 檔已從分支移除，dev 建置、check-only、icons 與三組 6016 離線測試通過；缺素材明列 skipped、錯誤 SHA 與新增資源探針明確拒收。嚴格追蹤內容閘門仍拒絕兩個範圍外歷史 JSON。本次保留它們，沒有把它們冒列為第 8／9 個 B15 例外；需要主控裁定移出追蹤的範圍後才能讓 CI 綠燈。

| 阻塞路徑 | 精確原因 | SHA-256 |
|---|---|---|
| `work/P31-pickup-composite-review-r1/cases.json` | `scenarios[].scene[].pixels` 等陣列，合計 68,935 筆；#102 列 U14 | `98369cbd4fbf437dca9d4262cdd01045a0c97639ce8754d530f3dfc54e34eb50` |
| `work/engine-pickup-presentation-candidate-review-r1/execution-rows.json` | `rows[175].actual.evidence.cover.Tile.Pixels` 陣列，1 筆；#102 列 U14。其他 `pixels: true` 不是陣列，不會誤擋。 | `a10c83bd5820680144211e6cb85854033e61d89d81f6e4544ffe1f2052856585` |

這兩檔不在指定 25 檔或 7 個 B15 例外內；沒有修改、刪除或放寬 detector。已於工作期間提出範圍澄清；未收到允許前保留現狀。PR 以 draft 交付，CI 拒收為預期且尚未解除的阻塞。

## 做了什麼

- 先讀主專案 AGENTS.md、docs/REPORT.md，再讀 #102／#88 報告、TASKS 及接線程式。#102 報告尚未在 `origin/main`，本次唯讀主目錄 `task/102` 的最新版。一般執行工具與 Node 工具均在 sandbox 初始化失敗；核准的提升執行模式才可讀寫。沒有自動審查拒絕，也沒有要求使用者變更設定。
- fetch 後從 `origin/main@1b718bc2a672b9f875acd902c773aa3795855a64` 建立 `scratch/wt/task-109`／`task/109`。主目錄 `task/102@a084ea2` 保持原狀，既有未追蹤截圖、runs、runtime 皆未動。
- 依 #88 建立本 worktree 的 `engine/assets` junction，指向主專案既有 assets；目標為唯讀來源，沒有複製遊戲素材或修改封存。離線 vendor 僅複製既有 WebView2 NuGet 套件；SHA `56f7f4b8bf9aee4b8efefbbdd4f67d5f74ebd1b100ed0806da71bf76af481aa9`。
- `WebPanel.cs` 對 canonical `https://p08.example/icons/<ushort>.png` 明確回應本機 `assets/inventory/<id>.png` bytes；既有頁面 URI 白名單保持原樣。新增 `WebIconResource.cs` 拒絕非固定 host／scheme、port/userinfo、query/fragment、非 ASCII 數字、非 canonical 數字、編碼／路徑穿越；assets 根是明確允許的 junction 邊界，inventory 與檔名葉節點拒絕 ReparsePoint。成功 200 PNG、缺檔／非 PNG 404、非法路徑／連結 403；不再交給 web 靜態映射回退。
- Ground 請求仍由 `GroundSpriteReader.Read/Image` 直接讀 `TW_CLIENT_ROOT` 的 SPR；全追蹤程式搜尋未發現其他讀兩個 web 靜態 PNG 的現行讀取者，沒有新增 fallback。
- 6016 的三個 Program.cs 共用 `LocalFixture.cs`，參數為 `<output-root> --assets-root <local-assets-root>`，讀 `maps/effects/6016.json`，保留 SHA `825673b11feb6b28c6d34663cd53266d0ca2209230b1b1cfa4c0a80125263383`。缺檔輸出 `status: skipped`／`testsExecuted: false`、exit 77；錯誤 SHA 輸出 blocked、exit 2。檢查發生於任何實際案例之前。
- Host 原有 r3 組件驗證依賴歷史 fixture 目錄及 accepted DLL。新增明確可選 `--product-receipt <dev-receipt>`，在本次 dev 模式比對測試載入的 product／audio DLL 與實際建置收據，保持原 r3 模式；本機 audio-manifest 的固定 SHA 亦保留。原 56 個 host 斷言變成 58 個（增加收據 schema／product 身分核對）。
- `run_stage.py` 僅同步三組測試的 `--assets-root` 參數，不執行歷史封裝。新增 `engine/tools/check-local-resources.py` 可重現測試；CI 無素材時逐組輸出 skipped，wrapper exit 0 只表示已報告跳過，沒有 PASS。
- `.gitignore` 增加四個指定根路徑；原 `engine/assets` 規則保留，不使用全域 png/json 排除。
- 只更新 dev lock；未修改 r33／r34／r35 lock 或 `releases/`。dev lock 來源數 169、總 pin 178；web manifest 中已無 22 PNG，check-only runtime/web/index 共 24 檔。

## 移除路徑及逐檔身分

移除前實際讀取這 25 個追蹤檔與既有本機檔，**25/25 SHA 與 bytes 完全相同**；沒有建立或複製替代素材。icons 對應 `engine/assets/inventory/`，ground 對應 `engine/assets/ground/`（僅比對，runtime 仍讀 SPR），WAV 對應 `engine/assets/sound/`，fixture 對應 `engine/assets/maps/effects/6016.json`。

| 刪除的追蹤路徑 | SHA-256 |
|---|---|
| `engine/_integration/check-template/fixtures/6016.json` | `825673b11feb6b28c6d34663cd53266d0ca2209230b1b1cfa4c0a80125263383` |
| `engine/_integration/delta/assets/sound/1340.wav` | `30b4ce04d5ca914cdbfd80c7ee9b9cdbe3bf79487e609b2a9d32f6546a90f9c3` |
| `engine/_integration/delta/assets/sound/3332.wav` | `973e0a3598edd23c2a24f6900643e43a9155fe36906b939d2d8e24374abeff2a` |
| `engine/web/ground/36.png` | `8d72431ab657dc156297aa3007b0843c01e75f20dfe5379b27d5cd8d5b9f29f5` |
| `engine/web/ground/66.png` | `3ecde86e10281647ddc1f66ddb42a44a83a98c86baa4ae317ddb8859b3e4fe34` |
| `engine/web/icons/124.png` | `7a989ea2e4f676a42d82e612ca9ff6a1d25e27005acf0cc312ffd270cef62919` |
| `engine/web/icons/138.png` | `335162b3786bced49563cfe86aa14fc4114867c9aa8237821023704a08ee0b98` |
| `engine/web/icons/2.png` | `0f609374be13892e61096e39fbd126cc7258a179a108dcfd80ad2e9e34d430a0` |
| `engine/web/icons/242.png` | `5ee66ffca19d9d3395318df30ee32aaf94c36753dadf8a9bb9935b564b07dccd` |
| `engine/web/icons/245.png` | `ea4ac1c311981c4380e17779b4b15f1ef2a2a71d2b1962a2591f203522e9b001` |
| `engine/web/icons/278.png` | `c7a17e518c4e3978a83446e66c4cf7ae5cd54caeb04184556442a48b9f75f220` |
| `engine/web/icons/318.png` | `ec7b23a61616e87b992ae01fd95ddfa83430300141aa85f4ded444af31c46a26` |
| `engine/web/icons/35.png` | `9ded6e008b2f981978dab2874f924b72b8d5f96ea6c4c856c4ebe60884a00073` |
| `engine/web/icons/4.png` | `75ab427d9ee5b9051b6cc4103b43eaadfd7cadd75d73d29bae9caf28ce3ac93e` |
| `engine/web/icons/474.png` | `569b75068a19f536d78b12a6b75d8bf6e61655b9c1a7f282b0096b9043ecfa1d` |
| `engine/web/icons/475.png` | `af4f6debaadb18732f040901c0ecf166156782a25e26da6b6867445d3c325846` |
| `engine/web/icons/5.png` | `9e35714d6b77ab794679b0a32f295cea5a78b773fbb745f46ce00366a4b06ff7` |
| `engine/web/icons/59.png` | `cb4045a12f6d28c81bbb25e12173741b184f8bc2fc90b42d9063d0ee076a669e` |
| `engine/web/icons/7.png` | `90f23210260582bfcc8c9e8b7a00cb727840e73082e3c7ca33c1e1fbc8b3143a` |
| `engine/web/icons/77.png` | `19f5dd065bc709dbd1c0fe59bad01e0a8acfad39593ff90621f069a709c9a727` |
| `engine/web/icons/80.png` | `0a6b703b2b6b0709ce4c7364f36ed99ef4cc907712d7d0059aea90c0ed9f4f37` |
| `engine/web/icons/81.png` | `eda21f8685278d50e150fa50ea4975756ea5cede2161fc86f1726199615ab988` |
| `engine/web/icons/86.png` | `f1a70fcdcee29ea66ff20f204157927e07468cfb50da545adcc2c93831b389a9` |
| `engine/web/icons/957.png` | `9b1df82afe88dc2183d9f3e69e8218d4126330e1f8f4179bf861ae305aec3a31` |
| `engine/web/icons/97.png` | `a630764b5725c0fc3629a4b86cc60d309695e9673179cb35d73b406d2769ec1d` |

## WAV／歷史腳本查核

| 腳本／流程 | 實際依賴與影響 |
|---|---|
| `_integration/setup.py:32` | 歷史 r28 初次整合，由舊 BASE/CAND 複製整份 assets，再登錄兩個 sound pin；並非讀 delta 副本。現今不應執行，會重新產生遊戲資源。 |
| `_integration/run_stage.py` | 歷史 r28 重建，固定 r28 產品及 accepted identity；測試從 OUT/assets 讀，未讀 delta WAV。三組參數已同步，本次未重跑整個歷史流程。 |
| `_integration/audit_assets.py` | 讀 BASE/OUT assets 完整清冊與本體，不讀 delta WAV。歷史契約，不執行。 |
| `_integration/audit_delivery.py` | 以 OUT/assets 比對後寫出 delta；本次刪除不會阻斷它的輸入，但重跑可能重新生成 WAV；CI 會拒絕重新提交。未執行。 |
| `_integration/finalize.py`、歷史 seal／changed-files 清冊 | 記載完整 r28 delta／交付身分；本次 HEAD 刻意不再是一份完整舊 r28 交付包。若拿本分支比對歷史完整封裝，會因移除檔案不再符合；封存原件保持不變。 |
| 現行 `build-product.ps1` → dev/rebuild.py、CI、AudioAssets | 沒有 delta WAV 讀取；現行 runtime 維持從 assets/sound 及 audio-manifest 核驗。本次 dev 建置／離線測試驗證通過。 |

沒有執行 setup／audit／finalize 的資源複製程序。Ground 的舊封裝清冊與舊 r33/r34/r35 的固定來源 pin 也保留歷史原值，不能拿那些舊 lock 驗本次 HEAD；本次使用 dev lock。

## CI 允許清單與界線

`engine/tools/check-no-game-assets.py` 列 `git ls-files` 並讀內容，不依副檔名；拒 PNG、GIF、BMP、RIFF/WAVE、gzip、ZIP magic，拒大小寫 pixels 陣列（含空陣列）、明示 RGB555 本文。追蹤 symlink/reparse、缺失追蹤檔與無法解析專案 XML 明確失敗。它不是任意編碼／所有格式的遊戲來源鑑識器，也不是 B15 portable ZIP 認證。

證據目錄依本 Issue 明示允許 `docs/demo/**`、`docs/native-baseline/**` 的 binary magic；JSON pixels 檢查仍適用。沒有刪改這些證據。其餘例外都列在 `engine/tools/game-assets-allowlist.json`，採精確 path + SHA；內容改變即使不再命中 detector 仍會失敗，新增檔案不會自動進清單。

### 七個 B15 已知例外（未修改）

| 路徑 | SHA-256 |
|---|---|
| `engine/source/Resources/pickup-own-base15-gfx0-weapon0.json` | `43b88cec329929484610aceae84566d1a922eb4b8ceba93e17dc44b65922ca90` |
| `engine/source/Resources/pickup-own-base15-profile-extension.json` | `aff2e04d4f9395f7993baf5818eb16218730b7def0a29c7d07e86c314db513ba` |
| `engine/source/OriginalExpTable.cs` | `325384a1437d6959b6233cbf2b7c76c681a2c8c6c1a05b41b7b9373e5bc198b1` |
| `engine/source/Resources/ground-catalog.json` | `ee5446856a00eeced52fecc296a8da139650ecaa8f69a47c725ba9d93c71f4d8` |
| `engine/action-metadata-vectors.json` | `6afd114d3c53be54dfc2a0116cb71358c45a0df51ac64546e13d2de64f4afc9e` |
| `engine/haste-metadata-vectors.json` | `4e6cb81ecfe9197fbf2d8b4d4496ec258355ee723367549a701de79f4320939a` |
| `engine/_integration/dev/combat-sfx-tests/expected-events.json` | `6a9b35c807273a284bbca2a14e648653678ddc6984d98532c2682d060efa58f7` |

### 19 個其他既有二進位例外

依本 Issue「如果發現還有其他既有的二進位檔，就把它們精確列入允許清單，並在報告中逐一說明」保留。**#102 對這 19 個都是 U，不是 C/M，也未裁定為可攜產品內容**。這裡只把既有歷史壓縮紀錄釘住，沒有重新核准其內文來源；未增加額外 C/M 二進位例外。主控若解讀「允許清單只有 C/M」優先而不允許這 19 個，須另裁定移出範圍，不能把它們標成已審非遊戲資料。

| 精確路徑 | SHA-256 | 保留原因／限制 |
|---|---|---|
| `docs/project-consolidation/physical-migration/before-P10Lab-TW13081901.jsonl.gz` | `de8e79cd99212e03d917165ceb83b32f4e59a1057e0f51e83c17d8567653fade` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-P12Lab-ClientEngine.jsonl.gz` | `2f597848ef52de69524dd1c5106d0b2f387d1e7cbd75be776f283d55bd2c046f` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-P13Lab-ClientEngine.jsonl.gz` | `d142cd7c1fc62d118c9a1e8ece4466399067e914e8fa6bfbc70118620f6c0aae` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-P14Lab-ClientEngine.jsonl.gz` | `f6f9d6f2887334d97be433ef70e31ac6bb8c0add02f755592785e1c849743478` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-P15Lab-ClientEngine.jsonl.gz` | `aec6d8effbb2b27377201679bdc48a2d3a28f761093a675c3b3bef9caaae2b76` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-P16Lab-ClientEngine.jsonl.gz` | `e686e24ca5ea63f2b812e86ee21a1a41e1a64081abfa3efa8248264ba07b8613` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-P17Lab-ClientEngine.jsonl.gz` | `636889f4add8f2f392610d4ec21ce12a597ca663624fccc8379c40711f738177` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-P18Lab-ClientEngine.jsonl.gz` | `3951da9909e2b890a917c9d485dd486e1233c78c632398c37a4c46a6729cdc2d` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-P19Lab-ClientEngine.jsonl.gz` | `aab6fd53ae97882694209eb17c5cbc20df9e69bd75e25ae943cab4cb3ef2e399` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-P20Lab-ClientEngine.jsonl.gz` | `e89ef1c4bc4c83b27e34f634100a47a79e067ed962f8c31d12058731f20eae36` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-P21Lab-ClientEngine.jsonl.gz` | `ad4773aa07455d09f16230d10fd037be2c0cc09d71646e06771cdac7716724aa` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-P22Lab-ClientEngine.jsonl.gz` | `f458f8331f5cf937c8266606e86ba0d21f79d58fb858c09289338581a99f514f` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-P23Lab-ClientEngine.jsonl.gz` | `4d8718249eeaff43068649326db21462e00d24709874267b75f4d15cef5097b0` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-P24Lab-6016-Export-r1.jsonl.gz` | `a0b15c4f478ec17b5f571d19ffad4182709bb3caaed71102f1cd9293970eb6de` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-P24Lab-ClientEngine.jsonl.gz` | `2ded39c79d7d4a169893f5fd3cdfdbf46e7f1da1884b89f33112826213f957d4` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-TW1608262200_research.jsonl.gz` | `ea30a0be7b5822eb88ac3e9e3fcbb7254c695ba5ac489cb7986e53c2fd936a2b` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/before-r28-interrupted-partial.jsonl.gz` | `521b06bbe7662c5ec023ba2e7b26ccb31f2f91c46cda1e780761e824f3d4cdc2` | 中斷的 r28 遷移盤點；#102 U03，gzip EOFError；僅釘住原件，不稱完整可解壓 |
| `docs/project-consolidation/physical-migration/before-r28.jsonl.gz` | `53cac9fc545e622f2e37c6ee62e836724e9e71431f1820ecf40cc4c1cab30c3c` | 既有實體遷移前路徑／大小／hash 壓縮盤點；#102 U02，JSONL schema 未核准 |
| `docs/project-consolidation/physical-migration/phase1-documents.zip` | `286d1bcd4d20eeb4984d0c76e4ba606cec68a63770c19cb9f6e16a25c6725e00` | 既有第一階段文件封裝 11 members；#102 U04，語義未全數核准；不解壓／發行 |

### EmbeddedResource

來源 XML 閘門只接受四組 project／Include／LogicalName，拒新增／重複項與 implicit `.resx`；CI 在 dev build 後執行 `ResourceChecks`，以 `PEReader`／`MetadataReader` 讀 product／audio 的 CLR ManifestResource table，拒額外或 linked resource，**不載入或執行產品組件**。四個 logical name：

- `P06.Resources.PickupOwnBase15Gfx0Weapon0.json`
- `P06.Resources.PickupOwnBase15ProfileExtension.json`
- `P06.Resources.GroundCatalog.json`
- `P24.Audio.CombatSfxPins.json`

## 怎麼驗證／實際輸出

建置時 TEMP/TMP、NuGet／dotnet cache 全部指向本 worktree 的 scratch；建置產物使用既定 engine/build、engine/launcher、各 csproj 的 bin/obj。一般 sandbox 無法啟動不是測試通過證據。

### dev 建置與 check-only（exit 0）

```text
python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock
{"status": "dev-inputs-verified", "sourceFiles": 169, "pinnedFiles": 178, "writes": true}
python -B -X utf8 engine/_integration/dev/rebuild.py --build --write-receipt --output scratch/issue-109/rebuild
{"status": "dev-inputs-verified", "sourceFiles": 169, "pinnedFiles": 178, "writes": false}
sdk 0
product-restore 0
product-build 0
launcher-restore 0
launcher-build 0
```

主程式及 launcher 各 0 警告／0 錯誤。建置結果：

```json
{
  "status": "pass-dev-build",
  "sourceFiles": 169,
  "GUI": false,
  "network": false,
  "database": false,
  "productDllSha256": "6950588c0552ddc92c55fbdf7d4962155b84d6de357b0f931b6c2ca5cff191e1",
  "audioDllSha256": "c64a38a8165f5651a331c19cf580e64d0e59541d060aeebb69c71941bb04b52d",
  "productManifestSha256": "ad1249b2592deecd12b5f91f5fde8f8745eb9cb04c83c45d1cf90c074818855f",
  "launcherDllSha256": "222e8a0d03c657a980c2d1c0ab24e46c2026e41b1ea17866b73029cf7452e63d"
}
```

`engine\啟動新版客戶端.cmd --check-only`（exit 0）：

```json
{
  "status": "checked-only",
  "revision": "dev",
  "executable": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\scratch\\wt\\task-109\\engine\\build\\client-product-dev\\P11.ClientEngine.exe",
  "workingDirectory": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\scratch\\wt\\task-109\\engine",
  "arguments": [
    "--normal-product"
  ],
  "manifest": "D:\\lineage\u958B\u767C\\TW1608262200\u7814\u7A76\\scratch\\wt\\task-109\\engine\\build\\product-dev-manifest.json",
  "manifestSha256": "ad1249b2592deecd12b5f91f5fde8f8745eb9cb04c83c45d1cf90c074818855f",
  "hashedRuntimeAndWebFiles": 24,
  "assetFilesCheckedForPresenceAndSize": 13957,
  "assetContentVerification": "Pinned integrity manifest; body hashes remain verified by the product when loaded, matching prior entry contract.",
  "runtime": {
    "architecture": "x64",
    "loadedCoreVersion": "10.0.8",
    "loadedCoreDirectory": "C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\10.0.8\\",
    "desktopVersion": "10.0.8",
    "desktopDirectory": "C:\\Program Files\\dotnet\\shared\\Microsoft.WindowsDesktop.App\\10.0.8"
  },
  "webView2Version": "156.0.4314.58",
  "powerShellRequired": false,
  "pathLookupUsed": false,
  "autoLogin": false,
  "uiStarted": false,
  "networkStarted": false,
  "servicesStarted": false
}

```

### 四組離線測試（exit 0）

```text
python -B -X utf8 engine/tools/check-local-resources.py --assets-root engine/assets
{"status":"pass","icons":20,"checks":61,"webViewStarted":false}
PASS 18 cases / 58 checks
PASS 4 client cases / 21 checks
PASS 7 audio cases / 39 checks
{"status": "pass", "suites": 4, "missingAssetCases": 4, "GUI": false, "network": false, "playback": false}
```

61 個 icons 檢查含 20 檔 response SHA、非法 URI／缺檔、inventory junction 與 `.png` 葉節點 junction 拒絕。曾嘗試建立檔案 symlink，Windows 回覆 WinError 1314 無特殊權限；沒有更改系統權限，改用 NTFS junction 驗同一 ReparsePoint 拒收分支。**真檔案 symlink 未測**。

Host 第一次跑發現舊 harness 所需 scope／舊 DLL 路徑，新增明確 dev receipt 模式後重跑成功；沒有將失敗輪算通過。三個測試以實際產品 DLL、既有受控 fake transport/world/UI 執行；不代表 native 畫面／聲音驗收。

### 缺素材與 SHA 錯誤

實際暫時改名本 worktree 的 `engine/assets` junction，於 finally 還原，原目標完全未動。wrapper 命令同上，exit 0 且輸出：

```json
{"suite": "icons", "status": "skipped", "reason": "Local assets unavailable", "testsExecuted": false}
{"suite": "host", "status": "skipped", "reason": "Local assets unavailable", "testsExecuted": false}
{"suite": "client", "status": "skipped", "reason": "Local assets unavailable", "testsExecuted": false}
{"suite": "audio-settings", "status": "skipped", "reason": "Local assets unavailable", "testsExecuted": false}

```

各已編譯 suite 另以明確缺素材路徑執行，exit 77：

```text
{"status":"skipped","reason":"Missing local assets","securityChecks":19,"localIconChecksExecuted":false}
{"status":"skipped","reason":"Missing local assets/maps/effects/6016.json","testsExecuted":false}
{"status":"skipped","reason":"Missing local assets/maps/effects/6016.json","testsExecuted":false}
{"status":"skipped","reason":"Missing local assets/maps/effects/6016.json","testsExecuted":false}
```

scratch 建立僅含 `{}` 的故意錯誤 6016 測試輸入，三套 managed suite 均 exit 2：

```json
{"status":"blocked","reason":"6016 SHA-256 mismatch","testsExecuted":false}
```

### 拒收與負向探針

內容閘門目前 exit 1、兩個阻塞，見本文開頭。scratch 的 `renamed-image.cs` 只有合成 PNG signature／說明文字，並非真遊戲圖；使用 `--probe` 執行同一 detector，結果 exit 1 並額外列出 PNG 命中：

```text
REFUSED: work/P31-pickup-composite-review-r1/cases.json: pixels array
REFUSED: work/engine-pickup-presentation-candidate-review-r1/execution-rows.json: pixels array
REFUSED: scratch/issue-109/renamed-image.cs: PNG magic
{"status": "blocked", "trackedFiles": 3037, "errors": 3, "embeddedResourceDeclarations": 4}
```

另以 scratch 驗 PNG/GIF/BMP/WAV/gzip/ZIP、改名 pixels/RGB555 JSON，以及七個 B15 原 hash 通過／修改一 byte 拒收，共 24 個 detector 斷言（含追加明示 format/body RGB555 探針）。編譯組件檢查：

```text
dotnet engine/tools/ResourceChecks/bin/Release/net10.0/ResourceChecks.dll engine/build/client-product-dev
{"status":"pass","assemblies":2,"embeddedResources":4,"assemblyCodeExecuted":false}
```

純合成 probe 組件加入 `Unexpected.Resource`，同工具 exit 1：

```text
REFUSED: Unexpected/linked resource: P11.ClientEngine.dll/Unexpected.Resource
```

初次探針揭露 `InvalidDataException` 未捕捉，已補處理並重驗正常／拒收兩路徑；探針及 probe build 只在 scratch，沒有提交。

### 遠端 CI 實際結果

Draft PR：[#116](https://github.com/chaoren0418-arch/tw-bin-engine/pull/116)。已驗證的產品來源提交 `b1cee285604ab4bb5938619f0c36533f4c0931ed`。

[Actions run 42152699076](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42152699076)，job `127679446718`，2026-11-06 02:49 UTC：**failure**，失敗步驟 `Reject tracked game assets`。實際 log：

```text
REFUSED: work/P31-pickup-composite-review-r1/cases.json: pixels array
REFUSED: work/engine-pickup-presentation-candidate-review-r1/execution-rows.json: pixels array
{"status": "blocked", "trackedFiles": 3050, "errors": 2, "embeddedResourceDeclarations": 4}
Process completed with exit code 1.
```

`Report local resource checks` 步驟成功執行；icons／host／client／audio-settings 四列均 `status: skipped`、`reason: Local assets unavailable`、`testsExecuted: false`。後續 build／組件／packet-trace 步驟因前置閘門失敗被跳過，沒有宣稱遠端建置通過。

工作期間 main 前進至 `0b963fcccb54cccc764d0f0b6a0f35f76c411866`，僅規則／文件變更（#102、#107）；已唯讀核對新版 AGENTS.md。CI 合併測試樹因此多兩份報告，追蹤檔為 3050，本機提交樹為 3048；兩者均只有上述兩個拒收項。

## 未驗證什麼

- **CI 尚非綠燈**：本機嚴格掃描已證實兩個既有 JSON 阻塞。不得跳過閘門或新增未授權 JSON 例外以取得綠燈。
- 未啟動遊戲、WebView2 面板、播放聲音、登入或連網／DB／服務；未執行 ground SPR 解碼新一輪測試。上述真機行為沒有用單元測試冒充。
- 未全面鑑識壓縮歷史證據；不宣稱 repo／portable ZIP 零遊戲資料，7 個 B15 檔仍明確保留。
- 未重跑歷史 r28 封裝／seal 流程；未修改 docs/REPORT.md、原 BIN、正式客戶端、releases、r33/r34/r35 lock 或硬連結。

## 產出路徑與給下一棒的提示

- 永久報告：`docs/reports/issue-109.md`；來源／測試／CI／dev lock 變更見同一 PR。完整允許清單：`engine/tools/game-assets-allowlist.json`。
- 可重現命令：先 dev/rebuild.py `--build --write-receipt`，再 `engine/tools/check-local-resources.py --assets-root engine/assets`；CI scanner 與 ResourceChecks 命令見上文。
- scratch 原始 logs／探針不提交；移除 worktree 前只保存必要文字證據至主專案 `scratch/issue-109/`，不保留任何素材複本。刪 worktree 前移除本次建立的 junction entry，避免跟隨目標。
- **主控／Claude**：先裁定兩個範圍外 U14 JSON 的移除範圍與 19 個歷史 binary 精確例外，再指派本機執行器完成嚴格閘門 exit 0／CI 綠燈；本 PR 暫勿合併。
- **dot 下一次經授權真機核對**：開啟 WebView2 背包、交易／商店等會顯示 icons 的面板，核對 20 個既有圖示正常顯示且沒有 404；核對金幣／肉地面圖仍由 SPR 顯示；在原測試流程確認 6016 效果與 1340／3332 音訊未退化。不要把本次 check-only 當成已驗。
