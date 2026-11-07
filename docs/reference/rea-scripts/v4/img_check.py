"""V4 (#459) read-only check of Sprite*.idx/pak (no files written).

1. Hash rule: for every entry in SpriteNN.idx, is NN == (sum of name bytes) % 16?  (HashFileManager, sub_879E60)
2. Duplicate .img names in Sprite.pak and SpriteNN.pak: which copy the BIN reaches first.
3. .img header flag (u16 @+4) distribution, and how often pixels equal the transparent word (u16 @+6).
4. Language-suffixed surf names (<n>c.img / <n>-c.img) present?
Usage: py -3 -I img_check.py [client_root] [surf ...]
"""
import glob, os, struct, sys
from collections import Counter

root = sys.argv[1] if len(sys.argv) > 1 else r"D:\lineage開發\0-客戶端"
probe = [int(x) for x in sys.argv[2:]]

def read_idx(path):
    b = open(path, "rb").read()
    n, = struct.unpack_from("<I", b, 0)
    ents = []
    for i in range(4, len(b) - 27, 28):
        off, = struct.unpack_from("<I", b, i)
        nm = b[i + 4:i + 24].split(b"\0")[0]
        sz, = struct.unpack_from("<I", b, i + 24)
        ents.append((nm, off, sz))
    return n, ents

paks = {}
for idx in sorted(glob.glob(os.path.join(root, "Sprite*.idx"))):
    tag = os.path.basename(idx)[6:-4]  # '' or '00'..'15'
    paks[tag] = (idx[:-4] + ".pak", read_idx(idx)[1])

def h16(nm):
    return sum(nm) % 16

# 1. hash rule
for tag in sorted(paks):
    if not tag: continue
    ents = paks[tag][1]
    bad = [e[0] for e in ents if h16(e[0]) != int(tag)]
    print(f"HASH Sprite{tag}: entries={len(ents)} mismatch={len(bad)} sample={bad[:3]}")
base = paks[""][1]
hb = Counter(h16(e[0]) for e in base)
print("HASH Sprite.pak: entries=%d bucket spread=%s" % (len(base), dict(sorted(hb.items()))))

# 2. duplicates (.img)
where = {}
for tag, (pak, ents) in paks.items():
    for nm, off, sz in ents:
        if nm.lower().endswith(b".img"):
            where.setdefault(nm.lower(), []).append((tag, nm, off, sz))
dups = {k: v for k, v in where.items() if len(v) > 1}
def blob(tag, off, sz):
    with open(paks[tag][0], "rb") as f:
        f.seek(off); return f.read(sz)
same = diff = 0; winners = Counter(); maxid = 0
for k, v in sorted(dups.items()):
    tags = sorted(t for t, *_ in v)
    blobs = {t: blob(t, o, s) for t, n, o, s in v}
    d = len(set(blobs.values())) > 1
    diff += d; same += (not d)
    nm = v[0][1]
    first = "%02d" % h16(nm)  # BIN tries Sprite<hash> first, then Sprite.pak
    winners["SpriteNN" if first in tags else "Sprite.pak"] += 1
    try: maxid = max(maxid, int(k[:-4]))
    except ValueError: pass
print(f"DUP .img names={len(dups)} differ={diff} same={same} max_numeric_name={maxid} reached_first={dict(winners)}")
for k, v in list(sorted(dups.items()))[:8]:
    print("  ", k.decode(), [t or "base" for t, *_ in v], "hash=%02d" % h16(v[0][1]))

# 3. flag distribution / transparent usage on all .img
flags = Counter(); keyuse = Counter(); keys = Counter()
for tag, (pak, ents) in paks.items():
    with open(pak, "rb") as f:
        for nm, off, sz in ents:
            if not nm.lower().endswith(b".img") or sz < 8: continue
            f.seek(off); b = f.read(sz)
            w, h, fl, key = struct.unpack_from("<4H", b, 0)
            if 8 + w * h * 2 != sz: flags["badsize"] += 1; continue
            flags[fl] += 1; keys[(fl != 0, key)] += 1
            px = struct.unpack_from("<%dH" % (w * h), b, 8)
            keyuse[(fl, any(p == key for p in px))] += 1
print("FLAG values:", dict(flags.most_common(12)))
print("FLAG,key-present-in-pixels:", dict(keyuse.most_common(12)))
print("KEY top (flag!=0,key):", keys.most_common(8))

# 4. suffixed names
suf = [k for k in where if k[:-4].endswith(b"c") or b"-" in k]
print("SUFFIXED .img names:", len(suf), [s.decode() for s in suf[:10]])

for s in probe:
    for tag, nm, off, sz in where.get(b"%d.img" % s, []):
        w, h, fl, key = struct.unpack("<4H", blob(tag, off, 8))
        print(f"PROBE {s}: Sprite{tag or ''} w={w} h={h} flag={fl} key=0x{key:04X}")
