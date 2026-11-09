# Issue #625 — T264：R2-K 血盟收尾

依據：`docs/reference/gap-matrix.md` 第 11.5 節 R2-K；AN§2.8、WIN2§3、PG§2.3、PBW§2.12、SS2§2／§2.3、FU3§7。分支 `task/625`。

## 做了什麼

| 目標 | 做法 | 檔案 |
|---|---|---|
| 送 C63 階級設定 | `ClanProtocol.EncodeRank(rank,name)` → `C63 C(1) C(rank) S(name)`（3.81 `C_Rank` data 1；伺服器只收 2–10，其他回 2149）。階級名稱取自 `C_Rank.rank` 的原始碼註解（2／7 一般、3 副君主、4／10 聯盟君主、5／8 修習騎士、6／9 守護騎士）。`LoginConnection.SendClanRank` 共用 `SendClan` 的防護（同一連線、同一世界、同一角色、一次一個請求），另外要求角色有血盟；君主權限與成員資格由伺服器判定（785／518／201）。不送其他 data 值（5 會改 HP、9 是地監時間） | `Clan.cs` |
| 送 C18 上傳盟徽 | 讀伺服器 `C_Emblem.java`（SHA-256 `4ea6cd5b…4415`，與 server-protocol.md 一致）：君主時 `readByte()` 取**封包剩下的全部 bytes** 存成盟徽，再以 S118 原樣推回。`ClanEmblemProtocol.EncodeUpload(pixels)` → `C18`＋384 bytes RGB555（TW16 `sub_96E790` 讀的格式；透明→0）。`UploadClanEmblem` 走同一條送出防護 | `ClanEmblem.cs`、`Clan.cs` |
| S118 收回上傳的盟徽 | 伺服器存的是整個 C18 本體，含上傳端的傳輸 padding，所以 384 bytes 後面可能多 7 個 0。原本只接受 384–391，改成 384–400 且 384 之後全為 0（`sub_96E790` 只讀 384）。BMP 解碼抽成 `ClanProtocol.DecodeBmp`，S118 與上傳檔共用，行為不變 | `Clan.cs` |
| 入盟 97 進共用詢問槽 | 不改 `YesNoPrompt.cs`（不在可改清單）：模組照舊先判斷能不能接受（Ready、申請人在場且唯一、未交易…），接受後才呼叫 `OfferYesNo(0,97,[name])`，記下槽裡的 prompt id。輸入列 Y／N 或 YES／NO 走 `YesNoPrompt` 的預設送出，bytes 與 `ClanProtocol.Answer` 相同（`C121 H0 D0 H97 C`）。模組的 `CheckClanState`（每個封包後、血盟窗計時器都會跑）發現槽裡不是這個 id 了，就清掉自己的申請；模組自己讓申請失效（申請人離開、換圖、血盟變更、5 分鐘…）時也把槽清掉 | `Clan.cs` |
| 盟戰 221／222 進共用詢問槽 | 同上（`OfferClanYesNo`、`clanWarSlot`）。**217 不動**：217 是原版 S_ASK switch 的特例（WIN§2.1），維持盟戰窗回答 | `ClanWar.cs` |
| 拿掉專用回覆鈕 | 血盟窗拿掉「接受入盟／拒絕入盟」，收到 97 不再自動開血盟窗（計時器保留，只用來同步狀態）。盟戰窗只在 217 時顯示「接受／拒絕」，收到 221／222 不再自動開窗 | `ClanUi.cs`、`ClanWar.cs` |
| 血盟窗入口 | 加「階級」下拉（2–10）＋「設定階級」、「上傳盟徽…」（選 16×12 未壓縮 BMP，1／4／8／16／24／32 位元，>64 KB 拒絕）。點成員列會填入名稱。狀態列顯示 S250/173 的「顯示盟徽：開／關」。**這些入口是引擎自訂**：原版是 PledgeUI 的 `ChangeRank` 彈出窗（WIN2§3），上傳的送出點沒追 | `ClanUi.cs` |
| PBW-D：S250/173 | `[WorldReceive(250,Subtype=173)]`：讀 `C(type) C(value)`＋（3.81 的）`D(0)`。type 1 → `ClanShowEmblem = value≠0`；type 130 → 訊息 3383（`ServerMessageText`，走畫面中央提示，不動 `LastServerMessage`，通道是推定，與 #560 的 1889 相同）；其他 type 回 false（記為未處理）。`[WorldReset]` 清除，`[WorldSnapshot("clanEmblemStatus")]` | `ClanEmblem.cs` |
| 血盟推薦 UI | 依 Q-3（T268 SS2§2、T275 FU3§7）：TW16 收到 op 133 一律丟棄、C_MATCH_MAKING 送出點全在不會建立的窗內 → **不做推薦 UI**。只更新 `ClanMatching.cs` 開頭註解，資料層不變 | `ClanMatching.cs` |
| 測試 | 新檔 `ClanFollowupTests.cs`，`[WorldModuleTest("clan-followup")]`，自動接 `--module-tests` | `ClanFollowupTests.cs` |

## 怎麼驗證

**已驗證（本機）：**
- 建置：`py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config` → `PASS dev`（暫時 lock，`rebuild-lock.json` 沒有改；依 T271／#646 由合併後重產）。productDll `e92735fc…e560f`；同步 origin/main（`a8ea003f`，含 #623、#672）後重建仍 `PASS dev`，productDll `65c884cd…efd72`。worktree 沒有 `engine/vendor`，所以用 CI 的 nuget 設定（與 #624 相同）。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → `pass`。

**交給 CI（依 2026-11-09 本機規定，本機不執行任何建出來的程式）：**
- `--module-tests` 的新組 `clan-followup`，涵蓋：
  - C63 bytes、範圍 2–10、空白名稱、沒有血盟不送、快照過期不送；重構後 Create／Ban 的 bytes 與錯誤不變。
  - C18 bytes（392 = 1＋384＋padding）、RGB555 換算（紅 0x7C00、綠 0x03E0、藍 0x001F、透明 0）、S118 原樣推回能解、384 後有非 0 拒絕、BMP 檔轉換與錯誤檔拒絕、`UploadClanEmblem` 只送一次。
  - S250/173：註冊、type 1 開／關、TW16 4 byte 與 3.81 8 byte 兩種長度、type 130 → 3383 不動 `LastServerMessage`、未知 type、padding 非 0、重設。
  - 97：進槽、Y／N 送出 bytes 等於舊模組回覆、槽回答後模組清掉申請且不能再從模組回答、模組回答會清槽、申請人離開時兩邊都清、新的 S219 取代、不明申請人不進槽。
  - 221／222：進槽、送出 bytes 等於 `ClanWarProtocol.Answer`、槽回答後盟戰窗清掉、盟戰窗回答會清槽；217 不進槽；沒有血盟不進槽。
- 既有 dev 測試（`clan`、`clan-war`、`marriage`、`yes-no`、`name-display` 的 S118 raw）沒有改；我逐條對過預期，判斷應維持綠燈，待 CI 確認。

## 未驗證什麼

- **沒有真機**：沒有啟動遊戲、伺服器或 DB。上傳後別人看到的盟徽、階級變更結果、3383 的顯示位置都沒看過。
- **C18 本體格式是推定**：選 384 bytes RGB555 是因為 TW16 收 S118 時只讀這個格式；原版上傳的送出點與讀檔（`Emblem%d.dat`？BMP？）沒有追。若 3.81 真機上傳的是 BMP，只需改 `EncodeUpload`，S118 兩種都能解。
- RGB555 的 0 在原版是透明，所以 BMP 裡**純黑**的像素上傳後會變透明；照原版讀法保留，沒有改成別的顏色。
- **共用詢問槽的已知差異**（因為不改 `YesNoPrompt.cs`）：輸入列回答 97／221／222 時走 `YesNoPrompt` 的預設送出，只檢查「槽裡還是同一個詢問、同一個世界」，不另外檢查模組的 `ClanAccess`（例如行動鎖定中）。模組失效時會在下一個封包或 250 ms 計時器內把槽清掉；5 分鐘的本地逾時（引擎自訂，原版沒有）只靠計時器觸發。要完全照 322 的做法（`ModuleOwned`、`YesNoModuleLive`、`AnswerYesNo` 轉給模組），需要改 `YesNoPrompt.cs`。
- 入盟 97 的 5 分鐘本地逾時保留（dev 測試 `ClanChecks` 有測），R2-J 已拿掉組隊與求婚的逾時，是否也拿掉要另外決定。
- S250/173 type 1 只存旗標；`PledgeUI+1220` 是不是「顯示盟徽」勾選框是推定（PBW§2.12）。名字旁的盟徽繪製沒有改成看這個旗標（`ObjectRendering.cs` 不在範圍）；原生血盟窗（`NativeUi/PledgePanel.cs`）也還沒有勾選框。
- SS2§3 建議的血盟窗內 7 列「關注血盟」清單不在本張任務文字內，也要改 `PledgePanel.cs`，沒有做。

## 產出路徑

- 程式：`engine/source/Clan.cs`、`ClanEmblem.cs`、`ClanUi.cs`、`ClanWar.cs`、`ClanMatching.cs`（只改註解）
- 測試：`engine/source/ClanFollowupTests.cs`（新，`[WorldModuleTest("clan-followup")]`）
- 本報告：`docs/reports/issue-625.md`

## 給下一棒的提示

- **YesNoPrompt 擴充點**：若之後允許改 `YesNoPrompt.cs`，把 97／221／222 從 `delegated` 移到 `ModuleOwned`，`YesNoModuleLive` 加 `97=>PendingClanInvitation!=null`、`221 or 222=>PendingClanWarPrompt?.Message==p.Message`，`AnswerYesNo` 轉給 `AnswerClanInvitation`／`AnswerClanWar`；本張的 `OfferClanYesNo`／`RetireClanYesNo` 可以照用。`YesNoFollowupTests.cs:50` 的「still delegated」斷言要一起改。
- **原生血盟窗（PledgePanel）**：要接階級與上傳時，`IUiCommands` 呼叫 `LoginConnection.SendClanRank(view,name,rank)`、`UploadClanEmblem(view,pixels)`；「顯示盟徽」勾選框讀 `WorldSession.ClanShowEmblem`（null＝伺服器還沒送）。3.81 `C_Windows` 切換顯示盟徽的送出格式沒有查。
- gap-matrix 第 1 節 op 161、第 3 節 C_EMBLEM／C_RANK_CONTROL、S250 子碼 173 的狀態，請下一輪盤點更新（本張不改 gap-matrix）。
- 真機驗收（Codex／dot）：盟主上傳 16×12 BMP → 自己與視野內玩家名字旁出現盟徽；`/階級` 等價操作（血盟窗設定階級）→ 對方收到 S250 MSG_RANK_CHANGED；別人申請入盟時輸入列出現詢問，Y＋Enter 後加入成功。
