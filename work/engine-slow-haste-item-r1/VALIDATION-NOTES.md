# 候選離線驗證

最終 candidate helper 實際 377 checks 全過，載入 DLL SHA c64bf0069722e28fc174d70d261b7540da6e3f74ee422d4874cbffb77149364f。Release product 與 helper build 皆成功。只執行 console helper；沒有開正常產品或 GUI、沒有 socket、服務、DB、SQL、原 native/JAR 操作。

## 檢查內容

- 7種 Speed × 6種 Brave；exact haste envelope 的合法單瓶／低位 flags 與錯 ID、Count、charge、bless、flags、name、desc/icon/use；實際 UseSelectedItem 與 C164 owned ID，沒有只測 property 冒充送出。
- Speed0/1/2/unknown 下7類既有用品，以及非零 Brave 保守拒絕；正常0/1各用品固定 delay 回歸。
- 初始與 queued movement/combat/magic/item/ground busy、各 deadline；同值／往返／未知 speed、同值 Brave、S24/S242/S57、session/world/actor/lease、選取／input、死亡／HP、trade/action lock、外觀／校正／位置／武器、設備 revision。
- 公開 API 雙向互斥：queued item 擋其他 producer；move/attack/magic/item/ground admitted producer 擋 item。Speed2 ground 自身即禁止，另外由 Speed0/1 ground positive controls 驗證互斥；不以獨立禁止假裝公開慢速 ground 互斥已證。
- 0/1/2 初始准入後的合法 S255/S67/S24/S242/S57、selection/input/appearance 更新完成；普通非haste用品同路回歸。沒有把送出前快照複用到 completion。
- 同session真S206、獨立S87角色替換、session Cancel、lease reset、直接actor反射替換的 late success/error。S206／S87 經 ApplyWorldPacket 實際入口；S87 的 Status fixture 先合成設定，以產生同world不同actor。直接反射替換屬防禦測試，刻意繞過入口，不聲稱清理 hook 由此被驗證。
- 新世界／session／actor／lease 透過公開 UseSelectedItem 取得新租約，舊成功／錯誤回來不清 busy、不延長deadline、不發事件或故障；新操作可完成且只發一次事件。同歸屬錯誤仍故障、sender false 清自身 busy。
- 真 TickCount64 的100ms admission/completion deadline界限、立即重按拒絕、真 Task.Delay 後恢復；S55 effect191與timer不改自身速度／數量；foreign speed isolation與own權威更新。
- 慢 move/attack/magic 已准入後收到0/1速度回包，既有deadline高水位與動畫保全；r31其他175來源SHA不變涵蓋renderer、flight30/60、effect191。

ActionClock 與 MovementClock 都綁 Environment.TickCount64；item/ground原本即用此真時鐘。沒有 synthetic future 時鐘繞過 last gate。Task.Delay 只用於短 guard 邊界。動作使用真正候選 DLL 的 API、predicate、decoder；actor、背包與 sender 是明示測試替身，沒有證明 socket 排程或原native live race。反射注入 busy／revision／actor 的案例與公開 producer 案例在各 row scope 分開。

第一輪353項已通過，結果保留 evidence/initial-353-checks.json；之後只增加24項缺口檢查，最終377項才計入 result.checks。來源階段36個r30觀察、獨立準備90個r30觀察均未混入此數，也未當作本候選獨立驗收。未重跑3171完整套件；全量同DLL回歸由後續指定整合者負責。

## 身份與保全

source-before.json 精確保留原 accepted stage 的177 pins。source-manifest.json 使用 legacy schema engine-r29-source-pins-v1，但 revision=r32，全部 source 指向本候選獨立副本。changed-files.json、changes.diff、delta列出 Inventory.cs 與 WorldEntry.cs，其他175 source/audio/web逐檔不變；底層 transport 失敗政策沒有修改。

final-base-binding-pins.json 與 evidence/final-base-equivalence.json 另核最終r31release177來源及四產品檔，並核seal/acceptance；主管的14329release檔全量稽核只引用主管證據，不冒充此worker重驗。封存前重新核所有原stage來源、input pins與獨立副本，資產只使用既有r28 read-only consumer junction，未複製14k素材、未寫共享engine/release。

交付為實作者離線候選，尚待獨立review、串行合入及正常產品驗收；不代表P24完成。真Goal實讀blocked，本輪依派工未新建、恢復或更改。
