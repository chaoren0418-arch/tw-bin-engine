# Issue #857：T373 R9-M 召喚窗收尾

依據：`docs/reference/followups-9.md` §3（Q7-3 召喚窗 `CSummonUI`）、§16.2（R9-M）、M39；`gap-matrix.md` 15.11。分支 `task/857`。

## 做了什麼

| 項目 | 做法（依據） | 狀態 |
|---|---|---|
| 點怪物＝立即召喚並關窗 | 怪物格按下開始拖曳；沒拖曳就放開＝`Host.Choose` 後關窗（`sub_C01090` → `sub_C003F0`）。拖出去再拖回來、在別格放開都不召喚。 | 已驗證（BIN）＋實作 |
| OK＝關窗 | OK 跟關閉鈕、取消一樣，只關窗、不送（`sub_C006E0`）。 | 已驗證＋實作 |
| 停用條件 | `SummonCatalog.Usable`：等級公式 `(等級−28)/4+7 ≥ action%256`（沿用 `RingLevelAllows`）**而且**魅力 ≥ minCha（`sub_C00BF0` → `sub_B0B6F0(1)`）。沒收到等級或魅力就當停用，不推定。 | 已驗證＋實作 |
| 停用格能不能點 | 停用格不能點、不能拖；滑鼠移上去：可用格畫 `OverSurf`，停用格畫 `DisableOverSurf`（13）。**OverSurf 改成滑鼠移上去才畫，不再表示選取。** | **推測，真機確認（M39）** |
| 快速 3 格 | 按下（513）＝直接召喚該格並關窗（`sub_C00990` → `sub_C00310`）；從怪物格拖來放開＝放進這一格；拖來的怪物已經在別格時兩格互換（「或交換」怎麼換是推測）。空格畫 XML BaseSurf 4958 和 `?`；有內容時畫怪物圖、等級、徽章（怪物圖缺少時才畫空格底圖）。 | 行為已驗證；交換方式、有內容時不畫 4958 是推測 |
| 本機保存 | `ISummonQuickStore`；正式面板用 `SummonQuickFile`，存在 `runtime/user/summon-quick.json`（`{"v":1,"quick":[id 或 null ×3]}`，先寫 `.tmp` 再取代）。讀不到或格式錯誤時三格都空，並記一行日誌。存的 id 不在清單裡就當空格。 | 原版格式待驗證（`sub_C009F0`），這個格式是引擎自己定的；全角色共用 |
| desc 提示與 `^` | 怪物格的提示是 `desc-c.tbl` 第 desc 行，空的快速格用 XML 的 `Desc`（13714）。`^` 是強制換行，最多 64 行。框寬＝最寬一行（6 px × Big5 位元組數）+20，再進位到 4 的倍數；框高＝12 ×（行數＋1）。字色 0xBDF7（RGB565 → 189,190,189），底色是 XML `BackColor`。文字表在執行期從主客戶端 Text.pak 讀（`PlayerChatProtocol.Desc`）；讀不到就不畫提示。 | 規則已驗證（`sub_B0DAF0`）；框的位置（格子正下方置中，夾在 800×600 內）、文字內距、沒有 `^` 時不自動換行都是推測 |
| 徽章 4944＋type | `summonList.xml` 的 `type` 屬性改成選讀（沒有就是 0），徽章＝XML `LevelLabel_Image` 的 Surf＋type。本機清單沒有 type，所以一律是 4944。 | 已驗證＋實作 |
| ESC（27） | 面板的 `Key` 吃 ESC（不含按住重複、放開、Shift+Esc），等同關閉鈕（`sub_C01060`）。 | 已驗證＋實作 |
| `IUiClosablePanel`／`--classic-ui` | `Close()` 的行為沒變（`UiStackEscTests` 照樣通過）。WinForms 的暫行清單（`SummonFallbackUi.cs`）沒動。 | — |

召喚後關窗的做法：只在面板這邊把這一次請求藏起來，**不呼叫** `Host.Close`。原因是送出是非同步的，`SendSummonChoice` 會檢查 `SummonCurrent(revision)`，先關掉請求會讓送出被判成過期。宿主不能送（`CanSend=false`）時不召喚，窗也留著。

改的檔案：
- `engine/source/NativeUi/SummonPanel.cs`
- 新檔 `engine/source/NativeUi/SummonWindowTests.cs`（`[WorldModuleTest("summon-window")]`，自動發現）
- **範圍外：`engine/source/NativeUi/SummonPanelTests.cs`**。不在可改清單內，但原本的期望值寫的是舊行為（選取後按 OK 送出、快速格不能點、OverSurf 表示選取），不改就會紅燈。改動只有三處：第 4 段互動改成新行為（測試裡的角色等級從 12 改成 28，讓等級公式成立）、第 6 段「選取」改成「滑鼠移上去」、`RealClient` 不再讀已經拿掉的 `Selected`。其他段落沒動。

沒改 `WorldSession.cs`、`UiPanelRegistry.cs`、`Program.cs`、`DemoChecks.csproj`、`PetUi.cs`、`rebuild-lock.json`。

## 怎麼驗證

- 編譯：先 `dotnet restore engine/source/OfflineClient.csproj --configfile .github/nuget.ci.config -r win-x64`，再 `dotnet msbuild engine/source/OfflineClient.csproj -t:ResolveReferences;CoreCompile -p:Configuration=Release -p:RuntimeIdentifier=win-x64`。只有 `WebPanel.cs` 的 WebView2 三個錯誤，這是本機環境的老問題（同 #848、#854）；這次改的檔案（含測試）沒有任何錯誤或警告。完整建置以 CI 為準。
- `py -3 -B -X utf8 engine/tools/check-no-game-assets.py`：pass（4343 個追蹤檔，0 錯誤）。
- 新的離線測試 `summon-window` 涵蓋：
  - `type` 解析；停用條件（魅力、等級公式邊界、沒有等級或魅力）；
  - 停用格不能點；滑鼠移上去時 Over 和 DisableOver 的像素；徽章 31+type 的像素；
  - 按下＋放開＝召喚＋關窗，而且不呼叫 Close；拖出去再拖回來、按在一格放在另一格都不召喚；
  - OK 只關窗；ESC 關窗，重複鍵、放開、其他鍵不吃；
  - 快速格：拖入、保存、拖到格子外、互換、拖回自己那一格；有內容和空格的畫法；按下召喚；重新載入時讀回；不認得的 id；停用時不召喚；宿主不能送時不召喚；
  - 提示：`^`、64 行上限、框的尺寸（ASCII 和雙位元組）、字色、空快速格用 13714、位置、Bounds 包含提示框、底色、停用格也有提示、沒有文字或沒有表時不畫、夾在 800×600 內；
  - 設定檔字串的序列化、反序列化和錯誤格式。
- 依本機限制，**沒有在本機執行任何測試**；結果看 PR 的 CI。

## 未驗證／推測（真機再確認）

- M39：停用格滑鼠移上去畫 13、能不能點；目前做成不能點、不能拖。
- 提示框的位置、內距，以及沒有 `^` 時要不要自動換行。
- 快速格「交換」怎麼換；有內容時要不要畫空格底圖 4958。
- 快速格原版的存檔格式，以及是不是分角色存。
- 拖曳時游標上的怪物圖（`SummonDrag_Image`）沒有畫。
- 等級公式不成立時原版會跳 743；這只會發生在宿主的送出路徑（local 來源在 `Pets.cs`／`AbilityStatus.cs`）。面板已經先擋掉停用格，所以從面板送出的都是條件成立的。
- 召喚後關窗只是在本機藏起這一次請求。送出失敗的話，宿主那邊的請求還開著（WinForms 寵物分頁上的「關閉召喚選擇」還看得到），要等下一次請求才會再開。

## 資源

沒有宣稱缺少任何資源。用到的 surf（4958、4944、5029、13、3397）和 `Desc` 13714 都來自 `SummonUI.xml`，`followups-9.md` §3.1 已經確認本機都有。執行期一律從客戶端讀，沒有複製任何資源進 repo。

## 給下一棒的提示

- 真機（Codex／dot）要看的：
  - 等級 28 頁上點一隻怪物，應該送出一次 C125（server 來源）或施放技能 51（戒指），窗立即關掉。
  - 把怪物拖到快速格後重開客戶端，格子內容要還在（檔案在 `runtime/user/summon-quick.json`）。
  - 滑鼠移到停用格上，看原版畫的是 13 還是 5029，能不能點（M39）。
  - 提示框的位置和換行要跟原版截圖比對。
- 如果要把 743 顯示在面板路徑，需要在 `ISummonUiHost` 加一個回報，要改 `PetUi.cs`，不在這次範圍內。
