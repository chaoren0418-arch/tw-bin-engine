# 稱號與表情離線測試

使用實際建出的產品 DLL、合成封包、記憶體 sender 及 SocketGuard，不呼叫遊戲入口、不顯示遊戲視窗、不連網或 DB。

先更新 dev lock 並建置產品，還原 `demo-tests/DemoChecks.csproj` 後執行：

```powershell
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -- . scratch/issue-323/title-emote.json title-emote
```

已接入 DemoChecks 預設入口與既有 CI 執行的 `combat-admission` 群組。`TitleAndEmoteChecks.cs` 的封包、狀態、sender、SPR 解碼與稱號合成測試不需遊戲資源。檢查數可能因 Windows 字型的實際像素數而不同。

選用的本機唯讀原圖核驗（不啟動遊戲）：

```powershell
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-build -- . scratch/issue-323/title-emote-assets.json title-emote-assets
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-build -- 'D:\lineage開發\TW1608262200研究' scratch/issue-323/title-emote-compose.json title-emote-compose
```

第二條第一個參數僅設定既有 assets 的唯讀定位，執行的仍是本 worktree 建出的測試與產品 DLL；結果檔寫在目前 worktree。沒有建立 junction、複製素材或寫入原專案。

`title-emote-assets`：gfx0 四動作八方向，以及另外 12 個現有 PlayerProfiles gfx 的四動作方向 0；`title-emote-compose`：四種自身姿勢實際替換、像素輸出、遠方姿勢與選取 owner。

T169（#441）新增：`EmotePlanChecks`（原版四項順序、G1／G1'／G2／G3／G4／10413／11635 等外型在動作槽與選單兩條路徑的重映射、2500 ms 節流與速度係數公式）、`EmoteDeliveryChecks`（職業外型立即本地播放、變身外型只送不播且 2500 ms 間隔、移動中／商店姿勢不擋、攻擊／施法／自身表情播放中會擋、送出前換外型會拒絕），以及缺圖退回（`TryParseSteps`、`Fallback`）的合成測試；`title-emote-assets` 另以實際 6080 列核對退回姿勢 20 與退回動作 0。
