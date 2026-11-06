using Godot;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;
using WorldEdit.Core.Map;

namespace WorldEdit.Assets;

/// <summary>
/// 把地图构建成一棵节点树并保存成 .tscn（Demo 用）。
///
/// 几何复用 <see cref="ViewportPanel.BuildMapTree"/>，和编辑器里看到的是同一套代码。
/// 产物布局：
///
/// <code>
/// Demo/
/// ├── BootyBay.tscn            ← 根节点挂 BootyBayScene.cs
/// └── BootyBay/
///     ├── ground_material.tres  ← 材质球（shader + 参数）
///     ├── cliff_material.tres
///     ├── water_material.tres
///     └── layers/{ground,cliff,water}/NN.png   ← 图层贴图
/// </code>
///
/// 图层贴图单独存 PNG 而不是 Texture2DArray：后者是渲染资源，Godot 没有序列化器，
/// 存成 .res 只剩尺寸、像素全丢。场景脚本 <c>BootyBayScene</c> 在运行时把 PNG 拼回数组。
/// 这也意味着导出要跑两次：第一次写 PNG，等编辑器导入后再跑一次，.tscn 里才会带上贴图引用。
/// </summary>
public static class MapSceneExporter
{
    private static readonly Dictionary<string, (string Parameter, string File, string Shader)> LayerGroups = new()
    {
        ["mapterrain"] = ("ground_texture", "ground", "terrain_map"),
        ["mapcliffs"] = ("cliff_texture", "cliff", "cliff_map"),
        ["mapwater"] = ("water_texture", "water", "water_map"),
    };

    /// <summary>Demo 专用 shader：吃竖向拼接的图集。运行时那套在 assets/shaders 里，用的是 Texture2DArray。</summary>
    private const string DemoShaderRoot = "res://Demo/shader/";

    /// <summary>
    /// 根节点缩放。War3 原生 1 格 = 128 单位，整张图 24576×12288，
    /// 在 Godot 3D 视口里比默认操作尺度大两个数量级，拖拽/取景都不顺手。
    /// 0.01 之后是 246×123，正好「1 格 = 1.28 单位」。几何数据本身不改，只缩放根节点。
    /// </summary>
    private const float DemoScale = 0.01f;

    public static void Export(string mapPath, string scenePath)
    {
        MapDocument document = DirAccess.DirExistsAbsolute(mapPath)
            ? MapDocument.LoadDirectory(mapPath)
            : MapDocument.Load(mapPath);

        W3eTerrain terrain = document.Terrain;
        if (terrain == null)
        {
            GD.PushError("[SCENE] 地图里没有 war3map.w3e，无法导出场景");
            return;
        }

        string sceneDirectory = scenePath.GetBaseDir();
        string sceneName = scenePath.GetFile().GetBaseName();
        string assetDirectory = sceneDirectory + "/" + sceneName;
        DirAccess.MakeDirRecursiveAbsolute(assetDirectory);

        var view = new ViewportPanel();
        Node3D root = view.BuildMapTree(terrain, document.Units);
        root.Name = sceneName;
        root.SetMeta("map_width", terrain.Width - 1);
        root.SetMeta("map_height", terrain.Height - 1);
        root.Scale = new Vector3(DemoScale, DemoScale, DemoScale);

        var exported = new List<string>();
        var pendingImages = new List<string>();

        foreach (Node child in root.GetChildren())
        {
            if (child is not MeshInstance3D instance || instance.MaterialOverride is not ShaderMaterial material)
            {
                continue;
            }

            string node = instance.Name.ToString().ToLowerInvariant();
            if (!LayerGroups.TryGetValue(node, out var group))
            {
                continue;
            }

            if (material.GetShaderParameter(group.Parameter).Obj is Texture2DArray array)
            {
                var images = new Godot.Collections.Array<Image>();
                for (int i = 0; i < array.GetLayers(); i++)
                {
                    images.Add(array.GetLayerData(i));
                }

                Texture2D atlas = LayerAtlas.Build(images, out int layers);
                // 存成 .res（ImageTexture）而不是 PNG：PNG 走 Godot 导入会默认 VRAM 有损压缩
                // （compress/mode=2），颜色会变，实测和运行时的差从 0.14 涨到 1.5。
                string asset = $"{assetDirectory}/atlas/{group.File}.res";
                DirAccess.MakeDirRecursiveAbsolute(asset.GetBaseDir());
                Error saveError = ResourceSaver.Save(atlas, asset);
                if (saveError != Error.Ok)
                {
                    GD.PushWarning($"[SCENE] 图集保存失败 {asset}: {saveError}");
                }

                Shader shader = GD.Load<Shader>($"{DemoShaderRoot}{group.Shader}.gdshader");
                if (shader != null)
                {
                    material.Shader = shader;
                    material.SetShaderParameter(group.File + "_layers", (float)layers);
                }

                Texture2D loaded = ResourceLoader.Load<Texture2D>(asset);
                if (loaded != null)
                {
                    material.SetShaderParameter(group.Parameter, loaded);
                }
                else
                {
                    material.SetShaderParameter(group.Parameter, (Texture2D)null);
                    pendingImages.Add(asset);
                }
                exported.Add(asset);
            }

            string materialPath = $"{assetDirectory}/{group.File}_material.tres";
            Error saved = ResourceSaver.Save(material, materialPath);
            if (saved != Error.Ok)
            {
                GD.PushWarning($"[SCENE] 材质保存失败 {materialPath}: {saved}");
            }
            instance.MaterialOverride = ResourceLoader.Load<Material>(materialPath);
        }

        SetOwner(root, root);
        var scene = new PackedScene();
        Error packed = scene.Pack(root);
        if (packed != Error.Ok)
        {
            GD.PushError($"[SCENE] 打包场景失败: {packed}");
            root.QueueFree();
            view.QueueFree();
            return;
        }

        Error result = ResourceSaver.Save(scene, scenePath);
        GD.Print($"[SCENE] {scenePath}：节点 {root.GetChildCount()} 个，图集 {exported.Count} 张，"
            + $"待导入 {pendingImages.Count} 张，保存 {(result == Error.Ok ? "成功" : result.ToString())}");

        if (pendingImages.Count > 0)
        {
            GD.Print("[SCENE] 有图层贴图还没被 Godot 导入，场景里的贴图引用是空的。"
                + "先跑一次编辑器导入（--editor --quit），再重新导出本场景。");
            foreach (string path in pendingImages)
            {
                GD.Print($"[SCENE]   待导入 {path}");
            }
        }

        foreach (Node child in root.GetChildren())
        {
            child.QueueFree();
        }
        root.QueueFree();
        view.QueueFree();
        document.Release();
    }

    /// <summary>PackedScene.Pack 要求所有子节点的 owner 指向根，否则导出的场景里只有根节点。</summary>
    private static void SetOwner(Node node, Node owner)
    {
        foreach (Node child in node.GetChildren())
        {
            child.Owner = owner;
            SetOwner(child, owner);
        }
    }
}
