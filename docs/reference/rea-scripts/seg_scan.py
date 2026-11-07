"""Read-only census of the effect tail in classic .seg map blocks (layout from OriginalMapCache.cs)."""
import collections, json, pathlib, struct, sys

ROOT = pathlib.Path(r"D:\lineage開發\0-客戶端\map")


def tail(b):
    p = 128 * 64 * 2
    (nc,) = struct.unpack_from("<H", b, p); p += 2 + nc * 4
    p += 8192
    (ng,) = struct.unpack_from("<i", b, p); p += 4
    for _ in range(ng):
        p += 2
        (n,) = struct.unpack_from("<H", b, p); p += 2 + n * 5
    (nt,) = struct.unpack_from("<i", b, p); p += 4 + nt * 5
    refs = b[p]; p += 1 + refs
    (portals,) = struct.unpack_from("<H", b, p); p += 2
    for _ in range(portals):
        p += 1 + b[p] + 8
    if p >= len(b):
        return None, p
    (raw,) = struct.unpack_from("<H", b, p)
    return raw, p + 2


st = collections.Counter()
raws = collections.Counter()
bad = []
for f in ROOT.rglob("*.seg"):
    st["files"] += 1
    try:
        b = f.read_bytes()
        raw, end = tail(b)
    except Exception as e:
        st["parse_error"] += 1
        bad.append((f.parent.name + "/" + f.name, repr(e)[:80]))
        continue
    if raw is None:
        st["no_tail"] += 1
    else:
        raws[raw] += 1
        if raw & 0x8000:
            st["extended_flag"] += 1
        if raw & 0x7FFF:
            st["nonzero_count"] += 1
    if end != len(b) and raw == 0:
        st["trailing_after_zero"] += 1
print(dict(st))
print("raw top", raws.most_common(10))
print("bad", bad[:6])
others = collections.Counter(p.suffix.lower() for p in ROOT.rglob("*") if p.is_file())
print("map file ext", others.most_common(12))
