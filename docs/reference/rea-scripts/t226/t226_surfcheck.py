"""Read-only (#533): for each UI XML, list the surf ids it references (BaseSurf/FrameSurf/PushSurf/HighlightSurf/
Surf/DisableSurf ...) and report which are present as `<id>.img` in the client's Sprite*.idx (or `<id>.tbt`).
Usage: t226_surfcheck.py <client dir> file.xml [file.xml ...]"""
import os, re, struct, sys
sys.stdout.reconfigure(encoding="utf-8")
client, files = sys.argv[1], sys.argv[2:]
names = set()
for fn in os.listdir(client):
    if fn.lower().startswith("sprite") and fn.lower().endswith(".idx"):
        b = open(os.path.join(client, fn), "rb").read()
        for i in range(4, len(b) - 27, 28):
            names.add(b[i + 4:i + 24].split(b"\0")[0].decode("latin1", "replace").lower())
for p in files:
    t = open(p, "rb").read().decode("utf-8", "replace")
    ids = sorted({int(m) for m in re.findall(r'(?:Surf|surfNumber)\s*=\s*"(\d+)', t)})
    img = [i for i in ids if f"{i}.img" in names]
    tbt = [i for i in ids if f"{i}.img" not in names and f"{i}.tbt" in names]
    none = [i for i in ids if f"{i}.img" not in names and f"{i}.tbt" not in names]
    print(f"{os.path.basename(p)}\tsurfs={len(ids)}\timg={len(img)}\ttbt_only={len(tbt)}\tabsent={len(none)}\tabsent_ids={none[:20]}")
