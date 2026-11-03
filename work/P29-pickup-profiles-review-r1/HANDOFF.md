# P29 拾取 profiles 獨立審查入口：準備階段交接

狀態：**harness-ready；candidate-runtime pending**。唯一可寫目錄是本目錄。未讀取 P28 未封存候選、未 build／載入／執行候選，也沒有合入、入口切換、GUI、socket、原生遊戲或服務操作。

正式入口仍 r33。離線 final baseline 使用主管的 `r34-final-offline-baseline.json`，SHA `ac4254ea339ed35c4257e27e955f0411c617f21698a1da5bb59330a162933024`；已驗其 authority、現行 AGENTS、release seal、source manifest、180 個封存來源檔及 r34 product DLL。歷史 prep AGENTS pin 不改寫，另綁既有 policy-drift proof。

## 本次實際完成

- 獨立 stdlib PNG parser 讀取封存 416 個 body-only PNG，確認解碼 RGBA、尺寸與封存 expected 一致。沒有重新製作圖檔。
- 對封存 2,688 筆 mapping 做 relational join，覆蓋 42 exact pairs × 8 directions × 2 speeds × 4 metadata indices；另讀 14 families、294 duration rows。這些是 parser／adapter 資料檢查，不是候選 runtime，也不是重跑歷史 1,118 斷言。
- C# harness 以已封存 r34 DLL **只作編譯參照**。初版與新增時間／replacement adapters 後的最終版均編譯成功，未執行產品。最終原始命令與二進位證據在 `harness-build-final`。
- 5 項 harness gate／raw-byte 自測通過：錯誤主管路徑、錯誤 SHA、錯用 baseline 當候選授權、缺少授權檔都拒絕；無效 UTF-8、NUL、CRLF 的 stdout／stderr bytes 原樣保存。

`coverage-gaps.json` 是實際覆蓋邊界；所有產品測項狀態均為 **authored-unexecuted** 或 **pending-adapter**，無候選 runtime 成功數。

## 候選入口與依賴

主管須先建立本目錄 `dispatch.example.json` 所示 schema 的正式 dispatch（只由主管寫主管目錄），並在同一對話提供其 exact path／SHA。P29 gate 要求：

1. supervisor ID、review root、baseline SHA 與 offline 授權文字精確吻合。
2. candidate root 精確等於 `work/P28-pickup-profiles-candidate-r1`。
3. artifact manifest 自身 exact SHA；全部 members 驗大小與 SHA。
4. source manifest 本身及 product DLL 必須是 artifact seal 的 members；全部 source members 依 sourceRoot 驗大小與 SHA。
5. 鄰接 `P24.Audio.dll` 也須是 artifact member；未封存相依不能複製到測試執行區。

通過後才會在本 root 的 fresh `runs/<label>` 建置 reviewer harness，獨立複製已驗的少量 profile／shadow／catalog assets（不複製整包素材、不建 junction、不 hardlink），再直接呼叫候選 DLL 的既有離線入口。候選產品本身不由本工具重建；如果需獨立重建，須下一次接回時另明列來源與命令。

原始候選 source、DLL、artifact pins 目前皆缺，所以沒有 candidate run directory，不能宣稱候選可執行或接受。

## 可重跑命令

工作目錄：`D:\lineage開發\TW1608262200研究\work\P29-pickup-profiles-review-r1`

```powershell
# 重新檢驗 fixture/parser 的命令；本次已通過，不需為等待重跑
python -B .\review.py prepare
# gate 與 bytes 自測；不會載入產品
python -B .\validate_gate.py
# 最終 harness build 已完成；工具拒絕覆蓋 harness-build-final
# 第一次建立 final 編譯時使用：python -B .\review.py build-harness
# 主管 exact dispatch 到位後才執行（替換兩個佔位參數）
python -B .\review.py run --dispatch '<supervisor absolute dispatch path>' --dispatch-sha '<exact sha256>' --label candidate-01
# 封存根產物；manifest 不含自身，排除工具快取，不排除交付二進位／命令證據
python -B .\review.py seal
```

最終 build 的 exact argv/cwd/exit 位於 `harness-build-final/build.command.json`，也可從該命令換成 fresh output/intermediate root 重跑，保留既有紀錄。

## 已實作的動態審查範圍（未執行）

- 2,688 mapping slots：走 bound S158/15、ComposeObjects、實際選出的 body Draw commands，對照封存 PNG RGBA hash／exact sprite/key；shadow commands 對照獨立已驗 shadow JSON；terminal 檢查當前 profile Frame 物件 identity、584 hold。
- 每個 profile 的 same-gfx weapon change、舊 callback serial 不可推進或清除新實例；每 pair／speed 兩筆同時 receipt 的逐筆計數與 latest replacement；2796 late callback sequence。
- unknown pair、Speed2/3/17/18、Brave、remote/unknown receipt 拒絕；remote 不得取消正在進行的 own presentation。
- 封存 active-window age cases（本版 **direction 0**）、duration double、captured cadence／Timing.Matches、7 個 projectile selector vectors、S87 weapon8 death fallback。
- embedded 原／擴展 resource bytes 與 SHA。

body-only hash 是實際 compositor body commands 的獨立投影，並非最後含 shadow／scene 的 pixel buffer。完整 composite／occluder ordering、real S30 projectile、所有 lifecycle cancel 維度、900/3000 memory transport／權威數量、事件計數器及 resource fault seams 尚需補 adapter；不以相近測項替代。既有 r34 成功數只作歷史 baseline，未匯入本次 runtime 計數。

## 證據與封存

- `source-manifest.json`：本階段外部唯讀來源完整 pins；當前 authority 與歷史 prep 分開。
- `artifact-manifest.json`：本目錄交付檔案、harness source、編譯 binary、raw logs、result／handoff 的 sizes／SHA；自身除外。
- `harness-build`：初版 build 歷史輸出是 **decoded-only**（先前 text=True/errors=replace），不可當 raw；`evidence/legacy-build-log-classification.json` 明列限制。沒有為日誌格式重跑初版。
- `harness-build-final`：因 adapters 實際變更才追加的最終 build；stdout/stderr 原始 bytes 保留，receipt 同列 SHA／bytes／display decoder／replacementCount／argv／cwd／exit／分類。
- `evidence/fixture-parser.json`、`evidence/gate-tests.json`：準備證據，與產品 runtime 分開。

104 terminal base frames 仍是 historical-selected-payload-matched；312 pickup keys／288 新幀仍是 bounded-slice-only，不提升 BIN/native 身分。本階段交付不等於合入或全案完成。平台 Goal 保持 active，等待同一對話主管 exact candidate pins 接回。
