# 6016 宿主接線隔離候選

已完成可覆核候選，等待主管接受；未發布正式 r28，未動 r27 正常入口。本候選唯一寫入根為 r28-6016-host-adapter-r1。後續派工解除音訊依賴後，已核對 r3 的 54 pins 與主管收據，接入完整已接受 transport、原 WAV／manifest；目前預設 factory 為真實 AudioApiTransport.Create，不再是 unavailable stub。scope-update-r3.json 保留這次範圍擴充；原 Goal 的初始 r2-only 邊界由這份後續明確派工取代。

## 版本與交付

- 產品 DLL SHA-256：`8529b7da31512b96aa6f487cbfe2d8e86d8fde37c920df8038703d3fcb4715c5`。
- 音訊 DLL SHA-256：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`，與主管接受 r3 完全一致。
- 以 r27 `release-integration-r11/delivery` 為唯讀基準。inputs.json 中所有基準 source／seal／入口 SHA 在完成時仍一致。
- 共 24 檔最小 delta，見 delta-manifest.json、delta/ 與 integration.patch。source/ 是完整可重建產品 source；audio/ 是未改動的已接受 r3 專案；assets/ 只有原音訊 manifest 與兩個 WAV。source-pins.json 對應本次編譯來源。
- controller 兩份原 source 完全保留；接受證據 references/controller-root-review.json。音訊接受證據 references/audio-r3-root-review.json，主管 native 診斷為歷史來源，未由本工作者重跑，也未冒充最終 host 原生驗收。

## 真實宿主接點

WorldSession.Snapshot6016 在所有原 record 上先索引再交 controller 過濾。BlockSnapshot.Lifetime 是原 OriginalMapBlock reference，retained shift 保留起點。只在 CompleteMapLoad 接受分支、UpdateOpacity 完成 cache 操作後同步；舊 request 不建立 owner。新增 cache Evicting hook 在移除／新載入前立即 retire 既有 lifetime，不從半成品 cache 建立新 owner。Reset、packet206、ResetOpacity、MapFailed 都立即清除；載入例外後清空全部 actor。

WinForms 16ms timer 在 RefreshScene 前獨立 Pump；paint／ComposeMapEffects 僅讀一次 controller Frames，6016 採相同 phase，其他 effect 保持舊時鐘。無 state 的 6016 畫安靜 frame11。保持 24Hz、5tick/step、每 ID 200ms cooldown、coalesced catch-up，無事件重播佇列。可聽政策目前為已提交 cache 的 actor，沒有宣稱原 Miles 距離／pan／context 等價。

EffectHostLane 驗證單一 UI thread；音訊呼叫當下同步進 AudioPlayer facade，先 retire 再 register。非同步註冊只將後續工作排回同一 lane；TryDispatch 在實際提交前重查 generation、原 capability、時刻與 gate。每 owner 至多一個 pending request；mute／音量／world gate 變更清掉舊 pending，缺件、未設定成功、失敗與靜音仍消耗游標。所有 async 音訊回覆（包括 fault／同步拋錯／cleanup pending）有 observation；maintenance 保留原成功旗標與完整 native reply。

正常玩家「服務」面板增加「聲音」分頁：主音量與靜音。預設 gain=0.5、mute=false、32 voices；dispatch 只有配置成功且世界 renderable 時啟用。UI 顯示已套用必須等 Configure 成功；未就緒僅顯示玩家可理解狀態。僅控制本模組，不改系統／其他 app。診斷寫入既有 evidence/runs 範圍下 audio/host.jsonl，不放進玩家流程。

登出／斷線／故障啟動 750ms bounded shutdown，保留待清理 host 到 WhenReleased 完成；下一次登入建立新 host。既有 WebPanel 的唯一 FormClosing gate 實際 await Shutdown6016，再記錄 form-closing reply、ClosePanel、finalClose、Close。FormClosed 是冪等 fallback，不是唯一等待點。逾時只回 CleanupPending，不宣稱已釋放；晚到回覆仍記錄。原生永久卡住與程序終止仍不能保證清理，只有 WhenReleased 證明資源已退。

## 本次驗證與限制

最終實際產品 DLL：18 cases / 56 checks，加上 4 client regression cases / 21 checks 全通過；三個建置皆 0 warning / 0 error。測試使用明示 FakeAudio，不啟動 AudioPlayer、COM、裝置、GUI、登入、socket、服務或 DB；default factory 僅核對綁定，未呼叫。讀取真實 map4 cache，驗證 ordinal1/offset162845、commit 拒絕 stale、reset/map failure/map packet、eviction 前 fence；synthetic actor/故障與 reflection 呼叫私有 hooks 均在測試內明示。FormClosing 與正常控件的編譯／source接線核對在 host-wiring-audit.json；未假稱已操作原生視窗。

其他測試覆蓋晚到 Register 回同 lane、跨 step／clear／reload 拒絕、mute/unmute 不補播、設定回覆亂序、retire/Play 失敗觀察、deadline 後晚到 shutdown、long stall coalescing、非6016時鐘保留。第一次合成 packet206 缺少協定8byte padding 的失敗在 evidence/attempt01，已修正測試；其他中途結果保留 attempt02–06，不合計為新增 checks。已接受 controller 280/audio 116/22/owner fence 等歷史 suite 未重跑。

獨立預審 F1/F2 的原負例與來源 pins 已收錄 references/host-review-*。修正後 Ensure6016 在 webClosing／Disposing／IsDisposed 時拒絕建立新 host；正常重登仍可建立。Pump6016 先 Apply6016Controls 再 Drain，最新不可渲染 gate 在 Register 完成前同步發布。新增四個案例使用產品方法逐字摘錄及真實產品 host DLL，UI/world 是明示替身，證明關窗不重建、正常重登、不 render 時零 Play、正常 Register 成功。見 review-fixes.json 與 evidence/client-regression-result.json。獨立 reviewer 最終接受仍待主管安排。

## 可重建與後續整合

在本候選的獲授權副本執行（封存原件不再寫）：

```powershell
python -B -X utf8 ./seal.py --verify
python -B -X utf8 ./build_test.py
```

SDK 10.0.300／Windows .NET Desktop 10；vendor 內既有 WebView2 nupkg，NuGet.Config 僅離線來源，無下載安裝。build_test.py 隔離所有可寫 cache，建置 product 與 host regression。host tests 最後一個參數需唯讀 r27 assets；副本若非相同 development 層級，調整 build_test.py 的基準 assets 路徑。runtime 須部署最終 build/host 的全部依賴，不能只拿產品 DLL。

本候選不含整份 r27 場景資產／web／正常入口，不能當正式 portable delivery。主管接受後另行派 release-integration-r12/delivery，唯一整合者才可將 delta 覆到該版 source/audio/assets，重建產品、asset integrity、版本 manifest 與 launcher pins；目前沒有自行執行這一步。正常 WinForms 操作、主音量／mute、正式 host 兩音重疊、訊號／關窗清理驗收均由主管後續執行。

bridge/、references/P24.Audio.r2.dll、build/bridge 是前期封存 r2 API 相容性編譯證據，不參與最終產品。唯一最終 bridge 是 source/AudioApiTransport.cs，ProjectReference 指向 audio/P24.Audio.csproj（已接受 r3）。prepare.py、patch_host.py、import_r3.py 是歷史組裝步驟；重建以 build_test.py 為準，不重新執行組裝。
