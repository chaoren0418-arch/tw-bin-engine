"""Inspect the trailing bytes of s32 files that do not end after the effect section."""
import collections, json, pathlib, re, sys
sys.path.insert(0, str(pathlib.Path(__file__).parent))
import importlib.util

src = pathlib.Path(__file__).with_name("s32_scan.py").read_text(encoding="utf-8").split("st = collections.Counter()")[0]
ns = {}
exec(src, ns)
tail, ROOT = ns["tail"], ns["ROOT"]
d = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding="utf-8"))
lens, maps, samples, shapes = collections.Counter(), collections.Counter(), [], collections.Counter()
for rel, msg in d["bad"]:
    pass
for f in ROOT.rglob("*.s32"):
    b = f.read_bytes()
    try:
        raw, end, ext, recs = tail(b)
    except Exception:
        continue
    if end != len(b):
        t = b[end:]
        lens[len(t)] += 1
        maps[f.parent.name] += 1
        shapes[(raw, len(t) % 2)] += 1
        if len(samples) < 12:
            samples.append((f.parent.name + "/" + f.name, raw, len(recs), t.hex()))
print("lens", lens.most_common(12))
print("maps", maps.most_common(20))
print("shape", shapes.most_common(8))
for s in samples:
    print(s)
