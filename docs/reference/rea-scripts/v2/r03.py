for va in (0xF18F40, 0xF0F0C0, 0xF067B0, 0xF12610, 0xF0A580, 0x94F100):
    t = dec(va)
    P(f"==== {va:08X} len={len(t) if t else 0}")
P(dis(0xF10230, 60))
P(dis(0xF06A50, 40))
P(dis(0xF128B0, 40))
