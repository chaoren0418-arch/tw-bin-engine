for v in (0xECD6E0, 0xED0550, 0xED0540, 0x11E29E4, 0x11E2804, 0xECD9D0, 0xED0610, 0x11E29B4):
    P(hex(v), [(hex(a), b and hex(b), t) for a,b,t in xr(v)][:20])
P(dis(0xED0540, 6))