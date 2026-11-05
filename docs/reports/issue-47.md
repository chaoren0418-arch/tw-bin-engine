# Issue #47：sprite 音效事件靜態契約核驗

日期：2026-11-05。分支：`task/47`。隔離基線：`89888870e52cf3c19e046c7b0c040619c117c644`。

## 結論

**部分靜態取證完成；原生音效契約仍 blocked，qualified binding = 0。** 本輪新增可重現的 50 組文字事件候選、33 個 WAV 身分，以及兩個歷史 native JSON 的機器碼核驗。找到 native 結構的「最多五候選，選一筆」分支；不能把全候選組一律播放。但 `[`／`<`／`!` 的 parser 欄位對映、版本配對與各動作派送生命週期仍不足，不能將本輪候選提升為產品可播放白名單。

已核對 Issue 最新 YAML 與標籤均為 `runner:dot`，正文殘留 `runner:codex-local` 不當成改派；起始只有派工留言，沒有 RESULT。本機與遠端沒有既存 `task/47`。使用 `scratch/wt/task-47` 稀疏 worktree；原工作目錄仍保留其他工作。未讀 `docs/REPORT.md`，依上層明確限制優先於派工範本要求。

## 做了什麼與證據層級

### 已驗證：定向文字與音檔

原 TXT：`D:\lineage開發\0-客戶端\TW13081901.txt`，5,990,880 bytes，整檔 SHA-256：
`28c4fc600833fb8c609ab195a67ff926e8ffa571100d8d4b389a23be8c2c8db8`。

保留磁碟 bytes 的 File Offset；只在計算歷史段落對照時正規化 CRCRLF。下列地址均為 **TXT File Offset，不是 RVA 或 VA**。index 從 0 起算，只代表資料順序。

| 技能／效果 | action | index | token File Offset | 原 token | WAV 候選 |
|---|---|---:|---|---|---|
| 1／11722 | 0.effect | 1 | `0x46ab48` | `0.0:3[109` | 109 |
| 3／221 | 0.fire | 1 | `0xd008` | `0.1:2[127` | 127 |
| 4／167 | 0.fly | 1 | `0xacc7` | `0.0:2[97` | 97 |
| 8／2165 | 0.animation | 1 | `0x5447c` | `0.0:4[804` | 804 |

技能→gfx 的身份沿用 `docs/design/sfx-and-ground-items.md` 的已記錄 catalog 對映，本輪重新核對 TXT 中上述 gfx/action/token。沒有重讀 assets junction，沒有把這份歷史對映說成原服查詢結果。gfx167 保留 `109.effect(4 219)`；gfx219 的 action0 沒有 `[`。這只能說明所查文字沒有該候選，不代表所有原生命中路徑都無聲。

近戰範圍由既有 `work/engine-pickup-asset-source-r1/profile-qualification.json` 的 42 組 profile 中，排除 weapon20 弓，取得 **31 組／13 gfx**。gfx 為 0、1、37、48、61、734、1186、2786、2796、6650、6658、6661、6671；weapon 為所列組合中的 0、4、46、50。逐一核對 `weapon+1` attack、`weapon+2` damage 的原文字，不主張這條算式就是 native selector 的完整實作，也不替未採用的斧／矛／杖等新增白名單。

| gfx／weapon／action | index | token File Offset | 原事件 token |
|---|---:|---|---|
| 0／0／1 attack | 4 | `0x7a` | `16.3:5[1539` |
| 0／0／2 damage | 1 | `0xab` | `24.0:6[1537[1538` |
| 0／4／5 attack | 3 | `0x17b` | `120.2:3[248[703[701` |
| 0／46／47 attack | 4 | `0x94c` | `152.3:4[1513[1517` |
| 0／50／51 attack | 4 | `0xa99` | `280.3:5[1540[1541` |
| 1／0／1 attack | 5 | `0xedd` | `16.4:2[246` |
| 1／0／2 damage | 1 | `0xf0d` | `24.0:6[1546[1547` |

完整 69 條 action 核對含 62 條近戰 attack/damage、4 個技能效果、219 impact 與 gfx0 walk/get 對照。保留每條 token 的尾綴、方向欄、ticks、資料幀鍵及候選順序；沒有括號事件的 action 也保留。50 組 `[` 候選是 **資料候選**，所有列均 `qualified:false`、`enabled:false`、`dispatchPolicy:null`。`<206`、`<478`、`<479` 和 `!` 只保留原文，不推定為聲音或命中通知。

33 個 `Sound/<ID>.wav` 全部存在，整檔重算 SHA，唯讀核對 RIFF 長度、chunk 邊界、fmt/data 的 File Offset、聲道、取樣率、位深與 frame count。全部為 PCM（Python wave 的 `NONE`）、22050 Hz、16 bit，單／雙聲道值逐檔記錄；沒有播放。WAV pins 可供後續來源核驗，**不是音效派送授權**。

四個技能 WAV SHA：

| ID | bytes | SHA-256 |
|---|---:|---|
| 97 | 47408 | `2e8661cc26238e422b3233509ba24ae11b3440d8c1d74666cb5c842e7de6f1a7` |
| 109 | 61046 | `664c8d8b353e2a03cbc337df7a7d36ce009d0213d33064e43779f4c76471b4d1` |
| 127 | 84854 | `6ca44e9ea605d14e6d582a3ca6071b0b0f0d1d395df2f24651fcdb1b2061d364` |
| 804 | 335476 | `a1ccf06439dd3fdf721016b85bb08563a07f16e69ce9ad8d18c2ac07e4d36117` |

全部 33 個完整 pin 及 RIFF offsets 見 `work/issue-47/evidence.json.wavPins`，不另複製音檔。

### 已驗證：歷史 native 片段內部一致性及條件式分支

只讀原工作目錄既存 `work/engine-slow-pickup-source-r1/native/0xa3ef40.json` 與 `0xa43560.json`，未開啟任何 BIN。JSON SHA 與 scheduler 的 source-pins 相符；其中 bytes 的 size/SHA 與 native-byte-audit 相符，再用 Capstone x86/32 獨立解碼全部 chunk，確認完整覆蓋。文件產出的 VA／RVA／File Offset 映射沿用該歷史 audit，**沒有重驗原 BIN PE section table**。

歷史證據標示 BIN SHA：`e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，ImageBase VA `0x400000`。

| chunk | VA | RVA | File Offset | bytes | bytes SHA-256 |
|---|---|---|---|---:|---|
| A3EF40 | `0xa3ef40` | `0x63ef40` | `0x63e340` | 3686 | `64701a56874b973f27af05bc1629d1502901c992a373c820611264a79b0be771` |
| A43560 | `0xa43560` | `0x643560` | `0x642960` | 839 | `5841695b55f6faa4abda547de6342f2433311d70573ab79724062cb5cef45181` |

本輪可確認以下 **結構層** 語義；仍缺 loader 將文字轉入結構的證據：

- `A3F9CC..A3FA7D`（起點 RVA `0x63f9cc`、File Offset `0x63edcc`）：參數非零及全域 `0x153CE60` 非零才進候選分支；以 frame index × 36 定址，讀 `+0x0c` 的 uint16 候選數，0 或 >5 略過。1 筆取 index0，2–5 筆呼叫 IAT `0x114b668` 後取除法餘數；讀 `+2+2*index` 的一個 int16，該值為0也略過。沒有遍歷全部候選的迴圈。
- `0xa3fa37` bytes=`ff1568b61401` 的間接呼叫，與歷史 imports.json 的 `MSVCR110.dll!rand` IAT VA 一致。這是既有靜態 import 對照；未核定 seed、真實抽樣序列、公平性或 live IAT。
- `A3FB91..A3FCBD`（起點 RVA `0x63fb91`、File Offset `0x63ef91`）：frame 結構 `+0x14` 為2時走 `A06210`，其他值走 `A060F0`。只能確認分支與目標地址；本輪沒有這些 callee 的完整語義，不能直接把1/2配成 `[`／`<`，也不能宣稱是哪一種音量／空間化模式。
- `A4358C`（RVA `0x64358c`、File Offset `0x64298c`）可見 frame index 先加1。`A4388F..A438A6`（File Offset `0x642c8f`）正常續幀分支呼叫 `A3EF40(1)` 並回傳 true。terminal 分支 `A437D8..A43825`（RVA `0x6437d8`、File Offset `0x642bd8`）把 index 設0後**仍呼叫 `A3EF40(1)`**，最後回傳 false。因此不能無條件把 terminal frame0 的事件抑制規則當成 native 事實，也不能從「有呼叫」推成一定播放。

兩個原 JSON 的 SHA：A3EF40=`b43377adab4fadd67838d8efec44cb97bbe320a672d0e99e03cc0bcdd0ec456f`；A43560=`591608eba014bbe515d61b2b811b8c20fa2e01ea6b54d90ea9163293cc2488fe`。截取的逐指令 bytes、地址、片段 SHA、歷史 import 和所有輸入 pin 已記錄於 evidence.json；沒有執行、模擬或 dump 任何原程式。

### 版本限制、推測與待驗證

設計 #42 記錄磁碟 LiTo.bin SHA `ce6ee68ada8fd37e6c096b1a27e46cb6888f1179f85defb5007ac60287360292`、Themida entry；本輪遵守限制未接觸該檔，也未重驗這項歷史身分。它與上述 native 證據 SHA 不同，兩者地址不能混用。TXT 檔名也不能證明對應 TW1608262200 原生載入器。

13 個角色段落與既有 entry 的 CRCRLF 正規化 bytes 比較：gfx37／2786／6650 相同，其他10個不同；差異不能全部假定只是空白。本輪沒有把整個版本配對提升為已驗證。每段兩種 hash 與相等旗標均保留。

| 待證問題 | 目前可說什麼 | 還缺什麼 |
|---|---|---|
| `[`、`<` | 原文候選與結構分支各自可見 | 同版本 parser 將符號、候選數、順序、mode 寫到36-byte frame 結構的 bytes／caller chain |
| `!` | 多個 attack token 出現；完整保留 | parser 欄位、讀取者、是否與攻擊／飛彈釋放有關的機器碼 |
| 多候選 | 歷史結構會擇一；不可全播 | TXT→候選欄位及對應版本，0、超過5、混合符號的規則 |
| 首次進幀 | A43560 先遞增；部分歷史 pickup 已有初始化研究 | 各技能／攻擊／受擊 constructor 與所有首次 caller，不能只靠 pickup 推廣 |
| terminal／重播 | terminal0 仍可能進候選分支 | a2=0/1 caller、事件消費、重啟／variant／取消／重播契約 |
| 飛行／命中 | gfx167有97候選、219 action0無括號事件 | 8211E0／821360 等歷史名稱的同版機器碼及音訊 caller，實際命中替換與事件時機 |
| 受擊 | 外觀不同有不同 damage 候選 | 接收事件→目標外觀／weapon→action→frame event 的完整原生鏈 |

`engine/source/ObjectAssets.cs` 的 AnimationStep 不含 events；`MagicRendering.cs` 的 PureFrame/60ms 飛行為現行產品邏輯。這些程式與註解不能代替原生派送證明。新引擎若採用去重／owner lease 政策，應明示為產品設計，不能宣稱已從本輪 native 證據證實。

## 怎麼驗證及產出

- `work/issue-47/evidence.json`：18 個文字段落、69 條 action、50 組 disabled 候選、33 WAV pins、2 native chunks、5 個機器碼範圍及輸入 hash；`qualifiedBindings: []`。
- `work/issue-47/REPRODUCE.md`：保存完整唯讀重現程式與執行步驟，依儲存庫規則將腳本取出至 scratch。只讀明列 TXT/WAV、既有 repo metadata、兩份歷史 native JSON，不呼叫既有研究的 collector/verifier，不追隨其 BIN 或封存路徑。
- `docs/reports/issue-47.md`：本報告。

本機 Python launcher `py -3` 找不到安裝；改用已存在的 Python312 執行檔，未安裝任何套件。使用環境既有 Capstone。從隔離 worktree 執行：

```powershell
# 先依 work/issue-47/REPRODUCE.md 將程式取出至 scratch。
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 scratch/issue-47/verify.py --prior 'D:\lineage開發\TW1608262200研究' --client 'D:\lineage開發\0-客戶端'
```

結果：`verified=true, candidates=50, wavPins=33, qualifiedBindings=0, nativeChunks=2, runtimeChecks=0`。JSON 重算整體相等；TXT 固定 SHA 及 native source pins 不符會拒絕。原工作目錄的兩份歷史 native JSON／metadata 未納入 git，乾淨 clone 若缺它們會如實失敗；**不會自行取得 dump 或改讀 BIN**。`--prior` 必須指向有合法既存證據的工作根目錄。這是已驗證的有限範圍，完整磁碟、資產與歷史檔案存取均未驗證。

本輪僅靜態取證，未修改產品來源、未建置或執行產品、未播放、不連 DB／服務、不啟停或附加任何遊戲程序，不讀 config/server.properties、正式 BIN、封存與 assets junction，不複製 PAK/MAP/SPR。沒有以 fixture 或未知常數補完原生功能。

## 給下一棒的提示

由 Claude 審查本輪條件式結論及證據等級；調度器保持 sfx-dispatch 依賴阻擋。**不得因本 Issue 交出報告或 RESULT 就把 native resource gate 視為通過。** 上述50列候選與33音檔只供來源查核，零列允許實作播放。

解除阻塞的最小交付是：經使用者既有授權範圍內合法取得、可版本對映的 parser/loader 與 event consumer 靜態片段，含來源整檔 SHA、ImageBase/section 映射、VA/RVA/File Offset/bytes SHA、符號→欄位→caller→播放介面完整鏈；補上首次／terminal／重播／飛行／受擊規則。優先搜尋已授權的現有靜態研究，不能為補件啟動／附加／dump、碰正式 BIN 或連服務。這是證據阻塞，並非要求使用者放寬本輪權限。
