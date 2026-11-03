# r28 6016 host adapter r3 — 精確回覆關聯與 no-op

本隔離候選完成，待主管及獨立覆核。唯一寫入 r28-6016-host-adapter-r3；r2 封存及其他基準未修改，沒有發布正式 r28。

產品 DLL：`7762951fba9a2d37d08e2d40a34dfa7f2a6f2219b93689a031a566a8fd8f8cb8`。音訊 DLL：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`（仍是已接受 r3）。相對封存 host-r2 只有 3 檔差異：source/ClientAudioSettings.cs、source/WebPanel.cs、web/panel.js；見 delta-manifest.json / delta/ / integration.patch。模型、控制器、原生控件、生命週期與音訊 source/assets 保持逐檔一致。

## 負例來源與修復

先完整驗證 host-r2 seal `51d3e41f226b1b4507605f7b273d687888b496dd03428747275c5d9898ad46ef` 的 321 pins，確認 panel.js SHA `6101c6012d095457b3e67b2fb2531f8b7724bcfea2e56ca8af8d1319ce6f0cdb` 與 reviewer 的確定性負例一致。references/independent-review.md 與 independent-negative-dom.json 保存 F3b（連續60→80，舊60回覆清最新pending）及 F4（80→50→blur同值，host snapshot不變，pending不結束）的精確證據；不把既有134項通過當這兩項已接受。

網頁 pending 現在記錄 send 回傳的唯一 request id。任何一般 state revision 增加都不再清 pending；只有同 id 的音訊 reply 才完成本次命令。舊音訊 reply 連 generic 顯示也忽略，不能覆蓋最新狀態。未確認期間保留最新完整 gain/mute，下一次靜音不會帶回較早 gain。

宿主音訊 reply 帶不可變 PlayerAudioState 快照（使用原 reply.id）；成功接受、同值 no-op、關閉拒絕都帶回精確狀態。其他 reply 的 audio 為 null，既有處理不變。no-op 不額外 Configure，但即使 PublishWebState 因相同簽章抑制，也能靠匹配 reply 完成 UI pending。accepted 與 applied 仍分離：快照 pending／failed 就不能顯示已套用，只有真正 Configure 成功的 applied 狀態能顯示已套用。

匹配回覆時若已收到更晚的 native 設定或同設定較新 Configure 狀態，保留該較新狀態，不回退至舊快照；正常 native→web 更新保持同步。input 草稿保護、change/blur/Enter/Escape、closed立即取消並禁用皆保留。面板不建立 host，原 origin／schema／權限限制、F1/F2、有界關閉皆未改。

## 本次驗證

實際最終 DLL：宿主22 cases/77 checks、音訊設定7 cases/39 checks、Node DOM35 checks，合計151項通過；四個建置全為0 warnings/0 errors。相對前版新增 managed 精確 request/no-op/closing ack 案例5項、DOM快速連續提交/舊回覆/第三次mute/no-op無新state/failed no-op/晚到外部native等12項。所有 helper 使用同一產品 DLL，並在 finalization 核對 SHA。

managed 使用真實 product model/host/parser 與逐字 client 方法，周邊 UI/world/bridge 明示替身；Node VM 使用實際 panel.js 音訊段落與 DOM/bridge 替身。沒有 GUI、COM、裝置播放、登入、socket、服務或DB；沒有把訊息替身當成 WebView2 原生驗收。原生視窗及真正音訊仍待主管。證據 evidence/*result.json、*excerpt-pins.json；保留邊界核對在 preservation.json。

## 重建與整合邊界

在獲授權副本執行：

```powershell
python -B -X utf8 ./seal.py --verify
python -B -X utf8 ./build_test.py
```

需要既有SDK10.0.300／Windows .NET Desktop10／Node；vendor只用既有離線WebView2套件，無下載安裝。build_test.py 的既有host回歸唯讀 sibling r27 assets；移至不同層級時只在副本調整其assets路徑。不要在已封存原件重建；prepare.py 是一次性組裝腳本。

本候選含完整 source/web、已接受audio專案及音訊assets，未含整份遊戲場景或正常入口，不能作正式portable delivery。相對原r27累積整合仍需host-r1、host-r2及本r3差異；只有主管接受並另派release-integration-r12/delivery後才能串行正式整合與更新integrity/manifest/launcher。Goal完成只代表本隔離修補，不代表P24完成。
