
for v in (0x4F7740, 0x581520, 0x66DAB0, 0x83BB50, 0xB39E80, 0x52D500, 0xE53460, 0xE53800, 0xE539B0, 0xE557D0, 0xE549C0):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:30])
P(dis(0xB3D710, 20))
P(dis(0xB3D640, 20))
# xml filename strings
import re
cnt = 0
for s in idautils.Strings():
    v = str(s)
    if v.lower().endswith(".xml") and cnt < 400:
        cnt += 1
        P("XML", hex(s.ea), v, sorted({hex(b) for a,b,t in xr(s.ea) if b})[:6])
