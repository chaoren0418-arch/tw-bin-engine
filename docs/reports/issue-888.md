# #888 T382：R10-A FishObtainUI 清單（背包尾段旗標＋desc id 表）

依據：`docs/reference/followups-10.md` §1（T380／#884）、R9-P（#860）。裁定：照原版邏輯完整實作。

## 做了什麼

1. **`engine/source/Inventory.cs`（只限尾段解析）**
   - S5 S_InvList／S15 S_AddItem 的尾段改成「1 byte 長度 n＋內容」（對應 `sub_926B10`／`sub_926A70`）。
   - n ≤ 10 照舊讀 10 byte，n > 10 讀滿 n byte。`TailHex` 仍然是「長度＋內容」的 hex，3.81（n=10）的值和以前完全相同。
   - 新增 `InventoryItem.FishObtainFlag`（init 屬性，不動建構子與既有欄位）＝延伸區塊 +10 的 bit 3（`sub_518C80`，遮罩 `{8,0}`）。只看前 n byte，最多 24 byte。
   - S5／S15 解析成功之後（截斷封包不會寫表），旗標成立的物品把 desc id 加進表（`sub_E65030` 的三個呼叫點）。
2. **新檔 `engine/source/FishObtainTable.cs`**：desc id 表模型（`unk_1542864`）。
   - 存 16 位元 desc id 的符號延伸（`movsx`），不排序、不去重；查詢用線性搜尋（`sub_E65420`）。
   - 只在世界重設時清空（`[WorldReset] ResetFishObtainTable`，對應 `sub_8C32B0 → sub_E65280 → sub_E65320`），結束釣魚不清。
   - 快照：`extensions.fishObtain`（revision／count／ids）。
   - 變動時發布 `FishObtainTable.Latest`，原生窗從這裡讀；同時提供 `Changed` 事件。
3. **`engine/source/NativeUi/FishObtainPanel.cs`**（這個檔已經存在，任務寫「新檔」，實際是改既有的面板）
   - 從參考客戶端 `FishObtainUI.xml` 讀出清單列（7 個 CheckButton，幾何同 UserObtainUI）和捲軸（底圖 5196、上／下鈕、拖曳鈕，ScrollStart 16／ScrollHeight 224）。
   - 清單＝背包裡 `DescriptionId` 在表內、數量 > 0 的物品，順序照背包。
   - 每列畫圖示（`ItemIconSource`）、名稱、數量。上／下鈕與滾輪可以捲動；拖曳鈕的位置依比例計算。
   - 表空時清單是空的（3.81 一定是這樣），畫面上只有底圖、外框、關閉鈕和捲軸。
   - 「清單待驗證」那兩項 `Skipped` 已經拿掉。面板仍實作 `IUiClosablePanel`，`--classic-ui` 的 WinForms 入口沒動。
   - XML 核對：用 PakViewer CLI（`pak extract` 加 `xml decrypt`）從 kor25 `D:\381懶人包kor25(原)\Tile.idx` 解出到 `scratch/t888/`（不進 git）：
     - 原檔 SHA-256 `b66b93d28c830603cc5063bbf88e061af5e2fbe845892c2a24d80fed8b43d55c`
     - 解密後 `01d94c4464e3d798ee87af362d4e838555530f9dd3635d26f47323ab1045096a`，6,370 byte
     - 兩者和 FU9§6.2 記的前綴相同。執行期仍由 `UiResources.Xml` 讀取，沒有複製進 repo。
4. **`engine/source/NativeUi/FishingPanel.cs`**：只改註解，指向新的清單與表（FU10§1）。
5. **新檔 `engine/source/NativeUi/FishObtainTests.cs`**：`[WorldModuleTest("fish-obtain")]`，自動發現，內容如下。
   - 尾段：3.81 的 n=10 不設旗標、`TailHex` 不變；舊合成封包（11 byte 全 0）照舊能解析；n=11／12／18／30 的旗標判斷正確，後面的欄位對齊不受影響；尾段截斷時丟例外，而且不寫表。
   - 表：S5／S15 加入、符號延伸、不去重；同 desc id 但沒旗標的物品也會列出；S57 刪除只拿掉清單列，表保留；新的 S5 不清表；世界重設清表；快照與 `Latest` 有發布。
   - 面板：列與捲軸的幾何；表空時清單空，畫面只有底圖和捲軸；表有值時列出、捲動、夾在範圍內；數量 0 不列；離開世界時關窗；產品路徑 WorldSession → `Latest` → 清單。
6. **範圍外的必要改動（請審查注意）**：`engine/source/NativeUi/FishingFollowupTests.cs` 有兩個斷言寫的是 #818 的舊行為（「清單、捲軸不畫」），照本任務裁定一定會失敗，所以改成新行為：清單照 XML 建、捲軸底圖畫出。其他斷言沒動。這個檔不在 Issue 列的可改檔案裡，但 FU10§1.6 建議的可改檔案有它。

## 怎麼驗證

- **編譯**：`dotnet build engine/source/OfflineClient.csproj -c Release --no-restore -r win-x64 -p:SelfContained=false -p:EnableWindowsTargeting=true`，結果 **0 錯誤**。4 個警告都是既有的（`HtmlActionTests.cs:108`、`MagicUi.cs:20`），這次改的檔案沒有警告。
  - 本機環境繞法（沒進 repo）：這台機器有 WebView2 的 NETSDK1130 已知問題（同 #855／#856），所以用 `NUGET_PACKAGES=scratch/t888/nuget` 隔離還原（設定檔用 `.github/nuget.ci.config`），再用 `-p:CustomAfterMicrosoftCommonTargets=scratch/t888/wv2-fix.targets` 把 winmd 換成 `lib_manual/netcoreapp3.0` 的 DLL。輸出放在 `scratch/t888/build`。
- **資源檢查**：`py -B -X utf8 engine/tools/check-no-game-assets.py` 結果 `status: pass`，commit 後再跑一次。
- **離線測試**：依本機限制**沒有在本機執行**。`fish-obtain`、`fishing-followup` 與既有的 module tests 交給 PR 的 GitHub Actions CI 跑，結果以 PR 上的 CI 為準。
- **沒做的事**：沒有啟動遊戲、伺服器或 DB；`rebuild-lock.json`、releases、封存都沒動。

## 已驗證／推測／待驗證

- **已驗證（靜態，FU10§1）**：表的來源、寫入條件、清空時機、比對欄位、3.81 尾段固定 10 byte 全 0。所以 3.81 下表恆空，清單恆空。
- **推測（標在程式註解）**
  - 原版在自己的動作 71 重填清單，S57 和數量更新時同步。引擎改成每個畫面從目前背包直接過濾，穩定之後結果相同。
  - 列的數量欄畫的是背包數量：`sub_C04B00` 寫的是「依數量差更新」，確切顯示什麼沒查。
  - 捲軸每格一列，拖曳鈕位置依比例。
  - n < 10 時引擎仍讀 10 byte，和原版不同：原版只讀 n byte。這是為了不改既有欄位語意與既有合成測試；3.81 不會送 n < 10。
  - n > 24 時原版怎麼寫沒查，引擎只用前 24 byte 判斷旗標。
  - `FishObtainTable.Latest` 是行程內共用的值（產品只有一個連線中的世界）；換帳號重登時，要等世界重設才會清。
- **待驗證（真機）**：原版 TW16 接 3.81 時，FishObtainUI 是否確實是空清單；清單空時捲軸會不會顯示拖曳鈕。

## 產出路徑

- `engine/source/Inventory.cs`、`engine/source/FishObtainTable.cs`（新）、`engine/source/NativeUi/FishObtainPanel.cs`、`engine/source/NativeUi/FishingPanel.cs`、`engine/source/NativeUi/FishObtainTests.cs`（新）、`engine/source/NativeUi/FishingFollowupTests.cs`（兩個斷言）
- 報告：`docs/reports/issue-888.md`
- 分支：`task/888`

## 給下一棒的提示

- 如果之後要接 TW16 的延伸尾段（伺服器送 n > 10），表與清單已經會動。若要做到逐筆和原版一致（動作 71 重填、S57／op 164 同步、`sub_C038D0` 直接加列），可以在 `FishObtainPanel.Update` 改成事件驅動。
- 數量欄若要改畫「本次釣到的數量差」，要先查 `sub_C04B00`／`sub_C03720` 的列內容。
- 真機確認交給 Codex／dot：P24Lab 開釣魚窗，按 FishingNet，預期看到空清單和捲軸。
