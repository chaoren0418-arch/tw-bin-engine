# Issue #102：T033 追蹤資源與 EmbeddedResource 發布查核

日期：2026-11-06。階段：test，第 1 次嘗試。**結論：查核與阻塞清單已交付；零遊戲資源發布不通過（BLOCKED）。** 本次沒有修正產品、刪除資源、修改 lock、建置或建立 Release。

## 交付範圍先決定義

- 查核來源固定為 fetch 後的 `origin/main` commit **`29110d53b110bdab93d9655b14910047d74bf39c`**，不是會移動的 main。主目錄依指示執行 `git checkout -B task/102 origin/main`，唯讀檢查在 `scratch/issue-102/audit-tree` 的 detached worktree 進行。開始時主目錄已有未追蹤截圖、receipt、runtime/runs，本次保留，沒有當成本輪證據或加入 commit。
- **交付 commit 的樹**：原有 3,061 路徑加本報告及必要查核腳本兩路徑。附錄逐一列出原有路徑、Git blob bytes、SHA-256、分類與依據；新增兩檔在下節列明。所有來源內容掃描只針對固定樹；不能把本報告合併解讀為已清除資源。
- **可攜 zip**：是未來由白名單產生的獨立產品，不得直接壓整個 repo、`git archive HEAD`、engine 或工作目錄。zip 合格也不代表 repo 的交付樹合格。
- **Demo／驗收證據**：目前 HEAD 的 `docs/demo`、`docs/native-baseline` PNG 仍是追蹤檔，因此納入本次帳冊；不能因為叫證據便排除。107 PNG 中 22 是 web 素材、85 是 Demo／原生截圖。截圖包含遊戲呈現，全部拒收入產品 zip；少數只有局部 UI 的圖亦不逕行豁免。若要保留獨立教學／Demo 發布範圍，須由 Claude／使用者另裁定，不能宣稱整個交付 commit 零資源。
- **舊 Git 歷史**：本次未掃描所有 ancestor blobs、tags、已發布附件或重寫歷史。從 HEAD 移除檔案不會消除歷史；clone／GitHub source archive／Release 附件是不同交付面。歷史清理、分拆證據 repo、重新建立乾淨發布 repo 的選擇交 Claude／使用者；不得自行 filter-repo、force-push、刪 tag 或刪 Demo。
- 不讀機密設定（如 `config/server.properties`）、不碰 DB／服務／遊戲、不打開原 BIN、不讀寫封存 release 或硬連結。本次只讀主目錄 `engine/assets` 的 reparse 屬性確認為 junction，不跟隨目標；不掃描 2 GB assets。歷史研究 Python／指定生成收據是唯讀參考，沒有執行它們，也沒有複製客戶端原檔或派生資源。

## 做了什麼、怎麼驗證

1. 讀根目錄 AGENTS.md、docs/REPORT.md、TASKS 的 T033 及 `docs/design/portable-delivery.md`。Windows sandbox 初始化發生 `helper_unknown_error`，改經核准的本機執行模式；未因此變更查核範圍。`rg` 不在 PATH，改用 Git 清單、Git grep 與 Python；Python 由 `py -3` 執行。
2. `git ls-files --stage -z` 列出 3,061 個 stage 0／100644 檔；用 `git cat-file --batch` 讀每一個 blob，計算 SHA-256，再與隔離 checkout bytes 比對。**3,061 個 hash 全相符，沒有 symlink/submodule mode 或追蹤路徑的 reparse ancestor**。沒有掃入主目錄未追蹤檔。
3. 全檔內容識別包含 PNG、RIFF/WAVE、gzip、ZIP、UTF-8、JSON/JSONL，JSON 即使改為 `.cs` 也解析；檢查 pixels、可還原 hex、Base64 候選、長常數；所有未識別、資料來源未證實或無法完整解碼者保守 unknown。18 gzip 中 17 可解壓，另 1 EOFError；1 ZIP 的 11 members 均讀取、未落地解壓，但文件內容仍未核准。
4. 解析 19 個 csproj：4 個明示 EmbeddedResource，僅分布於現行 source/audio 兩專案；其餘 17 個明示項目為零。追蹤樹未見 `.resx`、`.props`、`.targets`。這是靜態檢查，不是 MSBuild 評估或已編譯 assembly 枚舉。
5. 讀取選定 P24 產生器原始碼及生成收據，將 19 個 icon PNG 和 ground/36.png 的追蹤 SHA 與生成紀錄核對相等。icon124 的完整版本重播與 ground66 的 PNG 最終重播證據仍欠缺；沒有重新解碼 Sprite 或跑產生器。
6. 執行 7 個明示合成的盤點器探針：改名 PNG、改名 WAV、改成 `.cs` 的 pixels JSON、Base64 PNG、截斷 gzip、未知 JSON 均分類 D/U；單純 pins 結構為 M。**這是盤點器分類測試，不是遊戲功能、真素材重建或正式 zip 閘門驗收**。metadata 結構分類不驗 pin 值是否真的能匹配原檔。
7. 隔離 worktree `git status --porcelain` 空白；提交前檢查主目錄變更僅本報告與必要腳本。既有 docs/REPORT.md、產品、lock、releases 未修改。

| 類別 | 數量 | 判定含義及發布處置 |
|---|---:|---|
| C | 296 | 自主工程程式／UI／建置設定的候選；掃描未見持久化長編碼 payload。不是法律來源證明，也不是整目錄 zip 許可。 |
| M | 146 | 非內容性工程 metadata／pins／路徑大小雜湊清冊或已核讀工程文件。只可在必要、精確路徑及 schema 核准後加入 zip。 |
| D | 117 | 已知原始或派生遊戲資料／含遊戲呈現的截圖；零資源交付樹與 zip 均拒收。 |
| U（unknown） | 2,502 | 尚無逐欄／逐段來源證據、可還原測試 payload 真假來源未定、敏感欄位候選、未識別或損毀容器。**全部阻擋發布，不能因 scanner 沒找到 magic 就放行。** |

U 的多數是歷史工作紀錄及 JSON；本輪完整列舉、hash 與結構掃描不等於完成其人工語義審核。每列保留 unknown 原因，沒有將檔名叫 manifest、fixture、report 的內容一律視為 M。對少量 SQL/JAR/DB 名稱或來源 hash 的引用，不表示本輪讀過或使用了相應外部資料。歷史 JSON 命中帳密類欄位名稱時只記「敏感欄位名稱」，不在報告重刊值；也沒有查驗其有效性。

產出：

| 新增路徑 | 分類 | 用途 |
|---|---|---|
| `docs/reports/issue-102.md` | M：本輪工程查核報告 | 完整逐路徑帳冊與後續規格；不重刊像素、音訊、封包 hex 或秘密值。不列本檔自身 SHA，避免自我雜湊循環；交付 commit 識別其版本。 |
| `scratch/issue-102/audit.py` | C：必要唯讀驗證腳本 | 依本任務允許的 scratch 腳本交付範圍，單檔明確加入 Git；其餘 scratch 產出／worktree 一律不提交。SHA-256 `b459c0de25d97ee0e1af81abddbac923156398c2bf8c09b8fcb85314ee940d1c`。 |

本機重現命令（來源 worktree 固定在上述 commit；先查看腳本，不執行研究工具）：

```powershell
py -3 -B -X utf8 scratch/issue-102/audit.py --tree scratch/issue-102/audit-tree --out scratch/issue-102/results
```

本機 `results/inventory.json` 的 SHA-256 為 `e99f195eaf481c2d6bb02f7e2f67949cbff0e0f4963aff2bc2a231f9da83de49`；只是暫存，永久逐檔資料在本報告附錄。換機可先在 scratch 建立該 commit 的 detached worktree；腳本不需要原客戶端、junction 或套件安裝。輸出非正式發布 manifest。

## 已驗證的發布阻塞

| 項目 | 本次證據 | 必須處理的原因 |
|---|---|---|
| web 的 20 icons＋2 ground PNG | 下節逐圖追溯與附錄 SHA | 原 TBT／SPR 派生，改名或移進 ZIP 不改變性質。 |
| 85 個 Demo／原生 PNG | PNG magic＋目前 tracked paths | 資源內容不只在 engine；證據保留須另定範圍。 |
| `engine/_integration/delta/assets/sound/1340.wav`、`3332.wav` | RIFF/WAVE；歷史 `finalize.py:40–42` 明記原音訊沿用 | 兩檔 14,322／24,748 bytes。SHA 分別 `30b4ce04d5ca914cdbfd80c7ee9b9cdbe3bf79487e609b2a9d32f6546a90f9c3`／`973e0a3598edd23c2a24f6900643e43a9155fe36906b939d2d8e24374abeff2a`。目前仍在 HEAD。 |
| pickup 兩份 JSON | 共 2,713,313 bytes、312 frames、200,628 個 RGB555 pixel triples；`PickupAssets.cs:35–40,71–72` 直接讀 embedded resources | 確實是圖像本文，不能以 JSON 為理由放行。 |
| `engine/_integration/check-template/fixtures/6016.json` | `frames[].blocks[].pixels`、69,454 bytes | 測試 fixture 亦攜帶 6016 圖像。 |
| `engine/source/OriginalExpTable.cs` | 第 2 行宣告 generated from fixed original SQL；P24 `generate_exp_table.py:4–7` 寫入 Thresholds | 把資料表轉成 C# 常數仍是派生資料；不能只掃圖片／JSON。 |
| action/haste metadata vectors、combat-sfx expected-events | `engine/action-metadata-vectors.json`、`engine/haste-metadata-vectors.json`、`engine/_integration/dev/combat-sfx-tests/expected-events.json` | 原 metadata 動作／時序／事件預期表，不是只有 hash 的 pins；須本機生成或另審自主測試規則。 |
| ground catalog | 詳下表；`source.location` 指向原服模板，rows 有物品名稱、欄位和 sprite bounds | 混合工程匹配規則、來源 pins 與遊戲資料，整檔拒收。 |
| 壓縮證據及其他 U | `docs/project-consolidation/physical-migration/before-r28-interrupted-partial.jsonl.gz` 完整解壓 EOFError；`phase1-documents.zip` 含 11 份文件 | partial／壓縮／unknown 必須 fail closed。不能因副檔名非素材就准入。 |

對 `work/P31-pickup-composite-review-r1/cases.json`、`work/engine-pickup-presentation-candidate-review-r1/execution-rows.json` 的 pixels，以及 work/issue-49、51、77 的封包 hex，本輪只證實可以還原內容，未把未核對來源的測試資料冒認原始素材，故為 U14。後續須證明純人工 fixture 或改為本機生成，仍不得直接發行。

## EmbeddedResource 白名單裁定

| 專案宣告 | logical name／檔案 | 判定與 runtime consumer |
|---|---|---|
| `engine/source/OfflineClient.csproj:19` | `P06.Resources.PickupOwnBase15Gfx0Weapon0.json`；Resources/pickup-own-base15-gfx0-weapon0.json | **D，拒收**。239,766 bytes；SHA `43b88cec329929484610aceae84566d1a922eb4b8ceba93e17dc44b65922ca90`。24 frames／17,916 pixels，`PickupAssets.Load → Resource → ReadBody`。 |
| 同檔 :20 | `P06.Resources.PickupOwnBase15ProfileExtension.json`；Resources/pickup-own-base15-profile-extension.json | **D，拒收**。2,473,547 bytes；SHA `aff2e04d4f9395f7993baf5818eb16218730b7def0a29c7d07e86c314db513ba`。12 groups／288 frames／182,712 pixels，同消費鏈。須遷移到本機 assets provider，保留 gfx／weapon 適用條件、順序與像素核驗。 |
| 同檔 :21 | `P06.Resources.GroundCatalog.json`；Resources/ground-catalog.json | **D，整檔拒收**。`GroundCatalog.cs:15–24` 靜態讀取 rows，`GroundSpriteReader` 用 pins 讀 SPR。hash／entry／offset 可候選 M，但 name/nameToken、template 與圖像 bounds／物品規則不能自動豁免。區分客戶端可重算欄位與原服依賴；無自主來源閉合者阻塞，不把表搬到 C#。 |
| `engine/audio/P24.Audio.csproj:12` | `P24.Audio.CombatSfxPins.json`；Resources/combat-sfx-pins.json | **M 候選**。schema/source/policy 及 33 個 wavPins 的 id/path/size/sha256；未含 WAV bytes。`AudioAssets.cs` 的 manifest stream 讀取。僅此 exact logical name＋內容 SHA＋嚴格 schema 可列候選，不允許 `Resources/*`。 |

其他 17 個 csproj 的明示 EmbeddedResource 為零，包括 check-template 三項、delta 二項、dev 四項及歷史 launcher 八項；仍不可據此推論編譯後沒有 SDK／套件／native 資源。新閘門須檢查評估後的 MSBuild item 清單與真正待發行 assembly；兩者都要通過。既有 `engine/_integration/dev/pe_resource.py` 是 PE normalization 工具，不是「無遊戲資源」證明。

## 22 張 PNG 的來源、消費端與替代接線

以下 P24 代表 `P24-原2x2地圖快取與跨區載入`，DEV 代表 `P24/reference/development`。這些研究路徑不是發布依賴，只供本機唯讀追溯。所有 TBT 來源皆是主 Sprite.idx/Sprite.pak 的 `<id>.tbt`；表內 PAK 數字是 **File Offset**，不是 RVA／VA，且只適用於生成紀錄所釘的容器。新產生器必須按 entry 名與容器身份重新定位，不能盲套 offset。

共同 consumer／接線代號：

- **I（20 張各自適用）**：`InventoryUi.cs:8` 的 ItemIcon 讀 `assets/inventory/<id>.png`（native 清單及 `PlayerHudIntegration.cs` 傳給 PlayerHudView 的 icon callback）；`:26` 發出 `https://p08.example/icons/<id>.png`，`web/panel.js:55` 設 img.src；`WebPanel.cs:52,68,74` 在 native 圖存在後允許網址，由 web/icons 的靜態檔供應。新的本機產生器以同一 TBT 解碼結果寫 `assets/inventory/<id>.png`，WebPanel 在 icon request 直接由該已驗目錄回應，不再複製第二份到 web。沿用 URI 白名單，檢查主機／query／canonical numeric id／路徑及 reparse；換 recipe 後清 ItemIcon 的 null/Bitmap cache，不能只熱拷貝。
- **G（2 張各自適用）**：`GroundUi.cs:37` → `web/panel.js:65` 仍發 ground URL；但 `WebPanel.cs:69–72` 現在呼叫 `GroundSpriteReader.Read/Image` 即時回應 PNG，失敗回 404，不回退讀靜態 PNG。`GroundSpriteReader.cs` 是讀原 SPR 的接線；`GroundRendering.cs` 等 native 路徑另用已生成 ground 資料。替代方案是保留已驗原 SPR provider，或本機生成 ground JSON/PNG 並由 provider 提供；不能把兩張靜態圖的移除當作整個 ground 素材鏈已閉合。

目前 TradeUi／PrivateShopUi／NpcUi 沒有直接 `/icons/` 路徑消費，本次不將它們臆測為 PNG consumer。I 是動態 id 選圖，表內每張均經同一入口，不是每張存在一段硬編碼程式。

來源鍵：

| 鍵 | 產生器及證據 |
|---|---|
| A | `P24/inventory_assets.py:9–28`；`P24/reference/inventory/icons/manifest.json`（7 圖）；裝備擴充來源可追到 `P24/wire_equipment.py:18`。 |
| B | `P24/ranged_icons.py:9–28`；`P24/reference/ranged/icons/manifest.json`。 |
| C | `DEV/basic-food-home-scroll-r1/extract_icons.py:11–32`；同目錄 `evidence/icon-provenance.json`。 |
| D | `DEV/basic-potion-haste-r1/prepare_visuals.py:33–54`；同目錄 `visual-assets.json`。81 與 A 的結果一致。 |
| E | `DEV/inn-dialogue-r1/prepare_key_icon.py:11–27`；`source-evidence/key-icon/provenance.json`。 |
| F | `DEV/ship-ticket-icon-r1/prepare.py:21–40`；同目錄 `source-evidence.json`。 |
| H | `DEV/encountered-ruby-icon-r1/prepare.py:47–60`／`extend_318.py:39–51`；`evidence/icon-provenance-242.json`／`icon-provenance-318.json`。 |
| J | `DEV/encountered-sapphire-icon-r1/prepare.py:35–52`；同目錄 `evidence/icon-provenance.json`。 |
| K | `DEV/encountered-online-gift-icon-r1/prepare.py:48–61`；同目錄 `evidence/icon-provenance.json`。 |
| L | `P24/ground_assets.py:14–31`；`P24/reference/ground/assets/manifest.json`。 |
| N | `P24/arrow_assets.py:12–18` 生成 projectile-66，`P24/arrow_ground_asset.py:4–15` 選 `0:0` 幀轉 PNG 並複製 web。 |

**所有 22 個追蹤 PNG 都拒收。** 下表「相符」僅表示本次讀到的生成收據 PNG hash 等於 Git blob，不是本次重新生成；詳細 bytes 和完整 SHA 亦在全路徑附錄。I/G 同時指定本機生成替代接線。

| 追蹤路徑 | 來源／PAK File Offset | 生成收據與目前 SHA-256 | consumer／替代 |
|---|---|---|---|
| `engine/web/ground/36.png` | L：`36-0.spr`／971303715 | 相符；`8d72431ab657dc156297aa3007b0843c01e75f20dfe5379b27d5cd8d5b9f29f5` | G → 原SPR provider／本機 ground/36.json |
| `engine/web/ground/66.png` | N：`66-0.spr` key 0:0；現行 catalog pin File Offset 962888499 | 產生器已定位；最終PNG重播 unknown；`3ecde86e10281647ddc1f66ddb42a44a83a98c86baa4ae317ddb8859b3e4fe34` | G → 原SPR provider／本機 ground/66.json |
| `engine/web/icons/124.png` | L:33：預期 `124.tbt`／unknown | 精確版本生成鏈 unknown；`7a989ea2e4f676a42d82e612ca9ff6a1d25e27005acf0cc312ffd270cef62919` | I → 本機 assets/inventory/124.png |
| `engine/web/icons/138.png` | F：`138.tbt`／903311181 | 相符；`335162b3786bced49563cfe86aa14fc4114867c9aa8237821023704a08ee0b98` | I → 本機 assets/inventory/138.png |
| `engine/web/icons/2.png` | A：`2.tbt`／903258114 | 相符；`0f609374be13892e61096e39fbd126cc7258a179a108dcfd80ad2e9e34d430a0` | I → 本機 assets/inventory/2.png |
| `engine/web/icons/242.png` | H242：`242.tbt`／903201880 | 相符；`5ee66ffca19d9d3395318df30ee32aaf94c36753dadf8a9bb9935b564b07dccd` | I → 本機 assets/inventory/242.png |
| `engine/web/icons/245.png` | J：`245.tbt`／903199205 | 相符；`ea4ac1c311981c4380e17779b4b15f1ef2a2a71d2b1962a2591f203522e9b001` | I → 本機 assets/inventory/245.png |
| `engine/web/icons/278.png` | B：`278.tbt`／903882717 | 相符；`c7a17e518c4e3978a83446e66c4cf7ae5cd54caeb04184556442a48b9f75f220` | I → 本機 assets/inventory/278.png |
| `engine/web/icons/318.png` | H318：`318.tbt`／903848526 | 相符；`ec7b23a61616e87b992ae01fd95ddfa83430300141aa85f4ded444af31c46a26` | I → 本機 assets/inventory/318.png |
| `engine/web/icons/35.png` | C：`35.tbt`／903805861 | 相符；`9ded6e008b2f981978dab2874f924b72b8d5f96ea6c4c856c4ebe60884a00073` | I → 本機 assets/inventory/35.png |
| `engine/web/icons/4.png` | A：`4.tbt`／903706169 | 相符；`75ab427d9ee5b9051b6cc4103b43eaadfd7cadd75d73d29bae9caf28ce3ac93e` | I → 本機 assets/inventory/4.png |
| `engine/web/icons/474.png` | C：`474.tbt`／903594700 | 相符；`569b75068a19f536d78b12a6b75d8bf6e61655b9c1a7f282b0096b9043ecfa1d` | I → 本機 assets/inventory/474.png |
| `engine/web/icons/475.png` | A：`475.tbt`／903593506 | 相符；`af4f6debaadb18732f040901c0ecf166156782a25e26da6b6867445d3c325846` | I → 本機 assets/inventory/475.png |
| `engine/web/icons/5.png` | A：`5.tbt`／903564600 | 相符；`9e35714d6b77ab794679b0a32f295cea5a78b773fbb745f46ce00366a4b06ff7` | I → 本機 assets/inventory/5.png |
| `engine/web/icons/59.png` | E：`59.tbt`／903368573 | 相符；`cb4045a12f6d28c81bbb25e12173741b184f8bc2fc90b42d9063d0ee076a669e` | I → 本機 assets/inventory/59.png |
| `engine/web/icons/7.png` | B：`7.tbt`／903356012 | 相符；`90f23210260582bfcc8c9e8b7a00cb727840e73082e3c7ca33c1e1fbc8b3143a` | I → 本機 assets/inventory/7.png |
| `engine/web/icons/77.png` | D：`77.tbt`／903350892 | 相符；`19f5dd065bc709dbd1c0fe59bad01e0a8acfad39593ff90621f069a709c9a727` | I → 本機 assets/inventory/77.png |
| `engine/web/icons/80.png` | D：`80.tbt`／903348748 | 相符；`0a6b703b2b6b0709ce4c7364f36ed99ef4cc907712d7d0059aea90c0ed9f4f37` | I → 本機 assets/inventory/80.png |
| `engine/web/icons/81.png` | D：`81.tbt`／903348154 | 相符；`eda21f8685278d50e150fa50ea4975756ea5cede2161fc86f1726199615ab988` | I → 本機 assets/inventory/81.png |
| `engine/web/icons/86.png` | A：`86.tbt`／903344823 | 相符；`f1a70fcdcee29ea66ff20f204157927e07468cfb50da545adcc2c93831b389a9` | I → 本機 assets/inventory/86.png |
| `engine/web/icons/957.png` | K：`957.tbt`／656527524 | 相符；`9b1df82afe88dc2183d9f3e69e8218d4126330e1f8f4179bf861ae305aec3a31` | I → 本機 assets/inventory/957.png |
| `engine/web/icons/97.png` | A：`97.tbt`／903337895 | 相符；`a630764b5725c0fc3629a4b86cc60d309695e9673179cb35d73b406d2769ec1d` | I → 本機 assets/inventory/97.png |

icon124：`ground_assets.py:33` 的替換目標是 `for icon in [81,5,475]:`，但現存 inventory_assets.py:9 是 `[81,5,475,2,4,86,97]`，所以這段原樣重跑不會加入 124；目前 inventory manifest 也只有那 7 圖。原 TBT 出處由設計及舊流程指向 124.tbt，但本輪未找到能匹配目前版本的獨立生成收據，**精確生成鏈 unknown**。後續應直接以核定 TBT recipe 產生 124、比較 decoded RGBA，不執行整支含過期替換的研究腳本。

ground66：已定位 SPR → projectile JSON → key 0:0 → PNG 的原始碼鏈，但本輪未重播或核對獨立最終 PNG 收據；保持 **最終重播 unknown**。ground36 收據 SHA 與追蹤檔相符。任何一圖仍有 unknown 都不能核准新的素材準備流程。

## 現行封裝／Git 排除的缺口

- `engine/build-product.ps1:3` 現在指向 `_integration/dev/product-build.ps1`，該檔再呼叫 dev/rebuild.py；以目前來源為準，不沿用根規則的舊 r34 描述。
- `engine/_integration/dev/rebuild.py:194–203` 用 `rglob` 收 `build/client-product-dev` 和全部 `web`，再加入 assets/integrity.json。它會把 22 PNG 列進產品 manifest；「有 hash」不代表內容被允許。`:55–57` 的 lexical containment 特意容許 assets junction，不能拿它當可攜封裝的拒收界線。
- 歷史 `engine/_integration/setup.py`、`common.py`、`audit_assets.py`、`audit_delivery.py`、`run_stage.py`、`finalize.py` 會複製／驗證完整 assets 或 delta。部分已有 junction/symlink 檢查，但目的在保留原資產而非零資源。不得為本任務執行它們，也不修改其歷史契約。
- 全追蹤程式搜尋未找到已完成的 portable ZIP allowlist 閘門；`.github/workflows/build.yml` 現行僅建置、檢查及上傳 receipt/log，沒有本文所要求的內容裁定／assembly 完整檢查。
- `.gitignore` 已排除 engine/assets、build、vendor、bin/obj、scratch；**沒有排除 engine/runtime、engine/runs、web/icons、web/ground**。`git check-ignore` 無需真的建立檔案即可重現：runtime/runs 探針不命中，assets/inventory 命中；已追蹤 web PNG 不能靠追加 ignore 變成不追蹤。
- 後續新增精確規則 `/engine/runtime/`、`/engine/runs/`、`/engine/web/icons/`、`/engine/web/ground/`，保留 `/engine/assets` 排除。新原始碼 recipe/pins 放可追蹤的 `engine/source` 或 `engine/tools`；本機生成 manifest／PNG／JSON／音訊皆放 assets/runtime。不得用 `*.json` 或 `*.png` 全域排除掩蓋誤提交，也不得用 `git add -f` 送入生成內容。既有已追蹤資源是否移出 HEAD 是後續授權變更，本輪未做。

## 可實作的 zip 白名單與拒收規格（尚未實作／執行）

建議新增 `engine/tools/portable-content-policy.json`、`audit-portable.py`、`package-portable.ps1`，由另案 code 實作。政策檔只含精確路徑、內容分類、輸入 commit、SHA、bytes、schema、審查證據，不含遊戲 bytes。**沒有批准的 policy 就退出 3，不產生「通過」zip。**

CLI 契約：

```text
py -3 -B -X utf8 engine/tools/audit-portable.py source --commit <full-sha> --policy engine/tools/portable-content-policy.json
py -3 -B -X utf8 engine/tools/audit-portable.py staging --root engine/build/portable-stage --policy engine/tools/portable-content-policy.json
py -3 -B -X utf8 engine/tools/audit-portable.py zip --file engine/build/portable/tw-client.zip --policy engine/tools/portable-content-policy.json
```

退出碼：0＝該範圍全部核准且無 unknown；2＝內容／路徑拒收；3＝unknown、policy 不完整、解碼／assembly 解析失敗；4＝輸入 commit、hash、bytes 或清單不符。source、staging、zip 及組件掃描收據須同時存在、指向同一來源／輸出 SHA，才能宣告 T033 查核通過。所有暫存封裝只寫 engine/build；不寫 TEMP 或使用者目錄。

### 精確允許面

| zip 路徑 | 規格 |
|---|---|
| `README.md`、`啟動引擎.cmd` | 另核讀的新 portable 說明／入口，逐檔 SHA；目前入口不可未審即採用。 |
| `engine/web/index.html`、`panel.css`、`panel.js`、`login.js`、`interaction.js`、`trade.js`、`bonus-stats.js`、`private-shop.js` | 只有這 8 個文字程式頁面，逐檔 hash 及 source 審查，禁止同目錄 wildcard。 |
| `engine/build/client-product-dev/P11.ClientEngine.exe`、`.dll`、`.deps.json`、`.runtimeconfig.json` 與 `P24.Audio.dll` | 路徑、hash、bytes、架構、來源 commit 必須精確核定；須已移除本文 D embedded/resources 和資料常數，才可候選。 |
| `engine/launcher/P24.Launcher.exe`、`.dll`、`.deps.json`、`.runtimeconfig.json` | 同上，且 launcher 必須先完成本機準備接線，不再要求封存 assets。 |
| WebView2 Core／WinForms／Loader 及確實必要依賴 | 以 restore/publish 的受核對套件身份與待發行實際路徑逐條列出；依賴清單仍待後續建置任務產生，**目前 unknown，不允許猜檔名或 `*.dll` 整批加入**。第三方程式不自稱自主程式，須獨立來源／授權分類。 |
| recipe／content-free manifest | 嚴格 schema、additionalProperties=false、全值限制。只能是已審算法規則、id／定位／大小／hash，不得含已抽出的像素、動作表或文字表。 |

除精確允許項外預設拒收：assets、runtime、runs、icons、ground、vendor、source、_integration、work、主管、docs/demo、native-baseline、研究目錄、`.git`、DB、SQL、JAR、原 BIN、地圖、音訊、容器、截圖、測試 fixture、任意新檔。需打包的 schema 說明可按單檔另准，不豁免整個目錄。空資源目錄在使用者端建立，不從開發機打包。

### 檢查順序與繞過案例

1. **來源樹與政策集合相等**：source 模式列所有追蹤路徑；精確 path＋SHA＋類別＋證據必須覆蓋，D/U 立即失敗。混合 Demo repo 若需例外須另立可辨識的交付樹／範圍與審查，不可只略過某目錄後仍稱全 repo 零資源。政策自己也要有審查身份；禁止在打包時自動把所有新檔 hash 加入 allowlist。
2. **路徑先於讀取**：枚舉不跟隨 symlink/junction。檢查 staging root、每一祖先及每個檔的 ReparsePoint、NTFS link count/file identity，拒絕 junction、symlink、hardlink（包含允許路徑下指向外部的連結）。不得只用 Path.resolve 後再證明「在專案內」而默許繞進 releases；檢查與開檔之間的替換也要防護，複製後再驗 bytes/hash。生成 writer 的來源 TW_CLIENT_ROOT 永遠唯讀，輸出 active/staging 祖先若為 reparse 必須拒寫。
3. **ZIP 正規化**：拒絕絕對路徑、drive/UNC、`..`、反斜線歧義、ADS 冒號、NUL、Windows 裝置名、尾端空白／點、大小寫或 Unicode 正規化碰撞、重複 entries、symlink 外部屬性、加密成員。members 集合必須等於 allowlist；不要先解壓再驗。限制單檔／總解壓大小、壓縮比與巢狀深度；達上限／CRC 失敗／不支援格式一律 unknown。
4. **以 bytes 辨識，不靠副檔名**：PNG/JPEG/GIF/WebP、WAV/MP3/Ogg、ZIP/gzip/7z、PE、客戶端 idx/pak/spr/tbt/til/s32/seg 等由 magic／結構與已知來源 digest 判定。無通用 magic 的容器仍不能當純文字放行；未知 binary 失敗。允許程式 exe/dll 必須通過下一步，改名、重壓縮、截斷都不構成新來源。
5. **JSON／文字／原始碼**：解碼 UTF-8/UTF-16、JSON escape，拒重複 keys；嚴格 schema 限定每個值型別、長度、範圍。Base64／data URI／hex／數值陣列遞迴解碼後再驗（分塊拼接、gzip 包裹、JS/C# 常數也不能豁免）。pins 的 hash 必須 64 hex，path 為核定相對定位，不可把文字表混成 `description`。pixels/frame/terrain/名稱表／SQL rows／動作時序不能僅因字段名叫 metadata 而通過。演算法與必要協定常數可經來源證據核定；無法判定來源者 U。原始碼不靠任意 regex 證明乾淨，必須 hash 綁定已審版本及人工來源檢查；新增常數需重新審。
6. **assembly**：不得 Assembly.Load/執行入口做掃描。用獨立 metadata reader（例如後續新增的 .NET System.Reflection.Metadata/PEReader helper）讀 CLR ManifestResource table，逐項 logical name／size／SHA，對內容遞迴做相同 JSON/bytes 檢查。檢查 linked resource、satellite assemblies、`.resources`、Win32 `.rsrc`、PE overlay、單檔 bundle 和 FieldRVA／初始化陣列、長字串。不可把合法程序集簽章當成內容豁免。未支援 bundle／native resource 格式立即 unknown。P11 內前述三個 resource 當前均不准入；P24.Audio 只候選已審 CombatSfxPins exact SHA/schema；SDK manifest/version/icon 需獨立釘定為非遊戲資源。
7. **封裝後重新驗**：由 allowlist 精確拷貝、逐檔驗證、產 ZIP 後再讀所有 zip members 驗 bytes/hash/內容/重複路徑；掃描報告記工具 SHA、policy SHA、commit、每個 member verdict。發布只接受該 zip SHA 的收據，不接受先掃目錄後替換 zip。
8. **Git 防回流**：CI 在 PR/HEAD 做 tracked 全樹檢查，另用 `git diff --cached --name-only -z` 檢查準備提交的生成路徑；`git ls-files` 命中 assets/runtime/runs/icons/ground 即失敗，即使 `.gitignore` 已排除也照拒。合成測試只在 scratch/build 產生，不能把真素材 fixture 加回 Git。

必要驗收矩陣：合法空資料 pins 通過；改 `.png`→`.json/.cs`、音訊改副檔名、PNG Base64/hex/數值陣列／JSON escape、gzip 包 JSON、分塊字串、ZIP nested media、CRC 錯、未知欄位、未知檔、大小寫碰撞、`../`、ADS、junction/祖先junction、hardlink、assembly 同名 resource 換 bytes、額外 resource、FieldRVA 素材陣列、bundle 未支援、ignore 後已追蹤素材、scan 後 zip 換檔，都要拒收／unknown。Windows NTFS 連結案例和 assembly 案例未在本輪實測；不得拿 7 個分類探針代替這個矩陣。

## 未驗證什麼

- 未建置、未 MSBuild evaluate、未讀取或執行既有產品 DLL、未跑遊戲、登入、畫面或聲音；未建立 zip／Release。
- 未證明目前原客戶端能獨立生成完整 r28 assets；portable-delivery.md 的 terrain／skills／spr_action／Text 解碼查表等來源閉合缺口仍在。
- 未重新生成 PNG、未核對全 Sprite/Text/Tile 或原 assets；生成收據相符不等於新環境可重播。124／66 的特定缺口見上。
- 2,502 個 U 仍待來源裁定；C 是工程候選分類，不是對每行程式或第三方來源的法律保證；M 是內容分類，不是任意 zip 允許。
- 未掃歷史所有 commits、遠端附件、Git LFS 外部內容或第三方 NuGet 包；未清理歷史、Demo、原始資料。

## 給下一棒的提示：精確檔案與每批兩小時拆分

本任務是 test，故不產出 plan/design 專用 `docs/plans/issue-102.json`。以下供 Claude／調度器轉成後續工作；每批 120 分鐘是上限內的查核／可審交付切片，**不保證一批即完成所有移植**。若來源不足即交付具體 blocker，不能補常數表、假畫面或放寬閘門。

| 批次／接手者 | 精確檔案範圍與時間 | 可驗交付與依賴 |
|---|---|---|
| A／Claude＋使用者裁定 | 本報告附錄、`docs/demo/README.md`、`docs/native-baseline/r35.md`、`docs/design/portable-delivery.md`；30 分確認交付樹／歷史界線，60 分按 U 原因群組裁定責任，30 分派工 | 決定 Demo 證據保留位置及交付樹清理的授權範圍；歷史重寫另授權。未裁定前不刪追蹤資源。 |
| B／code 本機執行器 | 新增 `engine/tools/portable-content-policy.json`、`engine/tools/audit-portable.py`；20 分 schema、70 分 source/ZIP fail-closed、30 分拒收測試 | 先讓當前樹明確失敗，記錄 D/U；不得產「現況通過」白名單。測試落 scratch/build。 |
| C／code 本機執行器 | 新增 `engine/tools/PortableResourceAudit/PortableResourceAudit.csproj`、`Program.cs`，接 B 的 audit-portable.py；20 分格式範圍、70 分 CLR/PE 靜態解析、30 分 assembly 反例 | 不執行待掃 assembly；不支援格式標 unknown。測試建置須另案授權，產物只進 bin/obj/build。 |
| D／resource 本機執行器 | 新增 `engine/source/InventoryIconRecipe.cs` 的規格／後續實作；先查 P24 inventory_assets.py、ground_assets.py、arrow_assets.py、arrow_ground_asset.py 與上述 DEV 產生器 | 30 分釐清124／66來源版本，60 分定20 TBT＋2 SPR recipe及RGBA oracle，30 分交付 pins與blocker；不重跑含 DB／安裝／複製的研究腳本。 |
| E／code 本機執行器 | `engine/source/InventoryUi.cs`、`WebPanel.cs`、`GroundSpriteReader.cs`、`GroundUi.cs`，必要新 `InventoryIconRecipe.cs`；20 分契約、70 分本機供圖、30 分 URI/cache 測試 | 依 D 閉合來源，icons 不再依賴 web 副本；ground 保留現有原SPR行為。產品變更另授權並做後續實機驗收。 |
| F／resource→code | `engine/source/Resources/pickup-own-base15-gfx0-weapon0.json`、`pickup-own-base15-profile-extension.json`、`PickupAssets.cs`、`OfflineClient.csproj`；30 分來源/pins、60 分 provider 介面與本機生成方案、30 分等價 oracle | 全312 frames／200,628 pixels＋方向步序與42 profiles要有比對；未閉合不可刪 embedded 後以stub代替。後續另批實作。 |
| G／resource→Claude | `engine/source/Resources/ground-catalog.json`、`GroundCatalog.cs`、`OriginalExpTable.cs`、`engine/action-metadata-vectors.json`、`engine/haste-metadata-vectors.json`、`engine/_integration/dev/combat-sfx-tests/expected-events.json`；40 分拆pins/資料、50 分來源替代、30 分裁定缺口 | 原服資料不能悄移到C#；原客戶端不足時回報U並交Claude。fixture6016及附錄U14另逐項裁定真資料／合成。 |
| H／code 本機執行器 | `.gitignore`、新增 `engine/tools/package-portable.ps1`、`.github/workflows/build.yml`；30 分 Git防回流、60 分精確封裝／閘門、30 分反例 | 依A授權才從交付樹移除已追蹤資料；保留原檔與封存。政策依B/C；不遞迴copy engine。 |
| I／code→test | `engine/_integration/dev/launcher-source/Program.cs`、`engine/_integration/dev/rebuild.py`、`engine/build-product.ps1`、`engine/launch-product.ps1`、`engine/source/ProductEntry.cs`、`啟動引擎.cmd`；30 分準備生命週期、60 分接線、30 分錯誤流程 | 等來源閉合任務完成，分離程式manifest與本機assets receipt；lock更新由相應建置任務處理，不能改舊lock冒充相同輸入。再另派T033全樹/assembly/zip驗收，最後由使用者做原生流程。 |

依賴順序：A先定範圍；B/C可先建立拒收能力；D/F/G先閉合來源再做E/I；H於範圍核准及政策具體後完成；最後重新掃當時的完整交付commit，而不是沿用本次3061檔數。每輪只有所有 D 已移出正式交付面、U 歸零且動態功能另驗，才能討論發布通過。

## 補充：本輪讀取的研究程式／生成收據身份

以下只識別唯讀參考的版本，並未執行研究程式。receipt 僅刊檔名與 hash，不重刊其中的原始 record_hex。

| 本機研究相對路徑 | SHA-256 |
|---|---|
| `P24-原2x2地圖快取與跨區載入/inventory_assets.py` | `3b8fad12ceb0d7f7d006c621d981bf185ba3b9677a9002fac4f324170abc77f7` |
| `P24-原2x2地圖快取與跨區載入/wire_equipment.py` | `a82a26cefa72275b629ed9ebbbd74e8d3dcba6942a9908a0bba01839c8ceb0dc` |
| `P24-原2x2地圖快取與跨區載入/ranged_icons.py` | `fd64b5d344976debedb5c73e14e38e45f5c55c9e68033b85b72f5d2fe327000b` |
| `P24-原2x2地圖快取與跨區載入/ground_assets.py` | `3448d21b59ffd2a3d5b83728cb352ccd05d199231996703c74ff887bd1e7365b` |
| `P24-原2x2地圖快取與跨區載入/arrow_ground_asset.py` | `f96682c62caf7ad77ad3fcb98004770cfb8e9a7c24416572efec4eac4cb2a4ce` |
| `P24-原2x2地圖快取與跨區載入/arrow_assets.py` | `123eb0db69b2e878eb0be600e8376d5a2ca98c87baff6f3ef3a168592edfa4af` |
| `P24-原2x2地圖快取與跨區載入/generate_exp_table.py` | `0c27ffdfa2f0451e15b8b8db16c6cbc9a7303e1d9d2223d3de2c99301f922410` |
| `P24-原2x2地圖快取與跨區載入/reference/development/basic-food-home-scroll-r1/extract_icons.py` | `3c7edd197dbfdb6598a3520f0043b4b4ad2a024621bd88d7360f63b30da3ba9d` |
| `P24-原2x2地圖快取與跨區載入/reference/development/basic-potion-haste-r1/prepare_visuals.py` | `161cfab265fe1f92b4c4ff7fb75b8fedc30e04cef15e8d16c8d131ccf890196f` |
| `P24-原2x2地圖快取與跨區載入/reference/development/inn-dialogue-r1/prepare_key_icon.py` | `1a0104ccf0bedc7064c42b7fbbc39448802557892b3702d7ecd3711c4253213f` |
| `P24-原2x2地圖快取與跨區載入/reference/development/ship-ticket-icon-r1/prepare.py` | `5e23b7e362fceb8aad92746cb8c2b6e193c67898abefd6185c0baa64232d9400` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-ruby-icon-r1/prepare.py` | `13f1566287b94b588e1163a3c1dc7731136d0d7decef00365bb6d3a71add0f53` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-ruby-icon-r1/extend_318.py` | `f2444d13143b0640ff4b994f0be3f256da7ba75bc6529ad224c4a0cba85e5c5d` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-sapphire-icon-r1/prepare.py` | `92c2c40fcb209f08f67a10cc8e4bf715e34a6e95a400c1a2e2e13422e42d8541` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-online-gift-icon-r1/prepare.py` | `20b5263b9943dc176ec764793eee767c4fd31fa6b3a09d62be084a53228f3af1` |
| `P24-原2x2地圖快取與跨區載入/reference/inventory/icons/manifest.json` | `ba62b06d60227d11ebaf1617823a1ef3085e8ee756187e6abbdb7df0e7bba329` |
| `P24-原2x2地圖快取與跨區載入/reference/ranged/icons/manifest.json` | `c61b1a9f2206d7a5ae82ecaecbb795631a65fc9fac3f5f8e156313ea2ae0d814` |
| `P24-原2x2地圖快取與跨區載入/reference/development/basic-food-home-scroll-r1/evidence/icon-provenance.json` | `7fd2dad00ac6f71de856d99bc6888bea4122c9ef833c88ed775735b9c43967a1` |
| `P24-原2x2地圖快取與跨區載入/reference/development/basic-potion-haste-r1/visual-assets.json` | `529468c9348ae477cdf51eb3631571fbe387de29543c11e1a1e72e507af85ac9` |
| `P24-原2x2地圖快取與跨區載入/reference/development/inn-dialogue-r1/source-evidence/key-icon/provenance.json` | `e56e5f2539332aa462527552d301957c7fd246a597f61885d451c2af808c3ca3` |
| `P24-原2x2地圖快取與跨區載入/reference/development/ship-ticket-icon-r1/source-evidence.json` | `75d192c88875634726996a9a94b6c1d129d610dc9f91a2564b32da9858968f40` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-ruby-icon-r1/evidence/icon-provenance-242.json` | `19daa948cf75c3bcd53bef4e4caa72c62bb718c4554a824133f430250d645c74` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-ruby-icon-r1/evidence/icon-provenance-318.json` | `8c903898dcd1d7ceb8522a2be7a12d226fbdf8feb2b17bfd3b83aab6ae6de6d1` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-sapphire-icon-r1/evidence/icon-provenance.json` | `a645a5e18fd3a6fa4be308007d828b8b24202f4e265b624dd933e5095dce46cb` |
| `P24-原2x2地圖快取與跨區載入/reference/development/encountered-online-gift-icon-r1/evidence/icon-provenance.json` | `a3fc1cf54c112524d8469ebdd78260bf6ef284915bf109f96111d6228a1965a8` |
| `P24-原2x2地圖快取與跨區載入/reference/ground/assets/manifest.json` | `1d4fbe5327e804b861a37d92ce205963f4a38d43317f71c3f553a38c2fb0f395` |

## 附錄：全部追蹤路徑逐項帳冊

SHA-256 為 Git blob 原始 bytes（避免 CRLF checkout 差異）；C/M/D/U 定義見正文。每列只分類該路徑，不遞迴允許所在目錄。

| 路徑 | bytes | SHA-256 | 分類與依據 |
|---|---:|---|---|
| `.cursor/rules/project.mdc` | 6227 | `eb95881be5954c6377098475c5f8950029886063d484167b07ac4662efc2a7a1` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `.gitattributes` | 146 | `debc5c5694bef52ffe83520d09b7f8849ac1dfc814021a14d5adc692878f5a08` | M — M03：已核讀工程規則／設計／索引，不含素材本文 |
| `.github/nuget.ci.config` | 197 | `aec93b5637ab6d62470979a008990a4a336a287ab83a3f2541849ae735674055` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `.github/workflows/build.yml` | 2675 | `a00af90f444fc6cd4f70ed594c57e1f398b615aead3501f011d182db3cb63f50` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `.gitignore` | 1483 | `10cc0c416aa0f9c53318a9e162e3a0e2ab05007ef1c4b4ef64e47498cc77f77f` | M — M03：已核讀工程規則／設計／索引，不含素材本文 |
| `AGENTS.md` | 6143 | `7c2dc18d8ce8a00a0eef819de940e93772e97b8ff8967915b01fa7a3319a737e` | M — M03：已核讀工程規則／設計／索引，不含素材本文 |
| `CLAUDE.md` | 6143 | `7c2dc18d8ce8a00a0eef819de940e93772e97b8ff8967915b01fa7a3319a737e` | M — M03：已核讀工程規則／設計／索引，不含素材本文 |
| `PLAN.md` | 8010 | `4e98356fa38ef478455f5e0ab179ea8ab189bd50508a5b15bad66bd8c4002c67` | M — M03：已核讀工程規則／設計／索引，不含素材本文 |
| `README.md` | 1792 | `7e7b5ef84f25ea9de6810012e54445b86ddfa652351c7aa76ed784f59c69bb8e` | M — M03：已核讀工程規則／設計／索引，不含素材本文 |
| `TASKS.md` | 10993 | `4aa5cdd59fef8f8be8b871c8b3887ddb50c1f9c32daf04c157dde4fd626cb206` | M — M03：已核讀工程規則／設計／索引，不含素材本文 |
| `docs-AGENTS-history.md` | 11741 | `0c8f214831a286bec4a6844ab1293b4f4798ff829ef0a3d6507dbf455eddc040` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/ARCHITECTURE.md` | 16761 | `2ce5c63966fff9e490d607d04b8a4085e1079fb69a9edc8ff3e6eee5955526c9` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/BUILD.md` | 13987 | `6412d61043ef087d85db88fcb403e4120294ce50547c8acc0a69d73679ffcb5d` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/REPORT.md` | 7057 | `0d25057c5eab208224ae4cf34b0eb28b8d09b9739feb9db86cf9f25ba9e5e7d5` | M — M03：已核讀工程規則／設計／索引，不含素材本文 |
| `docs/demo/01-login.png` | 642854 | `3cffc2af04caf858a8e765eda4d8078a48b43bc893c5f390a4edcac23422179e` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/02-character-list.png` | 2588 | `4a6e4e3d1bf80ea103dd3423737f08530eeb3a6a48269c762e6534f2e6080ef9` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/03-world.png` | 711125 | `53e85ae9a11044ef14b64546d1d9b5d60b9ddb1ddf657c689e0d21419f82ce05` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/04-eight-directions.png` | 709648 | `e1d64a20978c67fb1c2a4ea1daa7d4de8880f0e86cfb3e86a36d4cf8ceb5bfeb` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/04-up-two.png` | 711405 | `f30ec0828e7505e03eac112eaaaef36adb5ff10e479dad981b4b07d2b8b3b408` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/05-click-move.png` | 701078 | `eec5e94bdebda188b2142342b2d32b54d86f395cd9d7f9502f2d1ead547da5b6` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/06-attack-first.png` | 705840 | `960597667c0f25e47b36ab8628cc6632538c6a38770e2c9a1946cf11b3a4a333` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/06-attack-second.png` | 708343 | `30a75760d513bdaf7c1a1f5482a78f6a66634a8309ee458da8ba629350611bbe` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/06-exp-before.png` | 454969 | `57a5ab8564155242738290cbdfb80d8fc1c57118199b219aaed2f9e2e3fec4ff` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/06-target-search.png` | 703516 | `2ebdee37022ebc24697db3e45aee067ca17666fe9bf6528958bd1e5aab8eb10b` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/07-arrow.png` | 451822 | `06f2a78c36110c2db9a8e5a1d24da777ac894907a23606b001b6bead82ff2569` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/07-holy-weapon.png` | 449810 | `986cba9122b180c34c0f5bdd76c9d5df7de72307736ae9c8d29b3c579e91369d` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/07-shield.png` | 451573 | `a7a319859f1bd0cca3378bec56522963b9ee40e43808aee0d0902b154127f08e` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/07-skill1.png` | 450257 | `c33a4340b25b4d65433cdd44dd74d098051ab2752631e8005a6bc99b7183add6` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/07-skills.png` | 449120 | `c2144453428dea8b8236395e50b1e70f5bb5e7cebb5ae573b31b19b1ca924309` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/08-potion.png` | 454731 | `affde941d37158ea84f4da3cb8fca8be53a16fc954628123a980ee1ef3a98168` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/09-equipment-before.png` | 451350 | `7e03c249c22342f7b74cf9a47e196e7483daf98f00233f920f3cd2e555a8889d` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/09-equipped.png` | 450789 | `531b0cf71e0f3cd055d25174108c0c6e7b37cc855d794e0d8184161948419ace` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/09-unequipped.png` | 443553 | `3ad89b03f9ade72f2854b4d08d72b8f31b4ee49005aae93ac52491b96f741d10` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/10-drop1.png` | 472275 | `0dc2bec44e760ebc0cc14a044fd020a141dfaa8496a43b603648e3c5a16133f1` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/10-drop10.png` | 473138 | `e7bce3be0b9ec425c4b10faadec260b0ee55fe67174a743863353fd199bb0d5f` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/10-gold-before.png` | 472646 | `29a7d9035f72dfd9d41b16ecd9881482cd13d622bdd52413b05118ccbfbceeb0` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/10-ground1.png` | 454793 | `b34019f190080352bbc4e03d0adaf8950be49db5cd292588207eb1f456bb7d0b` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/10-over-message.png` | 457226 | `68b9296c68ee97f7267a46d2e4d986c764e61bc7392772befdee4951236400d0` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/10-over-rejected.png` | 472696 | `44a17c1af2db183fc30d8fdd57feb355de07825e854c0d99487d11851a2215d9` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/10-panel.png` | 466284 | `07c3f1ac55205405a948be16c0f34f3d616f87b4ee4cd8013a4e6a3f533c9627` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/10-zero-rejected.png` | 472694 | `55688fd7f698d3ade5ae69b17bca253f9518bf22f68034d92b2e070a1941f1bb` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/11-stats.png` | 452458 | `aba2ae20a3e714a5ac659aeac3d38924bd18b2327b70e19057386968613b2554` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/12-chat-before.png` | 444006 | `f4dd058765e898c367077ce94f32e6a712bd1d4e92d7c591ce5cf35ca02e1329` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/13-before-logout.png` | 710287 | `7ddd5f0a28397dc2fb3d7a361737b0046cad0ad087bf4a8a8fefc6899d10fd6a` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/14-relogin.png` | 708182 | `427900c1db7dcfbc8e2984a164c8e4c46139af0a8599d626000044ae5176240d` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/15-audible-cast.png` | 713084 | `2c3a5e3012fbca0198f773ca818d8593dcbdef50e987105be670b9c332e80c12` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/15-muted-cast.png` | 713150 | `565f1c808390927b5ee7baf49251dcb50b2c43f2ce0e133a331d6b265f2ef822` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/15-muted-controls.png` | 454144 | `df35407804621453e6deb5dca13ac6e99f6a58ac7f548d51e73f0ccdcace31a8` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/15-sound-controls.png` | 452818 | `0b8881373231c709fc479fe52735702af0aafae993c66ca832370ea358427faa` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/15-unmuted-cast.png` | 713848 | `6abcc49767135e7f96edb725c73d69e216702302275c42db6b7e64f65db52207` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/15-unmuted-controls.png` | 453788 | `2e4d1fed84c994c74d0d31ac3f2aec9e7a243cd69bac6d3e68479f25fdf220d0` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/demo/README.md` | 6643 | `4a3067c5f3b240eacb6cbe5c56a96a3aecb638d0d55b6a25a402b2f7511b70e6` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/demo/RUN.md` | 3600 | `951fefcf8fe06fc35f3a5edd8146150755d3564c0b68f5397230ab5fdaee6379` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/design/portable-delivery.md` | 45680 | `68a9f42e64ea1d09a63d72d79c0ececd49f80d78ccc8781dc8eaf3a78e270e77` | M — M03：已核讀工程規則／設計／索引，不含素材本文 |
| `docs/design/sfx-and-ground-items.md` | 23441 | `9ca8c9807fa2556cafd46a55c1c91466ae2bc96e50b49a321d7c324cb55ed6fc` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/inventory/p01-p24.md` | 61582 | `70fe089a0ce9e582ee8b7f88266f48e55c78fd3f0968e5c6be148c35cbd4497f` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/native-baseline/CHECKLIST.md` | 4291 | `ab14ae3b5cc0bba98c59caef4a8dacf537ed4e87b640dd57033c1ca45cb68854` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/native-baseline/dev-retest.md` | 7224 | `c3a82ee8dfd220ff456b2456b1531bcc264bff23ecb95d66d9219fc112ca6c62` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/native-baseline/dev-retest/04-after.png` | 708384 | `5019f4f039c8f0048a3af99b8e77ab7d3c18d707c3667a78df441488fc043e4b` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/04-before.png` | 706293 | `8222e7fd1df6c6f534411be17f8992a203f64e5a8b6442e4e3ca8bfaf37259bf` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/04-start-diagnostics.png` | 530747 | `7bde3bb31522c9452482e6ca8b71cc3e5c7199200af2d66f5670504bf33f2568` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/05-after.png` | 712183 | `21763cfdb9c83358704e699d82fe924e91fc8d38f0645b5989860d6a5bc120cd` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/05-before.png` | 705796 | `e5db3ec14129f0a49c9b269e3ec2e2c0409aae3023df944be848f4a01ef2b0a8` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/06-approach.png` | 704681 | `3422bef3dd39556485f8cfde6b317edbbce66459103858d4595639cf07b4bd7e` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/06-before.png` | 711939 | `dd8774af535b974f2fd11eafc35f2a24b60d418eaef0720572cae48329fa9b3b` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/06-diagnostics.png` | 531384 | `b7fe669617d6ae3e2af7ed7ac89deb81706b000b5db3a4b7ab00ee3d7a6e112d` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/07-before.png` | 706208 | `681e5d7854e78d3d133ece9bfb50fc51e60d5bb1f4376def7beaf051634f7290` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/07-effect.png` | 531025 | `dc5bcf99445fbd3904327461082df6607555a521ff2fe9917a490baeacbdcd45` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/10-gold-selected.png` | 271240 | `320514dc23c23206fa31c918314edd509cd3720339215bea72a8b70263d10793` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/15-audible.png` | 255522 | `d9e2189754a738e6e595a528d0e302e1420390bdd838844c78238e4f16e94d63` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/15-closing.png` | 255319 | `7d743010698cc6789a33be4f42859055c9c858d4da990ca3b4f0d6e0e2bd7d8d` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/15-controls.png` | 249701 | `9ae902bbc792d32930a702cfa92a11106be44f90f644a9eb5054f85e54539099` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/15-muted.png` | 255990 | `1a78bb4ac1a8e751f807f4be2e128c079ff27893373790e3b4c566e614b6f930` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/15-unmuted.png` | 256103 | `6898e99c2d1d05c2e0eb22fc8fd439ae8dc1841ac1d1223c8e2d4d8764a4d179` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/15-volume50.png` | 264434 | `8ffd1c6db7e6fc37f5eb132bb7aafc1dad8347fe20f800e27eca015a66974c2c` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/dev-retest/MANIFEST.md` | 6228 | `285693b66ead510ee2f0c99c90829d2d49766b8ad2e48ed209de085d3c998627` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/native-baseline/dev-retest/trace/04.jsonl` | 85643 | `2a09c3aad72ea42d6214093e70b5f5e22db235e5b992ad15cb9e8df81cb73e2c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(1156);keys=utc,dir,op,len,seq |
| `docs/native-baseline/dev-retest/trace/05.jsonl` | 70846 | `a7a09a3092a42a0d22e53b0de4dd7de5c749e34c6b9091839fb6ac7df29c899b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(958);keys=utc,dir,op,len,seq |
| `docs/native-baseline/dev-retest/trace/06.jsonl` | 97569 | `10e903ceb1cf5ea49a9a975dc6e63ff409bbda77cf420006dadc11b74e09b5e7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(1315);keys=utc,dir,op,len,seq |
| `docs/native-baseline/dev-retest/trace/07.jsonl` | 20786 | `9458ee330f304f87761c8c87c3e5a1a523623ff448092057d8b247965a0adae9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(282);keys=utc,dir,op,len,seq |
| `docs/native-baseline/dev-retest/trace/10.jsonl` | 16867 | `fdde0d4f646dd9d92abefad1a4a70421270e617f7f4d341fa1d6648b2cbd1959` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(229);keys=utc,dir,op,len,seq |
| `docs/native-baseline/dev-retest/trace/15.jsonl` | 38983 | `750356442f0bf6cf33a952c80f4dff1db366827f50cb65433ae2ab7dfab6fb5a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(528);keys=utc,dir,op,len,seq |
| `docs/native-baseline/dev-retest/trace/trace-summary.json` | 29362 | `12dcb860bc33f5c3cfa58fe0af9281c2ef4c0ffc8ad8aee35f82b08a86e5a5ce` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(6);keys=window,scope,in,step,counts,fullSessionCounts |
| `docs/native-baseline/r35.md` | 10616 | `b8a5555e93c07bd8f4da1d100ea845db2c86cce3349f19025f16241fc1fed309` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/native-baseline/r35/01-登入畫面.png` | 659169 | `b7bb6f0ac916051f485d0045bcf622ef30b620195f421fceade5602b5ae5c798` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/02-選角局部.png` | 4960 | `b39862815604fd7b2c0cddac51fc04913aaabf71b4f2f91b4c7a65f08fb5f22f` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/03-世界完整畫面.png` | 582668 | `3e82fbe4c20c515c61d9e8dd16ce10fe1d53ebfca0802f536963891e4b12f808` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/04-接手後-上2.png` | 1069708 | `8a951858c30f8801c4b4a1ae0971a5dcae6f99125a66a88e77f4ca4d36927318` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/04-接手後-左下2.png` | 1071491 | `b02d88b10e0bbb8c7f8ca3e7e179c7f5cbfc75b400312146576245208f0a5a6e` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/05-點地移動結果.png` | 1079478 | `9342e535c1a2dd87fd14f53015a583f139d27abfe74e155e6a86908b686a6f84` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/06-重試攻擊結果.png` | 1082860 | `82391bbe576e44eca4b6f2f1c5b3b5627ea268c3d599a98d0cb39944a7a4b4fb` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/07-技能1初級治癒術.png` | 1077341 | `97e7085ae79929598e8ed5f77dd3959d0cd19d0f963d45f4a981819611dcc086` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/07-技能3保護罩.png` | 1079723 | `9ec620cc1065188a343071f61217d3159eedf6d3ff386c39b5acbf537ff15745` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/07-技能4無目標嘗試.png` | 821092 | `89d247097a6338d130b9b72b0b78daee7c625c58735b735fe307488ec8cf0855` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/07-技能8神聖武器.png` | 822190 | `2cf5798a65dba3e13d89bd6f2aafa6a585286fe16fd4d19df57f21a11549678d` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/08-吃肉後.png` | 845500 | `6437dd288ecea4ed4b67a28a7272ff861d18f73056997a3e0318072ca2f481bc` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/08-背包操作前.png` | 844286 | `69a2f393727e1b96881596cce180696159cc34ffb0a753b0b8456983903a3d1b` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/09-匕首選取.png` | 822522 | `af38e276b49787181198c894fca56a35ca8598762d5885888e135212696bc4bd` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/09-卸下確認.png` | 818564 | `3278fc982d7656f3b230ab325c5ca3e33d154ce6e749f17256c1610f86b140c6` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/09-重新裝備匕首.png` | 821876 | `d91f3fbbb5ec5a06b265929dd0055bd5ce4fa6839e3d589b175ad3037d106ef1` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/10-丟棄一個肉.png` | 797047 | `8fae94c759a944e402103acc01b5d13584e113b63766c0ed02af8f0f8abac4e9` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/10-地面肉.png` | 771591 | `e529ad42758de0f9dda9ac663af61ebd6e10bc8e496f043df9f99c062ba59982` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/10-接手目前.png` | 795549 | `cddbca75e1aee88d8b0193d1b380c49c1886ff477fa83274b7c68784605b869b` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/10-連續操作檢視.png` | 769245 | `1c4aa064832a76386a31ab4f569a4c9fb7031c6e3b108bcb2fafee1c16d1df9d` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/11-力量加一結果.png` | 783740 | `07ed19aec1607f4579f95a0218a71517200bc7d0aa53e8ed4f54d52bb73299a1` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/11-加點前.png` | 781905 | `b182ea5a7c1625dd89fa534d62032ffc85f4bc89117b7446e0e30f574d7245c0` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/12-聊天回傳.png` | 818402 | `af38559cce5e2d38e0044b51a5616555622e1fe8da25bf1b2f759a63798953ea` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/13-第二次關閉檢查局部.png` | 4354 | `ba8311540682316adb9e6ed7c89fea29e1f62109144cddad5bb9ef1aa38bb21e` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/13-退出前診斷.png` | 874318 | `0698aa1f6d09dcd8cae3590393c6b06ec7d16211f58a0ffa719bed00521c9187` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/14-力量保存.png` | 461136 | `0f8e233e62c32c8b2a6d16b56f296e4f23cee3ce956584bfcf3d8947a146cda9` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/14-重登穩定畫面.png` | 701543 | `3b1f7d1c6b191ce9aa3f1c1d40a11b19a10e0662e8efbd871c50696633808130` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/14-重登背包裝備.png` | 467299 | `30461b833efd86c629bceea65032016086e81a382d754d8cd261f1b8f7cf20a0` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/15-取消靜音.png` | 780018 | `54f3a3a80e52846e8960cdce036a70ec1e388e9a21a39d1e91af20b44df3152d` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/15-聲音設定前.png` | 781111 | `7cb25d26039c4b69e4fa1e439f9185306cdcf2cfc2ac5af96738a3747ec32209` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/15-靜音.png` | 780123 | `a9170b60c3854a13c4730c8fe8dfe6742efdd1f078b28a670670db40d5234127` | D — D02：PNG magic；Demo／原生畫面證據，含遊戲呈現，發布拒收 |
| `docs/native-baseline/r35/MANIFEST.md` | 16331 | `134c3c4534a056964963d817ebb63b96ce48d9e903894bf7a7604c0682e200f7` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/plans/issue-42.json` | 8282 | `864825091305da2d0bf335c24f40f22c8b2bb085128fbde9e4e5b25003d2697e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(6);keys=id,title,stage,priority,text,depends_on |
| `docs/plans/issue-97.json` | 4406 | `54ffbd092bf6539e7bd15c8879480d4eaed66400540ac8a5cf9280c69511f478` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(3);keys=id,title,stage,priority,text,depends_on |
| `docs/project-consolidation/HANDOFF.md` | 1910 | `cfd4d4ff63a7ac0faffaf14a73e7bf3ade86c2a3275a4358e9917b6bea12bc2f` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/analyze.py` | 3065 | `4b0e03ac17eaa1d3919f0d5333b987c51980a8ff2621fbbe89beed190a5c959e` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/deliverables-sha256.json` | 3649 | `3bb632ec8a54770b2fa67fd2302bab89ddaa55e475393421b64c2d6b14be7d20` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/dependencies.py` | 2467 | `4033538dd1759281b261911f13ff765d0a8ff86ff9d9439aba4a6059f7521b21` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/duplicate-groups.json` | 824348 | `89a111ddfe72278750a478fe747ba2fc400b88719a0d8954966db3a9dd140f60` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(600);keys=bytes_each,sha256,files |
| `docs/project-consolidation/evidence-index.json` | 6105 | `77e72264ee821daec6383ddb5136f0c04b276cad964a93aa7e5e6cb86ded14b9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=read_at_utc,r28_pin_checks,sources,resource_correction,caution |
| `docs/project-consolidation/hash-summary.json` | 2439249 | `1d3302dda40546533c3dffca9168da16eecd5d7a51ae7b0d6f0b673938e96425` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,roots,excluded,errors |
| `docs/project-consolidation/index.html` | 25581 | `0c37dce07e9908295a819c7f962d37a0ea38b742e3ced258cc48ab57fc4dff20` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/inventory-report.md` | 3970 | `dfd9f7c6d498c58f0349fbc40f0b988efce9059fa33b6bc88c9602416196e382` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/inventory.json` | 28933 | `61ab3560a4448a4c6aa0e5ed126afc62fcba1c95d3421fcae8a71900f41a866a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=created_utc,method,roots,links,errors,agents |
| `docs/project-consolidation/inventory.py` | 2984 | `2e2aac65eddc426c2d6c397e81e26f7fc953e48b218a9518bf32621683c32498` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/migration-plan.md` | 4417 | `ae2ad52ce5cdbfa00120fc59293a908f44d6d39776751a3c529b24d314644f1f` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/path-dependencies.json` | 359859 | `4d612beff20ea42e52d3bb58513fa163f64b4237e1ea9aade9fb69b09e47cbc3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=text_files_scanned,scope,errors,target_counts,hits |
| `docs/project-consolidation/path-map.csv` | 3604 | `63eb22ae86f00c41abeef056e7b3b17822a49f03fcf905de7689cf991c8b8569` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/path-map.json` | 18965 | `a295d71ac1bdd155f12c1162632a70398c06801bbe04ac2ea519d78408a168c6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(16);keys=root,files,logical_bytes,latest_file_utc,top_level_bytes,hashed_files |
| `docs/project-consolidation/physical-migration/P10Lab-TW13081901-validate-ascii.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P10Lab-TW13081901-validate-ascii.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P10Lab-TW13081901-validate.stderr` | 211 | `251aaf859763127d7a80d77ce63b1af26b9e31b067e6ae9a938417ca9fadf811` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P10Lab-TW13081901-validate.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P12Lab-ClientEngine-validate-ascii.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P12Lab-ClientEngine-validate-ascii.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P13Lab-ClientEngine-validate-ascii.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P13Lab-ClientEngine-validate-ascii.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P14Lab-ClientEngine-validate-ascii.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P14Lab-ClientEngine-validate-ascii.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P15Lab-ClientEngine-validate-ascii.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P15Lab-ClientEngine-validate-ascii.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P16Lab-ClientEngine-validate-ascii.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P16Lab-ClientEngine-validate-ascii.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P17Lab-ClientEngine-validate-ascii.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P17Lab-ClientEngine-validate-ascii.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P18Lab-ClientEngine-validate-ascii.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P18Lab-ClientEngine-validate-ascii.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P19Lab-ClientEngine-validate-ascii.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P19Lab-ClientEngine-validate-ascii.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P20Lab-ClientEngine-validate-ascii.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P20Lab-ClientEngine-validate-ascii.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P21Lab-ClientEngine-validate-ascii.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P21Lab-ClientEngine-validate-ascii.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P22Lab-ClientEngine-validate-ascii.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P22Lab-ClientEngine-validate-ascii.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P23Lab-ClientEngine-validate-ascii.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P23Lab-ClientEngine-validate-ascii.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P24Lab-ClientEngine-validate-ascii.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/P24Lab-ClientEngine-validate-ascii.stdout` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/ascii_bindings.py` | 1525 | `8f635d9ee1d53db6836f24b9c1267b3d8d3626e68c2284330ac1867505cbc61a` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/physical-migration/authorization.json` | 1333 | `b4dd388cb8786459c70ebb36e9c4d118501166e3c61b9e0a2ee5a2c27fc79417` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,owner_thread,parent_thread,user_instruction,permanent_deletion,development_resume |
| `docs/project-consolidation/physical-migration/backup_databases.py` | 1649 | `7dc99acd93056821c227f5cf91f3f7593890feba76e141a0f8fd785e06e1bba0` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/physical-migration/before-P10Lab-TW13081901.json` | 372 | `be0e93b30f76cf4e06aa8d6718d684264b3e1e99651ca3d20da772e347ce2745` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P10Lab-TW13081901.jsonl.gz` | 123010 | `de8e79cd99212e03d917165ceb83b32f4e59a1057e0f51e83c17d8567653fade` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-P12Lab-ClientEngine.json` | 376 | `3be209d3f0ec24eb3cddcc17f3d4bd4a5eb4837600ef6343d779f09b8a074f48` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P12Lab-ClientEngine.jsonl.gz` | 83872 | `2f597848ef52de69524dd1c5106d0b2f387d1e7cbd75be776f283d55bd2c046f` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-P13Lab-ClientEngine.json` | 376 | `48df625ac5cee5e08a90aaa228158b1da8f362a6bff0b12b097609fe3fa78a61` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P13Lab-ClientEngine.jsonl.gz` | 83164 | `d142cd7c1fc62d118c9a1e8ece4466399067e914e8fa6bfbc70118620f6c0aae` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-P14Lab-ClientEngine.json` | 376 | `50d480dd36b3fa901726ae490ae654520db3af8a1fe2fbed9b3941de79cffdaf` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P14Lab-ClientEngine.jsonl.gz` | 83626 | `f6f9d6f2887334d97be433ef70e31ac6bb8c0add02f755592785e1c849743478` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-P15Lab-ClientEngine.json` | 376 | `2158d7645733c4dfd78c3445413703a31cf6324840a24298a1881e4322cc7c6d` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P15Lab-ClientEngine.jsonl.gz` | 83311 | `aec6d8effbb2b27377201679bdc48a2d3a28f761093a675c3b3bef9caaae2b76` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-P16Lab-ClientEngine.json` | 376 | `f57e20dbaffb200f7bfc56803a18f0a698533f2df5902384798dc9bde6b8fc7d` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P16Lab-ClientEngine.jsonl.gz` | 83452 | `e686e24ca5ea63f2b812e86ee21a1a41e1a64081abfa3efa8248264ba07b8613` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-P17Lab-ClientEngine.json` | 376 | `ef9c59405be79bfdd00af41a5abdd4b763089ae327138b349281bb4e72d9ae46` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P17Lab-ClientEngine.jsonl.gz` | 81841 | `636889f4add8f2f392610d4ec21ce12a597ca663624fccc8379c40711f738177` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-P18Lab-ClientEngine.json` | 376 | `cfcc9e638e92d65c07cd89550ac325b740a539c6de51409a188a0e93b58d0c07` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P18Lab-ClientEngine.jsonl.gz` | 82084 | `3951da9909e2b890a917c9d485dd486e1233c78c632398c37a4c46a6729cdc2d` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-P19Lab-ClientEngine.json` | 376 | `2e08daed339299a1397638a3214ebfe33289830a2b24268e0adc69db74421aa2` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P19Lab-ClientEngine.jsonl.gz` | 82311 | `aab6fd53ae97882694209eb17c5cbc20df9e69bd75e25ae943cab4cb3ef2e399` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-P20Lab-ClientEngine.json` | 376 | `23be5f0ff5f6e5a02af79712bbeda4bf674b0815c6aafd346a6523b7c901a47b` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P20Lab-ClientEngine.jsonl.gz` | 82175 | `e89ef1c4bc4c83b27e34f634100a47a79e067ed962f8c31d12058731f20eae36` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-P21Lab-ClientEngine.json` | 376 | `58ff3d304bc2cd69238e49c6846855fbf3aaa2f8c79045559dd8b90e286c7622` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P21Lab-ClientEngine.jsonl.gz` | 86303 | `ad4773aa07455d09f16230d10fd037be2c0cc09d71646e06771cdac7716724aa` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-P22Lab-ClientEngine.json` | 376 | `e07750f433f9c32a71404ea1376a10fa8609e1e0bb75169a7cf2f3ee29b8c29b` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P22Lab-ClientEngine.jsonl.gz` | 87277 | `f458f8331f5cf937c8266606e86ba0d21f79d58fb858c09289338581a99f514f` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-P23Lab-ClientEngine.json` | 376 | `a2a6944fb407dc53f2d8ec3ef8f76c3d07e5e9971d0663e2872ae8e4366fe273` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P23Lab-ClientEngine.jsonl.gz` | 86366 | `4d8718249eeaff43068649326db21462e00d24709874267b75f4d15cef5097b0` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-P24Lab-6016-Export-r1.json` | 379 | `330bd8ef37db546e2635d78ab7038a8533712a850b8c4f48dbaba1ed0322f81b` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P24Lab-6016-Export-r1.jsonl.gz` | 7265 | `a0b15c4f478ec17b5f571d19ffad4182709bb3caaed71102f1cd9293970eb6de` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-P24Lab-ClientEngine.json` | 376 | `ff32dad4303754a0c9bd9aca7c776627e2c92d7bbb51ec74b813f68099275592` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-P24Lab-ClientEngine.jsonl.gz` | 85776 | `2ded39c79d7d4a169893f5fd3cdfdbf46e7f1da1884b89f33112826213f957d4` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-TW1608262200_research.json` | 380 | `071369e00873bdb213dc7a7eee8bd2638cbc4364f72ff308e381d08e3566279f` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-TW1608262200_research.jsonl.gz` | 26818 | `ea30a0be7b5822eb88ac3e9e3fcbb7254c695ba5ac489cb7986e53c2fd936a2b` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/before-r28-interrupted-partial.jsonl.gz` | 663435 | `521b06bbe7662c5ec023ba2e7b26ccb31f2f91c46cda1e780761e824f3d4cdc2` | U — U03：gzip 無法完整驗證 EOFError；unknown |
| `docs/project-consolidation/physical-migration/before-r28.json` | 474 | `d10c9d1565d48ae30b1369de083428b98f7ed2d5189a886662dc929b0cf523ac` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/before-r28.jsonl.gz` | 661182 | `53cac9fc545e622f2e37c6ee62e836724e9e71431f1820ecf40cc4c1cab30c3c` | U — U02：gzip 已解壓，非白名單 JSONL schema；unknown |
| `docs/project-consolidation/physical-migration/cold-backup-restore-probe.json` | 825 | `2b7b78b2281a5ccf26a67ff1ce6c11f79e0a5de7676487158a5eb6834e7374c3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,files |
| `docs/project-consolidation/physical-migration/config-relocations.json` | 29275 | `722dc94330500d1e595f3ad460bbf568fe93100f42ebf302270eb74b7fe0a866` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,changes,ascii_alias |
| `docs/project-consolidation/physical-migration/create_engine.py` | 1671 | `232fbf9918302c8bcb07ff2a0849f2669be58a43d871a8528d3df0217bb0135d` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/physical-migration/current-entry-dependencies.json` | 250 | `79ce33e70d833cf4294d6bd5335b6a22c2e37216f5e26409ba14d79a7837a69b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,files_scanned,old_lab_root_hits,runtime_settings_receipt,result |
| `docs/project-consolidation/physical-migration/database-backups.json` | 7628 | `b8296ff30e0ecd19c9cc7ed8bc7df8271dda5a47f9fc2763f10de1c3ba9490f7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(14);keys=source,archive,files,source_bytes,archive_bytes,verified_all_archive_entries |
| `docs/project-consolidation/physical-migration/dedupe.ps1` | 1710 | `82f05391c047b00b04912fffd08a508950af4143c6826cff45e390d6ca30cfad` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/physical-migration/deliverables-before-resume-sha256.json` | 2993 | `cbfa902ff48ded9dda93222b1b6f85ae2235a6af798918e77b09470ff2741007` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/physical-migration/diagnostic-observed.json` | 5023 | `6939cf4ccdaa8f666573ee82e15c025dff3a783d97a2f6977d922fafc8cff99e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=supervisor,workers,scope |
| `docs/project-consolidation/physical-migration/diagnostic-relay.json` | 1508 | `2da14423da5eed9e471f8a6242dcf49f930343afceaf6aaeba2d6e6926e0e9f5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state,recipient,send_count,send_error,parent |
| `docs/project-consolidation/physical-migration/engine-helper-fix.diff` | 1182 | `03da46bcd198e323e485030bf32cb6641306364e8583a0e056715ba7e5487cfb` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/engine-redirect-observed.json` | 4987 | `fe939a0c1ff7c75b8048296bae808f833e6c6c3e826aeff9688e7be2f73ec0db` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=supervisor,workers,scope |
| `docs/project-consolidation/physical-migration/engine-redirect-relay.json` | 1666 | `4c0da31713e898a1cd95f5e26c3ff97cdc3f585df9e0b06cd585588d564548e2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state,supervisor,send_count,user_direction,database_diagnosis_or_repair_authorized |
| `docs/project-consolidation/physical-migration/engine-source-origin.json` | 29100 | `e240d721f25b38796409820d57521c108fc19e0d24d8589c58b413ca56a70a5c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,source,target,files,assets,new_gameplay_changes |
| `docs/project-consolidation/physical-migration/environment-config-validation.json` | 5801 | `0f1f1adc644cf429f932250fcf14db07588a344254f496181be8d2cd27c46fad` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reference,results |
| `docs/project-consolidation/physical-migration/final-process-check.json` | 285 | `f49685c094e2b0dc23ee54a00889b65f4668539397aee1d3481e19510804c1c0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=servicesStarted,mysqlProcesses,projectListeners,projectProcesses,utc,stoppedProcesses |
| `docs/project-consolidation/physical-migration/final-verification.json` | 7465 | `afe9bd1ad52cc66d902dc69d2cd34623378939102966f55052faf89defdb7773` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,preservation,formal_r28_full_sha_files,formal_seal_sha256,hardlink_substitutions |
| `docs/project-consolidation/physical-migration/finish_migration.py` | 28176 | `3a61d8ce0f120b6a466057bd06707098a8aef7fd4366482903c5151e22bbf05b` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/physical-migration/manifest-before-resume.json` | 28831 | `1c880f6558397d0529d7945c1bbb63dfaab59b53cc4e01f10ad9180f471314dc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,updated_utc,canonical_root,stage,revision,whole_project_complete |
| `docs/project-consolidation/physical-migration/move_batch.ps1` | 2919 | `22e59b70b726f97631b0d977d26dc54c7ffa6d66efc4c494ff01aa79f07498d2` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/physical-migration/moves-external.jsonl` | 6908 | `b5b4495f762b3628975a95f355e1f3f95eb1d67d71b4e4f75742739122814d9b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(32);keys=utc,source,target,snapshot,state |
| `docs/project-consolidation/physical-migration/moves-r28.jsonl` | 585 | `d48f44b64118398541503a2ace9c4afcac8fbdaa566891fb141e41c69420958e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=utc,target,snapshot,state,source |
| `docs/project-consolidation/physical-migration/pause-confirmations.json` | 1295 | `66faa8f9b8886a2531b4a7ff5aaf6d9d4b71d3392d6b67a71d97ae4016d301a4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,confirmed |
| `docs/project-consolidation/physical-migration/phase1-documents.zip` | 39159 | `286d1bcd4d20eeb4984d0c76e4ba606cec68a63770c19cb9f6e16a25c6725e00` | U — U04：ZIP 11 members 已讀；文件語義未全數核准 unknown |
| `docs/project-consolidation/physical-migration/prepare_dedupe.py` | 2834 | `05916333e70533958896e9072ed9b1bf676b6c51d50dea41b7387805bea9445b` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/physical-migration/preservation-progress.json` | 5779 | `1c37c875ac7e910448a0c77991c70d57368331ded45c335c91977001fbfdac69` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(17);keys=source,target,files_accounted,content_sha_verified,volatile_evidence_size_mtime_only,config_changes_verified_against |
| `docs/project-consolidation/physical-migration/rebind_environments.py` | 2022 | `b59f0b58ab21f0cb1bfd69f44997e3a492683388b56a9dfa6ab210eaf712e53a` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/physical-migration/record_resume.py` | 4070 | `6307caf9ef0376a027a053b7dd4876435e0d07860c440c98ba0f2515258b05b0` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/physical-migration/report-verification.json` | 9906 | `31a02cf701d8b320c83dc09d14dcaed63533eb1ef5a181e392b27cbd07fa6719` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,maps,old_roots_absent,html_utf8,local_links_verified |
| `docs/project-consolidation/physical-migration/resume-handoff.json` | 4084 | `05add19563d29b65eb8cecf572c752262647111db5eace6cd730a6b1a109aa9e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=state,organizer,supervisor,integrator,pre_dispatch_observation,goal_directly_read_by_organizer |
| `docs/project-consolidation/physical-migration/resume-observation.json` | 6070 | `93b901cfdb2b863e8a00441cb3ca6a12669f0981522aa8d0204c1d1fe7c5be0f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=threadId,title,status,turnId,turnStatus,error |
| `docs/project-consolidation/physical-migration/rollback-probe.json` | 578 | `ecb007c9a834dbc8df674baa502e439aa57345caa4d6b4b470ff9b476e28ec87` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,restoredOriginalSha256,result,reappliedSha256,permanentDelete,utc |
| `docs/project-consolidation/physical-migration/rollback_probe.ps1` | 1561 | `4bb06f68667bc1a1b1f9906d06e8dbec2da67b357af42c622f55fa3d8935f571` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/physical-migration/root-entry-check.json` | 1247 | `f15f847b843149c35dd3d80d5937ea6c47faf661796c52ceb377c99850f98d1d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,revision,executable,workingDirectory,arguments,manifest |
| `docs/project-consolidation/physical-migration/root-entry.stderr` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/physical-migration/root-entry.stdout` | 1285 | `97e0550b222eced0354e49c7a443a7a2ba8a869e691c90be948f0643e39f9b12` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,revision,executable,workingDirectory,arguments,manifest |
| `docs/project-consolidation/physical-migration/runtime-archives.json` | 1048 | `0787a0e337c90fc426d0b07a1d6f757709d080351c21c971be4e96eedbe12204` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(5);keys=state,source,target |
| `docs/project-consolidation/physical-migration/slow-actions-continuation-observed.json` | 7782 | `24660d87fce6df663b8bbc28495dc6c3efe61c14d78779d78ec2bb8ec704bb5c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=observations,scope |
| `docs/project-consolidation/physical-migration/slow-actions-continuation-relay.json` | 1472 | `4bb6bcc6f42216aff5b5bde48602752443e972474f439558d79d3a9c712c064e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state,supervisor,send_count,scope,db_or_server_operations_authorized |
| `docs/project-consolidation/physical-migration/slow-candidate-continuation-observed.json` | 6434 | `bc8dfa8579fb4710ea47e40492218c4cc3d18d30ee2c9f5347d7bc646e739b52` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=observations,scope |
| `docs/project-consolidation/physical-migration/slow-candidate-continuation-relay.json` | 1263 | `4231492cdb7aedbdda9ce7faad57bed2537621aa5018cb86bce11fc075c00ad3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state,supervisor,send_count,scope,db_operations_authorized |
| `docs/project-consolidation/physical-migration/snapshot.py` | 2327 | `5e5633a74a0ae1bbbef3beaa461723162c2eef0fa147d0a7dd6d365dcccf964c` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/physical-migration/split_runtime.ps1` | 1245 | `0a7ae7798e8173086c7bff765a31ca8f88007989839b0cd17b24ca4f8d124927` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/physical-migration/status.json` | 906 | `cc4277ac131991b9cfd1fa9744a07c1f23e1d69179a5029886322bd475028cbf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,scope,B1,B2,B3,B4 |
| `docs/project-consolidation/physical-migration/validate_environment_configs.py` | 1625 | `46cbfbc511a29f0c9b33d112ce89771ff02beb53f2d04623591474eb597c6e07` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/physical-migration/verification-correction.json` | 676 | `f59ce51e68c5c111434a4eeace20920f48ea828ae95085ae086fe1d7193070c1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,supersedes,reason,replacement,proof,volatile_limit |
| `docs/project-consolidation/physical-migration/verified-moves-external.json` | 7512 | `3d1675e49dc7f7881fd295ff7a7f7a007043933d0c747f76e86b524c1a0915b1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,results |
| `docs/project-consolidation/physical-migration/verified-moves-r28.json` | 614 | `ec0096c06d91183d88e70b01917721576dea5e69a8937350cb88d2094fc7b264` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,results |
| `docs/project-consolidation/physical-migration/verify_final.py` | 7573 | `c2ef0a20f4cffaa614c4dd84460e5d3c81810a33f975ca6693ce773afc37a7e9` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/physical-migration/verify_moves.py` | 1363 | `3b6f843c88db9ce19d3e32cf5d1df608d816b51904a3f809fb0d35cb2d9af7a5` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/progress.md` | 8244 | `0c34704dc28c59dde35ebc15059aceb53892be382f83e95329d3bcc52c6f516b` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/publish_report.py` | 15373 | `49cfa032243778bf460f01271f6a5a6fbdecaff717ab8fa9ea810ebdd3364f7b` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/r28-pins.json` | 2134 | `a0c4bba47d9a44a9c5a2df50c05ed0bf8ee0a157ad0aea2cfb1187bf08a1bed7` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `docs/project-consolidation/r28-size-breakdown.json` | 311 | `0d36d975ce8c11598805b81cd32902188431d8d566836f8b73b4c7c9a079f5b2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=(root files),_integration,web,vendor,tmp,source |
| `docs/project-consolidation/refine_dependencies.py` | 1200 | `ea86fa0486d9b529f0a62ea1aaa2126e6015b123ccc972e44b20e577195f9d51` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/result.json` | 3125 | `170083d04e329c49ad544e9219b01adde44f5216cc5c7539530507b41bb63a33` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,physical_consolidation_complete,whole_project_complete,canonical_root |
| `docs/project-consolidation/static-hashes.jsonl` | 2609897 | `b8f4d44ccd9d56839ff89c9981478db89d6ef86f00228de80f10239482a56109` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(13481);keys=root,path,bytes,sha256,stable_during_hash |
| `docs/project-consolidation/structure.md` | 2838 | `18119fe079dfb2008adb40921ab2a3dd816c91a3eec8dbd5c5c44a6a4a7671c5` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/project-consolidation/summarize_sizes.py` | 906 | `6890efb03a20c5348b88c2d0ce4674bec5728674ad4faede6742c811f63420a4` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/project-consolidation/thread-observations.json` | 3700 | `0324ce96049f87e82774d5ab55f97ec71107e883c26db37fbf8426e32cc94640` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=observedUtc,p24,otherActiveThread,processInventory,goal,noMessagesSent |
| `docs/project-consolidation/unique-static-files.jsonl` | 227598 | `8579e4bc3ad5a24f3644a5d6ba11d583494456c18c3596fdcf7dfcb8306b67af` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(1124);keys=root,path,bytes,sha256,stable_during_hash |
| `docs/project-consolidation/verify_report.py` | 4900 | `30264a1976681593d382b2df31bc5949d1e247635152a3a4dcc943b1b706a856` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/reports/issue-12.md` | 4992 | `5ff12f54968934c1b2b5daa3f315929b74b38043c5b936b6ce3a5b049973d6c6` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-13.md` | 14395 | `8d4758e95330b5aef4273d007eda463bf08c59a7ab98b67a2d995bfef87f2056` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-27.md` | 9279 | `ddbcb3205bf843c3832d3c8c33441ebb135b113d732f963df29b9c166f2109e0` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-3.md` | 3022 | `1ace0bd0bfbbdd719ab1174399de7c0a7f99f087136cf76cf3614e8ac13977b7` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-31.md` | 5118 | `8d6196bfa93b02ee271146a1aa0ed35b159c026e7988aa58aa1f234cdaac681e` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-34.md` | 7112 | `cecbe8d53586958d7eaf2395d2a8ca86bc43c7b7a892ee6bfd84df148d27d5dd` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-37.md` | 9494 | `f38d85e228a12c04602340cd87af635efa4c63014ce53b7c49d2bd33e4703db5` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-38.md` | 6662 | `6bc92cfe2314ca28174da64cc9bcbdfe151c36946f43759d10167a306640a4b1` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-41.md` | 4419 | `2b672dd8a9e42d51fe3d6a64bad7aa0f8bd7304f3e730b6441f09a04bb73fa0a` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-42.md` | 4444 | `612c74958802bdfb56a74e267c963b31c27e236b8ff46d27db219f2e9b73696f` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-47.md` | 18568 | `1a8bf8788b608ac8c95bb4d0624111577dd60b587dd6d55373353ff6038a6040` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-48.md` | 43309 | `712aa2f6eb5827424504f4599cdcb6f5491d81ad31b2158ceba6de987699f851` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-49-replay.py` | 20253 | `fe554f0724e2c7c2e0c3357b1e40753b3e5dd36e66266422a288ffd8a9a3b4b1` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `docs/reports/issue-49.md` | 7586 | `d665970f793f6e78ae363fc6ae543e4dfdd2b8df2329eaf1733d8ad2994dba03` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-50.md` | 8581 | `f10c913eecffc7071289fa9c65c9a5c021deb2f58109ae65e29c9d7458db15fb` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-51.md` | 13591 | `c83249839ddf21a176f51b655ceb51b2cdb45d3337c41ba6fe140effd76809f8` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-6.md` | 2802 | `68288653375c87567024e4b51a844c034b7b47a77ba7d78a95f5d82a464e027d` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-67.md` | 7828 | `d9566e0394f584e399f3ade59c17e2077eddb677cb1d3fb1e01893074083d7c0` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-7.md` | 11599 | `8a0ab9dbc7847abf73c1e5cab972c4cce4974a4d75a8e0529788311affe4a6d6` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-70.md` | 10123 | `82a11b471f6b605afe58c5563e1ea59bc720d65c1e6f7bb8788d6d5f6b0982a0` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-71.md` | 6310 | `628598d2e750addf07ecaf9821dce8460de01a3d4be3e9cd74eecd053d346d91` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-76.md` | 6529 | `762a9d1d843aa8b5629d80aaeba7a8d202c773b579761adf048bbd2b05a7e000` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-77.md` | 13682 | `953a11b1e90cabc7045c78040875bc4e4817fdc1f942ca9aeb3490621beba83f` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-81.md` | 3599 | `b4fd9b0e69a183124b4254df726e5233bff21c5bc504022251601778a160495e` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-88.md` | 19129 | `9cda9fdfc774036f688bbb1ab8f4232d32c32976ee0560a17a69c8ef1d3045e4` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-91.md` | 17736 | `604b70179f91db0ad48f89be81f53e6523cb03f27fc566034d074ef65082c062` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-94.md` | 9874 | `4898b480e21e92f06d5feffeab848f34a91327ebf2845e1f829c833fa97e1b7b` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `docs/reports/issue-97.md` | 6337 | `b921e5dd36a04686b39a28bb979845795921d64d636523ce17806a5ecbd2c153` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `engine/NuGet.Config` | 155 | `337ed22a5dd2b3a69d86fac14c3a86a05cf3ffc3d1d65e400850c747010b4f19` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/README.md` | 2936 | `e3c749b245c853f9bd0956ba37c5983a920dbeefdf85dca09af588dc8640838e` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `engine/_integration/HANDOFF.md` | 6040 | `b74ce3439cde7455c0f18537a8f87ca2862c899c6e9521e6c4d03213d25f1d9d` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `engine/_integration/audit_assets.py` | 948 | `4d89beeaa079365795693328c27fb724117f3798ad9889dc2cfd3697c259dd2a` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/audit_delivery.py` | 3229 | `9ccc94dfcbe5c375dc28358b977f5536aec313e4f6d07feee8dd936d279ac758` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/baseline-pins.json` | 2401714 | `1bcabc8986be9b50dd29bc268ee4f2466f56db9da1e6173591ff970c3d95bce1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sealPath,sealSha256,pins |
| `engine/_integration/candidate-seal-manifest.json` | 2444544 | `83770091f8051466d5dccb420210cd0567ffef5a1158e8f7ebf5c7667127bfcd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,pathsRelativeTo,excludedDirectories,excludedMutableRoots,excludedFiles |
| `engine/_integration/changed-files.json` | 7307 | `52e353caa1daf2d5b4eccbc0f3abd7d24a356cec00b30b11a2be2dbb9e8a3ce9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(34);keys=path,beforeSha256,afterSha256,size |
| `engine/_integration/changes.diff` | 119748 | `d9e278ab12bbd16a9d3368e1c209e7d5534b7f1e2e26925c5e03c23e00a9547f` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `engine/_integration/check-template/audio-tests/AudioClientExcerpt.cs` | 4168 | `ac1219a477b1ffe5162b025296ac6885161dd1ed7045751dd43beaaa6ca6a889` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/check-template/audio-tests/AudioSettingsChecks.csproj` | 366 | `6f29ce11160a8adf737180c2d7711a6d7e890e88dbc6d41230026ae6fddc0832` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/check-template/audio-tests/Program.cs` | 7794 | `2eee0e370f190d9d5bd5e76047432f7f53ac5d9b5d86e7d4906edc28fe75a070` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/check-template/audio-tests/web-checks.cjs` | 8492 | `99f773aac4559d83f266e21f7f3973b8626ac02aa6cc74f722177c0a332eb322` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/check-template/client-tests/ClientChecks.csproj` | 366 | `6f29ce11160a8adf737180c2d7711a6d7e890e88dbc6d41230026ae6fddc0832` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/check-template/client-tests/ClientExcerpt.cs` | 4448 | `9c2135995b8250ea23cd96b0303f64267b4308fe41e3134d79a5d7084409d170` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/check-template/client-tests/Program.cs` | 5425 | `5055ca5961a898c5d55acccaa5aa76e02a0cec3b0f0359dc19013fd41c47a182` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/check-template/fixtures/6016.json` | 69454 | `825673b11feb6b28c6d34663cd53266d0ca2209230b1b1cfa4c0a80125263383` | D — D08：6016 SPR 派生 frames.blocks.pixels 測試素材 |
| `engine/_integration/check-template/prepare_audio_checks.py` | 3240 | `e332f215f7b14409892d617edf6beb077c95c192b064e5999a211b949591c7d3` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/check-template/prepare_client_checks.py` | 3296 | `57a38bc6fe17bb60d153e1d10c6b6068dde552f0e85dea55e7161567963716a7` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/check-template/scope-update-r3.json` | 1983 | `91c8a6d5a70ebdcdfe7fa515646fa3639331328db5fb8132fce36a01b242faa4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sourceSupervisor,authorization,review,reviewSha256,manifestSha256,filesVerified |
| `engine/_integration/check-template/tests/HostChecks.csproj` | 366 | `6f29ce11160a8adf737180c2d7711a6d7e890e88dbc6d41230026ae6fddc0832` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/check-template/tests/Program.cs` | 16454 | `d64c19fcd25348663a37b0f8689caff5e977f1985b38a11af61459daa087736a` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/common.py` | 1932 | `7c4190a4a5726757c290742fdb77bc0320856248fc70da15a87afb53bbbd09a4` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/complete_build_receipt.py` | 2745 | `4cdbd13530dfe8308ce472749bebb80ffba7c32b1c3d93158804420f913e18b8` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/assets/audio-manifest.json` | 3346 | `87c6cd6867e0d81bdc627c1cac0a922891d650018211b58525de9226926ad9fe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateOnly,ids,loadOrder,fallbackBasis,assets |
| `engine/_integration/delta/assets/integrity.json` | 1729333 | `07dd19f4407ec3f2bf61da9c00cca827cbbe206866863a87a77f17fce7a18b98` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/delta/assets/sound/1340.wav` | 14322 | `30b4ce04d5ca914cdbfd80c7ee9b9cdbe3bf79487e609b2a9d32f6546a90f9c3` | D — D03：RIFF/WAVE 原音訊；delta assets 歷史整合 |
| `engine/_integration/delta/assets/sound/3332.wav` | 24748 | `973e0a3598edd23c2a24f6900643e43a9155fe36906b939d2d8e24374abeff2a` | D — D03：RIFF/WAVE 原音訊；delta assets 歷史整合 |
| `engine/_integration/delta/audio/AudioAssets.cs` | 3553 | `a35890d65e2f3cfc7922aafea59f500d333a445d6ce8aaeb50960b031e272bb9` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/audio/AudioCore.cs` | 10244 | `53d4aa73be42ee674daceb08751c7c494ebf3c257dd1178257a4caaf94e316c4` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/audio/AudioPlayer.cs` | 9556 | `bad9ab78a2d9a2eeb334d2e8313e6bdcf97406efca730c36676a06ebc33c3745` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/audio/DirectShowDriver.cs` | 9770 | `914c9e9b62a55d1e752539eb05990389e3e8b654e276b51c1edf9831683d2772` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/audio/DirectShowInterop.cs` | 3937 | `174492eaaeeee9dbaf8d29d1ee9e2caa8d3c9a947a4d2f16109dd882f62976ba` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/audio/P24.Audio.csproj` | 355 | `30cb249a292c0b9e4df6d592e834896d9cf621cbae9a9d3a31dd3fbbff3d65db` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/build-product.ps1` | 1702 | `275cde69cdb4ac9b5176f6215ea35d121af622812a618191c14983711036c55f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/launch-product.ps1` | 2756 | `430060f2ae4d258fc6cf8a230180a34ae8c28471296659b2bee4173e1c2b03b3` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/AudioApiTransport.cs` | 1616 | `b1a18d9fc09bfba275e9c17230c57cc398dba06ccd30631f44333a17b9e42c7b` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/ClientAudioSettings.cs` | 1723 | `7000c30e62f17cc58a1be9318cf53d8b793d8899d64016713114b5c709995bf7` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/ClientEffect6016.cs` | 4853 | `19be31bff33a4584e638d3314225648b1ccc72bb382dd4adf64e320e1aa08eeb` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/Effect6016Controller.cs` | 12999 | `5c24d747277a6428a9faf83b96682c33fb40f039989137c438c6555f5a1cae66` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/Effect6016Definition.cs` | 3300 | `c978e0ba6b83d6d9dd34729a29b2f5f04658ad2373cc3a1a40930abe63594ab0` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/Effect6016Host.cs` | 7903 | `7a8ac5da322014f1cee956903b5e21d65a50435107eff51b0ccfe82340ebf6ab` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/MapEffects.cs` | 4465 | `151017e2a085c66427959f9d6164f3cec08a455ea3866fd7e1f411f38261debd` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/MapResources.cs` | 1187 | `8e3e1663a58345ab42bee1fe2c07777a74ece63da7fd6500323efd5b8daa016b` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/OfflineClient.csproj` | 991 | `4486a4dea1367d8f7aac3f4860a4179bcd3e34f5a5c3f9edc93495a52c80bbed` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/OriginalMapCache.cs` | 20268 | `8c7009fd8483196beda9108a07a01f54b1e02d4447ba6cb3c6b88fb78b15d084` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/PlayerAudioSettings.cs` | 2704 | `d72460ffa9c1fe7a3959ea614f00355ba1dbf1facf21a9f584413c3c0d074c3f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/Program.cs` | 25045 | `f055f64ddc4d05b8ca75571690769839ea12fc3f15261057dda858b8eeffbee8` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/SceneOpacity.cs` | 10283 | `c6dc450181b1f19d3778738512b5a3dee5b95f7bdb27442241081f9acb0200b1` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/WebPanel.cs` | 19383 | `0eaa1f5ef53b3834070b7cde37351efae2c3d54cbe7cace30379d55728f9ca4e` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/WorldEffect6016.cs` | 1574 | `e631a0aea5c79e4f7d70eebe9bdaf4696a9d95c746199e0f0c72b04ce65dd8cb` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/WorldEntry.cs` | 7636 | `a49718e5ce6e820eab594cb1c3610347a666e151f254d48c2c4ca8b086671c1e` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/source/WorldSession.cs` | 8958 | `96d9795b437544b453ecea77ee70a395468794dc9a9a9b1ceeaca99a8ecaa326` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/web/index.html` | 8086 | `27795cafd5b135781629df09c083ad290e8ce4527f4a51d498e367d5d3204b38` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/web/panel.css` | 2933 | `66310ea6a1cdeda68bf1a817c24127830a0baacff0cbef88a7ed6dfd4f0b4a91` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/web/panel.js` | 15490 | `7cda1eedc10db448323ef9db3ba9c28cf7ffb7e2d38cc38487904f8d9bf5b4ab` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/delta/新版客戶端使用說明.md` | 7966 | `6b920b89d9c3db6c36198d906f9f9ba17269db45accb188a733a57c3f0f82dfb` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `engine/_integration/dev/combat-sfx-tests/CombatSfxChecks.csproj` | 565 | `31f2beb4478f49345c083eb61c23c28aa0383748c90619823c5abb22227f8e23` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/dev/combat-sfx-tests/Program.cs` | 20288 | `ef2b6eb05f5e0222c1df79f41ddf384efb25a6b3fff1c332f788737248809b86` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/dev/combat-sfx-tests/expected-events.json` | 16558 | `6a9b35c807273a284bbca2a14e648653678ddc6984d98532c2682d060efa58f7` | D — D06：原始動作 sound events 預期向量 |
| `engine/_integration/dev/ground-tests/GroundChecks.csproj` | 413 | `093f90508b0bfb221d2b33c9541190539bfb93c451c47c5f0125e63c518e5aab` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/dev/ground-tests/Program.cs` | 20766 | `f485cbf540f23fe519b2d079f2ae6ef3faf92de84bd05564f670597ece5fb36d` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/dev/ground-tests/legacy.py` | 2341 | `96c1157e00a74b88a36e4cf5e147edab653b29e28e5724e7fbda24a8b7111098` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/dev/ground-tests/verify-sources.py` | 2098 | `6e6fb7ece13e83bc99f2e39c7e45feaf7465990fd49382ad740a3957b6a69ae3` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/dev/ground-tests/web-checks.cjs` | 4077 | `c681af4ad18c2c0ff90dbaf942642618b332008e8392a1fc883801c57d274b90` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/dev/launcher-source/Launcher.csproj` | 654 | `469ed87525ba93f37c3d7c73224949bd3fed08ae8c74010ca23946d5bab15a9d` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/dev/launcher-source/Program.cs` | 8462 | `c804718a386f5d53e708695791b71704341496119fbc2ef45981286c7bc6de6d` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/dev/packet-trace-tests/PacketTraceChecks.csproj` | 346 | `5477c58912b7f5a955ac530758a0d299b74fa9a541c9e619816016156ef63ab9` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/dev/packet-trace-tests/Program.cs` | 8764 | `024b59614136c58da529c522bcf130a8264ed8a8cdfb516782c8651accd34bd0` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/dev/pe_resource.py` | 3519 | `2aacf5ba096011f00ec7c77ff01f87746c0b892b5752c3f41e6b39f1e9048f37` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/dev/product-build.ps1` | 641 | `b1aaaa1fbd88f7db41dc5ec8c385996c78cd9bfba63289715e500f25a7ee3b3c` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/dev/rebuild-lock.json` | 28757 | `5ff6bbd7003f64df051f272a88e83a38bc93b378d2230acd557bea1308bb4b70` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,basedOn,sourceCount,files |
| `engine/_integration/dev/rebuild.py` | 12497 | `12c8b929bada20beec65333eba1a3984f0e5f33da7842218d5958377395a238d` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/dev/test_rebuild_paths.py` | 7752 | `f208e0c81539b0d1d8bbd109d022782e7b51c5dd32ba12d1f77f62d730d2bae0` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/finalize.py` | 8909 | `285b6a9219900b17450e032be6d74199538813dd64d6b82c8372277ec83726ee` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/full-assets-verification.json` | 808 | `3ec42ce499be9e36686e79698f393830d61fe1dedb0940fb80f429237c004cbe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,hashedPins,totalFiles,totalPinnedBytes,integritySha256,baselinePins |
| `engine/_integration/goal-active-receipt.json` | 947 | `ed26c5596e5a0a452fde3d16e3340dd2dda25b4628980f4be16768cfa898bfb7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=goal,remainingTokens,completionBudgetReport |
| `engine/_integration/goal-completed-receipt.json` | 1269 | `66af97ba8e62b983e6ab6a01539b85bb7c537087773c90b98ec9ac4d4336a08b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=goal,remainingTokens,completionBudgetReport |
| `engine/_integration/historical-evidence.json` | 573 | `9db6e9e9421c1bdf330f9dbf66236cfae6d19cd67e9e46b03113457b7d8e7e83` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=notRerun,currentFinalR28,supervisorIndependentCandidateReview,nativeHostAccepted |
| `engine/_integration/integrity-update.json` | 688 | `832e2f886833a73f3c141ea9fa6773358c376b200d8bde0580e4cb4ce81d0909` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=oldPins,newPins,unchangedPrefix,added,audioManifestSha256,missingMp3StillAbsent |
| `engine/_integration/launcher-source/Launcher.csproj` | 412 | `59a5f22c8ced415eb3d9a36d5f356b52766956a3ae66597e29214a53280e62ee` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/launcher-source/Program.cs` | 8488 | `9d705168d7f071f1b8065b6dd7d475314a1188d967cd08c9c2ae19c52653a934` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/preservation.json` | 946 | `dc18a11a4c7a23e1e6e5edda54f73da4e65b5ae61f5fb1cf3b5f8f79c23ecee9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,exactInputDeltaFiles,trees,cumulativeCandidateDeltaPreserved,sameCmdBytesAsR27,baselineSealPreserved |
| `engine/_integration/r29/acceptance/changed-files.json` | 2038 | `d145a34c9a0353e7304ea85dfb08507f7bf27102f2df45513c2b76de32c2d93b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(9);keys=path,baseSha256,candidateSha256 |
| `engine/_integration/r29/acceptance/delivery-manifest.json` | 52859 | `7bed792d86ec1838ce97e0efa935d061420fa56cf6c0435dbaba0f0c919859b9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,task,status,files,excludedDirectories |
| `engine/_integration/r29/acceptance/final-slow-result.json` | 7654 | `f7aefa2f11e10e98c5cc7b94ecc95bba457597bce717c3953d929fecf3978a78` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,productDllSha256,reviewChecks,f1Checks,reproCases,sourceCopies |
| `engine/_integration/r29/acceptance/preparation-seal.json` | 1818 | `f987628e668cfadc77f92b0245f1df19e4fedfb68e71ef2624f4a51ccd2ba21b` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r29/acceptance/r29-input-contract.json` | 945 | `431645c82ff2a1a6d21d68f68a6256d66e383286f21607a1a05de2e08f3880b9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,candidateId,candidateRoot,preparationSha256,sourceManifest |
| `engine/_integration/r29/acceptance/r29-integration-acceptance.json` | 1391 | `ff60d3d1a86af64a8dcea6542f17a79370e002ac3f93477025684a89c245b361` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,revision,decision,candidateId,dispatchId |
| `engine/_integration/r29/acceptance/result.json` | 2811 | `fe8ec487ed8712e43e941f61453d0877d8730ff746ed415031f4cb625d4610b5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,task,status,checksPassed,checks |
| `engine/_integration/r29/acceptance/review/HANDOFF.md` | 4606 | `08304ef5e05f10c739ccde24d77d97cda1e18fa544980e94cfd593e7241c7ac8` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `engine/_integration/r29/acceptance/review/artifact-manifest.json` | 20917 | `713a0d931be997f0f9cfe698ceee806b5a728512f61b679017ef70ab5b20d29b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=artifacts,excluded |
| `engine/_integration/r29/acceptance/review/result.json` | 2596 | `97080e4a6e885b0f3f3b99b9650f59aa2f4701073b8ab1e957403cf267eeb3f6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,candidateId,candidateReviewed,candidateAccepted,decision |
| `engine/_integration/r29/acceptance/review/review-acceptance.json` | 1611 | `a3c09dbdaf9bd4061deeb3a4deb10ab5fabc8975f71aabf72c071888e39fc0d6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,decision,candidateSealSha256,candidateDllSha256,candidateReviewed |
| `engine/_integration/r29/acceptance/source-manifest.json` | 40672 | `8938b63e49c7aff78f1a8baf7b1e7b3765de2bc4da7e16de8408b11db86bda5b` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r29/acceptance/stage-151-result.json` | 9552 | `575c1f709cf2e73dbeb6f1a61a719c7e557ffc975f2fad2b854f7c579ac2ae24` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,productDllSha256,audioDllSha256,productManifestSha256,launcherDllSha256,assetCount |
| `engine/_integration/r29/history-r28/README.md` | 1414 | `9074bfef7d6c564e3e3fbf8cabaf00c2d5498bf1ce1505a31bbe7c4d8462717c` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `engine/_integration/r29/history-r28/build-product.ps1` | 1702 | `275cde69cdb4ac9b5176f6215ea35d121af622812a618191c14983711036c55f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r29/history-r28/common.py` | 1932 | `7c4190a4a5726757c290742fdb77bc0320856248fc70da15a87afb53bbbd09a4` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r29/history-r28/launch-product.ps1` | 2756 | `430060f2ae4d258fc6cf8a230180a34ae8c28471296659b2bee4173e1c2b03b3` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r29/history-r28/run_stage.py` | 6730 | `0075560118430506e53ea674839f56398f495bb715d97e9046acc14e44e10720` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r29/history-r28/新版客戶端使用說明.md` | 7966 | `6b920b89d9c3db6c36198d906f9f9ba17269db45accb188a733a57c3f0f82dfb` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `engine/_integration/r29/launcher-source/Launcher.csproj` | 412 | `59a5f22c8ced415eb3d9a36d5f356b52766956a3ae66597e29214a53280e62ee` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r29/launcher-source/Program.cs` | 8488 | `7817846c1f5081348e6305313d8e5f99d850cb920e8c7409418408b988c3d535` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r29/product-build.ps1` | 2119 | `f919519bb6af71af7cf0b79074176be6b2fa85044e55fb34ca2788f571acb12b` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r29/rebuild-lock.json` | 34575 | `f85353f74e4198d9b4291525980ab440c12ea44bd66398f2e00f0348c4373093` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r29/rebuild.py` | 8409 | `d1b139798ec74282ffeb626b0548aaa1737a337ef79ce6395f676d0d6b1ba333` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r29/run_stage_r29.py` | 7043 | `78c0767aff8cb3b27e028fa8d1e1cbb8594494961b423c492f3016b2bad8ff9f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r30/acceptance/accepted-stage-result.json` | 3117 | `d7556282499416e583c5d5ac86e4ba75ee14eea5331460133462bbd9d9317bb8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,candidateId,productDllSha256,audioDllSha256 |
| `engine/_integration/r30/acceptance/accepted-stage-seal.json` | 2451238 | `ce72859e492ca092cbf3de8854648aa7c171d625a22419e40545b84575180237` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r30/acceptance/baseline-151-result.json` | 9646 | `a93fb177e5af7154c1db856896032712dab99f230f85c1b54949dddd6692eb57` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,productDllSha256,audioDllSha256,productManifestSha256,launcherDllSha256,assetCount |
| `engine/_integration/r30/acceptance/candidate-result.json` | 2662 | `088d79724a14be605aadcff8b67d27da37e03fdc04001b533474d17670717104` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,task,status,checksPassed,checks |
| `engine/_integration/r30/acceptance/candidate-seal.json` | 48212 | `6f952b532750778320ef1b238051b4bc37bcbf92fe7d3e2391d207be11cea66f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,task,status,files,excludedDirectories |
| `engine/_integration/r30/acceptance/independent-review.json` | 2258 | `2d150a8c224956ecf85c08f19eb0c952977877cc1d1f2af7fdf11a8acf55fa91` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,decision,candidateSealSha256,candidateDllSha256,candidateReviewed |
| `engine/_integration/r30/acceptance/overlay-manifest.json` | 2793 | `1067234500b5b86bcee5dfa766163133c50754f5c639238f691b6450080b3993` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,actionsSuitePassed,mergeAuthorizedByThisArtifact,installationRequiresNewSupervisorDispatch,sourceCount,candidateId |
| `engine/_integration/r30/acceptance/r30-input-contract.json` | 1204 | `bfd2c3a850654e579db05d7bb849478e693396d2fb5b775beda0936d9a1faf1f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,candidateId,candidateRoot,preparationSha256,baseRevision |
| `engine/_integration/r30/acceptance/r30-integration-acceptance.json` | 1939 | `b90bc049aebb14c5ba1cf7d228b902227f1da2589d394617b112b8e2bab1ac50` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,revision,decision,candidateId,dispatchId |
| `engine/_integration/r30/acceptance/r30-stage-acceptance.json` | 1528 | `20ffb3169bbd724b9cbb3393f8764bebdc73823f8ce55e511e48325b937eed79` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,decision,stageRoot,stageSealSha256 |
| `engine/_integration/r30/acceptance/source-manifest.json` | 43022 | `bfdf2b3d50822f9cd31aa84d26d1cba670386f1e26c749166bf5f465e9c197b2` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r30/launcher-source/Launcher.csproj` | 412 | `59a5f22c8ced415eb3d9a36d5f356b52766956a3ae66597e29214a53280e62ee` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r30/launcher-source/Program.cs` | 8488 | `53d9dfcff49951f784e27b1c528b9e36f699ec870b5fc42bcda794e1a44d8a46` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r30/product-build.ps1` | 2088 | `fa230b708b1f594c29549b376dc658109a6ed358f22fa657858f0c39a8ee33a8` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r30/rebuild-lock.json` | 32204 | `f340bb565470475f827ba61d61424712277cc4558eab2ade9a78d213523ac26a` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r30/rebuild.py` | 8449 | `5ae10cda9034c61b4b071a448dea6f8edb4ebb6708e86b5a1ddfb81657ffc532` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r31/acceptance/accepted-stage-result.json` | 1520 | `fab760248a083a67776e0ee469c26dcaebcf0918c9bf1490e99ded9ca781f2ae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,candidateId,productDllSha256,audioDllSha256 |
| `engine/_integration/r31/acceptance/accepted-stage-seal.json` | 2456879 | `5b014aa0f9c91185b8ca4d00cba2ba815dd1d45e193a77e753153639145835bb` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r31/acceptance/baseline-151-result.json` | 9892 | `f98b97724744746f9e54009174ef3f0ea0982fe664206865fde7c6b8b85cb042` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,productDllSha256,audioDllSha256,productManifestSha256,launcherDllSha256,assetCount |
| `engine/_integration/r31/acceptance/candidate-result.json` | 2418 | `18725fb5f2d679a70b4d65b883385aa58206e91dbf0ef7c9487b76582edd88e9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,task,status,checksPassed |
| `engine/_integration/r31/acceptance/candidate-seal.json` | 45964 | `ecf4a8642d13238222fad3173890dc1063bcfc311fc7aabe101d6b07465831b4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,status,files,excludedDirectories |
| `engine/_integration/r31/acceptance/independent-review.json` | 2707 | `bd5a64b10f67f1d9053026892a0e3a258c8e15c3be8cefedaaaf0479c2a69f18` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,decision,candidateSealSha256,candidateDllSha256,candidateReviewed |
| `engine/_integration/r31/acceptance/overlay-manifest.json` | 2792 | `c4b49ca07899a7e4dc5773dcda7dbbdc198ed0e65b29cf62fedf6923699cda2b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,hasteSuitePassed,mergeAuthorizedByThisArtifact,installationRequiresNewSupervisorDispatch,sourceCount,candidateId |
| `engine/_integration/r31/acceptance/r31-input-contract.json` | 1210 | `8fc2729c8149c5edca1ff1d5b69e1473977f3d6c503c1e8d86afe06bdb385ab3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,candidateId,candidateRoot,preparationSha256,baseRevision |
| `engine/_integration/r31/acceptance/r31-integration-acceptance.json` | 1964 | `dc76ef63ed547cd11f0b0c42b5e582b28a11f538cf26582253537b6271095602` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,revision,decision,candidateId,dispatchId |
| `engine/_integration/r31/acceptance/r31-stage-acceptance.json` | 1518 | `409493366d14e02d7138e18ccf529867034aa67337819b305ebaa99a27c7f166` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,decision,stageRoot,stageSealSha256 |
| `engine/_integration/r31/acceptance/source-manifest.json` | 43222 | `b7d663225973833c6576b9400c32f9afd7161c4517d02f51877796630d08f98e` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r31/launcher-source/Launcher.csproj` | 412 | `59a5f22c8ced415eb3d9a36d5f356b52766956a3ae66597e29214a53280e62ee` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r31/launcher-source/Program.cs` | 8488 | `74f24243582649738546ab28747623e20ab6debad36f57138e3c244aedc4e3ad` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r31/product-build.ps1` | 2088 | `75e89622dec6f19f19df6ff71857745cb5ef65a7b095f059da1a526d600d32fc` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r31/rebuild-lock.json` | 32205 | `3853ccc0628a8eb435b577b6d9f4d584763d8ab40082a41268a5810154dbc59e` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r31/rebuild.py` | 8450 | `c205bb716d19ac57a83293307a0973a3a404850b0dd22b223ead3e835e2fa56f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r32/acceptance/accepted-stage-result.json` | 1840 | `aa093d09a3428d0adb53cdd8ffaf61e7cf6652205756c7bc67a33180f1934ca6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,candidateId,productDllSha256,audioDllSha256 |
| `engine/_integration/r32/acceptance/accepted-stage-seal.json` | 2461595 | `121fd44082ab5f55c9d7c91ca78256276e1303e2734459210bc6c7e672d73734` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r32/acceptance/baseline-151-result.json` | 9974 | `dbf9f4343932d7f736266c7f681a039230ba4475528d5dfd88705c07a865a5fc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,productDllSha256,audioDllSha256,productManifestSha256,launcherDllSha256,assetCount |
| `engine/_integration/r32/acceptance/candidate-result.json` | 2520 | `6793b846a7a2cb0ce282014758a4defa13c544c43ea81afb52c8e3537a7c37e0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,task,status,checksPassed |
| `engine/_integration/r32/acceptance/candidate-seal.json` | 41097 | `0976aa3ffe829a1b4f77c3b94241f113ed77ca0dab245b6c6e1dca1ac18f3369` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,status,files,excludedDirectories |
| `engine/_integration/r32/acceptance/independent-review.json` | 1545 | `65ca9373b31bac4e279c143c34b14208271e846af96f4cf2fa65133fae0a2d95` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,candidateId,candidateSealSha256,candidateSourceManifestSha256,candidateResultSha256 |
| `engine/_integration/r32/acceptance/overlay-manifest.json` | 2798 | `45eb51d640cd591393c1b06a4cd933baa98b8361baa08e234074d61815638460` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,hasteItemSuitePassed,mergeAuthorizedByThisArtifact,installationRequiresNewSupervisorDispatch,sourceCount,candidateId |
| `engine/_integration/r32/acceptance/r32-input-contract.json` | 1219 | `3977c56367ae9383868ecf142414733343d6c6df3821d3384455ad159ed171e2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,candidateId,candidateRoot,preparationSha256,baseRevision |
| `engine/_integration/r32/acceptance/r32-integration-acceptance.json` | 2022 | `ac7863c0f078626188d390575b2b9815280909437a2141990d53b53cf8e0279d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,revision,decision,candidateId,dispatchId |
| `engine/_integration/r32/acceptance/r32-stage-acceptance.json` | 2397 | `2898e684ca8756fba7c0e38973461fcb905f48a43932cf6fafea6403b64c5eb5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,stageRoot,stageSealSha256,stageResultSha256 |
| `engine/_integration/r32/acceptance/source-manifest.json` | 43578 | `2713dd94bea243bfd71576878bbbb3649938703cdd04fb81ab519e9dcb851518` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r32/launcher-source/Launcher.csproj` | 412 | `59a5f22c8ced415eb3d9a36d5f356b52766956a3ae66597e29214a53280e62ee` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r32/launcher-source/Program.cs` | 8488 | `081cc60f0cc9a60e501d3213480f0b09fb6c6c7e3675e55f52b152fcd384166e` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r32/product-build.ps1` | 2088 | `d2375050a8e4dd7218ef585b9c3f157d074621b1bce541a5d419182cc6a95095` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r32/rebuild-lock.json` | 32207 | `a677ca9501aa5df28d045bf50b55ca606b44117a55a90ced8c5ff6bff2afeac7` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r32/rebuild.py` | 8452 | `c2d5c0ef7154d672f0aef08f6bb7ae7491cd46821b4a1f3ce45621a537009dc5` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r33/acceptance/accepted-stage-result.json` | 2638 | `fd4155f97e637a2ffb041f1ccbd2236cec1851f6159637d5148f68a98365d608` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,candidateId,productDllSha256,audioDllSha256 |
| `engine/_integration/r33/acceptance/accepted-stage-seal.json` | 2475348 | `6682cd2eedb7b1a7a8e6b5824d3f92e0408b57949a9e78dbeae471a056acd9e7` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r33/acceptance/baseline-151-result.json` | 10015 | `272acfecd8674028e57f2dc980a77eff83891ee27d160996911f362058289e83` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,productDllSha256,audioDllSha256,productManifestSha256,launcherDllSha256,assetCount |
| `engine/_integration/r33/acceptance/candidate-result.json` | 2822 | `359cb5a3111e0dc95e91000024d780098f842cff711c924e7f551212824cb207` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,task,status,checksPassed |
| `engine/_integration/r33/acceptance/candidate-seal.json` | 59650 | `2ad5c438372732384dbb505936a94ce386d25933223194c39c2997de524073cf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,status,files,excludedDirectories |
| `engine/_integration/r33/acceptance/independent-review.json` | 7178 | `fd699ef75df6b3116c52e14511756ba331bc16c7b43acd2d92391ad1e8065649` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,candidateReviewed,decision,candidate,finalBaseline |
| `engine/_integration/r33/acceptance/overlay-manifest.json` | 2806 | `e98f9a53856d32100e61658a84074a9becbbe3895e8b8ad706e9b170c3fc00f0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,groundLifecycleSuitePassed,mergeAuthorizedByThisArtifact,installationRequiresNewSupervisorDispatch,sourceCount,candida |
| `engine/_integration/r33/acceptance/r33-input-contract.json` | 1251 | `048770f99ca923ab05227dbd8fc3049f1aeddf70260f3423c553aa0427744c50` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,candidateId,candidateRoot,preparationSha256,baseRevision |
| `engine/_integration/r33/acceptance/r33-integration-acceptance.json` | 2307 | `3676137771224079b0a3a26abf094165c34e2b8b68e203da8089f2f877fc4a28` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,revision,decision,candidateId,dispatchId |
| `engine/_integration/r33/acceptance/r33-stage-acceptance.json` | 2478 | `613757a12150148df5b22b88faeef4ab8a1fc5e837720908aa799e3d52483671` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,stageRoot,stageSealSha256,stageResultSha256 |
| `engine/_integration/r33/acceptance/source-manifest.json` | 43756 | `adf30cb60180285541857fac85b3e522d68855a1a63d0117dd4fb71cb1de0056` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r33/launcher-source/Launcher.csproj` | 412 | `59a5f22c8ced415eb3d9a36d5f356b52766956a3ae66597e29214a53280e62ee` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r33/launcher-source/Program.cs` | 8488 | `105a4c5ab320b36222670b913d81eb131bbf6d382525c3b7bd2252c74a2da917` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r33/product-build.ps1` | 2088 | `9b69f5b3a20c8b260e331ca2b3c35290de534d8cdeda1a27acd3579552de6f51` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r33/rebuild-lock.json` | 32209 | `15cb88653a2b7aaf1bb7015e7db1231f8224b98ef4a4a75306ccf50694bb33eb` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r33/rebuild.py` | 8685 | `670124209cf02a11c2e4be2ce61abf18c4bc7f3f994c2c7c9c998970f079d48a` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r34/acceptance/baseline-151-result.json` | 11850 | `0000917527bb53091d61b157b0e68407a99cdd72e4c4b19d1184507efc943244` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,productDllSha256,audioDllSha256,productManifestSha256,launcherDllSha256,assetCount |
| `engine/_integration/r34/acceptance/candidate-result.json` | 1960 | `05983d2858d22a9b8420fdcbe17a03415445ff9315d131be740297e1c1cc5c88` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,status,policyId,checksPassed |
| `engine/_integration/r34/acceptance/candidate-seal.json` | 61912 | `cc7761665593c79b9f82255f5d361bedd6c8ac4563434e7cf7a6e07c5c16cbdc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,status,files,excludedDirectories |
| `engine/_integration/r34/acceptance/full-stage-validation.json` | 402 | `c9ddf09ccdd9ca8a455f2c684841a596d67374bc78d51c5f759806de71789c91` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,productDllSha256,baselineAssertions,baselineObservations,pickupAssertions,pickupObservations |
| `engine/_integration/r34/acceptance/independent-review-seal.json` | 72114 | `715c95654af8a1c59853f15e41c3511d5b15c1b0749bf35c47e39fe918b99196` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,reviewId,decision,files,excludedMutableNamespaces,selfExcluded |
| `engine/_integration/r34/acceptance/independent-review.json` | 1981 | `5ba17f25a978b146a282f14fce8f477a60307303451ded1b9bca916b56411d4d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,reviewId,candidateId,status,decision,candidateSealSha256 |
| `engine/_integration/r34/acceptance/normalized-resource.json` | 7439 | `0f4528609dd00c3836a9bd7ca1c9b9e2f4a8dd6cf6ed4c69c79110755c4bc6c7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,files,derived,original,proof |
| `engine/_integration/r34/acceptance/normalized-review.json` | 1905 | `9df5838a588a54d2b8e849473ea5c303cc4faaefe77be39b67c52857e7075b91` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,decision,counts,unresolvedFindings,derived |
| `engine/_integration/r34/acceptance/r34-input-contract.json` | 2391 | `37ff68bd2ce504a7dc1f8df8b80a9a9b70068c96d159fc5b1e5df75246c2efd1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,candidateId,candidateRoot,preparationSha256,implementationContractSha256 |
| `engine/_integration/r34/acceptance/r34-integration-acceptance.json` | 1943 | `ed80a7daa26d6b6aa3f00f12fadfb655d947ad33f76f5969821aaaa1a6cfa237` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,revision,dispatchId,decision,authorization |
| `engine/_integration/r34/acceptance/resource-material.json` | 88033 | `d71b951e7117bfd05847694266f28a5b209ff54d722cd1ed8dff462efceb6064` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=resource,bytes,sha256,recipe,inputPins,identityCounts |
| `engine/_integration/r34/acceptance/source-manifest.json` | 45249 | `337afaa3607e49e0c50f2f388ab8cec8c42f7816e33ed2ee98df6bfb0a48b533` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,base,baseSourceFiles,addedSourceFiles |
| `engine/_integration/r34/launcher-source/Launcher.csproj` | 412 | `59a5f22c8ced415eb3d9a36d5f356b52766956a3ae66597e29214a53280e62ee` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r34/launcher-source/Program.cs` | 8488 | `e93ade09f9d773dcec99c5637d162a5ab31ce6445363c76724cc4186857eeaef` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r34/pe_resource.py` | 3519 | `2aacf5ba096011f00ec7c77ff01f87746c0b892b5752c3f41e6b39f1e9048f37` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r34/product-build.ps1` | 2137 | `2a40579ebc4ff2b0b31d4e0c21c1eb6e8e4d44ddf1fff271558886348ff76319` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r34/rebuild-lock.json` | 33670 | `edc31fcb12204ec4fcabd6e5311e340dd13456eb4cc228d5506dab22b7313aec` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/_integration/r34/rebuild.py` | 9557 | `c215fa684a04a1f2fb924b5b28cbf5c259837a11f8170e28e4e7392a42691927` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r35/launcher-source/Launcher.csproj` | 412 | `59a5f22c8ced415eb3d9a36d5f356b52766956a3ae66597e29214a53280e62ee` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r35/launcher-source/Program.cs` | 8462 | `d7cd532a1af751be5d6f4a8b915fe46e850f33d76c2a4936dea60ee89b424db8` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r35/pe_resource.py` | 3519 | `2aacf5ba096011f00ec7c77ff01f87746c0b892b5752c3f41e6b39f1e9048f37` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r35/product-build.ps1` | 641 | `68dfb81629e716651799803593631eae5c888dd28e556ea424583986be3d13b2` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r35/rebuild-lock.json` | 27475 | `36b3e6991a69d5b06b8978022af61a8593dc5da41de84ecc50b52a1249e6794d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,basedOn,sourceCount,files |
| `engine/_integration/r35/rebuild.py` | 8890 | `24417d03d3ba264c9e4ef1624e4cfe7ddef223b17485645e7f97a454a8cb560d` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/r35/test_rebuild_paths.py` | 3151 | `776d023d01d12df2dfc887e88f21f87aaec16d64cf63f8928bcf53c86a068f0f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/references/audio-r3-review/root-review.json` | 2220 | `f8da9871fa6a2a744b3afbb40ae7f6e74e40a0ae4d53602ee9273fb184758c0f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,supervisor,candidateResultSha256,candidateManifestSha256,artifactPins |
| `engine/_integration/references/controller-review/root-review.json` | 1074 | `8eef6a2fead6bd0356cc83c69694430dd4c59f4266c706f9818b9df6d13dcfd1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,supervisor,sourceReviewed,candidateResultSha256,artifactPins,independentRebuild |
| `engine/_integration/references/host-r3-review/independent-dom-result-after.json` | 2490 | `982d4ffca8d7dcb9b2db1b4cb83305ba89d3ab432566c21b5f77d64aac0c82b5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,cases,assertions,details,race,rapid |
| `engine/_integration/references/host-r3-review/independent-managed-result.json` | 1292 | `50ade85b97f43e2c22a24c608e39f43bdb37b3e474bb9e0509cfeb1bcdf27a22` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,cases,assertions,details,scope |
| `engine/_integration/references/host-r3-review/independent-seal-verification-final.json` | 1579 | `8f3e7ad5de36d97f36d059a51bf739021430743618768d1d9aba70a1b24fc246` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,candidateRoot,manifestSha256,pinsVerified,sourceWebPinsVerified |
| `engine/_integration/references/host-r3-review/independent-static-audit.json` | 12102 | `f488002f64fa5a5fe2907a8358945d4df2ea0a71463495447a04f873d10a79a1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,anchors,preserved,scope,acknowledgementAnchors |
| `engine/_integration/references/host-r3-review/rebuild-receipt.json` | 793 | `07bfe2ec7b5684e009d3473e09fa58f2427927ebed21036b26e756f9bd9e973c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,candidate,manifestSha256,pins,copiedVerifiedFiles,rootBuildScriptChange |
| `engine/_integration/references/host-r3-review/root-review.json` | 1342 | `160887d13637cea4844324a1b66934d60523dd94d32ee8cc11768414ac12eb00` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,decision,candidate,sealSha256,productDllSha256,audioDllSha256 |
| `engine/_integration/references/r1/HANDOFF.md` | 7075 | `ca8814f23dffee4d4858733dda5022fd29ef6805f3f87155584768e74fe77e51` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `engine/_integration/references/r1/candidate-manifest.json` | 63610 | `bd61e78f1f4bca51e92597c1ee24020aedf3564193e4262bab466cea90234548` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,pathsRelativeTo,excludedDirectories,excludedFiles,files |
| `engine/_integration/references/r1/delta-manifest.json` | 4988 | `5bc7daf0c6d385631709a6a0343111b9c6a705ede8ecbaf09f728c4031cc75d6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(24);keys=path,beforeSha256,afterSha256,size |
| `engine/_integration/references/r1/result.json` | 1529 | `269368e39043c5c214a60583f6884c9eaf6863c0d32caee9b27eba715a897e3a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,writingStoppedAfterSeal,formalR28Published,wholeP24Complete,productDllSha256,audioDllSha256 |
| `engine/_integration/references/r2/HANDOFF.md` | 5493 | `06962701769aa7b457d3ef976c2b2df6cd882aafa8c5f9dafa9dec279fbd2ae4` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `engine/_integration/references/r2/candidate-manifest.json` | 53529 | `51d3e41f226b1b4507605f7b273d687888b496dd03428747275c5d9898ad46ef` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,pathsRelativeTo,excludedDirectories,excludedFiles,files |
| `engine/_integration/references/r2/delta-manifest.json` | 1832 | `1cd850fc9b38c70cba1dc2e808aac876461b48535714db4cbebd0b43a773f9c0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(8);keys=path,beforeSha256,afterSha256,size |
| `engine/_integration/references/r2/result.json` | 1048 | `a9569ba4acd39a4266816a659f0db40aebdefca014059041848fbe9b9f0d7389` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,baselineR1Dll,productDllSha256,audioDllSha256,deltaFiles,existingHostCases |
| `engine/_integration/references/r3/HANDOFF.md` | 4105 | `9c4bdc3e37fe2032680a2141804c14fbdbc4c6502954f31991fd1df70cd8a2e3` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `engine/_integration/references/r3/candidate-manifest.json` | 51133 | `f322c11e45d9b0fd64048c7fb0d464aa7fc76c1fc0c3b334efac6122b0928915` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,pathsRelativeTo,excludedDirectories,excludedFiles,files |
| `engine/_integration/references/r3/delta-manifest.json` | 735 | `b4eb1912ddfc328007402517f1b0ad21f4d26b152614161887daa5ad12b6e3df` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(3);keys=path,beforeSha256,afterSha256,size |
| `engine/_integration/references/r3/result.json` | 852 | `4254eb76ff13f70ff10cbf9e419cb5e0033ede1de4b3b41ed0fc377f0bae24e2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,baselineSeal,baselinePanelSha256,productDllSha256,audioDllSha256,deltaFiles |
| `engine/_integration/result.json` | 1003 | `98c1e436ad0598ac8c3b65fd5f3360704a3ce8d2154be162107deeb30aa1cdf3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,revision,productDllSha256,audioDllSha256,productManifestSha256,launcherDllSha256 |
| `engine/_integration/run_stage.py` | 6730 | `0075560118430506e53ea674839f56398f495bb715d97e9046acc14e44e10720` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/seal.py` | 2414 | `ea8786c4894f8c0a59c30a62c67a0e41d10caed4054d3e9dbd49ac56a8ca8a5e` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/setup.py` | 6073 | `df3308ca07bb72a854f57283e144822bf4e66df177ce47801266c2aa89672c7f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/_integration/source-lineage.json` | 10971 | `11594f16eb1069591d004193da4fcbd95e97795e3e6acd924b51127357fad9c5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,baselineSealSha256,accepted,candidates,cumulativeInputDelta,acceptanceReferences |
| `engine/_integration/version-pins.json` | 603 | `142a0b52f40b9583419e62b3f83dded5eb181ea12a9dd87aeb68e1a9b75925fb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=revision,productDllSha256,audioDllSha256,productManifestSha256,launcherDllSha256,entry |
| `engine/action-metadata-vectors.json` | 87305 | `6afd114d3c53be54dfc2a0116cb71358c45a0df51ac64546e13d2de64f4afc9e` | D — D06：動作 metadata 時序／序列測試向量 |
| `engine/audio/AudioAssets.cs` | 4897 | `e695a37f7d3bcc1f90f6da6dfac9af4875361980fea3265326546cffb8a03095` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/audio/AudioCore.cs` | 10318 | `c3d5281c252e064321162c8c2676dd6f8ae8b3c6508d6a5d02d9c28d41dd02ed` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/audio/AudioPlayer.cs` | 9877 | `b1d87eea120503076a369a2230d7c66a1502dfc58dddfd47dffd892f3e3f3000` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/audio/DirectShowDriver.cs` | 9770 | `914c9e9b62a55d1e752539eb05990389e3e8b654e276b51c1edf9831683d2772` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/audio/DirectShowInterop.cs` | 3937 | `174492eaaeeee9dbaf8d29d1ee9e2caa8d3c9a947a4d2f16109dd882f62976ba` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/audio/IAudioPlayback.cs` | 465 | `2f55533ba8859ef26b86463f759d29c8cbafa7ba0e5e1e417f13212405f4030b` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/audio/P24.Audio.csproj` | 788 | `9243964ff1e313750139de023f82ce512c4c8193db0bb56435a2eba6679102c1` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/audio/Resources/combat-sfx-pins.json` | 9441 | `78d35293bc8b5a8ad1a74452c485b12fc17f738e1fc759c79ffd027946dee0e8` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/baseline-pins.json` | 61289 | `07e6c5a6649eef932e8ff4205d382c5463e0548c9f54633e561e61e965952352` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=baseline,sourceFilesVerified,sourceStageSealVerified,productDllVerified,audioDllVerified,fullStageInventoryRehashed |
| `engine/build-product.ps1` | 146 | `1cb83b90511052c8edf26174c178be4fa35b76df919f877a149ae59a4ebcc1fd` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/haste-metadata-vectors.json` | 155958 | `4e6cb81ecfe9197fbf2d8b4d4496ec258355ee723367549a701de79f4320939a` | D — D06：動作 metadata 時序／序列測試向量 |
| `engine/launch-product.ps1` | 2781 | `9f93a1d39c4a97358a75be125751966779d08554fc395bea36fb642e286aacc1` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/launcher/P24.Launcher.deps.json` | 430 | `1cc0f476da7833ffe9258f8ac190eb56d167d517c1004acc98c2e054f168e90a` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/launcher/P24.Launcher.runtimeconfig.json` | 399 | `3e965cd7cfd553c2ff4e842d8d8019aaae2da0a21d92ff85a87573dd8d1b95c6` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `engine/source/ActionCadence.cs` | 7854 | `f533f652619d26639ca2725cb5c3b5c07f9a13bbfe9fcdc8d8164d7d0e9486a5` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Assets.cs` | 7448 | `7338b577e307dbf11a7d4699a11eeb0ce472186b77c82fdf0807bef4ef729b6a` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/AudioApiTransport.cs` | 1616 | `b1a18d9fc09bfba275e9c17230c57cc398dba06ccd30631f44333a17b9e42c7b` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/BasicSupplies.cs` | 3547 | `5311b9c03f0624f5425946f4f4615ebf1456141c82b73a5096e1d85794f2a201` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/BonusStats.cs` | 8791 | `b1dc4950bc1bd30218ab776b38bd68a6656ed68ae0eeef7f5817f1a3fcc8ad43` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/BonusStatsUi.cs` | 4738 | `072a214367754da457a0aee4ebcc457d441955cdf17caf30df31cbb1e91649c9` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/CacheFrameEvidence.cs` | 2669 | `313ec8d28c59bebd97d05d734e082e7e5b04398e673b8d89e7f64973014dc5ea` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/CacheTests.cs` | 11154 | `4584ba1a014e5378870bf1520427c10ff7ace1947ce350dc813ba5d772e5d521` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ClientAudioSettings.cs` | 1739 | `57875a68aaabbd6c50b6087dffc76329d44003b0d35a5c68db48b09804e161b0` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ClientCombatSfx.cs` | 2626 | `dfd1a65c9bc579d6ab7759e078466d8f28ce630966992c26af834b00bf3fc1ff` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ClientEffect6016.cs` | 4853 | `19be31bff33a4584e638d3314225648b1ccc72bb382dd4adf64e320e1aa08eeb` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ClientLogin.cs` | 11588 | `a00779bb20bc2bb97aac8fb520788f0f8cd674bfeb7f4ce35279b3713e4003d7` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ClientNetwork.cs` | 8638 | `a0fb986760c8a2b215cd03d044e4aa3f303cc8c53d10acf75e283b5b7369e88c` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Combat.cs` | 12344 | `1d30a374608f16163171c8a489393e80696b8d6632e20b9fe029044e30f218e2` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/CombatRegression.cs` | 8083 | `70e8f14db6bd3adfc6f92701e7a397a26ef38bc5985a9d84a8eb5e55e39cb554` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/CombatSfx.cs` | 8329 | `04bb9e986f9ca48ca345faad098b02581184086e45ce9771a8bde60c538553d4` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/CombatSfxScene.cs` | 5965 | `8aa03513a57fdf7a65c10e8117c34a275f929551a22849fa6cf032801767893a` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/CombatTests.cs` | 9297 | `22bc5eb8fef2b060f679c1ea98d1c1de4977729c432592744cbb836f432b39b3` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/CombatUiTests.cs` | 11379 | `60358fa7fc128420d9557f5acac448f54aa873176f8798992e9837466a76a97c` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/CombatVisibilityPlan.cs` | 2604 | `4cb71ff1ef122dec7974554d443f0fae236bef48674979ffd5c0428610e696c1` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/DualBlockTests.cs` | 8637 | `4d6990df06e302353b7eba857ba937b1b6f86ae570cad0f9c8e32601ce9c7546` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/DuplicateRecoveryProbe.cs` | 1765 | `bd9fac40979690f34622fe5112cc4359f52e185b205c8279d78a86bc1880c2c7` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Effect6016Controller.cs` | 12999 | `5c24d747277a6428a9faf83b96682c33fb40f039989137c438c6555f5a1cae66` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Effect6016Definition.cs` | 3300 | `c978e0ba6b83d6d9dd34729a29b2f5f04658ad2373cc3a1a40930abe63594ab0` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Effect6016Host.cs` | 7903 | `7a8ac5da322014f1cee956903b5e21d65a50435107eff51b0ccfe82340ebf6ab` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Equipment.cs` | 5373 | `54886622111435489a1bc1c36bcfafefa91278dd7b9ba3e2a352563cd8ab3c65` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/EquipmentGateTests.cs` | 3436 | `f0779f792543a6cdf19dd9780bda12144ceb3e180f7e3e07d1cada1a1199ce8a` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/EquipmentIntegration.cs` | 6065 | `45cc77321d5ce7e044b549105b22f27a61b64a5445758d36b9090fd606a22d88` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/EquipmentTests.cs` | 4389 | `9aea8c38ff99e5b3b134d305dc6fac5193cbcb80861b0c234e5a1e0333d1b063` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/EquipmentUiTests.cs` | 8179 | `13d3ea5b8a34983b7a0c9ea83980e861705712e890c1209880a40d1c831e2b48` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/EventJournal.cs` | 1474 | `4d0b1989f0ac6fb8ec76a0919c58538db0c3e8a7ff99e6916942818b151cd510` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Ground.cs` | 9464 | `fd811e6c79571f01cbdd0081dbb8afe211c672a5e582e32663d850373484e30d` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/GroundCatalog.cs` | 4424 | `89bed0d3164140d5ed6515a048c7bd4cd254804d54dd4b82e3324291633c135d` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/GroundIntegration.cs` | 14912 | `848ab681f874ea6962428f6e95ac4aec2c83a1bc20dcdff53096a500111a89c1` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/GroundLayoutTest.cs` | 4065 | `ee0034b15f7795dbfa89058259996f8b87dfeaf714e7a7fa0f26a416a7c8d996` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/GroundRendering.cs` | 1778 | `8eef4260c68637abe91ee453f3ef368df84f39dd24b43d683a541c001f2724a0` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/GroundSpriteReader.cs` | 5373 | `a5d570ff8225fa1c02183bb050bc03fe43f5477f669ef9f4f5a86768b61e35da` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/GroundTests.cs` | 8324 | `87777facedb0f6009b63e1e55768f33c6d89a0cfc74b5927a02d2713862d8cca` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/GroundTransfer.cs` | 2488 | `60bd7b015b3dd6c9cd4f0a79008e58666422fea1153bf202ae23a65aba1459ee` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/GroundUi.cs` | 4893 | `f00ad844845fdabc20ec0e22833e10018b75538c0f8e697f7d7332d9c97920ba` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Inventory.cs` | 14555 | `0a01800bfa778ae5c1c8ec983ee2c2b307bf8b25a590cc08fd629fa427202015` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/InventoryIntegration.cs` | 14089 | `d412953fe22db17434f23e3ae1d8311689ca6f6492b580de0dd82dae6a5255f1` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/InventoryLayoutTest.cs` | 3684 | `ae7eeb059aa737d498a82ff2206d051b185b4d4c50a84c9a335a32b837ad6bb3` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/InventoryTests.cs` | 9732 | `0ab9ce7721b8178b377ef43411d9f6aebdb06bca8b8efacb71f3d02579019997` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/InventoryUi.cs` | 4541 | `8c08340ec9b8ac5670ceb26e9f210f067334d678626c674693bb7bf5f0c9b472` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/JournalTests.cs` | 2010 | `860c983b8a7002c35c2f6f2f35de68036b8f76d400b17774b8b030bb1a0a24b8` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/KeyboardNavigation.cs` | 1812 | `a14571c5746777373cda58022e1374b256ba8f342c00b45d578181e7a4ea8a99` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/LoginConnection.cs` | 10487 | `2239e0918b0c2ae94a29debcd0ca5428a44d1f95d128f469c9c1053616dd9cfe` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/LoginControl.cs` | 8054 | `02f97ac834993fb26871759c2dc7c2853494e804eac793b199bcc64c59553e2c` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/LoginProtocol.cs` | 5510 | `c72b0d6a517949301be34458eb8ffa5166b876388a1b06680dc9739b381ce2be` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/LoginTests.cs` | 4850 | `6ae6514eaa10cb8b3465230737db89dbc00f3895f50028e9282b17362b194cd3` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Magic.cs` | 11305 | `0537fb469b12ece28b30aec28faf9461d2f13e5687d1c7e7cdf7dce782743ebd` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MagicIntegration.cs` | 18560 | `7831385e157937e560fc9445da3a28c0228be61fd9c3338ec3d10dff335c3ccc` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MagicRendering.cs` | 4867 | `c65cd7e3af2f92d689bb093de1007003efff060798e71a7fe1cad602120f9963` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MagicRenderingTests.cs` | 4161 | `58ae873c2c0a31d5a460eb059148697ca95c80b85544f8e98d338440d30fa2e3` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MagicSelectionRegression.cs` | 4749 | `5fb664de0fa677308bb515148cc085670c8c0c32c0ba263c81fb08012ee5e110` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MagicSpells.cs` | 1129 | `20cc278da9390e90757349fb27080a258b412928ff1bd0e9a3bbabc232822de0` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MagicStatus.cs` | 3337 | `5b8d0eeec6a7a45457f430ab587d833d8eea387e3ac94d2e3717c2e3f805a8ce` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MagicTargetPreparation.cs` | 8802 | `70987fd3993bf663eb9731df11ec135a14ef0d2d03bdda51d9d2262d557b790a` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MagicTargetTests.cs` | 12548 | `b2f1a30941f61604c982267c70dd2c912f428b880d2ebab654991aa6723b1b18` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MagicTests.cs` | 7160 | `55cf1bbcd8643570ff0d8d954089c9d9d8877057e7ea84cb95124878de94dad9` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MagicUi.cs` | 4913 | `bfc95fe67b281d8c2ab9beeb95a07cd607dbb91b52a04f9837460423c6ac4c92` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MagicUiTests.cs` | 15775 | `cdb6bab4d853d13cc22a8c925b944702faaef7fea1dafdb618d3a0327ecbb8c5` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MapEffects.cs` | 4465 | `151017e2a085c66427959f9d6164f3cec08a455ea3866fd7e1f411f38261debd` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MapResources.cs` | 1187 | `8e3e1663a58345ab42bee1fe2c07777a74ece63da7fd6500323efd5b8daa016b` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MovementRegression.cs` | 4481 | `bb6434e453e3be718606d02d61dd1b9c47c96709751f401a93f234ab13716def` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MovementTests.cs` | 9031 | `e71343ddef87fbfb998ca661eba9ff48e863486ba633f8f8006ba81999af70a1` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/MovementUiTests.cs` | 6825 | `7021dbcfbf3427142de2d6890e0942c253374cbce798cd581d1687df19317051` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/NativeFocusDiagnostics.cs` | 1837 | `deaf7308df2c3f4d2f9f09406510f84bc42c2c96267b739a3fcd8bd6b0b5cca8` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Network.cs` | 6231 | `023722c40298ab25dfd20963714e060704b0f51a0540ef7117c0e8e9d598eee8` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/NormalHaste.cs` | 3032 | `9eb5d6bb5b63ef5a0bef2631c184420589bf30aa3149e146b08106f63e53a6ee` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/NpcInteraction.cs` | 12417 | `482cd99aff9bc74be2a58dc6a14fe7d5de09b66cb08d06e25c6afa0891aa0505` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/NpcServices.cs` | 7391 | `3e35cdf0b495a0df7cb154f0f8f4e2da851257284a8844c7aefc73741921a65b` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/NpcTemplates.cs` | 4724 | `8a55bc6e2494767e00a6ee101627a2e63614d7dee49f1d027fd3fdab6f5f023b` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/NpcUi.cs` | 7501 | `9bae4c5a5253f11ac3d316cdb5033c64e2e5d4182e1445dbe493ced9c213d020` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ObjectAssets.cs` | 7254 | `50f875d2d295d7bc7a179b2bda3ee740e84d2c1c44c2bda0d3d1ffa96045b21a` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ObjectPresence.cs` | 3409 | `2284a95a4c8a977799d9e21f9c38d37e0a0c96d15f4f957efbd0ae7eb79434fb` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ObjectRegression.cs` | 5940 | `a564442accb3d14d31759f1e2c221b1cde7cdd5f5a07398dbef68f799b77be7e` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ObjectRendering.cs` | 9819 | `6de015ce11c61e71aaa6de096d192abbb7d0c427f3f0bcedf762871d3a536613` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ObjectTests.cs` | 7033 | `ee59363cf3a72a711884c91568a429c5b9e34ed52101225e7e5c6ccf6f321db5` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ObjectUiTests.cs` | 6834 | `1c4b7c0f6ff16224a8894d0fb16c5ba6dc4c1b7199b22eb02f5527aeb9d3656a` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/OfflineClient.csproj` | 1706 | `c10793d57577235e4103a144a59cec2d337863fdbaa3fe6d1651ccc60a989015` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/OnlineInput.cs` | 1644 | `95cc98d91f699c5792822693843aba708778c3f1a66dd9bd230ccf7f5dac74e0` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/OnlineMovement.cs` | 10655 | `c88269dc968e03da158ab8e4d0757b964fab396828cba6d71c7c5cb20287b8f4` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/OpacityHistoricalPumps.cs` | 803 | `54a54a27f4ba685d7a9f43c163c62e04622e171ee21f11788ee0de9ba78bdc83` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/OpacityIntegration.cs` | 12930 | `568d1a564d769cfd716c9dc55b035d05fd33b7cb8532181f06d511fbe79bd929` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/OpacityPixelTests.cs` | 3609 | `14bfbfa68816b60d553c4e6e50a6707b9c259219c3e0d580fa2c0f11f5fdc382` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/OpacityRendering.cs` | 4181 | `bd7672c9d1378e908fa2ea1db3d78148dc7aec6c6f0bbe595ba2d9236a531608` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/OpacityReplayTests.cs` | 5775 | `c04a4076ec6bfb19e77d2dc04c2689a05db22ecceef8c55cb4634cd573edb6f1` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/OpacitySchedulerTests.cs` | 3565 | `8b0ac7118c194438f23f4a0adeb0de3027fa271853ae3d6c05af12ff74352046` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/OpacityTests.cs` | 2989 | `9141e8c187dca510dae15bc72d279729b5193fc9eb1a1c27df361a7d3d7cae0f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/OpacityUiTests.cs` | 8762 | `637feca43666c12f9df5ab4cfc96d4477a1d2cf5e9465a41561a9ff5cde69e13` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/OpacityWeb.cs` | 1862 | `17c2b8c8c68ce0e3d5b929899186c6f3679edfd04e5362deb620f3c80e14fb29` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/OriginalExpTable.cs` | 1286 | `325384a1437d6959b6233cbf2b7c76c681a2c8c6c1a05b41b7b9373e5bc198b1` | D — D04：程式註解及 generate_exp_table.py 證明 SQL exp 表轉為 Thresholds |
| `engine/source/OriginalMapCache.cs` | 20268 | `8c7009fd8483196beda9108a07a01f54b1e02d4447ba6cb3c6b88fb78b15d084` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PacketTrace.cs` | 5670 | `f5b8b0f0af0a455bd75db265cb3995dd2508a095c8581f0a6449f5fbd68f19dd` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PickupAssets.cs` | 6448 | `c3c27789794fd0579062d7e3328be605735cc148e1cbd829f3413ffe2dde7f03` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PickupPresentation.cs` | 6721 | `e4dfc947c163485d80813a9027599e904584f0db6426eb661284a39a5322deb6` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PlayerAudioSettings.cs` | 2704 | `d72460ffa9c1fe7a3959ea614f00355ba1dbf1facf21a9f584413c3c0d074c3f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PlayerChat.cs` | 4117 | `39d47921f20315167b2dc095d5475cd033ae5f5338fcfbc66d10b1b2526343d8` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PlayerChatKeyboardFlow.cs` | 1915 | `ea2330e6de11e4815f679eca1552ebd7546e01ad72ef6a4a4cf835c6e7d8035e` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PlayerHudChat.cs` | 4390 | `a737ee8f3ba7a9b502f7871d06c335cef921665507aa30e790ff96cfe29e3847` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PlayerHudIntegration.cs` | 6631 | `c1b8d5f44489b0e1878f2db412485449dc043b9e255b972c4c3b406bb663648e` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PlayerHudModel.cs` | 4055 | `40e34662696e391ada88cf634088562dda37d1425288726d3e82466db798d526` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PlayerHudServices.cs` | 2506 | `70393c3cfb18d470491b2ce6f2410fcdd195ea1b7043a3a2f493ecd97c6e320f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PlayerHudView.cs` | 14880 | `aa8396e96b2cbff56313c148289c460ed2e37c8f07c2810ea70ca2f19d1aa271` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PlayerPointer.cs` | 9105 | `4214c71199b3db6a582e48049513028540b89fd7ad27e01b79c705590f8d07d8` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PlayerPointerHud.cs` | 1527 | `64dbce54801a133cfb2e76f175f2b30cd8562aea0bb107727aaa029186b3cda9` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PlayerPointerUi.cs` | 3218 | `05db52e330846e9abc684f0edbc33f001ca2ec355d097d47479ad3922e77b11a` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PlayerProfiles.cs` | 3355 | `8257012faba79f2977e979712f596c80f2610097c9e45ae13cee91561240c5f2` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PlayerShadows.cs` | 3677 | `c44d4c21bdf4764a891e8e366b3211f80898336b8fab651e18874ceaee86bca2` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PrivateShop.cs` | 15256 | `01628e7caaf8f52331166f12fdde8866ed1253b11312ffd9f1ecc61def15a3b1` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/PrivateShopUi.cs` | 9420 | `3e091b7d014d1ca58080a37a9a7ae230f3ee2f733d3cb28ead6c84f4a6c4ce4f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ProductEntry.cs` | 2593 | `be174e03d4ae6400acf7b6ba36f52e7c77521f1ab12864fdc9f3ad4fdf8e4cb1` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Program.cs` | 25077 | `15ae3b55f67e39e47afe64a388695903c2b3f578f8f390cecead2fab6217696c` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ProjectileRendering.cs` | 5561 | `917625f6c00b430195311f34e5c7fdabf56b0d5d28f7a53b8abc1053368ff466` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Protocol.cs` | 3680 | `9c51aad89c9bda86dcedcdaa46cf135b0f787bb8105aa3826c8d7759e176f3f8` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ProtocolTests.cs` | 3386 | `ea9a6964091f310477c60cdec3c1447fd2d89927397d9bfb409762b513287a7b` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Ranged.cs` | 752 | `0ca871255023fe2b0405cbaa09e54b9982e3e93eda820a3491376aa9acaf9074` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/RangedGateTests.cs` | 3697 | `fffadae8d9d09f189ce574eef56238aaa2b536f6210fc2e12b400ff969d2aeb2` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/RangedIntegration.cs` | 11751 | `b7401c07772fc7713e3fa52aa66a0dd53c7be2c71dc95f8480bb7c5045ae38ac` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/RangedRenderingTests.cs` | 3422 | `f2f914622b7fa2d47e66d22b84ee6b1c4e9683ff84d1afa09fb45198e9cded94` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/RangedTests.cs` | 3013 | `860e8978d2912b6431c7023d53558594fd2e844b2c76aa85bee02af06a247a0e` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/RangedUiTests.cs` | 11293 | `22e5913d68fd62d3ec2c494cecfe77525974eb8f31c78568645eab07b35a51c4` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/RealLoginTests.cs` | 1938 | `929d1bff1cf4405287ebec0920bcbb47ddb3325db4697c2871a70266f0ea28ce` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ReceiveLoadTests.cs` | 4517 | `7a7d74ac0bb854e3ef55f8e9df38ae3e94d2d261ae5fd86a884bbb43d6ce0eaa` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Renderer.cs` | 7142 | `889b6587ebc5b6d85cb914284efc800c2c18c1c2c4713226d7b1d3e60a1f74e5` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Resources/ground-catalog.json` | 9406 | `ee5446856a00eeced52fecc296a8da139650ecaa8f69a47c725ba9d93c71f4d8` | D — D05：rows 保存原服物品欄位與 SPR bounds；混合 pins 不使資料變成純 metadata |
| `engine/source/Resources/pickup-own-base15-gfx0-weapon0.json` | 239766 | `43b88cec329929484610aceae84566d1a922eb4b8ceba93e17dc44b65922ca90` | D — D08：SPR 派生 RGB555 frames.blocks.pixels；PickupAssets.ReadBody 消費 |
| `engine/source/Resources/pickup-own-base15-profile-extension.json` | 2473547 | `aff2e04d4f9395f7993baf5818eb16218730b7def0a29c7d07e86c314db513ba` | D — D08：SPR 派生 groups.frames.blocks.pixels；PickupAssets.ReadBody 消費 |
| `engine/source/RewardIntegration.cs` | 14142 | `a1fd17efcc8539fc0df8848634f14361dfe89b3320ffbe4ee56c3a355b86995f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/RewardTests.cs` | 5424 | `6c55245e46c70208f807668dde060725a1d5d76d5364e2bed67d1e93bcb7bdeb` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Rewards.cs` | 4546 | `eab28b4facf6de1fd9576f87ff99c19d3c929042b6ecb100d747681e419826b5` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/SceneOpacity.cs` | 10283 | `c6dc450181b1f19d3778738512b5a3dee5b95f7bdb27442241081f9acb0200b1` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ServerMessageText.cs` | 1256 | `aae0243434301a53a54c762ef1c2786e9336503361c7dbc6becad20ac434a25c` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ServerStatusMessages.cs` | 564 | `80595b85cb06efa6875fedc75f41796156bc967a3794288ed553c80755a897a4` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ShipBackdrop.cs` | 7711 | `c3226978d6a99d646ad669c09af31ff4188de83a96470e429149b24a7fe52f1f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/ShipTravel.cs` | 9590 | `be5f5bd8c3086c0aefd561ddf8579cfe3a24847456112fc59b1e56026e7b040d` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/SystemAnnouncements.cs` | 760 | `24267217581cf3c6abbca50627b00a20bb2aeacf79197c46c07cc6283812a043` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Teleport.cs` | 4346 | `be65b1ed0776e8fcef557115525945ea49c8b3080c74386f20044426f5344e37` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Tests.cs` | 12205 | `7c6c04dc6bf70a505d6ac518ebadc79c0832f72748959e0d8c73182a9aa7b612` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/Trade.cs` | 11364 | `964388f41f6841a5a86503aeee2fefd85a8e081712f552de5ff5aa5aa0b8cd37` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/TradeUi.cs` | 7341 | `007b0f58d5e0e41f035abf044673430645aff0e199930b58c91047bd77c3d544` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/WebPanel.cs` | 20463 | `30f20e7f1c9f3d4dbeb5b5396d174115b7c24788b5a1e58ea9b1d444b7b71d35` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/WebTests.cs` | 6046 | `e442819277dbab19f31c1677469d55a22b3f06408e1d4d0b85f078354c2f3809` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/World.cs` | 7187 | `94013006de44509dbf2837abd4d3b56c3abe3f6c2bf15e855765522fd6e69fbd` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/WorldEffect6016.cs` | 1574 | `e631a0aea5c79e4f7d70eebe9bdaf4696a9d95c746199e0f0c72b04ce65dd8cb` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/WorldEntry.cs` | 7921 | `8593bfc088b6e1f7796f842fe931a7a2d5a887dea0315ccfe3bb04ff7c1bee8f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/WorldObjects.cs` | 5869 | `d86aa50b81d36a9353b1d0910cccdbf18affca56e555b3080cb8499cfb22e680` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/WorldRegression.cs` | 3238 | `35a52654aa262a0689f24148be63e558ac0e81c1b163c098c93d99056e243d3f` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/WorldSession.cs` | 9257 | `92bf845b68138cdcb1d85e675cd7697579815cea83d173a97ca913cc7f9baf0a` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/source/WorldTests.cs` | 8432 | `89840da776b378d7e0ea05a8178d02c436547f0fe62ed0ea2330ff7f923b9bc4` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/tools/resolve-path.py` | 741 | `fe1ce763f30a071163c6bb627bc85ffd5557acbea2084be284a1a8ebeacf71da` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/web/bonus-stats.js` | 2361 | `eaaddd0f2c2524f931c11518f24d9bb3da14b3658c2d70aad4fd5da5c801c0f0` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/web/ground/36.png` | 289 | `8d72431ab657dc156297aa3007b0843c01e75f20dfe5379b27d5cd8d5b9f29f5` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/ground/66.png` | 465 | `3ecde86e10281647ddc1f66ddb42a44a83a98c86baa4ae317ddb8859b3e4fe34` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/124.png` | 844 | `7a989ea2e4f676a42d82e612ca9ff6a1d25e27005acf0cc312ffd270cef62919` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/138.png` | 858 | `335162b3786bced49563cfe86aa14fc4114867c9aa8237821023704a08ee0b98` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/2.png` | 495 | `0f609374be13892e61096e39fbd126cc7258a179a108dcfd80ad2e9e34d430a0` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/242.png` | 996 | `5ee66ffca19d9d3395318df30ee32aaf94c36753dadf8a9bb9935b564b07dccd` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/245.png` | 1036 | `ea4ac1c311981c4380e17779b4b15f1ef2a2a71d2b1962a2591f203522e9b001` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/278.png` | 401 | `c7a17e518c4e3978a83446e66c4cf7ae5cd54caeb04184556442a48b9f75f220` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/318.png` | 1499 | `ec7b23a61616e87b992ae01fd95ddfa83430300141aa85f4ded444af31c46a26` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/35.png` | 460 | `9ded6e008b2f981978dab2874f924b72b8d5f96ea6c4c856c4ebe60884a00073` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/4.png` | 510 | `75ab427d9ee5b9051b6cc4103b43eaadfd7cadd75d73d29bae9caf28ce3ac93e` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/474.png` | 1352 | `569b75068a19f536d78b12a6b75d8bf6e61655b9c1a7f282b0096b9043ecfa1d` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/475.png` | 1365 | `af4f6debaadb18732f040901c0ecf166156782a25e26da6b6867445d3c325846` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/5.png` | 907 | `9e35714d6b77ab794679b0a32f295cea5a78b773fbb745f46ce00366a4b06ff7` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/59.png` | 499 | `cb4045a12f6d28c81bbb25e12173741b184f8bc2fc90b42d9063d0ee076a669e` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/7.png` | 359 | `90f23210260582bfcc8c9e8b7a00cb727840e73082e3c7ca33c1e1fbc8b3143a` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/77.png` | 743 | `19f5dd065bc709dbd1c0fe59bad01e0a8acfad39593ff90621f069a709c9a727` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/80.png` | 724 | `0a6b703b2b6b0709ce4c7364f36ed99ef4cc907712d7d0059aea90c0ed9f4f37` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/81.png` | 696 | `eda21f8685278d50e150fa50ea4975756ea5cede2161fc86f1726199615ab988` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/86.png` | 1306 | `f1a70fcdcee29ea66ff20f204157927e07468cfb50da545adcc2c93831b389a9` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/957.png` | 1192 | `9b1df82afe88dc2183d9f3e69e8218d4126330e1f8f4179bf861ae305aec3a31` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/icons/97.png` | 776 | `a630764b5725c0fc3629a4b86cc60d309695e9673179cb35d73b406d2769ec1d` | D — D01：PNG magic；TBT／SPR 派生，詳逐圖表 |
| `engine/web/index.html` | 8086 | `27795cafd5b135781629df09c083ad290e8ce4527f4a51d498e367d5d3204b38` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/web/interaction.js` | 3596 | `b007e3ce0a2a4f2ef13dcf72db66adc26640379ad4eebedb357dc8e026132aae` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/web/login.js` | 4568 | `482384344ac719449c81c8e7c2212370dfd422dda52f8d4dbff8c9b3405c5a51` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/web/panel.css` | 2933 | `66310ea6a1cdeda68bf1a817c24127830a0baacff0cbef88a7ed6dfd4f0b4a91` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/web/panel.js` | 16257 | `ca89817e9b66c0adcb1aa6bc0ae7083b92d4306902792ffbd01d9b89e2335198` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/web/private-shop.js` | 4228 | `f9afcb14c0c1f3ef5a69556203d25348035a6fcb888084e59a50da1f53a140a4` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/web/trade.js` | 4161 | `5b677e40462c5ec933fc5fa014b94fa70b3b1f2e53bc860cec9f9ba66e4f42f9` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/啟動新版客戶端.cmd` | 486 | `74fa93e621839389a8bf5e13d18e82a51fc3b55b5e478b35fc2f793e4a80b117` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
| `engine/新版客戶端使用說明.md` | 622 | `2e75426eacea4ac85894836ec5032dbdc56a4f57d5c33da83696aa93f24a7e36` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `project-manifest.json` | 29380 | `98b89777d5de7ab7fa421f343d29545f7e22bac7d88d7a21f9f5d37554bb089b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,updated_utc,canonical_root,stage,revision,whole_project_complete |
| `work/P25-architecture-baseline-r1/HANDOFF.md` | 3893 | `1761680dbabe3b9f0053e6a26a00c32a45cf40a354539377fb2bcc3f6d16c0ab` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P25-architecture-baseline-r1/architecture.md` | 9075 | `b6c0566b07e8829e8bcd09aa0132ce17c399665d2bbe06d8a3436f4419a9594f` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P25-architecture-baseline-r1/artifact-manifest.json` | 2568 | `3f5b1f5d637127b33f9fa75d089e90dd32d6b40ab28b485c1d5f2beb2a245b19` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,phase,files,excludes,scope |
| `work/P25-architecture-baseline-r1/commands.json` | 639 | `0f27eeec70e40d51579e4823f51faed723b30e14cf5fedf7c275b51b2733dcfb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=verify,regenerateDocuments,limits,sealDocuments,regenerationOrder |
| `work/P25-architecture-baseline-r1/completion-matrix.json` | 175823 | `acaf132bd76642083316c78f23a7c6015d4cc0ca3932e00e4e26456f7c0d05ed` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,phase,baseline,sourceDocuments,denominators,rows |
| `work/P25-architecture-baseline-r1/finite-backlog.json` | 17328 | `b1dd3fe9c0ba31203c1ad779bdb24f6c4f037743a65ae55b996b9f1201d491c9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,baseline,items,noConfirmedNewProductDefectFromP25,closedStaleWork,boundary |
| `work/P25-architecture-baseline-r1/historical-evidence-index.json` | 56714 | `f8a43c57c3ca3cdc3db0fd1d101a992c77ab188679ceba1c47d165afb6d795a4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,rows,resolutionLimit,originalMatrixCurrent6016,nativeR33Accepted |
| `work/P25-architecture-baseline-r1/module-map.json` | 49796 | `56e73ea1906d654fa26ea7318b15280940cb9ae2cd460da2ce93369317c9e8cc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,phase,baseline,artifactOwner,productIntegrator,supervisor |
| `work/P25-architecture-baseline-r1/progress.json` | 29362 | `b2bef1981ab6cbbb526c026c135c2ea67fae9302431e19209c2bf9a0fc06c880` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,phase,taskId,taskIdEvidence,turnId,turnIdReason |
| `work/P25-architecture-baseline-r1/result.json` | 3802 | `a4380f74b6497d1963ded933ba0e54bc8aec119e90562dc20df6c32e79d4cd69` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,phase,baseline,status,artifactOwner,supervisor |
| `work/P25-architecture-baseline-r1/source-binding.json` | 69724 | `29610d509decdbac9d8f4663e78b16fcf909f5b7ce7ec3ff90e3da2fbaa6a028` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,acceptedSourceCount,acceptedManifest,rows,readOnlyAssetsTarget,fullAssetBodyAuditPerformed |
| `work/P25-architecture-baseline-r1/source-pins.json` | 96940 | `0dc08e96fb529baafa30eccc25f8fd26dd047df9d2509c1c114da5029e1df3ef` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,root,files,hashAlgorithm,scope |
| `work/P25-architecture-baseline-r1/verification-report.json` | 598 | `444fda57921c9a132dbb08406c25fe7e4f1b0c96620c26e22ca6685ed3bf32a1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,scope,sourcePinsVerified,acceptedSourcesBound,modules |
| `work/P26-authority-rebind-review-r1/HANDOFF.md` | 4369 | `615e3df83228b37e0bbeaa4c98e5f1a0819c43c480b35c759eb7ff5c155d1ec5` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P26-authority-rebind-review-r1/artifact-manifest.json` | 6420 | `c84747bc79756e2a94a9c3e9d3831fb7eb682f6018048e07730e39df81a77a00` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P26-authority-rebind-review-r1/authority-tests.json` | 3697 | `fddc3d2e6323ce533e1bd4f0b2a70a87cd4e00288ae55c028f2328fec47dab71` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=cases,sharedReceiptWritten,fullValidatePlanExecuted,controllerMutationExecuted |
| `work/P26-authority-rebind-review-r1/comparison.json` | 4334 | `5dd4fa9b48206bc7d69e022f94380527a8d472a9229ce2e3a4367705c8afa9e1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=seal,stageNonAssetPins,oldPlan,newPlan,controller,minimalDiffs |
| `work/P26-authority-rebind-review-r1/dependency-archive-review.json` | 13806 | `a97371a46b491fae06986ce1e257922ba59464270dce44d8a508f8052c0cfe72` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=oldSupportPinsRetained,newSupportPins,totalSupportPins,archiveFiles,archiveEntries,requiredDependencies |
| `work/P26-authority-rebind-review-r1/findings.json` | 5495 | `9b4d0945c90d55dff2ca5ccbadaa074485f514e07f8139156691b1ff76132268` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(6);keys=id,status,blocking,evidence,conclusion |
| `work/P26-authority-rebind-review-r1/progress.json` | 488 | `210cdf2d5569277815bac75b2df66158b1b19916e14f30286c7408699526e0be` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,turnId,utc,step,decision,dependencies |
| `work/P26-authority-rebind-review-r1/result.json` | 1780 | `7b416c48543abdb8e3149728b48f37372d7bc17e6dde8b34fae51fccf2e6bc7f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,taskId,turnId,utc,status,decision |
| `work/P26-authority-rebind-review-r1/review.json` | 9396 | `3fe29529a4809d9f0236d5a46890ddf9aa34bac41c8a2497f3e48dd5fbcc7658` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,meaning,candidateRoot,candidateSeal,candidatePlan |
| `work/P26-authority-rebind-review-r1/source-sha256.json` | 404525 | `e0c0dd829618b8cb0ee421f48fba07c6f0fce0e3001effca833e1388316a4b58` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,algorithm,assetLimit |
| `work/P26-authority-rebind-review-r1/supervisor-gate-contract-inputs.json` | 3868 | `4f6726375b64d4cbbbc74b424a9289ac7dd80e8a1eeff452d54047e0e6f764d4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,reviewDecision,supervisor,integrator,plan,controller |
| `work/P26-authority-rebind-review-r1/timeout-tests.json` | 6165 | `2eddfc6f8faf0a27b5a95583f0d3a69d041899e9c9c73679234f15d2f7e15d07` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=classification,cases,realProcesses,changedExecutionSupportTimeoutFixed,nestedSealedRebuildTimeoutFixed,nestedLimitation |
| `work/P26-pickup-integration-review-r1/HANDOFF.md` | 4455 | `1eb2ba1a499727dbec608ce11c3d6acb43b4bba61805345330aea7a2d88fb1c6` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P26-pickup-integration-review-r1/artifact-manifest.json` | 5469 | `833fe958e2ec9131f48626cfb0f01c826daa5beacdb399ee47f4d55655b581bb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,excluded,sealedProductChanged |
| `work/P26-pickup-integration-review-r1/findings.json` | 8234 | `76760796d3d9405860b3ef614d3d8860617cf60190627b7d8a1d3132308b0fde` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(4);keys=id,priority,status,title,evidence,trigger |
| `work/P26-pickup-integration-review-r1/followup-contract-inputs.json` | 12244 | `6397fb11d21222d71e9b418c0653a1389fd73aaf8c706ec7962ee5f4d9604771` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,supervisor,integrator,authority,canonicalCurrent |
| `work/P26-pickup-integration-review-r1/inventory-verification.json` | 3018 | `0141af792f0156918f3b69b4f0b6c20d287f688511d7289145f38f0222304378` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=seals,sourceManifest,sources,sourceCategories,overlayFiles,copyMappings |
| `work/P26-pickup-integration-review-r1/nested-command-review.json` | 1983 | `6a9378bdcd2b458adea29dba1a50626eafc5f2b296a16307bb38cdd37c339c44` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,commands,powershellChildren,perChildReceipts,childStreams,exitGuards |
| `work/P26-pickup-integration-review-r1/policy-drift-proof.json` | 675 | `dc2fa0d642ad975ac93cf6365852268a581562bc724cc6aa001be248fe7ea1fd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=method,removedOffset,removedBytes,removedSha256,historicalSha256,historicalSize |
| `work/P26-pickup-integration-review-r1/profile-prep-verification.json` | 1295 | `f66894df2622167708e1b853ba29dddfbba7b649fadc06a9f7cb020187ca5023` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=fullVerifier,fullVerifierNotOverridden,sourcePins,matchingSourcePins,drifts,artifactSealVerified |
| `work/P26-pickup-integration-review-r1/progress.json` | 1083 | `6dc9ee0848dd825867ab5d7f815bc1d637fe76b25b762f823d38a0029d40961f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,taskId,taskIdSource,turnId,read |
| `work/P26-pickup-integration-review-r1/result.json` | 2470 | `5a5ed6e91236084012381a53c80f593d6e0256a170b36b8dd7d5e4027f281122` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,taskId,turnId,taskIdSource,utc,status |
| `work/P26-pickup-integration-review-r1/review.json` | 4771 | `7bd2b528180f8ae42238fced602ef8ce116b95efd6bbbd0fbe03bc38d3e1de68` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,reviewScopeComplete,items,findings,plan |
| `work/P26-pickup-integration-review-r1/source-sha256.json` | 584626 | `910a1347777157d53aa8f310a1ca886b86c731bc4f96890adc2274110e7f1950` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=algorithm,files,note |
| `work/P26-pickup-integration-review-r1/synthetic-tests.json` | 1602 | `c6b1b65a980ffd25dfc449aed739844db8b65f8797585c14e9338c44aabc4171` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=classification,fixtures,tests,productAssertions,nativeChecks,realControllerInvocations |
| `work/P26-pickup-integration-review-r1/verification-commands.json` | 3168 | `1a36cf62e291e183e0916aae5f6efafc02c50db3159036103ff8589744a3e455` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(3);keys=name,argv,cwd,exitCode,exitSource,stdout |
| `work/P26-r34-authority-rebind-r1/HANDOFF.md` | 8369 | `548ef2050ae72bec1dca3a176d411d975fcb151a1e9d856dfd56def7603e7a0d` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P26-r34-authority-rebind-r1/adaptations.json` | 1175 | `37debb75c9b78e1c51f900087a021bec3770e20f4e187f77daabe31198714158` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=inheritedStageAdaptations,additional |
| `work/P26-r34-authority-rebind-r1/artifact-manifest.json` | 8024 | `f253df9c11125d8d2281dc5032d40cb7a38bdfcd7f27b7657bd866f1f67ed1ee` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,executionAuthorized |
| `work/P26-r34-authority-rebind-r1/artifact-seal.json` | 8250 | `867c75d72137b18becc8138576bc0b2e8b6e529fabcd51482b485caa17419ad6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,fullSHA,excludes,executionAuthorized |
| `work/P26-r34-authority-rebind-r1/authority-input-pins.json` | 1485 | `145aed381b66f1b8bd4a55be9099ef07e41ceefe5a559f78b63ae0171e77f822` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=goal,readEvidence,oldPlan,oldSeal,immutablePreparation |
| `work/P26-r34-authority-rebind-r1/authority-policy.json` | 2391 | `b0375a991b0128beca63fb53b31264d869f23ccb87e539bf4b1a1ab90c4dd6da` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,currentSharedSupervisor,historicalStageSupervisor,takeover,takeoverParent,historicalStageAcceptance |
| `work/P26-r34-authority-rebind-r1/before-state.json` | 3847 | `70cc332bde82e6339f48f7b3a7e80f97c37d9adc03f9cae2450357e9ca0570fe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=engineFiles,mustRemainAbsent,sealedFiles |
| `work/P26-r34-authority-rebind-r1/canonical-plan.json` | 714350 | `6a9d7d6d57b74e7de93bad32aeba82d6ccdf504efd641daad718a53cc7b14761` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,canonicalEngine,stageRoot,stageSealSha256 |
| `work/P26-r34-authority-rebind-r1/collector-provenance.json` | 1090 | `c9b442afa0582685e56818eb2c63299f20bbdc154664fee4b702a60d6ac4c213` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,function,extraction,functionBodyChanged,contextBinding,occurrenceSupportSource |
| `work/P26-r34-authority-rebind-r1/command-recipes.json` | 59897 | `d7ff9a1bce8d701f8763333fa8d67c4ce0d4bf4bad1608f14ac5c3c4d398cef7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,operations,nativeSuites |
| `work/P26-r34-authority-rebind-r1/context.json` | 1727 | `ff3cd8dacf6fb6ddcc391e48bfc8318eac85f4bc5fe6137495619992b184af14` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatch,preparationRoot,stageRoot,canonicalEngine,workflowRoot |
| `work/P26-r34-authority-rebind-r1/initial-source-rebindings.json` | 10609 | `f55d455727f76a6be964f7b6ee3e4d7683ff60677898cd5a885a79c8e920d86e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(19);keys=old,new,change |
| `work/P26-r34-authority-rebind-r1/nested-product-command-plan.json` | 4442 | `08ddef8d55952364505fc0dee9f75f62b16123e81d326ef10460c38d81885f5a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,commands,productPowerShellChildren,childLogging,sealedScriptsUnmodified |
| `work/P26-r34-authority-rebind-r1/plan-delta.json` | 1707 | `5a8c19065deae64a99e437300026b54c1b48bcd66d069156b82b24ee12456ade` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,oldPlan,newPlan,unchangedTopLevelFields,changes,innerCommandRecipes |
| `work/P26-r34-authority-rebind-r1/progress.json` | 1285 | `9db52c08ec9936a729058d23966c435bf82e4024d65d2e84f5aa2a7835d499db` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,supervisor,retiredSupervisor,goal |
| `work/P26-r34-authority-rebind-r1/rebindings.json` | 12667 | `7f526fc97c1493d2fed1505c3c7e9a9a154ff122dd4f46cd7faa151df800d56f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,pairs,newAuthority,newPolicy,separateTimeoutChange,planDelta |
| `work/P26-r34-authority-rebind-r1/result.json` | 4732 | `ded878bce7add4af345902ea0a075ed96ebf1cdb234dcb4da4f4501c5ccdf4fb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,supervisor,historicalStageSupervisor,onlyWritableRoot |
| `work/P26-r34-authority-rebind-r1/source-copy-mappings.json` | 76724 | `2ae78956ece2d27ab335c00ad213897e10b9ff5a0210264279de601e28a794d9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,assetBodyCopies,overwritePolicy,installOwnedBy,installSourceManifest |
| `work/P26-r34-authority-rebind-r1/timeout-fix.json` | 1448 | `62d3e0e59d162041d6c2769ae7d4ab219101a25092d63a100fd5b6782cc37198` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,authority,separateFromAuthorityRebind,original,updated,diff |
| `work/P26-r34-authority-rebind-r1/verification.json` | 3803 | `d4961a0a9403f8fe419a6f1ae79156eb5ba5050cbd06b70979d2c081f5614d83` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,planSha256,checks,syntheticGateCases,syntheticAuthorityCasesPassed |
| `work/P26-r34-package-plan-r1/HANDOFF.md` | 2003 | `40a39391022142a2fef1818a98a1124ecf485b2cf0508c695f5b58317eac473c` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P26-r34-package-plan-r1/artifact-manifest.json` | 1695 | `11ef00a4e49808bbfc1c5718d718df1b3e50bb142d3030b1da3b12f1865d5e1f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,acceptedForPackaging |
| `work/P26-r34-package-plan-r1/artifact-seal.json` | 1872 | `76a7cee9cf78022681eb744556f99d2570befd641df14cdc679a607c53919138` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,fullSHA,acceptedForPackaging |
| `work/P26-r34-package-plan-r1/generation-result.json` | 1021 | `d5b0e3deba495c40ae5cfc43586e5e86381cc700f4d14ad63eef2199c75ca78f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,plan,files,releaseCreated,releaseReceiptCreated,entryActivated |
| `work/P26-r34-package-plan-r1/generator.command.json` | 1002 | `99f3816d0ee876f6d4a8cfc514ff7c314a0e1fd8f4354820e2c3c07989178360` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=argv,cwd,exitCode,stdoutSha256,stderrSha256,workflowFrozen |
| `work/P26-r34-package-plan-r1/inventory-findings.json` | 5965 | `7f4c4007d896747a9818dc86ff512e1d1effa69fdb740522999490fae8374f40` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,plan,files,allListedSourcePinsVerified,issues |
| `work/P26-r34-package-plan-r1/inventory-verification-failure.json` | 661 | `74511db6cda46771f338f15f78f756b53516b617793eb52418d6152100f7d6b4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=command,exitCode,error,stage,workflowWritten,tracebackRetainedInToolOutput |
| `work/P26-r34-package-plan-r1/package-plan.json` | 1098050 | `a831ca6dbbe13dee16bf544b6ff9f1ea3284ff7091915af23685ab8827d0d890` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,canonicalWorkflowRoot,stageRoot,stageSealSha256 |
| `work/P26-r34-package-plan-r1/result.json` | 5877 | `d07d13eecb17cc723490a81d3256219de6b8b81d547f1a0ae794dd18fbd94861` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,plan,planFiles,generatorExitCode |
| `work/P26-r34-package-plan-r2/HANDOFF.md` | 1966 | `d8eced6d3734fa9ce931bde826a9d1ae03cb02f62edc2a5ebf58e4de0a47820b` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P26-r34-package-plan-r2/artifact-manifest.json` | 1497 | `a8f05f27f08dbae023a8b72a897cd380028fc255bfd745fcef2157d667f48db9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,packageExecutionAuthorized |
| `work/P26-r34-package-plan-r2/artifact-seal.json` | 1674 | `69d46c60191d6126ebc4ec7baac466ca2d1407f1787b0a87db93dd27361bc58e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,fullSHA,packageExecutionAuthorized |
| `work/P26-r34-package-plan-r2/commands.json` | 736 | `2aa22691f2ecd2762ffde5cfbbbf64fa385aa09296126f2d54005b5ddfd872f3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,records,observation,generatorReexecuted,productCommandsExecuted |
| `work/P26-r34-package-plan-r2/delta.json` | 1757 | `ece8a1227e1d4be1fead7308b860f766dde9c0cb50bb67787f370190ec48489b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,originalPlan,correctedPlan,correctionScript,addedRows,removedRows |
| `work/P26-r34-package-plan-r2/package-plan.json` | 1099423 | `939fcbd2176d1360b510153993f43deacf91b34a3b1737fb4d91ad8b30bb0c01` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,canonicalWorkflowRoot,stageRoot,stageSealSha256 |
| `work/P26-r34-package-plan-r2/progress.json` | 363 | `08217ad4f50beaf6c377742d3bf846d8cc68a60e7474d65bbb4b633eb4b78632` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,goal,workflowFrozen,oldPlanPreserved,expectedRows,releaseAuthorized |
| `work/P26-r34-package-plan-r2/result.json` | 5337 | `780ee5046ddcdae6181e4f5b225523f9295329552c9ea65458681c3d408cedc8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,plan,files,originalPlan,correctionScript |
| `work/P26-r34-package-plan-r2/verification.json` | 4087 | `b1366a7e13530aedb340462c1438e5e0bd654658ab811d746e0d9714a54e2bbc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,plan,files,originalRowsUnchanged,addedRows |
| `work/P26-r34-release-execution-r1/HANDOFF.md` | 2049 | `74ad70cea6d2c83c351d3e19e965c8da04b20ae2b703f77bcfdd4d47d83fd3dc` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P26-r34-release-execution-r1/artifact-manifest.json` | 2148 | `bcb82d7b7c7565b250f2a8c8db5973d25ec55b14b4c8d72a274f3410df7332fb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,releaseReportsStoredExternally |
| `work/P26-r34-release-execution-r1/artifact-seal.json` | 2310 | `c43cd6d4658d5e2bbbc415cf2ea2a9237e0a7ac7cda86b6289953014df94d9e5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,fullSHA,entryActivated |
| `work/P26-r34-release-execution-r1/before-state.json` | 682 | `57d356a9d7aadebbdeed0c04afee915f7c1dc29ff8794676bac1f076ddd9f2cd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=rootFiles,assetIntegrity,releaseInitiallyAbsent |
| `work/P26-r34-release-execution-r1/execution-summary.json` | 3193 | `a57352defb6e03c1b578e69b9fce2600400d8580a56eddcf74bd358fc28acd6f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,command,rawStdout,rawStderr |
| `work/P26-r34-release-execution-r1/input-pins.json` | 1770 | `25270ef477e5134d797f264fab7969789246d11a611d4e818b846c7a17679715` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,controller,toolSeal,planSeal |
| `work/P26-r34-release-execution-r1/package-plan.json` | 1099423 | `939fcbd2176d1360b510153993f43deacf91b34a3b1737fb4d91ad8b30bb0c01` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,canonicalWorkflowRoot,stageRoot,stageSealSha256 |
| `work/P26-r34-release-execution-r1/package-started.json` | 861 | `9a9989b7364ef51d2459ba3435413b57f6fca0104c0d5e0df9abf34d7f91a794` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=argv,cwd,startedUtc,exitCode,completed,attempt |
| `work/P26-r34-release-execution-r1/package.command.json` | 1507 | `6c15400be244adf9ecdfb6ca4b230b615b443a8a217932070b664e33ba163ff1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=argv,cwd,startedUtc,exitCode,completed,attempt |
| `work/P26-r34-release-execution-r1/progress.json` | 519 | `b879534bf53ec14d95664bd839b6a7ea2f2f58a0f80f139a4f997af19264dfda` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,dispatchId,goal,releaseRoot,entryActivationAuthorized,utc |
| `work/P26-r34-release-execution-r1/r34-release-acceptance.json` | 5501 | `98053457e8accacbfce4618f3fe6906b04c1f17ce0483167f2ec5907085770df` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,decision,dispatchId,utc,planSha256 |
| `work/P26-r34-release-execution-r1/result.json` | 4551 | `557cd0872f81fec15bdaaa42466811a2303351d860585cd46b9b442a997335a4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,command,rawStdout,rawStderr |
| `work/P26-r34-release-gate-r1/HANDOFF.md` | 5304 | `88793e243d47b3edd0bb0ff1f6cba65eac65d1a4fb631288a616eec30ade8eb3` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P26-r34-release-gate-r1/artifact-manifest.json` | 3097 | `25b73d3b5c8371e3a13af20a8f14e3e18644271056e892aff94fa9389fcc6e54` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P26-r34-release-gate-r1/artifact-seal.json` | 3307 | `e8b88833ce71b783a7a57c82d4322b2306ea4cec9534e21bdfef7e9b6785ad3a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,fullSHA,executionAuthorized |
| `work/P26-r34-release-gate-r1/before-state.json` | 2709 | `3b9e92c5c8ff8ed11d89ca1ab5387cf956be02ce44d72e353b2748fdff31a539` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=currentEngine,rootFiles,sealedInputs,mustRemainAbsent,workflowAtStart |
| `work/P26-r34-release-gate-r1/context.json` | 1504 | `79078f10a81be3f9eb817449f2950c3276c7c952b129d43e80646daa8df90f62` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,projectRoot,preparationRoot,canonicalEngine,workflowRoot,stageRoot |
| `work/P26-r34-release-gate-r1/progress.json` | 1538 | `0a9299a9047c4c8f067366291985d033a3596fc75b24441bff67ed10dddd3ef3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,onlyWritableRoot,goal,supervisor |
| `work/P26-r34-release-gate-r1/release-authority-policy.json` | 2241 | `ed7b626da713ba02a8e606fd13f57fe730d84f99983b7034495e5389b609151a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,releaseSupervisor,historicalStageSupervisor,takeover,takeoverParent,releaseReceiptPath |
| `work/P26-r34-release-gate-r1/result.json` | 2772 | `8de23c31ceaec8972a6c37bde66605e4d11506cbd0e4faedf2ae7eb1d0a78878` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,onlyWrittenRoot,goal,controller |
| `work/P26-r34-release-gate-r1/source-rebindings.json` | 2679 | `af681a0bcce23ca73a01ed3ed8192bd32949dfb83badc41e41664e27be787325` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(5);keys=old,new,change |
| `work/P26-r34-release-gate-r1/tool-source-seal.json` | 3782 | `40b32ea97bb80259c329ccb78001c71b0a5c9edbda160d66315d061fb5336c93` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,externalDependencies,executionAuthorized |
| `work/P26-r34-release-gate-r1/verification.json` | 4602 | `da2189db61c668d7125ceb3cfc70b4a4bef4ac3568034b6967c18114b98412f7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,cases,syntheticCasesPassed,originalSubstantivePackageGatesPreserved,assetHardlinksPlannedOnly |
| `work/P26-r34-release-gate-r2/HANDOFF.md` | 4145 | `6054080fb059135562494a2bdf877340720904a223bcf5bde232610cfd633017` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P26-r34-release-gate-r2/RG1-source.json` | 727 | `99ebad5340471b3cf0837e2434512ce04f1d730c314581e4e54c65abd9b707d9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=finding,sourceTool,readOnlyCanonicalAcceptance |
| `work/P26-r34-release-gate-r2/actual-evidence-closure.json` | 200027 | `27c7cfd17d070b328e334dc4ea5689076ab5eb028e952579c846868f720b0071` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,innerOperations,outerMarkers,nestedCommands,pixelRows,pins |
| `work/P26-r34-release-gate-r2/artifact-manifest.json` | 125326 | `29d5d65c3f5df4f7c968fc94a0b546acad31c25d1697a9c0250bcd81aaf33e2a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,syntheticDataNotProductEvidence |
| `work/P26-r34-release-gate-r2/artifact-seal.json` | 125494 | `30655ae3c7ce2b8efb82a2f7bfb35adc0f962a0f0162a0d323c29e231577fe13` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,fullSHA,executionAuthorized |
| `work/P26-r34-release-gate-r2/before-state.json` | 3214 | `2450fefadca1d36fdf1436b41c65ec5ed68a2279bd4535b0a8ddcde5de5b6785` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=canonicalCompleted,canonicalResult,r1Seal,rootFiles |
| `work/P26-r34-release-gate-r2/context.json` | 1539 | `efc346a2a43b57097894d044a60e469222561c4a647123cc9bdd900955224e02` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,projectRoot,preparationRoot,canonicalEngine,workflowRoot,stageRoot |
| `work/P26-r34-release-gate-r2/progress.json` | 518 | `1fca34d214c086784ed29057132585a34cf15dcd398bd3aa6e9c07e1afc64f55` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,onlyWritableRoot,goal,canonicalAccepted,productReruns |
| `work/P26-r34-release-gate-r2/release-authority-policy.json` | 2523 | `06dbd04f1ae38a1255e3ce0ec783bc896a5e0ea993fdaf0f298fe4bdbe0d7020` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,releaseSupervisor,historicalStageSupervisor,takeover,takeoverParent,releaseReceiptPath |
| `work/P26-r34-release-gate-r2/result.json` | 3639 | `f1bb135316170b08dd2fa2a1fb6b7b83168aebdcf8ccde306ef5d73d266e16e2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,onlyWrittenToolRoot,goal,finding,previousTool |
| `work/P26-r34-release-gate-r2/source-rebindings.json` | 4857 | `6225465685253d83becfeb453065706fc6831cdb9c7c53a3c05be2dc07b19e87` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,pairs,newEvidenceValidator,previousCompleteSeal,finding |
| `work/P26-r34-release-gate-r2/synthetic-tests.json` | 175439 | `619ab71457f5ba3aadcd2846d212d5f92270251a5d2acd178d42f819e8c6d2ee` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,cases,passed,actualProcesses,canonicalFixturesCreated,receiptsCreated |
| `work/P26-r34-release-gate-r2/tool-source-seal.json` | 8680 | `5bbde28bc03eef1e99453bedd92893ec7e5a4c4545023a9e0bbaef92dd391ec6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,externalDependencies,executionAuthorized |
| `work/P26-r34-release-gate-r2/verification.json` | 494 | `3cded81d89f9d6db918e10640204cbc9e5d45ba1d68e016707bb754087fb8a18` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,syntheticCases,historicalReferencePins,actualInnerOperations,actualOuterMarkers |
| `work/P26-release-review-r1/HANDOFF.md` | 4490 | `991022c4ab343d273535850cf65cb07240c773679ee63c18b0163be220a28f7a` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P26-release-review-r1/artifact-manifest.json` | 24270 | `4578e7b81a77a9336d2043b40fde92675a9902a0183b68d4b05d8c2e14b5f350` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P26-release-review-r1/findings.json` | 4403 | `330c1e953e1f6bba06d00b2a3abab5ed16409e58e87f2fb1729b36dfd6738e35` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(1);keys=id,priority,status,title,source,trigger |
| `work/P26-release-review-r1/generator-inner-evidence-finding.json` | 1723 | `650c5d3e957e5cf8dbeadfcdeb1108804130864bf9ff56fa714142857c6b2f95` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=id,status,source,completedContextHasInnerEvidenceValidation,completedContextHasNestedCommandValidation,unchangedReferencePin |
| `work/P26-release-review-r1/new-tool-comparison.json` | 2507 | `df1374be5f71639c820b4d6c0ad8da0b7f57219bedd6765c2139df7bea5ed020` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=artifactSeal,artifactFiles,toolSourceSeal,toolFiles,externalDependencies,controller |
| `work/P26-release-review-r1/new-tool-source-sha256.json` | 6926 | `57b48522c8e6f6be3c5609e223a9d22c34a7be7cb50a477e8fa3d14ec11660ba` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,scope |
| `work/P26-release-review-r1/new-tool-tests.json` | 7686 | `f6ae2c07232f0cd384847181504927da57ef134e443c6f148a121492888a0102` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=cases,scope,packageInvocations,sharedWrites,actualProcesses,innerEvidenceGap |
| `work/P26-release-review-r1/original-contract-review.json` | 1140 | `090f3c356e901f07453da57be3f3dd86f845506c8a820a7c8a1162bac727b8ba` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,controller,guardCount,syntheticCases,caseScope |
| `work/P26-release-review-r1/original-guard-coverage.json` | 7954 | `26cb36c6095d7e1a0dd36ae80833b5e05a2a3476ee9b37fce5cd527514de115c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,guards,dependencies,staticMutationAudit,assetMetadata |
| `work/P26-release-review-r1/progress.json` | 929 | `74600b1bf7ade25378ccd232336cfdf87f558b629fee6dba9ea0c70385037785` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,turnId,utc,step,goal,read |
| `work/P26-release-review-r1/release-contract.json` | 6282 | `0a698fba33d5a2fc550ee919b8cdf29dddc1751a7e4727a606b8e75843e65469` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,originalController,originalVerifier,scope,rows,threeSeparateGates |
| `work/P26-release-review-r1/result.json` | 2152 | `d2bcc80b110c2517cb6a6a6c2bd28dcf980c0d76e9b139845741785410b3cffb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,taskId,turnId,utc,status,decision |
| `work/P26-release-review-r1/source-sha256.json` | 2472 | `9ba31a21d8508abcdcaa5b75de80bd457b5e00186928b9d1ceb11aac0ac37425` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,scope |
| `work/P26-release-review-r1/synthetic-tests.json` | 16094 | `cde28662ee5b19e4db1235d168151a2406b83742603012aba8a988a44c2415e2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,cases,productExecutions,packageInvocations,realAssetLinks,realSubprocesses |
| `work/P26-release-review-r1/tool-review.json` | 1918 | `a784c5050fb013973dd3794d5ec7c9bee78447b8b528f99211a851a5c8b36039` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,artifactSeal,toolSourceSeal,controller,generator |
| `work/P26-release-review-r2/HANDOFF.md` | 3490 | `c1fcc1d028161583a9f02d957a09b6f62c8710e87d435154bbc89edd534e67bc` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P26-release-review-r2/artifact-manifest.json` | 3729 | `2b10325adcc5b108b6ff94e9224246251592e15c84bcd110ca8a5d6199e75189` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,excluded |
| `work/P26-release-review-r2/final-source-sha256.json` | 175592 | `c832ba82cf8701c37eb999163bffe183887af2262214621f5c828b72315d20ce` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P26-release-review-r2/final-verification.json` | 3809 | `9379eb093f2b8c5184e66c7a9f52814b8e54c11807521f77c05f281bd52e9a7e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisorThread,exactSupervisorPins,exactSupervisorPinsMatched,artifactFilesVerified |
| `work/P26-release-review-r2/progress.json` | 457 | `b7b69b69d45e16b78ceb8ce683b94e58f965f3a2b51bb6e076be307a19c85a04` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,utc,step,releaseToolAccepted,unpassed,nextGate |
| `work/P26-release-review-r2/provisional-review.json` | 27514 | `0b9022624701d51b4e9860c5fb4dfc94037029affab7e125b31177b1a9b8912b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,releaseToolAccepted,snapshotPins,cases,caseCount |
| `work/P26-release-review-r2/result.json` | 1622 | `77f57be3e136cb216646040aede76c56e115dfbcff890cafd828e409ac21b50d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,taskId,turnId,utc,status,releaseToolAccepted |
| `work/P26-release-review-r2/review.json` | 853 | `af3a2f9108b7c8c21eb0a9b6bec31a829a1683fa4dedeb50b8b929600c99d19e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,releaseToolAccepted,RG1,exactEvidence,independentEvidence |
| `work/P26-release-review-r2/snapshot-pins.json` | 4844 | `9637f1e5b1dca6ee4aae58e0662ad9d82937def03281fda2f50c6671f43f4ce8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files,capturedUtc |
| `work/P27-ui-input-candidate-r1/HANDOFF.md` | 3570 | `d596dce4fe97a0ef150909a7f1035fb08f3ad29677d9d69b47d72e188e6ff4a4` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P27-ui-input-candidate-r1/artifact-pins.json` | 3173 | `87a1a42e3d1f4fda3b76a5cebc238069037f2928e99e2324045c541986a125c2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,excludedMutable,files |
| `work/P27-ui-input-candidate-r1/base-pins.json` | 28906 | `d716dcbe30b9aebfdc558f59addf4910fe94e026aec31ae7823572a40a1ffced` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,release,files |
| `work/P27-ui-input-candidate-r1/baseline-verification.json` | 770 | `47ce40f0ed28625eea527d9fc5c48612a5253ebeb80f0d154fbc2fb7908d2837` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,identity,currentSourceMatchesBaseline,all171MatchR33Seal |
| `work/P27-ui-input-candidate-r1/changed-file-pins.json` | 851 | `77e42810162bc7c2f3b6cacd372eb118ced8f55bd580e2db2327cc1925561b4f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,baseline,changedProductFiles,candidateFiles,patch,reason |
| `work/P27-ui-input-candidate-r1/evidence-csharp.json` | 1371 | `5cb7f93a446f52951a60edeb67f530be4f68e334e6081a9e634e4a9835007671` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,evidenceClass,cases,assertions,details,productDllSha256 |
| `work/P27-ui-input-candidate-r1/evidence-web.json` | 1438 | `6dece7c1a8fce069cd480d502680830775897548f81e612181db73680ae47b4d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,evidenceClass,cases,assertions,details,sources |
| `work/P27-ui-input-candidate-r1/gap-evidence.json` | 13640 | `486014e8b9563cf45d95097b2b0d1ee0f5ffae065ba0b0e39178da9590163d7c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,identity,reviewConclusion,confirmedImplementationDefects,productChanges |
| `work/P27-ui-input-candidate-r1/historical-focus-evidence.json` | 3598 | `c7aee6e46b8ade5615d95691a089579dfb8c5ae8b56ec4eac5f16e6fa2aa5c2e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,source,sourceUtc,sourceResult,scope,pinsVerified |
| `work/P27-ui-input-candidate-r1/input-routing-map.json` | 20552 | `4cf2988939018bacda5a78a308617119cdd23c994df3629071c9ccaaff160693` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,identity,routes,pickupBoundary |
| `work/P27-ui-input-candidate-r1/native-acceptance-plan.json` | 9413 | `951c898913fb468c21a67284960c960301ac521e6a56361b9c74ac46266116b4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,identity,executionAuthorizedByThisDocument,executed,owner |
| `work/P27-ui-input-candidate-r1/progress.json` | 2083 | `41fee1a95756047a8589ae9827673fbc55205752b8c27a99148de430725a161d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,threadId,threadIdSource,turnId,supervisor,step |
| `work/P27-ui-input-candidate-r1/result.json` | 2521 | `4f0b0cd56cbf2e40c33fea6d108ce10f75cb6ce0931015007583bbfa033b6574` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,identity,goal,confirmedImplementationDefects |
| `work/P27-ui-input-candidate-r1/source-pins.json` | 6222 | `e171feb9e4ae3bf37c5be08a0c1913947b5911279ccf5dd4ac4051d799f0bf97` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,baseline,reviewedFiles,scope |
| `work/P27-ui-input-candidate-r1/verification.json` | 308 | `cb164f8720bdbe885e95aa8bb2b7d353bf45032f6523a1eccb3c274415a2ae67` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,basePins,artifactPins,sourceRefs,routingAnchorsVerified |
| `work/P28-pickup-profiles-candidate-r1/HANDOFF.md` | 5479 | `8c49057e6436a5043765a35246aeddf4db5b34b95171f75460c3d3ba14a2c0ec` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P28-pickup-profiles-candidate-r1/REPRODUCE.md` | 2826 | `b2976259e1729ad8fc3b7a1f106469a0b16d15cd4fd4f73d67055fcb06b3ddf7` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P28-pickup-profiles-candidate-r1/artifact-manifest.json` | 214902 | `3abd6ac7246f3ffbccc923e46611a37d91e2e7d6a331697661677b5bb4488eee` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,files,excluded,exclusionReason,candidateDllSha256 |
| `work/P28-pickup-profiles-candidate-r1/attempt-resolution.json` | 1058 | `9018730040c74f153c4b013765f7923bbc3cdc1d3810b67955a85209939478c4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=failedAttempt,failedExitCode,failedAssertion,cause,rawFailure,repair |
| `work/P28-pickup-profiles-candidate-r1/base-source-verification.json` | 47129 | `9e6547758a94c1cdac3f2d4cd274916e32b0c0b7f7bfb8ddfc692bd4b04bd278` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,comparisons,passed,baseline,filesVerified |
| `work/P28-pickup-profiles-candidate-r1/change-manifest.json` | 2654 | `8dbc43ec1b26811e87962d08267cfc11329f822f614404ef9df241835b509a8e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,schedulerBodyUnchanged,selectorBodyUnchanged,audioDllUnchanged |
| `work/P28-pickup-profiles-candidate-r1/control-comparison.json` | 14902 | `a984027377e5c507c0a2b1a5506f1679afcb8e6c536b5a9a47d2c7fb1db26701` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=candidateDllSha256,baselineDllSha256,baselineRuntimeAssertions,byteIdenticalPngs,native |
| `work/P28-pickup-profiles-candidate-r1/coverage.json` | 4183 | `90bab652e6691a076d10373b077343064389a3b2b361d75df5c7e963b4b161fc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=candidateDllSha256,families,notClaimed,fixtureModel |
| `work/P28-pickup-profiles-candidate-r1/final-input-audit.json` | 172601 | `de0d722e27054a34d3fb75b1e79f678385db8ffe2c7c5594c6fa0fd3cdf1a4fe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=engineAndSealedSourceComparisons,prepInputPins,unresolvedDrift,candidateDllSha256,sourceFiles,changedFiles |
| `work/P28-pickup-profiles-candidate-r1/helper-adaptations.json` | 614 | `3b48d3e469d7f13327357a513b8c01300942d252077a1438736170087f4d2a62` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,changes,native |
| `work/P28-pickup-profiles-candidate-r1/implementation-scope.json` | 1902 | `24cac7b5ebaede028c7b346f96a4a5ed90d9c45649b6f6e1cd0cf50ccd88c67f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=candidateId,changed,added,exactPairs,schedulerChanged,formalRelease |
| `work/P28-pickup-profiles-candidate-r1/input-pins.json` | 232882 | `c0161867899f36fa7d9c48bce5c8b6b95ade896594818b48cdbedd1e53311b39` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P28-pickup-profiles-candidate-r1/progress.json` | 1978 | `31b3950172631e7acf1fdff910e8709339d6882608bc389b0c0b3aabebebdfda` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,read,baseSourceFiles,independentCandidateSources,resourceSha256,goal |
| `work/P28-pickup-profiles-candidate-r1/resource-generation.json` | 484 | `fef69a397827ebc4029c4b7f3902b6d06362042b6f9594fa22f2ab25a39299b8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=recipe,method,size,sha256,staticMappingAssertions,native |
| `work/P28-pickup-profiles-candidate-r1/result.json` | 9463 | `12795e85da42121cd312d84f608760195ce47e28bdf751fa451037b853f83e2d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,status,utc,supervisor,soleSharedIntegrator |
| `work/P28-pickup-profiles-candidate-r1/source-manifest.json` | 32302 | `18aecf0d68455b5561637ca23f62d33228ef4b451be0e66602f07995daee49a9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,files,baseSourceManifest,unchangedNotCopied,note |
| `work/P29-pickup-profiles-review-r1/HANDOFF.md` | 6259 | `a68101cab2d52e22e5008f614e0a0a3842d2f655d32d205c814508ea07e99072` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P29-pickup-profiles-review-r1/artifact-manifest.json` | 14145 | `804f0c2b469db84248dc01e1570b23f5779e0a043d28b757282ebe4025d7b2fb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,selfExcluded,excludedDirectories,candidateBound,candidateRuntimeAssertions |
| `work/P29-pickup-profiles-review-r1/coverage-gaps.json` | 3839 | `7d7b93939cd8bea09e4606b941cbf7ac4fb67cd9c92efe2fcaca1c588e794ebf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateBound,runtimeAssertions,plannedSlotsAreNotExecuted,families |
| `work/P29-pickup-profiles-review-r1/dispatch.example.json` | 781 | `e7ebf8517c163e55be2a26370d3ada58451552fc627401c34a8fafe0392f1793` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,reviewRoot,baselineSha256,authorization,candidateRoot |
| `work/P29-pickup-profiles-review-r1/progress.json` | 853 | `4869dbf8e7527f192d5c0b14f033dff5dd5c98672d20e8a2895d23787efba0b5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=phase,goal,readDocuments,steps,dependencies,candidateRuntimePassed |
| `work/P29-pickup-profiles-review-r1/result.json` | 3286 | `5ab639f74e0a9a548c4630bdb43dfc74fdeb552acb2c6d3e020ab1cced12fdc4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,owner,supervisor,baselineSha256,goalStatus |
| `work/P29-pickup-profiles-review-r1/source-manifest.json` | 160201 | `1e1e0c6ec490ef39086b5f8270097083801d08b33821174d13cc5346cb50111e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,historicalPinsRecomputedAsSuccess,notes,historicalAdapterReferences |
| `work/P30-pickup-profiles-execution-r1/HANDOFF.md` | 3065 | `973717b8861e29619bfbea6ca99660ee33fad7f656d6e00e6f87572786b68a05` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P30-pickup-profiles-execution-r1/P30-executor-root-verification.json` | 800 | `416ff7209b6b2bb2c9255d8d0c080a2deb35800487ca5d3494de992099a28b68` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,sealSha256,artifactRows,argv,exitCode,stdoutSha256 |
| `work/P30-pickup-profiles-execution-r1/P30-product-only-execution-acceptance.json` | 3231 | `240ce19f1cd8f10bddff4a34366eacef31598d36b7bce0130a0cb83a431fe541` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,supervisor,owner,dispatchSha256,contractSha256 |
| `work/P30-pickup-profiles-execution-r1/P30-source-delta-root-review.json` | 832 | `46c1cdc4af83810c5ebef62c25bc159da9190299c9314d715e106cd1b8be4b4a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,baseSourceFiles,finalSourceFiles,unchangedFiles,unchangedWebFiles |
| `work/P30-pickup-profiles-execution-r1/artifact-manifest.json` | 6101 | `5943506917f72f43edf2eda3659f97a4e5c07c8e2e1f690ad4bd8a828f44c7f2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,root,files,excluded,status |
| `work/P30-pickup-profiles-execution-r1/artifact-seal.json` | 6181 | `48ca6f305da7eaa037b03440214658370ee14799dd9a2185ad491d56db3b944b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,root,files,fullSHA,selfExcluded |
| `work/P30-pickup-profiles-execution-r1/before-state.json` | 2231 | `629a197816369f319b389f7840fdba2b2f87324f6efc39f91f6c1a495cee4d82` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=protectedPins,planningSeal,executorSeal,contract,receipt,review |
| `work/P30-pickup-profiles-execution-r1/execution-contract.json` | 9434 | `37af732d7d30629d71cd3fb8e66bc18d77352bbef2ff3191de920743ced4a2d9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchSha256,owner,supervisor,executorRoot,futureRoot |
| `work/P30-pickup-profiles-execution-r1/executor.command.json` | 1744 | `fe9442c62a1ae194f59e8c4b7457d3c926f314ac6dffd843c54579fc57d0396a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=argv,cwd,startedUtc,invocations,timeoutSeconds,completed |
| `work/P30-pickup-profiles-execution-r1/failure-assessment.json` | 3752 | `6662511ae3dff08ab2528632163290f91abda67c9c866032a8befd593d619eb4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,executorInvocations,outerExitCode,dotnetBuildInvocations,dotnetExitCode |
| `work/P30-pickup-profiles-execution-r1/failure-log-excerpts.json` | 7057 | `3c3d733b1f2457a152f0bcdcc472a1c27c6159d33c31e44d55b5d63cba35b8e5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=raw,finalSummaryStartLine,excerpts |
| `work/P30-pickup-profiles-execution-r1/input-copy-pins.json` | 3190 | `9d991b0cdb57f729850888e5d619c7e876a0a69d6cccc261c175b90ea93e4ae0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(6);keys=source,copy |
| `work/P30-pickup-profiles-execution-r1/invocation-schema.json` | 3260 | `45914acba14320056e31661cc557bf5912788afefc04b15915c8403902028391` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,receiptRequiredPath,receiptRequiredConstants,receiptAdditionalRequired,expectedReceiptSha,validateArgv |
| `work/P30-pickup-profiles-execution-r1/result.json` | 4525 | `14257ec69cc88af0328f1277e2efea452592a0340e8d46448cb3bcaf06aa87fb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,executorInvocations,outerExitCode,dotnetBuildInvocations,dotnetExitCode |
| `work/P30-pickup-profiles-execution-r1/review-artifact-manifest.json` | 2393 | `4e9a0ec1b180788dbc1c08aef1bb5d9806d4b30c39568fa95d5965ad25ac443a` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P30-pickup-profiles-execution-r1/stage-evidence-copy-pins.json` | 7661 | `b698119ee5c9ac25c0a3250161495c6b1fbc75c7a1a68c257c335af0c6e0c522` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files |
| `work/P30-pickup-profiles-execution-r1/stage-files-seal.json` | 33244 | `174480157bdfcbdb26c76f5f1313c5dc3f296543bbf65f678411cdf58789807c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,root,files,fullSHA,status,writtenOutsideStage |
| `work/P30-pickup-profiles-execution-r1/started.json` | 839 | `556bd2dbede6a075515648a6b52483285ca8217101af05996aa854e2811394f6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=argv,cwd,startedUtc,invocations,timeoutSeconds,completed |
| `work/P30-pickup-profiles-execution-r2/HANDOFF.md` | 2938 | `0f5bc21e9612399f50e6ea9e6ae455311f0f1f003eeb23b1c1324baad3c67437` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P30-pickup-profiles-execution-r2/P30-environment-diagnosis-acceptance.json` | 702 | `fb2d85e5a0dd63c18f7a8daf051485a3d88fd7c0b87900c3038e9597ea290048` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,decision,reviewSeal,artifactRows,minimumFixedEnv,newBuildAuthorizedByThisRecord |
| `work/P30-pickup-profiles-execution-r2/P30-product-only-execution-acceptance-r2.json` | 3002 | `c6d74ab2020063d2ea432ca35efb3b5ebf42ce79c0296cc7bc050b363ecd2707` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,supervisor,owner,dispatchSha256,contractSha256 |
| `work/P30-pickup-profiles-execution-r2/P30-r2-root-diff-acceptance.json` | 1494 | `bbda39be4b2f8fb6fa47313dba4d767d4e7adde7aaed90d79c47f19fbc96efda` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,decision,seal,artifactsVerified,contract,unchangedRuntimeSources |
| `work/P30-pickup-profiles-execution-r2/artifact-manifest.json` | 7194 | `98c4333bb4fca38fdefc25e71aee8b8aa9cbfdc41bd3c71c8774a5f1de82613b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,root,files,excluded |
| `work/P30-pickup-profiles-execution-r2/artifact-seal.json` | 7316 | `2ceaf7801cc52d85a3abd7d9d331cd7298c2da754d72de9578c2eba4fc8490bd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,root,files,fullSHA,selfExcluded |
| `work/P30-pickup-profiles-execution-r2/before-state.json` | 2443 | `46bb2c37addeb909051160a769aca5032b41ca4b3c100c54a536b2623466d58b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=protectedPins,seals,failedStageSeal,receipt,contract,future02Absent |
| `work/P30-pickup-profiles-execution-r2/compiler-provenance.json` | 2244 | `37de97fa9830a08c63b1d7d5ed608edca83298ea9db71cd9eae650773758022c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=actualCscTasks,count,summaryStartLine,finalSummary,binlog,productBuildInvocations |
| `work/P30-pickup-profiles-execution-r2/execution-contract.json` | 9492 | `3f4bf1a3e6ec5f9db90ed8583f14d0f53869ad0c2e98ad3fc20ba6427d86b75d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchSha256,owner,supervisor,executorRoot,futureRoot |
| `work/P30-pickup-profiles-execution-r2/executor.command.json` | 1745 | `2a3e0e875872106cae63e7931456ea9163e99e9e79f6d07e67e442a2c527ea7b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=argv,cwd,startedUtc,invocations,timeoutSeconds,exitCode |
| `work/P30-pickup-profiles-execution-r2/input-copy-pins.json` | 2653 | `17ed3fff39fdd3adaccffc978f6751842abefb25e2ab463e718c1080fb216908` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(5);keys=source,copy |
| `work/P30-pickup-profiles-execution-r2/invocation-schema.json` | 3266 | `48f5b368f5e195dfc379a084a42bdfb10886fbee3499f17f64b05730aabb7880` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,receiptRequiredPath,receiptRequiredConstants,receiptAdditionalRequired,expectedReceiptSha,validateArgv |
| `work/P30-pickup-profiles-execution-r2/output-dependencies.json` | 21120 | `a08518219bcded6c71917d5147ec34b661c7566e09d311fd85ecc99776281547` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=outputFiles,count,actualDeps,libraries,dllComparisons,generatedIntermediateFiles |
| `work/P30-pickup-profiles-execution-r2/preservation.json` | 1557 | `9dd427fe141001dd722c1104fd03803352f0413fd62ed07fd5df01d6e386f27b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=roots,protectedPins,protectedPinsUnchanged,formalEntry |
| `work/P30-pickup-profiles-execution-r2/result.json` | 3823 | `4150bfa1cd36c6495e977682dd548a37a113e77aee3696c34c677cc98ee5a734` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,dispatchId,receipt,contract,outerCommand |
| `work/P30-pickup-profiles-execution-r2/stage-evidence-copy-pins.json` | 10135 | `027e15d48b991a3316bd1391c005601f1bd03f1d3d12274f72bee2dff1cf5263` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files |
| `work/P30-pickup-profiles-execution-r2/stage-files-seal.json` | 65164 | `45bf24321192718e090deca882ce1ce884113cb9bfae817b73ab92892d3f5514` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,root,files,fullSHA,writtenOutsideStage,status |
| `work/P30-pickup-profiles-execution-r2/started.json` | 842 | `33fb51096bc87bfe4024e7208fb854e5c13a09e75e78f45c24ab785c66412a7d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=argv,cwd,startedUtc,invocations,timeoutSeconds,exitCode |
| `work/P30-pickup-profiles-executor-r1/HANDOFF.md` | 8058 | `139b7d11e9790dbf8128ee074a9df49e1e240fefdf8faaab69919fb156ad258f` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P30-pickup-profiles-executor-r1/artifact-manifest.json` | 10784 | `44846a94a6c1a2f725acd48ef9fac33425ce5336a9c68a83436bdc73c27dc116` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,root,files,excluded,productBuilds,runtimeProbes |
| `work/P30-pickup-profiles-executor-r1/artifact-seal.json` | 10861 | `32413f1b4693325cddf8f5c2ac189902d09b5bc969829f43f062aba46638a50f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,root,files,fullSHA,selfExcluded |
| `work/P30-pickup-profiles-executor-r1/build-dependency-pins.json` | 1126895 | `58fd3d9a4de20417a2336c00f99494a7e4874352c470676cdbdfefa3763b116b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,roots,processesInvoked |
| `work/P30-pickup-profiles-executor-r1/copy-pins.json` | 3848 | `480fe8ba3f66b7c63b271a2363f55b85afa3ebedf645b056d6e2f8c5ea187ce0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files |
| `work/P30-pickup-profiles-executor-r1/execution-contract.json` | 9434 | `37af732d7d30629d71cd3fb8e66bc18d77352bbef2ff3191de920743ced4a2d9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchSha256,owner,supervisor,executorRoot,futureRoot |
| `work/P30-pickup-profiles-executor-r1/invocation-schema.json` | 3260 | `45914acba14320056e31661cc557bf5912788afefc04b15915c8403902028391` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,receiptRequiredPath,receiptRequiredConstants,receiptAdditionalRequired,expectedReceiptSha,validateArgv |
| `work/P30-pickup-profiles-executor-r1/preparation-evidence.json` | 833 | `3c7c797125a8894a755b7c94a5c985623c637a1aed71048604927dd4f9db145f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,dispatch,planningSeal,planningArtifactsVerified,goal,productBuilds |
| `work/P30-pickup-profiles-executor-r1/product-materialization.json` | 92977 | `63290a5c650dd992172d605210ca882b8212e1622f98ec000310f68e590c4f33` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,sourceCount,count,originalManifest |
| `work/P30-pickup-profiles-executor-r1/result.json` | 3693 | `9d2582af77fcf1a55250ff02d0005d7e890a72d1c0feaa6a01ed5d1aaa0343ab` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,dispatchId,utc,contract,sources |
| `work/P30-pickup-profiles-executor-r1/thread-snapshot.json` | 2117 | `59caae3d16030fb8dd6612cca9445b61f869821ded8b2395922e9076a9b5e41c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,threads,scope |
| `work/P30-pickup-profiles-executor-r1/validation-result.json` | 16647 | `1a0d680d6fde959dea110e1cc9ed260a2167d0b45191e8eeeb17b7d39013b0a6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,cases,count,productBuilds,candidateRuntimeProbes,syntheticPythonProcesses |
| `work/P30-pickup-profiles-executor-r2/HANDOFF.md` | 5596 | `dd29a40f394cb28522a9c38e984ed27e193bcc951277bfd648608896904d76d5` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P30-pickup-profiles-executor-r2/artifact-manifest.json` | 6187 | `455190462ebb37e86d0e2ea09fcf374b05bb98594302287e4f0aa2bd7e16ed87` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,root,files,excluded |
| `work/P30-pickup-profiles-executor-r2/artifact-seal.json` | 6309 | `030f92adce8b04fe01a864055cd28f90a3f95e12e3da83add167daec79c57ee3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,root,files,fullSHA,selfExcluded |
| `work/P30-pickup-profiles-executor-r2/build-dependency-pins.json` | 1126895 | `58fd3d9a4de20417a2336c00f99494a7e4874352c470676cdbdfefa3763b116b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,roots,processesInvoked |
| `work/P30-pickup-profiles-executor-r2/copy-before-edit.json` | 10337 | `b480e927e6483f1c9ed385370b1b8838bb79f784f7ceaa338bbbf247507c6706` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,note |
| `work/P30-pickup-profiles-executor-r2/delta-validation.json` | 4359 | `81d0f41073d72edceb3703ec6c33269b0a48ae7544fab4bbd8b41d86f34af1ca` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,checks,count,productBuilds,runtimeProbes,jobSyntheticTestsRerun |
| `work/P30-pickup-profiles-executor-r2/execution-contract.json` | 9492 | `3f4bf1a3e6ec5f9db90ed8583f14d0f53869ad0c2e98ad3fc20ba6427d86b75d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchSha256,owner,supervisor,executorRoot,futureRoot |
| `work/P30-pickup-profiles-executor-r2/final-preservation.json` | 1587 | `be1e8045fd98c104ea2575c97d76578060afe40f59edea7f43f154d6003a0570` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=roots,protectedPinsUnchanged,futureStage02Exists,systemPathState |
| `work/P30-pickup-profiles-executor-r2/invocation-schema.json` | 3266 | `48f5b368f5e195dfc379a084a42bdfb10886fbee3499f17f64b05730aabb7880` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,receiptRequiredPath,receiptRequiredConstants,receiptAdditionalRequired,expectedReceiptSha,validateArgv |
| `work/P30-pickup-profiles-executor-r2/nuget-system-path-state.json` | 423 | `5e989d17c25dd4c249c11122d3b1309976457d37fc2021b8029fd09dbfc7be77` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=fixedProgramFilesX86,exists,nugetBaseExists,defaultsPath,defaultsExists,machineConfigDirectory |
| `work/P30-pickup-profiles-executor-r2/preserved-inputs.json` | 5148 | `f5db920ace3b62d5263bd5b32cb5b0e76f8e36055c4addf34a5e36bd7e3a7f5e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=dispatch,immutableInputs,failurePins,diagnosticPins,planningSeal,failedStageSeal |
| `work/P30-pickup-profiles-executor-r2/product-materialization.json` | 92977 | `b5895020b5416b704b46348a05818154deb4c67759c922e78af2a466149858da` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,sourceCount,count,originalManifest |
| `work/P30-pickup-profiles-executor-r2/result.json` | 4274 | `b68f561c3ed2ff9cc5d7a896685cabd8777897c4c4dcc73292915356b2d35a16` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,dispatchId,utc,contract,scripts |
| `work/P30-pickup-profiles-executor-r2/source-diff-ledger.json` | 2696 | `7144cb8879e13a544afc77e202ea41c1495bd50fbf6a7aef89f2e6af497c19de` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=changes,semanticChanges,executorUnchanged,winProcessUnchanged,dependencyFileUnchanged |
| `work/P30-pickup-profiles-executor-r2/validate.command.json` | 1106 | `3aaed0dd353c8ed91c387101fba0f180d4b668cd6b59084c76758eae523d2ce5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=argv,cwd,exitCode,stdout,stderr,kind |
| `work/P30-pickup-profiles-stage-r1/EXECUTION-CONTRACT.md` | 6558 | `917dd1fba6103654104d8bb2ab03b9517c4f134202759c479e3e4ab500ee5341` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P30-pickup-profiles-stage-r1/HANDOFF.md` | 6954 | `b8bba54635e19158df4a9df92c0eab8f5251d6fac7ae8dbfb2675b456e51ea44` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P30-pickup-profiles-stage-r1/artifact-manifest.json` | 108060 | `a9f391d25a21cdd778d3e1a42f1621a51d709c558fad6bc2c8c919e2e2c3da7f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,root,files,excluded,runtimeExecuted |
| `work/P30-pickup-profiles-stage-r1/artifact-seal.json` | 108201 | `3b5e84c46f48c01b80a129caad493b4218a8151bec8f0d9051b687501764637c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,root,files,fullSHA,selfExcluded,productBuilds |
| `work/P30-pickup-profiles-stage-r1/build-dependencies.json` | 113115 | `2949a2454a61aa01c0567a990eecc1d2402e1cf32ecaebc59131b557b3c0dba0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk,targetFramework,productAssembly,projectReference,packageReference,files |
| `work/P30-pickup-profiles-stage-r1/command-plan.json` | 67688 | `9d10a24f4b0345c7f03b9056602f1b2f160d6ff78283b022ded12891fc5151bc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,operations,nativeSuites,executionAuthorized |
| `work/P30-pickup-profiles-stage-r1/copy-pins.json` | 242626 | `0afafc19348e57c440dba15fd9f32b95706cd6e706c2f672189cb1ba57c76109` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,independentCopies |
| `work/P30-pickup-profiles-stage-r1/coverage-matrix.json` | 62717 | `03d6b4b2bce2c4766131d3b4a1ee5eb9e24794ffa2a41a99b09d7b28be74d876` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=families,countsOverlap,newRuntimeAssertions,acceptance,additionalRequired |
| `work/P30-pickup-profiles-stage-r1/fixture-inventory.json` | 33779 | `9d4c30508b8fde9f6886363ee2e5d619ad7d7431a4e0464a7f8ccd48d1fe01f5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=inherited,profileOracle,baselineSetup,assetWrite |
| `work/P30-pickup-profiles-stage-r1/harness-rebinding.json` | 7614 | `eeedb79a9718d010319097f27c922a6697223166ddae768b86fbdddd3af70aff` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scan,rules,executionPrerequisites,preparedDiffs,occurrenceLedger |
| `work/P30-pickup-profiles-stage-r1/inherited-helper-inventory.json` | 34568 | `2407a69344d80fb02f99ba725bf3a5d3b97998bce34e2732f10fe4d9b28be119` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,count,status |
| `work/P30-pickup-profiles-stage-r1/input-pins.json` | 679757 | `7adce041906122c9e8c6be30231dc2a15c7c7169816cc58f9950d0a333025200` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P30-pickup-profiles-stage-r1/input-verification.json` | 1272 | `906580795aa9591698a70fad701236ae63454a576e46a783d0f4d7dbb9146206` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,audits,dispatchVerified,sourceReconciled,goal,notes |
| `work/P30-pickup-profiles-stage-r1/materialization-plan.json` | 152598 | `de67b00de533abb45e196f3c0e97d245c36ed1e9c7312e9b8badf72fdcc64050` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=executionAuthorized,executorImplemented,productOnlyFiles,files,assetsCreatedNow,laterAssetStrategy |
| `work/P30-pickup-profiles-stage-r1/occurrence-migration.json` | 3029 | `1c8bb58d3d541754411ce26d2c006287e0488d03cc4967e9cbbabf9f393c2f6e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=rows,assertionBooleanBodiesChanged,cancellationMethodChanged,expectedDerivedFromObservedOutput,historical174Plus10RetainedAsH |
| `work/P30-pickup-profiles-stage-r1/prepared-helper-manifest.json` | 37283 | `ea2a82143f179b797cfd9b9db797ea929f672ee1db44a0ccabc64b3abf81e95f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,reviewRequired,builds |
| `work/P30-pickup-profiles-stage-r1/product-only-spec.json` | 7316 | `96732bd48ea76890ce5bf82bf5613a7f13b16c882997ae2fdcded5f674de3086` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,authority,executionAuthorizedNow,executorImplemented,availableControllerModes,missingExecutor |
| `work/P30-pickup-profiles-stage-r1/profile-command-plan.json` | 9493 | `74447b1bb05b9fc0724da3babaff306eb94934e3c0080cfa91c0666ab95164cc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=operations,executionAuthorized,baselineControl |
| `work/P30-pickup-profiles-stage-r1/read-only-validation.json` | 4232 | `0dbfa64302470c5e5716dc9421186e1956ab1e98076ab533d269797d12e5d6a7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,validatorResult,planningChecks,planningCheckCount,productBuilds,runtimeProbes |
| `work/P30-pickup-profiles-stage-r1/refinement-receipt.json` | 387 | `6d4bccc58e377618158fca719dad5c4e97ac7c034bfc777887d3eb1423e2d7a0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,pathFix,preparedHelpers,helperDiffs,migrationRows,fixtures |
| `work/P30-pickup-profiles-stage-r1/regression-inventory.json` | 9394 | `6b12790b3ca4f3630824979ab38135b70b132559b8568ce152a82cefe8fda7d3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=inheritedSteps,historicalTotalAssertions,historicalObservations,newExecutedAssertions,profileSuites,p29 |
| `work/P30-pickup-profiles-stage-r1/result.json` | 3277 | `2d303c43417d4e12c8f6ef13d31e6bfa5d6f82325edcc6bf4df6fdb2ba1b2dae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,dispatchId,utc,plan,controller |
| `work/P30-pickup-profiles-stage-r1/source-inventory.json` | 159530 | `8522287dbcc2c0aca411facf30ed43490a8443b9efe85ff45768251f194bf94c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=baseCount,sourceAudioCount,webCount,finalCount,replacements,additions |
| `work/P30-pickup-profiles-stage-r1/stage-plan.json` | 7441 | `f58e0d6400d1715ad03ef32fc26486ac89a9b915824bbd7470a4ca6dff4f19f3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatch,acceptance,baseline,owner,supervisor |
| `work/P30-pickup-profiles-stage-r1/thread-snapshot.json` | 3639 | `ed73e5cbe8129175be2f8964adb9e71160821d309652fb647d8d4d649b58c8f4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,observedUtc,threads,noCompetingIntegratorObserved,goalActual |
| `work/P31-pickup-composite-review-r1/HANDOFF.md` | 8514 | `cb47e7124b7fcf035f30f18c092b6cb81ed4bf6120545b9127b6da4ef62becf3` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P31-pickup-composite-review-r1/artifact-manifest.json` | 88940 | `7d5fe8e598dad7979c32d5c84707f94c31381ce60b278b7f3b2c0171c194f7cb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,selfExcluded,files |
| `work/P31-pickup-composite-review-r1/cases.json` | 843169 | `98369cbd4fbf437dca9d4262cdd01045a0c97639ce8754d530f3dfc54e34eb50` | U — U14：可重構素材／封包欄位 $.scenarios[].scene[].pixels；真實或合成來源 unknown |
| `work/P31-pickup-composite-review-r1/comparison.json` | 47172 | `e8c6c20635b180bc9e3d7b687552e20af72f58beb98c386ce8341277cfd8cd96` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=uniqueCompositeCases,passed,failed,rows,witnessPixels,pixelAssertionsNotCountedAsCases |
| `work/P31-pickup-composite-review-r1/coverage-delta.json` | 665 | `540bf1961a4d67893c4790106c11e690de62255970978d3a4299898e3a414c4b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=historicalP29Rows,historicalRowsAdded,newTrajectories,newCompositeCases,profiles,directions |
| `work/P31-pickup-composite-review-r1/final-audit.json` | 2624 | `a8f46c69ad0dabad8e28a9b5f239b924b6e73884a4a6a90ff532a42d2802c5b9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sourcePinsUnchanged,copiedDependencies,loadedDll,runtimeOracleArtifacts,note,utc |
| `work/P31-pickup-composite-review-r1/goal-status.json` | 232 | `b112d1f2f22f7b4c5d962fa0e0f4e04c162be880a053618dfa48c344e463016c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=tool,threadId,status,tokensUsed,timeUsedSeconds,createdAt |
| `work/P31-pickup-composite-review-r1/prepare01-diagnosis.json` | 395 | `8661289181899622894bb9b58c0b867fb47a7fb562ca47208ba15be5832dfa0a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=preserved,expectedChanged,exit,failure,productRuntime,attempt |
| `work/P31-pickup-composite-review-r1/progress.json` | 9644 | `fb9576a028d3d5db7686b85a0350157524e6b4787cc7256ac34c30dc54a03614` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=phase,readDocuments,goal,completed,dependencies,remaining |
| `work/P31-pickup-composite-review-r1/result.json` | 1759 | `99b0fa2153a0ebcda0d04c0da2c959f478da35250207479aa918939faa5c7311` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,supervisor,dllSha256,audioSha256,helperBuilds |
| `work/P31-pickup-composite-review-r1/sensitivity.json` | 13272 | `37a6ad45da70de806a9339e113ad236565de4e0c659563f034e41c36bec3d0f2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=classification,method,checks,detected,rows |
| `work/P31-pickup-composite-review-r1/source-manifest.json` | 19914 | `6d5f22a29ad6a84c0bbf54ff6a1400df70b181601a5aa451fc90e8434140745d` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P32-certificate-readonly-r1/HANDOFF.md` | 2799 | `3b158b48ed1297adeb086dfb08ba5b19427a4a69faefe91f45d47b98effb7c75` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P32-certificate-readonly-r1/artifact-manifest.json` | 3638 | `ab4509168c1157e1a6699d3c017f27eecc219196138c7f6d955a369da9a0df2e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,selfExcluded,verificationExcluded |
| `work/P32-certificate-readonly-r1/copy-receipts.json` | 5259 | `09997b6156399cb6033473aa029083bf0f7c7c6b5700f7b68cdaa335ff98ed95` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=originalSealPath,originalSealSha256,files |
| `work/P32-certificate-readonly-r1/later-runner-checks.json` | 909 | `1c03a89a8699546c2607074ae5251125ad9b953f0c3f1573595a1c17e959fd1a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=build,runnerSha256,runnerMatchesCommandSnapshot,certificateFalse,toolsPathFalse,envPassedToSubprocess |
| `work/P32-certificate-readonly-r1/progress.json` | 1751 | `323e45db93c5236460739a8d0d30eeee93731fc3a159b6d513674b70d9109b7c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,readDocuments,dependencies,goalRead,goalChanged |
| `work/P32-certificate-readonly-r1/result.json` | 2953 | `d6c96e377e35ae9dbdea530781cf655d60c23c926a417b51217021eed72c13e5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,identity,matchesSupervisorReportedNameAndSidSuffix,fullSupervisorSidNotProvided,originalBuildIdentity,build01Utc |
| `work/P32-certificate-readonly-r1/store-observation.json` | 2667 | `b80b365b9ffea7f654cb5caab143dc9377c9e7455bb6fb018ff513b95df74883` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=startedUtc,finishedUtc,identity,originalBuildUtc,filter,stores |
| `work/P32-certificate-readonly-r1/verify-delivery.json` | 172 | `52e73fe77b34bfe729597a378a7d039da91cd5133cef206d83ef613fb660ebbf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,artifacts,manifestSha256,originalP32InputsUnchanged |
| `work/P32-pickup-lifecycle-review-r1/HANDOFF.md` | 8102 | `eef016bcdf18ced93d9ee4fd00ed0ed9b86c42ab658f6345083a78e5d6eba36e` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P32-pickup-lifecycle-review-r1/REPRODUCE.md` | 1348 | `eda0ab1d4edf354dcba23f2111753d5360ce889828510b80df9656c2a7c9450a` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P32-pickup-lifecycle-review-r1/artifact-manifest.json` | 36331 | `2668b3754b39af60659063ea096439b872c5417584e5cb2c5f1860924be588db` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P32-pickup-lifecycle-review-r1/asset-post-audit.json` | 1086 | `f6179fd0f98c43f494d3d0cdc88de46758dde6842bf155b138114864b54c329b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=phase,profiles,readOnly,notFullAssetAudit |
| `work/P32-pickup-lifecycle-review-r1/coverage-delta.json` | 1641 | `ce3ca073afc578dcb3529dd6fa0ca39c1a74488b5015d733832925aaee6fa6c3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=priorIndependentRows,historicalRowsAdded,newDistinctCases,families,windowScenarios,windowAgeSamples |
| `work/P32-pickup-lifecycle-review-r1/dimension-audit.json` | 6133 | `7b7f74a6450ebc66c47347bf366f086bcb341aa6bee0397bfa6c306c71b8f170` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=ownerFields,specDimensions,lease,actualOwnerFieldIsolation,specDimensionsExecuted,specDimensionsExcluded |
| `work/P32-pickup-lifecycle-review-r1/distinct-cases.json` | 30046 | `1f7441a85a36009ce045ab2f50e2acb9b80cb9f0d7b97d8a7076d099de3c77fd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=count,cases |
| `work/P32-pickup-lifecycle-review-r1/environment-deviation.json` | 1049 | `f7f4e762aea8efc9fa40ea276b4aa7b0c8847a9a967453a8be92ad6fb0e64269` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=id,phase,observed,limits,cause,containment |
| `work/P32-pickup-lifecycle-review-r1/frame-oracles.json` | 287496 | `11a2e1be30c22af6d96376823e2b086467e6c8b1ba34069538e0e4869f7960bf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(192);keys=slotId,plannedOrdinal,gfx,weapon,sprite,direction |
| `work/P32-pickup-lifecycle-review-r1/goal-readback.json` | 403 | `295263aa54b2ce036ba7acb07d8e220b0211a6f7db9c62f83ba8875e6920c167` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,threadId,initialRead,createdAfterSupervisorExplicitInstruction,status,tokensUsed |
| `work/P32-pickup-lifecycle-review-r1/lifecycle-matrix.json` | 11755 | `679b19e2f978da593731f696128e8eb995a0ae7048ca5c0db6027624a49c906c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(39);keys=id,reason,api,dimension,expected |
| `work/P32-pickup-lifecycle-review-r1/negative-controls.json` | 886 | `376eba2f97e35047e14ab162e6290bd86ff07dda95b8505c641f85563b66f5ff` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=type,controls,controlsAreNotProductCases,originalHelperDiagnosticsRetained |
| `work/P32-pickup-lifecycle-review-r1/progress.json` | 4063 | `2850bc82bf7ae9d808c7ea2630e154b0b7594e9e052f4eafbc1bc04d316d4721` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,readDocuments,goal,dependencies,productBuilds,completedRuns |
| `work/P32-pickup-lifecycle-review-r1/result.json` | 4288 | `34ae72c3fb405c235d686511ad74c8b04da20fe7c2f1e71d4457af3a6ea35aed` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,distinctCases,passed,failed,families |
| `work/P32-pickup-lifecycle-review-r1/source-audit-addendum.json` | 819 | `76d940433788388673e78ef7c6e391ae6dc8f7872ce00b45909e6a13a6087e73` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,readAfterRuntime |
| `work/P32-pickup-lifecycle-review-r1/source-manifest.json` | 7534 | `5da8abc1c9cff363350076047e81e8c16fdf7606f840eaba69fededfb58848cc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,purpose |
| `work/P32-pickup-lifecycle-review-r1/verification.json` | 475 | `8dcd8eb7c581888c88d25adf8527f1ace813d913fc9920954b69a9804a92c24e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,distinctCases,families,originalOracleSealVerified,allReadInputPinsUnchanged,rawCommandStreamsVerified |
| `work/P32-pickup-lifecycle-review-r1/verify-delivery.json` | 134 | `542fe449588636909b7804a685093a0085eb303c55da121b193504c8edfe12a1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,artifacts,manifestSha256 |
| `work/P32-pickup-lifecycle-review-r1/window-oracles.json` | 15942 | `7eaf5d37f4968ba916f6d8d68a5ebe306e0893e0902be13996e380d1e61dbafd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,rows,directions,sampleAges |
| `work/P33-pickup-resource-fault-review-r1/HANDOFF.md` | 8455 | `d6db0653062fa734cb3d1cea1fd2d6c47ba4e47bb93b9bfb1003fecb4d7aeb58` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P33-pickup-resource-fault-review-r1/REPRODUCE.md` | 1349 | `f5926ee71b408d021ef6034a954611efc617d94b696a436164782a67431dcf1c` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P33-pickup-resource-fault-review-r1/artifact-manifest.json` | 16041 | `99564bcea8ca04c90c2ec027a27e8cc0bcb7c68f14d89f9ff3bfc638be8ef661` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,note,files |
| `work/P33-pickup-resource-fault-review-r1/contract.md` | 2430 | `d865eea2074a0c27333e2467404e8b8e5c8676cd61a283610421b61bc53edf0b` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P33-pickup-resource-fault-review-r1/coverage-delta.json` | 1546 | `620b1ade2032d6d3570697499da26e8e37906109ab842befda668b9ec317548c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=prior,newScope,native,historicalRowsAdded,qualification,executedDistinctCases |
| `work/P33-pickup-resource-fault-review-r1/distinct-cases.json` | 4436 | `f39581303b071af1d46cae6a263888731d63b83926a031b223b76527b552ef46` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=cases |
| `work/P33-pickup-resource-fault-review-r1/finish-attempt01-diagnosis.json` | 449 | `87c3f9db8339359b0543abd1f5da2fca9dcc01d4cc56a4df5f47980cce0f2489` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=exitCode,stage,diagnosis,rawStreamCapturedSeparately,toolOutput,productExecutionAffected |
| `work/P33-pickup-resource-fault-review-r1/goal-readback.json` | 530 | `abe172a159574560920ffeb2c6a4efbb29c227442dade1d6e984fb84a3f46564` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=goal,remainingTokens,completionBudgetReport |
| `work/P33-pickup-resource-fault-review-r1/oracle.json` | 1647 | `1e814f6e630439ec1d257e82a11f2371abdf974c2f0b1359b2f0516d7f02f9f0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pairs,constructor,faults,basis |
| `work/P33-pickup-resource-fault-review-r1/pre-goal-artifact-manifest.json` | 15581 | `9f445cca772fa65eccaad9f9a1fcefb26da6ac81b33c0c9322be669c242ea484` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P33-pickup-resource-fault-review-r1/progress.json` | 353 | `35ec055cf8e4665ce9bdc00942fbb276051c632af5c080c6be164387c7b7b16f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,status,cases,passed,failed,productBuilds |
| `work/P33-pickup-resource-fault-review-r1/result.json` | 1702 | `942d4f16f3b3f1b65602865d99d39615c228f362390a66e0765d901b75672509` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,completedUtc,baseline,distinctCases,passed |
| `work/P33-pickup-resource-fault-review-r1/sensitivity.json` | 3274 | `d4eb91d9f589735276e8e6d02d02fcd4663961a4a49f13b2b48786cd7e12c2c0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,controls |
| `work/P33-pickup-resource-fault-review-r1/source-contract-locations.json` | 4218 | `e75a864c7f769531541b1249530620a6da62341fb5ecff8202a01329877e6089` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=methods |
| `work/P33-pickup-resource-fault-review-r1/source-manifest.json` | 10782 | `8343f6cf225dcf5b44f6c457d68795473328e95560d66de6ee5f737d932c95f6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,assetPolicy |
| `work/P33-pickup-resource-fault-review-r1/verification.json` | 10439 | `62b91ff48d3c10c0bec62419e78e64a9d1efb73cba4925600a09d66b2541c685` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=distinctCases,caseChecks,sourcePinsChecked,sourceAudit,runReceipts,environmentChecks |
| `work/P34-pickup-authoritative-flow-review-r1/HANDOFF.md` | 10247 | `2f3d91060e5e89ff84615da6d3d491d0c85ef4f2512041b55ae7d69dd2693f40` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P34-pickup-authoritative-flow-review-r1/REPRODUCE.md` | 1607 | `1d226a576f53c67dea299fde0572418f00e3602961404a858297af5737df6770` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P34-pickup-authoritative-flow-review-r1/artifact-manifest.json` | 34498 | `54d544f34db25c7b14564846a9bea83d2ae91cf36a38d1bae7e91a6b9b01bcd5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,root,files,excluded,caseCount |
| `work/P34-pickup-authoritative-flow-review-r1/coverage-delta.json` | 1325 | `4b7d75911814161e6f65aaec49346e6300b244cdda0dc73f910779707f9ca388` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=prior,newChains,distinctCaseCounting,remaining,supplementReason |
| `work/P34-pickup-authoritative-flow-review-r1/goal-before-final-verification.json` | 699 | `b70066e1c631385f9b259f6f0a87511bd596949f0a39be3f41765b4c979909ce` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=phase,initialRead,createdWithNoTokenBudget,readback,completion |
| `work/P34-pickup-authoritative-flow-review-r1/oracle.json` | 39542 | `19293603da21f098c3fc9ecf99549627938a9239c927bdf7bd1a20769f77c9ee` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,frames,requestHex,initialGround,initialInventory,partialGround |
| `work/P34-pickup-authoritative-flow-review-r1/pre-runtime-review.json` | 410 | `5b22e4dddb73ab386d38ee10e83555dfbe5b41abfc90f107a0b667ce9007a9b9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=build01,reason,newBuild,priorSnapshots,productDefect |
| `work/P34-pickup-authoritative-flow-review-r1/progress.json` | 257 | `f43c8c68f8db4f3df3f73eb27c8a4ce93ac498dcfbc5d7bc9c15b297214328ca` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,status,distinctCases,passed,failed,nativeCases |
| `work/P34-pickup-authoritative-flow-review-r1/result.json` | 2847 | `5a41e162dbc7cefb1ab5440249d3f98616d811e96dcc20f9b60f96dd343528d0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,status,supervisor,threadId,utc,writeRoot |
| `work/P34-pickup-authoritative-flow-review-r1/source-audit-addendum.json` | 2038 | `1d23ef83cd0c0c5a4818fbb403b297bb79fed80c76405c57c96a9bbbe74e9072` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=phase,files,note |
| `work/P34-pickup-authoritative-flow-review-r1/source-manifest.json` | 20299 | `0d213eb461de683b1a52f917deea5c9eb7a97c464c6ebdb6a4ab22285a456512` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,scope |
| `work/P34-pickup-authoritative-flow-review-r1/verification.json` | 8648 | `2308ce36fac3155ad5222fb1528b5d4ed155cc56c85a68ac0d8b3671e315010a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,distinctCases,sourcePinsUnchanged,cases,commandReceipts,negativeControls |
| `work/P35-release-generator-rg2-fix-r1/HANDOFF.md` | 4924 | `2403793321577789f9c8efe269dbf4a8dfa10fc7ded019bbdd2b707297ca00e6` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P35-release-generator-rg2-fix-r1/artifact-manifest.json` | 37227 | `1dc485e7ed75c870d18cf9f5f28668040fe3a6b9878bf693b2cde252d51ddce6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,qualification,excludedFiles,files |
| `work/P35-release-generator-rg2-fix-r1/artifact-seal.json` | 398 | `664a0d7df517d7c9bb191c99cf94995843be882eae26302c8cf344998596156a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,artifactManifest,sealedFiles,sourceSealSha256,entryActivated,packagePlanGenerated |
| `work/P35-release-generator-rg2-fix-r1/final-input-verification.json` | 2073 | `3f9d427017d244af5f763ab3c8eace5b8157cd9645553d857fb1d2c18246b195` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P35-release-generator-rg2-fix-r1/input-pins.json` | 2052 | `f552756f7f7abc1fae3e337893b85f1509aca104d20160b2e6f14df83fedb7a7` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P35-release-generator-rg2-fix-r1/progress.json` | 579 | `262c000e7205db3bcc4fbd69ab9022861fa71185a38d345ee0a6bf0ef868951b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,owner,supervisor,scope,goalActualRead,goalCreatedOrModified |
| `work/P35-release-generator-rg2-fix-r1/real-inventory-comparison.json` | 2097 | `37d133abe5ce5b25ecd3e0bc6829ecda600c0f1eeb0cb6385160b59770ac1e94` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,contextProvider,contextProviderSha256,authorityRebound,originalRows,candidateRows |
| `work/P35-release-generator-rg2-fix-r1/result.json` | 2132 | `bbb191794659ff5fbc4a335be5bcf797f1dc9ed158d84f198d0dfeb6e5c93f41` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,supervisor,owner,candidate,candidateSha256 |
| `work/P35-release-generator-rg2-fix-r1/source-change.json` | 405 | `a9c710c28796c84527ac80054ec149d95477f860432a2685a569cd76098873eb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=changedFunctions,originalSha256,referenceSha256,candidateSha256,authorityRebound,originalDependenciesUsedReadOnly |
| `work/P35-release-generator-rg2-fix-r1/source-seal.json` | 1846 | `0a9499f8e8290883cb71541af24d89792bc304405d65846970a21c8326de3cd3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateOnly,authorityRebound,files |
| `work/P35-release-generator-rg2-fix-r1/synthetic-result.json` | 3150 | `0e7823cff0a86a1df858f53f343cd8354c9f7c0d2360ab1d4492a67b3df09e13` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,qualification,cases,results |
| `work/P35-release-generator-rg2-review-r1/HANDOFF.md` | 4374 | `7e846d9d8914e61551ef5799cba0221c4860192399c5d436bc56437c39425d4b` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P35-release-generator-rg2-review-r1/artifact-manifest.json` | 29810 | `58e1722a178f3a9aab426488736d22ba2d91233c74672c604ac8f295106844d3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,qualification,files |
| `work/P35-release-generator-rg2-review-r1/artifact-seal.json` | 407 | `a210842eabf2b486add553fc41026f5a5468136d8f831134b92d3e1007a3ffb5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,artifactManifest,sourceSealSha256,sealedFiles,formalInstallation,entryActivated |
| `work/P35-release-generator-rg2-review-r1/baseline-review.json` | 4847 | `03553d352832091d0f6c1545a035c9813dea1bfc39c6aca73e6226e70d38fceb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,pins,originalRows,acceptedRows |
| `work/P35-release-generator-rg2-review-r1/final-pins.json` | 1065 | `1b7dfaf34d2b92f7560fff787af14ca4325aa7c3b62f04cebd04ca7d0591d94a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=providedBy,basis,generator,sourceSeal,artifactSeal,outerArtifactSeal |
| `work/P35-release-generator-rg2-review-r1/final-review.json` | 1311 | `3784c24e0becc77110876ab98642dfe2ed01553a8b8e04a2bc0d082fe7088777` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,candidate,sourceSeal,artifactSeal,verifiedSealRows |
| `work/P35-release-generator-rg2-review-r1/independent-cases.json` | 3089 | `6486d58791e9c8aa14412c3ddb15d40f25c281c43f16c7c5a57f88c3f2f4dbc3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=cases,total,expectedOutcomesMatched,productTests,fixtureRoot |
| `work/P35-release-generator-rg2-review-r1/progress.json` | 1537 | `1af3cd8cacf2df235cda0a5e8e5e34936ae3c1ea1c884d5c3091c901d17abb93` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,reviewRoot,baseGeneratorSha256,acceptedCorrectedPlanSha256,originalGeneratorRead |
| `work/P35-release-generator-rg2-review-r1/protected-baseline.json` | 1830 | `9ad592528403ab41a5f7c0ecff183a17f780e2ad3b29603a3f92f3287f21a748` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,releaseR34Exists |
| `work/P35-release-generator-rg2-review-r1/real-inventory-comparison.json` | 604 | `766c39b8f3e32a5c9c76408291dff44adb1ddf41cc7b0ebf8d9faf1f32574475` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=originalRows,candidateRows,allOriginalRowsUnchanged,exactAcceptedPlanFilesMatch,addedRows,closurePins |
| `work/P35-release-generator-rg2-review-r1/result.json` | 3680 | `69ec58d51cc5cb3a3d4fb41e34f2dd63570f99689a517f12b0efae02fa6a6672` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,reviewer,supervisor,status,isolatedFixAccepted |
| `work/P35-release-generator-rg2-review-r1/review-scope.json` | 2124 | `c8fc42d1be58189b5ffdbb6eab9b793a83ab71d39a993bbfe059aef2f7fa73f0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,baselineStatus,provisionalSourceObservation,candidateExecutionGate,finalChecks,excluded |
| `work/P35-release-generator-rg2-review-r1/source-seal.json` | 1480 | `581c93a5b4fe5bd852e3f52280f010c0811d37459210881d18b6cf81f9dcd233` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P36-P37-integration-plan-r1/HANDOFF.md` | 9477 | `19b704e626ca8bd25eef78275de0c7551feff65881a53109a3b37481901419b0` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P36-P37-integration-plan-r1/ancestor-policies.json` | 402 | `1c9eca2f29fa9bd99b1e8ff96c5c634b282c4e0f82596a42ededf72eaf54f2c9` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P36-P37-integration-plan-r1/artifact-manifest.json` | 68214 | `de9c24a5743a271c62cea24f2877e774d7b8f47702a792734f899e93e461d577` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P36-P37-integration-plan-r1/artifact-seal.json` | 649 | `a03d41a3f1966fe16967d3872e5acde7e5353c611c538c3e72e9d15ddd8fa878` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,artifactManifest,sealedFiles,planSha256,contractSha256,toolSourceSealSha256 |
| `work/P36-P37-integration-plan-r1/build-dependency-pins.json` | 1126895 | `58fd3d9a4de20417a2336c00f99494a7e4874352c470676cdbdfefa3763b116b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,roots,processesInvoked |
| `work/P36-P37-integration-plan-r1/checks-result.json` | 3219 | `fb7ddd6a395aa4532b807c4332841748ce8fd7f516417e80fa8859656e419fdd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,cases,results,productBuilds,sharedWrites,actualSuccessfulInstallNotTested |
| `work/P36-P37-integration-plan-r1/copy-ledger.json` | 187350 | `29f5e87792342bb28d1505076cd5b7a735717c847769f027def8f05531fd4334` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files |
| `work/P36-P37-integration-plan-r1/execution-contract.json` | 10124 | `7d7ff368af9a439c8bb65918766333479c493ca03a324f0ce96ac1278104b19a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,owner,supervisor,executionAuthorizedByPreparation,executionRoot,buildRoot |
| `work/P36-P37-integration-plan-r1/expected-outputs.json` | 6463 | `c7431dc50fbfaed13965aa35c0599f1486c6506c96fe55df596cb27b61197b55` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,productManifest,actualHashesMustComeFromExecution |
| `work/P36-P37-integration-plan-r1/final-preservation.json` | 44909 | `9f23c441d20dc6d3ea3851cb2d82aac345d4b5c10b3c55df4cabda9ffb6f2c87` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,sourceBeforeCount,sourceAfterCountPending,sourceBeforePins,protectedPins,assets |
| `work/P36-P37-integration-plan-r1/input-pins.json` | 2896 | `96e5617491df6ac65c29d8b20d5cd806bdfbb9371604fbf1925e9fa86659063c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,goalActualRead,goalCreatedOrModified,stageFilesVerified,dependencyFilesVerified |
| `work/P36-P37-integration-plan-r1/pending-receipt-requirements.json` | 2741 | `cced714433f69352161a7fa791c4b01c28f57e1ca6d1eb2eca86a0a798258879` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,isReceipt,executionAuthorized,knownBindings,receipts |
| `work/P36-P37-integration-plan-r1/plan.json` | 4166 | `58d072aee47cc8f6c5a08071025dcd2c46ebebf2c3ef0e663605b5f2705302b5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,preparationOnly,executionAuthorized,owner,supervisor,writeRoot |
| `work/P36-P37-integration-plan-r1/progress.json` | 186 | `703814d0ec1934f1e487b1c995f1aa515845b274bffb8a361e8c4dcc48c713a0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,goalActualRead,sharedWrites,builds,formalEntry,sealedThenStopWriting |
| `work/P36-P37-integration-plan-r1/protected-pins.json` | 7925 | `2d69faddd4e2cd887f0b824e64d4e3d1a8d738dec36ab45fe6f6659f5f5ff448` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,entry,assets,forbiddenRoots |
| `work/P36-P37-integration-plan-r1/result.json` | 2527 | `1cbd250e50fb4ee73942e7e79a5c1febf3f9df8e75ee7761f1785c10651e2aa6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,owner,supervisor,plan,executionContract |
| `work/P36-P37-integration-plan-r1/source-ledger.json` | 212187 | `701f95113b60daa1268d51f9ccf223c0a4994307d5ffe4f3ecea75ccd911f39d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=beforeCount,afterCount,replacements,additions,allExistingTargetsRegularNlink1,files |
| `work/P36-P37-integration-plan-r1/tool-reference-pins.json` | 3244 | `5427eb6ed5c5976d86df08fb23902c83961115c4ad6df52efdc4bc94502584ae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,referenceOnly,oldReceiptNotAcceptedForP36 |
| `work/P36-P37-integration-plan-r2/HANDOFF.md` | 4754 | `fa1a60335f711d75e6c85905ee0ffeb2429346b8b7ea1d92ead4afa8ddca752a` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P36-P37-integration-plan-r2/ancestor-policies.json` | 402 | `1c9eca2f29fa9bd99b1e8ff96c5c634b282c4e0f82596a42ededf72eaf54f2c9` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P36-P37-integration-plan-r2/artifact-manifest.json` | 147613 | `b106754c71c98a5df611a8b3aeed7f2afefb85159887ae146c2ee577f746667d` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P36-P37-integration-plan-r2/artifact-seal.json` | 642 | `d72f2c75fc792df44a5e035651a82e5cb2244d944d42564e2231c7e15a1a30c7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,artifactManifest,sealedFiles,planSha256,contractSha256,toolSourceSealSha256 |
| `work/P36-P37-integration-plan-r2/build-dependency-pins.json` | 1126895 | `58fd3d9a4de20417a2336c00f99494a7e4874352c470676cdbdfefa3763b116b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,roots,processesInvoked |
| `work/P36-P37-integration-plan-r2/copy-before-edit.json` | 457379 | `c2aea8cbb49952c330fd8bedc4b02b8257c7cd6d7de9cbd9b2c2b6619532c805` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files |
| `work/P36-P37-integration-plan-r2/copy-ledger.json` | 191686 | `54aca008c0f97f1f0e00f065e5f70a8ccad595f9b603417935981fb5ec352a10` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files |
| `work/P36-P37-integration-plan-r2/delta-verification.json` | 407 | `b3f2981d2c0f5330295d261593bbc0cee791f50024a2db858db38a0baded2b93` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,semanticChanges,unchangedRuntimeFiles,mechanicallyChangedDocuments,originalPlanAndReviewPinsUnchanged,builds |
| `work/P36-P37-integration-plan-r2/delta.json` | 250648 | `753413394bd49031318042343c778c6997b9a1a88ae805053efe02bd666bbd59` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=semanticChanges,unchangedRuntimeFiles,mechanicalChanges,unchangedFutureExecutionRoot,unchangedBuildRoot,unchangedReceiptPath |
| `work/P36-P37-integration-plan-r2/execution-contract.json` | 10326 | `f439915614397edb8a478196375706aec1af68b670bd06c310d1ba3e251e8c30` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,owner,supervisor,executionAuthorizedByPreparation,executionRoot,buildRoot |
| `work/P36-P37-integration-plan-r2/expected-outputs.json` | 6463 | `c7431dc50fbfaed13965aa35c0599f1486c6506c96fe55df596cb27b61197b55` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,productManifest,actualHashesMustComeFromExecution |
| `work/P36-P37-integration-plan-r2/final-preservation.json` | 7940 | `254663077e6994f41c5cc53296a0880c28472957c309e625d163bc40b146dbcc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,protectedPins,sourceBefore180,futurePathsAbsent,originalPlanAndReviewSealsAndMembersUnchanged,fullPreflightInvocations |
| `work/P36-P37-integration-plan-r2/input-pins.json` | 2896 | `96e5617491df6ac65c29d8b20d5cd806bdfbb9371604fbf1925e9fa86659063c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,goalActualRead,goalCreatedOrModified,stageFilesVerified,dependencyFilesVerified |
| `work/P36-P37-integration-plan-r2/minimal-checks-result.json` | 2003 | `080bd39e82722a65aeebbd86260793d20ff8a2cf636b9f74b8d2bdb0a4ebcc6e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,checks,original16CasesRerun,successfulSupervisorReceiptFabricated,builds,sharedWrites |
| `work/P36-P37-integration-plan-r2/pending-receipt-requirements.json` | 2818 | `d6dda794bf8fbfce2722e048ad608ac9bd3b4816c396a1a3b16ed91ab4d278b2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,isReceipt,executionAuthorized,knownBindings,receipts |
| `work/P36-P37-integration-plan-r2/plan.json` | 4250 | `f67fa56815fbc133c1710c450efe1305d58da7c75566e02a9d909a24ca6059bd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,preparationOnly,executionAuthorized,owner,supervisor,writeRoot |
| `work/P36-P37-integration-plan-r2/progress.json` | 200 | `6039c6d5342247ddfe9cdfb731d4f8dd6937687c181b62f0673c207d79e5cc1e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,goalActualRead,sharedWrites,builds,formalEntry,writeStopAfterSeal |
| `work/P36-P37-integration-plan-r2/protected-pins.json` | 7925 | `2d69faddd4e2cd887f0b824e64d4e3d1a8d738dec36ab45fe6f6659f5f5ff448` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,entry,assets,forbiddenRoots |
| `work/P36-P37-integration-plan-r2/repair-input-pins.json` | 1968 | `446e432e65cf390280bacb0473fff10383ff0cb2ce089dac5792073750bdc4b1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,originalArtifactMembers,reviewArtifactMembers,goalActualRead,goalCreatedOrModified |
| `work/P36-P37-integration-plan-r2/result.json` | 3162 | `41d0a243089f492bea17c7734e164633ff6293bb915ba622c4e5240768bf67cc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,owner,supervisor,plan,executionContract |
| `work/P36-P37-integration-plan-r2/source-ledger.json` | 216894 | `b465f2bcb13c6e283e76157b8a862d6ff3e4d0b0dd05d8b445267d4d989dca40` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=beforeCount,afterCount,replacements,additions,allExistingTargetsRegularNlink1,files |
| `work/P36-P37-integration-plan-r2/tool-reference-pins.json` | 3322 | `50520fdf296c93b3745e524a0263af2efc6431d0c47abd8d9c7ee64f7af4418b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,referenceOnly,oldReceiptNotAcceptedForP36 |
| `work/P36-P37-integration-review-r1/HANDOFF.md` | 5365 | `994d9d24f7269400797e27a4b6461707c0fb55e16acd9c65f8a868b1c7d803c1` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P36-P37-integration-review-r1/artifact-manifest.json` | 4174 | `ecf1f12dcbb976dfdaa546d12ac0ae8bdaa69964199dc32c45c7fa99dec542f2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,qualification,files |
| `work/P36-P37-integration-review-r1/artifact-seal.json` | 377 | `9cf3aeeac98b4a16d01f0b1089fbf0e561ca705efb2e749a911a9a629e81503b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,artifactManifest,sourceSealSha256,sealedFiles,decision |
| `work/P36-P37-integration-review-r1/baseline-review.json` | 7020 | `eb20ebd05e09339129ee06a74aa42c67fbf2a95c87735b76e5fc0c6581d7f683` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,exactInputPins,stageFilesVerified,acceptedSources |
| `work/P36-P37-integration-review-r1/final-pins.json` | 639 | `a76f37b570ed30bf01db4a604343c9bbdf0ae97800bd959c6ec2dd597577e249` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=providedBy,authorRoot,pins |
| `work/P36-P37-integration-review-r1/final-plan-verification.json` | 6462 | `cf4c53158d944f9495b53fd817c41272ffab7b6f4b67b3c5f5413d63e047066b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,authorFiles,toolFiles,sourceRows,readonlyChecks,sharedWrites |
| `work/P36-P37-integration-review-r1/plan-review-criteria.json` | 3100 | `5a089fbf8fd82e665699a07f47cf5b046c763224e6f0ba242d1c2f5fe02e38c3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,qualification,sourceBinding,sharedWriteAllowlist,immutable,installation |
| `work/P36-P37-integration-review-r1/progress.json` | 1410 | `a98769343fddeeb64ee2b775290b1de346284c4a490f98f6138444e35df26738` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,owner,supervisor,status,goalActualStatus,goalObservation |
| `work/P36-P37-integration-review-r1/protected-baseline.json` | 79301 | `94f351928085d64553dbed22326c924ec9a64037a7317070e68d79c96eea745c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,stageSourceRows,canonicalSourceRows |
| `work/P36-P37-integration-review-r1/result.json` | 8067 | `c37b92bf89bd7d73cc845e87a7746d62a4886335ec92745cfa7d80dfa198d60a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,decision,reviewer,supervisor |
| `work/P36-P37-integration-review-r1/source-seal.json` | 1502 | `c89d5c067b844a88325c4ec17d2b93c37fb8fb91c30ae9e059f6805bdce03a58` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P36-P37-integration-review-r2/HANDOFF.md` | 4040 | `58d869882616d24b12d7bb31389678a06daab428b9b5898efb5fe491b5be58d5` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P36-P37-integration-review-r2/artifact-manifest.json` | 3267 | `c6c53f4bbd18d3159cc2d5b443bcae2e5ce5454fb7f95ffddfb0825f8d6cfc9d` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P36-P37-integration-review-r2/artifact-seal.json` | 435 | `8cdfba1534056ca45d6b8fa36bb69c142051035dfea3e9be21cab1d522305bdb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,artifactManifest,sourceSealSha256,sealedFiles,decision,actualP37InventoryAccepted |
| `work/P36-P37-integration-review-r2/delta-review.json` | 2889 | `239a61446fcd02a790018d39fd9937c1e4e09c3cf72b231a9833b11592621ff0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,authorArtifactMembers,runtimeMembers,unchangedRuntimeFiles,semanticChanges,mechanicalDocuments |
| `work/P36-P37-integration-review-r2/final-pins.json` | 716 | `d07e97e7ccf14e71adc6d7bb00049aa85ebbf11fa97647bd20458eed3c547173` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=providedBy,authorRoot,pins |
| `work/P36-P37-integration-review-r2/plan-review.json` | 2315 | `e6978bc8ff740731fbe52c25e0f32e67db13b183474ff25af94a842cd9f22422` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,utc,reviewer,supervisor,planSha256 |
| `work/P36-P37-integration-review-r2/progress.json` | 577 | `e07e2fbd8d8db47b7a3a36e2deebea3b331b82e335cb5f0b9f856849f5b9c068` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,owner,supervisor,goalActualStatus,writeRoot,sharedWrites |
| `work/P36-P37-integration-review-r2/protected-row-deletion.json` | 7906 | `663b4b56ff0798f66028607965130039a0bb0d03ffc81c78ee0f97e33b0b092f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,entry,assets,forbiddenRoots |
| `work/P36-P37-integration-review-r2/result.json` | 1556 | `c596f53695d385315f3ea75168ef8a2ed710a6fd2caf5303878d26ee6600da2d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,receipt,authorArtifactSeal,authorArtifactMembersVerified,semanticChanges |
| `work/P36-P37-integration-review-r2/source-seal.json` | 1169 | `ea7fa24e8f18222a67625fbc65511ae3c44d366f26198f1c5cdcf9eff6cb70d4` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P36-canonical-final-review-r1/HANDOFF.md` | 4846 | `5eb0fec38d31940fdd42954855f444528a42bebab08c495fe3bd5cfe1ff34607` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P36-canonical-final-review-r1/artifact-manifest.json` | 5552 | `741b61d280e3dddf1a3c7ae4e7a58f49865acdcd96f837469514017da3eb9d05` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P36-canonical-final-review-r1/artifact-seal.json` | 440 | `fef92b16831f15a2b9d03048a6b257ad2ef64a72eb4411f2d0f5aac8bc3335f6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,artifactManifest,sourceSealSha256,sealedFiles,decision,P37InventoryAccepted |
| `work/P36-canonical-final-review-r1/auditor-correction.json` | 1403 | `ede353f17820b6ecee7f2006e8d7b26011456bd30c1fdab9a046e1c7783405e5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=initialScript,correctedScript,initialCommand,successfulCommand,reason,productFailure |
| `work/P36-canonical-final-review-r1/binlog-events.json` | 1256501 | `fbbe3ae9427442e8dd112ac7ce9433119c30bf67e6c5fa642ce211fe0e24b555` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=binlog,sha256,fileFormatVersion,minimumReaderVersion,parserAssembly,parserRuntime |
| `work/P36-canonical-final-review-r1/compiler-binding.json` | 221375 | `b977ad480a0c1fd5a113d1d5a66d284295fb5bc09c3e0e264c2fd6beca788175` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=binlog,totalEvents,buildSucceeded,warningEvents,errorEvents,cscTasks |
| `work/P36-canonical-final-review-r1/core-review-result.json` | 1266 | `190837b7fa038908548146e110d48620bf612ed2b98098ee56b4ffe9de2aef72` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,sourceFiles,changedTargets,independentBackups,buildInvocations,actualCscTasks |
| `work/P36-canonical-final-review-r1/final-pins.json` | 927 | `f220ef98bd5dff704e486a19194bcc84878cd064110b6440110981653936e455` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=providedBy,executionRoot,pins,newManifest |
| `work/P36-canonical-final-review-r1/final-verification.json` | 594 | `1760918ddb4e8731534e98548882a0b9d3a18e26fd3a950190a79622d3d2c8b1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,executionArtifactSeal,artifactMembers,externalSources,externalOutputs,evidenceIndependentCopies |
| `work/P36-canonical-final-review-r1/progress.json` | 716 | `ae2a86dc78c8b317b1c586c351f72eba9373c895db8f7b6236cf6aa79a254be0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,owner,supervisor,goalActualStatus,executionReceiptSha256,awaitingSupervisorFinalExecutionSeals |
| `work/P36-canonical-final-review-r1/provisional-core-pins.json` | 7693 | `79026036f0a5a6a64eeff417cf7bcbd3803231b0f6bb0ccef439bab9ef3c0082` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files,readerOriginal,readerCopy,readerAssemblies,dependenciesAll4350Rechecked |
| `work/P36-canonical-final-review-r1/reader-command.json` | 1056 | `175a674e6afaff29f8e6b71c8598d1c9ada336261b24e94d2a51de7ac9f282eb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=argv,cwd,exitCode,readerRuntime,productBuilds |
| `work/P36-canonical-final-review-r1/result.json` | 4318 | `63c2801789dadc95310348edbe0b3c440ed91ab537b6a609193eb65cb30cb631` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,decision,reviewer,supervisor |
| `work/P36-canonical-final-review-r1/source-install-review.json` | 9464 | `dd68db330ac40e83a5fca0cad3894f59a3e0b7421e0831296bce1778667096c5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sourceCount,transitions,independentBackups,protectedPinsVerified,assetsJunctionUnchanged,outputs |
| `work/P36-canonical-final-review-r1/source-seal.json` | 1478 | `33612309ee703695693f856cf6059fb71a4a838ae0f458412d5441e747396613` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P36-canonical-integration-execution-r1/HANDOFF.md` | 5775 | `1b4285ef2016613365f84968f747c5f4cc7faaf6b2f77c84ecd8f6f06bdca002` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P36-canonical-integration-execution-r1/artifact-manifest.json` | 40700 | `95ce307d92645d98d4bdaf58e663e10564c75c9d0f23d24a6a17925907068d7e` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P36-canonical-integration-execution-r1/artifact-seal.json` | 964 | `c7c194fed021123fcca4586df4b9b05d5381e0f6838d24e759728d8598bf52bf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,artifactManifest,sealedFiles,executionResult,externalMaterialSeal,buildInvocations |
| `work/P36-canonical-integration-execution-r1/authority.json` | 2792 | `dd35b80947da278a997cb3e307fd40ff0867cd87d048d2e6c0e963b879182871` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=receipt,pin |
| `work/P36-canonical-integration-execution-r1/compiler-command-index.json` | 58136 | `144ac5d55dbe0690b79db4fcef2e378e4bc01be0b160af8cba447d93a1fbb10c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=qualification,stdout,decoder,replacementCount,cscTaskStartLines,compilerCommands |
| `work/P36-canonical-integration-execution-r1/delivery-result.json` | 2012 | `c038bf986adde2adf265df01bb450cd5486adcf396b52e6845603cc2a910a53a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,executionResult,sourceManifest,productManifest,product |
| `work/P36-canonical-integration-execution-r1/evidence-copy-pins.json` | 15313 | `0fdc655627b11c34fb7d9fc39f7e55459a1881e8b8bfb785e16193d626814e85` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,independentCopies |
| `work/P36-canonical-integration-execution-r1/external-material-seal.json` | 41121 | `257d957cfc3c6b7061c1face5baeaf19c111f229a76c3834a3c90d68ab60ffa5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,sourceFiles,productManifest,outputs,qualification |
| `work/P36-canonical-integration-execution-r1/outer-invocation.json` | 1236 | `4763664484b69a429ee59a75e3d08334d1a3074b384a8ed674285d01b7f8a0ca` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,argv,cwd,sessionId,startChunkId,completionChunkId |
| `work/P36-canonical-integration-execution-r1/output-stage-comparison.json` | 6906 | `3b658e995fb55f931233da812bc523bfafdbdecf46d8e39ba510243fb89050a2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,matches,count,DLLsExact |
| `work/P36-canonical-integration-execution-r1/preservation.json` | 108266 | `6b35aa9c227351b30101be4d34ca93d42172f28cae34ad59399d89898418a094` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,sourceCount,sources,protectedPins,assets,planSeal |
| `work/P36-canonical-integration-execution-r1/result.json` | 6818 | `a86b0e4ea202d4dc02c71aec25a7c97e2609874a7b20662d839c0971509f0a8e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,completed,startedUtc,buildInvocations,planSha256 |
| `work/P36-canonical-integration-execution-r1/source-manifest.json` | 45035 | `f3275b4f202ee0d277e5749e85e2e10297396ce8f9bafa70696509a91027f95d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,sourceCount,planSha256,contractSha256 |
| `work/P36-canonical-integration-execution-r1/started.json` | 493 | `ecd9de34611facfa1b60779a07d6022b326de596bc7458111b9d29560b7b4a42` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,completed,startedUtc,buildInvocations,planSha256 |
| `work/P37-bound-inventory-execution-r1/HANDOFF.md` | 3356 | `eebca69077a0245252a8183d45113761e585b40a12c48c4356d5b8153c7fa894` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P37-bound-inventory-execution-r1/artifact-manifest.json` | 1395 | `2daf0f79dd9bd6522f20da707ff67a8bd3d3ed2d43cb54ab91c4bb6227b98a69` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P37-bound-inventory-execution-r1/artifact-seal.json` | 705 | `5a6dee83c3ed2a0fdc856b40703f6db517f1920b4997ebe62e4f2812a2a466c0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,artifactManifest,sealedFiles,rawInventory,inventoryInvocations,inventoryOnly |
| `work/P37-bound-inventory-execution-r1/command.json` | 1271 | `6b90c34810d8740d211bd8c1974f940848135024b83c6cb3c32069d14d047646` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=argv,cwd,exitCode,finishedUtc,inventoryInvocations,stdout |
| `work/P37-bound-inventory-execution-r1/input-pins.json` | 10464 | `d0d840e507c5838ef7297e0897ceb9d52d18419aa6b67b7dda7e45a50adecb5e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,protected,goalActualRead,goalCreatedOrModified |
| `work/P37-bound-inventory-execution-r1/inventory.stdout.json` | 58242 | `2bb58c3c48b576a213c05f5a9b2835e5f2ed4b717afe8fbbe8719009780bc778` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,qualification,fullReleaseAuthorized,entryActivationAuthorized,sourceFiles,files |
| `work/P37-bound-inventory-execution-r1/result.json` | 2108 | `4f41ba15550bf19e673423df985e40f8bac56218e47a40205489d85cc0dcc84c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,supervisor,owner,verification,rawInventory |
| `work/P37-bound-inventory-execution-r1/started.json` | 821 | `dcf3edd9bb3a06aa54d01e1b2af4f803fca2dd6ee56563369063a0e6c740b558` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=argv,cwd,startedUtc,maxInventoryInvocations,builds,runtimeRuns |
| `work/P37-bound-inventory-review-r1/HANDOFF.md` | 3547 | `7a2f6f1ddfee2b7ddb036ea48764ca109717ac83a93cc32c218a2c74621535eb` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P37-bound-inventory-review-r1/artifact-manifest.json` | 2961 | `f9a3231d4aee1b3d776dd180560676d78210c4def62320df49a46b46c381f563` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P37-bound-inventory-review-r1/artifact-seal.json` | 413 | `a4113e830ec66b160a361969173a343cb4f027ac181f3ae0fa4107aca9c18d10` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,artifactManifest,sourceSealSha256,sealedFiles,decision,fullReleaseAccepted |
| `work/P37-bound-inventory-review-r1/final-pins.json` | 533 | `24597b10077cc2cdab24e309afb866fa818abf0b6432fe714047fa5f17a05700` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=providedBy,executionRoot,pins |
| `work/P37-bound-inventory-review-r1/inventory-review.json` | 1969 | `19477448a3e0a586ea4379a92b1ac3396de89a3bd71375b9d7fbcf755323e015` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,rows,categories,allRowsSourceSizeShaAndTargetExact,targetsUniqueCasefold,fourIdentities |
| `work/P37-bound-inventory-review-r1/progress.json` | 393 | `c49116c796dcbab6e18763350bdd2d4a57dc8293f3c46098a1ca0827f9919a0d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,owner,supervisor,goalActualStatus,awaitingSupervisorAuthorSeal,inventoryCliInvocationsByReviewer |
| `work/P37-bound-inventory-review-r1/provisional-pins.json` | 1746 | `9189f647be7cd56988843a2448138be3818089770bbb6237ebf84daceae85994` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P37-bound-inventory-review-r1/result.json` | 4065 | `2a8ad4254cc81d7796e22a7621941883cff19f4cb243c470f9642ad7aca33be7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,decision,utc,reviewer,supervisor |
| `work/P37-bound-inventory-review-r1/source-seal.json` | 1008 | `e1ab709846ea1947945d3ccede26f2290030fd1c496a4379e48a59e90c67ee69` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P38-delivery-candidate-r1/DELIVERY.md` | 3280 | `28a3258171fbc6ad0c286a54d4bf99f133ab9118bc089219757a82866a91e2b4` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P38-delivery-candidate-r1/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `work/P38-delivery-execution-r1/HANDOFF.md` | 5664 | `d59d12ce979fad548138c7000e331e622c9a5ffcf744be4af837e27bc436fa23` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P38-delivery-execution-r1/artifact-manifest.json` | 12839 | `d9cb305246f5e23bcc55eda60829eae9d4556bac8a7206561074c321a4ef498f` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P38-delivery-execution-r1/artifact-seal.json` | 1297 | `b68ac8eb18888ca5806c81fc2ba38b8ff3dd0eea847d731f56ad02c0496d261b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,artifactManifest,sealedFiles,executionResult,candidateSeal,assetIdentities |
| `work/P38-delivery-execution-r1/authority.json` | 2247 | `dff2398ff89934109e4351a7fee4fa48ed006f59a2e59c707620300ab1cebd52` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=receipt,pin |
| `work/P38-delivery-execution-r1/candidate-file-seal.json` | 36350 | `3470b0ff44b49ae08c4751efe14ea2b8bd983437a8111c0512dc6c799d71e683` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=independentFiles,assetMetadataPlan,assetExpectedBodyHashesFromAcceptedIndex,assetFullSHARequiredByIndependentReviewer |
| `work/P38-delivery-execution-r1/compiler-command-index.json` | 25249 | `1c9c6c27fd4d79a40da850742bb6386ba139fea5da2e0c9518c378aacced1c81` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=compilerCommands,stdout,binlog,compilerTasks,qualification |
| `work/P38-delivery-execution-r1/delivery-result.json` | 2100 | `c8874984330980acce2fb81a7815e47cbd164f44d3ab1add7f5f493881c0cf78` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,executionResult,candidateRoot,candidateSeal,candidateFiles |
| `work/P38-delivery-execution-r1/environment.json` | 2166 | `6cb884e7806e230c3c44e20a53f924756221b38183bf0b3a68fc82a14981185b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=spec,HOME_USERPROFILE_CODEX_HOME,parentEnvironmentModified |
| `work/P38-delivery-execution-r1/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `work/P38-delivery-execution-r1/launcher-output-pins.json` | 1464 | `5b45393d03e3c191dd9687e201ca58af431f1a850a4996260994bd6df503fb84` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,compilerEvidence |
| `work/P38-delivery-execution-r1/materialized-asset-identities.json` | 5928701 | `930394cfa44ba3ef9ee747f444efb0b49a237f500a8e1cdc88a9ab37cec2d131` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,fullBodySHAComputed,readonlyByPolicyOnly,attributesOrACLChanged |
| `work/P38-delivery-execution-r1/materialized-copies.json` | 117855 | `933aaf2f94aa3d60197eff4d6ce9b4592a7af9aae9a8c4900873740e58974df1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files |
| `work/P38-delivery-execution-r1/outer-invocation.json` | 1223 | `d76f5c53c7a221a527c0165ed04d7b6cdfed1b4f87cdf215752011b5dcf74025` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,executor,mode,planSha256,contractSha256,receiptPath |
| `work/P38-delivery-execution-r1/preservation.json` | 9092 | `bf6760ffc09a22a1abd7e8ea691c556cf7340792b3e143009b1f721a4506e8f2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,protectedPins,plan,contract,planArtifactSeal,sourceSeal |
| `work/P38-delivery-execution-r1/result.json` | 2882 | `37655945b75dc0f86b6fe4153ba17779a186df571726f4ce162dcd8ee1807a2f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,completed,startedUtc,planSha256,contractSha256 |
| `work/P38-delivery-execution-r1/started.json` | 474 | `de380596e321441d7c09988683ee77def09b9809b65426d2eebf7ae11594b6e1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,completed,startedUtc,planSha256,contractSha256 |
| `work/P38-delivery-plan-r1/HANDOFF.md` | 7185 | `e995ab68f4066eac9d8ae7e1ec98e5767899741a1bcacb4e386e27e7e0fdc15f` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/P38-delivery-plan-r1/ancestor-policies.json` | 18 | `6ac91cab38eae269a4e6457275b3b956f615a7df03de4b1ce88f44b8110efc30` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P38-delivery-plan-r1/artifact-manifest.json` | 8094 | `1f31aa20d1abafcbf3928b85f8300ea1d3bb335a215f8843a3e9c424d46f2faf` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/P38-delivery-plan-r1/artifact-seal.json` | 628 | `0b601bb1dd624b12e49cc4550dfca166304d318d4615aba65706a1de18732bf5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,artifactManifest,sealedFiles,planSha256,contractSha256,sourceSealSha256 |
| `work/P38-delivery-plan-r1/asset-materialization.json` | 7947156 | `dda5ba5819ed0095b81b74facd2f1fad3e411aa7b30e57d6543de424cd825a62` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,junction,physicalRoot,integrity,bodyCount,files |
| `work/P38-delivery-plan-r1/build-dependency-pins.json` | 1126895 | `58fd3d9a4de20417a2336c00f99494a7e4874352c470676cdbdfefa3763b116b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,roots,processesInvoked |
| `work/P38-delivery-plan-r1/execution-contract.json` | 11771 | `0ceba81b82f80ccd601c12da36b10557a2a5b9bd2d867cc166923a4816b43b3e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,owner,supervisor,executionAuthorizedByPreparation,candidateRoot,executionRoot |
| `work/P38-delivery-plan-r1/input-pins.json` | 3181 | `b2500eae6d8c0e38cda8fbf82394cb64181fa359e0545c1a47e4532f2ed9a6cb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,goalActualRead,goalCreatedOrModified |
| `work/P38-delivery-plan-r1/launcher-delta.json` | 1803 | `21d1c65b106f8cbb3096d463786a4f8c87462fb76b63844d3875a61328e1c851` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=mechanicalSubstitutions,methodBodiesUnchangedAfterLiteralNormalization,projectUnchanged,checkOnlyReturnBeforeProductStart,cop |
| `work/P38-delivery-plan-r1/materialization.json` | 110054 | `1698c7313a6969e5eb7eb63bc2f31b26b846021062b49a15ee535f1301322cd8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,sourceFiles,productOutputs,webFilesIncludedInSource181,normalManifestEntries |
| `work/P38-delivery-plan-r1/pending-acceptance.json` | 1053 | `93a2216513968cf91ae3c9ab57d6abadb48e4480d0f4adb817a0235c08dd3f8a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,isReceipt,actualReceipt,expectedExecutionSchema,expectedDecision,receiptPath |
| `work/P38-delivery-plan-r1/plan.json` | 3830 | `d1649ed3104850b96b719a367583835c781aa0a1fd05fad52c27b6ce402cce92` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,preparationOnly,executionAuthorized,owner,supervisor,writeRoot |
| `work/P38-delivery-plan-r1/preservation.json` | 7770 | `9db36550f78e4f0a212003c96046a7b051d823c11d8ed5bf37e7f5588eec15be` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,protectedPins,futureRootsAbsent,assetBodyHashesRecomputed,assetContentOrAttributesModified,sharedWrites |
| `work/P38-delivery-plan-r1/progress.json` | 217 | `4b0bb28bcd4327db154787523af547d14df425f166266b89b192b8589cd5aa4e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,goalActualRead,candidateCreated,builds,checkOnlyRuns,sharedWrites |
| `work/P38-delivery-plan-r1/protected-pins.json` | 7925 | `2d69faddd4e2cd887f0b824e64d4e3d1a8d738dec36ab45fe6f6659f5f5ff448` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,entry,assets,forbiddenRoots |
| `work/P38-delivery-plan-r1/result.json` | 2183 | `e28e4bbec851870130b38ad03849cbce2dc34d7d0e2252b0ceb908b790eaa3db` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,owner,supervisor,plan,contract |
| `work/P38-delivery-plan-r1/runtime-closure.json` | 8499 | `cd09a4b8cd6cade408777347a970cf9a805c121cf5a6ebe5a48b93fa5662d5f5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=normalManifest,runtimeFiles,productAssetsRootRelativeToEntry,webCount,assetIndex,assetBodies |
| `work/P38-delivery-plan-r1/source-seal.json` | 1358 | `3d5f27eb648aa970fe970f00fc5121eda18ecd861c8bc0ae6e882b20ca199216` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,executionAuthorized,files |
| `work/P38-delivery-plan-r1/validation-result.json` | 630 | `e10b42c29620ae32d600a9fda306a9ca6f0fcc3aef5e5277da8d67494ed18121` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,sourceFiles,productOutputs,webFiles,normalManifestEntries,copyRows |
| `work/P38-delivery-plan-r1/version-boundaries.json` | 3243 | `ab6b97ff485d8a7f15e9aef949f26cfc30580f1693046c17fadf8f043c4bdcfc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,versions,currentEntry,projectManifest,r34LauncherSource |
| `work/engine-ground-lifecycle-r1/HANDOFF.md` | 3822 | `b3d89b2500c6c304fce803b69786fed5b83385e237ab0d716ecdd3826360a5c0` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-ground-lifecycle-r1/SOURCE-CONTRACT.md` | 4882 | `60c3338e7190d7bd2ae47c9b15f21082a02a1dbfa505afdb710f8c8f2ed2e2e0` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-ground-lifecycle-r1/VALIDATION-NOTES.md` | 3824 | `70cd6302a4afeda48b07f962fa8f6bb35f1a79c8ec2014d708fd34130c649e38` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-ground-lifecycle-r1/changed-files.json` | 451 | `8092434378ab43b5debe7404e8ab966d4993a95aa159aa23e208921a1ab54e38` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=path,baseSha256,candidateSha256 |
| `work/engine-ground-lifecycle-r1/delivery-manifest.json` | 59650 | `2ad5c438372732384dbb505936a94ce386d25933223194c39c2997de524073cf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,status,files,excludedDirectories |
| `work/engine-ground-lifecycle-r1/final-base-binding-pins.json` | 49611 | `fdd2f1e060463c515ff29f32ec3b4d85534f1d8039783e8362a2bf11fe0d684b` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-ground-lifecycle-r1/goal-read.json` | 77 | `17f94cc151ff4f0e74d76da2613ac700e24399ca132c4e6b295567c39d8b3a4d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,readAtTaskStart,modified |
| `work/engine-ground-lifecycle-r1/input-pins.json` | 15388 | `c89488ca520925f9c8c93d6a74e01069fb6027d7e0ec369e694321a5008be16f` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-ground-lifecycle-r1/preservation.json` | 627 | `83be7e177bcaed9ca6f10cf3dcfc51402b0b0183c741706b51acb5ecaa3e082b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,stageSourcePinsVerified,finalSourcePinsVerified,fourIdentitiesEqual,unchangedCandidateFiles,changedFiles |
| `work/engine-ground-lifecycle-r1/result.json` | 2822 | `359cb5a3111e0dc95e91000024d780098f842cff711c924e7f551212824cb207` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,task,status,checksPassed |
| `work/engine-ground-lifecycle-r1/source-before.json` | 54439 | `0e786195ccb4b64d82c5f58a2f082a1556465ad9cae78fa24bc8d0fc6b1361e2` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-ground-lifecycle-r1/source-manifest.json` | 43756 | `adf30cb60180285541857fac85b3e522d68855a1a63d0117dd4fb71cb1de0056` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-ground-lifecycle-review-r1/HANDOFF.md` | 3215 | `5595d3ffe1446b26eb991c2ddd77207db52a2291dc6b4651617680fe95555315` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-ground-lifecycle-review-r1/artifact-manifest.json` | 7125 | `1b7fbe6eb168a4b41fb34c5d703ef5b0f132297cec9fb65148fa9f7020f11b8d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,candidateReviewed,exclusions,artifacts |
| `work/engine-ground-lifecycle-review-r1/baseline-binding.json` | 94047 | `34176de7b355a57b920c120ebcf814fd39f83e3b26050daae04c6dcfa03c74fc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=candidateReviewed,stage,final,equivalence,verifiedSourceCountEach,deltaFromR31 |
| `work/engine-ground-lifecycle-review-r1/baseline-comparison.json` | 11821 | `c71577126fb971d5ccbaf60ac2c9a480d7190af0d84f22b982fe29b521019142` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=oldSeal,classificationIdentical,counts,pendingNewOwner,sourceDelta,sourceUnchanged |
| `work/engine-ground-lifecycle-review-r1/baseline-probe.json` | 66948 | `6b71f471bf95c3a7cbd8ea496b84d99c376d5147c05a633b19600f7293033ddc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=candidateReviewed,productCandidate,baseline,assembly,assemblySha256,clock |
| `work/engine-ground-lifecycle-review-r1/extended-probe.json` | 140953 | `c24ca011d3bd743b8b40ec67abab0fe4571251d900d078a5a2a9a53fe3fde5f7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=candidateReviewed,productCandidate,baseline,assembly,assemblySha256,clock |
| `work/engine-ground-lifecycle-review-r1/result.json` | 2670 | `bc2718c9f8b0b04717b9915d9eb80125ca26b7e87250e1968dfd3928c416da87` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,status,candidateReviewed,productCandidate,finalBaselineBound,baseline |
| `work/engine-ground-lifecycle-review-r1/review-contract.md` | 6547 | `180f1a72e98c7375e366411fc3bdbf971eedf592bece90963528e3e88c8f7c41` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-ground-lifecycle-review-r1/sealed-material-binding.json` | 766 | `d8abec97117b04ae4bdc00af565f1133998499574af708239e093e3bba0e2f47` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=checks,note |
| `work/engine-ground-lifecycle-review-r1/source-pins.json` | 134066 | `19c5f764b9f9d69fcc89e60dfca0f57b48787f4bea9750b0d94af40d4ae935d7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,sources,scope |
| `work/engine-ground-lifecycle-review-r1/supplement-probe.json` | 7405 | `189cc5d74e00b95fb1b0c6fcd836e91941173deac9593c2790a3c946eb818666` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=candidateReviewed,productCandidate,baseline,assembly,assemblySha256,clock |
| `work/engine-ground-lifecycle-review-r1/testplan.md` | 6037 | `ede9258aa788b82f00a0fa60e299fec93912f2387b4abd6abef92a8a25cef54c` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-haste-actions-r1/HANDOFF.md` | 4706 | `51b058cd92a28ce2141d6839d3a44bc071526f1fb7c46c6dac91e44cea71c008` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-haste-actions-r1/SOURCE-CONTRACT.md` | 5664 | `a7a1d980f7cda91722695c67fc1b8f9eec2c6253f1dfd821f2d1df044b656d61` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-haste-actions-r1/VALIDATION-NOTES.md` | 861 | `75ed9789e5f35312aa9bc1a8656e1eb5480bd64d8880a432abcef71bfff417ce` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-haste-actions-r1/changed-files.json` | 232 | `28113b43c3c7833954ad93646f8a2b5352fc4d1be7a8972ec1b3e976d09b6126` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(1);keys=path,baseSha256,candidateSha256 |
| `work/engine-haste-actions-r1/delivery-manifest.json` | 45964 | `ecf4a8642d13238222fad3173890dc1063bcfc311fc7aabe101d6b07465831b4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,status,files,excludedDirectories |
| `work/engine-haste-actions-r1/input-pins.json` | 4525 | `a7cb8935c4e640f4096200839dd77a674bb081906ebc8a39e1f741353b7c559c` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-haste-actions-r1/preservation.json` | 598 | `0ef5ac6bb8ffcfa8b31b309898ceba619cb6c78fe998e7a3b3c40bb1d338b94b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,sharedSourceAudioWebFiles,sharedAllMatchR30,independentCandidateCopies,sharedWrites,productChangedFiles |
| `work/engine-haste-actions-r1/result.json` | 2418 | `18725fb5f2d679a70b4d65b883385aa58206e91dbf0ef7c9487b76582edd88e9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,task,status,checksPassed |
| `work/engine-haste-actions-r1/source-before.json` | 42580 | `f1e786fa7e06ee84911579c9a9d7371924331669b0d1ffb9d28767c6c21b73bd` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-haste-actions-r1/source-manifest.json` | 43222 | `b7d663225973833c6576b9400c32f9afd7161c4517d02f51877796630d08f98e` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-haste-actions-review-r1/CONTRACT.md` | 7957 | `7504904112f7aa5fd0bc7e368332e5efb572cbca2cd64d29099041361f61ca26` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-haste-actions-review-r1/HANDOFF.md` | 5213 | `35cb7094a508e8ad0669b4fd9264cb93f5cbd067c679e7d267b2964a39af43bf` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-haste-actions-review-r1/action-metadata-vectors.json` | 87305 | `6afd114d3c53be54dfc2a0116cb71358c45a0df51ac64546e13d2de64f4afc9e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,profiles,rows,spells |
| `work/engine-haste-actions-review-r1/artifact-manifest.json` | 10781 | `1083f4052b4f175cb2dd93ea93a3abfa8eb08aed9280777efb1fee3ff4f12bae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,artifacts,excluded |
| `work/engine-haste-actions-review-r1/commands.json` | 1264 | `d2638c6015605d95c8235f1228d9fa24d6cb2dbc361aa329d001cd337233d128` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=commands,dllSha256 |
| `work/engine-haste-actions-review-r1/gap-analysis.json` | 5653 | `5a4b3aad429601ad2745a76869b8e2e5159fcea17fc33c1f216b75709ecb5414` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,checks,passed,failed,originalNamesRetained,originalUnchangedExpectations |
| `work/engine-haste-actions-review-r1/haste-metadata-vectors.json` | 155958 | `4e6cb81ecfe9197fbf2d8b4d4496ec258355ee723367549a701de79f4320939a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,profiles,rows |
| `work/engine-haste-actions-review-r1/preservation.json` | 618 | `77ccd7bdff050212523a4a83d456ef047f015ec19b09dcddb46d8fde68c89892` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sharedR30SourceFilesVerified,inputPinsVerified,previousReviewArtifactFilesVerified,engineAssetsJunctionTarget,engineAssetsWri |
| `work/engine-haste-actions-review-r1/probe-summary.json` | 19105 | `7c156c9028b21e4432a64dd7b7c936a23b795f6a6dd3d3389183ff4d580c29cb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=checks,passed,failed,failedNames,status |
| `work/engine-haste-actions-review-r1/r30-probe.json` | 804070 | `4cbc201ca8e7f144b36611268fcccea04ee8bfa99f68f7735e1ba933dd2e4328` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,assembly,assemblySha256,checks |
| `work/engine-haste-actions-review-r1/race-vectors.json` | 3491 | `d8412c579779c8e48995c4961a0bbe505f634f1416a41ba1d0555eed7492bdcf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,executedOnR30,futureCandidateRequirements |
| `work/engine-haste-actions-review-r1/result.json` | 2291 | `10ad866794d1ff0812fe0a1ada18e379fb6553f1d80578f470650790b6c9c5d1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,taskId,status,baseline,baselineManifestSha256,baselineReleaseSealSha256 |
| `work/engine-haste-actions-review-r1/source-pins.json` | 56308 | `0d3fbcadb77bf69c42849a6dd71b48c9b9a4fcf3177733936f944ec4748336d7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sources,goalObserved,goalModified |
| `work/engine-haste-actions-review-r1/source-qualification.json` | 2198 | `5c336e0ca6aaa20104ca4beda8f8943f5863da590488d8f7f6534d66b65f3c92` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=baseline,goalObserved,goalModified,sourcePinsVerified,nativeImageBase,nativeFunctionVA |
| `work/engine-haste-actions-review-r1/test-adaptations.json` | 35564 | `59d30cfa7c09b0017fabc46330aeb41f7b312be1ecda61bda6423d5c4d360369` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=originalSuite,originalSuiteSha256,originalChecks,oldProgramSha256,oldExtraSha256,retainedCheckNames |
| `work/engine-next-integration-r1/HANDOFF-r29.md` | 5065 | `9fc67df31fbc96456b71ea8eeba81371519514942f00571b871c14aa6f557510` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-next-integration-r1/HANDOFF.md` | 7642 | `366b8198230b08a7c3fa0958a7cc8a2fa0bd1b4873e8c5791bdc05f505f70f3f` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-next-integration-r1/acceptance-materials.schema.json` | 10819 | `c0807931c0ff015b4ac3bcb2f1a0ec7618da00bafe31ff118490060517bfb2c6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=$schema,$id,description,oneOf,$defs |
| `work/engine-next-integration-r1/artifact-seal.json` | 2661 | `79be3175a3b560eb873ef6079a77795f122c86332f76ba49ac8f5b7aaf94f104` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,excluded,files |
| `work/engine-next-integration-r1/baseline-lock.json` | 33239 | `2f810caf6d3f64a857173637416ebbd6e0ed6a104f67ef985706de213cadb1f4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,projectRoot,engineRoot,assetsResolved,assetCount,audioDllSha256 |
| `work/engine-next-integration-r1/preparation-seal.json` | 1818 | `f987628e668cfadc77f92b0245f1df19e4fedfb68e71ef2624f4a51ccd2ba21b` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-next-integration-r1/r29-integration-deliverables-seal.json` | 8586 | `3835ae20eeb305fa3bb39fe56d17f6f82bc201fd655a3f612a2dd0252da34aa8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,releaseSealSha256 |
| `work/engine-next-integration-r1/r29-integration-result.json` | 4134 | `6cd83d6e23a15d4f3723726088d248113faa6947c97973b8a1df617df5436b81` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,candidateId,revision,sourceFiles |
| `work/engine-next-integration-r1/result.json` | 2248 | `f4549117c707f04c15a9a99fd7edb6d142eb6cf7913c278e547f2c71425eea4a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,revisionPrepared,candidateAccepted,sharedEngineMerged |
| `work/engine-pickup-asset-review-r1/HANDOFF.md` | 3091 | `7630dddd5a88b8af8026859202a77420d85d296e646b92834108127fbb432ef8` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-asset-review-r1/archive-audit.json` | 1024453 | `884cbb8ff227bb40d679723bd90641358a8cfa05535e6331a4b6eb18e3ca6f95` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=idxCount,idxUniqueBinarySearchRecords,lookupCalls,changedSizeShards,wholePakHashesComputed,mainSelected |
| `work/engine-pickup-asset-review-r1/artifact-manifest.json` | 76618 | `b0f5dd9e1e0469c9c6ef3e0067d8b41e1fa9f2961ea86be25b46c2247d66805e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,decision,exclusions,files |
| `work/engine-pickup-asset-review-r1/audit-summary.json` | 725 | `6990375f7bb9ab24b0de0f0cdc9c34eeae391f7f6ef2b1788ffc8e3a77ae157d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sourceArtifacts,sourceExternalPins,independentExternalPins,profiles,gfx,profileKeys |
| `work/engine-pickup-asset-review-r1/contact-index.json` | 2311 | `a00824bb9e43875ea0119a57d3a53f162846d15cc343694123a1246f177e3efc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(13);keys=gfx,sprite,path,unionBounds,frames |
| `work/engine-pickup-asset-review-r1/decode-audit.json` | 265617 | `14a4a0bbd0f6fd88df5b5b99e7fa09ffda992f691f246fcdca45f9a95b397ef2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=implementation,entries,indexed,direct,frames,blackOpaque |
| `work/engine-pickup-asset-review-r1/execution-receipts.json` | 577 | `d4ffa1d5c4923cb49e651ab06778cfcf8b0867229f16defc887d31a915210599` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=audit,managedReference,comparison,visualImagesActuallyViewed,noFullPakReads,nativeExecution |
| `work/engine-pickup-asset-review-r1/findings.json` | 1846 | `f6d961430a314edee3cd426d8657d5735f8682df970369fefaeeea695783dacb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=blocking,boundedAcceptance,qualificationGaps,reviewImplementationNotes |
| `work/engine-pickup-asset-review-r1/metadata-audit.json` | 431650 | `3548e5b051db0f8a54d3c132f3a7d7cf3546f4924a3cffcd52f5a5911f38dffd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=profiles,gfx,profileKeys,reuseProfileKeys,missingProfileKeys,uniqueFrames |
| `work/engine-pickup-asset-review-r1/reference-comparison.json` | 166315 | `bced258bde02dcdce878aad08bb6213a8c5fa4275dd1fb932b53471589eb2ae0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=frames,comparedCanvasPixels,differentPixels,freshP04OutputMatchesSealedReference,method,rows |
| `work/engine-pickup-asset-review-r1/requests.json` | 8465 | `22f8d68c3403818f2895d3686b62fad4221e3a90b2bd3d593cc42b489cd3fe91` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=2786-10.spr,2786-11.spr,2786-12.spr,2786-13.spr,2786-14.spr,2786-15.spr |
| `work/engine-pickup-asset-review-r1/result.json` | 5811 | `80613924513ab0815b70ec40dcc09634fd11e1d35d54e3c5a01c8bc717e44a08` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,decision,scope,sourceSealSha256,sourceResultSha256 |
| `work/engine-pickup-asset-review-r1/review.json` | 5800 | `e63f13f0548eae9b00d490289f45d79acfb7b1c4b28feb0fe6a643a4f5c875c0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,decision,scope,sourceSealSha256,sourceResultSha256 |
| `work/engine-pickup-asset-review-r1/source-pins.json` | 438595 | `487586050cf5d71ec81eb24c2591bdb1114a3f8f00e8cebcc1c6f942e08a54d0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sources,wholePakHashed |
| `work/engine-pickup-asset-review-r1/visual-review.json` | 1132 | `c55e6fff445156efbe4df039e216a3a6429a25ca40c58e57b2781b0b64ab450f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=method,sampledGfx,directionsEach,tokensEach,sampledFrames,all416ManuallyViewed |
| `work/engine-pickup-asset-source-r1/HANDOFF.md` | 3674 | `a945fec04bb0b466e348cbda35d73067039375885137b9a4e499324b1587c840` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-asset-source-r1/SOURCE-CONTRACT.md` | 5469 | `1761be09e6ba1d38131b12dc3c9b113a9fa5f299ef5f79b3ae46b0f04309ccf4` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-asset-source-r1/VALIDATION-NOTES.md` | 3943 | `ab7e1991507a3db8b659053364a65d3a7f10fe1f021b7309157dc65cb3796c6d` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-asset-source-r1/archive-identity-history.json` | 6756 | `6a98db9707ea3cffec9ae6c7706919fdfd1f91e7dc1583c7030acad4b0aa4f08` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,source,sourceSha256,sources |
| `work/engine-pickup-asset-source-r1/archive-identity.json` | 12681 | `48c5ad410d983550174ca2cd119274da7256666013574765ea3430eabae2a899` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=archives,historicalIdentitySource,wholePakRehashed |
| `work/engine-pickup-asset-source-r1/artifact-manifest.json` | 214103 | `b9033cb8c0767eccd9eb8c739b0eb6a42d48834f81960404e84e418b7dad8efc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,status,files,excludedDirectories,excludedFiles |
| `work/engine-pickup-asset-source-r1/asset-source-list.json` | 67938 | `21cf1b046c796357195979944d66a767b00449e1063b051879dd4485c12ada29` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=productCandidate,productChanged,candidateBuilt,uniqueFrames,requiredSpr,sourceEntries |
| `work/engine-pickup-asset-source-r1/contact-index.json` | 3687 | `00524df5935f4afdda8cf547557846d1f1e3690a6ac012edeca417129ab22bac` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(13);keys=gfx,path,width,height,logicalUnionBounds,metadataOrderOnly |
| `work/engine-pickup-asset-source-r1/entry-identity-layers.json` | 193191 | `f05cbd6ba720ecbc3cbf62152e4b051b1200df4c501d9375858b9932ce069c61` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,historicalPayloadMatched,currentBoundedOnly,rows |
| `work/engine-pickup-asset-source-r1/entry-map.json` | 654651 | `4005d68ac240aa452c5f7f927b535951dbfe1e5260c218754e9cd88a18fecdbc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=entries,errors |
| `work/engine-pickup-asset-source-r1/execution-receipts.json` | 1468 | `d4fe0a02e8c9f7d8f208da8be7d26f4af46d2b80b1adfbc25900df069c1f3b00` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,python,runs |
| `work/engine-pickup-asset-source-r1/existing-frame-comparison.json` | 99298 | `b27ae22ea7cb524f34ce2ab1823511b9bbc47c0edb6972a416f0997452bffeb2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=rows,allEqual |
| `work/engine-pickup-asset-source-r1/frame-map.json` | 1444778 | `87c860778fc03287684bb1c57bef909844fd9162ffa52961f8d43a77e152c264` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=colorProfile,frames |
| `work/engine-pickup-asset-source-r1/gaps.json` | 6103 | `c01fe9abbbaebba0c114a807b1875cceaab95e53c643ca254106de84bfade3da` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=metadataGaps,selectedPixelGaps,missingOrUnsupportedSelectedSpr,archiveIdentityCaveats,allWholePakHashesCurrent,currentIdentit |
| `work/engine-pickup-asset-source-r1/index-probes.json` | 457128 | `825c8ecb267fc6ac2d713eb7bf48582236e09940bd0fe1f25e70c739d08d9e30` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,records |
| `work/engine-pickup-asset-source-r1/minimal-asset-source-list.json` | 1106578 | `18328dc13d953e60340674442ab6129420592b67da1131e708d8e724ad016e48` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=productCandidate,productChanged,candidateBuilt,scope,existingUniqueFrames,newUniqueFrames |
| `work/engine-pickup-asset-source-r1/pixel-comparison.json` | 156559 | `c6c53efd5950bb551c29800f065b610c28d6b023b21436c5465d9ecad7a57078` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=reference,frames,allEqual,comparedPixels,rows |
| `work/engine-pickup-asset-source-r1/profile-qualification.json` | 26552 | `dffcfc88bf57ee61ffebec7bee18b7b2b405f8f343feacb185ac46fd050aef04` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=profiles |
| `work/engine-pickup-asset-source-r1/result.json` | 1436 | `163f1e6d2df4e8f11e9d7800ee2da4de1643e4e8caa1b3087858902f568b8c63` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,status,metadataQualified,pixelsQualified,pixelQualificationScope |
| `work/engine-pickup-asset-source-r1/sequence-map.json` | 488686 | `10d58f87ba19a234942b619041a1f7989b7d41ced236954a192892b785c3fb49` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,profiles |
| `work/engine-pickup-asset-source-r1/source-pins.json` | 59679 | `1607e7185b4271a3f3b2ad2c5f9a271fe83a1cbb00d8e7be13516dffefecb6c3` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-pickup-asset-source-r1/validation.json` | 1779 | `6fddac3da2d5db20384b2564af99e10256ddc883f037f38f6bd396406cf1c5f8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,metadataProfiles,uniqueGfx,uniqueSpr,selectedPakBytes,indexSearchRecords |
| `work/engine-pickup-asset-source-r1/visual-review.json` | 676 | `491db926f997d763b5d7e0514a80754162268a6a45c83d435e55254fc3c1282a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,sampledGfx,allDirectionsViewedForSamples,observations,all13VisuallyInspected,all416NumericallyVerified |
| `work/engine-pickup-own-profiles-plan-r1/HANDOFF.md` | 5667 | `67b08ff2b361f6967de092f8b4ee40ca552aa0ee021bd25285197cc25ef0e077` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-own-profiles-plan-r1/artifact-manifest.json` | 2113 | `7e5605b111825be0e3d719bfea230743b11f7ed65e41b259eea23929e9616904` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,files,excludedFiles,productSourceChanged,productBuilds |
| `work/engine-pickup-own-profiles-plan-r1/body-groups.json` | 98677 | `138eba9701712e0e0883cad42f03844eb3e3e3fa849a666684289eb6598d0082` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=groups |
| `work/engine-pickup-own-profiles-plan-r1/candidate-tests.json` | 4454 | `9731f0079dd9647e790a0583e834f4ef19d306346f60001e802f527d40a44315` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,executed,productBuilds,productRuntimeChecks,nativeRuntimeChecks,cases |
| `work/engine-pickup-own-profiles-plan-r1/goal-read.json` | 270 | `766e1e210f3b6c1ad14ed2a3dbf24df0b49df26c1faa9d8e7d04447b9452da3d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskStartActualStatus,existingThreadId,created,updated,resumed,authorization |
| `work/engine-pickup-own-profiles-plan-r1/mapping-checks.json` | 170478 | `727914504e428e15afdb952be68a1555f441c7bccfbe978645a20e3b53fd839f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,assertions,passed,checks,productBuilds,productRuntimeChecks |
| `work/engine-pickup-own-profiles-plan-r1/plan.json` | 1791 | `572264093eccb1343cd8f44165f34d9d9a1c52fe0867400baad60c41e6337f36` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,profiles,alreadyImplementedControl,proposedAdditionalProfiles,gfxGroups |
| `work/engine-pickup-own-profiles-plan-r1/profiles.json` | 557981 | `6b447c0ee2dd4677fb04b41c3384630a4b67d604a0201684b1f2cc193a39349f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,profiles |
| `work/engine-pickup-own-profiles-plan-r1/result.json` | 1704 | `6b0cd8221231b2cd4d01fef21322fa7248551f21896651a005ed64e8384e0a15` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,status,profiles,controlProfiles,proposedAdditionalProfiles |
| `work/engine-pickup-own-profiles-plan-r1/source-pins.json` | 51974 | `df14f3b7cd452e9c9b9d28696edca04d79a5fa95e598e9d6b40e9f3fe59d9297` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-pickup-own-profiles-plan-review-r1/HANDOFF.md` | 2517 | `e894d4075d9ed5d75f7a2af2ffaedf047559ee283b805befb5a5294af2d6c251` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-own-profiles-plan-review-r1/SEMANTIC-REVIEW.md` | 5977 | `48c6694d855ee89c2d21251765fc50e934017e68684eca293b26840b6eef5faf` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-own-profiles-plan-review-r1/artifact-manifest.json` | 3539 | `dd828cb731a64511b8bce0c48fdacdca862a35fefe3b470dec756a11aff71a0b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,decision,independentStaticAssertions,sourcePins,productBuilds |
| `work/engine-pickup-own-profiles-plan-review-r1/candidate-test-review.json` | 4748 | `4aabe543bbc1fd5f8667a0eaf735bf7b60eec306927cef33da487d9b1bd38692` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=proposed,executed,reviewDisposition,rows |
| `work/engine-pickup-own-profiles-plan-review-r1/checks.json` | 1248727 | `f76de1b2dd87978c5c9b556af30b194beb754571fa97be8053ff86f399f0e0e4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,assertions,passed,failed,author1612Included,checks |
| `work/engine-pickup-own-profiles-plan-review-r1/combat-review.json` | 197334 | `1af4486c3cf19b239857762cf2f50de2086a67f11366c9aed92a28f7c43b0285` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=profiles,rows,damageBoundaryTriples,sameGfxWeaponTimingDifferences,productRuntimeChecks |
| `work/engine-pickup-own-profiles-plan-review-r1/execution-accounting.json` | 1596 | `bce091062812ef656d0ed4d708bd499fe54f72a62f736879bb50408e2bb40993` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=acceptedRun,authorHelperImportedOrExecuted,author1612Added,r34Author402Added,r34Prep84Added,r34Independent184Added |
| `work/engine-pickup-own-profiles-plan-review-r1/findings.json` | 5836 | `d077a3e92cdd083e1eaed2dacffc0c64710189e2737814c61f7f1720700a2644` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=decision,blockingDifferences,repairRequests,findings,scope,productExecutionEvidenceAdded |
| `work/engine-pickup-own-profiles-plan-review-r1/frame-review.json` | 124962 | `69c57cce5140ccea49e9c8c639f552bf0ea41ffd1d68b331e00681953463561a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=frames,decodedSprAgain,rasterFromAlreadyAcceptedBlocks,payloadWritten |
| `work/engine-pickup-own-profiles-plan-review-r1/namespace-review.json` | 36483 | `a79e8380d28fe11ebae251be73ab266ab778c8e7656d6929bff2c51e8aa5810e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=bareKeyCollisions,safeIdentity |
| `work/engine-pickup-own-profiles-plan-review-r1/profile-review.json` | 42705 | `39e9d7578199cc8d68eedfd569c0c852972c7400ff5fe2f87f80b7de96cc3f07` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=profiles,exactAllowlist,additionalAllowlist |
| `work/engine-pickup-own-profiles-plan-review-r1/projection-review.json` | 1035 | `2aeec1a52816ef89db63eabc268cac9cd780679c40da4489b4aa2e90c170c303` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=bytes,sha256,counts,structureSha256,written,recomputedFrom |
| `work/engine-pickup-own-profiles-plan-review-r1/replay-diagnosis.json` | 4177 | `2b4453b54dcf43ebf78f48e3c3e2aa39915e2a02afe54e8fe213bb2a1feae74b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=diagnosis,comparison,files,setTraversalAudit,productOrPlanRepair |
| `work/engine-pickup-own-profiles-plan-review-r1/result.json` | 2476 | `d2d00543129ab6f02ab1c20ed8e0aa763880de082322365018a8d4cc9829d18a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,decision,status,candidateId,candidateSealSha256 |
| `work/engine-pickup-own-profiles-plan-review-r1/source-pins.json` | 89283 | `792ceb9b3c00107b744c8f890833bce73e184ea5fad30dbfd8d94bd2e9cad37b` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-pickup-own-profiles-plan-review-r1/summary.json` | 573 | `eec14309fbae7d041bb27957f572e564330d18a6d79651f2190ec76e95e1d2ec` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=decision,assertions,passed,failed,sourcePins,planArtifactsVerified |
| `work/engine-pickup-own-profiles-review-prep-r1/HANDOFF.md` | 3380 | `cf2e6edc44da90d1b004adb209f0cace33ab4359f5255879e452fcbe1f788f01` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-own-profiles-review-prep-r1/active-window-slots.json` | 696586 | `d7b6a63b30af33e1b1e9b4c432febef4687060695d961b58c9e1d185424b67e2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=rows,plannedScenarios,plannedAgeSamples,executed,policy |
| `work/engine-pickup-own-profiles-review-prep-r1/artifact-manifest.json` | 74745 | `2f56ff408f7a2b96197d64313ec898f962ac1c53b49df8acc5b8a7b6cb316a66` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,status,staticAssertions,inputPins,fixtureFiles |
| `work/engine-pickup-own-profiles-review-prep-r1/base-reuse-slots.json` | 162452 | `03ff54653e78ed2d5c685af4a1b27ff30848d3cc071516edb5d6d4a6c3668241` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=rows,planned,executed |
| `work/engine-pickup-own-profiles-review-prep-r1/body-fixtures.json` | 375903 | `efa606697a17c321ab61faf151a53ecdf47ec38d50ea763283ab1c68dc1a58bb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,rows,uniqueFrames,bodyOnly,counts,pixelFormat |
| `work/engine-pickup-own-profiles-review-prep-r1/contact-index.json` | 13867 | `f421f9a055fa76917fc21e625ded37548a0fc4a90e18b053fc1271d68bc6a7ca` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sheets,overview,overviewSha256,contactFrames,bodyPngCount,visualInspectionPending |
| `work/engine-pickup-own-profiles-review-prep-r1/duration-table.json` | 147688 | `fb0768a3aa9da75af21e7c527cecd9f7794d202273339418a8c4b48a6c8d3aa5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=rows,staticProfileActionRows,productTimingMeasurements |
| `work/engine-pickup-own-profiles-review-prep-r1/fixture-manifest.json` | 73204 | `baf898adc1df0e48612ddbb25ed72dec2db439da99c3f32baaf396bd0e14befe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,sourceScopeSha256,inputPinsSha256,candidateBound,finalR34Bound |
| `work/engine-pickup-own-profiles-review-prep-r1/generation-receipt.json` | 480 | `b58111db6d054963e41ca87de1139cd60c52a33bfd6089c6e84677a5721eb41d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=staticAssertions,pins,png,mappingRows,contacts,productBuilds |
| `work/engine-pickup-own-profiles-review-prep-r1/input-pins.json` | 83333 | `b74969baf143b002b0034bb8804cbfccf4fc76d7572c804e3cdb2cef0416577e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,count,scopeSha256,priorReviewSealSha256 |
| `work/engine-pickup-own-profiles-review-prep-r1/namespace-fixtures.json` | 36538 | `06f3bdea9c3a3da5369ffe26da27942a6609bd241d7befeae0ce84549a1afa83` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=collisions,barePickupKeyCollisionCount,mustUse,noForGfxEligibilityFallback,weaponChangeCancelsEvenWhenBodyShared |
| `work/engine-pickup-own-profiles-review-prep-r1/owner-change-slots.json` | 18818 | `f737208fccff7896a808c5c12bad7b2fa6e07ed32115f03fa42bdb5774e78ad4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=rows,executed,note |
| `work/engine-pickup-own-profiles-review-prep-r1/planned-mapping.json` | 4345492 | `335960335d176dd6d623934fc446ddaa35f8e9df2ff5f76f88da41b3816d7804` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,rows,rowsCount,sequences,productAssertionsExecuted |
| `work/engine-pickup-own-profiles-review-prep-r1/profile-inputs.json` | 14706 | `d20d3fac02704a095c2c5ad2f506aae836cd9bf67b9a8271033532561818c5f2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pairs,worldSeed,receipt,metadataTokens,nativePrivateNeutralKnown |
| `work/engine-pickup-own-profiles-review-prep-r1/projectile-selection-fixtures.json` | 5030 | `64b2a9e8d4a53fdc670e92ed748aa37a19c9d7597a78d71be25434e21b7ad4d8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=rules,vectors,realProjectileFutureCases |
| `work/engine-pickup-own-profiles-review-prep-r1/result.json` | 2287 | `945760f87966ed206dd185cdf5f8f00ce44d039342b10de905ef1ab0df056189` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,status,scopeId,scopeSha256,acceptedPlanReviewSealSha256 |
| `work/engine-pickup-own-profiles-review-prep-r1/static-checks.json` | 410357 | `c98c923c375539aeb281a7fc75935e58b49b1e3a10246e622406bafd0bf8a6fe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=rows,assertions,passed,failed,productRuntimeAssertions,prior2915Included |
| `work/engine-pickup-own-profiles-review-prep-r1/test-families.json` | 330483 | `3f54abeeff484c21db97d47b2c33e57076a971bc05acaac163b8b91657fedab0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,families,familiesCount,allProductExecutionCounts,sumOfFamilySlots,counting |
| `work/engine-pickup-presentation-candidate-review-r1/HANDOFF.md` | 2553 | `5c4ed9084e5c5f0b4746ad0197db8fb5f0bb72e8ff9ef24dc37be144e757e4d0` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-presentation-candidate-review-r1/STATIC-REVIEW.md` | 3030 | `6b7cac75004830dcafd0bbb4f0c7e8918baf36e8d37d4e1ba70523de7c2dbb19` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-presentation-candidate-review-r1/accepted-contract.json` | 8517 | `8c60b1061c637bcb7f992d7f92c74fc80c0ad945c7f05d1c3a2d74507cffc6f2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=id,status,baseline,scope,trigger,observableAdmission |
| `work/engine-pickup-presentation-candidate-review-r1/artifact-manifest.json` | 72114 | `715c95654af8a1c59853f15e41c3511d5b15c1b0749bf35c47e39fe918b99196` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,reviewId,decision,files,excludedMutableNamespaces,selfExcluded |
| `work/engine-pickup-presentation-candidate-review-r1/baseline-pins.json` | 61289 | `07e6c5a6649eef932e8ff4205d382c5463e0548c9f54633e561e61e965952352` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=baseline,sourceFilesVerified,sourceStageSealVerified,productDllVerified,audioDllVerified,fullStageInventoryRehashed |
| `work/engine-pickup-presentation-candidate-review-r1/coverage.json` | 75839 | `862df5ed2253c08872e402e866c57fab9ff015b389c9fd0fe34f29ce1aa1968b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,riskFamilies,counting,rows |
| `work/engine-pickup-presentation-candidate-review-r1/execution-accounting.json` | 1361 | `5ecf154cd6e8f9874a3c1b5e4a4ada6a55f084ddd19e0c1611c03c8906ca1f0d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=acceptedRuns,excludedDevelopmentRun,earlierCompileCorrection,finalHelperBuild,authorAssertionsAdded,prepAssertionsAdded |
| `work/engine-pickup-presentation-candidate-review-r1/execution-rows.json` | 379060 | `a10c83bd5820680144211e6cb85854033e61d89d81f6e4544ffe1f2052856585` | U — U14：可重構素材／封包欄位 $.rows[].actual.evidence.cover.Tile.Pixels；真實或合成來源 unknown |
| `work/engine-pickup-presentation-candidate-review-r1/findings.json` | 13843 | `0362e84f9f44cb916a7cc74e3ac3ce93cfb2749ac1ef058edd16c3f81cf9d49a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=decision,blockingFindings,repairRequests,verifiedFindings,qualifications |
| `work/engine-pickup-presentation-candidate-review-r1/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `work/engine-pickup-presentation-candidate-review-r1/integrity-review.json` | 723 | `ee5f426f0d100ff65055a13e0102f4cd34615e23fcc0691e0c171c0875dbf8c7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=candidateSeal,candidateResultSha256,sourceManifestSha256,candidateArtifacts,candidateExternalPins,sourceFiles |
| `work/engine-pickup-presentation-candidate-review-r1/pixel-review.json` | 470 | `aa9fb111634ee074a820f87a85557ceeeb40cc4949872766c436b45559f93ef5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=coreComposites,acceptedUniqueBodyFrames,coreCompositePixels,coreBodyPixels,channelDifferences,extraSyntheticOcclusionComposit |
| `work/engine-pickup-presentation-candidate-review-r1/preservation-review.json` | 337 | `08bf0df8e76b118fcbe17514670c82560cc1a95bcca77005a9efcce2d2308780` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=groundSenderCompletionTextExact,unchangedSourceFiles,itemSourcesUnchanged,wholeExistingSuitesRerun,groundSenderCompletionByte |
| `work/engine-pickup-presentation-candidate-review-r1/result.json` | 3292 | `81481f6fc05d19baea5af17a1338638c5d038708182961db47ef4864d063cb0e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,reviewId,candidateId,status,decision,candidateSealSha256 |
| `work/engine-pickup-presentation-candidate-review-r1/review-acceptance.json` | 1981 | `5ba17f25a978b146a282f14fce8f477a60307303451ded1b9bca916b56411d4d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,reviewId,candidateId,status,decision,candidateSealSha256 |
| `work/engine-pickup-presentation-candidate-review-r1/source-diff.json` | 3245 | `fcae5bb250faecdf1c30c500ab6157bc203ea41f85388bfb6782c59fec1444c9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sources,baseline,unchanged,modified,added,changes |
| `work/engine-pickup-presentation-candidate-review-r1/source-pins.json` | 210797 | `06fb945708e6470b701028fc7b2bf1664a36f7043d2fba43a77840c5fba8df25` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-pickup-presentation-candidate-review-r1/test-matrix.json` | 11549 | `e56d02f64e69e433f3e7dc2c986afb4335b48c44a818c63efe44ded3b0b4283a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,proposedRows,rows,counting |
| `work/engine-pickup-presentation-r1/HANDOFF.md` | 6792 | `4d62eee5431719b13fe71a90afec87f962f1b3da9ca718010d541aac50cb8c77` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-presentation-r1/VALIDATION-NOTES.md` | 3592 | `468be24a1131f0b43028dcf55ff1f67d595d19c0102fdf1be25ca32962b39a9f` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-presentation-r1/changed-files.json` | 3132 | `1d9b1c9fc17d3b9fcecfac2e7222e074861005bd3f56c9ff9757394232246f9e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(13);keys=path,kind,baseSha256,candidateSha256 |
| `work/engine-pickup-presentation-r1/coverage.json` | 19368 | `2385d4bd0423a476e382164e1acc2565765de8bcf7af7991b7f3359d89692b76` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=proposedRows,rows,additional,notClaimed |
| `work/engine-pickup-presentation-r1/delivery-manifest.json` | 61912 | `cc7761665593c79b9f82255f5d361bedd6c8ac4563434e7cf7a6e07c5c16cbdc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,status,files,excludedDirectories |
| `work/engine-pickup-presentation-r1/final-base-binding-pins.json` | 102308 | `ddbc4624f0e1ff27cf1f0a19fb4bb21940af42b66b906dc87e87bdb9d2d562ca` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-pickup-presentation-r1/goal-read.json` | 905 | `7247b9f1f28854db702c6b5a2bef24586bd8457c9f022cbed589ff835f2f6e23` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=readBeforeSeal,taskStartSummary,scope |
| `work/engine-pickup-presentation-r1/input-pins.json` | 114372 | `10d51ba78777c6a4832b646d8ecc6ffc4d73cbcf8d3606b01c075d547bef08d3` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-pickup-presentation-r1/preservation.json` | 678 | `c4d5f238f053b3985bc2f6529cb10fa1c5fbe2b799ec605fbf22186006fc5865` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,originalSources,unchanged,modified,added,independentSourceCopies |
| `work/engine-pickup-presentation-r1/resource-projection.json` | 88033 | `d71b951e7117bfd05847694266f28a5b209ff54d722cd1ed8dff462efceb6064` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=resource,bytes,sha256,recipe,inputPins,identityCounts |
| `work/engine-pickup-presentation-r1/result.json` | 1960 | `05983d2858d22a9b8420fdcbe17a03415445ff9315d131be740297e1c1cc5c88` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,status,policyId,checksPassed |
| `work/engine-pickup-presentation-r1/runtime-dependencies.json` | 3073 | `30288c75670a7b6fc2a2fe27a0d7973f8a2a853f9fc16968dee9adbd20ba5f41` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=rendererAssetRoot,constructor,nonMapFiles,nonMapBytes,staticAppContextBinding,copies |
| `work/engine-pickup-presentation-r1/source-before.json` | 54616 | `502d64760f4c3c56ec887c4e5d5bb9f6e56ea34cf8d6a1e3ea3212988af421a2` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-pickup-presentation-r1/source-manifest.json` | 45249 | `337afaa3607e49e0c50f2f388ab8cec8c42f7816e33ed2ee98df6bfb0a48b533` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,base,baseSourceFiles,addedSourceFiles |
| `work/engine-pickup-presentation-r1/test-asset-pins.json` | 2343 | `b3b2ea3af6a050900f22ea50ef38c25c58e70e64e2e21017d501422d7cccc9b0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(6);keys=path,copy,size,sha256 |
| `work/engine-pickup-presentation-review-r1/HANDOFF.md` | 2952 | `fafd24478579e418c27142f28e74d3fc1addf5d8d5af4b74394855ee8797fdcd` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-presentation-review-r1/STATIC-REVIEW.md` | 7932 | `f5457d386662e0f8b3e9824ecb9d5d102ed26ea0113af41664f2b2f482957155` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-presentation-review-r1/accepted-contract.json` | 8517 | `8c60b1061c637bcb7f992d7f92c74fc80c0ad945c7f05d1c3a2d74507cffc6f2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=id,status,baseline,scope,trigger,observableAdmission |
| `work/engine-pickup-presentation-review-r1/artifact-manifest.json` | 97611 | `db2267a456ff5d802ec639b2819251c151407670ad26de5c7a7f85cfefe24229` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,status,exclusions,files |
| `work/engine-pickup-presentation-review-r1/baseline-pins.json` | 61289 | `07e6c5a6649eef932e8ff4205d382c5463e0548c9f54633e561e61e965952352` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=baseline,sourceFilesVerified,sourceStageSealVerified,productDllVerified,audioDllVerified,fullStageInventoryRehashed |
| `work/engine-pickup-presentation-review-r1/coverage.json` | 1055 | `7fd9a323de6eded19809e9aa80536fe6a091c347ce9869248627902f11cbf5b2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=acceptedRun,executedRows,assertedRows,observationRows,categoryCounts,packetOccurrences |
| `work/engine-pickup-presentation-review-r1/execution-receipts.json` | 1270 | `c8c08f67d4c1b2c31560a84c3e676a49cb70bd724ab4e3e28d5de4f75565bedc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=history,finalBuild,productRebuilt,candidateExecuted,helperOnlyBuilt |
| `work/engine-pickup-presentation-review-r1/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `work/engine-pickup-presentation-review-r1/policy-review.json` | 1464 | `173bcc5435d4b24d8cad2964c87a8f8b74a9549889afd8edc9ce84a8c04bd923` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=decision,contractId,contractSha256,sourceArtifactsVerified,sourceExternal44Pins,resourceProjection |
| `work/engine-pickup-presentation-review-r1/result.json` | 1813 | `186b368684dbd4b8eaf315303873c93c9bcdf559bae5643f01776445cd766e0b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,status,baseline,baselineSha256,stageSealSha256,productDllSha256 |
| `work/engine-pickup-presentation-review-r1/shadow-key-coverage.json` | 4178 | `63e3ce11a3bb85869286afa98c8f7159089a17e6282a601b68a61990cfa59adb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,rows |
| `work/engine-pickup-presentation-review-r1/source-pins.json` | 107397 | `e6330ab9db6c254745252adb15a9bac8be063aa6015758e6385443df45db7362` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,scope |
| `work/engine-pickup-presentation-review-r1/test-matrix.json` | 11549 | `e56d02f64e69e433f3e7dc2c986afb4335b48c44a818c63efe44ded3b0b4283a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,proposedRows,rows,counting |
| `work/engine-pickup-presentation-review-r1/visual-review.json` | 776 | `b0ff7b81aa80b37d50e322723acff1cf418838ea9f1ede72494fd54fc7277669` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,images,frameContact,baselineContact,limitations,nativeRuntimeChecks |
| `work/engine-pickup-renderer-eligibility-source-r1/HANDOFF.md` | 9470 | `f2ebf1b62d667dca6940c267e956e803f9bd3213d5a37d995f7ebec6e7b4f962` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-renderer-eligibility-source-r1/artifact-manifest.json` | 24565 | `ad19e01822d2ee6a096dcc7829c4ad5c097a1b48eec4298757e72092fa54b904` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,files,excludedFiles,runtimeChecks,productChanged |
| `work/engine-pickup-renderer-eligibility-source-r1/asset-delivery-plan.json` | 4747 | `ebc81f7364b9114c3e18459ec8a16f5c7423f90647861edc31576eed5cde24ba` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=policyId,method,proposedPath,logicalName,proposedCsprojItem,canonicalSerialization |
| `work/engine-pickup-renderer-eligibility-source-r1/condition-catalog.json` | 12394 | `fddc1d5160631973dc094c200346cb3d8c4445d79b91d4ad423385bd25d6ac81` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=conditions,strictDecision |
| `work/engine-pickup-renderer-eligibility-source-r1/eligibility-table.json` | 745824 | `cf0d80b0c90d3b560f58346e84607912c819b103099e5e18f41193aa9e992128` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,profiles,actorKinds,rows,strictNativeEligibleRows,firstIncrementPolicyRows |
| `work/engine-pickup-renderer-eligibility-source-r1/goal-read.json` | 51 | `bfb3c6e6c389e72f0c0b9df6b45d3be07976baa41a2ff7df19220880fad7679b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,modified |
| `work/engine-pickup-renderer-eligibility-source-r1/metadata-evidence.json` | 74866 | `21b5da78f563db1e2ea36824be0482966e28c1563559f579c9654118d0b10bee` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,normalization,entries,profiles |
| `work/engine-pickup-renderer-eligibility-source-r1/minimal-frame-inventory.json` | 84829 | `6c9ecbe2985f894d4d5247bd31b84cd85e52681b73e37a5442e795093810ca75` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,frames,counts,additionalCanonicalFrameByteSum,sourceIdentityCounts |
| `work/engine-pickup-renderer-eligibility-source-r1/native-byte-audit.json` | 19036 | `5a05426afe5b363d0393c86a4e760c8057be5ca5fe500ccdc86ff7837d7dbb71` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=imageBase,nativeExecuted,chunks |
| `work/engine-pickup-renderer-eligibility-source-r1/presentation-policy.json` | 5802 | `6cdf2e017a6de677c23e316ac8cb022f16948be03bf4ccc34d64f85a008f90c3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=id,status,baseline,scope,trigger,observableAdmission |
| `work/engine-pickup-renderer-eligibility-source-r1/proof-index.json` | 26634 | `9c0237939231af614f00fce203c0099082924a3f4a8e13ce3ba17830ff0a9a18` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=imageBase,nativeExecuted,proofs |
| `work/engine-pickup-renderer-eligibility-source-r1/r32-bindings.json` | 2508 | `a5d13ae825654436f959bd9acacdff511c26905bd764c2bc49fab010a8fed03c` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-pickup-renderer-eligibility-source-r1/result.json` | 3658 | `b5588ec3c3f1b47f49bc9c8a020c7fe2f73eaead72905949c243328fcab1885e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,status,runtimeChecks,productChanged,candidateBuilt,nativeExecuted |
| `work/engine-pickup-renderer-eligibility-source-r1/source-contract.json` | 1655 | `9eacf7334f335cb5b077438dc97352a76c1cd290726edb3e23dde672fa67cb59` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,productChanged,candidateBuilt,runtimeChecks,nativeExecuted,strictNativeNeutralSubset |
| `work/engine-pickup-renderer-eligibility-source-r1/source-pins.json` | 12018 | `1cabd2f93fee2a6d7ab4b2686ac409dd2591d595140fc93d290ca6d505a60600` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-pickup-renderer-eligibility-source-r1/test-matrix.json` | 11257 | `8003ef775c7eace500e14ef4a9243e072561d364fe0e640442479284d3740221` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,runtimeChecks,tests |
| `work/engine-pickup-renderer-eligibility-source-r1/vtable-bindings.json` | 579 | `b47da62a839d5a5b605684c795ad4e9ddf43344ebab262e9421579c6f31c4189` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(5);keys=baseVA,slots |
| `work/engine-pickup-scheduler-review-r1/HANDOFF.md` | 1808 | `69b4f2b9888547a54a8b7f1a5ade0b4e05fea46fd77760a3d84fbed1e88d0aeb` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-scheduler-review-r1/arithmetic-review.json` | 4410 | `44708c394c017dabde3341966713530774b7ea2af03f5d2d19d78020fedc9ca6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,baseInput,baseQualification,rows,latenessCases,remoteBacklogExamples |
| `work/engine-pickup-scheduler-review-r1/artifact-manifest.json` | 5450 | `d8aff3bc56f58b8e216f77d39b39e1b733811e75201b8098715eb0b4a303fd82` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,decision,files |
| `work/engine-pickup-scheduler-review-r1/binary-read-pins.json` | 3802432 | `eb3513eaa410941c3b390c4d4f9f2bd5b1b9e521fc89b5577baf3c672bffad8a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=path,size,reads,wholeFileRead,runtimeChecks |
| `work/engine-pickup-scheduler-review-r1/boundaries.json` | 548 | `cb598ba4b755418368bc4c3a06c0c6de7d53812341e8904507561c380f1d5df3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=runtimeChecks,nativeExecuted,nativeEmulated,jarExecuted,productChanged,candidateBuilt |
| `work/engine-pickup-scheduler-review-r1/claim-review.json` | 9339 | `292a741e3865ab8dbd969de0620cce283695d8dbb9082486a72eff254f7f52fb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=claims,sourceProofRanges,supplementalConnectors,runtimeChecks |
| `work/engine-pickup-scheduler-review-r1/findings.json` | 1651 | `6d6c4a37cb338af9e4154dc36e6abc66dba572a2c22beef67b0122e239028372` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=decision,blockingFindings,findings |
| `work/engine-pickup-scheduler-review-r1/integrity-audit.json` | 45513 | `f00b4f145eef57a4183efba768b1155ca1425bfdc0763dca578ed01f57218d11` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sourceArtifacts,sourceExternalPins,externalWholeFilesVerified,chunks,proofClaims,proofRanges |
| `work/engine-pickup-scheduler-review-r1/result.json` | 1977 | `01a57beb203e028c7cc78e286286d39d3e0e98be2ad4b171a445039f4da1e846` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,decision,reviewedSourceSeal,reviewedResultSha256,reviewedContractSha256,sourceArtifactsVerified |
| `work/engine-pickup-scheduler-review-r1/review.json` | 247 | `0001ffcc9bd3b1985bf2f2723fa8898bb1a0fabbe5c3d9edf7886b6051d72148` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=decision,claims,findings,integrity,algebra,scope |
| `work/engine-pickup-scheduler-review-r1/semantic-review.md` | 9514 | `dbee51c01442b3cf2f1c65328b6de6ff5dd558c0accba1adfdeda6587ffd677e` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-scheduler-review-r1/source-pins.json` | 41413 | `afec810d7b1ba258cfd26356ec76b60ce3eff8c9925d61851a2e7f02377ab78e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sources,boundedBinaryPins |
| `work/engine-pickup-scheduler-review-r1/supplemental-ranges.json` | 735 | `8dcb68e7d0cf7871a99fef945de179239897d0291d0f28092c938be3d92536d8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=VA,endExclusiveVA,containingAuditedChunk,disassembly,fileOffset,size |
| `work/engine-pickup-scheduler-source-r1/HANDOFF.md` | 8121 | `680de2dd45175070609aaf363642595013f03fff3a94cf6e38484e96a3455d81` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-pickup-scheduler-source-r1/analysis-boundaries.json` | 1675 | `c7a8e4b05d22111e3a75b0dfa67493cdb034627d361a06414fb16426549130de` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=method,nativeExecuted,runtimeChecks,sourceDiscovery,excludedLinearData,notUsed |
| `work/engine-pickup-scheduler-source-r1/artifact-manifest.json` | 23759 | `aecf48c342d082c17520351f992a387b880dc980ab4c500143036ce35f86b07a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,excludes,files,runtimeChecks,productChanged |
| `work/engine-pickup-scheduler-source-r1/dispatch-tables.json` | 847 | `4421579bd3e9449fd84aa01741cbcc55cfccc23598b473f162659dae3c192804` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=vtables,consumer226,consumerCodeEnd |
| `work/engine-pickup-scheduler-source-r1/goal-read.json` | 49 | `39e22d83692f3f221822fcc9123436da59d7156fc021dba4beb30bd93d729a6d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,modified |
| `work/engine-pickup-scheduler-source-r1/imports.json` | 94169 | `963a4c0698da528687121cb5f691af4f465467bc60363f2d99bac84585467d8f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(918);keys=dll,name,iatVA |
| `work/engine-pickup-scheduler-source-r1/native-byte-audit.json` | 20323 | `c240da5b9255f4ce7f2ab96ac42df2519cb90c5edd626268cbf2a9d56792e557` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=imageBase,chunks,nativeExecuted |
| `work/engine-pickup-scheduler-source-r1/proof-index.json` | 18001 | `58e6fffd94f9f7ca82ce6c81527e4e0e642d265ea82f5475d0aad8303eaf6f92` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=imageBase,binSha256,staticOnly,proofs |
| `work/engine-pickup-scheduler-source-r1/relative-timeline.json` | 2403 | `c2b52c47eea863baae600fa677335c29f12cf104a54f5e63700610a66147a83f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,origin,assumptions,rows,latenessExample |
| `work/engine-pickup-scheduler-source-r1/result.json` | 3127 | `446361792ed63eb83b5369677d48d637ca7be0e5a8b5d7ae8d3337c178577e0e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,status,runtimeChecks,productChanged,candidateBuilt,nativeExecuted |
| `work/engine-pickup-scheduler-source-r1/source-contract.json` | 6708 | `e81305a50c2ce4186a02930cd694b43e8e9701466b0286ff4105bc089c76db90` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,status,runtimeChecks,productChanged,candidateBuilt,nativeExecuted |
| `work/engine-pickup-scheduler-source-r1/source-pins.json` | 6659 | `e7c581edbd44cb43a84f2bb4a3409e5f99ffd04abe4d82e604e21aab2036ecd2` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-r30-integration-r1/HANDOFF.md` | 8129 | `7aedd3a8788de08fd5034b2346ebeff1feba3716e8af1db98ab7eca1074b5da4` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r30-integration-r1/acceptance-materials.schema.json` | 12087 | `02dfc5343c69b42c81f2cac2a8738822f38e6b5101422fe555c839c77192e254` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=$schema,$id,description,oneOf,$defs |
| `work/engine-r30-integration-r1/artifact-seal.json` | 4493 | `1bacba1502889077b034018dee1014893666d866596af8328ea57abc9ce0db7c` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-r30-integration-r1/baseline-lock.json` | 36974 | `2a360f9828788ad485c10f43bd62931b585abdf2f67c2f333cb8ff406e3ab169` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,projectRoot,engineRoot,baseRevision,basePinsPath,basePinsSha256 |
| `work/engine-r30-integration-r1/preparation-seal.json` | 3742 | `cc98bb3b358a03158c170a6b1fa30434dbeb7b0c39f5b2eeadf57a81943ecef6` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-r30-integration-r1/reference-pins.json` | 2105 | `e6b0cddab727f5a77ae4c42f35be2d75d6eaef26fe80644ae6f9c0735519ebe6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(8);keys=source,copy,size,sha256 |
| `work/engine-r30-integration-r1/result.json` | 2267 | `503f7e34b85d92c001657a97d1ebd8e5a8f22dfc7a1328df3fd10d28265b6b7a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,revisionPrepared,baseRevision,baseReleaseSealSha256 |
| `work/engine-r30-integration-r1/validation-plan.json` | 2695 | `fbabd76bf17122c587be52431592f6cc1e63a8f0a0f87ee6689b59f7756a3501` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,baseline151,newActionsSuite,oldSlow1199,requiredLaterTraceability |
| `work/engine-r31-integration-r1/HANDOFF.md` | 3024 | `b63d1478d630d2efa64db93a7e67fe62be2a091715669d85e236f4cd9b65707b` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r31-integration-r1/MATERIALS.md` | 3926 | `17b9fe1c36a6c087053976ab6fb89e6ff8cb37c17b6e204657d1734f85a8d1fb` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r31-integration-r1/VALIDATION-PLAN.md` | 5129 | `308b1e4f5c0cd819de1ccae605373f6d37bd7c8a96208b492ca268b0caeca2da` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r31-integration-r1/acceptance-materials.schema.json` | 11677 | `22bf9213a643ba1b5b3d70f48da3ee8d0a9b872434168299901c377cab042b5f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=$schema,$id,description,oneOf,$defs |
| `work/engine-r31-integration-r1/artifact-seal.json` | 6003 | `5ee366afa4f559a3ab34f4994117b2cb795687c535756c7f70708f158e207399` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,preparationSha256,files |
| `work/engine-r31-integration-r1/baseline-lock.json` | 37386 | `99223b952e402548bf1d141a976aecca94a24d2b20af6af22bcc79fbca07ba9f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,projectRoot,engineRoot,baseRevision,basePinsPath,basePinsSha256 |
| `work/engine-r31-integration-r1/preparation-seal.json` | 5474 | `1262f8d4e6bf52baa5e48aba28ff9e58a965d79a03c557ca3a20189ea8c1cf75` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,revision,baseRevision,files |
| `work/engine-r31-integration-r1/reference-pins.json` | 7038 | `70571397dee92db7eff1d2a16769f2e20c41c2e4ccc59a4c314e8ef0bb353b7f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,references,previousPreparationSeal,previousStageSeal,testSources,previousSharedIntegrationSeal |
| `work/engine-r31-integration-r1/result.json` | 2950 | `5aa92117684fbacfe67a69bbcbfc99415b76974bbef0ec19047dafae297b03f6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,utc,status,revision,baseRevision |
| `work/engine-r32-integration-r1/HANDOFF.md` | 3387 | `eeee197633331e2c8d3bacca27f35d65572caa7f6e000bea1e1145068e63f1df` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r32-integration-r1/MATERIALS.md` | 4126 | `d8caa53958d56d208e655d69054dd5c9d3f1d6cfe53a7f2ce8b7f0d90ef60967` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r32-integration-r1/VALIDATION-PLAN.md` | 5698 | `f6e38c1c87036e504f979825b95e4e2a329825de30c7a0f01826f8da79c2066c` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r32-integration-r1/acceptance-materials.schema.json` | 11689 | `382024a55d6331dfebcb511c74eeeb83baee1720815d7e40170dee4b8e2b721c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=$schema,$id,description,oneOf,$defs |
| `work/engine-r32-integration-r1/artifact-seal.json` | 10066 | `50f4dc0a0e214d7140ab3b2c90c5bffce62255c2fdc97087161ffb132325ae78` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,preparationSha256,files |
| `work/engine-r32-integration-r1/baseline-lock.json` | 37749 | `7461ddbec2df1f21a2ef4b562d49857b2804e2f1fdcf9cdfbdc982d2737fa395` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,projectRoot,engineRoot,baseRevision,basePinsPath,basePinsSha256 |
| `work/engine-r32-integration-r1/preparation-seal.json` | 9389 | `d4f5f311e323ee66cda2f903b671d0a25bd2b31e814b689b28c603ab6564e863` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,revision,baseRevision,hasteItemSuitePassed,files |
| `work/engine-r32-integration-r1/reference-pins.json` | 11720 | `515fe9c02117f4dac7e6694cf5073cb0e1c2ac068756e82840ad18959b7cacca` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,references,previousPreparationSeal,previousArtifactSeal,previousStageSeal,previousSharedIntegrationSeal |
| `work/engine-r32-integration-r1/result.json` | 3569 | `1cb91f0a7e3a961b9c2701c0d36dfd81c18c96b11d8ea45de5e29ff1895e70c7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,utc,status,revision,baseRevision |
| `work/engine-r33-integration-r1/HANDOFF.md` | 5583 | `1467fd7278b4a2573e6816e92bc52ae8f8613cceec54f2aadb3db6729d0a4c6f` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r33-integration-r1/MATERIALS.md` | 4651 | `989814417d6ece08f7bc5c3fa09a75b2916ace7e9f723610980d44700e3054cb` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r33-integration-r1/VALIDATION-PLAN.md` | 5476 | `9ef626a7e2f8bf8f19810f37bae85e6d71e2a9f12a4eed8c36fe236b299cb69b` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r33-integration-r1/acceptance-materials.schema.json` | 12196 | `c11c96f4ddb35abe9830ecc67bfc43a1daeb28108d12c3d3228982c431c9e683` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=$schema,$id,description,oneOf,$defs |
| `work/engine-r33-integration-r1/artifact-seal.json` | 21072 | `9ffa88dab3a23cf36c9bab6c4a8743be3628fa7a51e745a9124b9cfc629d2ad6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,preparationSealSha256,files |
| `work/engine-r33-integration-r1/baseline-lock.json` | 37977 | `e6fcf047b6b27128c15b6e9cdb983708aefd761a5a770b9d72e38e3f0daf4b5c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,projectRoot,engineRoot,baseRevision,basePinsPath,basePinsSha256 |
| `work/engine-r33-integration-r1/preparation-seal.json` | 20077 | `4f06edf3f5137ad8ec5d29a807bb0cc5973e943c0f117acc938a4c983d0fce4e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,revision,baseRevision,candidateAccepted,groundFormalAssertions |
| `work/engine-r33-integration-r1/reference-pins.json` | 29964 | `5ab66882b1b586130f81b0a846ff9283232cdb852dc5d1e5a26b01279767ff5b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=references,immutablePins,unsealedCandidateRead,unsealedFormalReviewRead |
| `work/engine-r33-integration-r1/result.json` | 3962 | `236559bc79d44fb1346c72a77cd93b3a655305afe933504e9cadb9f55bbcccaf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,baseRevision,targetRevision,candidateFamily |
| `work/engine-r34-canonical-plan-r1/HANDOFF.md` | 7590 | `d9f9ed9163a31b15fdce537436753fa7458c58b9e173bbd8f912e13309a7f833` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r34-canonical-plan-r1/adaptations.json` | 1202 | `6fc07e54e08b4eff3bafdf7b0bc99b078ebb696d6891a1b68d97e17bccd49dca` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=inheritedStageAdaptations,additional |
| `work/engine-r34-canonical-plan-r1/artifact-manifest.json` | 3793 | `46861b32a472c19a472f5f203f685d2f74d8c9fdaa404b17b82a29edbc02d773` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,files |
| `work/engine-r34-canonical-plan-r1/artifact-seal.json` | 4022 | `faf5f661f5d569c950ff854dfa8bff6f1cf9d338d98e3bbf9006fd6555b3a2fe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,files,fullSHA,excludes,executionAuthorized |
| `work/engine-r34-canonical-plan-r1/before-state.json` | 3847 | `70cc332bde82e6339f48f7b3a7e80f97c37d9adc03f9cae2450357e9ca0570fe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=engineFiles,mustRemainAbsent,sealedFiles |
| `work/engine-r34-canonical-plan-r1/canonical-plan.json` | 378638 | `d25c306fae5abee9209e46c557453f7b6a0a96645e5c052fbda70555f11f15ff` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,canonicalEngine,stageRoot,stageSealSha256 |
| `work/engine-r34-canonical-plan-r1/collector-provenance.json` | 1108 | `8cae042b9bfd72d52e3bdbf199fa8f7ff9f95026bdef239e32bc1f6fea2edd2e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,function,extraction,functionBodyChanged,contextBinding,occurrenceSupportSource |
| `work/engine-r34-canonical-plan-r1/command-recipes.json` | 61018 | `1b515878d59e684d174f5b88c1562493437c2eb16f82bb9f701836a61c575839` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,operations,nativeSuites |
| `work/engine-r34-canonical-plan-r1/context.json` | 1500 | `bfb8d5daba6898111e4dc8003584b1076621aa6e8fb85af8950c4cc9b24bdba1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatch,preparationRoot,stageRoot,canonicalEngine,workflowRoot |
| `work/engine-r34-canonical-plan-r1/draft-steps.json` | 20215 | `34d4c6de512acbc95d92bbe037ae6e652e07bd8b2d8ddbcd92c6245de127973a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(8);keys=id,commands,result |
| `work/engine-r34-canonical-plan-r1/nested-product-command-plan.json` | 4442 | `08ddef8d55952364505fc0dee9f75f62b16123e81d326ef10460c38d81885f5a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,commands,productPowerShellChildren,childLogging,sealedScriptsUnmodified |
| `work/engine-r34-canonical-plan-r1/result.json` | 4207 | `9140588652042df8da7e5f09afd5e423bfbae4529f3f766cd6af113225595401` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchId,status,candidateId,revision,onlyWrittenRoot |
| `work/engine-r34-canonical-plan-r1/source-copy-mappings.json` | 77990 | `4005977d7f467eb3c96620e13eadc156740cb1f625477b024870b5e32e0423d6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,assetBodyCopies,overwritePolicy,installOwnedBy,installSourceManifest |
| `work/engine-r34-canonical-plan-r1/static-verification.json` | 1163 | `f4dee06675ee5ef140c0a30a3cbe891c6a4c5bcfda85ddb7297333ff1929c744` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,planSha256,checks,plannedCommands,plannedHelpers |
| `work/engine-r34-integration-r1/HANDOFF.md` | 7972 | `ecff9ab026d01e48895ea058daa966a5c9f73f863415202518117763674381e6` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r34-integration-r1/MATERIALS.md` | 3754 | `716d446b2588f109eaf77baadc54758d74453c547eb68972ee558a7cf0cb4ae7` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r34-integration-r1/VALIDATION-PLAN.md` | 4283 | `70c5a900fde3ab240ec8a7a4a69e0fe2140f88508a395fe93cb99f1139e07da1` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r34-integration-r1/acceptance-materials.schema.json` | 17909 | `9cd3b074ae8a053d1d2b578b491449b1f6635400243dbf2f106a6036badd5bac` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=$schema,$id,description,oneOf,$defs |
| `work/engine-r34-integration-r1/artifact-seal.json` | 34478 | `84717f651d5ee4be53b64b397f8477edb68a78a0a9791510ca86e8eb1940e7e9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,preparationSealSha256,files,excludedFiles |
| `work/engine-r34-integration-r1/baseline-lock.json` | 38506 | `e0d00a412abdd2de672a5845ae10a160dd35f2b5a81fabf3f0e556c419e6d0fa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,projectRoot,engineRoot,baseRevision,basePinsPath,basePinsSha256 |
| `work/engine-r34-integration-r1/pending-materials.json` | 503 | `50748c8957a792d359db08a4ef272f82e70aefe44a23d99edb759006263cd85c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,candidateFamily,candidateRoot,reviewRoot,candidateSealSha256,sourceManifestSha256 |
| `work/engine-r34-integration-r1/preparation-change-index.json` | 5578 | `67206afc22b7b639b520cb2bd6c9798d666a9c9d8ce439b6e4ef0b4a2a38961d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(13);keys=source,sourceSha256,prepared,preparedSha256,patch,scope |
| `work/engine-r34-integration-r1/preparation-seal.json` | 34018 | `51b626cd31218a906d06b1b6afd97e2695923ddc809f127f56989ebf95d1a446` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,files,excludedFiles,formalBuildAuthorized |
| `work/engine-r34-integration-r1/reference-pins.json` | 48623 | `72371c8bbdfb9f15f534a20881ee1dd322ae4b15540934642e97e25106b80a0f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=references,immutablePins,unsealedCandidateRead,unsealedFormalReviewRead |
| `work/engine-r34-integration-r1/result.json` | 3637 | `98068ef87ffe1a99ac0ac251f805a22190c6262e33ccae65c9d5b857da57153b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,status,utc,preparationSealSha256,preparationFiles |
| `work/engine-r34-integration-r1/source-preservation-pins.json` | 34891 | `02cb045088e85c2fc09721b08104ef0aba99fa2db8538e024b8934741b5bda40` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,canonicalEngine,files,referencePins,oldReleasePins,qualifiedSourcePins |
| `work/engine-r34-integration-r1/test-adaptation-inventory.json` | 4530 | `6397715ff0d5a708c13661b72873f9f298052ff37c655f9b116ebdd34b5cb07f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateIndependent,suiteExecuted,functionalEdits,expectedEdits,items |
| `work/engine-r34-integration-r1/workflow-contract.json` | 3798 | `2421e07810cd7793565ca4435392e43d59f98827494337b4c15b2ca5a000a1a1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,stage,normalizedStageResult,canonical,release,currentAuthorization |
| `work/engine-r34-integration-r2/HANDOFF.md` | 6244 | `bda1f5a6f0f1b74415f433cbd91af3c352f2a3d79eca669e4d4fe69e554c0433` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r34-integration-r2/MATERIALS.md` | 3533 | `329beb283c4b35f11b1f63a134d6d82013b1e11fcf5731c599e2e13121d161d6` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r34-integration-r2/VALIDATION-PLAN.md` | 2660 | `a6df1cc4644a13d158a727200f5da17242044e738da2f89c24efd1af722dc4b9` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-r34-integration-r2/acceptance-materials.schema.json` | 20382 | `b48558ccf215fe324bd7fc760a0dbf0e089740d9eec25d03ba001756b28e4c5b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=$schema,$id,description,oneOf,$defs |
| `work/engine-r34-integration-r2/artifact-seal.json` | 46193 | `58751d0f0d0d72a1ec3fb13469645c472f734e55983cbac470b76116d9d697cd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,preparationSealSha256,files,excludedFiles |
| `work/engine-r34-integration-r2/baseline-lock.json` | 38506 | `e0d00a412abdd2de672a5845ae10a160dd35f2b5a81fabf3f0e556c419e6d0fa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,projectRoot,engineRoot,baseRevision,basePinsPath,basePinsSha256 |
| `work/engine-r34-integration-r2/candidate-lock.json` | 1006 | `4caad0e6c5dd066e0a51cc9a5aef38052cfcb2a2c16bdbced82fa11204883c11` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=candidateRoot,candidateId,candidateSealPath,candidateSealSha256,sourceManifestSha256,candidateResultSha256 |
| `work/engine-r34-integration-r2/correction-index.json` | 6507 | `3ee8ece667ce796b48c49f373db4b611383075e5ab51084fb2103cd7e62b682d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,newToolingAndEvidenceContracts,resourceCorrection,reviewCorrection,sourceSnapshotCorrection |
| `work/engine-r34-integration-r2/pending-materials.json` | 583 | `1035bdbde3acd505a0678a27007264bd68e255ed6e2d2ff1d94f4ef58fa38bec` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,candidateSealSha256,reviewSealSha256,acceptedIndependentAssertions,acceptedIndependentObservations,formalInputContract |
| `work/engine-r34-integration-r2/pickup-fixture-plan.json` | 10503 | `5cb0a758eb48854aa2c26e2cf1e2443057ccf93881041830d5848596cead8192` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,sourceRoot,reviewSealSha256,files,filesCopiedNow |
| `work/engine-r34-integration-r2/pickup-suite-snapshots.json` | 5442 | `c85e27709c39ef30e1b0860a7be6f26ff09ac979b419bec23602404f2f482072` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,reviewRoot,reviewSealSha256,acceptedAssertions,acceptedObservations,executedThisPreparation |
| `work/engine-r34-integration-r2/preparation-seal.json` | 45733 | `795705e162ffcc422e66343fa4a6f68c6430ff4ac6ee6e54220155a60ccb9d88` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,files,excludedFiles,formalBuildAuthorized |
| `work/engine-r34-integration-r2/r2-copy-pins.json` | 58778 | `6a514a3c2025e7cac7b3dd02b85584f42d1c9b8324f08e9e2fe8a93fca58dfee` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=copies,r1ArtifactSealSha256,candidateSealSha256,r1FilesVerified,candidateFilesVerified,productOrAssetCopies |
| `work/engine-r34-integration-r2/reference-pins.json` | 48623 | `72371c8bbdfb9f15f534a20881ee1dd322ae4b15540934642e97e25106b80a0f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=references,immutablePins,unsealedCandidateRead,unsealedFormalReviewRead |
| `work/engine-r34-integration-r2/result.json` | 4224 | `a5874dbc2d6770bb11cae9e6045c996a32c5c09f3ada4275ea22f6b051ba95fd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,status,utc,preparationFiles,preparationSealSha256 |
| `work/engine-r34-integration-r2/review-copy-pins.json` | 9476 | `ff3d4968236095d41f0eaa17e5f2078d5d8bb71d6a70214f62ec9860c9a6ac1c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,reviewSealSha256,artifactsVerified,helperDllCopies,bitmapCopies |
| `work/engine-r34-integration-r2/review-root-policy.json` | 544 | `3d710d519bbc58a030e17a2098baffba20d2423a50f64eb33833d46d92f9cec4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,authorizedRoots,source,reviewRootRead,reviewSealPending,sealSha256 |
| `work/engine-r34-integration-r2/source-preservation-pins.json` | 1577 | `82de3223b14dd692dc0e9aecffd21d2ee869d4a3152ee1585f004941b46086fd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=r1Root,r1ArtifactSealSha256,baseline,r1AndInheritedCopies,originalReferences,candidate |
| `work/engine-r34-integration-r2/test-adaptation-inventory.json` | 5287 | `c38c7611d0a247d48231432e52e55020d95a3f50b095f0ee7573f4cd353a968d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateIndependent,suiteExecuted,functionalEdits,expectedEdits,items |
| `work/engine-r34-integration-r2/workflow-contract.json` | 4184 | `510bd7d93d4cb002c346b821814e16dd03ff3ae389d54d990a55189764edd538` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,stage,normalizedStageResult,canonical,release,currentAuthorization |
| `work/engine-slow-actions-r1/HANDOFF.md` | 4622 | `cd55e23b3b85c1f5b3bcc4fa7ef6777830aa242a4cf06bbfa8096335451bd4c7` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-actions-r1/SOURCE-CONTRACT.md` | 5615 | `d94bb4a4ce42bafa11fc8ddd3f147fc4ab2413a10a8848cf83e38e6b734c2b70` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-actions-r1/VALIDATION-NOTES.md` | 1191 | `f302960d170651daaca47109390cd5ca48a5af1871a8db4915a6c747f69135c5` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-actions-r1/base-manifest.json` | 42338 | `a1ae325497d798a22614956115064163993ee9c45f473796d887a3f759041014` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-slow-actions-r1/changed-files.json` | 1981 | `b1621b655bf76b0d4f088f718c331b1b7ddd5909c5a0dbd28db985e6d03597f2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(8);keys=path,baseSha256,candidateSha256,status |
| `work/engine-slow-actions-r1/delivery-manifest.json` | 48212 | `6f952b532750778320ef1b238051b4bc37bcbf92fe7d3e2391d207be11cea66f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,task,status,files,excludedDirectories |
| `work/engine-slow-actions-r1/input-pins.json` | 1624 | `9f5c5a084dcbb24b4f97db38fc86e55a5bdec7a624ac2211ba86d276d57a713b` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-slow-actions-r1/preservation.json` | 473 | `fd2af072d7eff75379b07f3e3160f245433f14bd872b9b09da7b012395a5d9e7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,sharedSourceAudioWebFiles,sharedAllMatchBaseline,sharedWrites,independentCandidateCopies,assetsJunction |
| `work/engine-slow-actions-r1/result.json` | 2662 | `088d79724a14be605aadcff8b67d27da37e03fdc04001b533474d17670717104` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,task,status,checksPassed,checks |
| `work/engine-slow-actions-r1/source-manifest.json` | 43022 | `bfdf2b3d50822f9cd31aa84d26d1cba670386f1e26c749166bf5f465e9c197b2` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-slow-actions-r1/source-pins.json` | 13993 | `fbc8300d9231d37751592298f27b24f04a0eeda64b024649043de0b943945788` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-slow-actions-review-r1/CONTRACT.md` | 9246 | `d44405636808c88049d59c31f77422a92b1895ee2664bc14bbc29cc5e02cb42d` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-actions-review-r1/HANDOFF.md` | 4111 | `b10d86c295a285be183d0115b3ba35c0ea314ca7276a76e3c64a97f0316e2443` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-actions-review-r1/action-metadata-vectors.json` | 87305 | `6afd114d3c53be54dfc2a0116cb71358c45a0df51ac64546e13d2de64f4afc9e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,profiles,rows,spells |
| `work/engine-slow-actions-review-r1/artifact-manifest.json` | 14460 | `6fa869f2c6f1ef2fc7b4444683ab4398170cb827230fad5e4cfd080e60da19b1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=artifacts,excludes |
| `work/engine-slow-actions-review-r1/native-action-oracle.json` | 38821 | `babe3b9849dec5aaa434adb9fa03f401cea06e76657cec5d90530afc0543719e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=imageBase,functionVA,rows,stubbedCallees,scope |
| `work/engine-slow-actions-review-r1/native-byte-audit.json` | 6034 | `e8406c960e993314eed5c331acfd3835ab467740d4a02fc7d7190605754f91fe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=imageBase,binarySha256,functions,chunks,scope |
| `work/engine-slow-actions-review-r1/preparation-summary.json` | 189 | `fb62f5d8d078b8cf7074a3c3989fb461e48a6223076c48859d428e2d99bdc46b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=baseline,sharedSources,metadataProfiles,metadataRows,nativeActionVectors,nativeFunctions |
| `work/engine-slow-actions-review-r1/r29-commands.json` | 1259 | `5cf446011711c61fd26b0c6c2109b46be6e22faccb2f50595e0b5f18eda93ee2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=commands,dllSha256 |
| `work/engine-slow-actions-review-r1/r29-probe.json` | 85520 | `1257772aaebf78fcd1bd706c0813d40c51393e9551fcc372d698dd93133d54cd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,assembly,assemblySha256,checks |
| `work/engine-slow-actions-review-r1/race-vectors.json` | 32401 | `e798bb5a5a0629ab4721557711c643ad1bd71e4d55c258a07dc124e3c86818b6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=executedEvidence,planned,additionalCandidateGates |
| `work/engine-slow-actions-review-r1/result.json` | 3191 | `98bb5297d7e361e1d1dc24f102fbbc4e6eb452009b0a7dd592cb1dfa5946765b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,status,reviewComplete,candidateReviewed,candidateAccepted |
| `work/engine-slow-actions-review-r1/server-action-vectors.json` | 237579 | `d587962df201d45870d6a563949ca8e59fb0d1a808666fb1aa6f5767adaa40b4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,rows,reuseDisambiguation |
| `work/engine-slow-actions-review-r1/server-reservation-excerpt.json` | 3061 | `81c48df9b2de932c30e60c2f3f26fd5448be551ccfb5d47d1e4a5494cf795a1f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,sourceSha256,classByteEquality,encoding,excerpt,claim |
| `work/engine-slow-actions-review-r1/source-pins.json` | 53200 | `ac67a5a74c8096ffa933df3c01bf952025538d8f8fcb193908bc25c41737ca76` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sources,classPins,goalObserved,goalModified |
| `work/engine-slow-haste-item-r1/HANDOFF.md` | 3929 | `680ceb75d4200b7f1fb4447c2e5f12ba706d4f93d8e0d65f854d2a38227d111b` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-haste-item-r1/SOURCE-CONTRACT.md` | 5055 | `59d84d8f2850873745a8ee6cec60ecbc7c19dc74f14f7bbf08f1d6dc79546601` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-haste-item-r1/VALIDATION-NOTES.md` | 4161 | `ccdf89fa16e69be79d790485eae2e3a8789b7007ac753463b6b5c5b2d2206c79` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-haste-item-r1/changed-files.json` | 454 | `86d37a1ef47e2641b9cacc8380a4b040bc1272a3e202cd33fc4b4c0735e6e74b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=path,baseSha256,candidateSha256 |
| `work/engine-slow-haste-item-r1/delivery-manifest.json` | 41097 | `0976aa3ffe829a1b4f77c3b94241f113ed77ca0dab245b6c6e1dca1ac18f3369` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,status,files,excludedDirectories |
| `work/engine-slow-haste-item-r1/final-base-binding-pins.json` | 2154 | `9cbe2204f226c56b1801f6c3a0b3334d24cfe3409dfe2f20c13bcb362d1d9150` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-slow-haste-item-r1/input-pins.json` | 2645 | `96a66fba112ed9ff6679f563d8ad82024bb8ed4dc6bc255e297c9fd39e7ca77e` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-slow-haste-item-r1/preservation.json` | 797 | `47400869115798bd37838d05af2f4a6b23657c36d1805b518aa56686cbc1a192` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,acceptedStageSourceFiles,allStagePinsStillMatch,independentCandidateCopies,unchangedCandidateFiles,changedFiles |
| `work/engine-slow-haste-item-r1/result.json` | 2520 | `6793b846a7a2cb0ce282014758a4defa13c544c43ea81afb52c8e3537a7c37e0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,revision,task,status,checksPassed |
| `work/engine-slow-haste-item-r1/source-before.json` | 46275 | `86ce157fed1592d8bc6f08eec48c5c2bbc68c7e9140631a26d98feed7c0cf6be` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-slow-haste-item-r1/source-manifest.json` | 43578 | `2713dd94bea243bfd71576878bbbb3649938703cdd04fb81ab519e9dcb851518` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-slow-pickup-review-r1/HANDOFF.md` | 2407 | `bcc690a23104a9dc37a46773e1c4cc9f6ced3bd0a1d66fb83423b7f439eebfae` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-pickup-review-r1/artifact-manifest.json` | 8028 | `29fb10cb6919a9ea07c13185af3ffc0eab2b8be4944c18093cbf84cb00326095` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,candidateReviewed,exclusions,artifacts |
| `work/engine-slow-pickup-review-r1/baseline-binding.json` | 29934 | `00e9d4664024119595faeead50ade5827258763e47dc3d7068ea9b78abf8c813` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,baseline,sourcePins,releaseSeal,productDllSha256,audioDllSha256 |
| `work/engine-slow-pickup-review-r1/baseline-findings.md` | 3772 | `db6ebcf451f539e52e07af44b6bc1b99ad1c2c1cdec49378fa80d96a1f101620` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-pickup-review-r1/baseline-probe.json` | 66867 | `017be829d85b140cd5172ecb34147169a053e48588c4993cecf3d5baee6ed3fa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=candidateReviewed,productCandidate,baseline,assembly,assemblySha256,clock |
| `work/engine-slow-pickup-review-r1/class-pins.json` | 3580 | `0d8d5d837fc08423de4a5766198e22744f0b3b991cec2c8868bf58157ee8bbfd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=jarSha256,classes,commands |
| `work/engine-slow-pickup-review-r1/commands.json` | 1095 | `c48fb7ff903e91967b7ea9564e9c07c3210540476c4a538c4b52afb5db12e9d8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=buildAndProbeEntry,buildExit,probeExit,buildWarnings,buildErrors,staticClasses |
| `work/engine-slow-pickup-review-r1/profile-audit.json` | 26428 | `9d9f9fed4d77d7949826325077e16887b42afd297e38e29bcf34d4936584cdc7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,profiles,manifestUnavailable,completePickupProfiles |
| `work/engine-slow-pickup-review-r1/result.json` | 4942 | `91e51704cc0dab2727339c8e022709b841c045a41ae07f324a91337dbbabce7a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,status,candidateReviewed,productCandidate,slowPickupQualified,baseline |
| `work/engine-slow-pickup-review-r1/review-contract.md` | 11834 | `8e54eceb3010ccafbd0a6591611340d1bbcb13c3d64744557360e3cc44bb1763` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-pickup-review-r1/sealed-source-conclusion.md` | 2435 | `3c98fc3857bb45e89b5d069c33cfb9f45fa09e8549f1056990205dc7d5b6513f` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-pickup-review-r1/sealed-source-crosscheck.json` | 74956 | `1c9f6a60fe1772a87a89b25a0a47cd07034b664b0cff6e7b6352a652929813d3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,sourceSealSha256,sealedArtifactsVerified,peMappedChunksVerified,chunks,vtableWordsFromOriginal |
| `work/engine-slow-pickup-review-r1/source-pins.json` | 117949 | `15466f3780081b4123aef11a737d93c6afcfbe355a9cb30fd223b4535fe83897` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sources,releaseSourceCount,goalObserved,goalModified,baseline |
| `work/engine-slow-pickup-source-r1/CONTRACT.md` | 10910 | `ad976cc46a98a560aa36fc085c129522bb0806c13c747add897114e0de45b613` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-pickup-source-r1/HANDOFF.md` | 4146 | `f20836158e18e5e4d5e9f4137424a4c420c34736f2b4dcd799d98da892324573` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-pickup-source-r1/artifact-manifest.json` | 46172 | `8b1110e77614fbe8f9cb9b68718348c71f75d04110451cbca3c90699114125e1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,status,files,excluded,productChanged |
| `work/engine-slow-pickup-source-r1/collection.json` | 11004 | `9d29e40fc6d5579ed50f616a6cb629855d5d50f66b927b8ff73d3cf22504789a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=classes,nativeChunks,productChanged,candidateBuilt,nativeExecuted,jarHandlersExecuted |
| `work/engine-slow-pickup-source-r1/method-index.json` | 95186 | `33a1357ffdf151bc5297ba525aadcfe4c2ccdec33fe3ae942393bb9c6c84898b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,methods |
| `work/engine-slow-pickup-source-r1/native-byte-audit.json` | 88552 | `8f7621cbd98c70541140231ee6f6d9df87c641258f12190993cd7a5418afc878` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,imageBase,binarySha256,chunks,nativeExecuted,excludedExploration |
| `work/engine-slow-pickup-source-r1/proof-index.json` | 4520 | `88ff6c4383007eb724690a971ba4cf47b03f9f988fd4f66d9dedf20292a38646` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,proofs,methodIndex,nativeByteAudit,sourcePins |
| `work/engine-slow-pickup-source-r1/result.json` | 741 | `33fd784f399450ec3cab0fc6ed6b2bc86ed614a1f6d07b33853255e0c90719e8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,status,base,qualified,productChanged |
| `work/engine-slow-pickup-source-r1/source-contract.json` | 3271 | `8c29fd1b806789caec833e7b23cf7bfea719d912751e4429817d89b2b68fea65` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,base,productCandidate,scope,qualified |
| `work/engine-slow-pickup-source-r1/source-pins.json` | 18290 | `8194317d79f171f15ea1e78e22823058b8037addf41711694fc049e03667bf90` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-slow-pickup-source-r1/validation.json` | 865 | `e30387935bc8978d08793022f3439bb6c5ac3d125d941a375a5e76c0afdd5221` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,sourcePinsVerified,releaseSourcePinsVerified,classEntriesVerified,nativeChunksVerified,profilesVerified |
| `work/engine-slow-r1/HANDOFF.md` | 7897 | `ea77647c99c103cffdd5526634b0bb3aa75d81e77971ce68d7be21e4e475cf7e` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-r1/base-manifest.json` | 35787 | `1904c60a54b6d5f8bd94ba29c8a6cb293add5701970d1d63b3582385b49b56fc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(146);keys=path,relative,sha256,size |
| `work/engine-slow-r1/changed-files.json` | 2038 | `cfdf32a551e016b2aeaf78af26bf38649f7549edacb02da896ed73f899ecffa1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(9);keys=path,baseSha256,candidateSha256 |
| `work/engine-slow-r1/consumer-audit.json` | 2545 | `f4284a491ad27a817a6f1b1087bb83332de38afb700134b44e53526645c0f985` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scope,consumers |
| `work/engine-slow-r1/delivery-manifest.json` | 51581 | `bf22ad09f0d2b2940dfb295680d363287b56a253249f128de393520b562dac61` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,status,files,excludedDirectories,excludedFiles |
| `work/engine-slow-r1/native-byte-audit.json` | 46388 | `1dfcf41e5cfcf1491127f81b4c77518169ed792d98c90545b41dffeb3cf6e219` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,imageBase,sourceSha256,chunks |
| `work/engine-slow-r1/native-speed-oracle.json` | 5622 | `f29ad156d17cdd0d81757ed7d43ec295ae869fc2042b2364b5e956d01af02726` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,imageBase,va,rows,scope |
| `work/engine-slow-r1/result.json` | 1886 | `18342b1de15af414cfc50bed9545b889a903a961cb4882d8cdda2af26e3e6e1f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,status,sealed,sharedEngineMerged,nativeLiveAcceptance,wholeProjectComplete |
| `work/engine-slow-r1/scope.json` | 364 | `29952d340b3a0c466adbd4f86fefe732c7a4a6e8d0dc6bd6d5e463f0a90136f2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,writeRoot,baseline,normalProductRun,network,database |
| `work/engine-slow-r1/source-pins.json` | 800 | `fe9b9acb0c26744e4460d2d60c2d1177de18e1f7667020be351b282d84e69948` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-slow-r2/HANDOFF.md` | 4830 | `091787d52472789fc66e663ef7cfc466de64a756aa35192e5bb44362afca4a54` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-r2/base-manifest.json` | 35787 | `1904c60a54b6d5f8bd94ba29c8a6cb293add5701970d1d63b3582385b49b56fc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(146);keys=path,relative,sha256,size |
| `work/engine-slow-r2/changed-files.json` | 2038 | `d145a34c9a0353e7304ea85dfb08507f7bf27102f2df45513c2b76de32c2d93b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(9);keys=path,baseSha256,candidateSha256 |
| `work/engine-slow-r2/delivery-manifest.json` | 52859 | `7bed792d86ec1838ce97e0efa935d061420fa56cf6c0435dbaba0f0c919859b9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,task,status,files,excludedDirectories |
| `work/engine-slow-r2/input-pins.json` | 43442 | `b9f8eb172198a36307162a209a1500f73d90acaf620884fecead95791b1ba737` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(170);keys=path,copy,sha256 |
| `work/engine-slow-r2/jar-oracle.json` | 5839 | `1eb23d82aa36b47bd09a8944e5191f6d434a2128112fb3464798292edac8b78a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=classPins,rows,commands,scope |
| `work/engine-slow-r2/native-oracle.json` | 4890 | `0b11690c96a93e3c1cc8cd935ad8d9c35b907d48fcb6c1d265d6710f398fbe4a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=imageBase,functionVA,rows,auditedChunks,stubbedCallees,scope |
| `work/engine-slow-r2/native-speed-oracle.json` | 5622 | `f29ad156d17cdd0d81757ed7d43ec295ae869fc2042b2364b5e956d01af02726` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,imageBase,va,rows,scope |
| `work/engine-slow-r2/preservation.json` | 671 | `9e4d0d90c65c995a13142ee88a3a594d6feb61da3543a8fbe537802e7ca6815d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,r1ManifestSha256,r1SealedFilesVerified,inputPinsRechecked,sharedSourceAudioWebFiles,sharedSourceAudioWebAllMatchBaseli |
| `work/engine-slow-r2/r1-to-r2-changed-files.json` | 221 | `d417aee3d655c527271e26b5c091f6b8adecc97df6b7ffd54043e3546fa0869c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(1);keys=path,r1Sha256,r2Sha256 |
| `work/engine-slow-r2/result.json` | 2811 | `fe8ec487ed8712e43e941f61453d0877d8730ff746ed415031f4cb625d4610b5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,candidateId,task,status,checksPassed,checks |
| `work/engine-slow-r2/reused-evidence.json` | 3814 | `f9ee441cb308f26fd82b50dcb9951696c27ceda61a00491e3a00c08d60641fdd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=candidateId,originalSourcePins,references,nativeOrJarExecuted,note |
| `work/engine-slow-r2/source-manifest.json` | 40672 | `8938b63e49c7aff78f1a8baf7b1e7b3765de2bc4da7e16de8408b11db86bda5b` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-slow-r2/source-pins.json` | 800 | `fe9b9acb0c26744e4460d2d60c2d1177de18e1f7667020be351b282d84e69948` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/engine-slow-review-r1/CONTRACT.md` | 6055 | `431620765e32cf6d4cd456f3b5f491d318e4ea3478a6ebceb9725655fb5f3a15` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-review-r1/HANDOFF.md` | 4458 | `c0cb3b1028e8b49e58a839a63ff771072bc26af2896c5b814eb3d56bd0edfc17` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-review-r1/artifact-manifest.json` | 6382 | `64328a3734e72c9bf8abf2fc0e2f6a1e8c61fde244e11b871707a52b05203ae2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,status,artifacts |
| `work/engine-slow-review-r1/baseline-commands.json` | 2245 | `da64ae649918185d2a0908b2973de5d14ee6b51ec2ae7f25a0613ffb61304213` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=commands,assetCopies,dllSha256 |
| `work/engine-slow-review-r1/baseline-probe.json` | 9770 | `7e522a677a9753d293fb07c6af4d346ff396d825cfc4b9685b3b324b6755525d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,assembly,checks |
| `work/engine-slow-review-r1/initial-source-pins.json` | 2008 | `307a94a36615a9604ff557a96829603b799c3e315fcae8f9702528ea9eaa6997` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=goalObserved,goalCreated,reason,sources |
| `work/engine-slow-review-r1/jar-oracle.json` | 5839 | `1eb23d82aa36b47bd09a8944e5191f6d434a2128112fb3464798292edac8b78a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=classPins,rows,commands,scope |
| `work/engine-slow-review-r1/native-oracle.json` | 4890 | `0b11690c96a93e3c1cc8cd935ad8d9c35b907d48fcb6c1d265d6710f398fbe4a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=imageBase,functionVA,rows,auditedChunks,stubbedCallees,scope |
| `work/engine-slow-review-r1/result.json` | 1258 | `330e028416e1569a5c55ace0ea4ed07ff1d0ae8804674241e0c93b41e9bd60bb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,status,independentPreparationComplete,candidateReviewed,candidateAccepted,candidateRejected |
| `work/engine-slow-review-r1/source-pins.json` | 7605 | `db77b7c13c692db7fb0992d835cf27047e369325ecffb9c776473cade92150d2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sources |
| `work/engine-slow-utility-review-r1/CONTRACT.md` | 11472 | `5c296ceaaf625410e1dbfa833812b9eb4312cadea9475b193868d4be0aed23ba` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-utility-review-r1/HANDOFF.md` | 2641 | `873559defbaeca0a43cc1ddacbd233fcf7c928419819f73793ed9660642a73a3` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-utility-review-r1/artifact-manifest.json` | 11606 | `0844c5f809903930bfe10ebfd658b3f22bbed72693b1bb584a555aecabcb301a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,candidateReviewed,exclusions,artifacts |
| `work/engine-slow-utility-review-r1/class-pins.json` | 10520 | `c13d62158782199d6ac829fec4c513772f499853e0a8394cd2928d35b140f7b2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=jarSha256,classes,commands,newServerOracleExecutions |
| `work/engine-slow-utility-review-r1/commands.json` | 1424 | `56b5d220c1bfc1630fd1b40fdfa701ad20df350d159de10cf5b1d7090bfefebf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=build,probe,buildExit,probeExit,environment,assetJunction |
| `work/engine-slow-utility-review-r1/native-byte-audit.json` | 3785 | `6f12f45ad1f6f1a8f696cfcfead689f0b13ea1628c57510b43feaff24899fcf2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=imageBase,binSha256,chunks,scope |
| `work/engine-slow-utility-review-r1/native-selected.json` | 192615 | `8a70d0180e5d7a967972b931d78f28e392106d98409f85589d338be52362ea59` | U — U08：opaque Base64 候選；來源 unknown |
| `work/engine-slow-utility-review-r1/r30-probe.json` | 25088 | `ceaf3f43d0f384653444b2ba0934ce33c175056de9a9f6fdc4ed3b8293345a19` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=candidateReviewed,scope,assembly,sha256,clock,checks |
| `work/engine-slow-utility-review-r1/result.json` | 8502 | `b58faff142bc9d790db46b813506391e13f8b7977eabb05369bc945a49eb3bdd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,status,candidateReviewed,baseline,dllSha256,observations |
| `work/engine-slow-utility-review-r1/source-pins.json` | 43465 | `518eb47cca063bdb5696388127ae2d028932590ebe84a8cea1950f859faebb87` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sources,baseline,goalObserved,goalModified |
| `work/engine-slow-utility-source-r1/HANDOFF.md` | 6342 | `5938e3ec83cc2b54c6e00356fffadefbdbc1e0da4042491b5a83707c7cb2e212` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/engine-slow-utility-source-r1/artifact-manifest.json` | 15514 | `e21d05d5638742afdb069d988626e103a024f8c0555102cda6264a21e9eb4639` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,status,files,excludedDirectories,excludedFiles |
| `work/engine-slow-utility-source-r1/collection.json` | 77158 | `adc687e0907d7ba5fa3f73668c2440a49c4b8bf444283af0ef58563e9c9ad000` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,sourcePins,classes,nativeChunks,normalOrNativeExecution,jarExecution |
| `work/engine-slow-utility-source-r1/proof-index.json` | 13315 | `30317385cfa28d49c55fe67a9ae60968fe814bfa6f5744d7e21c350b11360473` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=java,native,nativeAddressBase,note |
| `work/engine-slow-utility-source-r1/result.json` | 1033 | `502e83abce8698baeaae8fd2c7a6141dc5588426e8304b35e0d302b49095e58a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,status,sourceQualificationComplete,qualifiedCount,unqualifiedCount |
| `work/engine-slow-utility-source-r1/source-contract.json` | 21240 | `85385401cdf40ef31ad966577da8527ed30ad1dae37b19eea6c8267fc138e0fc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,task,base,productCandidate,qualificationDefinition,items |
| `work/engine-slow-utility-source-r1/source-pins.json` | 83927 | `98769adefc19e0dd9cfd543c18da40406ac6b425d52168cf4c4a8cfb3283fe99` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,files,jarEntries,nativeChunks |
| `work/issue-47/REPRODUCE.md` | 11360 | `0d5bd19ff4116bb5b80db786dfeed8752cae252261ddb333c9c69bd327a44913` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/issue-47/evidence.json` | 172881 | `64691858b688ec1f0e5b5589e5d0301fe66bf5699fc647f43dfd48568bb1c57b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,sourceVersionPaired,status,scopeMeleeProfiles,entries,actions |
| `work/issue-49/contract.json` | 197811 | `dcdd5a3e6883e4405a6b1b297fa5f6efab25596321605b87cf14a700cad3bfdd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=issue,qualified,status,scope,historicalJarSha256,jarRehashedThisRun |
| `work/issue-49/fixtures.json` | 36818 | `2fd34c51c43b11becb5738508f1722918f9254ccb15f568a348f6b362061d9a4` | U — U14：可重構素材／封包欄位 $.packetFixtures[].payloadHex；真實或合成來源 unknown |
| `work/issue-50/evidence.json` | 20465 | `050c4902f993001875636e8f04f8657fd730b8894655020f2c3ce068d0041692` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,nativeListening,policy,sourceCommit,sourceEvidenceSha256 |
| `work/issue-51/combat-sfx-result.json` | 1225 | `86fd193b3a6c68f5f0b741a192f0691363e71f2299aac22da6c670f666d545f6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,checks,cases,productSha256,scope |
| `work/issue-51/fixtures-49.json` | 36818 | `2fd34c51c43b11becb5738508f1722918f9254ccb15f568a348f6b362061d9a4` | U — U14：可重構素材／封包欄位 $.packetFixtures[].payloadHex；真實或合成來源 unknown |
| `work/issue-51/result.json` | 1686 | `398918bd1958d8e89a8ed92f0853e0fe8a453e7ad686c6fb70bd51281120af69` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,assertions,cases,fixtureSha256,productSha256,scope |
| `work/issue-51/validation.json` | 4710 | `59547b8b41d16372ad0b9c62f48a892ae7f3268935ecd44f9a8ea51848068f01` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,build,groundAssertions,web,legacy,legacySourceSha256 |
| `work/issue-51/web-result.json` | 169 | `c85d91c77f72fea0efa2de260babf87e05becd6845a786257d204ee98958880c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,checks,scope |
| `work/issue-77/baseline-result.json` | 1188 | `e90cc94524530bbbc5514cea38a28afb114f0464f89cf8cba984bca9601dd7dd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,baselineDllSha256,source,testSourceSha256,exitCode,failure |
| `work/issue-77/combat-result.json` | 1225 | `1ab56a6bda509ddb414044e5c45bc6e92fa403f9d7a0679c26468e7f156d933e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,checks,cases,productSha256,scope |
| `work/issue-77/ground-result.json` | 1906 | `4b4c7099f5e819a3a4607314fb061384885d6ed65662bc8b2ee5025d11a6e42d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,assertions,cases,fixtureSha256,productSha256,scope |
| `work/issue-77/host-adaptation.json` | 413 | `5063619bb019112980a69c482afc87802325ffa82c559d6b9e89d37f5e5c94c3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,sha256,adaptedSha256,changes,assertionsChanged |
| `work/issue-77/static-evidence.json` | 17748 | `abb3dfcfc89477ee8e8f9c03c100dd2051a9ea3dace84271fad737bc78f0990f` | U — U14：可重構素材／封包欄位 $.serverReplyFixture.payloadHex；真實或合成來源 unknown |
| `work/issue-77/text-source.json` | 1128 | `3ccde038024eeb58cbdbc4ce991f40918e6738bdd2f772711b39823cc3e46ae2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=indexSha256,decodedIndexSha256,historicalMatch,historicalSha256,record,indexRecordFileOffset |
| `work/issue-77/validation.json` | 2585 | `37314b3ec13355fe229438b9c7c0070ca17d0881227724847219221669f56ea1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,baseCommit,scope,build,devLockSha256,sourceFiles |
| `work/issue-77/web-result.json` | 169 | `c5b526db7d0b96e0c19ca31e782d7ac400f25b02d098f777c2b477fe61db1257` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,checks,scope |
| `work/timed-item-lifecycle-r1/CAUSAL-TABLE.md` | 14787 | `0af23022044af9e6f1b6d7bcaaf69a5d34e3eb173d75040b0634e85d5f95a956` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/timed-item-lifecycle-r1/HANDOFF.md` | 8829 | `a54ec5746b1b0fadd4e6c8fdffe7c9baeea320692cbd7ec6381251e19466eb38` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/timed-item-lifecycle-r1/artifact-manifest.json` | 6338 | `7ab57d40d99666b0d870a739f05d4fc05ad88786aeb328192eeec31e4235fd9b` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/timed-item-lifecycle-r1/causal-table.json` | 74857 | `670450d70216c75a645315b4418495703f316d6a6aa6fc7ac6a2b8d07e0a3e61` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(12);keys=id,stage,verified,inferenceOrPossibleOrphanMechanism,missingEvidenceOrLimit,methodEvidence |
| `work/timed-item-lifecycle-r1/class-evidence.json` | 21945 | `e82520d90942027a1918d0e141cc91c3da3234ba74ec6dc176d545facc1e4aff` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(20);keys=class,jarEntry,classSha256,sourcePath,sourceSha256,javap |
| `work/timed-item-lifecycle-r1/historical-evidence.json` | 20957 | `294b7c428cf1ead9149f55e7627c176efa14a6d486c00728d8b4d4d162a11bfd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=timelineReceipt,timeline,rentalReview,innConfiguration,configurationFreshness,keyFacts |
| `work/timed-item-lifecycle-r1/initial-source-pins.json` | 5137 | `0012774b8fbc90d918b8cbe4fac9b0837c26dd64e7ddd918de9686453338ac23` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/timed-item-lifecycle-r1/method-evidence.json` | 603200 | `37047043a6d636f8bdb96966ae6898f8c20c11f7529421210e072ac41cf7bbae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(32);keys=id,class,classSha256,method,javapFile,javapStartLine |
| `work/timed-item-lifecycle-r1/result.json` | 6686 | `b4f83a474f552229cb19c00424298039f0b61a0ac140695e4237cee4c7c59691` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,task,baseline,jarSha256,exclusiveWriteDirectory,workerGoalActualStatus |
| `work/timed-item-lifecycle-r1/source-pins.json` | 12828 | `26b25c5a0161c3f17196cc56d8395c8749e1c06ca2b33f2987e6faacc6876b28` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `work/timed-item-lifecycle-r1/static-verification.json` | 3751 | `11b31dcfb57374f23f7160f9ef595bc2ce8bdf74d98d08cf26ad43bfe7409b50` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,methodChecks,methodCount,jarClasses,sourcePins,beforeAfterMain10326Absent |
| `work/timed-item-lifecycle-r1/supervisor-select-evidence.json` | 3889 | `446a4ac48adc71736da261d16ed2ca7e7d16bc70e4da4b1a821bd534a4265e57` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=executedBy,workerExecutedSql,source,sqlSha256,utc,results |
| `work/timed-item-lifecycle-r1/verification.json` | 343 | `02f9bae20e6b85c7fd743b9688e396607271d5e2e3abb7de241161357580a500` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,checks,sourcePins,classEntries,methods,causalRows |
| `work/timed-item-repair-review-r1/HANDOFF.md` | 5274 | `ad0d0977ce7ee9c323bf0943bf42072c822d8bdff65eab108f467543dbc46e35` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/timed-item-repair-review-r1/ISOLATED-TEST-DESIGN.md` | 7325 | `42852eda24cccf583252faa55b3900db084980a0ff675d61c0f92b65f26f7d95` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/timed-item-repair-review-r1/OPTIONS.md` | 7235 | `8508089c92e869fa3797de1b6e1fefcc2143ceffb4187c2273c6e838eae40ace` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `work/timed-item-repair-review-r1/artifact-manifest.json` | 2374 | `3a72a32d8d1842a4ddeb9c688f2ac503747c872b22667903135564bdef9d3fba` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,artifacts |
| `work/timed-item-repair-review-r1/evidence-review.json` | 10806 | `cd1340f6f657de437593cea1a99ec600d43101d1c980847ef3b0cba6e644068d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=baseline,jarClass,verifiedSupervisorPins,timedObservation,ledgerCounts,ledgerDelta |
| `work/timed-item-repair-review-r1/result.json` | 2514 | `6ce08d2deabc642e249eb268b4b12a0e5cdb3c15fe0e37fc00dfd403ea8bf8c9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,status,baseline,goal,writeRoot,sourcePinCount |
| `work/timed-item-repair-review-r1/review-gate-results.json` | 3218 | `427352bfbc224d5ce3b5087089ee5c7b49e54a0d7e0c9e972c2e308bd1bdd7c2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=type,retainedJavaExecuted,dbExecuted,repairApplied,count,cases |
| `work/timed-item-repair-review-r1/source-pins.json` | 4743 | `7a6e3018b28758a2236b2992b4269af1c8e407e9652bd24b2e2573a403706fde` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sources |
| `主管/P01-replay/baseline.json` | 11711 | `f99308ad7771ac98457938f6c46ab734943a3fa06c5d366b6d5038622fc8d708` | U — U08：opaque Base64 候選；來源 unknown |
| `主管/P01-replay/manifest.json` | 858 | `0af1f9232afcec1e0d1edc12b49df8e0815adbc4ebc416d59f7156e15ec88e92` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source_absolute_path,source_size,source_sha256,copy_relative_path,copy_sha256,expected_sha256 |
| `主管/P03-原始指令重跑/evidence.json` | 138204 | `ce41b760a8c8b128e8543cfb8ed6d425b9021fae739ecdfa6b0e9f5ba11e6d0d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=bin_sha256,imagebase,machine,P02_checks,chunks,imports |
| `主管/P03-獨立測試/index_validation.json` | 12691 | `f57d763006e1663b573e861e96857022497d328beed8a2f45ee5b55572be12c6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=count,pak_size,header_hex,ascii_fold_order_violations,ascii_fold_duplicates,non_ascii_names |
| `主管/P03-獨立測試/samples.json` | 5725 | `8a7d958081703548ae6d07e378b39c6d6749bc2abb19446b3c00ebf742c5b668` | U — U14：可重構素材／封包欄位 $[].record_hex；真實或合成來源 unknown |
| `主管/P03-獨立測試/tests.json` | 9723 | `7fc0a57a5bed99f4cdf4c8614949d881129d3d4c4115d1f9a70af5249a56011d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=passed,checks,cli |
| `主管/P04-乾淨重建/animation.json` | 87909 | `5924258d20c26696951e66a6f4631a3b0e2811603a8991bce7dd75d026620541` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P04-乾淨重建/canvas-checks.json` | 29390 | `3dc85af6f02c6b80a11e7f82c1e0a01834b34734b6ae2ab92cf881d1169b21a2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=canvas,checks |
| `主管/P04-參考解碼重跑/commands.json` | 7753 | `b8f3e269b644124c20949ccf624c13501974014cb8e52e158881828e657d3458` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(16);keys=argv,exit_code,stdout,stderr |
| `主管/P04-參考解碼重跑/comparison.json` | 545053 | `ae5d2ca28503c83984c4436ae415a5d0dcdcffce297aecde4470f5f97982bff0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=color_profile,dimension_difference,checks |
| `主管/P04-單檔CLI/manifest.json` | 3020 | `45a070e9dbf2d1438c39d112fb90c351ef6e276bf8d0d10c942bbdac253391c5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=input_sha256,color_profile,frames |
| `主管/P04-建置重跑/animation.json` | 87909 | `5924258d20c26696951e66a6f4631a3b0e2811603a8991bce7dd75d026620541` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P04-建置重跑/canvas-checks.json` | 29390 | `3dc85af6f02c6b80a11e7f82c1e0a01834b34734b6ae2ab92cf881d1169b21a2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=canvas,checks |
| `主管/P04-獨立驗證/color_profiles.json` | 4199 | `bc9c6b8c656ae78e6e07ee4a175c5076e4109af56299608db54d6ed63e01d516` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(32);keys=g5,g6,rgb555_replicated,rgb555_left_shift,rgb565_replicated |
| `主管/P04-獨立驗證/tests.json` | 7034 | `b2cbf36e69801b148a905444f0e8cbdd5dc991a638131debaf5f93d33413e723` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=checks,total,synthetic_policy |
| `主管/P05-乾淨重建/draw-commands.json` | 1673483 | `8a37fa29c36b3a18cc77d9a398604f6b449e8185e431983ee6262f825730b09c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(14613);keys=layer,source_index,x,y,index,tile |
| `主管/P05-乾淨重建/manifest.json` | 42478 | `0204d7ba1019405bc7b5eef93b775dd79d0c80142f2fcbbf2c627afdc5c75bee` | U — U14：可重構素材／封包欄位 $.source.records[].record_hex；真實或合成來源 unknown |
| `主管/P05-乾淨重建/map.json` | 2580835 | `bc15eb9bc98499b56331c79534851b313573333fd499a0e192a07de9188097ae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=profile,offsets,ground,cover,objects,attributes |
| `主管/P05-乾淨重建/tiles.json` | 2429557 | `268ff3a7bb95da17b6f24f26c2c4d508ba4fb2026715d506ea7272458decfefe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(7602);keys=tile,index,type,payload_offset,length,sha256 |
| `主管/P05-原檔抽取重跑/sources.json` | 38659 | `21dafd708ae286e0395a43cf017a1daf7d285204fbadee277ebf6684a90b04cc` | U — U14：可重構素材／封包欄位 $.records[].record_hex；真實或合成來源 unknown |
| `主管/P05-參考比較重跑/command.json` | 897 | `52277d8aa8f9867130d62bae67c6daa660ce067f5cdb5e14df0133d158ce425c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=argv,commandline,start,harness_sha256,exit_code,end |
| `主管/P05-參考比較重跑/comparison.json` | 1147 | `a3dcb397ac73fb1a8ea9c8120df36027d419140900bb2684bf29df885542cf29` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,comparisons,classification,tile_pixels_checked,tile_pixels_checked_twice_with_sentinel,raw_black_pixels_confirmed |
| `主管/P05-獨立驗證/validation.json` | 9489 | `13ac699295deb707a427ea9d00d66cb1bb76a775bf9d44d47713ba4af530830b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,status,checks,count,success_inputs,coordinate_samples |
| `主管/P05-預設入口重建/draw-commands.json` | 1673483 | `8a37fa29c36b3a18cc77d9a398604f6b449e8185e431983ee6262f825730b09c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(14613);keys=layer,source_index,x,y,index,tile |
| `主管/P05-預設入口重建/manifest.json` | 42478 | `0204d7ba1019405bc7b5eef93b775dd79d0c80142f2fcbbf2c627afdc5c75bee` | U — U14：可重構素材／封包欄位 $.source.records[].record_hex；真實或合成來源 unknown |
| `主管/P05-預設入口重建/map.json` | 2580835 | `bc15eb9bc98499b56331c79534851b313573333fd499a0e192a07de9188097ae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=profile,offsets,ground,cover,objects,attributes |
| `主管/P05-預設入口重建/tiles.json` | 2429557 | `268ff3a7bb95da17b6f24f26c2c4d508ba4fb2026715d506ea7272458decfefe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(7602);keys=tile,index,type,payload_offset,length,sha256 |
| `主管/P06-完整重建驗證/global.json` | 55 | `fe15c79ed27b7261f804746cdb9b8343003b2ecbdbc1cde8cd34e0eda4149921` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P06-完整重建驗證/source-snapshot-before-focus.json` | 844 | `3930cd46259a68373d9dd34d4db814163cb568bde01f9398d0e2e8a1b68b2f3e` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P06-完整重建驗證/source-snapshot.json` | 844 | `c8d505a955d53b3958a4b4eeee000bce6ec21228699d43aa80a19588331fcf66` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P06-獨立建置初核/global.json` | 55 | `fe15c79ed27b7261f804746cdb9b8343003b2ecbdbc1cde8cd34e0eda4149921` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P06-獨立建置初核/source-snapshot.json` | 856 | `11f053fe3f109ba5d5665bd03e0ae2dcd304486496905d3dbda6c2b9630fbfda` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P07-完整重建驗證/global.json` | 55 | `fe15c79ed27b7261f804746cdb9b8343003b2ecbdbc1cde8cd34e0eda4149921` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P07-完整重建驗證/snapshot.json` | 22639 | `c537c0cc37805a10eacfd76afdcb4b2278b6f2ac967f2baffeaed8baa39f9275` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P07-獨立參考初核/global.json` | 55 | `fe15c79ed27b7261f804746cdb9b8343003b2ecbdbc1cde8cd34e0eda4149921` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P07-獨立參考初核/reference-audit.json` | 7277 | `e04be9904cf5d53655b019208a143b2c5076af6abe8b3569e5f6fb98ac130cfa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,snapshots,original_chunks,fixture_json_equal,codec_checks,limitation |
| `主管/P08-完整重建驗證/final-refresh.json` | 434 | `2c33a77692febb76e2c88a5d42898ea3695db81be0d1cb6b94442ec444a8534b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=path,before,after |
| `主管/P08-完整重建驗證/global.json` | 55 | `fe15c79ed27b7261f804746cdb9b8343003b2ecbdbc1cde8cd34e0eda4149921` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P08-完整重建驗證/keyboard-refresh.json` | 834 | `a57a83d949a699b0b28d5138c5f8dd091abf95ba5a6a6fc56957f45c95d440e3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(4);keys=before,after,path |
| `主管/P08-完整重建驗證/snapshot-initial.json` | 23854 | `cca4ab5eb0b696c9cef999b4c4d476ce2e24f5f1cf6a4a456be64471e73ac7d4` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P08-完整重建驗證/snapshot.json` | 23854 | `09a8f23b4d3e4c76733fac8debd9be0877290a58afc01d962bbdeed762a03763` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P09 最終解包驗證/HANDOFF.md` | 2580 | `87967ac9b6ab2b4d004663764a25a623e9e78793f170db52cb9b023f7a6ba551` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P09 最終解包驗證/README.md` | 4442 | `1e3a148c51c7d2e0e10e002ab21fb8e613f78694b63857b48a29cd40ddf05fae` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P09 最終解包驗證/architecture-and-boundaries.md` | 6150 | `63ac9c72431ac154e1a88c588949515bd30ee24f8b848e2454ec4ff2e8746968` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P09 最終解包驗證/delivery_hashes.json` | 326 | `9bf448eecec3b38207dafc8c8e11ad39e02ec1e06bd245460e4654405ae9ddb1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,envelopes |
| `主管/P09 最終解包驗證/dependencies.json` | 1761 | `4a1eda7c6a66d0010b83ba92a74fb0e01cfdc24245e8b1fc9c261275d4d50bbb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=dotnet_sdk,runtime,sdk,docs,restore,runtime_install |
| `主管/P09 最終解包驗證/global.json` | 55 | `fe15c79ed27b7261f804746cdb9b8343003b2ecbdbc1cde8cd34e0eda4149921` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P09 最終解包驗證/manifest.json` | 256331 | `6ae49bb0da0fe02ac04d8914aaf5d2446cf0822ec7f8c8170f3c3a82c2540548` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,excluded,artifacts |
| `主管/P09 最終解包驗證/result.json` | 1780 | `60d910db399ea40779d61cdd188ad209fd117da0c3e62c3a2f1fa47611136731` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,scope,tests,native_audit,report_validation |
| `主管/P09 獨立解包驗證/HANDOFF.md` | 2580 | `87967ac9b6ab2b4d004663764a25a623e9e78793f170db52cb9b023f7a6ba551` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P09 獨立解包驗證/README.md` | 4442 | `1e3a148c51c7d2e0e10e002ab21fb8e613f78694b63857b48a29cd40ddf05fae` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P09 獨立解包驗證/architecture-and-boundaries.md` | 6150 | `63ac9c72431ac154e1a88c588949515bd30ee24f8b848e2454ec4ff2e8746968` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P09 獨立解包驗證/delivery_hashes.json` | 326 | `1a20eac55ca164f46efdbc650c2ef91819fd54c3f03047a363beb3b2afea54ea` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,envelopes |
| `主管/P09 獨立解包驗證/dependencies.json` | 1761 | `4a1eda7c6a66d0010b83ba92a74fb0e01cfdc24245e8b1fc9c261275d4d50bbb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=dotnet_sdk,runtime,sdk,docs,restore,runtime_install |
| `主管/P09 獨立解包驗證/global.json` | 55 | `fe15c79ed27b7261f804746cdb9b8343003b2ecbdbc1cde8cd34e0eda4149921` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P09 獨立解包驗證/manifest.json` | 255027 | `dd159bc9e6ea1b196b228b76530e92a8304c22853e7cbc1e69fa172d4d66a9af` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,excluded,artifacts |
| `主管/P09 獨立解包驗證/result.json` | 1780 | `60d910db399ea40779d61cdd188ad209fd117da0c3e62c3a2f1fa47611136731` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,scope,tests,native_audit,report_validation |
| `主管/P09-獨立報告渲染/result.json` | 7047 | `ff3814f3ddc4a67f4681c1297cf492ca1f219a0ed2206b602580c633e7c0510d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,root,browser,offline,browser_closed,documents |
| `主管/P10-歷史schema工具資料/index.json` | 20078 | `be222755d1d6754aaa129273515520c10401d6211a783bcaec61f2b1a4882b58` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,source_sha256,classification,records |
| `主管/P11-獨立驗收/delivery-audit.json` | 315 | `1f49a64e009b1085fcb2080ed73b715cc894955859c0baed2e0bf5e7d758264d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,delivery_files,tested_inputs_unchanged,reference_inputs,jar_classes,scope |
| `主管/P11-獨立驗收/snapshot.json` | 41303 | `ee4970d59a7c347cd2e4f3c89450fb95f261da725b5afedf17e9c310cc4b13d2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P12-獨立驗收/delivery-audit.json` | 298 | `c87e824d93e132114448c1c32e6b379e394840728cd2dfea0ae359966ab5533a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,tested_inputs,unchanged_copy_sources,original_java_references,jar_classes,delivery_files |
| `主管/P12-獨立驗收/private-case.json` | 274 | `c14b2cabc58589466610312f45c4e6a32255678b1e55e9ee5a072061142c46cd` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P12-獨立驗收/snapshot.json` | 47140 | `d4e79185504ab90b6bf177d3a52b8b9ffb8809b938afce381e8756753954cbf1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P13-獨立驗收/delivery-audit.json` | 337 | `919221f53e4867ab8604e719f7e92ee213d88bae504d518dbe15d254ec04d4a7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,tested_inputs,unchanged_copy_sources,original_java_references,jar_classes,world_references_and_classes |
| `主管/P13-獨立驗收/private-world-case.json` | 286 | `ed85c9cde12d6b0980420e9cecd0f10878c52d750769a5fd65df76116ecda559` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P13-獨立驗收/snapshot.json` | 57154 | `3b733141b91c70188b149f300631eac0b4a67b59ed1f16691f2c32b4a70760f4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P13-獨立驗收/world-regression-execution.json` | 231 | `beffeefae845d1948a7c1483c4ef2551a66ea24d76c3e7f21440c52329314292` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=exit_code,status,first_observation |
| `主管/P13-獨立驗收/world-regression-first-observation.json` | 592 | `de9998205af13de5fc5f62dabe7b90dd802ee0d5180f94896088c9e8f72513be` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,capture,source,packets,rejected,wrongName |
| `主管/P13-獨立驗收/world-regression.json` | 592 | `de9998205af13de5fc5f62dabe7b90dd802ee0d5180f94896088c9e8f72513be` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,capture,source,packets,rejected,wrongName |
| `主管/P14-獨立驗收/delivery-audit.json` | 109 | `04da11d26ac45f9dba8e4958a87888306da81ac6054cb5382d97e8de9b88abfb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,delivery_files,tested_inputs,goal_receipt_verified |
| `主管/P14-獨立驗收/freeze-audit.json` | 113 | `dec956b28b69482e9037f25a60849fd3c73177ea6943a26d87da7bb8d8b76571` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,counts |
| `主管/P14-獨立驗收/movement-regression.json` | 436 | `aaa2fbec1e75a8729105ec79abc332a54a89af732c6f2fc580c3e0e4f562ed00` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,kind,delayedBeforeAdmissionMs,remainingAtAdmission,admittedAt,queuedConsumesNothing |
| `主管/P14-獨立驗收/private-movement-case.json` | 436 | `4c2b0e1c456508b3de30ac58ddd126704ac83e8ecd85598a2cae9df615fb1a0e` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P14-獨立驗收/private-world-case.json` | 286 | `8cdd33755e0dfad74d55f180100aaf1f2839babc871501590615fd3a3a351e67` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P14-獨立驗收/regression-execution.json` | 158 | `1330c807140479c664cb20fb2326ab27df1d112c2173eec402866b88ad3433e0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=mode,exit_code,status |
| `主管/P14-獨立驗收/snapshot.json` | 68682 | `29ebf4a7fd209a0553e96e4abdacf2be2df12233900e234ebc52ce9d782b9471` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P14-獨立驗收/source-audit.json` | 89 | `cec52c36af590e7a875fd0b1def8d6847ae1a045c0ed29c7d2e75f59d5a0a9d5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,unchanged_sources,unchanged_tested_inputs |
| `主管/P14-獨立驗收/world-regression.json` | 592 | `b71458cf76f358874a8c03da291a7732b287853760efcad2a2733091605012cd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,capture,source,packets,rejected,wrongName |
| `主管/P15-獨立驗收-r2/build-result.json` | 107 | `a808fc513209dc3c01d9d69066c48cd5b18076406a51c4286208807513ec18c6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=exit_code,app_sha256 |
| `主管/P15-獨立驗收-r2/delivery-independent-audit.json` | 111 | `e300d2aa8b0879997161842f4c7dc502197fd7f415add48c20fff35548bc43c4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=delivery_count,delivery_mismatches,result_checks,check_mismatches |
| `主管/P15-獨立驗收-r2/final-session-release.json` | 4398 | `34dda13731e72e1ed22822c7ce66d80989000cba4de7028d9f2e3e1592e8a2de` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P15-獨立驗收-r2/freeze-source-audit.json` | 442 | `f7c3e969948d31d19a58cc4a227105880d243ba713c8733201f464255793c0ca` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,source_freeze_files,environment_files,snapshot_files,java_jar_classes |
| `主管/P15-獨立驗收-r2/private-movement-case.json` | 439 | `f3bf122f6ff24e2e0b6fe4a517920634f816caec0d8a656f190acdeff4453a01` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P15-獨立驗收-r2/private-world-case.json` | 289 | `1e7bcfd3db30afd1b2440299fc223f2f227112843151343bcaf6ca1e715a0781` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P15-獨立驗收-r2/revision-independent-diff.json` | 91 | `e2157db6fe3ca020ff61ed8d388896000b358fc20a255cd829372c6e3bbc0ffa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=changed,added,removed |
| `主管/P15-獨立驗收-r2/snapshot.json` | 123684 | `c57a1b7d4b2d032cf5a460b1392807188b0bf74653211849de3b0d4d81971fb8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P15-獨立驗收/build-result.json` | 268 | `0155b12b3a12abacfa256a9db0bc24682e058a64978a63235f76b26550167490` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=exit_code,snapshot,scope |
| `主管/P15-獨立驗收/failure-handback-sessions.json` | 469 | `e7a3149f68b933dea19007f67861de108927637bb010689cf2141bc18d0738cd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=multi,observer |
| `主管/P15-獨立驗收/first-environment-freeze.json` | 118981 | `c98a241b2a09516278b20396189f18b3b82366949a4f5e6bb9ff7333323fe8b1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=files,db,server |
| `主管/P15-獨立驗收/first-source-freeze.json` | 111568 | `4d5e7c4f44eae870a6b199fcf513c6637c71d9bb1ec5ed0e99b3f6258fe0ca2d` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P15-獨立驗收/freeze-source-audit.json` | 442 | `a5362356059fc8ccbe3971e90fe2164a74095314e9a427ad7c7d9094a7aec1f2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,source_freeze_files,environment_files,snapshot_files,java_jar_classes |
| `主管/P15-獨立驗收/movement-regression.json` | 436 | `8952e86eb571bdeaa8fd20221245b9787ae3fe6a9e6587fac5f867db81160b28` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,kind,delayedBeforeAdmissionMs,remainingAtAdmission,admittedAt,queuedConsumesNothing |
| `主管/P15-獨立驗收/object-regression.json` | 645 | `fc10c207ad88238a76efa737bfb8a2e55281fb98564e8da2c45e265852b1da24` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,source,packets,negatives,duplicateFullUpdates,unknownIdIgnored |
| `主管/P15-獨立驗收/private-movement-case.json` | 436 | `40773db0b7f3fe55a89d4272f928be110239c69a8c4bbde649f48a39f5490a5f` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P15-獨立驗收/private-object-case.json` | 307 | `4a0137e34e48c8dfb68a429d02444b2e1866429dc5459803ec8bb4ccfe1ceaae` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P15-獨立驗收/regression-execution.json` | 158 | `1330c807140479c664cb20fb2326ab27df1d112c2173eec402866b88ad3433e0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=mode,exit_code,status |
| `主管/P15-獨立驗收/snapshot.json` | 123681 | `db0233f3c5baa2e0fb088ed3cbc51bfe4ba6526543f6a1d2f0ad21bf0e1fdfad` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P15-獨立驗收/world-regression.json` | 599 | `74ef4096966fe9b6bad2ee1b9e542be2398642ab28099430b2d8b300b2937ec3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,capture,source,packets,rejected,wrongName |
| `主管/P16-HP隱藏值失敗/analysis.json` | 3878 | `39bd96a62ebe91674d1195d8d33007233a9c51cbb49782dc1f23279b7edfd307` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,source,capture_sha256,last,tail,meaning |
| `主管/P16-HP隱藏值失敗/result.json` | 481188 | `63d5cf39e6c9cc01be86f0dfb817f7dcd0e48f1e721895fd482e43dcbf89c827` | U — U08：opaque Base64 候選；來源 unknown |
| `主管/P16-R2原回位清選取失敗/result.json` | 380491 | `d4a56f701ab99b34f9b4416cb8c5e8a5333df2ed5800e2d6091fea684ab4a59d` | U — U08：opaque Base64 候選；來源 unknown |
| `主管/P16-R2原回位清選取失敗/review.json` | 1005 | `01e03bb67c5b2abd9910a6e114cacdbeb24c50d6893ca4bdeb3dedcde53fd8a3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,source,capture_sha256,error,target_id,attack_reports |
| `主管/P16-R2近戰UI初審/result.json` | 2509491 | `40e7ac40ea1c89a5d3ef9b1fd9364e5ef5c096daa7d67fa2e3ebbfd4138c9584` | U — U08：opaque Base64 候選；來源 unknown |
| `主管/P16-R2近戰UI初審/wire-review.json` | 21101 | `49f7c2b12c4a6201792eea0a84c7956d8bb0a201d22b9f946775b7ae751de949` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scope,capture_sha256,own,target,main_packets |
| `主管/P16-初輪狀態審查/manifest.json` | 783 | `b59302544ff966179bf983fbb28df3f960f5f9daa95793305ce7fd9937033459` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P16-原服長時UI初審/db-checks.json` | 1385 | `099a82c4e35398180d82a67b37df2042fb3170a105a00cd652ca996608f8d769` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(19);keys=field,packet,db |
| `主管/P16-原服長時UI初審/exit.json` | 73 | `c36558224fd97976ad6b6416e348724626a383b3e117328cf4b3ab618187ab12` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,exit_code,seconds |
| `主管/P16-原服長時UI初審/wire-review.json` | 1034 | `8262aa9ded727e8289d8dae6b4d6af83e63c92e017ad93ddd1b1366919bd7cec` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scope,capture_sha256,packet_count,applied_sequence,times |
| `主管/P16-原服長時UI初審/world-ui-result.json` | 99223 | `07eda9f114559247d8160b70546d8c06aeb3dcc36445e95391bf70926eefc0c0` | U — U08：opaque Base64 候選；來源 unknown |
| `主管/P16-原活動距離獨立核對/manifest.json` | 1241 | `c503c7c40fded43aa194628fc558293f7a78a46caffd2a3877803309fb1ec653` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scope,classes |
| `主管/P16-原近戰參考/manifest.json` | 4565 | `743b90414bf1af67b996f3301777da605b556ad4a93056feda927d0c814071f8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(7);keys=class_name,jar_entry,class_sha256,source,source_sha256,javap |
| `主管/P16-可見近戰UI初審/db-proof.json` | 113766 | `a597753fbca4b5872c97274488db1d788ca31b30e35b02e06cc9e00fa994b6d5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,after,accounts,server_before,server_after,samples |
| `主管/P16-可見近戰UI初審/result.json` | 1076163 | `30ad8bf087f7d685df141baba4464acfaf25b555009c8d429a51b907598b619e` | U — U08：opaque Base64 候選；來源 unknown |
| `主管/P16-可見近戰UI初審/result.json.after-recovery.json` | 50067 | `1603166e4fb873b958da474775101407309c5faf676a7c4a507866e665ebdac1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=phase,requiredHp,before,after,elapsedMs,method |
| `主管/P16-可見近戰UI初審/result.json.before-recovery.json` | 48992 | `d93774d5e734f674ee240931033e125befbc133fe454b95e1fc91acccd046028` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=phase,requiredHp,before,after,elapsedMs,method |
| `主管/P16-可見近戰UI初審/review.json` | 1188 | `04916bdc797a053abff3a125701051df10e6193ccd3df72b74b99787595d1838` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,images,position,observation,wire_result,limitations |
| `主管/P16-可見近戰UI初審/wire-independent.json` | 12729 | `c678cf2a2c4e0d6d62bc0087a7e139c9232c5d16d1f7cb3a861b1972de185a2d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scope,capture_sha256,own,target,main_packets |
| `主管/P16-屋頂遮擋UI首輪/db-proof.json` | 17528 | `f39e01661fc0c1b5293cdfc74b9054257e89ebd9164ae6bb4308fd2c2b5981a2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,after,accounts,server_before,server_after,samples |
| `主管/P16-屋頂遮擋UI首輪/result.json` | 409563 | `faf2b9a82add3cde8b60e45381e59ecf214ec98adfa395f4fd4da45b89345fde` | U — U08：opaque Base64 候選；來源 unknown |
| `主管/P16-屋頂遮擋UI首輪/review.json` | 854 | `413b054c57a84f59ac05cf00c8ad38b87467344ad00b3761f25fedf78bfeaa5d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files,finding |
| `主管/P16-屋頂遮擋UI首輪/wire-independent.json` | 6381 | `c23255c50598879eafde319b4dfee9e18e280c56a225699c2c3c354c593c8dea` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scope,capture_sha256,own,target,main_packets |
| `主管/P16-工作者首輪通過/db-proof.json` | 15235 | `e4da41de3ddc1249a35fd806769f03027b7d78e3b3d0a954fbc515e4554f5142` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,after,accounts,server_before,server_after,samples |
| `主管/P16-工作者首輪通過/independent-wire.json` | 8955 | `8f38268b58ddec2a8d1c77b2b0a2849e608a6a542d70826e80bce790ec46ce4d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scope,capture_sha256,own,target,main_packets |
| `主管/P16-工作者首輪通過/result.json` | 679623 | `5c9cb027d20cc9b45679ea3a332af9f084dfc1e6e5efb6950ae4a3d0461f75b3` | U — U08：opaque Base64 候選；來源 unknown |
| `主管/P16-接收負載完成核對/analysis.json` | 777 | `3e7b7f2c266a08fc7c2abae5c707e36eff7afbb9564511a38844f81e3f157b7a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scope,events,ordered_time_states,first,last |
| `主管/P16-接收負載完成核對/exit.json` | 73 | `e43165f53c9970928c7afc661f0c42beda8a169e8e002e58f4e10f50e39b16c7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,exit_code,seconds |
| `主管/P16-接收負載完成核對/processes-after.json` | 19 | `8b04685f9aa082fb693dc2c04bb566ad81a117be365d4e4e4d2f2c10f19a6a05` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=items |
| `主管/P16-接收負載完成核對/receive-load-result.json` | 738 | `64fd46ccad01909b6fbc2a073d215656ca7055e192187f162035a80e8466be94` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,kind,received,transmitted,expectedSequence,order |
| `主管/P16-日誌與接收負載首輪/journal-independent.json` | 483 | `95ac4193bf32db859e9fe930243dfb62ce0540931827dc0d291ade3f94eae8dc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scope,events,jsonl_bytes,jsonl_sha256,json_sha256 |
| `主管/P16-日誌與接收負載首輪/journal-result.json` | 575 | `e2b01cd4ba3b68bb9e418852399e5295f395b6da337ac2cc13fbc6e72570cbc9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,kind,events,jsonlBytes,legacyBytes,memoryGrowth |
| `主管/P16-日誌與接收負載首輪/receive-result.json` | 729 | `e1618cfab68c1327a062fa194e649fccb3ad11ccb2841176cee5092378402116` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,kind,received,transmitted,expectedSequence,order |
| `主管/P16-日誌與接收負載首輪/supervisor-review.json` | 371 | `bea3beb777bba94dfc11f4eef1e18bced7308491f74e124caa4a0afc8d65bdc3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,journal_events,journal_bytes,journal_memory_growth,receive_packets,receive_max_timer_gap_ms |
| `主管/P16-獨立驗收-r2/final-session-release.json` | 2824 | `61836566d4070befab794f19482b9da990ff381ff91dfe5bd8dcaa8a06fa1434` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P16-獨立驗收-r2/freeze-source-audit.json` | 571 | `ad6ae0c0fd61e0616cd5c0c6f06f2de37769393cbeeb0b4f0f5dd2e6d0678631` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P16-獨立驗收-r2/private-combat-case.json` | 312 | `9480c9c2442565c48c0bc2fa7d9f5fae7d11975f5a4aba53fb949e63b2fbc334` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P16-獨立驗收-r2/private-movement-case.json` | 442 | `f305c76b0ad6ae9f161d4a7bb99a21b8b8c8950711dec6a9c3602b49daa2e8fc` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P16-獨立驗收-r2/private-object-case.json` | 313 | `55fa3f6a4d81f52125fa6b89385c68dead0fdd033071dcd054928ba1cca32c37` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P16-獨立驗收-r2/private-world-case.json` | 292 | `c6670c133809648d0bbe667393737e9b88b8d0605f4cc4623907daa9759c50da` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P16-獨立驗收-r2/reused-assets-proof.json` | 135 | `5510803794c417c95e01ddceb119207f06a7a207d2f42e7e4b970c36df51ad33` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,unchangedInputs,frames,proofSHA256 |
| `主管/P16-獨立驗收-r2/reused-r1-proof.json` | 2690 | `4687215a189c9a4c228bdde1de5608c956a696b49939d7fd6f9c7c1b0401449b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scope,changed_inputs,product_changes,evidence,tcp_cases |
| `主管/P16-獨立驗收-r2/snapshot.json` | 164526 | `b7abf18b324279fbc7a5c58d38d02df1b3d96a68a8be86895ddba3470a829774` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P16-獨立驗收-r2/supervisor-regression-visual-review.json` | 902 | `8c6c57d61e852cd862bc6a02d49de99bcc7bc6e0ab2e6c21ea6a63c3d374d5cb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scope,screenshots |
| `主管/P16-獨立驗收-r2/worker-HANDOFF.md` | 8315 | `2744a7ddea3a4718b7d4df3dca0393deb10f0374b64f3131bfa55ada9d80b2ca` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P16-獨立驗收-r2/worker-PROTOCOL.md` | 9083 | `1c32f3558e815cbdb9816b573d93a598259cab6c950c90141e47aa6b1b6e2a72` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P16-獨立驗收-r2/worker-delivery-manifest.json` | 5972 | `44ea0d5ff67e4229e9bcc993b689ac5d91f158e6d482ef81978217f807d59b1a` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P16-獨立驗收-r2/worker-environment-freeze.json` | 141468 | `3610f2bb2eec218adbc00bb81f3dce3d5a51ee45ab7a7f432a049f8253dec99b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,server,db,files,noRestart,bind |
| `主管/P16-獨立驗收-r2/worker-ready-for-supervisor.json` | 2560 | `f57308bdc4d474bbb10e096c11172dbf8b74ec20b5ac5e21c69dbb8ae6c1d689` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,thread,sourceFreezeSha256,environmentFreezeSha256,server,db |
| `主管/P16-獨立驗收-r2/worker-result.json` | 9257 | `14b1e3b8ae570794816b03edc44406e0e31d425111f4b5385980f33ab0469663` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,status,completeEngine,goalComplete,jarSHA256,endpoints |
| `主管/P16-獨立驗收-r2/worker-source-freeze.json` | 169427 | `9811dacf70fd64292cc0e687729aa188e2bd85ffe12d3606a062e5e149d9ad15` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files,mutableExcluded,utc |
| `主管/P16-獨立驗收/freeze-source-audit.json` | 471 | `9ae6c0294f46f3f574c6c6803b517c9e452b6e114ca437ee614ad8b35996ee2c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,inherited_sources,source_freeze_files,environment_files,snapshot_files |
| `主管/P16-獨立驗收/handoff-os.json` | 1217 | `ed8dbd5382321e0b137d3d231cebd3bf054082dabc4e84ce93ea111fb182376a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=captured,processes,listeners,clients |
| `主管/P16-獨立驗收/mysql-runtime-audit.json` | 13587 | `1df91a594f777b89d1a210b8bd8fabc6d057cfa5a7c35c83d26be59803493b00` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scope,files |
| `主管/P16-獨立驗收/private-combat-case.json` | 309 | `391411653d24b23e906e3107e0cb27773be0c40928bc0caac4fd5788a506116d` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P16-獨立驗收/snapshot.json` | 162636 | `331b7587e998ad93a69cfaaaff79f69dcab92202cac04f0bf2441a6634792a63` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P16-獨立驗收/worker-HANDOFF.md` | 6953 | `9b6135f56987aded9ce5bb5d85e4de57d95375d623070df4f4b36b137eeebb1b` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P16-獨立驗收/worker-PROTOCOL.md` | 6585 | `af06373b441299944371407b9497d514db596ce4c6760088d2403e86eddf83bc` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P16-獨立驗收/worker-delivery-manifest.json` | 4559 | `7c11b45814aa9204f78b6a62113653b8123fed20533c48aa8e23846cc2d96081` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P16-獨立驗收/worker-environment-freeze.json` | 141468 | `7837e28b88c1d3d4c45ee8a5e5856194d71aac7ec08a0c97e30967f8c276f11d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,server,db,files,noRestart,bind |
| `主管/P16-獨立驗收/worker-ready-for-supervisor.json` | 2560 | `9a627de0fa8468ae6871920ff6056e1c484fc0c8281d9a9db72ed7bc3559707a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,thread,sourceFreezeSha256,environmentFreezeSha256,server,db |
| `主管/P16-獨立驗收/worker-result.json` | 7223 | `ce453bff5711d475cc92198d44420cf317321377d452e29072f5dedc18676797` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,status,completeEngine,goalComplete,jarSHA256,endpoints |
| `主管/P16-獨立驗收/worker-source-freeze.json` | 167267 | `1f4d33c35da0125b8620ed05e1dcddb62b3c0157dd7365cbfd8b91496aa20660` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files,mutableExcluded,utc |
| `主管/P16-長時日誌審查/manifest.json` | 1016 | `49a74504408dedfe1c960b50eadd32cf2c332497168f6bc239b2ad33e28b0fd4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,files |
| `主管/P16-長時阻塞失敗/analysis.json` | 10213 | `33aeb779033e1b14870fffdf8f0b6254eab5595d9d61586691e598e911804aea` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,sha256,error,streams,hp_updates,final_state |
| `主管/P16-長時阻塞失敗/result.json` | 2004798 | `b9d24b0c07feabd491cd554872bf3a940d4491bd375c33be5303e76c2163c607` | U — U08：opaque Base64 候選；來源 unknown |
| `主管/P16-首輪近戰失敗/analysis.json` | 2886 | `f552060b08d591890d713577160ad31fac8d4c6ae5061f246888ef065c8d88ad` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,capture_sha256,error,raw_evidence,last_vitals,target |
| `主管/P16-首輪近戰失敗/db-proof.json` | 20912 | `d10992c5551f82a990a867f36c3bf11da08be02ef1c6ef5729388fb58e218811` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,after,accounts,server_before,server_after,samples |
| `主管/P16-首輪近戰失敗/independent-wire.json` | 4701 | `68f4ac2cb369d75763a1cbe24eb2e050e7ee8ed6a68b74724b0ac0a581862e6b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,melee_packets,self_hp_updates,hp_meter,note |
| `主管/P16-首輪近戰失敗/result.json` | 553204 | `83488737add557c0d9647d9d3c2f69a54112def785660d4b7aae8cd3704a6593` | U — U08：opaque Base64 候選；來源 unknown |
| `主管/P17-原協議獨立核對/manifest.json` | 9506 | `81d9c5100d122550775ee287bb894f1abeebb5c0a89cbdc4ca7940be92e7efeb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,jar,jar_sha256,classes |
| `主管/P17-圖示像素獨立核對/475.json` | 36 | `245dd94ff6a0580813fa22f195ce2f065d93b3714209fea63ebd10ef68c09134` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=x,y,width,height |
| `主管/P17-圖示像素獨立核對/5.json` | 36 | `137c39dc12c5d617c1936c5f4a3ba6770971d35edd07cca070b6487a71286ed2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=x,y,width,height |
| `主管/P17-圖示像素獨立核對/81.json` | 36 | `da5887df12dac434f7062c40c48fec7bf6f6372ba04e5a52fb7a7cdc6800dc91` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=x,y,width,height |
| `主管/P17-次輪動態佔位/db-proof.json` | 16617 | `87ad6ffae30bf3133cb72e58d53f4584d1c4b11fa21967617e82125bf2961667` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,after,accounts,server_before,server_after,samples |
| `主管/P17-次輪動態佔位/result.json` | 524185 | `2d6b5cb477fe9af85745c9104d817914f4298c8740ed2dbeb002a84f187015e8` | U — U08：opaque Base64 候選；來源 unknown |
| `主管/P17-次輪動態佔位/resume.json` | 520 | `c4ff9b26723824a74b1f263246981c6e688fabec0bc2056898d191ba691c3cd7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=reason,previousResult,previousSha256,initialCount,previousConsumed,previousRequests |
| `主管/P17-獨立驗收/P16-source-comparison.json` | 1300 | `ef74d2438dbb531a8c1c69f1e9581c7b91a6080f747e39710a72ef324d7a5282` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,unchanged,changed,added,scope |
| `主管/P17-獨立驗收/freeze-source-audit.json` | 527 | `9c952f609cbfe598877cc3ce74f39168945030686fec14926a2979c9ee065079` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P17-獨立驗收/icon-proof-reuse.json` | 1606 | `66cc25bb37211c0546e7e94c44a0e181b8f393d1ba26faca0dcfe2d468343b1b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,independentOriginalPixelProof,exactInputs,pixels,scope |
| `主管/P17-獨立驗收/private-inventory-cli.json` | 323 | `a4fbe1b1bfbdb98fb0c3d241d3dc2de84d49e8d135a04a28d11ab9b470ce6214` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P17-獨立驗收/private-inventory-ui.json` | 325 | `e824c79875b377625f209a44ca4dae4abe26029550dd5d0a772c4c35e7d430ee` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P17-獨立驗收/session-release.json` | 2945 | `547f0d8e1cd8c062eb7ecd511d1143841f58b9a6f667faf2af7b584c7381a862` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,services,roles,clients,dbIdentity |
| `主管/P17-獨立驗收/snapshot.json` | 186013 | `bcbba9e2558e2507bce747de71f97a1a669e0780ff3127ab27f6be9d57bc2c99` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P17-獨立驗收/visual-input-review.json` | 1379 | `22ca4f5e3d74c34f486ec57a3925fa256c5712719ac808fd0b539c9df04cf760` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scope,gameAndWebNamesCountsMatch,webUseCommands,images,observations |
| `主管/P17-獨立驗收/worker-HANDOFF.md` | 5509 | `046884db7cc9d0752bfe4cdbc79cdcbfffa228236f8bda739e0359f6345fea3e` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P17-獨立驗收/worker-PROTOCOL.md` | 6459 | `43ba5acddf878463615da39ec980bf94f7b25e381469b11a7d8826f47be8e60e` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P17-獨立驗收/worker-delivery-manifest.json` | 299242 | `e0f1a9731e976ecb7dcacd79fad9e4611b4afeb145cf12eae13fe4db81cc17cb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,excluded,files |
| `主管/P17-獨立驗收/worker-environment-freeze.json` | 160642 | `045a1fa61fd9e21113dfbaad4bec92dfdfbd934903c1e0c4f24eb9339c8bc490` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files,db,server,exclusions |
| `主管/P17-獨立驗收/worker-ready-for-supervisor.json` | 3376 | `a9911deb6efbc6b15741cf378fa8f076b7d42f65574ca5af86e758a7aee558e2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,goal,server,db,clients |
| `主管/P17-獨立驗收/worker-result.json` | 1672 | `37c7be7e3d4e1ef1b6086e092fd14781878d617b7d3a5295e2bf7b923e82d229` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,goal,stage,scope,fixedJarSha256,cli |
| `主管/P17-獨立驗收/worker-source-freeze.json` | 189180 | `aa367e6b044ac8ed0ce0e998f4561e3357ba306b0ef8c66cc611a89b12cb96b1` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P17-首輪冷卻邊界/db-proof.json` | 8599 | `6d158b3499c1fff82ceca9ec1e89ea9a69275968d7e34a744cbf127f161a88a1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,after,accounts,server_before,server_after,samples |
| `主管/P17-首輪冷卻邊界/error.json` | 142 | `a3bd92aa40a622623de8aeec03adc4e545807d9be529c9e264a295a2f2c2bc51` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=error,type |
| `主管/P17-首輪冷卻邊界/result.json` | 232538 | `635d5e36915c986fbb9d9fec6506741cc0182bf2fd5dd83f924ccfbf546aeef4` | U — U08：opaque Base64 候選；來源 unknown |
| `主管/P18-原協議獨立核對/manifest.json` | 8442 | `81c43e41eb0c2c6993d042c0172be442a6c5e37bc14caa2a042652c937e248de` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,jar,jar_sha256,classes |
| `主管/P18-拾取朝向原始依據/manifest.json` | 2889 | `f84177bc64969675fab47e585151f66781b4d2aa83d18bb689261ae7adab48fa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,jar,jar_sha256,classes |
| `主管/P18-獨立驗收/freeze-source-audit.json` | 528 | `a8f9651d4fd48df45841df1b549b4aad672f2b8bc9c9f7dd17fc4271a63ba5f0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P18-獨立驗收/private-ground-cli-r2.json` | 304 | `fbcd251535df45216da0ac313b08709f481eceb49971fdcb12ad0587f35234c1` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P18-獨立驗收/private-ground-ui.json` | 303 | `1b535131e1a1ae081754c1a12fc4d9d20408af9e61dfec96e24934dc922d8a81` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P18-獨立驗收/snapshot-exclusion-correction.json` | 1016 | `ab1afda2eb28854ee889d9c325286ebcca4b9411c137437e3c2b3a8d6c906f91` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,failure,removed_review_copies_only,source_files_unchanged |
| `主管/P18-獨立驗收/snapshot-first-attempt.json` | 207804 | `a727bf007ec5ee06d582741358b43f3a691fdfa5dbff8b2fc960647e716cec10` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P18-獨立驗收/snapshot.json` | 207176 | `828113f68039ba31d21ec3cca3a84a655cb20765977c1f6f1b25ef2dd49a1773` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs,review_correction |
| `主管/P18-獨立驗收/worker-HANDOFF.md` | 7121 | `a5bfe5f4efa6529bcb189b2f2661d57d32a54bf6152998105851b02f8ba5ff7d` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P18-獨立驗收/worker-PROTOCOL.md` | 6520 | `12d3b5df5b11ed2d8b71d38796d4e87f37acd02e7ea25ae6bcf2108c85b44632` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P18-獨立驗收/worker-delivery-manifest.json` | 303730 | `f192685f13d56d2a63ce20d441275da56ae9676875adc3d0d826ad6787685497` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,excluded,files |
| `主管/P18-獨立驗收/worker-environment-freeze.json` | 160585 | `4f558bf6ce9f66e7853846fecf0aa6acf9d993e3f3dbbae85ca6b06246b918dd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files,db,server,exclusions |
| `主管/P18-獨立驗收/worker-ready-for-supervisor.json` | 4604 | `6d34280404e8faaa83a6e9ff8d71d74364775cae22ca0d8d18cee27cb14f2309` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,goal,server,db,clients |
| `主管/P18-獨立驗收/worker-result.json` | 2575 | `cd599d075c89aef2a56c3fe2074cbd6e1233993a8b612bf64c631f815b72e051` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,goal,stage,scope,fixedJarSha256,proofs |
| `主管/P18-獨立驗收/worker-source-freeze.json` | 211500 | `59e155a418dabb5297c7d9d14b5963da2152ebc1b8d5d30fa42ebe1458c56c9c` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P18-空背包原始依據/manifest.json` | 1325 | `4480a56b01516a039b04a8eff0bcf758c9af6c370ef8bfecf9dcd55f0b40a29c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,jar,jar_sha256,classes |
| `主管/P18-背包肉圖示像素獨立核對/124.json` | 36 | `0285348e1a086e65fa952ae83e6d9a4da856bb1211a9f1be8e339b90817fbffb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=x,y,width,height |
| `主管/P19-獨立驗收-r2/freeze-source-audit.json` | 528 | `a8f21ef6a7def1cd9ded6682ad13775c47ddc3547e5a060e3a7995bc8276890a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P19-獨立驗收-r2/private-reward-ui-r2.json` | 328 | `6d0a4a060c380c1cfb656afa92a9f3f871399e49b9c2c91a62e14b8fdaa94757` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P19-獨立驗收-r2/review-entry.json` | 686 | `694eb588f2cab4ea26bf1f2c91d7542ec6e28a3eae5addee6e0af6579fefa3c0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,changed_existing_inputs,source_sha256,environment_sha256,ready_sha256 |
| `主管/P19-獨立驗收-r2/selection-race-regression.json` | 647 | `57c8f902be00cfae64c66ac8c6f6747f1c06a5fd8412a1ef4ac72dc0ee047602` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,kind,capture,sha256,id,removals |
| `主管/P19-獨立驗收-r2/snapshot.json` | 227683 | `90f6328d7e337123d058a39b7729429e65d96e17c32cafb4b87b293d0a03c2b8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P19-獨立驗收-r2/worker-HANDOFF.md` | 7898 | `a0ea07d411bb79b57329b5995f643f90c87cf1320d98e25fe624a8fd2c31a3da` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P19-獨立驗收-r2/worker-PROTOCOL.md` | 8532 | `8b0600c32ef3c216cb7f1e5eeb43de136d54c371c1e4a3646aeafa2709d04d48` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P19-獨立驗收-r2/worker-delivery-manifest.json` | 304698 | `1399b85e77023ea6696deae05810df019312b8ab4a6597f36d8db5219a27ba8c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,excluded,files |
| `主管/P19-獨立驗收-r2/worker-environment-freeze.json` | 160560 | `44c218592b5d4f8af60ac2a3d68c034d92001b15f0bbd83ad20a7ab3debb9520` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files,db,server,exclusions |
| `主管/P19-獨立驗收-r2/worker-ready-for-supervisor.json` | 5522 | `082b55229054fcacfa93159d0fd4b3c8c6af41ec5827d1bf08389fb8f3d3e1b8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,goal,supervisorThread,server,db |
| `主管/P19-獨立驗收-r2/worker-result.json` | 2342 | `5fd2931863e3cc82c5caa1e6651104f750ed9ef3257ddc3c9f7b7a07120a7abc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,goal,stage,fixedJarSha256,proofs,supervisorReserved |
| `主管/P19-獨立驗收-r2/worker-source-freeze.json` | 232766 | `fb2c6caaaafef6ecbbae1647fce55a127027ca7dbac43ce26821cd8c840e3488` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P19-獨立驗收/freeze-source-audit.json` | 528 | `2a23dfbd9cc649afe2bcefe55c2bdeb29b890d07e7898cff0eae7acbbc162ebd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P19-獨立驗收/private-reward-cli.json` | 319 | `478b6292986fa64c2a83d7b5cead519a2c85feaf6814575f4a78171f45399d69` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P19-獨立驗收/private-reward-ui.json` | 321 | `1df5e4f83252f8d2f35d557a4928682105d63b26ff5908437d630f323e935d42` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P19-獨立驗收/snapshot.json` | 225150 | `f56ce46d1e4ac9eea723232553f78655ba9c33ce142ded3a75648a059e9d6f82` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P19-獨立驗收/worker-HANDOFF.md` | 5970 | `e4fd465323c748d5ed4aa466400603716a550c10a3af9fc392c468b1d1df1fb9` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P19-獨立驗收/worker-PROTOCOL.md` | 7701 | `1336f2a079f94abc6580708a356934ecafa7da159bd6f37c51ff40b909119bfd` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P19-獨立驗收/worker-delivery-manifest.json` | 292035 | `3c7bc9d60128dfb82de10ddbfbbcfd7284b42e89809a072f3d282b10e544f087` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,excluded,files |
| `主管/P19-獨立驗收/worker-environment-freeze.json` | 160560 | `44c218592b5d4f8af60ac2a3d68c034d92001b15f0bbd83ad20a7ab3debb9520` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files,db,server,exclusions |
| `主管/P19-獨立驗收/worker-ready-for-supervisor.json` | 5375 | `602b87ae62d82015aa409641ccb5785a1989006b85aca23aec16327f9d1234b4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,goal,supervisorThread,server,db |
| `主管/P19-獨立驗收/worker-result.json` | 1945 | `b53e76414a4577b3d47633e3e7d03380ec08512a14f5ad876435a0c2ab1b9015` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,goal,stage,fixedJarSha256,proofs,supervisorReserved |
| `主管/P19-獨立驗收/worker-source-freeze.json` | 230288 | `963939b5b9b0a408b99cde478111e4d559c4c3f8b823f77514a77ac4141e8194` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P20-原裝備來源獨立核對/manifest.json` | 6735 | `c4cd3503bd2b1c06db3bb2d4cced6e2bb259f56145e63a9b60f55798dcbdb541` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,jar,jar_sha256,classes,scope |
| `主管/P20-原裝備來源獨立核對/oracle-run-compile-failed.json` | 1078 | `c48eccb11a5fe8c37789af2464cf4d294cc398868c437dcf1e053707510b2269` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(1);keys=args,exit_code,stdout,stderr |
| `主管/P20-原裝備來源獨立核對/oracle-run.json` | 3217 | `8b56d1123d522d835d85ab5ae9d42305faa32ec49dfe46e28c616b72afb7f776` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=args,exit_code,stdout,stderr |
| `主管/P20-原裝備來源獨立核對/packet-extra-manifest.json` | 941 | `416ff2a23d2e96b43e6f89e8e6051172212249cfbd6a7fbeed91bf285544d120` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=source,source_sha256,jar_class,class_sha256,javap_sha256 |
| `主管/P20-獨立驗收-r2/freeze-source-audit.json` | 528 | `98493d99657c10f26ce936736b72364745f09adcab8e2bdd9704d7dc7de2f4e7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P20-獨立驗收-r2/private-equipment-ui-r2.json` | 331 | `9256dca866c7344b0a4528c95292a4801910890a9df1d0e1921bd862c57a00d0` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P20-獨立驗收-r2/snapshot.json` | 263031 | `5ad5dc3bc10fddb2ef72d0fa385165e71bb4252103819525acd8442c1b9d4e49` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P20-獨立驗收-r2/worker-HANDOFF.md` | 8549 | `5a1bc512354ef937c2aaf4efffeb7a7014ea4e414b3ca3d87ab87e15fba64082` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P20-獨立驗收-r2/worker-PROTOCOL.md` | 5293 | `3ad6fcf44a3cb49f077a2257259b1749351dad8e486be57244069aa801fd976a` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P20-獨立驗收-r2/worker-delivery-manifest.json` | 542173 | `09a60e78d0e9403409a09268271faf755167c25c78b539b5b4e453bd49acc4d9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,excluded,files |
| `主管/P20-獨立驗收-r2/worker-environment-freeze.json` | 160560 | `cdc434700f1984c7fecd0d718f44aaf3300b08d910f2497b1412fa42340f4397` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files,db,server,exclusions |
| `主管/P20-獨立驗收-r2/worker-ready-for-supervisor.json` | 20478 | `2567d95cd9f926a759b40ae9b5e3008d91386e02a19241db0ce72c9e8b0ffad0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,goal,supervisorThread,server,db |
| `主管/P20-獨立驗收-r2/worker-result.json` | 3758 | `f08bf12a55c552a984ee6f200e81f392dd152b4ca9d99ea6d5e157c3116569b5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,goal,stage,fixedJarSha256,proofs,supervisorReserved |
| `主管/P20-獨立驗收-r2/worker-source-freeze.json` | 270521 | `227064f974bbc282d3139d0edd17f9afb0402f29acc6ce7c6239961f8d0b7bee` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,revision,utc,previous,files |
| `主管/P20-獨立驗收/freeze-source-audit.json` | 528 | `2291ee981dca4edf5eb5c79fac4cd3ec8b414c56e3a5f455d1df30fce3d10c99` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P20-獨立驗收/private-equipment-cli.json` | 322 | `a037a35a9f6ad637e6328e44411b466d048b66dd554e695028c731eae4038393` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P20-獨立驗收/private-equipment-ui.json` | 324 | `0be4d55d15f0ba836dad28ca2a53c66227746558bb23662b7383e01745afc611` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P20-獨立驗收/snapshot.json` | 263028 | `9898ae9b5c0321ee22616f78d6d06b86da7c3e518498d1ecfceb749d56e4f2a9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P20-獨立驗收/worker-HANDOFF.md` | 7185 | `03da5ef6591e80bca110699cb87ae91029887f41b5284487cc6df864462636c9` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P20-獨立驗收/worker-PROTOCOL.md` | 5293 | `3ad6fcf44a3cb49f077a2257259b1749351dad8e486be57244069aa801fd976a` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P20-獨立驗收/worker-delivery-manifest.json` | 539492 | `c3505b2ede2b55a85913de465b97dd4b8f0d18f464f17b4d32423a48813c37e5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,excluded,files |
| `主管/P20-獨立驗收/worker-environment-freeze.json` | 160560 | `cdc434700f1984c7fecd0d718f44aaf3300b08d910f2497b1412fa42340f4397` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files,db,server,exclusions |
| `主管/P20-獨立驗收/worker-ready-for-supervisor.json` | 5151 | `2f5b887f48528c491ad445bc93b2241b88c3b23a2698cde3b2b6a19871ee12c4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,goal,supervisorThread,server,db |
| `主管/P20-獨立驗收/worker-result.json` | 3544 | `78d05323f2362aaced86cd7057b2fb4cc07975da8593f673aa6bdae8cd706c8a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,goal,stage,fixedJarSha256,proofs,supervisorReserved |
| `主管/P20-獨立驗收/worker-source-freeze.json` | 270268 | `791c68b825a3157ed40341cbccf34422eb102d31cae1885084554b1a08b111cb` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P20-裝備圖示像素獨立核對/2.json` | 36 | `44d06608058efe986556da2cedf08fae37362fc459be555db040978efec9c0de` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=x,y,width,height |
| `主管/P20-裝備圖示像素獨立核對/4.json` | 36 | `509882ef8d3225fc29331198aa7e338a09bef9abb58f766cf86920f0c8643b39` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=x,y,width,height |
| `主管/P20-裝備圖示像素獨立核對/86.json` | 36 | `822b4b73f007e8b578a17257af16b9e80eb66c7f03b09c19535c859f9901e6c6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=x,y,width,height |
| `主管/P20-裝備圖示像素獨立核對/97.json` | 36 | `08d7c9095f2ada2b95f0a3a7cade5a2d36bf4609ad1825cd8c9ae1cf9f528032` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=x,y,width,height |
| `主管/P20-裝備能力來源獨立核對/manifest.json` | 3475 | `a3c92c7da7dcd38097bfa8772a2ff30650ceab3e0fef29d0a368f67c6c6ace03` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,jar_sha256,classes |
| `主管/P21-freeze-v1封存完整性診斷/environment-freeze-manifest.json` | 142181 | `673c25718b17d0bbb34bebbdbc0b22f187736b6e95a724c7cd852852f2d0a0b7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,server,db,files,liveLogsAndDatabaseExcludedBecauseOriginalServerRemainsRunning |
| `主管/P21-freeze-v1封存完整性診斷/finding.json` | 690 | `16c616f536f10f9fcebcab286f5e138bada06aefba1d4a70b9b345186c7351f7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,missingBuildInput,alsoExcluded,workerCorrectionAuthorized,supervisorReservedRolesUsed,cleanBuildStarted |
| `主管/P21-freeze-v1封存完整性診斷/ready-for-supervisor.json` | 7802 | `25fdfc28d5f330acc6fbb54e1f3f3ff5e410b2ec105857c91ac6efc3f69fb106` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,goalStatus,sourceManifestSha256,environmentManifestSha256,server |
| `主管/P21-freeze-v1封存完整性診斷/source-freeze-manifest.json` | 317055 | `de4daeefca28e2d60e12746da634e2513c32c710beca1315ce7fb3f207e8bcb4` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P21-原安全區射擊來源獨立核對/manifest.json` | 867 | `7eaddb36bfde8665677b8fbf82ecbcdf587a381e052f820b1ba0ce3c715a54a3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,jar_sha256,classes |
| `主管/P21-原弓箭來源獨立核對/manifest.json` | 8667 | `066c80cbe5f04dfaac5a73022afcff5232f546da76733defc7386e357052ffef` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,jar,jar_sha256,classes,scope |
| `主管/P21-原弓箭附加來源獨立核對/manifest.json` | 1558 | `15a27dda6ac0eb31d62607df8c9e6d38b7377a243e8032917d9691d695a517a3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,jar_sha256,classes |
| `主管/P21-獨立驗收/freeze-source-audit.json` | 528 | `a905f3f508e9129354ab633086c02bccf7bd65d47944b3e2bb67fe5f90b6f6c0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P21-獨立驗收/private-ranged-cli.json` | 319 | `f2b791c561fe1005c14d06ac1ddda83fe22b94d8e73321cdfc5dd158f4e887dc` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P21-獨立驗收/private-ranged-ui.json` | 321 | `94adc72bd700ccbdb6f3367ef3f06f9bc0fb83065ee9e2e512e08522418a538f` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P21-獨立驗收/snapshot.json` | 307870 | `a8b83d79ccf058c72dd3bca96a0d1c9eab755b3c0c719712829672f5bf4b116e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P21-獨立驗收/worker-HANDOFF.md` | 3959 | `c42b7c5d8e957d63456493797ee3e50b099eeab88bdc7b55a03ff84dbf0070b1` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P21-獨立驗收/worker-PROTOCOL.md` | 5756 | `4aceec3db6a0cf69652619b2563373f8e24114826efe0bb875be7c79f0f022ff` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P21-獨立驗收/worker-delivery-manifest.json` | 502706 | `058b05ff7ccbc9fcb344302d2cb1347b6a725bc27feeb9a0200b2a6a9a66f241` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=phase,utc,files |
| `主管/P21-獨立驗收/worker-environment-freeze-manifest.json` | 142181 | `673c25718b17d0bbb34bebbdbc0b22f187736b6e95a724c7cd852852f2d0a0b7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,server,db,files,liveLogsAndDatabaseExcludedBecauseOriginalServerRemainsRunning |
| `主管/P21-獨立驗收/worker-ready-for-supervisor.json` | 7930 | `76ab9b386acf13923684dba490bd1f42311f8be7076c920f12ebbe02104ca7db` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,goalStatus,sourceManifestSha256,environmentManifestSha256,server |
| `主管/P21-獨立驗收/worker-result.json` | 14808 | `0c303e46dd928f611bbd445ffa056b177337da1ad63b3f5e25a22284df8887cf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,status,goalStatus,scope,workerCases,regressions |
| `主管/P21-獨立驗收/worker-source-freeze-manifest.json` | 318200 | `5e3215371cfad53d34f0199dc66c349ae89054518b5853aaeac66c75853bdf87` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=version,utc,previous,reason,files |
| `主管/P21-箭算術產品比對-v1/actual.json` | 46268 | `99d50a8f5ff286258402ecfd9f3281552f953ca5c4852feb472a6282be935a62` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(81);keys=direction,samples |
| `主管/P21-箭算術產品比對-v1/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P22-原南區塊獨立核對/map.json` | 2029327 | `0c8abaaea204fceeb152f62eaf9af1a05c28b39c24bc9d04b8520020a78d28fb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=profile,offsets,ground,cover,objects,attributes |
| `主管/P22-原南區塊獨立核對/run.json` | 682 | `f4c630721a4d2cfbc6f94065b3c06b05f3437325bc80514e092b88e295554897` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=args,pid,exit_code |
| `主管/P22-原地形LOS獨立核對/result.json` | 48 | `149103f03f0a25168f7095db8fd32a8abc456a55d0afdbd7a28ffd78386265d6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,vectors,blocked |
| `主管/P22-原地形LOS獨立核對/run.json` | 3652 | `03bcad7a2eeb09720d90439a81c29df7e4209eb03ed8b1c41d4e0263902484e7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=args,exit_code,stdout,stderr |
| `主管/P22-原技能來源獨立核對/manifest.json` | 10393 | `ea16b78ff8c31b503ed0366f9caf42b969fe3ced783b5f665b3e78915ad5873b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,jar_sha256,classes |
| `主管/P22-原技能序列化/run.json` | 5572 | `5eff7ce0d08a166132bbd89292f15cd508c0aa83753d0479c7935dbb53367271` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=args,exit_code,stdout,stderr |
| `主管/P22-原技能補充來源/manifest.json` | 1975 | `6f449353821dce0c15373aefbc022dd3f41433ba02a57f23cf02639934d5cce4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,jar_sha256,classes |
| `主管/P22-原跨區初載與插入向量/result.json` | 65587 | `66acc552afc5a5978cc32c778aed6950ac76f5216aacc06c394a5bb3db0d7076` | U — U08：opaque Base64 候選；來源 unknown |
| `主管/P22-原雙區冷載合成獨立核對/commands.json` | 5445502 | `4575bcd585d262cefc1f98af48efc424346014f7b0710c837448194bce0fcdf7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(26960);keys=x,y,index,tile,uk,offset |
| `主管/P22-原雙區冷載合成獨立核對/run.json` | 709 | `8adf7ad2dbb1ddba407f9b8c02377a8e278589bd0ef8d18c647366d9ef4b6675` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=args,pid,exit_code |
| `主管/P22-原雙區移動獨立核對/run.json` | 3670 | `971c41440d73e8b76ea6ab1a850f4a5845e8c02f786caad18165cbbb63a526d3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=args,exit_code,stdout,stderr |
| `主管/P22-地圖與屬性恢復原來源/manifest.json` | 1081 | `5153aa478b19581e7e2640e800754a5f3bec10c0348196da313a9911320e9eff` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,jar_sha256,classes |
| `主管/P22-技能增刪與光箭序列化/run.json` | 9908 | `83ba7669a50b33c2ad73cc2441987bc951435b45fd71131bdb94c395a76fbac7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=args,exit_code,stdout,stderr |
| `主管/P22-施法請求原結構/run.json` | 4466 | `c3fca9f8a999aada8d969abfe9a9ca3e764e2e658010f9d703ba8ef878802433` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=args,exit_code,stdout,stderr |
| `主管/P22-獨立驗收-r2/clean-build-result.json` | 1773 | `5c4753373cbab428df9047366be87f82d1b737e0a128d8e0b402385dcc843c4f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,exit_code,seconds,pid,outputs |
| `主管/P22-獨立驗收-r2/clean-build-start.json` | 441 | `8e73ca1d7166bc6427c630ae0c0e78c3e3525f67366a7220a3ad200ab2b74931` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,args,snapshot_sha256,ready_sha256 |
| `主管/P22-獨立驗收-r2/freeze-source-audit.json` | 528 | `4deec3f38fa8d95912db7263df089700a100202247d9abdbfc0a2299331cad97` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P22-獨立驗收-r2/independent-bin-reference-manifest.json` | 5484 | `9a3e59808f7808578b2693dd9ebb53cbd01c265bb0c8752444390a8c8995717a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=base,source,sha256,patched,targetExecuted,allChunksMatchOriginal |
| `主管/P22-獨立驗收-r2/snapshot.json` | 385680 | `e74c76526bde82495093b8161bfdd071ab47e0f5d5bca277167a4a7cfcdc7a10` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P22-獨立驗收-r2/worker-HANDOFF.md` | 6045 | `23ee35adf953ce4b36c5748be2bb9bab0083c14572f50d0f210ef80342cf7d87` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P22-獨立驗收-r2/worker-PROTOCOL.md` | 9382 | `20585879e3e775cc6b2e31266d158e9898b2951e1b5106d8179835e59eccd8d1` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P22-獨立驗收-r2/worker-environment-freeze-manifest.json` | 142181 | `b0667ae1902a1e1e61832c4a41c489ebf1bddb837d339d18404073b97de9ac12` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,server,db,files,liveLogsAndDatabaseExcludedBecauseOriginalServerRemainsRunning |
| `主管/P22-獨立驗收-r2/worker-ready-for-supervisor.json` | 27291 | `d4e5b6958f1182c66f0588b60ed969010c5d2d858578526458ce6b272e7888af` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,goalStatus,revision,priorReadySha256,correctionAuthorizationSha256 |
| `主管/P22-獨立驗收-r2/worker-result.json` | 1455 | `6e0127daf62c4f76b05234960f139edf78822e5826ab1c0c715c637998d961ea` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,goalStatus,revision,validation,priorResult,priorFrozenSource |
| `主管/P22-獨立驗收-r2/worker-source-freeze-manifest.json` | 396820 | `449e6bf9c23f618bdd595bbab0e9b7c8c270f7f5a7a1acc9595e32404ca6a569` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P22-獨立驗收-r3/clean-build-result.json` | 1772 | `7548614064bcbfa3d844c2b8038fe480133069ffbc30c25d3568c2376e862ee7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,exit_code,seconds,pid,outputs |
| `主管/P22-獨立驗收-r3/clean-build-start.json` | 440 | `50b8fe271cdfab657551b01f031bffc201fcd079abe87ba66e9e6fa3474cf83c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,args,snapshot_sha256,ready_sha256 |
| `主管/P22-獨立驗收-r3/freeze-source-audit.json` | 528 | `e4e024d8f3d8bf936520962fd9e8bdc79ba6f33b6bf5e2a0d0cc913fb19df8f0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P22-獨立驗收-r3/private-magic-ui-r3.json` | 354 | `af4a65f3e8300cee50960451838e7d38a3bfc0e157dfb9ac0fd78bbd08426a4c` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P22-獨立驗收-r3/snapshot.json` | 389097 | `259a3812932da806b2228bcab0bbda0b7cf077f2bb4afb0ca601afd6214f5cb6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P22-獨立驗收-r3/worker-HANDOFF.md` | 7146 | `fe5cf59d1c9e1648ae569fb224e610680de8435c1b2ea721aba78d0b6086fd0c` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P22-獨立驗收-r3/worker-PROTOCOL.md` | 9919 | `cfc782a1afebfb19e544c5b205a0222cae115fd5a9b132b77999e7e2538193a2` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P22-獨立驗收-r3/worker-environment-freeze-manifest.json` | 142181 | `8b4c49ea0b3959a45908fc20429af94c9b0cefdc2c483b4486ebb8259bc3d41e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,server,db,files,liveLogsAndDatabaseExcludedBecauseOriginalServerRemainsRunning |
| `主管/P22-獨立驗收-r3/worker-ready-for-supervisor.json` | 27684 | `80fcb052c9a1c4fbce5965031a8106a5057024ddefbda55eca07fd417a959dac` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,goalStatus,revision,previousCorrectionReadySha256,supervisorOfflineCorrectionSha256 |
| `主管/P22-獨立驗收-r3/worker-result.json` | 1304 | `eb3d809828c5bd2c3845798bd9efe2fdf4a301a19f4cc08998562205908de95a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,goalStatus,revision,validation,priorR2Result,priorR1Result |
| `主管/P22-獨立驗收-r3/worker-source-freeze-manifest.json` | 400321 | `1ef56516598b119f3ba8007f8191cc5bcdb5b5ad888b592f430dbfd2d5da0715` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P22-獨立驗收-r4/clean-build-result.json` | 1773 | `e43f7b15910f34a209ed14be524d97a84b34bdf55e25bf13168df2e44401c21c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,exit_code,seconds,pid,outputs |
| `主管/P22-獨立驗收-r4/clean-build-start.json` | 441 | `9a524556d1d347ca92fd95f6d9852b6e178ee843ec57e7891affde86248f9f31` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,args,snapshot_sha256,ready_sha256 |
| `主管/P22-獨立驗收-r4/freeze-source-audit.json` | 528 | `bbf7364be8ec66937241e8480d7c56e324dac6c3a974cf0a819bb7085ab13e63` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P22-獨立驗收-r4/private-magic-ui-r4.json` | 354 | `b68aec06aec8f452a78d861972e9f9873319b39074d9dcf0e965933560dc90f0` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P22-獨立驗收-r4/snapshot.json` | 389992 | `87dfead8fae1f45e49ca0e39c79bc854339d33e70c16c700ac8bc5e71805e0d9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P22-獨立驗收-r4/worker-HANDOFF.md` | 10035 | `ce4d2a314ba106214a214ec4b3dcf7b0d642e14cc18b23a08a594e50d5104e77` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P22-獨立驗收-r4/worker-PROTOCOL.md` | 11577 | `8dd1d50e3c47b5aea51b41dc219fd39fbac8b80342c482c93437757d863f2fed` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P22-獨立驗收-r4/worker-environment-freeze-manifest.json` | 142181 | `faf0d9858b732c266a09b9554ec78bd7b11b9d09b4ad2036d9698eb151e83d08` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,server,db,files,liveLogsAndDatabaseExcludedBecauseOriginalServerRemainsRunning |
| `主管/P22-獨立驗收-r4/worker-ready-for-supervisor.json` | 27292 | `03d64dc9507d7771539f65006c4a60bcc80548cdf80ad190103327e0ae35a812` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,goalStatus,revision,previousCorrectionReadySha256,supervisorVisibilityCorrectionSha256 |
| `主管/P22-獨立驗收-r4/worker-result.json` | 2103 | `be117ddf22d629cf526efa47c0559dea05ba0c0b11eea30a11da00aae320a11f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,goalStatus,revision,validation,priorR3Result,priorR2Result |
| `主管/P22-獨立驗收-r4/worker-source-freeze-manifest.json` | 401206 | `731184092b48c794d022b9e20d087ea5dd4b7dd7f9768171caec26ca4a2d7aa6` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P22-獨立驗收-r5/clean-build-result.json` | 1773 | `464a9bb971ed0d80fb405605210bf8fd50f8a165e4ed4a74beec4a897c87822c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,exit_code,seconds,pid,outputs |
| `主管/P22-獨立驗收-r5/clean-build-start.json` | 441 | `f2440df49249296f974afbac836704ff1a7f446dfd4f644a1265a624a13a5bb6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,args,snapshot_sha256,ready_sha256 |
| `主管/P22-獨立驗收-r5/freeze-source-audit.json` | 528 | `0349cb4bfdd8c0cb9fe2d9cd320d566bf185fa62d4edf613685fde230503f235` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P22-獨立驗收-r5/private-magic-cli-r5.json` | 352 | `c1da329c4068d3ccffb6541a7d3afdc606357f4e2403b7f17ebb1b596b5c4639` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P22-獨立驗收-r5/private-magic-ui-r5-web-target.json` | 424 | `126cd4536ef754d703c094133c21eb9e13793369ff6af13157493bbce0e44281` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P22-獨立驗收-r5/private-magic-ui-r5.json` | 354 | `ecdf263362664531f9d1a96946bce0d31143451a747e3ee6fdbbe0dca2f69cb5` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P22-獨立驗收-r5/snapshot.json` | 390543 | `85c3277cbdf09f62e18040fd658887e39d19ae5200655b8d11f55abadbf649dc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P22-獨立驗收-r5/worker-HANDOFF.md` | 13319 | `9c4481d590d01d85f9d9d2075ca0a259fde34c820b4a271159337a0cc9dd5dfb` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P22-獨立驗收-r5/worker-PROTOCOL.md` | 12679 | `d8d5f34bd1d92fd8ad3d30de9477ae3c6059ce2aff2bcbe2f4f9ea8b2ec5a438` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P22-獨立驗收-r5/worker-environment-freeze-manifest.json` | 142181 | `2232fab204840ad8117287859256d88a784d04e2e4394b1a3d623da030eccb33` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,server,db,files,liveLogsAndDatabaseExcludedBecauseOriginalServerRemainsRunning |
| `主管/P22-獨立驗收-r5/worker-ready-for-supervisor.json` | 27399 | `60582c9bbc97f10269a00a33608112e84f0b62845f859e29dcc266f9e1182c82` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,goalStatus,revision,previousCorrectionReadySha256,supervisorStagingCorrectionSha256 |
| `主管/P22-獨立驗收-r5/worker-result.json` | 2143 | `495ba57ca54c99112a84aa04b1262bfbabba500b6ece42766e6d56463cf39f6e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,goalStatus,revision,validation,priorR4Result,priorR3Result |
| `主管/P22-獨立驗收-r5/worker-source-freeze-manifest.json` | 401749 | `bbbc76257380d274b629da3b6b2e6b964d3acfc6790949f8a83d5471458a7336` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P22-獨立驗收-r6/clean-build-result.json` | 1773 | `963600ab02fcb53bd558ee237a32c5a3d23c9a6c6930b172d88f57eed27b9350` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,exit_code,seconds,pid,outputs |
| `主管/P22-獨立驗收-r6/clean-build-start.json` | 441 | `777d56c6fc64809c8895742c42ceb4cc59cafae4ed5df04ec88cf32639694fbf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,args,snapshot_sha256,ready_sha256 |
| `主管/P22-獨立驗收-r6/freeze-source-audit.json` | 528 | `6663574f98b87934545cbd36aa52e606a001c0126bdee0dccc6cad49ab26c303` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P22-獨立驗收-r6/private-magic-cli-r6.json` | 352 | `097a7193123c0705db21a4b5c397d7bac253ff8a7b074907bc06995a4e432fce` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P22-獨立驗收-r6/private-magic-ui-r6.json` | 354 | `4245eb16a00cce12506293190a7ac6b03cc78206c3f67552aed51482be992417` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P22-獨立驗收-r6/snapshot.json` | 390718 | `7bb72d4c67916434baabf5c8bdfabf44bfe9e6d6be442d2d6759bdf85a4ae73d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P22-獨立驗收-r6/worker-HANDOFF.md` | 16607 | `93f2fe84ed9a90da79adb4c1e0cc87928a8a8be0dde09c4f9e43171c2600e278` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P22-獨立驗收-r6/worker-PROTOCOL.md` | 13946 | `dc03d6555a93481e618b04728275432c2decd0eb2e9cfabd5e36297dc75bab03` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P22-獨立驗收-r6/worker-environment-freeze-manifest.json` | 142179 | `0f60673a4eea708a372fc89a6eec3ad186cb6a41fa158e752ba03703632872d3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,server,db,files,liveLogsAndDatabaseExcludedBecauseOriginalServerRemainsRunning |
| `主管/P22-獨立驗收-r6/worker-ready-for-supervisor.json` | 27532 | `d538defe47bf5d49f21c4423b23a5fa52ae809aa914e7dc8dbb5c21cd6c7f47d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,goalStatus,revision,previousCorrectionReadySha256,supervisorTargetSelectionCorrectionSha256 |
| `主管/P22-獨立驗收-r6/worker-result.json` | 2340 | `2584cef2fc53ac36383c2cd1ce4c6222373808978b432bb9bcab77556f2f5565` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,goalStatus,revision,validation,priorR5Result,priorR4Result |
| `主管/P22-獨立驗收-r6/worker-source-freeze-manifest.json` | 401921 | `d7af4e1378e42e5f5b3a370aa675f171d478f1292e1aa7ff67b00829a1c9e729` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P22-獨立驗收/clean-build-result.json` | 1773 | `ef769cdbb399daff3502af04442c3b2b689c53a01266677fb73dfe9f8470a613` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,exit_code,seconds,pid,outputs |
| `主管/P22-獨立驗收/clean-build-start.json` | 438 | `42b09e84a4b0f4fa805903aab5000a9c446afa92bc6d1ca972bf674831f8bf0c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,args,snapshot_sha256,ready_sha256 |
| `主管/P22-獨立驗收/freeze-source-audit.json` | 528 | `5df208c632b33817fdaf742179609f959e99ab5423a2b56ba09481b248545035` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P22-獨立驗收/private-magic-cli.json` | 318 | `97b6a85d8551b31723327919fbca8a376bc965bb197322405bc74310d9984882` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P22-獨立驗收/private-magic-ui.json` | 320 | `0ff3823f357339dede8cfb11c681eef72adaa30925d7dbe95a9635e70d2569e4` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P22-獨立驗收/snapshot.json` | 360298 | `9196845060f68c8cafd75158f3ace19d779a504dfb488b1459378e6867772b6d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P22-獨立驗收/worker-HANDOFF.md` | 9208 | `c76fa2ba95affb0b3e52e0f0746be973aac4a32c50c02be9e520189e6ee48fef` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P22-獨立驗收/worker-PROTOCOL.md` | 7438 | `36595537ae086adeb727b33e1ef699377861a52ca341973f066cc0cbbe92a7af` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P22-獨立驗收/worker-environment-freeze-manifest.json` | 142181 | `dc78bf1c9e7d7afd284c1ac683edc5f75c8ef87fe650437869879283dd02ba06` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,server,db,files,liveLogsAndDatabaseExcludedBecauseOriginalServerRemainsRunning |
| `主管/P22-獨立驗收/worker-ready-for-supervisor.json` | 8214 | `17d24ca56aef6cda94cc83d69ae1156d446427602bcd66fc9962e7bcc2f93102` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,goalStatus,sourceManifestSha256,environmentManifestSha256,server |
| `主管/P22-獨立驗收/worker-result.json` | 8392 | `41326a5b55fbfe734841a00672107f565b56de0fd4dc8e6fdf5c9532d1fbeda6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,goalStatus,stage,normalJarStarts,worker,finalRegressionRuns |
| `主管/P22-獨立驗收/worker-source-freeze-manifest.json` | 371076 | `246fa8889af523d93ecdde3321f26409a6af4af6f6e172e80b595048de1112a3` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P22-純特效影格產品核對-v1/actual.json` | 5158 | `7859334b0c7dea939e41d4eb4c8e47fd0929d54828e8b4a4bb1a7c7b4ef7eab0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(96);keys=gfx,elapsed,key,empty |
| `主管/P22-純特效影格產品核對-v1/cases.json` | 10374 | `e28061488389dbcbd19e338b15776d870ba9035f6c63ffaa955b10a4aec3ad6c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(96);keys=gfx,action,elapsed,expected,expectedEmpty,total |
| `主管/P22-純特效影格產品核對-v1/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P22-純特效影格產品核對-v1/run.json` | 2099 | `fec76b7d0774d5a2027a48b0a2c1368f8f6a0e7a1aab62f7729789acd6092f09` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=args,exit,stdout,stderr |
| `主管/P22-耗魔回復原來源/manifest.json` | 2470 | `fcf19ca94d397c7a76341f0b7f85bce848a67b6816752e3fc630d63ca7778bcf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,jar_sha256,classes |
| `主管/P22-魔法算術產品比對-v1/actual.json` | 69414 | `0bcc93ddf89724b3544a8fc2285844851ba82e5242716834ba1ec8141fa3f05c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(81);keys=samples |
| `主管/P22-魔法算術產品比對-v1/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P23-UI續跑路線固定副本/live-route-plan.json` | 13452 | `714e924cc9353cdd24838841f1b1cd6e2b37c943271f86bb5ba21dbb3bd3519a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,initial,observerStationary,initialMutualVisibility,visibilityOracle,prelude |
| `主管/P23-UI續跑路線固定副本/live-route-resume-ui-r1.json` | 14676 | `045513877331273598655b739f920b2e4094f6b5cff324a7cf75777aa39e0fae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,initial,observerStationary,initialMutualVisibility,visibilityOracle,prelude |
| `主管/P23-freeze路線固定副本/live-route-plan.json` | 13893 | `d46583bebf5853ffa17a2061a86085f8a3e395f17ceb91c35e63ee98bbc545e6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,initial,observerStationary,initialMutualVisibility,visibilityOracle,prelude |
| `主管/P23-r2路線固定副本/live-route-plan.json` | 13593 | `49bfa4652195e3b3d5de1526dc724e8b464bc2f820309834882a08547aeecbcc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,initial,observerStationary,legs,totalMoves,maxMoves |
| `主管/P23-r2路線固定副本/live-route-resume-cli-r1.json` | 14409 | `12c150ecb6e15efa95d115abde3e2e9e4502c2026a87154a100ffba168a36d56` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,initial,observerStationary,legs,totalMoves,maxMoves |
| `主管/P23-r3路線固定副本/live-route-plan.json` | 12138 | `f66e0e62f14da366a354bfe06fcc3cc4a13c5bb125aa26669e1b961f0f213a2d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,initial,observerStationary,initialMutualVisibility,visibilityOracle,prelude |
| `主管/P23-r3路線固定副本/live-route-resume-cli-r2.json` | 13526 | `bd9f5cd843e015ea1c9d17b8768880496806b90a86c75dfe30d0205a67b0a923` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,initial,observerStationary,initialMutualVisibility,visibilityOracle,prelude |
| `主管/P23-獨立驗收/clean-build-result.json` | 1773 | `0bff14323875a737af5e0a5e4a536d1939fd1d822d483f6ae8781a15e52fa55f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,exit_code,seconds,pid,outputs |
| `主管/P23-獨立驗收/clean-build-start.json` | 441 | `7735e62442fcf37649aba99171b37b5b0ab48c85185c30f026b6eba51729297f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,args,snapshot_sha256,ready_sha256 |
| `主管/P23-獨立驗收/freeze-source-audit.json` | 2960 | `be296f150b634217c031356ce3f3e294383198581221be12890bc463c6ba683f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,sourceFiles,environmentFiles,snapshotFiles,sourceManifestSha256 |
| `主管/P23-獨立驗收/original-references-audit.json` | 528 | `1e4abef79d4b6aab621d83fb12440f6d845c9d63be8603119699038653c9f51b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,original_sources,mysql_runtime_files,inherited_sources,source_freeze_files,environment_files |
| `主管/P23-獨立驗收/private-opacity-supervisor-cli.json` | 484 | `54f0f18a78484c0d82252970f109cfd3cbdfae0d722ec5326595c7e4129c42e1` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P23-獨立驗收/private-opacity-supervisor-ui.json` | 486 | `0a79ad66841f61af30b115db6dce6ff0e9f521a6eac90a01030b27ce02428c95` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P23-獨立驗收/snapshot.json` | 443899 | `00be9512d14182a819e6c38ba54367b2ea18a123f4644d985b8715da6df97317` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,destination,inputs |
| `主管/P23-獨立驗收/worker-HANDOFF.md` | 4831 | `b848611ce30b2d3d0bb0381bf092155696603686c8bf45243747dc34cf80494a` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P23-獨立驗收/worker-PROTOCOL.md` | 4827 | `3de90c37eff5901d633aff8572edf6be2c3db042bdf89993befa0b33383f6507` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P23-獨立驗收/worker-delivery-manifest.json` | 1006113 | `972018ffc519a42ea70613a8861deb38846de91cff764056459fe18f4137c0cd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=phase,root,onlyExcluded,files |
| `主管/P23-獨立驗收/worker-environment-freeze-manifest.json` | 142191 | `46a519ff136d414e87901235b5d4ff52782dc0f51ff236ff22471f0f0101869e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,server,db,files,privateCredentialsOnlyHashed,liveDatabaseAndLogsRemainRunning |
| `主管/P23-獨立驗收/worker-goal-freeze.json` | 723 | `bf656d3c25542dbe0bc432208959e3f3c8f6e1e1aeec649d5599af0ab69a364c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=goal,remainingTokens,completionBudgetReport |
| `主管/P23-獨立驗收/worker-ready-for-supervisor.json` | 11020 | `409b57c098bf6adb79285b53410a115ff4ad774a70e7f24e97b9c4529b6638d1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,revision,goalStatus,goalReceiptSha256,sourceManifestSha256 |
| `主管/P23-獨立驗收/worker-result.json` | 6083 | `eb98cae9562f8b66f439ec9f4b5eb866ed2129ba6c945b10d6bf25b7292d9c68` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=stage,status,goal,source,fixedBinSha256,fixedJarSha256 |
| `主管/P23-獨立驗收/worker-source-freeze-manifest.json` | 441613 | `75b77fea3a2d92d0374d70374d6a5564ebcc912c1d506c38b133127ba08cff56` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,files,explicitNativeBinsIncluded |
| `主管/P24-NPC商店接線獨立審查-20261026/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P24-NPC商店接線獨立審查-20261026/processes.json` | 2230 | `4ea34b76ee71ab76125b3c8f444826b67e0c0d8b6e034c4ccb2071ade1fd7c9a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(4);keys=name,pid,args,exitCode,elapsed |
| `主管/P24-NPC商店接線獨立審查-20261026/result.json` | 2644 | `40fd96bddd890c1905767c482f3393eab470c6675a335f4bedcf25337c24b92b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,snapshotFiles,assetInputs,controllerChecks,domDoubleChecks,originalNpcPixelFrames |
| `主管/P24-Web登入接線獨立審查-20261026/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P24-Web登入接線獨立審查-20261026/processes.json` | 2164 | `35ad50cafee7145bd1c24175301980f36f0f1230a718d48037d447c10c029bd7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(4);keys=name,pid,args,exitCode,elapsed |
| `主管/P24-Web登入接線獨立審查-20261026/result.json` | 16390 | `9dc82269d287ae9531d304b20a2cc96f4a1d39236f00a899c848a92a84f0f35b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,verifiedDeliveryFiles,copiedInputs,controllerChecks,domDoubleChecks,workerDll |
| `主管/P24-continuation-r1-獨立準備審核/cli-prepare22.json` | 8345 | `64c50f595b14431421b2561cf3669af444f4cc961159dd32a43ad5e4353201c8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=initial,observerStationary,initialMutualVisibility,legs,selectLeg,kind |
| `主管/P24-continuation-r1-獨立準備審核/cli-round1.json` | 15629 | `61eb964fe16b688916f76a6e96408df8903630b369bcd66bebe1276c1046557f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=initial,observerStationary,initialMutualVisibility,legs,selectLeg,kind |
| `主管/P24-continuation-r1-獨立準備審核/cli-round2.json` | 16455 | `3b35f6c183f9539452a91b474a1f4214e1d86fcc21d561f39f1c578b8f380da3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=initial,observerStationary,initialMutualVisibility,legs,selectLeg,kind |
| `主管/P24-continuation-r1-獨立準備審核/decision.json` | 1337 | `1cd5157605d2bd33d13c76db4c4132e6713ae6701e11ec57bfcff43bbb780645` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,workerThread,preparation,checks,checksSha256 |
| `主管/P24-continuation-r1-獨立準備審核/future-raw-adapter-syntax.json` | 919 | `d9c367ad97c631ba060a387426a91732484c3760c8d5e67a5d881cbe6804ac2c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,originalRawAuditSha256,files,scope |
| `主管/P24-continuation-r1-獨立準備審核/independent-49-checks.json` | 28510 | `e2aceabd4ad5e735b8303e371610741a14cdaae57083ed7240f8db87d6078716` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,cases,rows,pureSideEffectGuard,liveStarted,uiOpened |
| `主管/P24-continuation-r1-獨立準備審核/preparation.json` | 141489 | `c7a3dd71e341820770fbb887cf502aafb4bec6552e878d17c0b7e9771e390a58` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,workerThread,productDllSha256,plans,savedBindings,pendingBefore |
| `主管/P24-continuation-r1-獨立準備審核/source-review-binding.json` | 1576 | `6605ca9ab430d8e6f2aa5b9d747c170affef5a6b63e5ad891db4ad03ae9b80dd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,scripts,workerReadySha256,preparationSha256,adaptation,testSourceSha256 |
| `主管/P24-continuation-r1-獨立準備審核/tests-start.json` | 548 | `89edc0925c9f97385553fa853ec6ea660f0fdb33ff5fdba01cfe8942b1065e65` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,args,utc |
| `主管/P24-continuation-r1-獨立準備審核/tests-terminal.json` | 84 | `8ceade2a58e68e09a3e04fb84eac3b59357c9b11abfc4609376fd68cef737795` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,exitCode,utc |
| `主管/P24-continuation-r1-獨立準備審核/ui-round1.json` | 15628 | `b54e39753514bc15e6d47b56c33b33c9a648ed18be224c5c1a91e5562e25bdb6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=initial,observerStationary,initialMutualVisibility,legs,selectLeg,kind |
| `主管/P24-continuation-r1-獨立準備審核/ui-round2.json` | 16454 | `72f14950c19447c7c09a74078e31b0187b47e31f2159349e43adfca23c06e48b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=initial,observerStationary,initialMutualVisibility,legs,selectLeg,kind |
| `主管/P24-continuation-r1-獨立準備審核/worker-ready-preparation.json` | 3809 | `0fcb8e1f3bc1595d3a28adf4341800ff7ab1b2404d71e292b57682af9a4feb64` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,workerThread,supervisorThread,goalStatus,goalReceipt |
| `主管/P24-extended-route-oracle/proposal.json` | 31045 | `b00eb0f4f64b6852af6fd003ea9373e04a5d8c50dcf92242aa5f26309d82ed3a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,initial,observer,previouslyApprovedObserver,rounds,limits |
| `主管/P24-login-correction-oracle/live-round1.json` | 15373 | `7ac7d06ef7ab46a300533696f62e819e4d0a516f4fa7f39c2466e67c7245907e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,round,initial,observerStationary,initialMutualVisibility,legs |
| `主管/P24-login-correction-oracle/live-round2.json` | 16200 | `74a51d77633d5bea1b1ef864b51a8f556130b044e5b420357f5c2617b94c83f6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,round,initial,observerStationary,initialMutualVisibility,legs |
| `主管/P24-login-correction-oracle/proposal.json` | 49385 | `07486ced3c5ddb6f47cefbd7acbe78292dc80fa7d7acfd33122774751745f190` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,reason,initialMain,stationaryObserver,originalSeedObserver,seedTerrain |
| `主管/P24-observer-recovery-oracle/observer-original-settled.json` | 11116 | `4a2aebf3fe855bb4e9dedfc85a6d40de520e655d1a09a8602c2a27200f1d0ed7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=time,position,groups,states,timers |
| `主管/P24-observer-recovery-oracle/proposal.json` | 35299 | `3790ea5a1d72e200d15a6c01e4dd02604cc6feb883382a4449751457cd0e1b45` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,failure,actualSavedState,roles,attempt,routeRound |
| `主管/P24-observer-recovery-oracle/return22.json` | 8264 | `d8e9a2e5d81966e7aff7bc149eb8bfa628223a29c0a5c1b702cc7c5af34724fb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,round,initial,observerStationary,initialMutualVisibility,legs |
| `主管/P24-owned-lifetime-review-20261026/build-start.json` | 532 | `4114e381aa8c03b47bfbb976fa8bb20ba872fea7f56c06a7f27da422e8f42db7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,args,utc |
| `主管/P24-owned-lifetime-review-20261026/build-terminal.json` | 84 | `c878591c5ee604d6c6a72f5669c48f0190ec0908589a857aa699c4a08f153448` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,exitCode,utc |
| `主管/P24-owned-lifetime-review-20261026/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P24-owned-lifetime-review-20261026/harness-adaptation.json` | 641 | `7a328638f5ea73e0017df203e46b1af946c0de0df51dd9810ddaddba243a526d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,sourceSha256,adaptedSha256,onlySourceChange,workerDll,supervisorDll |
| `主管/P24-owned-lifetime-review-20261026/input-copy.json` | 93022 | `f0047ed7fd61386c267de4d0f5aa804a3e173c297119f72a5f50ca28982e40c8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,source,inputs,scope |
| `主管/P24-owned-lifetime-review-20261026/replay-start.json` | 333 | `7bb0478b02893d6ecb59315229226feee09e0ddebd7116eebb98babea0a82d8c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,args,utc |
| `主管/P24-owned-lifetime-review-20261026/replay-terminal.json` | 84 | `e3837b83b8d279a1677a3be41554ac62bb1335d6c8c9ff57045a32bbb00d77e3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,exitCode,utc |
| `主管/P24-owned-lifetime-review-20261026/result.json` | 7305 | `2a2633592e53f8309db232d9fb0a468aa27d09fd581f0e78b5de329be1e48ea0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,productDllSha256,productAssembly,scope,fixtureSha256,cases |
| `主管/P24-owned-lifetime-review-20261026/review.json` | 585 | `4672a941e3ac95ec4da828cd1f8bebbe771b96ca480eb09f44c4e72d77c08f52` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,productDllSha256,cases,resultSha256,harnessSourceSha256 |
| `主管/P24-route-oracle/receipts.json` | 1647 | `0eb20e3d13f0c0c2b5dac34bf400b998852f6c658433101bb1c8ca39aed1fe60` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=args,exitCode,stdout,stderr |
| `主管/P24-scheduler-r2-clean-offline/clean-build-result.json` | 1833 | `312d8072dd4dd40f589890ee13172cbc1fb035a41dbf80ac2e32db0e94e09a90` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,pid,exitCode,seconds,scope,outputs |
| `主管/P24-scheduler-r2-clean-offline/clean-build-start.json` | 370 | `2a69e7733e845447d6f5c23a2f54110c86295283690422f83451bebd79cddf61` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,args,snapshotSha256 |
| `主管/P24-scheduler-r2-clean-offline/completed-offline-review.json` | 2641 | `842b4978e7649617fc51be1bad5b28e1a8063597706789c18f15600cafdd6110` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,dllSha256,workerDllSha256,evidence,scope |
| `主管/P24-scheduler-r2-clean-offline/native-scheduler-product-audit.json` | 719 | `d75e991de614bebf6710841279f55a2c4133cfa9f53c7270796fa567a4788a7a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,dllSha256,productResultSha256,originalVectorsSha256,cases |
| `主管/P24-scheduler-r2-clean-offline/original-cache-audit-first-failure.json` | 518 | `8d6dfb62192026dd711eecd749d63bc676dc3ca41bdc87077812e44cca9e7b6e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,failure,correction,productDllSha256 |
| `主管/P24-scheduler-r2-clean-offline/original-source-audit-first-failure.json` | 743 | `1654f0b7df71362f0328e0c703d299739e9ef10aef0a0c4b4c094e7866bcce54` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,audit,exitCode,at,manifestSha256 |
| `主管/P24-scheduler-r2-clean-offline/original-source-audit-second-failure.json` | 614 | `bcd8324a9c0ead61c335631b14ed42ff97dcb6374aecf6195f66506d30b621f6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,exitCode,cause,verification,change |
| `主管/P24-scheduler-r2-clean-offline/snapshot.json` | 553479 | `34e6761d03df45f00cdcb4c45af61272fedf6936b26d150d2610dbfe286eda81` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,utc,source,destination,inputs,excluded |
| `主管/P24-scheduler-r2-clean-offline/supplemental-source-manifest.json` | 34654 | `4e901b1ee7de135320a66205ab8e0f34f6f6113ca482205b168c4db5b864a6c0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,initialSnapshotSha256,workerReceiptSha256,originalSha256,added |
| `主管/P24-scheduler-r2-clean-offline/worker-source-current-check.json` | 236 | `949fbde5015c17d3be53f644f63700d7ec31d3eea9aa5a73f6e9f84c52d7f7b6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,changed,files,scope |
| `主管/P24-ui-r4-recovery-oracle/no-child-r3-retry-proposal.json` | 1655 | `b85e43d1480df3764d068916b467a8724c1f75b7a32b32717cb3e74266e2ca1e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,attempt,round,moves,Fresh,failureFolder |
| `主管/P24-ui-r4-recovery-oracle/r4-source-proposal-binding.json` | 797 | `ff27ea0f7388c4eed920bab2010f8ebb6796afe8ed172b51e10088d7cece1a31` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files,scope |
| `主管/P24-一般裝備接線獨立審查-20261026/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P24-一般裝備接線獨立審查-20261026/processes.json` | 9650 | `6efcf7113509f893d71b01a80422a8ed4dd04dd89836677c11e5db23d3fdb8c4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(17);keys=name,pid,args,exitCode,elapsed |
| `主管/P24-一般裝備接線獨立審查-20261026/protocol-source-review.json` | 460 | `d3e96e16737d6affbdaf3f3fe990b075f78bdc06ca2ed2c79bb5aa9d46b0b30c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,jarSha256,classes,types,scope |
| `主管/P24-一般裝備接線獨立審查-20261026/result.json` | 9551 | `3d03db77656f6975c363f00bfbc7e60877be5aaa6994110110f94329189d686c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,snapshotFiles,assetInputs,checks,cleanDll,renderedScenes |
| `主管/P24-主管安全交接-20261027/AGENTS.before.md` | 2506 | `e1bd07ba9d7e3386276ad97a4478b1a95f57490e1c61b40f89743a654f019464` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-主管安全交接-20261027/HANDOFF.md` | 11556 | `7e57ab4a91f363546395e9c74f57398d24004c1df477298d669ac2de62428a59` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-主管安全交接-20261027/analysis-process-observation.json` | 174 | `1aa0ea0068fafe607128af360f6cc54b9c6a0082754cc57e9902fade7b906bc5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=observedUtc,scope,count,processes |
| `主管/P24-主管安全交接-20261027/cutoff-and-goal-read.json` | 1290 | `2755c69500e5eed993d9244cea9c8dd3ca5ecc2a22dfa66dfd7cad46ca0dd820` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=recordedUtc,noNewDispatchSinceUtc,localCutoff,goalReadTool,goalRead,goalRecreated |
| `主管/P24-主管安全交接-20261027/final-worker-and-goal-snapshot.json` | 2215 | `b1a327d7341c970c9847ecf416880e90ba475555acdc359c78e16d8bd24e4019` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=recordedUtc,workerSnapshot,goalRead,goalRecreated |
| `主管/P24-主管安全交接-20261027/handoff.json` | 8876 | `1a3ff4c40385e62a65cfda825e7eb5a04c8890c3985ab1debf267f1f8842e260` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,recordedUtc,recordedLocal,oldSupervisor,parentSupervisor,successor |
| `主管/P24-主管安全交接-20261027/publication.json` | 1908 | `a6b48c7ac57f8256d72c52f7612a8b8b3c2e74fe56d79bf375551ec26cc0ed17` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,handoff,machineReceipt,summaryFiles,agents |
| `主管/P24-主管安全交接-20261027/service-observation.json` | 620 | `ba6184bf26a7b125f966101984c9d80d2f92deebd6132dd2ef93d4c0cae8d535` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=observedUtc,processes,listeners,action |
| `主管/P24-交易接線獨立審查-20261026/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P24-交易接線獨立審查-20261026/processes.json` | 3663 | `ba5a05c4723386d3ddd773aa32aea12ac7ee02cc3b37c94fcc82f6957e1dd0d8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(7);keys=name,pid,args,exitCode,elapsed |
| `主管/P24-交易接線獨立審查-20261026/result.json` | 3986 | `3255034264f0041bd0894fcc0058a0a7bb6b62cef8a2e2f99138679c97a4aaaa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,snapshotFiles,assetInputs,checks,workerDll,cleanDll |
| `主管/P24-加速攻擊施法接續-20261029/HANDOFF.md` | 1303 | `cdd1514fb13de61964b5a143288cb19c2f682864af935a7128e1efe46fb27a7c` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-加速攻擊施法接續-20261029/baseline.json` | 29672 | `1329700b9fabf59e7406d62790ea8a32758ee9771963d0dcd49c7cde85708a66` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,baseline,sourcePins,releaseSeal,productDllSha256,audioDllSha256 |
| `主管/P24-加速攻擊施法接續-20261029/candidate-r1-review-active-receipt.json` | 1363 | `40aabc8983d824f69f5be88e40e6b2cb45c8c5ce4552d58c9251a1995a64c88e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schemaVersion,cursor,revision,changed,thread,latestTurn |
| `主管/P24-加速攻擊施法接續-20261029/candidate-r1-review-dispatch.json` | 3014 | `f16eeee7f503959e09a591c8bc0d7293fde3ae9dcbee1e70908cc517b5d50700` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,target,prompt |
| `主管/P24-加速攻擊施法接續-20261029/candidate-r1-review-root-verification.json` | 455 | `6621f55192b197628c20f7f63132e694a17bb96f37319d1c5e760c5c3856f02f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=verified,artifacts,sources,checksPassed,acceptanceSha256,artifactManifestSha256 |
| `主管/P24-加速攻擊施法接續-20261029/candidate-r1-root-precheck.json` | 802 | `937200fbd13dce760f2b86ee7307e6290157eb54796c7c103fb8e86d21f77eaa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,candidateId,candidateSealSha256,sourceManifestSha256,resultSha256,sealedFilesVerified |
| `主管/P24-加速攻擊施法接續-20261029/completion-matrix-r31.json` | 604 | `1cf5aa749bc57a7fea57fcb2c0bf6f2716e586975d58d0dd67036fd2fbf5ea96` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=revision,offlineCandidate,independentReview,isolatedSameDll,canonicalRebuild,releaseFullHash |
| `主管/P24-加速攻擊施法接續-20261029/dispatch.json` | 16033 | `56dbce406ada5bc4a63494952ababd658832d9f921536804b732022fbd64674b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=baseline,revisionTarget,tasks |
| `主管/P24-加速攻擊施法接續-20261029/queue.json` | 3168 | `81923f984c069705af3cd3647d043d5bc550b778cb6f24562b273d8d3aace13f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,baseline,revisionTarget,candidateScope,goalActualStatus |
| `主管/P24-加速攻擊施法接續-20261029/r31-input-contract.json` | 1210 | `8fc2729c8149c5edca1ff1d5b69e1473977f3d6c503c1e8d86afe06bdb385ab3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,candidateId,candidateRoot,preparationSha256,baseRevision |
| `主管/P24-加速攻擊施法接續-20261029/r31-integration-acceptance.json` | 1964 | `dc76ef63ed547cd11f0b0c42b5e582b28a11f538cf26582253537b6271095602` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,revision,decision,candidateId,dispatchId |
| `主管/P24-加速攻擊施法接續-20261029/r31-isolated-active-receipt.json` | 1933 | `6b87ab6d3b3be037c9db41e7a4e9667a57be0baec85f5b7292a928215a221d60` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,raw |
| `主管/P24-加速攻擊施法接續-20261029/r31-isolated-dispatch.json` | 3495 | `1b0ef7aeb4f987a30cde9e202cff725558da7e1383eb3670e7e62eeb289ac2ed` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc |
| `主管/P24-加速攻擊施法接續-20261029/r31-preparation-root-verification.json` | 716 | `e73cb4275e05a8aa89da1906e9b180d47f27cfaeb31645ffa8253bd0bfd00b75` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,acceptedAs,preparationSha256,artifactSealSha256,resultSha256,artifactCounts |
| `主管/P24-加速攻擊施法接續-20261029/r31-root-entry-and-source-checks.json` | 2801 | `e39b504fbb557d3cc269dc4ef0438d07d6670e5d5e531ef58a623fbcdc95f017` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,verified,sourceFilesMatchedAcrossEngineAndRelease,integrationArtifactsVerified,integrationSealSha256,releaseSealSha256 |
| `主管/P24-加速攻擊施法接續-20261029/r31-root-full-release-verification.json` | 724 | `c9d54238176ff9f62eeb6953e81f55d97bab37730370098c04ffead61a19971a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,command,result,exitCode,rootEntryVerificationPending |
| `主管/P24-加速攻擊施法接續-20261029/r31-shared-integration-active-receipt.json` | 2455 | `35b88665c83fe90f4c0a61f044016430f60d550dcd6fc9d723214118aa9f55dd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,raw |
| `主管/P24-加速攻擊施法接續-20261029/r31-shared-integration-dispatch.json` | 4057 | `480833eba1c9acfd4462364177fad9d6c3fc5d6dba68953543905cc4e6d9248f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc |
| `主管/P24-加速攻擊施法接續-20261029/r31-stage-acceptance.json` | 1518 | `409493366d14e02d7138e18ccf529867034aa67337819b305ebaa99a27c7f166` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,decision,stageRoot,stageSealSha256 |
| `主管/P24-加速攻擊施法接續-20261029/r31-supervisor-acceptance.json` | 2204 | `c61b78bc32afb590675f9a73e1c7e5f171156af2afa9e8d58c4ff03b55c25d1d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,utc,revision,decision,releaseRoot |
| `主管/P24-加速攻擊施法接續-20261029/review-preparation-root-verification.json` | 486 | `4134fc01f6cda2be366e27a4c11a437be887d526fa22cc7e208a1c0c84feac6c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=verified,artifacts,sources,resultSha256,artifactManifestSha256,checks |
| `主管/P24-加速攻擊施法接續-20261029/three-workers-active-receipt.json` | 2973 | `bc28e9132ca7968935332d0c94f422ac5897b7d775abaf401180376c584a237c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=observedUtc,sends,polls |
| `主管/P24-地圖特效接線獨立審查-20261026/fixed-processes.json` | 2409 | `43e2313ddfec9b2394af593a62fc1fd880bf8452c3f92d35960d160fd902ab81` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(7);keys=name,pid,args,exitCode,seconds |
| `主管/P24-地圖特效接線獨立審查-20261026/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P24-地圖特效接線獨立審查-20261026/pre-fix-integrity.json` | 2254169 | `643fb3d6a844f77bfe2375717e733096123d89a145acf1215c6234ccc09dbfdf` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P24-地圖特效接線獨立審查-20261026/processes.json` | 4712 | `45b6ff1b6f9e17cc34f1642ed619b05d20990da9e638c09215eeca9e610b4c20` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(6);keys=name,pid,args,exitCode,elapsed |
| `主管/P24-地圖特效接線獨立審查-20261026/result-fixed.json` | 4839 | `c2a3de0b1be0df418254757dfa4743ace50c8dc1ed21c581a3f6edae68136d95` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,originalFailure,fixDeliverySha256,assetInputs,cleanDll,checks |
| `主管/P24-地圖特效接線獨立審查-20261026/result.json` | 639 | `bd3b4ee0b7b593b0643005cbadf1b96374789b38cbec93112488b58a42a8d7f3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,asset,integrityInputsVerified,snapshotFilesVerified,builds,failedCheck |
| `主管/P24-完整地圖來源清冊-20261026/dungeon-routes.json` | 435348 | `c756ef54576fc1b17515257075b372947ed7799f7fd3a88355e9faa0b2596056` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,source,sourceSha256,columns,count,rows |
| `主管/P24-完整地圖來源清冊-20261026/effect-source-candidates-all-archives.json` | 5076307 | `fd9920be2b4e33f93b935a4cd78fc88de660220de1ea1724defee9addaed3337` | U — U14：可重構素材／封包欄位 $.rows[].matches[].entry.record_hex；真實或合成來源 unknown |
| `主管/P24-完整地圖來源清冊-20261026/effect-source-candidates.json` | 4869039 | `f9ce4addea1aad116a7cddde3fc235df01b15982c31724c0976d0c4ec19a78ac` | U — U14：可重構素材／封包欄位 $.rows[].entry.record_hex；真實或合成來源 unknown |
| `主管/P24-完整地圖來源清冊-20261026/map-metadata.json` | 232879 | `1282d74c2977635106f32bdbcacec48f309569b1a77be2d4a203d5086738b8f1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,source,sourceSha256,columns,maps,count |
| `主管/P24-完整地圖來源清冊-20261026/parser-limit-classification.json` | 185614 | `fc365448ec5310a6af070e4f1db014ddcffb06b58395f010e0ca944f3dbee157` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,parserSha256,count,summary,rows |
| `主管/P24-完整地圖來源清冊-20261026/seg-core-comparison.json` | 446317 | `5753fd612a64b18e070be3acabda4d16ffff0ddb1a991798907396a0cd32011e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,count,strictCoreRead,paired,identicalGroundPairs,withCovers |
| `主管/P24-完整地圖來源清冊-20261026/static-content-prerequisites.json` | 1059403 | `0e469252249674824ae8b66224915267263baf3355263b3918db59af3e046c22` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,inputs,counts,rows |
| `主管/P24-完整地圖來源清冊-20261026/summary.json` | 202063 | `1379173bd3c05459f67083461e7e082ebd38aaad39bf55e1b7fab056a121b6e1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,clientMapRoot,terrainRoot,decoder,decoderSha256,totalFiles |
| `主管/P24-引擎開發恢復-20261029/HANDOFF.md` | 3337 | `6f6544babfaa3f967ea99b8fc93cc6f886a0bd359caafb85496020d552a460ab` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-引擎開發恢復-20261029/baseline-source-pins.json` | 19962 | `8b3a5ed3ed8aa355477c48c58e7a3cff2f3d066eacfc2c15c35b8e1c519433bb` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P24-引擎開發恢復-20261029/candidate-review-dispatch.json` | 2676 | `54bf9526cdbcdb62fc09264de7d354cc9be81cbe741c8d1861b63a24a46a56f3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,prompt,result |
| `主管/P24-引擎開發恢復-20261029/candidate-root-identity-review.json` | 461 | `12bdb8ae8a92f1f003c0d1c376589ebac9a95e2354373724c9bdc83dc0a3ea86` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files,changedFiles,candidateSealSha256,candidateResultSha256,candidateDllSha256 |
| `主管/P24-引擎開發恢復-20261029/candidate-root-source-review.json` | 774 | `72090415a232ad835d3e8935478c01cc2b97ca503af176f042f6eb0252953961` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,baseFilesVerified,candidateSourceFilesVerified,changedFiles,unexpectedChanges,audioDllSha256 |
| `主管/P24-引擎開發恢復-20261029/completion-matrix-r29.json` | 3212 | `dc9da0f557529c19c704ff406256f0ee33e7d1b5335352faee059636a1c82307` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,updatedUtc,revision,wholeP24Complete,goalToolStatus,historicalMatrix |
| `主管/P24-引擎開發恢復-20261029/dispatch.json` | 7922 | `6e93930037977bf849950bfd0d16aeed9f71a6ca30647d03455c745231180576` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,userDirection,goalStatus,baseline,tasks |
| `主管/P24-引擎開發恢復-20261029/f1-repair-active-receipt.json` | 1316 | `2f0609a9813caae8373283dd0d67a2530ad0616dc545299301653effd9179f47` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=content,isError |
| `主管/P24-引擎開發恢復-20261029/f1-repair-dispatch.json` | 2527 | `2f07decab4ca950f4c430157a8ec7105cd0723590073493d20b223056c655fd2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,prompt,result |
| `主管/P24-引擎開發恢復-20261029/integrator-active-receipt.json` | 1310 | `263ee26f2e1507bd5ccfdf91c2667cb681e1db4742b9d668eb3fbc313e706d65` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=content,isError |
| `主管/P24-引擎開發恢復-20261029/queue.json` | 4172 | `254fc236a81190ec58a0a1a04df27c3f6b0c87e4b8b201e3267d9b085902d91f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,goalActualStatus,databaseScope,smokeGate,tasks |
| `主管/P24-引擎開發恢復-20261029/r2-review-active-receipt.json` | 1311 | `ccc156d46b1cea48ae8b5c6b185307b930620b969da78ef78aaaa94db964ba19` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=content,isError |
| `主管/P24-引擎開發恢復-20261029/r2-review-dispatch.json` | 2372 | `031c292b3b582c73858ed0dfc69312aa456657f7550099940902d8be70769b65` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=prompt,result |
| `主管/P24-引擎開發恢復-20261029/r2-root-identity.json` | 424 | `a6b1d057899ba177ffb6e01db03dd7e215c4435510870eb1fb84ba220a24cd6d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files,candidateSealSha256,resultSha256,sourceManifestSha256,productDllSha256 |
| `主管/P24-引擎開發恢復-20261029/r29-input-contract.json` | 945 | `431645c82ff2a1a6d21d68f68a6256d66e383286f21607a1a05de2e08f3880b9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,candidateId,candidateRoot,preparationSha256,sourceManifest |
| `主管/P24-引擎開發恢復-20261029/r29-integration-acceptance.json` | 1391 | `ff60d3d1a86af64a8dcea6542f17a79370e002ac3f93477025684a89c245b361` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,revision,decision,candidateId,dispatchId |
| `主管/P24-引擎開發恢復-20261029/r29-integration-active-receipt.json` | 1358 | `95fb85da95b4c3ae705b35abe0f1d4de71aa05a078def8aa8b5fc210d2e0cac3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=content,isError |
| `主管/P24-引擎開發恢復-20261029/r29-integration-completed-receipt.json` | 1711 | `57e0487c52507448b54ee6de161b759569256b1d3c7185363ea0996d67adee26` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=content,isError |
| `主管/P24-引擎開發恢復-20261029/r29-integration-dispatch.json` | 5098 | `4ef85518e9bbb1dda1f8682a538fbb81d00fb9fed61dc4f780b973832cf969c0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,prompt,result |
| `主管/P24-引擎開發恢復-20261029/r29-root-delivery-verification.json` | 699 | `bae0bda81035a029ed1a65b7e68aa07a166e7dd175dcdafbfbf913f2bdb51bc4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,releaseSeal,releaseSealSha256,sealedFilesVerified,sourceFilesVerifiedInEngineAndRelease |
| `主管/P24-引擎開發恢復-20261029/r29-root-entry-check-only.json` | 1285 | `ca8fc68e89eaa04db66466782cafc16a05e28f71d41d262d1b5fd964295b4aab` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,revision,executable,workingDirectory,arguments,manifest |
| `主管/P24-引擎開發恢復-20261029/r29-stage-build-root-review.json` | 691 | `9f9e82c51234bf451ab07c6a98244edaf31086957bdf49ca411ebb5234d5313b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,resultPath,resultSha256,productDllSha256,manifestSha256,launcherDllSha256 |
| `主管/P24-引擎開發恢復-20261029/r29-supervisor-acceptance.json` | 3693 | `9699505a77ead7358ad17629414fdb05a27125f8a0eae555eab7bbf65e9da182` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,decision,supervisor,scope,revision |
| `主管/P24-引擎開發恢復-20261029/root-r1-rejection.json` | 1517 | `eb78035682b7323f3cb9232afcfd296b3561fb9f51e196e0f757964cf1787dbf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=decision,candidateId,candidateSealSha256,finding,independentReview,rootSourceInspection |
| `主管/P24-引擎開發恢復-20261029/send-and-active-receipts.json` | 5695 | `7d22d595d0e32104fc51624b7472be37ba6d1c1f5d50269356ff5c4535cbdc58` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=tasks,activeObservation,integrator |
| `主管/P24-技能倉庫接線獨立審查-20261026/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P24-技能倉庫接線獨立審查-20261026/processes.json` | 2312 | `7531af2507081729bb7ab68c89eb05c54e4e61070d8a0bfbeaf7c3d898c65637` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(4);keys=name,pid,args,exitCode,elapsed |
| `主管/P24-技能倉庫接線獨立審查-20261026/result.json` | 2690 | `f8730b9392a3d34e6f74337b330b088099e2ff8c990a6e877cc14813b3d57baa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,snapshotFiles,assetInputs,controllerChecks,domDoubleChecks,originalNpcPixelFrames |
| `主管/P24-技能施法接線獨立審查-20261026-r2/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P24-技能施法接線獨立審查-20261026-r2/processes.json` | 8482 | `df693bbabef8eeea4cb7f38f3758c9986b0239f976ee993ffa5885128a7c004f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(15);keys=name,pid,args,exitCode,elapsed |
| `主管/P24-技能施法接線獨立審查-20261026-r2/result.json` | 9008 | `d70f2e3ccf6a7f48f2dca7f23f1ec87747814894ae99a6fde555adc4b2b76503` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,snapshotFiles,assetInputs,checks,pixelFrames,cleanDll |
| `主管/P24-技能施法接線獨立審查-20261026/preparation-failure.json` | 370 | `9b83b936f944b2075ec6c16960c605434f5c05bcce3bdd15f1a389f574b82ffe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,reason,next |
| `主管/P24-拾取外觀擴展規劃-20261029/active-receipt.json` | 600 | `2bf20e2e308bd3d1ffac0ce4d9ae8ea7a3a03763d74e6d1db70cf48bc2ec0bec` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,taskId,owner,turnId,cursor,status |
| `主管/P24-拾取外觀擴展規劃-20261029/independent-review-active-receipt.json` | 436 | `bd1767d87c45e569c0f0304653c522561262b4a8c58215a81922a6bd3a9cc4bd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,taskId,owner,turnId,cursor,status |
| `主管/P24-拾取外觀擴展規劃-20261029/independent-review-dispatch.json` | 2445 | `437c1118ee553a43fc87a01972adca70f18003d942aba87578a8204735df969b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,prompt |
| `主管/P24-拾取外觀擴展規劃-20261029/independent-review-root-acceptance.json` | 1228 | `b9f5bd1b76aaadd0a25889a5034f1bcf7cde5038896f39d7ce826c21d3cc09be` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,sealSha256,resultSha256,verifierActuallyExecuted,artifacts |
| `主管/P24-拾取外觀擴展規劃-20261029/next-increment-qualified-scope.json` | 12548 | `1177d6dfbd59f196dd34deee8e6433635f6386c9c890ed1e51e3f99ab20f547c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=id,utc,status,implementationAuthorized,finalR34BaselinePending,scope |
| `主管/P24-拾取外觀擴展規劃-20261029/queue.json` | 3537 | `c1d56e3e01f9b15216f91baf183c8184fb2dd23ffd7ba6789e52fdd9bff006ec` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,taskId,owner,turnId,cursor,status |
| `主管/P24-拾取外觀擴展規劃-20261029/review-prep-active-receipt.json` | 619 | `43988c7b2775569f369540bc27357d4eb8b7f43c139cb351ef6f7a30908288ae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,taskId,owner,turnId,cursor,status |
| `主管/P24-拾取外觀擴展規劃-20261029/root-fixture-visual-inspection.json` | 13642 | `4166ce666e8dc8561c4e5b32263d134c4d99144843f14841bc07124ab6ff1755` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,viewedSheets,displayedBodyFrames,findings,observations |
| `主管/P24-拾取外觀擴展規劃-20261029/root-verification.json` | 961 | `83a64a55e8248bfd0bfed0e792b63b8fbefe607d93e36a0423b4baac7bfa7e1e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,sealSha256,resultSha256,verifierActuallyExecuted,artifacts |
| `主管/P24-拾取外觀擴展規劃-20261029/source-plan-dispatch.json` | 2313 | `26d21fc2a76c3bc37ecdde240eb089fcae7cb6aacf94325a2bf7bd960beace2f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,taskId,owner,prompt |
| `主管/P24-拾取排程來源-20261029/active-receipt.json` | 429 | `a31c86329b6899d58c2588ab5bfc37c045a47cb7eae473b28d8d96065daf836b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-拾取排程來源-20261029/dispatch.json` | 4034 | `83f7fe9f680a34319bee90a2cd83e6b4ed91807c44886d52aafd019a250c0680` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc,implementationAuthorized |
| `主管/P24-拾取排程來源-20261029/eligibility-active-receipt.json` | 404 | `404c4dfbfeadca31a253ed3c781b2f328f4635832149bd28ca78912496d5fcc8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-拾取排程來源-20261029/eligibility-dispatch.json` | 1855 | `f901e2d65dff87005c922daec59bf24306c1dc50f06848cf430eb7cbab8ddfc6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,prompt |
| `主管/P24-拾取排程來源-20261029/independent-review-root-verification.json` | 860 | `f25e0bc284227769b093d7ae13944523d7fbb3db535ea1fbe25306c7b1035380` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,finalSealSha256,finalResultSha256,correctionArtifacts,preservedOriginalArtifacts |
| `主管/P24-拾取排程來源-20261029/queue.json` | 1545 | `9bfe3dda0398a1239160e7131307f745df5ab3303cccc933f976bbf102db771a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-拾取排程來源-20261029/source-r1-root-verification.json` | 658 | `943c337c4a4688bda31a8d6ef837efddc2997757933bc5c585e5ab68cde20cbc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,sourcePins,nativeChunks,proofClaims,proofRanges |
| `主管/P24-拾取生命週期修復-20261029/baseline-final.json` | 29654 | `0c645bf5c3a515fd5da88cf28c834a4b2967aed268946d45fb5a6ebe9f03fcb7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,baseline,releaseRoot,releaseSeal,releaseSealSha256 |
| `主管/P24-拾取生命週期修復-20261029/baseline.json` | 57611 | `015da2542257d3c124346dcdeb3f34528902b56e1913b5878f3bc995597093ff` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,baseline,phase,stageRoot,stageSealSha256 |
| `主管/P24-拾取生命週期修復-20261029/candidate-r1-review-active-receipt.json` | 511 | `f1a5b7338921422c844438d89cc3e3c54b9201898276e345ced7c212fc1bb3bc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-拾取生命週期修復-20261029/candidate-r1-review-dispatch.json` | 5130 | `e4b6f1a391a59599a3e0f595a638c5efa939299de7b8f9bc94f8abe4f4bd2d5f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc |
| `主管/P24-拾取生命週期修復-20261029/candidate-r1-review-root-verification.json` | 637 | `4b6fa2e1425832868a93f95efea8fd786d613566d052504ee6c32dcc57ee2ef3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,decision,artifactSealSha256,reviewAcceptanceSha256,artifactFiles,externalPins |
| `主管/P24-拾取生命週期修復-20261029/candidate-r1-root-precheck.json` | 867 | `77370bbcac4beb9afc5d8ea09756822d43603aaf7e941c648da66f9d5ec69701` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,decision,candidateSealSha256,sourceManifestSha256,resultSha256,artifactFiles |
| `主管/P24-拾取生命週期修復-20261029/canonical-ground-layout-review.json` | 1212 | `70b28642fff6ff892297d1e274f15a52774da77f6112340a499202b443a43980` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,failedCommand,observedExitCode,exception,rootCause |
| `主管/P24-拾取生命週期修復-20261029/final-base-binding.json` | 665 | `26cc55267f04896344333a6eda50d058d432aa4df1d2655875b6e9772eedff5e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,baseline,originalBaselineSha256,finalBaselineSha256,stageSealSha256 |
| `主管/P24-拾取生命週期修復-20261029/implementation-active-receipt.json` | 423 | `108c0d2be91e19e85fd2f741e7aab3bfd932ce5f823ba29a475d63da622595c1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-拾取生命週期修復-20261029/implementation-dispatch.json` | 4907 | `76af08c3ab6b985ac605c60767cd56f55514447640a2ae6ae4f401fc375047f8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc |
| `主管/P24-拾取生命週期修復-20261029/integration-prep-active-receipt.json` | 405 | `8c59c379a255031368feaa1e8ff541d6a012ede8a1fe351b70518ff0a842ad20` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-拾取生命週期修復-20261029/integration-prep-dispatch.json` | 4454 | `7371f50b0d297d246a27521189a419637575850d5e5c4b821bb9f0bb7daeddb4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc |
| `主管/P24-拾取生命週期修復-20261029/queue.json` | 5511 | `7239b1cd5512f17ad53269538bf83834993fae51c13b852a24d9f11cb95269bf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,targetRevision,baseline,baselineSha256,finalReleaseBindingPending,implementationAuthorized |
| `主管/P24-拾取生命週期修復-20261029/r33-formal-integration-active-receipt.json` | 438 | `d211383931289ad7c25e779af1957fc22e8e8fa5f0f114f91f22ebb0f8770796` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-拾取生命週期修復-20261029/r33-formal-integration-dispatch.json` | 3223 | `64b8394b27471bff3db1a5c89d53f096f61b5a2508d5007533b0bfeb8c48cb74` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,prompt |
| `主管/P24-拾取生命週期修復-20261029/r33-input-contract.initial-nested-review.json` | 1228 | `f02b2ecdbda100c8b3cd94182d7aa6e0ef950f042176e9be0df1104f944faa47` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,candidateId,candidateRoot,preparationSha256,baseRevision |
| `主管/P24-拾取生命週期修復-20261029/r33-input-contract.json` | 1251 | `048770f99ca923ab05227dbd8fc3049f1aeddf70260f3423c553aa0427744c50` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,candidateId,candidateRoot,preparationSha256,baseRevision |
| `主管/P24-拾取生命週期修復-20261029/r33-integration-acceptance.initial-nested-review.json` | 1996 | `7a8255bd8a4dfc3e9e9e3e10d7916918f36d1961e8548285b9612b909a5c96cb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,revision,decision,candidateId,dispatchId |
| `主管/P24-拾取生命週期修復-20261029/r33-integration-acceptance.json` | 2307 | `3676137771224079b0a3a26abf094165c34e2b8b68e203da8089f2f877fc4a28` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,revision,decision,candidateId,dispatchId |
| `主管/P24-拾取生命週期修復-20261029/r33-preparation-root-verification.json` | 854 | `3ce6dafffc62ce607c280bdf8b40637185d6af2d5cd6b5f8e282109068582b11` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,decision,preparationSealSha256,artifactSealSha256,resultSha256,baselineLockSha256 |
| `主管/P24-拾取生命週期修復-20261029/r33-readonly-gate-accepted.json` | 331 | `92ca5068d6cd9f14836da75edc0c5382b72203c3b537c04a82e8f74d2785bc48` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,sourceFiles,writes,exitCode,contractSha256 |
| `主管/P24-拾取生命週期修復-20261029/r33-readonly-gate-initial-refusal.json` | 613 | `a30ae1c5dcbde5a7bec89bbc32760cc50032e37d0c243bf387c6186b600f1210` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,reason,cause,contractSha256,receiptSha256,buildFlag |
| `主管/P24-拾取生命週期修復-20261029/r33-root-entry-and-canonical-checks.json` | 3762 | `b123b3cf73ca8fcc4780acb35c78a97d86cfc743b40dd0ce80f477238d7f8b4d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,verified,integrationArtifactsVerified,integrationSealSha256,oldReleaseNonAssetAndIndexPinsVerified,exactSourceCopiesVerif |
| `主管/P24-拾取生命週期修復-20261029/r33-root-full-release-verification.json` | 570 | `b77544fe507af07670a51ecc69aa914d45976b20eb5607f948c9969b99498552` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,verified,result,releaseWrites |
| `主管/P24-拾取生命週期修復-20261029/r33-root-stage-verification.json` | 1937 | `50255499f8eb90dda28bea7caed50c7b8ae24e4c68c13c598d6bb8e5178738a6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,stageRoot,stageSealSha256,stageResultSha256 |
| `主管/P24-拾取生命週期修復-20261029/r33-shared-integration-active-receipt.json` | 583 | `ce67e919b197b178841610781445de76c740230b88abcd4dcc2fdeeffdbfd8ed` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-拾取生命週期修復-20261029/r33-shared-integration-dispatch.json` | 3137 | `e18f7b63d6fc5f45d7f1b78b359a8e042b4d2312dc30373fbe93a04790920393` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,prompt |
| `主管/P24-拾取生命週期修復-20261029/r33-stage-acceptance.json` | 2478 | `613757a12150148df5b22b88faeef4ab8a1fc5e837720908aa799e3d52483671` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,stageRoot,stageSealSha256,stageResultSha256 |
| `主管/P24-拾取生命週期修復-20261029/r33-supervisor-acceptance.json` | 4200 | `09b35736898f5a5c269ddcaab7ecd375ce7f6c0f5b04bcc22776d331905d176a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,decision,supervisor,revision,scope |
| `主管/P24-拾取生命週期修復-20261029/review-binding-active-receipt.json` | 472 | `a3782374734b7159a8e82cf656bc782ef2512ae969b790cd2b0e823d74ba068c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-拾取生命週期修復-20261029/review-binding-dispatch.json` | 2491 | `868f9867c1f7cecb4219487afaba3f2db01aebb1ce9875f5c6c9670e49d9b9ea` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc |
| `主管/P24-拾取生命週期修復-20261029/review-binding-root-verification.json` | 526 | `6c79fa506026fd82412556c9bc7236fbaed2df43fcdd2f99fa3540cc08a56b2e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,accepted,equivalentProjectionOnly,artifacts,externalPins,reviewAcceptanceSha256 |
| `主管/P24-拾取生命週期修復-20261029/review-prep-active-receipt.json` | 397 | `52a6fbd61308d7f51daa4b02d4f71cc9c8c9e7fb8e529540777bb3764aa09bd7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-拾取生命週期修復-20261029/review-prep-dispatch.json` | 4174 | `c742d8b7613798a6e1f93797c52130b02ac1d026b0fdbd00224cee9f6cca89fa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc |
| `主管/P24-拾取生命週期修復-20261029/review-preparation-root-verification.json` | 707 | `6d6816715c342941ddcaf57c07a559f41c427381a8de296ae3b096e17dd45d93` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,decision,artifactSealSha256,resultSha256,artifactFiles,externalPins |
| `主管/P24-拾取素材資格-20261029/active-receipt.json` | 431 | `a0361b3d134e2c765a3832b693e17cd0f9f1e0431fffa49881da4b935aa3176f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-拾取素材資格-20261029/dispatch.json` | 3846 | `5e896557134913dff1a8c8fe7e84a57ecf4579f885ef2e7c812445f038dcacd1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc,implementationAuthorized,isolatedTargetedAssetDecodeAuthorized |
| `主管/P24-拾取素材資格-20261029/independent-review-active-receipt.json` | 381 | `a922203fa60d4df623961d2f1aee634223311782394f9cf4d10e53820068ad14` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-拾取素材資格-20261029/independent-review-dispatch.json` | 2459 | `86d7fe236084e2fb86c390895cdc93343dcdcdc1246bcd245ad64183474785f7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,prompt |
| `主管/P24-拾取素材資格-20261029/independent-review-root-verification.json` | 2728 | `7f7cc89889f5ab0eb5d2a2cefb688010988d8ec03a8c6b540f639305e3009b47` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,sealSha256,resultSha256,artifactsVerified,externalPinsVerified |
| `主管/P24-拾取素材資格-20261029/queue.json` | 992 | `3a6b464a7560bd6ae065796e94cbe54a1351c37c6102e5a38c5cc8c640940978` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-拾取素材資格-20261029/source-r1-root-verification.json` | 1099 | `aa49eb1f3d9e78806df26df4d8c1e497d1dfe75727141ca1e7120b436cfd580f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,decision,artifactSealSha256,resultSha256,sourceContractSha256,artifactFiles |
| `主管/P24-持續開發與同版驗收-20261027/AGENTS-before-division.md` | 1974 | `4b88d7ac11e1ec1160657bb1b9c46fdf004efdbfaad618cd634769070dfb873d` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-持續開發與同版驗收-20261027/appearance-review-and-door-dispatch.json` | 1043 | `213d888a1278f4e81d96f4fc2b2d6c2dc01cc28731968edf50a2278c181cc003` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,decision,review,visualReview,reviewFailurePreserved,nextTask |
| `主管/P24-持續開發與同版驗收-20261027/autonomous-gameplay-round-result.json` | 2304 | `e67cd088e845a8c6345097e837f38483d2211b436f2afe018a5fd9c6d5e33d3f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,latestDelivery,normalLauncher,r22Result,r21GameplayLedger |
| `主管/P24-持續開發與同版驗收-20261027/bonus-stats-live-db-baseline.json` | 6850 | `d0ac955b8e38f54d01adb42186327c921f0b12a56e68b7a0f4b3c2ea8d50ba93` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-持續開發與同版驗收-20261027/chat-keyboard-flow-root-review.json` | 2535 | `a28edd309f19aa01132947c87df02da720eadc253b753a7b19ccb9732daf072e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,source,review,sealSha256,rebuiltDllSha256,checks |
| `主管/P24-持續開發與同版驗收-20261027/continuation-decision.json` | 893 | `cbed5e5096beb9d64c1c50f2e0da7985755bf27651f72363075a379e7de76069` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,workExecution,actualGoalToolStatus,goalReceipt,stopReason,next |
| `主管/P24-持續開發與同版驗收-20261027/division-authorization-evidence.json` | 2448 | `d4ab6de30d118b197b741a7b57aea15bb9687ab9585e490803ffcef00db6dd39` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=source,sourceThread,originThread,originDirectRead,authorizationTranscript,scope |
| `主管/P24-持續開發與同版驗收-20261027/final-flow-current-plan.md` | 6238 | `41ece836663bec4d5b268192cb92a3d1aa866bea51c563148793d7118ff02943` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-持續開發與同版驗收-20261027/focus-observability-root-review.json` | 1518 | `3f6c60da77e159622693a387fd55d70517ef08f1ffe6172a5c4445a6f221c92c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,source,review,sealSha256,rebuiltDllSha256,checks |
| `主管/P24-持續開發與同版驗收-20261027/keyboard-navigation-root-review.json` | 5700 | `5d965ebbc72a365ed335048afc2adec9d7b41c8b8807de270efe138feba81dab` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,source,review,sealSha256,rebuiltDllSha256,checks |
| `主管/P24-持續開發與同版驗收-20261027/latest-reviewed-product.json` | 501 | `cd61b11133b29ec53d81a8fe16dd1e3c2d9704db155a2d1471cce7e0d49966ba` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,version,launcher,review,live,previousUserLauncher |
| `主管/P24-持續開發與同版驗收-20261027/parallel-start-receipts.json` | 4615 | `f3af1a27a0aa91a230c2d993ee9a9675cc6f52064ba3ec2514b3a64070b4b11c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=appearanceCreation,acceptancePreparationCreation,observedProgress,notes |
| `主管/P24-持續開發與同版驗收-20261027/parallel-task-boundaries-before-r18-review.json` | 4897 | `0906d2932c3123ee61d24c8efd70dcead24573b0549eecc2d8d867445d03d71d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=baseline,dllSha256,manifestSha256,tasks,normalIntegration,liveUiOwner |
| `主管/P24-持續開發與同版驗收-20261027/parallel-task-boundaries-before-r18.json` | 5025 | `93d3767159c0bd7393adad4376a5af690179835f23da52fb3d368dda566b56ce` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=baseline,dllSha256,manifestSha256,tasks,normalIntegration,liveUiOwner |
| `主管/P24-持續開發與同版驗收-20261027/parallel-task-boundaries-before-skill-refusal.json` | 5043 | `91f3c2a4f6f6dbf43e219bf2bd3330205576b0b2db323e464b5362992d1d7e0e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=baseline,dllSha256,manifestSha256,tasks,normalIntegration,liveUiOwner |
| `主管/P24-持續開發與同版驗收-20261027/parallel-task-boundaries.json` | 13652 | `9caf9cef1a6a11450c279ba98d3503d93ccf4599f4b6b0ab950503883080b05b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=baseline,dllSha256,manifestSha256,tasks,normalIntegration,liveUiOwner |
| `主管/P24-持續開發與同版驗收-20261027/player-facing-integration-contract.json` | 4218 | `e1a36d5cebf6708b1c557876f6b84f7cb427d45fb41bbd89d37edbe5d560d989` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,baseline,integrationOwner,sharedSourceFiles,rules,firstVisibleAcceptance |
| `主管/P24-持續開發與同版驗收-20261027/player-hud-root-review.json` | 4099 | `40ebfb174ea6bafdd5d4513cac984c0d742f93af7efebbf575fde73135bda70e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,source,review,sealSha256,rebuiltDllSha256,checks |
| `主管/P24-持續開發與同版驗收-20261027/player-pointer-root-review.json` | 11129 | `ed222f288304052e0fb938d9d1469d164262590fc149de035e495d3a52c7a734` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,source,review,sealSha256,checks,live |
| `主管/P24-持續開發與同版驗收-20261027/player-shadow-coverage-root-review.json` | 1949121 | `4e9cd19e8fedc1e14b46bbecdcafb4737ddd6653600698ea051b4df502be34b8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,source,review,sealSha256,rebuiltDllSha256,checks |
| `主管/P24-持續開發與同版驗收-20261027/player-shadow-root-review.json` | 61432 | `40ec8f0124a2f9a45a38cab4d9d2a1d6fde5edff56189faae6244bbf080ed935` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,source,review,sealSha256,rebuiltDllSha256,checks |
| `主管/P24-持續開發與同版驗收-20261027/player-ui-priority-20261027.json` | 8076 | `39a1f3e7f80b0040f636947c725f7fdaf0a7d5ce3d1c051d1a6f13f1e262ba84` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,authority,relayedUserQuotes,relaySourceThread,baseline |
| `主管/P24-持續開發與同版驗收-20261027/private-shop-dispatch.json` | 556 | `426b896a404437eea319562d65966955bc362e6b666690272a2185c9d063974e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,threadId,writeRoot,baseline,status,priorTaskAccepted |
| `主管/P24-持續開發與同版驗收-20261027/private-shop-review-dispatch.json` | 535 | `45319678d0bca0ca51a24232b0cbb0d004a495ee6a5bbc5f83e0b3ed372a39c6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,threadId,writeRoot,target,status,scope |
| `主管/P24-持續開發與同版驗收-20261027/r17-after-first-cancel-bonus-stats.json` | 5446 | `5fb6255ea013c7cb4dc961d5d4694c7c6c4128597fc001d532f75735b61b6fe0` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-持續開發與同版驗收-20261027/r17-before-live-bonus-stats.json` | 5446 | `1f7150a76f414d7fbda946e622b1538267f3fc5a78685debefcb9b845f4c32d9` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-持續開發與同版驗收-20261027/r17-final-bonus-stats.json` | 5446 | `3e0b6330a803ad8fc6bd738d5176f2045106b1bf3ba4715ffcb8116aa7b0432a` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-持續開發與同版驗收-20261027/r17-native-key-live-result.json` | 7159 | `0a4299b272440ee89439c455d6dfdf37aa525e8a7e8f1b2d318f3314fe247811` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,launch,method,up,down,typing |
| `主管/P24-持續開發與同版驗收-20261027/r17-native-keys-launch.json` | 882 | `8c75b0f9f65d19d2428e9441ca726e19bc329c0960d524144cb7ac38069b5596` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,productRoot,revision,executable,dllSha256 |
| `主管/P24-持續開發與同版驗收-20261027/r17-service-first-launch.json` | 884 | `2c97bda6c232990810ee9ef3d0942915ae68b3e5c77c9b392ab9a7a22bf30b1c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,productRoot,revision,executable,dllSha256 |
| `主管/P24-持續開發與同版驗收-20261027/r17-targeted-live-accepted.json` | 10713 | `6d6182adc2ac99e2a5c3d4d424c4c47409b42d6decf1ce9cf9fbdeb6f77a2b20` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,wholeP24Complete,formalFreeze,review,launch |
| `主管/P24-持續開發與同版驗收-20261027/r17-targeted-live-result.json` | 10511 | `d9186e70bdcc816a2357cf8bf697c1fecc5639ebe0d03fa43673092acd97b384` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,wholeP24Complete,formalFreeze,review,launch |
| `主管/P24-持續開發與同版驗收-20261027/r18-before-final-flow-stats.json` | 5446 | `fb2f41dac3ae12b85ce4618f56dfe56ba9ac4bbfa3351ac4e3309511618a35e1` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-持續開發與同版驗收-20261027/r18-current-status.json` | 1742 | `71ebf8deeef3a38207d93e5c5b44be5aba9d51d9002bda0916c8d12078d987ce` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,revision,utc,inputLock,lockSha256,manifestSha256 |
| `主管/P24-持續開發與同版驗收-20261027/r18-final-service-launch.json` | 850 | `2024b2841ab9425105d3eab7ac7b2e9a7bb45bd830c131f9be246f27933dda20` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,productRoot,revision,executable,dllSha256 |
| `主管/P24-持續開發與同版驗收-20261027/r18-live-progress-before-closed-round.json` | 4147 | `add051290935c53342aabaf78354b22c6a748d26113439b1944b068d454bc48a` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-持續開發與同版驗收-20261027/r18-live-progress.json` | 5655 | `6fc6021ad10bb9235e8719cddce343d39aa7805e125b90ce6bf4ae078af79af5` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-持續開發與同版驗收-20261027/r18-peer-final-flow-launch.json` | 852 | `2249cbcfae3e86933c50c34328b8f645daa2ec788677c6523c56baf489d41eff` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,productRoot,revision,executable,dllSha256 |
| `主管/P24-持續開發與同版驗收-20261027/r18-service-recovery-launch.json` | 853 | `2594b1ef8e0c346c0a388a04102e55f5861b31a39835158600bf9a85c192b9e0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,productRoot,revision,executable,dllSha256 |
| `主管/P24-持續開發與同版驗收-20261027/r19-before-final-flow-stats.json` | 5445 | `dd96e3cfaefec525195a3185dc98d9405d4f9478c47660b6e70039a1cdbf9f83` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-持續開發與同版驗收-20261027/r19-current-status.json` | 1010 | `9f83a80b4de7830b6d65c94edeab5f93848234d7fa3e896ce2e717ea9cc34280` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,revision,dllSha256,manifestSha256,lockSha256 |
| `主管/P24-持續開發與同版驗收-20261027/r19-final-flow-launch.json` | 847 | `8fb457ed415bb961b46eb0972d4f19809389f737c0d9b1f6c400de18d8df7fbc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,productRoot,revision,executable,dllSha256 |
| `主管/P24-持續開發與同版驗收-20261027/r19-live-progress.json` | 1724 | `b956743d8f11123ec7863f105297ca3d6b9a1bfea119ec9cd6165f9186c47ddc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,version,liveRoot,completed,current |
| `主管/P24-持續開發與同版驗收-20261027/r19-peer-final-flow-launch.json` | 852 | `849adfee7442e717b7e9fec65a28f916ec491109bf81fd08060ddd3351212a08` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,productRoot,revision,executable,dllSha256 |
| `主管/P24-持續開發與同版驗收-20261027/r20-after-normal-str-allocation.json` | 5446 | `d9c3847aecbee20b06f041c10fd05b8bae65714ce695591273cbfae96e4b59ac` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-持續開發與同版驗收-20261027/r20-before-normal-str-allocation.json` | 5446 | `0aca734dacf229e6a9a2e609d71a80e0cf2e000a11af96e2ee4aca4d434e2f10` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-持續開發與同版驗收-20261027/r20-current-status.json` | 1166 | `6d3a3559199d11d58272549e8eedf681f5c57c138bd168902fce296e717bcee5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,phase,revision,dllSha256,manifestSha256,sealSha256 |
| `主管/P24-持續開發與同版驗收-20261027/r20-live-progress.json` | 7843 | `b0fbf0468d03f5034b063f0ad27a12eb6943fcb50aa600cb53bcf3210198d078` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,revision,status,identity,verified,newBlocker |
| `主管/P24-持續開發與同版驗收-20261027/r20-main-live-launch.json` | 2090 | `ee4dd0e6fc3971b44c104547677d894dd3c0a1974dcf41db2e704e05495b354c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,instance,revision,productRoot,executable |
| `主管/P24-持續開發與同版驗收-20261027/r20-peer-live-launch.json` | 2090 | `219c8e83f691afba0421b34adab9aa9c073809102b6919ea91a25ee970d309d2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,instance,revision,productRoot,executable |
| `主管/P24-持續開發與同版驗收-20261027/r21-current-status.json` | 1704 | `94e0e802e6d349e50a1135cb4f9f7357027e7f684bc80e41ad263e154ce710b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,version,review,lock,lockSha256 |
| `主管/P24-持續開發與同版驗收-20261027/r21-live-progress.json` | 1704 | `94e0e802e6d349e50a1135cb4f9f7357027e7f684bc80e41ad263e154ce710b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,version,review,lock,lockSha256 |
| `主管/P24-持續開發與同版驗收-20261027/r21-main-live-launch.json` | 2090 | `5fdb08d1c45679824a0fb9d3a8c2072c1cafdbf9bb1830a62ea8e9131df084be` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,instance,revision,productRoot,executable |
| `主管/P24-持續開發與同版驗收-20261027/r21-peer-live-launch.json` | 2090 | `afb0d077815edb27d71fbe8cf329b30f36dd07b7659a7c4231ec9281eea093de` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,instance,revision,productRoot,executable |
| `主管/P24-持續開發與同版驗收-20261027/r22-completion-matrix.json` | 13477 | `0a597d38b94c156555cace3225bac0b173bbc49ca9eb2758ae96a1f32ea83451` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,baseline,dllSha256,manifestSha256,previousStopReason,wholeP24Complete |
| `主管/P24-持續開發與同版驗收-20261027/r22-current-status.json` | 1112 | `396e7b189724ae9267dc8e740ba25ed21b176164c67438c00983fa7ad9744009` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,revision,review,sealSha256,lock |
| `主管/P24-持續開發與同版驗收-20261027/r22-full-main-live-launch.json` | 2090 | `04339f97823b05a3e338b6f1bd3acce5152fbb5ea92e27126b7cc4e37a40b837` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,instance,revision,productRoot,executable |
| `主管/P24-持續開發與同版驗收-20261027/r22-full-peer-live-launch.json` | 2090 | `0386c0ac4b7bc7d39e62c51da19615eb387b36debe919cec6d54f3e9447c8d84` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,instance,revision,productRoot,executable |
| `主管/P24-持續開發與同版驗收-20261027/r22-full-peer-restart-live-launch.json` | 2090 | `0b03a071fe1b0e07acefe25822e94acc787358d3e97b54e124d9de6a10a0ea4f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,instance,revision,productRoot,executable |
| `主管/P24-持續開發與同版驗收-20261027/r22-main-live-launch.json` | 2090 | `441d45ba011667c41f5c508bd3357805a2cee2d512ff150e66c8c894b34d12ce` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,instance,revision,productRoot,executable |
| `主管/P24-持續開發與同版驗收-20261027/r22-peer-live-launch.json` | 2090 | `65d254bffc1f649ffe39b7adc16f172622fa55522277b1a3aecae7416505c72b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,instance,revision,productRoot,executable |
| `主管/P24-持續開發與同版驗收-20261027/r27-completion-matrix.json` | 38968 | `a2300db2fa040bb34e630eee417fd3ee240d61314b7d432e4802da865146b6e8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,revision,wholeP24Complete,sealSha256,dllSha256,entry |
| `主管/P24-持續開發與同版驗收-20261027/r27-launcher-root-review.json` | 2508 | `5357b3dbe79004b68b14c04f562813bcbe27f0ece1ca14b62d08dfb10c8e8086` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,sealSha256,independentlyRebuiltFiles,restrictedPath,check |
| `主管/P24-持續開發與同版驗收-20261027/r27-live-test-plan.json` | 947 | `b26a2802d2600423abca5b11cddae1110edce21e860ec5e869136e3189fe36cb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=revision,entry,role,actions,noReseeding,noServiceRestart |
| `主管/P24-持續開發與同版驗收-20261027/r27-package-root-review.json` | 446 | `650803bf23faf8ac9ba33d1da53a1606fa1563ab70b761790f4b80c02e23e269` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,sealSha256,filesFullHashVerified,compilerSourceMatchesIndependentRebuild,dllSha256 |
| `主管/P24-持續開發與同版驗收-20261027/release-integration-dispatch.json` | 662 | `0f3a65c67648291b4acdb4782c9f83b42d96126996a2159e88a1ca7eb2a8c134` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,threadId,writeRoot,baseline,target,inputs |
| `主管/P24-持續開發與同版驗收-20261027/reproducible-build-dispatch.json` | 586 | `79380ee34853c97c1589de45feb4345079bffc73374ae4cad98c433acde5150a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,threadId,writeRoot,baseline,status,priorTaskAccepted |
| `主管/P24-持續開發與同版驗收-20261027/root-goal-tool-receipt.json` | 1243 | `88a4a98e46a099d08122b48d2fb176a7707f691882683ddcd5816b8e80821533` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=observedUtc,tool,toolResponse,interpretation,stopAudit |
| `主管/P24-持續開發與同版驗收-20261027/shadow-original-slices-root-review.json` | 122 | `b86a5eadfc6f4a0a497e9cb8e58e6487f3a3a6d40ac722f350576cad69d8c55a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=passed,sourceSlices,idxAndPakAndExtractedBytesMatch,wholePakRescanned |
| `主管/P24-搬移後環境煙霧驗收-20261029/DIAGNOSTIC-DECISION.md` | 4828 | `e93307fe51921f0aa5c59fd5ff7b409ddab462bc63f0a6fd8000275eff0795ad` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-搬移後環境煙霧驗收-20261029/HANDOFF.md` | 1474 | `5f6214ab5672c760ac33396831027955b53be08f6877950ea58479416975f80d` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-搬移後環境煙霧驗收-20261029/after-server-start-failure.json` | 16202 | `33b701fb389a4d0746234b9a4e0abe6956a1fbd601aced6ecb47d25d6aea3a83` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,readOnly,dbRuntime,rows |
| `主管/P24-搬移後環境煙霧驗收-20261029/before-server.json` | 16202 | `d057f1cad35c40dfa4aec0e92128c901fa6a655113f6bcce90daf2fea9d048c9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,readOnly,dbRuntime,rows |
| `主管/P24-搬移後環境煙霧驗收-20261029/db-start.json` | 276 | `66892b3648a35c406902124684e840c226019e04788634d6553486953b028ed7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=args,executable,bootstrapOrInitialize,pid,utc |
| `主管/P24-搬移後環境煙霧驗收-20261029/diagnosis-baseline.json` | 16202 | `35cdbce915b980f1ea11b5085c8550319cea4d149df7b805081b0bda3e42be31` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,readOnly,dbRuntime,rows |
| `主管/P24-搬移後環境煙霧驗收-20261029/diagnosis-final-time.json` | 1730 | `8ab78d5711505885b20376ce6df14746c51d1568608d5c59418fbb0c22d92a34` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,readOnly,dbRuntime,rows |
| `主管/P24-搬移後環境煙霧驗收-20261029/diagnosis-final.json` | 16202 | `df625daabfca981be9263cc1a5a0f15f0c7dda9bfa2cd221f0629dfc6695eb65` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,readOnly,dbRuntime,rows |
| `主管/P24-搬移後環境煙霧驗收-20261029/diagnostic-completion.json` | 710 | `ccc89ccc43f3c6ebac7a904b3fe25b380847e5894e7d3b5247fcd67a7dceca9f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,rolesUnchanged,itemRowsUnchanged,skillRowsUnchanged,originalTimedRowUnchanged |
| `主管/P24-搬移後環境煙霧驗收-20261029/diagnostic-dispatch.json` | 1952 | `f9a0b90b138fbe8fb791151dcfcd23ed555eaee6e6692a1f0e71421cbe85360c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,scope,goalActualStatus,supervisor,tasks,dataChangeApproval |
| `主管/P24-搬移後環境煙霧驗收-20261029/diagnostic-review.json` | 1696 | `138f36bca6da138f96a22266ecadc4e47cd8e297322719e948c52092698725c0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,passed,scope,reviews,actualJarClassesChecked,methodAnchorChecks |
| `主管/P24-搬移後環境煙霧驗收-20261029/diagnostic-send-receipts.json` | 465 | `25f8a96db6c8351af73d8829ef49faabcf43e543c22245286f7b860679c6cc23` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=lifecycle,repairReview,scope |
| `主管/P24-搬移後環境煙霧驗收-20261029/key10326-timeline.json` | 7334 | `2c59e8f8d8afc4c3ca19e83918aa05b0b3b9502ee18743cf265d8670b49fe956` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,objectId,templateId,owner,evidence,boundedArchiveTransitions |
| `主管/P24-搬移後環境煙霧驗收-20261029/lifecycle-select-results.json` | 4363 | `2790f6b8840fa5ddab0a35e1a80a44fe9dcd32e1e7b691eb2950955c69007308` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,readOnly,sqlSource,sqlSha256,dbRuntime,results |
| `主管/P24-搬移後環境煙霧驗收-20261029/pre-start-processes.json` | 256 | `2d759fca27a5e97ba47141cfab0f9a5e1ff8c1e9bc5f9fd9a2f7de6dccd166cb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=processes,listeners |
| `主管/P24-搬移後環境煙霧驗收-20261029/preflight.json` | 168613 | `39c8f312e18f72797a34e3832600a2a779cc00b1021f841c9d418065577c2b6f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,passed,oldRootsAbsent,asciiAliasVerified,coldBackup,databaseFilesCompared |
| `主管/P24-搬移後環境煙霧驗收-20261029/reception.json` | 1518 | `69e9515c783d02b05f74ffdfeabeb9f6d88227926aa421fcc085989260f5ab69` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,supervisor,integrator,acceptedScope,goalActualStatus,goalResumeSupportedByExposedTools |
| `主管/P24-搬移後環境煙霧驗收-20261029/repair-review-acceptance.json` | 1292 | `2b27a4081121b7d6805f85dab7a5d791d13d0e2fe31f17efe40598deef237a56` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,decision,worker,cursor,workerIdleObserved,artifactManifestSha256 |
| `主管/P24-搬移後環境煙霧驗收-20261029/result.json` | 3713 | `308af8050dbffbef1113228da520056e9446f6a5d3df1e4472f92925ad747853` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,supervisor,goalActualStatus,goalResumeToolUnavailable,wholeP24Complete |
| `主管/P24-搬移後環境煙霧驗收-20261029/server-start.json` | 1242 | `d2b5ee8055c81f92d2dded320b8634308ed286ccb5f7757f7245c1430b17e9de` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,args,jarSha256,policySha256,preStartObservation |
| `主管/P24-搬移後環境煙霧驗收-20261029/timed-counts.json` | 1562 | `c4fa0de3550e7561a54e7b2640e1f06c97338c224cae89eb6b211081c437a737` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,readOnly,dbRuntime,rows |
| `主管/P24-搬移後環境煙霧驗收-20261029/timed-engines.json` | 1666 | `f12fb9eb5db92e084186410a7d5b3c57c324ca97434cb051eba38e8498203df7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,readOnly,dbRuntime,rows |
| `主管/P24-搬移後環境煙霧驗收-20261029/timed-rows.json` | 1730 | `168bd7631eaf467b0c951726d790f5ffde96aff8ca3084ae1e19475cf789a865` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,readOnly,dbRuntime,rows |
| `主管/P24-搬移後環境煙霧驗收-20261029/timed-schema.json` | 1581 | `294d41b8a3f87e71d350db357e808cb1061908f0de094b71523ea1761f8ed948` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,readOnly,dbRuntime,rows |
| `主管/P24-新主管接管與6016整合-20261027/goal-tool-availability.json` | 1543 | `9a381c6c57b8e0121c971f60571b55518af5de40fd108301f3851230f85333c9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,supervisor,requestedObjective,discovery,note |
| `主管/P24-新主管接管與6016整合-20261027/takeover-receipt.json` | 5711 | `456854ea8a2ddf36f95dee790cb0f2a4b91c5bcda34416069aba664e3df2a7c2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,supervisor,parent,previousSupervisor,authority,soleDispatchCoordinator |
| `主管/P24-新主管接管與6016整合-20261027/task-queue.json` | 1309 | `a6e322ce1e49613d96fd53d0644cdcd7c07985a5e329892ffd4b2bce09ccdba1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=supervisor,phase,product,tasks,integrator,review |
| `主管/P24-新版圖像時序證據接點-20261026/baseline.json` | 229 | `048762ded7b46a18bdb53fa62e65b7cfb5cf9f041a6a17f2e29a479321becf82` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=path,sha256,scope |
| `主管/P24-新版圖像時序證據接點-20261026/formal-identity-syntax.json` | 377 | `774cde6f1da7022fdf86ed48311a1e52a104e547fb6d40761d3a818da62d299e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,sha256,scope |
| `主管/P24-新版圖像時序證據接點-20261026/validation.json` | 2620 | `6a57141c3a6ccafb223502652f0a0cb2bf3f6913f0c073636f2ad84c3d601610` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,checks,newRevisionLiveExercised,pixelRenderingChanged,evidenceWrittenIntoCapture |
| `主管/P24-新版圖像時序證據接點-20261026/web-frame-counter-check.json` | 439 | `fc1cebcefdc6f288be91c0e810bc30a951dcc397b38b7890e59dd0c0977886b6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,rows,scope |
| `主管/P24-本機主管接管與完整整合-20261028/audio-interop-review-acceptance.json` | 420 | `2dc980aa5690be65a77357c96f0487600882ae2469ea953ba35b8fda99ae6f04` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=reviewResultSha256,artifactPinsVerified,findingsAccepted,fixAssigned,formalAudioAccepted,nativeR1DiagnosticIsSeparate |
| `主管/P24-本機主管接管與完整整合-20261028/completion-matrix.json` | 44813 | `815ea14323936891a66eedc24e8e8b324d2a873934d9418832437097e48ccd55` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,revision,wholeP24Complete,sealSha256,dllSha256,entry |
| `主管/P24-本機主管接管與完整整合-20261028/formal-r28-dispatch-receipt.json` | 398 | `d14e99463bdb3f824a6b33444ccb30c2ebaa8f62680086b80f2d68519934091a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,worker,outcome,task,writeRoot,gateReceipt |
| `主管/P24-本機主管接管與完整整合-20261028/goal-readback.json` | 789 | `4c36e55b32ba2648bf48354cc5102fa39d176d8bf64a357d3c2e7988c4ff02d8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=goal,remainingTokens,completionBudgetReport |
| `主管/P24-本機主管接管與完整整合-20261028/goal-tool-readback.json` | 789 | `4c36e55b32ba2648bf48354cc5102fa39d176d8bf64a357d3c2e7988c4ff02d8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=goal,remainingTokens,completionBudgetReport |
| `主管/P24-本機主管接管與完整整合-20261028/host-r1-review.json` | 740 | `e025720942972417d818c6a342a6f36e76755be36f119c5ef80bb45c1db99dc4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,decision,sealSha256,pinsVerified,sourceInspected,independentReviewer |
| `主管/P24-本機主管接管與完整整合-20261028/host-r2-dispatch-receipt.json` | 462 | `9914340dbe07a68d5c0cc3add190f66a71a9344f8bc3085246f6621587cd47c4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,worker,tool,outcome,writeRoot,baselineR1Seal |
| `主管/P24-本機主管接管與完整整合-20261028/native-activation-recovery.json` | 529 | `bea37486dba6ce87e8c221c6eb76b87de9f2ff3c78200a62347d3c327413b23d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,baseline,ownedClientPid,returnedWindowId,newRecoveryAction,captureError |
| `主管/P24-本機主管接管與完整整合-20261028/native-route-review.json` | 999 | `6f6c376dc6823adba16b06ef3587553fcb09c66602828e119dc18870dd7f0922` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,decision,sourcePinsVerified,map0ConfirmedActors,known6016,unknownCoverage |
| `主管/P24-本機主管接管與完整整合-20261028/native-tool-preflight.json` | 624 | `d8bbc7378530ad07742e3b41d474225f8495714dfe1021be6e3bddd52374c506` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,baseline,gameLoginAttempted,fullWindowCapture,accessibility,pointer |
| `主管/P24-本機主管接管與完整整合-20261028/retained-roles-after-caesar-disconnect.json` | 15896 | `79d24e54e9d760d6475f278530b4d0e37ca943b6106e086fba74a2ebf9e92a1c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,dbRuntime,gameRuntime,rows |
| `主管/P24-本機主管接管與完整整合-20261028/retained-roles-after-cave-home-save.json` | 15896 | `f13eeeb580c55775e789b47c9d5198939f484ba0d841abecddb6ab49203341df` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,dbRuntime,gameRuntime,rows |
| `主管/P24-本機主管接管與完整整合-20261028/retained-roles-after-death.json` | 15719 | `751ee2b9b325ff6fb36b2527d7e86d08dd29ee991515e747c6954199ee622157` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,dbRuntime,gameRuntime,rows |
| `主管/P24-本機主管接管與完整整合-20261028/retained-roles-after-ground-save.json` | 15895 | `8f19cb213338e7eb09abb970c84a3bd6b08fb5b3259a33493c94c2236f20f0e0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,dbRuntime,gameRuntime,rows |
| `主管/P24-本機主管接管與完整整合-20261028/retained-roles-after-normal-save.json` | 15722 | `1d9f957d1370d2d3e531788c2f9baf2b5e265934af7d3eb24e7e33a0396925ff` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,dbRuntime,gameRuntime,rows |
| `主管/P24-本機主管接管與完整整合-20261028/retained-roles-after-port-death.json` | 15893 | `7ed2bcaef82da5f0b325bfc94a62d4f64326d09a7bc8384a92c150bae29db713` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,dbRuntime,gameRuntime,rows |
| `主管/P24-本機主管接管與完整整合-20261028/retained-roles-after-strength-save.json` | 15723 | `4a833f6b1e3c12685f5a73cb8501ed9cefac6a3f87274ef41a143edbb1a29e4f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,dbRuntime,gameRuntime,rows |
| `主管/P24-本機主管接管與完整整合-20261028/retained-roles-before.json` | 15721 | `5d7dff25829a42d24dc61dec2fef22d8a69bae2685964bdbb0d11bfabcbc0d82` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,dbRuntime,gameRuntime,rows |
| `主管/P24-本機主管接管與完整整合-20261028/service-preflight.json` | 1381 | `4094967e2510051c0649b58c6798bea6bf45a2b07299a1b280ed63ac21b01212` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=db,previousGamePid,previousGameAbsent,port42061Absent,exitCause,jarSha256 |
| `主管/P24-本機主管接管與完整整合-20261028/takeover-receipt.json` | 6432 | `846e80dbd676c4a9778b8c347383e88128b5d151ce7a4309bb89ba304d7a2ab6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=supervisor,previousSupervisor,previousStatus,replacementReason,soleSupervisorWriter,soleDispatchCoordinator |
| `主管/P24-本機主管接管與完整整合-20261028/task-queue.json` | 5058 | `4f307bc84668ab31b705d65a1e4b6178cfeabe724c6a1b255c37d29e36c6d21c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=supervisor,product,wholeP24Complete,tasks,integrator,native |
| `主管/P24-權威拾取動畫-20261029/baseline-final.json` | 29657 | `2a946e965f1aed1f9a0358ceb560f52575ea5a3bf217fc9ccf36e9ce5823c40f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,baseline,releaseRoot,releaseSeal,releaseSealSha256 |
| `主管/P24-權威拾取動畫-20261029/baseline.json` | 57792 | `38956c96dfa7e6c7360fade71f8c095323990c2d5d24cb48395073457459b1ad` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,baseline,phase,stageRoot,stageSealSha256 |
| `主管/P24-權威拾取動畫-20261029/candidate-r1-root-integrity-verification.json` | 1090 | `85c86d5b0dc7a730ca7cdd245ccd25302d228004b7f44a0b966bfe4a99df0629` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,candidateId,candidateSealSha256,verifierActuallyExecutedBeforeThisReceipt,artifactCount |
| `主管/P24-權威拾取動畫-20261029/candidate-r1-root-static-review.json` | 1411 | `c8bac7a3d92e5d6b50c66754e54319c9a2d80452feb8fa16329a6acb56f52670` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,candidateId,candidateSealSha256,status,filesRead,findings |
| `主管/P24-權威拾取動畫-20261029/candidate-review-r1-active-receipt.json` | 499 | `064ce8fee6fb794908e5361cb38e0e4ae4f029316d76f23cc5a62abebbc1cde5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,taskId,owner,turnId,cursor,status |
| `主管/P24-權威拾取動畫-20261029/candidate-review-r1-dispatch.json` | 4159 | `c6092207848a18734f275901d559233c1cad10feba595c94102c3bd6ec85c0fc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,prompt |
| `主管/P24-權威拾取動畫-20261029/candidate-review-r1-root-acceptance.json` | 1694 | `34cd28e4567321ccd8be099fa02639941d981b0b4bd862d7a203d7dc3806e639` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,candidateId,candidateSealSha256,reviewSealSha256,reviewAcceptanceSha256 |
| `主管/P24-權威拾取動畫-20261029/canonical-plan-active-receipt.json` | 510 | `23dad5f4058d48c37f3c0ec49e9f8a76103eb50f612ad3f5234279afd2e54d4f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,taskId,owner,turnId,cursor,status |
| `主管/P24-權威拾取動畫-20261029/canonical-plan-draft-finding.json` | 964 | `9535315904b4c81658fe2988d40ce99019a47915246ae0dcee28a52e227cf2e2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,taskId,finding,evidence,requiredResolution,productBuildExecuted |
| `主管/P24-權威拾取動畫-20261029/eligibility-source-root-verification.json` | 1053 | `9de4f65771330edff28e60f3067486e6c40301be060fac0ddb4baea730083c93` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,sourceSealSha256,resultSha256,sourceContractSha256,presentationPolicySha256 |
| `主管/P24-權威拾取動畫-20261029/final-base-binding.json` | 665 | `28d0d6f3386575c175aa06b7b9076da961cfd1db62e2563cf7cd65c7c276ea95` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,baseline,originalBaselineSha256,finalBaselineSha256,stageSealSha256 |
| `主管/P24-權威拾取動畫-20261029/implementation-active-receipt.json` | 539 | `af4eb02e3f4da1c96dc79112cf9a51b76d3abc21aaaa75867b82abdea36e6558` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-權威拾取動畫-20261029/implementation-contract.json` | 8517 | `8c60b1061c637bcb7f992d7f92c74fc80c0ad945c7f05d1c3a2d74507cffc6f2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=id,status,baseline,scope,trigger,observableAdmission |
| `主管/P24-權威拾取動畫-20261029/implementation-dispatch.json` | 4529 | `ffae94e796e866256aa0d6c05eb44021658fa8871e997efdcff10c56c88e8e39` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,prompt |
| `主管/P24-權威拾取動畫-20261029/independent-preparation-root-verification.json` | 1363 | `ade95446777fcf36f797ad58614bfa9cd84c296e2f90f60003c816011295f274` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,artifactSealSha256,resultSha256,artifactsVerified,sourcePinsVerified |
| `主管/P24-權威拾取動畫-20261029/integration-prep-active-receipt.json` | 431 | `cf40413c49cba2957b90714dad2f84128ba75ae06b761691782dfedd5eb38189` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-權威拾取動畫-20261029/integration-prep-dispatch.json` | 4438 | `75b44796cbc73614aea78f10a5b840d022159f3cae86e8b6e2e41ddef5960bcc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,prompt |
| `主管/P24-權威拾取動畫-20261029/integration-prep-r1-root-verification.json` | 983 | `a19e1d91c9a0ab4486bc4becca44f887a87e29216a6e715d18eee5441a99d160` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,preparationSealSha256,artifactSealSha256,preparationFiles,artifactFiles |
| `主管/P24-權威拾取動畫-20261029/integration-prep-r2-active-receipt.json` | 431 | `aba767374352fa96e299a25d1a7674e788754c5dc5e768ff5d5f30cd8efde5c1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,taskId,owner,turnId,cursor,status |
| `主管/P24-權威拾取動畫-20261029/integration-prep-r2-dispatch.json` | 2912 | `771e37ef4acaf8cdbbd2520a5a2a3d662b89131f3d36fcc8a7bf620246a118a6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,prompt |
| `主管/P24-權威拾取動畫-20261029/integration-prep-r2-root-acceptance.json` | 1137 | `39039324435f6f525fc181be156a2823255402a08a548a181ace4d05b92c8c3f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,preparationSealSha256,artifactSealSha256,verifierActuallyExecuted,preparationFiles |
| `主管/P24-權威拾取動畫-20261029/queue.json` | 6609 | `959480d7bdc600850c17b7f32568fa49079004b836ebe5b4dae1ed09052c8d6e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,targetRevision,baseline,baselineSha256,implementationAuthorized,sharedMergeAuthorized |
| `主管/P24-權威拾取動畫-20261029/r34-input-contract.json` | 2391 | `37ff68bd2ce504a7dc1f8df8b80a9a9b70068c96d159fc5b1e5df75246c2efd1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,candidateId,candidateRoot,preparationSha256,implementationContractSha256 |
| `主管/P24-權威拾取動畫-20261029/r34-isolated-build-acceptance.json` | 1943 | `ed80a7daa26d6b6aa3f00f12fadfb655d947ad33f76f5969821aaaa1a6cfa237` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,revision,dispatchId,decision,authorization |
| `主管/P24-權威拾取動畫-20261029/r34-isolated-root-gate-verification.json` | 472 | `cb804f03a5361aef91d86dec7bb1db1da3d2c269ec8e5ddec42eede5d9cc43f3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,command,status,sourceFiles,writes,exitCode |
| `主管/P24-權威拾取動畫-20261029/r34-root-stage-verification.json` | 729 | `355bf89c8e039501c1fd81094d17c7e14706e6e45403197df6e45e9a31708938` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,sealSha256,fullInventoryFiles,hashedNonAssetAndIndex,assetSameFiles |
| `主管/P24-權威拾取動畫-20261029/r34-stage-active-receipt.json` | 769 | `daf7230a7ddced3f7fae813429f16e6434211660713af225db9f4b3d08a39f32` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,taskId,owner,turnId,cursor,status |
| `主管/P24-權威拾取動畫-20261029/r34-stage-dispatch.json` | 4028 | `67f5f6b85eb9ceed1b6fcab1448e774b822b5c0c4c3663d0de3fb8d615953480` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,prompt |
| `主管/P24-權威拾取動畫-20261029/r34-stage-root-acceptance.json` | 1628 | `d8923799b13660f4eab08ac52d45f27c183c3d0e01aed3d491a62b593e110308` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,decision,candidateId,revision |
| `主管/P24-權威拾取動畫-20261029/review-prep-active-receipt.json` | 472 | `6bb3d174f7da2206f8620cea74726cb6b543e23235365d6502f9f1e17596e5ed` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-權威拾取動畫-20261029/review-prep-dispatch.json` | 3019 | `42112b8b643bc8b7012f9bcc500e710166e2b733d3ee9d1723407ffd24fb3c38` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,prompt |
| `主管/P24-權威拾取動畫-20261029/workers-after-candidate-acceptance.json` | 683 | `a7d85e54644e5f993a38ceb0ad9a973828f99e2b3515f99f372ad471a13713a6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,integrator,reviewer,author |
| `主管/P24-正常產品入口獨立審查-20261026-r2/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P24-正常產品入口獨立審查-20261026-r2/processes.json` | 13820 | `7d39918fd0cf09e6efb5ee2b60ce479061c69fc7f6ee6f98233fc3b54c79d7df` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(25);keys=name,pid,args,exitCode,elapsed |
| `主管/P24-正常產品入口獨立審查-20261026-r2/protected-history.json` | 292 | `40473acb61fe1914e83512eed1c0e2b2cd50851ab490702c0ce0999534b53258` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,files |
| `主管/P24-正常產品入口獨立審查-20261026-r2/result.json` | 15564 | `98d5dfff30964f02c2cd13692ae00a21aa02eb16f306402495997737a36ca792` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,snapshotFiles,assetInputs,checks,cleanDll,renderedScenes |
| `主管/P24-正常產品入口獨立審查-20261026-r2/新版客戶端使用說明.md` | 2025 | `1e5ad4e30810a2b54f74139c3c84b0107564f608de7eeb93ae4f91a3ae9f39bb` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-正常產品入口獨立審查-20261026/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P24-正常產品入口獨立審查-20261026/reviewer-failure.json` | 265 | `60ca9fd23c7687bcf9939342877cdf3f8a20ab275a558b4cf1b932af3b0e0e6a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,reason,productDefect,ui,live |
| `主管/P24-正常產品入口獨立審查-20261026/新版客戶端使用說明.md` | 2025 | `1e5ad4e30810a2b54f74139c3c84b0107564f608de7eeb93ae4f91a3ae9f39bb` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-死亡復活接線獨立審查-20261026-r2/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P24-死亡復活接線獨立審查-20261026-r2/processes.json` | 5074 | `18ac6bcb4d60c9ac513098babb784d5df828d4d8260e8409b2b707e02a389630` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(7);keys=name,pid,args,exitCode,elapsed |
| `主管/P24-死亡復活接線獨立審查-20261026-r2/result.json` | 4971 | `b642e13ffdde79904161c08e18caf9028b79f9dccbc95dde5733bc28d4470711` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,checks,cleanDll,assetInputs,sharedImmutableAssetStore,unchangedSceneImages |
| `主管/P24-死亡復活接線獨立審查-20261026/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P24-死亡復活接線獨立審查-20261026/processes.json` | 1706 | `a392960d51b2d51a6d36c540ffff4dc9dc1fc29e47f51812a1f62831305e4abf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=name,pid,args,exitCode,elapsed |
| `主管/P24-死亡復活接線獨立審查-20261026/reviewer-failure.json` | 274 | `8c8f388ac3259562d121a9751259d08fc1cd6a4fc2dacc4748fdbf885fb121fc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,reason,productDefect,ui,live |
| `主管/P24-玩家外觀接線獨立審查-20261026/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P24-玩家外觀接線獨立審查-20261026/processes.json` | 9676 | `abc41ac1e02459d726b141929c540db052d1764d69a579c26febe25ede91d046` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(17);keys=name,pid,args,exitCode,elapsed |
| `主管/P24-玩家外觀接線獨立審查-20261026/result.json` | 14967 | `9de5aba8ee88e2136a85a94b988a783950b78114b628895efd6745edb7e7eeb3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,snapshotFiles,assetInputs,checks,pixelFrames,cleanDll |
| `主管/P24-產品互動協議來源-20261026/manifest.json` | 11863 | `f02a5582e93e2921d1707d2d87eab3c399e6e7f6edeeb2ff34d924c83c8d098e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=purpose,jar,jarSha256,serverStarted,socketUsed,databaseUsed |
| `主管/P24-產品互動協議補充-20261026/manifest.json` | 1267 | `22e69a8f54c3e16ce545d7c2d4f16be14f794ebb8ea89c2c6b8038231baff473` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=jarSha256,classes,purpose,serverExecuted,network |
| `主管/P24-緩速加速水例外-20261029/HANDOFF.md` | 859 | `7ee6f1df3c073bbb62f4d0830221d318ede0ac95cd52f6e73006839af229364d` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-緩速加速水例外-20261029/baseline-final.json` | 29934 | `00e9d4664024119595faeead50ade5827258763e47dc3d7068ea9b78abf8c813` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,baseline,sourcePins,releaseSeal,productDllSha256,audioDllSha256 |
| `主管/P24-緩速加速水例外-20261029/baseline.json` | 50001 | `16ab9dbb534e20e03e622777ab530065e1bc97363c08d8b765e2cb880115bb87` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,targetRevision,acceptancePhase,finalReleaseAccepted,finalReleaseSealSha256 |
| `主管/P24-緩速加速水例外-20261029/candidate-r1-review-active-receipt.json` | 2388 | `b260189978284278814b41133603d355e515953c75d98dbb0584c01df619edc5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,raw |
| `主管/P24-緩速加速水例外-20261029/candidate-r1-review-dispatch.json` | 4160 | `5b05dd47cd60916996a9f3fc87257170851355f2919140e5ef16f146e59ca2c2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc |
| `主管/P24-緩速加速水例外-20261029/candidate-r1-review-root-verification.json` | 481 | `1eaf034464d48ab07a0fed563704c1ad30234e3a0378ebde71a1016d19b4990a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=verified,artifacts,sources,checksPassed,npcObservations,acceptanceSha256 |
| `主管/P24-緩速加速水例外-20261029/candidate-r1-root-precheck.json` | 908 | `48dacd9cf975d766ee383339b3b21e5363e497aa744dce14c1aaff7407c5245d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=verified,sealFiles,sourceFiles,sharedR31SourcesVerified,changedSources,candidateSealSha256 |
| `主管/P24-緩速加速水例外-20261029/completion-matrix-r32.json` | 584 | `8fb9f88ba71e312eb8fa0f0aebaef3c92bd2e9df1dadb033a382d3da1d007910` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=revision,offlineCandidate,independentReview,isolatedSameDll,canonicalRebuild,releaseFullHash |
| `主管/P24-緩速加速水例外-20261029/dispatch.json` | 11653 | `4687ad5bdabbfbcf73eecdcbad77818f79b124bf69db81ad9b84eba4fe56ad68` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,tasks,sharedMergeAuthorized,baselinePhase,targetRevision |
| `主管/P24-緩速加速水例外-20261029/final-base-binding-dispatch.json` | 1024 | `73a4bedbecc4f3eefbd4987582202e53f6378210f3c3d4c2ece9d3c75bc67d7c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=message,targets,utc |
| `主管/P24-緩速加速水例外-20261029/final-base-binding.json` | 838 | `d6946c449f35b1d26804bd3fd4878bf24bdb0f74d66a302765dfd91058010fa4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,targetRevision,utc,stageBaselineSha256,finalBaselinePath |
| `主管/P24-緩速加速水例外-20261029/independent-preparation-root-verification.json` | 678 | `962b5ae47ee81da4d95d113c1a9145131deab52843d818b885c525a15da6324b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,verified,preparationArtifacts,sourcePins,observations,meetingContract |
| `主管/P24-緩速加速水例外-20261029/integration-preparation-dispatch.json` | 4563 | `fdc30367e3cc09e3faf2019db96d2b6f64d69a11a0a840dc3a82484252198daa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc |
| `主管/P24-緩速加速水例外-20261029/queue.json` | 4039 | `1aa3f818f555da2a7d736e574d8bbf37e9a5f2a53d045745debb3847775bb14a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,targetRevision,baselinePhase,baselineSha256,finalR31ReleaseBindingPending,sharedMergeAuthorized |
| `主管/P24-緩速加速水例外-20261029/r32-input-contract.json` | 1219 | `3977c56367ae9383868ecf142414733343d6c6df3821d3384455ad159ed171e2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,candidateId,candidateRoot,preparationSha256,baseRevision |
| `主管/P24-緩速加速水例外-20261029/r32-integration-acceptance.json` | 2022 | `ac7863c0f078626188d390575b2b9815280909437a2141990d53b53cf8e0279d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,revision,decision,candidateId,dispatchId |
| `主管/P24-緩速加速水例外-20261029/r32-isolated-active-receipt.json` | 2742 | `80a371325f31fa5ca7a74172d958ec6f2a0f32c28bd40dd079eb3bcfee3ea801` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,raw |
| `主管/P24-緩速加速水例外-20261029/r32-isolated-dispatch.json` | 4271 | `49f75ab61d35f9fec4a3c065f95c60892e6ca186ea412e51098f77b59e841e7b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc |
| `主管/P24-緩速加速水例外-20261029/r32-preparation-root-verification.json` | 577 | `d7531ccf98a84389044a57de08b1556a7262d86b5746282437838d8104b4376b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,verified,acceptance,files,preparationSha256,artifactSealSha256 |
| `主管/P24-緩速加速水例外-20261029/r32-root-entry-and-source-checks.json` | 2801 | `7b4321f54abf4df08d7eaf745ccfd38889969644a98ba8277f5535f8a56c7ba7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,verified,sourceFilesMatchedAcrossEngineAndRelease,integrationArtifactsVerified,integrationSealSha256,releaseSealSha256 |
| `主管/P24-緩速加速水例外-20261029/r32-root-full-release-verification.json` | 919 | `c221c62bc34ea737d5e58ea5731e4ec855fb931afc5bd6e16b3a575c9b1ec9cc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,verified,command,exitCode,result,stderr |
| `主管/P24-緩速加速水例外-20261029/r32-root-stage-verification.json` | 1808 | `fb852614e2c0bc0c705b7da43559a40adc06a6a912c8ec73e30a39a5386fb566` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,stageRoot,stageSealSha256,stageResultSha256 |
| `主管/P24-緩速加速水例外-20261029/r32-shared-integration-active-receipt.json` | 370 | `4188703921e064009aa70dbf8bb01debbe66e5f874bd0413d8af5bed789216e3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-緩速加速水例外-20261029/r32-shared-integration-dispatch.json` | 4306 | `e674268a1afc6bce6ce9f26606e93f430ab3651a6876c64eb5c1e880b8d0cdae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc |
| `主管/P24-緩速加速水例外-20261029/r32-stage-acceptance.json` | 2397 | `2898e684ca8756fba7c0e38973461fcb905f48a43932cf6fafea6403b64c5eb5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,stageRoot,stageSealSha256,stageResultSha256 |
| `主管/P24-緩速加速水例外-20261029/r32-supervisor-acceptance.json` | 3351 | `5ecb6d4d9b09ce99febe4ba4a3e80c95fc57d740c01fbe25d3a7159fa35c6de9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,decision,supervisor,revision,scope |
| `主管/P24-緩速加速水例外-20261029/three-workers-active-receipt.json` | 3627 | `28c4d30c2ad8f03f5061decfdf7960640124f2528ce9f947f6a8aa88bfd8350f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,raw |
| `主管/P24-緩速加速水例外-20261029/two-workers-active-receipt.json` | 3783 | `fed1103f9684a12d91df8d145b5fe573b2f951a00d2a18b1fe7b7eef18bb0d7f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,raw |
| `主管/P24-緩速拾取來源-20261029/active-receipt.json` | 3819 | `d3cd3e2ef4d2981e68f9ba27fba800cd80fb1671de225aaac43026cca5791e70` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,raw |
| `主管/P24-緩速拾取來源-20261029/dispatch.json` | 3504 | `5c5216deaf84e590bfdac50223bf2b22ec598fd09cd02ccbc56f3c13a052689a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc,implementationAuthorized |
| `主管/P24-緩速拾取來源-20261029/queue.json` | 1192 | `e53c0dd92bcdbb11a5b6607b531fbd55a13c1c70a675c563c58b6d8050935017` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,writeRoot,status,turnId,cursor |
| `主管/P24-緩速拾取來源-20261029/review-prep-active-receipt.json` | 379 | `941f7ec624f2ae24bf896dc6bcb9ad84adb5a36e85d25d2ccf895c0767062d7f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,turnId,cursor,status,utc |
| `主管/P24-緩速拾取來源-20261029/review-prep-dispatch.json` | 3094 | `16dac4ef1bd3110bc01402e79bdbd386de96658c4def086920d16791e94c86f9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc,implementationAuthorized |
| `主管/P24-緩速拾取來源-20261029/review-prep-lifecycle-focus.json` | 658 | `14db3efc42f87f7a5f99a5e009d59436b08d0363000a10a1a31743894313aa56` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,utc,threadId,turnId,messageSent,scope |
| `主管/P24-緩速拾取來源-20261029/review-prep-root-verification.json` | 928 | `d190e7876a6d9d45f8f0553b7fa4c624f0d0fdbf45e5ce70b684e1720cd6ca36` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,decision,artifactSealSha256,resultSha256,artifactFiles,sourcePins |
| `主管/P24-緩速拾取來源-20261029/source-r1-root-verification.json` | 696 | `3d0b5b505c9168b1ea03d660cfc7ea7b1669a9349b1bc992d386cb0ce6d8dde0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,artifactFiles,sourcePins,classEntries |
| `主管/P24-緩速攻擊施法接續-20261029/HANDOFF.md` | 916 | `5e9073aca46e4687192ed2a9d2c77021eb6b422cc4f89ac800641ba6ad869b14` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-緩速攻擊施法接續-20261029/all-direction-metadata-consistency.json` | 15973 | `2fc1c3ccec3125ff79b58283e502409bc34403d261d74fd65d69c11d6e65d244` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,profiles,actions,directions,differences,rows |
| `主管/P24-緩速攻擊施法接續-20261029/baseline.json` | 29834 | `2a6db2201916014b44b1281aaf7b244e4ebe0ac49b0bba4c432e9c964125b104` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,baseline,sourcePins,releaseSealSha256,productDllSha256,assetIntegritySha256 |
| `主管/P24-緩速攻擊施法接續-20261029/candidate-r1-review-active-receipt.json` | 604 | `eae87c748449bdc9682b1a3412829f0ad477747ca081c6aa25b31347ca871848` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schemaVersion,cursor,revision,changed,thread,latestTurn |
| `主管/P24-緩速攻擊施法接續-20261029/candidate-r1-review-dispatch.json` | 3644 | `f8c4d1c8b3bb290f56240d06b369cda30143cd3f069613d1142fe4996668fca7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,target,prompt |
| `主管/P24-緩速攻擊施法接續-20261029/candidate-r1-root-precheck.json` | 800 | `df2a5a8738ce20459c6adcc2cbe9f58bcd531b2aceee1854556ff9bd332fbc8f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,candidateId,candidateSealSha256,sourceManifestSha256,resultSha256,sealedFilesVerified |
| `主管/P24-緩速攻擊施法接續-20261029/completion-matrix-r30.json` | 544 | `f440cf64ee9496d33fea63b4f9b33d882feb94f790cf3b6f129453ee5f67c1d1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=revision,candidateAccepted,independentReviewAccepted,isolatedStageAccepted,canonicalBuildAccepted,releaseFullHashAccepted |
| `主管/P24-緩速攻擊施法接續-20261029/continuation-policy.md` | 1392 | `e0c100b0003179fe8fd00f6622ef6864d0b62a7598bda356fba9a653e846ff15` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-緩速攻擊施法接續-20261029/dispatch.json` | 10141 | `ed1309bba5acb16382936fa7635f1532129942891dbd210b1a2d1da70d36d382` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=baseline,tasks |
| `主管/P24-緩速攻擊施法接續-20261029/future-speed1-rounding-observation.json` | 21981 | `edc963c8aa41b90a49f44226ca581ba5f533f245574b78df598b4a913bfa9008` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,scope,method,actionRows,rowsWhereRoundingDiffers,differences |
| `主管/P24-緩速攻擊施法接續-20261029/independent-preparation-root-verification.json` | 592 | `4f09b588313fcacca57872890e880e3f989f1acb55564532ed1bf19ce5cf4a48` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,sourcePins,artifactPins,artifactManifestSha256,resultSha256,contractSha256 |
| `主管/P24-緩速攻擊施法接續-20261029/integration-prep-dispatch.json` | 2689 | `2be55fba58e05ae91909946d0440902377d42375147b035f320089177bdb2aae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=prompt,result |
| `主管/P24-緩速攻擊施法接續-20261029/next-gap-observation.md` | 1822 | `b81e6696d3809c4ebdeb4e9003c5d3311c0ff3b3ef6de0b6270d3534c9d808a6` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-緩速攻擊施法接續-20261029/preseal-observations.json` | 1643 | `8078afe652e538a65214547d1df3e4d901d14832f1cfc52e05eb00bc823e757e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,candidate,sentTo,observedActiveTurn,observedCursor,items |
| `主管/P24-緩速攻擊施法接續-20261029/progress-snapshot-01.json` | 4723 | `0f27fb23bb052974429cfca0fa850308d34c8b79730a6e003f68aa1e33381c1f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=observedUtc,scope,polls,sharedMergeAuthorized,databaseOperations |
| `主管/P24-緩速攻擊施法接續-20261029/queue.json` | 3228 | `4719c17a687d9e8e8741b0b598e198b85906ff823cde17e7fcdaa8bb9381076f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,utc,baseline,candidateScope,goalActualStatus,database |
| `主管/P24-緩速攻擊施法接續-20261029/r30-input-contract.json` | 1204 | `bfd2c3a850654e579db05d7bb849478e693396d2fb5b775beda0936d9a1faf1f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,revision,candidateId,candidateRoot,preparationSha256,baseRevision |
| `主管/P24-緩速攻擊施法接續-20261029/r30-integration-acceptance.json` | 1939 | `b90bc049aebb14c5ba1cf7d228b902227f1da2589d394617b112b8e2bab1ac50` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,revision,decision,candidateId,dispatchId |
| `主管/P24-緩速攻擊施法接續-20261029/r30-isolated-integration-active-receipt.json` | 604 | `826ae6f1c5b197dd2807dec01124dafb2f67c97531a2714ed30939faa25cb420` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schemaVersion,cursor,revision,changed,thread,latestTurn |
| `主管/P24-緩速攻擊施法接續-20261029/r30-isolated-integration-dispatch.json` | 3800 | `1e4d1ec4db503ec950da8bd552bdb8c1fed208ded7b75f5778d6517c80fb4a2d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,target,prompt |
| `主管/P24-緩速攻擊施法接續-20261029/r30-preparation-root-verification.json` | 558 | `166ff2d918db6ce08c9083507aadcc40dad662a9e69f0a341805af8142d3a7fc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,preparationSealSha256,sealedFilesVerified,baselinePinsVerified,resultSha256,acceptedAs |
| `主管/P24-緩速攻擊施法接續-20261029/r30-root-entry-and-source-checks.json` | 1550 | `f5974c4fa28d6e5ba71117f90fe279141ffeadce8aae71430f89d77b76b16350` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,source,entry,nativeLiveAccepted |
| `主管/P24-緩速攻擊施法接續-20261029/r30-root-full-release-verification.json` | 556 | `6ebc7fd2b0229f6cb73ffcc5a2eff1c9150fa77e0aa40638a0c000e24539834b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,revision,files,sealSha256,productDllSha256,audioDllSha256 |
| `主管/P24-緩速攻擊施法接續-20261029/r30-shared-integration-active-receipt.json` | 604 | `d34b4c04149ef2c519a98d2acb880ccd314f7682b44e2f01dbd39026db7642c6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schemaVersion,cursor,revision,changed,thread,latestTurn |
| `主管/P24-緩速攻擊施法接續-20261029/r30-shared-integration-dispatch.json` | 4194 | `f38765f29c26d6ffc51e91394eb1424aaae34e274d7f604dda345ce5f5532595` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=task,target,prompt |
| `主管/P24-緩速攻擊施法接續-20261029/r30-stage-acceptance.json` | 1528 | `20ffb3169bbd724b9cbb3393f8764bebdc73823f8ce55e511e48325b937eed79` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,decision,stageRoot,stageSealSha256 |
| `主管/P24-緩速攻擊施法接續-20261029/r30-supervisor-acceptance.json` | 1239 | `ea9101125ce8c2dae74c9e4fbda6f0543c426e0c7b527a530be037ee35ccf5f0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,revision,decision,releaseSealSha256 |
| `主管/P24-緩速攻擊施法接續-20261029/review-gates.md` | 1988 | `30c7ecc79edcd015b45a52f7aa89090b78377c1fdc5b4e979e7934f38f81d778` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P24-緩速攻擊施法接續-20261029/send-and-active-receipts.json` | 3288 | `83b6d59052f1b60c87783d7ef22d47071db2da8f622921107bc83d91de4efb9e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=send,observation |
| `主管/P24-緩速攻擊施法接續-20261029/three-workers-active-receipt.json` | 3859 | `52d5f8e43c3969706fe2164656ad6a65d2a427988dfa2cb56eeb192c8c3590ac` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=content,isError |
| `主管/P24-緩速生活操作來源-20261029/active-receipt.json` | 1970 | `bf81d228aed816c14a43819f55439c9246514d39cb8368018541670f1d074d4d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,raw |
| `主管/P24-緩速生活操作來源-20261029/dispatch.json` | 3153 | `5f379a2533b09f09da6fa3cfdfaa750423d60ca597dfef4f91aedd122d07a5fa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,supervisor,owner,prompt,authorization,candidateImplementationAuthorized |
| `主管/P24-緩速生活操作來源-20261029/independent-preparation-active-receipt.json` | 1432 | `545bee0caa66520fa264538acd39251ebe481271598af6ef472821d6da46013b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,raw |
| `主管/P24-緩速生活操作來源-20261029/independent-preparation-dispatch.json` | 3076 | `f656e1edf34160a242e97fbf23f2395013245608fe8d022c4b3772c7ffd61d27` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,prompt,utc |
| `主管/P24-緩速生活操作來源-20261029/independent-preparation-root-verification.json` | 527 | `bd4b592f10bf31ce76708a3bb826e447d925bc7e46a9a629b5a0261a79fa0ee2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=verified,artifacts,sources,classes,observations,baselineDifferences |
| `主管/P24-緩速生活操作來源-20261029/queue.json` | 1182 | `8a9d40706487e6776d638ec9ca22097bded3a48c53d9a79f7b43c766c4b07f97` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,writeRoot,status,turnId,cursor |
| `主管/P24-緩速生活操作來源-20261029/root-static-observation.json` | 593 | `6ac4a98c86fba3e17563d4d8fc9c29666b990967228381d7eae62ab3f9a5dc01` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,base,observations,reviewer,productDefectConfirmed,productWritesAuthorized |
| `主管/P24-緩速生活操作來源-20261029/source-report-root-verification.json` | 741 | `8e65a682ed554a1f97eac6fd32b0de8a62e8ff4fa39bf6103aeba197e923cd02` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,verified,artifactFiles,sourceFiles,jarEntries,nativeChunkRecords |
| `主管/P24-自主實機完整流程-20261026/active-server-lifecycle.json` | 94 | `c593f4949090efd29ebbef948613da57e290d5b8ae5e910a3d7430c96b92419c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=directory,previousPid,pid |
| `主管/P24-自主實機完整流程-20261026/authorization.json` | 678 | `0f31da4d258d51f6432542d1a3bbe4c035c1200a54ea4b1ad08282f38beaeda5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,authorization,scope,uiAndLiveAuthorized,waitingForUserStart,productDll |
| `主管/P24-自主實機完整流程-20261026/basic-potion-template-observation.json` | 6207 | `fb8b9ec36e94a912139f90a50f1ade35228eb7af762485bb979cf1333f357954` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/cave-original-route.json` | 433 | `d968af0a49784b88c03e97e39f8192b0073b6ab5584baf4f87662741f314b571` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,source,entry,exit,terrain0,terrain1 |
| `主管/P24-自主實機完整流程-20261026/combined-product-live-plan.json` | 1552 | `98fcca336c0f3580d79774d54d0a82affc28f1174ff81e0888b384250b7845cd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,role,start,steps,constraints |
| `主管/P24-自主實機完整流程-20261026/continuation-20261027-runtime-readonly.json` | 3836 | `76043ef1c82a83d9cba3d21596adeecebee840ec5b8c8a0d99d8570c95b3ff16` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/current-checkpoint.json` | 22479 | `990428ac073f15345e56922f0115b0d7296a2401112310f059481807b2fe6bd1` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/db-after-r2-login-failure.json` | 3839 | `0b0bb9e307a932383140878c8d949d9b9bbf9bbbd1c1e670593485d5169b683c` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/db-after-r3-combat-death.json` | 3837 | `348e86d8bef909b8fcd2ccfb2110ffe9288d8632f73042d0fa26a0e4399d01df` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/db-after-r3-normal-exit.json` | 3839 | `f53ad93e0d486a3153f5de815f19bbc2ebb0fb39bb13b39457eaf339d7bd9e58` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/db-after-r4-invisible-attack.json` | 3837 | `3c39527839d7cf3b7d04a75759da319afde4995e9dc5cfb914df6e50e288c0b9` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/db-after-r4-normal-exit.json` | 3838 | `82ca5dd97cd20b061de644c81d0ece6223f2620e6d05f3934384b74c7e8d64ef` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/db-after-r5-kurt-pursuit.json` | 3836 | `2b3ca8d12531f2a2e230e2d696a3507e9f7b8516688774f5fa91244785226bcc` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/db-after-r5-real-trade.json` | 3838 | `13bb91861f59011a2f57b8d513d65ad0c36b2ce476d616fd62b5472002852e4d` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/db-after-r5-shop-travel.json` | 3838 | `9cc993a9e8107538a0152c859e956e996eff23219550b66d71ac9249837d45eb` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/db-after-r6-relogin-failure.json` | 3838 | `165fd8670db68526b2e81c216513d150d729109d8f289c0ef9c4c8b08adf442b` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/db-after-r7-cave-return.json` | 3838 | `6007ca0605a83d11f1b30ccc37fac3993bac7e27cba5ac219c4f5037904af8cc` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/db-after-r8-resource-limit.json` | 3836 | `814d79cc47cbedff85fc82b92bd4f8f3c2c0486d3f38357bc84fe1bb49cea218` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/db-after-two-login-failures.json` | 3839 | `a0d34b7593f09932f8132016745fa24b2517ef9bcc8dd3ea3061e4b86272922f` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/db-os-before.json` | 929 | `9aef51afc66b7ab6ba9ee9902f5d7594cf57949639f731524ad79f70a5802a7d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=processes,listeners |
| `主管/P24-自主實機完整流程-20261026/db-roles-before.json` | 1777 | `bb8952ecc823b7312ddf2a9bb9c4c64d9b926129712906181dca3509cdda7622` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/flow-coverage.json` | 18098 | `b9ce5865c741a7f3c9e74fed6ce0a7bee76bfbfbe2fbc7ed00b777833f4ce60b` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/gludin-services-readonly.json` | 421 | `2546ad29e4f40285ac4a3c291fe91ab47ba96f9584058aa1ffdf41014ea4c651` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=readOnly,planningOnly,rows |
| `主管/P24-自主實機完整流程-20261026/gludin-warehouse-route-plan.json` | 4441 | `14859e47dc786329164264438c7b8a849e7c0c53874b9ac2a53b6af380a20fc4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=planningOnly,start,goal,steps,terrainSha,path |
| `主管/P24-自主實機完整流程-20261026/guard-1793013397572.json` | 72517 | `b1f6a1352058a59f76c500fb9c55b0d8ec6a8959109561e157150a95134f13cb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/guard-1793013615681.json` | 40789 | `02dcdd70c87dae2bc20ff55c57b787a08d5f91b604693a12cf3d4a0522ea11af` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/guard-1793013895307.json` | 37950 | `f284f94d72571dc58b194e622048f5e67b4a1cdc6928f676585ab43ed1e7f069` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/guard-1793014686800.json` | 34643 | `2c01e0c85fb707dfe278d429ebd83f435a8c2c8e6d2c1f8009c47690e1a4dc1c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/guard-1793014914168.json` | 43372 | `33fdd024028dd7c0aa3544074e1137d3ddeb798b04f65757b9e4a882f268f106` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/guard-1793015460635.json` | 38182 | `0bc86fed2887147271b93f1208e20e88526a163792e4354534d820704d528144` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/guard-1793015526407.json` | 35816 | `f6050ce14d846ccc967fd7a4a1aab8263e2a8c4d39582ccb10866818625a95e9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/guard-1793015711091.json` | 37417 | `831e5f36136962dc5d8a61570251bb16d19521a120b5cf96f1cf8e07322f7d0a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/guard-1793015731805.json` | 38165 | `784fbe93822137476e7980a046887cb5708cdf7efddca12b43dca1a6b4f9dcbb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/guard-1793015752926.json` | 37002 | `f8e3e6938a5f40eed4a64876e7476bd226af1cd620c46358d9987b52b7f107d0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/guard-1793015788141.json` | 37020 | `c29f32d2beda7133b3067d66f13b8da80ac731341b73e204d438dc7580c12455` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/guard-1793015798696.json` | 36977 | `853209d3a9776fc3c1e0002daaa7d732295107bfaf95fa1278934f94ce6ccb1d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/guard-1793015883236.json` | 38157 | `496d34ceaa004c3239c480a5405def92bdce2df70b36e4608a8ad87701533e08` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/guard-1793015941484.json` | 34650 | `e2aab55f97eeb770b376d3f63a4e12704ddb05a21ed5da357c07a152971207ef` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/inn-approach-terrain-plan-r2.json` | 684 | `b9164419fa29da3ea51bf67584e6e9ff9e7ff0c39995ae32629b5b8402adb456` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,planningOnly,start,target,avoidCapturedKurtRadius,steps |
| `主管/P24-自主實機完整流程-20261026/inn-approach-terrain-plan.json` | 345 | `d5fc36e25c54395f3d09b7b842a042fba37b7990c411e882a75f451b6ff515fa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,planningOnly,sourceState,start,target,avoidCapturedKurtRadius |
| `主管/P24-自主實機完整流程-20261026/inn-candidate-preliminary-review.json` | 573 | `90c55e881527ad5a530b1350dfa07965332e12aec7bdd7d4dc177a66ac64ca9f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,files,observations,live,applied |
| `主管/P24-自主實機完整流程-20261026/inn-runtime-config-readonly.json` | 9754 | `c383c0ef12b5cec60b42f5bea21f042a30b40924e617b8513c984f0fc1b78a83` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/land-route-db-observation.json` | 6610 | `2017730bc1de2a8fc78e9033b9ce0281c64b75de47c345568444a140802ec37d` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/map0-basic-potion-shops-readonly.json` | 473 | `79743dc9ea7b0a2ba8f695d8e78f8ddc5004a5c237e1764a80a1a2e01e69399e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=readOnly,rows |
| `主管/P24-自主實機完整流程-20261026/new-game-os-pin.json` | 989 | `2e797e9a8f724669f73a7cd439171824e2206c2664f87379e1b4268419dad3f9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=process,listeners |
| `主管/P24-自主實機完整流程-20261026/next-basic-potion-wiring.json` | 1890 | `b82ee4bd74504e416af7279924579b460ef9b31115f3776b23d13ef2dad6cd94` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,scope,reason,currentSource,required,source |
| `主管/P24-自主實機完整流程-20261026/normal-client-browser-control-launch.json` | 472 | `4373cb9cfa639f648e7f017cd64a4adfeb2320ded23b4892205a1b8a57531daf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,exe,arguments,utc,productSha256,browserControl |
| `主管/P24-自主實機完整流程-20261026/normal-client-r10-launch.json` | 642 | `71e902747bb13761d679ff5c9945663a191ba08407a375b6080fe8c6025af4f2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r10-peerprep-launch.json` | 650 | `6fb91ca599b814606b45566d897faab40fc07cc8ea63e00019802644dc066fd8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r10-recovery2-launch.json` | 651 | `b489d82d63285181da15628152b75a136dd7c58a34348053af79fe7584d0176d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r10-shipapproach2-launch.json` | 655 | `80d916c2ac0965fe106f6080f6674e15d50bfb06a3d38d8d4c349627412e8090` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r10-shipprep-launch.json` | 650 | `582587a7ae5bfe7a892ac86f895b851c4149a199b0cdc85cec0bbdfa1606d1c5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r11-peer-launch.json` | 646 | `08ecf97bcca4ab853a2d918af613c18ca8c5b37921c8bd074cd1593eed325ec8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r11-service-launch.json` | 649 | `485d86524622deea326c1cc78767d5a465f9340b7ba538204849929dc9d59911` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r11-service2-launch.json` | 650 | `ce8a6e81627d9b5d64a3e23b74b59887711f053d4cf56e73605e5e4cca70bf54` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r12-ship-launch.json` | 646 | `899e28bd4b1ffa2dce8980eb77df94f8cae0450a6e05aa0829e30cbbdbb3a378` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r13-inn-approach-main-launch.json` | 659 | `2a156d6782938f9904d3f6342f26871011065f4269c8a7811f2f85e8d1de4112` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r13-rabbit-return-main-launch.json` | 660 | `848b2d7af1ae3581a1159038d38cc941cc94cf1cc29b5153c4a1d98f42eb47c6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r14-inn-main-launch.json` | 650 | `2f6e979954bbeac617590e72b926e8f9a67d4a5f99390d250d500dfdc429785f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r15-inn-denial-main-launch.json` | 657 | `08807505640ed0d4dfe2489d5dabb98174bc6202c56e7a4733f80805b2d06d9b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r16-final-material-launch.json` | 656 | `48d31c0989ae8cd85dd61dc34f3cd6ad3f4d0f043f026f10dc93e141adab618f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r2-launch.json` | 622 | `f32dfa1e68c553c81c51e8519f6e9cfbf306b6e2c2221b6f949ad8ffcf866a0d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r3-launch.json` | 622 | `99b0767de4cc6f9c0b5609bfe65b6001003b525866a05afc79f7d3e72d03c633` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r4-launch.json` | 622 | `be4a802091b8469af6a14be16607bb4cd944e6d1e76a2881181330b9a57073e3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r5-launch.json` | 641 | `e36510749af28ae4ae3c5796fd8f30605a15fe0f2d7c5f1ef080c1e12c21ac71` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r5-peer-launch.json` | 645 | `c47ad97c73971ad3eac9e83772a51a613bc9836af03b650d992d0a8ca530e9e0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r5-peer-lure-launch.json` | 650 | `f5fb4f9b8d135662a6555d1fc535bb5f9627c291db1b8fc9e2426f7d9546aea5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r6-launch.json` | 640 | `b9005d1f60927659d01c31807de81dae783bfe6c62ea51a7653c0b6e9e007f68` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r7-launch.json` | 641 | `249ac8fab437825d22f0684b2e952aafd41d66d0aad4eb70b915af5947f6db0d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r8-launch.json` | 641 | `233933b63528c93302e7780c5cd5740a1e38582a8176bb70014e407b41714d3b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r8-rescue-launch.json` | 647 | `21744b535b7a1b22225c5da6144fec393c978e7dfe2c10e06f41e3a579045eda` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-client-r9-launch.json` | 641 | `506e061b8ae7e6c915b8e688839cb891cb4b301e83cc7db36be6ee01642cf6c9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pid,executable,dllSha256,manifestSha256,arguments |
| `主管/P24-自主實機完整流程-20261026/normal-hunting-source-plan.json` | 15205 | `3d251a760a9db6173435b853177275a999de891f5b210f67ae6fb8c81256e18f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,sha256,monsters |
| `主管/P24-自主實機完整流程-20261026/normal-login-failure.json` | 399529 | `be2b51c9d64c1692833ceb1c7dccdd8d304bf1f6ea3742ce597fb09ee1dd4184` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/normal-login-second-failure.json` | 15967 | `ce7d617865719d307a72c2e5a9d70b5a5f1f859d4c6c890106bfedee9d1fd851` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=attempt,reason,states |
| `主管/P24-自主實機完整流程-20261026/normal-login-second-last-world.json` | 25708 | `9ad38e33964f5187aafbab234602806073a8454f9f8b746e1a07565491112b51` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,data,utc,monotonic,pid |
| `主管/P24-自主實機完整流程-20261026/peer-guard-1793014482030.json` | 22226 | `9491aaebc36793abd5170b1d5ffd6d29a777a8e9310adbfc80405d8c64d765e8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state,reason |
| `主管/P24-自主實機完整流程-20261026/peer-lure-1793014652446.json` | 16344 | `aab5f67ceb39a804ebc2c2e57c0075afe31a78bb42a08b9ed7af8519d17f684d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state,reason |
| `主管/P24-自主實機完整流程-20261026/peer-protect-1793015478703.json` | 21264 | `99e90238ea6c73f9e1c1ca068510860783c0ddd4ed17acda9b1507a13ca92e0c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state,reason |
| `主管/P24-自主實機完整流程-20261026/peer-protect-1793015502771.json` | 21076 | `280384bd2d2842bca9a4a00f2deae502025830e3452284a7c306f2063eea2a44` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state,reason |
| `主管/P24-自主實機完整流程-20261026/peer-protect-1793015925286.json` | 16336 | `40b01877e65468507cc1bf0ab648b833b41d01c9980dca3f22f9949fa74e5035` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state,reason |
| `主管/P24-自主實機完整流程-20261026/pigeon-candidate-root-review.json` | 18380 | `332f5c582d21493bdd10cc2b48ba491b993a260eede50e6551a7c59d0c115f4b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,delivery,sources |
| `主管/P24-自主實機完整流程-20261026/potion-offline-image-review.json` | 766 | `15097df8fb8a8a46d9e8ac0e75d473607d88a16215cbc5535d41e279a6b73044` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,images,observation,live |
| `主管/P24-自主實機完整流程-20261026/r10-after-range-invalidation.json` | 80834 | `d330eafc232c04af64affc486e6532c9b59d3f51b81ba9fa2cf62248678f6358` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r10-candidate-root-review.json` | 23524 | `b3da7fa6a5ae9f7b7b9c8bddb8a78f20e9c946213641696a0503f24e966718cb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,schema,dll,files,sourceReview |
| `主管/P24-自主實機完整流程-20261026/r10-harbor-arrived-state.json` | 31577 | `85c714ef0d4c09da9419aa5379fd98c3ffecb72e6ab275af6058cb7b2e215dfe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r10-heal-preserves-dialogue-live.json` | 246372 | `f8fb05163be99cad2ac3d509ab5eef901c536024d884243b973b29c3a2eac0a4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,during,after |
| `主管/P24-自主實機完整流程-20261026/r10-inn-close-observation.json` | 175348 | `6efe619d4c2c7736863ad7a2ef6fd062f39319e59b9d6c5a3ec51aa58a1eb8ae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r10-live-plan.json` | 1006 | `426b6ac45876ea437c9c59010230645bb7ad79c9db275cd20215d6ffffaa19a8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=created,status,role,start,purpose,rules |
| `主管/P24-自主實機完整流程-20261026/r10-peer-normal-reposition.json` | 2522 | `c10a73e27ca086bda72436c44c3633d7de1c6389c5d2b664d75b1e2c58b368ac` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(36);keys=x,y,hp,mp |
| `主管/P24-自主實機完整流程-20261026/r10-peer-village-state.json` | 27903 | `afb2eb159577a6a2ad52ecc69ccfb59b161af98f4f2b254fba4f05e8e855d146` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r10-pigeon-normal-action.json` | 220249 | `89c0a3533d636e04fabdc1f032b2e9531dc0f10d3f5e054254e49469f621375a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,timeline,after |
| `主管/P24-自主實機完整流程-20261026/r10-potion-options-still-actionable.json` | 586726 | `a3ff0112c2aaeaee4d333a94e0f8556725712cdeac6fffe6d78f7e5193a033b8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,afterPotion,afterOption |
| `主管/P24-自主實機完整流程-20261026/r10-potion-preserves-dialogue-live.json` | 251122 | `fb0c1e2e990bcd9e1ab4e9db1c4c09b357cc2c783cf892df87175f0033af3fa1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,during,after |
| `主管/P24-自主實機完整流程-20261026/r10-real-terrain-rejection.json` | 167061 | `1888cb65496cb87425d024e4b77b78dbc729bcc20f04bd94a3806607906db4ef` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,after |
| `主管/P24-自主實機完整流程-20261026/r10-review.json` | 2608 | `3ef87eaeaa4bb22cf4128bfac68d119ced71d7792cf6b98e2509b7a0f1c35796` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,manifestSha256,files |
| `主管/P24-自主實機完整流程-20261026/r10-service-recovery2.json` | 3796 | `30efcc807755cac371438929ef7dea1d0f1735c10fe8f6f4fa75028e63279751` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(14);keys=hp,mp,items |
| `主管/P24-自主實機完整流程-20261026/r10-ship-ticket-approach.json` | 69905 | `9dcb40525310f9012cc2b44e382400ffab0bc2d329d468f6f8c5c5b1fbae9f3e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=route,guard,state |
| `主管/P24-自主實機完整流程-20261026/r10-shipapproach2-guard.json` | 949 | `d6c1528f0df5d41462d35f72aaa3da51ee500becf64a62b355805fb8751cbbb8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(1);keys=utc,reason,x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r10-shipapproach2-outcome.json` | 902 | `736fcc72e059d166eb834372be09cb365d7b28459b5ad49c573de7f7b31dc1bd` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r10-shipapproach2-retreat.json` | 3779 | `303180add62b79c685ec99b0ff70fad33e4f06f03f65d4e0f677441185b495da` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(48);keys=state,x,y,hp |
| `主管/P24-自主實機完整流程-20261026/r10-shipapproach2-south-state.json` | 32098 | `4f6a653950b2465a373fd9b4937765bc1a5d2c8bb37b5aa0f9410657234081de` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r10-shipprep-guard-1793026280463.json` | 54008 | `993fcf15142c38dc094415868e47344e80d863aefad6b05764a6f699d8889f59` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r11-after-expiry-offline-buffs.json` | 39 | `7ff54486687d979ed0fb2c9ed78308efcb534aea005af85b5c265230c9ae8d47` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=readOnly,rows |
| `主管/P24-自主實機完整流程-20261026/r11-approach-prep.json` | 2632 | `a961efb5ec56aab9a617062978e180c81c0ab3a81fca2dacb757f71100280cb5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,main,peer |
| `主管/P24-自主實機完整流程-20261026/r11-boarding-walk.json` | 398 | `4ecb373ccc1f47b4fd74a8232ae1f8292bc625521586451e0d4d9d4ace0fa1b6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(4);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r11-boarding-walk2.json` | 1651 | `cc8cc260a41f2480cf7c8a3413922049cddc2b351005b088cc1b900b7567ccd4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(14);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r11-coop-approach-attempt.json` | 325 | `48f7aad68c8446912ef61355721d0036a60fda67df8740b069cb2eeacd414bdb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,rows,error |
| `主管/P24-自主實機完整流程-20261026/r11-coop-cross3.json` | 5819 | `65ef3dbc867feeb3888d790d568bedcca0c619c77b96b161efcfbb8e7f704570` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=rows,guards |
| `主管/P24-自主實機完整流程-20261026/r11-coop-session2.json` | 3000 | `4064fb7cd9c31b28611c67dc07b6b95fadbf189b8ee4f06b18ed7d9f79575ba1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=rows,guards |
| `主管/P24-自主實機完整流程-20261026/r11-guard-session2.json` | 1902 | `0c5b83d59353226ca7dbe31e5024f03dcca9ec88cca2633820d78bd0cc4ee46b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(8);keys=utc,role,x,y,hp,distance |
| `主管/P24-自主實機完整流程-20261026/r11-guard-session3.json` | 710 | `4e62cf837613b1397f42d6ed26f1fe85131ef4cab121dfb5745e5dff368226b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(3);keys=utc,role,x,y,hp,distance |
| `主管/P24-自主實機完整流程-20261026/r11-harbor-dialog.json` | 1688 | `0af75e557d1a9084cfd4aeb536cd79d34bf4932d47e3534cefa0312e05d3a6ae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=canTalk,model,notice,options,rows,targets |
| `主管/P24-自主實機完整流程-20261026/r11-harbor-east-approach.json` | 519 | `75e64858b3a6c078cc0b1bed5b9f02be3478901ab4b471ed4ad0ff70d0f22ac6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(5);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r11-harbor-east-approach2.json` | 640 | `86ad32b6f4100b7ec7c5bf73cd8e794876bed27e2440314afe601ac022b82835` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(6);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r11-harbor-ticket-quote.json` | 1159 | `90275b52e103979726e02bdea4f34aea20e0beacac057e70e83606b5a232abd8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=canTalk,model,notice,options,rows,targets |
| `主管/P24-自主實機完整流程-20261026/r11-haste-expiry-relogin.json` | 5354 | `ab66efdeea388dfa570c17b891d7a8d78988f6dd55f1d1b890777827a2c7a907` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=moves,relogin |
| `主管/P24-自主實機完整流程-20261026/r11-haste-expiry-state.json` | 23785 | `e38e7c779e8e2d3bc4eb736a57e154c8889558d98934b54e91fd253ee15a29a9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r11-haste-harbor-walk1.json` | 1104 | `ae50d8b731d9a490e4ce706b9e35ccf6fb7257303413766203a53b5465e403b4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(12);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r11-haste-relogin.json` | 2950 | `91069b7f4e72e4f18c0996f8f7702694a7a36588920b0afc4938e3b0a97354a1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=state,x,y,actor,movement,items |
| `主管/P24-自主實機完整流程-20261026/r11-haste-use.json` | 5874 | `f41873ba9eef35ec40f3f8fde7eb67587a8a6e1a536890d2232e9e40acf599a1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,after |
| `主管/P24-自主實機完整流程-20261026/r11-live-guard.json` | 439 | `4584e6dbc263e9d745d8882043bedbe97c9acabf610b3cc881c6e8ac043388bd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=utc,role,x,y,hp,threats |
| `主管/P24-自主實機完整流程-20261026/r11-lure-attempts.json` | 7145 | `9cda39b081b26438bf5d163d3aa980aaca6552316f6778ebaf84c6420fe41703` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,walk,coop,guards |
| `主管/P24-自主實機完整流程-20261026/r11-normal-root-review.json` | 384 | `39cea348e4bef38848b13e6cac57ea7f762bd4dc36ca22d466ea13e202c93d93` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,manifestSha256,dllSha256,productFiles,assetFiles |
| `主管/P24-自主實機完整流程-20261026/r11-normal-white-potion-transfer.json` | 4320 | `0e7d1de849ae1ccda4ab530e5e14a99625f9d0d3e9342dc14d8bc6c08e6fe428` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,main,peer |
| `主管/P24-自主實機完整流程-20261026/r11-pandora-purchase.json` | 1748 | `caf31dd8034d9f4aeb588e759c2f220d8adbe7dfc5f752ecbda30768707ea548` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=state,inventory,notice |
| `主管/P24-自主實機完整流程-20261026/r11-pandora-quote.json` | 8946 | `338b4a757d79f4725121d68231fcd3d7c460796ae2edb7daf22d0c9eacf4ee0d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=canTalk,model,notice,options,rows,targets |
| `主管/P24-自主實機完整流程-20261026/r11-peer-healing.json` | 1064 | `d93f8ac576de00f86d917a257e383dc6f6c6f8a7d41b939d82df5c1f5dd87e7e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(18);keys=before,after,mp |
| `主管/P24-自主實機完整流程-20261026/r11-peer-white-recovery.json` | 421 | `9402cbd1773ba1d734dc526e531895ce0c010d60efcfc9e6cab0dd3a44d09617` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=hp,items |
| `主管/P24-自主實機完整流程-20261026/r11-pier-saved-buffs-readonly.json` | 217 | `0f9c6f156e018de715be77a07dfd29db6fa7f423e434ff6fbbc4ba71cbf914aa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=readOnly,scope,rows |
| `主管/P24-自主實機完整流程-20261026/r11-pier-wait-start.json` | 23556 | `008000c1dccc966f0f598d0acf9666e8582a148681d24891281d5be607b795d5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r11-red-first-use.json` | 9166 | `a015370b308d259f0930f14dd4ee26907007f86bea5c4356ab139cfae1e51c77` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,after |
| `主管/P24-自主實機完整流程-20261026/r11-red-recovery.json` | 1066 | `5faa81cabd105186efa5fccbcc678039dd410d69362c81c9fe089dce4addf3e9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(28);keys=hp,red |
| `主管/P24-自主實機完整流程-20261026/r11-service-login.json` | 32900 | `6f749d68a1ea530ae027643f9aca0e098db05a7a6b4628037f5f05455e15ab2b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r11-service-self-heals.json` | 224 | `ca57f07d613f2fa93a5983a9e1d6328a22e0558486e5514efc2d0bfcf921d71b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(6);keys=hp,mp |
| `主管/P24-自主實機完整流程-20261026/r11-service-white-recovery.json` | 2475 | `92b23a606bdf8a2ff0bba00baa1f3e0f6abe85acf49c085c62c2dcefb9a9e44d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=before,after,items |
| `主管/P24-自主實機完整流程-20261026/r11-shop-arrival-walk.json` | 1162 | `9ac47f89ef5e2e8d18d8e7834f2c4fbd2ecad6b6a90a304aa0996bd0e20a25b6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(12);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r11-short-cross.json` | 9450 | `c433ab9262b1522564cbae8af10cb58880f859a76fabebbc183a1ab78f1aef7a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=rows,guards |
| `主管/P24-自主實機完整流程-20261026/r11-south-approach.json` | 3589 | `7be947c1138ac750c357df664a5ab28eaed10dd4cccc1cae512f34297a993ca2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(49);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r11-ticket-purchase.json` | 2015 | `04920f2ef6e35456f84d4625ad54abf6b9fe1423638af9a402716f9193d40741` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=inventory,notice |
| `主管/P24-自主實機完整流程-20261026/r12-boarding-result.json` | 61206 | `7746f4c64cf5f34ce2abd61d547a5e1de8188bbc4c01027c4c8b5aabf778417d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r12-final-offline-service-db.json` | 6044 | `59e8654a8b4de6882d3f318111e29c9200e57687916f27d82d0047f1271aa9d1` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r12-normal-root-review.json` | 405 | `63ea0b7ae4688d7f627930664873f0c0fb07fd1cfef8e3f0880610185e5d744a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,productFiles,assets,unchangedSource,assetDelta |
| `主管/P24-自主實機完整流程-20261026/r12-outbound-arrival-state.json` | 27227 | `58d6474de78a9e188976d104036fb0ce74425d7933ac6832c711fa90a12c49dd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r12-pre-rabbit-upgrade.json` | 62 | `c3d6025ed911a9b6df0c2ead08da6fd32e55cb9667db366b089b7c48870f5920` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,login |
| `主管/P24-自主實機完整流程-20261026/r12-reboard-after-relogin.json` | 61228 | `36ab8ed273a0e958f17438015ae30048eae23f64ae2659c9bcfab76a7048b33d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r12-return-ticket-purchase.json` | 3138 | `98400fa8ad133c6d566453404391b36cb9b4da74b26df25679dec43e1051f7f8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,inventory,interaction |
| `主管/P24-自主實機完整流程-20261026/r12-ship-deck-walk.json` | 220 | `063b2991058f666343444c0f5c0cfee051e65043bfef190e0b4ddefc61eaa17f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(3);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r12-ship-relogin.json` | 23492 | `def5be99d23a23bbe545a97eadd03c544244adf2b0818d5bde98b749d8f0a07a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r12-ticket-icon-live.json` | 390 | `af1d00758d4e81e1a8b81e4b504e26e3968c2a6c4258e81711acf2a736584732` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=state,x,y,map,clock,ticket |
| `主管/P24-自主實機完整流程-20261026/r12-to-return-harbor.json` | 4338 | `a7c81f67d7f04f683bab6d4d89226d3d3abebcc34fd8c13f9bc9f2dbbbfa2b3b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(61);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r12-to-return-pier.json` | 1143 | `9fb815ea9577dc56e7f1b8216a5b9f6ee32cbee53a64f500e1de2769facdcd68` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(16);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r12-to-warehouse-walk.json` | 4267 | `5a7881e51659b0cc7b1ff80a80ed6b0990565ba780eddd69b033af2b8af3255d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(60);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r12-voyage-event-1.json` | 61155 | `9f5125da2cc78ffedbbebd91e5fe4da51d8371377ae1d42506372995a5eddd54` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r12-voyage-event-2.json` | 23492 | `def5be99d23a23bbe545a97eadd03c544244adf2b0818d5bde98b749d8f0a07a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r12-voyage-event-3.json` | 62833 | `25e5dfac2516076ca4d0ca05e72bba86fcc9b301a290583e7b74f29242ce53b1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r12-voyage-event-4.json` | 61165 | `cd3a4dbceced344738fdb6f79be8a2a375b584adc66bd0ac5eeb8daa70b81d89` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r12-voyage-event-5.json` | 20286 | `d6293225ed9c84a9d8b53475e99a7a51f1d4d2c3dff36b13a90408a251122716` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r12-warehouse-after-deposit-db.json` | 6419 | `69b5d57ac08d862618b62aea1fc4d5e4e4a1639c816d74da0f32f930e5e7fca7` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r12-warehouse-deposit.json` | 3092 | `76ac1c800beadd08e16960f056d564ddfd25dea0517e6d5a017e5611d4dc63b0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,interaction,inventory |
| `主管/P24-自主實機完整流程-20261026/r12-warehouse-empty-final.json` | 3018 | `96e2e35e6819c3a60aba36dafbb3a5035a035273b600f93a040afd9b0505de9c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,interaction,inventory |
| `主管/P24-自主實機完整流程-20261026/r12-warehouse-live-reconciliation.json` | 1293 | `938e4f53d60a6cdcbde97af2e0a2ac8f1eb60f61a3bb085891927e44f5ced2dd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,revision,method,deposit10,reloginPreserved10 |
| `主管/P24-自主實機完整流程-20261026/r12-warehouse-partial-db.json` | 6418 | `0f35e37a5e815b71bb102b4350b3082d888ae3a842f6882b3a0034118b3f37ea` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r12-warehouse-partial-settled.json` | 3363 | `dfd9e66a3f91de9cc697a858fec070689ded8217cc1c97b91e09415ec10d7f4b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,interaction,inventory |
| `主管/P24-自主實機完整流程-20261026/r12-warehouse-partial-withdraw.json` | 3092 | `8b37ebebac9d07ee24fe292f589272ab606a6e9cfffe78cc1c7490e7e63209c3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,interaction,inventory |
| `主管/P24-自主實機完整流程-20261026/r12-warehouse-relogin-list.json` | 36687 | `78554aef08d00c844a14b5f381410d210f76f0dd2a2cd7cb0e2736ebaee66661` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,interaction,world |
| `主管/P24-自主實機完整流程-20261026/r13-after-return-relogin.json` | 25094 | `194db840a846ef7f9a74775a51e136fa008845547df3456cbb18b34cb193ae0b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,state |
| `主管/P24-自主實機完整流程-20261026/r13-existing-eight-live-audit.json` | 3836 | `560110f0f01a32382c2b61551d714007df4df9ed1a3d15a8f1354652164833ce` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r13-inn-approach-final-offline-db.json` | 5770 | `18bffa9c936c89b64e0372ef4d4fa8c6689e7993a7ad19e0e19ccd5325803f2a` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r13-inn-approach-guard.json` | 946 | `f22d0fe212cade7e3401f189537a81c3a132ff817297c75fd77f215e9118408a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(4);keys=utc,role,x,y,hp,distance |
| `主管/P24-自主實機完整流程-20261026/r13-inn-arrived-state.json` | 140938 | `26c6e49364ba7a721bd5f36f68a636583845704cc54e5f4f1469e61745d110df` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,state |
| `主管/P24-自主實機完整流程-20261026/r13-normal-relogin.json` | 27482 | `2b2b30997e44777788224b23b68090f0b9ce6f89353af829068af4b339417306` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,state |
| `主管/P24-自主實機完整流程-20261026/r13-normal-root-review.json` | 436 | `82bcd44417c1e8fc743b4bf830e414d6b4c4fbf4d70289f860b6f32618204f29` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,productFiles,assets,unchangedSource,assetDelta |
| `主管/P24-自主實機完整流程-20261026/r13-rabbit-live-review.json` | 590 | `07a6ecd9a0a39d4a0fc94bd436d47f56db71782b5126fa94d37422560855a2ac` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,status,revision,actualNormalUi,evidence,observed |
| `主管/P24-自主實機完整流程-20261026/r13-rabbit-live-state.json` | 21993 | `ef137d405d41cba3ac7d6a49c22ffbe9e864b81bd0a40442af633d1d361d4ecf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,world |
| `主管/P24-自主實機完整流程-20261026/r13-rabbit-live-state2.json` | 21993 | `e94f793c7654ca04bfb41507e345d56294705d5dd9771db42808187a95da0226` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,world |
| `主管/P24-自主實機完整流程-20261026/r13-rabbit-search-walk.json` | 1359 | `a935cb0c739dbe4390741e747ee98d8c18d93c7ba44cbb26db8bd8c0138cde88` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(19);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r13-reserved-four-preservation.json` | 11107 | `7d8aebe544a2aa672d659d58cea651110a48dfeda877dfa9c1d4f9e81dd6b3cf` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r13-return-arrival-state.json` | 26294 | `cbda7318060125565495c34dc63c33cb3d874d6f0eb3b0513d021fa1c17901d5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,state |
| `主管/P24-自主實機完整流程-20261026/r13-return-boarding-observation-notes.json` | 675 | `2da6e2f18476c8a83748d6f82fe19f8355ed8355103d9fd9b9d69a7d07c9d682` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,map6Loaded,boardedAt,originalPortal,ticketObject,capture |
| `主管/P24-自主實機完整流程-20261026/r13-return-boarding-state.json` | 23552 | `cf78c9ed2771f05ad154292eba2f2b517477e1a1fd6c2bfcf603bb03f5bb9433` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,state |
| `主管/P24-自主實機完整流程-20261026/r13-return-closed-window-step.json` | 2999 | `bd0830eae0639c23afe5a9095642124e6b632e92fb7f6a974f1b1bcf91a22da4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,map,x,y,ship,items |
| `主管/P24-自主實機完整流程-20261026/r13-return-deck-walk.json` | 370 | `d2b91573f7a98bbb550195dfb0aaed4aacc5c122b364cc32a4a63568fa20fc4d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(5);keys=stop,x,y,hp |
| `主管/P24-自主實機完整流程-20261026/r13-return-event-1.json` | 854 | `281c81111d730c428269f14de4a4f7128ebfd8e750c1144a4acceafe5267411c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,login,map,x,y,hp |
| `主管/P24-自主實機完整流程-20261026/r13-return-event-10.json` | 1018 | `a2ea808d923068dd3322a29abeed57e1e0033354cc05212ca6b5174be8e597ad` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,login,map,x,y,hp |
| `主管/P24-自主實機完整流程-20261026/r13-return-event-2.json` | 926 | `4b43cdd2aec3b71dd189948f6aea4781c5ca0a4a4958c7e114b06b877c132acd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,login,map,x,y,hp |
| `主管/P24-自主實機完整流程-20261026/r13-return-event-3.json` | 927 | `78e98ee234a9c4f70702b9cb78acffd24bb66453bb1551f5967fc75143200262` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,login,map,x,y,hp |
| `主管/P24-自主實機完整流程-20261026/r13-return-event-4.json` | 297 | `f9c3a920482683bfa00c8b3678929fa3fbf6d5f617b0667c066f28e27a4aa002` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,login,map,ship,items |
| `主管/P24-自主實機完整流程-20261026/r13-return-event-5.json` | 860 | `22072cb0f8a81adf8f3ff669814801cd4cd208ed8ce2b94f53097b0685c23ab7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,login,map,x,y,hp |
| `主管/P24-自主實機完整流程-20261026/r13-return-event-6.json` | 854 | `fc05e9c018f58e8413e5c7f232aa2d0c7d6bb5ff0d91f42c99d2f6386aa8c912` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,login,map,x,y,hp |
| `主管/P24-自主實機完整流程-20261026/r13-return-event-7.json` | 933 | `6e20ed3741719fa5a67f7065ef282941fcd502cfb214e98dfee98d30b0bd7816` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,login,map,x,y,hp |
| `主管/P24-自主實機完整流程-20261026/r13-return-event-8.json` | 927 | `af440732e0e257d1c4dddb790a322030f5a403c7f9850f4972c140b5b5a8fc9e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,login,map,x,y,hp |
| `主管/P24-自主實機完整流程-20261026/r13-return-event-9.json` | 918 | `e98ead08f9fba0cbeb4f11b9de38cfbf1dd4586a8c1e42ae71d46b1a6edf2b38` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,login,map,x,y,hp |
| `主管/P24-自主實機完整流程-20261026/r13-return-reboard-after-relogin.json` | 23358 | `772a4771f85b6a05add42ff0a7d20a137078703bacbfe6900e3c0a6d4736cdec` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,state |
| `主管/P24-自主實機完整流程-20261026/r13-return-ship-relogin.json` | 27494 | `e9779cb0b92c188f58a309527de891b0774bbc7089e4c7394f578fda69f5ae79` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=at,state |
| `主管/P24-自主實機完整流程-20261026/r13-return-to-pier.json` | 2137 | `58f3cfb9f150395aa872b69a5ae1b2fb14e1eb5f55b4f1069b889f2d85d65438` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(30);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r13-roundtrip-final-offline-db.json` | 5904 | `0b6e3d2d8e80c6a3816f0fd9bf43b165496b4c4ae41e7910d369e8d340ff3aa5` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r13-to-inn-walk.json` | 5348 | `5ca64fec83df1651f1211a9e5407a5f67e0cac05ad812e2e7dcacba2444bb158` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(57);keys=stop,x,y,hp |
| `主管/P24-自主實機完整流程-20261026/r14-existing-eight-offline.json` | 3836 | `1ebede4258145b0a2ef45e3a5a60baf9fc8a730426fad6a7656f36b9f0f7fa9d` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r14-hall-after-relogin-db.json` | 5921 | `479a03ba808a17c5270a44d2f0171631e6247eed5d4a2a41b0a54af97663012d` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r14-inn-dialogue-state.json` | 128192 | `55a1e39c9fdf65a90300506290eb98b66eaffca11d11b1c748e4478ab77c0fa6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-inn-final-offline-db.json` | 6069 | `276adbbc87b9d7f9ae3023df5ce2258d9ad8be08fe14543abb82076d3b59aea5` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r14-inn-getback-restart-readonly.json` | 4664 | `30aaf1c18cf1caaf0ce2a3e7febb191a89d74ea63387226b0b1427712c685c7b` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r14-inn-hall-enter-state.json` | 18839 | `fb774b2e5acb1f8740f7696ece5a6a63a0c278d7aa47eae516bb9a8cc19bfc08` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-inn-hall-exit-state.json` | 121344 | `fac525f1d187a547de8ddbc877dabe3838214a772ec4a058cf02df3203c9e71f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-inn-hall-loaded-state.json` | 21341 | `41e1e4be3809a7782145eb6e53d6833e6c925df1c4933cc926928b0767863277` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-inn-hall-purchase-state.json` | 129348 | `73e1ef35c2a5ffb141fe095b816aaccff82cb2507cb668f4174b0f27d8d2132b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-inn-hall-relogin-state.json` | 121239 | `15686e6cdc64ddea5e6487340c29794a2d73be011866df1137c5c17c3a6f66d9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-inn-hall-walk.json` | 575 | `52c7c48b94c01cc30c90f56fd7c04b979510b3ebc25d47bbe4fee679ab98265f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(8);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r14-inn-live-reconciliation.json` | 1345 | `e8326140eb042443e58fa384bdf99f5e2b47b02b5b79d6268f8ce18ceb21ad17` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,normalProduct,live,hallMap,roomMap |
| `主管/P24-自主實機完整流程-20261026/r14-inn-no-key-settled-state.json` | 127243 | `bcd164466bcc03eedac31bad102b0fc8e766c5ee2170ee87af0ff9a98aedb7e3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-inn-no-key-state.json` | 127104 | `8ba505a30b67842e5f138acfd6fb2dbf18e250f7de2c06d645fcf43e1c8a94f7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-inn-return-timeout-state.json` | 127244 | `ed48dafbc288a0896afb2848fe61e76138c16056c73534f0b401ce73f7af74f5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-inn-room-enter-state.json` | 22512 | `b5025d5131bb6078d17b0947e5b87cea8ed743a1ef7d2070ab118cfa182e3b58` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-inn-room-exit-state.json` | 122581 | `0f9c21141cd4bdfe82a7e1ac9be958f060f4da32734daf00bd4cdb51e0f610b5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-inn-room-purchase-state.json` | 124960 | `f08b76c780a2eda484648ec63395953c6aa4dc7f87205335d18e8094a2d8904d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-inn-room-relogin-state.json` | 125751 | `d4d74e1bb14753c7cf3ece0cdfcd6e58a18876ee07c14602108325175dc08394` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-inn-room-walk.json` | 362 | `7728737522eb4b77cb6f2ee8dd15a2c5cbb359a5aac1cc3ff847cc2e7116e9fc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(5);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r14-live-guard.json` | 2 | `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P24-自主實機完整流程-20261026/r14-login-state.json` | 126098 | `ec83e7b541e7cfa1f7dd1c30c8077a83347f1933b6bda7192865846392d49418` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-normal-root-review.json` | 497 | `54e7825f5eccb011da2631d87e93ada3eb1e3f3bbc5100f9ee265d742eb182ae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,productFiles,assets,sourceWebRows,onlyChangedSource |
| `主管/P24-自主實機完整流程-20261026/r14-peer-at-inn-state.json` | 123380 | `6e35abcf057df93394ff6839ea579aced74e3b6b6645d83621c1f16b9e37c59e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r14-peer-inn-guard.json` | 2 | `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P24-自主實機完整流程-20261026/r14-peer-to-inn-walk.json` | 1285 | `9426eaee771973173d8757cde9c39827ee726269225052e9386215bf5d1a2fa8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(18);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r15-final-offline-db.json` | 6069 | `8907ed2f54625c12fadb13d11335de1bcc7dacf6581bf7207d22dacb4b2e5944` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r15-inn-live-reconciliation.json` | 1271 | `5ddb991eadb18131151e32dac341606d1403eabbcac0cb9876fe22107f6e95aa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,live,product,noKeyReply,observedMs |
| `主管/P24-自主實機完整流程-20261026/r15-inn-no-key-after-deadline-state.json` | 121243 | `ca0a443c24d5686e039938a7ecf9d918e3260680fa6017c0c16e3a0764bec8af` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r15-inn-no-key-state.json` | 121385 | `2d2bbd405ca551b4d5444a559f25e0c10b24805a870a864bdcd5ec960d2bd032` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r15-inn-no-key-timing.json` | 60 | `5e0e8dd12751a4f53b0e1fcaca083d8e0de672a1aff465edef220ccad843add9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,observedMs |
| `主管/P24-自主實機完整流程-20261026/r15-inn-no-money-state.json` | 121440 | `ab42c1b438d85390ec15618865283bb5b9b73330cc469db36c37bc956f6fb36e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r15-live-guard.json` | 2 | `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P24-自主實機完整流程-20261026/r15-normal-root-review.json` | 397 | `851f867ffa2a32ed738015a4e3e6b311a76bdd69eb10f0efe70c3653af699049` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,productFiles,unchangedSourceWebAssetFiles,onlyChangedSource,manifest |
| `主管/P24-自主實機完整流程-20261026/r15-peer-login-state.json` | 121933 | `b32a504f5e2e2487b36f30baec3d42a927bb3192af892d49b567319a782ef232` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r15-service-final-relogin-state.json` | 122397 | `ca69a78bbb26e7578dd7479dd8028ea040ceaaeee3270b454a78c7480c35a7f1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r15-service-live-guard.json` | 2 | `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P24-自主實機完整流程-20261026/r15-service-login-state.json` | 122478 | `11f90afaa8929fbd1f592efc366a99656764ba8d6564eb67c2c789d297df67b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r15-service-room-enter-state.json` | 21601 | `12d0ccc5c51adfe1fccdfcfb1f2458ddede6e36f46396bda7ad41b5073ab5de2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r15-service-room-exit-state.json` | 122471 | `51be6a7a46be2debf362ffe34793c4342a3493c4ea4f7fc8fdf12750fd55d2e4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r16-final-offline-db.json` | 6069 | `4787bd78367897ff563e53d7a24e2589e581f196d69031ca1f321c8e58eaa81a` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r16-inn-insufficient-gold-state.json` | 121376 | `784db4d51b9f1cfd70986742d3f3ec0d3bb954251ea28a7306b6ab07e092812f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r16-inn-insufficient-gold-timing.json` | 60 | `6d41acf5ea7f0b4855537a3f68eb1905c5df9bbd57598a75e60f6e74409552b2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,observedMs |
| `主管/P24-自主實機完整流程-20261026/r16-inn-no-key-state.json` | 121388 | `6be0ea88afd0ffa98ebbd117cfeff44cf2d6baab2106b6a70757c055255c45b6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r16-live-result.json` | 1882 | `456e38ab6859286e70eade5fe2c67ed9c040aea3f94dc0cd583aa86dfabfe7da` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r16-normal-root-review.json` | 405 | `aa49cdb48e61b0353569a10ac1e87dc7fa15ab766c9a40be9cce76e59e3fc928` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,productFiles,unchangedSourceWebAssetFiles,onlyChangedSource,manifest |
| `主管/P24-自主實機完整流程-20261026/r16-peer-guard.json` | 2 | `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P24-自主實機完整流程-20261026/r16-peer-login-state.json` | 123015 | `7e8597d6fcb74577f7f1b78d4f4ebe939d97c9823419e11a0992ba8f40c845ba` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r16-reserved-four-preservation.json` | 11096 | `cad772d4cc63302632e22ebda13f3e8e125dd01d8768e6139386127eb656bcea` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r16-room-walk.json` | 144 | `c3a84917b140809d3c2f663b2e8738398a61983f3fdaf4e058165bd34ef1a7c6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(2);keys=x,y,hp,kurt |
| `主管/P24-自主實機完整流程-20261026/r16-service-final-relogin-state.json` | 123324 | `09fa78d2a35cfeaad21c8ffa4bad57ded1d2b1f76d75f24c2400a45e88f6a723` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r16-service-guard.json` | 2 | `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P24-自主實機完整流程-20261026/r16-service-login-state.json` | 125671 | `c329bcc05ce3f52e9592a35730fa3e026f4edfaef08f0668bf38bbcb19467cc3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r16-service-room-enter-state.json` | 21552 | `bafde1d1ff05be100df956325147f591cb6f03afa8dd9aae57439508a3b9266e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r16-service-room-exit-state.json` | 122528 | `92be95e152dec11df03c554e1184121125fcd2e3734cc675ff79f167d7e197af` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r16-ui-closed.json` | 121 | `3e56526dccb8225f7816e9b523f0a41c51b4d0fe6bb64e204fa07e595acafc1f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,windows,guardTimersStopped,normalCancelBeforeClose |
| `主管/P24-自主實機完整流程-20261026/r17-after-native-keys-offline-db.json` | 5770 | `39a6f4ef6f3ccaf46b9bbc83f3415712a58453dd5f0d1a3f4ca15047fe3a20b8` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r17-final-offline-db.json` | 5770 | `a04b52a43dac7ff8ebbf652e78422614c8d947957aa9dbdc78e2b75ac69669e9` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r17-pre-integration-db.json` | 5770 | `44f49dcc6da4d795a7c27c5fd282cad0d68ed2102d0241f8aca9862db160e325` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r17-pre-integration-runtime.json` | 3836 | `8777d1653f06eaf372cb632ee5ba7a495322a72807bd9f7f4a58fb0fb152bc73` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r18-after-boss-retreat-db.json` | 5831 | `c795a3758445628edf59a190f01594a075edd915b0cd150982cb4a86301db9c5` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r18-after-first-combat-cancel-db.json` | 5770 | `87fee413dcde11e14f91c9dd34fcdf8d980f5681386b7638fa36e35cfc3b0333` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r18-after-learning-and-boss-recovery-db.json` | 5832 | `c6acc1f903ea431f73709cf5478b61ccf948b7c6ac36ccc2397e5ea0f55d7e0d` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r18-after-learning-refusal-db.json` | 5770 | `5eac2c204d262a810a924d476cd51c12b6e0add908dc6c19d3f3cd6442b6628f` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r18-after-route-ambush-db.json` | 5770 | `e3c840eea4f4b4b64d7ea3420371aa879b0df20e64e2fe9027a80032381ad7d8` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r18-after-supply-purchase-db.json` | 6586 | `ea5096815436735751663cc89fce4c01d2c3f87f9720779bee2b6a8324912ba4` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r18-after-west-retreat-db.json` | 5832 | `dcb0e4c236a7c0c2cdefa502783576475c1379bea755eb4a3fa1c17edeb36c8b` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r18-all-eight-closed-round.json` | 3836 | `9cbd4c3046159901def62a3fee2461caf49ae171f69728bd5ccf6d23fc402fcd` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r18-before-final-flow-db.json` | 5770 | `5ba3784163faa34e502b2176a91cb4db0fbcbcaa0ea809f45c479d15b53cabad` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r18-closed-round-post-supply-db.json` | 6584 | `f9434da2a6e876624563fbcbcd8cbe441ed7656cca761b36800cec17506ee87a` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r18-reserved-four-preservation.json` | 11096 | `95b5c14ddacae9a3caebc1798f8ba69dc6144700602d6e840fb793460e7a3696` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r19-after-direct-trades-db.json` | 6596 | `1275ea4f1606ba6e61d5819967627d34ed37e8b49b1021bf9cdf7d7c364c2784` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r19-after-direct-trades-eight.json` | 3836 | `b2f452221894fe3d37ed2f5742bc553ce045edd7b9f785cbf468009ad829365b` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r19-after-first-supplies-db.json` | 6584 | `06db4e12939c775654af380f863267cd8c628152eb7fe578604fa6e1f46cac6f` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r19-before-all-eight.json` | 3836 | `8bd52f77dfd256f0cf6663bae9fa66f60d3b809ab14d91fc14cd054f2895d2e8` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r19-before-final-flow-db.json` | 6584 | `91c8afa96696f3a7f4e98d5d39a4b31b4f5dea3209125b393fea6833b3a7b496` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r19-reserved-four-preservation.json` | 11096 | `729efde046ae9520ad8a9b7f2d62f0a29d1a2a29dee11eed2c54bbce364e8a3e` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r2-review.json` | 3478 | `ec0d44a6e2dc51313c0be2151252a4f395ee45b3440a9348db285d0d7dd98319` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,snapshotFiles,workerResult,reviewFinding,fullUi |
| `主管/P24-自主實機完整流程-20261026/r2-world-decode-failure.json` | 8508 | `fd6474f25b2963729ee3e17285a6000563571bfedff8eea5ce377585c6643821` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=kind,data,utc,monotonic,pid |
| `主管/P24-自主實機完整流程-20261026/r20-reserved-four-preservation.json` | 11096 | `b597878a6371ee3646b51e4f221bf14c97a401ea8eb1aa15f8962b091a6f5785` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r21-reserved-four-preservation.json` | 11095 | `d4b85bae5ac594008bc04c5b40fd2400e6884283ff4a5e2418686ed361904e85` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r22-reserved-four-preservation.json` | 11095 | `85e7cc95286f3e0d55bb64aec93f6354c94cc0d52b602da3c9b93451e91b7286` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/r3-broadcast-closes-dialogue.json` | 568 | `c812ce0a65a328db8763f5bfd4ec3f1ee50b7da8df96b77b01b25cfef0f635de` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,position,npc,closeEnabled,observation |
| `主管/P24-自主實機完整流程-20261026/r3-death-observed.json` | 45351 | `a2743351ab1ec1aa7bdd6af6b563390bbb064162ce0d2b17c60feb710585df95` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state,scope |
| `主管/P24-自主實機完整流程-20261026/r3-death-timeline.json` | 50896 | `4399ae31852d1e5191283bac329a7bee172707503a614b534996ec8ea228a71b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=firstDead,commands,operatorConclusion |
| `主管/P24-自主實機完整流程-20261026/r3-drop-pickup.json` | 259 | `5b79cc57c7081b54c224d832ac988242a809c2c5bcb537aa81ade50ccf9ca33c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,before,afterDrop,afterPickup,ground |
| `主管/P24-自主實機完整流程-20261026/r3-equipment-healing.json` | 1616 | `3e03a2b075cd2d85713d6e81159a1198a8da90a60634e94fd3fb761b1e7dd7cb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,world,inventory,magic,actions |
| `主管/P24-自主實機完整流程-20261026/r3-equipment-switch.json` | 1247 | `bf2e2a8bbcd2029a13829b98ae5a0610ae4a9081dd009bb208f0c60178119823` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,shieldTwoHanded,afterShieldRemoved,bow,world,inventory |
| `主管/P24-自主實機完整流程-20261026/r3-first-gameplay.json` | 1271 | `fcb9d404b7b2a6c0d61d687662c83199662088302978b25145e8af93bde15995` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,world,inventory,actualNameObservation,moves,notesArrow |
| `主管/P24-自主實機完整流程-20261026/r3-invisible-occupancy.json` | 15280 | `89dc0cc9b8b4cea9a1983810ec371ea4333227a5ca700bc81703d7b6d828a235` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,position,objects,visibleTargets,mapNotice,observation |
| `主管/P24-自主實機完整流程-20261026/r3-normal-relogin-recovery.json` | 1213 | `b6c985771e6bbec5681b31e7334921d53f78fd4bc06fe091547bb72801fc9dd0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,world,inventory,heals,source |
| `主管/P24-自主實機完整流程-20261026/r3-npc-dialogue.json` | 878 | `fb1727388e759406d213bd6b142d025c158d6589c8f5386e015e6e7403863cf0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,pos,target,dialogue,reply,correction |
| `主管/P24-自主實機完整流程-20261026/r3-review.json` | 2334 | `2af93ef44cf9c3ed581fe50c2b0694215d6d69e9a8e9997218d44de17ab7b7a5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,sourceReviewed,snapshotFiles,workerResult,status |
| `主管/P24-自主實機完整流程-20261026/r4-before-normal-exit.json` | 140642 | `0274fb89da43055596dc537dfcefb5e042d18251aaff373a3c9d38d758bc6e72` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state |
| `主管/P24-自主實機完整流程-20261026/r4-cleanup-announcement-closes-dialogue.json` | 142563 | `b992042ca512ec6d8eb3129fece9fb5b8594f298ffc1239d999654df6d2171fc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state,ui |
| `主管/P24-自主實機完整流程-20261026/r4-equipment-rejection.json` | 898 | `13a62bec1e241c1bf24d887998b649ddbeae985cf2e3730a7636ac35fc178112` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,ui,equipment |
| `主管/P24-自主實機完整流程-20261026/r4-invisible-attack.json` | 44005 | `f5bccee2415f7c51ecc1a700c1f2dcb5b7152acbc4ea8947a8cd67db9e4c8c37` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state |
| `主管/P24-自主實機完整流程-20261026/r4-login-world.json` | 42230 | `f3a0955c0950bb89abedd0d854e049c70c47f4f77ff56a41f52dceaa83cb4b3b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state |
| `主管/P24-自主實機完整流程-20261026/r4-recovery-and-equipment.json` | 45933 | `f10e422ae566190ab016e1b606e767f06517ff21ce896b0a0fbaa0a01fbcfc4d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state |
| `主管/P24-自主實機完整流程-20261026/r4-review.json` | 241 | `71e595044c5dba10a67a735c795dcf18a3c22834ba175ecd26a11de386bf865a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,snapshotFiles,mismatch,review |
| `主管/P24-自主實機完整流程-20261026/r4-routing-observations.json` | 1419 | `af5dd9b2c82735ac83a17d9230607dbe3698e2848a4652f975650d7c8703a15a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,method,lastLocation,combat,deathPreserved,knownInvisibleGraphics |
| `主管/P24-自主實機完整流程-20261026/r4-serena-dialogue.json` | 2794 | `5564ac4d0372ec5d2b7388f68d0b828ee1aa9d6c4302d7732c8811722bab720a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,ui,interaction |
| `主管/P24-自主實機完整流程-20261026/r5-after-ui-control-timeout.json` | 153881 | `e9b63b0e5105227c84e9fc269aa36e0acf941345e977b15472f38ec28e28c2d9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r5-announcement-trade-timeline.json` | 3067 | `91922bffbb00247876b5b9a585d719776f13391f21fc61f5b855b73d4842d4f2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(25);keys=utc,announcement,trade |
| `主管/P24-自主實機完整流程-20261026/r5-continuous-lure.json` | 2 | `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945` | M — M01：欄位限身份／路徑／大小／SHA／組件相依或已核讀 pins |
| `主管/P24-自主實機完整流程-20261026/r5-first-world.json` | 139624 | `c84590ab75091dcddb4ba0f435cf7d18568b481a71992cefc20cd20b405677c8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state |
| `主管/P24-自主實機完整流程-20261026/r5-guard-validation.json` | 78897 | `8676eb90512818d42c80d9dff9a8c267e5a2929ba166c3f034cd53f42f69d436` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,event,connection,note |
| `主管/P24-自主實機完整流程-20261026/r5-kurt-pursuit-death.json` | 55388 | `8b1760e40597e8a96f38f6fc135c5dc4c1f2a3b71cb51d1b7d342238f85f631d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state,ui |
| `主管/P24-自主實機完整流程-20261026/r5-normal-lure.json` | 24942 | `c725ea6eb1cf26677f95aa29a65c11c419da419794c1b2e039639ec77e062a55` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state,guardEvent |
| `主管/P24-自主實機完整流程-20261026/r5-northward-travel.json` | 146099 | `2d58d8d09d794e2fa5e5688d56ea94479c1d12f5f2a868452cfa9b0ee2b59b6e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r5-original-sprite-check.json` | 395 | `3145db7683217d5a195d006d9efa575beadea4a5ed81cdc2898797e92d845365` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,method,sets |
| `主管/P24-自主實機完整流程-20261026/r5-pandora-buy-arrows.json` | 82051 | `25f2e630cbd217469595afb4f1ce626453e0c35f6f3719011b9fac47d10bbf35` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,before,after,ui |
| `主管/P24-自主實機完整流程-20261026/r5-pandora-dialogue.json` | 33008 | `fd6a67e02b2ba9093ce360dd39d10e92d29713949e065a915e57050008f7994d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r5-pandora-insufficient-funds.json` | 33643 | `8a75654564b143f2711c4ed03e7cada1907e3ed122dce3e634288a8a0bf89420` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r5-pandora-sell-empty.json` | 32953 | `101006d5fce4c8d7f340b14d46c46e4b8a7f69bbc44bc694c460a4e698e211b1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r5-preview-review.json` | 430 | `06ac553222a5baf82152d33d1efbd5c899094bae5ff69022b095d0459acabae0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,scope,reviewed,observation,rawSourceEvidence |
| `主管/P24-自主實機完整流程-20261026/r5-review.json` | 358 | `ad5b17b5d83bd61a21b5015a0e0e376385a00b5d82f09508dd0a9abd4e24ab00` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,snapshotFiles,errors,rawSources,reviewed,limitations |
| `主管/P24-自主實機完整流程-20261026/r5-slime-kill.json` | 77823 | `133983f5f863a358f05054b0fcb3e5c0de64ef7881f785dd5ffa826d64dd37f4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,before,shots,after |
| `主管/P24-自主實機完整流程-20261026/r5-slime-magic-kill.json` | 49074 | `2afe2ce45a52c64cd1f55a4ef859a6b650de3d698e0c4bfd684992dfb5b777c2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,before,casts,after |
| `主管/P24-自主實機完整流程-20261026/r5-slime-melee-kill.json` | 76233 | `877f14a6c56f754dad576677f7019b054cdfa7aeecdb22f4b91a5db02374b44a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,before,attacks,after |
| `主管/P24-自主實機完整流程-20261026/r5-trade-cancel-return.json` | 3114 | `ef1cd1f00c7286220d89ffd441c1169bb8c33423eda1ce74318296be3c5398eb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,mainTrade,peerTrade,mainInventory,peerInventory |
| `主管/P24-自主實機完整流程-20261026/r5-trade-completed-outbound.json` | 7630 | `976d517f925aef13453446730e33a0ad5ed75223b271fed0087d51b1361ddd6b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,main,peer |
| `主管/P24-自主實機完整流程-20261026/r5-trade-completed-return.json` | 7135 | `221da5822646ebb0819fb01da14913fd53addde9fe87fdbdbb74f9e422654b16` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,main,peer |
| `主管/P24-自主實機完整流程-20261026/r5-trade-reject.json` | 2094 | `e5a52730d0c3554673ec140a8c3c522a458f2f9cb7c75d7b0fd70b382c4eaaa0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,main,peer |
| `主管/P24-自主實機完整流程-20261026/r5-visible-kurt.json` | 53805 | `16eef7385e00be96db3311c9b79df418b401eedaed729c64575ed58751195790` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state,targets |
| `主管/P24-自主實機完整流程-20261026/r6-cave-travel.json` | 21325 | `d09aa5f9c072cd9cf4a84fa565e33e10f51255b38ab133dcb2e3ada99b3b6b88` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(77);keys=utc,heading,expected,state,map,actor |
| `主管/P24-自主實機完整流程-20261026/r6-first-world.json` | 148241 | `1c28beb05a2fb189bcf5c0295ae33cd25b2ee251f3d6dfd6c951342c10f27e3c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r6-guard-1793016558001.json` | 61487 | `ef366aa8469e49d5596627e08705ffaa5320f75ad1da67f8455125be331e0e14` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r6-last-ui-events.json` | 33801 | `187e529d555bea437cee7b1ece6545cff49aa7122724a6ce67db48a1d878ccba` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(15);keys=utc,kind,data |
| `主管/P24-自主實機完整流程-20261026/r6-relogin-failed-observation.json` | 422 | `54313833cd090384c340f7a89fdc12be06658bc505e9eed16c2a74a9718f1c6a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,sequence,lastGuard,note |
| `主管/P24-自主實機完整流程-20261026/r6-review.json` | 8228 | `e7e328e036d60586ae6d0207de16ca4061b97ac21f480bdc198f79fbbbf94ccc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,manifest,checks |
| `主管/P24-自主實機完整流程-20261026/r7-cave-enter.json` | 38381 | `203c2b3d5edfda64099d4ac5f5bb3bc4fdbd95438afea04e0e74d3a05181ea7c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,steps,state |
| `主管/P24-自主實機完整流程-20261026/r7-cave-missing-appearances.json` | 7008 | `ebc6b30905e1bef6d0b24f55cd6503399e1aaac9dbf7690ea9e3442c7462865f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(7);keys=label,object |
| `主管/P24-自主實機完整流程-20261026/r7-cave-return-attempt2.json` | 31872 | `67e25bfed07ac80e826aaec9edab574e38414eb1e85ce3ebff3156cc51ea90bd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,out,back,state |
| `主管/P24-自主實機完整流程-20261026/r7-cave-travel.json` | 7703 | `64952b0ae8479bcf191f5fe06b60038710c065d8bfb69cf3c72d2c512d6210f9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(28);keys=utc,heading,expected,state,map,actor |
| `主管/P24-自主實機完整流程-20261026/r7-guard-1793017819402.json` | 36675 | `33ecd7bce36c67f113ff0518bd48c25e19c362ce41ab986e6f16e01bb4a38c91` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r7-guard-1793017914363.json` | 36787 | `fa4fcb0e606b3930f89864c35dcf302fd1f7f9181dcd087c6406e1200c77c2cc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r7-guard-1793019105335.json` | 43393 | `ea1b412fdc45057cf99a285bc687150c86906c10e19d66191f69ff4227d34caa` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r7-guard-1793019169914.json` | 32965 | `3a6f63748da8be5376201791ca806cf48f4005803613a281a7488627b3284e98` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r7-guard-1793019815595.json` | 56734 | `bea4d76336288f17a8b96fc09fe422f28701338677d3fdb522650b4d2849d5ea` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r7-normal-cancel-relogin.json` | 66456 | `fe251b70a8a26b9c521849fdf502436ae6d047f104b8e8cdd804dc9263ffde76` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,first,cancel,relogin |
| `主管/P24-自主實機完整流程-20261026/r7-observed-missing-appearances.json` | 5967 | `5f2ef693cd4a43b2de3acdc9139e3d83f3d86f9b97c714741ad179a3b6e85f4b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(6);keys=label,object |
| `主管/P24-自主實機完整流程-20261026/r7-original-1108-source-check.json` | 3935 | `8751f4c44ccfb86e9690be2864bba1807e0f57d41d27d493a311eaa80557fca7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,checks |
| `主管/P24-自主實機完整流程-20261026/r7-return-offline-preview.json` | 6886 | `e9a8d31364ffc0fd87bc007dccee66eee51ee1ac45438201009387d30c5467c9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r7-review.json` | 8228 | `0246d79a66c2561d6cb292a8ebac38dd71b8b5df857cdb2bc16eb789fa7c168f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,manifest,checks |
| `主管/P24-自主實機完整流程-20261026/r7-source-review-notes.json` | 513 | `cff8c0c801ae2e9cabef44f05514143899c9a61b10c40f2e776cc296da46b4d0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reviewed,status,source1108 |
| `主管/P24-自主實機完整流程-20261026/r8-first-gold-hunt.json` | 78509 | `4ec048613c84dac2f1e237b57cf266bf84c41a24ccc464c15feeae7389589901` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,fight,after |
| `主管/P24-自主實機完整流程-20261026/r8-gold-hunt-02-retry.json` | 66548 | `0f9584244504e4a6c6e62ff1e13dd4a9369079848bea8acc72543e9b763d9846` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,before,rows,after |
| `主管/P24-自主實機完整流程-20261026/r8-gold-hunt-02.json` | 67192 | `8a2753cf29c1fe3eaf1d2b9be3ff28bc93f0d97f4091d59fa8707a30a85544cc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,before,rows,after |
| `主管/P24-自主實機完整流程-20261026/r8-gold-hunt-03.json` | 210042 | `efee7025011524e40a758f36fe37aa8ac743cfe197759477826de56d91b344fc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,before,rows,after |
| `主管/P24-自主實機完整流程-20261026/r8-gold-hunt-04.json` | 254585 | `65cf3c0bd94a204b75f1899d002b7ba409024edcae37f5ec67fa84f1b2a373a7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,before,rows,after |
| `主管/P24-自主實機完整流程-20261026/r8-guard-1793019274394.json` | 59968 | `c6e79a7521bffd5dac8787e7897d96c11139c8e320fc7b61195f171bad7bdfce` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r8-guard-1793020302138.json` | 163557 | `81d68ae8998a0991b66430e9d6f69bb523e14ad8f44c1874c3e74e4b9a1ea853` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r8-guard-1793020471740.json` | 54172 | `691875bf16041af7bc85fbebbbfa6f31f93604e2a4ae424f01cc7c11208985b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r8-guard-1793020543332.json` | 34030 | `caca7ce054a7cc79bfe38ced02040946658514d831e35ad136b472f6e3a5ec97` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r8-guard-1793020633278.json` | 31319 | `9462d2db20a6f7b0cf59938113e2ae7d8edfadc83c87646dc76c01fc7d09b389` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r8-learned-skill-offer.json` | 50309 | `16c65ca374e959e92b2504772ebe55ac24e4c30c8dda45fad98263430f03c4d2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r8-learning-insufficient-funds.json` | 111283 | `c1a0574190a0349b9d42575555c61097d270d2bcbb8f897db9183e4ab24374a2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,after,ui |
| `主管/P24-自主實機完整流程-20261026/r8-low-health-recovery-plan.json` | 4139 | `e738533ff5097d545f085ca23b76317e2f70cd7c7fcf8ff47e2fd62542cbbdf6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,main,actor,items,plan |
| `主管/P24-自主實機完整流程-20261026/r8-magic-learning-list.json` | 52558 | `3b5b6537a0957e378362e58eeb6ff6f4dc4c3244ef5459889211aef0817f98c5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r8-magic-teacher-dialogue.json` | 50031 | `b18bf09ee398f1642cdaae58ba1b736e96807684c56c24dc2fa66c90d4245a2a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r8-normal-self-heal.json` | 56798 | `11fcc3beb3fb790e4e5bc455364ad9a5191255ba334d3706ce7906d2acaba094` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,rows,state |
| `主管/P24-自主實機完整流程-20261026/r8-original-source-check.json` | 52687 | `7e60d0e638b5c3e1fce76557e4a2755cead7aee32f008d5bd6f97cfbed15c853` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,checks,unavailable |
| `主管/P24-自主實機完整流程-20261026/r8-rescue-guard-1793021100500.json` | 29903 | `d5e108db89f90d5dc3358ff0637540a972ae1a4ad23096156dd40b5d0e25d034` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,state |
| `主管/P24-自主實機完整流程-20261026/r8-review.json` | 8228 | `ed090d6ba6ae1cf952a30f5ecdcfbba177cf27503f47abb9e5859ac4e661babd` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,manifest,checks |
| `主管/P24-自主實機完整流程-20261026/r8-role0-first-world.json` | 24421 | `4bcb6e401ecdac93c2a381f6aef2fd5b3201d2fd087513760c75861cf742afce` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r9-before-followup-fix.json` | 72721 | `70a32dc743bf46f5948e96d469253b51941f47b6d8e77c9606037bc383b2c016` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=state,guard,route |
| `主管/P24-自主實機完整流程-20261026/r9-potion-invalidates-dialogue.json` | 119393 | `49f27ad898f1bd2775201f9978690959b7ecfcd7dfd3f33d887c8d98cf0d966d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=usesBefore,usesAfter,before,after |
| `主管/P24-自主實機完整流程-20261026/r9-rejected-move-feedback.json` | 55137 | `29963453c190d62af1169283da4110260078107cebbef4c4c0b97986f7339b7b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=observation,lastState,route |
| `主管/P24-自主實機完整流程-20261026/r9-review.json` | 2596 | `9ec847d985660033f7fb063779c9b7bd46ff8865ca5566211b0f2771c0de16ab` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,manifestSha256,files |
| `主管/P24-自主實機完整流程-20261026/r9-service-cast-learned-shield.json` | 53445 | `21968f927f923d8a6ee1fe5d4904604c65b702c0c43bfd0939f85c5d8597eb6b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r9-service-first-world.json` | 13830 | `d7a5963c0a2a963e454e2301b2dd6d99f0a47b6b7dee6f1077629037fcbbace5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r9-service-guard-1793022680938.json` | 80790 | `e575132307bc76ff658b7c22d31ba599d14ff4f42c5c8ff48b862df7ee28b469` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r9-service-guard-1793022982602.json` | 96682 | `97cf1ed0f91e3330b76c4e8b2180ebf8f73b0d372fa4865a20d7b519d62b5712` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r9-service-guard-1793023407745.json` | 48055 | `8e54f8673d4c876bcaf8e05bc7fa77fbde24fa38643b512e62f7c97089a5f393` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r9-service-guard-1793023688059.json` | 36566 | `89f0b0b54d24d8d6201d51e2c485482c12064fe1ef3494981999c4624ad4e0b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r9-service-guard-1793023772276.json` | 26872 | `8dac3a3a182b273d89504922fca66cb876017a85901a2dc586985d96aa456d49` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r9-service-guard-1793023999725.json` | 28053 | `fbc36803e6e1a767403d95c8ed3274c72386fce4df6d09d483c5c9f52c15b7dc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r9-service-guard-1793024148903.json` | 34235 | `79e63bd7340c252d23415b90b305883f858e11ba2d0e746f473098835cfd2244` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,reason,state |
| `主管/P24-自主實機完整流程-20261026/r9-service-learn-shield.json` | 115386 | `a77c60b2b1c72352fa46e28757b1611dfa3cdfd111ec38a20af56b1bc8376f30` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=before,after |
| `主管/P24-自主實機完整流程-20261026/r9-service-normal-respawn-login.json` | 24769 | `4576cd9871f07f7adc890ff6543286d973d990626007fe10d8d2ae36e106ee36` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r9-service-restart-login.json` | 21191 | `96397a174a83370a9299e76f8700d0b9ced04ea8531e3a06adc9f44f9beff391` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=busy,character,confirmed,connection,direction,gameFocus |
| `主管/P24-自主實機完整流程-20261026/r9-service-route-teacher.json` | 110812 | `501a04c3c11fdd030de0b4a1c241503ac90b4a83ad5642f9a0a2aac4e6a3fd03` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=route,last |
| `主管/P24-自主實機完整流程-20261026/r9-teacher-to-port-route.json` | 73136 | `48429079025e0cf8df798de9def400683c2ce36ebbfc112176022b0a7ce11014` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=steps,last |
| `主管/P24-自主實機完整流程-20261026/review-findings.json` | 1196 | `946d1f1344ed90064682d2f51adab021698e767411f5b87ad9db8907afacae27` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=updated,findings |
| `主管/P24-自主實機完整流程-20261026/roles-after-r11-guard.json` | 3834 | `d9283df0aa41bdf343c9991ca2b3173f5a67b268b9af575d607fd3e524db4f04` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/server-exit.json` | 84 | `fee3b81eafae1c195c160b562b706bcbb2cd48651cf2e43f4935e87de820fdce` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,exitCode,utc |
| `主管/P24-自主實機完整流程-20261026/server-start.json` | 2131 | `78b173ae33a32443506e7a236615c40e0ae73035cb66fd01860e337f9239fc05` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=pid,args,utc,retainedDb,jarSha256,policySha256 |
| `主管/P24-自主實機完整流程-20261026/service-role-after-first-login.json` | 5849 | `715172d619c20aa2987836c3d83c9eae5904a40749a7a2ad161ad2e9999c0d0c` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-after-harbor-withdrawal.json` | 5906 | `07f05f05993c1c36275def0689487a563772f46f6be5f72ce4ed86b2ccce50c1` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-after-normal-restart.json` | 5849 | `b644ceacbcf7f3d0ed6daeb6075e60c2bb7856b8b51afafa424b3c12d2680eee` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-after-peer-reposition.json` | 5906 | `52cbcec09658f8e3732550fe9d1564a4003c7952060639d90b58f038a54dd069` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-after-port-death.json` | 5906 | `7cb32f97becdbd94f679eaac580a641c03a58baeeb3b8f0307ad5bc266066989` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-after-r11-coop1.json` | 5775 | `a36bca9965764a2cd684bce0b1771ed6a55ede46e3ed48110401053eacf87715` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-after-recovery-interrupt.json` | 5906 | `765c62d1d3b0d7a4a19a766d363f8d15d8cda24fc3934619b0cc6b5762e7f8de` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-after-recovery2.json` | 5906 | `6d946c6b0bbde2ceba1fa56921a7e5bdaff143267bd71b69f445bf11da57fba7` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-after-ticket-approach.json` | 5908 | `16c0de42236163ec5d37401d0666048e8150908ab50044314fe3dde0409cbcb3` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-before-r10-fix.json` | 5908 | `75054e69e3cceb95dff4bfaf182e24056b541db988f5fdc65204e110219878d1` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-before-r11.json` | 5906 | `4be569c33d9dfada13924f2af9ef010f2a5f6c2a9f9a0a2be02d9fa91136aa88` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-complete.json` | 1719 | `ff3b94d5ab81f799b2ba52e52bfb6f5b028ef7ccb7c37b64cfdd284318d51f79` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-insert-00.json` | 391 | `c068a2ff65af0b9cb859fda599b1013aae8e983a6d16dd055c2cf313c39f4c34` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-insert-01.json` | 785 | `586a3733fa85fb49ee4bd00e89a5b9cc7d50e18f3c7cba04b96688d7b1634d3e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,table,row,insertReturnedSuccess |
| `主管/P24-自主實機完整流程-20261026/service-role-insert-02.json` | 269 | `90e2207b3caab483d7d751251ee3a4ab3c26b936dce16c40ed036ffb04131d59` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,table,row,insertReturnedSuccess |
| `主管/P24-自主實機完整流程-20261026/service-role-insert-03.json` | 260 | `fcfec1760424648a690332ff8f1b2ef8fd21c51f3f056fb0ccc2e955d5da8416` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,table,row,insertReturnedSuccess |
| `主管/P24-自主實機完整流程-20261026/service-role-insert-04.json` | 486 | `f7dbb18b50d46591a864caf315fc75f410c4085a34d2a04dfb7cb1fb9f3149ff` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,table,row,insertReturnedSuccess |
| `主管/P24-自主實機完整流程-20261026/service-role-insert-05.json` | 495 | `1ef0e53bfe913754a4e061fde1a2d8e75803b1e29c739c56d3e69a4dfa3d45b6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,table,row,insertReturnedSuccess |
| `主管/P24-自主實機完整流程-20261026/service-role-insert-06.json` | 478 | `9aac5a74f2a2d2ede0464b99b8f9f26edc8f62d83303505be80c3850b4aee3a0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,table,row,insertReturnedSuccess |
| `主管/P24-自主實機完整流程-20261026/service-role-insert-07.json` | 481 | `8bab2400a2e0eb70ce9eea7bc5d451c21a5bf804230030c7b5ba90d6f8bb95c1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,table,row,insertReturnedSuccess |
| `主管/P24-自主實機完整流程-20261026/service-role-insert-08.json` | 481 | `969bd948bed574347bb24125a842e25694aa78aebecf8eca3f892da93cd0d8f3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,table,row,insertReturnedSuccess |
| `主管/P24-自主實機完整流程-20261026/service-role-preflight.json` | 23585 | `a5d47babada80411178ba60b979ad995246c9c0b457afb63b523550c7e75a0cb` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-r10-regression-saved.json` | 5908 | `ea2af35aa78533a02ebd502f30a31eedbdce833d9628d9359d319b1de3c6ed67` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-r11-pier-before-voyage.json` | 6040 | `ce718fa4f5655005088ae332f307d40ff4b76a19fa758d3cc42051823858027f` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-r9-final-saved.json` | 5908 | `b24c181f54b2530d9559a08123cdbc34ad61e582acb7121cdca3f39f16aa5f80` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-role-started.json` | 9158 | `57fc507e4c3f95e3535b94c83301420c1ff39f3e0ba8737659a5853aa48491f9` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/service-test-role-proposal.json` | 1075 | `6501622fc7476b16e6c1e9be759ccd02aa89d14fe0b857438325d9822d7c3a6c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,purpose,scope,proposedNewAccount,proposedCharacter,level |
| `主管/P24-自主實機完整流程-20261026/ship-backdrop-source-review.json` | 987 | `8ac2cb401599f4f7f649ec9dbb48d69414ed529579e2cb81ad345c7a637221f0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,candidate,readOnly,finding,requiredReview,status |
| `主管/P24-自主實機完整流程-20261026/ship-cache-point-review.json` | 2564 | `6032a8321a5815c5db5622358e459e6fbebdef61656a79e0b1d08157609f8644` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(4);keys=mapId,x,y,slots |
| `主管/P24-自主實機完整流程-20261026/ship-candidate-root-review.json` | 455 | `914c0815b308bb92b37378b855f95d97b9a86685ddf40ced8530ad47e7772fb6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,deliveryFiles,bytes,manifestSha256,deltaFiles |
| `主管/P24-自主實機完整流程-20261026/ship-login-return-rules-readonly.json` | 422 | `2b6b1b73c8682091f06279d08033a34d270198ffaac8f683a76b5e9dd8d82984` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=readOnly,rows,observed |
| `主管/P24-自主實機完整流程-20261026/ship-route-db-observation.json` | 7724 | `7da9575be22922051283cf09cac9ddaf2680c488303aa456c725909f4c85fa10` | U — U07：敏感欄位名稱命中；不輸出值、不作內容白名單；unknown |
| `主管/P24-自主實機完整流程-20261026/ship-warehouse-roundtrip-reconciliation.json` | 1605 | `e19b5bcb29aa2059849554f8bad385355b92fd726ff88f2d1868d1a0a4234dc7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,actualNormalUi,outbound,returnTrip,naturalServerClock |
| `主管/P24-自主實機完整流程-20261026/ship05-offline-image-review.json` | 893 | `4973e11eee06664c663d2c1f37fa5d7b2cd7fa32b400b34be51ffd3bfa16aa42` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,kind,artifacts,observed,pending,acceptance |
| `主管/P24-自主實機完整流程-20261026/ui-runtime-notes.json` | 970 | `d2f498f3980d1d5bf7e60902b4ecac9d37de0554622408cbef52ebb19bb322d7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,userConfirmed,nativeCapture,actualBrowserControl,loginAttempts,workerFix |
| `主管/P24-自主實機完整流程-20261026/自主實機測試結果.json` | 1895 | `cb3529c12b21a77bcf96e5a9bf751f2eb8c4f199a0cd59a42184fcc878eb338a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,latestRevision,entry,verifiedFlows,latestFixes |
| `主管/P24-跨圖接線獨立審查-20261026/global.json` | 56 | `1c530f4c116bcf04e451cf0ca155fa18d73415c85145534a415f1eb4c39198b3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=sdk |
| `主管/P24-跨圖接線獨立審查-20261026/processes.json` | 5210 | `02759628b8a7e41f478726677deb0f9d608f847a07fd76b24fefc63424e84869` | U — U09：JSON 證據缺逐欄來源裁定；unknown；list(10);keys=name,pid,args,exitCode,elapsed |
| `主管/P24-跨圖接線獨立審查-20261026/result.json` | 5959 | `6a9f0a92e48379dfbad93d6d2b605d968266cdfbfabfb848dd6ef94a694d8c9e` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,snapshotFiles,assetInputs,checks,workerDll,cleanDll |
| `主管/P25-P27-新主管接管-20261029/HANDOFF.md` | 4188 | `bdde21cb7a9987e9981fa46027a07361f383d0fe54d14c2b17ebf1348ff70893` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P25-P27-新主管接管-20261029/P28-P29-candidate-root-acceptance.json` | 2560 | `2537af60910b67dd6803b1ec8f5252823288e65936cd06c57d6197c5f1e05dd3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,decision,candidate,candidateSource |
| `主管/P25-P27-新主管接管-20261029/P28-P29-coverage-crosswalk.json` | 8557 | `59ce388fe1f987830f3b477d19ecf100b446c6a118041cad1eaa8afc84e25e98` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,classification,authorCandidateAssertions,independentCandidateAssertions,nativeAssertions,families |
| `主管/P25-P27-新主管接管-20261029/P28-P29-dispatch-receipts.json` | 1145 | `b6c99c6b76c84576e2bf06693e29f9ebf157b4734792d8d6809a454fbed899ae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,base,tasks,created,actualReadCommandsObserved,candidateImplemented |
| `主管/P25-P27-新主管接管-20261029/P29-candidate-review-dispatch-r1.json` | 2059 | `8cbdc3a55f6c003f241e12bb3a956dadf973c74c78a8cdc8128f15067da7466b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,reviewRoot,baselineSha256,authorization,candidateRoot |
| `主管/P25-P27-新主管接管-20261029/P29-loader-repair-dispatch-r1.json` | 2060 | `17e456750a07aab72864e05b0db498ae709d0d0cfb0910f08014ece41ed781a8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,target,activeTurn,finding,evidence |
| `主管/P25-P27-新主管接管-20261029/P29-root-verification.json` | 793 | `146ce54c40bc70422c68afb2cbc360328a28285d7d2ce531bb318f9344e3a057` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,artifactRows,sourcePins,pinFailures,runtimeUniqueRows,failedRows |
| `主管/P25-P27-新主管接管-20261029/P30-environment-diagnosis-acceptance.json` | 702 | `fb2d85e5a0dd63c18f7a8daf051485a3d88fd7c0b87900c3038e9597ea290048` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,decision,reviewSeal,artifactRows,minimumFixedEnv,newBuildAuthorizedByThisRecord |
| `主管/P25-P27-新主管接管-20261029/P30-executor-preparation-dispatch.json` | 3612 | `4be1242a443975632a8f13ab23c528aec6eb2f536b144647e579175dcbb80568` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,dispatchId,owner,decision,writeRoot |
| `主管/P25-P27-新主管接管-20261029/P30-executor-repair-r2-dispatch.json` | 6705 | `44751d44a1abff0aef270dce056082e8c37483fd2f08763f9358eb35d55dc30a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,dispatchId,owner,writeRoot,futureExecutionRoot |
| `主管/P25-P27-新主管接管-20261029/P30-executor-root-verification.json` | 800 | `416ff7209b6b2bb2c9255d8d0c080a2deb35800487ca5d3494de992099a28b68` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,sealSha256,artifactRows,argv,exitCode,stdoutSha256 |
| `主管/P25-P27-新主管接管-20261029/P30-final-HANDOFF.md` | 2037 | `8956fcfa3cbcffaa39ca93a1e141f4bbc2655c89dc7b03d15d2323bea7038120` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P25-P27-新主管接管-20261029/P30-first-build-failure.json` | 2408 | `cb0829fcd9610ab3614e206689655ceb7577e3bead2f823dec8f62165fe8e2f7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,buildAttempts,productCompileReached,productDllExists,actualProcessTreeEnded |
| `主管/P25-P27-新主管接管-20261029/P30-independent-review-dispatch.json` | 823 | `5526b9a93c86fba6323cdd919a2bf26bddc9aea43ee9d1d6375fd6160cfe1698` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,owner,scope,writeRoot,productBuildsAuthorized,runtimeProbesAuthorized |
| `主管/P25-P27-新主管接管-20261029/P30-isolated-plan-dispatch-receipt.json` | 915 | `ddf7a204148805fcd6392c8f9fa71e1863937d4bdc971561a3c43cc7f6997579` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,dispatchId,threadId,turnId,toolSendConfirmed,threadStatus |
| `主管/P25-P27-新主管接管-20261029/P30-isolated-stage-plan-dispatch.json` | 3865 | `b7ef761dd585c5594c38d870fb111294d4ffa519c31b4aeff3e83876dc5bbed0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,dispatchId,owner,authorization,writeRoot |
| `主管/P25-P27-新主管接管-20261029/P30-plan-root-verification.json` | 959 | `8a4face458005cd4932eaa012b3b06c5c7dd119fc3772d3e790b0a924aedc24f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,artifacts,failed,planSha256,argv,exitCode |
| `主管/P25-P27-新主管接管-20261029/P30-product-execution-dispatch.json` | 496 | `13ce8ef0486f655e33df2b73e3ff2eabd4ffa30cc68837ef03677400ece948fe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,dispatchId,owner,sendConfirmed,receipt,receiptSha256 |
| `主管/P25-P27-新主管接管-20261029/P30-product-only-execution-acceptance-r2.json` | 3002 | `c6d74ab2020063d2ea432ca35efb3b5ebf42ce79c0296cc7bc050b363ecd2707` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,supervisor,owner,dispatchSha256,contractSha256 |
| `主管/P25-P27-新主管接管-20261029/P30-product-only-execution-acceptance.json` | 3231 | `240ce19f1cd8f10bddff4a34366eacef31598d36b7bce0130a0cb83a431fe541` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,supervisor,owner,dispatchSha256,contractSha256 |
| `主管/P25-P27-新主管接管-20261029/P30-product-only-execution-acceptance.proposed.json` | 2438 | `93a4f0a6ecbf9d72ec58a7e743d58ac3f8b57e86d514fb75ddf3b74c8df0a75b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,supervisor,owner,dispatchSha256,contractSha256 |
| `主管/P25-P27-新主管接管-20261029/P30-r2-actual-identity-check.json` | 1340 | `118a03db3c7045b554b1493480ec3d74ed070ce248299a959f2d0636f544f8bb` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,result,command,product,audio |
| `主管/P25-P27-新主管接管-20261029/P30-r2-completed-core-run-pins.json` | 4172 | `4cac98d7d5f39197deca2adfb72188270bc93e59e24ffe4a46a70d2698ee1277` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,schema,state,root,files,producerTurn |
| `主管/P25-P27-新主管接管-20261029/P30-r2-final-acceptance.json` | 6919 | `b37e5993a5a2fd5c0aed6f10e5c1c313da6dbcaf5899fcb729fffd6d08c03cf1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,status,scope,reviewArtifactsVerified |
| `主管/P25-P27-新主管接管-20261029/P30-r2-full-seal-root-verification.json` | 831 | `fffad716ffa4ccd63f32393527cb4f89cd16bc524364c14aa2bc41c50aac6b3d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,seals,actualSourceIndependentCopiesPreviouslyVerified,productAndAudioExactHashPreviouslyVerified,finalIndependentB |
| `主管/P25-P27-新主管接管-20261029/P30-r2-root-diff-acceptance.json` | 1494 | `bbda39be4b2f8fb6fa47313dba4d767d4e7adde7aaed90d79c47f19fbc96efda` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,decision,seal,artifactsVerified,contract,unchangedRuntimeSources |
| `主管/P25-P27-新主管接管-20261029/P30-seal-filename-correction.json` | 1488 | `3ced45873aed586a9649ddc94f7be5ede63980339e9feb836f525b6f53a35f29` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,rootCause,originalIncorrectLabel,correctSeal,correctManifest |
| `主管/P25-P27-新主管接管-20261029/P30-source-delta-root-review.json` | 832 | `46c1cdc4af83810c5ebef62c25bc159da9190299c9314d715e106cd1b8be4b4a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,baseSourceFiles,finalSourceFiles,unchangedFiles,unchangedWebFiles |
| `主管/P25-P27-新主管接管-20261029/P31-P32-HANDOFF.md` | 2915 | `d5b8af15cc84af799afe0acde7c0072b34a49f57da920d22e7f0b5b3ce8446b9` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P25-P27-新主管接管-20261029/P31-P32-acceptance-gates.json` | 3009 | `0d9dd5d18d508fc8757180aed57d115bf250b5419a3c5421a0861166900e0ff1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,basis,status,common,P31,P32 |
| `主管/P25-P27-新主管接管-20261029/P31-P32-dispatch-receipts.json` | 1972 | `d78b613b7f7fb8f4849e54fb3d97b99ac793605e286e05849170053bdc8e1be8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,dispatchSha256,threads,sharedWritesAuthorized |
| `主管/P25-P27-新主管接管-20261029/P31-P32-dispatch.json` | 11181 | `e5f69053b211da7688f4c7f58f3b8e642e768ea85a98712e116016a7c99c3e7f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,baselineAcceptanceSha256,existingWorkers,sharedWriter,tasks |
| `主管/P25-P27-新主管接管-20261029/P31-P32-root-acceptance.json` | 8285 | `217e44587a14ae2b65441aceb4f551cca1717d4b228616e51d5a2ec6a62acccf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,status,baseline,dispatch |
| `主管/P25-P27-新主管接管-20261029/P31-root-review.json` | 2332 | `ebc50b16a96cbc60c85c2f068f1aa8453702f87c808c653bedbf1781556802f2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,seal,artifactsVerified,sourcePinsVerified |
| `主管/P25-P27-新主管接管-20261029/P32-certificate-readonly-observation.json` | 1525 | `7be8438265908062ae0b59e3a0bfe41528146a78936a54cedce03f7058736521` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,observedUtc,buildStartedUtc,buildFinishedUtc,mode,certificates |
| `主管/P25-P27-新主管接管-20261029/P32-certificate-root-acceptance.json` | 2448 | `4d8cc7dc7ab2d36bb97e95bb6384f4d8025bf490fa06d20e6a9c6b51b9ecbe15` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,seal,artifactFilesVerified,evidence |
| `主管/P25-P27-新主管接管-20261029/P32-root-review.json` | 7107 | `aea9d2c649a662ef335c4a23c369925a1348f89cf1eb019cf3a8a7c2a264ff72` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,seal,artifactsVerified,sourcePinsVerified |
| `主管/P25-P27-新主管接管-20261029/P33-P34-HANDOFF.md` | 5008 | `fb4fa41e9c4fb7959a501c6f7cdd7dae86a4988eca4f349746708e7e71a81e86` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P25-P27-新主管接管-20261029/P33-P34-dispatch-receipts.json` | 724 | `9a5fdc5c6c78a7e6056ae2ce9848f8f273283728a1897914a36a385fff39a901` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,workers,certificateAuditThread,certificateAuditTurn |
| `主管/P25-P27-新主管接管-20261029/P33-P34-dispatch.json` | 11479 | `706b1c4d1e62a22c8e529d0173ae35922a48ebd67743f20c2ec3430a29eb46f6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,baseline,tasks,parallelIndependence,productBuilds |
| `主管/P25-P27-新主管接管-20261029/P33-P34-root-acceptance.json` | 5551 | `7aa9f514b3b73b4b07d608ff0cf2f2459d15090d0ca021bba9f505130642c7d4` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,supervisor,workers,reviews |
| `主管/P25-P27-新主管接管-20261029/P33-root-review.json` | 3860 | `a36fe164775d568e34a455693f8e882d1c49a79ad3c7f90bcea2824224d9d36c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=phase,utc,artifactSeal,artifactFiles,unsealedFiles,sourcePins |
| `主管/P25-P27-新主管接管-20261029/P34-lease-hypothesis-resolution.json` | 1172 | `00a2003771427867342b88ebe7ba2e27468f53af0f2bf7d1f7a6a698e78f22e0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,hypothesis,status,correction,productDefectEstablished |
| `主管/P25-P27-新主管接管-20261029/P34-root-review.json` | 4497 | `69b11332f031d65b483dbb4b2791677bcabb6e8f502ffbc7d7c6b4fadc3e3e6f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=phase,utc,artifactSeal,artifactFiles,unsealedFiles,sourcePins |
| `主管/P25-P27-新主管接管-20261029/P35-HANDOFF.md` | 3648 | `bda479909c3ad9ec281d23f617787249f8f68687c150baa55119a7e7e94a1662` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P25-P27-新主管接管-20261029/P35-author-root-verification.json` | 2700 | `fd7765b34ac936f7feeae284aa48367bb95a69f265f54b6df10026c73197235f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,artifactSealSha256,artifactManifestSha256,artifactFilesVerified,sourcePinsVerified |
| `主管/P25-P27-新主管接管-20261029/P35-dispatch-receipts.json` | 709 | `7aaa476a03b9cd72631ed97fbd31d53197a7e2795fca375cf9ee5b8cd2c22f53` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,workers,existingThreadsReused,sharedWritesAuthorized |
| `主管/P25-P27-新主管接管-20261029/P35-dispatch.json` | 7775 | `5646a355bd4442addcb77da62d5c7cbb018529b1d9bb5dbc980d2c03b66bb20d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,authorThread,reviewThread,authorPrompt,reviewPrompt,sharedWritesAuthorized |
| `主管/P25-P27-新主管接管-20261029/P35-root-acceptance.json` | 6061 | `04174872b4a7d271776cdb8babd1eb4a8727d0c84ec1cacc8a8ce556adfccb95` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,supervisor,candidate,seals |
| `主管/P25-P27-新主管接管-20261029/P35-root-provisional-review.json` | 642 | `0a2ab0cc3a803a2484c79ef8683905b0fe3c0bebfabfaabcf3e91dc949154a87` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,originalSha256,candidateSha256,changedFunctions,fullGeneratorExecuted |
| `主管/P25-P27-新主管接管-20261029/P36-P37-HANDOFF.md` | 5587 | `a9bd7373db9fdddd28742bd8f5ba1f26a282cad7179993a241fadf27dd11702d` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P25-P27-新主管接管-20261029/P36-P37-convergence-delta.json` | 3777 | `c198d6836dfb9db55cda5b8dbc176a4afb2f9cefd83ddfebabf8bde2317875a1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,priorSnapshot,P36Acceptance,P37Acceptance,gameChange |
| `主管/P25-P27-新主管接管-20261029/P36-P37-dispatch-receipts.json` | 970 | `4aa295011bec7eb04e5a5111ae2136f09e76d1b7e1de6d7290bdc8db77becfc3` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,workers,sharedExecutionAuthorized,entryActivationAuthorized |
| `主管/P25-P27-新主管接管-20261029/P36-P37-plan-dispatch.json` | 9873 | `4c9821eefa7680d0cd1bb45503e7f6ab8257c90171d351e9633a864d07b2b399` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=scope,authorThread,reviewThread,authorPrompt,reviewPrompt,sharedExecutionAuthorized |
| `主管/P25-P27-新主管接管-20261029/P36-P37-r2-root-preliminary-review.json` | 469 | `67826d6c9402a9c1efe6426ac3bb4776e5c695cb3b27fb71e8b0ecf7542bceab` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,authorFiles,planSha256,contractSha256,artifactSealSha256 |
| `主管/P25-P27-新主管接管-20261029/P36-P37-repair-r2-dispatch.json` | 998 | `c9c7d868b6a94180d4c90e6182d6beae784b8c4c71b56a6c30e7aedd45c5c08d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,scope,authorThread,reviewThread,review,reviewSeal |
| `主管/P25-P27-新主管接管-20261029/P36-P37-result.json` | 2316 | `4884d517efe9c78a6a7dbcddb183e8849980245090957b93e99b97defe26cdd7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,supervisor,deliverables,productBuilds |
| `主管/P25-P27-新主管接管-20261029/P36-P37-root-preliminary-review.json` | 449 | `ac51c52f6f43d53cd651924203e41f5e929a7fe8948e97cbe4edeef48a4ce300` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,files,artifactSealSha256,planSha256,contractSha256 |
| `主管/P25-P27-新主管接管-20261029/P36-build-observed.json` | 1420 | `250a9dc1cb7f7932fc045c344663723326f054669e6b83469133c3c052da5952` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,result,product,audio,sourceManifest |
| `主管/P25-P27-新主管接管-20261029/P36-execution-acceptance-r1.json` | 2493 | `4d5a0d73650ddc9bd6d9f121feb3f55fb084b742e45eb26a96430cea540e4032` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,utc,dispatchId,supervisor,owner |
| `主管/P25-P27-新主管接管-20261029/P36-final-acceptance.json` | 3382 | `76145832e5865fad75741450f53500cfad455aa73507bb0a75709c35a86e1e17` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,utc,supervisor,planSha256,contractSha256 |
| `主管/P25-P27-新主管接管-20261029/P36-map-research-triage.json` | 2680 | `ee298d752d4d53cd6513c65fcf3dec0f0785149093fa2c2db0bf5f74241cea91` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,source,observedStatus,knownChain,missingEvidence,decision |
| `主管/P25-P27-新主管接管-20261029/P36-root-material-review.json` | 928 | `f4a2f5ee43302cfc2a1c674bb3cb43c00430b65be44b9edae82f07c4c9cb0d83` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,executionArtifactsVerified,externalMaterialsVerified,artifactSeal,externalSeal,decision |
| `主管/P25-P27-新主管接管-20261029/P37-final-acceptance.json` | 4089 | `1e6cc6f0296f85484402f5c9ab7cf345275342f345924c2fb54e60fc5d834336` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,decision,supervisor,P36Acceptance,executionAuthority |
| `主管/P25-P27-新主管接管-20261029/P37-inventory-only-acceptance-r1.json` | 1455 | `4c0236f33250cf63c5789af0586e4e9992b960e7841cf483da86c6fa89d2377d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,utc,supervisor,owner,planSha256 |
| `主管/P25-P27-新主管接管-20261029/P37-root-preliminary-review.json` | 743 | `0adfce16fa8a625332e755f94011b3b7f9c47e60e8fa98ed7c4186a66c958d97` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,status,inventory,authorSeal,invocations,sourceRows |
| `主管/P25-P27-新主管接管-20261029/P38-HANDOFF.md` | 3420 | `3e14fe61140818a3601572d1b3c26b7fdef05a162d9471be02eb9cee23235398` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P25-P27-新主管接管-20261029/P38-convergence-delta.json` | 3653 | `c222f594df5342457d7ca0e574195148e50338d19d2cb9667c7049dc0bce95d7` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supersedesCurrentDeliveryStateIn,acceptance,productImprovement,versionBoundaries |
| `主管/P25-P27-新主管接管-20261029/P38-delivery-execution-acceptance-r1.json` | 1944 | `11788b37e42a85f25d4a9cd9d8839a897d42fd16c1f98ed6e0d7322e454cc334` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,utc,supervisor,owner,dispatchId |
| `主管/P25-P27-新主管接管-20261029/P38-dispatch.json` | 12784 | `4322a06cd2321cb2ac9bc539536edeeb95ad45ccb2d38f833fad1f82933c406d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,scope,authorPrompt,reviewPrompt,states |
| `主管/P25-P27-新主管接管-20261029/P38-execution-observed.json` | 1064 | `06ec3b64466251045c6e1f8537e2ddd0cb9a17dd8f97ef83ca8609748bf50a39` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,authorTurn,resultPin,status,candidateFiles |
| `主管/P25-P27-新主管接管-20261029/P38-final-acceptance.json` | 4543 | `e4ac694414f9d112d181323e46b2f6fb61a5e788ee57554ba1e33971392ce478` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,decision,utc,supervisor,candidateRoot,executionRoot |
| `主管/P25-P27-新主管接管-20261029/P38-final-review-dispatch.json` | 983 | `8b3ff125e942401f1c179617431a6cf7fb3addb277fb93208f34a9b0923c39a9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,reviewerThread,reviewerTurn,actualReadStarted,assetHashStartedVerified |
| `主管/P25-P27-新主管接管-20261029/P38-plan-review-dispatch.json` | 744 | `9300486909d057872c2849e225659b2cfde1472f8e7954c328da6764402e60bf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,authorThread,authorTurn,reviewerThread,reviewerTurn |
| `主管/P25-P27-新主管接管-20261029/P38-qualification-scope.json` | 2998 | `f2b7256d2d5512fa1fc3fdb54c85fb8d530ca57fe2c6fd267f2dcf7e617efffe` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,purpose,offlineAuthorized,notAuthorizedHere,notProvedByOfflineScope |
| `主管/P25-P27-新主管接管-20261029/P38-result.json` | 3041 | `a0dd33231c702aea47c43857196b03133f6dca80bc16b9e1d50ef0858e6feb36` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,status,completed,wholeEngineComplete,acceptance |
| `主管/P25-P27-新主管接管-20261029/P38-root-final-evidence.json` | 996 | `9280ba3c34a447371a4845a2df8312b06d39c2bed9629e02da38de0d490f358d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,finalReviewSeal,reviewReceipt,rootVerifiedReviewMembers,rootReconciledAssetResultRows |
| `主管/P25-P27-新主管接管-20261029/P38-root-preservation-observed.json` | 497 | `f59ca3a7ab160de6e903c1248f012a47fc0816b0ccb8be7a093a2b4fe72e3146` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,candidateIndependentFilesVerified,protectedPinsVerified,candidateSealSha256,fullAssetBodyHashesByRoot |
| `主管/P25-P27-新主管接管-20261029/P38-version-boundaries.json` | 3243 | `ab6b97ff485d8a7f15e9aef949f26cfc30580f1693046c17fadf8f043c4bdcfc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,versions,currentEntry,projectManifest,r34LauncherSource |
| `主管/P25-P27-新主管接管-20261029/backlog-before-convergence.json` | 20297 | `c93014191a0a024b53493a29f4078bf46ebf94b2ce1c3e64af40909011b5530c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,baseline,items,noConfirmedNewProductDefectFromP25,closedStaleWork,boundary |
| `主管/P25-P27-新主管接管-20261029/baseline-verification.json` | 893 | `ea034901c633e0883993e30a60def6f67a6cdb9e3255576a062a94bf82c8b5d9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,checks,passed,failed,scope |
| `主管/P25-P27-新主管接管-20261029/continuation-HANDOFF.md` | 1818 | `f3c4846acd61fd0ce3ff498cd564d8e3c5f694cd46b7c122e47c4ffb04a43a52` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P25-P27-新主管接管-20261029/continuation-status.json` | 20298 | `e55ed7588ca8f521c83e6dc16a600f2a03c4dd48d1ffb4e67d43e2aa0e9f64b2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,formalEntry,sealedAcceptedRelease,releaseSealSha256 |
| `主管/P25-P27-新主管接管-20261029/current-finite-backlog.json` | 25519 | `aeb4947b25816d3e8e7e07c274761b68943a0271a4866c804172399499ad3770` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,baseline,items,noConfirmedNewProductDefectFromP25,closedStaleWork,boundary |
| `主管/P25-P27-新主管接管-20261029/dispatch-contracts.json` | 8145 | `5d0cc5dbd1d20a56bccfcd5cef0a11e74f26a7b8a079ef9fda3d40feb14097cc` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,utc,tasks |
| `主管/P25-P27-新主管接管-20261029/dispatch-receipts.json` | 2556 | `a95971943ff2e8474db3fc11bac31e703a8a0859ec30ea6412ca0caf18e296c9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,observations |
| `主管/P25-P27-新主管接管-20261029/finite-backlog-triage.json` | 2790 | `b2deec4cae5757088db9a8a13cb50374aa40fde2215e402202d0e090268f106f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,source,decision,items,inferredCompletionPercentage |
| `主管/P25-P27-新主管接管-20261029/integrator-rebind-dispatch.json` | 366 | `ebbb69807364c87d9fc88c076c0ae79eecc914c482ef4d2d337605edb6859b8c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,supervisor,writeRoot,basePlanSha256,state |
| `主管/P25-P27-新主管接管-20261029/isolated-r34-base-independence.json` | 1161 | `d277e82b7d6de1f3acf9ac093f42a0a466a6d3304894a0e1e5f739ff7f9d48e0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,formalEntry,formalR34Activated,r34Status,entryActivationApproval,independentBaseReason |
| `主管/P25-P27-新主管接管-20261029/latest-thread-snapshot.json` | 1645 | `5006a73593cd5c68b76b1e8b005744e126e9ae2c52cbff7529678ca699cec519` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=timedOut,wake,polls |
| `主管/P25-P27-新主管接管-20261029/p25-p27-root-acceptance.json` | 2052 | `fb82898e89105ff4ff5ab69059942de88cd0f6eb0f0fcf689e0f015f59a9e836` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,reviewer,P25,P27,wholeProjectComplete |
| `主管/P25-P27-新主管接管-20261029/p26-original-plan-review-acceptance.json` | 1234 | `86d94ea8b47e273b83a1b62ffc99732db1afe905cbb9ce249a817e06b2954557` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,reviewer,decision,utc,workerThread,workerTurn |
| `主管/P25-P27-新主管接管-20261029/p26-rebind-review-dispatch.json` | 582 | `b1929c7a4df059e7e511702eec6f9aba7bae25c841971f2bb5f22fc828e6c302` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,writeRoot,inputRoot,sealSha256,planSha256 |
| `主管/P25-P27-新主管接管-20261029/p26-rebind-root-acceptance.json` | 967 | `2c9f20b939b6217b4a60f434d85f4ee4ca922d606f2cf1c34d885eac6bb2ffaf` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,decision,supervisor,planSha256,sourceSealSha256 |
| `主管/P25-P27-新主管接管-20261029/progress-snapshot-01.json` | 6199 | `0065b21dd2e8b7e9d53356976be08b80d65eeb261a165328edb49131389e5236` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,observations,goalEvidence,decisions |
| `主管/P25-P27-新主管接管-20261029/queue.json` | 18786 | `ed855e55b3232f7f7561b1994b22549687c2c2a635b49675a4d9f5a58d82204d` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,formalRelease,productIntegrator,wholeProjectComplete |
| `主管/P25-P27-新主管接管-20261029/r34-canonical-acceptance.json` | 3791 | `b825f6ff7079c0e97a87385a0030634317b61c18aed6af0d209e13ad6ccad927` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,decision,candidateId,revision |
| `主管/P25-P27-新主管接管-20261029/r34-canonical-root-verification.json` | 3256 | `9d57e3975e72198fa2ce2814838392f3f8d283ef0827a83066bd70d60d2f2ab2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=status,planSha256,sourceFiles,outerSuccessfulCommands,innerSuccessfulCommands,nestedSuccessfulCommands |
| `主管/P25-P27-新主管接管-20261029/r34-entry-activation-acceptance.json` | 1611 | `db02c373966272a3444efb6fefe714b2984e1c6348b489287f74cdd975b64b1a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,dispatchId,decision,soleWriter |
| `主管/P25-P27-新主管接管-20261029/r34-entry-activation-dispatch-result.json` | 1065 | `17f0affca83a60b866d3178195c5630a9435b475e038288c1a34a74bd1909362` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,action,target,status,reason |
| `主管/P25-P27-新主管接管-20261029/r34-entry-activation-plan.json` | 2957 | `0da6e280ef0db1fe5657ae4ea74431cf395212cb8bead5165e0f9f3cd3cb21f1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,authorization,status,soleWriter,supervisor |
| `主管/P25-P27-新主管接管-20261029/r34-final-offline-baseline.json` | 1969 | `ac4254ea339ed35c4257e27e955f0411c617f21698a1da5bb59330a162933024` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,decision,releaseAcceptance,releaseSeal,sourceManifest |
| `主管/P25-P27-新主管接管-20261029/r34-full-release-command.json` | 744 | `dde6510c730560798401c4ff93de2f98a7bf0bd662e500f197169687bbb1075b` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=argv,cwd,startedUtc,endedUtc,exitCode,completed |
| `主管/P25-P27-新主管接管-20261029/r34-package-plan-RG2.json` | 706 | `97cc1dde4b6bc2f2f43501e993bdfd779046bb7c551ef497615e6d55ace8494f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,id,planSha256,finding,detectedBy |
| `主管/P25-P27-新主管接管-20261029/r34-package-plan-dispatch.json` | 2542 | `fdcf17860a3f692c168fee2b833d7441b046b215d5a7c7776ac5ba33baade81a` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,dispatchId,decision,toolSeal |
| `主管/P25-P27-新主管接管-20261029/r34-package-plan-root-verification.json` | 756 | `b631c165027f26f7c031d6a554fd103f1e402bb356f73efc0f29739c244f4916` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,status,planSha256,files,sourceFiles,completedSteps |
| `主管/P25-P27-新主管接管-20261029/r34-release-acceptance.json` | 5501 | `98053457e8accacbfce4618f3fe6906b04c1f17ce0483167f2ec5907085770df` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,decision,dispatchId,utc,planSha256 |
| `主管/P25-P27-新主管接管-20261029/r34-release-dispatch.json` | 595 | `8de151b2b8c6bf0bddfab1631dbd5a9bcea4eca71f3cf3ba73b5a899bd155666` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,dispatchId,target,planSha256,receiptSha256,readOnlyControllerValidation |
| `主管/P25-P27-新主管接管-20261029/r34-release-root-acceptance.json` | 2434 | `900ab8b7aadba88e2aa9fbd1161580098a381b5139441e0155c60f4a0cc2c4ba` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,decision,releaseSeal,packagePlan |
| `主管/P25-P27-新主管接管-20261029/r34-shared-acceptance.json` | 1803 | `0b96bf307f38e749143138d0930585b2bd7da6ef11d6b8a6d1ec1c66e4fc6e0c` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,supervisor,decision,authorization,planSha256,candidateId |
| `主管/P25-P27-新主管接管-20261029/r34-shared-dispatch.json` | 507 | `7e30413ffe80aeba41b59bb39c0ee074ecb683dd08f137cee98c035cb5f7ecae` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,receipt,receiptSha256,planSha256,preflight |
| `主管/P25-P27-新主管接管-20261029/reconnection-receipt-0523.json` | 1844 | `15a291c48f34a721c944fc89d27e4e08929e61f602c11130e9d131474823bdb6` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,environment,formalEntry,entryUnchanged,activationApproval,P28 |
| `主管/P25-P27-新主管接管-20261029/recovered-P28-P30-HANDOFF.md` | 1922 | `28846548ee10b15f5cb30eff4cdff6278b5861f50f42b9fe7606164ba92b82ea` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P25-P27-新主管接管-20261029/release-gate-prep-dispatch.json` | 328 | `f1d2d703f4552557f646600f4b556927567a827afbbce75b782266e1f26dcfe8` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,scope,writeRoot,sharedWrites,releaseCreation |
| `主管/P25-P27-新主管接管-20261029/release-r2-parallel-review-dispatch.json` | 358 | `5c15c2bb2acef0b8ad41c4773bf42b4e602a34367b4f3631f13a08528a3cd8d5` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,target,observedTurn,observed,scope,sharedWriteAuthorized |
| `主管/P25-P27-新主管接管-20261029/release-r2-root-pin-check.json` | 345 | `1e3a105010f6d1a56cb7b37f76b252fadcf5336157e7d497623991c6ffdfb5e0` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,artifactSeal,files,bad,reviewDispatched,toolAccepted |
| `主管/P25-P27-新主管接管-20261029/release-review-dispatch.json` | 285 | `75d13d69cead4e6d30add33665a9dce70ebd43565cd15a5d145739a89be4b68f` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=taskId,owner,writeRoot,stage,sharedMutation |
| `主管/P25-P27-新主管接管-20261029/release-rg1-repair-dispatch.json` | 758 | `78138ff78caa1519db6c4728c38a6296502915b1764a9d7c677f45bd6878be37` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,id,scope,state,author,reviewer |
| `主管/P25-P27-新主管接管-20261029/remaining-convergence.json` | 16633 | `09bbad3fba33d13aed9666356f5f5be6512dc03d482da4e6388ee04724c702d2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,rows,sourcePins,mapInventory,realMapConclusion |
| `主管/P25-P27-新主管接管-20261029/remaining-convergence.md` | 6943 | `54fab4e07cba51837b5904dd180199b2eb007a005432cc49156513f4f9685721` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/P25-P27-新主管接管-20261029/result.json` | 3432 | `c546cc323140dc8bce9753b92c145c2089f1128f7692dd5bbef2d86d571d8494` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,status,formalRevision,r34 |
| `主管/P25-P27-新主管接管-20261029/takeover.json` | 4697 | `d7f7d19b500238a3e9894ac8caae74d99d5d9e31ea0de1acc2cf100866ed3bb1` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema,utc,supervisor,authority,observations,formalRelease |
| `主管/接手封存-20261026-01a1748a/AGENTS.md` | 1855 | `6ef114a2243507cc3abef635f0afd46c882dfef38f6f64a34bee71aeca687f8f` | U — U11：文件／日誌／差異證據；尚無逐段內容豁免，unknown |
| `主管/接手封存-20261026-01a1748a/state.json` | 49880 | `d4dd8709b394ce3ab46eac3dd8499775e78a48cb26d1d2f65cb05ee2cf48b8b9` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=schema_version,supervisor_thread_id,project_id,execution_mode,current_stage,stages |
| `主管/接手封存-20261026-01a1748a/主管接續最新檢查點.json` | 41853 | `ac9a28c465eec6fab4499e3af5df492ce82278989253a61661edc385b27266a2` | U — U09：JSON 證據缺逐欄來源裁定；unknown；keys=utc,previous_goal_turn,overall_goal,current_stage,worker,host |
| `啟動引擎.cmd` | 158 | `f189ee18ac2f3881f26246cb724772ac97ba11b977f40d74b0d9c1195795c27e` | C — C01：工程程式／UI／建置設定；內容掃描未見持久化長編碼 payload，非法律來源保證 |
