# Issue #549 — T231：C26 聊天頻道開關（protocol-gaps 任務 C）

依據：`docs/reference/protocol-gaps.md` §6（C_ONOFF 184 列）與 §8 任務 C；3.81 欄位見 `docs/reference/server-protocol.md`（C_LoginToServerOK `C(type) C(button)`）與 `server-rules.md` R06。

## 做了什麼

- **新檔 `engine/source/ChatChannelToggle.cs`**
  - `ChatChannelKind`（全體 0、密語 2、買賣 6）、`ChatChannelSettings`（三個開關；保存位元 bit0 全體、bit1 密語、bit2 買賣）。
  - `ChatChannelPackets`：
    - `Toggle(kind,on)` → C26 `[26, type, 0|1]`（補到 8 byte）。
    - `Merged(settings)` → C26 `[26, 255, 0x5A | 全體?1 | 密語?4]`，也就是 90／91／94／95。
    - `Sync(settings)` → 先送 255＋合併位元，再送 6（買賣），順序同原版 `sub_99B430`。原版另外送的 9、10，3.81 伺服器不處理，所以不送。
    - `Apply(settings,type,button)` 依 3.81 伺服器語義算出設定變化（255 只認 90／91／94／95 與 +0x20 的 122／123／126／127；0／2／6 只認 button 0／1），只給離線測試核對封包用。
  - `ChatChannelCommands`：聊天列 `/on 頻道`、`/off 頻道`（頻道：全體／密語／買賣，另收交易、world、whisper、trade 等別名），回報文字用「開」「關」（字串 401／402）。
  - `WorldSession`（partial，M7-A 擴充點）：`[WorldReset] ResetChatChannels`、`[WorldSnapshot("chatChannels")]`（world／whisper／trade／stored）；`LatestCharacterConfig`（本場送出成功的設定優先，否則用伺服器登入時給的）、`PersistedChatChannels`、`ChatChannels`。身分（世代、角色）改變就失效，和 `ServerCharacterConfig` 的作法一樣。
  - `LoginConnection`（partial）：`SetChatChannel(stamp,kind,on)` 先送 C26，成功後把設定寫進角色設定（C244／TWCC v2）；`SyncChatChannels(stamp)` 登入後還原已保存的設定；`SendChatChannelCommand(text,stamp)` 是給聊天列用的入口；回報文字放在 `ChatChannelNotice`。
- **`engine/source/CharacterConfig.cs`**（只加頻道欄位與必要的保存接點，快捷列欄位與 v1 格式不變）
  - TWCC v2 格式＝v1（8 格）＋1 byte 頻道位元。`Channels` 屬性：v1 為 null。`FromSlots(bindings, channels=null)`：沒給頻道時輸出的 byte 和原本的 v1 完全相同；`WithChannels(settings)` 保留快捷列內容，舊版格式不能用（會丟例外，不覆寫）。
  - `SaveCharacterConfig` 送出成功後記下這份設定（`World.RememberCharacterConfig`），這樣快捷列存檔和頻道存檔不會互相蓋掉對方的內容。
  - `CharacterConfigHud`：存快捷列時一起帶上目前的頻道設定；收到伺服器給的角色設定、且裡面有頻道設定時，呼叫 `SyncChatChannels` 還原（3.81 角色每次登入頻道都是全開；從沒設定過的角色不送）。
- **新檔 `engine/source/ChatChannelToggleTests.cs`**：`[WorldModuleTest("chat-channel-toggle")]`，92 項，由 `--module-tests` 執行。
- `engine/_integration/dev/rebuild-lock.json`：用 `rebuild.py --update-lock` 重新產生。

## 怎麼驗證（本機 Windows，2026-11-08）

- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --update-lock` → `dev-inputs-verified`，241 個來源／250 個鎖定檔。
- `rebuild.py --build` → PASS；productDll `34eeb65632629721ae10e11b14af6a447be0732c1b3832e3e951d1bcf40e8b69`。
- `P11.ClientEngine.exe --module-tests` → 結束碼 0，5 組、309 項全部通過（agit 44、castle 62、**chat-channel-toggle 92**、recovery-death 72、world-extensions 39）。
- DemoChecks：`character-config` 455 項、`hotbar` 569 項、`map-continuity` 33019 項，全部 PASS（現有的角色設定與快捷列測試沒有被這次改動影響）。
- PlayerChatChecks：138 項 PASS。
- `engine/tools/check-no-game-assets.py` → pass，0 錯誤。
- CI：PR #576 第一版 dotnet／map-continuity／npc-offline 全綠。
- **同步 origin/main（2026-11-08，含 #525 等）**：`rebuild-lock.json` 衝突改用 main 版本後 `--update-lock` 重產（259 個來源／268 個鎖定檔）；`--build` PASS，productDll `0f3e6bdcfa42f43b0a59711f0991d2d3b1661869fcbf0c19562b49dc6a376ce1`；`--module-tests` status pass、7 組 439 項、0 失敗；`check-no-game-assets.py` pass、0 錯誤。

## 未驗證什麼（推定／待驗證）

- **合併位元的意義是推定**：bit0＝全體、bit2＝密語，其餘位元固定送 0x5A。原版 `sub_99B430` 怎麼組出 0x20 等其他位元沒有反組譯核對；3.81 伺服器對 0x20 位元的兩種值（90–95、122–127）處理方式相同，所以不影響 3.81。
- **`/on`、`/off` 指令語法是推定**：protocol-gaps 只記到有 on／off 指令和字串 401／402，沒有記確切格式。
- **聊天列還沒接上**：`PlayerChat.cs` 不在本任務可改範圍，`/on 全體` 目前會被當成一般聊天送出。需要另開任務：在 `LoginConnection.SendPlayerChat` 開頭判斷 `ChatChannelCommands.IsCommand(text)`，成立就改呼叫 `SendChatChannelCommand`。選項視窗（原版 C_ONOFF 的選項 UI）也還沒做。
- 登入還原（`SyncChatChannels`）掛在 `CharacterConfigHud.Update`（HUD 每次更新都會呼叫），沒有實機確認送出時機與伺服器效果；伺服器不會回包，只能從「關掉全體後是否還收得到 S243 全體聊天」去觀察。
- 沒有啟動遊戲、伺服器或 DB。

## 產出路徑

- `engine/source/ChatChannelToggle.cs`、`engine/source/ChatChannelToggleTests.cs`、`engine/source/CharacterConfig.cs`、`engine/_integration/dev/rebuild-lock.json`、本報告。
- 本機測試輸出（不入 git）：`scratch/issue-549/*.json`。

## 給下一棒的提示

- 接聊天列：在 `PlayerChat.cs` 的 `SendPlayerChat` 裡，`/loc` 判斷旁邊加上 `if(ChatChannelCommands.IsCommand(text))return await SendChatChannelCommand(text,new(session,world,actor));`，並把 `ChatChannelNotice` 顯示到聊天提示列；`PlayerChatProtocol.InputHelp` 可以加上 `ChatChannelCommands.Help`。
- 選項視窗：讀寫 `World.ChatChannels`，切換呼叫 `SetChatChannel`。
- 實機驗收（dot／P24Lab）：`/off 全體` 之後，其他角色的 `&` 全體聊天應該收不到；重新登入後應該維持關閉（會送 C26 255/94 與 6/x）。
- worktree 建置需要 `engine/vendor` junction（`cmd /c mklink /J engine\vendor <主專案>\engine\vendor`），本輪已建立，不入 git。
