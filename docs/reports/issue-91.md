# Issue #91／T006：兩個目錄的位元可重現建置

日期：2026-11-06（Asia/Taipei）；階段：code；第 1 次嘗試；分支：`task/91`。

## 結論／已驗證

**通過 T006 本次驗收**。兩個獨立 worktree 在相同修正提交、相同 SDK 下，清除各自 `obj/`、`bin/`、`engine/build/` 後重新建置，product、audio、launcher 三個 DLL 的 SHA-256 完全相同；三個 portable PDB、兩個 Windows apphost 和產品 manifest 也逐位元相同。不是複製某一邊產物代替建置，沒有後處理改寫 DLL。

`--check-only`、dev 預設來源核驗、receipt 核驗與 `test_rebuild_paths.py` 全部 exit 0，7 項測試通過。修正前後的三個組件 IL byte sequence SHA 與 CLR 內嵌資源 SHA 均不變。

建置修正提交 `6d3727ebbd4a700b9b51620cd94825e98f966c9c` 的 GitHub Actions [build #86](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42137698343) 已確認 `completed / success`。本報告是其後的文件提交；最新提交狀態另以 [PR #92 checks](https://github.com/chaoren0418-arch/tw-bin-engine/pull/92/checks) 為準，交付前會再次確認，不以舊 run 冒充新提交驗收。

## 工作目錄／來源與隔離

| 角色 | 完整路徑 |
|---|---|
| 主專案（保留原狀） | `D:\lineage開發\TW1608262200研究` |
| A：工作用 | `D:\lineage開發\TW1608262200研究\scratch\wt\task-91` |
| B：比對用，完成後已移除 | `D:\lineage開發\TW1608262200研究\scratch\wt\task-91-b` |

- 已讀根目錄 `AGENTS.md`、`docs/REPORT.md`、新 worktree 的 T006、#88 報告與建置／測試腳本。任務的具體隔離要求優先於通用 `checkout -B` 前言。
- fetch 後兩邊均從 `origin/main@aaf07f6846b350545f28ea5dcded2f8f92fd1e83` 建立。首輪兩邊 HEAD 相同，排除首輪比較的 commit 差異。
- 修正後 B 以 `git checkout --detach task/91` 對齊上列 `6d3727e`；最終兩邊都是乾淨建置，不重用首輪 obj。
- 主目錄前後仍為 `task/77@50d09756722961827411d2cca5d7900a1cf75f3b`，追蹤檔狀態乾淨。主目錄既有 DLL 交付前讀取得 `44eb5843f25833f2da1cdf7081eabeb7b8cd6e27562423e2d06b150ac787d477`，未重建或覆寫；既有未追蹤截圖／receipt 等未動。
- 兩邊各建立 `engine/assets` junction 指向主專案 `engine/assets`，唯讀引用既有 r28 素材；各複製唯一離線套件 `microsoft.web.webview2.1.0.4258.31.nupkg` 至自己的 `engine/vendor/`，未複製遊戲素材。
- Python 為既有 3.12；SDK `10.0.300`；Windows x64、.NET runtime `10.0.8`。NuGet 使用各自的離線 `engine/NuGet.Config`，packages cache 也各自獨立。
- 外層 TEMP／TMP／DOTNET_CLI_HOME／NuGet packages、HTTP cache、scratch 都在各自 `scratch/issue-91/`。rebuild 子程序依腳本改用該輪 `--output` 下的 tmp／.dotnet；沒有寫使用者家目錄或系統 TEMP，未安裝軟體。
- sandbox 程序初始化在真正執行前失敗（`helper_unknown_error: setup refresh had errors`），改以獲准的提升權限執行同一工作區命令。`rg` 不在 PATH，改用 Git／PowerShell 搜尋；未因此跳過驗收。

## 先歸因：首輪原始輸出

首輪 `rebuild.py --build` 實際 argv 明確包含 `-c Release`，既有 `PathMap` 因而有效。product、audio、launcher 的 PDB 文件名稱與 CodeView 路徑兩邊已相同；例如 product 是 `/_/p24/source/obj/Release/net10.0-windows/P11.ClientEngine.pdb`。因此「腳本其實建 Debug」不是本次原因。

| 首輪檔案 | A SHA-256 | B SHA-256 |
|---|---|---|
| `P11.ClientEngine.dll` | `0ce9085abe1de111072eb2c17138b9980998a34450b6b1b6e061e69f18f04f29` | `342ca063f8aaf40e5891c2b7d357c01f6cfbaf3baed3dd3b8f9a5d4fe2c1a1a2` |
| `P24.Audio.dll` | `e0122a1f1ee3d7300541cb1dc0ab8f6e28b1e27cf6454d4947620bca1e1fc9d1` | `096867db7a13604c8198c2c9b0ee219f9836601daf600f79ed9bf43fcd373cc8` |
| `P24.Launcher.dll` | `8a276771db5f4962b6b2e8b23d9395f6c2b7057ac2e559c1af08e812fe8fb4d2` | `652dd1d91d72c76a66d65d3bb2b824ed63ce97199eb97592dc62e41623dee45b` |

使用既有 .NET SDK 的 `System.Reflection.Metadata`／`PEReader` 建立一次性唯讀分析器（`scratch/issue-91/inspect/`，產物在自己的 `bin/obj`）。讀取 MVID、COFF header、debug directory、assembly attributes、CLR resources、method IL、portable PDB documents 及 Source Link custom debug information，再用 Python 對全部位元組差異分類。工具沒有載入執行產品。

**已驗證根因：Source Link 的 checkout 根目錄沒有被現有專案層級 PathMap 正規化。** PDB 中 GUID `cc110556-a091-4d38-9fec-25ab9a351a6a` 的 JSON 如下（只縮寫共同路徑前綴，實際完整內容已留證）：

```json
A: {"documents":{"D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-91\\*":"https://raw.githubusercontent.com/chaoren0418-arch/tw-bin-engine/aaf07f6846b350545f28ea5dcded2f8f92fd1e83/*"}}
B: {"documents":{"D:\\lineage開發\\TW1608262200研究\\scratch\\wt\\task-91-b\\*":"https://raw.githubusercontent.com/chaoren0418-arch/tw-bin-engine/aaf07f6846b350545f28ea5dcded2f8f92fd1e83/*"}}
```

三個 DLL 的差異全部落在以下識別值；沒有無法分類的不同位元組。位址一律是 **File Offset**，不是 RVA／VA；範圍是欄位首尾，部分欄位剛好有相同 byte，故實際差異數可能小於欄位長度。

| 檔案 | 欄位 | File Offset（含首尾） | 不同 bytes |
|---|---|---|---:|
| `P11.ClientEngine.dll` | MVID (#GUID) | `0x20fa1c–0x20fa2b` | 15 |
| `P11.ClientEngine.dll` | COFF deterministic timestamp | `0x88–0x8b` | 4 |
| `P11.ClientEngine.dll` | CodeView PDB ID | `0x4c25d8–0x4c25e7` | 16 |
| `P11.ClientEngine.dll` | Debug directory PDB stamp | `0x4c2584–0x4c2587` | 4 |
| `P11.ClientEngine.dll` | PDB SHA256 | `0x4c2632–0x4c2651` | 32 |
| `P24.Audio.dll` | MVID (#GUID) | `0xc1c8–0xc1d7` | 16 |
| `P24.Audio.dll` | COFF deterministic timestamp | `0x88–0x8b` | 4 |
| `P24.Audio.dll` | CodeView PDB ID | `0xfa7c–0xfa8b` | 16 |
| `P24.Audio.dll` | Debug directory PDB stamp | `0xfa28–0xfa2b` | 4 |
| `P24.Audio.dll` | PDB SHA256 | `0xface–0xfaed` | 32 |
| `P24.Launcher.dll` | MVID (#GUID) | `0x49e8–0x49f7` | 16 |
| `P24.Launcher.dll` | COFF deterministic timestamp | `0x88–0x8b` | 4 |
| `P24.Launcher.dll` | CodeView PDB ID | `0x5390–0x539f` | 15 |
| `P24.Launcher.dll` | Debug directory PDB stamp | `0x533c–0x533f` | 4 |
| `P24.Launcher.dll` | PDB SHA256 | `0x53e8–0x5407` | 32 |

總差異數依序為 product 71、audio 72、launcher 71 bytes。兩邊各組件的 assembly custom attributes、串接 method IL SHA、CLR resource block SHA、PDB documents 均相同；CodeView 路徑也相同。COFF timestamp 有 `Reproducible` debug entry 對應，是 deterministic 識別值的差異，不能直接當成牆鐘時間戳不固定。

| 檔案 | 首輪 A MVID | 首輪 B MVID |
|---|---|---|
| `P11.ClientEngine.dll` | `40c305a4-3a42-4272-888e-b47b7f9f1326` | `d245450f-e1b7-488d-8803-41c85bd83f89` |
| `P24.Audio.dll` | `157dba4f-6b79-4bda-b722-995f14abe773` | `d5ab7b30-e037-4298-a290-b7ad25366c49` |
| `P24.Launcher.dll` | `085072b5-8212-49b2-8f83-eaff9ea1e852` | `eec006c0-b9b7-478b-87db-61d717a3fcfd` |

**另一個已驗證輸入：Git revision 進入 InformationalVersion。** 首輪三個生成的 `obj/Release/net10.0-windows/*.AssemblyInfo.cs` 都包含 `1.0.0+aaf07f6846b350545f28ea5dcded2f8f92fd1e83`，最終為 `1.0.0`。它不是本次「相同 HEAD」首輪的兩邊差異，但會使 #88／#77 那種產品來源相同、Git commit 不同的情況無法僅憑來源 lock 保證相同產物；Source Link URL 也帶同一 commit。本次沒有重新逐位元分析歷史 #77／#88 DLL，所以不宣稱它們的每個不同 byte 都已個別歸因。

`obj` 中的絕對路徑仍可存在於 MSBuild 中間檔；本次驗收的是產物。不同 NuGet cache 的路徑未造成修正後的 DLL／PDB 差異，因此不額外擴張 PathMap。

## 最小修正／影響

只改以下三個專案，均新增兩項屬性及一行說明，保留原始換行：

- `engine/source/OfflineClient.csproj`
- `engine/audio/P24.Audio.csproj`
- `engine/_integration/dev/launcher-source/Launcher.csproj`

```xml
<EnableSourceLink>false</EnableSourceLink>
<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
```

第一項移除 PDB 中含實體根目錄與 commit URL 的 Source Link JSON；第二項讓版本資訊維持既有 `1.0.0`，不附加 Git commit。後果是 debugger 不再透過 Source Link 自動下載來源；portable PDB、既有 Release PathMap 與 deterministic 設定保留。版本／來源追溯請使用 Git 提交與 dev lock／receipt，不再依賴 DLL 的 InformationalVersion commit 後綴。

沒有改產品 `.cs`、資源、邏輯、入口腳本、建置腳本或 frozen locks；無需切換 DebugType、增加 CI 屬性、設定虛假的 commit 或後處理 DLL。dev `rebuild-lock.json` 僅更新這三個 csproj 的大小與 SHA；仍是 190 個來源檔／199 個鎖定輸入。

dev lock SHA-256：修正前 `855863f1fc073b1bbc9a9adcc5ddde342e27bedbd04dd9d2715e71f985e9f802`；修正後 `5ff6bbd7003f64df051f272a88e83a38bc93b378d2230acd557bea1308bb4b70`。

## 最終 SHA-256 對照／全新建置

以下 A、B 各自重新編譯取得；不只比較 receipt，另直接比較檔案 bytes 並計算 SHA-256。DLL、PDB、apphost 名稱分別位於上列專案的既定 `engine/build/client-product-dev/` 或 `engine/launcher/`；manifest 為 `engine/build/product-dev-manifest.json`。

| 檔案 | bytes | A SHA-256 | B SHA-256 | 結果 |
|---|---:|---|---|---|
| `P11.ClientEngine.dll` | 4994560 | `6b2faa260cbc1e35c0d8240b574099fa6c6ebde22ac10e7b733b493b3aa382a7` | `6b2faa260cbc1e35c0d8240b574099fa6c6ebde22ac10e7b733b493b3aa382a7` | 相同 |
| `P24.Audio.dll` | 66048 | `c64a38a8165f5651a331c19cf580e64d0e59541d060aeebb69c71941bb04b52d` | `c64a38a8165f5651a331c19cf580e64d0e59541d060aeebb69c71941bb04b52d` | 相同 |
| `P24.Launcher.dll` | 24064 | `222e8a0d03c657a980c2d1c0ab24e46c2026e41b1ea17866b73029cf7452e63d` | `222e8a0d03c657a980c2d1c0ab24e46c2026e41b1ea17866b73029cf7452e63d` | 相同 |
| `P11.ClientEngine.pdb` | 371548 | `ef0ba08b67c371c99c75450f4843193f6a5de102988b09826b3f085f7414669d` | `ef0ba08b67c371c99c75450f4843193f6a5de102988b09826b3f085f7414669d` | 相同 |
| `P24.Audio.pdb` | 21996 | `ba1ace35a20b1319438f6adda7cc8b17324bd7548c8ac1ad0678bb1f2c41f9c6` | `ba1ace35a20b1319438f6adda7cc8b17324bd7548c8ac1ad0678bb1f2c41f9c6` | 相同 |
| `P24.Launcher.pdb` | 13236 | `9cf2dc999d38e73ae7995ab83bf45913032e878825705d3dc9dc7ee5db50d370` | `9cf2dc999d38e73ae7995ab83bf45913032e878825705d3dc9dc7ee5db50d370` | 相同 |
| `P11.ClientEngine.exe` | 162304 | `73e816f7a6e6079443a3e4079d5ec2de46072b1d0869231586e4794301c9563f` | `73e816f7a6e6079443a3e4079d5ec2de46072b1d0869231586e4794301c9563f` | 相同 |
| `P24.Launcher.exe` | 162304 | `9ec5d1d644a2a4efddf083636c543f733dfc7a7a031ae6c3de48fb396aa98ada` | `9ec5d1d644a2a4efddf083636c543f733dfc7a7a031ae6c3de48fb396aa98ada` | 相同 |
| `product-dev-manifest.json` | 7953 | `cc3b2a3da7be32f43cc406be65f3adfd3fa5c51051dd4f0a8ad9fbc04a347542` | `cc3b2a3da7be32f43cc406be65f3adfd3fa5c51051dd4f0a8ad9fbc04a347542` | 相同 |

最終三組 metadata 亦逐欄相同，Source Link custom debug information 為空；前後 IL 與 CLR resources 未變。本次清理只針對 A、B：先將首輪 DLL/PDB 保存至各自 `scratch/issue-91/build/baseline/`，然後刪掉 source、audio、dev launcher-source 的 obj/bin 與 engine/build；另清掉 launcher 的 DLL/PDB/EXE，保留追蹤的 deps/runtimeconfig。所有遞迴刪除先驗證絕對路徑屬於指定 worktree、不是 reparse point。

## 命令／退出碼

Git 建立 worktree 命令在主目錄執行，其餘除明列者外均在 A 或 B 自己的目錄。下列 Python 使用既有 `py -3.12` 所列的 python.exe；一次性 runner 捕捉原始 stdout/stderr 與每次 exitCode。

| 位置 | 命令／動作 | exit |
|---|---|---:|
| 主目錄 | `git fetch origin` | 0 |
| 主目錄 | `git worktree add scratch/wt/task-91 -b task/91 origin/main` | 0 |
| 主目錄 | `git worktree add --detach scratch/wt/task-91-b origin/main` | 0 |
| A 首輪 | `python -B -X utf8 engine/_integration/dev/rebuild.py --build --output scratch/issue-91/rebuild-a` | 0 |
| B 首輪 | `python -B -X utf8 engine/_integration/dev/rebuild.py --build --output scratch/issue-91/rebuild-b` | 0 |
| A 修正 | `python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock` | 0 |
| B | `git checkout --detach task/91`（修正提交） | 0 |
| A、B | `scratch/issue-91/clean.ps1 -Worktree <A或B>`（A 的一次性腳本） | 0 |
| A 最終 | `python -B -X utf8 engine/_integration/dev/rebuild.py --build --output scratch/issue-91/final-a --write-receipt` | 0 |
| B 最終 | `python -B -X utf8 engine/_integration/dev/rebuild.py --build --output scratch/issue-91/final-b --write-receipt` | 0 |
| A | `engine\啟動新版客戶端.cmd --check-only`（經 cmd.exe /d /c） | 0 |
| A | `python -B -X utf8 engine/_integration/dev/rebuild.py` | 0 |
| A | `python -B -X utf8 engine/_integration/dev/test_rebuild_paths.py` | 0（7 tests） |
| A | `python -B -X utf8 engine/_integration/dev/rebuild.py --verify --output scratch/issue-91/verify-receipt` | 0 |
| A | PE/PDB 分析器、`compare.py`、`validate.py` | 0 |
| A | `git diff --check` | 0 |
| A | `git push -u origin task/91`（修正提交） | 0 |
| A | `git worktree remove --force <B絕對路徑>` | 0 |

每輪 rebuild 的 sdk、product-restore、product-build、launcher-restore、launcher-build 五個子命令皆 exit 0；四輪皆 0 警告、0 錯誤。實際建置命令形狀如下（cwd 為各自 `engine`，路徑由腳本展開為絕對路徑）：

```text
dotnet --version
dotnet restore <engine>/source/OfflineClient.csproj --configfile <engine>/NuGet.Config
dotnet build <engine>/source/OfflineClient.csproj -c Release --no-restore -o <engine>/build/client-product-dev --nologo -p:NuGetAudit=false -p:UseSharedCompilation=false
dotnet restore <engine>/_integration/dev/launcher-source/Launcher.csproj --configfile <engine>/NuGet.Config
dotnet build <engine>/_integration/dev/launcher-source/Launcher.csproj -c Release --no-restore --nologo -p:NuGetAudit=false -p:UseSharedCompilation=false -o <engine>/launcher
```

check-only 關鍵 JSON（其餘完整欄位留原始證據）：

```json
{
  "status": "checked-only",
  "revision": "dev",
  "manifestSha256": "cc3b2a3da7be32f43cc406be65f3adfd3fa5c51051dd4f0a8ad9fbc04a347542",
  "hashedRuntimeAndWebFiles": 46,
  "assetFilesCheckedForPresenceAndSize": 13957,
  "autoLogin": false,
  "uiStarted": false,
  "networkStarted": false,
  "servicesStarted": false
}
```

## 清理／產出路徑

B 已移除。清理前確認其追蹤檔乾淨、路徑精確等於本任務 B，先以非遞迴 `Directory.Delete` 移除 B 的 assets junction 本身，再 `git worktree remove --force`。`--force` 僅用來移除該臨時 worktree 的未追蹤 receipt／cache／建置證據；必要證據已複製至 A，未碰 junction 目標。清理後確認 B 不存在，主目錄分支、HEAD 與既有 DLL 如上。

提交範圍只有三個 csproj、dev lock 與本報告 `docs/reports/issue-91.md`，未更改 `docs/REPORT.md`、TASKS.md 或 PLAN.md。

A 下本機證據（不提交）統一在 `scratch/issue-91/`：

- `rebuild-a.*`、`rebuild-a/`、`final-a.*`、`final-a/`：原始命令、exitCode、stdout/stderr、result。
- `peer-evidence/rebuild-b.*`、`peer-evidence/rebuild-b/`、`peer-evidence/final-b.*`、`peer-evidence/final-b/`：B 的相同證據。
- `metadata-baseline.json`、`metadata-final.json`、`byte-diff.json`、`comparison.json`：PE/PDB、File Offset 與直接檔案比較。
- `build/baseline/`、`build/baseline-b/`：兩邊首輪 DLL/PDB；正式最終產物仍在 A 的既定 `engine/build`、`engine/launcher`。
- `verify-inputs.*`、`test-paths.*`、`check-only.*`、`verify-receipt.*`、`verify-receipt/`、`cleanup.txt`：驗收與清理。
- `run.py`、`checks.py`、`compare.py`、`validate.py`、`clean.ps1`、`inspect/`：一次性擷取／分析工具。分析器 source 與所有臨時腳本均在 scratch，未新增產品 `.cs`。

## 未驗證什麼／推測界線

- 未啟動遊戲、P24Lab、MySQL、伺服器，未連正式服務，未操作既有遊戲程序或機密設定；沒有原生實機驗收。
- 素材僅依 check-only 既有契約驗證存在／大小，沒有重新雜湊全部素材；checked-only 不等於畫面、登入、音效、戰鬥流程通過。
- 本次位元相同範圍是同一台 Windows、固定 SDK、Release、相同來源與套件，在兩個不同絕對目錄／獨立 NuGet cache。未聲稱 Windows/Linux、其他 SDK、Debug、其他套件版本或任意命令列覆寫也相同。
- CI 在 Ubuntu 交叉建置 Windows target；CI 成功證明既有 pipeline 通過，不等於 Linux 與本機 DLL 位元相同。
- 沒有為歷史 #77／#88 每個 byte 做完整比對；本次反例與修正均有自己的雙目錄證據。只證明上述 metadata／IL／資源性質，不以此替代遊戲功能測試。

## 給下一棒的提示

請 Claude／調度器審查 [PR #92](https://github.com/chaoren0418-arch/tw-bin-engine/pull/92) 的五檔差異與最新 CI，交使用者合併；主控據此更新 T006／M3 狀態。本機 A 保留為後續重現入口，B 已清理；不需切換或重建主目錄 task/77。之後重建請繼續使用 dev rebuild、相同 global.json 與 lock；來源追溯看提交／lock／receipt。若未來要恢復 Source Link，必須同時處理 source-root 映射與 commit URL 的重現性，並重新跑雙目錄驗收。
