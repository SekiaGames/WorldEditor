@echo off
setlocal
rem ---------------------------------------------------------------------------
rem Extracts War3 assets (glb + png + .wc3.json) from the game install folder.
rem Output tree is Godot\assets\converted, the same one the runtime reads.
rem macOS counterpart: extract.command
rem
rem Usage: extract.bat             (also import into Godot afterwards)
rem        extract.bat -noimport   (skip the slow import pass)
rem
rem Steps:
rem   1. dotnet build                 the conversions run inside the game binary
rem   2. mdx -> glb + .wc3.json       read straight out of the MPQs
rem   3. MPQs -> temp dir tree        (needed by the passes below, deleted after)
rem   4. temp dir images -> png
rem   5. temp dir wav / mp3 / ttf     copied as is, no decoding
rem   6. import pass                  rebuilds *.import + .godot/imported, 20-40 min
rem ---------------------------------------------------------------------------

rem ==== EDIT HERE ===========================================================
set "WAR3_DIR=D:\War3_1.27a"
set "GODOT=D:\Ghost\GameDev\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
rem =========================================================================

set "REIMPORT=1"
if /i "%~1"=="-noimport" set "REIMPORT=0"

set "ROOT=%~dp0"
set "PROJECT=%ROOT%Godot"
set "OUT=%PROJECT%\assets\converted"
set "WORK=%TEMP%\WorldEditor-war3"

rem Strip a trailing separator so the | split in the CLI stays clean.
if "%WAR3_DIR:~-1%"=="\" set "WAR3_DIR=%WAR3_DIR:~0,-1%"

if not exist "%WAR3_DIR%\WAR3.MPQ" (
  echo [ERROR] No WAR3.MPQ under: %WAR3_DIR%
  echo         Edit WAR3_DIR= at the top of this file.
  exit /b 1
)
if not exist "%GODOT%" (
  echo [ERROR] Godot not found at: %GODOT%
  echo         Edit GODOT= at the top of this file.
  exit /b 1
)

echo War3   : %WAR3_DIR%
echo Godot  : %GODOT%
echo Output : %OUT%
echo.

rem Godot runs the conversions, so build the assembly first. Same PATH fallback
rem as export.bat: the Godot editor needs dotnet on PATH and often does not have it.
if exist "%ProgramFiles%\dotnet\dotnet.exe" set "PATH=%ProgramFiles%\dotnet;%PATH%"
if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"

echo [1/6] dotnet build
dotnet build "%PROJECT%\WorldEdit.csproj" -v:minimal --nologo
if errorlevel 1 goto :failed

echo.
echo [2/6] mdx to glb + .wc3.json
"%GODOT%" --headless --path "%PROJECT%" -- "--game-dir=%WAR3_DIR%" "--mdx-export=%OUT%"
if errorlevel 1 goto :failed

echo.
echo [3/6] unpacking MPQs to %WORK%
if exist "%WORK%" rmdir /s /q "%WORK%"
"%GODOT%" --headless --path "%PROJECT%" -- "--war3-extract=%WAR3_DIR%|%WORK%"
if errorlevel 1 goto :failed

echo.
echo [4/6] images to png
"%GODOT%" --headless --path "%PROJECT%" -- "--image-convert=%WORK%|%OUT%"
if errorlevel 1 goto :failed

echo.
echo [5/6] copying wav / mp3 / ttf
rem robocopy reports 1-7 as success (1 = files were copied), only >=8 is an error
robocopy "%WORK%" "%OUT%" *.wav *.mp3 *.ttf *.TTF /S /XO /R:1 /NFL /NDL /NJH /NJS /NP
if errorlevel 8 goto :failed

echo.
echo [6/6] removing %WORK%
if exist "%WORK%" rmdir /s /q "%WORK%"

if "%REIMPORT%"=="0" goto :done

echo.
echo Importing into Godot, this takes a while...
"%GODOT%" --headless --path "%PROJECT%" --editor --quit
if errorlevel 1 (
  echo First pass crashed - the known message-queue overflow near 88 percent.
  echo Running it once more, the import is incremental.
  "%GODOT%" --headless --path "%PROJECT%" --editor --quit
  if errorlevel 1 goto :failed
)

:done
echo.
echo [OK] Assets extracted to: %OUT%
endlocal
exit /b 0

:failed
echo.
echo [ERROR] Step failed, see the log above.
echo         Temp tree kept at: %WORK%
endlocal
exit /b 1
