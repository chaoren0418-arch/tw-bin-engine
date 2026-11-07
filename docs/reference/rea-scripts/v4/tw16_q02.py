
P(dis(0xB3D390, 70))
for v in (0xA0D670, 0x4F7740, 0x66E150, 0x586370, 0xB3D710, 0xB3D0E0, 0xA14A60,
          0xE540D0, 0xE55DD0, 0xE55C50, 0x8444A0, 0x856B80, 0x840EC0, 0x843020, 0x865B50, 0x101F990):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:30])
for v in (0x1386A34, 0x1386A40, 0x11F582C):
    P("DREF", hex(v), [(hex(a), b and hex(b)) for a,b,t in xr(v)][:20])
# callers of B3D390: show 6 insns around each call site
for a,b,t in xr(0xB3D390):
    if b:
        P("CALLSITE", hex(a), hex(b)); P(dis(idc.prev_head(idc.prev_head(idc.prev_head(a, 0), 0), 0), 9))
