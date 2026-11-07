"""T180 (#482): read-only summary of list.spr field 109 (effect kind, param) from the client's Text.pak.
Same decode as engine/source/MagicRendering.cs (EffectSpriteCatalog). Nothing is written except the JSON
summary passed as argv[2] (put it under scratch/). Usage:
  py -3 -I listspr_109.py "D:/lineage開發/0-客戶端" scratch/rea/out/listspr109.json [gfx ...]
"""
import json, re, struct, sys, hashlib, collections
from pathlib import Path
from Crypto.Cipher import DES, AES

root, out = Path(sys.argv[1]), Path(sys.argv[2])
want = [int(x) for x in sys.argv[3:]]
DESK = bytes.fromhex("7E214023255E243C")

def des(b):
    n = len(b) // 8 * 8
    return DES.new(DESK, DES.MODE_ECB).decrypt(b[:n]) + b[n:]

idx = (root / "Text.idx").read_bytes()
count = struct.unpack_from("<I", idx)[0]
rows = des(idx[4:])
ent = {}
for i in range(count):
    r = rows[i * 28:(i + 1) * 28]
    off, ln = struct.unpack_from("<I", r)[0], struct.unpack_from("<I", r, 24)[0]
    name = r[4:24].split(b"\0")[0].decode("ascii", "replace")
    if name in ("list.spz", "list.spr"):
        ent[name] = (off, ln)
pak = (root / "Text.pak").open("rb")
if "list.spz" in ent:
    pak.seek(ent["list.spz"][0]); raw = bytearray(des(pak.read(ent["list.spz"][1])))
    assert raw[:5] == b"S1223"
    key, iv = bytes.fromhex("DC8401212A40200ADD25B9A70DB9C94E"), bytes.fromhex("3E0978AAC4D53063300C5F9A807F2246")
    n = (len(raw) - 5) // 16 * 16
    dec = bytearray(raw); dec[5:5 + n] = AES.new(key, AES.MODE_CBC, iv).decrypt(bytes(raw[5:5 + n]))
    for i in range(5 + n, len(raw)):
        dec[i] = raw[i] ^ (raw[i - 16] if n > 0 else iv[i - 5])
    raw, src_name = bytes(dec), "list.spz"
else:
    pak.seek(ent["list.spr"][0]); raw, src_name = des(pak.read(ent["list.spr"][1])), "list.spr"
text = raw.decode("ascii", "replace").replace("\r", "")
heads = list(re.finditer(r"(?m)^#(\d+)\s", text))
meta = {}
for i, m in enumerate(heads):
    meta[int(m.group(1))] = text[m.start():heads[i + 1].start() if i + 1 < len(heads) else len(text)]
kinds = collections.Counter(); bykind = collections.defaultdict(list); detail = {}
for g, e in meta.items():
    m = re.search(r"(?<![\d.])109\.[^(]*\((\d+)\s+(-?\d+)\)", e)
    k = (int(m.group(1)), int(m.group(2))) if m else (0, -1)
    kinds[k[0]] += 1; bykind[k[0]].append(g)
    if g in want:
        act = re.findall(r"(?<![\d.])(\d+)\.[^(]*\((\d+) (\d+),", e)
        flags = {a: e.count("!") for a in []}
        detail[g] = {"kind": k[0], "param": k[1], "head": e.split("\n")[0][:60],
                     "actions": [a[0] for a in act][:12], "bang_count": e.count("!")}
res = {"source": src_name, "sha256": hashlib.sha256(raw).hexdigest(), "gfx_count": len(meta),
       "kind_counts": dict(sorted(kinds.items())),
       "kind_samples": {k: v[:40] for k, v in sorted(bykind.items()) if k != 0},
       "detail": detail}
out.write_text(json.dumps(res, indent=1, ensure_ascii=False), encoding="utf-8")
print(json.dumps({k: res[k] for k in ("source", "sha256", "gfx_count", "kind_counts")}))
