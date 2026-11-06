using System;
using Godot;
using WorldEdit.Assets;

namespace WorldEdit.Core.Tools;

/// <summary>资源侧自检：把 BLP 解码成 PNG，用来确认解码器正确，不参与运行逻辑。</summary>
public static class AssetCli
{
    /// <summary>探针：从 MPQ 里按名读文件，确认安装目录能被直接解析。</summary>
    public static void ProbeMpq(string mpqPath, string name)
    {
        try
        {
            using var archive = WorldEdit.Core.Formats.Mpq.MpqArchive.Open(mpqPath);
            Console.WriteLine($"[MPQ] {mpqPath} blocks={archive.Blocks.Count} hash={archive.HashCount}");
            bool has = archive.Contains(name);
            Console.WriteLine($"[MPQ] Contains(\"{name}\") = {has}");
            if (has)
            {
                byte[] data = archive.TryRead(name);
                Console.WriteLine($"[MPQ] 读到 {data?.Length ?? 0} 字节，头 8 字节 {BitConverter.ToString(data, 0, 8)}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[MPQ] 失败 {e.GetType().Name}: {e.Message}");
        }
    }

    /// <summary>把生成的文本表逐张读一遍，打印键数，并抽查几个中文名。</summary>
    public static void DumpTexts()
    {
        Type type = typeof(Assets.Generated.War3Texts);
        var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Array.Sort(methods, (a, b) => string.CompareOrdinal(a.Name, b.Name));
        int tables = 0;
        long keys = 0;
        foreach (var method in methods)
        {
            if (method.GetParameters().Length > 0
                || !typeof(System.Collections.Generic.IReadOnlyDictionary<string, string>).IsAssignableFrom(method.ReturnType))
            {
                continue;
            }
            var map = (System.Collections.Generic.IReadOnlyDictionary<string, string>)method.Invoke(null, null);
            tables++;
            keys += map.Count;
            Console.WriteLine($"[TEXT] {method.Name} keys={map.Count}");
        }
        Console.WriteLine($"[TEXT] 合计 {tables} 张表、{keys} 个键");

        string[] samples =
        {
            "HumanUnitStrings|hfoo.Name",
            "HumanUnitStrings|Hblm.Name",
            "WorldEditStrings|WorldEditStrings.WESTRING_RACE_HUMAN",
        };
        foreach (string sample in samples)
        {
            string[] parts = sample.Split('|');
            var method = type.GetMethod(parts[0]);
            var map = method == null ? null : (System.Collections.Generic.IReadOnlyDictionary<string, string>)method.Invoke(null, null);
            Console.WriteLine($"[TEXT] {sample} = {(map != null && map.TryGetValue(parts[1], out string value) ? value : "(缺)")}");
        }
    }

    /// <summary>把生成的 35 张表逐张读一遍，打印行列数，用来和 slk 原文对照。</summary>
    public static void DumpTables()
    {
        Type type = typeof(Assets.Generated.War3Tables);
        var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Array.Sort(methods, (a, b) => string.CompareOrdinal(a.Name, b.Name));
        foreach (var method in methods)
        {
            if (method.ReturnType != typeof(WorldEdit.Core.Formats.Slk.SlkTable) || method.GetParameters().Length > 0)
            {
                continue;
            }
            var table = (WorldEdit.Core.Formats.Slk.SlkTable)method.Invoke(null, null);
            int rows = 0;
            foreach (int _ in table.DataRows)
            {
                rows++;
            }
            Console.WriteLine($"[TABLE] {method.Name} rows={rows} cols={table.Columns.Count}");
        }
    }

    public static void DumpBlp(string relative, string outPath)
    {
        try
        {
            byte[] raw = War3Assets.Read(relative);
            Image image = BlpDecoder.Decode(raw);
            Error error = image.SavePng(outPath);
            Console.WriteLine($"[BLP] {relative} raw={raw.Length} -> {image.GetWidth()}x{image.GetHeight()} {image.GetFormat()} save={error} -> {outPath}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[BLP] 失败 {relative}: {e.GetType().Name} {e.Message}");
        }
    }

    /// <summary>把任意 Godot 能读的图片（jpg/png/webp…）转成 PNG，用于和参考截图对比。</summary>
    public static void ConvertImage(string source, string target)
    {
        var image = new Image();
        Error error = image.Load(source);
        if (error != Error.Ok)
        {
            Console.WriteLine($"[IMG] 读取失败 {source}: {error}");
            return;
        }
        image.SavePng(target);
        Console.WriteLine($"[IMG] {source} -> {target} {image.GetWidth()}x{image.GetHeight()}");
    }

    /// <summary>按 tileID 查 Terrain.slk 再解码，验证映射链是否通。</summary>
    public static void DumpTile(string tileId, string outDir)
    {
        string relative = TerrainTileSet.PathFor(tileId);
        Console.WriteLine($"[TILE] {tileId} -> {relative ?? "(未在 Terrain.slk 中找到)"}");
        if (relative == null)
        {
            return;
        }
        DumpBlp(relative, $"{outDir}/{tileId}.png");
    }
}
