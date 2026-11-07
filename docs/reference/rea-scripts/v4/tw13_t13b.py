
for v in (0x5FE870, 0x7927F0, 0x7928D0, 0x792030, 0x783A60):
    t = dec(v); P("DEC", hex(v), (t or "").count("\n"), "callers", sorted({hex(b) for a,b,tt in xr(v) if b})[:10])
t = dec(0x5FE870) or ""
L = t.split("\n")
for i, l in enumerate(L):
    if "LEVEL_BTN" in l or "BALOG_BTN" in l or "SHOUT_BTN" in l or "FOOD_WARN_BTN" in l:
        P("CTX", "\n".join(L[i:i+10]))
# who calls 5FE870 and what is 'this'
for a,b,tt in xr(0x5FE870):
    P("CALLER", hex(a), b and hex(b)); P(dis(idc.prev_head(idc.prev_head(a,0),0), 4))
