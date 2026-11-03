# R2 validation boundary

已完成：r1/candidate/review封存與來源pins唯讀核驗；實際180來源／10changed＋3added；projection32mapping／24new＋8reuse與resource JSON/csproj對帳；actual candidate DLL PE resource 239766bytes/SHA核驗；原review174＋10 rows/ordinal/IDs/helper receipts/24risk/findings對帳；21項有限Python拒絕條件測試；5份真native文件的Draft202012 schema驗證。沒有執行產品或任何suite。

Formal receipt到齊後仍依原次序：readonly gate→fresh isolated stage→同DLL baseline4337＋93obs＋accepted pickup184＋0obs→stage封存／主管接受→serial canonical product/launcher一次→同DLL完整4521＋93→另一份release授權與完整source/resource/evidence package。此處4521只是future計畫，不是本輪結果。

Pickup兩組必須用execution-builds/main-174及supplement-10的exact source body。main的Candidate/Fixture/Focused與supplement含Supplement.cs的完整snapshot已pin；最後harness不是main同源替代品。只有HintPath/output/root／產品SHA metadata適配，逐diff與occurrence映射保存，不能改expected或functional expressions。輸出source snapshot與原helper hash的關係明記；經路徑適配重建helper可能hash不同，不冒稱原hash。

Root argument必須讓Root/assets、expected references、baseline-pins可用，並與當次產品assets一致。AppContext ../../assets由helper位於產品build下保證。fixture51檔只列pins，本輪copy0；未來fresh或existing exact pins後才依授權配置，沒有新junction或全包asset copy。`run_probe.ps1`只供原review replay，不能直接拿來驗canonical。

其餘r1要求保留：ground checker只有exact accepted DLL hash適配；735assertions/81obs與24new-owner observations分類不變；real900/3000 clocks與owner assertions不改；兩個Weapon46舊「無動畫」測試body/expected不改；baseline151含29aggregate rows/116assertions＋web35，不偽造個別151IDs；utility既有CS8602單獨保留。

Canonical在baseline收集前先綁actual product/launcher command receipts和raw logs；UTF8-sig errors=replace並記replacement數。failed/partial probes原樣保存；explicit selected-attempt manifest完整對argv/cwd/DLL/ordinal，collect-only不執行subprocess、不重跑已通過suite。外部pins先絕對SHA/size核驗再containment分類；SYSTEMROOT大小寫單一、certgeneratefalse、private caches。

共享engine/releases/root/主管及所有sealed roots不在本派工寫入授權內。R2所有default入口只驗證，missing receipt拒絕；stage/build/canonical/release仍需主管下一份精確材料與派工。
