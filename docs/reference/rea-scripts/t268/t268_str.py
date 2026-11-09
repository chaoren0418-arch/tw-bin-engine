"""T268 (#629): print NUL-terminated bytes at VAs (read-only, nat.py). Usage: py -3 -I t268_str.py VA..."""
import sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img
im = Img()
for a in sys.argv[1:]:
    a = int(a, 16); b = im.read(a, 48)
    print(hex(a), hex(im.off(a)), b.split(b"\0")[0])
