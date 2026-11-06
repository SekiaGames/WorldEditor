using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WorldEdit.Core.Formats.Mdx;

namespace WorldEdit.Core.Tools;

/// <summary>
/// 把一个 MDX 模型拆成 Godot 原生资源，模型侧只剩 glb（几何 + 骨骼 + 蒙皮）：
///
/// <code>
/// Units/Creeps/Gnoll/
/// ├── Gnoll.glb                几何 + 骨骼 + 蒙皮（不含材质、不含动画）
/// ├── Gnoll.tscn               per-model 场景：实例化 glb，逐 geoset 挂材质，挂 AnimationPlayer
/// ├── Gnoll_anims.tres         AnimationLibrary，一条 sequence 一个 Animation
/// └── materials/
///     ├── 00_mat0.tres         每个 geoset 一个材质（宁可重复也不共用，方便单独改）
///     └── ...
/// </code>
///
/// 为什么不用 glb 内嵌材质/动画：glTF 的材质只有一层贴图，War3 的材质是多层（队伍色 + 模型贴图），
/// 而且 glb 里的动画是 TRS 通道，改一个动作就得重导整个模型。拆出来之后材质能在检查器里直接调、
/// 动画能单独替换，也不用为了改一个 geoset 触发整模型的导入。
/// </summary>
public static class ModelBundle
{
    /// <summary>WC3 的 filterMode → Godot StandardMaterial3D 的透明度设置。</summary>
    private static readonly Dictionary<uint, (int Transparency, float Cutoff, int BlendMode)> FilterAlpha = new()
    {
        [0] = (0, 0f, 0),       // NONE       → 不透明
        [1] = (2, 0.75f, 0),    // TRANSPARENT→ alpha scissor
        [2] = (1, 0f, 0),       // BLEND      → alpha 混合
        [3] = (1, 0f, 1),       // ADDITIVE   → 加色
        [4] = (1, 0f, 0),       // ADDALPHA
        [5] = (1, 0f, 1),       // MODULATE
        [6] = (1, 0f, 1),       // MODULATE2X
    };

    public static void Write(MdxDocument model, string glbPath)
    {
        string glbRes = ToResPath(glbPath);
        string dirRes = glbRes.GetBaseDir();
        string dirAbs = glbPath.GetBaseDir();
        string baseName = glbPath.GetFile().GetBaseName();
        bool hasSkeleton = model.Bones.Count > 0;

        var materials = new List<string>();
        string materialsDir = Path.Combine(dirAbs, "materials");
        Directory.CreateDirectory(materialsDir);
        uint snapshotFrame = SnapshotFrame(model);
        var hiddenGeosets = new HashSet<int>();
        int totalVertices = 0;
        int hiddenVertices = 0;
        for (int i = 0; i < model.Geosets.Count; i++)
        {
            MdxGeoset geoset = model.Geosets[i];
            string file = $"{i:00}_mat{geoset.MaterialId}.tres";
            WriteText(Path.Combine(materialsDir, file), MaterialText(model, i, geoset, file, snapshotFrame));
            materials.Add($"{dirRes}/materials/{file}");

            int vertices = geoset.Positions == null ? 0 : geoset.Positions.Length / 3;
            totalVertices += vertices;
            if (model.GeosetAlphaAt(i, snapshotFrame) < 0.5f)
            {
                hiddenGeosets.Add(i);
                hiddenVertices += vertices;
            }
        }

        // 建筑这类模型在 Stand 起点上所有 geoset 的 KGAO 都是 0（它们的轨道描述的是建造/升级过程），
        // 藏掉一半以上的几何说明这个判定点不可靠，退回「全部显示」。
        if (hiddenGeosets.Count == model.Geosets.Count || hiddenVertices * 10 > totalVertices * 6)
        {
            hiddenGeosets.Clear();
        }

        Dictionary<int, float[]> restOffsets = RestOffsets(model);
        List<Clip> clips = BuildClips(model, hasSkeleton, restOffsets, snapshotFrame);
        string animations = null;
        string stateMachine = null;
        string idleAnimation = null;
        if (clips.Count > 0)
        {
            animations = WriteAnimationLibrary(clips, dirAbs, dirRes, baseName);
            (stateMachine, idleAnimation) = WriteStateMachine(clips, dirAbs, dirRes, baseName);
        }
        WriteText(Path.Combine(dirAbs, baseName + ".tscn"),
            SceneText(model, baseName, glbRes, materials, hiddenGeosets, animations, stateMachine, idleAnimation, hasSkeleton));
    }

    // ------------------------------------------------------------------ 材质

    /// <summary>静态快照取 Stand 序列的起点；没有 Stand 就用 0。</summary>
    private static uint SnapshotFrame(MdxDocument model)
    {
        foreach (MdxSequence sequence in model.Sequences)
        {
            if (string.Equals(sequence.Name, "Stand", StringComparison.OrdinalIgnoreCase))
            {
                return sequence.Start;
            }
        }
        return 0;
    }

    private static string TexturePng(MdxDocument model, MdxLayer layer)
    {
        if (layer.TextureId < 0 || layer.TextureId >= model.Textures.Count)
        {
            return null;
        }
        MdxTexture texture = model.Textures[layer.TextureId];
        string name = texture.ReplaceableId switch
        {
            1 => "ReplaceableTextures/TeamColor/TeamColor00",
            2 => "ReplaceableTextures/TeamGlow/TeamGlow00",
            _ => string.IsNullOrEmpty(texture.Path)
                ? null
                : Path.ChangeExtension(texture.Path.Replace('\\', '/'), null),
        };
        return name == null ? null : ResolveConverted($"{ConvertedRoot()}/{name}.png");
    }

    /// <summary>
    /// 把 MDX 里的贴图路径解析成磁盘上真实的大小写，找不到就退回占位贴图。
    /// MDX 里的路径大小写很随意，开发环境不区分所以看不出来，
    /// 打成 pck 以后严格匹配，材质会报 No loader found，整个模型场景都加载失败。
    /// </summary>
    private static string ResolveConverted(string resPath)
    {
        string project = ProjectSettings.GlobalizePath("res://").Replace('\\', '/').TrimEnd('/');
        string relative = resPath.StartsWith("res://", StringComparison.Ordinal) ? resPath[7..] : resPath;
        string current = project;
        foreach (string part in relative.Split('/'))
        {
            if (part.Length == 0)
            {
                continue;
            }
            string match = null;
            if (_directoryCache.TryGetValue(current, out string[] entries))
            {
                foreach (string entry in entries)
                {
                    if (string.Equals(Path.GetFileName(entry), part, StringComparison.OrdinalIgnoreCase))
                    {
                        match = entry;
                        break;
                    }
                }
            }
            else if (Directory.Exists(current))
            {
                entries = Directory.GetFileSystemEntries(current);
                _directoryCache[current] = entries;
                foreach (string entry in entries)
                {
                    if (string.Equals(Path.GetFileName(entry), part, StringComparison.OrdinalIgnoreCase))
                    {
                        match = entry;
                        break;
                    }
                }
            }
            if (match == null)
            {
                return MissingTexture;
            }
            current = match;
        }
        return ToResPath(current);
    }

    private const string MissingTexture = "res://assets/fallback/missing_texture.png";

    private static readonly Dictionary<string, string[]> _directoryCache = new(StringComparer.Ordinal);

    private static string ShaderFor(uint filterMode) => filterMode switch
    {
        3 => "war3_model_add",
        5 or 6 => "war3_model_mul",
        2 or 4 => "war3_model_blend",
        _ => "war3_model_opaque",
    };

    private static string MaterialText(MdxDocument model, int geosetIndex, MdxGeoset geoset, string fileName, uint snapshotFrame)
    {
        MdxMaterial material = geoset.MaterialId >= 0 && geoset.MaterialId < model.Materials.Count
            ? model.Materials[geoset.MaterialId]
            : null;
        MdxLayer first = material != null && material.Layers.Count > 0 ? material.Layers[0] : null;
        MdxLayer second = material != null && material.Layers.Count > 1 ? material.Layers[1] : null;

        uint replaceable = 0;
        if (first != null && first.TextureId >= 0 && first.TextureId < model.Textures.Count)
        {
            replaceable = model.Textures[first.TextureId].ReplaceableId;
        }

        // replaceableId 1 = 队伍色：用 team_color 参数，不加载那张 8x8 纯色板。
        // 默认给中立玩家的颜色 (40,40,40)，野怪看起来就和游戏里一致；玩家单位改参数即可。
        bool teamColor = replaceable == 1;
        bool glow = replaceable == 2;
        bool cutout = first != null && first.FilterMode == 1;

        string baseTexture = first != null && !teamColor ? TexturePng(model, first) : null;
        string detailTexture = second != null ? TexturePng(model, second) : null;

        var builder = new StringBuilder();
        int steps = 2 + (baseTexture != null ? 1 : 0) + (detailTexture != null ? 1 : 0);
        builder.Append($"[gd_resource type=\"ShaderMaterial\" load_steps={steps} format=3]\n\n");
        string shaderName = glow ? "war3_model_add" : ShaderFor(first?.FilterMode ?? 0);
        builder.Append($"[ext_resource type=\"Shader\" path=\"res://assets/shaders/{shaderName}.gdshader\" id=\"1_shader\"]\n");
        if (baseTexture != null)
        {
            builder.Append($"[ext_resource type=\"Texture2D\" path=\"{baseTexture}\" id=\"2_base\"]\n");
        }
        if (detailTexture != null)
        {
            builder.Append($"[ext_resource type=\"Texture2D\" path=\"{detailTexture}\" id=\"3_detail\"]\n");
        }

        builder.Append("\n[resource]\n");
        builder.Append($"resource_name = \"{Path.GetFileNameWithoutExtension(fileName)}\"\n");
        builder.Append("shader = ExtResource(\"1_shader\")\n");
        builder.Append($"shader_parameter/use_base_texture = {(baseTexture != null).ToString().ToLowerInvariant()}\n");
        if (baseTexture != null)
        {
            builder.Append("shader_parameter/base_texture = ExtResource(\"2_base\")\n");
        }
        builder.Append($"shader_parameter/use_team_color = {(teamColor ? "true" : "false")}\n");
        if (teamColor)
        {
            float neutral = 40f / 255f;
            builder.Append($"shader_parameter/team_color = Color({neutral.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {neutral.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {neutral.ToString(System.Globalization.CultureInfo.InvariantCulture)}, 1)\n");
        }
        builder.Append($"shader_parameter/use_detail = {(detailTexture != null ? "true" : "false")}\n");
        if (detailTexture != null)
        {
            builder.Append("shader_parameter/detail_texture = ExtResource(\"3_detail\")\n");
        }
        // GEOA 的静态 alpha 恒为 1，骸骨 / 血迹这类「只在死亡序列出现」的 geoset 靠 KGAO 在 Stand 期间为 0
        float geosetAlpha = model.GeosetAlphaAt(geosetIndex, snapshotFrame);
        float alpha = geosetAlpha * (first?.Alpha ?? 1f);
        builder.Append($"shader_parameter/layer_alpha = {alpha.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n");
        builder.Append($"shader_parameter/color_scale = {(first?.FilterMode == 6 ? "2.0" : "1.0")}\n");
        if (cutout)
        {
            builder.Append("shader_parameter/alpha_cutoff = 0.75\n");
        }
        return builder.ToString();
    }

    // ------------------------------------------------------------------ 动画

    private sealed class ClipTrack
    {
        public string Path = "";
        public string Type = "position_3d";
        public int Stride = 5;
        public readonly List<float> Values = new();
    }

    private sealed class Clip
    {
        public string Name = "";
        public float Length;
        public bool Loop;
        public readonly List<ClipTrack> Tracks = new();
    }

    /// <summary>每根骨骼相对父骨骼的静止位移（由 PIVT 算出，已转成 Godot 坐标）。</summary>
    private static Dictionary<int, float[]> RestOffsets(MdxDocument model)
    {
        var allNodes = new List<MdxNode>(model.Bones);
        allNodes.AddRange(model.Helpers);
        allNodes.Sort((a, b) => a.ObjectId.CompareTo(b.ObjectId));

        var pivotOf = new Dictionary<int, float[]>();
        foreach (MdxNode node in allNodes)
        {
            int offset = node.ObjectId * 3;
            pivotOf[node.ObjectId] = offset + 2 < model.Pivots.Length
                ? new[] { model.Pivots[offset], model.Pivots[offset + 1], model.Pivots[offset + 2] }
                : new[] { 0f, 0f, 0f };
        }

        var result = new Dictionary<int, float[]>();
        foreach (MdxNode node in allNodes)
        {
            MdxNode parent = allNodes.Find(candidate => candidate.ObjectId == node.ParentId);
            float[] pivot = pivotOf[node.ObjectId];
            float[] offset = parent != null && parent != node
                ? new[]
                {
                    pivot[0] - pivotOf[parent.ObjectId][0],
                    pivot[1] - pivotOf[parent.ObjectId][1],
                    pivot[2] - pivotOf[parent.ObjectId][2],
                }
                : pivot;
            result[node.ObjectId] = GlbWriter.ConvertPosition(offset[0], offset[1], offset[2]);
        }
        return result;
    }

    private static List<Clip> BuildClips(
        MdxDocument model, bool hasSkeleton, Dictionary<int, float[]> restOffsets, uint idleFrame)
    {
        var clips = new List<Clip>();
        var used = new HashSet<string>(StringComparer.Ordinal);

        var nodes = new List<MdxNode>(model.Bones);
        nodes.AddRange(model.Helpers);
        nodes.Sort((a, b) => a.ObjectId.CompareTo(b.ObjectId));

        foreach (MdxSequence sequence in model.Sequences)
        {
            if (sequence.End <= sequence.Start)
            {
                continue;
            }
            var clip = new Clip
            {
                Length = (sequence.End - sequence.Start) / 1000f,
                Loop = IsLoopingSequence(sequence.Name),
            };

            foreach (MdxNode node in nodes)
            {
                foreach (string path in new[] { "translation", "rotation", "scale" })
                {
                    if (!node.Tracks.TryGetValue(path, out MdxTrack track) || track.Keys.Count == 0)
                    {
                        continue;
                    }
                    List<MdxKey> keys = SelectKeys(track, sequence.Start, sequence.End, idleFrame);
                    if (keys.Count == 0)
                    {
                        continue;
                    }

                    var result = new ClipTrack
                    {
                        Path = hasSkeleton ? $"Model/Skeleton3D:node{node.ObjectId}" : $"Model:node{node.ObjectId}",
                        Type = path switch
                        {
                            "rotation" => "rotation_3d",
                            "scale" => "scale_3d",
                            _ => "position_3d",
                        },
                        Stride = path == "rotation" ? 6 : 5,
                    };

                    foreach (MdxKey key in keys)
                    {
                        result.Values.Add((float)((key.Frame - sequence.Start) / 1000.0));
                        result.Values.Add(1f);
                        if (path == "rotation")
                        {
                            float[] q = GlbWriter.ConvertQuaternion(key.Value);
                            result.Values.Add(q[0]);
                            result.Values.Add(q[1]);
                            result.Values.Add(q[2]);
                            result.Values.Add(q[3]);
                        }
                        else if (path == "translation")
                        {
                            // KGTR 是相对 pivot 的偏移，得叠加骨骼的静止位置，否则整根骨骼会被拉回原点
                            float[] p = GlbWriter.ConvertPosition(key.Value[0], key.Value[1], key.Value[2]);
                            float[] rest = restOffsets.TryGetValue(node.ObjectId, out float[] offset)
                                ? offset
                                : new[] { 0f, 0f, 0f };
                            result.Values.Add(rest[0] + p[0]);
                            result.Values.Add(rest[1] + p[1]);
                            result.Values.Add(rest[2] + p[2]);
                        }
                        else
                        {
                            float[] s = GlbWriter.ConvertScale(key.Value[0], key.Value[1], key.Value[2]);
                            result.Values.Add(s[0]);
                            result.Values.Add(s[1]);
                            result.Values.Add(s[2]);
                        }
                    }
                    clip.Tracks.Add(result);
                }
            }

            if (clip.Tracks.Count == 0)
            {
                continue;
            }

            string name = string.IsNullOrEmpty(sequence.Name) ? "seq" : sequence.Name;
            string unique = name;
            int suffix = 2;
            while (!used.Add(unique))
            {
                unique = $"{name} {suffix++}";
            }
            clip.Name = unique;
            clips.Add(clip);
        }
        return clips;
    }

    /// <summary>只有站立和走路循环；攻击、死亡、腐朽播完停在最后一帧。</summary>
    private static bool IsLoopingSequence(string name)
    {
        return name.StartsWith("Stand", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Walk", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>按时间对一条轨道求值（step / linear），用于取 idle 姿势。</summary>
    private static float[] SampleTrack(MdxTrack track, uint frame)
    {
        var keys = new List<MdxKey>();
        foreach (MdxKey key in track.Keys)
        {
            if ((key.Frame & 0x80000000) == 0)
            {
                keys.Add(key);
            }
        }
        if (keys.Count == 0)
        {
            return null;
        }
        keys.Sort((a, b) => a.Frame.CompareTo(b.Frame));
        if (frame <= keys[0].Frame)
        {
            return keys[0].Value;
        }
        for (int i = 0; i + 1 < keys.Count; i++)
        {
            if (frame < keys[i + 1].Frame)
            {
                if (track.Interpolation == 0 || keys[i + 1].Frame == keys[i].Frame)
                {
                    return keys[i].Value;
                }
                float t = (frame - keys[i].Frame) / (float)(keys[i + 1].Frame - keys[i].Frame);
                var value = new float[keys[i].Value.Length];
                for (int c = 0; c < value.Length; c++)
                {
                    value[c] = keys[i].Value[c] + (keys[i + 1].Value[c] - keys[i].Value[c]) * t;
                }
                return value;
            }
        }
        return keys[^1].Value;
    }

    /// <summary>
    /// 取序列窗口内的关键帧。窗口内没有关键帧时保持「进入这个动作前的那一帧」，
    /// 不能拿下一个动作的首帧来插值——那会让整段动作缓慢漂移成别的姿势。
    /// </summary>
    private static List<MdxKey> SelectKeys(MdxTrack track, uint start, uint end, uint idleFrame)
    {
        var keys = new List<MdxKey>();
        foreach (MdxKey key in track.Keys)
        {
            // 0x80000000 位表示「不循环」，这类关键帧不参与序列采样
            if ((key.Frame & 0x80000000) == 0)
            {
                keys.Add(key);
            }
        }
        keys.Sort((a, b) => a.Frame.CompareTo(b.Frame));

        MdxKey before = default;
        bool hasBefore = false;
        foreach (MdxKey key in keys)
        {
            if (key.Frame < start)
            {
                before = key;
                hasBefore = true;
            }
        }

        var selected = new List<MdxKey>();
        foreach (MdxKey key in keys)
        {
            if (key.Frame >= start && key.Frame <= end)
            {
                selected.Add(key);
            }
        }
        if (selected.Count > 0)
        {
            // 动作开头到第一个关键帧之间是从上一姿势过渡过来的，补一个起点
            if (hasBefore && selected[0].Frame > start)
            {
                selected.Insert(0, new MdxKey(start, before.Value));
            }
            return selected;
        }

        // 这个动作里这根骨骼没有关键帧：回到 idle 姿势。
        // 不能拿相邻序列的值——Walk 的根骨骼就这样拿到了尸体腐朽阶段的位置，角色会斜着走。
        float[] idle = SampleTrack(track, idleFrame);
        if (idle != null)
        {
            selected.Add(new MdxKey(start, idle));
            selected.Add(new MdxKey(end, idle));
        }
        return selected;
    }


    // ------------------------------------------------------------------ 场景

    private static string SceneText(
        MdxDocument model, string baseName, string glbRes, List<string> materials,
        HashSet<int> hiddenGeosets, string animations, string stateMachine, string idleAnimation, bool hasSkeleton)
    {
        var builder = new StringBuilder();
        int resources = 1 + materials.Count + (animations != null ? 1 : 0) + (stateMachine != null ? 1 : 0);
        builder.Append($"[gd_scene load_steps={resources + 1} format=3]\n\n");
        builder.Append($"[ext_resource type=\"PackedScene\" path=\"{glbRes}\" id=\"1_model\"]\n");
        for (int i = 0; i < materials.Count; i++)
        {
            builder.Append($"[ext_resource type=\"Material\" path=\"{materials[i]}\" id=\"m{i}\"]\n");
        }
        if (animations != null)
        {
            builder.Append($"[ext_resource type=\"AnimationLibrary\" path=\"{animations}\" id=\"9_anims\"]\n");
        }
        if (stateMachine != null)
        {
            builder.Append($"[ext_resource type=\"AnimationNodeStateMachine\" path=\"{stateMachine}\" id=\"8_tree\"]\n");
        }

        builder.Append($"\n[node name=\"{baseName}\" type=\"Node3D\"]\n\n");
        builder.Append("[node name=\"Model\" parent=\".\" instance=ExtResource(\"1_model\")]\n");
        for (int i = 0; i < materials.Count; i++)
        {
            string parent = hasSkeleton ? "Model/Skeleton3D" : "Model";
            builder.Append($"\n[node name=\"geoset{i}\" parent=\"{parent}\"]\n");
            if (hiddenGeosets.Contains(i))
            {
                // 隐藏的图元（尸体/血雾/TeamGlow）几何往往跨整个模型甚至超出脚底，
                // 不固定 AABB 会把整个模型的包围盒撑歪，编辑器里框选和相机适配都会偏。
                builder.Append("visible = false\n");
                builder.Append("custom_aabb = AABB(0, 0, 0, 1, 1, 1)\n");
            }
            builder.Append($"surface_material_override/0 = ExtResource(\"m{i}\")\n");
        }
        if (animations != null)
        {
            builder.Append("\n[node name=\"AnimationPlayer\" type=\"AnimationPlayer\" parent=\".\"]\n");
            builder.Append("root_node = NodePath(\"..\")\n");
            builder.Append("libraries = {\n\"\": ExtResource(\"9_anims\")\n}\n");
            if (idleAnimation != null)
            {
                builder.Append($"autoplay = \"{idleAnimation}\"\n");
                builder.Append($"current_animation = \"{idleAnimation}\"\n");
            }
        }
        if (stateMachine != null)
        {
            builder.Append("\n[node name=\"AnimationTree\" type=\"AnimationTree\" parent=\".\"]\n");
            builder.Append("active = true\n");
            builder.Append("root_node = NodePath(\"..\")\n");
            builder.Append("tree_root = ExtResource(\"8_tree\")\n");
            builder.Append("anim_player = NodePath(\"../AnimationPlayer\")\n");
        }
        // 导出器会把「子场景实例内部节点」的属性覆盖丢掉：打包后 tscn 转成 scn，
        // geoset 的 surface_material_override 全部消失，单位就变成白模。
        // 标记 Model 是「可编辑实例」，转换才会保留 geoset 的覆盖。
        builder.Append("\n[editable path=\"Model\"]\n");
        return builder.ToString();
    }

    // ------------------------------------------------------------------ 工具

    private static string ConvertedRoot()
    {
        return "res://assets/converted";
    }

    private static string ToResPath(string absolute)
    {
        string project = ProjectSettings.GlobalizePath("res://").Replace('\\', '/').TrimEnd('/');
        string path = absolute.Replace('\\', '/');
        if (path.StartsWith(project, StringComparison.OrdinalIgnoreCase))
        {
            return "res://" + path[project.Length..].TrimStart('/');
        }
        return path;
    }

    // ------------------------------------------------------------------ 资源文本

    private static string Fmt(float value)
    {
        return value.ToString("G7", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string KeysText(ClipTrack track)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < track.Values.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }
            builder.Append(Fmt(track.Values[i]));
        }
        return builder.ToString();
    }

    private static string WriteAnimationLibrary(List<Clip> clips, string dirAbs, string dirRes, string baseName)
    {
        var builder = new StringBuilder();
        builder.Append($"[gd_resource type=\"AnimationLibrary\" load_steps={clips.Count + 1} format=3]\n\n");
        for (int i = 0; i < clips.Count; i++)
        {
            Clip clip = clips[i];
            builder.Append($"[sub_resource type=\"Animation\" id=\"Animation_{i}\"]\n");
            builder.Append($"length = {Fmt(clip.Length)}\n");
            if (clip.Loop)
            {
                builder.Append("loop_mode = 1\n");
            }
            for (int t = 0; t < clip.Tracks.Count; t++)
            {
                ClipTrack track = clip.Tracks[t];
                builder.Append($"tracks/{t}/type = \"{track.Type}\"\n");
                builder.Append($"tracks/{t}/imported = false\n");
                builder.Append($"tracks/{t}/enabled = true\n");
                builder.Append($"tracks/{t}/path = NodePath(\"{track.Path}\")\n");
                builder.Append($"tracks/{t}/interp = 1\n");
                builder.Append($"tracks/{t}/loop_wrap = true\n");
                builder.Append($"tracks/{t}/keys = PackedFloat32Array({KeysText(track)})\n");
            }
            builder.Append("\n");
        }
        builder.Append("[resource]\n_data = {\n");
        for (int i = 0; i < clips.Count; i++)
        {
            builder.Append($"&\"{clips[i].Name}\": SubResource(\"Animation_{i}\"),\n");
        }
        builder.Append("}\n");

        string file = $"{baseName}_anims.tres";
        WriteText(Path.Combine(dirAbs, file), builder.ToString());
        return $"{dirRes}/{file}";
    }

    /// <summary>
    /// 每条 sequence 一个状态，idle 当中心节点，与其余状态双向连通。
    /// Start → idle 用 Auto 自动推进，所以不用脚本去调 playback.start()。
    /// </summary>
    private static (string Path, string Idle) WriteStateMachine(
        List<Clip> clips, string dirAbs, string dirRes, string baseName)
    {
        var states = new List<string>();
        foreach (Clip clip in clips)
        {
            if (clip.Name.StartsWith("Portrait", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            states.Add(clip.Name);
        }
        if (states.Count == 0)
        {
            return (null, null);
        }

        string idle = states.Find(name => string.Equals(name, "Stand", StringComparison.OrdinalIgnoreCase)) ?? states[0];

        var subs = new StringBuilder();
        var nodesText = new StringBuilder();
        for (int i = 0; i < states.Count; i++)
        {
            subs.Append($"[sub_resource type=\"AnimationNodeAnimation\" id=\"Anim_{i}\"]\n");
            subs.Append($"animation = &\"{states[i]}\"\n\n");
            nodesText.Append($"\"states/{states[i]}/node\" = SubResource(\"Anim_{i}\")\n");
            nodesText.Append($"\"states/{states[i]}/position\" = Vector2({320 + (i % 4) * 320}, {60 + (i / 4) * 180})\n");
        }

        var edges = new List<string>();
        void AddEdge(string from, string to, float xfade, bool auto)
        {
            string id = $"Trans_{edges.Count}";
            subs.Append($"[sub_resource type=\"AnimationNodeStateMachineTransition\" id=\"{id}\"]\n");
            subs.Append($"xfade_time = {Fmt(xfade)}\n");
            if (auto)
            {
                subs.Append("advance_mode = 2\n");
            }
            subs.Append("\n");
            edges.Add($"\"{from}\", \"{to}\", SubResource(\"{id}\")");
        }

        AddEdge("Start", idle, 0f, true);
        foreach (string state in states)
        {
            if (state == idle)
            {
                continue;
            }
            AddEdge(idle, state, 0.15f, false);
            AddEdge(state, idle, 0.15f, false);
        }

        var builder = new StringBuilder();
        builder.Append($"[gd_resource type=\"AnimationNodeStateMachine\" load_steps={states.Count + edges.Count + 1} format=3]\n\n");
        builder.Append(subs);
        builder.Append("[resource]\n");
        builder.Append(nodesText);
        builder.Append("transitions = [" + string.Join(", ", edges) + "]\n");

        string file = $"{baseName}_tree.tres";
        WriteText(Path.Combine(dirAbs, file), builder.ToString());
        return ($"{dirRes}/{file}", idle);
    }

    private static void WriteText(string path, string text)
    {
        File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
    }
}
