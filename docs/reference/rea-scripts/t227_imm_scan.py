"""IDAPython read-only: list instructions whose immediate operand is in REA_LIST (hex values); report containing function."""
import gc, json, os, traceback
from pathlib import Path
import ida_auto, ida_funcs, ida_ua, idautils, idc, ida_segment

out = Path(os.environ["REA_OUT"])
vals = {int(x, 16) for x in Path(os.environ["REA_LIST"]).read_text().split()}
rep = {"hits": []}
try:
    for obj in gc.get_objects():
        if type(obj).__name__ == "StringBatchAnalyzerV2Plugin":
            obj.term()
    ida_auto.auto_wait()
    seg = ida_segment.get_segm_by_name(".text")
    for fea in idautils.Functions(seg.start_ea, seg.end_ea):
        for ea in idautils.FuncItems(fea):
            insn = ida_ua.insn_t()
            if not ida_ua.decode_insn(insn, ea):
                continue
            for op in insn.ops:
                if op.type == ida_ua.o_void:
                    break
                if op.type == ida_ua.o_imm and (op.value & 0xFFFFFFFF) in vals:
                    rep["hits"].append([hex(ea), hex(fea), hex(op.value & 0xFFFFFFFF), idc.GetDisasm(ea)])
    rep["status"] = "pass"
except Exception:
    rep["status"] = "fail"; rep["exception"] = traceback.format_exc()
(out / "report.json").write_text(json.dumps(rep, indent=1), encoding="utf-8")
idc.qexit(0)
