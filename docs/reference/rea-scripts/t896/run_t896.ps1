param([string]$Root, [string]$List, [string]$Out, [int]$TimeoutSec = 1500, [string]$Drive = 'Q:')
# T896: idat batch on scratch\t896\ida\lito.exe (first run builds lito.exe.idb; later runs reuse it).
# Root = scratch\t896 (must contain ida\, scripts\ida_t896.py, q\). subst gives an ASCII path; released in finally.
if ((subst) -match "^$Drive") { "DRIVE_BUSY $Drive"; exit 2 }
subst $Drive (Resolve-Path $Root).Path
try {
    $e = "$Drive\ida\env"
    $env:IDAUSR = "$e\user"; $env:APPDATA = "$e\appdata"; $env:LOCALAPPDATA = "$e\local"
    $env:TEMP = "$e\temp"; $env:TMP = "$e\temp"
    $env:PYTHONHOME = 'D:\IDA_Pro_7.7\python38'
    $env:PATH = 'D:\IDA_Pro_7.7\python38;' + $env:PATH
    $env:REA_OUT = "$Drive\ida\$Out"; $env:REA_LIST = "$Drive\q\$List"
    New-Item -ItemType Directory -Force "$Drive\ida\$Out" | Out-Null
    $target = if (Test-Path "$Drive\ida\lito.exe.idb") { "$Drive\ida\lito.exe.idb" } else { "$Drive\ida\lito.exe" }
    $p = Start-Process -FilePath 'D:\IDA_Pro_7.7\idat.exe' -PassThru -WindowStyle Hidden -WorkingDirectory "$Drive\ida" `
        -ArgumentList '-A', "-L$Drive\ida\$Out\ida.log", "-S$Drive\scripts\ida_t896.py", $target
    if (-not $p.WaitForExit($TimeoutSec * 1000)) { $p.Kill(); "TIMEOUT"; exit 124 }
    "exit=$($p.ExitCode) target=$target"
} finally {
    subst $Drive /D
}

