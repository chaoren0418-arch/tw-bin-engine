# Issue #546 — T228：伺服器訊息改用完整字串表（protocol-gaps A）

依據：`docs/reference/protocol-gaps.md` 第 3、8 節（T225／#532）。分支 `task/546`。

## 做了什麼

- **新檔 `engine/source/TextArchive.cs`**：原客戶端 `Text.idx`／`Text.pak` 的共用唯讀讀取器，從 `MagicRendering.cs` 抽出。idx 列與 pak 內容用 DES-ECB 解碼（常數與原本相同），不到 8 byte 的尾段不解碼。`Text.idx` 本來就有重複檔名（本機 110 個），所以 `Entries` 會列出全部；`Single(name)` 取單一檔案，檔名重複就報錯（和原本 `list.spz`／`list.spr` 的檢查一樣）。另有 `FromBytes` 給合成測試用。內容只在記憶體解碼，不寫入磁碟。
- **`MagicRendering.cs`**：`EffectSpriteCatalog` 改成透過 `TextArchive.Open(clientRoot)` 讀 `list.spz`／`list.spr`。拿掉私有的 `DecodeText` 和 `Index(stem,text)` 的 Text 分支。Sprite 封存的讀取沒有改。
- **`ServerMessageText.cs`**：
  - 新增 `ServerStringTable`：執行期從 `GroundSpriteReader.ClientRoot`（`TW_CLIENT_ROOT`，預設 `D:\lineage開發\0-客戶端`）讀整張 `string-c.tbl`，以 CP950 解碼。第一行是筆數，之後一行一筆，id 從 0 開始。小於筆數但沒有對應行的 id 讀成空字串（照 `sub_4EAFF0`）。只在第一次用到時載入，載入後快取在記憶體。
  - `Decode`：有字串表時，**id ≥ 筆數**回傳 `Displayed=false`、`Text=""`（照 `sub_942B40` 整包丟棄）；其餘 id 依 `sub_933850` 格式化，`Recognized=true`。讀不到字串表時（例如 CI 沒有客戶端）保留舊行為：硬寫的 125／189／337／403 加上「原服訊息 {id}」。
  - `Format`：依本輪重新反組譯的 `sub_933850`（VA `0x933850`／FO `0x532C50`）CP950 路徑：
    - `%`＋數字 n：n 小於參數數就代入第 n 個參數，否則什麼都不輸出。參數只照字面複製、不再解析；參數裡的第一個 `^` 換成空白（`sub_941E40`），後面接 `\<`（`strcat_s`，字串由 `"\"`＋`'<'` 組成）。
    - `%d`／`%o`／`%s`：不輸出（只有 CP949 分支會處理，判斷在 `dword_1379208 == 949`）。
    - `%`＋其他字元：只輸出該字元（`%%`→`%`、`%n`→`n`）；`%` 在字串結尾時停止輸出。
  - `ServerMessageReport` 加兩個有預設值的欄位：`Displayed`、`Raw`（原生格式化結果，含色碼）。`Text` 是顯示用的版本，去掉 `\fN`、`\FN`、`\<`。
  - `UseTable(...)`（internal）只給測試替換字串表用。
- **`ServerStatusMessages.cs`**：`IsInformational(id,args)` 遇到被丟棄的 id（`ServerMessageText.IsDropped`）也回傳 true，所以這種 S71 不會被當成交易、裝備、NPC 等待中流程的回覆（原版整包丟棄，什麼都不做）。
- **新檔 `engine/source/ServerMessageTextTests.cs`**（接 `--module-tests`）：
  - `server-message-text`（33 項）：用自己編的表、在記憶體建 DES 編碼的合成 Text 封存，不用遊戲資源。測 TextArchive 的 DES 來回轉換、尾段、重複檔名、idx／pak 越界；字串表的筆數、id 從 0 開始、尾端空行；`%` 的每條規則；有表時 id ≥ 筆數被丟棄、`IsInformational` 回傳 true；S71 經過 `WorldSession.Receive` 的結果；沒有表時的舊行為。
  - `server-message-text-client`（7 項）：本機有客戶端時，核對解碼後 `string-c.tbl` 的 SHA-256 `4ebbc1a7…43e9`、筆數 3484、125／189／337／403 和舊的手寫結果一致、3484 和 4492 被丟棄。沒有客戶端時，改測「沒有表、有記錄錯誤、走內建文字」。
- `engine/_integration/dev/rebuild-lock.json`：用 `rebuild.py --update-lock` 重新產生（合併 origin/main 後又重產一次，沒有手動改 hash）。

## 怎麼驗證

- `rebuild.py --update-lock` → `dev-inputs-verified`（241 個來源檔）。`rebuild.py --build --configfile .github/nuget.ci.config` → `PASS dev`，product DLL SHA-256 `ec47397501e5094d32797ce295532852437481eae4a94f5190f6d5eea9aa134e`（合併 main 之後）。worktree 沒有 `engine/vendor`，所以改用 CI 的 NuGet 設定 restore。沒有新的警告，只有原本就有的 `MagicUi.cs(20,62)`。
- `P11.ClientEngine.exe --module-tests`：結束碼 0，6 組 257 項全過（agit 44、castle 62、recovery-death 72、server-message-text 33、server-message-text-client 7、world-extensions 39）。把 `TW_CLIENT_ROOT` 指向不存在的目錄（模擬 CI）也是結束碼 0。
- `DemoChecks`，用 CI 會跑的模式。模擬 CI（沒有客戶端）：map-continuity、combat-admission、item-status、afflictions、resurrection、polymorph-cadence、buddies、item-transfer、doors、pickup、hypertext 全部結束碼 0。native-ui-resources 在假的 client root 下失敗，原因是它會從別的路徑找到真客戶端、再讀 Tile.pak，跟本任務無關；改用真的 client root 跑就通過。`combat-admission` 包含 `AttackSkillChecks`，它用合成的 Text 封存跑 `EffectSpriteCatalog`，等於驗證了這次的抽取。
- **第二次同步 origin/main（2026-11-08，含 #446／#519／#523／#548／#551）**：`rebuild-lock.json` 取 main 版後以 `--update-lock` 重產（250 個來源檔，沒有手動改 hash）；`--build` → `PASS dev`，product DLL SHA-256 `23ff2d7b85213406fbe70fb89c155f24639f2a9e50d5c8e763e7ecdf70194e84`。`--module-tests`（照 CI 的 Start-Process 方式）結束碼 0：有客戶端 8 組 387 項全過；`TW_CLIENT_ROOT` 指向不存在目錄 8 組 382 項全過（新增 packetbox-status 86、server-features 44）。
- **第三次同步 origin/main（2026-11-08，含 #463／#510）**：`rebuild-lock.json` 取 main 版後以 `--update-lock` 重產（261 個來源檔）；`--build` → `PASS dev`，product DLL SHA-256 `bc781cbbba66592e8fa2d3bc25ab9c55d36e150123da9aebbd21e8a261076aa2`。`--module-tests`（CI 的 Start-Process 方式）結束碼 0：有客戶端 9 組 425 項全過；`TW_CLIENT_ROOT` 指向不存在目錄 9 組 420 項全過（新增 ability-status 38）。
- **第四次同步 origin/main（2026-11-09，含 #516／#520）**：`rebuild-lock.json` 取 main 版後以 `--update-lock` 重產（262 個來源檔）；`--build` → `PASS dev`，product DLL SHA-256 `899a92c559e5ec8ee2b6e8701cca4710b20aba492b8940d2ce9235ff36b9e6ba`。`--module-tests`（CI 的 Start-Process 方式）結束碼 0：有客戶端 9 組 425 項全過；`TW_CLIENT_ROOT` 指向不存在目錄 9 組 420 項全過。模擬 CI 的 DemoChecks map-continuity、combat-admission、item-status、hypertext、pickup 結束碼 0。
- **第五次同步 origin/main（2026-11-09，含 #514）**：`rebuild-lock.json` 取 main 版後以 `--update-lock` 重產（265 個來源檔）；`--build` → `PASS dev`，product DLL SHA-256 `374a3de602acee5dcee6940beece3b288825681ad1e77f120badee88443e563e`。`--module-tests` 結束碼 0：有客戶端 10 組 479 項全過；`TW_CLIENT_ROOT` 指向不存在目錄 10 組 474 項全過。模擬 CI 的同一組 DemoChecks 結束碼 0。
- `engine/tools/check-no-game-assets.py` → pass。字串表沒有進 repo，測試只用自己編的字串，加上原本註解裡就有的 125／189 文字。

## 未驗證／已知影響

- **本機有客戶端時，下列既有測試會失敗**（CI 沒有客戶端，不受影響）：`display-notices`（以及包含它的 `map-continuity`）中的「notice S245 unknown message stays labeled」期望 id 999 顯示成「原服訊息 999：%1」，「S105 700」期望「原服訊息 700」，但這兩個 id 現在會從真表取得文字。`ground-tests` 的「unexpected 125 argument stays unknown」期望多一個參數時不認得，但原版會忽略多出的參數、照樣顯示。這些斷言是舊的 4 個 id 時期的行為，測試檔不在本任務可改的範圍，**沒有改**。`ground-tests` 在 worktree 裡還缺 `work/issue-51/fixtures-49.json`（沒有進 git），所以沒有實際跑。
- 實機整合測試（`GroundIntegration`、`RangedIntegration`、`MagicIntegration`、`EquipmentIntegration`）用 `LastServerMessage=="原服訊息 125"` 之類的字串等待，有客戶端時會等不到。同樣不在可改範圍，**沒有改，也沒有跑**（不啟動遊戲／伺服器）。
- id ≥ 筆數時，`Inventory.cs` 仍然會把 `LastServerMessage` 設成空字串（`Inventory.cs` 不在範圍）。UI 可能會顯示一個空的「伺服器：」。流程判斷已經由 `IsInformational` 擋下。
- 沒有模擬的部分：參數的 `$nnn` 名稱查詢（`0x5417C0`；維持原樣，由顯示層的 `ResolveNpcText` 解析）、256／512 byte 緩衝上限、`\n` 與其他色碼的實際畫法（推定，待實機確認）。特殊 id（2533、834、875、3387 等）的另外處理沒有實作。
- 本機表的 SHA 與 protocol-gaps 第 3 節一致。TW16 引用的 ≥3484 id（4492、4536、4715）依原版規則丟棄，沒有補字。

## 產出路徑

- `engine/source/TextArchive.cs`、`engine/source/ServerMessageText.cs`、`engine/source/ServerStatusMessages.cs`、`engine/source/MagicRendering.cs`、`engine/source/ServerMessageTextTests.cs`、`engine/_integration/dev/rebuild-lock.json`
- 本報告 `docs/reports/issue-546.md`

## 給下一棒的提示

- 開一個小任務，更新上面列出的舊斷言（`DisplayNoticeChecks.cs` 的 999／700、`ground-tests` 的 125 多參數、四個 `*Integration.cs` 的「原服訊息 NNN」等待條件），改成以 `LastServerReply.Id` 判斷，或測試時用 `ServerMessageText.UseTable(null)` 固定成沒有表。
- `Inventory.cs` 收到 S71 時，若 `!LastServerReply.Displayed` 可直接不改 `LastServerMessage`（完全對齊原版丟棄）。
- B1（S250 聊天欄系統訊息子碼）與 WO 第 5 節的 1784／1785 現在可以直接用 `ServerMessageText.Table`、`ServerMessageText.Format`。
- `DisplayNoticeText.Template` 的 8 個手寫範本（134–138、551、552、637）也可以改用字串表，但要先核對 S245／S105 的原生路徑是否同樣走 `sub_933850`。
