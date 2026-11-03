# r28 6016 host adapter r2 — W1 網頁音量／靜音

已完成隔離候選，待主管及獨立覆核；沒有發布正式 r28，也沒有修改 r1、r27、r3 或主管目錄。唯一寫入根為 r28-6016-host-adapter-r2。以 r1 已封存產品 `8529b7da31512b96aa6f487cbfe2d8e86d8fde37c920df8038703d3fcb4715c5` 與 r27 原 web 為唯讀基準，起始完整驗證 r1 的 374 檔 seal。

產品 DLL：`70b873dfc2c71e11cfb125a3d073631bceb687b281c875fe095b2fe01d70e90a`。音訊 DLL：`d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`，與已接受 r3 相同。精確差異 8 檔，見 delta-manifest.json / delta/ / integration.patch。source 與 web 完整副本附於本候選；audio 與三個音訊 assets 未改。相對 r27 的累積整合仍須先套已接受 r1 delta，再套本 r2 delta；不要只套 r2 當完整 6016 實作。

## 行為

本機網頁面板新增主音量 slider／靜音 checkbox；共用 PlayerAudioSettings 同一組 gain/mute。原生控件輸入與網頁命令都進 SetAudioSettings，立即呼叫原 host.SetControls（含 gate/revision fence），並同步另一側。原生回填用 Rendering guard，不會因 ValueChanged／CheckedChanged 再 Configure；網頁 render guard 不送命令。正常顯示只有聲音設定，不暴露 transport／native 工程資訊。

AudioSettingsCommand 嚴格要求唯一 v/id/cmd/gain/muted 五欄：v=1、正 int id、cmd=audioSettings、有限 number gain 0..1、JSON boolean muted；拒絕字串數字、null、非有限值、超界、額外／重複／缺欄。原 DispatchWeb 的 LocalPage origin、32KB、版本/id gate 在音訊分支前；既有 navigation/resource/permission/download 規則未改，沒有新增 origin 或 script resource。設定操作走 LeaveGameFocus，保留遊戲 held-key 隔離。

主設定 requested state 與 transport applied state 分離：Configure 送出即 pending，只有最新 Configure 成功才 applied；失敗顯示未套用，過期成功不能蓋過新失敗。audio state 包含 gain/muted/applied/status/editable/revision/configurationRevision，完整放入既有 PublishWebState 簽章。timer 更新非同步回覆後，即使世界沒有其他變動也會推送新狀態。立即 reply 只承認收到設定；未成功前不聲稱已套用。

不新增任何面板建立 host 的路徑；已關閉或關閉中控制不可編輯。正常重登沿原 Ensure6016 使用同一份偏好。保留 F1 的 webClosing／Disposing／IsDisposed guard、F2 的 gate-before-Drain，以及單一 FormClosing await 750ms bounded shutdown。原生控件 scale 為 1%，model／web 命令仍保留合法 double，不因程式回填捨入而改寫設定。

## 已驗證

實際本版產品 DLL 重建，四個建置皆 0 warning / 0 error。原 22 cases / 77 checks 完整重跑通過，原兩份測試正文未變；UI rendering 在舊 F1/F2 excerpt harness 以明示空函式隔離，新增 W1 harness 另測實際 rendering 方法。新增 managed 6 cases / 34 checks，使用產品 parser/model/host 及 ClientAudioSettings 的逐字方法正文，周邊 controls/world/bridge 為明示替身；驗 schema、web→native、native→web、兩個控件回填不遞迴、立即 gate、focus 清除、非同步狀態簽章、失敗／亂序回覆、關閉拒絕及正常重登。

Node VM 執行實際 panel.js 音訊段落，23 checks 通過：明示 DOM/bridge 替身，驗 render 不回送、精確 payload、pending/applied/failed/closed 顯示與 disabled 不送出。合計 134 checks，沒有用 browser／GUI／COM／裝置／登入／socket／服務／DB。證據在 evidence/*result.json；來源與摘錄 pin 在 evidence/*excerpt-pins.json。preservation.json 記錄 origin、權限、原始歷史基準與生命週期修復保留。

獨立覆核 F3 已修正：滑桿 input 開始保留草稿，期間不接受 host gain 覆寫；change／blur／Enter 提交完整設定，並保護尚未確認值直到相符或較新 revision 的 host state 到達。Escape 取消並還原最新主機值；closed／不可編輯立即取消草稿與 pending，所有提交路徑不再送出。編輯 gain 時仍同步外部 mute。新增 DOM 交錯負例與 native→web 正例全部通過。原負例、來源 pins 見 references/F3-*，修前封存與 Goal 收據在 evidence/pre-F3-fix；目前結果取代該快照。這次修補只改 web，未重跑或改寫已通過的 managed 測試，產品 DLL 未變。

## 重建與後續

在獲授權的候選副本執行：

```powershell
python -B -X utf8 ./seal.py --verify
python -B -X utf8 ./build_test.py
```

需既有 SDK 10.0.300、Windows .NET Desktop 10、Node；vendor 使用既有離線 WebView2 nupkg，沒有下載／安裝。所有 cache 寫入本副本。原 host regression 唯讀 r27 assets；若副本不在相同 development 層級，調整 build_test.py 最後傳入 host-tests 的 assets 路徑。不要在封存原件重建。prepare.py／patch_web.py 是一次性歷史組裝步驟。

尚未驗證原生 WinForms／WebView2 真實點擊、鍵盤焦點、視覺排版及實際音訊。這是 source/web/音訊 assets 的隔離候選，未附全場景資產或正常入口，不能當正式 portable 版。主管與獨立覆核接受後，仍須另行派工 r12 串行整合並更新 integrity／產品 manifest／launcher pins；本階段無此授權。Goal 完成只表示本隔離 W1 修補完成，不表示正式產品或 P24 完成。
