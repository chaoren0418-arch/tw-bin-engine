# Issue #708 T296：R4-E 血盟窗收尾

依據：`docs/reference/gap-matrix.md` 第 13.5 節 R4-E；WIN2§3（`windows-2.md`）；SS2§3（`send-sites-2.md`）；PBW§2.10／2.12（`packetbox-windows.md`）；`server-protocol.md` 的 C_Windows、C_PledgeContent。

## 做了什麼

| 檔案 | 內容 |
|---|---|
| `engine/source/Clan.cs` | ① S200（TW16 op 78）改照 SS2§3 讀 `C(type) C(result)`：3.81 的 `H(type)` 就是 `C(type) C(0)`，所以原本的關注清單照常；3.81 結尾的 `H(0)` 改成「只允許零位元組（< 10 bytes）」，TW16 沒有結尾的格式也收。② type 2 只有 result＝0 才換清單（`sub_C146E0`）。③ type 0：result 131／129 → 聊天區訊息 3349，138 → 3324（`sub_C14680`）；其他 result 不顯示。`ClanAttention` 加 `Result`（預設 0，舊呼叫端不用改）。 |
| `engine/source/ClanEmblem.cs` | ① `ClanEmblemStatusPackets.EncodeShow`：C254 `C(0x2E) C(0／1)`（3.81 `C_Windows` case 0x2e，只收盟主 rank 4／10，存檔後對線上成員送 S250/173）。② `LoginConnection.SetClanShowEmblem`：走既有 `SendClanPacket` 閘門（同一時間一個請求、同 session／世界／角色、要有血盟）。③ `LoginConnection.OwnClanEmblem()`：血盟窗用的自己盟徽（缺圖時照名字盟徽的方式查詢）。④ 快照 `clanEmblemStatus.ui` 說明更新。 |
| `engine/source/NativeUi/PledgePanel.cs` | ① 新介面 `IPledgeWindowSource`（公告、階級、顯示盟徽、上傳、旗標、盟徽），面板不碰 `LoginConnection`。② **公告寫入**：公告窗 `Noticebutton_Control` 的「公告」鈕（Writenotice）開始編輯，草稿＝目前公告；打字（`IUiTextInput.Char`，上限 160、不收控制字元與代理字元）、Backspace、Esc 放棄、Enter 或「存檔」鈕（Savenotice）送 C78 15；送出後等伺服器回送 168 才改畫面。③ **改階級**：點成員列的 `Rank_Label`（XML 事件 `OpenPopup`）開 `ChangeRank_PopupWindow`，點 Rank0–3 送 C63（8／7／9／3）；點別處或 Esc 只關彈出窗。④ `Pledge_Emblem` 畫自己的盟徽。⑤ **側欄**（引擎自訂，緊貼視窗右緣 112×196）：關注血盟 7 列（接 `AttentionPage`／`ScrollAttention`，上下箭頭、滾輪）、「顯示盟徽」勾選框（反映 `ClanShowEmblem`，點了送 C254 0x2E 相反值）、「上傳盟徽…」鈕、最近一次動作的提示。⑥ `Bounds`＝視窗∪側欄；新增 `WindowBounds`（既有測試兩處改用它）。選用節點缺少時沒有該入口，不補座標。 |
| `engine/source/ClanUi.cs` | ① **原生 UI 關閉時的入口**：WinForms 血盟窗加「顯示盟徽（盟主）」勾選框（只反映伺服器旗標，點了送 C254）、公告輸入框＋「送出公告」（C78 15，收到 168 時帶入目前公告）。階級與上傳原本就有；關注清單與 3349／3324 在原有文字欄與聊天區。② 上傳盟徽流程抽成 `PickAndUploadClanEmblem`，WinForms 與原生窗共用。③ `AttachNativePledge`：原生 UI 開著時，每 250 ms tick 把 `WorldPledgeSource` 接到 `PledgePanel.Source`；上傳對話框用 `BeginInvoke` 開，不在原生滑鼠分派中開模態窗。④ `WorldPledgeSource`：轉給既有 `SendClanContent`／`SendClanRank`／`SetClanShowEmblem`。 |
| `engine/source/PledgeWindowFollowupTests.cs`（新） | `[WorldModuleTest("pledge-window-followup")]`：S200 各 type／result 解碼、錯誤結尾、type 0 聊天訊息、type 2 result≠0 不換清單；C254 bytes、沒有血盟不送；`WorldPledgeSource` 三種封包 bytes、旗標、盟徽；面板的側欄位置、關注清單捲動與繪製、勾選框（沒有 Source 不送、旗標 null／true 的切換、勾選記號像素）、上傳、盟徽像素、改階級彈出窗（位置、送出、點別處／Esc 關閉、空列）、公告編輯（開始、Backspace、打字、控制字元、代理字元、160 字上限、Enter 送出、被拒仍在編輯、Esc、換窗放棄、沒有 Source、關窗）、選用節點缺少與重複節點。全部是合成資料。 |

沒有改 `UiClientBridge.cs`、`Program.cs`、`DemoChecks.csproj`、各註冊表與 `rebuild-lock.json`。

## 已驗證／推測／待驗證
- **已驗證（文件與原始檔唯讀核對）**：
  - S200 欄位與 3349／3324 條件（SS2§3）。
  - C254 0x2E 的欄位與伺服器行為（伺服器 `C_Windows.java` case 0x2e，唯讀）。
  - C78 15 已有的編碼（`ClanMatching.cs`，#515）。
  - 本機 `PledgeUI.xml` 的節點：從 `D:\lineage開發\0-客戶端` 的 Tile.pak 用 `ui_xml_decrypt.py` 唯讀解到 scratch，看完已刪除，沒有進 repo。
    - 公告窗有 `Noticebutton_Control` (183,24) 內的 `Writenotice_Button`／`Savenotice_Button`（同位置）。
    - 兩個窗的 `Pledgename_Control` 都有 `Pledge_Emblem`。
    - 有 `ChangeRank_PopupWindow` 的 Rank0–3 鈕。
    - **沒有**關注清單與「顯示盟徽」節點。
  - Rank0–3 鈕的圖：用 `ui_img_render.py` 看過，韓文依序是 수련／일반／수호／부군주（修習／一般／守護／副君主）；圖檔沒有進 repo。
- **推測（程式內已標明）**：
  - Rank0–3 → C63 階級 8／7／9／3。依據是 3.81 `C_Rank` 的註解；TW16 `ChangeRank` 的索引對應沒查。
  - 彈出窗開在階級欄正下方。
  - 公告編輯中 Enter＝存檔。
  - 編輯字色。
  - 側欄的版面全部是引擎自訂。原版讀的是 `RePledgeUI.xml`，本機缺；其他客戶端的版本放在 `PledgeOption_Window`，本機缺 surf。
  - 關注清單每列的圖示槽留白：3.81 只送名稱。
  - 勾選框送 C254 0x2E 是依伺服器端推定；TW16 的送出點沒追。
- **待驗證**：WIN2§3 記載，TW16 建構子只註冊 `ChangeRank`、`ModifyMemo`、`OpenPopup`，沒有註冊 `WriteNotice`／`SaveNotice`（原版讀的是 RePledgeUI.xml）。所以「公告／存檔」鈕的行為是照本機 XML 的事件名由引擎實作，原版是否同樣可用沒有查證。

## 沒做（照任務規定）
- **C129 `C_PLEDGE_WATCH` 送出**：Q4-3 還沒查，只維持 S200 收包。
- 寫成員備註（`ModifyMemo` → C78 16）、排序（`SortUserInfo`）：不在本組範圍。

## 怎麼驗證
- `py -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile .github/nuget.ci.config`：PASS。
  - worktree 沒有離線 vendor，所以用 CI 的 NuGet 設定。
  - 用的是暫時 lock，沒有寫入 `rebuild-lock.json`。
  - 沒有新的編譯警告。
- `py -B -X utf8 engine/tools/check-no-game-assets.py`：pass（errors 0）。
- 依本機限制，**沒有在本機執行任何測試或程式**。`--module-tests` 交給 PR 的 CI 跑，包含新的 `pledge-window-followup` 和既有的 `pledge-panel`、`clan-followup`、`native-window-wiring`。

## 沒驗證什麼
- 本機沒跑離線測試，結果以 CI 為準。
- 真機都還沒驗：
  - 原生 UI 開著：171 開窗 → 側欄關注清單與勾選框；公告窗「公告」→ 打字 → 存檔 → 伺服器回 168；點階級欄 → 選階級 → 伺服器回應；上傳盟徽對話框。
  - 原生 UI 關閉：WinForms 血盟窗的「送出公告」與「顯示盟徽」。
  - 非盟主操作時伺服器的拒絕訊息。
- 編輯公告時，可列印鍵會照 WM_CHAR 進輸入框；這些鍵的 KeyDown 仍會傳給其他面板與遊戲（和聊天輸入列相同的做法）。有沒有誤觸快捷鍵要在真機確認。

## 產出路徑
- 上表 5 個程式檔與本報告 `docs/reports/issue-708.md`；分支 `task/708`。

## 給下一棒的提示
- **真機（Codex／dot，P24Lab 測試角色，盟主）**：
  1. 加 `--native-ui`，`/血盟`（或 C68）讓伺服器送 171 開窗。
  2. 側欄勾「顯示盟徽」→ 應收到 S250/173，勾選跟著變。
  3. 切到公告窗 →「公告」→ 輸入 →「存檔」→ 公告欄更新。
  4. 點成員的階級欄 → 選「守護」→ 對方收到階級變更。
  5. 「上傳盟徽…」選 16×12 BMP。
  6. 不加 `--native-ui` 時，在 WinForms 血盟窗重複公告與顯示盟徽。
- **resource 任務**：
  - IDA 查 TW16 `ChangeRank` 事件（`sub_B2A0C0` 註冊的處理函式）按鈕索引 → 階級值，查到只改 `PledgePanel.PopupRanks`。
  - 查 `PledgeUI+1220` 勾選框的送出點，確認 C254 0x2E。
- **Q4-3** 查到 C129 的按鈕與欄位後，在 `IPledgeWindowSource` 加一個動作，側欄加入口即可。
- gap-matrix 13.6 丙／丁裡「原生血盟窗沒有階級、上傳盟徽、顯示盟徽勾選框」「關注清單只有資料不畫」「公告不能寫、不送」可以標為已處理（推測部分除外）。
