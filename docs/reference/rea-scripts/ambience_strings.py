import re, sys, pathlib
b = pathlib.Path(r"D:\lineage開發\登入器bin資源\TW1608262200.bin拆解\TW1608262200.bin").read_bytes()
pat = re.compile(rb"[\x20-\x7e]{4,}")
keys = re.compile(rb"(?i)rain|snow|weather|light|night|dark|drunk|poison|blind|invis|fog|lightning|thunder|ambient|\.spr|sprite|\.wav|day")
for m in pat.finditer(b):
    s = m.group()
    if keys.search(s) and len(s) < 80:
        print(hex(m.start()), s.decode())
