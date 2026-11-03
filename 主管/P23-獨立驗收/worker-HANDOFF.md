# P23 交付（工作者驗證完成，交主管獨立驗收）

產品已實作原場景群組透明、恢復、混色與可見像素選取，原生/Web共用renderer。具體來源、驗證分類與限制見 PROTOCOL.md。

啟動與測試工作目錄：D:/lineage開發/TW1608262200研究/P23-原場景群組透明與遮擋。原始素材與歷史階段唯讀。

## 可重跑離線檢查

```powershell
.\build.ps1 -Clean
python -B -X utf8 run_complete_regressions.py
```

完整22項為既有19項＋本次原狀態、完整像素、真失敗重播3項。所有早期失敗保留 evidence/complete-regressions-*，修正說明見 PROTOCOL.md。opacity_sources.py、opacity_native_oracle.py、opacity_pixels.py 為原來源/oracle產生器；不必在驗收freeze上覆寫，請在主管真副本執行。

## 同首次原服環境

一次初始化 PID76108 exit0，terminal receipt initialization-complete.json。同第一次原JAR PID75900；DB67280，UUID d399e000-d080-11f1-ae98-4cedfb679789。game42057 wildcard受loopback peer policy限制，DB127.0.0.1:43323。合成42058/42059未當正式服務。

原JAR SHA d287bc4b3a38f237754accda9110dd6f894f461f3451b19a8f7c91e29de141cf。原SQL及policy/hash/OS身分見初始receipt。四主管角色保留fresh；私密憑證僅D:/P23Lab-ClientEngine。

## 有限live命令

```powershell
python -B -X utf8 opacity_integration.py cli worker 3 evidence/opacity-worker-cli-r2/db-proof.json reference/opacity/live-route-resume-cli-r2.json
python -B -X utf8 opacity_integration.py ui worker 2 evidence/opacity-worker-ui-r1/db-proof.json reference/opacity/live-route-resume-ui-r1.json
# freeze後由主管對四個reserved角色使用owner supervisor，各只首次1
python -B -X utf8 opacity_integration.py cli supervisor 1
python -B -X utf8 opacity_integration.py ui supervisor 1
python -B -X utf8 opacity_live_verify.py evidence/opacity-worker-cli-r3
```

最新42步fresh／48步UI saved-resume來源路線 reference/opacity/live-route-plan.json，ROUTE-AMENDMENT.md記錄避開旁觀者占位修改。50步/90秒、每等待2秒、焦點2次/2秒、死亡/斷線/HP<150/任一受擊即停。只有測試controller走規劃路線，產品無自動走路/戰鬥。每次正常登出／重登，同DB保存SELECT後退出。任何失敗不得重建/換角色/補給/重啟，從保存狀態Fresh=false明示接續。

## 收尾

工作者live、原x86事件、保存與圖片已通過；source/environment freeze由ready-for-supervisor.json具體收據標記。ready後工作者不改檔、不登入、不清理；保留相同首次DB/JAR供主管獨立clean build、來源、CLI/UI及PNG驗收。必須明確functional-acceptance-passed-handback-for-cleanup才console shutdown0、同DB SQL SHUTDOWN並確認全部ports/process結束。最終manifest全部檔案，僅排除根manifest本身；actual Goal最後才complete。


注意：worker上述命令為已執行歷史／明示接續，不可在已消耗角色上盲目重跑。freeze後只有主管使用保留四角色，先驗source/environment manifest及runtime身份；既有worker資料保留，不再登入。


## 已驗證結果

- 最新乾淨build 0 warning/0 error；evidence/complete-regressions-r6-1792941388832464900：全部22項exit0。OpacityTests 59原x86 state/group/timer檢查點，五幅10727424完整color/mask均零差異。
- worker CLI r3從原r2 savedstate正常接續44步，43原S10＋1原S87逐步相符；兩端52個實際透明事件重跑原x86全等；正常重登、exit0、原SQL保存通過。
- worker UI r2從UI r1 savedstate正常接續48步，48原peer位置相符；兩端111個實際透明事件原x86全等；真ActiveForm/own foreground、原OnKeyDown/Up與Web DOM、雙介面可見observer選取、正常重登及exit0。main最終HP504是原服自然恢復，無補藥或SQL補血。
- UI r2十組DrawToBitmap／CapturePreview／observer同renderer／source PNG；403860個Web scene像素核對通過。物理DOM rect以兩端round，非texel整數邊界RGB精確，整數tie僅允許原鄰接texel，不使用任意色差容忍。原fractional約26k mismatch診斷保留於UI r1，沒有假稱舊算法通過。
- 已實際view_image檢視，見evidence/actual-image-review.json；嵌入WebView佔位不計Web證據，沒有採用screen.png。
- CLI r1/r2、UI r1所有失敗、舊EXE/DLL/PDB、raw/SQL/PNG完整保存。修正與每次具體續跑邊界見ROUTE-AMENDMENT.md與PROTOCOL.md；不重建、重啟、補發或換角色掩蓋。

主管應使用未登入的supervisorCli/Ui及其Observer，在自己真副本clean build驗證後按最新fresh42步來源路線登入同一首次服務。四角色initial資料仍未消耗。工作者Goal此時active，驗收通過後仍需正式cleanup交還才正常關服。
