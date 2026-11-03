param()
$ErrorActionPreference='Stop'
& python -B -X utf8 (Join-Path $PSScriptRoot '_integration/r34/rebuild.py') --build
if($LASTEXITCODE -ne 0){throw 'Accepted r34 rebuild refused.'}
