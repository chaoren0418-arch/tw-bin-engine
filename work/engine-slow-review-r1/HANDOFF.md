# ENGINE-SLOW-REVIEW-R1：獨立驗收準備

**狀態：awaiting-candidate。** 已完成獨立原始契約、oracle與封存r28基底失敗重現。最後一次目錄檢查僅見實作者base-manifest與來源準備檔，沒有封存候選manifest／HANDOFF／result；未讀未封存實作、不做無限輪詢，候選可用後再由主管續派覆核。

## 本輪邊界

唯一寫入work/engine-slow-review-r1。已停止所有orphan／DB方向，沒有查DB、socket、UI、啟停服務、啟動正常產品、子代理、新聊天或模型變更。自己的真正get_goal=null；依最新派工及根AGENTS.md，沒有建立替代Goal或修改平台狀態。此準備完成不代表完整候選審查完成。

開始已保存initial-source-pins.json；固定BIN及JAR身份逐檔hash通過。封存r28產品DLL實讀SHA應為 `7762951fba9a2d37d08e2d40a34dfa7f2a6f2219b93689a031a566a8fd8f8cb8`，基底宣告seal `83770091f8051466d5dccb420210cd0567ffef5a1158e8f7ebf5c7667127bfcd`；本輪不宣稱重新核驗整份seal。engine/assets僅讀，測試只複製三個小型catalog／integrity檔到本目錄，確認不共享file identity／link count1；helper引用DLL亦為獨立副本。原資產、正式engine、r28與實作者目錄皆未改。

## 已有獨立證據

- `native-oracle.json`：固定BIN的36組原x86執行向量；Speed2基底加倍。明示synthetic metadata與中和callee，沒有原生遊戲實測。
- `jar-oracle.json`：固定JAR純calculate函式36組×MOVE/ATTACK/SPELL=108核對。只跑離線helper，不初始化世界、DB或遊戲服務。
- `baseline-probe.json`：實際封存r28 DLL的61項離線檢查，42符合／19重現Speed2缺口。12項Speed2數學拒絕、提示缺失、MovementAvailable不開放、0→2送出等待未延長、2→0／2→2排隊路徑不可達，以及慢速實際准入／deadline不可達。這些是預期的基底失敗，不是helper測試未完成。
- 0/1倍率、原S255狀態轉換、未知mode／Brave維持拒絕、遠端不污染自己、未知物件不創造、倒數不自動解除、取消清除及封鎖狀態下的攻擊／施法安全檢查成立。部分SlowCadence案例因基底拒絕准入而未進入後續分支，不能稱慢速正向通過。
- 所有helper均無Form／socket。WorldSession、LoginConnection以明示反射建立合成已入世界狀態，sender為記憶體委派；正常登入／滑鼠UI／renderer及伺服器實機不在此次已跑範圍。

## 優先交主管的兩項風險

1. `MovementAvailable`也放行攻擊與施法。只把Supported擴到2會使原本840ms攻擊、1320ms光箭直接沿用快間隔；固定JAR在Speed2要求1680／2640。排隊後才收到Speed2更要取消或重新驗證。獨立helper已準備四項檢查。
2. 遠端renderer當步以最新速度重算進度，0→2可能倒退、2→0可能提前結束。這是有行號與算式支持的靜態風險，尚非候選缺陷；要以候選renderer／時鐘向量確認。自己的NetworkStep現有固定時長契約應保留。

細節、來源語意、0/1轉換、同值重報、取消、物品門檻及未執行項目見CONTRACT.md。

## 候選交付後的覆核程序

先讀封存manifest並核對來源／候選SHA與file identity，不以檔名推定封存。比較變更只讀、定位精確候選行號；候選若含額外Brave／DB／共享資產修改先列範圍違反。用同一獨立oracle與helper對候選DLL重跑；遇到接口／安全政策改動先核對等價性，不把測試適配誤稱產品缺陷。另補候選的renderer、當步動畫、完整滑鼠route取消、跨session舊callback及物品消費鏈測試，依實際變更決定範圍。缺陷必須附檔案行號、實際／預期與重現輸出。

現在沒有候選接受或拒絕結論。不可把61項基底檢查或實作者自測冒充候選通過。

## 命令

本目錄下：

```powershell
python -B -X utf8 .\oracles.py
python -B -X utf8 .\run_probe.py
# 候選封存並經主管派工後，使用候選真實絕對DLL路徑：
python -B -X utf8 .\run_probe.py <candidate-dll-absolute-path> candidate
python -B -X utf8 .\verify.py
```

oracles.py使用根manifest解析舊路徑；沒有執行含舊路徑的歷史腳本。run_probe建置的是隔離console helper，不是正常產品。DOTNET/NuGet/temp與所有輸出固定本目錄；命令／exitcode／DLL hash在baseline-commands.json。封存後優先verify，不任意重建已交付證據。
