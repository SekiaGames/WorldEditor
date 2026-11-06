using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WorldEdit.Core.Tools;

public static class MpqCli
{
    public static void List(string mapPath)
    {
        using var archive = WorldEdit.Core.Formats.Mpq.MpqArchive.Open(mapPath);
        try
        {
            archive.LoadListFile();
        }
        catch (Exception e)
        {
            Console.WriteLine("[MPQ] listfile 读取失败: " + e.GetType().Name + " " + e.Message);
        }
        Console.WriteLine($"[MPQ] hash={archive.HashCount} blocks={archive.Blocks.Count} listfile={archive.KnownNames.Count}");

        foreach (var pair in archive.KnownNames)
        {
            var block = archive.Blocks[pair.Value];
            Console.WriteLine($"[MPQ] {pair.Key,-24} csize={block.CompressedSize,7} size={block.FileSize,7} flags=0x{block.Flags:X8} md5={Md5(archive.ReadBlock(pair.Value, pair.Key))}");
        }

        foreach (string name in new[]
                 {
                     "war3map.w3e", "war3map.w3i", "war3map.wts", "war3map.j",
                     "war3map.doo", "war3map.wpm", "war3mapUnits.doo",
                 })
        {
            byte[] data = archive.TryRead(name);
            Console.WriteLine(data == null
                ? $"[MPQ] {name,-20} MISSING"
                : $"[MPQ] {name,-20} {data.Length,7} md5={Md5(data)}");
        }
    }

    public static void Extract(string mapPath, string entry, string outPath)
    {
        using var archive = WorldEdit.Core.Formats.Mpq.MpqArchive.Open(mapPath);
        byte[] data = archive.TryRead(entry);
        if (data == null)
        {
            Console.WriteLine($"[MPQ] 读取失败: {entry}");
            return;
        }
        File.WriteAllBytes(outPath, data);
        Console.WriteLine($"[MPQ] {entry} -> {outPath} {data.Length} bytes md5={Md5(data)}");
    }

    /// <summary>
    /// 把地图 MPQ 里的具名条目按相对路径铺到一个目录，供 HiveWE 的目录模式打开。
    /// 匿名块没有名字，铺不出来，这里只统计数量。
    /// </summary>
    public static void Unpack(string mapPath, string outDir)
    {
        using var archive = WorldEdit.Core.Formats.Mpq.MpqArchive.Open(mapPath);
        archive.LoadListFile();
        archive.ResolveUnnamedBlocks();

        Directory.CreateDirectory(outDir);
        int written = 0;
        int failed = 0;
        foreach (var pair in archive.KnownNames)
        {
            string name = pair.Key.Replace('\\', '/').TrimStart('/');
            if (name.Length == 0 || name.Contains(".."))
            {
                continue;
            }

            byte[] data;
            try
            {
                data = archive.ReadBlock(pair.Value, pair.Key);
            }
            catch (Exception e)
            {
                failed++;
                Console.WriteLine($"[UNPACK] 跳过 {name}: {e.Message}");
                continue;
            }
            if (data == null)
            {
                continue;
            }

            string target = Path.Combine(outDir, name);
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? outDir);
            File.WriteAllBytes(target, data);
            written++;
        }

        Console.WriteLine($"[UNPACK] {Path.GetFileName(mapPath)} -> {outDir}: 写出 {written} 个文件, 失败 {failed}, 具名 {archive.KnownNames.Count}/{archive.Blocks.Count}");
    }

    public static void Stats(string path)
    {
        using var archive = WorldEdit.Core.Formats.Mpq.MpqArchive.Open(path);
        archive.LoadListFile();
        archive.ResolveUnnamedBlocks();
        long total = 0;
        foreach (var block in archive.Blocks)
        {
            total += block.FileSize;
        }
        Console.WriteLine($"[STAT] {System.IO.Path.GetFileName(path),-16} hash={archive.HashCount,6} blocks={archive.Blocks.Count,6} names={archive.KnownNames.Count,6} raw={total / 1048576.0:0.0} MB");
    }

    private static string Md5(byte[] data)
    {
        return data == null ? "null" : Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();
    }
}
