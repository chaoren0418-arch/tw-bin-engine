# Issue #1058 — T473 可攜版 README、入口 TW_CLIENT_ROOT、BUILD／ARCHITECTURE 同步

分支 `task/1058`。依 `docs/design/portable-delivery.md`「README 步驟草稿」與 `docs/design/asset-recipe.md` 第 1.2 節、第 7 節。

## 做了什麼

1. **可攜 README**（`engine/tools/portable/README.md`，打包時放 zip 根目錄的 `README.md`）改寫成正式版，共 8 節：
   - zip 內容；
   - 需要的環境：Windows x64、.NET 10 Desktop Runtime、WebView2 Runtime、自己的原客戶端；
   - 支援的原客戶端版本：19 組 idx／pak 加 `map`，以及 1.2 的錨點表；
   - 解壓、設定 `TW_CLIENT_ROOT`；
   - 第一次啟動會做什麼：啟動器核對 manifest → 檢查客戶端 → `--prepare-assets`（staging、進度 JSON、收據）→ 讀回驗證 → 檢查 Runtime → 登入畫面；
   - `--check-only` 三種結果與結束碼；
   - 登入需要另外有伺服器；
   - 磁碟空間與時間；
   - 常見錯誤 19 列，每列都附程式原文；
   - 已知限制：L1、L2、L5、L6、L7，加上按需外型；
   - 本機資料不要再散布。
2. **入口** `engine/tools/portable/啟動引擎.cmd`：
   - 只有一處 `set "TW_CLIENT_ROOT=…"`；`TW_ENGINE_ROOT` 自動推導為 `%~dp0engine`，啟動器也從這個路徑呼叫。
   - 新增 `chcp 65001`：記事本以 UTF-8 存檔時，中文路徑也能用。
   - 找不到啟動器時印出 `[tw-bin-engine] … not found`。
   - 沒帶參數且失敗時會 `pause`，連按兩下啟動時才看得到錯誤訊息。
   - 檔案仍是 ASCII＋CRLF。
3. **一致性檢查**：`engine/tools/package-portable.py` 新增 `template_errors()`，打包前與 `--self-test` 都會跑。檢查項目：
   - README 中反引號括住、含中文的訊息（`…` 代表執行期的值）必須逐段出現在 `launcher-source/Program.cs` 或 `engine/source/AssetPrep/*.cs`。目前 26 句，全數相符。
   - README 裡的 `[tw-bin-engine]` 訊息必須出現在 cmd 裡。
   - cmd 必須是 ASCII＋CRLF，而且只有一行 `set "TW_CLIENT_ROOT=`。
   - cmd 必須有 `TW_ENGINE_ROOT=%~dp0engine` 與呼叫啟動器的那一行。
   - README 顯示的 set 行必須與 cmd 一致。
   - `ClientRootHint` 仍指向這一行。
   - 打包模式下檢查不過就拒絕，不產生 zip。
   - 自測新增 2 項：真實 repo 通過；人為造成漂移時能被抓到（假訊息、兩行 set、LF 換行、非 ASCII）。
4. **文件同步**：
   - `docs/BUILD.md`：「資源目錄約定」補上可攜模式的段落：`engine/assets` 由本機產生的普通目錄、legacy-ready／ready 的分辨、準備器拒絕 junction。也補了執行期的 `TW_CLIENT_ROOT` 規則（provider 會退回開發機路徑；launcher 與準備器不退回），排錯表新增一列。
   - `docs/ARCHITECTURE.md`：架構圖改為兩種素材來源，並加上 `--prepare-assets` 虛線；說明段落把過時的「r28 junction」改正；資料流第 5 節第 1 步補上可攜模式。
   - `engine/README.md`：在 r33 歷史段落前加一則現況說明。
5. **打包白名單不用改**：README 與 cmd 原本就在 `TEMPLATES` 裡，`DOCS` 維持空的。

## 怎麼驗證

- 本機只跑 Python 唯讀檢查，沒有執行任何編譯產物，也沒有執行 `啟動引擎.cmd`：
  - `python -B -X utf8 scratch/issue-1058/probe_templates.py .` → 26 句程式訊息逐段對上，`errors: []`。
  - `package-portable.py` 語法檢查通過。
- CI（PR #1114，commit `3ad20fb1`，run 43482091638）：`dotnet`、`map-continuity`、`npc-offline` 三個 job 全部通過。
  - 兩個 job 都跑了 `package-portable.py --self-test`，共 17 項（新增 2 項）全部 ok。
  - Windows 實際建置後打包：`status: pass`，23 檔，總大小 10,251,856 bytes；zip 3,463,843 bytes，errors 0。`--review` 也是 pass。
- README 中的數字出處：
  - 1,616 MiB：`AssetPrepOrchestrator.cs` 的 `EstimatedOutputBytes`（800 MiB）× 2 ＋ `SpaceReserveBytes`（16 MiB）。
  - zip 大小：#1057 實測。
  - 9,186,505,456 bytes：#1055 實測。
  - 約 0.75 GB：`asset-recipe.md` 第 2 節的推估。

## 未驗證／待驗證

- **待驗證（T474，dot）**：
  - 入口的實際行為：`chcp 65001` 下中文與空格路徑、連按兩下失敗時 `pause`、`--check-only` 不停頓、找不到啟動器時的訊息。
  - README 各步驟在乾淨 Windows 上的實際畫面。
  - 「完全沒裝 .NET 時跳出安裝提示」只是推測（這是 .NET apphost 的行為，不是本程式的訊息）。
- **尚未實測**：第一次準備的時間，以及準備完成後 `engine/assets` 的實際大小。T470（#1055）因本機限制沒有執行準備器，README 標為「尚未實測」與「推測」，T474 量到後要回填第 5 節。
- 「重新執行入口會重建 `receipt-mismatch` 的資源」沒有逐行追到 orchestrator 怎麼替換既有的壞目錄，所以 README 另外寫了「仍然失敗就刪 `engine\assets`」作為備案。
- 根目錄的開發用 `啟動引擎.cmd`（指向 r33）沒有改：它不進 zip，改了會影響正式入口。

## 產出路徑

- `engine/tools/portable/README.md`、`engine/tools/portable/啟動引擎.cmd`
- `engine/tools/package-portable.py`（`template_errors` 與 2 項自測）
- `docs/BUILD.md`、`docs/ARCHITECTURE.md`、`engine/README.md`
- 本報告。不進 git：`scratch/issue-1058/probe_templates.py`。

## 給下一棒的提示

- 改了 launcher 或 AssetPrep 的使用者訊息，記得同步可攜 README 第 4、6 節；沒同步的話，`package-portable.py --self-test` 會在 CI 失敗，並指出是哪一句。
- T474 量到第一次準備的耗時、`engine/assets` 實際大小與峰值空間後，回填 README 第 5 節，拿掉「推測」與「尚未實測」字樣。
- `chcp 65001` 的效果會在呼叫者的主控台保留（launcher 本身也會把輸出字碼頁設成 UTF-8），T474 若發現問題，再評估要不要還原字碼頁。
