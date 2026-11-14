# Issue #1269：分流 #1247 實機驗收的失敗項目

結論：#1247 共 10 項，其中 ✅ 8 項、❌ 0 項、⚠️ 2 項（I01、I02，都跟真正的 IME 組字有關）。兩項 ⚠️ 是同一個根因候選，合開一張程式任務 `f1247-ime-path`，再開一張重測任務 `f1247-ime-retest`，只重測 I01、I02，另外附帶回歸 C01。這輪沒有開資源任務，也沒有列已知限制。

## 做了什麼
- 讀了 #1247 的 `=== RESULT ===` 和 `docs/reports/issue-1247.md`（`origin/task/1247`，`bd22a8d6`，PR #1268 目前還沒合併）。
- 唯讀核對本機輸入法：`Get-WinUserLanguageList` 顯示 zh-Hant-TW，TIP 是 `0404:{B115690A-EA02-48D5-A231-E3578D2FDF80}{B2F9C502-1742-11D4-9790-0080C882687E}`（微軟注音，TSF）。所以「HKL 只有 0x04040404」是正常的，不是沒裝中文輸入法。這一步沒有改任何設定。
- 靜態讀碼：`engine/source` 裡完全沒有 `WM_IME_*`、`Imm*`、`ImeMode` 的處理。字元輸入只靠 WinForms `KeyPress`（`NativeUi/UiClientBridge.cs:26`）。`ReturnGameFocus()` 會把 `ActiveControl=null`（`WebPanel.cs:31`）。
- 寫了 `docs/plans/issue-1269.json`（2 張任務）和本報告；更新 #1248 的內文，並在 #1264 留言（見下方「其他排隊任務」）。

## 已通過、不重測的項目（沿用 #1247）
B01 建置、D01 有效 DPI96 PrintWindow、D02 世界 Ctrl+P、C02 DPI96 清空＋送出、C03 工具契約、S01 收尾、S02 DB 備份核對。
C01（DPI192 清空＋聊天送出）也是 ✅，但 `f1247-ime-path` 會改到聊天列焦點和鍵盤訊息處理，所以重測時附帶回歸 C136、C129 這兩個子項。

## 失敗項目對應
| #1247 項目 | 結果 | 分類 | 對應 |
|---|---|---|---|
| I01 聊天列 IME（#1211 SM12） | ⚠️ | (a) 程式（疑似）：焦點視窗的 IME context 可能被 WinForms 停用；引擎沒有處理組字窗和候選窗的位置，也沒有 IME 診斷 | `f1247-ime-path` → `f1247-ime-retest` |
| I02 建角名稱欄 IME（#1211 SM08） | ⚠️ | 同 I01，同一個根因 | 同上 |

分類理由：#1247 自己的報告寫的是「不證明引擎有缺陷」。但本機已經確定裝有注音；送 Ctrl+Space 只得到英文、open status=0，這個現象和「焦點 HWND 的 HIMC 被解除關聯」一致；而且引擎完全沒有 IME 程式碼。所以先交給程式任務靜態查清楚。如果查出 IME context 本來就是開的，程式任務仍然要補診斷 log 和組字位置，並寫明工具端還能試的方法。重測時如果 log 證明 IME 已開、但自動按鍵還是叫不出候選，就標 `needs:user`，交使用者真機確認，這時才考慮列為工具的已知限制。

## 其他排隊任務（第 5 點）
- #1248 `f1211-retest`：它要重測 45 列，不是只測 IME，所以不關閉。只有 SM08、SM12 的 IME 子項會被這次的 ⚠️ 影響，所以我把這兩個子項移交 `f1247-ime-retest`，並在 #1248 內文加上「#1269 分流更新」：前置條件追加 PR #1268 必須已合併，DPI96 用 D01 方法，IME 子項寫「移交」、不算失敗。`depends_on` 沒有改成 IME 修正任務，以免 45 列都被一個子項卡住。目前 depends_on 已經含有 #1269。
- #1264 `f1222-board-retest`：內文要求 #1247 必須已結案（它的 depends_on 沒有 1247）。我留言說明：IME 仍然 ⚠️，SM27 發文改用英數或 Unicode 輸入，報告註明「非 IME 組字」，不必停下來等 IME。

## 怎麼驗證
- 用 python 載入計畫 JSON 成功：2 項，id、stage、depends_on 都正確。
- #1248 的內文和 #1264 的留言都用 `--body-file` 以 UTF-8 送出。

## 未驗證
- WinForms 停用 IME context 的假設還沒驗證，交給 `f1247-ime-path` 靜態核對（要查 dotnet/winforms 原始碼和行號）。
- 3.81 原版 BIN 的 IME 處理位址還沒找。
- 真機組字、候選窗位置、CP950 回顯，都交給 `f1247-ime-retest`。

## 產出
- `docs/plans/issue-1269.json`
- `docs/reports/issue-1269.md`

## 給下一棒的提示
- `f1247-ime-path`：先確認聊天列開啟時、建角名稱欄取得焦點時，Win32 焦點各在哪個 HWND，以及那個 HWND 的 HIMC 狀態。ClientForm 是 partial，請新增 partial 檔，不要動 `Program.cs`。
- `f1247-ime-retest` 必須等 PR #1268 和 `f1247-ime-path` 的 PR 都合進 main 才能開跑。
