# Issue #188／T101：法術與箭矢回包（第3次：同步輪）

## 做了什麼

- 已合入主控指定的 origin/main M6 契約，依 #188 核准範圍完成接收端、MagicRendering.cs／ProjectileRendering.cs；未修改 T100 送出端。
- S30 依完整布局／flight 分派，支援不同 action／gfx；S42 支援多目標、type0／8、有／無傷害延伸，整包驗證通過才更新狀態。HP 維持 S225／S237 權威來源，不從顯示傷害重複扣血。
- 移除箭矢 gfx==66 入列限制。不同 gfx 在執行期唯讀 Text／Sprite 封存，依自己的 sprite、方向、影格、時間、blend 與 impact gfx 送入 compositor；缺少或損壞素材明確記錄，不代用66。保留既有66／167呈現路徑。
- S42 依 type 分別建立可見目標特效或來源位置方向特效，空集合保留來源特效；補上 caster0 法術與無玩家 cadence 的投射物。跨圖／reset 清除飛行；傷害動畫與權威 HP 分開。
- 原 scratch 封包測試已提交為 demo-tests/AttackSkillChecks.cs，另加 AttackSkillRenderingChecks.cs。既有 CI 的 combat-admission 入口會一併執行，也可單獨指定 attack-skills。

## 怎麼驗證（已驗證）

- 先 `rebuild.py --update-lock`，再 `rebuild.py --build --configfile .github/nuget.ci.config --write-receipt --output scratch/issue-188/build`：產品與 launcher 建置成功。產品僅既有 MagicUi.cs CS8600／CS8602；launcher 零警告。
- `dotnet run --project engine/_integration/dev/demo-tests/DemoChecks.csproj -c Release -- . scratch/issue-188/combined-checks.json combat-admission`：**953項通過**（既有戰鬥212項＋本模組741項）。包含解碼、截斷／壞欄位的原子拒絕、HP回包、兩種不同投射物的實際 draw／像素、方向／影格／深度／blend、法術飛行與命中gfx、S42、caster0、缺素材、跨圖與reset。合成素材只在 scratch 生成，不是遊戲證據。
- 另以 `attack-skill-resources <TW_CLIENT_ROOT> <原專案/engine/assets>`：**929項唯讀原素材檢查通過**，涵蓋66／167／180／193／214／219／221／2349／2989／11722；比對既有法術cache的影格邊界、像素、錨點及深度。
- 工作期間再合入 T100／聊天上游，dev lock 由工具重建（174來源／183 pins），整合後重建及953項測試仍通過；另跑 T100 `--protocol-only`：691項通過。最終產品DLL SHA-256：`ad110103aa5f72a11c0d4e5c8e8f3f9fd3ee7e5cd6220ebf152c62b5299368bc`。
- check-no-game-assets.py：pass，0 errors；`git -c core.whitespace=cr-at-eol diff --check`：通過。GitHub CI 最終狀態另見 PR #202 checks 與本次結果留言。

## 未驗證／限制

- 未啟動遊戲、伺服器、DB，未登入、操作或播放音效；未改正式設定、封存、原素材或原專案檔案。
- 真機動作節奏、S42方向型錨點、多目標同時呈現、實際HP／音效由 M6 smoke 驗證；離線通過不能代替畫面驗收。
- 執行期 reader 支援已核對的 plain28／SPR 格式；沒有宣稱任意其他封存格式、所有 gfx metadata 音效／附屬效果事件都已實作。不存在或不合格式的gfx有 EffectResourceErrors 診斷，不借用其他圖像。

## 產出路徑／下一棒

- 來源：engine/source/{AttackSkillPackets,Magic,Combat,MagicRendering,ProjectileRendering}.cs；dev滾動lock。
- 持久化測試：engine/_integration/dev/demo-tests/{AttackSkillChecks,AttackSkillRenderingChecks,Program}.cs；格式／來源SHA及呈現政策：[attack-skill.md](../protocol/attack-skill.md)。
- 本機證據：scratch/issue-188/{combined-checks.json,resource-checks.json,build/,receipt.json}。重跑前先建產品與restore DemoChecks；合成測試不依賴本機遊戲素材。
- Claude審查 PR #202；合併後由 dot 依既有授權做 M6 smoke，檢查其他箭矢（2349／2989）、不同法術、S42多目標／方向、受擊抑制target0與權威HP。不要把 hitFlag 當HP差值。
## 最新同步輪：合入角色管理（2026-11-07）

- 做了什麼：在既有 `task/188` 執行 fetch／merge，合入 `origin/main` 的 `0380ae6062804b976fe32e5ac2a20b0b611e42ac`（含 #195／PR #219 角色管理）。本輪僅同步、重建 dev lock 與更新本報告，未新增功能。
- 衝突處理：唯一衝突是 dev lock，直接用 `--update-lock` 重新產生，未手動拼接 hash。本模組來源與共用分派點均無衝突、未修改；角色管理來源、測試與文件採 main。`docs/REPORT.md` 與 main 完全一致，未自行編輯。
- CI 原因已驗證：重新讀取 [run 42316647327 原始 log](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42316647327/job/128230764225)，失敗步驟為 `dev canonical build + receipt`，輸出 `REFUSED: sourceCount 176 != pinned source files 177`、退出 2。同步前 head `7d0edbd` 的 [build CI 已通過](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42321997372)；PR #202 仍開啟、尚未合併，不把合併預覽當成 main 已合併。
- 已驗證：先 `rebuild.py --update-lock`，再 `rebuild.py --build --configfile .github/nuget.ci.config --write-receipt --output scratch/issue-188/build`；**183 來源／192 pins**，產品與 launcher 建置成功。產品僅既有 MagicUi.cs CS8600／CS8602，launcher 零警告／零錯誤。使用本機 Python312 的完整 python.exe 路徑，參數為 `-B -X utf8`；NuGet 與暫存皆位於 worktree。
- 已驗證：DemoChecks `combat-admission` **1,019 項**（含本模組 741 項與上游受擊抑制 66 項）、`map-continuity` **358 項**、`item-transfer` **73 項**；CharacterManagementChecks **218 項**、LoginChecks **396,589 項斷言**、ItemUseChecks **426 項**全通過。四個測試專案採 `--artifacts-path engine/build/issue-188-tests`、`-p:UseSharedCompilation=false`，建置零警告／零錯誤；未修改測試斷言。
- 已驗證：`check-no-game-assets.py` pass（3,302 追蹤檔、0 errors）；`git -c core.whitespace=cr-at-eol diff HEAD --check` 通過。
- 產品 DLL SHA-256：`6e0d6d825419eaa76fc4e40a151cacb4dbfbad8f594f4a2c83a403d8af3af17f`。
- 產出路徑：本報告與 dev lock；main 帶入角色管理檔案。本機證據 `scratch/issue-188/build/`、`scratch/issue-188/sync-checks.log`、`scratch/issue-188/{combat-admission,map-continuity,item-transfer,character-management,item-use-checks}.json`；重跑腳本 `scratch/issue-188/sync-checks.ps1`。原有 receipt 已備份至 `scratch/issue-188/receipt-before-sync.json`，更新後的 `engine/_integration/dev/receipt.json` 不提交。
- 未驗證：未重跑原素材 929 項檢查，未執行真機／登入／音效驗收；未啟動遊戲、伺服器或 DB，未遞迴刪除。推送後 CI 結果見 [PR #202 checks](https://github.com/chaoren0418-arch/tw-bin-engine/pull/202/checks) 與交付摘要。
- 給下一棒的提示：沿用 PR #202；Claude／調度器核對最新提交 CI，交使用者合併。dot 接 M6 smoke，核對不同 gfx、S42 多目標／方向、權威 HP 與受擊抑制；合成封包通過不代表真機畫面驗收。
