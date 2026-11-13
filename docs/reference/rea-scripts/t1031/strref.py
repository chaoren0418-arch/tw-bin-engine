import sys, struct, pathlib, re
# usage: strref.py <pe> <substr> [substr...]  -> ASCII strings containing substr (case-insens.) with VA/FO and .text push/mov refs
import pefile
data = pathlib.Path(sys.argv[1]).read_bytes()
pe = pefile.PE(data=data, fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
secs = [(s.Name.rstrip(b'\0').decode(errors='replace'), base + s.VirtualAddress, s.PointerToRawData, s.SizeOfRawData) for s in pe.sections]
def va_of(o):
    for n, v, r, sz in secs:
        if r <= o < r + sz: return v + o - r
    return 0
tn, tv, tr, tsz = secs[0]; text = data[tr:tr + tsz]
low = data.lower()
for sub in sys.argv[2:]:
    b = sub.lower().encode('ascii'); i = low.find(b); seen = set()
    while i >= 0:
        s = i
        while s > 0 and 32 <= data[s - 1] < 127: s -= 1
        e = i
        while e < len(data) and 32 <= data[e] < 127: e += 1
        if s not in seen and va_of(s):
            seen.add(s); va = va_of(s); pat = struct.pack('<I', va)
            refs = []; j = text.find(pat)
            while j >= 0: refs.append(tv + j); j = text.find(pat, j + 1)
            print(f'{sub!r}: "{data[s:e].decode("ascii")}" VA {va:#x} FO {s:#x} refs(operand VA) {[hex(r) for r in refs[:8]]}')
        i = low.find(b, e)
