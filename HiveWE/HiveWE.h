#pragma once

#include "ui_HiveWE.h"

#include <QMainWindow>
#include "WindowHandler.h"
#include "QRibbon.h"
#include "Minimap.h"
#include "Map.h"
#include "INI.h"


class HiveWE : public QMainWindow {
	Q_OBJECT

public:
	explicit HiveWE(QWidget* parent = nullptr);

	void load_folder();
	void load_mpq();
	void save();
	void save_as();
	void export_mpq();
	void play_test();

private:
	Ui::HiveWEClass ui;
	QRibbonTab* current_custom_tab = nullptr;
	Minimap* minimap = new Minimap(this);

	void closeEvent(QCloseEvent* event) override;
	void resizeEvent(QResizeEvent* event) override;
	void moveEvent(QMoveEvent* event) override;

	void switch_camera();
	void switch_warcraft();
	void import_heightmap();
	void save_window_state();
	void restore_window_state();

	/// Adds the tab to the ribbon and sets the current index to this tab
	void set_current_custom_tab(QRibbonTab* tab, QString name);
	void remove_custom_tab();
signals:
	void tileset_changed();
	void palette_changed(QRibbonTab* tab);

	void saving_initiated();
};

extern Map* map;
extern ini::INI world_edit_strings;
extern ini::INI world_edit_game_strings;
extern ini::INI world_edit_data;
extern WindowHandler window_handler;

/// 命令行调试入口：给 HiveWE 一份地图目录、指定窗口尺寸，渲染若干帧后把视口存成 PNG 再退出。
/// 用来给 Godot 版生成"同相机同尺寸"的参考图（见 main.cpp 的参数解析）。
struct DebugLaunch {
	QString map_directory;
	int window_width = 0;
	int window_height = 0;
	bool hide_doodads = false;
	bool hide_units = false;
	bool hide_pathing = false;
	bool hide_water = false;
	bool hide_lighting = false;
	float distance = -1.f;      // 相机距离（格），<0 表示用 Camera::reset 的 20
	float focus_x = -1.f;       // 相机目标格坐标，<0 表示用地图中心
	float focus_y = -1.f;

	// 坡度探针：沿 (x0,y0)-(x1,y1) 打 steps+1 条向下的射线，打印碰撞高度和渲染高度后退出
	float probe_x0 = 0.f;
	float probe_y0 = 0.f;
	float probe_x1 = 0.f;
	float probe_y1 = 0.f;
	int probe_steps = 0;

	// 相机轨道中心（格，z 也是格）。给了之后不再贴地，用来复现 Godot 侧任意机位
	bool target_set = false;
	float target_x = 0.f;
	float target_y = 0.f;
	float target_z = 0.f;
};
extern DebugLaunch debug_launch;

extern slk::SLK units_slk;
extern slk::SLK units_meta_slk;

extern slk::SLK items_slk;

extern slk::SLK abilities_slk;

extern slk::SLK doodads_slk;
extern slk::SLK doodads_meta_slk;
extern slk::SLK destructibles_slk;
extern slk::SLK destructibles_meta_slk;