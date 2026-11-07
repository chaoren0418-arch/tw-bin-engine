import struct,sys,glob,os
root=r"D:\lineage開發\0-客戶端"
want={w.lower() for w in sys.argv[1:]}
for idx in sorted(glob.glob(root+r"\*.idx")):
    b=open(idx,"rb").read(); pak=idx[:-4]+".pak"
    if not os.path.exists(pak): continue
    with open(pak,"rb") as p:
        for i in range(4,len(b)-27,28):
            off,=struct.unpack_from("<I",b,i); nm=b[i+4:i+24].split(b"\0")[0].decode("latin1"); sz,=struct.unpack_from("<I",b,i+24)
            base,ext=os.path.splitext(nm.lower())
            if base in want:
                p.seek(off); h=p.read(8); w_,h_,a,c=struct.unpack("<4H",h)
                print(os.path.basename(idx),nm,sz,(w_,h_,a,c) if ext=='.img' else '')
