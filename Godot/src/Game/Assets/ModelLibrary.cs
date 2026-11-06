using Godot;
using System;

namespace WorldEdit.Assets;

/// <summary>
/// glb 模型库：把「war3 里的相对路径」（如 `Doodads/Terrain/Cliffs/CliffsAAAB0.mdx`）
/// 换成 `res://assets/converted/` 下的 glb 路径。
///
/// 不扫目录、不读清单：导出包里原始 .glb 不进 pck（只留 .import 注册和 .godot/imported 的产物），
/// 目录枚举会得到 0 个文件，但按精确路径 Load 是能命中的。
/// 路径大小写写错时 Exists 返回 false，调用方会打印「缺少模型」，按告警改常量即可。
/// </summary>
public static class ModelLibrary
{
    public const string Root = War3Assets.ConvertedRoot;

    /// <summary>把相对路径（带不带 .mdx/.glb 都行）换成 glb 的 res:// 路径；不存在返回 null。</summary>
    public static string Find(string name)
    {
        string key = name.Replace('\\', '/');
        if (key.EndsWith(".mdx", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
        {
            key = key[..^4];
        }

        string path = Root + key + ".glb";
        return ResourceLoader.Exists(path) || FileAccess.FileExists(path) ? path : null;
    }

    public static bool Exists(string name)
    {
        return Find(name) != null;
    }
}
