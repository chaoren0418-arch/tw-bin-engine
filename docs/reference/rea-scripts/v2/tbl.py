"""V2 (#457): print entries of string-c.tbl from the local client Text.pak (read-only). Output only to scratch/.
Usage: py -3 -I docs/reference/rea-scripts/v2/tbl.py <out.txt> 553 554 1829 ..."""
import sys, struct
from Crypto.Cipher import DES
root = r"D:\lineage開發\0-客戶端"
k = DES.new(bytes.fromhex("7E214023255E243C"), DES.MODE_ECB)  # Text archive format constant (same as MagicRendering.DecodeText)
def dec(b):
    n = len(b) // 8 * 8
    return k.decrypt(b[:n]) + b[n:]
idx = open(root + r"\Text.idx", "rb").read()
cnt = struct.unpack("<I", idx[:4])[0]
rows = dec(idx[4:])
data = None
for i in range(cnt):
    r = rows[i * 28:(i + 1) * 28]
    if r[4:24].split(b"\0")[0] == b"string-c.tbl":
        off, ln = struct.unpack("<I", r[:4])[0], struct.unpack("<I", r[24:28])[0]
        with open(root + r"\Text.pak", "rb") as f:
            f.seek(off); data = dec(f.read(ln))
        break
lines = data.split(b"\n")  # line 0 = count header, then zero-based ids
with open(sys.argv[1], "w", encoding="utf-8") as out:
    for want in map(int, sys.argv[2:]):
        out.write(f"{want} {lines[want + 1].rstrip(b'\r').decode('cp950', 'replace')}\n")