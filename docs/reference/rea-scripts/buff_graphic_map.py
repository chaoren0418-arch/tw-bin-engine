import re, sys, xml.etree.ElementTree as ET
raw = open(sys.argv[1], "rb").read()
root = ET.fromstring(re.sub(rb"<!--.*?-->", b"", raw, flags=re.S).decode("cp950", "replace"))
want = {int(a) for a in sys.argv[2:]}
g = {}
for e in root.iter("effect"):
    if e.get("graphic"):
        for v in re.split(r"[ ,]+", e.get("graphic").strip()):
            if v.isdigit(): g.setdefault(int(v), []).append(int(e.get("id")))
for k in sorted(want): print(k, g.get(k))
print("total graphic keys", len(g), "first-match rule: sub_81DCA0 returns first effect in parse order")
