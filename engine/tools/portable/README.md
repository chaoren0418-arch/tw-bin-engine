# tw-bin-engine 可攜版（草稿）

> 本檔是可攜 zip 的使用說明，由 `engine/tools/package-portable.py` 放到 zip 根目錄。
> 目前狀態：**打包與內容審查已可用；自主素材準備器仍為 partial**，正式 GitHub Release 由使用者另行決定。

## 本 zip 內容

- 只含程式：啟動器（`engine/launcher/`）、引擎（`engine/build/client-product-dev/`）、WebView2 相依、產品 manifest、`engine/web/` 的 8 個文字頁面檔。
- **不含任何遊戲資源**：沒有 assets、圖片、音效、map、Sprite、Tile、Text、PDB、原始碼、SQL、JAR、BIN、IDB。所有遊戲資料都在你自己的電腦上，從你自己的原客戶端讀取或準備。
- 不附伺服器、帳號或密碼。

## 前置需求

1. Windows x64。
2. .NET 10 Desktop Runtime x64。
3. Microsoft Edge WebView2 Runtime。
4. 你自己的受支援原客戶端目錄（3.81／TW1608262200）。

## 使用步驟

1. 把 zip 解壓到有寫入權限的目錄，例如 `D:\Games\TWEngine`。
2. 用文字編輯器打開 `啟動引擎.cmd`，把 `TW_CLIENT_ROOT` 那一行改成自己的原客戶端路徑（保留引號），例如 `set "TW_CLIENT_ROOT=D:\Games\MyOriginalClient"`。
3. 執行 `啟動引擎.cmd`。首次會檢查原檔版本並在本機準備資源；原客戶端保持唯讀。完成後出現正常登入畫面。
4. 若顯示版本不符、必要檔缺失或準備失敗，依訊息核對原客戶端目錄，修正後重跑。錯誤時不會進入遊戲。
5. `啟動引擎.cmd --check-only` 只檢查程式與已準備的資源，不開畫面；乾淨解壓尚未準備時會回報未準備狀態。
6. 登入畫面之後的連線需要另有相容且獲准的伺服器。

所需磁碟空間與首次準備時間尚未實測，待素材準備器完成後填入。
