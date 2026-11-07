import sys
d=open(sys.argv[1],'rb').read()
VA=0x11570d8; p=VA-0x401400
def varint(b,i):
    r=s=0
    while True:
        c=b[i]; i+=1; r|=(c&0x7f)<<s; s+=7
        if c<0x80: return r,i
def parse(b):
    i=0; out=[]
    while i<len(b):
        k,i=varint(b,i); f,w=k>>3,k&7
        if w==0: v,i=varint(b,i)
        elif w==2:
            n,i=varint(b,i); v=b[i:i+n]; i+=n
        elif w==5: v=b[i:i+4]; i+=4
        elif w==1: v=b[i:i+8]; i+=8
        else: raise ValueError((f,w,i))
        out.append((f,v))
    return out
# find end: parse greedily until failure
b=d[p:p+0x3000]
i=0; top=[]
try:
    while i<len(b):
        k,j=varint(b,i); f,w=k>>3,k&7
        if f==0 or f>20: break
        if w==2:
            n,j=varint(b,j); top.append((f,b[j:j+n])); i=j+n
        elif w==0:
            v,j=varint(b,j); top.append((f,v)); i=j
        else: break
except Exception as e: print('stop',e)
print('desc len',i, 'end VA',hex(VA+i))
TYPES={1:'double',2:'float',3:'int64',4:'uint64',5:'int32',8:'bool',9:'string',11:'message',12:'bytes',13:'uint32',14:'enum',17:'sint32'}
LAB={1:'optional',2:'required',3:'repeated'}
def field(fb):
    D=dict()
    for f,v in parse(fb): D[f]=v
    return f"  #{D.get(3)} {D.get(1,b'').decode()} {LAB.get(D.get(4))} {TYPES.get(D.get(5),D.get(5))} {D.get(6,b'').decode()} default={D.get(7,b'').decode() if 7 in D else None}"
def enum(eb, ind):
    name=None
    for f,v in parse(eb):
        if f==1: print(ind+'enum',v.decode())
        if f==2:
            E=dict(parse(v)); print(ind+'  ',E.get(1,b'').decode(),'=',E.get(2))
def msg(mb, ind=''):
    for f,v in parse(mb):
        if f==1: print(ind+'message',v.decode())
        elif f==2: print(ind+field(v))
        elif f==3: msg(v, ind+'    ')
        elif f==4: enum(v, ind+'  ')
for f,v in top:
    if f==1: print('file',v)
    elif f==2: print('package',v)
    elif f==4: msg(v)
    elif f==5: enum(v,'')
    else: print('top',f, v if isinstance(v,int) else v[:40])
