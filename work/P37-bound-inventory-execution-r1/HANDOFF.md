# P37 真實 inventory-only 執行交接

依唯一主管 `01a185b4-ecf6-705c-90e6-83b9eb1ba02f` 的 exact 派工，先確認本 execution root 不存在、8個 dispatch pins 相符，再以原封存 r2 `bound_inventory.py` 執行一次。未改函數、未 monkeypatch、未造收據。真 Goal 讀回 null，未建立或修改。

實際 command exit=0，stderr=0 bytes。完整 argv/cwd/exit 和 raw stdout/stderr pins 在 `command.json`；`inventory.stdout.json` 即未改寫的原始 JSON 輸出，58,242 bytes，SHA-256 `2bb58c3c48b576a213c05f5a9b2835e5f2ed4b717afe8fbbe8719009780bc778`。

驗證結果共210列，精確分為181個 source/audio/web來源、15個新P36 build輸出、1個產品manifest本身、4個唯讀繼承launcher檔與9個evidence檔。檔案來源、大小、SHA、唯一目標與排序均核對；來源完整集合對應P36已接受的181-source manifest。evidence閉合於實際P36 result/source manifest/build command/binlog/raw logs及三份真主管收據，沒有補造8步／4521或新增runtime完成紀錄。

四項identity與P36 final acceptance完全相同：

| 身份 | SHA-256 |
|---|---|
| product DLL | `6cb2f6282b445f1550cc5e62447756e5ecebcaca0a1d4a7c45892f0f4db44fb3` |
| audio DLL | `d390e22cdfd83249a72405e67f86d0dbbb0ea2cbd3111917af44aca97b92b9f4` |
| product manifest | `d55389e048aee9eb2b64088ddfb0128de6166071e21969bd1dd57b9464d9ce75` |
| inherited launcher DLL | `7270f9c8082473fdb9a10f379e9034552b43c16d4d5d4d91eb08f756d5ba7aa8` |

本次真正使用 P36-final-acceptance SHA `76145832e5865fad75741450f53500cfad455aa73507bb0a75709c35a86e1e17` 與 P37-inventory-only authority SHA `4c0236f33250cf63c5789af0586e4e9992b960e7841cf483da86c6fa89d2377d`。工具 SHA `3bb2bc7a22c486dbfe81b0c8d08c04e18b80c5cda5473dc46b17e319af8e61a1`、tool seal及author seal未變，完整8項輸入pins與33項protected pins在 `input-pins.json`。執行後全部一致，assets junction target不變。

這是 **inventory-only成功，仍待獨立final review**。不是full release接受、launcher使用驗收或入口啟用；相同DLL的既有runtime資格只繼承、不計新增。P36原build／執行／review與r2 plan封存、全部shared engine在本輪皆唯讀。

只在本目錄保存結果與封存；沒有build、runtime、product/native、DB/service、socket/network、憑證、發布或永久刪除操作。正式入口與project-manifest仍r33。舊r34 completed_context仍保留expected-refusal文字資格，未執行或更改其pins；未重跑任何負向案例。

下列命令只核對已保存的輸出與封存，**不重跑inventory工具**：

```powershell
python -B -X utf8 'D:\lineage開發\TW1608262200研究\work\P37-bound-inventory-execution-r1\verify_delivery.py'
```

原始工具只能執行這一次；不要重跑 `command.json` 的argv。驗證方式只讀取資料與pins，不含新的產品測試harness。`result.json` 保留驗證結果及資格；artifact manifest/seal排除自身兩檔以避免循環。封存後停寫，交主管獨立final review。

原入口啟用自動核准拒絕保留，未重試或改道：

> 此派工會持久修改正式入口與 project-manifest，將 r34 提升為現行產品版本，屬外部／正式承諾；使用者只授權引擎開發，未明確授權這次入口啟用，且政策不以先前助手派工替代該核准。
