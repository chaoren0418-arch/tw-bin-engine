"""Print VA -> (section, File Offset) for cited addresses. Usage: va2off.py VA [VA...]"""
import sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img
im = Img()
for a in sys.argv[1:]:
    va = int(a, 16); o = im.off(va); s = im.sec(va)
    print(f"{va:08X} {s[0]} {s[1]} {('0x%X' % o) if o is not None else 'bss(no file data)'}")
