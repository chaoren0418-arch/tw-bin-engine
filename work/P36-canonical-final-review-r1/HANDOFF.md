# P36 canonical 最終 provenance 獨立覆核

結論：accepted-p36-canonical-source-product-provenance。接受本次唯一canonical source→build證據，交主管核發P36 final acceptance。P37實際inventory未執行、未接受；沒有新增runtime/native驗收或正式入口啟用。

主管最終pins均相符：execution artifact seal c7c194fed021123fcca4586df4b9b05d5381e0f6838d24e759728d8598bf52bf，manifest 95ce307d92645d98d4bdaf58e663e10564c75c9d0f23d24a6a17925907068d7e（202檔完整集合），external seal 257d957cfc3c6b7061c1face5baeaf19c111f229a76c3834a3c90d68ab60ffa5；result a86b0e4ea202d4dc02c71aec25a7c97e2609874a7b20662d839c0971509f0a8e。最終core pins與暫審完全一致，因此不重跑reader或核心審查。

## 真實來源與建置

canonical source/audio/web完整181檔與已接受ledger after一致，五個變更的before/installed journals吻合，四個原件backup獨立且匹配before hash。新resource已加入；incoming已原子移走，不是原地修改共享硬連結。15個輸出與新manifest全量核對，15個size/SHA均與P30 accepted stage參照相同。新manifest d55389e048aee9eb2b64088ddfb0128de6166071e21969bd1dd57b9464d9ce75；source manifest f3275b4f202ee0d277e5749e85e2e10297396ce8f9bafa70696509a91027f95d。

獨立使用已接受P30 reader副本進行唯讀binlog replay，未build任何helper。7,269事件、唯一BuildFinished成功、零warning/error、兩個Csc任務的start→command→finish上下文與canonical project精確對應。audio 5個原始C#、product 142個原始C#，各加3個execution私有obj generated sources；不是以複製P30DLL充當編譯。實際Csc命令也逐字存在本次raw stdout。兩個private中間DLL與new build DLL雜湊相同且非同檔。

兩個EmbeddedResource命令參數來自canonical source/Resources並與csproj相符；產品DLL exact SHA同已接受P30，沒有另行推測新增資源語意。208個實際使用toolchain pins核對；私有NuGet／audio reference檔與已接受P30編譯引用hash一致，未重跑4,350全量依賴或舊遊戲suite。

product 6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3；audio d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4。單一build-01、exit0、owned-job assigned後resume、process tree結束、無timeout/interruption；zero warnings/errors在raw summary與binlog均一致。

private obj按OfflineClient/P24.Audio分開，實際PathMap映回原source/audio邏輯路徑；cache/temp/NuGet/CLI home/appdata位於專屬execution root。raw stdout確認首次child的certificate/global-tools-PATH禁用與telemetry off，契約spec及MSBuildProgramFiles32確認固定Program Files (x86)。HOME/USERPROFILE/CODEX_HOME未被覆寫。受保護入口/project-manifest、原r34 build/product manifest/launcher及release pins全部相同，assets junction target不變；只驗保護清單，未操作憑證或服務。

## 初次覆核錯誤保留

core-review-01 exit1是reviewer日誌格式假設錯誤：要求raw literal PROGRAMFILES(X86)，但MSBuild顯示衍生MSBuildProgramFiles32。audit_core-initial.py與原stdout/stderr/exit均保留。修正只改對應名稱，結合原契約spec驗證後core-review-02 exit0。不是產品失敗，沒有重建。另一次探索python -c因shell quoting失敗，對話工具紀錄保留；修正後只讀摘要成功。

reader-01、core-review-01、core-review-02、finalize-01的argv/cwd、原始stdout/stderr、hash及真實exit在commands；reader-command.json記錄真正PowerShell reader argv。final-verification另核202封存成員、181外部source、15輸出、新manifest、28個獨立工具/authority證據副本及既有core不變。

唯讀重驗：

```powershell
python -B -X utf8 "D:\lineage開發\TW1608262200研究\work\P36-canonical-final-review-r1\verify_delivery.py"
```

一次性reader/audit/finalize腳本不在封存根重跑寫入。source/artifact seals在finalize命令輸出與exit落盤後建立。所有review輸出只在本根，未改任何plan/review/execution封存或engine。平台Goal仍歷史blocked，沒有新建替代或修改狀態。

下一步：主管P36-final-acceptance綁定本result/seal及真實execution/source/manifest四身份，再另派P37 inventory-only。相同DLL既有runtime證據只繼承、不計新；本覆核不接受完整release/native/launcher重建。

原入口啟用拒絕保留且未重試/改道：ENGINE-R34-ENTRY-ACTIVATE-P26-R1 send_message_to_thread automatic approval review denied。原文：「此派工會持久修改正式入口與 project-manifest，將 r34 提升為現行產品版本，屬外部／正式承諾；使用者只授權引擎開發，未明確授權這次入口啟用，且政策不以先前助手派工替代該核准。」正式入口保持r33。
