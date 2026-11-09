"""#628: read-only. Find crown.raw in the client's Sprite*.idx (plain index) and summarise the payload.
Nothing is written outside scratch/; the payload itself is not saved.
Usage: py -3 -I crown_raw.py CLIENT_ROOT"""
import hashlib, pathlib, struct, sys

root = pathlib.Path(sys.argv[1])
names = ["Sprite.idx"] + [f"Sprite{i:02d}.idx" for i in range(16)]
for n in names:
    idx = (root / n).read_bytes()
    count = struct.unpack_from("<I", idx)[0]
    for i in range(count):
        at = 4 + 28 * i
        name = idx[at + 4:at + 24].split(b"\0")[0].decode("latin1")
        if name.lower() == "crown.raw":
            off, size = struct.unpack_from("<I", idx, at)[0], struct.unpack_from("<I", idx, at + 24)[0]
            with open(root / n.replace(".idx", ".pak"), "rb") as f:
                f.seek(off)
                data = f.read(size)
            print(n, "record", i, "offset", off, "size", size, "sha256", hashlib.sha256(data).hexdigest())
            print("frames of 384 bytes:", size / 384)
            words = struct.unpack(f"<{size // 2}H", data[:size // 2 * 2])
            for fr in range(min(12, size // 384)):
                px = words[fr * 192:(fr + 1) * 192]
                opaque = sum(1 for w in px if w)
                rows = ["".join("#" if px[y * 16 + x] else "." for x in range(16)) for y in range(12)]
                print(f"frame {fr}: opaque {opaque}/192")
                if fr in (0, 6):
                    print("\n".join(rows))
