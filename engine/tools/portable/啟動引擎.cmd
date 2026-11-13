@echo off
setlocal
rem Read the rest of this file as UTF-8, so a client path with Chinese characters saved by Notepad (UTF-8) works.
chcp 65001 >nul
rem tw-bin-engine portable entry. See README.md section 4.2.
rem Edit ONLY the next "set" line: point it at YOUR original client folder (the one with Text.pak and map), keep the quotes.
set "TW_CLIENT_ROOT=D:\Games\MyOriginalClient"
rem ---- Do not edit below this line. ----
rem TW_ENGINE_ROOT is derived from where this file is (the launcher also uses its own location).
set "TW_ENGINE_ROOT=%~dp0engine"
set "TW_EXIT=2"
if not exist "%TW_ENGINE_ROOT%\launcher\P24.Launcher.exe" goto :missing
"%TW_ENGINE_ROOT%\launcher\P24.Launcher.exe" %*
set "TW_EXIT=%errorlevel%"
goto :done
:missing
echo [tw-bin-engine] engine\launcher\P24.Launcher.exe not found next to this file. Extract the whole zip and keep this file in the extracted folder.
:done
rem Keep the window open on failure when started without arguments (double-click), so the message can be read.
if not "%TW_EXIT%"=="0" if "%~1"=="" pause
exit /b %TW_EXIT%
