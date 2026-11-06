# Issue #145／T074：6016 場景音效與 T075 交接

基線 `f91176d9de09279d0ecd2e885d05b64eacd03ec9`，工作樹 `scratch/wt/task-145`，分支 `task/145`。本輪完成程式核對、診斷補強與離線驗證；正式世界 MAP-F5／N05／S2 留給 T075，未宣稱真機通過。

## 做了什麼

- 核對 `ClientEffect6016` → `WorldEffect6016`／`Effect6016Controller` → `Effect6016Host` → `AudioApiTransport` → `AudioPlayer`／`AudioCore`。目前已有正確的區塊註冊、共享 200ms 同 ID 冷卻、不同 ID 重疊、世代隔離及淘汰／S206 換圖立即取消；本輪未重現須改動這些政策的功能缺陷。
- 修補驗收證據缺口：舊 `host.jsonl` 的播放／退役回覆沒有來源身分，無法對應地圖事件。現在 `register`、`play`、`dispatch`、`retire` 附 `Owner`（controller、map、block、原始 ordinal、generation）；播放／派送另附 `Candidate`（座標、音效 ID、序列與迴圈）。非同步回覆保留當次身分，不讀取後來的世界狀態。既有 Operation／Reply 欄位保留。
- 新增可重跑的 `engine/_integration/dev/effect6016-tests`，CI 加入 Windows 目標交叉編譯。只更新 dev 滾動 lock，未改封存 lock。
- 6016 是客戶端 S32 區塊尾端的本地動畫事件；本次不需新增伺服器觸發條件，未查伺服器、未改與 #140 共用的 `docs/protocol/maps.md`。

## 已驗證／怎麼驗證

| 項目 | 結果 |
|---|---|
| dev canonical build／verify | SDK 10.0.300；主程式 2 個既有 `MagicUi.cs:20` nullable 警告、0 錯誤；launcher 0 警告、0 錯誤；172 來源／181 pins |
| 新 6016 suite | 81 斷言通過：進區、保留 phase、離區、重入、實際 S206、同 ID 冷卻／不同 ID 重疊、過期註冊、靜音不補播、關閉及本機地圖解析 |
| 既有 HostChecks | 18 組／58 斷言通過；真實素材解析、原始 ordinal／File Offset、非同步載入拒收、淘汰前 fence、失敗清圖、mute/configure／shutdown deadline；目前產品與收據 SHA 相符 |
| 原生音訊 | 11 階段，各 1 秒／10 筆，僅量測測試 PID；110 筆 session 均存在、instance ID 相同；兩個 owner、兩個 ID 跨迴圈同時 4 voices，退役其中一個後精確剩 2 voices |
| 素材拒收 | `engine/tools/check-no-game-assets.py` 通過，0 錯誤；合成 WAV／manifest 僅在 scratch，未納入 git |

原生測試使用合成 440／660Hz PCM、合成區塊及手動測試時鐘；實際經目前產品 host、AudioApiTransport、AudioPlayer、DirectShow 播放。T064 的 `CreateCombat` factory 另開零 PCM 保活，沒有送任何戰鬥 Play。測得峰值：

| 階段 | 峰值 |
|---|---:|
| 進區 | 0.18249255 |
| 四路重疊 | 0.23417848 |
| 單一 owner 退役、另一路保留 | 0.23848544 |
| 離區 | 0 |
| 重入 | 0.17705746 |
| 靜音 | 0 |
| 恢復設定但尚無新事件 | 0 |
| 恢復後新事件 | 0.17705746 |
| S206 換圖 | 0 |
| 帶播放聲音的關閉前／關閉後 | 0.17705746／0 |

零 PCM 只證明 session 保活；有聲證據來自上述合成事件。關閉 scene transport 後保活仍為 0，最後 combat transport 的 `WhenReleased` 完成。未改系統或其他程序的音量。

本機 `engine/assets` 的 r28 目標為空，故測試明確指定原專案 `releases/r33/assets` **唯讀**；未改 junction、未複製素材。6016 metadata 與 audio manifest SHA 分別為 `825673b11feb6b28c6d34663cd53266d0ca2209230b1b1cfa4c0a80125263383`、`87c6cd6867e0d81bdc627c1cac0a922891d650018211b58525de9226926ad9fe`，與目前產品 pin 相同。真實 map4 冷載解析到一個 6016：`7ffd8000`、ordinal 1、File Offset `162845`、位置 `(32635,32782)`，區塊 SHA `763662559ef9a5f181cb0f80a0a805fa909a41b44d25172e4a5f38bd6f2136d3`。

NuGet 網路還原初次因 sandbox 的 Windows TLS 認證失敗，改用原專案既有 WebView2 nupkg 的唯讀來源，還原快取／暫存全在本工作樹。未修改認證或系統設定。

## 未驗證什麼

未啟動、附加或停止遊戲／伺服器／DB；未登入、連正式服務、播放真實遊戲 WAV 或做原生畫面驗收。未證明正式世界實際峰值、動態路線安全、聲音聽感、裝置拔除／切換復原，亦未宣稱與原 BIN 所有音訊政策等價。CI 交叉編譯不代表原生驗收。

## 產出路徑與重跑

- [offline.json](../../work/issue-145/offline.json)：81 斷言、本機地圖 placement／快取變更、110 筆原生取樣、帶 owner 的完整回覆。
- [host.json](../../work/issue-145/host.json)：既有 58 斷言與目前產品 SHA。
- [build.json](../../work/issue-145/build.json)：建置命令／stdout／stderr、收據、來源與唯讀素材 SHA。
- 新測試入口：`engine/_integration/dev/effect6016-tests/run.ps1`；詳細參數見同目錄 README。先建目前 dev，傳 `-Native -AssetsRoot <有效本機assets> -ConfigFile <NuGet設定>`；產物在 `scratch/issue-145/result.json`。
- 既有 HostChecks：建 `engine/_integration/check-template/tests/HostChecks.csproj` 時帶 `-p:ProductDll=<目前dev DLL絕對路徑>`，執行 DLL 參數 `<scratch輸出> --assets-root <本機assets> --product-receipt engine/_integration/dev/receipt.json`。不得把過往 DLL 或缺檔 skip 當作本輪 PASS。

## 給下一棒的提示：T075／dot 真機核對

1. Claude 審查、使用者合併後，由本機執行器建置同版 dev，保存 DLL／manifest SHA 與 check-only；先處理測試環境有效 assets 路徑，不改封存或拿本輪獨立宿主代替遊戲。dot 僅用已授權 P24Lab `42061／43324` 測試角色，不碰正式服務。
2. 在 map4 港口 `(32635,32782)`（或相鄰 `(32634,32782)`）停留 **至少 10 秒**，主音量 50%、不施法／攻擊。保存自身座標、快取 slots、own PID／session instance、`audio/host.jsonl` 與同期 combat log；每 100ms 取樣。預期至少三個新迴圈有 `Candidate.SoundId=3332 或 1340`、`Owner.Key.MapId=4／BlockKey=7ffd8000／RecordOrdinal=1` 的成功播放與峰值 > 0。週期 2.5 秒，第一事件約 209ms；成功提交本身不是可聽證據。
3. 靜音後先等 **0.5 秒** 清除裝置管線，再連量 **10 秒**：session 存在且峰值 0。解除靜音後連量 **10 秒**：下一新迴圈恢復 > 0，不補播靜音期間事件；instance ID 不變。若 session 缺失，必須記缺失，不得轉成 0。
4. 離區要看 **快取淘汰**：6016 以載入的 2×2 區塊為生命週期，走出畫面或跨過單一世界格不一定退役。離線從上述冷載位置向西推進，在 `(32596,32782)` 仍保留 1 owner，至 `(32532,32782)` 淘汰為 0；這是快取計算，**不是已驗證的安全行走路線**。港口有歷史遇怪死亡紀錄，dot 應選實際安全可達路徑；以 `retire` 的相同 Owner、成功回覆及剩餘 voice 為準。退役後等 0.5 秒，再量 10 秒應為 0 且無舊 owner 新 Play。
5. 返回後確認同 key 使用更高 generation、10 秒內有聲；再透過既有合法換圖流程離開 map4，記 S206 UTC，換圖退役後同樣等 0.5 秒、量 10 秒為 0。保持無其他音效，否則分離 combat log，不能用整個 PID 的其他聲音誤判 6016。
6. 此港口冷載只有一個 6016，真實 WAV 長度／隨機候選未必產生重疊。只有 `AudioReply.RemainingVoices > 1` 且不同 Playback／Owner 或跨迴圈實際並存，才能標真機重疊通過；未遇到就記未觀測，不能用本輪合成四路重疊替代。正常關窗核對 shutdown／released 與程序音訊資源釋放。

`docs/REPORT.md` 未修改；不新增伺服器資料或遊戲素材。GitHub PR／CI 的最終交付結果由本輪 RESULT 與 PR checks 提供。
