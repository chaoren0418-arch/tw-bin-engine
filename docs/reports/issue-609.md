# Issue #609 — T248：血盟公告與成員備註更新（PBW-C）

依據：`docs/reference/packetbox-windows.md`（T245／#602）第 2.10、2.11、6 節；`docs/reference/windows-2.md` 第 3 節。只改 `engine/source/NativeUi/PledgePanel.cs`。

## 做了什麼

- **169 成員備註**：W2-H（#561）已經做好（消化型 `[WorldReceive(250, Subtype=169)]`、`C(169) S(名稱)＋62 byte Big5`、寫進成員列的 `Memo_Label`），本次沒有改動，只確認符合 PBW-C。
- **168 血盟公告（新增）**：
  - `PledgeProtocol.DecodeNotice／EncodeNotice`：照 3.81 `S_PacketBox(168, announce)` 讀 `C(250) C(168) S(公告)`＋補齊；格式不對（沒有 NUL 結尾、補齊位置有非零位元組、子碼錯）就丟 `ProtocolException`。
  - `WorldSession.ReceivePledgeNotice`：用 M7-A 擴充點 `[WorldReceive(250, Subtype=168)]` 消化（主分派原本沒有 168 的 case；`WorldSession.cs` 沒改）。只更新公告文字並增加 `Revision`，**不開窗**（TW16 `sub_B2EF40` 只寫已經存在的公告欄）。換世界、`Reset`、血盟改變時跟成員資料一起清掉。
  - `PledgeWindowState` 加上可省略的 `Notice`（`null`＝這次進世界還沒收到 168）；快照 `pledgeWindow` 多一個 `notice` 欄位。
  - 版面：公告窗 `Noticeuserinfo_Window` 必須有 `Notice_Control`／`Notice_Label`（本機 `PledgeUI.xml`：控制項在 (21, 63)，標籤 220×109，靠上、靠左），缺少就丟 `UiLayoutException`，不補預設座標。無公告窗沒有公告區。
  - 畫面：切到公告窗時，從公告區左上角開始逐行畫最近一次 168 的文字；先依換行字元分段，再依寬度逐字斷行，最多畫 109 / 12 ＝ 9 行（跟 `Notice_Edit LimitLine="9"` 一致）。沒收到公告就留白。
- 寫公告和存公告（`Writenotice`／`Savenotice` 按鈕、`Notice_Edit`）會送出封包，本任務沒有做。

## 怎麼驗證

- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile scratch/issue-609/NuGet.Config`（設定檔指向原專案的離線 `engine/vendor`）→ `PASS dev`，productDll `ac72f76e…e73e6`。依 #646 的規定，PR 不改 `rebuild-lock.json`：`--build` 會顯示「lock 已過期」的警告，改用暫時的 lock 建置（變動的只有 `PledgePanel.cs`）。
- `P11.ClientEngine.exe --module-tests <out>`：31 組、1577 項全部通過，結束碼 0。`pledge-panel` 從 82 項增加到 110 項，新增的測試涵蓋：
  - 168 已註冊為消化型處理
  - 位元組格式與解碼（中文、空字串、換行）
  - 三種壞封包會被拒絕
  - 收到 168 不開窗；收到 171 後公告仍保留；新的 168 會取代舊公告，只增加 `Revision`
  - 快照有 `notice`；`Reset` 會清掉公告
  - 斷行：依寬度斷行、`\r\n` 與空行保留、最多 9 行
  - 公告區座標 (21, 63, 220, 109)，無公告窗沒有公告區
  - 有公告時公告區內有白色像素，沒公告時沒有
  - 缺少 `Notice_Control` 的 XML 會被拒絕
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py` → pass，errors 0。

## 未驗證什麼

- 沒有實機測試，也沒有讀真的 `PledgeUI.xml` 來畫圖。合成 XML 的公告區數值是照本機 `scratch/task448-uixml/PledgeUI.xml` 抄的。
- 推定：TW16 讀固定 212 byte，3.81 只送 `S`，兩者顯示結果相同。如果伺服器真的送 212 byte 固定長度（補齊後不是 0），本實作會照規則拒絕。
- 推測：MultiLabel 的行距（12 px）、斷行規則（沿用聊天列的逐字斷行）、字色（白色）。
- 原版收到 168 時會「設為可見」，本實作的做法是「收到過公告就畫出來」；在無公告窗時要不要顯示公告，目前不顯示。

## 產出路徑

- `engine/source/NativeUi/PledgePanel.cs`
- `docs/reports/issue-609.md`（本檔）
- 分支 `task/609`

## 給下一棒的提示

- `PledgePanel` 目前**還沒註冊進 `UiRoot`**（W2-H 已經提過）。接線時要用真的 `PledgeUI.xml` 載入一次，確認公告窗確實有 `Notice_Control`／`Notice_Label`，否則整個原生 UI 會退回舊 HUD。
- 如果要做寫公告（C_PledgeContent 類型 15）和寫備註（類型 16），需要 `Notice_Edit` 的輸入框和送出封包，應該另開任務；伺服器成功後會回送 168／169，本模組已經能接。
- 依 packetbox-windows.md，3.81 的 168／169 呼叫點只有寫入成功後回給本人；其他成員怎麼收到公告，本輪沒有查（待驗證）。
