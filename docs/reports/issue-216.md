# Issue #216：T121 物品 status blob 語義化

## 做了什麼

- `ItemStatus.cs` 解碼武器／防具／一般物品、強化、損傷度、能力、元素與狀態抗性、Big5 說明；未知或截斷 tag 保留原始 hex、未解析尾段與錯誤位置。可交易／刪除／封印狀態使用背包旗標。
- S5／S15／S24／S242 更新同步解析屬性；`InventoryItem.Equipped` 改讀 S64 權威裝備槽位。背包 tooltip 顯示解析結果及槽位／待同步狀態。
- 本輪依 2026-11-07 主控裁定，只修正 `ItemTransferChecks.cs` 的裝備前置：S15 名稱改成一般「劍」，再送 `S64/66 D(itemId) C(slot=9) C(equipped=1)`。保留裝備中不可刪除／給予兩個斷言；未改 `ItemTransfer.cs`、`SupportedUse`。
- 在既有 `task/216` 合入 `origin/main` `5fece00`（含 T109 與受擊抑制），唯一衝突為 dev lock，已用 `--update-lock` 重產，未手工拼接 hash。`docs/REPORT.md` 僅隨 main 合入，與 main 無差異；保留 T109 的使用參數與 UI。
- 沿用 [PR #242](https://github.com/chaoren0418-arch/tw-bin-engine/pull/242)。本模組測試已提交並掛入 DemoChecks 與 Windows CI。

## 怎麼驗證（已驗證）

- `rebuild.py --update-lock` 與唯讀核驗：181 個來源／190 個 pin 通過。
- `rebuild.py --build --configfile scratch/issue-216/NuGet.Config --output scratch/issue-216/build`：產品與 launcher 成功；產品有既有 `MagicUi.cs:20` 兩個 nullable 警告、0 錯誤。
- 產品 DLL SHA-256：`ed81da9a3513a44ed9f21d875e279701c72ff54eff799b35387654b07daff3c0`。
- 本機重跑全部 CI 自動測試項目：item-status 82、item-transfer 73、map-continuity 358（含 board 230）、combat-admission 278（含受擊抑制 66）、NPC 165、packet-trace 3468，及 embedded resources、WebView2 產品頁與 legacy 頁，全部通過。
- 額外離線回歸：完整 Demo 815、combat-loop 64、slow-actions 364、board 230、item-use 426、hit-reaction-suppress 66、login 396589、skill-cast 785、teleport 4369、status-icons 626、player-chat 107、sound-packets 148、6016 managed 25 項通過。不同測試群組有重疊，數字不加總為獨立案例數。
- `check-no-game-assets.py`：3295 個追蹤檔、0 errors；`git diff origin/main --check` 通過。
- 測試使用 `-p:UseSharedCompilation=false`。WebView2 在沙箱內導覽逾時；同一 DLL 在沙箱外重跑兩組皆通過，使用獨立離線 host，未啟動遊戲。sound-packets 的一次 PowerShell 參數組合錯誤已修正指令並重跑通過。
- 依既有測試需要，在 worktree 建立 `engine/assets` junction，僅唯讀引用主專案 `releases/r29/assets`；未複製遊戲資源、未修改目標，也未拆除 junction。NuGet 設定唯讀引用主專案 vendor，測試快取／暫存位於本 worktree。
- 新 head 的 GitHub Actions 結果於推送後確認；精確 head、run URL 與結論記錄在 PR #242 本輪 RESULT 留言，避免把歷史綠燈當成本輪證據。

## 未驗證什麼／已知限制

- **額外歷史 world-ui 測試未全過**：`world-ui-tests/Program.cs:138` 的聊天斷言要求 `history.Text == "server echo"`，而現行 `PlayerChatProtocol.FormatLine` 會加 `[頻道] `。這三個相關來源與 main 完全一致，本輪未改；對應 player-chat 測試 107 項通過。此歷史入口不在 CI，依本輪範圍未修改它，不能宣稱所有歷史測試全綠。
- 未啟動遊戲、登入、伺服器或 DB；真實 tooltip 排版、即時原服回包與音訊裝置仍待 M6 smoke。
- 空 status 不推定零能力，未知 tag 不嘗試重新對齊。沒有獨立 tag 的元素強化種類／階級不從名稱推測，自由文字不冒充數值解碼。
- 封存／check-template 舊測試、需真機或未追蹤 fixture 的歷史測試入口不作本次驗收證據；未執行會遞迴清理的測試腳本。

## 產出路徑

- 協定及唯讀來源 SHA：`docs/protocol/item-status.md`。
- 實作：`engine/source/{ItemStatus,Inventory,Equipment,InventoryUi}.cs`。
- 測試：`engine/_integration/dev/demo-tests/ItemStatusChecks.cs`、`Program.cs`；`engine/_integration/dev/item-transfer-tests/ItemTransferChecks.cs`；CI：`.github/workflows/build.yml`。
- 本報告：`docs/reports/issue-216.md`。
- 本機證據（未追蹤）：`scratch/issue-216/build/result.json`、各測試 JSON／log、`run-{regressions,extra,current,assets,web}.ps1`。測試原始碼已入 repo，scratch 僅為執行證據；沙箱／指令初次失敗與重跑分別記錄，最終結果以各測試 JSON／log 及 `web-regressions.json` 為準。

## 給下一棒的提示

請 Claude 依 PR #242 本輪 RESULT 所列新 head 的 CI 結果審查，再交使用者合併；調度器可另派歷史 world-ui 聊天斷言維護。dot 的 M6 smoke 核對 tooltip、穿卸及重新登入的槽位同步。`Equipped` 是即時權威查詢，需要歷史快照的呼叫端應保存 bool? 值。worktree 與 assets junction 保留交調度器清理，禁止遞迴刪除。
