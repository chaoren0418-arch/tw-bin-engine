"""T225 (#532): read-only, in-memory: Text.idx/Text.pak -> string-c.tbl (DES-ECB, same constant as
engine MagicRendering.cs DecodeText) and print the requested string ids. Nothing is written except the
optional summary JSON (ids asked + table identity), which stays in scratch/.
Usage: py -3 -I t225_strings.py CLIENT_ROOT OUT.json ID [ID...]"""
import hashlib, json, os, struct, sys, pathlib
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


root = pathlib.Path(sys.argv[1])
idx = (root / "Text.idx").read_bytes()
count = struct.unpack_from("<I", idx)[0]
assert len(idx) == 4 + 28 * count
rows = des(idx[4:])
entry = None
for i in range(count):
    r = rows[i * 28:(i + 1) * 28]
    name = r[4:24].split(b"\0")[0].decode("latin1")
    if name.lower() == os.environ.get("TBL", "string-c.tbl").lower():
        entry = (i, struct.unpack_from("<I", r)[0], struct.unpack_from("<I", r, 24)[0])
assert entry, "table not found"
with open(root / "Text.pak", "rb") as f:
    f.seek(entry[1])
    raw = des(f.read(entry[2]))
text = raw.decode("cp950", errors="replace").replace("\r", "")
lines = text.split("\n")
head = lines[0].strip()
table = lines[1:]
out = {"record": entry[0], "pak_offset": entry[1], "length": entry[2],
       "decoded_sha256": hashlib.sha256(raw).hexdigest(), "header": head, "lines": len(table), "ids": {}}
for a in sys.argv[3:]:
    i = int(a)
    out["ids"][a] = table[i] if 0 <= i < len(table) else None
pathlib.Path(sys.argv[2]).write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
print(json.dumps({k: v for k, v in out.items() if k != "ids"}, ensure_ascii=False))
for k, v in out["ids"].items():
    print(k, v)
