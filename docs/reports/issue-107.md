# Issue #107／T005：規則與建置文件同步

日期：2026-11-06；階段：code，第 1 次嘗試；分支：`task/107`。

## 做了什麼

- 先讀主專案 `AGENTS.md` 與 `docs/REPORT.md`，再 fetch；遵照本任務的特定要求建立獨立 worktree，未在主目錄 checkout。基準為 `origin/main@1b718bc2a672b9f875acd902c773aa3795855a64`。
- `CLAUDE.md` 狀態節縮短為 dev 滾動 lock、r33 正式入口及 PLAN／TASKS 導引；建置入口、Ubuntu CI 與位元可重現證據同步至目前實作。
- 逐字加入指定的 2026-11-06 dot 授權段落與出處，更新工作方式的實機測試執行者；逐字同步 `AGENTS.md`、Cursor 正文，保留 front matter。
- `docs/BUILD.md` 僅改第 92 行建置平台說明與「CI 怎麼跑」整節，補齊 YAML 的 Python 設定、Windows 目標參數、Linux 原生 PacketTraceChecks、SKIP、artifact 與無 assets／不跑 check-only 的限制。

## 授權原文來源與取得範圍

已核對 [#77 的本機調度器留言](https://github.com/chaoren0418-arch/tw-bin-engine/issues/77#issuecomment-6675086964)，GitHub API `created_at` 為 `2026-11-06T02:18:26Z`；留言轉述使用者已親自在 dot 對話授權，dot 已回「收到」。本次只照錄任務指定的第一段，沒有把主控建議文字當成使用者原文。

**後續條款未取得**。上述留言原文以「…」省略後續；已搜尋主專案 docs、scratch 的 issue-77／issue-71 文字紀錄及本機調度器的 tw-bin-engine #77 日誌，未找到完整原文。未直接取得 dot 對話全文，不推測或補寫條款。本機日誌搜尋僅讀相關文字，未讀機密設定或調度器資料庫。

## 怎麼驗證（已驗證）

### 正文逐字同步

以 UTF-8 讀取 Cursor 檔，僅移除開頭 `---` 至下一個 `---`（含其換行），寫入 worktree 的 `scratch/issue-107/project-body.md`，再執行：

```text
git diff --no-index --exit-code -- CLAUDE.md AGENTS.md
stdout: （空白）
exit: 0

git diff --no-index --exit-code -- CLAUDE.md scratch/issue-107/project-body.md
stdout: （空白）
exit: 0
```

另外比對基準版本：Cursor front matter 未變；「對話分工與語言」「目標」「目錄」「AI 產出規則」皆未變。BUILD.md 遮去指定的建置平台段落與 CI 節後，與基準內容相同。最終 diff 人工覆核確認未修改其他規則。

### 路徑與實作依據

在尚未提交、HEAD 等於上述 main 基準的 worktree，對以下每個路徑執行 `git -c core.quotepath=false ls-files --error-unmatch -- <path>`，並以 `git cat-file -e HEAD:<path>` 確認基準內容存在；全部 exit 0。ls-files 合併輸出如下：

```text
.cursor/rules/project.mdc
.gitattributes
.github/nuget.ci.config
.github/workflows/build.yml
AGENTS.md
CLAUDE.md
PLAN.md
TASKS.md
docs/BUILD.md
engine/NuGet.Config
engine/_integration/dev/packet-trace-tests/PacketTraceChecks.csproj
engine/_integration/dev/product-build.ps1
engine/_integration/dev/rebuild-lock.json
engine/_integration/dev/rebuild.py
engine/build-product.ps1
engine/global.json
engine/source/OfflineClient.csproj
啟動引擎.cmd
```

CI 指令中的 `source/…`、`_integration/…`、`../.github/…` 均依 YAML 的 `working-directory: engine` 解析成上述路徑；`engine/` 是上述追蹤來源的父目錄。

原 CI 節保留的 receipt、evidence、obj log 是 YAML 指定的產物路徑；assets 與套件快取也不是 main 的追蹤來源，不能宣稱 ls-files 能驗出這些執行時檔案。這些路徑及其用途已從 YAML、rebuild.py 與既有文件核對，本次沒有建立它們；唯一新交付路徑是本報告。授權段落沒有新增本機檔案路徑。

- YAML 的 6 條單行 `run:` 指令均逐字出現在更新後的 BUILD.md；runner 為 `ubuntu-latest`，工作目錄及執行順序已核對。
- `rebuild.py` 的非 Windows 分支對產品與 launcher 的 restore／build 加入 `-p:EnableWindowsTargeting=true -r win-x64 -p:SelfContained=false`；只查 Windows apphost 存在，不執行產品。
- `engine/build-product.ps1` → dev `product-build.ps1` → dev `rebuild.py --build` 呼叫鏈已讀取核對。

### Issue／PR 編號

已讀取 GitHub Issue／PR 實際內容，非僅從本次任務抄錄：

| 編號 | 核對結果 |
|---|---|
| [#23](https://github.com/chaoren0418-arch/tw-bin-engine/issues/23) | T011，r35 的 N01 真機基線 |
| [#37](https://github.com/chaoren0418-arch/tw-bin-engine/issues/37) | T042，dev 局部真機補測 |
| [#71](https://github.com/chaoren0418-arch/tw-bin-engine/issues/71) | DEMO-2，dev 真機及截圖交付；不是所有項目通過 |
| [#67](https://github.com/chaoren0418-arch/tw-bin-engine/issues/67) | T050，Ubuntu 交叉建置任務 |
| [PR #72](https://github.com/chaoren0418-arch/tw-bin-engine/pull/72) | #67 的 Linux CI／rebuild 修改；已合併 |
| [#77](https://github.com/chaoren0418-arch/tw-bin-engine/issues/77) | T062 DEMO-3；上列留言為授權段落出處，不視為本輪真機完成 |
| [#91](https://github.com/chaoren0418-arch/tw-bin-engine/issues/91) | T006，兩目錄位元可重現；另讀其報告確認相同來源／SDK 下三個 DLL 的 SHA-256 相同 |
| [#107](https://github.com/chaoren0418-arch/tw-bin-engine/issues/107) | 本次 T005 文件同步任務 |

### 變更範圍

以下為納入報告後的 `git diff --cached --stat` 輸出（僅指定四檔加報告）：

```text
 .cursor/rules/project.mdc |  19 ++++----
 AGENTS.md                 |  19 ++++----
 CLAUDE.md                 |  19 ++++----
 docs/BUILD.md             |  21 +++++----
 docs/reports/issue-107.md | 112 ++++++++++++++++++++++++++++++++++++++++++++++
 5 files changed, 151 insertions(+), 39 deletions(-)
```

`git diff --check` 與 `git diff --cached --check` 均通過；未改產品程式、lock、TASKS.md、PLAN.md 或 docs/REPORT.md。

## 未驗證什麼

- 本輪未建置、執行 rebuild 核驗、check-only、產品、遊戲或任何服務，也未連線 DB。既有 #91 與真機證據是來源核對，非本輪重測結果。
- 未取得授權後續條款；不保證文件同步即可消除 dot 的安全審查阻塞，也不替 dot 判定本輪操作是否符合授權。
- BUILD.md 的工具需求表、參數表及常見錯誤表仍有舊的 Windows 建置限制，末段也仍寫實機由使用者執行。本任務明定其他段落不要動，因此保持原樣，列為後續文件一致性問題。
- 本機 sandbox 啟動失敗（`helper_unknown_error: setup refresh had errors`）；本輪使用獲准的非 sandbox 命令完成文件與 Git 操作。沒有因此重試任何遊戲操作。

## 產出路徑

`CLAUDE.md`、`AGENTS.md`、`.cursor/rules/project.mdc`、`docs/BUILD.md`、`docs/reports/issue-107.md`。

## 給下一棒的提示

調度器／Claude 審查 `task/107`，由使用者合併；本輪不修改入口、PLAN／TASKS 或報告索引。完成推送後移除本任務 worktree，保留分支。dot 依既有直接授權及實際安全審查續接真機任務；若取得完整授權原文，再由主控安排精確補錄。BUILD.md 範圍外的殘留舊敘述交主控決定後續修正文案。
