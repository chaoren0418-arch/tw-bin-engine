# P28／P29 恢復後接回與 P30 隔離整合

2026-10-29 05:42 UTC 主管驗證：檔案系統與桌面對話工具可用。未重送斷線前結果不確定的 namespace 修正；既有修正已由 P28 完成。

- P28：41 組新增外觀與 0/0 控制組共 42 組精確 gfx/weapon 配對；26953 候選 assertions 通過。225 r34 對照 assertions 另列。1207 個交付檔已驗封。
- P29：實際載入 P28 DLL，5196 筆獨立 runtime rows 全通過；17 項來源審查。主管獨立核對 243 artifacts、2004 source pins，失配 0。H1 測試載入器相依缺漏已修正，原失敗證據保留。
- 接受範圍：可進入隔離 stage 準備，非完整 14 類覆蓋、共享合入、正式發布或原生驗收。
- P30：沿用唯一整合者 01a17a22-17e4-70c0-9792-166ddb86ea42；對話顯示名稱已更新，沒有另立共享寫入者。實際回合 01a1862f-aa42-7712-b4e2-8b73dbeb1eae 已讀契約與來源、六項 exact pins 通過。只准備 stage 計畫／控制器；完整來源 181 檔。組建與執行仍待主管審查精確計畫，無須使用者逐次回覆。

正式入口與 project-manifest 仍為 r33；sealed r34 已完成離線封存驗證。自動審查先前拒絕對唯一整合者派發 r34 正式入口切換，理由為缺少使用者對該次啟用的明確同意；動作未執行、未重試。此項待批准不阻擋隔離開發。

DB、服務、原生遊戲／角色均未操作。主管 Goal 工具未提供，不能宣稱已有平台 Goal；P28／P29 回報 Goal complete，實際對話 idle/completed 已核對。全案仍未完成，原生流程與其他來源缺口保留。

證據：P28-P29-candidate-root-acceptance.json、P29-root-verification.json、P28-P29-coverage-crosswalk.json、P30-isolated-stage-plan-dispatch.json、P30-isolated-plan-dispatch-receipt.json。最新活動以 continuation-status.json 與 queue.json 為準。
