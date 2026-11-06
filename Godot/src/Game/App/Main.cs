using Godot;
using System;
using System.IO;
using WorldEdit.Core.Map;

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
    private string _startupMap;
    private string _startupScene;
    private string _pendingWindow;
    private float _startupDistance = -1f;
    private Vector2 _startupFocus = new Vector2(-1f, -1f);
    private Vector3? _startupCamera;
    private float _startupYaw = float.NaN;
    private float _startupPitch = float.NaN;
    private bool _startupMinimap = true;
    private bool _startupLighting = true;
    private float _startupFov;
    private string _reopenPath;
    private MapDocument _currentMap;
    private string _mapDisplayName = "未打开地图";
    private string _lastTitle;
    private bool _checkTabs;
    private int _probeFrame;
    private int _probeTab;
    private string _shotPath;
    private bool _shotView;
    private Vector2I _viewSize;
    private int _shotFrames = 24;

    public override void _Ready()
    {
        // 必须最先做：调试包里资产不在 exe 内，字体、贴图、模型都从挂载后的 res:// 读
        Assets.ExternalAssets.Mount();

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
        _ribbon.MinimapToggled += SetMinimapVisible;
        _ribbon.LightingToggled += _view.SetLighting;
        _view.CameraChanged += UpdateWindowTitle;
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
                Windows.MapInfoView view = Windows.MapInfo(_currentMap);
                host = Windows.Host("地图信息", new Vector2I(720, 560), view.Root,
                    Windows.Footer(() => { CommitMapName(view); CloseWindow(); }, CloseWindow), CloseWindow);
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
                var body = Windows.Settings(_view);
                host = Windows.Host("设置", new Vector2I(560, 470), body,
                    Windows.Footer(() => { ShowToast("设置已应用"); CloseWindow(); }, CloseWindow), CloseWindow);
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
            case "file.open":
                ShowOpenMapDialog();
                return;
            case "file.save":
                SaveCurrentMap(false);
                return;
            case "file.save_as":
                SaveCurrentMap(true);
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

    /// <summary>打开地图：读进内存，重建视口地形，标题栏带上地图名。</summary>
    private void OpenMapFile(string path)
    {
        try
        {
            // --map= 既可以是 .w3m/.w3x，也可以是解包出来的目录（方便和 HiveWE 打开同一份数据）
            MapDocument document = Directory.Exists(path) ? MapDocument.LoadDirectory(path) : MapDocument.Load(path);
            MapDocument previous = _currentMap;
            _currentMap = document;
            _view.LoadMap(document);
            // 新地图已经接手，旧文档里的原始字节可以放掉了
            previous?.Release();
            if (document.Terrain != null)
            {
                _minimap.LoadMap(document.Terrain);
            }

            string name = document.MapName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = Path.GetFileNameWithoutExtension(path);
            }
            ApplyMapName(name);

            GD.Print($"[MAP] 打开 {path} 名称='{name}' 文件数={document.Entries.Count} 尺寸={document.Terrain?.Width}x{document.Terrain?.Height}");
            foreach (string warning in document.Warnings)
            {
                GD.PushWarning($"[MAP] 条目未能载入 {warning}");
            }
            ShowToast($"已打开 {name}");

            ApplyStartupView();
        }
        catch (Exception e)
        {
            GD.PushError($"[MAP] 打开失败 {path}: {e.GetType().Name} {e.Message}");
            ShowToast($"打开失败：{e.Message}");
        }
    }

    /// <summary>打开已经导出的场景（.tscn），用 --cam/--yaw/--pitch 复现相机。</summary>
    private void OpenSceneFile(string path)
    {
        _view.LoadScene(path);
        ApplyMapName(Path.GetFileNameWithoutExtension(path));
        ApplyStartupView();
    }

    /// <summary>把 --cam / --yaw / --pitch / --dist 这些启动参数应用到相机上。</summary>
    private void ApplyStartupView()
    {
        if (!float.IsNaN(_startupYaw) || !float.IsNaN(_startupPitch))
        {
            _view.SetOrbit(
                float.IsNaN(_startupYaw) ? _view.Yaw : _startupYaw,
                float.IsNaN(_startupPitch) ? _view.Pitch : _startupPitch);
        }

        if (_startupCamera.HasValue)
        {
            // 标题栏里的相机坐标可以直接拿来复现，比换算 --focus 省事
            _view.SetCameraPosition(_startupCamera.Value);
        }
        else if (_startupFocus.X >= 0f)
        {
            _view.FocusOn(_startupFocus);
        }
        if (_startupDistance > 0f)
        {
            _view.SetDistance(_startupDistance);
        }

        if (_cameraTest)
        {
            _view.RunCameraSelfTest();
            GetTree().Quit();
        }

        if (_pendingWindow != null)
        {
            string pending = _pendingWindow;
            _pendingWindow = null;
            OpenWindow(pending);
        }
    }

    private void ShowOpenMapDialog()
    {
        var dialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "打开地图",
            Size = new Vector2I(900, 600),
            UseNativeDialog = false,
            Exclusive = false,
            Theme = UiTheme.Build(),
        };
        dialog.AddFilter("*.w3m,*.w3x", "War3 地图");

        string documents = OS.GetSystemDir(OS.SystemDir.Documents);
        string maps = Path.Combine(documents, "Warcraft III", "Maps");
        dialog.CurrentDir = Directory.Exists(maps) ? maps : documents;

        dialog.FileSelected += path =>
        {
            OpenMapFile(path);
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    /// <summary>保存：saveAs=false 写回原文件，true 弹对话框选新路径。</summary>
    private void SaveCurrentMap(bool saveAs)
    {
        if (_currentMap == null)
        {
            ShowToast("还没有打开地图");
            return;
        }

        if (saveAs)
        {
            var dialog = new FileDialog
            {
                FileMode = FileDialog.FileModeEnum.SaveFile,
                Access = FileDialog.AccessEnum.Filesystem,
                Title = "地图另存为",
                Size = new Vector2I(900, 600),
                UseNativeDialog = false,
                Exclusive = false,
                Theme = UiTheme.Build(),
                CurrentFile = Path.GetFileName(_currentMap.SourcePath),
            };
            dialog.AddFilter("*.w3m,*.w3x", "War3 地图");
            dialog.FileSelected += path =>
            {
                WriteMap(path);
                dialog.QueueFree();
            };
            dialog.Canceled += () => dialog.QueueFree();
            AddChild(dialog);
            dialog.PopupCentered();
            return;
        }

        WriteMap(_currentMap.SourcePath);
    }

    private void WriteMap(string path)
    {
        try
        {
            _currentMap.Save(path);
            ShowToast($"已保存到 {Path.GetFileName(path)}");
            GD.Print($"[MAP] 保存 {path}");
        }
        catch (Exception e)
        {
            GD.PushError($"[MAP] 保存失败 {path}: {e.Message}");
            ShowToast($"保存失败：{e.Message}");
        }
    }

    /// <summary>把地图信息窗口里改的名字写回 wts，并同步标题栏。</summary>
    private void CommitMapName(Windows.MapInfoView view)
    {
        if (_currentMap == null || view?.Name == null)
        {
            return;
        }

        string name = view.Name.Text.Trim();
        if (string.IsNullOrEmpty(name) || name == _currentMap.MapName)
        {
            return;
        }

        try
        {
            _currentMap.SetMapName(name);
            ApplyMapName(name);
            ShowToast($"地图已改名为 {name}（保存后生效）");
            GD.Print($"[MAP] 改名 -> {name}");
        }
        catch (Exception e)
        {
            GD.PushError($"[MAP] 改名失败: {e.Message}");
            ShowToast($"改名失败：{e.Message}");
        }
    }

    private void ApplyMapName(string name)
    {
        _mapDisplayName = string.IsNullOrEmpty(name) ? "未打开地图" : name;
        if (_lblMap != null)
        {
            _lblMap.Text = _mapDisplayName;
        }
        UpdateWindowTitle();
    }

    /// <summary>
    /// 标题栏 = "WorldEditor - 地图名 - 相机坐标：(x, y, z)"。
    /// 相机一动就刷新，方便直接读窗口标题和截图对照（相机位置是世界坐标，和 --dist 等参数同一套单位）。
    /// </summary>
    private void UpdateWindowTitle()
    {
        if (_view == null)
        {
            return;
        }
        Vector3 cam = _view.CameraPosition;
        string title = $"WorldEditor - {_mapDisplayName} - 相机坐标：({cam.X:0}, {cam.Y:0}, {cam.Z:0})";
        if (title == _lastTitle)
        {
            return;
        }
        _lastTitle = title;
        GetWindow().Title = title;
    }

    /// <summary>View → 小地图。小地图是浮在视口左上角的独立控件，隐藏后视口自动铺满。</summary>
    private void SetMinimapVisible(bool visible)
    {
        if (_minimap != null)
        {
            _minimap.Visible = visible;
        }
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
            // --shot-view 时只存 3D 视口，方便和 HiveWE 的视口截图逐像素比
            var img = _shotView ? _view.CaptureView() : GetViewport().GetTexture().GetImage();
            img.SavePng(_shotPath);
            GD.Print($"[SHOT] {_shotPath} {img.GetWidth()}x{img.GetHeight()} title='{GetWindow().Title}'");

            if (_paletteWindow != null)
            {
                var pal = _paletteWindow.GetTexture().GetImage();
                string palPath = _shotPath.Replace(".png", "-palette.png");
                pal.SavePng(palPath);
                GD.Print($"[SHOT] {palPath} {pal.GetWidth()}x{pal.GetHeight()} at {_paletteWindow.Position}");
            }

            // 文件对话框之类的 Window 不经过 Windows.Host，也单独存一张
            int extra = 0;
            foreach (Node child in GetChildren())
            {
                if (child is Window w && w.Visible && w != _paletteWindow && w != _currentWindow)
                {
                    var shot = w.GetTexture().GetImage();
                    string path = _shotPath.Replace(".png", $"-win{extra}.png");
                    shot.SavePng(path);
                    GD.Print($"[SHOT] {path} {shot.GetWidth()}x{shot.GetHeight()} title='{w.Title}'");
                    extra++;
                }
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
                Stub.Fire("file.open");
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
            else if (a == "--shot-view")
            {
                // 只存 3D 视口，用来和 HiveWE 的视口截图逐像素比
                _shotView = true;
            }
            else if (a.StartsWith("--minimap-dump="))
            {
                // 调试：把按真实地图生成的小地图存成 PNG，方便和俯视图比对方向
                string[] parts = a.Substring("--minimap-dump=".Length).Split('|');
                if (parts.Length == 2)
                {
                    MapDocument doc = MapDocument.Load(parts[0]);
                    Texture2D tex = MinimapPanel.BuildFromTerrain(doc.Terrain, new WorldEdit.Assets.TerrainTileSet());
                    tex.GetImage().SavePng(parts[1]);
                    GD.Print($"[MINIMAP] {parts[1]} {tex.GetWidth()}x{tex.GetHeight()}");
                    GetTree().Quit();
                    return;
                }
            }
            else if (a.StartsWith("--yaw=") && float.TryParse(a.Substring("--yaw=".Length), out float yaw))
            {
                _startupYaw = yaw;
            }
            else if (a.StartsWith("--pitch=") && float.TryParse(a.Substring("--pitch=".Length), out float pitch))
            {
                _startupPitch = pitch;
            }
            else if (a == "--no-minimap")
            {
                _startupMinimap = false;
            }
            else if (a == "--no-lighting")
            {
                _startupLighting = false;
            }
            else if (a.StartsWith("--cam="))
            {
                string[] xyz = a.Substring("--cam=".Length).Split(',');
                if (xyz.Length == 3
                    && float.TryParse(xyz[0], out float cx)
                    && float.TryParse(xyz[1], out float cy)
                    && float.TryParse(xyz[2], out float cz))
                {
                    _startupCamera = new Vector3(cx, cy, cz);
                }
            }
            else if (a.StartsWith("--fov=") && float.TryParse(a.Substring("--fov=".Length), out float fov))
            {
                _startupFov = fov;
            }
            else if (a.StartsWith("--view-size="))
            {
                string[] wh = a.Substring("--view-size=".Length).Split('x');
                if (wh.Length == 2 && int.TryParse(wh[0], out int vw) && int.TryParse(wh[1], out int vh))
                {
                    _viewSize = new Vector2I(vw, vh);
                }
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
            else if (a.StartsWith("--focus="))
            {
                string[] xy = a.Substring("--focus=".Length).Split(',');
                if (xy.Length == 2 && float.TryParse(xy[0], out float fx) && float.TryParse(xy[1], out float fz))
                {
                    _startupFocus = new Vector2(fx, fz);
                }
            }
            else if (a.StartsWith("--reopen="))
            {
                // 调试：先打开 --map，再换 --reopen，用来验证「打开新地图会卸掉旧的」
                _reopenPath = a.Substring("--reopen=".Length);
            }
            else if (a.StartsWith("--dist=") && float.TryParse(a.Substring("--dist=".Length), out float dist))
            {
                _startupDistance = dist;
            }
            else if (a.StartsWith("--settings-tab=") && int.TryParse(a.Substring("--settings-tab=".Length), out int settingsTab))
            {
                Windows.SettingsTab = settingsTab;
            }
            else if (a == "--map-dialog")
            {
                CallDeferred(nameof(ShowOpenMapDialog));
            }
            else if (a.StartsWith("--map="))
            {
                _startupMap = a.Substring("--map=".Length);
            }
            else if (a.StartsWith("--export-scene="))
            {
                // --export-scene=地图路径|场景路径：把地形/悬崖/水面存成 .tscn，依赖放同名子目录
                string[] parts = a.Substring("--export-scene=".Length).Split('|');
                if (parts.Length == 2)
                {
                    WorldEdit.Assets.MapSceneExporter.Export(parts[0], parts[1]);
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--scene="))
            {
                _startupScene = a.Substring("--scene=".Length);
            }
            else if (a.StartsWith("--img-convert="))
            {
                string[] parts = a.Substring("--img-convert=".Length).Split('|');
                if (parts.Length == 2)
                {
                    WorldEdit.Core.Tools.AssetCli.ConvertImage(parts[0], parts[1]);
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--blp-dump="))
            {
                string[] parts = a.Substring("--blp-dump=".Length).Split('|');
                if (parts.Length == 2)
                {
                    WorldEdit.Core.Tools.AssetCli.DumpBlp(parts[0], parts[1]);
                }
                GetTree().Quit();
                return;
            }
            else if (a == "--table-check")
            {
                WorldEdit.Core.Tools.AssetCli.DumpTables();
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--game-dir="))
            {
                WorldEdit.Assets.War3Assets.SetGameRoot(a.Substring("--game-dir=".Length));
            }
            else if (a.StartsWith("--mpq-probe="))
            {
                string[] probe = a.Substring("--mpq-probe=".Length).Split('|');
                if (probe.Length == 2)
                {
                    WorldEdit.Core.Tools.AssetCli.ProbeMpq(probe[0], probe[1]);
                }
                GetTree().Quit();
                return;
            }
            else if (a == "--text-check")
            {
                WorldEdit.Core.Tools.AssetCli.DumpTexts();
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--tile-dump="))
            {
                string[] parts = a.Substring("--tile-dump=".Length).Split('|');
                if (parts.Length == 2)
                {
                    WorldEdit.Core.Tools.AssetCli.DumpTile(parts[0], parts[1]);
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--shot-diff="))
            {
                string[] parts = a.Substring("--shot-diff=".Length).Split('|');
                if (parts.Length >= 2)
                {
                    int block = parts.Length > 2 && int.TryParse(parts[2], out int parsed) ? parsed : 64;
                    WorldEdit.Core.Tools.ShotCli.Diff(parts[0], parts[1], block);
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--shot-mirror="))
            {
                string[] parts = a.Substring("--shot-mirror=".Length).Split('|');
                if (parts.Length == 2)
                {
                    WorldEdit.Core.Tools.ShotCli.Mirror(parts[0], parts[1]);
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--shot-water="))
            {
                string[] parts = a.Substring("--shot-water=".Length).Split('|');
                if (parts.Length == 2)
                {
                    WorldEdit.Core.Tools.ShotCli.Water(parts[0], parts[1]);
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--crop="))
            {
                // --crop=输入|输出|x,y,宽,高[|放大倍数]
                string[] parts = a.Substring("--crop=".Length).Split('|');
                if (parts.Length >= 3)
                {
                    string[] box = parts[2].Split(',');
                    if (box.Length == 4)
                    {
                        int scale = parts.Length > 3 && int.TryParse(parts[3], out int parsed) ? parsed : 3;
                        WorldEdit.Core.Tools.ShotCli.Crop(
                            parts[0], parts[1],
                            int.Parse(box[0]), int.Parse(box[1]),
                            int.Parse(box[2]), int.Parse(box[3]), scale);
                    }
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--w3e-probe="))
            {
                // --w3e-probe=路径|ramps   或   --w3e-probe=路径|line|x0,y0,x1,y1[,steps]
                string[] parts = a.Substring("--w3e-probe=".Length).Split('|');
                if (parts.Length >= 2)
                {
                    WorldEdit.Core.Tools.TerraCli.Probe(parts[0], parts[1], parts.Length > 2 ? parts[2] : "");
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--draw-tiles="))
            {
                // --draw-tiles=图|y0,y1[|x0,x1[|w3e[|ex,ey,eh]]]
                string[] parts = a.Substring("--draw-tiles=".Length).Split('|');
                if (parts.Length >= 2)
                {
                    WorldEdit.Core.Tools.TerraCli.DrawTiles(
                        parts[0], parts[1],
                        parts.Length > 2 ? parts[2] : "",
                        parts.Length > 3 ? parts[3] : "",
                        parts.Length > 4 ? parts[4] : "");
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--unit-dump="))
            {
                WorldEdit.Core.Tools.TerraCli.DumpUnits(a.Substring("--unit-dump=".Length));
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--paint-map="))
            {
                // --paint-map=解包目录[|tileset[|悬崖列表]]
                string[] parts = a.Substring("--paint-map=".Length).Split('|');
                WorldEdit.Core.Tools.TerraCli.PaintMap(
                    parts[0],
                    parts.Length > 1 ? parts[1] : "",
                    parts.Length > 2 ? parts[2] : "");
                GetTree().Quit();
                return;
            }
            else if (a == "--table-export" || a.StartsWith("--table-export="))
            {
                string target = a.StartsWith("--table-export=")
                    ? a.Substring("--table-export=".Length)
                    : "";
                WorldEdit.Core.Tools.ConvertCli.ExportTables(target);
                GetTree().Quit();
                return;
            }
            else if (a == "--text-export" || a.StartsWith("--text-export="))
            {
                string target = a.StartsWith("--text-export=")
                    ? a.Substring("--text-export=".Length)
                    : "";
                WorldEdit.Core.Tools.ConvertCli.ExportTexts(target);
                GetTree().Quit();
                return;
            }
            else if (a == "--mdx-export" || a.StartsWith("--mdx-export="))
            {
                // --mdx-export                      源 = 安装目录 MPQ，输出 = assets/converted
                // --mdx-export=输出目录              同上，指定输出
                // --mdx-export=源目录|输出目录        从解包目录读
                string rest = a.StartsWith("--mdx-export=") ? a.Substring("--mdx-export=".Length) : "";
                string[] parts = rest.Split('|');
                string source = parts.Length >= 2 ? parts[0] : "";
                string target = parts.Length >= 2 ? parts[1] : (parts.Length == 1 ? parts[0] : "");
                WorldEdit.Core.Tools.MdxExportCli.Export(source, target);
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--mdx-bundle="))
            {
                // --mdx-bundle=相对路径片段   只重导匹配的模型（如 Units\Creeps\Gnoll\Gnoll），
                // 输出仍是 assets/converted，用于单模型迭代验证
                string only = a.Substring("--mdx-bundle=".Length);
                WorldEdit.Core.Tools.MdxExportCli.Export("", "", only);
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--mdx-probe="))
            {
                WorldEdit.Core.Tools.MdxExportCli.Probe(a.Substring("--mdx-probe=".Length));
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--asset-status="))
            {
                // --asset-status=目录   打印解压产物的版本标记状态，
                // 末行固定 STATE=FRESH / STALE / MISSING / INCOMPLETE，解压脚本据此决定要不要重跑
                WorldEdit.Core.Tools.AssetStamp.PrintStatus(a.Substring("--asset-status=".Length));
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--asset-stamp="))
            {
                // --asset-stamp=目录[|源目录]   整轮解压成功后写版本标记（版本号唯一来源是 AssetStamp.Version）
                string[] stampParts = a.Substring("--asset-stamp=".Length).Split('|');
                WorldEdit.Core.Tools.AssetStamp.Write(
                    stampParts[0], stampParts.Length >= 2 ? stampParts[1] : "");
                GetTree().Quit();
                return;
            }
            else if (a == "--asset-version" || a.StartsWith("--asset-version="))
            {
                // --asset-version             打印 VERSION=N
                // --asset-version=文件        只把数字写进文件，export.bat 用它拼 converted.v<N>.pck
                string target = a.StartsWith("--asset-version=")
                    ? a.Substring("--asset-version=".Length)
                    : null;
                WorldEdit.Core.Tools.AssetStamp.PrintVersion(target);
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--mpq-list="))
            {
                WorldEdit.Core.Tools.MpqCli.List(a.Substring("--mpq-list=".Length));
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--war3-extract="))
            {
                string[] parts = a.Substring("--war3-extract=".Length).Split('|');
                if (parts.Length == 2)
                {
                    WorldEdit.Core.Tools.War3Extract.RunWithDefaults(parts[0], parts[1]);
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--war3-scan="))
            {
                WorldEdit.Core.Tools.War3Scan.Scan(a.Substring("--war3-scan=".Length));
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--war3-coverage="))
            {
                WorldEdit.Core.Tools.War3Cli.Coverage(a.Substring("--war3-coverage=".Length));
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--mpq-stats="))
            {
                WorldEdit.Core.Tools.MpqCli.Stats(a.Substring("--mpq-stats=".Length));
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--image-convert="))
            {
                // --image-convert=源目录|输出目录：把源目录下所有 BLP/TGA/JPG 转成 PNG（保持相对路径）
                string[] parts = a.Substring("--image-convert=".Length).Split('|');
                if (parts.Length == 2)
                {
                    WorldEdit.Assets.ImageBatchConverter.Run(parts[0], parts[1]);
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--mpq-unpack="))
            {
                string[] parts = a.Substring("--mpq-unpack=".Length).Split('|');
                if (parts.Length == 2)
                {
                    WorldEdit.Core.Tools.MpqCli.Unpack(parts[0], parts[1]);
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--map-saveas="))
            {
                string[] parts = a.Substring("--map-saveas=".Length).Split('|');
                if (parts.Length == 3)
                {
                    WorldEdit.Core.Tools.MapCli.SaveAs(parts[0], parts[1], parts[2]);
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--mpq-extract="))
            {
                string[] parts = a.Substring("--mpq-extract=".Length).Split('|');
                if (parts.Length == 3)
                {
                    WorldEdit.Core.Tools.MpqCli.Extract(parts[0], parts[1], parts[2]);
                }
                GetTree().Quit();
                return;
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

        SetMinimapVisible(_startupMinimap);
        _view.SetLighting(_startupLighting);

        if (_startupFov > 0f)
        {
            _view.SetFov(_startupFov);
        }

        // 固定 3D 视口像素尺寸（在加载地图之前设，相机的 aspect 才会按这个尺寸算）
        if (_viewSize.X > 0 && _viewSize.Y > 0)
        {
            _view.SetViewSize(_viewSize);
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

        // 有 --map 时把窗口延后到地图加载之后，否则地图信息会拿到空文档
        if (window != null)
        {
            if (_startupMap != null || _startupScene != null)
            {
                _pendingWindow = window;
            }
            else
            {
                OpenWindow(window);
            }
        }

        if (_startupScene != null)
        {
            CallDeferred(nameof(OpenSceneFile), _startupScene);
        }
        else if (_startupMap != null)
        {
            CallDeferred(nameof(OpenMapFile), _startupMap);
            if (_reopenPath != null)
            {
                string second = _reopenPath;
                _reopenPath = null;
                GetTree().CreateTimer(0.8).Timeout += () => OpenMapFile(second);
            }
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
            "file.new", "file.open", "file.save", "file.save_as", "file.test", "ui.mapinfo", "ui.settings",
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
