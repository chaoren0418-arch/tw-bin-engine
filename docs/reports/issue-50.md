# Issue #50 — combat-sfx 前置契約阻塞

日期：2026-11-05；階段：code，第 1 次嘗試；分支：`task/50`；基線：`dfdfadf851891faab09d9f976f1be490ec54d0db`。

## 結論

**blocked：sfx-native-contract 尚未 qualified，沒有可實作的核定 binding。** 本輪只交付依賴核對報告，不代表音效功能完成。不得因 #47 已關閉或本報告提交成功而解除 #50 的來源閘門。

## 做了什麼

- 已讀本工作樹根目錄 AGENTS.md、docs/REPORT.md、TASKS.md、#42 設計及報告；維持既有 `task/50`，未 checkout 或建立分支。
- 透過已連線 GitHub 工具讀取 #47、#50 正文與留言，核對 #50 明定「前置 sfx-native-contract 必須 qualified」。
- 工作樹與原始專案 docs 目錄均未找到 issue-47.md；再以唯讀 `git show 705171687b11dfb70f215b289715cc0f8c3ba560:docs/reports/issue-47.md` 核對本機前置報告。沒有 cherry-pick、複製或推送 #47 的檔案。
- 未修改 engine/source、engine/audio、Resources、assets、既有 lock 或 docs/REPORT.md；未啟動遊戲、音訊播放、服務或 DB，未寫入原始專案工作目錄的檔案。

## 怎麼驗證／已驗證

| 證據 | 核對結果 |
|---|---|
| [#50 任務正文](https://github.com/chaoren0418-arch/tw-bin-engine/issues/50) | depends_on 為 #47，明定 qualified 後才可實作，未知語義不得猜值。 |
| [#47 執行器 RESULT](https://github.com/chaoren0418-arch/tw-bin-engine/issues/47#issuecomment-6651394171) | 明列 blocked、qualified binding 為 0、檔案未 push，要求保持來源依賴阻擋。 |
| [#47 調度器回覆](https://github.com/chaoren0418-arch/tw-bin-engine/issues/47#issuecomment-6651413262) | 卻宣告「產出已在 main，任務完成」；此結論與執行器 RESULT 不一致。查核時 Issue 為 closed、task:done。 |
| 本機 #47 commit `705171687b11dfb70f215b289715cc0f8c3ba560` 的報告 | 明列 qualifiedBindings 為空、候選均未啟用，零列允許實作播放；符號至欄位映射、版本配對、首次進幀／terminal／重播／飛行／受擊完整派送鏈不足。 |
| 本工作樹追蹤檔案與遠端 main | `git ls-files docs/reports/issue-47.md work/issue-47` 無輸出；遠端 main 與本輪基線相同，只有 #47 索引不能證明契約交付或 qualified。 |

這些是依賴狀態與文件一致性核對，沒有重新驗證 #47 的原始資源或機器碼，也沒有將候選 ID、WAV pins 或既有產品時序提升為原生派送契約。

## 未驗證什麼

由於前置明確未通過，本輪沒有實作或執行技能 1/3/4/8、attack/damage、光箭飛行／impact、單次消費與取消、靜音／音量、缺檔／hash mismatch／fallback／voice cleanup、Effect6016 回歸測試；也未建置 dev。對未變更的產品做建置不能代替本任務的功能驗證。上述驗收項目全部保留待辦。

沒有原生實機可聽證據；將來的離線測試通過也不得當作實機可聽驗收。

## 產出路徑

- `docs/reports/issue-50.md`：本輪唯一版本管理產出。

## 給下一棒的提示

1. 調度器／Claude 先修正 #47 的完成判定，將 #50 保持依賴阻擋；不要只以 Issue closed、task:done 或出現 RESULT 作為 qualified 的依據。
2. 由 #47 資源執行器補齊合法既有靜態證據：可對映版本的 parser/loader、符號至欄位及事件 consumer／播放介面鏈，含來源 SHA-256 與明確 File Offset／RVA／VA，以及首次進幀、terminal、重播、飛行、受擊規則。候選選擇與時機須逐 binding 核定，不能全播、隨機猜值或套固定延遲。
3. Claude 核定可重現的 qualified binding 清單與資源 pins，將核定交付納入可讀基線後，再重派 codex-local 實作 #50、建置 dev 與離線回歸。實機驗收另按專案授權流程安排。
4. #47 RESULT 另記有其檔案公開推送被拒的問題；本輪沒有重試或代送那三份產出。該交付問題須由原任務處理，且即使交付成功也不會自動補足原生語義。

## 本輪交付工具狀態

本機 `gh issue view` 回傳 HTTP 401，故改用已連線 GitHub 工具唯讀查核，未變更認證設定。預設 Git HTTPS schannel 查詢失敗；以單次命令參數 `-c http.sslBackend=openssl` 成功查詢遠端，未修改 Git 設定。提交與推送結果以本輪最終 RESULT 為準。
