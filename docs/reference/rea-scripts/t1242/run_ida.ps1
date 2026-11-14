# T1242 (#1242): idat batch on scratch\i1242\ida\tw16.idb (a copy; read-only queries). Same pattern as t956\run_tw16.ps1.
# Usage (repo root): powershell -NoProfile -File docs/reference/rea-scripts/t1242/run_ida.ps1 -Script ida_surfattr.py -Out out1 [-Extra 0xEF13F0,...]
param([string]$Script = 'ida_surfattr.py', [string]$Out = 'out', [string]$Extra = '', [int]$TimeoutSec = 900, [string]$Drive = 'Q:')
$Root = (Resolve-Path 'scratch\i1242').Path
Copy-Item (Join-Path $PSScriptRoot $Script) "$Root\ida\$Script" -Force
if ((subst) -match "^$Drive") { "DRIVE_BUSY $Drive"; exit 2 }
subst $Drive $Root | Out-Null
try {
    $e = "$Drive\ida\env"
    $env:IDAUSR = "$e\user"; $env:APPDATA = "$e\appdata"; $env:LOCALAPPDATA = "$e\local"; $env:TEMP = "$e\temp"; $env:TMP = "$e\temp"
    $env:PYTHONHOME = 'D:\IDA_Pro_7.7\python38'; $env:PATH = 'D:\IDA_Pro_7.7\python38;' + $env:PATH
    $env:REA_OUT = "$Drive\ida\$Out"; $env:REA_EXTRA = $Extra
    New-Item -ItemType Directory -Force "$Drive\ida\$Out" | Out-Null
    $p = Start-Process -FilePath 'D:\IDA_Pro_7.7\idat.exe' -PassThru -WindowStyle Hidden -WorkingDirectory "$Drive\ida" `
        -ArgumentList '-A', "-L$Drive\ida\$Out\ida.log", "-S$Drive\ida\$Script", "$Drive\ida\tw16.idb"
    if (-not $p.WaitForExit($TimeoutSec * 1000)) { $p.Kill(); 'TIMEOUT'; exit 124 }
    "exit=$($p.ExitCode)"
} finally { subst $Drive /D | Out-Null }
