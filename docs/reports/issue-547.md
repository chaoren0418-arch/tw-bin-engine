# Issue #547 T229：S250 聊天訊息類子碼（protocol-gaps B1）

依據：`docs/reference/protocol-gaps.md` 第 2.2、2.3、8 節（字串 id）；欄位寬度依 `docs/reference/server-protocol.md` 的 3.81 寫法（`S_PacketBoxWar`、`S_PacketBoxItemLv`、`S_PacketBox`、`S_PacketBoxHpMsg`）；文字用 T228 的執行期字串表 `ServerMessageText.Table`（原客戶端 `string-c.tbl`，不存進 repo）。

## 做了什麼
- 新檔 `engine/source/PacketBoxMessages.cs`：一個方法 `ReceivePacketBoxMessage` 疊 12 個 `[WorldReceive(250,Subtype=n)]`，另有 `[WorldReset]`、`[WorldSnapshot("packetBoxMessages")]`。**沒改 `WorldSession.cs`**。每包先完整解析、驗證（含 padding），通過後才改狀態、寫聊天欄；格式錯誤丟 `ProtocolException`。

| 子碼 | 3.81 欄位 | 結果 |
|---|---|---|
| 0／1／2 | C(城堡) H(0) | 聊天欄字串 639／640／641，`%s`＝城堡名（字串 912＋編號） |
| 3／4 | 無 | 聊天欄字串 642／643 |
| 12 | C(min) C(max)（3.81 只送 (min,0) 或 (0,max)） | min>0 → 672、max>0 → 673，`%d`＝等級 |
| 15 | C(n) | 聊天欄字串 679，`%s`＝字串 679＋n（3.81 送 1 火、2 水、3 風、4 地） |
| 23 | S(名稱) | 聊天欄字串 764，`%s`＝名稱 |
| 79 | C(count) S×(count−1)，無主寫 `" "` | 1511；有主城時再印 1513 標題，每個有主城一行 1514（城名、血盟） |
| 27 | 無；或 C(階級) S(名稱) | 只存資料 `RankChange` |
| 30 | D(id) S(名稱) S(血盟) | 只存資料 `LastavardWin` |
| 31 | 無 | 只計數 `FeelGoodCount` |

- 聊天行寫進現有 `playerChatLines`（Speaker 0，與 `Castle.cs` 同一條路），聊天視窗不必改。另有 `PacketBoxMessageLines`（子碼、字串 id、參數、文字）、`PacketBoxMessageReceived` 事件與快照 `extensions.packetBoxMessages`。
- 格式化：字串表這幾筆是 C 格式（`%s`／`%d` 依序代入），另支援 `%數字`、`%%`；顏色控制碼用 `ServerMessageText.DisplayText` 去掉。id ≥ 表筆數時不顯示（照原版 S71 規則），計入 `dropped`。沒有字串表（CI）時顯示「原服訊息 id：參數」，和 `ServerMessageText` 一樣。
- 新檔 `engine/source/PacketBoxMessagesTests.cs`：`[WorldModuleTest("packetbox-messages")]` 69 項，由現有 `--module-tests` 入口與 CI 步驟自動執行。用的是**合成字串表（假文字）**，沒有遊戲字串進 repo。
- `engine/_integration/dev/rebuild-lock.json`：`--update-lock` 重產（297 個來源、306 個鎖定檔）。

## 怎麼驗證（本機 Windows）
- `rebuild.py --update-lock` → `--build --configfile scratch/issue-547/NuGet.Config`（指到原專案 `engine/vendor`，唯讀）：PASS，productDll SHA-256 `d8d8e8a8eeea3c87f42dca3049929753771eef2a39cdf38db85da612b127d3cc`。
- `P11.ClientEngine.exe --module-tests`：結束碼 0，23 組 1225 項全過（含新的 `packetbox-messages` 69 項、`packetbox-status` 86 項、`world-extensions` 39 項），Default 登錄表沒有錯誤。
- `packetbox-messages` 涵蓋：12 個註冊項目、各子碼依 3.81 寫法合成的封包、城名／屬性名查表、79 有主與全部無主兩種、27 兩種寫法、只存資料的子碼不寫聊天欄、id 超出表筆數不顯示、沒有字串表時的 fallback、全部被消化（沒留在 `unhandledSubtypes`）、S250/56 仍由 `PacketBoxStatus` 處理、9 種格式錯誤一律丟例外且狀態和聊天欄不變、Reset 清空。
- 用本機原客戶端字串表跑 scratch 探針（輸出沒進 git，看完已刪），聊天欄文字正確，例如「奇岩城的攻城戰開始。」、「等級45以上才可使用此道具。」、「忽然全身充滿了火的靈力。」、「新村長由…選出」、「已佔領城堡的血盟如下：」＋「[肯特城 = … 血盟]」。這也確認了 672／673 的 min／max 對應和 679＋n 的意思。
- `engine/tools/check-no-game-assets.py`：pass，0 錯誤。

## 沒驗什麼／推定
- 沒有真實伺服器封包，只用依 3.81 欄位簽章合成的封包；沒啟動遊戲、伺服器或 DB。遠端 CI 結果以 PR 頁面為準；DemoChecks 等其他 CI 步驟沒在本機跑。
- **城名取字串 912＋編號是推定**：本機表 913–920 依序是 8 座城，TW16 case 0x1E 也取 919；但 case 0／1／2 實際怎麼取城名沒展開。
- **79 的排版是推定**：1511／1513／1514 的字串與 `[%s = %s 血盟]` 格式已確認，但「無主城不列」「全部無主時不印 1513」是推定。
- 27（TW16 交給 `sub_B32AB0`）、30（3.81 沒有呼叫者，TW16 讀 `dd` 跟 3.81 寫的 `D S S` 寬度不同）、31（TW16 讀 `d`，沒有字串 id）原版怎麼顯示待驗證，所以只存資料、不寫聊天欄。27 的 `C S` 寫法如果是階級 0 加空名稱，會和不帶欄位的寫法分不出來，目前當成不帶欄位。
- 0–2 TW16 讀 `d`，3.81 寫 `C H(0)`，照 3.81 解析。

## 產出路徑
- `engine/source/PacketBoxMessages.cs`、`engine/source/PacketBoxMessagesTests.cs`、`engine/_integration/dev/rebuild-lock.json`、本報告；分支 `task/547`。

## 給下一棒的提示
- 原生聊天視窗不用改，S250 訊息已經在 `PlayerChatLines` 裡。要套原版顏色的話，可以讀 `PacketBoxMessageLines` 的字串 id 和原始字串表文字（目前去掉了 `\f` 控制碼）。
- 27 的階級名稱、30 的反王訊息（782／783）要等反編譯展開 `sub_B32AB0` 與 case 0x1E 才能決定顯示方式；資料已經在 `RankChange`、`LastavardWin`。
- 本機建置照 #512／#548 的作法：`py -3.12`、worktree 另寫 NuGet.Config 指到原專案 `engine/vendor`。用 scratch 探針看真實字串時，記得不要把輸出寫在專案根目錄（`dotnet run` 會把 `--configfile` 之後的參數當成程式參數）。
