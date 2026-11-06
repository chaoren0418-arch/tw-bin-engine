# Issue #151 — T075 第 a 段建置與交接

## 結論

⚠️ dev 建置通過，入口 `--check-only` 退出 1。第 a 段受素材索引缺件阻塞，須先排除再交 dot 執行第 b 段。

## 做了什麼

- 已讀根目錄 `AGENTS.md`、`docs/REPORT.md` 與 `TASKS.md` 的 M2／T075。
- 接手時為 detached HEAD `1cb4d024fee785a983f8bdd6c930ecd8d06f1d2c`，有 147 個既有未追蹤檔案。為滿足乾淨目錄前置，將它們保全至 `scratch/issue-151/preexisting-untracked/`，保留相對路徑，搬移前確認都是單一連結的普通檔案，搬移後逐檔核對 SHA-256。還原清單為 `scratch/issue-151/preexisting-untracked.json`。
- 確認 `git status --porcelain` 為空後，依序 fetch、切 main、fast-forward 至 origin/main，再建立 `task/151`。主目錄停在此分支。
- 建置來源 HEAD 為 `41d978ee001554c23b0372589bb79c4893dbd17c`。
- 以已安裝的 Python 3.12 補入本次命令 PATH，並指定套件快取 `engine/.nuget`，執行派工建置命令。

## 怎麼驗證

```text
python -B -X utf8 engine/_integration/dev/rebuild.py --build --write-receipt --output scratch/issue-151/rebuild
engine\啟動新版客戶端.cmd --check-only
```

- 已驗證建置退出 0，`PASS dev`，SDK `10.0.300`，172 個來源檔與 181 個鎖定檔核驗通過。主程式 0 錯誤、2 個警告，位於 `MagicUi.cs:20` 的 CS8600／CS8602。Launcher 0 錯誤、0 警告。
- `engine/build/client-product-dev/P11.ClientEngine.dll` 的 SHA-256 為 `7fa85547ec50caf0049d503db114da5f519b235c8173ea45cfda26788946fe08`。
- `engine/_integration/dev/rebuild-lock.json` 的 SHA-256 為 `038366ef8a532466fbd742642435f70174ec154986f388ceaa8dfa6138a5c4b8`。
- 已驗證 check-only 退出 1，錯誤為「manifest 缺少必要入口或素材索引。」因此尚未取得要求的 `revision: dev` 成功回傳。
- 已驗證 `engine/assets` junction 指向 `releases/r28/assets`，目標目錄存在，但 `engine/assets/integrity.json` 與目標下的 `integrity.json` 均不存在。建置腳本只有在該檔存在時才納入 manifest，launcher 則要求它存在，與 `docs/BUILD.md` 的前置說明一致。
- 唯讀核實調度器 `leases` 的 holder 為 `chaoren0418-arch/tw-bin-engine#151`，exp 為 `1793979710.0998135`，對應派工的 `2026-11-06T15:41:50Z`。
- 收尾時唯讀檢查 42061／43324，兩埠均無 TCP 監聽。

## 未驗證什麼

- M2 與 M1 移入列的遊戲畫面、登入、戰鬥、NPC、跨圖及音效均待第 b 段真機驗收，demo 頁的實測結果待 dot 填寫。
- Runtime／WebView2 完整入口核驗仍待素材索引問題排除後確認。
- 本輪遊戲與服務操作、DB 查詢均為零，產品來源與 lock 的 Git diff 為空。

## 產出路徑

- 本報告 `docs/reports/issue-151.md`。
- 建置結果與各步驟日誌 `scratch/issue-151/rebuild/`。
- 入口退出碼與輸出 `scratch/issue-151/check-only.json`、`check-only.stdout`、`check-only.stderr`。
- 監聽狀態 `scratch/issue-151/listeners.json`。
- 本機 receipt `engine/_integration/dev/receipt.json`，供後續核對，屬未追蹤建置產物。
- 既有檔案保全目錄與清單如上，僅存本機 scratch，交接前應保留。

## 給下一棒的提示

1. 主控先安排本機 resource 處理缺失的素材索引，確認合法且完整的既有素材來源。本輪依禁止事項保留封存內容與 junction，未以替代索引繞過檢查。
2. 素材前置排除後，由 codex-local 補完含素材索引的正式建置與入口 check-only，記錄成功的 dev 回傳及最新雜湊，再由主控改 runner 重排 dot。當前版本尚未達到第 b 段交接門檻。
3. dot 接手須重新核實有效 p24lab lease，沿用 #77 主控 06:03 安全裁定，依 #129、#130、#133、#140、#145 報告與 `docs/protocol/npc.md` 執行一次 M2 smoke，每項最多三次，填 demo 頁 ✅ 附截圖或 ⚠️ 加原因。普通肉需驗丟 1／撿 1、丟 10／撿 10；無第二測試角色時，直接交易照實註記 PLAN U3。
4. M2 smoke 完成後交主控結案，未過列由主控排下一里程碑 code 任務。
