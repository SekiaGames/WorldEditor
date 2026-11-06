using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using WorldEdit.Core.Formats.Mpq;

namespace WorldEdit.Core.Tools;

public static class War3Scan
{
    private static readonly Regex PathPattern = new(
        @"[A-Za-z0-9_\\/\.\- ]{3,}\.(?:mdx|mdl|blp|tga|dds|wav|mp3)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] TextExtensions =
    {
        ".slk", ".txt", ".ini", ".lua", ".j", ".w3i", ".w3u", ".w3t", ".w3a", ".w3b", ".w3d",
        ".w3h", ".w3q", ".w3c", ".w3r", ".w3s", ".wct", ".wtg", ".imp", ".mmp", ".shd", ".w3e",
    };

    public static void Scan(string war3Dir)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in War3Cli.MasterNames(war3Dir))
        {
            names.Add(name);
        }

        int before = names.Count;
        string basePath = Path.Combine(war3Dir, "WAR3.MPQ");
        var seen = new List<string>(names);
        using (var archive = MpqArchive.Open(basePath))
        {
            foreach (string name in seen)
            {
                if (!Array.Exists(TextExtensions, e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                byte[] data = archive.TryRead(name);
                if (data == null || data.Length > 8 * 1024 * 1024)
                {
                    continue;
                }
                foreach (Match match in PathPattern.Matches(Encoding.Latin1.GetString(data)))
                {
                    names.Add(match.Value.Trim());
                }
            }
        }
        Console.WriteLine($"[SCAN] 名字 {before} -> {names.Count}（扫描 WAR3.MPQ 文本引用）");

        var hashes = new List<(uint A, uint B)>(names.Count);
        foreach (string name in names)
        {
            hashes.Add((MpqCrypto.Hash(name, MpqCrypto.HashNameA), MpqCrypto.Hash(name, MpqCrypto.HashNameB)));
        }

        foreach (string file in War3Cli.Archives)
        {
            string path = Path.Combine(war3Dir, file);
            if (!File.Exists(path))
            {
                continue;
            }
            using var archive = MpqArchive.Open(path);
            var index = War3Cli.HashIndex(archive);
            int covered = 0;
            foreach (var pair in hashes)
            {
                if (index.ContainsKey(pair))
                {
                    covered++;
                }
            }
            int blocks = archive.Blocks.Count;
            Console.WriteLine($"[SCAN] {file,-16} blocks={blocks,6} 命中名字={covered,6} 缺口={blocks - covered,6}");
        }
    }
}
