using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Formats.Mdx;

/// <summary>动画轨道：MDX 用「毫秒帧号 + 定长向量」表示关键帧。</summary>
public sealed class MdxTrack
{
    public uint Interpolation;
    public readonly List<MdxKey> Keys = new();
}

public readonly struct MdxKey
{
    public readonly uint Frame;
    public readonly float[] Value;

    public MdxKey(uint frame, float[] value)
    {
        Frame = frame;
        Value = value;
    }
}

/// <summary>骨骼 / 辅助节点 / 附着点共用同一种记录格式。</summary>
public sealed class MdxNode
{
    public int ObjectId;
    public int ParentId = -1;
    public uint Flags;
    public string AttachmentPath = "";
    public int AttachmentId;
    public readonly Dictionary<string, MdxTrack> Tracks = new(StringComparer.Ordinal);
}

public sealed class MdxLayer
{
    public uint FilterMode;
    public uint ShadingFlags;
    public int TextureId = -1;
    public int TextureAnimationId = -1;
    public uint CoordId;
    public float Alpha = 1f;
}

public sealed class MdxMaterial
{
    public uint Priority;
    public uint Flags;
    public readonly List<MdxLayer> Layers = new();
}

public sealed class MdxTexture
{
    public uint ReplaceableId;
    public string Path = "";
}

public sealed class MdxSequence
{
    public string Name = "";
    public uint Start;
    public uint End;
}

public sealed class MdxGeoset
{
    public float[] Positions;
    public float[] Normals;
    public float[] Uvs;
    public uint[] Indices = Array.Empty<uint>();
    public byte[] VertexGroups;
    public uint[] MatrixGroups;
    public int MaterialId;
}

/// <summary>
/// Warcraft III 的 MDX（v800 经典模型）解析器。
/// 只做解析，不做坐标变换；glTF 组装在 <see cref="GlbWriter"/> 里。
/// </summary>
public sealed class MdxDocument
{
    public string Name = "";
    public readonly List<MdxSequence> Sequences = new();
    public readonly List<MdxTexture> Textures = new();
    public readonly List<MdxMaterial> Materials = new();
    public readonly List<MdxGeoset> Geosets = new();
    public readonly List<MdxNode> Bones = new();
    public readonly List<MdxNode> Helpers = new();
    public readonly List<MdxNode> Attachments = new();
    public readonly List<MdxNode> EventObjects = new();
    public readonly List<uint> GlobalSequences = new();
    public readonly Dictionary<string, int> Collisions = new();
    public float[] Pivots = Array.Empty<float>();
    public int Particles;
    public int Ribbons;

    public static MdxDocument Parse(byte[] data)
    {
        if (data.Length < 8 || Encoding.ASCII.GetString(data, 0, 4) != "MDLX")
        {
            throw new InvalidDataException("不是 MDX 文件");
        }

        var document = new MdxDocument();
        var reader = new MdxReader(data, 4);

        while (reader.Position + 8 <= data.Length)
        {
            string tag = reader.Tag();
            int size = (int)reader.U32();
            int end = reader.Position + size;
            if (size <= 0 || end > data.Length || end <= reader.Position)
            {
                // size 为 0 时位置不动，会原地打转
                break;
            }

            switch (tag)
            {
                case "MODL":
                    document.Name = reader.String(80);
                    break;

                case "SEQS":
                    // 没有 count 字段，每条 132 字节（128 内容 + 4 保留）
                    for (int i = 0; i < size / 132; i++)
                    {
                        var sequence = new MdxSequence { Name = reader.String(80) };
                        sequence.Start = reader.U32();
                        sequence.End = reader.U32();
                        reader.Skip(4 + 4 + 4 + 4 + 24 + 4);
                        document.Sequences.Add(sequence);
                    }
                    break;

                case "GLBS":
                    for (int i = 0; i < size / 4; i++)
                    {
                        document.GlobalSequences.Add(reader.U32());
                    }
                    break;

                case "TEXS":
                    // 没有 count 字段，每条 268 字节（replaceableId + path[260] + flags）
                    for (int i = 0; i < size / 268; i++)
                    {
                        var texture = new MdxTexture { ReplaceableId = reader.U32() };
                        texture.Path = reader.String(260);
                        reader.U32();
                        document.Textures.Add(texture);
                    }
                    break;

                case "MTLS":
                    ReadRecords(reader, end, data, record => document.Materials.Add(ReadMaterial(reader, record)));
                    break;

                case "GEOS":
                    ReadRecords(reader, end, data, record => document.Geosets.Add(ReadGeoset(reader, record)));
                    break;

                case "BONE":
                    ReadNodes(reader, end, data, document.Bones);
                    break;

                case "HELP":
                    ReadNodes(reader, end, data, document.Helpers);
                    break;

                case "ATCH":
                    ReadNodes(reader, end, data, document.Attachments);
                    break;

                case "EVTS":
                    ReadNodes(reader, end, data, document.EventObjects);
                    break;

                case "PIVT":
                    document.Pivots = reader.Floats((size / 12) * 3);
                    break;

                case "CLID":
                    ReadRecords(reader, end, data, record =>
                    {
                        reader.U32();
                        uint kind = reader.U32();
                        string key = kind.ToString();
                        document.Collisions[key] = document.Collisions.TryGetValue(key, out int count) ? count + 1 : 1;
                    });
                    break;

                case "PRE2":
                case "PREM":
                    document.Particles += CountRecords(reader, end, data);
                    break;

                case "RIBB":
                    document.Ribbons += CountRecords(reader, end, data);
                    break;

                case "LITE":
                case "CAMS":
                case "BPOS":
                    CountRecords(reader, end, data);
                    break;
            }

            reader.Position = end;
        }

        return document;
    }

    private static void ReadRecords(MdxReader reader, int end, byte[] data, Action<int> handler)
    {
        while (reader.Position + 8 <= end)
        {
            int cursor = reader.Position;
            int record = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(cursor));
            if (record <= 0 || cursor + record > end)
            {
                break;
            }
            handler(record);
            reader.Position = cursor + record;
        }
    }

    private static int CountRecords(MdxReader reader, int end, byte[] data)
    {
        int count = 0;
        while (reader.Position + 8 <= end)
        {
            int cursor = reader.Position;
            int record = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(cursor));
            if (record <= 0 || cursor + record > end)
            {
                break;
            }
            reader.Position = cursor + record;
            count++;
        }
        return count;
    }

    /// <summary>
    /// 节点块每条记录后面是否多 8 字节（geosetId / geosetAnimationId）在不同块里不一致，
    /// 用「哪种步长正好走完整块」自动判定。
    /// </summary>
    private static int NodeStride(byte[] data, int start, int end)
    {
        foreach (int delta in new[] { 8, 0 })
        {
            int position = start;
            int count = 0;
            while (position + 8 <= end)
            {
                int record = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position));
                if (record <= 0 || position + record > end)
                {
                    break;
                }
                position += record + delta;
                count++;
            }
            if (count > 0 && position == end)
            {
                return delta;
            }
        }
        return 0;
    }

    private static void ReadNodes(MdxReader reader, int end, byte[] data, List<MdxNode> target)
    {
        int stride = NodeStride(data, reader.Position, end);
        while (reader.Position + 8 <= end)
        {
            int cursor = reader.Position;
            int record = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(cursor));
            if (record <= 0 || cursor + record > end)
            {
                break;
            }
            target.Add(ReadNode(reader, record));
            reader.Position = cursor + record + stride;
        }
    }

    private static MdxNode ReadNode(MdxReader reader, int size)
    {
        int end = reader.Position + size;
        reader.U32();
        reader.String(80);
        var node = new MdxNode
        {
            ObjectId = (int)reader.U32(),
            ParentId = reader.I32(),
            Flags = reader.U32(),
        };

        while (reader.Position + 8 <= end)
        {
            int before = reader.Position;
            string tag = reader.Tag();
            int length = (int)reader.U32();
            int body = reader.Position;
            switch (tag)
            {
                case "GEOS":
                    reader.U32();
                    reader.U32();
                    break;
                case "KGTR":
                    node.Tracks["translation"] = ReadTrack(reader, 3);
                    break;
                case "KGRT":
                    node.Tracks["rotation"] = ReadTrack(reader, 4);
                    break;
                case "KGSC":
                    node.Tracks["scale"] = ReadTrack(reader, 3);
                    break;
                case "ATCH":
                    node.AttachmentPath = reader.String(260);
                    reader.U32();
                    node.AttachmentId = (int)reader.U32();
                    break;
            }
            reader.Position = body + length;
            if (reader.Position <= before)
            {
                // 畸形长度会让位置不前进，兜底跳过这条记录
                reader.Position = before + 8;
            }
        }

        reader.Position = end;
        return node;
    }

    private static MdxTrack ReadTrack(MdxReader reader, int width)
    {
        int count = (int)reader.U32();
        uint interpolation = reader.U32();
        var track = new MdxTrack { Interpolation = interpolation };
        for (int i = 0; i < count && reader.Position + width * 4 <= reader.Data.Length; i++)
        {
            uint frame = reader.U32();
            float[] value = reader.Floats(width);
            if (interpolation > 1)
            {
                // 贝塞尔切线，这里用不到（glTF 侧统一 LINEAR），跳过
                reader.Floats(width * 2);
            }
            track.Keys.Add(new MdxKey(frame, value));
        }
        return track;
    }

    private static MdxMaterial ReadMaterial(MdxReader reader, int size)
    {
        int end = reader.Position + size;
        reader.U32();
        var material = new MdxMaterial { Priority = reader.U32(), Flags = reader.U32() };

        while (reader.Position + 8 <= end)
        {
            int before = reader.Position;
            string tag = reader.Tag();
            int length = (int)reader.U32();
            int body = reader.Position;
            if (tag == "LAYS")
            {
                // LAYS 的第二个字段是「层数」而不是字节长度：每层自带 inclusiveSize，
                // 层内还有贴图动画之类的子块，所以必须按层大小逐层跳过。
                int layerCount = length;
                int cursor = body;
                for (int i = 0; i < layerCount && i < 8; i++)
                {
                    int layerSize = BinaryPrimitives.ReadInt32LittleEndian(reader.Data.AsSpan(cursor));
                    if (layerSize < 28 || cursor + layerSize > end)
                    {
                        break;
                    }
                    material.Layers.Add(new MdxLayer
                    {
                        FilterMode = BinaryPrimitives.ReadUInt32LittleEndian(reader.Data.AsSpan(cursor + 4)),
                        ShadingFlags = BinaryPrimitives.ReadUInt32LittleEndian(reader.Data.AsSpan(cursor + 8)),
                        TextureId = BinaryPrimitives.ReadInt32LittleEndian(reader.Data.AsSpan(cursor + 12)),
                        TextureAnimationId = BinaryPrimitives.ReadInt32LittleEndian(reader.Data.AsSpan(cursor + 16)),
                        CoordId = BinaryPrimitives.ReadUInt32LittleEndian(reader.Data.AsSpan(cursor + 20)),
                        Alpha = BinaryPrimitives.ReadSingleLittleEndian(reader.Data.AsSpan(cursor + 24)),
                    });
                    cursor += layerSize;
                }
                reader.Position = cursor;
            }
            else
            {
                reader.Position = body + length;
            }
            if (reader.Position <= before)
            {
                reader.Position = before + 8;
            }
        }

        reader.Position = end;
        return material;
    }

    private static MdxGeoset ReadGeoset(MdxReader reader, int size)
    {
        int end = reader.Position + size;
        reader.U32();
        var geoset = new MdxGeoset();
        var types = new List<uint>();
        var counts = new List<uint>();
        uint[] rawIndices = null;
        bool seenMaterials = false;

        while (reader.Position + 8 <= end)
        {
            int before = reader.Position;
            string tag = reader.Tag();
            int count = (int)reader.U32();
            int body = reader.Position;

            switch (tag)
            {
                case "VRTX":
                    geoset.Positions = reader.Floats(count * 3);
                    break;
                case "NRMS":
                    geoset.Normals = reader.Floats(count * 3);
                    break;
                case "PTYP":
                    for (int i = 0; i < count; i++)
                    {
                        types.Add(reader.U32());
                    }
                    break;
                case "PCNT":
                    for (int i = 0; i < count; i++)
                    {
                        counts.Add(reader.U32());
                    }
                    break;
                case "PVTX":
                    rawIndices = new uint[count];
                    for (int i = 0; i < count; i++)
                    {
                        rawIndices[i] = BinaryPrimitives.ReadUInt16LittleEndian(reader.Data.AsSpan(body + i * 2));
                    }
                    reader.Position = body + count * 2;
                    break;
                case "GNDX":
                    geoset.VertexGroups = new byte[count];
                    Array.Copy(reader.Data, body, geoset.VertexGroups, 0, count);
                    reader.Position = body + count;
                    break;
                case "MTGC":
                    geoset.MatrixGroups = new uint[count];
                    for (int i = 0; i < count; i++)
                    {
                        geoset.MatrixGroups[i] = BinaryPrimitives.ReadUInt32LittleEndian(reader.Data.AsSpan(body + i * 4));
                    }
                    reader.Position = body + count * 4;
                    break;
                case "MATS":
                    geoset.MaterialId = (int)BinaryPrimitives.ReadUInt32LittleEndian(reader.Data.AsSpan(body));
                    reader.Position = body + count * 4;
                    seenMaterials = true;
                    continue;
                case "UVBS":
                    geoset.Uvs = reader.Floats(count * 2);
                    break;
                default:
                    reader.Position = body;
                    break;
            }

            if (seenMaterials)
            {
                // MATS 之后是 selectionGroup / bounds / extents，长度不固定，
                // 直接按标签找 UVBS，找不到就说明块结束了。
                int match = IndexOf(reader.Data, "UVBS", reader.Position, end);
                if (match < 0)
                {
                    break;
                }
                reader.Position = match;
            }

            if (reader.Position <= before)
            {
                // 未知标签或畸形长度：强制跳过这条，避免原地打转
                reader.Position = before + 8;
            }
        }

        reader.Position = end;
        geoset.Indices = BuildTriangles(rawIndices, types, counts);
        return geoset;
    }

    private static uint[] BuildTriangles(uint[] rawIndices, List<uint> types, List<uint> counts)
    {
        if (rawIndices == null)
        {
            return Array.Empty<uint>();
        }

        var triangles = new List<uint>();
        int cursor = 0;
        for (int index = 0; index < types.Count && index < counts.Count; index++)
        {
            int count = (int)counts[index];
            if (cursor + count > rawIndices.Length)
            {
                break;
            }
            uint kind = types[index];
            if (kind == 4)
            {
                for (int i = 0; i + 2 < count; i += 3)
                {
                    triangles.Add(rawIndices[cursor + i]);
                    triangles.Add(rawIndices[cursor + i + 1]);
                    triangles.Add(rawIndices[cursor + i + 2]);
                }
            }
            else if (kind == 5)
            {
                for (int i = 0; i + 2 < count; i++)
                {
                    uint a = rawIndices[cursor + i];
                    uint b = rawIndices[cursor + i + 1];
                    uint c = rawIndices[cursor + i + 2];
                    if (i % 2 == 0)
                    {
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(c);
                    }
                    else
                    {
                        triangles.Add(a);
                        triangles.Add(c);
                        triangles.Add(b);
                    }
                }
            }
            else if (kind == 6)
            {
                for (int i = 0; i + 2 < count; i++)
                {
                    triangles.Add(rawIndices[cursor]);
                    triangles.Add(rawIndices[cursor + i + 1]);
                    triangles.Add(rawIndices[cursor + i + 2]);
                }
            }
            cursor += count;
        }
        return triangles.ToArray();
    }

    private static int IndexOf(byte[] data, string pattern, int start, int end)
    {
        byte a = (byte)pattern[0];
        byte b = (byte)pattern[1];
        byte c = (byte)pattern[2];
        byte d = (byte)pattern[3];
        int limit = Math.Min(end, data.Length) - 4;
        for (int i = Math.Max(start, 0); i <= limit; i++)
        {
            if (data[i] == a && data[i + 1] == b && data[i + 2] == c && data[i + 3] == d)
            {
                return i;
            }
        }
        return -1;
    }
}

/// <summary>按小端从前到后读 MDX 的小工具。</summary>
internal sealed class MdxReader
{
    public readonly byte[] Data;
    public int Position;

    public MdxReader(byte[] data, int start)
    {
        Data = data;
        Position = start;
    }

    // 畸形模型会读出越界位置，这里一律退化成一个默认值，行为和 Python 的切片一致

    public uint U32()
    {
        uint value = Position >= 0 && Position + 4 <= Data.Length
            ? BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(Position))
            : 0u;
        Position += 4;
        return value;
    }

    public int I32()
    {
        int value = Position >= 0 && Position + 4 <= Data.Length
            ? BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(Position))
            : 0;
        Position += 4;
        return value;
    }

    public float[] Floats(int count)
    {
        var values = new float[count];
        for (int i = 0; i < count; i++)
        {
            int offset = Position + i * 4;
            values[i] = offset >= 0 && offset + 4 <= Data.Length
                ? BinaryPrimitives.ReadSingleLittleEndian(Data.AsSpan(offset))
                : 0f;
        }
        Position += count * 4;
        return values;
    }

    public string String(int count)
    {
        if (Position >= Data.Length || Position < 0)
        {
            Position += count;
            return "";
        }
        int end = Math.Min(Position + count, Data.Length);
        int length = Position;
        while (length < end && Data[length] != 0)
        {
            length++;
        }
        string value = Encoding.Latin1.GetString(Data, Position, length - Position);
        Position += count;
        return value;
    }

    public string Tag()
    {
        string value = Position >= 0 && Position + 4 <= Data.Length
            ? Encoding.Latin1.GetString(Data, Position, 4)
            : "";
        Position += 4;
        return value;
    }

    public void Skip(int count)
    {
        Position += count;
    }
}
