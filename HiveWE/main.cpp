#include <QPalette>
#include <QCoreApplication>
#include <QApplication>
#include <QSurfaceFormat>
#include <QFont>
#include <QStyleFactory>
#include <QSettings>
#include <QFile>
#include <QStringList>

#include "HiveWE.h"

#ifdef WIN32
// To force HiveWE to run on the discrete GPU if available
extern "C" {
	__declspec(dllexport) int AmdPowerXpressRequestHighPerformance = 1;
	__declspec(dllexport) unsigned long NvOptimusEnablement = 1;
}
#endif

int main(int argc, char *argv[]) {
	QSurfaceFormat format;
	format.setDepthBufferSize(24);
	format.setStencilBufferSize(8);
	format.setVersion(4, 5);
	format.setProfile(QSurfaceFormat::CoreProfile);
	QSurfaceFormat::setDefaultFormat(format);

	QCoreApplication::setAttribute(Qt::AA_ShareOpenGLContexts);
	QCoreApplication::setOrganizationName("HiveWE");
	QCoreApplication::setApplicationName("HiveWE");

	QApplication a(argc, argv);

	qApp->setStyle(QStyleFactory::create("Fusion"));
	// increase font size for better reading
	QFont defaultFont = QApplication::font();
	defaultFont.setPointSize(defaultFont.pointSize() + 2);
	qApp->setFont(defaultFont);
	// modify palette to dark
	QPalette darkPalette;
	darkPalette.setColor(QPalette::Window, QColor(53, 53, 53));
	darkPalette.setColor(QPalette::WindowText, Qt::white);
	darkPalette.setColor(QPalette::Disabled, QPalette::WindowText, QColor(127, 127, 127));
	darkPalette.setColor(QPalette::Base, QColor(42, 42, 42));
	darkPalette.setColor(QPalette::AlternateBase, QColor(66, 66, 66));
	darkPalette.setColor(QPalette::ToolTipBase, Qt::white);
	darkPalette.setColor(QPalette::ToolTipText, Qt::white);
	darkPalette.setColor(QPalette::Text, Qt::white);
	darkPalette.setColor(QPalette::Disabled, QPalette::Text, QColor(127, 127, 127));
	darkPalette.setColor(QPalette::Dark, QColor(35, 35, 35));
	darkPalette.setColor(QPalette::Shadow, QColor(20, 20, 20));
	darkPalette.setColor(QPalette::Button, QColor(53, 53, 53));
	darkPalette.setColor(QPalette::ButtonText, Qt::white);
	darkPalette.setColor(QPalette::Disabled, QPalette::ButtonText, QColor(127, 127, 127));
	darkPalette.setColor(QPalette::BrightText, Qt::red);
	darkPalette.setColor(QPalette::Link, QColor(42, 130, 218));
	darkPalette.setColor(QPalette::Highlight, QColor(42, 130, 218));
	darkPalette.setColor(QPalette::Disabled, QPalette::Highlight, QColor(80, 80, 80));
	darkPalette.setColor(QPalette::HighlightedText, Qt::white);
	darkPalette.setColor(QPalette::Disabled, QPalette::HighlightedText, QColor(127, 127, 127));

	qApp->setPalette(darkPalette);

	QSettings settings;
	if (!settings.contains("theme")) {
		settings.setValue("theme", "Light");
	}

	QFile file("Data/Themes/" + settings.value("theme").toString() + ".qss");
	file.open(QFile::ReadOnly);
	QString StyleSheet = QLatin1String(file.readAll());

	qApp->setStyleSheet(StyleSheet);

	// 调试用启动参数（给 Godot 版生成同相机同尺寸的参考图）：
	//   --map=<地图目录>  用指定的目录地图替换内置测试地图
	//   --shot=<png>      渲染若干帧后把视口存成 PNG 并退出
	//   --frames=<n>      截图前的等待帧数，默认 90
	//   --size=<W>x<H>    主窗口尺寸
	//   --probe=x0,y0,x1,y1,steps  沿直线打射线测坡度（碰撞面 + 渲染面），打完退出
	for (const QString& argument : QCoreApplication::arguments()) {
		if (argument.startsWith("--map=")) {
			debug_launch.map_directory = argument.mid(6);
		} else if (argument.startsWith("--shot=")) {
			GLWidget::screenshot_path = argument.mid(7);
		} else if (argument.startsWith("--frames=")) {
			GLWidget::screenshot_countdown = argument.mid(9).toInt();
		} else if (argument == "--no-doodads") {
			debug_launch.hide_doodads = true;
		} else if (argument == "--no-units") {
			debug_launch.hide_units = true;
		} else if (argument.startsWith("--dist=")) {
			debug_launch.distance = argument.mid(7).toFloat();
		} else if (argument.startsWith("--focus=")) {
			const QStringList parts = argument.mid(8).split(',');
			if (parts.size() == 2) {
				debug_launch.focus_x = parts[0].toFloat();
				debug_launch.focus_y = parts[1].toFloat();
			}
		} else if (argument == "--no-water") {
			debug_launch.hide_water = true;
		} else if (argument == "--no-lighting") {
			debug_launch.hide_lighting = true;
		} else if (argument.startsWith("--target=")) {
			// --target=x,y,z：直接指定相机轨道中心（格），不再贴到地表，用来复现 Godot 的机位
			const QStringList parts = argument.mid(9).split(',');
			if (parts.size() == 3) {
				debug_launch.target_x = parts[0].toFloat();
				debug_launch.target_y = parts[1].toFloat();
				debug_launch.target_z = parts[2].toFloat();
				debug_launch.target_set = true;
			}
		} else if (argument.startsWith("--probe=")) {
			// --probe=x0,y0,x1,y1,steps：沿直线打射线测坡度，打完就退出
			const QStringList parts = argument.mid(8).split(',');
			if (parts.size() == 5) {
				debug_launch.probe_x0 = parts[0].toFloat();
				debug_launch.probe_y0 = parts[1].toFloat();
				debug_launch.probe_x1 = parts[2].toFloat();
				debug_launch.probe_y1 = parts[3].toFloat();
				debug_launch.probe_steps = parts[4].toInt();
			}
		} else if (argument == "--no-pathing") {
			debug_launch.hide_pathing = true;
		} else if (argument.startsWith("--size=")) {
			const QStringList parts = argument.mid(7).split('x');
			if (parts.size() == 2) {
				debug_launch.window_width = parts[0].toInt();
				debug_launch.window_height = parts[1].toInt();
			}
		}
	}
	if (GLWidget::screenshot_countdown < 0 && !GLWidget::screenshot_path.isEmpty()) {
		GLWidget::screenshot_countdown = 90;
	}

	HiveWE w;

	if (debug_launch.window_width > 0 && debug_launch.window_height > 0) {
		w.resize(debug_launch.window_width, debug_launch.window_height);
	}

	return QApplication::exec();
}