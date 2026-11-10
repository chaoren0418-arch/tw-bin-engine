"""T899 (#899): read-only. Compare list.spr (read by 3.81 LiTo.bin) and list.spz (read by TW16) in Text.pak
for list.spr fields 105 cloth / 111 stride / 112 altdir (+101/104/108 context). Decode as t783/listspr_fields.py.
Also reads LiTo/LTtext LTlist.spr when present. Writes JSON to argv[2] (under scratch/). Nothing is extracted to disk.
Usage: py -3 -I listspr_compare.py CLIENT_ROOT scratch/out.json [gfx ...]"""
import json, re, struct, sys, hashlib
from pathlib import Path
from Crypto.Cipher import DES, AES

root, out = Path(sys.argv[1]), Path(sys.argv[2])
want = [int(x) for x in sys.argv[3:]] or [0, 1, 37, 48, 61, 138, 734, 1186, 2786, 2796, 1395, 5940]
DESK = bytes.fromhex("7E214023255E243C")


def des(b):
    n = len(b) // 8 * 8
    return DES.new(DESK, DES.MODE_ECB).decrypt(b[:n]) + b[n:]


def entries(idx_path):
    idx = idx_path.read_bytes()
    count = struct.unpack_from("<I", idx)[0]
    rows = des(idx[4:])
    ent = {}
    for i in range(count):
        r = rows[i * 28:(i + 1) * 28]
        off, ln = struct.unpack_from("<I", r)[0], struct.unpack_from("<I", r, 24)[0]
        ent[r[4:24].split(b"\0")[0].decode("ascii", "replace")] = (off, ln)
    return ent


def load(pak_path, ent, name):
    with pak_path.open("rb") as pak:
        pak.seek(ent[name][0]); raw = bytearray(des(pak.read(ent[name][1])))
    if name.endswith(".spz"):
        assert raw[:5] == b"S1223"
        key, iv = bytes.fromhex("DC8401212A40200ADD25B9A70DB9C94E"), bytes.fromhex("3E0978AAC4D53063300C5F9A807F2246")
        n = (len(raw) - 5) // 16 * 16
        dec = bytearray(raw); dec[5:5 + n] = AES.new(key, AES.MODE_CBC, iv).decrypt(bytes(raw[5:5 + n]))
        for i in range(5 + n, len(raw)):
            dec[i] = raw[i] ^ (raw[i - 16] if n > 0 else iv[i - 5])
        raw = dec
    return bytes(raw)


def parse(raw):
    text = raw.decode("ascii", "replace").replace("\r", "")
    heads = list(re.finditer(r"(?m)^#(\d+)\s", text))
    meta = {}
    for i, m in enumerate(heads):
        meta[int(m.group(1))] = text[m.start():heads[i + 1].start() if i + 1 < len(heads) else len(text)]
    return meta


def field(e, f):
    m = re.search(r"(?<![\d.:\[])%d\.([A-Za-z_]+)\(([^)]*)\)" % f, e)
    return (m.group(1), m.group(2).split()) if m else None


res = {}
srcs = [("Text", root / "Text.idx", root / "Text.pak", n) for n in ("list.spr", "list.spz")]
srcs.append(("LTtext", root / "LiTo" / "LTtext.idx", root / "LiTo" / "LTtext.pak", "LTlist.spr"))
for tag, idx, pak, name in srcs:
    try:
        ent = entries(idx)
        if name not in ent:
            res[f"{tag}/{name}"] = {"missing": True}; continue
        raw = load(pak, ent, name)
    except Exception as ex:  # report, do not guess
        res[f"{tag}/{name}"] = {"error": repr(ex)}; continue
    meta = parse(raw)
    r = {"bytes": len(raw), "sha256": hashlib.sha256(raw).hexdigest(), "gfx_count": len(meta),
         "head": raw[:40].decode("ascii", "replace")}
    for f in (105, 111, 112):
        hits = {g: field(e, f) for g, e in meta.items() if field(e, f)}
        r[f"f{f}_count"] = len(hits)
        if f != 105:
            r[f"f{f}_all"] = {g: v[1] for g, v in sorted(hits.items())}
        else:
            r["f105_values"] = sorted({int(v) for _, vv in hits.values() for v in vv[1:]})[:400]
    r["want"] = {g: {f: field(meta[g], f) for f in (101, 104, 105, 108, 111, 112)} if g in meta else None for g in want}
    res[f"{tag}/{name}"] = r
out.write_text(json.dumps(res, indent=1, ensure_ascii=False), encoding="utf-8")
for k, v in res.items():
    print(k, {x: v[x] for x in v if x in ("bytes", "sha256", "gfx_count", "f105_count", "f111_count", "f112_count", "missing", "error", "head")})
