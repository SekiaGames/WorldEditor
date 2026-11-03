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
rem  Requirements:
rem    - Visual Studio 2022 with "Desktop development with C++"
rem    - Qt 5.15.2 msvc2019_64
rem    - git (used by deps mode to clone sources)
rem ===========================================================================

rem ---------------------- Paths: edit only this block -----------------------
set "QT_DIR=C:/Qt/5.15.2/msvc2019_64"
set "DEPS_PREFIX=C:/deps/prefix"
set "ADS_DIR=C:/deps/qads"
set "DEPS_SRC=C:/deps/src"
set "DEPS_BUILD=C:/deps/build"
set "CONFIG=Release"
set "GEN=Visual Studio 17 2022"
rem -------------------------------------------------------------------------

set "ROOT=%~dp0"
set "ROOT=%ROOT:~0,-1%"
set "BUILD_DIR=%ROOT%/build"

set "ACTION=%~1"
if "%ACTION%"=="" set "ACTION=app"

if /i "%ACTION%"=="help"    goto :help
if /i "%ACTION%"=="-h"      goto :help
if /i "%ACTION%"=="--help"  goto :help

rem ----------------------------- Locate VS 2022 -----------------------------
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
	echo [ERROR] vswhere.exe not found. Visual Studio 2022 is probably not installed.
	exit /b 1
)
set "VSPATH="
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSPATH=%%i"
if not defined VSPATH (
	echo [ERROR] No Visual Studio 2022 with the C++ toolset found.
	exit /b 1
)

rem Prefer cmake from PATH, fall back to the one shipped with VS
set "CMAKE=cmake"
where cmake >nul 2>&1 || set "CMAKE=%VSPATH%\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"

if /i "%ACTION%"=="clean" (
	if exist "%BUILD_DIR%" (
		echo [CLEAN] removing %BUILD_DIR%
		rmdir /s /q "%BUILD_DIR%"
	)
	exit /b 0
)

rem vcvars provides cl.exe / rc.exe / nmake.exe
call "%VSPATH%\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 (
	echo [ERROR] Failed to initialise the MSVC environment.
	exit /b 1
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
"%CMAKE%" -S "%DEPS_SRC%/bullet3" -B "%DEPS_BUILD%/bullet3" -G "%GEN%" -A x64 ^
 -DBUILD_SHARED_LIBS=OFF -DUSE_MSVC_RUNTIME_LIBRARY_DLL=ON ^
 -DBUILD_BULLET2_DEMOS=OFF -DBUILD_BULLET3=OFF -DBUILD_CPU_DEMOS=OFF -DBUILD_UNIT_TESTS=OFF ^
 -DBUILD_EXTRAS=OFF -DBUILD_OPENGL3_DEMOS=OFF -DUSE_GRAPHICAL_BENCHMARK=OFF -DINSTALL_LIBS=ON ^
 -DCMAKE_INSTALL_PREFIX=%DEPS_PREFIX%
if errorlevel 1 goto :deps_fail
"%CMAKE%" --build "%DEPS_BUILD%/bullet3" --config %CONFIG% --target BulletCollision BulletDynamics LinearMath
if errorlevel 1 goto :deps_fail
"%CMAKE%" --install "%DEPS_BUILD%/bullet3" --config %CONFIG%
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
"%CMAKE%" -S "%DEPS_SRC%/qads" -B "%DEPS_BUILD%/qads" -G "%GEN%" -A x64 ^
 -DCMAKE_PREFIX_PATH=%QT_DIR% -DADS_VERSION=4.3.1 -DBUILD_EXAMPLES=OFF -DBUILD_STATIC=OFF ^
 -DCMAKE_INSTALL_PREFIX=%ADS_DIR%
if errorlevel 1 goto :deps_fail
"%CMAKE%" --build "%DEPS_BUILD%/qads" --config %CONFIG% --target install
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
"%CMAKE%" -S "%ROOT%" -B "%BUILD_DIR%" -G "%GEN%" -A x64 ^
 -DQt5_DIR=%QT_DIR%/lib/cmake/Qt5 ^
 -DQSCINTILLA_ROOT=%QT_DIR% ^
 -Dqt5advanceddocking_DIR=%ADS_DIR%/lib/cmake/qt5advanceddocking ^
 -DHIVE_DEPS_PREFIX=%DEPS_PREFIX%
if errorlevel 1 goto :app_fail

echo.
echo ===== Building HiveWE (%CONFIG%) =====
"%CMAKE%" --build "%BUILD_DIR%" --config %CONFIG% --parallel
if errorlevel 1 goto :app_fail

echo.
echo ===== Deploying runtime =====
rem windeployqt only looks inside Qt\bin for dependencies, so seed the ADS DLL there
if not exist "%QT_DIR%\bin\qt5advanceddocking.dll" (
	copy /y "%ADS_DIR%\bin\qt5advanceddocking.dll" "%QT_DIR%\bin\" >nul 2>&1
)
"%QT_DIR%/bin/windeployqt.exe" --release --no-translations --no-opengl-sw ^
 "%BUILD_DIR%/%CONFIG%/HiveWE.exe" ^
 "%BUILD_DIR%/%CONFIG%/qscintilla2_qt5.dll" ^
 "%BUILD_DIR%/%CONFIG%/qt5advanceddocking.dll"
if errorlevel 1 (
	echo [WARN] windeployqt failed; Qt runtime DLLs may be missing.
)

echo.
echo [DONE] %BUILD_DIR%\%CONFIG%\HiveWE.exe
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
"%CMAKE%" -S "%DEPS_SRC%/%~1" -B "%DEPS_BUILD%/%~1" -G "%GEN%" -A x64 %~2
if errorlevel 1 exit /b 1
"%CMAKE%" --build "%DEPS_BUILD%/%~1" --config %CONFIG%
if errorlevel 1 exit /b 1
"%CMAKE%" --install "%DEPS_BUILD%/%~1" --config %CONFIG%
if errorlevel 1 exit /b 1
exit /b 0

:help
echo.
echo  HiveWE 0.6 build script
echo.
echo    build.bat          build HiveWE (default)
echo    build.bat app      same as above
echo    build.bat deps     build all third-party dependencies
echo    build.bat all      dependencies, then HiveWE
echo    build.bat clean    remove the build directory
echo.
echo  Qt path, dependency prefix and ADS path are configured at the top of this file.
echo.
exit /b 0
