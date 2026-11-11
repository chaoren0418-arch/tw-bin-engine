import sys
d=open(sys.argv[1],'rb').read()
def r(va,n): return d[va-0x401400:va-0x401400+n]
for va in (0x1235284,0x12352d0,0x1235744,0x1235790):
    b=r(va,0x48); print(hex(va), b.decode('utf-16le','replace').split('\0')[0])
for k in range(10):
    va=0x1172f80+4*k; b=r(va,4); print(hex(va),'FO',hex(va-0x401400), b.hex(), b[:2].decode('cp950','replace'))
