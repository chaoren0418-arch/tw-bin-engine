# Issue #42 — T045 設計：音效與一般地面物品

日期：2026-11-05；design，第 1 次嘗試；分支 `task/42`。

## 做了什麼

已讀根目錄 AGENTS.md 與 docs/REPORT.md，fetch origin 後依指示 `git checkout -B task/42 origin/main`；基線 `b573c7be069789d1f4c6c8121e9338a51dc88530`。既有未追蹤真機證據、issue-37.md、receipt 與 runs/runtime 未納入本次變更。

僅讀原客戶端、engine/assets、repo 程式及 work 文件。定位 TW13081901.txt 的技能效果事件 109/127/97/804 與依外觀/武器分組的近戰事件；核對十二個 WAV 的格式、SHA 與載入缺口。定位金幣 gfx7/3247 的 type9 metadata、既有 $4 名稱、肉/箭/藥水資料與 Sprite 索引。提出事件派送、受 pin 資產載入、物品 catalog、數量輸入與權威更新設計，拆出六項後續任務（三 resource、兩 code、一 dot test）。

提交前偵測到其他工作將共享目錄切到 task/41 並完成 commit `6417765`。核對暫存區與工作樹無 tracked 差異後切回既有 task/42，基線仍為 `b573c7b`；未重設 task/41 或納入其證據提交。

## 怎麼驗證

- SHA-256 重算原 TXT、LiTo.bin、Text.idx/pak、Sprite.idx/pak、引用的產品 JSON/程式/歷史依據及候選 WAV；Sprite.pak 整檔有重算，與定向 SPR payload hash 分開列示。
- TXT 依原 bytes offset 定位 gfx，正規化 CRCRLF 後逐動作比對；四技能的事件尾綴與既有 magic JSON 相符。gfx0 的整段 entryHash 不同，與 work entry0 逐行比較定位到 action50 前導空白差異；沒有宣稱整版相同。
- 以 plain28 索引長度、record 與 PAK bounds 定向讀取六個 SPR slice；不匯出素材。主包與 16 個 shard 的精確 3247-0.spr 查找未命中，僅列為缺口。
- 唯讀 RIFF/WAVE 標頭，確認候選 WAV 的 22050Hz/16bit PCM；未播放。四技能與列出的近戰音檔皆不在產品 sound，AudioAssets/AudioCore 仍限 3332/1340。
- 確認 ObjectAppearance 忽略 events，GroundRules/renderer/Web 各有 36/66 或肉/箭限制；數量 NumericUpDown 已存在，後續應擴充並驗證而非重造 UI。
- 文件重現片段與 JSON schema、唯一 id、合法 stage、依賴存在/無循環、UTF-8 與 git diff --check 於交付前驗證。此任務未建置產品，沒有建置產物。

本機一般沙箱起初因 `helper_unknown_error: setup refresh had errors` 無法建立程序；必要 shell 操作經 require_escalated 自動審查後成功。未更改 ACL 或安全設定。rg/python 命令不在 PATH，後續改用 git grep、Python launcher `py -3`；試探腳本錯誤只在 scratch，沒有執行產品或歷史研究腳本的寫入入口。

## 未驗證什麼

本任務完成的是有缺口標註的設計交付，**沒有完成原生事件符號、多候選選擇或 S87 分類呼叫鏈證明**。來源數值存在不等於已知原客戶端在每次揮擊/命中實際選中哪個音效。金幣實際 S87 gfx、名稱數量格式、其他物品模板/stackable/drop 規則與原生圖像仍待後續契約；Text.idx 解碼未核定。沒有查 P24Lab DB、讀伺服器/JAR/機密設定、啟停或附加遊戲/服務。未驗收可聽輸出、畫面、登入、金幣丟撿、06 傷害。

## 產出路徑

- `docs/design/sfx-and-ground-items.md`：結論分級、來源/格式/offset/SHA、實作與驗收閘門、重現方式。
- `docs/plans/issue-42.json`：六個後續任務，runner 寫在各 text，含範圍/驗收/禁止與 depends_on。
- `docs/reports/issue-42.md`：本報告。
- `scratch/issue-42/`：一次性唯讀解析、findings.json 與文件寫入/驗證腳本，不提交，不是必要的永久交付依賴。

## 給下一棒的提示

調度器/Claude 先審查本設計並依 JSON 建單；不要因為已有四個數值就直接把 [ 候選全播。codex-local 先接 sfx-native-contract 與 ground-native-contract，dot 接 ground-server-contract（靜態原服證據，不連 DB）。code 必須等各自依賴 qualified，保留未知值；完成後 dot 接 native-retest，06 必須使用不會移動的合法測試目標。

不可直接改 engine/assets：它是封存 junction；新的 catalog 放可寫來源，音檔引用原客戶端 Sound。金幣 gfx7 與背包 IconId7（箭）不同，gfx3247 尚缺精確資源命中，不以猜測補值。沒有改產品、lock、docs/REPORT.md 或既有證據；只提交上列三個 docs 檔案。
