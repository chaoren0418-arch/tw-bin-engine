# Issue #449 報告：T175 聊天格式與顏色對齊原版

## 做了什麼
- `engine/source/PlayerChat.cs`
  - 行格式：拿掉本機加的 `[一般]`／`[喊話]` 等標籤，原樣顯示伺服器字串（`name: msg`、`[name]`、`{name}`、`{{name}}`、`(name)`、`-> (target)`）。S73 收密語改成原版 `(name) msg`（原本是 `name -> 我：msg`）。座標分享行維持 `[座標] …`。
  - 頻道列舉在尾端追加 `WhisperTo, Trade, Prince, ChatParty, Alliance, ClanNotice`，既有序號不變。
  - 接收：S243 type 12 交易、13 盟主、15 同盟、17 血盟公告；S81/14 改歸 `ChatParty`；S243/9 送密語回覆改歸 `WhisperTo`。只收不送。
- `engine/source/PlayerHudChat.cs`：顏色改用原版調色盤（ui-layout.md §3）。一般 #FFFFFF、喊話 #FFE6CE、全體 #ADEFEF、收密語 #FFCECE、送密語 #FFF7B5、血盟 #C5EFB5、隊伍 #FFB500、交易 #BDA56B、盟主 #FF84FF、聊天隊伍 #BDEFE6、同盟 #CED6FF、血盟公告 #00BD00。
- `engine/_integration/dev/player-chat-tests/Program.cs`：更新並擴充測試。
- `.github/workflows/build.yml`：在 windows-latest job 加一步執行 PlayerChatChecks。這組測試原本沒有接上 CI。
- `engine/_integration/dev/rebuild-lock.json`：以 `--update-lock` 重產。
- `docs/protocol/player-chat.md`：在文末追加 T175 段落。
- 共用分派點（`WorldSession.cs`／`LoginConnection.cs`）沒有改，沿用既有的 `ReceivePlayerChat` 掛接。

## 輸入前綴（刻意不改）
`! & @ # "` 與送出編碼維持原狀。ui-layout.md 把前綴字元與頻道的對應（含 `$`→交易 12）標為**推測**，所以不新增 `$`，也不改既有前綴。

## 怎麼驗證（已驗證）
- `rebuild.py --update-lock` 之後跑 `rebuild.py --build`：PASS，productDll `663bedf3…faa24`。
- `player-chat-tests`：PASS 138 項，全部是合成封包。
  - 每個 S 格式都餵過實際 `WorldSession.Receive`：S81 type 0／2（含 x,y）／14，S73，S243 type 3／4（`{}` 與 `{{}}`）／9／11／12／13／15／17。核對分派的頻道、speaker、行文字與 `FormatLine`，確認沒有本機標籤。
  - S243/9 系統訊息仍交給 Inventory。S81/7 和 S243/14／16／18 不會被聊天模組攔走。
  - 每個 C 格式都有逐 byte 核對：C136 type 0／2／4／11、C40/3、C184。
  - 12 個頻道的 RGB 值都與調色盤相符，彼此互異，RichTextBox 實際行色也正確。
- 回歸測試：demo-tests `location-share`（375）、`player-query`（977）、`buddies`（445）全部 PASS。
- `check-no-game-assets.py`：pass。
- 伺服器端：唯讀 grep `1-伺服器/.../serverpackets`。d287 只會送 type 12（`S_ChatTransaction`）與 S81/14（`S_ChatParty2`）。13／15／17 沒有寫出點；同盟走 type 4 加 `{{name}}`，`0x0d` 那行已被註解。

## 沒驗證什麼
- 沒有開真機，畫面上的實際顏色與行格式留給 M6 smoke。
- CI 綠燈要等 PR 跑完才知道，新加的 CI 步驟也是第一次在 windows-latest 上跑。
- 13／15／17 在 P24Lab 伺服器不會出現，只用合成封包測過。
- 喊話：伺服器字串是 `<name> msg`，原版 Printer 的格式是 `[name] msg`。目前照伺服器原樣顯示，沒有改寫；方向前綴 `\d` 與頭上對話泡泡沒做。
- 以下規則沒有實作：一般 `*` 開頭改黃、全體 `*` 開頭改紅、訊息自帶色號（type 18／25）、色碼 `\f`／`\b` 轉譯、血盟的第二色 #BDBDBD。
- 聊天窗版面（362×240、頻道分頁）屬於 T174，本張不做。

## 產出路徑
- 分支 `task/449`
- 程式：`engine/source/PlayerChat.cs`、`engine/source/PlayerHudChat.cs`
- 測試：`engine/_integration/dev/player-chat-tests/Program.cs`
- 文件：`docs/protocol/player-chat.md`、本報告

## 給下一棒的提示
- T174（聊天窗框架）可以直接用 `PlayerHudChatPanel.ChannelColor` 與新列舉做頻道分頁。列舉只能在尾端追加。
- 若要支援 `$` 交易送出，需要先在送出鏈 `sub_7DD700` → `sub_7F2320` → `sub_6C6C60` 找到前綴對照表，並核對 `C_Chat` 是否處理 type 12。
- 本機執行測試：`engine/_integration/dev/player-chat-tests/run.ps1` 預設用 nuget.org 的設定。離線環境請改用 `--configfile engine/NuGet.Config` 手動 restore。
