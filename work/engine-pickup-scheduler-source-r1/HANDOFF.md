# ENGINE-PICKUP-SCHEDULER-SOURCE-R1

交付狀態：限定範圍的靜態來源研究完成；renderer 僅有提案。`runtimeChecks=0`、`productChanged=false`、`candidateBuilt=false`。本輪只寫此 work 目錄，未執行原 BIN/JAR、模擬其機器碼、連線、操作 GUI、DB 或服務。既有 Goal 實讀 blocked，未修改平台 Goal。

基準是正式 final r32，release seal `46e9249224e768a1ac09fe99c08a9edf387833295022147aa046c206769fa507`、baseline-final `0c645bf5c3a515fd5da88cf28c834a4b2967aed268946d45fb5a6ebe9f03fcb7`。固定 BIN SHA-256 `e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2`，image base `0x400000`。來源 pin、每段 VA/RVA/File Offset/size/hash 與逐項主張分別在 `source-pins.json`、`native-byte-audit.json`、`proof-index.json`。

## 可採用的來源結論

1. 普通 action15 經 `A467A0` 設 action、frame index=0，再由 `A43560` 先遞增，故第一個綁定的是 metadata frame1。後續為 2、3，終點回到 frame0、呼叫 `A3EF40(1)`，回傳 false。`A4B060` 隨後經 vtable+48 進入 `A44500`，不再從該 terminal 分支排下一個 pickup frame。此結論要求有效、穩定的 action15/variant，不能宣稱所有特殊狀態一律如此。（P01–P03）
2. `A44500` 的 queue>0 分支用傳入 D0 排 `A4BF00`，沒有 +1。空佇列普通分支則把 weapon+3 排入命令佇列，等 fresh clock+584 才 consume；它不會立刻設 action0，也不是固定 D0。特殊 +34 回傳非零會改 action71；class11/weapon32、weapon28..39、缺 idle metadata 與 self guards 都有其他路徑。`A4B060` 的 actor+0xD8 非零且 action!=8 還會把傳入 D0 改成1。（P03–P05）
3. base vtable 的 +64=`51E860` 是 no-op，derived 的 +64=`A3C4F0` 才建立 `{226, action}` 8-byte command，經 `A3C100` append 至 actor+0x19C。action15 不走 action2 的重複抑制；配置失敗則不加入。`A44B60` pop index0；`A4BF00` 的 opcode226 switch entry 已由資料 bytes 驗到 `A4BFB9`，經 +4C 回到動作 selector 並釋放命令。這是消耗命令，不是取消。（P05–P07）
4. `A3EBF0` 在 frame0 遇到 idle head，可移除它並取消該 actor 的 `A4BF00`；空 queue、非零 frame 的 current idle 則取消 `A4B060`。普通 weapon 的 action15 不符合這個 idle 條件。注意 head 的精確條件是 `(opcode226 && action==weapon+3) || (第二欄==9 && A43170)`，第二項沒有 opcode guard。`A23AC0` 以 callback **及** context 同時相符刪除全部匹配排程。（P08）
5. FIFO 有取消例外：`92CB70` 收到 action8 且走 busy 分支時，會清掉含 opcode226/action15 的 pending commands，保留 opcode186。若目前 action8，true 分支的 incoming action15 會在 +64 前被略過。另 `A44BB0(action)` 會移除匹配的 opcode226 記錄，但本輪未追其 caller；其迴圈刪後仍遞增，也未在此 helper 釋放命令或取消 timer，不能把它稱為可靠的「取消全部拾取」API。（P09）
6. 普通 action15 的 terminal 沒有自動 enqueue action15，因此不是 pickup 自循環。frame0 仍可保持 action15，直到後續 command selector 執行；等待中的 idle 命令可被新動作搶占。584 ms 不能當作輸入鎖、C112 gate 或 utility cooldown。（P02–P09）

## 排程與相對時間

`A228B0` 在全域 `0x153CDE0 !=0` 時讀 QueryPerformanceCounter，計算 `counter*1000 / configured64bitDivisor` 的 low32；否則 GetTickCount。IAT 名稱來自固定 PE。頻率/selector 的初始化與實際值未定位，沒有宣稱實際採用哪一支。

`A231C0(delay,callback,actor)` 讀 fresh clock N，插入 N+delay，回傳 N。`A23260` 使用 caller 提供的 N。兩者按 unsigned due 排序，等值排在既有等值之後。這不是保證 wrap-safe 的演算法。（P10–P11）

初次 callback deadline 與 actor+**0xBC** 都設成 N0+D1+1；0xBC 是十進位188，不能誤寫成 +0x188。下一幀 callback 在 `A43560` 綁定新幀後讀 now，令舊 baseline 為 B、新幀時長為 D：

```text
lateness = now - B
nextDue = now + max_signed(1, D - lateness) + 1
newBaseline = B + D + 1
```

無 wrap/overflow 時 nextDue=`max(now+2, B+D+1)`。baseline 保留理想進度；逾期不在同一 actor callback 內無限追幀。`A23360` 每次 pump 只讀一次 clock，對 due<=sample 的首筆先移除再呼叫 callback(context)，然後用同一 sample 繼續判斷。pump 呼叫頻率與實際呈現延遲尚未證明。（P12–P13）

以下只是 `relative-timeline.json` 的整數算式，不是實測或 native emulator：

| Speed | D0,D1,D2,D3 (ms) | frame1 / frame2 / frame3 / terminal0 | 有佇列 consume 理想值 | 空佇列普通 idle 理想值 |
|---|---|---|---|---|
| 0 | 41,41,166,166 | 0 / 42 / 209 / 376 | 417 | 960 |
| 1 | 30,30,124,124 | 0 / 31 / 156 / 281 | 311 | 865 |
| 2 | 82,82,332,332 | 0 / 83 / 416 / 749 | 831 | 1333 |

N0 是初次 `A231C0` 的取樣，**frame1 綁定實際早於 N0**；表內0只是參考慣例。後續列假設零遲到、零處理/取樣間隔。terminal 又讀另一時鐘 Nt，因此真正符號式是 Nt+D0 或 Nt+584，不是固定端到端總長。Speed2 只有來源算式，沒有 Slow qualification。

中性條件不只是 Brave=0：固定 duration 分支、secondary flags、status multipliers、variant、self/remote status 來源都要符合。尤其 `A407D0` 的 nonself 分支把 **actor+0x19C 的 queue count** 當 backlog 因子，>=2/4/8/16 會除以2/4/8/16。表格要求每次取 duration 時 count<=1；不能套在任意擁塞佇列。（P14）

## 有限 renderer 提案與未完成的驗收

`source-contract.json.rendererProposal` 是本輪交付的規格，不是實作。建議只在 current-owner 的 authoritative action15 到達，且 normal Speed0/1、中性狀態及 asset/variant 能表示時，建立有限 instance：frame1→2→3→terminal0，最多三次推幀及一次 pending transition。frame0 不自行重播 pickup；已知 pending command 用 D0，已知普通空 queue 分支用584，idle pending 可被新的 accepted action 搶占。

owner/session/world/actor lease 失效、actor removal/death、權威狀態切換或無法支援的外觀/速度時取消 renderer instance 與其 timer，是建議的引擎所有權政策，**不是**從 BIN 證明了相同 token 機制。無法表示的特殊狀態維持既有 presentation 並標為 timing unqualified。C112 gate900ms、pending fallback3000ms、inventory/ground 權威完成與既有 ownership admission/completion 都不因這份提案改變。

metadata/pixel 可引用已封存的 asset source，範圍是42 profiles、13 gfx、8方向的 bounded 素材。沒有新增像素驗證；8個 shard 與歷史 archive 不同、104 entries 只有 current IDX/slice identity 的限制完整保留。不能將該素材證據提升為完整 archive 等價或 timing parity。

未證明項目：所有 profile 的 variant virtual target、live self status、clock初始化與 pump cadence、所有取消 caller、真正畫面呈現、輸入起點與端到端拾取時間。沒有 utility cooldown=0 結論。後續實作需另派工並離線驗證 owner/event/queue/late timer；實機/native timing 仍須獨立驗收。

## 唯讀重跑

```powershell
& 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe' -B -X utf8 'D:\lineage開發\TW1608262200研究\work\engine-pickup-scheduler-source-r1\verify.py'
```

verifier 重新核對 source pins、62個 native chunks、14項 proof 的 byte ranges、switch/vtable、算式與 seal 清單；只讀、不寫收據。結果不計為 runtimeChecks。`collect.py`/`extend*.py`/`finalize_evidence.py`/`build_reports.py`/`seal.py` 是本輪建置來源，會寫產物，**不得在已封存目錄重跑**；重建應另派 fresh work。`analysis-boundaries.json` 記錄線性反組譯不可採用的 data/helper tail 範圍。

主要交付：`result.json`、`source-contract.json`、`proof-index.json`、`relative-timeline.json`、`native-byte-audit.json`、`source-pins.json`、`artifact-manifest.json`。交付完成不代表後續實作或共享整合獲得授權。
