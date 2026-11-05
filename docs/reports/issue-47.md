# Issue #47：sprite 音效事件靜態契約核驗

日期：2026-11-05。分支：`task/47`。隔離基線：`89888870e52cf3c19e046c7b0c040619c117c644`。

## 驗收調整

依 [2026-11-05 雲端主控裁定](https://github.com/chaoren0418-arch/tw-bin-engine/issues/47#issuecomment-6652566161)與 Issue #47 更新後規格，本輪只收尾：交付既有靜態事件、WAV pins 與明列的 unknown，不再把完整原生派送語義證明列為本張驗收條件。既有 commit `705171687b11dfb70f215b289715cc0f8c3ba560` 的 evidence.json 與 REPRODUCE.md 保持原樣，本次僅更新報告。

**靜態事件與 WAV pins 已整理；原生派送語義 unknown（Themida 執行期）；產品語義依主控裁定。** Themida 執行期限制是主控採納的停止研究理由，本輪未重新分析正式 BIN，也不把「所有靜態方法均不可能」當成新增的實驗證明。以下產品規則標為「**推測，待 #52 真機補測聽音核對**」，不冒充已證明的原生行為：

- 取各幀 `[N` 尾綴，動畫第一次進入該幀時播放；重繪不重播，循環動畫每輪進入時播一次。
- 同幀多候選隨機擇一；#50 的測試可注入亂數來源。不能全候選一律播放。
- `<`、`!` 及其他非 `[` 尾綴不作音效。
- 受擊音取目標 appearance 的 damage 事件；光箭飛行與 impact 各用自身 gfx 事件。
- 只採用原客戶端 `Sound/` 中有 SHA pin 的 WAV；缺檔或雜湊不符明確不播並記錄。

原 evidence.json 的 `status=blocked-native-contract`、`qualifiedBindings=[]` 與候選 disabled 旗標是原生證據資格，仍如實保留；不代表新驗收中的文件交付失敗，也不阻止主控另行採納上述產品政策。#50 須引用本節與明確 pin，不能把歷史 unknown 改寫成已驗證。

推送前已依使用者選擇 B 將唯一指定儲存庫 `chaoren0418-arch/tw-bin-engine` 改為 private；GitHub API 及連接器再次確認 private，管理帳號仍有 admin/pull/push，Git 遠端唯讀存取通過。本輪沒有公開推送，也沒有變更其他儲存庫。

## 結論

**靜態事件與 WAV pins 已整理；原生派送語義 unknown（Themida 執行期）；產品語義依主控裁定。** 原輪已核驗可重現的 50 組文字事件候選、33 個 WAV 身分及兩個歷史 native JSON 的機器碼。native 結構的「最多五候選，選一筆」分支有有限證據；完整 `[`／`<`／`!` parser 欄位對映、版本配對與動作生命週期仍不足，原生 qualified binding = 0。後續產品採用須依上節明示政策，不宣稱 native parity。

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

全部 33 個完整 pin 及 RIFF offsets 見 `work/issue-47/evidence.json.wavPins`，不另複製音檔。以下清單直接由該已核驗資料產生，供 #50 按主控產品政策查核；來源根目錄固定為 `D:\lineage開發\0-客戶端`，不得把技能 ID／gfx 當成 WAV ID。所列 33 檔均有來源 pin；未列的事件或檔案保持 unknown。

| 檔名 | 來源相對位置 | SHA-256 | 用途 |
|---|---|---|---|
| 97.wav | Sound/97.wav | 2e8661cc26238e422b3233509ba24ae11b3440d8c1d74666cb5c842e7de6f1a7 | 技能4／gfx167 |
| 109.wav | Sound/109.wav | 664c8d8b353e2a03cbc337df7a7d36ce009d0213d33064e43779f4c76471b4d1 | 技能1／gfx11722 |
| 127.wav | Sound/127.wav | 6ca44e9ea605d14e6d582a3ca6071b0b0f0d1d395df2f24651fcdb1b2061d364 | 技能3／gfx221 |
| 246.wav | Sound/246.wav | 0df58a6bc641edbdb283f797c1ed7b416f8d4c20a5d95095f079ebda4773cfcc | 已列近戰 attack／damage 候選 |
| 248.wav | Sound/248.wav | d2696d07692ba610371d301e3027c2153c6075eb8446476ff4fbcd1f1e101bb5 | 已列近戰 attack／damage 候選 |
| 405.wav | Sound/405.wav | e65d498bab6bc5a26bebb6c31929b7e4c4291d705c5e8b9ec721993679df2e2f | 已列近戰 attack／damage 候選 |
| 701.wav | Sound/701.wav | 6003c0c805fd9dc6b4988be7e31fdd9782f31543ca96955b0816a03698da39f2 | 已列近戰 attack／damage 候選 |
| 703.wav | Sound/703.wav | 30bac3007049ab0c15a7ccf315445da0a0b9cb990f49a772af0f138b4f127f09 | 已列近戰 attack／damage 候選 |
| 804.wav | Sound/804.wav | a1ccf06439dd3fdf721016b85bb08563a07f16e69ce9ad8d18c2ac07e4d36117 | 技能8／gfx2165 |
| 904.wav | Sound/904.wav | 013e31cbeee8c3472ccf881cdafc0e6ae5117805234a58117071a0eae1a85600 | 已列近戰 attack／damage 候選 |
| 1513.wav | Sound/1513.wav | ae4145e4c94b514e65b91fcfa4487f149fad01e6251e689c8cc276ce6fcafb3a | 已列近戰 attack／damage 候選 |
| 1517.wav | Sound/1517.wav | 7ecf324c4485fc4c43afe531b0e85d9db85dffaaf8cedef9264534ec8e198945 | 已列近戰 attack／damage 候選 |
| 1520.wav | Sound/1520.wav | db73647b7fc6a0e4919123201d27df537a65603389a0cc52eef2d87f95150dcc | 已列近戰 attack／damage 候選 |
| 1521.wav | Sound/1521.wav | 31a44b4544cc7559bbd96fb9c7e397293c8c6a2a425edb337da9add8475a9450 | 已列近戰 attack／damage 候選 |
| 1522.wav | Sound/1522.wav | 085b30d151c203f4fe81629ab2aed3dc170c4b68a0c80bddbde2040cc13b0ad2 | 已列近戰 attack／damage 候選 |
| 1537.wav | Sound/1537.wav | 1921c2d75c763d51a18efec28a9e59ae1799b9f1d91e3954e0a76656be64039c | 已列近戰 attack／damage 候選 |
| 1538.wav | Sound/1538.wav | b792497547fdd91fc057575ac37150aa8137cf0aca592f387008dbc6244c7bb6 | 已列近戰 attack／damage 候選 |
| 1539.wav | Sound/1539.wav | 4bdb0af502bff04b83598e1c865365031b960360fe3160cf8a8849b29de8ed4b | 已列近戰 attack／damage 候選 |
| 1540.wav | Sound/1540.wav | 254955a92b7f9f8688487debeb9193bb2d4401bb5424835e3abd7436c1c071c3 | 已列近戰 attack／damage 候選 |
| 1541.wav | Sound/1541.wav | 6f7153674f59f30fa6836856cb5ba1595a1df449fe4b46704993a5806b11b7c1 | 已列近戰 attack／damage 候選 |
| 1546.wav | Sound/1546.wav | 973829519ec59b59ab1dc78562c3b38bff96954ae99645e8e0911ca40bf860d1 | 已列近戰 attack／damage 候選 |
| 1547.wav | Sound/1547.wav | f56c71a583b1ec3dca6a0caeb165f6461add48cc7581c142a91ea06d3dd5f5e3 | 已列近戰 attack／damage 候選 |
| 1560.wav | Sound/1560.wav | e9232162f71a1703edcdbcdfb84d43f03dac6967424448683258a0ae78d94e54 | 已列近戰 attack／damage 候選 |
| 1561.wav | Sound/1561.wav | 90a7fa901924853dce97c761f545f20708566967d4e34bd645f5e4cb88570f2a | 已列近戰 attack／damage 候選 |
| 1562.wav | Sound/1562.wav | 99abfa6c6b93a3585763fb518272abdce6a6afd17c2ebebe59be045c779c6c7f | 已列近戰 attack／damage 候選 |
| 1567.wav | Sound/1567.wav | 78e5f3038f974d9e4a8527b31324a1e0cd33ff23d0ed03c031f33d77856a9ae1 | 已列近戰 attack／damage 候選 |
| 1568.wav | Sound/1568.wav | 2068f34cd7e7ee3cb937740bb744f1b6c282586d2999557d5fdb87ba832d818d | 已列近戰 attack／damage 候選 |
| 1569.wav | Sound/1569.wav | 219b8e3dd0f0cfc4d9fd77be6a062bebe1cbc432c6bfadc6169fd6f157290335 | 已列近戰 attack／damage 候選 |
| 1574.wav | Sound/1574.wav | b30b32f1ea23070e3803c3c49f410b211958d9e6b801baaeec79e66ade4d5b6e | 已列近戰 attack／damage 候選 |
| 1575.wav | Sound/1575.wav | 053523a61c462ffd2eb8589e3b2969e882a2e4e1731503b75708b42d6a40df1b | 已列近戰 attack／damage 候選 |
| 1580.wav | Sound/1580.wav | e2e760868de230cd778093ae41f9732ebc92517a5fe4704536bad61bff515e49 | 已列近戰 attack／damage 候選 |
| 1581.wav | Sound/1581.wav | 3c10c90cb1ee08584d39c25cd885a287bc9ef9c2e9ecdbe7c7e58e4c080062d0 | 已列近戰 attack／damage 候選 |
| 1582.wav | Sound/1582.wav | 24486b338807cb118bded3a04fda6d6e2d8883e86a755d8f2df62a84f64d3128 | 已列近戰 attack／damage 候選 |

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

由 Claude 依更新後驗收審查文件交付，並決定何時解除 #50 的依賴；本輪不直接修改 #50 或其他 Issue。上述50列候選與33音檔可供 #50 依「驗收調整」所列產品政策使用，但零列取得完整原生語義資格。**交付完成不等於 native resource gate 已通過，更不等於音效產品或可聽驗收完成。**

若未來另行要求證明原生語義，所缺仍是合法且可版本對映的 parser/loader 與 event consumer 靜態片段，包括來源 SHA、ImageBase/section 映射、VA/RVA/File Offset/bytes SHA 及完整 caller chain。這些是保留的 unknown，**不再是本張更新後的交付驗收條件**；本輪不繼續補件，不啟動／附加／dump、不碰正式 BIN 或連服務。後續 #50 實作及 #52 真機聽音驗證依主控新規格辦理。
