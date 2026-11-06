using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Formats.W3x;

/// <summary>地图上摆放的一个单位或物品（war3mapUnits.doo 的一条记录）。</summary>
public sealed class UnitPlacement
{
    public string Id = "";
    public uint Variation;

    /// <summary>WC3 世界坐标（单位：1 格 = 128）。</summary>
    public float X;
    public float Y;
    public float Z;

    /// <summary>朝向，弧度，绕 Z 轴（WC3 是 Z-up）。</summary>
    public float Angle;

    public uint Player;
    public byte Flags;

    /// <summary>true 表示这是掉在地上的物品，不是单位。</summary>
    public bool IsItem;
}

/// <summary>
/// war3mapUnits.doo 解析器。
/// 版本 7 和 8 都支持；两者的差别是 v8 多了 itemTablePointer 和力量/敏捷/智力三个字段。
/// 记录本身是变长的（物品组、背包、技能、随机单位），所以必须按顺序读完再算下一条的起点。
/// </summary>
public sealed class W3oUnits
{
    public int Version { get; private set; }
    public int SubVersion { get; private set; }
    public readonly List<UnitPlacement> Entries = new();

    public IEnumerable<UnitPlacement> Units
    {
        get
        {
            foreach (UnitPlacement entry in Entries)
            {
                if (!entry.IsItem)
                {
                    yield return entry;
                }
            }
        }
    }

    public IEnumerable<UnitPlacement> Items
    {
        get
        {
            foreach (UnitPlacement entry in Entries)
            {
                if (entry.IsItem)
                {
                    yield return entry;
                }
            }
        }
    }

    /// <summary>
    /// <paramref name="classify"/> 返回 true 表示这条记录是物品（通常查 ItemData 表）。
    /// </summary>
    public static W3oUnits Parse(byte[] data, Func<string, bool> classify)
    {
        if (data.Length < 16 || Encoding.ASCII.GetString(data, 0, 4) != "W3do")
        {
            throw new InvalidDataException("不是 war3mapUnits.doo");
        }

        var result = new W3oUnits
        {
            Version = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4)),
            SubVersion = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8)),
        };
        int count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(12));
        if (count < 0 || count > 100000)
        {
            throw new InvalidDataException($"单位数量异常: {count}");
        }

        var reader = new DooReader(data, 16);
        for (int i = 0; i < count; i++)
        {
            var entry = new UnitPlacement
            {
                Id = reader.String(4),
                Variation = reader.U32(),
                X = reader.F32(),
                Y = reader.F32(),
                Z = reader.F32(),
                Angle = reader.F32(),
            };
            reader.Skip(12);                 // scale，实际不用（统一用 modelScale）
            entry.Flags = reader.U8();
            entry.Player = reader.U32();
            reader.Skip(2);                  // unknown1 / unknown2
            reader.U32();                    // health
            reader.U32();                    // mana
            if (result.Version >= 8)
            {
                reader.U32();                // itemTablePointer
            }

            int setCount = reader.Count(64);
            for (int set = 0; set < setCount; set++)
            {
                int itemCount = reader.Count(64);
                reader.Skip(itemCount * 8);  // 每个掉落物：id(4) + 概率(4)
            }

            reader.U32();                    // gold
            reader.F32();                    // targetAcquisition
            reader.U32();                    // level
            if (result.Version >= 8)
            {
                reader.Skip(12);             // 力量 / 敏捷 / 智力
            }

            int inventory = reader.Count(64);
            reader.Skip(inventory * 8);      // 每个物品：槽位(4) + id(4)

            int abilities = reader.Count(256);
            reader.Skip(abilities * 12);     // 每个技能：id(4) + 自动施法(4) + 等级(4)

            // 随机单位：0 = 4 字节、1 = 8 字节、2 = 计数 + 数据；
            // 其它取值（常见是 0xFFFFFFFF）表示没有随机单位，不占额外字节
            uint randomType = reader.U32();
            if (randomType == 0)
            {
                reader.Skip(4);
            }
            else if (randomType == 1)
            {
                reader.Skip(8);
            }
            else if (randomType == 2)
            {
                int randomCount = reader.Count(64);
                reader.Skip(randomCount * 8);
            }

            reader.U32();                    // customColor
            reader.U32();                    // waygate
            reader.U32();                    // creationNumber

            entry.IsItem = classify(entry.Id);
            result.Entries.Add(entry);
        }

        return result;
    }
}

/// <summary>带边界保护的顺序读取。畸形地图里字段长度可能是垃圾值，越界时按 0 处理。</summary>
internal sealed class DooReader
{
    private readonly byte[] _data;
    public int Position;

    public DooReader(byte[] data, int start)
    {
        _data = data;
        Position = start;
    }

    public byte U8()
    {
        byte value = Position < _data.Length ? _data[Position] : (byte)0;
        Position += 1;
        return value;
    }

    public uint U32()
    {
        uint value = Position >= 0 && Position + 4 <= _data.Length
            ? BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(Position))
            : 0u;
        Position += 4;
        return value;
    }

    public float F32()
    {
        float value = Position >= 0 && Position + 4 <= _data.Length
            ? BinaryPrimitives.ReadSingleLittleEndian(_data.AsSpan(Position))
            : 0f;
        Position += 4;
        return value;
    }

    public string String(int count)
    {
        if (Position < 0 || Position + count > _data.Length)
        {
            Position += count;
            return "";
        }
        string value = Encoding.Latin1.GetString(_data, Position, count);
        Position += count;
        return value.TrimEnd('\0');
    }

    /// <summary>读一个计数并做上限钳制，防止垃圾值把指针带飞。</summary>
    public int Count(int limit)
    {
        int value = (int)U32();
        return value < 0 || value > limit ? 0 : value;
    }

    public void Skip(int count)
    {
        Position += count;
    }
}
