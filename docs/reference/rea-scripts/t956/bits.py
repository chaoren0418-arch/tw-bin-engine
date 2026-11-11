import sys
d=open(sys.argv[1],'rb').read()
fo=0x1368CEC-0x401800
b=d[fo:fo+0x34]
print(b.hex(' '))
def show(off,w,h):
    bpr=(w+7)//8
    for r in range(h):
        row=b[off+r*bpr:off+(r+1)*bpr]
        bits=int.from_bytes(row,'big'); n=bpr*8
        print(''.join('#' if bits>>(n-1-c)&1 else '.' for c in range(w)))
    print()
for name,off,w,h in [('tailF',0,7,4),('tailF_up',4,7,4),('TL',8,4,4),('TR',12,4,4),('BL',16,4,4),('BR',20,4,4),('tailC',0x18,7,4),('tailC_up',0x1C,7,4),('tailO',0x20,9,5),('tailO_up',0x2A,9,5)]:
    print(name, hex(0x1368CEC+off), 'FO', hex(fo+off)); show(off,w,h)
