# P31 拾取最終合成覆核

**有限離線覆核通過：8 條軌跡、56 個狀態案例，191,232 個最終 buffer 像素的 Colors／Mask／EntityOwners 全相同，差異 0。未發現有證據支持的產品缺陷，沒有修改或重建產品。**

唯一主管 `01a185b4-ecf6-705c-90e6-83b9eb1ba02f`；唯一共享整合者仍為 `01a17a22-17e4-70c0-9792-166ddb86ea42`。本工作只寫 `work/P31-pickup-composite-review-r1`。Goal 初讀 null；主管補充指示後建立 P31 有限 Goal 並讀回 active，完成後依真正工具更新。未增開子代理／對話。

## 基準與新增覆蓋

- acceptance：`主管/P25-P27-新主管接管-20261029/P30-r2-final-acceptance.json`，SHA `b37e5993a5a2fd5c0aed6f10e5c1c313da6dbcaf5899fcb729fffd6d08c03cf1`。
- stage：`work/P30-pickup-profiles-stage-r1/future-stage-02`；seal SHA `45bf24321192718e090deca882ce1ce884113cb9bfae817b73ab92892d3f5514`。
- 實際載入 P11 DLL SHA `6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3`，由 runtime 內部讀取 assembly location 並驗 SHA；引用的 audio SHA `d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4`。
- P28 對 BODY-PIXELS／SHADOW 已有作者測試聲明；P29 runtime-review-r1 的 5,196 rows 只獨立驗 body Draw bytes 與 shadow commands，remaining-scope 明列 final composite gap。P31 補上獨立完整最終 buffer 與遮擋像素 oracle。歷史 rows／fixtures／多重斷言均不加進 56 個案例。

`coverage-delta.json`、`cases.json` 是樣本及覆蓋差異；`comparison.json` 有逐案例結果與可定位 witness。每一案例是一個可觀察的 presentation 狀態，不把每像素／每欄位當獨立 case。

## 實際路徑與獨立 oracle

helper 直接引用已接受 DLL，只執行 LoginConnection 的 synthetic packet seed、原 receipt 路徑及 `Renderer.ComposeObjects`；反射只設定 helper clock／明示的 Assets fixture、呼叫內部 packet seam。沒有啟動 ClientForm、launcher、GUI、transport、音訊裝置或正式服務。

Products 的 `ObjectRendering.cs:ComposeObjects` 經原排序與 `Renderer.Blit` 寫出 Patch。helper 保存真實 Colors／Mask／EntityOwners、VisibleObjectIds、commands、visual 與 pickup snapshot，PNG 由產品 BitmapOf 產生。`PickObject` 使用的正是 EntityOwners，但本輪沒有執行 GUI 滑鼠點擊。

expected 在 runtime 之前建立，從已封存 `engine-pickup-asset-source-r1/frame-map.json` 的 tileMaps 與獨立 block JSON 取得 RGB555 sparse pixels；每個選定拾取／terminal body crop 另核對 sealed prep PNG 的 RGBA SHA。待機與結束採目前 exact weapon profile 的第一個 breath key。陰影採 sealed exact gfx/bodySprite/key shadow JSON。無 SPR 解碼、PAK 掃描或資產整包複製。

Python oracle 對每一像素收集來源 stack，再依有來源依據的 screen Y、screen X、ground 優先、Z、逆 serial、objectId 排序，逐 RGB555 channel 整數平均計算陰影。它不呼叫產品 DrawComparer／Blit／Blend，不讀候選 Draw commands 作 expected，也不從實測輸出建立 golden。每像素保留 contributor indices，連到獨立 commands 及 layer provenance。

場景是**明示的離線合成 fixture**：有孔洞的棋盤地面、藍色可點選後方 entity 7001、紅色及黑色不透明前景細條。前後物件在同 tile cell 用受控 Z 極值分開。entity 7001 為注入 scene 的 actor-layer fixture，不冒充真正 remote packet／session entity。此設計刻意讓遮擋、透明、黑色有效像素和 ownership 可判定，不代表原生遊戲畫面。

## 選例與玩家可見結果

| Exact pair | directions | 用途 |
|---|---|---|
| 0/0 | 0、4 | 原有對照，對向姿勢 |
| 1/0 | 2、7 | extension 一般序列，另一投影與最後方向邊界 |
| 1/4 | 2、7 | 同 gfx 不同 weapon，結束返回不同 exact breath |
| 2796/20 | 0、4 | 特殊 sequence frame 1→2→3，terminal 8/12:0 |

每條軌跡依 sealed contract 取 idle、start(0ms)、middle1(42)、middle2(209)、terminal(376)、hold-last(959)、finish(960)。terminal 的保持區間為 584ms。樣本覆蓋不同朝向的投影／遮擋形狀，並在 direction7 取索引上界；不是方向切換測試，也不主張所有 8 方向的 composite 已跑。

在上述合成場景，拾取三幀確實沒有陰影；terminal 恢復該 profile/key 的陰影，保持末端不提前消失，結束回目前 exact weapon 的 breath。例：gfx1 direction7，weapon0 回 `15:11`，weapon4 回 `119:11`。2796 direction0 從 `48:1/2/3` 到 `8:0`，結束回 weapon20 的 `160:11`。前景與身體遮擋、背景未受影響區、透明孔洞、有效黑色像素及點選 ownership 均與 oracle 相符。

實際有證據的像素包括：前景遮身體 3,610、身體遮後方 entity 7,964、前景遮陰影 1,056、陰影保留後方 entity ownership 524；透明孔洞 5,412、未受影響地面 74,448。這些是重疊的 witness 計數，不是案例數。6 個 pose 沒有「前景遮身體」交集，已由逐例 witness 如實呈現；不宣稱每種關係在每一幀都出現。

`actual-contact.png` 為產品實測 PNG 的有限接觸表，已視覺檢視；原始實測、expected、diff 分別保存。

## 負向檢查與失敗保留

對 frozen actual 作三種獨立 expected mutation：前景像素右移4px、前景 Z 改到後方、移除陰影。144 次適用變體全部產生差異，證明 oracle 不是永遠通過。這些屬負向控制，不是新增產品案例，也不是產品變更／native mutation。三類完整例子的 mutant expected、差異座標與差異圖在 `sensitivity/`。

首次 prepare 在 `2796/48:1` 的 body PNG cross-check 失敗，產品尚未執行。原因是 helper 的 oracle adapter 把明示 block index 誤作 list offset；該來源 list 以 index10 起頭。修為依 `index` 欄位查表，保持原 PNG hash、來源 bytes、預期數據不變。舊 source 和已產生 expected 保留在 `prepare-attempt01.py`、`expected-prep01/`，診斷在 `prepare01-diagnosis.json`。該 prepare 失敗由桌面 exec 輸出記錄；沒有另宣稱存在 raw stream 檔。

helper build 1 次成功、runtime 1 次成功，無產品 build／runtime 重試。build 原始 stdout 保留首次 dotnet 私有 PATH 提示；最後 0 warnings、0 errors。兩次實際 subprocess 的 argv/cwd/環境/exit/raw stdout/stderr/SHA/source snapshot 在 `runs/build-helper01`、`runs/runtime-helper01--runtime01`。

## 重現與封存

工作目錄：`D:\lineage開發\TW1608262200研究\work\P31-pickup-composite-review-r1`。

唯讀驗封：

```powershell
python -B .\verify.py
```

本次實際命令（原 label 已封存，runner 拒絕覆蓋）：

```powershell
python -B .\prepare.py
python -B .\run.py build helper01
python -B .\run.py runtime helper01--runtime01
python -B .\compare.py
```

若主管另授權重現，在本根用新的 build/runtime label；runtime argv 與 output path 由 runner 綁定。`compare.py` 現綁原 runtime01，重現時須獨立複本指定新輸出，不覆蓋封存。prepare 是一次性、已存在 cases 時拒絕覆寫。`run.py` 使用 P30 r2 已接受環境的私有子程序目錄及固定 PROGRAMFILES(X86)，清空 NuGet sources；無依賴下載。

`source-manifest.json` 保留只讀來源 pins，`artifact-manifest.json` 封存所有 P31 檔案（自身除外），`final-audit.json` 記錄事後來源/DLL不變；`result.json` 是交付摘要。完成後停寫，等待主管接回。

## 明示限制與原拒絕

只有4個exact pairs、directions0/2/4/7的8種組合、speed0、上述固定場景與 idle clock reset；其餘38pairs、directions1/3/5/6、speed1、完整地圖／真遮擋素材／opacity transition、remote actor事件／GUI picking、native／BIN等價均未驗。未擴張至P32生命週期、S30、transport900/3000、音訊event counters或resource faults。104 base仍historical-selected-payload-matched；312 pickup仍bounded-slice-only，不升格。

正式入口仍r33。保留原 automatic approval review 拒絕：向唯一整合者派發 `ENGINE-R34-ENTRY-ACTIVATE-P26-R1` 的 send_message_to_thread 未執行未重試，批准仍待答。原原因：「此派工會持久修改正式入口與 project-manifest，將 r34 提升為現行產品版本，屬外部／正式承諾；使用者只授權引擎開發，未明確授權這次入口啟用，且政策不以先前助手派工替代該核准。」本P31不代行或改道。
