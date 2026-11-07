# 門模組離線回歸

測試以合成封包及記憶體 sender 呼叫本次建置的產品 DLL，不啟動遊戲或連線。沿用 DemoChecks 的 socket 防護與手寫地圖 fixture；fixture 只寫入 `scratch/issue-322/synthetic/`，不包含遊戲素材。

先執行 `rebuild.py --update-lock`，再 `rebuild.py --build`。於 repo 根目錄執行：

```powershell
dotnet restore engine/_integration/dev/demo-tests/DemoChecks.csproj --configfile .github/nuget.ci.config
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-322/doors.json doors
```

`scratch/issue-322` 需先建立；隔離環境的 TEMP／TMP／DOTNET_CLI_HOME／NUGET_PACKAGES 應設於 worktree 內。CI 的既有 Windows job 已接上 `doors` 群組。

涵蓋 C41 HHD 明確 byte vector、S87 門的各 action／gfx、S158 開關／毀壞與非門分派、S209 兩方向與寬度、所有截斷長度及非零 padding、同圖重設、斷線、物件移除及 ID 重用、排隊發送前失效、不得預測開關、鍵盤移動與關門競態。

另涵蓋滑鼠路徑（開門穿越、關門拒絕與繞路、規劃後及排隊中關門、S158 不取代 S209、開門仍受地形與其他物件限制、斜向穿角），以及經由 `Renderer.ComposeObjects` 的播放（首／中／末幀、S87 停末幀、重複回包、反向切換、S209 與其他更新不影響、移除／替換／世界重設失效、未核驗 gfx 不繪製）。

播放用手寫的合成素材，放在 `scratch/issue-322/synthetic/render-*`，有 3 幀與 1 幀兩組，只驗證播放邏輯。測試不宣稱真實素材渲染或真機操作通過。
