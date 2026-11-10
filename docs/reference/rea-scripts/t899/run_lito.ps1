# T899（#899）：使用時把本檔放到 scratch\t899\（tw16）或 scratch\t899\lito\（lito），該目錄要有 ida\<idb>、ida\env\、scripts\<IDA 腳本>、q\<清單>。只做唯讀查詢。
param([string]$List, [string]$Out, [int]$TimeoutSec = 900, [string]$Drive = 'V:')
# T899: idat batch on scratch\t899\ida\lito.exe.idb (read-only queries; IDB copy only).
$Root = $PSScriptRoot
if ((subst) -match "^$Drive") { "DRIVE_BUSY $Drive"; exit 2 }
subst $Drive $Root | Out-Null
try {
    $e = "$Drive\ida\env"
    $env:IDAUSR = "$e\user"; $env:APPDATA = "$e\appdata"; $env:LOCALAPPDATA = "$e\local"
    $env:TEMP = "$e\temp"; $env:TMP = "$e\temp"
    $env:PYTHONHOME = 'D:\IDA_Pro_7.7\python38'
    $env:PATH = 'D:\IDA_Pro_7.7\python38;' + $env:PATH
    $env:REA_OUT = "$Drive\ida\$Out"; $env:REA_LIST = "$Drive\q\$List"
    New-Item -ItemType Directory -Force "$Drive\ida\$Out" | Out-Null
    $p = Start-Process -FilePath 'D:\IDA_Pro_7.7\idat.exe' -PassThru -WindowStyle Hidden -WorkingDirectory "$Drive\ida" `
        -ArgumentList '-A', "-L$Drive\ida\$Out\ida.log", "-S$Drive\scripts\ida_t896.py", "$Drive\ida\lito.exe.idb"
    if (-not $p.WaitForExit($TimeoutSec * 1000)) { $p.Kill(); "TIMEOUT"; exit 124 }
    "exit=$($p.ExitCode)"
} finally {
    subst $Drive /D | Out-Null
}
