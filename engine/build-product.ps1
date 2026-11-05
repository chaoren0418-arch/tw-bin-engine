param([string]$ConfigFile)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot '_integration/dev/product-build.ps1') -ConfigFile $ConfigFile
