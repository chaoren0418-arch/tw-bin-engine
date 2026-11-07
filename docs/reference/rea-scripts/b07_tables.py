"""B07: read the #48 'missing' switch tables and enumerate class-pointer readers/writers."""
import json, sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img, fmt

im = Img()
out = {"sections": im.secs}
print("sections", im.secs)

# file-offset rule check against #47 / #48 history
for va, fo in [(0xA3EF40, 0x63E340), (0x947616, 0x546A16), (0xA46EE3, 0x6462E3)]:
    print(f"va {va:X} -> off {im.off(va):X} expected {fo:X}", im.off(va) == fo)

SW = {
    "first": dict(index=0x94828C, target=0x9480C0, n=255, bias=None),
    "second": dict(index=0x9483D4, target=0x94838C, n=240, bias=6),
    "third": dict(index=0x9484CC, target=0x9484C4, n=235, bias=0x13B),
}
out["switches"] = {}
for name, s in SW.items():
    idx = im.read(s["index"], s["n"])
    mx = max(idx)
    tg = [im.d32(s["target"] + 4 * k) for k in range(mx + 1)]
    cases = {}
    for i, b in enumerate(idx):
        cases.setdefault(tg[b], []).append(i)
    print(name, "index_off", hex(im.off(s["index"])), "max", mx, "targets", len(set(tg)))
    out["switches"][name] = dict(index_va=hex(s["index"]), index_file_offset=hex(im.off(s["index"])),
                                 target_va=hex(s["target"]), target_file_offset=hex(im.off(s["target"])),
                                 index_max=mx, bias=s["bias"],
                                 cases={hex(k): v for k, v in cases.items()})

print("--- dispatcher head")
for i in im.dis(0x947530, 0x120):
    if i.address > 0x947628:
        break
    print(fmt(i))
print("--- second path")
for i in im.dis(0x947E80, 0x50):
    print(fmt(i))
print("--- third path")
for i in im.dis(0x948040, 0x40):
    print(fmt(i))

print("--- class pointers")
out["xrefs"] = {}
for t in (0x13CD838, 0x13CD878, 0x153CE60):
    print(hex(t), im.sec(t))
    rows = []
    for at, ins in im.xrefs(t):
        s = fmt(ins) if ins else f"{at:08X} (undecoded)"
        rows.append(s)
        print("  ", s)
    out["xrefs"][hex(t)] = rows
pathlib.Path(sys.argv[1]).write_text(json.dumps(out, indent=1, default=str), encoding="utf-8")
