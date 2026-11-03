# P36／P37 有限交付接受

2026-10-29 UTC。唯一主管 `01a185b4-ecf6-705c-90e6-83b9eb1ba02f` 已接回本輪實作、退修、共享整合及獨立覆核；兩位工作者最後回合均 completed／idle。本輪完成不代表全案完成，不報完成百分比。

## 已完成的遊戲功能整合

P30 已接受的額外 **41 組 exact gfx／weapon 自己拾取外觀**，現已合入 canonical `engine/source`：四檔替換、一個嵌入資源新增，完整 source／audio／web 共 181 檔。含控制組共 42 組，資格仍為 speed 0／1、Brave 0，沒有擴張未知素材語意。

唯一整合者從 canonical 專案執行一次產品／音訊組建，輸出為 `engine/build/client-product-p36-r1`。獨立 binlog 覆核確認兩個成功 Csc 任務、142 個產品 C# 原檔與 5 個音訊 C# 原檔，以及各三個私有生成來源。零警告、零錯誤，15 個輸出的大小與 SHA 全部重現 P30 候選。四份獨立備份、五個來源替換日誌及原始 stdout／stderr／binlog 均保留。

- 產品 DLL：`6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3`
- 音訊 DLL：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`
- [主管 P36 最終接受](P36-final-acceptance.json)，SHA `76145832e5865fad75741450f53500cfad455aa73507bb0a75709c35a86e1e17`

本輪新產品 runtime 執行為零；既有同 DLL 的 P28～P34 有限證據沿用，沒有假稱重跑 canonical 4521 或原生遊戲驗收。

## 已完成的工具修補與版本綁定

P35 的產品 manifest 漏列修補保留於新 P37 inventory-only 工具。獨立計畫覆核找到 P37-F1：入口未先驗證契約固定的 metadata 文件雜湊。作者在新 r2 根以一行修改接回 `validate(initial=False, dependencies=False)`；其餘 12 個 runtime／reference 檔相同，獨立差異覆核已關閉此缺口。r1 原件與退修證據保留。

使用真實 P36 完成收據，原封存 r2 工具實際執行一次，exit 0、stderr 空。210 列清單由另一位工作者獨立重建並逐列相符：181 來源、15 組建輸出、manifest 本身、4 個唯讀繼承 launcher 檔，以及 9 份證據。產品／音訊／產品 manifest／launcher 四個身分相符，33 個保護 pins 未變。覆核沒有再次執行 inventory CLI、build 或遊戲測試。

- [主管 P37 最終接受](P37-final-acceptance.json)，SHA `1e6cc6f0296f85484402f5c9ab7cf345275342f345924c2fb54e60fc5d834336`
- 實際清單：`work/P37-bound-inventory-execution-r1/inventory.stdout.json`，SHA `2bb58c3c48b576a213c05f5a9b2835e5f2ed4b717afe8fbbe8719009780bc778`
- P37 工具入口：`work/P36-P37-integration-plan-r2/bound_inventory.py`；精確參數與來源 pins 見上述執行根 `command.json`、`input-pins.json`。一般重驗讀保存清單與 seals，無需重跑 CLI。

這是版本綁定清單接受，**不是 P36 完整 release、launcher 可用性或原生驗收**。既有 r34 工具／封存不改；舊 completed_context 在來源變更後預期拒絕，沒有為了通過而改舊 pins，也未另外重跑它。

## 真實分工與回合

| 職責 | thread ID | 已完成回合 |
|---|---|---|
| 唯一共享整合者 | `01a17a22-17e4-70c0-9792-166ddb86ea42` | P36 `01a186e0-6f22-7df3-84b3-e6a670a2c3ee`；P37 `01a186ec-2d74-7c00-b64f-60f1170c9fa8` |
| 獨立覆核者 | `01a185b7-ea67-7053-9bfc-d9d90c1cff8f` | P36 `01a186e1-b0c9-7563-a4a5-be559c46c75a`；P37 `01a186ef-85b4-77e1-b2d9-56de7893885c` |

主管再次查工具清單，沒有 Goal API，沒有讀取／建立平台 Goal。整合者回報 null、覆核者歷史 Goal blocked；均未篡改平台儲存，未把文字文件當 Goal。

## 剩餘範圍與恢復條件

[本輪收斂差異](P36-P37-convergence-delta.json) 接續原 12 項待辦；[現行有限佇列](current-finite-backlog.json) 已更新 I01／S03，原 `remaining-convergence.json` 是已封存歷史快照，未改寫。

- P36 尚無新的完整 release／launcher 使用資格。後續如選此交付工作，須精確限定契約；它不必被誤認為必須先切換正式入口，也不能套用舊 180-source／八步完成收據。
- 最終固定版完整遊戲流程、原生畫面／滑鼠、交易保存一致性、安全跨圖及 world6016 音訊仍待原生驗收與使用者準備合法服務條件。DB／服務由使用者處理。
- 地圖一般群組淡出已有實作；map-local effect 的真實 record→group→fade/render 閉合案例仍缺來源。已讀回並核對既有報告 SHA；沒有新明確線索，因此未重做 24,760 筆有限盤點或另派重複研究。詳見 [來源判斷收據](P36-map-research-triage.json)。非 6016 token、312 個 pickup shadow keys 與 46 個不支援地圖區塊沒有藉此補造支援。
- 真實地圖與 S30 飛行既有實作的未執行組合，不自動等於產品缺陷；不為保持活動而新增無限測試階段。

本輪沒有 DB、服務、原生遊戲、憑證或永久刪除動作。產品組建一開始即關閉開發憑證生成；不改 HOME／USERPROFILE／CODEX_HOME。覆核腳本的一次日誌欄位名稱假設更正及主管外部清單 schema 判讀更正已留痕，均未重建產品。

## 正式入口仍為 r33

`啟動引擎.cmd` 與 `project-manifest.json` 的原 SHA 均未變。原 `ENGINE-R34-ENTRY-ACTIVATE-P26-R1` 派工遭自動核准審查拒絕，動作未執行／未重試／未改道；原因是缺少將正式入口與 project-manifest 升至 r34 的明確使用者授權。該問題仍 pending，不能把本輪 canonical P36 或 P37 清單接受視為批准入口切換。
