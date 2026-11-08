# Issue #561 T239：血盟成員窗（W2-H）

階段：code（第 1 次嘗試）。分支 `task/561`。依據：`docs/reference/windows-2.md` 第 3、9 節（T226／#533），`docs/reference/server-protocol.md` S_Pledge／S_PacketBox，介面契約 `docs/design/native-ui.md`。

## 做了什麼

新檔 **`engine/source/NativeUi/PledgePanel.cs`**（唯一的程式變更；另依規定重產 `engine/_integration/dev/rebuild-lock.json`）：

| 部分 | 內容 |
|---|---|
| `PledgeProtocol` | S250/169 解碼：`C(250) C(169) S(姓名)`＋**62 byte** Big5 備註（零填滿，解到第一個 0）。長度以 3.81 `S_Pledge(String,String)` 為準（伺服器原始碼 `new byte[62]`，唯讀核對，未複製）。另有 169／171 的合成編碼（只給測試用） |
| `WorldSession` 擴充（M7-A） | `[WorldReceive(250,Subtype=169)]` 消化型：更新該成員備註，不開窗。`[WorldReceive(250,Subtype=171,Observe=true)]` 觀察型：171 已由主分派交給 `Clan.cs`，這裡只觀察、不搶封包；記錄在線名單、當下盟名，`OpenRevision`＋1（開窗點，對應 TW16 `sub_B2E3D0` → vtable `+68(1)`）。壞的 171 不開窗，交給主分派照原樣報錯。`[WorldReset]` 清空；`[WorldSnapshot("pledgeWindow")]`。換世界（`WorldEpoch`）或自己的盟名改變時清空 |
| `PledgeWindowState`／`IUiPledgeState`／`UiPledgeGameState` | 唯讀快照與狀態包裝；`UiPledgeGameState.Of()` 可穿過 `UiHpMpGameState`／`UiMacroGameState` 找資料 |
| `PledgePanel`（`IUiMovablePanel`，`UiLayer.Window`，Id `PledgeUI`） | 執行期讀 `PledgeUI.xml` 的 `Nonoticeuserinfo_Window`（預設）與 `Noticeuserinfo_Window`。畫底圖／框、關閉鈕、盟名、公告條與箭頭、排序鈕底圖、成員列（姓名置中、備註靠左）、捲軸。每收到一次新的 171 就開窗並捲回頂端；關閉鈕或 Esc 關窗；公告條／箭頭切換兩個視窗；上下鈕與滾輪捲動；頂端拖曳帶可拖曳；窗內點擊不傳給世界 |
| 離線測試 `[WorldModuleTest("pledge-panel")]` | 同檔內，82 項：註冊、169／171 位元組、解碼邊界、會話行為、版面座標、捲動、繪製裁切、切換、拖曳、關閉與重開、包裝穿透、版面錯誤 |

**不處理**：167、170（3.81 把子碼寫成第一個 byte，不是正常 S250 包）、`PledgeFocusUI`。

## 怎麼驗證（已驗證）

- 同步 origin/main（#468 聊天窗）：只有 `rebuild-lock.json` 衝突，改用 main 版本後 `--update-lock` 重產（288 個來源檔／297 個鎖定檔）→ `rebuild.py --build` → `PASS dev`，productDll `53f69815…9969`。
- `P11.ClientEngine.exe --module-tests`：19 組、992 項全過，結束碼 0（`pledge-panel` 82 項，其餘 18 組既有測試照舊通過）。
- `DemoChecks`：`native-ui-canvas` 429、`native-ui-resources` 539、`native-ui-macro` 403、`native-ui-chat-window` 446、`native-ui-hpmp` 397、`native-ui-left-status` 370，全過。
- 本機建置需要離線 NuGet `engine/vendor`。worktree 裡沒有，所以臨時建了指向主專案 `engine/vendor` 的 junction（git 忽略），用完已用 `rmdir`（不遞迴）拆掉。
- `check-no-game-assets.py`：pass，errors 0。沒有任何 XML、`.img`、PNG 進 git；測試用的 XML 與圖都是程式合成。
- 版面數值由合成 XML 驗證，和本機 `PledgeUI.xml`（只解到 `scratch/`）的值一致：底 4450／4512、框 4451／4487、關閉 (260, 13)、盟名標籤 (19+17, 25−2) 169×20、公告條 (16, 41)、箭頭 (16+200, 41)、排序列 y=61／181、清單 (21, 82)／(21, 202)、欄位 x＝0／20／95／131（寬 19／74／35／97，Gap 1）、捲軸 (255, 67)，上鈕 y+0、拉桿 y+14、下鈕 y+277、ScrollStart 14、ScrollHeight 262。

## 推測（集中在 `PledgePanel` 頂端與註解，查證後只改那裡）

- 以 `PledgeUI.xml` 當 3.81 版面：TW16 實際讀 `RePledgeUI.xml`（本機缺）；3.81 `LiTo.bin` 有殼沒查。
- `BaseSurf="4450;1"` 只取分號前的數字；分號後的意義未查。
- 視窗與 frame 都畫在 (0, 0)；開窗位置用 XML 的 (0, 0)（原版是否置中或記憶位置未查）。
- `Moveable="20"` 當作頂端 20 px 拖曳帶。
- 清單列距＝列高 15（垂直清單沒寫 Gap）。實際 `Userinfo_List` 高 339／20 列、222／12 列，與 15 px 列距不完全吻合。
- 捲軸拉桿在 `ScrollStart` 起、`ScrollHeight` 內依比例移動。
- 文字白色、高 12 px、`VAlign=middle` 依此置中；Esc 等於 `KeyboardEvent="CloseUI"`。
- 每次 171 都捲回頂端。

## 未驗證／未做

- **還沒接上產品畫面**：本任務只能改 `PledgePanel.cs`，所以沒有在 `UiPanelRegistry.cs` 加註冊行，也沒有在 `UiClientBridge.cs` 的狀態包裝鏈加 `UiPledgeGameState`。目前產品開 `nativeUi` 也看不到這個窗；接線方式見下面「給下一棒的提示」。
- 沒有用真實 `Tile.pak`／`Sprite*.pak` 渲染 PNG（契約 §5.2 第 2 點是選用檢查），所以真實 surf 的尺寸與目視比對都還沒做。
- 職業圖（`Class_Image` 4546）、階級、公告內容、創立日、聯盟欄都空白，因為 3.81 送不到（167／170 格式壞掉）；不推算。
- 改階級彈出窗（`ChangeRank_PopupWindow`）、寫備註（`ModifyMemo` → C_PledgeContent 16）、寫／存公告（15）、排序（`SortUserInfo`）、名字 tooltip：要送封包，或語義待驗證，都沒做；`IUiCommands` 目前也沒有血盟動作。
- 盟徽（`Pledge_Emblem`）沒畫。
- 沒有啟動遊戲、伺服器或 DB；CI 結果以 PR 頁面為準。

## 產出路徑

- `engine/source/NativeUi/PledgePanel.cs`（新檔）
- `engine/_integration/dev/rebuild-lock.json`（`--update-lock` 重產）
- `docs/reports/issue-561.md`（本檔）

## 給下一棒的提示

1. **接線（建議開一張小 code 任務，可改 `UiPanelRegistry.cs` 與 `UiClientBridge.cs`）**：
   - 註冊：`new(PledgePanel.PanelId,PledgePanel.LayoutName,_=>new PledgePanel()),`
   - 狀態：在 `NativeUiGameState()` 最外層包 `new UiPledgeGameState(<現有鏈>,inWorld?login.World.PledgeWindow:null)`。`UiPledgeGameState.Of()` 已經會穿過 HpMp／巨集包裝，放最外層最安全。`PledgeWindow` 屬於 `WorldSession`，讀法和 bridge 目前讀 `w.Status`、`LeftStatusState.From(w)` 相同；執行緒安全性沒有另外查證。
   - 註冊後 `UiRoot.Build` 會在啟動時讀真實 `PledgeUI.xml`；版面缺節點會讓整個原生 UI 退回舊 HUD，所以接線時要順便做一次本機真實資源載入與渲染（PNG 放 `scratch/`）。
2. 寫備註、改階級要先在 `IUiCommands` 加血盟動作，並轉呼叫既有的 `LoginConnection` 送包流程（C_PledgeContent 16、C_Rank）。
3. 列距、`;1` 的意義、拖曳帶、開窗位置，等 V4／V5 或 3.81 實機截圖確認後，只改 `PledgePanel` 頂端的常數或 `LoadWindow` 中標「推測」的那幾行。
