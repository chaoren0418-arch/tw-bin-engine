import sys, struct, hashlib
try:
    import pefile
except ImportError:
    print("NOPEFILE"); sys.exit()
pe = pefile.PE(sys.argv[1], fast_load=True); pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY['IMAGE_DIRECTORY_ENTRY_RESOURCE']])
types = {e.id: e for e in pe.DIRECTORY_ENTRY_RESOURCE.entries}
def raw(tid, rid):
    for e in types[tid].directory.entries:
        if e.id == rid:
            d = e.directory.entries[0].data.struct
            return pe.get_data(d.OffsetToData, d.Size), d.OffsetToData
    return None, None
print("types", sorted(k for k in types if k))
for rid in [int(x,0) for x in sys.argv[2:]]:
    g, rva = raw(12, rid)
    if g is None: print(rid, "no group"); continue
    n = struct.unpack_from('<HHH', g)[2]
    for i in range(n):
        w,h,_,_,hx,hy,size,cid = struct.unpack_from('<HHHHHHIH', g, 6+14*i) if False else (None,)*8
    ent = [struct.unpack_from('<HHHHIH', g, 6+14*i) for i in range(n)]
    for (w,h2,planes,bpp,size,cid) in ent:
        c, crva = raw(1, cid)
        hx, hy = struct.unpack_from('<HH', c)
        print(f"group {rid} (0x{rid:X}) rva=0x{rva:X} -> cursor {cid} w={w} h={h2//2 if h2 else h2} hotspot=({hx},{hy}) size={len(c)} sha256={hashlib.sha256(c).hexdigest()[:16]}")
