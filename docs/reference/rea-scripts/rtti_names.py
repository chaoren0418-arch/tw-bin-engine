"""Read-only: list MSVC RTTI type names (.?AV / .?AU) matching any of the given substrings, with VA/FO."""
import re, sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
sys.stdout.reconfigure(encoding="utf-8")
from nat import Img
im = Img()
d = im.data
def va_of(fo):
    for n, v, vs, p, r in im.secs:
        if p <= fo < p + r:
            return v + fo - p
keys = [k.lower() for k in sys.argv[1:]]
for m in re.finditer(rb"\.\?A[UV][A-Za-z0-9_@?$]{2,120}", d):
    s = m.group().decode()
    if any(k in s.lower() for k in keys):
        print(f"VA={va_of(m.start()):#x}\tFO={m.start():#x}\t{s}")
