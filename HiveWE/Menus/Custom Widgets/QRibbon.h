#pragma once

#include <vector>

#include <QShortcut>
#include <QToolButton>
#include <QFrame>
#include <QGridLayout>
#include <QBoxLayout>
#include <QLabel>
#include <QMenu>
#include <QTabWidget>

class QRibbonButton : public QToolButton {
	Q_OBJECT

public:
	QRibbonButton(QWidget* parent = nullptr);
	~QRibbonButton();

	std::vector<QShortcut*> shortcuts;

	void setShortCut(const QKeySequence sequence, const std::vector<QWidget*>& attach_to);

	void disableShortcuts();
	void enableShortcuts();
	void disconnectShortcuts();
};

class QRibbonContainer : public QFrame {
	Q_OBJECT

	QGridLayout* layout = new QGridLayout;

public:
	QRibbonContainer(QWidget* parent = nullptr);

	void addWidget(QWidget* widget, int row, int column);
	void clear();
};

class QRibbonSection : public QWidget {
	Q_OBJECT

public:
	QLabel* section_text = new QLabel;
	QVBoxLayout* section_inner = new QVBoxLayout;
	QHBoxLayout* section_outer = new QHBoxLayout;

	QRibbonSection(QWidget* parent = nullptr);
	
	void addWidget(QWidget* widget);
	void addLayout(QLayout* layout);

	void setText(const QString& text);
};

class QRibbonTab : public QWidget {
	Q_OBJECT

public:
	QHBoxLayout* sections = new QHBoxLayout(this);
	
	QRibbonTab(QWidget* parent = nullptr);
	
	void addSection(QLayout* layout);
	void addSection(QRibbonSection* layout);

	void disableShortcuts();
	void enableShortcuts();


	void paintEvent(QPaintEvent* event) override;
};


class QRibbon : public QTabWidget {
	Q_OBJECT

public:
	QRibbon(QWidget *parent = nullptr);
	~QRibbon();
};
