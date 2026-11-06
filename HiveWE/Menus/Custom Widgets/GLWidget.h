#pragma once

#include <QObject>
#include <QOpenGLWidget>

#include "Camera.h"
#include <QElapsedTimer>

class GLWidget : public QOpenGLWidget {
	Q_OBJECT

public:
	QElapsedTimer elapsed_timer;
	GLuint vao;

	TPSCamera tps_camera;
	FPSCamera fps_camera;

	/// 截图路径（空 = 不截）和还剩几帧，由 main.cpp 的 --shot/--frames 写入。
	static QString screenshot_path;
	static int screenshot_countdown;

	explicit GLWidget(QWidget* parent);
	~GLWidget() = default;

	void initializeGL() override;
	void resizeGL(int w, int h) override;
	void update_scene();
	void paintGL() override;

	void keyPressEvent(QKeyEvent* event) override;
	void keyReleaseEvent(QKeyEvent* event) override;
	void mouseMoveEvent(QMouseEvent* event) override;
	void mousePressEvent(QMouseEvent* event) override;
	void mouseReleaseEvent(QMouseEvent* event) override;
	void wheelEvent(QWheelEvent* event) override;
};