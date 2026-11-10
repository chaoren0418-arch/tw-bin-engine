"""T309 (#736) read-only: map summonList.xml desc ids through names.json (desc-c.tbl copy). Prints to stdout only."""
import json, re, sys
names = json.load(open(sys.argv[1], encoding="utf-8"))
xml = open(sys.argv[2], encoding="utf-8", errors="replace").read()
print(type(names).__name__, len(names))
get = (lambda i: names[i] if isinstance(names, list) and i < len(names) else None) if isinstance(names, list) else (lambda i: names.get(str(i)))
for m in re.finditer(r'level="(\d+)" action="(\d+)" desc="(\d+)"', xml):
    lv, act, d = m.groups()
    print(lv, act, d, get(int(d)))
