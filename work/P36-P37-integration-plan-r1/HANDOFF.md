# P36–P37 準備交接：只供覆核，尚未授權共享執行

唯一主管 `01a185b4-ecf6-705c-90e6-83b9eb1ba02f`；唯一共享整合者 `01a17a22-17e4-70c0-9792-166ddb86ea42`。本輪唯一寫根為本目錄。真 Goal 讀回 null，未建立／修改 Goal、模型或平台儲存，未增開代理／聊天。

已核對 P30/P35 exact acceptance 與指定 seals。P30 stage 347 檔、建置相依 4,350 檔已驗 SHA；P35 修復 generator 與五個 import dependency 分別作獨立副本，封存於 `tool-bundle/reference`，不修改或冒充原 accepted tooling。

## P36 精確來源變更

`source-ledger.json` 列出完整 181 檔的 target、before/after size/SHA、prepared copy、baseline backup 和 nlink。canonical 現為 180 檔；全部現有來源皆 regular、nlink 1。P30 accepted source181 全部獨立複製至 `prepared`；canonical source180 全部獨立備份至 `baseline`。

| 動作 | engine 相對目標 |
|---|---|
| 替換 | source/ObjectAssets.cs |
| 替換 | source/OfflineClient.csproj |
| 替換 | source/PickupAssets.cs |
| 替換 | source/PickupPresentation.cs |
| 新增 | source/Resources/pickup-own-base15-profile-extension.json |

其餘 176 檔位元組相同。現階段沒有寫入以上共享目標。功能資格沿用 P30 的額外 41 exact own-pickup profiles、含 control 共 42、speed 0/1、Brave 0；不增加 runtime assertions，不擴充來源語意。

## 未來執行白名單與回復

`execution-contract.json` 含唯一單次 argv、cwd、environment、輸入文件 pins、執行程式 pins、五個 source 白名單與禁止碰觸表。必須依序完成獨立覆核、主管 exact execution receipt，才可由唯一整合者執行。`gate.authorize` 位於 mutation entry 內，直接 Python 呼叫也不能繞過。預設驗證只讀；沒有 receipt 不會建立 execution root。

- 執行根：`D:\lineage開發\TW1608262200研究\work\P36-canonical-integration-execution-r1`（目前不存在，禁止重用）。
- 真實 build 輸入：canonical `engine/source/OfflineClient.csproj`，cwd canonical engine；audio 由原 ProjectReference 編譯。不得把 P30 DLL 複製為 canonical build 結果。
- 新輸出：`engine/build/client-product-p36-r1`；新 manifest：`engine/build/product-p36-r1-manifest.json`。兩者目前不存在。這是 P36 開發輸出，不是正式 r35 啟用。
- 私有 obj 位於 execution 根下依 project name 分開；temp、NuGet/cache、CLI home、appdata 也限該 work 根。`build-isolation.targets` 把私有 obj 映回原 `/_/p24/source/obj/` 或 `/_/p24/audio/obj/` 邏輯位置。是否重現 exact DLL 仍須未來單次 build 證明，不能以靜態檢查冒充。
- 首次子程序即關閉 ASP.NET certificate、global tools PATH 修改、telemetry、audit、shared compilation、node reuse；只使用 pinned offline NuGet package。固定 `PROGRAMFILES(X86)`。HOME、USERPROFILE、CODEX_HOME 原值傳遞、不改值、不記錄其值；父程序／系統環境不改。
- 執行時重新驗證 before pins/nlink，獨立複製 backup 和 incoming，從同磁碟 work incoming 原子替換共享 source，絕不原地寫硬連結。日誌記錄每筆 replace 前後狀態；新增檔不得覆寫既存目標。
- 單次 dotnet build，600 秒上限，沿用未修改的 `win_process.py`：suspended launch → owned job assignment → resume；只處理該 owned job。保存 argv、stdout/stderr 原始 bytes、實際退出碼、binlog、來源清單及所有輸出 SHA。
- 要求 zero warnings/errors、全181來源一致、受保護檔案不變、product `6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3`、audio `d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`。`expected-outputs.json` 列出15預期輸出路徑及 P30參照 SHA；未來 actualAfter SHA 保持 pending，不捏造。DLL 必須 exact；其他 actual hashes 全數記錄供審查。
- 任一失敗即停，不改 expected、不盲重跑、不重用 execution root。若五個 owned targets 仍為 before/after pins，依獨立備份原子還原四檔，新增 resource 移入 execution 的 preserved-new-resource，保留失敗 build 與診斷，不永久刪除。若有第三方 drift、缺少原件、reparse 或 hardlink，拒絕自動回復，保留現場等待精確 recovery 覆核。硬中斷或程序崩潰後不能自動續跑；應先依 started/journal 和 pins 診斷，再由主管另給 recovery receipt／計畫。

正式入口指向 `releases/r33/launcher/P24.Launcher.exe`。此鏈不與五個 canonical source 目標重疊。`protected-pins.json` 固定 root entry/project-manifest、engine r34 product manifest/build/launcher、r33/r34 release launcher 及 release seals；所有 releases、assets junction、其他封存／共享硬連結均不可寫。protected pins 前後重驗，assets 只確認原 junction target，不遍歷或寫入。

## P37 新工具版本的資格

新入口 `bound_inventory.py` 是 **inventory-only**，不是完整 release generator。它獨立使用 source181、新 P36 manifest、實際一次 canonical build result、raw logs/binlog 與新主管 receipts。原 P35 `package_inventory` 修復與依賴保存為未改的 reference；新 collector 延續 manifest 本身納入、四項 identity 完整性、精確來源／SHA、pin、no_links、reserved path、casefold collision 及 source/evidence closure。更換用途不會放寬原 full release gate。

新 tool seal `tool-bundle/source-seal.json` 綁定13個 runtime/reference 檔案。四項 identity 是新 P36 product/audio DLL、新 product manifest，以及**未重建、唯讀繼承**的 engine launcher DLL。launcher 出現在 inventory 不代表 launcher 已為 P36 驗收或封裝可啟動。工具只輸出 JSON 至 stdout；無 archive、package/release write 或入口切換 API。

必須取得以下真資料；`pending-receipt-requirements.json` 只是欄位需求，明示 `isReceipt=false`，未知 pin 均為 null：

1. 獨立 plan review：`p36-p37-plan-review-v1`，綁定 plan、contract、author artifact seal，接受串行 source/build。
2. 主管 `P36-execution-acceptance-r1.json`：綁定上述 review、全部三個 seal/hash、固定 execution/build/manifest、single build、exact DLL、source rollback 許可，並禁止 release/entry/runtime。
3. 真正 build 成功且 compiler/source provenance 獨立覆核後的 `P36-final-acceptance.json`：綁定 execution result、source181、product manifest、四 identities；不可宣稱重跑 canonical4521／舊8步。
4. `P37-inventory-only-acceptance-r1.json`：綁定 P36 final receipt、plan/contract/tool/artifact seals，明定 `fullReleaseAuthorized=false`。

P36 完成安裝後，舊 r34 `completed_context` 應因其舊 source180 pins 不再等於 current canonical 而拒絕；舊工具／原碼／封存 release 的歷史資格仍保留。若另外授權診斷，保存真實 refusal，不改舊 manifest、receipt、source seal 或 expected hashes 使它通過。P37 不呼叫該 context，也不補造8步／4521完成紀錄。

## 已驗證、未驗證與命令

已執行唯讀 executor validation，以及16個 bounded checks：6個 authority／狀態拒絕、1個子程序環境檢查、1個 synthetic四identity正向和8個缺失／錯誤identity拒絕。這些不是產品測試。未執行成功共享 install／rollback、build、真實新 acceptance 的 inventory；這些須未來授權及獨立覆核。

`commands/assemble`、`validate`、`checks`、`finalize` 保存每個實際子命令的 argv、cwd、exit、原始stdout/stderr及其SHA。最初 `prepare.py` 透過 exec 工具執行，該次工具 stdout/exit 的轉錄另存 `commands/prepare/tool-result.json`，不冒稱另有 raw pipe 檔。所有準備腳本為一次性，勿在封存目錄重跑寫入命令。

可重跑的唯讀命令（PowerShell）：

```powershell
python -B 'D:\lineage開發\TW1608262200研究\work\P36-P37-integration-plan-r1\verify_delivery.py'
$p36Root = 'D:\lineage開發\TW1608262200研究\work\P36-P37-integration-plan-r1'
$p36PlanSha = (Get-FileHash -LiteralPath "$p36Root\plan.json" -Algorithm SHA256).Hash.ToLowerInvariant()
$p36ContractSha = (Get-FileHash -LiteralPath "$p36Root\execution-contract.json" -Algorithm SHA256).Hash.ToLowerInvariant()
python -B "$p36Root\executor.py" validate --plan-sha256 $p36PlanSha --contract-sha256 $p36ContractSha
```

重算 SHA 僅供這個已由外部 artifact seal 固定的交付驗證，不授權接受任意變更。未來 execute 必須使用主管派工的外部固定 plan/contract/receipt SHA，不能自算未知 receipt 冒充派工。

source/artifact seals、plan/contract SHA 見 `result.json` 及最終回報。封存涵蓋全目錄，僅外層 manifest/seal 自身不列入 manifest，避免循環。完成後停寫，只交一次 sealed candidate 供獨立覆核。

原入口拒絕保留，未重試或改道：ENGINE-R34-ENTRY-ACTIVATE-P26-R1 的 send_message_to_thread automatic approval review denied，原文：

> 此派工會持久修改正式入口與 project-manifest，將 r34 提升為現行產品版本，屬外部／正式承諾；使用者只授權引擎開發，未明確授權這次入口啟用，且政策不以先前助手派工替代該核准。

本輪 shared writes/builds/DB/SQL/service/GUI/native/game/socket/network/credential changes/deletions 均為0；未重跑 P28/P31–34、RG1、canonical4521 或舊44命令。正式入口仍 r33。
