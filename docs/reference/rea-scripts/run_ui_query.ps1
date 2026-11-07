param([string]$Query, [string]$Out, [string]$Drive = 'K:', [int]$TimeoutSec = 900)
# Runs idat (read-only queries) on scratch/rea/ida/tw16.idb through an ASCII subst drive; env stays in scratch/rea.
$real = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (Test-Path "$Drive\") { "drive $Drive busy"; exit 3 }
subst $Drive $real
if (-not (Test-Path "$Drive\scripts")) { "subst failed"; exit 3 }
try {
    $e = "$Drive\ida\env"
    $env:IDAUSR = "$e\user"; $env:APPDATA = "$e\appdata"; $env:LOCALAPPDATA = "$e\local"
    $env:TEMP = "$e\temp"; $env:TMP = "$e\temp"
    $env:PYTHONHOME = 'D:\IDA_Pro_7.7\python38'
    $env:PATH = 'D:\IDA_Pro_7.7\python38;' + $env:PATH
    $env:REA_OUT = "$Drive\ida\$Out"; $env:REA_LIST = "$Drive\$Query"
    New-Item -ItemType Directory -Force "$Drive\ida\$Out" | Out-Null
    $p = Start-Process -FilePath 'D:\IDA_Pro_7.7\idat.exe' -PassThru -WindowStyle Hidden -WorkingDirectory "$Drive\ida" `
        -ArgumentList '-A', "-L$Drive\ida\$Out\ida.log", "-S$Drive\scripts\ida_query.py", "$Drive\ida\tw16.idb"
    if (-not $p.WaitForExit($TimeoutSec * 1000)) { $p.Kill(); "TIMEOUT"; exit 124 }
    "exit=$($p.ExitCode)"
} finally {
    subst $Drive /D
}
