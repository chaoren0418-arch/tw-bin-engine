# P31／P32 平行行為驗收交接

兩個新階段已實際並行執行、交付並獲主管有限接受，均 idle／completed，沒有把派工成功當成完成。精確基準仍為 P30 已接受的 6cb2… 產品 DLL；產品未修改或重建，未發現確證缺陷。

| 階段 | 對話／回合 | 有限成果 |
|---|---|---|
| P31 | 01a18676-3321-7c60-a658-cc70dbe320ba / 01a18676-34ad-71b3-9d59-6f7774b23f2a | 56 案、191232 像素逐值一致、144 負向控制有效；508 檔與82來源經主管核對 |
| P32 | 01a18676-7607-7120-8804-d19ca3805f36 / 01a18676-790d-7ec0-a905-3c67385543d2 | 196 不重複案、3 負向控制有效；202 交付檔與33來源經主管核對，cache/home/tmp等明示不屬該封存 |

P31驗證拾取時陰影移除、terminal的exact profile陰影恢復、結束回到當前武器待機外觀、前後遮擋及最終點選歸屬。只涵蓋4組配對、8條方向軌跡、speed0與明示有限合成場景；真實完整地圖、opacity及GUI仍未驗。實際畫面接觸表：../../work/P31-pickup-composite-review-r1/actual-contact.png。

P32驗證12個owner欄位、取消後新receipt恢復、舊serial不復活、方向1–7代表性窗口、terminal替換、owner不變guard、100→101角色重入，以及ground lease舊成功／失敗完成隔離。188+7+1分批新增，未重跑首輪。315個窗口時間取樣仍計為105個情境，不膨脹案例數。ground lease不等於動畫owner，本輪不宣稱900/3000完整流程。

成果是補齊可重現的行為證據，沒有需要修正的產品缺陷，因此沒有為增加版本而改碼。原來源資格與P28/P29歷史計數保持，原生驗收仍為0。兩個新Goal均由工作者真工具建立及完成並留讀回；主管工具仍缺少Goal，不以文件冒充。

下一批有限缺口：真S30飛行、資源故障、900/3000受控transport及事件計數、原地圖合成與opacity、通用封裝工具RG2。原生同版驗收另受授權及使用者服務條件限制。這份里程碑不代表全案完成。

P32 build-01原始SDK訊息聲稱安裝ASP.NET Core HTTPS開發憑證；持久憑證庫效果未查明。已在後續helper停用自動產生及PATH修改，沒有信任／移除／清理操作。環境偏差保留於../../work/P32-pickup-lifecycle-review-r1/environment-deviation.json；不得宣稱完全零憑證副作用。

正式入口仍r33。先前向唯一整合者派發r34入口與project-manifest切換被自動核准審查拒絕，理由是缺少這次持久啟用的明確授權；未執行、未重試、批准仍待答。隔離工作不依賴此批准。DB／服務／原生遊戲未操作，永久刪除0。

主管接受收據：P31-P32-root-acceptance.json。詳細獨立核對：P31-root-review.json、P32-root-review.json；下一步依continuation-status.json與current-finite-backlog.json，不重用歷史running狀態。
