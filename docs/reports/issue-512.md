# Issue #512 T206：M7-D 盟屋清單與地圖（只做資料）

## 做了什麼
- 新檔 `engine/source/Agit.cs`：透過 M7-A 擴充點（`[WorldReceive]`／`[WorldReset]`／`[WorldSnapshot]`）掛上，**沒改 `WorldSession.cs`／`Program.cs`**。
  - **S156 HouseList**（線上 opcode 156／0x9C；原版 op 0 `sub_92E550`）：照 SP `S_AuctionBoard` 解析 `C D(board) H(count) {D(id) S(name) H(area) C(month) C(day) D(price)}×count`，最後用 `Finish()` 驗證 8 byte padding。價格照伺服器的 `(int)` 轉型存成有號 int。新清單取代舊清單。
  - **S187 HouseMap**（線上 187／0xBB；原版 op 18 `sub_92E710`）：照 SP `S_HouseMap` 解析 `C D(objectId) D(number)`。依 WIN2 §5.2 拆成「區域＝高 16 位元」「編號＝低 16 位元」；`InOriginalRange`（區域 < 9、編號 1–70）只做標示，不擋封包。這個拆法本身是**推測**（文件依變數配置推定）。
  - 快照 `extensions.agit`：封包計數、最後一份清單、最後一份地圖。另有 `AgitHouseList`／`AgitHouseMap` 屬性，以及 `AgitHouseListReceived`／`AgitHouseMapReceived` 事件，給之後的畫面任務訂閱。
  - `Reset()` 時清空。
- 新檔 `engine/source/AgitTests.cs`：`[WorldModuleTest("agit")]` 共 44 項，只用合成封包。CI 現有的「World module extension tests」步驟會自動跑，不必改 workflow。
- `engine/_integration/dev/rebuild-lock.json`：用 `--update-lock` 重產（與 main 同步後 238 個來源、247 個鎖定檔）。
- **沒做畫面**：`AgitAuctionUI` 的 XML 在 BIN 和各客戶端都找不到；`AgitMapWindow` 需要原生 UI 畫布（T190），地圖圖號表 `aAvadentelui` 內容也還沒查證。依 gap-matrix 第 7 節，標「需先查證」的部分只做協定與資料。

## 怎麼驗證（本機 Windows）
- `rebuild.py --update-lock` 之後跑 `--build`：PASS。worktree 沒有 `engine/vendor`，所以用 `--configfile scratch/issue-512/NuGet.Config` 指向原專案的 vendor（唯讀）。productDll SHA-256 `7243ff4768de84d0cc7733a636dbfc8196ac522bfb46de40d4603bf0f92ff04a`。
- `P11.ClientEngine.exe --module-tests`：結束碼 0。`agit` 44 項、`castle` 62 項、`world-extensions` 39 項全部通過，Default 登錄表沒有錯誤。
- `agit` 測試涵蓋：
  - 四個註冊項目都在。
  - 初始快照。
  - S156：兩筆資料（含 CP950 中文名稱、價格 0xFFFFFFFF → −1）、零筆清單取代舊清單、事件觸發。
  - S187：0x40001 拆成區域 4、編號 1，含範圍邊界。
  - 格式錯誤一律丟 `ProtocolException`，且不改既有狀態：截斷、padding 非零、多一個 8 byte 區塊、筆數比實際多、opcode 錯。
  - 消化後不計入 unhandled；Reset 會清空。
- DemoChecks 回歸：`hypertext`、`doors`、`item-transfer`、`buddies` 都是 exit 0。
- `check-no-game-assets.py`：pass，0 錯誤。

## 沒驗什麼
- 遠端 CI 結果以 PR 頁面為準。沒跑 CI 其他群組（map、combat、sound、mail 等），也沒跑 Linux job，因為本次沒碰那些模組。
- 沒有真實伺服器封包，只用依 SP 欄位簽章做的合成封包；沒啟動遊戲、伺服器或 DB。
- 「區域／編號」拆法、標記位置、地圖圖號都還沒查證，也沒有畫面。

## 產出路徑
- `engine/source/Agit.cs`、`engine/source/AgitTests.cs`、`engine/_integration/dev/rebuild-lock.json`、本報告；分支 `task/512`。

## 給下一棒的提示
- **W2-C 盟屋地圖窗**（新檔 `NativeUi/AgitMapPanel.cs`，依賴 T190）：訂閱 `WorldSession.AgitHouseMapReceived`，用 `Region`／`Number` 選地圖與標記位置（WIN2 §5.2：標記 surf 176，區域 7 用 34；按鈕 surf 160 (312,56)、164 (312,110)）。要先查 `aAvadentelui` 地圖圖號表，以及 `dword_13A96F0[…+560]` 位置表。
- **S156 拍賣清單畫面**：XML 缺，WIN2 建議用 WinForms 清單或 HTML 頁，資料從 `AgitHouseListReceived` 取得。點選後應該會走 S_AuctionBoardRead（`agsel` HTML，既有 S_SHOWHTML 路徑）／S136 `agapply`（M7-E），這部分待查證。
- 本機建置要用 `py -3.12`（PATH 上沒有 `python`）；worktree 要另外寫一個 NuGet.Config 指到原專案的 `engine/vendor`。跑 DemoChecks 時先單獨 `dotnet restore --configfile …`，再 `dotnet run --no-restore`。不要把 `--configfile` 直接放進 `dotnet run`，它會被當成程式參數傳進去，結果出現「Unknown regression group」。
