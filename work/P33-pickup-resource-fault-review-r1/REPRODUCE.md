# P33 reproduction

Work root: `D:\lineage開發\TW1608262200研究\work\P33-pickup-resource-fault-review-r1`

Completed commands (original labels are immutable; do not rerun them):

```powershell
python -B .\prepare.py
python -B .\run.py build build-01
python -B .\run.py runtime runtime-01 build-01
python -B .\finish.py
```

Read-only verification after seal:

```powershell
python -B .\verify.py
```

If supervisor separately authorizes a repeat, use fresh labels under this authorized work root:

```powershell
python -B .\run.py build build-review02
python -B .\run.py runtime runtime-review02 build-review02
```

Each runner creates a fresh directory and refuses an existing label. Exact subprocess argv/cwd/child environment/exit/raw stdout+stderr and source/oracle snapshots are in each `runs/<label>/command.json`. `prepare.py` and `finish.py` are one-shot; do not overwrite originals. The original `verification.json` covers runtime-01 only; a later repeat needs its own separately named audit.

The helper directly references the sealed P30 DLL, never a product ProjectReference. Only installed SDK/framework packs are used, with empty NuGet package/audit sources. All private cache/temp/CLI-home directories remain in this work root. HOME, USERPROFILE, CODEX_HOME, machine/user environment are unmodified. No dev-certs command is used.
