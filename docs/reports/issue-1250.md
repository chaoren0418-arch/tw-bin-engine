# Issue #1250：相鄰 fixture、PC 點選、loopback 與 SM22／SM35／SM51 重測

**結論：部分通過（✅ 27／❌ 3／⚠️ 0）。** 以本輪 30 個子步驟計數，沿用列不計。兩項工具失敗、一項引擎 6016 首次進入無聲；交易、隊伍、一般 BGM／技能／音量歸零及全部收尾通過。失敗未自行修程式。

實機時段：2026-11-14 約 20:23–21:07（台北）；同步編譯及交付另計。只使用 P24Lab 42061／43324 的隔離副本與測試角色，不設 GM、不手動寫 DB。正常登入、交易、步行、技能與購票造成的遊戲寫入僅在副本。沒有連正式 2000／3306、操作原 BIN、修改秘密設定或系統設定。

## 前置與版本

- #1249（f1229-adjacent-fixture-durable）的 PR #1273 已合入 main；使用 `work/f1186-smoke/adjacent-pcs.json`。fixture 為 `D:\lineage開發\TW1608262200研究\environments\p24\backups\issue-1249-adjacent-fixture\db-data`；manifest SHA-256=`4c4e40886102e9165294f2d28e0f0af8349ecbd36db1587612106395171d080c`。1722 檔逐檔核 SHA 後才複製至本 worktree 的 `scratch/issue-1250/lab/db-data` 開服，未從任何其他 task 副本開 DB。
- `lease-before-start.json` 核實 holder=`chaoren0418-arch/tw-bin-engine#1250`、exp=`1794669793.6027696`（2026-11-14T15:23:13Z，秒以下精度略）。原專案僅讀取；fixture、持久 DB 都先留完整逐檔 hash 清單。
- 真機來源 main `ef7b6fa40afd0c24d2a6aaf4bddcdb9c75975e1f`；product SHA-256=`1cb04035a55c3a82bae1d813493a8c0473eec82428849452cbb7b52d9a066046`，audio=`4c4aa97837e7c4e610d3e0b75eb2b619faf41508096435e37c635c2cec456473`，launcher=`8a912ce49d40a0e741a9a2c54eab255a490da53c78bcb9ba413e3f52717d1d33`。DPI 192，客戶區 800×600。
- #1210 product=`2a96bc10e5ec58d959b45d870c656fd5faf2fce020c6419115a4ad89f6055182`，本輪不同；比較來源 `b3888c8e01fc9039bc0916a7b1d782369ce0b7da..ef7b6fa4` 的 43 筆非 merge commit 完整 SHA／主旨見 `between-commits.txt`。主要產品改動：`6d234ace` 原生選角、`acf93457` 職業5/6技能窗、`086a33e3` 選角美術、`3df93233` 外觀 fallback；工具另含 `8ca9312d`／`7b7cc405` 的 #1230 session 判定。
- 建置指令：`powershell.exe -NoProfile -ExecutionPolicy Bypass -File engine/build-product.ps1 -ConfigFile .github/nuget.ci.config`；設 `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、`DOTNET_NOLOGO=1`，暫存與 NuGet 在 worktree，temporary lock 模式。首次直接執行 ps1 被執行政策擋住、尚未建置；改用上述命令成功。實驗伺服器只啟動一次。
- 測試期間 main 合入 #1271 IME；收尾後 fast-forward 同步 `7753f3d7f5f2f56890474ec418bfff896b741c80` 並重建成功，product=`32228bd34096f178ed248d0e50efd991654ccf95372af0f75db71dde5173e499`。同步後 `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release ... -- . scratch/issue-1250/ime-checks.json ime` 通過 361 項純規則檢查（含基礎檢查），不是新版真機驗收。沒有重跑本輪已通過的真機列。
- #1229 P00 沿用，本輪改以副本複製核對；P01/P02 是 #1249 的準備動作，不重做。#1210 R02/R03/R04/R05/R08/R09/R11 沿用，不另測。#1235 M01 已通過，因此本輪有執行 map4／6016，沒有以 fixture 阻塞跳過。

## 逐項結果

證據根：`D:\lineage開發\TW1608262200研究\scratch\wt\task-1250\scratch\issue-1250`。下表路徑均相對於此根。完整 124 檔路徑、大小與逐檔 SHA-256 在 `evidence-manifest.json`，其 SHA-256=`fdf2a2ebda891af36878468c85b16bcded8a6de95acf5477fb31310e6def4f7d`；PNG／WAV／log／DB 不進 git。所有畫面為 GDI 實際客戶區截圖，游標為 GetCursorInfo 擷取；未用 WGC／computer-use。

| 編號 | 結果 | 實際觀察／目視結論 | 重現步驟 | 證據路徑（SHA 見上述鎖定索引） |
|---|---|---|---|---|
| P03-1 | ✅ | 副本開服後 SELECT：A=10334/map0/32579,32974；B=1900240001/map0/32580,32974，完全符合持久 fixture。 | 僅連 127.0.0.1:43324 唯讀 SELECT。 | `positions-before.tsv`、`preparation.json` |
| P03-2 | ✅ | 兩人登入同框，Chebyshev 距離 1；當時未移動、未改位置。 | 兩個原生客戶端登入指定測試角色，保存 login-state 與 GDI 圖。 | `client-A2/p03-sameframe.png`、`client-B/p03-sameframe.png`、`client-A2/p03-state.json`、`client-B/p03-state.json` |
| P03-3 | ✅ | 目視周圍無牆、無 NPC 重疊；原服 map0 格資料亦確認兩人各自 3×3 通行，位置為權威讀回。 | 核對快照、伺服器 map0 唯讀格資料與畫面。 | `p03-check.json` |
| R01 | ❌ 工具 | 首跑 concurrent-truncate-and-read 的 ConvertFrom-Json 在 40 次讀取重試後失敗，exit 1；未修碼，新程序複驗全部 22 契約通過（含 session 分類）。不能以複驗抹掉首跑不穩定。 | 執行 Test-SmokeTools.ps1；完整指令與首跑觀察見 observations.json。 | `observations.json`、`contracts/shared.json`、`contracts/results.json`、`contracts-confirm.log`、`contracts-confirm/results.json` |
| R06-A | ✅ | A 經 Select-SmokeObject 點 B，稍後唯讀快照 targetId=1900240001；中間無第二次指標輸入。 | A 呼叫 Select-SmokeObject -ObjectId 1900240001 -Zoom 1.5，再單純讀回。 | `client-A2/r06-before.png`、`client-A2/r06-after.png`、`client-A2/r06-select.json`、`client-A2/r06-current-state.json`、`client-A2/r06-body-select.json`、`client-A2/client-input.jsonl` |
| R06-B | ✅ | B 點 A，Selected=true，AfterTarget=10334。雙向實際 targetId 均確認後才執行 SM22／SM35。 | B 呼叫 Select-SmokeObject -ObjectId 10334。 | `client-B/r06-before.png`、`client-B/r06-after.png`、`client-B/r06-select.json`、`client-B/r06-state.json` |
| R06-T | ❌ 工具 | A helper 預設 5 秒內回報 Selected=false／AfterTarget=null，但後續無新點擊即讀到目標；再呼叫在送輸入前以 Already selected 拒絕。存在等待／快照時序假陰性。 | 同 R06-A；比對 select JSON、後續快照與輸入紀錄。未改工具。 | `client-A2/r06-select.json`、`client-A2/r06-current-state.json`、`client-A2/r06-body-select.json` |
| R07 | ✅ | 正式最終錄音 4.99 秒、239520 frames，RMS／Peak／OwnMaxPeak 皆 >0，無拒錄；otherSessions 全表列於下方。 | A3 播放聲音時 Record-SmokeLoopback -Seconds 5。首拒後已等待超過 5 分鐘。 | `client-A3/r07-final.wav`、`client-A3/r07-final.wav.json`、`client-A2/r07.wav.json`、`client-A/forced-cleanup.json` |
| SM22-1 | ✅ | A /trade；B 出現 Y/N，按 YES 後兩端交易窗都出現。C2、C121 與 S52 可追蹤。 | 相鄰面對面，A /trade，B 點 YES。 | `client-A2/sm22-invite-send.json`、`client-B/sm22-prompt.png`、`client-A2/sm22-both-open.png`、`client-B/sm22-yes-click.png`、`client-B/sm22-answer-trace.json` |
| SM22-2 | ✅ | A 放入藥水 1 瓶、金幣 10；B 的對方欄顯示相同項目。 | 背包選物品後放入交易欄，數量分別輸入 1、10。 | `client-A2/sm22-offer-final.png`、`client-B/sm22-peer-offers.png` |
| SM22-3 | ✅ | 雙方分別確認後交易窗結束。 | A、B 依序按交易確認。 | `client-A2/sm22-own-confirm.png`、`client-A2/sm22-complete-inventory.png`、`client-B/sm22-complete-inventory.png` |
| SM22-4 | ✅ | 背包：A 藥水20→19、金幣8000→7990；B 藥水0→1、金幣144→154。 | 對照交易前後 login-state.inventory。 | `client-A2/sm22-complete-state.json`、`client-B/sm22-complete-state.json`、`verified-summary.json` |
| SM35-1 | ✅ | 無名 /invite 後實際游標由手形換瞄準圖樣（游標8），點 B 後恢復；保存 GetCursorInfo 原生游標截圖。 | 輸入 /invite、不帶名字，移到世界區觀察游標後點 B。 | `client-A2/sm35-cursor-normal.png`、`client-A2/sm35-cursor-pick.png`、`client-A2/sm35-cursor-after.png`、`client-A2/sm35-cursor-pick.json` |
| SM35-2 | ✅ | 點 B 後送 C230；B 出現邀請 Y/N，YES 送 C121 並建立隊伍。沒有用普通 targetId 契約代替邀請判定。 | 點選 B，B 按可見 YES 按鈕。 | `client-A2/sm35-invite-trace.json`、`client-B/sm35-invitation.png`、`client-B/sm35-accept-trace.json` |
| SM35-3 | ✅ | 隊伍頁顯示對方姓名／血條，名字旁可見隊伍圖示。 | 開隊伍頁並把滑鼠移到隊員。 | `client-B/sm35-party.png`、`client-B/sm35-party-state.json`、`client-B/sm35-name-icon.png` |
| SM35-4 | ✅ | B 離隊送 C33，隊伍欄清空。 | B 按隊伍頁離開。 | `client-B/sm35-left.png`、`client-B/sm35-leave-trace.json` |
| SM35-5 | ✅ | 重新邀請後 A 選 B 踢除，送 C255；B 隊伍欄清空。 | 再次邀請並接受，隊長選隊員按踢除。 | `client-A2/sm35-kick.png`、`client-A2/sm35-kick-trace.json`、`client-B/sm35-kicked.png` |
| SM35-6 | ✅ | 再次建隊後委任先出確認；字串表缺 4051 時畫面顯示 fallback『要委任透明王子 為組隊隊長嗎?』。回 Y 前追蹤沒有委任 C230。 | A 選 B，按委任，先截圖與封包標記。 | `client-A2/sm35-delegate-prompt.png`、`client-A2/sm35-delegate-before-y-trace.json` |
| SM35-7 | ✅ | 確認列輸入 y 並 Enter 後才送 C230；B 取得隊長委任操作，能對 A 顯示反向委任詢問，隨即 Esc 取消。 | A 回 Y；B 選 A 按委任確認權限，不再送出反向委任。 | `client-A2/sm35-delegate-typed-y.png`、`client-A2/sm35-delegate-trace.json`、`client-B/sm35-new-leader-proof.png` |
| SM51-1 | ✅ | 音樂100、效果0、環境0；普通技能觸發後取得 BGM 非零波形。 | Ctrl+V→環境頁設定類別音量，施放技能後錄5秒。 | `client-A2/sm51-music100-options.png`、`client-A2/sm51-music100.wav`、`client-A2/sm51-music100.wav.json` |
| SM51-2 | ✅ | 音樂0、環境0、效果100；光箭 C6 與 S30／S33 時段錄得技能非零波形。 | 選光箭，世界區聚焦並按 M，錄音覆蓋施放時間。 | `client-A2/sm51-spell-key-after.png`、`client-A2/sm51-spell-key-trace.json`、`client-A2/sm51-skill-key.wav`、`client-A2/sm51-skill-key.wav.json` |
| SM51-3 | ✅ | 音樂100→0，其餘類別維持0；RMS 0.000828632→0，Peak 0.00848219→0。 | 同一次會話在選項窗把音樂滑桿拉到0，再錄5秒。 | `client-A2/sm51-music0-options.png`、`client-A2/sm51-music0-state.json`、`client-A2/sm51-music0.wav`、`client-A2/sm51-music0.wav.json` |
| SM51-4 | ❌ 引擎 | 6016 首次接近：A 在 map4 (32634,32781)，音源 (32635,32782)，效果100／音樂0／環境0、未勾靜音，仍錄到 RMS=Peak=OwnMaxPeak=0；host 同期持續 dispatch／StartedWavFallback。不能把已送播放命令當成有聲。 | 新會話於船上先設音樂0、效果100、環境0；正常到 map4，步行至音源旁，再錄5秒。見近處圖、設定圖、位置與 WAV。 | `client-A3/sm51-6016-options.png`、`client-A3/sm51-6016-near-record.png`、`client-A3/sm51-6016-near-record-state.json`、`client-A3/sm51-6016-near.wav`、`client-A3/sm51-6016-near.wav.json`、`client-A3/audio/host.jsonl` |
| SM51-5 | ✅ | 同一位置先切效果0／環境100，再切回效果100／環境0；後者錄得 RMS=0.000396742、Peak=0.0112700。只證明重新套用後恢復，不消除 SM51-4。 | 原生環境設定頁重新套用類別音量，再錄5秒。 | `client-A3/sm51-6016-effect100-options.png`、`client-A3/sm51-6016-effect100-state.json`、`client-A3/sm51-6016-effect100.wav`、`client-A3/sm51-6016-effect100.wav.json` |
| SM51-6 | ✅ | 維持相同音量走到 (32616,32770)，離音源 Manhattan=31，錄音 RMS／Peak／OwnMaxPeak 都降至0。 | 正常步行離開聲音範圍，等待2秒再錄5秒。 | `client-A3/sm51-6016-exit.png`、`client-A3/sm51-6016-exit-state.json`、`client-A3/sm51-6016-exit.wav`、`client-A3/sm51-6016-exit.wav.json` |
| SM51-7 | ✅ | 不再調音量，步行回 (32634,32781)，錄音 RMS=0.000806552、Peak=0.0216675 恢復。確認恢復後的進出對照。 | 只走回音源附近，等待2秒再錄5秒。 | `client-A3/sm51-6016-reenter.png`、`client-A3/sm51-6016-reenter-state.json`、`client-A3/sm51-6016-reenter.wav`、`client-A3/sm51-6016-reenter.wav.json` |
| P04 | ✅ | 最終 SELECT 共24角色，AccessLevel=0、OnlineStatus=0 全數成立。A 最終 map4 (32634,32781)，B map0 (32580,32974)，皆為副本內普通遊戲操作結果。 | 關客戶端後、關 DB 前，在43324唯讀查全角色。 | `positions-final.tsv` |
| P05-1 | ✅ | A2、B、A3 正常關窗；首個無主窗的自啟 A 依已記錄 PID／建立時間清理。Java、MySQL 及已知衍生程序均結束。 | 對本輪 Process 實例 Close-SmokeClient；關閉自啟伺服器、mysqladmin shutdown，再依 PID／親子鏈核對。 | `client-A/forced-cleanup.json`、`client-A2/close.json`、`client-B/close.json`、`client-A3/close.json`、`runtime-shutdown.json`、`final-check.json` |
| P05-2 | ✅ | 42061／43324 均無 TCP Listen。 | Get-NetTCPConnection 僅篩選兩個測試埠。 | `final-check.json` |
| P05-3 | ✅ | fixture 1722檔／279512787 bytes；持久DB 1722檔／279513913 bytes，前後逐檔 SHA 完全一致；fixture manifest SHA 也未變。 | 收尾重新逐檔 hash，和開跑前清單逐項比對，不啟動原目錄。 | `fixture-before.json`、`fixture-after.json`、`persistent-before.json`、`persistent-after.json`、`source-readonly.json` |

## 錄音數值與 session 範圍

時間為 UTC；台北時間加 8 小時。WAV 為 48000 Hz、雙聲道。

| 錄音檔 | 開始時間 | 秒／Frames | RMS | Peak | OwnMaxPeak | WAV SHA-256 |
|---|---|---|---|---|---|---|
| `client-A3/r07-final.wav` | 2026-11-14T12:48:35.2715007Z | 4.99／239520 | 0.0002559845971 | 0.001795532764 | 0.02723695152 | `9f85f9252d92f1342aff500bf3eac6360564305736f2bd26d4ec22706123f4b1` |
| `client-A2/sm51-music100.wav` | 2026-11-14T12:44:53.7094776Z | 5.01／240480 | 0.0008286318238 | 0.008482189849 | 0.1286687702 | `d73c9f55591c769a8b07b79eb11d2c4a8303de79e5c4792b91e0556c2b75ea35` |
| `client-A2/sm51-music0.wav` | 2026-11-14T12:45:00.7860119Z | 4.99／239520 | 0 | 0 | 0 | `7751a328d6e0ca666659aad0f25e4beb7d453d28b223e224278d35010809c079` |
| `client-A2/sm51-skill-key.wav` | 2026-11-14T12:43:12.1811510Z | 4.98／239040 | 0.001151569533 | 0.01895727403 | 0.2875683308 | `0aee9fa42174fd0e8b681d88e897ccb07a3465efbc8e1ef8ec0ff80a46ca6a72` |
| `client-A3/sm51-6016-near.wav` | 2026-11-14T13:03:05.1014163Z | 4.99／239520 | 0 | 0 | 0 | `7751a328d6e0ca666659aad0f25e4beb7d453d28b223e224278d35010809c079` |
| `client-A3/sm51-6016-effect100.wav` | 2026-11-14T13:04:44.5157343Z | 4.99／239520 | 0.0003967418781 | 0.01127001457 | 0.1709580868 | `10fa0569c68267e63686459e34f4adba10f943fed0e0b0ff36cfdffd06069a42` |
| `client-A3/sm51-6016-exit.wav` | 2026-11-14T13:05:50.2058559Z | 4.98／239040 | 0 | 0 | 0 | `d9164975c63d2c98266433a7827c6a0032ca44228fb3c5654c8ceb006c544047` |
| `client-A3/sm51-6016-reenter.wav` | 2026-11-14T13:06:25.7939638Z | 4.97／238560 | 0.0008065523449 | 0.02166745812 | 0.3286798894 | `574b8b23a520b9452ce86d684daa336d5872b2f9e7c0a942baece9f8606da4b6` |

R07 正式最終錄音的 `OtherSessions` 全部兩列如下；完整 session Key／EndpointId 保存在同名 JSON。

| PID／名稱 | Kind／State | Verdict | Polls | MaxPeak | UnmeteredPolls／HResult | CrossProcess／SystemSounds |
|---|---|---|---|---|---|---|
| 14456／P11.ClientEngine | process／1 | other-active-silent | 158 | 0 | 0／0 | False／False |
| 17352／powershell | process／1 | capture-host-ignored | 157 | 0.001795532764 | 0／0 | False／False |

- 使用預設 render/eMultimedia 端點 loopback，約20ms輪詢、門檻0.0001；不是程序隔離，不能排除輪詢間隙音訊。正式 R07 有1次 discontinuity、0次 timestamp error，波形長度4.99秒。
- 12:31:20 首錄因 PID2520 有聲而拒錄；它是本輪起初隱藏啟動、未取得主窗的 A，核對建立時間後只清理該自啟程序。B 用遊戲內靜音，未關任何他人程式。12:36 的診斷重試距首拒只有297.247秒，不算滿5分鐘的正式重試；採用12:48:35的最終錄音，距首拒超過17分鐘。
- 6016 真實位置由 map4 `7ffd8000` 的效果資料讀回為(32635,32782)，音源1340的 `dispatch/play` 與 WAV 時段對照；沒有把 `StartedWavFallback` 單獨當作聽見聲音的證明。場景內仍有其他音源的可能，因此結論是既有隔離類別設定下的實測波形與進出對照，未聲稱逐音源分離。

## 失敗與限制：已驗證／推測／待驗證

- **已驗證（R01）**：首跑 exit1，錯誤 `Unterminated string passed in. (5): {"sta`，位置 `SmokeTools.ps1:22`；`contracts/shared.json` 僅5 bytes、`results.json` 空檔。首跑原始主控台未另存，`observations.json` 明確標為執行器依工具回傳轉錄，不能視作原始 log。複驗未修改腳本，22項全過；是否排程／寫入競態造成仍待修正者確認。
- **已驗證（R06-T）**：預設5秒等待回報未選到，但之後唯讀快照 targetId 正確；下一次呼叫在送輸入前拒絕 Already selected。點擊時間12:30:25.523Z，之後直到12:31:51的轉向鍵都沒有另一筆 A 指標輸入。**推測**是等待或發布時序；未定位根因。
- **已驗證（SM51-4）**：第一次抵近、效果音100且未靜音時錄音為0；同位置重新切換類別音量後恢復，離開再回來也恢復。**推測**與首次套用／場景切換後的音量更新有關；仍需新會話冷啟路徑重現，不把後來成功列當作此項通過。分類為引擎聲音路徑現象，根因未證實。
- **操作限制**：A2 暫存 runner 的 `$deadline` 與步行 job 變數同名，造成正常關窗；僅改本張 scratch wrapper 變數名，以 A3 接續未完成的港口路線，未改產品。第一次技能準備只選光箭未送 C6、第一次 BGM 採樣正逢無聲、曾誤停在選項常用頁，這些準備紀錄保留但未拿來判產品失敗；正式通過證據使用正確環境頁及實際 C6 時段。
- **已驗證（封包）**：交易邀請 C2 已送，回覆在 B；此輪開交易窗收到 S52，未以 helper 預設期待的 S219/S88 推定無回覆。/invite 在選對象前不送 C230，點 B 才送；委任回Y前沒有委任封包。沒有任何列被武斷分類成「伺服器不支援」。
- **待驗證**：同步後 #1271 的新版 DLL 未做這張真機清單；本輪證據只屬前述 ef7b6fa4。完整 SM51 的 MIDI、其他地圖事件等不在本張限定範圍，不擴張通過宣稱。

## 收尾與下一棒

- fixture 前後清單 SHA=`38c0ab7a9a46e9045a5749abea6ccc239bde6f8f8fa50b66f81661886d5a3a6e`；原持久 DB 前後清單 SHA=`44316ea5763d2d963a08cdc3e0fd2755c58fa9559da839977cbf9f5115c286a7`，各1722檔逐項一致。原持久 DB 位於 `D:\lineage開發\TW1608262200研究\environments\p24\db-data`。
- 沒有手動 DB 修改，故無手動還原 SQL；所有遊戲改動留在本張 scratch 副本。沒有刪除任何目錄、拆除 worktree 或 junction。自啟 runtime PID／建立時間及最終無監聽證據保留。
- 調度器下一棒只需分流三項失敗：R01 JSON 讀取穩定性、R06 helper 等待／快照假陰性、SM51-4 冷啟後6016首次有聲。修正後只重測這些失敗與必要對照，沿用本輪 P03、交易、隊伍、一般錄音及音量歸零結果。真機重測仍需新的 fixture 隔離副本，不能直接啟動原 fixture。
- 本 PR 只交付此報告；產品、helper、`docs/REPORT.md`、`rebuild-lock.json` 均無本任務修改。證據根與 hash 索引供本機下一棒唯讀取用。

## 主要證據 SHA-256

| 路徑（相對證據根） | SHA-256 |
|---|---|
| `verification.json` | `52119dd9465af3f7875025ae9612399fbe8cbaf0f0bb8a6eaf50e4d449bb151b` |
| `preparation.json` | `821ac326777912222de2845f777f88d42b01b937fab507a1672a028ab6f84673` |
| `positions-before.tsv` | `3313e74f5dbd8a6a8ddca530cd26ab46afe15ff001000c438f18f1262d4fc932` |
| `p03-check.json` | `51c0cd9a5d8e67565ff5692ba0fe1b2b07c21cb9bca2c95f3357a76978b33a14` |
| `contracts-confirm/results.json` | `8681b8ed96304affb8f674c29067f0b422d1f74ded49dd51a37beb002e69aab6` |
| `client-A2/r06-select.json` | `379075ef01e63b04c50164dded46f0c99a005dd2009d00032c2d3edb050c91c7` |
| `client-A2/r06-current-state.json` | `31abd7c23cf8c926dcd5111fdcb4120af09d987d6a4f739d4f12e3473e030357` |
| `client-B/r06-select.json` | `8a4dcca777c4470f2e854f7c7300ae0ecc09d2d806e29336efeedb3e2437aa0f` |
| `verified-summary.json` | `85d9892208ecf3e7c6df6d4b9e1b972d033127fa98c919db4d60ac77224708f4` |
| `positions-final.tsv` | `82e373a28737894aecfc2d091f8c5c329f90b0f4e35f417ad19aee090f0999c2` |
| `runtime-shutdown.json` | `184df3960e14e9a5a2e6027a42e606916fa9c777abcb35953baae3020da444d2` |
| `final-check.json` | `a1e36e8adefd83d83a9b75cb563446342b9aad3d60eccbcef67893abd7c17b92` |
| `source-readonly.json` | `96df707208619f56b2a7a1e292ff2cb5e6118ca3e7b8d2f8fd807941b154b1ff` |
| `build.log` | `a7bf6c78bcc99df28a01728563df68334e82fc6b9f642eaeebc6fa24c42baf8e` |
| `between-commits.txt` | `4d73712063a5889fc124df3629442d4a82c5b5f72c5aa6b77316c1a463b4d9e9` |
| `build-synchronized.log` | `8003b3817b641810716e7eef602353f754e39d89adbefce8641070b4a4e41f58` |
| `ime-checks.json` | `22552d32ea229716ef329849e50cc29eea520c25c2b51baff304185412c27678` |
| `client-A3/audio/host.jsonl` | `33e371ea4e8f08ad8df19444fd540e79b559baacca93d741ad8e5b25c511f35f` |
