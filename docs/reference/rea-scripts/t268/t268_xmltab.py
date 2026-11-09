"""T268 (#629): read XML filename table off_137B3A8 entries 0x2B/0x2F (read-only)."""
import sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img
im = Img()
for i in (0x2B, 0x2F):
    p = im.d32(0x137B3A8 + 4 * i)
    print(hex(i), hex(p), hex(im.off(p)), im.read(p, 40).split(b"\0")[0])
