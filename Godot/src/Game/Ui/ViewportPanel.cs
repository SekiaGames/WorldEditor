using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Assets;
using WorldEdit.Core.Formats.W3x;
using WorldEdit.Core.Map;

namespace WorldEdit;

/// <summary>
/// 中央 3D 视口。没打开地图时显示一块按公式起伏的占位地形；
/// 打开地图后用 war3map.w3e 的真实角点高度与地表贴图重建网格。
/// 相机操作对齐官方 WE：右键拖动平移（上下与左右反向），Ctrl + 右键绕视觉中心旋转，
/// Shift + 右键沿观察方向升降，滚轮缩放。
/// </summary>
public partial class ViewportPanel : SubViewportContainer
{
    public const int Cells = 48;
    public const float CellSize = 128f;
    public const float WaterLevel = 14f;

    /// <summary>相机能抬到的最高世界单位，设置窗口可调。</summary>
    public float MaxCameraHeight { get; set; } = 12000f;

    /// <summary>相机参数或位置变化时触发：标题栏、设置窗口靠它同步显示。</summary>
    public event Action CameraChanged;

    public float Fov => _fov;
    public float Yaw => _yaw;
    public float Pitch => _pitch;
    public float Distance => _dist;

    /// <summary>相机自身的位置（世界坐标，未镜像前的原始值不含符号翻转）。</summary>
    public Vector3 CameraPosition => _cam?.Position ?? Vector3.Zero;

    /// <summary>视觉中心的格坐标（回到地图坐标，x 已翻回正值）。</summary>
    public Vector2 TargetTile => WorldToMap(_target);

    public float TargetHeight => _target.Y;

    /// <summary>鼠标在地形上的位置（格坐标 + 高度），供状态栏显示。</summary>
    public event Action<Vector2, float> CursorMoved;

    private SubViewport _vp;
    private Camera3D _cam;
    private Node3D _ring;
    private TorusMesh _ringMesh;
    private StandardMaterial3D _ringMat;

    private Node3D _mapRoot;
    private Vector2 _mapSize = new Vector2(Cells, Cells);
    private float _waterY = WaterLevel;
    private W3eTerrain _terrain;
    private ShaderMaterial _waterMaterial;
    private int _waterFrameCount = 1;
    private int _waterFrameRate = 1;
    private float _waterElapsed;
    private GroundTextureArray _groundLayers;
    private List<string> _groundOrder = new();

    /// <summary>
    /// 地形/悬崖材质都吃 lighting_enabled 这个开关，换地图时整批重建，所以按材质列表统一管。
    /// 默认开，和 HiveWE 的 render_lighting 默认值一致。
    /// </summary>
    private readonly List<ShaderMaterial> _shadedMaterials = new();
    private bool _lighting = true;

    /// <summary>
    /// 默认垂直 FOV，和 HiveWE 对齐：它的 Camera.h 里 fov = 70，但 glm::perspective 要的是弧度，
    /// 等于传了 70 弧度，实际垂直视角只有 2*atan(1/tan(35)) ≈ 50.705°。
    /// 这样两边默认截图可以直接叠加比差异，不用再传 --fov。
    /// </summary>
    public const float HiveWeFov = 50.705f;

    private float _fov = HiveWeFov;
    private float _yaw = 35f;
    private float _pitch = -46f;
    private float _dist = 2900f;
    private Vector3 _target = new Vector3(-Cells * CellSize * 0.5f, 30f, Cells * CellSize * 0.5f);
    private float _brushSize = 5f;

    public ViewportPanel()
    {
        Stretch = true;
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        MouseFilter = Control.MouseFilterEnum.Stop;
        ClipContents = true;
    }

    /// <summary>
    /// 把 3D 视口固定成指定像素尺寸（调试截图用）。和 HiveWE 的 GLWidget 同尺寸时，
    /// 两边的相机 aspect 一致，画面才能逐像素对。
    /// </summary>
    public void SetViewSize(Vector2I size)
    {
        Stretch = false;
        _vp.Size = size;
    }

    /// <summary>调试用：临时改垂直 FOV（对齐 HiveWE 的等效视野时用）。</summary>
    public void SetFov(float fov)
    {
        _fov = Mathf.Clamp(fov, 10f, 140f);
        if (_cam != null)
        {
            _cam.Fov = _fov;
        }
        CameraChanged?.Invoke();
    }

    /// <summary>设置水平角与俯角（度）。</summary>
    public void SetOrbit(float yaw, float pitch)
    {
        _yaw = yaw;
        _pitch = Mathf.Clamp(pitch, -88f, -6f);
        UpdateCamera();
    }

    /// <summary>设置视觉中心的高度（世界单位）。</summary>
    public void SetTargetHeight(float height)
    {
        _target = new Vector3(_target.X, Mathf.Clamp(height, -200f, MaxCameraHeight), _target.Z);
        ClampTarget();
        UpdateCamera();
    }

    /// <summary>
    /// 直接指定相机位置（世界坐标），保持当前的水平角/俯角/距离，反推 target。
    /// 用来复现别人截图里那台相机（标题栏里就有这个坐标）。
    /// </summary>
    public void SetCameraPosition(Vector3 position)
    {
        float yaw = Mathf.DegToRad(_yaw);
        float pitch = Mathf.DegToRad(_pitch);
        var forward = new Vector3(
            -Mathf.Cos(pitch) * Mathf.Sin(yaw),
            Mathf.Sin(pitch),
            -Mathf.Cos(pitch) * Mathf.Cos(yaw));
        _target = position + forward * _dist;
        UpdateCamera();
    }

    /// <summary>把视觉中心移到某格（格坐标，和状态栏、标题栏用同一套坐标）。</summary>
    public void MoveTargetToTile(Vector2 tile)
    {
        Vector2 world = MapToWorld(tile);
        _target = new Vector3(world.X, _target.Y, world.Y);
        ClampTarget();
        UpdateCamera();
    }

    /// <summary>直接取 3D 视口的渲染结果，不含 UI。</summary>
    public Image CaptureView()
    {
        return _vp.GetTexture().GetImage();
    }

    /// <summary>
    /// 没打开地图时的占位地形/水面挂在这个节点下，加载地图时整体撤掉。
    /// 以前它们直接挂在视口上，打开地图后旧的占位水面（一个大蓝平面）还留在场景里。
    /// </summary>
    private Node3D _placeholder;

    private Node3D PlaceholderRoot()
    {
        if (_placeholder == null)
        {
            _placeholder = new Node3D { Name = "Placeholder" };
            _vp.AddChild(_placeholder);
        }
        return _placeholder;
    }

    public override void _Ready()
    {
        _vp = new SubViewport
        {
            Size = new Vector2I(1280, 720),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Disabled,
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
            BackgroundColor = Color.FromHtml("000000"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Color.FromHtml("9fb4c8"),
            AmbientLightEnergy = 0.62f,
            // HiveWE 不做色调映射、也不做 sRGB 线性化，地形着色全在 sRGB 数值上直接算，
            // 想和它对上颜色就只能把 tonemap 关掉（地表/悬崖本身是 unshaded）。
            TonemapMode = Godot.Environment.ToneMapper.Linear,
            FogEnabled = false,
        };
        var we = new WorldEnvironment { Environment = env };
        _vp.AddChild(we);

        var sun = new DirectionalLight3D
        {
            LightEnergy = 1.45f,
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
                // 占位地形和真实地图必须用同一套镜像坐标，否则相机会看向地图外面。
                // 顶点在 ToWorld 里做 x 取反，绕序跟着对调，正面才不会被背面剔除掉。
                Vector3 p00 = ToWorld(new Vector3(x * CellSize, HeightAt(x * CellSize, z * CellSize), z * CellSize));
                Vector3 p10 = ToWorld(new Vector3((x + 1) * CellSize, HeightAt((x + 1) * CellSize, z * CellSize), z * CellSize));
                Vector3 p01 = ToWorld(new Vector3(x * CellSize, HeightAt(x * CellSize, (z + 1) * CellSize), (z + 1) * CellSize));
                Vector3 p11 = ToWorld(new Vector3((x + 1) * CellSize, HeightAt((x + 1) * CellSize, (z + 1) * CellSize), (z + 1) * CellSize));

                st.AddVertex(p00);
                st.AddVertex(p11);
                st.AddVertex(p01);
                st.AddVertex(p00);
                st.AddVertex(p10);
                st.AddVertex(p11);
            }
        }

        st.GenerateNormals();
        var mesh = st.Commit();

        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat, Name = "MockTerrain" };
        PlaceholderRoot().AddChild(mi);
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
            Position = new Vector3(-Cells * CellSize * 0.5f, WaterLevel, Cells * CellSize * 0.5f),
            Name = "MockWater",
        };
        PlaceholderRoot().AddChild(mi);
    }

    /// <summary>
    /// 用地图数据替换占位地形：每格两个三角形，高度取 w3e 的角点值，
    /// 贴图取该格四个角里出现次数最多的地表，UV 落在 512×256 贴图的某个 64×64 变体内。
    /// </summary>
    public void LoadMap(MapDocument document)
    {
        W3eTerrain terrain = document.Terrain;
        _terrain = terrain;
        if (terrain == null)
        {
            GD.PushWarning("[MAP] 地图里没有 war3map.w3e，无法显示地形");
            return;
        }

        ClearMap();

        var root = new Node3D { Name = "MapRoot" };
        _vp.AddChild(root);
        _mapRoot = root;
        _mapSize = new Vector2(terrain.Width - 1, terrain.Height - 1);

        bool[,] covered = BuildMapCliffs(root, terrain);
        BuildMapTerrain(root, terrain, covered);
        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-water") < 0)
        {
            BuildMapWater(root, terrain);
        }
        BuildMapUnits(root, terrain, document.Units);
        FrameCamera(terrain);

        int blight = 0, ramp = 0, wet = 0;
        foreach (W3eTerrain.Corner c in terrain.Corners)
        {
            if (c.Blight) blight++;
            if (c.Ramp) ramp++;
            if (c.HasWater) wet++;
        }
        GD.Print($"[MAP] 地形已生成: {terrain.Width}x{terrain.Height} 角点, {_mapSize.X * _mapSize.Y:0} tile, 水面 y={_waterY:0.0}, 疫病角点={blight}, 斜坡角点={ramp}, 水域角点={wet}");
    }

    private void ClearMap()
    {
        if (_mapRoot != null)
        {
            // 先摘出场景树再 QueueFree：QueueFree 是帧末才执行，连开两张图时
            // 旧地形会和新地形同时挂在视口里（也同时占着显存）。
            _vp.RemoveChild(_mapRoot);
            _mapRoot.QueueFree();
            _mapRoot = null;
        }

        if (_placeholder != null)
        {
            _vp.RemoveChild(_placeholder);
            _placeholder.QueueFree();
            _placeholder = null;
        }

        // 贴图数组、材质、地形数据都断开引用，交给 GC 回收；
        // 否则打开第二张图时上一张的 texture array 还挂在字段上。
        _terrain = null;
        _groundLayers = null;
        _groundOrder.Clear();
        _shadedMaterials.Clear();
        _waterMaterial = null;
        _waterFrameCount = 1;
        _waterFrameRate = 1;
        _waterElapsed = 0f;
        _waterY = WaterLevel;
        _mapSize = new Vector2(Cells, Cells);
    }

    /// <summary>
    /// 登记一块吃光照开关的材质，并把当前开关状态写进去（换地图时材质是新建的，
    /// 不重写一遍就会退回 shader 默认值，按钮状态和画面对不上）。
    /// </summary>
    private void RegisterShadedMaterial(ShaderMaterial material)
    {
        material.SetShaderParameter("lighting_enabled", _lighting);
        _shadedMaterials.Add(material);
    }

    /// <summary>
    /// 对应 HiveWE 的 map->render_lighting：关掉之后地形和悬崖都按原色（shade=1）画。
    /// </summary>
    public void SetLighting(bool on)
    {
        _lighting = on;
        foreach (ShaderMaterial material in _shadedMaterials)
        {
            material.SetShaderParameter("lighting_enabled", on);
        }
        GD.Print($"[VIEW] 光照={(on ? "开" : "关")}");
    }

    private void BuildMapTerrain(Node3D root, W3eTerrain terrain, bool[,] covered)
    {
        TerrainTileSet.SetBlightTileset(terrain.Tileset);

        // 地表顺序必须按 w3e 头里的列表（= Terrain.slk 的 tileID 顺序）排，不能按角点出现顺序。
        // 四层叠加里「底层取编号最小的那张」，编号的定义就是这张表的索引；顺序不对，
        // 底层和上层就颠倒了，叠加出来的颜色、过渡宽度都会和 HiveWE 差一截。
        var used = new HashSet<string>();
        foreach (W3eTerrain.Corner corner in terrain.Corners)
        {
            int id = corner.GroundTexture;
            if (id >= 0 && id < terrain.GroundTextures.Count)
            {
                used.Add(terrain.GroundTextures[id]);
            }
        }

        var order = new List<string>();
        foreach (string tileId in terrain.GroundTextures)
        {
            if (used.Contains(tileId))
            {
                order.Add(tileId);
            }
        }

        // 悬崖周围的格子画的是该悬崖类型的 groundTile，这张地表可能压根没被 w3e 角点引用，
        // 不主动加进来就会缺图（HiveWE 是把整个 tileset 的地表都塞进数组的）。
        foreach (string cliffId in terrain.CliffTextures)
        {
            string cliffGround = CliffTextureSet.GroundTileFor(cliffId);
            if (!string.IsNullOrEmpty(cliffGround) && !order.Contains(cliffGround))
            {
                order.Add(cliffGround);
            }
        }

        if (System.Array.Exists(terrain.Corners, c => c.Blight))
        {
            order.Add(TerrainTileSet.BlightId);
        }

        var tiles = new TerrainTileSet();
        var loaded = new List<string>();
        GroundTextureArray ground = GroundTextureArray.Build(tiles, order, loaded);
        GD.Print($"[MAP] 地表贴图 {loaded.Count}/{order.Count} 张: {string.Join(",", loaded)}");
        foreach (string cliffId in terrain.CliffTextures)
        {
            GD.Print($"[MAP] 悬崖 {cliffId} 脚下地表 = {CliffTextureSet.GroundTileFor(cliffId)}");
        }

        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/terrain_map.gdshader"),
        };
        if (ground?.Texture != null)
        {
            material.SetShaderParameter("ground_texture", ground.Texture);
        }
        RegisterShadedMaterial(material);

        _groundLayers = ground;
        _groundOrder = loaded;

        int width = terrain.Width;
        int height = terrain.Height;
        int culled = 0;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        // Godot 4 要求先声明自定义通道格式，否则 SetCustom 会报 CUSTOM_MAX
        st.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat); // 四角地表层号

        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                // 悬崖格的地面整块不画（和 HiveWE 的 0x8000 cliff 位等价）：四个角点跨了两个层高，
                // 留着这个四边形会在竖直岩壁里斜切出一块草地——就是「地表和悬崖接缝」上的穿模面。
                // 斜坡入口必须保留地面，坡面本身就是这块地面。covered 是模型盖满整格时的补充。
                if (!IsRampEntrance(terrain, x, y) && (IsCliffTile(terrain, x, y) || covered[x, y]))
                {
                    culled++;
                    continue;
                }

                W3eTerrain.Corner c00 = terrain.Corners[y * width + x];
                W3eTerrain.Corner c10 = terrain.Corners[y * width + x + 1];
                W3eTerrain.Corner c01 = terrain.Corners[(y + 1) * width + x];
                W3eTerrain.Corner c11 = terrain.Corners[(y + 1) * width + x + 1];

                // 四角各自的贴图 + 变体，shader 里按遮罩逐层叠加，
                // 顺序和 HiveWE 一致：底层是四角里编号最小的地表，其余依次往上盖。
                // 贴图不是直接取角点的 ground_texture，还要走一遍 real_tile_texture：
                // 悬崖/斜坡周围换成悬崖的 groundTile，疫病角点换成 blight 贴图。
                var layers = TileLayers(
                    RealTileTexture(terrain, x, y),
                    RealTileTexture(terrain, x + 1, y),
                    RealTileTexture(terrain, x, y + 1),
                    RealTileTexture(terrain, x + 1, y + 1),
                    c00);

                float x0 = x * CellSize;
                float x1 = (x + 1) * CellSize;
                float z0 = y * CellSize;
                float z1 = (y + 1) * CellSize;

                var p00 = new Vector3(x0, CornerWorld(terrain, x, y), z0);
                var p10 = new Vector3(x1, CornerWorld(terrain, x + 1, y), z0);
                var p01 = new Vector3(x0, CornerWorld(terrain, x, y + 1), z1);
                var p11 = new Vector3(x1, CornerWorld(terrain, x + 1, y + 1), z1);

                // 法线按角点算（不是每格平面法线），相邻格共用同一角点的法线，光照才连续
                Vector3 n00 = CornerNormal(terrain, x, y);
                Vector3 n10 = CornerNormal(terrain, x + 1, y);
                Vector3 n01 = CornerNormal(terrain, x, y + 1);
                Vector3 n11 = CornerNormal(terrain, x + 1, y + 1);

                // 和 HiveWE 的 terrain.vs 一致：UV = (x, 1 - y)。地表贴图本身有上下方向性
                // （每个 64×64 小块的上半和下半亮度不一样），不翻转的话每一格都会出现亮暗横带。
                var uv00 = new Vector2(0f, 1f);
                var uv10 = new Vector2(1f, 1f);
                var uv11 = new Vector2(1f, 0f);
                var uv01 = new Vector2(0f, 0f);

                // 对角线要和 HiveWE 一致：它用 quad_indices = {(0,3,1),(1,3,2)}，
                // 也就是沿 p10–p01 这条反对角线切，而不是 p00–p11 主对角线。
                // 切法不同，坡度过渡处一个会翻成屋脊、另一个翻成凹谷——看起来就是
                // 「我们这边是直线折面、HiveWE 那边是曲面」。
                // 顶点在 AddVertex 里做了 x 镜像，绕序会反过来，所以每个三角形后两个顶点对调。
                AddVertex(st, p11, uv11, layers, n11);
                AddVertex(st, p10, uv10, layers, n10);
                AddVertex(st, p01, uv01, layers, n01);
                AddVertex(st, p01, uv01, layers, n01);
                AddVertex(st, p10, uv10, layers, n10);
                AddVertex(st, p00, uv00, layers, n00);
            }
        }

        GD.Print($"[MAP] 地面格抠掉 {culled}（悬崖+斜坡过渡）");
        var instance = new MeshInstance3D
        {
            Mesh = st.Commit(),
            MaterialOverride = material,
            Name = "MapTerrain",
        };
        root.AddChild(instance);
    }

    /// <summary>
    /// 一格四角的地表 → 四层贴图数组层号。底层取编号最小的那张地表（和 HiveWE 的 set.begin() 一致），
    /// 其余角按贴图编号从小到大依次叠加。
    /// </summary>
    private Vector4 TileLayers(string t00, string t10, string t01, string t11, W3eTerrain.Corner c00)
    {
        // 层序照 HiveWE 的 get_texture_variations：底层是四角里编号最小的地表，变体取「左下角角点」的
        // ground_variation（纯色块）；其余层用「哪几个角用了这张地表」的 4 位掩码当层号
        // （bit0=右下, bit1=左下, bit2=右上, bit3=左上），指向左半区的遮罩块。
        // 注意混合公式不能照抄它——见 terrain_map.gdshader 里的说明。
        int bl = TextureIndex(_groundOrder, t00);
        int br = TextureIndex(_groundOrder, t10);
        int tl = TextureIndex(_groundOrder, t01);
        int tr = TextureIndex(_groundOrder, t11);

        var ids = new List<int> { bl, br, tl, tr };
        ids.Sort();
        var distinct = new List<int>();
        foreach (int id in ids)
        {
            if (!distinct.Contains(id))
            {
                distinct.Add(id);
            }
        }

        int baseId = distinct[0];

        float LayerWithMask(int textureId)
        {
            int mask = 0;
            if (br == textureId) mask |= 1;
            if (bl == textureId) mask |= 2;
            if (tr == textureId) mask |= 4;
            if (tl == textureId) mask |= 8;
            return BaseLayerOf(textureId) + mask;
        }

        // 底层用角点的 variation 选「纯色块」（贴图右半区，alpha 全 255），负责纹理变化；
        // 其余层用 mask 选「遮罩块」（贴图左半区，alpha 是渐变），由 shader 按它们的 alpha
        // 覆盖上来。遮罩块一块对应一种「四角组合」，这就是 War3 地表在格子之间柔和过渡的来源。
        float baseLayer = LayerOf(c00, baseId);

        // 用不到的层指向全透明黑（第 0 层），和 HiveWE 的 17 号黑透明纹理等价。
        // 填成底层会把遮罩 alpha 抵消掉，草地会明显比 HiveWE 亮。
        float b = distinct.Count > 1 ? LayerWithMask(distinct[1]) : GroundTextureArray.BlankLayer;
        float g = distinct.Count > 2 ? LayerWithMask(distinct[2]) : GroundTextureArray.BlankLayer;
        float r = distinct.Count > 3 ? LayerWithMask(distinct[3]) : GroundTextureArray.BlankLayer;
        return new Vector4(baseLayer, b, g, r);
    }

    private float BaseLayerOf(int textureIndex)
    {
        if (_groundLayers == null || textureIndex < 0 || textureIndex >= _groundOrder.Count)
        {
            return 0f;
        }
        return _groundLayers.BaseLayer[_groundOrder[textureIndex]];
    }

    private float LayerOf(W3eTerrain.Corner corner, int textureIndex)
    {
        if (_groundLayers == null || textureIndex < 0 || textureIndex >= _groundOrder.Count)
        {
            return 0f;
        }
        return _groundLayers.LayerOf(_groundOrder[textureIndex], corner.VariationIndex);
    }

    /// <summary>
    /// 世界高度 = (raw-8192)/512 + layer_height - 2，与 HiveWE 的 final_ground_height 一致。
    /// layer_height 是地形层高（0-15），漏掉它地形会整体沉到水面以下。
    /// </summary>
    private static float GroundWorld(W3eTerrain.Corner corner)
    {
        return (corner.HeightWorld + corner.LayerHeight - 2f) * CellSize;
    }

    /// <summary>
    /// 渲染用角点高度：GroundWorld 再加斜坡的半层抬升。
    /// HiveWE 在 update_ground_heights 里把「斜坡入口格」的底层角点抬高 0.5，
    /// 一层高差就摊到两格上、每格半层（约 24°）；少了这半层，斜坡只剩一格宽、约 43°，
    /// 起坡/收坡的折痕会非常硬，斜坡下段还会掉到水面以下被淹。
    /// </summary>
    private static float CornerWorld(W3eTerrain terrain, int x, int y)
    {
        return GroundWorld(terrain.Corners[y * terrain.Width + x]) + RampLift(terrain, x, y) * CellSize;
    }

    /// <summary>
    /// 角点是否该抬半层：遍历包含这个角点的四格，只要有「斜坡入口格」且本角点是它的底层角点，就是 0.5。
    /// </summary>
    private static float RampLift(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        int layer = terrain.Corners[y * width + x].LayerHeight;

        for (int ox = -1; ox <= 0; ox++)
        {
            for (int oy = -1; oy <= 0; oy++)
            {
                int tx = x + ox;
                int ty = y + oy;
                if (tx < 0 || ty < 0 || tx >= width - 1 || ty >= height - 1)
                {
                    continue;
                }
                if (!IsRampEntrance(terrain, tx, ty))
                {
                    continue;
                }

                int baseLayer = Mathf.Min(
                    Mathf.Min(terrain.Corners[ty * width + tx].LayerHeight, terrain.Corners[ty * width + tx + 1].LayerHeight),
                    Mathf.Min(terrain.Corners[(ty + 1) * width + tx].LayerHeight, terrain.Corners[(ty + 1) * width + tx + 1].LayerHeight));
                if (layer == baseLayer)
                {
                    return 0.5f;
                }
            }
        }
        return 0f;
    }

    private static void AddVertex(SurfaceTool st, Vector3 position, Vector2 uv, Vector4 layers, Vector3 normal)
    {
        st.SetUV(uv);
        // 地图坐标的第二根水平轴在 HiveWE 里是 y、在我们这里是 z，直接换轴属于反射变换，
        // 画面会左右镜像。所以世界 x 取反把手性翻回来，法线的 x 分量跟着反。
        st.SetNormal(new Vector3(-normal.X, normal.Y, normal.Z));
        st.SetCustom(0, new Color(layers.X, layers.Y, layers.Z, layers.W));
        st.AddVertex(new Vector3(-position.X, position.Y, position.Z));
    }



    /// <summary>
    /// 角点法线：用左右/上下相邻角点的高度差，跨度按实际格数归一化。
    /// 竖直分量取 2.0 而不是 1.0，因为 HiveWE 用 normalize(vec3(hL-hR, hD-hU, 2))，
    /// 法线更接近竖直，坡度光差才和他们一致。
    /// </summary>
    private static Vector3 CornerNormal(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        int x0 = Mathf.Max(0, x - 1);
        int x1 = Mathf.Min(width - 1, x + 1);
        int y0 = Mathf.Max(0, y - 1);
        int y1 = Mathf.Min(height - 1, y + 1);

        // 和 HiveWE 的 terrain.vs 一致：取左右/上下各一个角点的高度差（层高不算），竖直分量固定 2.0。
        // 关键：HiveWE 的高度贴图存的是「格」单位（(raw-8192)/512），差量级 0.05，
        // 所以这里也要换成格，否则水平分量大 128 倍，坡面法线被夸成陡壁，光照全乱。
        float nx = (terrain.Corners[y * width + x0].HeightWorld - terrain.Corners[y * width + x1].HeightWorld) / CellSize;
        float nz = (terrain.Corners[y0 * width + x].HeightWorld - terrain.Corners[y1 * width + x].HeightWorld) / CellSize;
        return new Vector3(nx, 2f, nz).Normalized();
    }

    private static int TextureIndex(List<string> used, string tileId)
    {
        if (string.IsNullOrEmpty(tileId))
        {
            return 0;
        }
        int index = used.IndexOf(tileId);
        return index < 0 ? 0 : index;
    }

    /// <summary>
    /// 角点实际该用哪张贴图，照 HiveWE 的 real_tile_texture：
    /// 附近有悬崖/斜坡角点 → 该悬崖类型的 groundTile；本角点带疫病 → blight；否则就是自己的地表。
    /// 「附近」查的是 (x-1..x, y-1..y) 这四个角点，也就是这一格的左下角连同它的左/下邻居。
    /// </summary>
    private static string RealTileTexture(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int height = terrain.Height;

        // HiveWE 这一段用的是 goto 跳出两层循环，所以这里也得「找到就整体跳出」，
        // 不能让外层继续扫下一个角点。
        bool plainRampCell = false;
        for (int i = -1; i < 1 && !plainRampCell; i++)
        {
            for (int j = -1; j < 1 && !plainRampCell; j++)
            {
                int cx = x + i;
                int cy = y + j;
                if (cx < 0 || cx >= width || cy < 0 || cy >= height)
                {
                    continue;
                }

                W3eTerrain.Corner corner = terrain.Corners[cy * width + cx];
                bool cliff = cx < width - 1 && cy < height - 1 && IsCliffTile(terrain, cx, cy);
                if (!cliff)
                {
                    continue;
                }

                W3eTerrain.Corner br = terrain.Corners[cy * width + cx + 1];
                W3eTerrain.Corner tl = terrain.Corners[(cy + 1) * width + cx];
                W3eTerrain.Corner tr = terrain.Corners[(cy + 1) * width + cx + 1];

                // 四个角全是斜坡：这一格保持原地表（坡道口不换图），并且整体跳出
                if (corner.Ramp && tl.Ramp && br.Ramp && tr.Ramp)
                {
                    plainRampCell = true;
                    continue;
                }

                int texture = corner.CliffTexture;
                if (texture == 15)
                {
                    texture -= 14;
                }
                if (texture >= 0 && texture < terrain.CliffTextures.Count)
                {
                    string ground = CliffTextureSet.GroundTileFor(terrain.CliffTextures[texture]);
                    if (!string.IsNullOrEmpty(ground))
                    {
                        return ground;
                    }
                }
            }
        }

        if (terrain.Corners[y * width + x].Blight)
        {
            return TerrainTileSet.BlightId;
        }

        int id = terrain.Corners[y * width + x].GroundTexture;
        return id >= 0 && id < terrain.GroundTextures.Count ? terrain.GroundTextures[id] : null;
    }

    /// <summary>四角层高不一致就是悬崖格，和 HiveWE 的 corner.cliff 判定一致。</summary>
    private static bool IsCliffTile(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int bl = terrain.Corners[y * width + x].LayerHeight;
        int br = terrain.Corners[y * width + x + 1].LayerHeight;
        int tl = terrain.Corners[(y + 1) * width + x].LayerHeight;
        int tr = terrain.Corners[(y + 1) * width + x + 1].LayerHeight;
        return bl != br || bl != tl || bl != tr;
    }

    /// <summary>四角都是斜坡且不对称：这是斜坡入口，地面照常画、悬崖模型不摆。</summary>
    private static bool IsRampEntrance(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        W3eTerrain.Corner bl = terrain.Corners[y * width + x];
        W3eTerrain.Corner br = terrain.Corners[y * width + x + 1];
        W3eTerrain.Corner tl = terrain.Corners[(y + 1) * width + x];
        W3eTerrain.Corner tr = terrain.Corners[(y + 1) * width + x + 1];
        return bl.Ramp && tl.Ramp && br.Ramp && tr.Ramp
               && !(bl.LayerHeight == tr.LayerHeight && tl.LayerHeight == br.LayerHeight);
    }

    /// <summary>
    /// 悬崖：War3 的悬崖是逐格的 MDX 模型（Doodads/Terrain/Cliffs/Cliffs????.mdx），
    /// 文件名四个字母是四角相对最低层的高度差（A=0, B=1, C=2, D=3），末尾是变体号。
    /// 这里把整张图的悬崖实例合成一个网格，逐顶点带上贴图数组层号和光照系数。
    /// </summary>
    private static readonly Vector3 CliffLightDirection = new Vector3(-0.6095f, 0.5079f, -0.6095f);

    /// <summary>
    /// 单位的贴图很小（128×128 图集切出一小块），屏幕上只有十几像素，
    /// 三线性 mipmap 会把图集里相邻的脸/腿纹理混进来，看起来发花。
    /// 改成不带 mipmap 的双线性，和 HiveWE 的采样方式一致。
    /// </summary>
    private static int ApplyUnitTextureFilter(Node node)
    {
        int changed = 0;
        if (node is MeshInstance3D instance && instance.Mesh != null)
        {
            for (int surface = 0; surface < instance.Mesh.GetSurfaceCount(); surface++)
            {
                Material material = instance.MaterialOverride
                    ?? instance.GetSurfaceOverrideMaterial(surface)
                    ?? instance.Mesh.SurfaceGetMaterial(surface);
                if (material is BaseMaterial3D base3D)
                {
                    // WC3 的贴图是 128×128 图集切一小块，屏幕上只有十几像素，
                    // 三线性 mipmap 会把图集里相邻区域混进来，看起来发花。
                    base3D.TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear;
                    base3D.TextureRepeat = false;
                    changed++;
                }
            }
        }
        foreach (Node child in node.GetChildren())
        {
            changed += ApplyUnitTextureFilter(child);
        }
        return changed;
    }

    /// <summary>
    /// 单位与物品：war3mapUnits.doo 里摆放的实例。
    /// 静态显示（不播动画、不做阴影、不换队伍色），和 HiveWE 的 StaticMesh 行为一致。
    /// 起始点标记 sloc 只表示玩家出生位置，不渲染。
    /// </summary>
    private int BuildMapUnits(Node3D root, W3eTerrain terrain, W3oUnits units)
    {
        if (units == null || Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-units") >= 0)
        {
            return 0;
        }

        var holder = new Node3D { Name = "MapUnits" };
        root.AddChild(holder);

        float offsetX = terrain.OffsetX;
        float offsetY = terrain.OffsetY;
        int placed = 0;
        int skipped = 0;
        int materialSlots = 0;

        foreach (UnitPlacement entry in units.Entries)
        {
            if (!entry.IsItem && entry.Id == "sloc")
            {
                skipped++;
                continue;
            }

            UnitModel model = UnitMeshLibrary.Get(entry.Id, entry.IsItem);
            if (model?.Scene == null)
            {
                skipped++;
                continue;
            }

            var instance = model.Scene.Instantiate<Node3D>();
            // doo 里存的是 WC3 世界坐标，减掉地形原点就是格坐标；
            // 世界坐标的换算和地形一致：x 取反、y 换到 z、z 换到 y。
            instance.Position = new Vector3(
                -(entry.X - offsetX),
                entry.Z,
                entry.Y - offsetY);
            // 世界坐标的 x 被取反了（整体绕 Y 转了 180°），朝向要多补这个 180°
            instance.Rotation = new Vector3(0f, Mathf.Pi + entry.Angle, 0f);
            instance.Scale = Vector3.One * model.Scale;
            instance.Name = $"{entry.Id}_{placed}";
            ModelLibrary.ApplyMissingMaterials(instance, model.ScenePath);
            materialSlots += ApplyUnitTextureFilter(instance);
            holder.AddChild(instance);
            placed++;
        }

        GD.Print($"[UNIT] 单位 {placed} 个（模型 {UnitMeshLibrary.Count} 种），跳过 {skipped} 个，材质槽 {materialSlots} 个");
        return placed;
    }

    /// <summary>
    /// 把地图构建成一棵独立的节点树，不挂到 UI 上——供场景导出（Demo）使用。
    /// 地形/悬崖/水面走的是同一套 Build 方法，和编辑器里看到的完全一致。
    /// </summary>
    public Node3D BuildMapTree(W3eTerrain terrain, W3oUnits units = null, bool withWater = true)
    {
        _terrain = terrain;
        _mapSize = new Vector2(terrain.Width - 1, terrain.Height - 1);

        var root = new Node3D { Name = "MapRoot" };
        bool[,] covered = BuildMapCliffs(root, terrain);
        BuildMapTerrain(root, terrain, covered);
        if (withWater)
        {
            BuildMapWater(root, terrain);
        }
        BuildMapUnits(root, terrain, units);
        return root;
    }

    /// <summary>加载一个已经导出的场景（.tscn），替代从地图实时构建。</summary>
    public void LoadScene(string scenePath)
    {
        ClearMap();
        var packed = GD.Load<PackedScene>(scenePath);
        if (packed == null)
        {
            GD.PushError($"[SCENE] 加载失败: {scenePath}");
            return;
        }
        var instance = packed.Instantiate<Node3D>();
        _vp.AddChild(instance);
        _mapRoot = instance;
        float width = instance.HasMeta("map_width") ? instance.GetMeta("map_width").AsSingle() : _mapSize.X;
        float height = instance.HasMeta("map_height") ? instance.GetMeta("map_height").AsSingle() : _mapSize.Y;
        // 场景根节点带缩放（Demo 是 0.01），世界跨度和相机距离都要跟着缩，
        // 否则相机停在三万单位外看一个两百多单位的模型，等于什么都没有。
        float scale = instance.Scale.X <= 0f ? 1f : instance.Scale.X;
        _mapSize = new Vector2(width * scale, height * scale);
        _dist = 20f * CellSize * scale;
        _yaw = 180f;
        _pitch = Mathf.RadToDeg(-0.977f);
        SetRingVisible(false);
        GD.Print($"[SCENE] 已加载 {scenePath}，节点 {CountNodes(instance)} 个，地图 {width:0}x{height:0} 格");
    }

    private static int CountNodes(Node node)
    {
        int count = 1;
        foreach (Node child in node.GetChildren())
        {
            count += CountNodes(child);
        }
        return count;
    }

    private bool[,] BuildMapCliffs(Node3D root, W3eTerrain terrain)
    {
        int width = terrain.Width;
        int height = terrain.Height;

        bool[,] cliffCorner = new bool[width, height];
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                cliffCorner[x, y] = IsCliffTile(terrain, x, y);
            }
        }

        // 先收集所有悬崖/斜坡模型的三角形，最后用「格子里的采样点是否被真实三角形盖住」
        // 判断哪一格地面要抠。以前按模型包围盒标记，跨格的斜坡模型会把旁边没被盖住的格子
        // 也抠掉，画面上就是一个纯黑的方块（能看到背景）。
        bool[,] covered = new bool[width, height];

        CliffTextureSet cliffSet = CliffTextureSet.Build(terrain.CliffTextures);
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/cliff_map.gdshader"),
        };
        if (cliffSet?.Texture != null)
        {
            material.SetShaderParameter("cliff_texture", cliffSet.Texture);
        }
        RegisterShadedMaterial(material);

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat);

        int tiles = 0;
        int trans = 0;
        int firstTransX = -1, firstTransZ = -1;
        var missing = new Dictionary<string, int>();

        for (int x = 0; x < width - 1; x++)
        {
            for (int y = 0; y < height - 1; y++)
            {
                if (!cliffCorner[x, y])
                {
                    continue;
                }

                W3eTerrain.Corner bl = terrain.Corners[y * width + x];
                W3eTerrain.Corner br = terrain.Corners[y * width + x + 1];
                W3eTerrain.Corner tl = terrain.Corners[(y + 1) * width + x];
                W3eTerrain.Corner tr = terrain.Corners[(y + 1) * width + x + 1];

                // 地图边界外没有地形，朝向边界的那几圈不摆模型，避免崖壁飞到图外
                bool facingDown = tl.LayerHeight >= bl.LayerHeight && tr.LayerHeight >= br.LayerHeight;
                bool facingLeft = br.LayerHeight >= bl.LayerHeight && tr.LayerHeight >= tl.LayerHeight;
                if ((facingDown && y == 0) || (!facingDown && y >= height - 2)
                    || (facingLeft && x == 0) || (!facingLeft && x >= width - 2))
                {
                    continue;
                }

                int baseLayer = Math.Min(Math.Min(bl.LayerHeight, br.LayerHeight), Math.Min(tl.LayerHeight, tr.LayerHeight));

                // 斜坡和平面交界的那一格换用 CliffTrans 模型（HiveWE 的 br/bo 判定）
                int sideX = x + (facingLeft ? -1 : 1);
                int sideZ = y + (facingDown ? -1 : 1);
                bool alongX = bl.Ramp != br.Ramp && tl.Ramp != tr.Ramp
                              && !CornerFlag(cliffCorner, x + (br.Ramp ? 1 : 0), sideZ);
                bool alongZ = bl.Ramp != tl.Ramp && br.Ramp != tr.Ramp
                              && !CornerFlag(cliffCorner, sideX, y + (tl.Ramp ? 1 : 0));
                if (alongX || alongZ)
                {
                    string variant = string.Concat(
                        RampChar(bl, baseLayer),
                        RampChar(tl, baseLayer),
                        RampChar(tr, baseLayer),
                        RampChar(br, baseLayer));
                    string transModel = CliffMeshFactory.Find("CliffTrans" + variant + "0");
                    if (transModel != null)
                    {
                        int px = x + (alongZ && !facingLeft ? 1 : 0);
                        int pz = y - (alongX && facingDown ? 1 : 0);
                        MarkCovered(st, terrain, transModel, px, pz, baseLayer, cliffSet, bl, covered, cullGround: false);
                        tiles++;
                        trans++;
                        if (firstTransX < 0)
                        {
                            firstTransX = x;
                            firstTransZ = y;
                        }
                        continue;
                    }
                    missing["CliffTrans" + variant] = missing.GetValueOrDefault("CliffTrans" + variant) + 1;
                }

                // 四角全是斜坡且对角高度不一致：这是坡道本身，地面照常画（斜坡是斜面，
                // 摆崖壁模型会把它盖成垂直的岩石）。HiveWE 同样在这里 continue。
                if (IsRampEntrance(terrain, x, y))
                {
                    continue;
                }

                string cliffVariant = string.Concat(
                    (char)('A' + bl.LayerHeight - baseLayer),
                    (char)('A' + tl.LayerHeight - baseLayer),
                    (char)('A' + tr.LayerHeight - baseLayer),
                    (char)('A' + br.LayerHeight - baseLayer));
                if (cliffVariant == "AAAA")
                {
                    continue;
                }

                string model = CliffMeshFactory.ResolveCliff(cliffVariant, bl.CliffVariation);
                if (model == null)
                {
                    missing[cliffVariant] = missing.GetValueOrDefault(cliffVariant) + 1;
                    continue;
                }

                MarkCovered(st, terrain, model, x, y, baseLayer, cliffSet, bl, covered);
                tiles++;
            }
        }

        if (missing.Count > 0)
        {
            GD.PushWarning($"[CLIFF] 缺少模型: {string.Join(",", missing.Keys)}");
        }

        if (tiles > 0)
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = st.Commit(),
                MaterialOverride = material,
                Name = "MapCliffs",
            });
        }

        GD.Print($"[CLIFF] 悬崖格 {tiles} 个（其中斜坡过渡 {trans}@({firstTransX},{firstTransZ})）, 贴图集合 {terrain.CliffTextures.Count} 组: {string.Join(",", terrain.CliffTextures)}");
        return covered;
    }

    /// <summary>
    /// 把一个悬崖/斜坡过渡模型铺到格子 (x, y) 上：横向占满一格，竖直方向逐顶点贴合地面高度。
    /// 贴图层号取该格左下角角点的悬崖集合，光照系数取四角平均法线。
    /// </summary>
    private void MarkCovered(SurfaceTool st, W3eTerrain terrain, string model, int x, int y, int baseLayer,
        CliffTextureSet cliffSet, W3eTerrain.Corner bl, bool[,] covered, bool cullGround = true)
    {
        CliffGeometry geometry = CliffMeshFactory.Get(model);
        Vector3 normal = (
            CornerNormal(terrain, x, y) + CornerNormal(terrain, x + 1, y) +
            CornerNormal(terrain, x, y + 1) + CornerNormal(terrain, x + 1, y + 1)).Normalized();
        float layer = CliffLayer(cliffSet, terrain, bl.CliffTexture);
        // HiveWE 的假光照：clamp(dot(n, L) + 0.45)。法线是世界空间（x 在 AddVertex 里取反），
        // 这里直接算好塞进 CUSTOM0.y，片元里只做乘法，避免再走一遍法线插值。
        Vector3 worldNormal = new Vector3(-normal.X, normal.Y, normal.Z);
        float shade = Mathf.Clamp(worldNormal.Dot(CliffLightDirection) + 0.45f, 0f, 1f);
        var offset = new Vector3((x + 1) * CellSize, 0f, y * CellSize);

        var triangles = new List<Vector3>();
        for (int i = 0; i + 2 < geometry.Indices.Length; i += 3)
        {
            // 和地形一样：顶点镜像后绕序反了，后两个顶点对调
            var tri = new Vector3[3];
            for (int k = 0; k < 3; k++)
            {
                int index = geometry.Indices[i + (k == 1 ? 2 : (k == 2 ? 1 : 0))];
                Vector3 vertex = geometry.Vertices[index] + offset;
                vertex.Y = GroundSurface(terrain, x, y, vertex.X, vertex.Z, baseLayer) + geometry.Vertices[index].Y;
                tri[k] = vertex;
                AddVertex(st, vertex, geometry.Uvs[index], new Vector4(layer, shade, 0f, 0f), normal);
            }
            triangles.Add(tri[0]);
            triangles.Add(tri[1]);
            triangles.Add(tri[2]);
        }

        // 斜坡过渡模型（CliffTrans）是贴在坡面上的薄片，本身不封闭，抠掉地面就会露出背景。
        // 地面留着由它盖住，视觉上才是连续的斜坡。
        if (cullGround)
        {
            MarkFullyCoveredTiles(terrain, triangles, covered);
        }
    }

    /// <summary>三角形在 (x, z) 处的高度（XZ 平面重心坐标插值）；点不在三角形内返回 false。</summary>
    private static bool TriangleHeightAt(List<Vector3> triangles, int start, float x, float z, out float y)
    {
        Vector3 a = triangles[start];
        Vector3 b = triangles[start + 1];
        Vector3 c = triangles[start + 2];

        float area = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
        if (Mathf.Abs(area) < 1e-4f)
        {
            // 竖直的崖壁在 XZ 上是一条线，不参与覆盖判断
            y = 0f;
            return false;
        }

        float l1 = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / area;
        float l2 = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / area;
        float l3 = 1f - l1 - l2;
        if (l1 < -0.02f || l2 < -0.02f || l3 < -0.02f)
        {
            y = 0f;
            return false;
        }

        y = l1 * a.Y + l2 * b.Y + l3 * c.Y;
        return true;
    }

    /// <summary>
    /// 用**这一个模型自己的**三角形判断它把哪些格子整格盖住了，只对这些格子抠地面。
    /// 只看自己的三角形很重要：以前是把整张图所有悬崖三角形汇总后按格采样，隔壁模型的
    /// 斜面会误判成「这格被盖住」，结果地面挖掉、崖壁却没铺到，露出黑色背景。
    /// </summary>
    private static void MarkFullyCoveredTiles(W3eTerrain terrain, List<Vector3> triangles, bool[,] covered)
    {
        int width = terrain.Width;
        int height = terrain.Height;

        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < triangles.Count; i++)
        {
            minX = Mathf.Min(minX, triangles[i].X);
            maxX = Mathf.Max(maxX, triangles[i].X);
            minZ = Mathf.Min(minZ, triangles[i].Z);
            maxZ = Mathf.Max(maxZ, triangles[i].Z);
        }
        if (triangles.Count == 0)
        {
            return;
        }

        int tx0 = Mathf.Max(0, Mathf.FloorToInt(minX / CellSize));
        int tx1 = Mathf.Min(width - 2, Mathf.FloorToInt(maxX / CellSize - 0.001f));
        int tz0 = Mathf.Max(0, Mathf.FloorToInt(minZ / CellSize));
        int tz1 = Mathf.Min(height - 2, Mathf.FloorToInt(maxZ / CellSize - 0.001f));

        for (int tz = tz0; tz <= tz1; tz++)
        {
            for (int tx = tx0; tx <= tx1; tx++)
            {
                if (covered[tx, tz])
                {
                    continue;
                }

                bool all = true;
                for (int sz = 1; sz <= 4 && all; sz++)
                {
                    for (int sx = 1; sx <= 4 && all; sx++)
                    {
                        float wx = (tx + sx * 0.2f) * CellSize;
                        float wz = (tz + sz * 0.2f) * CellSize;
                        float ground = InterpolatedHeight(terrain, wx / CellSize, wz / CellSize);

                        bool hit = false;
                        for (int t = 0; t + 2 < triangles.Count; t += 3)
                        {
                            if (TriangleHeightAt(triangles, t, wx, wz, out float surface) && surface >= ground - 2f)
                            {
                                hit = true;
                                break;
                            }
                        }
                        all = hit;
                    }
                }
                covered[tx, tz] = all;
            }
        }
    }

    private static bool CornerFlag(bool[,] grid, int x, int y)
    {
        return x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1) && grid[x, y];
    }

    /// <summary>
    /// 斜坡过渡模型名的单个字母：斜坡角点从 'L' 往回数（每差一层退 4 个字母），平面角点从 'A' 往上数。
    /// </summary>
    private static char RampChar(W3eTerrain.Corner corner, int baseLayer)
    {
        int delta = corner.LayerHeight - baseLayer;
        return (char)((corner.Ramp ? 'L' : 'A') + delta * (corner.Ramp ? -4 : 1));
    }

    /// <summary>
    /// 格子内任意位置的地面高度：四角 w3e 原始高度双线性插值，再加上最低那层的层高。
    /// 悬崖模型的顶点都贴到这个面上，模型自带的 z 才表示它高出地面多少。
    /// </summary>
    /// <summary>任意格坐标处的最终地表高度（四角双线性插值），对应 HiveWE 的 interpolated_height。</summary>
    private static float InterpolatedHeight(W3eTerrain terrain, float x, float y)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        float cx = Mathf.Clamp(x, 0f, width - 1.01f);
        float cy = Mathf.Clamp(y, 0f, height - 1.01f);
        int x0 = (int)cx;
        int y0 = (int)cy;
        int x1 = Mathf.Min(x0 + 1, width - 1);
        int y1 = Mathf.Min(y0 + 1, height - 1);
        float tx = cx - x0;
        float ty = cy - y0;

        float h00 = GroundWorld(terrain.Corners[y0 * width + x0]);
        float h10 = GroundWorld(terrain.Corners[y0 * width + x1]);
        float h01 = GroundWorld(terrain.Corners[y1 * width + x0]);
        float h11 = GroundWorld(terrain.Corners[y1 * width + x1]);
        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
    }

    private static float GroundSurface(W3eTerrain terrain, int x, int y, float worldX, float worldZ, int baseLayer)
    {
        int width = terrain.Width;
        float h00 = terrain.Corners[y * width + x].HeightWorld;
        float h10 = terrain.Corners[y * width + x + 1].HeightWorld;
        float h01 = terrain.Corners[(y + 1) * width + x].HeightWorld;
        float h11 = terrain.Corners[(y + 1) * width + x + 1].HeightWorld;

        float tx = Mathf.Clamp((worldX - x * CellSize) / CellSize, 0f, 1f);
        float tz = Mathf.Clamp((worldZ - y * CellSize) / CellSize, 0f, 1f);
        float height = Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
        return (height + baseLayer - 2f) * CellSize;
    }

    private static float CliffLayer(CliffTextureSet set, W3eTerrain terrain, int cliffTexture)
    {
        if (set == null || cliffTexture < 0 || cliffTexture >= terrain.CliffTextures.Count)
        {
            return 0f;
        }
        return set.LayerOf(terrain.CliffTextures[cliffTexture]);
    }

    /// <summary>
    /// 水面：按 HiveWE 的做法逐格铺，四角各用自己的水位，颜色按水深查 Water.slk 的
    /// 浅水/深水端点。整张图一个平面会让内陆河、湖被海平面淹掉。
    /// </summary>
    private void BuildMapWater(Node3D root, W3eTerrain terrain)
    {
        WaterTileSet water = WaterTileSet.Load(terrain.Tileset);
        if (water == null)
        {
            return;
        }

        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/water_map.gdshader"),
        };
        if (water.Texture != null)
        {
            material.SetShaderParameter("water_texture", water.Texture);
        }
        _waterMaterial = material;
        _waterFrameCount = Mathf.Max(1, water.FrameCount);
        _waterFrameRate = Mathf.Max(1, water.FrameRate);

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        int width = terrain.Width;
        int height = terrain.Height;
        int tiles = 0;
        var levels = new Dictionary<int, int>();

        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                W3eTerrain.Corner c00 = terrain.Corners[y * width + x];
                W3eTerrain.Corner c10 = terrain.Corners[y * width + x + 1];
                W3eTerrain.Corner c01 = terrain.Corners[(y + 1) * width + x];
                W3eTerrain.Corner c11 = terrain.Corners[(y + 1) * width + x + 1];
                if (!c00.HasWater && !c10.HasWater && !c01.HasWater && !c11.HasWater)
                {
                    continue;
                }

                float x0 = x * CellSize;
                float x1 = (x + 1) * CellSize;
                float z0 = y * CellSize;
                float z1 = (y + 1) * CellSize;

                var p00 = new Vector3(x0, WaterWorld(c00, water), z0);
                var p10 = new Vector3(x1, WaterWorld(c10, water), z0);
                var p01 = new Vector3(x0, WaterWorld(c01, water), z1);
                var p11 = new Vector3(x1, WaterWorld(c11, water), z1);
                CountLevel(levels, c00);
                CountLevel(levels, c10);
                CountLevel(levels, c01);
                CountLevel(levels, c11);

                // 波纹贴图一格一次（War3 里就是 128×128 铺一格）；v 方向按 HiveWE 的
                // UV = (x, 1 - y) 翻转
                var uv00 = new Vector2(x, 1 - y);
                var uv10 = new Vector2(x + 1, 1 - y);
                var uv01 = new Vector2(x, -y);
                var uv11 = new Vector2(x + 1, -y);

                // 顶点镜像后绕序反了，每个三角形的后两个顶点对调
                AddWaterVertex(st, p00, uv00, WaterColor(water, terrain, x, y));
                AddWaterVertex(st, p11, uv11, WaterColor(water, terrain, x + 1, y + 1));
                AddWaterVertex(st, p10, uv10, WaterColor(water, terrain, x + 1, y));
                AddWaterVertex(st, p00, uv00, WaterColor(water, terrain, x, y));
                AddWaterVertex(st, p01, uv01, WaterColor(water, terrain, x, y + 1));
                AddWaterVertex(st, p11, uv11, WaterColor(water, terrain, x + 1, y + 1));
                tiles++;
            }
        }

        _waterY = MostCommonLevel(levels, water);
        if (tiles == 0)
        {
            return;
        }

        root.AddChild(new MeshInstance3D
        {
            Mesh = st.Commit(),
            MaterialOverride = material,
            Name = "MapWater",
        });
        GD.Print($"[WATER] 水面格 {tiles} 个, 最高 y={_waterY:0.0}, 帧数 {water.FrameCount}");
    }

    /// <summary>
    /// 角点没有水时把水面压到地面高度（w3e 里干燥角点的水位字段是残留值，
    /// 直接用会让水面从草地里戳出来）。
    /// </summary>
    private void CountLevel(Dictionary<int, int> levels, W3eTerrain.Corner corner)
    {
        if (!corner.HasWater)
        {
            return;
        }
        int key = (int)Mathf.Round(corner.WaterWorld * 100f);
        levels[key] = levels.GetValueOrDefault(key) + 1;
    }

    /// <summary>水域里出现最多的水位，用来放笔刷光标（水面是逐格的，没有唯一高度）。</summary>
    private static float MostCommonLevel(Dictionary<int, int> levels, WaterTileSet water)
    {
        int bestKey = int.MinValue;
        int bestCount = 0;
        foreach (var pair in levels)
        {
            if (pair.Value > bestCount)
            {
                bestCount = pair.Value;
                bestKey = pair.Key;
            }
        }
        return bestCount == 0 ? WaterLevel : (bestKey / 100f + water.HeightOffset) * CellSize;
    }

    /// <summary>
    /// 角点的水面高度，和 HiveWE 的 water.vs 一致：一律用角点自己的 water_height + 偏移，
    /// 不做「干燥角点压到地面」的修正。压到地面会在崖边把水面抬成一条竖着的深色三角片
    /// （看起来像破洞）；水位字段本来就只在 0 和正常水位两个值，都在地表以下，交给地形遮住。
    /// </summary>
    private static float WaterWorld(W3eTerrain.Corner corner, WaterTileSet water)
    {
        return (corner.WaterWorld + water.HeightOffset) * CellSize;
    }

    /// <summary>
    /// 顶点颜色 = 该角点的水色。水深 = 水面 - 最终地面高度（层高要算进去），
    /// 再到 deeplevel（0.5 格）分成浅水/深水两段线性插值，端点取 Water.slk。
    /// alpha 一起带出去，浅滩几乎透明、深水接近不透明。
    /// </summary>
    private static Color WaterColor(WaterTileSet water, W3eTerrain terrain, int x, int y)
    {
        const float MinDepth = 10f / 128f;
        const float DeepLevel = 64f / 128f;
        const float MaxDepth = 72f / 128f;

        // 和 HiveWE 的 water.vs 一致：水面高度 - 最终地面高度。
        // HiveWE 水面着色器绑的是 ground_corner_height（含层高和斜坡半层），漏掉会把浅滩算成深海。
        W3eTerrain.Corner corner = terrain.Corners[y * terrain.Width + x];
        float waterY = corner.WaterWorld + water.HeightOffset;
        float groundY = CornerWorld(terrain, x, y) / CellSize;
        float depth = Mathf.Clamp(waterY - groundY, 0f, 1f);

        if (depth <= DeepLevel)
        {
            float t = Mathf.Clamp((depth - MinDepth) / (DeepLevel - MinDepth), 0f, 1f);
            return water.ShallowMin.Lerp(water.ShallowMax, t);
        }

        float u = Mathf.Clamp((depth - DeepLevel) / (MaxDepth - DeepLevel), 0f, 1f);
        return water.DeepMin.Lerp(water.DeepMax, u);
    }

    /// <summary>
    /// 水面波纹帧：HiveWE 从 1.0 开始，每帧加 texRate*delta，超过帧数就绕回 0（取整当层号）。
    /// </summary>
    private void UpdateWaterFrame(float delta)
    {
        if (_waterMaterial == null)
        {
            return;
        }
        _waterElapsed += Mathf.Max(0f, delta);
        int frame = Mathf.FloorToInt(1f + _waterElapsed * _waterFrameRate) % _waterFrameCount;
        _waterMaterial.SetShaderParameter("frame", (float)frame);
    }

    private static void AddWaterVertex(SurfaceTool st, Vector3 position, Vector2 uv, Color color)
    {
        st.SetUV(uv);
        st.SetColor(color);
        st.AddVertex(new Vector3(-position.X, position.Y, position.Z));
    }

    /// <summary>
    /// 初始视角照 HiveWE 的 Camera::reset：目标 = 地图中心的地表高度，距离 20 格，
    /// 水平角 0、垂直角 -0.977 弧度（-55.98°），是从南往北俯视。
    /// </summary>
    private void FrameCamera(W3eTerrain terrain)
    {
        _terrain = terrain;
        // HiveWE 是 position = (width / 2, height / 2)，这里两个都是 int，193/2 得 96 而不是 96.5。
        // 用 96.5 会让整个画面偏半格（实测 22 像素），所以整数除法要照抄。
        int centerX = terrain.Width / 2;
        int centerZ = terrain.Height / 2;
        _target = new Vector3(
            -centerX * CellSize,
            InterpolatedHeight(terrain, centerX, centerZ),
            centerZ * CellSize);
        _dist = 20f * CellSize;
        _yaw = 180f;
        _pitch = Mathf.RadToDeg(-0.977f);
        SetRingVisible(false);
        UpdateCamera();
        GD.Print($"[CAM] reset target=({_target.X:0},{_target.Y:0},{_target.Z:0}) dist={_dist:0} yaw={_yaw:0} pitch={_pitch:0.0} cam=({_cam.Position.X:0},{_cam.Position.Y:0},{_cam.Position.Z:0}) fov={_cam.Fov:0}");
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
        _ring.Position = new Vector3(-Cells * CellSize * 0.5f, WaterLevel + 1f, Cells * CellSize * 0.5f);
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

        // 相机高度上限：超了就按比例拉近，滚轮、旋转、Shift 升降都受这条约束
        if (offset.Y > 0.01f && _target.Y + offset.Y > MaxCameraHeight)
        {
            float allowed = Mathf.Max(1f, MaxCameraHeight - _target.Y);
            float scale = Mathf.Clamp(allowed / offset.Y, 0.001f, 1f);
            _dist = Mathf.Max(1f, _dist * scale);
            offset *= scale;
        }

        _cam ??= new Camera3D { Current = true, Fov = _fov, Near = 0.05f * CellSize, Far = 2000f * CellSize };
        if (_cam.GetParent() == null)
        {
            _vp.AddChild(_cam);
        }
        _cam.Position = _target + offset;
        _cam.LookAt(_target, Vector3.Up);
        CameraChanged?.Invoke();
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

        // 平移扫描：连续向右拖，target 必须单调朝 0 走（世界 x 的上界），不能一跳到底
        float startX = _target.X;
        float prevX = startX;
        bool monotonic = true;
        for (int i = 0; i < 30; i++)
        {
            PanBy(new Vector2(40f, 0f));
            if (_target.X < prevX - 0.01f)
            {
                monotonic = false;
            }
            prevX = _target.X;
        }
        GD.Print($"[CAM] panRight30 x {startX:0} -> {_target.X:0} (上界 0, 单调={monotonic})");

        // 跳到另一头再扫回来，确认两端都能到
        _target = new Vector3(0f, _target.Y, _target.Z);
        for (int i = 0; i < 60; i++)
        {
            PanBy(new Vector2(-40f, 0f));
        }
        GD.Print($"[CAM] panLeft60 x -> {_target.X:0} (下界 {- _mapSize.X * CellSize:0})");

        // Shift 升降：向上拖 100px，目标点应升高
        var y0 = _target.Y;
        var cam0 = _cam.Position.Y;
        HeightBy(new Vector2(0f, -100f));
        UpdateCamera();
        GD.Print($"[CAM] heightUp100 targetY {y0:0.0} -> {_target.Y:0.0}, camY {cam0:0.0} -> {_cam.Position.Y:0.0} (应升高)");
    }

    /// <summary>调试用：把相机目标移到某格（格坐标），配合 --dist 看局部。</summary>
    public void FocusOn(Vector2 tile)
    {
        Vector2 world = MapToWorld(tile);
        _target = new Vector3(world.X, _target.Y, world.Y);
        ClampTarget();
        UpdateCamera();
    }

    /// <summary>调试用：直接设置相机距离，近距离看地表过渡。</summary>
    public void SetDistance(float distance)
    {
        _dist = Mathf.Max(1f, distance);
        UpdateCamera();
        GD.Print($"[CAM] dist={_dist:0} camY={_cam.Position.Y:0} limit={MaxCameraHeight:0}");
    }

    public void ResetCamera()
    {
        if (_terrain != null)
        {
            FrameCamera(_terrain);
            return;
        }

        _yaw = 180f;
        _pitch = Mathf.RadToDeg(-0.977f);
        _dist = 20f * CellSize;
        _target = new Vector3(-Cells * CellSize * 0.5f, 30f, Cells * CellSize * 0.5f);
        UpdateCamera();
    }

    public override void _Process(double delta)
    {
        UpdateWaterFrame((float)delta);

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

        float t = (_waterY - o.Y) / d.Y;
        if (t <= 0f)
        {
            return;
        }

        Vector3 hit = o + d * t;
        _ring.Visible = true;
        _ring.Position = new Vector3(hit.X, hit.Y + 0.4f, hit.Z);
        CursorMoved?.Invoke(WorldToMap(hit), hit.Y);
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
        // 抬高 target 时也要给相机留余量，否则 UpdateCamera 会把距离压到最小
        float headroom = MaxCameraHeight - -Mathf.Sin(Mathf.DegToRad(_pitch)) * _dist;
        _target = new Vector3(_target.X, Mathf.Clamp(_target.Y, -200f, Mathf.Max(-200f, headroom)), _target.Z);
        ClampTarget();
    }

    /// <summary>绕视觉中心旋转：目标点不动，只改水平角和俯角。</summary>
    public void OrbitBy(Vector2 screenDelta)
    {
        _yaw -= screenDelta.X * 0.25f;
        _pitch = Mathf.Clamp(_pitch - screenDelta.Y * 0.25f, -88f, -6f);
    }

    /// <summary>
    /// 格坐标 → 世界坐标。世界 x 是镜像的（见 AddVertex），所以范围是 [-宽, 0]；
    /// 所有需要在地图范围里夹取的地方都应该走这两个函数，别再直接假设 x 从 0 起。
    /// </summary>
    public static Vector2 MapToWorld(Vector2 tile)
    {
        return new Vector2(-tile.X * CellSize, tile.Y * CellSize);
    }

    /// <summary>占位几何用的世界坐标变换：格坐标已经是世界单位，只需要把 x 镜像。</summary>
    private static Vector3 ToWorld(Vector3 mapPoint)
    {
        return new Vector3(-mapPoint.X, mapPoint.Y, mapPoint.Z);
    }

    /// <summary>世界坐标 → 格坐标（MapToWorld 的逆）。</summary>
    public static Vector2 WorldToMap(Vector3 world)
    {
        return new Vector2(-world.X / CellSize, world.Z / CellSize);
    }

    /// <summary>官方 WE 会把视角限制在地图范围内，这里照做，避免拖到图外面。</summary>
    private void ClampTarget()
    {
        // 世界 x 是镜像的：格 0 在世界 x=0，格宽在世界 x=-宽*CellSize，所以上界是 0。
        // 之前按 [0, 宽] 夹，目标点一动就被拉到 x=0，表现出来就是「闪到地图左侧、拖不回右边」。
        _target = new Vector3(
            Mathf.Clamp(_target.X, -_mapSize.X * CellSize, 0f),
            _target.Y,
            Mathf.Clamp(_target.Z, 0f, _mapSize.Y * CellSize));
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed:
                _dist = Mathf.Clamp(_dist * 0.887f, 0.001f * CellSize, 1000f * CellSize);
                UpdateCamera();
                AcceptEvent();
                break;
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed:
                _dist = Mathf.Clamp(_dist * 1.127f, 0.001f * CellSize, 1000f * CellSize);
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
