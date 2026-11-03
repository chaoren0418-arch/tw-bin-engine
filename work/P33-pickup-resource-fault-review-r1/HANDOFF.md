# P33 拾取資源故障拒絕、取消與恢復

有限離線子集合完成：**28 個不同案例，28 pass／0 fail**。產品沒有修改或重建，未發現有證據支持的產品缺陷。交付待唯一主管 `01a185b4-ecf6-705c-90e6-83b9eb1ba02f` 接回；不代表全案或原生驗收。

唯一寫根：`D:\lineage開發\TW1608262200研究\work\P33-pickup-resource-fault-review-r1`。沒有增開對話／代理或改模型，唯一共享整合者仍為 `01a17a22-17e4-70c0-9792-166ddb86ea42`。初讀 Goal 為 null，依派工建立有限 Goal 並讀回 active，無 token 預算；最終真實狀態見 `goal-readback.json`。

## 精確基準

| Pin | SHA-256 |
|---|---|
| P30 acceptance | `b37e5993a5a2fd5c0aed6f10e5c1c313da6dbcaf5899fcb729fffd6d08c03cf1` |
| P30 stage-files-seal | `45bf24321192718e090deca882ce1ce884113cb9bfae817b73ab92892d3f5514` |
| 實際載入 P11.ClientEngine.dll | `6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3` |
| 綁定 audio DLL | `d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4` |
| P31–P32 主管接受 | `217e44587a14ae2b65441aceb4f551cca1717d4b228616e51d5a2ec6a62acccf` |

helper 直接 Reference `work/P30-pickup-profiles-stage-r1/future-stage-02/build/client-product-r34` 的固定 DLL，CopyLocal 後再次驗 SHA；runtime 自己讀取 assembly location 與 actualDLLsha。無 ProjectReference、launcher、ClientForm 入口或產品 build。

原 sealed prep manifest SHA `2f56ff408f7a2b96197d64313ec898f962ac1c53b49df8acc5b8a7b6cb316a66`，只驗使用的 planned-mapping／profile-inputs size+SHA。基礎資產來自唯讀 r34 release，先驗原 release seal SHA `955ea88173cb36cecaefb6d5e9bbce7d76dfd7bf39ccd7830e56a269ab3155c3`，再比對本輪使用的 13 個資產檔 size+SHA；不聲稱全資產重新驗封。產品 source 依 P30 原 stage seal 逐使用檔核對。共 45 個來源 pin，runtime 後全部不變。

## 覆蓋差異與計數

P28 作者已有 extension tamper、所有42 pairs缺 terminal／hash／borrowed profile 測試；本輪是固定 P30 DLL 的獨立有限覆核，不將其歷史斷言計入。P29 runtime-review-r1 只獨立核對兩份資源 byte identity，明列 fault／missing group／base-key 缺口。P31 56 pixel cases、P32 196 lifecycle cases 均不重跑或累加。

| 不重複案例類別 | 數量 |
|---|---:|
| 真 constructor：2 個正常控制、base 大小／hash 錯誤 × 2 overload、extension 大小／hash 錯誤 | 8 |
| 2 pairs × missing-base／missing-profile／missing-group／missing-key／profile-path／profile-hash 完整故障恢復鏈 | 12 |
| base-only constructor catalog 與正式 full Load 差異 | 2 |
| exact instance 拒絕、方向／不支援 pair API 邊界鏈 | 4 |
| 直接 private ReadBody parser seam（RGB555 座標、重複 direction） | 2 |
| **總計** | **28** |

每條鏈的多個觀測／欄位不是新案例。24 次事後 expected mutation 比對控制只證明 reason／recovery frame oracle 能辨別錯誤，不計產品案例，不重新執行產品。

## 觀察與可達性

短契約表在 `contract.md`，精確 source/method/行號在 `source-contract-locations.json`。

正常 constructor 先檢查大小與固定 SHA，再解析 JSON。六個錯 bytes 案例皆為 `InvalidDataException: Pickup resource integrity`，沒有返回可用 catalog。base 錯誤在兩種 overload 都拒絕；extension 錯誤不返回 base-only fallback。兩份原 embedded bytes 與常數一致，沒有修改 assembly 或 SHA constants。

`ReadBody` 的兩個負向檢查是**直接私有方法 seam-only**：測到 `Pickup RGB555 pixel`／`Pickup direction`。改過的 JSON 不可能通過固定 constructor SHA，因此不把這兩案當成壞檔可觸發的產品漏洞或 end-to-end 測試。

使用 0/0、2796/20、direction0、speed0。故障只在真實 authenticated catalog 載入之後，修改 helper 記憶體，再呼叫實際 `For`／`ObjectAssets.PickupFrames` 與原 `Renderer.ComposeObjects`。沒有替換 renderer callback、guard、產品 catch 或 expected。

- 缺 current terminal key、缺 exact profile、profile path/hash 不符：兩個 API 均回 null，下一 compositor 驗證以 `renderer/assets-unavailable` 取消。
- 已載入 catalog 缺 body group 或 embedded key：兩個 API 拋 `KeyNotFoundException`；`PickupContextAt` 接住，下一 compositor 驗證以 `asset-integrity/key` 取消。12 條鏈均無例外逸出 compositor。
- 取消後 snapshot active=null，實際 `OwnCombatVisual.Action=breath`；舊 callback 回 null 且不改 snapshot，故障期間新的 own S158/15 receipt 為 suppressed。
- 恢復資源後單獨 draw 不會復活動畫；新的合法 receipt 才由 serial1→serial2，0/0 使用 `88:0`，2796/20 使用 `48:1`，沒有借用 0/0 或其他 weapon。再送舊 callback 仍不改新 serial。
- 一份由相同合法檔案重新解析的 appearance，也會被 `ObjectAssets.PickupFrames` 的 exact catalog reference 檢查拒絕。方向 -1／8、不支援 weapon255 的 `For` 回 null。

單參數 constructor 的合法 catalog 的確僅支援 0/0；將它明示注入 ObjectAssets 時，0/0維持原動畫，2796/20取消，恢復完整 catalog 後合法 receipt 成功。**此注入不代表 production fallback**：正常 `Load()` 永遠載入兩個 embedded resources。無法在不改 DLL 的情況注入 missing embedded resource，本輪未實跑該情境；沒有繞開完整性 guard。

合成 packet seed 與 EmptyScene helper 結構取自 P32 的已封存 Bootstrap.cs，僅用它建立隔離狀態；沒有執行 P32 suite。故障後的 Frame／serial／reason／visual 原始 JSON 都保留，本輪未新增完整 pixel buffer oracle 或 GUI 畫面聲明。

## 執行環境與失敗保留

helper build **1次**、runtime **1次**，exit0；build 0 warnings／0 errors。`runs/build-01` 與 `runs/runtime-01` 有原始 argv/cwd/env/exit/raw stdout+stderr、helper/oracle snapshots。首次 build 即明確設 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`、`DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false`、telemetry off、空 NuGet sources／NuGetAudit=false、UseSharedCompilation=false、nr:false、固定 `PROGRAMFILES(X86)=C:\Program Files (x86)`。runtime 同樣帶子程序環境；nr 是 build 開關，runtime 不執行 MSBuild。

私有 CLI-home/cache/packages/temp 全在本 work；HOME、USERPROFILE、CODEX_HOME 與系統環境未改。SDK 首次 banner 有保留，沒有 certificate installed 訊息；未執行憑證操作或 store 查詢。P32 firstSDK 調查不屬本輪。

報告後處理首次 `finish.py` 因 Python reserved keyword `pass` 用作 dict keyword，於 parse 階段 SyntaxError，沒有執行產品或產生報告。保留 `finish-attempt01.py` 與診斷，僅修 Python dictionary syntax 後成功；expected／helper／runtime 不變，無 runtime 重試。該次錯誤在 desktop exec tool output，未另宣稱保存原始 stderr 檔。

## 交付及限制

`result.json`、`coverage-delta.json`、`distinct-cases.json`、`verification.json`、`sensitivity.json` 是摘要／獨立檢查；逐案原始觀察在 `runs/runtime-01/runtime.json`。`source-manifest.json` 保存精確唯讀來源；`artifact-manifest.json` 封存本 work 的檔案，自身除外。`REPRODUCE.md` 列實際命令與 fresh-label 重現規則；唯讀驗封使用 `python -B .\verify.py`。

未執行：missing embedded resource、其餘 ReadBody schema/ticks/completeness 變體、其他40 pairs、direction1–7／speed1 fault 組合、外部 IOException／JsonException 注入、native／GUI／網路／audio/effect instrumentation／完整合成像素。104 base仍 historical-selected-payload-matched，312 pickup仍 bounded-slice-only；native0，沒有來源資格升格。DB／SQL／服務／遊戲／角色、共享 engine／releases／主管／入口均未寫入，無永久刪除。

正式入口仍 r33。保留原 automatic approval review 拒絕：向唯一整合者派發 `ENGINE-R34-ENTRY-ACTIVATE-P26-R1` 的 send_message_to_thread 未執行、未重試，批准仍待答。原文：「此派工會持久修改正式入口與 project-manifest，將 r34 提升為現行產品版本，屬外部／正式承諾；使用者只授權引擎開發，未明確授權這次入口啟用，且政策不以先前助手派工替代該核准。」本輪不代行或改道。

交付後停寫，等待唯一主管接回，不擴張案例或自行派修。
