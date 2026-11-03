# P27 UI／輸入離線查核

本階段交付 bounded review，未找到在本次檢查範圍可確切重現的實作缺陷，因此沒有產品修補候選或 patch。不得據此推論完全沒有缺陷。共享 engine、release、其他 work 與主管資料均未修改；Ground／WorldEntry 拾取範圍未變更。

基準為現行 r33：`base-pins.json` 171 個 source/web 檔均與正式 r33 seal 相符。Seal SHA256 為 `846302448dc4a1421a2689b96632978353b6e39b1c9f8a9331bdd23b7760a417`。實際執行的既有 r33 DLL SHA256 為 `8ed92b8be9e532198e0f5bc88e404b47a3601f0e98e0962a0e1820571090d0c2`，也與 seal 相符。本次只建置離線測試 harness，沒有重建產品。

- `input-routing-map.json`：11 條輸入／焦點路由，每條附實際 source 行號、SHA、閘門、測試界線及 native 缺證。
- `gap-evidence.json`：R01/R02/R16/R20/R21/R22/R23 及 A1/A2/C1 對帳，保留 r27/r28 歷史版本；未把缺原生畫面／滑鼠證據視為 bug。
- `historical-focus-evidence.json`：舊 native-r28 焦點報告的 12 個 leaf pins 已核驗；八方向紀錄是產品預測且 `successAck=false`，Web notes 的 Down/U 隔離有歷史記錄，均不升格成 r33/r34 驗收。
- `evidence-csharp.json`：8 組／182 個斷言，執行封存 r33 DLL 的純規則與模型。未建立 Form、Control、LoginConnection 或 socket。
- `evidence-web.json`：6 組／55 個斷言，執行原 JS，以明示記憶體 DOM／bridge 替身檢查命令建構、pending 與焦點 stop 訊息；沒有瀏覽器或 host dispatch 執行。
- `native-acceptance-plan.json`：12 組同版驗收準備，包含版本 pins、實際視窗 geometry、輸入順序、焦點、HUD、聊天、Web、權威回報。此檔僅為計畫，執行數 0。
- `source-pins.json`／`artifact-pins.json`／`verification.json`：來源、交付與一致性核驗。`progress.json` 保留真實 Goal／進度，不以檔案冒充平台狀態。

既有 MovementUiTests 需要視窗、WebView2 與 socket／伺服器；InventoryLayoutTest 雖使用合成背包，仍需要 GUI。兩者本輪僅讀取、沒有執行。前者明示控制器／DOM 呼叫不等於外部原生事件；後者不能當作真實道具消耗或滑鼠驗收。規則測試無法確認 OS 事件路徑、IME、實際 DPI／像素、非同步 sender／服務端行為。

重跑（僅寫本目錄、NuGet 來源清空，不連服務）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\lineage開發\TW1608262200研究\work\P27-ui-input-candidate-r1\rerun.ps1'
```

只核驗交付：

```powershell
python 'D:\lineage開發\TW1608262200研究\work\P27-ui-input-candidate-r1\verify.py'
```

若基準已改版，重跑會在輸入 pin 驗證處停止，須主管指定新 exact base。不要將 r33 離線結果升格成 r34 native 接受。沒有候選需合入，也沒有要競寫 r34 拾取的產品檔案；主管可獨立覆核本報告，後續由具授權的操作者依實際 capture／geometry 補同版原生證據。服務／DB 由使用者處理；不為驗收改資料或走已失敗的危險路線。

測試工具首輪曾因 harness 相對 DLL 路徑多一層而編譯失敗，修正後通過；這是測試架設錯誤，並非產品缺陷。首次 .NET CLI 輸出首次執行開發憑證訊息；未要求憑證管理，後續及重跑已設停用憑證產生與 PATH 修改。沒有執行 GUI／遊戲／角色／DB／服務／socket 操作。

本階段離線交付完成不代表全案、原生輸入、完整畫面或 P24 gameplay 驗收完成。
