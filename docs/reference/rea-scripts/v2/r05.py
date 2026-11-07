for va in (0xF19680, 0xF18AB0, 0xF19FD0, 0x746980):
    t = dec(va)
    P(f"==== {va:08X} len={len(t) if t else 0}")
