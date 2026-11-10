"""T899 (#899): read-only. For given gfx, read every <gfx>-<sid>.spr header straight from the client's unencrypted
Sprite*.idx/.pak (28-byte rows: offset, name[20], size) and count tiles per frame (header layout as the engine's
MagicRendering.Decode). Nothing is written to disk. Usage: py -3 -I cloth_tiles.py CLIENT_ROOT gfx..."""
import struct, sys, collections
from pathlib import Path

root = Path(sys.argv[1]); want = {int(x) for x in sys.argv[2:]}
found = {}
for stem in ["Sprite"] + [f"Sprite{i:02}" for i in range(16)]:
    idx = root / f"{stem}.idx"
    if not idx.exists():
        continue
    b = idx.read_bytes(); n = struct.unpack_from("<I", b)[0]
    for i in range(n):
        r = b[4 + 28 * i:32 + 28 * i]
        name = r[4:24].split(b"\0")[0].decode("ascii", "replace").lower()
        if not name.endswith(".spr") or "-" not in name:
            continue
        g, s = name[:-4].split("-", 1)
        if g.isdigit() and s.isdigit() and int(g) in want and (int(g), int(s)) not in found:
            found[(int(g), int(s))] = (stem, struct.unpack_from("<I", r)[0], struct.unpack_from("<I", r, 24)[0])


def tiles(p):
    o = 0; first = p[o]; o += 1
    if first == 255:
        c = p[o] or 256; o += 1 + 2 * c; first = p[o]; o += 1
    res = []
    for _ in range(first):
        o += 12; cnt = struct.unpack_from("<h", p, o)[0]; o += 2 + 5 * max(cnt, 0); res.append(cnt)
    return res


for g in sorted(want):
    hist = collections.Counter(); nonempty = []
    for (gg, s), (stem, off, size) in sorted(found.items()):
        if gg != g:
            continue
        with (root / f"{stem}.pak").open("rb") as f:
            f.seek(off); t = tiles(f.read(size))
        k = sum(t); hist[k > 0] += 1
        if k > 0:
            nonempty.append((s, k))
    print(f"gfx {g}: sids={sum(hist.values())} all-empty={hist[False]} with-tiles={hist[True]}")
    print("  with tiles (sid:tiles):", " ".join(f"{s}:{k}" for s, k in nonempty[:60]))
