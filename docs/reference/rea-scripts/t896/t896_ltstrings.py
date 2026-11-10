"""T896 (#896): read-only. Generalises t680_strings.py to any idx/pak pair (e.g. LiTo\\LTtext.idx/.pak).
Usage:
  py -3 -I t896_ltstrings.py IDX PAK list                 -> list entry names (tries DES and plain idx)
  py -3 -I t896_ltstrings.py IDX PAK NAME OUT.json ID...  -> decode table NAME, print ids (line k = id k, header dropped)
Nothing is written except OUT.json (scratch/)."""
import hashlib, json, struct, sys, pathlib
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
try:
    from cryptography.hazmat.decrepit.ciphers.algorithms import TripleDES
except ImportError:
    TripleDES = algorithms.TripleDES

KEY = bytes.fromhex("7E214023255E243C")


def des(data):
    n = len(data) // 8 * 8
    d = Cipher(TripleDES(KEY * 3), modes.ECB()).decryptor()
    return d.update(data[:n]) + d.finalize() + data[n:]


def entries(idx):
    count = struct.unpack_from("<I", idx)[0]
    body = idx[4:4 + 28 * count]
    for mode, rows in (("des", des(body)), ("plain", body)):
        res = []
        for i in range(count):
            r = rows[i * 28:(i + 1) * 28]
            res.append((i, r[4:24].split(b"\0")[0].decode("latin1"), struct.unpack_from("<I", r)[0], struct.unpack_from("<I", r, 24)[0]))
        if all(n and all(32 <= ord(c) < 127 for c in n) for _, n, _, _ in res[:5]):
            return mode, res
    return "unknown", res


idx = pathlib.Path(sys.argv[1]).read_bytes()
pak = pathlib.Path(sys.argv[2])
mode, ents = entries(idx)
if sys.argv[3] == "list":
    print("idx_mode", mode, "count", len(ents))
    for e in ents:
        print(*e)
    sys.exit(0)
ent = next(e for e in ents if e[1].lower() == sys.argv[3].lower())
with open(pak, "rb") as f:
    f.seek(ent[2])
    raw = f.read(ent[3])
dec = des(raw) if mode == "des" else raw
for cand, label in ((dec, mode), (raw, "plain"), (des(raw), "des")):
    try:
        text = cand.decode("cp950")
        dec, mode = cand, label
        break
    except UnicodeDecodeError:
        continue
text = dec.decode("cp950", errors="replace").replace("\r", "")
lines = text.split("\n")
table = lines[1:]
out = {"idx": sys.argv[1], "entry": ent[1], "record": ent[0], "pak_offset": ent[2], "length": ent[3], "body_mode": mode,
       "decoded_sha256": hashlib.sha256(dec).hexdigest(), "header": lines[0].strip(), "lines": len(table), "ids": {}}
for a in sys.argv[5:]:
    i = int(a)
    out["ids"][a] = table[i] if 0 <= i < len(table) else None
pathlib.Path(sys.argv[4]).write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
print(json.dumps({k: v for k, v in out.items() if k != "ids"}, ensure_ascii=False))
for k, v in out["ids"].items():
    print(k, v)
