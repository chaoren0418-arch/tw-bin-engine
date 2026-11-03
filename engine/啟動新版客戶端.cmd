@echo off
setlocal
if not exist "%~dp0launcher\P24.Launcher.exe" (
  echo Missing launcher\P24.Launcher.exe. Please restore the complete launcher hotfix.
  if /i not "%~1"=="--check-only" pause
  exit /b 2
)
"%~dp0launcher\P24.Launcher.exe" %*
set "launchExit=%errorlevel%"
if not "%launchExit%"=="0" (
  echo Launcher failed. Windows x64 and .NET 10 Desktop Runtime are required. PowerShell is not required.
  if /i not "%~1"=="--check-only" pause
)
exit /b %launchExit%
