import struct, sys, pathlib
sys.path.insert(0, r"scratch/rea/scripts")
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
try:
    from cryptography.hazmat.decrepit.ciphers.algorithms import TripleDES
except ImportError:
    TripleDES = algorithms.TripleDES
KEY = bytes.fromhex("7E214023255E243C")
def des(d):
    n=len(d)//8*8; c=Cipher(TripleDES(KEY*3),modes.ECB()).decryptor(); return c.update(d[:n])+c.finalize()+d[n:]
root=pathlib.Path(sys.argv[1]); idx=(root/"Text.idx").read_bytes(); cnt=struct.unpack_from("<I",idx)[0]; rows=des(idx[4:])
for i in range(cnt):
    r=rows[28*i:28*i+28]; name=r[4:24].split(b"\0")[0].decode("latin1")
    if name.endswith(".tbl"): print(i,name,struct.unpack_from("<I",r,24)[0])