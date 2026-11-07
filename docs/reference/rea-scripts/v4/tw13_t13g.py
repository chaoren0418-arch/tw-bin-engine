
a = 0x8D381C; b = 0x8D372C
for i in range(30):
    fa = ida_bytes.get_dword(a + 4*i); fb = ida_bytes.get_dword(b + 4*i)
    P(i, hex(i*4), hex(fa), hex(fb), "DIFF" if fa != fb else "")
    if fa != fb and ida_funcs.get_func(fa):
        t = dec(fa) or ""
        P("   ", t.count("\n"))
P(dis(0x759A10, 3))
dec(0x759A10); dec(0x429FF0)
