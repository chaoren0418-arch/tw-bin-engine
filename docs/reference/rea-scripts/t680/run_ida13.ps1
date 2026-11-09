param([string]$Script = "ida_dec.py", [string]$List, [string]$Out, [int]$TimeoutSec = 540, [string]$Drive = 'R:')
# Runs idat against the scratch IDB copy through an ASCII subst drive; env stays inside scratch/rea.
# -Drive lets parallel tasks avoid clobbering each other's subst letter (#431).
$real = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$drive = $Drive
subst $drive $real
try {
    $e = "$drive\ida13\env"
    $env:IDAUSR = "$e\user"; $env:APPDATA = "$e\appdata"; $env:LOCALAPPDATA = "$e\local"
    $env:TEMP = "$e\temp"; $env:TMP = "$e\temp"
    $env:PYTHONHOME = 'D:\IDA_Pro_7.7\python38'
    $env:PATH = 'D:\IDA_Pro_7.7\python38;' + $env:PATH
    $env:REA_OUT = "$drive\ida13\$Out"; $env:REA_LIST = "$drive\$List"
    New-Item -ItemType Directory -Force "$drive\ida13\$Out" | Out-Null
    $p = Start-Process -FilePath 'D:\IDA_Pro_7.7\idat.exe' -PassThru -WindowStyle Hidden -WorkingDirectory "$drive\ida" `
        -ArgumentList '-A', "-L$drive\ida13\$Out\ida.log", "-S$drive\scripts\$Script", "$drive\ida13\tw13.idb"
    if (-not $p.WaitForExit($TimeoutSec * 1000)) { $p.Kill(); "TIMEOUT"; exit 124 }
    "exit=$($p.ExitCode)"
    Get-Content "$drive\ida13\$Out\report.json" -Encoding UTF8 -ErrorAction SilentlyContinue | Select-Object -First 8
} finally {
    subst $drive /D
}

