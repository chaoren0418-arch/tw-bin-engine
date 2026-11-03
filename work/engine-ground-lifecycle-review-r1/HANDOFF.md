# ENGINE-GROUND-LIFECYCLE-REVIEW-PREP-R1

獨立覆核準備完成；candidateReviewed=false。只寫本目錄，未讀未封存作者候選，未修改產品或任何舊 seal。等待主管指定 exact sealed r33 candidate。

基準已綁最終接受 r32：release seal `46e9249224e768a1ac09fe99c08a9edf387833295022147aa046c206769fa507`；final baseline SHA `0c645bf5c3a515fd5da88cf28c834a4b2967aed268946d45fb5a6ebe9f03fcb7`。實際 probe 使用 accepted stage DLL `c64bf0069722e28fc174d70d261b7540da6e3f74ee422d4874cbffb77149364f`。獨立核對 stage/final 各177來源及四身份相等；與r31相比僅 Inventory.cs、WorldEntry.cs 變更。原 stage baseline 未改。

結果分類：

| 輸出 | rows | baseline assertions成立 | future safety成立/缺口 | observations |
|---|---:|---:|---:|---:|
| baseline-probe.json |140|61|14 /10|55|
| extended-probe.json |195|156|9 /4|26|
| supplement-probe.json |17|17|0 /0|0|

合計352筆：234個baseline assertions成立、23個future contract成立、14個現況缺口、81個觀察。這不是352個通過。原140只在r32執行一次，逐ID/kind/pass與舊r31完全一致，10gaps/55obs分類保留。新增4 gaps為drop final actor/lease/magicSending/nextMagicAt，與共用SendGround缺口相符。

24個新owner準備觀察均 old admitted、Ready=true、GroundAvailable=true，但r32沒有reset ground owner，new admission被擋；全部candidatePending，不算新owner保護通過。snapshot已納GroundNotice/Detail/state/busy/deadline/lease/pending/events，正式候選須真正先admit新owner再釋放舊callback。actor切換需要synthetic matching Status，與真world wrapper路徑分開。

公開producer正向與互斥、肉/箭3routes部分/全量、手動drop wire/eligibility/early-reply、current error、r32 Slow haste/item owner及ground交叉忙碌均有獨立controls；詳 review-contract.md、testplan.md、baseline-comparison.json。未用作者實作決定預期。

建置0警告0錯誤、socket events0；只用memory sender與真DLL/packet wrapper，時鐘為真Environment.TickCount64、私人dotnet/NuGet/TEMP，DOTNET_GENERATE_ASPNET_CERTIFICATE=false。未執行3602 suite、14k完整release重驗、native/JAR/class/meta重驗、原生客戶端、GUI、DB/SQL或服務。已封存pickup source只pin選定材料，不重新推進其未通過的來源qualification。

產物：result.json、baseline-binding.json、baseline-comparison.json、三份probe JSON、review-contract.md、testplan.md、harness、差異檔、sealed-material-binding.json、source-pins.json、build/probe logs、artifact-manifest.json。

封存後唯讀核驗：

```powershell
python 'D:\lineage開發\TW1608262200研究\work\engine-ground-lifecycle-review-r1\verify.py'
```

重跑需主管新授權的獨立新目錄；不可在此seal下覆寫。將harness與run.ps1複製到新目錄、建立其tmp，再以 `& '<new-root>\run.ps1' -Mode baseline`、`-Mode extended`、`-Mode supplement` 執行；目前run.ps1針對accepted r32 stage，正式candidate需於新review root明確綁其DLL。setup/analyze/seal scripts均拒絕覆寫既有manifest。verify.py只讀563個外部pins與本輪artifacts，不掃14,342 release全體。
