# Issue #196／T108：buff 與狀態圖示

## 做了什麼

- 擴充 `MagicStatus.cs`：S216 五種防禦效果；S250/154 的 20 種共存代理與五種原生武器圖示；光環、Icon1／2、204-byte 狀態快照、藥水、變身、禁言、聖結界與自身風之枷鎖。
- 同技能更新倒數，零秒、解除旗標與到期會移除；原生圖示清除不會誤刪代理狀態。完整檢查封包後才寫入狀態。名稱不再依賴只有四種技能的 MagicSpells 素材。
- 新增 `StatusIcons.cs` 的 HUD 狀態列：自行繪製正／負狀態符號、名稱、剩餘秒數、完整 tooltip／無障礙文字與溢出翻頁。`PlayerHudView.cs` 僅增加四個顯示掛接點；共用封包分派不需修改。
- 只更新 dev lock；未改 `docs/REPORT.md`、封存、遊戲素材、伺服器、DB 或機密設定。

## 怎麼驗證（已驗證）

1. 先執行 `python -B -X utf8 engine/_integration/dev/rebuild.py --update-lock`，再執行 `--build --configfile .github/nuget.ci.config --output scratch/issue-196/build`。產品、audio、launcher 建置成功；173 個來源檔、182 個 lock 檔通過核驗。產品保留既有 `MagicUi.cs:20` 的兩個 nullable 警告，本次新增程式無編譯警告。
2. `dotnet restore engine/_integration/dev/status-icons-tests/StatusIconChecks.csproj --configfile .github/nuget.ci.config`。
3. `dotnet run --project engine/_integration/dev/status-icons-tests/StatusIconChecks.csproj -c Release --no-restore -p:UseSharedCompilation=false -- scratch/issue-196`：**626 個斷言通過**。直接餵入正式 WorldSession.Receive，覆蓋映射、共存、更新、最大 ushort、到期、解除、重設、他人效果排除、快照與原生來源隔離，以及截斷／非法旗標／padding 不得部分更新。測試完全不需要素材、遊戲程序、網路連線或 DB。
4. 隔離的 WinForms StatusIcons 控制項驗證 300／600／1000 寬度、空狀態恢復訊息列、完整無障礙文字；已目視檢查 `scratch/issue-196/status-icons.png` 的合成控制項截圖。這不是遊戲畫面或真機證據。
5. `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass、errors=0。CI 狀態見 PR checks；本報告不以本機編譯代稱 CI 或真機通過。

本機 Python 使用 `C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe`。建置／測試暫存、DOTNET_CLI_HOME、NuGet cache 均設定在本 worktree。測試須使用 `UseSharedCompilation=false`，避免共用 compiler server 的檔案寫入權限問題。

產品 DLL SHA-256：`b42c73fcfeb1ed1dd32ff3ed7ccdd19274dca96d7b279eda9cfe35ea206bcf63`。

## 未驗證什麼／限制

- 未啟動遊戲、登入、伺服器或 DB；原生 HUD 在完整場景中的視覺、實際 buff 發送順序與解除時機留待 M6 smoke。
- 批次倒數有量化；弱化／疾病短時間與長時間使用不同倍率卻沒有旗標，僅能顯示近似值。推測短效果可能高估，已明確標「約」，以後續伺服器零格解除。不能把顯示倒數當成角色能力權威。
- 未支援所有 packetbox overload；Icon1／2 型別沒有可靠名稱對照時顯示中性編號。詳細邊界與來源 SHA 見協定文件。HUD 圖示為自繪符號，沒有導入舊客戶端美術。
- 未改共享 CI workflow；本模組的 626 項離線測試於本機執行，既有 PR CI 另檢查跨平台建置與既有回歸。

## 產出路徑

- `engine/source/MagicStatus.cs`、`StatusIcons.cs`、`PlayerHudView.cs`
- `engine/_integration/dev/status-icons-tests/`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/protocol/status-icons.md`
- `docs/reports/issue-196.md`
- 本機、不入 git：`scratch/issue-196/build/`、`scratch/issue-196/status-icons.png`

## 給下一棒的提示

Claude 審查 PR；合併時由調度器按 M6 合約重新生成 dev lock，不手拼 hash。dot 在 M6 smoke 檢查防禦／元素武器共存、神聖武器原生清除後代理仍在、禁言／疾病解除、跨圖持續、重登清空，以及窄視窗翻頁與倒數到期消失。優先對照弱化／疾病的短倒數；若需精確值，須另外裁定帶秒數的協定，不能從量化 byte 猜回原秒數。
