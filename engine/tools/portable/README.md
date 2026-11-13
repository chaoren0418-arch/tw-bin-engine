# tw-bin-engine 可攜版

天堂 3.81 舊客戶端（TW1608262200）的 C#／.NET 重新實作。這個 zip 只有程式，遊戲資料全部來自**你自己的原客戶端**：第一次啟動時在本機準備，原客戶端只讀不寫。

> 本檔由 `engine/tools/package-portable.py` 放到 zip 根目錄（來源 `engine/tools/portable/README.md`）。
> 文中用反引號括起來、含中文的訊息，都是程式實際輸出的文字；`…` 代表會換成實際值的部分。打包腳本的自測會逐句核對這些文字仍在程式裡。

## 1. zip 內容

| 路徑 | 內容 |
|---|---|
| `README.md` | 本檔 |
| `啟動引擎.cmd` | 啟動入口；**唯一需要你修改的檔案** |
| `engine/launcher/` | 啟動器 `P24.Launcher.exe` 與執行期設定 |
| `engine/build/client-product-dev/` | 引擎 `P11.ClientEngine.exe`、`P24.Audio.dll`、WebView2 相依、`runtimes/win-x64/native/WebView2Loader.dll` |
| `engine/build/product-dev-manifest.json` | 上面每個程式檔的大小與 SHA-256；啟動器每次都逐檔核對 |
| `engine/web/` | 8 個面板頁面檔（html／css／js） |

**不含**任何遊戲資源（圖片、音效、map、Sprite、Tile、Text、assets）、PDB、原始碼、SQL、JAR、BIN、IDB。**不附伺服器、帳號或密碼。**

## 2. 需要的環境

1. Windows 10／11 x64。
2. **.NET 10 Desktop Runtime x64**（Windows Desktop Runtime；只裝一般 .NET Runtime 不夠）。
3. **Microsoft Edge WebView2 Runtime**（多數 Windows 10／11 已內建）。
4. **你自己的原客戶端目錄**，版本見第 3 節。
5. 磁碟空間與時間見第 5 節。

## 3. 支援的原客戶端版本

`TW_CLIENT_ROOT` 指向的目錄必須有：

- `Text`、`Tile`、`Sprite`、`Sprite00`–`Sprite15` 共 19 組 `.idx`／`.pak`；
- `map` 目錄。

`Sound` 目錄不是啟動條件，但音效在執行期直接從這裡讀；檔案缺少或內容與程式的 pin 不符時，該音效不播放。

是不是支援的版本，由下列「錨點」決定（程式只存雜湊，不含內容）。任何一項不符，準備就停止並寫出是哪個容器、哪個 entry，**不會**改用其他來源補：

| 容器 | entry | 比對 |
|---|---|---|
| Text | `list.spz` | 解碼後 SHA-256 `e8cec0388d6e9fdf518493fba1674953141c023765e02c210c05a23dfbbe6242` |
| Text | `list.spr` | 解碼後 SHA-256 `523c61ad5cc362a56a36f58b262c84efa8b4435f017e7074d35b5834e8b9f127` |
| Text | `desc-c.tbl` | 解碼後 SHA-256 `f06c94f47271fcb01f1d5af63725dfa9a230b5f6be3c0dd1d53cd37628adb7df` |
| Text | `spelldesc-c.tbl` | 解碼後 SHA-256（開頭 `387c4a9d`） |
| Tile | `Water.lac`、`pier1.lac`、`pier5.lac` | 船運背景用的三個 SHA-256 |
| map/6 | `7fff8000.seg` | SHA-256（開頭 `aad5ff59`） |
| 產生結果 | `maps/effects/6016.json` | SHA-256（開頭 `825673b1`） |

整包 `.pak` 的 SHA 不當拒絕條件：同一版客戶端被附加了無關檔案，只要用到的 entry 沒變，仍然可以用。`*-c.html` 對話模板不設錨點（不同發行只差圖片編號）。

## 4. 使用步驟

### 4.1 解壓

把整個 zip 解壓到**有寫入權限**的目錄，例如 `D:\Games\TWEngine`。保持資料夾結構，不要只拿出 exe。第一次啟動會在這個目錄底下建立 `engine\assets`（準備好的資源）與 `engine\runtime`（準備過程的暫存與紀錄）。

- 不要解壓到 `C:\Program Files` 這類需要系統管理員權限的位置。
- `engine\assets` 與它上層的任何一層都不可以是 junction 或符號連結，否則準備器會拒絕寫入。

### 4.2 設定 `TW_CLIENT_ROOT`（唯一要改的地方）

用文字編輯器（例如記事本）打開 `啟動引擎.cmd`，找到這一行：

```
set "TW_CLIENT_ROOT=D:\Games\MyOriginalClient"
```

把等號後面改成你的原客戶端**完整路徑**，引號保留，例如：

```
set "TW_CLIENT_ROOT=E:\Lineage\TW381"
```

- 路徑可以有空格與中文；入口會先切到 UTF-8 字碼頁，用記事本預設的 UTF-8 存檔即可。
- 要指向放 `Text.pak`、`map` 的那個目錄，不是指向檔案或子目錄。
- 同一個檔案裡的 `TW_ENGINE_ROOT` **不用改**：入口用自己的位置推導（`%~dp0engine`）。啟動器本身也一律以自己所在的位置當引擎根目錄，不會被外部設定改到別處。
- 直接執行 `engine\launcher\P24.Launcher.exe`（不經入口）時，要在呼叫端自己設定 `TW_CLIENT_ROOT` 環境變數。

### 4.3 第一次啟動

連按兩下 `啟動引擎.cmd`（或在命令提示字元執行）。依序發生：

1. 啟動器逐檔核對 `engine/build/product-dev-manifest.json` 列的程式檔大小與 SHA-256。
2. 檢查 `TW_CLIENT_ROOT`：目錄存在、19 組 `.idx`／`.pak` 與 `map` 目錄都在。
3. 發現還沒準備資源時，輸出一行 `"status":"preparing"`，接著執行 `P11.ClientEngine.exe --prepare-assets`（不開畫面、不連網路、不啟動服務）：
   - 先確認磁碟空間，再逐項從原客戶端解碼。每完成一項就輸出一行 `"status":"preparing","progress":{…}`，`progress` 裡有 `phase`、`recipe`、`completed`／`total`、`estimatedRequiredBytes`、`availableBytes`。
   - 輸出先寫到 `engine\runtime\assets-staging\`，全部驗過後才一次換成 `engine\assets`。耗時、磁碟用量與實際的 `TW_CLIENT_ROOT` 記在 `engine\runtime\prep-log.json`。
   - 完成時輸出 `"status":"prepared"` 與準備收據（`engine\assets\prep-receipt.json`）。
4. 啟動器重新讀回收據並核對每個輸出檔的 SHA-256；接著檢查 Windows x64、.NET 10 Desktop Runtime 與 WebView2 Runtime。
5. 開啟正常登入畫面，啟動器輸出 `已啟動 dev 客戶端；請在正常登入畫面手動登入。`。不會自動登入。

之後再啟動時，資源已準備好就直接跳到第 4 步；執行期仍然會從 `TW_CLIENT_ROOT` 直接讀地圖原檔、TIL 與 `Sound`，所以原客戶端目錄要一直保留在原位。

準備途中按 Ctrl+C 或關掉視窗可以中斷；已完成的項目會保留，下次執行入口時接著做。

### 4.4 只做檢查：`--check-only`

```
啟動引擎.cmd --check-only
```

只核對程式檔與已準備的資源，**不開畫面、不準備資源、不連網路**，輸出一行 JSON：

- 已準備完成：`"status":"checked-only"`、`"assetState":"ready"`，含程式 manifest 的 SHA-256、資源檔數、收據與收據 SHA-256、.NET 與 WebView2 版本；結束碼 0。
- 剛解壓、還沒準備：`"status":"unprepared"`，訊息是 `尚未準備素材；請設定 TW_CLIENT_ROOT 後執行啟動入口。`；結束碼 2。
- 準備到一半：`"status":"interrupted"`，訊息是 `素材準備尚未完成；請重新執行啟動入口以續跑。` 或 `素材準備中斷（partial）；請重新執行啟動入口以續跑。`；結束碼 2。

輸出一律帶 `"uiStarted":false`、`"networkStarted":false`、`"servicesStarted":false`。

### 4.5 登入

登入畫面之後的連線需要你**另外有相容而且獲准使用的伺服器與帳號**；本發行不附伺服器、帳號或密碼。只確認能進登入畫面時，不需要任何網路連線。

## 5. 磁碟空間與時間

| 項目 | 數字 | 依據 |
|---|---:|---|
| zip 檔 | 約 3.5 MB | T472 打包實測 3,458,059 bytes（#1057）；之後本 README 加長，略有增加 |
| 解壓後程式 | 23 檔，約 10 MB | T472 實測 10,239,010 bytes；同上 |
| 準備開始前要求的可用空間 | 1,694,498,816 bytes（1,616 MiB，約 1.6 GiB） | 程式的估計值：輸出估 800 MiB × 2（staging 與正式目錄同時存在）＋ 16 MiB 保留；不足就停止並報出需要與可用的 bytes |
| 準備完成後的 `engine\assets` | 約 0.75 GB（推測） | 地圖類約 245 MB ＋ 圖像類約 0.5 GB，由舊版素材大小推估，尚未實測 |
| 原客戶端讀取量 | 19 組容器合計 9,186,505,456 bytes（只讀） | T470 對本機客戶端的實測（#1055） |
| 第一次準備的時間 | **尚未實測** | T470 本機實跑受限未執行準備器；待 T474 真機實測後補上 |

建議：解壓目錄所在的磁碟至少留 **2 GB** 可用空間。`engine\assets` 必須與 `engine\runtime` 在同一顆磁碟（同一個解壓目錄底下就是）。

## 6. 常見錯誤與處理

失敗時啟動器輸出一行 `"status":"failed"` 的 JSON，`message` 以 `啟動檢查失敗：` 開頭；準備器本身失敗時是 `"status":"prepare-failed"`，另有 `failure` 欄位。入口在**沒有加參數**而失敗時會停在「請按任意鍵繼續」，讓你看得到訊息。任何錯誤都不會進入遊戲，也不會改用開發機或其他路徑補檔。

| 訊息（程式原文） | 原因 | 處理 |
|---|---|---|
| `TW_CLIENT_ROOT 未設定或不是存在的絕對目錄。` | `啟動引擎.cmd` 還是範例路徑、打錯，或不是完整路徑 | 依 4.2 改成原客戶端的完整路徑 |
| `TW_CLIENT_ROOT 缺少 …（…）。` | 指到的目錄少了某個 `.idx`／`.pak`（例如 `Sprite05.idx`） | 確認指的是原客戶端根目錄（`Text.pak` 所在處），不是它的子目錄 |
| `TW_CLIENT_ROOT 缺少 map 目錄（…）。` | 目錄裡沒有 `map` | 同上 |
| `錨點 …/… SHA-256 不符：…`、`容器 … 的 … 與支援版本不符（解碼後 SHA-256 …）` | 原客戶端不是第 3 節支援的版本 | 換成支援的客戶端；不要手動替換 `.pak` 裡的檔案 |
| `容器 … 沒有 …`、`Tile 缺少錨點 …` | 必要的 entry 不在容器裡 | 同上 |
| `磁碟空間不足：估計尚需 … bytes，可用 … bytes；已完成項目可續跑` | 可用空間不夠 | 清出空間後重新執行入口 |
| `已有另一個準備程序正在寫入，本次不執行` | 同時開了兩個入口 | 等前一個結束，或關掉它後重新執行 |
| `是 junction／symlink，拒絕寫入` | 解壓目錄或 `engine\assets` 經過 junction／符號連結 | 解壓到一般資料夾 |
| `staging 與 assets 不在同一磁碟`、`staging 與正式目錄不在同一磁碟，無法以改名發布` | `engine\runtime` 與 `engine\assets` 在不同磁碟 | 不要把其中一個改成連結；整包放在同一顆磁碟 |
| `素材準備已取消；已完成的項目保留供下次續跑。` | 準備途中按了 Ctrl+C | 重新執行入口即可續跑 |
| `素材準備未完成（結束碼 …）；請依上方訊息修正後重試，已完成項目可續跑。` | 準備器失敗，真正原因在上面幾行 | 依上面那行 `prepare-failed` 的 `message` 處理 |
| `素材收據或輸出不符：` | `engine\assets` 裡的檔案被改過、刪過或損壞 | 重新執行入口（會重新準備）；仍然失敗時，只刪掉 `engine\assets` 這個資料夾（不是整個解壓目錄）再執行入口 |
| `缺少 dev 產品 manifest；請使用完整交付目錄。` | 解壓不完整 | 重新解壓整個 zip |
| `缺少產品檔案：`、`產品檔案已變更：` | 程式檔缺少或被改過（防毒軟體隔離也會這樣） | 重新解壓整個 zip |
| `找不到 .NET 10 Windows Desktop Runtime；已安裝一般 .NET Runtime 不等於 Desktop Runtime。` | 只有一般 .NET Runtime | 安裝 .NET 10 **Desktop** Runtime x64 |
| `此啟動器需要 Windows x64 與 .NET 10 Runtime。` | 不是 64 位元 Windows | 換 Windows x64 |
| `找不到可用 WebView2 Runtime，HRESULT=0x` | 沒有 WebView2 Runtime | 安裝 Microsoft Edge WebView2 Runtime |
| `只接受 --check-only、--check-only --root <完整產品目錄>，或 --evidence <證據目錄>；其他參數不會啟動產品。` | 入口後面加了不支援的參數 | 不加參數，或只加 `--check-only` |

另外兩種情況沒有 JSON 輸出：

- 入口印出 `[tw-bin-engine] engine\launcher\P24.Launcher.exe not found`：zip 沒有完整解壓，或 `啟動引擎.cmd` 被移出解壓目錄。重新解壓，入口留在原位。
- 完全沒裝 .NET 10 時，Windows 會直接跳出 .NET 的安裝提示（不是本程式的訊息）。依提示安裝 .NET 10 Desktop Runtime x64。

## 7. 已知限制

以下是目前版本使用者看得到的差異，都已經過裁定；不會用伺服器資料或其他來源去補。

1. **EXP 百分比可能不準**：EXP% 與「下一級還差多少」用的是程式內建的門檻表。伺服器的門檻表不同時，這兩個顯示會不準；等級與 EXP 原值不受影響。
2. **能不能丟棄物品由伺服器決定**：客戶端不再用白名單擋，所有物品都能送出丟棄，送出前一律跳出「確認丟棄物品」（預設按鈕是「否」）。伺服器拒絕時顯示伺服器的訊息。
3. **攻擊與施法間隔是用客戶端動畫算的**：基線以外的姿勢（斧、矛、杖、雙刀、鋼爪等）與變身外型，算出的間隔可能和伺服器不同，表現是伺服器把角色拉回或擋下動作。
4. **154 個地圖特效不會顯示**：這些特效在客戶端沒有可用的動畫資料（80 張地圖、共 3,280 處），那些位置不畫特效。舊版也是如此。
5. **15 種玩家外型與姿勢的組合畫不出來**：客戶端缺完整動作（12 筆），或男妖精（gfx 138）的 3 個姿勢圖像超出邊界；這些會改用既有的替代外型。
6. **第一次見到的外型會稍晚出現**：第一次準備只先做一份常用外型；遇到其他怪物、NPC、物件或姿勢時，在背景從原客戶端解碼（只放記憶體），完成前暫時顯示替代外型。

## 8. 原客戶端與本機資料

- 原客戶端目錄只讀，程式不會改它。
- 本機產生的東西都在解壓目錄底下：`engine\assets`（資源，含 `integrity.json` 與 `prep-receipt.json`）、`engine\runtime`（staging、`prep-log.json`）。這兩處的內容來自你的原客戶端，**不要再散布**。
- 不要把解壓目錄連同 `engine\assets` 重新打包給別人；別人應該用自己的原客戶端準備。
