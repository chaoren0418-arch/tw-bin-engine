"""T690 (#690): read-only. List items in the 3.81 DB dump (armor/etcitem/weapon) whose invgfx equals the given value.
Usage: py -3 -I t690_invgfx.py <380_db.sql> VALUE"""
import re, sys
sys.stdout.reconfigure(encoding="utf-8")
path, want = sys.argv[1], sys.argv[2]
txt = open(path, encoding="utf-8", errors="replace").read()
for tbl in ("armor", "etcitem", "weapon"):
    m = re.search(r"CREATE TABLE `%s`\s*\((.*?)\)\s*ENGINE" % tbl, txt, re.S)
    cols = re.findall(r"^\s*`([^`]+)`", m.group(1), re.M)
    if "背包圖示編號" not in cols:
        print(tbl, "no 背包圖示編號 column", cols[:12]); continue
    gi, ni = cols.index("背包圖示編號"), cols.index("名稱")
    nid = cols.index("名稱字串編號") if "名稱字串編號" in cols else None
    n = 0
    for row in re.finditer(r"INSERT INTO `%s` VALUES \((.*?)\);\s*$" % tbl, txt, re.M):
        vals = [v.strip().strip("'") for v in re.findall(r"'(?:[^'\\]|\\.)*'|[^,]+", row.group(1))]
        if len(vals) == len(cols):
            n += 1
            if vals[gi] == want:
                print(tbl, vals[cols.index("item_id")], vals[ni], vals[nid] if nid is not None else "", "invgfx=" + vals[gi])
    print(tbl, "rows", n, "cols", len(cols))