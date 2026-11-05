@echo off
setlocal enabledelayedexpansion
rem ===========================================================================
rem  HiveWE 0.6 - Windows build script
rem
rem  Usage:  build.bat [app|deps|all|clean|help]
rem    app    build HiveWE and deploy runtime (default)
rem    deps   build all third-party dependencies into DEPS_PREFIX
rem    all    run deps, then app
rem    clean  remove the build directory
rem
rem  Toolchain resolution (in order):
rem    1) <repo>\toolchain\  -- self-contained: Qt 5.15.2, prebuilt deps,
rem       MSVC 14.44.35207 toolset, Windows SDK 10.0.26100.0, CMake, Ninja.
rem       Copied out of C: on 2026-11-04, so the repo can still be built after
rem       a clean Windows install WITHOUT Visual Studio installed.
rem    2) installed Visual Studio 2022 (located via vswhere, no vcvars call).
rem
rem  Requirements
rem    local mode : nothing outside this folder (git only for `deps`)
rem    VS mode    : Visual Studio 2022 with "Desktop development with C++"
rem ===========================================================================

set "ROOT=%~dp0"
set "ROOT=%ROOT:~0,-1%"

rem ---------------------- Paths: edit only this block -----------------------
rem  Everything lives inside the repo; toolchain\ is in .gitignore.
set "LOCAL_TT=%ROOT%\toolchain"
set "QT_DIR=%LOCAL_TT%\qt\5.15.2\msvc2019_64"
set "DEPS_PREFIX=%LOCAL_TT%\deps\prefix"
set "ADS_DIR=%LOCAL_TT%\deps\qads"
set "DEPS_SRC=%LOCAL_TT%\deps\src"
set "DEPS_BUILD=%LOCAL_TT%\deps\build"
set "CONFIG=Release"
rem -------------------------------------------------------------------------

set "BUILD_DIR=%ROOT%/build"

set "ACTION=%~1"
if "%ACTION%"=="" set "ACTION=app"

if /i "%ACTION%"=="help"    goto :help
if /i "%ACTION%"=="-h"      goto :help
if /i "%ACTION%"=="--help"  goto :help

rem ======================= Locate toolchain (local first) ===================
set "TOOLCHAIN="
set "MSVCVER="
set "VCTOOLS="
set "SDKROOT="
set "SDKVER="
set "CMAKE="
set "NINJA="

rem --- 1) repo-local toolchain ---------------------------------------------
for /f "delims=" %%i in ('dir /b /ad /o-n "%LOCAL_TT%\msvc" 2^>nul') do if not defined MSVCVER set "MSVCVER=%%i"
if not defined MSVCVER goto :find_vs
if not exist "%LOCAL_TT%\msvc\%MSVCVER%\bin\Hostx64\x64\cl.exe" goto :find_vs

set "TOOLCHAIN=local"
set "VCTOOLS=%LOCAL_TT%\msvc\%MSVCVER%"
set "SDKROOT=%LOCAL_TT%\winsdk\10"
for /f "delims=" %%i in ('dir /b /ad /o-n "%SDKROOT%\Include\10.0.*" 2^>nul') do if not defined SDKVER set "SDKVER=%%i"
set "CMAKE=%LOCAL_TT%\cmake\bin\cmake.exe"
set "NINJA=%LOCAL_TT%\bin\ninja.exe"
goto :toolchain_ready

rem --- 2) installed Visual Studio 2022 -------------------------------------
:find_vs
set "MSVCVER="
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
	echo [ERROR] No local toolchain at %LOCAL_TT% and vswhere.exe not found.
	echo         Either copy toolchain\ into the repo, or install Visual Studio 2022
	echo         with "Desktop development with C++".
	exit /b 1
)
set "VSPATH="
rem Note: paths below are expanded with !VAR! (delayed) on purpose. The literal
rem "(x86)" in %ProgramFiles(x86)% would otherwise close the FOR statement early.
for /f "usebackq tokens=*" %%i in (`"!VSWHERE!" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSPATH=%%i"
if not defined VSPATH (
	echo [ERROR] No Visual Studio 2022 with the C++ toolset found.
	exit /b 1
)

rem Highest installed MSVC toolset
for /f "delims=" %%i in ('dir /b /ad /o-n "!VSPATH!\VC\Tools\MSVC" 2^>nul') do if not defined MSVCVER set "MSVCVER=%%i"
if not defined MSVCVER (
	echo [ERROR] No MSVC toolset found under !VSPATH!\VC\Tools\MSVC
	exit /b 1
)
set "VCTOOLS=%VSPATH%\VC\Tools\MSVC\%MSVCVER%"

rem Highest installed Windows SDK
set "SDKROOT=%ProgramFiles(x86)%\Windows Kits\10"
for /f "delims=" %%i in ('dir /b /ad /o-n "!SDKROOT!\Include\10.0.*" 2^>nul') do if not defined SDKVER set "SDKVER=%%i"
if not defined SDKVER (
	echo [ERROR] No Windows SDK found under !SDKROOT!\Include
	exit /b 1
)

rem CMake and Ninja: prefer PATH, fall back to the copies shipped with VS
set "CMAKE=cmake"
where cmake >nul 2>&1 || set "CMAKE=%VSPATH%\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
set "NINJA=ninja"
where ninja >nul 2>&1 || set "NINJA=%VSPATH%\Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja\ninja.exe"
set "TOOLCHAIN=visual-studio"

:toolchain_ready
if not defined SDKVER (
	echo [ERROR] No Windows SDK found under %SDKROOT%\Include
	exit /b 1
)
if not exist "%CMAKE%" (
	echo [ERROR] cmake not found: %CMAKE%
	exit /b 1
)
if not exist "%NINJA%" (
	echo [ERROR] ninja.exe not found: %NINJA%
	echo         Install it or use the copy bundled with Visual Studio.
	exit /b 1
)

echo [env] toolchain %TOOLCHAIN%
echo [env] MSVC %MSVCVER%
echo [env] SDK  %SDKVER%
echo [env] Qt   %QT_DIR%

rem cl.exe / link.exe / nmake.exe plus the headers and import libraries
set "PATH=%VCTOOLS%\bin\Hostx64\x64;%SDKROOT%\bin\%SDKVER%\x64;%QT_DIR%\bin;%PATH%"
set "INCLUDE=%VCTOOLS%\include;%SDKROOT%\Include\%SDKVER%\ucrt;%SDKROOT%\Include\%SDKVER%\um;%SDKROOT%\Include\%SDKVER%\shared;%SDKROOT%\Include\%SDKVER%\winrt"
set "LIB=%VCTOOLS%\lib\x64;%SDKROOT%\Lib\%SDKVER%\ucrt\x64;%SDKROOT%\Lib\%SDKVER%\um\x64"

if /i "%ACTION%"=="clean" (
	if exist "%BUILD_DIR%" (
		echo [CLEAN] removing %BUILD_DIR%
		rmdir /s /q "%BUILD_DIR%"
	)
	exit /b 0
)

if /i "%ACTION%"=="deps" goto :deps
if /i "%ACTION%"=="all"  goto :deps
if /i "%ACTION%"=="app"  goto :app

echo [ERROR] Unknown argument: %ACTION%
goto :help


rem ===========================================================================
rem  Build third-party dependencies
rem ===========================================================================
:deps
echo.
echo ===== Building dependencies into %DEPS_PREFIX% =====

if not exist "%DEPS_SRC%" mkdir "%DEPS_SRC%"
if not exist "%DEPS_BUILD%" mkdir "%DEPS_BUILD%"

call :clone https://github.com/madler/zlib                 zlib
call :clone https://github.com/g-truc/glm                  glm
call :clone https://github.com/SpartanJ/SOIL2              soil2
call :clone https://github.com/ladislav-zezula/StormLib    stormlib
call :clone https://github.com/ladislav-zezula/CascLib     casclib
call :clone https://github.com/libjpeg-turbo/libjpeg-turbo jpeg
call :clone https://github.com/bulletphysics/bullet3       bullet3
call :clone https://github.com/githubuser0xFFFF/Qt-Advanced-Docking-System qads 4.3.1

rem Order matters: CascLib's CMake config requires zlib to be present already
call :build zlib     "-DCMAKE_INSTALL_PREFIX=%DEPS_PREFIX%"
call :build glm      "-DGLM_BUILD_TESTS=OFF -DGLM_BUILD_INSTALL=ON -DCMAKE_INSTALL_PREFIX=%DEPS_PREFIX%"
call :build soil2    "-DCMAKE_INSTALL_PREFIX=%DEPS_PREFIX%"
call :build stormlib "-DBUILD_SHARED_LIBS=OFF -DSTORM_USE_BUNDLED_LIBRARIES=ON -DSTORM_UNICODE=ON -DCMAKE_INSTALL_PREFIX=%DEPS_PREFIX%"
call :build casclib  "-DCASC_BUILD_SHARED_LIB=OFF -DCASC_BUILD_STATIC_LIB=ON -DCASC_UNICODE=ON -DCMAKE_INSTALL_PREFIX=%DEPS_PREFIX%"
call :build jpeg     "-DWITH_SIMD=OFF -DENABLE_SHARED=OFF -DENABLE_STATIC=ON -DWITH_TURBOJPEG=ON -DCMAKE_INSTALL_PREFIX=%DEPS_PREFIX%"

rem Bullet: only the three core libraries. BUILD_EXTRAS contains a translation
rem unit that hangs the compiler, so it is disabled and targets are explicit.
echo.
echo ----- bullet3 -----
"%CMAKE%" -S "%DEPS_SRC%/bullet3" -B "%DEPS_BUILD%/bullet3" -G Ninja -DCMAKE_MAKE_PROGRAM="%NINJA%" -DCMAKE_BUILD_TYPE=%CONFIG% ^
 -DBUILD_SHARED_LIBS=OFF -DUSE_MSVC_RUNTIME_LIBRARY_DLL=ON ^
 -DBUILD_BULLET2_DEMOS=OFF -DBUILD_BULLET3=OFF -DBUILD_CPU_DEMOS=OFF -DBUILD_UNIT_TESTS=OFF ^
 -DBUILD_EXTRAS=OFF -DBUILD_OPENGL3_DEMOS=OFF -DUSE_GRAPHICAL_BENCHMARK=OFF -DINSTALL_LIBS=ON ^
 -DCMAKE_INSTALL_PREFIX=%DEPS_PREFIX%
if errorlevel 1 goto :deps_fail
"%CMAKE%" --build "%DEPS_BUILD%/bullet3" --target BulletCollision BulletDynamics LinearMath
if errorlevel 1 goto :deps_fail
"%CMAKE%" --install "%DEPS_BUILD%/bullet3"
if errorlevel 1 goto :deps_fail

rem ----- QScintilla: must be a shared build; DLL goes to Qt\lib and needs a copy in bin -----
echo.
echo ----- QScintilla -----
if not exist "%DEPS_SRC%\QScintilla_src-2.13.4\src\qscintilla.pro" (
	echo [NOTE] Unpack the QScintilla 2.13.4 sources to %DEPS_SRC%\QScintilla_src-2.13.4 first.
	goto :deps_fail
)
pushd "%DEPS_SRC%\QScintilla_src-2.13.4\src"
"%QT_DIR%/bin/qmake.exe" qscintilla.pro
if errorlevel 1 (popd & goto :deps_fail)
nmake /NOLOGO
if errorlevel 1 (popd & goto :deps_fail)
nmake /NOLOGO install
if errorlevel 1 (popd & goto :deps_fail)
popd
copy /y "%QT_DIR%\lib\qscintilla2_qt5.dll" "%QT_DIR%\bin\" >nul 2>&1

rem ----- Qt Advanced Docking System: upstream installs under the name qt5advanceddocking -----
echo.
echo ----- Qt Advanced Docking System -----
"%CMAKE%" -S "%DEPS_SRC%/qads" -B "%DEPS_BUILD%/qads" -G Ninja -DCMAKE_MAKE_PROGRAM="%NINJA%" -DCMAKE_BUILD_TYPE=%CONFIG% ^
 -DCMAKE_PREFIX_PATH=%QT_DIR% -DADS_VERSION=4.3.1 -DBUILD_EXAMPLES=OFF -DBUILD_STATIC=OFF ^
 -DCMAKE_INSTALL_PREFIX=%ADS_DIR%
if errorlevel 1 goto :deps_fail
"%CMAKE%" --build "%DEPS_BUILD%/qads"
if errorlevel 1 goto :deps_fail
"%CMAKE%" --install "%DEPS_BUILD%/qads"
if errorlevel 1 goto :deps_fail

echo.
echo [DONE] dependencies installed to %DEPS_PREFIX% and %ADS_DIR%
if /i "%ACTION%"=="deps" exit /b 0
goto :app


rem ===========================================================================
rem  Build HiveWE
rem ===========================================================================
:app
echo.
echo ===== Configuring HiveWE =====

rem Ninja is single config: wipe a build directory produced by another generator
if exist "%BUILD_DIR%\CMakeCache.txt" (
	findstr /c:"CMAKE_GENERATOR:INTERNAL=Ninja" "%BUILD_DIR%\CMakeCache.txt" >nul 2>&1 || (
		echo [CLEAN] generator changed, removing stale build directory
		rmdir /s /q "%BUILD_DIR%"
	)
)

"%CMAKE%" -S "%ROOT%" -B "%BUILD_DIR%" -G Ninja -DCMAKE_MAKE_PROGRAM="%NINJA%" -DCMAKE_BUILD_TYPE=%CONFIG% ^
 -DCMAKE_INSTALL_PREFIX=%ROOT%/out ^
 -DQt5_DIR=%QT_DIR%/lib/cmake/Qt5 ^
 -DQSCINTILLA_ROOT=%QT_DIR% ^
 -Dqt5advanceddocking_DIR=%ADS_DIR%/lib/cmake/qt5advanceddocking ^
 -DHIVE_DEPS_PREFIX=%DEPS_PREFIX%
if errorlevel 1 goto :app_fail

echo.
echo ===== Building HiveWE (%CONFIG%) =====
"%CMAKE%" --build "%BUILD_DIR%"
if errorlevel 1 goto :app_fail

echo.
echo ===== Deploying runtime =====
rem windeployqt only looks inside Qt\bin for dependencies, so seed the ADS DLL there
if not exist "%QT_DIR%\bin\qt5advanceddocking.dll" (
	copy /y "%ADS_DIR%\bin\qt5advanceddocking.dll" "%QT_DIR%\bin\" >nul 2>&1
)

rem VC runtime: windeployqt does not ship these. Without them the exe fails to
rem start on a machine that has no VC++ redistributable (e.g. right after a clean
rem Windows install). Best effort: take them from the running system.
for %%d in (vcruntime140.dll vcruntime140_1.dll msvcp140.dll) do (
	if not exist "%BUILD_DIR%\%%d" if exist "%SystemRoot%\System32\%%d" copy /y "%SystemRoot%\System32\%%d" "%BUILD_DIR%\" >nul
)

"%QT_DIR%/bin/windeployqt.exe" --release --no-translations --no-opengl-sw ^
 "%BUILD_DIR%/HiveWE.exe" ^
 "%BUILD_DIR%/qscintilla2_qt5.dll" ^
 "%BUILD_DIR%/qt5advanceddocking.dll"
if errorlevel 1 (
	echo [WARN] windeployqt failed; Qt runtime DLLs may be missing.
)

echo.
echo [DONE] %BUILD_DIR%\HiveWE.exe
echo Set the Warcraft III install path in the settings on first run.
echo An OpenGL 4.5 Core capable GPU is required.
exit /b 0

:deps_fail
echo.
echo [FAILED] dependency build failed.
exit /b 1

:app_fail
echo.
echo [FAILED] HiveWE build failed.
exit /b 1

:clone
if exist "%DEPS_SRC%\%~2" (
	echo [SKIP] %~2 already present
	exit /b 0
)
if "%~3"=="" (
	git clone --depth 1 %~1 "%DEPS_SRC%\%~2"
) else (
	git clone --depth 1 --branch %~3 %~1 "%DEPS_SRC%\%~2"
)
if errorlevel 1 (
	echo [ERROR] failed to clone %~1
	exit /b 1
)
exit /b 0

:build
echo.
echo ----- %~1 -----
"%CMAKE%" -S "%DEPS_SRC%/%~1" -B "%DEPS_BUILD%/%~1" -G Ninja -DCMAKE_MAKE_PROGRAM="%NINJA%" -DCMAKE_BUILD_TYPE=%CONFIG% %~2
if errorlevel 1 exit /b 1
"%CMAKE%" --build "%DEPS_BUILD%/%~1"
if errorlevel 1 exit /b 1
"%CMAKE%" --install "%DEPS_BUILD%/%~1"
if errorlevel 1 exit /b 1
exit /b 0

:help
echo.
echo  HiveWE 0.6 build script (Ninja)
echo.
echo    build.bat          build HiveWE (default)
echo    build.bat app      same as above
echo    build.bat deps     build all third-party dependencies
echo    build.bat all      dependencies, then HiveWE
echo    build.bat clean    remove the build directory
echo.
echo  Uses toolchain\ inside the repo when present, else an installed VS 2022.
echo  Qt path, dependency prefix and ADS path are configured at the top of this file.
echo  Output: build\HiveWE.exe
echo.
exit /b 0
