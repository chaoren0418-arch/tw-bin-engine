"""T227 (#534) read-only: map-effect light class table (sub_F0B570 .s32 path) + census + list.spr names.
Usage: py -3 -I light_census.py <TW1608262200.bin> <client root> <out.json>"""
import collections, json, pathlib, re, struct, sys

BIN, CLIENT, OUT = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), pathlib.Path(sys.argv[3])
b = BIN.read_bytes()
DATA = 0x401800  # .data FO = VA - 0x401800 (gap-matrix section 0)
t8 = list(struct.unpack_from("<12H", b, 0x138BE2C - DATA))
t11 = list(struct.unpack_from("<14H", b, 0x138BE44 - DATA))


def light(i):  # mirrors sub_F0B570 lines for the .s32 tail (0xF0C700..)
    if i in t8: return 8
    if i in t11: return 11
    if i in (1884, 1885): return 5
    if 1892 <= i <= 1897: return 7
    if i == 1886: return 14
    if 1901 <= i <= 1987: return 5
    if i == 2195: return 9
    if i == 2196: return 14
    if 2197 <= i <= 2202: return 9
    if i == 2209: return 5
    if i in (149, 1522): return 13
    if 3442 <= i <= 3446: return 2 * i - 6879
    return 0


names = {}
for line in (CLIENT / "TW13081901.txt").read_bytes().decode("cp950", "replace").splitlines():
    m = re.match(r"#(\d+)\s+\d+\s+(.*)", line)
    if m: names[int(m.group(1))] = m.group(2).strip()[:40]


def tail(buf):
    p = 128 * 64 * 4
    (nc,) = struct.unpack_from("<H", buf, p); p += 2 + nc * 6
    p += 8192 * 2
    (ng,) = struct.unpack_from("<i", buf, p); p += 4
    for _ in range(ng):
        p += 2
        (n,) = struct.unpack_from("<H", buf, p); p += 2 + n * 7
    (nt,) = struct.unpack_from("<i", buf, p); p += 4 + nt * 5
    (refs,) = struct.unpack_from("<i", buf, p); p += 4 + refs * 4
    (portals,) = struct.unpack_from("<H", buf, p); p += 2
    for _ in range(portals):
        p += 1 + buf[p] + 8
    if p == len(buf): return []
    (raw,) = struct.unpack_from("<H", buf, p); p += 2
    ext, n = bool(raw & 0x8000), raw & 0x7FFF
    out = []
    for _ in range(n):
        out.append(struct.unpack_from("<H", buf, p)[0]); p += 6 + (4 if ext else 0)
    return out


ids = collections.Counter()
files = 0
for f in (CLIENT / "map").rglob("*.s32"):
    files += 1
    try: ids.update(tail(f.read_bytes()))
    except Exception: pass
total = sum(ids.values())
by_light = collections.Counter()
lit_ids = collections.defaultdict(list)
for i, c in ids.items():
    k = light(i); by_light[k] += c
    if k: lit_ids[k].append((i, c, names.get(i, "")))
res = dict(t8=t8, t11=t11, files=files, records=total, distinct=len(ids),
           by_light={k: [v, round(100 * v / total, 1)] for k, v in sorted(by_light.items())},
           lit_ids={k: sorted(v) for k, v in sorted(lit_ids.items())},
           table_names={i: names.get(i, "") for i in sorted(set(t8 + t11 + list(range(3442, 3447)) + [149, 1522, 1884, 1885, 1886, 2195, 2196, 2209]))},
           effect_loc_light1={i: names.get(i, "") for i in range(2028, 2057)})
OUT.write_text(json.dumps(res, ensure_ascii=False, indent=1), encoding="utf-8")
print(json.dumps({k: res[k] for k in ("t8", "t11", "files", "records", "distinct", "by_light")}))
