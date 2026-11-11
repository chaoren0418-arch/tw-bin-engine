# Issue #926 — T409：R10-V 好友窗入口與 S39 buddy 頁（FU13 D3–D5）

## 結論

- **D3**（已完成）：`FriendPanel.cs` 的註解改成「已驗證：TW16 無開窗鍵（FU13§3.1）」，並寫明入口照原版走 `/buddy` 與 HyperAct `buddy`。`BuddyUi.cs` 的過渡開窗鈕留著，註解同步更新（WinForms／`--classic-ui` 入口不動）。
- **D5**（已完成）：`/exclude 名稱` 仍然送 C171。`Buddies.cs` 的註解改成「原版固定傳空字串（已驗證，FU13§3.2）」，拿掉「推測，M37」。
- **D4**（已完成，**有一點和任務描述不同**）：用 3.81 OEP 快照確認 **3.81 收到 S39 `buddy` 會開 `buddy` HTML 頁**。不過頁面變數的填法和 FU13 推測的「#0／#1 人數、#2 名單」不一樣。本引擎照 3.81 實際的變數陣列做，細節見下節。

## D4 的依據（已驗證，靜態）

- 快照：`D:\lineage開發\3-lito開發\lito登入器\舊版分析與驗收\runtime\LiTo.unpacked-oep-current.exe`，SHA-256 `4271d92fb2b9a2fa8d118dde0767f6d8a67a896227bcc24127438fcb5c2ab6e9`（和 FU12 用的是同一份）。複製到 `scratch/t926/lito_oep.exe` 分析，沒有進 git。FU12 留下的 `scratch/t896/` 已經不在了，所以重新複製一份。
- 地址都是 VA；快照的 FO＝VA−`0x400000`。工具是 Capstone 5.0.6 線性反組譯，一次性腳本 `scratch/t926/{s1..s4,dis}.py`，不進 git。

| 位置 | 內容 |
|---|---|
| `sub_4A5220`（S_HYPERTEXT 處理函式） | 用格式 `"dssh"` 讀 objid、htmlid、一個字串（實際上把第一個 H `02 00` 當成 1 字元字串吃掉）、參數數 n（第二個 H） |
| `0x4A5490` | `sub_43A680`：清空 HTML 物件 `dword_647E14` 的變數陣列，把旗標 `+0x181` 歸 0 |
| `0x4A549B`–`0x4A554D` | htmlid 是 `"pledgeM"`、`"pledge"`、`"buddy"`（字串 `0x502D7C`）其中之一 → 旗標 `+0x181`＝1 |
| `0x4A5567`–`0x4A563D` | 逐一讀 n 個字串。旗標為 1，而且（i≠0，或 htmlid＝`"buddy"`）時，先算字串裡**空格的個數**，用 `"%d"`（`0x50307C`）格式化後當成一個變數加進去（`0x4A55D6`–`0x4A561F`），再把字串本身加進去（`0x4A5631`） |
| `sub_43A710` | strdup 之後 push 到 `+0x290`／`+0x294` 的陣列 |
| `0x4A5654` | `vtable+0x54(htmlid, 1)`：開頁 |
| `0x4369A8`／`0x438740` | 頁面裡 `<var src="#n">` 的 `#` 後面接數字，n＜900 時直接取陣列 `[+0x294]+4n` |

所以 3.81 伺服器的 `S_Buddy`（`H2 H2 S 全部 S 在線`，每個名字後面帶一個空格）送來之後，變數陣列是 **[全部空格數, 全部名單, 在線空格數, 在線名單]**。

`buddy-c.html`（0-客戶端 `Text.idx`，SHA-256 `8b7b624f…5b11d08d`）的內容是 `好友名單(全部#0人／在線#1人)：<br><p>#2`，3.81 實際會顯示成 **#0＝全部人數、#1＝全部名單字串、#2＝在線人數**。這個畫面看起來不自然，但是靜態分析的結果就是這樣。本引擎照原版的陣列做，**畫面要真機再確認**。對照一下：`pledgeM-c.html` 的頁面（#0 盟名、#1 在線數、#2 在線名單、#3 成員數、#4 成員名單）和同一段程式碼完全吻合，可以佐證這個解讀沒錯。

## 做了什麼（可改檔案範圍內）

- `engine/source/Buddies.cs`
  - `BuddyListPacket` 加上 `All`／`Online` 兩個原文字串（有預設值，舊的呼叫端不受影響）。`TryBuddy` 把這兩個字串帶出來。
  - 新增 `BuddyPage`：`Html="buddy"`；`Arguments(all,online)` 照 3.81 回傳 `[空格數, all, 空格數, online]`。
  - `ReceiveBuddies`：名單照原本的方式更新；`Ready` 時多呼叫一次 `ShowQueryPage(owner,"buddy",args,args)`。頁面放在伺服器查詢頁，也就是原生 HYPER_TEXT 視窗，所用的面板和 y_who 等查詢結果相同。沒有模板時會用解碼後的參數文字顯示，並附上 `HyperTextFallbackNotice`。NPC 對話不受影響。
  - 註解：D5（`/exclude`）、`/buddy` 入口、HyperAct `buddy`。
- `engine/source/NativeUi/FriendPanel.cs`：D3 註解。
- `engine/source/BuddyUi.cs`：過渡開窗鈕的註解（D3）。
- 新檔 `engine/source/BuddyPageTests.cs`：`[WorldModuleTest("buddy-page")]`，靠自動發現註冊。測試項目：
  - 變數陣列；
  - `TryBuddy` 保留原文；
  - 收到 S39 → 開 `buddy` 頁，模板照陣列填，名單模型照舊，NPC 對話不動，頁上沒有可送的選項；
  - 再收到一次就換新內容；
  - 沒有模板 → 退回顯示參數文字；
  - 世界未就緒 → 不開頁；
  - owner 不是自己 → 照舊拒絕，不開頁。

  測試只用合成封包和合成模板（只保留 `#0/#1/#2` 的位置，文字不是原檔）。

沒有改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

## 怎麼驗證

- `python engine/tools/check-no-game-assets.py`：`pass`（4474 個追蹤檔，0 個錯誤）。
- 資源查詢：用 PakViewer CLI 的 `pak list` 查過以下檔案：
  - `0-客戶端\Text.idx`：有 `buddy-c/e/h/j/k.html`、`pledge-c.html`、`pledgeM-c.html`；
  - `0-客戶端\LiTo\LTtext.idx`：357 筆，沒有 buddy；
  - 參考客戶端 kor25、雷雨的 `Text.idx`：`buddy-c.html` 都是 110 B；
  - 參考客戶端 89天堂 的 `Text.idx`：`buddy-c.html` 是 85 B，內容是 `朋友名單(#1/#0)：#2`。

  用 `pak extract` 解出 `buddy-c.html`、`pledge-c.html`、`pledgeM-c.html` 和 t89 的 `buddy-c.html`，放在 `scratch/t926/pak/`（不進 git），用來核對變數位置。r29 的 `dialogs/templates.json` 有 `buddy` 這個鍵。
- **本機建置沒有通過，原因在環境，和本次改動無關**：`dotnet build source/OfflineClient.csproj` 在 ResolveAssemblyReferences 階段就因為 `NETSDK1130 Microsoft.Web.WebView2.Core.winmd` 失敗，還沒進到 C# 編譯。NuGet 對 WebView2 1.0.4258.31 退回用 net462 資產（NU1701）。我試過換成 vendor 來源、換一個獨立的 `NUGET_PACKAGES`、加 `-p:WebView2UseWinRT=false`，結果都一樣。編譯和單元測試交給 PR 的 CI（ubuntu 交叉建置）。
- 依照本機限制，沒有執行任何編譯出來的程式（`--module-tests`、DemoChecks 都交給 CI）。

## 未驗證／待驗證

- **真機再確認**：在 P24Lab 3.81 打 `/buddy`，看 HTML 頁實際長什麼樣子。照靜態分析，預期是「全部 n 人／在線〈全部名單〉人：〈在線人數〉」。另外要確認 3.81 的 S39 `buddy` 是不是蓋掉同一個 HTML 視窗的內容（本引擎放在伺服器查詢頁，和 NPC 對話共用面板）。
- `buddy` 頁在本引擎走 `ShowQueryPage`，選項不能點；`buddy-c.html` 本來就沒有選項，所以沒有影響。
- `pledge`／`pledgeM` 在 3.81 也會插入計數變數（同一段程式碼）；本引擎目前沒有做，不在本任務範圍。

## 產出路徑

- 程式：`engine/source/Buddies.cs`、`engine/source/BuddyUi.cs`、`engine/source/NativeUi/FriendPanel.cs`、`engine/source/BuddyPageTests.cs`
- 報告：`docs/reports/issue-926.md`
- 分支：`task/926`

## 給下一棒的提示

1. **HyperAct `buddy`／`refreshbuddy` → C4**：原版只送 `c` 56，也就是 C4（FU13§3.1）。動作分派在 `NpcInteraction.cs`（`IsAddBuddy` 旁邊，第 160、181 行），本任務不能改，建議另開 code 任務接上。接好之後，NPC 頁上的 `buddy` 動作就不會再送 C125。
2. **`pledge`／`pledgeM` 的計數變數**：3.81 `sub_4A5220` 對這兩頁從第 1 個字串起，每個字串前面都插入空格數。`pledgeM-c.html` 的 #1、#3 是人數，目前本引擎會錯位，建議另開任務照 `BuddyPage` 的做法處理。
3. `/exclude`（不帶名稱）：原版會切到封鎖頁；引擎目前只提示格式。之後要的話可以改成打開原生 `Exclude_Window`（FU13 D5 的選項）。
4. 本機 Windows 建置的 WebView2 NETSDK1130 問題建議標 `needs:local` 查：看起來是使用者的 NuGet 快取或 SDK 環境造成的，不是這個 PR。
