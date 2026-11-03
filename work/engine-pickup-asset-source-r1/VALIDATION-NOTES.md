# 定向來源重現與驗證

只讀已知metadata、reader／decoder／P04證據和208個指定SPR；沒有執行原BIN、JARhandler、正常產品或GUI/socket/服務／DB／SQL。獨立比較runner是純managed資料解碼console，沿用既有P04 library，並非原native oracle。私有DOTNET_CLI_HOME/NUGET_PACKAGES/TEMP，DOTNET_GENERATE_ASPNET_CERTIFICATE=false，NuGet sources清空。

實際結果：208 SPR、1,256,525 PAK切片bytes、416必要幀、42 profiles，獨立RGBA比較1,402,968畫布pixels差異0；271,289不透明像素、6752黑色不透明像素、重疊0。全部frame bounds/index/offset/RLE expanded size由嚴格既有decoder驗證，PNG與reference raw RGBA/hash另保存。原有336個profile幀鍵另外從產品blocks重建，逐一bounds/RGBA相等。

首次extract在8組archive size mismatch中止，exit1、原程式快照與失敗原因保留。第二次明確分離歷史整包與目前IDX/slice身分，exit0，沒有改資料或舊pin讓它通過。當前17個PAK均不宣稱整檔hash已重新驗；entry-identity-layers.json逐項區分有歷史payload核對與本輪bounded來源。

最初遞迴檔名搜尋範圍過廣而中止，只列舉路徑，未讀／hash全量asset本文；後續定位用指定資料夾、明確檔名與IDX二分查找。讀過歷史source-records-r3整份證據JSON，但未重掃它所列的11097幀或全部素材。這些限制保留在validation，不能描述成全流程從未做過廣泛檔名列舉。

qualify.py成功但Pillow對getdata發出棄用警告（未來API移除）；不是像素解析失敗，未為消警告更改輸出。managed reference build/run均exit0，build零警告零錯誤。Python各步命令／exit見execution-receipts；managed步驟另有實際stdout/stderr/command JSON。沒有產品測試數目或r33/r32 suite混入。

人工視覺抽看gfx0、2796、6650各8方向。最初contact文字重疊，已用contacts.py改兩行標籤及2x nearest預覽，原416 PNG不變；沒有宣稱全13張人工都看過。所有416幀均有數值比對。

## 在獲授權fresh work重現

不可原地重跑已封存目錄。建立同一研究根的work子目錄，複製setup.py、extract.py、run_reference.py、qualify.py、contacts.py、finish.py、verify.py、SOURCE-CONTRACT.md、VALIDATION-NOTES.md，以及reference/Program.cs、Reference.csproj。另將本封存entry-map.json複製為expected-entry-map.json、archive-identity.json複製為expected-archive-identity.json；extract會先核當前IDX SHA與PAK size，再逐entry比原記錄與slice SHA，來源不同便拒絕。setup建立其他子目錄；不必複製raw/png/reference-output等生成物。所有原件路徑已固定，來源不符時應保留失敗，不自動套新pin。expected-* replay guard是封存前加入的重現保護，本輪初次取材時尚無這份expected map；本輪final source核對使用同樣逐entry資料重讀。

```powershell
$python = 'C:\Users\CHAOREN\AppData\Local\Programs\Python\Python312\python.exe'
& $python -B -X utf8 (Join-Path $scratch 'setup.py')
& $python -B -X utf8 (Join-Path $scratch 'extract.py')
& $python -B -X utf8 (Join-Path $scratch 'run_reference.py')
& $python -B -X utf8 (Join-Path $scratch 'qualify.py')
& $python -B -X utf8 (Join-Path $scratch 'contacts.py')
```

原first-extract失敗快照只供審核，不需故意重跑。正常新重現可到qualify/contacts完成後獨立比對entry/frame/pixel map；原finish.py的歷史執行敘述屬本輪收據，reviewer不應直接複製成自己的歷史。要封存自己的交付需按自己的實際執行更新收據。

`verify.py`為唯讀完整artifact/source pins核對，重讀208個PAK精確slice與IDX record，並核PNG及reference比對記錄。會全hash小IDX、已pin metadata及封存產物，不全hashPAK、不執行renderer。對目前wholefile identity有新要求時另派必要工作，本包不因此無謂重掃archive。
