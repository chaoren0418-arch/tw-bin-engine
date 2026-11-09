"""T690 (#690): read-only. Report whether <id>.img / <id>.tbt exist in the client's Sprite*.idx (and Tile.idx).
Usage: py -3 -I t690_surfcheck.py <client dir> ID [ID ...]"""
import os, struct, sys
sys.stdout.reconfigure(encoding="utf-8")
client, ids = sys.argv[1], sys.argv[2:]
where = {}
for fn in sorted(os.listdir(client)):
    low = fn.lower()
    if low.endswith(".idx") and (low.startswith("sprite") or low.startswith("tile")):
        b = open(os.path.join(client, fn), "rb").read()
        for i in range(4, len(b) - 27, 28):
            n = b[i + 4:i + 24].split(b"\0")[0].decode("latin1", "replace").lower()
            where.setdefault(n, []).append(fn)
for i in ids:
    for ext in ("img", "tbt", "png"):
        k = f"{i}.{ext}"
        print(k, where.get(k, "absent"))