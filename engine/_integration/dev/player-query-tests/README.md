# 玩家查詢離線測試

測試執行實際產品 DLL，以合成封包與記憶體 sender 驗證，既有 `SocketGuard` 禁止 socket；不啟動遊戲或伺服器，不讀取遊戲素材。

先執行 `rebuild.py --update-lock`、`rebuild.py --build`，再還原既有 DemoChecks 專案。於 repo 根目錄執行（輸出目錄須先建立）：

```powershell
dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- . scratch/issue-357/player-query.json player-query
```

產品 DLL 重建後不要加 `--no-build`：DemoChecks 的 bin 裡是 DLL 副本，不重新建置就會測到舊版。

已透過 DemoChecks.csproj 引用，並接至 `player-query`、預設全套及既有 CI 執行的 `map-continuity` 群組，不另改 workflow。

覆蓋範圍：

- 送出：C206、C125（`lv_ranking`、`pc_list`、NPC action）、C223 的位元組向量；命令分派、一般聊天逃脫、輸入限制。
- 回包：S71／166 的玩家資訊與泛用訊息區分；S39 的 `y_who`、`pc_show`、`y_h_2`、`y_k_2`、`y_k_3`、`y_w_1`、`y_c_1`；控制字元顯示、所有截斷長度、padding、非法計數與身份。
- 流程：`/rank` 與 `/check` 走實際聊天傳輸；C223 必須等選取提示；逾時、目標變更、session 重設、失敗回覆；風雲榜 NPC 鏡射與對話並存；不鏡射未請求的頁面。
- 既有行為：真實 WorldSession 分派、NPC 對話與 RaiseAttr、S161 資訊顯示、100 行上限／重設、節流、session／world／actor／死亡／排隊失效。

C99 與 C63 不是本伺服器的查詢封包，測試斷言三個指令都不會送出。詳見 `docs/protocol/player-query.md`。
