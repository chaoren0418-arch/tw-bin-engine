import sys, struct, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img
im = Img()
def va_of(fo):
    for n, v, vs, p, r in im.secs:
        if p <= fo < p + r: return v + fo - p
print(im.secs)
names = [b"S_WEATHER\0", b"S_CHANGE_LIGHT\0", b"S_POISON\0", b"S_BLIND\0", b"S_DRUNKEN\0", b"S_INVISIBLE\0", b"C_CONTROL_WEATHER\0", b"S_TIME\0", b"S_GAMETIME\0", b"before lightSource\0", b"rain\0", b"snow\0", b"Rain\0", b"Snow\0", b".?AVRunRain@Lineage@@", b".?AVRunSnow@Lineage@@", b".?AVLightning@@"]
d = im.data
for nm in names:
    i = -1
    while True:
        i = d.find(nm, i + 1)
        if i < 0: break
        if i and d[i-1] != 0 and not nm.startswith(b".?"): continue
        va = va_of(i)
        refs = []
        pat = struct.pack("<I", va)
        j = -1
        while len(refs) < 12:
            j = d.find(pat, j + 1)
            if j < 0: break
            refs.append(hex(va_of(j)))
        print(nm.rstrip(b"\0").decode(), hex(va), "refs", refs)
