"""T337 (#783): read-only count of list.spr fields 100-119 (which gfx set them, sample values). Same decode as
t180/listspr_109.py. Writes only the JSON summary given as argv[2] (under scratch/).
Usage: py -3 -I listspr_fields.py CLIENT_ROOT scratch/out.json"""
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
res = {"source": src_name, "sha256": hashlib.sha256(raw).hexdigest(), "gfx_count": len(meta), "fields": {}}
for f in range(100, 120):
    rx = re.compile(r"(?<![\d.:\[])%d\.([A-Za-z_]+\([^)]*\))" % f)
    hits = []
    for g, e in sorted(meta.items()):
        m = rx.search(e)
        if m:
            hits.append((g, m.group(1).strip()[:40]))
    res["fields"][f] = {"count": len(hits), "samples": hits[:12]}
out.write_text(json.dumps(res, indent=1, ensure_ascii=False), encoding="utf-8")
print(json.dumps({"sha256": res["sha256"], "gfx": res["gfx_count"], "counts": {k: v["count"] for k, v in res["fields"].items()}}))

