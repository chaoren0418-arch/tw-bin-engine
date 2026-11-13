import sys, struct, pathlib
# usage: callers.py <pe> <target va hex>   -> E8/E9 rel32 call sites in .text
import pefile
data = pathlib.Path(sys.argv[1]).read_bytes()
pe = pefile.PE(data=data, fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
s = pe.sections[0]; v = base + s.VirtualAddress; r = s.PointerToRawData; blob = data[r:r + s.SizeOfRawData]
t = int(sys.argv[2], 16)
for op in (0xE8, 0xE9):
    i = blob.find(bytes([op]))
    while i >= 0 and i + 5 <= len(blob):
        rel = struct.unpack_from('<i', blob, i + 1)[0]
        if v + i + 5 + rel == t: print(f'{"call" if op == 0xE8 else "jmp"} at VA {v + i:#x} FO {r + i:#x}')
        i = blob.find(bytes([op]), i + 1)
