"""Read-only census of map-local effect tails in client .s32 files (layout from OriginalMapCache.cs / sub_F0B570)."""
import collections, json, pathlib, struct, sys

ROOT = pathlib.Path(r"D:\lineage開發\0-客戶端\map")


def tail(b):
    p = 128 * 64 * 4
    (nc,) = struct.unpack_from("<H", b, p); p += 2 + nc * 6
    p += 8192 * 2
    (ng,) = struct.unpack_from("<i", b, p); p += 4
    for _ in range(ng):
        p += 2
        (n,) = struct.unpack_from("<H", b, p); p += 2 + n * 7
    (nt,) = struct.unpack_from("<i", b, p); p += 4 + nt * 5
    (refs,) = struct.unpack_from("<i", b, p); p += 4 + refs * 4
    (portals,) = struct.unpack_from("<H", b, p); p += 2
    for _ in range(portals):
        p += 1 + b[p] + 8
    if p == len(b):
        return None, p, 0, []
    (raw,) = struct.unpack_from("<H", b, p); p += 2
    ext, n = bool(raw & 0x8000), raw & 0x7FFF
    recs = []
    for _ in range(n):
        i, x, y = struct.unpack_from("<HHH", b, p); p += 6
        g = None
        if ext:
            (g,) = struct.unpack_from("<I", b, p); p += 4
        recs.append((i, x, y, g))
    return raw, p, ext, recs


st = collections.Counter()
ids = collections.Counter()
ext_rows, nonzero, bad = [], [], []
for f in ROOT.rglob("*.s32"):
    st["files"] += 1
    try:
        b = f.read_bytes()
        raw, end, ext, recs = tail(b)
    except Exception as e:
        st["parse_error"] += 1
        bad.append((str(f.relative_to(ROOT)), repr(e)))
        continue
    if end != len(b):
        st["trailing_mismatch"] += 1
        bad.append((str(f.relative_to(ROOT)), f"end={end} len={len(b)}"))
    if raw is None:
        st["no_tail"] += 1
        continue
    if recs:
        st["files_with_effects"] += 1
        st["records"] += len(recs)
    if ext:
        st["extended_files"] += 1
        st["extended_records"] += len(recs)
    for i, x, y, g in recs:
        ids[i] += 1
        if ext:
            ext_rows.append((str(f.relative_to(ROOT)), i, x, y, g))
            if g not in (0, 0xFFFFFFFF):
                nonzero.append((str(f.relative_to(ROOT)), i, x, y, g))
print(dict(st))
print("distinct ids", len(ids), "top", ids.most_common(15))
print("extended sample", ext_rows[:15])
print("nonzero group", len(nonzero), nonzero[:25])
print("bad sample", bad[:8])
pathlib.Path(sys.argv[1]).write_text(json.dumps(dict(stats=st, ids=ids.most_common(), extended=ext_rows, nonzero_group=nonzero, bad=bad[:200]), ensure_ascii=False), encoding="utf-8")
