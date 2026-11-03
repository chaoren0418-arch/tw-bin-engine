# P21 弓箭遠程與有限彈藥交付

worker CLI 與遊戲／Web 流程已通過，來源與環境已凍結，以 ready-for-supervisor.json 的版本與雜湊為準，等待主管獨立驗收；實際 Goal 保持 active。原 JAR 與 DB 只啟動一次，繼續保留，不自行收尾或進入 P22。v1 封存清單漏列 global.json 與 scripts/，依主管交還修正覆蓋範圍，歷史完整保存在 evidence/freeze-v1；產品、素材與建置未變。

## 成果與證據

- 原弓172、箭40743、gfx0王族，原持弓／射擊／箭矢素材與原時序；共用遊戲 G／Web 單次攻擊，無產品自動射擊或追怪。
- 兩端 C123/S30/D338/gfx66、原朝向、距離大於相鄰格、原箭絕對減量、擊殺、EXP／自然金幣、死亡／移除、保存重登。細節見 PROTOCOL.md、result.json。
- CLI 三輪為80→80（原丟棄拒絕，fail）、80→70（10支原消耗，fail）、70→0（pass）；UI 三輪80→79（可見像素重選失敗）、79→76（原目標移除，未證死亡）、76→0（pass）。完整失敗與實際保存值均保留，後續Fresh=false逐欄接續。
- evidence/finite-arrow-ledger.json核對每角色有限80支全去向；每輪independent-ranged.json獨立解析雙端全世代raw與applied boundary，不依產品decoder自證。CLI最後22,720欄、UI最後43,603欄。
- CLI最終EXP416461922（兩次原擊殺，包括保留失敗輪）、UI最終EXP416461902／金幣27／HP528／MP120；兩者箭0、弓與環甲保持裝備、位置32588/32930。只反映原保存結果，不重設受傷值。
- 8組針對性回歸：原遠程65向量／2535截短／2025射程邊界／原彈藥絕對狀態；C123延遲閘門；原時序／多投射物／深度合成；受影響combat/inventory/ground/equipment向量及equipment gates。最後乾淨重建0警告0錯誤，DLL hash與已驗證版本相同。
- evidence/visual-review.json列出實際view_image檢視的PNG。主角DrawToBitmap、WebView2 CapturePreview與實際旁觀連線同渲染器圖片分開標明。r2飛行PNG保留為有效局部證據，不改稱整輪通過。

## 重建與驗證

在本目錄用 PowerShell：

```powershell
.\build.ps1 -Clean
python -B -X utf8 run_ranged_regressions.py
python -B -X utf8 verify_ranged.py evidence\ranged-worker-cli-r3\result.json
python -B -X utf8 verify_ranged.py evidence\ranged-worker-ui-r3\result.json
```

這些命令只建置／離線重播。worker角色箭已耗盡，不能重新跑fresh或重置。主管應真複製乾淨來源至自己的驗收目錄，保留同一原服務身份，使用未動的supervisorCli/supervisorUi與各自Observer；原測試入口是 `python -B -X utf8 ranged_integration.py cli supervisor` 與 `ui supervisor`。私密測試設定仍只在D:/P21Lab-ClientEngine/private-tests.json，不寫入交付報告。每次失敗必須新的證據目錄與Fresh=false，不覆寫。

## 邊界與生命周期

只寫P21階段及D:/P21Lab-ClientEngine。原JAR24180／launcher83176、DB79464／launcher73080；port42049／43321，合成42050／42051目前不用。身份以server-start.json/db-identity.json及freeze收據為準，不以PID單獨識別。

ready-for-supervisor.json發布後，工作者不再改來源、登入、重啟或清理。待主管具體 `functional-acceptance-passed-handback-for-cleanup` 收據，才使用原console `shutdown 0`、等JAR與launcher退出、再次核對同DB、原SQL `SHUTDOWN` 並等DB與launcher退出；無強殺。cleanup_p21.py只在該門檻後執行。最終更新完整manifest及正常退出證據後才實際Goal complete。

本階段不代表全遠程、魔法或完整遊戲。原規則禁止丟箭，不能宣稱真實箭地物拾取；動態超距僅原AOI移除／本機stale拒絕，LOS原阻擋分支為來源核對。P18舊UI8肉未證／拾取動畫未畫、P19無自然地物與真實升級、P10舊EXE0/11未驗收等限制保留。
