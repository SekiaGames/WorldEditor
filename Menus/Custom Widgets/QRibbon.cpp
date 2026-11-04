#include "QRibbon.h"

#include <QStyle>
#include <QStyleOption>
#include <QPainter>

QRibbonButton::QRibbonButton(QWidget* parent) : QToolButton(parent) {
	setSizePolicy(QSizePolicy::Fixed, QSizePolicy::Expanding);
}

QRibbonButton::~QRibbonButton() {
	for (auto&& i : shortcuts) {
		i->setParent(nullptr);
		delete i;
	}
}

void QRibbonButton::setShortCut(const QKeySequence sequence, const std::vector<QWidget*>& attach_to) {
	for (auto&& i : attach_to) {
		shortcuts.push_back(new QShortcut(sequence, i));
		connect(shortcuts.back(), &QShortcut::activated, this, &QToolButton::click);
		connect(shortcuts.back(), &QShortcut::activatedAmbiguously, []() { printf("kut2\n"); });
	}
}

void QRibbonButton::disableShortcuts() {
	for (auto&& i : shortcuts) {
		i->setEnabled(false);
	}
}

void QRibbonButton::enableShortcuts() {
	for (auto&& i : shortcuts) {
		i->setEnabled(true);
	}
}

void QRibbonButton::disconnectShortcuts() {
	//for (auto&& i : shortcuts) {
	//	delete i;
	//	shortcuts.clear();
	//}
}

QRibbonContainer::QRibbonContainer(QWidget* parent) : QFrame(parent) {
	setLayout(layout);
	setAutoFillBackground(true);

	layout->setContentsMargins(0, 0, 0, 0);
	layout->setSpacing(0);
}

void QRibbonContainer::addWidget(QWidget* widget, int row, int column) {
	layout->addWidget(widget, row, column);
}

void QRibbonContainer::clear() {
	QLayoutItem *item;
	while ((item = layout->takeAt(0))) {
		if (item->widget()) {
			delete item->widget();
		}
		delete item;
	}
}

QRibbonSection::QRibbonSection(QWidget* parent) : QWidget(parent) {
	setLayout(section_inner);

	section_inner->setContentsMargins(0, 0, 0, 0);
	section_inner->setSpacing(5);
	section_inner->setMargin(0);

	section_outer->setContentsMargins(0, 0, 0, 0);
	section_outer->setSpacing(0);
	section_outer->setMargin(0);

	layout()->setContentsMargins(0, 0, 0, 0);
	setContentsMargins(0, 0, 0, 0);
	
	section_inner->addLayout(section_outer);
	section_inner->addWidget(section_text);

	section_text->setAlignment(Qt::AlignBottom | Qt::AlignmentFlag::AlignHCenter);
}

void QRibbonSection::addWidget(QWidget* widget) {
	section_outer->addWidget(widget);
}

void QRibbonSection::addLayout(QLayout* layout) {
	section_outer->addLayout(layout);
}


void QRibbonSection::setText(const QString& text) {
	section_text->setText(text);
}

QRibbonTab::QRibbonTab(QWidget* parent) : QWidget(parent) {
	setContentsMargins(0, 0, 0, 0);
	sections->addStretch(1);
	layout()->setContentsMargins(6, 4, 5, 3);
}

void QRibbonTab::disableShortcuts() {
}

void QRibbonTab::enableShortcuts() {
}

void QRibbonTab::paintEvent(QPaintEvent* event) {
	QStyleOption opt;
	opt.init(this);
	QPainter p(this);
	style()->drawPrimitive(QStyle::PE_Widget, &opt, &p, this);

	QWidget::paintEvent(event);
}

void QRibbonTab::addSection(QLayout* layout) {
	sections->insertLayout(0, layout);
}

void QRibbonTab::addSection(QRibbonSection* section) {
	QFrame* line = new QFrame();
	line->setObjectName("seperator");

	int count = sections->count();
	sections->insertWidget(count - 1, section);
	sections->insertWidget(count, line);
}


QRibbon::QRibbon(QWidget* parent) : QTabWidget(parent) {
}

QRibbon::~QRibbon() {
}