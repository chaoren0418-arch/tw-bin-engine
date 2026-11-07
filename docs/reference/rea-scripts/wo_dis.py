"""T182 (#484): Capstone disassembly of a VA range. Read-only; uses nat.py. (Not named dis.py: that shadows the stdlib.)
Usage: py -3 -I wo_dis.py 0x8D4A90 0x60"""
import sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img, fmt
im = Img()
a = int(sys.argv[1], 16); n = int(sys.argv[2], 16)
for i in im.dis(a, n): print(fmt(i))
