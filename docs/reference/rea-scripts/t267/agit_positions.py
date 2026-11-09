"""T267 (#628): parse the ida_dis.py listing of sub_113F600 (static initializer) into the AgitMapWindow marker table.
Each entry is `push y; push x; mov ecx, offset <slot>; call sub_4F84A0`. Region r (4-8), number n (1-70) lives at
0x13A96F0 + 0x230*(r-4) + 8n; region 1 reuses region 8 (sub_542640). Read-only; output JSON goes to scratch/.
Usage: py -3 -I agit_positions.py dis_0113F600.asm out.json"""
import json, re, sys

pushes, rows, target = [], [], None
for line in open(sys.argv[1], encoding="utf-8").read().splitlines():
    va, ins = line[:8], line[10:].strip()
    m = re.match(r"push\s+([0-9A-Fa-f]+)h?\b", ins)
    if m:
        pushes.append(int(m.group(1), 16))
        continue
    m = re.match(r"mov\s+ecx, offset (?:unk|dword|stru)_([0-9A-F]+)", ins)
    if m:
        target = int(m.group(1), 16)
        continue
    if ins.startswith("call    sub_4F84A0"):
        rows.append((target, pushes[-1], pushes[-2]))  # last push = x (first argument), the one before = y
        pushes = []
        continue
    if ins.startswith("retn") or ins.startswith("pop     ebp"):
        break
table = {}
for slot, x, y in rows:
    off = slot - 0x13A96F0
    region = 4 + (off - 8) // 0x230
    number = (off - 0x230 * (region - 4)) // 8
    table.setdefault(region, []).append([number, x, y])
for region, items in sorted(table.items()):
    filled = [n for n, x, y in items if (x, y) != (0, 0)]
    print(f"region {region}: {len(items)} slots, {len(filled)} non-zero (1..{max(filled) if filled else 0})")
json.dump({str(k): sorted(v) for k, v in table.items()}, open(sys.argv[2], "w"), indent=1)
