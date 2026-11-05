cmake_minimum_required(VERSION 3.20)
project(HiveWE CXX)

set(CMAKE_INCLUDE_CURRENT_DIR ON)

set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)

# ---------------------------------------------------------------- Qt 5
# 代码里用到 QOpenGLWidget / QOpenGLFunctions_4_5_Core，只需要 Gui + Widgets
find_package(Qt5 5.15 REQUIRED COMPONENTS Gui Widgets)

# --------------------------------------------------------- 依赖查找前缀
# 每个依赖都优先用 find_package 的 CONFIG 模式（vcpkg 的安装方式），
# 找不到再退回手动编译安装的目录布局（include/ + lib/）。
# 默认指向仓库本地的 toolchain\deps\prefix（build.bat 会显式传同一个路径），
# 避免把依赖装到 C 盘。
set(HIVE_DEPS_PREFIX "${CMAKE_SOURCE_DIR}/toolchain/deps/prefix" CACHE PATH "手动编译的依赖安装前缀（非 vcpkg）")
list(APPEND CMAKE_PREFIX_PATH "${HIVE_DEPS_PREFIX}")

# 手写一个 imported target，让链接和头文件路径一次搞定
function(hivewe_import_target name lib_path include_dir)
	if(NOT lib_path)
		message(FATAL_ERROR "找不到 ${name} 的库文件。请检查 -DHIVE_DEPS_PREFIX=${HIVE_DEPS_PREFIX}，或显式指定它的库路径")
	endif()
	add_library(${name} UNKNOWN IMPORTED)
	set_target_properties(${name} PROPERTIES
		IMPORTED_LOCATION "${lib_path}"
		INTERFACE_INCLUDE_DIRECTORIES "${include_dir}")
endfunction()

# ------------------------------------------------------------------ glm
find_package(glm CONFIG QUIET)
if(glm_FOUND)
	set(HIVE_GLM_TARGET glm::glm)
else()
	find_path(GLM_INCLUDE_DIR NAMES glm/glm.hpp HINTS "${HIVE_DEPS_PREFIX}/include")
	if(NOT GLM_INCLUDE_DIR)
		message(FATAL_ERROR "找不到 glm。请检查 ${HIVE_DEPS_PREFIX}/include/glm")
	endif()
	add_library(hivewe_glm INTERFACE)
	target_include_directories(hivewe_glm INTERFACE "${GLM_INCLUDE_DIR}")
	set(HIVE_GLM_TARGET hivewe_glm)
endif()

# ---------------------------------------------------------------- soil2
find_package(soil2 CONFIG QUIET)
if(TARGET soil2::soil2)
	set(HIVE_SOIL2_TARGET soil2::soil2)
elseif(TARGET soil2)
	set(HIVE_SOIL2_TARGET soil2)
else()
	find_path(SOIL2_INCLUDE_DIR NAMES SOIL2/SOIL2.h HINTS "${HIVE_DEPS_PREFIX}/include")
	find_library(SOIL2_LIBRARY NAMES soil2 HINTS "${HIVE_DEPS_PREFIX}/lib")
	hivewe_import_target(hivewe_soil2 "${SOIL2_LIBRARY}" "${SOIL2_INCLUDE_DIR}")
	set(HIVE_SOIL2_TARGET hivewe_soil2)
endif()

# -------------------------------------------------------------- StormLib
find_package(stormlib CONFIG QUIET)
if(TARGET stormlib::stormlib)
	set(HIVE_STORMLIB_TARGET stormlib::stormlib)
elseif(TARGET StormLib::storm)
	set(HIVE_STORMLIB_TARGET StormLib::storm)
else()
	find_path(STORM_INCLUDE_DIR NAMES StormLib.h HINTS "${HIVE_DEPS_PREFIX}/include")
	find_library(STORM_LIBRARY NAMES storm StormLib HINTS "${HIVE_DEPS_PREFIX}/lib")
	hivewe_import_target(hivewe_storm "${STORM_LIBRARY}" "${STORM_INCLUDE_DIR}")
	set(HIVE_STORMLIB_TARGET hivewe_storm)
endif()

# --------------------------------------------------------------- CascLib
find_package(casclib CONFIG QUIET)
if(TARGET casclib::casclib)
	set(HIVE_CASCLIB_TARGET casclib::casclib)
elseif(TARGET CascLib::casc)
	set(HIVE_CASCLIB_TARGET CascLib::casc)
else()
	find_path(CASC_INCLUDE_DIR NAMES CascLib.h HINTS "${HIVE_DEPS_PREFIX}/include")
	find_library(CASC_LIBRARY NAMES casc casc_static HINTS "${HIVE_DEPS_PREFIX}/lib")
	hivewe_import_target(hivewe_casc "${CASC_LIBRARY}" "${CASC_INCLUDE_DIR}")
	set(HIVE_CASCLIB_TARGET hivewe_casc)
endif()

# ---------------------------------------------------------------- Bullet
# 不用 find_package(Bullet)：它导出的 BULLET_INCLUDE_DIRS 是相对路径 "include/bullet"，
# 会被解析到源码目录下，而且导出 target 不带 INTERFACE_INCLUDE_DIRECTORIES。
# 自己定位头文件和库更可靠。
find_path(HIVE_BULLET_INCLUDE_DIR NAMES btBulletCollisionCommon.h
	HINTS "${HIVE_DEPS_PREFIX}/include/bullet" "${HIVE_DEPS_PREFIX}/include")
if(NOT HIVE_BULLET_INCLUDE_DIR)
	find_package(Bullet QUIET)
	set(HIVE_BULLET_INCLUDE_DIR "${BULLET_INCLUDE_DIRS}")
endif()
if(NOT HIVE_BULLET_INCLUDE_DIR)
	message(FATAL_ERROR "找不到 Bullet 头文件（btBulletCollisionCommon.h）。请检查 ${HIVE_DEPS_PREFIX}/include/bullet")
endif()

find_library(BULLET_COLLISION_LIBRARY NAMES BulletCollision HINTS "${HIVE_DEPS_PREFIX}/lib")
find_library(BULLET_DYNAMICS_LIBRARY NAMES BulletDynamics HINTS "${HIVE_DEPS_PREFIX}/lib")
find_library(BULLET_LINEARMATH_LIBRARY NAMES LinearMath HINTS "${HIVE_DEPS_PREFIX}/lib")
hivewe_import_target(hivewe_BulletCollision "${BULLET_COLLISION_LIBRARY}" "${HIVE_BULLET_INCLUDE_DIR}")
hivewe_import_target(hivewe_BulletDynamics "${BULLET_DYNAMICS_LIBRARY}" "${HIVE_BULLET_INCLUDE_DIR}")
hivewe_import_target(hivewe_LinearMath "${BULLET_LINEARMATH_LIBRARY}" "${HIVE_BULLET_INCLUDE_DIR}")
set(HIVE_BULLET_LIBS hivewe_BulletCollision hivewe_BulletDynamics hivewe_LinearMath)
set(BULLET_INCLUDE_DIRS "${HIVE_BULLET_INCLUDE_DIR}")

# ----------------------------------------------------------- libjpeg-turbo
# BLP.cpp 用的是 turbojpeg API（tjInitDecompress 等），不是 libjpeg 的 jpeg_* API，
# 所以不能用 find_package(JPEG)，必须显式找 turbojpeg
find_path(TURBOJPEG_INCLUDE_DIR NAMES turbojpeg.h HINTS "${HIVE_DEPS_PREFIX}/include")
find_library(TURBOJPEG_LIBRARY NAMES turbojpeg turbojpeg-static HINTS "${HIVE_DEPS_PREFIX}/lib")
if(NOT TURBOJPEG_INCLUDE_DIR OR NOT TURBOJPEG_LIBRARY)
	message(FATAL_ERROR "找不到 turbojpeg。\n"
		"请用 -DTURBOJPEG_INCLUDE_DIR=<...>/include -DTURBOJPEG_LIBRARY=<...>/lib/turbojpeg.lib 指定")
endif()

# ------------------------------------------------------- QScintilla
# QScintilla 必须构建成动态库：JassEditor.h 里 #define QSCINTILLA_DLL 表示从 DLL 导入符号
find_path(QSCINTILLA_INCLUDE_DIR NAMES Qsci/qsciscintilla.h
	HINTS ${QSCINTILLA_ROOT} ENV QSCINTILLA_ROOT
	PATH_SUFFIXES include)
find_library(QSCINTILLA_LIBRARY NAMES qscintilla2_qt5 qscintilla2
	HINTS ${QSCINTILLA_ROOT} ENV QSCINTILLA_ROOT
	PATH_SUFFIXES lib)
if(NOT QSCINTILLA_INCLUDE_DIR OR NOT QSCINTILLA_LIBRARY)
	message(FATAL_ERROR "找不到 QScintilla。请用 -DQSCINTILLA_ROOT=<repo>/toolchain/qt/5.15.2/msvc2019_64 指定安装前缀")
endif()

# ------------------------------------- Qt Advanced Docking System
# 包名有两套：vcpkg 的 port 叫 qt-advanced-docking-system（target: qt-advanced-docking-system::qtadvanceddocking），
# 上游 4.x 源码自己安装出来叫 qt5advanceddocking（target: ads::qt5advanceddocking），两个都试
find_package(qt-advanced-docking-system CONFIG QUIET)
find_package(qt5advanceddocking CONFIG QUIET)
if(TARGET qt-advanced-docking-system::qtadvanceddocking)
	set(HIVE_ADS_TARGET qt-advanced-docking-system::qtadvanceddocking)
elseif(TARGET ads::qt5advanceddocking)
	set(HIVE_ADS_TARGET ads::qt5advanceddocking)
elseif(TARGET qtadvanceddocking)
	set(HIVE_ADS_TARGET qtadvanceddocking)
else()
	message(FATAL_ERROR "找不到 Qt Advanced Docking System。\n"
		"vcpkg 版请设 -Dqt-advanced-docking-system_DIR=<install>/lib/cmake/qt-advanced-docking-system\n"
		"上游源码版请设 -Dqt5advanceddocking_DIR=<install>/lib/cmake/qt5advanceddocking")
endif()

# ------------------------------------------------------------ 可执行文件
add_executable(HiveWE
	${QT_RESOURCES}
	HiveWE.rc
	Camera.cpp
	HiveWE.cpp
	InputHandler.cpp
	JassTokenizer.cpp
	main.cpp
	TerrainUndo.cpp
	Utilities.cpp
	WindowHandler.h

	Base/Doodads.cpp
	Base/FlowLayout.cpp
	Base/GameCameras.cpp
	Base/Hierarchy.cpp
	Base/Imports.cpp
	Base/Map.cpp
	Base/MapInfo.cpp
	Base/PathingMap.cpp
	Base/Regions.cpp
	Base/ResourceManager.cpp
	Base/ShadowMap.cpp
	Base/Sounds.cpp
	Base/Terrain.cpp
	Base/Triggers.cpp
	Base/TriggerStrings.cpp
	Base/Units.cpp
	Base/Physics.cpp

	Base/Brush/Brush.cpp
	Base/Brush/DoodadBrush.cpp
	Base/Brush/PathingBrush.cpp
	Base/Brush/TerrainBrush.cpp

	"Base/File Formats/BLP.cpp"
	"Base/File Formats/CASC.cpp"
	"Base/File Formats/INI.cpp"
	"Base/File Formats/JSON.cpp"
	"Base/File Formats/MDX.cpp"
	"Base/File Formats/MPQ.cpp"
	"Base/File Formats/SLK.cpp"

	Base/Resources/CliffMesh.cpp
	Base/Resources/GPUTexture.cpp
	Base/Resources/GroundTexture.cpp
	Base/Resources/Shader.cpp
	Base/Resources/SkinnedMesh.cpp
	Base/Resources/StaticMesh.cpp
	Base/Resources/Texture.cpp

	Menus/DoodadPalette.cpp
	Menus/ImportManager.cpp
	Menus/ImportManagerEdit.cpp
	Menus/MapInfoEditor.cpp
	Menus/Minimap.cpp
	Menus/Palette.cpp
	Menus/PathingPalette.cpp
	Menus/SettingsEditor.cpp
	Menus/TerrainPalette.cpp
	Menus/TilePather.cpp
	Menus/TilePicker.cpp
	Menus/TileSetter.cpp
	Menus/TriggerEditor.cpp

	"Menus/Custom Widgets/AspectRatioPixmapLabel.cpp"
	"Menus/Custom Widgets/ColorButton.cpp"
	"Menus/Custom Widgets/GLWidget.cpp"
	"Menus/Custom Widgets/JassEditor.cpp"
	"Menus/Custom Widgets/QRibbon.cpp"
	"Menus/Custom Widgets/MainRibbon.cpp"
	"Menus/Custom Widgets/SearchWindow.cpp"
	"Menus/Custom Widgets/VariableEditor.cpp"
	"Menus/Custom Widgets/TriggerExplorer.cpp"
)

target_include_directories(HiveWE PRIVATE
	"Base" "Base/Brush" "Base/File Formats" "Base/Resources" "Menus" "Menus/Custom Widgets"
	${QSCINTILLA_INCLUDE_DIR}
	${TURBOJPEG_INCLUDE_DIR}
	${BULLET_INCLUDE_DIRS}
)

target_compile_features(HiveWE PUBLIC cxx_std_20)

target_link_libraries(HiveWE PRIVATE
	Qt5::Gui
	Qt5::Widgets
	${HIVE_GLM_TARGET}
	${HIVE_SOIL2_TARGET}
	${HIVE_STORMLIB_TARGET}
	${HIVE_CASCLIB_TARGET}
	${HIVE_BULLET_LIBS}
	${TURBOJPEG_LIBRARY}
	${QSCINTILLA_LIBRARY}
	${HIVE_ADS_TARGET}
)

if(WIN32)
	target_compile_definitions(HiveWE PUBLIC UNICODE _UNICODE)
endif()

if(MSVC)
	# 源码里有非 ASCII 字符，不加 /utf-8 会报 C4819
	target_compile_options(HiveWE PRIVATE /utf-8 /bigobj)
endif()

# ------------------------------------------------ 构建后整理运行目录
# 原版用 create_symlink，Windows 上需要管理员权限或开发者模式，改成直接复制
add_custom_command(TARGET HiveWE POST_BUILD
	COMMAND ${CMAKE_COMMAND} -E copy_directory
		"${PROJECT_SOURCE_DIR}/Data"
		"$<TARGET_FILE_DIR:HiveWE>/Data"
	COMMENT "复制 Data 目录到可执行文件旁"
)

if(WIN32)
	# QScintilla 是动态库，windeployqt 不会带上它，这里顺手拷过去
	find_file(HIVE_QSCINTILLA_DLL NAMES qscintilla2_qt5.dll qscintilla2.dll
		HINTS ${QSCINTILLA_ROOT} ENV QSCINTILLA_ROOT
		PATH_SUFFIXES bin)
	if(HIVE_QSCINTILLA_DLL)
		add_custom_command(TARGET HiveWE POST_BUILD
			COMMAND ${CMAKE_COMMAND} -E copy_if_different
				"${HIVE_QSCINTILLA_DLL}" "$<TARGET_FILE_DIR:HiveWE>"
			COMMENT "复制 QScintilla 运行时 DLL")
	else()
		message(WARNING "没找到 qscintilla2_qt5.dll，运行时请手动放到 exe 同目录")
	endif()

	# ADS 同样是动态库，直接从 imported target 的 DLL 位置拷
	get_target_property(HIVE_ADS_DLL ${HIVE_ADS_TARGET} IMPORTED_LOCATION_RELEASE)
	if(NOT HIVE_ADS_DLL)
		get_target_property(HIVE_ADS_DLL ${HIVE_ADS_TARGET} IMPORTED_LOCATION)
	endif()
	if(HIVE_ADS_DLL AND EXISTS "${HIVE_ADS_DLL}")
		add_custom_command(TARGET HiveWE POST_BUILD
			COMMAND ${CMAKE_COMMAND} -E copy_if_different
				"${HIVE_ADS_DLL}" "$<TARGET_FILE_DIR:HiveWE>"
			COMMENT "复制 ADS 运行时 DLL")
	endif()
endif()
