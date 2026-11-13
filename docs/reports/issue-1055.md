# Issue #1055 — T470 建置與唯讀前置核驗（未完成等價驗收）

日期：2026-11-14（台北）。執行者：codex-local，第 1 次嘗試。分支：`task/1055`。受驗來源 commit：`b581cca60d5881fd641a6395c6fd51c5f0cb68b6`。

## 結論與執行限制

**T470 尚未通過，需由調度器處理執行限制衝突後續跑。** 本次完成建置、基線完整性、原檔來源逐 byte 比對與客戶端容器前後 SHA；沒有執行 C# 準備器，不能證明其輸出等價。

本次派工同時要求執行 `P11.ClientEngine.exe --prepare-assets`，又在「本機執行限制」明文規定：**「不得在本機執行任何編譯出來的程式」**，且包含 **「P11.ClientEngine.exe（含 --module-tests 等所有參數）」**。因此未將資源／逆向工具例外擴張為允許執行產品。這是本次使用者訊息的限制，不是缺素材、不是 C# 執行失敗，也不是自動審批拒絕。

沒有啟動遊戲、GUI、服務、DB 或正式連線；沒有修改原客戶端、原專案、封存、junction、系統設定。沒有 checkout／建立分支、沒有遞迴刪除。原專案只讀。`docs/REPORT.md` 與追蹤的 `rebuild-lock.json` 不變。

## 做了什麼／怎麼驗證

1. 讀取 `AGENTS.md`、`docs/REPORT.md`、`TASKS.md` T470、`docs/design/asset-recipe.md` 與準備器入口及建置腳本；fetch 後確認分支與 `origin/main` 的起點一致。
2. 計算客戶端根目錄全部 19 組容器（38 個 `.idx`／`.pak`，9,186,505,456 bytes）的 SHA-256。
3. 使用 .NET SDK 10.0.300，執行以下建置（沒有執行產物）：

```powershell
python -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/issue-1055/NuGet.Config --output scratch/issue-1055/build
```

`NuGet.Config` 是 scratch 內的臨時設定，只讀原專案 `engine/vendor`；套件快取設在本 worktree `engine/.nuget`。呼叫端設定 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`，建置腳本同時將 CLI home、TEMP、TMP 放在工作樹內。

- 結束碼 **0**，狀態 `pass-dev-build`；包含 restore 的總耗時 **36.97 秒**。
- 產品／audio 建置：4 警告、0 錯誤（HtmlActionTests.cs CS8602；MagicUi.cs CS8600、CS8602；MapResources.cs CS0649）。launcher：0 警告、0 錯誤。
- 555 個來源檔、564 個鎖定輸入。既有 lock 落後，按 #646 使用記憶體臨時 lock，沒有重產／提交 lock，也沒有 `--write-receipt`。
- product DLL SHA-256：`1642ea357d5300fcdac8f5b40e6595ba3504c17aa10b830d8fd6816041726e93`。
- audio DLL SHA-256：`4c4aa97837e7c4e610d3e0b75eb2b619faf41508096435e37c635c2cec456473`。
- launcher DLL SHA-256：`fe3df5035e39578e1b98468d308e513c5ff7cb1678f59a5cf085315339f7266f`。

4. Python 以 `rb` 讀取基線，將 integrity 每列 size／SHA 與實檔核對；再把下列原檔與目前客戶端逐 byte 比對。Tile 使用目前 idx 的名稱查詢，沒有沿用基線的舊 File Offset／record。資料只留在記憶體，不解出 pak／map／sprite 到專案。
5. 全部唯讀核驗後，再計算同一組容器的 SHA；38／38 相同、0 個變更（集合、大小、SHA 全同）。**這只證明本次建置與唯讀核驗期間不變，不是準備器執行前後證據。**

## 逐類數字：基線完整性與來源核驗

基線：`D:\lineage開發\TW1608262200研究\engine\assets`，實際指向 r29。基線 integrity SHA-256：`07dd19f4407ec3f2bf61da9c00cca827cbbe206866863a87a77f17fce7a18b98`。

以下「相符」是 r29 實檔對其既有 integrity 的核驗，**不是準備器新產物對 r29 的等價比對**。合計 13,957 檔、2,222,723,085 bytes；相符 13,957、不符 0。

| 類別 | 基線檔數 | 基線 SHA 相符 | 基線 SHA 不符 | C# 等價比對 |
|---|---:|---:|---:|---|
| `audio-manifest.json` | 1 | 1 | 0 | 未執行 |
| `cache` | 378 | 378 | 0 | 未執行 |
| `character` | 103 | 103 | 0 | 未執行 |
| `dialogs` | 2 | 2 | 0 | 未執行 |
| `doors` | 2 | 2 | 0 | 未執行 |
| `encountered-monsters.json` | 1 | 1 | 0 | 未執行 |
| `ground` | 4 | 4 | 0 | 未執行 |
| `inventory` | 20 | 20 | 0 | 未執行 |
| `magic` | 1 | 1 | 0 | 未執行 |
| `maps` | 13,304 | 13,304 | 0 | 未執行 |
| `motion.json` | 1 | 1 | 0 | 未執行 |
| `objects` | 54 | 54 | 0 | 未執行 |
| `players` | 39 | 39 | 0 | 未執行 |
| `scene` | 13 | 13 | 0 | 未執行 |
| `shadows` | 18 | 18 | 0 | 未執行 |
| `shops` | 12 | 12 | 0 | 未執行 |
| `sound` | 2 | 2 | 0 | 未執行 |
| `terrain0.bin` | 1 | 1 | 0 | 未執行 |
| `world-appearances.json` | 1 | 1 | 0 | 未執行 |
| pickup（不在 v1 integrity） | — | — | — | 未執行；312 幀待驗 |

原檔來源比對（實際 Python bytes 比較）：

| 類別 | 已比檔數 | 相符 | 不符 | bytes |
|---|---:|---:|---:|---:|
| `cache/map` | 20 | 20 | 0 | 2,429,620 |
| `cache/tiles` | 353 | 353 | 0 | 34,445,943 |
| `maps/map` | 7,198 | 7,198 | 0 | 828,455,751 |
| `maps/ship` | 3 | 3 | 0 | 709,197 |
| `maps/tiles` | 5,052 | 5,052 | 0 | 555,746,825 |
| `sound` | 2 | 2 | 0 | 39,070 |

Tile.idx 實際解析 5,446 筆，核對索引長度、名稱不重複與每筆範圍不超過 pak。沒有宣稱缺資源，沒有更動 pin；本輪不需要以 PakViewer 查缺件。

## 未驗證什麼

以下均為 **未執行**，不是 0 個差異的通過結果：

| T470 驗收項目 | 本機已執行數 | 狀態 |
|---|---:|---|
| 真客戶端全量 C# 準備 | 0／2 次 | 待驗 |
| 一般路徑與含空格中文路徑的全樹／integrity／收據逐 byte 比對 | 0 組 | 待驗 |
| JSON frames、RGB555、bounds、ticks、events、時間、集合及順序 | 0 組 | 待驗 |
| inventory 20 張、ground 2 張，對執行期 decoded RGBA | 0／22 張 | 待驗 |
| terrain bounds、逐格位元與已知差異範圍 | 0／713 張 | 待驗 |
| pickup | 0／312 幀 | 待驗 |
| scene 三檔、336 個特效檔及 names 的新輸出逐 byte 比對 | 0 組 | 待驗 |
| 程式規則（混色、motion、技能、音效載入順序等） | 0 組 | 待驗 |
| 缺檔、錯版本、截斷、取消後續跑、同時兩個 writer、目標 junction | 0／6 類 | 待驗 |
| 每 recipe 耗時、active 樹大小、首次新 gfx 停頓 | 0 組 | 待量測 |
| D3 額外地圖、tile／特效集合差異 | 0 組 | 待驗 |

現有 CI 的 `native-ui-resources` 群組已掛載 `asset-prep`／`asset-prepare-command` 等合成測試；`AssetPrepChecks.cs` 包含缺檔、截斷、錯誤 Text 錨點、取消續跑、writer 互斥及 junction。其結果僅代表合成契約測試，不能填入上表真客戶端驗收。PR 最新 CI 由 GitHub Actions 執行，交付摘要列實際結果；本機沒有執行任何 Checks、`dotnet run` 或 `dotnet test`。

## 耗時與磁碟用量

| 作業 | 秒 |
|---|---:|
| 客戶端容器前置 SHA | 43.09 |
| rebuild（含 restore） | 36.97 |
| r29 全量 integrity 核驗 | 123.27 |
| 原檔與客戶端逐 byte 比對 | 81.45 |
| 客戶端容器後置 SHA | 51.68 |

以上為各作業 wall time，建置與基線讀取曾並行，不能相加當總耗時。下表是核驗完成時的檔案邏輯大小，不是 NTFS 配置量或峰值；scratch 數字在產生報告前取樣。準備器輸出與 active 樹均未建立，不能以 0 bytes 當其需求。

| 路徑 | 檔數 | 邏輯 bytes |
|---|---:|---:|
| `engine/build` | 16 | 12,689,176 |
| `engine/launcher` | 5 | 223,597 |
| `engine/source/obj` | 21 | 14,189,051 |
| `engine/audio/obj` | 17 | 243,480 |
| `engine/_integration/dev/launcher-source/obj` | 19 | 293,824 |
| `engine/.nuget` | 90 | 59,947,459 |
| `scratch/issue-1055` | 31 | 72,206 |

## 產出路徑

- `docs/reports/issue-1055.md`：本報告。
- `docs/reports/issue-1055-evidence.json`：容器前後 SHA、基線／原檔逐類數字、建置結果、來源 pins 與磁碟用量；只有 metadata，沒有素材內容。
- `docs/plans/issue-1055.json`：空陣列。已執行的核驗未發現不符；未執行的類別不能冒充失敗而開修正任務。既有 #1055 應保持未完成，避免另開重複 T470。
- 不入 git：`scratch/issue-1055/audit.py`、`build.py`、`collect.py`、`report.py`、前後容器清單、`audit.json`、`build.log`、`build-summary.json`、`build/` 子命令輸出；編譯產物在 `engine/build/client-product-dev` 與建置腳本既定的 `engine/launcher`。未複製客戶端素材。

## 給下一棒的提示

1. **調度器／主控先處理 runner 執行限制衝突**，再把本 Issue 交給有明確授權執行產品準備模式的本機 runner（任務指定 codex-local，備援 dot）。本次限制仍適用時不能只換名稱繞過；不需要啟動遊戲，也不需要使用者手動跑測試。不要因本 PR 建置或 CI 綠燈結束 T470／T475。
2. 準備入口在 `AssetPrepareCommand.TryRun`，只接受單獨的 `--prepare-assets`。`engineRoot` 從 `AppContext.BaseDirectory/../..` 推導，**不讀 `TW_ENGINE_ROOT` 作輸出覆寫**。下一棒需部署完整產品檔案至兩個各自的 `<root>/build/client-product-dev/`，例如 worktree `scratch/issue-1055/run-a/`、`scratch/issue-1055/中文 含空格/run-b/`；輸出為各自 `<root>/assets` 與 `<root>/runtime`。不要把原專案 assets junction 當寫入目標。
3. 故障注入只對合成 fixture 或隔離副本做，不能截斷／改名／修改真客戶端。兩 writer 應實際驗跨程序鎖；取消須保留 staging 再續跑；junction 只用工作樹內的新 sentinel 目錄，拆連結只能用不含 `/s` 的 `cmd /c rmdir`。不得遞迴清理 worktree。準備器本身有 staging／candidate 清理流程，執行前也需確認符合本次工作樹禁止遞迴刪除的要求。
4. 比對依 `asset-recipe.md` §1.4、§2、§3、L3、D3：terrain bit0–3／bit6–7 與 bounds 必須全同，bit4–5 僅允許 map 42:13075、4:304、0:112、244:26、2101:21、2151:21、70:7、440:1 格以內；cache map0 最多 112 格。文字模板 8720 筆相同、176 筆限定圖片號差異，另加 `lyra_y01`。這些是契約門檻，**本輪未重新驗證**。
5. 嚴格比對指定 B 檔、S 所有消費欄位、P 尺寸／offset／RGBA、G 每格位元及 R 程式輸出；13 個 recipe 的 receipts／integrity 在兩路徑間也必須逐 byte 相同。6016 pin 不可為通過驗收而改。每個實際不符類別再填一筆修正任務至本 Issue 的 plans JSON。
6. 本次核驗可重跑 `python -B -X utf8 scratch/issue-1055/audit.py before`、`audit`、`after`；此 scratch 腳本不隨 PR 保存，調度器清理工作樹前可依需要留存。交付的 metadata 與工具 SHA 可核對當次證據，不能取代未來實跑。
