"""T267 (#628): read-only, in-memory. Decrypt string-c.tbl and MapDesc-c.tbl from the client's Text.pak (DES-ECB,
same constant as t225_strings.py), print the requested string-c ids, then compare every MapSystem*.xml mapDesc id
against MapDesc-c.tbl and string-c.tbl. Nothing is written; redirect stdout into scratch/ if needed.
Usage: py -3 -I -X utf8 text_tables.py CLIENT_ROOT DECRYPTED_XML_DIR ID [ID...]"""
import hashlib, re, struct, sys, pathlib
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


root, xml_dir = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
idx = (root / "Text.idx").read_bytes()
count = struct.unpack_from("<I", idx)[0]
rows = des(idx[4:])
tables = {}
for i in range(count):
    r = rows[i * 28:(i + 1) * 28]
    name = r[4:24].split(b"\0")[0].decode("latin1")
    if name.lower() in ("string-c.tbl", "mapdesc-c.tbl", "desc-c.tbl"):
        off, ln = struct.unpack_from("<I", r)[0], struct.unpack_from("<I", r, 24)[0]
        with open(root / "Text.pak", "rb") as f:
            f.seek(off)
            raw = des(f.read(ln))
        lines = raw.decode("cp950", errors="replace").replace("\r", "").split("\n")
        tables[name.lower()] = lines
        print(f"{name}: record {i}, length {ln}, sha256 {hashlib.sha256(raw).hexdigest()}, rows {lines[0].strip()}")
sc, md = tables["string-c.tbl"], tables["mapdesc-c.tbl"]
for a in sys.argv[3:]:
    k = int(a)
    print(k, repr(sc[k + 1]) if 0 <= k < int(sc[0]) else "<out of range>")
for xml in ("MapSystem-c.xml", "MapSystem.xml", "MapSystem86.xml"):
    p = xml_dir / xml
    if not p.exists():
        continue
    pairs = re.findall(r'id\s*=\s*"(\d+)"[^>]*?mapDesc\s*=\s*"(\d+)"', p.read_bytes().decode("cp950", "replace"), re.I)
    ids = [int(d) for _, d in pairs]
    print(f"{xml}: {len(pairs)} entries, mapDesc {min(ids)}-{max(ids)}, out of MapDesc-c range: "
          f"{sum(1 for k in ids if k >= int(md[0]))}")
    for zone, d in pairs[:5]:
        k = int(d)
        print(f"  zone {zone} mapDesc {k}: MapDesc-c {md[k + 1]!r} | string-c {sc[k + 1]!r}")
