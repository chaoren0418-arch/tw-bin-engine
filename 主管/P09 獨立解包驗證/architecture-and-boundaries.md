# P08 替換邊界與實作

這是 P07 accepted 複本上的 Windows WinForms + WebView2 本機面板。世界/SPR 渲染仍在原生客戶端，新網頁不是整個遊戲瀏覽器化，也不是原 Awesomium 二進位或 ABI 重建。

## 原證據與新邊界

`provenance/P02/modules.md` 保留原證據。以下為 **RVA**，原 PE 基址 VA 00400000：006F2510 → 006A5D70 → Awesomium JSObject::SetCustomMethod 是原生/JS 綁定邊界；006A6A60 → 006A5ED0 是原名稱與型別分派；00707700 的 WebView 導航、00710150 的 BitmapSurface → 565 緩衝為已查證局部邊界。間接 vtable 方法、完整 ABI、原生命週期仍 unknown。

新實作用 CoreWebView2.WebMessageReceived / PostWebMessageAsJson 取代研究展示所需的訊息邊界。WebView2 是 HWND 子控制項直接合成畫面，不重作原 BitmapSurface 到 565 的路徑。角色素材與世界繪製沿用 P06/P07。

## 有限訊息

來源必須完全等於 `https://p08.example/index.html`，這是虛擬主機到本階段 web 目錄的本機映射，不是對外網站。對送達WebResourceRequested的請求，HTML/CSS/JS三個精確URL以外回403；本機虛擬主機的缺頁可能直接回導航錯誤，最終缺頁證據為IsSuccess=false、HttpStatusCode=0，不能誤稱收到HTTP403。非指定導覽、開新窗、下載、權限請求均拒絕。CSP 禁止 connect、frame、表單、inline script、外部資源。無 host object、shell、任意路徑或網路 API。

頁→客戶端是 JSON object，最多1024字元，v=1、正整數id、字串cmd；重複/多餘欄位拒絕。cmd只有 ready、focusGame、face。face另需整數 direction=0..7。未知命令/錯誤型別記錄 bridge-rejected，模型不改變。id只作本機回覆顯示，不是新增的網路 transaction ID，也不承諾防止同 id 的語義重播。

face 經既有 HandleDown/HandleUp → NetworkKey/World.Face。忙碌、行走、通訊未Ready時拒絕。通訊模式沿 P07 HeadingConnection.Request → 原切片編碼 → 自有 loopback → 有效解碼 → Applied → World.Face；頁面不能繞過回覆修改模型。native→頁為 v1 state（遞增revision、角色、位置、方向、行走、步數、模式、連線、pending、確認數、焦點）及reply。頁面只呈現客戶端狀態，沒有自行預測方向。

## 焦點與生命週期

原生 KeyDown 與 ProcessCmdKey 同時由 GameInputAllowed 保護。面板/工具列進入焦點、視窗 Deactivate、面板開關會清 Input.Held 與 World.Intent；既有 World 允許目前格步完成後停止。頁面 focusGame、F10、原生「遊戲控制」或地圖點擊恢復遊戲門檻；F11將焦點交給網頁輸入欄。F10/F11在WinForms與頁面兩側都有處理，頁面的F10經既有focusGame橋接，並阻止瀏覽器/系統選單預設動作。頁內 WASD/數字/方向鍵不走角色控制。程式內測試呼叫 controller 另行標記，不能當原生輸入證據。

OpenPanel 使用 epoch；ClosePanel 先失效 epoch，再等正在進行的真實 CreateAsync/EnsureCoreWebView2Async 完成並釋放建立出的 controller，移除所有具名事件、Stop、Dispose。FormClosing 先取消第一次關閉，完成非同步清理後再 Close，不把晚到初始化交給已銷毀視窗。各次開啟只有一套訂閱。user-data 位於本階段 runtime/session-PID，沒有使用既有瀏覽器 profile，也不停止任何外部瀏覽器。

WebView2 無法以 CancellationToken 取消已開始的建立工作，因此這裡採失效後等待回收；若系統/COM 永不返回，仍可能等待，沒有宣稱能修復任意 OS 卡死。UDF 建立/寫入預檢避免已知的 Chromium 錯誤對話框；SDK錯誤與頁面錯誤有可見訊息，離線客戶端仍能操作。瀏覽器背景服務不屬完全離線沙箱承諾，但本頁資源與橋接無外送路徑，沒有正式服務目的位址。

## 依賴與驗證界線

WebView2 SDK 1.0.4258.31 由 Microsoft 官方 NuGet URL 取得；完整 nupkg 保存在 vendor，NuGet.Config只還原本機vendor。Runtime 使用既有155.0.4283.62；版本、exe SHA256及Microsoft簽章狀態見 dependencies.json。未全機安裝。SDK package同時引入WPF參考，本專案於ResolveAssemblyReferences前移除未使用的WPF引用，以避免WindowsBase版本衝突，不隱藏編譯警告。

官方使用依據：[安全設計](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security)、[WinForms 起步](https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/winforms)、[本機虛擬主機映射](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.setvirtualhostnametofoldermapping)。這些是新WebView2實作依據，不是原BIN行為證據。

故障測試明示：runtime-missing 是將SDK的runtime路徑指向不存在的本階段目錄，未移除真實runtime；init-failed 是用現存nupkg檔案作UDF以觸發真實檔案系統衝突/預檢；page-failed 是真正請求不存在的本機頁，必須等待NavigationCompleted失敗及本機錯誤提示，HTTP狀態以事件紀錄為準。close-init 在真實初始化Task未完成時ClosePanel，紀錄可區分 environment/controller 取消。

`*-screen.png` 由應用程式 CopyFromScreen 擷取實際桌面，需可見、不遮擋的自有視窗；`*-client.png` 是WinForms DrawToBitmap，無法證明WebView2原生子視窗內容。DOM測試與外部computer-use輸入分別保存。原生工具的截圖/輸入原始收據在工作對話，交付另保存操作摘要及可核對模型/線路。

## 上游未知仍保留

未實作原服握手、seed導出、帳號/身份登入；預置post-init key/物件/序號是P07測試政策。與3.80c/正式服版本配對unknown；單pending同方向回應沒有transaction ID，不能辨別語義重複回覆。移動僅離線；原切片網路只做站立轉向。單地圖區塊、碰撞/尋路unknown、可穿建物/水，群組未觸發；資產/BIN配對、原565模式unknown；沒有完整速度/負重、影子/音效。詳見 provenance/P07/format-and-state.md 與 provenance/P06。
