P("raw dword ECD6E0", [hex(x) for x in find_all((0xECD6E0).to_bytes(4,"little"))])
P("raw dword ECD9D0", [hex(x) for x in find_all((0xECD9D0).to_bytes(4,"little"))])
fs = sorted({b for a,b,t in xr(0x153F440) if b})
P("153F440 users", len(fs), [hex(x) for x in fs])
# manual E8 call scan in .text for targets
import ida_segment
seg = ida_segment.get_segm_by_name(".text")
buf = ida_bytes.get_bytes(seg.start_ea, seg.end_ea-seg.start_ea)
import struct
tg = {0xECD6E0:"S800ctor", 0xECD9D0:"Sctor", 0xED0550:"Spaint"}
for i in range(len(buf)-5):
    if buf[i] == 0xE8:
        d = struct.unpack_from("<i", buf, i+1)[0]
        t = (seg.start_ea + i + 5 + d) & 0xffffffff
        if t in tg: P("E8", tg[t], hex(seg.start_ea+i))