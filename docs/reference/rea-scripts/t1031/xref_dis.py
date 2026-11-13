import sys, struct, pathlib
# usage: xref_dis.py <pe> scan <lo hex> <hi hex>      -> list .text places whose 4 bytes are an address in [lo,hi)
#        xref_dis.py <pe> dis <va hex> <bytes>         -> linear disassembly from VA
#        xref_dis.py <pe> func <va hex> [max]          -> walk back to function start (after int3/ret padding) and disassemble to ret
import pefile, capstone
data = pathlib.Path(sys.argv[1]).read_bytes()
pe = pefile.PE(data=data, fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
secs = [(s.Name.rstrip(b'\0').decode(errors='replace'), base + s.VirtualAddress, s.PointerToRawData, s.SizeOfRawData) for s in pe.sections]
def fo(va):
    for n, v, r, sz in secs:
        if v <= va < v + sz: return r + va - v
    raise SystemExit(f'va {va:#x} not in file')
def va_of(o):
    for n, v, r, sz in secs:
        if r <= o < r + sz: return v + o - r
    return 0
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
mode = sys.argv[2]
if mode == 'scan':
    lo, hi = int(sys.argv[3], 16), int(sys.argv[4], 16)
    n, v, r, sz = secs[0]
    blob = data[r:r + sz]
    # addresses share the top 2 bytes typically; brute force over candidates by 3rd/4th byte prefix
    hits = []
    prefixes = {struct.pack('<I', a)[2:] for a in range(lo, hi, 0x100)} | {struct.pack('<I', hi - 1)[2:]}
    for pre in prefixes:
        i = blob.find(pre)
        while i >= 0:
            if i >= 2:
                a = struct.unpack_from('<I', blob, i - 2)[0]
                if lo <= a < hi: hits.append((v + i - 2, a))
            i = blob.find(pre, i + 1)
    for at, a in sorted(set(hits)):
        print(f'ref at VA {at:#x} FO {r + at - v:#x} -> {a:#x}')
elif mode == 'dis':
    va = int(sys.argv[3], 16); n = int(sys.argv[4])
    o = fo(va)
    for ins in md.disasm(data[o:o + n], va):
        print(f'{ins.address:#x} (FO {fo(ins.address):#x}): {ins.mnemonic} {ins.op_str}')
elif mode == 'func':
    va = int(sys.argv[3], 16); mx = int(sys.argv[4]) if len(sys.argv) > 4 else 1200
    o = fo(va); s = o
    # walk back to 0xCC padding or 'ret; push ebp' boundary
    while s > o - 0x3000:
        if data[s - 1] == 0xCC and data[s] != 0xCC: break
        s -= 1
    start = va - (o - s)
    print(f'function start guess VA {start:#x} FO {s:#x}')
    for ins in md.disasm(data[s:s + mx], start):
        mark = ' <==' if ins.address <= va < ins.address + ins.size else ''
        print(f'{ins.address:#x}: {ins.mnemonic} {ins.op_str}{mark}')
        if ins.mnemonic == 'int3': break
