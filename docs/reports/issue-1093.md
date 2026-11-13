# Issue #1093：SPELL_WINDOW 直向等級條使清單高度為零（#475 SM01）

階段：code，第 1 次嘗試；分支 `task/1093`。

## 原因

`SpellWindowPanel.Build` 假設等級鈕是**橫向**長條，把清單頂端設為「等級圖底緣」與「上箭頭」中較低者。
真實客戶端的 33 張等級圖（2058–2090）**全部是 14×212 直條**，位於 (12,37)（#475 `spell-layout.json`；例如 Sprite13 `2058c.img`，SHA-256 `84dbb3af…7b9c4e`）。
於是清單頂端 = 37+212 = 249，大於下箭頭 y=225 → `Rows.PageSize=0` → `UiLayoutException` → `UiRoot.Build` 整體失敗、退回 classic。
既有離線檢查用的是合成的 150×18 橫條，所以 CI 沒攔到。

## 做了什麼

- `engine/source/NativeUi/SpellWindowPanel.cs`
  - 新增 `LevelStrip`（所有等級圖的聯集矩形）與 `ListArea(strip,left,right,top,bottom)`：
    - 直條（高 > 寬）：清單放在等級條**右側**，`left = max(底圖左緣, 等級條右緣)`，上下界仍是上箭頭 y～下箭頭 y。
    - 橫條：維持原行為，清單從等級條下方開始。
    - 範圍不成立時回傳空矩形，仍丟 `UiLayoutException`（不硬塞尺寸、不隱藏面板）；錯誤訊息改為附上等級條與清單範圍數字，方便下次診斷。
  - 真實素材的結果：等級條 (12,37,14,212)，清單 x 26–171、y 44–225 → **11 列 × 16 px**，`Rows.Area=(26,47,145,175)`，不與等級條重疊。
- `engine/source/NativeUi/ItemIconSource.cs`：`LegacyStrip.SegmentAt` 沿長邊切段——寬條由左到右（背包／倉庫分頁不變），高條由上到下（魔法等級條）。原本對 14 px 寬的直條以寬度切 10–11 段，點擊會選錯等級。
- 回歸檢查（`[WorldModuleTest("native-spell-window")]`）：合成等級圖改為真實尺寸 14×212，新增
  - 等級條矩形 = (12,37,14,212)、`Rows.PageSize==11`、`Rows.Area==(26,47,145,175)`、清單不覆蓋等級條；
  - 橫條仍把清單往下推；橫條超過下箭頭 → 空範圍；
  - 等級條寬到蓋滿清單 → 仍拋 `UiLayoutException`；
  - 直條點最下端 → 一般第 10 級、第 2 段 → 第 2 級。
  - 原本的 150×18 預期值改為 14×212。用舊程式碼跑這組檢查會在 `Load` 就拋例外，可攔住本問題。

未改：`UiClientBridge.cs`／`UiRoot.PinMismatchMessage`（「原客戶端 UI 資源與鎖定版本不符」的統一提示）、`Program.cs`、註冊表、`rebuild-lock.json`、任何客戶端或封存素材。

## 怎麼驗證

- 已驗證：本機 `dotnet build source/OfflineClient.csproj -c Release`（輸出到 `scratch/issue-1093/build`）0 錯誤，4 個既有警告（與本次無關的檔案）。
- 已驗證（靜態）：真實幾何用 #475 證據 `spell-layout.json` 手算（底圖 (6,8) 181×271、捲軸 x=171、上箭頭 y=44、下箭頭 y=225、33 張等級圖 14×212）。
- 待驗證（CI）：`native-spell-window` 模組檢查由 GitHub Actions windows-latest 的 `--module-tests` 執行；依本機執行限制，本機未執行任何編譯產物。

## 未驗證什麼

- 真機：正常 dev 入口是否進入 `native-ui active`、魔法窗在原生畫面開啟時清單位置是否與原版一致——交給重測任務（Codex／dot）重跑 #475 受阻列。
- 推測：清單文字確切起點（原版 BIN 的列原點未追）；等級條各段等長切分（與背包分頁相同的推測）。等級圖本身可能把各級分頁畫在固定像素位置，若真機發現點擊錯位，需量 2058c.img 的分頁邊界。
- 只修了 #475 報出的這個面板；其他 61 個面板 Load 成功不代表畫面驗收通過。

## 產出路徑

- `engine/source/NativeUi/SpellWindowPanel.cs`
- `engine/source/NativeUi/ItemIconSource.cs`
- `docs/reports/issue-1093.md`

## 給下一棒的提示

- 重測：`engine\啟動新版客戶端.cmd --evidence <scratch 內目錄>`，`native-ui.log` 應為 `verifying → active`，不再有 `SPELL_WINDOW: 魔法清單沒有空間`。開魔法窗（選單 3／Ctrl+S）確認文字列在等級條右側、不蓋住等級條；點等級條上下不同位置應切換等級頁。
- 若仍退回 classic，看 `native-ui.log` 第一個例外的面板名：現在錯誤訊息會帶等級條與清單矩形。
- 「原客戶端 UI 資源與鎖定版本不符」對所有版面例外一律顯示，會誤導；建議另開小任務讓 `UiClientBridge` 區分「資源 pin 不符」與「版面例外」。
