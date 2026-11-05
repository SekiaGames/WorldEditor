using Godot;
using System;

namespace WorldEdit;

/// <summary>
/// 中央 3D 视口。当前只是占位：一块按公式起伏的地形、一片水、一个跟随鼠标的笔刷环。
/// 真正的 W3E 地形要等格式层落地后替换 <see cref="BuildTerrain"/> 的数据来源。
/// 相机操作对齐官方 WE：右键拖动平移（上下与左右反向），Ctrl + 右键绕视觉中心旋转，
/// Shift + 右键沿观察方向升降，滚轮缩放。
/// </summary>
public partial class ViewportPanel : SubViewportContainer
{
    public const int Cells = 48;
    public const float CellSize = 128f;
    public const float WaterLevel = 14f;

    /// <summary>鼠标在地形上的位置（格坐标 + 高度），供状态栏显示。</summary>
    public event Action<Vector2, float> CursorMoved;

    private SubViewport _vp;
    private Camera3D _cam;
    private Node3D _ring;
    private TorusMesh _ringMesh;
    private StandardMaterial3D _ringMat;

    private float _yaw = 35f;
    private float _pitch = -38f;
    private float _dist = 2900f;
    private Vector3 _target = new Vector3(Cells * CellSize * 0.5f, 30f, Cells * CellSize * 0.5f);
    private float _brushSize = 5f;

    public ViewportPanel()
    {
        Stretch = true;
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        MouseFilter = Control.MouseFilterEnum.Stop;
        ClipContents = true;
    }

    public override void _Ready()
    {
        _vp = new SubViewport
        {
            Size = new Vector2I(1280, 720),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Msaa4X,
        };
        AddChild(_vp);

        BuildEnvironment();
        BuildTerrain();
        BuildWater();
        BuildRing();
        UpdateCamera();
    }

    public static float HeightAt(float x, float z)
    {
        float h = 26f * Mathf.Sin(x * 0.0016f) * Mathf.Cos(z * 0.0014f)
                  + 13f * Mathf.Sin((x + z) * 0.0027f)
                  + 8f * Mathf.Cos(x * 0.0043f - z * 0.0037f);
        return h + 30f;
    }

    private void BuildEnvironment()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = Color.FromHtml("1b2026"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Color.FromHtml("9fb4c8"),
            AmbientLightEnergy = 0.45f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            FogEnabled = true,
            FogLightColor = Color.FromHtml("232a31"),
            FogDensity = 0.00035f,
        };
        var we = new WorldEnvironment { Environment = env };
        _vp.AddChild(we);

        var sun = new DirectionalLight3D
        {
            LightEnergy = 1.25f,
            LightColor = Color.FromHtml("ffe9c8"),
            RotationDegrees = new Vector3(-51, -38, 0),
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 4000f,
        };
        _vp.AddChild(sun);
    }

    private void BuildTerrain()
    {
        var shader = GD.Load<Shader>("res://assets/shaders/terrain_mock.gdshader");
        var mat = new ShaderMaterial { Shader = shader };

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        for (int z = 0; z < Cells; z++)
        {
            for (int x = 0; x < Cells; x++)
            {
                Vector3 p00 = new Vector3(x * CellSize, HeightAt(x * CellSize, z * CellSize), z * CellSize);
                Vector3 p10 = new Vector3((x + 1) * CellSize, HeightAt((x + 1) * CellSize, z * CellSize), z * CellSize);
                Vector3 p01 = new Vector3(x * CellSize, HeightAt(x * CellSize, (z + 1) * CellSize), (z + 1) * CellSize);
                Vector3 p11 = new Vector3((x + 1) * CellSize, HeightAt((x + 1) * CellSize, (z + 1) * CellSize), (z + 1) * CellSize);

                st.AddVertex(p00);
                st.AddVertex(p01);
                st.AddVertex(p11);
                st.AddVertex(p00);
                st.AddVertex(p11);
                st.AddVertex(p10);
            }
        }

        st.GenerateNormals();
        var mesh = st.Commit();

        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat, Name = "MockTerrain" };
        _vp.AddChild(mi);
    }

    private void BuildWater()
    {
        var shader = GD.Load<Shader>("res://assets/shaders/water_mock.gdshader");
        var mat = new ShaderMaterial { Shader = shader };
        var plane = new PlaneMesh
        {
            Size = new Vector2(Cells * CellSize * 1.6f, Cells * CellSize * 1.6f),
            SubdivideWidth = 1,
            SubdivideDepth = 1,
        };
        var mi = new MeshInstance3D
        {
            Mesh = plane,
            MaterialOverride = mat,
            Position = new Vector3(Cells * CellSize * 0.5f, WaterLevel, Cells * CellSize * 0.5f),
            Name = "MockWater",
        };
        _vp.AddChild(mi);
    }

    private void BuildRing()
    {
        _ringMesh = new TorusMesh
        {
            InnerRadius = 1f,
            OuterRadius = 1.12f,
            Rings = 48,
            RingSegments = 6,
        };
        _ringMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1f, 0.83f, 0.25f, 0.95f),
            NoDepthTest = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var mi = new MeshInstance3D
        {
            Mesh = _ringMesh,
            MaterialOverride = _ringMat,
            Name = "BrushRing",
            RotationDegrees = new Vector3(-90, 0, 0),
        };
        _ring = new Node3D { Name = "BrushCursor" };
        _ring.AddChild(mi);
        _ring.Position = new Vector3(Cells * CellSize * 0.5f, WaterLevel + 1f, Cells * CellSize * 0.5f);
        _ring.Visible = false;
        _vp.AddChild(_ring);
        SetBrushSize(5f);
    }

    public void SetBrushSize(float cells)
    {
        _brushSize = Mathf.Max(1f, cells);
        if (_ringMesh == null)
        {
            return;
        }
        float radius = _brushSize * 0.5f * CellSize;
        _ringMesh.InnerRadius = radius * 0.94f;
        _ringMesh.OuterRadius = radius;
    }

    public void SetRingVisible(bool visible)
    {
        if (_ring != null)
        {
            _ring.Visible = visible;
        }
    }

    private void UpdateCamera()
    {
        float yaw = Mathf.DegToRad(_yaw);
        float pitch = Mathf.DegToRad(_pitch);
        var offset = new Vector3(
            Mathf.Cos(pitch) * Mathf.Sin(yaw),
            -Mathf.Sin(pitch),
            Mathf.Cos(pitch) * Mathf.Cos(yaw)) * _dist;

        _cam ??= new Camera3D { Current = true, Fov = 55f, Near = 1f, Far = 12000f };
        if (_cam.GetParent() == null)
        {
            _vp.AddChild(_cam);
        }
        _cam.Position = _target + offset;
        _cam.LookAt(_target, Vector3.Up);
    }

    /// <summary>自检：验证平移的换算与方向、以及旋转不动目标点（--camera-test）。</summary>
    public void RunCameraSelfTest()
    {
        float wpp = WorldPerPixel();
        var before = _target;
        float yaw0 = _yaw;

        PanBy(new Vector2(120f, 0f));
        var pan = _target - before;
        float yawRad = Mathf.DegToRad(yaw0);
        var right = new Vector3(Mathf.Cos(yawRad), 0, -Mathf.Sin(yawRad));
        float dot = pan.X * right.X + pan.Z * right.Z;

        GD.Print($"[CAM] worldPerPixel={wpp:0.00} panLen={pan.Length():0.00} expect={120f * wpp:0.00} dotRight={dot:0.00} (应为负)");

        var afterPan = _target;
        OrbitBy(new Vector2(90f, 0f));
        GD.Print($"[CAM] yaw {yaw0:0.0} -> {_yaw:0.0}, targetMovedByOrbit={(_target - afterPan).Length():0.000} (应为 0)");

        // 竖直平移：向下拖 120px，应沿 -forward 移动
        var beforeV = _target;
        PanBy(new Vector2(0f, 120f));
        var panV = _target - beforeV;
        var forward = new Vector3(Mathf.Sin(yawRad), 0, Mathf.Cos(yawRad));
        float dotF = panV.X * forward.X + panV.Z * forward.Z;
        GD.Print($"[CAM] panDown120 len={panV.Length():0.00} dotForward={dotF:0.00} (应为负)");

        // Shift 升降：向上拖 100px，目标点应升高
        var y0 = _target.Y;
        var cam0 = _cam.Position.Y;
        HeightBy(new Vector2(0f, -100f));
        UpdateCamera();
        GD.Print($"[CAM] heightUp100 targetY {y0:0.0} -> {_target.Y:0.0}, camY {cam0:0.0} -> {_cam.Position.Y:0.0} (应升高)");
    }

    public void ResetCamera()
    {
        _yaw = 35f;
        _pitch = -38f;
        _dist = 2900f;
        _target = new Vector3(Cells * CellSize * 0.5f, 30f, Cells * CellSize * 0.5f);
        UpdateCamera();
    }

    public override void _Process(double delta)
    {
        var local = GetLocalMousePosition();
        var rect = new Rect2(Vector2.Zero, Size);
        if (!rect.HasPoint(local) || _cam == null)
        {
            if (_ring != null)
            {
                _ring.Visible = false;
            }
            return;
        }

        Vector2 p = local * (_vp.Size / Size);
        Vector3 o = _cam.ProjectRayOrigin(p);
        Vector3 d = _cam.ProjectRayNormal(p);
        if (Mathf.Abs(d.Y) < 0.0001f)
        {
            return;
        }

        float t = (WaterLevel - o.Y) / d.Y;
        if (t <= 0f)
        {
            return;
        }

        Vector3 hit = o + d * t;
        _ring.Visible = true;
        _ring.Position = new Vector3(hit.X, hit.Y + 0.4f, hit.Z);
        CursorMoved?.Invoke(new Vector2(hit.X / CellSize, hit.Z / CellSize), hit.Y);
    }

    /// <summary>屏幕上一像素对应目标平面上的世界距离，保证拖动时地图跟手。</summary>
    private float WorldPerPixel()
    {
        // 布局完成前 SubViewport 的 Size 可能是 1~2，退回到控制器尺寸，避免算出天文数字
        float h = _vp != null && _vp.Size.Y > 8 ? _vp.Size.Y : (Size.Y > 8 ? Size.Y : 720f);
        return 2f * _dist * Mathf.Tan(Mathf.DegToRad(_cam.Fov * 0.5f)) / h;
    }

    /// <summary>拖动地图：目标点沿相机的右/前方向移动，方向与拖动相反，视觉上地图跟着鼠标走。</summary>
    public void PanBy(Vector2 screenDelta)
    {
        float yaw = Mathf.DegToRad(_yaw);
        var right = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));
        var forward = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        float k = WorldPerPixel();
        // 竖直方向与水平方向相反：官方 WE 里往下拖，地图向上走
        _target += (-right * screenDelta.X - forward * screenDelta.Y) * k;
        ClampTarget();
    }

    /// <summary>
    /// Shift + 右键：沿当前观察方向缓慢升降相机。移动方向取视线反方向（相机在上方俯视时即“向上后退”），
    /// 所以升降过程中视角与俯角都不变，只是整体沿轴线滑。
    /// </summary>
    public void HeightBy(Vector2 screenDelta)
    {
        var look = (_target - _cam.Position).Normalized();
        float k = WorldPerPixel() * 0.35f;
        _target += -look * (-screenDelta.Y * k);
        _target = new Vector3(_target.X, Mathf.Clamp(_target.Y, -200f, 1200f), _target.Z);
        ClampTarget();
    }

    /// <summary>绕视觉中心旋转：目标点不动，只改水平角和俯角。</summary>
    public void OrbitBy(Vector2 screenDelta)
    {
        _yaw -= screenDelta.X * 0.25f;
        _pitch = Mathf.Clamp(_pitch - screenDelta.Y * 0.25f, -88f, -6f);
    }

    /// <summary>官方 WE 会把视角限制在地图范围内，这里照做，避免拖到图外面。</summary>
    private void ClampTarget()
    {
        float max = Cells * CellSize;
        _target = new Vector3(
            Mathf.Clamp(_target.X, 0f, max),
            _target.Y,
            Mathf.Clamp(_target.Z, 0f, max));
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed:
                _dist = Mathf.Clamp(_dist * 0.88f, 120f, 4200f);
                UpdateCamera();
                AcceptEvent();
                break;
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed:
                _dist = Mathf.Clamp(_dist * 1.14f, 120f, 4200f);
                UpdateCamera();
                AcceptEvent();
                break;
            // 与官方 WE 一致：右键拖动平移地图，Ctrl + 右键绕视觉中心旋转
            case InputEventMouseMotion mm when (mm.ButtonMask & MouseButtonMask.Right) != 0:
                if (mm.ShiftPressed)
                {
                    HeightBy(mm.Relative);
                }
                else if (mm.CtrlPressed)
                {
                    OrbitBy(mm.Relative);
                }
                else
                {
                    PanBy(mm.Relative);
                }
                UpdateCamera();
                AcceptEvent();
                break;
            case InputEventMouseMotion mm when (mm.ButtonMask & MouseButtonMask.Middle) != 0:
                PanBy(mm.Relative);
                UpdateCamera();
                AcceptEvent();
                break;
        }
    }
}
