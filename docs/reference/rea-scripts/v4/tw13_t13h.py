
import ida_funcs
if not ida_funcs.get_func(0x50CA20):
    P("add_func", ida_funcs.add_func(0x50CA20))
t = dec(0x50CA20); P("DEC", (t or "").count("\n"))
P(dis(0x50CA20, 120))
