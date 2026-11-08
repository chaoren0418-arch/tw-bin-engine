# Issue #515 — T209：M7-H 血盟推薦（code，第 1 次嘗試）

分支：`task/515`。日期：2026-11-08。

## 做了什麼
- 新檔 `engine/source/ClanMatching.cs`：透過 M7-A 擴充點（`[WorldReceive(0)]`／`[WorldReset]`／`[WorldSnapshot("clanMatching")]`）掛上。**沒有改** `WorldSession.cs`、`Program.cs`、`LoginConnection.cs`、`Clan.cs`、`ClanUi.cs` 或其他保留檔。
  - 接收 **S0 `S_PacketBoxPledgeRecommend`**（格式照 SP；子型別在 `plain[1]`）：
    - type 0／1：登錄、撤下推薦成功（`C(type) C(0)`）。
    - type 2：推薦血盟清單（每列：血盟 id、盟名、盟主、`loginLevel`、類型、有無盟屋、有無城、固定 0、說明、盟徽 id）。
    - type 3：自己的申請清單（每列先 `D(clanId) C(0)`，再接 type 2 的欄位；兩個 clanId 不一致時視為壞封包）。
    - type 4：自己血盟的推薦內容。`0x82` 表示沒有登錄；否則讀類型、說明，以及申請者（objId、是否在線、名稱、職業、正義值、等級）。
    - type 5／6：操作結果 `C(actionType) D(recordId) C(acceptType)`，actionType 0＝成功、131＝失敗。申請成功後，從本機推薦清單移除該血盟；接受或移除成功後，從申請者清單移除該人。
    - 其他 type 回 false，計入 `unhandled`。
  - 送出：
    - **C76** `ClanMatchingProtocol.Register/Unregister/ListRecommended/ListApplied/Detail/Apply/Answer`（data 0–6）。
    - **C78** `Announce`（子碼 15，血盟公告：≤ 160 字、不含代理字元，照原服規則）、`Notes`（子碼 16，成員備註）。C78 的子碼 13／14 是火神精煉，不屬本模組，沒做。
    - 送出入口：`LoginConnection.SendClanMatching(view, request)`、`SendClanContent(view, kind, text)`、`ClanMatchingSnapshot()`。送出前會檢查連線世代、角色是否還活著、是否傳送中，以及本機看得到的資格：申請加入要沒有血盟；盟主操作、公告、備註要有血盟。是不是盟主由原服判斷（原服不合就直接丟棄）。
  - 快照 `extensions.clanMatching`：revision、notice、recommended、applied、detail、lastResult。
  - 聊天區會加 `[血盟推薦] …` 文字通知（收到清單、結果、送出）。原版沒有這些文字，是暫代。
- 新檔 `engine/source/ClanMatchingTests.cs`：`[WorldModuleTest("clan-matching")]`，共 62 項檢查，由既有的 `--module-tests` 入口自動執行（CI 的 "World module extension tests" 步驟），不用另外接線。
  - 涵蓋：註冊、C76 七種與 C78 兩種的位元組（含 CP950 中文）、輸入檢查（160 字、代理字元、控制字元、id 0、answer 值）、S0 type 0–6 解析與狀態變化、截斷或不一致的封包丟 `ProtocolException`、未知 type 計入 unhandled、快照、Reset。
- `engine/_integration/dev/rebuild-lock.json`：用 `rebuild.py --update-lock` 重新產生（238 個來源檔），沒有手動改 hash。

## 怎麼驗證（本機，2026-11-08）
- `rebuild.py --update-lock` → `dev-inputs-verified`。`engine/build-product.ps1` → `PASS dev`，product DLL SHA-256 `298b89da72a5b02b8bb3301c3c4501c63c1d6dda9d55a189e03ce6bcbd7a4ede`。另外單獨建置 `OfflineClient.csproj`，新檔沒有任何警告。
- `P11.ClientEngine.exe --module-tests` → `pass`，共 3 組、163 項：castle 62、clan-matching 62、world-extensions 39（其中「default registry has no invalid registrations」也通過）。
- 其他 CI 離線測試全部退出碼 0：
  - DemoChecks：map-continuity、combat-admission、item-status、native-ui-resources、afflictions、resurrection、polymorph-cadence、buddies、item-transfer、doors、pickup、hypertext。
  - PlayerChatChecks、MailChecks、WeaponRepairChecks、CombatSfxChecks（selection）、SoundRulesChecks、SoundPacketChecks。
- `check-no-game-assets.py` → `pass`（3747 個追蹤檔，0 錯誤）。
- 協定依據：SP 的 S0、C76、C78 段落。另外唯讀查看了伺服器原始碼 `S_PacketBoxPledgeRecommend.java`、`C_PledgeRecommendation.java`、`C_PledgeContent.java`，三個檔案的 SHA-256 都和 SP 記錄的一致，用來確認各 type 的語義與資格規則。沒有複製原始碼。
- **同步 main 後重驗**（merge origin/main，含 #512 Agit 模組；無衝突）：`--update-lock` 重產（240 個來源檔）→ `PASS dev`，product DLL `a2b3e351fd79bb08a3facfe8d30a7eddeeeebe35f7c6eefcaf91c7f6f52a32a7`。`--module-tests` → `pass`，共 4 組、207 項：agit 44、castle 62、clan-matching 62、world-extensions 39。`check-no-game-assets.py` → `pass`（3756 個追蹤檔）。
- **第二次同步 main**（含 #531 Recovery 模組；`rebuild-lock.json` 衝突，取 main 版後以 `--update-lock` 重產，241 個來源檔）→ `PASS dev`，product DLL `5c8181812c7cbc5b52bb195388a688adc01e35af2824a6f72f00be6184303352`。`--module-tests` → `pass`，共 5 組、279 項（含 clan-matching 62、recovery-death）。`check-no-game-assets.py` → `pass`（3758 個追蹤檔）。
- **第三次同步 main**（含 #446、#519、#523、#548、#551；`rebuild-lock.json` 衝突，取 main 版後以 `--update-lock` 重產，250 個來源檔）→ `PASS dev`，product DLL `fbe2c756ef55385bda6108b71fa8e6d44c493572e29831f2a2fb04647244beaf`。`--module-tests`（照 CI 指令）→ `pass`，共 7 組、409 項（agit 44、castle 62、clan-matching 62、packetbox-status 86、recovery-death 72、server-features 44 等）。`check-no-game-assets.py` → `pass`（3777 個追蹤檔）。
- **第四次同步 main**（含 #463 AbilityStatus／NativeUi、#510；`rebuild-lock.json` 衝突，取 main 版後以 `--update-lock` 重產，261 個來源檔）→ `PASS dev`，product DLL `d74f36c692064af447733b53da637a634aa907b106145678ef4da87107753964`。`--module-tests`（照 CI 指令）→ `pass`，共 8 組、447 項（ability-status 38、agit 44、castle 62、clan-matching 62、packetbox-status 86、recovery-death 72、server-features 44、world-extensions 39）。`check-no-game-assets.py` → `pass`（3793 個追蹤檔）。
- 建置前置：worktree 沒有離線的 `engine/vendor`，所以建了一個 junction 指向主專案的 `engine/vendor`（唯讀使用，已被 gitignore，沒有 commit）。
- CI 結果以 PR 為準。

## 沒驗證什麼
- **沒有做真機驗收**：沒有啟動遊戲、伺服器或 DB，也沒有在原服上實際收發過封包。
- **畫面沒做**：TW16 的 `MatchMakingUI` 不建立物件（gap-matrix §4.1）。op 133 處理函式 `sub_B243A0` 實際怎麼顯示還沒查證，所以只做協定與資料。目前沒有任何 UI 會呼叫 `SendClanMatching`／`SendClanContent`。
- **TW16 op 133 與 3.81 S0 的對應是推定**（gap-matrix §0）。TW16 是否真的照這些欄位讀，要先反編譯 `sub_B243A0`。
- **欄位意義待驗證**：
  - type 2／3 的 `loginLevel`：只存原值，沒有解讀。
  - 推薦類型 `typeId` 的名稱：只存數字。
  - C76 6 的 answer 2 與 3：原服處理完全相同，差別待查。
  - type 2／3 標頭與每列的固定 0：讀了但沒有使用。
- **輸入上限**：C76 0 的推薦說明、C78 16 的成員備註，原服都沒有長度檢查，原版輸入框的上限待查。目前暫用 160 字（比照公告）；這是推測。
- **職業名稱**（`ClassName`，0–6）依 3.81 的 `pc.getType()` 慣例推定，沒有接到 UI。
- C78 的回覆（S250 169 成員備註、公告解除）由 `Clan.cs`／PG 的範圍處理，本模組不接。

## 產出路徑
- `engine/source/ClanMatching.cs`、`engine/source/ClanMatchingTests.cs`
- `engine/_integration/dev/rebuild-lock.json`
- `docs/reports/issue-515.md`（本檔）

## 給下一棒的提示
- 要做畫面前，先開查證任務：反編譯 `sub_B243A0`（VA `0xB243A0`／FO `0x7237A0`），確認 TW16 收到 op 133 時讀哪些欄位、會不會開任何視窗或只寫資料表，以及送出 `C_MATCH_MAKING(221)` 的呼叫點和子碼。查完再決定要不要做 UI；gap-matrix 不建議另做視窗。
- UI 接法：用 `LoginConnection.ClanMatchingSnapshot()` 取快照，再用 `ClanMatchingProtocol.*` 組請求，交給 `SendClanMatching` 送出。公告和備註用 `SendClanContent(view, 15 或 16, text)`。
- 聊天區的 `[血盟推薦]` 文字是暫代；接上原版畫面後應該拿掉。
- 本機建置要讓 PATH 裡有 `python`（`py -3` 找到的那個），而且 worktree 要有 `engine/vendor` junction。
