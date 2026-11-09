# Issue #672 — T279：R3-C 料理窗與血盟倉庫紀錄窗（code，第 1 次嘗試）

依據：`docs/reference/gap-matrix.md` 第 12.5 節 R3-C；`docs/reference/windows-2.md` 第 7 節（W2-F、W2-G）。

## 做了什麼

| 檔案 | 變更 |
|---|---|
| `engine/source/NativeUi/CookBookPanel.cs`（新） | 原生 `COOK_BOOK_WINDOW` 面板，`[UiPanel(170)]` 在本檔自動註冊（版面 `uiextra86`）。從 XML 讀：底 2254 (6,8)、關閉 X 1962 (179,15)、捲軸 2039 (171,55)、上鈕 2042 (171,41)、下鈕 2040 (171,222)、等級圖 `SURF_RENEWAL_IMG_COOK_BOOK_1..11`（2545–2555，(116,257)）、量表 2256 (35,251)。元件缺少、不唯一、缺座標或等級圖不是 11 張都丟 `UiLayoutException`；缺圖的元件不畫。資料來源介面 `ICookBookSource`（`Current`／`Close(revision)`），關閉 X 只關這一次 revision 的窗。 |
| `engine/source/Cooking.cs` | `cookBook` 加鎖（網路執行緒收包、UI 執行緒關窗）；新增 `CloseCookBook(long revision)`（舊 revision 不關新窗）；`WorldCookBookSource` 把 `WorldSession.CookBook` 轉給面板；`ClientForm.AttachNativeCookBook()` 在原生 UI 開著時接上面板。快照多一欄 `ui`。 |
| `engine/source/ClanWarehouseHistory.cs` | 新增 `ClanWarehouseHistoryInbox`（訂閱 `ClanWarehouseHistoryReceived`，跨執行緒只留最新一份，UI tick 取走）與 `ClanWarehouseHistoryView`（列文字：角色、`類型 n`、物品（`$` 名稱代碼經字串表轉換）、數量、`n 分鐘前`）。快照 `ui` 欄更新。 |
| `engine/source/ClanWarehouseHistoryUi.cs`（新） | `ClientForm` 掛點與暫行 WinForms 清單窗 `ClanWarehouseHistoryWindow`（ListView 五欄、不搶焦點、註明 Q3-6 待查證）。收到非空 S250/117 就整份取代並顯示；離開世界時隱藏並丟掉未顯示的清單；0 筆照舊由 `WorldSession` 顯示訊息 1889。 |
| `engine/source/CookAndHistoryTests.cs`（新） | `[WorldModuleTest("cook-and-history")]`，65 項。 |

設計說明：
- **掛點**：`ClientForm` 的 `OnLoad`／`OnShown`／`OnActivated`／`OnHandleCreated`／`OnCreateControl`／`OnVisibleChanged`／`OnLayout`／`OnResize`／`OnSizeChanged` 都已被其他模組覆寫，本組不能改那些檔案，所以用 `OnClientSizeChanged`（建構子 `ClientSize=new(1180,800)` 時必定觸發）只做一次初始化：建收件匣、掛 `timer.Tick`。料理窗接線與清單顯示都在 tick（UI 執行緒）。
- 沒改 `UiPanelRegistry.cs`、`UiClientBridge.cs`、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

## 已驗證／推測／待驗證

- **已驗證（XML＋WIN2§7）**：元件名稱、surf 與座標，與 `uiextra86-c.xml` 的 `COOK_BOOK_WINDOW` 逐一對過（本機讀取，不複製進 repo；測試用同結構、不同數字的合成 XML）。
- **推測（程式內已標明）**：11 張等級圖一次只畫第 value＋1 張（value>10 不畫）；窗原點用 XML 的 x／y（建構子 `sub_A5AAC0` 的擺放未查）；框 2255、拉桿 2044 沒有座標，不畫；量表照 XML 靜態畫出。
- **不做（待驗證，不以假資料冒充）**：料理清單內容與捲動（上下鈕目前不動作）、量表數值、拖曳、Esc 關窗；原生 UI 關閉時料理窗沒有 WinForms 備援（不在本組範圍）。
- **Q3-6**：血盟倉庫紀錄的原生窗（`sub_A559D0`，按鈕 surf 9183）**只做協定與資料，不做畫面**，先用 WinForms 清單；類型 0／1 的意義、分鐘數的換算都未查證，照原服數值顯示。

## 怎麼驗證

- `rebuild.py --update-lock`（只在本機，提交前已還原 lock）→ `rebuild.py --build --configfile .github/nuget.ci.config`：`PASS dev`（productDll `f647d53b…`）。
- `P11.ClientEngine.exe --module-tests`：45 組、2276 項、0 失敗；`cook-and-history` 65 項、`cooking` 32 項、`clan-warehouse-history` 47 項、`auto-discovery` 28 項全過。
- DemoChecks `native-ui-resources`（539 項）、`native-ui-canvas`（429 項）PASS（真客戶端部分因 CI 同樣沒設 `TW_CLIENT_ROOT` 而略過）。
- `engine/tools/check-no-game-assets.py`：pass，errors 0。
- 本機證據（不進 git）：`scratch/t672/module-tests.json`。

## 未驗證什麼

- 沒有真機：原生面板實際畫面、`OnClientSizeChanged` 掛點在產品啟動時的實際觸發、WinForms 清單外觀都未實測。
- 設 `TW_CLIENT_ROOT=D:\lineage開發\0-客戶端` 跑真客戶端資源檢查時，在載入面板之前就因 `Sprite13: idx 與鎖定版本不符（SHA-256）` 停止——這是本機客戶端與資源鎖的既有差異，與本組無關，但也因此沒有用真 surf 跑過 `UiRoot.Build`。
- CI 結果見 PR。

## 產出路徑

- `engine/source/NativeUi/CookBookPanel.cs`、`engine/source/Cooking.cs`、`engine/source/ClanWarehouseHistory.cs`、`engine/source/ClanWarehouseHistoryUi.cs`、`engine/source/CookAndHistoryTests.cs`
- 本報告 `docs/reports/issue-672.md`；分支 `task/672`

## 給下一棒的提示

- 真機 smoke（dot，P24Lab）：`--native-ui` 開啟時使用料理書 → 應出現料理窗與對應等級圖，按 X 關閉；血盟倉庫按「紀錄」→ 有紀錄時跳出「血盟倉庫紀錄（n 筆）」清單，無紀錄時顯示訊息 1889。
- Q3-6 查到 `sub_A559D0` 版面後，新增原生面板並讓它也從 `ClanWarehouseHistoryInbox` 取資料；原生面板可用時把 `SyncCookAndHistory` 的 WinForms 顯示改成只在原生 UI 關閉時使用。
- 料理清單內容、量表意義要另查 `CCookWindow`（`sub_A5AAC0`）的繪製函式後再補；`CookBookPanel` 的上下鈕已有座標，可直接接捲動。
- `Sprite13` idx 的 SHA 不符要另開資源任務查（本機客戶端是否被更新過）。
