import sys
sys.path.insert(0, r"scratch/rea/scripts")
from nat import Img
im = Img()
for i in range(0, 200):
    p = im.d32(0x137B3A8 + 4 * i)
    if not p or im.off(p) is None: break
    s = im.read(p, 48).split(b"\0")[0]
    if any(k in s for k in (b"ookmark", b"ummon", b"oundScore", b"btain", b"pell")):
        print(hex(i), hex(0x137B3A8 + 4*i), hex(p), s.decode("latin1"))
