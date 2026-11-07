"""#430 cross-check of TW16 ambience findings against TW13081901.unpacked.exe (read-only byte search).
Usage: py -3 -I -X utf8 xcheck13_ambience.py
"""
import hashlib, pathlib, re, struct, sys
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from nat import Img

P13 = r"D:\lineage開發\登入器bin資源\TW13081901bin拆解\TW13081901.unpacked.exe"
SHA13 = "653da514f750d4714fde2c83e5162aeef553c876ec541fe1cfebf6970f5c62a4"
tw16 = Img()
b13 = pathlib.Path(P13).read_bytes()
assert hashlib.sha256(b13).hexdigest() == SHA13, "unexpected TW13 identity"
v, t16 = tw16.text()

tab = tw16.read(0x1374168, 1536)  # darkness factor table: 3 modes x 16 levels x 32
for mode in range(3):
    j = b13.find(tab[512 * mode:512 * (mode + 1)])
    print(f"darkness table mode {mode}: TW13 file offset", hex(j) if j >= 0 else None)
for s in [b"source.lmp\0", b"crown.raw\0", b"S_WEATHER\0", b"S_CHANGE_LIGHT\0"]:
    print(s.rstrip(b"\0").decode(), "in TW13:", b13.find(s) >= 0)

rain = re.compile(rb"\xc7\x45.\x80\x00\x00\x00.{2,12}\xc7\x45.\x00\x01\x00\x00.{2,12}\xc7\x45.\x00\x02\x00\x00", re.S)
print("rain 128/256/512 sequence TW16 VA", [hex(v + m.start()) for m in rain.finditer(t16)][:4],
      "TW13 file offset", [hex(m.start()) for m in rain.finditer(b13)][:4])

day = re.compile(rb"\xc7\x45.\x90\x01\x00\x00\xc7\x45.\xb0\x04\x00\x00", re.S)  # mov [ebp-x],400 ; mov [ebp-y],1200
print("day curve 400/1200 init TW16 VA", [hex(v + m.start()) for m in day.finditer(t16)],
      "TW13 file offset", [hex(m.start()) for m in day.finditer(b13)])