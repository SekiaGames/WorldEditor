using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WorldEdit.Core.Formats.Mpq;

namespace WorldEdit.Core.Tools;

public static class War3Cli
{
    public static readonly string[] Archives =
    {
        "WAR3.MPQ", "War3x.mpq", "War3xlocal.mpq", "War3Patch.mpq", "Patch127.mpq", "patch.mpq", "zhCN.mpq",
    };

    public static List<string> MasterNames(string war3Dir)
    {
        var names = new List<string>();
        string path = Path.Combine(war3Dir, "WAR3.MPQ");
        if (!File.Exists(path))
        {
            return names;
        }
        using var archive = MpqArchive.Open(path);
        byte[] list = archive.TryRead("(listfile)");
        if (list == null)
        {
            return names;
        }
        foreach (string line in Encoding.UTF8.GetString(list).Split('\n'))
        {
            string name = line.Trim('\r', ' ', '\0');
            if (name.Length > 0)
            {
                names.Add(name);
            }
        }
        return names;
    }

    /// <summary>
    /// 全部归档里能读到的 (listfile) 名字合集。单个主 listfile 只覆盖 WAR3.MPQ，
    /// 资料片里有少量表（AbilityMetaData / MiscMetaData 之类）名字不在其中，
    /// 但它们的 (listfile) 在别的归档里，合并后才能枚举全。
    /// </summary>
    public static List<string> AllNames(string war3Dir)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Archives)
        {
            string path = Path.Combine(war3Dir, file);
            if (!File.Exists(path))
            {
                continue;
            }
            try
            {
                using var archive = MpqArchive.Open(path);
                byte[] list = archive.TryRead("(listfile)");
                if (list == null)
                {
                    continue;
                }
                foreach (string line in Encoding.UTF8.GetString(list).Split('\n'))
                {
                    string name = line.Trim('\r', ' ', '\0');
                    if (name.Length > 0 && seen.Add(name))
                    {
                        names.Add(name);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[WAR3] {file} 的 listfile 读不了: {e.Message}");
            }
        }
        return names;
    }

    public static Dictionary<(uint, uint), int> HashIndex(MpqArchive archive)
    {
        var map = new Dictionary<(uint, uint), int>();
        for (int i = 0; i < archive.Blocks.Count; i++)
        {
            if (!archive.TryGetNameHashes(i, out uint a, out uint b))
            {
                continue;
            }
            map.TryAdd((a, b), i);
        }
        return map;
    }

    public static void Coverage(string war3Dir)
    {
        var names = MasterNames(war3Dir);
        Console.WriteLine($"[WAR3] 主 listfile 名字数 = {names.Count}");
        var hashes = new List<(uint A, uint B, string Name)>(names.Count);
        foreach (string name in names)
        {
            hashes.Add((MpqCrypto.Hash(name, MpqCrypto.HashNameA), MpqCrypto.Hash(name, MpqCrypto.HashNameB), name));
        }

        foreach (string file in Archives)
        {
            string path = Path.Combine(war3Dir, file);
            if (!File.Exists(path))
            {
                continue;
            }
            using var archive = MpqArchive.Open(path);
            var index = HashIndex(archive);
            var covered = new HashSet<int>();
            foreach (var item in hashes)
            {
                if (index.TryGetValue((item.A, item.B), out int block))
                {
                    covered.Add(block);
                }
            }
            long total = 0;
            foreach (var block in archive.Blocks)
            {
                total += block.FileSize;
            }
            Console.WriteLine($"[WAR3] {file,-16} blocks={archive.Blocks.Count,6} 覆盖={covered.Count,6} 缺失={archive.Blocks.Count - covered.Count,6} 解压后={total / 1048576.0:0.0} MB");
        }
    }
}
