# #988 T431：R12-D 信件結果字串

依據：`docs/reference/gap-matrix.md` 15.14.5／15.14.6（1:99）、`docs/reference/followups-15.md` §2.7；裁定 #980 第 2 點（`--classic-ui` 窗內文字一起改）。

## 做了什麼

- **`Mail.cs`（只動 notice）**
  - 新 `MailNoticeLine(Id, Prefix)`：記 string-c.tbl 編號。`For(type, result)` 照 `sub_941000` 分支：0x20 結果 1 → 1239，否則血盟信匣（低 4 位 1）→ 400＋1261，否則 1240；0x30 → 1243／1244；0x40 → 1241／1242；0x50 → 428；清單（0x00）、本文（0x10）→ 不印。
  - `Text`：有字串表就讀表（`ServerMessageText.Table`，去掉 `\f` 色碼）；沒有表（CI）就用 PakViewer 核對過的原文。
  - `MailView` 後面加了幾個選填參數，舊的 6 參數建構不受影響：`Line`（編號）、`NoticeSerial`（第幾行結果，同字串連續兩次也算兩行）、`Status`（引擎自己的訊息）、`Recent`（最近 8 行）。
  - `Notice` 現在**只放原版結果字串**。收到清單、本文時不再改 `Notice`。引擎自己的句子（「已送出寄信請求…」、欄位檢查錯誤、「收到無法解析的信件回包…」、「尚未要求信件清單。」）都移到 `Status`，不會進聊天欄。
  - 刪除／保管不論 id 在不在清單都印（`sub_941000` 不看清單；`sub_9B14C0`／`sub_9B3120` 只動窗）。清單只在刪除成功、而且 id 已知時才改。
- **`NativeUi/MailPanel.cs`（`MailNoticeRelay`）**
  - 比對 `Notice` 的同時也比 `NoticeSerial`，所以連刪兩封會印兩次 1243。
  - 新 `Lines()`：同一個畫面週期收到多筆回包時，從 `Recent` 逐行印（最多 8 行）。
  - 新的回包行在請求進行中照樣印，`Absorb` 也不會把它吞掉；`serial` 沒變的改動照舊吸收。
  - `Next()` 保留原本的介面。
- **`MailUi.cs`**
  - 加 `MailChatSecondColor = 14`（palette[14]，存成資料；聊天列 API 只收一個顏色，作法同公告板 189）。
  - `MailDialog.NoticeText()`：classic 窗內有 `Status` 時顯示 `Status`，沒有時顯示原版字串，後面照舊附「最近伺服器訊息」。
- **新檔 `MailNoticeStringTests.cs`**：3 組 `[WorldModuleTest]`（自動發現）：`mail-notice-strings`、`mail-notice-world`、`mail-notice-relay`。只用合成字串表與合成封包。

### 超出「可改檔案」的兩處（必要，請審查）

1. `engine/_integration/dev/mail-tests/Program.cs` 第 114、123、124、126 行：CI 的 MailChecks 斷言舊的引擎句子（「確認存入」「無法解析」「沒有成功 ACK」「無法寄出」）。本任務的目標就是換掉這些句子，所以改成比對 `Line == new MailNoticeLine(1241／1240)`，以及 `Status.Contains(…)`。其他斷言沒動。
2. `NativeUi/MailPanel.cs` 的 `Update` 一行：從 `Notices.Next(...)` 改成 `foreach (Notices.Lines(...))`，才能做到一筆回包印一行。其他 R12 組都沒有分到這個檔。

## 依據（已驗證）

- **IDA 7.7 唯讀反編譯** `sub_941000`（VA `0x941000`／FO `0x540400`）。IDB 是 P01 `ida/03-ascii-isolated/TW1608262200.idb` 的副本，放在 `scratch/t988/ida/`，查詢腳本照 `rea-scripts/t898/ida_t898.py`，輸出在 `scratch/t988/out/`，都不進 git。
  - 子類取 `type & 0xF0`，信匣取 `type & 0xF`。
  - 0x20 讀一個位元組：等於 1 → `sub_4EAFF0(1239)`；信匣 1 → `sub_4D4880(buf, 0x40, "%s %s", …)`；其他 → `sub_7DC800(1240)`。
  - 0x30 → 1243／1244；0x40 → 1241／1242；0x50 → 428。
  - 顏色全部是 `dword_1369D70[24]`、`[14]`。
- **`%s %s` 的順序**（反組譯 VA `0x941A3C`–`0x941A6A`）：先 `push 4EDh`（1261）→ 呼叫 → `push eax`，再 `push 190h`（400）→ 呼叫 → `push eax`。後推的是第一個 `%s`，所以結果是「400 1261」：「血盟 信箱已滿，無法再收信件。」
- **字串表**（PakViewer CLI `pak search`／`pak extract`，只讀，輸出在 `scratch/t988/pak/`）：
  - 搜 `string` 的範圍：`0-客戶端` 全部 19 個 idx（`Sprite.idx`、`Sprite00`–`Sprite15.idx`、`Text.idx`、`Tile.idx`），只有 `Text.idx` 有 6 筆（`string-c/e/h/j/k.tbl`、`string-c.tbl.bak`）。
  - `string-c.tbl`：105,931 B，3484 筆，SHA-256 `4ebbc1a7…43e9`，與 FU6／#746 相同。
  - 各筆內容：400「血盟」、428「您收到鴿子信差給你的信件。」、1239「已將信件送出了。」、1240「對方的信箱已經滿了，所以無法再寄信到對方信箱。」、1241「信件已被儲存。」、1242「無法再儲存信件。」、1243「信件已被刪除了。」、1244「無法刪除信件。」、1261「信箱已滿，無法再收信件。」
  - 全部都有，沒有缺字串。
- **3.81 伺服器**（`C_Mail.java`、`S_Mail.java`，只讀）：只有收件人信箱滿時才送 S186 `0x20`／`0x21`（`new S_Mail(type)`），而且不帶結果位元組。原版讀到的是補位 0，所以一般信是 1240、血盟信是 400＋1261。1239 在 3.81 下不會出現，但對應保留。伺服器也不送 0x50。

## 怎麼驗證

- `dotnet build engine/source/OfflineClient.csproj -c Release -r win-x64 -p:SelfContained=false`：成功，0 警告 0 錯誤。
- `dotnet build engine/_integration/dev/mail-tests/MailChecks.csproj -c Release`：成功（product DLL 放在 `engine/build/client-product-dev/`）。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：pass（4583 檔，0 錯誤）。
- 依本機規定**沒有在本機執行任何測試**。單元測試、MailChecks、DemoChecks 都交給 PR 的 GitHub Actions CI，結果見 PR。
- `Q8FollowupTests.cs` 的 `q8-mail-notice-chat`（舊 6 參數、serial 0、沒有 `Recent`）照新邏輯逐條推過一遍，預期結果不變；這是推演，以 CI 結果為準。

## 未驗證什麼

- **真機再確認**（交給 Codex／dot，對 `smoke-m7.md` SM16）：
  - 刪信、保管、寄到滿箱時，聊天欄印的是上面的原版字串，顏色是 palette[24]；
  - 收清單、讀信時聊天欄不印；
  - `--classic-ui` 窗內顯示原版字串。
- `sub_4D4880` 的 64 位元組緩衝沒有模擬截斷（「400 1261」是 29 個 CP950 位元組，不會超過）。
- 第 2 色 palette[14] 只存成資料，聊天列不畫雙色（同公告板 189）。
- `sub_7DC800` 沒有另外反編譯；照 FU15§2.7 已驗證的「字串編號＋同兩色」處理。
- 0x50（428）只有對應與字串，`MailPackets.Parse` 沒有加 0x50（不在本任務範圍，3.81 伺服器也不送）。

## 產出路徑

- `engine/source/Mail.cs`、`engine/source/MailUi.cs`、`engine/source/NativeUi/MailPanel.cs`
- `engine/source/MailNoticeStringTests.cs`（新）
- `engine/_integration/dev/mail-tests/Program.cs`（4 行斷言）
- `docs/reports/issue-988.md`（本檔）

## 給下一棒的提示

- gap-matrix 1:99 的 K5 子項已做完。下一輪盤點可以改回「已有」，但字串與顏色仍要靠 SM16 真機確認。
- 新的信件結果要印到聊天欄時，在 `WorldSession.ReceiveMail` 設 `mailLine` 並把 `mailNoticeSerial++`；引擎自己的提示一律走 `MailStatus()`，不要寫進 `Notice`。
- 如果之後要支援 0x50（鴿子信差），要在 `MailPackets.Parse` 加子類（格式見 `sub_941000` case 0x50：D、C、`sS`）。字串對應已經有了（`MailNoticeLine.For(0x50, …)` → 428）。
