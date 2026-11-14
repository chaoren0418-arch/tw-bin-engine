# Issue #1230 — f1210-loopback-silent-session：loopback 改用 session 音量表區分「他人 active 但靜音」與「他人真的在出聲」

結論：`Record-SmokeLoopback` 不再一看到其他 active render session 就拒錄。改成對每個 active session 讀取音量表的 peak：其他程式全程 peak ≤ `1e-4` 時照常錄音，超過門檻或讀不到音量表才拒錄，拒錄時不留 WAV。原本的嚴格行為改成 `-RequireExclusive` 參數。這次只做了靜態編譯與語法檢查；契約檢查和實際錄音交給 `f1210-tooling-retest` 的 R01／R07。

前置：開工時 PR #1224（`task/1210`）**尚未合進 main**，依任務指示先 `git merge origin/task/1210` 再改。因此 PR diff 會連帶包含 #1210 的檔案（`SmokeLoopback.cs`、`SmokeTools.ps1`、`Test-SmokeTools.ps1`、`Test-NativeClient.ps1`、`UiaGdi.ps1`、`README.md`、`docs/reports/issue-1210.md`）。#1224 合併後，本 PR 實際的增量只有下面 4 個檔案加上本報告。

## 第 2 次嘗試：合併前閘門退回的修正

閘門指出 `work/f1118-uia-path/SmokeLoopback.cs` 被 `.gitignore` 的 `work/*` 排除，卻被加進 git。`.gitignore` 對這個目錄只放行 `*.ps1`（#1148）與第一層 `*.md`／`*.json`。這個 `.cs` 是 #1210（PR #1224，至今仍未合併）引入、本分支合併 `origin/task/1210` 時帶進來的。

修正方式（不改 `.gitignore`，維持任務檔案範圍）：
- 把 C# 原始碼**逐字**搬進 `SmokeTools.ps1` 的新函式 `Import-SmokeLoopback`（單引號 here-string ＋ `Add-Type -TypeDefinition`，型別已載入就直接返回），並 `git rm` 掉 `SmokeLoopback.cs`。
- `Record-SmokeLoopback` 改呼叫 `Import-SmokeLoopback`；`Test-SmokeTools.ps1` 的 `Add-Type -Path SmokeLoopback.cs` 改成 `Import-SmokeLoopback`（它在前面已 dot-source `SmokeTools.ps1`）。
- `SmokeTools.ps1` 維持純 ASCII、無 BOM（Windows PowerShell 5.1 對無 BOM 檔用 ANSI 碼頁讀取，非 ASCII 註解會被誤解碼，這次新增的註解也用英文）。
- README 註明原始碼位置改變。
- 因此本 PR 對 main 的 diff 不再含 `SmokeLoopback.cs`。**注意**：PR #1224 本身仍新增了被忽略的 `SmokeLoopback.cs`，若它先合進 main 再合本 PR，本 PR 的 `git rm` 會把它移除，結果一致。

第 2 次的驗證（已驗證）：
- 用 PowerShell AST 從 `SmokeTools.ps1` 抽出 here-string，與上一版 `SmokeLoopback.cs`（`8ca9312d`）比對：內容完全相同。
- 抽出的原始碼以 `csc.exe /t:library /warn:4` 編譯：exit 0；DLL 只放 `scratch/issue-1230/inline/`，未載入、未執行。
- `Parser::ParseFile` 剖析 `SmokeTools.ps1`、`Test-SmokeTools.ps1`：0 個語法錯誤。

## 做了什麼（第 1 次）

只改 `work/f1118-uia-path/` 的 4 個檔案；沒改產品程式、`rebuild-lock.json` 和 `docs/REPORT.md`。（第 2 次起，下文的 `SmokeLoopback.cs` 內容改在 `SmokeTools.ps1` 的 `Import-SmokeLoopback` 內。）

1. `SmokeLoopback.cs`（現內嵌於 `SmokeTools.ps1`）
   - session 檢查時機跟原本相同：開錄前、錄製中約每 20 ms、結束後。每個 Active session 都會從 `ISessionControl2` 的 IUnknown `Marshal.QueryInterface` 取得 `IAudioMeterInformation`（IID `C02216F6-8C67-4B5B-9D00-D008E73E0064`），再直接呼叫 vtable 第 3 格的 `GetPeakValue`。不建立第二個 RCW，避免和 session control 共用 RCW 參考計數；用完 `Marshal.Release`。
   - 純函式 `ClassifySession(isOwn, state, peak, threshold, meterOk, crossProcess, requireExclusive)` 的判定規則：
     - 非 Active → `inactive`。
     - 自家 PID 而且不是跨程序 session → `own`（記錄 `OwnMaxPeak`、`OwnPolls`）。
     - `-RequireExclusive` → `other-active-exclusive`。
     - 讀不到 meter，或 peak 是 NaN、∞、負值 → `other-active-unmetered`。
     - peak ≤ 門檻 → `other-active-silent`（繼續錄）；否則 → `other-audible`。
   - 跨程序 session（`AUDCLNT_S_NO_SINGLE_PROCESS`）和系統音效 session 一律當成「其他」來量測，即使 PID 剛好等於客戶端也一樣。
   - `IsRejecting()`：`other-audible`、`other-active-unmetered`、`other-active-exclusive` 會丟 `SmokeLoopbackRejectedException`，立刻停錄。`RejectReason` 是代碼，`RejectDetail` 寫 pid、程序名、種類、peak 與門檻（或 meter HRESULT）。
   - 拒錄時，如果 WAV 已經由本次 `CreateNew` 建立，關檔後就刪除（`WavKept=false`）；開錄前就拒錄的話，根本不會建檔。
   - 錄音工具自己的 PowerShell PID 如果出現 session，沿用舊版的做法略過，記為 `capture-host-ignored`（見「推測」）。
   - 保留舊的 3 參數 `Record(process, path, seconds)`，等同非嚴格模式；另新增 4 參數多載加上 `requireExclusive`。
2. `SmokeTools.ps1`：`Record-SmokeLoopback` 新增 `-RequireExclusive`，傳給 4 參數 `Record`。
3. `Test-SmokeTools.ps1`：新增離線契約檢查，不碰音訊裝置。
   - 門檻常數等於 1e-4。
   - 11 組分類輸入：自家有聲、他人靜音 active、剛好等於門檻、他人有聲、無 meter、NaN peak、跨程序靜音、跨程序有聲、非 active、嚴格模式下的他人靜音、嚴格模式下的自家。
   - 拒錄集合恰好是 3 種 verdict。
   - `SmokeLoopbackResult` 具備新舊 JSON 欄位。
4. `README.md` 的 `f1186-retest`／WASAPI loopback 段落：
   - 更新用法（含 `-RequireExclusive`）、判定規則、新 JSON 欄位，以及「`other-audible`／`other-active-unmetered` 記 ⚠️（環境）跳過、不關他人程式、不改音量或裝置設定」。
   - 提醒型別簽章變了，要開新的 `powershell.exe` 才能重新載入。
   - 函式索引和契約測試說明同步更新。

### 門檻值來源
`1e-4` 採用任務內文的建議值：以滿刻度 1 正規化，20·log10(1e-4) = -80 dBFS，遠低於可聽程度。它是程式常數 `SmokeLoopback.SilenceThreshold`，每次都寫進 JSON 的 `Threshold`。沒有開參數讓呼叫端調整，避免重測時各自放寬。

### 新 JSON 欄位範例（示意，數值不是實測）
```json
{
  "Path": "...\\scratch\\f1210-retest\\client-A\\loopback.wav",
  "Completed": true, "HasWaveform": true, "WavKept": true,
  "Frames": 144000, "CapturedSeconds": 3.0, "Rms": 0.031, "Peak": 0.42,
  "Threshold": 0.0001, "OwnMaxPeak": 0.39, "OwnPolls": 140, "SessionChecks": 140,
  "RequireExclusive": false, "RejectReason": null, "RejectDetail": null, "Error": null,
  "OtherSessions": [
    { "Key": "{0.0.0.00000000}...|chrome.exe%b{...}", "Pid": 8928, "Name": "chrome", "Kind": "process",
      "State": 1, "MaxPeak": 0.0, "Polls": 140, "UnmeteredPolls": 0, "LastMeterHResult": 0,
      "Verdict": "other-active-silent", "CrossProcess": false, "SystemSounds": false }
  ],
  "Scope": "Default render/eMultimedia endpoint mix; own PID active; every other active render session (incl. cross-process/system sounds) had session meter peak <= Threshold at every ~20 ms poll; sound between polls is not excluded; not process-isolated"
}
```
拒錄時的例子：`"Completed": false, "WavKept": false, "RejectReason": "other-audible", "RejectDetail": "other-audible: pid 8928 (chrome, process) peak 0.0123 vs threshold 0.0001"`。

## 怎麼驗證（已驗證）
- `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /t:library /warn:4` 編譯 `SmokeLoopback.cs`：0 error、0 warning。這和 Windows PowerShell 5.1 `Add-Type` 用的是同一代 C# 5 編譯器。輸出的 DLL 只放 `scratch/issue-1230/`，沒有載入也沒有執行。
- `[System.Management.Automation.Language.Parser]::ParseFile` 靜態剖析 `SmokeTools.ps1` 和 `Test-SmokeTools.ps1`：都是 0 個語法錯誤。
- vtable 順序對照 Windows SDK `audiopolicy.h`／`endpointvolume.h`：
  - `IAudioSessionControl2` 依序是 GetSessionIdentifier、GetSessionInstanceIdentifier、GetProcessId、IsSystemSoundsSession，接在 `IAudioSessionControl` 的 9 個方法之後，和 #1210 原本的宣告相同。
  - `IAudioMeterInformation` 在 IUnknown 之後的第一個方法就是 `GetPeakValue(float*)`，也就是第 3 格。
- GitHub Actions CI：見 PR。CI 不編譯這段 `Add-Type` interop，只能證明產品建置沒被影響。

## 未驗證／推測／待驗證
- **待驗證（依使用者 2026-11-09 規定，本機不執行）**：
  - `Test-SmokeTools.ps1` 全部契約檢查（含新的 `loopback-classify-*`、`loopback-reject-set`、`loopback-json-fields`），交 `f1210-tooling-retest` R01。
  - 實際錄音：Chrome 靜音 active session 下 `Record-SmokeLoopback -Seconds 3` 應該錄到波形，Chrome 的 `Verdict=other-active-silent`。交 R07。
  - 重測時請另外錄一次「他人真的在出聲」的情況，確認 `other-audible` 會拒錄而且不留 WAV（任務驗收沒有強制）。
- **推測**：
  - 從 `IAudioSessionControl` QI `IAudioMeterInformation` 是 Microsoft 文件記載的 per-session 音量表取法，但沒有在 19045 實測，所以 QI 可能失敗。失敗時會得到 `other-active-unmetered`，和舊版一樣拒錄，不會誤錄。
  - session 音量表反映的是該 session 的串流 peak，一般還沒套用 endpoint 音量。如果使用者在音量混合器把 Chrome 調成靜音，meter 可能仍然顯示原始 peak，導致拒錄。這個方向是偏保守的。
  - `GetPeakValue` 是近一段處理週期的 peak，不是自上次呼叫以來累積的最大值，所以 20 ms 輪詢之間的短促聲音可能漏掉。這點已寫進 `Scope`，屬於 endpoint 混音的既有限制。
  - 錄音工具自己的 PowerShell PID 沿用 #1210 的略過做法（記 `capture-host-ignored`），因為錄音流程中 PowerShell 本身不播放聲音。
- **已知限制**：Windows 10 build 19045 沒有 `AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK`（需要 20348+），所以仍然不是程序隔離錄音。

## 產出路徑
- `work/f1118-uia-path/SmokeTools.ps1`（含內嵌的 `SmokeLoopback` C#）、`Test-SmokeTools.ps1`、`README.md`；`SmokeLoopback.cs` 已刪除
- `docs/reports/issue-1230.md`（本檔）
- 分支 `task/1230`（含 `origin/task/1210` 的合併）

## 給下一棒的提示
- 調度器：PR #1224 本身也新增了被 `.gitignore` 排除的 `SmokeLoopback.cs`，閘門大概也會擋它；可考慮讓 #1224 採同樣的內嵌做法，或直接由本 PR 一併合入（本 PR 已移除該檔）。#1224 合併後本 PR 的 diff 就只剩 #1230 的增量。如果先合本 PR，等於連 #1210 一起合進 main，請依合併順序裁定。
- `f1210-tooling-retest`：
  - 開**新的** `powershell.exe` 再 dot-source。舊程序裡已載入的 `SmokeLoopback` 型別沒有 4 參數多載。
  - R01 先跑 `Test-SmokeTools.ps1`；如果 `Add-Type` 編譯失敗，把錯誤原文貼回本 Issue。
  - R07 錄完附上 `<wav>.json`，重點看 `OtherSessions[].Verdict/MaxPeak/UnmeteredPolls`、`OwnMaxPeak`、`RejectReason`。
  - 遇到 `other-audible` 或 `other-active-unmetered`，記 ⚠️（環境）跳過，不關他人程式。
- 如果 R07 出現 `other-active-unmetered` 而且 `LastMeterHResult` 是 `0x80004002`（E_NOINTERFACE），表示 session control 不支援 QI 取得音量表。下一步可以考慮改用其他途徑，例如 endpoint 層級的 `IAudioMeterInformation`（`IMMDevice::Activate`）。這需要另開 code 任務。
