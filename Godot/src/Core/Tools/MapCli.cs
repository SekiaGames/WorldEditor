using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using WorldEdit.Core.Formats.Mpq;
using WorldEdit.Core.Map;

namespace WorldEdit.Core.Tools;

public static class MapCli
{
    public static void SaveAs(string source, string target, string newName)
    {
        var document = MapDocument.Load(source);
        Console.WriteLine($"[MAP] 载入 {document.Entries.Count} 个文件, 名称='{document.MapName}', 尺寸={document.Terrain.Width}x{document.Terrain.Height}");

        var before = new Dictionary<string, string>();
        foreach (var entry in document.Entries)
        {
            before[Key(entry)] = Md5(entry.Data);
        }

        document.SetMapName(newName);
        document.Save(target);
        Console.WriteLine($"[MAP] 已写入 {target} ({new FileInfo(target).Length} bytes)");

        var reloaded = MapDocument.Load(target);
        Console.WriteLine($"[MAP] 重新打开: 名称='{reloaded.MapName}', 文件数={reloaded.Entries.Count}");

        int same = 0;
        int diff = 0;
        foreach (var entry in reloaded.Entries)
        {
            string key = Key(entry);
            string md5 = Md5(entry.Data);
            if (before.TryGetValue(key, out string old) && old == md5)
            {
                same++;
            }
            else
            {
                diff++;
                Console.WriteLine($"[MAP] 内容变化: {key} {before.GetValueOrDefault(key, "缺失")} -> {md5}");
            }
        }
        Console.WriteLine($"[MAP] 逐文件比对: 相同 {same}, 变化 {diff}");
    }

    private static string Key(MapDocument.Entry entry)
    {
        return entry.Name ?? $"#{entry.NameA:X8}";
    }

    private static string Md5(byte[] data)
    {
        return data == null ? "null" : Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();
    }
}
