import sys; sys.path.insert(0, r"docs\reference\rea-scripts")
import nat
im = nat.Img(path=r"scratch\rea\bin\tw16.bin")
for n,v,vs,p,r in im.secs: print(n, hex(v), hex(vs), hex(p), hex(r))
for va in (0x94FB00,0x94F100,0x94FCEA,0xF10205,0xF10230,0xF102E7,0x841770,0x933850,0xF044A0,0xA231C0,0xA22830,0xF0A580,0xF06A84,0xF128E9,0xF04180,0xF18F40,0xF191A0,0xF188A0,0xF19680,0xF18AB0,0xF19F90,0xF0F0C0,0x942B40,0x944F50,0x1369D70,0x1369D7C,0x1544388,0x153C998,0x947530):
    print(hex(va), im.sec(va), hex(im.off(va)) if im.off(va) is not None else None)
print("pal0..3", [hex(im.d32(0x1369D70+4*i)) for i in range(4)])
