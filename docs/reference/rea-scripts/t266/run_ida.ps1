param([string]$Script = "ida_dec.py", [string]$List, [string]$Out, [int]$TimeoutSec = 540, [string]$Drive = 'R:')
# Runs idat against the scratch IDB copy through an ASCII subst drive; env stays inside scratch/rea.
# -Drive lets parallel tasks avoid clobbering each other's subst letter (#431).
# T266 (#627): copy of ../run_ida.ps1 that refuses a drive letter already in use (exit 3) BEFORE mapping, so the
# finally block never removes another task's subst. Copy next to ida_t266.py in <scratch>\rea\scripts\.
$real = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$drive = $Drive
if (Test-Path "$drive\") { 'DRIVE BUSY'; exit 3 }
subst $drive $real
try {
    $e = "$drive\ida\env"
    $env:IDAUSR = "$e\user"; $env:APPDATA = "$e\appdata"; $env:LOCALAPPDATA = "$e\local"
    $env:TEMP = "$e\temp"; $env:TMP = "$e\temp"
    $env:PYTHONHOME = 'D:\IDA_Pro_7.7\python38'
    $env:PATH = 'D:\IDA_Pro_7.7\python38;' + $env:PATH
    $env:REA_OUT = "$drive\ida\$Out"; $env:REA_LIST = "$drive\$List"
    New-Item -ItemType Directory -Force "$drive\ida\$Out" | Out-Null
    $p = Start-Process -FilePath 'D:\IDA_Pro_7.7\idat.exe' -PassThru -WindowStyle Hidden -WorkingDirectory "$drive\ida" `
        -ArgumentList '-A', "-L$drive\ida\$Out\ida.log", "-S$drive\scripts\$Script", "$drive\ida\tw16.idb"
    if (-not $p.WaitForExit($TimeoutSec * 1000)) { $p.Kill(); "TIMEOUT"; exit 124 }
    "exit=$($p.ExitCode)"
    Get-Content "$drive\ida\$Out\report.json" -Encoding UTF8 -ErrorAction SilentlyContinue | Select-Object -First 8
} finally {
    subst $drive /D
}


