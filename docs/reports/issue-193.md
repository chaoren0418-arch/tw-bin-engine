# Issue #193／T106：多頻道聊天

## 做了什麼

一般、喊話、全體、密語、血盟、隊伍皆可由聊天輸入送出，依伺服器回包顯示各自前綴與顏色。輸入方式為直接輸入、`!訊息`、`&訊息`、`"對象 訊息`、`@訊息`、`#訊息`；`/say 原文` 可在一般頻道保留命令符號。

送出對齊 C136/type0、2、4、11，C40/type3，以及 C184 的兩個字串。接收涵蓋 S81/type0、2、14、S73、S243/type3、4、9、11。喊話消耗座標尾段；密語 type9 與系統訊息分流。UI 有語法說明、正文長度／頻道提示、不同顏色與可讀前綴。

正文維持 1–52 個 UTF-16 字元及 CP950 編碼檢查；命令前綴／密語對象不占正文額度。沿用原有 session/world/actor 和傳送閘門，不追加本機假回聲。產品程式只改 `PlayerChat.cs`、`PlayerHudChat.cs`，既有鍵盤流程和共用分派檔案不需修改；dev lock 由 `--update-lock` 產生，沒有手動拼接 hash。

## 怎麼驗證（已驗證）

在指定 `task/193` worktree，使用 .NET SDK 10.0.300 與 Python 3.12，未啟動遊戲、伺服器或 DB。

| 檢查 | 結果 |
|---|---|
| `rebuild.py --update-lock` | 172 個來源、181 個鎖定輸入通過；只變動本模組兩個來源的 size／SHA |
| `rebuild.py --build --configfile .github/nuget.ci.config --output scratch/issue-193/build` | 產品與 launcher 成功；產品 0 errors、2 個既有 `MagicUi.cs:20` CS8600／CS8602 警告，launcher 0 warnings／errors |
| `powershell -NoProfile -ExecutionPolicy Bypass -File engine/_integration/dev/player-chat-tests/run.ps1` | **107 項通過**；執行實際產品 DLL、合成封包、記憶體 sender、未顯示的 WinForms 控制項 |
| `python -B -X utf8 engine/tools/check-no-game-assets.py` | 通過，0 errors；提交前再次檢查包含本次新增追蹤檔 |
| `git -c core.whitespace=cr-at-eol diff --check` | 通過；保留既有 `PlayerHudChat.cs` CRLF |

測試涵蓋逐 byte 送出格式與 padding、中文字 52／53 邊界、密語對象／格式錯誤、無法編碼字元、六頻道實際接收分派、喊話座標尾段、S243/type9 系統訊息回歸、截斷／錯誤 padding 不改歷史、100 筆上限、session reset、拒絕死亡／過期身分／世界、傳送中重複要求、等候時換世界、六頻道實際 RichTextBox 行色，以及開啟 Enter 不誤送。SocketGuard 禁止測試程式建立網路活動。

CI 由本分支 PR 的 `build` workflow 提供可追溯結果；既有 Windows／Linux CI 驗證與本模組的本機 107 項測試分開計算。本次沒有擴改共用 workflow，CI 不宣稱另有執行新聊天測試組。

## 未驗證什麼／已知邊界

- 未做登入、雙角色真實送收、原生畫面或中文輸入法驗收；留給 M6 smoke。
- 伺服器來源 SHA 與 #132 相符，但未證明其與部署 JAR 位元一致。禁言、等級、血盟／隊伍資格、全體消耗與伺服器限流仍由伺服器裁定。
- S243/type9 沒有密語／系統來源旗標；依來源定義的 `-> (對象) 訊息` 外形識別密語。若系統刻意送相同外形，協議無法區分。
- 交易與聯盟的獨立送出不在本任務範圍；聊天隊伍 type14 接收相容，隊伍送出使用 type11。

## 產出路徑

- 程式：`engine/source/PlayerChat.cs`、`engine/source/PlayerHudChat.cs`
- dev lock：`engine/_integration/dev/rebuild-lock.json`
- 可重跑測試：`engine/_integration/dev/player-chat-tests/{PlayerChatChecks.csproj,Program.cs,run.ps1}`
- 協議與來源識別：`docs/protocol/player-chat.md`
- 本報告：`docs/reports/issue-193.md`
- 本機證據（不進 git）：`scratch/issue-193/chat-checks.json`、`scratch/issue-193/build/`

| 產物／來源 | SHA-256 |
|---|---|
| P11.ClientEngine.dll | `8741d7aa3a5e06e5e08a96b547fed11bed5381413b44a8cbe379ca29d3f83cb9` |
| PlayerChat.cs | `b3ff2b6e96b7ab8873da8fc1991f42eae2c7bddfa711c6e80f5456915af3a99e` |
| PlayerHudChat.cs | `4a4c1661761b04a6573d7ce367710b4fe79da5191b0841ebf02f08ab0efdd53d` |

## 給下一棒的提示

Claude／調度器審查本 PR；dev lock 合併衝突依 M6 契約由同步輪重建。使用者合併後由 dot 在 M6 smoke 以兩個測試角色逐一檢查六頻道、密語雙向／離線對象錯誤、血盟／隊伍資格、全體消耗，以及不同解析度下的語法提示、顏色／前綴與 Enter／Esc。詳見協議文件末段。不要以本機合成封包測試替代真機驗收。
