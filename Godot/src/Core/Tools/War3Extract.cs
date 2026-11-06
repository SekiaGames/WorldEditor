using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WorldEdit.Core.Formats.Mpq;

namespace WorldEdit.Core.Tools;

public static class War3Extract
{
    private static readonly string[] Order =
    {
        "WAR3.MPQ", "War3x.mpq", "War3xlocal.mpq", "War3Patch.mpq", "Patch127.mpq", "patch.mpq", "zhCN.mpq",
    };

    private static readonly HashSet<string> SkipExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm", ".js", ".css", ".w3m", ".w3x", ".w3n",
        ".mpq", ".exe", ".dll", ".asi", ".ax", ".pdb", ".bat", ".url",
    };

    private static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase)
    {
        "(listfile)", "(attributes)", "(signature)", "(user data)",
    };

    public static void Run(string war3Dir, string outDir, List<string> masterNames, bool keepUnnamed = false)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, ".gdignore"), string.Empty);

        var written = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var hashed = new List<(uint A, uint B, string Name)>(masterNames.Count);
        foreach (string name in masterNames)
        {
            hashed.Add((MpqCrypto.Hash(name, MpqCrypto.HashNameA), MpqCrypto.Hash(name, MpqCrypto.HashNameB), name));
        }

        foreach (string file in Order)
        {
            string path = Path.Combine(war3Dir, file);
            if (!File.Exists(path))
            {
                continue;
            }

            using var archive = MpqArchive.Open(path);
            archive.LoadListFile();
            archive.ResolveUnnamedBlocks();

            var byIndex = new Dictionary<int, string>();
            foreach (var pair in archive.KnownNames)
            {
                byIndex.TryAdd(pair.Value, pair.Key);
            }
            var byHash = new Dictionary<(uint, uint), string>();
            foreach (var item in hashed)
            {
                byHash.TryAdd((item.A, item.B), item.Name);
            }

            int named = 0;
            int unnamed = 0;
            long bytes = 0;

            for (int i = 0; i < archive.Blocks.Count; i++)
            {
                if (!archive.TryGetNameHashes(i, out uint a, out uint b))
                {
                    continue;
                }
                byHash.TryGetValue((a, b), out string resolved);
                string name = resolved;
                if (name == null && byIndex.TryGetValue(i, out string local))
                {
                    name = local;
                }

                byte[] data;
                if (name != null && !Skip.Contains(name))
                {
                    data = archive.TryRead(name);
                }
                else if (name != null)
                {
                    continue;
                }
                else
                {
                    if (!keepUnnamed)
                    {
                        continue;
                    }
                    data = TryReadByIndex(archive, i);
                }

                if (data == null || data.Length == 0)
                {
                    continue;
                }

                if (name != null && SkipExtensions.Contains(Path.GetExtension(name)))
                {
                    continue;
                }

                string target;
                if (name != null && !Skip.Contains(name))
                {
                    target = Path.Combine(outDir, name.Replace('\\', Path.DirectorySeparatorChar));
                    named++;
                }
                else
                {
                    target = Path.Combine(outDir, "_unnamed", Path.GetFileNameWithoutExtension(file), $"{i:D5}.bin");
                    unnamed++;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, data);
                written[target] = data.Length;
                bytes += data.Length;
            }

            Console.WriteLine($"[EXTRACT] {file,-16} 命名={named,6} 匿名={unnamed,5} 写入={bytes / 1048576.0,7:0.0} MB");
        }

        long total = 0;
        foreach (var pair in written)
        {
            total += pair.Value;
        }
        Console.WriteLine($"[EXTRACT] 合计文件 {written.Count} 个，{total / 1048576.0:0.0} MB -> {outDir}");
    }

    private static byte[] TryReadByIndex(MpqArchive archive, int index)
    {
        try
        {
            return archive.ReadBlock(index, null);
        }
        catch
        {
            return null;
        }
    }

    public static void RunWithDefaults(string war3Dir, string outDir)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in War3Cli.MasterNames(war3Dir))
        {
            names.Add(name);
        }

        foreach (string file in Order)
        {
            string path = Path.Combine(war3Dir, file);
            if (!File.Exists(path))
            {
                continue;
            }
            using var archive = MpqArchive.Open(path);
            archive.LoadListFile();
            foreach (string name in archive.KnownNames.Keys)
            {
                names.Add(name);
            }
        }

        Run(war3Dir, outDir, new List<string>(names));
    }
}
