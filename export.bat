@echo off
setlocal
rem One-click Windows export. Keeps working even if dotnet is not on PATH.
rem Usage: export.bat [output-exe-name]
rem   export.bat                          -> "WorldEditor.exe"
rem   export.bat "WorldEditor.new.exe"      -> export under another name
rem The exe cannot be overwritten while the app is running, so the second form
rem lets you build next to a running instance and swap it in afterwards.
rem The Godot project lives in .\Godot; artifacts are written next to this script.
set "PROJECT=%~dp0Godot\"
set "OUTPUT=%~dp0"
if exist "%ProgramFiles%\dotnet\dotnet.exe" set "PATH=%ProgramFiles%\dotnet;%PATH%"
if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"

set "OUT=%~1"
if "%OUT%"=="" set "OUT=WorldEditor.exe"

set "GODOT=D:\Ghost\GameDev\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
if not exist "%GODOT%" (
  echo [ERROR] Godot not found at: %GODOT%
  echo Edit GODOT= in this file.
  exit /b 1
)

echo Project: %PROJECT%
echo Exporting to "%OUTPUT%%OUT%"
"%GODOT%" --headless --path "%PROJECT%." --export-release "Windows Desktop" "%OUTPUT%%OUT%"
if errorlevel 1 (
  echo [ERROR] Export failed, see the log above.
  echo         If it says the temp file could not be renamed, the target exe is
  echo         running. Close it, or export with another name first.
  exit /b 1
)

echo [OK] Done. Output: "%OUTPUT%%OUT%"
echo      Single-file build: no data folder is produced (embed_build_outputs=true).
endlocal
