# Issue #736 報告：T309 R5-D 小協定差異

依據：`docs/reference/gap-matrix.md` 第 14.6 節 R5-D；PG§5、AN§1、FU3§3、WO 第 6 節。分支 `task/736`。

## 做了什麼

全部用 pefile＋Capstone 靜態讀原版 `TW1608262200.bin`（SHA-256 `e12949cf…41a2`）核對後才改。腳本在 `docs/reference/rea-scripts/t736/`（`dis.py` 反組譯、`xref.py` 找絕對位址引用、`callers.py` 找 `E8 rel32` 呼叫點、`summon_names.py` 對照召喚名稱）。

| 項目 | 原版（已驗證，VA） | 引擎改動 |
|---|---|---|
| S_BLINK 延後回 C52（PG§5） | `sub_931B90`：自己正在走（`sub_A43510`＝目前動作是步行且 `+0xC`≠0）時只記待辦 3（`0x931C06`），由 `sub_A48590` 在這一步結束後送；否則立刻送 C_BLINK | `Teleport.cs`：S149 清掉 Actor 之前先記下這一步的動畫長度（`TeleportStepMs`，只有本地預測步之後才記）。`AcknowledgeTeleport` 算出「最後一次 C29＋步長」，還沒到就先等（提示「這一步還沒走完」），等完再確認請求仍有效才送 C52 |
| op 92 `hold=1` 的 1234 ms 計時器（AN§1） | `sub_4FBA70` hold=1：取消舊計時器、`byte_153CE6A=1`、排 1234 ms（`0x4D2`）的 `sub_4F94F0`，它只做 `byte_153CE6A=0`。`sub_4FAAF0`（節流型）**不管有沒有送出**都設同一個旗標與計時器（`0x4FAC18`）。讀這個旗標的只有兩個輸入處理（`0xDF5557`、`0xF078A7`）：旗標為 1 時不呼叫 `sub_A4C270(0)`（依按住的輸入繼續走） | `TitleAndEmote.cs`：`WorldSession.EmoteHeld`／`StartEmoteHold`（1234 ms，跟世界 epoch 走）。立即型帶 hold 時重新計時；節流型送出或被間隔擋下時都設（已在保留中則不延長）。`Resurrection.cs` 的 `MoveLocked` 改成「S241 鎖 **或** 表情保留」，所以保留期間移動輸入停住 |
| 節流係數（AN§1） | `0x14EBF88` 由加速收包在 `0x94C5B6` 寫入（與 `NormalSpeed` 依據的 `0xA407F8` 是同一個值）＝自己的 Speed；`0x14EC00C` 由 TW16 op 92 的 `sub_935260` 在 `0x9352B3` 寫入＝自己的 Brave。比較是 `now−last > 間隔`（`0x4FABFB` jbe，等於也不送）；`dword_13A9624` 只在真的送出時更新 | `ThrottleMs(false, Actor.Speed, Actor.Brave)`；改成嚴格大於；記「上次送出時間」而不是「下次可送時間」，每次嘗試依當下速度重算 |
| op 122 Lv≥50 分支（FU3§3） | `sub_E08A20` `0xE08AF2–0xE08B20`：`byte_1542AA1==2`、等級 ≥ 50（`cmp 0x32`）、`byte_13A931C` → 和戴傳送控制戒指一樣開書籤窗；這條路徑（`0xE08B22` 起）**不檢查限制地圖**，276 只在直接送 C6 的路徑（`0xE08BA9`）。`byte_13A931C`＝本機設定檔（`sub_59DD30`，設定物件 `+9`；`sub_89A5F0`／`sub_999B50` 由位元 0x08 寫入），等級 < 50 時 `0xEC99EB`／`0xEED7B9` 清成 0 | `RingWindows.cs`：`ElfBookmarkTeleportRules`、`WorldSession.ElfBookmarkTeleportOption`（預設關）、`TryOpenElfTeleportWindow`。戒指路徑沒開窗時才看這個分支；職業取選角清單的 `Class`（FU6§2）。送出照舊 3.81 `HD`（`TELEPORT.java` 以書籤 id 查 `CharBookTable`，不看戒指） |
| 召喚暫行清單補怪物名稱 | `summonList.xml` 的 `desc` 是 `desc-c.tbl` 行號（13683 起）；r29 `names.json`（同一張表）的內容是「怪物名^(魅力 8/1, 10/2, …)」 | `SummonFallbackUi.cs`：`SummonFallback.Describe`／`Caption(m, resolve)`，畫面用 `ResolveNpcText("$desc")`，`^` 前是名稱、後是魅力說明；查不到退回原本的「召喚編號」 |
| op 197 拿掉 5 秒上限 | 原版沒有這個上限（S241 在 TW16 名稱表沒有對應，WO§6） | `Resurrection.cs`：刪 `MoveLockTimeoutMs`／`MoveLockClock`，鎖只在 S87、世界重建、Reset 時解除；新增 `ServerMoveLocked`（只看 S241） |

**和 gap-matrix 不同的地方（重要）**：14.6 與 FU3§3.1 把這個分支寫成「王族 Lv≥50」，依據是 SS2§7 的推定。FU6§2 已經查證 `byte_1542AA1`＝選角清單的職業碼，0＝王族、2＝妖精（盟戰要求 `== 0`、夜視加成看 2／4）。所以這其實是**妖精** Lv≥50 的分支，本 PR 照 BIN 實作成妖精。

**範圍外但必要的改動**：
- `engine/_integration/dev/resurrection-tests/ResurrectionChecks.cs`（CI `resurrection` 組）：原本有 3 處斷言「5 秒後自動解除」並用到被刪的 `MoveLockTimeoutMs`／`MoveLockClock`，改成「S87 才解除、沒有時間上限」。
- `engine/_integration/dev/title-emote-tests/TitleAndEmoteChecks.cs`（CI `combat-admission` 組）：「剛好 2500 ms 可送」改成「2500 ms 不送、2501 ms 可送」（對應 jbe）。
- `docs/protocol/resurrection.md`：5 秒上限的列與兩句說明。

## 怎麼驗證

- `py -3 -B -X utf8 engine/_integration/dev/rebuild.py --build --configfile ../.github/nuget.ci.config`：sdk／product／launcher 的 restore 與 build 都是 0，`PASS dev`（暫時 lock，沒有寫入 `rebuild-lock.json`）。worktree 沒有離線 `vendor`，所以用 CI 的 NuGet 設定。
- `dotnet build engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release`：0 個錯誤（含改過的 ResurrectionChecks、TitleAndEmoteChecks）。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：`pass`，0 錯誤。
- 新離線測試 `engine/source/ProtocolFollowupTests.cs`（`[WorldModuleTest("protocol-followup")]`，自動發現）：S149 沒有步行時立刻回 C52、走到一半時延到該步結束且不重複送、世界重設後丟掉延後的確認；hold 1233／1234 ms 邊界、職業外型沒有 hold、Reset 清除；節流的 2500／2501 ms 邊界、被擋下仍設保留且不延長、Speed／Brave 係數（1875、5000、1406、937）與加速後 1876 ms 可送；S241 沒有時間上限；妖精分支的職業／等級／選項條件、限制地圖也開窗、王族與 Lv 49 不開；召喚名稱的 `^` 拆分與查不到時退回。
- **依使用者規定沒有在本機執行任何測試**（不跑 `--module-tests`／DemoChecks），測試結果以 PR 的 CI 為準。

## 未驗證什麼

- 所有測試都還沒跑過，等 CI。
- 沒有開遊戲、伺服器或 DB；全部行為沒有真機驗證（待驗證：走路中被傳送時 C52 的時機、變身表情後按住滑鼠約 1.2 秒不動、召喚清單的中文名稱）。
- `TeleportStepEndsAt` 用「最後一次移動送出時間」：如果最後送的是轉向而不是一步，會多等，最多一步的時間（推定可接受）。
- 1234 ms 保留在引擎是併進 `MoveLocked`，所以也會擋攻擊、施法、門與傳送點，並讓 `ClientLogin` 清掉按住的輸入；原版只擋 `sub_A4C270(0)`（它是否也處理按住時的攻擊沒追）。`sub_A231C0` 是否允許同一個 callback 重複排入是推定（節流型不延長）。
- 立即型**不帶** hold 時，原版設 `byte_153CE69`，由 `0xA4B573` 在表情動作（66–69）結束後 700 ms 清掉，也擋同一條輸入路徑；本 PR 沒做。
- `self+547`（S103 型 8，三段加速）的節流係數：`TimedBuffs.cs` 不保留型 8，不在本組可改範圍，仍固定 false。
- `byte_13A931C` 的選項名稱、設定檔預設值沒查；引擎預設關，**沒有 UI 開關**（只有 `WorldSession.ElfBookmarkTeleportOption` 屬性）。原版在等級 < 50 時會把選項清掉，引擎只在判斷時要求 Lv ≥ 50。
- 戒指路徑（`AbilityStatus.cs`，不在本組範圍）在限制地圖仍先顯示 276，原版開窗路徑不查限制地圖。
- `^` 在原版 `CSummonUI` 裡怎麼排版（推定換行）沒追；CI 沒有名稱表，畫面上的名稱只靠單元測試的假表驗證。
- S241 拿掉上限後，若原服送了 S241 卻沒有後續 S87，角色要靠傳送卷軸或重新開始才能再走（原版行為仍未定位）。

## 產出路徑

- `engine/source/Teleport.cs`、`TitleAndEmote.cs`、`RingWindows.cs`、`SummonFallbackUi.cs`、`Resurrection.cs`
- 新檔 `engine/source/ProtocolFollowupTests.cs`
- `engine/_integration/dev/resurrection-tests/ResurrectionChecks.cs`、`engine/_integration/dev/title-emote-tests/TitleAndEmoteChecks.cs`
- `docs/protocol/resurrection.md`、`docs/reference/rea-scripts/t736/*.py`、本報告

## 給下一棒的提示

- gap-matrix 下一輪：op 189、92、122、52、197 可以重新判定；op 122 的依據要改成「妖精（FU6§2）」，並更正 14.6 與 FU3§3.1 的「王族」。
- 妖精選項要給使用者用：需要一個 UI 開關（例如書籤分頁或選項頁，`BookmarkUi.cs`／選項窗不在本組範圍），最好也存進本機設定。
- `AbilityStatus.cs` 的戒指路徑：照 `0xE08B22` 拿掉開窗前的限制地圖檢查（276 只留給直接施放）。
- `TimedBuffs.cs` 保留 S103 型 8 後，把 `flag547` 傳進 `EmoteThrottleMs`。
- 若要做立即型的 700 ms（`byte_153CE69`），起點是 `0xA4B540` 附近（動作 66–69 結束時排 `sub_A400C0`）。
- 真機（Codex）：在 P24Lab 走路中踩傳送點，確認 C52 在該步之後；用 6080 外型做選單表情後按住滑鼠，約 1.2 秒才開始走。
