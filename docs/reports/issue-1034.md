# Issue #1034 — T449 來源閉合：SPR 類素材的 recipe

日期：2026-11-13；執行者：本機 Claude（claude-local）；分支：`task/1034`；階段：resource。只新增文件，沒有改程式。

## 結論

1. **八類全部 pass**：`character`、`objects`、`players`、`shops`、`shadows`、`doors`、`ground` 與兩個 pickup 內嵌 JSON 的幀、blocks、RGB555 像素、offset、bounds、動作 ticks、events，都能只從 `Sprite.idx／pak`、`Sprite00–15` 與 Text 的 `list.spz` 重建，和本機基線全量比對沒有任何不符。recipe 寫在 `docs/reference/asset-recipes/sprites.md`。
2. **pickup 兩個 JSON 可以由同一個 recipe 產生**（動作 15 `15.get`）：13 個 group、312 幀、200,628 pixels 全部相符。每個 group 的 container、entry、payload SHA 列在 recipe 文件第 5 節（104 列）。
3. **`character` 的預覽檔產品沒有讀**：產品只讀 `blocks.json` 與 `animation.json` 的 `mapping`。96 張 PNG、2 個 GIF、contact sheet、canvas checks、entry txt 共 101 個檔建議不再產生。
4. **原本來自 SQL 的時間欄位看起來不需要 SQL**：`stepMs`、`serverActionMs`、`serverAttackMs` 與怪物的 walk／attack／spell 長度共 309 個值，全部等於 `list.spz` 的 ticks 總和 × 40 ms。本張沒有讀 SQL，這是對基線值的觀察，交 T447 定案。
5. **需要主控知道的一件事**：本機客戶端的 `Sprite03`–`Sprite14` 共 12 組 idx／pak，與產品 `ClientArchive.cs`、`GroundSpriteReader.cs` 的 pin 不符。這 12 組在 2026-11-09 11:23 被改過，檔尾多了 `22124.img`–`22159.img` 共 36 個檔。舊內容沒有變（本輪取用的 SPR 都與基線相符），但產品開這 12 包時會因 SHA 不符而拒絕。細節在 recipe 文件 §8.1。

## 做了什麼

- 讀 `CLAUDE.md`、`docs/REPORT.md`、`docs/reports/issue-1027.md`、`docs/design/portable-delivery.md`、`docs/reports/issue-102.md` 的 pickup 段落、`主管/P24-外觀來源版本選擇-20261026.md`。
- 讀產品的 `GroundSpriteReader.cs`、`ClientArchive.cs`、`TextArchive.cs`、`Afflictions.cs`（`list.spz` 解法）、`Assets.cs`、`ObjectAssets.cs`、`PlayerProfiles.cs`、`PlayerShadows.cs`、`PickupAssets.cs`，確認解碼規則與產品實際讀哪些欄位。
- 讀（沒有執行）研究階段的產生腳本，取得各類的動作編號與欄位算法。
- 寫 Python 原型（`scratch/t449/`，不入 git）：讀 idx／pak、解 Text 與 `list.spz`、解 SPR、依 recipe 重算每一類，與基線逐欄比對。
- 對 17 組 Sprite 包與 Text 的 idx、pak 全檔重算 SHA-256。
- 寫 `docs/reference/asset-recipes/sprites.md`。

## 怎麼驗證

比對對象是主目錄的 `engine/assets`（junction → `releases/r29/assets`，只讀）與 worktree 的 `engine/source/Resources/pickup-*.json`。不是抽樣，每一類的每個檔都比。

| 類別 | 檔數 | 幀 | pixels | 不符 |
|---|---:|---:|---:|---:|
| `character` | `blocks.json`、`animation.json`、96 PNG、entry txt、canvas checks | 96 | — | 0 |
| `objects` | 52 | 8,549 | 6,645,446（不含 `world-234`） | 0 |
| `players` | 38 | 19,409 | 13,695,649 | 0 |
| `shops` | 11 | 1,336 | 879,740 | 0 |
| `shadows` | 17 | 12,125 | 2,507,105 | 0 |
| `doors` | 1 | 2 | 4,648 | 0 |
| `ground` | 2 JSON＋2 PNG | 2 | — | 0 |
| pickup | 2（13 groups） | 312 | 200,628 | 0 |

另外核對：

- gfx → sprite：63 個 gfx 全部與基線相符。
- 序列：每一步的 `key`、`ticks`、`events`、`milliseconds` 全部相符。
- `entryHash` 56 個、`sourceOffset`／`spzOffset` 14 組、shops 的 `metadataSha256` 11 個：全部相符。
- `castReleaseMs`、`projectileReleaseMs` 95 個值：全部相符。
- players 的 15 筆 unavailable：逐筆重跑，失敗原因類別與基線相同。
- shadows：17 個 gfx 的 `101.shadow` 值等於 `shadowSprite`；frame keys 等於 body profile 序列用到的 key 的聯集。
- 主包優先與替代包唯一性：4,765 個 SPR 檔中 3,820 個取自主包（其中 340 個在其他包有內容不同的同名檔，未選用），945 個取自替代包（每個只出現在一個包）。
- `list.spr`、`list.spz` 解碼後的 SHA 與研究階段留下的相同。

過程中遇到三次不符，都是原型的問題，修正後歸零，並寫進 recipe 文件 §8，因為 C# 實作會踩到同樣的地方：

- 找動作的正規表示式會錯配（gfx 0 的 die）。
- `1094-1.spr` 有一種產品目前不接受的空幀表頭。
- gfx 11722 只在 `list.spz`，不在 `list.spr`。

## 未驗證什麼

- **沒有執行任何編譯出的程式**，也沒有建置。Sprite03–14 pin 不符對產品畫面的實際影響沒有驗證（只從原始碼判斷會被拒絕）。
- **Sprite03–14 是誰改的、為什麼改**：沒有查。只確認修改時間與多出的 36 個檔名。
- PNG、GIF、JSON 的檔案 bytes：新 recipe 的編碼與序列化會不同，沒有比；比的是解碼或解析後的值。GIF 與 contact sheet 的內容沒有比。
- 「伺服器時間 = ticks × 40」只對基線有的 309 個值成立，沒有對 SQL 全表驗證（本張不讀 SQL）。
- `magic-167` 的方向數（4）是否等於 entry 表頭的 count：只有這一個例子，是推測。
- 要產生哪些 gfx、哪些 weapon 的清冊（原本來自 JAR 常數、SQL spawn、研究時的人工選擇）不在本張範圍，屬於 T447。`objects/names.json` 屬於 T444。
- 沒有宣稱任何資源缺少，所以沒有用 PakViewer 查 pak。
- CI 結果以 PR 為準，push 當下尚未取得。本張只新增兩個 Markdown 檔。

## 產出路徑

- `docs/reference/asset-recipes/sprites.md`
- `docs/reports/issue-1034.md`
- 原型與比對輸出：`scratch/t449/`（不入 git；`results.json` 是逐檔結果）

## 需要主控或使用者決定

1. **Sprite03–14 的 pin**：本機客戶端已經和產品 pin 不一致。要更新 pin、還原客戶端，還是改成逐 entry 驗證，需要裁定。recipe 文件建議 B15 的準備器改成逐 entry 記檔名、大小、payload SHA，不 pin 整個 pak。這也關係到可攜版能不能接受別人的客戶端。
2. **`character` 預覽檔不再產生**：需要 T467 的新 manifest 不再列這 101 個檔，否則 `Assets.VerifyIntegrity` 會因缺檔失敗。

## 給下一棒的提示

- **T454／T455（SPR 類準備器）**：照 recipe 文件 §3 與 §4 實作。三個坑在 §8：空幀判定要放寬、動作要先切 token 再比編號、動畫表用 `list.spz`。`GroundSpriteReader.DecodeCore` 的空幀條件太窄，不能直接重用。
- **T447（動作時序與 gfx 清冊）**：先驗「ticks × 40」能否取代 `spr_action`。基線的 `sourceActions[].sql` 與 `serverSpeedSource` 記了原 SQL 的 framecount／framerate，可以直接對。
- **T451／T452（核心與契約）**：決定輸入驗證的單位（整包或逐 entry）時，請看 §8.1。§8.1 附了 12 組目前的 SHA。
- **T463（pickup）**：recipe 在 §5。第 0 步的幀來自 body profile，不要重複產生；`frames` 順序是方向 0–7、每個方向第 1–3 步。
- **shadows 要排在 body 之後產生**；frame keys 取 body 序列實際用到的，不是 body 的全部幀。
- 輸出的 `source_offset`、`block_index` 產品沒有讀，新 schema 可以拿掉。
- 重跑比對：`python -I -X utf8 scratch/t449/compare.py scratch/t449/results.json`，約 10 分鐘。腳本只在這個 worktree 的 `scratch/`，worktree 清掉就沒了；需要的話從本報告與 recipe 文件重寫，算法都在文件裡。
