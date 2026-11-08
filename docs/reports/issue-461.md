# Issue #461 — ui-core-resources（C1）：原生 UI 資源層

階段：code，第 1 次嘗試；runner：claude-local；分支 `task/461`。
依據：[`docs/design/native-ui.md`](../design/native-ui.md) 第 2 節；並採用之後合併的 V4 查證（[`docs/reference/ui-layout.md`](../reference/ui-layout.md)「框架層細節（V4，Issue #459）」）中已驗證的規則。

## 做了什麼

新增 `engine/source/NativeUi/`（命名空間 `P06.NativeUi`，csproj 預設 glob 自動編入，不改 csproj）：

| 檔案 | 內容 |
|---|---|
| `ClientArchive.cs` | `ClientArchive`：共用的唯讀 idx/pak 讀取器（`FileShare.Read`）。檢查：idx SHA-256 pin、`筆數×28+4 == 檔案大小`、`offset+size ≤ pak 大小`、名稱只能是可列印 ASCII 且不得為空、同一個 idx 內不得重名、單一 entry 1 byte–8 MiB；第一次讀取時檢查 pak 的大小與 SHA-256 pin。`UiResourcePins`：Tile 與 Sprite／Sprite00–15 的 pin 表。另含 `UiResourceException`、`UiResourceLog`（只供除錯）。`GroundSpriteReader` **沒有改** |
| `UiXmlSource.cs` | `X?xm` 解密：用 `Aes.DecryptEcb` 逐塊做 CBC，尾段不足 16 byte 的部分直接與目前 IV 做 XOR，輸出 `"<"+blob[1..4]+plain`。檔名先找 `<名>-c.xml`，找不到再找 `<名>.xml`，退回時記一行 `ui-xml-fallback`。文字解碼：有 `encoding` 宣告就照宣告；沒有宣告時用 UTF-8（嚴格模式），不合法的 byte 一律拒絕 |
| `UiLayout.cs` | `UiLayout.Parse/Load`、`UiNode`（`Name`、`Attr`、`RequiredAttr`、`Int`、`TryInt`、`Surfs`、`Children`、`ChildrenNamed`、`Child("A/B")`）。缺節點、節點不唯一、缺屬性、非整數，都丟出帶檔名與路徑的 `UiLayoutException`，不補預設值。元素名稱照原文保留 |
| `UiSurface.cs` | `.img` 解碼成 `UiImage`（ARGB）→ `Bitmap`（32bpp PArgb）。RGB555，R 在高位，每個通道 `v*255/31`。必須滿足 `8+寬×高×2 == size` |
| `SurfCatalog.cs` | `SurfCatalog`：讀 SurfList.xml（同樣走 `-c` 退回），依 BIN 的查找順序找圖，解碼結果放記憶體 LRU（32 MiB）。`Get(surf)` 找不到時回傳 null，同一個 id 只記一次日誌。`UiResources`：把一個客戶端根目錄的 Tile、17 個 Sprite、XML 來源與 SurfCatalog 包在一起 |

測試：
- `engine/_integration/dev/native-ui-tests/ResourceChecks.cs`：接進 `DemoChecks.csproj`（加 1 行）與 `demo-tests/Program.cs`（新分組 `native-ui-resources`，並加進預設全組）。
- `.github/workflows/build.yml`：Windows job 加一步 `native-ui-resources`。這不在設計文件列出的檔案範圍內，理由是讓合成測試在 CI 上跑；這一步不讀客戶端。
- `rebuild-lock.json`：用 `rebuild.py --update-lock` 重產。

### 與 Issue 原文不同之處（採用 V4 已驗證的規則）

Issue 文字寫於 V4 合併前。V4 已經從 BIN 查證，並在 ui-layout.md 寫明「這項會影響 C1」，所以改採 V4：

| Issue 原文 | 本次實作 | 依據 |
|---|---|---|
| 沒有宣告時用 CP950 | 沒有宣告時用 **UTF-8**；有宣告就照宣告（測試包含 big5 宣告） | V4 (c)：BIN 用 expat、不轉 CP950；65 個檔裡 62 個是 ASCII、3 個是 UTF-8 |
| 17 個 idx 合成一個聯合索引；同名且內容不同就拒絕 | 依 `HashFileManager::Read` 的順序找：`Fc.img`＠`Sprite{h}` → `Fc.img`＠`Sprite` → `F.img`＠`Sprite{h}` → `F.img`＠`Sprite`，其中 `h = 名稱 byte 和 % 16`。重名時以 `Sprite{h}` 為準；同一個 idx 內重名仍然拒絕 | V4 (e)；ui-layout.md 寫明「不再需要重名拒絕規則」 |
| 透明色一律當色鍵 | **旗標≠0** 才把等於透明色的像素設成 alpha=0；旗標=0 時整張不透明 | V4 (a) |

## 怎麼驗證

1. **合成離線測試**（不讀客戶端；fixture 由程式產生，寫在 `scratch/issue-461/synthetic/`）：
   `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release -- . scratch/issue-461/resource-checks.json native-ui-resources`
   → `PASS 539 DEMO checks`。其中本分組 212 項，其餘 327 項是每次都會跑的 party／pets／polymorph 基本組。內容：
   - **解密**：本體長度 0–64，涵蓋所有尾段長度 0–15。測試端用 `EncryptEcb` 自己加密再解回；完整區塊的情況另外和函式庫的 `DecryptCbc` 交叉比對。不是 `X?xm` 開頭、長度太短都會拒絕。
   - **編碼**：無宣告 UTF-8；宣告 big5；BOM；無宣告的 CP950 byte 拒絕；不認識的編碼宣告拒絕。
   - **版面**：`=` 兩側有空白、`FOOD_GUAGE_BAR` 原文、`5726~5728` 範圍、`yPos`。缺節點／不唯一／缺屬性／非整數／範圍顛倒／範圍過大／負數，都丟出帶檔名與路徑的例外。
   - **Tile**：`-c` 優先、退回時記 `ui-xml-fallback`、兩種名稱都沒有就拒絕、路徑式檔名拒絕。
   - **idx/pak 邊界**：筆數與大小不符、idx 截斷、越界、不可列印字元、空名稱、同一個 idx 重名、idx pin 不符、pak pin 不符、pak 截斷、entry 超過 8 MiB、size=0、entry 屬於別的封存檔。
   - **`.img`**：色序、`v*255/31`、旗標=1 時色鍵、旗標=0 時不透明、PArgb 格式、截斷、多餘 byte、標頭太短、尺寸為 0。
   - **SurfCatalog**：c 版優先、`Sprite.pak` 的 c 版優先於 `Sprite{h}` 的原版、重名時 `Sprite{h}` 勝出、退回 `Sprite.pak`、放在非 h 的 `SpriteNN` 讀不到、`id≠file` 照 file、沒有 file 的列回傳 null、缺件回傳 null 且只記一次日誌、壞掉的 `.img` 拒絕、LRU 淘汰順序與容量、SurfList id 重複拒絕、Sprite／Tile pin 不符拒絕。
   - **雜湊**：`h` 與 T448 抽查的 6 個封存檔位置一致（3168→13、3176→12、2273→9、3204→4、4313→6、2818→14）。
2. **本機選用檢查**（`TW_CLIENT_ROOT=D:\lineage開發\0-客戶端`，只讀；輸出放在 `scratch/issue-461/real/`）→ `PASS 550`，多出的 11 項是真實客戶端檢查：
   - `Tile.pak` 的 **65 個 XML 全部解密並解析成功**，1,859 個 `surfNumber` 全部可以解析。
   - `uistatus86` 讀到 `-c` 版，`BookmarkUI` 退回無後綴檔。
   - 尺寸：

     | surf | 實際讀到 | 封存檔 | 尺寸 | 旗標／透明色 | 原版 `F.img` | ui-layout.md |
     |---|---|---|---|---|---|---|
     | 3168 | `3168c.img` | Sprite00 | 135×125 | 1／`0x324C` | Sprite13，135×125 | 135×125 ✔ |
     | 3176 | `3176c.img` | Sprite15 | 157×124 | 1／`0x324C` | Sprite12，157×124 | 157×124 ✔ |
     | 3279 | `3279c.img` | Sprite03 | 362×240 | 1／`0x324C` | Sprite00，362×240 | 362×240 ✔ |

   - 目視 `surf-3176-3176c.img.png`：傳統金框、4×2 格、沒有 F5–F12 字樣，與 V4 (e) 和 V5 (a) 的描述一致。
3. `python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock` 與 `--build`：合併 origin/main（含 C3 #462）之後重產 lock，230 個來源檔、239 個鎖定檔；`--build` → `PASS dev`，productDll `12a56cef…12fe`。合併後重跑 native-ui-resources（合成 539、選用 550）、combat-admission、item-status、doors，全部 PASS。
4. 既有 demo 分組（CI 會跑的）：map-continuity 32953、combat-admission 1686、item-status 409、afflictions 580、polymorph-cadence 380、buddies 496、item-transfer 400、doors 648，**全部 PASS**。
5. `engine/tools/check-no-game-assets.py` → `pass, errors 0`。新增的檔案只有 `.cs`；沒有任何 `.img`、`.xml`、`.png` 或像素進 git、embedded resource 或 `engine/assets`。

## 已驗證／推測／待驗證

**已驗證（本輪）**
- 金鑰／IV 與尾段 XOR 的實作正確：真實的 65 個 XML 全部解出合法 XML，其中 62 個的尾段不足 16 byte。
- 真實 `Tile.idx` 第 5420 筆 `ReNStatusWinUI-c.xml` 剛好 20 byte、**沒有 NUL**。reader 允許名稱填滿 20 byte，與 `ui_xml_decrypt.py` 的行為相同。既有的 `GroundSpriteReader` 會拒絕這種名稱，但它不讀 Tile。
- **SurfList 更正**：6,653 列裡，6,310 列有 `file`，而且**全部 `id == file`**；另外 343 列（id 6310–7246）**沒有 `file` 屬性**，只有 `filedesc`。設計文件與 ui-layout.md 說的「343 筆 `id ≠ file`」，實際上就是這 343 列沒有 file。程式仍然一律用 `file`，不用 id。
- 4 個檔（`uistatus86-c.xml`、`uistatus86.xml`、`uistatus.xml`、`lineage2.xml`）的 `RIGHT_WINDOW/ETC/MACRO_ICON` 是 `surfNumber=""`，`Surfs()` 回傳空陣列。
- 3168／3176／3279 的 c 版位置符合雜湊規則：例如 `3168c.img` 的 byte 和為 672，672 % 16 = 0，確實在 Sprite00 找到。

**推測**
- 沒有 `file` 的 343 列，BIN 怎麼處理沒有查（可能用 0 或略過）。目前一律回傳 null；UI 用到的 surf（2000–5800）不受影響。
- `surfNumber=""` 在 BIN 是 `atoi("")=0`。巨集圖示應該是執行期依內容決定，所以這裡當作不畫。

**待驗證**
- UI 實際用到的 surf 裡，c 版與原版尺寸不同的有哪些（V4 說全體有 585 張）。本輪只核了 3 張，3 張都相同。
- 冷啟動時 SHA-256 的耗時：Sprite.pak 有 1.9 GB，第一次讀取會整個雜湊一次。本機選用檢查全程約 15 秒（包含 Tile 與 4 個 Sprite pak）。C2 是否需要背景預熱或磁碟快取，交給 C2／量測決定。
- 本輪沒有做任何真機驗收，也沒有接上 UI（C2 的範圍）。

## 產出路徑

- `engine/source/NativeUi/ClientArchive.cs`、`UiXmlSource.cs`、`UiLayout.cs`、`UiSurface.cs`、`SurfCatalog.cs`
- `engine/_integration/dev/native-ui-tests/ResourceChecks.cs`
- `engine/_integration/dev/demo-tests/DemoChecks.csproj`、`demo-tests/Program.cs`（新分組 `native-ui-resources`）
- `.github/workflows/build.yml`（Windows job 新增一步）
- `engine/_integration/dev/rebuild-lock.json`（重產）
- 本機證據（不進 git）：`scratch/issue-461/resource-checks.json`、`real-checks.json`、`real/real-client.json`、`real/surf-*.png`、`build-evidence/`、`surflist_probe.py`

## 給下一棒的提示

- **C2（ui-core-canvas）**：
  - 用 `UiResources.Open()`（或 `UiResources.Shared`）取得 `Xml` 與 `Surfs`。
  - 任何 `UiResourceException`（含子類 `UiLayoutException`）都代表 pin 不符或資料錯誤，依設計應該停用原生 UI 並退回舊 HUD。
  - 第一次 `Get` 某個 Sprite pak 時會雜湊整個 pak，耗時數秒，建議在背景執行緒預熱。
  - `UiSurface.Bitmap` 屬於快取，呼叫端**不可 Dispose**，也不要跨幀保留。
- **模組任務**：
  - 按鈕狀態圖用 `node.Surfs()` 取得範圍，再依 V4 (b) 的表選 `a`／`a+1`／`a+2`。
  - `Surfs()` 回傳空陣列，或 `Get` 回傳 null 時，都不畫。
- **文件維護者**：建議把 `native-ui.md` §2.3 與 ui-layout.md §1 的「343 筆 `id ≠ file`」更正為「343 筆沒有 `file` 屬性」，並把 §2.2 的 CP950 預設改成 UTF-8（V4 已更正）。本輪依任務範圍沒有改這兩份文件。
- **本機環境**：worktree 沒有 `engine/assets` junction，所以 demo-tests 的預設全組會在 `Movement123` 找不到 `terrain0.bin`。這和本次變更無關；CI 只跑分組。
