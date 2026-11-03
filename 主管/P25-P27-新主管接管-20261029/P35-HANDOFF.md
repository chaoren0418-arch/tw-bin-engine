# P35 收斂與 RG2 修補交接

本輪要求的 12 項功能收斂已完成，並選定一項可重現、無 DB／原生／入口依賴的缺陷完成隔離實作與獨立覆核。完整分類及恢復條件見 `remaining-convergence.md`／`remaining-convergence.json`；沒有另開遊戲測試階段。

接受收據 `P35-root-acceptance.json` SHA-256：`04174872b4a7d271776cdb8babd1eb4a8727d0c84ec1cacc8a8ce556adfccb95`。

修補位於 `work/P35-release-generator-rg2-fix-r1/candidate/generate_package_plan.py`，SHA `4fbf32a4d1a5faa12e34631ad2c0e9ee02647ff51947128c69e1894690f13fa2`。只改 package_inventory：納入 product manifest 本身，並拒絕缺少、錯 hash 或錯來源／目標的產品 DLL、audio DLL、manifest、launcher 四身份。原 authority、no_links、collision、source/evidence closure 與其他函式保持不變。

作者 14 個有限 synthetic 案例與覆核者獨立 10 案均符合預期；它們是工具單元案例，不是 24 個新增遊戲測試。兩者各自使用原接受 completed_context 作唯讀真輸入核對，保留 180 source／690 evidence pins 的原 gate。真 inventory 2289→2290，原列全部不變，只加入 7955 bytes 的 build/product-r34-manifest.json；結果與先前精確 PP2 的 files 完全一致。沒有生成新正式 plan、archive 或 release。

主管核驗作者 173 個 artifact、覆核者 140 個 artifact 的完整集合與來源封存，讀取獨立案例及 raw command receipts。作者 seal：`664a0d7df517d7c9bb191c99cf94995843be882eae26302c8cf344998596156a`；覆核 seal：`a210842eabf2b486add553fc41026f5a5468136d8f831134b92d3e1007a3ffb5`。覆核 baseline-01 曾把合法新增的補單 provenance metadata 當違規，保留 exit1／原始輸出；修正檢查為原欄位不變且只允許 planCorrection 後通過。這不是產品失敗。

沿用原兩位，沒有新建對話：

- 作者／唯一整合者：01a17a22-17e4-70c0-9792-166ddb86ea42，turn 01a186a4-8db4-79e2-9929-fc6b320d069e，completed／idle。
- 獨立覆核者：01a185b7-ea67-7053-9bfc-d9d90c1cff8f，turn 01a186a4-9e46-7131-ab4e-27f2f3f63c82，completed／idle。

作者 Goal 實讀 null；覆核者歷史 Goal 實讀 blocked，無 resume API；均未改平台狀態。主管沒有 Goal 工具。平台 Goal 限制不阻止本輪已授權的有限工作交付，也不以文件冒充真正 Goal。

關鍵澄清：真實地圖的 S32/TIL、快取、遮擋及 renderer 已有實作；目前索引明列 46 個未支援區塊，並非所有地圖已通過。S30 箭矢 parser→flight→hit 已有實作；三個未跑的 prep 案例是拾取交互證據，使用合成封包／注入時鐘，不是缺少飛行實作。S30 與換圖 network-loop C52 ACK 是不同路徑，舊合併敘述已更正。

本輪實作結果是封裝工具修補候選，不是遊戲新增功能、正式工具安裝或入口啟用。舊 r2 工具不可變；候選正式版本綁定與 P30 外觀候選串行整合仍可另立精確主管派工，不應說所有工作都被使用者回覆阻塞。其他功能欠缺的是 map-local effect 群組關聯、非6016事件語意、部分格式／素材來源；登入／交易／移動／原生畫面／6016音訊等則欠固定版實機證據及適當服務／安全路線條件。

正式入口及 project-manifest 保持 r33，憑證設定未改。先前 ENGINE-R34-ENTRY-ACTIVATE-P26-R1 被自動批准審查拒絕，因缺少持久切換正式入口的明確批准；未執行、未重試，仍 pending。DB／服務／原生遊戲／角色禁區保持，沒有永久刪除或共享寫入。全案未完成，不提供虛構完成率。
