using Godot;
using System;
using System.Collections.Generic;

namespace WorldEdit.Assets;

/// <summary>
/// 模型库：把「war3 里的相对路径」（如 `Doodads/Terrain/Cliffs/CliffsAAAB0.mdx`）
/// 换成 `res://assets/converted/` 下的 per-model 场景或 glb。
///
/// slk 里写的目录是小写的，而 converted 下的目录用的是 MPQ 原始大小写。
/// 编辑器在 Windows 上不区分大小写所以看不出问题，但导出包里是严格匹配的，
/// 所以精确路径找不到时要逐段做一次不区分大小写的塌缩。
/// </summary>
public static class ModelLibrary
{
    public const string Root = War3Assets.ConvertedRoot;

    /// <summary>
    /// 把相对路径（带不带 .mdx/.glb 都行）换成 res:// 路径；
    /// 优先用同名的 per-model 场景（材质/动画已拆成独立资源），没有才回退到裸 glb。
    /// </summary>
    public static string Find(string name)
    {
        string key = Normalize(name);
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        foreach (string extension in new[] { ".tscn", ".glb" })
        {
            string direct = Root + key + extension;
            if (ResourceLoader.Exists(direct) || FileAccess.FileExists(direct))
            {
                return direct;
            }
        }

        foreach (string extension in new[] { ".tscn", ".glb" })
        {
            string guessed = CapitalizePath(key, extension);
            if (ResourceLoader.Exists(guessed) || FileAccess.FileExists(guessed))
            {
                return guessed;
            }
        }

        foreach (string extension in new[] { ".tscn", ".glb" })
        {
            string resolved = ResolvePath(key, extension);
            if (resolved != null)
            {
                return resolved;
            }
        }
        return null;
    }

    public static bool Exists(string name)
    {
        return Find(name) != null;
    }

    /// <summary>
    /// 兜底：per-model 场景里对 geoset 的材质覆盖依赖「实例内部节点」，
    /// 导出包里这条链可能断掉（材质一个都没加载、模型变白）。
    /// 这里按 materials 目录里 00_/01_… 的序号顺序，把缺材质的 MeshInstance3D 补上。
    /// </summary>
    public static int ApplyMissingMaterials(Node root, string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            return 0;
        }

        string materialsDirectory = scenePath.GetBaseDir() + "/materials";
        if (!DirAccess.DirExistsAbsolute(materialsDirectory))
        {
            return 0;
        }

        var files = new List<string>();
        using (DirAccess access = DirAccess.Open(materialsDirectory))
        {
            if (access == null)
            {
                return 0;
            }
            access.ListDirBegin();
            string entry = access.GetNext();
            while (!string.IsNullOrEmpty(entry))
            {
                if (!access.CurrentIsDir() && entry.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(materialsDirectory + "/" + entry);
                }
                entry = access.GetNext();
            }
            access.ListDirEnd();
        }
        if (files.Count == 0)
        {
            return 0;
        }
        files.Sort(StringComparer.Ordinal);

        int index = 0;
        int applied = 0;
        foreach (Node node in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh == null)
            {
                continue;
            }
            if (mesh.GetSurfaceOverrideMaterial(0) == null && index < files.Count)
            {
                string candidate = files[index];
                try
                {
                    if (ResourceLoader.Exists(candidate) && ResourceLoader.Load(candidate) is Material material)
                    {
                        mesh.SetSurfaceOverrideMaterial(0, material);
                        applied++;
                    }
                }
                catch (Exception error)
                {
                    GD.PushWarning($"[UNIT] 材质兜底失败 {candidate}: {error.Message}");
                }
            }
            index++;
        }
        return applied;
    }

    private static string Normalize(string name)
    {
        string key = name.Replace('\\', '/').TrimStart('/');
        if (key.EndsWith(".mdx", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
        {
            key = key[..^4];
        }
        return key;
    }

    /// <summary>
    /// slk 里写的目录是全小写，converted 下用的是 MPQ 原始大小写（每个目录段首字母大写）。
    /// 按这个规律拼一次，能覆盖绝大多数；剩下的交给逐段枚举。
    /// </summary>
    private static string CapitalizePath(string key, string extension)
    {
        string[] parts = key.Split('/');
        var builder = new System.Text.StringBuilder(Root.TrimEnd('/'));
        for (int i = 0; i < parts.Length; i++)
        {
            string segment = parts[i];
            if (segment.Length > 0 && char.IsLower(segment[0]))
            {
                segment = char.ToUpperInvariant(segment[0]) + segment[1..];
            }
            builder.Append('/');
            builder.Append(i == parts.Length - 1 ? segment + extension : segment);
        }
        return builder.ToString();
    }

    /// <summary>逐段拼接，每一段目录和最后的文件名都允许大小写不同。</summary>
    private static string ResolvePath(string key, string extension)
    {
        string[] parts = key.Split('/');
        string current = Root.TrimEnd('/');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            string directory = MatchEntry(current, parts[i], false);
            if (directory == null)
            {
                return null;
            }
            current += "/" + directory;
        }

        string file = MatchEntry(current, parts[^1] + extension, true);
        return file == null ? null : current + "/" + file;
    }

    private static string MatchEntry(string directory, string want, bool isFile)
    {
        string exact = directory + "/" + want;
        bool hit = isFile ? FileAccess.FileExists(exact) : DirAccess.DirExistsAbsolute(exact);
        if (hit)
        {
            return want;
        }

        using DirAccess access = DirAccess.Open(directory);
        if (access == null)
        {
            return null;
        }

        access.ListDirBegin();
        string name = access.GetNext();
        while (!string.IsNullOrEmpty(name))
        {
            if (name != "." && name != ".." && string.Equals(name, want, StringComparison.OrdinalIgnoreCase))
            {
                access.ListDirEnd();
                return name;
            }
            name = access.GetNext();
        }
        access.ListDirEnd();
        return null;
    }
}
