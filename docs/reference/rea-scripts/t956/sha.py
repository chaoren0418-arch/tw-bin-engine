import sys,hashlib
d=open(sys.argv[1],'rb').read()
fo=0x1368CEC-0x401800
b=d[fo:fo+0x34]; print(hex(fo), len(b), hashlib.sha256(b).hexdigest())
t=d[0x1172f80-0x401400:0x1172f80-0x401400+40]; print(hashlib.sha256(t).hexdigest())
