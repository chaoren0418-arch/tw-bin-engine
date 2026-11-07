"""Read-only: locate window-related strings (ASCII and UTF-16LE) and their absolute-address refs. Usage: win_strings.py name [name...]"""
import sys, struct, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img
sys.stdout.reconfigure(encoding='utf-8')
im = Img()
d = im.data

def va_of(fo):
    for n, v, vs, p, r in im.secs:
        if p <= fo < p + r:
            return v + fo - p

for name in sys.argv[1:]:
    for enc, pat in (("a", name.encode("latin1")), ("w", name.encode("utf-16le"))):
        i = -1
        while True:
            i = d.find(pat, i + 1)
            if i < 0:
                break
            va = va_of(i)
            # walk back to string start
            st = i
            step = 2 if enc == "w" else 1
            while st - step >= 0 and d[st - step:st] != b"\0" * step:
                st -= step
            full = d[st:d.find(b"\0\0" if enc == "w" else b"\0", i)]
            text = full.decode("utf-16le", "replace") if enc == "w" else full.decode("latin1")
            sva = va_of(st)
            refs, j = [], -1
            p4 = struct.pack("<I", sva)
            while len(refs) < 10:
                j = d.find(p4, j + 1)
                if j < 0:
                    break
                refs.append(hex(va_of(j)))
            print(f"{name}\t{enc}\tVA={sva:#x}\tFO={st:#x}\t{text[:60]!r}\trefs={refs}")
