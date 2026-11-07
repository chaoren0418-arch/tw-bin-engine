import sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img, fmt
im = Img()
a = int(sys.argv[1], 16); n = int(sys.argv[2], 16)
for i in im.dis(a, n):
    print(fmt(i))
