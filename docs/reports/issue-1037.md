# Issue #1037 — T452 設計：recipe 契約與已知限制清單（b15-recipe-contract）

日期：2026-11-13；執行者：本機 Claude（claude-local）；分支：`task/1037`；階段：design。只改文件，沒有改程式。

## 結論

1. 契約寫在 [`docs/design/asset-recipe.md`](../design/asset-recipe.md)。19 類素材加 pickup **沒有任何一類整類受阻**；每一類都定了做法（準備器產生、執行期直讀、或改成程式規則）、輸出路徑、schema 與 T470 的比對方式。
2. 受阻的是個別表與欄位，列成已知限制 L1–L7，每項寫了缺什麼、對遊戲的影響、暫行做法與建議，全部交主控（`needs:claude`）：
   - L1 EXP 門檻表、L2 `ground-catalog.json` 的模板列：真正需要選擇的兩項，T464 要等裁定。
   - L3 terrain 區域位元（8 張地圖 13,567 格，產品不讀）、L4 `maps[].name`：有暫行做法，T458、T456 不用等。
   - L5 伺服器 `spr_action` 無法由客戶端驗證、L6 154 個地圖特效不可用、L7 15 筆玩家 profile 不可用：知悉即可，後兩項是 r29 基線既有的狀況。
3. 另有 8 個不屬於受阻、但要主控決定的做法選擇 D1–D8。其中 **D1 影響現在的產品**：本機客戶端的 `Sprite03`–`Sprite14` 在 2026-11-09 被附加了 36 個 `.img`，產品執行期對這 12 包的 pin 已經不符，照原始碼會被拒絕（T449 發現，實際影響沒有驗證）。
4. 本文件自己定案的幾件事：
   - 準備器的輸入驗證不再用整包 pak 的 SHA，改成結構檢查、少數錨點 entry、逐 entry 記錄。T451 核心現在的 17 容器 pin 在這台機器上會直接拒絕，由 T454 改。
   - gfx 範圍照 T447 的建議採「遇到再準備」：首次只準備一份預熱清單（等於 r29 基線的成員），執行期按需準備另開 T477、T478。
   - 地圖原檔、TIL、LAC、Sound 採執行期直讀，不複製；`motion.json`、三張混色表、`skills.json`、`magic/catalog.json`、`audio-manifest.json` 不再是檔案。
   - 新的 integrity 是 `tw-assets-integrity-v2`，`files[]` 的三個鍵與 v1 相同，六個只查 `files[]` 的讀取點不用改；v1（r29 基線）不動。
   - recipe 共 13 個，先後分三層（例如 shadows 在 players 與 objects 之後、maps 在 terrain 之後）。
5. `docs/design/portable-delivery.md` 加了一節 2026-11-13 修訂，並在過時的句子原位加註記。

## 做了什麼

- 讀 `CLAUDE.md`、`docs/REPORT.md`、#1027 報告與規劃檔、T444–T451 的 8 份報告（#1029–#1036）、7 份配方文件、`docs/design/portable-delivery.md` 全文；用 `gh` 讀 #1027、#1029–#1036 的留言，確認主控還沒有對任何已知限制下裁定。
- 讀原始碼核對契約會動到的地方（只讀）：七個用 `integrity.json` 驗檔的讀取點、`AssetPrepCore.cs` 的 recipe 介面、`AudioAssets.cs` 驗 manifest 的方式、`engine/tools/game-assets-allowlist.json` 的 B15 例外現況（7 列）。
- 讀主目錄基線 `engine/assets` 裡四個小檔的**鍵名結構**（`encountered-monsters.json`、`world-appearances.json`、`objects/manifest.json`、`audio-manifest.json`），用來確認清冊檔的欄位；沒有複製任何內容。
- 新增 `docs/design/asset-recipe.md`（11 節）。
- 修訂 `docs/design/portable-delivery.md`：新增「2026-11-13 修訂」一節（junction 指向 r29、22 張 web PNG 已移出、六項前置的結論表、工作量），另有 6 處原位註記。
- `TASKS.md`：M8 區段加一段來源閉合結果；T454–T462、T464 的標題照契約改範圍；新增 T476–T478 三列。
- `docs/reference/INDEX.md`：補上 `asset-recipes/` 的索引列（T446 報告請 T452 補）。
- 新增 `docs/plans/issue-1037.json`（3 項）。

## 怎麼驗證

- 契約裡的數字逐一對回來源文件；自己加總核對過的有：地圖原檔 7,198＋5,052＋3 = 12,253，L3 八張地圖的格數合計 13,567，總流程 recipe 13 個。
- `docs/plans/issue-1037.json` 用 Python 驗過：是陣列、3 項、欄位齊全、id 不重複、`depends_on` 都指向同檔內的 id、沒有循環。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：結果見下方「已驗證」。
- 本張只改 Markdown 與一個 JSON 規劃檔，CI 結果以 PR 為準。

### 已驗證

- 第 2 節每一類的閉合結論、第 7 節 L1–L7 的數字：引自 T444–T450 的報告與配方文件（都是 Python 原型對 r29 基線的全量比對）。本張沒有重跑。
- 讀取點與行號（契約 4.3）：本張讀原始碼確認。
- 主控尚未裁定任何已知限制：`gh issue view` 的留言裡只有 #1027 的五項裁定（M8 驗收、分母、T466、T470 runner、不建 Release）。
- `check-no-game-assets.py`：`status: pass`，errors 0（加入新檔後 4,678 個追蹤檔）。

### 推測

- integrity v2 的格式能讓六個讀取點完全不用改：依據是它們只用 `files[]` 的 `path`／`size`／`sha256`，沒有逐一試。
- 新 active 樹約 245 MB（地圖衍生檔）加 0.5 GB（SPR 類 JSON）：從基線檔案大小推的。
- 不收 6 個檔名不合規的 `.s32` 之後，`tiles[]` 與特效 id 集合不變或只變小：沒有算過，已要求 T456 回報。
- 調度器會把 `stage: plan`、`runner: claude-chat` 的 T476 交給主控：比照先前的主控任務寫法，沒有確認過調度器怎麼解析。

### 待驗證（本張沒做）

- C# 準備器的全部輸出與基線的等價（T470）。
- C# 能不能讓 `6016.json`、scene 三檔、`names.json` 逐 byte 相同。
- D1：產品執行期在本機客戶端下開 `Sprite03`–`Sprite14` 的實際表現（需要 Codex 或 dot 實機）。
- D2：602／603 的畫面；L5：基線以外姿勢的伺服器間隔。
- 沒有建置、沒有執行任何編譯出的程式；沒有讀客戶端、SQL、JAR、DB；沒有用 PakViewer（本張沒有新宣稱任何「缺資源」，L1、L2、L6 的查詢紀錄在各自的配方文件）。
- 沒有改 `PLAN.md`：新增三張任務是否計入主線分母要主控決定。

## 需要主控決定（`needs:claude`）

全部列在 `docs/design/asset-recipe.md` 第 7、8 節，已另開 T476（`b15-limits-ruling`）承接。最需要先看的三項：

1. **L1、L2**（EXP 門檻表、可丟棄白名單）：T464 等這兩項。建議分別是「拿掉表、EXP% 空白」與「拿掉白名單、由伺服器裁定」，兩者都是行為變更。
2. **D1**（執行期 Sprite pin 與本機客戶端不符）：不屬於 B15，但現在的產品可能已經受影響。
3. **D8 與分母**：T477、T478 要不要做、三張新任務算不算主線。

## 產出路徑

- `docs/design/asset-recipe.md`（新）
- `docs/design/portable-delivery.md`（修訂）
- `docs/plans/issue-1037.json`（新，3 項）
- `docs/reference/INDEX.md`（加一列）
- `TASKS.md`（M8 區段）
- `docs/reports/issue-1037.md`（本檔）

## 給下一棒的提示

- **T453–T463 各張**：先讀契約第 9 節自己那一列，再讀第 3 節對應小節；與 Issue 原文不同的地方以契約為準。
- **T454 要先做輸入驗證的改動**（契約 1.1、4.6）。不改的話，準備器在這台機器的真客戶端上開不起來，CI 用合成資料看不出這個問題，會拖到 T470 才爆。
- **核心檔案會被多張任務同時改**（`AssetPrepInput.cs`、`AssetPrepRunner.cs`）：T453 加 Text 續跑驗證、T454 改 Sprite 驗證、T455 加「讀先前 recipe 輸出」、T456 加 Tile 與 map 輸入。同步 main 時要解衝突。
- **拿掉某個檔的任務要同時改它的讀取點**：多數讀取點用 `.Single(path == …)` 查 integrity，檔案不在清單裡會直接丟例外。
- **T456 與 T458**：`maps/manifest.json` 只有 `maps` recipe 能寫；T458 是去改 `maps` recipe 補上 terrain 欄位，不是自己另寫 manifest。
- **T457**：`6016.json` 做不到逐 byte 相同時停下來回報，不要改 `ClientEffect6016.cs` 的 pin。
- **T464**：沒有 T476 的裁定就不要動程式。
- **T470**：比對標準在契約第 2 節的「比對」欄、1.4 與 L3。`Sprite03`–`Sprite14` 與產品 pin 不符是已知狀況（D1），不要當成準備器的失敗。
- 寫 RESULT 時，「結論」那一行不要出現英文的受阻字樣：調度器會把它當成失敗宣告（#1029 第 3 次嘗試查到的，`runners.py` 的 `_declared_failure`）。
