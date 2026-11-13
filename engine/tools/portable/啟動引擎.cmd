@echo off
setlocal
rem Portable entry (T472). Edit the next line to point at YOUR original client folder (keep the quotes).
set "TW_CLIENT_ROOT=D:\Games\MyOriginalClient"
set "TW_ENGINE_ROOT=%~dp0engine"
if not exist "%~dp0engine\launcher\P24.Launcher.exe" exit /b 2
"%~dp0engine\launcher\P24.Launcher.exe" %*
exit /b %errorlevel%
