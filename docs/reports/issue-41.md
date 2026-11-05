# Issue #41 — T044 提交 T042 局部補測證據

## 做了什麼

已 fetch，從 origin/main 的 b573c7be069789d1f4c6c8121e9338a51dc88530 建立 task/41，整理 [#37 指定結果留言](https://github.com/chaoren0418-arch/tw-bin-engine/issues/37#issuecomment-6650577185)。

- 新增 [dev-retest.md](../native-baseline/dev-retest.md)，結果維持 ✅ 04、05、07；⚠️ 06、15；❌ 10，逐項列出數字、完整操作窗 op 統計與截圖。
- 提交既有 17 張 PNG（任務數量為約估），全部單檔小於 2,000,000 bytes，合計 8,356,949 bytes，小於 15,000,000 bytes；原圖未改寫。
- 複製並提交 6 份五鍵 trace 與原 trace-summary.json，scratch 原件保留。
- 既有 docs/reports/issue-37.md 直接納入版本控制，內容未改；其 SHA-256 為 f38d85e228a12c04602340cd87af635efa4c63014ce53b7c49d2bd33e4703db5。
- [MANIFEST](../native-baseline/dev-retest/MANIFEST.md) 列出大小、SHA-256、掃描結果及處置。本輪沒有因容量、機密檢查或忽略規則排除的候選檔案。

## 怎麼驗證（已驗證）

- 17 張原圖逐張目視未見帳密明文；有登入面板的圖片帳號／密碼欄皆為空。沒有讀取機密設定或私密帳密值，也沒有把檔名搜尋當作 OCR。
- 6 份 trace 共 4,468 行，每行皆 JSON 解析成功，原始鍵名精確為 utc／dir／op／len／seq，共 5 個且無重複；額外確認方向、UTC 格式及數字／null 型別。
- 17 張 PNG 與 6 份 trace 的大小、SHA-256 全數符合 #37 本機 evidence-manifest.json，共 23／23。trace 與摘要複本逐檔比對 SHA-256 相同。
- 六項操作窗的 in/out 總數與所有 op 分組均由原始 trace 重算一致；04／05／06／07／10 的 fullSessionCounts 亦一致。
- **15 的 fullSessionCounts 僅涵蓋前 456 行，最終原件有 528 行。** 前 456 行分組完全吻合；後 72 行在操作窗後，為 S10×48、S33×2、S87×10、S120×8、S123×2、C95×2。原摘要與 trace 均原樣保留，沒有修改 #37 的結果。摘要先產生、trace 後續追加是推測，不能當作已核對的生成時序。
- 唯讀查核 GroundRules.Droppable 的金幣限制，以及 Magic／Combat 無音效派送、現有遊戲音效接線只有 Effect6016；文件分別標為產品限制／功能缺口，06 則標測試條件問題。
- 新增文字與摘要檢查帳密賦值、私鑰／token 模式；trace 由嚴格欄位與純量型別檢查限制可公開內容。未把私密設定或執行期 state 納入提交。
- 核對 Markdown 相對連結、暫存區限定 28 個檔案、原 PNG／trace／摘要／原報告的 Git blob 完整性。git ls-files -ci --exclude-standard 為空；目的地逐檔 check-ignore 未忽略，未使用 git add -f。完整暫存區通過 git -c core.whitespace=cr-at-eol diff --cached --check。

原件保留說明：issue-37.md 與 trace-summary.json 保留既有位元組與換行格式，完整性以 SHA-256 及 Git blob 核對。

暫存驗證工具及結果位於 scratch/issue-41/validate.ps1、validation.json，未納入版本控制。

## 未驗證什麼

本輪未啟動、附加或停止遊戲、伺服器、MySQL，也未查詢 DB、連正式服務、建置或重跑 check-only。未變更產品程式、lock、docs/REPORT.md、封存 release 或硬連結。專案既有其他未追蹤檔案保留原狀，未一併提交。

未完成原生驗收：06 怪物 HP／擊殺、10 金幣丟棄／拾取、15 可聽聲音與音訊峰值、完整 N01。這些是既有產品／驗收缺口，不是本資源整理任務已通過的項目。

## 產出路徑

- docs/native-baseline/dev-retest.md
- docs/native-baseline/dev-retest/MANIFEST.md
- docs/native-baseline/dev-retest/*.png（17 張）
- docs/native-baseline/dev-retest/trace/{04,05,06,07,10,15}.jsonl
- docs/native-baseline/dev-retest/trace/trace-summary.json
- docs/reports/issue-37.md（原樣）
- docs/reports/issue-41.md（本報告）

## 給下一棒的提示

由 Claude／調度器審查 task/41 的證據與文件，交使用者合併；docs/REPORT.md 索引由調度器合併後處理。

後續先處理金幣支援、Magic／Combat 音效派送及穩定可見的弱怪測試條件，再由使用者做原生局部驗收。15 的摘要全工作階段數量不是最終 trace 全檔數量，請以 MANIFEST 的前 456 行／尾段 72 行區分；操作窗 85 入站／5 出站不受影響。
