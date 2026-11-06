@echo off
setlocal EnableExtensions
rem ---------------------------------------------------------------------------
rem One-click Windows export.
rem
rem The game ships as two files:
rem   WorldEditor.exe          code and everything outside assets/converted
rem   converted.v<N>.pck       the whole Godot/assets/converted tree
rem N is AssetStamp.Version. The exe only mounts the pack with its own version
rem number, see Godot/src/Game/Assets/ExternalAssets.cs.
rem
rem Usage:
rem   export.bat              ensure converted.v<N>.pck, then build WorldEditor.exe
rem   export.bat -converted   only ensure converted.v<N>.pck
rem   export.bat -name X.exe  exe output name (default WorldEditor.exe)
rem
rem The pack is always a full build. It is skipped when converted.v<N>.pck for
rem the current version already exists; delete that file to repack, or bump
rem AssetStamp.Version so the name changes.
rem
rem Presets in Godot/export_presets.cfg:
rem   "win"              everything except assets/converted, single file exe
rem   "converted"        assets/converted only, exported as a standalone .pck
rem ---------------------------------------------------------------------------

set "PROJECT=%~dp0Godot\"
set "OUTPUT=%~dp0"
set "GODOT=D:\Ghost\GameDev\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"

if exist "%ProgramFiles%\dotnet\dotnet.exe" set "PATH=%ProgramFiles%\dotnet;%PATH%"
if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"

set "MODE=both"
set "OUT=WorldEditor.exe"

:parse
set "ARG=%~1"
if "%ARG%"=="" goto :parsed
if /i "%ARG%"=="-converted" (
  set "MODE=converted"
) else if /i "%ARG%"=="-name" (
  if not "%~2"=="" set "OUT=%~2"
  shift
) else if "%ARG:~0,1%"=="-" (
  echo [WARN] Unknown option ignored: %ARG%
) else (
  rem Legacy form: export.bat WorldEditor.new.exe
  set "OUT=%ARG%"
)
shift
goto :parse
:parsed

if not exist "%GODOT%" (
  echo [ERROR] Godot not found at: %GODOT%
  echo         Edit GODOT= in this file.
  exit /b 1
)

echo Project : %PROJECT%
echo Godot   : %GODOT%

rem The version comes from the C# side, so build first: the version lookup and the
rem export itself both run inside the game binary.
echo.
echo [0/2] dotnet build
dotnet build "%PROJECT%WorldEdit.csproj" -v:minimal --nologo
if errorlevel 1 goto :failed

set "VER_FILE=%TEMP%\WorldEditor-asset-version.txt"
if exist "%VER_FILE%" del /q "%VER_FILE%"
"%GODOT%" --headless --path "%PROJECT%." -- "--asset-version=%VER_FILE%" >nul 2>&1
set "ASSET_VER="
if exist "%VER_FILE%" set /p ASSET_VER=<"%VER_FILE%"
if not defined ASSET_VER (
  echo [ERROR] Cannot read the extract version: --asset-version wrote nothing to %VER_FILE%
  echo         Make sure dotnet build succeeded, then retry.
  exit /b 1
)

set "ASSET_PACK=%OUTPUT%converted.v%ASSET_VER%.pck"
echo Version : v%ASSET_VER%  -^>  converted.v%ASSET_VER%.pck
echo Mode    : %MODE%

rem ---------------------------------------------------------------- assets pack
rem Always a full build. Skipped when the file for this version already exists;
rem delete it (or bump AssetStamp.Version) to force a repack.
if exist "%ASSET_PACK%" (
  echo.
  echo [1/2] skip: converted.v%ASSET_VER%.pck already exists
  goto :pack_ready
)

echo.
echo [1/2] packing converted.v%ASSET_VER%.pck
"%GODOT%" --headless --path "%PROJECT%." --export-pack "converted" "%ASSET_PACK%"
if errorlevel 1 goto :failed

:pack_ready
if not exist "%ASSET_PACK%" goto :failed_missing
echo [OK] Assets: "%ASSET_PACK%"
if /i "%MODE%"=="converted" goto :done

rem ---------------------------------------------------------------- exe
echo.
echo [2/2] building %OUT%
"%GODOT%" --headless --path "%PROJECT%." --export-release "win" "%OUTPUT%%OUT%"
if errorlevel 1 goto :failed
echo.
echo [OK] Exe: "%OUTPUT%%OUT%"
echo      Assets: "%ASSET_PACK%"
goto :done

:failed_missing
echo.
echo [ERROR] Pack was not produced: %ASSET_PACK%
exit /b 1

:failed
echo.
echo [ERROR] Export failed, see the log above.
echo         If it says the temp file could not be renamed, the target file is
echo         running. Close it, or export with another name: export.bat -name X.exe
exit /b 1

:done
endlocal
exit /b 0
