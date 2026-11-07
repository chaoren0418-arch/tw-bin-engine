"""V4 (#459) read-only: localized surf images '<n>c.img' vs '<n>.img' (no files written).
Usage: py -3 -I img_csuffix.py [client_root] [surf ...]
"""
import glob, os, struct, sys
from collections import Counter

root = sys.argv[1] if len(sys.argv) > 1 else r"D:\lineage開發\0-客戶端"
probe = sys.argv[2:]
where = {}
for idx in sorted(glob.glob(os.path.join(root, "Sprite*.idx"))):
    b = open(idx, "rb").read(); tag = os.path.basename(idx)[6:-4]
    for i in range(4, len(b) - 27, 28):
        off, = struct.unpack_from("<I", b, i); sz, = struct.unpack_from("<I", b, i + 24)
        nm = b[i + 4:i + 24].split(b"\0")[0].decode("latin1").lower()
        if nm.endswith(".img"):
            where.setdefault(nm[:-4], []).append((tag, idx[:-4] + ".pak", off, sz))
def rd(e):
    with open(e[1], "rb") as f:
        f.seek(e[2]); return f.read(e[3])
loc = sorted(k for k in where if k.endswith("c") and k[:-1].isdigit())
rng = Counter(); dims = Counter(); nobase = 0; same = 0
for k in loc:
    n = int(k[:-1]); rng[(n // 1000) * 1000] += 1
    base = where.get(k[:-1])
    if not base: nobase += 1; continue
    a, b = rd(where[k][0]), rd(base[0])
    same += a == b
    dims["same-wh" if a[:4] == b[:4] else "diff-wh"] += 1
print("LOCALIZED <n>c.img:", len(loc), "by-thousand:", dict(sorted(rng.items())))
print("  without base:", nobase, "identical-to-base:", same, dims)
ui = [k for k in loc if 2000 <= int(k[:-1]) <= 5800]
print("  in 2000-5800:", len(ui), ui[:40])
for s in probe:
    print("PROBE", s, "base" if s in where else "-", "c-variant" if s + "c" in where else "-")
