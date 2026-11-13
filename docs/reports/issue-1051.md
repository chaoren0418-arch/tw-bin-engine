# Issue #1051 — T466 移除已知例外：兩個帶 pixels 陣列的 work/ 歷史證據（b15-exc-work-evidence）

2026-11-13，runner：claude-local，分支 `task/1051`。

## 結論

兩個歷史證據檔的 `pixels` 陣列已換成「筆數＋SHA-256」，allowlist 的 `b15Exceptions` 由 9 列減為 7 列，`check-no-game-assets.py` 在本機通過。CI 結果要等調度器建 PR 後才有。

開工依據：#1027 留言（2026-11-13 04:19 UTC，調度器代主控裁定）第 3 項——「T466 採預設：兩個 `work/` 歷史證據的 pixels 陣列換成『筆數＋SHA-256』，原內容靠 git 歷史保留；不重寫歷史。」

## 做了什麼

| 檔案 | 替換的陣列 | 陣列內筆數 | 替換前 size／SHA-256 | 替換後 size／SHA-256 |
|---|---|---|---|---|
| `work/P31-pickup-composite-review-r1/cases.json` | 255 個 `pixels`（`$.scenarios[].scene[].pixels`） | 68,935 | 843,169／`98369cbd4fbf437dca9d4262cdd01045a0c97639ce8754d530f3dfc54e34eb50` | 63,039／`122f2351610e239b2459d08c04237aa266cfb56fdfa1e7bf3a3996166252382e` |
| `work/engine-pickup-presentation-candidate-review-r1/execution-rows.json` | 1 個 `Pixels`（`$.rows[].actual.evidence.cover.Tile.Pixels`） | 1 | 379,060／`a10c83bd5820680144211e6cb85854033e61d89d81f6e4544ffe1f2052856585` | 379,005／`10badc776f7dd2b6d854ee3574c26363468a8f4f2dc6a96459cb00c98dd732d8` |

- Issue 寫的「68,935 筆 pixels 陣列」實際是 255 個陣列、合計 68,935 筆像素（每個陣列 96、192 或 523 筆，各 85 個）。
- 每個陣列換成 `{"count":N,"sha256":"…"}`，鍵名與大小寫不變（`pixels`／`Pixels`）。
- **SHA 的定義**：原陣列以緊湊 JSON 序列化（`json.dumps(v, ensure_ascii=False, separators=(",",":"))`，UTF-8，鍵順序照原檔）後的 SHA-256。`cases.json` 原本就是緊湊格式，255 個都等於原檔中該陣列的原始位元組；`execution-rows.json` 原檔有縮排，所以是重新序列化後的值。
- 其餘內容位元組不動（就地替換文字區段，沒有重新序列化整個檔；CRLF 與結尾換行照舊）。
- `execution-rows.json` 另有 15 個 `pixels: true／false` 布林欄位，不是陣列，沒有動。
- 兩個目錄各加一個單行的 `PIXELS-REPLACED.md`，寫明原檔 size、SHA、commit `1e47e415f28972a7631b42b0245d0e8490f0b366`、blob 與 `git show` 取回指令。
- `engine/tools/game-assets-allowlist.json`：`b15Exceptions` 拿掉這兩列，剩 7 列。
- 沒有改 `rebuild-lock.json`、`check-no-game-assets.py`、`TASKS.md`、`PLAN.md`。

## 怎麼驗證（已驗證）

- 替換腳本先把原檔做結構轉換（只把 `pixels` 陣列換成筆數＋SHA），再和替換後檔案的解析結果比對，兩檔都完全相等；所以兩檔仍是合法 JSON，其他欄位值沒有變。
- 列數不變：`cases.json` scenarios 8 → 8、scene 列 255 → 255；`execution-rows.json` rows 184 → 184。
- 文字行數：`cases.json` 1 → 1；`execution-rows.json` 13,580 → 13,574（少掉的 6 行就是那個陣列的縮排行）。
- 再跑一次替換腳本，兩檔可替換的陣列都是 0。
- `python -B -X utf8 engine/tools/check-no-game-assets.py` → `{"status": "pass", "trackedFiles": 4665, "errors": 0, "embeddedResourceDeclarations": 4}`，exit 0。
- 會讀 `work/` 的腳本與 CI 步驟只用到 `work/issue-49`、`work/issue-51`、`work/engine-ground-lifecycle-r1`，不讀這兩個檔。r34 `rebuild.py` 核的是 `engine/_integration/r34/acceptance/` 內的 seal 檔本身，不回頭核 `work/` 的檔案。

要重驗某個陣列的 SHA，可用：

```
git show 1e47e415:work/P31-pickup-composite-review-r1/cases.json > scratch/cases-orig.json
python -B -X utf8 -c "import json,hashlib;v=json.load(open('scratch/cases-orig.json',encoding='utf-8'));p=v['scenarios'][0]['scene'][0]['pixels'];print(len(p),hashlib.sha256(json.dumps(p,ensure_ascii=False,separators=(',',':')).encode()).hexdigest())"
```

## 未驗證／已知影響

- **CI**：本張只 push 分支，PR 由調度器建立，CI 結果尚未看到。
- **歷史 pin 不再對得上工作樹**：約 30 個歷史清單仍記著兩檔的舊 SHA 與 size（兩個目錄自己的 `artifact-manifest.json`、`engine/_integration/r34/acceptance/independent-review-seal.json`、`work/P26-*`、`work/P32`／`P33`／`P35`、`work/engine-r34-*`、`主管/` 下的紀錄、`docs/reports/issue-102.md`）。這些是歷史紀錄，沒有改；舊 SHA 對應的是 git 歷史中的原檔，`PIXELS-REPLACED.md` 有寫對照。目前沒有任何會執行的腳本拿這些 pin 去核工作樹的檔案，但若日後有人手動重核這些封存清單，這兩檔會不符。
- `TASKS.md` M3「已知例外（共 9 個）」是凍結文字，本張沒改；T471（#1056）會加註。

## 產出路徑

- `work/P31-pickup-composite-review-r1/cases.json`、`PIXELS-REPLACED.md`
- `work/engine-pickup-presentation-candidate-review-r1/execution-rows.json`、`PIXELS-REPLACED.md`
- `engine/tools/game-assets-allowlist.json`
- `docs/reports/issue-1051.md`

## 給下一棒的提示

- T471（#1056）：`b15Exceptions` 現在剩 7 列，都屬 T463–T465。`check-no-game-assets.py` 的偵測器只擋「鍵名是 pixels 且值是陣列」，換成物件後就不會觸發；T471 若加強偵測，注意別把 `{"count","sha256"}` 這種摘要物件當成素材。
- 替換腳本是一次性的，放在 `scratch/replace_pixels.py`（未入 git）；作法已寫在上面，不需要再跑。
