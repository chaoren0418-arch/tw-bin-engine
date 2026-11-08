"""IDAPython T225 (#532): every code xref to the packet writer sub_9FBC00 with the last pushes
(opcode = 2nd-to-last push, fmt = last push) resolved; non-immediate opcodes get the defining
instruction of the register. Read-only, writes sends.json under REA_OUT."""
import gc, json, os, traceback
from pathlib import Path
import ida_funcs, idautils, idc, ida_auto, ida_bytes

out = Path(os.environ["REA_OUT"])
rep = {"status": "?", "sites": []}
T = 0x9FBC00


def s(ea):
    b = ida_bytes.get_strlit_contents(ea, -1, 0)
    return b.decode("latin1") if b else None


try:
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
            obj.term()
    ida_auto.auto_wait()
    for x in idautils.XrefsTo(T):
        if not x.iscode:
            continue
        ea = x.frm
        f = ida_funcs.get_func(ea)
        pushes, cur, back = [], ea, []
        for _ in range(14):
            cur = idc.prev_head(cur, (f.start_ea if f else ea - 0x80))
            if cur == idc.BADADDR:
                break
            back.append(f"{cur:08X} {idc.generate_disasm_line(cur, 0)}")
            if idc.print_insn_mnem(cur) == "push":
                pushes.append(cur)
            if len(pushes) >= 2:
                break
        r = {"site": hex(ea), "func": hex(f.start_ea) if f else None}
        if len(pushes) >= 2:
            fm, op = pushes[0], pushes[1]
            if idc.get_operand_type(fm, 0) == idc.o_imm:
                r["fmt"] = s(idc.get_operand_value(fm, 0))
            if idc.get_operand_type(op, 0) == idc.o_imm:
                r["op"] = idc.get_operand_value(op, 0)
            else:
                r["op_expr"] = idc.generate_disasm_line(op, 0)
                r["back"] = back
        rep["sites"].append(r)
    rep["status"] = "pass"
except Exception:
    rep["status"] = "fail"
    rep["exception"] = traceback.format_exc()
(out / "sends.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
(out / "report.json").write_text(json.dumps({"status": rep["status"], "n": len(rep["sites"])}), encoding="utf-8")
idc.qexit(0 if rep["status"] == "pass" else 4)
