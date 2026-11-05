# Issue #81：P01–P24 本機研究盤點

## 做了什麼

依任務先讀 AGENTS.md 與 docs/REPORT.md，fetch 後從 origin/main 建立 task/81，基準 `0b747de4e230c72906c830be821de322894acbf2`。逐一盤點24個階段根層 HANDOFF／result，掃描主管175個直接子目錄、33份根層 HANDOFF 變體及101份主管根層 Markdown 標題；核對目前 engine/source、P25的32主列／16補充紀錄／12待辦與真機／展示文件。

產出 [完整盤點](../inventory/p01-p24.md)：24列階段表、5個未明確涵蓋／部分涵蓋子範圍（U01～U05）、3個歷史界線、已涵蓋範圍去重說明、主管完整目錄清單與來源 SHA-256。

核心結論：弓箭單發、彈藥、箭矢呈現確有產品程式及歷史實驗驗收，但無明確 P25驗收列，也未出現在現有真機文件的弓箭驗收中。另有自身死亡／復活的獨立離線修正與物件AOI生命週期需明確對帳。多外觀／速度／拾取／服務／地圖已有相關ID，未重複當新功能缺口。

## 怎麼驗證

- 24階段一一核對名稱、HANDOFF／result存在及SHA-256；解析兩份P25 JSON，區分泛關聯與明確條件。
- 核對所有表列產品檔案存在及內容錨點；P21的C123／gfx66與技能4的C6／gfx167分開。
- 以文件核對真機版本與限定結果；沒有把歷史controller／DOM測試當外部鍵鼠通過。
- 檢查通過：24列完整、317個本機文件連結存在、兩份文件UTF-8有效、147個唯讀來源pin未變；git diff --check與提交白名單只允許兩份指定文件。
- 一般exec執行器因setup refresh初始化錯誤無法啟動，改用核准的專案命令路徑；rg／python不在PATH，改用PowerShell及既有py -3.12。首次廣泛規範檔搜尋遇不相關work目錄拒讀，已停止並改用git ls-files與指定路徑，未變更ACL。

另記共用工作目錄競寫：驗證時另一工作已切到task/77並提交e21241d（#77建置報告），本次來源pins皆未變。確認追蹤工作區乾淨後切回原task/81，校正文件基準為0b747de並重驗；不將#77額外提交帶入本分支。

## 未驗證什麼

未建置或執行產品／歷史測試，未開遊戲／服務、未查DB，未讀機密設定、未修改研究／主管／release／環境／產品／docs/REPORT.md，未複製pak／map／sprite。未遞迴驗封全部歷史raw／PNG／資產。沒有新native驗收；P10仍0／11，P24根交接所列新版live未完成，後續整合另由主管文件辨識。

## 產出路徑

- docs/inventory/p01-p24.md
- docs/reports/issue-81.md

暫存盤點／檢查腳本與唯讀來源pins只在scratch/issue-81，不提交。工作區原有未追蹤真機圖片、receipt、runs／runtime保留不動，沒有git add -A。

## 給下一棒的提示

由 #76 主控／Claude接回：U01～U03應先合併成有限弓箭驗收範圍；U04死亡恢復、U05物件生命週期可併入既有戰鬥／物件任務或明確排除。對H01-local～H03-local決定歷史研究與相容性邊界後，再去重計算封頂數，不能直接沿用「新增0張」，也不能把5個子項直接算5張Issue。

R18已記多外觀；R03／R04已記normal／haste／slow；MAP-F3是一般群組、MAP-F4是map-local actor關聯，勿混淆。現有docs/demo比dev-retest更新，戰鬥音效已接線但事件真機對應仍缺；不可沿舊報告重派從零實作。

本任務為resource，未產生plan/design拆票JSON。依指派提交並推送task/81，由調度器接續PR／Claude審查、使用者合併；本輪未自行留言或變更Issue狀態。
