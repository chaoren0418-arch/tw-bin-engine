import sys
sys.path.insert(0, r"scratch/rea/scripts")
from nat import Img
im = Img()
def d(va):
    o = im.off(va); return int.from_bytes(im.data[o:o+4], "little") if o is not None else None
def s(va):
    o = im.off(va); return im.data[o:o+80].split(b"\0")[0].decode("latin1") if o is not None else None
for slot in (0x11A9654,):
    for k in range(0, 40):
        vt = slot - 4*k
        col = d(vt - 4)
        if col and 0x1100000 < col < 0x1300000 and d(col) == 0:
            td = d(col + 12)
            n = s(td + 8) if td else None
            if n and n.startswith(".?AV"):
                print(hex(slot), "vtable", hex(vt), "index", k, n); break
