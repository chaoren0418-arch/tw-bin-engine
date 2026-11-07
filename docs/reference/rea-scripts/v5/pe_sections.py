import struct, sys
for p in sys.argv[1:]:
    b = open(p, "rb").read(4096)
    pe = struct.unpack_from("<I", b, 0x3C)[0]
    n = struct.unpack_from("<H", b, pe + 6)[0]
    opt = struct.unpack_from("<H", b, pe + 20)[0]
    ib = struct.unpack_from("<I", b, pe + 24 + 28)[0]
    print(p, "imagebase", hex(ib))
    o = pe + 24 + opt
    for i in range(n):
        name = b[o:o+8].rstrip(b"\0").decode("latin1")
        vs, va, rs, ra = struct.unpack_from("<IIII", b, o + 8)
        print(f"  {name:8} VA {ib+va:#x}-{ib+va+vs:#x} raw {ra:#x} size {rs:#x}  delta(VA-FO)={ib+va-ra:#x}")
        o += 40
