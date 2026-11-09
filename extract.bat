@echo off
setlocal

set "ROOT=%~dp0"
set "WAR3_DIR=%ROOT%test\War3_1.27a"
set "GODOT=D:\Ghost\GameDev\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"

set "PROJECT=%ROOT%Extract"
set "OUT=%PROJECT%\extract"
set "WORK=%ROOT%test\war3-unpack"
set "PACK=%ROOT%extract.pck"
set "UNPACK_MARKER=.unpacked"

if "%WAR3_DIR:~-1%"=="\" set "WAR3_DIR=%WAR3_DIR:~0,-1%"

if not exist "%GODOT%" (
  echo [ERROR] Godot not found at %GODOT%
  pause
  exit /b 1
)

if exist "%ProgramFiles%\dotnet\dotnet.exe" set "PATH=%ProgramFiles%\dotnet;%PATH%"
if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"

if not exist "%WAR3_DIR%\war3.mpq" (
  echo [ERROR] No war3.mpq under %WAR3_DIR%
  pause
  exit /b 1
)

echo [1/8] dotnet build
dotnet build "%PROJECT%\Extract.csproj" -v:minimal --nologo
if errorlevel 1 goto :failed

echo [2/8] tables and texts
"%GODOT%" --headless --quiet --path "%PROJECT%" -- "--game-dir=%WAR3_DIR%" --table-export
if errorlevel 1 goto :failed
"%GODOT%" --headless --quiet --path "%PROJECT%" -- "--game-dir=%WAR3_DIR%" --text-export
if errorlevel 1 goto :failed

echo [3/8] unpack mpq
if not exist "%WORK%" mkdir "%WORK%"
if not exist "%OUT%" mkdir "%OUT%"
if exist "%WORK%\%UNPACK_MARKER%" (
  echo       marker found, skip unpack
  goto :unpack_done
)
"%GODOT%" --headless --quiet --path "%PROJECT%" -- "--war3-extract=%WAR3_DIR%|%WORK%"
if errorlevel 1 goto :failed
type nul > "%WORK%\%UNPACK_MARKER%"
:unpack_done

echo [4/8] images to png
"%GODOT%" --headless --quiet --path "%PROJECT%" -- "--image-convert=%WORK%|%OUT%"
if errorlevel 1 goto :failed

echo [5/8] mdx to glb
"%GODOT%" --headless --quiet --path "%PROJECT%" -- "--game-dir=%WAR3_DIR%" "--mdx-export=%OUT%"
if errorlevel 1 goto :failed

echo [6/8] copy audio
"%GODOT%" --headless --quiet --path "%PROJECT%" -- "--media-copy=%WORK%|%OUT%"
if errorlevel 1 goto :failed

echo [7/8] importing into godot, this takes 20 to 40 minutes
"%GODOT%" --headless --quiet --path "%PROJECT%" --import
if errorlevel 1 "%GODOT%" --headless --quiet --path "%PROJECT%" --import
if errorlevel 1 goto :failed

echo [8/8] pack extract.pck
"%GODOT%" --headless --quiet --path "%PROJECT%" --export-pack "extract.pck" "%PACK%"
if errorlevel 1 goto :failed

echo.
echo [OK] resources %OUT%
echo      pack      %PACK%
echo      copy the pack next to Editor.exe
echo.
pause
exit /b 0

:failed
echo.
echo [ERROR] See the log above.
echo.
pause
exit /b 1
