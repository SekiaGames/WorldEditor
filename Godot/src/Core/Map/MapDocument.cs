using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using WorldEdit.Core.Formats.Mpq;
using WorldEdit.Core.Formats.W3x;

namespace WorldEdit.Core.Map;

public sealed class MapDocument
{
    public sealed class Entry
    {
        public string Name;
        public uint NameA;
        public uint NameB;
        public byte[] Data;
        public bool Compress;
    }

    public string SourcePath { get; private set; }
    public List<Entry> Entries { get; } = new();
    public W3eTerrain Terrain { get; private set; }
    public W3iInfo Info { get; private set; }

    /// <summary>地图上摆放的单位与物品（war3mapUnits.doo）。没有这个文件时是 null。</summary>
    public W3oUnits Units { get; private set; }
    public string MapName { get; private set; }

    /// <summary>
    /// .w3m/.w3x 在地图数据前有 512 字节的 HM3W 头（存地图名），MPQ 从 512 开始。
    /// 读进来存着，写回时原样带上，否则存出来的地图会少一段头。
    /// </summary>
    public byte[] Header { get; private set; }

    /// <summary>读不出来的条目（压缩算法还没实现等）。有内容说明这张图不是完整载入的。</summary>
    public List<string> Warnings { get; } = new();

    public static MapDocument Load(string path)
    {
        var document = new MapDocument { SourcePath = path };
        document.Header = MpqArchive.ReadMapHeader(path);
        using var archive = MpqArchive.Open(path);
        archive.LoadListFile();
        archive.ResolveUnnamedBlocks();

        var byIndex = new Dictionary<int, string>();
        foreach (var pair in archive.KnownNames)
        {
            byIndex[pair.Value] = pair.Key;
        }

        for (int i = 0; i < archive.Blocks.Count; i++)
        {
            string name = byIndex.TryGetValue(i, out string known) ? known : null;
            byte[] data;
            try
            {
                data = archive.ReadBlock(i, name);
            }
            catch (Exception e)
            {
                document.Warnings.Add($"{name ?? $"#{i}"}: {e.Message}");
                continue;
            }
            if (data == null)
            {
                continue;
            }
            if (!archive.TryGetNameHashes(i, out uint nameA, out uint nameB))
            {
                continue;
            }
            document.Entries.Add(new Entry
            {
                Name = name,
                NameA = nameA,
                NameB = nameB,
                Data = data,
                Compress = (archive.Blocks[i].Flags & 0x00000200) != 0,
            });
        }

        document.Build();
        return document;
    }

    /// <summary>
    /// 从解包目录（war3map.w3e 之类的散文件）加载。HiveWE 只吃目录模式，
    /// 调试时两边要打开同一份数据，所以这里也支持目录，省掉来回打包。
    /// </summary>
    public static MapDocument LoadDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"目录不存在: {directory}");
        }

        var document = new MapDocument { SourcePath = directory };
        foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetRelativePath(directory, file).Replace('\\', '/');
            document.Entries.Add(new Entry
            {
                Name = name,
                Data = File.ReadAllBytes(file),
                Compress = false,
            });
        }

        document.Build();
        return document;
    }

    /// <summary>entries 就绪之后解析 w3i / w3e / wts。</summary>
    private void Build()
    {
        byte[] w3i = Find("war3map.w3i");
        byte[] w3e = Find("war3map.w3e");
        byte[] wts = Find("war3map.wts");
        if (w3i == null || w3e == null)
        {
            throw new InvalidDataException("地图缺少 war3map.w3i 或 war3map.w3e");
        }

        Info = W3iInfo.Parse(w3i);
        Terrain = W3eTerrain.Parse(w3e);

        byte[] unitData = Find("war3mapUnits.doo");
        if (unitData != null)
        {
            try
            {
                Units = W3oUnits.Parse(unitData, IsItemId);
            }
            catch (Exception exception)
            {
                Warnings.Add($"war3mapUnits.doo 解析失败: {exception.Message}");
            }
        }

        // 名字优先取 w3i 里的字符串表引用（需要 war3map.wts），
        // wts 读不到时（有些图用还没实现的压缩）退回 HM3W 头里存的地图名。
        string resolved = wts != null ? Info.ResolveName(WtsDocument.Parse(wts)) : Info.NameRef;
        if (string.IsNullOrWhiteSpace(resolved) || resolved.StartsWith("TRIGSTR_", StringComparison.OrdinalIgnoreCase))
        {
            resolved = HeaderName(Header) ?? resolved;
        }
        MapName = resolved;
    }

    /// <summary>
    /// war3mapUnits.doo 把单位和地上的物品混在同一个列表里，靠 ID 区分：
    /// 在 ItemData 表里、且不在 UnitData 表里的就是物品。
    /// </summary>
    private static bool IsItemId(string id)
    {
        var items = WorldEdit.Assets.Generated.War3Tables.ItemData();
        var units = WorldEdit.Assets.Generated.War3Tables.UnitData();
        return items.FindRow("itemID", id) >= 0 && units.FindRow("unitID", id) < 0;
    }

    /// <summary>HM3W 头里 8 字节之后就是地图名字符串。</summary>
    private static string HeaderName(byte[] header)
    {
        if (header == null || header.Length < 16)
        {
            return null;
        }
        int end = 8;
        while (end < header.Length && header[end] != 0)
        {
            end++;
        }
        return end > 8 ? Encoding.UTF8.GetString(header, 8, end - 8) : null;
    }

    /// <summary>
    /// 释放内存里持有的地图数据。打开下一张图之前调用：entries 里装着 MPQ 解出来的
    /// 全部原始字节（大图几十 MB），不断开引用会一直等到 GC 才回收。
    /// </summary>
    public void Release()
    {
        Entries.Clear();
        Terrain = null;
        Info = null;
        Units = null;
        Header = null;
    }

    public byte[] Find(string name)
    {
        foreach (var entry in Entries)
        {
            if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Data;
            }
        }
        return null;
    }

    private void Replace(string name, byte[] data)
    {
        foreach (var entry in Entries)
        {
            if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                entry.Data = data;
                return;
            }
        }
        Entries.Add(new Entry
        {
            Name = name,
            NameA = MpqCrypto.Hash(name, MpqCrypto.HashNameA),
            NameB = MpqCrypto.Hash(name, MpqCrypto.HashNameB),
            Data = data,
            Compress = true,
        });
    }

    private void EnsureListFileComplete()
    {
        var missing = new List<string>();
        foreach (var entry in Entries)
        {
            if (entry.Name != null && !entry.Name.StartsWith("_Locales", StringComparison.OrdinalIgnoreCase))
            {
                missing.Add(entry.Name);
            }
        }

        var existing = Find("(listfile)");
        string text = existing != null ? Encoding.UTF8.GetString(existing) : string.Empty;
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in text.Split('\n'))
        {
            known.Add(line.Trim('\r', ' ', '\0'));
        }

        var builder = new StringBuilder(text);
        if (builder.Length > 0 && builder[^1] != '\n')
        {
            builder.Append("\r\n");
        }
        bool added = false;
        foreach (string name in missing)
        {
            if (known.Contains(name))
            {
                continue;
            }
            builder.Append(name).Append("\r\n");
            added = true;
        }

        if (added || existing == null)
        {
            Replace("(listfile)", Encoding.UTF8.GetBytes(builder.ToString()));
        }
    }

    public void SetMapName(string name)
    {
        int stringId = W3iInfo.ExtractStringId(Info.NameRef);
        if (stringId < 0)
        {
            throw new InvalidOperationException($"w3i 里的地图名不是 TRIGSTR 引用：{Info.NameRef}");
        }

        bool changed = false;
        foreach (var entry in Entries)
        {
            if (entry.Name == null || !entry.Name.EndsWith("war3map.wts", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var strings = WtsDocument.Parse(entry.Data);
            if (strings.Get(stringId) == null)
            {
                continue;
            }
            strings.Set(stringId, name);
            entry.Data = strings.ToBytes();
            changed = true;
        }

        if (!changed)
        {
            throw new InvalidOperationException("没有可修改的 war3map.wts");
        }
        MapName = name;
    }

    public void Save(string path)
    {
        EnsureListFileComplete();

        var writer = new MpqWriter();
        foreach (var entry in Entries)
        {
            if (entry.Name != null)
            {
                writer.Add(entry.Name, entry.Data, entry.Compress);
            }
            else
            {
                writer.AddHashed(entry.NameA, entry.NameB, entry.Data, entry.Compress);
            }
        }
        writer.Write(path, Header);
    }
}
