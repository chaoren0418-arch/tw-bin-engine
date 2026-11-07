for v in (0x7923E0, 0x4DEC00, 0x4DEEE0, 0x792230):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:20])
for a,b,t in xr(0x7923E0):
    P("call 7923E0 from", hex(a), b and hex(b)); P(dis(a-0x30, 16))
    if b: dec(b)
# StatusMiniMap mode (+0x18) writers: functions that read dword_C31E64 then +0x200 ...
users = sorted({b for a,b,t in xr(0xC31E64) if b})
P("C31E64 users", len(users), [hex(x) for x in users][:80])
