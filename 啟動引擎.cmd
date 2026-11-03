@echo off
setlocal
if not exist "%~dp0releases\r33\launcher\P24.Launcher.exe" exit /b 2
"%~dp0releases\r33\launcher\P24.Launcher.exe" %*
exit /b %errorlevel%
