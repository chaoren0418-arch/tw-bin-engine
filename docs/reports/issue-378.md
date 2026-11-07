# Issue #378 T146：血盟戰爭

## 結論

血盟戰爭的宣戰、投降、停戰已實作，離線測試通過，尚未真機驗收。

## 做了什麼

- 新檔 `engine/source/ClanWar.cs`：C227 編碼、S84 解析、S219／217／221／222 確認與 C121 回答、交戰狀態、血盟戰爭視窗、敵對名稱標示。
- `Clan.cs`：加 `ClanWarEnemies`、`IsClanWarEnemy` 兩個查詢介面。
- `Trade.cs`：S219 分派加一個宣戰分支。放在求婚分支之前，才能看到每一則 S219，並作廢舊確認。
- `ObjectRendering.cs`：加一個 `PaintClanWarEnemies(g)` 呼叫。敵對血盟成員的名稱改為橘紅色加深色底。
- `WorldSession.cs`：加 `case 84`。Issue 的檔案清單沒有列這個檔，依 `m6-contract.md` 共同規則 2「一個 opcode 加一個 case」新增。
- 測試：`engine/_integration/dev/clan-war-tests/`，接上 `DemoChecks.csproj`、`clan-war` 群組、`map-continuity` 與預設入口。
- 協定文件：`docs/protocol/clan-war.md`，含 d287 位元碼核對與來源 SHA-256。

## 怎麼驗證

| 項目 | 結果 |
|---|---|
| `rebuild.py --update-lock` | 通過，213 個來源、222 個鎖定輸入 |
| `rebuild.py --build`（本機 vendor） | product、audio、launcher 全部通過 |
| `clan-war` 群組 | 479 項通過（戰爭本身 203 項，其餘 276 項是共用檢查） |
| 回歸群組 | clan 395、duel 390、marriage 412、map-continuity 22561、combat-admission 1409，全部通過 |
| `check-no-game-assets.py` | 通過，0 個錯誤 |
| d287 JAR | `javap` 唯讀核對 C_War、C_Attr、S_War、S_Message_YN、L1War，SHA-256 相符 |

同步 origin/main（含小地圖 `MinimapChecks`）後重跑：`--update-lock`（215 個來源、224 個鎖定輸入）、`--build` 通過，`clan-war` 479 項、`map-continuity` 22632 項通過，`check-no-game-assets.py` 0 錯誤。

開發中測試抓到兩個產品問題，都已修正：第三方廣播可能把己方戰爭擠出上限清單；切地圖時 `Actor` 短暫為 null，交戰清單會變空。

## 未驗證什麼

- 沒有真機：未啟動遊戲、伺服器或 DB，沒有實際雙人宣戰往返。
- 視窗與敵對名稱的畫面效果只通過編譯，離線測試不涵蓋繪製。UI 入口用 `OnLayout` 掛 `Shown`，也待真機確認。
- 無群組的預設入口未跑：worktree 沒有 `engine/assets` junction，與 #262 相同，失敗點在血盟測試之前。CI 不跑此入口。
- S84 文字取自伺服器註解，未與 `Text.pak` 逐字比對。

## 已知限制

- S84 的提示只顯示在血盟戰爭視窗，不進聊天欄。聊天模組的行是玩家頻道，不宜冒充。
- 同時多場戰爭時，伺服器登入只回報一場，重新登入後交戰清單可能不完整。
- 加入正在交戰的血盟時，伺服器不補送狀態，要到下一則 S84 或重新登入才知道。
- `S_HelpMessage` 的「攻城戰期間，暫停血盟宣戰」未處理。
- 城堡路徑（名稱 `" "`）與 `S_WarTime` 不在本任務範圍。

## 產出路徑

- `engine/source/ClanWar.cs`
- `engine/_integration/dev/clan-war-tests/`
- `docs/protocol/clan-war.md`
- `docs/reports/issue-378.md`

## 給下一棒的提示

- 真機 smoke 需要兩個君主角色，等級超過 25，各有血盟。核對：宣戰確認視窗、接受後敵對名稱變色、切地圖後仍保留、投降與停戰、重新登入後由 S84／8 恢復。
- 新增 S219 模組時，留意 `ReceiveClanWarYesNo` 必須維持在 `ReceiveTrade` 的第一個分支；若新模組在 Trade 之前就消耗 S219，要比照 Party／Duel 讓 `CheckClanWar` 觀察得到。

第二次同步 origin/main（4a962f3，含顯示通知 `DisplayNoticeChecks`）：`Program.cs` 測試入口衝突以「兩邊都保留」解決（`display-notices` case 與 `ClanWarChecks` 並存），`rebuild-lock.json` 取 main 版後 `--update-lock` 重產（216 個來源、225 個鎖定輸入）；`--build` 通過，`clan-war` 479 項、`display-notices` 334 項、`map-continuity` 22690 項通過，`check-no-game-assets.py` 0 錯誤。

第三次同步 origin/main（98bf2b7，含暫時能力 #416 `TimedBuffChecks`）：`Program.cs` 的 `map-continuity` 與預設入口衝突以「兩邊都保留」解決（`ClanWarChecks` 與 `TimedBuffChecks` 並存），`rebuild-lock.json` 取 main 版後 `--update-lock` 重產（217 個來源、226 個鎖定輸入）；`--build` 通過，`clan-war` 479 項、`timed-buffs` 560 項、`map-continuity` 22974 項通過，`check-no-game-assets.py` 0 錯誤。

第四次同步（c8ea735，GitHub 端把 main 2aa41de 合進來，含物品狀態 #216 `ItemStatus.cs`）：合併時沒有重產 lock，CI `dotnet` 失敗（`REFUSED: sourceCount 217 != pinned source files 218`）。本機 `--update-lock` 重產（218 個來源、227 個鎖定輸入）；`--build` 通過，`clan-war` 479 項、`item-status` 358 項、`map-continuity` 22974 項通過，`check-no-game-assets.py` 0 錯誤。