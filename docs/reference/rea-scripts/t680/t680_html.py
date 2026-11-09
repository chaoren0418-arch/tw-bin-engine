import struct, sys, pathlib, re
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
try:
    from cryptography.hazmat.decrepit.ciphers.algorithms import TripleDES
except ImportError:
    TripleDES = algorithms.TripleDES
KEY = bytes.fromhex("7E214023255E243C")
def des(d):
    n=len(d)//8*8; c=Cipher(TripleDES(KEY*3),modes.ECB()).decryptor(); return c.update(d[:n])+c.finalize()+d[n:]
root=pathlib.Path(sys.argv[1]); idx=(root/"Text.idx").read_bytes(); cnt=struct.unpack_from("<I",idx)[0]; rows=des(idx[4:])
want=set(sys.argv[2:])
pak=open(root/"Text.pak","rb")
for i in range(cnt):
    r=rows[28*i:28*i+28]; name=r[4:24].split(b"\0")[0].decode("latin1")
    if name.lower() in want:
        off=struct.unpack_from("<I",r)[0]; ln=struct.unpack_from("<I",r,24)[0]; pak.seek(off); raw=des(pak.read(ln))
        t=raw.decode("cp950","replace"); t=re.sub(r"<[^>]+>"," ",t); t=re.sub(r"\s+"," ",t)
        open(r"scratch/rea/html1.txt","a",encoding="utf-8").write(f"{name} {ln} | {t[:200]}\n")