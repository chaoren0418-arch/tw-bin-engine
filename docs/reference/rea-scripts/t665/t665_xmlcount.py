import sys
sys.path.insert(0, r"scratch/rea/scripts")
from nat import Img
im = Img()
n = 0
while True:
    p = im.d32(0x137B3A8 + 4 * n)
    if not p or im.off(p) is None or not (0x1100000 < p < 0x1300000): break
    n += 1
print("xml table entries", n, "max reach", hex(0x153F460 + 4 * (n - 1)))
