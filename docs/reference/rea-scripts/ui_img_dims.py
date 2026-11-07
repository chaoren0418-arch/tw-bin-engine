"""Read-only: print .img headers (w,h) for surf ids from Sprite*.pak (no files written)."""
import struct,sys,glob,os
root=r"D:\lineage開發\0-客戶端"
want={w.lower() for w in sys.argv[1:]}; out={}
for idx in sorted(glob.glob(root+r"\Sprite*.idx")):
    b=open(idx,"rb").read(); pak=idx[:-4]+".pak"
    with open(pak,"rb") as p:
        for i in range(4,len(b)-27,28):
            off,=struct.unpack_from("<I",b,i); nm=b[i+4:i+24].split(b"\0")[0].decode("latin1"); sz,=struct.unpack_from("<I",b,i+24)
            if nm.lower().endswith(".img") and nm[:-4].lower() in want:
                p.seek(off); h=p.read(8); w_,h_,a,c=struct.unpack("<4H",h)
                out[nm[:-4]]=(w_,h_,a,c,sz,(sz-8)/(w_*h_) if w_*h_ else 0)
for k in sys.argv[1:]: print(k,out.get(k))
