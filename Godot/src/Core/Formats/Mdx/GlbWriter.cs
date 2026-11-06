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

    internal static float[] ConvertPosition(float x, float y, float z)
    {
        return new[] { x, z, -y };
    }

    internal static float[] ConvertScale(float x, float y, float z)
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

    internal static float[] ConvertQuaternion(float[] q)
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

    /// <summary>组装 .glb，返回文件字节与解析用的 JSON（便于调用方核对）。</summary>
    public static (byte[] Blob, JsonObject Gltf) Build(MdxDocument model, string glbDirectory, string convertedRoot)
    {
        var builder = new Builder();
        var nodes = new JsonArray();
        var meshes = new JsonArray();
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

        // 关节的静止世界位置：沿父链累加偏移，用来算 inverseBindMatrices。
        // 不能依赖 allNodes 的遍历顺序（父节点 objectId 比子节点大时会先算到子节点）。
        var worldOffset = new Dictionary<int, float[]>();
        var resolving = new HashSet<int>();
        float[] WorldOffsetOf(MdxNode node)
        {
            if (worldOffset.TryGetValue(node.ObjectId, out float[] cached))
            {
                return cached;
            }
            float[] local = translationOfObject[node.ObjectId];
            MdxNode parent = allNodes.Find(candidate => candidate.ObjectId == node.ParentId);
            float[] world = local;
            if (parent != null && parent != node && resolving.Add(node.ObjectId))
            {
                float[] parentOffset = WorldOffsetOf(parent);
                world = new[]
                {
                    parentOffset[0] + local[0], parentOffset[1] + local[1], parentOffset[2] + local[2],
                };
            }
            resolving.Remove(node.ObjectId);
            worldOffset[node.ObjectId] = world;
            return world;
        }

        foreach (MdxNode node in allNodes)
        {
            WorldOffsetOf(node);
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
                // glTF 的 MAT4 按列主序存，平移在第 4 列（12/13/14），不是第 4 行（3/7/11）
                matrices[start + 12] = -offset[0];
                matrices[start + 13] = -offset[1];
                matrices[start + 14] = -offset[2];
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
            if (skin != null && geoset.VertexGroups != null
                && geoset.MatrixGroupSizes != null && geoset.MatrixGroupSizes.Length > 0
                && geoset.MatrixIds != null && geoset.MatrixIds.Length > 0)
            {
                // MTGC 是每组包含的骨骼个数，MATS 是展平的骨骼 objectId。
                // 第 g 组的骨骼是 MatrixIds[offset[g] .. offset[g]+sizes[g])，War3 在组内均分权重。
                int groupCount = geoset.MatrixGroupSizes.Length;
                var groupOffset = new int[groupCount];
                int cursor = 0;
                for (int i = 0; i < groupCount; i++)
                {
                    groupOffset[i] = cursor;
                    cursor += (int)geoset.MatrixGroupSizes[i];
                }

                var jointIndices = new ushort[count * 4];
                var jointWeights = new float[count * 4];
                for (int vertex = 0; vertex < count; vertex++)
                {
                    int group = geoset.VertexGroups[vertex];
                    if (group >= groupCount)
                    {
                        continue;
                    }
                    int start = groupOffset[group];
                    int size = (int)geoset.MatrixGroupSizes[group];
                    int used = 0;
                    for (int k = 0; k < size && used < 4; k++)
                    {
                        int index = start + k;
                        if (index >= geoset.MatrixIds.Length)
                        {
                            break;
                        }
                        if (!objectToJoint.TryGetValue((int)geoset.MatrixIds[index], out int joint))
                        {
                            continue;
                        }
                        jointIndices[vertex * 4 + used] = (ushort)joint;
                        used++;
                    }
                    if (used == 0)
                    {
                        continue;
                    }
                    // 超过 4 根骨骼时截断，权重在留下的几根里重新归一化
                    float weight = 1f / used;
                    for (int k = 0; k < used; k++)
                    {
                        jointWeights[vertex * 4 + k] = weight;
                    }
                    skinned = true;
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

        // 动画与材质不写进 glb：见 ModelBundle（材质 .tres / AnimationLibrary .tres / per-model .tscn）

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
        };
        if (skin != null)
        {
            gltf["skins"] = new JsonArray { skin };
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
