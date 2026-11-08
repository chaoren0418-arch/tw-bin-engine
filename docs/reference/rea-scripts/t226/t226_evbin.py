"""Read-only (#533): for each given UI XML, report how many of its Mouse/KeyboardEvent names exist as
NUL-terminated ASCII strings in TW1608262200.bin (proxy for "this XML version matches TW16's event table").
Usage: t226_evbin.py file.xml [file.xml ...]"""
import hashlib, os, re, sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent.parent))  # rea-scripts/nat.py
from nat import Img
sys.stdout.reconfigure(encoding="utf-8")
data = Img().data
for p in sys.argv[1:]:
    raw = open(p, "rb").read()
    t = raw.decode("utf-8", "replace")
    ev = sorted(set(re.findall(r'(?:Mouse|Keyboard)Event\s*=\s*"([^"]+)"', t)) - {"Dummy"})
    miss = [e for e in ev if b"\0" + e.encode("latin1") + b"\0" not in data]
    print(f"{p}\tsha={hashlib.sha256(raw).hexdigest()[:12]}\tevents={len(ev)}\tinBIN={len(ev) - len(miss)}\tmissing={miss[:10]}")
