# Issue #916 — T401：R10-Q 個人商店頭上訊息兩行輪流

依據：[`followups-12.md`](../reference/followups-12.md) §2.3（T384／#896，3.81 `sub_4E9140`／`sub_414320`）。本輪先查 TW16 IDB，再照 TW16 實作。

## 做了什麼

| 檔案 | 改動 |
|---|---|
| `engine/source/PrivateShop.cs`（只限頭上訊息） | 新增 `PersonalShopTitle`：`Split`（照 TW16 `sub_A4EF80` 切兩行）、`Shown`（照 `sub_7CFFC0` 每 5000 ms 輪流）。`WorldSession` 新增 `[WorldReceive(158,Observe=true)] ObserveShopTitle`（存 S158 動作 70 的**原始 byte**，不依賴 CP950 解碼能不能保留 0xFF）、`[WorldReset] ResetShopTitles`、`ShopTitleLine(o,now)`。沒有原始 byte 時把 `ShopText` 轉回 CP950 再切。 |
| `engine/source/ObjectRendering.cs`（只限商店訊息繪製） | `NameMarksFor(id,now)` 的 `Line252` 改成 `ShopTitleLine`（此刻該顯示的那一行）；`NameLayout.Plan` 把 `now` 傳進去。畫的位置、顏色不變。 |
| `engine/source/PrivateShopTitleTests.cs`（新檔） | `[WorldModuleTest("private-shop-title")]`，自動發現；內容見下。 |

沒有改 `WorldSession.cs`、`WorldObjects.cs`、註冊表、`Program.cs`、`DemoChecks.csproj`、`rebuild-lock.json`。

## TW16 IDB 查證（已驗證，靜態）

- 來源：`P01-分析基準/ida/03-ascii-isolated/TW1608262200.idb` 複製到 `scratch/t916/ida/tw16.idb`（SHA-256 `3A0BF2065993989E44EC9F6CD764449A2B2EC36F8B499384D53FDA16BC17E82F`，與 FU5–FU13 相同）。IDA 7.7 idat 批次、腳本沿用 `docs/reference/rea-scripts/t898/ida_t898.py`（唯讀，沒有建函式或改名）；subst 只用 `K:`，每輪結束解除（結束後 `subst` 無輸出）。查詢 5 輪：`dec 92CB70／F01380`、`dec A4EF80／A41EC0／A41510／A41E80／A41420`、`xref A41510／A41EC0`＋`immscan 1388 1388`、`dec 7CFFC0／833380／6EADC0／DE78A0／EFC120／5836A0／7CE710`、`dec 4FED10`＋`xref 7CE710／7CFFC0／136E99F／138BE60`。
- 地址：`.text` FO＝VA − `0x400C00`（FU13 §0）。

1. **收包**：`sub_92CB70`（FO `0x52BF70`，S_ACTION）動作值 70 時呼叫 `sub_A4EF80(物件, 動作後的 byte)`，最後 `return sub_7CFFC0(物件)`。
2. **存兩行**：`sub_A4EF80`（FO `0x64E380`）先複製最多 80 byte（遇 0 停），清 `+98`、`+139`，再逐 byte 放進 `+98`（第一行）、`+139`（第二行），每行 41 byte 間距：
   - 遇 `0xFF`（`Str[j] == -1`）結束這一行、換下一行；
   - 第一行（`v6 == 0`）到 `j == 40` 時強制換行，`Str[40]` 直接當第二行第一個 byte（**就算它是 0xFF 也不當分隔**）；
   - 迴圈條件 `v6 < 2`：**只存兩行**，第二個 0xFF 之後不存。
3. **輪流顯示**：`sub_7CFFC0`（FO `0x3CF3C0`）每次把 `+97` 反轉；`strlen(+139) && !+97` 時顯示第二行，否則第一行。該行不空且還沒排程時，交給 `sub_7CE710`（對話框式頭上文字，色 `dword_1369D70[14]`、持續 5 秒）並 `sub_A231C0(5000, sub_7CFFC0, 物件)`（`0x7D01BA` `push 1388h`）排 5000 ms 後再呼叫自己。**該行是空的就不排下一次**。
4. 開關：`byte_136E99F` 為真才畫（同 3.81 `byte_62C9C9`，推測是「顯示對話」類選項）。`sub_4FED10` 是聊天關鍵字過濾（`Source` 空＝回 2＝不過濾）。

### 與 3.81（FU12§2.3）的差異

| 項目 | 3.81 | TW16（本輪採用） |
|---|---|---|
| 切行規則、40 byte 強制換行、80 byte 上限 | 相同 | 相同 |
| 第三行以後 | 存但不顯示 | 不存（顯示結果相同） |
| 輪流間隔 | 5000 ms | 5000 ms |
| 行緩衝位移 | `+0xC6`／`+0xEF`、旗標 `+0x118` | `+98`／`+139`、旗標 `+97` |
| 畫在哪 | `sub_4143D0` 頭上 | `sub_7CE710` 對話框式頭上文字（加框、5 秒） |

### 發現：引擎的「+252 行＝商店訊息」不對（已驗證）

`actions-and-names.md`:116 推測 `+252` 是個人商店標題，引擎據此把 `ShopText` 畫成全名模式的 +252 淺藍行。TW16 的商店訊息存在 `+98`／`+139`，**不是 `+252`**（`sub_A41510` 回傳 `this+252`，是另一個欄位）；商店訊息走的是 `sub_7CE710` 的頭上對話框，不受全名模式限制。本輪**沒有改畫的位置**：引擎沒有頭上對話框的繪製（要另外反編譯 `sub_7CE080`／`sub_7D01E0`／`sub_7D07C0` 的版面），且 `ObjectDrawTests.cs`（不在本任務可改範圍）斷言 +252 行。所以目前仍畫在 +252 行的位置，只是內容改成「此刻輪到的那一行」。見「給下一棒的提示」。

## 測試內容（`PrivateShopTitleTests.cs`）

- 切行：0xFF 切兩行；沒有 0xFF 只有一行；45 byte 無 0xFF → 40／5；第 41 byte 是 0xFF 時當第二行內容；第三行丟掉；80 byte 上限（第一行先遇 0xFF 時第二行 78 byte）；遇 0 停；開頭 0xFF＝第一行空白；`PersonalShopPackets.Chat()` 的輸出切回兩行。
- 輪流：0／4999 ms 第一行、5000／9999 ms 第二行、10000 ms 回第一行；只有一行一直顯示；第一行空白不顯示；時間倒退當 0。
- 合成封包：S87 店主＋S158 動作 70（含 0xFF）→ `ShopActive`、`NameMarksFor(id, ShopAt+…)` 與 `NameLayout.Plan` 全名模式依時間換行；滿 40 byte 的訊息；收攤後不顯示；`Reset` 清掉原始 byte。
- 自動發現：S158 觀察器、重設已登錄，預設登錄表無錯誤。

## 怎麼驗證

- `dotnet build engine/source/OfflineClient.csproj -c Release --no-restore`：**0 錯誤**（3 個警告不在本次檔案）。本機 restore 會出 NETSDK1130（同 #675），改用 `scratch/t916/NuGet.Config` 唯讀指向主專案 `engine/vendor`、`NUGET_PACKAGES` 指到 `scratch/t916/packages`；為此刪掉 `engine/source/obj/` 下 5 個 NuGet 輸出單檔（非遞迴，未入 git）。
- `engine/tools/check-no-game-assets.py`：`pass`（4487 檔，0 錯誤）。
- 單元測試與本組離線測試：依本機規定**不在本機執行**，交給 PR #949 的 GitHub Actions CI：`dotnet`、`map-continuity`、`npc-offline` 全部 pass；`--module-tests` 中 `private-shop-title` pass（32 項檢查），所有群組 `failed` 都是 0。
- 因此 S158 動作 70 帶 0xFF 經過 `WorldObjects.cs` 的 `r.S()`（CP950、例外 fallback）**不會丟例外**（已驗證，CI）。
- 資源：本任務只處理文字，沒有宣稱缺任何資源，沒有用到 PakViewer；沒有複製遊戲資源。

## 未驗證／推測

- **推測**：`+97` 初值為 0（所以第一輪顯示第一行）；TW16 換訊息時不重設 `+97`、已排程時不重排，引擎改用「從 `ShopAt` 起算」的週期，可能和原版差一個相位。
- **推測**：被 40 byte 強制換行切開的半個雙位元組字，引擎以替代字元顯示；原版直接畫那個 byte，畫面要真機看。
- **未做**：`sub_7CE710` 對話框式外觀（框、位置、5 秒淡出）；`byte_136E99F` 選項；`sub_7CFFC0` 開頭的條件（`a1` 是自己或遊戲狀態 340／350／360／370／800、虛擬函式 +28／+35）沒有逐一對應。
- **真機再確認**：兩行訊息在畫面上輪流（P24Lab 角色 A 開店填兩行，角色 B 看 5 秒換一次）。

## 產出路徑

- `engine/source/PrivateShop.cs`、`engine/source/ObjectRendering.cs`、`engine/source/PrivateShopTitleTests.cs`
- `docs/reports/issue-916.md`（本檔）
- 不進 git：`scratch/t916/`（IDB 副本、查詢清單與輸出、建置輸出）

## 給下一棒的提示

1. **建議開 code 任務：商店訊息改畫成頭上對話框**。先反編譯 TW16 `sub_7CE080`（建框物件）、`sub_7CF080`（斷行）、`sub_7D01E0`（寬度，`v25` 16／20 字）、`sub_7D07C0`（畫）與 `sub_7CE710` 的位置計算（`+223`／`+224`、全名模式時 −32、否則 −12），再把 `NameMarksFor` 的 `Line252` 改回真正的 +252 欄位（同時要改 `ObjectDrawTests.cs`:150–164 的斷言）。`PersonalShopTitle.Shown`／`WorldSession.ShopTitleLine` 可以直接沿用。
2. 一般聊天的頭上對話框也走 `sub_7CE710`（呼叫者 `sub_94AF00`、`sub_94B4F0` 等），可以一起做。
3. 真機（Codex／dot，P24Lab 測試角色）：角色 A 開店填兩行訊息，角色 B 開全名模式看商店訊息是不是每 5 秒換一行。
