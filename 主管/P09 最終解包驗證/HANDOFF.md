# P09 獨立展示套件交接

從 [README](README.md) 開始；[學期報告](report/semester-report.html)可離線閱讀，[Markdown](report/semester-report.md)可修改。姓名／學號／班級／課程欄位待填，未捏造個資。

本包保留 accepted P08 source/assets/web/vendor bytes。兩個 launch*.cmd 只新增缺少建置輸出時先 build.ps1、失敗退出。原 P08 程式名與虛擬主機名保留，不依賴上游路徑。未打包 prepare.py、原 BIN、全機 runtime、快取、build 或歷史失敗測試。

正式 P09 回歸結果在 evidence/p09-regression、evidence/p09-web；外部鍵盤在 evidence/p09-native。共226項 P06、959項 C#協定、43項 Python、15項 socket、8個 TCP/UI情境56項、6個 Web情境48項。原生 D7 線路以原指令 oracle核對，欄位wasd3時座標/方向/步數不變，F10/W/3後一格移動且D2。一次工具拒絕F10已保留，重新觀察後重試；外部選格干擾明示，不宣稱無干擾桌面或滑鼠驗證。

```powershell
pwsh -NoProfile -File .\build.ps1 -Clean
python -B .\scripts\integrity.py
.\launch-offline.cmd
.\launch-test.cmd
$env:PYTHONUTF8='1'
python -B .\scripts\verify.py --clean --output .\runs\review-regression-01
python -B .\scripts\verify_web.py --output .\runs\review-web-01
```

Windows、PowerShell7、SDK10.0.300、WindowsDesktop .NET10、WebView2 Runtime及Python為前置需求。SDK nupkg固定且本地還原；一般server/launcher只用Python標準庫，oracle另需Unicorn2.1.4。執行資料寫runs/runtime等可重建目錄，不改正式manifest。HTML實際瀏覽證據見evidence/report-validation.json；完整矩陣路徑有獨立存在檢查。

ZIP與解包重建／啟動的最終收據位於本包外的P09交付根目錄result.json及evidence/unpack-validation，避免自我hash循環與ZIP遞迴。本包manifest列不可變artifacts，delivery_hashes.json覆核manifest/result兩個envelopes。

限制：單64×64區塊含來源外伸物件、單角色站立/移動八方向；碰撞/尋路/動態群組unknown，可穿水或建物。資產/BIN版本配對、原565模式未知。只有固定fixture post-init轉向，沒有原服握手、seed、身份登入或正式服相容；同方向語義重複回覆不可辨認。WebView2是新Windows內嵌面板，原Awesomium ABI/生命週期未知，OS/COM永不返回仍可能等待。沒有部署、原遊戲執行或全機安裝。

P09完成回報只供主管驗收，不代表主管總Goal或新階段授權。使用computer-use技能僅操作自有程式；沒有向第三方傳訊。
