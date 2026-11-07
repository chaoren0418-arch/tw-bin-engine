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
## 最新同步輪：同步公告欄上游與重建 lock（2026-11-07）

- 做了什麼：在既有 `task/188` 執行 `git fetch origin`、`git merge origin/main`，合入 `bbaac76`（含 #266 公告欄）。唯一衝突為 dev lock，使用 `rebuild.py --update-lock` 重建，未手動拼接 hash。本模組接收／呈現與 T100 送出端均未變；共用分派點和測試入口保留雙方功能；`docs/REPORT.md` 完全採 main 版本，未手動修改。
- CI 原因核對：PR #202 目前仍開啟、尚未合併；上一輪 `a8f1d20` 的 build 與 npc-checks 均成功。舊失敗 [run 42316647327](https://github.com/chaoren0418-arch/tw-bin-engine/actions/runs/42316647327) 的 jobs／annotations 確認失敗點為 `dev canonical build + receipt`、退出 2。本輪下載原始 log 被 GitHub 以 HTTP 403 拒絕；沿用前輪已讀 log 的結論，並直接核對失敗提交 `7dd30ac` 的 lock：`sourceCount=176`、實際來源 pins=177，與前輪記錄一致。
- 已驗證：`rebuild.py --update-lock` 核驗 **179 來源／188 pins**；`rebuild.py --build --configfile .github/nuget.ci.config --write-receipt --output scratch/issue-188/build` 成功。產品 0 錯誤、僅既有 MagicUi.cs CS8600／CS8602；launcher 0 警告、0 錯誤。本機使用已安裝 Python312 的 `python.exe -B -X utf8`（PATH 無 python）。
- 已驗證：既有 DemoChecks `combat-admission` **953 項通過**（含本模組 741 項）；`map-continuity` **358 項通過**（含上游公告欄 230 項）；`item-transfer` **73 項通過**。沿用 `--artifacts-path engine/build/issue-188-tests`、`-p:UseSharedCompilation=false`，測試專案建置 0 警告／0 錯誤；未修改測試邏輯。
- 已驗證：`check-no-game-assets.py` pass（3287 個追蹤檔、0 errors）；`git -c core.whitespace=cr-at-eol diff HEAD --check` 通過。
- 產品 DLL SHA-256：`e82b1d0d15bab7105a3c83df74508f40317a830aed72503153690415e088f793`。
- 產出路徑：本報告、`engine/_integration/dev/rebuild-lock.json`；本機證據沿用 `scratch/issue-188/build/`、`scratch/issue-188/{combined-checks,map-checks,item-transfer-checks}.json`；receipt 位於 `engine/_integration/dev/receipt.json`（原有未追蹤檔，不提交）。
- 未驗證：未重跑原素材 929 項檢查，未執行真機／登入／音效驗收；未啟動遊戲、伺服器或 DB，未遞迴刪除。推送後 CI 結果以 [PR #202 checks](https://github.com/chaoren0418-arch/tw-bin-engine/pull/202/checks) 與結果留言為準。
- 給下一棒的提示：沿用 PR #202，Claude／調度器確認最新提交 CI 後交使用者合併；dot 接 M6 smoke，沿用上方多目標、不同 gfx、S42 與權威 HP 核對清單。本輪僅同步上游、重建 lock 與更新此報告。
