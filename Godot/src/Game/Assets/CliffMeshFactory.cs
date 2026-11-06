using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.Mdx;

namespace WorldEdit.Assets;

/// <summary>悬崖模型转成世界朝向的三角形数据：Y 向上，顶点单位已经是世界单位。</summary>
public sealed class CliffGeometry
{
    public Vector3[] Vertices = Array.Empty<Vector3>();
    public Vector2[] Uvs = Array.Empty<Vector2>();
    public int[] Indices = Array.Empty<int>();
}

/// <summary>
/// 悬崖 / 斜坡过渡模型（Doodads/terrain/Cliffs/Cliffs????.mdx、Doodads/terrain/CliffTrans/CliffTrans????.mdx）。
/// MDX 是 Z 向上，Godot 是 Y 向上，转换 (x, y, z) → (x, z, y)。
/// 实测这个映射之后三角形绕序和 Godot 的正面约定一致（反着绕反而会被背面剔除掉）。
/// </summary>
public static class CliffMeshFactory
{
    private const string CliffDirectory = "Doodads/Terrain/Cliffs/";
    private const string TransDirectory = "Doodads/Terrain/CliffTrans/";

    private static readonly Dictionary<string, CliffGeometry> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Resolved = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>按模型名（不含扩展名）在悬崖/斜坡过渡两个目录里找，返回 glb 的相对路径。</summary>
    public static string Find(string fileName)
    {
        foreach (string directory in new[] { CliffDirectory, TransDirectory })
        {
            string relative = $"{directory}{fileName}";
            if (ModelLibrary.Exists(relative))
            {
                return relative;
            }
        }
        return null;
    }

    /// <summary>
    /// 悬崖变体名（如 AAAB）+ w3e 里的悬崖变体号 → 实际存在的模型路径。
    /// 变体号超出该组合的模型数量时往下退（HiveWE 用 Cliffs.slk 的 variations 做同样的钳制）。
    /// </summary>
    public static string ResolveCliff(string variant, int variation)
    {
        string key = "Cliffs" + variant + "|" + variation;
        if (Resolved.TryGetValue(key, out string cached))
        {
            return cached;
        }

        string result = null;
        for (int v = Math.Clamp(variation, 0, 7); v >= 0; v--)
        {
            result = Find("Cliffs" + variant + v);
            if (result != null)
            {
                break;
            }
        }
        Resolved[key] = result;
        return result;
    }

    public static CliffGeometry Get(string relativePath)
    {
        if (Cache.TryGetValue(relativePath, out CliffGeometry cached))
        {
            return cached;
        }

        var geometry = new CliffGeometry();
        string path = ModelLibrary.Find(relativePath);
        if (path == null)
        {
            GD.PrintErr($"[CLIFF] 找不到模型 {relativePath}");
            Cache[relativePath] = geometry;
            return geometry;
        }

        var packed = GD.Load<PackedScene>(path);
        if (packed == null)
        {
            GD.PrintErr($"[CLIFF] 加载失败 {path}");
            Cache[relativePath] = geometry;
            return geometry;
        }

        Node root = packed.Instantiate();
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();
        CollectMeshes(root, vertices, uvs, indices);
        root.QueueFree();

        if (vertices.Count > 0)
        {
            Vector3 min = vertices[0], max = vertices[0];
            foreach (Vector3 v in vertices) { min = min.Min(v); max = max.Max(v); }
            GD.Print($"[CLIFF] {relativePath} verts={vertices.Count} tris={indices.Count / 3} bbox={min}~{max}");
        }
        else
        {
            GD.Print($"[CLIFF] {relativePath} 没有几何");
        }
        geometry.Vertices = vertices.ToArray();
        geometry.Uvs = uvs.ToArray();
        geometry.Indices = indices.ToArray();
        Cache[relativePath] = geometry;
        return geometry;
    }

    /// <summary>
    /// 把 glb 里的网格摊平成一份顶点/UV/索引表。
    /// glb 是 Y-up（mdx 的 (x, y, z) 换成了 (x, z, -y)），这里换回原来 MDX 的朝向
    /// (x, z, y)，下游的镜像、贴地、绕序处理就不用改。
    /// </summary>
    private static void CollectMeshes(Node node, List<Vector3> vertices, List<Vector2> uvs, List<int> indices)
    {
        if (node is MeshInstance3D instance && instance.Mesh != null)
        {
            Mesh mesh = instance.Mesh;
            for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                Godot.Collections.Array arrays = mesh.SurfaceGetArrays(surface);
                var positions = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var texture = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
                var triangles = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();

                int offset = vertices.Count;
                for (int i = 0; i < positions.Length; i++)
                {
                    vertices.Add(new Vector3(positions[i].X, positions[i].Y, -positions[i].Z));
                    uvs.Add(i < texture.Length ? texture[i] : Vector2.Zero);
                }
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    indices.Add(offset + triangles[i]);
                    indices.Add(offset + triangles[i + 1]);
                    indices.Add(offset + triangles[i + 2]);
                }
            }
        }

        foreach (Node child in node.GetChildren())
        {
            CollectMeshes(child, vertices, uvs, indices);
        }
    }
}
