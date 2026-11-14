# Issue #1208 — f1186-option-labels：Ctrl+V 選項窗「常用選項」勾選框沒有名稱（#1186 SM57 ❌）

階段：code（claude-local，第 1 次）　分支：`task/1208`

## 結論

- **根因不是「執行期才填的 Caption」**（#1201 分流時的推測不成立）。常用選項八個勾選列的名稱是 OptionUI.xml 裡 `Label Desc="…"` 的文字表編號，引擎本來就會查 desc-c.tbl。
- 真正原因：目前的參考客戶端 **kor25 的 desc-c.tbl 在這些編號存的是 `dummyNNNN` 佔位字**。引擎依 #817 的規則不畫佔位字，所以只剩勾選框。
- 本機另外四個客戶端（小草、超人760、雷雨、韓版）的 desc-c.tbl 在**同一個編號**有真正的文字，四個版本完全相同，每個名稱也都對得上那一列的 MouseEvent。所以名稱有來源，**不是編出來的字串**。
- 修正：選項窗的 TextResolver 改用新的 `RefClientTextFill`。規則是參考表（kor25）優先；只有在參考記錄是 `dummy` 佔位字時，才去第二個客戶端（預設小草）查同類同編號的文字。補字表的編號必須和參考表對齊（兩表都有字的記錄，相同比例至少 80%）才會使用，否則不補。其他面板的行為不變。

## 已驗證／推測／待驗證

### 已驗證（靜態讀檔＋建置）
1. **OptionUI.xml 來源**：`D:\381懶人包kor25(原)\Tile.pak:OptionUI.xml`。這次在記憶體重新解出，各 SHA 與 #1186 `native-ui.log` 的 `ui-xml-ref` 一致：Tile.idx `e205c88f…6a6a`、blob `b6e2ac59…047f`、plain `fe32d2fd9d7bff00c80443b9ea9158bb9bf42e3312ff4d8fc3a670cb5b8c846d`。解密用 `docs/reference/rea-scripts/ui_xml_decrypt.py`，腳本在 `scratch/t1208/optionui.py`（不進 git）。
2. **Home 頁（常用選項）八列**：

   | 勾選列 MouseEvent | Label Desc | kor25 desc-c.tbl | 小草／760／雷雨／韓版 desc-c.tbl（四者相同） |
   |---|---|---|---|
   | ShowDropItemNameBtn | 18805 | `dummy18807` | 掉落道具顯示 |
   | ShowNpcNameBtn | 18806 | `dummy18808` | NPC名稱顯示 |
   | MacroScrollBtn | 18803 | `dummy18805` | 快捷鍵視窗點卷 |
   | GetAllItemsBtn | 20826 | `dummy20828` | 全部檢取 |
   | AutoLootPartyBtn | 18808 | `dummy18810` | 組隊自動分配 |
   | TransparentDeadMonsterBtn | 18807 | `dummy18809` | 怪物屍體半透明化 |
   | TopmostWindowBtn | 18813 | `dummy18815` | 遊戲視窗固定在上方 |
   | EnableElfTelControlBtn | 20829 | `dummy20831` | 瞬間移動調整(等級50妖精) |

   - 在 OptionUI.xml 用到的其他編號中，kor25 也是佔位字的有：Desc 18792–18801、18804、18810–18814、20818–20825、20827、20828、20830、20832、20862、20863、21145、21391，以及 String 4042–4045、4408（視窗模式下拉）。這些編號在四個客戶端都有字。kor25 本身有字的是 Desc 18809（可交易）與 String 613、1994–1997、893–896，四個客戶端在這些編號的內容和 kor25 相同。
   - 標題「[常用選項]」是 png 10913（`<Image X="141" Y="149" Png="10913;1"/>`），不是文字，所以截圖裡標題有畫、名稱沒有。這和根因一致。
3. **#1186 log**：`ui-text-ref desc-c.tbl -> D:\381懶人包kor25(原)\Text.pak:#4415 plain sha256 67242c39…d354 (count 22617)` 有成功載入，表示「整張表沒讀到」不是原因。
4. **編號對齊**（兩表都有真字的記錄裡，內容相同的比例；`scratch/t1208/align.py`）：

   | 補字候選 | desc-c.tbl（Text.pak 筆／SHA-256） | desc 相同 | string-c.tbl（筆／SHA-256） | string 相同 |
   |---|---|---|---|---|
   | `D:\381懶人包小草(原)`（預設） | #4339 `eaf72c1a…bdb4`，27356 筆 | 97.9%（16513／16870） | #32852 `6e14b267…754d`，5208 筆 | 90.8%（3498／3853） |
   | `D:\760測試\超人760客戶端` | #5267 `719ec496…dd60` | 98.7% | #37805 `23123bb1…cc78` | 96.4% |
   | `D:\天堂參考用客戶單\雷雨天堂` | #4674 `87388481…8660` | 87.4% | #33883 `fee67cbe…a5e257` | 77.0% |
   | `D:\天堂參考用客戶單\韓版天堂` | #4622 `3c508bbf…d068` | 97.6% | #35982 `1433a73c…a1b` | 97.7% |

5. **查過的客戶端與文字表**（Text.idx／Text.pak 唯讀，以 python 依 `TextArchive` 的格式解 DES；完整清單見 `scratch/t1208/scan.txt`，不進 git）：每個客戶端都查了 desc-c.tbl 與 desc-k.tbl。kor25 另外也查了 desc-e／desc-h／desc-j（筆數 4948–10110，都不到 18803）。
   - **編號超出筆數**：`D:\lineage開發\0-客戶端`（desc-c 17001 筆）、`0-客戶端-公版260707修正門跟icon`、`D:\380\` 底下三個客戶端（J17公版、3.80C、3.81C(1)）、`登入器開發\0-遊戲客戶端client`、`凝時天堂`、`古典經典版`。這些客戶端也都沒有 OptionUI.xml（PAK§4.2）。
   - **是 `dummy` 佔位字**：kor25、泡麵陳（與 kor25 同 SHA）、K23 快樂合併／懷舊合併／超人OG、K25 小宇宙／犬夜、89天堂。
   - **有真字**：小草、超人760、雷雨、韓版。
6. **建置**：`dotnet build engine/source/OfflineClient.csproj -c Release` 成功，0 個錯誤（本機）。

### 推測
- kor25 原版客戶端畫這些 Label 時，可能直接把 `dummyNNNN` 印出來或留白。本輪沒有追原 BIN 的 Label 繪製路徑，也不能在本機啟動 kor25 確認。不論哪一種，都不是「正確的名稱」。補字只是改用其他客戶端裡同一編號的正式文字。
- 兩表都有真字的記錄裡，相同比例不是 100%（小草 desc 97.9%），差異大多是字詞修訂。因為只有 kor25 是佔位字時才補，這些差異不影響已經有字的編號。

### 待驗證（交 `f1186-retest`，真機）
- SM57：Ctrl+V → 環境設定頁的八個勾選列旁邊顯示名稱（字型大小另由 `f1186-uitext-pixel` 處理）；`native-ui.log` 應出現 `ui-text-fill desc-c.tbl -> D:\381懶人包小草(原)\Text.pak:#4339 plain sha256 eaf72c1a… 與參考表相同 97.9%，佔位字照此補`。
- 其他頁（UI 設定／遊戲設定／其他設定）的勾選列名稱、應用程式頁的音量標籤也應出現。
- 音量獨立性與重啟保存（本輪沒有驗這些子項，照原安排交重測）。
- 離線檢查 `option-labels` 與既有的 `option-panel`、`ref-client-text`：本機規定不能執行，交 PR CI（`--module-tests`）。

## 做了什麼（程式）

| 檔案 | 變更 |
|---|---|
| `engine/source/NativeUi/RefClientTextSource.cs` | `RefClientTextSource.IsPlaceholder(kind,value)`；新增 `RefClientTextFill`：參考表優先，`dummy` 佔位字才查補字表；`Agreement` 計算對齊率，門檻 `MinAgreement=0.8`、共同記錄至少 `MinShared=100` 筆；補字根目錄依序取 `TW_REF_TEXT_FILL`（路徑，或 `off`／`0`／`false` 關閉），預設 `D:\381懶人包小草(原)`；與參考客戶端同資料夾、相對路徑、沒有 Text.idx 時不補；日誌寫 `ui-text-fill …`（記錄筆、SHA-256、與小草已知 SHA 的比對、對齊率）。 |
| `engine/source/NativeUi/OptionPanel.cs` | 註冊時改用 `RefClientTextFill.Resolver`（只影響選項窗）；更正檔頭註解（名稱是 Desc 編號，不是執行期 Caption）。 |
| `engine/source/NativeUi/OptionLabelTests.cs`（新） | `[WorldModuleTest("option-labels")]`，自動發現（與既有 `option-panel` 測試同一套機制，CI 以 `--module-tests` 執行；沒有改 `Program.cs`、`DemoChecks.csproj` 或註冊表）。內容用合成的八列 OptionUI 版面，加上合成文字表（記憶體內、假字）。 |

離線檢查 `option-labels` 會斷言：
- 每個勾選列後面都有名稱 Label，`Desc` 對到那一列，位置在勾選框右側 (+18,+1)。
- 佔位字會從補字表取同一編號；參考表有字時用參考表；參考表是空字串時不補；`"300;1"` 的旗標會忽略。
- 補字表編號錯開、共同記錄不足、沒有補字表時都不補。
- `TW_REF_TEXT_FILL` 的各種設定都有測到。
- 畫面上：只有 dummy 時名稱位置沒有字（重現 SM57），補字後有字。

沒有改：`rebuild-lock.json`、`docs/REPORT.md`、其他面板（仍用 `RefClientTextSource.Resolver`）。

## 產出路徑
- 程式：上表三個檔案。
- 報告：`docs/reports/issue-1208.md`（本檔）。
- 本機分析腳本與輸出（不進 git）：`scratch/t1208/{dump_tbl.py,scan_clients.py,scan.txt,align.py,align.txt,optionui.py}`。

## 給下一棒的提示
- **雲端（Claude）審查重點**：是否同意用第二個客戶端（預設小草）補 kor25 的佔位字。若不同意，把 `TW_REF_TEXT_FILL` 預設改成關閉即可回到原本行為，SM57 則改列已知限制。另外可以考慮之後讓其他 P-D 面板（ResetStat、AgitAuction…）也改用 `RefClientTextFill.Resolver`：kor25 desc 有 5733 筆佔位字，小草能補 798 筆。本張依任務範圍沒有動其他面板。
- **f1186-retest**：照上方「待驗證」看 SM57，並從 `native-ui.log` 抓 `ui-text-fill` 那行。使用者若沒有 `D:\381懶人包小草(原)`，log 會出現 `ui-text-fill … 沒有 Text.idx，佔位字不補`，名稱仍不會出現，這時可以設 `TW_REF_TEXT_FILL` 指到 760 或韓版。
- 字太大是另一張 `f1186-uitext-pixel`；名稱出現後如果和勾選框重疊或超出 120px 寬，先看那張的修正有沒有合進來。
