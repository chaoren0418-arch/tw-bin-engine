param([string]$ConfigFile)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot '_integration/r35/product-build.ps1') -ConfigFile $ConfigFile
