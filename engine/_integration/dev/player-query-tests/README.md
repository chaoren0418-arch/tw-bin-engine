# 玩家查詢離線測試

測試執行實際產品 DLL，以合成封包與記憶體 sender 驗證，既有 `SocketGuard` 禁止 socket；不啟動遊戲或伺服器，不讀取遊戲素材。

先執行 `rebuild.py --update-lock`、`rebuild.py --build`，再還原既有 DemoChecks 專案。於 repo 根目錄執行（輸出目錄須先建立）：

```powershell
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-357/player-query.json player-query
```

已透過 DemoChecks.csproj 引用，並接至 `player-query`、預設全套及既有 CI 執行的 `map-continuity` 群組，不另改 workflow。

覆蓋 C206 空 S／中文 S、命令分派與一般聊天逃脫、輸入限制、S71／166、S39／y_who 九欄、控制字元顯示、所有截斷長度、非法計數／padding／身份、真實 WorldSession 分派、NPC 對話與 RaiseAttr、S161 既有資訊顯示、100 行上限／重設、送出節流與 session／world／actor／死亡／排隊失效。

C99 與 C63 不是本伺服器的玩家資訊／排行榜查詢；測試驗證 `/check`、`/rank` 明確拒絕且零封包，而非用虛構格式冒充已支援。詳見 `docs/protocol/player-query.md`。
