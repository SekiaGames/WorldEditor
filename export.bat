@echo off
setlocal EnableExtensions

set "ROOT=%~dp0"
set "PROJECT=%ROOT%Editor\"
set "GODOT=D:\Ghost\GameDev\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
set "EXE=Editor.exe"
set "PACK=extract.pck"

if not exist "%GODOT%" (
  echo [ERROR] Godot not found at %GODOT%
  pause
  exit /b 1
)

if exist "%ProgramFiles%\dotnet\dotnet.exe" set "PATH=%ProgramFiles%\dotnet;%PATH%"
if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"

echo [1/2] dotnet build
dotnet build "%PROJECT%WorldEdit.csproj" -v:minimal --nologo
if errorlevel 1 goto :failed

echo [2/2] %EXE%
"%GODOT%" --headless --quiet --path "%PROJECT%." --export-release "Editor" "%ROOT%%EXE%"
if errorlevel 1 goto :failed
if not exist "%ROOT%%EXE%" goto :failed

echo.
echo [OK] %ROOT%%EXE%
if exist "%ROOT%%PACK%" (
  echo      art assets: %ROOT%%PACK%
) else (
  echo      [!] %PACK% missing - run extract.bat first
  echo          without it the editor runs but every model/texture is empty
)
echo.
pause
exit /b 0

:failed
echo.
echo [ERROR] See the log above.
echo.
pause
exit /b 1
