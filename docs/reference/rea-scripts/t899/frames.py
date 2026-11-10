"""T899: read-only. From PakViewer `pak list` outputs, collect which <gfx>-<frame>.spr exist per gfx (local + alt).
Usage: py -3 -I frames.py PAK_INDEX_DIR gfx...  -> prints frame-set summaries / differences."""
import re, sys, collections
from pathlib import Path

d = Path(sys.argv[1]); want = [int(x) for x in sys.argv[2:]]
rx = re.compile(r"\s(\d+)-(\d+)\.spr\b", re.I)
sets = collections.defaultdict(lambda: collections.defaultdict(set))  # src -> gfx -> frames
for f in list(d.glob("*.list.txt")) + list((d / "alt").glob("*.list.txt")):
    src = "local" if f.parent == d else f.name.split(".")[0]
    if not f.name.lower().startswith(("sprite", "kor25.sprite", "ly.sprite", "t89.sprite")):
        continue
    for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
        m = rx.search(line)
        if m and int(m.group(1)) in want:
            sets[src][int(m.group(1))].add(int(m.group(2)))

def rng(s):
    s = sorted(s); out = []; i = 0
    while i < len(s):
        j = i
        while j + 1 < len(s) and s[j + 1] == s[j] + 1:
            j += 1
        out.append(f"{s[i]}" if i == j else f"{s[i]}-{s[j]}"); i = j + 1
    return ",".join(out)

for src in sorted(sets):
    for g in want:
        fr = sets[src].get(g, set())
        print(f"{src}\t{g}\tn={len(fr)}\t{rng(fr)[:300]}")
if len(want) >= 2:
    for src in sorted(sets):
        a, b = sets[src].get(want[0], set()), sets[src].get(want[1], set())
        print(f"{src}\t{want[0]} minus {want[1]}: n={len(a - b)} {rng(a - b)[:300]}")
