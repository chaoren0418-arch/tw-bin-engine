# Issue #1056 — T471 CI 拒收收尾（b15-exc-ci-close）

## 結論
- **最後的 B15 例外數：1**（不是 0）。依使用者裁定 L1＝A（T476／#1072，2026-11-13；`docs/design/asset-recipe.md` 第 7 節 L1、第 8 節 T471 列），T471 的例外目標是 1，例外機制保留、不移除。
- 保留項與理由：

| 路徑 | 已知限制 | 理由 |
|---|---|---|
| `engine/source/OriginalExpTable.cs` | L1＝A | 各等級累積 EXP 門檻是伺服器規則值；客戶端把自己的表放在受保護執行檔的 `.data`，伺服器不送門檻，客戶端素材裡沒有來源。檔案不動，以精確路徑加 SHA-256 鎖定（`325384a1…98b1`）。詳見 `docs/design/asset-recipe.md` 第 7 節 L1、`docs/reference/asset-recipes/rule-tables.md` 第 3 節 |

## 做了什麼
1. `engine/tools/check-no-game-assets.py`
   - 新增 `KNOWN_LIMITS`（只含 `OriginalExpTable.cs` → L1）。allowlist 的 `b15Exceptions` 多出任何不在這裡的路徑，一律 `REFUSED: … new B15 exception forbidden`；要放行只能改程式，經 PR 審查。沒有理由的例外、理由對不上例外、例外檔不在追蹤清單，仍然失敗。
   - 保留項每次執行都印 `B15-KNOWN-LIMIT L1: <路徑>: <理由>`，輸出 JSON 多一欄 `b15KnownLimits`——不靜默放行。
   - 新增 pixels／時序表檢查：`engine/source/Resources/`、`engine/audio/Resources/` 下的追蹤檔，以及所有 csproj／props／targets 的 EmbeddedResource 指向的檔案，內容不得是圖檔 magic、pixels／RGB555 JSON 或時序表（`frameTimings`、`timeline`、`timings`、`delays`、`durations` 等鍵帶非空陣列／物件）。時序表規則只套用在資源內容，不套用到 docs／work 的一般報告。
   - EmbeddedResource 允許清單維持實際剩下的 1 個 logical name：`P24.Audio.CombatSfxPins.json`（WAV 的 SHA pin 表，不是 pixels 也不是時序表；產品 `OfflineClient.csproj` 已沒有任何 EmbeddedResource，`engine/source/Resources/` 已不存在）。
   - 主流程改寫成 `run(policy, paths, read)`，可餵合成檔案樹；新增 `--self-test`（14 項 unittest），涵蓋：任何新增例外即失敗（三種路徑）、新增例外且無理由、L1 缺理由、L1 內容被改（SHA 不符）、L1 列印、拿掉 L1 仍可通過、Resources 下的 pixels／時序表／PNG、EmbeddedResource 內容含時序表、新增 EmbeddedResource、docs 下的 timings 不誤判、真實 allowlist 是 `KNOWN_LIMITS` 的子集。
2. `.github/workflows/build.yml`：在「Reject tracked game assets」前加一步 `check-no-game-assets.py --self-test`。
3. `TASKS.md` M3：在凍結的「已知例外」條目下加後註（已由 M8 處理、只剩 1 列、清單鎖死），凍結文字本身不改。
4. `docs/design/asset-recipe.md`：例外現況段補一行 T471 收尾說明。
5. `game-assets-allowlist.json` 沒改（已是 1 列）；`rebuild-lock.json` 沒改。

## 怎麼驗證
- 本機（只跑 python 檢查腳本，未執行任何編譯產物）：
  - `python -B -X utf8 engine/tools/check-no-game-assets.py --self-test` → 14 項全部 ok，exit 0。
  - `python -B -X utf8 engine/tools/check-no-game-assets.py` → `{"status": "pass", "trackedFiles": 4724, "errors": 0, "embeddedResourceDeclarations": 1, "b15Exceptions": 1, "b15KnownLimits": ["engine/source/OriginalExpTable.cs"]}`，並印出 L1 理由。
- CI：以 PR 上 `task/1056` 最新 commit 的 GitHub Actions 結果為準。

## 未驗證什麼
- 時序表偵測是鍵名規則（推測足以擋下目前已知的格式），未針對所有歷史時序表格式窮舉；只套用在 Resources 目錄與 EmbeddedResource 內容。
- 不涉及實機，無待驗證的遊戲畫面項目。

## 產出路徑
- `engine/tools/check-no-game-assets.py`、`.github/workflows/build.yml`、`TASKS.md`、`docs/design/asset-recipe.md`、`docs/reports/issue-1056.md`

## 給下一棒的提示
- 若之後 L1 有了客戶端來源（例如從執行檔 `.data` 正式提取的配方），把 `OriginalExpTable.cs` 從 allowlist 拿掉即可通過（自我測試 `test_dropping_known_limit_is_allowed`），再把 `KNOWN_LIMITS` 清空，清單就真正歸零。
- 新增任何 B15 例外都需要同時改 `KNOWN_LIMITS` 與 allowlist，且應有使用者／主控裁定。
- T475（M8 結案）可直接引用本報告的例外數 1 與理由。
