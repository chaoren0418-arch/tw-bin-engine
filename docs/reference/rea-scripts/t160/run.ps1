param([Parameter(Mandatory)][string]$Script, [Parameter(Mandatory)][string]$Out, [string]$Arg = '',
      [string]$Root = '', [int]$TimeoutSec = 900)
# T160 (#428): run one IDAPython script from this folder against the scratch IDB copy.
# Prepare once: copy P01 ida/03-ascii-isolated/TW1608262200.idb to <Root>\ida\tw16.idb (SHA 3a0bf206...7e82f).
# Root defaults to <repo>\scratch\rea. Scripts are copied to <Root>\scripts\t160; output goes to <Root>\out\<Out>.
# The script argument is passed in env REA_ARG (see queries.txt). The original IDB is never opened.
if (-not $Root) { $Root = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path 'scratch\rea' }
New-Item -ItemType Directory -Force "$Root\scripts\t160", "$Root\out", "$Root\ida\env\user", "$Root\ida\env\appdata",
    "$Root\ida\env\local", "$Root\ida\env\temp" | Out-Null
Copy-Item "$PSScriptRoot\*.py" "$Root\scripts\t160\" -Force
$drive = 'J:'
if (Test-Path "$drive\") { 'DRIVE BUSY (another subst uses J:)'; exit 3 }
subst $drive $Root
try {
    $e = "$drive\ida\env"
    $env:IDAUSR = "$e\user"; $env:APPDATA = "$e\appdata"; $env:LOCALAPPDATA = "$e\local"
    $env:TEMP = "$e\temp"; $env:TMP = "$e\temp"
    $env:PYTHONHOME = 'D:\IDA_Pro_7.7\python38'
    $env:PATH = 'D:\IDA_Pro_7.7\python38;' + $env:PATH
    $env:REA_OUT = "$drive\out\$Out"; $env:REA_ARG = $Arg
    New-Item -ItemType Directory -Force "$drive\out\$Out" | Out-Null
    $p = Start-Process -FilePath 'D:\IDA_Pro_7.7\idat.exe' -PassThru -WindowStyle Hidden -WorkingDirectory "$drive\ida" `
        -ArgumentList '-A', "-L$drive\out\$Out\ida.log", "-S$drive\scripts\t160\$Script", "$drive\ida\tw16.idb"
    if (-not $p.WaitForExit($TimeoutSec * 1000)) { $p.Kill(); 'TIMEOUT'; exit 124 }
    "exit=$($p.ExitCode)"
} finally { subst $drive /D }
