"""Read-only: dump the UI XML file-name pointer table (around VA 0x137B3A0) with VA/FO of each entry."""
import sys, pathlib
sys.path.insert(0, str(pathlib.Path(__file__).parent))
sys.stdout.reconfigure(encoding="utf-8")
from nat import Img
im = Img()
lo, hi = int(sys.argv[1], 16), int(sys.argv[2], 16)
for va in range(lo, hi, 4):
    p = im.d32(va)
    s = im.read(p, 48) if im.off(p) is not None else b""
    s = s.split(b"\0")[0].decode("latin1") if s else ""
    print(f"{va:#x}\tFO={im.off(va):#x}\t-> {p:#x}\t{s}")
