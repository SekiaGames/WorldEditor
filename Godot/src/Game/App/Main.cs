using Godot;
using System;

namespace WorldEdit;

/// <summary>
/// 主窗口：Ribbon + 3D 视口 + 调色板停靠区 + 状态栏 + 浮层窗口。
/// 这一版只搭界面，所有业务动作都走 <see cref="Stub"/>。
/// </summary>
public partial class Main : Control
{
    private Ribbon _ribbon;
    private ViewportPanel _view;
    private Window _paletteWindow;
    private Window _currentWindow;
    private MinimapPanel _minimap;

    private Label _lblMap;
    private Label _lblBrush;
    private Label _lblCoord;
    private Label _lblFps;
    private Label _lblHint;

    private PanelContainer _toast;
    private Label _toastLabel;
    private double _toastLeft;

    private string _paletteId;
    private double _fpsTimer;
    private int _fpsFrames;
    private int _frames;

    private bool _cameraTest;
    private bool _tooltipProbe;
    private bool _checkTabs;
    private int _probeFrame;
    private int _probeTab;
    private string _shotPath;
    private int _shotFrames = 24;

    public override void _Ready()
    {
        // Tooltip 之类的弹窗由视口自己创建，不挂在 Main 下就继承不到 UiTheme，
        // 会退回引擎默认主题（深底 + 无中文字形的 Open Sans）。这里把回退字体统一成中文字体。
        ThemeDB.FallbackFont = UiTheme.Font;
        ThemeDB.FallbackFontSize = 12;

        Theme = UiTheme.Build();
        BuildUi();

        Stub.Fired += OnStubFired;
        // HiveWE 启动时只有主窗口，调色板要点 Window 页签里的按钮才出现
        ParseCommandLine();

        if (_tooltipProbe)
        {
            BuildTooltipProbe();
        }

        // 截图/冒烟模式加个看门狗：万一没走到退出分支，也不要留进程占着 exe
        if (_shotPath != null)
        {
            GetTree().CreateTimer(30).Timeout += () =>
            {
                GD.Print("[SHOT] watchdog timeout, quitting");
                GetTree().Quit();
            };
        }
    }

    /// <summary>
    /// 自检：用与 Tooltip 完全相同的主题类型链（TooltipPanel / TooltipLabel）建一个探针，
    /// 打印解析出来的底色与文字色，并按同样样式渲染出来。参数 --tooltip-probe。
    /// </summary>
    private void BuildTooltipProbe()
    {
        // 与 Viewport::_gui_show_tooltip_at 完全同构：PopupPanel(variation=TooltipPanel)
        // 加到悬停控件下面，里面放一个 Label(variation=TooltipLabel)。
        Button owner = null;
        foreach (var n in _ribbon.FindChildren("*", "Button", true, false))
        {
            if (n is Button b && !string.IsNullOrEmpty(b.TooltipText))
            {
                owner = b;
                break;
            }
        }
        if (owner == null)
        {
            GD.Print("[TIP] 没找到带 Tooltip 的按钮");
            return;
        }

        var popup = new PopupPanel { ThemeTypeVariation = "TooltipPanel", Visible = false };
        var text = new Label { ThemeTypeVariation = "TooltipLabel", Text = "新建空白地图（打桩）" };
        popup.AddChild(text);
        owner.AddChild(popup);

        var sb = popup.GetThemeStylebox("panel");
        string panel = sb is StyleBoxFlat flat
            ? $"bg={flat.BgColor} border={flat.BorderColor}"
            : sb?.GetType().Name ?? "null";
        var font = text.GetThemeFont("font");
        GD.Print($"[TIP] 弹出层解析: panel {panel} | text={text.GetThemeColor("font_color")} | font={font?.GetFontName()} 有中文={font?.HasChar('新')}");

        // 同样的类型链画一份在视口上，方便截图核对观感
        var visual = new PanelContainer { ThemeTypeVariation = "TooltipPanel", Position = new Vector2(430, 250) };
        AddChild(visual);
        visual.AddChild(new Label { ThemeTypeVariation = "TooltipLabel", Text = "新建空白地图（打桩）" });
    }

    // ------------------------------------------------------------------ 布局

    private void BuildUi()
    {
        var bg = new ColorRect { Color = UiTheme.WindowBg, MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 0);
        AddChild(root);

        _ribbon = new Ribbon();
        root.AddChild(_ribbon);

        var mainArea = new HBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        mainArea.AddThemeConstantOverride("separation", 0);
        root.AddChild(mainArea);

        var viewWrap = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        mainArea.AddChild(viewWrap);

        _view = new ViewportPanel();
        _view.SetAnchorsPreset(LayoutPreset.FullRect);
        _view.CursorMoved += OnCursorMoved;
        viewWrap.AddChild(_view);

        _minimap = new MinimapPanel();
        _minimap.SetAnchorsPreset(LayoutPreset.TopLeft);
        _minimap.Position = new Vector2(10, 10);
        viewWrap.AddChild(_minimap);

        BuildStatusBar(root);
        BuildToast(viewWrap);

    }

    /// <summary>HiveWE 0.6 没有状态栏；想用坐标/FPS 时把这个改成 true。</summary>
    private static readonly bool ShowStatusBar = false;

    private void BuildStatusBar(Control parent)
    {
        if (!ShowStatusBar)
        {
            return;
        }
        var bar = new PanelContainer { CustomMinimumSize = new Vector2(0, 26) };
        var sb = UiTheme.Box(UiTheme.PanelAlt, UiTheme.Border, 0);
        sb.ContentMarginLeft = 8;
        sb.ContentMarginRight = 8;
        sb.ContentMarginTop = 2;
        sb.ContentMarginBottom = 2;
        bar.AddThemeStyleboxOverride("panel", sb);

        var row = W.Row(14);
        _lblMap = W.L("地图：未打开（打桩）", 11, UiTheme.TextDim);
        _lblBrush = W.L("笔刷：圆形 · 5", 11, UiTheme.TextDim);
        _lblCoord = W.L("坐标：—", 11, UiTheme.TextDim);
        _lblHint = W.L("就绪", 11, Color.FromHtml("3d7a35"));
        _lblFps = W.L("60 FPS", 11, UiTheme.TextDim);

        row.AddChild(_lblMap);
        row.AddChild(W.Dim("│", 11));
        row.AddChild(_lblBrush);
        row.AddChild(W.Dim("│", 11));
        row.AddChild(_lblCoord);
        row.AddChild(W.Dim("│", 11));
        row.AddChild(_lblHint);
        row.AddChild(W.Spacer());
        row.AddChild(_lblFps);

        bar.AddChild(row);
        parent.AddChild(bar);
    }

    private void BuildToast(Control parent)
    {
        _toast = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        var sb = UiTheme.Box(Color.FromHtml("fffef7"), UiTheme.BorderLight, 4);
        sb.ContentMarginLeft = 14;
        sb.ContentMarginRight = 14;
        sb.ContentMarginTop = 7;
        sb.ContentMarginBottom = 7;
        _toast.AddThemeStyleboxOverride("panel", sb);

        _toastLabel = W.L("", 12);
        _toast.AddChild(_toastLabel);
        _toast.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _toast.GrowHorizontal = GrowDirection.Both;
        _toast.GrowVertical = GrowDirection.Begin;
        _toast.OffsetBottom = -28;
        _toast.OffsetTop = -60;
        parent.AddChild(_toast);
    }

    // ------------------------------------------------------------------ 调色板

    /// <summary>调色板尺寸取 HiveWE 0.6 的 .ui 设计尺寸。</summary>
    private static (string Title, Vector2I Size, System.Func<Control> Body, System.Func<Control> Strip) PaletteDef(string id)
    {
        return id switch
        {
            "terrain" => ("地形", new Vector2I(284, 592), Palettes.BuildTerrain, Palettes.BuildTerrainRibbon),
            "doodads" => ("装饰物", new Vector2I(244, 560), Palettes.BuildDoodads, Palettes.BuildDoodadRibbon),
            "pathing" => ("寻路", new Vector2I(242, 265), Palettes.BuildPathing, () => null),
            "units" => ("单位", new Vector2I(300, 520), Palettes.BuildUnits, () => null),
            "regions" => ("区域", new Vector2I(300, 460), Palettes.BuildRegions, () => null),
            _ => (id, new Vector2I(280, 400), () => null, () => null),
        };
    }

    private void OpenPalette(string id)
    {
        if (_paletteId == id)
        {
            ClosePalette();
            return;
        }

        ClosePalette();

        var def = PaletteDef(id);
        var body = def.Body();
        if (body == null)
        {
            return;
        }

        _paletteId = id;

        var win = new Window
        {
            Title = def.Title + " Palette",
            Size = def.Size,
            Transient = true,
            Exclusive = false,
            Theme = UiTheme.Build(),
        };
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        var sb = UiTheme.Box(UiTheme.PanelBg, UiTheme.Border, 0);
        sb.ContentMarginLeft = 6;
        sb.ContentMarginRight = 6;
        sb.ContentMarginTop = 6;
        sb.ContentMarginBottom = 6;
        panel.AddThemeStyleboxOverride("panel", sb);
        panel.AddChild(body);
        win.AddChild(panel);
        win.CloseRequested += ClosePalette;

        AddChild(win);

        // 0.6 的摆法：贴住主窗口右侧，视口顶部往下 29
        var main = GetWindow();
        win.Position = new Vector2I(
            main.Position.X + main.Size.X - win.Size.X - 10,
            main.Position.Y + 116 + 29);

        _paletteWindow = win;

        var strip = def.Strip();
        if (strip != null && !_ribbon.HasPaletteTab(def.Title))
        {
            _ribbon.AddPaletteTab(def.Title, strip);
        }
    }

    private void ClosePalette()
    {
        if (_paletteId == null)
        {
            return;
        }

        _ribbon.RemovePaletteTab(PaletteDef(_paletteId).Title);
        _paletteId = null;

        if (_paletteWindow != null)
        {
            var win = _paletteWindow;
            _paletteWindow = null;
            RemoveChild(win);
            win.QueueFree();
        }
    }

    // ------------------------------------------------------------------ 窗口

    private void OpenWindow(string id)
    {
        CloseWindow();

        Window host;
        switch (id)
        {
            case "mapinfo":
            {
                var body = Windows.MapInfo();
                host = Windows.Host("地图信息", new Vector2I(720, 560), body,
                    Windows.Footer(() => { Stub.Fire("mapinfo.ok"); CloseWindow(); }, CloseWindow), CloseWindow);
                break;
            }
            case "trigger":
            {
                var body = Windows.TriggerEditor();
                host = Windows.Host("触发器编辑器", new Vector2I(1000, 640), body, null, CloseWindow, modal: false);
                break;
            }
            case "import":
            {
                var body = Windows.ImportManager();
                host = Windows.Host("导入管理器", new Vector2I(900, 600), body, null, CloseWindow, modal: false);
                break;
            }
            case "settings":
            {
                var body = Windows.Settings();
                host = Windows.Host("设置", new Vector2I(540, 360), body,
                    Windows.Footer(() => { Stub.Fire("settings.ok"); CloseWindow(); }, CloseWindow), CloseWindow);
                break;
            }
            case "tilesetter":
            {
                var body = Windows.TileSetter();
                host = Windows.Host("修改地形集", new Vector2I(595, 290), body,
                    Windows.Footer(() => { Stub.Fire("tilesetter.ok"); CloseWindow(); }, CloseWindow), CloseWindow);
                break;
            }
            case "tilepather":
            {
                var body = Windows.TilePather();
                host = Windows.Host("瓦片寻路", new Vector2I(590, 200), body,
                    Windows.Footer(() => { Stub.Fire("tilepather.ok"); CloseWindow(); }, CloseWindow), CloseWindow);
                break;
            }
            case "tilepicker":
            {
                var body = Windows.TilePicker();
                host = Windows.Host("替换瓦片", new Vector2I(595, 225), body,
                    Windows.Footer(() => { Stub.Fire("tilepicker.ok"); CloseWindow(); }, CloseWindow), CloseWindow);
                break;
            }
            case "objecteditor":
            {
                var body = Windows.ObjectEditor();
                host = Windows.Host("对象编辑器（规划中）", new Vector2I(940, 560), body, null, CloseWindow, modal: false);
                break;
            }
            case "players":
            {
                var body = Windows.Players();
                host = Windows.Host("玩家与队伍（规划中）", new Vector2I(760, 540), body, null, CloseWindow, modal: false);
                break;
            }
            case "model":
            {
                var body = Windows.ModelPreview();
                host = Windows.Host("模型预览（规划中）", new Vector2I(860, 620), body, null, CloseWindow, modal: false);
                break;
            }
            default:
                return;
        }

        AddChild(host);
        CenterOnMainWindow(host);
        _currentWindow = host;

        if (OS.GetEnvironment("WE_LAYOUT") == "1")
        {
            CallDeferred(nameof(DumpLayout));
        }
    }

    /// <summary>把子窗口摆到主窗口中间；摆好之后用户可以随便拖走。</summary>
    private void CenterOnMainWindow(Window win)
    {
        var main = GetWindow();
        var offset = (main.Size - win.Size) / 2;
        win.Position = main.Position + offset;
    }

    /// <summary>调试用：打印当前窗口里各层容器尺寸（WE_LAYOUT=1）。</summary>
    private void DumpLayout()
    {
        if (_currentWindow == null)
        {
            return;
        }
        GD.Print($"[LAYOUT] window={_currentWindow.Size}");
        foreach (var n in _currentWindow.FindChildren("*", "Control", true, false))
        {
            if (n is Control c && (c is Container || c is TabContainer || c.Name == "MapInfoTabs"))
            {
                GD.Print($"[LAYOUT] {c.GetType().Name,-16} name={c.Name,-14} size={c.Size} min={c.GetCombinedMinimumSize()} parent={c.GetParent()?.GetType().Name}");
            }
        }
    }

    private void CloseWindow()
    {
        if (_currentWindow == null)
        {
            return;
        }
        var win = _currentWindow;
        _currentWindow = null;
        RemoveChild(win);
        win.QueueFree();
    }

    // ------------------------------------------------------------------ 打桩路由

    private void OnStubFired(string id)
    {
        switch (id)
        {
            case "ui.terrain":
                OpenPalette("terrain");
                return;
            case "ui.doodads":
                OpenPalette("doodads");
                return;
            case "ui.pathing":
                OpenPalette("pathing");
                return;
            case "ui.units":
                OpenPalette("units");
                return;
            case "ui.regions":
                OpenPalette("regions");
                return;
            case "ui.mapinfo":
                OpenWindow("mapinfo");
                return;
            case "ui.trigger":
                OpenWindow("trigger");
                return;
            case "ui.import":
                OpenWindow("import");
                return;
            case "ui.settings":
                OpenWindow("settings");
                return;
            case "ui.tilesetter":
                OpenWindow("tilesetter");
                return;
            case "ui.tilepather":
                OpenWindow("tilepather");
                return;
            case "ui.tilepicker":
                OpenWindow("tilepicker");
                return;
            case "ui.objecteditor":
                OpenWindow("objecteditor");
                return;
            case "ui.players":
                OpenWindow("players");
                return;
            case "ui.model":
                OpenWindow("model");
                return;
            case "file.exit":
                GetTree().Quit();
                return;
            case "view.reset_camera":
                _view.ResetCamera();
                ShowToast("相机已重置");
                return;
            case "view.switch_camera":
                ShowToast("切换相机：打桩（当前只有轨道相机）");
                return;
        }

        ShowToast($"[打桩] {id}");
    }

    private void OnCursorMoved(Vector2 cell, float height)
    {
        if (_lblCoord != null)
        {
            _lblCoord.Text = $"坐标：{cell.X:0.0}, {cell.Y:0.0}  高度 {height:0.0}";
        }
    }

    private void ShowToast(string message)
    {
        if (_toast == null)
        {
            return;
        }
        _toastLabel.Text = message;
        _toast.Visible = true;
        _toastLeft = 2.4;
    }

    // ------------------------------------------------------------------ 每帧

    public override void _Process(double delta)
    {
        if (_toastLeft > 0)
        {
            _toastLeft -= delta;
            if (_toastLeft <= 0)
            {
                _toast.Visible = false;
            }
        }

        _fpsTimer += delta;
        _fpsFrames++;
        if (_fpsTimer >= 0.5)
        {
            if (_lblFps != null)
            {
                _lblFps.Text = $"{_fpsFrames / _fpsTimer:0} FPS";
            }
            _fpsTimer = 0;
            _fpsFrames = 0;
        }

        if (_shotPath != null && ++_frames >= _shotFrames)
        {
            var img = GetViewport().GetTexture().GetImage();
            img.SavePng(_shotPath);
            GD.Print($"[SHOT] {_shotPath} {img.GetWidth()}x{img.GetHeight()}");

            if (_paletteWindow != null)
            {
                var pal = _paletteWindow.GetTexture().GetImage();
                string palPath = _shotPath.Replace(".png", "-palette.png");
                pal.SavePng(palPath);
                GD.Print($"[SHOT] {palPath} {pal.GetWidth()}x{pal.GetHeight()} at {_paletteWindow.Position}");
            }

            // 子窗口是独立系统窗口，主窗口截图拍不到它，单独存一张
            if (_currentWindow != null)
            {
                var sub = _currentWindow.GetTexture().GetImage();
                string subPath = _shotPath.Replace(".png", "-window.png");
                sub.SavePng(subPath);
                GD.Print($"[SHOT] {subPath} {sub.GetWidth()}x{sub.GetHeight()} at {_currentWindow.Position}");
            }

            GetTree().Quit();
        }
    }

    /// <summary>逐页签切过去并打印 Ribbon 高度，用来验证切页签不会跳。</summary>
    private bool ProbeRibbonHeight()
    {
        _probeFrame++;
        int count = _ribbon.GetTabCount();

        if (_probeFrame % 4 == 0 && _probeTab < count)
        {
            _ribbon.CurrentTab = _probeTab;
            return false;
        }

        if (_probeFrame % 4 == 2 && _probeTab < count)
        {
            GD.Print($"[TABS] {_probeTab} {_ribbon.GetTabTitle(_probeTab)} ribbonHeight={_ribbon.Size.Y} contentHeight={((Control)_ribbon.GetChild(_probeTab)).Size.Y}");
            _probeTab++;
            if (_probeTab >= count)
            {
                GD.Print("[TABS] done");
                GetTree().Quit();
                return true;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------ 输入

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey k || !k.Pressed || k.Echo)
        {
            return;
        }

        bool ctrl = k.CtrlPressed;
        bool shift = k.ShiftPressed;

        switch (k.Keycode)
        {
            case Key.O when ctrl:
                Stub.Fire("file.open_folder");
                break;
            case Key.I when ctrl:
                Stub.Fire("file.open_mpq");
                break;
            case Key.S when ctrl && shift:
                Stub.Fire("file.save_as");
                break;
            case Key.S when ctrl:
                Stub.Fire("file.save");
                break;
            case Key.T when ctrl:
                Stub.Fire("file.test");
                break;
            case Key.Z when ctrl:
                Stub.Fire("edit.undo");
                break;
            case Key.Y when ctrl:
                Stub.Fire("edit.redo");
                break;
            case Key.F1:
                Stub.Fire("view.switch_camera");
                break;
            case Key.F3:
                Stub.Fire("view.debug");
                break;
            case Key.F5:
                Stub.Fire("editor.reload_theme");
                break;
            case Key.Escape:
                if (_currentWindow != null)
                {
                    CloseWindow();
                }
                else
                {
                    ClosePalette();
                }
                break;
        }
    }

    // ------------------------------------------------------------------ 命令行

    private void ParseCommandLine()
    {
        var args = new System.Collections.Generic.List<string>();
        args.AddRange(OS.GetCmdlineUserArgs());
        args.AddRange(OS.GetCmdlineArgs());

        string palette = null;
        string window = null;
        bool smoke = false;

        foreach (var a in args)
        {
            if (a.StartsWith("--shot="))
            {
                _shotPath = a.Substring("--shot=".Length);
            }
            else if (a.StartsWith("--frames=") && int.TryParse(a.Substring("--frames=".Length), out int f))
            {
                _shotFrames = f;
            }
            else if (a.StartsWith("--palette="))
            {
                palette = a.Substring("--palette=".Length);
            }
            else if (a.StartsWith("--window="))
            {
                window = a.Substring("--window=".Length);
            }
            else if (a == "--smoke")
            {
                smoke = true;
            }
            else if (a == "--camera-test")
            {
                _cameraTest = true;
            }
            else if (a == "--tooltip-probe")
            {
                _tooltipProbe = true;
            }
            else if (a == "--check-tabs")
            {
                _checkTabs = true;
            }
            else if (a.StartsWith("--tab=") && int.TryParse(a.Substring("--tab=".Length), out int tab))
            {
                _ribbon.CurrentTab = tab;
            }
        }

        if (palette != null)
        {
            if (palette == "none")
            {
                ClosePalette();
            }
            else
            {
                ClosePalette();
                OpenPalette(palette);
            }
        }

        if (window != null)
        {
            OpenWindow(window);
        }

        if (smoke)
        {
            RunSmokeTest();
        }
    }

    /// <summary>
    /// 冒烟：把每个打桩动作都触发一遍，确认面板/窗口都能建起来、不抛异常。
    /// 只做界面构建，不触碰任何数据。
    /// </summary>
    private void RunSmokeTest()
    {
        string[] ids =
        {
            "file.new", "file.open_folder", "file.open_mpq", "file.save", "file.save_as",
            "file.export", "file.test", "ui.mapinfo", "ui.settings",
            "ui.terrain", "ui.terrain", "ui.doodads", "ui.doodads", "ui.pathing", "ui.pathing",
            "ui.units", "ui.units", "ui.regions", "ui.regions",
            "ui.trigger", "ui.import", "ui.tilesetter", "ui.tilepather", "ui.tilepicker",
            "ui.objecteditor", "ui.players", "ui.model",
            "edit.undo", "edit.redo", "view.reset_camera", "view.switch_camera",
            "terrain.cliff.压平", "terrain.deform.抬升", "doodad.select", "region.add",
        };

        int ok = 0;
        foreach (var id in ids)
        {
            OnStubFired(id);
            ok++;
        }
        GD.Print($"[SMOKE] {ok}/{ids.Length} actions ok, palette={_paletteId ?? "none"}, window={_currentWindow?.Title ?? "none"}");
        GetTree().Quit();
    }
}
