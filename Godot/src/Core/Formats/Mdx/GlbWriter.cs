using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldEdit.Core.Formats.Mdx;

namespace WorldEdit.Core.Formats.Mdx;

/// <summary>
/// MDX → glTF 2.0 二进制（.glb）组装。
///
/// 坐标：MDX 是 Z-up，glTF 是 Y-up，这里做 -90° 绕 X 的正交旋转
///     mdx (x, y, z)  ->  gltf (x, z, -y)
/// 行列式为 +1，不是镜像，Blender 打开方向正常。
///
/// glTF 装不下的东西（队伍颜色、混合模式、粒子、附着点、碰撞球）写到 sidecar JSON。
/// </summary>
public static class GlbWriter
{
    private static readonly float[] RotationQuaternion =
    {
        MathF.Sin(-MathF.PI / 4f), 0f, 0f, MathF.Cos(-MathF.PI / 4f),
    };

    private static readonly float[] RotationQuaternionInverse =
    {
        -MathF.Sin(-MathF.PI / 4f), 0f, 0f, MathF.Cos(-MathF.PI / 4f),
    };

    /// <summary>
    /// WC3 的 filterMode 与 glTF alphaMode 的对应关系（阈值照 HiveWE 的 static_mesh_instanced.fs）：
    ///   0 NONE        → OPAQUE
    ///   1 TRANSPARENT → MASK，alphaCutoff 0.75（镂空，不混合）
    ///   2 BLEND       → BLEND
    ///   3-6 是加法/调制类（ADDITIVE / ADDITIVEALPHA / MODULATE / MODULATE2X），
    ///       glTF 没有对应表达，暂时退化成 BLEND，做特效时再单独处理。
    /// </summary>
    private static readonly Dictionary<uint, (string Mode, float Cutoff)> FilterAlpha = new()
    {
        [0] = ("OPAQUE", 0f),
        [1] = ("MASK", 0.75f),
        [2] = ("BLEND", 0f),
        [3] = ("BLEND", 0f),
        [4] = ("BLEND", 0f),
        [5] = ("BLEND", 0f),
        [6] = ("BLEND", 0f),
    };

    private static float[] ConvertPosition(float x, float y, float z)
    {
        return new[] { x, z, -y };
    }

    private static float[] ConvertScale(float x, float y, float z)
    {
        return new[] { x, z, y };
    }

    private static float[] QuaternionMultiply(float[] a, float[] b)
    {
        float ax = a[0], ay = a[1], az = a[2], aw = a[3];
        float bx = b[0], by = b[1], bz = b[2], bw = b[3];
        return new[]
        {
            aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz,
        };
    }

    private static float[] ConvertQuaternion(float[] q)
    {
        float[] result = QuaternionMultiply(QuaternionMultiply(RotationQuaternion, q), RotationQuaternionInverse);
        float norm = MathF.Sqrt(result[0] * result[0] + result[1] * result[1]
            + result[2] * result[2] + result[3] * result[3]);
        if (norm < 1e-8f)
        {
            return new[] { 0f, 0f, 0f, 1f };
        }
        return new[] { result[0] / norm, result[1] / norm, result[2] / norm, result[3] / norm };
    }

    /// <summary>二进制缓冲区：所有 accessor 的数据按 4 字节对齐依次追加。</summary>
    private sealed class Builder
    {
        public readonly MemoryStream Bin = new();
        public readonly JsonArray BufferViews = new();
        public readonly JsonArray Accessors = new();

        private int AddView(byte[] raw, int target)
        {
            while (Bin.Length % 4 != 0)
            {
                Bin.WriteByte(0);
            }
            int offset = (int)Bin.Length;
            Bin.Write(raw, 0, raw.Length);
            var view = new JsonObject
            {
                ["buffer"] = 0,
                ["byteOffset"] = offset,
                ["byteLength"] = raw.Length,
            };
            if (target != 0)
            {
                view["target"] = target;
            }
            BufferViews.Add(view);
            return BufferViews.Count - 1;
        }

        public int AddFloat(float[] values, int components, string type, int target, bool minmax)
        {
            var raw = new byte[values.Length * 4];
            for (int i = 0; i < values.Length; i++)
            {
                BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(i * 4), values[i]);
            }
            int view = AddView(raw, target);
            var accessor = new JsonObject
            {
                ["bufferView"] = view,
                ["componentType"] = 5126,
                ["count"] = values.Length / components,
                ["type"] = type,
            };
            if (minmax && values.Length > 0)
            {
                var min = new float[components];
                var max = new float[components];
                for (int c = 0; c < components; c++)
                {
                    min[c] = float.MaxValue;
                    max[c] = float.MinValue;
                }
                for (int i = 0; i < values.Length; i += components)
                {
                    for (int c = 0; c < components; c++)
                    {
                        min[c] = MathF.Min(min[c], values[i + c]);
                        max[c] = MathF.Max(max[c], values[i + c]);
                    }
                }
                var minArray = new JsonArray();
                var maxArray = new JsonArray();
                foreach (float value in min)
                {
                    minArray.Add(value);
                }
                foreach (float value in max)
                {
                    maxArray.Add(value);
                }
                accessor["min"] = minArray;
                accessor["max"] = maxArray;
            }
            Accessors.Add(accessor);
            return Accessors.Count - 1;
        }

        public int AddUInt16(ushort[] values, int components, string type, int target)
        {
            var raw = new byte[values.Length * 2];
            for (int i = 0; i < values.Length; i++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(i * 2), values[i]);
            }
            int view = AddView(raw, target);
            Accessors.Add(new JsonObject
            {
                ["bufferView"] = view,
                ["componentType"] = 5123,
                ["count"] = values.Length / components,
                ["type"] = type,
            });
            return Accessors.Count - 1;
        }

        public int AddUInt32(uint[] values, int components, string type, int target)
        {
            var raw = new byte[values.Length * 4];
            for (int i = 0; i < values.Length; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(i * 4), values[i]);
            }
            int view = AddView(raw, target);
            Accessors.Add(new JsonObject
            {
                ["bufferView"] = view,
                ["componentType"] = 5125,
                ["count"] = values.Length / components,
                ["type"] = type,
            });
            return Accessors.Count - 1;
        }
    }

    private static string TextureUri(string path, uint replaceableId, string glbDirectory, string convertedRoot)
    {
        string name;
        if (replaceableId == 1)
        {
            name = "ReplaceableTextures/TeamColor/TeamColor00";
        }
        else if (replaceableId == 2)
        {
            name = "ReplaceableTextures/TeamGlow/TeamGlow00";
        }
        else
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            name = Path.ChangeExtension(path.Replace('\\', '/'), null);
        }

        string target = Path.GetFullPath(Path.Combine(convertedRoot, name + ".png"));
        return Path.GetRelativePath(glbDirectory, target).Replace('\\', '/');
    }

    /// <summary>组装 .glb，返回文件字节与解析用的 JSON（便于调用方核对）。</summary>
    public static (byte[] Blob, JsonObject Gltf) Build(MdxDocument model, string glbDirectory, string convertedRoot)
    {
        var builder = new Builder();
        var nodes = new JsonArray();
        var meshes = new JsonArray();
        var materials = new JsonArray();
        var images = new List<string>();
        List<MdxNode> bones = model.Bones;
        float[] pivots = model.Pivots;

        // 骨骼和辅助节点都是场景图里的节点。骨骼的父节点经常是辅助节点，
        // 只按 objectId 建图会把层级断开，动画会丢一半。
        var allNodes = new List<MdxNode>(bones);
        allNodes.AddRange(model.Helpers);
        allNodes.Sort((a, b) => a.ObjectId.CompareTo(b.ObjectId));

        var pivotOf = new Dictionary<int, float[]>();
        foreach (MdxNode node in allNodes)
        {
            int offset = node.ObjectId * 3;
            pivotOf[node.ObjectId] = offset + 2 < pivots.Length
                ? new[] { pivots[offset], pivots[offset + 1], pivots[offset + 2] }
                : new[] { 0f, 0f, 0f };
        }

        var nodeOfObject = new Dictionary<int, int>();
        var translationOfObject = new Dictionary<int, float[]>();
        foreach (MdxNode node in allNodes)
        {
            MdxNode parent = allNodes.Find(candidate => candidate.ObjectId == node.ParentId);
            float[] pivot = pivotOf[node.ObjectId];
            float[] offset = parent != null && parent != node
                ? new[] { pivot[0] - pivotOf[parent.ObjectId][0], pivot[1] - pivotOf[parent.ObjectId][1], pivot[2] - pivotOf[parent.ObjectId][2] }
                : pivot;
            float[] converted = ConvertPosition(offset[0], offset[1], offset[2]);
            translationOfObject[node.ObjectId] = converted;

            var nodeJson = new JsonObject { ["name"] = $"node{node.ObjectId}" };
            var translation = new JsonArray();
            foreach (float value in converted)
            {
                translation.Add(value);
            }
            nodeJson["translation"] = translation;
            nodes.Add(nodeJson);
            nodeOfObject[node.ObjectId] = nodes.Count - 1;
        }

        var roots = new List<int>();
        foreach (MdxNode node in allNodes)
        {
            MdxNode parent = allNodes.Find(candidate => candidate.ObjectId == node.ParentId);
            int index = nodeOfObject[node.ObjectId];
            if (parent != null && parent != node)
            {
                int parentIndex = nodeOfObject[parent.ObjectId];
                JsonArray children = nodes[parentIndex]["children"] as JsonArray;
                if (children == null)
                {
                    children = new JsonArray();
                    nodes[parentIndex]["children"] = children;
                }
                children.Add(index);
            }
            else
            {
                roots.Add(index);
            }
        }

        // 关节的静止世界位置：累加父级偏移，用来算 inverseBindMatrices
        var worldOffset = new Dictionary<int, float[]>();
        foreach (MdxNode node in allNodes)
        {
            MdxNode parent = allNodes.Find(candidate => candidate.ObjectId == node.ParentId);
            float[] offset = translationOfObject[node.ObjectId];
            if (parent != null && parent != node && worldOffset.ContainsKey(parent.ObjectId))
            {
                float[] parentOffset = worldOffset[parent.ObjectId];
                worldOffset[node.ObjectId] = new[]
                {
                    parentOffset[0] + offset[0], parentOffset[1] + offset[1], parentOffset[2] + offset[2],
                };
            }
            else
            {
                worldOffset[node.ObjectId] = offset;
            }
        }

        JsonObject skin = null;
        bool anyVertexGroups = false;
        foreach (MdxGeoset geoset in model.Geosets)
        {
            if (geoset.VertexGroups != null)
            {
                anyVertexGroups = true;
                break;
            }
        }

        if (bones.Count > 0 && anyVertexGroups)
        {
            var matrices = new float[bones.Count * 16];
            for (int i = 0; i < bones.Count; i++)
            {
                float[] offset = worldOffset.TryGetValue(bones[i].ObjectId, out float[] value) ? value : new[] { 0f, 0f, 0f };
                int start = i * 16;
                matrices[start] = 1f;
                matrices[start + 5] = 1f;
                matrices[start + 10] = 1f;
                matrices[start + 15] = 1f;
                matrices[start + 3] = -offset[0];
                matrices[start + 7] = -offset[1];
                matrices[start + 11] = -offset[2];
            }

            int inverseBind = builder.AddFloat(matrices, 16, "MAT4", 0, false);
            var joints = new JsonArray();
            foreach (MdxNode bone in bones)
            {
                joints.Add(nodeOfObject[bone.ObjectId]);
            }
            skin = new JsonObject
            {
                ["joints"] = joints,
                ["inverseBindMatrices"] = inverseBind,
            };
        }

        var objectToJoint = new Dictionary<int, int>();
        for (int i = 0; i < bones.Count; i++)
        {
            objectToJoint[bones[i].ObjectId] = i;
        }

        int TextureIndex(string path, uint replaceableId)
        {
            string uri = TextureUri(path, replaceableId, glbDirectory, convertedRoot);
            if (uri == null)
            {
                return -1;
            }
            int existing = images.IndexOf(uri);
            if (existing >= 0)
            {
                return existing;
            }
            images.Add(uri);
            return images.Count - 1;
        }

        var sceneChildren = new JsonArray();
        foreach (int root in roots)
        {
            sceneChildren.Add(root);
        }

        for (int geosetIndex = 0; geosetIndex < model.Geosets.Count; geosetIndex++)
        {
            MdxGeoset geoset = model.Geosets[geosetIndex];
            if (geoset.Positions == null || geoset.Positions.Length == 0 || geoset.Indices.Length == 0)
            {
                continue;
            }
            int count = geoset.Positions.Length / 3;

            var positions = new float[count * 3];
            for (int i = 0; i < count; i++)
            {
                float[] converted = ConvertPosition(geoset.Positions[i * 3], geoset.Positions[i * 3 + 1], geoset.Positions[i * 3 + 2]);
                positions[i * 3] = converted[0];
                positions[i * 3 + 1] = converted[1];
                positions[i * 3 + 2] = converted[2];
            }

            var attributes = new JsonObject
            {
                ["POSITION"] = builder.AddFloat(positions, 3, "VEC3", 34962, true),
            };

            if (geoset.Normals != null && geoset.Normals.Length == count * 3)
            {
                var normals = new float[count * 3];
                for (int i = 0; i < count; i++)
                {
                    float[] converted = ConvertPosition(geoset.Normals[i * 3], geoset.Normals[i * 3 + 1], geoset.Normals[i * 3 + 2]);
                    normals[i * 3] = converted[0];
                    normals[i * 3 + 1] = converted[1];
                    normals[i * 3 + 2] = converted[2];
                }
                attributes["NORMAL"] = builder.AddFloat(normals, 3, "VEC3", 34962, false);
            }

            float[] uvs = geoset.Uvs;
            if (uvs == null || uvs.Length != count * 2)
            {
                uvs = new float[count * 2];
            }
            attributes["TEXCOORD_0"] = builder.AddFloat((float[])uvs.Clone(), 2, "VEC2", 34962, false);

            bool skinned = false;
            if (skin != null && geoset.VertexGroups != null && geoset.MatrixGroups != null && geoset.MatrixGroups.Length > 0)
            {
                var jointOfGroup = new int[geoset.MatrixGroups.Length];
                for (int i = 0; i < geoset.MatrixGroups.Length; i++)
                {
                    jointOfGroup[i] = objectToJoint.TryGetValue((int)geoset.MatrixGroups[i], out int joint) ? joint : -1;
                }

                var jointIndices = new ushort[count * 4];
                var jointWeights = new float[count * 4];
                for (int vertex = 0; vertex < count; vertex++)
                {
                    int group = geoset.VertexGroups[vertex];
                    if (group < jointOfGroup.Length && jointOfGroup[group] >= 0)
                    {
                        jointIndices[vertex * 4] = (ushort)jointOfGroup[group];
                        jointWeights[vertex * 4] = 1f;
                        skinned = true;
                    }
                }

                if (skinned)
                {
                    attributes["JOINTS_0"] = builder.AddUInt16(jointIndices, 4, "VEC4", 34962);
                    attributes["WEIGHTS_0"] = builder.AddFloat(jointWeights, 4, "VEC4", 34962, false);
                }
            }

            var primitive = new JsonObject { ["attributes"] = attributes };
            if (count > 65535)
            {
                primitive["indices"] = builder.AddUInt32(geoset.Indices, 1, "SCALAR", 34963);
            }
            else
            {
                var shorts = new ushort[geoset.Indices.Length];
                for (int i = 0; i < geoset.Indices.Length; i++)
                {
                    shorts[i] = (ushort)geoset.Indices[i];
                }
                primitive["indices"] = builder.AddUInt16(shorts, 1, "SCALAR", 34963);
            }
            primitive["mode"] = 4;

            int materialId = geoset.MaterialId;
            if (materialId >= 0 && materialId < model.Materials.Count)
            {
                MdxMaterial material = model.Materials[materialId];
                var jsonMaterial = new JsonObject
                {
                    ["name"] = $"mat{materialId}",
                    ["doubleSided"] = true,
                };
                if (material.Layers.Count > 0)
                {
                    MdxLayer layer = material.Layers[0];
                    int textureId = layer.TextureId;
                    if (textureId >= 0 && textureId < model.Textures.Count)
                    {
                        MdxTexture texture = model.Textures[textureId];
                        int index = TextureIndex(texture.Path, texture.ReplaceableId);
                        if (index >= 0)
                        {
                            jsonMaterial["pbrMetallicRoughness"] = new JsonObject
                            {
                                ["baseColorTexture"] = new JsonObject { ["index"] = index },
                                ["metallicFactor"] = 0.0,
                                ["roughnessFactor"] = 1.0,
                            };
                            (string mode, float cutoff) = FilterAlpha.TryGetValue(layer.FilterMode, out var entry)
                                ? entry
                                : ("BLEND", 0f);
                            jsonMaterial["alphaMode"] = mode;
                            if (mode == "MASK")
                            {
                                jsonMaterial["alphaCutoff"] = cutoff;
                            }
                            jsonMaterial["extensions"] = new JsonObject
                            {
                                ["KHR_materials_unlit"] = new JsonObject(),
                            };
                            jsonMaterial["extras"] = new JsonObject
                            {
                                ["wc3FilterMode"] = layer.FilterMode,
                                ["wc3Alpha"] = layer.Alpha,
                                ["wc3LayerCount"] = material.Layers.Count,
                            };
                        }
                    }
                }
                primitive["material"] = materials.Count;
                materials.Add(jsonMaterial);
            }

            meshes.Add(new JsonObject
            {
                ["name"] = $"geoset{geosetIndex}",
                ["primitives"] = new JsonArray { primitive },
            });

            var node = new JsonObject
            {
                ["name"] = $"geoset{geosetIndex}",
                ["mesh"] = meshes.Count - 1,
            };
            if (skinned)
            {
                node["skin"] = 0;
            }
            nodes.Add(node);
            sceneChildren.Add(nodes.Count - 1);
        }

        var animations = new JsonArray();
        foreach (MdxSequence sequence in model.Sequences)
        {
            uint start = sequence.Start;
            uint end = sequence.End;
            if (end <= start)
            {
                continue;
            }

            var channels = new JsonArray();
            var samplers = new JsonArray();

            foreach (MdxNode node in allNodes)
            {
                foreach (string path in new[] { "translation", "rotation", "scale" })
                {
                    if (!node.Tracks.TryGetValue(path, out MdxTrack track) || track.Keys.Count == 0)
                    {
                        continue;
                    }

                    // 0x80000000 位表示「不循环」，这类关键帧不参与序列采样
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
                        continue;
                    }
                    keys.Sort((a, b) => a.Frame.CompareTo(b.Frame));

                    var selected = new List<MdxKey>();
                    foreach (MdxKey key in keys)
                    {
                        if (key.Frame >= start && key.Frame <= end)
                        {
                            selected.Add(key);
                        }
                    }
                    if (selected.Count == 0)
                    {
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
                        bool hasAfter = false;
                        MdxKey after = default;
                        foreach (MdxKey key in keys)
                        {
                            if (key.Frame > end)
                            {
                                after = key;
                                hasAfter = true;
                                break;
                            }
                        }
                        if (hasBefore)
                        {
                            selected.Add(new MdxKey(start, before.Value));
                        }
                        if (hasAfter)
                        {
                            selected.Add(new MdxKey(end, after.Value));
                        }
                        if (selected.Count == 1)
                        {
                            selected.Add(new MdxKey(end, selected[0].Value));
                        }
                    }
                    if (selected.Count == 0)
                    {
                        continue;
                    }

                    var times = new float[selected.Count];
                    for (int i = 0; i < selected.Count; i++)
                    {
                        times[i] = (selected[i].Frame - start) / 1000f;
                    }

                    int components;
                    string typeName;
                    var values = new List<float>();
                    if (path == "rotation")
                    {
                        components = 4;
                        typeName = "VEC4";
                        foreach (MdxKey key in selected)
                        {
                            values.AddRange(ConvertQuaternion(key.Value));
                        }
                    }
                    else if (path == "translation")
                    {
                        components = 3;
                        typeName = "VEC3";
                        foreach (MdxKey key in selected)
                        {
                            values.AddRange(ConvertPosition(key.Value[0], key.Value[1], key.Value[2]));
                        }
                    }
                    else
                    {
                        components = 3;
                        typeName = "VEC3";
                        foreach (MdxKey key in selected)
                        {
                            values.AddRange(ConvertScale(key.Value[0], key.Value[1], key.Value[2]));
                        }
                    }

                    samplers.Add(new JsonObject
                    {
                        ["input"] = builder.AddFloat(times, 1, "SCALAR", 0, true),
                        ["output"] = builder.AddFloat(values.ToArray(), components, typeName, 0, false),
                        ["interpolation"] = "LINEAR",
                    });
                    channels.Add(new JsonObject
                    {
                        ["sampler"] = samplers.Count - 1,
                        ["target"] = new JsonObject
                        {
                            ["node"] = nodeOfObject[node.ObjectId],
                            ["path"] = path,
                        },
                    });
                }
            }

            if (channels.Count > 0)
            {
                animations.Add(new JsonObject
                {
                    ["name"] = string.IsNullOrEmpty(sequence.Name) ? "seq" : sequence.Name,
                    ["channels"] = channels,
                    ["samplers"] = samplers,
                });
            }
        }

        byte[] binary = builder.Bin.ToArray();
        int paddedBinary = (binary.Length + 3) / 4 * 4;

        var gltf = new JsonObject
        {
            ["asset"] = new JsonObject
            {
                ["version"] = "2.0",
                ["generator"] = "Godot --mdx-export (WorldEditor)",
            },
            ["scene"] = 0,
            ["scenes"] = new JsonArray { new JsonObject { ["nodes"] = sceneChildren } },
            ["nodes"] = nodes,
            ["meshes"] = meshes,
            ["accessors"] = builder.Accessors,
            ["bufferViews"] = builder.BufferViews,
            ["buffers"] = new JsonArray { new JsonObject { ["byteLength"] = paddedBinary } },
            ["extensionsUsed"] = new JsonArray { "KHR_materials_unlit" },
        };
        if (materials.Count > 0)
        {
            gltf["materials"] = materials;
        }
        if (images.Count > 0)
        {
            var imageArray = new JsonArray();
            var textureArray = new JsonArray();
            for (int i = 0; i < images.Count; i++)
            {
                imageArray.Add(new JsonObject { ["uri"] = images[i] });
                textureArray.Add(new JsonObject { ["source"] = i });
            }
            gltf["images"] = imageArray;
            gltf["textures"] = textureArray;
        }
        if (skin != null)
        {
            gltf["skins"] = new JsonArray { skin };
        }
        if (animations.Count > 0)
        {
            gltf["animations"] = animations;
        }

        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
        };
        byte[] json = System.Text.Encoding.UTF8.GetBytes(gltf.ToJsonString(options));
        int paddedJson = (json.Length + 3) / 4 * 4;

        int total = 12 + 8 + paddedJson + 8 + paddedBinary;
        using var stream = new MemoryStream(total);
        using var writer = new BinaryWriter(stream);
        writer.Write(new[] { (byte)'g', (byte)'l', (byte)'T', (byte)'F' });
        writer.Write(2u);
        writer.Write((uint)total);

        writer.Write((uint)paddedJson);
        writer.Write(new[] { (byte)'J', (byte)'S', (byte)'O', (byte)'N' });
        writer.Write(json);
        for (int i = json.Length; i < paddedJson; i++)
        {
            writer.Write((byte)' ');
        }

        writer.Write((uint)paddedBinary);
        writer.Write(new[] { (byte)'B', (byte)'I', (byte)'N', (byte)0 });
        writer.Write(binary);
        for (int i = binary.Length; i < paddedBinary; i++)
        {
            writer.Write((byte)0);
        }

        writer.Flush();
        return (stream.ToArray(), gltf);
    }
}
