# Issue #752 報告：T318 用 PakViewer 查遍所有 .pak

## 做了什麼
- 讀了 PakViewer 的 README 與 CLI 原始碼，確認用到的命令都只讀 pak：
  - 用到的是 `src/PakViewer.Cli` 的 `pak list`／`info`／`extract`、`xml decrypt`。
  - `PakFile` 以 `FileAccess.Read` 開檔。
  - GUI 版 `PakViewer.exe -cli` 是 WinExe，主控台沒有輸出，所以改用 `pakviewer-cli.exe`。
- 列出 `D:\lineage開發\0-客戶端` 全部 24 個 idx 的清單，共 308,323 個檔名，放在 `scratch/pak-index/`（不進 git）。
  - 頂層 Text（L1）、Tile、Sprite、Sprite00–15。
  - `LiTo\` 下的 LTtext（L1）、LTsurf、LTsprite、LTtile、LTicon。
  - `launcher\TW13081901.pak` 沒有 idx，打不開。
- 另列 3 套參考客戶端（kor25／t89／ly）的 Tile、Text、Sprite*.idx，放在 `scratch/pak-index/alt/`，用來判斷缺的資源能從哪裡補。
- 需要的檔案解到 `scratch/pak-extract/`，並算 SHA-256（`HASHES.tsv`）。
- 本機 Tile.pak 的 65 份 XML 全部解密，搜視窗名稱。
- 產出 `docs/reference/pak-resources.md`，回填 `gap-matrix.md`（14.5 K3 列的原因欄、`AgitAuctionUI` 列的更正、新增 14.9），在 `INDEX.md` 加一列。

## 結論
- **K3 的 18 列，在 `0-客戶端` 找到 1 列**：5:`sub_8444A0` 字型。用的是系統 `MingLiU` 和 `Font\*.fnt` 散檔，不在 pak 裡，應改歸 K5。
- **其餘 17 列**：本機全部 pak 都查過，仍找不到，搜過的 pak 清單與關鍵字見 PAK§2。但這 17 列的 XML 在 kor25／t89／ly 的 `Tile.pak` **都有**。
- **更正**：先前寫「他版也沒有」的 `AgitAuctionUI.xml`，3 套參考客戶端都有。
- **先前標「本機沒有」的項目**：
  - `monlist-c.html`、`summonList.xml`、`recipe.tbl`：本機有。
  - surf 9183／9184、`SpellList.xml`、字串 4118：本機沒有，參考客戶端有。
  - `ServerName.xml`、`LoadingScene.xml`（B16）：本機沒有，參考客戶端有。

## 怎麼驗證
- kor25 解出的 XML，解密後的 SHA-256 與 windows-2 §8、followups-3 記錄的相同。所以 PakViewer 的解法與先前腳本一致。
- `python -B -X utf8 engine/tools/check-no-game-assets.py`：通過，輸出見 PR。
- `gap_count_r5.py`：14.5 的分類沒有移動，計數不變。

## 未驗證
- 參考客戶端 XML 與 TW16 版面是否相同：只依事件名推測。
- ly 第 4118 筆的對齊：推測。
- `StatInfoUI`／`ServerName` 沒有 `Surf` 屬性的原因。
- `.tbt` 的顯示方式。
- 字型改動要真機比對。

## 產出路徑
- `docs/reference/pak-resources.md`
- `docs/reference/rea-scripts/pak/pak_index.ps1`、`pak_extract.ps1`、`pak_find.py`
- `docs/reference/gap-matrix.md`（14.5、4.1 `AgitAuctionUI`、14.9）、`docs/reference/INDEX.md`
- `scratch/pak-index/`、`scratch/pak-extract/`：不進 git

## 給下一棒的提示
- **需要使用者決定一件事**：是否同意在執行期讀參考客戶端（建議 kor25，缺圖時補 t89／ly）的 UI XML 與 surf。不複製進 git，做法沿用 `TW_SPELLLIST_CLIENT` 的前例。
  - 同意 → 開 PAK§6 的 P-A（`RefClientXmlSource.cs`）→ P-B（surf 備援）→ P-D（逐窗面板）。
  - 不同意 → 17 列維持 WinForms 備援，並裁定終態。
- P-E（MingLiU 字型）不必等這個決定，可以直接開 code 任務。
- 重跑方法：在 repo 根目錄依序執行 `pak_index.ps1 -WithAlt`、`pak_extract.ps1`、`py -3 -I …/pak_find.py`。
