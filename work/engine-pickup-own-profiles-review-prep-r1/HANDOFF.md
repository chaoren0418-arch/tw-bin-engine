# ENGINE-PICKUP-OWN-PROFILES-REVIEW-PREP-R1

準備完成，尚未對任何未來候選執行。僅寫入本fresh root。原bounded metadata／已解碼block是全部body期望來源，未以產品輸出當expected，也未載入／建置／執行產品helper。

交付416個透明body PNG及RGBA fingerprints／bounds、13個分組contact sheets＋1overview、42精確pair×8方向×2速度×4影格的2688列planned mapping、336個current-profile terminal重用slots、294項profile/action時長資料、672個active-window scenarios／1344個待測age samples，以及14類具體輸入／期望／coverage slots。

本次1118個靜態資料斷言全部通過，308個來源pins。2688列是planned mapping，不是2688個runtime assertions；14類共有7822個有重疊的planned slots，也不可加成執行計數。舊2915／1612／4521／93均未納入。本次product runtime assertions／build／native全部0。

集合必須正確去重：32 control含8個terminal，因此32＋288extension＋104terminal－8重疊＝416 unique。gfx0其餘4武器共用24個pickup body，仍有獨立profile hash、weapon owner與combat timing。只有42個明確pair，不能從gfx推weapon。gfx2796依48:1→48:2→48:3→8:0；6組角色terminal sid8起。64個跨sprite裸key碰撞已固定，必須用gfx/sprite namespace。

mapping內每列有exact key／來源PNG／bounds／profile hash／shadow lookup與hit/miss、metadata events inert、nominal sample clock及owner欄位。terminal樣本normal376／haste281，hold584後分別960／865清除；計畫列沒有宣稱原生時序。672個terminal mapping rows有exact shadow，2016個pickup rows無shadow；PNG全部body-only，不能直接拿來比包含shadow／背景的整張composite。

duration／selector fixtures保留每profile的normal／haste及double boundary；damage583.3333333333334在583可見、584到期。newer expired hit先被選中時，不可回退到仍可見的較舊attack。另有真正S30→flight→hit三類未來case，以及42個same-gfx weapon切換／舊callback slots。所有case的executed與actual欄位都明示未執行。

視覺QA：overview52tiles及gfx0／2796／6671各32tiles，共148次呈現／136個不同body幀；未見裁切或拼接錯位。其餘10分組表未逐張視看，全部416 PNG仍須由verifier解壓核對已接受RGBA hash。這不是產品render或動畫驗收。

fixture-manifest.json列441個fixture檔案；input-pins.json列外部來源。獨立verify.py不import prepare.py或任何舊review helper；使用Python標準庫解PNG／核CRC與RGBA，重讀原metadata／profile資料核2688映射、shadow、duration／boundary／allowlist／selector fixtures並核所有封存hash。它不輸出新payload或cache。

```powershell
python -B -X utf8 .\verify.py --seal '<published artifact-manifest SHA256>'
```

完成封存後停寫；不得重跑prepare.py、specs.py或finalize.py覆寫本root。主管後續另綁定finalr34與candidate，再派實際覆核。native private flags／variant／FIFO／nativeTiming／Slow／remote／同版BIN資格均false，不重開研究。

Goal實讀null，未建立或更改；未建chat／agent。無DB／services／SQL／server／GUI／socket／native操作；未解碼SPR、掃PAK或複製整包assets。原candidate／source／review／engine／releases／主管皆唯讀。
