# Issue #417 REA 靜態檢查摘要

`docs/reference/rea-scripts/rea_compare.py` 的輸出，判讀見 `docs/reference/rea-inventory.md`。

```text
tw16 vs tw13u: tw16=143 other=119 shared_keys=114 same_payload=69 diff=['(1, 11, 1033)', '(1, 18, 1033)', '(1, 19, 1033)', '(1, 21, 1033)', '(1, 22, 1033)', '(1, 23, 1033)', '(1, 25, 1033)', '(1, 26, 
tw16 vs lito_oep: tw16=143 other=37 shared_keys=36 same_payload=33 diff=['(1, 11, 1033)', '(14, 123, 1033)', '(24, 1, 1033)']
tw16 vs lin: tw16=143 other=143 shared_keys=143 same_payload=142 diff=['(16, 1, 1042)']
tw16 vs linr: tw16=143 other=12 shared_keys=3 same_payload=1 diff=['(14, 123, 1033)', '(16, 1, 1042)']
-- lin only / tw16 only [] []
dialog tw16 (5, 134, 1033) None 641709325090
dialog tw16 (5, 146, 1042) None a0bf193757be
version tw16 (16, 1, 1042) ['041204b0', 'CompanyName', 'NCSOFT', 'FileDescription', 'Lineage', 'FileVersion', '16,08,26,2200', 'InternalName', 'Lineage', 'LegalCopyright', 'Copyright ', ' 2010 NCSOFT'
dialog tw13u (5, 134, 1033) None 641709325090
dialog tw13u (5, 146, 1042) None a0bf193757be
version tw13u (16, 1, 1042) ['041204b0', 'CompanyName', 'NCSOFT', 'FileDescription', 'Lineage', 'FileVersion', '13,08,19,1', 'InternalName', 'Lineage', 'LegalCopyright', 'Copyright ', ' 2010 NCSOFT', 
dialog lito_oep (5, 134, 1033) None 641709325090
dialog lito_oep (5, 146, 1042) None a0bf193757be
dialog lin (5, 134, 1033) None 641709325090
dialog lin (5, 146, 1042) None a0bf193757be
version lin (16, 1, 1042) ['041204b0', 'CompanyName', 'NCSOFT', 'FileDescription', 'Lineage', 'FileVersion', '15,05,26,301', 'InternalName', 'Lineage', 'LegalCopyright', 'Copyright ', ' 2010 NCSOFT', 
version linr (16, 1, 1042) ['041204b0', 'CompanyName', 'NCSOFT', 'FileDescription', 'Lineage', 'FileVersion', '22,11,23,1701', 'InternalName', 'Lineage', 'LegalCopyright', ' NCSOFT', 'OriginalFilename
-- signers (lin)
-- Miles quick API per sample
tw16 {'mss32.dll': 16, 'mss32midi.dll': 9} quick_play
tw13u {'mss32.dll': 23} quick_play
lito_oep {} no quick_play in table
lin {} no quick_play in table
linr {'mss32.dll': 14, 'mss32midi.dll': 9} quick_play
```

